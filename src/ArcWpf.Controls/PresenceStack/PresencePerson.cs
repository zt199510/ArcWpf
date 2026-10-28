using System.Windows;
using System.ComponentModel;
using System.Windows.Media;

namespace ArcWpf.Controls;

public enum PresenceStatus { Active, Idle, Typing }
public enum PresenceStackAlignment { Start, End }
public sealed class PresencePerson(string id) : INotifyPropertyChanged
{
    public string Id { get; } = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A stable id is required.", nameof(id)) : id;
    private string _name = "";
    private string? _detail;
    private ImageSource? _source;
    private PresenceStatus _status;
    public string Name { get => _name; set { if (_name == value) return; _name = value; Changed(nameof(Name)); } }
    public string? Detail { get => _detail; set { if (_detail == value) return; _detail = value; Changed(nameof(Detail)); } }
    public ImageSource? Source { get => _source; set { if (ReferenceEquals(_source, value)) return; _source = value; Changed(nameof(Source)); } }
    public PresenceStatus Status { get => _status; set { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); if (_status == value) return; _status = value; Changed(nameof(Status)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string property) => PropertyChanged?.Invoke(this, new(property));
}
public sealed class PresencePersonSelectedEventArgs(RoutedEvent routedEvent, PresencePerson person) : RoutedEventArgs(routedEvent)
{
    public PresencePerson Person { get; } = person;
    protected override void InvokeEventHandler(Delegate handler, object target) => ((EventHandler<PresencePersonSelectedEventArgs>)handler)(target, this);
}
