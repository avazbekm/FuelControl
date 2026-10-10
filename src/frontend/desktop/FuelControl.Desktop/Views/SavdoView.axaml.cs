using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FuelControl.Desktop.ViewModels;

namespace FuelControl.Desktop.Views;

public partial class SavdoView : UserControl
{
    private SavdoViewModel? _vm;

    public SavdoView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PlastikQoshildi -= PlastikQoshildi;
            _vm = DataContext as SavdoViewModel;
            if (_vm is not null) _vm.PlastikQoshildi += PlastikQoshildi;
        };
    }

    /// <summary>"+ Plastik qo'shish" — yangi (oxirgi) qatorga fokus.</summary>
    private void PlastikQoshildi(PlastikQatori q) => Dispatcher.UIThread.Post(() =>
    {
        var t = PlastikRoyxat.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(x => ReferenceEquals(x.DataContext, q));
        t?.Focus(NavigationMethod.Tab);
    }, DispatcherPriority.Background);
}
