using System.Net.Mime;
using Application.Abstract.Services;
using Application.Common.Exceptions;
using Application.Tasks.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskMasterApi.Contracts.Common;
using TaskMasterApi.Contracts.Tasks;
using TaskMasterApi.Mapping;
using Microsoft.EntityFrameworkCore;

namespace TaskMasterApi.Controllers;

[ApiController]
[Route("api/v1/tasks")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public sealed class TasksController(
    ITaskService taskService,
    ITaskMapper mapper) : ControllerBase
{
    // ---------------------------
    // GET /api/v1/tasks
    // ---------------------------
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Search([FromQuery] TaskQueryRequest request, CancellationToken cancellationToken)
    {
        var criteria = mapper.ToDomainQuery(request);
        var page = await taskService.SearchAsync(criteria, cancellationToken);
        var response = mapper.ToPagedResponse(page);
        return Ok(response);
    }

    // ---------------------------
    // GET /api/v1/tasks/{id}
    // ---------------------------
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var entity = await taskService.GetByIdAsync(id, includeTags: true, cancellationToken);
        if (entity is null)
            return NotFound();

        var dto = mapper.ToTaskDto(entity);
        SetETagHeader(dto.ETag);
        return Ok(dto);
    }

    // ---------------------------
    // POST /api/v1/tasks
    // ---------------------------
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        // Model validated by FluentValidationActionFilter
        var model = new CreateTaskModel
        {
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            DueDate = request.DueDate,
            Tags = request.Tags
        };

        var created = await taskService.CreateAsync(model, cancellationToken);
        var dto = mapper.ToTaskDto(created);

        SetETagHeader(dto.ETag);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    // ---------------------------
    // PUT /api/v1/tasks/{id}
    // (idempotent, partial fields are okay)
    // Requires If-Match header (or body ETag) for optimistic concurrency.
    // ---------------------------
    [HttpPut("{id:guid}")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)] // If-Match mismatch
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)] // If-Match missing
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        // Enforce precondition: ETag must be supplied via If-Match header or request body.
        var ifMatchRowVersion = DecodeIfMatchOrBodyETag(request.ETag);
        if (ifMatchRowVersion is null)
        {
            return Problem(
                title: "Precondition Required",
                detail: "Provide an If-Match header (preferred) or an ETag in the request body. Use the ETag from the latest GET response.",
                statusCode: StatusCodes.Status428PreconditionRequired);
        }

        try
        {
            var model = new UpdateTaskModel
            {
                Title = request.Title,
                Description = request.Description,
                Priority = request.Priority,
                DueDate = request.DueDate,
                Status = request.Status,
                Tags = request.Tags,
                IfMatchRowVersion = ifMatchRowVersion
            };

            var updated = await taskService.UpdateAsync(id, model, cancellationToken);
            var dto = mapper.ToTaskDto(updated);

            SetETagHeader(dto.ETag);
            return Ok(dto);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ConcurrencyException ex)
        {
            return Problem(
                title: "Precondition Failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status412PreconditionFailed);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                title: "Precondition Failed",
                detail: "The task was modified by another request. Refresh and retry with the latest ETag.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }
    }

    // ---------------------------
    // PATCH /api/v1/tasks/{id}
    // (same shape as PUT; PATCH supports partial updates explicitly)
    // Enforces the same If-Match requirement through Update().
    // ---------------------------
    [HttpPatch("{id:guid}")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Patch([FromRoute] Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
        => Update(id, request, cancellationToken); // DRY: reuse PUT logic

    // ---------------------------
    // DELETE /api/v1/tasks/{id}
    // Requires If-Match header for optimistic concurrency.
    // ---------------------------
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        // DELETE must use If-Match header; bodies are uncommon and disabled here.
        var ifMatchRowVersion = DecodeIfMatchOrBodyETag(bodyEtag: null);
        if (ifMatchRowVersion is null)
        {
            return Problem(
                title: "Precondition Required",
                detail: "DELETE requires an If-Match header containing the current entity ETag.",
                statusCode: StatusCodes.Status428PreconditionRequired);
        }

        try
        {
            await taskService.DeleteAsync(id, ifMatchRowVersion, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ConcurrencyException ex)
        {
            return Problem(
                title: "Precondition Failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status412PreconditionFailed);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                title: "Precondition Failed",
                detail: "The task was modified by another request. Refresh and retry with the latest ETag.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }
    }

    // ---------------------------
    // Helpers
    // ---------------------------
    private byte[]? DecodeIfMatchOrBodyETag(string? bodyEtag)
    {
        // Prefer the standard If-Match header; fall back to body ETag if provided.
        var ifMatchHeader = Request.Headers.IfMatch.FirstOrDefault();
        var etag = string.IsNullOrWhiteSpace(ifMatchHeader)
            ? bodyEtag
            : TrimEtagQuotes(ifMatchHeader);

        return mapper.FromETag(etag);
    }

    private static string? TrimEtagQuotes(string? etag)
        => string.IsNullOrWhiteSpace(etag) ? etag : etag.Trim().Trim('"');

    private void SetETagHeader(string? etagBase64)
    {
        if (string.IsNullOrWhiteSpace(etagBase64)) return;

        // RFC7232: strong validator with quotes.
        Response.Headers.ETag = $"\"{etagBase64}\"";
    }
}
