<#
.SYNOPSIS
Comprehensive MSTest to xUnit converter for AeroDebrief.Tests

.DESCRIPTION
This script performs a complete automated conversion of all test files from MSTest to xUnit.
Run this from the tests\AeroDebrief.Tests directory.

.EXAMPLE
.\Convert-AllTests-ToXUnit.ps1
#>

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MSTest ? xUnit Conversion Tool" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Define conversion patterns
$conversionPatterns = @(
    # Using statements
    @{
        Pattern = "using Microsoft\.VisualStudio\.TestTools\.UnitTesting;"
        Replacement = "using Xunit;`nusing FluentAssertions;"
    }
    
    # Class attributes
    @{
        Pattern = "^\s*\[TestClass\]\s*$"
        Replacement = ""
    }
    
    # Method attributes
    @{
        Pattern = "\[TestMethod\]"
        Replacement = "[Fact]"
    }
    @{
        Pattern = "\[TestInitialize\]"
        Replacement = "// TODO: Convert to constructor or IAsyncLifetime.InitializeAsync()"
    }
    @{
        Pattern = "\[TestCleanup\]"
        Replacement = "// TODO: Implement IDisposable or IAsyncLifetime.DisposeAsync()"
    }
    @{
        Pattern = '\[TestCategory\("([^"]+)"\)\]'
        Replacement = '[Trait("Category", "$1")]'
    }
    
    # Assert.IsTrue
    @{
        Pattern = 'Assert\.IsTrue\(([^,]+),\s*"([^"]+)"\)'
        Replacement = '$1.Should().BeTrue("$2")'
    }
    @{
        Pattern = 'Assert\.IsTrue\(([^)]+)\)'
        Replacement = '$1.Should().BeTrue()'
    }
    
    # Assert.IsFalse
    @{
        Pattern = 'Assert\.IsFalse\(([^,]+),\s*"([^"]+)"\)'
        Replacement = '$1.Should().BeFalse("$2")'
    }
    @{
        Pattern = 'Assert\.IsFalse\(([^)]+)\)'
        Replacement = '$1.Should().BeFalse()'
    }
    
    # Assert.IsNull
    @{
        Pattern = 'Assert\.IsNull\(([^,]+),\s*"([^"]+)"\)'
        Replacement = '$1.Should().BeNull("$2")'
    }
    @{
        Pattern = 'Assert\.IsNull\(([^)]+)\)'
        Replacement = '$1.Should().BeNull()'
    }
    
    # Assert.IsNotNull
    @{
        Pattern = 'Assert\.IsNotNull\(([^,]+),\s*"([^"]+)"\)'
        Replacement = '$1.Should().NotBeNull("$2")'
    }
    @{
        Pattern = 'Assert\.IsNotNull\(([^)]+)\)'
        Replacement = '$1.Should().NotBeNull()'
    }
    
    # Assert.AreEqual with tolerance (for floating point)
    @{
        Pattern = 'Assert\.AreEqual\(([^,]+),\s*([^,]+),\s*([^,]+),\s*"([^"]+)"\)'
        Replacement = '$2.Should().BeApproximately($1, $3, "$4")'
    }
    @{
        Pattern = 'Assert\.AreEqual\(([^,]+),\s*([^,]+),\s*([^)]+)\)\s*;'
        Replacement = '$2.Should().BeApproximately($1, $3);'
    }
    
    # Assert.AreEqual
    @{
        Pattern = 'Assert\.AreEqual\(([^,]+),\s*([^,]+),\s*"([^"]+)"\)'
        Replacement = '$2.Should().Be($1, "$3")'
    }
    @{
        Pattern = 'Assert\.AreEqual\(([^,]+),\s*([^)]+)\)'
        Replacement = '$2.Should().Be($1)'
    }
    
    # Assert.AreNotEqual
    @{
        Pattern = 'Assert\.AreNotEqual\(([^,]+),\s*([^,]+),\s*"([^"]+)"\)'
        Replacement = '$2.Should().NotBe($1, "$3")'
    }
    @{
        Pattern = 'Assert\.AreNotEqual\(([^,]+),\s*([^)]+)\)'
        Replacement = '$2.Should().NotBe($1)'
    }
    
    # Assert.AreSame
    @{
        Pattern = 'Assert\.AreSame\(([^,]+),\s*([^)]+)\)'
        Replacement = '$2.Should().BeSameAs($1)'
    }
    
    # Assert.AreNotSame
    @{
        Pattern = 'Assert\.AreNotSame\(([^,]+),\s*([^)]+)\)'
        Replacement = '$2.Should().NotBeSameAs($1)'
    }
    
    # Assert.IsInstanceOfType
    @{
        Pattern = 'Assert\.IsInstanceOfType\(([^,]+),\s*typeof\(([^)]+)\)\)'
        Replacement = '$1.Should().BeOfType<$2>()'
    }
    
    # Assert.ThrowsException
    @{
        Pattern = 'Assert\.ThrowsException<([^>]+)>\(\(\)\s*=>\s*new\s+([^(]+)\(([^)]*)\)\)'
        Replacement = 'FluentActions.Invoking(() => new $2($3)).Should().Throw<$1>()'
    }
    @{
        Pattern = 'await\s+Assert\.ThrowsExceptionAsync<([^>]+)>\(\(\)\s*=>\s*([^)]+)\)'
        Replacement = 'await FluentActions.Invoking(async () => await $2).Should().ThrowAsync<$1>()'
    }
    
    # CollectionAssert.AreEqual
    @{
        Pattern = 'CollectionAssert\.AreEqual\(([^,]+),\s*([^)]+)\)'
        Replacement = '$2.Should().Equal($1)'
    }
    
    # CollectionAssert.Contains
    @{
        Pattern = 'CollectionAssert\.Contains\(([^,]+),\s*([^)]+)\)'
        Replacement = '$1.Should().Contain($2)'
    }
    
    # StringAssert.Contains
    @{
        Pattern = 'StringAssert\.Contains\(([^,]+),\s*([^)]+)\)'
        Replacement = '$1.Should().Contain($2)'
    }
    
    # Assert.Inconclusive
    @{
        Pattern = 'Assert\.Inconclusive\("([^"]+)"\)'
        Replacement = 'Skip.If(true, "$1"); return'
    }
)

