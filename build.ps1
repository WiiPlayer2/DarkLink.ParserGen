#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'

$now = [DateTime]::UtcNow
$versionSuffix = "pre$($now.ToString("yyyyMMddHHmmss"))"

New-Item ./output -ItemType Directory -Force | Out-Null
Remove-Item ./output/* -Recurse -Force | Out-Null
dotnet clean ./DarkLink.ParserGen/DarkLink.ParserGen.csproj `
    --configuration Release `
    --verbosity normal
dotnet build ./DarkLink.ParserGen/DarkLink.ParserGen.csproj `
    --configuration Release `
    --verbosity normal
dotnet pack ./DarkLink.ParserGen/DarkLink.ParserGen.csproj `
    --configuration Release `
    --version-suffix $versionSuffix `
    --output ./output `
    --verbosity minimal
