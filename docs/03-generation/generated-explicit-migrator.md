# Migrator public explicite

[LogicGate.SiteFactory.Migrator](../../src/LogicGate.SiteFactory.Migrator/MigrationCommand.cs)
est un exécutable ponctuel, distinct des hosts runtime.
Il utilise Infrastructure.PostgreSql et la factory publique ; Web/Worker
ne le référencent pas. La CLI Documentation ne génère ni ne lance ce processus.
Cette extraction ne modifie pas les invariants des migrations historiques.

## Périmètre et commande

Ce premier runner est limité à une base **existante de Development ou Test**,
choisie et préparée explicitement par l'opérateur. Il ne constitue pas le runner
de production complet d'[ADR-0004](../adr/0004-explicit-production-migrations.md).
L'environnement est obligatoire : ni défaut Production ni déduction depuis Web.
`Staging` et `Production` sont refusés tant que leurs contrôles opératoires
(plan approuvé, cible/identité/rôles, backup, rapport et orchestration) sont absents.
Le libellé Development/Test ne certifie pas l'identité réelle du serveur :
l'opérateur reste responsable de ne fournir qu'une base isolée de développement/test.

Depuis la racine du checkout, après compilation explicite :

```sh
dotnet run --project src/LogicGate.SiteFactory.Migrator --configuration Release --no-build --no-restore -- --help
dotnet run --project src/LogicGate.SiteFactory.Migrator --configuration Release --no-build --no-restore -- apply --environment Development --confirm-migration
```

La seconde commande est une **action DDL volontaire**, pas une commande à exécuter
pendant une génération ou un simple contrôle de build. La syntaxe minimale est
exacte, dans cet ordre ; `Test` est l'autre environnement autorisé. Sans commande,
sans confirmation, ou avec un argument inconnu, le processus refuse avant toute I/O.
Ni `--force`, `--yes`, cible `latest`, rollback ni auto-apply.

## Connexion distincte

`SITEFACTORY_MIGRATOR_CONNECTION_FILE` référence le fichier UTF-8 contenant
la connexion dédiée au Migrator, résolue au lancement. Ce fichier secret est
fourni hors Git, avec des droits limités à l'opérateur. Aucun fichier secret
ou credential n'est généré. Ne pas transmettre la connexion en argument,
dans le YAML client ou les appsettings Web/Worker.

Le fichier doit déclarer explicitement Host, Database, Username et Password ; aucun fallback
vers une connexion runtime ou une base par défaut. Sa valeur, son chemin, les
exceptions provider et le SQL ne sont jamais imprimés dans les diagnostics.
Le runner ne configure pas de logs EF sensibles et ne désactive pas TLS.
Il ne crée ni rôles ni grants. Depuis SF-TASK-044, il vérifie son login et
l'appartenance au propriétaire de la base avant DDL (voir ci-dessous).
La variable doit être exposée uniquement au processus ponctuel Migrator.

Avant le DDL, il vérifie que la migration embarquée est exactement celle attendue,
que le modèle n'a pas dérivé, ouvre la base existante et refuse un historique
contenant une migration inconnue. L'ouverture explicite empêche EF de créer une
base absente. Seul cet exécutable appelle `MigrateAsync`, avec l'ID cible fixé
et le CancellationToken appelant. Il ne répare ni identité ni état runtime.

| Résultat du Migrator public | Exit | Diagnostic |
| --- | --- | --- |
| Aide ou migration terminée | 0 | Aucun ; résumé stdout |
| Commande non explicite / environnement interdit | 2 | SF-MIGRATION-USAGE-001 |
| Configuration distincte absente, invalide ou illisible | 2 | SF-MIGRATION-CONFIG-001 |
| Composition/modèle divergent | 1 | SF-MIGRATION-MODEL-001 |
| Historique non supporté | 1 | SF-MIGRATION-HISTORY-001 |
| Login / propriétaire / appartenance non conformes | 1 | SF-DB-MIGRATOR-003 |
| Échec provider | 1 | SF-MIGRATION-FAILED-001 |
| Annulation demandée | 130 | SF-MIGRATION-CANCELLED-001 |

Ces codes appartiennent au **Migrator public**, pas à la CLI SiteFactory.
Les erreurs attendues utilisent stderr, sans stack trace ou donnée brute.
Les exceptions de programmation ne sont pas absorbées par un catch global.
Après échec ou annulation pendant l'application, inspecter l'état avant toute reprise :
aucun rollback automatique ni garantie de transaction générale n'est ajouté.

## Migration initiale et compatibilité

`20260923093309_CreateRuntimeMetadata`, son designer et
`RuntimeDbContextModelSnapshot` résident dans
[Infrastructure.PostgreSql/Persistence/Migrations](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/Migrations).
Les sources de migration sont figées et cohérentes avec le modèle public,
pas recalculées à chaque génération. Aucun package EF Core Design n'est
requis au runtime ; l'outillage qui a préparé ces sources est distinct.

Le `Up` contient exactement :

- le schéma `sitefactory_runtime` ;
- `database_identity` et `schema_state`, avec les colonnes, clés et checks
  du [modèle SF-TASK-038](./generated-runtime-database-model.md) ;
