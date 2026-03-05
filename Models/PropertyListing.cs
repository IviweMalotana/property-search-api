namespace PropertySearchAPI.Models
{
    public class PropertyListing
    {
        public string Title { get; set; }

        public string Price { get; set; }

        public string Location { get; set; }

        public string Bedrooms { get; set; }

        public string Bathrooms { get; set; }

        public string URL { get; set; }

        public int Page { get; set; }
    }
}