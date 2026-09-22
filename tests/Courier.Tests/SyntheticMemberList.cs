using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Courier.Tests;

/// <summary>Builds a PDF laid out the way LCR lays out the Member List report, so the
/// reader can be tested on what makes that report hard without putting a real
/// directory in the repository.
///
/// Every number here is measured from a real export rather than chosen to contrast:
/// 9.0pt text, rows 19.0pt apart, the lines of a wrapped cell 6.1pt either side of the
/// row's own baseline, and the first row 22.0pt below the heading. A bad edit to
/// RowBreakFactor therefore shows up here with no real export on hand.
///
/// Every feature the parser depends on is here too: the report's title and the unit it
/// covers printed above the table on page 1 and nowhere else, the heading repeated on
/// page 2, a name wrapped over two lines, an e-mail wrapped over two, a Gender column
/// with nothing behind it, the full birth date and age the report prints on a few rows
/// and not the rest, and the page's own furniture.</summary>
internal static class SyntheticMemberList
{
    private const double Size = 9.0;

    /// <summary>Heading and data share these x positions exactly — unlike the
    /// Organizations and Callings report, whose data sits 1.5pt right of its
    /// headings.</summary>
    private const double XName = 41.8, XGender = 153.9, XAge = 225.2, XBirthday = 274.7,
                         XPhone = 346.0, XEmail = 430.9;

    /// <summary>Consecutive rows, 2.11x the text size apart.</summary>
    private const double Step = 19.01;

    /// <summary>How far the lines of a wrapped cell sit either side of the row's own
    /// baseline. 0.68x the text size, so the threshold has to fall between this and
    /// <see cref="Step"/>.</summary>
    private const double Wrap = 6.13;

    /// <summary>The gap between the heading and the first row beneath it, which is
    /// wider than the gap between two rows.</summary>
    private const double UnderHeading = 22.01;

    public static byte[] Build()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var one = builder.AddPage(612, 792);
        var two = builder.AddPage(612, 792);

        void Put(PdfPageBuilder page, string text, double x, double y, double size = Size)
        {
            if (text.Length > 0) page.AddText(text, size, new PdfPoint(x, y), font);
        }

        void Heading(PdfPageBuilder page, double y)
        {
            Put(page, "Name", XName, y);
            Put(page, "Gender", XGender, y);
            Put(page, "Age", XAge, y);
            Put(page, "Birth Date", XBirthday, y);
            Put(page, "Phone Number", XPhone, y);
            Put(page, "Email", XEmail, y);
        }

        void Person(PdfPageBuilder page, double y,
                    string name, string gender, string age, string birthday, string phone, string email)
        {
            Put(page, name, XName, y);
            Put(page, gender, XGender, y);
            Put(page, age, XAge, y);
            Put(page, birthday, XBirthday, y);
            Put(page, phone, XPhone, y);
            Put(page, email, XEmail, y);
        }

        void Footer(PdfPageBuilder page, string number)
        {
            Put(page, "21 Sep 2026", 28.3, 19.2);
            Put(page, "For Church Use Only © 2026 by Intellectual Reserve, Inc. All rights reserved.", 153.1, 19.2);
            Put(page, number, 578.6, 19.2);
        }

        // ---- page 1 ----
        // The title and the unit the report covers, above the table. The unit is the
        // only place the ward is named, and no column can reach it — which is exactly
        // why this format carries no Unit. Neither line may become a person.
        Put(one, "Member List", 34.3, 732.0, 24);
        Put(one, "Manti 3rd Ward (12564)", 34.3, 712.1, 12);

        const double heading = 681.0;
        Heading(one, heading);

        var first = heading - UnderHeading;
        Person(one, first,
            "Ashdown, Marigold", "F", "", "17 Jan", "(435) 555-0100", "m.ashdown@example.com");

        // The rows where the report prints a full birth date, and an age beside it.
        Person(one, first - Step,
            "Quilley, Barnaby", "M", "44", "9 Feb 1982", "(435) 555-0127", "b.quilley@example.com");

        // No e-mail, and a seven-digit phone, as the report prints some.
        Person(one, first - Step * 2,
            "Crowther, Dell", "F", "", "30 Jul", "555-0133", "");

        // No phone at all.
        Person(one, first - Step * 3,
            "Winslade, Verity", "F", "", "3 Jul", "", "v.winslade@example.com");

        // A name too long for its column. Its two lines sit either side of the row, so
        // neither of them lines up with any other cell.
        var wrapped = first - Step * 4 - Wrap;
        Put(one, "Featherstonehaugh,", XName, wrapped + Wrap);
        Person(one, wrapped, "", "F", "", "20 Aug", "(801) 555-0155", "w.feather@example.com");
        Put(one, "Wilhelmina", XName, wrapped - Wrap);

        Footer(one, "1");

        // ---- page 2: the heading repeats; the title and the unit do not ----
        const double second = 753.3;
        Heading(two, second);

        Person(two, second - UnderHeading,
            "Wraithwell, Mordecai", "M", "", "18 Sep", "(435) 555-0170", "m.wraithwell@example.com");

        // An e-mail too long for its column, wrapped the same way a name is.
        var longEmail = second - UnderHeading - Step - Wrap;
        Put(two, "k.yelverton@averylongdomainname.", XEmail, longEmail + Wrap);
        Person(two, longEmail, "Yelverton, Katriona", "F", "18", "12 Nov 2008", "(435) 555-0184", "");
        Put(two, "example", XEmail, longEmail - Wrap);

        Put(two, "Count: 7", 34.3, longEmail - Wrap - Step * 2);
        Footer(two, "2");

        return builder.Build();
    }
}
