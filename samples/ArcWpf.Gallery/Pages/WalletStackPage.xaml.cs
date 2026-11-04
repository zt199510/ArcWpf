using System.Windows.Controls;
using System.Windows.Media;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class WalletStackPage : UserControl
{
    public WalletStackPage()
    {
        InitializeComponent();
        Wallet.Cards = new WalletStackCardCollection
        {
            new()
            {
                Id = "fieldnote", Issuer = "Fieldnote", Product = "Everyday debit", LastFour = "4821", Holder = "Jordan Hayes", Variant = WalletCardVariant.Metal, Expires = "09/29", Balance = 3248.60, BalanceLabel = "Available",
                Transactions = new WalletTransactionCollection
                {
                    new() { Id = "t1", Merchant = "Ryan Mitchell", Detail = "Dinner split · Today", Amount = 36.50, Avatar = Portrait() },
                    new() { Id = "t2", Merchant = "Greenleaf Market", Detail = "Groceries · Yesterday", Amount = -84.12, Icon = "shopping-basket" },
                    new() { Id = "t3", Merchant = "Brightline Studio", Detail = "Payroll · Sep 18", Amount = 2860, Icon = "arrow-down-left" },
                },
            },
            new()
            {
                Id = "meridian", Issuer = "Meridian", Product = "Reserve credit", LastFour = "0917", Holder = "Jordan Hayes", Variant = WalletCardVariant.Black, Expires = "03/28", Balance = 1120.45, BalanceLabel = "Statement balance",
                Transactions = new WalletTransactionCollection
                {
                    new() { Id = "t4", Merchant = "Northline Air", Detail = "Flights · Sep 16", Amount = -642.30, Icon = "shopping-basket" },
                    new() { Id = "t5", Merchant = "Payment received", Detail = "Thank you · Sep 12", Amount = 900, Icon = "arrow-down-left" },
                },
            },
            new()
            {
                Id = "tidewater", Issuer = "Tidewater", Product = "Transit pass", LastFour = "3306", Holder = "Jordan Hayes", Variant = WalletCardVariant.Glass, Expires = "11/27", Balance = 42.75, BalanceLabel = "Pass balance",
                Transactions = new WalletTransactionCollection
                {
                    new() { Id = "t6", Merchant = "Harbor Line", Detail = "Single ride · Today", Amount = -2.75, Icon = "train-front" },
                    new() { Id = "t7", Merchant = "Auto top up", Detail = "Sep 15", Amount = 40, Icon = "arrow-down-left" },
                },
            },
            new()
            {
                Id = "common", Issuer = "Common Ground", Product = "Coffee club", LastFour = "7730", Holder = "Jordan Hayes", Variant = WalletCardVariant.Color, Tint = Color.FromRgb(0x00, 0x51, 0x38), Expires = "06/28", Balance = 18, BalanceLabel = "Club credit",
                Transactions = new WalletTransactionCollection
                {
                    new() { Id = "t8", Merchant = "Common Ground", Detail = "Flat white · Today", Amount = -4.50, Icon = "coffee" },
                },
            },
        };
    }

    // A code-drawn stand-in, so the sample ships no portraits.
    private static ImageSource Portrait()
    {
        var image = new DrawingImage(new GeometryDrawing(new LinearGradientBrush(Color.FromRgb(0x84, 0xA9, 0x8C), Color.FromRgb(0x52, 0x79, 0x6F), 45), null, new RectangleGeometry(new System.Windows.Rect(0, 0, 64, 64))));
        image.Freeze();
        return image;
    }
}
