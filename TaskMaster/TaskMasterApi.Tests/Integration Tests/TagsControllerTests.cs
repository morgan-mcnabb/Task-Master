using Domain.Abstract.Repositories;
using Domain.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskMasterApi.Controllers;
using TaskMasterApi.Mapping;
using TaskMasterApi.Tests.TestHelpers;

namespace TaskMasterApi.Tests.Integration_Tests;

public sealed class TagsControllerTests
{
    [Fact]
    public async Task Search_Normalizes_Limit_And_Maps()
    {
        var repo = new Mock<ITagRepository>();
        var mapper = new TaskMapper();

        var tags = new[]
        {
            new Tag { Id = Guid.NewGuid(), Name = "life", NormalizedName = "LIFE", OwnerUserId = "u1" },
            new Tag { Id = Guid.NewGuid(), Name = "work", NormalizedName = "WORK", OwnerUserId = "u1" },
        };

        repo.Setup(r => r.SearchAsync("w", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tags);

        var controller = new TagsController(repo.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextHelper.NewContext() }
        };

        var result = await controller.Search("w", limit: 999, CancellationToken.None) as OkObjectResult;
        result.Should().NotBeNull();

        var dtos = (result!.Value as IReadOnlyList<TaskMasterApi.Contracts.Tags.TagDto>)!;
        dtos.Select(d => d.Name).Should().BeEquivalentTo("life", "work");
    }

    [Fact]
    public async Task Search_Clamps_Low_Limit_To_1()
    {
        var repo = new Mock<ITagRepository>();
        var mapper = new TaskMapper();

        repo.Setup(r => r.SearchAsync(null, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Tag>());

        var controller = new TagsController(repo.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextHelper.NewContext() }
        };

        var result = await controller.Search(null, limit: 0, CancellationToken.None) as OkObjectResult;
        result.Should().NotBeNull();
    }
}