using System.Runtime.InteropServices;
using System.Windows;

namespace ToyBoxx.Foundation;

public class WindowStatus
{
    public WindowState WindowState { get; set; }
    public WindowStyle WindowStyle { get; set; }
    public ResizeMode ResizeMode { get; set; }
    public double Top { get; set; }
    public double Left { get; set; }

    public static void EnableDisplayTimeout()
    {
        NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);
    }

    public static void DisableDisplayTimeout()
    {
        NativeMethods.SetThreadExecutionState(NativeMethods.EXECUTION_STATE.ES_DISPLAY_REQUIRED | NativeMethods.EXECUTION_STATE.ES_CONTINUOUS);
    }

    public void ApplyState(Window w)
    {
        ArgumentNullException.ThrowIfNull(w, nameof(w));

        w.WindowState = WindowState;
        w.Top = Top;
        w.Left = Left;
        w.WindowStyle = WindowStyle;
        w.ResizeMode = ResizeMode;
    }

    public void CaptureState(Window w)
    {
        ArgumentNullException.ThrowIfNull(w, nameof(w));

        WindowState = w.WindowState;
        Top = w.Top;
        Left = w.Left;
        WindowStyle = w.WindowStyle;
        ResizeMode = w.ResizeMode;
    }

    /// <summary>
    /// Provides access to disable or enable screen timeout. Original idea taken from:
    /// http://www.blackwasp.co.uk/DisableScreensaver.aspx address.
    /// </summary>
    private static class NativeMethods
    {
        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);
    }
}
