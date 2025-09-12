using FFmpeg.AutoGen;
using ToyBoxx.Extensions;

namespace ToyBoxx.Decoders;


public unsafe class VideoDecoder : IDisposable
{
    private AVFormatContext* _formatContext;

    private AVStream* _videoStream;
    public AVStream VideoStream => *_videoStream;
    private AVCodec* _videoCodec;
    public AVCodec VideoCodec => *_videoCodec;
    private AVCodecContext* _videoCodecContext;
    public AVCodecContext VideoCodecContext => *_videoCodecContext;

    private AVStream* _audioStream;
    private AVCodec* _audioCodec;
    private AVCodecContext* _audioCodecContext;

    private readonly object _sendPackedSyncObject = new();
    private readonly Queue<AVPacketPtr> _videoPackets = new();
    private readonly Queue<AVPacketPtr> _audioPackets = new();

    private bool _isVideoFrameEnded;
    private bool _isAudioFrameEnded;

    public VideoDecoder()
    {
        var rootPath = App.Configuration["FFMpegRootPath"] ?? throw new Exception("'FFMpegRootPath' does not exist.");
        ffmpeg.RootPath = rootPath;
        ffmpeg.avdevice_register_all();
    }

    public void OpenFile(string path)
    {
        AVFormatContext* formatContext = null;
        ffmpeg.avformat_open_input(&formatContext, path, null, null)
            .OnError(() => throw new InvalidOperationException("Could not open the specified file."));
        _formatContext = formatContext;

        ffmpeg.avformat_find_stream_info(_formatContext, null)
            .OnError(() => throw new InvalidOperationException("Could not detect the stream info."));

        // Video codec
        _videoStream = GetFirstVideoStream();
        if (_videoStream is not null)
        {
            _videoCodec = ffmpeg.avcodec_find_decoder(_videoStream->codecpar->codec_id);
            if (_videoCodec is null)
            {
                throw new InvalidOperationException("Could not detect the video decoder.");
            }

            _videoCodecContext = ffmpeg.avcodec_alloc_context3(_videoCodec);
            if (_videoCodecContext is null)
            {
                throw new InvalidOperationException($"Could not allocate CodecContext of the video.");
            }

            ffmpeg.avcodec_parameters_to_context(_videoCodecContext, _videoStream->codecpar)
                .OnError(() => throw new InvalidOperationException("Failed to get the video codec parameters."));
            ffmpeg.avcodec_open2(_videoCodecContext, _videoCodec, null)
                .OnError(() => throw new InvalidOperationException("Failed to initialize the video codec."));
        }

        // Audio codec
        _audioStream = GetFirstAudioStream();
        if (_audioStream is not null)
        {
            _audioCodec = ffmpeg.avcodec_find_decoder(_audioStream->codecpar->codec_id);
            if (_audioCodec is null)
            {
                throw new InvalidOperationException("Could not detect the audio decoder.");
            }

            _audioCodecContext = ffmpeg.avcodec_alloc_context3(_audioCodec);
            if (_audioCodecContext is null)
            {
                throw new InvalidOperationException($"Could not allocate CodecContext of the audio.");
            }

            ffmpeg.avcodec_parameters_to_context(_audioCodecContext, _audioStream->codecpar)
                .OnError(() => throw new InvalidOperationException("Failed to get the audio codec parameters."));
            ffmpeg.avcodec_open2(_audioCodecContext, _audioCodec, null)
                .OnError(() => throw new InvalidOperationException("Failed to initialize the audio codec."));
        }
    }

    public int SendPacket(int index)
    {
        lock (_sendPackedSyncObject)
        {
            if (index == _videoStream->index)
            {
                if (_videoPackets.TryDequeue(out var ptr))
                {
                    ffmpeg.avcodec_send_packet(_videoCodecContext, ptr.Ptr)
                        .OnError(() => throw new InvalidOperationException("Failed to send a packet to the video decoder."));
                    ffmpeg.av_packet_unref(ptr.Ptr);
                    return 0;
                }
            }

            if (index == _audioStream->index)
            {
                if (_audioPackets.TryDequeue(out var ptr))
                {
                    ffmpeg.avcodec_send_packet(_audioCodecContext, ptr.Ptr)
                        .OnError(() => throw new InvalidOperationException("Failed to send a packet to the audio decoder."));
                    ffmpeg.av_packet_unref(ptr.Ptr);
                    return 0;
                }
            }

            while (true)
            {
                var packet = new AVPacket();
                var result = ffmpeg.av_read_frame(_formatContext, &packet);
                if (result != 0)
                {
                    // End of video
                    return -1;
                }
                else
                {
                    if (packet.stream_index == _videoStream->index)
                    {
                        if (packet.stream_index == index)
                        {
                            ffmpeg.avcodec_send_packet(_videoCodecContext, &packet)
                                .OnError(() => throw new InvalidOperationException("Failed to send a packet to the video decoder."));
                            ffmpeg.av_packet_unref(&packet);
                            return 0;
                        }
                        else
                        {
                            var p = ffmpeg.av_packet_clone(&packet);
                            _videoPackets.Enqueue(new AVPacketPtr(p));
                            continue;
                        }
                    }

                    if (packet.stream_index == _audioStream->index)
                    {
                        if (packet.stream_index == index)
                        {
                            ffmpeg.avcodec_send_packet(_audioCodecContext, &packet)
                                .OnError(() => throw new InvalidOperationException("Failed to send a packet to the audio decoder."));
                            ffmpeg.av_packet_unref(&packet);
                            return 0;
                        }
                        else
                        {
                            var p = ffmpeg.av_packet_clone(&packet);
                            _audioPackets.Enqueue(new AVPacketPtr(p));
                            continue;
                        }
                    }
                }
            }
        }
    }

