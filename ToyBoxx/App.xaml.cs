using Microsoft.Extensions.Configuration;
using System.Windows;
using ToyBoxx.ViewModels;
using Unosquare.FFME;

namespace ToyBoxx
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static IConfiguration? _configuration;
        public static IConfiguration Configuration => _configuration ?? throw new InvalidOperationException("Configuration not initialized.");

        public App()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            _configuration = builder.Build();

            var ffmpegPath = _configuration["FFMpegRootPath"] ?? throw new InvalidOperationException("Variable 'FFMpegRootPath' does not exist");
            Unosquare.FFME.Library.FFmpegDirectory = ffmpegPath;
            Library.EnableWpfMultiThreadedVideo = false;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Current.MainWindow = new MainWindow();
            Current.MainWindow.Loaded += (snd, eva) => ViewModel.OnApplicationLoaded();
            Current.MainWindow.Show();
        }

        public static RootViewModel ViewModel => Current.Resources[nameof(ViewModel)] as RootViewModel ?? throw new Exception("ViewModel not found.");
    }
}
