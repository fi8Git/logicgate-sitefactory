# Rôles PostgreSQL — baseline publique d'intégration

## Décision et sources réelles

[ADR-0027](../../docs/adr/0027-database-roles-and-runtime-least-privilege.md)
sépare Owner, Migrator, Web et Worker. La clarification humaine historique
autorise le SET ROLE Owner temporaire du Migrator : elle n'autorise pas une
connexion directe Owner. Web/Worker n'assument jamais Owner ou Migrator.

La source SQL publique est
[PostgreSqlRoleBaseline](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlRoleBaseline.cs).
La [fixture de rôles](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlRoleFixture.cs)
crée dans une transaction des rôles et credentials éphémères, puis ouvre de
vraies connexions authentifiées. Ce helper est **de test**, pas un outil de
provisioning d'un client ou de production.

Noms internes : forme ADR `sf_{client}_{environment}_{role}`, composante
aléatoire de 32 chiffres hexadécimaux, environnement test et rôle final.
ASCII, au plus 63 octets, base/rôles isolés ; aucune identité client réelle ni
nouvelle convention de production.

| Rôle | Identité et appartenance | Autorisations dans sa base de test |
|---|---|---|
| Owner | NOLOGIN, NOINHERIT, aucun password, aucune membership sortante | Propriétaire structurel |
| Migrator | LOGIN propre, NOINHERIT ; Owner avec SET TRUE, INHERIT FALSE, ADMIN FALSE | CONNECT ; DDL uniquement après élévation explicite |
| Web | LOGIN distinct, aucune membership | CONNECT, USAGE runtime, SELECT sur métadonnées |
| Worker | LOGIN distinct, aucune membership | Lecture runtime, credential différent de Web |

Tous sont NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS.
Owner reste puissant dans sa base ; le credential Migrator n'est jamais runtime.

## PUBLIC, search_path et default privileges

PUBLIC n'a aucun privilège sur la base isolée, public ou sitefactory_runtime.
Web/Worker n'ont ni CREATE, TEMP, DML ni délégation. Les sessions utilisent
`pg_catalog, sitefactory_runtime`, sans public, $user ni schéma modifiable
par le runtime. Les délais/limites de connexion du helper sont des valeurs de
test bornées, pas un profil de production.

Les defaults ciblent le créateur réel, Owner :

- tables/vues runtime : SELECT Web/Worker seulement ;
- séquences, fonctions, types et nouveaux schémas : aucun droit runtime ajouté ;
- retrait des droits PUBLIC, y compris les defaults globaux EXECUTE/USAGE.

Révoquer uniquement par schéma ne suffit pas à neutraliser des defaults globaux.
Les objets sondes sont créés après installation des defaults, sans GRANT
correctif. Aucun GRANT ALL runtime ni fallback permissif.

## Huit Facts P01 publics

[PostgreSqlRoleTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlRoleTests.cs)
et le [manifest public](Public/shards.json) attribuent exactement au shard p01 :

1. `RoleAttributesAndMembershipAreLeastPrivilege` ;
2. `MigratorRequiresExplicitOwnerAndResetsItsIdentity` ;
3. `WebCanReadButCannotChangeRuntimeSchema` ;
4. `WorkerCanReadButCannotChangeRuntimeSchema` ;
5. `PublicPrivilegesAndDatabaseIsolationAreExplicit` ;
6. `FutureOwnerObjectsReceiveOnlyExpectedDefaultPrivileges` ;
7. `ControlledSearchPathIgnoresPublicHomonyms` ;
8. `RoleCleanupSurvivesFailureAndCancellation`.

Ils interrogent pg_roles/pg_auth_members, ACL, ownership et permissions effectives,
sans lire de hash de password. DDL Migrator sous Owner puis RESET ROLE ;
refus DDL/DML/TEMP/élévation runtime avec **42501**, pas erreur syntaxique.

Les tables sondes de P01 ne reproduisent pas le modèle EF : elles ne prouvent
pas la migration. [G04](migrations.md) et la [matrice P02](permissions-and-errors.md)
couvrent séparément les véritables tables migrées et la fermeture du graphe.

## Isolation, cleanup, preuves et limites

La suppression vise seulement la base possédée et les quatre rôles exacts,
après exception ou annulation, avec nettoyage borné indépendant du token annulé.
Aucun DROP OWNED global, balayage de rôles ou CASCADE de nettoyage.
Les ressources préexistantes sont conservées ; une interruption brutale doit
être traitée sur le seul cluster jetable explicitement identifié.

P01 a été validé et clôturé documentairement. P04 final a acquis les huit
résultats p01 dans une preuve **23/23**, cinq shards/TRX, PostgreSQL Linux 18.6,
TLS VerifyFull et cleanup. [Harnais public et limites](README.md).
Cette édition ne rejoue rien ; CI distante non validée et serveur PostgreSQL
natif Windows non certifié. Les vingt Facts Business historiques sont un
inventaire distinct, jamais ajoutés aux vingt Facts Infrastructure publics.

Hors portée : provisioning/exploitation production, rôles backup/restore/
opérateur, nouvelle permission runtime et déploiement client.
