# Checklist test thu cong Jira Desktop

## Tai khoan test
- Admin: `admin` / `ChangeMe123!`
- Project Manager: `yoda` / `ChangeMe123!`
- Developer: `gaben` / `ChangeMe123!`

## Chuan bi truoc khi test
- [x] Chay `dotnet ef database update` de ap dung migration moi nhat.
- [x] Mo app va dang nhap bang tung tai khoan seed.
- [x] Xac nhan giao dien hien chu Viet o cac man hinh chinh: dang nhap, sidebar, board, backlog, roadmap, reports, settings.
- [x] Xac nhan project mac dinh `JIRA` duoc tai len thanh cong.

## Smoke test chung
- [x] Dang nhap thanh cong bang `admin`, `yoda`, `gaben` voi mat khau `ChangeMe123!`.
- [x] Dang xuat va dang nhap lai khong loi.
- [x] Thu `Remember me` roi mo lai app, session duoc khoi phuc.
- [ ] Notification badge tu dong cap nhat sau khoang 30 giay.
- [x] Search issue o thanh tren tra ve ket qua.
- [x] Chuyen project bang project switcher neu co nhieu project.

## Luong test cho Admin
### Quan tri he thong
- [ ] Mo `User Management`.
- [ ] Tao user moi.
- [ ] Reset mat khau user bang dialog moi, khong con VB InputBox.
- [ ] Kich hoat / vo hieu hoa user.
- [ ] Vao `Profile` va tao API token, sao chep token, revoke token.

### Quan tri du an
- [ ] Mo `Project Settings`.
- [ ] Sua thong tin chung cua project.
- [ ] Sua WIP limit tung cot trong `Board` tab.
- [ ] Sua `Permissions` va luu scheme.
- [ ] Sua `Workflow`: them status, them transition, kiem tra dropdown status trong issue chi hien transition hop le.
- [ ] CRUD `Labels`, `Components`, `Versions`.
- [ ] Thu `Archive Project`.
- [ ] Thu `Delete Project` voi double confirmation nhap dung project key.

### Tich hop va he thong ngoai
- [ ] Vao `Integrations`, mo dialog cau hinh GitHub.
- [ ] Mo dialog cau hinh Confluence.
- [ ] Vao `Webhooks`, them webhook moi.
- [ ] Bam `Test` webhook, xem response code.
- [ ] Xem `Delivery History` cua webhook.

## Luong test cho Project Manager
### Board / Sprint / Scrum-Kanban
- [ ] Mo `Board`.
- [ ] Toggle `Scrum / Kanban`.
- [ ] O che do Kanban, xac nhan board hien tat ca issue chua Done va khong can active sprint.
- [ ] Thu keo tha issue giua cac cot.
- [ ] Thu keo issue vao cot vuot WIP limit, xac nhan co canh bao va cho override.
- [ ] Bat `Group by Epic` va xem swimlane epic.
- [ ] Bam vao header epic de mo chi tiet epic.
- [ ] Xem cycle time tren board header neu o Kanban.

### Sprint / Backlog / Roadmap
- [ ] Vao `Backlog`, tao sprint moi.
- [ ] Start sprint.
- [ ] Complete sprint.
- [ ] Bat `Group by Epic` trong backlog, collapse/expand tung group.
- [ ] Vao `Roadmap`, loc theo sprint / assignee.
- [ ] Zoom timeline roadmap bang chuot.
- [ ] Keo thanh epic de doi `StartDate` / `DueDate`.
- [ ] Double click epic de mo `IssueDetails`.

### Bao cao
- [ ] Mo `Reports`.
- [ ] Xem `Burndown Chart`.
- [ ] Xem `Velocity Chart`.
- [ ] Xem `Cumulative Flow Diagram`.
- [ ] Xem `Sprint Report` voi sprint da close.
- [ ] Thu `Export PNG` tren moi tab report.

## Luong test cho Developer
### Xu ly issue hang ngay
- [ ] Tao issue moi trong `IssueEditor`.
- [ ] Dat `Due date`, bo chon due date, luu lai.
- [ ] Chon `Epic Link` khi tao Story/Task.
- [ ] Mo `IssueDetails` va sua mo ta bang markdown editor.
- [ ] Toggle `Edit / Preview` trong phan mo ta.
- [ ] Them `Labels`, `Component`, `Fix Version`.
- [ ] Theo doi issue bang nut `Watch`, xem watcher count tang/giam.
- [ ] Them comment, mention user bang `@` neu co ho tro.
- [ ] Upload / download / xoa attachment.
- [ ] Log time.
- [ ] Chuyen trang thai issue trong dropdown status.
- [ ] Xac nhan due date overdue hien mau do tren card va trong detail panel.

### Tim kiem va bo loc
- [ ] Mo `Issues` / navigator.
- [ ] Nhap JQL mau: `assignee = currentUser() AND priority in (High, Highest)`.
- [ ] Kiem tra autocomplete field/operator/value.
- [ ] Luu filter moi.
- [ ] Mo lai filter tu sidebar `Filters`.

## Notification / Email / Webhook
- [ ] Gan issue cho user khac va xac nhan co notification in-app.
- [ ] Doi status issue va xac nhan watcher nhan notification.
- [ ] Them comment va xac nhan co notification.
- [ ] Neu SMTP da cau hinh, xac nhan email thong bao duoc gui.
- [ ] Neu webhook da cau hinh, xac nhan delivery duoc tao sau cac su kien write.

## API token / Local API
- [ ] Tao API token trong `Profile`.
- [ ] Copy token mot lan duy nhat.
- [ ] Goi `GET /api/v1/issues?projectKey=JIRA` voi Bearer token.
- [ ] Goi `POST /api/v1/issues` va xac nhan issue moi duoc tao.

## SSO / Bao mat
- [ ] Neu OAuth da bat, bam `Dang nhap voi SSO`.
- [ ] Dang nhap bang trinh duyet he thong va quay ve app.
- [ ] Xac nhan nonce / issuer / JWKS validation khong loi.

## Tich hop GitHub / Confluence
- [ ] Cau hinh GitHub repo.
- [ ] Tao commit / PR co chua key issue dang `[JIRA-1]`.
- [ ] Xac nhan issue hien section `Commits` / `Pull Requests`.
- [ ] Cau hinh Confluence.
- [ ] Them page link vao issue.
- [ ] Bam `Create Confluence Page` va mo link trang vua tao.

## Ghi chu ket qua test
- [ ] Ghi lai bug theo mau: man hinh, buoc tai hien, ket qua thuc te, ket qua mong doi.
- [ ] Chup anh man hinh voi cac loi ve layout, tieng Anh con sot, hoac thao tac fail.
- [ ] Danh dau ro bug nao block user flow va bug nao co the de sau.
