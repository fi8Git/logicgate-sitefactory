---
adrId: ADR-0032
title: Deploy immutable releases through explicit rollouts, maintenance controls and verified rollback plans
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
  - ../03-generation/15-cli-specification.md
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
  - ./0005-file-generation-strategies.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0013-local-git-initialization-and-explicit-remote-publishing.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0017-background-jobs-and-reliable-side-effects.md
  - ./0019-audit-trail-and-sensitive-administrative-actions.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ./0021-administrator-provisioning-invitations-and-account-recovery.md
  - ./0022-browser-security-headers-and-content-security-policy.md
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
  - ./0024-client-data-export-and-portability.md
  - ./0025-third-party-integrations-and-egress-control.md
  - ./0026-container-image-build-and-runtime-hardening.md
  - ./0027-database-roles-and-runtime-least-privilege.md
  - ./0028-database-transaction-boundaries-and-concurrency-control.md
  - ./0029-http-api-contracts-problem-details-and-idempotent-commands.md
  - ./0030-cache-strategy-and-invalidation.md
  - ./0031-application-startup-validation-and-runtime-compatibility.md
targetVersion: 0.1.0
---

# ADR-0032 — Déploiement progressif, mode maintenance et sécurité du rollback

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Les plans de rollout, maintenance/drainage, backups, orchestration de
déploiement et rollback ci-dessous sont des exigences **cibles**,
pas des commandes de la CLI publique actuelle.
Le [runtime livré](../03-generation/generated-startup-validation.md) apporte
startup validation et readiness, pas un orchestrateur de production.
Un rollback applicatif n'autorise jamais un retour à un schéma incompatible ;
les garde-fous de maintenance, restore, réconciliation et confirmation humaine
restent intacts. Aucun déploiement, migration production ou effet distant
n'est autorisé par la présence des exemples. CI distante non encore validée.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory déploie les releases de production au moyen d’une procédure :

```text
explicite

planifiée

verrouillée

vérifiable

auditée

réversible lorsque la compatibilité le permet
```

La baseline du MVP repose sur :

```text
un VPS Linux

Docker Compose

Caddy

une instance web principale

un worker principal

PostgreSQL partagé par les composants du projet

des images OCI immuables

des migrations explicites

un mode maintenance indépendant de l’application
```

SiteFactory ne promet pas un déploiement sans interruption dans le profil VPS initial.

Lorsqu’un changement de schéma, de stockage, de contrat de jobs ou de configuration rend la coexistence risquée, une fenêtre de maintenance courte et contrôlée est préférée à une simulation de haute disponibilité non maîtrisée.

Les principes suivants sont obligatoires :

```text
plan avant apply

même digest entre staging et production

aucun tag latest

aucune migration automatique

aucun déploiement concurrent

aucun trafic métier pendant une migration incompatible

aucun worker revendiquant des jobs pendant une phase incompatible

aucune désactivation du mode maintenance avant validation

aucun rollback automatique de base de données

aucun rollback applicatif si le schéma n’est pas compatible

aucun restore présenté comme un simple rollback

aucun contournement public du mode maintenance

aucune réouverture automatique après l’échec du déploiement

aucun rapport contenant des secrets
```

---

## 2. Résumé

Le déploiement de référence suit :

```text
release construite et signée
→ validation en staging
→ génération du plan de déploiement
→ vérification de compatibilité
→ acquisition du verrou de déploiement
→ vérification du backup
→ activation du mode maintenance edge
→ activation du write freeze applicatif
→ drainage du worker
→ arrêt des composants mutateurs
→ application explicite des migrations
→ déploiement des digests cibles
→ startup validation
→ smoke tests
→ reprise contrôlée du runtime
→ retrait du mode maintenance edge
→ surveillance renforcée
→ clôture et audit
```

Le rollback suit :

```text
incident détecté
→ maintien ou réactivation du mode maintenance
→ génération du plan de rollback
→ vérification du schéma, des jobs et de la configuration
→ choix entre :
   - redéploiement de l’ancien digest
   - correction forward
   - migration corrective
   - restauration contrôlée
→ exécution explicite
→ validation
→ réouverture
```

Le mode maintenance possède deux couches :

```text
Edge maintenance
→ géré par Caddy
→ indépendant du web et de PostgreSQL
→ page statique 503

Runtime maintenance
→ état persistant
→ bloque les mutations
→ suspend les claims de jobs
→ expose les capabilities autorisées
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Une release de production possède un Deployment Plan.

Une release de production possède un Rollback Assessment.

Une seule opération de déploiement peut être active
pour un client et un environnement.

Le déploiement utilise les digests OCI approuvés.

Le digest production est identique
au digest validé en staging.

Une migration est exécutée uniquement
par l’image migrator approuvée.

Le web et le worker n’appliquent aucune migration.

Le mode maintenance edge reste actif
tant que la nouvelle release n’est pas validée.

Un processus de déploiement interrompu
ne réouvre pas automatiquement le trafic.

Le worker ne revendique aucun nouveau job
pendant son drainage ou une incompatibilité de schéma.

Un job en cours n’est pas tué silencieusement
si son effet externe est incertain.

Une migration échouée laisse le site
en mode maintenance.

Une release non-ready ne reçoit aucun trafic métier.

Un smoke test échoué bloque la réouverture.

Un rollback applicatif utilise un digest déjà vérifié.

Une ancienne release ne redémarre pas
sur un schéma qu’elle ne supporte pas.

Une restauration déclenche les procédures
de sécurité, d’epochs et de privacy reconciliation.

La clôture de l’opération produit un rapport sans secret.

Toute dérogation possède une raison, un propriétaire
et une expiration.
```

---

## 4. Contexte

Le déploiement d’une application SiteFactory peut modifier simultanément :

- les images web et worker ;
- l’image migrator ;
- la configuration runtime ;
- les secrets référencés ;
- le schéma PostgreSQL ;
- les contrats de jobs ;
- les contrats de l’outbox ;
- les profils de cache ;
- les profils CSP ;
- les permissions PostgreSQL ;
- le layout des médias ;
- les catalogues de confidentialité ;
- les intégrations externes.

Un simple :

```text
docker compose pull
docker compose up -d
```

ne garantit pas :

- que l’image est signée ;
- que le schéma est compatible ;
- que le backup est utilisable ;
- que le worker ne traite pas un ancien payload ;
- que les migrations sont terminées ;
- que le web utilise la bonne base ;
- que la nouvelle release est réellement ready ;
- que l’ancien digest peut encore être redéployé ;
- que la restauration est possible ;
- que les données supprimées ne réapparaîtront pas.

Les opérations de déploiement sont particulièrement risquées lorsqu’elles combinent :

```text
nouvelle image

migration destructive

transformation de données

contrats de jobs modifiés

modification de configuration

changement de fournisseur
```

La baseline doit donc considérer le déploiement comme une opération métier et de sécurité à part entière.

---

## 5. Problème à résoudre

Comment déployer une nouvelle release tout en garantissant :

- l’intégrité des données ;
- la compatibilité application-base ;
- la cohérence du worker ;
- la possibilité de revenir à une version antérieure lorsque cela est sûr ;
- une indisponibilité bornée et expliquée ;
- une absence d’écritures pendant les phases incompatibles ;
- une restauration contrôlée en dernier recours ;
- une traçabilité complète ?

La solution doit également éviter :

- les migrations au démarrage ;
- les déploiements simultanés ;
- les scripts shell non idempotents ;
- la réouverture trop précoce ;
- les rollbacks improvisés ;
- le retour à un ancien binaire avec un nouveau schéma incompatible ;
- l’utilisation de `latest` ;
- les migrations descendantes automatiques ;
- les restores exécutés directement en production ouverte ;
- le maintien de Caddy hors contrôle pendant un incident ;
- la suppression trop rapide des anciens digests.

---

## 6. Portée

Cette décision couvre :

- plans de déploiement ;
- classification des releases ;
- verrouillage opérateur ;
- mode maintenance ;
- Caddy ;
- write freeze ;
- drainage des jobs ;
- backups pré-déploiement ;
- migrations ;
- images ;
- configuration ;
- secrets ;
- rollout ;
- startup validation ;
- smoke tests ;
- surveillance ;
- rollback ;
- forward fixes ;
- restores ;
- rapports ;
- audit ;
- tests ;
- quality gates ;
- runbooks ;
- handover.

---

## 7. Hors portée

Cette décision ne définit pas :

- Kubernetes ;
- un déploiement multi-région ;
- une bascule DNS globale ;
- un load balancer managé ;
- un déploiement canary distribué ;
- une architecture blue-green complète ;
- une réplication PostgreSQL haute disponibilité ;
- un rollback automatique universel ;
- une garantie de zéro interruption ;
- une orchestration centralisée multi-client ;
- une réplication active-active.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Protection des données**
2. **Compatibilité du schéma**
3. **Absence d’écritures concurrentes incompatibles**
4. **Rollback évalué avant le déploiement**
5. **Mode maintenance indépendant de l’application**
6. **Drainage fiable des jobs**
7. **Même artefact entre staging et production**
8. **Validation avant réouverture**
9. **Backup vérifié**
10. **Traçabilité**
11. **Échec sûr**
12. **Temps de maintenance maîtrisé**
13. **Reprise après interruption**
14. **Compatibilité Docker Compose**
15. **Transfert client**
16. **Simplicité proportionnée au MVP**

---

## 9. Contraintes

La décision doit respecter :

