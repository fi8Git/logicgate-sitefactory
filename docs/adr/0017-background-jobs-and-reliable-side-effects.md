---
adrId: ADR-0017
title: Execute background jobs and external side effects through a durable PostgreSQL-backed workflow
status: accepted
date: 2026-07-29
decisionOwners:
  - LogicGate
authors:
  - LogicGate
reviewers: []
supersedes: []
supersededBy: null
relatedTasks: []
relatedDocuments:
  - ../../README.md
  - ../../AGENTS.md
  - ../03-generation/13-generation-pipeline.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/09-module-architecture.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ../03-generation/generated-runtime-database-model.md
  - ../04-quality-security/18-security-baseline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/21-observability-and-logging.md
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/23-backup-and-restore.md
  - ../05-operations/24-operational-runbooks.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../05-operations/27-client-handover-and-service-closure.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0003-postgresql-as-primary-database.md
  - ./0004-explicit-production-migrations.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0016-content-and-media-storage-boundaries.md
targetVersion: 0.1.0
---

# ADR-0017 — Jobs en arrière-plan et effets de bord fiables

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le Worker générique public utilise le lifecycle du Generic Host et peut
rester idle après validation ; il n'est **pas** un moteur de jobs.
Jobs durables, outbox, polling, SMTP et handlers métier ci-dessous restent
des exigences d'architecture future, pas des fonctionnalités livrées.
Le [runtime actuel](../03-generation/generated-worker-postgresql-readiness.md)
valide sa compatibilité avant tout travail et respecte l'annulation.
Les garanties cibles at-least-once, idempotence, transactions et retries
bornés restent intactes ; aucune promesse exactly-once n'est ajoutée.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory adopte un mécanisme persistant de jobs en arrière-plan reposant initialement sur PostgreSQL.

Les effets de bord différables ou potentiellement longs ne doivent pas être exécutés directement dans la requête HTTP principale.

Cela concerne notamment :

- les emails ;
- les notifications ;
- les transformations de médias ;
- les exports ;
- les synchronisations externes ;
- les appels CRM ou ERP ;
- les tâches de maintenance ;
- les suppressions différées ;
- les réconciliations ;
- certains traitements SEO ;
- certains traitements d’import.

Les jobs critiques doivent survivre :

- au redémarrage de l’application ;
- au redémarrage du worker ;
- à une interruption réseau ;
- à une indisponibilité temporaire d’un fournisseur ;
- à un déploiement ;
- à un crash du processus.

Le système garantit une exécution :

```text
at-least-once
```

Il ne prétend pas garantir une exécution exactement une fois pour les effets externes.

Les handlers doivent donc être idempotents.

---

## 2. Résumé

L’architecture de référence est :

```text
Requête HTTP ou cas d’usage
→ transaction PostgreSQL
→ données métier
→ message outbox dans la même transaction
→ commit

Worker
→ lit l’outbox
→ matérialise les jobs destinataires
→ revendique un job avec un lease
→ exécute le handler idempotent
→ enregistre le résultat
→ retry borné ou dead-letter
```

Le profil Docker Compose peut contenir :

```text
web
worker
postgres
```

Le service `worker` :

- utilise la même image applicative ou un artefact compatible ;
- fonctionne dans un mode d’exécution distinct ;
- partage PostgreSQL et les stockages autorisés ;
- ne publie aucun port Internet ;
- possède ses propres health checks et limites de ressources.

La baseline n’introduit pas de broker externe tel que :

- RabbitMQ ;
- Kafka ;
- Azure Service Bus ;
- Redis Streams.

Un broker pourra être introduit ultérieurement si le volume ou l’architecture distribuée le justifie.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Un effet durable ne repose pas sur Task.Run.

Un effet critique ne repose pas sur une queue uniquement en mémoire.

Une transaction métier et son message outbox sont validés ensemble.

Un job peut être exécuté plusieurs fois.

Tout handler possède une stratégie d’idempotence.

Un retry est borné.

Un job définitivement échoué devient visible et exploitable.

Un payload de job ne contient aucun secret.

Un job persistant n’utilise pas un nom de type CLR comme contrat.

Un worker ne conserve pas une transaction PostgreSQL ouverte
pendant toute la durée d’un appel externe.

Une requête réussie ne signifie pas nécessairement
que tous ses effets externes sont déjà terminés.

Une sauvegarde restaurée peut rejouer des jobs ;
le worker reste donc arrêté jusqu’à vérification.
```

---

## 4. Contexte

Une application SiteFactory peut déclencher des opérations dont la durée ou la fiabilité ne convient pas à une requête HTTP synchrone.

Exemples :

```text
Création d’un lead
→ envoyer une notification

Upload d’une image
→ créer plusieurs variantes

Demande d’export
→ générer le fichier
→ le stocker
→ notifier l’utilisateur

Publication d’un contenu
→ invalider un cache externe
→ notifier un service tiers

Suppression d’un média
→ attendre le délai de grâce
→ supprimer les variantes
→ supprimer le blob

Synchronisation CRM
→ appeler un fournisseur
→ gérer les rate limits
→ réessayer en cas d’erreur transitoire
```

Une exécution synchrone dans la requête entraînerait :

- une latence accrue ;
- des timeouts ;
- une expérience utilisateur dégradée ;
- des transactions trop longues ;
- une forte dépendance aux fournisseurs externes ;
- une duplication lors d’un retry navigateur ;
- une perte du travail après crash ;
- une faible capacité de diagnostic.

Une exécution simplement lancée en mémoire avec :

```csharp
Task.Run(...)
```

ou une queue volatile peut perdre l’opération au redémarrage.

Un broker externe améliorerait certaines capacités, mais ajouterait au MVP :

- un service supplémentaire ;
- des credentials ;
- des backups ou une haute disponibilité propre ;
- des diagnostics distribués ;
- davantage d’exploitation.

PostgreSQL est déjà présent dans chaque projet client et offre les primitives suffisantes pour la première version.

---

## 5. Problème à résoudre

Comment exécuter des traitements asynchrones et effets externes tout en garantissant :

- la persistance ;
- la reprise après crash ;
- l’idempotence ;
- des retries contrôlés ;
- une absence de duplication évitable ;
- un traitement des échecs définitifs ;
- une corrélation avec l’opération métier ;
- une isolation client et environnement ;
- des upgrades compatibles ;
- une exploitation proportionnée au MVP ?

La solution doit éviter :

- les tâches fire-and-forget ;
- les retries infinis ;
- les transactions PostgreSQL longues ;
- les appels externes avant le commit métier ;
- les payloads volumineux ;
- les secrets dans les jobs ;
- les handlers arbitraires chargés par réflexion ;
- les jobs inconnus après un upgrade ;
- la suppression silencieuse d’un job échoué ;
- les traitements concurrents incompatibles ;
- les tempêtes de rattrapage après une restauration.

---

## 6. Portée

Cette décision couvre :

- jobs persistants ;
- transactional outbox ;
- workers ;
- dispatch ;
- leases ;
- concurrence ;
- retries ;
- timeouts ;
- idempotence ;
- dead letters ;
- tâches planifiées ;
- tâches récurrentes ;
- emails ;
- transformations de médias ;
- exports ;
- intégrations externes ;
- suppression différée ;
- réconciliation ;
- observabilité ;
- health checks ;
- restauration ;
- upgrades ;
- sécurité ;
- opérations manuelles.

---

## 7. Hors portée

Cette décision ne définit pas :

- un broker distribué obligatoire ;
- une plateforme BPM ;
- un moteur complet de workflow humain ;
- une orchestration de microservices ;
- une garantie exactly-once externe ;
- un moteur de calcul distribué ;
- un système de streaming ;
- un cluster de workers multi-région ;
- une interface graphique complète de supervision ;
- tous les contenus exacts des emails ;
- les conditions contractuelles de chaque fournisseur.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Aucune perte silencieuse des effets critiques**
2. **Cohérence entre transaction métier et intention d’effet**
3. **Idempotence**
4. **Retries bornés**
5. **Reprise après crash**
6. **Simplicité d’exploitation**
7. **Utilisation de PostgreSQL déjà présent**
8. **Observabilité**
9. **Isolation client et environnement**
10. **Compatibilité avec les modules**
11. **Compatibilité avec les upgrades**
12. **Contrôle de la concurrence**
13. **Absence de secret dans les payloads**
14. **Possibilité future d’utiliser un broker**
15. **Transfert client**
16. **Coût d’infrastructure raisonnable**

---

## 9. Contraintes

La décision doit respecter :

```text
.NET 10

PostgreSQL

Docker Compose

Un repository et une base par client

Worker sans port Internet

Jobs persistants

Transaction outbox

Exécution at-least-once

Handlers idempotents

Retries et timeouts bornés

UTC

Payloads versionnés

Aucun secret dans un job

Aucun type arbitraire désérialisé

Observabilité OpenTelemetry

Migrations production explicites
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les volumes initiaux restent compatibles avec PostgreSQL ;
- une latence de quelques secondes est acceptable pour les effets asynchrones ordinaires ;
- un worker par projet suffit initialement ;
- plusieurs workers pourront être lancés ultérieurement ;
- les appels externes sont la principale source de retries ;
- les jobs peuvent être modélisés par des payloads JSON de taille limitée ;
- les gros fichiers sont stockés dans le media storage et non dans le payload ;
- les modules peuvent déclarer leurs handlers et tâches récurrentes ;
- les services externes ne garantissent pas tous l’idempotence.

---

# 11. Option A — Effets synchrones dans la requête HTTP

## 11.1 Description

L’application exécute immédiatement :

- email ;
- export ;
- transformation ;
- intégration externe ;

avant de retourner la réponse.

---

## 11.2 Avantages

- modèle simple ;
- résultat immédiat ;
- pas de worker ;
- pas de table de jobs ;
- erreurs visibles directement.

---

## 11.3 Inconvénients

- latence ;
- timeout ;
- transaction longue ;
- dépendance fournisseur ;
- faible résilience ;
- expérience utilisateur fragile ;
- difficulté de retry.

---

## 11.4 Risques

- lead créé mais réponse en erreur à cause de l’email ;
- doublon lors d’un nouveau submit ;
- connexion HTTP fermée pendant le traitement ;
- saturation des threads ;
- rollback métier causé par un fournisseur non critique.

---

## 11.5 Réversibilité

```text
moyenne
```

Les effets doivent ensuite être extraits des cas d’usage.

---

# 12. Option B — Queue en mémoire ou `Task.Run`

## 12.1 Description

