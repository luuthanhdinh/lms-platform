using LMS.NotificationWorker.Extensions;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNotificationWorkerServices();

var host = builder.Build();
host.Run();
