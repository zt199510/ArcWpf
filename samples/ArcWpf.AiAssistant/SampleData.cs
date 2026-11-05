using System.Windows.Media;
using ArcWpf.AiAssistant.Backend;
using ArcWpf.Controls;

namespace ArcWpf.AiAssistant;

// The canned content of the simulated assistant. The brand marks are Simple Icons paths (CC0); the library itself ships none.
internal static class SampleData
{
    private static class Paths
    {
        public const string GitHub = "M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12";
        public const string Postgres = "M23.5594 14.7228a.5269.5269 0 0 0-.0563-.1191c-.139-.2632-.4768-.3418-1.0074-.2321-1.6533.3411-2.2935.1312-2.5256-.0191 1.342-2.0482 2.445-4.522 3.0411-6.8297.2714-1.0507.7982-3.5237.1222-4.7316a1.5641 1.5641 0 0 0-.1509-.235C21.6931.9086 19.8007.0248 17.5099.0005c-1.4947-.0158-2.7705.3461-3.1161.4794a9.449 9.449 0 0 0-.5159-.0816 8.044 8.044 0 0 0-1.3114-.1278c-1.1822-.0184-2.2038.2642-3.0498.8406-.8573-.3211-4.7888-1.645-7.2219.0788C.9359 2.1526.3086 3.8733.4302 6.3043c.0409.818.5069 3.334 1.2423 5.7436.4598 1.5065.9387 2.7019 1.4334 3.582.553.9942 1.1259 1.5933 1.7143 1.7895.4474.1491 1.1327.1441 1.8581-.7279.8012-.9635 1.5903-1.8258 1.9446-2.2069.4351.2355.9064.3625 1.39.3772a.0569.0569 0 0 0 .0004.0041 11.0312 11.0312 0 0 0-.2472.3054c-.3389.4302-.4094.5197-1.5002.7443-.3102.064-1.1344.2339-1.1464.8115-.0025.1224.0329.2309.0919.3268.2269.4231.9216.6097 1.015.6331 1.3345.3335 2.5044.092 3.3714-.6787-.017 2.231.0775 4.4174.3454 5.0874.2212.5529.7618 1.9045 2.4692 1.9043.2505 0 .5263-.0291.8296-.0941 1.7819-.3821 2.5557-1.1696 2.855-2.9059.1503-.8707.4016-2.8753.5388-4.1012.0169-.0703.0357-.1207.057-.1362.0007-.0005.0697-.0471.4272.0307a.3673.3673 0 0 0 .0443.0068l.2539.0223.0149.001c.8468.0384 1.9114-.1426 2.5312-.4308.6438-.2988 1.8057-1.0323 1.5951-1.6698zM2.371 11.8765c-.7435-2.4358-1.1779-4.8851-1.2123-5.5719-.1086-2.1714.4171-3.6829 1.5623-4.4927 1.8367-1.2986 4.8398-.5408 6.108-.13-.0032.0032-.0066.0061-.0098.0094-2.0238 2.044-1.9758 5.536-1.9708 5.7495-.0002.0823.0066.1989.0162.3593.0348.5873.0996 1.6804-.0735 2.9184-.1609 1.1504.1937 2.2764.9728 3.0892.0806.0841.1648.1631.2518.2374-.3468.3714-1.1004 1.1926-1.9025 2.1576-.5677.6825-.9597.5517-1.0886.5087-.3919-.1307-.813-.5871-1.2381-1.3223-.4796-.839-.9635-2.0317-1.4155-3.5126zm6.0072 5.0871c-.1711-.0428-.3271-.1132-.4322-.1772.0889-.0394.2374-.0902.4833-.1409 1.2833-.2641 1.4815-.4506 1.9143-1.0002.0992-.126.2116-.2687.3673-.4426a.3549.3549 0 0 0 .0737-.1298c.1708-.1513.2724-.1099.4369-.0417.156.0646.3078.26.3695.4752.0291.1016.0619.2945-.0452.4444-.9043 1.2658-2.2216 1.2494-3.1676 1.0128zm2.094-3.988-.0525.141c-.133.3566-.2567.6881-.3334 1.003-.6674-.0021-1.3168-.2872-1.8105-.8024-.6279-.6551-.9131-1.5664-.7825-2.5004.1828-1.3079.1153-2.4468.079-3.0586-.005-.0857-.0095-.1607-.0122-.2199.2957-.2621 1.6659-.9962 2.6429-.7724.4459.1022.7176.4057.8305.928.5846 2.7038.0774 3.8307-.3302 4.7363-.084.1866-.1633.3629-.2311.5454zm7.3637 4.5725c-.0169.1768-.0358.376-.0618.5959l-.146.4383a.3547.3547 0 0 0-.0182.1077c-.0059.4747-.054.6489-.115.8693-.0634.2292-.1353.4891-.1794 1.0575-.11 1.4143-.8782 2.2267-2.4172 2.5565-1.5155.3251-1.7843-.4968-2.0212-1.2217a6.5824 6.5824 0 0 0-.0769-.2266c-.2154-.5858-.1911-1.4119-.1574-2.5551.0165-.5612-.0249-1.9013-.3302-2.6462.0044-.2932.0106-.5909.019-.8918a.3529.3529 0 0 0-.0153-.1126 1.4927 1.4927 0 0 0-.0439-.208c-.1226-.4283-.4213-.7866-.7797-.9351-.1424-.059-.4038-.1672-.7178-.0869.067-.276.1831-.5875.309-.9249l.0529-.142c.0595-.16.134-.3257.213-.5012.4265-.9476 1.0106-2.2453.3766-5.1772-.2374-1.0981-1.0304-1.6343-2.2324-1.5098-.7207.0746-1.3799.3654-1.7088.5321a5.6716 5.6716 0 0 0-.1958.1041c.0918-1.1064.4386-3.1741 1.7357-4.4823a4.0306 4.0306 0 0 1 .3033-.276.3532.3532 0 0 0 .1447-.0644c.7524-.5706 1.6945-.8506 2.802-.8325.4091.0067.8017.0339 1.1742.081 1.939.3544 3.2439 1.4468 4.0359 2.3827.8143.9623 1.2552 1.9315 1.4312 2.4543-1.3232-.1346-2.2234.1268-2.6797.779-.9926 1.4189.543 4.1729 1.2811 5.4964.1353.2426.2522.4522.2889.5413.2403.5825.5515.9713.7787 1.2552.0696.087.1372.1714.1885.245-.4008.1155-1.1208.3825-1.0552 1.717-.0123.1563-.0423.4469-.0834.8148-.0461.2077-.0702.4603-.0994.7662zm.8905-1.6211c-.0405-.8316.2691-.9185.5967-1.0105a2.8566 2.8566 0 0 0 .135-.0406 1.202 1.202 0 0 0 .1342.103c.5703.3765 1.5823.4213 3.0068.1344-.2016.1769-.5189.3994-.9533.6011-.4098.1903-1.0957.333-1.7473.3636-.7197.0336-1.0859-.0807-1.1721-.151zm.5695-9.2712c-.0059.3508-.0542.6692-.1054 1.0017-.055.3576-.112.7274-.1264 1.1762-.0142.4368.0404.8909.0932 1.3301.1066.887.216 1.8003-.2075 2.7014a3.5272 3.5272 0 0 1-.1876-.3856c-.0527-.1276-.1669-.3326-.3251-.6162-.6156-1.1041-2.0574-3.6896-1.3193-4.7446.3795-.5427 1.3408-.5661 2.1781-.463zm.2284 7.0137a12.3762 12.3762 0 0 0-.0853-.1074l-.0355-.0444c.7262-1.1995.5842-2.3862.4578-3.4385-.0519-.4318-.1009-.8396-.0885-1.2226.0129-.4061.0666-.7543.1185-1.0911.0639-.415.1288-.8443.1109-1.3505.0134-.0531.0188-.1158.0118-.1902-.0457-.4855-.5999-1.938-1.7294-3.253-.6076-.7073-1.4896-1.4972-2.6889-2.0395.5251-.1066 1.2328-.2035 2.0244-.1859 2.0515.0456 3.6746.8135 4.8242 2.2824a.908.908 0 0 1 .0667.1002c.7231 1.3556-.2762 6.2751-2.9867 10.5405zm-8.8166-6.1162c-.025.1794-.3089.4225-.6211.4225a.5821.5821 0 0 1-.0809-.0056c-.1873-.026-.3765-.144-.5059-.3156-.0458-.0605-.1203-.178-.1055-.2844.0055-.0401.0261-.0985.0925-.1488.1182-.0894.3518-.1226.6096-.0867.3163.0441.6426.1938.6113.4186zm7.9305-.4114c.0111.0792-.049.201-.1531.3102-.0683.0717-.212.1961-.4079.2232a.5456.5456 0 0 1-.075.0052c-.2935 0-.5414-.2344-.5607-.3717-.024-.1765.2641-.3106.5611-.352.297-.0414.6111.0088.6356.1851z";
        public const string OpenAI = "M22.2819 9.8211a5.9847 5.9847 0 0 0-.5157-4.9108 6.0462 6.0462 0 0 0-6.5098-2.9A6.0651 6.0651 0 0 0 4.9807 4.1818a5.9847 5.9847 0 0 0-3.9977 2.9 6.0462 6.0462 0 0 0 .7427 7.0966 5.98 5.98 0 0 0 .511 4.9107 6.051 6.051 0 0 0 6.5146 2.9001A5.9847 5.9847 0 0 0 13.2599 24a6.0557 6.0557 0 0 0 5.7718-4.2058 5.9894 5.9894 0 0 0 3.9977-2.9001 6.0557 6.0557 0 0 0-.7475-7.0729zm-9.022 12.6081a4.4755 4.4755 0 0 1-2.8764-1.0408l.1419-.0804 4.7783-2.7582a.7948.7948 0 0 0 .3927-.6813v-6.7369l2.02 1.1686a.071.071 0 0 1 .038.052v5.5826a4.504 4.504 0 0 1-4.4945 4.4944zm-9.6607-4.1254a4.4708 4.4708 0 0 1-.5346-3.0137l.142.0852 4.783 2.7582a.7712.7712 0 0 0 .7806 0l5.8428-3.3685v2.3324a.0804.0804 0 0 1-.0332.0615L9.74 19.9502a4.4992 4.4992 0 0 1-6.1408-1.6464zM2.3408 7.8956a4.485 4.485 0 0 1 2.3655-1.9728V11.6a.7664.7664 0 0 0 .3879.6765l5.8144 3.3543-2.0201 1.1685a.0757.0757 0 0 1-.071 0l-4.8303-2.7865A4.504 4.504 0 0 1 2.3408 7.872zm16.5963 3.8558L13.1038 8.364 15.1192 7.2a.0757.0757 0 0 1 .071 0l4.8303 2.7913a4.4944 4.4944 0 0 1-.6765 8.1042v-5.6772a.79.79 0 0 0-.407-.667zm2.0107-3.0231l-.142-.0852-4.7735-2.7818a.7759.7759 0 0 0-.7854 0L9.409 9.2297V6.8974a.0662.0662 0 0 1 .0284-.0615l4.8303-2.7866a4.4992 4.4992 0 0 1 6.6802 4.66zM8.3065 12.863l-2.02-1.1638a.0804.0804 0 0 1-.038-.0567V6.0742a4.4992 4.4992 0 0 1 7.3757-3.4537l-.142.0805L8.704 5.459a.7948.7948 0 0 0-.3927.6813zm1.0976-2.3654l2.602-1.4998 2.6069 1.4998v2.9994l-2.5974 1.4997-2.6067-1.4997Z";
        public const string Gemini = "M11.04 19.32Q12 21.51 12 24q0-2.49.93-4.68.96-2.19 2.58-3.81t3.81-2.55Q21.51 12 24 12q-2.49 0-4.68-.93a12.3 12.3 0 0 1-3.81-2.58 12.3 12.3 0 0 1-2.58-3.81Q12 2.49 12 0q0 2.49-.96 4.68-.93 2.19-2.55 3.81a12.3 12.3 0 0 1-3.81 2.58Q2.49 12 0 12q2.49 0 4.68.96 2.19.93 3.81 2.55t2.55 3.81";
        public const string Stripe = "M13.976 9.15c-2.172-.806-3.356-1.426-3.356-2.409 0-.831.683-1.305 1.901-1.305 2.227 0 4.515.858 6.09 1.631l.89-5.494C18.252.975 15.697 0 12.165 0 9.667 0 7.589.654 6.104 1.872 4.56 3.147 3.757 4.992 3.757 7.218c0 4.039 2.467 5.76 6.476 7.219 2.585.92 3.445 1.574 3.445 2.583 0 .98-.84 1.545-2.354 1.545-1.875 0-4.965-.921-6.99-2.109l-.9 5.555C5.175 22.99 8.385 24 11.714 24c2.641 0 4.843-.624 6.328-1.813 1.664-1.305 2.525-3.236 2.525-5.732 0-4.128-2.524-5.851-6.594-7.305h.003z";
        public const string Claude = "m4.7144 15.9555 4.7174-2.6471.079-.2307-.079-.1275h-.2307l-.7893-.0486-2.6956-.0729-2.3375-.0971-2.2646-.1214-.5707-.1215-.5343-.7042.0546-.3522.4797-.3218.686.0608 1.5179.1032 2.2767.1578 1.6514.0972 2.4468.255h.3886l.0546-.1579-.1336-.0971-.1032-.0972L6.973 9.8356l-2.55-1.6879-1.3356-.9714-.7225-.4918-.3643-.4614-.1578-1.0078.6557-.7225.8803.0607.2246.0607.8925.686 1.9064 1.4754 2.4893 1.8336.3643.3035.1457-.1032.0182-.0728-.164-.2733-1.3539-2.4467-1.445-2.4893-.6435-1.032-.17-.6194c-.0607-.255-.1032-.4674-.1032-.7285L6.287.1335 6.6997 0l.9957.1336.419.3642.6192 1.4147 1.0018 2.2282 1.5543 3.0296.4553.8985.2429.8318.091.255h.1579v-.1457l.1275-1.706.2368-2.0947.2307-2.6957.0789-.7589.3764-.9107.7468-.4918.5828.2793.4797.686-.0668.4433-.2853 1.8517-.5586 2.9021-.3643 1.9429h.2125l.2429-.2429.9835-1.3053 1.6514-2.0643.7286-.8196.85-.9046.5464-.4311h1.0321l.759 1.1293-.34 1.1657-1.0625 1.3478-.8804 1.1414-1.2628 1.7-.7893 1.36.0729.1093.1882-.0183 2.8535-.607 1.5421-.2794 1.8396-.3157.8318.3886.091.3946-.3278.8075-1.967.4857-2.3072.4614-3.4364.8136-.0425.0304.0486.0607 1.5482.1457.6618.0364h1.621l3.0175.2247.7892.522.4736.6376-.079.4857-1.2142.6193-1.6393-.3886-3.825-.9107-1.3113-.3279h-.1822v.1093l1.0929 1.0686 2.0035 1.8092 2.5075 2.3314.1275.5768-.3218.4554-.34-.0486-2.2039-1.6575-.85-.7468-1.9246-1.621h-.1275v.17l.4432.6496 2.3436 3.5214.1214 1.0807-.17.3521-.6071.2125-.6679-.1214-1.3721-1.9246L14.38 17.959l-1.1414-1.9428-.1397.079-.674 7.2552-.3156.3703-.7286.2793-.6071-.4614-.3218-.7468.3218-1.4753.3886-1.9246.3157-1.53.2853-1.9004.17-.6314-.0121-.0425-.1397.0182-1.4328 1.9672-2.1796 2.9446-1.7243 1.8456-.4128.164-.7164-.3704.0667-.6618.4008-.5889 2.386-3.0357 1.4389-1.882.929-1.0868-.0062-.1579h-.0546l-6.3385 4.1164-1.1293.1457-.4857-.4554.0608-.7467.2307-.2429 1.9064-1.3114Z";
    }

