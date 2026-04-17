# Tài liệu Hướng dẫn sử dụng Jira Clone (WinForms)

Tài liệu này cung cấp hướng dẫn thao tác chi tiết từng bước cho các nhóm chức năng trên ứng dụng Jira Clone Desktop (WinForms). Ứng dụng được thiết kế dựa trên Clean Architecture, hỗ trợ thao tác nhóm và quản lý tiến độ dự án.

---

## 1. PHÂN QUYỀN VÀ VAI TRÒ (ROLES)

Hệ thống cung cấp 4 vai trò (Project Role) cơ bản, được kế thừa quyền từ trên xuống dưới:

1. **Viewer (Người xem):** Chỉ có quyền đọc (Read-only). Xem danh sách dự án, xem bảng (Board), xem Roadmap.
2. **Developer (Lập trình viên / Người thực thi):** Gồm tất cả quyền Viewer + Quyền quản lý Issue (Tạo mới, Sửa, Xóa, Cập nhật trạng thái) + Quyền tương tác comment (Thêm, Sửa, Xóa comment của chính mình).
3. **Project Manager (Quản lý dự án):** Gồm tất cả quyền Developer + Quản lý Sprint (Tạo, Bắt đầu, Hoàn thành Sprint) + Chỉnh sửa giao diện bảng Board (Quản lý cột, WIP limits) + Quản lý thành viên (Manage Members) và cài đặt dự án.
4. **Admin (Quản trị trị hệ thống):** Gồm tất cả quyền Project Manager + Toàn quyền can thiệp vào bất kỳ dự án nào, có quyền quản lý người dùng cấp hệ thống (Users), tích hợp Webhook và API Token.

---

## 2. HƯỚNG DẪN SỬ DỤNG CHO VIEWER

Là người chỉ theo dõi tiến độ (khách hàng, ban giám đốc...), công việc chủ yếu của bạn là truy cập tra cứu, không làm thay đổi dữ liệu bảng dự án.

### 2.1. Đăng nhập và Xem Tổng quan (Dashboard)
- **Bước 1:** Khởi động ứng dụng, tại màn hình Login, nhập thông tin tài khoản (ví dụ tài khoản Viewer) và mật khẩu, sau đó bấm nút **Login**.
- **Bước 2:** Hệ thống mặc định mở màn hình **Tổng quan (Dashboard)**.
- **Bước 3:** Tại thanh Sidebar bên tay trái, đảm bảo bạn đang chọn đúng dự án tại mục chọn dự án trên cùng (Project Switcher).
- **Bước 4:** Bảng Dashboard sẽ hiển thị Tiến độ Sprint hiện tại (Sprint Progress), Biểu đồ thống kê Issue (Statistics), Các biến động mới nhất (Recent Activity) và Khối lượng công việc theo người (Team Workload).

### 2.2. Xem Bảng (Kanban/Scrum Board)
- **Bước 1:** Tại Sidebar bên trái, bấm vào menu **Bảng (Board)**.
- **Bước 2:** Giao diện hiển thị các cột trạng thái tương ứng (ví dụ: Backlog, Selected, In Progress, Done). Các thẻ công việc (Issue Cards) được hiển thị dọc theo từng cột.
- **Bước 3:** Bấm *đúp chuột* (double-click) vào thẻ công việc bất kỳ để mở **Chi tiết Issue**.
- **Bước 4:** Ở màn hình chi tiết, bạn có thể đọc mô tả (Description), xem bình luận (Comments) và xem danh sách nhãn dán (Labels) hoặc Component. Bạn **không** có quyền thay đổi các giá trị này.

### 2.3. Xem Lộ trình (Roadmap)
- **Bước 1:** Trở về thanh Sidebar, bấm vào menu **Lộ trình (Roadmap)**.
- **Bước 2:** Giao diện hiển thị các Issue lớn (Epic) hoặc mục tiêu dự án theo chiều trục thời gian (Gantt chart đơn giản).
- **Bước 3:** Kéo thanh cuộn chuột để xem lịch trình của các tháng/quý.

---

## 3. HƯỚNG DẪN SỬ DỤNG CHO DEVELOPER

Developer là người trực tiếp tham gia xử lý công việc và báo cáo tiến trình.

### 3.1. Cập nhật trạng thái công việc (Board / Drag & Drop)
- **Bước 1:** Đăng nhập, từ Sidebar menu chọn **Bảng (Board)**.
- **Bước 2:** Nhấn và giữ chuột trái (Drag) vào thẻ (Issue) đang được gán cho bạn ở cột *Selected* hoặc *Backlog*.
- **Bước 3:** Kéo thẻ di chuyển sang cột *In Progress* hoặc *Done* tùy thuộc vào tiến độ công việc.
- **Bước 4:** Thả chuột (Drop). Thẻ sẽ thay đổi trạng thái và hệ thống tự động lưu lịch sử thay đổi (Recent Activity).

### 3.2. Cập nhật chi tiết Issue và Viết bình luận
- **Bước 1:** Bấm *đúp chuột* vào thẻ công việc của bạn trên **Board** hoặc **Danh sách công việc**.
- **Bước 2:** Cửa sổ **Chi Tiết Issue (Issue Details)** mở ra ra. Bạn có thể sửa tiêu đề (Title) bằng cách click vào dòng tiêu đề, hoặc bấm **Edit Description** để viết mô tả bằng Text/Rich Text.
- **Bước 3:** Ở khung bình luận (Comments) phía dưới cùng, nhập phản hồi của bạn vào hộp thoại văn bản.
- **Bước 4:** Bấm **Lưu (Save)**. Bình luận sẽ lưu vào hệ thống và được tự động đánh dấu người gửi cùng thời gian.

