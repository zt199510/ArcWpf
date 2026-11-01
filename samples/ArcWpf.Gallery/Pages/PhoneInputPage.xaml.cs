using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

// The registry demo: "Send code" waits for a valid number, sends for 900 ms, then offers "Resend code" and confirms the formatted number.
public partial class PhoneInputPage : UserControl
{
    private bool _sending;

    public PhoneInputPage() { InitializeComponent(); }

    private void PhoneChanged(object? sender, PhoneValueChangedEventArgs e)
    {
        Sent.Visibility = Visibility.Collapsed;
        Send.Content = "Send code";
        Send.IsEnabled = !_sending && e.IsValid;
    }

    private async void SendClick(object sender, RoutedEventArgs e)
    {
        _sending = true;
        Send.IsLoading = true;
        await Task.Delay(900);
        _sending = false;
        Send.IsLoading = false;
        Send.Content = "Resend code";
        Sent.Text = $"Code sent to {PhoneNumbers.Format(Phone.Value)}";
        Sent.Visibility = Visibility.Visible;
    }
}
