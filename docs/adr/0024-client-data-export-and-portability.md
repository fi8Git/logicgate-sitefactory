---
adrId: ADR-0024
title: Export client data through documented, verifiable and portable packages
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
  - ./0005-file-generation-strategies.md
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
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
targetVersion: 0.1.0
---

# ADR-0024 — Export des données client et portabilité

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

L'export décrit ici concerne les **données d'une application cliente** :
ce n'est ni la génération documentaire ni l'export assaini du code source.
Le public ne livre pas encore d'outil d'export/import des données, de backup
ou de restauration. Les commandes, manifests, schémas et formats enrichis
ci-dessous sont cibles ; `client.sample-project.*` est un exemple fictif.
Voir les [limites actuelles](../../README.md). Portabilité, autorisation,
minimisation, intégrité, protection des secrets et distinction export/backup
restent requises ; cet ADR n'autorise aucune extraction de données réelle.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory fournit un mécanisme d’export documenté, structuré et vérifiable permettant de remettre au client ses données sans dépendre du fonctionnement interne de LogicGate.

L’export portable doit pouvoir inclure, selon le profil choisi :

```text
contenus éditables

leads et données métier

métadonnées SEO

médias et documents

relations entre entités

configuration structurelle non sensible

versions de schémas

métadonnées de déploiement non sensibles

piste d’audit lorsque autorisée

Privacy Data Catalog

politiques de rétention

Deletion Ledger

Retention Holds

rapports nécessaires au handover
```

L’export portable exclut toujours :

- mots de passe ;
- password hashes ;
- secrets TOTP ;
- recovery codes ;
- cookies ;
- sessions ;
- tokens d’invitation ;
- tokens de récupération ;
- clés Data Protection ;
- credentials PostgreSQL ;
- credentials SMTP ;
- tokens de registry ;
- credentials de backup ;
- secrets de fournisseurs ;
- URLs signées encore actives ;
- données temporaires non nécessaires ;
- fichiers de cache ;
- artefacts `bin/` et `obj/`.

Un export portable n’est pas un backup.

Un backup n’est pas un export portable.

---

## 2. Résumé

L’architecture de référence est :

```text
Demande d’export
→ authentification
→ autorisation
→ step-up
→ raison
→ sélection d’un profil
→ export plan
→ validation humaine
→ job durable
→ snapshot logique cohérent
→ sérialisation portable
→ copie des médias
→ vérification des checksums
→ création du manifest
→ création de l’archive
→ calcul du hash global
→ protection du paquet
→ stockage privé temporaire
→ audit
→ téléchargement ou remise contrôlée
→ expiration
→ suppression
```

Le format canonique comprend :

```text
export-manifest.json

README.md

schemas/

data/

media/

audit/

privacy/

checksums.sha256
```

Les données structurées utilisent principalement :

```text
JSON Lines
UTF-8
JSON Schema
identifiants opaques
dates UTC ISO 8601
ordre stable
```

Les fichiers CSV peuvent être ajoutés comme format de consultation.

Ils ne constituent pas le format canonique d’import.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Un export portable ne contient aucun secret.

Un export portable ne contient aucun credential réutilisable.

Un export portable ne contient aucun mot de passe ou password hash.

Un export portable ne contient aucune session active.

Un export n’est pas envoyé comme pièce jointe d’un email ordinaire.

Un export sensible n’est jamais placé dans wwwroot.

Un export incomplet n’est jamais proposé au téléchargement.

Un fichier sans checksum valide ne fait pas partie
d’un export considéré comme réussi.

Une archive ne contient aucun chemin absolu.

Une archive ne contient aucun chemin avec `..`.

Une archive portable ne contient aucun lien symbolique.

Les identifiants exportés ne dépendent pas
des clés primaires physiques PostgreSQL.

Les dates sont exportées en UTC.

Les nombres utilisent une représentation indépendante
de la locale de la machine.

Une donnée supprimée ne doit pas réapparaître
après import d’un export antérieur.

Une identité administrateur n’est jamais transférée
avec ses facteurs d’authentification.

Un export complet est audité lors de sa demande,
de sa génération et de son téléchargement.

Un export temporaire possède une expiration.

Un export expiré devient immédiatement inaccessible.

Une suppression physique de l’export est ensuite exécutée
par un job idempotent.

Un import ne modifie jamais directement la production
sans plan, tests, confirmation et procédure dédiée.
```

---

## 4. Contexte

Un projet SiteFactory peut être transféré :

- de LogicGate vers le client ;
- d’un VPS vers un autre ;
- d’un prestataire vers un autre ;
- d’un stockage filesystem vers un object storage ;
- d’une version SiteFactory vers une plateforme compatible ;
- d’un environnement de production vers un environnement de reprise ;
- vers un système tiers pour traitement ou archivage.

Le client peut également demander :

- un export de ses leads ;
- un export de ses contenus ;
- un export de ses médias ;
- un export de sa piste d’audit ;
- un export complet lors d’un handover ;
- un export lié à une demande de confidentialité.

Une simple copie de base PostgreSQL ne fournit pas toujours une portabilité suffisante.

Elle dépend notamment :

- de PostgreSQL ;
- du schéma exact ;
- des extensions ;
- des migrations ;
- de l’infrastructure ;
- des volumes ;
- des credentials ;
- de la version applicative.

Inversement, un simple CSV de leads ne permet pas de reconstruire :

- les relations ;
- les contenus ;
- les médias ;
- les métadonnées ;
- les règles de rétention ;
- les suppressions déjà effectuées ;
- les informations nécessaires à un handover complet.

SiteFactory doit donc distinguer plusieurs niveaux d’export.

---

## 5. Problème à résoudre

Comment fournir des données client :

- exploitables ;
- documentées ;
- vérifiables ;
- sécurisées ;
- indépendantes du stockage initial ;
- compatibles avec un transfert ;
- importables de manière contrôlée ;

sans exposer :

- des secrets ;
- des credentials ;
- des sessions ;
- des données internes inutiles ;
- des payloads techniques dangereux ;
- des structures propriétaires non documentées ?

La solution doit également éviter :

- les exports manuels non reproductibles ;
- les archives incomplètes ;
- les incohérences entre PostgreSQL et les médias ;
- les différences de format liées à la locale ;
- les suppressions réapparues après import ;
- les mots de passe transférés ;
- les fichiers sans provenance ;
- les archives contenant des chemins dangereux ;
- les exports conservés indéfiniment ;
- les imports destructifs silencieux.

---

## 6. Portée

Cette décision couvre :

- exports métier ;
- exports complets ;
- exports de handover ;
- formats portables ;
- manifests ;
- checksums ;
- médias ;
- contenus ;
- leads ;
- comptes administrateur ;
- audit ;
- Privacy Data Catalog ;
- Deletion Ledger ;
- Retention Holds ;
- cohérence des snapshots ;
- protection des archives ;
- téléchargement ;
- expiration ;
- import ;
- validation ;
- conflits ;
- transfert client ;
- clôture de service ;
- observabilité ;
- tests ;
- runbooks.

---

## 7. Hors portée

Cette décision ne définit pas :

- une obligation juridique universelle d’export ;
- les délais juridiques de réponse ;
- un format sectoriel réglementaire universel ;
- un outil ETL complet ;
- une synchronisation temps réel ;
- un data warehouse ;
- une réplication continue ;
- une migration automatique vers un autre CMS ;
- une compatibilité avec tous les systèmes du marché ;
- une restauration directe de production depuis un export portable ;
- la remise de credentials ;
- la remise de clés privées ;
- une garantie que tous les fournisseurs tiers acceptent un import identique.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Portabilité réelle des données**
2. **Absence de secrets et credentials**
3. **Intégrité vérifiable**
4. **Cohérence entre données et médias**
5. **Prévention de la réapparition des données supprimées**
6. **Format documenté et versionné**
7. **Accès strictement contrôlé**
8. **Protection pendant le transfert**
9. **Import planifié et non destructif**
10. **Indépendance du fournisseur de stockage**
11. **Compatibilité avec le handover**
12. **Streaming et gestion des gros volumes**
13. **Audit**
14. **Rétention limitée des archives**
15. **Testabilité**
16. **Faible dépendance fournisseur**

---

## 9. Contraintes

La décision doit respecter :

```text
PostgreSQL

App_Data ou blob storage

Jobs durables

Audit

Step-up authentication

UTC

UTF-8

JSON Schema

Identifiants stables

Checksums SHA-256

Archives sans chemins dangereux

Stockage privé

TTL

Aucun secret

Aucun password hash

Import en environnement isolé

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les modules peuvent déclarer leurs contrats d’export ;
- PostgreSQL peut fournir un snapshot cohérent ;
- les médias peuvent être lus par streaming ;
- les exports complets sont relativement rares ;
- un temps de maintenance court est acceptable lors d’un handover complet ;
- les gros exports peuvent être générés en arrière-plan ;
- les clients disposent d’un canal sécurisé pour récupérer les archives ;
- l’import cible peut exécuter des validateurs SiteFactory ;
- les credentials peuvent être reprovisionnés séparément.

---

# 11. Définitions

## 11.1 Export métier

Export ciblé d’une catégorie fonctionnelle.

Exemples :

- leads ;
- contenus ;
- médias ;
- audit.

---

## 11.2 Export portable

Paquet logique indépendant de la structure physique PostgreSQL.

---

## 11.3 Export de handover

Export complet destiné au transfert d’exploitation ou de propriété.

---

## 11.4 Snapshot natif

Dump PostgreSQL ou copie de stockage dépendant d’une technologie précise.

---

## 11.5 Export Profile

Contrat définissant :

- catégories incluses ;
- formats ;
- cohérence ;
- permissions ;
- TTL ;
- protection ;
- règles d’import.

---

## 11.6 Export Manifest

Document décrivant le contenu exact du paquet.

---

## 11.7 Export ID

Identifiant opaque unique d’un export.

---

## 11.8 Export Cutoff

Instant ou watermark définissant la limite temporelle des données incluses.

---

## 11.9 Portable Record ID

Identifiant logique stable d’un enregistrement.

---

## 11.10 Export Attestation

Métadonnée reliant :

- Export ID ;
- hash global ;
- auteur ;
- résultat ;
- environnement ;
- événement d’audit.

---

## 11.11 Import Plan

Artefact en lecture seule décrivant les actions proposées sur une cible.

---

# 12. Option A — Dump PostgreSQL uniquement

## 12.1 Description

La portabilité repose sur :

```text
pg_dump
```

et une copie du volume média.

---

## 12.2 Avantages

- fidélité élevée ;
- restauration rapide dans PostgreSQL ;
- relations préservées ;
- peu de code d’export.

