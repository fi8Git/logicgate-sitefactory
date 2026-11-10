# Résolution des catalogues SQL de sécurité

## Portée publique et origine de la régression

Le preflight du [Migrator public](../../src/LogicGate.SiteFactory.Migrator/MigrationCommand.cs)
ne doit pas dépendre du `search_path` reçu. Le
[reader concret](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/PostgreSqlDatabaseReader.cs)
collecte les observations ; le
[probe pur](../../src/LogicGate.SiteFactory.Runtime/Database/DatabaseCompatibility.cs)
décide. Aucun host runtime ne migre ni ne répare une base.

La contre-preuve historique de septembre 2026 concernait un véritable exécutable
client **Business privé**, avant extraction : un agrégat homonyme faisait accepter
migration et bootstrap malgré un graphe de rôles interdit. Témoin conservé :
`pg_catalog.count(*) = 2`, `count(*) = 1`, puis `SET ROLE` vers CREATEDB possible.
Les deux commandes terminaient avec exit 0 et mutation ; ce n'était pas une
preuve du Migrator public. La correction complémentaire est désormais exercée
par les tests autonomes publics ci-dessous, sans génération de client métier.

## Contre-preuve publique G04

[PostgreSqlMigratorSearchPathCases](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlMigratorSearchPathCases.cs)
porte le Fact
`ExtractedMigratorRejectsHostileSearchPathBeforeApplyAndBootstrapMutation`.
La [fixture publique](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlRoleFixture.cs)
provisionne uniquement sa base/rôles isolés. Elle ne remplace pas le preflight.

1. Créer `sf_corr002_shadow.count(*)` et une membership supplémentaire NOLOGIN CREATEDB.
2. Fournir `search_path = sf_corr002_shadow, pg_catalog` aux deux commandes réelles.
3. Prouver les valeurs 2/1 et la capacité effective SET ROLE sous le vrai login Migrator.
4. Exiger le refus de `apply` **et** `bootstrap`, exit 1,
   `SF-DB-MIGRATOR-003`, stdout vide et diagnostic borné.
5. Comparer catalogue, métadonnées et historique avant/après : aucune mutation.
6. Ajouter un opérateur `=(int8,int4)` et un type homonyme `text` :
   la résolution adverse est réellement active, mais la décision reste sûre.
7. Rétablir uniquement l'arête Migrator → Owner autorisée : apply/bootstrap,
   puis bootstrap répété, restent acceptés même avec les objets de masquage présents.

Ne pas contourner la preuve en imposant simplement un chemin sûr dans le test
ou en retirant l'agrégat avant le preflight. La
[fermeture du graphe](migrator-role-graph-regression.md) reste obligatoire.

## Requêtes et responsabilités conservées

| Décision ou lecture | Frontière de résolution |
|---|---|
| `AssumeDatabaseOwnerAsync` : identité, attributs, owner et memberships | Catalogues, fonctions, `pg_catalog.count(*)` et opérateurs qualifiés ; Owner est celui de la base explicitement sélectionnée |
| SET ROLE | Identifiant issu de `pg_catalog.quote_ident` sur l'Owner vérifié ; aucune élévation avant acceptation du graphe |
| Historique EF | `sitefactory_runtime."__EFMigrationsHistory"`, schéma fixé par [RuntimeDbContextFactory](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/RuntimeDbContextFactory.cs), pas par la configuration |
| Bootstrap | Tables/verrous runtime qualifiés, cast `pg_catalog.text`, comparaisons d'identité en C# et transaction explicite |
| Reader de sécurité | Catalogues, fonctions, types, opérateurs et collations nommés qualifiés ; préfixe système littéral `pg_` |
| Probe / validators | Décisions sur observations, pas de SQL supplémentaire ni de réparation |

Les mots-clés SQL ne sont pas des fonctions libres résolues par le chemin.
La qualification concerne aussi opérateurs/types/collations, pas seulement les
tables. Le snapshot administratif est un témoin distinct, pas le preflight.

[PostgreSqlDatabaseReaderSearchPathTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlDatabaseReaderSearchPathTests.cs)
porte le Fact G05
`ExtractedReaderPreservesObservationsAndRefusalsOnHostileSearchPaths`.
La politique runtime refuse toujours une session au chemin incompatible :
`SF-DB-STARTUP-009`, sans masquer le chemin fourni ; un CREATE excessif reste
aussi observé et refusé (`SF-DB-STARTUP-008`). La robustesse des lectures ne rend
pas toute configuration de session acceptable.

## Preuve acquise et limites

Le [harnais public](README.md) répartit exactement 20 Facts Infrastructure et
3 Facts Hosts en cinq shards. Le Fact Migrator appartient à `g04` (5),
celui du reader à `g05` (3) ; ce ne sont pas deux nouveaux inventaires.
P04 final a acquis **23/23 Passed, cinq TRX**, PostgreSQL 18.6 réel,
TLS VerifyFull et cleanup vérifié. Aucun rejeu pendant cette adaptation.

Le traitement des secrets reste par fichiers privés ; aucune connexion, SQL
provider brut ou stack trace n'est publiée. Les ressources adversariales sont
possédées par le test puis nettoyées, sans nouveau privilège runtime.
Aucune certification du provisioning de production ni protection contre un
administrateur modifiant le catalogue après contrôle. CI distante non validée.

Sources : [résolution des schémas PostgreSQL 18](https://www.postgresql.org/docs/18/ddl-schemas.html),
[baseline des rôles](roles.md), [migration explicite](migrations.md).
