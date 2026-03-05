using Newtonsoft.Json;
using PropertySearchAPI.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace PropertySearchAPI.Services
{
    public class OpenAIIntentService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public OpenAIIntentService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        private Dictionary<string, (string city, string area)> CONTEXT_LOCATION_MAP =
            new()
            {
                { "uct", ("Cape Town", "Rondebosch") },
                { "university of cape town", ("Cape Town", "Rondebosch") },
                { "up", ("Pretoria", "Hatfield") },
                { "university of pretoria", ("Pretoria", "Hatfield") },
                { "wits", ("Johannesburg", "Braamfontein") },
                { "stellenbosch university", ("Stellenbosch", "Stellenbosch") },
                { "nwu", ("Potchefstroom", "Potchefstroom") },
                { "uj", ("Johannesburg", "Auckland Park") },
                { "bishops", ("Cape Town", "Rondebosch") },
                { "herschel", ("Cape Town", "Claremont") },
                { "reddam", ("Cape Town", "Constantia") },
                { "sandton city", ("Johannesburg", "Sandton") },
                { "rosebank mall", ("Johannesburg", "Rosebank") },
                { "canal walk", ("Cape Town", "Century City") },
                { "v&a waterfront", ("Cape Town", "Waterfront") },
                { "gateway mall", ("Durban", "Umhlanga") },
                { "menlyn", ("Pretoria", "Menlyn") }
            };

        private (string city, string area, string enriched) NormalizeLocation(string query)
        {
            var qLower = query.ToLower();

            foreach (var key in CONTEXT_LOCATION_MAP.Keys)
            {
                if (qLower.Contains(key))
                {
                    var info = CONTEXT_LOCATION_MAP[key];

                    string enriched =
                        $"{query} (near {info.area}, {info.city})";

                    Console.WriteLine($"Context match: {key}");

                    return (info.city, info.area, enriched);
                }
            }

            return (null, null, query);
        }

        public async Task<PropertyIntent> ExtractIntent(string userInput)
        {
            Console.WriteLine("===== INTENT PARSER START =====");
            Console.WriteLine($"User Query: {userInput}");

            var (cityHint, areaHint, enriched) =
                NormalizeLocation(userInput);

            Console.WriteLine($"Enriched Query: {enriched}");

            string prompt = $"""
You are a property search assistant.

Analyze this user query:

"{enriched}"

Return ONLY valid JSON with:

- listing_type ("to-rent" or "for-sale")
- property_type (house, apartment, townhouse etc)
- areas (list of suburbs mentioned)
- city
- min_price
- max_price
- bedrooms
- bathrooms
""";

            var body = new
            {
                model = "gpt-4o",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions"
            );

            request.Headers.Add(
                "Authorization",
                $"Bearer {_config["ApiKeys:OpenAI"]}"
            );

            request.Content = new StringContent(
                JsonConvert.SerializeObject(body),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _http.SendAsync(request);

            var json = await response.Content.ReadAsStringAsync();

            Console.WriteLine("OpenAI response received");

            dynamic obj = JsonConvert.DeserializeObject(json);

            string content = obj.choices[0].message.content;

            Console.WriteLine("Raw response:");
            Console.WriteLine(content);

            content = Regex.Replace(content, "^```(json)?", "", RegexOptions.IgnoreCase);
            content = content.Trim('`', '\n', ' ');

            try
            {
                var parsed = JsonConvert.DeserializeObject<PropertyIntent>(content);

                if (parsed.areas == null || parsed.areas.Count == 0)
                    parsed.areas = new List<string> { areaHint ?? cityHint };

                if (parsed.listing_type == null)
                    parsed.listing_type = "to-rent";

                if (parsed.property_type == null)
                    parsed.property_type = "house";

                if (parsed.city == null)
                    parsed.city = cityHint;

                Console.WriteLine("Intent parsed successfully");

                return parsed;
            }
            catch
            {
                Console.WriteLine("JSON parse failed");

                return new PropertyIntent
                {
                    listing_type = "to-rent",
                    property_type = "house",
                    areas = new List<string> { areaHint ?? cityHint },
                    city = cityHint
                };
            }
        }
    }
}