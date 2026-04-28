var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure
var postgres = builder.AddPostgres("postgres").WithPgAdmin();
var mongo    = builder.AddMongoDB("mongo");
var redis    = builder.AddRedis("redis");
var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();
var keycloak = builder.AddKeycloak("keycloak")
                      .WithRealmImport("./keycloak/lms-realm.json");

// Databases — uncomment as each service is scaffolded
// var identityDb    = postgres.AddDatabase("lms_identity");
// var courseDb      = postgres.AddDatabase("lms_courses");
// var contentDb     = mongo.AddDatabase("lms_content");
// var enrollmentDb  = postgres.AddDatabase("lms_enrollment");
// var progressDb    = postgres.AddDatabase("lms_progress");
// var assessmentDb  = postgres.AddDatabase("lms_assessment");
// var certificateDb = postgres.AddDatabase("lms_certificate");

// Gateway — validates JWTs, forwards X-User-Id / X-Tenant-Id / X-Roles
builder.AddProject<Projects.LMS_Gateway>("gateway")
    .WithReference(keycloak)
    .WithReference(redis)
    .WaitFor(keycloak)
    .WaitFor(redis);

// Services — uncomment as each service project is scaffolded
// builder.AddProject<Projects.LMS_IdentityService>("identity")
//     .WithReference(identityDb).WithReference(rabbitmq).WithReference(keycloak);

// builder.AddProject<Projects.LMS_CourseService>("courses")
//     .WithReference(courseDb).WithReference(rabbitmq);

// builder.AddProject<Projects.LMS_ContentService>("content")
//     .WithReference(contentDb).WithReference(rabbitmq);

// builder.AddProject<Projects.LMS_EnrollmentService>("enrollment")
//     .WithReference(enrollmentDb).WithReference(rabbitmq);

// builder.AddProject<Projects.LMS_ProgressService>("progress")
//     .WithReference(progressDb).WithReference(rabbitmq);

// builder.AddProject<Projects.LMS_AssessmentService>("assessment")
//     .WithReference(assessmentDb).WithReference(rabbitmq).WithReference(redis);

// builder.AddProject<Projects.LMS_CertificateService>("certificate")
//     .WithReference(certificateDb).WithReference(rabbitmq);

// builder.AddProject<Projects.LMS_NotificationWorker>("notifications")
//     .WithReference(rabbitmq);

// Frontend — React app via Vite dev server; uncomment once frontend scaffold is present
// var gateway = builder.AddProject<Projects.LMS_Gateway>("gateway"); // reference above
// builder.AddNpmApp("frontend", "../frontend")
//     .WithReference(gateway)
//     .WithEnvironment("VITE_API_BASE_URL", gateway.GetEndpoint("http"))
//     .WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
//     .WithEnvironment("VITE_KEYCLOAK_REALM", "lms")
//     .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "lms-spa")
//     .WithHttpEndpoint(5173, name: "http")
//     .WithExternalHttpEndpoints();

// Phase 2+ infrastructure (uncomment when starting Phase 2 sprint)
// builder.AddContainer("clickhouse", "clickhouse/clickhouse-server", "24")
//        .WithHttpEndpoint(8123, name: "http");
// builder.AddContainer("elasticsearch", "elasticsearch", "8.13.0")
//        .WithEnvironment("discovery.type", "single-node")
//        .WithHttpEndpoint(9200, name: "http");
// builder.AddContainer("eventstoredb", "eventstore/eventstore", "23.10")
//        .WithHttpEndpoint(2113, name: "http");

builder.Build().Run();
