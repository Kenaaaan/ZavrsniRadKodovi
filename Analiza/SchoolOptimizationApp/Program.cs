using System;
using System.Threading.Tasks;
using GIS.SchoolOptimization;

namespace GIS.SchoolOptimization.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // MongoDB connection settings
            string connectionString = "mongodb://localhost:27017";
            string databaseName = "spatial_analiza";
            
            try
            {
                var optimizer = new SchoolLocationOptimizer(connectionString, databaseName);
                
                System.Console.WriteLine("=== SCHOOL LOCATION OPTIMIZATION SYSTEM ===");
                System.Console.WriteLine("Connecting to MongoDB...");
                
                // Display all available municipality centroids
                await optimizer.DisplayAllMunicipalityCentroidsAsync();
                
                System.Console.WriteLine("\n=== STARTING OPTIMIZATION ===");
                
                // Test optimization for different municipalities
                string[] testMunicipalities = { 
                    "NOVO SARAJEVO"
                };
                
                foreach (string municipality in testMunicipalities)
                {
                    System.Console.WriteLine($"\n--- Optimizing for {municipality} ---");
                    
                    try
                    {
                        // Find 3 optimal school locations
                        var optimalLocations = await optimizer.FindOptimalSchoolLocationsAsync(3, municipality);
                        
                        System.Console.WriteLine($"\nOptimal school locations for {municipality}:");
                        for (int i = 0; i < optimalLocations.Count; i++)
                        {
                            var location = optimalLocations[i];
                            System.Console.WriteLine($"School {i + 1}: [Longitude: {location.Coordinates.X:F6}, Latitude: {location.Coordinates.Y:F6}]");
                        }
                        
                        // Analyze the results
                        await optimizer.AnalyzeOptimizationResultsAsync(optimalLocations, municipality);
                        
                        System.Console.WriteLine("Press any key to continue to next municipality...");
                        System.Console.ReadKey();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error optimizing for {municipality}: {ex.Message}");
                    }
                }
                
                System.Console.WriteLine("\n=== INTERACTIVE MODE ===");
                System.Console.WriteLine("Available municipalities:");
                System.Console.WriteLine("1. CENTAR SARAJEVO");
                System.Console.WriteLine("2. HADŽIĆI");
                System.Console.WriteLine("3. ILIDŽA");
                System.Console.WriteLine("4. ILIJAŠ");
                System.Console.WriteLine("5. NOVI GRAD SARAJEVO");
                System.Console.WriteLine("6. NOVO SARAJEVO");
                System.Console.WriteLine("7. STARI GRAD SARAJEVO");
                System.Console.WriteLine("8. TRNOVO");
                System.Console.WriteLine("9. VOGOŠĆA");
                
                while (true)
                {
                    System.Console.WriteLine("\nEnter municipality name (or 'quit' to exit):");
                    string? input = System.Console.ReadLine();
                    
                    if (input?.ToLower() == "quit")
                        break;
                    
                    if (string.IsNullOrWhiteSpace(input))
                        continue;
                    
                    System.Console.WriteLine("Enter number of schools to optimize:");
                    if (int.TryParse(System.Console.ReadLine(), out int numberOfSchools) && numberOfSchools > 0)
                    {
                        try
                        {
                            var optimalLocations = await optimizer.FindOptimalSchoolLocationsAsync(numberOfSchools, input!.ToUpper());
                            
                            System.Console.WriteLine($"\nOptimal locations for {numberOfSchools} schools in {input!.ToUpper()}:");
                            for (int i = 0; i < optimalLocations.Count; i++)
                            {
                                var location = optimalLocations[i];
                                System.Console.WriteLine($"School {i + 1}: [Longitude: {location.Coordinates.X:F6}, Latitude: {location.Coordinates.Y:F6}]");
                            }
                            
                            await optimizer.AnalyzeOptimizationResultsAsync(optimalLocations, input!.ToUpper());
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    else
                    {
                        System.Console.WriteLine("Please enter a valid number of schools.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Fatal error: {ex.Message}");
                System.Console.WriteLine("Press any key to exit...");
                System.Console.ReadKey();
            }
        }
    }
}
