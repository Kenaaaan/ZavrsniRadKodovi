using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SchoolDataProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            string inputJsonPath = @"C:\Users\kenan\Downloads\export.geojson";
            string outputJsonPath = @"C:\Users\kenan\Downloads\novi.geojson";

            try
            {
                string jsonText = File.ReadAllText(inputJsonPath);
                JObject originalData = JObject.Parse(jsonText);

                var jednostavneSkole = KreirajJednostavneSkole(originalData);

                var skoleBesPotpunihPodataka = jednostavneSkole.Where(s =>
                    string.IsNullOrEmpty(s.Naziv) ||
                    s.Latitude == 0 ||
                    s.Longitude == 0).ToList();

                string noviJson = JsonConvert.SerializeObject(jednostavneSkole, Formatting.Indented);
                File.WriteAllText(outputJsonPath, noviJson);

                Console.WriteLine($"Podaci o lokacijama škola su sačuvani u {outputJsonPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greška prilikom obrade podataka: {ex.Message}");
            }
        }
        static List<SkolaLokacija> KreirajJednostavneSkole(JObject originalData)
        {
            var skole = new List<SkolaLokacija>();

            var features = originalData["features"] as JArray;

            if (features == null)
                return skole;

            foreach (var feature in features)
            {
                var properties = feature["properties"] as JObject;
                var geometry = feature["geometry"] as JObject;

                if (geometry == null || geometry["coordinates"] == null)
                    continue;

                var koordinate = geometry["coordinates"] as JArray;
                if (koordinate == null || koordinate.Count < 2)
                    continue;

                string naziv = properties?["name"]?.ToString() ?? "Unknown";
                double longitude = koordinate[0].Value<double>();
                double latitude = koordinate[1].Value<double>();

                var skola = new SkolaLokacija
                {
                    Naziv = naziv,
                    Latitude = latitude,
                    Longitude = longitude
                };
                skole.Add(skola);
            }
            return skole;
        }
    }
    public class SkolaLokacija
    {
        public string Naziv { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}