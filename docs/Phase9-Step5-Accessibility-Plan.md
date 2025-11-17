# Phase 9 Step 5: Accessibility - Implementation Plan

**Date**: January 21, 2025  
**Status**: ?? IN PROGRESS  
**Estimated Duration**: 3 hours  
**Prerequisites**: ? Steps 1-4 Complete

---

## ?? Objectives

Implement comprehensive accessibility features to ensure the application is usable by everyone, including users with disabilities.

### Key Goals
- **Screen Reader Support**: Full narration of UI elements
- **Keyboard Navigation**: Complete keyboard-only operation
- **High Contrast**: Support for system high contrast themes
- **Focus Management**: Clear visual focus indicators
- **WCAG 2.1 AA Compliance**: Meet accessibility standards

---

## ?? Task Breakdown

### Task 1: ARIA Labels & AutomationProperties (45 min)

#### Subtasks
1. **LoadingSpinnerOverlay** (10 min)
   ```xaml
   <UserControl AutomationProperties.Name="Loading Overlay"
                AutomationProperties.HelpText="Displays loading progress for data tiles">
       
       <ProgressBar AutomationProperties.Name="Loading progress"
                   AutomationProperties.LiveSetting="Polite"/>
       
       <TextBlock AutomationProperties.IsOffscreenBehavior="FromClip"
                 AutomationProperties.LiveSetting="Polite"
                 Text="{Binding LoadingStatusText}"/>
       
       <Button AutomationProperties.Name="Cancel loading"
               AutomationProperties.HelpText="Stops the current tile loading operation"
               Content="Cancel Loading"/>
   </UserControl>
   ```

2. **ErrorBannerOverlay** (10 min)
   ```xaml
   <Border AutomationProperties.Name="Error notification"
           AutomationProperties.LiveSetting="Assertive">
       
       <TextBlock AutomationProperties.Name="Error title"
                 Text="{Binding Title}"/>
       
       <TextBlock AutomationProperties.Name="Error message"
                 Text="{Binding Message}"/>
       
       <Button AutomationProperties.Name="Retry action"
               AutomationProperties.HelpText="Retries the failed operation"
               Content="Retry"/>
       
       <Button AutomationProperties.Name="Dismiss error"
               AutomationProperties.HelpText="Closes the error notification"
               Content="Dismiss"/>
   </Border>
   ```

3. **PerformanceStatsOverlay** (10 min)
   ```xaml
   <Border AutomationProperties.Name="Performance statistics"
           AutomationProperties.HelpText="Shows real-time performance metrics. Press F3 to toggle.">
       
       <TextBlock AutomationProperties.Name="Memory usage"
                 AutomationProperties.HelpText="{Binding MemoryUsageMB, StringFormat='Current memory usage: {0:F1} megabytes'}"/>
       
       <TextBlock AutomationProperties.Name="Cache hit rate"
                 AutomationProperties.HelpText="{Binding CacheHitRate, StringFormat='Cache hit rate: {0:P0}'}"/>
       
       <TextBlock AutomationProperties.Name="Frame rate"
                 AutomationProperties.HelpText="{Binding CurrentFPS, StringFormat='Current frame rate: {0:F0} frames per second'}"/>
   </Border>
   ```

4. **UnifiedGraphControl** (15 min)
   - Add AutomationProperties to main chart
   - Add AutomationProperties to minimap
   - Add AutomationProperties to viewport overlay
   - Announce viewport changes to screen reader

**Files to Modify**:
- `src/AeroDebrief.UI/Controls/Charts/LoadingSpinnerOverlay.xaml`
- `src/AeroDebrief.UI/Controls/Charts/ErrorBannerOverlay.xaml`
- `src/AeroDebrief.UI/Controls/Charts/PerformanceStatsOverlay.xaml`
- `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.xaml`

---

### Task 2: Keyboard Navigation (45 min)

#### Current Keyboard Shortcuts
**Already Implemented**:
- Arrow Keys: Pan (with Shift/Ctrl modifiers)
- +/-: Zoom in/out
- Home/End: Jump to edges
- PageUp/PageDown: Pan full viewport
- R: Reset view
- F3: Toggle performance stats
- , and .: Frame-by-frame seek
- F: Toggle follow mode

