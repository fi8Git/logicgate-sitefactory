using System.Reflection;
using System.Text;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Cli.Commands.Generate;
using LogicGate.SiteFactory.Cli.Commands.Plan;
using LogicGate.SiteFactory.Cli.Commands.Validate;

namespace LogicGate.SiteFactory.Cli;

public static class SiteFactoryCli
{
    private const string HelpText = """
        LogicGate SiteFactory
        Usage: sitefactory [--help | --version]
               sitefactory validate --config <path>
               sitefactory plan --config <path>
               sitefactory generate --config <path> --output <directory>

        Commandes:
          validate   Charge et valide une configuration V1, sans la modifier.
                     Utiliser validate --help pour les options.
          plan       Produit le plan JSON sur stdout, sans écrire de fichier.
                     Utiliser plan --help pour les options.
          generate   Génère ou régénère les fichiers du template sélectionné.
                     Utiliser generate --help pour les limites et options.

        Options:
          --help     Affiche cette aide.
          --version  Affiche la version.
        """;

    private const string UnknownArgumentMessage = "Erreur : argument inconnu.";

    private const string ValidateUsage = "Usage: sitefactory validate --config <path>";
    private const string PlanUsage = "Usage: sitefactory plan --config <path>";
    private const string GenerateUsage = "Usage: sitefactory generate --config <path> --output <directory>";

    private const string GenerateHelpText = """
        Usage: sitefactory generate --config <path> --output <directory>

        Options obligatoires, dans l'un ou l'autre ordre :
          --config <path>       Configuration UTF-8 V1 explicite.
          --output <directory>  Cible locale absente ou reconnue, relative ou absolue.
          --help                Affiche cette aide sans lire ni écrire de fichier.

        Fichiers du template sélectionné, lock candidat, rapport et état de génération.
        Aucun restore/build/test ou déploiement exécuté automatiquement.
        modules.enabled doit être vide. La préférence Git V1 reste validée, mais n'est pas exécutée.
        Régénération uniquement avec état valide et mêmes configuration, template et générateur.
        Une cible sans état, même vide, ou un fichier géré modifié est refusé avant écriture (exit 6).
        Les fichiers client sont préservés ; aucun upgrade ni adoption implicite.
        Succès court sur stdout, stderr vide ; erreurs sur stderr, stdout vide.
        Codes : 0 succès, 1 erreur inattendue, 2 arguments invalides, 3 configuration invalide,
                4 catalogue/source invalide, 5 plan bloqué, 6 conflit, 7 échec d'écriture,
                11 cible/environnement invalide, 12 annulation, 15 schéma non supporté.
        Atomicité par fichier seulement : un échec ou une annulation peut laisser une sortie partielle.
        Aucun --force, --execute, --dry-run, Git, remote, upgrade, backup ou prompt.
        """;

    private const string PlanHelpText = """
        Usage: sitefactory plan --config <path>

        Options:
          --config <path>  Fichier UTF-8 obligatoire, chemin relatif ou absolu.
                          Aucune valeur par défaut ni découverte automatique.
          --help           Affiche cette aide sans lire de fichier.

        Exemple:
          sitefactory plan --config ./client.sitefactory.yml

        Succès : plan JSON V1 sur stdout, stderr vide.
        Échec : diagnostics CODE path message sur stderr, aucun plan sur stdout.
        Codes de sortie : 0 succès, 1 erreur inattendue, 2 arguments invalides,
                          3 configuration invalide, 4 catalogue non résolu ou invalide,
                          5 plan bloqué, 12 annulation, 15 schéma non supporté.
        Aucun dossier créé, fichier écrit ou modifié, aucune opération Git.
        Aucun --output, --apply, --force, --upgrade ou --dry-run.
        """;

    private const string ValidateHelpText = """
        Usage: sitefactory validate --config <path>

        Options:
          --config <path>  Fichier UTF-8 obligatoire, chemin relatif ou absolu.
                          Aucune valeur par défaut ni découverte automatique.
          --help           Affiche cette aide sans lire de fichier.

        Exemple:
          sitefactory validate --config ./client.sitefactory.yml

        Succès sur stdout ; diagnostics CODE path message sur stderr.
        Codes de sortie : 0 succès, 1 erreur inattendue, 2 arguments invalides,
                          3 configuration invalide, 4 catalogue non résolu ou invalide,
                          12 annulation, 15 schéma non supporté.
        Aucune modification de fichier, génération ou opération Git.
        """;

    public static Task<int> RunConsoleAsync(string[] args, GenerationComposition composition) =>
        RunConsoleCoreAsync(token => RunAsync(args, composition, Console.Out, Console.Error, token));

