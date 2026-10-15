using System.Text.RegularExpressions;

namespace Otopark.Services;

public static class Plates
{
    // Türk plaka biçimi: il kodu (01-81) + 1-3 harf + 2-4 rakam
    private static readonly Regex Rx = new(@"^(0[1-9]|[1-7][0-9]|8[01])[A-Z]{1,3}\d{2,4}$", RegexOptions.Compiled);

    public static string Normalize(string? p) =>
        new string((p ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static bool IsValid(string normalized) => Rx.IsMatch(normalized);
}
