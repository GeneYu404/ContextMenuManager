using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ContextMenuManager.Services;

/// <summary>
/// 从 "path,index" 形式的图标描述中提取小图标。
/// 用 GetIconInfo + GetDIBits 取出 BGRA 像素，拷入 Avalonia WriteableBitmap ——
/// 与 WPF 版的 BitmapSource.Freeze 等价：后台线程生成、UI 线程显示。
/// </summary>
internal static class IconLoader
{
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? Load(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        return Cache.GetOrAdd(source.Trim(), LoadCore);
    }

    private static Bitmap? LoadCore(string source)
    {
        var (path, index) = Parse(source);
        if (path is null) return null;

        var large = new IntPtr[1];
        var small = new IntPtr[1];
        try
        {
            var count = Native.ExtractIconEx(path, index, large, small, 1);
            if (count == 0 || count == uint.MaxValue) return null;

            var handle = small[0] != IntPtr.Zero ? small[0] : large[0];
            return handle == IntPtr.Zero ? null : FromHIcon(handle);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (large[0] != IntPtr.Zero) Native.DestroyIcon(large[0]);
            if (small[0] != IntPtr.Zero) Native.DestroyIcon(small[0]);
        }
    }

    /// <summary>把 HICON 解码为 32 位 BGRA 像素并装入 WriteableBitmap。</summary>
    private static Bitmap? FromHIcon(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var info)) return null;
        try
        {
            // 无彩色平面的单色图标极少见，直接放弃（UI 退回字形显示）
            if (info.hbmColor == IntPtr.Zero) return null;

            var desc = new BITMAP();
            if (GetObject(info.hbmColor, Marshal.SizeOf<BITMAP>(), ref desc) == 0) return null;
            int width = desc.bmWidth;
            int height = Math.Abs(desc.bmHeight);
            if (width <= 0 || height <= 0) return null;

            // biHeight 取负 = 自顶向下 DIB，省去行翻转
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            };

            var pixels = new byte[width * height * 4];
            var dc = GetDC(IntPtr.Zero);
            try
            {
                if (GetDIBits(dc, info.hbmColor, 0, (uint)height, pixels, ref header, 0) == 0) return null;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height), new Vector(96, 96),
                PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using (var fb = bitmap.Lock())
            {
                Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
        }
    }

    /// <summary>解析 "%SystemRoot%\system32\shell32.dll,-16769" / "\"C:\a b\x.exe\",0" 等写法</summary>
    public static (string? path, int index) Parse(string source)
    {
        var s = Environment.ExpandEnvironmentVariables(source.Trim());
        var index = 0;

        if (s.StartsWith('"'))
        {
            var end = s.IndexOf('"', 1);
            if (end > 1)
            {
                var rest = s[(end + 1)..].Trim();
                s = s[1..end];
                if (rest.StartsWith(',') && int.TryParse(rest[1..].Trim(), out var i1)) index = i1;
            }
        }
        else
        {
            var comma = s.LastIndexOf(',');
            if (comma > 0 && int.TryParse(s[(comma + 1)..].Trim(), out var i2))
            {
                index = i2;
                s = s[..comma];
            }
        }

        return (ShellText.FindFile(s), index);
    }

    // ================================================================= 本文件专用的 Win32 互操作

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr hObject, int cbBuffer, ref BITMAP lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
        byte[] lpvBits, ref BITMAPINFOHEADER lpbmi, uint uUsage);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public short bmBitsPlane;
        public short bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
}
