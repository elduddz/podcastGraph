using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Xml;
using podcastGraph.dto;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using static Gremlin.Net.Process.Traversal.AnonymousTraversalSource;
using static Gremlin.Net.Process.Traversal.__;
using static Gremlin.Net.Process.Traversal.P;
using static Gremlin.Net.Process.Traversal.Order;
using static Gremlin.Net.Process.Traversal.Operator;
using static Gremlin.Net.Process.Traversal.Pop;
using static Gremlin.Net.Process.Traversal.Scope;
using static Gremlin.Net.Process.Traversal.TextP;
using static Gremlin.Net.Process.Traversal.Column;
using static Gremlin.Net.Process.Traversal.Direction;
using static Gremlin.Net.Process.Traversal.T;
using Gremlin.Net.Driver.Remote;
using Gremlin.Net.CosmosDb;

namespace podcastGraph
{
    public static class FeedIndexer
    {
        [FunctionName("FeedIndexer")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string feed = req.Query["feed"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            feed = feed ?? data?.feed;

            GetFeed(feed);

            return new OkObjectResult("");
        }

        private static void GetFeed(string feed)
        {
            var xml = new XmlDocument();

            using (var httpClient = new HttpClient())
            {
                var result = httpClient.GetStreamAsync(feed);

                xml.Load(result.Result);

                var channel = xml.GetElementsByTagName("channel").Item(0);
                var feedTitle = channel.SelectSingleNode("title").InnerText;
                var url = channel.SelectSingleNode("url").InnerText;

                var feedId = StorePodcastFeed(feedTitle, url);

                var items = xml.GetElementsByTagName("item");

                foreach(XmlNode item in items)
                {
                    var entry = new FeedEntry()
                    {
                        FeedId = feedId,
                        Title = item.SelectSingleNode("title").InnerText,
                        DatePublished = DateTime.ParseExact(item.SelectSingleNode("pubDate").InnerText, "ddd, dd MMM yyyy HH:mm:ss EDT", null),
                        Description = item.SelectSingleNode("description").InnerText,
                        SourceUrl = item.SelectSingleNode("source").Attributes["url"].InnerText
                    };

                    StorePodcastEntry(entry);
                }
            }
        }

        private static void StorePodcastEntry(FeedEntry entry)
        {
            using(var gremlinClient = GraphConnection())
            {
                var g = Traversal().WithRemote(new DriverRemoteConnection(gremlinClient));

                var v = gremlinClient.SubmitWithSingleResultAsync<dynamic>($"g.{g.V().Has("episode", "Title", entry.Title).ToGremlinQuery()}").Result;
                if (v == null)
                {
                    var command = g.AddV("episode").Property("Title", entry.Title)
                        .Property("Description", entry.Description)
                        .Property("DatePublished", entry.DatePublished)
                        .Property("SourceUrl", entry.SourceUrl).ToGremlinQuery();

                    v = gremlinClient.SubmitWithSingleResultAsync<dynamic>($"g.{command}").Result;
                }

                gremlinClient.SubmitAsync($"g.V('{v["id"]}').AddE('feed').To(__.V('{entry.FeedId}'))").Wait();
            }
        }

        private static string StorePodcastFeed(string title, string url)
        {
            using (var gremlinClient = GraphConnection())
            {
                var g = Traversal().WithRemote(new DriverRemoteConnection(gremlinClient));
                var v = gremlinClient.SubmitWithSingleResultAsync<dynamic>($"g.{g.V().Has("feed", "Title", title).ToGremlinQuery()}").Result;
                if (v == null)
                {
                    var command = g.AddV("feed").Property("Title", title);
                    v = gremlinClient.SubmitWithSingleResultAsync<dynamic>($"g.{command.ToGremlinQuery()}").Result;
                }

                return v["id"];

            }
        }

            private static GremlinClient GraphConnection()
        {
            string connectionString, password, databaseId, containerId;

            connectionString = Environment.GetEnvironmentVariable("connectionString");
            password = Environment.GetEnvironmentVariable("key");
            databaseId = Environment.GetEnvironmentVariable("databaseId");
            containerId = Environment.GetEnvironmentVariable("containerId");

            var gremlinServer = new GremlinServer(connectionString, 443, true, $"/dbs/{databaseId}/colls/{containerId}", password);
            var gremlinClient = new GremlinClient(gremlinServer, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);

            return gremlinClient;
        }
    }
}
