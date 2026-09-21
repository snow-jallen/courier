using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Courier.App;
using Courier.App.ViewModels;
using Courier.App.Views;
using Courier.Data;
using Courier.Messaging.Settings;
using Microsoft.EntityFrameworkCore;

namespace Courier.Tests;

/// <summary>Builds the real window against a real database, with no screen attached.
///
/// A view model compiles whatever its XAML says, so a mistyped binding, a resource that
/// does not exist or a template bound to the wrong type is invisible until someone opens
/// the app. These walk every screen and would have caught each of those.</summary>
public sealed class UserInterfaceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"courier-ui-{Guid.NewGuid():N}");

    private sealed class NoFiles : IFilePicker
    {
        public Task<string?> PickPdfAsync() => Task.FromResult<string?>(null);
    }

    private sealed class NoClipboard : IClipboardWriter
    {
        public Task CopyAsync(string text) => Task.CompletedTask;
    }

    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() =>
        HeadlessUnitTestSession.StartNew(typeof(HeadlessApp)));

    private static Task InWindow(Func<MainWindow, MainWindowViewModel, Task> body, string folder) =>
        Session.Value.Dispatch(async () =>
        {
            Directory.CreateDirectory(folder);
            var services = AppServices.Start(Path.Combine(folder, "contacts.db"));
            var window = new MainWindow(services);
            window.Show();

            var model = (MainWindowViewModel)window.DataContext!;
            await body(window, model);
        }, CancellationToken.None);

    [Fact]
    public Task The_window_opens_on_the_import_screen() => InWindow((window, model) =>
    {
        Assert.IsType<ImportViewModel>(model.Current);
        Assert.Equal("Courier", window.Title);
        return Task.CompletedTask;
    }, _folder);

    [Fact]
    public Task Every_screen_opens() => InWindow(async (_, model) =>
    {
        await model.ShowPeopleAsync();
        Assert.IsType<PeopleViewModel>(model.Current);

        await model.ShowSendAsync();
        Assert.IsType<SendViewModel>(model.Current);

        await model.ShowLcrAsync();
        Assert.IsType<LcrBacklogViewModel>(model.Current);

        await model.ShowHistoryAsync();
        Assert.IsType<HistoryViewModel>(model.Current);

        model.ShowSetup();
        Assert.IsType<SetupViewModel>(model.Current);

        model.ShowImport();
        Assert.IsType<ImportViewModel>(model.Current);
    }, _folder);

    [Fact]
    public Task A_new_database_shows_an_empty_directory_rather_than_failing() =>
        InWindow(async (_, model) =>
        {
            await model.ShowPeopleAsync();
            var people = (PeopleViewModel)model.Current;
            Assert.Empty(people.Rows);
            Assert.Equal("0 people", people.Summary);

            await model.ShowLcrAsync();
            var lcr = (LcrBacklogViewModel)model.Current;
            Assert.True(lcr.IsEmpty);
            Assert.Contains("Nothing waiting", lcr.Summary, StringComparison.Ordinal);
        }, _folder);

    [Fact]
    public Task The_send_screen_offers_nobody_when_the_directory_is_empty() =>
        InWindow(async (_, model) =>
        {
            await model.ShowSendAsync();
            var send = (SendViewModel)model.Current;
            Assert.Empty(send.Rows);
            Assert.Equal("Send to 0 people", send.SendLabel);
        }, _folder);

    [Fact]
    public Task Setup_starts_from_the_settings_on_disk_and_says_where_the_file_is() =>
        InWindow((_, model) =>
        {
            model.ShowSetup();
            var setup = (SetupViewModel)model.Current;

            Assert.Equal("smtp.gmail.com", new SettingsStore(setup.SettingsPath).Load().Email.Host);
            Assert.EndsWith("contacts.db", setup.DatabasePath, StringComparison.Ordinal);
            return Task.CompletedTask;
        }, _folder);

    [Fact]
    public Task The_rail_names_the_person_not_the_stake() => InWindow((_, model) =>
    {
        // Nothing filled in yet, so it asks rather than inventing an organisation.
        Assert.Equal("SET UP WHO THIS IS FROM", model.SenderLabel);
        return Task.CompletedTask;
    }, _folder);

    [Fact]
    public Task A_message_is_signed_with_the_name_and_calling_from_setup() =>
        InWindow(async (_, model) =>
        {
            var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
            store.Save(store.Load() with
            {
                Sender = new Courier.Core.Domain.SenderIdentity("Jonathan Allen", "Stake Singles Representative"),
            });

            await model.ShowSendAsync();
            var send = (SendViewModel)model.Current;
            send.Body = "Dinner is Friday at 6:30.";

            Assert.Equal("Dinner is Friday at 6:30.\n\u2014 Jonathan Allen, Stake Singles Representative",
                send.MessagePreview);
            Assert.Contains("one text message", send.LengthLine, StringComparison.Ordinal);

            model.ShowImport();
            Assert.Equal("JONATHAN ALLEN, STAKE SINGLES REPRESENTATIVE", model.SenderLabel);
        }, _folder);

    /// <summary>Seeds one person so the list screens have something to show.</summary>
    private static async Task<Guid> SeedOneAsync(AppServices services)
    {
        await using var db = services.Db();
        var person = new Courier.Data.Entities.Person
        {
            LastName = "Ashgrove", FirstName = "Adelaide", DisplayName = "Ashgrove, Adelaide",
            Ward = "Manti 2nd Ward", Age = 40, BirthMonth = 3, BirthDay = 4,
            LcrEmail = "a.ashgrove@example.com", LcrPhone = "(435) 555-0111",
            FirstSeenOn = new DateOnly(2026, 9, 16), LastSeenOn = new DateOnly(2026, 9, 16),
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    private static ScrollViewer Scroller(Window window, string name) =>
        window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == name);

    private static void Resize(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        // Headless has no compositor driving frames, so the layout pass has to be
        // asked for: measure and arrange against the new size, then let bindings settle.
        window.Measure(new Size(width, height));
        window.Arrange(new Rect(0, 0, width, height));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The lists are the point of these screens, so they take whatever room
    /// the window has rather than a height picked in advance. Asserted by measuring,
    /// because a fixed height looks perfectly fine until somebody maximises.</summary>
    [Theory]
    [InlineData("people", "PeopleScroller")]
    [InlineData("send", "RecipientScroller")]
    [InlineData("lcr", "BacklogScroller")]
    public Task A_list_grows_with_the_window(string screen, string scroller) =>
        InWindow(async (window, model) =>
        {
            switch (screen)
            {
                case "people": await model.ShowPeopleAsync(); break;
                case "send": await model.ShowSendAsync(); break;
                default: await model.ShowLcrAsync(); break;
            }

            Resize(window, 1000, 700);
            var small = Scroller(window, scroller).Bounds;

            Resize(window, 1500, 1050);
            var large = Scroller(window, scroller).Bounds;

            Assert.True(large.Height > small.Height + 250,
                $"{scroller} was {small.Height:0} tall in a 700px window and {large.Height:0} in a 1050px one");
            Assert.True(large.Width > small.Width + 400,
                $"{scroller} was {small.Width:0} wide in a 1000px window and {large.Width:0} in a 1500px one");
        }, _folder);

    /// <summary>The Send screen carries a recipient list and a compose area, and in a
    /// short window they cannot both have what they want. The list keeps a usable
    /// minimum and the page scrolls instead of squeezing it to nothing.</summary>
    [Fact]
    public Task A_short_window_scrolls_rather_than_crushing_the_recipient_list() =>
        InWindow(async (window, model) =>
        {
            await model.ShowSendAsync();
            Resize(window, 900, 600);

            var list = Scroller(window, "RecipientScroller");
            Assert.True(list.Bounds.Height >= 200,
                $"the recipient list was squeezed to {list.Bounds.Height:0}px");
            Assert.True(list.Bounds.Width <= window.Width,
                "the recipient list is wider than the window it sits in");

            var page = Scroller(window, "PageScroller");
            Assert.True(page.Extent.Height > page.Viewport.Height,
                "the page does not scroll, so the compose area below the list is unreachable");
        }, _folder);

    [Fact]
    public Task A_tall_window_gives_the_extra_room_to_the_list_rather_than_scrolling() =>
        InWindow(async (window, model) =>
        {
            await model.ShowSendAsync();

            Resize(window, 1440, 960);
            var roomy = Scroller(window, "RecipientScroller").Bounds.Height;

            Resize(window, 900, 600);
            var cramped = Scroller(window, "RecipientScroller").Bounds.Height;

            Assert.True(roomy > cramped + 150,
                $"the list was {cramped:0}px in a short window and only {roomy:0}px in a tall one");
        }, _folder);

    [Fact]
    public Task Setup_scrolls_on_its_own_now_that_the_window_does_not() =>
        InWindow((window, model) =>
        {
            model.ShowSetup();
            Resize(window, 1000, 700);

            // Setup is a long form; with the window's scroller gone it must bring its own
            // or the last card becomes unreachable.
            var scrollers = window.GetVisualDescendants().OfType<ScrollViewer>().ToList();
            Assert.NotEmpty(scrollers);
            Assert.Contains(scrollers, s => s.Extent.Height > s.Viewport.Height);
            return Task.CompletedTask;
        }, _folder);

    [Fact]
    public Task A_person_can_be_corrected_and_the_correction_reaches_the_lcr_list() =>
        InWindow(async (_, model) =>
        {
            await SeedOneAsync(AppServices.Start(Path.Combine(_folder, "contacts.db")));

            await model.ShowPeopleAsync();
            var people = (PeopleViewModel)model.Current;
            var row = Assert.Single(people.Rows);

            Assert.Null(people.Editing);
            row.EditCommand.Execute(null);
            var editor = Assert.IsType<PersonEditor>(people.Editing);
            Assert.Equal("Ashgrove, Adelaide", editor.Name);
            Assert.Equal("a.ashgrove@example.com", editor.Email);

            editor.Email = "adelaide.new@example.com";
            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Null(people.Editing);
            Assert.Contains("waiting to be entered in LCR", people.EditStatus, StringComparison.Ordinal);
            Assert.Equal("adelaide.new@example.com", Assert.Single(people.Rows).Person.Email);

            // The correction is what the LCR screen lists.
            await model.ShowLcrAsync();
            var lcr = (LcrBacklogViewModel)model.Current;
            Assert.Equal("adelaide.new@example.com", Assert.Single(lcr.Rows).Value);
        }, _folder);

    [Fact]
    public Task Choosing_a_channel_for_everyone_overrides_what_each_person_picked() =>
        InWindow(async (_, model) =>
        {
            await SeedOneAsync(AppServices.Start(Path.Combine(_folder, "contacts.db")));

            await model.ShowSendAsync();
            var send = (SendViewModel)model.Current;
            var row = Assert.Single(send.Rows);

            // Nobody has chosen a channel, so nobody can be reached.
            Assert.Equal("Send to 0 people", send.SendLabel);
            Assert.True(send.AnyUnreachable);

            send.SendVia = "Everyone by text";
            Assert.Equal("Send to 1 person", send.SendLabel);
            Assert.Equal(1, send.TextCount);
            Assert.False(send.AnyUnreachable);

            // Or set it on the row, which sticks for next time.
            send.SendVia = SendViewModel.EachPersonsChoice;
            await row.ChooseEmailCommand.ExecuteAsync(null);
            Assert.Equal(Courier.Core.Domain.Channel.Email, row.Channel);
            Assert.Equal(1, send.EmailCount);
            Assert.Equal("Send to 1 person", send.SendLabel);
        }, _folder);

    [Fact]
    public Task Somebody_the_export_does_not_carry_can_be_added_from_the_people_screen() =>
        InWindow(async (_, model) =>
        {
            await model.ShowPeopleAsync();
            var people = (PeopleViewModel)model.Current;
            Assert.Empty(people.Rows);

            people.AddPersonCommand.Execute(null);
            var editor = Assert.IsType<PersonEditor>(people.Editing);
            Assert.True(editor.IsNew);

            // A surname is the one thing required, since it is what the list sorts on.
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Contains("surname is needed", editor.Status, StringComparison.Ordinal);
            Assert.NotNull(people.Editing);

            editor.LastName = "Winslade";
            editor.FirstName = "Verity";
            editor.WardChoice = "Manti 5th Ward";
            editor.Phone = "(435) 555-0150";
            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Null(people.Editing);
            Assert.Contains("no import will remove them", people.EditStatus, StringComparison.Ordinal);

            var row = Assert.Single(people.Rows);
            Assert.Equal("Winslade, Verity", row.Name);
            Assert.Equal("Manti 5th Ward", row.Ward);
            Assert.Equal("+14355550150", row.Person.Phone);
        }, _folder);

    [Fact]
    public Task Setup_says_whether_it_needs_saving_without_anyone_scrolling_to_find_out() =>
        InWindow(async (window, model) =>
        {
            model.ShowSetup();
            var setup = (SetupViewModel)model.Current;

            Assert.False(setup.IsDirty);
            Assert.Equal("Everything here is saved.", setup.SaveHint);

            setup.SenderName = "Jonathan Allen";
            Assert.True(setup.IsDirty);
            Assert.Contains("not saved", setup.SaveHint, StringComparison.Ordinal);

            setup.SaveCommand.Execute(null);
            Assert.False(setup.IsDirty);

            // Typing in any of the other fields marks it too, without each one having
            // to be wired up by hand.
            setup.GatewayUrl = "http://192.168.1.44:8080";
            Assert.True(setup.IsDirty);

            // And the Save button is not inside the part that scrolls.
            Resize(window, 1000, 700);
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
                .First(s => s.Extent.Height > s.Viewport.Height);
            var save = window.GetVisualDescendants().OfType<Button>()
                .First(b => Equals(b.Content, "Save"));

            Assert.False(save.GetVisualAncestors().Contains(scroller),
                "the Save button is inside the scrolling area, so it can be scrolled out of sight");
            await Task.CompletedTask;
        }, _folder);

    [Fact]
    public Task Numbers_and_notes_are_shown_the_way_people_read_them() =>
        InWindow(async (_, model) =>
        {
            var services = AppServices.Start(Path.Combine(_folder, "contacts.db"));
            var id = await SeedOneAsync(services);
            await using (var db = services.Db())
            {
                await new Courier.Data.DirectoryService(db).UpdateDetailsAsync(
                    id, null, null, null, "Hard of hearing — call the landline.", AppServices.Today);
            }

            await model.ShowPeopleAsync();
            var row = Assert.Single(((PeopleViewModel)model.Current).Rows);

            Assert.Equal("(435) 555-0111", row.Phone);
            Assert.True(row.HasNote);
            Assert.Equal("Hard of hearing — call the landline.", row.Note);

            await model.ShowSendAsync();
            var sendRow = Assert.Single(((SendViewModel)model.Current).Rows);
            Assert.True(sendRow.HasNote);

            await sendRow.ChooseTextCommand.ExecuteAsync(null);
            Assert.Equal("(435) 555-0111", sendRow.GoesTo);

            await sendRow.ChooseEmailCommand.ExecuteAsync(null);
            Assert.Equal("a.ashgrove@example.com", sendRow.GoesTo);
        }, _folder);

    [Fact]
    public Task Double_clicking_a_row_opens_the_edit_pane() =>
        InWindow(async (window, model) =>
        {
            await SeedOneAsync(AppServices.Start(Path.Combine(_folder, "contacts.db")));
            await model.ShowPeopleAsync();
            var people = (PeopleViewModel)model.Current;
            Resize(window, 1200, 800);

            Assert.Null(people.Editing);

            // The row's own Border carries the handler, so this is what a double-click
            // on any part of the row reaches.
            var row = window.GetVisualDescendants().OfType<Border>()
                .First(b => b.Classes.Contains("row") && b.DataContext is PersonRow);
            row.RaiseEvent(new Avalonia.Input.TappedEventArgs(
                Avalonia.Input.InputElement.DoubleTappedEvent, null!));

            var editor = Assert.IsType<PersonEditor>(people.Editing);
            Assert.Equal("Ashgrove, Adelaide", editor.Name);

            // And the editor shows the number in the readable form too.
            Assert.Equal("(435) 555-0111", editor.Phone);
        }, _folder);

    [Fact]
    public Task The_calls_read_the_message_out_unless_you_record_it_yourself() =>
        InWindow(async (_, model) =>
        {
            await model.ShowSendAsync();
            var send = (SendViewModel)model.Current;

            // Ready to send without recording anything, which is the common case.
            Assert.True(send.SpeakAloud);
            Assert.False(send.UseMyVoice);
            Assert.False(send.HasRecording);
            Assert.Contains("read the message out", send.VoiceStatus, StringComparison.OrdinalIgnoreCase);

            send.UseMyVoice = true;
            Assert.False(send.SpeakAloud);
            Assert.Contains("Record yourself", send.VoiceStatus, StringComparison.Ordinal);
            await Task.CompletedTask;
        }, _folder);

    [Fact]
    public Task Changing_the_message_throws_away_a_recording_of_the_old_wording() =>
        InWindow(async (_, model) =>
        {
            await model.ShowSendAsync();
            var send = (SendViewModel)model.Current;

            send.Body = "Dinner is Friday at 6:30.";
            send.RecordingUrl = "https://api.twilio.com/RE1.mp3";
            Assert.True(send.HasRecording);

            send.Body = "Dinner has moved to Thursday.";

            // A recording of the old wording would go out sounding confident and wrong.
            Assert.False(send.HasRecording);
            Assert.Contains("recording was cleared", send.VoiceStatus, StringComparison.Ordinal);
            await Task.CompletedTask;
        }, _folder);

    [Fact]
    public Task The_history_screen_says_so_plainly_before_anything_has_been_sent() =>
        InWindow(async (_, model) =>
        {
            await model.ShowHistoryAsync();
            var history = (HistoryViewModel)model.Current;

            Assert.True(history.IsEmpty);
            Assert.Empty(history.Batches);
            Assert.False(history.HasSelection);
            Assert.Contains("Nothing has been sent yet", history.Summary, StringComparison.Ordinal);
        }, _folder);

    public void Dispose()
    {
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* a temp folder left behind harms nothing */ }
    }
}
