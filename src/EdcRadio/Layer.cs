using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace EdcRadio
{
    internal sealed class Layer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Blend
        {
            public byte Op;
            public byte Flags;
            public byte Alpha;
            public byte Format;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Header
        {
            public int Size;
            public int Width;
            public int Height;
            public short Planes;
            public short Bits;
            public int Compression;
            public int ImageSize;
            public int XPels;
            public int YPels;
            public int Used;
            public int Important;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr screen, IntPtr position, ref Size size, IntPtr source, ref Point origin, int key, ref Blend blend, int flags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr item);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr item);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr dc, ref Header header, uint usage, out IntPtr bits, IntPtr section, uint offset);

        private IntPtr _dc;
        private IntPtr _bitmap;
        private IntPtr _old;

        public IntPtr Bits { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stride => Width * 4;

        public bool Ensure(int width, int height)
        {
            if (_bitmap != IntPtr.Zero && width == Width && height == Height) return true;
            Free();
            _dc = CreateCompatibleDC(IntPtr.Zero);
            if (_dc == IntPtr.Zero) return false;
            var header = new Header { Size = Marshal.SizeOf<Header>(), Width = width, Height = -height, Planes = 1, Bits = 32 };
            _bitmap = CreateDIBSection(_dc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero)
            {
                Free();
                return false;
            }
            _old = SelectObject(_dc, _bitmap);
            Bits = bits;
            Width = width;
            Height = height;
            return true;
        }

        public bool Push(IntPtr hwnd, byte alpha)
        {
            if (_bitmap == IntPtr.Zero) return false;
            var size = new Size(Width, Height);
            var origin = Point.Empty;
            var blend = new Blend { Op = 0, Flags = 0, Alpha = alpha, Format = 1 };
            return UpdateLayeredWindow(hwnd, IntPtr.Zero, IntPtr.Zero, ref size, _dc, ref origin, 0, ref blend, 2);
        }

        private void Free()
        {
            if (_dc != IntPtr.Zero && _old != IntPtr.Zero) SelectObject(_dc, _old);
            if (_bitmap != IntPtr.Zero) DeleteObject(_bitmap);
            if (_dc != IntPtr.Zero) DeleteDC(_dc);
            _dc = _bitmap = _old = IntPtr.Zero;
            Bits = IntPtr.Zero;
            Width = Height = 0;
        }

        public void Dispose() => Free();
    }
}
