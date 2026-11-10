# Migration et bootstrap explicites — Migrator public

## Source et identité

Le projet public
[LogicGate.SiteFactory.Migrator](../../src/LogicGate.SiteFactory.Migrator/LogicGate.SiteFactory.Migrator.csproj)
est un exécutable indépendant. Son
[Program](../../src/LogicGate.SiteFactory.Migrator/Program.cs) appelle
[MigrationCommand](../../src/LogicGate.SiteFactory.Migrator/MigrationCommand.cs),
sans génération de projet métier ni dépendance à une CLI Business.

Le login authentifié est **Migrator**, jamais Owner/Web/Worker/admin.
SET ROLE Owner est explicite après preflight, selon [ADR-0027](../../docs/adr/0027-database-roles-and-runtime-least-privilege.md).
Owner reste NOLOGIN ; aucune connexion directe Owner.
Web/Worker n'appliquent ni migration ni bootstrap et n'appellent pas EnsureCreated.

Avant mutation : session_user=current_user, NOINHERIT et aucun attribut critique ;
Owner réel de la base sélectionnée, NOLOGIN/NOINHERIT et sans attribut critique ;
unique arête Migrator → Owner avec SET TRUE, INHERIT FALSE, ADMIN FALSE ;
aucune appartenance sortante d'Owner. Voir [graphe fermé](migrator-role-graph-regression.md)
et [résolution search_path](catalog-resolution-regression.md).

La connexion validée appartient au contexte construit par
[RuntimeDbContextFactory](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/RuntimeDbContextFactory.cs),
pas à la classe de configuration. Sa libération ferme la connexion élevée.
Aucun pooling/multiplexing, rôle créé, grant réparateur ou fallback.
Un refus de rôle produit exit 1 et SF-DB-MIGRATOR-003, sans secret ni SQL brut.

## Commande réellement disponible

Après préparation explicitement autorisée d'une **cible de test jetable**,
la référence privée SITEFACTORY_MIGRATOR_CONNECTION_FILE doit être injectée
par le provisioning, jamais une connection string en argument.
La commande apply publique, après build Release, est :

```powershell
dotnet src/LogicGate.SiteFactory.Migrator/bin/Release/net10.0/LogicGate.SiteFactory.Migrator.dll apply --environment Test --confirm-migration
```

Ce n'est pas une instruction de production. Development/Test sont les seuls
environnements acceptés ; le runner ne peut pas prouver à lui seul qu'un opérateur
n'a pas dirigé un secret vers une mauvaise cible. Staging/Production restent refusés.
Ctrl+C transmet l'annulation ; après interruption, inspecter l'état avant
toute nouvelle décision, sans retry automatique.

## Migration et bootstrap séparés

Migration initiale : **20260923093309_CreateRuntimeMetadata**.
Elle crée uniquement les tables runtime database_identity, schema_state et
l'historique EF dans sitefactory_runtime, avec index/contraintes/mappings explicites.
Ni donnée métier ni seed d'identité.

Le [bootstrap public](../../src/LogicGate.SiteFactory.Migrator/RuntimeMetadataBootstrap.cs)
est une action distincte `bootstrap`, exigeant --environment, --client-slug,
--database-instance-id, --operation-id et --confirm-bootstrap dans l'ordre du
contrat. Ces valeurs structurelles doivent être fournies volontairement ;
aucun identifiant client d'exemple n'est nécessaire à ce runbook.
Il exige l'historique attendu, puis initialise les deux métadonnées dans une
transaction courte avec verrous/temps bornés. Toute erreur annule l'initialisation.

Une même identité est acceptée de nouveau sans réécriture. Identité de client,
environnement ou instance incompatibles, état partiel, version/provisioning
incompatible ou historique absent : refus stable, pas réparation.
La migration initiale n'est pas réécrite lors d'une régénération Business.

## Cinq Facts G04, PostgreSQL réel

[PostgreSqlMigrationTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlMigrationTests.cs)
porte :

- `ExtractedMigratorAppliesOnceWithItsOwnLoginAndRejectsRuntimeCredentials` :
  tables/colonnes/contraintes/ACL et historique réels, refus runtime/admin,
  première migration puis seconde application sans dérive ;
- `ExtractedMigratorPhysicallyClosesElevatedSessionsInsideTheLiveTestHost` :
  fermeture effective des sessions avant fin du host de test.

Compléments :
[graphe de rôles](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlMigratorRoleGraphTests.cs)
`ExtractedMigratorRejectsAnyRoleGraphOutsideItsSingleOwnerEdge`,
[search_path](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlMigratorSearchPathCases.cs)
`ExtractedMigratorRejectsHostileSearchPathBeforeApplyAndBootstrapMutation`,
[bootstrap](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlBootstrapTests.cs)
`ExtractedBootstrapIsTransactionalIdempotentAndRefusesMismatchesAndRuntimeWrites`.

La fixture fournit base/rôles/schéma vide et ACL, pas une migration cachée.
PostgreSQL confirme les refus runtime avec 42501 ; les snapshots prouvent
l'absence de mutation et l'idempotence. Isolation et cleanup sont requis.

## Preuves et limites

G04 REQUIRED a été validé sur ses cinq Facts. P04 final a acquis **23/23 Passed**,
cinq shards/TRX, PostgreSQL Linux 18.6, TLS VerifyFull et cleanup vérifié.
[Commande du harnais public](README.md), preuve non rejouée ici,
CI distante non encore validée.

Historiquement, une suite Business de 20 Facts générait et exécutait le vrai
Migrator client, puis régénérait le projet. Cette preuve reste privée ;
elle n'est pas requise par le harnais public et n'est pas confondue avec G04.
Aucun déploiement, provisioning production, upgrade, migration destructive,
rollback production ou nouveau droit runtime n'est promis.
