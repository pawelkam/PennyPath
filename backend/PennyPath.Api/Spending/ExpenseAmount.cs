using System.Globalization;

namespace PennyPath.Api.Spending;

public static class ExpenseAmount
{
    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrEmpty(text) || text.Length > 64)
        {
            return false;
        }

        var separator = text.IndexOf('.');
        var integerLength = separator < 0 ? text.Length : separator;
        if (integerLength is < 1 or > 12 || separator == text.Length - 1)
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (index != separator && text[index] is not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        // Discard extra fractional digits as text, before any numeric conversion.
        var fraction = separator < 0 ? "00" : text[(separator + 1)..];
        fraction = fraction.Length < 2 ? fraction + "0" : fraction[..2];
        amount = decimal.Parse(text[..integerLength] + "." + fraction, CultureInfo.InvariantCulture);
        return amount > 0m;
    }

    public static string Format(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);
}
