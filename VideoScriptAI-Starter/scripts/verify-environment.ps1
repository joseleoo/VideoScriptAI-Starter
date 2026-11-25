$ErrorActionPreference = "Stop"

Write-Host "Checking .NET..."
dotnet --version

Write-Host "Checking Node..."
node --version
npm --version

Write-Host "Checking FFmpeg..."
ffmpeg -version

Write-Host ""
Write-Host "If all checks passed, build the backend:"
Push-Location "$PSScriptRoot\backend"
dotnet restore
dotnet build
Pop-Location

Write-Host ""
Write-Host "Install frontend dependencies:"
Push-Location "$PSScriptRoot\frontend"
npm install
Pop-Location

Write-Host "Starter validation complete."
