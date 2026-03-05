using PropertySearchAPI.Models;

namespace PropertySearchAPI.Services
{
    public class SearchService
    {
        public SearchResult RunSearch(string query)
        {
            Console.WriteLine("Service running...");
            Console.WriteLine($"Query received: {query}");

            return new SearchResult
            {
                Query = query,
                Message = $"Search executed successfully for '{query}'",
                Time = DateTime.UtcNow
            };
        }
    }
}