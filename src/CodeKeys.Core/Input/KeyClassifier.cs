namespace CodeKeys.Core.Input;

/// <summary>The broad category of a key press — enough to compute typing stats without ever knowing the character.</summary>
public enum KeyKind
{
    Letter,
    Digit,
    Space,
    Enter,
    Backspace,
    Punctuation,
    Modifier,
    Other
}

/// <summary>
/// Classifies a virtual-key code into a <see cref="KeyKind"/>. This is the ONLY thing the
/// signal capture ever learns about a key — never the actual character — so typed text is
/// never reconstructable (privacy).
/// </summary>
public static class KeyClassifier
{
    public static KeyKind Classify(int vk)
    {
        if (VirtualKey.IsPureModifier(vk)) return KeyKind.Modifier;

        return vk switch
        {
            VirtualKey.Space => KeyKind.Space,
            VirtualKey.Enter => KeyKind.Enter,
            VirtualKey.Back => KeyKind.Backspace,
            >= 'A' and <= 'Z' => KeyKind.Letter,
            >= '0' and <= '9' => KeyKind.Digit,
            >= 0x60 and <= 0x69 => KeyKind.Digit,        // numpad 0-9
            >= 0xBA and <= 0xC0 => KeyKind.Punctuation,  // ; = , - . / `
            >= 0xDB and <= 0xDF => KeyKind.Punctuation,  // [ \ ] '
            _ => KeyKind.Other
        };
    }
}
