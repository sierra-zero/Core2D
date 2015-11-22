﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Core2D.Wpf.Converters
{
    /// <summary>
    /// Provides a way to apply custom logic in a <see cref="MultiBinding"/>.
    /// </summary>
    public class ArgbColorToBrushMultiConverter : IMultiValueConverter
    {
        /// <summary>
        /// Converts source values to a value for the binding target.
        /// </summary>
        /// <param name="values">The array of values that the source bindings in the <see cref="MultiBinding"/> produces.</param>
        /// <param name="targetType">The type of the binding target property.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture">The culture to use in the converter.</param>
        /// <returns>A converted value.</returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Length == 4)
            {
                var brush = new SolidColorBrush(
                    Color.FromArgb(
                        (byte)values[0],
                        (byte)values[1],
                        (byte)values[2],
                        (byte)values[3]));
                brush.Freeze();
                return brush;
            }
            return null;
        }

        /// <summary>
        ///  Converts a binding target value to the source binding values.
        /// </summary>
        /// <param name="value">The value that the binding target produces.</param>
        /// <param name="targetTypes">The array of types to convert to.</param>
        /// <param name="parameter">The converter parameter to use.</param>
        /// <param name="culture"> The culture to use in the converter.</param>
        /// <returns> An array of values that have been converted from the target value back to the source values.</returns>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
