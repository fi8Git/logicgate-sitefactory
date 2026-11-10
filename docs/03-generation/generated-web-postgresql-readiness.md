# Readiness PostgreSQL du Web public

Décision humaine confirmée le 2026-09-25 : le Web conserve HTTP lorsque sa phase
PostgreSQL échoue, avec liveness disponible et trafic applicatif interdit. Les
erreurs de configuration structurelle, de secret et de rôle attendu restent
fail-fast, avant toute connexion. SF-TASK-047 ne modifiait ni Worker ni Migrator.
Le [startup PostgreSQL Worker SF-TASK-048](./generated-worker-postgresql-readiness.md)
ajoute ensuite le contrôle Worker fail-fast, sans changer le présent comportement Web.
Ce comportement applique [ADR-0031 §§19, 75–78, 97 et 108–111](../adr/0031-application-startup-validation-and-runtime-compatibility.md).

## Configuration explicite

Le Web exige les paramètres runtime déjà prévus (client, environnement explicite,
PublicBaseUrl) ainsi que :

- `SITEFACTORY_WEB_CONNECTION_FILE` : référence du seul fichier secret Web, selon
  [SF-TASK-041](./generated-postgresql-runtime-configuration.md) ;
- **`SITEFACTORY_WEB_EXPECTED_ROLE`** : nom du login Web attendu, fourni séparément
  par la configuration d'environnement. Requis, non blanc, borné, sans caractère
  de contrôle ni espace périphérique. Aucune valeur générée ou par défaut.

Le rôle attendu n'est jamais déduit du username du credential ou du rôle observé.
Une connexion Worker ne devient donc pas une connexion Web parce que ses droits
sont semblables. Les noms de rôles de production ne sont pas inventés ; l'opérateur
doit fournir ceux de son provisioning. Cette valeur non sensible suit les sources
de configuration .NET existantes ; elle n'appartient pas au YAML client ou au lock.
Elle n'est jamais recopiée dans le health endpoint ou les diagnostics.

## Ordre et résultats

[WebDatabaseReadiness](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Runtime/WebDatabaseReadiness.cs)
est un adaptateur Infrastructure.PostgreSql de `IRuntimeStartupValidator`.
Il exécute d'abord le pipeline local existant, vérifie le rôle attendu, puis appelle
le vrai [probe SF-TASK-046](./generated-postgresql-compatibility-probe.md). La décision
de compatibilité reste dans Runtime.Database pur, sans IConfiguration, EF ou Npgsql.
Les contrôles du probe, les migrations et les permissions ne sont pas modifiés.

| Situation | HTTP | Liveness | Readiness et routes applicatives |
| --- | --- | --- | --- |
| Configuration/secret/rôle attendu invalide | Non démarré | Pas de listener | Startup bloqué |
| PostgreSQL accessible et compatible | Démarré | `200` | `200` |
| PostgreSQL inaccessible, mauvaise identité, schéma ou rôle | Démarré | `200` | `503` |
| Arrêt du host | Arrêt en cours | Dépend du listener | Non-ready |

Les réponses JSON d'échec restent exactement `{"status":"unavailable"}` ; les
health checks réussis retournent `{"status":"healthy"}`. `Cache-Control: no-store`,
aucun hostname PostgreSQL, username, password, SQL, connexion ou exception. La route
historique `/health` garde son format texte et suit la readiness. Le middleware
bloque toutes les routes non marquées comme health endpoints, y compris `/`.

`RuntimeStartupService.Report` reste le rapport historique immuable, avec les codes
stables du probe et les logs structurés initiaux. `WebDatabaseReadiness.CurrentReport`
est le dernier résultat DB **complet** en mémoire ; il n'est pas publié par HTTP. `WebDatabaseReadiness.IsReady` reflète ce résultat complet et la configuration ;
`RuntimeStartupService.IsReady` ajoute les signaux ApplicationStarted/Stopping.
Un succès historique ne masque donc pas une base devenue incompatible.

## Réévaluation et limites

Chaque requête readiness ou applicative réalise une nouvelle vérification read-only.
La liveness n'en réalise aucune. Aucun cache de succès, boucle de fond, polling,
retry ou auto-réparation n'est ajouté. Une seule vérification DB peut s'exécuter à
la fois par host ; l'attente du sémaphore est bornée à 5 secondes, puis le lecteur
conserve sa deadline de 5 secondes et ses timeouts Npgsql. Une requête en attente
échoue fermée si le délai expire. Annulation HTTP et arrêt du host sont propagés.
Ce choix simple implique le coût du probe sur le trafic applicatif ; une politique
de rafraîchissement/caching éventuelle demandera une tâche et des preuves dédiées.