L’application ajoute les tâches dans une queue locale ou lance une tâche non attendue.

---

## 12.2 Avantages

- rapide à implémenter ;
- faible latence HTTP ;
- aucune table supplémentaire ;
- aucun service externe.

---

## 12.3 Inconvénients

- perte au redémarrage ;
- aucune reprise fiable ;
- difficile à superviser ;
- pas de dead-letter ;
- déploiement risqué ;
- mémoire non bornée ;
- comportement différent entre instances.

---

## 12.4 Risques

- email perdu ;
- fichier jamais généré ;
- exception non observée ;
- tâche interrompue pendant le déploiement ;
- queue vidée après crash.

---

## 12.5 Réversibilité

```text
moyenne
```

---

# 13. Option C — Broker externe dès le MVP

## 13.1 Description

Les jobs sont publiés dans un service tel qu’un broker de messages.

---

## 13.2 Avantages

- découplage ;
- débit ;
- multi-consommateurs ;
- reprise ;
- distribution ;
- fonctionnalités de dead-letter.

---

## 13.3 Inconvénients

- infrastructure supplémentaire ;
- credentials ;
- monitoring ;
- sauvegarde ;
- coût ;
- disponibilité ;
- complexité distribuée ;
- opérations supplémentaires pour chaque client.

---

## 13.4 Risques

- publication réussie mais transaction métier échouée ;
- panne du broker ;
- mauvaise configuration de routing ;
- messages inter-clients ;
- coût disproportionné.

---

## 13.5 Réversibilité

```text
moyenne
```

Les contrats de messages restent utiles, mais l’infrastructure devient structurante.

---

# 14. Option D — Jobs PostgreSQL persistants et transactional outbox

## 14.1 Description

Les intentions d’effets liées à une transaction métier sont enregistrées dans une outbox PostgreSQL.

Un worker persistant les transforme et les exécute.

---

## 14.2 Avantages

- aucune infrastructure supplémentaire ;
- transaction avec les données métier ;
- reprise après crash ;
- observabilité ;
- dead-letter ;
- retries ;
- isolation par base ;
- backup commun ;
- migration future vers un broker possible.

---

## 14.3 Inconvénients

- tables supplémentaires ;
- worker ;
- gestion des leases ;
- maintenance ;
- nettoyage ;
- at-least-once seulement ;
- risque de charge sur PostgreSQL ;
- moteur interne à tester rigoureusement.

---

## 14.4 Risques

- concurrence mal implémentée ;
- jobs bloqués ;
- backlog important ;
- payload incompatible après upgrade ;
- rejeu après restauration ;
- croissance des tables.

---

## 14.5 Réversibilité

```text
élevée
```

L’outbox peut ultérieurement publier vers un broker sans modifier les cas d’usage métier.

---

# 15. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Synchrone | Mémoire | Broker externe | PostgreSQL + outbox |
|---|---:|---:|---:|---:|---:|
| Persistance | 5 | 3 | 1 | 5 | 5 |
| Cohérence transactionnelle | 5 | 4 | 2 | 3 | 5 |
| Simplicité d’infrastructure | 5 | 5 | 5 | 1 | 4 |
| Reprise après crash | 5 | 2 | 1 | 5 | 5 |
| Observabilité | 4 | 3 | 2 | 5 | 5 |
| Débit initial nécessaire | 3 | 2 | 3 | 5 | 4 |
| Isolation client | 5 | 4 | 3 | 3 | 5 |
| Coût MVP | 4 | 5 | 5 | 1 | 4 |
| Évolution future | 4 | 2 | 2 | 5 | 5 |
| Maintenabilité | 5 | 2 | 2 | 3 | 5 |

L’option D est retenue.

---

# 16. Décision

Nous décidons d’utiliser :

```text
PostgreSQL-backed background jobs
+ transactional outbox
+ worker dédié
```

pour les effets de bord fiables.

La plateforme fournit des contrats communs.

Les modules fournissent :

- les types de jobs ;
- les schémas de payload ;
- les handlers ;
- les politiques d’idempotence ;
- les timeouts ;
- les politiques de retry ;
- les éventuelles tâches récurrentes.

---

## 16.1 Pas de broker initial

Aucun broker externe n’est requis pour le MVP.

---

## 16.2 Pas de garantie exactly-once

Le système garantit qu’un job correctement persisté sera tenté au moins une fois tant qu’il reste supporté et que l’exploitation fonctionne.

Il ne garantit pas qu’un effet externe se produira exactement une fois.

---

## 16.3 Effets non critiques

Un effet non critique peut échouer sans annuler l’opération métier déjà validée.

L’échec doit néanmoins être :

- visible ;
- retryable lorsque pertinent ;
- exploitable.

---

# 17. Service worker

Le profil de déploiement peut ajouter :

```yaml
services:
  web:
    image: client-application

  worker:
    image: client-application
    command:
      - worker

  postgres:
    image: postgres
```

La syntaxe exacte dépendra de l’implémentation.

---

## 17.1 Même release

Le web et le worker utilisent la même release applicative ou des artefacts déclarés compatibles.

---

## 17.2 Responsabilités du worker

Le worker exécute :

- dispatch outbox ;
- jobs ;
- tâches récurrentes ;
- retries ;
- nettoyage ;
- réconciliation opérationnelle prévue.

---

## 17.3 Port réseau

Le worker ne publie aucun port Internet.

---

## 17.4 Ressources

Le worker possède ses propres limites :

- CPU ;
- mémoire ;
- concurrence ;
- files temporaires.

---

# 18. Modes d’exécution

L’exécutable peut supporter des modes tels que :

```text
web

worker

migration

maintenance
```

---

## 18.1 Séparation

Le mode `web` ne lance pas automatiquement tous les jobs durables en production.

---

## 18.2 Développement

Un mode combiné peut exister localement pour simplifier le développement.

Il ne doit pas modifier les garanties fonctionnelles.

---

# 19. Schéma PostgreSQL

Les tables de plateforme appartiennent à un schéma logique dédié.

Exemple conceptuel :

```text
sitefactory_jobs.jobs

sitefactory_jobs.job_attempts

sitefactory_jobs.outbox_messages

sitefactory_jobs.schedules
```

Le nom exact sera défini dans les migrations.

---

## 19.1 Propriété

Ces tables appartiennent à la plateforme de jobs, et non à un module métier particulier.

---

## 19.2 Accès

Les modules passent par des contrats publics.

Ils ne modifient pas directement ces tables.

---

# 20. Outbox transactionnelle

Lorsqu’un effet découle d’une modification métier, un message outbox est créé dans la même transaction PostgreSQL.

Exemple :

```text
Créer le lead
+ créer LeadCreatedIntegrationEvent
+ commit
```

---

## 20.1 Garantie

Les états possibles sont :

```text
transaction échouée
→ ni lead ni message

transaction réussie
→ lead et message présents
```

---

## 20.2 Appel externe interdit dans la transaction

La transaction ne doit pas :

- envoyer l’email ;
- appeler le CRM ;
- créer une variante distante ;
- contacter un provider.

---

## 20.3 Contrat

L’écriture passe par un contrat tel que :

```text
IOutboxWriter
```

---

# 21. Dispatch de l’outbox

Le worker revendique les messages outbox disponibles.

Pour chaque message :

1. identifier les handlers déclarés ;
2. créer un job par handler ;
3. utiliser une clé d’idempotence déterministe ;
4. marquer le message comme dispatché ;
5. valider l’ensemble dans une transaction courte.

---

## 21.1 Clé de dispatch

Exemple conceptuel :

```text
{OutboxMessageId}:{HandlerId}
```

---

## 21.2 Reprise

Si le worker s’arrête avant le commit, le message pourra être repris.

---

## 21.3 Handler absent

Un message sans handler compatible devient une erreur bloquante d’exploitation.

Il ne doit pas être marqué comme correctement traité.

---

# 22. Jobs directs

Un job peut également être créé directement lorsqu’il n’est pas couplé à une transaction métier.

Exemples :

- réconciliation ;
- nettoyage ;
- tâche opérateur ;
- traitement d’un fichier déjà persistant.

---

## 22.1 Persistance avant réponse

Une commande annonçant qu’un traitement est planifié ne doit réussir qu’après la persistance durable du job.

---

## 22.2 Transaction métier

Lorsqu’une transaction métier existe, l’outbox reste préférée.

---

# 23. Modèle de job

Un job doit pouvoir conserver :

```text
JobId

JobType

PayloadSchemaVersion

Payload

Queue

Priority

Status

CreatedAtUtc

AvailableAtUtc

StartedAtUtc

CompletedAtUtc

AttemptCount

MaximumAttempts

Timeout

LockedBy

LockedUntilUtc

ExecutionToken

IdempotencyKey

CorrelationId

TraceParent

LastErrorCode
```

---

## 23.1 Champs exacts

La structure physique exacte pourra évoluer.

Les contrats fonctionnels de cette section restent obligatoires.

---

# 24. Identifiant du job

Un job utilise un identifiant opaque :

```text
JobId
```

Un UUID ou ULID peut être utilisé.

---

## 24.1 Sécurité

L’identifiant non séquentiel ne remplace pas l’autorisation des endpoints de statut.

---

# 25. Job Type

Un job possède un identifiant stable et versionné.

Convention recommandée :

```text
{module-id}.{operation}.v{schema-version}
```

Exemples :

```text
sample-records.send-admin-notification.v1

sample-media.generate-variants.v1

exports.generate-leads-export.v1
```

---

## 25.1 Nom de type CLR

Sont interdits comme contrat persistant :

```text
Namespace.ClassName, Assembly
```

---

## 25.2 Raisons

Un type CLR peut changer après :

- refactoring ;
- renommage ;
- modification d’assembly ;
- obfuscation ;
- upgrade.

---

## 25.3 Handler allowlisté

Chaque Job Type est associé explicitement à un handler enregistré.

---

# 26. Payloads versionnés

Le payload possède :

```text
PayloadSchemaVersion
```

---

## 26.1 Validation

Avant exécution, il est validé contre :

- son schéma ;
- ses limites ;
- les règles métier nécessaires.

---

## 26.2 Évolution compatible

Un nouveau champ facultatif peut conserver la version lorsque le contrat reste compatible.

---

## 26.3 Évolution incompatible

Une évolution incompatible produit un nouveau Job Type ou une nouvelle version de payload.

---

## 26.4 Anciennes versions

Une release doit conserver les handlers nécessaires aux jobs encore en attente ou prévoir leur migration.