```text
Docker Compose

Caddy

PostgreSQL

images par digest

images signées

migrations explicites

rôles PostgreSQL séparés

startup validation

worker durable

outbox

backups chiffrés

audit

step-up

aucun secret dans les rapports

compatibilité Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le profil initial utilise un seul VPS ;
- une courte fenêtre de maintenance est acceptable ;
- un opérateur autorisé déclenche la production ;
- la release a été validée en staging ;
- les anciennes images restent disponibles pendant la fenêtre de rollback ;
- un backup peut être produit avant une migration risquée ;
- les jobs utilisent des leases et l’idempotence ;
- Caddy peut servir une page statique même si l’application est arrêtée ;
- les migrations possèdent un plan préalable ;
- la base contient un état opérationnel persistant.

---

# 11. Définitions

## 11.1 Deployment

Opération faisant passer un environnement d’une release source à une release cible.

---

## 11.2 Rollout

Enchaînement contrôlé des étapes d’activation d’une release.

---

## 11.3 Rollback

Retour contrôlé vers un état applicatif antérieur compatible.

---

## 11.4 Forward fix

Nouvelle correction appliquée sur l’état actuel plutôt qu’un retour vers l’état précédent.

---

## 11.5 Maintenance mode

État limitant ou bloquant le trafic métier pendant une opération.

---

## 11.6 Write freeze

État empêchant les nouvelles mutations tout en permettant éventuellement certaines lectures.

---

## 11.7 Worker drain

Arrêt de la prise de nouveaux jobs et attente bornée des jobs actifs.

---

## 11.8 Deployment Plan

Artefact en lecture seule décrivant l’opération cible.

---

## 11.9 Rollback Assessment

Analyse prouvant ou refusant la compatibilité d’un retour.

---

## 11.10 Stabilization window

Période de surveillance renforcée après la réouverture.

---

## 11.11 Deployment lock

Mécanisme empêchant plusieurs opérations concurrentes.

---

## 11.12 Deployment Operation ID

Identifiant opaque reliant les étapes, rapports et audits du déploiement.

---

# 12. Option A — `docker compose up` direct

## 12.1 Description

L’opérateur tire les nouvelles images et redémarre les services.

---

## 12.2 Avantages

- simple ;
- rapide ;
- faible outillage ;
- peu de procédures.

---

## 12.3 Inconvénients

- aucune analyse ;
- migration et image mélangées ;
- rollback non préparé ;
- trafic potentiellement actif ;
- worker concurrent ;
- faible traçabilité.

---

## 12.4 Risques

- ancien web sur nouveau schéma ;
- nouveau worker sur ancien schéma ;
- job inconnu ;
- base non sauvegardée ;
- image non signée ;
- service rouvert en erreur.

---

## 12.5 Réversibilité

```text
faible à moyenne
```

---

# 13. Option B — Migration automatique au démarrage

## 13.1 Description

Le nouveau web applique les migrations avant d’écouter.

---

## 13.2 Avantages

- faible nombre d’étapes ;
- déploiement automatisé ;
- schéma mis à jour avec l’image.

---

## 13.3 Inconvénients

- credential puissant dans le runtime ;
- race entre composants ;
- migration non observée ;
- rollback difficile ;
- indisponibilité imprévisible ;
- mauvais environnement potentiellement modifié.

---

## 13.4 Risques

- migration partielle ;
- plusieurs instances migrent ;
- site redémarre en boucle ;
- ancien worker continue ;
- perte de données.

---

## 13.5 Réversibilité

```text
faible
```

---

# 14. Option C — Blue-green généralisé

## 14.1 Description

Deux stacks complètes coexistent et le trafic bascule entre elles.

---

## 14.2 Avantages

- faible interruption ;
- validation avant bascule ;
- rollback de trafic rapide ;
- isolement applicatif.

---

## 14.3 Inconvénients

- deux stacks ;
- capacité VPS ;
- base partagée ;
- migrations compatibles obligatoires ;
- complexité ;
- coûts ;
- jobs concurrents.

---

## 14.4 Risques

- deux versions écrivant dans la même base ;
- contrats de jobs incompatibles ;
- double worker ;
- rollback impossible malgré la bascule.

---

## 14.5 Réversibilité

```text
élevée pour l’image
faible pour le schéma
```

---

# 15. Option D — Rollout explicite avec maintenance contrôlée

## 15.1 Description

Le déploiement utilise :

- plan ;
- lock ;
- maintenance ;
- drainage ;
- backup ;
- migration ;
- validation ;
- réouverture.

---

## 15.2 Avantages

- prévisible ;
- compatible VPS ;
- sécurité forte ;
- rollback évalué ;
- jobs protégés ;
- audit ;
- faible infrastructure supplémentaire.

---

## 15.3 Inconvénients

- interruption contrôlée ;
- davantage d’étapes ;
- procédures opérateur ;
- maintenance page ;
- tests et rapports.

---

## 15.4 Risques

- maintenance prolongée ;
- opérateur interrompu ;
- migration irréversible ;
- smoke tests insuffisants ;
- erreur de séquence.

---

## 15.5 Réversibilité

```text
élevée avant migration
variable après migration
```

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Compose direct | Auto-migration | Blue-green | Rollout contrôlé |
|---|---:|---:|---:|---:|---:|
| Protection des données | 5 | 1 | 1 | 3 | 5 |
| Compatibilité VPS | 5 | 5 | 5 | 2 | 5 |
| Contrôle des migrations | 5 | 1 | 1 | 4 | 5 |
| Rollback | 5 | 2 | 1 | 4 | 5 |
| Worker safety | 5 | 1 | 1 | 2 | 5 |
| Faible interruption | 3 | 3 | 3 | 5 | 3 |
| Auditabilité | 4 | 1 | 2 | 4 | 5 |
| Diagnostic | 4 | 2 | 2 | 4 | 5 |
| Maintenabilité | 5 | 2 | 2 | 3 | 5 |
| Testabilité | 4 | 2 | 2 | 4 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’utiliser un orchestrateur de déploiement SiteFactory piloté par des commandes explicites.

Commandes conceptuelles :

```text
sitefactory deployment plan

sitefactory deployment apply

sitefactory deployment status

sitefactory deployment abort

sitefactory deployment finalize

sitefactory rollback plan

sitefactory rollback apply
```

L’outil ne remplace pas Docker Compose.

Il orchestre et vérifie les opérations nécessaires autour de Docker Compose.

---

## 17.1 Aucun apply implicite

La génération d’un plan ne modifie pas l’environnement.

---

## 17.2 Plan obligatoire

`apply` exige :

- le plan exact ;
- son hash ;
- sa durée de validité ;
- la confirmation de l’environnement ;
- l’identité de l’opérateur.

---

# 18. Profils de rollout

Profils initiaux :

```text
deployment.application-only.v1

deployment.expand-compatible.v1

deployment.maintenance-required.v1

deployment.high-risk-data-change.v1

deployment.restore-recovery.v1
```

---

## 18.1 Application only

Aucune migration ni modification de stockage.

Le schéma courant est compatible avec les deux releases.

---

## 18.2 Expand compatible

Une migration additive permet une coexistence courte entre les versions.

---

## 18.3 Maintenance required

Une migration ou modification incompatible exige l’arrêt des écritures.

---

## 18.4 High-risk data change

Une transformation importante ou difficilement réversible exige des contrôles renforcés.

---

## 18.5 Restore recovery

Profil distinct utilisé après un restore.

Il ne constitue pas un rollout ordinaire.

---

# 19. Classification d’une release

Chaque release est classée avant déploiement.

---

## 19.1 Classe A — Application only

```text
aucune migration

aucun nouveau contrat de job incompatible

aucun changement de layout persistant

configuration compatible
```

---

## 19.2 Classe B — Expand compatible

```text
migration additive

ancienne et nouvelle application compatibles

aucune suppression immédiate de colonne

aucun renommage destructif

contrats de jobs coexistants
```

---

## 19.3 Classe C — Exclusive migration

```text
schéma exact requis

coexistence interdite

maintenance obligatoire

rollback applicatif potentiellement bloqué
```

---

## 19.4 Classe D — High-risk data transformation

```text
transformation volumineuse

migration de média

changement irréversible

suppression ou anonymisation

modification de format durable
```

---

# 20. Deployment Plan

Nom conceptuel :

```text
deployment-plan.json
```

---

## 20.1 Contenu

Le plan doit pouvoir contenir :

```text
SchemaVersion

DeploymentOperationId

ClientSlug

Environment

SourceApplicationVersion

TargetApplicationVersion

SourceImageDigests

TargetImageDigests

MigrationImageDigest

SourceConfigurationHash

TargetConfigurationHash

SourceModuleLockHash

TargetModuleLockHash

CurrentDatabaseSchema

TargetDatabaseSchema

DeploymentClass

RolloutProfile

MaintenanceRequired

WorkerDrainRequired

BackupPolicy

MigrationPlanId

MigrationPlanHash

RollbackAssessment

SmokeTestProfile

ExpectedDowntime

StabilizationWindow

RequiredPermissions

Warnings

BlockingIssues

CreatedAtUtc

ExpiresAtUtc
```

---

## 20.2 Secret

Le plan ne contient pas :

- password ;
- token ;
- secret value ;
- chaîne de connexion ;
- clé privée ;
- credential de registry.

---

# 21. Validité du plan

Un plan devient invalide si changent :

- release cible ;
- digest ;
- configuration ;
- schéma ;
- jobs actifs incompatibles ;
- lock module ;
- état de restauration ;
- backup requis ;
- environnement ;
- date d’expiration.

---

## 21.1 Recalcul

Un nouveau plan est requis.

---

## 21.2 Pas d’adaptation silencieuse

`apply` ne recalcule pas silencieusement les différences.

---

# 22. Rollback Assessment

Le plan contient un diagnostic :

```text
rollback-compatible

rollback-compatible-with-maintenance

rollback-requires-forward-migration

rollback-requires-restore

rollback-incompatible

rollback-unknown
```

---

## 22.1 Éléments vérifiés

- schéma PostgreSQL ;
- configuration ;
- secrets ;
- modules ;
- jobs ;
- outbox ;
- cache ;
- médias ;
- APIs internes ;
- permissions ;
- Data Protection.

---

## 22.2 Valeur unknown

Un rollback `unknown` bloque le déploiement d’une release à risque élevé jusqu’à une décision humaine explicite.

---

# 23. Manifest de déploiement

Une release possède un artefact :

```text
deployment-release-manifest.json
```

---

## 23.1 Contenu

```text
ApplicationVersion

ImageDigests

MigrationImageDigest

RuntimeCompatibilityManifestHash

ContainerReleaseManifestHash

ModuleLockHash

ConfigurationSchemaVersion

DatabaseCompatibility

JobCompatibility

RollbackCompatibility

OpenApiArtifactHash

GeneratedAtUtc
```

---

## 23.2 Vérification

Le plan relie chaque valeur à la release signée.

---

# 24. Même artefact entre environnements

Le digest validé en staging est promu en production.

---

## 24.1 Interdiction

L’image n’est pas reconstruite pour :

- changer une URL ;
- ajouter un secret ;
- modifier un environnement ;
- corriger un fichier de configuration.

---

## 24.2 Configuration

Les différences d’environnement sont injectées au runtime.

---

# 25. Verrou de déploiement

Une seule opération peut être active.

---

## 25.1 Verrou hôte

Le profil mono-VPS utilise un verrou exclusif local, par exemple via :

```text
flock
```

sur un fichier appartenant au répertoire opérateur SiteFactory.

---

## 25.2 Enregistrement PostgreSQL

Lorsque PostgreSQL est disponible, une opération est également enregistrée dans :

```text
sitefactory_runtime.deployment_operations
```

---

## 25.3 Verrou de migration

Le migrator peut prendre un advisory lock PostgreSQL explicitement dédié aux opérations de migration.

---

## 25.4 Exception ADR-0028

L’usage d’un advisory lock de session est accepté ici uniquement pour :

- l’opération migrator ;
- une connexion dédiée ;
- une durée bornée ;
- une release identifiée.

La fermeture de la connexion libère le verrou.

---

# 26. Modèle d’opération de déploiement

Champs conceptuels :

```text
DeploymentOperationId

PlanHash

SourceRelease

TargetRelease

Status

CurrentPhase

DeploymentClass

RuntimeMode

StartedAtUtc

UpdatedAtUtc

CompletedAtUtc

ActorId

ReasonCode

BackupId

MigrationOperationId

FailureCode

ReportId
```

---

## 26.1 État PostgreSQL indisponible

Le processus conserve également un rapport opérateur local sécurisé afin de permettre une réconciliation ultérieure.

---

# 27. États de déploiement

Valeurs recommandées :

```text
planned

preflight-running

preflight-failed

ready-to-apply

maintenance-activating

maintenance-active

worker-draining

backup-verifying

migration-running

migration-failed

deploying

startup-validating

smoke-testing

ready-to-open

opening

active

stabilizing

stabilized

rollback-required

rollback-running

failed

aborted
```

---

## 27.1 Transition

Chaque transition est validée.

---

## 27.2 Reprise

Une reprise après interruption repart de l’état persistant et du rapport opérateur.

---

# 28. Permissions de déploiement

Permissions conceptuelles :

```text
Deployment.Plan

Deployment.Apply

Deployment.Abort

Deployment.Finalize

Deployment.Rollback

Deployment.Maintenance.Manage

