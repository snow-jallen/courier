using Courier.App;
using Courier.App.ViewModels;
using Courier.Messaging.Settings;

namespace Courier.Tests;

/// <summary>The iPhone route drives the Messages app, which only exists on macOS. These
/// pin down that it is refused everywhere else rather than accepted and then failing
/// once per recipient at the moment somebody presses Send.</summary>
public sealed class TextRouteTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"courier-route-{Guid.NewGuid():N}");
    private readonly SettingsStore _store;
    private readonly AppServices _services;

    public TextRouteTests()
    {
        Directory.CreateDirectory(_folder);
        _store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        _services = AppServices.Start(Path.Combine(_folder, "contacts.db"), Path.Combine(_folder, "settings.json"));
    }

    private SetupViewModel Setup(bool isMac) => new(_services, _store, isMac);

    [Fact]
    public void Off_a_mac_the_iphone_route_cannot_even_be_chosen()
    {
        var setup = Setup(isMac: false);
        Assert.False(setup.IphoneRouteAvailable);

        setup.UseIphone = true;

        Assert.False(setup.UseIphone);
        Assert.True(setup.UseTwilioForText);
    }

    [Fact]
    public void On_a_mac_it_can()
    {
        var setup = Setup(isMac: true);
        setup.UseIphone = true;

        Assert.True(setup.UseIphone);
        Assert.False(setup.UseTwilioForText);
        Assert.False(setup.TextRouteBlocked);
    }

    [Fact]
    public void The_android_route_is_offered_everywhere_because_it_is_just_a_web_call()
    {
        var setup = Setup(isMac: false);
        setup.UseAndroid = true;
        Assert.True(setup.UseAndroid);
    }

    [Fact]
    public void A_choice_carried_over_from_a_mac_is_flagged_rather_than_silently_wrong()
    {
        _store.Save(_store.Load() with { TextVia = TextTransport.MacMessages });

        var setup = Setup(isMac: false);

        Assert.True(setup.TextRouteBlocked);
        Assert.Contains("needs Courier running on a Mac", setup.TextRouteBlockedMessage, StringComparison.Ordinal);

        // Kept, so going back to the Mac restores it rather than making them choose again.
        Assert.Equal(TextTransport.MacMessages, _store.Load().TextVia);
    }

    [Fact]
    public async Task Sending_is_refused_outright_rather_than_failing_once_per_person()
    {
        _store.Save(_store.Load() with { TextVia = TextTransport.MacMessages });

        var send = new SendViewModel(_services, _store, isMac: false);
        await send.LoadAsync();

        Assert.True(send.IsBlocked);
        Assert.Contains("needs to run on a Mac", send.BlockedReason, StringComparison.Ordinal);

        send.Body = "Dinner is Friday.";
        await send.SendCommand.ExecuteAsync(null);
        Assert.Equal(send.BlockedReason, send.Status);
    }

    [Fact]
    public async Task On_a_mac_nothing_is_blocked()
    {
        _store.Save(_store.Load() with { TextVia = TextTransport.MacMessages });

        var send = new SendViewModel(_services, _store, isMac: true);
        await send.LoadAsync();

        Assert.False(send.IsBlocked);
    }

    [Fact]
    public async Task Twilio_is_never_blocked_wherever_courier_is_running()
    {
        _store.Save(_store.Load() with { TextVia = TextTransport.Twilio });

        var send = new SendViewModel(_services, _store, isMac: false);
        await send.LoadAsync();

        Assert.False(send.IsBlocked);
    }

    // ---- only the guidance that applies to the chosen route ------------------------

    [Fact]
    public void Twilio_advice_is_labelled_as_being_about_twilio()
    {
        var setup = Setup(isMac: true);
        Assert.True(setup.UseTwilioForText);
        Assert.Equal("Used for your texts and your phone calls", setup.TwilioPurpose);
        Assert.Equal("Send myself a test text", setup.TestTextLabel);
    }

    [Fact]
    public void Choosing_a_phone_says_the_twilio_account_is_now_only_for_calls()
    {
        var setup = Setup(isMac: true);
        setup.UseIphone = true;

        // The account is still needed — calls always go through it — so it must not read
        // as a leftover from a route no longer in use.
        Assert.Equal("Used for phone calls — your texts go out from your own phone", setup.TwilioPurpose);
        Assert.Equal("Text myself through Messages", setup.TestTextLabel);
    }

    [Fact]
    public void The_android_route_names_the_phone_rather_than_the_service()
    {
        var setup = Setup(isMac: false);
        setup.UseAndroid = true;

        Assert.Equal("Text myself through my phone", setup.TestTextLabel);
        Assert.DoesNotContain("texts", setup.TwilioPurpose.Split('—')[0], StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true); }
        catch (IOException) { /* a temp folder left behind harms nothing */ }
    }
}
