using System.Windows.Input;

namespace ArcWpf.Controls;

/// <summary>Which key caps a shortcut reads as: ⌃ ⌥ ⇧ ⌘ on a Mac, Ctrl Alt Shift Win elsewhere.</summary>
public enum ShortcutPlatform { Mac, Other }

/// <summary>One key cap: what it shows, the physical key id it lights for, and a spoken name.</summary>
public sealed record ShortcutToken(string Label, string Id, string Spoken);

/// <summary>A shortcut already in use, such as "mod+k" for "Open command menu".</summary>
public sealed record ShortcutBinding(string Shortcut, string Label);

/// <summary>
/// The registry's shortcut model (shortcut-recorder.tsx, frozen 2026-10-31). A shortcut is a string such as "mod+shift+k"; `mod` is ⌘ on a Mac
/// and Ctrl elsewhere, so one binding reads right everywhere. WPF reports physical keys, so ⌥K records as "alt+k".
/// </summary>
public static class Shortcuts
{
    private static readonly string[] ModifierOrder = ["mod", "ctrl", "alt", "shift", "meta"];
    private static readonly string[] HeldModifiers = ["meta", "ctrl", "alt", "shift"];

    /// <summary>Combinations the browser or system keeps, warned about by default.</summary>
    public static IReadOnlyList<ShortcutBinding> Reserved { get; } =
    [
        new("mod+w", "Close tab"), new("mod+t", "New tab"), new("mod+n", "New window"), new("mod+q", "Quit"),
        new("mod+l", "Address bar"), new("mod+r", "Reload"), new("mod+shift+t", "Reopen tab"),
        new("mod+c", "Copy"), new("mod+v", "Paste"), new("mod+x", "Cut"), new("mod+z", "Undo"), new("mod+a", "Select all"),
    ];

    private static readonly Dictionary<string, (string Label, string Spoken)> MacKeys = new()
    {
        ["enter"] = ("↩", "Return"), ["backspace"] = ("⌫", "Delete"), ["delete"] = ("⌦", "Forward delete"), ["tab"] = ("⇥", "Tab"), ["escape"] = ("esc", "Escape"),
    };

    private static readonly Dictionary<string, (string Label, string Spoken)> SharedKeys = new()
    {
        ["space"] = ("Space", "Space"), ["enter"] = ("Enter", "Enter"), ["escape"] = ("Esc", "Escape"), ["backspace"] = ("Backspace", "Backspace"), ["delete"] = ("Del", "Delete"), ["tab"] = ("Tab", "Tab"),
        ["up"] = ("↑", "Up arrow"), ["down"] = ("↓", "Down arrow"), ["left"] = ("←", "Left arrow"), ["right"] = ("→", "Right arrow"), ["home"] = ("Home", "Home"), ["end"] = ("End", "End"), ["pageup"] = ("PgUp", "Page up"), ["pagedown"] = ("PgDn", "Page down"),
        [","] = (",", "Comma"), ["."] = (".", "Period"), ["/"] = ("/", "Slash"), [";"] = (";", "Semicolon"), ["'"] = ("'", "Quote"), ["["] = ("[", "Left bracket"), ["]"] = ("]", "Right bracket"), ["\\"] = ("\\", "Backslash"), ["-"] = ("-", "Minus"), ["="] = ("=", "Equals"), ["`"] = ("`", "Backtick"),
    };

    public static bool IsModifier(string part) => ModifierOrder.Contains(part);

