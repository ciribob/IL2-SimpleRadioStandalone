using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ciribob.IL2.SimpleRadio.Standalone.Overlay
{
    //Source http://stackoverflow.com/a/37724335
    public static class WindowHelper
    {
        private const int SW_RESTORE = 9;

        public static void BringProcessToFront(Process process)
        {
            var handle = process.MainWindowHandle;
            if (IsIconic(handle))
            {
                ShowWindow(handle, SW_RESTORE);
            }

            SetForegroundWindow(handle);
        }

        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        [DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int nCmdShow);

        [DllImport("User32.dll")]
        private static extern bool IsIconic(IntPtr handle);

        [DllImport("User32.dll")]
        public static extern IntPtr GetForegroundWindow();
    }
}