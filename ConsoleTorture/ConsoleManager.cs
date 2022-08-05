using System.Runtime.InteropServices;
using static ConsoleTorture.PInvoke;

namespace ConsoleTorture;

public class ConsoleManager
{
    public static short BufferWidth { get; private set; }
    public static short BufferHeight { get; private set; }

    public static bool showFps;

    private static bool _started;
    private static CharInfo[] _buffer;
    private static SmallRect _consoleBounds;

    private static void ConsoleSetup(short width, short height)
    {
        // capability test
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("This program is windows only!");
            Console.ReadKey(true);
            Environment.Exit(0);
        }

        Console.CursorVisible = false;
        BufferWidth = width;
        BufferHeight = height;

        // remove resizing
        var handle = GetConsoleWindow();
        var sysMenu = GetSystemMenu(handle, false);

        if (handle != IntPtr.Zero)
        {
            DeleteMenu(sysMenu, SC_CLOSE, MF_BYCOMMAND);
            DeleteMenu(sysMenu, SC_MINIMIZE, MF_BYCOMMAND);
            DeleteMenu(sysMenu, SC_MAXIMIZE, MF_BYCOMMAND);
            DeleteMenu(sysMenu, SC_SIZE, MF_BYCOMMAND);
        }

        // set console size
        var hConsole = GetStdHandle(-11);
        SetConsoleDisplayMode(hConsole, 1, out _);
        Console.SetWindowSize(BufferWidth, BufferHeight);

        // set buffer
        _buffer = new CharInfo[BufferWidth * BufferHeight];
        _consoleBounds = new() { Left = 0, Top = 0, Right = BufferWidth, Bottom = BufferHeight };
    }

    private static void StartInput()
    {
        Task.Run(() =>
        {
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key is ConsoleKey.Escape) Environment.Exit(0);
                if (key.Key is ConsoleKey.F3) showFps = !showFps;
            }
        });
    }

    public static void Start(short width = 120, short height = 30)
    {
        // setup
        if (_started) return;
        _started = true;
        ConsoleSetup(width, height);

        var h = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
        if (h.IsInvalid)
        {
            Console.WriteLine("Invalid behavior, oof + L + ratio");
            Console.ReadKey(true);
            Environment.Exit(0);
        }

        StartInput();

        Coord size = new() { X = BufferWidth, Y = BufferHeight };
        Coord zero = new() { X = 0, Y = 0 };

        var lastFrame = GetMs();
        var firstSlot = true;
        var frameSet = new int[2];
        var timeSet = new float[2];

        void UpdateFrameTime(float dt)
        {
            var i = firstSlot ? 0 : 1;
            frameSet[i]++;
            timeSet[i] += dt;
            if (timeSet[i] >= 1e3) firstSlot = !firstSlot;
            else return;
            i = firstSlot ? 0 : 1;
            frameSet[i] = 0;
            timeSet[i] = 0;
        }

        int GetFps() => (int) Math.Truncate((frameSet[0] + frameSet[1]) / (timeSet[0] + timeSet[1]) * 1e3f);

        while (true)
        {
            var thisTime = GetMs();
            var dt = thisTime - lastFrame;
            if (showFps) UpdateFrameTime((float) dt);
            lastFrame = thisTime;

            Array.Fill(_buffer, new() { Char = new() { UnicodeChar = ' ' } });

            if (showFps) PrintString($"fps: {GetFps()}", 0, 0, ConsoleColor.Green);

            WriteConsoleOutputW(h, _buffer, size, zero, ref _consoleBounds);
            Console.SetCursorPosition(0, 0);
        }
    }

    public static void PrintChar(char c, int x, int y, ConsoleColor? fontColor = null, ConsoleColor? backColor = null)
    {
        if (!_started) return;
        fontColor ??= ConsoleColor.White;
        backColor ??= ConsoleColor.Black;

        if (x < 0 || x > BufferWidth || y < 0 || y > BufferHeight) return;
        _buffer[BufferWidth * y + x] = new()
        {
            Char = new() { UnicodeChar = c },
            Attributes = (short) ((short) fontColor | ((short) backColor << 4))
        };
    }

    public static void PrintString(string s, int x, int y, ConsoleColor? fontColor = null,
        ConsoleColor? backColor = null)
    {
        if (!_started) return;
        for (var i = 0; i < s.Length; i++) PrintChar(s[i], x + i, y, fontColor, backColor);
    }

    public static double GetMs() => GetTicks() / 1e4d;
    public static long GetTicks() => DateTime.Now.Ticks;
}