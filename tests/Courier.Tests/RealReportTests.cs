using System.Text.RegularExpressions;
using Courier.Core.Domain;
using Courier.Core.Import;
using Xunit.Abstractions;

namespace Courier.Tests;

/// <summary>Measures the parser against a real export. These are the numbers that
/// decide whether an import can be trusted, so they are asserted, not just printed.</summary>
public sealed class RealReportTests(ITestOutputHelper output)
{
    [RequiresRealReport]
    public void Reads_every_person_from_the_report()
    {
        var report = LcrReportParser.Parse(TestPaths.RealReport!);

        var phone = new Regex(@"^(\(\d{3}\)\s*)?\d{3}-\d{4}$");
        var email = new Regex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$");
        var birthday = new Regex(@"^\d{1,2} [A-Z][a-z]{2}$");
        var age = new Regex(@"^\d{1,3}$");

        int Bad(Func<LcrRow, string> field, Regex shape) =>
            report.Rows.Count(r => field(r).Length > 0 && !shape.IsMatch(field(r)));

        output.WriteLine($"pages       {report.PageCount}");
        output.WriteLine($"people      {report.Rows.Count}");
        output.WriteLine($"bad phone   {Bad(r => r.Phone, phone)}");
        output.WriteLine($"bad email   {Bad(r => r.Email, email)}");
        output.WriteLine($"bad bday    {Bad(r => r.Birthday, birthday)}");
        output.WriteLine($"bad age     {Bad(r => r.Age, age)}");
        output.WriteLine($"no email    {report.Rows.Count(r => r.Email.Length == 0)}");
        output.WriteLine($"no phone    {report.Rows.Count(r => r.Phone.Length == 0)}");
        foreach (var r in report.Rows.Where(r => r.Phone.Length > 0 && !phone.IsMatch(r.Phone)).Take(5))
            output.WriteLine($"  PHONE '{r.Phone}'");
        foreach (var r in report.Rows.Where(r => r.Email.Length > 0 && !email.IsMatch(r.Email)).Take(5))
            output.WriteLine($"  EMAIL '{r.Email}'");

        Assert.Equal(29, report.PageCount);

        // The report prints "Count: 427" in its own footer. Reading exactly that many
        // people back is the strongest check available that no row was dropped.
        Assert.Equal(427, report.Rows.Count);
        Assert.Equal(0, Bad(r => r.Phone, phone));
        Assert.Equal(0, Bad(r => r.Age, age));
        Assert.Equal(0, Bad(r => r.Birthday, birthday));
        Assert.Equal(0, Bad(r => r.Email, email));
    }

    [RequiresRealReport]
    public void Every_unit_snaps_onto_a_known_ward()
    {
        var report = LcrReportParser.Parse(TestPaths.RealReport!);
        var unsnapped = report.Rows.Where(r => Wards.Snap(r.Unit) is null).ToList();

        foreach (var r in unsnapped.Take(10)) output.WriteLine($"  UNIT '{r.Unit}'");
        output.WriteLine($"distinct raw units: {report.Rows.Select(r => r.Unit).Distinct().Count()}");

        // Assert.Empty(unsnapped) would dump every unsnapped LcrRow's name, address,
        // e-mail and phone on failure; Assert.True with a count keeps a failure to a
        // number, same as the Organizations and Callings version of this test.
        Assert.True(unsnapped.Count == 0, $"{unsnapped.Count} rows had a unit that did not snap to a known ward");
    }

    [RequiresRealReport]
    public void Every_person_has_a_surname_and_a_given_name()
    {
        var report = LcrReportParser.Parse(TestPaths.RealReport!);
        var malformed = report.Rows
            .Where(r => r.Name.Split(',', StringSplitOptions.TrimEntries).Length != 2
                     || r.Name.Split(',', StringSplitOptions.TrimEntries).Any(p => p.Length == 0))
            .ToList();
        // The name itself is the thing under test here, so it cannot be printed
        // without printing a person; the part count is enough to see the shape of
        // the failure without it.
        foreach (var r in malformed.Take(10))
            output.WriteLine($"  NAME shape: {r.Name.Split(',', StringSplitOptions.TrimEntries).Length} part(s)");
        Assert.True(malformed.Count == 0, $"{malformed.Count} names were not 'Surname, Given'");
    }
}

