# NotificationWorker

**Port:** none (worker only) | **DB:** none | **Phase:** 1

## Responsibility
Pure event consumer. Sends emails and (Phase 3) push notifications.
No HTTP endpoints. No database. Stateless.

## Events consumed → actions

| Event | Action |
|---|---|
| `UserRegistered` | Welcome email |
| `UserEnrolled` | Enrollment confirmation email |
| `CourseCompleted` | Completion congratulations email |
| `CredentialIssued` | Certificate ready email + LinkedIn prompt |
| `LiveSessionAttended` (Phase 2) | Attendance confirmation |
| `AchievementUnlocked` (Phase 2) | Badge earned notification |
| `StreakBroken` (Phase 2) | Streak reminder email |
| `LearnerAtRisk` (Phase 3) | Instructor alert email |
| `TrainingOverdue` (Phase 3) | Escalation email |
| `TranslationJobCompleted` (Phase 4) | Review prompt to instructor |

## Email provider
Phase 1: SMTP via `MailKit`.
Phase 2+: configurable via `IEmailSender` abstraction (SendGrid, Mailgun, SES).

## Setup

```csharp
// Program.cs — worker, no HTTP pipeline
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumers(Assembly.GetExecutingAssembly());
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq"));
        cfg.UseMessageRetry(r => { r.Immediate(3); r.Interval(3, TimeSpan.FromSeconds(30)); });
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Build().Run();
```
