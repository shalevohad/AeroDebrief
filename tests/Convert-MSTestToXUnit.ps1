<#
.SYNOPSIS
Converts MSTest test files to xUnit with FluentAssertions

.DESCRIPTION
This script automatically converts MSTest attributes and assertions to xUnit equivalents.
Supports the following conversions:
- [TestClass] -> remove (not needed in xUnit)
- [TestMethod] -> [Fact]
- [TestInitialize] -> Constructor or [Fact] setup
- [TestCleanup] -> IDisposable.Dispose()
- [TestCategory] -> [Trait]
- Assert.* -> FluentAssertions Should().*
- CollectionAssert.* -> FluentAssertions collection assertions

.EXAMPLE
.\Convert-MSTestToXUnit.ps1
#>

$ErrorActionPreference = "Stop"

# Get all test files
$testFiles = Get-ChildItem -Path "." -Include "*Tests.cs" -Recurse

Write-Host "Found $($testFiles.Count) test files" -ForegroundColor Cyan

foreach ($file in $testFiles) {
    Write-Host "`nProcessing: $($file.Name)" -ForegroundColor Yellow
    
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Skip if already converted (check for xUnit using statement)
    if ($content -match "using Xunit;") {
        Write-Host "  Already converted (xUnit detected)" -ForegroundColor Green
        continue
    }
    
    # Replace using statements
    $content = $content -replace "using Microsoft\.VisualStudio\.TestTools\.UnitTesting;", "using Xunit;`nusing FluentAssertions;"
    
    # Remove [TestClass] attribute
    $content = $content -replace "\s*\[TestClass\]\s*`n", "`n"
    
    # Convert [TestMethod] to [Fact]
    $content = $content -replace "\[TestMethod\]", "[Fact]"
    
    # Convert [TestInitialize] to constructor comment
    $content = $content -replace "\[TestInitialize\]", "// Use constructor or IAsyncLifetime for setup"
    
    # Convert [TestCleanup] to IDisposable comment
    $content = $content -replace "\[TestCleanup\]", "// Implement IDisposable for cleanup"
    
    # Convert [TestCategory("...")] to [Trait("Category", "...")]
    $content = $content -replace '\[TestCategory\("([^"]+)"\)\]', '[Trait("Category", "$1")]'
    
    # Convert Assert statements to FluentAssertions
    
    # Assert.IsTrue -> Should().BeTrue()
    $content = $content -replace 'Assert\.IsTrue\(([^,]+),\s*"([^"]+)"\)', '$1.Should().BeTrue("$2")'
    $content = $content -replace 'Assert\.IsTrue\(([^)]+)\)', '$1.Should().BeTrue()'
    
    # Assert.IsFalse -> Should().BeFalse()
    $content = $content -replace 'Assert\.IsFalse\(([^,]+),\s*"([^"]+)"\)', '$1.Should().BeFalse("$2")'
    $content = $content -replace 'Assert\.IsFalse\(([^)]+)\)', '$1.Should().BeFalse()'
    
    # Assert.IsNull -> Should().BeNull()
    $content = $content -replace 'Assert\.IsNull\(([^,]+),\s*"([^"]+)"\)', '$1.Should().BeNull("$2")'
    $content = $content -replace 'Assert\.IsNull\(([^)]+)\)', '$1.Should().BeNull()'
    
    # Assert.IsNotNull -> Should().NotBeNull()
    $content = $content -replace 'Assert\.IsNotNull\(([^,]+),\s*"([^"]+)"\)', '$1.Should().NotBeNull("$2")'
    $content = $content -replace 'Assert\.IsNotNull\(([^)]+)\)', '$1.Should().NotBeNull()'
    
    # Assert.AreEqual -> Should().Be()
    $content = $content -replace 'Assert\.AreEqual\(([^,]+),\s*([^,]+),\s*([^,]+),\s*"([^"]+)"\)', '$2.Should().BeApproximately($1, $3, "$4")'
    $content = $content -replace 'Assert\.AreEqual\(([^,]+),\s*([^,]+),\s*"([^"]+)"\)', '$2.Should().Be($1, "$3")'
    $content = $content -replace 'Assert\.AreEqual\(([^,]+),\s*([^)]+)\)', '$2.Should().Be($1)'
    
    # Assert.AreNotEqual -> Should().NotBe()
    $content = $content -replace 'Assert\.AreNotEqual\(([^,]+),\s*([^,]+),\s*"([^"]+)"\)', '$2.Should().NotBe($1, "$3")'
    $content = $content -replace 'Assert\.AreNotEqual\(([^,]+),\s*([^)]+)\)', '$2.Should().NotBe($1)'
    
    # Assert.AreSame -> Should().BeSameAs()
    $content = $content -replace 'Assert\.AreSame\(([^,]+),\s*([^)]+)\)', '$2.Should().BeSameAs($1)'
    
    # Assert.AreNotSame -> Should().NotBeSameAs()
    $content = $content -replace 'Assert\.AreNotSame\(([^,]+),\s*([^)]+)\)', '$2.Should().NotBeSameAs($1)'
    
    # Assert.IsInstanceOfType -> Should().BeOfType()
    $content = $content -replace 'Assert\.IsInstanceOfType\(([^,]+),\s*typeof\(([^)]+)\)\)', '$1.Should().BeOfType<$2>()'
    
    # CollectionAssert.AreEqual -> Should().Equal()
    $content = $content -replace 'CollectionAssert\.AreEqual\(([^,]+),\s*([^)]+)\)', '$2.Should().Equal($1)'
    
    # CollectionAssert.Contains -> Should().Contain()
    $content = $content -replace 'CollectionAssert\.Contains\(([^,]+),\s*([^)]+)\)', '$1.Should().Contain($2)'
    
    # StringAssert.Contains -> Should().Contain()
    $content = $content -replace 'StringAssert\.Contains\(([^,]+),\s*([^)]+)\)', '$1.Should().Contain($2)'
    
    # Save if changed
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "  ? Converted" -ForegroundColor Green
    }
    else {
        Write-Host "  No changes needed" -ForegroundColor Gray
    }
}

Write-Host "`n? Conversion complete!" -ForegroundColor Green
Write-Host "`nNote: Some manual adjustments may be needed for:" -ForegroundColor Yellow
Write-Host "  - Complex Assert statements"
Write-Host "  - TestInitialize/TestCleanup conversions"
Write-Host "  - Custom test attributes"
Write-Host "`nPlease review the changes and run tests to verify." -ForegroundColor Cyan
