---
adrId: ADR-0019
title: Record sensitive administrative actions in a dedicated tamper-evident audit trail
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
  - ./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ./0028-database-transaction-boundaries-and-concurrency-control.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ../03-generation/13-generation-pipeline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../04-quality-security/18-security-baseline.md
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
  - ./0013-local-git-initialization-and-explicit-remote-publishing.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0017-background-jobs-and-reliable-side-effects.md
  - ./0018-public-form-abuse-protection-and-rate-limiting.md
targetVersion: 0.1.0
---

# ADR-0019 — Piste d’audit et actions administratives sensibles

## Portée publique et état d'implémentation

Le statut `accepted` et la date ci-dessous décrivent la décision architecturale
historique, pas un inventaire de fonctions livrées. La distribution publique
ne fournit pas d'administration métier, de schéma d'audit administratif ni de
chaîne d'intégrité opérationnelle démontrés par cet ADR.
Les événements, tables, permissions, exports et parcours décrits sont des modèles
de référence pour les applications et modules qui implémenteront ces capacités.

La distinction entre audit et observabilité, les décisions, restrictions et
preuves attendues sont conservées. Les rapports de génération et les preuves du
harnais public ne sont pas une implémentation de cette piste d'audit.
Les responsabilités d'un opérateur ne rendent pas une distribution privée
nécessaire au fonctionnement du public.

## 1. Statut

```text
accepted
```

Cet ADR distingue explicitement :

```text
logs techniques
traces et métriques
événements de sécurité
piste d’audit
historique métier
rapports opérationnels
```

La piste d’audit possède une responsabilité propre :

```text
enregistrer durablement qui a tenté ou effectué
une action administrative sensible,
sur quelle ressource,
quand,
depuis quel contexte technique,
avec quel résultat
et pour quelle raison
```

Les événements d’audit sont :

- structurés ;
- append-only ;
- versionnés ;
- horodatés en UTC ;
- isolés par client et environnement ;
- protégés par des permissions spécifiques ;
- vérifiables par une chaîne d’intégrité ;
- soumis à une politique de rétention explicite ;
- distincts des logs d’observabilité.

Une action administrative critique ne doit pas être considérée comme réussie si son événement d’audit obligatoire n’a pas pu être enregistré.

---

## 2. Résumé

L’architecture de référence est :

```text
Action administrative
→ authentification
→ autorisation
→ éventuelle confirmation renforcée
→ validation de la raison
→ transaction métier
→ événement d’audit dans la même transaction
→ commit
→ réponse
```

Pour les opérations externes ou longues :

```text
Demande opérateur
→ audit de la demande
→ job ou procédure
→ audit du démarrage
→ exécution
→ audit du résultat
→ rapport opérationnel
```

La piste d’audit est stockée dans PostgreSQL dans un schéma logique dédié.

Exemple conceptuel :

```text
sitefactory_audit.audit_events
sitefactory_audit.audit_chain_state
sitefactory_audit.audit_epochs
```

Les événements sont ajoutés au moyen d’un contrat contrôlé.

Le rôle applicatif ne doit pas pouvoir :

- modifier un événement existant ;
- supprimer directement un événement ;
- réécrire la séquence ;
- désactiver l’audit depuis l’administration fonctionnelle.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Un log technique ne remplace pas un événement d’audit.

Un événement d’audit ne constitue pas une autorisation.

Une interface masquée ne remplace pas le contrôle serveur.

Une action sensible réussie possède un événement d’audit durable.

Un événement d’audit déjà enregistré n’est jamais modifié.

Une correction produit un nouvel événement.

Un secret n’apparaît jamais dans l’audit.

Un mot de passe n’apparaît jamais dans l’audit.

Un token n’apparaît jamais dans l’audit.

Un contenu complet de formulaire n’apparaît jamais dans l’audit.

Un export de la piste d’audit est lui-même audité.

Une opération de rétention ou de purge est elle-même auditée.

Une restauration de backup démarre un nouvel epoch d’audit.

Une rupture de chaîne d’intégrité déclenche un incident.

Un administrateur fonctionnel n’accède pas automatiquement
à l’intégralité de la piste d’audit.
```

---

## 4. Contexte

Les applications consommant SiteFactory peuvent prévoir des opérations dont l’impact dépasse une modification fonctionnelle ordinaire.

Exemples :

- connexion à l’administration ;
- création ou désactivation d’un administrateur ;
- attribution d’un rôle ;
- modification de permissions ;
- export de leads ;
- suppression de données ;
- publication de contenu ;
- téléchargement d’un document privé ;
- activation d’un kill switch ;
- modification d’un feature flag ;
- retry manuel d’un job ;
- migration PostgreSQL ;
- restauration ;
- modification de configuration de production ;
- rotation d’un credential ;
- déploiement ;
- changement de visibilité ;
- transfert d’un projet.

Ces actions doivent pouvoir être reconstituées lors :

- d’un incident ;
- d’une erreur opérateur ;
- d’une enquête de sécurité ;
- d’une contestation ;
- d’un transfert client ;
- d’une revue de maintenance ;
- d’une restauration ;
- d’une analyse de permissions.

Les logs techniques ne suffisent pas.

Ils peuvent être :

- échantillonnés ;
- supprimés rapidement ;
- dupliqués ;
- liés à une implémentation ;
- incomplets ;
- indisponibles après rotation ;
- dépourvus de contexte fonctionnel ;
- envoyés vers un backend externe.

La piste d’audit nécessite donc :

- un contrat stable ;
- une rétention distincte ;
- une intégrité renforcée ;
- des accès plus limités ;
- une sémantique fonctionnelle.

---

## 5. Problème à résoudre

Comment enregistrer les actions administratives sensibles tout en garantissant :

- l’identité de l’acteur ;
- la ressource ciblée ;
- le résultat ;
- la raison ;
- la cohérence avec la transaction ;
- l’absence de secret ;
- la minimisation des données ;
- la détection des modifications ;
- l’isolation client ;
- la consultation contrôlée ;
- une rétention maîtrisée ;
- la continuité après restauration ?

La solution doit également éviter :

- l’utilisation de messages de logs comme preuve ;
- le stockage de snapshots complets ;
- les événements texte non structurés ;
- les mises à jour de lignes d’audit ;
- les suppressions silencieuses ;
- la journalisation excessive de chaque lecture ordinaire ;
- les métriques à forte cardinalité ;
- l’exposition des identités et données personnelles à tous les administrateurs ;
- la dépendance obligatoire à un service distant.

---

## 6. Portée

Cette décision couvre :

- piste d’audit applicative ;
- actions administratives ;
- événements d’authentification sélectionnés ;
- changements Identity ;
- rôles et permissions ;
- exports ;
- suppressions ;
- publications ;
- accès sensibles ;
- feature flags ;
- kill switches ;
- entitlements ;
- jobs opérateur ;
- migrations ;
- sauvegardes ;
- restaurations ;
- déploiements ;
- rotations de secrets ;
- consultation et export de l’audit ;
- intégrité ;
- rétention ;
- backups ;
- transfert client ;
- procédures break-glass.

---

## 7. Hors portée

Cette décision ne définit pas :

- un SIEM obligatoire ;
- une plateforme SOC ;
- une preuve juridique universelle ;
- une signature qualifiée ;
- un stockage WORM externe obligatoire ;
- une conservation réglementaire universelle ;
- l’audit de chaque lecture publique ;
- l’audit détaillé de chaque page vue ;
- un système central multi-client ;
- un système d’archivage légal ;
- une analyse comportementale par AI ;
- l’ensemble des obligations juridiques propres à chaque secteur.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Traçabilité des actions sensibles**
2. **Cohérence avec les transactions métier**
3. **Intégrité des événements**
4. **Absence de secrets**
5. **Minimisation des données**
6. **Isolation client et environnement**
7. **Contrôle des accès**
8. **Reconstitution des incidents**
9. **Continuité après backup et restauration**
10. **Contrats structurés et versionnés**
11. **Requêtes et exports maîtrisés**
12. **Compatibilité avec PostgreSQL**
13. **Faible dépendance fournisseur**
14. **Testabilité**
15. **Rétention configurable**
16. **Transfert client**

---

## 9. Contraintes

La décision doit respecter :

```text
PostgreSQL

Un repository et une base par client

UTC

Événements structurés

Append-only

Aucune modification directe

Aucune suppression directe par l’application

Aucun secret

Aucune PII excessive

Transaction commune lorsque possible

Chaîne d’intégrité

Permissions dédiées

Exports privés et temporaires

Observabilité OpenTelemetry

Migrations production explicites
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le volume d’actions administratives reste modéré ;
- PostgreSQL peut conserver la piste initiale ;
- les actions sensibles sont identifiables avant leur implémentation ;
- la majorité des actions applicatives utilise une transaction PostgreSQL ;
- les opérations externes produisent déjà des rapports ;
- un root ou administrateur PostgreSQL possède techniquement un pouvoir élevé ;
- la chaîne d’intégrité améliore la détection sans prétendre rendre la base inviolable ;
- une rétention différente peut être définie selon le projet ;
- une nouvelle plateforme d’audit pourra être ajoutée ultérieurement.

---

# 11. Définitions

## 11.1 Événement d’audit

Enregistrement structuré d’une tentative ou action sensible.

---

## 11.2 Acteur

Identité ayant initié l’action.

Types possibles :

```text
user

operator

service

system

break-glass
```

---

## 11.3 Acteur effectif

Identité dont les permissions ont été utilisées.

---

## 11.4 Acteur réel

Identité ayant réellement déclenché l’action lorsqu’une délégation ou impersonation existe.

---

## 11.5 Cible

Ressource affectée par l’action.

Exemples :

- utilisateur ;
- rôle ;
- lead ;
- contenu ;
- média ;
- job ;
- configuration ;
- environnement ;
- migration.

---

## 11.6 Action

Verbe stable décrivant l’intention.

Exemples :

```text
identity.role.assigned

lead.export.requested

content.published

operations.restore.started
```

---

## 11.7 Outcome

Résultat de l’action.

Valeurs recommandées :

```text
succeeded

failed

denied

cancelled

partially-completed
```

---

## 11.8 Audit epoch

Période continue de chaîne d’audit.

Un nouvel epoch est créé notamment après une restauration.

---

## 11.9 Sequence Number

Numéro monotone attribué par PostgreSQL dans un epoch.

---

## 11.10 Event hash

Empreinte calculée à partir :

- des données canoniques de l’événement ;
- du hash de l’événement précédent.

---

# 12. Option A — Utiliser uniquement les logs techniques

## 12.1 Description

Les actions administratives sont écrites avec `ILogger`.

---

## 12.2 Avantages

- simple ;
- infrastructure déjà présente ;
- recherche dans le backend ;
- faible duplication de code.

---

## 12.3 Inconvénients

