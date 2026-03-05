using Microsoft.AspNetCore.Mvc;
using PropertySearchAPI.Services;
using PropertySearchAPI.Models;
using HtmlAgilityPack;

namespace PropertySearchAPI.Controllers
{
    [ApiController]
    [Route("api/property")]
    public class PropertyController : ControllerBase
    {
        private readonly SearchService _search;
        private readonly CsvAreaService _csv;
        private readonly OpenAIIntentService _intent;
        private readonly Property24ScraperService _scraper;
        private readonly OxylabsFetchService _fetch;

        public PropertyController(
            SearchService search,
            CsvAreaService csv,
            OpenAIIntentService intent,
            Property24ScraperService scraper,
            OxylabsFetchService fetch)
        {
            _search = search;
            _csv = csv;
            _intent = intent;
            _scraper = scraper;
            _fetch = fetch;
        }

        // ------------------------------------------------
        // BASIC TEST
        // ------------------------------------------------
        [HttpGet("search")]
        public IActionResult Search(string query)
        {
            Console.WriteLine("========== BASIC SEARCH ==========");
            Console.WriteLine($"Query: {query}");

            var result = _search.RunSearch(query);

            Console.WriteLine("Search service completed");
            Console.WriteLine("==================================");

            return Ok(result);
        }

        // ------------------------------------------------
        // CSV AREAS
        // ------------------------------------------------
        [HttpGet("areas")]
        public IActionResult GetAreas()
        {
            Console.WriteLine("========== CSV AREAS ==========");

            var areas = _csv.GetAllAreas();

            Console.WriteLine($"Total areas loaded: {areas.Count}");

            return Ok(areas);
        }

        // ------------------------------------------------
        // FIND SUBURB
        // ------------------------------------------------
        [HttpGet("find-suburb")]
        public IActionResult FindSuburb(string suburb)
        {
            Console.WriteLine("========== FIND SUBURB ==========");
            Console.WriteLine($"Searching for suburb: {suburb}");

            var result = _csv.FindSuburb(suburb);

            return Ok(result);
        }

        // ------------------------------------------------
        // FULL PROPERTY PIPELINE
        // ------------------------------------------------
        [HttpGet("intent")]
        public async Task<IActionResult> ExtractIntent(string query)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("🚀 PROPERTY SEARCH PIPELINE STARTED");
            Console.WriteLine($"User Query: {query}");
            Console.WriteLine("==============================================");

            // STEP 1: INTENT
            Console.WriteLine("STEP 1: Parsing intent...");
            var intent = await _intent.ExtractIntent(query);

            Console.WriteLine($"Listing Type: {intent.listing_type}");
            Console.WriteLine($"Property Type: {intent.property_type}");
            Console.WriteLine($"City: {intent.city}");
            Console.WriteLine($"Areas: {string.Join(",", intent.areas ?? new List<string>())}");

            if (intent.areas == null || intent.areas.Count == 0)
                return Ok(new { error = "No areas detected" });

            // STEP 2: CSV LOOKUP
            Console.WriteLine("STEP 2: CSV lookup...");
            var suburb = intent.areas.First();

            var areaRow = _csv.FindSuburb(suburb);

            if (areaRow == null)
            {
                Console.WriteLine("❌ Suburb not found in CSV");
                return Ok(new { error = "Suburb not found in CSV" });
            }

            Console.WriteLine($"CSV match: {areaRow.city} - {areaRow.suburb}");

            // STEP 3: BUILD PROPERTY24 URL
            Console.WriteLine("STEP 3: Building Property24 URL...");

            string basePath = intent.listing_type == "for-sale"
                ? areaRow.sale_href
                : areaRow.href;

            string propertyUrl = $"https://www.property24.com{basePath}";

            List<string> parameters = new();

            if (intent.min_price != null)
                parameters.Add($"pf%3d{intent.min_price}");

            if (intent.max_price != null)
                parameters.Add($"pt%3d{intent.max_price}");

            if (intent.bedrooms != null)
                parameters.Add($"bd%3d{intent.bedrooms}");

            if (intent.bathrooms != null)
                parameters.Add($"bth%3d{intent.bathrooms}");

            if (parameters.Count > 0)
                propertyUrl += "?sp=" + string.Join("%26", parameters);

            Console.WriteLine($"Property24 URL: {propertyUrl}");

            // ------------------------------------------------
            // STEP 4: SCRAPE LISTINGS
            // ------------------------------------------------

            List<object> listings = new();

            int page = 1;
            int maxPages = 50;

            while (page <= maxPages)
            {
                string pageUrl;

                if (page == 1)
                    pageUrl = propertyUrl;
                else
                {
                    if (propertyUrl.Contains("?sp="))
                    {
                        var split = propertyUrl.Split("?sp=");
                        pageUrl = $"{split[0]}/p{page}?sp={split[1]}";
                    }
                    else
                    {
                        pageUrl = $"{propertyUrl}/p{page}";
                    }
                }

                Console.WriteLine($"📡 Fetching page {page}: {pageUrl}");

                var html = await _fetch.FetchHtml(pageUrl);

                if (html == "ERROR")
                {
                    Console.WriteLine("❌ Oxylabs failed");
                    break;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var cards = doc.DocumentNode.SelectNodes("//div[contains(@class,'js_resultTile')]");

                if (cards == null)
                {
                    Console.WriteLine("⚠️ No listings found, stopping pagination");
                    break;
                }

                Console.WriteLine($"Listings found on page {page}: {cards.Count}");

                foreach (var card in cards)
                {
                    var price = card.SelectSingleNode(".//span[contains(@class,'p24_price')]")?.InnerText.Trim();
                    var title = card.SelectSingleNode(".//span[contains(@class,'p24_title')]")?.InnerText.Trim();
                    var suburbText = card.SelectSingleNode(".//span[contains(@class,'p24_location')]")?.InnerText.Trim();
                    var address = card.SelectSingleNode(".//span[contains(@class,'p24_address')]")?.InnerText.Trim();

                    var bedrooms = card.SelectSingleNode(".//span[@title='Bedrooms']/span")?.InnerText.Trim();
                    var bathrooms = card.SelectSingleNode(".//span[@title='Bathrooms']/span")?.InnerText.Trim();

                    var linkNode = card.SelectSingleNode(".//a[contains(@class,'p24_content')]");
                    var imgNode = card.SelectSingleNode(".//img[contains(@class,'js_P24_listingImage')]");

                    var link = linkNode != null
                        ? "https://www.property24.com" + linkNode.GetAttributeValue("href", "")
                        : null;

                    var image = imgNode?.GetAttributeValue("src", "");

                    listings.Add(new
                    {
                        title,
                        price,
                        suburb = suburbText,
                        address,
                        bedrooms,
                        bathrooms,
                        image,
                        link,
                        page
                    });
                }

                page++;

                await Task.Delay(2000);
            }

            Console.WriteLine("==============================================");
            Console.WriteLine($"🏁 SCRAPING COMPLETE - {listings.Count} listings");
            Console.WriteLine("==============================================");

            return Ok(new
            {
                intent,
                property24_url = propertyUrl,
                total = listings.Count,
                listings
            });
        }

        // ------------------------------------------------
        // DEBUG FETCH
        // ------------------------------------------------
        [HttpGet("fetch-html")]
        public async Task<IActionResult> FetchHtml(string url)
        {
            Console.WriteLine("================================");
            Console.WriteLine("FETCH HTML ENDPOINT");
            Console.WriteLine(url);
            Console.WriteLine("================================");

            var html = await _fetch.FetchHtml(url);

            return Ok(new
            {
                html_length = html.Length,
                html
            });
        }
    }
}