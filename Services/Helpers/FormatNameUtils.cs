public static class UserNameFormatter
{
    public static string FormatNameUtils(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Unknown";

        var name = raw.Split('#')[0];
        return char.ToUpper(name[0]) + name.Substring(1).ToLower();
    }
}
