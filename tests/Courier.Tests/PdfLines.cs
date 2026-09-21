using Courier.Core.Import;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Courier.Tests;

/// <summary>Writes words at given positions into a one-page PDF and reads them back
/// as lines. Tests that need geometry get real glyph advance boxes this way, rather
/// than a hand-made stand-in that can agree with a broken reader.</summary>
internal static class PdfLines
{
    public static IReadOnlyList<TextLine> Of(double size, params (double X, double Y, string Text)[] words)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(612, 792);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var (x, y, text) in words)
            if (text.Length > 0) page.AddText(text, size, new PdfPoint(x, y), font);

        using var document = PdfDocument.Open(builder.Build());
        return PdfTableReader.ReadLines(document.GetPage(1));
    }
}
