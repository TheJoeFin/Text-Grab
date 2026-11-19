using System.Windows;
using System.Windows.Media.Animation;
using Text_Grab.Services;

namespace Text_Grab.Utilities;

internal static class MotionAnimation
{
    internal static void AnimateOrSet<T>(
        this MotionPreferences motion,
        T target,
        DependencyProperty property,
        AnimationTimeline animation,
        object? finalValue)
        where T : DependencyObject, IAnimatable
    {
        if (motion.ReduceMotion)
        {
            target.BeginAnimation(property, null);
            target.SetCurrentValue(property, finalValue);
            return;
        }

        target.BeginAnimation(property, animation);
    }
}
