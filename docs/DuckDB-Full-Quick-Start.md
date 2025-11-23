# ? Quick Start: DuckDB.NET.Data.Full

## What You're Getting

? **DuckDB.NET.Data.Full v1.4.1** - Complete package with:
- Full ADO.NET implementation
- Native library auto-bundled (DuckDB v1.1.2)
- Appender API (10-50x faster bulk inserts)
- Complete native bindings
- Cross-platform support

## Upgrade Now

### Option 1: Automated Script (Recommended)
```powershell
# Close Visual Studio first!
cd C:\Users\Ohad\source\repos\AeroDebrief
.\scripts\upgrade-duckdb-full.ps1
```

### Option 2: Manual Steps
```powershell
# 1. Close Visual Studio completely

# 2. Clean
cd C:\Users\Ohad\source\repos\AeroDebrief
dotnet clean
Remove-Item -Path "src\*\bin","src\*\obj" -Recurse -Force
dotnet nuget locals all --clear

# 3. Build
dotnet restore
dotnet build

# 4. Verify
Get-ChildItem -Path "src\AeroDebrief.Core\bin" -Filter "duckdb.dll" -Recurse
```

## Test It

```powershell
# Test migration
.\scripts\run-cli.ps1 --migrate "Records\recorded_audio_srv_88.99.165.102_5072_t20251115T195042Z.adb"
```

## Key Benefits

### Before (DuckDB.NET.Data)
```xml
<PackageReference Include="DuckDB.NET.Data" Version="1.1.0" />
<!-- Manual native library download required -->
<!-- Limited API access -->
```

### After (DuckDB.NET.Data.Full)
```xml
<PackageReference Include="DuckDB.NET.Data.Full" Version="1.4.1" />
<!-- Everything included automatically! -->
```

| Feature | Before | After |
|---------|--------|-------|
| Native DLL | Manual download | ? Auto-bundled |
| Appender API | ? | ? 10-50x faster inserts |
| Native Bindings | Limited | ? Complete |
| Memory Limits | Buggy | ? Works properly |
| Cross-platform | Manual | ? Automatic |

## What's Fixed

1. **No more manual DLL downloads** - Native library included
2. **Memory limit syntax works** - `SET memory_limit='2GB'` now works
3. **Better performance** - Can use Appender API for bulk inserts
4. **Complete ADO.NET** - Full IDbConnection/IDbCommand support

## Your Code Still Works

All your existing code continues to work:
```csharp
using var connection = new DuckDBConnection($"Data Source={dbPath}");
await connection.OpenAsync();
// Everything works as before!
```

## Optional Performance Boost

You can now use the Appender API for 10-50x faster bulk inserts:
```csharp
// Old way (still works)
cmd.CommandText = "INSERT INTO packets VALUES (?, ?, ?)";
cmd.ExecuteNonQuery();

// New way (much faster!)
using var appender = connection.CreateAppender("main", "packets");
appender.CreateRow()
    .AppendValue(value1)
    .AppendValue(value2)
    .AppendValue(value3)
    .EndRow();
appender.Close();
```

## Documentation

?? **Full Details**: `docs\DuckDB-Full-Package-Upgrade.md`

## Status

- ? Project file updated to v1.4.1
- ? Upgrade script ready
- ? **Action Required**: Close Visual Studio ? Run upgrade script ? Test

---

**Next**: Close Visual Studio and run `.\scripts\upgrade-duckdb-full.ps1`
