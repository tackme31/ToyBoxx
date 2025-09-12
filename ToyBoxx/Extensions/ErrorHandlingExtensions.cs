using FFmpeg.AutoGen;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ToyBoxx.Extensions;

public static class ErrorHandlingExtensions
{
    public static int OnError(this int n, Action act)
    {
        if (n >= 0)
        {
            return n;
        }

        var buffer = Marshal.AllocHGlobal(1000);
        string str;
        unsafe
        {
            ffmpeg.av_make_error_string((byte*)buffer.ToPointer(), 1000, n);
            str = new string((sbyte*)buffer.ToPointer());
        }
        Marshal.FreeHGlobal(buffer);
        Debug.WriteLine(str);
        act.Invoke();

        return n;
    }
}
