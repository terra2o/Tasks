using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Tasks.Platform;

public static class NativeWindowMenu
{
    private const int ClientMessage = 33;
    private const long SubstructureRedirectMask = 0x00080000;
    private const long SubstructureNotifyMask = 0x00040000;

    [DllImport("libX11", EntryPoint = "XOpenDisplay")]
    private static extern IntPtr XOpenDisplay(string? display_name);

    [DllImport("libX11", EntryPoint = "XCloseDisplay")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11", EntryPoint = "XInternAtom")]
    private static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

    [DllImport("libX11", EntryPoint = "XDefaultRootWindow")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11", EntryPoint = "XSendEvent")]
    private static extern int XSendEvent(IntPtr display, IntPtr w, bool propagate, long event_mask, ref XClientMessageEvent event_send);

    [DllImport("libX11", EntryPoint = "XFlush")]
    private static extern int XFlush(IntPtr display);

    [StructLayout(LayoutKind.Sequential)]
    private struct XClientMessageEvent
    {
        public int type;
        public IntPtr serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr message_type;
        public int format;
        public IntPtr ptr1;
        public IntPtr ptr2;
        public IntPtr ptr3;
        public IntPtr ptr4;
        public IntPtr ptr5;
    }

    // Windows P/Invoke for Win32 system menu
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    private const int WM_SYSCOMMAND = 0x0112;
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100;

    public static bool TryShowSystemMenu(Window window, PointerPressedEventArgs e)
    {
        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle == null)
            return false;

        var screenPoint = window.PointToScreen(e.GetPosition(window));

        // Linux X11: Send _GTK_SHOW_WINDOW_MENU client message to root window
        if (WindowEnvironment.IsGnomeOrLinux)
        {
            try
            {
                var display = XOpenDisplay(null);
                if (display == IntPtr.Zero)
                    return false;

                try
                {
                    var atom = XInternAtom(display, "_GTK_SHOW_WINDOW_MENU", false);
                    var root = XDefaultRootWindow(display);

                    var msg = new XClientMessageEvent
                    {
                        type = ClientMessage,
                        display = display,
                        window = platformHandle.Handle,
                        message_type = atom,
                        format = 32,
                        ptr1 = IntPtr.Zero, // device id
                        ptr2 = (IntPtr)screenPoint.X,
                        ptr3 = (IntPtr)screenPoint.Y
                    };

                    XSendEvent(display, root, false, SubstructureRedirectMask | SubstructureNotifyMask, ref msg);
                    XFlush(display);
                    return true;
                }
                finally
                {
                    XCloseDisplay(display);
                }
            }
            catch
            {
                return false;
            }
        }

        // Windows: TrackPopupMenu with native GetSystemMenu
        if (WindowEnvironment.IsWindows)
        {
            try
            {
                var hMenu = GetSystemMenu(platformHandle.Handle, false);
                if (hMenu == IntPtr.Zero)
                    return false;

                var cmd = TrackPopupMenu(hMenu, TPM_LEFTALIGN | TPM_RETURNCMD, screenPoint.X, screenPoint.Y, 0, platformHandle.Handle, IntPtr.Zero);
                if (cmd > 0)
                {
                    SendMessage(platformHandle.Handle, WM_SYSCOMMAND, (IntPtr)cmd, IntPtr.Zero);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}
