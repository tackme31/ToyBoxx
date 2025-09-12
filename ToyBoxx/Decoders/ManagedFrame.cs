using FFmpeg.AutoGen;

namespace ToyBoxx.Decoders;

public unsafe class ManagedFrame : IDisposable
{
    private readonly AVFrame* _frame;

    public ManagedFrame(AVFrame* frame)
    {
        _frame = frame;
    }

    ~ManagedFrame()
    {
        DisposeUnManaged();
    }

    public void Dispose()
    {
        DisposeUnManaged();
        GC.SuppressFinalize(this);
    }

    private bool isDisposed = false;

    private void DisposeUnManaged()
    {
        if (isDisposed)
        {
            return;
        }

        AVFrame* aVFrame = _frame;
        ffmpeg.av_frame_free(&aVFrame);

        isDisposed = true;
    }
}