---

## 12.3 Inconvénients

- dépendance PostgreSQL ;
- dépendance au schéma ;
- credentials et données techniques potentiellement inclus ;
- comptes et password hashes présents ;
- difficulté d’extraire une catégorie ;
- import difficile dans un autre système ;
- deletion ledger peu explicite.

---

## 12.4 Risques

- restauration de sessions ;
- réactivation de tokens ;
- incompatibilité de version ;
- export comprenant des données inutiles ;
- lecture difficile par le client.

---

## 12.5 Réversibilité

```text
moyenne
```

---

# 13. Option B — CSV uniquement

## 13.1 Description

Chaque table principale est exportée en CSV.

---

## 13.2 Avantages

- lisible ;
- simple ;
- compatible tableur ;
- facile à transmettre ;
- peu de tooling.

---

## 13.3 Inconvénients

- types faibles ;
- relations complexes ;
- nested structures ;
- médias ;
- encodage ;
- dates ;
- enum ;
- injection CSV ;
- schémas peu explicites ;
- import ambigu.

---

## 13.4 Risques

- perte de fidélité ;
- données tronquées ;
- identifiants incohérents ;
- formules exécutées dans un tableur ;
- absence de règles de suppression.

---

## 13.5 Réversibilité

```text
élevée
```

---

# 14. Option C — Export propriétaire opaque

## 14.1 Description

SiteFactory produit une archive binaire interprétable uniquement par son propre importer.

---

## 14.2 Avantages

- format compact ;
- contrôle complet ;
- implémentation interne libre ;
- import optimisé.

---

## 14.3 Inconvénients

- verrouillage fournisseur ;
- difficulté d’inspection ;
- transfert client faible ;
- absence d’outils standards ;
- pérennité incertaine ;
- documentation limitée.

---

## 14.4 Risques

- export inutilisable sans SiteFactory ;
- impossibilité de vérifier les données ;
- format abandonné ;
- perte d’autonomie client.

---

## 14.5 Réversibilité

```text
faible
```

---

# 15. Option D — Paquet logique documenté et versionné

## 15.1 Description

SiteFactory produit un paquet comprenant :

- manifest ;
- schémas ;
- JSON Lines ;
- médias ;
- checksums ;
- documentation.

---

## 15.2 Avantages

- lisible ;
- portable ;
- vérifiable ;
- importable ;
- indépendant du schéma physique ;
- compatible avec plusieurs stockages ;
- extensible ;
- transférable ;
- adapté aux données et médias.

---

## 15.3 Inconvénients

- contrats d’export ;
- serializers ;
- importers ;
- maintenance de schémas ;
- gestion des versions ;
- taille des archives ;
- tests supplémentaires.

---

## 15.4 Risques

- module non exporté ;
- relation oubliée ;
- schéma incompatible ;
- bundle incomplet ;
- archive mal protégée.

---

## 15.5 Réversibilité

```text
élevée
```

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Dump natif | CSV | Format opaque | Paquet portable |
|---|---:|---:|---:|---:|---:|
| Portabilité | 5 | 2 | 4 | 1 | 5 |
| Fidélité | 5 | 5 | 2 | 5 | 5 |
| Lisibilité | 4 | 2 | 5 | 1 | 5 |
| Médias | 5 | 3 | 1 | 4 | 5 |
| Import contrôlé | 5 | 3 | 2 | 5 | 5 |
| Indépendance fournisseur | 5 | 2 | 5 | 1 | 5 |
| Sécurité | 5 | 2 | 3 | 3 | 5 |
| Transfert client | 5 | 3 | 4 | 1 | 5 |
| Maintenabilité | 4 | 3 | 3 | 2 | 5 |
| Réversibilité | 4 | 3 | 5 | 2 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’utiliser des paquets d’export logiques :

```text
documentés
versionnés
portables
vérifiables
protégés
```

Un snapshot natif peut être ajouté à un handover comme artefact complémentaire.

Il ne constitue pas le contrat de portabilité principal.

---

## 17.1 Format canonique

Le format canonique est :

```text
JSON Lines
+ JSON Schema
+ fichiers binaires séparés
+ manifest
+ checksums
```

---

## 17.2 CSV

CSV reste un format de consultation facultatif.

---

## 17.3 Import

L’importer utilise les fichiers canoniques, pas les fichiers CSV de consultation.

---

# 18. Types d’exports

Types initiaux :

```text
selective-business-export

portable-client-data-export

complete-client-handover-export

privacy-subject-export

audit-export
```

---

## 18.1 Selective business export

Cible une catégorie métier.

Exemple :

```text
leads
```

---

## 18.2 Portable client data export

Couvre les données fonctionnelles et médias du projet.

---

## 18.3 Complete client handover export

Ajoute les informations nécessaires au transfert :

- audit autorisé ;
- privacy metadata ;
- ledger ;
- versions ;
- inventaire ;
- rapports ;
- configuration non sensible.

---

## 18.4 Privacy subject export

Suit principalement l’ADR-0023.

Il peut réutiliser les formats techniques de cet ADR.

---

## 18.5 Audit export

Suit principalement l’ADR-0019.

---

# 19. Profils d’export

Profils conceptuels initiaux :

```text
export.leads.v1

export.content.v1

export.media.v1

export.client-portable.v1

export.client-handover-complete.v1

export.privacy-subject.v1

export.audit.v1
```

---

## 19.1 Version

Chaque profil possède une version explicite.

---

## 19.2 Composition

Un profil déclare :

```text
DataCategories

MediaPolicy

AuditPolicy

PrivacyPolicy

ConsistencyMode

ArchiveFormat

ProtectionPolicy

TimeToLive

Permissions

ImportCompatibility
```

---

## 19.3 Override

Un client ne peut pas ajouter arbitrairement une catégorie sensible à un profil sans décision et autorisation.

---

# 20. Distinction avec les backups

Un backup vise principalement :

```text
reprise technique fidèle
```

Un export portable vise :

```text
lecture, transfert et réimport contrôlé
```

---

## 20.1 Backup

Peut inclure :

- structures internes ;
- sessions historiques ;
- tokens chiffrés ;
- détails PostgreSQL ;
- informations non destinées au client final.

---

## 20.2 Export

Exclut les éléments inutiles ou dangereux.

---

## 20.3 Complémentarité

Un handover peut remettre :

```text
export portable
+ backup natif chiffré
```

avec des finalités et procédures distinctes.

---

# 21. Format logique

Les enregistrements utilisent JSON Lines :

```text
un objet JSON par ligne
```

---

## 21.1 Avantages

- streaming ;
- reprise ;
- outils standards ;
- fichiers volumineux ;
- validation par ligne ;
- diff et inspection.

---

## 21.2 Encodage

```text
UTF-8 sans encodage dépendant de la machine
```

---

## 21.3 Fin de ligne

La convention canonique est :

```text
LF
```

---

# 22. Format d’archive

Le format logique est indépendant du conteneur d’archive.

La baseline portable utilise :

```text
ZIP64
```

avec :

- noms UTF-8 ;
- paths relatifs ;
- aucune entrée chiffrée par un mécanisme ZIP historique ;
- aucun lien symbolique ;
- aucune entrée de type device ;
- aucune entrée en dehors de la racine.

---

## 22.1 Compression alternative

Un profil opérationnel pourra utiliser :

```text
tar.zst
```

lorsqu’il est explicitement supporté.

---

## 22.2 Import

L’importer ne déduit pas la confiance du format de l’archive.

Toutes les entrées sont validées.

---

# 23. Structure du paquet

Exemple conceptuel :

```text
sitefactory-export/
├── export-manifest.json
├── README.md
├── checksums.sha256
├── schemas/
│   ├── manifest.schema.json
│   ├── content-page.schema.json
│   ├── lead.schema.json
│   └── media.schema.json
├── data/
│   ├── content-pages.jsonl
│   ├── content-sections.jsonl
│   ├── leads.jsonl
│   ├── lead-history.jsonl
│   └── media-metadata.jsonl
├── media/
│   ├── originals/
│   └── variants/
├── audit/
│   ├── audit-events.jsonl
│   └── audit-epochs.jsonl
├── privacy/
│   ├── privacy-data-catalog.json
│   ├── retention-policies.json
│   ├── deletion-ledger.jsonl
│   └── retention-holds.jsonl
└── project/
    ├── sitefactory.lock.json
    ├── application-version.json
    └── deployment-profile.json
```

---

## 23.1 Présence conditionnelle

Les dossiers sont présents uniquement lorsque le profil les autorise.

---

## 23.2 Absence explicite

Le manifest liste également les catégories omises et leur raison.

---

# 24. Export Manifest

Le manifest doit pouvoir contenir :

```text
SchemaVersion

ExportId

ExportProfileId

ExportProfileVersion

ClientSlug

Environment

ApplicationVersion

SiteFactoryGeneratorVersion

SiteFactoryPlatformVersion

CreatedAtUtc

ExportCutoffUtc

ConsistencyMode

Locale

TimeZonePolicy

DataSets

MediaSets

Schemas

RecordCounts

FileCounts

TotalBytes

ChecksumAlgorithm

ArchiveHash

PrivacyCatalogVersion

DeletionLedgerSequence

AuditEpochs

ImportCompatibility

OmittedCategories

ProtectionMetadata

CreatedByActorId

AuditEventId
```

---

## 24.1 Données interdites

Le manifest ne contient pas :

- secret ;
- token ;
- mot de passe ;
- chaîne de connexion ;
- clé privée ;
- URL signée ;
- contenu métier complet.

---

## 24.2 ClientSlug

Le Client Slug peut être masqué dans un manifest externe minimal si le canal de transfert l’exige.

---

# 25. JSON Schemas

Chaque dataset possède un schéma versionné.

---

## 25.1 Contenu

Le schéma décrit :

- propriétés ;
- types ;
- champs obligatoires ;
- enums ;
- formats ;
- relations ;
- versions.

---

## 25.2 Copie dans le paquet

Une copie des schémas applicables est incluse.

---

## 25.3 Source

La source canonique reste versionnée dans les contrats SiteFactory.

---

# 26. Identifiants stables

Les enregistrements utilisent des identifiants logiques.

---

## 26.1 Interdiction

Le format ne doit pas dépendre exclusivement :

- d’un numéro séquentiel PostgreSQL ;
- d’un `ctid` ;
- d’un ordre de table ;
- d’une clé physique interne non stable.

---

## 26.2 UUID et ULID

Les identifiants existants peuvent être conservés lorsqu’ils constituent déjà des contrats stables.

---

## 26.3 Identifiant d’export

Un mapping peut être créé lorsque l’identifiant physique ne doit pas être exposé.

