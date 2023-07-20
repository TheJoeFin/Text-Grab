using System.Windows.Input;

namespace Text_Grab;

public static class KeyboardExtensions
{
    public static bool IsCtrlDown()
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            return true;

        return false;
    }

    public static bool IsAltDown()
    {
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            return true;

        return false;
    }

    public static bool IsShiftDown()
    {
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            return true;

        return false;
    }

    public static bool IsCtrlAltDown() => IsCtrlDown() && IsAltDown();

    public static bool IsShiftCtrlDown() => IsCtrlDown() && IsShiftDown();

    public static bool IsShiftAltDown() => IsShiftDown() && IsAltDown();

    public static bool IsShiftCtrlAltDown() => IsShiftDown() && IsCtrlDown() && IsAltDown();

    public static KeyboardModifiersDown GetKeyboardModifiersDown()
    {
        if (IsShiftCtrlAltDown()) return KeyboardModifiersDown.ShiftCtrlAlt;
        if (IsShiftAltDown()) return KeyboardModifiersDown.ShiftAlt;
        if (IsCtrlAltDown()) return KeyboardModifiersDown.CtrlAlt;
        if (IsShiftCtrlDown()) return KeyboardModifiersDown.ShiftCtrl;
        if (IsShiftDown()) return KeyboardModifiersDown.Shift;
        if (IsCtrlDown()) return KeyboardModifiersDown.Ctrl;
        if (IsAltDown()) return KeyboardModifiersDown.Alt;

        return KeyboardModifiersDown.None;
    }
}

public enum KeyboardModifiersDown
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 3,
    ShiftCtrl = 4,
    ShiftAlt = 5,
    CtrlAlt = 6,
    ShiftCtrlAlt = 7
}