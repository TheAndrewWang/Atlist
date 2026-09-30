using System.Collections.ObjectModel;
using Atlist.Models;
using Atlist.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atlist.ViewModels;

/// <summary>One row on the Quick messages page.</summary>
public class QuickMessageRow
{
    public QuickMessageRow(QuickMessage message) => Message = message;

    public QuickMessage Message { get; }
    public string Line1 => Message.Line1;
    public string Line2 => Message.Line2;
    public string UsedText => Message.TimesUsed == 0 ? "New" : $"Used {Message.TimesUsed}×";

    /// <summary>Reminders preview in blue, reassurance in warm yellow, as in the mockup.</summary>
    public Color PreviewColor => Message.Category == QuickMessageCategory.Reassurance
        ? LcdColors.FromName("yellow")
        : LcdColors.Default;

    public string Description => $"{Line1}, {Line2}. {UsedText}. Tap to use.";
}

/// <summary>A heading ("REMINDERS") and its rows, for the grouped list.</summary>
public class QuickMessageGroup : List<QuickMessageRow>
{
    public QuickMessageGroup(string title, IEnumerable<QuickMessageRow> rows) : base(rows) => Title = title;
    public string Title { get; }
}

/// <summary>
/// Drives the "Quick messages" page: tap a saved message to load it into
/// the Send page; add new ones; swipe to delete.
/// </summary>
public partial class QuickMessagesViewModel : ObservableObject
{
    private readonly QuickMessageStore _store;

    public QuickMessagesViewModel(QuickMessageStore store) => _store = store;

    public ObservableCollection<QuickMessageGroup> Groups { get; } = [];

    public void Load()
    {
        var all = _store.GetAll().OrderByDescending(m => m.TimesUsed).Select(m => new QuickMessageRow(m)).ToList();

        Groups.Clear();
        AddGroup("REMINDERS", all.Where(r => r.Message.Category == QuickMessageCategory.Reminder));
        AddGroup("REASSURANCE", all.Where(r => r.Message.Category == QuickMessageCategory.Reassurance));
    }

    private void AddGroup(string title, IEnumerable<QuickMessageRow> rows)
    {
        var list = rows.ToList();
        if (list.Count > 0) Groups.Add(new QuickMessageGroup(title, list));
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    /// <summary>Goes back to the Send page with the two lines filled in.</summary>
    [RelayCommand]
    private async Task UseAsync(QuickMessageRow row)
    {
        _store.MarkUsed(row.Message.Id);
        await Shell.Current.GoToAsync("..", new ShellNavigationQueryParameters
        {
            ["line1"] = row.Line1,
            ["line2"] = row.Line2
        });
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var page = Shell.Current;

        var line1 = await page.DisplayPromptAsync("New quick message", "Top line (up to 16 characters)",
            accept: "Next", maxLength: DeviceProtocol.LineLength);
        if (string.IsNullOrWhiteSpace(line1)) return;

        var line2 = await page.DisplayPromptAsync("New quick message", "Bottom line (up to 16 characters, can be empty)",
            accept: "Next", maxLength: DeviceProtocol.LineLength) ?? string.Empty;

        var kind = await page.DisplayActionSheetAsync("What kind of message?", "Cancel", null, "Reminder", "Reassurance");
        if (kind is not ("Reminder" or "Reassurance")) return;

        _store.Add(new QuickMessage(
            Guid.NewGuid(),
            DeviceProtocol.CleanLine(line1),
            DeviceProtocol.CleanLine(line2),
            kind == "Reminder" ? QuickMessageCategory.Reminder : QuickMessageCategory.Reassurance));
        Load();
    }

    [RelayCommand]
    private async Task DeleteAsync(QuickMessageRow row)
    {
        var ok = await Shell.Current.DisplayAlertAsync("Delete quick message?",
            $"\"{row.Line1} / {row.Line2}\" will be removed from this list.", "Delete", "Keep");
        if (!ok) return;

        _store.Remove(row.Message.Id);
        Load();
    }
}