Deployment.Override
```

---

## 28.1 Step-up

`Apply`, `Rollback`, `Maintenance.Manage` et `Override` exigent un step-up critique.

---

## 28.2 Raison

Une raison structurée est obligatoire.

---

## 28.3 Séparation future

Un profil renforcé peut imposer deux acteurs distincts.

---

# 29. Preflight

Le preflight est en lecture seule.

---

## 29.1 Vérifications release

- digest ;
- signature ;
- provenance ;
- SBOM ;
- scans ;
- architecture ;
- manifest.

---

## 29.2 Vérifications environnement

- client ;
- environnement ;
- espace disque ;
- Docker ;
- Compose ;
- Caddy ;
- registry ;
- volumes ;
- permissions.

---

## 29.3 Vérifications PostgreSQL

- Database Identity ;
- rôle ;
- schéma ;
- migrations ;
- locks ;
- état restore ;
- état privacy ;
- epochs ;
- backup metadata.

---

## 29.4 Vérifications applicatives

- runtime compatibility ;
- modules ;
- jobs ;
- outbox ;
- operations actives ;
- cache ;
- integrations ;
- configuration ;
- secrets requis.

---

# 30. Espace disque

Le plan estime l’espace nécessaire pour :

- nouvelles images ;
- ancienne image conservée ;
- backup ;
- WAL de migration ;
- fichiers temporaires ;
- exports ;
- logs.

---

## 30.1 Seuil

Le déploiement est bloqué si l’espace de sécurité n’est pas respecté.

---

## 30.2 Nettoyage

Le nettoyage préalable n’efface pas un digest de rollback encore requis.

---

# 31. Vérification Caddy

Avant toute modification :

```text
caddy validate
```

ou mécanisme équivalent valide la configuration cible.

---

## 31.1 Échec

Le mode normal reste actif si la configuration de maintenance ne peut pas être validée avant activation.

---

## 31.2 Pendant maintenance

Une configuration invalide ne doit pas faire tomber Caddy.

---

# 32. Mode maintenance edge

Le mode edge est servi par Caddy indépendamment du web.

---

## 32.1 Objectif

Maintenir une réponse contrôlée même lorsque :

- le web est arrêté ;
- PostgreSQL est migré ;
- l’application est incompatible ;
- le worker est drainé.

---

## 32.2 Activation

Le processus opérateur :

1. génère ou sélectionne le fragment de maintenance ;
2. valide Caddy ;
3. remplace atomiquement la configuration active ;
4. recharge Caddy ;
5. vérifie la réponse 503.

---

# 33. Page de maintenance

La page est :

- statique ;
- locale ;
- sans JavaScript ;
- sans analytics ;
- sans ressource tierce ;
- accessible ;
- responsive ;
- neutre.

---

## 33.1 Statut

Elle retourne :

```text
503 Service Unavailable
```

---

## 33.2 Headers

```text
Retry-After

Cache-Control: no-store

Content-Security-Policy stricte

X-Content-Type-Options: nosniff

Referrer-Policy: no-referrer
```

---

## 33.3 Contenu

Elle peut indiquer :

- maintenance en cours ;
- indisponibilité temporaire ;
- invitation à réessayer plus tard.

Elle ne contient pas :

- détails de migration ;
- stack trace ;
- nom de base ;
- version interne ;
- identifiant d’opérateur.

---

# 34. Pas de bypass public

Le mode maintenance ne se contourne pas avec :

```text
?bypass=secret

cookie caché

header public non authentifié

URL obscure
```

---

## 34.1 Accès opérateur

Les validations internes passent par :

- localhost ;
- réseau Docker ;
- SSH tunnel ;
- endpoint interne protégé ;
- commande CLI.

---

## 34.2 Administration publique

L’administration publique reste indisponible pendant une migration incompatible.

---

# 35. Routes autorisées pendant maintenance

La baseline peut autoriser uniquement :

- challenge ACME nécessaire ;
- liveness Caddy ;
- page de maintenance ;
- ressources statiques de maintenance.

---

## 35.1 Health application

Les health checks applicatifs sont accessibles depuis le réseau interne, pas nécessairement via le host public.

---

# 36. Mode maintenance runtime

PostgreSQL conserve un état opérationnel.

Table conceptuelle :

```text
sitefactory_runtime.operational_mode
```

---

## 36.1 Champs

```text
Mode

DeploymentOperationId

ReasonCode

ActivatedAtUtc

ActivatedByActorId

ExpectedEndAtUtc

Version
```

---

## 36.2 Modes

```text
normal

write-freeze

maintenance

restore-reconciliation

emergency-read-only
```

---

# 37. Write freeze

Le write freeze empêche :

- nouveaux formulaires persistés ;
- mutations administratives ;
- création d’exports ;
- modifications de contenu ;
- nouveaux jobs métier.

---

## 37.1 Lectures

Certaines lectures peuvent rester possibles avant l’activation du mode edge.

---

## 37.2 Réponse

Les mutations retournent :

```text
503 Service Unavailable
```

avec un Error Code stable.

---

# 38. Double couche de protection

La maintenance utilise :

```text
Caddy edge maintenance

+ runtime write freeze
```

---

## 38.1 Raison

Caddy bloque les nouvelles requêtes publiques.

Le runtime protège les requêtes déjà établies ou les connexions internes.

---

## 38.2 Blazor circuits

Les circuits existants ne doivent pas pouvoir effectuer de nouvelles mutations après le write freeze.

---

# 39. Activation sûre de la maintenance

Ordre recommandé :

```text
créer l’opération

activer le write freeze

vérifier le write freeze

activer la maintenance edge

vérifier la réponse 503

commencer le drainage
```

---

## 39.1 Échec avant edge

Si le write freeze échoue, le déploiement est interrompu avant migration.

---

## 39.2 Échec après edge

Le site reste en maintenance jusqu’à résolution ou abort sûr.

---

# 40. Aucun auto-expiry dangereux

Le mode maintenance ne se désactive pas automatiquement à une heure donnée.

---

## 40.1 Alerte

Un `ExpectedEndAtUtc` dépassé déclenche une alerte.

---

## 40.2 Réouverture

Seul un workflow validé retire la maintenance.

---

# 41. Requêtes en cours

Après le write freeze :

- les nouvelles mutations sont refusées ;
- les transactions courtes déjà commencées peuvent terminer ;
- une période de grâce est respectée ;
- les requêtes longues sont annulées selon leur contrat.

---

## 41.1 Deadline

La période de grâce est bornée.

---

## 41.2 Transaction incertaine

Une commande idempotente peut être vérifiée après drainage.

---

# 42. Drainage du worker

Le worker passe dans un état :

```text
draining
```

---

## 42.1 Effets

- aucun nouveau claim ;
- aucun nouveau schedule ;
- les jobs actifs continuent selon leur politique ;
- les leases restent renouvelés pendant la grâce ;
- les jobs terminés sont finalisés.

---

## 42.2 Readiness

Le worker peut rester live mais non-ready pour de nouveaux jobs.

---

# 43. Deadline de drainage

Le plan définit :

```text
WorkerDrainTimeout
```

---

## 43.1 À l’expiration

Pour chaque job encore actif :

- attendre davantage ;
- demander l’annulation coopérative ;
- laisser expirer le lease ;
- aborter le déploiement ;
- utiliser une dérogation.

---

## 43.2 Pas de kill aveugle

Un processus ne doit pas être tué sans savoir si un effet externe est en cours ou terminé.

---

# 44. Jobs non interruptibles

Un Job Type doit déclarer :

```text
Interruptibility

MaximumSafeDrainTime

IdempotencyPolicy

UncertainEffectPolicy
```

---

## 44.1 Job externe

Un email ou webhook en cours utilise :

- Idempotency Key ;
- delivery record ;
- état incertain ;
- réconciliation.

---

## 44.2 Plan

Les jobs non interruptibles sont visibles avant apply.

---

# 45. Contrats de jobs incompatibles

Le déploiement est bloqué si la release cible ne reconnaît pas :

- Job Type pending ;
- payload version active ;
- message outbox non traité ;
- Operation active.

---

## 45.1 Solutions

- drainer ;
- migrer ;
- annuler ;
- maintenir un handler compatible ;
- déployer une phase intermédiaire.

---

# 46. Arrêt des composants mutateurs

Après drainage :

```text
worker arrêté

web arrêté ou placé hors trafic

migrator seul autorisé
```

---

## 46.1 PostgreSQL

PostgreSQL reste disponible pour la migration.

---

## 46.2 Caddy

Caddy continue de servir la page de maintenance.

---

# 47. Backup pré-déploiement

Un backup est obligatoire lorsque le déploiement comprend :

- migration ;
- transformation de données ;
- changement de layout média ;
- changement de privacy state ;
- opération destructive ;
- risque de rollback nécessitant un restore.

---

## 47.1 Application only

Pour une release sans mutation persistante, un backup récent et vérifié conforme à la policy peut suffire.

---

# 48. Backup ID

Le plan référence :

```text
BackupId
```

---

## 48.1 Vérifications

- date ;
- client ;
- environnement ;
- chiffrement ;
- checksum ;
- base ;
- App_Data lorsque nécessaire ;
- statut ;
- rétention ;
- procédure de restore.

---

## 48.2 Existence seule

La présence d’un fichier ne prouve pas que le backup est exploitable.

---

# 49. Vérification du backup

Avant une migration risquée :

```text
backup terminé

archive lisible

manifest présent

checksums valides

liste des objets vérifiée

destination distincte disponible
```

---

## 49.1 Restore drill

Un restore drill complet n’est pas obligatoire avant chaque release.

Le plan vérifie que le dernier restore drill reste dans la fenêtre définie par la politique.

---

## 49.2 High risk

Une transformation critique peut exiger un restore de validation dans un environnement isolé.

---

# 50. Backup des médias

Une migration affectant le layout ou les métadonnées média exige un snapshot cohérent de :

- PostgreSQL ;
- originaux ;
- variantes nécessaires ;
- manifest média.

---

# 51. Migration Plan

Toute migration possède un plan versionné.

Nom conceptuel :

```text
database-migration-plan.json
```

---

## 51.1 Contenu

```text
MigrationOperationId

CurrentSchema

TargetSchema

MigrationIds

SqlArtifactHash

TransactionMode

ExpectedDuration

LockImpact

DataTransformation

Reversibility

RollbackStrategy

RequiredDiskSpace

RequiredBackup

PostMigrationChecks
```

---

## 51.2 SQL

Le SQL généré ou appliqué est lié par hash au plan.

---

# 52. Revue des migrations

La revue doit identifier :

- opérations DDL ;
- locks ;
- table rewrites ;
- index ;
- contraintes ;
- suppressions ;
- renommages ;
- transformations ;
- extensions ;
- fonctions ;
- grants ;
- default privileges.

---

## 52.1 `DROP`

Une suppression de colonne, table ou donnée nécessite une justification explicite.

---

## 52.2 Rename

Un renommage direct peut être incompatible avec l’ancienne release.

---

# 53. Expand-contract

Pour réduire l’interruption, une évolution peut suivre :

```text
expand

déploiement compatible

migration ou backfill

validation

contract dans une release ultérieure
```

---

## 53.1 Expand

- ajouter une colonne nullable ;
- ajouter une table ;
- ajouter un index ;
- accepter ancien et nouveau format.

---

## 53.2 Contract

- supprimer l’ancien champ ;
- rendre une contrainte stricte ;
- retirer un handler ;
- retirer une compatibilité.

---

## 53.3 Baseline

Le profil MVP peut toujours utiliser une maintenance courte même avec une migration additive.

L’expand-contract prépare surtout les évolutions futures.

---

# 54. Migrations transactionnelles

Une migration s’exécute dans une transaction lorsque PostgreSQL le permet.

---

## 54.1 Non transactionnel

Une migration non transactionnelle doit :

- être identifiée ;
- posséder des checkpoints ;
- définir son comportement après interruption ;
- posséder un plan de reprise.

---

# 55. Timeouts de migration

Le migrator utilise des valeurs explicites :

```text
lock_timeout

statement_timeout

