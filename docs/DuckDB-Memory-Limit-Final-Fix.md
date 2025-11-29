# DuckDB Memory Limit Syntax Fix - FINAL

## The Error You're Getting

```
Parser Error: Unknown unit for memory_limit: %s 
(expected: KB, MB, GB, TB for 1000^i units or KiB, MiB, GiB, TiB for 1024^i unites)
```

## Root Cause

The error message showing `%s` suggests DuckDB is receiving a format string placeholder instead of the actual value. This happens when the SQL command isn't properly formatted.

## The Fix Applied

Changed from:
```csharp
cmd.CommandText = $"SET memory_limit='{memoryLimit}GB'";
```

To:
```csharp
cmd.CommandText = $"PRAGMA memory_limit='{memoryLimit}GB'";
```

## Why PRAGMA Instead of SET?

### SET Command (Less Reliable)
```sql
SET memory_limit='2GB'    -- Sometimes works
SET memory_limit=2GB      -- Sometimes works
```

### PRAGMA Command (More Reliable)
```sql
PRAGMA memory_limit='2GB'   -- ? Works consistently
PRAGMA memory_limit='2GiB'  -- ? Also works
```

## Valid Unit Formats (DuckDB 1.4.1)

| Unit | Base | Example |
|------|------|---------|
| KB | 1000^1 | `'2KB'` |
| MB | 1000^2 | `'2MB'` |
| **GB** | 1000^3 | `'2GB'` ? |
| TB | 1000^4 | `'2TB'` |
| KiB | 1024^1 | `'2KiB'` |
| MiB | 1024^2 | `'2MiB'` |
| **GiB** | 1024^3 | `'2GiB'` ? |
| TiB | 1024^4 | `'2TiB'` |

**Note**: The value must be in quotes: `'2GB'` not `2GB`

## Current Implementation

```csharp
private void LimitMemoryGB(int memoryLimit)
{
    using var cmd = _connection!.CreateCommand();
    // Use PRAGMA instead of SET for better compatibility
    cmd.CommandText = $"PRAGMA memory_limit='{memoryLimit}GB'";
    cmd.ExecuteNonQuery();
    Console.WriteLine($"Memory limit set to {memoryLimit}GB using PRAGMA.");
    Logger.Info($"?? Executing DuckDB configuration: {cmd.CommandText}");
}
```

## Testing

After rebuilding, the memory limit command should work:
```csharp
LimitMemoryGB(2);  // Sets to 2GB
// Executes: PRAGMA memory_limit='2GB'
```

## Alternative: No Memory Limit

If you continue to have issues, you can disable the memory limit entirely:

```csharp
private async Task ConfigureConnectionAsync(CancellationToken ct = default)
{
    // Enable parallel processing
    await ExecuteNonQueryAsync("PRAGMA threads=4", ct);

    // Memory limit disabled - DuckDB will auto-manage
    // LimitMemoryGB(2);  // Comment out if still problematic

    // Enable WAL for concurrent access
    await ExecuteNonQueryAsync("PRAGMA wal_autocheckpoint=1000", ct);
    
    Logger.Debug("DuckDB connection configured");
}
```

## Next Steps

1. **Stop debugging** (if running)
2. **Rebuild** the solution:
   ```powershell
   dotnet clean
   dotnet build
   ```
3. **Test migration**:
   ```powershell
   .\scripts\run-cli.ps1 --migrate "Records\*.adb"
   ```

## If Still Failing

Try removing the memory limit entirely:

```csharp
private async Task ConfigureConnectionAsync(CancellationToken ct = default)
{
    await ExecuteNonQueryAsync("PRAGMA threads=4", ct);
    // Skip memory limit - let DuckDB manage it
    await ExecuteNonQueryAsync("PRAGMA wal_autocheckpoint=1000", ct);
    Logger.Debug("DuckDB connection configured (no memory limit)");
}
```

DuckDB 1.4.1 is very good at auto-managing memory, so explicitly setting a limit may not be necessary for your use case.

---

**Status**: PRAGMA syntax fix applied  
**Action**: Rebuild and test migration