---

# 27. Relations

Les relations utilisent des identifiants explicites.

Exemple :

```json
{
  "leadId": "01J...",
  "privacySubjectId": "01J...",
  "status": "new"
}
```

---

## 27.1 Pas de position

Une relation ne doit pas dépendre du numéro de ligne dans un autre fichier.

---

## 27.2 Ressource absente

Une référence vers une ressource omise doit être :

- supprimée ;
- neutralisée ;
- ou explicitement documentée.

---

# 28. Dates et heures

Les dates utilisent ISO 8601 en UTC.

Exemple :

```text
2026-07-29T14:30:00.000Z
```

---

## 28.1 Date sans heure

Une date civile reste représentée comme telle :

```text
2026-07-29
```

---

## 28.2 Pas de format local

Les formats suivants ne doivent pas apparaître dans le format canonique :

```text
29/07/2026

07/29/2026
```

---

# 29. Nombres et devises

Les données numériques utilisent une représentation indépendante de la locale.

---

## 29.1 Décimales

Le point est utilisé comme séparateur JSON.

---

## 29.2 Montants

Un montant utilise un objet structuré.

Exemple :

```json
{
  "amount": "6400.00",
  "currency": "EUR"
}
```

---

## 29.3 Arrondis

Le format ne doit pas introduire de nouvel arrondi.

---

# 30. Enums et valeurs métier

Les valeurs utilisent des identifiants stables.

Exemple :

```text
archived
```

et non :

```text
3
```

---

## 30.1 Libellés

Les libellés traduits peuvent être inclus comme informations de présentation.

Ils ne constituent pas la valeur canonique.

---

# 31. Fichiers de données

Les fichiers utilisent une convention stable.

Exemples :

```text
data/leads.jsonl

data/content-pages.jsonl

data/media-metadata.jsonl
```

---

## 31.1 Ordre

Les enregistrements sont triés par identifiant stable ou ordre défini.

---

## 31.2 En-tête

JSON Lines ne nécessite pas de ligne d’en-tête.

Les métadonnées appartiennent au manifest et au schéma.

---

# 32. CSV de consultation

Un profil peut produire un fichier CSV.

---

## 32.1 Canonique

Le CSV n’est jamais la source canonique d’import lorsque les données possèdent :

- relations ;
- structures imbriquées ;
- types complexes ;
- versions.

---

## 32.2 Encodage

```text
UTF-8
```

---

## 32.3 Injection CSV

Les cellules commençant par :

```text
=
+
-
@
```

sont neutralisées selon la stratégie documentée.

---

## 32.4 Locale

Le séparateur et les formats sont définis par le profil, pas par la locale de la machine.

---

# 33. Contenus

Les exports de contenu peuvent inclure :

- pages ;
- sections ;
- versions éditoriales ;
- états ;
- SEO ;
- références médias ;
- locale ;
- dates ;
- identifiants d’auteur opaques lorsque nécessaire.

---

## 33.1 HTML

Le contenu riche reste dans son format validé.

---

## 33.2 Code

Aucun script ou code exécutable n’est ajouté par l’exporter.

---

## 33.3 Brouillons

Le profil indique si les brouillons sont inclus.

---

# 34. Leads

Un export de leads peut inclure :

- Lead ID ;
- Privacy Subject ID ;
- coordonnées ;
- message ;
- statut ;
- historique ;
- source ;
- dates ;
- informations de consentement lorsqu’elles existent ;
- catégories métier.

---

## 34.1 Données sensibles

L’export est considéré comme sensible.

---

## 34.2 Autorisation

Une permission dédiée et un step-up sont obligatoires.

---

## 34.3 Filtres

Le profil ou plan précise :

- période ;
- statuts ;
- catégories ;
- champs inclus.

---

# 35. Médias

Les médias sont exportés séparément de leurs métadonnées.

---

## 35.1 Originaux

Les originaux valides sont inclus dans un export complet.

---

## 35.2 Variantes

Les variantes peuvent être :

- incluses ;
- omises comme régénérables ;
- incluses uniquement lorsqu’elles ne sont pas reproductibles.

---

## 35.3 Manifest

Chaque média possède :

- Media ID ;
- Storage Key logique exportée ;
- nom d’origine neutralisé ;
- Content-Type ;
- taille ;
- hash ;
- visibilité ;
- relations ;
- variantes.

---

# 36. Noms de fichiers média

Le nom physique de l’archive est généré depuis le Media ID.

Exemple :

```text
media/originals/01JABCDEF.bin
```

---

## 36.1 Extension

Une extension validée peut être ajoutée pour la lisibilité.

---

## 36.2 Nom utilisateur

Le nom fourni par l’utilisateur reste une métadonnée.

Il ne pilote jamais le path de l’archive.

---

# 37. Vérification des médias

Avant finalisation :

- blob présent ;
- taille attendue ;
- checksum ;
- type ;
- Media ID ;
- visibilité ;
- variantes attendues.

---

## 37.1 Blob absent

L’export complet échoue ou devient explicitement incomplet.

Il ne doit pas être présenté comme réussi sans diagnostic.

---

# 38. Identités administrateur

Un export portable peut inclure :

- User ID ;
- adresse email lorsque autorisée ;
- statut ;
- rôles ;
- dates ;
- MFA configuré ou non ;
- informations de provisioning non sensibles.

---

## 38.1 Données exclues

Sont toujours exclues :

```text
password hash

Security Stamp

TOTP secret

recovery codes

session IDs

cookies

reset tokens

invitation tokens

Delivery Secrets

step-up grants
```

---

## 38.2 Import

Les administrateurs importés restent :

```text
inactive
```

ou :

```text
re-provisioning-required
```

jusqu’à une nouvelle invitation.

---

## 38.3 Rôles

Les rôles peuvent être proposés dans l’import plan.

Ils ne deviennent pas automatiquement actifs sans validation.

---

# 39. Piste d’audit

Un export de handover peut inclure la piste d’audit.

---

## 39.1 Conditions

- permission `Audit.Export` ;
- step-up ;
- raison ;
- profil autorisé ;
- audit de l’export.

---

## 39.2 Intégrité

Les éléments suivants sont inclus :

- epochs ;
- Sequence Numbers ;
- Previous Event Hash ;
- Event Hash ;
- Schema Version.

---

## 39.3 Import

Une piste importée reste identifiée comme provenant de l’environnement source.

Elle ne doit pas être fusionnée silencieusement dans l’epoch courant.

---

# 40. Privacy Data Catalog

Un export complet inclut :

- catégories ;
- classifications ;
- finalités ;
- policies ;
- owners ;
- processors ;
- handlers attendus ;
- versions.

---

## 40.1 Valeurs

Le catalogue ne contient pas les données elles-mêmes.

---

# 41. Deletion Ledger

Un export de handover complet inclut :

- entrées nécessaires ;
- dernier Sequence Number ;
- hash ou checkpoint ;
- versions ;
- politiques.

---

## 41.1 Importance

Le ledger empêche une réapparition silencieuse des données lors d’un import ou restore ultérieur.

---

## 41.2 Import

Le ledger doit être importé ou réconcilié avant l’ouverture du service cible.

---

# 42. Retention Holds

Les holds actifs doivent être exportés lorsque le handover transfère la responsabilité de conservation.

---

## 42.1 Données

- Hold ID ;
- scope ;
- catégorie ;
- raison structurée ;
- date ;
- owner ;
- ReviewAt ;
- expiration.

---

## 42.2 Texte libre

Le texte est minimisé et protégé.

---

# 43. Jobs et outbox

Les jobs actifs ne font pas partie du format portable métier standard.

---

## 43.1 Raisons

Ils peuvent contenir :

- état technique ;
- versions de handlers ;
- dépendances externes ;
- effets potentiellement déjà réalisés ;
- payloads non portables.

---

## 43.2 Handover

Le handover inclut plutôt un inventaire :

```text
jobs pending

dead letters

outbox non dispatchée

schedules actifs

actions recommandées
```

---

## 43.3 Transfert actif

Le transfert de jobs en cours nécessite une procédure distincte :

- drainage ;
- annulation ;
- migration de payload ;
- vérification d’idempotence.

---

# 44. Configuration du projet

Un export complet peut inclure :

- `sitefactory.lock.json` ;
- versions ;
- profil de déploiement ;
- configuration structurelle ;
- catalogues résolus ;
- schémas.

---

## 44.1 Secrets

La configuration runtime sensible n’est jamais incluse.

---

## 44.2 YAML client

`client.sitefactory.yml` peut appartenir au repository remis séparément.

Une copie peut être incluse uniquement après secret scan et validation.

---

# 45. Repository

Le repository Git ne fait pas partie du paquet de données portable par défaut.

---

## 45.1 Handover

Le handover comprend séparément :

```text
repository Git
+ export portable
+ rapports opérationnels
+ procédures
```

---

## 45.2 Raisons

Le repository possède :

- son historique ;
- ses droits ;
- ses remotes ;
- ses packages ;
- son propre transfert.

---

# 46. Snapshot natif facultatif

Un handover peut inclure un dump PostgreSQL natif.

---

## 46.1 Statut

Il est identifié comme :

```text
native-operational-snapshot
```

---

## 46.2 Protection

Il est :

- chiffré ;
- transmis séparément ou dans une enveloppe protégée ;
- accompagné de sa version PostgreSQL ;
- soumis à une procédure de restauration.

---

## 46.3 Limite

Il n’est pas considéré comme le format de portabilité principal.

---

# 47. Consistency Modes

Modes initiaux :

```text
best-effort

point-in-time-logical

maintenance-window
```

---

## 47.1 `best-effort`

Adapté aux exports métier simples.

Des modifications concurrentes peuvent exister.

Le manifest le signale.

---

## 47.2 `point-in-time-logical`

Utilise un snapshot PostgreSQL cohérent pour les données relationnelles.

---

## 47.3 `maintenance-window`

Suspend les écritures concernées afin de coordonner :

- PostgreSQL ;
- médias ;
- jobs ;
- providers.

---

# 48. Export complet

Le profil complet utilise :

```text
maintenance-window
```

dans la baseline initiale.

---

## 48.1 Flux

```text
activer le mode d’export

bloquer les nouvelles écritures concernées

laisser terminer les transactions courtes

suspendre les jobs mutateurs concernés

capturer le snapshot PostgreSQL

capturer le manifest média

générer l’export

vérifier

réactiver les écritures
```

---

## 48.2 Limite temporelle

La fenêtre doit être :

- planifiée ;
- bornée ;
- observable ;
- réversible en cas d’échec.

---

# 49. Snapshot PostgreSQL

Les données relationnelles utilisent une transaction :

