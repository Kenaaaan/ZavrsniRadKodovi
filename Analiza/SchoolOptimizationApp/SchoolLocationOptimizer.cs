using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;

namespace GIS.SchoolOptimization
{
    public class SchoolLocationOptimizer
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<BsonDocument> _skolarciCollection;
        private readonly IMongoCollection<BsonDocument> _skoleCollection;
        private readonly IMongoCollection<BsonDocument> _centarOpstineCollection;

        // Configuration parameters for the optimization algorithm
        private const int numberOfRandomPointsGenerated = 1000; // Number of random points to generate inside each polygon
        private const double EARTH_RADIUS_KM = 6371.0;
        private const double MIN_DISTANCE_BETWEEN_SCHOOLS_KM = 1.5; // Minimum distance between schools
        private const double GAUSSIAN_SIGMA = 3.0; // Standard deviation for Gaussian distribution (km)
        private const double COVERAGE_RADIUS_KM = 2.5; // Effective coverage radius of a school
        private const int MAX_ITERATIONS = 1000; // Maximum iterations for optimization

        public SchoolLocationOptimizer(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
            _skolarciCollection = _database.GetCollection<BsonDocument>("skolarci_koordinate");
            _skoleCollection = _database.GetCollection<BsonDocument>("skole_geo");
            _centarOpstineCollection = _database.GetCollection<BsonDocument>("centar_opstina");
        }

