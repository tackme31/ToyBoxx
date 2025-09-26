using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToyBoxx.ViewModels;
using Unosquare.FFME;
using Wpf.Ui.Appearance;

namespace ToyBoxx.Services;

internal class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ApplicationHostService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Build config
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        var config = builder.Build();

        // Set application theme
        var theme = config["ApplicationTheme"] switch
        {
            "Dark" => ApplicationTheme.Dark,
            "Light" => ApplicationTheme.Light,
            "HighContrast" => ApplicationTheme.HighContrast,
            _ => ApplicationTheme.Dark
        };
        ApplicationThemeManager.Apply(theme);

        // Initialize FFME
        var ffmpegPath = config["FFMpegRootPath"] ?? throw new InvalidOperationException("Variable 'FFMpegRootPath' does not exist");
        Library.FFmpegDirectory = ffmpegPath;
        Library.EnableWpfMultiThreadedVideo = true;

        // Setup main window
        var window = _serviceProvider.GetRequiredService<MainWindow>();

        window.Top = Properties.Settings.Default.WindowTop;
        window.Left = Properties.Settings.Default.WindowLeft;
        window.Width = Properties.Settings.Default.WindowWidth;
        window.Height = Properties.Settings.Default.WindowHeight;

        var viewModel = _serviceProvider.GetRequiredService<RootViewModel>();
        viewModel.RequestToggleFullScreen += window.ToggleFullScreen;
        window.DataContext = viewModel;
        window.Loaded += (sender, arg) => viewModel.OnApplicationLoaded();
        window.Closing += (sender, args) =>
        {
            Properties.Settings.Default.WindowTop = window.RestoreBounds.Top;
            Properties.Settings.Default.WindowLeft = window.RestoreBounds.Left;
            Properties.Settings.Default.WindowWidth = window.RestoreBounds.Width;
            Properties.Settings.Default.WindowHeight = window.RestoreBounds.Height;
            Properties.Settings.Default.LoopingBehavior = (int)viewModel.MediaElement.LoopingBehavior;
            Properties.Settings.Default.Volume = viewModel.MediaElement.Volume;
            Properties.Settings.Default.IsMuted = viewModel.MediaElement.IsMuted;
            Properties.Settings.Default.Save();
        };

        window.Show();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
