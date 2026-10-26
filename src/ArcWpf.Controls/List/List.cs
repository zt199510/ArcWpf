using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Input;

namespace ArcWpf.Controls;

public sealed class ListItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private object? _icon;
    private bool _isEnabled = true;

    public string Id { get; set; } = string.Empty;
    public string Title { get => _title; set { if (_title == value) return; _title = value; PropertyChanged?.Invoke(this, new(nameof(Title))); } }
    public string Description { get => _description; set { if (_description == value) return; _description = value; PropertyChanged?.Invoke(this, new(nameof(Description))); } }
    public object? Icon { get => _icon; set { if (Equals(_icon, value)) return; _icon = value; PropertyChanged?.Invoke(this, new(nameof(Icon))); } }
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled == value) return; _isEnabled = value; PropertyChanged?.Invoke(this, new(nameof(IsEnabled))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public class List : ListBox
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(List), new FrameworkPropertyMetadata("List"));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    static List() => DefaultStyleKeyProperty.OverrideMetadata(typeof(List), new FrameworkPropertyMetadata(typeof(List)));

    public List()
    {
        SetCurrentValue(SelectionModeProperty, SelectionMode.Single);
        SetCurrentValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.Once);
        AutomationProperties.SetName(this, Label);
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is ListBoxItem;
    protected override DependencyObject GetContainerForItemOverride() => new ListBoxItem();
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == LabelProperty) AutomationProperties.SetName(this, Label);
    }
}
