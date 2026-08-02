namespace ServiceLib.ViewModels;

public partial class SubSettingViewModel : MyReactiveObject
{
    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();
    public Interaction<string, RxVoid> ShareSubInteraction { get; } = new();

    public BulkObservableCollection<SubItem> SubItems { get; } = [];

    [Reactive]
    public partial SubItem SelectedSource { get; set; }

    public IList<SubItem> SelectedSources { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> SubAddCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubDeleteCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubEditCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubShareCmd { get; }
    public bool IsModified { get; set; }

    public SubSettingViewModel()
    {
        _config = AppManager.Instance.Config;

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedSource,
           selectedSource => selectedSource != null
               && !selectedSource.Id.IsNullOrEmpty()
               && !MiaomiaoManagedSubscriptionPolicy.IsManaged(selectedSource));

        SubAddCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(true);
        });
        SubDeleteCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DeleteSubAsync();
        }, canEditRemove);
        SubEditCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(false);
        }, canEditRemove);
        SubShareCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ShareSubInteraction.Handle(SelectedSource?.Url);
        }, canEditRemove);

        _ = Init();
    }

    private async Task Init()
    {
        SelectedSource = new();

        await RefreshSubItems();
    }

    public async Task RefreshSubItems()
    {
        SubItems.Clear();
        SubItems.AddRange((await AppManager.Instance.SubItems())
            .Where(item => !MiaomiaoManagedSubscriptionPolicy.IsManaged(item)));
    }

    public async Task EditSubAsync(bool blNew)
    {
        SubItem item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = await AppManager.Instance.GetSubItem(SelectedSource?.Id);
            if (item is null || MiaomiaoManagedSubscriptionPolicy.IsManaged(item))
            {
                return;
            }
        }
        var subEditViewModel = new SubEditViewModel(item);
        if (await AppManager.Instance.WindowDialog.ShowDialogAsync(subEditViewModel) == true)
        {
            await RefreshSubItems();
            IsModified = true;
        }
    }

    private async Task DeleteSubAsync()
    {
        if (await ShowYesNoInteraction.Handle(ResUI.RemoveServer) == false)
        {
            return;
        }

        var deletable = (SelectedSources ?? [SelectedSource])
            .Where(item => !MiaomiaoManagedSubscriptionPolicy.IsManaged(item))
            .ToList();
        if (deletable.Count == 0)
        {
            return;
        }
        foreach (var it in deletable)
        {
            await ConfigHandler.DeleteSubItem(_config, it.Id);
        }
        await RefreshSubItems();
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        IsModified = true;
    }
}
