using System.Globalization;

namespace DotNetMonoRepoTemplate.Ingestion.Lake;

public static class RawZonePath
{
    public static string ForPart(
        string sourceSystem,
        string sourceEntity,
        DateOnly ingestDate,
        string runId,
        int partSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(partSequence);

        return $"{Prefix(sourceSystem, sourceEntity, ingestDate, runId)}/part-{partSequence.ToString("D5", CultureInfo.InvariantCulture)}.json.gz";
    }

    public static string ForOriginalArtifact(
        string sourceSystem,
        string sourceEntity,
        DateOnly ingestDate,
        string runId,
        int artifactSequence,
        string extension)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(artifactSequence);

        var safeExtension = Validate(extension.TrimStart('.'), nameof(extension));
        var sequence = artifactSequence.ToString("D5", CultureInfo.InvariantCulture);

        return $"{Prefix(sourceSystem, sourceEntity, ingestDate, runId)}/original/artifact-{sequence}.{safeExtension}";
    }

    private static string Prefix(string sourceSystem, string sourceEntity, DateOnly ingestDate, string runId) =>
        $"source={Validate(sourceSystem, nameof(sourceSystem))}"
        + $"/entity={Validate(sourceEntity, nameof(sourceEntity))}"
        + $"/ingest_date={ingestDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
        + $"/run_id={Validate(runId, nameof(runId))}";

    private static string Validate(string segment, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment, parameterName);

        foreach (var character in segment)
        {
            var isAllowed = char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.';
            if (!isAllowed)
            {
                throw new ArgumentException(
                    $"Path segment '{segment}' contains an unsupported character. Only letters, digits, underscore, hyphen and dot are allowed.",
                    parameterName);
            }
        }

        if (segment is "." or "..")
        {
            throw new ArgumentException("Path segment may not be a relative path marker.", parameterName);
        }

        return segment;
    }
}