```text
read only
repeatable read
```

ou un mécanisme de snapshot PostgreSQL équivalent validé.

---

## 49.1 Transaction longue

La durée doit être surveillée afin de limiter :

- rétention MVCC ;
- vacuum bloqué ;
- charge ;
- stockage WAL.

---

## 49.2 Gros volume

Un export très volumineux peut nécessiter un mécanisme différent et un plan dédié.

---

# 50. Cohérence des médias

Le snapshot PostgreSQL et les blobs ne partagent pas une transaction commune.

---

## 50.1 Baseline

La maintenance window empêche :

- nouveaux uploads ;
- remplacements ;
- suppressions ;
- génération concurrente de variantes critiques.

---

## 50.2 Manifest média

Le système capture :

- Media ID ;
- Storage Key ;
- hash ;
- taille ;
- statut.

---

## 50.3 Vérification

Chaque fichier exporté doit correspondre au manifest capturé.

---

# 51. Export Cutoff

Le manifest conserve :

```text
ExportCutoffUtc
```

---

## 51.1 Signification

Les données créées après cet instant ne sont pas garanties dans l’export.

---

## 51.2 Best effort

Le profil best-effort indique également les limites de cohérence.

---

# 52. États d’un export

Valeurs recommandées :

```text
requested

planning

awaiting-approval

queued

snapshotting

generating

verifying

protecting

ready

downloaded

expired

deletion-pending

deleted

cancelled

failed
```

---

## 52.1 `ready`

L’archive :

- existe ;
- est protégée ;
- possède un hash ;
- a passé les validations ;
- possède un manifest.

---

## 52.2 `failed`

Un export échoué n’est pas téléchargeable.

---

# 53. Permissions

Permissions conceptuelles :

```text
DataExport.Request

DataExport.Download

DataExport.Cancel

DataExport.Verify

DataExport.Handover

DataImport.Plan

DataImport.Apply
```

---

## 53.1 Séparation

Un utilisateur pouvant demander un export ne possède pas nécessairement le droit de télécharger un handover complet.

---

## 53.2 Export d’audit

Nécessite également :

```text
Audit.Export
```

---

# 54. Step-up

Les exports contenant des données personnelles ou confidentielles exigent un step-up.

---

## 54.1 Export complet

Toujours :

```text
critical
```

---

## 54.2 Export limité

Peut être :

```text
sensitive
```

selon le profil.

---

## 54.3 Téléchargement

Le téléchargement peut exiger que la fraîcheur du step-up reste valide.

---

# 55. Raison

Une raison est obligatoire pour :

- export complet ;
- handover ;
- export d’audit ;
- export massif ;
- import ;
- remise externe.

---

## 55.1 Codes

Exemples :

```text
CLIENT_HANDOVER

SERVICE_CLOSURE

DATA_MIGRATION

OPERATIONAL_RECOVERY

CLIENT_REQUEST

REGULATORY_REQUEST
```

---

# 56. Export Plan

La commande ou interface produit d’abord un plan.

---

## 56.1 Contenu

```text
ExportProfile

Categories

Filters

EstimatedRecords

EstimatedMediaCount

EstimatedBytes

ConsistencyMode

MaintenanceRequired

PermissionsRequired

ProtectionPolicy

TTL

OmittedCategories

PotentialWarnings
```

---

## 56.2 Effet

Le plan ne crée aucune archive.

---

## 56.3 Expiration

Un plan est invalidé si :

- le profil change ;
- les filtres changent ;
- les permissions changent ;
- la composition change ;
- la base change de manière significative ;
- le délai est dépassé.

---

# 57. Artefact de plan

Nom conceptuel :

```text
data-export-plan.json
```

---

## 57.1 Secret

Le plan ne contient pas de données métier.

---

## 57.2 Hash

Le plan possède une empreinte utilisée par `apply`.

---

# 58. Application du plan

Commande conceptuelle :

```text
sitefactory data export apply
```

---

## 58.1 Préconditions

```text
plan valide

acteur autorisé

step-up valide

raison présente

stockage temporaire disponible

espace disque suffisant

worker disponible

profil compatible

audit disponible
```

---

## 58.2 Pas de recalcul silencieux

Un plan modifié est refusé.

---

# 59. Job durable

La génération est exécutée par un job durable.

Job Type conceptuel :

```text
data-export.generate.v1
```

---

## 59.1 Payload

Le payload contient :

```text
ExportId

ExportPlanId
```

et non les données exportées.

---

## 59.2 Idempotence

La clé peut utiliser :

```text
ExportId
```

---

## 59.3 Retry

Les retries sont bornés.

---

# 60. Stockage temporaire

Les fichiers intermédiaires utilisent une zone :

```text
App_Data/exports/staging/{ExportId}
```

ou un stockage privé équivalent.

---

## 60.1 Protection

- non public ;
- permissions restrictives ;
- quota ;
- nettoyage ;
- aucune exécution.

---

## 60.2 Promotion

L’archive finale n’est promue vers `ready` qu’après validation complète.

---

# 61. Streaming

Les datasets et médias sont exportés par streaming.

---

## 61.1 Mémoire

L’exporter ne charge pas :

- tous les leads ;
- tous les médias ;
- tout le dump ;

en mémoire.

---

## 61.2 JSON Lines

Chaque ligne peut être écrite et hashée progressivement.

---

# 62. Taille et quotas

Le système vérifie :

- estimation ;
- espace libre ;
- quota d’exports ;
- taille maximale du profil ;
- nombre d’exports concurrents.

---

## 62.1 Dépassement

L’export est refusé ou nécessite un profil opérateur spécifique.

---

## 62.2 Saturation

Une génération ne doit pas remplir le volume `App_Data`.

---

# 63. Checksums

Chaque fichier possède un hash :

```text
SHA-256
```

---

## 63.1 Fichier

`checksums.sha256` contient une ligne par fichier.

---

## 63.2 Ordre

Les paths sont triés de manière stable.

---

## 63.3 Manifest

Le manifest référence l’algorithme utilisé.

---

# 64. Hash global

Après création de l’archive, un SHA-256 global est calculé.

---

## 64.1 Enregistrement

Le hash est conservé dans :

- rapport d’export ;
- événement d’audit ;
- attestation de handover.

---

## 64.2 Transmission

Le hash peut être transmis au client par un canal distinct.

---

# 65. Signature ou attestation future

Une signature cryptographique détachée pourra être ajoutée.

---

## 65.1 Baseline MVP

Le MVP exige :

- checksums internes ;
- hash d’archive ;
- audit ;
- canal sécurisé.

---

## 65.2 Profil renforcé

Un profil réglementé peut exiger :

- signature ;
- clé de signature dédiée ;
- timestamp externe ;
- vérification hors ligne.

---

# 66. Protection de l’archive

L’archive est considérée comme sensible.

---

## 66.1 Stockage

Elle est stockée dans un espace privé.

---

## 66.2 Transfert

Le transfert utilise :

- HTTPS authentifié ;
- canal de partage sécurisé ;
- ou enveloppe cryptographique approuvée.

---

## 66.3 Chiffrement de transfert

Un handover complet doit être chiffré pour son transfert lorsque le canal ne fournit pas à lui seul les garanties requises.

---

## 66.4 Cryptographie

SiteFactory ne doit pas inventer un format cryptographique propriétaire.

Une abstraction telle que :

```text
IExportEnvelopeProtector
```

peut utiliser un mécanisme approuvé.

---

# 67. Passphrases

Une passphrase d’archive ne doit pas :

- être transmise avec l’archive ;
- apparaître dans un email commun ;
- apparaître dans les logs ;
- être passée comme argument CLI ;
- être présente dans le rapport.

---

## 67.1 Préférence

Privilégier une protection par clé publique ou un canal sécurisé géré.

---

# 68. Téléchargement administratif

Un export prêt est accessible uniquement à un utilisateur autorisé.

---

## 68.1 URL

L’URL ne doit pas être publique ou permanente.

---

## 68.2 Contrôles

- session valide ;
- permission ;
- step-up ;
- Export ID ;
- statut `ready` ;
- expiration ;
- audit.

---

## 68.3 Cache

```text
Cache-Control: no-store
```

---

## 68.4 Content-Disposition

```text
attachment
```

avec un nom neutralisé.

---

# 69. Download Grant

Un téléchargement sensible peut utiliser un grant temporaire lié à :

```text
UserId

SessionId

ExportId

ExpiresAtUtc

Nonce

UseCount
```

---

## 69.1 Baseline

Le grant peut autoriser plusieurs reprises bornées pendant une courte fenêtre.

---

## 69.2 Rejeu

Un grant expiré ou révoqué est refusé.

---

## 69.3 URL signée

Une URL signée publique n’est pas la baseline du profil VPS initial.

---

# 70. Email

L’export n’est pas envoyé comme pièce jointe.

---

## 70.1 Notification

Un email peut indiquer :

```text
votre export est prêt
```

sans contenir :

- fichier ;
- token durable ;
- donnée métier ;
- lien public permanent.

---

# 71. TTL

Valeurs initiales conceptuelles :

```text
export métier :
24 heures

export complet de handover :
72 heures maximum dans le stockage temporaire

Download Grant :
15 minutes
```

---

## 71.1 Ajustement

Le profil peut réduire ces durées.

---

## 71.2 Handover

Une copie remise au client sort du stockage temporaire SiteFactory et suit le contrat de transfert.

---

# 72. Expiration

À expiration :

```text
statut expired
→ accès refusé
→ job de suppression
→ suppression de l’archive
→ suppression des fichiers staging
→ mise à jour du statut
→ audit ou événement opérationnel
```

---

## 72.1 Téléchargement antérieur

Le téléchargement réussi ne supprime pas nécessairement immédiatement l’archive si une reprise est autorisée.

---

## 72.2 Suppression anticipée

Un utilisateur autorisé peut demander une suppression anticipée.

---

# 73. Annulation

Un export peut être annulé avant son statut `ready`.

---

## 73.1 Effets

- annulation coopérative du job ;
- suppression des fichiers temporaires ;
- statut `cancelled` ;
- audit.

---

## 73.2 Export déjà prêt

Une annulation devient une suppression anticipée.

---

# 74. Gestion des échecs

Un échec doit indiquer :

- étape ;
- Error Code ;
- dataset concerné ;
- catégorie ;
- possibilité de retry ;
- nettoyage réalisé.

---

## 74.1 Données

L’erreur ne contient pas les données exportées.

---

## 74.2 Archive partielle

Une archive partielle n’est jamais proposée au téléchargement.

---

# 75. Audit

Événements recommandés :

