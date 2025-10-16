using System.Windows;

namespace AeroDebrief.UI{
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