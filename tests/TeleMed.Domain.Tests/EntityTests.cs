using TeleMed.Domain.Common;
using TeleMed.Domain.Entities;

namespace TeleMed.Domain.Tests;

public class EntityTests
{
    private sealed class Sample : Entity;

    [Fact]
    public void New_entities_get_distinct_version7_ids()
    {
        var a = new Sample();
        var b = new Sample();

        a.Id.Version.ShouldBe(7);
        b.Id.ShouldNotBe(a.Id);
    }

    [Fact]
    public void New_users_get_a_version7_id()
    {
        new User().Id.Version.ShouldBe(7);
    }
}