    public static readonly AiMark Claude = new(Paths.Claude, Color.FromRgb(0xD9, 0x77, 0x57));
    public static readonly AiMark OpenAI = new(Paths.OpenAI);
    public static readonly AiMark Gemini = new(Paths.Gemini, Color.FromRgb(0x42, 0x85, 0xF4));
    public static readonly AiMark GitHub = new(Paths.GitHub);
    public static readonly AiMark Postgres = new(Paths.Postgres, Color.FromRgb(0x41, 0x69, 0xE1));
    public static readonly AiMark Stripe = new(Paths.Stripe, Color.FromRgb(0x63, 0x5B, 0xFF));

    public static readonly PickerProvider[] Providers = [new("anthropic", "Anthropic", Claude), new("openai", "OpenAI", OpenAI), new("google", "Google", Gemini)];

    public static readonly PickerModel[] Models =
    [
        new("opus-5.5", "Claude Opus 5.5", "anthropic") { Short = "Opus 5.5", Mark = Claude, Blurb = "Complex coding, agents and analysis" },
        new("sonnet-5.5", "Claude Sonnet 5.5", "anthropic") { Short = "Sonnet 5.5", Mark = Claude, Blurb = "Smart and efficient for everyday work" },
        new("gpt-6.1", "GPT-6.1 Sol", "openai") { Short = "GPT-6.1", Mark = OpenAI, Blurb = "General reasoning across text and images" },
        new("gemini-4", "Gemini 4 Argon", "google") { Short = "Gemini 4", Mark = Gemini, Blurb = "Long context, reads whole repositories" },
    ];

