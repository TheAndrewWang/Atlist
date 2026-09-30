namespace Atlist.Services;

public enum QuickMessageCategory { Reminder, Reassurance }

public record QuickMessage(Guid Id, string Line1, string Line2, QuickMessageCategory Category, int TimesUsed = 0);

/// <summary>Saved two-line messages the caregiver can load into the Send page with one tap.</summary>
public class QuickMessageStore : JsonListStore<QuickMessage>
{
    public QuickMessageStore() : base("quick_messages")
    {
        // A starting set on first run; the caregiver can delete any of them.
        if (IsEmptyOnDisk)
        {
            SaveAll(
            [
                new(Guid.NewGuid(), "Drink water", "Glass by sink", QuickMessageCategory.Reminder),
                new(Guid.NewGuid(), "Lunch in fridge", "Top shelf", QuickMessageCategory.Reminder),
                new(Guid.NewGuid(), "Take a short", "walk outside", QuickMessageCategory.Reminder),
                new(Guid.NewGuid(), "Good morning!", "Have a nice day", QuickMessageCategory.Reassurance),
                new(Guid.NewGuid(), "Time to rest", "Sleep well", QuickMessageCategory.Reassurance),
            ]);
        }
    }

    public void Add(QuickMessage message) => SaveAll(GetAll().Append(message));

    public void Remove(Guid id) => SaveAll(GetAll().Where(m => m.Id != id));

    public void MarkUsed(Guid id) =>
        SaveAll(GetAll().Select(m => m.Id == id ? m with { TimesUsed = m.TimesUsed + 1 } : m));
}
