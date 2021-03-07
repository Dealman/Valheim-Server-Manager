using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Valheim_Server_Manager
{
    // Since the server process is being wrapped within this program and hidden, it proved rather tricky to find a way
    // to gracefully stop it - since it wait for you to manually close the window or press CTRL+C.
    // If the server made proper use of StandardInput, all this black magic (probably) wouldn't have been necessary...
    public static class NativeMethods
    {
        [Flags]
        enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000,
            ReadControl = 0x00020000,
            PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
        }

        private static string GetExecutablePathAboveVista(int ProcessId)
        {
            var buffer = new StringBuilder(1024);
            IntPtr hprocess = OpenProcess(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION,
                                          false, ProcessId);
            if (hprocess != IntPtr.Zero)
            {
                try
                {
                    int size = buffer.Capacity;
                    if (QueryFullProcessImageName(hprocess, 0, buffer, out size))
                    {
                        return buffer.ToString();
                    }
                } finally {
                    CloseHandle(hprocess);
                }
            }
            return null;
        }
        private static Process GetProcessByHandle(IntPtr hwnd)
        {
            try
            {
                uint processID;
                GetWindowThreadProcessId(hwnd, out processID);
                return Process.GetProcessById((int)processID);
            } catch { 
                return null; 
            }
        }
        public static string GetWindowPath(IntPtr hwind)
        {
            try
            {
                Process currentProcess = GetProcessByHandle(hwind);

                if (Environment.OSVersion.Version.Major >= 6)
                {
                    string newMethReturn = GetExecutablePathAboveVista(currentProcess.Id);
                    if (!string.IsNullOrWhiteSpace(newMethReturn))
                        return newMethReturn;
                }


                if (currentProcess != null)
                    return currentProcess.MainModule.FileName;
                else
                    return null;
            } catch { 
                return null;
            }
        }
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        private static extern bool QueryFullProcessImageName(IntPtr hprocess, int dwFlags, StringBuilder lpExeName, out int size);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hHandle);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);
        [DllImport("user32.dll")]
        public static extern int SetWindowText(IntPtr hWnd, string text);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        // Delegate to filter which windows to include 
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public static string GetWindowText(IntPtr hWnd)
        {
            int size = GetWindowTextLength(hWnd);
            if (size > 0)
            {
                var builder = new StringBuilder(size + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                return builder.ToString();
            }

            return String.Empty;
        }
        private static IEnumerable<IntPtr> FindWindows(EnumWindowsProc filter)
        {
            IntPtr found = IntPtr.Zero;
            List<IntPtr> windows = new List<IntPtr>();

            EnumWindows(delegate (IntPtr wnd, IntPtr param)
            {
                if (filter(wnd, param))
                {
                    // only add the windows that pass the filter
                    windows.Add(wnd);
                }

                // but return true here so that we iterate all windows
                return true;
            }, IntPtr.Zero);

            return windows;
        }
        public static IEnumerable<IntPtr> FindWindowsWithText(string titleText)
        {
            return FindWindows(delegate (IntPtr wnd, IntPtr param)
            {
                return GetWindowText(wnd).Contains(titleText);
            });
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out uint processId);
    }
}
