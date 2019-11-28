using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordProxy
{
    public static class DiscordProxy
    {
        private static HttpClient HttpClient = new HttpClient();
        private static readonly string[] RateLimitHeaderNames = new string[] { "X-RateLimit-Global", "X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset", "X-RateLimit-Reset-After", "X-RateLimit-Bucket" };

        private static MemoryCache InvalidWebhooks = new MemoryCache(new MemoryCacheOptions { ExpirationScanFrequency = TimeSpan.FromMinutes(1)});

        private static void CacheInvalidWebhook(string webhookId, string token, HttpStatusCode status, string body)
        {
            InvalidWebhooks.Set(webhookId + token, (status, body), new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(5)
            });
        }

        [FunctionName("DiscordProxy")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "discord/{webhookid}/{token}")] HttpRequest req,
            string webhookid, string token)
        {
            if(InvalidWebhooks.TryGetValue<(HttpStatusCode status, string body)>(webhookid + token, out var cachedResponse))
                return CreateResponse(cachedResponse.status, cachedResponse.body);

            var content = new StringContent(await req.ReadAsStringAsync(), Encoding.UTF8, "application/json");
            var discordResponse = await HttpClient.PostAsync($"https://discordapp.com/api/webhooks/{webhookid}/{token}", content);

            var discordResponseBody = await discordResponse.Content.ReadAsStringAsync();

            if(discordResponse.StatusCode == HttpStatusCode.NotFound || discordResponse.StatusCode == HttpStatusCode.Unauthorized)
                CacheInvalidWebhook(webhookid, token, discordResponse.StatusCode, discordResponseBody);

            var response = CreateResponse(discordResponse.StatusCode, discordResponseBody);

            for (int i = 0; i < RateLimitHeaderNames.Length; i++)
            {
                var headerName = RateLimitHeaderNames[i];

                if (discordResponse.Headers.TryGetValues(headerName, out var headerValue))
                    response.Headers.TryAddWithoutValidation(headerName, headerValue);
            }
            return response;
        }

        private static HttpResponseMessage CreateResponse(HttpStatusCode status, string body)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
