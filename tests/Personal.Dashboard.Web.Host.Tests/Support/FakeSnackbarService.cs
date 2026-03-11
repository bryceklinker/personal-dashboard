using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public class FakeSnackbarService : ISnackbar
{
    private readonly ConcurrentBag<Snackbar> _snackbars = [];
    private readonly ConcurrentBag<(string Message, Severity Severity)> _addedMessages = [];

    public IEnumerable<(string Message, Severity Severity)> AddedMessages => _addedMessages.ToArray();

    public IEnumerable<Snackbar> ShownSnackbars => _snackbars.ToArray();

    public Snackbar? Add(string message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null)
    {
        _addedMessages.Add((message, severity));
        return null;
    }

    public Snackbar? Add(RenderFragment message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null)
        => throw new NotImplementedException();

    public Snackbar? Add(MarkupString message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null)
        => throw new NotImplementedException();

    public Snackbar? Add<TComponent>(Dictionary<string, object>? componentParameters = null, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null) where TComponent : IComponent
        => throw new NotImplementedException();

    public void Clear()
    {
        _snackbars.Clear();
        _addedMessages.Clear();
    }

    public void Remove(Snackbar snackbar) => throw new NotImplementedException();
    public void RemoveByKey(string key) => throw new NotImplementedException();

    public SnackbarConfiguration Configuration => throw new NotImplementedException();

    public event Action? OnSnackbarsUpdated;

    public void Dispose() { }
}
