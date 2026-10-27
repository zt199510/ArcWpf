using System.ComponentModel;
using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcWpf.Controls;
using ArcWpf.Gallery.Pages;

namespace ArcWpf.Tests;
[TestClass]
public sealed class PasswordInputTests
{
    [TestMethod] public void ExplicitPasswordAndSecureCopiesSurviveTemplates() => Sta.Run(() => InWindow(c =>
    {
        c.Password = "demo-only"; Assert.IsTrue(c.HasPassword); Assert.AreEqual("demo-only", c.Password);
        using var snapshot = c.SecurePassword; Assert.AreEqual(9, snapshot.Length);
        c.Clear(); Assert.IsFalse(c.HasPassword); Assert.AreEqual(9, snapshot.Length, "Returned copy must not alias control storage.");
        c.SetSecurePassword(snapshot); Assert.AreEqual("demo-only", c.Password);
        var old = Part<PasswordBox>(c, "PART_PasswordBox"); var template = c.Template;
        c.Template = new ControlTemplate(typeof(PasswordInput)); c.ApplyTemplate(); Assert.AreEqual("", old.Password); Assert.AreEqual("demo-only", c.Password);
        c.Template = template; c.ApplyTemplate(); Assert.AreEqual("demo-only", Part<PasswordBox>(c, "PART_PasswordBox").Password);
        Assert.IsNull(DependencyPropertyDescriptor.FromName("Password", typeof(PasswordInput), typeof(PasswordInput)), "Password must not silently become a plaintext DP.");
    }));

    [TestMethod] public void NativeEditingRaisesOneChangeAndRevealDoesNot() => Sta.Run(() => InWindow(c =>
    {
        int changes = 0; c.PasswordChanged += (_, _) => changes++;
        Part<PasswordBox>(c, "PART_PasswordBox").Password = "native-demo"; Assert.AreEqual(1, changes); Assert.AreEqual("native-demo", c.Password);
        c.IsPasswordRevealed = true; Assert.AreEqual(1, changes); var shown = Part<PasswordRevealBox>(c, "PART_RevealBox");
        Assert.AreEqual("native-demo", shown.Text); shown.SelectAll(); shown.SelectedText = "edited-demo";
        Assert.AreEqual(2, changes); Assert.AreEqual("edited-demo", c.Password); Assert.AreEqual("edited-demo", Part<PasswordBox>(c, "PART_PasswordBox").Password);
        Assert.IsFalse(shown.IsUndoEnabled); Assert.IsFalse(shown.CanUndo);
        c.IsPasswordRevealed = false; Assert.AreEqual("", shown.Text); Assert.IsFalse(shown.CanUndo); Assert.AreEqual(2, changes);
    }));

    [TestMethod] public void RevealBindingToggleNamesAndKeyboardFocusRemainUsable() => Sta.Run(() => InWindow(c =>
    {
        var model = new RevealModel(); c.SetBinding(PasswordInput.IsPasswordRevealedProperty, new Binding("Revealed") { Source = model });
        c.Password = "demo"; c.ShowPasswordText = "显示密码"; c.HidePasswordText = "隐藏密码";
        var toggle = Part<PasswordRevealToggle>(c, "PART_RevealToggle"); Window.GetWindow(c).Activate(); toggle.Focus();
        var peer = UIElementAutomationPeer.CreatePeerForElement(toggle)!; Assert.AreEqual("显示密码", peer.GetName());
        Key(toggle, System.Windows.Input.Key.Enter); Assert.IsTrue(c.IsPasswordRevealed); Assert.IsTrue(model.Revealed); Assert.IsTrue(toggle.IsKeyboardFocused); Assert.AreEqual("隐藏密码", peer.GetName());
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle(); Assert.IsFalse(model.Revealed); Assert.IsNotNull(c.GetBindingExpression(PasswordInput.IsPasswordRevealedProperty));
        c.FocusEditor(); Assert.IsTrue(Part<PasswordBox>(c, "PART_PasswordBox").IsKeyboardFocused);
        c.IsPasswordRevealed = true; Assert.IsTrue(Part<PasswordRevealBox>(c, "PART_RevealBox").IsKeyboardFocused);
        c.IsPasswordRevealed = false; Assert.IsTrue(Part<PasswordBox>(c, "PART_PasswordBox").IsKeyboardFocused);
    }));