idle_in_transaction_session_timeout
```

---

## 55.1 Pas d’illimité implicite

Une migration longue peut augmenter ses limites dans son profil, mais pas utiliser une absence de limite non documentée.

---

# 56. État de migration

Avant l’application :

```text
migration-planned
```

Pendant :

```text
migration-running
```

Après succès :

```text
stable
```

Après échec :

```text
migration-failed
```

---

## 56.1 Runtime

Le web et le worker refusent le mode normal tant que l’état n’est pas `stable`.

---

# 57. Application de la migration

Le migrator :

1. vérifie la cible ;
2. vérifie le rôle ;
3. acquiert le verrou ;
4. vérifie le plan et le hash ;
5. marque l’état ;
6. applique les migrations ;
7. vérifie owners et ACL ;
8. met à jour Schema State ;
9. produit un rapport ;
10. libère le verrou.

---

# 58. Échec de migration

En cas d’échec :

```text
mode maintenance conservé

web et worker non démarrés en mode normal

état migration-failed

rapport produit

audit produit ou réconcilié

décision humaine requise
```

---

## 58.1 Pas de rollback SQL automatique

L’outil ne lance pas automatiquement toutes les migrations descendantes.

---

## 58.2 Choix

- reprendre la migration ;
- appliquer une correction forward ;
- restaurer ;
- exécuter une procédure spécifique validée.

---

# 59. Déploiement des images

Les images sont tirées par digest.

---

## 59.1 Vérification locale

Avant démarrage :

- digest ;
- signature ;
- provenance ;
- architecture ;
- manifest.

---

## 59.2 Échec de pull

Le mode maintenance reste actif.

---

## 59.3 Anciennes images

Les anciens digests ne sont pas supprimés pendant la fenêtre de rollback.

---

# 60. Mise à jour de la configuration

La configuration cible est validée avant activation.

---

## 60.1 Snapshot logique

Le déploiement conserve :

```text
SourceConfigurationHash

TargetConfigurationHash
```

et les références aux versions de secrets nécessaires.

---

## 60.2 Valeurs secrètes

Elles ne sont pas copiées dans le plan.

---

## 60.3 Rollback

La configuration précédente doit rester disponible ou reconstructible pendant la fenêtre de rollback.

---

# 61. Secrets et rollback

Une rotation effectuée avec le déploiement doit indiquer :

- compatibilité ancienne release ;
- coexistence éventuelle ;
- possibilité de restauration ;
- date de révocation.

---

## 61.1 Révocation trop précoce

Un ancien secret nécessaire au rollback ne doit pas être révoqué avant la fin de la fenêtre prévue, sauf incident de sécurité.

---

## 61.2 Compromission

La sécurité du secret prévaut sur la facilité de rollback.

---

# 62. Ordre de démarrage de la nouvelle release

Ordre recommandé :

```text
PostgreSQL

web cible en mode maintenance interne

startup validation web

worker cible en claims-disabled

startup validation worker

smoke tests internes

activation runtime normal

validation complémentaire

retrait maintenance edge
```

---

## 62.1 Worker

Le worker ne revendique pas de jobs tant que l’opération n’a pas autorisé la reprise.

---

## 62.2 Caddy

Caddy continue à servir la maintenance pendant la validation interne.

---

# 63. Startup validation

Le web et le worker doivent satisfaire l’ADR-0031.

---

## 63.1 Web non-ready

Le trafic public ne bascule pas vers le web.

---

## 63.2 Worker non-ready

Les jobs restent suspendus.

---

## 63.3 Blocage

Toute incompatibilité bloquante maintient la maintenance.

---

# 64. Smoke tests

Le plan déclare un profil de smoke tests.

Profils conceptuels :

```text
smoke.public-read.v1

smoke.admin-security.v1

smoke.worker-readiness.v1

smoke.full-release.v1
```

---

## 64.1 Public read

- page d’accueil ;
- contenu principal ;
- média public ;
- headers de sécurité ;
- canonical ;
- JSON-LD ;
- statut attendu.

---

## 64.2 Administration

- page de login ;
- `no-store` ;
- CSP ;
- aucun redirect API inattendu ;
- Identity ready ;
- aucune session réelle créée automatiquement.

---

## 64.3 Worker

- readiness ;
- contrats de jobs ;
- claims encore suspendus ;
- accès PostgreSQL ;
- volumes.

---

# 65. Smoke tests d’écriture

Un test d’écriture en production est facultatif et doit utiliser :

- un contrat canary explicite ;
- des données synthétiques ;
- une marque de test ;
- une suppression ou archivage contrôlé ;
- un audit ;
- aucun email ou provider réel non prévu.

---

## 65.1 Baseline

Le smoke test initial est principalement en lecture seule.

---

## 65.2 Formulaire public

Le formulaire réel n’est pas rempli automatiquement sans profil de test dédié.

---

# 66. Données réelles

Les smoke tests ne modifient pas un lead, contenu ou compte existant.

---

# 67. Validation de sécurité après déploiement

Vérifier au minimum :

```text
HTTPS

HSTS

CSP

cookies sécurisés

no-store sensible

rôle PostgreSQL attendu

aucun port PostgreSQL public

digest attendu

aucun mode development

aucune migration pending
```

---

# 68. Activation du runtime normal

Une fois les smoke tests réussis :

- Runtime Mode passe à `normal` ;
- le write freeze est levé ;
- le worker peut reprendre les claims ;
- l’epoch ou les caches sont vérifiés.

---

## 68.1 Edge toujours actif

Le mode edge peut rester actif quelques instants pendant la vérification finale.

---

# 69. Retrait du mode maintenance edge

Le processus :

1. génère la configuration normale ;
2. valide Caddy ;
3. remplace atomiquement le fragment ;
4. recharge Caddy ;
5. vérifie les routes publiques ;
6. confirme le statut.

---

## 69.1 Échec de retrait

Le site reste en maintenance.

---

## 69.2 Pas d’ouverture partielle silencieuse

Le rapport doit indiquer exactement quelles routes sont accessibles.

---

# 70. Stabilization window

Après réouverture, la release passe dans :

```text
stabilizing
```

---

## 70.1 Baseline initiale

```text
15 minutes
```

de surveillance renforcée.

---

## 70.2 Indicateurs

- taux d’erreur ;
- readiness ;
- redémarrages ;
- latence ;
- connexions PostgreSQL ;
- deadlocks ;
- jobs ;
- outbox ;
- erreurs Identity ;
- espace disque ;
- métriques de cache.

---

## 70.3 Clôture

Après la fenêtre et les contrôles :

```text
stabilized
```

---

# 71. Erreur pendant la stabilization window

Une anomalie déclenche :

- maintien du trafic si faible et compris ;
- réactivation de la maintenance ;
- rollback plan ;
- forward fix ;
- incident.

---

## 71.1 Seuils

Les seuils sont définis dans le profil et non improvisés après l’incident.

---

# 72. Critères de succès

Un déploiement est terminé lorsque :

```text
images cibles actives

migration stable

web ready

worker ready

smoke tests réussis

maintenance retirée

métriques stables

rapport produit

audit écrit

verrou libéré
```

---

# 73. Clôture de l’opération

La commande `finalize` :

- vérifie les critères ;
- produit le rapport final ;
- met l’état à `stabilized` ;
- libère les locks ;
- enregistre l’audit ;
- planifie éventuellement le nettoyage futur.

---

# 74. Rapport de déploiement

Nom conceptuel :

```text
deployment-report.json
```

---

## 74.1 Contenu

```text
SchemaVersion

DeploymentOperationId

ClientSlug

Environment

SourceRelease

TargetRelease

ImageDigests

DeploymentClass

PlanHash

BackupId

MigrationOperationId

MigrationResult

MaintenanceStartedAtUtc

MaintenanceEndedAtUtc

WorkerDrainResult

StartupValidationResults

SmokeTestResults

StabilizationResult

RollbackAssessment

StartedAtUtc

CompletedAtUtc

ActorId

ReasonCode

OverallStatus

ErrorCodes
```

---

## 74.2 Données interdites

- secret ;
- password ;
- connection string ;
- token ;
- private key ;
- contenu métier ;
- credential de registry.

---

# 75. Audit du déploiement

Actions recommandées :

```text
deployment.planned

deployment.started

deployment.maintenance.activated

deployment.worker-drain.started

deployment.worker-drain.completed

deployment.backup.verified

deployment.migration.started

deployment.migration.completed

deployment.migration.failed

deployment.image.activated

deployment.smoke-tests.completed

deployment.maintenance.deactivated

deployment.completed

deployment.failed

deployment.aborted

deployment.rollback.started

deployment.rollback.completed
```

---

## 75.1 Audit indisponible

Une opération d’urgence peut produire un rapport sécurisé puis réconcilier l’audit conformément à l’ADR-0019.

---

# 76. Rollback

Le rollback est une nouvelle opération planifiée.

---

## 76.1 Interdiction

L’opérateur ne remplace pas simplement :

```text
target digest
```

par :

```text
previous digest
```

sans compatibilité vérifiée.

---

## 76.2 Nouveau plan

Un :

```text
rollback-plan.json
```

est produit.

---

# 77. Contenu du Rollback Plan

```text
RollbackOperationId

FailedDeploymentOperationId

CurrentRelease

TargetPreviousRelease

CurrentSchema

PreviousReleaseSchemaRange

CurrentConfigurationHash

PreviousConfigurationHash

ActiveJobContracts

ActiveOutboxContracts

MediaLayoutCompatibility

CacheCompatibility

RequiredMaintenance

RequiredForwardMigration

RestoreRequired

BlockingIssues

EstimatedDowntime
```

---

# 78. Rollback avant migration

Si l’échec survient avant toute mutation persistante :

- arrêt du déploiement ;
- restauration de la configuration normale ;
- redémarrage éventuel des services source ;
- levée du write freeze ;
- retrait de la maintenance ;
- rapport.

---

## 78.1 Réversibilité

```text
élevée
```

---

# 79. Rollback application only

Si aucun changement persistant incompatible n’a eu lieu :

```text
mode maintenance

arrêt des composants cibles

redéploiement des anciens digests

restauration de la configuration compatible

startup validation

smoke tests

réouverture
```

---

# 80. Rollback après migration additive

Un rollback applicatif est permis uniquement si :

```text
current schema
∈ supported schema range
of previous release
```

---

## 80.1 Expand-compatible

L’ancienne release peut ignorer les nouvelles structures si cela a été testé.

---

## 80.2 Jobs

Les nouveaux Job Types doivent être drainés, migrés ou compris par l’ancienne release.

---

# 81. Rollback après migration exclusive

Si la migration rend l’ancienne release incompatible :

```text
automatic application rollback blocked
```

---

## 81.1 Choix

- correction forward ;
- migration corrective ;
- restore contrôlé ;
- nouvelle release intermédiaire.

---

## 81.2 Maintenance

Le site reste en maintenance tant qu’aucun état compatible n’est atteint.

---

# 82. Migration descendante

Une down migration n’est pas la stratégie automatique.

---

## 82.1 Raisons

Elle peut :

- supprimer des données ;
- reconstruire incorrectement un état ;
- échouer ;
- être non testée ;
- ignorer des effets externes.

---

## 82.2 Utilisation

Une migration descendante nécessite :

- artefact revu ;
- plan ;
- backup ;
- tests ;
- confirmation ;
- rapport.

---

# 83. Forward fix

Le forward fix est privilégié lorsque :

- les données ont déjà adopté le nouveau format ;
- la migration est irréversible ;
- l’ancienne release est incompatible ;
- une correction ciblée est plus sûre qu’un restore.

---

## 83.1 Release

Le forward fix est une nouvelle release signée et testée.

---

## 83.2 Urgence

L’urgence ne justifie pas une modification manuelle du conteneur.

---

# 84. Restore comme dernier recours

Un restore n’est pas un rollback applicatif ordinaire.

---

## 84.1 Effets

Il peut perdre :

- transactions postérieures au backup ;
- nouveaux leads ;
- opérations ;
- jobs ;
- médias ;
- changements administratifs.

---

## 84.2 Procédure

Il suit :

- l’ADR-0023 ;
- l’ADR-0031 ;
- les runbooks de backup et restore.

---

# 85. Restore et maintenance

Le mode maintenance edge reste actif pendant :

- restauration ;
- nouveaux epochs ;
- ACL ;
- privacy reconciliation ;
- cache invalidation ;
- startup validation ;
- smoke tests.

---

# 86. Restore et epochs

Après restore, créer ou renouveler :

```text
Audit Epoch