- rétention variable ;
- événements non transactionnels ;
- duplication ;
- structure non garantie ;
- dépendance au backend ;
- suppression fréquente ;
- intégrité insuffisante ;
- difficultés de transfert.

---

## 12.4 Risques

- action métier commit sans log ;
- log perdu pendant une panne ;
- information sensible exposée ;
- changement de message cassant les recherches ;
- absence d’identité fiable.

---

## 12.5 Réversibilité

```text
moyenne
```

L’historique passé ne peut pas être reconstruit complètement.

---

# 13. Option B — Historique métier par table fonctionnelle

## 13.1 Description

Chaque module possède ses propres tables d’historique.

---

## 13.2 Avantages

- proximité métier ;
- modèles adaptés ;
- transaction facile ;
- requêtes spécialisées.

---

## 13.3 Inconvénients

- fragmentation ;
- conventions divergentes ;
- recherche globale difficile ;
- duplications ;
- rétention incohérente ;
- intégrité variable ;
- couverture incomplète des opérations Infrastructure.

---

## 13.4 Risques

- certaines actions non auditées ;
- tables sans protection append-only ;
- impossibilité de reconstituer un parcours cross-module ;
- customizations utilisant un autre format.

---

## 13.5 Réversibilité

```text
moyenne
```

---

# 14. Option C — Service distant obligatoire

## 14.1 Description

Chaque action sensible est envoyée vers une plateforme d’audit distante.

---

## 14.2 Avantages

- isolation du système audité ;
- centralisation ;
- recherche ;
- détection cross-client ;
- stockage append-only possible.

---

## 14.3 Inconvénients

- dépendance réseau ;
- disponibilité ;
- credentials ;
- transfert client ;
- coût ;
- données externalisées ;
- transaction métier difficile ;
- verrouillage fournisseur.

---

## 14.4 Risques

- opération réussie mais audit distant échoué ;
- audit accepté mais transaction métier annulée ;
- fuite inter-client ;
- impossibilité d’utiliser le projet de manière autonome.

---

## 14.5 Réversibilité

```text
faible à moyenne
```

---

# 15. Option D — Piste PostgreSQL dédiée, append-only et vérifiable

## 15.1 Description

Les événements sont enregistrés dans un schéma PostgreSQL dédié.

Ils sont ajoutés dans la même transaction que l’action lorsque possible.

Une chaîne de hashes permet de détecter une modification postérieure.

---

## 15.2 Avantages

- cohérence transactionnelle ;
- aucun service supplémentaire ;
- isolation par client ;
- requêtes locales ;
- backup commun ;
- contrats stables ;
- contrôle des accès ;
- possibilité d’export ultérieur ;
- compatibilité avec un backend externe futur.

---

## 15.3 Inconvénients

- même base que l’application ;
- administrateur PostgreSQL puissant ;
- sérialisation des écritures de chaîne ;
- rétention à gérer ;
- table supplémentaire ;
- vérification d’intégrité ;
- traitement particulier des restaurations.

---

## 15.4 Risques

- contention ;
- permissions mal configurées ;
- chaîne cassée ;
- root capable de réécrire l’ensemble ;
- événement trop volumineux ;
- mauvaise classification des actions.

---

## 15.5 Réversibilité

```text
élevée
```

Les événements pourront ultérieurement être exportés vers un système spécialisé.

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Logs | Historiques modules | Service distant | PostgreSQL dédié |
|---|---:|---:|---:|---:|---:|
| Transaction métier | 5 | 2 | 5 | 2 | 5 |
| Structure uniforme | 5 | 2 | 2 | 5 | 5 |
| Intégrité | 5 | 2 | 3 | 5 | 4 |
| Autonomie client | 5 | 4 | 5 | 1 | 5 |
| Simplicité MVP | 4 | 5 | 3 | 1 | 4 |
| Recherche globale | 4 | 3 | 2 | 5 | 5 |
| Transfert client | 4 | 3 | 4 | 2 | 5 |
| Coût | 4 | 5 | 4 | 2 | 5 |
| Réversibilité | 4 | 3 | 3 | 3 | 5 |
| Maintenabilité | 5 | 2 | 2 | 4 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’implémenter une piste d’audit PostgreSQL :

```text
dédiée
structurée
append-only
transactionnelle
isolée
vérifiable
```

Les logs techniques peuvent référencer l’Audit Event ID.

Ils ne dupliquent pas nécessairement toutes les données de l’événement.

---

## 17.1 Source de vérité

La table d’audit PostgreSQL constitue la source principale de l’historique des actions sensibles pour le projet client.

---

## 17.2 Backend externe

Une copie ou export vers un backend spécialisé peut être ajouté.

Il ne remplace pas la persistance transactionnelle locale sans nouvelle décision.

---

# 18. Schéma PostgreSQL

Le schéma conceptuel est :

```text
sitefactory_audit
```

Tables principales :

```text
audit_events

audit_chain_state

audit_epochs
```

Tables ou vues futures possibles :

```text
audit_export_requests

audit_integrity_checks

audit_retention_runs
```

---

## 18.1 Propriété

Le schéma appartient à la plateforme d’audit.

Les modules n’accèdent pas directement aux tables.

---

## 18.2 Migrations

Les évolutions suivent l’ADR-0004.

---

# 19. Modèle d’événement

Un événement doit pouvoir contenir :

```text
AuditEventId

SchemaVersion

EpochId

SequenceNumber

OccurredAtUtc

RecordedAtUtc

Category

Action

Outcome

Sensitivity

ActorType

ActorId

EffectiveActorId

TargetType

TargetId

ClientSlug

Environment

Source

ReasonCode

ReasonText

ChangedFields

Metadata

CorrelationId

TraceId

IdempotencyKey

PreviousEventHash

EventHash
```

---

## 19.1 Modèle physique

La structure physique exacte peut évoluer.

Les responsabilités restent obligatoires.

---

# 20. Audit Event ID

Chaque événement possède un identifiant opaque.

Un UUID ou ULID peut être utilisé.

---

## 20.1 Unicité

Une contrainte PostgreSQL garantit l’unicité.

---

## 20.2 Exposition

L’identifiant peut être affiché à un opérateur autorisé ou associé à un rapport.

---

## 20.3 Métriques

Il ne doit pas être utilisé comme label métrique.

---

# 21. Schema Version

Chaque événement possède une version de schéma.

---

## 21.1 Évolution

Un consommateur doit pouvoir interpréter les anciennes versions.

---

## 21.2 Suppression de champ

Un champ historique ne doit pas être supprimé sans migration ou compatibilité.

---

# 22. Horodatage

## 22.1 `OccurredAtUtc`

Moment auquel l’action est considérée comme ayant eu lieu.

---

## 22.2 `RecordedAtUtc`

Moment auquel PostgreSQL a enregistré l’événement.

---

## 22.3 Source du temps

`RecordedAtUtc` utilise l’horloge PostgreSQL.

---

## 22.4 Écart

Un écart inhabituel entre les deux horodatages peut être signalé.

---

## 22.5 Locale

Tous les événements sont stockés en UTC.

L’interface peut les afficher dans le fuseau de l’opérateur.

---

# 23. Catégories

Valeurs conceptuelles :

```text
authentication

identity

authorization

data-access

data-export

data-deletion

content

media

configuration

security

operations

deployment

migration

backup

restore

jobs

audit
```

---

## 23.1 Catégorie stable

Les catégories sont versionnées et bornées.

---

## 23.2 Texte libre

Une catégorie ne doit pas être fournie librement par l’utilisateur.

---

# 24. Actions

Les actions utilisent des identifiants stables.

Convention :

```text
{domain}.{resource}.{verb}
```

Exemples :

```text
authentication.session.started

identity.user.disabled

authorization.role.assigned

lead.export.requested

content.page.published

configuration.kill-switch.activated

operations.restore.completed
```

---

## 24.1 Verbe passé

L’action représente une tentative ou un résultat.

Le champ `Outcome` porte le résultat.

---

## 24.2 Renommage

Une action publiée ne doit pas être renommée silencieusement.

---

# 25. Outcome

Valeurs :

```text
succeeded

failed

denied

cancelled

partially-completed
```

---

## 25.1 Action refusée

Une tentative administrative sensible refusée peut être auditée.

---

## 25.2 Bruit

Les requêtes anonymes ordinaires refusées par le rate limiter ne doivent pas toutes être ajoutées à la piste d’audit.

Elles appartiennent à l’observabilité et aux événements de sécurité agrégés.

---

# 26. Sensibilité

Niveaux recommandés :

```text
standard

sensitive

critical
```

---

## 26.1 `standard`

Action administrative significative mais réversible et de portée limitée.

---

## 26.2 `sensitive`

Action affectant :

- données personnelles ;
- permissions ;
- publication ;
- export ;
- suppression.

---

## 26.3 `critical`

Action affectant :

- restaurations ;
- migrations destructives ;
- sécurité ;
- accès privilégiés ;
- kill switches ;
- audit ;
- secrets ;
- environnement de production.

---

# 27. Acteur

Un acteur doit être identifié par :

```text
ActorType

ActorId
```

---

## 27.1 Utilisateur

L’Actor ID utilise l’identifiant Identity stable.

---

## 27.2 Opérateur

Une opération CLI ou Infrastructure utilise un identifiant opérateur technique.

---

## 27.3 Service

Un worker ou service utilise une identité de service stable.

---

## 27.4 System

Une action interne non initiée directement par une personne peut utiliser :

```text
system
```

avec un Source et une raison explicites.

---

## 27.5 Acteur inconnu

Une action critique ne doit pas utiliser un acteur inconnu si l’opérateur peut être identifié.

---

# 28. Acteur réel et acteur effectif

L’architecture réserve :

```text
ActorId

EffectiveActorId
```

---

## 28.1 Baseline

L’impersonation n’est pas activée dans le MVP.

---

## 28.2 Évolution future

Si elle est ajoutée, l’audit doit conserver :

- l’identité réelle ;
- l’identité impersonée ;
- la raison ;
- la durée ;
- la permission utilisée.

---

## 28.3 Masquage interdit

Une action impersonée ne doit pas apparaître comme une action directe de l’utilisateur cible.

---

# 29. Snapshot de l’acteur

L’Audit Event ID doit rester exploitable même si l’utilisateur est supprimé.

---

## 29.1 Baseline

Conserver :

- Actor ID ;
- Actor Type ;
- éventuellement un label d’affichage minimal.

---

## 29.2 PII

Un email complet ne doit pas être copié dans tous les événements par défaut.

---

## 29.3 Résolution

L’interface peut résoudre le nom courant lorsque l’utilisateur existe encore.

---

## 29.4 Compte supprimé

L’interface affiche un identifiant technique et un statut :

```text
deleted-user
```

---

# 30. Cible

Une cible est définie par :

```text
TargetType

TargetId
```

Exemples :

