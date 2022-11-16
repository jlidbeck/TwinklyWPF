using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows;
using System.Xml.Serialization;
using System.Text.Json.Serialization;

namespace WindowPlacement
{
    // RECT structure required by WINDOWPLACEMENT structure
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public RECT(int left, int top, int right, int bottom)
        {
            this.Left = left;
            this.Top = top;
            this.Right = right;
            this.Bottom = bottom;
        }

        [XmlIgnore]
        [JsonIgnore]
        public bool IsEmpty { get { return Right <= Left || Bottom <= Top; } }
    }

    // POINT structure required by WINDOWPLACEMENT structure
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X { get; set; }
        public int Y { get; set; }

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    // WINDOWPLACEMENT stores the position, size, and state of a window
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        [JsonIgnore]
        [XmlIgnore]
        public int length { get; set; }

        [JsonIgnore]
        [XmlIgnore]
        public int flags { get; set; }

        public int showCmd { get; set; }
        public POINT minPosition { get; set; }
        public POINT maxPosition { get; set; }
        public RECT normalPosition { get; set; }

        [JsonIgnore]
        [XmlIgnore]
        public bool IsValid
        {
            get
            {
                return (showCmd > 0 && !normalPosition.IsEmpty);
            }
        }
    }

    public static class WindowPlacement
    {
        private static Encoding encoding = new UTF8Encoding();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;

        public static void SetPlacement(IntPtr windowHandle, WINDOWPLACEMENT placement)
        {
            placement.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            placement.flags = 0;
            placement.showCmd = (placement.showCmd == SW_SHOWMINIMIZED ? SW_SHOWNORMAL : placement.showCmd);
            SetWindowPlacement(windowHandle, ref placement);
        }

        public static WINDOWPLACEMENT GetPlacement(IntPtr windowHandle)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            GetWindowPlacement(windowHandle, out placement);
            return placement;
        }

        public static void SetPlacement(this Window window, WINDOWPLACEMENT placement)
        {
            WindowPlacement.SetPlacement(new WindowInteropHelper(window).Handle, placement);
        }

        public static WINDOWPLACEMENT GetPlacement(this Window window)
        {
            return WindowPlacement.GetPlacement(new WindowInteropHelper(window).Handle);
        }

    }
}
