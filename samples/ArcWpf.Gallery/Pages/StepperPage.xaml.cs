using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages; public partial class StepperPage : UserControl { public StepperPage() { InitializeComponent(); Stepper.Steps.Add(new StepperStep { Title = "Account", Description = "Details" }); Stepper.Steps.Add(new StepperStep { Title = "Plan", Description = "Billing" }); Stepper.Steps.Add(new StepperStep { Title = "Review", Description = "Confirm" }); } private void Previous(object sender, RoutedEventArgs e) => Stepper.Previous(); private void Next(object sender, RoutedEventArgs e) => Stepper.Next(); }
