using System.Collections.Concurrent;
using System.Reflection;
using Scriban;

namespace LMS.NotificationWorker.Templates;

public sealed class ScribanEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ConcurrentDictionary<string, Template> _cache = new();
    private readonly ILogger<ScribanEmailTemplateRenderer> _logger;

    public ScribanEmailTemplateRenderer(ILogger<ScribanEmailTemplateRenderer> logger)
    {
        _logger = logger;
    }

    public async Task<(string Subject, string HtmlBody, string TextBody)> RenderAsync(
        string templateName, object model, CancellationToken ct = default)
    {
        var subject = await RenderPartAsync(templateName, "subject.txt", model, ct);
        var htmlBody = await RenderPartAsync(templateName, "html.scriban", model, ct);
        var textBody = await RenderPartAsync(templateName, "txt.scriban", model, ct);
        return (subject.Trim(), htmlBody, textBody);
    }

    private async Task<string> RenderPartAsync(string templateName, string suffix, object model, CancellationToken ct)
    {
        var resourceName = $"LMS.NotificationWorker.Templates.Email.{templateName}.{suffix}";
        var cacheKey = resourceName;

        var template = _cache.GetOrAdd(cacheKey, key =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(key)
                ?? throw new InvalidOperationException($"Template '{templateName}' not found (resource: {key})");
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            // subject.txt files are not Scriban templates; parse as plain text (no error)
            return suffix == "subject.txt"
                ? Template.Parse(content)
                : Template.Parse(content);
        });

        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages);
            throw new InvalidOperationException($"Template '{templateName}.{suffix}' has parse errors: {errors}");
        }

        var scriptObject = new Scriban.Runtime.ScriptObject();
        scriptObject.SetValue("model", model, readOnly: true);

        var context = new Scriban.TemplateContext { MemberRenamer = member => member.Name.ToLowerInvariant() };
        context.PushGlobal(scriptObject);

        var result = await template.RenderAsync(context);
        return result;
    }
}
