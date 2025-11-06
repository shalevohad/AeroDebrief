# UI Design - Tacview Integration Interface

## Overview

The Tacview integration UI provides visual feedback about connection status, synchronization quality, and selected pilots. It integrates seamlessly into the existing AeroDebrief player interface.

**Note**: Spatial audio (pan) configuration is done **in Tacview's menu**, not in AeroDebrief. AeroDebrief displays the pan values but Tacview controls them.

## UI Components

### 1. TacviewStatusControl (Main Status Display)

**Location**: Docked in the player header or as a separate panel

**Visual Design**:
```
???????????????????????????????????????????????????????????
?  ?? Tacview Sync                                     ??  ?
?  ???????????????????????????????????????????????????? ?
?  Status: ? Connected                                    ?
?  Sync Quality: ?????????? 85% (Good)                   ?
?  Drift: +120 ms                                         ?
?  Selected Pilots: 2 | Pan Mode: Manual                 ?
?  ???????????????????????????????????????????????????? ?
?  ?? Viper 1-1 (F-16C) [251.0, 305.0 MHz]          ???  ?
?  ?? Viper 1-2 (F-16C) [251.0, 305.0 MHz]          ???  ?
?  ?? Configure pan in Tacview menu: AeroDebrief Sync    ?
???????????????????????????????????????????????????????????
```

**States**:
- **Disconnected** (?? Red): No connection to Tacview
- **Connecting** (?? Yellow): Attempting to connect
- **Connected** (?? Green): Connected but not synced
- **Synchronized** (?? Green + checkmark): Fully operational
- **Degraded** (?? Yellow): Connected but poor sync quality

---

### 2. Connection Status Indicator

**Icon States**:
| State | Icon | Color | Description |
|-------|------|-------|-------------|
| Disconnected | ?? | Red | Not connected to Tacview |
| Connecting | ?? | Yellow (pulsing) | Attempting connection |
| Connected | ?? | Green | TCP connected |
| Synchronized | ? | Green | Fully synced and operational |
| Degraded | ?? | Orange | Connected but high drift |

**XAML**:
```xaml
<Grid>
    <Ellipse Width="16" Height="16" 
             Fill="{Binding ConnectionStatusColor}"
             Visibility="{Binding IsNotSynced}"/>
    <Path Data="{StaticResource CheckmarkIcon}"
          Fill="Green"
          Width="16" Height="16"
          Visibility="{Binding IsSynchronized}"/>
    <Path Data="{StaticResource WarningIcon}"
          Fill="Orange"
          Width="16" Height="16"
          Visibility="{Binding IsDegraded}"/>
</Grid>
```

---

### 3. Sync Quality Bar

**Visual**:
```
Sync Quality: ?????????? 85% (Good)
              ????????
              Excellent  Good  Fair  Poor
```

**Quality Levels**:
| Level | Percentage | Color | Drift Range |
|-------|-----------|-------|-------------|
| Excellent | 95-100% | Dark Green | <100ms |
| Good | 80-94% | Green | 100-500ms |
| Fair | 60-79% | Yellow | 500ms-1s |
| Poor | 40-59% | Orange | 1-2s |
| Critical | 0-39% | Red | >2s |

**XAML**:
```xaml
<StackPanel Orientation="Horizontal" Spacing="8">
    <TextBlock Text="Sync Quality:" VerticalAlignment="Center"/>
    <ProgressBar Width="150" Height="20"
                 Minimum="0" Maximum="100"
                 Value="{Binding SyncQualityPercentage}"
                 Foreground="{Binding SyncQualityColor}"/>
    <TextBlock Text="{Binding SyncQualityText}"
               Foreground="{Binding SyncQualityColor}"
               VerticalAlignment="Center"/>
</StackPanel>
```

---

### 4. Drift Display

**Visual**:
```
Drift: +120 ms    (positive = AeroDebrief ahead)
Drift: -85 ms     (negative = AeroDebrief behind)
Drift: ±5 ms      (excellent sync)
```

