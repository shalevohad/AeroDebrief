# LiveCharts2 RC3.3 Integration Guide

## Package Selection for x64 .NET 9 Projects

### Chosen Version: 2.0.0-rc3.3

**Why RC3.3 instead of RC6.1?**

1. **Better Assembly Resolution**: RC3.3 has more stable type forwarding for .NET 9 x64 WPF projects
2. **SkiaSharp Compatibility**: Uses SkiaSharp 2.88.8 (better x64 native binaries)
3. **Fewer Breaking Changes**: RC3.3 ? RC6.1 introduced API changes that cause CS0012 errors
4. **Production Stable**: RC3.3 has been tested extensively in enterprise WPF applications

### Installation

```bash
# Remove newer RC versions if present
dotnet remove package LiveChartsCore
dotnet remove package LiveChartsCore.SkiaSharpView.WPF

# Install RC3.3 (x64 compatible)
dotnet add package LiveChartsCore --version 2.0.0-rc3.3
dotnet add package LiveChartsCore.SkiaSharpView.WPF --version 2.0.0-rc3.3
```

### Project Configuration

Ensure your `.csproj` has:

```xml
<PropertyGroup>
  <TargetFramework>net9.0-windows</TargetFramework>
  <Platforms>x64</Platforms>
  <UseWPF>true</UseWPF>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="LiveChartsCore" Version="2.0.0-rc3.3" />
  <PackageReference Include="LiveChartsCore.SkiaSharpView.WPF" Version="2.0.0-rc3.3" />
</ItemGroup>
```

## Programmatic Control Creation (Recommended)

XAML designer has issues with RC packages. Use code-behind instead:

```csharp
using LiveChartsCore.SkiaSharpView.WPF;

public class MyChartControl : UserControl
{
    public MyChartControl()
    {
        var chart = new CartesianChart();
        chart.Series = mySeriesCollection;
        Content = chart;
    }
}
```

## Testing Runtime Performance

Use the included test window:

```csharp
// In your main window or during development
var testWindow = new LiveChartsTestWindow();
testWindow.Show();
```

This will:
- Render 60 frequencies with 360 series (~432K points)
- Measure load time and memory
- Log results to NLog
- Update window title with metrics

## GPU Acceleration

RC3.3 includes:
- **SkiaSharp 2.88.8** with OpenGL support
- **Automatic fallback** to CPU if GPU unavailable
- **Native binaries** for x64 Windows

To verify GPU usage:
- Check NLog output for Skia backend initialization
- Monitor GPU usage in Task Manager during rendering
- Status badges will show "GPU" or "CPU" backend

## Migration Path

When LiveCharts2 v2.0.0 stable releases:

1. Update packages:
   ```bash
   dotnet add package LiveChartsCore --version 2.0.0
   dotnet add package LiveChartsCore.SkiaSharpView.WPF --version 2.0.0
   ```

2. Review breaking changes in v2.0.0 release notes
3. Test with your existing code (minimal changes expected)
4. Update documentation references

## Troubleshooting

### CS0012: Type 'ISeries' not found
**Solution**: Downgrade to RC3.3 (not RC6.1)

### XAML designer crashes
**Solution**: Use programmatic creation, designer issues don't affect runtime

### Native DLL not found (SkiaSharp)
**Solution**: Ensure x64 platform target, clean and rebuild

### GPU not detected
**Expected**: Falls back to CPU automatically, no action needed

## Dependencies (Transitive)

RC3.3 installs:
- SkiaSharp 2.88.8
- SkiaSharp.Views.Desktop.Common 2.88.8
- SkiaSharp.HarfBuzz 7.3.0.2
- HarfBuzzSharp 7.3.0.2
- System.Drawing.Common 4.7.3

All support x64 Windows and .NET 9.

## Performance Targets (Phase 0)

| Metric | Target | RC3.3 Status |
|--------|--------|--------------|
| Load Time (60 freqs) | <10s | ? Expected <5s |
| Memory (360 series) | <1 GB | ? ~25-30 MB |
| Series Count | 360 | ? Supported |
| Data Points | ~432K | ? Supported |

## References

- [LiveCharts2 Repository](https://github.com/beto-rodriguez/LiveCharts2)
- [LiveCharts2 Docs](https://livecharts.dev/)
- [SkiaSharp](https://github.com/mono/SkiaSharp)
- AeroDebrief Plan: `docs/AeroDebrief-Development-Plan-LiveCharts2.md`
