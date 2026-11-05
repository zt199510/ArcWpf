using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using ArcWpf.AiAssistant;
using ArcWpf.AiAssistant.Backend;
using ArcWpf.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pi.Agent;
using Pi.Ai.Providers;
using Pi.Ai.Types;
using ChatMessage = Pi.Ai.Types.ChatMessage;

namespace ArcWpf.Tests;

[TestClass]
public sealed class PiAgentBackendTests
{
    private static readonly AssistantRequest Ask = new("hello", [], null, "Faux", null, []);
    private static readonly AssistantProfile Profile = new() { Badge = "Test", Models = [new PickerModel("faux-1", "Faux", "faux")] };

    private sealed class Script
    {
        public List<string> Ran { get; } = [];
        public List<JsonElement> Inputs { get; } = [];
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];
        public TaskCompletionSource ToolStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IAssistantBackend Backend(params Faux.FauxResponseStep[] steps)
        {
            var inner = Faux.CreateStreamFn(steps);
            return new PiAgentBackend(Profile, (model, context, options, cancellation) =>
            {
                Requests.Add([.. context.Messages]);
                return inner(model, context, options, cancellation);
            }, _ => Faux.DefaultModel, Tools(), "system");
        }

        private IReadOnlyList<AssistantTool> Tools() =>
        [
            new("read_it", "Read it", "reads", new Dictionary<string, object?> { ["type"] = "object" }, (input, _) => { Ran.Add("read_it"); return Task.FromResult(new AssistantToolResult("read result")); }),
            new("write_it", "Write it", "writes", new Dictionary<string, object?> { ["type"] = "object" }, (input, _) =>
            {
                Ran.Add("write_it");
                Inputs.Add(input);
                return Task.FromResult(new AssistantToolResult("written", new ArtifactItem { Title = "Out", Kind = ArtifactKind.Markdown, Content = "x" }));
            }) { RequiresApproval = true },
            new("slow_it", "Slow it", "waits", new Dictionary<string, object?> { ["type"] = "object" }, async (input, cancellation) =>
            {
                ToolStarted.TrySetResult();
                await Task.Delay(TimeSpan.FromSeconds(30), cancellation);
                return new AssistantToolResult("never");
            }),
        ];
    }

    private static Faux.Response Call(string name, object? input = null, string id = "c1") => Faux.Response.ToolUse(new ToolCallContent(id, name, input ?? new { }));

    private static List<AssistantEvent> Collect(IAssistantSession session, AssistantRequest request, Func<ApprovalRequest, ApprovalOutcome>? answer = null, params (string Key, string Value)[] edits)
    {
        var events = new List<AssistantEvent>();
        Task.Run(async () =>
        {
            await foreach (var item in session.RunAsync(request, CancellationToken.None))
            {
                events.Add(item);
                if (item is AssistantEvent.ApprovalNeeded ask)
                {
                    var arguments = ask.Request.Arguments.ToDictionary(field => field.Label, field => field.Value);
                    foreach (var (key, value) in edits) { arguments[key] = value; }
                    ask.Answer.TrySetResult(new ApprovalDecision(ask.Request, answer!(ask.Request), arguments, edits.Length > 0));
                }
            }
        }).Wait(TimeSpan.FromSeconds(20));
        return events;
    }

    private static string Words(IEnumerable<AssistantEvent> events) => string.Concat(events.OfType<AssistantEvent.Text>().Select(item => item.Delta));
    private static IEnumerable<ToolCallStatus> Statuses(IEnumerable<AssistantEvent> events) => events.OfType<AssistantEvent.Tool>().Select(item => item.Call.Status);

    [TestMethod]
    public void AReadOnlyToolRunsWithoutACard()
    {
        var script = new Script();
        var events = Collect(script.Backend(Call("read_it"), Faux.Response.Text("done")).CreateSession(), Ask);
        Assert.IsFalse(events.OfType<AssistantEvent.ApprovalNeeded>().Any());
        CollectionAssert.AreEqual(new[] { ToolCallStatus.Running, ToolCallStatus.Success }, Statuses(events).ToArray());
        Assert.AreEqual("done", Words(events));
    }

    [TestMethod]
    public void AToolThatNeedsApprovalStaysOffTheThreadUntilAllowed()
    {
        var script = new Script();
        var events = Collect(script.Backend(Call("write_it", new { path = "a" }), Faux.Response.Text("done")).CreateSession(), Ask, _ => ApprovalOutcome.Approved);
        int card = events.FindIndex(item => item is AssistantEvent.ApprovalNeeded);
        int tool = events.FindIndex(item => item is AssistantEvent.Tool);
        Assert.IsTrue(card >= 0 && card < tool, "The card comes before the tool appears");
        CollectionAssert.AreEqual(new[] { "write_it" }, script.Ran);
        Assert.AreEqual(1, events.OfType<AssistantEvent.Artifact>().Count());
    }

    [TestMethod]
    public void TheCardShowsTheModelsArgumentsAndAnEditReachesTheTool()
    {
        var script = new Script();
        ApprovalRequest? asked = null;
        Collect(script.Backend(Call("write_it", new { path = "from-model", count = 2 }), Faux.Response.Text("done")).CreateSession(), Ask, request => { asked = request; return ApprovalOutcome.Approved; }, ("path", "from-person"));
        Assert.AreEqual("from-model", asked!.Arguments.First(field => field.Label == "path").Value);
        Assert.IsFalse(asked.Arguments.First(field => field.Label == "count").IsEditable);
        Assert.AreEqual("from-person", script.Inputs[0].GetProperty("path").GetString());
        Assert.AreEqual(2, script.Inputs[0].GetProperty("count").GetInt32());
    }

    [TestMethod]
    public void DenyingRunsNothingAndTellsTheModel()
    {
        var script = new Script();
        var events = Collect(script.Backend(Call("write_it"), Faux.Response.Text("understood")).CreateSession(), Ask, _ => ApprovalOutcome.Denied);
        Assert.AreEqual(0, script.Ran.Count);
        CollectionAssert.AreEqual(new[] { ToolCallStatus.Denied }, Statuses(events).ToArray());
        Assert.AreEqual("understood", Words(events));
        var result = script.Requests[^1].OfType<ToolResultMessage>().Single();
        StringAssert.Contains(string.Concat(result.Content.OfType<TextContent>().Select(block => block.Text)), "denied");
    }

    [TestMethod]
    public void AlwaysAllowCoversTheChatAndTheWorkspaceRuleCoversNewChats()
    {
        var script = new Script();
        var backend = script.Backend(Call("write_it"), Faux.Response.Text("a"), Call("write_it"), Faux.Response.Text("b"), Call("write_it"), Faux.Response.Text("c"), Call("write_it"), Faux.Response.Text("d"));
        var chat = backend.CreateSession();
        Assert.AreEqual(1, Collect(chat, Ask, _ => ApprovalOutcome.AllowedForAgent).OfType<AssistantEvent.ApprovalNeeded>().Count());
        Assert.AreEqual(0, Collect(chat, Ask).OfType<AssistantEvent.ApprovalNeeded>().Count());
        var other = backend.CreateSession();
        Assert.AreEqual(1, Collect(other, Ask, _ => ApprovalOutcome.AllowedForWorkspace).OfType<AssistantEvent.ApprovalNeeded>().Count());
        Assert.AreEqual(0, Collect(backend.CreateSession(), Ask).OfType<AssistantEvent.ApprovalNeeded>().Count());
        Assert.AreEqual(4, script.Ran.Count);
    }

    [TestMethod]
    public void StoppingAtTheCardRunsNothingAndTheChatCanGoOn()
    {
        var script = new Script();
        var session = script.Backend(Call("write_it"), Faux.Response.Text("next")).CreateSession();
        using var stop = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Task.Run(async () =>
        {
            await foreach (var item in session.RunAsync(Ask, stop.Token)) { if (item is AssistantEvent.ApprovalNeeded) { stop.Cancel(); } }
        }).GetAwaiter().GetResult());
        Assert.AreEqual(0, script.Ran.Count);
        Assert.AreEqual("next", Words(Collect(session, Ask with { Text = "again" })));
    }

    [TestMethod]
    public void StoppingInsideAToolLeavesNoCallWithoutAResultForTheNextRequest()
    {
        var script = new Script();
        var session = script.Backend(Call("slow_it"), Faux.Response.Text("next")).CreateSession();
        using var stop = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => Task.Run(async () =>
        {
            var reading = Task.Run(async () => { await foreach (var _ in session.RunAsync(Ask, stop.Token)) { } });
            await script.ToolStarted.Task;
            stop.Cancel();
            await reading;
        }).GetAwaiter().GetResult());

        Assert.AreEqual("next", Words(Collect(session, Ask with { Text = "again" })));
        var sent = script.Requests[^1];
        var calls = sent.OfType<AssistantMessage>().SelectMany(message => message.Content.OfType<ToolCallContent>()).Select(call => call.Id).ToList();
        var answered = sent.OfType<ToolResultMessage>().Select(result => result.ToolCallId).ToList();
        CollectionAssert.AreEquivalent(answered, calls);
        Assert.IsFalse(sent.OfType<AssistantMessage>().Any(message => message.StopReason is StopReason.Aborted or StopReason.Error));
    }

    [TestMethod]
    public void AProviderErrorBecomesAFailedReplyAndTheChatCanGoOn()
    {
        var script = new Script();
        var session = script.Backend().CreateSession();
        var thrown = Assert.ThrowsExactly<AssistantException>(() => Task.Run(async () => { await foreach (var _ in session.RunAsync(Ask, CancellationToken.None)) { } }).GetAwaiter().GetResult());
        StringAssert.Contains(thrown.Message, "No more faux responses");
        Assert.IsFalse(script.Requests[^1].OfType<AssistantMessage>().Any());
    }

    [TestMethod]
    public void ASecondAttemptReplacesTheTurnInsteadOfAddingToIt()
    {
        var script = new Script();
        var session = script.Backend(Faux.Response.Text("first"), Faux.Response.Text("first again"), Faux.Response.Text("second")).CreateSession();
        Collect(session, Ask);
        Collect(session, Ask with { Attempt = 1 });
        Assert.AreEqual(1, script.Requests[1].OfType<UserMessage>().Count());
        Assert.IsFalse(script.Requests[1].OfType<AssistantMessage>().Any());
        Collect(session, Ask with { Text = "next" });
        Assert.AreEqual(2, script.Requests[2].OfType<UserMessage>().Count());
        Assert.AreEqual(1, script.Requests[2].OfType<AssistantMessage>().Count());
    }

    [TestMethod]
    public void TextFilesGoWithTheQuestionAndOthersAreOnlyNamed()
    {
        string folder = Path.Combine(Path.GetTempPath(), "arcwpf-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        try
        {
            string text = Path.Combine(folder, "a.txt"), binary = Path.Combine(folder, "b.bin");
            File.WriteAllText(text, "inside the file");
            File.WriteAllBytes(binary, [1, 0, 2]);
            var script = new Script();
            Collect(script.Backend(Faux.Response.Text("ok")).CreateSession(), Ask with { Files = [new("a.txt", text), new("b.bin", binary), new("pasted", null, "pasted words"), new("gone.txt", Path.Combine(folder, "gone.txt"))] });
            string sent = JsonSerializer.Serialize(script.Requests[0].OfType<UserMessage>().Single(), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            StringAssert.Contains(sent, "inside the file");
            StringAssert.Contains(sent, "pasted words");
            StringAssert.Contains(sent, "b.bin, but its content could not be included");
            StringAssert.Contains(sent, "gone.txt, but its content could not be included");
        }
        finally { Directory.Delete(folder, true); }
    }

    private static Func<string, string?> Env(params (string Key, string Value)[] values) => key => values.FirstOrDefault(item => item.Key == key).Value;

    [TestMethod]
    public void WithoutAKeyTheScriptRuns()
    {
        Assert.IsTrue(PiConfiguration.Create(Env()).Profile.IsSimulated);
        Assert.IsTrue(PiConfiguration.Create(Env(("ANTHROPIC_API_KEY", "k"), ("ARCWPF_AI_BACKEND", "simulated"))).Profile.IsSimulated);
    }

    [TestMethod]
    public void AKeySelectsItsProviderAndNeverAppearsInTheProfile()
    {
        var anthropic = PiConfiguration.Create(Env(("ANTHROPIC_API_KEY", "secret-key")));
        Assert.IsFalse(anthropic.Profile.IsSimulated);
        Assert.AreEqual("claude-opus-5-5", anthropic.Profile.Models[0].Id);
        Assert.IsFalse(anthropic.Profile.ToString().Contains("secret-key", StringComparison.Ordinal));
        var compatible = PiConfiguration.Create(Env(("OPENAI_API_KEY", "k"), ("OPENAI_BASE_URL", "http://localhost:1/v1"), ("ARCWPF_AI_MODELS", "m1, m2")));
        CollectionAssert.AreEqual(new[] { "m1", "m2" }, compatible.Profile.Models.Select(model => model.Id).ToArray());
    }

    [TestMethod]
    public void AnOpenAiKeyWithoutAModelSaysWhatIsMissing()
    {
        var profile = PiConfiguration.Create(Env(("OPENAI_API_KEY", "k"))).Profile;
        Assert.IsTrue(profile.IsSimulated);
        StringAssert.Contains(profile.Welcome, "ARCWPF_AI_MODELS");
    }

    // A real HTTP round trip through Pi's OpenAI provider, against a server on this machine: no model and no network beyond loopback.
    [TestMethod]
    public void ACompatibleEndpointGetsTheKeyToolsAndToolResultAndStreamsBack()
    {
        // A machine's proxy settings would otherwise catch the loopback address.
        var proxy = System.Net.Http.HttpClient.DefaultProxy;
        System.Net.Http.HttpClient.DefaultProxy = new WebProxy();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var bodies = new List<string>();
        var headers = new List<string>();
        var server = Task.Run(async () =>
        {
            string[] replies =
            [
                """data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"role":"assistant","tool_calls":[{"index":0,"id":"call_1","type":"function","function":{"name":"get_current_time","arguments":"{}"}}]}}]}""" + "\n\n" +
                """data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}""" + "\n\ndata: [DONE]\n\n",
                """data: {"id":"2","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"role":"assistant","content":"Hello "}}]}""" + "\n\n" +
                """data: {"id":"2","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"there"}}]}""" + "\n\n" +
                """data: {"id":"2","object":"chat.completion.chunk","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}""" + "\n\ndata: [DONE]\n\n",
            ];
            foreach (string reply in replies)
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                var buffer = new byte[1 << 16];
                var seen = new StringBuilder();
                int bodyAt, length = 0;
                while (true)
                {
                    seen.Append(Encoding.UTF8.GetString(buffer, 0, await stream.ReadAsync(buffer)));
                    string so = seen.ToString();
                    bodyAt = so.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (bodyAt < 0) { continue; }
                    var match = System.Text.RegularExpressions.Regex.Match(so, "content-length: *(\\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    length = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                    if (Encoding.UTF8.GetByteCount(so[(bodyAt + 4)..]) >= length) { break; }
                }
                string all = seen.ToString();
                headers.Add(all[..bodyAt]);
                bodies.Add(all[(bodyAt + 4)..]);
                byte[] answer = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: close\r\n\r\n" + reply);
                await stream.WriteAsync(answer);
            }
        });

        try
        {
            var backend = (PiAgentBackend)PiConfiguration.Create(Env(("OPENAI_API_KEY", "test-key"), ("OPENAI_BASE_URL", $"http://127.0.0.1:{port}/v1"), ("ARCWPF_AI_MODELS", "local-model")));
            var events = Collect(backend.CreateSession(), new AssistantRequest("what time is it", [], "local-model", "local-model", null, []));
            Assert.IsTrue(server.Wait(TimeSpan.FromSeconds(5)), "Both requests reached the server");
            Assert.AreEqual(2, bodies.Count);
            StringAssert.Contains(headers[0].ToLowerInvariant(), "authorization: bearer test-key");
            StringAssert.Contains(headers[0], "/v1/chat/completions");
            using var first = JsonDocument.Parse(bodies[0]);
            Assert.AreEqual("local-model", first.RootElement.GetProperty("model").GetString());
            Assert.IsTrue(first.RootElement.GetProperty("stream").GetBoolean());
            CollectionAssert.Contains(first.RootElement.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("function").GetProperty("name").GetString()).ToArray(), "save_note");
            CollectionAssert.AreEqual(new[] { ToolCallStatus.Running, ToolCallStatus.Success }, Statuses(events).ToArray());
            using var second = JsonDocument.Parse(bodies[1]);
            var returned = second.RootElement.GetProperty("messages").EnumerateArray().Last();
            Assert.AreEqual("tool", returned.GetProperty("role").GetString());
            Assert.AreEqual("call_1", returned.GetProperty("tool_call_id").GetString());
            Assert.AreEqual("Hello there", Words(events));
        }
        finally
        {
            listener.Stop();
            System.Net.Http.HttpClient.DefaultProxy = proxy;
        }
    }

    [TestMethod]
    public void APanelShowsWordsBeforeAndAfterAToolAsSeparateParts() => Sta.Run(() =>
    {
        var script = new Script();
        var backend = script.Backend(new Faux.Response([new TextContent("Let me check."), new ToolCallContent("c1", "read_it", new { })], StopReason.ToolUse), Faux.Response.Text("All done."));
        var panel = new AssistantPanel { Backend = backend };
        ArcMotion.SetLevel(panel, ArcMotionLevel.Off);
        var window = new Window { Content = panel, Width = 1240, Height = 800, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            Sta.Pump();
            var composer = (PromptInput)panel.FindName("Composer");
            composer.TextBoxForTest.Text = "go";
            composer.KeyForTest(System.Windows.Input.Key.Enter);
            var thread = (StreamingScrollHost)panel.FindName("Scroller");
            var limit = DateTime.UtcNow.AddSeconds(10);
            while (composer.IsGenerating && DateTime.UtcNow < limit) { Pump(); }
            Pump();
            var body = Nested<StreamingMessage>(thread).ToList();
            CollectionAssert.AreEqual(new[] { "Let me check.", "All done." }, body.Select(message => message.Text).ToArray());
            Assert.IsTrue(body.All(message => message.Status == StreamingStatus.Done));
            Assert.AreEqual("Let me check.\n\nAll done.", Nested<MessageActions>(thread).Single().Text);
            Assert.AreEqual(1, Nested<ToolCallGroup>(thread).Count());
        }
        finally { window.Close(); }
    });

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<T> Nested<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) { yield return match; }
            foreach (var nested in Nested<T>(child)) { yield return nested; }
        }
    }
}
