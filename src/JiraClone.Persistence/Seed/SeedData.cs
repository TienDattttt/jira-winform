using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Domain.Permissions;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Seed;

public static class SeedData
{
    private static readonly DateTime SeedTime = new(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc);
    private const string DefaultPasswordHash = "jgpZxNxCoXwMhOTNRy7GXZHyX6pwZyruG7q31ducr54=";
    private const string DefaultPasswordSalt = "hyp1jJnol7RJsq08AjbBaw==";

    public static void Apply(ModelBuilder modelBuilder)
    {
        SeedRoles(modelBuilder);
        SeedUsers(modelBuilder);
        SeedProjects(modelBuilder);
        SeedPermissionSchemes(modelBuilder);
        SeedProjectMemberships(modelBuilder);
        SeedWorkflows(modelBuilder);
        SeedBoardColumns(modelBuilder);
        SeedLabels(modelBuilder);
        SeedComponents(modelBuilder);
        SeedVersions(modelBuilder);
        SeedSprints(modelBuilder);
        SeedIssues(modelBuilder);
        SeedIssueAssignments(modelBuilder);
        SeedIssueLabels(modelBuilder);
        SeedIssueComponents(modelBuilder);
        SeedSavedFilters(modelBuilder);
        SeedComments(modelBuilder);
        SeedWatchers(modelBuilder);
        SeedNotifications(modelBuilder);
        SeedWebhooks(modelBuilder);
        SeedActivities(modelBuilder);
    }

    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin", Description = "Quản trị hệ thống", CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new Role { Id = 2, Name = "ProjectManager", Description = "Điều phối dự án", CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new Role { Id = 3, Name = "Developer", Description = "Thực thi và cập nhật issue", CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new Role { Id = 4, Name = "Viewer", Description = "Chỉ xem dữ liệu", CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime });
    }

    private static void SeedUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            CreateUser(1, "admin", "Nguyen Hoang Minh", "minh.admin@anphuc.vn"),
            CreateUser(2, "ngoc.hanh", "Tran Ngoc Hanh", "hanh.pm@anphuc.vn"),
            CreateUser(3, "minh.quan", "Le Minh Quan", "quan.dev@anphuc.vn"),
            CreateUser(4, "thu.lan", "Pham Thu Lan", "lan.viewer@anphuc.vn"),
            CreateUser(5, "thu.trang", "Nguyen Thu Trang", "trang.qa@anphuc.vn"),
            CreateUser(6, "quoc.phuc", "Do Quoc Phuc", "phuc.dev@anphuc.vn"),
            CreateUser(7, "ngoc.anh", "Vo Ngoc Anh", "anh.pm@baominh.vn"),
            CreateUser(8, "thu.hoa", "Bui Thu Hoa", "hoa.finance@cuulong.vn"),
            CreateUser(9, "duc.an", "Truong Duc An", "an.support@anphuc.vn"),
            CreateUser(10, "minh.hieu", "Ho Minh Hieu", "hieu.ops@baominh.vn"),
            CreateUser(11, "thuy.chi", "Dang Thuy Chi", "chi.ba@cuulong.vn"));

        modelBuilder.Entity<UserRole>().HasData(
            new { UserId = 1, RoleId = 1 },
            new { UserId = 2, RoleId = 2 },
            new { UserId = 3, RoleId = 3 },
            new { UserId = 4, RoleId = 4 },
            new { UserId = 5, RoleId = 3 },
            new { UserId = 6, RoleId = 3 },
            new { UserId = 7, RoleId = 2 },
            new { UserId = 8, RoleId = 4 },
            new { UserId = 9, RoleId = 3 },
            new { UserId = 10, RoleId = 4 },
            new { UserId = 11, RoleId = 4 });
    }

    private static void SeedProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>().HasData(
            new Project
            {
                Id = 1,
                Key = "APR",
                Name = "An Phuc Retail OMS",
                Description = "Hệ thống điều phối đơn hàng, tồn kho và giao vận cho chuỗi bán lẻ gia dụng tại Việt Nam.",
                Category = ProjectCategory.Software,
                BoardType = BoardType.Scrum,
                Url = "https://oms.anphuc.vn",
                IsActive = true,
                CreatedAtUtc = SeedTime,
                UpdatedAtUtc = SeedTime
            },
            new Project
            {
                Id = 2,
                Key = "BMG",
                Name = "Bao Minh Growth",
                Description = "Vận hành chiến dịch marketing đa kênh, quản lý lead và nội dung cho thị trường nội địa.",
                Category = ProjectCategory.Marketing,
                BoardType = BoardType.Kanban,
                Url = "https://growth.baominh.vn",
                IsActive = true,
                CreatedAtUtc = SeedTime,
                UpdatedAtUtc = SeedTime
            },
            new Project
            {
                Id = 3,
                Key = "CLF",
                Name = "Cuu Long Finance Ops",
                Description = "Theo dõi đối soát, kiểm soát rủi ro và báo cáo vận hành cho khối tài chính SME.",
                Category = ProjectCategory.Business,
                BoardType = BoardType.Scrum,
                Url = "https://ops.cuulongfinance.vn",
                IsActive = true,
                CreatedAtUtc = SeedTime,
                UpdatedAtUtc = SeedTime
            });
    }

    private static void SeedPermissionSchemes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PermissionScheme>().HasData(
            new PermissionScheme { Id = 1, ProjectId = 1, Name = PermissionDefaults.DefaultSchemeName, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new PermissionScheme { Id = 2, ProjectId = 2, Name = PermissionDefaults.DefaultSchemeName, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new PermissionScheme { Id = 3, ProjectId = 3, Name = PermissionDefaults.DefaultSchemeName, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime });

        modelBuilder.Entity<PermissionGrant>().HasData(
            PermissionDefaults.GetDefaultGrants()
                .SelectMany(grant => new[]
                {
                    new { PermissionSchemeId = 1, grant.Permission, grant.ProjectRole },
                    new { PermissionSchemeId = 2, grant.Permission, grant.ProjectRole },
                    new { PermissionSchemeId = 3, grant.Permission, grant.ProjectRole },
                })
                .ToArray());
    }

    private static void SeedProjectMemberships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectMember>().HasData(
            new { ProjectId = 1, UserId = 1, ProjectRole = ProjectRole.Admin, JoinedAtUtc = SeedTime.AddDays(-120) },
            new { ProjectId = 1, UserId = 2, ProjectRole = ProjectRole.ProjectManager, JoinedAtUtc = SeedTime.AddDays(-118) },
            new { ProjectId = 1, UserId = 3, ProjectRole = ProjectRole.Developer, JoinedAtUtc = SeedTime.AddDays(-110) },
            new { ProjectId = 1, UserId = 4, ProjectRole = ProjectRole.Viewer, JoinedAtUtc = SeedTime.AddDays(-90) },
            new { ProjectId = 1, UserId = 5, ProjectRole = ProjectRole.Developer, JoinedAtUtc = SeedTime.AddDays(-100) },
            new { ProjectId = 1, UserId = 6, ProjectRole = ProjectRole.Developer, JoinedAtUtc = SeedTime.AddDays(-96) },
            new { ProjectId = 1, UserId = 9, ProjectRole = ProjectRole.Developer, JoinedAtUtc = SeedTime.AddDays(-80) },
            new { ProjectId = 1, UserId = 11, ProjectRole = ProjectRole.Viewer, JoinedAtUtc = SeedTime.AddDays(-72) },

            new { ProjectId = 2, UserId = 1, ProjectRole = ProjectRole.Admin, JoinedAtUtc = SeedTime.AddDays(-90) },
            new { ProjectId = 2, UserId = 7, ProjectRole = ProjectRole.ProjectManager, JoinedAtUtc = SeedTime.AddDays(-88) },
            new { ProjectId = 2, UserId = 5, ProjectRole = ProjectRole.Developer, JoinedAtUtc = SeedTime.AddDays(-82) },
            new { ProjectId = 2, UserId = 4, ProjectRole = ProjectRole.Viewer, JoinedAtUtc = SeedTime.AddDays(-60) },
            new { ProjectId = 2, UserId = 8, ProjectRole = ProjectRole.Viewer, JoinedAtUtc = SeedTime.AddDays(-58) },
            new { ProjectId = 2, UserId = 10, ProjectRole = ProjectRole.Viewer, JoinedAtUtc = SeedTime.AddDays(-55) },

            new { ProjectId = 3, UserId = 1, ProjectRole = ProjectRole.Admin, JoinedAtUtc = SeedTime.AddDays(-75) },
            new { ProjectId = 3, UserId = 2, ProjectRole = ProjectRole.ProjectManager, JoinedAtUtc = SeedTime.AddDays(-73) },
            new { ProjectId = 3, UserId = 6, ProjectRole = ProjectRole.Developer, JoinedAtUtc = SeedTime.AddDays(-68) },
            new { ProjectId = 3, UserId = 8, ProjectRole = ProjectRole.Viewer, JoinedAtUtc = SeedTime.AddDays(-66) },
            new { ProjectId = 3, UserId = 9, ProjectRole = ProjectRole.Developer, JoinedAtUtc = SeedTime.AddDays(-62) },
            new { ProjectId = 3, UserId = 11, ProjectRole = ProjectRole.Viewer, JoinedAtUtc = SeedTime.AddDays(-60) });
    }

    private static void SeedWorkflows(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowDefinition>().HasData(
            new WorkflowDefinition { Id = 1, ProjectId = 1, Name = "APR Default Workflow", IsDefault = true, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowDefinition { Id = 2, ProjectId = 2, Name = "BMG Content Workflow", IsDefault = true, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowDefinition { Id = 3, ProjectId = 3, Name = "CLF Control Workflow", IsDefault = true, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime });

        modelBuilder.Entity<WorkflowStatus>().HasData(
            new WorkflowStatus { Id = 1, WorkflowDefinitionId = 1, Name = "Backlog", Color = "#42526E", Category = StatusCategory.ToDo, DisplayOrder = 1, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 2, WorkflowDefinitionId = 1, Name = "Selected", Color = "#4C9AFF", Category = StatusCategory.ToDo, DisplayOrder = 2, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 3, WorkflowDefinitionId = 1, Name = "In Progress", Color = "#0052CC", Category = StatusCategory.InProgress, DisplayOrder = 3, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 4, WorkflowDefinitionId = 1, Name = "Ready for QA", Color = "#6554C0", Category = StatusCategory.InProgress, DisplayOrder = 4, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 5, WorkflowDefinitionId = 1, Name = "Done", Color = "#36B37E", Category = StatusCategory.Done, DisplayOrder = 5, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },

            new WorkflowStatus { Id = 6, WorkflowDefinitionId = 2, Name = "To Do", Color = "#42526E", Category = StatusCategory.ToDo, DisplayOrder = 1, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 7, WorkflowDefinitionId = 2, Name = "Doing", Color = "#0052CC", Category = StatusCategory.InProgress, DisplayOrder = 2, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 8, WorkflowDefinitionId = 2, Name = "Cho duyet noi dung", Color = "#6554C0", Category = StatusCategory.InProgress, DisplayOrder = 3, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 9, WorkflowDefinitionId = 2, Name = "Done", Color = "#36B37E", Category = StatusCategory.Done, DisplayOrder = 4, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },

            new WorkflowStatus { Id = 10, WorkflowDefinitionId = 3, Name = "Backlog", Color = "#42526E", Category = StatusCategory.ToDo, DisplayOrder = 1, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 11, WorkflowDefinitionId = 3, Name = "Dang phan tich", Color = "#4C9AFF", Category = StatusCategory.ToDo, DisplayOrder = 2, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 12, WorkflowDefinitionId = 3, Name = "Dang trien khai", Color = "#0052CC", Category = StatusCategory.InProgress, DisplayOrder = 3, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 13, WorkflowDefinitionId = 3, Name = "Cho doi soat", Color = "#6554C0", Category = StatusCategory.InProgress, DisplayOrder = 4, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new WorkflowStatus { Id = 14, WorkflowDefinitionId = 3, Name = "Done", Color = "#36B37E", Category = StatusCategory.Done, DisplayOrder = 5, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime });

        var transitions = new List<object>();
        var transitionRoles = new List<object>();
        var transitionId = 1;
        transitionId = AddTransitions(transitions, transitionRoles, transitionId, 1, [1, 2, 3, 4, 5]);
        transitionId = AddTransitions(transitions, transitionRoles, transitionId, 2, [6, 7, 8, 9]);
        AddTransitions(transitions, transitionRoles, transitionId, 3, [10, 11, 12, 13, 14]);

        modelBuilder.Entity<WorkflowTransition>().HasData(transitions.ToArray());
        modelBuilder.Entity("WorkflowTransitionRole").HasData(transitionRoles.ToArray());
    }

    private static void SeedBoardColumns(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoardColumn>().HasData(
            new { Id = 1, ProjectId = 1, Name = "Backlog", WorkflowStatusId = 1, DisplayOrder = 1, WipLimit = (int?)null, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 2, ProjectId = 1, Name = "Selected", WorkflowStatusId = 2, DisplayOrder = 2, WipLimit = (int?)null, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 3, ProjectId = 1, Name = "In Progress", WorkflowStatusId = 3, DisplayOrder = 3, WipLimit = (int?)6, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 4, ProjectId = 1, Name = "Ready for QA", WorkflowStatusId = 4, DisplayOrder = 4, WipLimit = (int?)4, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 5, ProjectId = 1, Name = "Done", WorkflowStatusId = 5, DisplayOrder = 5, WipLimit = (int?)null, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },

            new { Id = 6, ProjectId = 2, Name = "To Do", WorkflowStatusId = 6, DisplayOrder = 1, WipLimit = (int?)null, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 7, ProjectId = 2, Name = "Doing", WorkflowStatusId = 7, DisplayOrder = 2, WipLimit = (int?)8, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 8, ProjectId = 2, Name = "Cho duyet noi dung", WorkflowStatusId = 8, DisplayOrder = 3, WipLimit = (int?)5, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 9, ProjectId = 2, Name = "Done", WorkflowStatusId = 9, DisplayOrder = 4, WipLimit = (int?)null, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },

            new { Id = 10, ProjectId = 3, Name = "Backlog", WorkflowStatusId = 10, DisplayOrder = 1, WipLimit = (int?)null, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 11, ProjectId = 3, Name = "Dang phan tich", WorkflowStatusId = 11, DisplayOrder = 2, WipLimit = (int?)5, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 12, ProjectId = 3, Name = "Dang trien khai", WorkflowStatusId = 12, DisplayOrder = 3, WipLimit = (int?)4, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 13, ProjectId = 3, Name = "Cho doi soat", WorkflowStatusId = 13, DisplayOrder = 4, WipLimit = (int?)3, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 14, ProjectId = 3, Name = "Done", WorkflowStatusId = 14, DisplayOrder = 5, WipLimit = (int?)null, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime });
    }

    private static void SeedLabels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Label>().HasData(
            CreateLabel(1, 1, "Thanh toan", "#FF8B00"),
            CreateLabel(2, 1, "Dong bo kho", "#0052CC"),
            CreateLabel(3, 1, "Van hanh", "#36B37E"),
            CreateLabel(4, 1, "P1", "#DE350B"),
            CreateLabel(5, 1, "Ha Noi", "#6554C0"),

            CreateLabel(6, 2, "Landing Page", "#0052CC"),
            CreateLabel(7, 2, "CRM", "#36B37E"),
            CreateLabel(8, 2, "Ads", "#FF8B00"),
            CreateLabel(9, 2, "Noi dung", "#6554C0"),
            CreateLabel(10, 2, "Lead", "#00B8D9"),

            CreateLabel(11, 3, "Doi soat", "#0052CC"),
            CreateLabel(12, 3, "Bao cao", "#36B37E"),
            CreateLabel(13, 3, "Rui ro", "#DE350B"),
            CreateLabel(14, 3, "SME", "#6554C0"),
            CreateLabel(15, 3, "Kiem toan", "#FF8B00"));
    }

    private static void SeedComponents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Component>().HasData(
            CreateComponent(1, 1, "OMS Backend", "Dong bo don hang, ton kho va dieu phoi xu ly don.", 3),
            CreateComponent(2, 1, "WinForms Desktop", "Giao dien van hanh cho kho, CSKH va quan ly khu vuc.", 6),
            CreateComponent(3, 1, "Tich hop POS", "Ket noi KiotViet, Sapo va may POS tai cua hang.", 9),
            CreateComponent(4, 1, "Kho van", "Rule chia don, SLA giao hang va goi y kho xuat.", 2),

            CreateComponent(5, 2, "Creative", "Banner, social post va video ngan cho chien dich.", 7),
            CreateComponent(6, 2, "Performance Marketing", "Quan ly ngan sach ads va toi uu CPL.", 5),
            CreateComponent(7, 2, "CRM Automation", "Dong bo lead, scoring va nuoi duong qua email.", 7),

            CreateComponent(8, 3, "Doi soat giao dich", "Nghiep vu doi chieu file sao ke va Core Banking.", 2),
            CreateComponent(9, 3, "Bao cao quan tri", "Tong hop dashboard cho dieu hanh va kiem soat noi bo.", 6),
            CreateComponent(10, 3, "Kiem soat rui ro", "Canh bao sai lech va xu ly giao dich treo.", 9));
    }

    private static void SeedVersions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectVersion>().HasData(
            CreateVersion(1, 1, "Pilot 10 cua hang", "Moc van hanh cho cum Ha Noi va Hai Phong.", new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Utc), true),
            CreateVersion(2, 1, "Rollout mien Nam", "On dinh luong don va ton kho cho 48 cua hang mien Nam.", new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc), false),
            CreateVersion(3, 1, "Toi uu giao van", "Giam don tre SLA va dieu phoi kho xuat theo khu vuc.", new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), false),

            CreateVersion(4, 2, "Chien dich He 2026", "Dot ra mat cho nhom san pham loc nuoc gia dinh.", new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc), false),
            CreateVersion(5, 2, "Remarketing Q2", "Day lead cu quay lai CRM va remarketing da kenh.", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), false),

            CreateVersion(6, 3, "Doi soat quy 1", "Khoa du lieu va bao cao doi soat cho Q1/2026.", new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc), true),
            CreateVersion(7, 3, "Kiem soat thang 5", "Bo dashboard canh bao cho khoi SME va tai chinh.", new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc), false));
    }

    private static void SeedSprints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sprint>().HasData(
            new { Id = 1, ProjectId = 1, Name = "Sprint 09 - go live Ha Noi", Goal = "Chot cac hang muc can thiet truoc rollout 10 cua hang dau tien.", StartDate = new DateOnly(2026, 3, 16), EndDate = new DateOnly(2026, 3, 29), State = SprintState.Closed, ClosedAtUtc = new DateTime(2026, 3, 29, 11, 0, 0, DateTimeKind.Utc), IsDeleted = false, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 2, ProjectId = 1, Name = "Sprint 10 - on dinh van hanh mien Nam", Goal = "Xu ly loi thanh toan, ton kho va dashboard dieu phoi sau khi mo rong 48 cua hang.", StartDate = new DateOnly(2026, 4, 13), EndDate = new DateOnly(2026, 4, 26), State = SprintState.Active, ClosedAtUtc = (DateTime?)null, IsDeleted = false, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 3, ProjectId = 1, Name = "Sprint 11 - toi uu giao van", Goal = "Giam don tre SLA o noi thanh Ha Noi va TP HCM.", StartDate = new DateOnly(2026, 4, 27), EndDate = new DateOnly(2026, 5, 10), State = SprintState.Planned, ClosedAtUtc = (DateTime?)null, IsDeleted = false, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },

            new { Id = 4, ProjectId = 3, Name = "Sprint 07 - doi soat quy 1", Goal = "Dong bo bao cao doi soat va khoa ky thang 03/2026.", StartDate = new DateOnly(2026, 4, 1), EndDate = new DateOnly(2026, 4, 14), State = SprintState.Closed, ClosedAtUtc = new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc), IsDeleted = false, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 5, ProjectId = 3, Name = "Sprint 08 - bao cao kiem soat noi bo", Goal = "Chot dashboard canh bao lech doanh so va giao dich treo.", StartDate = new DateOnly(2026, 4, 21), EndDate = new DateOnly(2026, 5, 4), State = SprintState.Planned, ClosedAtUtc = (DateTime?)null, IsDeleted = false, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime });
    }

    private static void SeedIssues(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Issue>().HasData(
            new { Id = 1, ProjectId = 1, SprintId = (int?)2, FixVersionId = (int?)2, IssueKey = "APR-1", Title = "Chuan hoa dashboard dieu phoi don hang cho 48 cua hang mien Nam", DescriptionHtml = "Tong hop tinh trang don, ton kho va SLA giao hang cho van hanh khu vuc.", DescriptionText = "Tong hop tinh trang don, ton kho va SLA giao hang cho van hanh khu vuc.", Type = IssueType.Epic, WorkflowStatusId = 3, Priority = IssuePriority.High, ReporterId = 2, CreatedById = 1, EstimateHours = 40, TimeSpentHours = 18, TimeRemainingHours = 22, StoryPoints = 13, StartDate = new DateOnly(2026, 4, 10), DueDate = new DateOnly(2026, 5, 8), BoardPosition = 1m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-25), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 2, ProjectId = 1, SprintId = (int?)2, FixVersionId = (int?)2, IssueKey = "APR-2", Title = "Loc don theo kenh ban va trang thai thanh toan", DescriptionHtml = "Cho phep van hanh loc nhanh theo POS, website, sàn va tinh trang thanh toan.", DescriptionText = "Cho phep van hanh loc nhanh theo POS, website, san va tinh trang thanh toan.", Type = IssueType.Story, WorkflowStatusId = 3, Priority = IssuePriority.High, ReporterId = 2, CreatedById = 3, EstimateHours = 20, TimeSpentHours = 11, TimeRemainingHours = 9, StoryPoints = 5, StartDate = new DateOnly(2026, 4, 14), DueDate = new DateOnly(2026, 4, 24), BoardPosition = 2m, ParentIssueId = (int?)1, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-19), UpdatedAtUtc = SeedTime.AddHours(-6) },
            new { Id = 3, ProjectId = 1, SprintId = (int?)2, FixVersionId = (int?)2, IssueKey = "APR-3", Title = "Dong bo ton kho tu POS ve OMS moi 15 phut", DescriptionHtml = "Dong bo du lieu ton kho giua cua hang va he thong trung tam de giam don vuot ton.", DescriptionText = "Dong bo du lieu ton kho giua cua hang va he thong trung tam de giam don vuot ton.", Type = IssueType.Task, WorkflowStatusId = 4, Priority = IssuePriority.High, ReporterId = 2, CreatedById = 2, EstimateHours = 16, TimeSpentHours = 10, TimeRemainingHours = 6, StoryPoints = 3, StartDate = new DateOnly(2026, 4, 13), DueDate = new DateOnly(2026, 4, 22), BoardPosition = 1m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-18), UpdatedAtUtc = SeedTime.AddHours(-5) },
            new { Id = 4, ProjectId = 1, SprintId = (int?)2, FixVersionId = (int?)2, IssueKey = "APR-4", Title = "Don hoan tu VNPay chua tra trang thai ve ERP", DescriptionHtml = "Mot so don da hoan tien thanh cong nhung ERP van giu trang thai dang thanh toan.", DescriptionText = "Mot so don da hoan tien thanh cong nhung ERP van giu trang thai dang thanh toan.", Type = IssueType.Bug, WorkflowStatusId = 3, Priority = IssuePriority.High, ReporterId = 9, CreatedById = 9, EstimateHours = 12, TimeSpentHours = 7, TimeRemainingHours = 5, StoryPoints = 3, StartDate = new DateOnly(2026, 4, 15), DueDate = new DateOnly(2026, 4, 21), BoardPosition = 3m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-12), UpdatedAtUtc = SeedTime.AddHours(-2) },
            new { Id = 5, ProjectId = 1, SprintId = (int?)2, FixVersionId = (int?)3, IssueKey = "APR-5", Title = "Tu dong goi y kho xuat gan nhat cho don giao nhanh", DescriptionHtml = "Uu tien kho xuat theo khoang cach, ton kha dung va SLA cam ket.", DescriptionText = "Uu tien kho xuat theo khoang cach, ton kha dung va SLA cam ket.", Type = IssueType.Story, WorkflowStatusId = 2, Priority = IssuePriority.Medium, ReporterId = 2, CreatedById = 1, EstimateHours = 24, TimeSpentHours = 4, TimeRemainingHours = 20, StoryPoints = 8, StartDate = new DateOnly(2026, 4, 18), DueDate = new DateOnly(2026, 5, 6), BoardPosition = 4m, ParentIssueId = (int?)1, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-9), UpdatedAtUtc = SeedTime.AddHours(-7) },
            new { Id = 6, ProjectId = 1, SprintId = (int?)2, FixVersionId = (int?)3, IssueKey = "APR-6", Title = "Viet rule tinh khoang cach kho theo quan huyen", DescriptionHtml = "Tinh diem uu tien kho dua tren quan huyen giao hang va thoi gian cat-off.", DescriptionText = "Tinh diem uu tien kho dua tren quan huyen giao hang va thoi gian cat-off.", Type = IssueType.Subtask, WorkflowStatusId = 3, Priority = IssuePriority.Medium, ReporterId = 6, CreatedById = 6, EstimateHours = 8, TimeSpentHours = 3, TimeRemainingHours = 5, StoryPoints = 2, StartDate = new DateOnly(2026, 4, 18), DueDate = new DateOnly(2026, 4, 23), BoardPosition = 5m, ParentIssueId = (int?)5, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-8), UpdatedAtUtc = SeedTime.AddHours(-10) },
            new { Id = 7, ProjectId = 1, SprintId = (int?)1, FixVersionId = (int?)1, IssueKey = "APR-7", Title = "Chuan hoa mapping ma cua hang tu KiotViet", DescriptionHtml = "Dong nhat ma cua hang de bao cao doanh thu va ton kho khong bi tach dong.", DescriptionText = "Dong nhat ma cua hang de bao cao doanh thu va ton kho khong bi tach dong.", Type = IssueType.Task, WorkflowStatusId = 5, Priority = IssuePriority.Medium, ReporterId = 2, CreatedById = 3, EstimateHours = 10, TimeSpentHours = 10, TimeRemainingHours = 0, StoryPoints = 3, StartDate = new DateOnly(2026, 3, 20), DueDate = new DateOnly(2026, 3, 27), BoardPosition = 1m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-31), UpdatedAtUtc = SeedTime.AddDays(-22) },
            new { Id = 8, ProjectId = 1, SprintId = (int?)3, FixVersionId = (int?)3, IssueKey = "APR-8", Title = "Bao cao doanh thu theo ca lech 1 ngay khi qua 0h", DescriptionHtml = "Ca ban dem hien doanh thu sang ngay hom sau khi dong ca sau 00:00.", DescriptionText = "Ca ban dem hien doanh thu sang ngay hom sau khi dong ca sau 00:00.", Type = IssueType.Bug, WorkflowStatusId = 1, Priority = IssuePriority.Highest, ReporterId = 4, CreatedById = 2, EstimateHours = 6, TimeSpentHours = 0, TimeRemainingHours = 6, StoryPoints = 2, StartDate = (DateOnly?)null, DueDate = new DateOnly(2026, 4, 30), BoardPosition = 2m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-4), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 9, ProjectId = 1, SprintId = (int?)3, FixVersionId = (int?)3, IssueKey = "APR-9", Title = "Toi uu luong giao hang noi thanh Ha Noi", DescriptionHtml = "Danh cho don giao nhanh trong 2h va giam don tre SLA gio cao diem.", DescriptionText = "Danh cho don giao nhanh trong 2h va giam don tre SLA gio cao diem.", Type = IssueType.Epic, WorkflowStatusId = 1, Priority = IssuePriority.Medium, ReporterId = 2, CreatedById = 1, EstimateHours = 32, TimeSpentHours = 0, TimeRemainingHours = 32, StoryPoints = 8, StartDate = new DateOnly(2026, 4, 27), DueDate = new DateOnly(2026, 5, 15), BoardPosition = 3m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-3), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 10, ProjectId = 1, SprintId = (int?)3, FixVersionId = (int?)3, IssueKey = "APR-10", Title = "Canh bao don co nguy co tre SLA trong 30 phut", DescriptionHtml = "Thong bao som de kho va CSKH can thiep truoc khi don vuot cam ket.", DescriptionText = "Thong bao som de kho va CSKH can thiep truoc khi don vuot cam ket.", Type = IssueType.Story, WorkflowStatusId = 1, Priority = IssuePriority.High, ReporterId = 2, CreatedById = 9, EstimateHours = 14, TimeSpentHours = 0, TimeRemainingHours = 14, StoryPoints = 5, StartDate = new DateOnly(2026, 4, 27), DueDate = new DateOnly(2026, 5, 3), BoardPosition = 4m, ParentIssueId = (int?)9, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-2), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 11, ProjectId = 1, SprintId = (int?)1, FixVersionId = (int?)1, IssueKey = "APR-11", Title = "Them phan quyen xem roadmap cho khoi van hanh", DescriptionHtml = "Cap quyen cho truong ca va giam sat kho de theo doi moc rollout.", DescriptionText = "Cap quyen cho truong ca va giam sat kho de theo doi moc rollout.", Type = IssueType.Task, WorkflowStatusId = 5, Priority = IssuePriority.Low, ReporterId = 2, CreatedById = 1, EstimateHours = 4, TimeSpentHours = 4, TimeRemainingHours = 0, StoryPoints = 1, StartDate = new DateOnly(2026, 3, 22), DueDate = new DateOnly(2026, 3, 24), BoardPosition = 2m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-30), UpdatedAtUtc = SeedTime.AddDays(-25) },
            new { Id = 12, ProjectId = 1, SprintId = (int?)2, FixVersionId = (int?)2, IssueKey = "APR-12", Title = "Bo sung chi muc tim kiem cho bo loc trang thai", DescriptionHtml = "Giam thoi gian tra ket qua tren danh sach don khi peak traffic.", DescriptionText = "Giam thoi gian tra ket qua tren danh sach don khi peak traffic.", Type = IssueType.Subtask, WorkflowStatusId = 4, Priority = IssuePriority.Medium, ReporterId = 3, CreatedById = 3, EstimateHours = 6, TimeSpentHours = 4, TimeRemainingHours = 2, StoryPoints = 1, StartDate = new DateOnly(2026, 4, 16), DueDate = new DateOnly(2026, 4, 22), BoardPosition = 6m, ParentIssueId = (int?)2, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-11), UpdatedAtUtc = SeedTime.AddHours(-4) },

            new { Id = 13, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)4, IssueKey = "BMG-1", Title = "Chien dich He 2026 cho dong may loc nuoc gia dinh", DescriptionHtml = "Epic bao trum toan bo hoat dong thu lead, remarketing va creative cho dot ban hang mua he.", DescriptionText = "Epic bao trum toan bo hoat dong thu lead, remarketing va creative cho dot ban hang mua he.", Type = IssueType.Epic, WorkflowStatusId = 7, Priority = IssuePriority.High, ReporterId = 7, CreatedById = 7, EstimateHours = 30, TimeSpentHours = 12, TimeRemainingHours = 18, StoryPoints = 13, StartDate = new DateOnly(2026, 4, 8), DueDate = new DateOnly(2026, 5, 15), BoardPosition = 1m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-14), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 14, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)4, IssueKey = "BMG-2", Title = "Landing page thu lead theo tinh thanh", DescriptionHtml = "Can hoi dap nhanh theo khu vuc de chuyen lead ve dai ly phu trach.", DescriptionText = "Can hoi dap nhanh theo khu vuc de chuyen lead ve dai ly phu trach.", Type = IssueType.Story, WorkflowStatusId = 7, Priority = IssuePriority.High, ReporterId = 7, CreatedById = 5, EstimateHours = 18, TimeSpentHours = 8, TimeRemainingHours = 10, StoryPoints = 5, StartDate = new DateOnly(2026, 4, 10), DueDate = new DateOnly(2026, 4, 25), BoardPosition = 2m, ParentIssueId = (int?)13, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-13), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 15, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)4, IssueKey = "BMG-3", Title = "Duyet bo banner social cho mien Tay", DescriptionHtml = "Can bo banner rieng cho dai ly tinh va thiet bi loc nuoc gia dinh.", DescriptionText = "Can bo banner rieng cho dai ly tinh va thiet bi loc nuoc gia dinh.", Type = IssueType.Task, WorkflowStatusId = 8, Priority = IssuePriority.Medium, ReporterId = 7, CreatedById = 7, EstimateHours = 10, TimeSpentHours = 7, TimeRemainingHours = 3, StoryPoints = 3, StartDate = new DateOnly(2026, 4, 11), DueDate = new DateOnly(2026, 4, 22), BoardPosition = 3m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-12), UpdatedAtUtc = SeedTime.AddHours(-12) },
            new { Id = 16, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)4, IssueKey = "BMG-4", Title = "Form dang ky lead khong luu nguon quang cao", DescriptionHtml = "Mat tham so UTM khi nguoi dung submit tren mobile Safari.", DescriptionText = "Mat tham so UTM khi nguoi dung submit tren mobile Safari.", Type = IssueType.Bug, WorkflowStatusId = 6, Priority = IssuePriority.High, ReporterId = 5, CreatedById = 5, EstimateHours = 6, TimeSpentHours = 1, TimeRemainingHours = 5, StoryPoints = 2, StartDate = new DateOnly(2026, 4, 19), DueDate = new DateOnly(2026, 4, 23), BoardPosition = 4m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-1), UpdatedAtUtc = SeedTime.AddHours(-8) },
            new { Id = 17, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)4, IssueKey = "BMG-5", Title = "Chuan hoa naming cho UTM Facebook va TikTok", DescriptionHtml = "Thong nhat naming de dashboard CPL va CRM khong bi tach nhom sai.", DescriptionText = "Thong nhat naming de dashboard CPL va CRM khong bi tach nhom sai.", Type = IssueType.Task, WorkflowStatusId = 9, Priority = IssuePriority.Low, ReporterId = 7, CreatedById = 7, EstimateHours = 4, TimeSpentHours = 4, TimeRemainingHours = 0, StoryPoints = 1, StartDate = new DateOnly(2026, 4, 5), DueDate = new DateOnly(2026, 4, 8), BoardPosition = 1m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-15), UpdatedAtUtc = SeedTime.AddDays(-10) },
            new { Id = 18, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)5, IssueKey = "BMG-6", Title = "Ket noi lead sang CRM trong 5 phut", DescriptionHtml = "Dong bo lead moi ve CRM de telesales goi trong khung gio vang.", DescriptionText = "Dong bo lead moi ve CRM de telesales goi trong khung gio vang.", Type = IssueType.Story, WorkflowStatusId = 6, Priority = IssuePriority.High, ReporterId = 7, CreatedById = 5, EstimateHours = 16, TimeSpentHours = 0, TimeRemainingHours = 16, StoryPoints = 5, StartDate = new DateOnly(2026, 4, 21), DueDate = new DateOnly(2026, 5, 3), BoardPosition = 5m, ParentIssueId = (int?)13, IsDeleted = false, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 19, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)4, IssueKey = "BMG-7", Title = "Thiet lap dashboard CPL theo dai ly", DescriptionHtml = "Cho phep loc theo vung, ngan sach va chat luong lead de toi uu ads hang ngay.", DescriptionText = "Cho phep loc theo vung, ngan sach va chat luong lead de toi uu ads hang ngay.", Type = IssueType.Task, WorkflowStatusId = 7, Priority = IssuePriority.Medium, ReporterId = 7, CreatedById = 10, EstimateHours = 12, TimeSpentHours = 5, TimeRemainingHours = 7, StoryPoints = 3, StartDate = new DateOnly(2026, 4, 14), DueDate = new DateOnly(2026, 4, 28), BoardPosition = 6m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-6), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 20, ProjectId = 2, SprintId = (int?)null, FixVersionId = (int?)4, IssueKey = "BMG-8", Title = "Gui email automation bi loi dau tieng Viet", DescriptionHtml = "Template email mat dau o mot so may Android cu khi mo qua Gmail app.", DescriptionText = "Template email mat dau o mot so may Android cu khi mo qua Gmail app.", Type = IssueType.Bug, WorkflowStatusId = 9, Priority = IssuePriority.Medium, ReporterId = 5, CreatedById = 5, EstimateHours = 5, TimeSpentHours = 5, TimeRemainingHours = 0, StoryPoints = 1, StartDate = new DateOnly(2026, 4, 7), DueDate = new DateOnly(2026, 4, 9), BoardPosition = 2m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-16), UpdatedAtUtc = SeedTime.AddDays(-9) },

            new { Id = 21, ProjectId = 3, SprintId = (int?)5, FixVersionId = (int?)7, IssueKey = "CLF-1", Title = "Tu dong hoa doi soat sao ke cho khoi SME", DescriptionHtml = "Epic tap trung dong bo sao ke ngan hang, doi chieu giao dich va canh bao sai lech.", DescriptionText = "Epic tap trung dong bo sao ke ngan hang, doi chieu giao dich va canh bao sai lech.", Type = IssueType.Epic, WorkflowStatusId = 11, Priority = IssuePriority.High, ReporterId = 2, CreatedById = 1, EstimateHours = 36, TimeSpentHours = 6, TimeRemainingHours = 30, StoryPoints = 13, StartDate = new DateOnly(2026, 4, 18), DueDate = new DateOnly(2026, 5, 18), BoardPosition = 1m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-7), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 22, ProjectId = 3, SprintId = (int?)5, FixVersionId = (int?)7, IssueKey = "CLF-2", Title = "Xuat bao cao doi soat theo chi nhanh va RM", DescriptionHtml = "Cho phep khoi tai chinh va kinh doanh xem lech doi soat theo don vi phu trach.", DescriptionText = "Cho phep khoi tai chinh va kinh doanh xem lech doi soat theo don vi phu trach.", Type = IssueType.Story, WorkflowStatusId = 12, Priority = IssuePriority.High, ReporterId = 2, CreatedById = 6, EstimateHours = 18, TimeSpentHours = 8, TimeRemainingHours = 10, StoryPoints = 5, StartDate = new DateOnly(2026, 4, 21), DueDate = new DateOnly(2026, 4, 30), BoardPosition = 2m, ParentIssueId = (int?)21, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-5), UpdatedAtUtc = SeedTime.AddHours(-9) },
            new { Id = 23, ProjectId = 3, SprintId = (int?)5, FixVersionId = (int?)7, IssueKey = "CLF-3", Title = "Bo sung buoc kiem tra giao dich treo cuoi ngay", DescriptionHtml = "Danh dau giao dich chua doi duoc so tham chieu sau 17h30 de xu ly truoc 9h sang hom sau.", DescriptionText = "Danh dau giao dich chua doi duoc so tham chieu sau 17h30 de xu ly truoc 9h sang hom sau.", Type = IssueType.Task, WorkflowStatusId = 13, Priority = IssuePriority.High, ReporterId = 8, CreatedById = 9, EstimateHours = 10, TimeSpentHours = 7, TimeRemainingHours = 3, StoryPoints = 3, StartDate = new DateOnly(2026, 4, 21), DueDate = new DateOnly(2026, 4, 24), BoardPosition = 3m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-4), UpdatedAtUtc = SeedTime.AddHours(-4) },
            new { Id = 24, ProjectId = 3, SprintId = (int?)null, FixVersionId = (int?)7, IssueKey = "CLF-4", Title = "So du dau ky bi am khi import CSV tu Core", DescriptionHtml = "Xuat hien o mot so file tu chi nhanh Can Tho khi cot number co dau phay ngan cach.", DescriptionText = "Xuat hien o mot so file tu chi nhanh Can Tho khi cot number co dau phay ngan cach.", Type = IssueType.Bug, WorkflowStatusId = 10, Priority = IssuePriority.Highest, ReporterId = 8, CreatedById = 8, EstimateHours = 6, TimeSpentHours = 0, TimeRemainingHours = 6, StoryPoints = 2, StartDate = (DateOnly?)null, DueDate = new DateOnly(2026, 4, 23), BoardPosition = 4m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-1), UpdatedAtUtc = SeedTime.AddHours(-3) },
            new { Id = 25, ProjectId = 3, SprintId = (int?)4, FixVersionId = (int?)6, IssueKey = "CLF-5", Title = "Khoa ky doi soat thang 03/2026", DescriptionHtml = "Hoan tat khoa du lieu thang 03 va chot bien ban doi soat cho khoi SME.", DescriptionText = "Hoan tat khoa du lieu thang 03 va chot bien ban doi soat cho khoi SME.", Type = IssueType.Task, WorkflowStatusId = 14, Priority = IssuePriority.Medium, ReporterId = 2, CreatedById = 2, EstimateHours = 5, TimeSpentHours = 5, TimeRemainingHours = 0, StoryPoints = 1, StartDate = new DateOnly(2026, 4, 11), DueDate = new DateOnly(2026, 4, 14), BoardPosition = 1m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-12), UpdatedAtUtc = SeedTime.AddDays(-6) },
            new { Id = 26, ProjectId = 3, SprintId = (int?)null, FixVersionId = (int?)7, IssueKey = "CLF-6", Title = "Canh bao lech doanh so giua Core va MIS", DescriptionHtml = "Sinh canh bao theo nguong tuy chi nhanh va RM phu trach de xu ly trong ngay.", DescriptionText = "Sinh canh bao theo nguong tuy chi nhanh va RM phu trach de xu ly trong ngay.", Type = IssueType.Story, WorkflowStatusId = 10, Priority = IssuePriority.High, ReporterId = 2, CreatedById = 9, EstimateHours = 14, TimeSpentHours = 0, TimeRemainingHours = 14, StoryPoints = 5, StartDate = new DateOnly(2026, 4, 25), DueDate = new DateOnly(2026, 5, 5), BoardPosition = 5m, ParentIssueId = (int?)21, IsDeleted = false, CreatedAtUtc = SeedTime, UpdatedAtUtc = SeedTime },
            new { Id = 27, ProjectId = 3, SprintId = (int?)4, FixVersionId = (int?)6, IssueKey = "CLF-7", Title = "Chuan hoa mau email gui kiem toan noi bo", DescriptionHtml = "Thong nhat bien ban gui tu he thong doi soat de phuc vu kiem toan thang.", DescriptionText = "Thong nhat bien ban gui tu he thong doi soat de phuc vu kiem toan thang.", Type = IssueType.Task, WorkflowStatusId = 14, Priority = IssuePriority.Low, ReporterId = 11, CreatedById = 2, EstimateHours = 4, TimeSpentHours = 4, TimeRemainingHours = 0, StoryPoints = 1, StartDate = new DateOnly(2026, 4, 8), DueDate = new DateOnly(2026, 4, 10), BoardPosition = 2m, ParentIssueId = (int?)null, IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-14), UpdatedAtUtc = SeedTime.AddDays(-10) });
    }

    private static void SeedIssueAssignments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IssueAssignee>().HasData(
            new { IssueId = 1, UserId = 2, AssignedAtUtc = SeedTime.AddDays(-25) },
            new { IssueId = 2, UserId = 3, AssignedAtUtc = SeedTime.AddDays(-19) },
            new { IssueId = 3, UserId = 6, AssignedAtUtc = SeedTime.AddDays(-18) },
            new { IssueId = 4, UserId = 9, AssignedAtUtc = SeedTime.AddDays(-12) },
            new { IssueId = 5, UserId = 3, AssignedAtUtc = SeedTime.AddDays(-9) },
            new { IssueId = 5, UserId = 6, AssignedAtUtc = SeedTime.AddDays(-9) },
            new { IssueId = 6, UserId = 6, AssignedAtUtc = SeedTime.AddDays(-8) },
            new { IssueId = 7, UserId = 9, AssignedAtUtc = SeedTime.AddDays(-31) },
            new { IssueId = 8, UserId = 5, AssignedAtUtc = SeedTime.AddDays(-4) },
            new { IssueId = 10, UserId = 9, AssignedAtUtc = SeedTime.AddDays(-2) },
            new { IssueId = 12, UserId = 3, AssignedAtUtc = SeedTime.AddDays(-11) },

            new { IssueId = 13, UserId = 7, AssignedAtUtc = SeedTime.AddDays(-14) },
            new { IssueId = 14, UserId = 5, AssignedAtUtc = SeedTime.AddDays(-13) },
            new { IssueId = 15, UserId = 7, AssignedAtUtc = SeedTime.AddDays(-12) },
            new { IssueId = 16, UserId = 5, AssignedAtUtc = SeedTime.AddDays(-1) },
            new { IssueId = 18, UserId = 5, AssignedAtUtc = SeedTime },
            new { IssueId = 19, UserId = 10, AssignedAtUtc = SeedTime.AddDays(-6) },

            new { IssueId = 21, UserId = 2, AssignedAtUtc = SeedTime.AddDays(-7) },
            new { IssueId = 22, UserId = 6, AssignedAtUtc = SeedTime.AddDays(-5) },
            new { IssueId = 23, UserId = 9, AssignedAtUtc = SeedTime.AddDays(-4) },
            new { IssueId = 24, UserId = 6, AssignedAtUtc = SeedTime.AddDays(-1) },
            new { IssueId = 26, UserId = 9, AssignedAtUtc = SeedTime },
            new { IssueId = 27, UserId = 11, AssignedAtUtc = SeedTime.AddDays(-14) });
    }

    private static void SeedIssueLabels(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IssueLabel>().HasData(
            new { IssueId = 1, LabelId = 3 },
            new { IssueId = 2, LabelId = 1 },
            new { IssueId = 2, LabelId = 3 },
            new { IssueId = 3, LabelId = 2 },
            new { IssueId = 3, LabelId = 3 },
            new { IssueId = 4, LabelId = 1 },
            new { IssueId = 4, LabelId = 4 },
            new { IssueId = 5, LabelId = 2 },
            new { IssueId = 6, LabelId = 5 },
            new { IssueId = 8, LabelId = 4 },
            new { IssueId = 9, LabelId = 5 },
            new { IssueId = 10, LabelId = 3 },
            new { IssueId = 12, LabelId = 2 },

            new { IssueId = 13, LabelId = 8 },
            new { IssueId = 13, LabelId = 10 },
            new { IssueId = 14, LabelId = 6 },
            new { IssueId = 14, LabelId = 10 },
            new { IssueId = 15, LabelId = 9 },
            new { IssueId = 16, LabelId = 6 },
            new { IssueId = 16, LabelId = 8 },
            new { IssueId = 18, LabelId = 7 },
            new { IssueId = 19, LabelId = 8 },
            new { IssueId = 20, LabelId = 9 },

            new { IssueId = 21, LabelId = 11 },
            new { IssueId = 21, LabelId = 14 },
            new { IssueId = 22, LabelId = 12 },
            new { IssueId = 22, LabelId = 14 },
            new { IssueId = 23, LabelId = 11 },
            new { IssueId = 23, LabelId = 13 },
            new { IssueId = 24, LabelId = 11 },
            new { IssueId = 24, LabelId = 13 },
            new { IssueId = 26, LabelId = 12 },
            new { IssueId = 27, LabelId = 15 });
    }

    private static void SeedIssueComponents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IssueComponent>().HasData(
            new { IssueId = 1, ComponentId = 2 },
            new { IssueId = 2, ComponentId = 2 },
            new { IssueId = 3, ComponentId = 3 },
            new { IssueId = 4, ComponentId = 1 },
            new { IssueId = 5, ComponentId = 4 },
            new { IssueId = 6, ComponentId = 4 },
            new { IssueId = 8, ComponentId = 1 },
            new { IssueId = 10, ComponentId = 4 },
            new { IssueId = 12, ComponentId = 1 },

            new { IssueId = 13, ComponentId = 6 },
            new { IssueId = 14, ComponentId = 7 },
            new { IssueId = 15, ComponentId = 5 },
            new { IssueId = 16, ComponentId = 7 },
            new { IssueId = 18, ComponentId = 7 },
            new { IssueId = 19, ComponentId = 6 },
            new { IssueId = 20, ComponentId = 7 },

            new { IssueId = 21, ComponentId = 8 },
            new { IssueId = 22, ComponentId = 9 },
            new { IssueId = 23, ComponentId = 10 },
            new { IssueId = 24, ComponentId = 8 },
            new { IssueId = 26, ComponentId = 10 },
            new { IssueId = 27, ComponentId = 9 });
    }

    private static void SeedSavedFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SavedFilter>().HasData(
            new SavedFilter
            {
                Id = 1,
                ProjectId = 1,
                UserId = 2,
                Name = "Blocker van hanh tuan nay",
                QueryText = "priority in (Highest, High) AND status != Done ORDER BY updated DESC",
                IsFavorite = true,
                CreatedAtUtc = SeedTime.AddDays(-7),
                UpdatedAtUtc = SeedTime.AddDays(-1)
            },
            new SavedFilter
            {
                Id = 2,
                ProjectId = 1,
                UserId = 3,
                Name = "Viec cua Quan trong sprint hien tai",
                QueryText = "assignee = currentUser() AND sprint = \"Sprint 10 - on dinh van hanh mien Nam\"",
                IsFavorite = false,
                CreatedAtUtc = SeedTime.AddDays(-5),
                UpdatedAtUtc = SeedTime.AddDays(-2)
            },
            new SavedFilter
            {
                Id = 3,
                ProjectId = 2,
                UserId = 7,
                Name = "Bug lead can xu ly",
                QueryText = "type = Bug AND status != Done ORDER BY priority DESC",
                IsFavorite = true,
                CreatedAtUtc = SeedTime.AddDays(-3),
                UpdatedAtUtc = SeedTime.AddDays(-1)
            },
            new SavedFilter
            {
                Id = 4,
                ProjectId = 3,
                UserId = 2,
                Name = "Doi soat dang trien khai",
                QueryText = "status in (\"Dang trien khai\", \"Cho doi soat\") ORDER BY dueDate ASC",
                IsFavorite = false,
                CreatedAtUtc = SeedTime.AddDays(-2),
                UpdatedAtUtc = SeedTime.AddDays(-1)
            },
            new SavedFilter
            {
                Id = 5,
                ProjectId = 1,
                UserId = 11,
                Name = "Roadmap rollout cua hang",
                QueryText = "type in (Epic, Story) AND fixVersion = \"Rollout mien Nam\" ORDER BY rank ASC",
                IsFavorite = false,
                CreatedAtUtc = SeedTime.AddDays(-4),
                UpdatedAtUtc = SeedTime.AddDays(-1)
            });
    }

    private static void SeedComments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comment>().HasData(
            new { Id = 1, IssueId = 2, UserId = 5, Body = "Da kiem tra tren danh sach don 50.000 ban ghi, bo loc hien dang cham khi loc them theo trang thai thanh toan.", IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-4), UpdatedAtUtc = SeedTime.AddDays(-4) },
            new { Id = 2, IssueId = 4, UserId = 2, Body = "@duc.an uu tien trace callback VNPay truoc 10h sang mai de doi ke toan doi soat.", IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-1), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 3, IssueId = 5, UserId = 3, Body = "Da co draft rule chon kho theo ban kinh va ton kha dung, can PM chot nguyen tac khi ton kho sat nguong.", IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-2), UpdatedAtUtc = SeedTime.AddDays(-2) },
            new { Id = 4, IssueId = 15, UserId = 7, Body = "Bo creative da duyet concept, hien chi con chot line claim cho khu vuc mien Tay.", IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-2), UpdatedAtUtc = SeedTime.AddDays(-2) },
            new { Id = 5, IssueId = 16, UserId = 5, Body = "Da reproduce tren iPhone 11 Safari, submit xong mat toan bo UTM source va campaign.", IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-1), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 6, IssueId = 22, UserId = 8, Body = "Bao cao can gom them cot RM phu trach va nguong lech duoc phep theo chi nhanh.", IsDeleted = false, CreatedAtUtc = SeedTime.AddDays(-2), UpdatedAtUtc = SeedTime.AddDays(-2) },
            new { Id = 7, IssueId = 23, UserId = 9, Body = "Da them buoc check giao dich treo luc 17h30, dang doi khoi tai chinh test tren file sao ke thuc te.", IsDeleted = false, CreatedAtUtc = SeedTime.AddHours(-16), UpdatedAtUtc = SeedTime.AddHours(-16) },
            new { Id = 8, IssueId = 8, UserId = 4, Body = "Cua hang thuong dong ca sau 0h nen bao cao doanh thu theo ca hien dang khong doi duoc voi ke toan.", IsDeleted = false, CreatedAtUtc = SeedTime.AddHours(-20), UpdatedAtUtc = SeedTime.AddHours(-20) });
    }

    private static void SeedWatchers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Watcher>().HasData(
            new { IssueId = 4, UserId = 2, WatchedAtUtc = SeedTime.AddDays(-1) },
            new { IssueId = 4, UserId = 5, WatchedAtUtc = SeedTime.AddDays(-1) },
            new { IssueId = 5, UserId = 4, WatchedAtUtc = SeedTime.AddDays(-2) },
            new { IssueId = 16, UserId = 7, WatchedAtUtc = SeedTime.AddDays(-1) },
            new { IssueId = 22, UserId = 8, WatchedAtUtc = SeedTime.AddDays(-2) },
            new { IssueId = 24, UserId = 2, WatchedAtUtc = SeedTime.AddHours(-18) });
    }

    private static void SeedNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>().HasData(
            new Notification
            {
                Id = 1,
                RecipientUserId = 9,
                IssueId = 4,
                ProjectId = 1,
                Type = NotificationType.IssueAssigned,
                Title = "Ban vua duoc giao APR-4",
                Body = "Bug hoan tien VNPay can duoc xu ly trong Sprint 10.",
                IsRead = false,
                CreatedAtUtc = SeedTime.AddDays(-1),
                UpdatedAtUtc = SeedTime.AddDays(-1)
            },
            new Notification
            {
                Id = 2,
                RecipientUserId = 9,
                IssueId = 4,
                ProjectId = 1,
                Type = NotificationType.CommentMentioned,
                Title = "Ban duoc nhac trong APR-4",
                Body = "Tran Ngoc Hanh da mention ban trong comment ve callback VNPay.",
                IsRead = false,
                CreatedAtUtc = SeedTime.AddHours(-22),
                UpdatedAtUtc = SeedTime.AddHours(-22)
            },
            new Notification
            {
                Id = 3,
                RecipientUserId = 3,
                IssueId = 2,
                ProjectId = 1,
                Type = NotificationType.IssueWatcherUpdate,
                Title = "APR-2 vua co cap nhat moi",
                Body = "QA da bo sung nhan xet ve hieu nang tren bo loc don.",
                IsRead = true,
                ReadAtUtc = SeedTime.AddHours(-20),
                CreatedAtUtc = SeedTime.AddDays(-4),
                UpdatedAtUtc = SeedTime.AddHours(-20)
            },
            new Notification
            {
                Id = 4,
                RecipientUserId = 5,
                IssueId = 16,
                ProjectId = 2,
                Type = NotificationType.IssueAssigned,
                Title = "Ban vua duoc giao BMG-4",
                Body = "Can fix gap loi mat UTM tren form lead mobile.",
                IsRead = false,
                CreatedAtUtc = SeedTime.AddHours(-18),
                UpdatedAtUtc = SeedTime.AddHours(-18)
            },
            new Notification
            {
                Id = 5,
                RecipientUserId = 2,
                ProjectId = 1,
                Type = NotificationType.SprintStarted,
                Title = "Sprint 10 da bat dau",
                Body = "Sprint 10 - on dinh van hanh mien Nam da duoc kich hoat cho APR.",
                IsRead = true,
                ReadAtUtc = SeedTime.AddDays(-6),
                CreatedAtUtc = SeedTime.AddDays(-7),
                UpdatedAtUtc = SeedTime.AddDays(-6)
            },
            new Notification
            {
                Id = 6,
                RecipientUserId = 8,
                IssueId = 22,
                ProjectId = 3,
                Type = NotificationType.CommentAdded,
                Title = "CLF-2 vua co binh luan moi",
                Body = "Bao cao doi soat can bo sung cot RM phu trach.",
                IsRead = false,
                CreatedAtUtc = SeedTime.AddDays(-2),
                UpdatedAtUtc = SeedTime.AddDays(-2)
            },
            new Notification
            {
                Id = 7,
                RecipientUserId = 2,
                ProjectId = 3,
                Type = NotificationType.SprintCompleted,
                Title = "Sprint 07 da dong",
                Body = "Du lieu doi soat quy 1 da duoc khoa cho CLF.",
                IsRead = true,
                ReadAtUtc = SeedTime.AddDays(-5),
                CreatedAtUtc = SeedTime.AddDays(-6),
                UpdatedAtUtc = SeedTime.AddDays(-5)
            },
            new Notification
            {
                Id = 8,
                RecipientUserId = 4,
                IssueId = 5,
                ProjectId = 1,
                Type = NotificationType.IssueWatcherUpdate,
                Title = "APR-5 vua duoc cap nhat",
                Body = "Minh Quan da bo sung rule goi y kho xuat va dang doi PM chot nghiep vu.",
                IsRead = false,
                CreatedAtUtc = SeedTime.AddDays(-2),
                UpdatedAtUtc = SeedTime.AddDays(-2)
            });
    }

    private static void SeedWebhooks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WebhookEndpoint>().HasData(
            new WebhookEndpoint
            {
                Id = 1,
                ProjectId = 1,
                Name = "Ops channel OMS",
                Url = "https://ops.anphuc.vn/hooks/oms-jira",
                Secret = string.Empty,
                IsActive = true,
                CreatedAtUtc = SeedTime.AddDays(-20),
                UpdatedAtUtc = SeedTime.AddDays(-3)
            },
            new WebhookEndpoint
            {
                Id = 2,
                ProjectId = 3,
                Name = "Finance audit endpoint",
                Url = "https://audit.cuulongfinance.vn/hooks/doi-soat",
                Secret = string.Empty,
                IsActive = true,
                CreatedAtUtc = SeedTime.AddDays(-14),
                UpdatedAtUtc = SeedTime.AddDays(-2)
            });

        modelBuilder.Entity<WebhookEndpointSubscription>().HasData(
            new { WebhookEndpointId = 1, EventType = WebhookEventType.IssueCreated },
            new { WebhookEndpointId = 1, EventType = WebhookEventType.IssueStatusChanged },
            new { WebhookEndpointId = 1, EventType = WebhookEventType.SprintCompleted },
            new { WebhookEndpointId = 2, EventType = WebhookEventType.CommentAdded },
            new { WebhookEndpointId = 2, EventType = WebhookEventType.SprintCompleted });

        modelBuilder.Entity<WebhookDelivery>().HasData(
            new WebhookDelivery
            {
                Id = 1,
                WebhookEndpointId = 1,
                EventType = WebhookEventType.IssueStatusChanged,
                Payload = "{\"issueKey\":\"APR-3\",\"from\":\"In Progress\",\"to\":\"Ready for QA\"}",
                ResponseCode = 200,
                Success = true,
                AttemptedAtUtc = SeedTime.AddHours(-6),
                RetryCount = 0,
                ErrorMessage = null,
                CreatedAtUtc = SeedTime.AddHours(-6),
                UpdatedAtUtc = SeedTime.AddHours(-6)
            },
            new WebhookDelivery
            {
                Id = 2,
                WebhookEndpointId = 2,
                EventType = WebhookEventType.CommentAdded,
                Payload = "{\"issueKey\":\"CLF-2\",\"commentId\":6}",
                ResponseCode = 202,
                Success = true,
                AttemptedAtUtc = SeedTime.AddDays(-2),
                RetryCount = 0,
                ErrorMessage = null,
                CreatedAtUtc = SeedTime.AddDays(-2),
                UpdatedAtUtc = SeedTime.AddDays(-2)
            });
    }

    private static void SeedActivities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActivityLog>().HasData(
            new { Id = 1, ProjectId = 1, IssueId = 1, UserId = 2, ActionType = ActivityActionType.Created, FieldName = (string?)null, OldValue = (string?)null, NewValue = "Chuan hoa dashboard dieu phoi don hang cho 48 cua hang mien Nam", OccurredAtUtc = SeedTime.AddDays(-25), MetadataJson = (string?)null, CreatedAtUtc = SeedTime.AddDays(-25), UpdatedAtUtc = SeedTime.AddDays(-25) },
            new { Id = 2, ProjectId = 1, IssueId = 3, UserId = 6, ActionType = ActivityActionType.StatusChanged, FieldName = nameof(Issue.WorkflowStatusId), OldValue = "In Progress", NewValue = "Ready for QA", OccurredAtUtc = SeedTime.AddHours(-6), MetadataJson = "{\"OldStatusId\":3,\"OldStatusName\":\"In Progress\",\"NewStatusId\":4,\"NewStatusName\":\"Ready for QA\"}", CreatedAtUtc = SeedTime.AddHours(-6), UpdatedAtUtc = SeedTime.AddHours(-6) },
            new { Id = 3, ProjectId = 1, IssueId = 4, UserId = 2, ActionType = ActivityActionType.CommentAdded, FieldName = nameof(Comment.Body), OldValue = (string?)null, NewValue = "Can trace callback VNPay truoc 10h", OccurredAtUtc = SeedTime.AddHours(-22), MetadataJson = "{\"CommentId\":2}", CreatedAtUtc = SeedTime.AddHours(-22), UpdatedAtUtc = SeedTime.AddHours(-22) },
            new { Id = 4, ProjectId = 1, IssueId = 8, UserId = 4, ActionType = ActivityActionType.Created, FieldName = (string?)null, OldValue = (string?)null, NewValue = "Bao cao doanh thu theo ca lech 1 ngay khi qua 0h", OccurredAtUtc = SeedTime.AddDays(-4), MetadataJson = (string?)null, CreatedAtUtc = SeedTime.AddDays(-4), UpdatedAtUtc = SeedTime.AddDays(-4) },
            new { Id = 5, ProjectId = 1, IssueId = 7, UserId = 9, ActionType = ActivityActionType.SprintClosed, FieldName = nameof(Sprint.State), OldValue = "Active", NewValue = "Closed", OccurredAtUtc = new DateTime(2026, 3, 29, 11, 0, 0, DateTimeKind.Utc), MetadataJson = "{\"SprintId\":1,\"SprintName\":\"Sprint 09 - go live Ha Noi\"}", CreatedAtUtc = new DateTime(2026, 3, 29, 11, 0, 0, DateTimeKind.Utc), UpdatedAtUtc = new DateTime(2026, 3, 29, 11, 0, 0, DateTimeKind.Utc) },

            new { Id = 6, ProjectId = 2, IssueId = 13, UserId = 7, ActionType = ActivityActionType.Created, FieldName = (string?)null, OldValue = (string?)null, NewValue = "Chien dich He 2026 cho dong may loc nuoc gia dinh", OccurredAtUtc = SeedTime.AddDays(-14), MetadataJson = (string?)null, CreatedAtUtc = SeedTime.AddDays(-14), UpdatedAtUtc = SeedTime.AddDays(-14) },
            new { Id = 7, ProjectId = 2, IssueId = 15, UserId = 7, ActionType = ActivityActionType.CommentAdded, FieldName = nameof(Comment.Body), OldValue = (string?)null, NewValue = "Da chot concept banner", OccurredAtUtc = SeedTime.AddDays(-2), MetadataJson = "{\"CommentId\":4}", CreatedAtUtc = SeedTime.AddDays(-2), UpdatedAtUtc = SeedTime.AddDays(-2) },
            new { Id = 8, ProjectId = 2, IssueId = 16, UserId = 5, ActionType = ActivityActionType.Created, FieldName = (string?)null, OldValue = (string?)null, NewValue = "Form dang ky lead khong luu nguon quang cao", OccurredAtUtc = SeedTime.AddDays(-1), MetadataJson = (string?)null, CreatedAtUtc = SeedTime.AddDays(-1), UpdatedAtUtc = SeedTime.AddDays(-1) },
            new { Id = 9, ProjectId = 2, IssueId = 17, UserId = 7, ActionType = ActivityActionType.StatusChanged, FieldName = nameof(Issue.WorkflowStatusId), OldValue = "Doing", NewValue = "Done", OccurredAtUtc = SeedTime.AddDays(-10), MetadataJson = "{\"OldStatusId\":7,\"OldStatusName\":\"Doing\",\"NewStatusId\":9,\"NewStatusName\":\"Done\"}", CreatedAtUtc = SeedTime.AddDays(-10), UpdatedAtUtc = SeedTime.AddDays(-10) },

            new { Id = 10, ProjectId = 3, IssueId = 21, UserId = 2, ActionType = ActivityActionType.Created, FieldName = (string?)null, OldValue = (string?)null, NewValue = "Tu dong hoa doi soat sao ke cho khoi SME", OccurredAtUtc = SeedTime.AddDays(-7), MetadataJson = (string?)null, CreatedAtUtc = SeedTime.AddDays(-7), UpdatedAtUtc = SeedTime.AddDays(-7) },
            new { Id = 11, ProjectId = 3, IssueId = 22, UserId = 8, ActionType = ActivityActionType.CommentAdded, FieldName = nameof(Comment.Body), OldValue = (string?)null, NewValue = "Bao cao can them cot RM phu trach", OccurredAtUtc = SeedTime.AddDays(-2), MetadataJson = "{\"CommentId\":6}", CreatedAtUtc = SeedTime.AddDays(-2), UpdatedAtUtc = SeedTime.AddDays(-2) },
            new { Id = 12, ProjectId = 3, IssueId = 23, UserId = 9, ActionType = ActivityActionType.StatusChanged, FieldName = nameof(Issue.WorkflowStatusId), OldValue = "Dang trien khai", NewValue = "Cho doi soat", OccurredAtUtc = SeedTime.AddHours(-16), MetadataJson = "{\"OldStatusId\":12,\"OldStatusName\":\"Dang trien khai\",\"NewStatusId\":13,\"NewStatusName\":\"Cho doi soat\"}", CreatedAtUtc = SeedTime.AddHours(-16), UpdatedAtUtc = SeedTime.AddHours(-16) },
            new { Id = 13, ProjectId = 3, IssueId = 25, UserId = 2, ActionType = ActivityActionType.SprintClosed, FieldName = nameof(Sprint.State), OldValue = "Active", NewValue = "Closed", OccurredAtUtc = new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc), MetadataJson = "{\"SprintId\":4,\"SprintName\":\"Sprint 07 - doi soat quy 1\"}", CreatedAtUtc = new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc), UpdatedAtUtc = new DateTime(2026, 4, 14, 10, 0, 0, DateTimeKind.Utc) });
    }

    private static User CreateUser(int id, string userName, string displayName, string email) => new()
    {
        Id = id,
        UserName = userName,
        DisplayName = displayName,
        Email = email,
        PasswordHash = DefaultPasswordHash,
        PasswordSalt = DefaultPasswordSalt,
        IsActive = true,
        EmailNotificationsEnabled = true,
        CreatedAtUtc = SeedTime,
        UpdatedAtUtc = SeedTime
    };

    private static Label CreateLabel(int id, int projectId, string name, string color) => new()
    {
        Id = id,
        ProjectId = projectId,
        Name = name,
        Color = color,
        CreatedAtUtc = SeedTime,
        UpdatedAtUtc = SeedTime
    };

    private static Component CreateComponent(int id, int projectId, string name, string description, int leadUserId) => new()
    {
        Id = id,
        ProjectId = projectId,
        Name = name,
        Description = description,
        LeadUserId = leadUserId,
        CreatedAtUtc = SeedTime,
        UpdatedAtUtc = SeedTime
    };

    private static ProjectVersion CreateVersion(int id, int projectId, string name, string description, DateTime? releaseDate, bool isReleased) => new()
    {
        Id = id,
        ProjectId = projectId,
        Name = name,
        Description = description,
        ReleaseDate = releaseDate,
        IsReleased = isReleased,
        CreatedAtUtc = SeedTime,
        UpdatedAtUtc = SeedTime
    };

    private static int AddTransitions(List<object> transitions, List<object> transitionRoles, int transitionId, int workflowDefinitionId, IReadOnlyList<int> statusIds)
    {
        foreach (var fromStatusId in statusIds)
        {
            foreach (var toStatusId in statusIds.Where(id => id != fromStatusId))
            {
                transitions.Add(new
                {
                    Id = transitionId,
                    WorkflowDefinitionId = workflowDefinitionId,
                    FromStatusId = fromStatusId,
                    ToStatusId = toStatusId,
                    Name = $"{ResolveStatusName(fromStatusId)} to {ResolveStatusName(toStatusId)}",
                    CreatedAtUtc = SeedTime,
                    UpdatedAtUtc = SeedTime
                });

                transitionRoles.Add(new { WorkflowTransitionId = transitionId, RoleId = 1 });
                transitionRoles.Add(new { WorkflowTransitionId = transitionId, RoleId = 2 });
                transitionRoles.Add(new { WorkflowTransitionId = transitionId, RoleId = 3 });
                transitionId++;
            }
        }

        return transitionId;
    }

    private static string ResolveStatusName(int statusId) => statusId switch
    {
        1 => "Backlog",
        2 => "Selected",
        3 => "In Progress",
        4 => "Ready for QA",
        5 => "Done",
        6 => "To Do",
        7 => "Doing",
        8 => "Cho duyet noi dung",
        9 => "Done",
        10 => "Backlog",
        11 => "Dang phan tich",
        12 => "Dang trien khai",
        13 => "Cho doi soat",
        14 => "Done",
        _ => "Status"
    };
}
