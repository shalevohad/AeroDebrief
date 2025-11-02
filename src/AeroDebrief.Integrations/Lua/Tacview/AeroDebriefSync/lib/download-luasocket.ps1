# Download and Install LuaSocket to lib Folder
# This script downloads LuaSocket from the official GitHub releases
# and extracts it to the correct folder structure

param(
    [string]$Version = "3.0-rc1",
    [string]$Platform = "win64"
)

# Colors for output
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }

Write-Info "=========================================="
Write-Info "LuaSocket Downloader for AeroDebrief Sync"
Write-Info "=========================================="
Write-Info ""

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$LibDir = $ScriptDir

Write-Info "Target directory: $LibDir"
Write-Info "Version: $Version"
Write-Info "Platform: $Platform"
Write-Info ""

# Check if we're in the right directory
if (-not (Test-Path (Join-Path $ScriptDir ".." "main.lua"))) {
    Write-Error "ERROR: This script must be run from the lib/ folder!"
    Write-Error "Current location: $ScriptDir"
    Write-Error "Expected: AeroDebriefSync/lib/"
    exit 1
}

# LuaSocket download URL
$DownloadUrl = "https://github.com/lunarmodules/luasocket/archive/refs/tags/v$Version.zip"
$TempZip = Join-Path $env:TEMP "luasocket-$Version.zip"
$TempExtract = Join-Path $env:TEMP "luasocket-$Version-extract"

Write-Info "Step 1: Downloading LuaSocket $Version..."
Write-Info "URL: $DownloadUrl"

try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempZip -UseBasicParsing
    Write-Success "? Download complete"
} catch {
    Write-Error "? Failed to download LuaSocket"
    Write-Error $_.Exception.Message
    Write-Warning ""
    Write-Warning "Please download manually from:"
    Write-Warning "https://github.com/lunarmodules/luasocket/releases"
    exit 1
}

Write-Info ""
Write-Info "Step 2: Extracting archive..."

try {
    # Remove old extraction if exists
    if (Test-Path $TempExtract) {
        Remove-Item $TempExtract -Recurse -Force
    }
    
    Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force
    Write-Success "? Extraction complete"
} catch {
    Write-Error "? Failed to extract archive"
    Write-Error $_.Exception.Message
    exit 1
}

Write-Info ""
Write-Info "Step 3: Locating files..."

# Find the extracted folder (name varies by version)
$ExtractedFolder = Get-ChildItem $TempExtract | Where-Object { $_.PSIsContainer } | Select-Object -First 1

if (-not $ExtractedFolder) {
    Write-Error "? Could not find extracted folder"
    exit 1
}

$SourceDir = $ExtractedFolder.FullName
Write-Info "Source: $SourceDir"

Write-Info ""
Write-Info "Step 4: Copying files..."

# Copy Lua files
$FilesToCopy = @(
    @{Source="src/socket.lua"; Dest="socket.lua"},
    @{Source="src/mime.lua"; Dest="mime.lua"},
    @{Source="src/ltn12.lua"; Dest="ltn12.lua"},
    @{Source="src/http.lua"; Dest="socket/http.lua"},
    @{Source="src/smtp.lua"; Dest="socket/smtp.lua"},
    @{Source="src/tp.lua"; Dest="socket/tp.lua"},
    @{Source="src/url.lua"; Dest="socket/url.lua"},
    @{Source="src/ftp.lua"; Dest="socket/ftp.lua"}
)

$CopiedFiles = 0
foreach ($file in $FilesToCopy) {
    $sourcePath = Join-Path $SourceDir $file.Source
    $destPath = Join-Path $LibDir $file.Dest
    
    if (Test-Path $sourcePath) {
        # Create destination directory if needed
        $destDir = Split-Path $destPath -Parent
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        
        Copy-Item $sourcePath $destPath -Force
        Write-Success "  ? Copied: $($file.Dest)"
        $CopiedFiles++
    } else {
        Write-Warning "  ? Not found: $($file.Source)"
    }
}

Write-Info ""
Write-Info "Step 5: Binary files (.dll)..."
Write-Warning "? NOTE: Source distribution doesn't include pre-compiled binaries"
Write-Warning ""
Write-Warning "You need to:"
Write-Warning "1. Download pre-compiled binaries from:"
Write-Warning "   https://github.com/lunarmodules/luasocket/releases"
Write-Warning ""
Write-Warning "2. Look for Windows x64 binaries (.dll files)"
Write-Warning ""
Write-Warning "3. Copy these files:"
Write-Warning "   - socket/core.dll ? $LibDir\socket\core.dll"
Write-Warning "   - mime/core.dll ? $LibDir\mime\core.dll"
Write-Warning ""
Write-Warning "Or build from source using Visual Studio"

Write-Info ""
Write-Info "Step 6: Cleanup..."

try {
    Remove-Item $TempZip -Force
    Remove-Item $TempExtract -Recurse -Force
    Write-Success "? Cleanup complete"
} catch {
    Write-Warning "? Could not clean up temporary files"
}

Write-Info ""
Write-Info "=========================================="
Write-Success "Lua files copied: $CopiedFiles"
Write-Warning "Binary files: Manual download required (see above)"
Write-Info "=========================================="
Write-Info ""

# Check what we have
$HasSocket = Test-Path (Join-Path $LibDir "socket.lua")
$HasMime = Test-Path (Join-Path $LibDir "mime.lua")
$HasSocketDll = Test-Path (Join-Path $LibDir "socket\core.dll")
$HasMimeDll = Test-Path (Join-Path $LibDir "mime\core.dll")

Write-Info "Current status:"
Write-Info "  socket.lua: $(if ($HasSocket) { '?' } else { '?' })"
Write-Info "  mime.lua: $(if ($HasMime) { '?' } else { '?' })"
Write-Info "  socket/core.dll: $(if ($HasSocketDll) { '?' } else { '? (download needed)' })"
Write-Info "  mime/core.dll: $(if ($HasMimeDll) { '?' } else { '? (download needed)' })"

Write-Info ""
if ($HasSocket -and $HasMime) {
    Write-Success "? Lua files are ready!"
    if (-not $HasSocketDll -or -not $HasMimeDll) {
        Write-Warning "? Remember to download and add the .dll files (see instructions above)"
    } else {
        Write-Success "? All files ready! LuaSocket is complete."
    }
} else {
    Write-Error "? Some Lua files are missing. Check the output above."
}

Write-Info ""
Write-Info "For help, see: lib/MANUAL-INSTALL.md"
