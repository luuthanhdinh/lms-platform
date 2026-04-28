using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;
using Polly.Retry;

namespace LMS.NotificationWorker.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<MailKit.Net.Smtp.SmtpCommandException>()
                    .Handle<IOException>(),
                MaxRetryAttempts = 3,
                DelayGenerator = static args =>
                {
                    var delay = args.AttemptNumber switch
                    {
                        0 => TimeSpan.FromSeconds(1),
                        1 => TimeSpan.FromSeconds(3),
                        _ => TimeSpan.FromSeconds(9),
                    };
                    return ValueTask.FromResult<TimeSpan?>(delay);
                },
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "SMTP send attempt {Attempt} failed, retrying...", args.AttemptNumber + 1);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        };
        mime.Body = builder.ToMessageBody();

        await _retryPipeline.ExecuteAsync(async token =>
        {
            using var client = new SmtpClient();
            var socketOptions = _options.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, token);

            if (_options.UseAuth && _options.Username is not null && _options.Password is not null)
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, token);
            }

            await client.SendAsync(mime, token);
            await client.DisconnectAsync(true, token);

            _logger.LogInformation("Email sent with subject {Subject}", message.Subject);
        }, ct);
    }
}