- l'index unique `ux_database_identity_singleton`.

EF maintient en plus sa table technique `__EFMigrationsHistory` dans
`sitefactory_runtime`, jamais dans `public`. Il ne s'agit pas d'une entité
métier supplémentaire. Aucun seed, identité client, état `stable`, rôle, UUID
ou date runtime n'est créé par la migration ; le [bootstrap distinct SF-TASK-045](./generated-runtime-metadata-bootstrap.md)
initialise explicitement les deux métadonnées après application.

Migration additive sur une base vide déjà provisionnée ; aucune transformation
de données ou table rewrite. La création des tables/index et le verrou de migration
EF requièrent des droits DDL, non donnés au runtime par cette tâche. L'application
réelle et la réexécution sont désormais prouvées sur PostgreSQL isolé par
SF-TASK-044 ; les scénarios de concurrence, upgrade et panne restent futurs.
`Down` supprimerait les deux tables et leurs données ; il
n'est pas exposé par ce runner et n'est pas une procédure de rollback production.
Le schéma lui-même n'est pas supprimé.

## Preuves publiques acquises

[RuntimeMigrationTests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/RuntimeMigrationTests.cs)
et [tests du modèle](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/RuntimeDatabaseModelTests.cs)
inspectent modèle, migration, snapshot et SQL du vrai provider sans serveur.
Ils ne prouvent pas l'exécution des contraintes.

Les cinq Facts G04 couvrent migration réelle/réexécution, bootstrap et refus
des rôles/graphes/search_path avec le Migrator public dans PostgreSQL isolé.
Le [harnais public](../../tests/PostgreSql/README.md) possède vingt Facts
Infrastructure et trois Hosts, pas une suite cliente générée.
La preuve P04 23/23 Passed est acquise localement, non rejouée ici ;
CI distante non validée, pas de migration production ou déploiement client.

## Application PostgreSQL réelle — SF-TASK-044

Décision humaine confirmée : l'authentification est Migrator ; le `SET ROLE owner`
temporaire prévu par ADR-0027 est exécuté par le runner lui-même avant EF.
Il ne s'agit jamais d'une connexion directe owner ou d'un fallback superuser.

Le contrôle en lecture seule exige un login distinct `NOINHERIT`, un propriétaire
de base `NOLOGIN` sans privilèges globaux dangereux et une appartenance directe
`SET TRUE`, `INHERIT FALSE`, `ADMIN FALSE`. Le propriétaire attendu est celui de
la base explicitement choisie, lu dans `pg_database`, sans nouvelle convention de
nommage ou option CLI. L'identifiant est quoté par PostgreSQL. Un état non conforme
retourne `SF-DB-MIGRATOR-003` / exit `1` avant toute création d'historique.
Depuis [SPRINT-005-CORR-002](../../tests/PostgreSql/migrator-role-graph-regression.md),
ce lien est l'unique appartenance du Migrator et Owner ne possède aucune
appartenance sortante. Tout autre chemin, même transitif ou sans héritage,
est refusé avant élévation ; les attributs des deux rôles restent contrôlés.
Le login et Owner refusent explicitement SUPERUSER, CREATEDB, CREATEROLE,
REPLICATION, BYPASSRLS et INHERIT ; Owner reste NOLOGIN. NOINHERIT seul ne
suffit pas : un chemin SET ROLE inattendu reste un motif de refus.

Le runner désactive pooling, multiplexing et `No Reset On Close` pour sa seule
connexion, maintenue ouverte pendant EF et fermée en sortie, y compris sur erreur
ou annulation. Ni les hosts, ni le lecteur partagé de secrets ne changent.
Ce préflight n'est pas la validation complète de cible/ACL exigée en production.

Le [scénario réel public](../../tests/PostgreSql/migrations.md) utilise les
assemblies publiques et une base isolée : objets/propriétaires, historique unique,
seconde exécution sans dérive, refus Web/Worker et attributs administratifs.
Le [test de résolution hostile](../../tests/PostgreSql/catalog-resolution-regression.md)
vérifie apply et bootstrap avec agrégat count homonyme, types, opérateurs et
fonctions masqués : les requêtes de sécurité qualifient pg_catalog explicitement,
dont `pg_catalog.count(*)`. Aucun search_path fourni ne doit changer leur décision.
Les refus ont lieu avant mutation ; aucune source publique n'est patchée.

## Bootstrap distinct — SF-TASK-045

Le même exécutable expose [bootstrap](generated-runtime-metadata-bootstrap.md).
Apply ne crée aucune ligne d'identité/état ; bootstrap ne migre jamais.
La migration doit être appliquée, puis les deux métadonnées sont initialisées
dans une transaction unique ou refusées sans réparation. Development/Test,
secret distinct et préflight complet du graphe de rôles sont communs.

Le probe read-only et les adaptateurs Web/Worker consomment ces métadonnées
sans appeler Migrate/EnsureCreated. Aucun grant, rôle ou base de production
n'est provisionné par le runner.

Références techniques :
[gestion des migrations EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing),
[application explicite](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying),
[table d'historique](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table).