Session Epoch

Recovery Epoch

Global Cache Epoch
```

---

## 86.1 Sessions

Toutes les anciennes sessions sont invalidées.

---

## 86.2 Tokens

Les anciens tokens de provisioning et récupération sont invalidés.

---

# 87. Restore et Deletion Ledger

Les suppressions postérieures au backup sont réappliquées avant réouverture.

---

## 87.1 Blocage

L’absence du ledger requis bloque le mode normal.

---

# 88. Rollback des médias

Une modification de layout média doit déclarer :

- compatibilité ancienne release ;
- possibilité de lecture des deux layouts ;
- backfill ;
- suppression différée ;
- stratégie de restore.

---

## 88.1 Contract différé

L’ancien layout n’est supprimé qu’après la fin de la fenêtre de rollback.

---

# 89. Rollback de configuration

Une ancienne release peut ne pas comprendre la nouvelle configuration.

---

## 89.1 Snapshot

Les hashes et artefacts non sensibles des configurations source et cible sont conservés.

---

## 89.2 Secret references

Les références anciennes restent disponibles si leur maintien n’affaiblit pas la sécurité.

---

## 89.3 Incompatibilité

Le rollback est bloqué si aucune configuration compatible ne peut être reconstruite.

---

# 90. Rollback des jobs

Avant retour à une ancienne release :

- inventorier les jobs ;
- inventorier l’outbox ;
- inventorier les Operations ;
- vérifier les versions ;
- drainer ou migrer les contrats nouveaux.

---

## 90.1 Job inconnu

L’ancienne release ne doit jamais revendiquer un payload inconnu.

---

# 91. Rollback de cache

Le cache mémoire est vidé au redémarrage.

---

## 91.1 Global Cache Epoch

Il peut être incrémenté.

---

## 91.2 Cache distribué futur

Un namespace compatible ou une purge explicite est requis.

---

# 92. Rollback de CSP et API

Le rollback doit vérifier :

- Browser Security Profile ;
- assets statiques ;
- OpenAPI ;
- clients internes ;
- configuration Caddy ;
- Cache Schema Version.

---

# 93. Rollback automatique

Le MVP n’effectue pas de rollback production entièrement automatique.

---

## 93.1 Raisons

Un health check échoué ne permet pas toujours de savoir si :

- la migration a réussi ;
- des jobs ont été créés ;
- des données ont changé ;
- l’ancienne release est compatible.

---

## 93.2 Automatisation future

Un rollback automatique pourra être autorisé uniquement pour une classe :

```text
application-only
```

sans mutation persistante et avec preuves de compatibilité.

---

# 94. Abort

`deployment abort` est autorisé uniquement lorsque l’état courant permet une interruption sûre.

---

## 94.1 Avant migration

L’abort peut revenir à l’état source.

---

## 94.2 Pendant migration

L’abort ne tue pas arbitrairement le processus migrator.

Il suit la procédure du plan.

---

## 94.3 Après migration

L’opération devient :

```text
rollback-required
```

ou :

```text
forward-fix-required
```

---

# 95. Reprise après interruption de l’orchestrateur

Une interruption peut survenir à chaque phase.

---

## 95.1 État persistant

Le nouvel opérateur consulte :

```text
deployment status
```

---

## 95.2 Détection

Le système vérifie :

- lock hôte ;
- processus migrator ;
- état PostgreSQL ;
- Caddy ;
- services ;
- images ;
- rapports ;
- Runtime Mode.

---

## 95.3 Pas de réexécution aveugle

Une phase déjà terminée n’est pas rejouée sans idempotence prouvée.

---

# 96. Idempotence des étapes

Chaque étape déclare :

```text
idempotent

retriable

manual-reconciliation-required
```

---

## 96.1 Exemples idempotents

- vérifier signature ;
- valider Caddy ;
- vérifier backup ;
- tirer une image par digest ;
- lire la readiness.

---

## 96.2 Exemples sensibles

- migration ;
- transformation de données ;
- retrait de colonne ;
- restore ;
- rotation de secret.

---

# 97. Journal opérateur local

Le VPS conserve des rapports non sensibles dans un répertoire protégé.

Exemple :

```text
/var/lib/sitefactory/deployments/{DeploymentOperationId}/
```

---

## 97.1 Contenu

- plan ;
- hashes ;
- résultats ;
- timestamps ;
- Error Codes ;
- rapports Caddy ;
- rapports migrator ;
- smoke tests.

---

## 97.2 Permissions

Répertoire accessible uniquement aux opérateurs autorisés.

---

## 97.3 Secrets

Aucun `.env`, mot de passe ou token.

---

# 98. Caddy comme composant indépendant

Le déploiement de l’application ne redémarre pas Caddy sans besoin.

---

## 98.1 Reload

Privilégier un reload validé.

---

## 98.2 Échec applicatif

Caddy continue à servir la page de maintenance.

---

## 98.3 Certificats

Les données ACME ne sont pas recréées à chaque release.

---

# 99. Mises à jour Caddy

Une modification de l’image ou configuration Caddy constitue une sous-opération explicitement planifiée.

---

## 99.1 Validation

- digest ;
- signature ;
- configuration ;
- TLS ;
- headers ;
- maintenance fallback.

---

# 100. Mise à jour PostgreSQL

Une mise à jour majeure du serveur PostgreSQL n’appartient pas à un déploiement applicatif ordinaire.

---

## 100.1 Profil distinct

Elle exige :

- plan Infrastructure ;
- compatibilité ;
- backup ;
- restore test ;
- maintenance ;
- rollback ou restore.

---

# 101. Mise à jour Docker Compose

Une évolution Compose est versionnée et comparée.

---

## 101.1 Changements sensibles

- réseau ;
- volume ;
- secret ;
- utilisateur ;
- capabilities ;
- read-only ;
- ports ;
- image ;
- health check.

---

## 101.2 Quality gate

Un affaiblissement du durcissement bloque le déploiement.

---

# 102. Maintenance longue

Une maintenance dépassant la durée prévue déclenche :

- alerte ;
- mise à jour opérateur ;
- réévaluation du plan ;
- décision de poursuivre, rollback ou restore.

---

## 102.1 Réouverture pour réduire la durée

Le site ne doit pas être rouvert dans un état incompatible uniquement pour respecter une estimation.

---

# 103. Communication de maintenance

La communication externe au client est une responsabilité opérationnelle.

---

## 103.1 Plan

Une maintenance planifiée peut définir :

- heure ;
- durée estimée ;
- impact ;
- contact ;
- confirmation de fin.

---

## 103.2 Données techniques

La communication ne détaille pas les vulnérabilités ou credentials.

---

# 104. Déploiement d’urgence

Une urgence de sécurité peut réduire certaines étapes, mais ne supprime pas :

- vérification de cible ;
- signature ou provenance ;
- backup lorsque la mutation persiste ;
- maintenance pour migration incompatible ;
- startup validation ;
- rapport ;
- audit.

---

## 104.1 Emergency reason

Le plan utilise un Reason Code spécifique.

---

## 104.2 Revue ultérieure

Une post-review est obligatoire.

---

# 105. Dérogations

Une dérogation doit préciser :

```text
Validation ou gate contourné

raison

impact

contrôles compensatoires

owner

approbateur

expiration

tâche corrective
```

---

## 105.1 Gates non dérogeables

Dans la baseline :

```text
mauvais client

mauvais environnement

image non identifiée

schéma plus récent inconnu

restore non réconcilié

migration partielle

rôle PostgreSQL superuser runtime
```

---

# 106. Observabilité

Événements recommandés :

```text
DeploymentPlanCreated

DeploymentPreflightCompleted

DeploymentLockAcquired

MaintenanceModeActivated

WorkerDrainStarted

WorkerDrainCompleted

DeploymentBackupVerified

DeploymentMigrationStarted

DeploymentMigrationCompleted

DeploymentImageActivated

DeploymentStartupValidationCompleted

DeploymentSmokeTestsCompleted

MaintenanceModeDeactivated

DeploymentStabilizationStarted

DeploymentCompleted

DeploymentFailed

RollbackPlanCreated

RollbackStarted

RollbackCompleted
```

---

## 106.1 Propriétés autorisées

```text
DeploymentOperationId

DeploymentClass

CurrentPhase

SourceVersion

TargetVersion

Result

ErrorCode

Environment

Duration

MaintenanceDuration

WorkerDrainDuration
```

---

## 106.2 Données interdites

```text
secret

connection string

password

registry token

private key

business payload

customer data
```

---

# 107. Métriques

Exemples :

```text
deployment duration

deployment failures

maintenance duration

worker drain duration

migration duration

startup validation duration

smoke test failures

rollback attempts

rollback failures

stabilization incidents
```

---

## 107.1 Labels autorisés

```text
deployment_class

phase

result

error_category

environment
```

---

## 107.2 Labels interdits

```text
DeploymentOperationId

ActorId

ImageDigest complet comme label permanent

BackupId

Client data

TraceId
```

---

# 108. Alertes

Alertes possibles :

```text
Maintenance trop longue

Worker non drainé

Migration échouée

Migrator toujours actif

Web non-ready

Worker non-ready

Smoke test échoué

Erreur 5xx en hausse après ouverture

Jobs en dead-letter après déploiement

Rollback incompatible

Espace disque insuffisant

