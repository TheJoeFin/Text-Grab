using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using Text_Grab.Models;

namespace Text_Grab.Utilities;

// https://stackoverflow.com/questions/3654787/global-hotkey-in-console-application
// Answer by: https://stackoverflow.com/users/314028/chris-taylor
// Read on 11/18/2021

public static partial class HotKeyManager
{
    public static event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    public static int? RegisterHotKey(ShortcutKeySet keySet)
    {
        if (Enum.TryParse(keySet.NonModifierKey.ToString(), out Keys winFormsKeys))
            return RegisterHotKey(winFormsKeys, keySet.Modifiers.Aggregate((x, y) => x | y));
        else
            return null;
    }

    public static int RegisterHotKey(Keys key, KeyModifiers modifiers)
    {
        _windowReadyEvent?.WaitOne();
        int id = System.Threading.Interlocked.Increment(ref _id);
        _wnd?.Invoke(new RegisterHotKeyDelegate(RegisterHotKeyInternal), _hwnd, id, (uint)modifiers, (uint)key);
        return id;
    }

    public static void UnregisterHotKey(int id)
    {
        _wnd?.Invoke(new UnRegisterHotKeyDelegate(UnRegisterHotKeyInternal), _hwnd, id);
    }

    delegate void RegisterHotKeyDelegate(IntPtr hwnd, int id, uint modifiers, uint key);
    delegate void UnRegisterHotKeyDelegate(IntPtr hwnd, int id);

    private static void RegisterHotKeyInternal(IntPtr hwnd, int id, uint modifiers, uint key)
    {
        RegisterHotKey(hwnd, id, modifiers, key);
    }

    private static void UnRegisterHotKeyInternal(IntPtr hwnd, int id)
    {
        UnregisterHotKey(_hwnd, id);
    }

    private static void OnHotKeyPressed(HotKeyEventArgs e)
    {
        if (HotKeyManager.HotKeyPressed != null)
        {
            HotKeyManager.HotKeyPressed(null, e);
        }
    }

    private static volatile MessageWindow? _wnd;
    private static volatile IntPtr _hwnd;
    private static ManualResetEvent? _windowReadyEvent = new ManualResetEvent(false);
    static HotKeyManager()
    {
        Thread messageLoop = new Thread(delegate ()
          {
              Application.Run(new MessageWindow());
          });
        messageLoop.Name = "MessageLoopThread";
        messageLoop.IsBackground = true;
        messageLoop.Start();
    }

    private class MessageWindow : Form
    {
        public MessageWindow()
        {
            _wnd = this;
            _hwnd = this.Handle;
            _windowReadyEvent?.Set();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                HotKeyEventArgs e = new HotKeyEventArgs(m.LParam);
                HotKeyManager.OnHotKeyPressed(e);
            }

            base.WndProc(ref m);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Ensure the window never becomes visible
            base.SetVisibleCore(false);
        }

        private const int WM_HOTKEY = 0x312;
    }

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    private static int _id = 0;
}


public class HotKeyEventArgs : EventArgs
{
    public readonly Keys Key;
    public readonly KeyModifiers Modifiers;

    public HotKeyEventArgs(Keys key, KeyModifiers modifiers)
    {
        this.Key = key;
        this.Modifiers = modifiers;
    }

    public HotKeyEventArgs(IntPtr hotKeyParam)
    {
        uint param = (uint)hotKeyParam.ToInt64();
        Key = (Keys)((param & 0xffff0000) >> 16);
        Modifiers = (KeyModifiers)(param & 0x0000ffff);
    }
}

[Flags]
public enum KeyModifiers
{
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
    NoRepeat = 0x4000
}
