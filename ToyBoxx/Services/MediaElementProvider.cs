using Microsoft.Extensions.DependencyInjection;
using Unosquare.FFME;

namespace ToyBoxx.Services;

public interface IMediaElementProvider
{
    MediaElement GetMediaElement(string key);
}

public class MediaElementProvider(IServiceProvider serviceProvider) : IMediaElementProvider
{
    public MediaElement GetMediaElement(string key)
    {
        return key switch
        {
            "main" => serviceProvider.GetRequiredService<MainWindow>().Media,
            "preview" => serviceProvider.GetRequiredService<MainWindow>().ControllerPanel.PreviewMedia,
            _ => throw new ArgumentException($"Invalid media key: '{key}'")
        };
    }
}
