using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace PropertySearchAPI.Services
{
    public class OxylabsFetchService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public OxylabsFetchService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<string> FetchHtml(string url)
        {
            Console.WriteLine("=====================================");
            Console.WriteLine("OXylabs Fetch Starting");
            Console.WriteLine($"Target URL: {url}");
            Console.WriteLine("=====================================");

            var username = _config["ApiKeys:OxylabsUsername"];
            var password = _config["ApiKeys:OxylabsPassword"];

            Console.WriteLine($"Username loaded: {username}");
            Console.WriteLine($"Password exists: {!string.IsNullOrEmpty(password)}");

            var payload = new
            {
                source = "universal",
                url = url
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://realtime.oxylabs.io/v1/queries"
            );

            var auth = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{username}:{password}")
            );

            request.Headers.Add("Authorization", $"Basic {auth}");

            request.Content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _http.SendAsync(request);

            Console.WriteLine($"Oxylabs status: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Raw Oxylabs response length: " + json.Length);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("❌ Oxylabs request failed");
                Console.WriteLine(json);
                return "ERROR";
            }

            try
            {
                var obj = JObject.Parse(json);

                var results = obj["results"];

                if (results == null || !results.Any())
                {
                    Console.WriteLine("❌ No results returned by Oxylabs");
                    return "ERROR";
                }

                var html = results[0]["content"]?.ToString();

                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine("❌ HTML content missing");
                    return "ERROR";
                }

                Console.WriteLine($"HTML length: {html.Length}");

                return html;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Failed to parse Oxylabs response");
                Console.WriteLine(ex.Message);
                Console.WriteLine(json);

                return "ERROR";
            }
        }
    }
}
