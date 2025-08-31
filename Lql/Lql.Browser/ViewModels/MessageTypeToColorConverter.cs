using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Lql.Browser.ViewModels;

/// <summary>
/// Converter that maps MessageType to appropriate brush colors
/// </summary>
public class MessageTypeToColorConverter : IValueConverter
{
    public static readonly MessageTypeToColorConverter Instance = new();

    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Blue
    private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(156, 163, 175)); // Gray

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageType messageType)
        {
            return messageType switch
            {
                MessageType.Error => ErrorBrush,
                MessageType.Warning => WarningBrush,
                MessageType.Success => SuccessBrush,
                MessageType.Info => InfoBrush,
                _ => DefaultBrush,
            };
        }

        return DefaultBrush;
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotImplementedException();
}
