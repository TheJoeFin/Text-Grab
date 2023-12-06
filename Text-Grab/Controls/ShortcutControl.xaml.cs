using System.Collections.Generic;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Linq;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for ShortcutControl.xaml
/// </summary>
public partial class ShortcutControl : UserControl
{
    // Register a custom routed event using the Bubble routing strategy.
    public static readonly RoutedEvent RecordingStarted = EventManager.RegisterRoutedEvent(
        name: "Recording",
        routingStrategy: RoutingStrategy.Bubble,
        handlerType: typeof(RoutedEventHandler),
        ownerType: typeof(ShortcutControl));

    // Provide CLR accessors for adding and removing an event handler.
    public event RoutedEventHandler Recording
    {
        add { AddHandler(RecordingStarted, value); }
        remove { RemoveHandler(RecordingStarted, value); }
    }

    public string ShortcutName
    {
        get { return (string)GetValue(ShortcutNameProperty); }
        set { SetValue(ShortcutNameProperty, value); }
    }

    // Using a DependencyProperty as the backing store for ShortcutName.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ShortcutNameProperty =
        DependencyProperty.Register("ShortcutName", typeof(string), typeof(ShortcutControl), new PropertyMetadata("shortcutName"));

    bool isRecording = false;

    string previousSequence = string.Empty;
    public bool HasModifier { get; set; } = false;
    public bool HasLetter { get; set; } = false;

    private ShortcutKeySet _keySet = new();

    public ShortcutKeySet KeySet {
        get
        {
            return _keySet;
        }
        set
        {
            _keySet = value;

            if (_keySet.Modifiers.Contains(KeyModifiers.Windows))
                WinKey.Visibility = Visibility.Visible;
            else
                WinKey.Visibility = Visibility.Collapsed;

            if (_keySet.Modifiers.Contains(KeyModifiers.Shift))
                ShiftKey.Visibility = Visibility.Visible;
            else
                ShiftKey.Visibility = Visibility.Collapsed;

            if (_keySet.Modifiers.Contains(KeyModifiers.Control))
                CtrlKey.Visibility = Visibility.Visible;
            else
                CtrlKey.Visibility = Visibility.Collapsed;

            if (_keySet.Modifiers.Contains(KeyModifiers.Alt))
                AltKey.Visibility = Visibility.Visible;
            else

                AltKey.Visibility = Visibility.Collapsed;

            KeyLetterTextBlock.Text = _keySet.NonModifierKey.ToString();
        }
    }

    // public delegate RoutedEvent? RecordingStarted();

    public ShortcutControl()
    {
        InitializeComponent();
    }

