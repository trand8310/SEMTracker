using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SEM.Win32
{
    public class NativeMethod
    {
        public const int WM_COPYDATA = 0x004A;
        public const int WM_MYSYMPLE = 0x005A;
        public const int SW_SHOWNOACTIVATE = 4;
        public const int CREATE_NEW_CONSOLE = 0x00000010;
        public const int STARTF_USESHOWWINDOW = 0x00000001;
        public const int WM_CLOSE = 0x0010;



        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);



        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        [In] ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);
        public static IntPtr FindMainWindow(uint processId)
        {
            IntPtr found = IntPtr.Zero;

            EnumWindows((hWnd, lParam) =>
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);

                if (pid == processId && IsWindowVisible(hWnd) && GetParent(hWnd) == IntPtr.Zero)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }




        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr parentWindow, IntPtr previousChildWindow, string windowClass, string windowTitle);
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr window, out int process);
        private IntPtr[] GetProcessWindows(int process, string title)
        {
            IntPtr[] apRet = (new IntPtr[256]);
            int iCount = 0;
            IntPtr pLast = IntPtr.Zero;
            do
            {
                pLast = FindWindowEx(IntPtr.Zero, pLast, null, title);
                int iProcess_;
                GetWindowThreadProcessId(pLast, out iProcess_);
                if (iProcess_ == process) apRet[iCount++] = pLast;
            } while (pLast != IntPtr.Zero);
            System.Array.Resize(ref apRet, iCount);
            return apRet;
        }



   


        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        public static extern int SendMessage(int hWnd, int msg, int wParam, ref COPYDATASTRUCT lParam);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(int hWnd, int Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);


        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);


        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        public static extern void keybd_event(

           byte bVk,    //虚拟键值
           byte bScan,// 一般为0
           int dwFlags,  //这里是整数类型  0 为按下，2为释放
           int dwExtraInfo  //这里是整数类型 一般情况下设成为 0
       );






    }
}