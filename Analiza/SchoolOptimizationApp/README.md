# School Location Optimization System

## 📁 Project Structure

```
SchoolOptimizationApp/
├── SchoolLocationOptimizer.cs    # Main optimization algorithm
├── Program.cs                   # Console application entry point
├── SchoolOptimization.csproj    # Project configuration
├── SchoolOptimization.sln       # Visual Studio solution file
├── .gitignore                   # Git ignore patterns
├── run.bat                      # Windows batch script to run the app
├── run.ps1                      # PowerShell script to run the app
├── README.md                    # This documentation
├── bin/                         # Build output directory
└── obj/                         # Build cache directory
```

## Overview

This C# application implements an advanced school location optimization algorithm for Sarajevo municipalities using MongoDB spatial data. The system uses Gaussian distribution modeling to weight population density and considers existing school locations to find optimal placement for new schools.

## Features

### 🎯 Core Optimization Algorithm

- **Gaussian Distribution Weighting**: Population density decreases with distance from municipality center
- **Multi-constraint Optimization**: Considers existing schools, population coverage, and spatial distribution
- **Iterative Placement**: Each new school placement considers previously placed schools
- **Grid Search + Local Refinement**: Systematic exploration with fine-tuning

### 📊 Spatial Analysis

- **Real Municipality Centroids**: Uses actual calculated centroids from `centar_opstina` collection
- **Haversine Distance Calculations**: Accurate geographic distance measurements
- **Coverage Radius Analysis**: Configurable school service area (default 2.5km)
- **Population Coverage Scoring**: Exponential decay based on distance

### 🏫 School Placement Constraints

- **Minimum Distance Between Schools**: 2km minimum spacing
- **Existing School Avoidance**: Heavy penalties for placement near existing schools
- **Population Density Priority**: Higher weights for areas with more students
- **Central Location Preference**: Slight bonus for central municipality locations

## Database Collections

### Required MongoDB Collections:

1. **`centar_opstina`** - Municipality centroids with 2dsphere index
2. **`skolarci_koordinate`** - Population/student data with coordinates
3. **`skole_geo`** - Existing school locations

## Configuration Parameters

```csharp
MIN_DISTANCE_BETWEEN_SCHOOLS_KM = 2.0    // Minimum spacing between schools
GAUSSIAN_SIGMA = 3.0                     // Standard deviation for population weighting
COVERAGE_RADIUS_KM = 2.5                 // Effective service radius
```

## Available Municipalities

The system supports optimization for all 9 Sarajevo municipalities:

- **CENTAR SARAJEVO**: [18.417221, 43.900405]
- **HADŽIĆI**: [18.146261, 43.771696]
- **ILIDŽA**: [18.273158, 43.824487]
- **ILIJAŠ**: [18.430087, 44.011035]
- **NOVI GRAD SARAJEVO**: [18.318360, 43.870706]
- **NOVO SARAJEVO**: [18.387752, 43.856713]
- **STARI GRAD SARAJEVO**: [18.460457, 43.899434]
- **TRNOVO**: [18.373487, 43.677402]
- **VOGOŠĆA**: [18.357204, 43.926924]

## Installation & Setup

### Prerequisites

- .NET 6.0 or higher
- MongoDB 4.4+ with spatial data
- MongoDB.Driver NuGet package

### Setup Steps

## 🚀 Quick Start

### Easy Run Options:

1. **Visual Studio**: Open `SchoolOptimization.sln` in Visual Studio → Press F5 to run
2. **Windows Batch File**: Double-click `run.bat`
3. **PowerShell Script**: Right-click `run.ps1` → "Run with PowerShell"
4. **Command Line**:
   ```bash
   cd SchoolOptimizationApp
   dotnet run
   ```

### For Visual Studio Users:

- **Open in VS**: Double-click `SchoolOptimization.sln` in the project folder
- **Alternative**: Open the parent `ZavrsniRad.sln` from the root directory
- **Build**: Ctrl+Shift+B or Build → Build Solution
- **Run**: F5 (with debugging) or Ctrl+F5 (without debugging)

## Installation & Setup

### Prerequisites

- .NET 6.0 or higher
- MongoDB 4.4+ with spatial data
- MongoDB.Driver NuGet package

### Setup Steps

