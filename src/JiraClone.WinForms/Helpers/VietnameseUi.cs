using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace JiraClone.WinForms.Helpers;

public static class VietnameseUi
{
    private static readonly Dictionary<string, string> Exact = new(StringComparer.Ordinal)
    {
        ["Projects"] = "Dá»± Ã¡n",
        ["Dashboard"] = "Tá»•ng quan",
        ["Board"] = "Báº£ng",
        ["Backlog"] = "Backlog",
        ["Roadmap"] = "Lá»™ trÃ¬nh",
        ["Sprints"] = "Sprint",
        ["Issues"] = "Issue",
        ["Reports"] = "BÃ¡o cÃ¡o",
        ["Settings"] = "CÃ i Ä‘áº·t",
        ["Notifications"] = "ThÃ´ng bÃ¡o",
        ["No notifications yet."] = "ChÆ°a cÃ³ thÃ´ng bÃ¡o nÃ o.",
        ["Mark all as read"] = "ÄÃ¡nh dáº¥u Ä‘Ã£ Ä‘á»c táº¥t cáº£",
        ["Search projects"] = "TÃ¬m kiáº¿m dá»± Ã¡n",
        ["Logout"] = "ÄÄƒng xuáº¥t",
        ["Create"] = "Táº¡o",
        ["Cancel"] = "Há»§y",
        ["Clear filters"] = "XÃ³a bá»™ lá»c",
        ["Open issue"] = "Má»Ÿ issue",
        ["Open Project"] = "Má»Ÿ dá»± Ã¡n",
        ["Cards"] = "Tháº»",
        ["Grid"] = "LÆ°á»›i",
        ["Your Projects"] = "Dá»± Ã¡n cá»§a báº¡n",
        ["+ Create Project"] = "+ Táº¡o dá»± Ã¡n",
        ["You do not have any active projects yet."] = "Báº¡n chÆ°a cÃ³ dá»± Ã¡n Ä‘ang hoáº¡t Ä‘á»™ng nÃ o.",
        ["Project"] = "Dá»± Ã¡n",
        ["Profile"] = "Há»“ sÆ¡",
        ["General"] = "Tá»•ng quan",
        ["Members"] = "ThÃ nh viÃªn",
        ["Permissions"] = "PhÃ¢n quyá»n",
        ["Integrations"] = "TÃ­ch há»£p",
        ["Webhooks"] = "Webhook",
        ["Workflow"] = "Quy trÃ¬nh",
        ["Labels"] = "NhÃ£n",
        ["Components"] = "ThÃ nh pháº§n",
        ["Versions"] = "PhiÃªn báº£n",
        ["Project Settings"] = "CÃ i Ä‘áº·t dá»± Ã¡n",
        ["Save Project"] = "LÆ°u dá»± Ã¡n",
        ["Save Board"] = "LÆ°u báº£ng",
        ["Archive Project"] = "LÆ°u trá»¯ dá»± Ã¡n",
        ["Delete Project"] = "XÃ³a dá»± Ã¡n",
        ["Add Member"] = "ThÃªm thÃ nh viÃªn",
        ["Change Role"] = "Äá»•i vai trÃ²",
        ["Remove Member"] = "XÃ³a thÃ nh viÃªn",
        ["Edit Column"] = "Sá»­a cá»™t",
        ["Add Label"] = "ThÃªm nhÃ£n",
        ["Edit Label"] = "Sá»­a nhÃ£n",
        ["Delete Label"] = "XÃ³a nhÃ£n",
        ["Add Component"] = "ThÃªm thÃ nh pháº§n",
        ["Edit Component"] = "Sá»­a thÃ nh pháº§n",
        ["Delete Component"] = "XÃ³a thÃ nh pháº§n",
        ["Add Version"] = "ThÃªm phiÃªn báº£n",
        ["Edit Version"] = "Sá»­a phiÃªn báº£n",
        ["Delete Version"] = "XÃ³a phiÃªn báº£n",
        ["Mark Released"] = "ÄÃ¡nh dáº¥u phÃ¡t hÃ nh",
        ["Add Webhook"] = "ThÃªm webhook",
        ["Edit Webhook"] = "Sá»­a webhook",
        ["Delete Webhook"] = "XÃ³a webhook",
        ["Test"] = "Kiá»ƒm tra",
        ["Delivery History"] = "Lá»‹ch sá»­ gá»­i",
        ["Save Permissions"] = "LÆ°u phÃ¢n quyá»n",
        ["No members on this project yet."] = "Dá»± Ã¡n nÃ y chÆ°a cÃ³ thÃ nh viÃªn nÃ o.",
        ["No board columns configured."] = "ChÆ°a cáº¥u hÃ¬nh cá»™t báº£ng.",
        ["No labels created for this project yet."] = "Dá»± Ã¡n nÃ y chÆ°a cÃ³ nhÃ£n nÃ o.",
        ["No components created for this project yet."] = "Dá»± Ã¡n nÃ y chÆ°a cÃ³ thÃ nh pháº§n nÃ o.",
        ["No versions created for this project yet."] = "Dá»± Ã¡n nÃ y chÆ°a cÃ³ phiÃªn báº£n nÃ o.",
        ["No webhooks configured for this project yet."] = "Dá»± Ã¡n nÃ y chÆ°a cáº¥u hÃ¬nh webhook nÃ o.",
        ["Name"] = "TÃªn",
        ["Description"] = "MÃ´ táº£",
        ["Category"] = "Danh má»¥c",
        ["URL"] = "URL",
        ["Project key"] = "MÃ£ dá»± Ã¡n",
        ["User"] = "NgÆ°á»i dÃ¹ng",
        ["Project Role"] = "Vai trÃ² dá»± Ã¡n",
        ["WIP Limit (0 = none)"] = "Giá»›i háº¡n WIP (0 = khÃ´ng giá»›i háº¡n)",
        ["Color"] = "MÃ u sáº¯c",
        ["Lead"] = "Phá»¥ trÃ¡ch",
        ["Release date"] = "NgÃ y phÃ¡t hÃ nh",
        ["Released"] = "ÄÃ£ phÃ¡t hÃ nh",
        ["Scheme name"] = "TÃªn scheme",
        ["Board Mode"] = "Cháº¿ Ä‘á»™ báº£ng",
        ["Dashboard"] = "Tá»•ng quan",
        ["Refresh"] = "LÃ m má»›i",
        ["Auto-refresh every 5 minutes"] = "Tá»± lÃ m má»›i má»—i 5 phÃºt",
        ["Reports"] = "BÃ¡o cÃ¡o",
        ["Sprint"] = "Sprint",
        ["Export PNG"] = "Xuáº¥t PNG",
        ["No sprint selected"] = "ChÆ°a chá»n sprint",
        ["Velocity history"] = "Lá»‹ch sá»­ velocity",
        ["Cumulative flow"] = "Luá»“ng tÃ­ch lÅ©y",
        ["Sprint report"] = "BÃ¡o cÃ¡o sprint",
        ["Closed sprint"] = "Sprint Ä‘Ã£ Ä‘Ã³ng",
        ["Roadmap"] = "Lá»™ trÃ¬nh",
        ["No epics match the current roadmap filters."] = "KhÃ´ng cÃ³ epic nÃ o khá»›p bá»™ lá»c hiá»‡n táº¡i.",
        ["Select an epic"] = "Chá»n má»™t epic",
        ["Open Epic"] = "Má»Ÿ epic",
        ["Epics"] = "Epic",
        ["Log in"] = "ÄÄƒng nháº­p",
        ["Log in to Jira Clone"] = "ÄÄƒng nháº­p vÃ o Jira Desktop",
        ["Username"] = "TÃªn Ä‘Äƒng nháº­p",
        ["Password"] = "Máº­t kháº©u",
        ["Remember me for 30 days"] = "Ghi nhá»› Ä‘Äƒng nháº­p trong 30 ngÃ y",
        ["Show"] = "Hiá»‡n",
        ["Hide"] = "áº¨n",
        ["Remember Me"] = "Ghi nhá»› Ä‘Äƒng nháº­p",
        ["Attachment"] = "Tá»‡p Ä‘Ã­nh kÃ¨m",
        ["Browse"] = "Chá»n tá»‡p",
        ["Upload"] = "Táº£i lÃªn",
        ["Download"] = "Táº£i xuá»‘ng",
        ["Delete"] = "XÃ³a",
        ["No attachments yet."] = "ChÆ°a cÃ³ tá»‡p Ä‘Ã­nh kÃ¨m nÃ o.",
        ["No activity yet."] = "ChÆ°a cÃ³ hoáº¡t Ä‘á»™ng nÃ o.",
        ["No issues in this column."] = "KhÃ´ng cÃ³ issue nÃ o trong cá»™t nÃ y.",
        ["No issues"] = "KhÃ´ng cÃ³ issue",
        ["Details"] = "Chi tiáº¿t",
        ["No labels"] = "KhÃ´ng cÃ³ nhÃ£n",
        ["Hours"] = "Sá»‘ giá»",
        ["Comment"] = "BÃ¬nh luáº­n",
        ["Apply"] = "Ãp dá»¥ng",
        ["Choose one or more labels"] = "Chá»n má»™t hoáº·c nhiá»u nhÃ£n",
        ["Link issues"] = "LiÃªn káº¿t issue",
        ["Choose one or more existing stories or tasks"] = "Chá»n má»™t hoáº·c nhiá»u story/task cÃ³ sáºµn",
        ["Choose one or more assignees"] = "Chá»n má»™t hoáº·c nhiá»u ngÆ°á»i Ä‘Æ°á»£c giao",
        ["Edit Comment"] = "Sá»­a bÃ¬nh luáº­n",
        ["Delete Comment"] = "XÃ³a bÃ¬nh luáº­n",
        ["Create Token"] = "Táº¡o token",
        ["Expiry"] = "Háº¿t háº¡n",
        ["Scopes"] = "Pháº¡m vi",
        ["Copy Token"] = "Sao chÃ©p token",
        ["Close"] = "ÄÃ³ng",
        ["Your new API token"] = "API token má»›i cá»§a báº¡n",
        ["Drop files here or browse to attach"] = "Tháº£ tá»‡p vÃ o Ä‘Ã¢y hoáº·c chá»n tá»‡p Ä‘á»ƒ Ä‘Ã­nh kÃ¨m",
        ["Watch"] = "Theo dÃµi",
        ["Log Time"] = "Ghi nháº­n thá»i gian",
        ["Edit Labels"] = "Sá»­a nhÃ£n",
        ["Add existing issue"] = "ThÃªm issue cÃ³ sáºµn",
        ["Create child issue"] = "Táº¡o issue con",
        ["Save"] = "LÆ°u",
        ["Comment body:"] = "Ná»™i dung bÃ¬nh luáº­n:",
        ["Assign"] = "GÃ¡n",
        ["Choose a file first."] = "HÃ£y chá»n tá»‡p trÆ°á»›c.",
        ["File exceeds the 10 MB limit."] = "Tá»‡p vÆ°á»£t quÃ¡ giá»›i háº¡n 10 MB.",
        ["Create project"] = "Táº¡o dá»± Ã¡n",
        ["Back"] = "Quay láº¡i",
        ["Next"] = "Tiáº¿p theo",
        ["Create Project"] = "Táº¡o dá»± Ã¡n",
        ["Create Sprint"] = "Táº¡o sprint",
        ["Close Sprint"] = "ÄÃ³ng sprint",
        ["Users"] = "NgÆ°á»i dÃ¹ng",
        ["Edit"] = "Sá»­a",
        ["Deactivate"] = "VÃ´ hiá»‡u hÃ³a",
        ["Activate"] = "KÃ­ch hoáº¡t",
        ["Reset Password"] = "Äáº·t láº¡i máº­t kháº©u",
        ["No users match the current filters."] = "KhÃ´ng cÃ³ ngÆ°á»i dÃ¹ng nÃ o khá»›p bá»™ lá»c hiá»‡n táº¡i.",
        ["Unexpected Error"] = "Lá»—i ngoÃ i dá»± kiáº¿n",
        ["Error"] = "Lá»—i",
        ["Copy Token"] = "Sao chÃ©p token",
        ["API Tokens"] = "API token",
        ["Create New Token"] = "Táº¡o token má»›i",
        ["No API tokens created yet."] = "ChÆ°a táº¡o API token nÃ o.",
        ["Status"] = "Tráº¡ng thÃ¡i",
        ["Priority"] = "Äá»™ Æ°u tiÃªn",
        ["Type"] = "Loáº¡i",
        ["Assignees"] = "NgÆ°á»i Ä‘Æ°á»£c giao",
        ["Key"] = "MÃ£",
        ["Summary"] = "TÃ³m táº¯t",
        ["Member"] = "ThÃ nh viÃªn",
        ["Column"] = "Cá»™t",
        ["Label"] = "NhÃ£n",
        ["Component"] = "ThÃ nh pháº§n",
        ["Version"] = "PhiÃªn báº£n",
        ["Active"] = "Báº­t",
        ["Last delivery"] = "Láº§n gá»­i gáº§n nháº¥t",
        ["Page title"] = "TiÃªu Ä‘á» trang",
        ["Page URL"] = "URL trang",
        ["Add Link"] = "ThÃªm liÃªn káº¿t",
        ["Base URL"] = "Base URL",
        ["Space key"] = "Space key",
        ["Email"] = "Email",
        ["API token"] = "API token",
        ["Create Confluence Page"] = "Táº¡o trang Confluence",
        ["Add Page Link"] = "ThÃªm liÃªn káº¿t trang",
        ["No active sprint"] = "KhÃ´ng cÃ³ sprint Ä‘ang hoáº¡t Ä‘á»™ng",
        ["Start Sprint"] = "Báº¯t Ä‘áº§u sprint",
        ["Mode: Scrum"] = "Cháº¿ Ä‘á»™: Scrum",
        ["Mode: Kanban"] = "Cháº¿ Ä‘á»™: Kanban",
        ["Group by Epic"] = "NhÃ³m theo Epic",
        ["Search issues"] = "TÃ¬m kiáº¿m issue",
        ["Search users"] = "TÃ¬m kiáº¿m ngÆ°á»i dÃ¹ng",
        ["Leave blank to use ChangeMe123!"] = "Äá»ƒ trá»‘ng Ä‘á»ƒ dÃ¹ng ChangeMe123!",
        ["Password has been reset."] = "Máº­t kháº©u Ä‘Ã£ Ä‘Æ°á»£c Ä‘áº·t láº¡i.",
        ["User Management"] = "Quáº£n lÃ½ ngÆ°á»i dÃ¹ng",
        ["Confirm Delete"] = "XÃ¡c nháº­n xÃ³a",
        ["Delete this comment?"] = "XÃ³a bÃ¬nh luáº­n nÃ y?",
        ["Delete this issue?"] = "XÃ³a issue nÃ y?",
        ["Logout"] = "ÄÄƒng xuáº¥t",
        ["Log out from Jira Clone?"] = "Báº¡n cÃ³ muá»‘n Ä‘Äƒng xuáº¥t khá»i Jira Desktop khÃ´ng?",
        ["Export PNG"] = "Xuáº¥t PNG"
    };

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
                Apply(form);
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

