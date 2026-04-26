Generate a tenant-isolation test pair for an endpoint: $ARGUMENTS

Argument: `<METHOD> <path>` (e.g. `GET /courses/{id}`).

Pattern (from the `tenant-isolation` skill):

1. Locate the endpoint in `src/services/.../Api/`.
2. Identify the test project for the owning service (under `tests/`).
3. Generate a paired test:

   ```csharp
   [Fact]
   public async Task TenantB_cannot_access_TenantA_resource()
   {
       var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());

       // Arrange: tenantA creates / has the resource
       var clientA = _factory.CreateClientAs(tenantA, role: "Instructor");
       var created = await clientA.PostAsJsonAsync("/courses", new { ... });
       created.EnsureSuccessStatusCode();
       var id = (await created.Content.ReadFromJsonAsync<CourseDto>())!.Id;

       // Act: tenantB attempts to access
       var clientB = _factory.CreateClientAs(tenantB, role: "Instructor");
       var resp = await clientB.GetAsync($"/courses/{id}");

       // Assert: 404, not 403 (avoid existence-disclosure)
       resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
   }
   ```

4. For mutating endpoints (PUT/PATCH/DELETE), repeat the pattern
   for each verb.
5. Run the new test → must pass. If it fails, you found a real
   tenant leak — STOP, do NOT silence the test, surface as BLOCKER.
6. Print the file:line of the new test(s).

Read the `tenant-isolation` skill for the underlying invariant and
the audit script.
