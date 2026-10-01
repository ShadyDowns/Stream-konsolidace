using System.Globalization;
using System.Windows.Data;

namespace StreamKartoteka.Converters;

public sealed class HandledOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? 0.42d : 1d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