    [TestMethod] public void CompositionDefersRevealUntilNativeInputFinishes() => Sta.Run(() => InWindow(c =>
    {
        c.IsPasswordRevealed = true; var editor = Part<PasswordRevealBox>(c, "PART_RevealBox");
        var composition = new TextComposition(InputManager.Current, editor, "测");
        editor.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputStartEvent });
        Assert.IsFalse(c.IsPlaceholderVisible); Assert.IsFalse(Part<PasswordRevealToggle>(c, "PART_RevealToggle").IsEnabled);
        c.IsPasswordRevealed = false; Assert.IsTrue(c.IsPasswordRevealed); editor.Text = "测试-demo";
        editor.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent }); Pump(40);
        Assert.IsFalse(c.IsPasswordRevealed); Assert.AreEqual("测试-demo", c.Password); Assert.AreEqual("", editor.Text);
    }));

    [TestMethod] public void DisabledHiddenAndUnloadedEditorsDiscardRevealedText() => Sta.Run(() => InWindow(c =>
    {
        c.Password = "demo-secret"; c.IsPasswordRevealed = true; var editor = Part<PasswordRevealBox>(c, "PART_RevealBox"); Pump(30);
        c.IsEnabled = false; Assert.IsFalse(c.IsPasswordRevealed); Assert.AreEqual("", editor.Text); Assert.IsFalse(c.IsMotionRunning);
        c.IsPasswordRevealed = true; Assert.IsFalse(c.IsPasswordRevealed); c.Password = "external-demo"; Assert.AreEqual("external-demo", c.Password);
        c.IsEnabled = true; c.IsPasswordRevealed = true; c.Visibility = Visibility.Hidden; Assert.IsFalse(c.IsPasswordRevealed); Assert.AreEqual("", editor.Text); Assert.IsFalse(c.IsMotionRunning);
        c.Visibility = Visibility.Visible; c.IsPasswordRevealed = true; Window.GetWindow(c).Content = null; Sta.Pump(); Assert.AreEqual("", editor.Text); Assert.IsFalse(c.IsMotionRunning); Assert.AreEqual("external-demo", c.Password);
    }));

    [TestMethod] public void AutomationNeverReadsPlaintextInEitherMode() => Sta.Run(() => InWindow(c =>
    {
        c.Password = "only-synthetic-test"; var container = UIElementAutomationPeer.CreatePeerForElement(c)!;
        Assert.AreEqual(AutomationControlType.Group, container.GetAutomationControlType()); Assert.IsNull(container.GetPattern(PatternInterface.Value));
        foreach (bool reveal in new[] { false, true })
        {
            c.IsPasswordRevealed = reveal; FrameworkElement editor = reveal ? Part<PasswordRevealBox>(c, "PART_RevealBox") : Part<PasswordBox>(c, "PART_PasswordBox");
            var peer = UIElementAutomationPeer.CreatePeerForElement(editor)!; Assert.IsTrue(peer.IsPassword()); Assert.AreEqual("Password", peer.GetName());
            var value = (IValueProvider)peer.GetPattern(PatternInterface.Value); Assert.AreEqual("", value.Value);
            if (reveal) { Assert.IsNull(peer.GetPattern(PatternInterface.Text)); Assert.IsFalse(peer is TextBoxAutomationPeer); }
            value.SetValue("uia-demo"); Assert.AreEqual("uia-demo", c.Password);
            c.IsEnabled = false; Assert.Throws<ElementNotEnabledException>(() => value.SetValue("ignored")); c.IsEnabled = true;
        }
    }));

    [TestMethod] public void EyeMaskResolveAndReverseReachStableBase() => Sta.Run(() => InWindow(c =>
    {
        c.Password = "demo"; Pump(120); var toggle = Part<PasswordRevealToggle>(c, "PART_RevealToggle"); var visual = Part<FrameworkElement>(c, "PART_EditorVisual");
        Assert.AreEqual(0d, toggle.Eye.Progress); Assert.AreEqual(0d, ((BlurEffect)visual.Effect).Radius);
        c.IsPasswordRevealed = true;
        for (int i = 0; i < 8; i++) { Pump(20); if (toggle.Eye.Progress is > 0 and < 1) break; }
        Assert.IsGreaterThan(0d, toggle.Eye.Progress); Assert.IsLessThan(1d, toggle.Eye.Progress); Assert.IsGreaterThan(0d, ((BlurEffect)visual.Effect).Radius);
        c.IsPasswordRevealed = false; Pump(30); c.IsPasswordRevealed = true; Pump(600);
        Assert.AreEqual(1d, toggle.Eye.Progress, .001); Assert.AreEqual(1d, toggle.Eye.SlashOpacity, .001); Assert.AreEqual(1d, visual.Opacity, .001); Assert.AreEqual(0d, ((BlurEffect)visual.Effect).Radius, .001); Assert.IsFalse(c.IsMotionRunning);
        Assert.IsGreaterThan(19d, PasswordEye.Outline.Bounds.Width); Assert.IsLessThan(24d, PasswordEye.Outline.Bounds.Width);
        ArcMotion.SetLevel(c, ArcMotionLevel.Reduced); c.IsPasswordRevealed = false; Assert.AreEqual(0d, toggle.Eye.Progress); Assert.IsFalse(c.IsMotionRunning);
        ArcMotion.SetLevel(c, ArcMotionLevel.Off); c.IsPasswordRevealed = true; Assert.AreEqual(1d, toggle.Eye.Progress); Assert.IsFalse(c.IsMotionRunning);
    }));

    [TestMethod] public void MessageWordsAndHeightAnimateWithoutMovingShell() => Sta.Run(() => InWindow(c =>
    {
        c.Width = 180; c.UpdateLayout(); var shell = Part<Border>(c, "PART_Shell"); var before = shell.TranslatePoint(new Point(), Window.GetWindow(c));
        c.Description = "Keep this word"; Pump(600); var row = (PasswordMessage)Part<Grid>(c, "PART_DescriptionHost").Children[0]; double shortHeight = row.ActualHeight;
        var live = (WrapPanel)VisualTreeHelper.GetChild(row, 0); var firstWord = live.Children[0];
        c.Description = "Keep this word while more words wrap onto another line"; Pump(55); Assert.AreSame(firstWord, live.Children[0]); Assert.IsGreaterThan(shortHeight, row.ActualHeight); Assert.IsTrue(row.IsMotionRunning);
        Pump(550); Assert.IsFalse(row.IsMotionRunning); Assert.AreEqual(before, shell.TranslatePoint(new Point(), Window.GetWindow(c)));
        c.Description = ""; Pump(40); Assert.IsGreaterThan(0d, row.ActualHeight); c.Description = "Keep this word"; Pump(600); Assert.AreEqual(shortHeight, row.ActualHeight, .1); Assert.IsFalse(row.IsMotionRunning);
        c.Description = ""; Pump(600); Assert.AreEqual(0d, row.ActualHeight, .01);
    }));

    [TestMethod] public void ValidationThemeAndNativeLimitsStayConnected() => Sta.Run(() => InWindow(c =>
    {
        c.MaxLength = 12; c.PasswordChar = '*'; Assert.AreEqual(12, Part<PasswordBox>(c, "PART_PasswordBox").MaxLength); Assert.AreEqual(12, Part<PasswordRevealBox>(c, "PART_RevealBox").MaxLength); Assert.AreEqual('*', Part<PasswordBox>(c, "PART_PasswordBox").PasswordChar);
        Assert.Throws<ArgumentException>(() => c.MaxLength = -1);
        c.SetBinding(PasswordInput.LabelProperty, new Binding("Label") { Source = new { Label = "Password" } }); var binding = c.GetBindingExpression(PasswordInput.LabelProperty)!;
        Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, "Required", null)); Assert.IsTrue(c.HasError); Assert.AreEqual("Required", c.EffectiveErrorMessage);
        c.ErrorMessage = "Try again"; Assert.AreEqual("Try again", c.EffectiveErrorMessage); Validation.ClearInvalid(binding); c.ErrorMessage = ""; Assert.IsFalse(c.HasError);
        Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(600);
        Assert.AreEqual(((SolidColorBrush)c.BorderBrush).Color, ((SolidColorBrush)Part<Border>(c, "PART_Shell").BorderBrush).Color);
    }));

    [TestMethod] public void LayoutPlaceholderAndToggleRemainStableAtNarrowWidths() => Sta.Run(() => InWindow(c =>
    {
        ArcMotion.SetLevel(c, ArcMotionLevel.Off);
        foreach (double width in new[] { 400d, 180d })
        {
            c.Width = width; c.Clear(); c.UpdateLayout(); var shell = Part<Border>(c, "PART_Shell"); var toggle = Part<PasswordRevealToggle>(c, "PART_RevealToggle");
            Assert.AreEqual(44d, shell.ActualHeight, .01); Assert.AreEqual(30d, toggle.ActualWidth, .01); Assert.AreEqual(18d, toggle.Eye.ActualWidth, .01);
            var origin = shell.TranslatePoint(new Point(), Window.GetWindow(c)); var button = toggle.TranslatePoint(new Point(), Window.GetWindow(c));
            var watermark = Part<TextBlock>(c, "Watermark").TranslatePoint(new Point(), c);
            c.IsPasswordRevealed = true; var editor = Part<PasswordRevealBox>(c, "PART_RevealBox"); editor.Text = "A"; c.UpdateLayout();
            var rect = editor.GetRectFromCharacterIndex(0); var character = editor.TranslatePoint(rect.TopLeft, c); Assert.AreEqual(watermark.X, character.X, .5);
            c.Password = "synthetic-long-password-for-horizontal-scrolling";
            foreach (bool shown in new[] { true, false, true, false }) { c.IsPasswordRevealed = shown; c.UpdateLayout(); Assert.AreEqual(origin, shell.TranslatePoint(new Point(), Window.GetWindow(c))); Assert.AreEqual(button, toggle.TranslatePoint(new Point(), Window.GetWindow(c))); Assert.AreEqual(width, shell.ActualWidth, .01); }
            var hit = c.InputHitTest(toggle.TranslatePoint(new Point(15, 15), c)); Assert.IsTrue(Popover.IsWithin(hit as DependencyObject, toggle));
        }
    }));

    [TestMethod] public void GalleryAndEyeStatesRenderWithoutRealCredentials() => Sta.Run(() =>
    {
        var page = new PasswordInputPage(); page.SetResourceReference(Control.BackgroundProperty, "ArcWpf.Brush.Background"); var window = new Window { Content = page, Width = 720, Height = 960, ShowActivated = false };
        try
        {
            window.Show(); Pump(650); Save(window, "gallery-light.png");
            var control = (PasswordInput)page.FindName("Editable"); control.Password = "Arc-demo-2026!"; control.IsPasswordRevealed = true; Pump(500); Save(window, "gallery-revealed.png");
            Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().Single().Mode = ArcThemeMode.Dark; Pump(500); Save(window, "gallery-dark.png");
        }
        finally { window.Close(); Sta.Pump(); }
    });

    private static T Part<T>(PasswordInput c, string name) where T : class => (T)c.Template.FindName(name, c);
    private static void Key(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target)!, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
    private static void InWindow(Action<PasswordInput> action)
    {
        var control = new PasswordInput { Label = "Password", Placeholder = "Enter password", Width = 400, Margin = new Thickness(30), VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Content = control, Width = 600, Height = 500, ShowActivated = false };
        try { window.Show(); Sta.Pump(); action(control); }
        finally { window.Close(); Sta.Pump(); Assert.IsFalse(control.IsMotionRunning); }
    }
    private static void Pump(int ms) { var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) }; timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame); }
    private static void Save(FrameworkElement element, string filename)
    {
        element.UpdateLayout(); var bounds = new Rect(0, 0, element.ActualWidth, element.ActualHeight); var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen()) { drawing.DrawRectangle((Brush)element.FindResource("ArcWpf.Brush.Background"), null, bounds); drawing.DrawRectangle(new VisualBrush(element) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds, Stretch = Stretch.Fill }, null, bounds); }
        var image = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32); image.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory); while (root is not null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "ArcWpf.slnx"))) root = root.Parent;
        var folder = System.IO.Path.Combine(root!.FullName, "artifacts", "password-input"); System.IO.Directory.CreateDirectory(folder); using var file = System.IO.File.Create(System.IO.Path.Combine(folder, filename)); encoder.Save(file);
    }
    private sealed class RevealModel : INotifyPropertyChanged
    {
        private bool _revealed;
        public bool Revealed { get => _revealed; set { _revealed = value; PropertyChanged?.Invoke(this, new(nameof(Revealed))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
