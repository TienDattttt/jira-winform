using JiraClone.Domain.Entities;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class WebhookDeliveryHistoryForm : Form
{
    private readonly AppSession _session;
    private readonly WebhookEndpoint _endpoint;
    private readonly DataGridView _grid = new();
    private readonly Label _emptyState = JiraControlFactory.CreateLabel("No delivery attempts recorded yet.", true);
    private readonly CancellationTokenSource _disposeCts = new();
    private CancellationTokenSource? _loadCts;

    public WebhookDeliveryHistoryForm(AppSession session, WebhookEndpoint endpoint)
    {
        _session = session;
        _endpoint = endpoint;

        Text = $"Delivery History - {endpoint.Name}";
        AutoScaleMode = AutoScaleMode.Font;
        Width = 980;
        Height = 560;
        MinimumSize = new Size(820, 460);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        JiraTheme.StyleDataGridView(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Attempted", Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Event", Width = 150 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Response", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Result", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Retry", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Error", Width = 220 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Payload", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220 });

        _emptyState.Dock = DockStyle.Fill;
        _emptyState.TextAlign = ContentAlignment.MiddleCenter;
        _emptyState.Visible = false;

        var header = new Panel { Dock = DockStyle.Top, Height = 68, Padding = new Padding(16, 14, 16, 8), BackColor = JiraTheme.BgSurface };
        var title = JiraControlFactory.CreateLabel($"{endpoint.Name} delivery history");
        title.Font = JiraTheme.FontH2;
        title.Location = new Point(0, 0);
        var caption = JiraControlFactory.CreateLabel(endpoint.Url, true);
        caption.Location = new Point(0, 32);
        header.Controls.Add(title);
        header.Controls.Add(caption);

        Controls.Add(_grid);
        Controls.Add(_emptyState);
        Controls.Add(header);

        Load += OnWebhookDeliveryHistoryFormLoad;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelPendingLoad();
            _disposeCts.Cancel();
            Load -= OnWebhookDeliveryHistoryFormLoad;
            _disposeCts.Dispose();
        }

        base.Dispose(disposing);
    }

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

    private void OnWebhookDeliveryHistoryFormLoad(object? sender, EventArgs e) => _ = RefreshHistoryAsync();

    private Task RefreshHistoryAsync(CancellationToken cancellationToken = default) => LoadHistoryAsync(RestartLoadCancellation(cancellationToken));

    private async Task LoadHistoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deliveries = await _session.Webhooks.GetDeliveryHistoryAsync(_endpoint.Id, 100, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _grid.Rows.Clear();
            foreach (var delivery in deliveries)
            {
                _grid.Rows.Add(
                    delivery.AttemptedAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm:ss"),
                    delivery.EventType.ToString(),
                    delivery.ResponseCode == 0 ? "-" : delivery.ResponseCode.ToString(),
                    delivery.Success ? "Success" : "Failed",
                    delivery.RetryCount,
                    string.IsNullOrWhiteSpace(delivery.ErrorMessage) ? "-" : delivery.ErrorMessage,
                    delivery.Payload);
            }

            _grid.Visible = deliveries.Count > 0;
            _emptyState.Visible = deliveries.Count == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                ErrorDialogService.Show(exception);
                Close();
            }
        }
    }
}