        if (text.StartsWith("Sign in with ", StringComparison.Ordinal))
        {
            return $"ÄÄƒng nháº­p vá»›i {text[13..]}";
        }

        if (text.StartsWith("Or sign in with ", StringComparison.Ordinal))
        {
            return $"Hoáº·c Ä‘Äƒng nháº­p vá»›i {text[16..]}";
        }

        if (text.StartsWith("Browse every issue in ", StringComparison.Ordinal) && text.EndsWith(".", StringComparison.Ordinal))
        {
            return $"Xem má»i issue trong {text[22..^1]}.";
        }

        if (text.StartsWith("Saved ", StringComparison.Ordinal) && text.EndsWith(".", StringComparison.Ordinal))
        {
            return $"ÄÃ£ lÆ°u {text[6..^1]}.";
        }

        if (Regex.Match(text, @"^Step\s+(\d+)\s+of\s+(\d+)$") is { Success: true } stepMatch)
        {
            return $"BÆ°á»›c {stepMatch.Groups[1].Value} / {stepMatch.Groups[2].Value}";
        }

        if (Regex.Match(text, @"^(\d+)\s+issues$") is { Success: true } issuesMatch)
        {
            return $"{issuesMatch.Groups[1].Value} issue";
        }

        if (Regex.Match(text, @"^(\d+)\s+projects$") is { Success: true } projectsMatch)
        {
            return $"{projectsMatch.Groups[1].Value} dá»± Ã¡n";
        }

