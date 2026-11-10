using System.Text;
using LogicGate.SiteFactory.Application.Configuration;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace LogicGate.SiteFactory.Infrastructure.Configuration;

public static class YamlConfigurationLoader
{
    public const int MaximumFileSizeBytes = 1024 * 1024;
    public const int MaximumDepth = 32;
    public const int MaximumNodeCount = 16_384;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Loads one V1 document without functional validation or external YAML resolution.</summary>
    public static async Task<ConfigurationLoadResult> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string text = await ReadTextAsync(filePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            using StringReader reader = new(text);
            RestrictedYamlParser parser = new(new Parser(reader), cancellationToken);
            YamlStream stream = [];
            stream.Load(parser);
            cancellationToken.ThrowIfCancellationRequested();

            if (stream.Documents.Count == 0 ||
                stream.Documents[0].RootNode is YamlScalarNode { Style: ScalarStyle.Plain, Value: "" or null })
                return Failure("SF-CONFIG-YAML-004", "Le fichier de configuration est vide.");

            ClientConfigurationV1 configuration = ClientConfigurationYamlReader.Read(stream.Documents[0].RootNode);
            cancellationToken.ThrowIfCancellationRequested();
            return ConfigurationLoadResult.Success(configuration);
        }
        catch (ConfigurationYamlException exception)
        {
            return ConfigurationLoadResult.Failure(exception.Diagnostic);
        }
        catch (YamlException)
        {
            return Failure("SF-CONFIG-YAML-004", "La syntaxe du document YAML est invalide.");
        }
        catch (DecoderFallbackException)
        {
            return Failure("SF-CONFIG-YAML-011", "Le fichier doit utiliser un encodage UTF-8 valide.");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Failure("SF-CONFIG-YAML-001", "Le fichier de configuration est introuvable.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure("SF-CONFIG-YAML-002", "Le fichier de configuration ne peut pas être lu.");
        }
    }

    private static async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 8192, options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        // The extra byte detects oversize input even if the file grows while being read.
        byte[] buffer = new byte[MaximumFileSizeBytes + 1];
        int length = 0;

        while (length < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);

            if (read == 0)
                break;

            length += read;
        }

        if (length > MaximumFileSizeBytes)
            throw new ConfigurationYamlException("SF-CONFIG-YAML-003", "Le fichier de configuration dépasse la limite de 1 Mio.");

        cancellationToken.ThrowIfCancellationRequested();
        int offset = length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
        return StrictUtf8.GetString(buffer, offset, length - offset);
    }

    private static ConfigurationLoadResult Failure(string code, string message) =>
        ConfigurationLoadResult.Failure(new(new DiagnosticCode(code), DiagnosticSeverity.Error, message));
}
