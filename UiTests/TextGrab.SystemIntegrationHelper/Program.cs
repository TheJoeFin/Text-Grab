using System.Runtime.InteropServices;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TextGrab.SystemIntegrationHelper;

internal static partial class Program
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseAbsolute = 0x8000;
    private const uint VirtualKeyEscape = 0x1b;
    private const uint KeyUp = 0x0002;

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2. Must be set before any screen metric is read
    // or synthetic input is injected so that GetSystemMetrics and MOUSEEVENTF_ABSOLUTE mapping
    // operate in physical pixels — the same coordinate space WinApp/UIA reports element bounds
    // in. Without this the process is DPI-unaware and every coordinate is off by the display
    // scale factor (e.g. 2x at 200%).
    private static readonly IntPtr PerMonitorAwareV2 = -4;

    [STAThread]
    private static int Main(string[] args)
    {
        SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            return args.FirstOrDefault() switch
            {
                "--preflight" => Preflight(),
                "--click" => Click(args, false),
                "--right-click" => Click(args, true),
                "--drag" => Drag(args),
                "--move" => MoveCursor(args),
                "--escape" => SendKey(VirtualKeyEscape),
                "--set-text" => SetText(args),
                "--set-image" => SetImage(),
                "--set-files" => SetFiles(args),
                "--drag-files" => DragFiles(args),
                "--hold-hotkey" => HoldHotkey(args),
                _ => Fail("Expected --preflight, --click, --right-click, --drag, --move, --escape, --set-text, --set-image, --set-files, --drag-files, or --hold-hotkey.")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int Preflight()
    {
        IntPtr desktop = OpenInputDesktop(0, false, 0x0100);
        int error = desktop == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
        string? name = desktop == IntPtr.Zero ? null : GetObjectName(desktop);
        if (desktop != IntPtr.Zero)
            CloseDesktop(desktop);

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            userInteractive = Environment.UserInteractive,
            inputDesktopAvailable = desktop != IntPtr.Zero,
            inputDesktop = name,
            error,
            foregroundWindow = GetForegroundWindow().ToInt64()
        }));
        return Environment.UserInteractive && desktop != IntPtr.Zero ? 0 : 2;
    }

    private static int Click(string[] args, bool right)
    {
        RequireArgumentCount(args, 3);
        int x = int.Parse(args[1]);
        int y = int.Parse(args[2]);
        Move(x, y);
        SendMouse(right ? MouseRightDown : MouseLeftDown);
        SendMouse(right ? MouseRightUp : MouseLeftUp);
        return 0;
    }

    private static int Drag(string[] args)
    {
        RequireArgumentCount(args, 5);
        int startX = int.Parse(args[1]);
        int startY = int.Parse(args[2]);
        int endX = int.Parse(args[3]);
        int endY = int.Parse(args[4]);
        Move(startX, startY);
        SendMouse(MouseLeftDown);
        Thread.Sleep(75);
        Move(endX, endY);
        Thread.Sleep(75);
        SendMouse(MouseLeftUp);
        return 0;
    }

    private static int MoveCursor(string[] args)
    {
        RequireArgumentCount(args, 3);
        int x = int.Parse(args[1]);
        int y = int.Parse(args[2]);
        // Two absolute moves guarantee a WM_MOUSEMOVE delta so auto-hiding UI, such as the
        // Fullscreen Grab toolbar, reveals itself even when the cursor already sits at (x, y).
        Move(x + 3, y + 3);
        Thread.Sleep(40);
        Move(x, y);
        return 0;
    }

    private static int SendKey(uint virtualKey)
    {
        Send(new INPUT { type = InputKeyboard, union = new InputUnion { keyboard = new KEYBDINPUT { wVk = (ushort)virtualKey } } });
        Send(new INPUT { type = InputKeyboard, union = new InputUnion { keyboard = new KEYBDINPUT { wVk = (ushort)virtualKey, dwFlags = KeyUp } } });
        return 0;
    }

    private static int SetText(string[] args)
    {
        RequireArgumentCount(args, 2);
        Clipboard.SetText(args[1]);
        return 0;
    }

    private static int SetImage()
    {
        const int width = 16;
        const int height = 16;
        byte[] pixels = Enumerable.Repeat((byte)255, width * height * 4).ToArray();
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        Clipboard.SetImage(bitmap);
        return 0;
    }

    private static int SetFiles(string[] args)
    {
        if (args.Length < 2)
            return Fail("--set-files requires at least one existing path.");

        foreach (string path in args.Skip(1))
            if (!File.Exists(path))
                return Fail($"File does not exist: {path}");

        DataObject data = new();
        data.SetData(DataFormats.FileDrop, args.Skip(1).ToArray());
        Clipboard.SetDataObject(data, true);
        return 0;
    }

    // A small visible source window is necessary because OLE drag-and-drop starts from a real HWND.
    private static int DragFiles(string[] args)
    {
        if (args.Length < 3)
            return Fail("--drag-files requires a ready-file and at least one existing path.");

        string readyFile = args[1];
        string[] files = args.Skip(2).ToArray();
        if (files.Any(path => !File.Exists(path)))
            return Fail("--drag-files received a missing path.");

        Application application = new() { ShutdownMode = ShutdownMode.OnMainWindowClose };
        Border source = new()
        {
            Background = Brushes.DodgerBlue,
            Child = new TextBlock { Text = "Text Grab test file drop source", Margin = new Thickness(12), Foreground = Brushes.White }
        };
        Window window = new()
        {
            Title = "Text Grab system-test drop source",
            Content = source,
            Width = 220,
            Height = 70,
            Left = 10,
            Top = 10,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow
        };
        source.PreviewMouseLeftButtonDown += (_, _) =>
        {
            DataObject data = new();
            data.SetData(DataFormats.FileDrop, files);
            DragDrop.DoDragDrop(source, data, DragDropEffects.Copy);
        };
        window.Loaded += (_, _) => File.WriteAllText(readyFile, "ready");
        application.Run(window);
        return 0;
    }

    private static int HoldHotkey(string[] args)
    {
        RequireArgumentCount(args, 4);
        uint modifiers = uint.Parse(args[1]);
        uint key = uint.Parse(args[2]);
        string readyFile = args[3];
        const int id = 7001;
        if (!RegisterHotKey(IntPtr.Zero, id, modifiers, key))
            return Fail($"RegisterHotKey failed with {Marshal.GetLastWin32Error()}.");

        try
        {
            File.WriteAllText(readyFile, "registered");
            Console.WriteLine("registered");
            Console.Out.Flush();
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        finally
        {
            UnregisterHotKey(IntPtr.Zero, id);
        }
    }

    private static void Move(int x, int y)
    {
        int screenWidth = GetSystemMetrics(0) - 1;
        int screenHeight = GetSystemMetrics(1) - 1;
        if (screenWidth < 1 || screenHeight < 1)
            throw new InvalidOperationException("No interactive screen is available.");

        Send(new INPUT
        {
            type = InputMouse,
            union = new InputUnion
            {
                mouse = new MOUSEINPUT
                {
                    dx = (int)Math.Round(x * 65535d / screenWidth),
                    dy = (int)Math.Round(y * 65535d / screenHeight),
                    dwFlags = MouseMove | MouseAbsolute
                }
            }
        });
    }

    private static void SendMouse(uint flags) => Send(new INPUT
    {
        type = InputMouse,
        union = new InputUnion { mouse = new MOUSEINPUT { dwFlags = flags } }
    });

    private static void Send(INPUT input)
    {
        if (SendInput(1, [input], Marshal.SizeOf<INPUT>()) != 1)
            throw new InvalidOperationException($"SendInput failed with {Marshal.GetLastWin32Error()}.");
    }

    private static string? GetObjectName(IntPtr desktop)
    {
        GetUserObjectInformation(desktop, 2, IntPtr.Zero, 0, out uint length);
        if (length == 0)
            return null;

        IntPtr buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            return GetUserObjectInformation(desktop, 2, buffer, length, out _)
                ? Marshal.PtrToStringUni(buffer)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void RequireArgumentCount(string[] args, int count)
    {
        if (args.Length != count)
            throw new ArgumentException($"Expected {count - 1} argument(s) for {args[0]}.");
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint desiredAccess);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseDesktop(IntPtr desktop);

    [LibraryImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetUserObjectInformation(IntPtr handle, int index, IntPtr info, uint length, out uint needed);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(IntPtr value);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint inputs, INPUT[] input, int size);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr window, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