        // Enhanced method that uses real centroid data from centar_opstina collection
        private async Task<GeoJsonPoint<GeoJson2DCoordinates>> CalculatePolygonCentroid(string areaName)
        {
            try
            {
                // Query the centar_opstina collection for the specific area
                var filter = Builders<BsonDocument>.Filter.Eq("area", areaName.ToUpper());
                var centroidDoc = await _centarOpstineCollection.Find(filter).FirstOrDefaultAsync();

                if (centroidDoc != null && centroidDoc.Contains("centroid"))
                {
                    var centroidData = centroidDoc["centroid"].AsBsonDocument;
                    var coordinates = centroidData["coordinates"].AsBsonArray;

                    double longitude = coordinates[0].AsDouble;
                    double latitude = coordinates[1].AsDouble;

                    return new GeoJsonPoint<GeoJson2DCoordinates>(
                        new GeoJson2DCoordinates(longitude, latitude)
                    );
                }
                else
                {
                    // Fallback to default Sarajevo center if area not found
                    System.Console.WriteLine($"Area '{areaName}' not found in centar_opstina collection. Using default center.");
                    throw new Exception(
                        $"Area '{areaName}' not found in centar_opstina collection. Using default center."
                    );
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error retrieving centroid for area '{areaName}': {ex.Message}");
                // Return default Sarajevo center as fallback
                throw new Exception(
                    $"Error retrieving centroid for area '{areaName}': {ex.Message}. Using default center."
                );
            }
        }

        // Enhanced optimization algorithm with Gaussian distribution and existing school consideration
        public async Task<List<GeoJsonPoint<GeoJson2DCoordinates>>> FindOptimalSchoolLocationsAsync(
            int numberOfSchools,
            string targetMunicipality = "NOVO SARAJEVO")
        {
            System.Console.WriteLine($"Starting optimization for {numberOfSchools} schools in {targetMunicipality}...");

            var populationAreas = await GetPopulationAreasAsync();
            var existingSchools = await GetExistingSchoolsAsync();

            // Get the real centroid for the target municipality
            var municipalityCentroid = await CalculatePolygonCentroid(targetMunicipality);
            double centerLon = municipalityCentroid.Coordinates.X;
            double centerLat = municipalityCentroid.Coordinates.Y;


            // Extract population points with Gaussian weighting for the target municipality only
            var weightedPopulationPoints = await ExtractWeightedPopulationPointsAsync(targetMunicipality, centerLon, centerLat);

            // Extract existing school locations
            var existingSchoolLocations = ExtractExistingSchoolLocations(existingSchools);
            System.Console.WriteLine($"Found {existingSchoolLocations.Count} existing schools");

            // Initialize optimal locations list
            var optimalLocations = new List<GeoJsonPoint<GeoJson2DCoordinates>>();

            // Use iterative optimization algorithm
            for (int i = 0; i < numberOfSchools; i++)
            {

                var bestLocation = await FindBestLocationIterativeAsync(
                    weightedPopulationPoints,
                    existingSchoolLocations,
                    optimalLocations,
                    centerLon,
                    centerLat,
                    targetMunicipality
                );

                optimalLocations.Add(bestLocation);
            }

            return optimalLocations;
        }

        // Extract population points with Gaussian weighting for a specific municipality
        private async Task<List<WeightedPopulationPoint>> ExtractWeightedPopulationPointsAsync(
            string targetMunicipality,
            double centerLon,
            double centerLat)
        {
            var weightedPoints = new List<WeightedPopulationPoint>();

            // Filter population areas for only the target municipality
            var filter = Builders<BsonDocument>.Filter.Eq("area", targetMunicipality.ToUpper());
            var municipalityAreas = await _skolarciCollection.Find(filter).ToListAsync();

            foreach (var area in municipalityAreas)
            {
                if (area.Contains("geometry") && area.Contains("totalChildren"))
                {
                    try
                    {
                        var geometry = area["geometry"].AsBsonDocument;
                        var coordinates = geometry["coordinates"].AsBsonArray;
                        int totalPopulation = area["totalChildren"].AsInt32;

                        // Extract polygon boundary coordinates
                        var polygonBoundary = ExtractPolygonCoordinates(coordinates);
                        if (polygonBoundary.Count == 0) continue;

                        // Generate random points inside the polygon using MongoDB GeoSpatial queries
                        var interiorPoints = await GenerateRandomPointsInsidePolygonAsync(targetMunicipality, polygonBoundary, numberOfRandomPointsGenerated);
                        if (interiorPoints.Count == 0) continue;

                        // Distribute population evenly across all interior points
                        double populationPerPoint = (double)totalPopulation / interiorPoints.Count;

                        System.Console.WriteLine($"Processing area with {totalPopulation} children using {interiorPoints.Count} MongoDB-verified points ({populationPerPoint:F1} per point)");

                        // Create weighted population point for each random interior point
                        foreach (var point in interiorPoints)
                        {
                            double lon = point.longitude;
                            double lat = point.latitude;

                            // Calculate distance from municipality center
                            double distanceFromCenter = CalculateHaversineDistance(centerLon, centerLat, lon, lat);

                            // Apply Gaussian distribution weighting
                            double gaussianWeight = CalculateGaussianWeight(distanceFromCenter, GAUSSIAN_SIGMA);
                            double weightedPopulation = populationPerPoint * gaussianWeight;

                            weightedPoints.Add(new WeightedPopulationPoint
                            {
                                Longitude = lon,
                                Latitude = lat,
                                Population = (int)Math.Round(populationPerPoint),
                                WeightedPopulation = weightedPopulation,
                                DistanceFromCenter = distanceFromCenter
                            });
                        }

                        System.Console.WriteLine($"Created {interiorPoints.Count} weighted points inside the polygon using MongoDB GeoSpatial queries");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error processing population area: {ex.Message}");
                    }
                }
            }

            // Sort by weighted population (highest first)
            return weightedPoints.OrderByDescending(p => p.WeightedPopulation).ToList();
        }

        // Generate random points inside a polygon using MongoDB GeoSpatial queries
        private async Task<List<(double longitude, double latitude)>> GenerateRandomPointsInsidePolygonAsync(
            string targetMunicipality,
            List<(double longitude, double latitude)> polygonBoundary,
            int targetPointCount)
        {
            var interiorPoints = new List<(double longitude, double latitude)>();
            var random = new Random();

            if (polygonBoundary.Count < 3) return interiorPoints;

            // Calculate bounding box of the polygon
            double minLon = polygonBoundary.Min(p => p.longitude);
            double maxLon = polygonBoundary.Max(p => p.longitude);
            double minLat = polygonBoundary.Min(p => p.latitude);
            double maxLat = polygonBoundary.Max(p => p.latitude);


            int attempts = 0;
            int maxAttempts = targetPointCount * 100; // More attempts for complex polygons

            while (interiorPoints.Count < targetPointCount && attempts < maxAttempts)
            {
                // Generate random point within bounding box
                double randomLon = minLon + (maxLon - minLon) * random.NextDouble();
                double randomLat = minLat + (maxLat - minLat) * random.NextDouble();

                // Check if point is inside the polygon using MongoDB GeoSpatial query
                if (await IsPointInPolygonAsync(randomLon, randomLat, targetMunicipality))
                {
                    interiorPoints.Add((randomLon, randomLat));
                }

                attempts++;

                // Progress indicator
                if (attempts % 1000 == 0 && interiorPoints.Count > 0)
                {
                }
            }
            return interiorPoints;
        }

        // Check if a point is inside a polygon using MongoDB GeoSpatial query
        private async Task<bool> IsPointInPolygonAsync(double testLon, double testLat, string targetMunicipality)
        {
            try
            {
                // First get the polygon coordinates for the municipality
                var filter = Builders<BsonDocument>.Filter.Eq("area", targetMunicipality.ToUpper());
                var municipalityDoc = await _skolarciCollection.Find(filter).FirstOrDefaultAsync();

                if (municipalityDoc == null || !municipalityDoc.Contains("geometry"))
                    return false;

                var geometry = municipalityDoc["geometry"].AsBsonDocument;
                var coordinates = geometry["coordinates"].AsBsonArray;

                // Extract polygon boundary coordinates
                var polygonBoundary = ExtractPolygonCoordinates(coordinates);
                if (polygonBoundary.Count < 3) return false;

                // Use improved Winding Number Algorithm for precise point-in-polygon testing
                return IsPointInPolygonWindingNumber(testLon, testLat, polygonBoundary);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in point-in-polygon test: {ex.Message}");
                return false;
            }
        }

        // Winding Number Algorithm - more precise for complex/concave polygons
        private bool IsPointInPolygonWindingNumber(double testLon, double testLat, List<(double longitude, double latitude)> polygon)
        {
            int windingNumber = 0;
            int n = polygon.Count;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                double x1 = polygon[i].longitude;
                double y1 = polygon[i].latitude;
                double x2 = polygon[next].longitude;
                double y2 = polygon[next].latitude;

                if (y1 <= testLat)
                {
                    if (y2 > testLat) // Upward crossing
                    {
                        double cross = (x2 - x1) * (testLat - y1) - (testLon - x1) * (y2 - y1);
                        if (cross > 0) // Point is left of edge
                            windingNumber++;
                    }
                }
                else
                {
                    if (y2 <= testLat) // Downward crossing
                    {
                        double cross = (x2 - x1) * (testLat - y1) - (testLon - x1) * (y2 - y1);
                        if (cross < 0) // Point is right of edge
                            windingNumber--;
                    }
                }
            }

            return windingNumber != 0;
        }

