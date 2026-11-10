namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>ADR-0005 preservation rules. Existing client content is never an input to the decision.</summary>
public static class ClientFileStrategy
{
    public static ClientFileDecision Decide(PlannedFile file, bool destinationExists)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Strategy is not (FileGenerationStrategy.CreateOnce or FileGenerationStrategy.Custom or FileGenerationStrategy.Protected))
        {
            throw new ArgumentException("Only create-once, custom and protected strategies are supported.", nameof(file));
        }

        if (file.Strategy == FileGenerationStrategy.CreateOnce)
        {
            if (file.Action is not (PlannedFileAction.Create or PlannedFileAction.Preserve)) return ClientFileDecision.Conflict;
            if (destinationExists) return ClientFileDecision.Preserve;
            return file.Action == PlannedFileAction.Create ? ClientFileDecision.Create : ClientFileDecision.Skip;
        }

        if (file.Action != PlannedFileAction.Preserve) return ClientFileDecision.Conflict;
        if (file.Strategy == FileGenerationStrategy.Protected) return ClientFileDecision.Protect;
        return destinationExists ? ClientFileDecision.Preserve : ClientFileDecision.Skip;
    }
}