    public static readonly string[] Featured = ["opus-5.5", "sonnet-5.5", "gpt-6.1", "gemini-4"];

    public static readonly PromptCommand[] Commands =
    [
        new("explain", "explain", "Walk through code or an idea step by step", AiGlyphs.Lightbulb),
        new("fix", "fix", "Find and fix a bug in the code", AiGlyphs.Bug),
        new("summarize", "summarize", "Condense text or a file into key points", AiGlyphs.FileText),
    ];

    public static readonly PromptMentionGroup[] Mentions =
    [
        new("Files", [new("route", "route.ts", "src/app/api/webhook"), new("checkout", "checkout.tsx", "src/app/checkout")]),
    ];

    public static readonly PromptAttachAction[] AttachActions =
    [
        new("upload", "Upload file", AiGlyphs.Paperclip),
        new("sample", "Add a sample log", AiGlyphs.FileText),
        new("flaky", "Add a file that fails once", AiGlyphs.Bug),
    ];

    public static PromptTool[] Tools() => [new("web", "Web search", AiGlyphs.Globe)];

    public static readonly (string Label, string Prompt)[] Starters =
    [
        ("Fix a webhook, with a tool approval", "Fix the Stripe webhook that fails signature checks"),
        ("Check migrations, with a tool that fails", "Check the pending database migrations"),
        ("Simulate a connection drop", "Simulate a connection drop while you answer"),
    ];

