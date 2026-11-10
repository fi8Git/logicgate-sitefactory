---
adrId: ADR-0023
title: Govern data retention, deletion and privacy lifecycle through explicit versioned policies
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
  - ./0015-deterministic-generation-and-optional-ai.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0017-background-jobs-and-reliable-side-effects.md
  - ./0018-public-form-abuse-protection-and-rate-limiting.md
  - ./0019-audit-trail-and-sensitive-administrative-actions.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ./0021-administrator-provisioning-invitations-and-account-recovery.md
  - ./0022-browser-security-headers-and-content-security-policy.md
targetVersion: 0.1.0
---

# ADR-0023 — Cycle de vie, conservation, suppression et confidentialité des données

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le public ne livre pas encore de catalogue privacy opérationnel, rétention,
effacement, Legal Hold ou Deletion Ledger. Les structures et procédures
ci-dessous sont des obligations cibles pour des applications traitant ces
données, pas des traitements automatiques du moteur ou du runtime générique.
Le [périmètre actuel](../../README.md) ne contient aucune donnée métier cliente.
Aucun délai de rétention réel, consentement, purge ou réouverture après restore
n'est déduit d'un exemple ; réconciliation et validation humaine demeurent
obligatoires avant une future opération explicitement autorisée.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory impose une gestion explicite du cycle de vie des données.

Toute catégorie de données persistantes doit posséder une politique versionnée décrivant au minimum :

```text
finalité technique ou fonctionnelle

propriétaire fonctionnel

classification

événement de départ de conservation

durée ou règle de conservation

mode d’archivage

mode de suppression

mode d’anonymisation éventuel

dépendances

impact sur les backups

actions externes nécessaires

rôle autorisé

audit attendu
```

Les principes suivants constituent la baseline :

```text
aucune conservation indéfinie implicite

aucune suppression destructive sans plan

aucune suppression directe par simple changement d’état

aucune confusion entre archivage et effacement

aucune confusion entre pseudonymisation et anonymisation

aucune restauration remettant silencieusement en service
des données déjà supprimées

aucune donnée personnelle copiée dans les logs,
traces, métriques ou jobs sans nécessité explicite

aucun module ne conserve des données sans déclarer
leur cycle de vie
```

Les durées légales, contractuelles et métier exactes ne sont pas définies universellement par cet ADR.

Elles doivent être validées pour chaque projet par les responsables compétents avant la mise en production.

---

## 2. Résumé

Le modèle de référence est :

```text
Collecte
→ validation
→ classification
→ association à une finalité
→ définition de la date d’éligibilité
→ utilisation active
→ archivage éventuel
→ suppression ou anonymisation planifiée
→ propagation aux stockages et fournisseurs
→ vérification
→ rapport
→ audit
```

Les principaux composants sont :

```text
Privacy Data Catalog
→ décrit les catégories et leurs responsables

Retention Policy
→ décrit les règles de conservation

Privacy Subject
→ identifiant interne opaque reliant les données d’une personne

Deletion Request
→ demande ou opération de suppression contrôlée

Retention Hold
→ suspension explicite d’une purge

Deletion Marker
→ empêche une réapparition involontaire

Deletion Ledger
→ conserve un historique minimal des suppressions terminées

Retention Worker
→ évalue et exécute les politiques

Restore Reconciliation
→ réapplique les suppressions postérieures au backup restauré
```

La suppression complète peut nécessiter une coordination entre :

```text
PostgreSQL

media storage

exports

jobs et outbox

quarantaines

sessions et tokens

caches

backups

index externes

CRM ou autres fournisseurs

rapports temporaires
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Toute donnée persistante appartient à une catégorie déclarée.

Toute catégorie possède une politique de conservation.

L’absence de politique bloque la production
pour une donnée personnelle ou sensible.

L’archivage ne constitue pas une suppression.

Le soft delete ne constitue pas un effacement.

La désactivation d’un compte ne constitue pas une suppression.

La pseudonymisation réversible ne constitue pas une anonymisation.

Une suppression physique n’est pas déclenchée par une requête GET.

Une suppression sensible exige une autorisation serveur,
une confirmation, une raison, un step-up et un audit.

Une suppression ne s’appuie pas sur une cascade
non inspectée comme unique stratégie.

Une restauration ne remet pas le site en service
avant réconciliation des suppressions.

Un média supprimé n’est pas conservé dans une variante oubliée.

Un export expiré devient inaccessible puis est purgé.

Un job ne conserve pas indéfiniment une copie des données supprimées.

Un fournisseur externe est inclus dans le plan de suppression.

Un rapport de suppression ne contient pas les données supprimées.

Une politique nouvelle ne modifie pas rétroactivement
les données sans plan de migration ou recalcul explicite.
```

---

## 4. Contexte

Les projets SiteFactory peuvent conserver différentes catégories de données :

- contenus publics ;
- leads ;
- demandes de devis ;
- coordonnées ;
- messages de formulaires ;
- comptes administrateur ;
- rôles et permissions ;
- sessions ;
- invitations ;
- tokens de récupération ;
- médias publics ;
- documents privés ;
- quarantaines anti-abus ;
- jobs ;
- exports ;
- événements d’audit ;
- logs ;
- traces ;
- métriques ;
- backups ;
- rapports opérationnels ;
- données envoyées à des fournisseurs externes.

Ces données n’ont pas le même cycle de vie.

Exemples :

```text
Un contenu public
→ peut être conservé jusqu’à son retrait éditorial.

Un export contenant des leads
→ doit être temporaire.

Une invitation administrateur
→ possède une expiration courte.

Un événement d’audit
→ possède une politique distincte et renforcée.

Un fichier temporaire d’upload
→ doit disparaître rapidement.

Un backup
→ suit un cycle de rotation propre.

Un lead
→ peut être actif, archivé, anonymisé ou supprimé
selon sa finalité et la politique du projet.
```

Sans politique explicite, les données tendent à s’accumuler dans :

- PostgreSQL ;
- les volumes ;
- les backups ;
- les exports ;
- les dead letters ;
- les logs ;
- les systèmes externes.

Cette accumulation augmente :

- la surface d’exposition ;
- les coûts ;
- les volumes à transférer ;
- les difficultés de restauration ;
- les conséquences d’un incident ;
- le risque de réapparition de données supprimées.

---

## 5. Problème à résoudre

Comment conserver les données uniquement pendant la durée nécessaire tout en garantissant :

- l’exploitation normale du projet ;
- les besoins contractuels et réglementaires validés ;
- la traçabilité ;
- la suppression contrôlée ;
- l’anonymisation lorsque pertinente ;
- la cohérence entre plusieurs stockages ;
- la prise en compte des backups ;
- la prise en compte des fournisseurs externes ;
- la prévention des réimportations ;
- la possibilité de transfert client ?

La solution doit également éviter :

- les suppressions massives accidentelles ;
- les politiques implicites ;
- les purges non auditées ;
- les backups conservés indéfiniment ;
- les données supprimées réapparues après restauration ;
- les copies cachées dans les payloads de jobs ;
- les exports oubliés ;
- les quarantaines permanentes ;
- les identifiants personnels dans la télémétrie ;
- l’utilisation d’un soft delete comme réponse universelle ;
- une conservation « au cas où ».

---

## 6. Portée

Cette décision couvre :

- inventaire des données ;
- classification ;
- finalités ;
- politiques de conservation ;
- archivage ;
- anonymisation ;
- pseudonymisation ;
- suppression logique ;
- suppression physique ;
- suppression différée ;
- demandes de confidentialité ;
- données personnelles ;
- données sensibles ;
- leads ;
- comptes ;
- sessions ;
- contenus ;
- médias ;
- formulaires ;
- jobs ;
- audit ;
- exports ;
- logs ;
- traces ;
- métriques ;
- backups ;
- restaurations ;
- fournisseurs externes ;
- transfert ;
- clôture de service ;
- tests ;
- runbooks.

---

## 7. Hors portée

Cette décision ne définit pas :

- les durées légales applicables universellement ;
- la base juridique de chaque traitement ;
- l’identité juridique systématique du responsable de traitement ;
- un avis juridique ;
- un registre réglementaire complet ;
- un outil de consent management complet ;
- une politique sectorielle universelle ;
- un système d’archivage légal ;
- une conservation WORM obligatoire ;
- une plateforme centrale de privacy management ;
- la réponse juridique détaillée à chaque demande d’une personne ;
- une garantie que toute anonymisation est juridiquement irréversible dans tous les contextes.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Minimisation des données**
2. **Suppression réellement exécutable**
3. **Prévention de la réapparition après restauration**
4. **Protection contre les suppressions accidentelles**
5. **Politiques explicites et versionnées**
6. **Prise en compte de tous les stockages**
7. **Prise en compte des fournisseurs externes**
8. **Audit des actions sensibles**
9. **Isolation client et environnement**
10. **Traçabilité des décisions**
11. **Automatisation idempotente**
12. **Gestion des backups**
13. **Protection des données dans l’observabilité**
14. **Transfert client**
15. **Testabilité**
16. **Faible dépendance fournisseur**

---

## 9. Contraintes

La décision doit respecter :

