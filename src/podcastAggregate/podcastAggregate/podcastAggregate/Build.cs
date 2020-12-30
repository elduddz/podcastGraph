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

namespace podcastAggregate
{
    public static class Build
    {
        private static AggregateConfiguration _config;

        [FunctionName("Build")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            log.LogInformation("C# HTTP trigger function processed a request.");

            try
            {
                _config = ReadConfig();
                var services = new AggregateServices();

                var returns = services.GetFeeds(_config.Feeds);

                return new OkObjectResult(returns);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);
            }

            return new BadRequestObjectResult("Didnt work, check you are on the internet");
        }

        private static AggregateConfiguration ReadConfig()
        {
            return new AggregateConfiguration
            {
                Feeds = Environment.GetEnvironmentVariable("Feeds", EnvironmentVariableTarget.Process)?.Split(';')
            };
        }
    }
}
