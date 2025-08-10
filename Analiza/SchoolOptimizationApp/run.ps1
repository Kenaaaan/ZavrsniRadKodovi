# School Location Optimization System
# MongoDB Spatial Analysis for Sarajevo

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "   School Location Optimization System" -ForegroundColor Yellow
Write-Host "   MongoDB Spatial Analysis for Sarajevo" -ForegroundColor Yellow  
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting application..." -ForegroundColor Green
Write-Host ""

try {
    dotnet run
} catch {
    Write-Host "Error running application: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Application finished." -ForegroundColor Green
Write-Host "Press any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
