using FFmpeg.AutoGen;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ToyBoxx.Foundation;

public sealed unsafe class VideoCaptureContext : IDisposable
{
    private AVFormatContext* _formatContext;
    private AVCodecContext* _codecContext;
    private AVStream* _videoStream;
    private int _videoStreamIndex = -1;
    private SwsContext* _swsContext;
    private AVFrame* _sourceFrame;
    private AVFrame* _rgbFrame;
    private byte* _frameBuffer;
    private bool _disposed;

    private Func<(int width, int height), (int width, int height)>? _calcCaptureSize;

    private (int width, int height)? _captureSize;
    private (int width, int height) CaptureSize
    {
        get
        {
            if (_captureSize is not null)
            {
                return _captureSize.Value;
            }

            var size = _calcCaptureSize?.Invoke((_codecContext->width, _codecContext->height)) ?? (_codecContext->width, _codecContext->height);
            if (size.width <= 0 || size.height <= 0)
            {
                throw new InvalidOperationException($"Capture size must be positive: ({size.width}, {size.height})");
            }

            _captureSize = size;
            return size;
        }
    }

    public VideoCaptureContext(string filePath, Func<(int width, int height), (int width, int height)>? calcCaptureSize)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Video file not found: {filePath}");
        }

        _calcCaptureSize = calcCaptureSize;

        Initialize(filePath);
    }

    private void Initialize(string filePath)
    {
        try
        {
            InitializeFormatContext(filePath);
            FindVideoStream();
            InitializeCodecContext();
            InitializeFrames();
            InitializeSwsContext();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void InitializeFormatContext(string filePath)
    {
        AVFormatContext* formatContext = ffmpeg.avformat_alloc_context();

        if (ffmpeg.avformat_open_input(&formatContext, filePath, null, null) != 0)
        {
            throw new InvalidOperationException("Could not open video file.");
        }

        if (ffmpeg.avformat_find_stream_info(formatContext, null) != 0)
        {
            ffmpeg.avformat_close_input(&formatContext);
            throw new InvalidOperationException("Could not find stream information.");
        }

        _formatContext = formatContext;
    }

    private void FindVideoStream()
    {
        for (var i = 0; i < _formatContext->nb_streams; i++)
        {
            if (_formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                _videoStreamIndex = i;
                _videoStream = _formatContext->streams[i];

                return;
            }
        }

        if (_videoStream == null)
        {
            throw new InvalidOperationException("No video stream found in the file.");
        }
    }

    private void InitializeCodecContext()
    {
        var codec = ffmpeg.avcodec_find_decoder(_videoStream->codecpar->codec_id);
        if (codec == null)
        {
            throw new InvalidOperationException("Codec not found for video stream.");
        }

        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        if (_codecContext == null)
        {
            throw new InvalidOperationException("Could not allocate codec context.");
        }

        if (ffmpeg.avcodec_parameters_to_context(_codecContext, _videoStream->codecpar) < 0)
        {
            throw new InvalidOperationException("Could not copy codec parameters to context.");
        }

        if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
        {
            throw new InvalidOperationException("Could not open codec.");
        }
    }

    private void InitializeFrames()
    {
        _sourceFrame = ffmpeg.av_frame_alloc();
        _rgbFrame = ffmpeg.av_frame_alloc();

        if (_sourceFrame == null || _rgbFrame == null)
        {
            throw new InvalidOperationException("Could not allocate frames.");
        }

        var bufferSize = ffmpeg.av_image_get_buffer_size(
            AVPixelFormat.AV_PIX_FMT_BGR24,
            _codecContext->width,
            _codecContext->height,
            1);

        _frameBuffer = (byte*)ffmpeg.av_malloc((ulong)bufferSize);
        if (_frameBuffer == null)
        {
            throw new InvalidOperationException("Could not allocate frame buffer.");
        }

        byte_ptrArray4 frameData = default;
        int_array4 frameLinesize = default;

        ffmpeg.av_image_fill_arrays(
            ref frameData,
            ref frameLinesize,
            _frameBuffer,
            AVPixelFormat.AV_PIX_FMT_BGR24,
            _codecContext->width,
            _codecContext->height,
            1);

        for (uint i = 0; i < 4; i++)
        {
            _rgbFrame->data[i] = frameData[i];
            _rgbFrame->linesize[i] = frameLinesize[i];
        }
    }

    private void InitializeSwsContext()
    {
        _swsContext = ffmpeg.sws_getCachedContext(
            null,
            _codecContext->width,
            _codecContext->height,
            _codecContext->pix_fmt,
            CaptureSize.width,
            CaptureSize.height,
            AVPixelFormat.AV_PIX_FMT_BGR24,
            ffmpeg.SWS_BILINEAR,
            null, null, null);

        if (_swsContext == null)
        {
            throw new InvalidOperationException("Could not initialize scale context.");
        }
    }

    /// <summary>
    /// Capture a frame at specified time.
    /// </summary>
    public Bitmap? CaptureFrameAsBitmap(TimeSpan position)
    {
        ThrowIfDisposed();

        if (position < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Position must be non-negative.");
        }

        SeekToTime(position.TotalSeconds);
        return CaptureCurrentFrame(position.TotalSeconds);
    }

    private void SeekToTime(double timeInSeconds)
    {
        var timestamp = (long)(timeInSeconds * ffmpeg.AV_TIME_BASE);
        if (ffmpeg.av_seek_frame(_formatContext, -1, timestamp, ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
        {
            throw new InvalidOperationException($"Could not seek to time {timeInSeconds} seconds.");
        }

        ffmpeg.avcodec_flush_buffers(_codecContext);
    }

    private Bitmap? CaptureCurrentFrame(double timeInSeconds)
    {
        AVPacket packet = new();

        try
        {
            while (ffmpeg.av_read_frame(_formatContext, &packet) >= 0)
            {
                try
                {
                    if (packet.stream_index != _videoStreamIndex)
                    {
                        continue;
                    }

                    if (!TryDecodeFrame(&packet))
                    {
                        continue;
                    }

                    var frameTime = _sourceFrame->pts * ffmpeg.av_q2d(_videoStream->time_base);
                    if (frameTime < timeInSeconds)
                    {
                        continue;
                    }

                    ffmpeg.sws_scale(
                        _swsContext,
                        _sourceFrame->data,
                        _sourceFrame->linesize,
                        0,
                        _codecContext->height,
                        _rgbFrame->data,
                        _rgbFrame->linesize);

                    return CreateBitmapFromFrame();
                }
                finally
                {
                    ffmpeg.av_packet_unref(&packet);
                }
            }

            throw new InvalidOperationException("Could not capture frame at the specified time.");
        }
        finally
        {
            ffmpeg.av_packet_unref(&packet);
        }
    }

    private bool TryDecodeFrame(AVPacket* packet)
    {
        if (ffmpeg.avcodec_send_packet(_codecContext, packet) != 0)
        {
            return false;
        }

        if (ffmpeg.avcodec_receive_frame(_codecContext, _sourceFrame) != 0)
        {
            return false;
        }

        return true;
    }

    private Bitmap CreateBitmapFromFrame()
    {
        var width = CaptureSize.width;
        var height = CaptureSize.height;
        var stride = CalculateStride(width);

        var bitmapData = new byte[stride * height];

        // Copy data row by row, taking padding into account
        for (int y = 0; y < height; y++)
        {
            var sourcePtr = (IntPtr)(_rgbFrame->data[0] + y * _rgbFrame->linesize[0]);
            var targetOffset = y * stride;
            System.Runtime.InteropServices.Marshal.Copy(sourcePtr, bitmapData, targetOffset, width * 3);
        }

        unsafe
        {
            fixed (byte* ptr = bitmapData)
            {
                return new Bitmap(width, height, stride, PixelFormat.Format24bppRgb, (IntPtr)ptr);
            }
        }
    }

    private static int CalculateStride(int width)
    {
        // 4-byte alignment
        return ((width * 3 + 3) / 4) * 4;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_frameBuffer != null)
        {
            ffmpeg.av_free(_frameBuffer);
            _frameBuffer = null;
        }

        if (_rgbFrame != null)
        {
            var frame = _rgbFrame;
            ffmpeg.av_frame_free(&frame);
            _rgbFrame = null;
        }

        if (_sourceFrame != null)
        {
            var frame = _sourceFrame;
            ffmpeg.av_frame_free(&frame);
            _sourceFrame = null;
        }

        if (_swsContext != null)
        {
            ffmpeg.sws_freeContext(_swsContext);
            _swsContext = null;
        }

        if (_codecContext != null)
        {
            var context = _codecContext;
            ffmpeg.avcodec_free_context(&context);
            _codecContext = null;
        }

        if (_formatContext != null)
        {
            var context = _formatContext;
            ffmpeg.avformat_close_input(&context);
            _formatContext = null;
        }

        _disposed = true;
    }
}