---

# 27. Contenu des payloads

Le payload doit rester minimal.

La préférence est :

```text
identifiants
+ paramètres strictement nécessaires
```

plutôt que :

```text
agrégat complet
+ contenu libre
+ secret
```

---

## 27.1 Gros fichiers

Un média, export ou document n’est pas placé en Base64 dans le payload.

Le job conserve un identifiant de stockage.

---

## 27.2 Données personnelles

Une donnée personnelle n’est incluse que si l’effet ne peut pas être reproduit correctement à partir d’une référence et si sa conservation est justifiée.

---

## 27.3 Snapshot

Un snapshot peut être nécessaire lorsque l’effet doit refléter l’état exact au moment de la demande.

Exemple :

- contenu d’un email contractuel ;
- paramètres d’un export ;
- document à générer.

Le snapshot doit être :

- minimal ;
- sécurisé ;
- soumis à une rétention ;
- exclu des logs.

---

# 28. Secrets dans les jobs

Un payload ne contient jamais :

- mot de passe ;
- token ;
- clé API ;
- chaîne de connexion ;
- URL signée ;
- clé privée ;
- cookie ;
- credential SMTP.

---

## 28.1 Résolution

Le handler résout les credentials au runtime via l’ADR-0012.

---

## 28.2 Secret expiré

Un credential expiré produit un échec classifié.

Il ne doit pas être recopié dans le job.

---

# 29. Taille des payloads

La taille maximale d’un payload est bornée.

---

## 29.1 Objectif

Empêcher :

- croissance excessive de PostgreSQL ;
- sérialisation coûteuse ;
- données sensibles massives ;
- transactions trop lourdes.

---

## 29.2 Dépassement

Un payload trop volumineux est refusé avant l’enqueue.

---

# 30. États d’un job

Valeurs recommandées :

```text
queued

scheduled

running

retry-wait

succeeded

cancelled

dead-lettered
```

---

## 30.1 `queued`

Le job peut être revendiqué immédiatement.

---

## 30.2 `scheduled`

Le job attend son `AvailableAtUtc`.

---

## 30.3 `running`

Un worker possède actuellement un lease.

---

## 30.4 `retry-wait`

Le job attend une prochaine tentative.

---

## 30.5 `succeeded`

Le handler a terminé avec succès.

---

## 30.6 `dead-lettered`

Le job ne sera plus exécuté automatiquement.

---

# 31. Revendication des jobs

Le worker revendique les jobs disponibles dans une transaction PostgreSQL courte.

La baseline utilise une approche compatible avec :

```text
SELECT ... FOR UPDATE SKIP LOCKED
```

ou une primitive PostgreSQL équivalente validée.

---

## 31.1 Transaction courte

La transaction sert uniquement à :

- sélectionner ;
- changer le statut ;
- attribuer le lease ;
- incrémenter l’essai ;
- commit.

Elle est terminée avant l’exécution du handler.

---

## 31.2 Plusieurs workers

Deux workers ne doivent pas revendiquer volontairement le même job au même instant.

---

## 31.3 Limite

Un crash ou une expiration de lease peut néanmoins provoquer une nouvelle exécution.

L’idempotence reste donc obligatoire.

---

# 32. Leases

Un job en cours possède :

```text
LockedBy

LockedUntilUtc

ExecutionToken
```

---

## 32.1 Expiration

Si le worker disparaît, le job redevient éligible après expiration du lease.

---

## 32.2 Renouvellement

Un job long peut renouveler périodiquement son lease.

---

## 32.3 Horloge

Les comparaisons critiques privilégient le temps PostgreSQL afin de réduire les effets de décalage entre machines.

---

## 32.4 Validation finale

Un worker ne doit marquer le job comme réussi que s’il possède encore l’Execution Token attendu.

---

# 33. Heartbeat

Les jobs longs peuvent produire un heartbeat.

---

## 33.1 Usage

Le heartbeat :

- renouvelle le lease ;
- signale la progression technique ;
- permet de détecter un worker bloqué.

---

## 33.2 Fréquence

La fréquence reste bornée afin de ne pas surcharger PostgreSQL.

---

## 33.3 Effet externe

Le heartbeat ne garantit pas que l’effet externe n’a pas déjà eu lieu.

---

# 34. Idempotence

Chaque handler doit documenter sa stratégie d’idempotence.

---

## 34.1 Stratégies possibles

- contrainte unique PostgreSQL ;
- enregistrement de delivery ;
- clé d’idempotence fournisseur ;
- création conditionnelle ;
- hash déterministe ;
- upsert contrôlé ;
- vérification d’état ;
- clé logique par événement et destinataire.

---

## 34.2 Clé

Un job possède une `IdempotencyKey` stable.

---

## 34.3 Déduplication d’enqueue

Une contrainte unique peut empêcher la création de plusieurs jobs identiques pour le même effet logique.

---

## 34.4 Déduplication d’exécution

Le handler vérifie également si l’effet a déjà été réalisé.

---

# 35. Exactly-once

SiteFactory ne promet pas une livraison exactement une fois.

---

## 35.1 Exemple SMTP

Un scénario possible est :

```text
SMTP accepte l’email
→ connexion interrompue
→ worker ne reçoit pas la confirmation
→ retry
→ second email possible
```

---

## 35.2 Mesures

- identifiant logique ;
- registre local des deliveries ;
- provider avec idempotency key lorsque disponible ;
- contenu tolérant la duplication ;
- réconciliation.

---

## 35.3 Communication

Les modules ne doivent pas documenter une garantie exactly-once qu’ils ne peuvent pas fournir.

---

# 36. Retries

Les retries sont réservés aux erreurs potentiellement transitoires.

---

## 36.1 Catégories d’erreurs

Valeurs conceptuelles :

```text
transient

rate-limited

permanent

validation

unauthorized

cancelled

unknown
```

---

## 36.2 Erreurs retryables

Exemples :

- timeout ;
- connexion interrompue ;
- indisponibilité temporaire ;
- HTTP 429 ;
- erreur serveur fournisseur ;
- verrou temporaire.

---

## 36.3 Erreurs non retryables

Exemples :

- payload invalide ;
- Job Type inconnu ;
- permission absente ;
- entitlement absent ;
- fichier introuvable définitivement ;
- adresse rejetée définitivement ;
- format non supporté.

---

# 37. Politique de retry

La baseline utilise :

```text
exponential backoff
+ jitter
+ délai maximal
+ nombre maximal de tentatives
```

---

## 37.1 Configuration par type

Chaque Job Type peut définir :

- MaximumAttempts ;
- délai initial ;
- délai maximal ;
- timeout ;
- classification d’erreurs.

---

## 37.2 `Retry-After`

Un fournisseur indiquant `Retry-After` doit être respecté dans les limites opérationnelles.

---

## 37.3 Retry infini

Un retry sans limite est interdit.

---

## 37.4 Tentative manuelle

Un opérateur peut relancer un dead-letter après correction de la cause.

---

# 38. Timeouts

Chaque handler possède un timeout borné.

---

## 38.1 Propagation

Le `CancellationToken` est propagé :

- aux appels HTTP ;
- à PostgreSQL ;
- au storage ;
- aux transformations ;
- aux providers.

---

## 38.2 Timeout externe

Le timeout du provider doit être inférieur au lease ou compatible avec son renouvellement.

---

## 38.3 Blocage

Un handler ne doit pas attendre indéfiniment :

- un réseau ;
- un fichier ;
- un verrou ;
- un processus externe.

---

# 39. Dead-letter

Un job ayant épuisé ses tentatives devient :

```text
dead-lettered
```

---

## 39.1 Visibilité

Un dead-letter doit être :

- observable ;
- requêtable ;
- associé à un Error Code ;
- lié à son Correlation ID ;
- inclus dans les alertes adaptées.

---

## 39.2 Suppression silencieuse

Un dead-letter n’est pas supprimé immédiatement.

---

## 39.3 Inspection

L’opérateur peut inspecter :

- Job Type ;
- état ;
- nombre d’essais ;
- erreur neutralisée ;
- dates ;
- identifiants techniques.

Le payload sensible n’est pas exposé sans permission et besoin.

---

# 40. Relance manuelle

Une relance manuelle doit être explicite.

Commande conceptuelle :

```text
sitefactory jobs retry \
  --job-id <id> \
  --confirm
```

---

## 40.1 Préconditions

- cause corrigée ;
- handler toujours supporté ;
- idempotence vérifiée ;
- payload valide ;
- environnement confirmé.

---

## 40.2 Modification du payload

L’opérateur ne modifie pas directement le JSON d’un job échoué.

Une correction de données utilise une opération métier ou une migration dédiée.

---

## 40.3 Relance massive

Une commande globale de type :

```text
retry-all
```

est interdite sans filtres, plan et confirmation renforcée.

---

# 41. Annulation

Un job en attente peut être annulé.

---

## 41.1 Job en cours

L’annulation d’un job en cours est coopérative.

---

## 41.2 Effet déjà réalisé

Annuler un job ne garantit pas que l’effet externe n’a pas déjà eu lieu.

---

## 41.3 Audit

Une annulation opérateur est auditée.

---

# 42. Priorités

Les jobs peuvent utiliser une priorité bornée.

Exemple :

```text
critical

default

bulk
```

---

## 42.1 Critical

Réservée à un petit nombre d’effets réellement prioritaires.

---

## 42.2 Bulk

Utilisée pour :

- exports volumineux ;
- régénération de variantes ;
- maintenance ;
- réconciliation.

---

## 42.3 Famine

Le dispatcher doit éviter qu’une queue basse priorité soit bloquée indéfiniment.

---

# 43. Queues logiques

Les jobs peuvent être classés par queue.

Exemples :

```text
notifications

media

integrations

exports

maintenance
```

---

## 43.1 Objectif

Permettre :

- concurrence distincte ;
- ressources distinctes ;
- diagnostics ;
- isolation d’un fournisseur défaillant.

---

## 43.2 Queue inconnue

Une queue inconnue est refusée ou laissée sans consommateur avec une alerte explicite.

---

# 44. Concurrence

La concurrence est bornée :

- globalement ;
- par queue ;
- par Job Type ;
- éventuellement par fournisseur.

---

## 44.1 Transformation média

Le nombre de transformations simultanées doit tenir compte :

- CPU ;
- mémoire ;
- stockage temporaire.

---

## 44.2 Provider externe

La concurrence respecte :

- quotas ;
- rate limits ;
- capacité du fournisseur.

---

## 44.3 Valeur illimitée

