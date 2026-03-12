using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;
using ToyBoxx.Services;
using ToyBoxx.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace ToyBoxx;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = new HostBuilder()
        .ConfigureAppConfiguration(c =>
        {
            c.SetBasePath(AppContext.BaseDirectory);
            c.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            c.AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: false);
        })
        .ConfigureLogging(logging => logging.ClearProviders())
        .ConfigureServices((context, services) =>
        {
            services.AddHostedService<ApplicationHostService>();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<RootViewModel>();
            services.AddSingleton<IMediaElementProvider, MediaElementProvider>();
            services.AddSingleton<ISnackbarService, SnackbarService>();
        })
        .Build();

    public static void ShowSnackbar(
        string title,
        string message,
        ControlAppearance appearance = ControlAppearance.Secondary,
        SymbolRegular icon = SymbolRegular.Info12,
        Action? onClick = null)
    {
        var snackbarService = _host.Services.GetRequiredService<ISnackbarService>();
        snackbarService.Show(title, message, appearance, new SymbolIcon(icon), TimeSpan.FromSeconds(3));

        if (onClick is not null)
        {
            var presenter = snackbarService.GetSnackbarPresenter();
            if (presenter is SnackbarPresenter { Content: Snackbar snackbar })
            {
                snackbar.IsCloseButtonEnabled = false;
                snackbar.HorizontalAlignment = HorizontalAlignment.Right;
                snackbar.Width = 500;
                snackbar.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    onClick();
                    e.Handled = true;
                };
            }
        }
    }

    public static T GetRequiredService<T>()
        where T : class
    {
        return _host.Services.GetRequiredService<T>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();
    }
}