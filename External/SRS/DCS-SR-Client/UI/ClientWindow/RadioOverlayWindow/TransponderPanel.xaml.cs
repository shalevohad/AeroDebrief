﻿using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;
using UserControl = System.Windows.Controls.UserControl;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.RadioOverlayWindow;

/// <summary>
///     Interaction logic for TransponderPanel.xaml
/// </summary>
public partial class TransponderPanel : UserControl
{
    private readonly SolidColorBrush _buttonOn = new((Color)ColorConverter.ConvertFromString("#00FF00"));
    private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;


    public TransponderPanel()
    {
        InitializeComponent();

        Mode1.MaxLines = 1;
        Mode1.MaxLength = 2;

        Mode1.LostFocus += Mode1OnLostFocus;
        Mode1.KeyDown += ModeOnKeyDown;
        Mode1.GotFocus += ModeOnGotFocus;

        Mode3.MaxLines = 1;
        Mode3.MaxLength = 4;
        Mode3.LostFocus += Mode3OnLostFocus;
        Mode3.KeyDown += ModeOnKeyDown;
        Mode3.GotFocus += ModeOnGotFocus;
    }


    public void RepaintTransponderStatus()
    {
        var dcsPlayerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

        if (dcsPlayerRadioInfo == null || !dcsPlayerRadioInfo.IsCurrent() || dcsPlayerRadioInfo.iff == null ||
            dcsPlayerRadioInfo.iff.control == Common.Models.Player.Transponder.IFFControlMode.DISABLED)
        {
            Mode1.IsEnabled = false;
            Mode1.Text = "--";

            Mode3.IsEnabled = false;
            Mode3.Text = "--";

            Mode4Button.IsEnabled = false;
            Mode4Button.Foreground = new SolidColorBrush(Colors.Black);

            Ident.IsEnabled = false;
            Ident.Foreground = new SolidColorBrush(Colors.Black);

            TransponderActive.Fill = new SolidColorBrush(Colors.Red);
        }
        else
        {
            var iff = dcsPlayerRadioInfo.iff;

            if (iff.control != Common.Models.Player.Transponder.IFFControlMode.OVERLAY)
            {
                Mode1.IsEnabled = false;
                Mode3.IsEnabled = false;
                Mode4Button.IsEnabled = false;
                Ident.IsEnabled = false;
            }
            else
            {
                Mode1.IsEnabled = true;
                Mode3.IsEnabled = true;
                Mode4Button.IsEnabled = true;
                Ident.IsEnabled = true;
            }

            if (iff.status == Common.Models.Player.Transponder.IFFStatus.OFF)
            {
                Mode1.Text = "--";
                Mode3.Text = "--";
                Mode4Button.Foreground = new SolidColorBrush(Colors.Black);
                Mode4Button.IsEnabled = false;

                Ident.Foreground = new SolidColorBrush(Colors.Black);
                Ident.IsEnabled = false;

                TransponderActive.Fill = new SolidColorBrush(Colors.Red);

                Mode1.IsEnabled = false;
                Mode3.IsEnabled = false;
                Mode4Button.IsEnabled = false;
                Ident.IsEnabled = false;
            }
            else
            {
                TransponderActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));

                if (!Mode1.IsFocused)
                {
                    if (iff.mode1 != -1)
                        Mode1.Text = iff.mode1.ToString("D2");
                    else
                        Mode1.Text = "--";
                }

                if (!Mode3.IsFocused)
                {
                    if (iff.mode3 != -1)
                        Mode3.Text = iff.mode3.ToString("D4");
                    else
                        Mode3.Text = "--";
                }

                if (iff.mode4)
                    Mode4Button.Foreground = _buttonOn;
                else
                    Mode4Button.Foreground = new SolidColorBrush(Colors.Black);

                if (iff.mode2 > -1)
                {
                    Mode2Button.Foreground = _buttonOn;
                    if (Mode2Button.Visibility != Visibility.Visible) Mode2Button.Visibility = Visibility.Visible;
                }
                else
                {
                    if (Mode2Button.Visibility != Visibility.Collapsed) Mode2Button.Visibility = Visibility.Collapsed;
                }

                if (iff.status == Common.Models.Player.Transponder.IFFStatus.IDENT)
                    Ident.Foreground = _buttonOn;
                else
                    Ident.Foreground = new SolidColorBrush(Colors.Black);
            }
        }
    }

    private void TransponderPowerClick(object sender, MouseButtonEventArgs e)
    {
        if (!CanInteract()) return;

        if (TransponderHelper.TogglePower()) RepaintTransponderStatus();
    }

    private void ModeOnGotFocus(object sender, RoutedEventArgs routedEventArgs)
    {
        if (!CanInteract())
        {
            // Kill logical focus
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(TransponderActive), null);
            // Kill keyboard focus
            Keyboard.ClearFocus();
        }
    }

    private
        void ModeOnKeyDown(object sender, KeyEventArgs keyEventArgs)
    {
        if (keyEventArgs.Key == Key.Enter || keyEventArgs.Key == Key.Tab)
        {
            // Kill logical focus
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(TransponderActive), null);
            // Kill keyboard focus
            Keyboard.ClearFocus();

            if (sender.Equals(Mode3))
                Mode3OnLostFocus(null, null);
            else
                Mode1OnLostFocus(null, null);
        }
    }

    private void Mode3OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (!CanInteract()) return;

        var mode3 = 0;
        if (int.TryParse(Mode3.Text.Replace(',', '.').Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out mode3))
        {
            TransponderHelper.SetMode3(mode3);
        }
        else
        {
            Mode1.Text = "--";
            TransponderHelper.SetMode3(-1);
        }
    }

    private void Mode1OnLostFocus(object sender, RoutedEventArgs routedEventArgs)
    {
        if (!CanInteract()) return;

        var mode1 = 0;
        if (int.TryParse(Mode1.Text.Replace(',', '.').Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out mode1))
        {
            TransponderHelper.SetMode1(mode1);
        }
        else
        {
            Mode1.Text = "--";
            TransponderHelper.SetMode1(-1);
        }
    }

    private void Mode4ButtonOnClick(object sender, RoutedEventArgs e)
    {
        if (TransponderHelper.Mode4Toggle()) RepaintTransponderStatus();
    }

    private void IdentButtonOnClick(object sender, RoutedEventArgs e)
    {
        if (TransponderHelper.ToggleIdent()) RepaintTransponderStatus();
    }

    private bool CanInteract()
    {
        var trans = TransponderHelper.GetTransponder();

        if (trans == null || trans.control != Common.Models.Player.Transponder.IFFControlMode.OVERLAY) return false;

        return true;
    }
}