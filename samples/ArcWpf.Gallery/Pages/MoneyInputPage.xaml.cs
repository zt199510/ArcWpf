using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;

// The registry demo: the balance caps the amount per currency, quick add uses ¥1,000 / ¥5,000 for yen, and paying deducts the amount after 900 ms.
public partial class MoneyInputPage : UserControl
{
    private readonly Dictionary<string, long> _balances = new() { ["USD"] = 248000, ["EUR"] = 112050, ["GBP"] = 64000, ["JPY"] = 88000 };
    private bool _paying;

    public MoneyInputPage()
    {
        InitializeComponent();
        Amount.Currencies = ["USD", "EUR", "GBP", "JPY"];
        Amount.Value = 6000;
        Sync();
    }

    private MoneyCurrency Info => MoneyMath.Currency(Amount.Currency);

    private void AmountChanged(object? sender, MoneyValueChangedEventArgs e) { Sent.Visibility = Visibility.Collapsed; Sync(); }

    private void Sync()
    {
        var info = Info;
        long balance = _balances[info.Code];
        Amount.Maximum = balance;
        Amount.QuickAdd = info.Code == "JPY" ? [1000, 5000] : [10, 50, 100];
        Balance.Text = $"Balance {info.Format(balance)}";
        long? value = Amount.Value;
        Pay.Content = value is long amount && amount > 0 ? $"Pay {info.Format(amount)}" : "Pay";
        Pay.IsEnabled = !_paying && value is long minor && minor >= 100 && minor <= balance;
    }

    private async void PayClick(object sender, RoutedEventArgs e)
    {
        if (Amount.Value is not long amount) return;
        var info = Info;
        _paying = true;
        Pay.IsLoading = true;
        Sync();
        await Task.Delay(900);
        _balances[info.Code] -= amount;
        _paying = false;
        Pay.IsLoading = false;
        Amount.Value = null;
        Sync();
        Sent.Text = $"Sent {info.Format(amount)} to Jasmine.";
        Sent.Visibility = Visibility.Visible;
    }
}
