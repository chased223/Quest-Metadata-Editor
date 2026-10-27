$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'QuestMetadataEditor.csproj'
Write-Host 'Building Quest Metadata Editor for Windows x64...' -ForegroundColor Cyan
dotnet publish $project -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
$exe = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\win-x64\publish\QuestMetadataEditor.exe'
if (-not (Test-Path $exe)) {
    throw "Build reported success, but the expected EXE was not found: $exe"
}
Write-Host "Success: $exe" -ForegroundColor Green
