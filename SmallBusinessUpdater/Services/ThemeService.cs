using System;
using System.Windows;
using System.Windows.Media;

namespace SmallBusinessUpdater.Services;

internal static class ThemeService
{
    private static bool initialized;

    public static void Apply(bool dark)
    {
        var resources = Application.Current.Resources;
        if (!initialized)
        {
            resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/SmallBusinessUpdater;component/SmallBusinessUpdater/Themes/Controls.xaml", UriKind.Relative)
            });
            initialized = true;
        }
        Set("WindowBrush", dark ? "#111827" : "#F6F7F9");
        Set("SurfaceBrush", dark ? "#1F2937" : "#FFFFFF");
        Set("TextBrush", dark ? "#F3F4F6" : "#111827");
        Set("MutedBrush", dark ? "#CBD5E1" : "#6B7280");
        Set("InputBrush", dark ? "#111827" : "#FFFFFF");
        Set("BorderBrush", dark ? "#64748B" : "#CBD5E1");
        Set("ButtonBrush", dark ? "#374151" : "#E5E7EB");
        Set("InfoBrush", dark ? "#172554" : "#EFF6FF");
        Set("InfoTextBrush", dark ? "#BFDBFE" : "#1E3A8A");
        Set("WarningBrush", dark ? "#431407" : "#FFF7ED");
        Set("WarningTextBrush", dark ? "#FED7AA" : "#9A3412");
        Set("SuccessBrush", dark ? "#052E22" : "#ECFDF5");
        Set("SuccessTextBrush", dark ? "#A7F3D0" : "#065F46");
        Set("ErrorBrush", dark ? "#450A0A" : "#FEF2F2");
        Set("ErrorTextBrush", dark ? "#FECACA" : "#991B1B");

        // Standard WPF editors and calendar popups also use system resource keys.
        resources[SystemColors.WindowBrushKey] = resources["InputBrush"];
        resources[SystemColors.WindowTextBrushKey] = resources["TextBrush"];
        resources[SystemColors.ControlBrushKey] = resources["SurfaceBrush"];
        resources[SystemColors.ControlTextBrushKey] = resources["TextBrush"];

        void Set(string key, string color) =>
            resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}

