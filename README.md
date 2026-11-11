# LogicGate SiteFactory

[![Public CI](https://github.com/fi8Git/logicgate-sitefactory/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/fi8Git/logicgate-sitefactory/actions/workflows/ci.yml?query=branch%3Amain)

SiteFactory est un moteur open source modulaire **.NET 10** de validation, de
planification et de génération déterministe. Il comprend le Core, le ModuleSdk,
le module Documentation et les bibliothèques runtime/PostgreSQL génériques.

Le code original du périmètre public est distribué sous
[Apache-2.0](LICENSE), avec les attributions de [NOTICE](NOTICE).
Le projet est disponible sur
[GitHub](https://github.com/fi8Git/logicgate-sitefactory).
La CI publique est opérationnelle sur **Windows et Linux**, avec un job
**PostgreSQL REQUIRED** distinct et **SourceLink VERIFIED** sur les campagnes
publiques validées. Aucun package NuGet ni aucune release officielle ne sont publiés.

## Capacités disponibles

- **Core générique** : configuration YAML V1 stricte, diagnostics stables,
  catalogue de modules explicite, résolution de templates et Generation Plan
  validé avant application.
- **Génération physique** : confinement des chemins, refus des traversals,
  collisions et segments `.git`, écriture atomique par fichier, lock déterministe,
  rapport des opérations réelles et état de régénération.
- **Préservation** : stratégies
  [generated/create-once/merge/custom/protected](docs/adr/0005-file-generation-strategies.md).
  Un fichier géré modifié localement provoque un conflit ; une customization
  n'est jamais écrasée silencieusement. Merge non implémenté et stratégie inconnue
  échouent sans fallback vers overwrite.
- **ModuleSdk** : contrats publics légers, identités/versions/capacités et
  descripteurs immuables. Les modules sont connus au build et enregistrés
  explicitement ; aucun scan de DLL ni installation de plugin au runtime.
- **DocumentationModule** : site HTML statique embarqué, titre/navigation et
  deux pages ; également scanner Git local read-only, modèle documentaire et
  rendu sécurisé par composition C#. Pas de second moteur de génération.
- **Runtime** : contrats purs, validation de démarrage, binding en snapshot,
  lifecycle et adaptateur ASP.NET Core avec liveness/readiness minimales.
- **PostgreSQL générique** : configuration par composant et fichiers secrets,
  factory EF/DbContext, métadonnées runtime et migration initiale ; Migrator
  explicite, reader read-only, probe de compatibilité et intégration Web/Worker.
  Ces bibliothèques ne sont pas une application métier ni un outil de
  provisioning de production.

Les composants génériques se construisent et se testent sans dépendance
propriétaire obligatoire. Les consommateurs intègrent leurs propres modules
par les contrats publics du ModuleSdk.

## Solution et frontières

[SiteFactory.slnx](SiteFactory.slnx) contient **12 projets de production et
11 projets de tests publics**.

| Famille publique | Sources |
|---|---|
| Core et ports génériques | [Domain](src/LogicGate.SiteFactory.Domain/LogicGate.SiteFactory.Domain.csproj), [Application](src/LogicGate.SiteFactory.Application/LogicGate.SiteFactory.Application.csproj), [Infrastructure](src/LogicGate.SiteFactory.Infrastructure/LogicGate.SiteFactory.Infrastructure.csproj) |
| Contrats de modules | [ModuleSdk](src/LogicGate.SiteFactory.ModuleSdk/LogicGate.SiteFactory.ModuleSdk.csproj) |
| Contribution documentaire et CLI | [Documentation](src/LogicGate.SiteFactory.Modules.Documentation/LogicGate.SiteFactory.Modules.Documentation.csproj), [CLI](src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj) |
| Runtime pur | [Runtime.Contracts](src/LogicGate.SiteFactory.Runtime.Contracts/LogicGate.SiteFactory.Runtime.Contracts.csproj), [Runtime](src/LogicGate.SiteFactory.Runtime/LogicGate.SiteFactory.Runtime.csproj) |
| Adaptateurs de hosts | [Runtime.Hosting](src/LogicGate.SiteFactory.Runtime.Hosting/LogicGate.SiteFactory.Runtime.Hosting.csproj), [Runtime.AspNetCore](src/LogicGate.SiteFactory.Runtime.AspNetCore/LogicGate.SiteFactory.Runtime.AspNetCore.csproj) |
| Persistence et opérations explicites | [Infrastructure.PostgreSql](src/LogicGate.SiteFactory.Infrastructure.PostgreSql/LogicGate.SiteFactory.Infrastructure.PostgreSql.csproj), [Migrator](src/LogicGate.SiteFactory.Migrator/LogicGate.SiteFactory.Migrator.csproj) |

Les modules consommateurs dépendent des contrats publics ; le Core reste
indépendant de leurs implémentations concrètes.
Le [composition root public](src/LogicGate.SiteFactory.Cli/Program.cs) enregistre
DocumentationModule et transmet une `GenerationComposition` explicite à la CLI.
Un host externe peut composer ses propres modules par les mêmes contrats,
sans modification du Core. La
[composition est statique](docs/adr/0037-compose-modules-explicitly-without-dynamic-loading.md) ;
les sources utilisent aujourd'hui des ProjectReference, pas des packages déjà publiés.

Runtime.Contracts et Runtime ne dépendent ni d'IConfiguration/Hosting,
ni d'EF Core/Npgsql. Le binding et le lifecycle sont des adaptateurs ;
le reader PostgreSQL collecte, la politique pure décide.
Aucune migration, réparation ou création d'identité n'est exécutée au démarrage
du Web ou du Worker.

## Démarrage local

Prérequis : SDK .NET 10 selon [global.json](global.json) (10.0.301, politique de
roll-forward déclarée), Git et PowerShell 7 pour les scripts.
**Docker/PostgreSQL ne sont pas requis pour les suites offline ordinaires.**

Depuis la racine :

```powershell
dotnet restore SiteFactory.slnx --locked-mode
dotnet build SiteFactory.slnx --configuration Release --no-restore
dotnet test SiteFactory.slnx --configuration Release --no-build --no-restore --filter "Category!=PostgreSql"
```

Le filtre exclut explicitement les Facts PostgreSQL réels ; un résultat offline
n'est pas une preuve de base de données. Les tests architecturaux font partie
des suites publiques. [CONTRIBUTING](CONTRIBUTING.md) décrit les guards, les
preuves isolées et le workflow de revue.

Ces commandes sont documentées pour le périmètre public ; elles ne déclenchent
ni publication, ni déploiement, ni génération automatique d'un projet client.

## CLI publique : validate, plan, generate

Après build, consulter l'aide du véritable exécutable :

```powershell
dotnet run --project src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj --configuration Release --no-build --no-restore -- --help
```

Exemple exécutable avec la
[configuration publique fictive](tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml) :

```powershell
dotnet run --project src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj --configuration Release --no-build --no-restore -- validate --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml
dotnet run --project src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj --configuration Release --no-build --no-restore -- plan --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml
dotnet run --project src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj --configuration Release --no-build --no-restore -- generate --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml --output ./artifacts/documentation-site
```

La cible doit être **absente au premier appel**, ou reconnue par son état pour
une régénération. Même une cible vide sans état est refusée.
`validate` et `plan` n'écrivent rien ; `plan` émet du JSON sur stdout.
`generate` matérialise ici `index.html` et `guide.html` du template embarqué
`documentation-minimal`, ainsi que `sitefactory.lock.json`,
`generation-report.json` et `.sitefactory/generation-state.json`.

Limites réellement appliquées :

- `modules.enabled` doit être vide ; le module Documentation est déjà enregistré
  par le host, pas installé par le YAML.
- Les champs Git/déploiement V1 restent validés mais ne déclenchent ni Git init,
  remote, Docker ni déploiement.
- Régénération uniquement avec état valide et composition identique ; conflit
  avant écriture si un fichier géré a été modifié localement (exit 6).
- Atomicité **par fichier**, pas transaction filesystem globale ; une erreur ou
  annulation peut laisser une sortie partielle. Aucun `--force`, backup ou upgrade.
- Aucun restore/build/test exécuté par `generate`. Le site HTML embarqué
  n'est pas une solution .NET client à compiler.

L'import `repository Git → scanner → DocumentationSite → contribution → site`
existe par les **API C#**, pas par une commande CLI supplémentaire.
Le scanner reste sous la racine source et ne modifie pas `.git`.
Le rendu du modèle désactive le HTML brut et limite les liens actifs ;
il ne télécharge ni n'exécute de contenu distant.

## Preuves PostgreSQL publiques

Le [harnais P04](tests/PostgreSql/README.md) utilise PostgreSQL Linux
**18.6 / 180006**, image épinglée par digest, **TLS VerifyFull**, port loopback
dynamique, credentials éphémères et cleanup limité aux ressources possédées.

Après restore/build, avec Docker Linux disponible et la campagne explicitement voulue :

```powershell
pwsh -NoProfile -File tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired
```

Deux inventaires fermés distincts : **20 Facts Infrastructure + 3 Facts Hosts**.
Cinq shards séquentiels : **g04=5, g05=3, p01=8, p02=4, p03=3**.
Budget de 360 s **par shard**, pas une limite globale implicite.
Secret/instance absent ou invalide, résultat absent/ignoré/échoué, TRX incohérent
ou cleanup incomplet rendent la preuve rouge, sans retry jusqu'au vert.

Une campagne complète exige **23/23 Passed et cinq TRX** ; le critère P03 est
**32 × 24 = 768/768 HTTP 200**. Les résultats CI sont présentés ci-dessous.
Voir [rôles](tests/PostgreSql/roles.md),
[migrations/bootstrap](tests/PostgreSql/migrations.md) et
[permissions/diagnostics](tests/PostgreSql/permissions-and-errors.md).

**Risque connu P03-F1 : OPEN / UNKNOWN.** Historique : **749 HTTP 200 /
19 HTTP 503**, cause non démontrée, risque résiduel accepté avec réserve.
Échéance actuelle du réexamen : **2026-11-16**, sans prolongation tacite.
Les campagnes vertes n'ont pas démontré de correction causale.
[Observabilité et limites](tests/readiness-concurrency-regression.md).

## CI, limites et publication

Le [workflow public](.github/workflows/ci.yml) et les
[scripts public-ci](scripts/public-ci/Invoke-PublicCi.ps1) exécutent les suites
offline Windows/Linux, guards de composition et job PostgreSQL REQUIRED distinct.
La validation refuse les preuves incomplètes ; elle ne repose pas sur le seul
exit code. Les campagnes publiques validées contrôlent **SourceLink VERIFIED**,
les inventaires, les TRX, le cleanup et l'agrégation.

Dernier run `main` validé :
[43009023135](https://github.com/fi8Git/logicgate-sitefactory/actions/runs/43009023135),
sur le SHA
[`4be284f4fdfdf621e7bc850833e02c4820b062a9`](https://github.com/fi8Git/logicgate-sitefactory/commit/4be284f4fdfdf621e7bc850833e02c4820b062a9).

| Preuve publique | Résultat |
|---|---|
| Windows offline | 11 suites, **2 726 Passed** |
| Linux offline | 11 suites, **2 726 Passed** |
| PostgreSQL REQUIRED sur Linux | **23 Facts Passed**, cinq shards, cinq TRX |
| P03 | **768/768 HTTP 200** |
| SourceLink, 27 TRX, cleanup et agrégation | **Conformes** |

Les deux plateformes exécutent les mêmes suites offline. Ces résultats ne
démontrent aucune correction causale du risque historique P03-F1.

Ne sont pas annoncés comme livrés : application métier complète,
déploiement client de production, exploitation/provisioning
PostgreSQL production, backup/restore, upgrade, chargement dynamique de modules
ou publication NuGet. La publication de packages et de releases nécessite une
décision distincte ; aucune n'est réalisée par une contribution ordinaire.

## Contribution et licence

Lire [CONTRIBUTING](CONTRIBUTING.md), les règles [AGENTS](AGENTS.md) et les ADR
applicables. Les contributions doivent rester bornées, testées, revues et sans
secret ni effet de production implicite.

[CHANGELOG](CHANGELOG.md) décrit les capacités publiques sous **Unreleased**.
[LICENSE](LICENSE), [NOTICE](NOTICE) et
[ADR-0034](docs/adr/0034-apache-licence-choice-for-publish-site-factory.md)
définissent la portée Apache-2.0 du code public original et les attributions.
Les composants tiers conservent leurs licences ; les modules consommateurs et les
sorties client générées ne sont **pas automatiquement relicenciés**.