```text
data-export.requested

data-export.approved

data-export.started

data-export.completed

data-export.failed

data-export.downloaded

data-export.cancelled

data-export.expired

data-export.deleted

data-import.planned

data-import.started

data-import.completed

data-import.failed
```

---

## 75.1 Metadata

- Export ID ;
- profile ;
- catégories ;
- nombre d’enregistrements par classe ;
- hash global ;
- environnement ;
- raison.

---

## 75.2 Données interdites

- contenu exporté ;
- email complet ;
- secret ;
- chemin privé ;
- clé de protection.

---

# 76. Observabilité

Événements techniques recommandés :

```text
DataExportJobStarted

DataExportSnapshotCreated

DataExportDatasetWritten

DataExportMediaCopied

DataExportVerificationStarted

DataExportArchiveCreated

DataExportReady

DataExportCleanupCompleted

DataImportValidationStarted

DataImportConflictDetected
```

---

## 76.1 Propriétés autorisées

```text
ExportProfileId

DatasetId

Result

RecordCountBucket

FileCountBucket

SizeBucket

ConsistencyMode

ErrorCode

Duration

Environment
```

---

## 76.2 Données interdites

```text
ExportId dans les métriques

Email

LeadId

MediaId

UserId

FileName utilisateur

Archive path

Token
```

---

# 77. Métriques

Exemples :

```text
data exports requested

data exports completed

data exports failed

data export duration

data export bytes

data export downloads

data exports expired

data import plans

data import failures
```

---

## 77.1 Labels autorisés

```text
profile_id

dataset_id

result

consistency_mode

environment
```

---

## 77.2 Labels interdits

```text
ExportId

Client email

ActorId

Path

PrivacySubjectId

LeadId
```

---

# 78. Alertes

Alertes possibles :

```text
Export complet en échec

Archive prête non supprimée après TTL

Espace disque faible

Checksum invalide

Média manquant

Export téléchargé plusieurs fois de manière inhabituelle

Import avec nombreux conflits

Export de handover non récupéré

Job de nettoyage en échec
```

---

# 79. Import

SiteFactory peut fournir un mécanisme d’import correspondant aux profils portables.

---

## 79.1 Baseline

L’import est :

```text
explicite
planifié
validé
non destructif par défaut
```

---

## 79.2 Cible

La cible recommandée est :

- nouvel environnement ;
- base vide ;
- projet compatible ;
- stockage média vide ou isolé.

---

# 80. Profils d’import

Profils conceptuels :

```text
import.client-portable.v1

import.client-handover.v1

import.content.v1

import.leads.v1
```

---

## 80.1 Correspondance

Un profil d’export indique les profils d’import compatibles.

---

## 80.2 Import inconnu

Un profil non supporté est refusé.

---

# 81. Import Plan

Le plan doit pouvoir indiquer :

```text
SourceExportId

SourceProfile

SourceVersions

TargetClient

TargetEnvironment

TargetApplicationVersion

Datasets

Records

Media

Conflicts

MissingDependencies

RoleMappings

SchemaMigrations

PrivacyLedgerActions

ExternalProcessorActions

EstimatedDuration

BlockingIssues
```

---

## 81.1 Lecture seule

La génération du plan ne modifie pas la cible.

---

## 81.2 Expiration

Le plan est invalidé si :

- l’archive change ;
- la cible change ;
- les schémas changent ;
- les conflits évoluent ;
- la version cible change.

---

# 82. Validation initiale

Avant extraction complète :

```text
format

taille

hash global

manifest

schemaVersion

profile

client source

environment source

checksums

nombre d’entrées

protection
```

---

## 82.1 Archive non fiable

Une archive fournie par un tiers est considérée comme non fiable.

---

# 83. Sécurité d’extraction

L’importer doit refuser :

```text
paths absolus

segments ..

liens symboliques

hard links

device files

noms réservés

entrées dupliquées ambiguës

paths dépassant la limite

noms invalides

ratio de compression excessif
```

---

## 83.1 Racine

Toute extraction reste sous une racine temporaire dédiée.

---

## 83.2 Permissions

Les permissions issues de l’archive ne sont pas appliquées aveuglément.

---

# 84. Zip Slip

Les protections doivent empêcher :

```text
../../appsettings.json
```

ou tout équivalent encodé ou normalisé.

---

## 84.1 Normalisation

Le path est normalisé avant toute écriture.

---

## 84.2 Case sensitivity

Les collisions liées à la casse doivent être détectées pour la compatibilité Windows/Linux.

---

# 85. Compression bombs

L’importer limite :

- taille compressée ;
- taille décompressée ;
- ratio ;
- nombre de fichiers ;
- profondeur ;
- temps ;
- mémoire.

---

## 85.1 Dépassement

L’archive est refusée avant saturation du stockage.

---

# 86. Validation des schémas

Chaque ligne JSON est validée contre son schéma.

---

## 86.1 Erreur

Le plan indique :

- dataset ;
- numéro de ligne ;
- propriété ;
- Error Code.

Il ne copie pas la donnée complète dans le log.

---

## 86.2 Inconnu

Les champs inconnus sont refusés ou conservés selon une règle de compatibilité explicite.

---

# 87. Compatibilité de versions

L’import vérifie :

- export schema ;
- SiteFactory version ;
- module versions ;
- dataset versions ;
- profile version ;
- target compatibility.

---

## 87.1 Upgrade intermédiaire

Une conversion peut nécessiter :

```text
export schema v1
→ conversion v2
→ import cible
```

---

## 87.2 Conversion

Les convertisseurs sont :

- versionnés ;
- déterministes ;
- testés ;
- sans modification silencieuse de sens.

---

# 88. Cible isolée

Un import complet utilise une cible isolée.

---

## 88.1 Production

L’import direct dans une production active n’est pas la baseline.

---

## 88.2 Validation

Le projet cible doit être :

- compatible ;
- migré ;
- sans trafic ;
- sauvegardé si non vide ;
- doté de nouveaux epochs de sécurité.

---

# 89. Stratégies de conflit

Valeurs conceptuelles :

```text
reject

skip

create-new-id

merge-explicitly

replace-after-confirmation
```

---

## 89.1 Baseline

Pour un import complet dans une cible vide :

```text
reject on conflict
```

---

## 89.2 Merge

Une fusion nécessite :

- contrat métier ;
- rapport ;
- décision humaine ;
- tests.

---

## 89.3 Pas de last-write-wins

Une politique globale :

```text
la dernière valeur gagne
```

est interdite.

---

# 90. Mapping d’identifiants

Un import peut préserver les identifiants source lorsque :

- la cible est vide ;
- le format le permet ;
- aucune collision ;
- les contrats les considèrent stables.

---

## 90.1 Nouvelle cible

Une nouvelle identité peut être générée avec un mapping.

---

## 90.2 Mapping

Le mapping est conservé dans le rapport d’import, sans données sensibles.

---

# 91. Import des administrateurs

Les profils et rôles peuvent être importés comme propositions.

---

## 91.1 Credentials

Aucun credential n’est importé.

---

## 91.2 Activation

Les comptes nécessitent :

```text
nouvelle invitation
→ nouveau mot de passe
→ nouveau MFA
```

---

## 91.3 Sessions

Aucune session source n’est importée.

---

## 91.4 Epochs

La cible possède :

- nouveau Session Epoch ;
- nouveau Recovery Epoch ;
- nouvel Audit Epoch selon le contexte.

---

# 92. Import des secrets

Les secrets ne font pas partie du paquet.

---

## 92.1 Préflight

L’import plan génère une liste de Secret IDs à reprovisionner.

---

## 92.2 Valeurs

Les valeurs sont créées dans le système de secrets de la cible.

---

## 92.3 Blocage

Les fonctionnalités dépendantes restent désactivées jusqu’au provisioning.

---

# 93. Import des médias

Le flux est :

```text
vérifier les checksums

stager les blobs

valider les métadonnées

écrire les métadonnées

promouvoir les blobs

vérifier les références

réconcilier
```

---

## 93.1 Provider

Le provider cible peut différer du provider source.

---

## 93.2 URLs

Les URLs publiques sont recréées depuis les Media IDs.

---

## 93.3 Fichiers manquants

Un import complet échoue si un média obligatoire manque.

---

# 94. Import de l’audit

La piste source ne doit pas être fusionnée dans l’epoch cible courant.

---

## 94.1 Options

- conservation comme archive consultable ;
- import dans un namespace source ;
- remise séparée sans import.

---

## 94.2 Intégrité

Les hashes source restent inchangés.

---

## 94.3 Nouvel événement

L’environnement cible audite :

```text
audit.external-history.imported
```

ou une action équivalente.

---

# 95. Import du Deletion Ledger

Le ledger doit être importé avant l’ouverture du service.

---

## 95.1 Objectif

Empêcher l’import de données qui devraient rester supprimées.

---

## 95.2 Conflit

Une donnée présente dans le dataset mais marquée supprimée par un ledger plus récent est :

- omise ;
- supprimée ;
- ou bloquée pour revue.

---

## 95.3 Priorité

Le ledger le plus récent et vérifié prévaut sur un export métier plus ancien.

---

# 96. Retention Holds

Les holds transférés sont importés seulement lorsque :

- leur responsabilité est transférée ;
- ils sont encore valides ;
- leur owner cible est défini.

---

## 96.1 Hold inconnu

Un hold sans propriétaire ou justification compatible bloque le handover complet.

---

# 97. Import et fournisseurs externes

L’import ne doit pas déclencher automatiquement :

- emails ;
- CRM sync ;
- webhooks ;
- analytics ;
- notifications ;
- suppression externe.

---

## 97.1 Mode isolé

Les intégrations restent désactivées jusqu’à validation.

---

## 97.2 Réactivation

La réactivation suit un plan distinct.

---

# 98. Application de l’import

Commande conceptuelle :

```text
sitefactory data import apply
```

---

## 98.1 Préconditions

```text
plan valide

cible confirmée

environnement confirmé

backup si cible non vide

secrets non requis pour la phase

integrations disabled

worker contrôlé

step-up ou opérateur

audit disponible
```

---

## 98.2 Effets

L’import peut :

- écrire des données ;
- écrire des médias ;
- créer des mappings ;
- initialiser des catalogues ;
- créer un rapport.

---

# 99. Transactions

Les données PostgreSQL utilisent des transactions bornées.

---

## 99.1 Import global

Un très gros import ne maintient pas nécessairement une transaction unique.

---

## 99.2 Checkpoints

Les batches possèdent des checkpoints et une stratégie de rollback ou reprise.

---

## 99.3 Visibilité

Le service cible reste fermé jusqu’à validation finale.

---

# 100. Échec partiel

Un import partiel n’est pas considéré comme réussi.

