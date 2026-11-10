# Frontière Core / Module

## Périmètre public actuel

Le Core explique la génération sans connaître une offre ou un module concret :
configuration structurelle → identités/versions → contribution explicite →
plan → stratégies de fichiers → matérialisation contrôlée.

La solution publique contient douze projets de production et onze de tests.
Domain, Application et Infrastructure génériques ne dépendent d'aucun module
Business. Le [SDK](module-sdk.md) décrit un module ; le [catalogue](module-catalog.md)
résout uniquement les enregistrements fournis par le host.

Cette frontière applique [ADR-0007](../adr/0007-module-composition-and-public-contracts.md),
[ADR-0033](../adr/0033-split-public-sitefactory-and-private-logicgate-modules.md)
et [ADR-0037](../adr/0037-compose-modules-explicitly-without-dynamic-loading.md).
La séparation logique est implémentée ; la publication d'un nouveau repository
et de packages n'est pas encore réalisée.

## Responsabilités et dépendances

| Responsabilité | Contrat / implémentation publique | Propriétaire |
|---|---|---|
| Métadonnées immuables | `ISiteFactoryModule`, `ModuleDescriptor` | ModuleSdk, BCL uniquement |
| Enregistrement, doublons, versions exactes, capacités | `IModuleCatalog`, `ModuleCatalog`, `ModuleRegistration` | Application |
| Sélection structurelle | `IConfigurationSelectionValidator` facultatif, injecté | Politique du host |
| Association module/template/contenu | `GenerationContribution`, `GenerationComposition` | Application |
| Lecture du manifest | `ITemplateManifestReader`, `TemplateManifestJsonReader` | Port Application, adaptateur Infrastructure |
| Accès au contenu choisi | `ITemplateContentProvider` | Contribution, pas une condition du Core |
| Plan, chemins et stratégies | `GenerationPlan`, `PlannedFile`, validations | Application |
| Préflight physique, état, écritures | `InitialGenerationExecutor`, writer atomique | Infrastructure |
| Sources et rendu documentaire | `DocumentationModule` et son modèle | Module Documentation |
| Choix des modules exécutés | `Program.cs`, injection dans `SiteFactoryCli` | Composition root |

Domain ne connaît ni module concret, EF, Npgsql, HTTP, YAML ni filesystem.
ModuleSdk ne connaît aucune couche moteur ou provider.
Application définit les ports ; Infrastructure les implémente.
La CLI publique référence Application, Infrastructure et Documentation.
Aucune référence circulaire, ni dépendance publique vers un projet Business.

Après séparation physique, le privé consomme les contrats/packages publics
versionnés ; aucun `ProjectReference` entre repositories. Aucun package publié
n'est annoncé par cette règle.

## Découplage livré — SF-TASK-054

Cette ancre est conservée pour les liens du catalogue.
Les anciennes sélections d'offre ont été sorties des couches génériques :
le validateur reçoit une politique explicite, le résolveur reçoit son catalogue
et le moteur reçoit son fournisseur de contenu. Un catalogue vide ou un module
fictif peut être utilisé sans charger une offre privée.

`GenerationComposition` copie et trie les contributions par ModuleId/TemplateId,
vérifie leur enregistrement/version et refuse les templates ambigus.
Le contenu doit appartenir au manifest effectivement résolu.
Un module absent produit un diagnostic ; aucun fallback implicite vers Business.
La génération V1 exige encore `modules.enabled` vide : les contributions
enregistrées ne sont pas une implémentation du système d'activation cible.

Les sources historiques propres aux offres, leurs manifests, assets et preuves
clientes demeurent privés ; leur inventaire n'est pas une dépendance documentaire
du Core public. Le Core ne scanne ni répertoire de templates implicite ni DLL.

## Composition publique réelle

Le [point d'entrée](../../src/LogicGate.SiteFactory.Cli/Program.cs) utilise :

```csharp
DocumentationModule module = new(new TemplateManifestJsonReader());
ModuleCatalog modules = new([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]);
GenerationComposition composition = new(modules, [module.CreateContribution()]);
```

Les types appartiennent respectivement à Modules.Documentation,
Infrastructure.Templates, Application.Modules et Application.Generation.
`SiteFactoryCli.RunConsoleAsync(args, composition)` reçoit ce choix explicitement.
Un host privé peut injecter une autre composition sans modifier le Core ;
la CLI publique n'expose pas pour autant ces modules.

Le module `documentation` expose une contribution embarquée et une surcharge
`CreateContribution(DocumentationSite)`. Cette dernière matérialise le modèle
dans le même moteur, pas dans un second générateur.
[Pipeline repository → documentation](../03-generation/repository-documentation-end-to-end.md).

## Sécurité et déterminisme

- Un module compilé est du code de confiance du host, pas un plugin sandboxé.
- Aucun chargement dynamique, installation NuGet runtime ou téléchargement/exécution.
- Chemins logiques et physiques bornés ; refus traversal, liens sortants,
  collisions et `.git` avant écriture.
- Même moteur, cinq stratégies canoniques, lock/rapport/état ; aucune transaction
  filesystem globale ni overwrite silencieux.
- Ordre ordinal, UTF-8/LF et sorties canoniques reproductibles.
- La régénération exige un état reconnu et préserve les fichiers client.
- Les secrets, connexions, contenus privés et données inutiles ne vont pas dans
  les diagnostics ou artefacts de génération.

## Preuves existantes et limites

Les [tests de frontière](../../tests/LogicGate.SiteFactory.Application.Tests/CoreModuleBoundaryTests.cs),
[tests de composition](../../tests/LogicGate.SiteFactory.Application.Tests/Generation/GenerationCompositionTests.cs)
et [E2E documentaires publics](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.EndToEnd.cs)
couvrent composition générique, contribution fictive, module inconnu, rendu,
régénération, sécurité et préservation du repository.
Les tests de dépendances inspectent directement les projets et leur graphe.

Les preuves locales acquises ne sont pas rejouées pour cette adaptation documentaire.
La CI distante Windows/Linux et SourceLink du véritable repository public ne sont
pas encore validés. Le SDK minimal ne fournit ni entitlements, contrats de jobs,
plages de compatibilité, marketplace ni installation dynamique.
