using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sportarr.Api.Services;

/// <summary>
/// One-shot startup service that recovers the task queue after a process
/// restart: requeues orphan Running rows and kicks ProcessQueueAsync so the
/// drain loop resumes. Without this, queues built up before a restart sit
/// idle until the next manual user action triggers a new task.
/// </summary>
public class TaskQueueRecoveryService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskQueueRecoveryService> _logger;

    public TaskQueueRecoveryService(IServiceScopeFactory scopeFactory, ILogger<TaskQueueRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
            await taskService.RecoverAndResumeAsync();
            _logger.LogInformation("[TASK] Queue recovery on startup complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TASK] Queue recovery on startup failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
