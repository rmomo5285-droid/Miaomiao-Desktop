using DialogHostAvalonia;

namespace v2rayN.Desktop.Views;

public partial class MiaomiaoNoticeDialog : UserControl
{
    public MiaomiaoNoticeDialog(MiaomiaoNotice notice)
    {
        InitializeComponent();
        txtTitle.Text = notice.Title.IsNotEmpty() ? notice.Title : "公告";
        txtDate.Text = notice.CreatedAt is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(notice.CreatedAt.Value).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : string.Empty;
        richContent.Source = notice.Content;
        btnClose.Click += (_, _) => DialogHost.Close(null);
    }
}