Une concurrence non bornée est interdite.

---

# 45. Verrouillage métier

Certains jobs ne doivent pas s’exécuter simultanément pour la même ressource.

Exemples :

```text
deux exports identiques

deux transformations du même média

deux synchronisations du même lead
```

---

## 45.1 Clé de concurrence

Un handler peut déclarer une clé telle que :

```text
{JobType}:{ResourceId}
```

---

## 45.2 Implémentation

La plateforme peut utiliser :

- une contrainte ;
- un lease ;
- une table de verrouillage ;
- une stratégie idempotente.

---

## 45.3 Transaction longue interdite

Le système ne conserve pas un verrou de ligne métier pendant toute la durée d’un appel externe.

---

# 46. Backpressure

La plateforme doit pouvoir détecter :

- backlog ;
- âge du plus ancien job ;
- saturation d’une queue ;
- workers insuffisants ;
- stockage en croissance.

---

## 46.1 Enqueue

Une transaction métier critique ne doit pas être rejetée uniquement parce qu’un fournisseur est momentanément indisponible, tant que l’outbox reste persistable.

---

## 46.2 Saturation PostgreSQL

Si la persistance du job ou de l’outbox échoue, l’opération doit échouer ou suivre son contrat explicite.

---

## 46.3 Bulk

Les opérations bulk peuvent être refusées ou différées lorsque les quotas sont atteints.

---

# 47. Tâches récurrentes

Une tâche récurrente possède :

```text
ScheduleId

JobType

ScheduleDefinition

TimeZone

OverlapPolicy

MissedRunPolicy

Enabled
```

---

## 47.1 Déclaration

Les schedules standards sont déclarés par :

- la plateforme ;
- les modules ;
- les customizations autorisées.

---

## 47.2 Configuration arbitraire

Une expression de schedule fournie par un utilisateur non fiable n’est pas exécutée sans validation.

---

## 47.3 UTC

Les schedules techniques utilisent UTC par défaut.

---

# 48. Politique de chevauchement

Valeurs recommandées :

```text
forbid

allow

replace
```

---

## 48.1 Baseline

Les tâches de maintenance et réconciliation utilisent généralement :

```text
forbid
```

---

## 48.2 Job précédent actif

Une occurrence peut être :

- ignorée ;
- reportée ;
- enregistrée comme missed.

La politique doit être explicite.

---

# 49. Occurrences manquées

Une tâche récurrente doit définir :

```text
skip

run-once

bounded-catch-up
```

---

## 49.1 Tempête de rattrapage

Le système ne doit pas exécuter automatiquement des centaines d’occurrences après une longue interruption.

---

## 49.2 Restauration

Après une restauration, la politique de missed runs doit être vérifiée avant de réactiver le scheduler.

---

# 50. Jobs longs

Un job long doit envisager :

- heartbeat ;
- checkpoint ;
- progression ;
- cancellation ;
- reprise ;
- stockage temporaire ;
- nettoyage.

---

## 50.1 Transactions

Il ne conserve pas une transaction PostgreSQL ouverte pendant toute sa durée.

---

## 50.2 Progression

La progression ne doit pas produire une écriture en base à chaque élément traité.

---

## 50.3 Reprise

Un job peut conserver un checkpoint borné permettant une reprise idempotente.

---

# 51. Effets HTTP externes

Les handlers HTTP doivent appliquer :

- timeout ;
- retry adapté ;
- cancellation ;
- idempotency key lorsque disponible ;
- circuit breaker éventuel ;
- kill switch ;
- validation des URLs ;
- logs minimisés.

---

## 51.1 Réponses 4xx

Une réponse 4xx n’est pas retryable par défaut.

Exceptions possibles :

- 408 ;
- 409 selon contrat ;
- 425 ;
- 429.

---

## 51.2 Réponses 5xx

Une réponse 5xx peut être transitoire, avec retry borné.

---

## 51.3 Corps

Le corps complet de la réponse fournisseur ne doit pas être journalisé automatiquement.

---

# 52. Circuit breaker

Un provider fréquemment défaillant peut utiliser un circuit breaker.

---

## 52.1 Objectif

Éviter :

- surcharge ;
- retries inutiles ;
- saturation ;
- coût ;
- blocage d’autres jobs.

---

## 52.2 Persistance

L’état du circuit breaker peut initialement être local au worker.

Il ne constitue pas un remplacement du kill switch opérationnel.

---

# 53. Kill switches

Conformément à l’ADR-0006, un kill switch peut désactiver :

- une queue ;
- un Job Type ;
- une intégration ;
- un fournisseur ;
- une tâche récurrente.

---

## 53.1 Job déjà en cours

Le handler doit vérifier le kill switch avant un effet critique lorsque le contrat le prévoit.

---

## 53.2 Jobs en attente

Ils restent :

- en attente ;
- annulés ;
- ou dead-lettered ;

selon la politique documentée.

---

## 53.3 Aucun enable implicite

Un kill switch ne peut pas activer une capability ou un entitlement.

---

# 54. Feature flags

Un feature flag peut contrôler l’activation technique d’un Job Type déjà installé et entitled.

---

## 54.1 Job existant

La politique doit préciser le traitement des jobs créés avant la désactivation du flag.

---

## 54.2 Baseline

La création de nouveaux jobs est refusée lorsque la feature est désactivée.

Les jobs existants sont examinés selon leur contrat.

---

# 55. Autorisation de l’enqueue

La création d’un job issu d’une action utilisateur reste soumise à :

- authentification ;
- permission ;
- entitlement ;
- feature flag ;
- kill switch ;
- validation métier.

---

## 55.1 Worker

Le worker ne réévalue pas nécessairement le rôle actuel de l’utilisateur pour une action déjà autorisée et persistée.

Le contrat doit distinguer :

- autorisation au moment de la demande ;
- état courant de la ressource ;
- autorisation nécessaire au moment de l’exécution.

---

## 55.2 Action sensible

Une opération sensible de longue durée peut nécessiter une nouvelle validation avant l’effet final.

---

# 56. Emails

Les emails sont envoyés par des jobs persistants.

---

## 56.1 Cas d’usage

Exemples :

- notification de nouveau lead ;
- confirmation de réception ;
- invitation administrateur ;
- alerte opérationnelle ;
- notification d’export.

---

## 56.2 Idempotency key

Une clé logique peut utiliser :

```text
{NotificationType}:{BusinessResourceId}:{RecipientId}:{TemplateVersion}
```

---

## 56.3 Payload

Le payload ne contient pas le mot de passe SMTP.

---

## 56.4 Adresse

Lorsque l’adresse doit être figée au moment de la demande, elle peut appartenir à un snapshot sécurisé et à rétention courte.

---

## 56.5 Doublons

Le système réduit les doublons sans promettre leur impossibilité absolue.

---

# 57. Templates d’emails

Les templates doivent être :

- versionnés ;
- validés ;
- indépendants des secrets ;
- compatibles avec les données du payload.

---

## 57.1 Template retiré

Un job en attente utilisant une ancienne version doit rester exécutable ou être migré.

---

## 57.2 Contenu dynamique

Les données utilisateur sont encodées selon le format de sortie.

---

# 58. Transformations de médias

Les variantes sont générées en arrière-plan.

---

## 58.1 Clé d’idempotence

Exemple :

```text
{MediaId}:{MediaHash}:{ProfileId}:{ProfileVersion}
```

---

## 58.2 Original

Un échec ne supprime pas l’original validé.

---

## 58.3 Variante existante

Le handler vérifie si une variante conforme existe déjà.

---

## 58.4 Ressources

La queue média possède une concurrence limitée.

---

# 59. Exports

Un export volumineux est produit par un job.

---

## 59.1 Réponse HTTP

La demande peut retourner :

```text
202 Accepted
+ OperationId
```

---

## 59.2 Statut

L’utilisateur autorisé peut consulter :

- queued ;
- running ;
- ready ;
- failed ;
- expired.

---

## 59.3 Stockage

Le fichier utilise une zone privée avec :

- TTL ;
- autorisation ;
- audit ;
- quota.

---

## 59.4 Snapshot

Lorsque l’export doit refléter l’état à la date de demande, le job conserve :

- un watermark ;
- une version ;
- ou une définition immuable de sélection.

---

# 60. Intégrations CRM ou ERP

Une synchronisation externe utilise :

- Job Type dédié ;
- idempotency key ;
- mapping versionné ;
- timeout ;
- retry ;
- kill switch ;
- audit ;
- réconciliation.

---

## 60.1 Provider ID

Le provider est un label borné.

---

## 60.2 Réponse externe

Les identifiants retournés peuvent être stockés.

Les payloads complets ne doivent pas être conservés sans besoin.

---

## 60.3 Rejet métier

Un rejet permanent est dead-lettered avec un code métier exploitable.

---

# 61. Suppression différée

Les suppressions de médias ou artefacts utilisent des jobs idempotents.

---

## 61.1 Préconditions

Le handler revérifie :

- délai de grâce ;
- références ;
- statut ;
- environnement ;
- lock éventuel.

---

## 61.2 Rejeu

Rejouer une suppression déjà terminée doit produire un succès idempotent ou un résultat contrôlé.

---

# 62. Réconciliation

Les jobs de réconciliation sont en lecture seule par défaut.

---

## 62.1 Correction

Une correction destructive nécessite :

- plan ;
- confirmation ;
- Job Type distinct ;
- rapport.

---

## 62.2 Volume

Les réconciliations volumineuses utilisent :

- pagination ;
- checkpoints ;
- concurrence limitée ;
- métriques.

---

# 63. Gestion des erreurs

Les handlers ne doivent pas utiliser les exceptions comme unique contrat de retry.

---

## 63.1 Résultat structuré

Une erreur doit pouvoir être classifiée :

```text
retryable

permanent

cancelled

blocked
```

---

## 63.2 Exception inattendue

Une exception inconnue peut être traitée comme transitoire pour un faible nombre de tentatives, puis dead-lettered.

---

## 63.3 Données sensibles

L’erreur enregistrée ne contient pas :

- secret ;
- payload complet ;
- PII inutile ;
- réponse fournisseur complète.

---

# 64. Tentatives

Chaque tentative peut conserver :

```text
AttemptNumber

WorkerId

StartedAtUtc

CompletedAtUtc

Result

ErrorCode

Duration
```

---

## 64.1 Stack trace

Une stack trace peut être conservée dans les logs sécurisés selon la politique de rétention.

Elle n’est pas nécessairement stockée intégralement dans la table.