public sealed class ColumnDetectionTests(ITestOutputHelper output)
{
    [RequiresRealReport]
    public void Finds_the_column_positions_from_the_headings()
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(TestPaths.RealReport!);
        var lines = PdfTableReader.ReadLines(document.GetPage(1));
        var format = ReportFormats.SingleAdults;
        var threshold = PdfTableReader.RowBreakThreshold(lines, format.RowBreakFactor);

        var layout = PdfTableReader.GroupIntoRows(lines, threshold)
            .Select(format.Detect)
            .FirstOrDefault(l => l is not null);

        output.WriteLine($"row break at: {threshold:0.00}pt");
        Assert.NotNull(layout);
        Assert.True(layout.Has(LcrField.Address));
    }
}

/// <summary>Runs a genuine export all the way through the importer into a real
/// database — the path the app takes, not just the parser the other tests exercise.</summary>
public sealed class RealImportTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"courier-real-{Guid.NewGuid():N}.db");

    private Courier.Data.CourierDbContext Open()
    {
        var db = Courier.Data.CourierDatabase.Open(_path);
        Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(db.Database);
        return db;
    }

    [RequiresRealReport]
    public async Task The_whole_directory_imports_and_then_imports_again_unchanged()
    {
        using var db = Open();
        var service = new Courier.Data.ImportService(db);

        var (report, plan) = await service.PrepareAsync(TestPaths.RealReport!);
        output.WriteLine($"first run: {plan.Added.Count} added, {plan.Warnings.Count} warnings");
        Assert.Equal(427, plan.Added.Count);
        // Assert.Empty(plan.Deactivated) would dump every deactivated PersonUpdate's
        // name and contact details on failure; Assert.True with a count keeps a
        // failure to a number.
        Assert.True(plan.Deactivated.Count == 0, $"{plan.Deactivated.Count} deactivated when none were expected");

        var run = await service.ApplyAsync(report, plan, new DateOnly(2026, 9, 16));
        Assert.Equal(427, run.AddedCount);

        var people = await new Courier.Data.DirectoryService(db).RecipientsAsync();
        Assert.Equal(427, people.Count);

        var withPhone = people.Count(p => p.Phone is not null);
        var withEmail = people.Count(p => p.Email is not null);
        output.WriteLine($"reachable by phone: {withPhone}, by email: {withEmail}");
        Assert.True(withPhone > 300, $"only {withPhone} people got a usable phone number");
        Assert.True(withEmail > 200, $"only {withEmail} people got an email address");

        // Every phone Courier would dial must be in the form Twilio accepts.
        // Assert.All would print the offending Recipient (name, ward, phone and all)
        // on failure; a count keeps a failure to a number.
        var twilioShape = new Regex(@"^\+1\d{10}$");
        Assert.Equal(0, people.Count(p => p.Phone is not null && !twilioShape.IsMatch(p.Phone)));

        // Importing the very same file again must change nothing at all. This is the
        // check that matters most: the app re-imports every week.
        var (secondReport, secondPlan) = await service.PrepareAsync(TestPaths.RealReport!);
        output.WriteLine($"second run: {secondPlan.Added.Count} added, {secondPlan.Updated.Count} updated, " +
                         $"{secondPlan.Deactivated.Count} deactivated, {secondPlan.Unchanged} unchanged");

        // Same reasoning as above: Assert.Empty on these would dump the offending
        // PersonUpdate/NormalizedPerson records, names and contact details included.
        Assert.True(secondPlan.Added.Count == 0, $"{secondPlan.Added.Count} added on a re-import that should change nothing");
        Assert.True(secondPlan.Updated.Count == 0, $"{secondPlan.Updated.Count} updated on a re-import that should change nothing");
        Assert.True(secondPlan.Deactivated.Count == 0, $"{secondPlan.Deactivated.Count} deactivated on a re-import that should change nothing");
        Assert.Equal(427, secondPlan.Unchanged);
        Assert.Equal(secondReport.Sha256, report.Sha256);
    }

    public void Dispose()
    {
        // Deliberately not ClearAllPools: it is process-wide, and clearing pools while
        // another test class still holds a connection makes unrelated tests fail.
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { /* a temp file left behind harms nothing */ }
    }
}

