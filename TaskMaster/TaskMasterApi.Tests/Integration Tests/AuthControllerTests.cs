using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskMasterApi.Contracts.Auth;
using TaskMasterApi.Controllers;
using TaskMasterApi.Tests.TestHelpers;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;
using Domain.Common.Abstract;

namespace TaskMasterApi.Tests.Integration_Tests;

public sealed class AuthControllerTests
{
    private sealed class FakeDbContext() : ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        new FakeCurrentUser());

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public string? UserId => "u1";
    }

    private static UserManager<IdentityUser> NewUserManager(Mock<IUserStore<IdentityUser>>? storeMock = null)
    {
        storeMock ??= new Mock<IUserStore<IdentityUser>>();
        var mgr = new UserManager<IdentityUser>(
            storeMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
        return mgr;
    }

    private static SignInManager<IdentityUser> NewSignInManager(UserManager<IdentityUser> userManager)
    {
        var contextAccessor = new Mock<IHttpContextAccessor>();
        contextAccessor.SetupGet(a => a.HttpContext).Returns(HttpContextHelper.NewContext());
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<IdentityUser>>();
        return new SignInManager<IdentityUser>(
            userManager, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!);
    }

    [Fact]
    public async Task Login_BadRequest_When_Missing_Fields()
    {
        var userMgr = NewUserManager();
        var signInMgr = NewSignInManager(userMgr);
        await using var db = new FakeDbContext();

        var controller = new AuthController(userMgr, signInMgr, db);
        var res = await controller.Login(new LoginRequest { UserName = "", Password = "" });

        res.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_Returns_Locked_With_RetryAfter_Header_When_LockedOut()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        var userMgr = new Mock<UserManager<IdentityUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signInMgr = new Mock<SignInManager<IdentityUser>>(
            userMgr.Object,
            Mock.Of<IHttpContextAccessor>(a => a.HttpContext == HttpContextHelper.NewContext()),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);

        var user = new IdentityUser { UserName = "demo", Id = "user-1" };
        userMgr.Setup(m => m.FindByNameAsync("demo")).ReturnsAsync(user);
        userMgr.Setup(m => m.GetLockoutEndDateAsync(user)).ReturnsAsync(DateTimeOffset.UtcNow.AddSeconds(25));

        signInMgr.Setup(m => m.PasswordSignInAsync("demo", "pw", false, true))
                 .ReturnsAsync(SignInResult.LockedOut);

        await using var db = new FakeDbContext();

        var controller = new AuthController(userMgr.Object, signInMgr.Object, db)
        {
            ControllerContext = new ControllerContext { HttpContext = HttpContextHelper.NewContext() }
        };

        var result = await controller.Login(new LoginRequest { UserName = "demo", Password = "pw" })
                     as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status423Locked);
        controller.Response.Headers.ContainsKey("Retry-After").Should().BeTrue();
        int.TryParse(controller.Response.Headers["Retry-After"].ToString(), out var retry).Should().BeTrue();
        retry.Should().BeGreaterThanOrEqualTo(0);
    }
    [Fact]
    public async Task Login_Ok_On_Success()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        var userMgr = new Mock<UserManager<IdentityUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signInMgr = new Mock<SignInManager<IdentityUser>>(
            userMgr.Object,
            Mock.Of<IHttpContextAccessor>(a => a.HttpContext == HttpContextHelper.NewContext()),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);

        signInMgr.Setup(m => m.PasswordSignInAsync("demo", "pw", false, true))
                 .ReturnsAsync(SignInResult.Success);

        await using var db = new FakeDbContext();

        var controller = new AuthController(userMgr.Object, signInMgr.Object, db);
        var result = await controller.Login(new LoginRequest { UserName = "demo", Password = "pw" });

        result.Should().BeOfType<OkObjectResult>();
    }
}
