using JiraClone.Domain.Entities;
using JiraClone.WinForms.Theme;

namespace JiraClone.WinForms.Controls;

public class CommentListControl : UserControl
{
    private readonly ListView _listView;
    private readonly Button _editButton;
    private readonly Button _deleteButton;
    private List<Comment> _comments = [];

    public CommentListControl()
    {
        DoubleBuffered = true;
        BackColor = JiraTheme.BgPage;
        Font = JiraTheme.FontBody;
        AutoScroll = true;

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            FullRowSelect = true,
            MultiSelect = false,
            View = View.Details,
            BorderStyle = BorderStyle.None,
            BackColor = JiraTheme.BgSurface,
            ForeColor = JiraTheme.TextPrimary,
            Font = JiraTheme.FontSmall,
            HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _listView.Columns.Add("Author", 140);
        _listView.Columns.Add("Comment", 360);
        _listView.Columns.Add("Updated", 180);

        _editButton = JiraControlFactory.CreateSecondaryButton("Edit Comment");
        _deleteButton = JiraControlFactory.CreateSecondaryButton("Delete Comment");
        ConfigureButton(_editButton, 116);
        ConfigureButton(_deleteButton, 126);

        _editButton.Click += async (_, _) =>
        {
            if (SelectedComment is not null && EditRequested is not null)
            {
                await EditRequested.Invoke(SelectedComment);
            }
        };
        _deleteButton.Click += async (_, _) =>
        {
            if (SelectedComment is not null && DeleteRequested is not null)
            {
                await DeleteRequested.Invoke(SelectedComment);
            }
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(4),
            BackColor = JiraTheme.BgPage
        };
        actions.Controls.Add(_editButton);
        actions.Controls.Add(_deleteButton);

        Controls.Add(_listView);
        Controls.Add(actions);
    }

    public Func<Comment, Task>? EditRequested { get; set; }
    public Func<Comment, Task>? DeleteRequested { get; set; }

    private Comment? SelectedComment =>
        _listView.SelectedIndices.Count == 0 ? null : _comments[_listView.SelectedIndices[0]];

    public void Bind(IReadOnlyList<Comment> comments)
    {
        _comments = comments.ToList();
        _listView.Items.Clear();
        foreach (var comment in _comments)
        {
            var updatedText = comment.UpdatedAtUtc > comment.CreatedAtUtc
                ? $"Edited {comment.UpdatedAtUtc:g}"
                : $"{comment.CreatedAtUtc:g}";

            var item = new ListViewItem(comment.User?.DisplayName ?? comment.UserId.ToString());
            item.SubItems.Add(comment.Body);
            item.SubItems.Add(updatedText);
            _listView.Items.Add(item);
        }
    }

    private static void ConfigureButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 36;
        button.MinimumSize = new Size(width, 32);
    }
}
