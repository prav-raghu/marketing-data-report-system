using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScheduleApi.Jobs;
using ScheduleApi.Services;
using Xunit;

namespace ScheduleApi.Tests.Services;

public sealed class CronSchedulerHostedServiceTests
{
    [Fact]
    public async Task StartAsync_RunsWebhookProcessorJobImmediately_WithoutWaitingForTimerInterval()
    {
        var scopeCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(() =>
            {
                scopeCreated.TrySetResult();
                throw new InvalidOperationException("no scope needed for this test");
            });
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var job = new WebhookProcessorJob(scopeFactory.Object, httpClientFactory.Object);
        var service = new CronSchedulerHostedService(job);

        await service.StartAsync(CancellationToken.None);
        try
        {
            await scopeCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        scopeFactory.Verify(f => f.CreateScope(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_CompletesWithoutThrowing_AfterStart()
    {
        var scopeCreated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory
            .Setup(f => f.CreateScope())
            .Returns(() =>
            {
                scopeCreated.TrySetResult();
                throw new InvalidOperationException("no scope needed for this test");
            });
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var job = new WebhookProcessorJob(scopeFactory.Object, httpClientFactory.Object);
        var service = new CronSchedulerHostedService(job);

        await service.StartAsync(CancellationToken.None);
        await scopeCreated.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await service.StopAsync(CancellationToken.None);
    }
}