    public const string WebhookReasoning = """
        **Reading the webhook handler**
        The handler parses the body as JSON before it verifies the signature, so the signed bytes no longer match.

        **Choosing a fix**
        Read the raw body first, verify it, and only then parse the event.
        """;

    public static readonly ToolCallData SearchCall = new("search", "GitHub", "Searched issues in arc-ui/web", ToolCallStatus.Success)
    {
        Mark = GitHub, DurationMs = 880,
        Titles = new Dictionary<ToolCallStatus, string> { [ToolCallStatus.Running] = "Searching issues in arc-ui/web" },
        Input = [new("repo", "arc-ui/web"), new("query", "webhook signature")],
        Output = new ToolOutput.List([new("#1291", "Webhook signature fails after a JSON parse", "billing"), new("#1284", "Webhook retry posts a second charge", "billing")]),
    };

    public static readonly ToolCallData ReadCall = new("read", "Files", "Read src/app/api/webhook/route.ts", ToolCallStatus.Success)
    {
        DurationMs = 640,
        Titles = new Dictionary<ToolCallStatus, string> { [ToolCallStatus.Running] = "Reading src/app/api/webhook/route.ts" },
        Input = [new("path", "src/app/api/webhook/route.ts")],
        Output = new ToolOutput.Text("const event = await request.json();\nstripe.webhooks.constructEvent(JSON.stringify(event), signature, secret);"),
    };