```text
PostgreSQL

Un projet et une base par client

App_Data persistant

Jobs durables

Audit append-only

Step-up pour les opérations sensibles

UTC

Politiques versionnées

Suppression idempotente

Rapports sans secrets ni contenu supprimé

Aucune PII dans les labels métriques

Backups chiffrés et bornés

Restauration contrôlée

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- chaque module peut déclarer les données qu’il possède ;
- PostgreSQL reste la source principale des relations métier ;
- les suppressions lourdes peuvent être asynchrones ;
- certaines données doivent être anonymisées plutôt que supprimées ;
- certaines données doivent être temporairement conservées malgré une demande de suppression ;
- le projet peut utiliser des fournisseurs externes ;
- les backups ne permettent pas toujours un effacement sélectif sûr ;
- une restauration reste une opération rare et contrôlée ;
- le volume initial permet des jobs de rétention PostgreSQL planifiés.

---

# 11. Définitions

## 11.1 Donnée active

Donnée encore utilisée pour la finalité courante du projet.

---

## 11.2 Donnée archivée

Donnée retirée des parcours ordinaires, mais toujours conservée et accessible selon des permissions.

---

## 11.3 Soft delete

Marquage logique tel que :

```text
IsDeleted = true
```

sans suppression physique.

---

## 11.4 Suppression physique

Retrait des données du stockage actif concerné.

---

## 11.5 Pseudonymisation

Remplacement ou séparation d’un identifiant afin de réduire l’identification directe, avec possibilité de réassociation dans certains contextes.

---

## 11.6 Anonymisation

Transformation visant à rendre la réidentification non raisonnablement possible dans le périmètre retenu.

La validité de cette qualification dépend du contexte et ne doit pas être supposée uniquement parce qu’un email a été remplacé.

---

## 11.7 Privacy Subject

Identifiant interne opaque permettant de relier les données appartenant à une même personne dans les modules compatibles.

---

## 11.8 Data Category

Catégorie stable utilisée pour appliquer une politique de conservation.

---

## 11.9 Retention Anchor

Date ou événement à partir duquel la durée de conservation est calculée.

---

## 11.10 Purge Eligibility

Moment à partir duquel une donnée peut être supprimée ou anonymisée selon la politique.

---

## 11.11 Retention Hold

Suspension explicite d’une suppression planifiée.

---

## 11.12 Deletion Marker

Enregistrement minimal empêchant une réapparition ou réimportation involontaire.

---

## 11.13 Deletion Ledger

Historique minimal et opaque des suppressions terminées, notamment utilisé lors d’une restauration.

---

# 12. Option A — Conservation indéfinie

## 12.1 Description

Les données restent stockées tant qu’un opérateur ne les supprime pas manuellement.

---

## 12.2 Avantages

- implémentation simple ;
- aucune suppression accidentelle automatique ;
- historique complet ;
- faible nombre de jobs.

---

## 12.3 Inconvénients

- accumulation ;
- augmentation de la surface d’exposition ;
- coûts croissants ;
- backups lourds ;
- demandes de suppression difficiles ;
- transferts volumineux ;
- absence de politique.

---

## 12.4 Risques

- données anciennes compromises ;
- exports oubliés ;
- comptes inactifs conservés ;
- messages privés maintenus sans finalité ;
- impossibilité d’expliquer les durées.

---

## 12.5 Réversibilité

```text
moyenne
```

Une purge ultérieure devient plus complexe à mesure que les copies se multiplient.

---

# 13. Option B — Suppression manuelle par module

## 13.1 Description

Chaque module ajoute ses propres boutons et scripts de suppression.

---

## 13.2 Avantages

- logique proche du métier ;
- mise en œuvre progressive ;
- flexibilité locale ;
- faible infrastructure centrale.

---

## 13.3 Inconvénients

- politiques divergentes ;
- copies oubliées ;
- suppression incomplète ;
- absence de vue globale ;
- backups ignorés ;
- externalisations ignorées ;
- comportements différents selon le module.

---

## 13.4 Risques

- lead supprimé mais export conservé ;
- média restant ;
- job contenant une copie ;
- intégration externe non notifiée ;
- suppression non auditée.

---

## 13.5 Réversibilité

```text
moyenne
```

---

# 14. Option C — Suppression physique immédiate

## 14.1 Description

Toute demande de suppression efface immédiatement les lignes et fichiers associés.

---

## 14.2 Avantages

- données rapidement retirées ;
- peu d’états intermédiaires ;
- faible stockage ;
- implémentation apparemment directe.

---

## 14.3 Inconvénients

- erreurs irréversibles ;
- relations complexes ;
- jobs externes ;
- backups ;
- absence de délai de vérification ;
- suppression massive risquée ;
- conflits avec une conservation justifiée.

---

## 14.4 Risques

- mauvaise cible ;
- suppression de données encore nécessaires ;
- corruption référentielle ;
- incident non récupérable ;
- données recréées par une intégration.

---

## 14.5 Réversibilité

```text
faible
```

---

# 15. Option D — Cycle de vie gouverné par politiques

## 15.1 Description

Chaque catégorie possède une politique versionnée.

Les opérations utilisent :

- plan ;
- états ;
- jobs ;
- holds ;
- markers ;
- ledger ;
- vérification ;
- audit.

---

## 15.2 Avantages

- cohérence ;
- automatisation ;
- sécurité ;
- visibilité ;
- suppression multi-stockages ;
- gestion des backups ;
- gestion des restaurations ;
- gestion des fournisseurs ;
- transfert facilité ;
- réduction de la conservation excessive.

---

## 15.3 Inconvénients

- catalogue ;
- tables supplémentaires ;
- jobs ;
- tests ;
- réconciliation ;
- politiques à valider ;
- opérations plus longues ;
- besoin de responsables identifiés.

---

## 15.4 Risques

- politique mal configurée ;
- job incomplet ;
- hold oublié ;
- deletion marker conservé trop longtemps ;
- module non déclaré ;
- durée erronée.

---

## 15.5 Réversibilité

```text
élevée avant suppression physique
variable après suppression
```

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Indéfini | Manuel par module | Immédiat | Gouverné |
|---|---:|---:|---:|---:|---:|
| Minimisation | 5 | 1 | 3 | 5 | 5 |
| Sécurité contre les erreurs | 5 | 3 | 2 | 1 | 5 |
| Couverture multi-stockages | 5 | 1 | 2 | 2 | 5 |
| Gestion des backups | 5 | 1 | 1 | 2 | 5 |
| Audit | 4 | 2 | 3 | 3 | 5 |
| Automatisation | 4 | 1 | 2 | 4 | 5 |
| Transfert client | 4 | 2 | 3 | 3 | 5 |
| Maintenabilité | 5 | 1 | 2 | 2 | 5 |
| Réversibilité | 4 | 3 | 3 | 1 | 4 |
| Testabilité | 4 | 1 | 3 | 3 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons de gérer le cycle de vie des données avec :

```text
un catalogue de données

des politiques de conservation versionnées

des identifiants de sujets opaques

des demandes et plans de suppression

des holds explicites

des jobs idempotents

des markers anti-réapparition

un ledger de suppression

une réconciliation après restauration
```

Les durées spécifiques sont renseignées dans les profils clients et validées avant production.

---

## 17.1 Absence de valeur

Une catégorie personnelle ou sensible sans durée ni règle de conservation valide est refusée en production.

---

## 17.2 Nouvelle catégorie

Un module ne peut pas ajouter une donnée persistante sans déclarer son cycle de vie.

---

## 17.3 Changement de politique

Une nouvelle version ne recalcule pas silencieusement toutes les dates existantes.

Un plan explicite détermine :

- données affectées ;
- ancienne règle ;
- nouvelle règle ;
- nouvelles dates ;
- suppressions immédiates potentielles ;
- impacts ;
- rollback possible.

---

# 18. Privacy Data Catalog

Le catalogue machine-readable doit pouvoir déclarer :

```text
CategoryId

OwnerModuleId

Description

Classification

ContainsPersonalData

ContainsSensitiveData

PurposeIds

SubjectLinkStrategy

RetentionPolicyId

DeletionStrategy

AnonymizationStrategy

ExportStrategy

ExternalProcessors

BackupTreatment

AuditActionIds
```

---

## 18.1 Source

Le catalogue est versionné avec la plateforme et les modules.

---

## 18.2 Composition

Le générateur produit un catalogue résolu propre au projet client.

---

## 18.3 Rapport

La génération peut produire :

```text
privacy-data-catalog.json
```

sans inclure de valeurs métier.

---

# 19. Classification des données

Valeurs initiales recommandées :

```text
public

internal

confidential

personal

sensitive

secret
```

---

## 19.1 `public`

Donnée destinée à être publiée.

---

## 19.2 `internal`

Donnée technique ou fonctionnelle non publique.

---

## 19.3 `confidential`

Donnée client nécessitant un accès restreint.

---

## 19.4 `personal`

Donnée relative à une personne identifiable dans le contexte.

---

## 19.5 `sensitive`

Donnée dont l’exposition pourrait produire un impact renforcé.

---

## 19.6 `secret`

Credential ou matériel de sécurité, géré principalement par l’ADR-0012.

---

# 20. Finalités

Une donnée doit être associée à une ou plusieurs finalités déclarées.

Exemples techniques :

```text
lead-follow-up

administrator-access

security-monitoring

service-operation

content-publication

contract-handover
```

---

## 20.1 Texte libre

La finalité ne doit pas être uniquement un texte libre dispersé.

---

## 20.2 Arrêt de finalité

La fin d’une finalité peut déclencher :

- archivage ;
- recalcul de date ;
- suppression ;
- anonymisation.

---

## 20.3 Nouvelle utilisation

Une nouvelle utilisation des données nécessite une mise à jour explicite du catalogue et de la politique.

---

# 21. Catégories initiales

Le catalogue doit couvrir au minimum :

```text
public-content

lead-contact-data

lead-message-content

public-form-quarantine

administrator-identity

administrator-session

administrator-invitation

administrator-recovery-token

public-media

private-media

temporary-upload

background-job-payload

outbox-message

temporary-export

audit-event

technical-log

distributed-trace

operational-metric

backup-archive

deletion-ledger

retention-hold
```

---

## 21.1 Extension

Les modules peuvent ajouter des catégories avec un namespace stable.

Exemple :

```text
module.portfolio.project-customer-reference
```

---

# 22. Retention Policy

Une politique doit pouvoir contenir :

```text
PolicyId

PolicyVersion

CategoryId

RetentionAnchor

ActivePeriod

ArchivePeriod

TerminalAction

GracePeriod

HoldAllowed

DeletionVerification

BackupTreatment

ExternalPropagation

Owner

ReviewAt
```

---

## 22.1 Durées

Les durées peuvent utiliser le format ISO 8601 lorsque la règle est temporelle.

Exemples de syntaxe :

```text
P30D

P12M

P5Y
```

Ces exemples illustrent uniquement le format.

Ils ne constituent pas des durées recommandées universelles.

---

## 22.2 Règle événementielle

Une politique peut également utiliser :

```text
jusqu’à clôture du lead

jusqu’à expiration du token

jusqu’à fin du contrat

jusqu’à traitement de la demande

jusqu’à fin du délai de grâce
```

via des règles structurées.

---

# 23. Retention Anchor

Le point de départ doit être explicite.

Exemples :

```text
CreatedAtUtc

LastMeaningfulActivityAtUtc

ArchivedAtUtc

ClosedAtUtc

PurposeEndedAtUtc

ContractEndedAtUtc

InvitationExpiredAtUtc

ExportCreatedAtUtc
```

---

## 23.1 Activité significative

Une simple consultation ne doit pas prolonger automatiquement une durée de conservation métier.

---

## 23.2 Modification technique

Une migration ou mise à jour de colonne ne doit pas réinitialiser l’anchor.

---

## 23.3 UTC

Les dates sont conservées en UTC.

---

# 24. Date d’éligibilité

Chaque ressource concernée doit pouvoir déterminer :

```text
PurgeEligibleAtUtc
```

ou une règle permettant de la calculer.

---

## 24.1 Calcul

Le calcul dépend de :

- policy version ;
- anchor ;
- hold ;
- état ;
- dépendances.

---

## 24.2 Valeur inconnue

Une date impossible à déterminer doit produire un état de revue, pas une conservation silencieuse indéfinie.

---

# 25. Version de politique appliquée

Les données ou projections de rétention doivent conserver :

```text
RetentionPolicyId

RetentionPolicyVersion
```

---

## 25.1 Objectif

Permettre de savoir pourquoi une ressource est :

- conservée ;
- archivée ;
- supprimée ;
- bloquée.

---

## 25.2 Changement

Une migration de politique est auditée.

---

# 26. Cycle de vie générique

États conceptuels :

```text
active

archived

deletion-pending

anonymization-pending

held

deleted