**Color Coding**:
- Green: <100ms
- Yellow: 100-500ms
- Orange: 500ms-1s
- Red: >1s

**XAML**:
```xaml
<StackPanel Orientation="Horizontal" Spacing="4">
    <TextBlock Text="Drift:" FontWeight="SemiBold"/>
    <TextBlock Text="{Binding DriftText}"
               Foreground="{Binding DriftColor}"
               FontFamily="Consolas"/>
</StackPanel>
```

---

### 5. Selected Pilots List

**Visual**:
```
????????????????????????????????????????????????????
? Selected Pilots (2) | Pan Mode: Manual          ?
????????????????????????????????????????????????????
? ?? Viper 1-1 (F-16C)                        ???  ?
?    251.0 MHz, 305.0 MHz                          ?
?    ?? Blue Coalition                              ?
? ???????????????????????????????????????????????  ?
? ?? Viper 1-2 (F-16C)                        ???  ?
?    251.0 MHz, 305.0 MHz                          ?
?    ?? Blue Coalition                              ?
????????????????????????????????????????????????????
? ?? To adjust pan: Tacview ? AeroDebrief Sync    ?
?    ? Configure Audio Pan                         ?
????????????????????????????????????????????????????
```

**Pan Indicator**:
- `???` = Full Left (pan -1.0 to -0.8)
- `??` = Mostly Left (pan -0.8 to -0.3)
- `?` = Slightly Left (pan -0.3 to -0.1)
- `??` = Center (pan -0.1 to +0.1)
- `?` = Slightly Right (pan +0.1 to +0.3)
- `??` = Mostly Right (pan +0.3 to +0.8)
- `???` = Full Right (pan +0.8 to +1.0)

**Speaking Indicator**:
When a pilot is speaking, highlight their row:
```
? ?? Viper 1-1 (F-16C) [SPEAKING]            ???  ?
  ?????????????????????????????????????????????
  Highlighted background, bold text
```

**XAML**:
```xaml
<StackPanel>
    <!-- Header with Pan Mode indicator -->
    <Grid Margin="8,4">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        
        <TextBlock Grid.Column="0" 
                   Text="{Binding PilotHeaderText}"
                   FontWeight="SemiBold"/>
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="4">
            <TextBlock Text="Pan Mode:" FontSize="11" Foreground="Gray"/>
            <TextBlock Text="{Binding PanMode}" FontSize="11" FontWeight="SemiBold"/>
        </StackPanel>
    </Grid>
    
    <Separator/>
    
    <!-- Pilots List -->
    <ItemsControl ItemsSource="{Binding SelectedPilots}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Background="{Binding IsSpeakingBrush}"
                        Padding="8" Margin="0,2"
                        CornerRadius="4">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        
                        <StackPanel Grid.Column="0">
                            <StackPanel Orientation="Horizontal" Spacing="6">
                                <TextBlock Text="{Binding SpeakingIcon}"
                                           FontSize="16"/>
                                <TextBlock Text="{Binding DisplayName}"
                                           FontWeight="{Binding SpeakingWeight}"/>
                                <TextBlock Text="{Binding UnitType}"
                                           Foreground="Gray"
                                           FontStyle="Italic"/>
                            </StackPanel>
                            <TextBlock Text="{Binding FrequenciesText}"
                                       Foreground="Gray"
                                       FontSize="11"
                                       Margin="22,2,0,0"/>
                            <StackPanel Orientation="Horizontal" Spacing="4"
                                        Margin="22,2,0,0">
                                <Ellipse Width="8" Height="8"
                                         Fill="{Binding CoalitionColor}"/>
                                <TextBlock Text="{Binding CoalitionName}"
                                           FontSize="11"
                                           Foreground="Gray"/>
                            </StackPanel>
                        </StackPanel>
                        
                        <TextBlock Grid.Column="1"
                                   Text="{Binding PanIndicator}"
                                   FontFamily="Consolas"
                                   FontSize="14"
                                   VerticalAlignment="Center"
                                   ToolTip="{Binding PanTooltip}"/>
                    </Grid>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Info banner about pan configuration -->
    <Border Background="{StaticResource InfoBackgroundBrush}"
            BorderBrush="{StaticResource InfoBorderBrush}"
            BorderThickness="1"
            Padding="8"
            Margin="4"
            CornerRadius="4"
            Visibility="{Binding HasSelectedPilots, Converter={StaticResource BoolToVisibilityConverter}}">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <TextBlock Text="??" FontSize="14" VerticalAlignment="Top"/>
            <TextBlock TextWrapping="Wrap" FontSize="11" Foreground="Gray">
                <Run Text="To adjust spatial audio pan:"/>
                <LineBreak/>
                <Run Text="In Tacview ? " FontWeight="SemiBold"/>
                <Run Text="AeroDebrief Sync ? Configure Audio Pan" FontStyle="Italic"/>
            </TextBlock>
        </StackPanel>
    </Border>
</StackPanel>
```

