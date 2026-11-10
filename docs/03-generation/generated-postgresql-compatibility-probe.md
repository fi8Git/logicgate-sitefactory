# Probe PostgreSQL public read-only

La politique de compatibilité est extraite dans Runtime ; le reader concret
est dans Infrastructure.PostgreSql. Aucun manifest ou assembly Business n'est
nécessaire. La CLI Documentation n'en fait pas un template client PostgreSQL.

## Frontières

`DatabaseCompatibilityProbe` et `DatabaseCompatibilityExpectation` appartiennent à
`LogicGate.SiteFactory.Runtime.Database`. L'attente contient une RuntimeIdentity déjà validée,
le **nom du login attendu indépendamment du credential fourni**, et un éventuel
DatabaseInstanceId attendu. Pas de connexion, Npgsql, EF Core, IConfiguration ou secret.
Le nom attendu est fourni par l'appelant/provisioning ; le probe ne fabrique aucun
nom de rôle de production et ne déduit pas l'identité attendue de la base observée.

`IRuntimeDatabaseReader` retourne uniquement des observations copiées : identité,
état, historique et attributs/privilèges du rôle. `Evaluate` décide sans I/O.
`PostgreSqlDatabaseReader`, dans Infrastructure.PostgreSql.Persistence, utilise la configuration secrète
typée SF-TASK-041 et réalise les lectures concrètes. Aucun modèle EF n'est modifié.

L'adaptateur public [Web](./generated-web-postgresql-readiness.md)
branche le probe à son startup et à sa readiness, avec un rôle attendu indépendant.
L'adaptateur [Worker](./generated-worker-postgresql-readiness.md) appelle
le probe au startup, exclusivement avec son credential et rôle attendu ;
une incompatibilité empêche le démarrage des services, sans fallback ni réparation.
Le probe concerne les profils runtime Web/Worker, pas le profil DDL du Migrator.

## Compatibilité initiale