anonymized
```

---

## 26.1 Modèles spécifiques

Tous les agrégats n’ont pas besoin d’exposer directement tous ces états.

La plateforme doit néanmoins pouvoir représenter leur situation de rétention.

---

## 26.2 État terminal

`deleted` et `anonymized` ne doivent pas permettre une réactivation ordinaire.

---

# 27. Archivage

L’archivage retire la donnée des parcours ordinaires.

---

## 27.1 Effets possibles

- masquage dans les listes actives ;
- réduction des permissions ;
- arrêt des notifications ;
- nouvelle date d’éligibilité ;
- stockage froid éventuel.

---

## 27.2 Limite

La donnée reste présente et doit être :

- protégée ;
- sauvegardée ;
- comptabilisée ;
- supprimée ultérieurement selon sa politique.

---

## 27.3 Audit

L’archivage d’une ressource sensible est audité selon l’ADR-0019.

---

# 28. Soft delete

Le soft delete peut être utilisé comme étape technique.

---

## 28.1 Usages

- délai de grâce ;
- restauration courte ;
- suppression asynchrone ;
- protection contre une erreur immédiate.

---

## 28.2 Interdiction

Il ne doit pas devenir l’état terminal universel.

---

## 28.3 Requêtes

Les filtres de soft delete doivent être testés afin d’éviter l’exposition involontaire des données supprimées logiquement.

---

# 29. Suppression physique

La suppression physique retire la donnée des stockages actifs ciblés.

---

## 29.1 Préconditions

```text
policy valide

date atteinte ou demande approuvée

aucun hold actif

plan validé

dépendances identifiées

backup treatment défini

external actions identifiées

audit disponible
```

---

## 29.2 Suppression transactionnelle

Les lignes d’une même base sont supprimées ou transformées dans des transactions courtes et bornées.

---

## 29.3 Gros volumes

Les suppressions massives utilisent :

- batches ;
- checkpoints ;
- limites ;
- pause ;
- métriques ;
- rapport.

---

# 30. Anonymisation

Une anonymisation transforme les champs identifiants tout en conservant certaines informations utiles.

---

## 30.1 Exemple conceptuel

Un lead historique peut conserver :

```text
date de création agrégée

statut final

catégorie de demande

source commerciale générale
```

tout en retirant :

```text
nom

email

téléphone

message libre

identifiants externes

liens vers des médias privés
```

---

## 30.2 Stratégie

Chaque anonymisation est définie champ par champ.

---

## 30.3 Texte libre

Les textes libres sont considérés comme potentiellement identifiants.

Ils ne doivent pas être conservés sans analyse.

---

## 30.4 Garantie

Remplacer uniquement l’email par une valeur fictive ne suffit pas automatiquement à rendre une ligne anonyme.

---

# 31. Pseudonymisation

La pseudonymisation peut servir à :

- limiter l’exposition ;
- analyser des volumes ;
- détecter des doublons ;
- appliquer un deletion marker ;
- rapprocher un restore.

---

## 31.1 Clé

Lorsqu’un HMAC est utilisé, sa clé suit l’ADR-0012.

---

## 31.2 Rotation

La rotation doit tenir compte :

- des comparaisons historiques ;
- du deletion ledger ;
- des restores ;
- de la période de transition.

---

## 31.3 Limite

Une donnée pseudonymisée reste soumise à sa politique tant qu’une réassociation raisonnable reste possible.

---

# 32. Privacy Subject

Les données personnelles doivent pouvoir être reliées à un identifiant interne :

```text
PrivacySubjectId
```

---

## 32.1 Format

Un UUID ou ULID opaque peut être utilisé.

---

## 32.2 Absence d’identité publique

Le Privacy Subject ID ne contient pas :

- email ;
- téléphone ;
- nom ;
- identifiant client lisible.

---

## 32.3 Leads anonymes

Une personne sans compte administrateur peut tout de même recevoir un Privacy Subject ID lors de la création du lead.

---

## 32.4 Plusieurs interactions

Les règles de rapprochement entre plusieurs interactions doivent être prudentes et documentées.

---

# 33. Subject Link Strategy

Chaque module déclare comment ses données sont reliées à un sujet.

Exemples :

```text
direct foreign key

association table

owned resource

derived reference

no subject link
```

---

## 33.1 JSON arbitraire

Rechercher un email dans du JSON ou du texte ne doit pas être la seule stratégie officielle.

---

## 33.2 Ressource partagée

Une ressource liée à plusieurs sujets nécessite une règle de suppression claire.

---

# 34. Privacy Data Descriptor des modules

Chaque module conservant des données doit déclarer :

```text
DataCategoryIds

SubjectLinks

RetentionPolicies

ExportHandlers

DeletionHandlers

AnonymizationHandlers

ExternalProcessors

BackupDependencies

DeletionOrder

VerificationHandlers
```

---

## 34.1 Quality gate

Un module personnel sans descriptor complet est refusé.

---

## 34.2 Customizations

Les customizations suivent le même contrat.

---

# 35. Demandes de confidentialité

Le système peut gérer des demandes telles que :

```text
access

export

rectification

restriction

deletion

objection
```

Le traitement juridique et la validation de l’identité restent définis hors de cet ADR.

---

## 35.1 Workflow technique

L’architecture fournit :

- enregistrement de la demande ;
- portée ;
- plan ;
- approbation ;
- exécution ;
- rapport ;
- audit.

---

# 36. Modèle de demande

Une demande doit pouvoir conserver :

```text
PrivacyRequestId

RequestType

PrivacySubjectId

Status

RequestedAtUtc

VerifiedAtUtc

PlannedAtUtc

ApprovedAtUtc

CompletedAtUtc

RejectedAtUtc

ReasonCode

PolicyVersion

RequestedByActorId

ApprovedByActorId

CorrelationId
```

---

## 36.1 Données de demande

Le texte libre et les pièces justificatives éventuelles doivent être minimisés et stockés dans un espace protégé.

---

## 36.2 Statuts

Valeurs recommandées :

```text
received

identity-verification-required

scope-review

planned

approval-required

approved

processing

blocked

partially-completed

completed

rejected

cancelled
```

---

# 37. Vérification de l’identité

Une demande affectant les données d’une personne ne doit pas être exécutée uniquement à partir d’un email librement fourni.

---

## 37.1 Processus

Le processus exact dépend :

- du type de compte ;
- du canal ;
- de la sensibilité ;
- du contrat client.

---

## 37.2 Données supplémentaires

La vérification ne doit pas collecter davantage de données que nécessaire.

---

## 37.3 Audit

Le résultat de vérification peut être audité sans conserver les secrets ou justificatifs complets.

---

# 38. Plan de suppression

Avant exécution, un plan doit indiquer :

```text
PrivacyRequestId

PrivacySubjectId

Categories

RecordCounts

MediaCounts

Exports

PendingJobs

ExternalProcessors

RetentionHolds

ProposedActions

BlockedActions

BackupTreatment

EstimatedImpact

PolicyVersions
```

---

## 38.1 Pas de contenu complet

Le plan ne copie pas les messages, documents ou valeurs personnelles.

---

## 38.2 Dry-run

La génération du plan est en lecture seule.

---

## 38.3 Expiration

Un plan est invalidé si :

- les données changent significativement ;
- un hold est ajouté ;
- la policy change ;
- la cible change ;
- la date maximale est dépassée.

---

# 39. Approbation

Une suppression sensible nécessite :

- permission dédiée ;
- step-up critique ;
- raison ;
- confirmation ;
- plan valide ;
- audit.

---

## 39.1 Auto-approbation

Le même opérateur peut exécuter et approuver dans le MVP lorsque le projet n’exige pas une séparation à deux personnes.

Cette situation reste visible dans l’audit.

---

## 39.2 Double approbation future

Un profil client peut exiger deux acteurs distincts.

---

# 40. Exécution asynchrone

Les suppressions multi-stockages utilisent l’ADR-0017.

Job Type conceptuel :

```text
privacy.subject-deletion.execute.v1
```

---

## 40.1 Idempotence

Le job doit pouvoir être rejoué.

---

## 40.2 Checkpoint

Chaque catégorie et fournisseur possède un état :

```text
pending

processing

completed

blocked

failed
```

---

## 40.3 Retry

Les erreurs transitoires sont réessayées.

Les erreurs permanentes deviennent visibles et bloquent la complétion globale.

---

# 41. Ordre de suppression

L’ordre doit être explicite.

Exemple conceptuel :

```text
révoquer les accès

désactiver les automatisations

annuler les jobs non nécessaires

supprimer les exports temporaires

retirer les liens fonctionnels

supprimer ou anonymiser les données métier

supprimer les médias

propager aux fournisseurs

créer les markers

mettre à jour le ledger

