using SuchByte.MacroDeck.ActionButton;
using Color = System.Drawing.Color;

namespace SuchByte.MacroDeck.Utils;

public static class ColorExtensions
{
    public static string ToHex(this Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}