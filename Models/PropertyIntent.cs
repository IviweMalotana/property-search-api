namespace PropertySearchAPI.Models
{
    public class PropertyIntent
    {
        public string listing_type { get; set; }

        public string property_type { get; set; }

        public List<string> areas { get; set; }

        public string city { get; set; }

        public int? min_price { get; set; }

        public int? max_price { get; set; }

        public int? bedrooms { get; set; }

        public int? bathrooms { get; set; }
    }
}