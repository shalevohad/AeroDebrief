﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Network.DCS.Models.DCSState;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientSettingsControl.Model;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Models.Player;
using Ciribob.DCS.SimpleRadio.Standalone.Common.Settings;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow;

/// <summary>
///     Interaction logic for RadioOverlayWindow.xaml
/// </summary>
public partial class RadioOverlayWindow : Window,IHandle<CloseRadioOverlayMessage>
{
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

    private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

    private readonly double _originalMinHeight;

    private readonly double _radioHeight;

    private readonly DispatcherTimer _updateTimer;
    private readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly RadioControlGroup[] radioControlGroup =
        new RadioControlGroup[5];

    private double _aspectRatio;

    private long _lastFocus;

    private long _lastUnitId;
    private RadioCapabilities _radioCapabilitiesWindow;


    public RadioOverlayWindow()
    {
        //load opacity before the intialising as the slider changed
        //method fires after initialisation
        InitializeComponent();

        WindowStartupLocation = WindowStartupLocation.Manual;

        _aspectRatio = MinWidth / MinHeight;


        _originalMinHeight = MinHeight;
        _radioHeight = Radio1.Height;

        AllowsTransparency = true;
        Opacity = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioOpacity).DoubleValue;
        WindowOpacitySlider.Value = Opacity;

        radioControlGroup[0] = Radio1;
        radioControlGroup[1] = Radio2;
        radioControlGroup[2] = Radio3;
        radioControlGroup[3] = Radio4;
        radioControlGroup[4] = Radio5;

        //allows click and drag anywhere on the window
        ContainerPanel.MouseLeftButtonDown += WrapPanel_MouseLeftButtonDown;

