using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VSLoader.Behaviors;

public static class ContextMenuInputBehavior
{
    public static readonly DependencyProperty SuppressRightClickActivationProperty =
        DependencyProperty.RegisterAttached(
            "SuppressRightClickActivation",
            typeof(bool),
            typeof(ContextMenuInputBehavior),
            new PropertyMetadata(false, OnSuppressRightClickActivationChanged));

    public static bool GetSuppressRightClickActivation(DependencyObject obj) =>
        (bool)obj.GetValue(SuppressRightClickActivationProperty);

    public static void SetSuppressRightClickActivation(DependencyObject obj, bool value) =>
        obj.SetValue(SuppressRightClickActivationProperty, value);

    private static void OnSuppressRightClickActivationChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ContextMenu menu)
        {
            return;
        }

        menu.PreviewMouseRightButtonDown -= SuppressRightClick;
        menu.PreviewMouseRightButtonUp -= SuppressRightClick;

        if ((bool)e.NewValue)
        {
            menu.PreviewMouseRightButtonDown += SuppressRightClick;
            menu.PreviewMouseRightButtonUp += SuppressRightClick;
        }
    }

    private static void SuppressRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