---

## 100.1 Statuts

```text
failed

partially-applied

rolled-back

completed
```

---

## 100.2 Reprise

La reprise utilise :

- Import ID ;
- plan ;
- checkpoint ;
- idempotence.

---

# 101. Vérification après import

Vérifications :

```text
record counts

checksums

relations

médias

Privacy Data Catalog

Deletion Ledger

holds

rôles proposés

epochs

health checks

smoke tests

réconciliation
```

---

## 101.1 Rapport

Nom conceptuel :

```text
data-import-report.json
```

---

# 102. Ouverture du service cible

Le service ne peut être ouvert qu’après :

- validation ;
- import complet ;
- secrets provisionnés ;
- intégrations revues ;
- comptes réinvités ;
- jobs contrôlés ;
- ledger appliqué ;
- audits initialisés ;
- smoke tests réussis.

---

# 103. Export portable et restore

Un export portable ne remplace pas un restore opérationnel ordinaire.

---

## 103.1 Restore

Privilégie :

```text
backup natif compatible
```

---

## 103.2 Import

Privilégie :

```text
transfert, migration ou reconstruction logique
```

---

# 104. Handover complet

Le handover doit comprendre :

```text
repository Git

export portable complet

médias

audit selon périmètre

Privacy Data Catalog

Deletion Ledger

Retention Holds

versions et manifests

inventaire des jobs

inventaire des intégrations

liste des secrets à recréer

runbooks

rapports de vérification

hashes et attestations
```

---

## 104.1 Backup natif

Peut être ajouté pour faciliter une reprise fidèle.

---

## 104.2 Credentials

Ils sont recréés ou transférés par un processus distinct.

---

# 105. Attestation de handover

Un document peut conserver :

```text
HandoverId

ExportId

ArchiveHash

RepositoryCommit

ApplicationVersion

LedgerSequence

AuditEpochs

CreatedAtUtc

TransferredAtUtc

Recipient

VerificationResult
```

---

## 105.1 Signature

Une signature future peut renforcer le document.

---

## 105.2 Données

L’attestation ne contient pas de secret.

---

# 106. Vérification par le destinataire

Le client ou nouveau prestataire doit pouvoir :

- recalculer le hash ;
- vérifier les checksums ;
- lire le manifest ;
- valider les schémas ;
- compter les fichiers ;
- tester un import en environnement isolé.

---

## 106.1 Acceptation

La remise n’est pas considérée techniquement vérifiée uniquement parce qu’un fichier a été téléchargé.

---

# 107. Clôture de service

La clôture suit :

```text
geler les modifications selon le plan

drainer ou inventorier les jobs

produire l’export

vérifier

remettre

faire vérifier

transférer le repository

transférer les responsabilités

révoquer les accès LogicGate

appliquer la rétention des copies LogicGate

auditer
```

---

## 107.1 Copies LogicGate

Les copies temporaires de l’export sont supprimées selon le TTL ou le plan de clôture.

---

## 107.2 Backup résiduel

La conservation résiduelle des backups suit l’ADR-0023.

---

# 108. Sécurité

## 108.1 Actifs

- données métier ;
- données personnelles ;
- contenus ;
- médias ;
- audit ;
- ledger ;
- archives ;
- manifests ;
- hashes ;
- repository ;
- identité du destinataire.

---

## 108.2 Menaces

- export par un acteur non autorisé ;
- export téléchargé par une session volée ;
- archive publique ;
- secret inclus ;
- path traversal ;
- zip bomb ;
- archive modifiée ;
- mauvais client ;
- import dans la mauvaise production ;
- password hashes transférés ;
- donnée supprimée réimportée ;
- export oublié après expiration.

---

## 108.3 Contrôles

- permissions ;
- step-up ;
- plan ;
- audit ;
- stockage privé ;
- TTL ;
- checksums ;
- hash global ;
- schemas ;
- validation d’archive ;
- absence de secrets ;
- Deletion Ledger ;
- import isolé ;
- rapports ;
- nettoyage.

---

# 109. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Export massif non autorisé | Permission et step-up |
| Archive placée publiquement | Stockage privé et no-store |
| Secret inclus | Allowlist des datasets et secret scan |
| Password hash transféré | Exclusion Identity stricte |
| Archive modifiée | Checksums et hash global audité |
| Zip Slip | Validation des paths |
| Compression bomb | Quotas et limites d’extraction |
| Mauvais client exporté | Client et environnement dans le plan |
| Donnée supprimée réimportée | Deletion Ledger |
| Sessions réactivées | Sessions exclues et nouveaux epochs |
| Média manquant | Manifest et checksum |
| Export oublié | TTL et job de suppression |

---

# 110. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-EXPORT-001` | Profil d’export inconnu |
| `SF-EXPORT-002` | Version de profil non supportée |
| `SF-EXPORT-003` | Permission d’export absente |
| `SF-EXPORT-004` | Step-up requis |
| `SF-EXPORT-005` | Raison obligatoire absente |
| `SF-EXPORT-006` | Plan absent |
| `SF-EXPORT-007` | Plan invalide |
| `SF-EXPORT-008` | Plan expiré |
| `SF-EXPORT-009` | Plan ne correspondant plus aux données |
| `SF-EXPORT-010` | Environnement source incompatible |
| `SF-EXPORT-011` | Client source incompatible |
| `SF-EXPORT-012` | Mode de cohérence non supporté |
| `SF-EXPORT-013` | Maintenance window requise |
| `SF-EXPORT-014` | Snapshot PostgreSQL impossible |
| `SF-EXPORT-015` | Écriture non suspendue |
| `SF-EXPORT-016` | Job mutateur encore actif |
| `SF-EXPORT-017` | Dataset inconnu |
| `SF-EXPORT-018` | Exporter de module absent |
| `SF-EXPORT-019` | Schéma d’export absent |
| `SF-EXPORT-020` | Enregistrement invalide |
| `SF-EXPORT-021` | Média manquant |
| `SF-EXPORT-022` | Checksum média invalide |
| `SF-EXPORT-023` | Fichier interdit détecté |
| `SF-EXPORT-024` | Secret détecté |
| `SF-EXPORT-025` | Credential détecté |
| `SF-EXPORT-026` | Password hash détecté |
| `SF-EXPORT-027` | Espace disque insuffisant |
| `SF-EXPORT-028` | Quota d’export dépassé |
| `SF-EXPORT-029` | Archive impossible à créer |
| `SF-EXPORT-030` | Archive trop volumineuse |
| `SF-EXPORT-031` | Checksum global invalide |
| `SF-EXPORT-032` | Protection d’archive échouée |
| `SF-EXPORT-033` | Export non vérifié |
| `SF-EXPORT-034` | Export non prêt |
| `SF-EXPORT-035` | Export expiré |
| `SF-EXPORT-036` | Download Grant invalide |
| `SF-EXPORT-037` | Download Grant expiré |
| `SF-EXPORT-038` | Téléchargement non autorisé |
| `SF-EXPORT-039` | Nettoyage échoué |
| `SF-EXPORT-040` | Rapport absent |
| `SF-ARCHIVE-001` | Format d’archive inconnu |
| `SF-ARCHIVE-002` | Path absolu interdit |
| `SF-ARCHIVE-003` | Traversée de chemin détectée |
| `SF-ARCHIVE-004` | Lien symbolique interdit |
| `SF-ARCHIVE-005` | Entrée dupliquée |
| `SF-ARCHIVE-006` | Nombre d’entrées excessif |
| `SF-ARCHIVE-007` | Taille décompressée excessive |
| `SF-ARCHIVE-008` | Ratio de compression dangereux |
| `SF-ARCHIVE-009` | Nom de fichier invalide |
| `SF-ARCHIVE-010` | Collision de casse détectée |
| `SF-IMPORT-001` | Profil d’import inconnu |
| `SF-IMPORT-002` | Permission d’import absente |
| `SF-IMPORT-003` | Plan d’import absent |
| `SF-IMPORT-004` | Plan d’import invalide |
| `SF-IMPORT-005` | Hash d’archive invalide |
| `SF-IMPORT-006` | Manifest invalide |
| `SF-IMPORT-007` | Schema Version incompatible |
| `SF-IMPORT-008` | Dataset incompatible |
| `SF-IMPORT-009` | Cible non isolée |
| `SF-IMPORT-010` | Cible production active |
| `SF-IMPORT-011` | Conflit d’identifiant |
| `SF-IMPORT-012` | Stratégie de conflit absente |
| `SF-IMPORT-013` | Relation orpheline |
| `SF-IMPORT-014` | Média invalide |
| `SF-IMPORT-015` | Secret requis non provisionné |
| `SF-IMPORT-016` | Identité nécessitant un reprovisioning |
| `SF-IMPORT-017` | Deletion Ledger absent |
| `SF-IMPORT-018` | Donnée supprimée détectée dans l’import |
| `SF-IMPORT-019` | Retention Hold incompatible |
| `SF-IMPORT-020` | Intégration externe encore active |
| `SF-IMPORT-021` | Import partiellement appliqué |
| `SF-IMPORT-022` | Rollback impossible |
| `SF-IMPORT-023` | Vérification finale échouée |
| `SF-IMPORT-024` | Rapport d’import absent |

---

# 111. Tests unitaires

Tester notamment :

- Export Profile ID ;
- schéma ;
- manifest ;
- paths ;
- dates ;
- identifiants ;
- enums ;
- valeurs monétaires ;
- checksum ;
- plan ;
- états ;
- TTL ;
- grants.

---

# 112. Tests des datasets

Chaque module doit vérifier :

```text
sérialisation

désérialisation

ordre stable

schéma

champs obligatoires

relations

versions

valeurs inconnues
```

---

# 113. Tests des secrets

Injecter des marqueurs représentant :

- mot de passe ;
- token ;
- clé API ;
- chaîne de connexion ;
- TOTP secret ;
- recovery code ;
- cookie ;
- password hash.

Vérifier leur absence dans :

- archives ;
- manifests ;
- JSON Lines ;
- CSV ;
- rapports ;
- audit ;
- logs.

---

# 114. Tests de cohérence

Scénarios :

```text
export best-effort avec modifications concurrentes

export point-in-time PostgreSQL

export maintenance-window

upload média pendant l’export

suppression média pendant l’export

job de variante pendant l’export
```

---

## 114.1 Résultat

Le profil doit respecter la garantie annoncée dans le manifest.

---

# 115. Tests des médias

Vérifier :

- original ;
- variante ;
- hash ;
- fichier manquant ;
- mauvais Content-Type ;
- nom d’origine dangereux ;
- gros média ;
- streaming ;
- provider filesystem ;
- provider fake.