produire le rapport
```

---

## 41.1 Dépendances

Le graphe de dépendances est fourni par les modules.

---

## 41.2 Cascade SQL

Une cascade peut être utilisée pour une relation connue et testée.

Elle ne remplace pas le plan global.

---

# 42. Suppression d’un administrateur

Une demande concernant un administrateur doit notamment traiter :

- compte Identity ;
- rôles ;
- sessions ;
- invitations ;
- tokens ;
- MFA ;
- exports ;
- documents privés créés ;
- événements d’audit ;
- données métier éventuelles.

---

## 42.1 Sessions

Toutes les sessions sont révoquées avant la suppression ou anonymisation.

---

## 42.2 Audit

Les événements d’audit ne sont pas supprimés automatiquement.

Leur acteur peut être réduit à un identifiant opaque selon la politique applicable, tout en préservant l’intégrité de la chaîne.

---

## 42.3 Dernier administrateur

La suppression du dernier administrateur reste soumise aux protections de l’ADR-0021.

---

# 43. Suppression d’un lead

Le plan doit notamment examiner :

- coordonnées ;
- message ;
- historique métier ;
- notes ;
- médias ;
- exports ;
- CRM ;
- notifications ;
- quarantaines ;
- jobs ;
- audit.

---

## 43.1 Anonymisation possible

Un historique commercial agrégé peut être conservé lorsque la politique validée le prévoit et que l’anonymisation est suffisante.

---

## 43.2 Notification antérieure

Un email déjà envoyé ne peut pas être retiré du système du destinataire par SiteFactory.

Cette limite doit être documentée.

---

# 44. Contenus publics

Un contenu public peut contenir des données personnelles.

Exemples :

- photo ;
- témoignage ;
- nom ;
- fonction ;
- coordonnées.

---

## 44.1 Retrait

La suppression doit couvrir :

- contenu PostgreSQL ;
- média ;
- variantes ;
- cache ;
- sitemap ;
- données structurées ;
- CDN futur.

---

## 44.2 Moteurs de recherche

Le retrait du site ne garantit pas la disparition immédiate des caches ou index externes.

Cette limite est documentée.

---

# 45. Médias

La suppression d’un média suit l’ADR-0016.

---

## 45.1 Couverture

- original ;
- variantes ;
- miniature ;
- quarantaine ;
- copie temporaire ;
- backup selon sa rotation ;
- CDN futur.

---

## 45.2 Références

Un média partagé n’est pas supprimé tant qu’une référence autorisée reste active, sauf plan de remplacement.

---

# 46. Quarantaines anti-abus

Les soumissions mises en quarantaine possèdent une rétention courte et explicite.

---

## 46.1 Absence de traitement

Une quarantaine non revue ne doit pas devenir une conservation indéfinie.

---

## 46.2 Rejet

Après rejet, la politique détermine :

- suppression ;
- conservation minimale d’un fingerprint ;
- durée de blocage éventuelle.

---

## 46.3 Contenu libre

Le message complet doit être supprimé avant ou avec la fin de la période prévue.

---

# 47. Sessions et tokens

Les éléments suivants possèdent des durées courtes déjà définies dans les ADR-0020 et ADR-0021 :

- sessions ;
- invitations ;
- resets ;
- recovery tokens ;
- acceptance contexts ;
- step-up grants.

---

## 47.1 Nettoyage

L’expiration logique doit être suivie d’un nettoyage physique borné.

---

## 47.2 Recovery Epoch

Une rotation d’epoch invalide les tokens, mais ne remplace pas leur purge ultérieure.

---

# 48. Jobs et outbox

Les payloads de jobs ne doivent pas devenir une copie persistante durable des données supprimées.

---

## 48.1 Avant suppression

Le plan recherche :

- jobs pending ;
- dead letters ;
- outbox non dispatchée ;
- snapshots ;
- exports en cours.

---

## 48.2 Actions

Selon le contrat :

- annuler ;
- anonymiser ;
- laisser terminer ;
- bloquer la suppression ;
- remplacer le payload par une référence neutralisée.

---

## 48.3 Jobs terminés

La rétention des jobs réussis et échoués est bornée.

---

# 49. Exports

Un export de données personnelles est :

- privé ;
- temporaire ;
- soumis à TTL ;
- autorisé ;
- audité ;
- supprimé après expiration.

---

## 49.1 Expiration logique

Après expiration, l’URL ou le téléchargement devient immédiatement invalide.

---

## 49.2 Suppression physique

Le blob est ensuite supprimé par un job.

---

## 49.3 Notification

Une notification ne doit pas contenir le fichier en pièce jointe par défaut.

---

# 50. Logs techniques

Les logs ne doivent pas contenir les données métier complètes.

---

## 50.1 Baseline

Interdire notamment :

- message de lead ;
- email complet lorsque non nécessaire ;
- téléphone ;
- token ;
- contenu de document ;
- chaîne de connexion.

---

## 50.2 Rétention

La rétention des logs est :

- finie ;
- distincte par environnement ;
- adaptée à l’exploitation ;
- validée par le projet.

---

## 50.3 Suppression individuelle

Une suppression sélective dans un backend de logs peut être difficile.

La minimisation à l’écriture constitue donc le contrôle principal.

---

# 51. Traces distribuées

Les spans ne doivent pas contenir :

- corps de requête ;
- formulaire complet ;
- documents ;
- tokens ;
- URLs signées ;
- query strings sensibles.

---

## 51.1 Attributs

Utiliser des catégories bornées et identifiants techniques non personnels.

---

## 51.2 Rétention

La rétention des traces est bornée et généralement plus courte que celle de l’audit.

---

# 52. Métriques

Les métriques ne doivent pas utiliser comme labels :

- email ;
- téléphone ;
- User ID ;
- Lead ID ;
- Media ID ;
- Session ID ;
- IP ;
- Trace ID ;
- Privacy Subject ID.

---

## 52.1 Rétention

La rétention des métriques est définie par le backend d’observabilité.

---

## 52.2 Suppression individuelle

Les métriques correctement conçues ne doivent pas nécessiter de suppression ciblée par personne.

---

# 53. Piste d’audit

La piste d’audit suit l’ADR-0019.

---

## 53.1 Conservation distincte

La suppression d’une ressource ne supprime pas automatiquement les événements d’audit liés.

---

## 53.2 Minimisation

L’audit conserve :

- identifiants opaques ;
- action ;
- résultat ;
- raison structurée.

Il ne conserve pas les données métier complètes.

---

## 53.3 Pseudonymisation de l’acteur

Après suppression d’un compte, l’interface peut afficher :

```text
deleted-user
```

tout en conservant l’identifiant technique requis par la chaîne.

---

## 53.4 Purge

La purge d’audit suit sa propre politique, ses epochs et son audit.

---

# 54. Retention Hold

Un hold suspend temporairement une suppression.

---

## 54.1 Modèle

Un hold doit pouvoir conserver :

```text
RetentionHoldId

Scope

PrivacySubjectId éventuel

CategoryIds

ReasonCode

ReasonText

CreatedByActorId

CreatedAtUtc

ExpiresAtUtc éventuel

ReviewAtUtc

Status
```

---

## 54.2 Raisons

Exemples structurés :

```text
SECURITY_INCIDENT

ACTIVE_DISPUTE

CONTRACTUAL_REVIEW

DATA_INTEGRITY_INVESTIGATION

OPERATIONAL_RECOVERY
```

---

## 54.3 Aucun hold implicite

Un hold possède :

- propriétaire ;
- raison ;
- périmètre ;
- revue ;
- expiration ou justification d’absence d’expiration.

---

## 54.4 Audit

Création, prolongation et retrait sont audités.

---

# 55. Priorité des règles

Ordre conceptuel :

```text
hold valide

obligation de conservation validée

demande de suppression approuvée

politique de rétention normale
```

---

## 55.1 Documentation

Un blocage doit être visible dans le plan de suppression.

---

## 55.2 Pas de refus silencieux

Une demande bloquée par un hold reçoit un état :

```text
blocked
```

avec une raison interne et un suivi.

---

# 56. Deletion Marker

Un marker minimal peut empêcher :

- une réimportation ;
- une resynchronisation ;
- une recréation depuis une source externe ;
- une réactivation après restore.

---

## 56.1 Contenu

Exemple conceptuel :

```text
DeletionMarkerId

PrivacySubjectId

CategoryScope

CreatedAtUtc

ExpiresAtUtc éventuel

ReasonCode

PolicyVersion
```

---

## 56.2 Identifiants externes

Un HMAC d’un identifiant normalisé peut être conservé lorsque nécessaire pour détecter une réimportation.

---

## 56.3 Minimisation

Le marker ne contient pas :

- email en clair ;
- téléphone ;
- message ;
- document ;
- nom.

---

## 56.4 Rétention

Le marker possède lui-même une politique de conservation.

---

# 57. Deletion Ledger

Le ledger conserve un historique minimal des suppressions terminées.

---

## 57.1 Objectifs

- réconciliation après restauration ;
- vérification ;
- prévention de réapparition ;
- transfert ;
- audit technique.

---

## 57.2 Contenu

```text
DeletionLedgerEntryId

PrivacyRequestId

PrivacySubjectId

CompletedAtUtc

CategoryScope

PolicyVersion

DeletionReportId

LedgerSequence
```

---

## 57.3 Données interdites

Le ledger ne contient pas :

- email ;
- téléphone ;
- nom ;
- message ;
- token ;
- contenu supprimé.

---

# 58. Checkpoint externe du ledger

Une copie minimale et protégée du ledger doit être conservée hors du point-in-time principal de PostgreSQL.

---

## 58.1 Raison

Un backup ancien de PostgreSQL ne contient pas les suppressions réalisées après sa création.

---

## 58.2 Checkpoint

Le checkpoint peut contenir :

```text
ClientSlug

Environment

LastLedgerSequence

LastLedgerHash

CreatedAtUtc

ApplicationVersion
```

---

## 58.3 Entrées nécessaires

Les entrées postérieures au backup doivent être disponibles afin de réappliquer les suppressions après restauration.

---

## 58.4 Stockage

Le checkpoint et les deltas utilisent un stockage :

- chiffré ;
- contrôlé ;
- séparé ;
- soumis à rétention ;
- sans données personnelles directes.

---

# 59. Backups

Les backups suivent l’ADR-0023 et le document `23-backup-and-restore.md`.

---

## 59.1 Politique bornée

Chaque catégorie de backup possède :

- fréquence ;
- durée ;
- rotation ;
- chiffrement ;
- propriétaire ;
- procédure de destruction.

---

## 59.2 Conservation indéfinie

Une conservation indéfinie de backups n’est pas la baseline.

---

## 59.3 Suppression sélective

Modifier chaque backup historique pour retirer une donnée peut :

- compromettre l’intégrité ;
- être techniquement risqué ;
- rendre les restores non fiables.

La baseline privilégie :

```text
rotation bornée
+ accès fortement limité
+ aucune remise en production directe
+ réapplication du deletion ledger après restore
```

---

# 60. Données supprimées dans les backups

Une donnée supprimée peut subsister jusqu’à l’expiration d’un backup existant.

---

## 60.1 Restrictions

Pendant cette période :

- le backup reste chiffré ;
- il n’est pas utilisé comme source fonctionnelle ;
- il n’est accessible qu’aux opérations de reprise ;
- toute restauration est contrôlée ;
- la suppression est réappliquée avant réouverture.

---

## 60.2 Documentation

Cette limite doit être décrite dans les politiques opérationnelles du projet.

---

# 61. Restauration

Une restauration suit :

```text
isoler l’environnement

arrêter les workers

restaurer PostgreSQL et les médias

créer de nouveaux epochs de sécurité

charger le dernier deletion ledger externe

identifier les suppressions postérieures au backup

réappliquer les suppressions

réconcilier les fournisseurs externes

vérifier

produire un rapport

ouvrir progressivement le service
```

---

## 61.1 Interdiction

Le site ne doit pas être rendu accessible publiquement immédiatement après la simple restauration technique.

---

## 61.2 Deletion Ledger absent

L’absence d’un ledger suffisamment récent constitue un risque bloquant à analyser.

---

## 61.3 Rapports

La restauration référence :

- Backup ID ;
- dernier Ledger Sequence disponible ;
- suppressions réappliquées ;
- anomalies ;
- opérateur ;
- audit.

---

# 62. Données restaurées déjà supprimées

Lorsqu’une entrée du ledger correspond à une donnée présente dans le backup restauré :

```text
réexécuter la stratégie de suppression
```

---

## 62.1 Idempotence

La stratégie doit accepter que certaines ressources aient déjà disparu.

---

## 62.2 Fournisseurs externes

Une suppression externe déjà réalisée ne doit pas être annulée par la restauration locale.

---

# 63. Fournisseurs externes

Chaque intégration doit déclarer :

```text
ProcessorId

DataCategories

IdentifiersSent

DeletionCapability

DeletionEndpointOrProcedure

RetentionInformation

VerificationMethod

FailurePolicy

Owner
```

---

## 63.1 Aucun fournisseur caché

Un module ne doit pas envoyer des données à un service non déclaré.

---

## 63.2 Suppression

La demande locale crée une action externe durable.

---

## 63.3 Confirmation

La suppression n’est considérée complète que lorsque :

- le fournisseur confirme ;
- ou une procédure documentée explique la limite ou l’impossibilité.

---

# 64. Propagation externe

Le workflow utilise l’ADR-0017.

Job Type conceptuel :

```text
privacy.external-deletion.dispatch.v1
```

---

## 64.1 Idempotency key

Exemple :

```text
{PrivacyRequestId}:{ProcessorId}:{CategoryId}
```

---

## 64.2 Erreur

Une erreur permanente place la demande en :

```text
partially-completed
```

ou :

```text
blocked
```

---

## 64.3 Dead-letter

Un dead-letter de suppression externe déclenche une alerte prioritaire.

---

# 65. Synchronisations entrantes

Une intégration ne doit pas recréer une donnée supprimée lors du prochain import.

---

## 65.1 Contrôles

- deletion marker ;
- version de synchronisation ;
- filtre ;
- suppression côté source ;
- revue des conflits.

---

## 65.2 Réimportation légitime

Une nouvelle interaction légitime après suppression peut créer un nouveau sujet ou une nouvelle finalité selon les règles validées.

Le système ne doit pas utiliser un marker permanent sans politique.

---

# 66. Caches

Une suppression doit examiner :

- cache mémoire ;
- cache distribué futur ;
- CDN ;
- pages statiques ;
- cache navigateur lorsque contrôlable ;
- projections.

---

## 66.1 Invalidation

L’invalidation du cache fait partie du workflow.

---

## 66.2 Limite navigateur

Un cache déjà présent sur un poste tiers ne peut pas toujours être retiré par l’application.

---

# 67. Index de recherche

Un futur index de recherche doit déclarer :

- catégories indexées ;
- identifiants ;
- suppression ;
- réconciliation ;
- rétention.

---

## 67.1 Suppression

Le retrait de PostgreSQL doit produire un effet de suppression d’index idempotent.

---

# 68. Rapports opérationnels

Les rapports ne doivent pas contenir les données supprimées.

---

## 68.1 Contenu autorisé

```text
PrivacyRequestId

