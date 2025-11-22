# Test DuckDB Memory Limit Configuration

## Quick Test Script

Save this as `test-duckdb-config.ps1` and run it:

```powershell
# Test DuckDB memory_limit configuration directly

Write-Host "Testing DuckDB memory_limit configuration..." -ForegroundColor Cyan

# Navigate to CLI output
cd "C:\Users\Ohad\source\repos\AeroDebrief\src\AeroDebrief.CLI\bin\x64\Debug\net9.0"

# Create a simple C# test program inline
$testCode = @'
using DuckDB.NET.Data;
using System;

class Test
{
    static void Main()
    {
        var dbPath = "test_config.duckdb";
        
        // Clean up old file
        if (System.IO.File.Exists(dbPath))
            System.IO.File.Delete(dbPath);
        
        using var conn = new DuckDBConnection($"Data Source={dbPath}");
        conn.Open();
        
        using var cmd = conn.CreateCommand();
        
        // Test 1: Try with 2GB (decimal - should fail)
        Console.WriteLine("\nTest 1: SET memory_limit='2GB'");
        try
        {
            cmd.CommandText = "SET memory_limit='2GB'";
            cmd.ExecuteNonQuery();
            Console.WriteLine("? SUCCESS (unexpected!)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? FAILED: {ex.Message}");
        }
        
        // Test 2: Try with 2GiB (binary - should work)
        Console.WriteLine("\nTest 2: SET memory_limit='2GiB'");
        try
        {
            cmd.CommandText = "SET memory_limit='2GiB'";
            cmd.ExecuteNonQuery();
            Console.WriteLine("? SUCCESS!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? FAILED: {ex.Message}");
        }
        
        // Test 3: Check what was actually set
        Console.WriteLine("\nTest 3: SELECT current_setting('memory_limit')");
        try
        {
            cmd.CommandText = "SELECT current_setting('memory_limit')";
            var result = cmd.ExecuteScalar();
            Console.WriteLine($"Current memory_limit: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? FAILED: {ex.Message}");
        }
        
        conn.Close();
        
        // Clean up
        if (System.IO.File.Exists(dbPath))
            System.IO.File.Delete(dbPath);
    }
}
'@

# Compile and run the test
Write-Host "`nCompiling test program..." -ForegroundColor Yellow
$testCode | Out-File -Encoding UTF8 "test_duckdb.cs"

csc /reference:DuckDB.NET.Data.dll test_duckdb.cs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nRunning test..." -ForegroundColor Yellow
    .\test_duckdb.exe
    
    # Cleanup
    Remove-Item test_duckdb.cs, test_duckdb.exe -ErrorAction SilentlyContinue
} else {
    Write-Host "? Compilation failed" -ForegroundColor Red
}
```

## Alternative: Direct PowerShell Test

```powershell
# Load DuckDB assembly
Add-Type -Path "C:\Users\Ohad\source\repos\AeroDebrief\src\AeroDebrief.CLI\bin\x64\Debug\net9.0\DuckDB.NET.Data.dll"

$dbPath = "test.duckdb"
if (Test-Path $dbPath) { Remove-Item $dbPath }

$conn = New-Object DuckDB.NET.Data.DuckDBConnection("Data Source=$dbPath")
$conn.Open()

$cmd = $conn.CreateCommand()

# Test with 2GiB
Write-Host "Testing: SET memory_limit='2GiB'"
$cmd.CommandText = "SET memory_limit='2GiB'"
try {
    $cmd.ExecuteNonQuery()
    Write-Host "? SUCCESS" -ForegroundColor Green
} catch {
    Write-Host "? FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

$conn.Close()
Remove-Item $dbPath -ErrorAction SilentlyContinue
```

## What This Will Tell Us

1. **If Test 1 fails and Test 2 succeeds**: DuckDB requires 'GiB' (as expected)
2. **If both tests fail**: DuckDB version issue or syntax problem
3. **If both tests succeed**: Something else is wrong with our code

Run this test to isolate whether it's:
- ? The DuckDB library itself
- ? Our code
- ? The connection string
- ? Something else

---

**After running this test, share the output and we can pinpoint the exact problem!**
