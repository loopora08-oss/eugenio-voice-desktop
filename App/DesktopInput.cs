using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;
using VoceLocale.Core;

namespace VoceLocale;

public sealed record InputTarget(nint Window, int ProcessId, string ElementId);

public sealed class DesktopInput : IDisposable
{
    private const int HotkeyId = 0x6141;
    private readonly HwndSource _source;
    private readonly HookProc _keyboardCallback;
    private readonly HookProc _mouseCallback;
    private readonly WinEventProc _focusCallback;
    private nint _keyboardHook, _mouseHook;
    private nint _focusHook, _foregroundHook;
    private readonly nint _ignoredMouseWindow;
    private Hotkey _hotkey;
    private bool _registered;
    private InputTarget? _watchedTarget;
    private nint _watchedWindow;
    private int _watchGeneration;
    private readonly DispatcherTimer _focusPoll = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _focusCheckPending;
    public bool Watching { get; set; }
    public bool Changed { get; private set; }
    public event Action? Toggle;
    public event Action? Cancel;
    public DesktopInput(nint hwnd, nint ignoredMouseWindow = 0)
    {
        _ignoredMouseWindow = ignoredMouseWindow;
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(WindowMessage);
        _keyboardCallback = KeyboardHook;
        _mouseCallback = MouseHook;
        _focusCallback = (_, evt, hwnd, _, _, _, _) => ObserveFocus(evt, hwnd);
        _focusPoll.Tick += (_, _) => { if (Watching && !Changed && _watchedTarget is { } target) _ = VerifyFocusAsync(target, _watchGeneration); };
        _keyboardHook = SetWindowsHookEx(13, _keyboardCallback, GetModuleHandle(null), 0);
        _mouseHook = SetWindowsHookEx(14, _mouseCallback, GetModuleHandle(null), 0);
        _focusHook = SetWinEventHook(0x8005, 0x8005, 0, _focusCallback, 0, 0, 2);
        _foregroundHook = SetWinEventHook(3, 3, 0, _focusCallback, 0, 0, 2);
        if (_keyboardHook == 0 || _mouseHook == 0 || _focusHook == 0 || _foregroundHook == 0) { Dispose(); throw new Win32Exception(); }
    }
    public bool Configure(Hotkey next)
    {
        if (_registered && next == _hotkey) return true;
        if (_registered) UnregisterHotKey(_source.Handle, HotkeyId);
        if (RegisterHotKey(_source.Handle, HotkeyId, next.Modifiers | 0x4000, next.Key))
        { _registered = true; _hotkey = next; return true; }
        if (_registered) _registered = RegisterHotKey(_source.Handle, HotkeyId, _hotkey.Modifiers | 0x4000, _hotkey.Key);
        return false;
    }
    public void BeginWatch() { _watchGeneration++; _watchedTarget = null; _watchedWindow = GetForegroundWindow(); Changed = false; Watching = true; _focusPoll.Start(); }
    public void TrackTarget(InputTarget target) => _watchedTarget = target;
    public void EndWatch() { _watchGeneration++; Watching = false; Changed = false; _watchedTarget = null; _focusPoll.Stop(); }
    private void ObserveFocus(uint evt, nint hwnd)
    {
        if (!Watching || Changed) return;
        // Windows also sends focus notifications for background apps and repeated
        // notifications for the same editor. Only the selected destination matters.
        if (GetForegroundWindow() != _watchedWindow || (evt == 3 && hwnd != _watchedWindow))
        { Changed = true; return; }
        var target = _watchedTarget;
        if (target == null || evt != 0x8005) return;
        if (hwnd != 0)
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != target.ProcessId) return;
        }
        _ = VerifyFocusAsync(target, _watchGeneration);
    }
    private async Task VerifyFocusAsync(InputTarget target, int generation)
    {
        if (_focusCheckPending) return;
        _focusCheckPending = true;
        try
        {
            var focused = await Task.Run(Capture).WaitAsync(TimeSpan.FromSeconds(2));
            if (Watching && generation == _watchGeneration && _watchedTarget == target && focused != target) Changed = true;
        }
        catch { if (Watching && generation == _watchGeneration) Changed = true; }
        finally { _focusCheckPending = false; }
    }
    public static bool ModifiersDown() => new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
    private nint WindowMessage(nint hwnd, int msg, nint wp, nint lp, ref bool handled)
    {
        if (msg == 0x312 && wp.ToInt32() == HotkeyId) { handled = true; Toggle?.Invoke(); }
        return 0;
    }
    private nint KeyboardHook(int code, nint wp, nint lp)
    {
        if (code >= 0 && Watching && (wp == 0x100 || wp == 0x104))
        {
            var info = Marshal.PtrToStructure<KeyboardInfo>(lp);
            if (info.ExtraInfo != 0x564F4345)
            {
                if (info.Key == 0x1B) { Cancel?.Invoke(); return 1; }
                bool modifier = info.Key is 0x10 or 0x11 or 0x12 or >= 0xA0 and <= 0xA5;
                bool trigger = info.Key == _hotkey.Key &&
                    ((_hotkey.Modifiers & 2) == 0 || (GetAsyncKeyState(0x11) & 0x8000) != 0) &&
                    ((_hotkey.Modifiers & 1) == 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0) &&
                    ((_hotkey.Modifiers & 4) == 0 || (GetAsyncKeyState(0x10) & 0x8000) != 0);
                if (!modifier && !trigger) Changed = true;
            }
        }
        return CallNextHookEx(_keyboardHook, code, wp, lp);
    }
    private nint MouseHook(int code, nint wp, nint lp)
    {
        if (code >= 0 && Watching && wp.ToInt32() is 0x201 or 0x204 or 0x207 or 0x20A or 0x20B)
        {
            var info = Marshal.PtrToStructure<MouseInfo>(lp);
            if (_ignoredMouseWindow == 0 || WindowFromPoint(info.Point) != _ignoredMouseWindow) Changed = true;
        }
        return CallNextHookEx(_mouseHook, code, wp, lp);
    }
    public static InputTarget? Capture()
    {
        try
        {
            nint window = GetForegroundWindow();
            GetWindowThreadProcessId(window, out uint pid);
            if (pid == Environment.ProcessId || window == 0 || !AllowedIntegrity(pid)) return null;
            var focused = AutomationElement.FocusedElement;
            if (focused == null) return null;
            var current = focused.Current;
            if (current.ProcessId != pid || current.IsPassword || !current.IsEnabled || !current.IsKeyboardFocusable) return null;
            bool editable = current.ControlType == ControlType.Edit;
            if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var value))
            {
                if (((ValuePattern)value).Current.IsReadOnly) return null;
                editable = true;
            }
            if (!editable && focused.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern))
            {
                // A web document also exposes TextPattern when it is NOT an editor. Inspect
                // only the read-only capability; never retrieve the document's contents.
                var readOnly = ((TextPattern)textPattern).DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                editable = readOnly is false;
            }
            if (!editable) return null;
            // Only read identity and capabilities; never Name, Value, text ranges or surrounding text.
            var id = focused.GetRuntimeId();
            if (id.Length == 0) return null;
            return new(window, (int)pid, string.Join('.', id));
        }
        catch { return null; }
    }
    public async Task<bool> InsertAsync(InputTarget target, string text, CancellationToken token)
    {
        // Do not allow Enter/Tab/control characters to execute commands or submit chats.
        var safe = TextPolicy.ToSingleParagraph(text);
        if (safe.Length > 0 && text.StartsWith(' ')) safe = " " + safe;
        if (safe.Any(char.IsControl)) return false;
        for (int n = 0; n < 30 && ModifiersDown(); n++) await Task.Delay(25, token);
        if (ModifiersDown()) return false;
        for (int offset = 0; offset < safe.Length;)
        {
            token.ThrowIfCancellationRequested();
            if (Changed || ModifiersDown() || GetForegroundWindow() != target.Window) return false;
            var current = await Task.Run(Capture, token).WaitAsync(TimeSpan.FromSeconds(2), token);
            if (current != target || Changed) return false;
            int count = Math.Min(24, safe.Length - offset);
            if (offset + count < safe.Length && char.IsHighSurrogate(safe[offset + count - 1])) count--;
            var inputs = new INPUT[count * 2];
            for (int i = 0; i < count; i++)
            {
                inputs[2 * i] = new INPUT { Type = 1, Data = new InputUnion { Keyboard = new KEYBDINPUT { Scan = safe[offset + i], Flags = 4, ExtraInfo = 0x564F4345 } } };
                inputs[2 * i + 1] = inputs[2 * i];
                inputs[2 * i + 1].Data.Keyboard.Flags = 6;
            }
            // Pace Unicode packets so rich editors/IME queues consume each scalar before the
            // next one. Keep surrogate pairs in a single SendInput call.
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Changed || ModifiersDown() || GetForegroundWindow() != target.Window) return false;
                bool pair = char.IsHighSurrogate(safe[offset + i]) && i + 1 < count;
                var scalar = inputs.AsSpan(i * 2, pair ? 4 : 2).ToArray();
                if (SendInput((uint)scalar.Length, scalar, Marshal.SizeOf<INPUT>()) != scalar.Length) return false;
                if (pair) i++;
                await Task.Delay(3, token);
            }
            offset += count;
        }
        await Task.Delay(40, token);
        return true;
    }
    private static bool AllowedIntegrity(uint pid)
    {
        var handle = OpenProcess(0x1000, false, pid);
        if (handle == 0) return false;
        try
        {
            if (!OpenProcessToken(handle, 8, out var token)) return false;
            try
            {
                GetTokenInformation(token, 25, 0, 0, out int size);
                if (size <= 0) return false;
                nint memory = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetTokenInformation(token, 25, memory, size, out _)) return false;
                    var sid = Marshal.ReadIntPtr(memory);
                    byte count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                    if (count == 0) return false;
                    uint level = (uint)Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
                    return level <= 0x2000; // Normal desktop or below; refuse elevated/UIAccess targets.
                }
                finally { Marshal.FreeHGlobal(memory); }
            }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(handle); }
    }
    public void Dispose()
    {
        EndWatch();
        if (_registered) UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
        if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
        if (_focusHook != 0) UnhookWinEvent(_focusHook);
        if (_foregroundHook != 0) UnhookWinEvent(_foregroundHook);
        _keyboardHook = _mouseHook = 0;
        _source.RemoveHook(WindowMessage);
    }
    private delegate nint HookProc(int code, nint wp, nint lp);
    private delegate void WinEventProc(nint hook, uint evt, nint hwnd, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInfo { public uint Key, Scan, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInfo { public NativePoint Point; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT Keyboard; [FieldOffset(0)] public MOUSEINPUT Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort Key, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int X, Y; public uint MouseData, Flags, Time; public nuint ExtraInfo; }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wp, nint lp);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("advapi32.dll")] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll")] private static extern bool GetTokenInformation(nint token, int kind, nint info, int length, out int needed);
    [DllImport("advapi32.dll")] private static extern nint GetSidSubAuthorityCount(nint sid);
    [DllImport("advapi32.dll")] private static extern nint GetSidSubAuthority(nint sid, uint index);
}