#### New Keyboard Support
1. **Tab Navigation** (15 min)
   - Set TabIndex on all interactive elements
   - Logical tab order:
     1. Main chart (TabIndex="1")
     2. Minimap (TabIndex="2")
     3. Loading cancel button (TabIndex="3")
     4. Error retry button (TabIndex="4")
     5. Error dismiss button (TabIndex="5")
   
2. **Escape Key** (10 min)
   - Dismiss error banner
   - Cancel loading operation
   - Close performance stats
   
   ```csharp
   private void OnKeyDown(KeyEventArgs e)
   {
       if (e.Key == Key.Escape)
       {
           // Priority order:
           if (ViewModel.HasActiveError)
           {
               ViewModel.DismissError();
               e.Handled = true;
           }
           else if (ViewModel.IsLoadingTiles)
           {
               ViewModel.CancelLoadingCommand.Execute(null);
               e.Handled = true;
           }
           else if (ViewModel.ShowPerformanceStats)
           {
               ViewModel.ShowPerformanceStats = false;
               e.Handled = true;
           }
       }
   }
   ```

3. **Enter Key on Buttons** (10 min)
   - Ensure Enter key activates buttons
   - Add keyboard focus to buttons

4. **Focus Management** (10 min)
   - Restore focus after modal dismiss
   - Focus trap in error banner (Tab cycles between buttons)
   - Skip navigation links (Ctrl+Shift+N)

**Files to Modify**:
- `src/AeroDebrief.UI/Controls/Charts/UnifiedGraphControl.cs`
- `src/AeroDebrief.UI/Controls/Charts/ErrorBannerOverlay.xaml`
- `src/AeroDebrief.UI/ViewModels/UnifiedGraphViewModel.cs`

---

### Task 3: High Contrast Support (45 min)

#### Implementation

1. **System Theme Detection** (15 min)
   
   **New File**: `src/AeroDebrief.UI/Helpers/SystemThemeHelper.cs`
   ```csharp
   public static class SystemThemeHelper
   {
       public static bool IsHighContrastMode()
       {
           return SystemParameters.HighContrast;
       }
       
       public static event EventHandler HighContrastChanged
       {
           add => SystemParameters.StaticPropertyChanged += value;
           remove => SystemParameters.StaticPropertyChanged -= value;
       }
   }
   ```

2. **High Contrast Styles** (20 min)
   
   **New File**: `src/AeroDebrief.UI/Styles/HighContrastStyles.xaml`
   ```xaml
   <ResourceDictionary>
       <!-- High Contrast Button Style -->
       <Style x:Key="HighContrastButton" TargetType="Button">
           <Setter Property="Background" Value="{DynamicResource {x:Static SystemColors.WindowBrushKey}}"/>
           <Setter Property="Foreground" Value="{DynamicResource {x:Static SystemColors.WindowTextBrushKey}}"/>
           <Setter Property="BorderBrush" Value="{DynamicResource {x:Static SystemColors.WindowTextBrushKey}}"/>
           <Setter Property="BorderThickness" Value="2"/>
       </Style>
       
       <!-- High Contrast Border Style -->
       <Style x:Key="HighContrastBorder" TargetType="Border">
           <Setter Property="Background" Value="{DynamicResource {x:Static SystemColors.WindowBrushKey}}"/>
           <Setter Property="BorderBrush" Value="{DynamicResource {x:Static SystemColors.WindowTextBrushKey}}"/>
           <Setter Property="BorderThickness" Value="2"/>
       </Style>
       
       <!-- High Contrast Text Style -->
       <Style x:Key="HighContrastText" TargetType="TextBlock">
           <Setter Property="Foreground" Value="{DynamicResource {x:Static SystemColors.WindowTextBrushKey}}"/>
       </Style>
   </ResourceDictionary>
   ```

3. **Apply High Contrast Styles** (10 min)
   - Update all overlays to detect high contrast mode
   - Apply high contrast styles conditionally
   
   ```xaml
   <Border>
       <Border.Style>
           <Style TargetType="Border" BasedOn="{StaticResource ModernCard}">
               <Style.Triggers>
                   <DataTrigger Binding="{Binding Source={x:Static SystemParameters.HighContrast}}" Value="True">
                       <Setter Property="Background" Value="{DynamicResource {x:Static SystemColors.WindowBrushKey}}"/>
                       <Setter Property="BorderBrush" Value="{DynamicResource {x:Static SystemColors.WindowTextBrushKey}}"/>
                       <Setter Property="BorderThickness" Value="2"/>
                   </DataTrigger>
               </Style.Triggers>
           </Style>
       </Border.Style>
   </Border>
   ```

