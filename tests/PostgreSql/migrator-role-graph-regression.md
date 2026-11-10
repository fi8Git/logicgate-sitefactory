# Graphe de rôles du Migrator public

## Invariant et preflight livré

Le [Migrator public](../../src/LogicGate.SiteFactory.Migrator/MigrationCommand.cs)
et la [baseline d'intégration](roles.md) conservent exactement :

```text
Migrator LOGIN NOINHERIT
  └─ SET TRUE, INHERIT FALSE, ADMIN FALSE → Owner NOLOGIN NOINHERIT
                                          └─ aucune appartenance sortante
```

Ni Migrator ni Owner ne possèdent SUPERUSER, CREATEDB, CREATEROLE, REPLICATION
ou BYPASSRLS. Owner est le propriétaire de la **base sélectionnée**, pas un rôle
choisi par suffixe. Le login authentifié demeure Migrator ; l'élévation Owner
est explicite et temporaire. Web/Worker n'ont ni cette membership ni DDL.

`AssumeDatabaseOwnerAsync` lit les rôles et memberships réels : unique arête
Migrator → Owner, options exactes et aucun départ depuis Owner. Cela ferme aussi
les chemins transitifs, puisqu'un chemin supplémentaire commencerait par une
arête interdite. Un rôle inattendu est refusé même NOLOGIN, sans héritage ou sans
capacité SET ; NOINHERIT seul n'est pas une protection suffisante.

Refus : exit 1, `SF-DB-MIGRATOR-003`, avant SET ROLE, migration ou bootstrap.
Aucun grant réparateur, fallback, SQL brut, secret ni stack trace. La
[protection search_path](catalog-resolution-regression.md) qualifie également
les objets SQL participant à cette décision.

## Test réel autonome G04

[PostgreSqlMigratorRoleGraphTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlMigratorRoleGraphTests.cs)
porte
`ExtractedMigratorRejectsAnyRoleGraphOutsideItsSingleOwnerEdge`.
Il utilise le vrai Migrator public et la
[fixture de rôles publique](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlRoleFixture.cs),
pas un host ou une DLL client Business.

Sont exercés avant apply puis avant bootstrap :

- attributs directs interdits sur Migrator et Owner, Owner LOGIN ou INHERIT ;
- Migrator → rôle supplémentaire avec chacun des cinq attributs administratifs ;
- Migrator → intermédiaire → CREATEDB, et le chemin passant par Owner ;
- rôles supplémentaires inoffensifs, memberships inactives, héritées ou ADMIN ;
- lien Owner absent, SET FALSE, INHERIT TRUE ou ADMIN TRUE.

Les chemins CREATEDB sont des capacités réelles : la connexion Migrator prouve
ses attributs directs sûrs puis réussit SET ROLE vers le rôle privilégié.
Le test n'utilise pas cette élévation pour créer des objets hors de sa preuve.

Les snapshots capturent récursivement les appartenances et attributs atteignables,
ainsi que catalogue/métadonnées. Ils doivent rester identiques après refus.
La baseline rétablie accepte apply/bootstrap et leur réexécution sans mutation.
Les rôles adversariaux sont uniques au test et supprimés en finally.

[PostgreSqlMigrationTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlMigrationTests.cs)
complète la preuve avec
`ExtractedMigratorPhysicallyClosesElevatedSessionsInsideTheLiveTestHost` :
la libération du contexte ferme réellement les sessions élevées avant arrêt du
processus de test. La réussite d'un exit code ne suffit pas au cleanup.

## Historique et preuve actuelle

La régression de septembre 2026 était initialement démontrée sur un véritable
exécutable client privé : SET ROLE vers CREATEDB réussissait et l'ancien
preflight retournait exit 0 au lieu de 1. Cet historique reste Business,
distinct de l'extraction publique et de ses résultats.

Le harnais historique possédait **20 Facts privés**, dont cette régression.
Le [harnais public](README.md) possède **23 Facts** : ce test est l'un des
**5 Facts g04**, et non un Fact supplémentaire. P04 final a acquis
23/23 Passed avec cinq TRX, PostgreSQL 18.6 réel, TLS VerifyFull et cleanup ;
aucun rejeu ni preuve CI distante pendant cette adaptation.

Aucun changement de rôle/ACL/migration/runtime, aucun allongement de deadline
ou diminution de couverture. Les contre-preuves refusées restent des sous-cas
du même Fact, pas autant de Facts supplémentaires.
Ce preflight ne certifie ni toutes les permissions du cluster ni la production,
et ne bloque pas un administrateur modifiant les droits après vérification.

Sources : [ADR-0027](../../docs/adr/0027-database-roles-and-runtime-least-privilege.md),
[appartenances](https://www.postgresql.org/docs/18/role-membership.html),
[pg_auth_members](https://www.postgresql.org/docs/18/catalog-pg-auth-members.html),
[SET ROLE](https://www.postgresql.org/docs/18/sql-set-role.html).
