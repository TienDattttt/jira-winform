using JiraClone.Domain.Entities;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public sealed class ProjectSwitcherControl : UserControl
{
    private readonly AppSession _session;
    private readonly Label _captionLabel = JiraControlFactory.CreateLabel("Project", true);
    private readonly ComboBox _projectComboBox = new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
    };
    private readonly Panel _surface = new() { Dock = DockStyle.Top, Height = 42, BackColor = JiraTheme.BgSurface };

    private bool _isBinding;

    public ProjectSwitcherControl(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgSidebar;
        Height = 82;
        Dock = DockStyle.Top;
        Padding = new Padding(20, 14, 20, 10);

        _captionLabel.ForeColor = Color.FromArgb(220, 255, 255, 255);
        _captionLabel.Font = JiraTheme.FontCaption;
        _captionLabel.Location = new Point(0, 0);

        _projectComboBox.DisplayMember = nameof(ProjectOption.DisplayName);
        _projectComboBox.ValueMember = nameof(ProjectOption.ProjectId);
        _projectComboBox.SelectedIndexChanged += async (_, _) => await HandleSelectionChangedAsync();

        _surface.Controls.Add(_projectComboBox);
        _surface.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(54, 255, 255, 255));
            e.Graphics.DrawRectangle(pen, 0, 0, _surface.Width - 1, _surface.Height - 1);
        };

        Controls.Add(_surface);
        Controls.Add(_captionLabel);
        _surface.Location = new Point(0, 24);

        Load += async (_, _) => await RefreshProjectsAsync();
        _session.ProjectChanged += HandleSessionProjectChanged;
    }

    public event EventHandler<AppSession.ProjectChangedEventArgs>? ProjectChanged;

    public async Task RefreshProjectsAsync()
    {
        try
        {
            _isBinding = true;
            var projects = await _session.Projects.GetAccessibleProjectsAsync();
            var items = projects
                .OrderBy(project => project.Name)
                .Select(project => new ProjectOption(project.Id, $"{project.Key} - {project.Name}"))
                .ToList();

            _projectComboBox.DataSource = items;
            _projectComboBox.Enabled = items.Count > 0;

            if (_session.ActiveProject is not null)
            {
                _projectComboBox.SelectedValue = _session.ActiveProject.Id;
            }
            else if (items.Count > 0)
            {
                _projectComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _isBinding = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _session.ProjectChanged -= HandleSessionProjectChanged;
        }

        base.Dispose(disposing);
    }

    private async Task HandleSelectionChangedAsync()
    {
        if (_isBinding || _projectComboBox.SelectedValue is not int projectId)
        {
            return;
        }

        if (_session.ActiveProject?.Id == projectId)
        {
            return;
        }

        try
        {
            await _session.SetActiveProjectAsync(projectId);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private async void HandleSessionProjectChanged(object? sender, AppSession.ProjectChangedEventArgs eventArgs)
    {
        if (IsDisposed)
        {
            return;
        }

        await RefreshProjectsAsync();
        ProjectChanged?.Invoke(this, eventArgs);
    }

    private sealed record ProjectOption(int ProjectId, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