        // Extract all coordinate points from a polygon
        private List<(double longitude, double latitude)> ExtractPolygonCoordinates(BsonArray coordinates)
        {
            var points = new List<(double longitude, double latitude)>();

            try
            {
                // Handle MultiPolygon structure: coordinates[0][0] contains the polygon points
                if (coordinates.Count > 0 && coordinates[0].IsBsonArray)
                {
                    var outerArray = coordinates[0].AsBsonArray;
                    if (outerArray.Count > 0 && outerArray[0].IsBsonArray)
                    {
                        var polygonArray = outerArray[0].AsBsonArray;

                        // Extract all coordinate points from the polygon
                        foreach (var pointArray in polygonArray)
                        {
                            if (pointArray.IsBsonArray)
                            {
                                var point = pointArray.AsBsonArray;
                                if (point.Count >= 2)
                                {
                                    double lon = point[0].AsDouble;
                                    double lat = point[1].AsDouble;
                                    points.Add((lon, lat));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error extracting polygon coordinates: {ex.Message}");
            }

            return points;
        }

        // Extract existing school locations
        private List<SchoolLocation> ExtractExistingSchoolLocations(List<BsonDocument> existingSchools)
        {
            var schoolLocations = new List<SchoolLocation>();

            foreach (var school in existingSchools)
            {
                if (school.Contains("location"))
                {
                    try
                    {
                        var location = school["location"].AsBsonDocument;
                        var coordinates = location["coordinates"].AsBsonArray;

                        double lon = coordinates[0].AsDouble;
                        double lat = coordinates[1].AsDouble;
                        string name = school.Contains("Naziv") ? school["Naziv"].AsString : "Unknown School";

                        schoolLocations.Add(new SchoolLocation
                        {
                            Longitude = lon,
                            Latitude = lat,
                            Name = name
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error processing existing school: {ex.Message}");
                    }
                }
            }

            return schoolLocations;
        }

        // Iterative algorithm to find the best location by evaluating all Monte Carlo points
        private async Task<GeoJsonPoint<GeoJson2DCoordinates>> FindBestLocationIterativeAsync(
            List<WeightedPopulationPoint> populationPoints,
            List<SchoolLocation> existingSchools,
            List<GeoJsonPoint<GeoJson2DCoordinates>> alreadyPlacedSchools,
            double centerLon,
            double centerLat,
            string targetMunicipality)
        {
            double bestScore = double.MinValue;
            double bestLon = centerLon;
            double bestLat = centerLat;


            // Evaluate each Monte Carlo generated point
            foreach (var point in populationPoints)
            {
                double candidateLon = point.Longitude;
                double candidateLat = point.Latitude;

                // Calculate score for this candidate location
                double score = CalculateLocationScore(
                    candidateLon, candidateLat,
                    populationPoints,
                    existingSchools,
                    alreadyPlacedSchools,
                    centerLon, centerLat
                );

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLon = candidateLon;
                    bestLat = candidateLat;
                }
            }

            // Refine the best location using local optimization  
            var refinedLocation = await RefineLocationLocallyAsync(bestLon, bestLat, populationPoints, existingSchools, alreadyPlacedSchools, centerLon, centerLat, targetMunicipality);


            return new GeoJsonPoint<GeoJson2DCoordinates>(
                new GeoJson2DCoordinates(refinedLocation.Item1, refinedLocation.Item2)
            );
        }

        // Calculate a comprehensive score for a potential school location
        private double CalculateLocationScore(
            double candidateLon, double candidateLat,
            List<WeightedPopulationPoint> populationPoints,
            List<SchoolLocation> existingSchools,
            List<GeoJsonPoint<GeoJson2DCoordinates>> alreadyPlacedSchools,
            double centerLon, double centerLat)
        {
            double score = 0.0;

            // 1. Population coverage score (higher weight for closer, denser populations)
            foreach (var popPoint in populationPoints)
            {
                double distance = CalculateHaversineDistance(candidateLon, candidateLat, popPoint.Longitude, popPoint.Latitude);
                if (distance <= COVERAGE_RADIUS_KM)
                {
                    // Exponential decay based on distance
                    double coverageScore = popPoint.WeightedPopulation * Math.Exp(-distance / COVERAGE_RADIUS_KM);
                    score += coverageScore * 2;
                }
            }

            // 2. Distance from existing schools (penalty for being too close)
            foreach (var existingSchool in existingSchools)
            {
                double distance = CalculateHaversineDistance(candidateLon, candidateLat, existingSchool.Longitude, existingSchool.Latitude);
                if (distance < MIN_DISTANCE_BETWEEN_SCHOOLS_KM)
                {
                    // Heavy penalty for being too close to existing schools
                    score -= 1000 * (MIN_DISTANCE_BETWEEN_SCHOOLS_KM - distance);
                }
            }

            // 3. Distance from already placed new schools
            foreach (var placedSchool in alreadyPlacedSchools)
            {
                double distance = CalculateHaversineDistance(candidateLon, candidateLat, placedSchool.Coordinates.X, placedSchool.Coordinates.Y);
                if (distance < MIN_DISTANCE_BETWEEN_SCHOOLS_KM)
                {
                    // Heavy penalty for being too close to other new schools
                    score -= 10000 * (MIN_DISTANCE_BETWEEN_SCHOOLS_KM - distance);
                }
            }

            // 4. Distance from municipality center (slight preference for central locations)
            double distanceFromCenter = CalculateHaversineDistance(candidateLon, candidateLat, centerLon, centerLat);
            double centralityBonus = Math.Exp(-distanceFromCenter / (2 * GAUSSIAN_SIGMA)) * 100;
            score += centralityBonus;

            return score;
        }

        // Local optimization to refine the location with polygon boundary checking
        private async Task<(double, double)> RefineLocationLocallyAsync(
            double startLon, double startLat,
            List<WeightedPopulationPoint> populationPoints,
            List<SchoolLocation> existingSchools,
            List<GeoJsonPoint<GeoJson2DCoordinates>> alreadyPlacedSchools,
            double centerLon, double centerLat,
            string targetMunicipality)
        {
            double bestLon = startLon;
            double bestLat = startLat;
            double bestScore = CalculateLocationScore(startLon, startLat, populationPoints, existingSchools, alreadyPlacedSchools, centerLon, centerLat);

            double step = 0.002; // Slightly larger step for better exploration  
            bool improved = true;
            int iterations = 0;


            while (improved && iterations < 10) // Reduced iterations to avoid long processing
            {
                improved = false;
                iterations++;

                // Try 8 directions around current best location
                double[] deltaLons = { -step, -step, -step, 0, 0, step, step, step };
                double[] deltaLats = { -step, 0, step, -step, step, -step, 0, step };

                for (int i = 0; i < 8; i++)
                {
                    double newLon = bestLon + deltaLons[i];
                    double newLat = bestLat + deltaLats[i];

                    // Check if new location is still within the target municipality polygon
                    bool isInside = await IsPointInPolygonAsync(newLon, newLat, targetMunicipality);
                    if (!isInside) continue; // Skip if outside polygon

                    double score = CalculateLocationScore(newLon, newLat, populationPoints, existingSchools, alreadyPlacedSchools, centerLon, centerLat);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestLon = newLon;
                        bestLat = newLat;
                        improved = true;
                        System.Console.WriteLine($"Improved to [{bestLon:F6}, {bestLat:F6}] with score: {bestScore:F2}");
                    }
                }
                
                // Reduce step size for fine-tuning in later iterations
                if (iterations > 25) step = 0.001;
            }

            System.Console.WriteLine($"Local optimization completed after {iterations} iterations. Final location: [{bestLon:F6}, {bestLat:F6}]");
            return (bestLon, bestLat);
        }

        // Calculate Gaussian weight based on distance
        private double CalculateGaussianWeight(double distance, double sigma)
        {
            return Math.Exp(-(distance * distance) / (2 * sigma * sigma));
        }

        // Calculate Haversine distance between two points
        private double CalculateHaversineDistance(double lon1, double lat1, double lon2, double lat2)
        {
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EARTH_RADIUS_KM * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        // Method to get all available municipality centroids
        public async Task<Dictionary<string, GeoJsonPoint<GeoJson2DCoordinates>>> GetAllMunicipalityCentroidsAsync()
        {
            var centroids = new Dictionary<string, GeoJsonPoint<GeoJson2DCoordinates>>();

            try
            {
                var cursor = await _centarOpstineCollection.FindAsync(new BsonDocument());
                var documents = await cursor.ToListAsync();

                foreach (var doc in documents)
                {
                    if (doc.Contains("area") && doc.Contains("centroid"))
                    {
                        string area = doc["area"].AsString;
                        var centroidData = doc["centroid"].AsBsonDocument;
                        var coordinates = centroidData["coordinates"].AsBsonArray;

                        double longitude = coordinates[0].AsDouble;
                        double latitude = coordinates[1].AsDouble;

                        centroids[area] = new GeoJsonPoint<GeoJson2DCoordinates>(
                            new GeoJson2DCoordinates(longitude, latitude)
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error retrieving municipality centroids: {ex.Message}");
            }

            return centroids;
        }

        // Helper method to display all municipality centroids
        public async Task DisplayAllMunicipalityCentroidsAsync()
        {
            var centroids = await GetAllMunicipalityCentroidsAsync();

            System.Console.WriteLine("Municipality Centroids:");
            System.Console.WriteLine("=======================");

            foreach (var kvp in centroids)
            {
                var coords = kvp.Value.Coordinates;
                System.Console.WriteLine($"{kvp.Key}: [Longitude: {coords.X:F6}, Latitude: {coords.Y:F6}]");
            }
        }

        // Alternative method using direct coordinate extraction
        public async Task DisplayAllMunicipalityCentroidsAlternativeAsync()
        {
            try
            {
                var cursor = await _centarOpstineCollection.FindAsync(new BsonDocument());
                var documents = await cursor.ToListAsync();

                System.Console.WriteLine("Municipality Centroids (Alternative method):");
                System.Console.WriteLine("=============================================");

                foreach (var doc in documents)
                {
                    if (doc.Contains("area") && doc.Contains("centroid"))
                    {
                        string area = doc["area"].AsString;
                        var centroidData = doc["centroid"].AsBsonDocument;
                        var coordinates = centroidData["coordinates"].AsBsonArray;

                        double longitude = coordinates[0].AsDouble;
                        double latitude = coordinates[1].AsDouble;

                        System.Console.WriteLine($"{area}: [Longitude: {longitude:F6}, Latitude: {latitude:F6}]");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error displaying centroids: {ex.Message}");
            }
        }

        public async Task<List<BsonDocument>> GetPopulationAreasAsync()
        {
            try
            {
                var cursor = await _skolarciCollection.FindAsync(new BsonDocument());
                return await cursor.ToListAsync();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error retrieving population areas: {ex.Message}");
                return new List<BsonDocument>();
            }
        }

        public async Task<List<BsonDocument>> GetExistingSchoolsAsync()
        {
            try
            {
                var cursor = await _skoleCollection.FindAsync(new BsonDocument());
                return await cursor.ToListAsync();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error retrieving existing schools: {ex.Message}");
                return new List<BsonDocument>();
            }
        }

        // Method to analyze optimization results
        public async Task AnalyzeOptimizationResultsAsync(List<GeoJsonPoint<GeoJson2DCoordinates>> optimalLocations, string targetMunicipality)
        {
            System.Console.WriteLine("\n=== OPTIMIZATION RESULTS ANALYSIS ===");

            // Get only population areas for the target municipality
            var filter = Builders<BsonDocument>.Filter.Eq("area", targetMunicipality.ToUpper());
            var populationAreas = await _skolarciCollection.Find(filter).ToListAsync();
            var existingSchools = await GetExistingSchoolsAsync();
            var municipalityCentroid = await CalculatePolygonCentroid(targetMunicipality);

            System.Console.WriteLine($"Target Municipality: {targetMunicipality}");
            System.Console.WriteLine($"Municipality Center: [{municipalityCentroid.Coordinates.X:F6}, {municipalityCentroid.Coordinates.Y:F6}]");
            System.Console.WriteLine($"Number of New Schools: {optimalLocations.Count}");
            System.Console.WriteLine($"Total Existing Schools: {existingSchools.Count}");

            // Calculate total population served
            double totalPopulationServed = 0;
            foreach (var location in optimalLocations)
            {
                double populationServed = 0;
                foreach (var area in populationAreas)
                {
                    if (area.Contains("geometry") && area.Contains("totalChildren"))
                    {
                        var geometry = area["geometry"].AsBsonDocument;
                        var coordinates = geometry["coordinates"].AsBsonArray;

                        // Handle MultiPolygon structure: coordinates[0][0][0] contains [lng, lat]
                        double lon, lat;
                        if (coordinates.Count > 0 && coordinates[0].IsBsonArray)
                        {
                            var outerArray = coordinates[0].AsBsonArray;
                            if (outerArray.Count > 0 && outerArray[0].IsBsonArray)
                            {
                                var polygonArray = outerArray[0].AsBsonArray;
                                if (polygonArray.Count > 0 && polygonArray[0].IsBsonArray)
                                {
                                    var firstPoint = polygonArray[0].AsBsonArray;
                                    if (firstPoint.Count >= 2)
                                    {
                                        lon = firstPoint[0].AsDouble;
                                        lat = firstPoint[1].AsDouble;
                                    }
                                    else
                                    {
                                        continue; // Skip invalid coordinates
                                    }
                                }
                                else
                                {
                                    continue; // Skip invalid structure
                                }
                            }
                            else
                            {
                                continue; // Skip invalid structure
                            }
                        }
                        else
                        {
                            continue; // Skip invalid structure
                        }

                        double distance = CalculateHaversineDistance(
                            location.Coordinates.X, location.Coordinates.Y,
                            lon, lat
                        );

                        if (distance <= COVERAGE_RADIUS_KM)
                        {
                            populationServed += area["totalChildren"].AsInt32;
                        }
                    }
                }
                totalPopulationServed += populationServed;
            }

            System.Console.WriteLine("=========================================\n");
        }

        // Helper classes for data structures
        public class WeightedPopulationPoint
        {
            public double Longitude { get; set; }
            public double Latitude { get; set; }
            public int Population { get; set; }
            public double WeightedPopulation { get; set; }
            public double DistanceFromCenter { get; set; }
        }

        public class SchoolLocation
        {
            public double Longitude { get; set; }
            public double Latitude { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