```text
identity-user / 01J...

lead / 01J...

content-page / page.home

kill-switch / public.contact.submission.disabled

migration / AddLeadArchiveState
```

---

## 30.1 Opacité

Le Target ID ne doit pas contenir de secret.

---

## 30.2 Action globale

Une action affectant tout l’environnement peut utiliser :

```text
TargetType = environment
TargetId = production
```

---

# 31. Source

Valeurs conceptuelles :

```text
web

worker

cli

deployment

migration

backup

restore

system
```

---

## 31.1 Usage

La source permet d’identifier le chemin technique ayant produit l’événement.

---

## 31.2 User-Agent

Le User-Agent complet n’est pas utilisé comme source.

---

# 32. Raison

Certaines actions exigent :

```text
ReasonCode
ReasonText
```

---

## 32.1 Actions exigeant une raison

La baseline exige une raison pour :

- désactivation d’un utilisateur ;
- changement de rôle privilégié ;
- suppression de données ;
- export massif ;
- activation d’un kill switch ;
- retry manuel d’un job critique ;
- restauration ;
- migration destructive ;
- purge d’audit ;
- usage break-glass.

---

## 32.2 Reason Code

Le code reste structuré et borné.

Exemples :

```text
USER_REQUEST

SECURITY_INCIDENT

DATA_CORRECTION

OPERATIONAL_RECOVERY

CONTRACT_CLOSURE

MANUAL_REMEDIATION
```

---

## 32.3 Reason Text

Le texte libre :

- est facultatif ou obligatoire selon l’action ;
- possède une longueur maximale ;
- ne contient aucun secret ;
- n’est pas utilisé comme label métrique ;
- est encodé dans l’interface.

---

# 33. Changed Fields

Une modification peut conserver la liste des champs affectés.

Exemple :

```json
[
  "RoleAssignments",
  "IsEnabled"
]
```

---

## 33.1 Valeurs avant et après

Les valeurs complètes ne sont pas conservées par défaut.

---

## 33.2 Valeurs sensibles

Ne jamais enregistrer les anciennes ou nouvelles valeurs de :

- mot de passe ;
- token ;
- secret ;
- clé ;
- chaîne de connexion ;
- réponse MFA ;
- contenu privé complet.

---

## 33.3 Valeur bornée

Une petite valeur non sensible peut être conservée lorsque nécessaire.

Exemple :

```text
PreviousStatus = "new"
NewStatus = "archived"
```

---

# 34. Metadata

Le champ Metadata est un objet JSON structuré et borné.

---

## 34.1 Allowlist

Chaque Action définit les clés autorisées.

---

## 34.2 Taille

La taille maximale est contrôlée.

---

## 34.3 Données interdites

Le Metadata ne doit pas devenir un moyen de contourner les restrictions de l’événement principal.

---

## 34.4 Exemples autorisés

```text
RoleId

PermissionId

ExportFormat

DeploymentVersion

MigrationName

BackupId

JobType

PolicyVersion
```

---

# 35. Corrélation

Un événement peut conserver :

```text
CorrelationId

TraceId
```

---

## 35.1 Trace ID

Permet de retrouver les logs et traces techniques.

---

## 35.2 Correlation ID

Permet de rapprocher :

- job ;
- rapport ;
- action métier ;
- opération longue.

---

## 35.3 Données interdites

Le Correlation ID ne doit pas être dérivé d’un email ou d’un secret.

---

# 36. Idempotence

Chaque écriture d’audit possède une clé d’idempotence.

---

## 36.1 Retry

Une nouvelle tentative de la même opération ne doit pas créer des événements identiques en nombre illimité.

---

## 36.2 Unicité

Une contrainte peut utiliser :

```text
Action + IdempotencyKey
```

dans le périmètre approprié.

---

## 36.3 Événements distincts

La demande et le résultat d’une opération longue utilisent des actions distinctes ou des clés distinctes.

---

# 37. Écriture transactionnelle

Lorsqu’une action modifie PostgreSQL, son événement d’audit est enregistré dans la même transaction.

Exemple :

```text
attribuer le rôle
+ ajouter l’événement d’audit
+ commit
```

---

## 37.1 Garantie

États possibles :

```text
transaction commit
→ modification et audit présents

transaction rollback
→ modification et audit absents
```

---

## 37.2 Audit failed

Si l’audit obligatoire échoue :

```text
rollback de l’action
```

---

## 37.3 Transaction longue

L’audit ne doit pas garder une transaction ouverte pendant un appel externe.

---

# 38. Opérations longues

Une opération longue utilise plusieurs événements.

Exemple restauration :

```text
operations.restore.requested
operations.restore.started
operations.restore.completed
```

ou :

```text
operations.restore.failed
```

---

## 38.1 Operation ID

Tous les événements partagent un Correlation ID ou Operation ID.

---

## 38.2 État final

Une demande ne doit pas être présentée comme une restauration réussie.

---

# 39. Actions hors disponibilité de PostgreSQL

Certaines opérations de reprise peuvent avoir lieu alors que la base principale est indisponible.

---

## 39.1 Exemples

- restauration de la base ;
- reconstruction du VPS ;
- récupération d’un backup ;
- correction d’un volume.

---

## 39.2 Pre-operation report

L’opérateur doit produire un rapport local sécurisé contenant :

- Operation ID ;
- acteur ;
- client ;
- environnement ;
- action ;
- raison ;
- date ;
- artefacts ;
- résultat.

---

## 39.3 Réconciliation

Après retour de PostgreSQL, un événement d’audit est ajouté avec une référence au rapport.

---

## 39.4 Secret

Le rapport ne contient aucun secret.

---

# 40. Politique fail-closed

Les actions sensibles exécutées depuis l’application échouent si l’audit obligatoire ne peut pas être enregistré.

---

## 40.1 Actions ordinaires

Une consultation publique ou opération non sensible ne dépend pas de l’audit.

---

## 40.2 Availability

L’indisponibilité du sous-système d’audit ne doit pas rendre tout le site public indisponible.

---

## 40.3 Administration

Les actions sensibles de l’administration deviennent temporairement indisponibles.

---

# 41. Break-glass

Une procédure break-glass permet une intervention exceptionnelle lorsque les contrôles ordinaires ne sont pas utilisables.

---

## 41.1 Conditions

- incident réel ;
- compte ou mécanisme dédié ;
- durée limitée ;
- raison obligatoire ;
- permissions minimales ;
- alerte immédiate ;
- rapport ;
- revue après incident.

---

## 41.2 Pas de compte partagé permanent

Un compte break-glass ne doit pas être utilisé pour les opérations ordinaires.

---

## 41.3 Audit indisponible

Si la base d’audit est indisponible, le rapport externe sécurisé devient obligatoire.

---

## 41.4 Revue

L’utilisation break-glass doit être revue après rétablissement.

---

# 42. Append-only

Les événements enregistrés ne sont jamais modifiés.

---

## 42.1 Correction

Une correction produit un nouvel événement tel que :

```text
audit.event.corrected
```

référençant l’événement initial.

---

## 42.2 Annulation métier

Annuler une action métier ne supprime pas l’événement de l’action initiale.

---

## 42.3 Suppression directe

Le rôle runtime ne possède pas de permission `DELETE` sur les événements.

---

# 43. Permissions PostgreSQL

Le rôle applicatif doit pouvoir :

- exécuter la fonction d’ajout ;
- lire les événements via les requêtes autorisées.

Il ne doit pas pouvoir :

- mettre à jour ;
- supprimer ;
- modifier l’état de chaîne directement ;
- désactiver les triggers ou contraintes.

---

## 43.1 Fonction contrôlée

Une fonction conceptuelle peut être utilisée :

```text
sitefactory_audit.append_event(...)
```

---

## 43.2 Rôle de rétention

Une opération de rétention utilise un rôle ou chemin opérateur distinct.

---

## 43.3 Superutilisateur

Un superutilisateur PostgreSQL conserve techniquement des capacités élevées.

La solution ne prétend pas le rendre inoffensif.

---

# 44. Chaîne d’intégrité

Chaque événement inclut :

```text
PreviousEventHash
EventHash
```

---

## 44.1 Calcul

Conceptuellement :

```text
EventHash =
SHA-256(
  PreviousEventHash
  + CanonicalAuditEvent
)
```

---

## 44.2 Canonicalisation

La représentation canonique doit être :

- versionnée ;
- déterministe ;
- indépendante de l’ordre des propriétés JSON ;
- indépendante de la locale ;
- encodée de manière stable.

---

## 44.3 Exclusions

`EventHash` lui-même est exclu du contenu hashé.

---

## 44.4 Sequence Number

Le numéro de séquence est inclus dans le contenu hashé.

---

# 45. Sérialisation des écritures

L’ajout à la chaîne utilise un verrou PostgreSQL court sur l’état de l’epoch.

---

## 45.1 Flux

```text
verrouiller audit_chain_state
→ lire le hash précédent
→ attribuer le numéro de séquence
→ calculer le nouvel hash
→ insérer l’événement
→ mettre à jour chain_state
→ commit
```

---

## 45.2 Transaction

Le verrou reste limité à l’ajout de l’événement.

---

## 45.3 Volume

Le volume administratif attendu rend cette sérialisation acceptable dans le MVP.

---

# 46. Limites de la chaîne

La chaîne détecte notamment :

- modification d’un événement ;
- suppression d’un événement intermédiaire ;
- insertion rétroactive ;
- changement d’ordre.

---

## 46.1 Elle ne garantit pas

La chaîne ne protège pas absolument contre un attaquant capable de :

- réécrire toute la base ;
- recalculer toute la chaîne ;
- modifier l’application et la base ;
- remplacer également tous les backups et anchors.

---

## 46.2 Communication

SiteFactory doit parler de :

```text
tamper-evident
```

et non de :

```text
tamper-proof
```

---

# 47. Audit epochs

Chaque chaîne appartient à un epoch.

---

## 47.1 Création initiale

Un premier epoch est créé à l’initialisation de la base.

---

## 47.2 Nouvel epoch

Un nouvel epoch est créé après :

- restauration ;
- migration exceptionnelle de l’audit ;
- rupture confirmée ;
- import contrôlé.

---

## 47.3 Référence

Le nouvel epoch peut référencer :

- Previous Epoch ID ;
- dernier hash connu ;
- Restore Report ID ;
- raison.

---

# 48. Vérification d’intégrité

Un job récurrent vérifie :

- séquence ;
- Previous Event Hash ;
- Event Hash ;
- état de l’epoch ;
- premier et dernier événement.

---

## 48.1 Mode

La vérification est en lecture seule.

---

## 48.2 Résultat

Valeurs :

```text
valid

invalid

incomplete

unknown
```

---

## 48.3 Rupture

Une rupture produit :

- alerte critique ;
- kill switch éventuel sur les actions sensibles ;
- incident ;
- rapport ;
- absence de réparation automatique.

