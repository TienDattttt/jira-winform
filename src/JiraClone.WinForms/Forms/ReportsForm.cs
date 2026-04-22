using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Helpers;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public class ReportsForm : UserControl
{
    private const string DefaultSubtitle = "Theo dõi burndown, velocity, luồng tích lũy và tín hiệu kết thúc sprint ngay trên desktop.";
    private const int ChartSurfaceMinHeight = 300;
    private const int ChartSurfaceMaxHeight = 400;
    private const int ChartSurfaceReservedSpace = 72;
    private const int ChartMetaHeight = 66;

    private readonly AppSession _session;
    private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Báo cáo");
    private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel(DefaultSubtitle, true);
    private readonly Label _sprintSelectorLabel = JiraControlFactory.CreateLabel("Sprint", true);
    private readonly ComboBox _sprintSelector = CreateSprintSelector();
    private readonly Button _refreshButton = JiraControlFactory.CreateSecondaryButton("Làm mới");
    private readonly Button _exportButton = JiraControlFactory.CreatePrimaryButton("Xuất PNG");
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Font = JiraTheme.FontBody };
    private readonly TabPage _burndownTab = CreatePage("Biểu đồ burndown");
    private readonly TabPage _velocityTab = CreatePage("Biểu đồ velocity");
    private readonly TabPage _cfdTab = CreatePage("Luồng tích lũy");
    private readonly TabPage _sprintReportTab = CreatePage("Báo cáo sprint");

    private readonly Label _burndownTitleLabel = JiraControlFactory.CreateLabel("Chưa chọn sprint");
    private readonly Label _burndownMetaLabel = JiraControlFactory.CreateLabel("Chọn một sprint để vẽ số điểm story còn lại theo từng ngày.", true);
    private readonly Label _velocityTitleLabel = JiraControlFactory.CreateLabel("Lịch sử velocity");
    private readonly Label _velocityMetaLabel = JiraControlFactory.CreateLabel("Đóng một sprint để bắt đầu hình thành lịch sử giao hàng.", true);
    private readonly Label _cfdTitleLabel = JiraControlFactory.CreateLabel("Luồng tích lũy");
    private readonly Label _cfdMetaLabel = JiraControlFactory.CreateLabel("Theo dõi cách công việc đang làm dịch chuyển qua các trạng thái theo thời gian.", true);
    private readonly Label _sprintReportTitleLabel = JiraControlFactory.CreateLabel("Báo cáo sprint");
    private readonly Label _sprintReportMetaLabel = JiraControlFactory.CreateLabel("Chọn một sprint đã đóng để xem phạm vi hoàn thành, mang sang và bị loại bỏ.", true);
    private readonly Label _sprintReportSelectorLabel = JiraControlFactory.CreateLabel("Sprint đã đóng", true);
    private readonly ComboBox _sprintReportSelector = CreateSprintSelector();

    private readonly BurndownChartPanel _burndownChart = new() { Dock = DockStyle.Fill };
    private readonly VelocityChartPanel _velocityChart = new() { Dock = DockStyle.Fill };
    private readonly CfdChartPanel _cfdChart = new() { Dock = DockStyle.Fill };

    private readonly Label _burndownEmptyState = CreateEmptyStateLabel("Chọn một sprint để hiển thị biểu đồ burndown.");
    private readonly Label _velocityEmptyState = CreateEmptyStateLabel("Đóng ít nhất một sprint để hiển thị biểu đồ velocity.");
    private readonly Label _cfdEmptyState = CreateEmptyStateLabel("Hoàn thành sprint hoặc thay đổi trạng thái issue để bắt đầu vẽ luồng tích lũy.");
    private readonly Label _sprintReportEmptyState = CreateEmptyStateLabel("Đóng một sprint để mở khóa báo cáo sprint của nó.");

    private readonly DoubleBufferedPanel _burndownExportSurface = CreateChartExportSurface();
    private readonly DoubleBufferedPanel _velocityExportSurface = CreateChartExportSurface();
    private readonly DoubleBufferedPanel _cfdExportSurface = CreateChartExportSurface();
    private readonly DoubleBufferedPanel _sprintReportExportSurface = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface, Padding = new Padding(20) };

    private readonly ReportMetricCard _committedMetricCard = new("SP cam kết", JiraTheme.PrimaryActive);
    private readonly ReportMetricCard _completedMetricCard = new("SP hoàn thành", JiraTheme.Success);
    private readonly ReportMetricCard _completionMetricCard = new("Hoàn thành", JiraTheme.Warning);
    private readonly ReportMetricCard _removedMetricCard = new("Loại bỏ", JiraTheme.Danger);
    private readonly TableLayoutPanel _sprintReportBody = BuildSprintReportBody();
    private readonly SprintIssueBucket _completedBucket = new("Công việc hoàn thành", JiraTheme.Success);
    private readonly SprintIssueBucket _notCompletedBucket = new("Chưa hoàn thành", JiraTheme.Danger);
    private readonly SprintIssueBucket _removedBucket = new("Bị loại khỏi sprint", JiraTheme.Warning);

    private List<Sprint> _allSprints = [];
    private List<Sprint> _closedSprints = [];
    private Project? _project;
    private BurndownReportDto? _burndownData;
    private VelocityReportDto? _velocityData;
    private IReadOnlyList<CfdDataPointDto> _cfdData = [];
    private SprintReportDto? _sprintReportData;
    private DateOnly _cfdFromDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-13));
    private DateOnly _cfdToDate = DateOnly.FromDateTime(DateTime.Today);
    private string _shellSearch = string.Empty;
    private bool _isLoading;
    private bool _bindingSprintSelector;
    private bool _bindingSprintReportSelector;
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _loadCts;

    public ReportsForm(AppSession session)
    {
        _session = session;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        _titleLabel.Font = JiraTheme.FontH1;
        _subtitleLabel.Font = JiraTheme.FontCaption;
        _burndownTitleLabel.Font = JiraTheme.FontH2;
        _velocityTitleLabel.Font = JiraTheme.FontH2;
        _cfdTitleLabel.Font = JiraTheme.FontH2;
        _sprintReportTitleLabel.Font = JiraTheme.FontH2;

        _sprintReportSelector.Width = 320;

        ConfigureActionButton(_refreshButton, 108);
        ConfigureActionButton(_exportButton, 118);

        _refreshButton.Click += OnRefreshButtonClick;
        _exportButton.Click += OnExportButtonClick;
        _sprintSelector.SelectedIndexChanged += OnSprintSelectorSelectedIndexChanged;
        _sprintReportSelector.SelectedIndexChanged += OnSprintReportSelectorSelectedIndexChanged;
        _tabs.SelectedIndexChanged += OnTabsSelectedIndexChanged;

        _burndownTab.Controls.Add(BuildBurndownTabContent());
        _velocityTab.Controls.Add(BuildVelocityTabContent());
        _cfdTab.Controls.Add(BuildCfdTabContent());
        _sprintReportTab.Controls.Add(BuildSprintReportTabContent());
        _tabs.TabPages.Add(_burndownTab);
        _tabs.TabPages.Add(_velocityTab);
        _tabs.TabPages.Add(_cfdTab);
        _tabs.TabPages.Add(_sprintReportTab);

        Controls.Add(BuildLayout());
        Resize += OnReportsResize;

        _sprintReportBody.Controls.Add(_completedBucket, 0, 0);
        _sprintReportBody.Controls.Add(_notCompletedBucket, 1, 0);
        _sprintReportBody.Controls.Add(_removedBucket, 2, 0);

        Load += OnReportsLoad;
        ApplySprintReportData(null);
        UpdateChartSurfaceHeights();
        UpdateToolbarState();
    }

    public Task RefreshReportsAsync(CancellationToken cancellationToken = default) => ReloadReportsAsync(cancellationToken);

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = JiraTheme.BgPage,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildContentArea(), 0, 1);
        return root;
    }

    private Control BuildContentArea()
    {
        var content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgPage,
        };
        content.Controls.Add(_tabs);
        content.Controls.Add(BuildToolbar());
        return content;
    }
    private Task ReloadReportsAsync(CancellationToken cancellationToken = default) => LoadReportsAsync(RestartLoadCancellation(cancellationToken));

    private Task ReloadSelectedSprintBurndownAsync(CancellationToken cancellationToken = default) =>
        LoadSelectedSprintBurndownAsync(RestartLoadCancellation(cancellationToken));

    private Task ReloadSelectedSprintReportAsync(CancellationToken cancellationToken = default) =>
        LoadSelectedSprintReportAsync(RestartLoadCancellation(cancellationToken));

    private CancellationToken RestartLoadCancellation(CancellationToken cancellationToken = default)
    {
        CancelPendingLoad();
        _loadCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        return _loadCts.Token;
    }

    private void CancelPendingLoad()
    {
        if (_loadCts is null)
        {
            return;
        }

        try
        {
            _loadCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        _loadCts.Dispose();
        _loadCts = null;
    }


    public void SetShellSearch(string value)
    {
        _shellSearch = value?.Trim() ?? string.Empty;
        var previousSprintId = SelectedSprintId;
        var previousSprintReportId = SelectedSprintReportId;

        PopulateSprintSelector(previousSprintId);
        PopulateSprintReportSelector(previousSprintReportId);
        UpdateToolbarState();

        if (SelectedSprintId != previousSprintId)
        {
            if (SelectedSprintId.HasValue)
            {
                _ = ReloadSelectedSprintBurndownAsync(_disposeCts.Token);
            }
            else
            {
                ApplyBurndownData(null);
            }
        }

        if (SelectedSprintReportId != previousSprintReportId)
        {
            if (SelectedSprintReportId.HasValue)
            {
                _ = ReloadSelectedSprintReportAsync(_disposeCts.Token);
            }
            else
            {
                ApplySprintReportData(null);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingLoad();
            _disposeCts.Cancel();
            Resize -= OnReportsResize;
            Load -= OnReportsLoad;
            _refreshButton.Click -= OnRefreshButtonClick;
            _exportButton.Click -= OnExportButtonClick;
            _sprintSelector.SelectedIndexChanged -= OnSprintSelectorSelectedIndexChanged;
            _sprintReportSelector.SelectedIndexChanged -= OnSprintReportSelectorSelectedIndexChanged;
            _tabs.SelectedIndexChanged -= OnTabsSelectedIndexChanged;
        }

        base.Dispose(disposing);
    }

    private int? SelectedSprintId => _sprintSelector.SelectedValue is int sprintId
        ? sprintId
        : (_sprintSelector.SelectedItem as Sprint)?.Id;

    private int? SelectedSprintReportId => _sprintReportSelector.SelectedValue is int sprintId
        ? sprintId
        : (_sprintReportSelector.SelectedItem as Sprint)?.Id;

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

        var meta = CreateMetaPanel(_burndownTitleLabel, _burndownMetaLabel);
        var chartHost = CreateChartHost(_burndownChart, _burndownEmptyState);

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

        var meta = CreateMetaPanel(_velocityTitleLabel, _velocityMetaLabel);
        var chartHost = CreateChartHost(_velocityChart, _velocityEmptyState);

        _velocityExportSurface.Controls.Add(chartHost);
        _velocityExportSurface.Controls.Add(meta);
        host.Controls.Add(_velocityExportSurface);
        return host;
    }

    private Control BuildCfdTabContent()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            BackColor = JiraTheme.BgPage,
        };

        ApplySurfaceBorder(_cfdExportSurface);

        var meta = CreateMetaPanel(_cfdTitleLabel, _cfdMetaLabel);
        var chartHost = CreateChartHost(_cfdChart, _cfdEmptyState);

        _cfdExportSurface.Controls.Add(chartHost);
        _cfdExportSurface.Controls.Add(meta);
        host.Controls.Add(_cfdExportSurface);
        return host;
    }

    private Control BuildSprintReportTabContent()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 0, 20, 20),
            BackColor = JiraTheme.BgPage,
        };

        ApplySurfaceBorder(_sprintReportExportSurface);

        var meta = new Panel
        {
            Dock = DockStyle.Top,
            Height = 112,
            BackColor = JiraTheme.BgSurface,
        };
        _sprintReportTitleLabel.Location = new Point(0, 0);
        _sprintReportMetaLabel.Location = new Point(0, 38);
        _sprintReportSelectorLabel.Location = new Point(0, 76);
        _sprintReportSelector.Location = new Point(96, 68);
        meta.Controls.Add(_sprintReportTitleLabel);
        meta.Controls.Add(_sprintReportMetaLabel);
        meta.Controls.Add(_sprintReportSelectorLabel);
        meta.Controls.Add(_sprintReportSelector);

        var statsStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = JiraTheme.BgSurface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 8),
            Margin = new Padding(0),
        };
        statsStrip.Controls.Add(_committedMetricCard);
        statsStrip.Controls.Add(_completedMetricCard);
        statsStrip.Controls.Add(_completionMetricCard);
        statsStrip.Controls.Add(_removedMetricCard);

        var bodyHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 12, 0, 0),
        };
        bodyHost.Controls.Add(_sprintReportEmptyState);
        bodyHost.Controls.Add(_sprintReportBody);

        _sprintReportExportSurface.Controls.Add(bodyHost);
        _sprintReportExportSurface.Controls.Add(statsStrip);
        _sprintReportExportSurface.Controls.Add(meta);
        host.Controls.Add(_sprintReportExportSurface);
        return host;
    }
    private async Task LoadReportsAsync(CancellationToken cancellationToken = default)
    {
        if (!Visible)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _isLoading = true;
            SetBusyState(true);

            var previousSprintId = SelectedSprintId;
            var previousSprintReportId = SelectedSprintReportId;
            Project? project = null;
            List<Sprint> sprints = [];
            VelocityReportDto? velocityData = null;
            IReadOnlyList<CfdDataPointDto> cfdData = [];
            var cfdRange = ResolveCfdRange(sprints);

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
                cfdRange = ResolveCfdRange(sprints);
                cfdData = await _session.Sprints.GetCfdDataAsync(project.Id, cfdRange.FromUtc, cfdRange.ToUtc, cancellationToken);
            }, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            _project = project;
            _allSprints = sprints;
            _closedSprints = sprints.Where(x => x.State == SprintState.Closed).OrderByDescending(GetSprintSortDate).ToList();
            _velocityData = velocityData;
            _cfdData = cfdData;
            _cfdFromDate = DateOnly.FromDateTime(cfdRange.FromUtc.Date);
            _cfdToDate = DateOnly.FromDateTime(cfdRange.ToUtc.Date);
            _subtitleLabel.Text = project is null
                ? DefaultSubtitle
                : $"Burndown, velocity, luồng tích lũy và tín hiệu kết thúc sprint của {project.Name}.";

            PopulateSprintSelector(previousSprintId);
            PopulateSprintReportSelector(previousSprintReportId);

            ApplyVelocityData(_velocityData);
            ApplyCfdData(_cfdData);

            if (SelectedSprintId.HasValue)
            {
                _burndownData = await _session.Sprints.GetBurndownDataAsync(SelectedSprintId.Value, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                _burndownData = null;
            }

            if (SelectedSprintReportId.HasValue)
            {
                _sprintReportData = await _session.Sprints.GetSprintReportAsync(SelectedSprintReportId.Value, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                _sprintReportData = null;
            }

            ApplyBurndownData(_burndownData);
            ApplySprintReportData(_sprintReportData);
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

    private async Task LoadSelectedSprintBurndownAsync(CancellationToken cancellationToken = default)
    {
        var sprintId = SelectedSprintId;
        if (!sprintId.HasValue)
        {
            _burndownData = null;
            ApplyBurndownData(null);
            UpdateToolbarState();
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _isLoading = true;
            SetBusyState(true);
            _burndownData = await _session.Sprints.GetBurndownDataAsync(sprintId.Value, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyBurndownData(_burndownData);
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

    private async Task LoadSelectedSprintReportAsync(CancellationToken cancellationToken = default)
    {
        var sprintId = SelectedSprintReportId;
        if (!sprintId.HasValue)
        {
            _sprintReportData = null;
            ApplySprintReportData(null);
            UpdateToolbarState();
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _isLoading = true;
            SetBusyState(true);
            _sprintReportData = await _session.Sprints.GetSprintReportAsync(sprintId.Value, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplySprintReportData(_sprintReportData);
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

    private void PopulateSprintReportSelector(int? preferredSprintId)
    {
        _bindingSprintReportSelector = true;
        try
        {
            var filtered = _closedSprints
                .Where(x => string.IsNullOrWhiteSpace(_shellSearch)
                    || x.Name.Contains(_shellSearch, StringComparison.OrdinalIgnoreCase)
                    || (x.Goal ?? string.Empty).Contains(_shellSearch, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(GetSprintSortDate)
                .ToList();

            _sprintReportSelector.DataSource = null;
            _sprintReportSelector.Items.Clear();

            if (filtered.Count == 0)
            {
                return;
            }

            _sprintReportSelector.DisplayMember = nameof(Sprint.Name);
            _sprintReportSelector.ValueMember = nameof(Sprint.Id);
            _sprintReportSelector.DataSource = filtered;

            var selectedSprint = filtered.FirstOrDefault(x => preferredSprintId.HasValue && x.Id == preferredSprintId.Value)
                ?? filtered.FirstOrDefault();

            if (selectedSprint is not null)
            {
                _sprintReportSelector.SelectedItem = selectedSprint;
            }
        }
        finally
        {
            _bindingSprintReportSelector = false;
        }
    }

    private void ApplyBurndownData(BurndownReportDto? data)
    {
        _burndownChart.Data = data;
        _burndownChart.Visible = data is not null;
        _burndownEmptyState.Visible = data is null;

        if (_project is null)
        {
            _burndownTitleLabel.Text = "Không có dự án đang hoạt động";
            _burndownMetaLabel.Text = "Hãy chọn một dự án đang hoạt động trước khi mở báo cáo.";
            _burndownEmptyState.Text = "Hãy chọn một dự án đang hoạt động để hiển thị biểu đồ burndown.";
            return;
        }

        if (data is null)
        {
            _burndownTitleLabel.Text = _sprintSelector.Items.Count == 0 ? "Không có sprint phù hợp" : "Chưa có dữ liệu burndown";
            _burndownMetaLabel.Text = _sprintSelector.Items.Count == 0
                ? "Không có sprint nào khớp bộ lọc tìm kiếm hiện tại."
                : "Chọn một sprint để vẽ số điểm story còn lại theo từng ngày.";
            _burndownEmptyState.Text = _sprintSelector.Items.Count == 0
                ? "Hãy điều chỉnh tìm kiếm hoặc tạo sprint để hiển thị báo cáo này."
                : "Chọn một sprint để hiển thị biểu đồ burndown.";
            return;
        }

        var remainingStoryPoints = data.ActualPoints.LastOrDefault()?.RemainingStoryPoints ?? 0d;
        var sprintLength = data.IdealPoints.Count == 1 ? "1 ngày" : $"{data.IdealPoints.Count} ngày";

        _burndownTitleLabel.Text = $"{data.SprintName} | {FormatDateRange(data.StartDate, data.EndDate)}";
        _burndownMetaLabel.Text = $"{data.TotalStoryPoints} điểm cam kết trong {sprintLength}. Còn lại ở ngày cuối: {remainingStoryPoints:0.#}.";
    }

    private void ApplyVelocityData(VelocityReportDto? data)
    {
        _velocityChart.Data = data;
        _velocityChart.Visible = data is not null && data.Sprints.Count > 0;
        _velocityEmptyState.Visible = data is null || data.Sprints.Count == 0;

        if (_project is null)
        {
            _velocityTitleLabel.Text = "Không có dự án đang hoạt động";
            _velocityMetaLabel.Text = "Hãy chọn một dự án đang hoạt động trước khi mở báo cáo.";
            _velocityEmptyState.Text = "Hãy chọn một dự án đang hoạt động để hiển thị biểu đồ velocity.";
            return;
        }

        if (data is null || data.Sprints.Count == 0)
        {
            _velocityTitleLabel.Text = $"Velocity của {_project.Name}";
            _velocityMetaLabel.Text = "Đóng một sprint để bắt đầu hình thành lịch sử giao hàng.";
            _velocityEmptyState.Text = "Đóng ít nhất một sprint để hiển thị biểu đồ velocity.";
            return;
        }

        _velocityTitleLabel.Text = $"Velocity của {_project.Name}";
        _velocityMetaLabel.Text = $"{data.Sprints.Count} sprint đã đóng | trung bình hoàn thành {data.AverageCompletedStoryPoints:0.#} điểm story.";
    }
    private void ApplyCfdData(IReadOnlyList<CfdDataPointDto>? data)
    {
        var normalized = data?
            .OrderBy(x => x.Date)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Status)
            .ToList() ?? [];

        _cfdChart.Data = normalized;
        _cfdChart.Visible = normalized.Count > 0;
        _cfdEmptyState.Visible = normalized.Count == 0;

        if (_project is null)
        {
            _cfdTitleLabel.Text = "Không có dự án đang hoạt động";
            _cfdMetaLabel.Text = "Hãy chọn một dự án đang hoạt động trước khi mở báo cáo.";
            _cfdEmptyState.Text = "Hãy chọn một dự án đang hoạt động để hiển thị biểu đồ luồng tích lũy.";
            return;
        }

        if (normalized.Count == 0)
        {
            _cfdTitleLabel.Text = $"Luồng tích lũy của {_project.Name}";
            _cfdMetaLabel.Text = $"Không dựng lại được hoạt động trạng thái nào cho giai đoạn {FormatDateRange(_cfdFromDate, _cfdToDate)}.";
            _cfdEmptyState.Text = "Hãy chuyển công việc qua các trạng thái workflow để bắt đầu vẽ biểu đồ luồng tích lũy.";
            return;
        }

        var dates = normalized.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
        var latestDate = dates.LastOrDefault();
        var latestCount = normalized.Where(x => x.Date == latestDate).Sum(x => x.IssueCount);
        var statusCount = normalized.Select(x => x.StatusId).Distinct().Count();

        _cfdTitleLabel.Text = $"Luồng tích lũy của {_project.Name} | {FormatDateRange(_cfdFromDate, _cfdToDate)}";
        _cfdMetaLabel.Text = $"Theo dõi {statusCount} trạng thái trong {dates.Count} ngày. Phạm vi hiển thị mới nhất: {latestCount} issue.";
    }

    private void ApplySprintReportData(SprintReportDto? data)
    {
        _sprintReportData = data;
        _sprintReportBody.Visible = data is not null;
        _sprintReportEmptyState.Visible = data is null;

        if (_project is null)
        {
            _sprintReportTitleLabel.Text = "Không có dự án đang hoạt động";
            _sprintReportMetaLabel.Text = "Hãy chọn một dự án đang hoạt động trước khi mở báo cáo.";
            _sprintReportEmptyState.Text = "Hãy chọn một dự án đang hoạt động để xem báo cáo sprint.";
            ResetSprintReportVisuals();
            return;
        }

        if (_sprintReportSelector.Items.Count == 0)
        {
            _sprintReportTitleLabel.Text = $"Báo cáo sprint của {_project.Name}";
            _sprintReportMetaLabel.Text = "Đóng một sprint để mở khóa phần phân tích phạm vi hoàn thành, mang sang và bị loại bỏ.";
            _sprintReportEmptyState.Text = "Không có sprint đã đóng nào khớp bộ lọc tìm kiếm hiện tại.";
            ResetSprintReportVisuals();
            return;
        }

        if (data is null)
        {
            _sprintReportTitleLabel.Text = "Chưa có báo cáo sprint";
            _sprintReportMetaLabel.Text = "Chọn một sprint đã đóng để xem phạm vi hoàn thành, mang sang và bị loại bỏ.";
            _sprintReportEmptyState.Text = "Chọn một sprint đã đóng để hiển thị báo cáo sprint.";
            ResetSprintReportVisuals();
            return;
        }

        var closedLabel = data.ClosedAtUtc.HasValue
            ? $"Đóng lúc {UtcDateTimeHelper.FormatLocal(data.ClosedAtUtc.Value, "dd MMM yyyy HH:mm")}"
            : "Sprint đã đóng";
        _sprintReportTitleLabel.Text = $"{data.SprintName} | {FormatDateRange(data.StartDate, data.EndDate)}";
        _sprintReportMetaLabel.Text = $"{closedLabel} | {data.CompletedWork.Count} hoàn thành, {data.NotCompleted.Count} mang sang, {data.RemovedFromSprint.Count} bị loại bỏ.";

        _committedMetricCard.SetValue($"{data.CommittedStoryPoints}", "điểm story cam kết");
        _completedMetricCard.SetValue($"{data.CompletedStoryPoints}", "điểm story hoàn thành");
        _completionMetricCard.SetValue($"{data.CompletionPercentage:0.#}%", "trên phạm vi đã cam kết");
        _removedMetricCard.SetValue($"{data.RemovedFromSprint.Count}", "issue bị loại bỏ");

        _completedBucket.SetIssues(
            "Completed work",
            JiraTheme.Success,
            data.CompletedWork,
            "No issues reached Done before the sprint closed.");
        _notCompletedBucket.SetIssues(
            "Not completed",
            JiraTheme.Danger,
            data.NotCompleted,
            "No committed issues were carried forward.");
        _removedBucket.SetIssues(
            "Removed from sprint",
            JiraTheme.Warning,
            data.RemovedFromSprint,
            "No scope was removed during the sprint.");
    }

    private void ResetSprintReportVisuals()
    {
        _committedMetricCard.SetValue("0", "điểm story cam kết");
        _completedMetricCard.SetValue("0", "điểm story hoàn thành");
        _completionMetricCard.SetValue("0%", "of committed scope");
        _removedMetricCard.SetValue("0", "issues removed");
        _completedBucket.SetIssues("Completed work", JiraTheme.Success, Array.Empty<SprintReportIssueDto>(), "No issues available.");
        _notCompletedBucket.SetIssues("Not completed", JiraTheme.Danger, Array.Empty<SprintReportIssueDto>(), "No issues available.");
        _removedBucket.SetIssues("Removed from sprint", JiraTheme.Warning, Array.Empty<SprintReportIssueDto>(), "No issues available.");
    }

    private void UpdateToolbarState()
    {
        var burndownSelected = _tabs.SelectedTab == _burndownTab;
        _sprintSelector.Visible = burndownSelected;
        _sprintSelectorLabel.Visible = burndownSelected;
        _sprintSelector.Enabled = burndownSelected && !_isLoading && _sprintSelector.Items.Count > 0;
        _sprintSelectorLabel.Enabled = burndownSelected;
        _sprintReportSelector.Enabled = !_isLoading && _sprintReportSelector.Items.Count > 0;
        _refreshButton.Enabled = !_isLoading;

        var exportControl = GetExportControl();
        _exportButton.Enabled = !_isLoading && exportControl.Width > 0 && exportControl.Height > 0;
    }

    private void SetBusyState(bool isBusy)
    {
        UseWaitCursor = isBusy;
        _tabs.Enabled = !isBusy;
        _refreshButton.Enabled = !isBusy;
        _exportButton.Enabled = !isBusy;
        _sprintSelector.Enabled = !isBusy && _tabs.SelectedTab == _burndownTab && _sprintSelector.Items.Count > 0;
        _sprintReportSelector.Enabled = !isBusy && _sprintReportSelector.Items.Count > 0;
    }

    private async void OnReportsLoad(object? sender, EventArgs e)
    {
        BeginInvoke(UpdateChartSurfaceHeights);
        await ReloadReportsAsync(_disposeCts.Token);
    }

    private async void OnRefreshButtonClick(object? sender, EventArgs e)
    {
        await ReloadReportsAsync(_disposeCts.Token);
    }

    private void OnExportButtonClick(object? sender, EventArgs e)
    {
        ExportCurrentReport();
    }

    private async void OnSprintSelectorSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_bindingSprintSelector || _isLoading)
        {
            return;
        }

        await ReloadSelectedSprintBurndownAsync(_disposeCts.Token);
    }

    private async void OnSprintReportSelectorSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_bindingSprintReportSelector || _isLoading)
        {
            return;
        }

        await ReloadSelectedSprintReportAsync(_disposeCts.Token);
    }

    private void OnTabsSelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateToolbarState();
        BeginInvoke(UpdateChartSurfaceHeights);
    }

    private void OnReportsResize(object? sender, EventArgs e)
    {
        UpdateChartSurfaceHeights();
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
                Filter = "Ảnh PNG|*.png",
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

    private Control GetExportControl()
    {
        if (_tabs.SelectedTab == _velocityTab)
        {
            return _velocityExportSurface;
        }

        if (_tabs.SelectedTab == _cfdTab)
        {
            return _cfdExportSurface;
        }

        if (_tabs.SelectedTab == _sprintReportTab)
        {
            return _sprintReportExportSurface;
        }

        return _burndownExportSurface;
    }

    private string BuildExportFileName()
    {
        string baseName;
        if (_tabs.SelectedTab == _velocityTab)
        {
            baseName = $"velocity-{_project?.Name ?? "project"}";
        }
        else if (_tabs.SelectedTab == _cfdTab)
        {
            baseName = $"cfd-{_project?.Name ?? "project"}-{_cfdFromDate:yyyyMMdd}-{_cfdToDate:yyyyMMdd}";
        }
        else if (_tabs.SelectedTab == _sprintReportTab)
        {
            baseName = $"sprint-report-{(_sprintReportData?.SprintName ?? (_sprintReportSelector.SelectedItem as Sprint)?.Name ?? "sprint")}";
        }
        else
        {
            baseName = $"burndown-{(_burndownData?.SprintName ?? (_sprintSelector.SelectedItem as Sprint)?.Name ?? "sprint")}";
        }

        return SanitizeFileName(baseName) + ".png";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return sanitized.Replace(' ', '-').ToLowerInvariant();
    }

    private static Control CreateMetaPanel(Control titleLabel, Control metaLabel)
    {
        var meta = new Panel
        {
            Dock = DockStyle.Top,
            Height = ChartMetaHeight,
            BackColor = JiraTheme.BgSurface,
        };
        titleLabel.Location = new Point(0, 0);
        metaLabel.Location = new Point(0, 34);
        meta.Controls.Add(titleLabel);
        meta.Controls.Add(metaLabel);
        return meta;
    }

    private static Control CreateChartHost(Control chart, Control emptyState)
    {
        var chartHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 8, 0, 0),
        };
        chartHost.Controls.Add(emptyState);
        chartHost.Controls.Add(chart);
        return chartHost;
    }

    private static DoubleBufferedPanel CreateChartExportSurface() => new()
    {
        Dock = DockStyle.Top,
        Height = 380,
        BackColor = JiraTheme.BgSurface,
        Padding = new Padding(20)
    };

    private void UpdateChartSurfaceHeights()
    {
        if (_tabs.DisplayRectangle.Height <= 0)
        {
            return;
        }

        UpdateChartSurfaceHeight(_burndownExportSurface);
        UpdateChartSurfaceHeight(_velocityExportSurface);
        UpdateChartSurfaceHeight(_cfdExportSurface);
    }

    private void UpdateChartSurfaceHeight(Control surface)
    {
        if (surface.Parent is null)
        {
            return;
        }

        var availableHeight = _tabs.DisplayRectangle.Height - surface.Parent.Padding.Vertical;
        if (availableHeight <= 0)
        {
            return;
        }

        var targetHeight = Math.Min(
            availableHeight,
            Math.Clamp(availableHeight - ChartSurfaceReservedSpace, ChartSurfaceMinHeight, ChartSurfaceMaxHeight));

        if (surface.Height != targetHeight)
        {
            surface.Height = targetHeight;
        }
    }

    private static TableLayoutPanel BuildSprintReportBody()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        return layout;
    }

    private static void ApplySurfaceBorder(DoubleBufferedPanel control) => control.DrawBorder = true;

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

    private static (DateTime FromUtc, DateTime ToUtc) ResolveCfdRange(IReadOnlyList<Sprint> sprints)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var activeSprint = sprints
            .Where(x => x.State == SprintState.Active)
            .OrderByDescending(GetSprintSortDate)
            .FirstOrDefault();
        if (activeSprint is not null)
        {
            var from = activeSprint.StartDate ?? today.AddDays(-13);
            var to = activeSprint.EndDate ?? today;
            if (to < from)
            {
                to = from;
            }

            return (from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue));
        }

        var recentSprint = sprints
            .OrderByDescending(GetSprintSortDate)
            .FirstOrDefault(x => x.StartDate.HasValue || x.EndDate.HasValue);
        if (recentSprint is not null)
        {
            var from = recentSprint.StartDate ?? recentSprint.EndDate ?? today.AddDays(-13);
            var to = recentSprint.EndDate ?? recentSprint.StartDate ?? today;
            if (to < from)
            {
                to = from;
            }

            return (from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue));
        }

        return (today.AddDays(-13).ToDateTime(TimeOnly.MinValue), today.ToDateTime(TimeOnly.MinValue));
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public bool DrawBorder { get; set; }

        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!DrawBorder)
            {
                return;
            }

            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    private sealed class ReportMetricCard : Panel
    {
        private readonly Label _label;
        private readonly Label _valueLabel;
        private readonly Label _captionLabel;
        private readonly Color _accent;

        public ReportMetricCard(string label, Color accent)
        {
            _accent = accent;
            Width = 190;
            Height = 72;
            Margin = new Padding(0, 0, 12, 0);
            Padding = new Padding(12, 10, 12, 10);
            BackColor = JiraTheme.BgSurface;
            DoubleBuffered = true;

            _label = JiraControlFactory.CreateLabel(label, true);
            _valueLabel = JiraControlFactory.CreateLabel("0");
            _captionLabel = JiraControlFactory.CreateLabel(string.Empty, true);
            _valueLabel.Font = JiraTheme.FontH2;

            _label.Location = new Point(12, 10);
            _valueLabel.Location = new Point(12, 28);
            _captionLabel.Location = new Point(12, 52);

            Controls.Add(_label);
            Controls.Add(_valueLabel);
            Controls.Add(_captionLabel);
        }

        public void SetValue(string value, string caption)
        {
            _valueLabel.Text = value;
            _captionLabel.Text = caption;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using var accentBrush = new SolidBrush(_accent);
            e.Graphics.FillRectangle(accentBrush, 0, 0, Width, 4);
        }
    }

    private sealed class SprintIssueBucket : Panel
    {
        private readonly Panel _header;
        private readonly Label _titleLabel;
        private readonly Label _metaLabel;
        private readonly ListView _listView;
        private readonly Label _emptyLabel;
        private readonly ColumnHeader _keyColumn;
        private readonly ColumnHeader _summaryColumn;
        private readonly ColumnHeader _statusColumn;
        private readonly ColumnHeader _storyPointsColumn;
        private readonly ColumnHeader _assigneeColumn;
        private Color _accent;

        public SprintIssueBucket(string title, Color accent)
        {
            _accent = accent;
            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 12, 0);
            Padding = new Padding(0);
            BackColor = JiraTheme.BgSurface;

            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 54,
                BackColor = Tint(accent, 0.9f),
                Padding = new Padding(12, 10, 12, 8),
            };
            _titleLabel = JiraControlFactory.CreateLabel(title);
            _metaLabel = JiraControlFactory.CreateLabel("0 issue", true);
            _titleLabel.Location = new Point(0, 0);
            _metaLabel.Location = new Point(0, 26);
            _header.Controls.Add(_titleLabel);
            _header.Controls.Add(_metaLabel);

            _keyColumn = new ColumnHeader { Text = "Key" };
            _summaryColumn = new ColumnHeader { Text = "Summary" };
            _statusColumn = new ColumnHeader { Text = "Status" };
            _storyPointsColumn = new ColumnHeader { Text = "SP" };
            _assigneeColumn = new ColumnHeader { Text = "Assignee" };
            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None,
                MultiSelect = false,
            };
            _listView.Columns.AddRange([_keyColumn, _summaryColumn, _statusColumn, _storyPointsColumn, _assigneeColumn]);
            JiraTheme.StyleListView(_listView);

            _emptyLabel = JiraControlFactory.CreateLabel(string.Empty, true);
            _emptyLabel.Dock = DockStyle.Fill;
            _emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            _emptyLabel.Visible = false;

            Controls.Add(_emptyLabel);
            Controls.Add(_listView);
            Controls.Add(_header);

            UpdateColumnWidths();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateColumnWidths();
        }

        public void SetIssues(string title, Color accent, IReadOnlyList<SprintReportIssueDto> issues, string emptyText)
        {
            _accent = accent;
            _header.BackColor = Tint(accent, 0.9f);
            _titleLabel.Text = title;
            _metaLabel.Text = $"{issues.Count} issue | {issues.Sum(x => x.StoryPoints)} SP";

            _listView.BeginUpdate();
            _listView.Items.Clear();
            foreach (var issue in issues)
            {
                var item = new ListViewItem(issue.IssueKey);
                item.SubItems.Add(issue.Title);
                item.SubItems.Add(IssueDisplayText.TranslateStatus(issue.StatusName));
                item.SubItems.Add(issue.StoryPoints.ToString());
                item.SubItems.Add(string.IsNullOrWhiteSpace(issue.AssigneeSummary) ? "Chưa giao" : issue.AssigneeSummary);
                _listView.Items.Add(item);
            }
            _listView.EndUpdate();

            _listView.Visible = issues.Count > 0;
            _emptyLabel.Visible = issues.Count == 0;
            _emptyLabel.Text = emptyText;
            UpdateColumnWidths();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using var accentBrush = new SolidBrush(_accent);
            e.Graphics.FillRectangle(accentBrush, 0, 0, Width, 4);
        }

        private void UpdateColumnWidths()
        {
            var available = Math.Max(320, _listView.ClientSize.Width);
            _keyColumn.Width = 82;
            _statusColumn.Width = 108;
            _storyPointsColumn.Width = 44;
            _assigneeColumn.Width = 116;
            _summaryColumn.Width = Math.Max(120, available - _keyColumn.Width - _statusColumn.Width - _storyPointsColumn.Width - _assigneeColumn.Width - 4);
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

    private sealed class CfdChartPanel : Panel
    {
        private IReadOnlyList<CfdDataPointDto> _data = [];

        public CfdChartPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = JiraTheme.BgSurface;
        }

        public IReadOnlyList<CfdDataPointDto> Data
        {
            get => _data;
            set
            {
                _data = value ?? [];
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_data.Count == 0)
            {
                return;
            }

            var dates = _data.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
            var statuses = _data
                .GroupBy(x => x.StatusId)
                .Select(group => group.First())
                .OrderBy(x => x.Category == StatusCategory.Done ? 2 : x.Category == StatusCategory.InProgress ? 1 : 0)
                .ThenBy(x => x.DisplayOrder)
                .ThenBy(x => x.Status)
                .ToList();
            if (dates.Count == 0 || statuses.Count == 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            const int legendWidth = 160;
            var plot = new Rectangle(70, 48, Math.Max(10, Width - legendWidth - 110), Math.Max(10, Height - 118));
            if (plot.Width <= 10 || plot.Height <= 10)
            {
                return;
            }

            var totalsByDate = dates.ToDictionary(
                date => date,
                date => _data.Where(point => point.Date == date).Sum(point => point.IssueCount));
            var maxValue = (float)Math.Max(1, totalsByDate.Values.DefaultIfEmpty(0).Max());

            DrawAxes(e.Graphics, plot, maxValue, dates.Count);
            DrawYAxisLabels(e.Graphics, plot, maxValue);
            DrawDateAxisLabels(e.Graphics, plot, dates);
            DrawStackedAreas(e.Graphics, plot, maxValue, dates, statuses, _data);
            DrawStatusLegend(e.Graphics, new Rectangle(plot.Right + 18, 52, legendWidth - 20, Math.Max(80, Height - 100)), statuses);
        }

        private static void DrawStackedAreas(
            Graphics graphics,
            Rectangle plot,
            float maxValue,
            IReadOnlyList<DateOnly> dates,
            IReadOnlyList<CfdDataPointDto> statuses,
            IReadOnlyList<CfdDataPointDto> points)
        {
            var values = points.ToDictionary(
                point => (point.Date, point.StatusId),
                point => point.IssueCount);
            var lower = new float[dates.Count];

            foreach (var status in statuses)
            {
                var upper = new float[dates.Count];
                for (var index = 0; index < dates.Count; index++)
                {
                    var count = values.GetValueOrDefault((dates[index], status.StatusId), 0);
                    upper[index] = lower[index] + count;
                }

                var areaPoints = new List<PointF>(dates.Count * 2);
                for (var index = 0; index < dates.Count; index++)
                {
                    areaPoints.Add(MapPlotPoint(plot, maxValue, dates.Count, index, upper[index]));
                }

                for (var index = dates.Count - 1; index >= 0; index--)
                {
                    areaPoints.Add(MapPlotPoint(plot, maxValue, dates.Count, index, lower[index]));
                }

                var seriesColor = ParseSeriesColor(status);
                using var fillBrush = new SolidBrush(Color.FromArgb(172, seriesColor));
                using var borderPen = new Pen(seriesColor, 1.4f);
                if (areaPoints.Count >= 3)
                {
                    graphics.FillPolygon(fillBrush, areaPoints.ToArray());
                }

                var topEdge = areaPoints.Take(dates.Count).ToArray();
                if (topEdge.Length > 1)
                {
                    graphics.DrawLines(borderPen, topEdge);
                }

                lower = upper;
            }
        }

        private static void DrawStatusLegend(Graphics graphics, Rectangle bounds, IReadOnlyList<CfdDataPointDto> statuses)
        {
            var y = bounds.Top;
            foreach (var status in statuses)
            {
                var color = ParseSeriesColor(status);
                using var brush = new SolidBrush(color);
                using var borderPen = new Pen(ControlPaint.Dark(color));
                graphics.FillRectangle(brush, bounds.Left, y + 2, 14, 14);
                graphics.DrawRectangle(borderPen, bounds.Left, y + 2, 14, 14);
                TextRenderer.DrawText(
                    graphics,
                    status.Status,
                    JiraTheme.FontCaption,
                    new Rectangle(bounds.Left + 22, y - 2, bounds.Width - 22, 22),
                    JiraTheme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                y += 24;
            }
        }

        private static PointF MapPlotPoint(Rectangle plot, float maxValue, int pointCount, int index, float value)
        {
            var x = pointCount == 1
                ? plot.Left + (plot.Width / 2f)
                : plot.Left + (plot.Width * (index / (float)(pointCount - 1)));
            var y = plot.Bottom - (value / maxValue * plot.Height);
            return new PointF(x, y);
        }

        private static Color ParseSeriesColor(CfdDataPointDto status)
        {
            if (!string.IsNullOrWhiteSpace(status.Color))
            {
                try
                {
                    return ColorTranslator.FromHtml(status.Color);
                }
                catch
                {
                }
            }

            if (status.Category == StatusCategory.Done)
            {
                return JiraTheme.Success;
            }

            if (status.Category == StatusCategory.InProgress)
            {
                return JiraTheme.PrimaryActive;
            }

            if (status.Status.Contains("selected", StringComparison.OrdinalIgnoreCase))
            {
                return JiraTheme.Warning;
            }

            return JiraTheme.Neutral500;
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

    private static void DrawDateAxisLabels(Graphics graphics, Rectangle plot, IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
        {
            return;
        }

        var step = dates.Count <= 7 ? 1 : (int)Math.Ceiling(dates.Count / 7d);
        for (var index = 0; index < dates.Count; index += step)
        {
            DrawDateLabel(graphics, plot, dates, index);
        }

        if ((dates.Count - 1) % step != 0)
        {
            DrawDateLabel(graphics, plot, dates, dates.Count - 1);
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

    private static void DrawDateLabel(Graphics graphics, Rectangle plot, IReadOnlyList<DateOnly> dates, int index)
    {
        var x = dates.Count == 1
            ? plot.Left + (plot.Width / 2f)
            : plot.Left + (plot.Width * (index / (float)(dates.Count - 1)));
        TextRenderer.DrawText(
            graphics,
            dates[index].ToString("dd MMM"),
            JiraTheme.FontCaption,
            new Rectangle((int)x - 28, plot.Bottom + 8, 56, 18),
            JiraTheme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static Color Tint(Color color, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var red = (int)(color.R + ((255 - color.R) * amount));
        var green = (int)(color.G + ((255 - color.G) * amount));
        var blue = (int)(color.B + ((255 - color.B) * amount));
        return Color.FromArgb(red, green, blue);
    }

    private static string TrimLabel(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..Math.Max(1, maxLength - 3)] + "...";
}




