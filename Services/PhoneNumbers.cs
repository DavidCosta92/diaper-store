namespace panialera.Services;

// Argentine phone numbers as people write them ("+54 9 264 412-3456", "0264 15 412 3456",
// "15 412-3456"). The stored number is never changed: this only completes it to compare two
// numbers and to build the WhatsApp link. A number without area code gets the shop's one.
// The phone app has the same rules (Services/PhoneNumbers.cs there).
public static class PhoneNumbers
{
    public const string DefaultAreaCode = "264";

    // Area code plus subscriber: 10 digits in Argentina.
    private const int NationalLength = 10;

    // The 10-digit national number, or null when it cannot be completed.
    public static string? National(string? phone, string areaCode = DefaultAreaCode)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        var international = phone.TrimStart().StartsWith('+') || digits.StartsWith("00");
        if (digits.StartsWith("00"))
            digits = digits[2..];

        if (international || (digits.StartsWith("54") && digits.Length >= 12))
        {
            if (!digits.StartsWith("54"))
                return null;
            digits = digits[2..];
            if (digits.StartsWith('9'))
                digits = digits[1..];
        }

        // National prefix.
        if (digits.StartsWith('0'))
            digits = digits[1..];

        // "15" after the area code marks a mobile in the local format: it is not dialed with +54 9.
        if (digits.Length == NationalLength + 2)
        {
            foreach (var areaLength in new[] { 2, 3, 4 })
            {
                if (digits.Substring(areaLength, 2) == "15")
                {
                    digits = digits.Remove(areaLength, 2);
                    break;
                }
            }
        }

        if (digits.Length == NationalLength)
            return digits;

        // Without area code: the shop's one ("15" first marks a mobile).
        var area = new string(areaCode.Where(char.IsDigit).ToArray()).TrimStart('0');
        if (area.Length == 0)
            return null;
        if (digits.StartsWith("15") && digits.Length - 2 + area.Length == NationalLength)
            digits = digits[2..];
        return digits.Length + area.Length == NationalLength ? area + digits : null;
    }

    // The number for a WhatsApp link (country code and the 9 of mobiles), or null.
    public static string? ForWhatsApp(string? phone, string areaCode = DefaultAreaCode) =>
        National(phone, areaCode) is { } national ? "549" + national : null;

    // Whether two written numbers are the same phone.
    public static bool Same(string? a, string? b, string areaCode = DefaultAreaCode) =>
        National(a, areaCode) is { } x && x == National(b, areaCode);
}
