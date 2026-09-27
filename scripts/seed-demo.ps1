# RTF Financial Core — seed professional demo data into the configured cloud Postgres.
# Usage (from repo root):
#   .\scripts\seed-demo.ps1
# Optional:
#   $env:CORE_POSTGRES_CONNECTION = "Host=...;..."

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tools\Core.DemoSeeder\Core.DemoSeeder.csproj"

Write-Host "Running demo seeder..." -ForegroundColor Cyan
dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Seeder failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Done." -ForegroundColor Green