---

# 49. Anchor externe

Un anchor peut conserver périodiquement :

```text
EpochId

LastSequenceNumber

LastEventHash

CreatedAtUtc

ApplicationVersion
```

dans un stockage opérationnel distinct.

---

## 49.1 MVP

L’anchor externe est recommandé pour les environnements de production et devient obligatoire pour les projets nécessitant une garantie d’intégrité renforcée.

---

## 49.2 Secret

L’anchor ne contient aucun contenu métier ou secret.

---

## 49.3 Signature future

Une signature externe pourra renforcer l’anchor.

---

# 50. Événements d’authentification

La piste d’audit doit enregistrer au minimum :

```text
authentication.session.started

authentication.session.ended

authentication.account.locked

authentication.account.unlocked

authentication.password-reset.completed

authentication.mfa.changed
```

---

## 50.1 Échecs de connexion

Chaque échec individuel n’est pas nécessairement conservé dans la piste principale afin d’éviter :

- spam ;
- volume ;
- exposition ;
- attaque par saturation.

---

## 50.2 Sécurité

Les échecs sont :

- observés ;
- rate-limited ;
- agrégés ;
- transformés en événements de lockout ou incident lorsque nécessaire.

---

## 50.3 Connexion réussie

Une connexion administrateur réussie est auditée.

---

# 51. Identité et comptes

Événements recommandés :

```text
identity.user.created

identity.user.enabled

identity.user.disabled

identity.user.deleted

identity.invitation.created

identity.invitation.revoked

identity.session.revoked
```

---

## 51.1 Suppression

La suppression d’un compte ne supprime pas son historique d’audit.

---

## 51.2 Invitation

Le token d’invitation n’est jamais enregistré.

---

# 52. Rôles et permissions

Événements obligatoires :

```text
authorization.role.created

authorization.role.updated

authorization.role.deleted

authorization.role.assigned

authorization.role.removed

authorization.permission.granted

authorization.permission.revoked
```

---

## 52.1 Raison

L’attribution d’un rôle privilégié exige une raison.

---

## 52.2 Acteur et cible

L’acteur ayant réalisé l’opération et l’utilisateur cible sont distincts.

---

## 52.3 Auto-élévation

Une tentative d’auto-attribution d’un privilège refusée peut être auditée.

---

# 53. Entitlements

Bien que les entitlements ne soient pas modifiables par l’administration fonctionnelle dans le MVP, les opérations suivantes doivent être auditées lorsqu’elles existent :

```text
entitlement.granted

entitlement.revoked

entitlement.configuration-applied
```

---

## 53.1 Origine

L’événement doit référencer :

- décision commerciale ou change request ;
- configuration ;
- release ;
- acteur.

---

## 53.2 Prix

Le prix contractuel n’est pas nécessairement copié dans l’événement technique.

---

# 54. Feature flags

Événements recommandés :

```text
configuration.feature-flag.enabled

configuration.feature-flag.disabled
```

---

## 54.1 Metadata

- Feature Flag ID ;
- ancienne valeur ;
- nouvelle valeur ;
- environnement ;
- raison.

---

## 54.2 Pas d’entitlement

L’événement ne doit pas faire apparaître un flag comme un entitlement.

---

# 55. Kill switches

Événements obligatoires :

```text
configuration.kill-switch.activated

configuration.kill-switch.deactivated
```

---

## 55.1 Raison

Toujours obligatoire.

---

## 55.2 Actions affectées

Le Metadata peut contenir :

- Kill Switch ID ;
- capability ;
- incident ID ;
- expiration ou prochaine revue.

---

## 55.3 Désactivation

La désactivation du kill switch nécessite également une raison.

---

# 56. Configuration runtime

Les changements significatifs de production doivent être audités.

Exemples :

```text
configuration.runtime.changed

configuration.observability.changed

configuration.rate-limit.changed

configuration.provider.changed
```

---

## 56.1 Valeurs sensibles

L’événement conserve :

- clé ;
- catégorie ;
- ancienne présence ;
- nouvelle présence ;
- version logique.

Il ne conserve pas la valeur d’un secret.

---

# 57. Secrets

Événements possibles :

```text
security.secret.created

security.secret.rotated

security.secret.revoked

security.secret.accessed-exceptionally
```

---

## 57.1 Valeurs interdites

Ne jamais conserver :

- secret ;
- hash de secret ;
- token prefix inutile ;
- chaîne de connexion.

---

## 57.2 Metadata

- Secret ID ;
- provider ;
- scope ;
- résultat ;
- prochaine revue.

---

# 58. Leads

Actions sensibles recommandées :

```text
lead.status.changed

lead.archived

lead.restored

lead.deleted

lead.export.requested

lead.export.completed

lead.export.downloaded
```

---

## 58.1 Modifications ordinaires

Toutes les petites modifications de champ ne sont pas nécessairement des événements d’audit global.

Elles peuvent utiliser l’historique métier du module.

---

## 58.2 Export

Un export est toujours considéré comme sensible.

---

## 58.3 Données

L’événement ne contient pas les données du lead.

---

# 59. Contenus

Événements recommandés :

```text
content.published

content.unpublished

content.archived

content.deleted

content.restored
```

---

## 59.1 Brouillon

Chaque frappe ou sauvegarde automatique de brouillon n’est pas nécessairement auditée globalement.

---

## 59.2 Publication

Une publication publique doit être auditée.

---

## 59.3 Contenu

Le texte intégral n’est pas copié dans l’événement.

---

# 60. Médias et documents

Actions sensibles possibles :

```text
media.private.downloaded

media.deleted

media.restored

media.visibility.changed

media.quarantine.released
```

---

## 60.1 Téléchargements publics

Les téléchargements publics ordinaires ne sont pas audités individuellement.

---

## 60.2 Document privé

Un accès à un document privé sensible peut être audité selon son profil.

---

## 60.3 Contenu

Le binaire et l’URL signée ne sont jamais conservés.

---

# 61. Formulaires et quarantaines

Événements recommandés :

```text
forms.quarantine.accepted

forms.quarantine.rejected

forms.quarantine.marked-spam

forms.submission.merged
```

---

## 61.1 Contenu

Le message complet n’est pas copié.

---

## 61.2 Reason Codes

La décision humaine utilise un code et une note bornée.

---

# 62. Jobs

Actions opérateur sensibles :

```text
jobs.retry.requested

jobs.cancelled

jobs.paused

jobs.resumed

jobs.dead-letter.purged
```

---

## 62.1 Retry

Un retry manuel exige une raison.

---

## 62.2 Payload

Le payload complet n’est pas copié dans l’audit.

---

## 62.3 Job ID

Le Job ID peut être utilisé comme Target ID.

---

# 63. Migrations

Événements obligatoires :

```text
operations.migration.requested

operations.migration.started

operations.migration.completed

operations.migration.failed
```

---

## 63.1 Metadata

- Source Version ;
- Target Version ;
- Migration Name ;
- Backup ID ;
- Report ID ;
- durée ;
- résultat.

---

## 63.2 SQL

Le SQL complet n’est pas conservé dans l’événement.

---

# 64. Backups

Événements recommandés :

```text
operations.backup.requested

operations.backup.completed

operations.backup.failed

operations.backup.verified
```

---

## 64.1 Backup ID

Le Backup ID peut être conservé.

---

## 64.2 Path

Le chemin complet du stockage externe n’est pas nécessairement inclus.

---

# 65. Restaurations

Événements obligatoires :

```text
operations.restore.requested

operations.restore.started

operations.restore.completed

operations.restore.failed

operations.restore.epoch-created
```

---

## 65.1 Niveau

Une restauration est toujours :

```text
critical
```

---

## 65.2 Confirmation

Elle nécessite :

- raison ;
- cible ;
- backup ;
- confirmation renforcée ;
- rapport.

---

## 65.3 Nouvel epoch

Après restauration, la piste d’audit démarre un nouvel epoch.

---

# 66. Déploiements

Événements recommandés :

```text
operations.deployment.started

operations.deployment.completed

operations.deployment.failed

operations.deployment.rolled-back
```

---

## 66.1 Metadata

- Application Version ;
- Image Digest ;
- Environment ;
- Deployment ID ;
- Migration Report ID.

---

## 66.2 Secrets

Aucun credential registry ou environnemental.

---

# 67. Repository et supply chain

Actions sensibles possibles :

```text
repository.remote-published

repository.visibility.changed

repository.transferred

supply.package-published

supply.package-withdrawn

supply.image-published
```

---

## 67.1 Audit local

Certaines opérations peuvent produire un rapport local en plus de l’événement applicatif.

---

# 68. Consultation de la piste d’audit

La lecture nécessite une permission dédiée :

```text
Audit.Read
```

---

## 68.1 Pas de droit implicite

Le rôle administrateur fonctionnel ne reçoit pas automatiquement cette permission.

---

## 68.2 Filtres

La consultation doit être paginée et filtrable par :

- période ;
- catégorie ;
- action ;
- outcome ;
- acteur ;
- cible ;
- sensibilité.

---

## 68.3 Recherche libre

Une recherche libre sur Reason Text peut être limitée afin de réduire :

- exposition ;
- coût ;
- abus.

---

## 68.4 Consultation sensible

L’accès à un événement critique peut lui-même être audité selon le profil.

---

# 69. Export de la piste d’audit

Un export nécessite :

```text
Audit.Export
```

---

## 69.1 Traitement

L’export est généré par un job persistant.

---

## 69.2 Stockage

Il est :

- privé ;
- temporaire ;
- autorisé ;
- chiffré selon le stockage ;
- soumis à un TTL.

---

## 69.3 Audit

Les événements suivants sont enregistrés :

```text
audit.export.requested

audit.export.completed

audit.export.downloaded
```

---

## 69.4 Format

Formats possibles :

```text
JSON Lines

CSV contrôlé
```

---

## 69.5 CSV injection

Les exports CSV doivent neutraliser les cellules commençant par :

```text
=
+
-
@
```

selon la stratégie retenue.

---

# 70. API de consultation

Une API d’audit ne doit pas exposer :

- secrets ;
- payloads complets ;
- stack traces ;
- chemins ;
- données personnelles excessives.

---

## 70.1 Pagination

La pagination est obligatoire.

---

## 70.2 Limite maximale

La taille de page est bornée.

---

## 70.3 Tri

Le tri principal utilise :

```text
EpochId
SequenceNumber
```

ou :

```text
RecordedAtUtc
```

avec une clé secondaire stable.

---

# 71. Contrats applicatifs

Une abstraction conceptuelle est utilisée :

```csharp
public interface IAuditTrailWriter
{
    Task AppendAsync(
        AuditEventCandidate auditEvent,
        CancellationToken cancellationToken);
}
```

---

## 71.1 Transaction existante

Le contrat doit pouvoir participer à la transaction PostgreSQL courante.