    private void ShortcutControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!isRecording)
            return;

        e.Handled = true;

        HashSet<Key> downKeys = GetDownKeys();

        bool containsWin = downKeys.Contains(Key.LWin) || downKeys.Contains(Key.RWin);
        bool containsShift = downKeys.Contains(Key.LeftShift) || downKeys.Contains(Key.RightShift);
        bool containsCtrl = downKeys.Contains(Key.LeftCtrl) || downKeys.Contains(Key.RightCtrl);
        bool containsAlt = downKeys.Contains(Key.LeftAlt) || downKeys.Contains(Key.RightAlt);

        HashSet<Key> justLetterKeys = RemoveModifierKeys(downKeys);

        HasLetter = justLetterKeys.Count != 0;
        HasModifier = containsWin || containsShift || containsCtrl || containsAlt;

        HashSet<KeyModifiers> modifierKeys = new();

        if (HasLetter)
            KeyKey.Visibility = Visibility.Visible;
        else
            KeyKey.Visibility = Visibility.Collapsed;

        if (containsWin)
        {
            WinKey.Visibility = Visibility.Visible;
            modifierKeys.Add(KeyModifiers.Windows);
        }
        else
            WinKey.Visibility = Visibility.Collapsed;

        if (containsShift)
        {
            ShiftKey.Visibility = Visibility.Visible;
            modifierKeys.Add(KeyModifiers.Shift);
        }
        else
            ShiftKey.Visibility = Visibility.Collapsed;

        if (containsCtrl)
        {
            CtrlKey.Visibility = Visibility.Visible;
            modifierKeys.Add(KeyModifiers.Control);
        }
        else
            CtrlKey.Visibility = Visibility.Collapsed;

        if (containsAlt)
        {
            AltKey.Visibility = Visibility.Visible;
            modifierKeys.Add(KeyModifiers.Alt);
        }
        else
            AltKey.Visibility = Visibility.Collapsed;

        List<string> keyStrings = [];
        foreach (Key key in justLetterKeys)
            keyStrings.Add(key.ToString());

        string currentSequence = string.Join('+', keyStrings);

        if (HasLetter && HasModifier)
            KeySet = new(modifierKeys, justLetterKeys.FirstOrDefault());

        if (string.IsNullOrEmpty(currentSequence) || currentSequence.Equals(previousSequence))
            return;

        KeyLetterTextBlock.Text = string.Join('+', justLetterKeys);
        previousSequence = currentSequence;
    }

    private void ShortcutControl_PreviewKeyUp(object sender, KeyEventArgs e)
    {

    }

    private static HashSet<Key> RemoveModifierKeys(HashSet<Key> downKeys)
    {
        HashSet<Key> filteredKeys = new(downKeys);

        filteredKeys.Remove(Key.LWin);
        filteredKeys.Remove(Key.RWin);

        filteredKeys.Remove(Key.LeftShift);
        filteredKeys.Remove(Key.RightShift);

        filteredKeys.Remove(Key.LeftCtrl);
        filteredKeys.Remove(Key.RightCtrl);

        filteredKeys.Remove(Key.LeftAlt);
        filteredKeys.Remove(Key.RightAlt);


        return filteredKeys;
    }

    private static readonly byte[] DistinctVirtualKeys = Enumerable
        .Range(0, 256)
        .Select(KeyInterop.KeyFromVirtualKey)
        .Where(item => item != Key.None)
        .Distinct()
        .Select(item => (byte)KeyInterop.VirtualKeyFromKey(item))
        .ToArray();

    public static HashSet<Key> GetDownKeys()
    {
        var keyboardState = new byte[256];
        NativeMethods.GetKeyboardState(keyboardState);

        HashSet<Key> downKeys = [];
        for (var index = 0; index < DistinctVirtualKeys.Length; index++)
        {
            var virtualKey = DistinctVirtualKeys[index];
            if ((keyboardState[virtualKey] & 0x80) != 0)
                downKeys.Add(KeyInterop.KeyFromVirtualKey(virtualKey));
        }

        return downKeys;
    }

    private void RecordingToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton recordingToggleButton)
            return;

        isRecording = recordingToggleButton.IsChecked ?? false;

        if (isRecording)
            RaiseEvent(new RoutedEventArgs(RecordingStarted, this));

    }

    public void StopRecording(object sender)
    {
        RecordingToggleButton.IsChecked = false;
        isRecording = false;
    }
}

public class ShortcutKeySet : IEquatable<ShortcutKeySet>
{
    public HashSet<KeyModifiers> Modifiers { get; set; } = new();
    public Key NonModifierKey { get; set; } = Key.None;

    public ShortcutKeySet(HashSet<KeyModifiers> modifiers, Key key)
    {
        this.Modifiers = modifiers;
        this.NonModifierKey = key;
    }

    public ShortcutKeySet()
    {
        
    }

    public ShortcutKeySet(string shortcutsAsString)
    {
        HashSet<KeyModifiers> validModifiersToCheck = new()
        {
            KeyModifiers.Windows,
            KeyModifiers.Shift,
            KeyModifiers.Control,
            KeyModifiers.Alt,
        };

        foreach (KeyModifiers modifier in validModifiersToCheck)
            if (shortcutsAsString.Contains(modifier.ToString(), StringComparison.CurrentCultureIgnoreCase))
                Modifiers.Add(modifier);
        
        var splitUpString = shortcutsAsString.Split('+');
        string? keyString = splitUpString.LastOrDefault();

        if (Enum.TryParse(keyString, out Key parsedKey))
            NonModifierKey = parsedKey;
    }

    public override string ToString()
    {
        List<string> keyStrings = new();

        foreach (var key in Modifiers)
            keyStrings.Add(key.ToString());

        keyStrings.Add(NonModifierKey.ToString());

        return string.Join('+', keyStrings);
    }

    public bool Equals(HotKeyEventArgs e)
    {
        if (!Enum.TryParse(e.Key.ToString(), out Key pressedKey))
            return false;

        if (pressedKey != NonModifierKey)
            return false;

        if (e.Modifiers != Modifiers.Aggregate((x,y) => x | y))
            return false;

        return true;
    }

    public bool Equals(ShortcutKeySet? other)
    {
        if (other is null) 
            return false;

        if (string.Equals(other.ToString(), ToString(), StringComparison.InvariantCultureIgnoreCase))
            return true;

        return false;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        return Equals(obj as ShortcutKeySet);
    }
}