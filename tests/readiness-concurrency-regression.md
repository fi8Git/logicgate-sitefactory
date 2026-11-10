# Readiness concurrente — public courant et réserve P03-F1

## Publication complète et décision locale

La correction historique F3 supprime l'état partagé null transitoire pendant
un nouveau probe. Elle reste distincte du finding **P03-F1 OPEN / UNKNOWN**.
Les sources publiques sont
[WebDatabaseReadiness](../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Runtime/WebDatabaseReadiness.cs)
et [RuntimeHealthEndpoints](../src/LogicGate.SiteFactory.Runtime.AspNetCore/RuntimeHealthEndpoints.cs).

Le rapport complet est calculé localement puis publié ; une requête HTTP
décide sur **son propre résultat**, pas une relecture globale qu'un probe
concurrent pourrait remplacer. Un check en cours n'est pas un échec.
Annulation active/en attente : propagation sans effacer le dernier rapport
complet ni publier un succès arbitraire.

Le sémaphore conserve une attente bornée à 5 s ; la lecture PostgreSQL a son
propre délai borné. Une vraie incompatibilité, indisponibilité ou expiration
reste fail-closed. Aucun cache de succès, retry, baisse de charge ou allongement
des deadlines n'est introduit.

## Preuves publiques sans base, synchronisées

[WebDatabaseReadinessTests](LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/WebDatabaseReadinessTests.cs)
réutilise le vrai pipeline Web générique avec reader contrôlé :

- `CompletedRequestUsesItsOwnResultWhileAnotherProbeRuns`, six variantes :
  trois routes × second probe compatible/incompatible ;
- `RefreshCancellationIsPropagatedAndKeepsTheLastCompleteReport` ;
- `CompatibleRealHttpPipelineHasNoFalseFailuresUnderConcurrentTraffic` ;
- événements décisionnels, vrai queue timeout et exceptions distingués par
  `ReadinessEventsIdentifyOwnDecisionOnEveryRealHttpRoute`,
  `QueueTimeoutProducesCausallyMatched503WithoutAnotherReaderCall` et
  `ThrownCheckEventIsDistinctFromTheFrameworksFinal503`.

Interleaving déterministe : A termine compatible, sa continuation HTTP est
retenue ; B commence et reste bloquée ; A est libérée et doit retourner 200.
Si B termine incompatible avant A, B retourne 503 mais A doit encore retourner
200. Une correction limitée au retrait du null ne suffirait pas à ce second cas.
Pas de port fixe, busy-loop ou attente arbitraire.
Ces tests contrôlés ne sont pas présentés comme une preuve PostgreSQL.

## Trois Facts P03 avec PostgreSQL réel

Le [manifest public](PostgreSql/Public/shards.json) attribue à la seule assembly
LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests :

- [PostgreSqlWebReadinessTests](LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebReadinessTests.cs) :
  `RealWebReadinessUsesPostgreSqlWithoutWritingAndKeepsLivenessIndependent` ;
- [PostgreSqlWorkerReadinessTests](LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWorkerReadinessTests.cs) :
  `RealWorkerValidatesItsOwnDatabaseBeforeStartingAndRemainsIdleWithoutWrites` ;
- [PostgreSqlWebConcurrencyTests](LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/PostgreSql/PostgreSqlWebConcurrencyTests.cs) :
  `RealWebReadinessHasNoFalseFailuresUnderConcurrentTraffic`.

Hosts génériques, vrai reader, rôles distincts et base isolée, sans remplacement
du port DB ni import d'un exécutable client.
DB incompatible : Web live 200, ready et trafic 503 ; Worker refuse le démarrage
avant ApplicationStarted. Aucun DDL/DML, migration/bootstrap ou réparation runtime.

## Critère de capacité et observations

**32 workers × 24 requêtes = 768**, toutes HTTP 200, **0 HTTP 503**,
256 par route /, /health et /health/ready.
P04 final a acquis **23/23 Facts Passed**, cinq TRX dont p03=3, TLS VerifyFull,
PostgreSQL Linux 18.6, cleanup vérifié, et le stress **768/768 HTTP 200**.
Ce résultat est réutilisé, pas une nouvelle campagne documentaire.

Observations : 768 requêtes et paires Activity distinctes corrélées,
768 refresh.completed, 768 http.decision et 12 288 observations Npgsql filtrées ;
lifecycle complet, zéro connexion corrélée résiduelle.
Une TraceId partagée avec des SpanIds distincts peut être valide ;
une unique Activity réutilisée pour toutes les requêtes ne l'est pas.
Aucune observation manquante ou réponse inattendue n'est acceptée.

Les décisions READY/BLOCKED/QUEUE_TIMEOUT, NOT_INITIALIZED, exceptions,
annulations et décisions HTTP sont distinguées dans l'observabilité existante.
Un 503 doit être corrélé aux événements de sa propre requête :
ne pas déduire une cause de l'état partagé ou d'une absence d'événement provider.
Une campagne contenant un 503 échoue même si le délai contractuel explique ce
nouveau refus. Les deadlines et le critère strict restent inchangés.

## F1 historique : ouvert, pas corrigé par les campagnes vertes

**P03-F1 OPEN / UNKNOWN**.
Historique : **749 HTTP 200 / 19 HTTP 503**, cause non démontrée.
Risque résiduel accepté avec réserve ; responsable technique LogicGate
SiteFactory ; réexamen humain du 2026-10-16, échéance **2026-10-23**.

Les campagnes ultérieures 768/768, l'observabilité ajoutée après l'échec et
l'expérimentation ciblée sans reproduction ne démontrent **ni la cause ni un
correctif causal**. L'investigation historique est terminée ; F1 reste ouvert.
Les 19 refus ne sont attribués ni au sémaphore, ni à PostgreSQL, ni à une race,
ni à une cancellation sans événement causal correspondant.
Toutes les preuves rouges/vertes restent conservées, sans relance jusqu'au vert.

## Frontière historique Business et limites

La correction F3 de septembre 2026 avait été reproduite dans les tests clients
privés : 226 Passed / 9 Failed, puis 235/235 après correction, avec génération,
build/tests dans deux racines et régénération. Ces nombres restent **historiques
Business**, pas des Facts ou tests publics ajoutés au harnais.
Les vrais exécutables Worker et leur exit/stderr restent des preuves privées.

Le [harnais public](PostgreSql/README.md) exige 20 Facts Infrastructure et 3 Hosts
distincts, cinq shards et TRX stricts. CI distante **non encore validée** ;
Windows .NET contre PostgreSQL Linux n'est pas une preuve de serveur natif Windows.
Aucun build/test, PostgreSQL/Docker ni CI lancé pendant cette adaptation.
