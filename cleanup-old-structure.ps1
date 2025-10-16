# AeroDebrief - Cleanup Old Structure Script
# This script removes old folders and files from the previous DCS-SRS-Recorder structure

Write-Host "=== AeroDebrief Cleanup Script ===" -ForegroundColor Cyan
Write-Host "Removing old folder structure and files..." -ForegroundColor Yellow
Write-Host ""

$rootPath = "C:\Users\Ohad\source\repos\AeroDebrief"
$itemsToRemove = @()

# Old project folders (no longer needed)
$oldFolders = @(
    "$rootPath\DCS-SRS-RecordingClient.CLI",
    "$rootPath\DCS-SRS-RecordingClient.UI",
    "$rootPath\Core",  # Old Core folder (replaced by src\AeroDebrief.Core)
    "$rootPath\src\AeroDebrief.UI\Converters"  # Old Converters folder (moved to Helpers)
)

# Old documentation files (outdated)
$oldDocs = @(
    "$rootPath\PlayerClient.md",
    "$rootPath\RecordingClients.md",
    "$rootPath\RecordingClient_UI_Structure.md",
    "$rootPath\AudioBuffering.md",
    "$rootPath\EnhancedUIFeatures.md",
    "$rootPath\Themeing.md",
    "$rootPath\FIX_SUMMARY.md",
    "$rootPath\IMPLEMENTATION_SUMMARY.md",
    "$rootPath\QUICK_START.md",
    "$rootPath\VALIDATION_NOTES.md"
)

# Combine all items to remove
$itemsToRemove = $oldFolders + $oldDocs

Write-Host "Items to be removed:" -ForegroundColor Yellow
foreach ($item in $itemsToRemove) {
    if (Test-Path $item) {
        $itemType = if (Test-Path $item -PathType Container) { "Directory" } else { "File" }
        Write-Host "  [$itemType] $item" -ForegroundColor Gray
    }
}

Write-Host ""
$confirmation = Read-Host "Do you want to proceed with deletion? (yes/no)"

if ($confirmation -eq "yes") {
    Write-Host ""
    Write-Host "Removing items..." -ForegroundColor Yellow
    
    foreach ($item in $itemsToRemove) {
        if (Test-Path $item) {
            try {
                Remove-Item -Path $item -Recurse -Force -ErrorAction Stop
                $itemName = Split-Path -Leaf $item
                Write-Host "  ? Removed: $itemName" -ForegroundColor Green
            }
            catch {
                Write-Host "  ? Failed to remove: $item" -ForegroundColor Red
                Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        else {
            $itemName = Split-Path -Leaf $item
            Write-Host "  - Skipped (not found): $itemName" -ForegroundColor DarkGray
        }
    }
    
    Write-Host ""
    Write-Host "=== Cleanup Complete ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary of current structure:" -ForegroundColor Cyan
    Write-Host "  src\AeroDebrief.Core      - Core library" -ForegroundColor White
    Write-Host "  src\AeroDebrief.UI        - WPF UI application" -ForegroundColor White
    Write-Host "  src\AeroDebrief.CLI       - Command-line interface" -ForegroundColor White
    Write-Host "  src\AeroDebrief.Integrations - External integrations" -ForegroundColor White
    Write-Host "  tests\AeroDebrief.Tests   - Unit tests" -ForegroundColor White
    Write-Host "  External\SRS\             - SRS Common libraries" -ForegroundColor White
    Write-Host ""
}
else {
    Write-Host ""
    Write-Host "Cleanup cancelled." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
