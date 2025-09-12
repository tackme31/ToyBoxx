using FFmpeg.AutoGen;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ToyBoxx.Decoders;

namespace ToyBoxx.Controllers;

internal class VideoPlayController
{
    private static readonly AVPixelFormat _ffPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
    private static readonly PixelFormat _wpfPixelFormat = PixelFormats.Bgr24;

    private VideoDecoder? _decoder;
    private ImageWriter? _imageWriter;
    private FrameConverter? _frameConverter;

    public void OpenFile(string path)
    {
        _decoder = new VideoDecoder();
        _decoder.OpenFile(path);
    }

    public WriteableBitmap CreateBitmap(int dpiX, int dpiY)
    {
        if (_decoder is null)
        {
            throw new InvalidOperationException("A file must be opened before playback.");
        }

        var context = _decoder.VideoCodecContext;
        var width = context.width;
        var height = context.height;
        var writeableBitmap = new WriteableBitmap(width, height, dpiX, dpiY, _wpfPixelFormat, null);

        _imageWriter = new ImageWriter(width, height, writeableBitmap);
        _frameConverter = new FrameConverter();
        _frameConverter.Configure(context.pix_fmt, context.width, context.height, _ffPixelFormat, width, height);

        return writeableBitmap;
    }
}
