using TeleMed.Api.IntegrationTests.Infrastructure;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture(typeof(ApiFixture))]
[assembly: Parallelization(Mode = ParallelMode.None)]
