using System.Windows;
using System.Windows.Input;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArcWpf.Tests;

[TestClass]
public sealed class WalletStackTests
{
    private static WalletStackCardCollection Cards() =>
    [
        new() { Id = "a", Issuer = "Fieldnote", Product = "Everyday debit", LastFour = "4821", Holder = "Jordan Hayes", Variant = WalletCardVariant.Metal, Balance = 3248.60, BalanceLabel = "Available", Transactions = new WalletTransactionCollection { new() { Id = "t1", Merchant = "Greenleaf", Detail = "Groceries", Amount = -84.12, Icon = "shopping-basket" }, new() { Id = "t2", Merchant = "Payroll", Detail = "Sep 18", Amount = 2860, Icon = "arrow-down-left" } } },
        new() { Id = "b", Issuer = "Meridian", Product = "Reserve", LastFour = "0917", Holder = "Jordan Hayes", Variant = WalletCardVariant.Black, Balance = 1120.45, BalanceLabel = "Balance" },
        new() { Id = "c", Issuer = "Tidewater", Product = "Transit", LastFour = "3306", Holder = "Jordan Hayes", Variant = WalletCardVariant.Glass, Balance = 42.75, BalanceLabel = "Pass" },
        new() { Id = "d", Issuer = "Common Ground", Product = "Club", LastFour = "7730", Holder = "Jordan Hayes", Variant = WalletCardVariant.Color, Balance = 18, BalanceLabel = "Credit" },
    ];

    private static void Run(Action<WalletStack> action)
    {
        Sta.Run(() =>
        {
            var wallet = new WalletStack { Cards = Cards(), Label = "Jordan’s cards" };
            var window = new Window { Content = wallet, Width = 600, Height = 600, ShowActivated = false, ShowInTaskbar = false };
            try { window.Show(); Sta.Pump(); action(wallet); }
            finally { window.Close(); }
        });
    }

    private static void Settle(WalletStack wallet) { for (int i = 0; i < 400 && wallet.RunningForTest; i++) { wallet.AdvanceForTest(1d / 60); } }

    [TestMethod]
    public void RestingPoseMatchesTheMeasuredGeometry()
    {
        var front = WalletStack.PoseOf(0, 4, 0, 0, 0, 0, 0);
        Assert.AreEqual(1, front.Scale, 1e-6);
        Assert.AreEqual(100, front.Y, 1e-6);
        Assert.AreEqual(.45, front.Shadow, 1e-6);
        var second = WalletStack.PoseOf(1, 4, 0, 0, 0, 0, 0);
        Assert.AreEqual(.95, second.Scale, 1e-6);
        Assert.AreEqual(88.8, second.Y, 1e-6);
        var fanned = WalletStack.PoseOf(0, 4, 0, 0, 1, 0, 0);
        Assert.AreEqual(144.8, fanned.Y, 1e-6);
    }

    [TestMethod]
    public void OpenSelectedCardRisesAndTheOthersTuckAway()
    {
        var chosen = WalletStack.PoseOf(1, 4, 1, 1, 0, 1, 0);
        Assert.AreEqual(.88, chosen.Scale, 1e-6);
        Assert.AreEqual(0, chosen.Y, 1e-6);
        Assert.AreEqual(.94, WalletStack.PoseOf(0, 4, 1, 1, 0, 1, 0).Scale, 1e-6);
        Assert.AreEqual(377, WalletStack.PoseOf(0, 4, 1, 1, 0, 1, 0).Y, 1e-6);
    }

    [TestMethod]
    public void DragDownBringsTheNextCardAndALongDragNeverSkipsMore() => Run(wallet =>
    {
        wallet.DownForTest(0);
        wallet.MoveForTest(80, .1);
        Assert.IsGreaterThan(0d, wallet.PositionForTest, "The drag follows the pointer one to one.");
        wallet.MoveForTest(100, .1);
        wallet.UpForTest();
        Settle(wallet);
        Assert.AreEqual(1, wallet.PositionForTest, .01);
        Assert.AreEqual("2 of 4", wallet.CountForTest);
        wallet.DownForTest(0);
        wallet.MoveForTest(30, .5);
        wallet.MoveForTest(-30, .5);
        wallet.UpForTest();
        Settle(wallet);
        Assert.AreEqual(1, Math.Round(wallet.PositionForTest), "A drag that returns stays on its card.");
    });

    [TestMethod]
    public void TheFirstCardRubberBandsInsteadOfGoingBack() => Run(wallet =>
    {
        wallet.DownForTest(200);
        wallet.MoveForTest(40, .2);
        Assert.IsLessThan(0d, wallet.PositionForTest);
        Assert.IsGreaterThan(-.5, wallet.PositionForTest);
        wallet.UpForTest();
        Settle(wallet);
        Assert.AreEqual(0, wallet.PositionForTest, .01);
    });

    [TestMethod]
    public void ChoosingOpensTheDetailAndEscapeCloses() => Run(wallet =>
    {
        WalletStackCard? selected = null;
        wallet.SelectedChanged += (_, e) => selected = e.Card;
        wallet.ChooseForTest(0);
        Settle(wallet);
        Assert.IsTrue(wallet.IsOpenForTest);
        Assert.AreEqual("a", selected?.Id);
        Assert.IsTrue(wallet.BackVisibleForTest);
        Assert.AreEqual(2, wallet.ActivityCountForTest);
        Assert.AreEqual("$3,248.60", wallet.MoneyForTest);
        Assert.AreEqual(1, wallet.DetailOpacityForTest, .01);
        wallet.KeyForTest(Key.Escape);
        Settle(wallet);
        Assert.IsFalse(wallet.IsOpenForTest);
        Assert.IsNull(selected);
        Assert.IsFalse(wallet.BackVisibleForTest);
    });

    [TestMethod]
    public void MoneyFormatsSignedAmountsAndOnlyTheLastFourAreSpoken() => Run(wallet =>
    {
        Assert.AreEqual("+$36.50", WalletStack.MoneyTextForTest(36.5, "USD", "en-US", true));
        Assert.AreEqual("-$84.12", WalletStack.MoneyTextForTest(-84.12, "USD", "en-US", true).Replace('−', '-'));
        StringAssert.Contains(wallet.SpokenForTest(0), "4821");
        wallet.KeyForTest(Key.Down);
        Settle(wallet);
        Assert.AreEqual(1, wallet.PositionForTest, .01);
    });
}