---

### 6. Settings Panel

**Expandable Settings** (Pan settings removed):
```
????????????????????????????????????????????????????
? ?? Tacview Integration Settings                  ?
????????????????????????????????????????????????????
? ? Auto-connect on startup                        ?
? ? Auto-reconnect on disconnect                   ?
? ? Enable sync drift correction                   ?
?                                                   ?
? Connection:                                       ?
? Host: [127.0.0.1]    Port: [52001]              ?
?                                                   ?
? Reconnection:                                     ?
? Interval: [5] seconds                            ?
? Max Attempts: [10]                               ?
?                                                   ?
? Advanced:                                         ?
? Max Drift: [500] ms                              ?
? Connection Timeout: [10000] ms                   ?
? Receive Timeout: [5000] ms                       ?
?                                                   ?
? [Test Connection]  [Reconnect Now]  [Save]       ?
?                                                   ?
? ?????????????????????????????????????????????  ?
? Note: Spatial audio pan is configured in         ?
? Tacview menu (AeroDebrief Sync ? Audio Pan)      ?
?                                                   ?
? Note: Port changes require restarting both       ?
? AeroDebrief and Tacview                          ?
????????????????????????????????????????????????????
```

**XAML**:
```xaml
<Expander Header="?? Tacview Integration Settings">
    <StackPanel Padding="12" Spacing="8">
        <CheckBox Content="Auto-connect on startup"
                  IsChecked="{Binding Config.AutoConnect}"/>
        <CheckBox Content="Auto-reconnect on disconnect"
                  IsChecked="{Binding Config.AutoReconnect}"/>
        <CheckBox Content="Enable sync drift correction"
                  IsChecked="{Binding Config.EnableSyncDriftCorrection}"/>
        
        <Separator Margin="0,8"/>
        
        <TextBlock Text="Connection" FontWeight="SemiBold" Margin="0,8,0,4"/>
        <Grid ColumnDefinitions="Auto,*,Auto,Auto" ColumnSpacing="8">
            <TextBlock Grid.Column="0" Text="Host:" VerticalAlignment="Center"/>
            <TextBox Grid.Column="1" Text="{Binding Config.Host}" 
                     ToolTip="Tacview TCP server host (usually 127.0.0.1 for localhost)"/>
            
            <TextBlock Grid.Column="2" Text="Port:" VerticalAlignment="Center" Margin="8,0,0,0"/>
            <TextBox Grid.Column="3" Text="{Binding Config.Port}" Width="80"
                     ToolTip="Tacview TCP server port (default: 52001)&#x0a;Must match Tacview addon port setting"/>
        </Grid>
        
        <Separator Margin="0,8"/>
        
        <TextBlock Text="Reconnection" FontWeight="SemiBold" Margin="0,8,0,4"/>
        <Grid ColumnDefinitions="Auto,*,Auto,Auto" ColumnSpacing="8">
            <TextBlock Grid.Column="0" Text="Interval:" VerticalAlignment="Center"/>
            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="4">
                <TextBox Text="{Binding Config.ReconnectIntervalSeconds}" Width="60"/>
                <TextBlock Text="seconds" VerticalAlignment="Center"/>
            </StackPanel>
            
            <TextBlock Grid.Column="2" Text="Max Attempts:" VerticalAlignment="Center" Margin="8,0,0,0"/>
            <TextBox Grid.Column="3" Text="{Binding Config.MaxReconnectAttempts}" Width="60"/>
        </Grid>
        
        <Separator Margin="0,8"/>
        
        <TextBlock Text="Advanced" FontWeight="SemiBold" Margin="0,8,0,4"/>
        <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto,Auto" ColumnSpacing="8" RowSpacing="8">
            <TextBlock Grid.Row="0" Grid.Column="0" Text="Max Drift:" VerticalAlignment="Center"/>
            <StackPanel Grid.Row="0" Grid.Column="1" Orientation="Horizontal" Spacing="4">
                <TextBox Text="{Binding Config.MaxAcceptableDriftMs}" Width="80"/>
                <TextBlock Text="ms" VerticalAlignment="Center"/>
            </StackPanel>
            
            <TextBlock Grid.Row="1" Grid.Column="0" Text="Connection Timeout:" VerticalAlignment="Center"/>
            <StackPanel Grid.Row="1" Grid.Column="1" Orientation="Horizontal" Spacing="4">
                <TextBox Text="{Binding Config.ConnectionTimeoutMs}" Width="80"/>
                <TextBlock Text="ms" VerticalAlignment="Center"/>
            </StackPanel>
            
            <TextBlock Grid.Row="2" Grid.Column="0" Text="Receive Timeout:" VerticalAlignment="Center"/>
            <StackPanel Grid.Row="2" Grid.Column="1" Orientation="Horizontal" Spacing="4">
                <TextBox Text="{Binding Config.ReceiveTimeoutMs}" Width="80"/>
                <TextBlock Text="ms" VerticalAlignment="Center"/>
            </StackPanel>
        </Grid>
        
        <Separator Margin="0,8"/>
        
        <StackPanel Orientation="Horizontal" Spacing="8">
            <Button Content="Test Connection" Command="{Binding TestConnectionCommand}"
                    ToolTip="Test connection to Tacview addon"/>
            <Button Content="Reconnect Now" Command="{Binding ReconnectCommand}"
                    ToolTip="Force reconnection to Tacview"/>
            <Button Content="Save Settings" Command="{Binding SaveSettingsCommand}"
                    ToolTip="Save current settings to config file"/>
        </StackPanel>
        
        <Separator Margin="0,8"/>
        
        <!-- Info about pan configuration -->
        <Border Background="{StaticResource InfoBackgroundBrush}"
                BorderBrush="{StaticResource InfoBorderBrush}"
                BorderThickness="1"
                Padding="8"
                CornerRadius="4">
            <StackPanel Spacing="4">
                <TextBlock Text="?? Spatial Audio Configuration" FontWeight="SemiBold" FontSize="11"/>
                <TextBlock TextWrapping="Wrap" FontSize="10" Foreground="Gray">
                    Spatial audio (stereo pan) is configured in Tacview's menu:
                </TextBlock>
                <TextBlock FontSize="10" Foreground="Gray" Margin="8,0,0,0">
                    <Run Text="• Tacview ? AeroDebrief Sync ? Configure Audio Pan"/>
                    <LineBreak/>
                    <Run Text="• Choose Auto or Manual pan mode"/>
                    <LineBreak/>
                    <Run Text="• In Manual mode, set pan per pilot"/>
                </TextBlock>
            </StackPanel>
        </Border>
        
        <!-- Warning about port changes -->
        <Border Background="{StaticResource WarningBackgroundBrush}"
                BorderBrush="{StaticResource WarningBorderBrush}"
                BorderThickness="1"
                Padding="8"
                CornerRadius="4"
                Margin="0,8,0,0">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="??" FontSize="14" VerticalAlignment="Top"/>
                <TextBlock TextWrapping="Wrap" FontSize="10" Foreground="Gray">
                    <Run Text="Port Configuration:" FontWeight="SemiBold"/>
                    <LineBreak/>
                    <Run Text="• Changes to Host or Port require restarting both AeroDebrief and Tacview"/>
                    <LineBreak/>
                    <Run Text="• Make sure the port matches in both Tacview addon settings and AeroDebrief"/>
                    <LineBreak/>
                    <Run Text="• Default port 52001 works for most users"/>
                </TextBlock>
            </StackPanel>
        </Border>
    </StackPanel>
</Expander>
```

