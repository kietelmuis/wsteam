using System;
using System.Threading.Tasks;
using SteamKit2;

public class SteamSession
{
    public readonly SteamClient SteamClient;
    public readonly SteamUser SteamUser;
    public SteamContent SteamContent;
    public SteamApps SteamApps;

    public readonly CallbackManager callbackManager;

    public bool loggedIn = false;

    public SteamSession()
    {
        this.SteamClient = new SteamClient();
        this.SteamUser = this.SteamClient.GetHandler<SteamUser>()
            ?? throw new InvalidOperationException("SteamUser handler not found");
        this.SteamContent = this.SteamClient.GetHandler<SteamContent>()
            ?? throw new InvalidOperationException("SteamContent handler not found");
        this.SteamApps = this.SteamClient.GetHandler<SteamApps>()
            ?? throw new InvalidOperationException("SteamApps handler not found");
        this.callbackManager = new CallbackManager(SteamClient);

        callbackManager.Subscribe<SteamClient.ConnectedCallback>(_ =>
        {
            Console.WriteLine("SteamClient connected, logging on");
            SteamUser.LogOnAnonymous();
        });

        callbackManager.Subscribe<SteamClient.DisconnectedCallback>(cb =>
        {
            Console.WriteLine($"SteamClient disconnected: forced={!cb.UserInitiated}");
            loggedIn = false;
        });

        callbackManager.Subscribe<SteamUser.LoggedOffCallback>(cb =>
        {
            Console.WriteLine($"SteamUser logged off: {cb.Result}");
            loggedIn = false;
        });

        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(LogOnCallback);
        SteamClient.Connect();
    }

    public async Task WaitLoggedOnAsync()
    {
        while (!loggedIn) await callbackManager.RunWaitCallbackAsync();
    }

    private void LogOnCallback(SteamUser.LoggedOnCallback loggedOn)
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
}
