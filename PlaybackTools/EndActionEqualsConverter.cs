using System;
using System.Globalization;
using System.Windows.Data;

namespace PlaybackTools.ViewModels
{
    public class EndActionEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value != null && value.Equals(parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}