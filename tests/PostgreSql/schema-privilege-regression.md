# Schémas système et capacité CREATE — régression publique

## Règle littérale conservée

Le [reader PostgreSQL public](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/PostgreSqlDatabaseReader.cs)
et le [snapshot public](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlCatalogSnapshot.cs)
utilisent un préfixe **littéral** : `pg_catalog.starts_with(..., 'pg_')`.
Aucun LIKE ambigu ne doit traiter `pg` suivi d'un caractère quelconque comme
un schéma système.

`pg_catalog`, `pg_toast` et les vrais `pg_*` sont exclus de ce contrôle
applicatif ; `information_schema` conserve son exclusion explicite.
`pgreview`, `pgclient`, `pgame`, `pg1` et `client_schema` sont inspectés.

## Contre-preuves publiques G05 et P02

Le Fact G05
`ExtractedReaderRejectsCreateOnNonSystemPgSchemasForWebAndWorker` se trouve dans
[PostgreSqlCompatibilityTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlCompatibilityTests.cs).
Pour chaque schéma ordinaire, avec les **vrais logins Web puis Worker** :

1. USAGE seul : compatible et CREATE TABLE réellement refusé par PostgreSQL.
2. USAGE + CREATE : probe incompatible, `SF-DB-STARTUP-008`.
3. Après ce refus, CREATE TABLE réussit sous le même rôle, ce qui confirme
   la capacité dangereuse ; la transaction est annulée.
4. Retrait de CREATE : compatibilité retrouvée après une nouvelle vérification.

La preuve précède la création de table : un ownership d'objet créé ne peut
pas expliquer à lui seul le refus. Le schéma reste possédé par Owner NOLOGIN.
Les snapshots avant/après chaque probe prouvent sa lecture sans mutation.

Le Fact P02
`CatalogSnapshotExcludesOnlyLiteralSystemSchemaPrefixes` dans
[PostgreSqlCatalogSnapshotTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlCatalogSnapshotTests.cs)
vérifie que pg_catalog et pg_toast existent mais sont exclus du snapshot,
et que les cinq schémas ordinaires, public et sitefactory_runtime sont présents.
Le snapshot et le reader ont donc la même sémantique de préfixe.

P03 teste aussi la dérive de CREATE sur pgreview dans les
[hosts Web génériques](../LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebReadinessTests.cs).
Ces preuves n'importent ni host client ni template Business.

## Historique distinct

La régression de septembre 2026 a d'abord été reproduite dans la suite Business :
snapshot tronqué, probe acceptant à tort CREATE et readiness saine malgré le
privilège. Le passage rouge 16 Passed / 3 Failed puis 19 Passed / 0 Failed
est une preuve historique de cette suite privée, **pas le compteur public**.
Les vrais exécutables clients et leur E2E restent privés.
Aucun replay, patch de template ou upgrade client n'est réalisé ici.

## Preuve actuelle et invariants

Le [manifest public](Public/shards.json) affecte le premier Fact à g05 (3 Facts)
et le second à p02 (4 Facts). Le total public est **20 Infrastructure + 3 Hosts**,
pas les 20 Facts Business historiques. P04 final : **23/23 Passed**, cinq TRX,
PostgreSQL 18.6, TLS VerifyFull, zéro ressource possédée résiduelle avant
destruction du conteneur. Preuve réutilisée, non rejouée ; CI distante non validée.

Les ACL excessives ne sont jamais acceptées pour contourner le test. Aucun
DDL/DML/migration/réparation par le reader, aucun allongement de deadline,
aucun secret/provider brut dans les diagnostics.
La [baseline](roles.md) reste least privilege et le
[risque P03-F1](../readiness-concurrency-regression.md) reste ouvert :
une campagne verte ne corrige pas rétroactivement une cause inconnue.

Source : [fonction starts_with PostgreSQL 18](https://www.postgresql.org/docs/18/functions-string.html).
