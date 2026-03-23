using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace JiraClone.WinForms.Helpers;

public static class VietnameseUi
{
    private static readonly ConditionalWeakTable<Form, object?> AppliedForms = new();
    private static readonly Dictionary<string, string> Exact = new(StringComparer.Ordinal)
    {
        ["Projects"] = "Dự án",
        ["Dashboard"] = "Tổng quan",
        ["Sprint Progress"] = "Tiến độ sprint",
        ["Issue Statistics"] = "Thống kê issue",
        ["Recent Activity"] = "Hoạt động gần đây",
        ["Assigned To Me"] = "Được giao cho tôi",
        ["Team Workload"] = "Tải công việc của nhóm",
        ["Board"] = "Bảng",
        ["Backlog"] = "Backlog",
        ["Roadmap"] = "Lộ trình",
        ["Sprints"] = "Sprint",
        ["Issues"] = "Issue",
        ["Reports"] = "Báo cáo",
        ["Settings"] = "Cài đặt",
        ["Notifications"] = "Thông báo",
        ["Project"] = "Dự án",
        ["Profile"] = "Hồ sơ",
        ["General"] = "Tổng quan",
        ["Overview"] = "Tổng quan",
        ["Members"] = "Thành viên",
        ["Permissions"] = "Phân quyền",
        ["Integrations"] = "Tích hợp",
        ["Webhooks"] = "Webhook",
        ["Workflow"] = "Quy trình",
        ["Labels"] = "Nhãn",
        ["Components"] = "Thành phần",
        ["Versions"] = "Phiên bản",
        ["Users"] = "Người dùng",
        ["Refresh"] = "Làm mới",
        ["Create"] = "Tạo",
        ["Save"] = "Lưu",
        ["Edit"] = "Sửa",
        ["Delete"] = "Xóa",
        ["Cancel"] = "Hủy",
        ["Close"] = "Đóng",
        ["Open"] = "Mở",
        ["Username"] = "Tên đăng nhập",
        ["Password"] = "Mật khẩu",
        ["Email"] = "Email",
        ["Name"] = "Tên",
        ["Description"] = "Mô tả",
        ["Status"] = "Trạng thái",
        ["Priority"] = "Độ ưu tiên",
        ["Type"] = "Loại",
        ["Assignee"] = "Người phụ trách",
        ["Assignees"] = "Người được giao",
        ["Key"] = "Mã",
        ["Category"] = "Danh mục",
        ["Color"] = "Màu sắc",
        ["Search projects"] = "Tìm kiếm dự án",
        ["Search issues"] = "Tìm kiếm issue",
        ["Search issue"] = "Tìm kiếm issue",
        ["Search users"] = "Tìm kiếm người dùng",
        ["Clear filters"] = "Xóa bộ lọc",
        ["Open issue"] = "Mở issue",
        ["Open Project"] = "Mở dự án",
        ["Open Epic"] = "Mở epic",
        ["Your Projects"] = "Dự án của bạn",
        ["+ Create Project"] = "+ Tạo dự án",
        ["Run Query"] = "Chạy truy vấn",
        ["Clear"] = "Xóa",
        ["Save Filter"] = "Lưu bộ lọc",
        ["Delete Filter"] = "Xóa bộ lọc",
        ["Saved Filters"] = "Bộ lọc đã lưu",
        ["0 issues"] = "0 issue",
        ["No issues match the current query."] = "Không có issue nào khớp truy vấn hiện tại.",
        ["No notifications yet."] = "Chưa có thông báo nào.",
        ["Mark all as read"] = "Đánh dấu đã đọc tất cả",
        ["No active project was found."] = "Không tìm thấy dự án đang hoạt động.",
        ["No active sprint"] = "Không có sprint đang hoạt động",
        ["Start Sprint"] = "Bắt đầu sprint",
        ["Mode: Scrum"] = "Chế độ: Scrum",
        ["Mode: Kanban"] = "Chế độ: Kanban",
        ["Group by Epic"] = "Nhóm theo Epic",
        ["All assignees"] = "Tất cả người được giao",
        ["All priorities"] = "Tất cả mức ưu tiên",
        ["All types"] = "Tất cả loại",
        ["All sprints"] = "Tất cả sprint",
        ["Week view"] = "Chế độ tuần",
        ["Month view"] = "Chế độ tháng",
        ["Day view"] = "Chế độ ngày",
        ["No epics match the current roadmap filters."] = "Không có epic nào khớp bộ lọc lộ trình hiện tại.",
        ["Select an epic"] = "Chọn một epic",
        ["Click an epic bar to inspect child issues and progress."] = "Bấm vào thanh epic để xem issue con và tiến độ.",
        ["This epic has no linked child issues yet."] = "Epic này chưa có issue con được liên kết.",
        ["Confluence Pages"] = "Trang Confluence",
        ["Commits"] = "Commit",
        ["Pull Requests"] = "Pull Request",
        ["Connected"] = "Đã kết nối",
        ["Not configured"] = "Chưa cấu hình",
        ["No linked commits yet."] = "Chưa có commit nào được liên kết.",
        ["No linked pull requests yet."] = "Chưa có pull request nào được liên kết.",
        ["No Confluence pages linked yet."] = "Chưa có trang Confluence nào được liên kết.",
        ["Configure"] = "Cấu hình",
        ["Disconnect"] = "Ngắt kết nối",
        ["Disconnect Integration"] = "Ngắt kết nối tích hợp",
        ["Disconnected"] = "Chưa kết nối",
        ["Not available."] = "Chưa khả dụng.",
        ["Not configured."] = "Chưa cấu hình.",
        ["Email notifications"] = "Thông báo email",
        ["Save Preferences"] = "Lưu tùy chọn",
        ["Create New Token"] = "Tạo token mới",
        ["API Tokens"] = "API token",
        ["No API tokens created yet."] = "Chưa tạo API token nào.",
        ["Scopes"] = "Phạm vi",
        ["Created"] = "Ngày tạo",
        ["Last Used"] = "Dùng gần nhất",
        ["Expires"] = "Hết hạn",
        ["Revoke"] = "Thu hồi",
        ["Revoke API Token"] = "Thu hồi API token",
        ["Revoked"] = "Đã thu hồi",
        ["Expired"] = "Đã hết hạn",
        ["Active"] = "Đang hoạt động",
        ["Never"] = "Không bao giờ",
        ["Create Token"] = "Tạo token",
        ["Expiry"] = "Thời hạn",
        ["30 days"] = "30 ngày",
        ["90 days"] = "90 ngày",
        ["1 year"] = "1 năm",
        ["Drop files here or browse to attach"] = "Thả tệp vào đây hoặc chọn tệp để đính kèm",
        ["Choose a file first."] = "Hãy chọn tệp trước.",
        ["Drop issue here"] = "Thả issue vào đây",
        ["Choose how Jira Desktop should notify you about assignments, comments, sprint updates, and other changes."] = "Chọn cách Jira Desktop thông báo cho bạn về việc được giao, bình luận, cập nhật sprint và các thay đổi khác.",
        ["When enabled, in-app notifications will also send email if SMTP is configured and your account has an email address."] = "Khi bật, thông báo trong ứng dụng cũng sẽ gửi email nếu SMTP đã được cấu hình và tài khoản của bạn có địa chỉ email.",
        ["Create personal access tokens for local tools and integrations. Raw tokens are shown once and stored only as SHA-256 hashes."] = "Tạo personal access token cho công cụ nội bộ và tích hợp. Raw token chỉ hiển thị một lần và chỉ được lưu dưới dạng hash SHA-256.",
        ["Link commits and pull requests to issue activity."] = "Liên kết commit và pull request với hoạt động của issue.",
        ["Create and link knowledge-base pages from issues."] = "Tạo và liên kết trang kiến thức từ issue.",
        ["Project Settings"] = "Cài đặt dự án",
        ["Adjust project details, members, board structure, workflows, permissions, labels, components, release versions, and outbound webhooks without leaving the desktop flow."] = "Điều chỉnh thông tin dự án, thành viên, cấu trúc bảng, workflow, phân quyền, nhãn, thành phần, phiên bản phát hành và webhook mà không cần rời luồng làm việc trên desktop.",
        ["Project overview across sprint progress, issue mix, activity, and team workload."] = "Tổng quan dự án theo tiến độ sprint, cơ cấu issue, hoạt động gần đây và tải công việc của nhóm.",
        ["Browse issues with advanced JQL search."] = "Duyệt issue với tìm kiếm JQL nâng cao.",
        ["Epic timeline across the active project. Filter by sprint or assignee, zoom the horizon, and drag bars to update schedule."] = "Dòng thời gian epic trong dự án hiện tại. Lọc theo sprint hoặc người phụ trách, phóng to thu nhỏ mốc thời gian và kéo thanh để cập nhật lịch.",
        ["Add a description..."] = "Thêm mô tả...",
        ["Unexpected Error"] = "Lỗi ngoài dự kiến",
        ["Error"] = "Lỗi",
        ["Startup Error"] = "Lỗi khởi động"
    };

