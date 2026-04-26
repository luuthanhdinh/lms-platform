Scaffold a new microservice with the standard 4-project layout: $ARGUMENTS

Argument: `<ServiceShortName>` (e.g. `Course`, `Enrollment`).
The service folder becomes `src/services/LMS.{ServiceShortName}Service/`.

Run via the `db-migrator` + `backend` agents in sequence:

1. Create the four projects (uses `dotnet new` + `dotnet sln add`):

   ```bash
   SVC=src/services/LMS.{Name}Service
   mkdir -p $SVC && cd $SVC

   dotnet new classlib  -n LMS.{Name}Service.Domain
   dotnet new classlib  -n LMS.{Name}Service.Infrastructure
   dotnet new web       -n LMS.{Name}Service.Api
   dotnet new worker    -n LMS.{Name}Service.Migrator
   ```

2. Wire references (one direction only):

   ```bash
   dotnet add LMS.{Name}Service.Infrastructure reference \
     LMS.{Name}Service.Domain ../../LMS.SharedKernel ../../LMS.Contracts
   dotnet add LMS.{Name}Service.Api reference \
     LMS.{Name}Service.Infrastructure ../../LMS.ServiceDefaults
   dotnet add LMS.{Name}Service.Migrator reference \
     LMS.{Name}Service.Infrastructure ../../LMS.ServiceDefaults
   ```

3. Add to root solution:

   ```bash
   dotnet sln ../../LMS.sln add LMS.{Name}Service.Domain \
     LMS.{Name}Service.Infrastructure LMS.{Name}Service.Api \
     LMS.{Name}Service.Migrator
   ```

4. Add NuGet packages per project:
   - `Infrastructure`: `Microsoft.EntityFrameworkCore`,
     `Npgsql.EntityFrameworkCore.PostgreSQL`, `MassTransit.RabbitMQ`,
     `MassTransit.EntityFrameworkCore` (outbox)
   - `Api`: `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`,
     `FluentValidation.AspNetCore`, `Microsoft.FeatureManagement.AspNetCore`
   - `Migrator`: `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`

5. Generate from the templates in
   [.claude/skills/aspire-orchestration/SKILL.md](../skills/aspire-orchestration/SKILL.md)
   and [.claude/skills/dotnet-ef-migrations/SKILL.md](../skills/dotnet-ef-migrations/SKILL.md):
   - `Infrastructure/Persistence/{Name}DbContext.cs` with tenant filter
   - `Infrastructure/Persistence/Configurations/.gitkeep`
   - `Migrator/Program.cs` + `MigrationRunner` BackgroundService
   - `Api/Program.cs` with `AddServiceDefaults`, `MapDefaultEndpoints`,
     `AddNpgsqlDbContext<T>("...")`, `AddMassTransit(...)`

6. Register in AppHost (`src/LMS.AppHost/Program.cs`):

   ```csharp
   var {name}Db = pg.AddDatabase("{name}");
   var {name}Migrator = builder
       .AddProject<Projects.LMS_{Name}Service_Migrator>("{name}-migrator")
       .WithReference({name}Db).WaitFor({name}Db);
   builder.AddProject<Projects.LMS_{Name}Service_Api>("{name}-api")
          .WithReference({name}Db).WithReference(rabbit)
          .WaitForCompletion({name}Migrator);
   ```

7. Add an empty initial migration to verify the toolchain:

   ```bash
   dotnet ef migrations add Initial \
     -p $SVC/LMS.{Name}Service.Infrastructure \
     -s $SVC/LMS.{Name}Service.Migrator -o Migrations
   ```

8. `dotnet build` the whole solution. Run AppHost to verify migrator
   exits 0 and Api comes up healthy.

STOP after scaffold. Do not commit.
