# Desktop Smoke Checklist

Use this checklist before calling the WinForms desktop build ready for review.

## Environment
- [ ] SQL Server is reachable from the configured connection string
- [ ] `dotnet ef database update` completes without migration errors
- [ ] Seed admin user exists
- [ ] Attachments path exists and is writable

## Startup And Login
- [ ] App starts without startup exception
- [ ] Login screen renders without clipped text at common desktop size
- [ ] Invalid login shows a user-facing error
- [ ] Seed admin login succeeds

## Board
- [ ] Main shell opens after login
- [ ] Board loads columns and issue cards without exception
- [ ] Filters for assignee, priority, type, and search work
- [ ] Opening an issue from a card loads issue details
- [ ] Moving an issue changes status and refreshes the board
- [ ] Deleting an issue removes it from the board after refresh

## Issue Details
- [ ] Title inline edit saves correctly
- [ ] Description edit saves correctly
- [ ] Status, priority, sprint, and story points update correctly
- [ ] Multi-assignee selection persists after reopen
- [ ] Adding a comment shows it immediately in the activity area
- [ ] Editing and deleting a comment work correctly
- [ ] Uploading an attachment persists metadata and refreshes the list
- [ ] Downloading and deleting an attachment work correctly
- [ ] History tab shows recent activity entries

## Sprint Workflow
- [ ] Sprint list loads without exception
- [ ] Create sprint succeeds
- [ ] Assign issues to sprint succeeds
- [ ] Start sprint succeeds and marks the sprint active
- [ ] Board active sprint filter shows only active sprint issues
- [ ] Close sprint moves incomplete issues to backlog or selected sprint

## Administration
- [ ] User list loads without exception
- [ ] Create user succeeds and assigns project membership
- [ ] Edit user updates display name, email, and roles
- [ ] Activate/deactivate reflects immediately in the list
- [ ] Reset password completes successfully
- [ ] Project settings save name, description, category, and URL changes
- [ ] Add/change/remove project member succeeds
- [ ] Edit board column updates name and WIP limit

## Final Sanity
- [ ] No unexpected error dialog appears during the above flows
- [ ] Closing and reopening the app preserves migrated schema and seeded access
