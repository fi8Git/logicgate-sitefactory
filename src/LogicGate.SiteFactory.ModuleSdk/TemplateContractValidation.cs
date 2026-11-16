namespace LogicGate.SiteFactory.ModuleSdk;

internal static class TemplateContractValidation
{
    internal static void RequireText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Any(char.IsControl))
            throw new ArgumentException("A non-empty value without control characters is required.", parameterName);
    }
}