---

# 116. Tests Identity

Vérifier que l’export n’inclut jamais :

```text
PasswordHash

SecurityStamp

AuthenticatorKey

RecoveryCodes

Sessions

Tokens

DeliverySecrets
```

---

## 116.1 Import

Vérifier que les comptes importés exigent une nouvelle invitation.

---

# 117. Tests de l’audit

Vérifier :

- epochs ;
- séquence ;
- hashes ;
- metadata autorisée ;
- export autorisé ;
- export refusé ;
- piste non fusionnée dans l’epoch cible.

---

# 118. Tests du Privacy Data Catalog

Vérifier :

- catégories ;
- policies ;
- owners ;
- processors ;
- versions ;
- absence de données métier.

---

# 119. Tests du Deletion Ledger

Vérifier :

- inclusion ;
- sequence ;
- hash ;
- import ;
- donnée ancienne présente dans l’export ;
- suppression prévalant après import.

---

# 120. Tests d’archive

Couvrir :

```text
ZIP valide

path absolu

../

path encodé

symlink

hard link

entrée dupliquée

collision de casse

fichier réservé Windows

trop de fichiers

archive tronquée

checksum invalide
```

---

# 121. Tests de compression bomb

Créer des archives de test simulant :

- ratio excessif ;
- très grande taille décompressée ;
- profondeur excessive ;
- nombre excessif d’entrées.

L’import doit refuser avant saturation.

---

# 122. Tests de téléchargement

Vérifier :

- permission ;
- step-up ;
- grant ;
- TTL ;
- no-store ;
- attachment ;
- audit ;
- export expiré ;
- téléchargement concurrent ;
- session révoquée.

---

# 123. Tests de nettoyage

Vérifier :

- expiration ;
- accès immédiatement refusé ;
- suppression archive ;
- suppression staging ;
- retry ;
- orphelin ;
- audit ;
- métriques.

---

# 124. Tests d’import

Couvrir :

```text
cible vide

cible non vide

conflit d’identifiant

relation manquante

schéma ancien

schéma futur

média manquant

secret absent

admin à reprovisionner

ledger absent

hold inconnu

provider externe désactivé
```

---

# 125. Tests d’import partiel

Simuler un échec :

- pendant staging média ;
- pendant PostgreSQL ;
- pendant promotion média ;
- pendant rapport ;
- pendant vérification.

Vérifier :

- checkpoint ;
- reprise ;
- rollback ;
- service non ouvert.

---

# 126. Tests de handover

Produire un paquet complet puis vérifier :

- manifest ;
- repository commit référencé ;
- application version ;
- media count ;
- ledger sequence ;
- audit epochs ;
- hash global ;
- liste des secrets à recréer ;
- runbooks ;
- import en environnement isolé.

---

# 127. Tests multiplateformes

Les tests de format, archives et import sont exécutés sur :

```text
Windows x64

Linux x64
```

---

## 127.1 Paths

Les tests doivent couvrir les différences :

- casse ;
- séparateurs ;
- noms réservés ;
- longueurs ;
- encodage.

---

# 128. Quality gates

Une contribution est bloquée lorsque :

```text
Un dataset n’a pas de schéma.

Un module conserve des données sans exporter correspondant.

Un export contient un secret.

Un export contient un password hash.

Un export contient une session ou un token.

Un export complet ne contient pas les médias requis.

Un fichier n’a pas de checksum.

Le manifest est incomplet.

Le format dépend de la locale de la machine.

Un path utilisateur devient un path d’archive.

Un export partiel est téléchargeable.

Un export ne possède pas de TTL.

Un handover n’inclut pas le Deletion Ledger.

Un import peut créer des administrateurs actifs.

Un import peut ouvrir directement la production.

Les tests de path traversal échouent.
```

---

# 129. Quality gates de production

La production est bloquée lorsque :

```text
Les permissions d’export sont absentes.

Le step-up n’est pas appliqué.

Le stockage d’export est public.

Le job de nettoyage est absent.

L’espace disque n’est pas surveillé.

Le profil de handover n’est pas validé.

Le hash global n’est pas produit.

Le ledger n’est pas à jour.

Les query strings de téléchargement sont logguées.

Le téléchargement ne possède pas no-store.

Une archive peut être envoyée par email.

La procédure d’import ou handover n’est pas documentée.
```

---

# 130. CI/CD

La CI doit vérifier :

- schemas ;
- serializers ;
- secret scan ;
- password-hash exclusion ;
- manifests ;
- checksums ;
- archives ;
- path traversal ;
- compression bombs ;
- import ;
- médias ;
- Identity ;
- ledger ;
- tests multiplateformes.

---

## 130.1 Golden exports

Des exports de référence peuvent être utilisés.

---

## 130.2 Données

Ils utilisent uniquement des données fictives.

---

## 130.3 Diff

Une évolution du format doit être revue explicitement.

---

# 131. Règles pour les modules

Chaque module conservant des données portables doit déclarer :

```text
ExportDatasetId

ExportSchemaVersion

ExportHandler

ImportHandler éventuel

Dependencies

PersonalDataClassification

MediaReferences

Ordering

VerificationHandler
```

---

## 131.1 Donnée non exportable

Une donnée non exportable doit être justifiée et apparaître dans le manifest comme omise.

---

## 131.2 Interdiction

Un module ne doit pas sérialiser directement ses entités EF Core comme contrat public.

---

# 132. Règles pour les customizations

Une customization ajoutant une donnée doit fournir :

- schéma ;
- exporter ;
- importeur lorsque nécessaire ;
- tests ;
- classification ;
- documentation de handover.

---

## 132.1 Nom de dataset

Utiliser un namespace client explicite.

Exemple :

```text
client.sample-project.quote-approvals.v1
```

---

## 132.2 Secret

Une customization ne doit pas ajouter de secret au paquet.

---

# 133. Règles pour LogicGate

LogicGate doit :

- sélectionner le bon profil ;
- vérifier le client et l’environnement ;
- protéger les archives ;
- transmettre le hash ;
- gérer le TTL ;
- faire vérifier le paquet ;
- documenter les omissions ;
- préparer le handover ;
- supprimer les copies temporaires.

---

## 133.1 LogicGate ne doit pas

- envoyer un export par email ordinaire ;
- inclure des password hashes ;
- utiliser un export ancien sans vérifier le ledger ;
- importer directement en production ;
- remettre une archive sans checksum ;
- conserver indéfiniment les exports ;
- annoncer un handover complet si des catégories obligatoires manquent.

---

# 134. Règles pour Codex

Codex peut :

- définir les schémas ;
- implémenter les exporters ;
- implémenter les importers ;
- ajouter les tests ;
- créer les manifests ;
- créer les commandes ;
- documenter les runbooks.

---

## 134.1 Codex ne doit pas

```text
Exporter des données de production

Télécharger une archive réelle

Envoyer une archive

Inventer une clé de chiffrement

Inclure un secret

Inclure un password hash

Importer dans la production

Contourner le step-up

Supprimer un export réel

Modifier un Deletion Ledger réel

Créer un administrateur actif après import
```

---

## 134.2 Décision humaine

Une décision humaine est requise pour :

- export complet réel ;
- handover ;
- téléchargement d’une archive sensible ;
- durée de conservation exceptionnelle ;
- profil contenant l’audit ;
- import ;
- stratégie de conflit ;
- remise à un tiers ;
- snapshot natif ;
- protection cryptographique du transfert.

---

# 135. Anti-patterns interdits

## 135.1 Dump présenté comme format portable

Le client reçoit uniquement un fichier PostgreSQL non documenté.

---

## 135.2 CSV sans schéma

Les colonnes et enums ne sont pas documentés.

---

## 135.3 Export de la table Identity

Tous les champs Identity, y compris les hashes, sont exportés.

---

## 135.4 Archive dans `wwwroot`

Un lien public permet de télécharger les leads.

---

## 135.5 Export envoyé par email

Le fichier complet est joint à une notification SMTP.

---

## 135.6 Path basé sur le nom utilisateur

Le nom d’origine d’un média pilote le path de l’archive.

---

## 135.7 Import last-write-wins

Les données cibles sont écrasées sans revue.

---

## 135.8 Import activant les comptes

Les administrateurs source peuvent se connecter immédiatement.

---

## 135.9 Import ignorant le ledger

Des leads supprimés réapparaissent.

---

## 135.10 Archive sans TTL

Les exports s’accumulent sur le VPS.

---

## 135.11 Archive partielle téléchargeable

Un export échoué est remis comme s’il était complet.

---

## 135.12 Passphrase dans le rapport

La protection de l’archive est annulée par sa documentation.

---

# 136. Conséquences positives

La décision apporte :

- portabilité réelle ;
- formats inspectables ;
- transfert client facilité ;
- données et médias cohérents ;
- absence de credentials ;
- intégrité vérifiable ;
- prévention de la réapparition des suppressions ;
- imports contrôlés ;
- meilleure autonomie ;
- meilleure documentation ;
- réduction du verrouillage fournisseur ;
- handovers testables.

---

# 137. Conséquences négatives

La décision entraîne :

- schemas supplémentaires ;
- serializers ;
- importers ;
- jobs ;
- manifests ;
- checksums ;
- stockage temporaire ;
- tests d’archive ;
- maintenance des versions ;
- coordination des snapshots ;
- procédures de transfert ;
- gestion des gros volumes.

---

# 138. Risques acceptés

## 138.1 Taille des exports

Un handover complet peut être volumineux.

Réduction :

- streaming ;
- compression ;
- quotas ;
- fenêtre de maintenance ;
- object storage futur.

---

## 138.2 Données sensibles dans l’archive

Même sans secrets, un export peut contenir des données personnelles.

Réduction :

- permissions ;
- step-up ;
- chiffrement ;
- TTL ;
- canal sécurisé ;
- audit.

---

## 138.3 Format évolutif

Un ancien export peut nécessiter un convertisseur.

Réduction :

- Schema Version ;
- compatibilité ;
- convertisseurs testés ;
- schémas inclus.

---

## 138.4 Import non universel

Un autre système peut ne pas supporter directement toutes les données.

Le format reste néanmoins documenté et inspectable.

---

# 139. Mesures compensatoires

- profils versionnés ;
- schemas ;
- manifest ;
- checksums ;
- hash global ;
- secret scan ;
- password-hash exclusion ;
- maintenance window ;
- stockage privé ;
- TTL ;
- step-up ;
- audit ;
- imports isolés ;
- Deletion Ledger ;
- tests de sécurité ;
- handover vérifié.

---

# 140. Performance

## 140.1 Streaming

Les exports et imports sont conçus pour le streaming.

---

