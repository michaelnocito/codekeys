namespace CodeKeys.Core.Input;

/// <summary>
/// The default physical ordering of the main typing keys, low→high.
/// Reading bottom-row → number-row, left → right within each row, so the keyboard
/// becomes an instrument: notes rise as you move up and to the right. Pressing the
/// same key always plays the same note, so the same word plays the same phrase.
/// </summary>
public static class KeyboardLayout
{
    // Virtual-key codes for letters/digits equal their ASCII upper-case char.
    private static int C(char c) => char.ToUpperInvariant(c);

    /// <summary>Ordered low→high. Each entry is a virtual-key code.</summary>
    public static readonly IReadOnlyList<int> DefaultOrder = Build();

    private static int[] Build()
    {
        var rows = new[]
        {
            "ZXCVBNM",        // bottom row (letters only; punctuation omitted for a clean span)
            "ASDFGHJKL",      // home row
            "QWERTYUIOP",     // top row
            "1234567890"      // number row (highest)
        };

        var order = new List<int>();
        foreach (var row in rows)
            foreach (var ch in row)
                order.Add(C(ch));
        return order.ToArray();
    }
}
