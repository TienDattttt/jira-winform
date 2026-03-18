using System.Diagnostics;
using JiraClone.Application.Integrations;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Forms.Integrations;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public sealed class IssueIntegrationsControl : UserControl
{
    private readonly AppSession _session;
    private readonly FlowLayoutPanel _commitsPanel = CreateItemsPanel();
    private readonly FlowLayoutPanel _pullRequestsPanel = CreateItemsPanel();
    private readonly FlowLayoutPanel _confluencePagesPanel = CreateItemsPanel();
    private readonly Button _addConfluencePage = JiraControlFactory.CreateSecondaryButton("Add Page Link");
    private readonly Button _createConfluencePage = JiraControlFactory.CreatePrimaryButton("Create Confluence Page");
    private readonly Label _confluenceStatus = JiraControlFactory.CreateLabel(string.Empty, true);
    private int _issueId;
    private int _projectId;
    private bool _confluenceConfigured;
    private bool _loading;

    public IssueIntegrationsControl(AppSession session)
    {
        _session = session;
        Dock = DockStyle.Fill;
        BackColor = JiraTheme.BgSurface;

        _addConfluencePage.AutoSize = false;
        _addConfluencePage.Size = new Size(118, 30);
        _addConfluencePage.Click += OnAddConfluencePageClick;
        _createConfluencePage.AutoSize = false;
        _createConfluencePage.Size = new Size(162, 30);
        _createConfluencePage.Click += OnCreateConfluencePageClick;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = JiraTheme.BgSurface };
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        content.Controls.Add(BuildSection("Confluence Pages", _confluencePagesPanel, BuildConfluenceActions()));
        content.Controls.Add(BuildSection("Commits", _commitsPanel));
        content.Controls.Add(BuildSection("Pull Requests", _pullRequestsPanel));
        scroll.Controls.Add(content);
        Controls.Add(scroll);
    }

    public event EventHandler? DataChanged;

    public async Task LoadAsync(int issueId, int projectId, CancellationToken cancellationToken = default)
    {
        if (_loading)
        {
            return;
        }

        _issueId = issueId;
        _projectId = projectId;
        try
        {
            _loading = true;
            var commitsTask = _session.Integrations.GetIssueCommitsAsync(issueId, cancellationToken);
            var pullRequestsTask = _session.Integrations.GetIssuePullRequestsAsync(issueId, cancellationToken);
            var pagesTask = _session.Integrations.GetIssueConfluencePagesAsync(issueId, cancellationToken);
            var confluenceConfigTask = _session.Integrations.GetConfluenceConfigAsync(projectId, cancellationToken);
            await Task.WhenAll(commitsTask, pullRequestsTask, pagesTask, confluenceConfigTask);

            _confluenceConfigured = confluenceConfigTask.Result is not null;
            _confluenceStatus.Text = _confluenceConfigured ? "Connected" : "Not configured";
            _confluenceStatus.ForeColor = _confluenceConfigured ? JiraTheme.Green700 : JiraTheme.TextSecondary;
            _createConfluencePage.Enabled = _confluenceConfigured;

            RenderCommits(commitsTask.Result);
            RenderPullRequests(pullRequestsTask.Result);
            RenderConfluencePages(pagesTask.Result);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _loading = false;
        }
    }

    private Control BuildConfluenceActions()
    {
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 8),
        };
        _confluenceStatus.Margin = new Padding(0, 8, 12, 0);
        actions.Controls.Add(_confluenceStatus);
        actions.Controls.Add(_addConfluencePage);
        actions.Controls.Add(_createConfluencePage);
        return actions;
    }

    private static Panel BuildSection(string title, Control content, Control? actions = null)
    {
        var section = new Panel { Width = 520, Height = 154, BackColor = JiraTheme.BgSurface, Margin = new Padding(0, 0, 0, 12) };
        var header = new Panel { Dock = DockStyle.Top, Height = actions is null ? 26 : 64, BackColor = JiraTheme.BgSurface };
        var label = JiraControlFactory.CreateLabel(title);
        label.Dock = DockStyle.Top;
        label.Font = JiraTheme.FontSmall;
        header.Controls.Add(label);
        if (actions is not null)
        {
            header.Controls.Add(actions);
        }

        content.Dock = DockStyle.Fill;
        section.Controls.Add(content);
        section.Controls.Add(header);
        return section;
    }

    private static FlowLayoutPanel CreateItemsPanel() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        BackColor = JiraTheme.BgSurface,
        Margin = new Padding(0),
        Padding = new Padding(0),
    };

    private void RenderCommits(IReadOnlyList<GitHubCommitLinkDto> commits)
    {
        RenderItems(
            _commitsPanel,
            commits.Take(8).Select(commit => CreateLinkItem(
                $"{commit.Sha}  {commit.Message}",
                $"{commit.Author} • {commit.TimestampUtc.ToLocalTime():dd MMM yyyy HH:mm}",
                commit.Url))
                .ToList(),
            "No linked commits yet.");
    }

    private void RenderPullRequests(IReadOnlyList<GitHubPullRequestLinkDto> pullRequests)
    {
        RenderItems(
            _pullRequestsPanel,
            pullRequests.Take(8).Select(pullRequest => CreateLinkItem(
                $"#{pullRequest.Number}  {pullRequest.Title}",
                $"{pullRequest.State} • {pullRequest.Author} • {pullRequest.UpdatedAtUtc.ToLocalTime():dd MMM yyyy HH:mm}",
                pullRequest.Url))
                .ToList(),
            "No linked pull requests yet.");
    }

    private void RenderConfluencePages(IReadOnlyList<ConfluencePageLinkDto> pages)
    {
        RenderItems(
            _confluencePagesPanel,
            pages.Take(8).Select(page => CreateLinkItem(
                page.Title,
                $"Linked {page.LinkedAtUtc.ToLocalTime():dd MMM yyyy HH:mm}",
                page.Url))
                .ToList(),
            "No Confluence pages linked yet.");
    }

    private static void RenderItems(FlowLayoutPanel host, IReadOnlyList<Control> items, string emptyText)
    {
        host.SuspendLayout();
        try
        {
            foreach (Control control in host.Controls)
            {
                control.Dispose();
            }
            host.Controls.Clear();

            if (items.Count == 0)
            {
                var empty = JiraControlFactory.CreateLabel(emptyText, true);
                empty.AutoSize = true;
                empty.Margin = new Padding(0, 8, 0, 0);
                host.Controls.Add(empty);
                return;
            }

            foreach (var item in items)
            {
                host.Controls.Add(item);
            }
        }
        finally
        {
            host.ResumeLayout();
        }
    }

    private static Control CreateLinkItem(string title, string subtitle, string url)
    {
        var item = new Panel { Width = 500, Height = 46, BackColor = JiraTheme.BgSurface, Margin = new Padding(0, 0, 0, 8) };
        var link = new LinkLabel
        {
            AutoSize = false,
            Width = 500,
            Height = 22,
            Text = title,
            LinkColor = JiraTheme.PrimaryActive,
            ActiveLinkColor = JiraTheme.Primary,
            VisitedLinkColor = JiraTheme.PrimaryActive,
            BackColor = JiraTheme.BgSurface,
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        var caption = JiraControlFactory.CreateLabel(subtitle, true);
        caption.Location = new Point(0, 24);
        caption.AutoSize = true;
        item.Controls.Add(caption);
        item.Controls.Add(link);
        return item;
    }

    private async void OnAddConfluencePageClick(object? sender, EventArgs e)
    {
        if (_issueId <= 0)
        {
            return;
        }

        try
        {
            using var dialog = new ConfluencePageLinkDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            var currentUserId = _session.CurrentUserContext.RequireUserId();
            await _session.Integrations.AddConfluencePageLinkAsync(_issueId, dialog.PageTitle, dialog.PageUrl, currentUserId);
            await LoadAsync(_issueId, _projectId);
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async void OnCreateConfluencePageClick(object? sender, EventArgs e)
    {
        if (_issueId <= 0 || !_confluenceConfigured)
        {
            return;
        }

        try
        {
            var currentUserId = _session.CurrentUserContext.RequireUserId();
            await _session.Integrations.CreateConfluencePageFromIssueAsync(_issueId, currentUserId);
            await LoadAsync(_issueId, _projectId);
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
