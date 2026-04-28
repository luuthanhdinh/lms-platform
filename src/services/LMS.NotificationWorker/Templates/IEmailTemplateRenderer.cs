namespace LMS.NotificationWorker.Templates;

public interface IEmailTemplateRenderer
{
    Task<(string Subject, string HtmlBody, string TextBody)> RenderAsync(
        string templateName, object model, CancellationToken ct = default);
}
