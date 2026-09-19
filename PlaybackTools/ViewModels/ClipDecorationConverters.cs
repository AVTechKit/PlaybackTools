using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PlaybackTools.ViewModels
{
    /// <summary>Shows an element only when EndAction equals the ConverterParameter -
    /// used to pick exactly one End Action icon among several stacked ones on each clip
    /// button, so only the icon matching that clip's actual End Action ever renders.</summary>
    public class EndActionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is EndAction ea && parameter is EndAction target && ea == target) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Shows an element only when a Mark In/Out time string isn't the untouched
    /// default "00:00:00" - drives the Mark In/Out flag decorations' visibility.</summary>
    public class NonDefaultTimeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is string s && s != "00:00:00") ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Shortens a stored "hh:mm:ss" Mark In/Out value to a compact label for the
    /// tiny flag decorations - "3:00" instead of "00:03:00", dropping the hours segment
    /// entirely for the vast majority of clips that don't need it.</summary>
    public class TimeToShortLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string s || !TimeSpan.TryParseExact(s, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var ts))
                return "";
            return ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}