---

## 64.2 Volume

L’historique des tentatives reste borné.

---

# 65. Nettoyage des jobs

Les jobs terminés ne sont pas conservés indéfiniment.

---

## 65.1 Politiques distinctes

La rétention distingue :

- succès ;
- annulations ;
- dead letters ;
- outbox dispatchée ;
- tentatives ;
- payloads sensibles.

---

## 65.2 Payloads

Les payloads doivent être supprimés ou minimisés dès qu’ils ne sont plus nécessaires.

---

## 65.3 Audit

L’audit d’une action sensible peut être conservé séparément du job.

---

## 65.4 Nettoyage

Le nettoyage est lui-même :

- borné ;
- observable ;
- idempotent ;
- sans suppression des jobs actifs.

---

# 66. Pause et reprise du worker

Les opérations doivent permettre de suspendre :

- l’ensemble du worker ;
- une queue ;
- un Job Type ;
- les tâches récurrentes.

---

## 66.1 Déploiement

Une pause peut être utilisée avant :

- migration ;
- restauration ;
- upgrade incompatible ;
- intervention opérateur.

---

## 66.2 Persistance

La pause ne supprime aucun job.

---

## 66.3 Reprise

La reprise doit vérifier la compatibilité des jobs en attente.

---

# 67. Arrêt gracieux

Lors de l’arrêt :

1. arrêter de revendiquer de nouveaux jobs ;
2. signaler l’annulation aux handlers ;
3. attendre une période bornée ;
4. ne pas marquer un job interrompu comme réussi ;
5. laisser expirer le lease si nécessaire.

---

## 67.1 Timeout d’arrêt

Le conteneur ne doit pas attendre indéfiniment.

---

## 67.2 Déploiement

Le temps de grâce Docker doit être cohérent avec les handlers ordinaires.

---

# 68. Health checks du worker

Le worker expose ou produit au minimum :

```text
liveness

readiness
```

---

## 68.1 Liveness

Le processus worker fonctionne.

---

## 68.2 Readiness

Le worker peut :

- joindre PostgreSQL ;
- lire les contrats ;
- accéder aux ressources essentielles ;
- exécuter les Job Types attendus.

---

## 68.3 Fournisseur externe

Un fournisseur externe indisponible ne doit pas nécessairement rendre tout le worker non ready.

La queue concernée utilise retries, circuit breaker ou kill switch.

---

# 69. Observabilité

Événements recommandés :

```text
OutboxMessageCreated

OutboxDispatchStarted

OutboxDispatched

OutboxDispatchFailed

BackgroundJobEnqueued

BackgroundJobClaimed

BackgroundJobStarted

BackgroundJobSucceeded

BackgroundJobRetryScheduled

BackgroundJobDeadLettered

BackgroundJobCancelled

BackgroundJobLeaseExpired

RecurringJobScheduled

RecurringJobOccurrenceSkipped
```

---

## 69.1 Métriques

Exemples :

```text
job queue depth

oldest queued job age

outbox lag

job duration

job success count

job retry count

dead-letter count

lease expiration count

worker active jobs

schedule delay
```

---

## 69.2 Labels autorisés

```text
job_type

queue

result

error_category

provider

environment
```

---

## 69.3 Labels interdits

```text
JobId

MediaId

LeadId

UserId

Email

CorrelationId

TraceId

OriginalFileName
```

---

# 70. Traces

Un job crée une nouvelle activité liée à l’opération d’origine.

---

## 70.1 Propagation

Le job peut conserver :

- `TraceParent` ;
- `CorrelationId`.

---

## 70.2 Nouvelle trace ou lien

Un job exécuté longtemps après la requête peut utiliser :

- une nouvelle trace ;
- un `ActivityLink` vers la trace d’origine.

---

## 70.3 Payload

Le payload complet n’est pas ajouté au span.

---

# 71. Alertes

Alertes possibles :

```text
Worker non disponible

Outbox lag trop élevé

Plus ancien job trop ancien

Dead letters présents

Lease expirant fréquemment

Queue média saturée

Provider externe en échec durable

Job récurrent non exécuté

Croissance anormale des tables

Nettoyage en échec
```

---

## 71.1 Action

Chaque alerte importante doit référencer un runbook.

---

# 72. Interface d’administration

Une future vue d’administration peut afficher :

- état des queues ;
- jobs échoués ;
- âge ;
- type ;
- tentative ;
- Error Code.

---

## 72.1 Accès

Cette vue nécessite une permission opérationnelle dédiée.

---

## 72.2 Payload

Le payload complet n’est pas affiché par défaut.

---

## 72.3 Action

Les actions de retry, annulation et purge exigent :

- permission ;
- confirmation ;
- audit.

---

# 73. Commandes opérateur

Commandes conceptuelles :

```text
sitefactory jobs status

sitefactory jobs list

sitefactory jobs inspect

sitefactory jobs retry

sitefactory jobs cancel

sitefactory jobs pause

sitefactory jobs resume

sitefactory jobs reconcile
```

---

## 73.1 Production

Les opérations de production doivent identifier explicitement :

- client ;
- environnement ;
- Job ID ou filtre ;
- action ;
- confirmation.

---

## 73.2 Payload

Aucune commande ne reçoit un secret dans ses arguments.

---

# 74. Upgrades

Une mise à niveau doit analyser les jobs en attente.

---

## 74.1 Vérifications

```text
Job Types en attente

Versions de payload

Handlers disponibles dans la cible

Schedules

Jobs running

Dead letters

Outbox non dispatchée
```

---

## 74.2 Handler retiré

Un handler ne peut pas être retiré tant que des jobs compatibles restent en attente, sauf plan de :

- migration ;
- annulation ;
- drainage ;
- dead-letter contrôlé.

---

## 74.3 Module retiré

Le retrait d’un module est bloqué s’il possède :

- jobs actifs ;
- outbox non dispatchée ;
- schedules actifs ;
- handlers requis.

---

# 75. Déploiement compatible

Lorsqu’une nouvelle release reste compatible avec les payloads existants :

```text
arrêt gracieux du worker
→ déploiement
→ migration éventuelle explicite
→ démarrage du worker
→ vérification
```

---

## 75.1 Compatibilité simultanée

Si l’ancien et le nouveau worker peuvent coexister, les contrats doivent être compatibles avec les deux versions.

---

## 75.2 Docker Compose

La baseline peut utiliser une courte interruption contrôlée du worker pendant l’upgrade.

---

# 76. Upgrade incompatible

Un upgrade incompatible nécessite :

1. suspendre les nouveaux jobs concernés ;
2. drainer ou migrer les jobs existants ;
3. arrêter le worker ;
4. appliquer la migration explicite ;
5. déployer les nouveaux handlers ;
6. vérifier ;
7. reprendre les queues.

---

## 76.1 Effet de bord

Aucun job n’est supprimé pour simplifier l’upgrade sans rapport et décision.

---

# 77. Migrations PostgreSQL

Les tables de jobs évoluent par migrations explicites.

---

## 77.1 Worker arrêté

Une migration incompatible avec le worker courant nécessite son arrêt.

---

## 77.2 Auto-migration

Le worker ne migre pas automatiquement la production au démarrage.

---

## 77.3 Historique

Une migration publiée ne doit pas être modifiée.

---

# 78. Sauvegarde

Les tables de jobs et outbox font partie du backup PostgreSQL.

---

## 78.1 Effets externes

Le backup ne capture pas l’état exact d’un fournisseur externe.

---

## 78.2 Limite

Un job marqué `running` au moment d’un backup peut être restauré plus tard et repris.

---

# 79. Restauration

Après restauration d’un backup, le worker reste désactivé jusqu’à inspection.

---

## 79.1 Risque de rejeu

Un effet externe peut avoir été réalisé après la date du backup, alors que le job restauré apparaît encore en attente.

Exemple :

```text
backup à 10 h
email envoyé à 10 h 05
incident à 10 h 10
restauration du backup de 10 h
job email à nouveau en attente
```

---

## 79.2 Procédure

```text
restaurer PostgreSQL
→ ne pas démarrer le worker
→ inventorier outbox et jobs
→ réconcilier les effets externes
→ vérifier les idempotency keys
→ décider les reprises
→ activer les queues progressivement
```

---

## 79.3 Schedules

Le rattrapage des tâches récurrentes doit être contrôlé afin d’éviter une tempête.

---

# 80. Recovery epoch

Une restauration peut enregistrer un identifiant ou epoch de reprise.

---

## 80.1 Objectif

Permettre :

- l’audit ;
- la corrélation ;
- la détection de jobs restaurés ;
- une politique spéciale de reprise.

---

## 80.2 Idempotence

Le Recovery Epoch ne remplace pas les clés d’idempotence métier.

---

# 81. Isolation client

Chaque projet client possède :

- sa base ;
- son outbox ;
- ses jobs ;
- son worker ;
- ses schedules ;
- ses credentials.

---

## 81.1 VPS mutualisé

Les workers de plusieurs clients peuvent partager un VPS, mais pas leur base ni leur queue.

---

## 81.2 Erreur de cible

Un worker doit vérifier qu’il utilise la base et l’environnement attendus.

---

# 82. Isolation des environnements

Staging et production ne partagent pas :

- jobs ;
- outbox ;
- worker identity ;
- schedules ;
- credentials ;
- external provider environments.

---

## 82.1 SMTP staging

Staging ne doit pas envoyer involontairement des notifications réelles de production.

---

# 83. Sécurité des handlers

Les Job Types sont enregistrés explicitement.

---

## 83.1 Réflexion arbitraire

Le worker ne doit pas charger un type indiqué librement dans le payload.

---

## 83.2 Désérialisation

La désérialisation utilise un type connu et un schéma connu.

---

## 83.3 Exécution de scripts

Un job ne peut pas contenir :

- commande shell ;
- script ;
- SQL arbitraire ;
- assembly à charger ;
- URL exécutable arbitraire.

---

## 83.4 Endpoint générique

L’application ne doit pas exposer un endpoint permettant d’enqueue n’importe quel Job Type avec un JSON libre.

---

# 84. Sécurité des intégrations

Les handlers externes doivent prévenir :

- SSRF ;
- exfiltration ;
- URL arbitraire ;
- redirection non maîtrisée ;
- fuite de headers ;
- payload excessif.

---

## 84.1 Endpoint

Les endpoints viennent d’une configuration approuvée.

Ils ne sont pas fournis librement dans le payload.

---

## 84.2 Credentials

Les credentials sont résolus séparément.

