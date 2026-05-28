using System.Globalization;
using System.Windows.Data;

namespace VSLoader.Converters;

public sealed class BusyCursorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? System.Windows.Input.Cursors.Wait : System.Windows.Input.Cursors.Arrow;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
