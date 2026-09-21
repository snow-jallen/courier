using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Courier.Tests;

/// <summary>Builds a PDF laid out the way LCR lays out the Single Adult Members
/// section of the Organizations and Callings report, so the reader can be tested on
/// what actually makes that report hard without putting a real directory in the
/// repository.
///
/// Every feature the parser depends on is here: the heading repeated on both pages,
/// the stake's callings table printed above the members table on page 1, a name
/// wrapped over two lines and centred on its row, an Age column with a heading and no
/// values, a Gender column with nothing behind it, and the page's own furniture.</summary>
internal static class SyntheticCallingsReport
{
    private const double Size = 8.0;

    // The heading positions a real export uses, and the data positions, which sit a
    // point and a half to their right.
    private const double HName = 54.1, HGender = 152.4, HAge = 215.6, HBirthday = 240.6,
                         HPhone = 282.6, HEmail = 343.2, HUnit = 480.3;
    private const double XName = 55.6, XGender = 153.9, XBirthday = 242.1,
                         XPhone = 284.1, XEmail = 344.7, XUnit = 481.8;

    /// <summary>Consecutive rows, 1.68x the text size apart.</summary>
    private const double Step = 13.4;

    /// <summary>Half the gap between the two lines of a wrapped name, which sit either
    /// side of the row's own baseline.</summary>
    private const double Wrap = 4.5;

    public static byte[] Build()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var one = builder.AddPage(612, 792);
        var two = builder.AddPage(612, 792);

        void Put(PdfPageBuilder page, string text, double x, double y)
        {
            if (text.Length > 0) page.AddText(text, Size, new PdfPoint(x, y), font);
        }

        void Heading(PdfPageBuilder page, double y)
        {
            Put(page, "Name", HName, y);
            Put(page, "Gender", HGender, y);
            Put(page, "Age", HAge, y);
            Put(page, "Birth Date", HBirthday, y);
            Put(page, "Phone Number", HPhone, y);
            Put(page, "Email", HEmail, y);
            Put(page, "Current Unit", HUnit, y);
        }

        void Person(PdfPageBuilder page, double y,
                    string name, string gender, string birthday, string phone, string email, string unit)
        {
            Put(page, name, XName, y);
            Put(page, gender, XGender, y);
            Put(page, birthday, XBirthday, y);
            Put(page, phone, XPhone, y);
            Put(page, email, XEmail, y);
            Put(page, unit, XUnit, y);
        }

        void Footer(PdfPageBuilder page, string number)
        {
            Put(page, "21 Sep 2026", 28.3, 19.2);
            Put(page, "For Church Use Only © 2026 by Intellectual Reserve, Inc. All rights reserved.", 153.1, 19.2);
            Put(page, number, 578.6, 19.2);
        }

        // ---- page 1 ----
        Put(one, "Organizations and Callings", 28.3, 782.9);
        Put(one, "Test Utah Stake (000000)", 476.1, 782.9);
        Put(one, "Single Adult", 34.3, 730.8);

        // The callings table. Its own Name and Current Unit headings sit at quite
        // different positions from the members table's, which is what breaks any
        // detection that merges the headings found across a whole page.
        Put(one, "Calling", 35.1, 713.6); Put(one, "Name", 225.6, 713.6);
        Put(one, "Sustained", 321.7, 713.6); Put(one, "Set Apart", 408.5, 713.6);
        Put(one, "Current Unit", 483.5, 713.6);
        Put(one, "Stake Single Adult Adviser", 36.6, 694.9);
        Put(one, "Calling Vacant", 227.1, 694.9);
        Put(one, "Stake Single Adult Representative", 36.6, 681.5);
        Put(one, "Pilkington, Hattie", 227.1, 681.5);
        Put(one, "19 Aug 2026", 323.2, 681.5);
        Put(one, "Manti 2nd Ward", 485.0, 681.5);
        Put(one, "Count: 2", 34.3, 663.8);

        Put(one, "Single Adult Members", 34.3, 643.9);
        Heading(one, 626.7);

        const double first = 608.0;
        Person(one, first,
            "Ashdown, Marigold", "F", "17 Jan", "(435) 555-0100", "m.ashdown@example.com", "Manti 2nd Ward");

        // A seven-digit phone and no e-mail, as the report prints some.
        Person(one, first - Step,
            "Quilley, Barnaby", "M", "6 May", "555-0127", "", "Sterling Ward");

        // No phone at all.
        Person(one, first - Step * 2,
            "Crowther, Dell", "F", "30 Jul", "", "d.crowther@example.com", "Manti 4th Ward");

        // A name too long for its column. Its two lines sit either side of the row,
        // so neither of them lines up with any other cell.
        var wrapped = first - Step * 3 - Wrap;
        Put(one, "Featherstonehaugh,", XName, wrapped + Wrap);
        Person(one, wrapped,
            "", "F", "3 Jul", "(435) 555-0142", "w.feather@example.com", "Manti 10th Ward");
        Put(one, "Wilhelmina", XName, wrapped - Wrap);

        Person(one, wrapped - Wrap - Step,
            "Zelmore, Briony", "F", "20 Aug", "(801) 555-0155", "b.zelmore@example.com", "Manti 5th Ward");

        Footer(one, "1");

        // ---- page 2: the heading repeats, and there is no callings table ----
        Put(two, "Organizations and Callings", 28.3, 782.9);
        Put(two, "Test Utah Stake (000000)", 476.1, 782.9);
        Heading(two, 762.0);

        Person(two, 743.3,
            "Wraithwell, Mordecai", "M", "18 Sep", "(435) 555-0170", "m.wraithwell@example.com", "Manti 9th Ward");
        Person(two, 743.3 - Step,
            "Yelverton, Katriona", "F", "6 Aug", "(435) 555-0184", "", "Manti 1st Ward");

        Put(two, "Count: 7", 34.3, 710.0);
        Footer(two, "2");

        return builder.Build();
    }
}