---

# 85. Données personnelles

Les tables de jobs ne doivent pas devenir un stockage secondaire durable de données personnelles.

---

## 85.1 Préférence

Utiliser :

- identifiants ;
- snapshots minimaux ;
- rétention courte ;
- tables spécialisées lorsque nécessaire.

---

## 85.2 Suppression

Une demande de suppression peut nécessiter :

- suppression ou neutralisation de payloads ;
- annulation de jobs ;
- traitement des dead letters ;
- respect des obligations d’audit.

---

# 86. Chiffrement

Les payloads généraux ne sont pas chiffrés individuellement dans la baseline.

---

## 86.1 Compensation

- base non publique ;
- volume protégé ;
- accès limité ;
- backups chiffrés ;
- payloads minimisés ;
- absence de secrets ;
- rétention.

---

## 86.2 Donnée hautement sensible

Une donnée hautement sensible doit utiliser :

- une table spécialisée ;
- un stockage chiffré ;
- ou un identifiant vers une source sécurisée.

Elle ne doit pas être placée librement dans le job JSON.

---

# 87. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-JOB-001` | Job Type inconnu |
| `SF-JOB-002` | Version de payload inconnue |
| `SF-JOB-003` | Payload invalide |
| `SF-JOB-004` | Payload trop volumineux |
| `SF-JOB-005` | Secret détecté dans le payload |
| `SF-JOB-006` | Donnée interdite dans le payload |
| `SF-JOB-007` | Handler absent |
| `SF-JOB-008` | Handler incompatible |
| `SF-JOB-009` | Stratégie d’idempotence absente |
| `SF-JOB-010` | Clé d’idempotence invalide |
| `SF-JOB-011` | Job dupliqué |
| `SF-JOB-012` | Job impossible à persister |
| `SF-JOB-013` | Job impossible à revendiquer |
| `SF-JOB-014` | Lease expiré |
| `SF-JOB-015` | Execution Token invalide |
| `SF-JOB-016` | Timeout du job |
| `SF-JOB-017` | Job annulé |
| `SF-JOB-018` | Tentatives épuisées |
| `SF-JOB-019` | Job dead-lettered |
| `SF-JOB-020` | Queue inconnue |
| `SF-JOB-021` | Concurrence maximale atteinte |
| `SF-JOB-022` | Kill switch actif |
| `SF-JOB-023` | Feature désactivée |
| `SF-JOB-024` | Entitlement absent |
| `SF-JOB-025` | Provider externe indisponible |
| `SF-JOB-026` | Rate limit externe |
| `SF-JOB-027` | Réponse externe permanente |
| `SF-JOB-028` | Job incompatible après upgrade |
| `SF-JOB-029` | Worker indisponible |
| `SF-JOB-030` | Worker du mauvais environnement |
| `SF-JOB-031` | Worker du mauvais client |
| `SF-JOB-032` | Schedule invalide |
| `SF-JOB-033` | Occurrence récurrente manquée |
| `SF-JOB-034` | Chevauchement interdit |
| `SF-JOB-035` | Rejeu post-restauration non validé |
| `SF-JOB-036` | Rétention non définie |
| `SF-JOB-037` | Nettoyage échoué |
| `SF-JOB-038` | Backlog au-dessus du seuil |
| `SF-JOB-039` | Job en cours trop ancien |
| `SF-JOB-040` | Rapport d’exécution absent |
| `SF-OUTBOX-001` | Message outbox inconnu |
| `SF-OUTBOX-002` | Version de message incompatible |
| `SF-OUTBOX-003` | Dispatch impossible |
| `SF-OUTBOX-004` | Handler destinataire absent |
| `SF-OUTBOX-005` | Message déjà dispatché |
| `SF-OUTBOX-006` | Outbox lag trop élevé |
| `SF-OUTBOX-007` | Outbox restaurée nécessitant une revue |
| `SF-SCHEDULE-001` | Schedule inconnu |
| `SF-SCHEDULE-002` | Fuseau horaire invalide |
| `SF-SCHEDULE-003` | Politique de rattrapage absente |
| `SF-SCHEDULE-004` | Politique de chevauchement absente |

---

# 88. Tests unitaires

Tester notamment :

- Job Type ;
- payload schema ;
- transitions d’état ;
- idempotency key ;
- backoff ;
- jitter ;
- limites ;
- classification d’erreurs ;
- schedules ;
- politiques de chevauchement ;
- politiques d’occurrences manquées.

---

# 89. Tests PostgreSQL

Les tests d’intégration doivent utiliser un PostgreSQL réel.

Ils couvrent :

```text
enqueue

outbox transactionnelle

claim

SKIP LOCKED

lease

renouvellement

expiration

retry

dead-letter

nettoyage

concurrence multi-worker
```

---

## 89.1 Base en mémoire

Une base en mémoire ne suffit pas pour valider les primitives PostgreSQL.

---

# 90. Tests de transaction outbox

Vérifier :

```text
transaction métier commit
→ données et outbox présentes

transaction métier rollback
→ données et outbox absentes

crash avant commit
→ aucun effet

crash après commit
→ message repris par le worker
```

---

# 91. Tests d’idempotence

Chaque handler doit posséder un test prouvant qu’une double exécution ne provoque pas un double effet non maîtrisé.

---

## 91.1 Emails

Utiliser un fake provider et vérifier le registre local de delivery.

---

## 91.2 Médias

Exécuter deux fois la génération de la même variante.

---

## 91.3 Intégrations

Réutiliser la même clé d’idempotence.

---

# 92. Tests de concurrence

Lancer plusieurs workers.

Vérifier :

- un claim à la fois ;
- aucune perte ;
- reprise après lease ;
- respect des limites ;
- absence de deadlock durable ;
- traitement des jobs indépendants.

---

# 93. Tests de crash

Simuler un arrêt :

- après claim ;
- avant effet ;
- pendant effet ;
- après effet externe ;
- avant marquage success ;
- pendant renouvellement.

---

## 93.1 Attente

Certains scénarios peuvent provoquer une seconde tentative.

Le handler doit rester sûr.

---

# 94. Tests de retries

Couvrir :

```text
timeout transitoire

HTTP 429

HTTP 500

validation permanente

HTTP 400

credential invalide

Retry-After

maximum attempts

jitter borné
```

---

# 95. Tests de dead-letter

Vérifier :

- passage au statut ;
- alerte ;
- absence de retry automatique ;
- inspection ;
- retry manuel ;
- audit ;
- idempotence après retry.

---

# 96. Tests de schedules

Tester :

- occurrence normale ;
- heure UTC ;
- fuseau explicite ;
- chevauchement ;
- worker arrêté ;
- reprise ;
- bounded catch-up ;
- longue interruption ;
- changement d’heure pour les schedules locaux.

---

# 97. Tests de pause

Vérifier :

- pause globale ;
- pause de queue ;
- pause de Job Type ;
- jobs conservés ;
- reprise ;
- absence de tempête.

---

# 98. Tests de restauration

Restaurer un backup contenant :

- outbox ;
- job queued ;
- job running ;
- job succeeded avant le backup seulement ;
- schedule en retard.

Vérifier que :

- le worker ne démarre pas automatiquement ;
- l’inventaire est produit ;
- les jobs à risque sont signalés ;
- la reprise nécessite une décision.

---

# 99. Tests d’upgrade

Tester :

```text
ancienne version du payload toujours supportée

handler retiré avec jobs en attente

module retiré avec queue active

plan de drainage

migration de payload

web et worker de versions incompatibles
```

---

# 100. Tests de sécurité

Vérifier :

- secret dans payload ;
- type CLR arbitraire ;
- Job Type inconnu ;
- payload surdimensionné ;
- URL externe arbitraire ;
- script dans payload ;
- accès cross-client ;
- endpoint générique d’enqueue ;
- payload affiché sans permission ;
- commande opérateur sans confirmation.

---

# 101. Tests de performance

Mesurer :

- débit d’enqueue ;
- débit de claim ;
- latence de dispatch ;
- contention PostgreSQL ;
- taille des tables ;
- nettoyage ;
- concurrence média ;
- coût des heartbeats.

---

## 101.1 Objectif

Détecter le moment où PostgreSQL ne suffit plus avant un incident de production.

---

# 102. Tests multiplateformes

Les contrats, handlers et outils sont testés sur :

```text
Windows x64

Linux x64
```

L’exécution Docker et les scénarios de production sont validés sur Linux.

---

# 103. Quality gates

Une contribution est bloquée lorsque :

```text
Un effet durable utilise Task.Run.

Une queue critique est uniquement en mémoire.

Un appel externe est exécuté dans la transaction métier.

Un handler n’a pas de stratégie d’idempotence.

Un retry est non borné.

Un timeout est absent.

Un payload contient un secret.

Un payload contient un fichier volumineux.

Un type CLR est persisté comme Job Type.

Un Job Type n’est pas versionné.

Un handler inconnu est chargé par réflexion.

Un job peut écrire hors de son périmètre.

Une tâche récurrente n’a pas de politique de chevauchement.

Une tâche récurrente n’a pas de politique de rattrapage.

Un module est retiré avec des jobs en attente.

Un test PostgreSQL requis est absent.
```

---

# 104. Quality gates de production

La production est bloquée lorsque :

```text
Worker et web sont incompatibles.

Migration requise non appliquée.

Jobs incompatibles non drainés.

Outbox non inspectée après restauration.

Worker du mauvais environnement.

Credential fournisseur absent.

Kill switch requis non disponible.

Health check worker en échec.

Backlog critique non compris.

Rollback ou pause worker non documenté.
```

---

# 105. CI/CD

La CI doit exécuter :

- tests unitaires ;
- tests PostgreSQL ;
- tests d’idempotence ;
- tests de concurrence ;
- tests de payload ;
- tests de sécurité ;
- build de l’image worker ;
- validation Compose ;
- secret scan.

---

## 105.1 Provider réel

Les tests standards utilisent des fakes.

Les tests avec fournisseurs réels restent :

- séparés ;
- protégés ;
- bornés ;
- sans données clientes.

---

# 106. Sécurité

## 106.1 Actifs

- données métier ;
- payloads ;
- credentials ;
- fournisseurs ;
- médias ;
- exports ;
- notifications ;
- bases ;
- worker.

---

## 106.2 Menaces

- job arbitraire ;
- désérialisation dangereuse ;
- exfiltration ;
- SSRF ;
- retry amplifiant une attaque ;
- saturation ;
- job cross-client ;
- payload contenant un secret ;
- rejeu malveillant ;
- suppression non autorisée.

