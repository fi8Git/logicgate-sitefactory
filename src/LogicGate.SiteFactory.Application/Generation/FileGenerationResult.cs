namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>A reported outcome correlated to a planned file by its stable ID, without a physical path or content.</summary>
public sealed record FileGenerationResult
{
    public FileGenerationResult(PlannedFileId fileId, FileGenerationStatus status, GenerationExecutionError? error = null)
        : this(fileId, status, error, null)
    {
    }

    public FileGenerationResult(PlannedFileId fileId, FileGenerationStatus status, GenerationExecutionError? error, FileGenerationObservation? observation)
    {
        ArgumentNullException.ThrowIfNull(fileId);
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool requiresError = status is FileGenerationStatus.Conflict or FileGenerationStatus.Failed;
        if (requiresError != (error is not null))
        {
            throw new ArgumentException("Exactly conflicts and failures require an error explanation.", nameof(error));
        }

        if (observation is not null && observation.File.Id != fileId)
        {
            throw new ArgumentException("The observation must refer to the reported file.", nameof(observation));
        }

        FileId = fileId;
        Status = status;
        Error = error;
        Observation = observation;
    }

    public PlannedFileId FileId { get; }

    public FileGenerationStatus Status { get; }

    public GenerationExecutionError? Error { get; }

    /// <summary>Absent for legacy callers; report production requires observations for every reported file.</summary>
    public FileGenerationObservation? Observation { get; }
}
