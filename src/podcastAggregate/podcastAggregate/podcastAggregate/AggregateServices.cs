using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using podcastAggregate.dto;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Linq;

namespace podcastAggregate
{
    public class AggregateServices
    {
        public dynamic GetFeeds(string[] feeds)
        {
            var feedArray = new List<Feed>();
            foreach (var feed in feeds)
            {
                if (string.IsNullOrEmpty(feed))
                {
                    continue;
                }

                var feedData = GetFeed(feed).Result;

                var xmlDoc = new XmlDocument();

                xmlDoc.LoadXml(feedData);

                var json = JsonConvert.SerializeXmlNode(xmlDoc);

                feedArray.Add(JsonConvert.DeserializeObject<Feed>(json));
            }
            var items = feedArray.SelectMany(x => x.rss.channel.item);

            return items.OrderBy(x => x.pubDate);
        }

        private static async Task<string> GetFeed(string feed)
        {
            var uri = new Uri(feed);
            var baseAddress = feed.Replace(uri.AbsolutePath, "");
            var client = new HttpClient();
            client.BaseAddress = new Uri(baseAddress);
            HttpResponseMessage response = await client.GetAsync(feed);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}