        if (Regex.Match(text, @"^Type\s+(.+?)\s+to\s+confirm\s+deleting\s+(.+)\.$") is { Success: true } deleteMatch)
        {
            return $"Nháº­p {deleteMatch.Groups[1].Value} Ä‘á»ƒ xÃ¡c nháº­n xÃ³a {deleteMatch.Groups[2].Value}.";
        }

        return text switch
        {
            "TODO" => "Cáº¦N LÃ€M",
            "SELECTED" => "ÄÃƒ CHá»ŒN",
            "IN PROGRESS" => "ÄANG LÃ€M",
            "DONE" => "HOÃ€N THÃ€NH",
            "LOW" => "THáº¤P",
            "MEDIUM" => "TRUNG BÃŒNH",
            "HIGH" => "CAO",
            "HIGHEST" => "CAO NHáº¤T",
            "TASK" => "TASK",
            "BUG" => "BUG",
            "STORY" => "STORY",
            _ => text,
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
        control.Text = Translate(control.Text);

        if (control is TextBox textBox)
        {
            textBox.PlaceholderText = Translate(textBox.PlaceholderText);
        }

        if (control is ComboBox comboBox && comboBox.DataSource is null && comboBox.Items.Count > 0)
        {
            for (var index = 0; index < comboBox.Items.Count; index++)
            {
                if (comboBox.Items[index] is string item)
                {
                    comboBox.Items[index] = Translate(item);
                }
            }
        }

        if (control is TabControl tabControl)
        {
            foreach (TabPage tabPage in tabControl.TabPages)
            {
                tabPage.Text = Translate(tabPage.Text);
                Apply(tabPage);
            }
        }

        if (control is DataGridView dataGridView)
        {
            foreach (DataGridViewColumn column in dataGridView.Columns)
            {
                column.HeaderText = Translate(column.HeaderText);
            }
        }

        if (control is ListView listView)
        {
            foreach (ColumnHeader column in listView.Columns)
            {
                column.Text = Translate(column.Text);
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
        node.Text = Translate(node.Text);
        foreach (TreeNode child in node.Nodes)
        {
            TranslateNode(child);
        }
    }
}
