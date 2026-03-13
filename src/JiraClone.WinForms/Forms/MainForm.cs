using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class MainForm : Form
{
    private readonly AppSession _session;
    private readonly string _displayName;
    private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgPage };
    private readonly Label _breadcrumbLabel = new() { AutoSize = true, Font = JiraTheme.FontSmall, ForeColor = JiraTheme.TextSecondary };
    private readonly TextBox _searchBox = JiraControlFactory.CreateTextBox();
    private readonly SidebarNavItem _boardItem = new(NavKind.Board, "Board");
    private readonly SidebarNavItem _backlogItem = new(NavKind.Backlog, "Backlog");
    private readonly SidebarNavItem _sprintsItem = new(NavKind.Sprints, "Sprints");
    private readonly SidebarNavItem _issuesItem = new(NavKind.Issues, "Issues");
    private readonly SidebarNavItem _settingsItem = new(NavKind.Settings, "Settings");
    private readonly InitialsAvatar _sidebarAvatar;
    private readonly InitialsAvatar _navbarAvatar;
    private readonly Label _sidebarUserLabel;
    private readonly Button _logoutButton;

    private string _projectName = "Project";
    private SidebarNavItem? _activeNavItem;
    private Control? _activeContent;

    public MainForm(AppSession session, string displayName)
    {
        _session = session;
        _displayName = session.CurrentUserContext.CurrentUser?.DisplayName ?? displayName;

        Text = $"Jira Clone Desktop - {_displayName}";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;

        _searchBox.Width = 240;
        _searchBox.MinimumSize = new Size(220, 36);
        _searchBox.PlaceholderText = "Search project";

        var initials = BuildInitials(_displayName);
        _sidebarAvatar = new InitialsAvatar(initials, 32) { BackCircleColor = JiraTheme.Blue600 };
        _navbarAvatar = new InitialsAvatar(initials, 32) { BackCircleColor = JiraTheme.Blue600 };
        _sidebarUserLabel = JiraControlFactory.CreateLabel(_displayName);
        _sidebarUserLabel.ForeColor = Color.FromArgb(220, 255, 255, 255);
        _sidebarUserLabel.Font = JiraTheme.FontSmall;
        _sidebarUserLabel.AutoEllipsis = true;
        _sidebarUserLabel.MaximumSize = new Size(150, 30);
        _logoutButton = JiraControlFactory.CreateSecondaryButton("Logout");
        _logoutButton.AutoSize = false;
        _logoutButton.Width = 124;
        _logoutButton.Height = 40;
        _logoutButton.MinimumSize = new Size(124, 36);
        _logoutButton.Click += (_, _) => Logout();

        BuildLayout();
        WireNavigation();

        Shown += async (_, _) => await LoadProjectContextAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = JiraTheme.BgPage,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, JiraTheme.SidebarWidth));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMainArea(), 1, 0);
        Controls.Add(root);
    }

    private Control BuildSidebar()
    {
        var sidebar = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSidebar,
            Padding = new Padding(0, 20, 0, 20),
        };

        var brandRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 62,
            Padding = new Padding(20, 8, 20, 8),
            BackColor = JiraTheme.BgSidebar,
        };

        var brandAvatar = new InitialsAvatar("J", 34) { BackCircleColor = JiraTheme.Blue600 };
        var brandLabel = JiraControlFactory.CreateLabel("Jira Clone");
        brandLabel.ForeColor = Color.White;
        brandLabel.Font = JiraTheme.FontH2;
        brandLabel.Location = new Point(52, 8);
        brandRow.Controls.Add(brandAvatar);
        brandRow.Controls.Add(brandLabel);
        brandAvatar.Location = new Point(20, 7);

        var separatorTop = JiraControlFactory.CreateSeparator();
        separatorTop.Dock = DockStyle.Top;
        separatorTop.BackColor = Color.FromArgb(28, 255, 255, 255);

        var navStack = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 14, 0, 0),
            BackColor = JiraTheme.BgSidebar,
        };
        navStack.Controls.AddRange([_boardItem, _backlogItem, _sprintsItem, _issuesItem, _settingsItem]);

        var bottomSection = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 140,
            Padding = new Padding(20, 16, 20, 0),
            BackColor = JiraTheme.BgSidebar,
        };

        var bottomSeparator = JiraControlFactory.CreateSeparator();
        bottomSeparator.Dock = DockStyle.Top;
        bottomSeparator.BackColor = Color.FromArgb(28, 255, 255, 255);

        var userRow = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = JiraTheme.BgSidebar };
        userRow.Controls.Add(_sidebarAvatar);
        userRow.Controls.Add(_sidebarUserLabel);
        _sidebarAvatar.Location = new Point(0, 2);
        _sidebarUserLabel.Location = new Point(44, 8);

        var logoutHost = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = JiraTheme.BgSidebar };
        logoutHost.Controls.Add(_logoutButton);
        _logoutButton.Location = new Point(0, 6);

        bottomSection.Controls.Add(logoutHost);
        bottomSection.Controls.Add(userRow);
        bottomSection.Controls.Add(bottomSeparator);

        sidebar.Controls.Add(bottomSection);
        sidebar.Controls.Add(navStack);
        sidebar.Controls.Add(separatorTop);
        sidebar.Controls.Add(brandRow);
        return sidebar;
    }

    private Control BuildMainArea()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = JiraTheme.BgPage,
        };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, JiraTheme.NavbarHeight));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.Controls.Add(BuildNavbar(), 0, 0);
        main.Controls.Add(_contentPanel, 0, 1);
        return main;
    }

    private Control BuildNavbar()
    {
        var navbar = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(20, 12, 20, 12),
        };

        navbar.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawLine(pen, 0, navbar.Height - 1, navbar.Width, navbar.Height - 1);
        };

        var rightPanel = new Panel { Dock = DockStyle.Right, Width = 336, BackColor = JiraTheme.BgSurface };
        rightPanel.Controls.Add(_navbarAvatar);
        rightPanel.Controls.Add(_searchBox);
        rightPanel.Resize += (_, _) =>
        {
            _navbarAvatar.Location = new Point(rightPanel.Width - _navbarAvatar.Width, 0);
            _searchBox.Location = new Point(rightPanel.Width - _navbarAvatar.Width - _searchBox.Width - 16, 0);
        };

        var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        leftPanel.Controls.Add(_breadcrumbLabel);
        _breadcrumbLabel.Location = new Point(0, 6);

        navbar.Controls.Add(rightPanel);
        navbar.Controls.Add(leftPanel);
        return navbar;
    }

    private void WireNavigation()
    {
        _boardItem.Click += (_, _) => NavigateTo(_boardItem, () => new BoardForm(_session));
        _backlogItem.Click += (_, _) => NavigateTo(_backlogItem, () => new BoardForm(_session));
        _sprintsItem.Click += (_, _) => NavigateTo(_sprintsItem, () => new SprintManagementForm(_session));
        _issuesItem.Click += (_, _) => NavigateTo(_issuesItem, () => new IssueLauncherView(_session));
        _settingsItem.Click += (_, _) => NavigateTo(_settingsItem, () => new ProjectSettingsForm(_session));
    }

    private void NavigateTo(SidebarNavItem navItem, Func<Control> createContent)
    {
        _activeNavItem?.SetActive(false);
        _activeNavItem = navItem;
        _activeNavItem.SetActive(true);

        if (_activeContent is not null)
        {
            _contentPanel.Controls.Remove(_activeContent);
            _activeContent.Dispose();
        }

        _activeContent = createContent();
        _activeContent.Dock = DockStyle.Fill;
        _contentPanel.Controls.Add(_activeContent);
        _breadcrumbLabel.Text = $"Projects > {_projectName} > {navItem.TextLabel}";
    }

    private async Task LoadProjectContextAsync()
    {
        var project = await _session.Projects.GetActiveProjectAsync();
        if (project is not null)
        {
            _projectName = project.Name;
        }

        NavigateTo(_boardItem, () => new BoardForm(_session));
    }

    private void Logout()
    {
        if (MessageBox.Show(this, "Log out from Jira Clone?", "Logout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _session.CurrentUserContext.Clear();
        System.Windows.Forms.Application.Restart();
        Close();
    }

    private static string BuildInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])).ToArray();
        return parts.Length == 0 ? "U" : new string(parts);
    }

    private class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private enum NavKind
    {
        Board,
        Backlog,
        Sprints,
        Issues,
        Settings
    }

    private sealed class SidebarNavItem : DoubleBufferedPanel
    {
        private readonly NavKind _kind;
        private bool _hovered;
        private bool _active;

        public SidebarNavItem(NavKind kind, string text)
        {
            _kind = kind;
            TextLabel = text;
            Height = 44;
            Width = JiraTheme.SidebarWidth;
            Cursor = Cursors.Hand;
            Margin = new Padding(0);
            BackColor = Color.Transparent;

            MouseEnter += (_, _) => { _hovered = true; Invalidate(); };
            MouseLeave += (_, _) => { _hovered = false; Invalidate(); };
        }

        public string TextLabel { get; }

        public void SetActive(bool active)
        {
            _active = active;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var background = _active ? JiraTheme.Blue600 : _hovered ? JiraTheme.SidebarHover : Color.Transparent;
            using var backgroundBrush = new SolidBrush(background);
            e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);

            if (_active)
            {
                using var activeBrush = new SolidBrush(Color.White);
                e.Graphics.FillRectangle(activeBrush, 0, 0, 3, Height);
            }

            DrawIcon(e.Graphics, new Rectangle(18, 8, 20, 28));
            TextRenderer.DrawText(e.Graphics, TextLabel, JiraTheme.FontSmall, new Rectangle(48, 0, Width - 64, Height), JiraTheme.SidebarText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void DrawIcon(Graphics graphics, Rectangle bounds)
        {
            using var pen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fill = new SolidBrush(Color.White);
            var middleY = bounds.Top + bounds.Height / 2;

            switch (_kind)
            {
                case NavKind.Board:
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y + 2, 7, 7);
                    graphics.DrawRectangle(pen, bounds.X + 10, bounds.Y + 2, 7, 7);
                    graphics.DrawRectangle(pen, bounds.X, bounds.Y + 13, 7, 7);
                    graphics.DrawRectangle(pen, bounds.X + 10, bounds.Y + 13, 7, 7);
                    break;
                case NavKind.Backlog:
                    graphics.DrawLine(pen, bounds.X, middleY - 7, bounds.Right, middleY - 7);
                    graphics.DrawLine(pen, bounds.X, middleY, bounds.Right, middleY);
                    graphics.DrawLine(pen, bounds.X, middleY + 7, bounds.Right, middleY + 7);
                    break;
                case NavKind.Sprints:
                    graphics.DrawLine(pen, bounds.X + 1, bounds.Y + 4, bounds.Right - 2, middleY);
                    graphics.DrawLine(pen, bounds.Right - 2, middleY, bounds.X + 1, bounds.Bottom - 4);
                    break;
                case NavKind.Issues:
                    graphics.FillEllipse(fill, bounds.X + 4, bounds.Y + 6, 10, 10);
                    break;
                case NavKind.Settings:
                    graphics.DrawEllipse(pen, bounds.X + 4, bounds.Y + 6, 10, 10);
                    graphics.DrawLine(pen, bounds.X + 9, bounds.Y + 1, bounds.X + 9, bounds.Y + 5);
                    graphics.DrawLine(pen, bounds.X + 9, bounds.Bottom - 1, bounds.X + 9, bounds.Bottom - 5);
                    graphics.DrawLine(pen, bounds.X + 1, middleY, bounds.X + 5, middleY);
                    graphics.DrawLine(pen, bounds.Right - 1, middleY, bounds.Right - 5, middleY);
                    break;
            }
        }
    }

    private sealed class InitialsAvatar : Control
    {
        public InitialsAvatar(string initials, int size)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Initials = initials;
            Size = new Size(size, size);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public string Initials { get; }
        public Color BackCircleColor { get; set; } = JiraTheme.Blue600;
        public Color ForeTextColor { get; set; } = Color.White;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(BackCircleColor);
            e.Graphics.FillEllipse(fill, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, Initials, JiraTheme.FontCaption, ClientRectangle, ForeTextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private sealed class IssueLauncherView : UserControl
    {
        private readonly AppSession _session;
        private readonly Label _hint = JiraControlFactory.CreateLabel("Create a new issue for the active project.", true);
        private readonly Button _createButton = JiraControlFactory.CreatePrimaryButton("New Issue");

        public IssueLauncherView(AppSession session)
        {
            _session = session;
            BackColor = JiraTheme.BgPage;
            var card = new DoubleBufferedPanel { Size = new Size(420, 180), BackColor = JiraTheme.BgSurface, Anchor = AnchorStyles.None };
            card.Paint += (_, e) =>
            {
                using var pen = new Pen(JiraTheme.Border);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            var title = JiraControlFactory.CreateLabel("Issues");
            title.Font = JiraTheme.FontH1;
            title.Location = new Point(24, 24);

            _hint.Location = new Point(24, 70);
            _createButton.AutoSize = false;
            _createButton.Size = new Size(140, 40);
            _createButton.Location = new Point(24, 108);
            _createButton.Click += async (_, _) => await OpenIssueEditorAsync();

            card.Controls.Add(title);
            card.Controls.Add(_hint);
            card.Controls.Add(_createButton);

            Controls.Add(card);
            Resize += (_, _) => CenterCard(card);
            Load += (_, _) => CenterCard(card);
        }

        private async Task OpenIssueEditorAsync()
        {
            var project = await _session.Projects.GetActiveProjectAsync();
            if (project is null)
            {
                MessageBox.Show(this, "No active project found.", "Issues", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new IssueEditorForm(_session, project.Id, null);
            dialog.ShowDialog(this);
        }

        private void CenterCard(Control card)
        {
            card.Location = new Point(Math.Max(24, (ClientSize.Width - card.Width) / 2), Math.Max(24, (ClientSize.Height - card.Height) / 2));
        }
    }
}
