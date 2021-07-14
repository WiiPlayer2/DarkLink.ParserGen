#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'

Push-Location ./output
dotnet nuget push `
    '*.nupkg' `
    --api-key $env:NUGET_API_KEY `
    --source $env:NUGET_SOURCE
Pop-Location
