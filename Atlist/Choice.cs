using CommunityToolkit.Mvvm.ComponentModel;

namespace Atlist.Models;

/// <summary>One option in a row of chips ("Take Meds", "All devices", "1 hour"…).</summary>
public partial class Choice : ObservableObject
{
    public Choice(string label, object? value, int index = 0)
    {
        Label = label;
        Value = value;
        Index = index;
    }

    public string Label { get; }

    /// <summary>What the choice stands for: a device Id, an enum value, or null for "all".</summary>
    public object? Value { get; }

    /// <summary>Grid column when the chips sit in a fixed grid.</summary>
    public int Index { get; }

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>One character cell of the 16×2 LCD preview.</summary>
public partial class LcdCell : ObservableObject
{
    public LcdCell(int row, int column)
    {
        Row = row;
        Column = column;
    }

    public int Row { get; }
    public int Column { get; }

    [ObservableProperty]
    private string character = " ";
}
