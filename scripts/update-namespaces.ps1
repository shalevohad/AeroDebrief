# Update Namespaces Script
# This script updates all C# file namespaces to match the new project structure

param(
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== Namespace Update Script ===" -ForegroundColor Cyan
Write-Host ""

if ($WhatIf) {
    Write-Host "[DRY RUN MODE - No changes will be made]" -ForegroundColor Magenta
    Write-Host ""
}

# Define namespace mappings
$namespaceMappings = @{
    # Old namespaces -> New namespaces
    "Core" = "AeroDebrief.Core"
    "ShalevOhad.DCS.SRS.Recorder.Core" = "AeroDebrief.Core"
    "ShalevOhad.DCS.SRS.Recorder.CLI" = "AeroDebrief.CLI"
    "DCS_SRS_RecordingClient.CLI" = "AeroDebrief.CLI"
    "ShalevOhad.DCS.SRS.Recorder.PlayerClient.UI" = "AeroDebrief.UI"
}

function Update-Namespace {
    param(
        [string]$FilePath,
        [hashtable]$Mappings
    )
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    
    $content = Get-Content -Path $FilePath -Raw
    $originalContent = $content
    $changed = $false
    
    # Update namespace declarations
    foreach ($oldNs in $Mappings.Keys) {
        $newNs = $Mappings[$oldNs]
        
        # Match "namespace OldNamespace" or "namespace OldNamespace.Something"
        $pattern = "namespace\s+$([regex]::Escape($oldNs))(\s*[;{]|\.\w+)"
        if ($content -match $pattern) {
            $content = $content -replace "namespace\s+$([regex]::Escape($oldNs))\s*([;{])", "namespace $newNs`$1"
            $content = $content -replace "namespace\s+$([regex]::Escape($oldNs))\.(\w+)", "namespace $newNs.`$1"
            $changed = $true
        }
    }
    
    # Update using statements
    foreach ($oldNs in $Mappings.Keys) {
        $newNs = $Mappings[$oldNs]
        
        # Match "using OldNamespace;" or "using OldNamespace.Something;"
        $pattern = "using\s+$([regex]::Escape($oldNs))(\s*;|\.)"
        if ($content -match $pattern) {
            $content = $content -replace "using\s+$([regex]::Escape($oldNs))\s*;", "using $newNs;"
            $content = $content -replace "using\s+$([regex]::Escape($oldNs))\.(\w+)", "using $newNs.`$1"
            $changed = $true
        }
    }
    
    if ($changed -and (-not $WhatIf)) {
        Set-Content -Path $FilePath -Value $content -NoNewline
        Write-Host "  ? Updated: $FilePath" -ForegroundColor Green
        return $true
    } elseif ($changed) {
        Write-Host "  [DRY RUN] Would update: $FilePath" -ForegroundColor Yellow
        return $true
    }
    
    return $false
}

function Process-Directory {
    param(
        [string]$Path,
        [hashtable]$Mappings
    )
    
    if (-not (Test-Path $Path)) {
        Write-Host "Directory not found: $Path" -ForegroundColor DarkGray
        return
    }
    
    Write-Host "Processing: $Path" -ForegroundColor Cyan
    
    $csFiles = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse | Where-Object {
        $_.FullName -notmatch "\\obj\\" -and 
        $_.FullName -notmatch "\\bin\\" -and
        $_.Name -notmatch "\.g\.cs$" -and
        $_.Name -notmatch "\.g\.i\.cs$" -and
        $_.Name -notmatch "AssemblyInfo\.cs$" -and
        $_.Name -notmatch "GlobalUsings\.g\.cs$"
    }
    
    $updatedCount = 0
    foreach ($file in $csFiles) {
        if (Update-Namespace -FilePath $file.FullName -Mappings $Mappings) {
            $updatedCount++
        }
    }
    
    if ($updatedCount -eq 0) {
        Write-Host "  No files needed updates" -ForegroundColor DarkGray
    } else {
        Write-Host "  Updated $updatedCount file(s)" -ForegroundColor Green
    }
    
    Write-Host ""
}

# Process each project directory
Process-Directory -Path "src\AeroDebrief.Core" -Mappings $namespaceMappings
Process-Directory -Path "src\AeroDebrief.CLI" -Mappings $namespaceMappings
Process-Directory -Path "src\AeroDebrief.UI" -Mappings $namespaceMappings
Process-Directory -Path "src\AeroDebrief.Integrations" -Mappings $namespaceMappings
Process-Directory -Path "tests\AeroDebrief.Tests" -Mappings $namespaceMappings

Write-Host "=== Namespace Update Complete ===" -ForegroundColor Cyan
Write-Host ""

if ($WhatIf) {
    Write-Host "Run without -WhatIf to execute the updates" -ForegroundColor Magenta
} else {
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Review the changes with: git diff" -ForegroundColor White
    Write-Host "2. Build the solution: dotnet build AeroDebrief.sln" -ForegroundColor White
    Write-Host "3. Fix any remaining compilation errors" -ForegroundColor White
    Write-Host "4. Run tests: dotnet test AeroDebrief.sln" -ForegroundColor White
}
