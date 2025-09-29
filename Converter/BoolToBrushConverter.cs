using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Caro_game.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush? TrueBrush { get; set; }
        public Brush? FalseBrush { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var resources = Application.Current?.Resources;

            var winningBrush = TrueBrush ?? resources?["WinningCellBrush"] as Brush ?? Brushes.Gold;
            var regularBrush = FalseBrush ?? resources?["CellBackgroundBrush"] as Brush ?? new SolidColorBrush(Color.FromRgb(15, 23, 42));

            if (value is bool b && b)
            {
                return winningBrush;
            }

            return regularBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
