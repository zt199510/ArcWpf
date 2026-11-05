using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class ArtifactPreviewPage : UserControl
{
    public ArtifactPreviewPage()
    {
        InitializeComponent();
        Preview.Items.Add(new ArtifactItem
        {
            Title = "Code", Kind = ArtifactKind.Code, FileName = "region.sql", Language = "sql",
            Content = "ALTER TABLE orders\n  ADD COLUMN region text;\n\nCREATE INDEX CONCURRENTLY orders_region_idx\n  ON orders (region, created_at);\n\nUPDATE orders SET region = 'eu'\n  WHERE country IN ('DE', 'FR');",
        });
        Preview.Items.Add(new ArtifactItem
        {
            Title = "JSON", Kind = ArtifactKind.Json, FileName = "plan.json",
            Content = "{\"table\":\"orders\",\"rows\":4200000,\"online\":true,\"steps\":[{\"name\":\"add column\",\"lock\":\"brief\"},{\"name\":\"build index\",\"lock\":\"none\"}],\"rollback\":null}",
        });
        Preview.Items.Add(new ArtifactItem
        {
            Title = "Markdown", Kind = ArtifactKind.Markdown, FileName = "summary.md",
            Content = "## Rollout\n\nAdd the column first, then build the index **without** taking a write lock.\n\n1. Run `ALTER TABLE` during the quiet hour.\n2. Build the index concurrently.\n3. Backfill in batches.\n\n- Takes about 40 minutes\n- Safe to pause between batches",
        });
        Preview.Items.Add(new ArtifactItem
        {
            Title = "File", Kind = ArtifactKind.File, FileName = "orders-backfill.csv", Detail = "2.4 MB  ·  CSV",
            Content = "id,region,created_at\n1001,eu,2026-10-02\n1002,us,2026-10-02\n1003,eu,2026-10-03",
        });
        Preview.OpenRequested += (_, e) => Log.Text = "Open " + e.Item.FileName;
        Preview.DownloadRequested += (_, e) => Log.Text = "Download " + e.Item.FileName;
    }
}
