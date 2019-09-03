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
using System.Text;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace DiscordProxy
{
    public static class DiscordProxy
    {
        private static HttpClient HttpClient = new HttpClient();
        private static readonly string[] RateLimitHeaderNames = new string[]{ "X-RateLimit-Global","X-RateLimit-Limit","X-RateLimit-Remaining","X-RateLimit-Reset","X-RateLimit-Reset-After","X-RateLimit-Bucket" };

        [FunctionName("DiscordProxy")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "discord/{webhookid}/{token}")] HttpRequest req,
            string webhookid, string token,
            ILogger log)
        {
            var body = await req.ReadAsStringAsync();

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var discordResponse = await HttpClient.PostAsync($"https://discordapp.com/api/webhooks/{webhookid}/{token}", content);

            var discordResponseBody = await discordResponse.Content.ReadAsStringAsync();

            var response = new HttpResponseMessage(discordResponse.StatusCode) {
                Content = new StringContent(discordResponseBody, Encoding.UTF8, "application/json")
            };

            IEnumerable<string> headerValues = new List<string>();
            for (int i = 0; i < RateLimitHeaderNames.Length; i++)
            {
                var headerName = RateLimitHeaderNames[i];
                if(discordResponse.Headers.TryGetValues(headerName, out headerValues))
                    response.Headers.TryAddWithoutValidation(headerName, headerValues);
            }
            return response;
        }
    }
}
