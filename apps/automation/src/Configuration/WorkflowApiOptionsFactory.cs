namespace WorkflowApi.Configuration;

public static class WorkflowApiOptionsFactory
{
    public static WorkflowApiOptions Load(IConfiguration configuration)
    {
        var options = new WorkflowApiOptions
        {
            DatabaseUrl = configuration["DATABASE_URL"] ?? string.Empty,
            Port = int.TryParse(configuration["PORT"], out var port) ? port : 0,
            NodeEnv = configuration["NODE_ENV"] ?? "development",
            CorsOrigin = configuration["CORS_ORIGIN"] ?? string.Empty,
        };

        var result = new WorkflowApiOptionsValidator().Validate(options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid environment variables: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }

        return options;
    }
}