    private static async Task<int> RunConsoleCoreAsync(Func<CancellationToken, Task<int>> run)
    {
        // Keep redirected diagnostics readable independently of the host's console code page.
        Console.OutputEncoding = new UTF8Encoding(false);
        using CancellationTokenSource cancellation = new();
        void cancelHandler(object? _, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += cancelHandler;

        try
        {
            return await run(cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    public static async Task<int> RunAsync(
        string[] args,
        GenerationComposition composition,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        try
        {
            return await DispatchAsync(args, composition, standardOutput, standardError, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The final status must be writable even though the operation's token is cancelled.
            await WriteFinalErrorAsync(standardError, "Opération annulée.");
            return CliExitCode.Cancelled;
        }
        catch (Exception)
        {
            // CLI boundary: never expose provider exceptions, paths or stack traces.
            await WriteFinalErrorAsync(standardError, "Erreur : la commande a échoué de manière inattendue.");
            return CliExitCode.GeneralError;
        }
    }

    private static async Task WriteFinalErrorAsync(TextWriter standardError, string message)
    {
        try
        {
            await standardError.WriteLineAsync(message);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // A closed stderr cannot report the error; still return the documented exit code.
        }
    }

    private static async Task<int> DispatchAsync(
        string[] args,
        GenerationComposition composition,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (args.Length == 0 || args is ["--help"])
        {
            await standardOutput.WriteLineAsync(HelpText.AsMemory(), cancellationToken);
            return CliExitCode.Success;
        }

        if (args is ["--version"])
        {
            string version = GetVersion();
            await standardOutput.WriteLineAsync(version.AsMemory(), cancellationToken);
            return CliExitCode.Success;
        }

        if (args is ["validate", "--help"])
        {
            await standardOutput.WriteLineAsync(ValidateHelpText.AsMemory(), cancellationToken);
            return CliExitCode.Success;
        }

        if (args is ["validate", "--config", string path] && TryResolveConfigurationPath(path, out string configurationPath))
            return await ValidateCommand.RunAsync(configurationPath, composition, composition.SelectionValidator,
                standardOutput, standardError, cancellationToken);

        if (args is ["plan", "--help"])
        {
            await standardOutput.WriteLineAsync(PlanHelpText.AsMemory(), cancellationToken);
            return CliExitCode.Success;
        }

        if (args is ["plan", "--config", string planPath] && TryResolveConfigurationPath(planPath, out string planConfigurationPath))
            return await PlanCommand.RunAsync(planConfigurationPath, composition, composition.SelectionValidator,
                standardOutput, standardError, cancellationToken);

        if (args is ["generate", "--help"])
        {
            await standardOutput.WriteLineAsync(GenerateHelpText.AsMemory(), cancellationToken);
            return CliExitCode.Success;
        }

        if (args[0] == "generate")
        {
            string? config = null;
            string? output = null;
            if (args is ["generate", "--config", string firstConfig, "--output", string lastOutput])
                (config, output) = (firstConfig, lastOutput);
            else if (args is ["generate", "--output", string firstOutput, "--config", string lastConfig])
                (config, output) = (lastConfig, firstOutput);

            if (config is not null && TryResolveConfigurationPath(config, out string generateConfigurationPath)
                && !string.IsNullOrWhiteSpace(output) && !output.StartsWith('-') && !output.Any(char.IsControl))
                return await GenerateCommand.RunAsync(generateConfigurationPath, output, composition, composition, composition.SelectionValidator,
                    standardOutput, standardError, cancellationToken);

            await standardError.WriteLineAsync(
                $"Erreur : --config et --output exigent chacun une valeur unique ; toute autre option est refusée.\n{GenerateUsage}".AsMemory(),
                cancellationToken);
            return CliExitCode.InvalidArguments;
        }

        if (args[0] is "validate" or "plan")
        {
            string usage = args[0] == "validate" ? ValidateUsage : PlanUsage;
            await standardError.WriteLineAsync(
                $"Erreur : arguments invalides. --config exige un chemin unique ; toute autre option est refusée.\n{usage}".AsMemory(),
                cancellationToken);
            return CliExitCode.InvalidArguments;
        }

        await standardError.WriteLineAsync(
            UnknownArgumentMessage.AsMemory(),
            cancellationToken);

        return CliExitCode.InvalidArguments;
    }

    private static bool TryResolveConfigurationPath(string path, out string configurationPath)
    {
        configurationPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('-') || path.Any(char.IsControl))
            return false;

        try
        {
            configurationPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string GetVersion()
    {
        Assembly assembly = typeof(SiteFactoryCli).Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString()
            ?? assembly.GetName().Name
            ?? "SiteFactory";
    }
}
