namespace Tally.Api.Features.Foods;

public static class Barcodes
{
    /// <summary>
    /// Normalises a scanned or typed code: digits only, UPC-E expanded to UPC-A,
    /// and UPC-A (12 digits) stored as EAN-13 with a leading zero, which is how
    /// Open Food Facts keys products. Returns null if it can't be a retail barcode.
    /// </summary>
    public static string? Normalize(string? raw, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());

        if (format is "upc_e" or "UPC_E" && digits.Length is 6 or 7 or 8)
            digits = ExpandUpcE(digits);

        return digits.Length switch
        {
            8 or 13 => digits,
            12 => "0" + digits,
            14 when digits[0] == '0' => digits[1..],
            _ => null,
        };
    }

    /// <summary>Expands a UPC-E code (6, 7 or 8 digits) to 12-digit UPC-A.</summary>
    public static string ExpandUpcE(string upcE)
    {
        var numberSystem = upcE.Length >= 7 ? upcE[0] : '0';
        var body = upcE.Length switch { 6 => upcE, 7 => upcE[1..7], _ => upcE[1..7] };
        var check = upcE.Length == 8 ? upcE[7].ToString() : "";

        var manufacturerAndProduct = body[5] switch
        {
            '0' or '1' or '2' => $"{body[..2]}{body[5]}0000{body[2..5]}",
            '3' => $"{body[..3]}00000{body[3..5]}",
            '4' => $"{body[..4]}00000{body[4]}",
            _ => $"{body[..5]}0000{body[5]}",
        };
        var withoutCheck = numberSystem + manufacturerAndProduct;
        return withoutCheck + (check.Length == 1 ? check : CheckDigit(withoutCheck).ToString());
    }

    private static int CheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
            sum += (digits[i] - '0') * (i % 2 == 0 ? 3 : 1);
        return (10 - sum % 10) % 10;
    }
}
