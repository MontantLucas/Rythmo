# Extrait uniquement les cles client (jamais le mot de passe Postgres ni service_role).
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = "Stop"
$keep = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[void]$keep.Add("SUPABASE_URL")
[void]$keep.Add("SUPABASE_ANON_KEY")
[void]$keep.Add("SUPABASE_PROJECT_REF")

if (-not (Test-Path $Source)) {
    throw "Fichier introuvable : $Source"
}

$out = [System.Collections.Generic.List[string]]::new()
$out.Add("# Client-only env packaged into the APK. Never include DB password or service_role.")

Get-Content -LiteralPath $Source | ForEach-Object {
    $line = $_.Trim()
    if ($line.Length -eq 0 -or $line.StartsWith("#")) { return }
    $eq = $line.IndexOf("=")
    if ($eq -le 0) { return }
    $name = $line.Substring(0, $eq).Trim()
    $val = $line.Substring($eq + 1).Trim().Trim([char]34)
    if ($keep.Contains($name)) {
        $out.Add("$name=$val")
    }
}

$destDir = Split-Path -Parent $Destination
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir | Out-Null
}

$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllLines($Destination, $out, $utf8)
Write-Host "Ecrit $Destination (cles client uniquement)"
