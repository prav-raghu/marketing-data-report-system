namespace DotNetMonoRepoTemplate.Queue;

public sealed record JobHandlerDefinition<T>(string Name, JobProcessor<T> Processor);

public static class WorkerService
{
    public static void RegisterHandler<T>(string queueName, string jobName, JobProcessor<T> processor) =>
        JobHandlerRegistry.Register(queueName, jobName, processor);

    public static void RegisterHandlers<T>(string queueName, IReadOnlyList<JobHandlerDefinition<T>> handlers)
    {
        foreach (var handler in handlers)
        {
            RegisterHandler(queueName, handler.Name, handler.Processor);
        }
    }
}