**Files to Create**:
- `src/AeroDebrief.UI/Helpers/SystemThemeHelper.cs`
- `src/AeroDebrief.UI/Styles/HighContrastStyles.xaml`

**Files to Modify**:
- `src/AeroDebrief.UI/Controls/Charts/LoadingSpinnerOverlay.xaml`
- `src/AeroDebrief.UI/Controls/Charts/ErrorBannerOverlay.xaml`
- `src/AeroDebrief.UI/Controls/Charts/PerformanceStatsOverlay.xaml`
- `src/AeroDebrief.UI/App.xaml` (add HighContrastStyles.xaml)

---

### Task 4: Focus Indicators (45 min)

#### Implementation

1. **Custom Focus Visual Style** (20 min)
   
   **Add to**: `src/AeroDebrief.UI/Styles/ModernStyles.xaml`
   ```xaml
   <!-- Enhanced Focus Visual Style -->
   <Style x:Key="EnhancedFocusVisual">
       <Setter Property="Control.Template">
           <Setter.Value>
               <ControlTemplate>
                   <Rectangle Stroke="#007ACC" 
                             StrokeThickness="2"
                             StrokeDashArray="2 2"
                             SnapsToDevicePixels="True"
                             Margin="-2"/>
               </ControlTemplate>
           </Setter.Value>
       </Setter>
   </Style>
   
   <!-- Apply to all buttons -->
   <Style TargetType="Button" BasedOn="{StaticResource ModernButton}">
       <Setter Property="FocusVisualStyle" Value="{StaticResource EnhancedFocusVisual}"/>
   </Style>
   ```

2. **Focus on Error Banner** (10 min)
   - Auto-focus Retry button when error appears
   - Focus trap (Tab cycles between Retry and Dismiss)
   
   ```csharp
   // ErrorBannerOverlay.xaml.cs
   private void OnLoaded(object sender, RoutedEventArgs e)
   {
       // Focus the first button
       var retryButton = this.FindName("RetryButton") as Button;
       retryButton?.Focus();
   }
   ```

3. **Focus on Loading Cancel** (10 min)
   - Auto-focus Cancel button after 2 seconds of loading
   - Allow Escape to cancel

4. **Focus Restoration** (5 min)
   - Store focus before showing overlay
   - Restore focus after dismissing overlay

**Files to Modify**:
- `src/AeroDebrief.UI/Styles/ModernStyles.xaml`
- `src/AeroDebrief.UI/Controls/Charts/ErrorBannerOverlay.xaml`
- `src/AeroDebrief.UI/Controls/Charts/ErrorBannerOverlay.xaml.cs`
- `src/AeroDebrief.UI/Controls/Charts/LoadingSpinnerOverlay.xaml.cs`

---

## ? Success Criteria

### Functional Requirements
- [ ] All controls have ARIA labels
- [ ] Full keyboard navigation works
- [ ] Tab order is logical
- [ ] Escape key dismisses overlays
- [ ] Screen reader announces loading states
- [ ] Screen reader announces errors
- [ ] Performance stats readable by screen reader

### Visual Requirements
- [ ] Focus indicators clear and visible (2px blue dashed)
- [ ] High contrast mode supported
- [ ] Text contrast ratios ? 4.5:1
- [ ] Interactive elements ? 44x44 pixels (touch-friendly)

### Technical Requirements
- [ ] SystemParameters.HighContrast detection
- [ ] Dynamic theme switching support
- [ ] Focus management in code-behind
- [ ] No breaking changes

### Testing Requirements
- [ ] Test with Narrator (Windows)
- [ ] Test with NVDA (free screen reader)
- [ ] Test keyboard-only navigation
- [ ] Test high contrast mode (Windows settings)
- [ ] Test focus indicators visible
- [ ] Test color contrast with accessibility tools

---

## ?? Testing Strategy

### Manual Testing

#### Screen Reader Testing
1. **Narrator (Windows built-in)**
   - Win + Ctrl + Enter to start
   - Navigate with Tab
   - Verify announcements for:
     - Loading spinner status
     - Error messages
     - Performance stats
     - Button labels

