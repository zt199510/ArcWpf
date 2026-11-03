using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ArcWpf.Controls;

namespace ArcWpf.Tests;

[TestClass]
public sealed class ArcDataGridTests
{
    private static readonly string[] Regions = ["North", "South"];

    private static List<ArcDataGridColumn> Columns() =>
    [
        new() { Key = "name", Header = "Name", Width = 160, Pinned = true },
        new() { Key = "region", Header = "Region", Width = 120, Type = ArcDataGridColumnType.Select, Options = Regions },
        new() { Key = "seats", Header = "Seats", Width = 100, Type = ArcDataGridColumnType.Number },
        new() { Key = "arr", Header = "ARR", Width = 120, Type = ArcDataGridColumnType.Currency },
    ];

    private static List<ArcDataGridRow> Rows(int count = 12)
    {
        var rows = new List<ArcDataGridRow>();
        for (int i = 0; i < count; i++)
        {
            var row = new ArcDataGridRow("r" + i);
            row["name"] = "Row " + (char)('A' + i);
            row["region"] = Regions[i % 2];
            row["seats"] = 10d * (count - i);
            row["arr"] = 100d * (i + 1);
            rows.Add(row);
        }
        return rows;
    }

    private static void Hosted(Action<ArcDataGrid> body)
    {
        var grid = new ArcDataGrid { Width = 520, Label = "Test" };
        foreach (var column in Columns()) { grid.Columns.Add(column); }
        grid.Rows = Rows();
        var window = new Window { Content = grid, Width = 640, Height = 640, ShowInTaskbar = false };
        try { window.Show(); Sta.Pump(); grid.UpdateLayout(); body(grid); }
        finally { window.Close(); }
    }

    private static T Part<T>(ArcDataGrid grid, string name) where T : class => (T)grid.Template.FindName(name, grid);

