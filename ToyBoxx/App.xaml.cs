using Microsoft.Extensions.Configuration;
using System.Windows;
using ToyBoxx.ViewModels;
using Unosquare.FFME;

namespace ToyBoxx;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static IConfiguration? _configuration;
    public static IConfiguration Configuration => _configuration ?? throw new InvalidOperationException("Configuration not initialized.");

    public static RootViewModel ViewModel => Current.Resources[nameof(ViewModel)] as RootViewModel ?? throw new Exception("ViewModel not found.");

    public App()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        _configuration = builder.Build();

        var ffmpegPath = _configuration["FFMpegRootPath"] ?? throw new InvalidOperationException("Variable 'FFMpegRootPath' does not exist");
        Library.FFmpegDirectory = ffmpegPath;
        Library.EnableWpfMultiThreadedVideo = true;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Current.MainWindow = new MainWindow
        {
            Top = ToyBoxx.Properties.Settings.Default.WindowTop,
            Left = ToyBoxx.Properties.Settings.Default.WindowLeft,
            Width = ToyBoxx.Properties.Settings.Default.WindowWidth,
            Height = ToyBoxx.Properties.Settings.Default.WindowHeight
        };

        Current.MainWindow.Loaded += (sender, arg) => ViewModel.OnApplicationLoaded();
        Current.MainWindow.Closing += (sender, args) =>
        {
            var window = sender as MainWindow;
            if (window is not null)
            {
                ToyBoxx.Properties.Settings.Default.WindowTop = window.RestoreBounds.Top;
                ToyBoxx.Properties.Settings.Default.WindowLeft = window.RestoreBounds.Left;
                ToyBoxx.Properties.Settings.Default.WindowWidth = window.RestoreBounds.Width;
                ToyBoxx.Properties.Settings.Default.WindowHeight = window.RestoreBounds.Height;
                ToyBoxx.Properties.Settings.Default.LoopingBehavior = (int)window.ViewModel.MediaElement.LoopingBehavior;
                ToyBoxx.Properties.Settings.Default.Volume = window.ViewModel.MediaElement.Volume;
                ToyBoxx.Properties.Settings.Default.IsMuted = window.ViewModel.MediaElement.IsMuted;
                ToyBoxx.Properties.Settings.Default.Save();
            }
        };

        Current.MainWindow.Show();
    }
}