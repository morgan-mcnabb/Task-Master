using System.Text;
using Application.Abstract.Services;
using Application.Common.Exceptions;
using Application.Tasks.Models;
using Domain.Common;
using Domain.Tasks;
using Domain.Tasks.Queries;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TaskMasterApi.Contracts.Tasks;
using TaskMasterApi.Controllers;
using TaskMasterApi.Mapping;
using TaskMasterApi.Tests.TestHelpers;

namespace TaskMasterApi.Tests.Integration_Tests;

public sealed class TasksControllerTests
{
    private static TaskItem NewTask(byte[]? rowVersion = null)
    {
        var t = new TaskItem
        {
            Id = Guid.NewGuid(),
            OwnerUserId = "u1",
            Title = "title",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            RowVersion = rowVersion ?? Encoding.ASCII.GetBytes("rv-123456")
        };
        return t;
    }

    [Fact]
    public async Task Search_Returns_Ok_With_PagedResponse()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();

        var items = new[] { NewTask() };
        svc.Setup(s => s.SearchAsync(It.IsAny<TaskQuery>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new PagedResult<TaskItem>(items, 1, 1, 20));

        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextHelper.NewContext() }
        };

        var result = await controller.Search(new TaskQueryRequest(), CancellationToken.None) as OkObjectResult;
        result.Should().NotBeNull();
        var body = result!.Value!;
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_NotFound_When_Service_Returns_Null()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();

        svc.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
           .ReturnsAsync((TaskItem?)null);

        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextHelper.NewContext() }
        };

        var res = await controller.GetById(Guid.NewGuid(), CancellationToken.None);
        res.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_Sets_ETag_Header_On_Success()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();
        var t = NewTask(Encoding.ASCII.GetBytes("rowv1234"));

        svc.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
           .ReturnsAsync(t);

        var http = HttpContextHelper.NewContext();
        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var res = await controller.GetById(Guid.NewGuid(), default) as OkObjectResult;
        res.Should().NotBeNull();
        http.Response.Headers.ETag.Should().NotBeEmpty();
        http.Response.Headers.ETag.ToString().Should().StartWith("\"").And.EndWith("\"");
    }

    [Fact]
    public async Task Create_Returns_Created_And_Sets_ETag()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();
        var created = NewTask();

        svc.Setup(s => s.CreateAsync(It.IsAny<CreateTaskModel>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(created);

        var http = HttpContextHelper.NewContext();
        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var res = await controller.Create(new CreateTaskRequest { Title = "T" }, default) as CreatedAtActionResult;
        res.Should().NotBeNull();
        http.Response.Headers.ETag.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Update_Returns_428_When_No_IfMatch_And_No_Body_ETag()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();

        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextHelper.NewContext() }
        };

        var res = await controller.Update(Guid.NewGuid(), new UpdateTaskRequest(), default) as ObjectResult;
        res!.StatusCode.Should().Be(StatusCodes.Status428PreconditionRequired);
    }

    [Fact]
    public async Task Update_Uses_IfMatch_Header_And_Returns_Ok()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();
        var updated = NewTask(Encoding.ASCII.GetBytes("rowv5678"));

        svc.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateTaskModel>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(updated);

        var http = HttpContextHelper.NewContext();
        http.Request.Headers.IfMatch = $"\"{Convert.ToBase64String(Encoding.ASCII.GetBytes("rowv1234"))}\"";

        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var res = await controller.Update(Guid.NewGuid(), new UpdateTaskRequest { Title = "x" }, default) as OkObjectResult;
        res.Should().NotBeNull();
        http.Response.Headers.ETag.ToString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Update_Returns_404_On_NotFound()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();

        svc.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateTaskModel>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new NotFoundException("nope"));

        var http = HttpContextHelper.NewContext();
        http.Request.Headers.IfMatch = Convert.ToBase64String(Encoding.ASCII.GetBytes("rv"));

        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var res = await controller.Update(Guid.NewGuid(), new UpdateTaskRequest(), default);
        res.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_Returns_412_On_Concurrency()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();

        svc.Setup(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateTaskModel>(), It.IsAny<CancellationToken>()))
           .ThrowsAsync(new ConcurrencyException("boom"));

        var http = HttpContextHelper.NewContext();
        http.Request.Headers.IfMatch = Convert.ToBase64String(Encoding.ASCII.GetBytes("rv"));

        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var res = await controller.Update(Guid.NewGuid(), new UpdateTaskRequest(), default) as ObjectResult;
        res!.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);
    }

    [Fact]
    public async Task Delete_Returns_428_When_Missing_IfMatch()
    {
        var svc = new Mock<ITaskService>();
        var mapper = new TaskMapper();
        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextHelper.NewContext() }
        };

        var res = await controller.Delete(Guid.NewGuid(), default) as ObjectResult;
        res!.StatusCode.Should().Be(StatusCodes.Status428PreconditionRequired);
    }

    [Fact]
    public async Task Delete_NoContent_On_Success()
    {
        var svc = new Mock<ITaskService>();
        svc.Setup(s => s.DeleteAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask);

        var mapper = new TaskMapper();

        var http = HttpContextHelper.NewContext();
        http.Request.Headers.IfMatch = Convert.ToBase64String(Encoding.ASCII.GetBytes("rv"));

        var controller = new TasksController(svc.Object, mapper)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var res = await controller.Delete(Guid.NewGuid(), default);
        res.Should().BeOfType<NoContentResult>();
    }
}