La baseline reprend [SF-TASK-045](./generated-runtime-metadata-bootstrap.md) et
[ADR-0031 §§36–45](../adr/0031-application-startup-validation-and-runtime-compatibility.md#36-database-identity) :

- une identité, slug et environnement exacts ; InstanceId comparé lorsqu'attendu ;
- une famille `platform`, version **exacte** `"1"` ; aucune interprétation numérique
  des versions opaques et aucune plage d'upgrade implicite ;
- état `stable`, LastMigrationId correspondant à la migration initiale ;
- présence réelle de `20260923093309_CreateRuntimeMetadata` dans l'historique EF,
  absence de migration inconnue ; l'attendue reste recherchée même si d'autres IDs la précèdent ;
- session et login attendus identiques, login autorisé, pas de `postgres` ;
- `search_path` déclaré exactement `pg_catalog, sitefactory_runtime`, comme la
  [baseline SF-TASK-043](../../tests/PostgreSql/roles.md) ; aucun fallback public.

Le profil runtime initial refuse les cinq attributs critiques (SUPERUSER, CREATEDB,
CREATEROLE, REPLICATION, BYPASSRLS), INHERIT et toute appartenance : la baseline
initiale ne prévoit aucun rôle-groupe autorisé. Il refuse également propriété
de base/schéma/relation/fonction, CREATE de base ou schéma utilisateur, TEMP,
DML/DDL sur les tables runtime et délégation de privilèges. Les contrôles utilisent
les droits effectifs, y compris PUBLIC et grants de colonne, pas seulement les ACL
directes. Une future permission/groupe nécessaire exige une évolution explicite du
profil et de ses tests. Ce n'est pas l'auditeur exhaustif des profils métier futurs.

## Read-only et délais

Une connexion dédiée non poolée/non multiplexée est physiquement fermée en sortie.
La configuration originale et ses autres consommateurs restent inchangés.
Les observations sont prises dans une transaction `REPEATABLE READ READ ONLY` ;
le lecteur vérifie effectivement `transaction_read_only = on`. Aucune écriture de
données, DDL, migration, SET ROLE, réparation ou tentative de GRANT.
Les seules commandes de session/transaction préparent et ferment le snapshot read-only.
Les requêtes et fonctions système sont qualifiées `pg_catalog` ; les trois tables
sont qualifiées `sitefactory_runtime`. Pas d'exécution d'une vue ou foreign table
substituée aux tables attendues. Deux lignes au maximum par collection initiale
suffisent à constater absence, unicité ou incompatibilité.

Budget total du lecteur : **5 secondes**, sans retry. Connect timeout et command
timeout : 3 secondes ; attente d'acquittement de cancellation Npgsql : 1 seconde.
Ces limites du probe remplacent les éventuels délais illimités de sa seule connexion.
Le CancellationToken appelant est lié à la deadline et propagé à toutes les I/O.
L'annulation demandée est propagée comme annulation, pas comme base incompatible.
Timeout/connexion interrompue donnent un résultat de disponibilité négatif, jamais
une hypothèse de compatibilité fondée sur un snapshot partiel.

## Diagnostics

Messages fixes et ordre déterministe : transport, rôle, lecture, identité, état,
historique. Plusieurs erreurs indépendantes de métadonnées peuvent être retournées.
Un problème de rôle est signalé avant les données qu'il ne permet pas de lire.
Aucune valeur observée, connexion, chemin secret, SQL ou exception provider n'est
recopié dans StartupValidationResult ou ToString().

| Code | Signification |
| --- | --- |
| SF-DB-STARTUP-001 | Connexion/lecture indisponible ou délai dépassé |
| SF-DB-STARTUP-003 | Identité absente |
| SF-DB-STARTUP-004 | Mauvais client |
| SF-DB-STARTUP-005 | Mauvais environnement |
| SF-DB-STARTUP-006 | Instance différente lorsqu'attendue |
| SF-DB-STARTUP-007 | Login/session/search_path non conformes |
| SF-DB-STARTUP-008 | Attributs ou privilèges excessifs |
| SF-DB-STARTUP-009 | Permission de lecture nécessaire absente |
| SF-SCHEMA-STARTUP-001 | État du schéma absent |
| SF-SCHEMA-STARTUP-004 | Migration attendue non appliquée |
| SF-SCHEMA-STARTUP-005 | Migration planifiée/en cours |
| SF-SCHEMA-STARTUP-006 | Migration échouée |
| SF-SCHEMA-STARTUP-007 | Migration inconnue |
| SF-SCHEMA-STARTUP-008 | Dernière migration déclarée incohérente |
| SF-SCHEMA-STARTUP-009 | Famille/version/structure/état incompatible |

Les codes existants viennent d'ADR-0031. `SF-SCHEMA-STARTUP-009` complète cette
liste pour les contrats opaques incompatibles : on ne prétend pas qu'une version
inconnue est numériquement plus ancienne ou plus récente. Les erreurs de transport,
permissions et matérialisation sont traduites à la frontière du lecteur. Une erreur
de programmation ou une erreur SQL inattendue n'est pas absorbée par un catch global.

## Tests publics et limites

[DatabaseCompatibilityTests](../../tests/LogicGate.SiteFactory.Runtime.Tests/DatabaseCompatibilityTests.cs)
vérifie la décision pure, diagnostics, privilèges critiques, ordre,
copies défensives, annulation et propagation des erreurs de programmation.
[PostgreSqlCompatibilityTests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlCompatibilityTests.cs)
et les cas G05 utilisent les vrais reader/modèle/Migrator publics :
identité, environnement, rôle, droits effectifs/PUBLIC/colonnes,
historique et schéma incompatibles, non-mutation des métadonnées/historique/xmin.
Les tests P02 complètent la matrice de permissions et l'E2E de fondation.

Le contrôle des schémas utilise le préfixe **littéral pg_**, pas LIKE avec
underscore wildcard. pg_catalog/pg_toast sont système ; pgreview, pgclient,
pgame, pg1 et un schéma ordinaire sont inspectés.
Le [cas de search_path hostile](../../tests/PostgreSql/catalog-resolution-regression.md)
prouve la résolution qualifiée des objets/fonctions/types/opérateurs de sécurité.
Le reader collecte ; Evaluate décide, sans recopier les secrets ou Npgsql dans Runtime.

Le [harnais public](../../tests/PostgreSql/README.md) possède 20 Facts Infrastructure
(G04=5, G05=3, P01=8, P02=4) et 3 Facts Hosts P03.
La preuve locale P04 23/23 Passed, TLS VerifyFull et cleanup, est acquise et
non rejouée ici. Les tests offline ne remplacent pas cette preuve PostgreSQL.
Les vingt Facts Business historiques sont un autre inventaire privé.

Pas de migration/bootstrap automatique, de retry, de modèle métier,
de provisioning client ou de validation de production.
P03-F1 reste **OPEN / UNKNOWN**, 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve ; réexamen **2026-10-23**.
768/768 ultérieurs ne prouvent pas une correction causale.
CI distante Windows/Linux non validée.
