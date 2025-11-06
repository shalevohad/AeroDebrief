# AeroDebrief Sync Diagnostic Script
# Run this to check if the addon is properly installed

Write-Host "╔═══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   AeroDebrief Sync - Installation Diagnostic     ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check Tacview directory
$tacviewDir = "$env:APPDATA\Tacview"
Write-Host "1. Tacview Directory Check" -ForegroundColor Yellow
Write-Host "   Path: $tacviewDir"
if (Test-Path $tacviewDir) {
    Write-Host "   ✓ Exists" -ForegroundColor Green
} else {
    Write-Host "   ✗ Not found - Tacview may not be installed correctly" -ForegroundColor Red
    Write-Host ""
    Write-Host "Install Tacview from: https://www.tacview.net/" -ForegroundColor Yellow
    exit 1
}

# Check addons directory
Write-Host ""
$addonsDir = "$tacviewDir\AddOns"
Write-Host "2. Addons Directory Check" -ForegroundColor Yellow
Write-Host "   Path: $addonsDir"
if (Test-Path $addonsDir) {
    Write-Host "   ✓ Exists" -ForegroundColor Green
} else {
    Write-Host "   ⚠ Not found - Creating..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $addonsDir | Out-Null
    Write-Host "   ✓ Created" -ForegroundColor Green
}

