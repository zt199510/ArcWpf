using System.Windows;
using System.Windows.Controls;
using ArcWpf.Controls;
namespace ArcWpf.Gallery.Pages; public partial class StepperPage : UserControl { public StepperPage() { InitializeComponent(); Stepper.Steps.Add(new StepperStep { Title = "Cart" }); Stepper.Steps.Add(new StepperStep { Title = "Shipping", Description = "Address and delivery" }); Stepper.Steps.Add(new StepperStep { Title = "Payment" }); } private void Previous(object sender, RoutedEventArgs e) => Stepper.Previous(); private void Next(object sender, RoutedEventArgs e) => Stepper.Next(); }
