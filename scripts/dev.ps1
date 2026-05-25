# Charge .env, applique les migrations SQL, lance Rhythmo.Mobile (Windows)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$envFile = Join-Path $Root ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -match '^\s*#' -or $line -eq "") { return }
        if ($line -match '^([^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $val = $matches[2].Trim().Trim('"')
            Set-Item -Path "Env:$name" -Value $val
        }
    }
    Write-Host "Chargé $envFile"
} else {
    Write-Warning "Fichier .env absent — copie .env.example vers .env"
}

Write-Host "Migrations Supabase..."
dotnet run --project (Join-Path $Root "tools\Rhythmo.DbMigrate\Rhythmo.DbMigrate.csproj")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $env:SUPABASE_ANON_KEY) {
    Write-Warning "SUPABASE_ANON_KEY vide : récupère la clé anon dans Supabase → Project Settings → API"
}

Write-Host "Lancement MAUI..."
dotnet run --project (Join-Path $Root "Rhythmo.Mobile\Rhythmo.Mobile.csproj") -f net10.0-windows10.0.19041.0
