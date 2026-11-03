using System.Windows; using System.Windows.Controls; using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages;
public partial class CookieConsentPage : UserControl { public CookieConsentPage(){InitializeComponent();} private void Open(object s,RoutedEventArgs e)=>Consent.OpenPreferences(); private void Changed(object? s,CookieConsentChangedEventArgs e){} }