        Left = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioX).DoubleValue;
        Top = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioY).DoubleValue;

        Width = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioWidth).DoubleValue;
        Height = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioHeight).DoubleValue;

        //  Window_Loaded(null, null);
        CalculateScale();

        LocationChanged += Location_Changed;

        RadioRefresh(null, null);

        //init radio refresh
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _updateTimer.Tick += RadioRefresh;
        _updateTimer.Start();
    }

    private void Location_Changed(object sender, EventArgs e)
    {
    }

    private void RadioRefresh(object sender, EventArgs eventArgs)
    {
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        foreach (var radio in radioControlGroup)
        {
            radio.RepaintRadioStatus();
            radio.RepaintRadioReceive();
        }

        Intercom.RepaintRadioStatus();
        TransponderPanel.RepaintTransponderStatus();

        if (dcsPlayerRadioInfo != null && dcsPlayerRadioInfo.IsCurrent())
        {
            //reset when we switch planes
            if (_lastUnitId != dcsPlayerRadioInfo.unitId)
            {
                _lastUnitId = dcsPlayerRadioInfo.unitId;
                ResetHeight();
            }

            var availableRadios = 0;

            for (var i = 0; i < dcsPlayerRadioInfo.radios.Length; i++)
                if (dcsPlayerRadioInfo.radios[i].modulation != Modulation.DISABLED)
                    availableRadios++;

            if (availableRadios == 6
                || (dcsPlayerRadioInfo.radios.Length >= 6
                    && dcsPlayerRadioInfo.radios[5].modulation != Modulation.DISABLED))
            {
                Radio5.Visibility = Visibility.Visible;
                Radio4.Visibility = Visibility.Visible;

                if (MinHeight != _originalMinHeight + _radioHeight * 2)
                {
                    MinHeight = _originalMinHeight + _radioHeight * 2;
                    Recalculate();
                }
            }
            else if (availableRadios == 5
                     || (dcsPlayerRadioInfo.radios.Length >= 5
                         && dcsPlayerRadioInfo.radios[4].modulation != Modulation.DISABLED))
            {
                Radio5.Visibility = Visibility.Collapsed;
                Radio4.Visibility = Visibility.Visible;
                if (MinHeight != _originalMinHeight + _radioHeight)
                {
                    MinHeight = _originalMinHeight + _radioHeight;
                    Recalculate();
                }
            }
            else
            {
                ResetHeight();
            }


            if (availableRadios > 1)
            {
                if (dcsPlayerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.HOTAS)
                    ControlText.Text = Properties.Resources.OverlayHotasControls;
                else
                    ControlText.Text = Properties.Resources.OverlayCockpitControls;
            }
            else
            {
                ControlText.Text = "";
            }
        }
        else
        {
            ResetHeight();
            ControlText.Text = "";
        }

        FocusDCS();
    }

    private void ResetHeight()
    {
        Radio4.Visibility = Visibility.Collapsed;
        Radio5.Visibility = Visibility.Collapsed;
        if (MinHeight != _originalMinHeight)
        {
            MinHeight = _originalMinHeight;
            Recalculate();
        }
    }

    private void Recalculate()
    {
        _aspectRatio = MinWidth / MinHeight;
        containerPanel_SizeChanged(null, null);
        Height = Height + 1;
    }

    private void FocusDCS()
    {
        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RefocusDCS))
        {
            var overlayWindow = new WindowInteropHelper(this).Handle;

            //focus DCS if needed
            var foreGround = WindowHelper.GetForegroundWindow();

            var localByName = Process.GetProcessesByName("dcs");

            if (localByName != null && localByName.Length > 0)
            {
                //either DCS is in focus OR Overlay window is not in focus
                if (foreGround == localByName[0].MainWindowHandle || overlayWindow != foreGround ||
                    IsMouseOver)
                    _lastFocus = DateTime.Now.Ticks;
                else if (DateTime.Now.Ticks > _lastFocus + 20000000 && overlayWindow == foreGround)
                    WindowHelper.BringProcessToFront(localByName[0]);
            }
        }
    }

    private void WrapPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, Width);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, Height);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioOpacity, Opacity);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, Left);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, Top);
        base.OnClosing(e);

        _updateTimer.Stop();
    }

    private void Button_Minimise(object sender, RoutedEventArgs e)
    {
        // Minimising a window without a taskbar icon leads to the window's menu bar still showing up in the bottom of screen
        // Since controls are unusable, but a very small portion of the always-on-top window still showing, we're closing it instead, similar to toggling the overlay
        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide))
            Close();
        else
            WindowState = WindowState.Minimized;
    }

    private void Button_About(object sender, RoutedEventArgs e)
    {
        //Show Radio Capabilities
        if (_radioCapabilitiesWindow == null || !_radioCapabilitiesWindow.IsVisible ||
            _radioCapabilitiesWindow.WindowState == WindowState.Minimized)
        {
            _radioCapabilitiesWindow?.Close();

            _radioCapabilitiesWindow = new RadioCapabilities();
            _radioCapabilitiesWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            _radioCapabilitiesWindow.Owner = this;
            _radioCapabilitiesWindow.ShowDialog();
        }
        else
        {
            _radioCapabilitiesWindow?.Close();
            _radioCapabilitiesWindow = null;
        }
    }


    private void Button_Close(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void windowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
    }

    private void containerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        //force aspect ratio
        CalculateScale();

        WindowState = WindowState.Normal;
    }


    private void CalculateScale()
    {
        var yScale = ActualHeight / RadioOverlayWin.MinWidth;
        var xScale = ActualWidth / RadioOverlayWin.MinWidth;
        var value = Math.Min(xScale, yScale);
        ScaleValue = (double)OnCoerceScaleValue(RadioOverlayWin, value);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        if (sizeInfo.WidthChanged)
            Width = sizeInfo.NewSize.Height * _aspectRatio;
        else
            Height = sizeInfo.NewSize.Width / _aspectRatio;


        // Console.WriteLine(this.Height +" width:"+ this.Width);
    }

    private void RadioOverlayWindow_OnLocationChanged(object sender, EventArgs e)
    {
        //reset last focus so we dont switch back to dcs while dragging
        _lastFocus = DateTime.Now.Ticks;
    }

    #region ScaleValue Depdency Property //StackOverflow: http://stackoverflow.com/questions/3193339/tips-on-developing-resolution-independent-application/5000120#5000120

    public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
        typeof(double), typeof(RadioOverlayWindow),
        new UIPropertyMetadata(1.0, OnScaleValueChanged,
            OnCoerceScaleValue));

    private static object OnCoerceScaleValue(DependencyObject o, object value)
    {
        var mainWindow = o as RadioOverlayWindow;
        if (mainWindow != null)
            return mainWindow.OnCoerceScaleValue((double)value);
        return value;
    }

    private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        var mainWindow = o as RadioOverlayWindow;
        if (mainWindow != null)
            mainWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
    }

    protected virtual double OnCoerceScaleValue(double value)
    {
        if (double.IsNaN(value))
            return 1.0f;

        value = Math.Max(0.1, value);
        return value;
    }

    protected virtual void OnScaleValueChanged(double oldValue, double newValue)
    {
    }

    public double ScaleValue
    {
        get => (double)GetValue(ScaleValueProperty);
        set => SetValue(ScaleValueProperty, value);
    }

    #endregion

    public Task HandleAsync(CloseRadioOverlayMessage message, CancellationToken cancellationToken)
    {
        Close();
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, 300);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, 122);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, 270);
        _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioOpacity, 1.0);
        
        return Task.CompletedTask;
        
    }
}