    private static readonly Regex OverviewRegex = new(@"^Overview for (.+)\. Search filters activity, assigned work, and team rows\.$", RegexOptions.Compiled);
    private static readonly Regex TimelineRegex = new(@"^Timeline for (.+)\. Use the sidebar to jump between epics and drag bars to update dates\.$", RegexOptions.Compiled);
    private static readonly Regex NavigatorRegex = new(@"^Browse issues in (.+) with advanced JQL search\.$", RegexOptions.Compiled);
    private static readonly Regex ZoomRegex = new(@"^Zoom:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex LinkedRegex = new(@"^Linked (.+)$", RegexOptions.Compiled);
    private static readonly Regex EditedRegex = new(@"^Edited (.+)$", RegexOptions.Compiled);
    private static readonly Regex LastSyncRegex = new(@"^(.+?)\s+Last sync:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex RevokeRegex = new(@"^Revoke API token '(.+)'\? This action cannot be undone\.$", RegexOptions.Compiled);
    private static readonly Regex DisconnectRegex = new(@"^Disconnect (.+) from this project\?$", RegexOptions.Compiled);
    private static readonly Regex DeleteStatusRegex = new(@"^Delete status '(.+)'\? Related board column and transitions will be removed too\.$", RegexOptions.Compiled);
    private static readonly Regex DeleteTransitionRegex = new(@"^Delete transition '(.+)'\?$", RegexOptions.Compiled);
    private static readonly Regex ActiveSprintRegex = new(@"^Active sprint:\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex CountRegex = new(@"^(\d+)\s+(issues|projects|members|columns|labels|components|versions|webhooks)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StepRegex = new(@"^Step\s+(\d+)\s+of\s+(\d+)$", RegexOptions.Compiled);
    private static bool _initialized;

    public static void InitializeGlobalHook()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        System.Windows.Forms.Application.Idle += (_, _) =>
        {
            foreach (Form form in System.Windows.Forms.Application.OpenForms)
            {
                if (AppliedForms.TryGetValue(form, out _))
                {
                    continue;
                }

                LayoutHelper.EnableResponsiveLayout(form);
                Apply(form);
                AppliedForms.Add(form, null);
            }
        };
    }

    public static string Translate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        if (Exact.TryGetValue(text, out var translated))
        {
            return translated;
        }

        if (OverviewRegex.Match(text) is { Success: true } overview)
        {
            return $"Tổng quan cho {overview.Groups[1].Value}. Dùng ô tìm kiếm để lọc hoạt động, công việc được giao và dữ liệu nhóm.";
        }

        if (TimelineRegex.Match(text) is { Success: true } timeline)
        {
            return $"Dòng thời gian của {timeline.Groups[1].Value}. Dùng thanh bên để chuyển giữa các epic và kéo thanh để cập nhật ngày.";
        }

        if (NavigatorRegex.Match(text) is { Success: true } navigator)
        {
            return $"Duyệt issue trong {navigator.Groups[1].Value} với tìm kiếm JQL nâng cao.";
        }

        if (ZoomRegex.Match(text) is { Success: true } zoom)
        {
            return $"Thu phóng: {Translate(zoom.Groups[1].Value)}";
        }

        if (LinkedRegex.Match(text) is { Success: true } linked)
        {
            return $"Đã liên kết {linked.Groups[1].Value}";
        }

        if (EditedRegex.Match(text) is { Success: true } edited)
        {
            return $"Đã sửa {edited.Groups[1].Value}";
        }

        if (LastSyncRegex.Match(text) is { Success: true } lastSync)
        {
            return $"{Translate(lastSync.Groups[1].Value)}  Đồng bộ lần cuối: {lastSync.Groups[2].Value}";
        }

        if (RevokeRegex.Match(text) is { Success: true } revoke)
        {
            return $"Thu hồi API token '{revoke.Groups[1].Value}'? Hành động này không thể hoàn tác.";
        }

        if (DisconnectRegex.Match(text) is { Success: true } disconnect)
        {
            return $"Ngắt kết nối {disconnect.Groups[1].Value} khỏi dự án này?";
        }

        if (DeleteStatusRegex.Match(text) is { Success: true } deleteStatus)
        {
            return $"Xóa trạng thái '{deleteStatus.Groups[1].Value}'? Cột bảng liên quan và các chuyển trạng thái cũng sẽ bị xóa.";
        }

        if (DeleteTransitionRegex.Match(text) is { Success: true } deleteTransition)
        {
            return $"Xóa chuyển trạng thái '{deleteTransition.Groups[1].Value}'?";
        }

        if (ActiveSprintRegex.Match(text) is { Success: true } activeSprint)
        {
            return $"Sprint đang hoạt động: {activeSprint.Groups[1].Value}";
        }

        if (CountRegex.Match(text) is { Success: true } count)
        {
            var suffix = count.Groups[2].Value.ToLowerInvariant() switch
            {
                "issues" => "issue",
                "projects" => "dự án",
                "members" => "thành viên",
                "columns" => "cột",
                "labels" => "nhãn",
                "components" => "thành phần",
                "versions" => "phiên bản",
                "webhooks" => "webhook",
                _ => count.Groups[2].Value
            };
            return $"{count.Groups[1].Value} {suffix}";
        }

        if (StepRegex.Match(text) is { Success: true } step)
        {
            return $"Bước {step.Groups[1].Value} / {step.Groups[2].Value}";
        }

        return text switch
        {
            "TODO" => "CẦN LÀM",
            "SELECTED" => "ĐÃ CHỌN",
            "IN PROGRESS" => "ĐANG LÀM",
            "DONE" => "HOÀN THÀNH",
            _ => text
        };
    }

    public static void Apply(Control root)
    {
        TranslateControl(root);
        foreach (Control child in root.Controls)
        {
            Apply(child);
        }
    }

    private static void TranslateControl(Control control)
    {
        var translatedText = Translate(control.Text);
        if (!string.Equals(control.Text, translatedText, StringComparison.Ordinal))
        {
            control.Text = translatedText;
        }

        if (control is TextBox textBox)
        {
            var translatedPlaceholder = Translate(textBox.PlaceholderText);
            if (!string.Equals(textBox.PlaceholderText, translatedPlaceholder, StringComparison.Ordinal))
            {
                textBox.PlaceholderText = translatedPlaceholder;
            }
        }

        if (control is ComboBox comboBox && comboBox.DataSource is null && comboBox.Items.Count > 0)
        {
            for (var index = 0; index < comboBox.Items.Count; index++)
            {
                if (comboBox.Items[index] is string item)
                {
                    var translatedItem = Translate(item);
                    if (!string.Equals(item, translatedItem, StringComparison.Ordinal))
                    {
                        comboBox.Items[index] = translatedItem;
                    }
                }
            }
        }

        if (control is TabControl tabControl)
        {
            foreach (TabPage tabPage in tabControl.TabPages)
            {
                var translatedTab = Translate(tabPage.Text);
                if (!string.Equals(tabPage.Text, translatedTab, StringComparison.Ordinal))
                {
                    tabPage.Text = translatedTab;
                }
            }
        }

        if (control is DataGridView dataGridView)
        {
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                var translatedHeader = Translate(column.HeaderText);
                if (!string.Equals(column.HeaderText, translatedHeader, StringComparison.Ordinal))
                {
                    column.HeaderText = translatedHeader;
                }
            }
        }

        if (control is ListView listView)
        {
            foreach (ColumnHeader column in listView.Columns)
            {
                var translatedHeader = Translate(column.Text);
                if (!string.Equals(column.Text, translatedHeader, StringComparison.Ordinal))
                {
                    column.Text = translatedHeader;
                }
            }
        }

        if (control is TreeView treeView)
        {
            foreach (TreeNode node in treeView.Nodes)
            {
                TranslateNode(node);
            }
        }
    }

    private static void TranslateNode(TreeNode node)
    {
        var translated = Translate(node.Text);
        if (!string.Equals(node.Text, translated, StringComparison.Ordinal))
        {
            node.Text = translated;
        }

        foreach (TreeNode child in node.Nodes)
        {
            TranslateNode(child);
        }
    }
}


