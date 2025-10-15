﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Octokit;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace AutoUpdater;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public static readonly string GITHUB_USERNAME = "ciribob";

    public static readonly string GITHUB_REPOSITORY = "DCS-SimpleRadioStandalone";

    // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
    public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";
    private bool _cancel;
    private string _directory;
    private string _file;

    private bool _finished;
    private double _lastValue = -1;
    private DispatcherTimer _progressCheckTimer;
    private Uri _uri;

    private string changelogURL = "";

    public MainWindow()
    {
        InitializeComponent();
        QuitSimpleRadio();
        if (IsAnotherRunning())
        {
            MessageBox.Show("Please close DCS-SimpleRadio Standalone before running", "SRS Auto Updater",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(0);

            return;
        }

        try
        {
            DownloadLatestVersion();
        }
        catch (Exception exception)
        {
            ShowError();
        }
    }

    private bool IsDCSRunning()
    {
        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.ProcessName.ToLower().Trim().Equals("dcs"))
                return true;

        return false;
    }

    private void QuitSimpleRadio()
    {
        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.ProcessName.ToLower().Trim().StartsWith("sr-server") ||
                clsProcess.ProcessName.ToLower().Trim().StartsWith("srs-server") ||
                clsProcess.ProcessName.ToLower().Trim().StartsWith("sr-client"))
            {
                clsProcess.Kill();
                clsProcess.WaitForExit(5000);
                clsProcess.Dispose();
            }
    }

    private bool IsAnotherRunning()
    {
        var currentProcess = Process.GetCurrentProcess();
        var currentProcessName = currentProcess.ProcessName.ToLower().Trim();

        foreach (var clsProcess in Process.GetProcesses())
            if (clsProcess.Id != currentProcess.Id &&
                clsProcess.ProcessName.ToLower().Trim() == currentProcessName)
                return true;

        return false;
    }

    private Release FindRightRelease(IReadOnlyList<Release> releases)
    {
        var allowBeta = AllowBeta();
        var allowAlpha = AllowAlpha();

        Release latestAlpha = null;
        Release latestBeta = null;
        Release latestStable = null;
        
        foreach (var release in releases)
        {
            if (release.Prerelease)
            {
                if (release.Name.ToLower().Contains("alpha") && latestAlpha == null)
                {
                    latestAlpha = release;
                }

                if (release.Name.ToLower().Contains("beta") && latestBeta == null)
                {
                    latestBeta = release;
                }
            }
            else
            {
                if (latestStable == null)
                {
                    latestStable = release;
                }
            }

            if (latestAlpha != null && latestBeta != null && latestStable != null)
            {
                break;
            }
        }

        var lastStableVersion = new Version(latestStable?.TagName.Replace("v", "") ?? "0.0.0.0");
        var lastBetaVersion = new Version(latestBeta?.TagName.Replace("v", "") ?? "0.0.0.0");
        var lastAlphaVersion = new Version(latestAlpha?.TagName.Replace("v", "") ?? "0.0.0.0");
        
        if (allowAlpha && latestAlpha != null 
                       && lastAlphaVersion > lastBetaVersion 
            && lastAlphaVersion > lastStableVersion)
        {
            return latestAlpha;
        }
        //allowing alpha gives you beta if its newer
        if ((allowBeta || allowAlpha) && latestBeta != null && lastBetaVersion > lastStableVersion)
        {
            return latestBeta;
        }
            
        return latestStable;
    }
    
    private async Task<Uri> GetPathToLatestVersion()
    {
        Status.Content = "Finding Latest SRS Version";
        var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, "1.0.0.0"));

        var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);

        var release = FindRightRelease(releases);
        var releaseAsset = release.Assets.First();

        foreach (var asset in release.Assets)
            if (asset.Name.ToLower().StartsWith("dcs-simpleradiostandalone") &&
                asset.Name.ToLower().Contains(".zip"))
            {
                changelogURL = release.HtmlUrl;
                Status.Content = "Downloading Version " + release.TagName;

                if (ServerInstall())
                {
                    //check the path and version
                    var path = ServerPath();

                    if (path.Length > 0)
                    {
                        var latestVersion = new Version(release.TagName.Replace("v", ""));
                        var serverExe = Path.Combine(path,"Server", "SRS-Server.exe");
                        var useThisVersion = true;
                        if (Path.Exists(serverExe))
                        {
                            var serverVersion = new Version(FileVersionInfo.GetVersionInfo(serverExe).FileVersion);
                            useThisVersion = serverVersion < latestVersion;
                        }
                        

                        if (useThisVersion) return new Uri(releaseAsset.BrowserDownloadUrl);

                        //no update
                        return null;
                    }
                }

                return new Uri(releaseAsset.BrowserDownloadUrl);
            }
        
        return null;
    }

    private bool AllowBeta()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-beta"))
                return true;

        return false;
    }

    private bool AllowAlpha()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-alpha"))
                return true;

        return false;
    }

    private string ServerPath()
    {
        foreach (var commandLineArg in Environment.GetCommandLineArgs())
            if (commandLineArg.Trim().StartsWith("-path="))
            {
                var line = commandLineArg.Trim();
                line = line.Replace("-path=", "");

                return line;
            }

        return "";
    }

    private bool ServerInstall()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-server"))
                return true;

        return false;
    }

    private bool WaitInstaller()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-wait"))
                return true;

        return false;
    }

    public void ShowError()
    {
        MessageBox.Show(
            "Error Auto Updating SRS - Please check internet connection and try again \n\nAlternatively: \n1. Download the latest DCS-SimpleRadioStandalone.zip from the SRS Github Release page\n2. Extract all the files to a temporary directory\n3. Run the installer.",
            "Auto Updater Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Close();
    }

    public async void DownloadLatestVersion()
    {
        try
        {
            _uri = await GetPathToLatestVersion();

            if (_uri == null) Environment.Exit(0);


            _directory = GetTemporaryDirectory();
            _file = _directory + "\\temp.zip";

            using (WebClient wc = new MyWebClient())
            {
                wc.Headers.Add("user-agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)");
                wc.DownloadProgressChanged += DownloadProgressChanged;
                wc.DownloadFileAsync(_uri, _file);
                wc.DownloadFileCompleted += DownloadComplete;

                //check download progress periodically - if the download is stalled we dont get told by anything
                _progressCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                _progressCheckTimer.Tick += CheckProgress;
                _progressCheckTimer.Start();
            }
        }
        catch (Exception exception)
        {
            ShowError();
        }
    }

    private void CheckProgress(object sender, EventArgs e)
    {
        if (_lastValue == DownloadProgress.Value && _finished == false)
            //no progress
            ShowError();

        _lastValue = DownloadProgress.Value;
    }

    private bool ShouldRestart()
    {
        foreach (var arg in Environment.GetCommandLineArgs())
            if (arg.Trim().Equals("-restart"))
                return true;

        return false;
    }

    private void DownloadComplete(object sender, AsyncCompletedEventArgs e)
    {
        _finished = true;
        if (!_cancel)
        {
            ZipFile.ExtractToDirectory(_file, Path.Combine(_directory, "extract"));

            Thread.Sleep(400);

            if (!ServerInstall())
            {
                while (IsDCSRunning())
                    MessageBox.Show(
                        "Please Close DCS \n\nSRS cannot be installed - please close DCS before hitting OK \n\n",
                        "Close DCS",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                var releaseNotes = MessageBox.Show(
                    "Do you want to read the release notes? \n\nHighly recommended before installing! \n\n",
                    "Read Release Notes?",
                    MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (releaseNotes == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(changelogURL)
                        { UseShellExecute = true });
                }
            }

            var procInfo = new ProcessStartInfo();
            procInfo.WorkingDirectory = Path.Combine(_directory, "extract");
            if (ServerInstall())
            {
                procInfo.Arguments = "-autoupdate";
                procInfo.Arguments += " -server ";
                procInfo.Arguments += " -path=\"" + ServerPath() + "\"";

                if (ShouldRestart()) procInfo.Arguments += " -restart ";
            }
            else
            {
                procInfo.Arguments = "-autoupdate";
            }

            procInfo.FileName = Path.Combine(Path.Combine(_directory, "extract"), "installer.exe");
            procInfo.UseShellExecute = false;
            var installerProcess = Process.Start(procInfo);

            if (WaitInstaller() && installerProcess != null)
            {
                installerProcess.WaitForExit();
            }
            


            //Process.Start(changelogURL);
        }

        Close();
    }

    public string GetTemporaryDirectory()
    {
        var tempFolder = Path.GetTempFileName();
        File.Delete(tempFolder);
        Directory.CreateDirectory(tempFolder);

        return tempFolder;
    }

    private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        DownloadProgress.Value = e.ProgressPercentage;
    }

    private void CancelButtonClick(object sender, RoutedEventArgs e)
    {
        _cancel = true;
        Close();
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        _cancel = true;
        _progressCheckTimer?.Stop();
    }
}
