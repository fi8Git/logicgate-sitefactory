# Budgets PostgreSQL — public courant et mesures Business historiques

## Harnais public P04

Le [harnais Public](Public/Invoke-PostgreSqlTests.ps1) exécute séquentiellement
**g04=5, g05=3, p01=8, p02=4, p03=3**, selon son
[manifest fermé](Public/shards.json).
Deux inventaires distincts : **20 Facts Infrastructure et 3 Facts Hosts**.
Un seul conteneur PostgreSQL 18.6 est provisionné pour la campagne, TLS VerifyFull,
bases/rôles isolés ; chaque invocation dotnet test conserve **360 s maximum**.
Il n'existe pas de limite globale implicite de 360 s ni de retry automatique.

Les budgets auxiliaires restent ceux du script : discovery/Docker 180 s,
readiness DB 60 s, configuration/archivage/cleanup opérationnel 30 s,
cleanup du scope 10 s et marge transport bridge 25 s.
[Exécution et règles fail-closed](README.md).

P04 final a acquis les mesures suivantes, **Windows .NET vers PostgreSQL Linux
18.6 temporaire**, pas un runner CI Linux :

| Shard public | Facts Passed | Processus dotnet test | Borne |
|---|---:|---:|---:|
| g04 | 5 | 128,822 s | 360 s |
| g05 | 3 | 43,126 s | 360 s |
| p01 | 8 | 7,290 s | 360 s |
| p02 | 4 | 10,655 s | 360 s |
| p03 | 3 | 52,154 s | 360 s |

**23/23 Passed**, cinq TRX complets, aucun Failed/Skipped/NotExecuted/Aborted.
Cleanup DB/rôles/sessions, conteneur, certificats/secrets et processus vérifié ;
ressources préexistantes conservées. Ces valeurs sont des observations acquises,
pas des seuils à incorporer aux tests, ni un nouveau rejeu de P04.

## Pourquoi les anciennes mesures ne décrivent pas cette partition

L'incident historique Business de septembre 2026 atteignait exactement 360 s
après provisioning : 18 résultats observés Passed et deux NotObserved.
Aucun TRX complet ni mesure de phase n'établissait la cause distante.
**NotObserved ne signifie pas bloqué** : queued, running ou unreported restent possibles.
L'URL et le SHA du repository privé ne sont pas nécessaires au runbook public.

La suite privée conservait 20 Facts sur de vrais projets clients générés,
avec **9 générations, 8 restores, 9 builds et deux dotnet test client**.
Ces coûts ne sont ni attribués aux bibliothèques publiques extraites ni
supprimés rétroactivement par leur séparation.

Mesures locales historiques, Windows, SDK 10.0.301 et PostgreSQL Linux 18.6 :

- première mesure non partitionnée : 227,972 s pour 20 Passed ;
- mesure instrumentée : 257,316 s, **19 Passed / 1 Failed Web** ;
- builds concurrents 40,252–64,476 s, contre 18,039 s pour le dernier ;
  restores concurrents 19,858–29,968 s, contre 5,843 s ;
- probe et permissions, chacun isolé après les deux anciens NotObserved :
  respectivement 50,815 s et 28,083 s, Passed.

L'échec Web intermédiaire reste rouge, cause non établie ; un succès ultérieur
ne le transforme pas en preuve verte. Les mesures établissaient un coût répété
et une dégradation lorsque les préparations se chevauchaient, **pas** la cause
certaine du timeout Linux. Aucun processus/host bloqué n'était démontré ;
aucune mesure PostgreSQL Linux locale n'avait été obtenue.

## Cinq anciens shards Business — pas les cinq shards publics

| Shard historique privé | Facts | Dernier processus local observé |
|---|---:|---:|
| core | 12 | 7,385 s |
| compatibility | 2 | 57,554 s |
| e2e | 1 | 67,446 s |
| hosts | 3 | 78,983 s |
| migrator | 2 | 160,931 s |

Ces cinq TRX historiques prouvaient 20 Passed. Maximum 160,931 s sous la borne
inchangée ; somme séquentielle 372,299 s, hors discovery/provisioning/cleanup.
La première partition avait une somme de 374,153 s. Ce n'était **pas** un gain de
durée locale totale face aux 227,972 s initiaux ; la réduction visait le chemin
critique par runner. Ces mesures ne constituent pas une CI publique validée.
L'E2E privé intégral, ses builds/tests et sa régénération restent privés.

## Concurrence, métriques et réserve actuelles

Distinguer parallélisme xUnit, shards et HTTP : P04 lance ses shards en séquence ;
P03 conserve **32 × 24 = 768 requêtes**, sans réduire la charge.
P04 final : **768/768 HTTP 200**, zéro 503, 256 par route, événements corrélés
et lifecycle complet. Les métriques G04/P01/P02 sont allowlistées ;
G05/P03 ne reçoivent pas implicitement de nouvelles métriques.
Les durées de phases qui se recouvrent ne s'additionnent pas en wall-clock.

**P03-F1 OPEN / UNKNOWN** : **749 HTTP 200 / 19 HTTP 503 historiques**,
cause non démontrée, risque accepté avec réserve ; responsable technique
LogicGate SiteFactory, réexamen **2026-10-23**. Ne pas attribuer ces 19 refus
à une contention, queue timeout, lenteur PostgreSQL ou race sans preuve causale.
Les campagnes vertes n'ont pas corrigé F1.
[Observabilité et concurrence](../readiness-concurrency-regression.md).

La supervision reste limitée aux processus possédés ; timeout, exit non nul,
TRX incomplet et cleanup échoué restent des échecs. Pas de kill global ni de
relance jusqu'au vert. TRX natifs archivés seulement après contrôle sensible,
preuves expurgées distinctes et dossiers de campagne neufs.
CI distante publique **non encore validée**. Aucun test, build, Docker,
PostgreSQL ou CI lancé pendant cette mise à jour documentaire.