---

## 106.3 Contrôles

- Job Types allowlistés ;
- schémas ;
- payloads bornés ;
- credentials séparés ;
- idempotence ;
- autorisation à l’enqueue ;
- workers isolés ;
- queues bornées ;
- timeouts ;
- audit ;
- dead-letter ;
- rétention.

---

# 107. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Job perdu au redémarrage | Persistance PostgreSQL |
| Donnée métier commit sans notification | Transactional outbox |
| Email dupliqué | Idempotency key et registre de delivery |
| Job exécuté deux fois après crash | Handler idempotent |
| Job arbitraire injecté | Job Types allowlistés |
| Secret dans le payload | Validation et secret scanning |
| SSRF | Endpoint issu de configuration approuvée |
| Retry infini | MaximumAttempts |
| Worker compromis | Moindre privilège et isolation |
| Rejeu après restauration | Worker arrêté et procédure de reprise |
| Tempête de schedules | Bounded catch-up |
| Module retiré trop tôt | Analyse des jobs pendant l’upgrade |

---

# 108. Règles pour les modules

Chaque module ajoutant un job doit déclarer :

```text
JobType

PayloadSchemaVersion

Queue

Priority

Timeout

MaximumAttempts

RetryPolicy

IdempotencyStrategy

FeatureFlag éventuel

KillSwitch éventuel

DataClassification

Retention

Handler
```

---

## 108.1 Documentation

Le module documente :

- effet produit ;
- duplication possible ;
- comportement en échec ;
- réconciliation ;
- opérations manuelles.

---

## 108.2 Données

Le module ne doit pas utiliser la table de jobs comme stockage métier.

---

# 109. Règles pour les customizations

Une customization client peut déclarer ses propres jobs via les contrats publics.

---

## 109.1 Interdictions

Elle ne doit pas :

- accéder directement aux tables de jobs ;
- lancer une tâche fire-and-forget ;
- inventer un Job Type non déclaré ;
- stocker un secret dans le payload ;
- contourner les limites de concurrence ;
- enregistrer un endpoint fournisseur libre dans le job.

---

# 110. Règles pour LogicGate

LogicGate doit :

- surveiller les queues ;
- traiter les dead letters ;
- maintenir les handlers historiques nécessaires ;
- tester les restaurations ;
- définir les rétentions ;
- maintenir les runbooks ;
- vérifier les providers ;
- planifier les upgrades ;
- limiter la concurrence.

---

## 110.1 LogicGate ne doit pas

- supprimer un dead-letter sans diagnostic ;
- relancer massivement sans plan ;
- modifier un payload directement en base ;
- démarrer le worker immédiatement après une restauration ;
- promettre exactly-once ;
- utiliser PostgreSQL comme queue sans surveiller sa croissance.

---

# 111. Règles pour Codex

Codex peut :

- implémenter les contrats ;
- créer les tables et migrations ;
- créer les handlers ;
- ajouter les tests ;
- créer les fakes ;
- ajouter l’observabilité ;
- préparer les runbooks ;
- analyser un dead-letter fourni.

---

## 111.1 Codex ne doit pas

```text
Ajouter Task.Run pour un effet durable

Créer un retry infini

Supprimer un job de production

Modifier un payload réel

Relancer des dead letters en production

Démarrer un worker après restauration

Inventer un credential

Ajouter un broker externe sans décision

Retirer un handler avec des jobs en attente

Appliquer une migration production

Promettre une exécution exactly-once
```

---

## 111.2 Décision humaine

Une décision humaine est requise pour :

- relance d’un dead-letter en production ;
- purge ;
- annulation massive ;
- changement de retry ;
- changement de concurrence ;
- nouvelle intégration externe ;
- suppression d’un handler ;
- migration de payload ;
- activation après restauration ;
- introduction d’un broker.

---

# 112. Anti-patterns interdits

## 112.1 `Task.Run` dans un endpoint

```csharp
_ = Task.Run(() => SendEmailAsync(...));
```

---

## 112.2 `async void`

Un handler ou service utilise `async void`.

---

## 112.3 Queue en mémoire pour un effet critique

Un redémarrage perd les opérations.

---

## 112.4 Appel SMTP dans la transaction

La transaction PostgreSQL reste ouverte pendant l’appel.

---

## 112.5 Type CLR persisté

Le worker désérialise un type choisi dans la base.

---

## 112.6 Payload contenant une chaîne de connexion

Le credential devient persistant dans la queue.

---

## 112.7 Retry sur toute exception

Une erreur de validation est réessayée plusieurs fois.

---

## 112.8 Retry sans jitter

Plusieurs jobs réessaient simultanément après une panne fournisseur.

---

## 112.9 Transaction longue pendant le handler

Le job maintient un verrou PostgreSQL pendant plusieurs minutes.

---

## 112.10 Suppression du job en erreur

Un échec définitif disparaît sans dead-letter ni alerte.

---

## 112.11 Cron non validé

Un utilisateur peut fournir librement une expression exécutée par le scheduler.

---

## 112.12 Worker démarré après restore sans contrôle

Les effets externes sont rejoués immédiatement.

---

# 113. Conséquences positives

La décision apporte :

- effets persistants ;
- cohérence métier-outbox ;
- reprise après crash ;
- retries contrôlés ;
- dead-letter ;
- observabilité ;
- isolation des traitements longs ;
- meilleure latence HTTP ;
- workers séparés ;
- compatibilité avec les modules ;
- possibilité future de broker ;
- meilleure exploitation des intégrations externes ;
- sauvegarde commune avec PostgreSQL.

---

# 114. Conséquences négatives

La décision entraîne :

- tables supplémentaires ;
- worker à déployer ;
- migrations ;
- leases ;
- nettoyage ;
- politiques de retry ;
- tests de concurrence ;
- idempotence obligatoire ;
- risque d’orphelins opérationnels ;
- exploitation des dead letters ;
- procédure spéciale de restauration ;
- charge supplémentaire sur PostgreSQL.

---

# 115. Risques acceptés

## 115.1 Exécution multiple

Un job peut être exécuté plusieurs fois.

Ce risque est accepté et traité par l’idempotence.

---

## 115.2 Doublon externe résiduel

Certains fournisseurs ne permettent pas une garantie parfaite.

---

## 115.3 Charge PostgreSQL

La queue partage la base avec l’application.

Réduction :

- index ;
- batches ;
- rétention ;
- monitoring ;
- concurrence ;
- migration future vers un broker.

---

## 115.4 Worker indisponible

Les requêtes métier peuvent continuer à créer des messages outbox pendant une courte interruption.

Le backlog doit être surveillé.

---

# 116. Mesures compensatoires

- outbox ;
- Job Types versionnés ;
- handlers allowlistés ;
- idempotency keys ;
- leases ;
- retries bornés ;
- timeouts ;
- dead-letter ;
- worker dédié ;
- health checks ;
- métriques ;
- alertes ;
- rétention ;
- pause ;
- reprise contrôlée ;
- tests PostgreSQL ;
- runbooks.

---

# 117. Performance

## 117.1 Polling

Le worker peut utiliser un polling borné avec :

- batch ;
- délai adaptatif ;
- réveil fréquent lorsque la queue est active ;
- backoff lorsque la queue est vide.

---

## 117.2 Polling agressif

Un polling permanent sans délai est interdit.

---

## 117.3 Batch

La taille du batch doit rester configurable et bornée.

---

## 117.4 Index

Les tables doivent posséder des index adaptés à :

```text
Status

Queue

AvailableAtUtc

Priority

LockedUntilUtc
```

---

## 117.5 Tables volumineuses

Le nettoyage et l’archivage doivent éviter une croissance indéfinie.

---

# 118. Coûts

## 118.1 Coûts supplémentaires

- service worker ;
- CPU ;
- mémoire ;
- stockage PostgreSQL ;
- observabilité ;
- maintenance ;
- développement du moteur ;
- support opérationnel.

---

## 118.2 Coûts évités

- emails perdus ;
- traitements interrompus ;
- timeouts HTTP ;
- intégrations fragiles ;
- jobs manuels ;
- broker prématuré ;
- incidents difficiles à reproduire ;
- suppression silencieuse d’effets.

---

# 119. Maintenabilité

Pour chaque Job Type, il doit être possible de répondre :

```text
Qui le produit ?

Quel effet exécute-t-il ?

Quel est son schéma ?

Quelle est sa clé d’idempotence ?

Peut-il être exécuté plusieurs fois ?

Quelles erreurs sont retryables ?

Quel est son timeout ?

Combien de tentatives ?

Quel est son kill switch ?

Comment le réconcilier ?

Comment le supprimer ou le migrer pendant un upgrade ?
```

---

# 120. Compatibilité

## 120.1 Schéma des jobs

Les tables possèdent des migrations versionnées.

---

## 120.2 Payloads

Les payloads possèdent une version explicite.

---

## 120.3 Worker ancien

Un worker ancien ne doit pas revendiquer un Job Type qu’il ne comprend pas.

---

## 120.4 Worker futur

Une nouvelle version doit conserver les handlers nécessaires ou fournir un plan de migration.

---

## 120.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration d’un projet utilisant des tâches fire-and-forget nécessite un plan d’adoption.

---

# 121. Migration depuis des effets synchrones

Le processus est :

```text
identifier les effets
→ distinguer critique et non critique
→ définir les Job Types
→ définir les payloads
→ définir l’idempotence
→ ajouter l’outbox
→ créer les handlers
→ ajouter les tests
→ retirer l’appel synchrone
```

---

## 121.1 Réponse utilisateur

Le contrat HTTP peut évoluer de :

```text
effet terminé
```

vers :

```text
opération acceptée ou en cours
```

---

# 122. Migration depuis une queue en mémoire

Le processus est :

```text
inventorier les messages
→ créer la table persistante
→ adapter les producteurs
→ adapter les handlers
→ ajouter leases et retries
→ ajouter dead-letter
→ désactiver la queue volatile
```

---

# 123. Migration vers un broker futur

Le processus pourra conserver :

- Job Types ;
- payloads ;
- outbox ;
- idempotency keys ;
- handlers ;
- observabilité.

La couche de transport pourra évoluer :

```text
PostgreSQL outbox
→ dispatcher
→ broker
→ consumers
```

---

## 123.1 Conditions

Un broker sera réévalué si :

- débit PostgreSQL insuffisant ;
- plusieurs services indépendants ;
- plusieurs consommateurs distants ;
- exigences de latence ;
- haute disponibilité ;
- routage complexe ;
- plusieurs régions.