    private static void Click(ButtonBase button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    [TestMethod]
    public void FormatRoundsExactHalvesUpAsTheBrowserDoes()
    {
        var column = new ArcDataGridColumn { Key = "x", Type = ArcDataGridColumnType.Number, Decimals = 1 };
        Assert.AreEqual("23,236.3", ArcDataGridModel.Format(23236.25, column));
        Assert.AreEqual("3", ArcDataGridModel.Format(2.5, new ArcDataGridColumn { Key = "x", Type = ArcDataGridColumnType.Number }));
    }

    [TestMethod]
    public void NumberFilterReadsComparisonsAndRanges()
    {
        Assert.IsTrue(ArcDataGridModel.NumberFilter(">10", out bool valid)!(11));
        Assert.IsTrue(valid);
        Assert.IsFalse(ArcDataGridModel.NumberFilter("10..50", out _)!(51));
        ArcDataGridModel.NumberFilter("abc", out bool invalid);
        Assert.IsFalse(invalid);
    }

    [TestMethod]
    public void CsvRoundTripKeepsValuesAndQuoting()
    {
        var columns = Columns();
        var rows = Rows(2);
        rows[0]["name"] = "Comma, \"quoted\"";
        var back = ArcDataGridModel.ParseCsv(ArcDataGridModel.ToCsv(columns, rows), columns);
        Assert.AreEqual(2, back.Count);
        Assert.AreEqual("Comma, \"quoted\"", back[0]["name"]);
        Assert.AreEqual(20d, back[0]["seats"]);
    }

    [TestMethod]
    public void ExtendContinuesANumberSeries()
    {
        var next = ArcDataGridModel.Extend([1d, 2d], 2, true, ArcDataGridColumnType.Number);
        CollectionAssert.AreEqual(new object?[] { 3d, 4d }, next);
    }

    [TestMethod]
    public void ColumnLettersRollOverLikeASpreadsheet()
    {
        Assert.AreEqual("A", ArcDataGridModel.ColumnLetters(0));
        Assert.AreEqual("AA", ArcDataGridModel.ColumnLetters(26));
    }

    [TestMethod]
    public void TemplateRendersAndAutomationReportsGrid() => Sta.Run(() => Hosted(grid =>
    {
        Assert.IsNotNull(grid.Surface);
        var peer = UIElementAutomationPeer.CreatePeerForElement(grid);
        Assert.AreEqual(AutomationControlType.DataGrid, peer.GetAutomationControlType());
    }));

    [TestMethod]
    public void SelectionStaysInPlaceAcrossSortGroupAndCollapse() => Sta.Run(() => Hosted(grid =>
    {
        grid.Select(1, 4, false);
        Assert.AreEqual("B5", grid.Address());
        grid.Sort = [new ArcDataGridSort("seats")];
        Assert.AreEqual("B5", grid.Address());
        grid.Sort = null;
        grid.GroupBy = ["region"];
        Assert.AreEqual("B5", grid.Address());
        Assert.IsFalse(grid.View[grid.Active.Row].IsGroup);
        grid.ToggleGroup(grid.View[0]);
        Assert.AreEqual("B5", grid.Address());
    }));

    [TestMethod]
    public void RangeSurvivesARebuild() => Sta.Run(() => Hosted(grid =>
    {
        grid.Select(0, 0, false);
        grid.Select(2, 3, true);
        grid.Sort = [new ArcDataGridSort("name", true)];
        Assert.AreEqual("A1:C4", grid.Address());
    }));

    [TestMethod]
    public void SingleNumericCellOnlyCountsButARangeAdds() => Sta.Run(() => Hosted(grid =>
    {
        var stats = Part<Panel>(grid, "PART_Stats");
        bool Shows(string label) => stats.Children.OfType<StackPanel>().Any(panel => (string?)panel.Tag == label && panel.Visibility == Visibility.Visible);
        grid.Select(2, 1, false);
        Assert.IsFalse(Shows("Sum"));
        Assert.IsTrue(Shows("Count"));
        grid.Select(2, 2, true);
        Assert.IsTrue(Shows("Sum"));
        Assert.IsTrue(Shows("Average"));
    }));

    [TestMethod]
    public void NoMatchesShowDashAndClearActionsRestoreRows() => Sta.Run(() => Hosted(grid =>
    {
        Click(Part<ButtonBase>(grid, "PART_Filter"));
        grid.Filters = new Dictionary<string, object> { ["name"] = "zzzz" };
        Sta.Pump();
        Assert.AreEqual(0, grid.View.Count);
        Assert.AreEqual("\u2013", Part<TextBlock>(grid, "PART_Address").Text);
        var clear = typeof(ArcDataGrid).GetField("_filterClearButton", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(grid) as System.Windows.Controls.Button;
        Assert.IsNotNull(clear);
        Assert.AreEqual(Visibility.Visible, clear.Visibility);
        Click(clear);
        Assert.AreEqual(12, grid.View.Count);
        Assert.AreEqual(Visibility.Collapsed, clear.Visibility);
    }));

    [TestMethod]
    public void InvalidEditStaysOpenAndValidEditRaisesAndUndoes() => Sta.Run(() => Hosted(grid =>
    {
        int raised = 0;
        grid.RowsEdited += (_, _) => raised++;
        grid.Select(2, 1, false);
        object? before = grid.View[1].Row!["seats"];
        grid.BeginEdit("abc", true);
        grid.CommitEdit(1, 0);
        Assert.IsTrue(grid.IsEditing);
        Assert.AreEqual(before, grid.View[1].Row!["seats"]);
        Assert.AreEqual(0, raised);
        grid.CancelEdit();

        grid.BeginEdit("77", true);
        grid.CommitEdit(1, 0);
        Assert.IsFalse(grid.IsEditing);
        Assert.AreEqual(77d, grid.Rows![1]["seats"]);
        Assert.AreEqual(1, raised);
        Assert.AreEqual("C3", grid.Address());
        grid.Undo();
        Assert.AreEqual(before, grid.Rows![1]["seats"]);
        Assert.AreEqual(2, raised);
    }));

    [TestMethod]
    public void EditorTextStartsWhereTheCellTextDoes() => Sta.Run(() => Hosted(grid =>
    {
        grid.Select(1, 1, false);
        grid.BeginEdit(null, false);
        Sta.Pump();
        var editor = (TextBox)typeof(ArcDataGrid).GetField("_editor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(grid)!;
        double start = editor.TranslatePoint(editor.GetRectFromCharacterIndex(0).TopLeft, grid.Surface!).X;
        Assert.AreEqual(grid.ColX[1] - grid.Surface!.ScrollX + 12, start, 1);
        grid.CancelEdit();
    }));

    [TestMethod]
    public void DeleteCheckedRemovesRowsAndUndoRestoresThem() => Sta.Run(() => Hosted(grid =>
    {
        int raised = 0;
        grid.RowsEdited += (_, _) => raised++;
        grid.ToggleChecked("r1");
        Click(Part<ButtonBase>(grid, "PART_Delete"));
        Assert.AreEqual(11, grid.Rows!.Count);
        Assert.AreEqual(1, raised);
        grid.Undo();
        Assert.AreEqual(12, grid.Rows!.Count);
        Assert.AreEqual("r1", grid.Rows![1].Id);
        Assert.AreEqual(2, raised);
    }));

    [TestMethod]
    public void PasteTilesAcrossTheRange() => Sta.Run(() => Hosted(grid =>
    {
        grid.Select(2, 0, false);
        grid.Select(2, 3, true);
        grid.Paste([["5"], ["6"]]);
        CollectionAssert.AreEqual(new object?[] { 5d, 6d, 5d, 6d }, grid.Rows!.Take(4).Select(row => row["seats"]).ToArray());
    }));

    [TestMethod]
    public void FillHandleContinuesTheSeries() => Sta.Run(() => Hosted(grid =>
    {
        grid.Rows![0]["seats"] = 1d;
        grid.Rows![1]["seats"] = 2d;
        grid.Select(2, 0, false);
        grid.Select(2, 1, true);
        grid.FillTo(2, 3);
        CollectionAssert.AreEqual(new object?[] { 1d, 2d, 3d, 4d }, grid.Rows!.Take(4).Select(row => row["seats"]).ToArray());
    }));

    [TestMethod]
    public void HeaderMenuOpensAndResetClearsTheView() => Sta.Run(() => Hosted(grid =>
    {
        grid.OpenHeaderMenu(1, new Rect(200, 8, 24, 24));
        Sta.Pump();
        Assert.IsTrue(grid.IsMenuOpen);
        grid.Sort = [new ArcDataGridSort("seats")];
        grid.GroupBy = ["region"];
        grid.ResetState();
        Assert.AreEqual(0, grid.SortKeys.Count);
        Assert.AreEqual(12, grid.View.Count);
        Assert.AreEqual("A1", grid.Address());
    }));
}
