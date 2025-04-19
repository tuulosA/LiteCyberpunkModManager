using System;
using System.Globalization;
using System.Windows.Data;

namespace CyberpunkModManager.Views
{
    public class ModNameColumnWidthConverter : IValueConverter
    {
        // Subtract width taken by other columns and scrollbar (adjust as needed)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double totalWidth)
            {
                double otherColumns = 180 + 140 + 35; // Status + FilesDownloaded + scrollbar margin
                return Math.Max(100, totalWidth - otherColumns);
            }

            return 300; // Fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