CategoryId

RecordCount

BlobCount

ProcessorStatus

StartedAtUtc

CompletedAtUtc

Result

ErrorCodes
```

---

## 68.2 Contenu interdit

```text
Email

Nom

Téléphone

Message

Document

Token

Secret

Payload complet
```

---

# 69. Rapport de suppression

Une suppression produit :

```text
privacy-deletion-report.json
```

---

## 69.1 Exemple conceptuel

```json
{
  "schemaVersion": 1,
  "privacyRequestId": "5b49e9e8-e868-4fc3-bf01-583d1eac84a7",
  "privacySubjectId": "01J...",
  "policyVersion": "privacy.standard-b2b.v1",
  "startedAtUtc": "2026-07-29T10:00:00Z",
  "completedAtUtc": "2026-07-29T10:02:18Z",
  "categories": [
    {
      "categoryId": "lead-contact-data",
      "action": "anonymized",
      "recordCount": 1,
      "status": "completed"
    }
  ],
  "externalProcessors": [],
  "backupTreatment": "reapply-after-restore",
  "ledgerSequence": 84,
  "status": "completed"
}
```

---

## 69.2 Protection

Le rapport est :

- privé ;
- autorisé ;
- soumis à rétention ;
- audité ;
- sans contenu supprimé.

---

# 70. Vérification de suppression

La complétion exige des contrôles.

---

## 70.1 Vérifications locales

- lignes absentes ou anonymisées ;
- relations retirées ;
- blobs absents ;
- variantes absentes ;
- exports absents ;
- sessions révoquées ;
- jobs neutralisés ;
- caches invalidés.

---

## 70.2 Vérifications externes

- statut du fournisseur ;
- identifiant de confirmation ;
- aucune resynchronisation ;
- aucune erreur permanente.

---

## 70.3 Recherche par marqueurs

Les tests de non-divulgation peuvent utiliser des valeurs marqueurs dans un environnement de test.

---

# 71. Suppression partielle

Une demande peut être partiellement complétée lorsque :

- un hold bloque une catégorie ;
- un fournisseur échoue ;
- un backup reste dans sa période ;
- une donnée doit être conservée selon une règle validée.

---

## 71.1 Transparence technique

Le statut et la raison sont enregistrés.

---

## 71.2 Aucun statut trompeur

Une demande n’est pas marquée `completed` si un composant obligatoire reste en échec.

---

# 72. Suppression différée

Une période de grâce peut être utilisée pour :

- corriger une erreur ;
- confirmer une suppression ;
- coordonner des dépendances ;
- préparer un export.

---

## 72.1 Accès

Pendant le délai :

- la donnée peut être masquée ;
- l’accès peut être limité ;
- les effets sortants peuvent être arrêtés.

---

## 72.2 Réversibilité

La restauration avant purge nécessite :

- permission ;
- raison ;
- confirmation ;
- audit.

---

# 73. Suppressions massives

Une opération dépassant un seuil doit exiger une procédure renforcée.

---

## 73.1 Plan

Le plan indique :

- nombre de sujets ;
- catégories ;
- volumes ;
- blobs ;
- fournisseurs ;
- durée estimée ;
- seuils.

---

## 73.2 Confirmation

La confirmation doit reprendre :

```text
client
environnement
nombre de sujets
nombre de ressources
action terminale
```

---

## 73.3 Step-up

Toujours critique.

---

## 73.4 Approbation supplémentaire

Un profil peut exiger un second acteur.

---

# 74. Suppression par SQL manuel

La suppression directe par SQL n’est pas le workflow ordinaire.

---

## 74.1 Risques

- dépendances ignorées ;
- médias oubliés ;
- audit absent ;
- ledger absent ;
- providers non notifiés ;
- backups non traités.

---

## 74.2 Urgence

Une correction break-glass exige :

- plan ;
- backup ;
- script revu ;
- rapport ;
- audit de réconciliation ;
- mise à jour du ledger.

---

# 75. Règles de contraintes PostgreSQL

La baseline privilégie :

```text
ON DELETE RESTRICT
```

pour les relations importantes tant qu’une cascade n’est pas explicitement validée.

---

## 75.1 Cascade

Une cascade peut être utilisée lorsque :

- relation strictement owned ;
- aucun effet externe ;
- tests ;
- plan connu ;
- audit global conservé.

---

## 75.2 `SET NULL`

Utilisable lorsqu’une relation historique doit rester sans identifier directement la cible.

---

# 76. Nettoyage des données temporaires

Les données temporaires utilisent des politiques courtes.

Exemples :

- uploads incomplets ;
- Acceptance Contexts ;
- Delivery Secrets ;
- exports ;
- fichiers de traitement ;
- caches de proposition AI ;
- fichiers de diagnostic.

---

## 76.1 Job

Un job récurrent exécute le nettoyage.

---

## 76.2 Orphelins

Les orphelins sont détectés avant suppression définitive.

---

# 77. Données AI

Conformément à l’ADR-0015 :

- prompts ;
- sources ;
- réponses ;
- propositions ;
- caches ;

possèdent une rétention distincte.

---

## 77.1 Contenu accepté

Le contenu accepté suit ensuite la politique de contenu normale.

---

## 77.2 Proposition rejetée

Une proposition rejetée ne doit pas être conservée indéfiniment.

---

## 77.3 Provider externe

La rétention du fournisseur doit être documentée avant l’usage.

---

# 78. Données de sécurité

Les éléments tels que :

- fingerprints ;
- denylist ;
- alertes ;
- lockouts ;
- violations CSP ;
- signaux d’abus ;

possèdent une finalité de sécurité et une rétention bornée.

---

## 78.1 Données brutes

Les IP ou URLs complètes sont évitées lorsque des formes agrégées ou pseudonymisées suffisent.

---

## 78.2 Incident actif

Un incident peut justifier un hold explicite.

---

# 79. Privacy by default

Les valeurs par défaut doivent :

- collecter moins ;
- conserver moins ;
- ne pas activer un fournisseur externe ;
- ne pas journaliser les corps ;
- ne pas créer d’export permanent ;
- ne pas conserver d’IP brute ;
- ne pas permettre un upload public sans besoin.

---

# 80. Minimisation à la collecte

Un formulaire ne doit pas demander un champ uniquement parce qu’il pourrait être utile ultérieurement.

---

## 80.1 Champs facultatifs

Chaque champ doit posséder :

- purpose ;
- validation ;
- owner ;
- retention category.

---

## 80.2 Texte libre

Les textes libres sont limités car ils peuvent contenir des informations inattendues.

---

# 81. Modification du schéma

Ajouter une nouvelle propriété personnelle nécessite :

```text
catalogue mis à jour

purpose déclaré

policy définie

export handler mis à jour

deletion handler mis à jour

tests ajoutés

documentation mise à jour
```

---

## 81.1 Migration

Une migration ne doit pas remplir automatiquement le champ avec une donnée issue d’une autre source sans décision.

---

# 82. Accès aux politiques

La consultation des politiques peut être accordée à :

```text
Privacy.View

Operations.View
```

selon le projet.

---

## 82.1 Modification

La modification de politique nécessite :

- permission dédiée ;
- step-up ;
- raison ;
- plan ;
- audit ;
- release ou configuration contrôlée.

---

## 82.2 Administrateur fonctionnel

Il ne peut pas prolonger arbitrairement la conservation d’une catégorie sensible.

---

# 83. Interface de gestion

Une future interface peut afficher :

- catégories ;
- policies ;
- données arrivant à échéance ;
- holds ;
- demandes ;
- jobs ;
- erreurs ;
- rapports ;
- état du ledger.

---

## 83.1 Données

Les dashboards utilisent des comptes et agrégats.

Ils ne listent pas toutes les données personnelles sans permission.

---

# 84. Commandes opérateur

Commandes conceptuelles :

```text
sitefactory privacy catalog validate

sitefactory privacy retention plan

sitefactory privacy retention apply

sitefactory privacy request plan

sitefactory privacy request apply

sitefactory privacy request status

sitefactory privacy hold create

sitefactory privacy hold release

sitefactory privacy ledger verify

sitefactory privacy restore reconcile
```

---

## 84.1 Effets

Les commandes `plan` sont en lecture seule.

---

## 84.2 Apply

Les commandes destructives exigent :

- client ;
- environnement ;
- plan ;
- confirmation ;
- step-up ou contexte opérateur ;
- raison.

---

# 85. Retention Worker

Le worker évalue périodiquement les données éligibles.

---

## 85.1 Étapes

```text
scanner les dates éligibles

exclure les holds

produire un batch

verrouiller les ressources

exécuter l’action terminale

vérifier

mettre à jour le ledger

auditer le batch
```

---

## 85.2 Pas de scan complet permanent

Les tables possèdent des index sur les dates d’éligibilité et états.

---

## 85.3 Taille du batch

Elle est bornée et configurable.

---

# 86. Tâches récurrentes

Exemples :

```text
privacy.retention.evaluate.v1

privacy.temporary-data.cleanup.v1

privacy.deletion-marker.cleanup.v1

privacy.ledger.checkpoint.v1

privacy.external-deletion.reconcile.v1
```

---

## 86.1 Chevauchement

La politique par défaut est :

```text
forbid
```

pour une même catégorie et environnement.

---

## 86.2 Rattrapage

Après une interruption :

```text
bounded-catch-up
```

afin d’éviter une suppression massive immédiate non surveillée.

---

# 87. Kill switch

Les purges automatiques doivent pouvoir être suspendues.

Exemple conceptuel :

```text
privacy.retention.automatic-purge.disabled
```

---

## 87.1 Effet

Le kill switch :

- suspend les nouvelles purges ;
- ne réactive pas les données ;
- ne supprime pas les plans ;
- produit une alerte.

---

## 87.2 Activation

Elle est critique et auditée.

---

# 88. Observabilité

Événements recommandés :

```text
RetentionEvaluationStarted

RetentionCandidateDetected

RetentionHoldApplied

RetentionActionStarted

RetentionActionCompleted

RetentionActionFailed

PrivacyRequestReceived

PrivacyRequestPlanned

PrivacyRequestApproved

PrivacyRequestCompleted

PrivacyRequestPartiallyCompleted

DeletionMarkerCreated

DeletionLedgerUpdated

RestoreDeletionReconciliationStarted

RestoreDeletionReconciliationCompleted
```

---

## 88.1 Données autorisées

```text
PolicyId

PolicyVersion

CategoryId

RequestType

Action

Result

RecordCountBucket

ErrorCode

Environment

Duration
```

---

## 88.2 Données interdites

```text
PrivacySubjectId dans les métriques

Email

Téléphone

Nom

Message

Document

Token

Secret

Valeur supprimée
```

---

# 89. Métriques

Exemples :

```text
retention candidates