1. **Ensure MongoDB is running** with the `spatial_analiza` database
2. **Verify collections exist**: `centar_opstina`, `skolarci_koordinate`, `skole_geo`
3. **Install dependencies**:
   ```bash
   dotnet restore
   ```
4. **Build the project**:
   ```bash
   dotnet build
   ```
5. **Run the application**:
   ```bash
   dotnet run
   ```

## Usage Examples

### Basic Optimization

```csharp
var optimizer = new SchoolLocationOptimizer("mongodb://localhost:27017", "spatial_analiza");

// Find 3 optimal schools in Centar Sarajevo
var locations = await optimizer.FindOptimalSchoolLocationsAsync(3, "CENTAR SARAJEVO");

foreach (var location in locations)
{
    Console.WriteLine($"School: [Lon: {location.Coordinates.X:F6}, Lat: {location.Coordinates.Y:F6}]");
}
```

### Comprehensive Analysis

```csharp
// Display all municipality centroids
await optimizer.DisplayAllMunicipalityCentroidsAsync();

// Run optimization with analysis
var optimalLocations = await optimizer.FindOptimalSchoolLocationsAsync(2, "NOVI GRAD SARAJEVO");
await optimizer.AnalyzeOptimizationResultsAsync(optimalLocations, "NOVI GRAD SARAJEVO");
```

## Algorithm Workflow

1. **Data Extraction**:

   - Load population data from `skolarci_koordinate`
   - Load existing schools from `skole_geo`
   - Get municipality centroid from `centar_opstina`

2. **Gaussian Weighting**:

   - Calculate distance from each population point to municipality center
   - Apply Gaussian distribution: `weight = exp(-(distance²)/(2σ²))`
   - Weight population by distance decay

3. **Grid Search**:

   - Create systematic grid around municipality center
   - Evaluate each grid point using comprehensive scoring

4. **Location Scoring**:

   - **Population Coverage**: Weighted sum of served population
   - **Distance Penalties**: Heavy penalties for proximity to existing schools
   - **Spacing Optimization**: Maintain minimum distance between new schools
   - **Centrality Bonus**: Slight preference for central locations

5. **Local Refinement**:

   - Fine-tune best locations using hill-climbing algorithm
   - 8-directional search with decreasing step size

6. **Iterative Placement**:
   - Place schools one by one
   - Each placement considers all previously placed schools

## Output Analysis

The system provides detailed analysis including:

- **Municipality center coordinates**
- **Number of population areas analyzed**
- **Existing schools count**
- **Population served by each new school**
- **Total population coverage**
- **Optimization scores and refinement steps**

## Mathematical Foundation

### Gaussian Distribution

Population weight decreases with distance using:

```
weight(d) = exp(-d²/(2σ²))
```

Where `d` is distance from center, `σ` is standard deviation (3km default).

### Haversine Distance

Accurate geographic distance calculation:

```
d = 2R * arcsin(√(sin²(Δlat/2) + cos(lat1) * cos(lat2) * sin²(Δlon/2)))
```

### Location Score

Comprehensive scoring considers:

- Population coverage with exponential distance decay
- Distance penalties from existing schools
- Spacing optimization between new schools
- Centrality bonuses

## Performance Characteristics

- **Grid Search**: 21×21 = 441 candidate locations per school
- **Local Refinement**: Up to 100 iterations of 8-directional search
- **Memory Efficient**: Processes data in batches
- **Scalable**: Linear complexity with number of schools

## Customization

You can modify the algorithm parameters:

```csharp
// In SchoolLocationOptimizer.cs
private const double GAUSSIAN_SIGMA = 3.0;        // Population spread
private const double COVERAGE_RADIUS_KM = 2.5;    // School service area
private const double MIN_DISTANCE_BETWEEN_SCHOOLS_KM = 2.0;  // Minimum spacing
```

## Troubleshooting

### Common Issues:

1. **MongoDB Connection**: Ensure MongoDB is running on localhost:27017
2. **Missing Collections**: Verify all required collections exist
3. **Index Missing**: Ensure 2dsphere index exists on `centar_opstina.centroid`
4. **Data Format**: Check that coordinates are in [longitude, latitude] format

### Debug Output:

The application provides verbose logging including:

- Connection status
- Data loading progress
- Optimization steps
- Score calculations
- Final results analysis

## License

This project is developed for academic research purposes at the Faculty of Electrical Engineering, University of Sarajevo.
