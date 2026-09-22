Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$sourceDir = (Resolve-Path "web\out").Path
$zipPath = Join-Path (Get-Location) "punnya_task_portal.zip"

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
    Write-Host "Removed old zip."
}

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)

Get-ChildItem -Path $sourceDir -Recurse -Force | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
    $relativePath = $_.FullName.Substring($sourceDir.Length + 1).Replace('\', '/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relativePath, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
}

$zip.Dispose()

Write-Host "ZIP created successfully with forward slashes!"
Get-Item $zipPath | Select-Object Name, @{Name="SizeMB"; Expression={[math]::Round($_.Length / 1MB, 2)}}, LastWriteTime
