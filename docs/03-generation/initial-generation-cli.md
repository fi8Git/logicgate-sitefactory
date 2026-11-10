# Génération minimale par la CLI publique

## Périmètre livré

La CLI compose explicitement `DocumentationModule` et injecte
`GenerationComposition` ; aucune composition Business implicite.
Le module `documentation` version `0.2.0` fournit le template embarqué
`documentation-minimal` version `0.1.0`. Ces versions de contrats ne sont
ni une release SiteFactory ni des packages NuGet publiés.

## Commandes depuis le checkout

Après restore/build explicites, avec la
[fixture publique](../../tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml) :

```sh
dotnet run --project src/LogicGate.SiteFactory.Cli --configuration Release --no-build --no-restore -- validate --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml
dotnet run --project src/LogicGate.SiteFactory.Cli --configuration Release --no-build --no-restore -- plan --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml
dotnet run --project src/LogicGate.SiteFactory.Cli --configuration Release --no-build --no-restore -- generate --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml --output ./artifacts/documentation-site
```

La cible doit être absente, ou reconnue par un état de génération valide.
Un dossier existant, même vide, sans état est refusé.
`validate` reste read-only ; `plan` produit le JSON V1 déterministe sur stdout.
`generate` produit `index.html`, `guide.html`, `sitefactory.lock.json`,
`generation-report.json` et `.sitefactory/generation-state.json`.
Le rendu embarqué échappe les textes ; aucun script/HTML arbitraire n'est exécuté.
L'[import d'un repository](repository-documentation-end-to-end.md) est disponible
par API C#, pas par une commande CLI supplémentaire.

## Préflight, stratégies et régénération

Le parseur exige une configuration UTF-8 V1 explicite, sans découverte automatique.
Les clés, types, propriétés inconnues et options non supportées sont refusés.
`modules.enabled` doit rester vide ; les préférences Git sont validées, pas exécutées.
Le catalogue et le contenu sont résolus par les contributions enregistrées.

Avant la première écriture : validation de tous les chemins logiques/physiques,
limites de noms, collisions fichier/répertoire, `.git`, liens et stratégies.
Un dernier nom physiquement invalide ne doit créer aucun des fichiers précédents.
Les sources sont préparées puis recontrôlées avant application ; aucun plan
différent n'est recalculé silencieusement.

Les stratégies d'[ADR-0005](../adr/0005-file-generation-strategies.md) restent :
`generated`, `create-once`, `merge`, `custom`, `protected`.
Le pipeline actuel n'improvise pas de fusion. Un fichier géré modifié provoque
un conflit ; un fichier client existant est préservé ; `protected` interdit
toute écriture, création comprise. Pas de `--force`.

L'état reconnu lie configuration, template, générateur, structure et hashes.
Une régénération à composition identique ne réécrit pas inutilement les sorties.
Une ancienne sortie n'est jamais adoptée, réparée ou mise à niveau implicitement.
[Contrat détaillé de régénération](regeneration-and-end-to-end-proof.md).

## Résultats et erreurs

Succès court sur stdout et stderr vide ; diagnostics stables sur stderr en échec.
Console UTF-8 sans BOM, annulation Ctrl+C propagée.
Codes : 0 succès, 1 erreur inattendue, 2 arguments, 3 configuration,
4 catalogue/source, 5 plan bloqué, 6 conflit/état, 7 écriture,
11 cible/environnement, 12 annulation, 15 schéma non supporté.
[Constantes](../../src/LogicGate.SiteFactory.Cli/CliExitCode.cs).

Le writer est atomique par fichier, **pas pour l'ensemble du dossier**.
Une panne I/O ou annulation peut laisser une sortie partielle ; aucune transaction
filesystem générale n'est promise. Les erreurs déterministes sont en revanche
détectées au préflight avant écriture.
Les diagnostics n'exposent ni secret, document utilisateur, stack trace ni
message provider brut.

## Preuves et hors portée

Les [tests du moteur](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Generation/InitialGenerationExecutorTests.cs)
et les [tests E2E documentaires](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.EndToEnd.cs)
prouvent génération, état, customizations, conflits et sécurité des chemins.
Les preuves locales déjà acquises ne sont pas rejouées ici.

Aucun restore/build/test de sortie, Git init, remote, migration, déploiement,
backup ou publication n'est lancé par generate.
Les générateurs d'applications Business et leurs E2E restent privés ;
le public n'annonce pas un site client complet.
CI distante non validée, SourceLink public PENDING.
