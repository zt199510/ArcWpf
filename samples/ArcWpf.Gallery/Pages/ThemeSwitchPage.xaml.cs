using System.Windows;
using System.Windows.Controls;
using ArcWpf;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class ThemeSwitchPage : UserControl
{
    public ThemeSwitchPage() { InitializeComponent(); }
    private void ThemeChanged(object sender, ThemeChangedEventArgs e) { var theme = Application.Current.Resources.MergedDictionaries.OfType<ArcThemeResources>().SingleOrDefault(); if (theme is not null) theme.Mode = e.Theme == ThemeSwitchTheme.Dark ? ArcThemeMode.Dark : ArcThemeMode.Light; Status.Text = $"当前主题：{e.Theme}"; }
}