---

# 124. Transfert client

Le handover doit inclure :

- schéma des jobs ;
- worker ;
- configuration ;
- schedules ;
- retries ;
- dead letters ;
- runbooks ;
- métriques ;
- alertes ;
- jobs en attente ;
- procédures de reprise ;
- providers et credentials à recréer.

---

## 124.1 État final

Le client doit savoir si le transfert contient :

- une queue vide ;
- des jobs en attente ;
- des dead letters ;
- des schedules actifs.

---

# 125. Clôture de service

La clôture suit :

```text
bloquer les nouveaux jobs non nécessaires
→ drainer les queues
→ traiter les dead letters
→ suspendre les schedules
→ produire un état final
→ transférer ou annuler
→ révoquer les credentials
→ appliquer la rétention
```

---

## 125.1 Job critique en attente

Un job critique ne doit pas être supprimé uniquement parce que le contrat de maintenance se termine.

Il doit être :

- exécuté ;
- transféré ;
- annulé explicitement ;
- ou documenté.

---

# 126. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de relancer des jobs ;
- de purger une queue ;
- d’annuler des jobs ;
- de démarrer un worker après restauration ;
- de modifier les retries ;
- d’appliquer une migration ;
- d’activer un schedule réel.

---

# 127. Réversibilité

## 127.1 Classification

```text
élevée avant déploiement

moyenne après persistance de jobs

élevée vers un broker conservant les contrats
```

---

## 127.2 Retrait d’un handler

La réversibilité dépend des jobs en attente et des effets déjà réalisés.

---

## 127.3 Effet externe

Un email envoyé ou une synchronisation effectuée n’est pas automatiquement réversible.

---

# 128. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `JobId` ;
- `JobType` ;
- `IdempotencyKey` ;
- statuts ;
- erreurs ;
- politiques.

---

## Étape 2 — Modèle PostgreSQL

Créer :

- jobs ;
- tentatives ;
- outbox ;
- schedules ;
- index ;
- migrations.

---

## Étape 3 — Enqueue

Implémenter :

- job direct ;
- outbox transactionnelle ;
- validation des payloads ;
- déduplication.

---

## Étape 4 — Dispatcher

Ajouter :

- claim outbox ;
- fan-out ;
- matérialisation idempotente ;
- marquage dispatché.

---

## Étape 5 — Worker

Ajouter :

- claim ;
- lease ;
- heartbeat ;
- cancellation ;
- résultat.

---

## Étape 6 — Retries

Implémenter :

- classification ;
- backoff ;
- jitter ;
- maximum attempts ;
- dead-letter.

---

## Étape 7 — Premier cas d’usage

Migrer un effet représentatif :

```text
notification de nouveau lead
```

---

## Étape 8 — Médias

Ajouter la génération asynchrone de variantes.

---

## Étape 9 — Schedules

Ajouter :

- réconciliation ;
- nettoyage ;
- politiques de chevauchement.

---

## Étape 10 — Opérations

Ajouter :

- health checks ;
- métriques ;
- alertes ;
- commandes ;
- runbooks de restauration.

---

# 129. Tâches liées proposées

```text
SF-TASK-189 — Define durable background job contracts

SF-TASK-190 — Add PostgreSQL job and outbox persistence

SF-TASK-191 — Implement transactional outbox writes

SF-TASK-192 — Implement idempotent outbox dispatch

SF-TASK-193 — Add PostgreSQL job claiming and leases

SF-TASK-194 — Add bounded retry and dead-letter handling

SF-TASK-195 — Add worker health checks and observability

SF-TASK-196 — Move lead notifications to durable jobs

SF-TASK-197 — Move media variant generation to durable jobs

SF-TASK-198 — Add recurring job scheduling policies

SF-TASK-199 — Add background job operator commands

SF-TASK-200 — Add job restoration and replay runbooks

SF-TASK-201 — Add multi-worker concurrency tests

SF-TASK-202 — Add background job security tests

SF-TASK-203 — Add upgrade compatibility checks for pending jobs
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 130. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Une transaction métier produit son outbox dans le même commit.

Un rollback métier ne laisse aucun message outbox.

Un redémarrage ne perd pas un job persisté.

Un crash après claim permet une reprise après lease.

Deux workers ne revendiquent pas normalement le même job.

Une seconde exécution reste idempotente.

Les retries sont bornés.

Les erreurs permanentes ne sont pas réessayées inutilement.

Un job épuisé devient dead-lettered.

Un dead-letter est visible et relançable explicitement.

Aucun payload ne contient de secret.

Les gros fichiers ne sont pas stockés dans les payloads.

Les Job Types sont stables et versionnés.

Un type CLR arbitraire ne peut pas être exécuté.

Les queues et concurrences sont bornées.

Les schedules possèdent une politique de chevauchement.

Les schedules possèdent une politique de rattrapage.

Les emails sont exécutés hors de la requête HTTP.

Les transformations médias sont exécutées hors requête.

Le worker possède des health checks.

L’outbox lag et les dead letters sont observables.

Un upgrade refuse de retirer un handler encore nécessaire.

Après restauration, le worker reste arrêté jusqu’à validation.

Les tests de concurrence utilisent PostgreSQL réel.

Le déploiement fonctionne sur Linux avec un service worker distinct.
```

---

# 131. Conditions de révision

Réexaminer cette décision si :

- PostgreSQL devient un goulot d’étranglement ;
- plusieurs services doivent consommer les mêmes événements ;
- plusieurs régions sont nécessaires ;
- des millions de jobs doivent être traités ;
- une latence sub-seconde devient obligatoire ;
- plusieurs équipes déploient des consumers indépendants ;
- un broker managé devient économiquement pertinent ;
- des workflows humains complexes sont introduits ;
- les jobs nécessitent une orchestration de longue durée ;
- les traitements GPU deviennent importants ;
- la haute disponibilité du worker devient une exigence standard.

---

# 132. Indicateurs à suivre

- profondeur des queues ;
- âge du plus ancien job ;
- outbox lag ;
- durée moyenne des jobs ;
- taux de succès ;
- taux de retry ;
- dead letters ;
- leases expirés ;
- workers actifs ;
- jobs par type ;
- jobs par queue ;
- taille des tables ;
- durée de nettoyage ;
- schedules manqués ;
- coût des fournisseurs ;
- doublons détectés ;
- durée de drainage avant upgrade.

---

# 133. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des Task.Run apparaissent dans les endpoints

des effets critiques utilisent une queue mémoire

des retries deviennent infinis

les dead letters sont ignorés

les payloads contiennent des secrets ou fichiers

les jobs non versionnés s’accumulent

les leases expirent fréquemment

PostgreSQL subit une forte contention

les restaurations rejouent des effets sans contrôle

les modules sont retirés avec des jobs en attente

les workers exécutent des types par réflexion

les schedules produisent des tempêtes de rattrapage
```

---

# 134. Conséquences observées

Cette section est append-only après mise en œuvre.

## 134.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation du worker,
de la transactional outbox et de la queue PostgreSQL.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- Job Type ;
- queue ;
- volume ;
- durée ;
- retry ;
- incident ou bénéfice ;
- action proposée.

---

# 135. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0031-application-startup-validation-and-runtime-compatibility.md](0031-application-startup-validation-and-runtime-compatibility.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- [18-security-baseline.md](../04-quality-security/18-security-baseline.md)
- [19-testing-strategy.md](../04-quality-security/19-testing-strategy.md)
- [20-quality-gates-and-ci.md](../04-quality-security/20-quality-gates-and-ci.md)
- [21-observability-and-logging.md](../05-operations/21-observability-and-logging.md)
- [22-deployment-and-environments.md](../05-operations/22-deployment-and-environments.md)
- [23-backup-and-restore.md](../05-operations/23-backup-and-restore.md)
- [24-operational-runbooks.md](../05-operations/24-operational-runbooks.md)
- [25-upgrade-and-maintenance-strategy.md](../05-operations/25-upgrade-and-maintenance-strategy.md)
- [26-release-and-versioning.md](../05-operations/26-release-and-versioning.md)
- [27-client-handover-and-service-closure.md](../05-operations/27-client-handover-and-service-closure.md)
- [28-codex-operating-guide.md](../06-codex/28-codex-operating-guide.md)
- [0000-adr-template.md](0000-adr-template.md)
- [0001-one-repository-and-database-per-client.md](0001-one-repository-and-database-per-client.md)
- [0003-postgresql-as-primary-database.md](0003-postgresql-as-primary-database.md)
- [0004-explicit-production-migrations.md](0004-explicit-production-migrations.md)
- [0006-feature-flags-and-entitlements-separation.md](0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](0007-module-composition-and-public-contracts.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](0012-runtime-configuration-and-secrets.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](0014-explicit-client-upgrades-and-version-pinning.md)
- [0016-content-and-media-storage-boundaries.md](0016-content-and-media-storage-boundaries.md)

---

# 136. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 131 |

---

# 137. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 138. Synthèse de la décision

```text
Transaction métier
→ données PostgreSQL
→ message outbox
→ même commit

Dispatch
→ worker
→ outbox revendiquée
→ un job par handler
→ idempotency key
→ transaction courte

Job
→ Job Type stable
→ payload versionné
→ aucun secret
→ taille bornée
→ queue et priorité
→ timeout
→ maximum attempts

Exécution
→ claim PostgreSQL
→ lease
→ handler idempotent
→ at-least-once
→ aucun exactly-once promis

Échec
→ classification
→ exponential backoff
→ jitter
→ retry borné
→ dead-letter
→ alerte
→ retry manuel explicite

Worker
→ service Docker distinct
→ aucun port Internet
→ health checks
→ concurrence bornée
→ arrêt gracieux

Schedules
→ UTC
→ chevauchement explicite
→ rattrapage borné
→ aucune tempête après interruption

Upgrades
→ jobs en attente analysés
→ anciens handlers conservés ou migrés
→ module non retiré avec jobs actifs

Restauration
→ worker arrêté
→ inventaire
→ réconciliation des effets externes
→ reprise progressive

Évolution
→ PostgreSQL dans le MVP
→ mêmes contrats réutilisables avec un broker futur
```

> Un traitement en arrière-plan n’est fiable ni parce qu’il est asynchrone, ni parce qu’il est exécuté dans un worker. Il devient exploitable lorsqu’il est persisté, idempotent, borné, observable et capable de reprendre sans supposer qu’un effet externe ne s’est jamais produit.
