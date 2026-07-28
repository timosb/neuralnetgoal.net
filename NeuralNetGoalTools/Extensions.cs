using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeuralNetGoalTools;

public static class Extensions
{
    public static long? ZeroIfNull(this long? value)
    {
        return value ?? 0;
    }

    public static int ZeroIfNull(this int? value)
    {
        return value ?? 0;
    }
    public static string DBNullIfNull(this int? value)
    {
        return value.HasValue ?value.ToString(): "NULL";
    }
    public static long? OneIfZero(this long? value)
    {
        return !value.HasValue || value.Value == 0 ? 1 : value;
    }

    public static decimal? RoundToDecimalPlaces(this decimal? value, int decimalPlaces)
    {
        return value.HasValue ? Math.Round(value.Value, decimalPlaces) : (decimal?)null;
    }

    public static double? RoundToDecimalPlaces(this double? value, int decimalPlaces)
    {
        return value.HasValue ? Math.Round(value.Value, decimalPlaces) : (double?)null;
    }
    public static double RoundToDecimalPlaces(this double value, int decimalPlaces)
    {
        return Math.Round(value, decimalPlaces);
    }
}


