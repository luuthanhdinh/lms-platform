Set up or run a BenchmarkDotNet benchmark: $ARGUMENTS

Argument: `<TypeName>.<MethodName>` or a free-text target.

1. Locate the method. If it doesn't have a paired benchmark, create
   one in `tests/LMS.Benchmarks/` (create the project if missing,
   add to solution, reference the target service's project).

2. Benchmark template:

   ```csharp
   [MemoryDiagnoser]
   [SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
   public class CourseQueryBenchmarks
   {
       private CourseDbContext _db = default!;

       [GlobalSetup] public async Task Setup() { /* seed */ }

       [Benchmark(Baseline = true)]
       public Task<List<Course>> Current() => _db.Courses.ToListAsync();

       [Benchmark]
       public Task<List<CourseDto>> Projected() =>
           _db.Courses.Select(c => new CourseDto(...)).ToListAsync();
   }
   ```

3. Run in Release mode:
   ```bash
   dotnet run --project tests/LMS.Benchmarks -c Release \
     -- --filter "*CourseQueryBenchmarks*"
   ```

4. Surface: ns/op, allocations, ratio vs baseline. Save report to
   `tests/LMS.Benchmarks/Results/{YYYYMMDD}-{slug}.md`.

Rules:
- Always include `[MemoryDiagnoser]` — allocations matter as much as time
- Compare to a baseline (`Baseline = true`) — absolute numbers lie
- Never benchmark in Debug; never benchmark with a debugger attached
- Don't bench EF Core against an in-memory provider — use Testcontainers
  Postgres, same as integration tests
