// Compile-check stub: only the TextMeshPro members the project uses, signatures copied from
// com.unity.ugui 2.0 Runtime/TMP (TMP_Text.cs, TextMeshProUGUI.cs, TMP_Settings.cs).
using UnityEngine;
using UnityEngine.UI;

namespace TMPro
{
    public enum TextAlignmentOptions { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight, Midline }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public enum FontStyles { Normal = 0x0, Bold = 0x1, Italic = 0x2, Underline = 0x4, LowerCase = 0x8, UpperCase = 0x10, SmallCaps = 0x20 }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3 }

    public class TMP_Asset : ScriptableObject { }
    public class TMP_FontAsset : TMP_Asset { }

    public class TMP_Settings : ScriptableObject
    {
        public static TMP_FontAsset defaultFontAsset => null;
        public static TMP_Settings instance => null;
    }

    public abstract class TMP_Text : MaskableGraphic
    {
        public virtual string text { get; set; }
        public TMP_FontAsset font { get; set; }
        public float fontSize { get; set; }
        public bool enableAutoSizing { get; set; }
        public float fontSizeMin { get; set; }
        public float fontSizeMax { get; set; }
        public FontStyles fontStyle { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public float characterSpacing { get; set; }
        public TextWrappingModes textWrappingMode { get; set; }
        public TextOverflowModes overflowMode { get; set; }
    }

    public class TextMeshProUGUI : TMP_Text { }
}