---

## 71.2 Domain

Domain ne dépend pas directement :

- de PostgreSQL ;
- du hash ;
- du schéma d’audit ;
- de l’interface de requête.

---

## 71.3 Application

Application décide :

- quelle action est sensible ;
- quel événement produire ;
- quelle raison exiger.

---

# 72. Builder typé

Les événements ne doivent pas être construits comme des dictionnaires libres dispersés.

---

## 72.1 Exemple conceptuel

```csharp
AuditEventCandidate.ForAction(AuditActions.RoleAssigned)
    .WithActor(actor)
    .WithTarget(userId)
    .WithReason(reason)
    .WithMetadata(new RoleAssignedAuditMetadata(roleId));
```

---

## 72.2 Objectif

- propriétés obligatoires ;
- metadata typée ;
- validation ;
- absence de clés arbitraires ;
- tests.

---

# 73. Catalogue des actions

Le catalogue est :

- versionné ;
- machine-readable ;
- associé aux modules ;
- associé aux métadonnées autorisées ;
- associé au niveau de sensibilité ;
- associé aux permissions.

---

## 73.1 Module

Chaque module déclare ses actions d’audit.

---

## 73.2 Collision

Deux modules ne peuvent pas déclarer la même Action ID avec des contrats différents.

---

## 73.3 Retrait

Une Action ID historique ne doit pas être réutilisée avec un sens différent.

---

# 74. Action administrative sensible

Une action est considérée comme sensible lorsqu’elle affecte au moins un élément parmi :

```text
identité

permissions

données personnelles

confidentialité

publication

suppression

export

configuration de production

sécurité

intégrité

disponibilité

infrastructure

restauration
```

---

## 74.1 Revue d’architecture

Une nouvelle action sensible doit définir son audit avant sa livraison.

---

# 75. Matrice de contrôles

| Niveau | Confirmation | Raison | Authentification récente | Audit fail-closed |
|---|---:|---:|---:|---:|
| Standard | Selon action | Facultative | Non | Oui si audit obligatoire |
| Sensitive | Oui | Souvent | Recommandée | Oui |
| Critical | Oui renforcée | Obligatoire | Obligatoire | Oui |

---

## 75.1 Authentification récente

Le mécanisme exact sera formalisé dans un ADR dédié.

---

## 75.2 Double validation

Une approbation par deux personnes n’est pas obligatoire dans le MVP, LogicGate pouvant être opéré par une seule personne.

Elle pourra être ajoutée pour les clients ou secteurs qui l’exigent.

---

# 76. Confirmation explicite

Une action sensible ne doit pas être déclenchée par :

- un changement de select immédiat ;
- un simple lien GET ;
- une action ambiguë ;
- une interaction sans rappel de la cible.

---

## 76.1 Interface

La confirmation indique :

- action ;
- cible ;
- impact ;
- réversibilité ;
- raison éventuelle.

---

## 76.2 Serveur

La confirmation UI ne remplace pas :

- autorisation ;
- validation ;
- audit.

---

# 77. Step-up authentication

Les actions critiques doivent exiger une authentification récente ou renforcée.

Exemples :

- changement de rôle administrateur ;
- export massif ;
- restauration ;
- modification de l’audit ;
- break-glass ;
- rotation de credential privilégié.

---

## 77.1 Baseline

Le système doit pouvoir vérifier l’âge de la session ou demander une nouvelle authentification.

---

## 77.2 MFA

Une future authentification multifacteur pourra renforcer ce contrôle.

---

## 77.3 Audit

La réussite ou l’échec du step-up peut être audité sans enregistrer le secret d’authentification.

---

# 78. Tentatives refusées

Une tentative refusée est auditée lorsqu’elle concerne une action administrative sensible et un acteur authentifié.

---

## 78.1 Exemples

- changement de rôle sans permission ;
- export sans autorisation ;
- suppression interdite ;
- retry de job sans permission ;
- accès à l’audit sans permission.

---

## 78.2 Volume

Les attaques anonymes à fort volume restent dans les événements de sécurité et métriques agrégées.

---

# 79. Historique métier et audit

Un module peut conserver un historique métier distinct.

Exemple :

```text
Lead status history
```

---

## 79.1 Historique métier

Répond à :

```text
Comment la ressource a-t-elle évolué ?
```

---

## 79.2 Audit

Répond à :

```text
Qui a effectué l’action sensible,
depuis quel contexte
et avec quel résultat ?
```

---

## 79.3 Duplication limitée

Les deux systèmes peuvent référencer le même Correlation ID sans recopier toutes leurs données.

---

# 80. Logs techniques et audit

Les logs techniques peuvent enregistrer :

```text
AuditEventId

Action

Outcome

CorrelationId
```

---

## 80.1 Pas de duplication complète

L’intégralité de l’événement n’est pas nécessairement envoyée dans les logs.

---

## 80.2 Rétention

La suppression des logs ne supprime pas la piste d’audit.

---

## 80.3 Backend indisponible

Une panne du backend de logs ne doit pas empêcher l’écriture PostgreSQL de l’audit.

---

# 81. Métriques

Exemples :

```text
audit events appended

audit append failures

audit denied actions

audit critical actions

audit integrity failures

audit export requests

audit retention runs
```

---

## 81.1 Labels autorisés

```text
category

action_group

outcome

sensitivity

environment
```

---

## 81.2 Labels interdits

```text
AuditEventId

ActorId

TargetId

Email

IP

TraceId

CorrelationId
```

---

# 82. Observabilité

Événements techniques recommandés :

```text
AuditAppendStarted

AuditEventAppended

AuditAppendFailed

AuditIntegrityCheckStarted

AuditIntegrityCheckCompleted

AuditIntegrityViolationDetected

AuditExportStarted

AuditExportCompleted

AuditRetentionStarted

AuditRetentionCompleted
```

---

## 82.1 Données autorisées

```text
AuditEventId

Action

Category

Outcome

EpochId

SequenceNumber

ErrorCode

Duration
```

---

## 82.2 Données interdites

```text
ReasonText complet

Metadata complète

Secret

PII

Contenu métier
```

---

# 83. Rétention

La rétention de la piste d’audit doit être explicitement définie par projet.

---

## 83.1 Aucun délai universel

Cet ADR ne fixe pas une durée juridique universelle.

---

## 83.2 Profil

Un profil doit pouvoir définir :

```text
DefaultRetention

CriticalEventRetention

AuthenticationRetention

ExportRetention

LegalHoldPolicy

PurgeSchedule
```

---

## 83.3 Conservation indéfinie

Une conservation indéfinie par défaut est interdite.

---

## 83.4 Minimum opérationnel

La durée doit être suffisante pour couvrir :

- incidents ;
- maintenance ;
- contestations raisonnables ;
- cycles de backup ;
- transfert.

---

# 84. Legal hold

Un projet futur peut nécessiter un gel de conservation.

---

## 84.1 Baseline

Le legal hold n’est pas inclus dans le MVP standard.

---

## 84.2 Introduction

Il nécessitera :

- permission ;
- raison ;
- périmètre ;
- durée ;
- audit ;
- décision juridique.

---

# 85. Purge et rétention

Une purge est une opération sensible.

---

## 85.1 Processus

```text
plan
→ période
→ catégories
→ compte d’événements
→ confirmation
→ export ou anchor si requis
→ suppression contrôlée
→ rapport
→ audit
```

---

## 85.2 Application runtime

Le rôle runtime ordinaire ne supprime pas les événements.

---

## 85.3 Partitions

Un partitionnement temporel pourra faciliter la rétention.

---

## 85.4 Événements actifs

La purge ne doit pas supprimer des événements soumis à un gel ou une investigation.

---

# 86. Audit de la purge

L’événement de purge conserve :

- période ;
- nombre d’événements ;
- catégories ;
- policy ;
- rapport ;
- acteur ;
- raison.

---

## 86.1 Paradoxe de rétention

L’événement de purge peut lui-même suivre une rétention plus longue.

---

# 87. Isolation client

Chaque base cliente possède sa propre piste.

---

## 87.1 ClientSlug

Le Client Slug est enregistré comme contrôle supplémentaire.

---

## 87.2 Incohérence

Une tentative d’ajouter un événement pour un autre client doit être refusée.

---

## 87.3 Mutualisation future

Une plateforme centrale multi-client nécessiterait un ADR distinct.

---

# 88. Isolation des environnements

Staging et production possèdent :

- bases distinctes ;
- epochs distincts ;
- événements distincts ;
- clés d’intégrité et anchors distincts si utilisés.

---

## 88.1 Import

Les événements staging ne doivent pas être importés dans la production comme s’ils y avaient eu lieu.

---

# 89. Adresses réseau

Une adresse IP peut être utile lors d’un incident d’administration.

---

## 89.1 Baseline

La piste principale ne conserve pas systématiquement l’adresse IP brute.

---

## 89.2 Fingerprint

Un fingerprint pseudonymisé et à rétention limitée peut être utilisé.

---

## 89.3 Événement critique

Une adresse réseau peut être conservée dans un store de sécurité distinct lorsque cela est justifié.

---

## 89.4 Logs reverse proxy

Les logs Caddy possèdent leur propre politique.

---

# 90. Données personnelles

La piste peut contenir :

- Actor ID ;
- Target ID ;
- Reason Text ;
- identifiants techniques.

Elle constitue donc une donnée à accès restreint.

---

## 90.1 Minimisation

Ne pas conserver :

- nom complet à chaque événement ;
- email à chaque événement ;
- contenu métier ;
- documents ;
- payloads complets ;
- données de formulaire.

---

## 90.2 Suppression d’un utilisateur

L’Actor ID historique peut être conservé selon la politique applicable afin de préserver l’intégrité de la piste.

---

## 90.3 Export de données personnelles

Une demande d’accès ou suppression doit analyser le rôle particulier de la piste d’audit et les obligations applicables.

---

# 91. Secrets

Les secrets sont interdits dans :

- Action ;
- Reason Text ;
- Changed Fields ;
- Metadata ;
- Target ID ;
- Correlation ID ;
- rapports ;
- exports.

---

## 91.1 Scanner

Les metadata et raisons peuvent être soumises à des contrôles de patterns sensibles.

---

## 91.2 Limite

Le secret scanning est une protection complémentaire.

L’API doit surtout empêcher l’ajout de propriétés sensibles.

---

# 92. Chiffrement

La baseline repose sur :

- base non publique ;
- permissions ;
- volume protégé ;
- backups chiffrés ;
- secrets séparés ;
- accès limités.

---

## 92.1 Chiffrement champ par champ

Il n’est pas obligatoire dans le MVP.

---

## 92.2 Besoin futur

Une donnée particulièrement sensible devra être :

- omise ;
- pseudonymisée ;
- ou stockée dans un système spécialisé.

---

# 93. Backup

Le schéma d’audit fait partie du backup PostgreSQL.

