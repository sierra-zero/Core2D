﻿// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Test.Converters
{
    public class FlagsEnumConverter : IValueConverter
    {
        private int _value;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int mask = (int)parameter;
            _value = (int)value;
            return (mask == 0 && _value == 0) ? true : ((mask & _value) != 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            _value ^= (int)parameter;
            return Enum.Parse(targetType, _value.ToString());
        }
    }
}