Lock de déploiement abandonné
```

---

## 108.1 Runbook

Chaque alerte critique référence une procédure.

---

# 109. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-DEPLOY-001` | Plan de déploiement absent |
| `SF-DEPLOY-002` | Plan invalide |
| `SF-DEPLOY-003` | Plan expiré |
| `SF-DEPLOY-004` | Hash du plan incompatible |
| `SF-DEPLOY-005` | Client cible incompatible |
| `SF-DEPLOY-006` | Environnement cible incompatible |
| `SF-DEPLOY-007` | Release cible inconnue |
| `SF-DEPLOY-008` | Digest cible inconnu |
| `SF-DEPLOY-009` | Signature de release invalide |
| `SF-DEPLOY-010` | Provenance de release invalide |
| `SF-DEPLOY-011` | Scan de release bloquant |
| `SF-DEPLOY-012` | Déploiement concurrent détecté |
| `SF-DEPLOY-013` | Verrou de déploiement indisponible |
| `SF-DEPLOY-014` | Espace disque insuffisant |
| `SF-DEPLOY-015` | Configuration cible invalide |
| `SF-DEPLOY-016` | Secret cible absent |
| `SF-DEPLOY-017` | État restore incompatible |
| `SF-DEPLOY-018` | Module Lock incompatible |
| `SF-DEPLOY-019` | Job Contract incompatible |
| `SF-DEPLOY-020` | Opération active incompatible |
| `SF-PREFLIGHT-001` | Preflight absent |
| `SF-PREFLIGHT-002` | Preflight échoué |
| `SF-PREFLIGHT-003` | Preflight trop ancien |
| `SF-PREFLIGHT-004` | Rollback Assessment absent |
| `SF-PREFLIGHT-005` | Rollback inconnu |
| `SF-MAINTENANCE-001` | Mode maintenance impossible à activer |
| `SF-MAINTENANCE-002` | Configuration Caddy invalide |
| `SF-MAINTENANCE-003` | Réponse maintenance invalide |
| `SF-MAINTENANCE-004` | Write freeze non activé |
| `SF-MAINTENANCE-005` | Mode maintenance déjà actif |
| `SF-MAINTENANCE-006` | Mode maintenance d’une autre opération |
| `SF-MAINTENANCE-007` | Retrait de maintenance refusé |
| `SF-MAINTENANCE-008` | Maintenance dépassant la durée prévue |
| `SF-MAINTENANCE-009` | Bypass public détecté |
| `SF-DRAIN-001` | Worker drain impossible |
| `SF-DRAIN-002` | Worker continue à revendiquer des jobs |
| `SF-DRAIN-003` | Deadline de drainage dépassée |
| `SF-DRAIN-004` | Job non interruptible actif |
| `SF-DRAIN-005` | Effet externe incertain |
| `SF-DRAIN-006` | Lease actif non réconcilié |
| `SF-BACKUP-DEPLOY-001` | Backup pré-déploiement requis |
| `SF-BACKUP-DEPLOY-002` | Backup absent |
| `SF-BACKUP-DEPLOY-003` | Backup trop ancien |
| `SF-BACKUP-DEPLOY-004` | Checksum de backup invalide |
| `SF-BACKUP-DEPLOY-005` | Backup d’un autre environnement |
| `SF-BACKUP-DEPLOY-006` | Restore drill trop ancien |
| `SF-MIGRATION-DEPLOY-001` | Plan de migration absent |
| `SF-MIGRATION-DEPLOY-002` | Hash de migration incompatible |
| `SF-MIGRATION-DEPLOY-003` | Verrou de migration indisponible |
| `SF-MIGRATION-DEPLOY-004` | Mauvaise base cible |
| `SF-MIGRATION-DEPLOY-005` | Mauvais rôle migrator |
| `SF-MIGRATION-DEPLOY-006` | Migration déjà en cours |
| `SF-MIGRATION-DEPLOY-007` | Migration échouée |
| `SF-MIGRATION-DEPLOY-008` | Migration partiellement appliquée |
| `SF-MIGRATION-DEPLOY-009` | Migration non transactionnelle non planifiée |
| `SF-MIGRATION-DEPLOY-010` | ACL post-migration invalides |
| `SF-ROLLOUT-001` | Image impossible à tirer |
| `SF-ROLLOUT-002` | Digest local incompatible |
| `SF-ROLLOUT-003` | Web cible non-ready |
| `SF-ROLLOUT-004` | Worker cible non-ready |
| `SF-ROLLOUT-005` | Startup validation échouée |
| `SF-ROLLOUT-006` | Runtime Mode incompatible |
| `SF-ROLLOUT-007` | Reprise du worker refusée |
| `SF-SMOKE-001` | Profil de smoke test inconnu |
| `SF-SMOKE-002` | Smoke test public échoué |
| `SF-SMOKE-003` | Smoke test Identity échoué |
| `SF-SMOKE-004` | Smoke test worker échoué |
| `SF-SMOKE-005` | Header de sécurité absent |
| `SF-SMOKE-006` | Mutation canary non autorisée |
| `SF-STABILIZATION-001` | Fenêtre de stabilisation incomplète |
| `SF-STABILIZATION-002` | Taux d’erreur excessif |
| `SF-STABILIZATION-003` | Redémarrages excessifs |
| `SF-STABILIZATION-004` | Backlog worker anormal |
| `SF-ROLLBACK-001` | Plan de rollback absent |
| `SF-ROLLBACK-002` | Rollback incompatible |
| `SF-ROLLBACK-003` | Ancien digest indisponible |
| `SF-ROLLBACK-004` | Ancienne configuration indisponible |
| `SF-ROLLBACK-005` | Schéma incompatible avec l’ancienne release |
| `SF-ROLLBACK-006` | Job incompatible avec l’ancienne release |
| `SF-ROLLBACK-007` | Migration corrective requise |
| `SF-ROLLBACK-008` | Restore requis |
| `SF-ROLLBACK-009` | Rollback échoué |
| `SF-ROLLBACK-010` | Down migration automatique interdite |
| `SF-DEPLOY-REPORT-001` | Rapport de déploiement absent |
| `SF-DEPLOY-REPORT-002` | Rapport contenant une donnée interdite |
| `SF-DEPLOY-REPORT-003` | Audit de déploiement non réconcilié |

---

# 110. Tests unitaires

Tester notamment :

- classification des releases ;
- phases ;
- transitions ;
- validité du plan ;
- Rollback Assessment ;
- locks ;
- policies de maintenance ;
- état du worker ;
- critères de clôture ;
- Error Codes.

---

# 111. Tests du Deployment Plan

Couvrir :

```text
release application-only

migration additive

migration exclusive

transformation risquée

plan expiré

digest modifié

configuration modifiée

job incompatible

restore pending

backup requis
```

---

# 112. Tests du verrou

Exécuter deux opérations concurrentes.

Résultat :

```text
une seule obtient le verrou

la seconde échoue sans modifier l’environnement
```

---

## 112.1 Processus interrompu

Vérifier :

- libération du `flock` ;
- libération de l’advisory lock ;
- état d’opération persistant ;
- reprise contrôlée.

---

# 113. Tests du mode maintenance edge

Vérifier :

- activation ;
- `503` ;
- `Retry-After` ;
- `no-store` ;
- CSP ;
- absence de scripts tiers ;
- application arrêtée ;
- PostgreSQL arrêté ;
- retrait ;
- configuration Caddy invalide.

---

# 114. Tests d’absence de bypass

Tester :

- query string ;
- cookie ;
- header arbitraire ;
- URL cachée ;
- ancienne route admin.

Aucun accès public au métier ne doit être possible.

---

# 115. Tests du write freeze

Vérifier pendant le freeze :

- GET public selon le profil ;
- POST public refusé ;
- mutation admin refusée ;
- export refusé ;
- création de job refusée ;
- circuit Blazor existant refusé.

---

# 116. Tests de drainage du worker

Couvrir :

```text
aucun job actif

job court

job long interruptible

job non interruptible

job avec effet externe incertain

deadline dépassée

lease expiré

worker crash
```

---

# 117. Tests de backup pré-déploiement

Vérifier :

- bon client ;
- bon environnement ;
- âge ;
- checksum ;
- chiffrement ;
- média inclus ;
- backup incomplet ;
- restore drill expiré.

---

# 118. Tests du migrator

Couvrir :

- bon plan ;
- mauvais hash ;
- mauvais rôle ;
- mauvaise base ;
- lock absent ;
- migration transactionnelle ;
- migration non transactionnelle ;
- échec ;
- ACL ;
- Schema State.

---

# 119. Tests de déploiement des images

Vérifier :

- digest staging égal à production ;
- signature ;
- provenance ;
- pull échoué ;
- architecture incorrecte ;
- ancien digest conservé ;
- tag `latest` refusé.

---

# 120. Tests de startup validation

Après déploiement, simuler :

- mauvais rôle ;
- schéma incompatible ;
- secret absent ;
- App_Data inaccessible ;
- handler de job manquant ;
- restore pending.

Le mode maintenance doit rester actif.

---

# 121. Tests des smoke tests

Couvrir :

- homepage ;
- média ;
- headers ;
- login ;
- API Problem Details ;
- worker ;
- échec d’une route ;
- timeout ;
- donnée réelle non modifiée.

---

# 122. Tests de réouverture

Vérifier :

- Runtime Mode normal ;
- worker autorisé ;
- Caddy validé ;
- maintenance retirée ;
- première requête publique ;
- première requête administrative ;
- cache froid ;
- aucune ancienne configuration.

---

# 123. Tests de stabilization window

Simuler :

- 5xx en hausse ;
- worker backlog ;
- OOM ;
- restart loop ;
- deadlocks ;
- timeout PostgreSQL ;
- health failure.

La release ne doit pas être finalisée comme stable.

---

# 124. Tests de rollback application only

Scénario :

1. déployer une release sans migration ;
2. provoquer un échec ;
3. générer un plan ;
4. redéployer l’ancien digest ;
5. valider ;
6. rouvrir.

---

# 125. Tests de rollback avec schéma compatible

Déployer une migration additive compatible puis revenir à l’ancienne image.

Vérifier :

- startup compatibility ;
- jobs ;
- configuration ;
- données ;
- ACL.

---

# 126. Tests de rollback incompatible

Déployer une migration exclusive.

Tenter un rollback applicatif simple.

Résultat :

```text
rollback refusé
maintenance conservée
forward fix ou restore requis
```

---

# 127. Tests de down migration

Vérifier qu’aucune down migration n’est lancée automatiquement après un échec.

---

# 128. Tests de restore recovery

Couvrir :

- backup source ;
- environnement isolé ;
- nouveaux epochs ;
- Deletion Ledger ;
- Cache Global Epoch ;
- startup validation ;
- réouverture.

---

# 129. Tests de configuration rollback

Vérifier :

- ancienne configuration disponible ;
- schéma de configuration compatible ;
- secret ancien encore valide ;
- secret révoqué ;
- incompatibilité bloquante.

---

# 130. Tests de jobs rollback

Créer un Job Type nouveau puis tenter un retour vers une release qui ne le reconnaît pas.

Le rollback doit être bloqué ou exiger son drainage.

---

# 131. Tests d’interruption de phase

Interrompre l’orchestrateur pendant :

- maintenance activation ;
- drainage ;
- backup ;
- migration ;
- pull ;
- startup ;
- smoke tests ;
- opening.

Vérifier la reprise et l’absence de réouverture automatique.

---

# 132. Tests de non-divulgation

Injecter des marqueurs représentant :

- password ;
- connection string ;
- registry token ;
- private key ;
- email ;
- contenu métier.

Vérifier leur absence dans :

- plans ;
- rapports ;
- logs ;
- métriques ;
- audit ;
- page de maintenance.

---

# 133. Tests multiplateformes

Les contrats, plans et validateurs sont testés sur :

```text
Windows x64

Linux x64
```

Les opérations Docker Compose, Caddy, filesystem, signaux et migrations de production sont validées sur Linux.

---

# 134. Quality gates

Une contribution est bloquée lorsque :

```text
Un déploiement peut s’exécuter sans plan.

Le plan ne contient pas de Rollback Assessment.

La production utilise un digest différent de staging.

Un tag latest est utilisé.

Une migration est exécutée au démarrage.

Le worker revendique pendant une phase incompatible.

Le mode maintenance dépend uniquement du web.

La page de maintenance retourne 200.

La page de maintenance charge un tiers.

Une migration exclusive n’active pas la maintenance.

Un backup requis n’est pas vérifié.

Un smoke test échoué permet la réouverture.

Une ancienne release peut être redéployée
sans vérification du schéma.

Une down migration est lancée automatiquement.

Un restore est traité comme un rollback simple.

Les anciens digests sont supprimés avant la fin
de la fenêtre de rollback.

Un rapport contient un secret.
```

---

# 135. Quality gates de production

La production est bloquée lorsque :

```text
La release n’est pas signée.

Le preflight n’est pas réussi.

Le Deployment Plan est expiré.

Le lock ne peut pas être acquis.

La base ne correspond pas au client.

L’état restore n’est pas stable.

Le backup requis est absent.

Le worker ne peut pas être drainé.

La migration cible n’est pas vérifiée.

La configuration Caddy est invalide.

Le web cible n’est pas ready.

Le worker cible n’est pas compatible.

Les smoke tests ne sont pas réussis.

Le rollback est inconnu pour une release risquée.

La procédure opérateur n’est pas disponible.
```

