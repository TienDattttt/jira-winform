using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JiraClone.Infrastructure.Session;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class LoginForm : Form
{
    private readonly AppSession _session;
    private readonly ISessionPersistenceService _sessionPersistence;
    private readonly ShadowPanel _cardPanel;
    private readonly TextBox _emailTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly CheckBox _rememberMeCheckBox;
    private readonly Label _errorLabel;
    private readonly Button _loginButton;
    private readonly Button _showPasswordButton;
    private readonly Button _closeButton;

    private Point _dragOrigin;
    private Point _formOrigin;
    private bool _dragging;
    private readonly HashSet<Control> _draggableControls = [];

    public LoginForm(AppSession session, ISessionPersistenceService sessionPersistence)
    {
        _session = session;
        _sessionPersistence = sessionPersistence;

        Text = "Jira Clone Login";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(960, 640);
        BackColor = JiraTheme.BgPage;
        KeyPreview = true;
        DoubleBuffered = true;

        _cardPanel = new ShadowPanel
        {
            Size = new Size(480, 612),
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.None,
        };

        _emailTextBox = JiraControlFactory.CreateTextBox();
        _emailTextBox.Height = 40;
        _emailTextBox.Dock = DockStyle.Fill;
        _emailTextBox.Text = string.Empty;

        _passwordTextBox = JiraControlFactory.CreateTextBox();
        _passwordTextBox.Height = 40;
        _passwordTextBox.Dock = DockStyle.Fill;
        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.Text = string.Empty;

        _rememberMeCheckBox = new CheckBox
        {
            AutoSize = true,
            Text = "Remember me for 30 days",
            Checked = true,
            ForeColor = JiraTheme.TextPrimary,
            BackColor = JiraTheme.BgSurface,
            Font = JiraTheme.FontBody,
            Margin = new Padding(0, 4, 0, 0)
        };

        _showPasswordButton = new Button
        {
            Text = "Show",
            Dock = DockStyle.Right,
            Width = 96,
            FlatStyle = FlatStyle.Flat,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.Primary,
            Font = JiraTheme.FontCaption,
            Cursor = Cursors.Hand,
            TabStop = false,
        };
        _showPasswordButton.MinimumSize = new Size(92, 40);
        _showPasswordButton.FlatAppearance.BorderSize = 0;
        _showPasswordButton.MouseDown += (_, _) => SetPasswordVisibility(true);
        _showPasswordButton.MouseUp += (_, _) => SetPasswordVisibility(false);
        _showPasswordButton.MouseLeave += (_, _) => SetPasswordVisibility(false);

        _errorLabel = JiraControlFactory.CreateLabel(string.Empty, true);
        _errorLabel.ForeColor = JiraTheme.Danger;
        _errorLabel.AutoSize = false;
        _errorLabel.Height = 42;
        _errorLabel.Dock = DockStyle.Top;
        _errorLabel.TextAlign = ContentAlignment.MiddleLeft;
        _errorLabel.Visible = false;

        _loginButton = JiraControlFactory.CreatePrimaryButton("Log in");
        _loginButton.AutoSize = false;
        _loginButton.Dock = DockStyle.Fill;
        _loginButton.MinimumSize = new Size(0, 44);
        _loginButton.Height = 44;
        _loginButton.Click += async (_, _) => await LoginAsync();

        _closeButton = JiraControlFactory.CreateSecondaryButton("X");
        _closeButton.AutoSize = false;
        _closeButton.Size = new Size(36, 32);
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _closeButton.Click += (_, _) => Close();

        AcceptButton = _loginButton;

        BuildLayout();
        WireDragging(_cardPanel);

        Resize += (_, _) => { CenterCard(); PositionChrome(); };
        Shown += (_, _) =>
        {
            CenterCard();
            _emailTextBox.Select();
        };
        KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
    }

    private void BuildLayout()
    {
        Controls.Add(_cardPanel);
        Controls.Add(_closeButton);
        _closeButton.Location = new Point(ClientSize.Width - _closeButton.Width - 16, 16);

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(40, 28, 40, 40),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 11,
            BackColor = JiraTheme.BgSurface,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logoHost = new Panel { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
        var logo = new LogoControl
        {
            Size = new Size(48, 48),
            Location = new Point((logoHost.Width - 48) / 2, 8),
            Anchor = AnchorStyles.Top,
        };
        logoHost.Controls.Add(logo);
        logoHost.Resize += (_, _) => logo.Left = (logoHost.ClientSize.Width - logo.Width) / 2;

        var title = JiraControlFactory.CreateLabel("Log in to Jira Clone");
        title.Font = JiraTheme.FontH1;
        title.TextAlign = ContentAlignment.MiddleCenter;
        title.Dock = DockStyle.Fill;

        var subtitle = JiraControlFactory.CreateLabel("Enter your credentials", true);
        subtitle.Font = JiraTheme.FontSmall;
        subtitle.TextAlign = ContentAlignment.TopCenter;
        subtitle.Dock = DockStyle.Fill;

        var emailLabel = JiraControlFactory.CreateLabel("Username");
        emailLabel.Dock = DockStyle.Fill;
        emailLabel.TextAlign = ContentAlignment.BottomLeft;

        var emailHost = BuildInputHost(_emailTextBox);
        var passwordLabel = JiraControlFactory.CreateLabel("Password");
        passwordLabel.Dock = DockStyle.Fill;
        passwordLabel.TextAlign = ContentAlignment.BottomLeft;
        var passwordHost = BuildPasswordHost();
        var rememberHost = BuildRememberMeHost();

        layout.Controls.Add(logoHost, 0, 0);
        layout.Controls.Add(title, 0, 1);
        layout.Controls.Add(subtitle, 0, 2);
        layout.Controls.Add(emailLabel, 0, 3);
        layout.Controls.Add(emailHost, 0, 4);
        layout.Controls.Add(passwordLabel, 0, 5);
        layout.Controls.Add(passwordHost, 0, 6);
        layout.Controls.Add(rememberHost, 0, 7);
        layout.Controls.Add(_errorLabel, 0, 8);
        layout.Controls.Add(_loginButton, 0, 9);

        content.Controls.Add(layout);
        _cardPanel.Controls.Add(content);
    }

    private Panel BuildInputHost(Control input)
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0),
            Controls = { input }
        };
    }

    private Control BuildPasswordHost()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0),
            MinimumSize = new Size(0, 40),
        };

        host.Controls.Add(_passwordTextBox);
        host.Controls.Add(_showPasswordButton);

        return host;
    }

    private Control BuildRememberMeHost()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0),
            Margin = new Padding(0),
        };
        host.Controls.Add(_rememberMeCheckBox);
        _rememberMeCheckBox.Location = new Point(0, 4);
        return host;
    }

    private void PositionChrome()
    {
        _closeButton.Location = new Point(ClientSize.Width - _closeButton.Width - 16, 16);
    }

    private void CenterCard()
    {
        _cardPanel.Location = new Point(
            (ClientSize.Width - _cardPanel.Width) / 2,
            (ClientSize.Height - _cardPanel.Height) / 2);
    }

    private void WireDragging(Control control)
    {
        if (!_draggableControls.Add(control))
        {
            return;
        }

        control.MouseDown += StartDrag;
        control.MouseMove += DragWindow;
        control.MouseUp += StopDrag;
        control.ControlAdded += HandleDragControlAdded;

        foreach (Control child in control.Controls)
        {
            WireDragging(child);
        }
    }

    private void UnwireDragging(Control control)
    {
        if (!_draggableControls.Remove(control))
        {
            return;
        }

        control.MouseDown -= StartDrag;
        control.MouseMove -= DragWindow;
        control.MouseUp -= StopDrag;
        control.ControlAdded -= HandleDragControlAdded;

        foreach (Control child in control.Controls)
        {
            UnwireDragging(child);
        }
    }

    private void HandleDragControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control is not null)
        {
            WireDragging(e.Control);
        }
    }

    private void StartDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _dragOrigin = Cursor.Position;
        _formOrigin = Location;
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var current = Cursor.Position;
        Location = new Point(
            _formOrigin.X + (current.X - _dragOrigin.X),
            _formOrigin.Y + (current.Y - _dragOrigin.Y));
    }

    private void StopDrag(object? sender, MouseEventArgs e) => _dragging = false;

    private void SetPasswordVisibility(bool visible)
    {
        _passwordTextBox.UseSystemPasswordChar = !visible;
        _showPasswordButton.Text = visible ? "Hide" : "Show";
    }

    private async Task LoginAsync()
    {
        try
        {
            SetBusyState(true);
            HideError();

            var result = await _session.Authentication.LoginAsync(_emailTextBox.Text.Trim(), _passwordTextBox.Text);
            if (!result.Succeeded || result.User is null)
            {
                ShowError(result.ErrorMessage ?? "Login failed.");
                return;
            }

            await ApplyRememberMePreferenceAsync(result.User.Id);

            Hide();
            using var mainForm = new MainForm(_session, result.User.DisplayName, _sessionPersistence);
            mainForm.ShowDialog(this);
            Close();
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private async Task ApplyRememberMePreferenceAsync(int userId)
    {
        if (_rememberMeCheckBox.Checked)
        {
            try
            {
                var sessionData = await _session.Authentication.CreatePersistentSessionAsync(userId);
                await _sessionPersistence.SaveAsync(sessionData);
                return;
            }
            catch (Exception exception)
            {
                try
                {
                    await _session.Authentication.ClearPersistentSessionAsync(userId);
                }
                catch
                {
                }

                ShowRememberMeWarning("Logged in, but remember me could not be enabled.", exception.Message);
                return;
            }
        }

        try
        {
            await _session.Authentication.ClearPersistentSessionAsync(userId);
        }
        catch (Exception exception)
        {
            ShowRememberMeWarning("Logged in, but a previous remembered session could not be cleared.", exception.Message);
        }

        await _sessionPersistence.ClearAsync();
    }

    private void ShowRememberMeWarning(string title, string details)
    {
        MessageBox.Show(
            this,
            $"{title}\n\n{details}",
            "Remember Me",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void SetBusyState(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _loginButton.Enabled = !isBusy;
        _emailTextBox.Enabled = !isBusy;
        _passwordTextBox.Enabled = !isBusy;
        _rememberMeCheckBox.Enabled = !isBusy;
        _showPasswordButton.Enabled = !isBusy;
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
    }

    private void HideError()
    {
        _errorLabel.Visible = false;
        _errorLabel.Text = string.Empty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnwireDragging(_cardPanel);
        }

        base.Dispose(disposing);
    }

    private sealed class LogoControl : Control
    {
        private static readonly Font LogoFont = new("Segoe UI", 22f, FontStyle.Bold);

        public LogoControl()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var fill = new SolidBrush(JiraTheme.Primary);
            e.Graphics.FillEllipse(fill, ClientRectangle);

            var textBounds = ClientRectangle;
            TextRenderer.DrawText(e.Graphics, "J", LogoFont, textBounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    private sealed class ShadowPanel : Panel
    {
        public ShadowPanel()
        {
            DoubleBuffered = true;
            Padding = new Padding(14);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var shadowBounds = new Rectangle(12, 12, Width - 24, Height - 24);
            using var shadowPath = GraphicsHelper.CreateRoundedPath(shadowBounds, 16);

            using var shadowBrush = new SolidBrush(Color.FromArgb(30, 9, 30, 66));
            e.Graphics.FillPath(shadowBrush, shadowPath);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            var cardBounds = new Rectangle(0, 0, Width - 16, Height - 16);
            cardBounds.Offset(0, 0);

            e.Graphics.SetClip(new Rectangle(0, 0, Width, Height));
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GraphicsHelper.CreateRoundedPath(cardBounds, 12);
            using var brush = new SolidBrush(JiraTheme.BgSurface);
            using var border = new Pen(JiraTheme.Border);

            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(border, path);
        }
    }
}
