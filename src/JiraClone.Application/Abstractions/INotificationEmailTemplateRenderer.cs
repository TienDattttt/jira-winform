using JiraClone.Application.Models;

namespace JiraClone.Application.Abstractions;

public interface INotificationEmailTemplateRenderer
{
    string Render(NotificationEmailTemplateModel model);
}
