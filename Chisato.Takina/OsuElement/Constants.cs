using System.Globalization;

namespace Chisato.Takina.OsuElement;

internal static class Constants
{
    //nfi so parsing works on all cultures
    public static readonly NumberFormatInfo NumberFormat = new CultureInfo(@"en-US", false).NumberFormat;
}