# AeroDebrief Migration Script
# This script reorganizes the existing project structure into the new modular layout

param(
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== AeroDebrief Migration Script ===" -ForegroundColor Cyan
Write-Host "This will reorganize your project into the new modular structure" -ForegroundColor Yellow
Write-Host ""

if ($WhatIf) {
    Write-Host "[DRY RUN MODE - No changes will be made]" -ForegroundColor Magenta
    Write-Host ""
}

# Define source and destination mappings
$coreMappings = @(
    @{ Source = "Core\Tests\OpusDecodingTests.cs"; Dest = "tests\AeroDebrief.Tests\OpusDecodingTests.cs" }
    @{ Source = "Core\Audio\*"; Dest = "src\AeroDebrief.Core\Audio\" }
    @{ Source = "Core\Settings\*"; Dest = "src\AeroDebrief.Core\Settings\" }
    @{ Source = "Core\Theme\*"; Dest = "src\AeroDebrief.Core\Theme\" }
    @{ Source = "Core\Singleton\*"; Dest = "src\AeroDebrief.Core\Singleton\" }
    @{ Source = "Core\Helpers\*"; Dest = "src\AeroDebrief.Core\Helpers\" }
    @{ Source = "Core\Filtering\*"; Dest = "src\AeroDebrief.Core\Filtering\" }
    @{ Source = "Core\Models\*"; Dest = "src\AeroDebrief.Core\Models\" }
    @{ Source = "Core\Playback\*"; Dest = "src\AeroDebrief.Core\Playback\" }
    @{ Source = "Core\Analysis\*"; Dest = "src\AeroDebrief.Core\Analysis\" }
    @{ Source = "Core\AudioPacketRecorder.cs"; Dest = "src\AeroDebrief.Core\AudioPacketRecorder.cs" }
    @{ Source = "Core\AudioPacketMetadata.cs"; Dest = "src\AeroDebrief.Core\AudioPacketMetadata.cs" }
    @{ Source = "Core\AudioPacketReader.cs"; Dest = "src\AeroDebrief.Core\AudioPacketReader.cs" }
    @{ Source = "Core\Constants.cs"; Dest = "src\AeroDebrief.Core\Constants.cs" }
)

$cliMappings = @(
    @{ Source = "DCS-SRS-RecordingClient.CLI\Program.cs"; Dest = "src\AeroDebrief.CLI\Program.cs" }
    @{ Source = "DCS-SRS-RecordingClient.CLI\AudioDiagnostics.cs"; Dest = "src\AeroDebrief.CLI\AudioDiagnostics.cs" }
    @{ Source = "DCS-SRS-RecordingClient.CLI\NLog.config"; Dest = "src\AeroDebrief.CLI\NLog.config" }
)

$uiMappings = @(
    @{ Source = "DCS-SRS-RecordingClient.UI\Controls\*"; Dest = "src\AeroDebrief.UI\Controls\" }
    @{ Source = "DCS-SRS-RecordingClient.UI\ViewModels\*"; Dest = "src\AeroDebrief.UI\ViewModels\" }
    @{ Source = "DCS-SRS-RecordingClient.UI\Services\*"; Dest = "src\AeroDebrief.UI\Services\" }
    @{ Source = "DCS-SRS-RecordingClient.UI\MainWindow.xaml*"; Dest = "src\AeroDebrief.UI\" }
    @{ Source = "DCS-SRS-RecordingClient.UI\App.xaml*"; Dest = "src\AeroDebrief.UI\" }
    @{ Source = "DCS-SRS-RecordingClient.UI\Program.cs"; Dest = "src\AeroDebrief.UI\Program.cs" }
    @{ Source = "DCS-SRS-RecordingClient.UI\AssemblyInfo.cs"; Dest = "src\AeroDebrief.UI\AssemblyInfo.cs" }
    @{ Source = "DCS-SRS-RecordingClient.UI\NLog.config"; Dest = "src\AeroDebrief.UI\NLog.config" }
    @{ Source = "DCS-SRS-RecordingClient.UI\Properties\*"; Dest = "src\AeroDebrief.UI\Properties\" }
)

function Copy-FilesWithMapping {
    param(
        [array]$Mappings,
        [string]$Description
    )
    
    Write-Host "Processing $Description..." -ForegroundColor Green
    
    foreach ($mapping in $Mappings) {
        $source = $mapping.Source
        $dest = $mapping.Dest
        
        # Handle wildcards
        if ($source -like "*\*") {
            $sourceDir = Split-Path $source -Parent
            $pattern = Split-Path $source -Leaf
            
            if (Test-Path $sourceDir) {
                $files = Get-ChildItem -Path $sourceDir -Filter $pattern -File -Recurse:$false
                
                foreach ($file in $files) {
                    $destPath = if ($dest.EndsWith("\")) {
                        Join-Path $dest $file.Name
                    } else {
                        $dest
                    }
                    
                    $destDir = Split-Path $destPath -Parent
                    
                    Write-Host "  $($file.FullName) -> $destPath"
                    
                    if (-not $WhatIf) {
                        if (-not (Test-Path $destDir)) {
                            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                        }
                        Copy-Item -Path $file.FullName -Destination $destPath -Force
                    }
                }
            }
        } else {
            if (Test-Path $source) {
                $destDir = Split-Path $dest -Parent
                
                Write-Host "  $source -> $dest"
                
                if (-not $WhatIf) {
                    if (-not (Test-Path $destDir)) {
                        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
                    }
                    Copy-Item -Path $source -Destination $dest -Force
                }
            } else {
                Write-Host "  [SKIP] $source (not found)" -ForegroundColor DarkGray
            }
        }
    }
    
    Write-Host ""
}

# Execute migrations
Copy-FilesWithMapping -Mappings $coreMappings -Description "Core files"
Copy-FilesWithMapping -Mappings $cliMappings -Description "CLI files"
Copy-FilesWithMapping -Mappings $uiMappings -Description "UI files"

# Create placeholder for Integrations
if (-not $WhatIf) {
    $integrationsDir = "src\AeroDebrief.Integrations"
    if (-not (Test-Path $integrationsDir)) {
        New-Item -ItemType Directory -Path $integrationsDir -Force | Out-Null
    }
    
    # Create a placeholder file
    $placeholderPath = Join-Path $integrationsDir "Placeholder.cs"
    @"
namespace AeroDebrief.Integrations
{
    // This is a placeholder file for the Integrations project.
    // Add TacView and Lua integration code here.
    
    public class IntegrationsPlaceholder
    {
        // TODO: Implement TacView integration
        // TODO: Implement DCS Lua export integration
    }
}
"@ | Out-File -FilePath $placeholderPath -Encoding UTF8
    
    Write-Host "Created placeholder for Integrations project" -ForegroundColor Green
}

Write-Host "=== Migration Plan Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Run this script without -WhatIf to perform the actual migration" -ForegroundColor White
Write-Host "2. Update namespaces in all C# files (use the namespace update script)" -ForegroundColor White
Write-Host "3. Build the solution: dotnet build AeroDebrief.sln" -ForegroundColor White
Write-Host "4. Run tests: dotnet test AeroDebrief.sln" -ForegroundColor White
Write-Host "5. Commit changes and push to GitHub" -ForegroundColor White
Write-Host ""

if ($WhatIf) {
    Write-Host "Run without -WhatIf to execute the migration" -ForegroundColor Magenta
}