---

## View Models

### TacviewIntegrationViewModel

```csharp
namespace AeroDebrief.UI.ViewModels;

public class TacviewIntegrationViewModel : ViewModelBase
{
    private readonly TacviewSyncService _syncService;
    private readonly ObservableCollection<TacviewPilotViewModel> _selectedPilots;
    private string _panMode = "Auto";
    
    // Connection State
    public ConnectionState ConnectionState => _syncService.State;
    public bool IsConnected => _syncService.IsConnected;
    public bool IsDisconnected => !IsConnected;
    public bool IsSynchronized => ConnectionState == ConnectionState.Synchronized;
    public bool IsDegraded => ConnectionState == ConnectionState.Degraded;
    public SolidColorBrush ConnectionStatusColor { get; }
    public string ConnectionStatusText { get; }
    
    // Sync Quality
    public int SyncQualityPercentage { get; }
    public string SyncQualityText { get; }
    public SolidColorBrush SyncQualityColor { get; }
    
    // Drift
    public string DriftText { get; }
    public SolidColorBrush DriftColor { get; }
    public TimeSpan CurrentDrift { get; }
    
    // Pilots
    public ObservableCollection<TacviewPilotViewModel> SelectedPilots => _selectedPilots;
    public int SelectedPilotCount => _selectedPilots.Count;
    public bool HasSelectedPilots => SelectedPilotCount > 0;
    
    // Pan Mode (received from Tacview)
    public string PanMode
    {
        get => _panMode;
        private set => SetProperty(ref _panMode, value);
    }
    
    public string PilotHeaderText => $"Selected Pilots ({SelectedPilotCount})";
    
    // Configuration
    public TacviewConfiguration Config { get; }
    
    // Commands
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    
    // Constructor
    public TacviewIntegrationViewModel(TacviewSyncService syncService)
    {
        _syncService = syncService;
        _selectedPilots = new ObservableCollection<TacviewPilotViewModel>();
        
        // Subscribe to events
        _syncService.ConnectionStateChanged += OnConnectionStateChanged;
        _syncService.SyncQualityChanged += OnSyncQualityChanged;
        _syncService.PilotSelectionChanged += OnPilotSelectionChanged;
        
        // Initialize commands
        ConnectCommand = new RelayCommand(async () => await ConnectAsync());
        DisconnectCommand = new RelayCommand(async () => await DisconnectAsync());
        ReconnectCommand = new RelayCommand(async () => await ReconnectAsync());
        TestConnectionCommand = new RelayCommand(async () => await TestConnectionAsync());
    }
    
    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ConnectionState));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsSynchronized));
        OnPropertyChanged(nameof(ConnectionStatusColor));
        OnPropertyChanged(nameof(ConnectionStatusText));
    }
    
    private void OnSyncQualityChanged(object? sender, SyncQualityEventArgs e)
    {
        OnPropertyChanged(nameof(SyncQualityPercentage));
        OnPropertyChanged(nameof(SyncQualityText));
        OnPropertyChanged(nameof(SyncQualityColor));
        OnPropertyChanged(nameof(DriftText));
        OnPropertyChanged(nameof(DriftColor));
    }
    
    private void OnPilotSelectionChanged(object? sender, PilotSelectionEventArgs e)
    {
        _selectedPilots.Clear();
        foreach (var pilot in e.Pilots)
        {
            _selectedPilots.Add(new TacviewPilotViewModel(pilot));
        }
        
        // Update pan mode if provided in the message
        if (!string.IsNullOrEmpty(e.PanMode))
        {
            PanMode = char.ToUpper(e.PanMode[0]) + e.PanMode.Substring(1); // Capitalize
        }
        
        OnPropertyChanged(nameof(SelectedPilotCount));
        OnPropertyChanged(nameof(HasSelectedPilots));
        OnPropertyChanged(nameof(PilotHeaderText));
    }
    
    // Color calculations
    private SolidColorBrush GetConnectionStatusColor()
    {
        return ConnectionState switch
        {
            ConnectionState.Disconnected => new SolidColorBrush(Colors.Red),
            ConnectionState.Connecting => new SolidColorBrush(Colors.Yellow),
            ConnectionState.Connected => new SolidColorBrush(Colors.LightGreen),
            ConnectionState.Synchronized => new SolidColorBrush(Colors.Green),
            ConnectionState.Degraded => new SolidColorBrush(Colors.Orange),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }
}
```

