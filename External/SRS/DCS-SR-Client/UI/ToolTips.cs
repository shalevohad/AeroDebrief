using System.Windows;
using System.Windows.Controls;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Properties;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI;

public static class ToolTips
{
    public static ToolTip ExternalAWACSMode;
    public static ToolTip ExternalAWACSModeName;
    public static ToolTip ExternalAWACSModePassword;

    public static void Init()
    {
        ExternalAWACSMode = new ToolTip();
        var externalAWACSModeContent = new StackPanel();

        externalAWACSModeContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMButton,
            FontWeight = FontWeights.Bold
        });
        externalAWACSModeContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMButtonL1
        });
        externalAWACSModeContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMButtonL2
        });

        ExternalAWACSMode.Content = externalAWACSModeContent;


        ExternalAWACSModeName = new ToolTip();
        var externalAWACSModeNameContent = new StackPanel();

        externalAWACSModeNameContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMName,
            FontWeight = FontWeights.Bold
        });
        externalAWACSModeNameContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMNameL1
        });

        ExternalAWACSModeName.Content = externalAWACSModeNameContent;


        ExternalAWACSModePassword = new ToolTip();
        var externalAWACSModePasswordContent = new StackPanel();

        externalAWACSModePasswordContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMPassword,
            FontWeight = FontWeights.Bold
        });
        externalAWACSModePasswordContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMPasswordL1
        });
        externalAWACSModePasswordContent.Children.Add(new TextBlock
        {
            Text = Resources.ToolTipEAMPasswordL2
        });

        ExternalAWACSModePassword.Content = externalAWACSModePasswordContent;
    }
}