using System.Windows;

namespace ShalevOhad.DCS.SRS.Recorder.PlayerClient.UI
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}