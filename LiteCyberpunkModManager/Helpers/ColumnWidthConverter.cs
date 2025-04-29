using System.Globalization;
using System.Windows.Data;

namespace LiteCyberpunkModManager.Views
{
    public class ColumnWidthConverter : IValueConverter
    {
        // subtract width taken by other columns and scrollbar
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double totalWidth)
            {
                double otherColumns = 180 + 140 + 35; // status + FilesDownloaded + scrollbar margin
                return Math.Max(100, totalWidth - otherColumns);
            }

            return 300;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
