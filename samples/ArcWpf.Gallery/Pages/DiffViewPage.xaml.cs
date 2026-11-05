using System.Linq;
using System.Windows.Controls;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class DiffViewPage : UserControl
{
    private const string Before = """
        import { retry } from "./retry";

        export async function deliver(event: WebhookEvent) {
          const url = event.target;
          const body = JSON.stringify(event.payload);

          // Sent once; a failed call is only logged.
          const response = await fetch(url, { method: "POST", body });
          if (!response.ok) {
            console.error("Webhook failed", response.status);
          }
          return response.status;
        }

        export function describe(event: WebhookEvent) {
          return `${event.kind} to ${event.target}`;
        }

        export const TIMEOUT = 30;
        export const NAME = "webhooks";
        export const VERSION = 2;
        export const ENABLED = true;
        export const LIMIT = 100;

        """;

    private const string After = """
        import { retry } from "./retry";
        import { sign } from "./sign";

        export async function deliver(event: WebhookEvent) {
          const url = event.target;
          const body = JSON.stringify(event.payload);
          const signature = sign(body);

          // Retried with backoff until the receiver accepts it.
          const response = await retry(() => fetch(url, { method: "POST", body, headers: { signature } }), { attempts: 5 });
          return response.status;
        }

        export function describe(event: WebhookEvent) {
          return `${event.kind} to ${event.target}`;
        }

        export const TIMEOUT = 30;
        export const NAME = "webhooks";
        export const VERSION = 2;
        export const ENABLED = true;
        export const LIMIT = 250;

        """;

    private const string GitPatch = """
        diff --git a/src/old-name.ts b/src/new-name.ts
        similarity index 88%
        rename from src/old-name.ts
        rename to src/new-name.ts
        old mode 100644
        new mode 100755
        --- a/src/old-name.ts
        +++ b/src/new-name.ts
        @@ -12,4 +12,4 @@ export function load() {
           const value = read();
        -  return value;
        +  return value ?? fallback;
         }

        @@ -90,3 +90,4 @@ export function save() {
           write(value);
        +  flush();
         }

        """;

    public DiffViewPage()
    {
        InitializeComponent();
        Main.Files = new[]
        {
            DiffFile.FromText("src/webhooks/deliver.ts", Before, After),
            DiffFile.FromText("src/webhooks/sign.ts", null, "import { createHmac } from \"crypto\";\n\nexport function sign(body: string) {\n  return createHmac(\"sha256\", process.env.SECRET).update(body).digest(\"hex\");\n}\n"),
            DiffFile.FromText("src/webhooks/legacy.ts", "export const legacy = true;\n", null),
        };
        Patch.Files = DiffFile.ParsePatch(GitPatch);
        Careful.Files = new[]
        {
            DiffFile.FromText("auth.ts", "if (isAdmin) {\n  grant();\n}\n", "if (isAdmin) { /*‮ } ⁦if (true)⁩ ⁦*/\n  grant();\n}\n"),
            DiffFile.FromText("windows.txt", "one\ntwo\n", "one\r\ntwo\r\n"),
            DiffFile.FromText("logo.png", "\u0089PNG\0\0", "\u0089PNG\0\u0001"),
        };
        string[] lines = Enumerable.Range(1, 20000).Select(i => $"line {i}: the quick brown fox").ToArray();
        string[] edited = lines.ToArray();
        for (int i = 50; i < edited.Length; i += 400) { edited[i] += " jumps over"; }
        Large.Files = new[] { DiffFile.FromText("data/large.txt", string.Join("\n", lines), string.Join("\n", edited)) };
    }
}
