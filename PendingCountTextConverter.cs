using System.Globalization;
using System.Windows.Data;

namespace StreamKartoteka.Converters;

public sealed class PendingCountTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value is int integer ? integer : 0;
        return count switch
        {
            0 => "vyřízeno",
            1 => "1 zpráva",
            >= 2 and <= 4 => $"{count} zprávy",
            _ => $"{count} zpráv"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

