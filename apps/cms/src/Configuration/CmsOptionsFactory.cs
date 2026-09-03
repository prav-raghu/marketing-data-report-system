namespace Cms.Configuration;

public static class CmsOptionsFactory
{
    public static CmsOptions Load(IConfiguration configuration)
    {
        var options = new CmsOptions
        {
            DatabaseUrl = configuration["DATABASE_URL"] ?? string.Empty,
            Port = int.TryParse(configuration["PORT"], out var port) ? port : 0,
            NodeEnv = configuration["NODE_ENV"] ?? "development",
        };

        var result = new CmsOptionsValidator().Validate(options);
        if (!result.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid environment variables: {string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        }

        return options;
    }
}
