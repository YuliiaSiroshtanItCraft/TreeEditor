namespace TreeEditor.Core.Constants;

public static class TreeConstants
{
    public const int MaxValueLength = 200;

    public static string? ValidateValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Value must not be empty.";
        if (value.Length > MaxValueLength)
            return $"Value must be at most {MaxValueLength} characters.";
        return null;
    }
}
