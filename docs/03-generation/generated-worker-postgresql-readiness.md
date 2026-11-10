# Readiness PostgreSQL du Worker public

Décision humaine confirmée le 2026-09-25 : rôle Worker attendu obligatoire et
conservation du fail-fast. Une erreur locale ou PostgreSQL bloque le démarrage
avant tout `IHostedService.StartAsync`. Le service fournit des diagnostics fixes,
sans secret ; le code de sortie du processus appartient au host consommateur.
Aucun retry ou maintien artificiel du host en cas d'échec. Ce choix est permis par
[ADR-0031 §§17–19, 98 et 106–107](../adr/0031-application-startup-validation-and-runtime-compatibility.md).

## Connexion et rôle exclusifs

Le Worker exige sa configuration runtime explicite (client, environnement, URL),
`SITEFACTORY_WORKER_CONNECTION_FILE` et désormais `SITEFACTORY_WORKER_EXPECTED_ROLE`.
Cette dernière valeur non sensible représente le login attendu, indépendamment
du username contenu dans le secret. Elle est requise, sans espaces périphériques
ni caractères de contrôle, bornée à 63 octets UTF-8. Aucun nom de production,
credential ou environnement par défaut n'est généré.

Le seul secret consulté est celui du Worker. Aucun repli vers Web, Migrator ou
Owner, même si leurs références sont disponibles. L'identité observée et les
privilèges sont contrôlés par le [probe SF-TASK-046](./generated-postgresql-compatibility-probe.md),
avec la baseline read-only du Sprint 5 : aucune permission métier supplémentaire.
Les règles [ADR-0027](../adr/0027-database-roles-and-runtime-least-privilege.md)
et la [frontière de secrets](./generated-postgresql-runtime-configuration.md) restent inchangées.

## Démarrage et readiness

[WorkerDatabaseStartupValidator](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Runtime/WorkerDatabaseStartupValidator.cs)
est un adaptateur Infrastructure.PostgreSql du contrat Runtime.Contracts
`IRuntimeStartupValidator`. Il réutilise le pipeline local, puis le probe existant.
La décision reste dans Runtime.Database pur ; aucune logique SQL ou de compatibilité dupliquée.
`RuntimeStartupService` conserve son comportement : un résultat bloquant lève
`RuntimeStartupException` pendant `StartingAsync`, avant le démarrage des services.

Un succès rend le Worker ready uniquement après `ApplicationStarted`. Le Generic
Host reste ensuite idle jusqu'à cancellation ou Ctrl+C, puis s'arrête proprement.
Il n'y a qu'un service hébergé de startup : aucun job, polling, serveur HTTP,
outbox, boucle de maintien ou provider métier. `ApplicationStopping` retire readiness.

La configuration est contrôlée avant la connexion. Le lecteur PostgreSQL conserve
sa deadline de 5 secondes, ses timeouts et le token appelant ; aucun nouveau retry.
Les diagnostics existants distinguent disponibilité, client, environnement, schéma,
migration et rôle/privilèges, sans recopier connexion, SQL, secret ou exception provider.
Les erreurs de programmation ne sont pas absorbées par un catch global.

Cette coupe valide **le startup**, pas une surveillance continue de la base.
Le rapport mémorise le contrôle initial. Puisqu'aucun traitement n'existe, aucune
revendication de job n'est ajoutée pour simuler une garantie future. L'introduction
des jobs devra imposer le contrôle de readiness/compatibilité à la frontière de
traitement et gérer la perte de connexion ou les dérives après démarrage.
Le comportement et la réévaluation HTTP du [Web SF-TASK-047](./generated-web-postgresql-readiness.md)
ne sont pas modifiés. Le Migrator, les migrations et les ACL restent inchangés.

## Composition et preuves publiques

Le host appelle explicitement AddRuntimeStartupValidation,
AddPostgreSqlStartupValidation pour Worker et AddWorkerDatabaseStartupValidation.
Le dernier adaptateur remplace l'orchestrateur de host par le validateur Worker ;
il réutilise RuntimeStartupValidator sans copier sa politique pure.
La bibliothèque ne génère pas un Program client ni un serveur HTTP Worker.

Les [tests offline](../../tests/LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/WorkerDatabaseReadinessTests.cs)
couvrent rôle attendu, défaut local, diagnostics, service sentinelle non démarré,
succès idle, annulation, arrêt, absence de fallback et erreur de programmation.
Les lecteurs contrôlés ne constituent pas une preuve PostgreSQL.

Le [test réel P03](../../tests/LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWorkerReadinessTests.cs)
construit un Generic Host public : identité/environnement/schéma/rôle/privilèges,
credentials Web/Migrator refusés, Owner NOLOGIN, indisponibilité bornée,
cancellation et arrêt propre, sans mutation de métadonnées/historique/xmin.
Le host compatible reste idle ; aucun faux job n'est ajouté.

Le [harnais public](../../tests/PostgreSql/README.md) exige 20 Facts Infrastructure
et trois Hosts, répartis en cinq shards séquentiels ; P04 23/23 Passed
acquis localement Windows .NET vers PostgreSQL Linux 18.6, TLS VerifyFull/cleanup.
Les deux tests d'exécution du véritable Worker client et son exit/stderr restent
dans la suite Business privée ; ils ne sont pas revendiqués par ce host générique.
Aucun test rejoué ici ; CI distante Windows/Linux non validée.

P03-F1 reste **OPEN / UNKNOWN**, 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve ; réexamen **2026-10-23**.
Les campagnes 768/768 du Web ne corrigent pas rétroactivement ce finding.
Pas de jobs durables, surveillance continue, déploiement ou exploitation production.
