namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A complete byte snapshot, including valid empty content, or a closed failure.</summary>
public sealed class TemplateContentResult
{
    private readonly byte[] content;

    private TemplateContentResult(byte[] content, TemplateReadFailure? error)
    {
        this.content = content;
        Error = error;
    }

    /// <summary>A defensive copy on each read; even recovering its backing array cannot mutate this result.</summary>
    public ReadOnlyMemory<byte> Content => content.ToArray();
    public TemplateReadFailure? Error { get; }
    public bool IsSuccess => Error is null;

    public static TemplateContentResult Success(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new(content.ToArray(), null);
    }

    public static TemplateContentResult Failure(TemplateReadFailure error)
    {
        if (!Enum.IsDefined(error)) throw new ArgumentOutOfRangeException(nameof(error));
        return new([], error);
    }
}