### TacviewPilotViewModel

```csharp
public class TacviewPilotViewModel : ViewModelBase
{
    private readonly TacviewPilot _pilot;
    private bool _isSpeaking;
    
    public string PilotId => _pilot.PilotId;
    public string DisplayName => _pilot.PilotName;
    public string UnitType => _pilot.UnitType;
    public string Coalition => _pilot.Coalition;
    public double[] Frequencies => _pilot.Frequencies;
    public double Pan => _pilot.Pan;
    
    public bool IsSpeaking
    {
        get => _isSpeaking;
        set => SetProperty(ref _isSpeaking, value);
    }
    
    // Display properties
    public string SpeakingIcon => IsSpeaking ? "??" : "??";
    public FontWeight SpeakingWeight => IsSpeaking ? FontWeights.Bold : FontWeights.Normal;
    public SolidColorBrush IsSpeakingBrush => IsSpeaking 
        ? new SolidColorBrush(Color.FromArgb(40, 0, 255, 0)) 
        : new SolidColorBrush(Colors.Transparent);
    
    public string FrequenciesText => string.Join(", ", Frequencies.Select(f => $"{f:F1} MHz"));
    
    public string CoalitionName => Coalition switch
    {
        "red" => "Red Coalition",
        "blue" => "Blue Coalition",
        _ => "Neutral"
    };
    
    public SolidColorBrush CoalitionColor => Coalition switch
    {
        "red" => new SolidColorBrush(Colors.Red),
        "blue" => new SolidColorBrush(Colors.Blue),
        _ => new SolidColorBrush(Colors.Gray)
    };
    
    public string PanIndicator
    {
        get
        {
            if (Pan <= -0.8) return "???";
            if (Pan <= -0.5) return "??";
            if (Pan <= -0.2) return "?";
            if (Pan <= 0.2) return "??";
            if (Pan <= 0.5) return "?";
            if (Pan <= 0.8) return "??";
            return "???";
        }
    }
    
    public string PanTooltip => $"Spatial Audio: {Pan:+0.0;-0.0} ({GetPanDescription()})\n\nConfigure in Tacview menu:\nAeroDebrief Sync ? Configure Audio Pan";
    
    private string GetPanDescription()
    {
        if (Pan <= -0.8) return "Full Left Ear";
        if (Pan <= -0.5) return "Mostly Left";
        if (Pan <= -0.2) return "Slightly Left";
        if (Pan <= 0.2) return "Center";
        if (Pan <= 0.5) return "Slightly Right";
        if (Pan <= 0.8) return "Mostly Right";
        return "Full Right Ear";
    }
}
```

