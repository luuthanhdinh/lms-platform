var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure — fixed dev passwords so named volumes survive restarts
var pgPassword       = builder.AddParameter("pg-password",       "lms_dev_pg",       secret: true);
var mongoPassword    = builder.AddParameter("mongo-password",    "lms_dev_mongo",    secret: true);
var redisPassword    = builder.AddParameter("redis-password",    "lms_dev_redis",    secret: true);
var rabbitmqPassword = builder.AddParameter("rabbitmq-password", "lms_dev_rabbitmq", secret: true);

var postgres = builder.AddPostgres("postgres", password: pgPassword).WithPgAdmin().WithDataVolume("lms-postgres-data");
var mongo    = builder.AddMongoDB("mongo", password: mongoPassword).WithDataVolume("lms-mongo-data");
var redis    = builder.AddRedis("redis", password: redisPassword).WithDataVolume("lms-redis-data");
var rabbitmq = builder.AddRabbitMQ("rabbitmq", password: rabbitmqPassword).WithManagementPlugin().WithDataVolume("lms-rabbitmq-data");
var keycloak = builder.AddKeycloak("keycloak", port: 8080)
                      .WithRealmImport("./keycloak/lms-realm.json")
                      .WithDataVolume("lms-keycloak-data");

// Databases — uncomment as each service is scaffolded
var identityDb    = postgres.AddDatabase("lms-identity");
var courseDb      = postgres.AddDatabase("lms-courses");
// var courseDb      = postgres.AddDatabase("lms_courses");
var contentDb     = mongo.AddDatabase("lms-content");
var enrollmentDb  = postgres.AddDatabase("lms-enrollments");
// var assessmentDb  = postgres.AddDatabase("lms_assessment");
// var certificateDb = postgres.AddDatabase("lms_certificate");

// Gateway — validates JWTs, forwards X-User-Id / X-Tenant-Id / X-Roles
var gateway = builder.AddProject<Projects.LMS_Gateway>("gateway")
    .WithEndpoint("http", e => e.Port = 5000)
    .WithReference(keycloak)
    .WithReference(redis)
    .WaitFor(keycloak)
    .WaitFor(redis)
    .WithEnvironment("Keycloak__Authority", $"{keycloak.GetEndpoint("http")}/realms/lms");

// IdentityService
var identityMigrator = builder.AddProject<Projects.LMS_IdentityService_Migrator>("identity-migrator")
    .WithReference(identityDb)
    .WaitFor(identityDb);

var identity = builder.AddProject<Projects.LMS_IdentityService_Api>("identity")
    .WithReference(identityDb)
    .WithReference(rabbitmq)
    .WaitForCompletion(identityMigrator);

gateway.WithReference(identity);

// CourseService
var courseMigrator = builder.AddProject<Projects.LMS_CourseService_Migrator>("course-migrator")
    .WithReference(courseDb)
    .WaitFor(courseDb);

var course = builder.AddProject<Projects.LMS_CourseService_Api>("courses")
    .WithReference(courseDb)
    .WithReference(rabbitmq)
    .WaitForCompletion(courseMigrator);

gateway.WithReference(course);

// ContentService
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var contentBlobs = storage.AddBlobs("content-blobs");

var content = builder.AddProject<Projects.LMS_ContentService_Api>("content")
    .WithReference(contentDb)
    .WithReference(rabbitmq)
    .WithReference(contentBlobs)
    .WaitFor(contentDb)
    .WaitFor(contentBlobs)
    .WaitFor(rabbitmq);

var contentWorker = builder.AddProject<Projects.LMS_ContentService_Worker>("content-worker")
    .WithReference(contentDb)
    .WithReference(rabbitmq)
    .WithReference(contentBlobs)
    .WaitFor(content);

gateway.WithReference(content);

// EnrollmentService
var enrollmentMigrator = builder.AddProject<Projects.LMS_EnrollmentService_Migrator>("enrollment-migrator")
    .WithReference(enrollmentDb)
    .WaitFor(enrollmentDb);

var enrollment = builder.AddProject<Projects.LMS_EnrollmentService_Api>("enrollment")
    .WithReference(enrollmentDb)
    .WithReference(rabbitmq)
    .WaitForCompletion(enrollmentMigrator);

gateway.WithReference(enrollment);

// ProgressService
var progressDb = postgres.AddDatabase("lms-progress");

var progressMigrator = builder.AddProject<Projects.LMS_ProgressService_Migrator>("progress-migrator")
    .WithReference(progressDb)
    .WaitFor(progressDb);

var progress = builder.AddProject<Projects.LMS_ProgressService_Api>("progress")
    .WithReference(progressDb)
    .WithReference(rabbitmq)
    .WaitForCompletion(progressMigrator);

gateway.WithReference(progress);

// AssessmentService
var assessmentDb = postgres.AddDatabase("lms-assessment");

var assessmentMigrator = builder.AddProject<Projects.LMS_AssessmentService_Migrator>("assessment-migrator")
    .WithReference(assessmentDb)
    .WaitFor(assessmentDb);

var assessment = builder.AddProject<Projects.LMS_AssessmentService_Api>("assessment")
    .WithReference(assessmentDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitForCompletion(assessmentMigrator);

gateway.WithReference(assessment);

// CertificateService
var certificateDb = postgres.AddDatabase("lms-certificate");
var certificatePdfs = storage.AddBlobs("certificate-pdfs");

var certificateMigrator = builder.AddProject<Projects.LMS_CertificateService_Migrator>("certificate-migrator")
    .WithReference(certificateDb)
    .WaitFor(certificateDb);

var certificate = builder.AddProject<Projects.LMS_CertificateService_Api>("certificate")
    .WithReference(certificateDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(certificatePdfs)
    .WaitForCompletion(certificateMigrator)
    .WaitFor(certificatePdfs);

gateway.WithReference(certificate);

// NotificationWorker
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog", "v1.0.1")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp")
    .WithEndpoint(port: 8025, targetPort: 8025, name: "ui", scheme: "http");

builder.AddProject<Projects.LMS_NotificationWorker>("notifications")
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithReference(redis).WaitFor(redis)
    .WaitFor(mailhog)
    .WithEnvironment("Smtp__Host", "localhost")
    .WithEnvironment("Smtp__Port", "1025");

// Frontend — React app via Vite dev server
builder.AddNpmApp("frontend", "../frontend", "dev")
    .WithReference(gateway)
    .WaitFor(gateway)
    .WithEnvironment("VITE_API_BASE_URL", gateway.GetEndpoint("http"))
    .WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
    .WithEnvironment("VITE_KEYCLOAK_REALM", "lms")
    .WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "lms-spa")
    .WithEnvironment("BROWSER", "none")
    .WithHttpEndpoint(port: 5173, targetPort: 5173, name: "http", isProxied: false)
    .WithExternalHttpEndpoints();

// Phase 2+ infrastructure (uncomment when starting Phase 2 sprint)
// builder.AddContainer("clickhouse", "clickhouse/clickhouse-server", "24")
//        .WithHttpEndpoint(8123, name: "http");
// builder.AddContainer("elasticsearch", "elasticsearch", "8.13.0")
//        .WithEnvironment("discovery.type", "single-node")
//        .WithHttpEndpoint(9200, name: "http");
// builder.AddContainer("eventstoredb", "eventstore/eventstore", "23.10")
//        .WithHttpEndpoint(2113, name: "http");

builder.Build().Run();
