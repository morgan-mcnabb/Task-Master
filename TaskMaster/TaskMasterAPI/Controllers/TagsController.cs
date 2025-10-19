using System.Net.Mime;
using Domain.Abstract.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskMasterApi.Contracts.Tags;
using TaskMasterApi.Mapping;

namespace TaskMasterApi.Controllers;

[ApiController]
[Route("api/v1/tags")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public sealed class TagsController(
    ITagRepository tagRepository,
    ITaskMapper mapper) : ControllerBase
{
    /// <summary>
    /// Typeahead search for tags. Returns up to <paramref name="limit"/> results ordered by name.
    /// </summary>
    /// <param name="search">Optional search text. Case-insensitive, matches anywhere in the tag.</param>
    /// <param name="limit">Max results to return (default 10, max 50).</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? search, [FromQuery] int limit = 10, CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 50);
        var tags = await tagRepository.SearchAsync(search, normalizedLimit, cancellationToken);
        var results = tags.Select(mapper.ToTagDto).ToArray();
        return Ok(results);
    }
}