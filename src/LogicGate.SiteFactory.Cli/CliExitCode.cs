namespace LogicGate.SiteFactory.Cli;

// docs/03-generation/15-cli-specification.md, section 26.
internal static class CliExitCode
{
    public const int Success = 0;
    public const int GeneralError = 1;
    public const int InvalidArguments = 2;
    public const int InvalidConfiguration = 3;
    public const int InvalidCatalog = 4;
    public const int NonExecutablePlan = 5;
    public const int FileConflict = 6;
    public const int GenerationFailed = 7;
    public const int EnvironmentInvalid = 11;
    public const int Cancelled = 12;
    public const int UnsupportedVersion = 15;
}
