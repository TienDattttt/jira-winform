# Deployment Checklist

Use this checklist before shipping Jira Desktop to a production environment.

## Pre-Deploy

- [ ] DB: run `dotnet ef database update --project src/JiraClone.Persistence/JiraClone.Persistence.csproj --startup-project src/JiraClone.WinForms/JiraClone.WinForms.csproj` against the target environment before first launch.
- [ ] App settings: verify `src/JiraClone.WinForms/appsettings.json` and environment overrides contain the correct connection string, attachment storage path, SMTP settings, OAuth settings, and any integration-specific values.
- [ ] Secrets: confirm `appsettings.Development.json` or production secret injection contains SMTP credentials, OAuth client configuration, GitHub token, and Confluence API token where applicable.

## Operator Smoke Tests

- [ ] SMTP: send a test email notification to an administrator address and confirm delivery plus correct HTML rendering.
- [ ] SSO: fill `Issuer`, `JwksUri`, and `ClientId` in config, then complete a real OAuth login round-trip from the desktop app.
- [ ] GitHub integration: verify the configured token can access the target owner/repo and that commit or PR linking syncs into issue activity.
- [ ] Confluence integration: verify the configured API token, email, and space key can create and link a page from an issue.
- [ ] Webhook: create a test endpoint, trigger a project or issue event, and verify the request arrives with a valid `X-Jira-Desktop-Signature` HMAC header.
- [ ] API Token: generate a token in Profile settings, then run a local `curl` or equivalent request against `GET /api/v1/issues?projectKey=PROJ` and confirm a JSON response.

## Post-Deploy Verification

- [ ] Launch the app, confirm remembered-session restore works, and verify logout clears the local session file.
- [ ] Move an issue card, add a comment, start/close a sprint, and confirm the UI stays responsive while background webhook delivery and notifications continue working.
- [ ] Open Reports, Dashboard, Roadmap, Project Settings, and Issue Details to confirm the main production surfaces render without runtime errors.
- [ ] Review the latest application log file for startup, migration, webhook, OAuth, SMTP, and integration errors before handing the build to users.