    public static ApprovalRequest PatchRequest(string? modelName, AiMark? modelMark) => new()
    {
        AgentName = "Assistant",
        ModelName = modelName,
        ModelMark = modelMark,
        Asked = "just now",
        Title = "Apply a patch to `route.ts`",
        Reason = "Writes to the working tree. The change reads the raw body before it verifies the signature.",
        Risk = ApprovalRisk.Medium,
        Arguments =
        [
            new ApprovalArgument("path", "src/app/api/webhook/route.ts"),
            new ApprovalArgument("change", "+3 -2 lines") { IsEditable = false, IsMono = false },
        ],
    };

    public static ToolCallData ApplyCall(string path) => new("apply", "Files", $"Patched {path}", ToolCallStatus.Success)
    {
        DurationMs = 1080, Summary = "+3 -2",
        Titles = new Dictionary<ToolCallStatus, string> { [ToolCallStatus.Running] = $"Patching {path}" },
        Input = [new("path", path)],
        Output = new ToolOutput.Text("Simulated: no file was changed."),
    };

    public static readonly CitationSource[] WebhookSources =
    [
        new("Check the webhook signatures", "https://docs.stripe.com/webhooks/signature") { Site = "stripe.com", Mark = Stripe, Snippet = "Stripe needs the raw body of the request to verify the signature." },
        new("Request: text() method", "https://developer.mozilla.org/docs/Web/API/Request/text") { Site = "mozilla.org", Snippet = "The text() method of the Request interface reads the request body and returns it as a string." },
    ];

