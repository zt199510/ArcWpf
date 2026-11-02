using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class JSONViewerPage : UserControl
{
    private static readonly string[] EventTypes = ["page_view", "add_to_cart", "checkout_started", "payment_authorized", "email_opened"];
    private static readonly string[] Agents = ["Chrome 128 on macOS", "Safari on iPhone", "Firefox 130 on Windows"];

    public JSONViewerPage()
    {
        InitializeComponent();
        Viewer.Data = new Dictionary<string, object?>
        {
            ["id"] = "ord_8Kd2Q7",
            ["object"] = "order",
            ["status"] = "fulfilled",
            ["livemode"] = false,
            ["created"] = 1757851200L,
            ["currency"] = "usd",
            ["amount_total"] = 48600,
            ["customer"] = new Dictionary<string, object?>
            {
                ["id"] = "cus_R3v9",
                ["name"] = "Emma Collins",
                ["email"] = "emma.collins@example.com",
                ["address"] = new Dictionary<string, object?> { ["line1"] = "2150 NW Lovejoy St", ["city"] = "Portland", ["state"] = "OR", ["postal_code"] = "97210", ["country"] = "US" },
                ["tags"] = new List<object?> { "returning", "newsletter" },
            },
            ["items"] = new List<object?>
            {
                Item("OAK-LC-01", "Oak lounge chair", 1, 38000),
                Item("LIN-TH-04", "Linen throw, natural", 2, 4800),
                Item("STN-CP-02", "Stoneware cups, set of 2", 1, 1000),
            },
            ["shipping"] = new Dictionary<string, object?> { ["carrier"] = "UPS", ["service"] = "Ground", ["tracking"] = "1Z999AA10123456784", ["delivered_at"] = null },
            ["discount"] = null,
            ["metadata"] = new Dictionary<string, object?> { ["campaign id"] = "fall-26", ["gift wrap"] = true },
            ["events"] = Enumerable.Range(0, 180).Select(index => (object?)new Dictionary<string, object?>
            {
                ["id"] = $"evt_{(4473 + index * 17).ToString("x4", CultureInfo.InvariantCulture)}",
                ["type"] = EventTypes[index % EventTypes.Length],
                ["at"] = new System.DateTime(2026, 9, 14, 0, 0, 0, System.DateTimeKind.Utc).AddMinutes(index * 7).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["agent"] = Agents[index % Agents.Length],
            }).ToList(),
        };
    }

    private static Dictionary<string, object?> Item(string sku, string name, int quantity, int amount) =>
        new() { ["sku"] = sku, ["name"] = name, ["quantity"] = quantity, ["unit_amount"] = amount };
}
