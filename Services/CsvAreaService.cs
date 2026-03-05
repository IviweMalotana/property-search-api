using CsvHelper;
using PropertySearchAPI.Models;
using System.Globalization;

namespace PropertySearchAPI.Services
{
    public class CsvAreaService
    {
        private readonly List<Area> _areas;

        public CsvAreaService()
        {
            Console.WriteLine("Loading CSV file...");

            var path = "Data/areas.csv";

            Console.WriteLine($"CSV Path: {path}");

            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            _areas = csv.GetRecords<Area>().ToList();

            Console.WriteLine($"Loaded {_areas.Count} areas from CSV");
        }

        public List<Area> GetAllAreas()
        {
            Console.WriteLine("Returning all areas");

            return _areas;
        }

        public Area FindSuburb(string suburb)
        {
            Console.WriteLine($"Searching for suburb: {suburb}");

            var result = _areas
                .FirstOrDefault(x => x.suburb.ToLower().Contains(suburb.ToLower()));

            if (result == null)
                Console.WriteLine("Suburb not found");
            else
                Console.WriteLine($"Found suburb: {result.suburb}");

            return result;
        }
    }
}