using System.Windows;

namespace ArcWpf.Controls;

/// <summary>An applied filter. Picks from the Add filter menu use the field id, so a second pick for a field replaces the first.</summary>
public sealed class FilterChip
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }
}

/// <summary>One value a field can take. <see cref="Hint"/> sits at the end of the row, for example a count.</summary>
public sealed class FilterOption
{
    public string Value { get; set; } = string.Empty;
    public string? Label { get; set; }
    public object? Hint { get; set; }
    /// <summary>A 24-unit Geometry drawn as a stroke, or any element.</summary>
    public object? Icon { get; set; }
    public string Text => Label ?? Value;
    public static implicit operator FilterOption(string value) => new() { Value = value };
}

public sealed class FilterField
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>A 24-unit Geometry drawn as a stroke, or any element.</summary>
    public object? Icon { get; set; }
    public List<FilterOption> Options { get; set; } = [];
}

public enum FilterMenuAlignment { Start, End }

public sealed class FilterChipEventArgs(RoutedEvent routedEvent, object source, FilterChip filter, FilterField? field) : RoutedEventArgs(routedEvent, source)
{
    public FilterChip Filter { get; } = filter;
    public FilterField? Field { get; } = field;
}
