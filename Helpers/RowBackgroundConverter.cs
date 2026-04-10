using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Blood_Alcohol.Helpers
{
    public class RowBorderThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                // 每10行增加底部边框
                return (index + 1) % 10 == 0 ? new Thickness(0, 0, 0, 2) : new Thickness(0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}