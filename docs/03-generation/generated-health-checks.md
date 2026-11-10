# Liveness et readiness : adaptateur HTTP public

[RuntimeHealthEndpoints](../../src/LogicGate.SiteFactory.Runtime.AspNetCore/RuntimeHealthEndpoints.cs)
dans Runtime.AspNetCore expose `AddRuntimeHealthChecks`,
`MapRuntimeHealthEndpoints` et `UseRuntimeReadinessGate`.
Le host les compose explicitement avec
[Runtime.Hosting](generated-startup-validation.md), et éventuellement
[WebDatabaseReadiness](generated-web-postgresql-readiness.md).
La CLI Documentation ne génère pas elle-même ce host.

## Contrat public

| Route | Condition | Réponse |
|---|---|---|
| GET /health/live | Pipeline HTTP actif, aucun check enregistré exécuté | 200, `{"status":"healthy"}` |
| GET /health/ready | Host démarré, non stopping et vérification ready | 200 healthy, sinon 503 unavailable |
| /health | Même sélection readiness, compatibilité historique | 200 Healthy ou 503 Unhealthy, texte |

Les deux routes nouvelles sont GET seulement.
JSON `application/json; charset=utf-8`, champ status uniquement.
Les trois routes imposent **Cache-Control: no-store**, y compris en échec.
Aucun chemin, ClientSlug, hostname DB, username, secret, SQL, exception,
stack trace ou détail de HealthReport n'est sérialisé.
Le writer public ne propage jamais les détails d'un check.

La liveness ne dépend ni de PostgreSQL, ni du réseau, ni d'un provider.
Les checks de tag ready sont sélectionnés pour readiness :
un état Unhealthy ou Degraded donne 503 ; un warning non bloquant du
pipeline startup n'est pas, à lui seul, un état non-ready.

## Décision locale et lifecycle

Sans adaptateur DB, readiness utilise le rapport startup.
Avec DB, elle attend `IRuntimeReadinessRefresh.RefreshAsync` et décide du
booléen de **cette vérification**, pas d'une relecture d'un état partagé
qu'une autre requête pourrait modifier. Seuls des rapports complets sont publiés.
ApplicationStarted et ApplicationStopping restent contrôlés ; annulation HTTP
et arrêt sont propagés. Le [contrat du refresh](generated-web-postgresql-readiness.md)
conserve ses délais bornés, sans cache de succès ni retry.

Le middleware de trafic refuse les routes non marquées health avec 503 et
un corps minimal tant que le host n'est pas prêt.
Configuration/secret/rôle attendu invalides bloquent l'ouverture du Web.
DB inaccessible ou incompatible après prérequis locaux valides laisse
live 200, ready et trafic 503, sans migration/réparation.

Le [Worker](generated-worker-postgresql-readiness.md) expose un état interne
testable, pas un serveur HTTP imposé. Un rapport initial réussi ne maintient
pas readiness après ApplicationStopping.

## Preuves existantes et limites

Les tests Runtime.AspNetCore et Runtime.PostgreSql.Hosts couvrent live/ready,
startup bloqué, no-store, absence de détails sensibles, décisions locales,
annulation, arrêt et concurrence déterministe.
Les tests HTTP utilisent de vrais pipelines et des ports dynamiques ;
les readers contrôlés offline ne prouvent pas PostgreSQL.

La [preuve PostgreSQL publique](../../tests/PostgreSql/README.md) est distincte :
20 Facts Infrastructure + 3 Hosts, cinq shards ; P04 23/23 Passed et
P03 32 × 24 = 768 HTTP 200 acquis localement, sans replay dans cette tâche.
P03-F1 reste **OPEN / UNKNOWN**, 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve ; réexamen **2026-10-23**.
Ces campagnes vertes ne démontrent pas une correction causale.
CI distante non validée ; pas d'endpoint opérateur détaillé ou monitoring
production complet annoncé.
