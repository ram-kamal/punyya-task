param(
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"
$root = (Get-Location).Path
$desktopProj = Join-Path $root "client\src\PHF.Client.Desktop\PHF.Client.Desktop.csproj"
$publishDir = Join-Path $root "client\publish"
$pngIconPath = Join-Path $root "client\src\PHF.Client.Wpf\app.png"
$webDownloadsPublic = Join-Path $root "web\public\downloads"
$webDownloadsOut = Join-Path $root "web\out\downloads"

# Ensure directories exist
if (!(Test-Path $webDownloadsPublic)) { New-Item -ItemType Directory -Path $webDownloadsPublic -Force | Out-Null }
if (!(Test-Path $webDownloadsOut)) { New-Item -ItemType Directory -Path $webDownloadsOut -Force | Out-Null }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Punnya Heart Desk - macOS Bundler    " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Generate app.icns from app.png
$icnsPath = Join-Path $publishDir "app.icns"
if (Test-Path $pngIconPath) {
    Write-Host "[1/4] Generating Apple ICNS icon from app.png..." -ForegroundColor Yellow
    $pngBytes = [System.IO.File]::ReadAllBytes($pngIconPath)
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    $chunkType = [System.Text.Encoding]::ASCII.GetBytes("ic08")
    $chunkLen = [uint32]($pngBytes.Length + 8)
    $totalLen = [uint32](8 + 8 + $pngBytes.Length)

    # Reverse endianness for Big-Endian format
    function To-BigEndian([uint32]$val) {
        $bytes = [System.BitConverter]::GetBytes($val)
        if ([System.BitConverter]::IsLittleEndian) { [System.Array]::Reverse($bytes) }
        return $bytes
    }

    $bw.Write([System.Text.Encoding]::ASCII.GetBytes("icns"))
    $bw.Write((To-BigEndian $totalLen))
    $bw.Write($chunkType)
    $bw.Write((To-BigEndian $chunkLen))
    $bw.Write($pngBytes)

    [System.IO.File]::WriteAllBytes($icnsPath, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()
    Write-Host "  -> Generated $icnsPath" -ForegroundColor Green
}

# 2. Info.plist content
$infoPlistContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Punnya Heart Desk</string>
    <key>CFBundleDisplayName</key>
    <string>Punnya Heart Desk</string>
    <key>CFBundleIdentifier</key>
    <string>org.punnya.heartdesk</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>PunnyaHeartDesk</string>
    <key>CFBundleIconFile</key>
    <string>app.icns</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSRequiresAquaSystemAppearance</key>
    <false/>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
    <key>NSAppleEventsUsageDescription</key>
    <string>Punnya Heart Desk requires accessibility permissions to log active application shifts.</string>
</dict>
</plist>
"@

$targets = @(
    @{ Rid = "osx-arm64"; ArchName = "AppleSilicon"; Display = "Apple Silicon (M1/M2/M3/M4)" },
    @{ Rid = "osx-x64";   ArchName = "Intel";        Display = "Intel Mac (x64)" }
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($t in $targets) {
    $rid = $t.Rid
    $archName = $t.ArchName
    $display = $t.Display
    $outDir = Join-Path $publishDir "mac-$rid"
    $bundleDir = Join-Path $publishDir "bundle-$archName\PunnyaHeartDesk.app"

    Write-Host ""
    Write-Host "[2/4] Publishing $display..." -ForegroundColor Yellow
    if (!$SkipPublish) {
        dotnet publish $desktopProj -r $rid -c Release --self-contained true -o $outDir
    }

    Write-Host "[3/4] Assembling macOS .app bundle for $display..." -ForegroundColor Yellow
    if (Test-Path (Join-Path $publishDir "bundle-$archName")) {
        Remove-Item (Join-Path $publishDir "bundle-$archName") -Recurse -Force
    }

    $contentsDir = Join-Path $bundleDir "Contents"
    $macOsDir = Join-Path $contentsDir "MacOS"
    $resourcesDir = Join-Path $contentsDir "Resources"

    New-Item -ItemType Directory -Path $macOsDir -Force | Out-Null
    New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null

    # Copy binary and runtime files to Contents/MacOS
    Copy-Item -Path "$outDir\*" -Destination $macOsDir -Recurse -Force

    # Write Info.plist and PkgInfo
    [System.IO.File]::WriteAllText((Join-Path $contentsDir "Info.plist"), $infoPlistContent, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllBytes((Join-Path $contentsDir "PkgInfo"), [System.Text.Encoding]::ASCII.GetBytes("APPL????"))

    # Copy app.icns to Contents/Resources
    if (Test-Path $icnsPath) {
        Copy-Item -Path $icnsPath -Destination (Join-Path $resourcesDir "app.icns") -Force
    }

    # Package into ZIP with forward slashes
    Write-Host "[4/4] Creating ZIP archive for $display..." -ForegroundColor Yellow
    $zipFileName = "PunnyaHeartDesk-macOS-$archName.zip"
    $zipPath = Join-Path $publishDir $zipFileName

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    $bundleRoot = Join-Path $publishDir "bundle-$archName"
    $zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)

    Get-ChildItem -Path $bundleRoot -Recurse -Force | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
        $rel = $_.FullName.Substring($bundleRoot.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }

    $zip.Dispose()

    # Copy to web downloads
    Copy-Item -Path $zipPath -Destination (Join-Path $webDownloadsPublic $zipFileName) -Force
    Copy-Item -Path $zipPath -Destination (Join-Path $webDownloadsOut $zipFileName) -Force

    $fileInfo = Get-Item $zipPath
    $sizeMb = [math]::Round($fileInfo.Length / 1MB, 2)
    Write-Host ("  [+] Created " + $zipFileName + " (" + $sizeMb + " MB)") -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " macOS Builds Complete and Packaged!    " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