retention actions completed

retention actions failed

privacy requests open

privacy requests blocked

deletion jobs pending

external deletion failures

deletion ledger lag

expired exports

active retention holds
```

---

## 89.1 Labels autorisés

```text
category_id

policy_id

action

result

environment

processor_id borné
```

---

## 89.2 Labels interdits

```text
PrivacyRequestId

PrivacySubjectId

Email

UserId

LeadId

MediaId

TraceId

IP
```

---

# 90. Alertes

Alertes possibles :

```text
Politique manquante

Purge en échec

Suppression externe en dead-letter

Ledger non checkpointé

Données éligibles non traitées

Hold expiré non revu

Export expiré encore accessible

Backup dépassant sa rétention

Restore sans réconciliation

Suppression massive inattendue
```

---

## 90.1 Runbook

Chaque alerte critique doit référencer une procédure.

---

# 91. Audit

Actions recommandées :

```text
privacy.policy.changed

privacy.request.created

privacy.request.approved

privacy.request.rejected

privacy.request.completed

privacy.retention-hold.created

privacy.retention-hold.extended

privacy.retention-hold.released

privacy.mass-deletion.started

privacy.mass-deletion.completed

privacy.restore-reconciliation.completed

privacy.ledger.checkpoint.created
```

---

## 91.1 Données

L’audit conserve :

- IDs opaques ;
- catégorie ;
- action ;
- résultat ;
- raison ;
- acteur.

Il ne conserve pas le contenu supprimé.

---

# 92. Rétention des demandes de confidentialité

Une demande possède elle-même une politique.

---

## 92.1 Après clôture

Les données justificatives et textes libres sont supprimés ou minimisés selon la politique validée.

---

## 92.2 Historique minimal

Un historique technique peut conserver :

- Request ID ;
- type ;
- dates ;
- résultat ;
- policy ;
- rapport ;
- acteur.

---

# 93. Sécurité

## 93.1 Actifs

- données personnelles ;
- documents ;
- comptes ;
- exports ;
- backups ;
- markers ;
- ledger ;
- policies ;
- rapports ;
- fournisseurs externes.

---

## 93.2 Menaces

- conservation excessive ;
- suppression accidentelle ;
- suppression de la mauvaise cible ;
- restauration réintroduisant des données ;
- export oublié ;
- réimportation ;
- hold malveillant ;
- purge non auditée ;
- provider externe non traité ;
- data leak dans logs ;
- suppression partielle présentée comme complète.

---

## 93.3 Contrôles

- catalogue ;
- policies ;
- plan ;
- step-up ;
- raison ;
- jobs idempotents ;
- holds ;
- markers ;
- ledger ;
- checkpoints externes ;
- restore reconciliation ;
- audit ;
- tests ;
- backups bornés.

---

# 94. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Suppression du mauvais client | Client et environnement explicites |
| Suppression massive accidentelle | Plan, seuil et confirmation renforcée |
| Donnée oubliée dans un média | Descriptor et vérification multi-stockages |
| Donnée réapparue après restore | Deletion Ledger externe |
| CRM recréant un lead | Deletion Marker et propagation externe |
| Soft delete permanent | Action terminale obligatoire |
| Hold conservé sans fin | Owner, ReviewAt et alerte |
| Email dans les métriques | Interdiction de labels personnels |
| Backup conservé indéfiniment | Rotation et policy |
| Job contenant une copie | Payload minimal et cleanup |
| Rapport divulguant la donnée | Rapports fondés sur IDs et comptes |
| SQL manuel incomplet | Workflow orchestré et audit |

---

# 95. Tests unitaires

Tester notamment :

- Policy ID ;
- version ;
- anchor ;
- calcul d’éligibilité ;
- grace period ;
- holds ;
- états ;
- terminal actions ;
- markers ;
- ledger ;
- validation de catalogue ;
- ordre des handlers.

---

# 96. Tests du catalogue

Vérifier :

```text
catégorie connue

owner présent

classification présente

policy présente

deletion handler présent

export handler présent lorsque nécessaire

subject link présent

external processor déclaré

backup treatment présent
```

---

# 97. Tests de politiques

Couvrir :

- durée ;
- événement ;
- archive ;
- delete ;
- anonymize ;
- hold ;
- date inconnue ;
- policy version modifiée ;
- recalcul explicite ;
- candidate future ;
- candidate overdue.

---

# 98. Tests d’archivage

Vérifier :

- retrait des listes actives ;
- absence de notification ;
- accessibilité restreinte ;
- date d’éligibilité ;
- restauration autorisée ;
- audit.

---

# 99. Tests d’anonymisation

Utiliser des données marqueurs.

Vérifier leur absence dans :

- colonnes directes ;
- texte libre ;
- index ;
- JSON ;
- médias ;
- exports ;
- jobs ;
- caches.

---

## 99.1 Agrégats

Vérifier que les données conservées ne permettent pas une réidentification triviale dans le jeu de test.

---

# 100. Tests de suppression

Couvrir :

```text
plan valide

plan expiré

hold actif

step-up absent

raison absente

suppression réussie

suppression partielle

handler externe en échec

retry

rejeu idempotent

rapport

ledger
```

---

# 101. Tests de suppression massive

Vérifier :

- seuil ;
- confirmation ;
- mauvais environnement ;
- limite de batch ;
- pause ;
- reprise ;
- kill switch ;
- métriques ;
- rapport final.

---

# 102. Tests PostgreSQL

Vérifier :

- contraintes ;
- `ON DELETE RESTRICT` ;
- cascades approuvées ;
- transactions ;
- batches ;
- concurrence ;
- locks ;
- index des dates ;
- cohérence du ledger.

---

# 103. Tests des médias

Vérifier :

- original ;
- variantes ;
- previews ;
- quarantaine ;
- fichiers temporaires ;
- references ;
- blob missing ;
- suppression rejouée.

---

# 104. Tests des jobs

Vérifier :

- job pending ;
- job running ;
- dead-letter ;
- payload snapshot ;
- cancellation ;
- anonymisation ;
- rétention des tentatives ;
- absence de secret.

---

# 105. Tests des exports

Vérifier :

- TTL ;
- accès avant expiration ;
- refus après expiration ;
- suppression du blob ;
- suppression des metadata ;
- notification ;
- audit.

---

# 106. Tests des fournisseurs externes

Utiliser des fakes pour :

- suppression réussie ;
- timeout ;
- retry ;
- erreur permanente ;
- confirmation ambiguë ;
- réimportation ;
- idempotency key ;
- dead-letter.

---

# 107. Tests des holds

Couvrir :

- création ;
- scope ;
- expiration ;
- extension ;
- release ;
- policy candidate ;
- tentative de suppression ;
- audit ;
- alerte de revue.

---

# 108. Tests du ledger

Vérifier :

- entrée créée ;
- ordre ;
- duplicat ;
- checkpoint ;
- hash ;
- delta ;
- absence de PII ;
- restauration.

---

# 109. Tests de restauration

Scénario :

1. créer une donnée ;
2. produire un backup ;
3. supprimer la donnée ;
4. mettre à jour le ledger externe ;
5. restaurer le backup ;
6. charger le ledger ;
7. réappliquer la suppression ;
8. vérifier tous les stockages.

Résultat attendu :

```text
la donnée ne redevient pas accessible
```

---

# 110. Tests de non-divulgation

Injecter des valeurs marqueurs dans :

- lead ;
- message ;
- média ;
- job ;
- export ;
- provider fake.

Vérifier leur absence dans :

- logs ;
- traces ;
- métriques ;
- rapports ;
- ledger ;
- audit ;
- checkpoints.

---

# 111. Tests de politiques manquantes

Un module déclarant une donnée personnelle sans policy doit échouer :

- à la validation de composition ;
- à la génération ;
- ou au quality gate avant production.

---

# 112. Tests de customizations

Une customization ajoutant une nouvelle table personnelle doit fournir :

- descriptor ;
- policy ;
- export handler ;
- deletion handler ;
- tests ;
- documentation.

---

# 113. Tests multiplateformes

Les règles fonctionnelles sont testées sur :

```text
Windows x64

Linux x64
```

Les tests PostgreSQL, Docker, filesystem, backups et restore drills sont validés sur Linux.

---

# 114. Quality gates

Une contribution est bloquée lorsque :

```text
Une donnée personnelle ne possède pas de catégorie.

Une catégorie ne possède pas de policy.

Une durée est indéfinie sans justification.

Un soft delete est considéré comme terminal.

Une anonymisation conserve les identifiants évidents.

Une suppression n’a pas de plan.

Une action destructive ne possède pas de step-up.

Un module ne possède pas de deletion handler.

Un fournisseur externe n’est pas déclaré.

Un export ne possède pas de TTL.

Un job conserve une copie indéfinie.

Une suppression ne met pas à jour le ledger.

Un rapport contient la donnée supprimée.

Une restauration ne réapplique pas le ledger.

Une métrique utilise un identifiant personnel.
```

---

# 115. Quality gates de production

La production est bloquée lorsque :

```text
Le Privacy Data Catalog résolu est absent.

Une catégorie personnelle n’a pas de policy validée.

Le Retention Worker n’est pas configuré.

Le kill switch de purge n’est pas disponible.

Le ledger n’est pas initialisé.

Le checkpoint externe du ledger est absent.

La rotation des backups n’est pas définie.

Les exports n’ont pas de stockage temporaire privé.

Les providers externes n’ont pas de procédure de suppression.

La procédure de restauration ne contient pas la réconciliation.

Les permissions Privacy sont absentes.

