using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Text_Grab.Properties;
using Text_Grab.Services;
using Text_Grab.Utilities;

namespace Tests;

public class MotionPreferencesTests
{
    [WpfTheory]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public void ReduceMotion_UsesTheMoreRestrictivePreference(
        bool windowsAnimationsEnabled, bool appReduceMotion, bool expected)
    {
        Settings settings = new() { ReduceMotion = appReduceMotion };
        using MotionPreferences motion = new(settings, Dispatcher.CurrentDispatcher, () => windowsAnimationsEnabled);

        Assert.Equal(expected, motion.ReduceMotion);
    }

    [WpfFact]
    public async Task SystemChange_IsReadBeforeNotificationAndNotifiesBindingOnDispatcher()
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        Settings settings = new() { ReduceMotion = false };
        bool windowsAnimationsEnabled = true;
        using MotionPreferences motion = new(settings, dispatcher, () => windowsAnimationsEnabled);
        CheckBox target = new();
        target.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(MotionPreferences.ReduceMotion))
        {
            Source = motion,
            Mode = BindingMode.OneWay
        });

        List<bool> notifications = [];
        motion.PropertyChanged += (_, e) =>
        {
            Assert.True(dispatcher.CheckAccess());
            Assert.Equal(nameof(MotionPreferences.ReduceMotion), e.PropertyName);
            notifications.Add(motion.ReduceMotion);
        };

        Assert.False(target.IsChecked);
        windowsAnimationsEnabled = false;
        Assert.True(motion.ReduceMotion);
        Assert.False(target.IsChecked);

        await Task.Run(motion.Refresh, TestContext.Current.CancellationToken);
        await dispatcher.InvokeAsync(() => Assert.True(target.IsChecked));
        settings.ReduceMotion = true;

        Assert.Equal([true], notifications);
    }

    [WpfFact]
    public void AppSettingChange_NotifiesOnlyWhenEffectiveValueChanges()
    {
        Settings settings = new() { ReduceMotion = false };
        using MotionPreferences motion = new(settings, Dispatcher.CurrentDispatcher, () => true);
        List<bool> notifications = [];
        motion.PropertyChanged += (_, _) => notifications.Add(motion.ReduceMotion);

        settings.ReduceMotion = true;
        settings.ReduceMotion = true;
        settings.ReduceMotion = false;

        Assert.Equal([true, false], notifications);
    }

    [WpfFact]
    public void AnimateOrSet_WhenReduced_AppliesFinalValueAndPreservesBinding()
    {
        Settings settings = new() { ReduceMotion = false };
        bool windowsAnimationsEnabled = true;
        using MotionPreferences motion = new(settings, Dispatcher.CurrentDispatcher, () => windowsAnimationsEnabled);
        Slider source = new() { Value = 3 };
        TranslateTransform target = new();
        BindingOperations.SetBinding(target, TranslateTransform.YProperty, new Binding(nameof(Slider.Value)) { Source = source });
        target.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, TimeSpan.FromSeconds(1)));
        Assert.True(target.HasAnimatedProperties);

        windowsAnimationsEnabled = false;
        motion.AnimateOrSet(target, TranslateTransform.YProperty, new DoubleAnimation(), 6d);

        Assert.False(target.HasAnimatedProperties);
        Assert.Equal(6d, target.Y);
        source.Value = 7;
        Assert.Equal(7d, target.Y);
    }

    [WpfFact]
    public void AnimateOrSet_WhenNotReduced_LeavesTheBaseValueUnchanged()
    {
        Settings settings = new() { ReduceMotion = false };
        using MotionPreferences motion = new(settings, Dispatcher.CurrentDispatcher, () => true);
        Border target = new() { Opacity = 0.2 };
        DoubleAnimation animation = new(0.9, TimeSpan.FromSeconds(1));

        motion.AnimateOrSet(target, UIElement.OpacityProperty, animation, 0.9);

        Assert.True(target.HasAnimatedProperties);
        Assert.Equal(0.2, target.GetAnimationBaseValue(UIElement.OpacityProperty));
    }
}