    public static readonly ArtifactItem[] PatchArtifacts =
    [
        new()
        {
            Title = "Patch", Kind = ArtifactKind.Code, Language = "ts", FileName = "route.ts",
            Content = """
                export async function POST(request: Request) {
                  const body = await request.text();
                  const signature = request.headers.get("stripe-signature")!;
                  const event = stripe.webhooks.constructEvent(body, signature, secret);
                  await handle(event);
                  return new Response(null, { status: 200 });
                }
                """,
        },
        new()
        {
            Title = "Report", Kind = ArtifactKind.Json,
            Content = """{"simulated":true,"file":"src/app/api/webhook/route.ts","added":3,"removed":2,"checks":{"types":"not run","tests":"not run"}}""",
        },
        new()
        {
            Title = "Summary", Kind = ArtifactKind.Markdown,
            Content = """
                ## Webhook signature fix

                The handler now verifies the **raw body**.

                - Read the body with `request.text()`.
                - Verify it before anything parses it.
                - Parse only the verified event.
                """,
        },
        new() { Title = "File", Kind = ArtifactKind.File, FileName = "webhook-fix.patch", Detail = "Sample file, 412 bytes" },
    ];

    public const string WebhookAnswer = """
        The signature check fails because the handler **parses the body first** [1]. Stripe signs the raw bytes, and `JSON.stringify` does not give them back.

        I changed the handler in three steps:

        1. Read the body with `request.text()` [2].
        2. Verify that text with `constructEvent`.
        3. Handle the event the check returns.

        The patch, a report and a summary are in the results pane. This is a simulated run, so no file changed.
        """;

    public const string DeniedAnswer = "Understood, I left the file as it is. The cause stays the same: the handler parses the body before it verifies the signature. Use **Request again** on the card if you change your mind.";

    public static readonly ToolCallData MigrationCall = new("migrations", "PostgreSQL", "Queried pending migrations", ToolCallStatus.Running)
    {
        Mark = Postgres,
        Titles = new Dictionary<ToolCallStatus, string> { [ToolCallStatus.Running] = "Querying pending migrations", [ToolCallStatus.Error] = "Could not query pending migrations" },
        Input = [new("database", "app_production"), new("timeout", "5s")],
    };

    public static readonly ToolOutput MigrationOutput = new ToolOutput.Table(["Migration", "State"], [["0042_processed_events", "Pending"], ["0043_refund_index", "Pending"]]);

    public static readonly ArtifactItem MigrationArtifact = new()
    {
        Title = "Migrations", Kind = ArtifactKind.Json,
        Content = """{"simulated":true,"database":"app_production","pending":["0042_processed_events","0043_refund_index"]}""",
    };

    public const string MigrationFailed = "The database did not answer in time, so I have no list yet. Use **Retry** on the tool card to run this turn again.";
    public const string MigrationAnswer = "There are **two pending migrations**: `0042_processed_events` and `0043_refund_index`. Neither has been applied. This is a simulated result.";

    public const string DroppedStart = "Here is the start of an answer that will not reach the end, because the simulated connection breaks off";
    public const string DroppedAnswer = "This attempt finished. **Retry** starts the reply again from the same question and keeps nothing from the failed attempt.";

    public static string PlainAnswer(AssistantRequest request)
    {
        string files = request.Files.Count > 0 ? $" I can see {string.Join(", ", request.Files.Select(file => $"`{file.Name}`"))} attached, but a simulated assistant does not read them." : "";
        string web = request.Tools.Contains("web") ? " Web search is on, so the sources below are sample links." : "";
        string model = request.ModelName is { } name ? $" You picked **{name}**, which changes the label only." : "";
        return $"This is a simulated reply: no model ran.{model}{files}{web}\n\nAsk about a **webhook**, **migrations** or a **connection drop** to see tools, approvals and failures.";
    }

    /// <summary>Earlier chats, so that the sidebar has something to search, rename and archive.</summary>
    public static readonly (string Id, string Title, string Group, string Question, string Answer, bool Archived)[] History =
    [
        ("launch", "Q4 launch checklist", "Yesterday", "What is left before the Q4 launch?", "Three items are open in this sample: **pricing copy**, the `checkout` smoke test and the status page. Nothing here comes from a real project.", false),
        ("rename", "Rename the billing module", "Previous 7 days", "How should I rename the billing module?", "Rename the folder first, then let the compiler list every import. Keep the old name as a re-export for one release.", false),
        ("retries", "Retry policy notes", "Previous 7 days", "When should a client retry?", "Retry on timeouts and 5xx answers with a growing delay, and never on 4xx answers.", false),
        ("old", "Old pricing ideas", "Previous 7 days", "Any pricing ideas?", "This archived chat is here to show the restore flow.", true),
    ];
}
