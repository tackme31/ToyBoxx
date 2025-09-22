using System.Drawing;

namespace ToyBoxx.Foundation;

public class VideoThumbnailProvider(int thumbnailWidth, int thumbnailHeight) : IDisposable
{
    private readonly Lock _captureLock = new();
    private bool _isCapturing;

    private VideoCaptureContext? _context;

    public void Open(string mediaPath)
    {
        if (_context is null)
        {
            _context?.Dispose();
        }

        _context = new(mediaPath, CalculateThumbnailSize);
    }

    public void Close()
    {
        _context?.Dispose();
    }

    public Task<Bitmap?> CaptureAsync(TimeSpan position)
    {
        if (_context is null)
        {
            throw new InvalidOperationException("Media isn't open.");
        }

        lock (_captureLock)
        {
            if (_isCapturing)
            {
                return Task.FromResult<Bitmap?>(null);
            }

            _isCapturing = true;
        }

        return Task.Run(() =>
        {
            try
            {
                unsafe
                {
                    return _context.CaptureFrameAsBitmap(position);
                }
            }
            finally
            {
                lock (_captureLock)
                {
                    _isCapturing = false;
                }
            }
        });
    }

    private (int, int) CalculateThumbnailSize((int width, int height) videoSize)
    {
        var scaleWidth = thumbnailWidth / videoSize.width;
        var scaleHeight = thumbnailHeight / videoSize.height;
        var scale = Math.Min(scaleWidth, scaleHeight);

        return (videoSize.width * scale, videoSize.height * scale);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
