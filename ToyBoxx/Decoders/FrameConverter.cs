using FFmpeg.AutoGen;
using ToyBoxx.Extensions;

namespace ToyBoxx.Decoders;

public unsafe class FrameConverter : IDisposable
{
    private AVPixelFormat _srcFormat;
    private (int width, int height) _srcSize;
    private AVPixelFormat _distFormat;
    private (int width, int height) _distSize;

    private SwsContext* _swsContext;

    public void Configure(AVPixelFormat srcFormat, int srcWidth, int srcHeight, AVPixelFormat distFormat, int distWidth, int distHeight)
    {
        _srcFormat = srcFormat;
        _srcSize = (srcWidth, srcHeight);
        _distFormat = distFormat;
        if (_distSize.width == distWidth || _distSize.height == distHeight)
        {
            return;
        }

        _distSize = (distWidth, distHeight);

        ffmpeg.sws_freeContext(_swsContext);
        _swsContext = ffmpeg.sws_getContext(srcWidth, srcHeight, srcFormat, distWidth, distHeight, distFormat, 0, null, null, null);
    }

    public unsafe byte* ConvertFrame(ManagedFrame frame)
    {
        return ConvertFrame(frame.Frame);
    }

    public unsafe void ConvertFrameDirect(ManagedFrame frame, IntPtr buffer)
    {
        ConvertFrameDirect(frame.Frame, (byte*)buffer.ToPointer());
    }

    public unsafe void ConvertFrameDirect(ManagedFrame frame, byte* buffer)
    {
        ConvertFrameDirect(frame.Frame, buffer);
    }

    private unsafe byte* ConvertFrame(AVFrame* frame)
    {
        byte_ptrArray4 data = default;
        int_array4 lineSize = default;
        var buffer = (byte*)ffmpeg.av_malloc(
            (ulong)ffmpeg.av_image_get_buffer_size(_distFormat, _srcSize.width, _srcSize.height, align: 1));

        ffmpeg.av_image_fill_arrays(ref data, ref lineSize, buffer, _distFormat, _srcSize.width, _srcSize.height, align: 1)
            .OnError(() => throw new InvalidOperationException("Failed to allocate a buffer for frame scaling."));
        ffmpeg.sws_scale(_swsContext, frame->data, frame->linesize, 0, _srcSize.height, data, lineSize)
            .OnError(() => throw new InvalidOperationException("Failed to scale a frame."));

        return buffer;
    }

    private unsafe void ConvertFrameDirect(AVFrame* frame, byte* buffer)
    {
        byte_ptrArray4 data = default;
        int_array4 lineSize = default;

        ffmpeg.av_image_fill_arrays(ref data, ref lineSize, buffer, _distFormat, _srcSize.width, _srcSize.height, align: 1)
                .OnError(() => throw new InvalidOperationException("フレームスケーリング用バッファの確保に失敗しました。"));
        ffmpeg.sws_scale(_swsContext, frame->data, frame->linesize, 0, _srcSize.height, data, lineSize)
            .OnError(() => throw new InvalidOperationException("フレームのスケーリングに失敗しました。"));
    }

    #region Dispose

    public void Dispose()
    {
        DisposeUnManaged();
        GC.SuppressFinalize(this);
    }

    ~FrameConverter()
    {
        DisposeUnManaged();
    }

    private bool _isDisposed = false;
    private void DisposeUnManaged()
    {
        if (_isDisposed)
        {
            return;
        }

        ffmpeg.sws_freeContext(_swsContext);
        _isDisposed = true;
    }

    #endregion
}