Une purge automatique utilise un profil de développement.
```

---

# 116. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-PRIVACY-001` | Catégorie de données inconnue |
| `SF-PRIVACY-002` | Classification absente |
| `SF-PRIVACY-003` | Finalité absente |
| `SF-PRIVACY-004` | Owner de données absent |
| `SF-PRIVACY-005` | Subject Link Strategy absente |
| `SF-PRIVACY-006` | Module sans Privacy Descriptor |
| `SF-PRIVACY-007` | Donnée personnelle non déclarée |
| `SF-RETENTION-001` | Politique inconnue |
| `SF-RETENTION-002` | Version de politique incompatible |
| `SF-RETENTION-003` | Retention Anchor absent |
| `SF-RETENTION-004` | Date d’éligibilité impossible à calculer |
| `SF-RETENTION-005` | Durée invalide |
| `SF-RETENTION-006` | Action terminale absente |
| `SF-RETENTION-007` | Conservation indéfinie non justifiée |
| `SF-RETENTION-008` | Politique de backup absente |
| `SF-RETENTION-009` | Policy owner absent |
| `SF-RETENTION-010` | Migration de policy requise |
| `SF-HOLD-001` | Hold inconnu |
| `SF-HOLD-002` | Scope de hold invalide |
| `SF-HOLD-003` | Raison de hold absente |
| `SF-HOLD-004` | Hold expiré |
| `SF-HOLD-005` | Hold empêchant la suppression |
| `SF-HOLD-006` | Revue de hold en retard |
| `SF-DELETE-001` | Demande inconnue |
| `SF-DELETE-002` | Privacy Subject inconnu |
| `SF-DELETE-003` | Vérification d’identité requise |
| `SF-DELETE-004` | Plan absent |
| `SF-DELETE-005` | Plan expiré |
| `SF-DELETE-006` | Plan ne correspondant plus aux données |
| `SF-DELETE-007` | Approbation absente |
| `SF-DELETE-008` | Step-up requis |
| `SF-DELETE-009` | Raison absente |
| `SF-DELETE-010` | Suppression bloquée |
| `SF-DELETE-011` | Suppression partiellement terminée |
| `SF-DELETE-012` | Handler de suppression absent |
| `SF-DELETE-013` | Dépendance non traitée |
| `SF-DELETE-014` | Suppression PostgreSQL échouée |
| `SF-DELETE-015` | Suppression média échouée |
| `SF-DELETE-016` | Export encore présent |
| `SF-DELETE-017` | Job contenant encore les données |
| `SF-DELETE-018` | Cache non invalidé |
| `SF-DELETE-019` | Suppression externe échouée |
| `SF-DELETE-020` | Rapport absent |
| `SF-ANON-001` | Stratégie d’anonymisation absente |
| `SF-ANON-002` | Champ personnel conservé |
| `SF-ANON-003` | Texte libre non traité |
| `SF-ANON-004` | Réidentification triviale détectée |
| `SF-MARKER-001` | Deletion Marker absent |
| `SF-MARKER-002` | Marker contenant une donnée interdite |
| `SF-MARKER-003` | Marker expiré |
| `SF-LEDGER-001` | Ledger non initialisé |
| `SF-LEDGER-002` | Entrée manquante |
| `SF-LEDGER-003` | Séquence invalide |
| `SF-LEDGER-004` | Checkpoint externe absent |
| `SF-LEDGER-005` | Checkpoint incohérent |
| `SF-LEDGER-006` | Ledger contenant une PII |
| `SF-RESTORE-PRIVACY-001` | Réconciliation de suppression requise |
| `SF-RESTORE-PRIVACY-002` | Ledger plus ancien que le backup cible |
| `SF-RESTORE-PRIVACY-003` | Suppression restaurée non réappliquée |
| `SF-RESTORE-PRIVACY-004` | Service ouvert avant réconciliation |
| `SF-PROCESSOR-001` | Fournisseur externe inconnu |
| `SF-PROCESSOR-002` | Procédure de suppression absente |
| `SF-PROCESSOR-003` | Suppression externe non confirmée |
| `SF-PROCESSOR-004` | Réimportation détectée |
| `SF-EXPORT-RETENTION-001` | Export sans TTL |
| `SF-EXPORT-RETENTION-002` | Export expiré encore accessible |
| `SF-EXPORT-RETENTION-003` | Suppression d’export échouée |

---

# 117. CI/CD

La CI doit vérifier :

- catalogue ;
- descriptors ;
- policies ;
- migrations ;
- handlers ;
- tests d’anonymisation ;
- tests de suppression ;
- jobs ;
- exports ;
- non-divulgation ;
- absence de PII dans les métriques ;
- restore reconciliation ;
- scripts de purge.

---

## 117.1 Données réelles

La CI n’utilise pas de données client réelles.

---

## 117.2 Fixtures

Les fixtures utilisent des valeurs marqueurs faciles à rechercher dans tous les artefacts.

---

# 118. Règles pour les modules

Chaque module conservant des données doit déclarer :

```text
DataCategoryId

Classification

PurposeIds

RetentionPolicyId

SubjectLinkStrategy

TerminalAction

DeletionHandler

AnonymizationHandler éventuel

ExportHandler éventuel

ExternalProcessors

BackupTreatment
```

---

## 118.1 Interdictions

Un module ne doit pas :

- conserver une donnée sans policy ;
- utiliser ses logs comme historique métier ;
- ignorer les jobs ou médias ;
- ajouter une cascade destructive non testée ;
- conserver un export permanent ;
- utiliser un email comme identifiant technique global.

---

# 119. Règles pour les customizations

Une customization client suit les mêmes contrats.

---

## 119.1 Nouvelle table

Toute nouvelle table personnelle exige :

- descriptor ;
- policy ;
- migration ;
- handler ;
- tests ;
- documentation de handover.

---

## 119.2 Code direct

La customization ne doit pas supprimer directement les données d’autres modules.

---

# 120. Règles pour LogicGate

LogicGate doit :

- maintenir le catalogue ;
- obtenir la validation des politiques ;
- surveiller les jobs ;
- traiter les blocages ;
- vérifier les backups ;
- effectuer les restore drills ;
- maintenir le ledger ;
- revoir les holds ;
- préparer le transfert ;
- documenter les limites externes.

---

## 120.1 LogicGate ne doit pas

- inventer seul une durée juridique ;
- utiliser « conservation indéfinie » par commodité ;
- supprimer directement par SQL ;
- marquer une demande complète si un fournisseur reste en échec ;
- restaurer sans réconciliation ;
- conserver des exports de production localement sans TTL ;
- copier une donnée supprimée dans un rapport.

---

# 121. Règles pour Codex

Codex peut :

- définir les contrats ;
- créer le catalogue ;
- implémenter les policies ;
- ajouter les jobs ;
- créer les handlers ;
- ajouter les tests ;
- créer les rapports ;
- documenter les runbooks.

---

## 121.1 Codex ne doit pas

```text
Inventer une durée légale

Supprimer des données de production

Créer un hold de production

Lever un hold réel

Modifier un ledger réel

Purger un backup réel

Lancer une suppression massive

Marquer une suppression comme complète sans preuve

Ajouter des PII dans les logs ou métriques

Appliquer une migration de production
```

---

## 121.2 Décision humaine

Une décision humaine est requise pour :

- nouvelle durée de production ;
- nouvelle catégorie personnelle ;
- nouvelle finalité ;
- nouveau fournisseur ;
- création ou retrait de hold ;
- suppression massive ;
- traitement d’une demande réelle ;
- anonymisation d’un nouvel agrégat ;
- modification du ledger ;
- changement de politique de backup.

---

# 122. Anti-patterns interdits

## 122.1 Conservation « au cas où »

Aucune date ou finalité n’est définie.

---

## 122.2 Soft delete permanent

Les lignes restent indéfiniment avec `IsDeleted = true`.

---

## 122.3 Email remplacé uniquement

Le système affirme que le lead est anonyme alors que le nom, le message et le téléphone restent présents.

---

## 122.4 Suppression SQL directe

Un script retire une ligne sans traiter médias, jobs, exports ou providers.

---

## 122.5 Backup oublié

La politique annonce une suppression immédiate sans expliquer la rotation des backups.

---

## 122.6 Restore direct en production

Un backup ancien est ouvert publiquement sans ledger.

---

## 122.7 Export permanent

Un fichier de leads reste dans `App_Data/exports` sans TTL.

---

## 122.8 Hold sans owner

La purge est suspendue indéfiniment sans revue.

---

## 122.9 PII dans les métriques

Un label contient l’email ou l’identifiant du sujet.

---

## 122.10 Ledger contenant l’email

Le mécanisme de prévention recrée une base personnelle durable.

---

## 122.11 Suppression déclarée complète

Un fournisseur externe reste en dead-letter, mais le statut est `completed`.

---

## 122.12 Politique rétroactive silencieuse

Une nouvelle release rend immédiatement des milliers de lignes éligibles sans plan.

---

# 123. Conséquences positives

La décision apporte :

- minimisation ;
- politiques explicables ;
- suppression réellement orchestrée ;
- prévention des restaurations dangereuses ;
- meilleure maîtrise des backups ;
- prise en compte des fournisseurs ;
- réduction des données anciennes ;
- transfert facilité ;
- meilleure testabilité ;
- meilleur audit ;
- réduction de la surface d’exposition ;
- contrôle des exports et données temporaires.

---

# 124. Conséquences négatives

La décision entraîne :

- catalogue ;
- policies ;
- descriptors ;
- tables supplémentaires ;
- jobs ;
- ledger ;
- checkpoints ;
- restore reconciliation ;
- holds ;
- tests ;
- procédures ;
- besoin de validation humaine ;
- complexité pour les intégrations externes.

---

# 125. Risques acceptés

## 125.1 Données dans les backups

Une donnée peut rester dans un backup jusqu’à l’expiration de sa rotation.

Réduction :

- chiffrement ;
- accès limité ;
- rotation bornée ;
- restore reconciliation ;
- aucune utilisation fonctionnelle du backup.

---

## 125.2 Suppression externe impossible

Un fournisseur peut ne pas fournir de suppression immédiate ou vérifiable.

Réduction :

- documentation ;
- choix du fournisseur ;
- statut partiel ;
- escalade ;
- changement futur de fournisseur.

---

## 125.3 Faux rapprochement de sujets

Une stratégie de déduplication peut associer ou séparer incorrectement deux personnes.

Réduction :

- Privacy Subject ID ;
- règles prudentes ;
- revue ;
- absence de déduction excessive.

---

## 125.4 Suppression irréversible

Une suppression physique ou anonymisation forte peut empêcher une restauration métier.

Ce risque est accepté uniquement après plan et confirmation.

---

# 126. Mesures compensatoires

- catalogue ;
- policies versionnées ;
- plans ;
- dry-run ;
- step-up ;
- holds ;
- grace periods ;
- jobs idempotents ;
- markers ;
- ledger ;
- checkpoints externes ;
- backups chiffrés ;
- restore drills ;
- audit ;
- rapports ;
- tests de non-divulgation ;
- kill switch de purge.

---

# 127. Performance

## 127.1 Index

Les ressources éligibles utilisent des index sur :

```text
PurgeEligibleAtUtc

LifecycleStatus

RetentionPolicyId

PrivacySubjectId
```

---

## 127.2 Batches

Les suppressions utilisent des batches afin de limiter :

- locks ;
- WAL ;
- mémoire ;
- durée de transaction ;
- charge des providers.

---

## 127.3 Scans

Le worker ne doit pas rescanner intégralement toutes les tables à chaque occurrence.

---

## 127.4 Ledger

Le ledger et ses checkpoints restent compacts.

---

# 128. Coûts

## 128.1 Coûts supplémentaires

- développement ;
- stockage du ledger ;
- jobs ;
- backups ;
- tests ;
- exploitation ;
- revue des policies ;
- fournisseurs.

---

## 128.2 Coûts évités

- conservation excessive ;
- incident plus grave ;
- backups incontrôlés ;
- transferts volumineux ;
- suppressions manuelles ;
- réapparition de données ;
- exports oubliés ;
- litiges techniques difficiles à analyser.

---

# 129. Maintenabilité

Pour chaque catégorie, il doit être possible de répondre :

```text
Pourquoi est-elle conservée ?

Qui en est propriétaire ?

Quel sujet concerne-t-elle ?

Quel est son anchor ?

Quand devient-elle éligible ?

Est-elle archivée, supprimée ou anonymisée ?

Quels stockages la contiennent ?

Quels providers la reçoivent ?

Comment la vérifier ?

Que se passe-t-il après une restauration ?
```

---

# 130. Compatibilité

## 130.1 Policy Version

Chaque policy possède une version.

---

## 130.2 Catalogue

Le catalogue possède une `schemaVersion`.

---

## 130.3 Anciennes données

Une nouvelle release doit continuer à interpréter les anciennes versions ou fournir une migration.

---

## 130.4 Nouveaux modules

Un module non compatible avec le framework de privacy est refusé.

---

## 130.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

L’adoption par un projet existant nécessite un inventaire et un plan initial.

---