/// <summary>Measures the parser against a real Organizations and Callings export.
/// The numbers here are what decide whether an import of that report can be
/// trusted, so they are asserted, not just printed.</summary>
public sealed class RealCallingsReportTests(ITestOutputHelper output)
{
    [RequiresRealCallingsReport]
    public void Reads_every_person_from_the_report()
    {
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);

        var phone = new Regex(@"^(\(\d{3}\)\s*)?\d{3}-\d{4}$");
        var email = new Regex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$");
        var birthday = new Regex(@"^\d{1,2} [A-Z][a-z]{2}$");

        int Bad(Func<LcrRow, string> field, Regex shape) =>
            report.Rows.Count(r => field(r).Length > 0 && !shape.IsMatch(field(r)));

        output.WriteLine($"format      {report.Source.FormatName}");
        output.WriteLine($"pages       {report.PageCount}");
        output.WriteLine($"people      {report.Rows.Count}");
        output.WriteLine($"bad phone   {Bad(r => r.Phone, phone)}");
        output.WriteLine($"bad email   {Bad(r => r.Email, email)}");
        output.WriteLine($"bad bday    {Bad(r => r.Birthday, birthday)}");
        output.WriteLine($"no email    {report.Rows.Count(r => r.Email.Length == 0)}");
        output.WriteLine($"no phone    {report.Rows.Count(r => r.Phone.Length == 0)}");
        foreach (var r in report.Rows.Where(r => r.Phone.Length > 0 && !phone.IsMatch(r.Phone)).Take(5))
            output.WriteLine($"  PHONE '{r.Phone}'");
        foreach (var r in report.Rows.Where(r => r.Email.Length > 0 && !email.IsMatch(r.Email)).Take(5))
            output.WriteLine($"  EMAIL '{r.Email}'");

        Assert.Equal("Organizations and Callings", report.Source.FormatName);
        Assert.Equal(9, report.PageCount);

        // The report prints "Count: 428" in its own footer. Reading exactly that many
        // people back is the strongest check available that no row was dropped.
        Assert.Equal(428, report.Rows.Count);
        Assert.Equal(0, Bad(r => r.Phone, phone));
        Assert.Equal(0, Bad(r => r.Birthday, birthday));
        Assert.Equal(0, Bad(r => r.Email, email));
    }

    [RequiresRealCallingsReport]
    public void Reads_nothing_into_the_columns_this_report_does_not_print()
    {
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);

        Assert.Equal(428, report.Rows.Count);
        Assert.Equal(0, report.Rows.Count(r => r.Address.Length > 0));
        Assert.Equal(0, report.Rows.Count(r => r.Age.Length > 0));
        Assert.False(report.Source.Carry(ReportFields.Address));
        Assert.False(report.Source.Carry(ReportFields.Age));
    }

    [RequiresRealCallingsReport]
    public void Leaves_out_the_stake_callings_table()
    {
        // Page 1 prints the stake's Single Adult callings above the members table,
        // including rows reading "Calling Vacant" and names with no contact details.
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);
        Assert.Equal(428, report.Rows.Count);
        Assert.Equal(0, report.Rows.Count(r => r.Name.Contains("Vacant", StringComparison.Ordinal)));
        Assert.Equal(0, report.Rows.Count(r => !r.Name.Contains(',')));
    }

    [RequiresRealCallingsReport]
    public void Every_unit_snaps_onto_a_known_ward()
    {
        var report = LcrReportParser.Parse(TestPaths.RealCallingsReport!);
        Assert.Equal(428, report.Rows.Count);

        // The unit itself identifies nobody, so it is safe to show on failure — unlike
        // the row, which would also carry the person's name and contact details.
        var unsnapped = report.Rows.Select(r => r.Unit).Distinct().Where(u => Wards.Snap(u) is null).ToList();
        foreach (var u in unsnapped.Take(10)) output.WriteLine($"  UNIT '{u}'");
        Assert.Empty(unsnapped);
    }
}

