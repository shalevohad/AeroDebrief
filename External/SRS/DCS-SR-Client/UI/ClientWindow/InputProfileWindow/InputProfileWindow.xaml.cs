﻿using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using MahApps.Metro.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.InputProfileWindow;

/// <summary>
///     Interaction logic for InputProfileWindow.xaml
/// </summary>
public partial class InputProfileWindow : MetroWindow
{
    public delegate void CreateProfileCallback(string profileName);

    private readonly CreateProfileCallback _callback;


    public InputProfileWindow(CreateProfileCallback callback, bool rename = false, string initialText = "")
    {
        InitializeComponent();
        _callback = callback;
        if (rename)
        {
            ProfileName.Text = initialText;
            CreateRename.Content = "Rename";
        }
    }

    private static string CleanString(string str)
    {
        var regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
        str = r.Replace(str, "").Replace(".cfg", "");

        if (str.Equals("default")) return str + " 1";

        return str.Trim();
    }

    private void CreateOrRename_Click(object sender, RoutedEventArgs e)
    {
        _callback(CleanString(ProfileName.Text));
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}