# 131. Migration d’un projet existant

Le processus est :

```text
inventorier les tables et fichiers

identifier les catégories

identifier les données personnelles

identifier les providers

identifier les backups

attribuer les Privacy Subject IDs

définir les policies

calculer les dates d’éligibilité

créer les handlers

produire un plan

tester sur une copie

activer le worker
```

---

## 131.1 Purge initiale

La première purge ne doit pas être exécutée immédiatement après l’inventaire sans :

- rapport ;
- revue ;
- seuil ;
- backup ;
- validation.

---

# 132. Migration depuis soft delete

Le processus est :

```text
inventorier les lignes soft-deleted
→ identifier les dates
→ déterminer la policy
→ traiter les dépendances
→ produire un plan
→ supprimer ou anonymiser
→ ajouter le worker
```

---

# 133. Migration depuis des backups non bornés

Le processus est :

```text
inventorier les backups
→ identifier leur propriétaire
→ vérifier le chiffrement
→ définir une rotation
→ protéger les archives nécessaires
→ supprimer selon un plan
→ documenter
```

---

# 134. Transfert client

Le handover doit inclure :

- Privacy Data Catalog ;
- policies ;
- versions ;
- demandes ouvertes ;
- holds ;
- ledger ;
- dernier checkpoint ;
- backups ;
- providers ;
- runbooks ;
- jobs ;
- rapports ;
- responsabilités futures.

---

## 134.1 Données LogicGate

Les données et identifiants propres à LogicGate sont retirés ou transférés selon le contrat.

---

## 134.2 Fournisseurs

Les comptes fournisseurs sont recréés ou transférés au client.

---

# 135. Clôture de service

La clôture suit :

```text
geler les nouvelles collectes non nécessaires

inventorier les données

produire les exports contractuels

traiter les demandes ouvertes

transférer les données

définir le délai de conservation résiduel

révoquer les accès

arrêter les jobs

produire les backups finaux

supprimer après validation

produire un rapport de clôture

auditer
```

---

## 135.1 Suppression LogicGate

La suppression des copies détenues par LogicGate suit un plan distinct de la suppression sur l’infrastructure transférée au client.

---

# 136. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de supprimer une donnée réelle ;
- d’anonymiser un lead réel ;
- de modifier une durée ;
- de créer ou retirer un hold ;
- de purger un backup ;
- de lancer une suppression massive ;
- de notifier un fournisseur ;
- de modifier le ledger ;
- d’appliquer une migration.

---

# 137. Réversibilité

## 137.1 Classification

```text
élevée avant suppression physique

moyenne pendant le délai de grâce

faible après anonymisation forte

très faible après suppression et expiration des backups
```

---

## 137.2 Plan

Le plan doit indiquer le niveau de réversibilité de chaque action.

---

## 137.3 Rollback

Le rollback ne doit pas être promis lorsqu’il dépend d’un backup qui sera lui-même purgé.

---

# 138. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `PrivacySubjectId` ;
- `DataCategoryId` ;
- `RetentionPolicyId` ;
- statuts ;
- actions ;
- erreurs.

---

## Étape 2 — Catalogue

Créer le Privacy Data Catalog initial.

---

## Étape 3 — Policies

Définir les schémas de policies versionnées.

---

## Étape 4 — PostgreSQL

Créer :

- Privacy Subjects ;
- demandes ;
- plans ;
- holds ;
- markers ;
- ledger ;
- index ;
- migrations.

---

## Étape 5 — Descriptors modules

Ajouter les déclarations à :

- Leads ;
- Identity ;
- Media ;
- Forms ;
- Jobs ;
- Audit.

---

## Étape 6 — Handlers

Implémenter :

- export ;
- delete ;
- anonymize ;
- verify.

---

## Étape 7 — Worker

Ajouter :

- évaluation ;
- purge ;
- cleanup ;
- propagation externe ;
- checkpoint du ledger.

---

## Étape 8 — Restore

Intégrer le ledger au runbook et aux tests de restauration.

---

## Étape 9 — Interface et CLI

Ajouter :

- plan ;
- statut ;
- holds ;
- rapports ;
- permissions ;
- step-up.

---

## Étape 10 — Quality gates

Ajouter :

- validation catalogue ;
- non-divulgation ;
- restore tests ;
- backup rotation ;
- customizations.

---

# 139. Tâches liées proposées

```text
SF-TASK-290 — Define privacy subjects and data-category contracts

SF-TASK-291 — Create the versioned privacy data catalog

SF-TASK-292 — Define versioned retention-policy schemas

SF-TASK-293 — Add privacy-subject PostgreSQL persistence

SF-TASK-294 — Add privacy-request and deletion-plan persistence

SF-TASK-295 — Add retention-hold workflows

SF-TASK-296 — Add deletion markers and anti-reimport controls

SF-TASK-297 — Add the minimal deletion ledger

SF-TASK-298 — Add encrypted external deletion-ledger checkpoints

SF-TASK-299 — Add module privacy-data descriptors

SF-TASK-300 — Add lead anonymization and deletion handlers

SF-TASK-301 — Add Identity privacy lifecycle handlers

SF-TASK-302 — Add media and export deletion handlers

SF-TASK-303 — Add job-payload privacy cleanup

SF-TASK-304 — Add external-processor deletion dispatch

SF-TASK-305 — Add automated retention evaluation

SF-TASK-306 — Add restore-time deletion reconciliation

SF-TASK-307 — Add privacy plan and apply operator commands

SF-TASK-308 — Add privacy lifecycle observability

SF-TASK-309 — Add retention, deletion and restore security tests
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 140. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Toutes les catégories persistantes sont inventoriées.

Chaque donnée personnelle possède une policy.

Les durées et actions terminales sont explicites.

Les modules possèdent leurs Privacy Descriptors.

Les données peuvent être reliées à un Privacy Subject.

Un soft delete n’est pas considéré comme terminal.

Une anonymisation traite tous les champs identifiants prévus.

Une suppression possède un plan en lecture seule.

Un hold bloque effectivement la purge.

Une suppression sensible exige un step-up.

Les suppressions sont idempotentes.

Les médias, variantes et exports sont traités.

Les jobs et dead letters sont analysés.

Les fournisseurs externes reçoivent une action durable.

Un échec externe empêche un faux statut completed.

Un Deletion Marker empêche une réimportation prévue.

Le Deletion Ledger ne contient aucune PII directe.

Un checkpoint externe du ledger est produit.

Un backup possède une rotation bornée.

Une restauration réapplique les suppressions postérieures.

Le site ne s’ouvre pas avant la réconciliation du restore.

Les logs, traces, métriques et rapports ne divulguent aucune donnée supprimée.

Les suppressions massives sont bornées et confirmées.

Les customizations doivent déclarer leur cycle de vie.

Les tests fonctionnent sur Windows et Linux.
```

---

# 141. Conditions de révision

Réexaminer cette décision si :

- une plateforme privacy centrale est introduite ;
- plusieurs services partagent les mêmes sujets ;
- une architecture multi-tenant est adoptée ;
- un archivage légal externe devient obligatoire ;
- une conservation WORM est requise ;
- les volumes rendent les suppressions PostgreSQL trop coûteuses ;
- plusieurs régions sont utilisées ;
- les providers externes deviennent nombreux ;
- une obligation sectorielle impose une politique uniforme ;
- un data warehouse est introduit ;
- une réplication analytique est ajoutée ;
- une double approbation devient obligatoire.

---

# 142. Indicateurs à suivre

- catégories inventoriées ;
- catégories sans policy ;
- données éligibles ;
- purges réussies ;
- purges échouées ;
- demandes ouvertes ;
- demandes bloquées ;
- holds actifs ;
- holds en retard ;
- suppressions externes en échec ;
- exports expirés ;
- jobs avec données anciennes ;
- ledger lag ;
- checkpoints manquants ;
- backups hors rétention ;
- restores réconciliés ;
- suppressions massives ;
- volume anonymisé.

---

# 143. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des catégories n’apparaissent pas dans le catalogue

les soft deletes s’accumulent

les holds ne sont jamais revus

les exports expirés restent accessibles

les jobs conservent des snapshots anciens

les fournisseurs ne confirment pas les suppressions

le ledger contient des emails

les checkpoints ne sont plus produits

les backups dépassent leur durée

les restores réintroduisent des données

des métriques utilisent des identifiants personnels

les suppressions massives deviennent fréquentes

les équipes utilisent SQL pour contourner le workflow
```

---

# 144. Conséquences observées

Cette section est append-only après mise en œuvre.

## 144.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
du Privacy Data Catalog,
des politiques de conservation,
du Deletion Ledger
et de la réconciliation après restauration.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- catégorie ;
- policy ;
- action ;
- volume ;
- résultat ;
- incident ou bénéfice ;
- action proposée.

---

# 145. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
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
- [0015-deterministic-generation-and-optional-ai.md](0015-deterministic-generation-and-optional-ai.md)
- [0016-content-and-media-storage-boundaries.md](0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](0017-background-jobs-and-reliable-side-effects.md)
- [0018-public-form-abuse-protection-and-rate-limiting.md](0018-public-form-abuse-protection-and-rate-limiting.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](0019-audit-trail-and-sensitive-administrative-actions.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](0021-administrator-provisioning-invitations-and-account-recovery.md)
- [0022-browser-security-headers-and-content-security-policy.md](0022-browser-security-headers-and-content-security-policy.md)

---

# 146. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 141 |

---

# 147. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 148. Synthèse de la décision

```text
Inventaire
→ toutes les données persistantes
→ catégories
→ owners
→ classifications
→ finalités

Policies
→ versionnées
→ anchor explicite
→ durée ou règle
→ archive
→ delete ou anonymize
→ traitement backup
→ traitement externe

Privacy Subject
→ identifiant opaque
→ liens explicites dans les modules
→ aucun email comme clé globale

Cycle de vie
→ active
→ archived
→ pending
→ held
→ anonymized ou deleted

Suppression
→ demande
→ vérification
→ plan
→ step-up
→ raison
→ approbation
→ job idempotent
→ vérification
→ rapport
→ audit

Stockages
→ PostgreSQL
→ médias
→ exports
→ jobs
→ caches
→ providers
→ backups

Holds
→ scope
→ owner
→ raison
→ revue
→ expiration ou justification

Prévention de réapparition
→ Deletion Marker
→ Deletion Ledger
→ checkpoint externe
→ anti-réimportation

Restore
→ environnement isolé
→ workers arrêtés
→ nouveau Session Epoch
→ nouveau Recovery Epoch
→ nouveau Audit Epoch
→ ledger chargé
→ suppressions réappliquées
→ vérification
→ ouverture progressive

Observabilité
→ aucune PII dans les métriques
→ aucun contenu dans les logs
→ rapports fondés sur IDs et comptes

Backups
→ chiffrés
→ rotation bornée
→ aucun usage fonctionnel direct
→ suppression réappliquée après restauration

Production
→ aucune durée inventée
→ policies validées
→ aucun soft delete permanent
→ aucune suppression SQL ordinaire
```

> Une donnée n’est pas correctement supprimée uniquement parce qu’une ligne a disparu de PostgreSQL. Son cycle de vie doit couvrir toutes ses copies, ses références, ses effets externes et ses backups, puis empêcher qu’une restauration ou une synchronisation ne la fasse réapparaître silencieusement.