---

## 93.1 Vérification

Le backup doit inclure :

- événements ;
- epochs ;
- chain state ;
- fonctions ;
- contraintes.

---

## 93.2 Manifest

Le manifest de backup peut enregistrer :

```text
CurrentAuditEpochId

LastAuditSequence

LastAuditHash
```

---

## 93.3 Secret

Le manifest ne contient aucune donnée métier ou raison.

---

# 94. Restauration

Une restauration peut revenir à un état où certains événements plus récents n’existent plus.

---

## 94.1 Nouveau stream

Après restauration :

```text
créer un nouvel epoch
```

---

## 94.2 Événement

Créer :

```text
operations.restore.epoch-created
```

avec :

- ancien Epoch ID ;
- dernier hash restauré ;
- Restore Report ID ;
- raison.

---

## 94.3 Anchor

Comparer si possible avec le dernier anchor externe.

---

## 94.4 Écart

Un écart entre le backup restauré et l’anchor le plus récent est attendu lorsqu’il existe des événements après la date du backup.

Il doit être documenté.

---

# 95. Intégrité après restauration

Avant de réactiver les actions critiques :

1. vérifier la chaîne restaurée ;
2. créer le nouvel epoch ;
3. rapprocher les rapports opérateurs ;
4. vérifier les accès ;
5. réactiver l’administration sensible.

---

# 96. Export et transfert client

Le handover doit inclure :

- schéma d’audit ;
- epochs ;
- events ;
- politique de rétention ;
- rapports d’intégrité ;
- dernier hash ;
- permissions ;
- procédure de consultation ;
- procédure de purge.

---

## 96.1 Format

Un export portable doit conserver :

- ordre ;
- schema version ;
- hashes ;
- epoch ;
- metadata structurée.

---

## 96.2 Credentials

Aucun credential LogicGate n’est transféré avec l’audit.

---

# 97. Clôture de service

La clôture doit déterminer :

- événement final ;
- export ;
- transfert ;
- durée de conservation ;
- suppression future ;
- retrait des accès LogicGate ;
- propriétaire de la piste.

---

## 97.1 Événement final

Une action telle que :

```text
operations.service-closure.completed
```

peut marquer la clôture technique.

---

# 98. Accès opérateur

Les rôles conceptuels sont :

```text
Audit.Viewer

Audit.Exporter

Audit.IntegrityReviewer

Audit.RetentionOperator
```

---

## 98.1 Séparation

Les permissions peuvent être distinctes.

Un Viewer ne peut pas nécessairement exporter.

---

## 98.2 Runtime

Le service web utilise les permissions minimales d’écriture et de lecture nécessaires.

---

# 99. Endpoints

Les endpoints d’audit doivent être :

- authentifiés ;
- autorisés ;
- protégés contre le CSRF ;
- paginés ;
- rate-limited selon le contexte ;
- non mis en cache publiquement.

---

## 99.1 GET sensible

Une lecture est effectuée par `GET`, mais reste protégée par autorisation.

---

## 99.2 Actions

Les exports, purges et vérifications utilisent des opérations explicites et confirmées.

---

# 100. UI d’administration

La vue doit permettre :

- filtre ;
- détail ;
- corrélation ;
- intégrité ;
- export ;
- navigation vers la cible lorsqu’elle existe.

---

## 100.1 Immutable

Aucun bouton de modification ou suppression d’événement ordinaire.

---

## 100.2 Correctif

Une correction ajoute un événement séparé.

---

## 100.3 Reason Text

Le texte est affiché encodé et borné.

---

# 101. Performance

## 101.1 Volume

Les actions administratives ont un volume inférieur aux requêtes publiques ordinaires.

---

## 101.2 Index recommandés

Selon les requêtes :

```text
EpochId + SequenceNumber

RecordedAtUtc

Category + RecordedAtUtc

Action + RecordedAtUtc

ActorId + RecordedAtUtc

TargetType + TargetId + RecordedAtUtc

Outcome + RecordedAtUtc
```

---

## 101.3 Metadata

Éviter des index JSON génériques coûteux sans besoin démontré.

---

## 101.4 Pagination

Aucune consultation ne charge toute la piste en mémoire.

---

# 102. Partitionnement

Le partitionnement temporel n’est pas obligatoire pour le premier incrément.

---

## 102.1 Introduction

Il peut devenir nécessaire lorsque :

- volume important ;
- rétention par période ;
- purge ;
- performance ;
- transfert.

---

## 102.2 Chaîne

Le partitionnement ne doit pas rompre la vérification de la chaîne.

---

# 103. Contention de chaîne

La chaîne sérialise brièvement les écritures d’un projet.

---

## 103.1 Acceptation

Le coût est accepté pour le volume administratif du MVP.

---

## 103.2 Surveillance

Mesurer :

- durée de lock ;
- échecs ;
- latence d’append ;
- contention.

---

## 103.3 Révision

Une architecture par sous-chaînes pourra être étudiée si le débit devient important.

---

# 104. Sécurité

## 104.1 Actifs

- actions administratives ;
- identités ;
- permissions ;
- rapports ;
- intégrité ;
- événements ;
- raisons ;
- données de corrélation.

---

## 104.2 Menaces

- modification ;
- suppression ;
- omission ;
- événement falsifié ;
- secret enregistré ;
- consultation non autorisée ;
- export abusif ;
- saturation ;
- purge malveillante ;
- restauration masquant des actions.

---

## 104.3 Contrôles

- append-only ;
- transaction ;
- chaîne de hashes ;
- epochs ;
- permissions ;
- step-up ;
- raisons ;
- exports privés ;
- rétention ;
- anchors ;
- tests ;
- alertes.

---

# 105. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Action commit sans audit | Même transaction PostgreSQL |
| Événement modifié | Append-only et hash chain |
| Événement supprimé | Permissions et chaîne |
| Secret ajouté | Metadata typée et contrôles |
| Admin consultant tout | Permission `Audit.Read` dédiée |
| Export massif abusif | Permission, step-up, TTL et audit |
| Purge malveillante | Rôle distinct, plan et audit |
| Restore masquant des événements | Nouvel epoch et anchor externe |
| Impersonation cachée | Acteur réel et effectif |
| Spam d’échecs anonymes | Agrégation hors piste principale |

---

# 106. Tests unitaires

Tester notamment :

- Action ID ;
- catégories ;
- outcomes ;
- metadata typée ;
- reason validation ;
- Actor Type ;
- Target Type ;
- canonicalisation ;
- hash ;
- idempotency key ;
- schema version.

---

# 107. Tests transactionnels

Vérifier :

```text
modification métier commit
→ audit présent

modification métier rollback
→ audit absent

audit append échoue
→ modification métier rollback

deux opérations simultanées
→ séquences distinctes et chaîne valide
```

---

# 108. Tests append-only

Le rôle runtime doit échouer lorsqu’il tente :

```text
UPDATE audit_events

DELETE audit_events

TRUNCATE audit_events

UPDATE audit_chain_state
```

---

## 108.1 Fonction d’ajout

Il doit pouvoir utiliser uniquement le chemin d’append prévu.

---

# 109. Tests de chaîne

Couvrir :

- premier événement ;
- plusieurs événements ;
- concurrence ;
- event modifié ;
- event supprimé ;
- sequence modifiée ;
- previous hash modifié ;
- metadata modifiée ;
- nouvel epoch ;
- vérification complète.

---

# 110. Tests de restauration

Simuler :

1. création d’un epoch ;
2. plusieurs événements ;
3. backup ;
4. événements supplémentaires ;
5. restauration du backup ;
6. nouvel epoch ;
7. rapprochement avec l’anchor.

---

## 110.1 Résultat

La piste restaurée reste vérifiable sans prétendre contenir les événements postérieurs au backup.

---

# 111. Tests d’identité

Couvrir :

- utilisateur ;
- opérateur ;
- service ;
- system ;
- compte supprimé ;
- Actor ID absent ;
- acteur réel et effectif ;
- break-glass.

---

# 112. Tests des actions sensibles

Vérifier au minimum :

```text
attribution de rôle

désactivation utilisateur

export de leads

suppression de lead

publication de contenu

activation de kill switch

retry manuel de job

migration

restauration

rotation de secret

export de l’audit
```

---

# 113. Tests des actions refusées

Vérifier :

- utilisateur sans permission ;
- session trop ancienne ;
- raison absente ;
- confirmation absente ;
- target invalide ;
- audit indisponible.

---

## 113.1 Résultat

L’action métier n’est pas appliquée.

Un événement de tentative refusée est enregistré lorsque possible et pertinent.

---

# 114. Tests de non-divulgation

Injecter des valeurs marqueurs représentant :

- mot de passe ;
- token ;
- clé API ;
- chaîne de connexion ;
- contenu de lead ;
- message ;
- URL signée.

Vérifier leur absence dans :

- événements ;
- exports ;
- logs ;
- traces ;
- rapports ;
- UI.

---

# 115. Tests d’accès

Couvrir :

```text
utilisateur sans Audit.Read

Audit.Viewer

Audit.Exporter

Audit.IntegrityReviewer

Audit.RetentionOperator

administrateur fonctionnel sans droit d’audit
```

---

# 116. Tests d’export

Vérifier :

- permission ;
- step-up ;
- filtre ;
- pagination ;
- job ;
- stockage privé ;
- TTL ;
- téléchargement ;
- audit de l’export ;
- neutralisation CSV.

---

# 117. Tests de rétention

Vérifier :

- plan ;
- période ;
- catégories ;
- legal hold futur simulé ;
- rôle ;
- confirmation ;
- rapport ;
- événement de purge ;
- chaîne et epochs selon la stratégie retenue.

---

# 118. Tests d’intégrité programmée

Vérifier :

- schedule ;
- résultat valide ;
- rupture détectée ;
- alerte ;
- absence de réparation automatique ;
- permission de consultation.

---

# 119. Tests de performance

Mesurer :

- temps d’append ;
- contention ;
- vérification de chaîne ;
- pagination ;
- export ;
- rétention ;
- concurrence.

---

# 120. Tests multiplateformes

Les contrats, services et outils sont testés sur :

```text
Windows x64

Linux x64
```

Les tests PostgreSQL et Docker sont validés sur Linux.

---

# 121. Quality gates

Une action sensible est bloquée lorsque :

```text
Aucun Audit Action ID n’est défini.

L’événement n’est pas structuré.

L’Actor ID est absent sans justification.

La Target est ambiguë.

La raison obligatoire est absente.

Une valeur sensible est ajoutée.

L’audit est écrit après le commit sans garantie.

Le rôle runtime peut modifier les événements.

La chaîne n’est pas vérifiable.

Un export n’est pas audité.

Une purge n’est pas auditée.

Une restauration ne crée pas un nouvel epoch.

Une action critique peut réussir sans audit.
```

---

