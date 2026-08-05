namespace v2rayN.Desktop.Views;

public partial class MiaomiaoAccountView : ReactiveUserControl<MiaomiaoAccountViewModel>
{
    public MiaomiaoAccountView()
    {
        InitializeComponent();
        ViewModel = new MiaomiaoAccountViewModel();
    }

    public void SelectSection(int index)
    {
        tabAccountSections.SelectedIndex = Math.Clamp(index, 0, 1);
    }
}
