using System.Collections.Generic;
using Domain.Common;
using FluentAssertions;
using Xunit;

namespace Domain.Tests.Unit_Tests;

public sealed class PagedResultTests
{
    [Fact]
    public void Record_Sets_Properties()
    {
        var items = new List<int> { 1, 2, 3 };
        var page = new PagedResult<int>(items, 42, 2, 3);

        page.Items.Should().BeSameAs(items);
        page.TotalCount.Should().Be(42);
        page.PageNumber.Should().Be(2);
        page.PageSize.Should().Be(3);
    }

    [Fact]
    public void Structural_Equivalence_Is_True_When_Data_Matches()
    {
        var a = new PagedResult<string>(["a"], 10, 1, 10);
        var b = new PagedResult<string>(["a"], 10, 1, 10);

        a.Should().BeEquivalentTo(b);
    }
}