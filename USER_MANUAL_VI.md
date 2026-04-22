# Tài liệu hướng dẫn sử dụng Jira Clone (WinForms)

Tài liệu này đã được cập nhật theo bộ seed data thực tế sau đợt reset dữ liệu gần nhất. Nội dung bên dưới bám theo dữ liệu đã nạp sẵn trong database để bạn có thể đăng nhập và test nghiệp vụ ngay.

## 1. Dữ liệu seed sẵn sàng để test

Mật khẩu mặc định của toàn bộ tài khoản seed là `ChangeMe123!`.

### 1.1. Tài khoản chính theo role

| Mục đích test | Username | Vai trò hệ thống | Project role chính |
| --- | --- | --- | --- |
| Admin hệ thống | `admin` | `Admin` | `Admin` ở `APR`, `BMG`, `CLF` |
| PM Scrum | `ngoc.hanh` | `ProjectManager` | `ProjectManager` ở `APR`, `CLF` |
| PM Kanban | `ngoc.anh` | `ProjectManager` | `ProjectManager` ở `BMG` |
| Developer chính | `minh.quan` | `Developer` | `Developer` ở `APR` |
| Developer hỗ trợ | `quoc.phuc` | `Developer` | `Developer` ở `APR`, `CLF` |
| QA/Dev | `thu.trang` | `Developer` | `Developer` ở `APR`, `BMG` |
| Viewer | `thu.lan` | `Viewer` | `Viewer` ở `APR`, `BMG` |

### 1.2. Project demo đang có sẵn

| Key | Tên project | Loại board | Dùng để test |
| --- | --- | --- | --- |
| `APR` | `An Phuc Retail OMS` | `Scrum` | vận hành đơn hàng, sprint, roadmap, issue flow |
| `BMG` | `Bao Minh Growth` | `Kanban` | marketing campaign, lead, board Kanban |
| `CLF` | `Cuu Long Finance Ops` | `Scrum` | đối soát tài chính, báo cáo, workflow nội bộ |

### 1.3. Dữ liệu nổi bật đã seed

- `APR` có `Sprint 10 - ổn định vận hành miền Nam` ở trạng thái `Active`, `Sprint 11 - tối ưu giao vận` ở trạng thái `Planned`.
- `APR` có các issue mẫu như `APR-1`, `APR-4`, `APR-5`, `APR-8`, `APR-10` để test luồng dashboard, board, bug, story, epic.
- `BMG` là board `Kanban`, có các issue mẫu như `BMG-1` đến `BMG-8`, phù hợp test content workflow và bug lead form.
- `CLF` có `Sprint 07 - đối soát quý 1` đã `Closed`, `Sprint 08 - báo cáo kiểm soát nội bộ` đang `Planned`.
- `APR` đã seed sẵn cấu hình `GitHub`, `Confluence` và webhook.
- `CLF` đã seed sẵn `Confluence` và webhook.
- Hệ thống đã có `API token`, `notification`, `comment`, `saved filter`, `component`, `label`, `version` để test các màn liên quan.

## 2. Phân quyền hiện tại

### 2.1. Project role

1. `Viewer`
   Có quyền xem project, dashboard, board, roadmap, reports, issue details.
2. `Developer`
   Có toàn bộ quyền của `Viewer` và được tạo, sửa, xóa issue, chuyển trạng thái issue, thêm comment, sửa hoặc xóa comment của chính mình.
3. `ProjectManager`
   Có toàn bộ quyền của `Developer` và được quản lý sprint, board, thành viên, cấu hình project, workflow, labels, components, versions, webhooks, integrations.
4. `Admin`
   Có toàn bộ quyền của `ProjectManager` trong project và là mức quyền cao nhất trong phạm vi project đó.

### 2.2. Vai trò hệ thống

- `Admin` hệ thống thấy menu `Users`, quản lý tài khoản hệ thống, reset password, kích hoạt hoặc khóa user.
- Lưu ý: quyền quản trị hệ thống không tự động thay thế membership theo project. User vẫn cần là member của project để thấy và thao tác project đó.

## 3. Hướng dẫn nhanh cho Viewer

Khuyến nghị dùng tài khoản `thu.lan` để test các luồng chỉ đọc.

### 3.1. Đăng nhập và chọn project

1. Mở ứng dụng và đăng nhập bằng `thu.lan / ChangeMe123!`.
2. Ở thanh chọn project phía trên sidebar, chọn `An Phuc Retail OMS`.
3. Kiểm tra breadcrumb và dashboard đã đổi sang project `APR`.

### 3.2. Xem dashboard

1. Mở menu `Tổng quan`.
2. Kiểm tra các khối `Tiến độ sprint`, `Thống kê issue`, `Hoạt động gần đây`, `Tải công việc của nhóm`.
3. Dùng dữ liệu có sẵn của `APR` để xem tiến độ sprint và activity log.

### 3.3. Xem board và issue

1. Mở menu `Bảng`.
2. Xem các cột `Backlog`, `Selected`, `In Progress`, `Ready for QA`, `Done`.
3. Mở các issue như `APR-3`, `APR-4`, `APR-5`.
4. Kiểm tra comment, label, component, assignee, priority, version.

### 3.4. Xem roadmap và báo cáo

1. Mở `Lộ trình` để xem epic `APR-1` và `APR-9`.
2. Mở `Báo cáo` để xem các chart như burndown, velocity, cumulative flow nếu project có dữ liệu tương ứng.

## 4. Hướng dẫn nhanh cho Developer

Khuyến nghị dùng `minh.quan` hoặc `quoc.phuc`.

### 4.1. Cập nhật issue đang có sẵn

