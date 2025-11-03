# Quick Diagnostic Script for Tacview Connection Issue
# Run this to get all relevant information

Write-Host "????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  Tacview Connection Diagnostic - Quick Check        ?" -ForegroundColor Cyan
Write-Host "????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host ""

# Check 1: Are files in place?
Write-Host "1. Checking Addon Files..." -ForegroundColor Yellow
$addonPath = "C:\Program Files (x86)\Tacview\AddOns\AeroDebriefSync"
if (Test-Path $addonPath) {
    Write-Host "   ? Addon directory exists" -ForegroundColor Green
    
    $requiredFiles = @("main.lua", "tcp_server.lua", "state_manager.lua")
    foreach ($file in $requiredFiles) {
        $filePath = Join-Path $addonPath $file
        if (Test-Path $filePath) {
            $lastModified = (Get-Item $filePath).LastWriteTime
            Write-Host "   ? $file (modified: $lastModified)" -ForegroundColor Green
        } else {
            Write-Host "   ? $file MISSING!" -ForegroundColor Red
        }
    }
} else {
    Write-Host "   ? Addon directory not found!" -ForegroundColor Red
}

# Check 2: Is Tacview running?
Write-Host ""
Write-Host "2. Checking Tacview Process..." -ForegroundColor Yellow
$tacview = Get-Process -Name "Tacview" -ErrorAction SilentlyContinue
if ($tacview) {
    Write-Host "   ? Tacview is running (PID: $($tacview.Id))" -ForegroundColor Green
} else {
    Write-Host "   ? Tacview is NOT running" -ForegroundColor Red
    Write-Host "   ? Start Tacview first!" -ForegroundColor Yellow
}

# Check 3: Is port listening?
Write-Host ""
Write-Host "3. Checking TCP Port 52001..." -ForegroundColor Yellow
$portCheck = netstat -an | Select-String "52001.*LISTENING"
if ($portCheck) {
    Write-Host "   ? Port 52001 is LISTENING" -ForegroundColor Green
    Write-Host "   $portCheck" -ForegroundColor Gray
} else {
    Write-Host "   ? Port 52001 is NOT listening" -ForegroundColor Red
    Write-Host "   ? This means the TCP server didn't start" -ForegroundColor Yellow
}

# Check 4: Is test client running?
Write-Host ""
Write-Host "4. Checking Test Client..." -ForegroundColor Yellow
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
if ($dotnetProcesses) {
    Write-Host "   ?? Dotnet process(es) found - test client might be running" -ForegroundColor Yellow
    foreach ($proc in $dotnetProcesses) {
        Write-Host "   PID: $($proc.Id)" -ForegroundColor Gray
    }
} else {
    Write-Host "   ?? No dotnet processes - test client not running" -ForegroundColor Cyan
}

# Check 5: Check for established connections
Write-Host ""
Write-Host "5. Checking Active Connections on Port 52001..." -ForegroundColor Yellow
$connections = netstat -an | Select-String "52001.*ESTABLISHED"
if ($connections) {
    Write-Host "   ? Active connection(s) found:" -ForegroundColor Green
    foreach ($conn in $connections) {
        Write-Host "   $conn" -ForegroundColor Gray
    }
} else {
    Write-Host "   ?? No active connections" -ForegroundColor Yellow
    Write-Host "   ? Test client might not be connected" -ForegroundColor Yellow
}

# Check 6: Tacview log file location
Write-Host ""
Write-Host "6. Tacview Log File Location..." -ForegroundColor Yellow
$logPath = "$env:APPDATA\Tacview\Logs"
if (Test-Path $logPath) {
    Write-Host "   ? Log directory: $logPath" -ForegroundColor Green
    $latestLog = Get-ChildItem $logPath -Filter "*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestLog) {
        Write-Host "   Latest log: $($latestLog.Name) (modified: $($latestLog.LastWriteTime))" -ForegroundColor Gray
        
        # Check if OnUpdate message is in log
        $logContent = Get-Content $latestLog.FullName -Tail 100 | Out-String
        if ($logContent -match "OnUpdate loop started") {
            Write-Host "   ? 'OnUpdate loop started' found in log!" -ForegroundColor Green
        } else {
            Write-Host "   ? 'OnUpdate loop started' NOT found in recent log" -ForegroundColor Red
            Write-Host "   ? OnUpdate is NOT being called!" -ForegroundColor Yellow
        }
        
        if ($logContent -match "CLIENT CONNECTED") {
            Write-Host "   ? 'CLIENT CONNECTED' found in log!" -ForegroundColor Green
        } else {
            Write-Host "   ?? 'CLIENT CONNECTED' NOT found in recent log" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "   ?? Log directory not found at expected location" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "????????????????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?                    DIAGNOSIS                          ?" -ForegroundColor Cyan
Write-Host "????????????????????????????????????????????????????????" -ForegroundColor Cyan

if (-not $tacview) {
    Write-Host "? PROBLEM: Tacview is not running" -ForegroundColor Red
    Write-Host "   FIX: Start Tacview" -ForegroundColor Yellow
}
elseif (-not $portCheck) {
    Write-Host "? PROBLEM: TCP server not started (port not listening)" -ForegroundColor Red
    Write-Host "   POSSIBLE CAUSES:" -ForegroundColor Yellow
    Write-Host "   - Addon not loaded" -ForegroundColor Yellow
    Write-Host "   - Addon failed to start server" -ForegroundColor Yellow
    Write-Host "   - Check Tacview logs for errors" -ForegroundColor Yellow
}
elseif ($logContent -notmatch "OnUpdate loop started") {
    Write-Host "? PROBLEM: OnUpdate is NOT being called" -ForegroundColor Red
    Write-Host "   MOST LIKELY CAUSE: No telemetry file loaded in Tacview" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   FIX:" -ForegroundColor Green
    Write-Host "   1. In Tacview: File ? Open" -ForegroundColor White
    Write-Host "   2. Load ANY .acmi file" -ForegroundColor White
    Write-Host "   3. Check Tacview logs again" -ForegroundColor White
    Write-Host "   4. You should see 'OnUpdate loop started'" -ForegroundColor White
}
elseif (-not $connections) {
    Write-Host "?? PROBLEM: OnUpdate is working but no client connected" -ForegroundColor Yellow
    Write-Host "   FIX:" -ForegroundColor Green
    Write-Host "   1. Run test client: cd src\AeroDebrief.TacviewTestApp" -ForegroundColor White
    Write-Host "   2. dotnet run" -ForegroundColor White
    Write-Host "   3. Check Tacview logs for 'CLIENT CONNECTED'" -ForegroundColor White
}
else {
    Write-Host "? Everything looks good!" -ForegroundColor Green
    Write-Host "   - Tacview running" -ForegroundColor Gray
    Write-Host "   - Port listening" -ForegroundColor Gray
    Write-Host "   - Client connected" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   If you still don't see messages:" -ForegroundColor Yellow
    Write-Host "   1. Load a recording and press PLAY" -ForegroundColor White
    Write-Host "   2. Check Tacview logs for state change messages" -ForegroundColor White
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
