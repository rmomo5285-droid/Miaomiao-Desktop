using System.Net;
using System.Text.RegularExpressions;

namespace v2rayN.Desktop.Views;

public partial class MiaomiaoRichTextView : UserControl
{
    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MiaomiaoRichTextView, string?>(nameof(Source));

    public static readonly StyledProperty<bool> CompactProperty =
        AvaloniaProperty.Register<MiaomiaoRichTextView, bool>(nameof(Compact));

    private static readonly Regex UnsafeHtmlRegex = new(
        "<(script|style|iframe|object|embed)\\b[^>]*>.*?</\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex HeadingHtmlRegex = new(
        "<h([1-6])\\b[^>]*>(.*?)</h\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ListItemHtmlRegex = new(
        "<li\\b[^>]*>(.*?)</li>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex LinkHtmlRegex = new(
        "<a\\b[^>]*href=[\"'](https://[^\"']+)[\"'][^>]*>(.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex BlockHtmlRegex = new(
        "<(br\\s*/?|/p|/div|/section|/article|/blockquote|hr\\s*/?)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex MarkdownImageRegex = new(@"!\[([^]]*)]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^]]+)]\((https://[^)]+)\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownMarkerRegex = new("(\\*\\*|__|~~|`|(?<!\\*)\\*(?!\\*)|(?<!_)_(?!_))", RegexOptions.Compiled);

    public MiaomiaoRichTextView()
    {
        InitializeComponent();
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == CompactProperty)
        {
            Render();
        }
    }

    private void Render()
    {
        if (contentPanel == null)
        {
            return;
        }

        contentPanel.Children.Clear();
        var source = Normalize(Source);
        if (source.IsNullOrEmpty())
        {
            contentPanel.Children.Add(CreateText("暂无说明", 12, FontWeight.Normal, muted: true));
            return;
        }

        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.IsNullOrEmpty())
            {
                continue;
            }

            if (line is "---" or "***" or "___")
            {
                contentPanel.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 3),
                    Background = Brushes.Gray,
                    Opacity = 0.3
                });
                continue;
            }

            var headingLevel = 0;
            while (headingLevel < line.Length && headingLevel < 6 && line[headingLevel] == '#')
            {
                headingLevel++;
            }
            if (headingLevel > 0 && line.Length > headingLevel && char.IsWhiteSpace(line[headingLevel]))
            {
                var headingSize = Compact ? 13 : Math.Max(15, 21 - headingLevel);
                contentPanel.Children.Add(CreateText(
                    CleanInline(line[(headingLevel + 1)..]),
                    headingSize,
                    FontWeight.SemiBold,
                    muted: false));
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                contentPanel.Children.Add(new Border
                {
                    Padding = new Thickness(10, 7),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Child = CreateText(CleanInline(line[2..]), Compact ? 12 : 13, FontWeight.Normal, muted: true)
                });
                continue;
            }

            var isBullet = line.StartsWith("- ", StringComparison.Ordinal)
                || line.StartsWith("* ", StringComparison.Ordinal)
                || line.StartsWith("+ ", StringComparison.Ordinal)
                || line.StartsWith("• ", StringComparison.Ordinal);
            if (isBullet)
            {
                contentPanel.Children.Add(CreateText(
                    $"•  {CleanInline(line[2..])}",
                    Compact ? 12 : 13,
                    FontWeight.Normal,
                    muted: false));
                continue;
            }

            var orderedMatch = Regex.Match(line, "^([0-9]+)[.)]\\s+(.+)$");
            if (orderedMatch.Success)
            {
                contentPanel.Children.Add(CreateText(
                    $"{orderedMatch.Groups[1].Value}.  {CleanInline(orderedMatch.Groups[2].Value)}",
                    Compact ? 12 : 13,
                    FontWeight.Normal,
                    muted: false));
                continue;
            }

            var boldParagraph = (line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith("**", StringComparison.Ordinal))
                || (line.StartsWith("__", StringComparison.Ordinal) && line.EndsWith("__", StringComparison.Ordinal));
            contentPanel.Children.Add(CreateText(
                CleanInline(line),
                Compact ? 12 : 13,
                boldParagraph ? FontWeight.SemiBold : FontWeight.Normal,
                muted: Compact));
        }
    }

    private TextBlock CreateText(string text, double fontSize, FontWeight weight, bool muted)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            TextWrapping = TextWrapping.Wrap,
            Opacity = muted ? 0.72 : 1
        };
    }

    private static string Normalize(string? source)
    {
        if (source.IsNullOrEmpty())
        {
            return string.Empty;
        }

        var text = source!.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (!text.Contains('<'))
        {
            return text;
        }

        text = UnsafeHtmlRegex.Replace(text, string.Empty);
        text = HeadingHtmlRegex.Replace(text, match =>
            $"\n{new string('#', int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))} {match.Groups[2].Value}\n");
        text = ListItemHtmlRegex.Replace(text, match => $"\n- {match.Groups[1].Value}\n");
        text = LinkHtmlRegex.Replace(text, match => $"[{match.Groups[2].Value}]({match.Groups[1].Value})");
        text = BlockHtmlRegex.Replace(text, "\n");
        text = TagRegex.Replace(text, string.Empty);
        return WebUtility.HtmlDecode(text);
    }

    private static string CleanInline(string text)
    {
        text = MarkdownImageRegex.Replace(text, "$1");
        text = MarkdownLinkRegex.Replace(text, "$1 ($2)");
        return MarkdownMarkerRegex.Replace(text, string.Empty).Trim();
    }
}