# 122. Quality gates de production

La production est bloquée lorsque :

```text
Le schéma d’audit est absent.

Les migrations d’audit requises ne sont pas appliquées.

Le rôle runtime possède UPDATE ou DELETE.

L’epoch initial est absent.

La vérification initiale échoue.

Les permissions Audit sont mal configurées.

La rétention n’est pas définie.

L’export peut devenir public.

La procédure de restauration ne traite pas l’audit.

Les actions critiques ne sont pas couvertes.
```

---

# 123. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-AUDIT-001` | Action d’audit inconnue |
| `SF-AUDIT-002` | Catégorie inconnue |
| `SF-AUDIT-003` | Schema Version non supportée |
| `SF-AUDIT-004` | Actor Type invalide |
| `SF-AUDIT-005` | Actor ID obligatoire absent |
| `SF-AUDIT-006` | Target Type invalide |
| `SF-AUDIT-007` | Target ID obligatoire absent |
| `SF-AUDIT-008` | Outcome invalide |
| `SF-AUDIT-009` | Niveau de sensibilité invalide |
| `SF-AUDIT-010` | Raison obligatoire absente |
| `SF-AUDIT-011` | Reason Text trop long |
| `SF-AUDIT-012` | Metadata inconnue |
| `SF-AUDIT-013` | Metadata trop volumineuse |
| `SF-AUDIT-014` | Donnée sensible détectée |
| `SF-AUDIT-015` | Idempotency Key invalide |
| `SF-AUDIT-016` | Événement dupliqué |
| `SF-AUDIT-017` | Écriture d’audit impossible |
| `SF-AUDIT-018` | Transaction d’audit absente |
| `SF-AUDIT-019` | Epoch inconnu |
| `SF-AUDIT-020` | Sequence Number invalide |
| `SF-AUDIT-021` | Previous Event Hash invalide |
| `SF-AUDIT-022` | Event Hash invalide |
| `SF-AUDIT-023` | Chaîne d’intégrité rompue |
| `SF-AUDIT-024` | Chain State incohérent |
| `SF-AUDIT-025` | Modification d’événement interdite |
| `SF-AUDIT-026` | Suppression directe interdite |
| `SF-AUDIT-027` | Permission de lecture absente |
| `SF-AUDIT-028` | Permission d’export absente |
| `SF-AUDIT-029` | Permission de rétention absente |
| `SF-AUDIT-030` | Step-up authentication requis |
| `SF-AUDIT-031` | Confirmation renforcée requise |
| `SF-AUDIT-032` | Export invalide |
| `SF-AUDIT-033` | Export expiré |
| `SF-AUDIT-034` | Export public interdit |
| `SF-AUDIT-035` | Politique de rétention absente |
| `SF-AUDIT-036` | Purge non autorisée |
| `SF-AUDIT-037` | Événement soumis à conservation |
| `SF-AUDIT-038` | Vérification d’intégrité échouée |
| `SF-AUDIT-039` | Anchor externe incohérent |
| `SF-AUDIT-040` | Nouvel epoch requis |
| `SF-AUDIT-041` | Restore Report absent |
| `SF-AUDIT-042` | Break-glass non autorisé |
| `SF-AUDIT-043` | Rapport break-glass absent |
| `SF-AUDIT-044` | Action critique non auditable |
| `SF-AUDIT-045` | Rapport d’audit absent |

---

# 124. CI/CD

La CI doit vérifier :

- catalogue des actions ;
- metadata typée ;
- tests transactionnels ;
- permissions PostgreSQL ;
- chaîne ;
- migrations ;
- non-divulgation ;
- tests d’accès ;
- tests de restauration ;
- scans de secrets.

---

## 124.1 PostgreSQL réel

Les garanties de transaction, verrou et permissions doivent être testées sur PostgreSQL réel.

---

## 124.2 Backend externe

La CI standard ne dépend pas d’un SIEM ou backend distant.

---

# 125. Sécurité des exports

Un export d’audit peut lui-même contenir des identifiants sensibles.

---

## 125.1 Stockage

Il utilise :

- espace privé ;
- TTL ;
- autorisation ;
- audit ;
- suppression différée.

---

## 125.2 Notification

Une notification d’export ne contient pas directement le fichier.

---

## 125.3 Partage

Le fichier ne doit pas être envoyé par email ordinaire sans protection.

---

# 126. Règles pour les modules

Chaque module ajoutant une action sensible doit déclarer :

```text
AuditActionId

Category

Sensitivity

RequiredPermission

ReasonPolicy

MetadataSchema

TargetType

RetentionCategory
```

---

## 126.1 Tests

Le module fournit :

- succès ;
- échec ;
- refus ;
- non-divulgation ;
- transaction.

---

## 126.2 Interdiction

Le module ne doit pas écrire directement dans `audit_events`.

---

# 127. Règles pour les customizations

Une customization client utilise le contrat public d’audit.

---

## 127.1 Action client

Une Action ID client utilise un namespace explicite.

Exemple :

```text
client.cabinet-alpha.quote.approved
```

---

## 127.2 Interdictions

Elle ne doit pas :

- modifier un événement ;
- ajouter un secret ;
- contourner l’audit ;
- utiliser un dictionnaire non borné ;
- inventer une nouvelle rétention de production sans décision.

---

# 128. Règles pour LogicGate

LogicGate doit :

- cataloguer les actions ;
- limiter les métadonnées ;
- maintenir les permissions ;
- vérifier l’intégrité ;
- traiter les alertes ;
- définir la rétention ;
- préparer les exports ;
- gérer les restaurations ;
- revoir les break-glass.

---

## 128.1 LogicGate ne doit pas

- utiliser les logs comme seule piste ;
- ajouter des secrets dans les raisons ;
- donner `Audit.Read` à tous les administrateurs ;
- purger sans rapport ;
- réparer automatiquement une chaîne cassée ;
- masquer une action échouée ;
- promettre une inviolabilité absolue.

---

# 129. Règles pour Codex

Codex peut :

- définir les contrats ;
- créer le modèle PostgreSQL ;
- créer la chaîne ;
- ajouter les actions ;
- ajouter les tests ;
- créer l’interface de consultation ;
- produire les runbooks.

---

## 129.1 Codex ne doit pas

```text
Supprimer un événement réel

Modifier un événement réel

Désactiver la chaîne

Ajouter un secret

Exporter une piste de production

Purger la production

Créer un compte break-glass

Appliquer une migration production

Réparer automatiquement une rupture

Donner Audit.Read à tous les administrateurs

Inventer une durée juridique de rétention
```

---

## 129.2 Décision humaine

Une décision humaine est requise pour :

- nouvelle action critique ;
- changement de rétention ;
- purge ;
- export de production ;
- break-glass ;
- anchor externe ;
- modification du modèle d’intégrité ;
- collecte d’une nouvelle PII ;
- restauration ;
- correction de chaîne.

---

# 130. Anti-patterns interdits

## 130.1 Audit par message libre

```csharp
logger.LogInformation("Admin changed something");
```

---

## 130.2 Audit après commit

L’action réussit puis l’écriture d’audit échoue sans rollback.

---

## 130.3 Snapshot complet

L’entité avant et après est sérialisée intégralement.

---

## 130.4 Secret dans Metadata

Un token ou mot de passe est conservé pour « diagnostic ».

---

## 130.5 UPDATE d’événement

Une erreur de texte est corrigée en modifiant la ligne.

---

## 130.6 Suppression par l’admin

Un bouton permet de retirer un événement gênant.

---

## 130.7 Audit universel de chaque lecture

Chaque page vue produit un événement, rendant la piste inutilisable.

---

## 130.8 IP comme label métrique

Une série est créée par source réseau.

---

## 130.9 Export public

Un fichier CSV est placé dans `wwwroot`.

---

## 130.10 Hash sans canonicalisation

Deux représentations équivalentes produisent des hashes différents.

---

## 130.11 Restore sans nouvel epoch

La chaîne reprend silencieusement après retour dans le temps.

---

## 130.12 Superadmin universel

Le rôle administrateur reçoit automatiquement toutes les permissions d’audit.

---

# 131. Conséquences positives

La décision apporte :

- reconstitution des actions sensibles ;
- cohérence transactionnelle ;
- piste structurée ;
- détection des modifications ;
- meilleure réponse aux incidents ;
- séparation des logs ;
- contrôle des exports ;
- protection des secrets ;
- transfert client facilité ;
- meilleure compréhension des restaurations ;
- historique des permissions ;
- responsabilisation des opérations critiques.

---

# 132. Conséquences négatives

La décision entraîne :

- schéma supplémentaire ;
- catalogue d’actions ;
- metadata typée ;
- chaîne de hashes ;
- contention limitée ;
- jobs d’intégrité ;
- exports ;
- permissions ;
- rétention ;
- procédures break-glass ;
- tests supplémentaires ;
- responsabilité de traitement des alertes.

---

# 133. Risques acceptés

## 133.1 Administrateur PostgreSQL

Un administrateur très privilégié peut théoriquement réécrire la base et recalculer la chaîne.

Réduction :

- accès limité ;
- anchors externes ;
- backups ;
- audit opérateur ;
- séparation des rôles.

---

## 133.2 Contention

L’ajout sérialisé peut ajouter une petite latence aux actions sensibles.

---

## 133.3 Données personnelles

La piste contient des identifiants.

Réduction :

- minimisation ;
- accès limité ;
- rétention ;
- exports contrôlés.

---

## 133.4 Échec d’audit

Une panne de la piste peut bloquer certaines actions administratives.

Ce coût est accepté afin de ne pas créer d’actions critiques non traçables.

---

# 134. Mesures compensatoires

- append-only ;
- fonction d’append ;
- transaction ;
- epochs ;
- hash chain ;
- anchors ;
- permissions ;
- step-up ;
- raisons ;
- metadata typée ;
- rétention ;
- export privé ;
- intégrité programmée ;
- backups ;
- runbooks ;
- tests PostgreSQL.

---

# 135. Performance

## 135.1 Écriture

Une action sensible produit généralement un événement.

---

## 135.2 Consultation

Les requêtes utilisent :

- période ;
- pagination ;
- index ;
- filtres bornés.

---

## 135.3 Export

Les exports volumineux utilisent un job.

---

## 135.4 Vérification

Une vérification complète peut être exécutée en arrière-plan avec :

- batch ;
- progression ;
- rapport.

---

# 136. Coûts

## 136.1 Coûts supplémentaires

- stockage ;
- indexes ;
- développement ;
- intégrité ;
- export ;
- revue ;
- maintenance ;
- rétention.

---

## 136.2 Coûts évités

- enquête impossible ;
- contestation sans preuve technique ;
- changements de rôle non traçables ;
- restauration non documentée ;
- export abusif non détecté ;
- incident prolongé ;
- logs insuffisants.

---

# 137. Maintenabilité

