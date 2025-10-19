using Domain.Common.Abstract;
using Domain.Tasks;
using FluentAssertions;

namespace Domain.Tests.Unit_Tests;

public sealed class TagTests
{
    [Fact]
    public void Implements_IAuditableEntity()
    {
        var tag = new Tag();
        (tag is IAuditableEntity).Should().BeTrue();
    }

    [Fact]
    public void Defaults_Are_Reasonable()
    {
        var tag = new Tag();

        tag.Id.Should().NotBeEmpty();
        tag.TaskTags.Should().NotBeNull();
        tag.TaskTags.Should().BeEmpty();

        // String props are non-nullable but not initialized; tests just verify we can set them.
        tag.OwnerUserId = "user-1";
        tag.Name = "Work";
        tag.NormalizedName = "WORK";

        tag.OwnerUserId.Should().Be("user-1");
        tag.Name.Should().Be("Work");
        tag.NormalizedName.Should().Be("WORK");
    }
}