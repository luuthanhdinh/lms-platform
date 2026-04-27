using LMS.CourseService.Api.Auth;
using LMS.CourseService.Api.Endpoints;
using LMS.CourseService.Domain.Abstractions;
using LMS.CourseService.Infrastructure.Data;
using LMS.CourseService.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CourseDbContext>("lms-courses");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();
builder.Services.AddCourseInfrastructure(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapCourseEndpoints();
app.Run();