# Find all test files
$testFiles = Get-ChildItem -Path "." -Filter "*Tests.cs" -Recurse

Write-Host "Found $($testFiles.Count) test files to process`n" -ForegroundColor Yellow

$convertedCount = 0
$skippedCount = 0
$errorCount = 0

foreach ($file in $testFiles) {
    Write-Host "Processing: $($file.FullName.Replace((Get-Location).Path, ''))" -ForegroundColor Cyan
    
    try {
        $content = Get-Content $file.FullName -Raw
        
        # Skip if already using xUnit
        if ($content -match "using Xunit;") {
            Write-Host "  ? Skipped (already xUnit)" -ForegroundColor Gray
            $skippedCount++
            continue
        }
        
        # Apply all conversion patterns
        $modified = $false
        foreach ($pattern in $conversionPatterns) {
            if ($content -match $pattern.Pattern) {
                $content = $content -replace $pattern.Pattern, $pattern.Replacement
                $modified = $true
            }
        }
        
        if ($modified) {
            # Clean up multiple blank lines
            $content = $content -replace '(\r?\n){3,}', "`n`n"
            
            # Save the file
            Set-Content -Path $file.FullName -Value $content -NoNewline
            Write-Host "  ? Converted" -ForegroundColor Green
            $convertedCount++
        }
        else {
            Write-Host "  ? No changes needed" -ForegroundColor Gray
            $skippedCount++
        }
    }
    catch {
        Write-Host "  ? Error: $_" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Conversion Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Converted:  $convertedCount files" -ForegroundColor Green
Write-Host "? Skipped:    $skippedCount files" -ForegroundColor Gray
Write-Host "? Errors:     $errorCount files" -ForegroundColor Red
Write-Host "========================================`n" -ForegroundColor Cyan

if ($convertedCount > 0) {
    Write-Host "? Conversion complete!`n" -ForegroundColor Green
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review the converted files for manual adjustments" -ForegroundColor White
    Write-Host "  2. Handle TestInitialize ? Constructor conversions" -ForegroundColor White
    Write-Host "  3. Handle TestCleanup ? IDisposable conversions" -ForegroundColor White
    Write-Host "  4. Run: dotnet build" -ForegroundColor White
    Write-Host "  5. Run: dotnet test`n" -ForegroundColor White
}