Après rétablissement de la connectivité ou correction externe d'une incompatibilité,
une nouvelle vérification peut rendre le Web ready. Le Web ne change jamais les
ACL, le schéma, DatabaseIdentity ou SchemaState, ne migre pas et n'initialise rien.
Les snapshots de configuration/secrets ne sont pas rechargés dynamiquement.

## Concurrence — SPRINT-005-CORR-003

L'adaptateur public conserve une seule vérification DB active par host. Deux
requêtes concurrentes sont sérialisées par le sémaphore existant ; chacune obtient
le résultat de sa propre vérification. Le calcul reste local, puis le rapport
immuable terminé est publié atomiquement. Commencer une vérification ne publie
jamais `null` ou un échec provisoire. L'ordre de publication est celui des probes
terminés, sans risque qu'un ancien probe lent écrase un résultat plus récent.

Les routes `/health/ready`, `/health` et le garde applicatif ne relisent plus la
readiness DB globale après leur probe. Un autre appel peut être en cours, voire
avoir publié un résultat différent : cela ne change pas le résultat de l'appel
terminé. Le signal d'arrêt du host reste vérifié avant d'autoriser la réponse.
Un résultat réellement incompatible produit toujours `503` ; DB indisponible
laisse la liveness à `200`. Aucun résultat historique n'autorise une nouvelle
requête sans probe. Les limites d'attente et de lecture restent inchangées.

Une annulation se propage, ne publie pas de succès ni n'efface le dernier rapport
complet (succès ou échec). Ce dernier rapport ne signifie pas que la vérification
annulée a réussi. Aucune boucle, tâche de fond ou réparation n'est ajoutée.

Les [tests offline](../../tests/LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/WebDatabaseReadinessTests.cs)
synchronisent A après publication de son probe et avant décision HTTP, bloquent B
dans le reader, puis attendent A=200 sur les trois routes. Une variante termine B
incompatible avant A et prouve l'indépendance des décisions. Aucun hook runtime,
scheduling probabiliste ou nouveau délai. Les readers contrôlés prouvent la race
F3 et le pipeline HTTP, pas PostgreSQL.

## Preuves PostgreSQL et observabilité

Les [tests P03](../../tests/LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebReadinessTests.cs)
utilisent les adaptateurs, le vrai reader et Kestrel sur port dynamique, sans
host Business. Base valide/down, mauvais client/environnement/schéma/rôle,
privilèges excessifs et dérive après startup ; snapshots DB avant/après
identiques. Seule la préparation adversariale de la base jetable écrit.

Le [stress réel](../../tests/LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebConcurrencyTests.cs)
conserve **32 workers × 24 = 768 requêtes**, 256 pour chacune de
`/`, `/health`, `/health/ready`, sans retry et deadlines inchangées.
Une campagne verte exige 768 HTTP 200, zéro 503/inattendu ;
chaque requête possède sa décision locale et sa corrélation Activity.
768 refresh.completed, 768 http.decision et 12 288 observations Npgsql filtrées
sont conservés avec le lifecycle et zéro connexion corrélée résiduelle.
TraceId partagée avec SpanIds distincts valide ; une Activity réutilisée
pour toutes les requêtes est refusée.

Le [harnais public](../../tests/PostgreSql/README.md) conserve deux inventaires
fermés (20 Infrastructure + 3 Hosts), cinq shards et cinq TRX exacts ;
P04 23/23 Passed et P03 768/768 acquis localement en TLS VerifyFull avec cleanup.
Les preuves ne sont pas rejouées pendant cette adaptation documentaire.

**P03-F1 OPEN / UNKNOWN** : 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve, réexamen **2026-10-23**.
Les campagnes positives ne démontrent pas sa résolution ni une attribution
aux timeouts, à la contention ou à une race.
La correction F3 est une garantie distincte de cette capacité historique.

Le contrat conserve un sémaphore borné : les événements distinguent READY,
BLOCKED, QUEUE_TIMEOUT, NOT_INITIALIZED, CANCELLED et UNEXPECTED_ERROR ;
le HTTP distingue notamment ACCEPTED, REFRESH_REJECTED et HOST_STOPPING.
Aucun détail sensible n'est exposé au public.
CI distante non validée ; les E2E d'exécutables clients restent privés.
