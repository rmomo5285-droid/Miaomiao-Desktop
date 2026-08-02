namespace v2rayN.Desktop.Views;

public partial class MiaomiaoAccountView : ReactiveUserControl<MiaomiaoAccountViewModel>
{
    public MiaomiaoAccountView()
    {
        InitializeComponent();
        ViewModel = new MiaomiaoAccountViewModel();
    }

    private void BillingPeriod_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null && cmbBillingPeriod.SelectedItem is ComboBoxItem { Tag: string period })
        {
            ViewModel.SelectedPeriod = period;
        }
    }

    private void Plan_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        cmbBillingPeriod.Items.Clear();
        if (ViewModel?.SelectedPlan is not { } plan)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedPeriod = string.Empty;
            }
            return;
        }

        AddBillingPeriod("月付", "month_price", plan.MonthPrice);
        AddBillingPeriod("季付", "quarter_price", plan.QuarterPrice);
        AddBillingPeriod("半年付", "half_year_price", plan.HalfYearPrice);
        AddBillingPeriod("年付", "year_price", plan.YearPrice);
        AddBillingPeriod("两年付", "two_year_price", plan.TwoYearPrice);
        AddBillingPeriod("三年付", "three_year_price", plan.ThreeYearPrice);
        AddBillingPeriod("一次性", "onetime_price", plan.OneTimePrice);

        if (cmbBillingPeriod.Items.Count > 0)
        {
            cmbBillingPeriod.SelectedIndex = 0;
        }
        else
        {
            ViewModel.SelectedPeriod = string.Empty;
        }
    }

    private void AddBillingPeriod(string label, string key, decimal? price)
    {
        if (price is null)
        {
            return;
        }

        cmbBillingPeriod.Items.Add(new ComboBoxItem
        {
            Content = $"{label}  ¥{price.Value / 100m:0.##}",
            Tag = key
        });
    }
}
