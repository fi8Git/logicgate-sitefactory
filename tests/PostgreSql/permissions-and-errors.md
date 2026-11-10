# Permissions et erreurs PostgreSQL — preuves publiques

## Matrice SQL réelle P02

[PostgreSqlPermissionMatrixTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlPermissionMatrixTests.cs)
porte le nom historique
`GeneratedMetadataPermissionsAreEnforcedByPostgreSqlForEveryLogin`.
Ce Fact public P02 utilise les métadonnées migrées par le **Migrator public**,
sans générer un projet client Business : son préfixe de nom ne décrit pas une
dépendance privée. La
[fixture](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlRoleFixture.cs)
ouvre de vraies connexions distinctes, pas un login runtime simulé par un admin.

| Action | Migrator avant SET ROLE | Après SET ROLE Owner | Web | Worker |
|---|---|---|---|---|
| SELECT DatabaseIdentity / SchemaState | refus | autorisé | autorisé | autorisé |
| UPDATE des métadonnées | refus | autorisé dans une transaction annulée | refus | refus |
| CREATE TABLE | refus | autorisé sur objet sonde | refus | refus |
| ALTER / DROP TABLE | pas de nouvelle garantie avant élévation | autorisé sur objet sonde | refus | refus |
| CREATE ROLE / CREATE DATABASE | refus | refus | refus | refus |

Chaque refus de permission exige **SQLSTATE 42501**, pas une erreur de syntaxe
ou une simple décision du probe. `session_user`, `current_user` et les cinq
flags critiques sont vérifiés. Les essais transactionnels sont annulés ;
les noms de bases/rôles sont internes et le cleanup limité aux ressources possédées.

Les [huit Facts P01 de rôles](roles.md) complètent DML/DDL/TEMP, ownership,
memberships, PUBLIC, default privileges et search_path.
Le [bootstrap G04](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlBootstrapTests.cs)
reste transactionnel/idempotent et refuse de réécrire une identité incompatible.
Un UPDATE autorisé sous Owner pour un test ne donne pas une API de changement
d'identité aux runtime hosts.

## Observations G05 et décisions

[PostgreSqlCompatibilityTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlCompatibilityTests.cs)
porte `ExtractedReaderAndProbeDistinguishCompatibilityFailuresWithoutChangingPostgreSql`.
Reader concret et politique pure sont réutilisés, sans remplacement du port DB.
Le snapshot vérifie catalogue, valeurs, historique et xmin avant/après les lectures.

| Incompatibilité | Code stable |
|---|---|
| Identité absente | SF-DB-STARTUP-003 |
| Identité de client incompatible | SF-DB-STARTUP-004 |
| Environnement incompatible | SF-DB-STARTUP-005 |
| Instance incompatible | SF-DB-STARTUP-006 |
| Rôle inattendu | SF-DB-STARTUP-007 |
| Privilèges excessifs | SF-DB-STARTUP-008 |
| search_path incompatible | SF-DB-STARTUP-009 |
| Connexion indisponible / authentification refusée | SF-DB-STARTUP-001 |
| SchemaState absent | SF-SCHEMA-STARTUP-001 |
| Migration attendue absente | SF-SCHEMA-STARTUP-004 |
| Migration en cours / en échec | SF-SCHEMA-STARTUP-005 / SF-SCHEMA-STARTUP-006 |
| Dernière migration incohérente | SF-SCHEMA-STARTUP-008 |
| Famille/version/état de schéma incompatible | SF-SCHEMA-STARTUP-009 |

Aucun message ne contient une connection string, un secret, du SQL provider ou
une stack trace. Les mutations préparatoires des cas adversariaux appartiennent
exclusivement au fixture administratif de test, jamais au runtime.

## Hosts génériques P03, pas exécutables clients

Les Facts
[Web](../LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebReadinessTests.cs)
`RealWebReadinessUsesPostgreSqlWithoutWritingAndKeepsLivenessIndependent` et
[Worker](../LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWorkerReadinessTests.cs)
`RealWorkerValidatesItsOwnDatabaseBeforeStartingAndRemainsIdleWithoutWrites`
utilisent les hosts génériques et le vrai reader public.

Web incompatible : live 200, readiness et trafic applicatif 503, réponses
minimales no-store. Worker : startup refusé avant ApplicationStarted, non-ready,
aucun traitement ni fallback vers un autre credential. Métadonnées et sessions
sont contrôlées ; les hosts ne migrent, ne bootstrapent et ne réparent jamais.

Les deux tests de véritables **processus clients Worker** restent dans la suite
Business privée. Aucun résultat exit/stderr de ces exécutables n'est attribué
au host générique public.

## Délais et preuves attribuées

Un socket loopback silencieux teste les délais sans remplacer les autres preuves
sur PostgreSQL réel. Même avec de grands délais fournis dans les secrets de test :
scénario Worker borné à 15 s, Web à 25 s ; annulation après 100 ms,
propagation attendue en moins de 5 s. G05 teste aussi une lecture réellement
bloquée par un verrou PostgreSQL. Aucune deadline de production augmentée.

P02 a acquis **4 Passed REQUIRED**, dont cette matrice ; le P04 final a acquis
**20 Facts Infrastructure + 3 Facts Hosts = 23 Passed**, cinq TRX, TLS VerifyFull
et cleanup vérifié. Aucun rejeu ici. [Inventaires/commandes publics](README.md).
Sans configuration, Skipped n'est pas Passed ; en REQUIRED, absence de secret,
skip, résultat manquant ou erreur rend la preuve rouge.

Le stress public et le risque résiduel **P03-F1 OPEN / UNKNOWN** sont décrits dans
[la preuve de concurrence](../readiness-concurrency-regression.md).
CI distante non encore validée ; provisioning/exploitation production hors portée.
