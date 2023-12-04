using System.Collections.Generic;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Linq;

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
    bool HasModifier = false;
    bool hasLetter = false;

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

        hasLetter = justLetterKeys.Count != 0;
        HasModifier = containsWin || containsShift || containsCtrl || containsAlt;

        if (hasLetter)
            KeyKey.Visibility = Visibility.Visible;
        else
            KeyKey.Visibility = Visibility.Collapsed;

        if (containsWin)
            WinKey.Visibility = Visibility.Visible;
        else
            WinKey.Visibility = Visibility.Collapsed;

        if (containsShift)
            ShiftKey.Visibility = Visibility.Visible;
        else
            ShiftKey.Visibility = Visibility.Collapsed;

        if (containsCtrl)
            CtrlKey.Visibility = Visibility.Visible;
        else
            CtrlKey.Visibility = Visibility.Collapsed;

        if (containsAlt)
            AltKey.Visibility = Visibility.Visible;
        else
            AltKey.Visibility = Visibility.Collapsed;

        List<string> keyStrings = [];
        foreach (Key key in justLetterKeys)
            keyStrings.Add(key.ToString());

        string currentSequence = string.Join('+', keyStrings);

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
