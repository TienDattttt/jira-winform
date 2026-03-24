using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class ProjectListForm : UserControl
{
    private readonly AppSession _session;
    private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Your Projects");
    private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel("Browse every project you can access, switch context instantly, or create a new one.", true);
    private readonly Label _countBadge = JiraControlFactory.CreateLabel("0 projects", true);
    private readonly Button _createProjectButton = JiraControlFactory.CreatePrimaryButton("+ Create Project");
    private readonly Button _openProjectButton = JiraControlFactory.CreateSecondaryButton("Open Project");
    private readonly Button _cardsViewButton = JiraControlFactory.CreateSecondaryButton("Cards");
    private readonly Button _gridViewButton = JiraControlFactory.CreateSecondaryButton("Grid");
    private readonly ListView _gridView = new()
    {
        Dock = DockStyle.Fill,
        FullRowSelect = true,
        MultiSelect = false,
        View = View.Details,
        BorderStyle = BorderStyle.None,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        HideSelection = false,
    };
    private readonly Label _emptyState = JiraControlFactory.CreateLabel("You do not have any active projects yet.", true);
    private readonly FlowLayoutPanel _cardsPanel = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        BackColor = JiraTheme.BgSurface,
        Padding = new Padding(16),
        Margin = new Padding(0),
    };
    private readonly Panel _cardsScrollPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = JiraTheme.BgSurface };

    private IReadOnlyList<Project> _projects = Array.Empty<Project>();
    private string _shellSearch = string.Empty;
    private bool _cardsMode = true;

    public ProjectListForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _titleLabel.Font = JiraTheme.FontH1;
        _countBadge.AutoSize = true;
        _countBadge.BackColor = JiraTheme.Blue100;
        _countBadge.ForeColor = JiraTheme.PrimaryActive;
        _countBadge.Padding = new Padding(10, 6, 10, 6);
        _countBadge.Margin = new Padding(12, 4, 0, 0);

        ConfigureActionButton(_createProjectButton, 172);
        ConfigureActionButton(_openProjectButton, 142);
        ConfigureActionButton(_cardsViewButton, 108);
        ConfigureActionButton(_gridViewButton, 108);

        _createProjectButton.Click += async (_, _) => await CreateProjectAsync();
        _openProjectButton.Click += async (_, _) => await OpenSelectedProjectAsync();
        _cardsViewButton.Click += (_, _) => SetViewMode(cardsMode: true);
        _gridViewButton.Click += (_, _) => SetViewMode(cardsMode: false);

        ConfigureGrid();
        _cardsScrollPanel.Controls.Add(_cardsPanel);
        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Visible = false;

        Controls.Add(BuildSurface());
        Controls.Add(BuildHeader());

        Load += async (_, _) => await LoadProjectsAsync();
        _session.ProjectChanged += HandleSessionProjectChanged;
        UpdateButtonStates();
        SetViewMode(cardsMode: true);
    }

    public event EventHandler? ProjectOpened;

    public Task RefreshProjectsAsync(CancellationToken cancellationToken = default) => LoadProjectsAsync(cancellationToken);

    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        BindProjects();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session.ProjectChanged -= HandleSessionProjectChanged;
        }

        base.Dispose(disposing);
    }

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 132,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 18, 20, 10),
        };

        var titleRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        titleRow.Controls.Add(_titleLabel);
        titleRow.Controls.Add(_countBadge);

        titleRow.Location = new Point(0, 0);
        _subtitleLabel.Location = new Point(0, 52);
        header.Controls.Add(titleRow);
        header.Controls.Add(_subtitleLabel);
        return header;
    }

    private Control BuildSurface()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            BackColor = JiraTheme.BgPage,
        };

        var surface = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
        };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };

        var toolbarHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(16, 12, 16, 8),
        };

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = JiraTheme.BgSurface,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _cardsViewButton.Margin = new Padding(0, 0, 12, 0);
        _gridViewButton.Margin = new Padding(0, 0, 12, 0);
        _openProjectButton.Margin = new Padding(0, 0, 12, 0);
        _createProjectButton.Margin = new Padding(0);
        toolbar.Controls.Add(_cardsViewButton);
        toolbar.Controls.Add(_gridViewButton);
        toolbar.Controls.Add(_openProjectButton);
        toolbar.Controls.Add(_createProjectButton);
        toolbarHost.Controls.Add(toolbar);

        surface.Controls.Add(_emptyState);
        surface.Controls.Add(_cardsScrollPanel);
        surface.Controls.Add(_gridView);
        surface.Controls.Add(toolbarHost);
        host.Controls.Add(surface);
        return host;
    }

    private void ConfigureGrid()
    {
        JiraTheme.StyleListView(_gridView);
        _gridView.Columns.Add("Avatar", 92);
        _gridView.Columns.Add("Name", 240);
        _gridView.Columns.Add("Key", 120);
        _gridView.Columns.Add("Category", 140);
        _gridView.Columns.Add("Members", 120);
        _gridView.SelectedIndexChanged += (_, _) => UpdateButtonStates();
        _gridView.DoubleClick += async (_, _) => await OpenSelectedProjectAsync();
        _gridView.Resize += (_, _) => ApplyResponsiveColumns();
    }

    private async Task LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _projects = await _session.Projects.GetAccessibleProjectsAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            BindProjects();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void BindProjects()
    {
        var filtered = _projects
            .Where(project => string.IsNullOrWhiteSpace(_shellSearch)
                || project.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || project.Key.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                || project.Category.ToString().Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(project => project.Name)
            .ToList();

        _countBadge.Text = filtered.Count == 1 ? "1 project" : $"{filtered.Count} projects";
        BindGrid(filtered);
        BindCards(filtered);
        _emptyState.Visible = filtered.Count == 0;
        _gridView.Visible = !_cardsMode && filtered.Count > 0;
        _cardsScrollPanel.Visible = _cardsMode && filtered.Count > 0;
        UpdateButtonStates();
    }

    private void BindGrid(IReadOnlyList<Project> projects)
    {
        _gridView.BeginUpdate();
        _gridView.Items.Clear();
        foreach (var project in projects)
        {
            var item = new ListViewItem(BuildProjectAvatarText(project)) { Tag = project };
            item.SubItems.Add(project.Name);
            item.SubItems.Add(project.Key);
            item.SubItems.Add(project.Category.ToString());
            item.SubItems.Add(project.Members.Count.ToString());
            if (_session.ActiveProject?.Id == project.Id)
            {
                item.BackColor = JiraTheme.SelectionBg;
            }

            _gridView.Items.Add(item);
        }
        _gridView.EndUpdate();
        ApplyResponsiveColumns();
    }

    private void BindCards(IReadOnlyList<Project> projects)
    {
        _cardsPanel.SuspendLayout();
        _cardsPanel.Controls.Clear();
        foreach (var project in projects)
        {
            var card = new ProjectCard(project, _session.ActiveProject?.Id == project.Id)
            {
                Margin = new Padding(0, 0, 16, 16),
            };
            card.ProjectClicked += async (_, _) => await OpenProjectAsync(project.Id, navigateToBoard: true);
            _cardsPanel.Controls.Add(card);
        }
        _cardsPanel.ResumeLayout();
    }

    private void ApplyResponsiveColumns()
    {
        if (_gridView.ClientSize.Width <= 0 || _gridView.Columns.Count < 5)
        {
            return;
        }

        var avatarWidth = 90;
        var keyWidth = 120;
        var categoryWidth = 140;
        var membersWidth = 100;
        var nameWidth = Math.Max(220, _gridView.ClientSize.Width - avatarWidth - keyWidth - categoryWidth - membersWidth - 14);

        _gridView.Columns[0].Width = avatarWidth;
        _gridView.Columns[1].Width = nameWidth;
        _gridView.Columns[2].Width = keyWidth;
        _gridView.Columns[3].Width = categoryWidth;
        _gridView.Columns[4].Width = membersWidth;
    }

    private void SetViewMode(bool cardsMode)
    {
        _cardsMode = cardsMode;
        ApplyToggleState(_cardsViewButton, _cardsMode);
        ApplyToggleState(_gridViewButton, !_cardsMode);
        _gridView.Visible = !_cardsMode && _gridView.Items.Count > 0;
        _cardsScrollPanel.Visible = _cardsMode && _cardsPanel.Controls.Count > 0;
        _emptyState.Visible = !_gridView.Visible && !_cardsScrollPanel.Visible;
        UpdateButtonStates();
    }

    private static void ApplyToggleState(Button button, bool active)
    {
        button.BackColor = active ? JiraTheme.Blue100 : JiraTheme.BgSurface;
        button.ForeColor = active ? JiraTheme.PrimaryActive : JiraTheme.TextPrimary;
        button.FlatAppearance.BorderColor = active ? JiraTheme.Primary : JiraTheme.Border;
    }

    private async Task CreateProjectAsync()
    {
        try
        {
            using var dialog = new CreateProjectForm(_session);
            if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.CreatedProjectId.HasValue)
            {
                return;
            }

            await _session.SetActiveProjectAsync(dialog.CreatedProjectId.Value);
            await LoadProjectsAsync();
            ProjectOpened?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async Task OpenSelectedProjectAsync()
    {
        if (_gridView.SelectedItems.Count == 0 || _gridView.SelectedItems[0].Tag is not Project project)
        {
            return;
        }

        await OpenProjectAsync(project.Id, navigateToBoard: true);
    }

    private async Task OpenProjectAsync(int projectId, bool navigateToBoard)
    {
        try
        {
            await _session.SetActiveProjectAsync(projectId);
            await LoadProjectsAsync();
            if (navigateToBoard)
            {
                ProjectOpened?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void UpdateButtonStates()
    {
        var canCreate = _session.Authorization.IsInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager);
        _createProjectButton.Enabled = canCreate;
        _openProjectButton.Enabled = !_cardsMode && _gridView.SelectedItems.Count > 0;
    }

    private async void HandleSessionProjectChanged(object? sender, AppSession.ProjectChangedEventArgs eventArgs)
    {
        if (IsDisposed)
        {
            return;
        }

        await LoadProjectsAsync();
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
        button.MinimumSize = new Size(width, 36);
    }

    private static string BuildProjectAvatarText(Project project)
    {
        var initials = BuildProjectInitials(project);
        return initials.Length >= 2 ? initials[..2] : initials;
    }

    private static string BuildProjectInitials(Project project)
    {
        var parts = project.Name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => char.ToUpperInvariant(part[0]))
            .Take(2)
            .ToArray();
        if (parts.Length > 0)
        {
            return new string(parts);
        }

        return project.Key.Length >= 2 ? project.Key[..2].ToUpperInvariant() : project.Key.ToUpperInvariant();
    }

    private sealed class ProjectCard : Control
    {
        private readonly Project _project;
        private readonly string _initials;
        private bool _hovered;

        public ProjectCard(Project project, bool active)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _project = project;
            _initials = BuildProjectInitials(project);
            Active = active;
            Size = new Size(340, 196);
            Cursor = Cursors.Hand;
            BackColor = JiraTheme.BgSurface;

            MouseEnter += (_, _) => { _hovered = true; Invalidate(); };
            MouseLeave += (_, _) => { _hovered = false; Invalidate(); };
            Click += (_, _) => ProjectClicked?.Invoke(this, EventArgs.Empty);
        }

        public bool Active { get; }

        public event EventHandler? ProjectClicked;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var background = Active ? JiraTheme.Blue100 : _hovered ? JiraTheme.Neutral100 : JiraTheme.BgSurface;
            var border = Active ? JiraTheme.Primary : JiraTheme.Border;
            using var backgroundBrush = new SolidBrush(background);
            using var borderPen = new Pen(border, Active ? 1.5f : 1f);
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
            e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);

            using var avatarBrush = new SolidBrush(JiraTheme.Blue600);
            e.Graphics.FillEllipse(avatarBrush, 18, 18, 42, 42);
            TextRenderer.DrawText(e.Graphics, _initials, JiraTheme.FontCaption, new Rectangle(18, 18, 42, 42), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(e.Graphics, _project.Name, JiraTheme.FontH2, new Rectangle(74, 16, Width - 92, 34), JiraTheme.TextPrimary, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, $"{_project.Key}  |  {_project.Category}", JiraTheme.FontCaption, new Rectangle(74, 50, Width - 92, 24), JiraTheme.TextSecondary, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, _project.Description ?? "No project description yet.", JiraTheme.FontSmall, new Rectangle(18, 82, Width - 36, 72), JiraTheme.TextSecondary, TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
            TextRenderer.DrawText(e.Graphics, $"{_project.Members.Count} member{(_project.Members.Count == 1 ? string.Empty : "s")}", JiraTheme.FontSmall, new Rectangle(18, 164, Width - 36, 20), JiraTheme.PrimaryActive, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}




