using System.Windows;
using ArcWpf.AiAssistant.Backend;

namespace ArcWpf.AiAssistant;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Panel.Backend = PiConfiguration.Create(Environment.GetEnvironmentVariable);
    }
}
