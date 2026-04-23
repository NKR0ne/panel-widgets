using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Kiota.Abstractions.Authentication;

namespace PanelWidgets.Services;

public sealed record EventModel(string Title, string Time, bool IsAllDay);
public sealed record TaskModel(string Id, string Title, bool Completed);

/// <summary>
/// Thin wrapper around Microsoft Graph SDK v5.
/// Auth uses MSAL with WAM (Windows Account Manager) so the user gets a
/// native Windows sign-in dialog and tokens are cached in the OS keychain.
///
/// TODO: Register your app in Entra ID (https://portal.azure.com) and set ClientId below.
///       Required scopes: Calendars.Read  Tasks.ReadWrite
/// </summary>
public sealed class GraphService
{
    public static readonly GraphService Default = new();

    // ── Replace with your Entra ID app registration ──────────────────────────
    private const string ClientId = "00000000-0000-0000-0000-000000000000"; // TODO
    private const string TenantId = "common";
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly string[] Scopes =
        ["Calendars.Read", "Tasks.ReadWrite", "offline_access"];

    private IPublicClientApplication? _msal;
    private GraphServiceClient?       _graph;

    // ── Auth ─────────────────────────────────────────────────────────────────

    private async Task<GraphServiceClient> GetClientAsync()
    {
        if (_graph is not null) return _graph;

        _msal = PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, TenantId)
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
            .Build();

        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new MsalTokenProvider(_msal, Scopes));

        _graph = new GraphServiceClient(authProvider);
        return _graph;
    }

    /// <summary>
    /// Triggers the interactive WAM sign-in.  Called from the settings window.
    /// </summary>
    public async Task SignInAsync()
    {
        _msal ??= PublicClientApplicationBuilder.Create(ClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, TenantId)
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows))
            .Build();

        await _msal.AcquireTokenInteractive(Scopes).ExecuteAsync();
        _graph = null; // force rebuild with fresh token
    }

    public async Task SignOutAsync()
    {
        if (_msal is null) return;
        foreach (var account in await _msal.GetAccountsAsync())
            await _msal.RemoveAsync(account);
        _graph = null;
    }

    // ── Calendar ─────────────────────────────────────────────────────────────

    public async Task<List<EventModel>> GetTodayEventsAsync()
    {
        return await CacheService.Default.GetOrFetchAsync(
            "graph:events:today",
            FetchTodayEventsAsync,
            TimeSpan.FromMinutes(5));
    }

    private async Task<List<EventModel>> FetchTodayEventsAsync()
    {
        var client  = await GetClientAsync();
        var today   = DateTimeOffset.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var result = await client.Me.CalendarView.GetAsync(req =>
        {
            req.QueryParameters.StartDateTime = today.ToString("o");
            req.QueryParameters.EndDateTime   = tomorrow.ToString("o");
            req.QueryParameters.Select        = ["subject", "start", "end", "isAllDay"];
            req.QueryParameters.Orderby       = ["start/dateTime"];
            req.QueryParameters.Top           = 10;
        });

        return result?.Value?.Select(e => new EventModel(
            Title:    e.Subject ?? "(no title)",
            Time:     e.IsAllDay == true
                          ? "All day"
                          : DateTimeOffset.Parse(e.Start!.DateTime!).ToLocalTime().ToString("HH:mm"),
            IsAllDay: e.IsAllDay ?? false))
            .ToList() ?? [];
    }

    // ── To-Do ────────────────────────────────────────────────────────────────

    public async Task<(List<TaskModel> Tasks, string ListId, string ListName)> GetDefaultTaskListAsync()
    {
        return await CacheService.Default.GetOrFetchAsync(
            "graph:tasks:default",
            FetchDefaultTaskListAsync,
            TimeSpan.FromMinutes(2));
    }

    private async Task<(List<TaskModel>, string, string)> FetchDefaultTaskListAsync()
    {
        var client = await GetClientAsync();

        var lists = await client.Me.Todo.Lists.GetAsync();
        var list  = lists?.Value?.FirstOrDefault(l =>
            l.WellknownListName == WellknownListName.DefaultList)
            ?? lists?.Value?.FirstOrDefault();

        if (list is null) return ([], "", "Tasks");

        var tasks = await client.Me.Todo.Lists[list.Id].Tasks.GetAsync(req =>
        {
            req.QueryParameters.Filter = "status ne 'completed'";
            req.QueryParameters.Top    = 10;
            req.QueryParameters.Select = ["id", "title", "status"];
        });

        var models = tasks?.Value?.Select(t => new TaskModel(
            Id:        t.Id ?? "",
            Title:     t.Title ?? "",
            Completed: t.Status == Microsoft.Graph.Models.TaskStatus.Completed))
            .ToList() ?? [];

        return (models, list.Id ?? "", list.DisplayName ?? "Tasks");
    }

    public async Task ToggleTaskAsync(string listId, string taskId, bool markComplete)
    {
        var client = await GetClientAsync();
        await client.Me.Todo.Lists[listId].Tasks[taskId].PatchAsync(new TodoTask
        {
            Status = markComplete
                ? Microsoft.Graph.Models.TaskStatus.Completed
                : Microsoft.Graph.Models.TaskStatus.NotStarted,
        });
        // Invalidate cache so next refresh picks up the change.
        CacheService.Default.Set("graph:tasks:default",
            (new List<TaskModel>(), listId, ""), TimeSpan.Zero);
    }
}

// ── MSAL token provider for the Graph SDK (Kiota auth interface) ──────────────
file sealed class MsalTokenProvider(IPublicClientApplication app, string[] scopes)
    : IAccessTokenProvider
{
    public AllowedHostsValidator AllowedHostsValidator { get; } =
        new(["graph.microsoft.com"]);

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var accounts = await app.GetAccountsAsync();
        try
        {
            var result = await app
                .AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                .ExecuteAsync(cancellationToken);
            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            // Silent refresh failed — caller must trigger interactive sign-in via settings.
            throw new InvalidOperationException(
                "Sign-in required. Open Panel Widgets settings to authenticate.");
        }
    }
}