### 3.3. Tạo mới Issue (Task/Bug)
- **Bước 1:** Trên thanh điều hướng Navbar (Top bar), bấm vào nút **Tạo (Create)** có màu xanh gạch.
- **Bước 2:** Cửa sổ Editor bật lên. Chọn **Dự án** (nếu cần), chọn loại thẻ **Loại Issue (Issue Type)**: *Task*, *Bug*, hoặc *Story*.
- **Bước 3:** Nhập Tiêu đề ngắn gọn và Mô tả chi tiết cho issue.
- **Bước 4:** Ở mục *Assignee*, có thể tự chọn tên chính mình (hoặc gán cho người khác). Chọn độ ưu tiên (Priority) -> Bấm **Save**. Issue mới sẽ xuất hiện ở Backlog hoặc Sprint hiện tại phụ thuộc vào vị trí khởi tạo.

---

## 4. HƯỚNG DẪN SỬ DỤNG CHO PROJECT MANAGER (PM)

Tập trung vào quản lý tiến độ, cấu hình Sprint và tổ chức thành viên trong nhóm.

### 4.1. Quản lý Nhóm và Project Settings
- **Bước 1:** Tại Sidebar, bấm chọn **Cài đặt (Settings)** (hoặc có thể được tìm thấy trong ProjectSettings tùy luồng hiển thị).
- **Bước 2:** Chọn mục **Quản lý nhóm / Thành viên**. Nhấn **Thêm**, tìm user có sẵn trong hệ thống và phân quyền tham gia dự án (VD: Viewer, Developer).
- **Bước 3:** Ở mục tuỳ chỉnh **Bảng (Board)**, PM có thể đổi tên các cột trên bảng hoặc thay đổi giới hạn công việc tối đa (WIP Limits) để tránh quá tải cho Developer.

### 4.2. Lập kế hoạch Sprint & Quản lý Backlog
- **Bước 1:** Tại Sidebar, chọn tab **Backlog**.
- **Bước 2:** Nhấn nút **Tạo Sprint (Create Sprint)** để khởi tạo một Sprint trống (hoặc chuyển sang SprintManagementForm).
- **Bước 3:** Nhấn giữ và kéo các công việc từ khu vực *Backlog Tổng* ở bên dưới lên khu vực của *Sprint vừa tạo*.
- **Bước 4:** Để khởi động Sprint, bấm nút **Bắt đầu Sprint (Start Sprint)**. Thiết lập Ngày bắt đầu (Start Date), Ngày kết thúc (End Date) và Mục tiêu Sprint (Sprint Goal) rồi nhấn **Lưu**.

### 4.3. Đóng (Complete) Sprint và Xem báo cáo
- **Bước 1:** Khi Sprint đã hết thời hạn, tại màn hình *Backlog* (hoặc trong *Board*), bấm nút **Hoàn thành Sprint (Complete Sprint)**.
- **Bước 2:** Hệ thống sẽ thu thập các Issue chưa hoàn thành (Incomplete issues). Bạn chọn xác nhận để chuyển các issue này sang Sprint kế tiếp hoặc trả lại về Backlog.
- **Bước 3:** Từ Sidebar, mở tab **Báo cáo (Reports)** để truy cập giao diện xem biểu đồ thông kê và đánh giá hiệu suất của nhóm sau kỳ chạy.

---

## 5. HƯỚNG DẪN SỬ DỤNG CHO SYSTEM ADMIN

Quản trị toàn quyền về tài khoản hệ thống, dự án tổng và các webhook.

### 5.1. Quản lý Toàn cầu (Global Users)
- **Bước 1:** (Menu chỉ hiện với quyền Admin). Tại Sidebar bên tay trái, bấm vào **Người dùng (Users)**.
- **Bước 2:** Danh sách tất cả tài khoản hiển thị. Bấm nút Thêm hoặc quản lý tài khoản User.
- **Bước 3:** Tạo Username, gắn Role hệ thống (Admin/Thành viên) và nhập mật khẩu mới.

### 5.2. Quản lý Dự Án (Projects Lifecycle)
- **Bước 1:** Click tab **Dự án (Projects)** ở top menu sidebar. 
- **Bước 2:** Nhấn nút **Tạo Dự Án (Create Project)** để cấp mới.
- **Bước 3:** Nhập Tên dự án (Project Name) và Khóa (Project Key - ví dụ: JIRA, SALES). 
- **Bước 4:** Assign User làm Project Manager.

### 5.3. Cài đặt Tích hợp và API (Integrations)
- **Bước 1:** Vào **Cài đặt (Settings)** -> Chọn menu quản lý API / Hook.
- **Bước 2:** Mở mục **API Tokens** (CreateApiTokenDialog) -> Nhấn tạo Token mới. Copy chuỗi Token sinh ra để giao cho phần mềm thứ 3 gọi vào Jira.
- **Bước 3:** Mở mục **Webhooks** (WebhookEndpointDialog) -> Nhấn **Thêm (Add Endpoint)**. Nhập URL Server nhận tin (HTTP POST) và tick chọn sự kiện bắt (Issue Created/Updated).
- **Bước 4:** Nhấn **Save**. Bạn có thể kiểm tra danh sách WebhookDeliveryHistory để xem log gọi có thành công hay không.
