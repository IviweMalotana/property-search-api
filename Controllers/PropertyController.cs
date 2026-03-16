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
        private readonly OpenAIImageService _images;
        private readonly IConfiguration _config;

        public PropertyController(
            SearchService search,
            CsvAreaService csv,
            OpenAIIntentService intent,
            Property24ScraperService scraper,
            OxylabsFetchService fetch,
            OpenAIImageService images,
            IConfiguration config)
        {
            _search = search;
            _csv = csv;
            _intent = intent;
            _scraper = scraper;
            _fetch = fetch;
            _images = images;
            _config = config;
        }

        // ------------------------------------------------
        // BASIC SEARCH
        // ------------------------------------------------

        [HttpGet("search")]
        public IActionResult Search(string query)
        {
            var result = _search.RunSearch(query);
            return Ok(result);
        }

        // ------------------------------------------------
        // CSV AREAS
        // ------------------------------------------------

        [HttpGet("areas")]
        public IActionResult GetAreas()
        {
            var areas = _csv.GetAllAreas();
            return Ok(areas);
        }

        // ------------------------------------------------
        // FIND SUBURB
        // ------------------------------------------------

        [HttpGet("find-suburb")]
        public IActionResult FindSuburb(string suburb)
        {
            var result = _csv.FindSuburb(suburb);
            return Ok(result);
        }

        // ------------------------------------------------
        // LIST BOTTLES
        // ------------------------------------------------

        [HttpGet("bottles")]
        public IActionResult GetBottles()
        {
            var bottles = _images.GetBottleFamilies();
            return Ok(bottles);
        }

        // ------------------------------------------------
        // LIST BOTTLE TYPES
        // ------------------------------------------------

        [HttpGet("bottle-types")]
        public IActionResult GetBottleTypes(string bottle)
        {
            var types = _images.GetBottleTypes(bottle);
            return Ok(types);
        }

        // ------------------------------------------------
        // GENERATE IMAGE
        // ------------------------------------------------

        [HttpPost("generate-image")]
        public async Task<IActionResult> GenerateImage(
            string bottle,
            string type)
        {
            try
            {
                var base64 = await _images.GenerateBottleImage(bottle, type);

                return Ok(new
                {
                    bottle,
                    type,
                    image_base64 = base64
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ------------------------------------------------
        // PROPERTY SEARCH PIPELINE (UNCHANGED)
        // ------------------------------------------------

        [HttpGet("intent")]
        public async Task<IActionResult> ExtractIntent(string query)
        {
            var intent = await _intent.ExtractIntent(query);

            if (intent.areas == null || intent.areas.Count == 0)
                return Ok(new { error = "No areas detected" });

            var suburb = intent.areas.First();

            var areaRow = _csv.FindSuburb(suburb);

            if (areaRow == null)
                return Ok(new { error = "Suburb not found in CSV" });

            string basePath = intent.listing_type == "for-sale"
                ? areaRow.sale_href
                : areaRow.href;

            string propertyUrl = $"https://www.property24.com{basePath}";

            List<object> listings = new();

            var html = await _fetch.FetchHtml(propertyUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var cards = doc.DocumentNode.SelectNodes("//div[contains(@class,'js_resultTile')]");

            if (cards != null)
            {
                foreach (var card in cards)
                {
                    var price = card.SelectSingleNode(".//span[contains(@class,'p24_price')]")?.InnerText.Trim();
                    var title = card.SelectSingleNode(".//span[contains(@class,'p24_title')]")?.InnerText.Trim();

                    listings.Add(new
                    {
                        title,
                        price
                    });
                }
            }

            return Ok(new
            {
                intent,
                property24_url = propertyUrl,
                listings
            });
        }

        // ------------------------------------------------
        // DEBUG FETCH
        // ------------------------------------------------

        [HttpGet("fetch-html")]
        public async Task<IActionResult> FetchHtml(string url)
        {
            var html = await _fetch.FetchHtml(url);

            return Ok(new
            {
                html_length = html.Length,
                html
            });
        }
    }
}