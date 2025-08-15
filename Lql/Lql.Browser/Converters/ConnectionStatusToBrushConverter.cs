using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Lql.Browser.ViewModels;

namespace Lql.Browser.Converters;

public class ConnectionStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ConnectionStatus status)
        {
            var resourceKey = status switch
            {
                ConnectionStatus.Connected => "DeepForestBrush",
                ConnectionStatus.Error => "ErrorRedBrush",
                ConnectionStatus.Disconnected => "MediumGrayBrush",
                _ => "MediumGrayBrush",
            };

            if (Application.Current?.TryGetResource(resourceKey, null, out var brush) == true)
                return brush;
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotImplementedException();
}
