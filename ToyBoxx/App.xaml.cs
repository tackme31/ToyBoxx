using FFmpeg.AutoGen;
using Microsoft.Extensions.Configuration;
using System.Windows;

namespace ToyBoxx
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static IConfiguration? _configuration;
        public static IConfiguration Configuration => _configuration ?? throw new InvalidOperationException("Configuration not initialized.");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            _configuration = builder.Build();

            var rootPath = Configuration["FFMpegRootPath"] ?? throw new Exception("'FFMpegRootPath' does not exist.");
            ffmpeg.RootPath = rootPath;
            ffmpeg.avdevice_register_all();
        }
    }

}
