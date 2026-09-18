# Build and publish ComputeQuiet (run on Windows with .NET 8 SDK)
$ErrorActionPreference = "Stop"
dotnet build "$PSScriptRoot\ComputeQuiet\ComputeQuiet.csproj" -c Release
dotnet publish "$PSScriptRoot\ComputeQuiet\ComputeQuiet.csproj" -c Release -r win-x64 --self-contained false -o "$PSScriptRoot\publish"
Write-Host "Published to $PSScriptRoot\publish\ComputeQuiet.exe"
