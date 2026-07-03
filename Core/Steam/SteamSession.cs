using System;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;

public class SteamSession
{
    public readonly SteamClient SteamClient;
    public readonly SteamUser SteamUser;
    public readonly PublishedFile PublishedFile;
    public SteamContent SteamContent;
    public SteamApps SteamApps;

    public readonly CallbackManager callbackManager;

    public bool loggedIn = false;

    private bool anonymous = true;
    private string? refreshToken;

    // Credentials source
    // https://github.com/SteamAutoCracks/WallpaperEngineWorkshopDownloader
    private static readonly string Username = Encoding.UTF8.GetString(Convert.FromBase64String("YWRnamwxMTgy"));
    private static readonly string Password = Encoding.UTF8.GetString(Convert.FromBase64String("UUVUVU85OTk5OQ=="));

    public SteamSession()
    {
        this.SteamClient = new SteamClient();
        this.SteamUser = this.SteamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler not found");
        this.SteamContent = this.SteamClient.GetHandler<SteamContent>()
            ?? throw new InvalidOperationException("SteamContent handler not found");
        this.SteamApps = this.SteamClient.GetHandler<SteamApps>()
            ?? throw new InvalidOperationException("SteamApps handler not found");

        var steamUnifiedMessages = this.SteamClient.GetHandler<SteamUnifiedMessages>()
            ?? throw new InvalidOperationException("SteamUnifiedMessages handler not found");
        this.PublishedFile = steamUnifiedMessages.CreateService<PublishedFile>()
            ?? throw new InvalidOperationException("PublishedFile handler not found");
        this.callbackManager = new CallbackManager(SteamClient);

        callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
    }

    public void Connect(bool anonymous = true)
    {
        this.anonymous = anonymous;

        SteamClient.Connect();
    }

    public async Task WaitLoggedOnAsync()
    {
        while (!loggedIn) await callbackManager.RunWaitCallbackAsync();
    }

    private async void OnConnected(SteamClient.ConnectedCallback _)
    {
        if (anonymous)
        {
            SteamUser.LogOnAnonymous();
            return;
        }

        Console.WriteLine("SteamClient connected, logging on");

        if (refreshToken is not null)
        {
            SteamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = Username,
                AccessToken = refreshToken,
            });
            return;
        }

        try
        {
            var session = await SteamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = Username,
                    Password = Password,
                    IsPersistentSession = true,
                    Authenticator = new UserConsoleAuthenticator(),
                }
            );

            var pollResponse = await session.PollingWaitForResultAsync();

            refreshToken = pollResponse.RefreshToken;

            SteamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken,
                ShouldRememberPassword = true,
                LoginID = (uint)Random.Shared.Next(1, int.MaxValue),
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
        }
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback cb)
    {
        Console.WriteLine($"SteamClient disconnected: forced={!cb.UserInitiated}");
        loggedIn = false;
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback loggedOn)
    {
        if (loggedOn.Result == EResult.OK)
        {
            Console.WriteLine("Logged in!");
            loggedIn = true;
        }
        else
        {
            Console.WriteLine($"Error upon logging in: {loggedOn.Result}");
        }
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback cb)
    {
        Console.WriteLine($"SteamUser logged off: {cb.Result}");
        loggedIn = false;
    }
}
