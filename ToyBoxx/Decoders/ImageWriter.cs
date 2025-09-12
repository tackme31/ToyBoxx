using System.Windows;
using System.Windows.Media.Imaging;

namespace ToyBoxx.Decoders;

public class ImageWriter(int width, int height, WriteableBitmap writeableBitmap)
{
    private readonly Int32Rect _rect = new(0, 0, width, height);
    private readonly WriteableBitmap _writeableBitmap = writeableBitmap;

    public void WriteFrame(ManagedFrame frame, FrameConverter frameConverter)
    {
        var bitmap = _writeableBitmap;
        bitmap.Lock();

        try
        {
            IntPtr ptr = bitmap.BackBuffer;
            frameConverter.ConvertFrameDirect(frame, ptr);
            bitmap.AddDirtyRect(_rect);
        }
        finally
        {
            bitmap.Unlock();
        }
    }
}