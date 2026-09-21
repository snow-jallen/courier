using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Courier.Tests;

/// <summary>Builds a PDF laid out the way LCR lays out the Single Adults report, so
/// the reader can be tested on the geometry that actually makes it hard — cells
/// centred vertically on the row rather than aligned to its top — without putting a
/// real directory in the repository.</summary>
internal static class SyntheticReport
{
    private const double Size = 6.5;

    // The column positions the real export uses.
    private const double XName = 41, XEmail = 114, XPhone = 279, XUnit = 355,
                         XAge = 402, XBirthday = 446, XAddress = 515;

    /// <summary>One line of the 6.4pt baseline grid.</summary>
    private const double Step = 6.4;

    public static byte[] Build()
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(612, 792);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        void Put(string text, double x, double y)
        {
            if (text.Length > 0) page.AddText(text, Size, new PdfPoint(x, y), font);
        }

        // Headings, wrapped across three lines exactly as the report wraps them.
        Put("Preferred", XName, 622); Put("Individual", XPhone, 622);
        Put("Birthday", XBirthday, 622); Put("Address -", XAddress, 622);

        Put("Individual E-mail", XEmail, 615); Put("Unit", XUnit, 615); Put("Age", XAge, 615);

        Put("Name", XName, 608); Put("Phone", XPhone, 608);
        Put("(1 Jan)", XBirthday, 608); Put("Street 1", XAddress, 608);

        // A person whose name, unit and address all wrap. The unit's three lines sit
        // one step either side of the centre; the name's two lines sit half a step
        // either side, so no cell's first line lines up with any other's.
        const double centre = 572;
        Put("Manti", XUnit, centre + Step);
        Put("Ashby,", XName, centre + Step / 2); Put("812 North 700", XAddress, centre + Step / 2);
        Put("m.ashby@example.com", XEmail, centre);
        Put("(435) 555-0111", XPhone, centre);
        Put("2nd", XUnit, centre);
        Put("86", XAge, centre);
        Put("17 Jan", XBirthday, centre);
        Put("Miriam", XName, centre - Step / 2); Put("East", XAddress, centre - Step / 2);
        Put("Ward", XUnit, centre - Step);

        // A person who fits on one line, after the gap that separates rows.
        const double second = centre - Step - 23;
        Put("Quilley, Barnaby", XName, second);
        Put("(435) 555-0127", XPhone, second);
        Put("Sterling Ward", XUnit, second);
        Put("40", XAge, second);
        Put("6 May", XBirthday, second);
        Put("12 Main", XAddress, second);

        // A person with no e-mail and a seven-digit phone, as the report prints some.
        const double third = second - 23;
        Put("Crowther, Dell", XName, third);
        Put("555-0133", XPhone, third);
        Put("Manti 4th Ward", XUnit, third);
        Put("55", XAge, third);
        Put("30 Jul", XBirthday, third);

        // Page furniture that must not become a person.
        Put("Single Adults", 37, 728);
        Put("https://lcr.churchofjesuschrist.org/mlt/report", 37, 28);
        Put("Page 1 of 1", 533, 20);
        Put("Count: 3", 37, 40);

        return builder.Build();
    }
}
