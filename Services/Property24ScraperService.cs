using HtmlAgilityPack;
using Newtonsoft.Json;
using PropertySearchAPI.Models;
using System.Text;

namespace PropertySearchAPI.Services
{
    public class Property24ScraperService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public Property24ScraperService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<List<PropertyListing>> Scrape(string propertyUrl)
        {
            Console.WriteLine("==================================");
            Console.WriteLine("STARTING PROPERTY24 SCRAPE");
            Console.WriteLine(propertyUrl);
            Console.WriteLine("==================================");

            List<PropertyListing> results = new();
            List<string> visitedUrls = new();

            int page = 1;
            int maxPages = 50;

            while (page <= maxPages)
            {
                string pageUrl;

                if (page == 1)
                {
                    pageUrl = propertyUrl;
                }
                else
                {
                    if (propertyUrl.Contains("?sp="))
                    {
                        var parts = propertyUrl.Split("?sp=");
                        pageUrl = $"{parts[0]}/p{page}?sp={parts[1]}";
                    }
                    else
                    {
                        pageUrl = $"{propertyUrl}/p{page}";
                    }
                }

                visitedUrls.Add(pageUrl);

                Console.WriteLine($"📡 Fetching Page {page}");
                Console.WriteLine(pageUrl);

                var payload = new
                {
                    source = "universal",
                    url = pageUrl
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://realtime.oxylabs.io/v1/queries"
                );

                var auth = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes(
                        $"{_config["ApiKeys:OxylabsUsername"]}:{_config["ApiKeys:OxylabsPassword"]}"
                    ));

                request.Headers.Add("Authorization", $"Basic {auth}");

                request.Content = new StringContent(
                    JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _http.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ Oxylabs request failed: {response.StatusCode}");
                    break;
                }

                var json = await response.Content.ReadAsStringAsync();

                dynamic obj = JsonConvert.DeserializeObject(json);

                string html = obj.results[0].content;

                if (string.IsNullOrEmpty(html))
                {
                    Console.WriteLine("⚠️ Empty HTML returned");
                    break;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var cards = doc.DocumentNode.SelectNodes("//div[contains(@class,'js_resultTile')]");

                if (cards == null)
                {
                    Console.WriteLine($"⚠️ No listings found on page {page}");
                    break;
                }

                foreach (var card in cards)
                {
                    var title = card.SelectSingleNode(".//span[contains(@class,'p24_title')]")?.InnerText.Trim();
                    var price = card.SelectSingleNode(".//span[contains(@class,'p24_price')]")?.InnerText.Trim();
                    var loc = card.SelectSingleNode(".//span[contains(@class,'p24_location')]")?.InnerText.Trim();

                    var link = card.SelectSingleNode(".//a[contains(@href,'for-sale') or contains(@href,'to-rent')]");

                    var bedrooms = card.SelectSingleNode(".//span[@title='Bedrooms']/span")?.InnerText;
                    var bathrooms = card.SelectSingleNode(".//span[@title='Bathrooms']/span")?.InnerText;

                    results.Add(new PropertyListing
                    {
                        Title = title,
                        Price = price,
                        Location = loc,
                        Bedrooms = bedrooms,
                        Bathrooms = bathrooms,
                        URL = link != null ? "https://www.property24.com" + link.GetAttributeValue("href", "") : null,
                        Page = page
                    });
                }

                Console.WriteLine($"✅ Page {page} scraped ({results.Count} total)");

                page++;

                await Task.Delay(2000);
            }

            Console.WriteLine("🏁 Scraping finished");
            Console.WriteLine($"Listings collected: {results.Count}");

            Console.WriteLine("Visited pages:");

            foreach (var url in visitedUrls)
                Console.WriteLine(url);

            return results;
        }
    }
}