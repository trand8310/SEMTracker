using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MainClient.Net;

public static class ScreenshotHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public RECT rcNormalPosition;
    }

    private const int SW_SHOWMINIMIZED = 2;
    private const int SW_MINIMIZE = 6;
    private const int SW_SHOWMINNOACTIVE = 7;

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    public sealed class CaptureResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
        public string ContentType { get; set; } = "image/jpeg";
        public string FileName { get; set; } = "";
        public string ImageBase64 { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public string CaptureMode { get; set; } = "";
    }

    public static CaptureResult CaptureFullScreen(long quality = 85L)
    {
        try
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (width <= 0 || height <= 0)
            {
                return new CaptureResult
                {
                    Success = false,
                    Status = "session_disconnected_or_locked",
                    Message = "无法获取虚拟屏幕尺寸，可能当前会话已断开或锁定"
                };
            }

            using var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
            }

            var bytes = SaveBitmapToJpegBytes(bmp, quality);

            return new CaptureResult
            {
                Success = true,
                Status = "ok",
                Message = "整屏截图成功",
                FileName = $"screen_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                ImageBase64 = Convert.ToBase64String(bytes),
                Width = width,
                Height = height,
                CaptureMode = "screen"
            };
        }
        catch (Exception ex)
        {
            return new CaptureResult
            {
                Success = false,
                Status = "capture_failed",
                Message = "整屏截图失败: " + ex.Message
            };
        }
    }

    public static CaptureResult CaptureWindow(IntPtr hWnd, long quality = 85L)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
        {
            return new CaptureResult
            {
                Success = false,
                Status = "window_not_found",
                Message = "窗口句柄无效"
            };
        }

        if (!IsWindowVisible(hWnd))
        {
            return new CaptureResult
            {
                Success = false,
                Status = "window_not_visible",
                Message = "窗口不可见"
            };
        }

        if (IsMinimized(hWnd))
        {
            return new CaptureResult
            {
                Success = false,
                Status = "window_minimized",
                Message = "窗口已最小化，无法稳定截图"
            };
        }

        if (!GetWindowRect(hWnd, out var rect))
        {
            return new CaptureResult
            {
                Success = false,
                Status = "window_rect_failed",
                Message = "获取窗口坐标失败"
            };
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
        {
            return new CaptureResult
            {
                Success = false,
                Status = "window_invalid_size",
                Message = "窗口尺寸无效"
            };
        }

        try
        {
            using var bmp = new Bitmap(width, height);

            bool printWindowOk = false;
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                try
                {
                    try
                    {
                        printWindowOk = PrintWindow(hWnd, hdc, 0);
                    }
                    catch
                    {
                        printWindowOk = false;
                    }
                }
                finally
                {
                    g.ReleaseHdc(hdc);
                }
            }

            string captureMode = "print_window";

            if (!printWindowOk)
            {
                using var g2 = Graphics.FromImage(bmp);
                g2.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                captureMode = "copy_from_screen";
            }

            var bytes = SaveBitmapToJpegBytes(bmp, quality);

            return new CaptureResult
            {
                Success = true,
                Status = "ok",
                Message = "窗口截图成功",
                FileName = $"app_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                ImageBase64 = Convert.ToBase64String(bytes),
                Width = width,
                Height = height,
                CaptureMode = captureMode
            };
        }
        catch (Exception ex)
        {
            return new CaptureResult
            {
                Success = false,
                Status = "capture_failed",
                Message = "窗口截图失败: " + ex.Message
            };
        }
    }

    public static CaptureResult CaptureCurrentProcessMainWindow(long quality = 85L)
    {
        IntPtr hWnd = IntPtr.Zero;

        try
        {
            hWnd = Process.GetCurrentProcess().MainWindowHandle;
        }
        catch
        {
        }

        if (hWnd == IntPtr.Zero)
        {
            return new CaptureResult
            {
                Success = false,
                Status = "window_not_found",
                Message = "当前进程主窗口句柄为空"
            };
        }

        return CaptureWindow(hWnd, quality);
    }

    public static CaptureResult CaptureForegroundWindow(long quality = 85L)
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            return new CaptureResult
            {
                Success = false,
                Status = "window_not_found",
                Message = "未找到前台窗口"
            };
        }

        return CaptureWindow(hWnd, quality);
    }

    private static bool IsMinimized(IntPtr hWnd)
    {
        var wp = new WINDOWPLACEMENT();
        wp.length = Marshal.SizeOf<WINDOWPLACEMENT>();

        if (!GetWindowPlacement(hWnd, ref wp))
            return false;

        return wp.showCmd == SW_SHOWMINIMIZED ||
               wp.showCmd == SW_MINIMIZE ||
               wp.showCmd == SW_SHOWMINNOACTIVE;
    }

    private static byte[] SaveBitmapToJpegBytes(Bitmap bmp, long quality)
    {
        using var ms = new MemoryStream();

        var codec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);

        if (codec == null)
        {
            bmp.Save(ms, ImageFormat.Jpeg);
            return ms.ToArray();
        }

        using var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        bmp.Save(ms, codec, ep);
        return ms.ToArray();
    }
}