1. Đăng nhập bằng `minh.quan / ChangeMe123!`.
2. Chọn project `APR`.
3. Mở `APR-2` hoặc `APR-5`.
4. Chỉnh tiêu đề, mô tả, priority hoặc assignee nếu cần.
5. Thêm comment mới để kiểm tra activity log và notification.

### 4.2. Chuyển trạng thái issue trên board

1. Mở `Bảng`.
2. Kéo issue như `APR-5` từ `Selected` sang `In Progress`.
3. Kéo tiếp sang `Ready for QA` hoặc `Done` nếu muốn test full flow.
4. Kiểm tra issue vừa đổi trạng thái xuất hiện ở activity feed.

### 4.3. Tạo issue mới

1. Bấm `Tạo`.
2. Chọn project `APR` hoặc `BMG`.
3. Chọn loại issue như `Task`, `Bug`, `Story`.
4. Nhập tiêu đề, mô tả, assignee, priority, sprint hoặc fix version.
5. Lưu issue và kiểm tra issue mới xuất hiện trên board hoặc backlog.

### 4.4. Test comment, watcher và notification

1. Mở `APR-4` hoặc `CLF-2`.
2. Thêm comment có mention như `@duc.an`.
3. Bật theo dõi issue hoặc kiểm tra danh sách watcher hiện có.
4. Xác nhận notification sinh ra cho user liên quan.

## 5. Hướng dẫn nhanh cho Project Manager

Khuyến nghị dùng `ngoc.hanh` cho Scrum và `ngoc.anh` cho Kanban.

### 5.1. Quản lý sprint trên project Scrum

1. Đăng nhập bằng `ngoc.hanh / ChangeMe123!`.
2. Chọn `APR` hoặc `CLF`.
3. Mở `Backlog` hoặc `Sprint`.
4. Kiểm tra sprint hiện có:
   `APR`: `Sprint 10 - ổn định vận hành miền Nam`, `Sprint 11 - tối ưu giao vận`
   `CLF`: `Sprint 07 - đối soát quý 1`, `Sprint 08 - báo cáo kiểm soát nội bộ`
5. Tạo sprint mới, thêm issue vào sprint, hoặc đóng sprint đang active để test luồng PM.

### 5.2. Quản lý board và workflow

1. Vào `Cài đặt dự án`.
2. Mở các mục `Board`, `Workflow`, `Permissions`, `Labels`, `Components`, `Versions`.
3. Thử chỉnh WIP limit, thêm status, thêm transition, thêm label hoặc component.

### 5.3. Quản lý thành viên

1. Vào `Cài đặt dự án` → `Members`.
2. Thêm user có sẵn vào project.
3. Đổi role của member giữa `Viewer`, `Developer`, `ProjectManager`, `Admin`.

### 5.4. Quản lý integrations và webhooks

1. Ở `APR`, vào `Integrations` để xem cấu hình `GitHub` và `Confluence` đã seed sẵn.
2. Ở `CLF`, vào `Integrations` để xem `Confluence`.
3. Vào `Webhooks` để xem endpoint mẫu và delivery history.

## 6. Hướng dẫn nhanh cho System Admin

Khuyến nghị dùng `admin`.

### 6.1. Quản lý user hệ thống

1. Đăng nhập bằng `admin / ChangeMe123!`.
2. Mở menu `Users`.
3. Xem danh sách user đã seed.
4. Test tạo user mới, khóa hoặc mở khóa user, reset password, gán system role.

### 6.2. Quản lý project

1. Mở `Dự án`.
2. Kiểm tra các project mẫu `APR`, `BMG`, `CLF`.
3. Tạo project mới để test riêng nếu không muốn làm thay đổi dữ liệu seed chính.

### 6.3. API token và tích hợp

1. Mở hồ sơ cá nhân hoặc phần `API Tokens`.
2. Kiểm tra token mẫu đã seed.
3. Tạo token mới, copy token, sau đó revoke để test đầy đủ vòng đời token.

## 7. Kịch bản test nghiệp vụ đề xuất

### 7.1. Kịch bản 1: Viewer kiểm tra tiến độ

1. Đăng nhập `thu.lan`.
2. Chọn `APR`.
3. Vào `Tổng quan`, `Bảng`, `Lộ trình`, `Báo cáo`.
4. Mở `APR-1`, `APR-3`, `APR-8`.

### 7.2. Kịch bản 2: Developer xử lý bug vận hành

1. Đăng nhập `minh.quan`.
2. Chọn `APR`.
3. Mở `APR-4`.
4. Thêm comment, đổi trạng thái, cập nhật priority hoặc fix version.

### 7.3. Kịch bản 3: PM quản lý sprint

1. Đăng nhập `ngoc.hanh`.
2. Chọn `APR`.
3. Vào `Backlog`.
4. Tạo sprint mới hoặc hoàn thành sprint hiện tại.
5. Kiểm tra issue chưa xong được chuyển về backlog hoặc sprint tiếp theo.

### 7.4. Kịch bản 4: Admin quản lý hệ thống

1. Đăng nhập `admin`.
2. Vào `Users`.
3. Tạo hoặc chỉnh user.
4. Vào `Projects` và `Settings` để kiểm tra quyền quản trị hệ thống.

## 8. Ghi chú sau khi cập nhật tài liệu

- Tài liệu cũ không còn phù hợp hoàn toàn vì vẫn mô tả theo seed data cũ và có vài nhận xét lẫn với bug note.
- Tài liệu này đã đổi sang project thật hơn theo bối cảnh Việt Nam: retail, marketing, finance ops.
- Nếu bạn test E2E theo file cấu hình hiện tại, bộ user mặc định là:
  `admin`, `minh.quan`, `ngoc.hanh`.
- Những góp ý về chuỗi tiếng Anh còn sót hoặc lỗi layout search nên được theo dõi như bug UI riêng, không nên để lẫn trong user manual.