    public unsafe ManagedFrame? ReadFrame()
    {
        var frame = ReadUnsafeFrame();
        if (frame is null)
        {
            return null;
        }

        return new ManagedFrame(frame);
    }

    public unsafe ManagedFrame? ReadAudioFrame()
    {
        var frame = ReadUnsafeAudioFrame();
        if (frame is null)
        {
            return null;
        }

        return new ManagedFrame(frame);
    }

    private unsafe AVFrame* ReadUnsafeFrame()
    {
        AVFrame* frame = ffmpeg.av_frame_alloc();
        if (ffmpeg.avcodec_receive_frame(_videoCodecContext, frame) == 0)
        {
            return frame;
        }

        if (_isVideoFrameEnded)
        {
            return null;
        }

        int n;
        while ((n = SendPacket(_videoStream->index)) == 0)
        {
            if (ffmpeg.avcodec_receive_frame(_videoCodecContext, frame) == 0)
            {
                return frame;
            }
        }

        _isVideoFrameEnded = true;
        ffmpeg.avcodec_send_packet(_videoCodecContext, null)
            .OnError(() => throw new InvalidOperationException("Failed to send a null packet to the decoder."));
        if (ffmpeg.avcodec_receive_frame(_videoCodecContext, frame) == 0)
        {
            return frame;
        }

        return null;
    }

    private unsafe AVFrame* ReadUnsafeAudioFrame()
    {
        AVFrame* frame = ffmpeg.av_frame_alloc();
        if (ffmpeg.avcodec_receive_frame(_audioCodecContext, frame) == 0)
        {
            return frame;
        }

        if (_isAudioFrameEnded)
        {
            return null;
        }

        int n;
        while ((n = SendPacket(_audioStream->index)) == 0)
        {
            if (ffmpeg.avcodec_receive_frame(_audioCodecContext, frame) == 0)
            {
                return frame;
            }
        }

        _isAudioFrameEnded = true;
        ffmpeg.avcodec_send_packet(_audioCodecContext, null)
            .OnError(() => throw new InvalidOperationException("Failed to send a null packet to the decoder."));
        if (ffmpeg.avcodec_receive_frame(_audioCodecContext, frame) == 0)
        {
            return frame;
        }

        return null;
    }

    private AVStream* GetFirstVideoStream() => GetFirstStreamOf(AVMediaType.AVMEDIA_TYPE_VIDEO);
    private AVStream* GetFirstAudioStream() => GetFirstStreamOf(AVMediaType.AVMEDIA_TYPE_AUDIO);

    private AVStream* GetFirstStreamOf(AVMediaType mediaType)
    {
        for (var i = 0; i < (int)_formatContext->nb_streams; ++i)
        {
            var stream = _formatContext->streams[i];
            if (stream->codecpar->codec_type == mediaType)
            {
                return stream;
            }
        }

        return null;
    }

    private struct AVPacketPtr(AVPacket* ptr)
    {
        public AVPacket* Ptr = ptr;
    }

    #region Dispose

    ~VideoDecoder()
    {
        DisposeUnManaged();
    }

    public void Dispose()
    {
        DisposeUnManaged();
        GC.SuppressFinalize(this);
    }

    private bool _isDisposed = false;

    private void DisposeUnManaged()
    {
        if (_isDisposed)
        {
            return;
        }

        AVCodecContext* videoCodecContext = _videoCodecContext;
        AVCodecContext* audioCodecContext = _audioCodecContext;
        AVFormatContext* formatContext = _formatContext;

        ffmpeg.avcodec_free_context(&videoCodecContext);
        ffmpeg.avcodec_free_context(&audioCodecContext);
        ffmpeg.avformat_close_input(&formatContext);

        _isDisposed = true;
    }

    #endregion
}