    /// <summary>The physical key id: "k", "1", "enter", "meta", "shift"… null for keys that never make a shortcut, such as Caps Lock.</summary>
    public static string? KeyId(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ((char)('a' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
        >= Key.F1 and <= Key.F24 => $"f{key - Key.F1 + 1}",
        Key.LWin or Key.RWin => "meta",
        Key.LeftCtrl or Key.RightCtrl => "ctrl",
        Key.LeftAlt or Key.RightAlt => "alt",
        Key.LeftShift or Key.RightShift => "shift",
        Key.OemComma => ",", Key.OemPeriod => ".", Key.OemQuestion => "/", Key.OemSemicolon => ";", Key.OemQuotes => "'",
        Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]", Key.OemPipe or Key.OemBackslash => "\\", Key.OemMinus => "-", Key.OemPlus => "=", Key.OemTilde => "`",
        Key.Space => "space", Key.Enter => "enter", Key.Escape => "escape", Key.Back => "backspace", Key.Delete => "delete", Key.Tab => "tab",
        Key.Up => "up", Key.Down => "down", Key.Left => "left", Key.Right => "right", Key.Home => "home", Key.End => "end", Key.PageUp => "pageup", Key.PageDown => "pagedown",
        _ => null,
    };

    /// <summary>The key behind a WPF key event: Alt chords arrive as <see cref="Key.System"/>, IME-handled keys as <see cref="Key.ImeProcessed"/>.</summary>
    public static Key RealKey(KeyEventArgs e) => e.Key switch { Key.System => e.SystemKey, Key.ImeProcessed => e.ImeProcessedKey, Key.DeadCharProcessed => e.DeadCharProcessedKey, _ => e.Key };

    private static (HashSet<string> Mods, string Key) Split(string shortcut)
    {
        var parts = shortcut.ToLowerInvariant().Split('+').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
        return (parts.Where(IsModifier).ToHashSet(), parts.LastOrDefault(part => !IsModifier(part)) ?? "");
    }

    /// <summary>One spelling per combination: on a Mac, Command is `mod`; elsewhere, Ctrl is. Modifiers come in a fixed order.</summary>
    public static string Normalize(string? shortcut, ShortcutPlatform platform)
    {
        var (mods, key) = Split(shortcut ?? "");
        if (platform == ShortcutPlatform.Mac && mods.Remove("meta")) mods.Add("mod");
        if (platform == ShortcutPlatform.Other && mods.Remove("ctrl")) mods.Add("mod");
        return string.Join("+", ModifierOrder.Where(mods.Contains).Append(key).Where(part => part.Length > 0));
    }

    /// <summary>The modifiers held for a key event, in the order the live caps show them.</summary>
    public static IReadOnlyList<string> LiveModifiers(ModifierKeys modifiers, ShortcutPlatform platform)
    {
        var mods = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control) && platform == ShortcutPlatform.Mac) mods.Add("ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods.Add("alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods.Add("shift");
        if (modifiers.HasFlag(platform == ShortcutPlatform.Mac ? ModifierKeys.Windows : ModifierKeys.Control)) mods.Add("mod");
        if (platform == ShortcutPlatform.Other && modifiers.HasFlag(ModifierKeys.Windows)) mods.Add("meta");
        return mods;
    }

    /// <summary>Reads a key press into a shortcut string, or null while only modifiers are down.</summary>
    public static string? FromKey(Key key, ModifierKeys modifiers, ShortcutPlatform platform)
    {
        var id = KeyId(key);
        if (id is null || HeldModifiers.Contains(id)) return null;
        var mods = new List<string>();
        if (modifiers.HasFlag(platform == ShortcutPlatform.Mac ? ModifierKeys.Windows : ModifierKeys.Control)) mods.Add("mod");
        if (platform == ShortcutPlatform.Mac && modifiers.HasFlag(ModifierKeys.Control)) mods.Add("ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods.Add("alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods.Add("shift");
        if (platform == ShortcutPlatform.Other && modifiers.HasFlag(ModifierKeys.Windows)) mods.Add("meta");
        mods.Add(id);
        return Normalize(string.Join("+", mods), platform);
    }

    /// <summary>True when a key press is this shortcut. Use it to run the action a recorded shortcut is bound to.</summary>
    public static bool Matches(Key key, ModifierKeys modifiers, string? shortcut, ShortcutPlatform platform)
    {
        var pressed = FromKey(key, modifiers, platform);
        return pressed is not null && !string.IsNullOrEmpty(shortcut) && pressed == Normalize(shortcut, platform);
    }

    /// <summary>Key caps for a shortcut in platform order.</summary>
    public static IReadOnlyList<ShortcutToken> Tokens(string? shortcut, ShortcutPlatform platform)
    {
        var (mods, key) = Split(Normalize(shortcut, platform));
        var tokens = new List<ShortcutToken>();
        if (platform == ShortcutPlatform.Mac)
        {
            if (mods.Contains("ctrl")) tokens.Add(new("⌃", "ctrl", "Control"));
            if (mods.Contains("alt")) tokens.Add(new("⌥", "alt", "Option"));
            if (mods.Contains("shift")) tokens.Add(new("⇧", "shift", "Shift"));
            if (mods.Contains("mod") || mods.Contains("meta")) tokens.Add(new("⌘", "meta", "Command"));
        }
        else
        {
            if (mods.Contains("mod") || mods.Contains("ctrl")) tokens.Add(new("Ctrl", "ctrl", "Control"));
            if (mods.Contains("alt")) tokens.Add(new("Alt", "alt", "Alt"));
            if (mods.Contains("shift")) tokens.Add(new("Shift", "shift", "Shift"));
            if (mods.Contains("meta")) tokens.Add(new("Win", "meta", "Windows"));
        }
        if (key.Length > 0)
        {
            if ((platform == ShortcutPlatform.Mac && MacKeys.TryGetValue(key, out var named)) || SharedKeys.TryGetValue(key, out named)) tokens.Add(new(named.Label, key, named.Spoken));
            else tokens.Add(new(key.ToUpperInvariant(), key, key.ToUpperInvariant()));
        }
        return tokens;
    }

    /// <summary>"⌘⇧K" on a Mac, "Ctrl+Shift+K" elsewhere, plus a spoken form for assistive technology.</summary>
    public static (string Text, string Spoken) Format(string? shortcut, ShortcutPlatform platform)
    {
        var tokens = Tokens(shortcut, platform);
        return (string.Join(platform == ShortcutPlatform.Mac ? "" : "+", tokens.Select(token => token.Label)), string.Join(" ", tokens.Select(token => token.Spoken)));
    }

    /// <summary>Keys that are not modifiers: the recorder asks for ⌘, Ctrl, or ⌥ unless the key is a function key.</summary>
    public static bool HasStrongModifier(string shortcut)
    {
        var (mods, key) = Split(shortcut);
        return mods.Contains("mod") || mods.Contains("ctrl") || mods.Contains("alt") || mods.Contains("meta") || System.Text.RegularExpressions.Regex.IsMatch(key, "^f\\d+$");
    }

    /// <summary>The held-key ids that a key event adds or removes, as `usePressedKeys` tracks them.</summary>
    internal static void Track(HashSet<string> held, KeyEventArgs e, bool down)
    {
        var id = KeyId(RealKey(e));
        var modifiers = Keyboard.Modifiers;
        if (down) { if (id is not null) held.Add(id); return; }
        if (id is not null) held.Remove(id);
        if (id == "meta") held.RemoveWhere(key => !HeldModifiers[1..].Contains(key));
        if (!modifiers.HasFlag(ModifierKeys.Windows)) held.Remove("meta");
        if (!modifiers.HasFlag(ModifierKeys.Control)) held.Remove("ctrl");
        if (!modifiers.HasFlag(ModifierKeys.Alt)) held.Remove("alt");
        if (!modifiers.HasFlag(ModifierKeys.Shift)) held.Remove("shift");
    }

    internal static bool IsHeldModifier(string id) => HeldModifiers.Contains(id);
}
