using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class ReportsForm : UserControl
{
    private readonly AppSession _session;
    private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Reports");
    private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel("Follow sprint burn and delivery pace without leaving the desktop flow.", true);
    private readonly Label _sprintSelectorLabel = JiraControlFactory.CreateLabel("Sprint", true);
    private readonly ComboBox _sprintSelector = CreateSprintSelector();
    private readonly Button _refreshButton = JiraControlFactory.CreateSecondaryButton("Refresh");
    private readonly Button _exportButton = JiraControlFactory.CreatePrimaryButton("Export PNG");
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Font = JiraTheme.FontBody };
    private readonly TabPage _burndownTab = CreatePage("Burndown Chart");
    private readonly TabPage _velocityTab = CreatePage("Velocity Chart");
    private readonly Label _burndownTitleLabel = JiraControlFactory.CreateLabel("No sprint selected");
    private readonly Label _burndownMetaLabel = JiraControlFactory.CreateLabel("Select a sprint to plot remaining story points by day.", true);
    private readonly Label _velocityTitleLabel = JiraControlFactory.CreateLabel("Velocity history");
    private readonly Label _velocityMetaLabel = JiraControlFactory.CreateLabel("Close a sprint to start building delivery history.", true);
    private readonly BurndownChartPanel _burndownChart = new() { Dock = DockStyle.Fill };
    private readonly VelocityChartPanel _velocityChart = new() { Dock = DockStyle.Fill };
    private readonly Label _burndownEmptyState = CreateEmptyStateLabel("Select a sprint to render the burndown chart.");
    private readonly Label _velocityEmptyState = CreateEmptyStateLabel("Close a sprint to render the velocity chart.");
    private readonly DoubleBufferedPanel _burndownExportSurface = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(20) };
    private readonly DoubleBufferedPanel _velocityExportSurface = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(20) };

    private List<Sprint> _allSprints = [];
    private Project? _project;
    private BurndownReportDto? _burndownData;
    private VelocityReportDto? _velocityData;
    private string _shellSearch = string.Empty;
    private bool _isLoading;
    private bool _bindingSprintSelector;

    public ReportsForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;

        _titleLabel.Font = JiraTheme.FontH1;
        _subtitleLabel.Font = JiraTheme.FontCaption;
        _burndownTitleLabel.Font = JiraTheme.FontH2;
        _velocityTitleLabel.Font = JiraTheme.FontH2;

        ConfigureActionButton(_refreshButton, 108);
        ConfigureActionButton(_exportButton, 118);

        _refreshButton.Click += async (_, _) => await LoadReportsAsync();
        _exportButton.Click += (_, _) => ExportCurrentReport();
        _sprintSelector.SelectedIndexChanged += async (_, _) =>
        {
            if (_bindingSprintSelector || _isLoading)
            {
                return;
            }

            await LoadSelectedSprintBurndownAsync();
        };
        _tabs.SelectedIndexChanged += (_, _) => UpdateToolbarState();

        _burndownTab.Controls.Add(BuildBurndownTabContent());
        _velocityTab.Controls.Add(BuildVelocityTabContent());
        _tabs.TabPages.Add(_burndownTab);
        _tabs.TabPages.Add(_velocityTab);

        Controls.Add(_tabs);
        Controls.Add(BuildToolbar());
        Controls.Add(BuildHeader());

        Load += async (_, _) => await LoadReportsAsync();
        UpdateToolbarState();
    }

    public Task RefreshReportsAsync(CancellationToken cancellationToken = default) => LoadReportsAsync(cancellationToken);

    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        var previousSprintId = SelectedSprintId;
        PopulateSprintSelector(previousSprintId);
        UpdateToolbarState();

        if (SelectedSprintId == previousSprintId)
        {
            return;
        }

        if (SelectedSprintId.HasValue)
        {
            _ = LoadSelectedSprintBurndownAsync();
            return;
        }

        ApplyBurndownData(null);
    }

    private int? SelectedSprintId => _sprintSelector.SelectedValue is int sprintId
        ? sprintId
        : (_sprintSelector.SelectedItem as Sprint)?.Id;

    private Control BuildHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 104,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 18, 20, 8),
        };

        _titleLabel.Location = new Point(0, 0);
        _subtitleLabel.Location = new Point(0, 42);
        header.Controls.Add(_titleLabel);
        header.Controls.Add(_subtitleLabel);
        return header;
    }

    private Control BuildToolbar()
    {
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = JiraTheme.BgPage,
            Padding = new Padding(20, 0, 20, 12),
        };

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        _sprintSelectorLabel.Margin = new Padding(0, 10, 10, 0);
        _sprintSelector.Margin = new Padding(0, 0, 12, 0);
        left.Controls.Add(_sprintSelectorLabel);
        left.Controls.Add(_sprintSelector);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = JiraTheme.BgPage,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        right.Controls.Add(_exportButton);
        right.Controls.Add(_refreshButton);

        toolbar.Controls.Add(right);
        toolbar.Controls.Add(left);
        return toolbar;
    }

    private Control BuildBurndownTabContent()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            BackColor = JiraTheme.BgPage,
        };

        ApplySurfaceBorder(_burndownExportSurface);

        var meta = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = JiraTheme.BgSurface,
        };
        _burndownTitleLabel.Location = new Point(0, 0);
        _burndownMetaLabel.Location = new Point(0, 38);
        meta.Controls.Add(_burndownTitleLabel);
        meta.Controls.Add(_burndownMetaLabel);

        var chartHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 12, 0, 0),
        };
        chartHost.Controls.Add(_burndownEmptyState);
        chartHost.Controls.Add(_burndownChart);

        _burndownExportSurface.Controls.Add(chartHost);
        _burndownExportSurface.Controls.Add(meta);
        host.Controls.Add(_burndownExportSurface);
        return host;
    }

    private Control BuildVelocityTabContent()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            BackColor = JiraTheme.BgPage,
        };

        ApplySurfaceBorder(_velocityExportSurface);

        var meta = new Panel
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = JiraTheme.BgSurface,
        };
        _velocityTitleLabel.Location = new Point(0, 0);
        _velocityMetaLabel.Location = new Point(0, 38);
        meta.Controls.Add(_velocityTitleLabel);
        meta.Controls.Add(_velocityMetaLabel);

        var chartHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 12, 0, 0),
        };
        chartHost.Controls.Add(_velocityEmptyState);
        chartHost.Controls.Add(_velocityChart);

        _velocityExportSurface.Controls.Add(chartHost);
        _velocityExportSurface.Controls.Add(meta);
        host.Controls.Add(_velocityExportSurface);
        return host;
    }

    private async Task LoadReportsAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoading || !Visible)
        {
            return;
        }

        try
        {
            _isLoading = true;
            SetBusyState(true);
            var previousSprintId = SelectedSprintId;
            Project? project = null;
            List<Sprint> sprints = [];
            VelocityReportDto? velocityData = null;

            await _session.RunSerializedAsync(async () =>
            {
                project = await _session.Projects.GetActiveProjectAsync(cancellationToken);
                if (project is null)
                {
                    return;
                }

                sprints = (await _session.Sprints.GetByProjectAsync(project.Id, cancellationToken))
                    .OrderByDescending(GetSprintSortDate)
                    .ToList();
                velocityData = await _session.Sprints.GetVelocityDataAsync(project.Id, 6, cancellationToken);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _project = project;
            _allSprints = sprints;
            _velocityData = velocityData;
            _subtitleLabel.Text = project is null
                ? "Follow sprint burn and delivery pace without leaving the desktop flow."
                : $"Burndown and velocity snapshots for {project.Name}.";

            PopulateSprintSelector(previousSprintId);
            ApplyVelocityData(_velocityData);

            if (SelectedSprintId.HasValue)
            {
                _burndownData = await _session.Sprints.GetBurndownDataAsync(SelectedSprintId.Value, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ApplyBurndownData(_burndownData);
            }
            else
            {
                _burndownData = null;
                ApplyBurndownData(null);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _isLoading = false;
            SetBusyState(false);
            UpdateToolbarState();
        }
    }

    private async Task LoadSelectedSprintBurndownAsync()
    {
        var sprintId = SelectedSprintId;
        if (!sprintId.HasValue)
        {
            _burndownData = null;
            ApplyBurndownData(null);
            UpdateToolbarState();
            return;
        }

        try
        {
            _isLoading = true;
            SetBusyState(true);
            _burndownData = await _session.Sprints.GetBurndownDataAsync(sprintId.Value);
            ApplyBurndownData(_burndownData);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            _isLoading = false;
            SetBusyState(false);
            UpdateToolbarState();
        }
    }

    private void PopulateSprintSelector(int? preferredSprintId)
    {
        _bindingSprintSelector = true;
        try
        {
            var filtered = _allSprints
                .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                    || x.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                    || (x.Goal ?? string.Empty).Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(GetSprintSortDate)
                .ToList();

            _sprintSelector.DataSource = null;
            _sprintSelector.Items.Clear();

            if (filtered.Count == 0)
            {
                return;
            }

            _sprintSelector.DisplayMember = nameof(Sprint.Name);
            _sprintSelector.ValueMember = nameof(Sprint.Id);
            _sprintSelector.DataSource = filtered;

            var selectedSprint = filtered.FirstOrDefault(x => preferredSprintId.HasValue && x.Id == preferredSprintId.Value)
                ?? filtered.FirstOrDefault(x => x.State == SprintState.Active)
                ?? filtered.FirstOrDefault();

            if (selectedSprint is not null)
            {
                _sprintSelector.SelectedItem = selectedSprint;
            }
        }
        finally
        {
            _bindingSprintSelector = false;
        }
    }

    private void ApplyBurndownData(BurndownReportDto? data)
    {
        _burndownChart.Data = data;
        _burndownChart.Visible = data is not null;
        _burndownEmptyState.Visible = data is null;

        if (_project is null)
        {
            _burndownTitleLabel.Text = "No active project";
            _burndownMetaLabel.Text = "Choose an active project before opening reports.";
            _burndownEmptyState.Text = "Choose an active project to render the burndown chart.";
            return;
        }

        if (data is null)
        {
            _burndownTitleLabel.Text = _sprintSelector.Items.Count == 0 ? "No matching sprint" : "Burndown unavailable";
            _burndownMetaLabel.Text = _sprintSelector.Items.Count == 0
                ? "No sprints match the current search filter."
                : "Select a sprint to plot remaining story points by day.";
            _burndownEmptyState.Text = _sprintSelector.Items.Count == 0
                ? "Adjust the search or create a sprint to render this report."
                : "Select a sprint to render the burndown chart.";
            return;
        }

        var remainingStoryPoints = data.ActualPoints.LastOrDefault()?.RemainingStoryPoints ?? 0d;
        var sprintLength = data.IdealPoints.Count == 1 ? "1 day" : $"{data.IdealPoints.Count} days";

        _burndownTitleLabel.Text = $"{data.SprintName} | {FormatDateRange(data.StartDate, data.EndDate)}";
        _burndownMetaLabel.Text = $"{data.TotalStoryPoints} committed points across {sprintLength}. Remaining on the last day: {remainingStoryPoints:0.#}.";
    }

    private void ApplyVelocityData(VelocityReportDto? data)
    {
        _velocityChart.Data = data;
        _velocityChart.Visible = data is not null && data.Sprints.Count > 0;
        _velocityEmptyState.Visible = data is null || data.Sprints.Count == 0;

        if (_project is null)
        {
            _velocityTitleLabel.Text = "No active project";
            _velocityMetaLabel.Text = "Choose an active project before opening reports.";
            _velocityEmptyState.Text = "Choose an active project to render the velocity chart.";
            return;
        }

        if (data is null || data.Sprints.Count == 0)
        {
            _velocityTitleLabel.Text = $"{_project.Name} velocity";
            _velocityMetaLabel.Text = "Close a sprint to start building delivery history.";
            _velocityEmptyState.Text = "Close at least one sprint to render the velocity chart.";
            return;
        }

        _velocityTitleLabel.Text = $"{_project.Name} velocity";
        _velocityMetaLabel.Text = $"{data.Sprints.Count} closed sprints | average completed {data.AverageCompletedStoryPoints:0.#} story points.";
    }

    private void UpdateToolbarState()
    {
        var burndownSelected = _tabs.SelectedTab == _burndownTab;
        _sprintSelector.Enabled = burndownSelected && !_isLoading && _sprintSelector.Items.Count > 0;
        _sprintSelectorLabel.Enabled = burndownSelected;
        _refreshButton.Enabled = !_isLoading;
        _exportButton.Enabled = !_isLoading && GetExportControl().Width > 0 && GetExportControl().Height > 0;
    }

    private void SetBusyState(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _tabs.Enabled = !isBusy;
        _refreshButton.Enabled = !isBusy;
        _exportButton.Enabled = !isBusy;
        _sprintSelector.Enabled = !isBusy && _tabs.SelectedTab == _burndownTab && _sprintSelector.Items.Count > 0;
    }

    private void ExportCurrentReport()
    {
        try
        {
            var exportControl = GetExportControl();
            if (exportControl.Width <= 0 || exportControl.Height <= 0)
            {
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                FileName = BuildExportFileName(),
                RestoreDirectory = true,
                AddExtension = true,
                DefaultExt = "png"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            using var bitmap = new Bitmap(exportControl.Width, exportControl.Height);
            exportControl.DrawToBitmap(bitmap, new Rectangle(Point.Empty, exportControl.Size));
            bitmap.Save(dialog.FileName, ImageFormat.Png);
            MessageBox.Show(this, $"Saved {Path.GetFileName(dialog.FileName)}.", "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private Control GetExportControl() => _tabs.SelectedTab == _velocityTab
        ? _velocityExportSurface
        : _burndownExportSurface;

    private string BuildExportFileName()
    {
        var baseName = _tabs.SelectedTab == _velocityTab
            ? $"velocity-{_project?.Name ?? "project"}"
            : $"burndown-{(_burndownData?.SprintName ?? (_sprintSelector.SelectedItem as Sprint)?.Name ?? "sprint")}";
        return SanitizeFileName(baseName) + ".png";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return sanitized.Replace(' ', '-').ToLowerInvariant();
    }

    private static void ApplySurfaceBorder(Control control)
    {
        control.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
        };
    }

    private static string FormatDateRange(DateOnly startDate, DateOnly endDate) =>
        $"{startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}";

    private static DateTime GetSprintSortDate(Sprint sprint)
    {
        if (sprint.ClosedAtUtc.HasValue)
        {
            return sprint.ClosedAtUtc.Value;
        }

        if (sprint.EndDate.HasValue)
        {
            return sprint.EndDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        if (sprint.StartDate.HasValue)
        {
            return sprint.StartDate.Value.ToDateTime(TimeOnly.MinValue);
        }

        return DateTime.MinValue;
    }

    private static ComboBox CreateSprintSelector() => new()
    {
        Width = 260,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = JiraTheme.BgSurface,
        ForeColor = JiraTheme.TextPrimary,
        Font = JiraTheme.FontBody,
        IntegralHeight = false,
        Margin = new Padding(0),
    };

    private static Label CreateEmptyStateLabel(string text)
    {
        var label = JiraControlFactory.CreateLabel(text, true);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.Visible = false;
        return label;
    }

    private static TabPage CreatePage(string text) => new() { Text = text, BackColor = JiraTheme.BgPage };

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 40;
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private sealed class BurndownChartPanel : Panel
    {
        private BurndownReportDto? _data;

        public BurndownChartPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = JiraTheme.BgSurface;
        }

        public BurndownReportDto? Data
        {
            get => _data;
            set
            {
                _data = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_data is null || _data.ActualPoints.Count == 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var plot = new Rectangle(70, 48, Math.Max(10, Width - 110), Math.Max(10, Height - 112));
            if (plot.Width <= 10 || plot.Height <= 10)
            {
                return;
            }

            var maxValue = (float)Math.Max(1d, Math.Ceiling(Math.Max(
                _data.TotalStoryPoints,
                Math.Max(
                    _data.IdealPoints.Max(x => x.RemainingStoryPoints),
                    _data.ActualPoints.Max(x => x.RemainingStoryPoints)))));

            DrawLegend(e.Graphics, new[]
            {
                ("Ideal", JiraTheme.PrimaryActive),
                ("Actual", JiraTheme.Danger)
            });
            DrawAxes(e.Graphics, plot, maxValue, _data.ActualPoints.Count);
            DrawYAxisLabels(e.Graphics, plot, maxValue);
            DrawXAxisLabels(e.Graphics, plot, _data.ActualPoints);
            DrawBurndownLines(e.Graphics, plot, maxValue, _data.IdealPoints, _data.ActualPoints);
        }

        private static void DrawBurndownLines(
            Graphics graphics,
            Rectangle plot,
            float maxValue,
            IReadOnlyList<BurndownPointDto> idealPoints,
            IReadOnlyList<BurndownPointDto> actualPoints)
        {
            var idealPath = BuildPointSeries(plot, maxValue, idealPoints);
            var actualPath = BuildPointSeries(plot, maxValue, actualPoints);

            using var idealPen = new Pen(JiraTheme.PrimaryActive, 2f) { DashStyle = DashStyle.Dash };
            using var actualPen = new Pen(JiraTheme.Danger, 2.4f);
            if (idealPath.Length > 1)
            {
                graphics.DrawLines(idealPen, idealPath);
            }

            if (actualPath.Length > 1)
            {
                graphics.DrawLines(actualPen, actualPath);
            }

            using var markerBrush = new SolidBrush(JiraTheme.Danger);
            foreach (var point in actualPath)
            {
                graphics.FillEllipse(markerBrush, point.X - 4f, point.Y - 4f, 8f, 8f);
            }
        }

        private static PointF[] BuildPointSeries(Rectangle plot, float maxValue, IReadOnlyList<BurndownPointDto> points)
        {
            if (points.Count == 0)
            {
                return Array.Empty<PointF>();
            }

            var series = new PointF[points.Count];
            for (var index = 0; index < points.Count; index++)
            {
                var x = points.Count == 1
                    ? plot.Left + (plot.Width / 2f)
                    : plot.Left + (plot.Width * (index / (float)(points.Count - 1)));
                var y = plot.Bottom - (float)(points[index].RemainingStoryPoints / maxValue * plot.Height);
                series[index] = new PointF(x, y);
            }

            return series;
        }
    }

    private sealed class VelocityChartPanel : Panel
    {
        private VelocityReportDto? _data;

        public VelocityChartPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = JiraTheme.BgSurface;
        }

        public VelocityReportDto? Data
        {
            get => _data;
            set
            {
                _data = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_data is null || _data.Sprints.Count == 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var plot = new Rectangle(70, 48, Math.Max(10, Width - 110), Math.Max(10, Height - 128));
            if (plot.Width <= 10 || plot.Height <= 10)
            {
                return;
            }

            var maxValue = (float)Math.Max(1d, Math.Ceiling(Math.Max(
                _data.AverageCompletedStoryPoints,
                _data.Sprints.Max(x => Math.Max(x.CommittedStoryPoints, x.CompletedStoryPoints)))));

            DrawLegend(e.Graphics, new[]
            {
                ("Committed", JiraTheme.Blue100),
                ("Completed", JiraTheme.PrimaryActive),
                ("Average", JiraTheme.Success)
            });
            DrawAxes(e.Graphics, plot, maxValue, _data.Sprints.Count);
            DrawYAxisLabels(e.Graphics, plot, maxValue);
            DrawVelocityBars(e.Graphics, plot, maxValue, _data);
        }

        private static void DrawVelocityBars(Graphics graphics, Rectangle plot, float maxValue, VelocityReportDto data)
        {
            var groupWidth = plot.Width / (float)Math.Max(1, data.Sprints.Count);
            var barWidth = Math.Min(26f, Math.Max(12f, groupWidth * 0.22f));
            var gap = Math.Max(6f, groupWidth * 0.08f);
            using var committedBrush = new SolidBrush(JiraTheme.Blue100);
            using var committedBorder = new Pen(JiraTheme.Primary, 1f);
            using var completedBrush = new SolidBrush(JiraTheme.PrimaryActive);
            using var averagePen = new Pen(JiraTheme.Success, 2f) { DashStyle = DashStyle.Dash };

            var averageY = plot.Bottom - (float)(data.AverageCompletedStoryPoints / maxValue * plot.Height);
            graphics.DrawLine(averagePen, plot.Left, averageY, plot.Right, averageY);
            TextRenderer.DrawText(
                graphics,
                $"Avg {data.AverageCompletedStoryPoints:0.#}",
                JiraTheme.FontCaption,
                new Rectangle(plot.Right - 96, (int)averageY - 18, 92, 18),
                JiraTheme.Success,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

            for (var index = 0; index < data.Sprints.Count; index++)
            {
                var sprint = data.Sprints[index];
                var groupCenter = plot.Left + groupWidth * (index + 0.5f);
                var committedHeight = (float)(sprint.CommittedStoryPoints / maxValue * plot.Height);
                var completedHeight = (float)(sprint.CompletedStoryPoints / maxValue * plot.Height);
                var committedRect = RectangleF.FromLTRB(
                    groupCenter - gap - barWidth,
                    plot.Bottom - committedHeight,
                    groupCenter - gap,
                    plot.Bottom);
                var completedRect = RectangleF.FromLTRB(
                    groupCenter + gap,
                    plot.Bottom - completedHeight,
                    groupCenter + gap + barWidth,
                    plot.Bottom);

                graphics.FillRectangle(committedBrush, committedRect);
                graphics.DrawRectangle(committedBorder, committedRect.X, committedRect.Y, committedRect.Width, committedRect.Height);
                graphics.FillRectangle(completedBrush, completedRect);

                var labelBounds = new Rectangle(
                    (int)(groupCenter - (groupWidth / 2f)),
                    plot.Bottom + 8,
                    (int)groupWidth,
                    36);
                TextRenderer.DrawText(
                    graphics,
                    TrimLabel(sprint.SprintName, 12),
                    JiraTheme.FontCaption,
                    labelBounds,
                    JiraTheme.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }
    }

    private static void DrawLegend(Graphics graphics, IReadOnlyList<(string Label, Color Color)> items)
    {
        var left = 16;
        foreach (var item in items)
        {
            using var brush = new SolidBrush(item.Color);
            graphics.FillRectangle(brush, left, 16, 14, 14);
            TextRenderer.DrawText(
                graphics,
                item.Label,
                JiraTheme.FontCaption,
                new Rectangle(left + 20, 12, 90, 20),
                JiraTheme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            left += 100;
        }
    }

    private static void DrawAxes(Graphics graphics, Rectangle plot, float maxValue, int pointCount)
    {
        using var borderPen = new Pen(JiraTheme.Border);
        using var gridPen = new Pen(JiraTheme.Border) { DashStyle = DashStyle.Dot };
        graphics.DrawLine(borderPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        graphics.DrawLine(borderPen, plot.Left, plot.Top, plot.Left, plot.Bottom);

        const int horizontalSegments = 4;
        for (var segment = 0; segment <= horizontalSegments; segment++)
        {
            var y = plot.Bottom - (plot.Height * segment / (float)horizontalSegments);
            graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
        }

        if (pointCount <= 0)
        {
            return;
        }

        var verticalStep = pointCount <= 7 ? 1 : (int)Math.Ceiling(pointCount / 7d);
        for (var index = 0; index < pointCount; index += verticalStep)
        {
            var x = pointCount == 1
                ? plot.Left + (plot.Width / 2f)
                : plot.Left + (plot.Width * (index / (float)(pointCount - 1)));
            graphics.DrawLine(gridPen, x, plot.Top, x, plot.Bottom);
        }

        var maxLabel = ((int)Math.Ceiling(maxValue)).ToString();
        TextRenderer.DrawText(
            graphics,
            maxLabel,
            JiraTheme.FontCaption,
            new Rectangle(4, plot.Top - 10, 52, 20),
            JiraTheme.TextSecondary,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    private static void DrawYAxisLabels(Graphics graphics, Rectangle plot, float maxValue)
    {
        const int segments = 4;
        for (var segment = 0; segment <= segments; segment++)
        {
            var value = maxValue * (segments - segment) / segments;
            var y = plot.Top + (plot.Height * segment / (float)segments) - 10f;
            TextRenderer.DrawText(
                graphics,
                $"{value:0}",
                JiraTheme.FontCaption,
                new Rectangle(4, (int)y, 52, 20),
                JiraTheme.TextSecondary,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    private static void DrawXAxisLabels(Graphics graphics, Rectangle plot, IReadOnlyList<BurndownPointDto> points)
    {
        if (points.Count == 0)
        {
            return;
        }

        var step = points.Count <= 10 ? 1 : (int)Math.Ceiling(points.Count / 10d);
        for (var index = 0; index < points.Count; index += step)
        {
            DrawDayLabel(graphics, plot, points, index);
        }

        if ((points.Count - 1) % step != 0)
        {
            DrawDayLabel(graphics, plot, points, points.Count - 1);
        }
    }

    private static void DrawDayLabel(Graphics graphics, Rectangle plot, IReadOnlyList<BurndownPointDto> points, int index)
    {
        var x = points.Count == 1
            ? plot.Left + (plot.Width / 2f)
            : plot.Left + (plot.Width * (index / (float)(points.Count - 1)));
        TextRenderer.DrawText(
            graphics,
            $"D{points[index].DayNumber}",
            JiraTheme.FontCaption,
            new Rectangle((int)x - 18, plot.Bottom + 8, 36, 18),
            JiraTheme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static string TrimLabel(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..Math.Max(1, maxLength - 3)] + "...";
}

