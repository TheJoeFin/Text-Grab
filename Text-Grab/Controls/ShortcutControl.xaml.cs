using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Text_Grab.Models;
using Text_Grab.Utilities;

namespace Text_Grab.Controls;

/// <summary>
/// Interaction logic for ShortcutControl.xaml
/// </summary>
public partial class ShortcutControl : UserControl
{
    private readonly Brush BadBrush = new SolidColorBrush(Colors.Red);
    private readonly Brush GoodBrush = new SolidColorBrush(Colors.Transparent);

    private bool HasErrorWithKeySet { get; set; } = false;
    public bool HasConflictingError { get; set; } = false;

    public bool IsShortcutEnabled
    {
        get { return (bool)GetValue(IsShortcutEnabledProperty); }
        set { SetValue(IsShortcutEnabledProperty, value); }
    }

    public static readonly DependencyProperty IsShortcutEnabledProperty =
        DependencyProperty.Register("IsShortcutEnabled", typeof(bool), typeof(ShortcutControl), new PropertyMetadata(false));

    public string ShortcutName
    {
        get { return (string)GetValue(ShortcutNameProperty); }
        set { SetValue(ShortcutNameProperty, value); }
    }

    public static readonly DependencyProperty ShortcutNameProperty =
        DependencyProperty.Register("ShortcutName", typeof(string), typeof(ShortcutControl), new PropertyMetadata("shortcutName"));

    bool isRecording = false;

    string previousSequence = string.Empty;
    public bool HasModifier { get; set; } = false;
    public bool HasLetter { get; set; } = false;

    private ShortcutKeySet _keySet = new();

    public ShortcutKeySet KeySet
    {
        get => _keySet;
        set
        {
            if (value == _keySet)
                return;

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

            IsShortcutEnabled = _keySet.IsEnabled;

            if (IsShortcutEnabled)
                ButtonsPanel.Visibility = Visibility.Visible;
            else
                ButtonsPanel.Visibility = Visibility.Collapsed;

            ShortcutName = _keySet.Name;

            KeyLetterTextBlock.Text = _keySet.NonModifierKey.ToString();
            KeySetChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? KeySetChanged;
    public event EventHandler? RecordingStarted;

    public ShortcutControl()
    {
        InitializeComponent();
    }

    public void GoIntoErrorMode(string errorMessage = "")
    {
        BorderBrush = BadBrush;

        if (!string.IsNullOrEmpty(errorMessage))
            ErrorText.Text = errorMessage;

        ErrorText.Visibility = Visibility.Visible;
    }

    public void GoIntoNormalMode()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        ErrorText.Text = string.Empty;
        BorderBrush = GoodBrush;
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

        HashSet<KeyModifiers> modifierKeys = [];

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
        {
            HasErrorWithKeySet = false;
            ShortcutKeySet newKeySet = new()
            {
                Modifiers = modifierKeys,
                NonModifierKey = justLetterKeys.FirstOrDefault(),
                IsEnabled = IsShortcutEnabled,
                Name = _keySet.Name,
                Action = _keySet.Action,
            };
            KeySet = newKeySet;
        }
        else
        {
            HasErrorWithKeySet = true;
            ErrorText.Text = "Need to have at least one modifier and one non-modifier key";
        }

        if (string.IsNullOrEmpty(currentSequence) || currentSequence.Equals(previousSequence))
            return;

        KeyLetterTextBlock.Text = justLetterKeys.FirstOrDefault().ToString();
        previousSequence = currentSequence;
    }

    private void ShortcutControl_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (!isRecording)
            return;

        CheckForErrors();
    }

    public void CheckForErrors()
    {
        if (HasErrorWithKeySet || HasConflictingError)
            GoIntoErrorMode();
        else
            GoIntoNormalMode();
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
        byte[] keyboardState = new byte[256];
        NativeMethods.GetKeyboardState(keyboardState);

        HashSet<Key> downKeys = [];
        for (int index = 0; index < DistinctVirtualKeys.Length; index++)
        {
            byte virtualKey = DistinctVirtualKeys[index];
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
            RecordingStarted?.Invoke(this, e);
    }

    public void StopRecording(object sender)
    {
        RecordingToggleButton.IsChecked = false;
        isRecording = false;
    }

    private void IsEnabledToggleSwitch_Click(object sender, RoutedEventArgs e)
    {
        _keySet.IsEnabled = IsShortcutEnabled;

        if (IsShortcutEnabled)
            ButtonsPanel.Visibility = Visibility.Visible;
        else
            ButtonsPanel.Visibility = Visibility.Collapsed;

        KeySetChanged?.Invoke(this, EventArgs.Empty);
    }
}
