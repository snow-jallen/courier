using Courier.Messaging.Settings;

namespace Courier.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"courier-settings-{Guid.NewGuid():N}");
    private readonly SettingsStore _store;

    public SettingsStoreTests()
    {
        Directory.CreateDirectory(_folder);
        _store = new SettingsStore(Path.Combine(_folder, "settings.json"));
    }

    [Fact]
    public void A_machine_with_no_settings_yet_starts_from_defaults()
    {
        var settings = _store.Load();
        Assert.Equal("smtp.gmail.com", settings.Email.Host);
        Assert.Equal("435", settings.DefaultAreaCode);
        Assert.False(settings.Email.IsComplete);
        Assert.False(settings.Twilio.IsComplete);
    }

    [Fact]
    public void Everything_the_user_typed_comes_back()
    {
        var settings = new CourierSettings
        {
            Email = new EmailSettings
            {
                Address = "manti.singles@example.com",
                AppPassword = "abcd efgh ijkl mnop",
                DisplayName = "Manti Stake Singles",
                Host = "smtp.example.com",
                Port = 465,
            },
            Twilio = new TwilioSettings
            {
                AccountSid = "AC0123456789abcdef0123456789abcdef",
                AuthToken = "token",
                FromNumber = "+14355550188",
                TestNumber = "+14355550164",
                VoiceRecordingUrl = "https://api.twilio.com/recording.mp3",
            },
            DefaultAreaCode = "801",
        };

        _store.Save(settings);

        Assert.Equal(settings, _store.Load());
    }

    [Fact]
    public void A_file_that_cannot_be_read_is_kept_rather_than_overwritten()
    {
        File.WriteAllText(_store.Path, "{ this is not json");

        var settings = _store.Load();

        Assert.False(settings.Email.IsComplete);
        Assert.True(File.Exists(_store.SalvagePath));
        Assert.Contains("not json", File.ReadAllText(_store.SalvagePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Saving_twice_leaves_no_half_written_file_behind()
    {
        _store.Save(new CourierSettings());
        _store.Save(new CourierSettings { DefaultAreaCode = "208" });

        Assert.Equal("208", _store.Load().DefaultAreaCode);
        Assert.Empty(Directory.GetFiles(_folder, "*.writing"));
    }

    [Fact]
    public void The_file_holds_a_password_so_only_its_owner_may_read_it()
    {
        if (OperatingSystem.IsWindows()) return;

        _store.Save(new CourierSettings());

        var mode = File.GetUnixFileMode(_store.Path);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
        Assert.False(mode.HasFlag(UnixFileMode.GroupRead));
        Assert.False(mode.HasFlag(UnixFileMode.OtherRead));
    }

    [Fact]
    public void Settings_are_kept_away_from_the_directory_and_its_backups()
    {
        // The database is backed up on every import; credentials must not be copied
        // along with it. Belt and braces: this test fails if the store is ever pointed
        // at the database file itself.
        Assert.EndsWith("settings.json", _store.Path, StringComparison.Ordinal);
        Assert.DoesNotContain(".db", _store.Path, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }
}