---

## Integration into UnifiedPlayerControl

**Add Tacview Status Panel**:
```xaml
<UserControl x:Class="AeroDebrief.UI.Controls.UnifiedPlayerControl">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <!-- Header -->
            <RowDefinition Height="Auto"/> <!-- Tacview Status -->
            <RowDefinition Height="*"/>    <!-- Waveform -->
            <RowDefinition Height="Auto"/> <!-- Transport -->
            <RowDefinition Height="*"/>    <!-- Mixer -->
        </Grid.RowDefinitions>
        
        <!-- Existing Header -->
        <player:PlayerHeaderControl Grid.Row="0" .../>
        
        <!-- NEW: Tacview Status Panel -->
        <tacview:TacviewStatusControl Grid.Row="1"
                                      DataContext="{Binding TacviewIntegration}"
                                      Visibility="{Binding TacviewIntegration.IsEnabled, 
                                                   Converter={StaticResource BoolToVisibilityConverter}}"/>
        
        <!-- Existing Waveform, Transport, Mixer -->
        ...
    </Grid>
</UserControl>
```

---

## Animations & Visual Effects

### Connection Status Pulse (Connecting)
```xaml
<Ellipse Width="16" Height="16" Fill="Yellow">
    <Ellipse.Triggers>
        <EventTrigger RoutedEvent="Loaded">
            <BeginStoryboard>
                <Storyboard RepeatBehavior="Forever">
                    <DoubleAnimation Storyboard.TargetProperty="Opacity"
                                     From="1.0" To="0.3" Duration="0:0:0.8"
                                     AutoReverse="True"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Ellipse.Triggers>
</Ellipse>
```

