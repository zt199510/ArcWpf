using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ArcDataGridPage : UserControl
{
    private static readonly string[] Regions = ["North America", "Europe", "Asia Pacific", "Latin America"];
    private static readonly string[] Stages = ["Discovery", "Proposal", "Negotiation", "Committed", "Closed won", "Closed lost"];
    private List<ArcDataGridRow> _original = [];

    public ArcDataGridPage()
    {
        InitializeComponent();
        foreach (var column in BuildColumns()) { Grid.Columns.Add(column); }
        Grid.ExportFormats = [ArcDataGridExportFormat.Csv, ArcDataGridExportFormat.Xlsx];
        _original = LoadRows();
        Grid.Rows = LoadRows();
        ResetButton.IsEnabled = false;
        Grid.RowsEdited += (_, _) => ResetButton.IsEnabled = !SameAsOriginal();
    }

    // Reset restores the data, so it wakes only while the data differs: view changes leave it alone and undoing back to the start puts it to sleep.
    private bool SameAsOriginal()
    {
        var rows = Grid.Rows;
        if (rows is null || rows.Count != _original.Count) { return false; }
        var columns = BuildColumns();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id != _original[i].Id) { return false; }
            foreach (var column in columns)
            {
                if (!Equals(rows[i][column.Key], _original[i][column.Key])) { return false; }
            }
        }
        return true;
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        Grid.ResetState(LoadRows());
        ResetButton.IsEnabled = false;
    }

    private static List<ArcDataGridColumn> BuildColumns() =>
    [
        new() { Key = "account", Header = "Account", Width = 188, Pinned = true },
        new() { Key = "owner", Header = "Owner", Width = 148 },
        new() { Key = "region", Header = "Region", Width = 144, Type = ArcDataGridColumnType.Select, Options = Regions },
        new() { Key = "stage", Header = "Stage", Width = 132, Type = ArcDataGridColumnType.Select, Options = Stages },
        new() { Key = "seats", Header = "Seats", Width = 96, Type = ArcDataGridColumnType.Number },
        new() { Key = "arr", Header = "ARR", Width = 128, Type = ArcDataGridColumnType.Currency },
        new() { Key = "health", Header = "Health", Width = 104, Type = ArcDataGridColumnType.Percent },
        new() { Key = "renews", Header = "Renews", Width = 132, Type = ArcDataGridColumnType.Date },
    ];

    private static List<ArcDataGridRow> LoadRows()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("renewals.csv.gz")!;
        using var unzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(unzip);
        return ArcDataGridModel.ParseCsv(reader.ReadToEnd(), BuildColumns());
    }
}