# Check AeroDebriefSync directory
Write-Host ""
$addonDir = "$addonsDir\AeroDebriefSync"
Write-Host "3. AeroDebriefSync Addon Directory" -ForegroundColor Yellow
Write-Host "   Path: $addonDir"
if (Test-Path $addonDir) {
    Write-Host "   ✓ Exists" -ForegroundColor Green
    
    # List files
    Write-Host ""
    Write-Host "   Files in addon directory:" -ForegroundColor Cyan
    $files = Get-ChildItem $addonDir -Name
    foreach ($file in $files) {
        Write-Host "   - $file" -ForegroundColor Gray
    }
    
    # Check required files
    Write-Host ""
    Write-Host "4. Required Files Check" -ForegroundColor Yellow
    $required = @{
        "main.lua" = "Main entry point"
        "config.lua" = "Configuration manager"
        "tcp_server.lua" = "TCP server (FIXED)"
        "protocol.lua" = "Protocol handler"
        "state_manager.lua" = "State manager (CRITICAL FIX)"
        "pilot_extractor.lua" = "Pilot info extractor"
        "pan_manager.lua" = "Audio pan manager"
        "visual_effects.lua" = "Visual effects"
        "menu_ui.lua" = "Menu interface"
        "utils.lua" = "Utility functions"
        "manifest.txt" = "Addon manifest"
    }
    
    $allPresent = $true
    foreach ($file in $required.Keys) {
        if (Test-Path "$addonDir\$file") {
            Write-Host "   ✓ $file" -ForegroundColor Green -NoNewline
            Write-Host " - $($required[$file])" -ForegroundColor Gray
        } else {
            Write-Host "   ✗ $file MISSING!" -ForegroundColor Red
            $allPresent = $false
        }
    }
    
    if (-not $allPresent) {
        Write-Host ""
        Write-Host "   ⚠ Some files are missing!" -ForegroundColor Red
        Write-Host "   Copy all files from: src\AeroDebrief.Integrations\Lua\Tacview\AeroDebriefSync\" -ForegroundColor Yellow
    }
    
    # Check file timestamps
    Write-Host ""
    Write-Host "5. File Timestamps" -ForegroundColor Yellow
    Write-Host "   (Recently modified files indicate fresh installation)" -ForegroundColor Gray
    $luaFiles = Get-ChildItem $addonDir\*.lua | Select-Object Name, LastWriteTime
    foreach ($file in $luaFiles) {
        $age = (Get-Date) - $file.LastWriteTime
        $color = if ($age.TotalDays -lt 1) { "Green" } elseif ($age.TotalDays -lt 7) { "Yellow" } else { "Red" }
        Write-Host ("   {0,-25} {1}" -f $file.Name, $file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")) -ForegroundColor $color
    }
    
} else {
    Write-Host "   ✗ Not found - Addon not installed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "   To install, run:" -ForegroundColor Yellow
    Write-Host "   Copy-Item 'src\AeroDebrief.Integrations\Lua\Tacview\AeroDebriefSync' '$addonsDir' -Recurse" -ForegroundColor Cyan
    $allPresent = $false
}

# Check if Tacview is running
Write-Host ""
Write-Host "6. Tacview Process Check" -ForegroundColor Yellow
$tacviewProcess = Get-Process -Name "Tacview" -ErrorAction SilentlyContinue
if ($tacviewProcess) {
    Write-Host "   ✓ Tacview is running (PID: $($tacviewProcess.Id))" -ForegroundColor Green
    Write-Host "   ⚠ You need to restart Tacview to load updated addon files" -ForegroundColor Yellow
} else {
    Write-Host "   ⚠ Tacview is not currently running" -ForegroundColor Yellow
    Write-Host "   Start Tacview to test the addon" -ForegroundColor Gray
}

# Check if port is listening
Write-Host ""
Write-Host "7. TCP Port 52001 Check" -ForegroundColor Yellow
$portCheck = netstat -an | Select-String "52001.*LISTENING"
if ($portCheck) {
    Write-Host "   ✓ Port 52001 is listening (addon TCP server is running)" -ForegroundColor Green
    Write-Host "   $portCheck" -ForegroundColor Gray
} else {
    Write-Host "   ⚠ Port 52001 not listening" -ForegroundColor Yellow
    Write-Host "   Possible reasons:" -ForegroundColor Gray
    Write-Host "   - Tacview not running" -ForegroundColor Gray
    Write-Host "   - Addon not loaded" -ForegroundColor Gray
    Write-Host "   - Addon failed to start TCP server" -ForegroundColor Gray
}

# Check repo location
Write-Host ""
Write-Host "8. Repository Location" -ForegroundColor Yellow
$repoPath = "C:\Users\Ohad\source\repos\AeroDebrief\src\AeroDebrief.Integrations\Lua\Tacview\AeroDebriefSync"
if (Test-Path $repoPath) {
    Write-Host "   ✓ Repository found at: $repoPath" -ForegroundColor Green
    
    # Compare files
    Write-Host ""
    Write-Host "9. File Comparison (Repo vs Installed)" -ForegroundColor Yellow
    if (Test-Path $addonDir) {
        $criticalFiles = @("state_manager.lua", "main.lua", "tcp_server.lua")
        foreach ($file in $criticalFiles) {
            $repoFile = "$repoPath\$file"
            $installedFile = "$addonDir\$file"
            
            if ((Test-Path $repoFile) -and (Test-Path $installedFile)) {
                $repoHash = (Get-FileHash $repoFile -Algorithm MD5).Hash
                $installedHash = (Get-FileHash $installedFile -Algorithm MD5).Hash
                
                if ($repoHash -eq $installedHash) {
                    Write-Host "   ✓ $file - Up to date" -ForegroundColor Green
                } else {
                    Write-Host "   ⚠ $file - DIFFERENT (needs update)" -ForegroundColor Yellow
                }
            }
        }
    }
} else {
    Write-Host "   ⚠ Repository not found at expected location" -ForegroundColor Yellow
}

# Summary and recommendations
Write-Host ""
Write-Host "╔═════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║                    SUMMARY                          ║" -ForegroundColor Cyan
Write-Host "╚═════════════════════════════════════════════════════╝" -ForegroundColor Cyan

if ($allPresent -and (Test-Path $addonDir)) {
    Write-Host "✓ Addon appears to be installed correctly" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Restart Tacview (if running)" -ForegroundColor White
    Write-Host "2. Check Tacview logs: Help ? Show Log" -ForegroundColor White
    Write-Host "3. Look for: 'AeroDebrief Sync: Initialized successfully'" -ForegroundColor White
    Write-Host "4. Run test app: cd src\AeroDebrief.TacviewTestApp; dotnet run" -ForegroundColor White
} else {
    Write-Host "✗ Addon is NOT properly installed" -ForegroundColor Red
    Write-Host ""
    Write-Host "To fix:" -ForegroundColor Yellow
    Write-Host "1. Run this PowerShell command:" -ForegroundColor White
    Write-Host "   Copy-Item '$repoPath' '$addonsDir' -Recurse -Force" -ForegroundColor Cyan
    Write-Host "2. Restart Tacview completely" -ForegroundColor White
    Write-Host "3. Run this diagnostic script again" -ForegroundColor White
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