---

# 136. CI/CD

La CI doit exécuter :

```text
génération du release manifest

génération du compatibility manifest

validation du Compose

validation du Caddyfile

tests de maintenance

tests de migration

tests de rollback compatibility

tests de worker drain

tests de startup validation

tests de smoke profiles

tests d’interruption

tests de rapports

tests de non-divulgation
```

---

## 136.1 Release candidate

Une release candidate n’est promue qu’après :

- staging ;
- migration staging ;
- smoke tests ;
- rollback assessment ;
- artefacts signés.

---

## 136.2 Production

L’application en production est une action explicite avec environnement protégé.

---

# 137. Analyse statique

La CI peut détecter :

```text
Database.Migrate dans le runtime

image: latest

docker compose up sans digest

ports PostgreSQL publics

commande de migration dans web entrypoint

suppression automatique de volume

down migration automatique

caddy reload sans validate

bypass maintenance

worker démarré avant validation
```

---

# 138. Sécurité

## 138.1 Actifs

- données ;
- base ;
- médias ;
- images ;
- registry ;
- configuration ;
- secrets ;
- jobs ;
- audit ;
- availability ;
- backups.

---

## 138.2 Menaces

- mauvaise release ;
- mauvais client ;
- migration destructive ;
- rollback incompatible ;
- déploiement concurrent ;
- image substituée ;
- backup inutilisable ;
- worker concurrent ;
- réouverture prématurée ;
- maintenance bypass ;
- restore incomplet ;
- rapport divulguant un secret.

---

## 138.3 Contrôles

- plan ;
- lock ;
- signatures ;
- maintenance ;
- drainage ;
- backups ;
- migrations explicites ;
- startup validation ;
- smoke tests ;
- rollback assessment ;
- audit ;
- rapports ;
- quality gates.

---

# 139. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Deux opérateurs déploient simultanément | Deployment lock |
| Mauvaise image activée | Digest, signature et manifest |
| Ancien worker sur nouveau schéma | Maintenance et drainage |
| Migration pendant le trafic | Write freeze et Caddy maintenance |
| Backup absent | Preflight et Backup ID |
| Application non-ready exposée | Caddy maintenu en maintenance |
| Retour vers un binaire incompatible | Rollback Assessment |
| Down migration destructive | Interdiction automatique |
| Restore réintroduisant des suppressions | Deletion Ledger reconciliation |
| Déploiement interrompu réouvrant le site | Maintenance fail-safe |
| Secret dans le rapport | Redaction et tests |
| Bypass par URL cachée | Maintenance edge sans bypass public |

---

# 140. Règles pour les modules

Chaque module modifiant le runtime ou les données doit déclarer :

```text
DeploymentImpact

SchemaCompatibility

MigrationIds

JobCompatibility

OutboxCompatibility

CacheCompatibility

MediaLayoutCompatibility

ConfigurationCompatibility

RollbackCompatibility

RequiredSmokeTests
```

---

## 140.1 Module sans déclaration

Une release contenant un module persistant sans métadonnées de déploiement est bloquée.

---

## 140.2 Interdictions

Un module ne doit pas :

- appliquer sa migration au startup ;
- démarrer un job avant readiness ;
- supprimer immédiatement un ancien format ;
- rendre son rollback implicitement compatible ;
- contourner le mode maintenance.

---

# 141. Règles pour les customizations

Une customization suit les mêmes contrats.

---

## 141.1 Migration client

Elle doit fournir :

- plan ;
- durée ;
- locks ;
- backup ;
- compatibilité ;
- smoke tests ;
- rollback.

---

## 141.2 Script externe

Un script de déploiement spécifique est :

- versionné ;
- hashé ;
- testé ;
- sans secret ;
- intégré au plan.

---

# 142. Règles pour LogicGate

LogicGate doit :

- construire une seule fois ;
- valider en staging ;
- générer le plan ;
- vérifier le backup ;
- activer la maintenance ;
- drainer les jobs ;
- appliquer les migrations ;
- vérifier les services ;
- surveiller ;
- documenter le résultat ;
- conserver les anciens digests ;
- préparer le rollback.

---

## 142.1 LogicGate ne doit pas

- déployer directement depuis le poste local ;
- utiliser `latest` ;
- reconstruire pour production ;
- migrer depuis le web ;
- retirer la maintenance après un simple démarrage du processus ;
- tuer un worker sans analyser ses jobs ;
- promettre un rollback non testé ;
- modifier manuellement une DLL dans un conteneur ;
- restaurer sans Privacy Reconciliation.

---

# 143. Règles pour Codex

Codex peut :

- générer les plans ;
- créer l’orchestrateur ;
- créer les scripts ;
- ajouter les tests ;
- analyser un rollback ;
- produire les manifests ;
- documenter les runbooks.

---

## 143.1 Codex ne doit pas

```text
Déployer en production

Activer une maintenance réelle

Appliquer une migration réelle

Arrêter un service réel

Lancer un rollback réel

Restaurer un backup réel

Inventer un credential

Publier une image

Contourner une confirmation

Marquer une release comme stable sans preuves
```

---

## 143.2 Décision humaine

Une décision humaine est requise pour :

- apply production ;
- migration exclusive ;
- transformation risquée ;
- prolongation de maintenance ;
- interruption forcée d’un job ;
- dérogation ;
- rollback ;
- down migration ;
- restore ;
- réouverture après incident.

---

# 144. Anti-patterns interdits

## 144.1 Déploiement direct

```text
git pull
docker compose up -d --build
```

sur le VPS de production.

---

## 144.2 Build production sur le VPS

La production compile le repository.

---

## 144.3 `latest`

Le Compose ne sait pas quel artefact est réellement déployé.

---

## 144.4 Auto-migration

Le web modifie la base pendant son startup.

---

## 144.5 Maintenance applicative uniquement

L’application est arrêtée et Caddy retourne une erreur proxy générique.

---

## 144.6 Maintenance 200

Une page d’indisponibilité retourne `200 OK`.

---

## 144.7 Bypass secret dans l’URL

Un paramètre public contourne la maintenance.

---

## 144.8 Kill worker immédiat

Un job externe reste dans un état incertain.

---

## 144.9 Rollback image uniquement

L’ancien digest est relancé sans vérifier le schéma.

---

## 144.10 Down migration automatique

Chaque migration est annulée en ordre inverse après un échec.

---

## 144.11 Restore rapide

Un backup est restauré puis le site est immédiatement rouvert.

---

## 144.12 Nettoyage prématuré

L’ancien digest est supprimé juste après la réouverture.

---

# 145. Conséquences positives

La décision apporte :

- déploiements prévisibles ;
- migrations maîtrisées ;
- protection des jobs ;
- maintenance indépendante ;
- rollbacks évalués ;
- backups intégrés ;
- startup validation obligatoire ;
- réouverture contrôlée ;
- traçabilité ;
- reprise après interruption ;
- meilleure sécurité ;
- handover opérationnel plus clair.

---

# 146. Conséquences négatives

La décision entraîne :

- interruptions planifiées ;
- orchestrateur supplémentaire ;
- page de maintenance ;
- procédures plus longues ;
- stockage de rapports ;
- locks ;
- smoke tests ;
- surveillance renforcée ;
- conservation temporaire de plusieurs images ;
- davantage de responsabilités opérateur.

---

# 147. Risques acceptés

## 147.1 Interruption courte

Le profil VPS initial peut produire quelques minutes d’indisponibilité.

Ce risque est accepté pour protéger les données et éviter une coexistence non maîtrisée.

---

## 147.2 Rollback non disponible

Certaines migrations rendent le rollback applicatif impossible.

Réduction :

- détection avant apply ;
- backup ;
- expand-contract ;
- forward fix ;
- restore planifié.

---

## 147.3 Maintenance prolongée

Une migration peut durer plus longtemps que prévu.

Réduction :

- staging ;
- mesure ;
- plan ;
- alertes ;
- communication ;
- décision de poursuite.

---

## 147.4 Job externe incertain

Un worker interrompu peut avoir exécuté un effet sans confirmation.

Réduction :

- idempotence ;
- delivery records ;
- réconciliation ;
- drainage.

---

# 148. Mesures compensatoires

- plan ;
- Rollback Assessment ;
- deployment lock ;
- maintenance edge ;
- write freeze ;
- drainage ;
- backups ;
- migration plan ;
- digests ;
- startup validation ;
- smoke tests ;
- stabilization window ;
- audit ;
- rapports ;
- restore reconciliation ;
- runbooks.

---

# 149. Performance

## 149.1 Déploiement

Le téléchargement des images peut être effectué avant l’activation de la maintenance lorsque cela ne modifie pas le runtime.

---

## 149.2 Migration

Les opérations longues sont mesurées en staging.

---

## 149.3 Maintenance

Les étapes préparatoires non mutantes sont exécutées avant la fenêtre.

---

## 149.4 Images

Les anciens digests restent locaux pour accélérer un rollback compatible.

---

# 150. Coûts

## 150.1 Coûts supplémentaires

- développement de l’orchestrateur ;
- CI ;
- stockage d’images ;
- backups ;
- maintenance ;
- tests ;
- observabilité ;
- temps opérateur.

---

## 150.2 Coûts évités

- corruption de données ;
- mauvaise migration ;
- rollback improvisé ;
- perte de jobs ;
- réouverture prématurée ;
- incident sans trace ;
- mauvais environnement modifié ;
- restore dangereux.

---

# 151. Maintenabilité

Pour chaque release, il doit être possible de répondre :

```text
Quel digest est déployé ?

Quel digest était déployé avant ?

Quelle classe de déploiement ?

Quelle migration ?

Quel backup ?

Quelle durée de maintenance ?

Quels jobs ont été drainés ?

Quels smoke tests ont réussi ?

Le rollback est-il compatible ?

Quelle configuration est active ?

Quel rapport et quel audit prouvent l’opération ?
```

---

# 152. Compatibilité

## 152.1 Deployment Plan Schema

Le plan possède une version.

---

## 152.2 Ancien orchestrateur

Un orchestrateur ancien ne doit pas appliquer un plan d’une version future inconnue.

---

## 152.3 Docker Compose

Le profil de déploiement possède une version.

---

## 152.4 Migrations

La compatibilité est déclarée par release.

---

## 152.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

L’adoption dans un projet existant nécessite un premier inventaire et une baseline de release.

---

# 153. Migration depuis des déploiements manuels

Le processus est :

```text
identifier le digest actuel

identifier le schéma actuel

identifier les rôles et secrets

identifier les jobs actifs

produire les manifests

créer la page maintenance

créer le Deployment Plan initial

tester en staging

tester le rollback application-only

activer l’orchestrateur
```

---

# 154. Migration depuis les builds VPS

Le processus est :

```text
déplacer le build vers la CI

publier les images signées

créer le registry

déployer par digest

retirer le repository du VPS si inutile

retirer le SDK du runtime

documenter les artefacts
```

---

# 155. Migration depuis les auto-migrations

Le processus est :

```text
retirer Database.Migrate

créer l’image migrator

créer les Migration Plans

ajouter Schema State

ajouter le mode maintenance

tester les échecs

tester le rollback
```

---

# 156. Transfert client

Le handover doit inclure :

- orchestrateur ;
- Deployment Plan Schema ;
- Rollback Plan Schema ;
- manifests ;
- digests ;
- images ou procédure de rebuild ;
- page de maintenance ;
- configuration Caddy ;
- procédures de backup ;
- migrations ;
- smoke tests ;
- permissions ;
- rapports ;
- runbooks.

---

## 156.1 Registry

Le client doit pouvoir :

- accéder aux images transférées ;
- ou reconstruire les images depuis le repository.

---

## 156.2 Secrets

