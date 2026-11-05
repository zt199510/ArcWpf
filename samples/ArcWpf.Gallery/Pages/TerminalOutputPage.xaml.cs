using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ArcWpf.Controls;

namespace ArcWpf.Gallery.Pages;

public partial class TerminalOutputPage : UserControl
{
    private const string E = "\u001b[";
    private readonly DispatcherTimer _timer = new();
    private Func<int, bool>? _step;
    private int _tick;

    public TerminalOutputPage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => { if (_step is null || !_step(_tick++)) { _timer.Stop(); } };
        Unloaded += (_, _) => _timer.Stop();
        Output.Command = "npm install";
        Output.WriteLine("Press a button above to run a simulated command.");
        Colors.Write($"{E}31mred{E}0m {E}32mgreen{E}0m {E}33myellow{E}0m {E}34mblue{E}0m {E}35mmagenta{E}0m {E}36mcyan{E}0m\n");
        Colors.Write($"{E}91mred{E}0m {E}92mgreen{E}0m {E}93myellow{E}0m {E}94mblue{E}0m {E}95mmagenta{E}0m {E}96mcyan{E}0m (bright)\n");
        Colors.Write($"{E}1mbold{E}0m {E}2mdim{E}0m {E}3mitalic{E}0m {E}4munderline{E}0m {E}9mstrike{E}0m {E}7minverse{E}0m {E}30;43m on yellow {E}0m\n");
        Colors.Write($"{E}38;5;208m256 color{E}0m {E}38;2;120;90;220mtrue color{E}0m\n");
        Colors.Write("hidden: a‮b\n");
        Colors.Write("tab\tstops\tline up\n");
        Colors.Write("a very long line that scrolls sideways under the frame instead of wrapping, " + string.Concat(Enumerable.Repeat("because output is often wider than the view ", 6)) + "\n");
    }

    private void Run(string command, int intervalMs, Func<int, bool> step)
    {
        _timer.Stop();
        Output.Clear();
        Output.Command = command;
        Output.ExitCode = null;
        Output.Status = TerminalStatus.Idle;
        Output.Status = TerminalStatus.Running;
        _step = step;
        _tick = 0;
        _timer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        _timer.Start();
    }

    private void InstallClicked(object sender, RoutedEventArgs e) => Run("npm install", 90, tick =>
    {
        if (tick < 40)
        {
            int percent = (tick + 1) * 100 / 40;
            string bar = new string('#', percent / 5).PadRight(20, '.');
            Output.Write($"\r{E}36mfetching{E}0m [{bar}] {percent}%{E}K");
            return true;
        }
        Output.Write($"\r{E}K{E}32madded 214 packages{E}0m in 3s\n");
        Output.WriteLine($"{E}33mwarning{E}0m 2 packages are looking for funding");
        Output.Status = TerminalStatus.Succeeded;
        return false;
    });

    private void FailClicked(object sender, RoutedEventArgs e) => Run("dotnet test", 180, tick =>
    {
        if (tick < 6) { Output.WriteLine($"{E}32mPassed{E}0m Webhooks.DeliversOnce.{tick} [12 ms]"); return true; }
        Output.WriteLine($"{E}31mFailed{E}0m Webhooks.RetriesOn429 [40 ms]");
        Output.WriteLine("  Expected: 5 attempts");
        Output.WriteLine("  Actual:   1 attempt");
        Output.ExitCode = 1;
        Output.Status = TerminalStatus.Failed;
        return false;
    });

    private void FloodClicked(object sender, RoutedEventArgs e) => Run("yes | head -n 200000", 30, tick =>
    {
        for (int i = 0; i < 1000; i++) { Output.WriteLine($"{E}2m{tick * 1000 + i,7}{E}0m y"); }
        if (tick < 200) { return true; }
        Output.Status = TerminalStatus.Succeeded;
        return false;
    });

    private void ScreenClicked(object sender, RoutedEventArgs e) => Run("vim notes.txt", 100, _ =>
    {
        Output.Write($"{E}?1049h{E}2J{E}H~\n~\n\"notes.txt\" 0L, 0B\n");
        Output.ExitCode = 0;
        Output.Status = TerminalStatus.Succeeded;
        return false;
    });

    private void StopRequested(object? sender, EventArgs e)
    {
        _timer.Stop();
        Output.WriteLine("^C");
        Output.Status = TerminalStatus.Stopped;
    }
}
