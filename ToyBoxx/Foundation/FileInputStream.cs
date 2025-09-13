using FFmpeg.AutoGen;
using System.IO;
using System.Runtime.InteropServices;
using Unosquare.FFME.Common;

namespace ToyBoxx.Foundation;

public class FileInputStream : IMediaInputStream
{
    private readonly FileStream _backingStream;
    private readonly object _readLockObject = new();
    private readonly byte[] _readBuffer;

    public FileInputStream(string path)
    {
        var fullPath = Path.GetFullPath(path);
        _backingStream = File.OpenRead(fullPath);
        StreamUri = new Uri(fullPath);
        CanSeek = true;
        _readBuffer = new byte[ReadBufferLength];
    }

    public Uri StreamUri { get; }

    public bool CanSeek { get; }

    public int ReadBufferLength => 1024 * 16;

    public InputStreamInitializing? OnInitializing { get; }

    public InputStreamInitialized? OnInitialized { get; }


    public void Dispose()
    {
        _backingStream?.Dispose();
    }

    public unsafe int Read(void* opaque, byte* targetBuffer, int targetBufferLength)
    {
        lock (_readLockObject)
        {
            try
            {
                var readCount = _backingStream.Read(_readBuffer, 0, _readBuffer.Length);
                if (readCount > 0)
                    Marshal.Copy(_readBuffer, 0, (IntPtr)targetBuffer, readCount);
                else if (readCount == 0)
                    return ffmpeg.AVERROR_EOF;

                return readCount;
            }
            catch (Exception)
            {
                return ffmpeg.AVERROR_EOF;
            }
        }
    }

    public unsafe long Seek(void* opaque, long offset, int whence)
    {
        lock (_readLockObject)
        {
            try
            {
                return whence == ffmpeg.AVSEEK_SIZE ?
                    _backingStream.Length : _backingStream.Seek(offset, SeekOrigin.Begin);
            }
            catch
            {
                return ffmpeg.AVERROR_EOF;
            }
        }
    }
}