/// <summary>Runs a genuine Organizations and Callings export all the way through the
/// importer into a real database, then imports it again to prove it settles.</summary>
public sealed class RealCallingsImportTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"courier-callings-{Guid.NewGuid():N}.db");

    private Courier.Data.CourierDbContext Open()
    {
        var db = Courier.Data.CourierDatabase.Open(_path);
        Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate(db.Database);
        return db;
    }

    [RequiresRealCallingsReport]
    public async Task The_whole_directory_imports_and_then_imports_again_unchanged()
    {
        using var db = Open();
        var service = new Courier.Data.ImportService(db);

        var (report, plan) = await service.PrepareAsync(TestPaths.RealCallingsReport!);
        output.WriteLine($"first run: {plan.Added.Count} added, {plan.Warnings.Count} warnings");
        output.WriteLine($"note: {string.Join(" ", plan.Notes)}");

        Assert.Equal(428, plan.Added.Count);
        // Assert.Empty(plan.Deactivated) would dump every deactivated PersonUpdate's
        // name and contact details on failure; Assert.True with a count keeps a
        // failure to a number.
        Assert.True(plan.Deactivated.Count == 0, $"{plan.Deactivated.Count} deactivated when none were expected");
        Assert.Contains(plan.Notes, n => n.Contains("Organizations and Callings", StringComparison.Ordinal));

        var run = await service.ApplyAsync(report, plan, new DateOnly(2026, 9, 21));
        Assert.Equal(428, run.AddedCount);

        var people = await new Courier.Data.DirectoryService(db).RecipientsAsync();
        Assert.Equal(428, people.Count);

        var withPhone = people.Count(p => p.Phone is not null);
        var withEmail = people.Count(p => p.Email is not null);
        output.WriteLine($"reachable by phone: {withPhone}, by email: {withEmail}");
        Assert.True(withPhone > 300, $"only {withPhone} people got a usable phone number");
        Assert.True(withEmail > 200, $"only {withEmail} people got an email address");

        var twilioShape = new Regex(@"^\+1\d{10}$");
        Assert.Equal(0, people.Count(p => p.Phone is not null && !twilioShape.IsMatch(p.Phone)));

        var (_, second) = await service.PrepareAsync(TestPaths.RealCallingsReport!);
        output.WriteLine($"second run: {second.Added.Count} added, {second.Updated.Count} updated, " +
                         $"{second.Deactivated.Count} deactivated, {second.Unchanged} unchanged");

        // Same reasoning as above: Assert.Empty on these would dump the offending
        // PersonUpdate/NormalizedPerson records, names and contact details included.
        Assert.True(second.Added.Count == 0, $"{second.Added.Count} added on a re-import that should change nothing");
        Assert.True(second.Updated.Count == 0, $"{second.Updated.Count} updated on a re-import that should change nothing");
        Assert.True(second.Deactivated.Count == 0, $"{second.Deactivated.Count} deactivated on a re-import that should change nothing");
        Assert.Equal(428, second.Unchanged);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch (IOException) { /* a temp file left behind harms nothing */ }
    }
}