## 140.2 Pagination PostgreSQL

Les datasets utilisent :

- curseurs ;
- pagination par clé ;
- batches ;
- snapshot cohérent.

---

## 140.3 Compression

La compression est équilibrée avec :

- CPU ;
- durée ;
- espace disque ;
- taille finale.

---

## 140.4 Médias

Les fichiers sont copiés en stream avec calcul de checksum simultané.

---

# 141. Coûts

## 141.1 Coûts supplémentaires

- stockage temporaire ;
- CPU ;
- bande passante ;
- développement ;
- tests ;
- chiffrement ;
- exploitation ;
- support d’import.

---

## 141.2 Coûts évités

- handover manuel ;
- perte de données ;
- dépendance permanente ;
- migrations improvisées ;
- médias oubliés ;
- credentials exposés ;
- litiges sur le contenu transféré ;
- import impossible à vérifier.

---

# 142. Maintenabilité

Pour chaque dataset, il doit être possible de répondre :

```text
Quel est son Dataset ID ?

Quel est son schéma ?

Quelle version ?

Qui le possède ?

Contient-il des données personnelles ?

Comment est-il exporté ?

Comment est-il importé ?

Quelles relations utilise-t-il ?

Quels médias référence-t-il ?

Comment est-il vérifié ?

Comment évolue-t-il entre deux versions ?
```

---

# 143. Compatibilité

## 143.1 Manifest

Le manifest possède une `schemaVersion`.

---

## 143.2 Datasets

Chaque dataset possède sa propre version.

---

## 143.3 Export Profile

Le profil possède une version indépendante.

---

## 143.4 Importer ancien

Un importer ancien rencontrant un schéma futur doit refuser avant écriture.

---

## 143.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

L’adoption d’exports existants non structurés nécessite une migration.

---

# 144. Migration depuis des exports ad hoc

Le processus est :

```text
inventorier les scripts existants

identifier les colonnes

identifier les données omises

définir les Dataset IDs

créer les schemas

créer le manifest

ajouter les checksums

ajouter les tests

remplacer les scripts
```

---

# 145. Migration depuis un dump uniquement

Le processus est :

```text
conserver le dump comme snapshot natif

implémenter les exporters logiques

produire un export portable

comparer les comptes

comparer les médias

tester un import isolé

documenter les différences
```

---

# 146. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- d’exporter des données réelles ;
- de télécharger des leads ;
- de remettre une archive ;
- de créer une clé de chiffrement ;
- d’importer ;
- d’ouvrir un environnement cible ;
- de supprimer un export ;
- de transférer un repository ;
- d’exécuter un handover.

---

# 147. Réversibilité

## 147.1 Classification

```text
élevée avant remise

moyenne après import

faible après écrasement ou diffusion externe
```

---

## 147.2 Export

Créer un export ne modifie pas les données métier.

---

## 147.3 Import

La réversibilité dépend :

- de la cible ;
- du backup ;
- des conflits ;
- des effets externes.

---

# 148. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `ExportId` ;
- `ExportProfileId` ;
- `DatasetId` ;
- états ;
- manifests ;
- erreurs.

---

## Étape 2 — Schémas

Créer :

- manifest ;
- contenu ;
- leads ;
- médias ;
- Identity ;
- privacy ;
- audit.

---

## Étape 3 — Exporters

Implémenter :

- contenu ;
- leads ;
- médias ;
- configuration ;
- privacy.

---

## Étape 4 — Job d’export

Ajouter :

- plan ;
- snapshot ;
- streaming ;
- staging ;
- état ;
- retry.

---

## Étape 5 — Vérification

Ajouter :

- checksums ;
- hash global ;
- validation schemas ;
- comptes ;
- médias.

---

## Étape 6 — Téléchargement

Ajouter :

- permissions ;
- step-up ;
- grants ;
- no-store ;
- audit ;
- TTL.

---

## Étape 7 — Handover

Créer le profil complet et l’attestation.

---

## Étape 8 — Import Plan

Ajouter :

- validation ;
- compatibilité ;
- conflits ;
- mapping ;
- secrets requis.

---

## Étape 9 — Import Apply

Ajouter :

- staging ;
- PostgreSQL ;
- médias ;
- ledger ;
- verification.

---

## Étape 10 — Quality gates

Ajouter :

- secret scan ;
- path traversal ;
- compression bombs ;
- tests multiplateformes ;
- golden exports.

---

# 149. Tâches liées proposées

```text
SF-TASK-310 — Define portable export and dataset contracts

SF-TASK-311 — Define the versioned export manifest schema

SF-TASK-312 — Add JSON Lines export infrastructure

SF-TASK-313 — Add portable content export

SF-TASK-314 — Add portable lead export

SF-TASK-315 — Add portable media export and verification

SF-TASK-316 — Add Identity export without credentials

SF-TASK-317 — Add privacy catalog and deletion-ledger export

SF-TASK-318 — Add audit export integration

SF-TASK-319 — Add durable export-generation jobs

SF-TASK-320 — Add point-in-time export snapshots

SF-TASK-321 — Add export checksums and archive hashing

SF-TASK-322 — Add protected export download grants

SF-TASK-323 — Add export TTL and cleanup jobs

SF-TASK-324 — Add complete client-handover exports

SF-TASK-325 — Define portable import-plan contracts

SF-TASK-326 — Add secure archive extraction

SF-TASK-327 — Add portable data import staging

SF-TASK-328 — Add Identity reprovisioning after import

SF-TASK-329 — Add deletion-ledger import reconciliation

SF-TASK-330 — Add export and import security tests

SF-TASK-331 — Add handover verification runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 150. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Les profils d’export sont versionnés.

Le manifest est obligatoire.

Les datasets possèdent des JSON Schemas.

Les données canoniques utilisent JSON Lines.

Les dates sont en UTC.

Les enums utilisent des identifiants stables.

Les montants sont indépendants de la locale.

Les médias possèdent des checksums.

L’archive possède un hash global.

Aucun secret n’apparaît.

Aucun password hash n’apparaît.

Aucune session ou token n’apparaît.

Un export incomplet n’est pas téléchargeable.

Un export sensible exige un step-up.

Un export possède un TTL.

Un export expiré devient inaccessible.

Les fichiers temporaires sont supprimés.

Les paths d’archive sont sûrs.

Les compression bombs sont refusées.

Les comptes importés nécessitent un nouveau provisioning.

Les secrets sont recréés séparément.

Le Deletion Ledger est inclus dans le handover.

Une donnée supprimée n’est pas réimportée.

L’audit source n’est pas fusionné dans l’epoch cible courant.

L’import s’exécute dans un environnement isolé.

Le service cible reste fermé jusqu’à vérification.

Le handover est vérifiable par le destinataire.

Les tests fonctionnent sur Windows et Linux.
```

---

# 151. Conditions de révision

Réexaminer cette décision si :

- les exports atteignent régulièrement plusieurs centaines de gigaoctets ;
- un object storage devient la baseline ;
- un standard sectoriel obligatoire est imposé ;
- une plateforme centrale de migration est créée ;
- plusieurs services doivent produire un export commun ;
- les clients exigent un export continu ;
- un data warehouse devient la source principale ;
- un mécanisme de signature réglementée devient nécessaire ;
- les handovers doivent être entièrement automatisés ;
- plusieurs tenants partagent une même base ;
- les imports entre clients deviennent fréquents ;
- une API de portabilité temps réel est introduite.

---

# 152. Indicateurs à suivre

- exports demandés ;
- exports terminés ;
- exports échoués ;
- durée ;
- taille ;
- médias manquants ;
- checksum failures ;
- téléchargements ;
- exports expirés ;
- nettoyages échoués ;
- imports planifiés ;
- conflits d’import ;
- imports échoués ;
- handovers vérifiés ;
- datasets sans exporter ;
- schemas incompatibles ;
- archives refusées.

---

# 153. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des exports contiennent des secrets

des password hashes sont remis

des archives sont accessibles publiquement

les exports s’accumulent

les checksums ne sont pas vérifiés

les médias manquent régulièrement

les formats changent sans version

les imports écrasent silencieusement

les comptes deviennent actifs après import

le ledger n’est pas transféré

des données supprimées réapparaissent

les handovers reposent uniquement sur un dump PostgreSQL

les paths d’archive sont construits depuis des noms utilisateurs
```

---

# 154. Conséquences observées

Cette section est append-only après mise en œuvre.

## 154.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
des formats d’export portables,
des paquets de handover
et des imports contrôlés.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- profil ;
- taille ;
- durée ;
- datasets ;
- résultat ;
- incident ou bénéfice ;
- action proposée.

---

# 155. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
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
- [0005-file-generation-strategies.md](0005-file-generation-strategies.md)
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
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)

---

# 156. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 151 |

---

# 157. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 158. Synthèse de la décision

```text
Profils
→ export métier
→ export portable
→ handover complet
→ privacy subject
→ audit

Plan
→ catégories
→ filtres
→ volumes
→ cohérence
→ protection
→ TTL
→ approbation

Génération
→ job durable
→ snapshot PostgreSQL
→ maintenance window pour export complet
→ manifest média
→ streaming
→ staging

Format
→ JSON Lines
→ UTF-8
→ JSON Schema
→ IDs stables
→ dates UTC
→ manifest
→ README

Médias
→ originaux
→ variantes selon profil
→ Media IDs
→ checksums
→ aucun path fournisseur exposé

Identity
→ profils et rôles uniquement
→ aucun password hash
→ aucun TOTP secret
→ aucune session
→ nouvelle invitation après import

Sécurité
→ permission
→ step-up
→ raison
→ stockage privé
→ no-store
→ aucun email avec pièce jointe
→ TTL
→ nettoyage

Intégrité
→ SHA-256 par fichier
→ hash global de l’archive
→ audit
→ vérification par le destinataire

Privacy
→ Privacy Data Catalog
→ Retention Holds
→ Deletion Ledger
→ suppressions préservées après import

Import
→ archive non fiable
→ path traversal refusé
→ compression bomb refusée
→ schemas validés
→ plan
→ cible isolée
→ conflits explicites
→ service fermé jusqu’à vérification

Handover
→ repository Git séparé
→ export portable complet
→ backup natif facultatif
→ runbooks
→ liste des secrets à recréer
→ attestation
→ révocation des accès LogicGate
```

> La portabilité ne consiste pas à remettre un dump de base de données ou quelques fichiers CSV. Elle consiste à fournir un ensemble documenté, cohérent et vérifiable de données, de médias, de schémas et de règles permettant au client de comprendre, conserver et réutiliser son patrimoine sans dépendre des secrets, des sessions ou de l’infrastructure interne de LogicGate.
