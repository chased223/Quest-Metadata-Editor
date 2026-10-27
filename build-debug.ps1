$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'QuestMetadataEditor.csproj'
dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }
dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }
