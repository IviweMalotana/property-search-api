using Newtonsoft.Json;
using System.Text;

namespace PropertySearchAPI.Services
{
    public class OpenAIImageService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public OpenAIImageService(
            HttpClient http,
            IConfiguration config,
            IWebHostEnvironment env)
        {
            _http = http;
            _config = config;
            _env = env;
        }

        public List<string> GetBottleFamilies()
        {
            var root = Path.Combine(_env.ContentRootPath, "Data", "Images");

            if (!Directory.Exists(root))
                return new List<string>();

            return Directory
                .GetDirectories(root)
                .Select(Path.GetFileName)
                .ToList();
        }

        public List<string> GetBottleTypes(string bottle)
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "Images", bottle);

            if (!Directory.Exists(path))
                return new List<string>();

            return Directory
                .GetFiles(path)
                .Select(x => Path.GetFileNameWithoutExtension(x))
                .ToList();
        }

        public async Task<string> GenerateBottleImage(string bottle, string type)
        {
            Console.WriteLine("===== IMAGE GENERATION SERVICE =====");
            Console.WriteLine($"Bottle: {bottle}");
            Console.WriteLine($"Type: {type}");

            var imagePath = Path.Combine(
                _env.ContentRootPath,
                "Data",
                "Images",
                bottle,
                $"{type}.png"
            );

            if (!File.Exists(imagePath))
                throw new Exception("Bottle reference image not found");

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var base64Image = Convert.ToBase64String(imageBytes);

            string prompt = """
Use the exact bottle shape and pump dispenser from the reference image.
Do not change the bottle structure, silhouette, proportions, neck, base, or pump shape in any way.
The bottle geometry must remain identical to the reference bottle.

Maintain the same frosted matte glass bottle material shown in the reference image, with soft diffused reflections.

Fill the bottle with an opaque creamy white serum, subtly visible through the frosted glass.

Apply minimal modern skincare branding printed directly onto the bottle.

The brand name should be:

BDP

Typography must use Montserrat font.

Use only one color for all text:

#b1abac (soft neutral grey)

Do not introduce any other text colors.

Follow a minimal Rhode-style skincare aesthetic:

• large empty space
• simple layout
• modern typography
• no decorative graphics

Text layout:

BDP

GLOW SERUM

brightening + hydration treatment

vitamin C + peptides

30 ml / 1 fl oz

Render as ultra-realistic cosmetic product photography, studio lighting, frosted glass texture, creamy white serum inside, subtle shadows, clean neutral background, premium Sephora-style skincare packaging.
""";

            var body = new
            {
                model = "gpt-image-1",
                prompt = prompt,
                image = base64Image,
                size = "1024x1024"
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/images/edits"
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

            dynamic obj = JsonConvert.DeserializeObject(json);

            string image = obj.data[0].b64_json;

            Console.WriteLine("Image generated successfully");

            return image;
        }
    }
}