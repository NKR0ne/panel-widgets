using Microsoft.Windows.Widgets.Providers;
using PanelWidgets.Services;
using System.Text.Json;

namespace PanelWidgets.Providers;

public sealed class TodoWidgetHandler(string widgetId)
    : BaseWidgetHandler(widgetId, TimeSpan.FromMinutes(2))
{
    private static readonly string _template = LoadTemplate("TodoCard.json");

    // Persisted across refreshes so toggle actions know the list ID.
    private string _listId = string.Empty;

    protected override async Task<(string Template, string Data)> BuildCardAsync()
    {
        var (tasks, listId, listName) = await GraphService.Default.GetDefaultTaskListAsync();
        _listId = listId;

        var data = Serialize(new
        {
            listName,
            tasks = tasks.Select(t => new
            {
                id        = t.Id,
                title     = t.Title,
                completed = t.Completed,
            }),
        });
        return (_template, data);
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        if (args.Verb == "toggleTask")
        {
            using var doc    = JsonDocument.Parse(args.Data);
            var taskId       = doc.RootElement.GetProperty("taskId").GetString() ?? "";
            var nowCompleted = doc.RootElement.TryGetProperty("completed", out var c) && c.GetBoolean();

            _ = Task.Run(async () =>
            {
                await GraphService.Default.ToggleTaskAsync(_listId, taskId, !nowCompleted);
                RequestUpdate();
            });
        }
        else if (args.Verb == "openApp")
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("ms-todo://"));
        }
    }
}
