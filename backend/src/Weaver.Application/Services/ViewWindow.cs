namespace Weaver.Application.Services;

/// <summary>
/// What running a <see cref="Domain.View"/> takes besides the View itself: the date window,
/// which a View doesn't store, and the page.
/// </summary>
public sealed record ViewWindow(
    DateOnly? From = null,
    DateOnly? To = null,
    string? Cursor = null,
    int Limit = WorkItemQuery.DefaultLimit);
