using Application.Abstract.Services;
using Application.Common.Exceptions;
using Application.Tasks.Models;
using Domain.Abstract.Persistence;
using Domain.Abstract.Repositories;
using Domain.Common;
using Domain.Common.Abstract;
using Domain.Tasks;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace Application.Services;

public sealed class TaskService(
    ITaskRepository taskRepository,
    ITagRepository tagRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ITaskService
{

    public Task<PagedResult<TaskItem>> SearchAsync(Domain.Tasks.Queries.TaskQuery criteria, CancellationToken cancellationToken)
        => taskRepository.SearchAsync(criteria, cancellationToken);

    public Task<TaskItem?> GetByIdAsync(Guid taskId, bool includeTags, CancellationToken cancellationToken)
        => taskRepository.GetByIdAsync(taskId, includeTags, asTracking: false, cancellationToken);
    
    public async Task<TaskItem> CreateAsync(CreateTaskModel model, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var newTask = TaskItem.Create(
            ownerUserId: currentUser.UserId!,
            title: model.Title.Trim(),
            description: string.IsNullOrWhiteSpace(model.Description) ? null : model.Description,
            priority: model.Priority,
            dueDate: model.DueDate,
            utcNow: utcNow);

        if (model.Tags.Count > 0)
        {
            var ensuredTags = await tagRepository.EnsureTagsExistAsync(model.Tags, cancellationToken);
            SyncTags(newTask, ensuredTags);
        }

        await taskRepository.AddAsync(newTask, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return newTask;
    }

    public async Task<TaskItem> UpdateAsync(Guid taskId, UpdateTaskModel model, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var task = await taskRepository.GetByIdAsync(taskId, includeTags: true, asTracking: true, cancellationToken);
        if (task is null)
            throw new NotFoundException($"Task {taskId} not found.");

        if (model.IfMatchRowVersion is not null &&
            !RowVersionEquals(model.IfMatchRowVersion, task.RowVersion))
        {
            throw new ConcurrencyException("The task has been modified by another process.");
        }

        var title = model.Title ?? task.Title;
        var description = model.Description ?? task.Description;
        var priority = model.Priority ?? task.Priority;
        var dueDate = model.DueDate ?? task.DueDate;

        task.UpdateCoreDetails(title, description, priority, dueDate);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        if (model.Status is not null)
        {
            switch (model.Status.Value)
            {
                case TaskStatus.Todo:
                    task.MarkTodo();
                    break;
                case TaskStatus.InProgress:
                    task.MarkInProgress();
                    break;
                case TaskStatus.Done:
                    task.MarkDone(utcNow);
                    break;
                case TaskStatus.Archived:
                    task.Archive();
                    break;
            }
        }

        if (model.Tags is not null)
        {
            var ensuredTags = await tagRepository.EnsureTagsExistAsync(model.Tags, cancellationToken);
            SyncTags(task, ensuredTags);
        }

        taskRepository.Update(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return task;
    }

    public async Task DeleteAsync(Guid taskId, byte[]? ifMatchRowVersion, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var task = await taskRepository.GetByIdAsync(taskId, includeTags: false, asTracking: true, cancellationToken);
        if (task is null)
            throw new NotFoundException($"Task {taskId} not found.");

        if (ifMatchRowVersion is not null &&
            !RowVersionEquals(ifMatchRowVersion, task.RowVersion))
        {
            throw new ConcurrencyException("The task has been modified by another process.");
        }

        taskRepository.Remove(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
    
    private void EnsureAuthenticated()
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
            throw new UnauthorizedAccessException("Authentication is required.");
    }

    private static bool RowVersionEquals(byte[] a, byte[] b)
        => a.AsSpan().SequenceEqual(b);

    private static void SyncTags(TaskItem task, IReadOnlyList<Tag> desiredTags)
    {
        var desiredIds = desiredTags.Select(t => t.Id).ToHashSet();

        var toRemove = task.TaskTags.Where(link => !desiredIds.Contains(link.TagId)).ToList();
        foreach (var link in toRemove)
            task.TaskTags.Remove(link);

        var currentIds = task.TaskTags.Select(link => link.TagId).ToHashSet();
        foreach (var tag in desiredTags)
        {
            if (!currentIds.Contains(tag.Id))
            {
                task.TaskTags.Add(new TaskTag
                {
                    TaskId = task.Id,
                    TagId = tag.Id,
                    Tag = tag
                });
            }
        }
    }
}