Les credentials LogicGate ne sont pas transférés lorsqu’ils couvrent d’autres clients.

---

# 157. Clôture de service

La clôture doit :

```text
produire un dernier rapport de release

transférer les digests

transférer les manifests

transférer les runbooks

vérifier le rollback ou restore

transférer la page maintenance

révoquer les credentials LogicGate

retirer les images selon la rétention

auditer le handover
```

---

# 158. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de déployer ;
- d’activer la maintenance ;
- d’arrêter les workers ;
- d’appliquer une migration ;
- de produire ou restaurer un backup ;
- d’exécuter un rollback ;
- de modifier Caddy ;
- de rouvrir un site.

---

# 159. Réversibilité

## 159.1 Classification

```text
élevée avant migration

élevée pour une release application-only

moyenne après migration additive

faible après migration exclusive

très faible après transformation irréversible
```

---

## 159.2 Plan

Le niveau de réversibilité apparaît avant apply.

---

## 159.3 Promesse

Un déploiement ne doit pas être présenté comme rollbackable lorsque seule une restauration avec perte potentielle est disponible.

---

# 160. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- Deployment Operation ;
- phases ;
- profils ;
- Rollback Assessment ;
- Error Codes.

---

## Étape 2 — Manifests

Créer :

```text
deployment-release-manifest.json

deployment-plan.json

rollback-plan.json

deployment-report.json
```

---

## Étape 3 — Verrouillage

Ajouter :

- lock hôte ;
- opération PostgreSQL ;
- advisory lock migrator ;
- reprise.

---

## Étape 4 — Maintenance

Créer :

- page statique ;
- fragment Caddy ;
- validation ;
- activation atomique ;
- Runtime Mode.

---

## Étape 5 — Drainage

Ajouter :

- worker drain ;
- deadlines ;
- statut des jobs ;
- uncertain effect handling.

---

## Étape 6 — Backups

Intégrer :

- Backup ID ;
- vérification ;
- media snapshot ;
- restore drill freshness.

---

## Étape 7 — Migrations

Intégrer :

- Migration Plan ;
- migrator ;
- Schema State ;
- rapports ;
- échecs.

---

## Étape 8 — Rollout

Ajouter :

- pull par digest ;
- configuration ;
- startup validation ;
- worker claims-disabled ;
- ouverture.

---

## Étape 9 — Smoke et stabilization

Ajouter :

- profils ;
- tests ;
- métriques ;
- seuils ;
- finalization.

---

## Étape 10 — Rollback et restore

Ajouter :

- plan ;
- compatibilité ;
- forward fix ;
- restore recovery ;
- runbooks.

---

# 161. Tâches liées proposées

```text
SF-TASK-476 — Define deployment-operation and rollout contracts

SF-TASK-477 — Define deployment and rollback plan schemas

SF-TASK-478 — Generate signed deployment release manifests

SF-TASK-479 — Add exclusive host deployment locking

SF-TASK-480 — Add persistent deployment-operation tracking

SF-TASK-481 — Add static Caddy maintenance responses

SF-TASK-482 — Add atomic maintenance-mode activation

SF-TASK-483 — Add application write-freeze enforcement

SF-TASK-484 — Add worker-drain orchestration

SF-TASK-485 — Add non-interruptible job deployment checks

SF-TASK-486 — Add pre-deployment backup verification

SF-TASK-487 — Integrate explicit migration plans with rollout

SF-TASK-488 — Add migration failure and resume handling

SF-TASK-489 — Add immutable image rollout by digest

SF-TASK-490 — Add claims-disabled worker startup mode

SF-TASK-491 — Add versioned deployment smoke-test profiles

SF-TASK-492 — Add post-deployment stabilization monitoring

SF-TASK-493 — Add rollback compatibility assessment

SF-TASK-494 — Add application-only rollback orchestration

SF-TASK-495 — Block rollback across incompatible schemas

SF-TASK-496 — Add restore-recovery deployment orchestration

SF-TASK-497 — Add resumable interrupted deployment workflows

SF-TASK-498 — Add sanitized deployment reports and auditing

SF-TASK-499 — Add deployment, maintenance and rollback tests

SF-TASK-500 — Add production deployment and rollback runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 162. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Chaque déploiement possède un plan.

Chaque plan possède un hash.

Chaque release possède un Rollback Assessment.

Le même digest est utilisé en staging et production.

Les images sont vérifiées et signées.

Un seul déploiement peut être actif.

La maintenance edge fonctionne sans le web.

La page de maintenance retourne 503.

La page de maintenance utilise no-store.

Aucun bypass public n’existe.

Le write freeze bloque les mutations existantes.

Le worker cesse de revendiquer de nouveaux jobs.

Les jobs actifs sont drainés ou réconciliés.

Un backup requis est produit et vérifié.

Les migrations utilisent l’image migrator.

Le web et le worker n’appliquent aucune migration.

Une migration échouée conserve la maintenance.

La nouvelle release passe la startup validation.

Les smoke tests réussissent avant l’ouverture.

Le worker reste claims-disabled pendant la validation.

Le mode runtime normal est activé explicitement.

Caddy est validé avant la réouverture.

La stabilization window est observée.

Le rapport final ne contient aucun secret.

Un rollback application-only fonctionne.

Un rollback incompatible avec le schéma est refusé.

Aucune down migration n’est lancée automatiquement.

Un restore déclenche les nouveaux epochs.

Le Deletion Ledger est réconcilié après restore.

Une interruption du processus ne rouvre pas le trafic.

Les anciens digests restent disponibles pendant la fenêtre prévue.

Les tests fonctionnent sur Linux et les contrats sur Windows.
```

---

# 163. Conditions de révision

Réexaminer cette décision si :

- plusieurs instances web deviennent la baseline ;
- Kubernetes est introduit ;
- une stratégie blue-green devient économiquement pertinente ;
- un proxy ou load balancer managé est ajouté ;
- PostgreSQL devient hautement disponible ;
- des déploiements sans interruption deviennent contractuels ;
- plusieurs régions sont utilisées ;
- les migrations deviennent très fréquentes ;
- le nombre de clients nécessite une orchestration centralisée ;
- les workers sont répartis sur plusieurs hôtes ;
- un rollback automatique est demandé ;
- un système GitOps est introduit.

---

# 164. Indicateurs à suivre

- fréquence des déploiements ;
- taux de succès ;
- durée totale ;
- durée de maintenance ;
- durée de drainage ;
- durée de migration ;
- durée de startup ;
- smoke test failures ;
- rollback assessments ;
- rollbacks ;
- forward fixes ;
- restores ;
- stabilization incidents ;
- locks abandonnés ;
- maintenance dépassée ;
- backups pré-déploiement ;
- digests actifs ;
- jobs incompatibles.

---

# 165. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
les déploiements contournent régulièrement le plan

la maintenance dure beaucoup plus longtemps que prévu

les workers ne peuvent pas être drainés

les rollbacks sont souvent incompatibles

des migrations destructives deviennent fréquentes

les backups pré-déploiement échouent

les smoke tests sont trop faibles

les sites sont rouverts avec des warnings critiques

les opérateurs patchent les conteneurs manuellement

les anciens digests ne sont plus disponibles

les restores sont utilisés comme rollback ordinaire

les rapports de déploiement manquent
```

---

# 166. Conséquences observées

Cette section est append-only après mise en œuvre.

## 166.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
de l’orchestrateur de déploiement,
du mode maintenance edge,
du worker drain
et des plans de rollback.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- Deployment Operation ID ;
- classe ;
- durée ;
- maintenance ;
- migration ;
- rollback ;
- incident ou bénéfice ;
- action proposée.

---

# 167. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- [15-cli-specification.md](../03-generation/15-cli-specification.md)
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
- [0005-file-generation-strategies.md](0005-file-generation-strategies.md)
- [0006-feature-flags-and-entitlements-separation.md](0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](0007-module-composition-and-public-contracts.md)
- [0008-private-package-feed-and-supply-chain.md](0008-private-package-feed-and-supply-chain.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](0012-runtime-configuration-and-secrets.md)
- [0013-local-git-initialization-and-explicit-remote-publishing.md](0013-local-git-initialization-and-explicit-remote-publishing.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](0014-explicit-client-upgrades-and-version-pinning.md)
- [0016-content-and-media-storage-boundaries.md](0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](0017-background-jobs-and-reliable-side-effects.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](0019-audit-trail-and-sensitive-administrative-actions.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](0021-administrator-provisioning-invitations-and-account-recovery.md)
- [0022-browser-security-headers-and-content-security-policy.md](0022-browser-security-headers-and-content-security-policy.md)
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0024-client-data-export-and-portability.md](0024-client-data-export-and-portability.md)
- [0025-third-party-integrations-and-egress-control.md](0025-third-party-integrations-and-egress-control.md)
- [0026-container-image-build-and-runtime-hardening.md](0026-container-image-build-and-runtime-hardening.md)
- [0027-database-roles-and-runtime-least-privilege.md](0027-database-roles-and-runtime-least-privilege.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- [0029-http-api-contracts-problem-details-and-idempotent-commands.md](0029-http-api-contracts-problem-details-and-idempotent-commands.md)
- [0030-cache-strategy-and-invalidation.md](0030-cache-strategy-and-invalidation.md)
- [0031-application-startup-validation-and-runtime-compatibility.md](0031-application-startup-validation-and-runtime-compatibility.md)

---

# 168. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 163 |

---

# 169. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 170. Synthèse de la décision

```text
Release
→ image immuable
→ digest signé
→ manifest
→ même artefact staging et production

Plan
→ source
→ cible
→ schéma
→ jobs
→ configuration
→ backup
→ maintenance
→ rollback assessment

Lock
→ flock hôte
→ opération PostgreSQL
→ advisory lock migrator
→ un seul déploiement

Maintenance edge
→ Caddy
→ page statique
→ 503
→ Retry-After
→ no-store
→ aucun bypass public

Maintenance runtime
→ write freeze
→ mutations refusées
→ workers suspendus
→ état persistant

Drainage
→ aucun nouveau claim
→ jobs actifs terminés
→ deadline
→ idempotence
→ effets incertains réconciliés

Backup
→ requis pour migration ou donnée
→ client et environnement vérifiés
→ checksum
→ média selon impact
→ restore drill suffisamment récent

Migration
→ plan hashé
→ image migrator
→ rôle distinct
→ verrou
→ Schema State
→ aucun auto-rollback

Rollout
→ pull par digest
→ web en maintenance interne
→ startup validation
→ worker claims-disabled
→ smoke tests
→ runtime normal
→ retrait Caddy

Smoke tests
→ public
→ sécurité
→ Identity
→ worker
→ aucune donnée réelle modifiée par défaut

Stabilisation
→ 15 minutes par défaut
→ erreurs
→ readiness
→ jobs
→ PostgreSQL
→ ressources

Rollback
→ nouveau plan
→ ancien digest vérifié
→ schéma compatible obligatoire
→ jobs et configuration compatibles
→ aucune down migration automatique

Incompatibilité
→ forward fix
→ migration corrective
→ restore en dernier recours

Restore
→ maintenance conservée
→ nouveaux epochs
→ Deletion Ledger
→ ACL
→ startup validation
→ réouverture contrôlée

Traçabilité
→ Deployment Operation ID
→ rapports
→ métriques
→ audit
→ reprise après interruption
```

> Un rollback sûr n’est pas le simple redémarrage d’une ancienne image. Il exige que cette image puisse encore comprendre le schéma, les jobs, la configuration et les données actuellement présents. SiteFactory évalue donc la réversibilité avant le déploiement, maintient le trafic en maintenance pendant les phases incompatibles et privilégie une correction forward lorsque le retour n’est plus démontré comme sûr.