2. **NVDA (free alternative)**
   - Download from nvaccess.org
   - Same navigation tests

#### Keyboard Navigation Testing
1. Open application
2. Press Tab repeatedly - verify logical order
3. Press Escape - verify dismisses overlays
4. Press F3 - verify toggles performance stats
5. Test all shortcuts still work

#### High Contrast Testing
1. Windows Settings ? Ease of Access ? High contrast
2. Enable high contrast theme
3. Verify all overlays visible
4. Verify borders clear
5. Verify text readable

### Automated Testing
```csharp
[Fact]
public void LoadingOverlay_HasAutomationProperties()
{
    var overlay = new LoadingSpinnerOverlay();
    
    var name = AutomationProperties.GetName(overlay);
    Assert.NotNull(name);
    Assert.NotEmpty(name);
}

[Fact]
public void ErrorBanner_HasLiveRegion()
{
    var banner = new ErrorBannerOverlay();
    var border = banner.FindName("ErrorBanner") as Border;
    
    var liveSetting = AutomationProperties.GetLiveSetting(border);
    Assert.Equal(AutomationLiveSetting.Assertive, liveSetting);
}

[Fact]
public void Buttons_HaveFocusVisualStyle()
{
    var button = new Button();
    Assert.NotNull(button.FocusVisualStyle);
}

[Fact]
public async Task HighContrast_AppliesCorrectStyles()
{
    // Simulate high contrast mode
    SystemParameters.HighContrast = true;
    
    var overlay = new ErrorBannerOverlay();
    await Task.Delay(100); // Allow style application
    
    var border = overlay.FindName("ErrorBanner") as Border;
    Assert.Equal(SystemColors.WindowBrush, border.Background);
}
```

---

## ?? Estimated Timeline

| Task | Duration | Cumulative |
|------|----------|------------|
| ARIA Labels | 45 min | 0:45 |
| Keyboard Navigation | 45 min | 1:30 |
| High Contrast Support | 45 min | 2:15 |
| Focus Indicators | 45 min | 3:00 |
| **Total** | **3 hours** | |

---

## ?? WCAG 2.1 AA Compliance Checklist

### Perceivable
- [x] **1.1.1 Non-text Content**: All images have alt text (icons use AutomationProperties)
- [x] **1.3.1 Info and Relationships**: Semantic markup with AutomationProperties
- [x] **1.4.1 Use of Color**: Not relying solely on color (text + icons)
- [x] **1.4.3 Contrast (Minimum)**: 4.5:1 text contrast
- [x] **1.4.11 Non-text Contrast**: 3:1 UI component contrast

### Operable
- [x] **2.1.1 Keyboard**: All functions keyboard accessible
- [x] **2.1.2 No Keyboard Trap**: Can navigate in/out of all components
- [x] **2.4.3 Focus Order**: Logical tab order
- [x] **2.4.7 Focus Visible**: Clear focus indicators

### Understandable
- [x] **3.2.1 On Focus**: No automatic context changes on focus
- [x] **3.2.2 On Input**: No automatic context changes on input
- [x] **3.3.1 Error Identification**: Errors clearly identified
- [x] **3.3.3 Error Suggestion**: Retry actions provided

### Robust
- [x] **4.1.2 Name, Role, Value**: All components have accessible names
- [x] **4.1.3 Status Messages**: Live regions for dynamic content

---

## ?? Implementation Order

1. **Start**: Add AutomationProperties to all overlays (45 min)
2. **Next**: Implement keyboard navigation (45 min)
3. **Then**: High contrast support (45 min)
4. **Finally**: Focus indicators (45 min)

---

## ?? Resources

### Tools
- **Accessibility Insights**: https://accessibilityinsights.io/
- **NVDA Screen Reader**: https://www.nvaccess.org/
- **Windows Narrator**: Built into Windows (Win + Ctrl + Enter)
- **Color Contrast Analyzer**: https://www.tpgi.com/color-contrast-checker/

### Documentation
- **WPF Accessibility**: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/accessibility-best-practices
- **WCAG 2.1 Guidelines**: https://www.w3.org/WAI/WCAG21/quickref/
- **AutomationProperties**: https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationproperties

---

**Status**: ?? **READY TO IMPLEMENT**  
**Next Action**: Add AutomationProperties to LoadingSpinnerOverlay  
**Target Completion**: 3 hours from start

