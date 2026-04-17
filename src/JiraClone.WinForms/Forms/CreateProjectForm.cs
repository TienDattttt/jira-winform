using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.WinForms.Composition;
using JiraClone.WinForms.Services;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Forms;

public sealed class CreateProjectForm : Form
{
    private readonly AppSession _session;
    private readonly Label _stepLabel = JiraControlFactory.CreateLabel("Step 1 of 2", true);
    private readonly Label _titleLabel = JiraControlFactory.CreateLabel("Create project");
    private readonly Label _subtitleLabel = JiraControlFactory.CreateLabel("Set the project name, key, category, and description before inviting teammates.", true);
    private readonly Label _validationLabel = JiraControlFactory.CreateLabel(string.Empty, true);
    private readonly TextBox _nameTextBox = JiraControlFactory.CreateTextBox();
    private readonly TextBox _keyTextBox = JiraControlFactory.CreateTextBox();
    private readonly ComboBox _categoryComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = JiraTheme.BgSurface, ForeColor = JiraTheme.TextPrimary, Font = JiraTheme.FontBody };
    private readonly TextBox _descriptionTextBox = JiraControlFactory.CreateTextBox();
    private readonly Panel _stepOnePanel = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
    private readonly Panel _stepTwoPanel = new() { Dock = DockStyle.Fill, BackColor = JiraTheme.BgSurface };
    private readonly DataGridView _memberGrid = new();
    private readonly BindingSource _memberBindingSource = new();
    private readonly Button _backButton = JiraControlFactory.CreateSecondaryButton("Quay lại");
    private readonly Button _nextButton = JiraControlFactory.CreatePrimaryButton("Tiếp theo");
    private readonly Button _createButton = JiraControlFactory.CreatePrimaryButton("Tạo dự án");
    private readonly Button _cancelButton = JiraControlFactory.CreateSecondaryButton("Hủy");

    private bool _isBusy;
    private bool _isKeyDirty;
    private bool _isSynchronizingKey;
    private int _stepIndex;

    public CreateProjectForm(AppSession session)
    {
        _session = session;
        Text = "Create Project";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(860, 620);
        MinimumSize = new Size(860, 620);
        BackColor = JiraTheme.BgSurface;
        Font = JiraTheme.FontBody;

        _titleLabel.Font = JiraTheme.FontH1;
        _nameTextBox.AccessibleName = "CreateProject_TextBox_Name";
        _keyTextBox.AccessibleName = "CreateProject_TextBox_Key";
        _categoryComboBox.AccessibleName = "CreateProject_ComboBox_Category";
        _descriptionTextBox.AccessibleName = "CreateProject_TextBox_Description";
        _nextButton.AccessibleName = "CreateProject_Button_Next";
        _createButton.AccessibleName = "CreateProject_Button_Create";
        _cancelButton.AccessibleName = "CreateProject_Button_Cancel";
        _stepLabel.Font = JiraTheme.FontCaption;
        _validationLabel.ForeColor = JiraTheme.Danger;
        _validationLabel.AutoSize = false;
        _validationLabel.Height = 34;
        _validationLabel.Dock = DockStyle.Top;
        _validationLabel.TextAlign = ContentAlignment.MiddleLeft;
        _validationLabel.Visible = false;

        _nameTextBox.Dock = DockStyle.Fill;
        _keyTextBox.Dock = DockStyle.Fill;
        _keyTextBox.CharacterCasing = CharacterCasing.Upper;
        _descriptionTextBox.Multiline = true;
        _descriptionTextBox.ScrollBars = ScrollBars.Vertical;
        _descriptionTextBox.AcceptsReturn = true;
        _descriptionTextBox.Height = 140;
        _descriptionTextBox.Dock = DockStyle.Fill;
        _categoryComboBox.DataSource = Enum.GetValues<ProjectCategory>();

        _nameTextBox.TextChanged += (_, _) => SyncProjectKeyFromName();
        _keyTextBox.TextChanged += (_, _) =>
        {
            if (!_isSynchronizingKey && _keyTextBox.Focused)
            {
                _isKeyDirty = true;
            }
        };

        ConfigureMemberGrid();
        ConfigureButtons();
        BuildLayout();
        ApplyStep();

        Shown += async (_, _) => await LoadMembersAsync();
    }

    public int? CreatedProjectId { get; private set; }

    private void ConfigureButtons()
    {
        ConfigureActionButton(_backButton, 106);
        ConfigureActionButton(_nextButton, 106);
        ConfigureActionButton(_createButton, 142);
        ConfigureActionButton(_cancelButton, 106);

        _backButton.Click += (_, _) =>
        {
            if (_stepIndex == 0)
            {
                return;
            }

            _stepIndex = 0;
            ApplyStep();
        };
        _nextButton.Click += async (_, _) => await MoveNextAsync();
        _createButton.Click += async (_, _) => await CreateProjectAsync();
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        CancelButton = _cancelButton;
    }

    private void BuildLayout()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 120,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(28, 24, 28, 12),
        };
        _stepLabel.Location = new Point(0, 0);
        _titleLabel.Location = new Point(0, 28);
        _subtitleLabel.Location = new Point(0, 66);
        header.Controls.Add(_stepLabel);
        header.Controls.Add(_titleLabel);
        header.Controls.Add(_subtitleLabel);

        BuildStepOne();
        BuildStepTwo();

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(28, 0, 28, 0),
        };
        body.Controls.Add(_stepTwoPanel);
        body.Controls.Add(_stepOnePanel);
        body.Controls.Add(_validationLabel);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 78,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(24, 16, 24, 18),
            BackColor = JiraTheme.BgSurface,
        };
        footer.Controls.Add(_createButton);
        footer.Controls.Add(_nextButton);
        footer.Controls.Add(_cancelButton);
        footer.Controls.Add(_backButton);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
    }

    private void BuildStepOne()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            BackColor = JiraTheme.BgSurface,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddRow(layout, 0, "Name", _nameTextBox);
        AddRow(layout, 1, "Key", _keyTextBox);
        AddRow(layout, 2, "Category", _categoryComboBox);
        AddRow(layout, 3, "Description", _descriptionTextBox);

        var helperLabel = JiraControlFactory.CreateLabel("Khóa dự án phải phù hợp với định dạng [A-Z]{2,10}. Bạn có thể chỉnh sửa khóa được đề xuất trước khi tiếp tục.", true);
        helperLabel.Dock = DockStyle.Top;
        helperLabel.MaximumSize = new Size(720, 0);

        _stepOnePanel.Padding = new Padding(0, 8, 0, 0);
        _stepOnePanel.Controls.Add(layout);
        _stepOnePanel.Controls.Add(helperLabel);
    }

    private void BuildStepTwo()
    {
        var intro = JiraControlFactory.CreateLabel("Mời các thành viên và thiết lập vai trò của họ trong dự án. Bạn sẽ được tự động thêm vào dự án với vai trò Quản trị viên (Admin).", true);
        intro.Dock = DockStyle.Top;
        intro.Height = 36;

        var surface = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = JiraTheme.BgSurface,
            Padding = new Padding(0, 16, 0, 0),
        };
        surface.Paint += (_, e) =>
        {
            using var pen = new Pen(JiraTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, surface.Width - 1, surface.Height - 1);
        };
        surface.Controls.Add(_memberGrid);

        _stepTwoPanel.Padding = new Padding(0, 8, 0, 0);
        _stepTwoPanel.Controls.Add(surface);
        _stepTwoPanel.Controls.Add(intro);
    }

    private void ConfigureMemberGrid()
    {
        _memberGrid.Dock = DockStyle.Fill;
        _memberGrid.AllowUserToAddRows = false;
        _memberGrid.AllowUserToDeleteRows = false;
        _memberGrid.AllowUserToResizeRows = false;
        _memberGrid.AutoGenerateColumns = false;
        _memberGrid.RowHeadersVisible = false;
        _memberGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _memberGrid.MultiSelect = false;
        _memberGrid.BackgroundColor = JiraTheme.BgSurface;
        JiraTheme.StyleDataGridView(_memberGrid);
        _memberGrid.DataSource = _memberBindingSource;
        _memberGrid.ColumnHeadersHeight = 42;
        _memberGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        _memberGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(MemberInviteRow.Invite),
            HeaderText = "Invite",
            Width = 80,
        });
        _memberGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MemberInviteRow.DisplayName),
            HeaderText = "Name",
            Width = 220,
        });
        _memberGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MemberInviteRow.Email),
            HeaderText = "Email",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 220,
        });
        _memberGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(MemberInviteRow.ProjectRole),
            HeaderText = "Role",
            Width = 150,
            DataSource = Enum.GetValues<ProjectRole>(),
        });
    }

    private void ApplyStep()
    {
        _stepOnePanel.Visible = _stepIndex == 0;
        _stepTwoPanel.Visible = _stepIndex == 1;
        _backButton.Visible = _stepIndex == 1;
        _nextButton.Visible = _stepIndex == 0;
        _createButton.Visible = _stepIndex == 1;
        _stepLabel.Text = _stepIndex == 0 ? "Step 1 of 2" : "Step 2 of 2";
        _subtitleLabel.Text = _stepIndex == 0
            ? "Đặt tên dự án, khóa (key), danh mục và mô tả trước khi mời các thành viên trong nhóm."
            : "Mời các thành viên trong nhóm, phân công vai trò trong dự án và tạo dự án khi bạn đã sẵn sàng.";
        AcceptButton = _stepIndex == 0 ? _nextButton : _createButton;
        _validationLabel.Visible = false;
        _validationLabel.Text = string.Empty;
    }

    private async Task LoadMembersAsync()
    {
        try
        {
            var currentUserId = _session.CurrentUserContext.RequireUserId();
            var rows = (await _session.UserCommands.GetAllAsync())
                .Where(user => user.Id != currentUserId && user.IsActive)
                .OrderBy(user => user.DisplayName)
                .Select(user => new MemberInviteRow
                {
                    UserId = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                    ProjectRole = ProjectRole.Developer,
                })
                .ToList();
            _memberBindingSource.DataSource = new BindingList<MemberInviteRow>(rows);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
    }

    private void SyncProjectKeyFromName()
    {
        if (_isKeyDirty)
        {
            return;
        }

        var generatedKey = GenerateProjectKey(_nameTextBox.Text);
        _isSynchronizingKey = true;
        _keyTextBox.Text = generatedKey;
        _isSynchronizingKey = false;
    }

    private async Task MoveNextAsync()
    {
        if (!await ValidateStepOneAsync())
        {
            return;
        }

        _stepIndex = 1;
        ApplyStep();
    }

    private async Task<bool> ValidateStepOneAsync()
    {
        HideValidation();

        if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
        {
            ShowValidation("Tên dự án là bắt buộc.");
            return false;
        }

        var normalizedKey = NormalizeProjectKey(_keyTextBox.Text);
        if (!Regex.IsMatch(normalizedKey, "^[A-Z]{2,10}$", RegexOptions.CultureInvariant))
        {
            ShowValidation("Khóa dự án phải phù hợp với định dạng [A-Z]{2,10} (từ 2 đến 10 ký tự in hoa).");
            return false;
        }

        try
        {
            if (await _session.ProjectCommands.ProjectKeyExistsAsync(normalizedKey))
            {
                ShowValidation("Khóa dự án đó đã được sử dụng.");
                return false;
            }
        }
        catch (Exception exception)
        {
            ShowValidation(exception.Message);
            return false;
        }

        _keyTextBox.Text = normalizedKey;
        return true;
    }

    private async Task CreateProjectAsync()
    {
        if (_isBusy)
        {
            return;
        }

        try
        {
            if (!await ValidateStepOneAsync())
            {
                _stepIndex = 0;
                ApplyStep();
                return;
            }

            SetBusyState(true);
            var memberRows = _memberBindingSource.List.Cast<MemberInviteRow>().Where(row => row.Invite).ToList();
            var project = await _session.ProjectCommands.CreateProjectAsync(
                _nameTextBox.Text.Trim(),
                NormalizeProjectKey(_keyTextBox.Text),
                (ProjectCategory)_categoryComboBox.SelectedItem!,
                string.IsNullOrWhiteSpace(_descriptionTextBox.Text) ? null : _descriptionTextBox.Text.Trim(),
                memberRows.Select(row => new ProjectMemberInput(row.UserId, row.ProjectRole)).ToList());

            CreatedProjectId = project.Id;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ValidationException exception)
        {
            ShowValidation(exception.Message);
        }
        catch (Exception exception)
        {
            ErrorDialogService.Show(exception);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void SetBusyState(bool isBusy)
    {
        _isBusy = isBusy;
        UseWaitCursor = isBusy;
        _nameTextBox.Enabled = !isBusy;
        _keyTextBox.Enabled = !isBusy;
        _categoryComboBox.Enabled = !isBusy;
        _descriptionTextBox.Enabled = !isBusy;
        _memberGrid.Enabled = !isBusy;
        _backButton.Enabled = !isBusy;
        _nextButton.Enabled = !isBusy;
        _createButton.Enabled = !isBusy;
        _cancelButton.Enabled = !isBusy;
    }

    private void ShowValidation(string message)
    {
        _validationLabel.Text = message;
        _validationLabel.Visible = true;
    }

    private void HideValidation()
    {
        _validationLabel.Visible = false;
        _validationLabel.Text = string.Empty;
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(JiraControlFactory.CreateLabel(label, true), 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 42;
        button.MinimumSize = new Size(width, 38);
    }

    private static string NormalizeProjectKey(string? key)
    {
        return (key ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string GenerateProjectKey(string name)
    {
        var tokens = Regex.Matches(name.ToUpperInvariant(), "[A-Z]+")
            .Select(match => match.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        var letters = new string(name.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());

        var candidate = tokens.Count >= 2
            ? string.Concat(tokens.Select(token => token[0]))
            : letters;

        if (candidate.Length < 2)
        {
            candidate = letters;
        }

        candidate = new string(candidate.Where(char.IsLetter).ToArray());
        if (candidate.Length == 0)
        {
            return string.Empty;
        }

        if (candidate.Length == 1 && letters.Length > 1)
        {
            candidate = (candidate + letters[1]).ToUpperInvariant();
        }

        if (candidate.Length == 1)
        {
            candidate += "X";
        }

        return candidate[..Math.Min(10, candidate.Length)];
    }

    private sealed class MemberInviteRow
    {
        public bool Invite { get; set; }
        public int UserId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public ProjectRole ProjectRole { get; set; }
    }
}

