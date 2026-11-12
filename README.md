# LogicGate SiteFactory

[![Public CI — main](https://github.com/fi8Git/logicgate-sitefactory/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/fi8Git/logicgate-sitefactory/actions/workflows/ci.yml?query=branch%3Amain)

SiteFactory est un moteur open source modulaire **.NET 10** de validation, de
planification et de génération déterministe. Il fournit des contrats publics
pour intégrer ses propres modules, sans dépendance propriétaire obligatoire.
Le code original est sous [Apache-2.0](LICENSE), avec les attributions de
[NOTICE](NOTICE). **Aucun package NuGet ni aucune release officielle ne sont publiés.**

## Capacités disponibles

- **Core générique**: YAML V1 strict, diagnostics stables, catalogue explicite de
  modules et templates, plan validé avant écriture.
- **Génération/régénération**: chemins confinés, refus des traversals, collisions
  et segments `.git`; lock, rapport d’opérations et état de propriété.
- **Modules**: identités, versions exactes et capacités immuables; composition
  explicite au build. Aucun scan de DLL ni installation de plugin au runtime.
- **DocumentationModule**: template HTML embarqué à deux pages; scanner Git local
  read-only, modèle documentaire et rendu sécurisé par API C#, avec le même moteur.
- **Runtime**: contrats purs, configuration en snapshot, validation de démarrage,
  lifecycle et adaptateur ASP.NET Core liveness/readiness.
- **PostgreSQL**: configuration/secrets par composant, EF/DbContext et métadonnées
  runtime, reader read-only, probe de compatibilité, adaptateurs Web/Worker et
  Migrator explicite limité à Development/Test sur une base existante.

## Solution et frontières

[SiteFactory.slnx](SiteFactory.slnx) contient **23 projets: 12 de production et
11 de tests**. Les composants de production sont:

| Famille | Projets LogicGate.SiteFactory.* |
|---|---|
| Core et contrats de modules | Domain, Application, Infrastructure, ModuleSdk |
| Documentation et CLI | Modules.Documentation, Cli |
| Runtime pur et adaptateurs | Runtime.Contracts, Runtime, Runtime.Hosting, Runtime.AspNetCore |
| PostgreSQL et migrations | Infrastructure.PostgreSql, Migrator |

Les consommateurs dépendent des contrats publics; le Core reste indépendant de
leurs implémentations. Le [point d’entrée](src/LogicGate.SiteFactory.Cli/Program.cs)
compose explicitement DocumentationModule. Un host externe peut composer ses
modules par les mêmes contrats. Les projets internes utilisent des
ProjectReference; cela ne signifie pas que des packages sont publiés.
Runtime.Contracts/Runtime ne dépendent ni d’IConfiguration/DI/Hosting ni
d’EF/Npgsql. Le reader PostgreSQL collecte, la politique pure décide; aucun
host Web/Worker ne migre, ne répare ou ne crée d’identité au démarrage.

Références: [frontière Core/Module](docs/02-architecture/core-module-boundary.md),
[ModuleSdk](docs/02-architecture/module-sdk.md),
[catalogue](docs/02-architecture/module-catalog.md),
[composition statique](docs/adr/0037-compose-modules-explicitly-without-dynamic-loading.md).

## Démarrage local

Prérequis: SDK .NET 10 selon [global.json](global.json) (10.0.301, roll-forward
déclaré), Git et PowerShell 7 pour les scripts. **Les suites offline ne requièrent
ni Docker ni PostgreSQL.** Depuis la racine:

```powershell
dotnet restore SiteFactory.slnx --locked-mode
dotnet build SiteFactory.slnx --configuration Release --no-restore
dotnet test SiteFactory.slnx --configuration Release --no-build --no-restore --filter "Category!=PostgreSql"
```

Le filtre exclut les Facts PostgreSQL réels, sans les déclarer réussis. Les
guards architecturaux sont inclus; [CONTRIBUTING](CONTRIBUTING.md#30-commandes-de-validation)
détaille format, tests ciblés et preuve isolée.

## CLI publique : validate, plan, generate

Après build, avec la [fixture V1](tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml):

```powershell
dotnet run --project src/LogicGate.SiteFactory.Cli --configuration Release --no-build --no-restore -- --help
dotnet run --project src/LogicGate.SiteFactory.Cli --configuration Release --no-build --no-restore -- validate --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml
dotnet run --project src/LogicGate.SiteFactory.Cli --configuration Release --no-build --no-restore -- plan --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml
dotnet run --project src/LogicGate.SiteFactory.Cli --configuration Release --no-build --no-restore -- generate --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml --output ./artifacts/documentation-site
```

validate et plan restent read-only; plan émet le JSON déterministe sur stdout.
generate produit `index.html`, `guide.html`, `sitefactory.lock.json`,
`generation-report.json` et `.sitefactory/generation-state.json`.
L’import d’un repository documentaire est disponible par **API C#**, sans
commande CLI dédiée. Le scanner ne modifie pas `.git`; le rendu désactive le
HTML brut et contrôle les liens, sans téléchargement ni exécution de contenu distant.

Limites et garanties:

- Première cible **absente**, ou régénération avec état valide et composition
  identique. Même un dossier vide sans état est refusé.
- `modules.enabled` doit rester vide; le host compose Documentation explicitement.
- Les cinq [stratégies de fichiers](docs/adr/0005-file-generation-strategies.md)
  sont generated/create-once/merge/custom/protected. Conflit avant écriture si
  un fichier géré a été modifié (exit 6); custom préservé; merge non implémenté
  et stratégie inconnue refusés sans overwrite.
- Atomicité **par fichier**, pas transaction globale: panne/annulation peut laisser
  une sortie partielle. Aucun `--force` ni adoption/upgrade implicite.
- generate ne lance ni restore/build/test de sortie, ni Git init/remote, migration,
  backup ou déploiement. Les préférences Git/deployment V1 sont seulement validées.
  Le site statique n’est pas une application .NET cliente à compiler.

Guides actuels: [génération CLI](docs/03-generation/initial-generation-cli.md),
[régénération](docs/03-generation/regeneration-and-end-to-end-proof.md),
[repository documentaire par API](docs/03-generation/repository-documentation-end-to-end.md).

## Preuves PostgreSQL publiques

Le [harnais REQUIRED](tests/PostgreSql/README.md) utilise PostgreSQL Linux 18.6,
image par digest, TLS VerifyFull, port loopback dynamique, credentials éphémères
et cleanup limité aux ressources possédées. Après restore/build, avec Docker
Linux et une campagne explicitement voulue:

```powershell
pwsh -NoProfile -File tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired
```

**23 Facts** (20 Infrastructure + 3 Hosts), **cinq shards séquentiels** et 360 s
par shard, pas une limite globale. Absence, skip, échec, TRX incohérent ou cleanup
incomplet gardent la preuve rouge, sans retry jusqu’au vert.
[Contrat et provenance des preuves](CONTRIBUTING.md#302-postgresql-required-distinct),
[rôles](tests/PostgreSql/roles.md), [migrations/bootstrap](tests/PostgreSql/migrations.md),
[permissions](tests/PostgreSql/permissions-and-errors.md).

**P03-F1 reste OPEN / UNKNOWN**: 749 HTTP 200 / 19 HTTP 503 historiques, cause
non démontrée, risque accepté avec réserve. Réexamen obligatoire **2026-11-16**,
sans prolongation tacite. Une campagne verte ne démontre pas de correction
causale; conserver les [observations historiques](tests/readiness-concurrency-regression.md).

## CI, limites et publication

La [CI publique](.github/workflows/ci.yml) est opérationnelle sous Windows et
Linux, avec PostgreSQL REQUIRED sous Linux et **SourceLink VERIFIED**. Le
[run main 43228653055](https://github.com/fi8Git/logicgate-sitefactory/actions/runs/43228653055),
tentative 1, a validé le SHA `e001093a1cfce5c5d1716b182fc84a785f5696f8`.
Les [inventaires et garanties d’agrégation](CONTRIBUTING.md#321-checks-attendus)
restent 2 726 Passed par plateforme, 23 PostgreSQL, P03 768/768, 27 TRX, cleanup
et provenance conformes. Cette preuve n’est pas rejouée par ce guide.

**Prévus, non livrés:** merge structuré, upgrade/adoption de sorties, plages de
compatibilité multi-versions, application métier complète, provisioning ou
exploitation PostgreSQL production, déploiement client, backup/restore et
publication de packages. Un ADR accepté ou un exemple de procédure ne prouve
pas leur implémentation et n’autorise aucune opération réelle.

## Contribution et licence

Lire [CONTRIBUTING](CONTRIBUTING.md), [AGENTS](AGENTS.md) et les ADR applicables.
Contributions bornées, testées, revues, sans secret ni effet de production implicite.
[CHANGELOG](CHANGELOG.md) conserve les évolutions sous Unreleased.
[LICENSE](LICENSE), [NOTICE](NOTICE) et
[ADR-0034](docs/adr/0034-apache-licence-choice-for-publish-site-factory.md)
définissent la portée du code original; les droits des composants tiers, modules
consommateurs, sorties et contenus générés restent distincts.