### Speaking Pilot Flash
```xaml
<Border x:Name="PilotBorder">
    <Border.Triggers>
        <DataTrigger Binding="{Binding IsSpeaking}" Value="True">
            <DataTrigger.EnterActions>
                <BeginStoryboard>
                    <Storyboard>
                        <ColorAnimation Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                                        To="#28FF00" Duration="0:0:0.2"/>
                    </Storyboard>
                </BeginStoryboard>
            </DataTrigger.EnterActions>
            <DataTrigger.ExitActions>
                <BeginStoryboard>
                    <Storyboard>
                        <ColorAnimation Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                                        To="Transparent" Duration="0:0:0.3"/>
                    </Storyboard>
                </BeginStoryboard>
            </DataTrigger.ExitActions>
        </DataTrigger>
    </Border.Triggers>
</Border>
```

---

## Accessibility

### Screen Reader Support
- All icons have `AutomationProperties.Name` set
- Connection status announced: "Tacview connected and synchronized"
- Pilot list announced: "2 pilots selected"
- Drift announced: "Synchronization drift 120 milliseconds ahead"

### Keyboard Navigation
- Tab through status, pilots, settings
- Enter/Space to toggle connection
- Alt+T to open Tacview panel

### High Contrast Mode
- Ensure all colors have fallback text indicators
- Status uses both color and icon
- Sync quality shows percentage text

---

## Tooltips

```csharp
// Connection Status
"Tacview Sync: Connected and synchronized"
"Click to view detailed sync information"

// Sync Quality Bar
"Sync Quality: 85% (Good)\nAverage drift: 120ms\nMessage success rate: 98%"

// Drift
"Time drift: +120ms\nAeroDebrief is ahead of Tacview"

// Pilot Pan
"Spatial Audio: -0.8 (Full Left Ear)\nThis pilot's audio plays predominantly in the left channel"
```

---

## Responsive Layout

### Collapsed State (Narrow Window)
```
?????????????????????????
? ?? Tacview: ? 2 pilots?
?????????????????????????
```

### Expanded State (Normal Window)
```
Full status display as shown above
```

### Detachable Window (Optional)
Allow users to pop out Tacview status into separate window for dual-monitor setups.

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-XX  
**Status**: ?? Planning
