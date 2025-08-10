using System;
using System.Collections.Generic;
using System.IO;
using MongoDB.Bson;
using MongoDB.Driver;

class Program
{
    static void Main(string[] args)
    {
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("spatial_analiza");

        var sourceCollection = database.GetCollection<BsonDocument>("opstine_koordinate");

        string outputDir = "centroid_results";
        Directory.CreateDirectory(outputDir);

        var documents = sourceCollection.Find(FilterDefinition<BsonDocument>.Empty).ToList();

        foreach (var doc in documents)
        {
            var areaName = doc.GetValue("area", "").AsString;
            var type = doc.GetValue("type", "").AsString;

            if (type != "MultiPolygon")
                continue;

            var coordinatesArray = doc["coordinates"].AsBsonArray;
            var points = new List<(double X, double Y)>();
            foreach (var coord in coordinatesArray[0].AsBsonArray[0].AsBsonArray)
            {
                double lon = coord.AsBsonArray[0].ToDouble();
                double lat = coord.AsBsonArray[1].ToDouble();
                points.Add((lon, lat));
            }
            var centroid = CalculateCentroid(points);
            var resultDoc = new BsonDocument
            {
                { "area", areaName },
                { "centroid", new BsonDocument
                    {
                        { "type", "Point" },
                        { "coordinates", new BsonArray { centroid.X, centroid.Y } }
                    }
                }
            };
            string fileName = Path.Combine(outputDir, $"{SanitizeFileName(areaName)}.json");
            File.WriteAllText(fileName, resultDoc.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true }));
            Console.WriteLine($"Saved centroid for area '{areaName}' to file: {fileName}");
        }
        Console.WriteLine("All centroids saved as JSON files.");
    }
    public static (double X, double Y) CalculateCentroid(List<(double X, double Y)> points)
    {
        if (points.Count < 3)
            throw new ArgumentException("A polygon must have at least 3 points.");

        if (points[0] != points[points.Count - 1])
            points.Add(points[0]);

        double area = 0, cx = 0, cy = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            double x0 = points[i].X;
            double y0 = points[i].Y;
            double x1 = points[i + 1].X;
            double y1 = points[i + 1].Y;

            double cross = x0 * y1 - x1 * y0;
            area += cross;
            cx += (x0 + x1) * cross;
            cy += (y0 + y1) * cross;
        }
        area *= 0.5;
        if (Math.Abs(area) < 1e-9)
            throw new InvalidOperationException("Polygon area is zero — centroid undefined.");

        cx /= (6 * area);
        cy /= (6 * area);

        return (cx, cy);
    }
    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
