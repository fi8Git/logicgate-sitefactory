# Fondation PostgreSQL — preuve publique et E2E Business distincts

## Ce qui est public et effectivement prouvé

Le [harnais P04 public](README.md) relie les composants extraits :

```text
Infrastructure PostgreSQL + Migrator public
  → base et rôles de test isolés
  → apply puis bootstrap explicites
  → reader concret + probe pur
  → hosts génériques Web / Worker
  → HTTP/readiness, arrêt et preuve de non-mutation
```

Il n'invoque pas un template métier, une DLL client ou un repository privé.
Le [manifest public](Public/shards.json) conserve **20 Facts Infrastructure**
et **3 Facts Hosts**, en cinq shards disjoints g04/g05/p01/p02/p03.
Ce n'est **pas** un Fact public de génération/régénération d'un projet métier.

| Étape publique | Preuve existante |
|---|---|
| Migration réelle avec login Migrator puis élévation Owner explicite | G04, [PostgreSqlMigrationTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlMigrationTests.cs) |
| Bootstrap transactionnel/idempotent, identités incompatibles refusées | G04, [PostgreSqlBootstrapTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlBootstrapTests.cs) |
| Rôles, DDL/DML refusés et default privileges | P01 et [matrice P02](permissions-and-errors.md) |
| Connectivité, identité, schéma, migration et rôle/privilèges observés sans mutation | G05, [PostgreSqlCompatibilityTests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlCompatibilityTests.cs) |
| Web réel générique, liveness indépendante, readiness dépendante de la DB | P03, [PostgreSqlWebReadinessTests](../LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebReadinessTests.cs) |
| Generic Host Worker, startup/idle/cancellation/arrêt | P03, [PostgreSqlWorkerReadinessTests](../LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWorkerReadinessTests.cs) |
| 768 requêtes HTTP réelles concurrentes avec reader PostgreSQL concret | P03, [PostgreSqlWebConcurrencyTests](../LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebConcurrencyTests.cs) |

Les cas négatifs couvrent notamment identité/environnement incompatibles,
métadonnées/historique absents, version incompatible, mauvais rôle,
privilèges excessifs et connexion indisponible.
La préparation adverse et son rétablissement restent administratifs ;
aucune réparation par un host, aucune migration automatique.

Web incompatible : live 200, readiness et trafic applicatif 503, corps minimal
et no-store. Worker incompatible : échec avant ApplicationStarted, non-ready ;
pas de travail, fallback ou serveur HTTP ajouté au Worker.
Les snapshots contrôlent catalogue/métadonnées et xmin ; connexions et sessions
doivent être fermées après arrêt. Les smokes utilisent le loopback dynamique,
pas un port fixe ni une base développeur.

## Résultat P04 acquis, non rejoué

Campagne publique finale **23/23 Passed**, zéro Failed/Skipped/NotExecuted/Aborted,
**cinq TRX** stricts, PostgreSQL Linux **18.6 / 180006**, TLS **VerifyFull**,
credentials éphémères, cleanup DB/rôles/sessions puis conteneur/secrets vérifié.
Plateforme acquise : Windows .NET vers serveur Linux temporaire ;
**CI distante non encore validée**.

Stress : **32 workers × 24 = 768 HTTP 200**, zéro 503 et 256 requêtes par route
`/`, `/health`, `/health/ready`. 768 identités de requête/paires Activity
distinctes corrélées, 768 refresh.completed, 768 http.decision,
12 288 observations Npgsql filtrées ; lifecycle complet et zéro connexion
corrélée résiduelle. Pas de retry ou allongement de deadline.

**P03-F1 OPEN / UNKNOWN** : **749 HTTP 200 / 19 HTTP 503 historiques**,
cause non démontrée, risque accepté avec réserve ; propriétaire responsable
technique LogicGate SiteFactory, réexamen **2026-10-23**.
Aucune campagne verte ne corrige ou n'explique rétroactivement F1.
[Contrat de concurrence](../readiness-concurrency-regression.md).

## Ce qui reste dans l'E2E Business historique

La suite privée possède **20 Facts** sur cinq anciens shards
(core/migrator/compatibility/hosts/e2e). Son E2E de fondation demeure distinct :

1. génération d'un projet client réel, restore/build/tests, sans retouche manuelle ;
2. base/rôles isolés, migration et bootstrap avec le véritable exécutable client ;
3. vrais Web et Worker clients sur la même base, avec credentials séparés ;
4. refus du credential Migrator dans les runtimes, identité incompatible,
   base jamais migrée et PostgreSQL indisponible, sans auto-migration ;
5. snapshots catalogue/ACL/defaults/objets et métadonnées/xmin avant/après ;
6. régénération préservant customizations et configuration, lock déterministe,
   migration non dupliquée, nouveau build/tests puis redémarrage des assemblies reconstruites.

Les 235 tests client et les checks des véritables processus Worker de cet
historique sont **privés**, pas 235 tests publics ou des Facts ajoutés à P04.
Cette documentation ne réduit pas l'E2E privé et ne prétend pas l'avoir rejoué.
Aucun lien privé obligatoire ne conditionne le harnais public.

## Exécution et limites

La commande publique complète figure dans [README](README.md) :
`tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired`.
Chaque invocation de shard garde 360 s, avec budgets auxiliaires bornés et
preuve globale fail-closed. Aucun succès fondé uniquement sur un exit code.

Migrations, bootstrap et provisioning sont explicites et limités aux
environnements autorisés ; aucun composant n'infère une cible production.
Hors portée : provisioning/exploitation PostgreSQL production, données métier,
jobs durables, Docker client, backup/restore et déploiement.
Cette adaptation ne lance ni build/test, ni PostgreSQL/Docker, ni CI.