Pour chaque action sensible, il doit être possible de répondre :

```text
Quel est son Action ID ?

Quelle permission l’autorise ?

Quel acteur l’a initiée ?

Quelle cible est affectée ?

Quelle raison est exigée ?

Quelle metadata est autorisée ?

Est-elle transactionnelle ?

Quel est son niveau de sensibilité ?

Quelle rétention s’applique ?

Comment est-elle testée ?
```

---

# 138. Compatibilité

## 138.1 Schema Version

Les événements possèdent une version.

---

## 138.2 Action IDs

Les identifiants restent stables.

---

## 138.3 Canonicalisation

La version de canonicalisation doit rester disponible pour vérifier les anciens événements.

---

## 138.4 Hash algorithm

Un changement d’algorithme crée un nouvel epoch ou une transition explicitement versionnée.

---

## 138.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration d’une ancienne piste de logs vers ce modèle nécessite une adoption explicite.

---

# 139. Migration depuis des logs

Le processus est :

```text
inventorier les actions sensibles
→ définir les Action IDs
→ définir les metadata
→ créer le schéma
→ ajouter l’écriture transactionnelle
→ ajouter les permissions
→ ajouter la chaîne
→ ajouter les tests
→ conserver les anciens logs selon leur rétention
```

---

## 139.1 Import historique

Les anciens logs ne doivent pas être transformés automatiquement en événements certifiés.

---

## 139.2 Marquage

Un import éventuel doit être identifié comme :

```text
legacy-import
```

avec une provenance et des garanties limitées.

---

# 140. Migration depuis des tables d’historique

Le processus peut :

```text
conserver les historiques métier
→ ajouter les événements d’audit futurs
→ relier les deux systèmes par Correlation ID
→ éviter une duplication complète
```

---

# 141. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- d’exporter une piste réelle ;
- de purger ;
- de modifier les permissions ;
- de créer un break-glass ;
- de restaurer une base ;
- de corriger une chaîne ;
- d’appliquer une migration de production.

---

# 142. Réversibilité

## 142.1 Classification

```text
élevée avant déploiement

moyenne après accumulation d’événements

faible pour les actions déjà réalisées
```

---

## 142.2 Backend futur

Les événements peuvent être exportés vers un service spécialisé.

---

## 142.3 Suppression de la piste

Retirer la piste réduirait significativement les garanties.

Un nouvel ADR serait nécessaire.

---

# 143. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `AuditEventId` ;
- `AuditActionId` ;
- catégories ;
- outcomes ;
- sensitivity ;
- actors ;
- targets.

---

## Étape 2 — Catalogue

Créer le catalogue machine-readable des actions.

---

## Étape 3 — Modèle PostgreSQL

Créer :

- events ;
- epochs ;
- chain state ;
- indexes ;
- migrations.

---

## Étape 4 — Append transactionnel

Implémenter la fonction contrôlée et la participation aux transactions applicatives.

---

## Étape 5 — Intégrité

Ajouter :

- canonicalisation ;
- Previous Hash ;
- Event Hash ;
- vérification ;
- alertes.

---

## Étape 6 — Premières actions

Couvrir :

- login administrateur ;
- rôles ;
- exports ;
- suppressions ;
- kill switches ;
- jobs opérateur.

---

## Étape 7 — Opérations

Ajouter :

- migrations ;
- backups ;
- restaurations ;
- déploiements ;
- secrets.

---

## Étape 8 — Consultation

Créer :

- permissions ;
- filtres ;
- pagination ;
- détail ;
- corrélation.

---

## Étape 9 — Exports et rétention

Ajouter :

- job d’export ;
- stockage privé ;
- purge contrôlée ;
- rapports.

---

## Étape 10 — Restore et break-glass

Créer :

- epochs ;
- anchors ;
- procédures ;
- tests ;
- runbooks.

---

# 144. Tâches liées proposées

```text
SF-TASK-220 — Define audit event and action contracts

SF-TASK-221 — Create the versioned audit action catalog

SF-TASK-222 — Add PostgreSQL append-only audit persistence

SF-TASK-223 — Add transactional audit event writes

SF-TASK-224 — Add audit hash-chain canonicalization

SF-TASK-225 — Add audit epoch and chain-state management

SF-TASK-226 — Add audit integrity verification

SF-TASK-227 — Audit identity, role and permission changes

SF-TASK-228 — Audit exports, deletions and publications

SF-TASK-229 — Audit feature flags and kill switches

SF-TASK-230 — Audit migrations, backups and restorations

SF-TASK-231 — Add protected audit consultation

SF-TASK-232 — Add private asynchronous audit exports

SF-TASK-233 — Add audit retention and purge workflows

SF-TASK-234 — Add break-glass audit procedures

SF-TASK-235 — Add audit restore-epoch handling

SF-TASK-236 — Add audit non-disclosure and integrity tests
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 145. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Les logs techniques et l’audit sont distincts.

Une action sensible possède un Action ID stable.

L’acteur et la cible sont identifiés.

Une raison est exigée lorsque nécessaire.

L’événement est écrit dans la transaction métier.

Un échec d’audit annule l’action sensible.

Le rôle runtime ne peut pas modifier un événement.

Le rôle runtime ne peut pas supprimer un événement.

Une correction produit un nouvel événement.

La chaîne détecte une modification.

La chaîne détecte une suppression.

Les écritures concurrentes conservent un ordre valide.

Une restauration crée un nouvel epoch.

Un export d’audit est privé, temporaire et audité.

Une purge est planifiée, confirmée et auditée.

Les rôles et permissions sont audités.

Les exports et suppressions sont audités.

Les kill switches sont audités.

Les migrations et restaurations sont auditées.

Les retries manuels de jobs sont audités.

Aucun secret n’apparaît dans les événements.

Aucun contenu métier complet n’apparaît dans les événements.

Audit.Read n’est pas accordé automatiquement aux administrateurs.

Une action critique exige une authentification récente.

Les tests transactionnels utilisent PostgreSQL réel.

Les tests fonctionnent sur Windows et Linux.
```

---

# 146. Conditions de révision

Réexaminer cette décision si :

- un SIEM central devient obligatoire ;
- une garantie WORM est exigée ;
- une signature externe devient nécessaire ;
- les volumes dépassent PostgreSQL ;
- plusieurs services doivent écrire dans une piste partagée ;
- une architecture multi-tenant est introduite ;
- les exigences réglementaires imposent une durée fixe ;
- l’impersonation administrateur est introduite ;
- une approbation à deux personnes devient obligatoire ;
- un stockage légal externe est adopté ;
- des audits cross-client doivent être corrélés ;
- le modèle de menace inclut un administrateur PostgreSQL hostile.

---

# 147. Indicateurs à suivre

- événements par catégorie ;
- événements critiques ;
- actions refusées ;
- échecs d’append ;
- latence d’append ;
- contention de chaîne ;
- ruptures d’intégrité ;
- exports ;
- consultations ;
- purges ;
- taille de la table ;
- durée de vérification ;
- usage break-glass ;
- événements sans raison attendue ;
- projets sans vérification récente ;
- anchors externes manquants.

---

# 148. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des actions sensibles réussissent sans audit

des événements sont modifiables

des secrets apparaissent dans Metadata

les exports sont publics

Audit.Read est accordé à tous

les purges ne produisent pas de rapport

la chaîne n’est jamais vérifiée

les restores ne créent pas d’epoch

les break-glass deviennent ordinaires

les Reason Text contiennent des données métier

les logs sont encore utilisés comme seule preuve

les événements critiques ne nécessitent pas de step-up
```

---

# 149. Conséquences observées

Cette section est append-only après mise en œuvre.

## 149.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
du schéma d’audit, de la chaîne d’intégrité
et des premières actions administratives sensibles.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- Action ID ;
- volume ;
- latence ;
- résultat d’intégrité ;
- incident ou bénéfice ;
- action proposée.

---

# 150. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](./0028-database-transaction-boundaries-and-concurrency-control.md)
- [0020-administrator-session-security-and-step-up-authentication.md](./0020-administrator-session-security-and-step-up-authentication.md)
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
- [0000-adr-template.md](./0000-adr-template.md)
- [0001-one-repository-and-database-per-client.md](./0001-one-repository-and-database-per-client.md)
- [0003-postgresql-as-primary-database.md](./0003-postgresql-as-primary-database.md)
- [0004-explicit-production-migrations.md](./0004-explicit-production-migrations.md)
- [0006-feature-flags-and-entitlements-separation.md](./0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](./0007-module-composition-and-public-contracts.md)
- [0009-client-customizations-and-extension-points.md](./0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](./0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](./0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](./0012-runtime-configuration-and-secrets.md)
- [0013-local-git-initialization-and-explicit-remote-publishing.md](./0013-local-git-initialization-and-explicit-remote-publishing.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](./0014-explicit-client-upgrades-and-version-pinning.md)
- [0016-content-and-media-storage-boundaries.md](./0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](./0017-background-jobs-and-reliable-side-effects.md)
- [0018-public-form-abuse-protection-and-rate-limiting.md](./0018-public-form-abuse-protection-and-rate-limiting.md)

---

# 151. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 146 |

---

# 152. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 153. Synthèse de la décision

```text
Audit
→ distinct des logs
→ distinct de l’historique métier
→ PostgreSQL dédié
→ append-only
→ accès limité

Événement
→ Action ID stable
→ acteur
→ cible
→ résultat
→ raison
→ metadata typée
→ corrélation
→ aucun secret

Transaction
→ action métier
→ audit
→ même commit
→ échec d’audit = rollback de l’action sensible

Intégrité
→ Sequence Number
→ Previous Event Hash
→ Event Hash
→ canonicalisation versionnée
→ vérification programmée
→ anchor externe possible

Restore
→ ancien epoch restauré
→ nouvel epoch
→ référence au Restore Report
→ rapprochement avec le dernier anchor

Actions couvertes
→ connexions administrateur
→ comptes
→ rôles
→ permissions
→ exports
→ suppressions
→ publications
→ feature flags
→ kill switches
→ jobs opérateur
→ migrations
→ backups
→ restaurations
→ déploiements
→ rotations de secrets

Consultation
→ Audit.Read
→ pagination
→ filtres
→ pas d’accès implicite pour tous les administrateurs

Export
→ permission dédiée
→ step-up
→ job
→ fichier privé
→ TTL
→ export lui-même audité

Rétention
→ profil explicite
→ aucune conservation indéfinie par défaut
→ purge planifiée
→ purge auditée

Actions critiques
→ confirmation renforcée
→ raison obligatoire
→ authentification récente
→ audit fail-closed
```

> Une piste d’audit utile ne consiste pas à conserver davantage de logs. Elle consiste à enregistrer de manière structurée, durable et vérifiable les décisions capables de modifier les accès, les données, la sécurité ou l’état opérationnel d’un projet.
