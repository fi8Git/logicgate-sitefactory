---
adrId: ADR-0016
title: Separate versioned structure, editable content and media blob storage
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
  - ../03-generation/repository-documentation-end-to-end.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/09-module-architecture.md
  - ../03-generation/generated-runtime-database-model.md
  - ../03-generation/14-client-configuration-schema.md
  - ./0015-deterministic-generation-and-optional-ai.md
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
  - ./0002-yaml-client-configuration.md
  - ./0003-postgresql-as-primary-database.md
  - ./0004-explicit-production-migrations.md
  - ./0005-file-generation-strategies.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
targetVersion: 0.1.0
---

# ADR-0016 — Frontières de stockage des contenus et médias

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le public ne fournit pas encore de module de contenu éditable, de média,
de BlobStorage ou d'upload. Le site Documentation rend les sources Markdown
autorisées par le [pipeline documentaire](../03-generation/repository-documentation-end-to-end.md) ;
cela ne constitue pas le système de stockage applicatif décrit ci-dessous.
PostgreSQL pour métadonnées/contenus structurés et stockage de blobs séparé
restent la décision cible pour une future application qui en a besoin.
Aucun asset propriétaire, volume client ou fournisseur S3 n'est requis
pour construire ou tester les composants publics.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory sépare explicitement :

```text
structure versionnée du projet
contenus éditables
métadonnées des médias
fichiers binaires des médias
assets statiques de build
artefacts temporaires
```

La baseline retenue est :

```text
Configuration et structure
→ repository Git

Contenus administrables
→ PostgreSQL

Métadonnées des médias
→ PostgreSQL

Fichiers des médias
→ stockage de blobs abstrait
→ App_Data persistant pour le profil docker-compose-vps initial

Assets statiques nécessaires au build
→ repository et image OCI

Fichiers temporaires
→ stockage temporaire borné et non persistant
```

Les fichiers médias déposés par les utilisateurs ou administrateurs ne doivent pas être :

- committés dans Git ;
- inclus dans l’image OCI ;
- stockés dans le repository ;
- conservés dans le filesystem éphémère du conteneur ;
- servis directement depuis un chemin hôte non contrôlé ;
- stockés dans PostgreSQL par défaut.

---

## 2. Résumé

L’architecture de référence est :

```text
Repository Git
├── configuration structurelle
├── templates
├── thèmes
├── code
├── contrats
├── migrations
├── assets statiques de build
└── seeds de contenu initiaux éventuels

PostgreSQL
├── pages
├── sections
├── contenus éditables
├── métadonnées SEO
├── versions éditoriales
├── références de médias
├── métadonnées de médias
└── états de publication

Media storage
├── originaux validés
├── variantes dérivées
├── miniatures
└── fichiers importés

App_Data initial
→ volume Docker persistant propre au client et à l’environnement

Object storage futur
→ compatible S3 ou fournisseur équivalent
→ remplacement derrière un contrat Infrastructure
```

L’application ne doit pas exposer le chemin physique d’un média dans ses contrats publics.

Un média est identifié par :

```text
MediaId
```

et résolu vers une clé interne :

```text
StorageKey
```

L’URL publique reste indépendante du fournisseur de stockage.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Un contenu éditable n’est pas une configuration structurelle.

Un média runtime n’est pas un asset de build.

Une URL publique n’est pas un chemin de stockage.

Un nom de fichier fourni par l’utilisateur n’est pas une clé de stockage.

Une ligne PostgreSQL ne garantit pas à elle seule l’existence du blob.

Un blob ne garantit pas à lui seul l’existence de ses métadonnées.

Supprimer un contenu ne supprime pas immédiatement ses médias.

Remplacer une image ne modifie pas silencieusement son ancien binaire.

Une migration de stockage ne doit pas changer les URLs publiques.

Les données staging et production restent séparées.
```

---

## 4. Contexte

SiteFactory doit générer des sites et applications administrables.

Les projets peuvent contenir :

- textes de pages ;
- titres ;
- CTA ;
- FAQ ;
- réalisations ;
- témoignages validés ;
- métadonnées SEO ;
- logos ;
- images ;
- documents ;
- illustrations ;
- pièces jointes ;
- exports ;
- contenus produits ou assistés par AI.

Ces éléments n’ont pas tous le même cycle de vie.

Une structure de page ou un module peut nécessiter :

```text
génération
→ revue
→ commit
→ release
→ déploiement
```

Un texte éditorial peut nécessiter :

```text
édition dans l’administration
→ brouillon
→ validation
→ publication
```

Une image peut nécessiter :

```text
upload
→ validation
→ stockage
→ transformation
→ publication
→ archivage
→ suppression différée
```

Mélanger ces cycles de vie conduirait à :

- redéployer pour corriger un texte ;
- reconstruire une image pour modifier un média ;
- stocker des fichiers clients dans Git ;
- perdre les médias lors d’une recréation du conteneur ;
- rendre les backups incomplets ;
- exposer des chemins de fichiers ;
- rendre impossible une migration vers un object storage ;
- accumuler des fichiers orphelins ;
- écraser du contenu pendant un upgrade.

---

## 5. Problème à résoudre

Comment stocker et servir les contenus et médias d’un projet client tout en garantissant :

- l’édition sans redéploiement ;
- la persistance ;
- la sécurité des uploads ;
- la cohérence entre métadonnées et fichiers ;
- des URLs stables ;
- des backups restaurables ;
- une future migration vers un object storage ;
- la séparation entre clients et environnements ;
- la protection des fichiers privés ;
- la préservation pendant les upgrades ?

La solution doit également éviter :

- les binaires dans PostgreSQL par défaut ;
- les médias dans Git ;
- les noms de fichiers utilisés directement comme chemins ;
- l’exposition directe d’`App_Data` ;
- la suppression immédiate de fichiers référencés ;
- les transactions fictivement atomiques entre PostgreSQL et filesystem ;
- l’écriture directe des modules dans des dossiers arbitraires ;
- les URLs dépendantes d’un volume Docker.

---

## 6. Portée

Cette décision couvre :

- contenus administrables ;
- contenus initiaux ;
- pages ;
- sections ;
- métadonnées SEO ;
- médias ;
- documents ;
- images ;
- fichiers uploadés ;
- métadonnées ;
- stockage filesystem ;
- `App_Data` ;
- object storage futur ;
- URLs ;
- cache ;
- variantes ;
- suppressions ;
- rétention ;
- backups ;
- restaurations ;
- migrations de stockage ;
- sécurité des uploads ;
- quotas ;
- observabilité ;
- tests ;
- transfert client.

---

## 7. Hors portée

Cette décision ne définit pas :

- le modèle éditorial détaillé de chaque module ;
- un DAM complet ;
- un CDN obligatoire ;
- un fournisseur S3 précis ;
- une édition collaborative temps réel ;
- une transformation vidéo ;
- un moteur de publication multicanal ;
- un stockage WORM réglementaire ;
- la conservation juridique de chaque document ;
- un antivirus fournisseur obligatoire ;
- un système de signature électronique ;
- une plateforme multi-tenant ;
- un moteur complet de recherche documentaire.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Persistance fiable des contenus et médias**
2. **Sécurité des uploads**
3. **Séparation entre structure et contenu**
4. **URLs publiques stables**
5. **Sauvegarde et restauration**
6. **Indépendance du fournisseur de stockage**
7. **Isolation client et environnement**
8. **Préservation pendant les upgrades**
9. **Propriété claire des données**
10. **Cohérence PostgreSQL-stockage**
11. **Performance de livraison**
12. **Suppression contrôlée**
13. **Observabilité**
14. **Transfert client**
15. **Simplicité du MVP**
16. **Possibilité de migration vers un object storage**

---

## 9. Contraintes

La décision doit respecter :

```text
PostgreSQL comme base principale

Volume App_Data persistant dans le profil initial

Un projet par client

Un stockage distinct par environnement

Aucun média runtime dans Git

Aucun média runtime dans l’image

Aucun chemin hôte dans les contrats publics

Upload validé avant publication

URLs indépendantes du provider

Backups PostgreSQL et médias coordonnés

Domain indépendant du filesystem

Modules utilisant des contrats publics

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le volume initial de médias reste compatible avec un volume VPS ;
- les projets initiaux utilisent une seule instance web ;
- les médias publics sont majoritairement des images et documents de taille modérée ;
- PostgreSQL reste la source de vérité des métadonnées ;
- un futur object storage peut être introduit sans changer les identifiants publics ;
- les médias privés nécessitent une autorisation applicative ;
- les suppressions peuvent être différées ;
- une faible incohérence temporaire entre base et stockage peut être détectée et réparée.

---

# 11. Définitions

## 11.1 Contenu structurel

Élément définissant la composition technique du projet.

Exemples :

- page disponible ;
- type de section ;
- route structurelle ;
- module ;
- entitlement ;
- contrat ;
- thème.

---

## 11.2 Contenu éditable

Valeur pouvant être modifiée sans reconstruire l’application.

Exemples :

- titre ;
- texte ;
- CTA ;
- méta-description ;
- FAQ ;
- réalisation ;
- contenu légal validé.

---

## 11.3 Asset statique

Fichier nécessaire au build ou au fonctionnement technique de l’interface.

Exemples :

- CSS ;
- JavaScript ;
- icône du design system ;
- police autorisée ;
- image interne d’un composant.

---

## 11.4 Média runtime

Fichier ajouté, remplacé ou supprimé pendant la vie de l’application.

Exemples :

- logo client administrable ;
- photo ;
- document ;
- image de réalisation ;
- pièce jointe.

---

## 11.5 Media metadata

Données décrivant un média :

- identifiant ;
- type ;
- taille ;
- dimensions ;
- hash ;
- propriétaire logique ;
- état ;
- date ;
- stockage ;
- visibilité.

---

## 11.6 Storage key

Identifiant interne opaque utilisé par un provider de stockage.

---

## 11.7 Public media ID

Identifiant stable utilisé par les contrats applicatifs et URLs.

---

## 11.8 Variant

Fichier dérivé d’un original.

Exemples :

- miniature ;
- image redimensionnée ;
- WebP ;
- AVIF ;
- aperçu.

---

## 11.9 Orphan

Blob ou métadonnée ne possédant plus son élément correspondant.

---

# 12. Option A — Tout stocker dans Git

## 12.1 Description

Les contenus et médias sont placés dans le repository.

Toute modification nécessite un commit et un déploiement.

---

## 12.2 Avantages

- historique ;
- review ;
- backup via Git ;
- structure simple ;
- contenu associé au code.

---

## 12.3 Inconvénients

- redéploiement pour éditer ;
- repository volumineux ;
- Git peu adapté aux binaires ;
- accès administrateur impossible sans Git ;
- suppression historique difficile ;
- transfert de données personnelles dans l’historique ;
- déploiements plus lourds.

---

## 12.4 Risques

- image client publique dans un repository ;
- secrets ou documents sensibles committés ;
- clones volumineux ;
- suppression RGPD difficile ;
- historique impossible à purger proprement.

---

## 12.5 Réversibilité

```text
faible à moyenne
```

Les fichiers déjà présents restent dans l’historique.

---

# 13. Option B — Stocker contenus et binaires dans PostgreSQL

## 13.1 Description

Les textes, métadonnées et fichiers binaires sont stockés dans PostgreSQL.

---

## 13.2 Avantages

- transaction unique ;
- sauvegarde centralisée ;
- cohérence forte ;
- autorisation applicative ;
- pas de volume distinct.

---

## 13.3 Inconvénients

- base volumineuse ;
- backups plus lourds ;
- I/O binaire dans PostgreSQL ;
- restauration plus lente ;
- coût de réplication ;
- livraison de médias moins naturelle ;
- migration vers un object storage plus difficile.

---

## 13.4 Risques

- croissance rapide ;
- vacuum et backup affectés ;
- saturation du stockage PostgreSQL ;
- requêtes métier perturbées par les blobs.

---

## 13.5 Réversibilité

```text
moyenne
```

Une extraction vers un blob storage nécessiterait une migration importante.

---

# 14. Option C — Tout stocker dans le filesystem

## 14.1 Description

Les contenus et médias sont représentés par des fichiers.

---

## 14.2 Avantages

- accès direct ;
- simple pour les binaires ;
- faible surcharge ;
- facile à copier.

---

## 14.3 Inconvénients

- requêtes éditoriales difficiles ;
- transactions limitées ;
- autorisation complexe ;
- versions de contenu difficiles ;
- indexation et relations faibles ;
- risque de corruption ou d’orphelins ;
- concurrence d’édition.

---

## 14.4 Risques

- chemins devenant des identifiants ;
- fichiers écrasés ;
- contenu incohérent ;
- accès direct non contrôlé ;
- suppression accidentelle.

---

## 14.5 Réversibilité

```text
moyenne
```

---

# 15. Option D — PostgreSQL pour le contenu et stockage de blobs pour les médias

## 15.1 Description

PostgreSQL conserve :

- contenus ;
- métadonnées ;
- références ;
- états.

Un provider de blob storage conserve les binaires.

---

## 15.2 Avantages

- séparation adaptée aux types de données ;
- édition sans déploiement ;
- médias persistants ;
- URLs abstraites ;
- migration de stockage possible ;
- sauvegardes spécialisées ;
- performances adaptées ;
- stockage privé ou public contrôlable.

---

## 15.3 Inconvénients

- deux systèmes à sauvegarder ;
- cohérence à gérer ;
- suppressions différées ;
- abstraction supplémentaire ;
- opérations de réconciliation ;
- restauration coordonnée.

---

## 15.4 Risques

- fichier sans métadonnée ;
- métadonnée sans fichier ;
- backup incohérent ;
- échec entre écriture du blob et transaction DB ;
- mauvaise configuration de provider.

---

## 15.5 Réversibilité

```text
élevée
```

Les métadonnées et URLs peuvent rester stables pendant une migration de provider.

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Git | PostgreSQL complet | Filesystem complet | Modèle séparé |
|---|---:|---:|---:|---:|---:|
| Édition runtime | 5 | 1 | 5 | 3 | 5 |
| Gestion des binaires | 5 | 2 | 3 | 5 | 5 |
| Requêtes de contenu | 5 | 2 | 5 | 1 | 5 |
| Sauvegarde simple | 4 | 4 | 5 | 3 | 4 |
| Portabilité | 4 | 3 | 4 | 3 | 5 |
| URLs stables | 4 | 3 | 4 | 2 | 5 |
| Performance | 4 | 2 | 3 | 5 | 5 |
| Sécurité | 5 | 2 | 4 | 3 | 5 |
| Future object storage | 4 | 2 | 3 | 4 | 5 |
| Maintenabilité | 5 | 2 | 3 | 2 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons que :

```text
les contenus éditables et métadonnées de médias sont stockés dans PostgreSQL
```

et que :

```text
les binaires des médias sont stockés derrière une abstraction de blob storage
```

Le provider initial du profil `docker-compose-vps` utilise :

```text
App_Data/media
```

dans un volume Docker persistant propre au projet client et à l’environnement.

---

## 17.1 Provider futur

Un provider S3-compatible ou équivalent pourra remplacer le filesystem sans modifier :

- Domain ;
- les Media IDs ;
- les références de contenus ;
- les URLs publiques ;
- les contrats des modules.

---

## 17.2 Pas de stockage direct par les modules

Les modules ne doivent pas écrire directement avec :

```csharp
File.WriteAllBytes(...)
```

dans un chemin arbitraire.

Ils utilisent un contrat public de gestion des médias.

---

# 18. Matrice de propriété

| Élément | Stockage | Propriétaire |
|---|---|---|
| Configuration client | Git | SiteFactory/client |
| Code et contrats | Git | SiteFactory/client |
| Assets de design system | Git/image | SiteFactory |
| Contenus éditables | PostgreSQL | Module de contenu |
| Métadonnées SEO | PostgreSQL | Module de contenu/SEO |
| Métadonnées média | PostgreSQL | Module média |
| Binaires média | Blob storage | Module média |
| Clés Data Protection | Volume dédié | Plateforme |
| Fichiers temporaires | Temp storage | Processus concerné |
| Backups | Stockage de backup | Operations |

---

# 19. Contenus structurels

Les éléments suivants restent versionnés dans le repository :

- types de pages ;
- types de sections ;
- contrats de contenu ;
- schémas ;
- validateurs ;
- composants Razor ;
- règles de composition ;
- design tokens ;
- routes ;
- permissions ;
- capabilities.

---

## 19.1 Modification

Une modification structurelle suit :

```text
branche
→ code ou configuration
→ tests
→ release
→ déploiement
```

---

# 20. Contenus éditables

Les éléments suivants appartiennent généralement à PostgreSQL :

- titres ;
- textes ;
- CTA ;
- FAQ ;
- réalisations ;
- coordonnées publiques ;
- méta-titres ;
- méta-descriptions ;
- alt texts ;
- contenus légaux validés ;
- associations de médias.

---

## 20.1 Pas de redéploiement

Une modification éditoriale validée ne nécessite pas de rebuild de l’image.

---

## 20.2 Validation

Le contenu reste soumis :

- au schéma ;
- aux longueurs ;
- à la sanitization ;
- aux permissions ;
- au workflow de publication.

---

# 21. Modèle de contenu typé

Les contenus ne doivent pas devenir un ensemble arbitraire de clés non validées.

Chaque type de page ou section possède un contrat versionné.

Exemple conceptuel :

```text
HeroContent

ServiceListContent

ContactContent

FaqContent

PortfolioContent
```

---

## 21.1 Champs

Un contrat définit :

- champs obligatoires ;
- champs facultatifs ;
- longueurs ;
- locale ;
- médias acceptés ;
- validation ;
- sanitization.

---

## 21.2 Évolution

Une modification incompatible du schéma de contenu nécessite :

- version ;
- migration ;
- compatibilité ;
- tests.

---

# 22. Rich text

Le rich text doit utiliser un format maîtrisé.

Options possibles :

- Markdown limité ;
- modèle de blocs ;
- HTML sanitizé.

---

## 22.1 Baseline

Le MVP privilégie :

```text
texte simple
+ Markdown limité
+ composants structurés
```

plutôt qu’un HTML arbitraire.

---

## 22.2 HTML

Lorsque du HTML est autorisé :

- allowlist d’éléments ;
- allowlist d’attributs ;
- suppression des scripts ;
- suppression des handlers ;
- suppression des URLs dangereuses ;
- sanitization à l’entrée et avant rendu selon le contrat.

---

# 23. États éditoriaux

Valeurs recommandées :

```text
draft

published

archived
```

Des états futurs peuvent inclure :

```text
review-required

scheduled
```

---

## 23.1 Brouillon

Un brouillon n’est pas visible publiquement.

---

## 23.2 Publication

La publication est une action explicite et autorisée.

---

## 23.3 Archivage

L’archivage retire le contenu des parcours publics sans suppression immédiate.

---

# 24. Versionnement éditorial

Les contenus importants peuvent conserver un historique de versions.

---

## 24.1 Objectifs

- restaurer une version ;
- comparer ;
- auditer ;
- corriger une publication ;
- identifier l’auteur.

---

## 24.2 Volume

La politique de rétention des versions doit être bornée.

---

## 24.3 Données supprimées

L’historique ne doit pas empêcher une suppression réglementaire lorsqu’elle est requise.

---

# 25. Seeds de contenu initiaux

SiteFactory peut produire des contenus initiaux destinés au premier déploiement.

---

## 25.1 Artefact

Exemple conceptuel :

```text
content/bootstrap-content.json
```

---

## 25.2 Responsabilité

Le seed constitue une proposition ou baseline initiale.

Il ne devient pas automatiquement la source permanente après import.

---

## 25.3 Import explicite

Le seed est appliqué par une opération dédiée telle que :

```text
sitefactory content seed plan
sitefactory content seed apply
```

ou un outil équivalent.

---

## 25.4 Pas au démarrage

L’application ne réimporte pas automatiquement le seed à chaque démarrage.

---

## 25.5 Contenu existant

Un seed ne doit pas écraser un contenu déjà modifié.

---

# 26. Identifiants stables des contenus

Les contenus initiaux utilisent des identifiants stables.

Exemple :

```text
page.home

section.home.hero

section.home.services
```

---

## 26.1 Idempotence

Un import peut détecter qu’un élément existe déjà.

---

## 26.2 Écrasement

Une mise à jour de seed ne doit pas remplacer silencieusement la valeur éditoriale actuelle.

---

## 26.3 Upgrade

Un upgrade structurel peut proposer un nouveau champ ou contenu, mais son application éditoriale reste explicite.

---

# 27. AI et contenus

Conformément à l’ADR-0015, une sortie AI est une proposition.

---

## 27.1 Import

Elle peut être acceptée comme :

- brouillon PostgreSQL ;
- seed initial ;
- contenu client versionné.

---

## 27.2 Publication

Elle n’est jamais publiée automatiquement.

---

## 27.3 Médias

Une génération d’image AI future devra suivre une décision spécifique et le même pipeline de validation média.

---

# 28. Assets statiques de build

Les fichiers suivants peuvent rester dans le repository :

- CSS ;
- JavaScript ;
- icônes internes ;
- illustrations génériques ;
- placeholders ;
- assets du thème ;
- images nécessaires à une page d’erreur générique.

---

## 28.1 Immutabilité

Ils sont versionnés avec l’application et bénéficient du cache par version.

---

## 28.2 Données client

Un média modifiable par l’administrateur ne doit pas être transformé en asset de build uniquement pour simplifier l’implémentation.

---

# 29. Logo et identité visuelle

Un logo peut appartenir à deux catégories.

---

## 29.1 Logo figé de build

Acceptable pour un projet où l’identité visuelle ne doit pas être administrable.

Il est versionné dans le repository.

---

## 29.2 Logo administrable

Il appartient au media storage et est référencé par le contenu ou les paramètres de marque.

---

## 29.3 Baseline SiteFactory

Pour les projets administrables, les médias de marque remplaçables privilégient le media storage.

---

# 30. Contrat de stockage

Infrastructure expose un contrat minimal.

Exemple conceptuel :

```csharp
public interface IMediaBlobStorage
{
    Task<StoredBlob> WriteAsync(
        BlobWriteRequest request,
        Stream content,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(
        StorageKey storageKey,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        StorageKey storageKey,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        StorageKey storageKey,
        CancellationToken cancellationToken);
}
```

La signature définitive pourra évoluer.

---

## 30.1 Absence d’URL

Le provider ne doit pas obligatoirement retourner une URL publique persistante.

---

## 30.2 Domain

Domain ne dépend pas :

- de `FileInfo` ;
- de chemin disque ;
- de bucket S3 ;
- d’URL signée ;
- du SDK fournisseur.

---

# 31. Service applicatif média

Un service applicatif coordonne :

- upload ;
- validation ;
- stockage ;
- métadonnées ;
- références ;
- transformations ;
- suppression ;
- audit.

Exemple conceptuel :

```text
IMediaService
```

---

## 31.1 Autorisation

Les opérations sont protégées par des permissions.

---

## 31.2 Capabilities

Le module média peut exposer :

```text
media.read

media.upload

media.manage

media.delete
```

---

# 32. Identifiant public

Un média possède un identifiant stable tel que :

```text
MediaId
```

---

## 32.1 Format

Un UUID ou ULID peut être utilisé.

---

## 32.2 Opacité

L’identifiant ne doit pas révéler :

- chemin ;
- client ;
- email ;
- nom de fichier ;
- serveur ;
- bucket.

---

## 32.3 Énumération

Un identifiant non séquentiel ne remplace pas l’autorisation.

---

# 33. Storage key

La clé de stockage est générée par l’application ou le provider.

Exemple conceptuel :

```text
media/01JABC.../original
```

---

## 33.1 Nom fourni

Le nom de fichier utilisateur n’est pas utilisé comme clé.

---

## 33.2 Traversée de chemin

La clé ne peut pas contenir :

```text
..

chemin absolu

lettre de lecteur

séparateur injecté

lien symbolique
```

---

## 33.3 Normalisation

Les chemins utilisent une convention interne indépendante de la plateforme.

---

# 34. Nom de fichier d’origine

Le nom d’origine peut être conservé comme métadonnée pour l’affichage ou le téléchargement.

---

## 34.1 Non fiable

Il est considéré comme une entrée utilisateur.

---

## 34.2 Affichage

Il doit être :

- borné ;
- encodé ;
- neutralisé ;
- sans contrôle de chemin.

---

## 34.3 Logs

Le nom complet ne doit pas nécessairement être journalisé.

---

# 35. Métadonnées média

Le modèle doit pouvoir conserver :

```text
MediaId

StorageProvider

StorageKey

OriginalFileName

DeclaredContentType

DetectedContentType

SizeBytes

Sha256

Width

Height

CreatedAtUtc

CreatedBy

Status

Visibility

OriginalMediaId

VariantName
```

---

## 35.1 Données facultatives

Les dimensions ne s’appliquent pas à tous les fichiers.

---

## 35.2 Hash

Le hash sert notamment à :

- vérifier l’intégrité ;
- détecter des doublons potentiels ;
- vérifier une migration ;
- contrôler une restauration.

---

## 35.3 Secret

Un hash de média n’est pas un secret, mais peut révéler qu’un fichier identique est utilisé dans plusieurs contextes.

Il ne doit pas devenir une métrique ou donnée publique sans nécessité.

---

# 36. Visibilité des médias

Valeurs conceptuelles :

```text
public

authenticated

private
```

---

## 36.1 Public

Accessible sans authentification à travers une route contrôlée.

---

## 36.2 Authenticated

Nécessite un utilisateur authentifié.

---

## 36.3 Private

Nécessite une permission ou une relation précise avec la ressource.

---

## 36.4 Stockage

La visibilité applicative ne doit pas dépendre uniquement d’un dossier portant le nom `public`.

---

# 37. URLs publiques

Les URLs utilisent l’identifiant média et non le chemin physique.

Exemple conceptuel :

```text
/media/{mediaId}/{slug}
```

---

## 37.1 Slug

Le slug améliore la lisibilité.

Il ne constitue pas l’identifiant de sécurité.

---

## 37.2 Changement de stockage

Une migration :

```text
App_Data
→ object storage
```

ne doit pas modifier l’URL publique.

---

## 37.3 Versionnement

Un remplacement de fichier crée une nouvelle version ou un nouvel identifiant afin de maîtriser le cache.

---

# 38. Service des médias publics

La baseline peut servir les médias via l’application.

---

## 38.1 Avantages

- autorisation ;
- headers ;
- validation ;
- abstraction du storage ;
- URLs stables ;
- observabilité.

---

## 38.2 Optimisation future

Un reverse proxy ou CDN pourra servir les médias publics si :

- la clé n’expose pas le stockage ;
- les contrôles d’accès restent valides ;
- la purge est gérée ;
- les headers restent corrects.

---

# 39. Médias privés

Les médias privés passent par une autorisation serveur.

---

## 39.1 URL permanente directe

Une URL publique permanente vers un objet privé est interdite.

---

## 39.2 URL temporaire

Un object storage futur peut produire une URL signée de courte durée.

---

## 39.3 Vérification

La permission est vérifiée avant de produire l’URL signée.

---

## 39.4 Logs

L’URL signée complète ne doit pas être journalisée.

---

# 40. Headers HTTP

La livraison doit configurer selon le type :

```text
Content-Type

Content-Length

Content-Disposition

Cache-Control

ETag

Last-Modified

X-Content-Type-Options
```

---

## 40.1 `nosniff`

Les fichiers servis doivent utiliser :

```text
X-Content-Type-Options: nosniff
```

lorsque pertinent.

---

## 40.2 Téléchargement

Les documents non destinés à être rendus dans le navigateur peuvent utiliser :

```text
Content-Disposition: attachment
```

---

## 40.3 Inline

L’affichage inline est réservé aux types explicitement autorisés.

---

# 41. Cache

Les médias immuables peuvent utiliser un cache long.

---

## 41.1 Remplacement

Un remplacement crée :

- un nouvel identifiant ;
- ou une nouvelle version d’URL.

---

## 41.2 Pas d’écrasement silencieux

Réutiliser la même URL pour un binaire différent peut provoquer :

- cache obsolète ;
- incohérence CDN ;
- contenu ancien ;
- difficulté de rollback.

---

# 42. Original immuable

Après validation, le binaire original est traité comme immuable.

---

## 42.1 Modification

Une modification crée un nouveau média ou une nouvelle version.

---

## 42.2 Avantages

- intégrité ;
- cache ;
- historique ;
- rollback ;
- hash stable ;
- variantes reproductibles.

---

# 43. Variantes d’image

Les variantes peuvent inclure :

```text
thumbnail

small

medium

large

webp

avif
```

---

## 43.1 Référence

Chaque variante possède :

- son Storage Key ;
- son hash ;
- son format ;
- ses dimensions ;
- une référence vers l’original.

---

## 43.2 Génération

La génération est :

- bornée ;
- testée ;
- non exécutée à chaque requête publique ;
- relançable ;
- observable.

---

## 43.3 Échec

Un échec de variante ne doit pas supprimer l’original valide.

---

# 44. Traitement d’image

Le pipeline peut :

- décoder ;
- vérifier les dimensions ;
- retirer certaines métadonnées ;
- corriger l’orientation ;
- redimensionner ;
- encoder.

---

## 44.1 Bombe de décompression

La taille compressée ne suffit pas.

Le système doit limiter :

- dimensions ;
- pixels ;
- mémoire ;
- durée de décodage.

---

## 44.2 Métadonnées EXIF

Les métadonnées inutiles, notamment géographiques, doivent être retirées des variantes publiques.

---

# 45. Types de fichiers autorisés

Les types sont définis par une allowlist.

Exemples initiaux possibles :

```text
image/jpeg

image/png

image/webp

application/pdf
```

---

## 45.1 SVG

Le SVG est refusé par défaut pour les uploads non fiables en raison des risques :

- script ;
- référence externe ;
- CSS ;
- contenu actif ;
- XML complexe.

---

## 45.2 SVG approuvé

Un SVG peut être accepté uniquement dans un workflow de design contrôlé avec sanitization et revue.

---

## 45.3 Exécutables

Les fichiers exécutables ou interprétables sont interdits.

---

# 46. Validation du type

La validation compare :

- extension ;
- Content-Type déclaré ;
- signature binaire ;
- décodage réel lorsque possible.

---

## 46.1 Header utilisateur

Le `Content-Type` fourni par le navigateur n’est pas une preuve suffisante.

---

## 46.2 Extension

L’extension seule n’est pas suffisante.

---

## 46.3 Incohérence

Une incohérence provoque un refus.

---

# 47. Fichiers polyglottes

Un fichier pouvant être interprété comme plusieurs formats doit être traité avec prudence.

---

## 47.1 Baseline

Un fichier ambigu est refusé lorsque le pipeline ne peut pas garantir son innocuité.

---

# 48. Taille maximale

Chaque type de fichier possède une limite.

Exemple conceptuel :

```text
logo: 5 Mo

image: 15 Mo

document PDF: 25 Mo
```

Les valeurs définitives sont configurées et documentées.

---

## 48.1 Plusieurs niveaux

La limite doit être cohérente entre :

- reverse proxy ;
- ASP.NET Core ;
- module ;
- provider ;
- quota client.

---

## 48.2 Réponse

Un dépassement est refusé avant de remplir le stockage.

---

# 49. Quotas

Un projet peut définir :

```text
quota total

quota par média

quota par type

quota par période
```

---

## 49.1 Baseline

Le MVP doit au minimum permettre :

- limite par fichier ;
- surveillance du volume total ;
- alerte avant saturation.

---

## 49.2 Quota dépassé

L’upload est refusé proprement sans laisser de fichier temporaire durable.

---

# 50. Streaming des uploads

Les fichiers ne doivent pas être chargés intégralement en mémoire sans limite.

---

## 50.1 Pipeline

```text
stream
→ limite
→ hash
→ validation
→ stockage temporaire
→ promotion
```

---

## 50.2 Cancellation

L’annulation est propagée.

---

## 50.3 Timeout

L’upload possède un timeout adapté.

---

# 51. Stockage temporaire

Les uploads sont d’abord écrits dans une zone temporaire protégée.

---

## 51.1 Emplacement

L’emplacement ne doit pas être :

- publiquement servi ;
- partagé entre clients ;
- persistant sans nettoyage ;
- exécutable.

---

## 51.2 Nom

Le nom temporaire est généré aléatoirement.

---

## 51.3 Permissions

Les permissions sont restrictives.

---

## 51.4 Nettoyage

Les fichiers abandonnés sont supprimés par une tâche bornée.

---

# 52. Flux d’upload filesystem

Flux de référence :

```text
autorisation
→ vérification du quota
→ streaming vers fichier temporaire
→ calcul du hash
→ détection du type
→ validation
→ transformation éventuelle
→ génération d’une Storage Key
→ déplacement atomique vers la destination
→ transaction PostgreSQL des métadonnées
→ publication du résultat
```

---

## 52.1 Échec avant promotion

Le fichier temporaire est supprimé.

---

## 52.2 Échec PostgreSQL

Si le blob final a été créé mais que la transaction échoue :

- le blob est supprimé si possible ;
- sinon il est marqué comme orphan candidat ;
- une réconciliation ultérieure le détecte.

---

# 53. Atomicité

PostgreSQL et le blob storage ne partagent pas une transaction ACID commune.

---

## 53.1 Interdiction

Le système ne doit pas prétendre qu’un commit PostgreSQL couvre le filesystem ou S3.

---

## 53.2 Stratégie

La cohérence repose sur :

- états explicites ;
- opérations idempotentes ;
- compensations ;
- réconciliation ;
- rapports.

---

# 54. États techniques d’un média

Valeurs conceptuelles :

```text
uploading

validating

available

processing

quarantined

failed

pending-deletion

deleted

missing
```

---

## 54.1 Publicité

Seul un média :

```text
available
```

peut être servi normalement.

---

## 54.2 Quarantaine

Un média suspect n’est jamais rendu accessible publiquement.

---

# 55. Antivirus et malware scanning

Le MVP peut démarrer sans moteur antivirus embarqué pour les types très restreints et décodés.

Cependant, le contrat doit permettre un scanner futur.

---

## 55.1 Document PDF

Les documents augmentent le besoin de scan.

---

## 55.2 État

Un média peut rester :

```text
quarantined
```

jusqu’au résultat.

---

## 55.3 Scanner indisponible

La politique doit être explicite :

- refuser ;
- conserver en quarantaine ;
- permettre uniquement certains types sûrs.

La baseline est fail-closed pour les types nécessitant un scan.

---

# 56. Déduplication

Le hash peut détecter des fichiers identiques.

---

## 56.1 Baseline

La déduplication physique globale n’est pas obligatoire dans le MVP.

---

## 56.2 Isolation

Un fichier identique dans deux clients ne doit pas créer une fuite d’information ou une dépendance croisée.

---

## 56.3 Déduplication interne

Une déduplication dans le périmètre d’un client peut être étudiée ultérieurement.

---

# 57. Références depuis les contenus

Les contenus conservent un `MediaId`.

---

## 57.1 Pas de Storage Key

Une page ne doit pas contenir :

```text
/srv/sitefactory/...

media/01J.../original

bucket-name/object-key
```

---

## 57.2 Validation

L’association vérifie :

- existence ;
- état ;
- visibilité ;
- type attendu ;
- propriétaire logique.

---

# 58. Propriété logique d’un média

Un média peut être :

- partagé entre plusieurs contenus du même projet ;
- attaché à une ressource ;
- privé à un module ;
- global au site.

---

## 58.1 Module propriétaire

Le module média possède les métadonnées techniques.

Le module métier possède la relation fonctionnelle.

---

## 58.2 Suppression d’une ressource

La suppression d’une page ne supprime pas automatiquement le média si celui-ci reste référencé ailleurs.

---

# 59. Comptage des références

Le système doit pouvoir déterminer si un média est encore utilisé.

---

## 59.1 Approches

- table de références ;
- relations explicites ;
- projection maintenue ;
- requête contrôlée.

---

## 59.2 Réflexion ou recherche textuelle

Rechercher l’identifiant dans du JSON arbitraire ou du HTML ne doit pas être la seule stratégie.

---

# 60. Suppression d’un média

La suppression suit :

```text
demande autorisée
→ vérification des références
→ statut pending-deletion
→ délai de grâce
→ suppression du blob
→ mise à jour des métadonnées
→ audit
```

---

## 60.1 Média référencé

La suppression est refusée ou nécessite une opération explicite de remplacement.

---

## 60.2 Délai

Un délai permet :

- rollback ;
- traitement des caches ;
- synchronisation ;
- vérification.

---

## 60.3 Suppression physique

Elle est exécutée par un job idempotent.

---

# 61. Suppression définitive

Une suppression définitive doit tenir compte :

- des backups ;
- de la rétention ;
- des obligations ;
- des variantes ;
- des caches ;
- du CDN ;
- de l’audit.

---

## 61.1 Backup

Un média supprimé peut rester dans un backup jusqu’à expiration de celui-ci.

Cette limite doit être documentée.

---

# 62. Remplacement d’un média

Le remplacement crée un nouvel original.

---

## 62.1 Référence

La ressource fonctionnelle est mise à jour vers le nouveau `MediaId`.

---

## 62.2 Ancien média

L’ancien média suit ensuite sa politique de rétention et de suppression.

---

## 62.3 URL

Le nouvel identifiant évite les problèmes de cache.

---

# 63. Réconciliation

Un job de réconciliation doit pouvoir détecter :

```text
métadonnée sans blob

blob sans métadonnée

hash incohérent

variante manquante

média pending-deletion trop ancien

fichier temporaire abandonné
```

---

## 63.1 Baseline

La réconciliation est :

- en lecture seule par défaut ;
- observable ;
- accompagnée d’un rapport.

---

## 63.2 Correction

Une correction destructive nécessite une commande explicite ou un mode approuvé.

---

# 64. Blob sans métadonnée

Un blob sans métadonnée est considéré comme orphelin.

---

## 64.1 Suppression immédiate

Il ne doit pas être supprimé immédiatement sur la seule base d’une première observation.

---

## 64.2 Délai

Un délai protège contre :

- transaction encore en cours ;
- restauration partielle ;
- retard de réplication ;
- opération de migration.

---

# 65. Métadonnée sans blob

Le média passe dans un état :

```text
missing
```

---

## 65.1 Service public

La route retourne une réponse contrôlée.

---

## 65.2 Diagnostic

Un événement et une alerte peuvent être produits.

---

## 65.3 Réparation

Les options sont :

- restaurer le blob ;
- remplacer le média ;
- retirer la référence ;
- restaurer depuis backup.

---

# 66. Filesystem provider initial

Le provider initial stocke les fichiers sous :

```text
App_Data/media
```

---

## 66.1 Volume

Le chemin appartient à un volume Docker persistant.

---

## 66.2 Root filesystem

Il ne doit pas utiliser le filesystem éphémère de l’image.

---

## 66.3 Permissions

Seul l’utilisateur du conteneur et les opérations autorisées accèdent au volume.

---

## 66.4 Exécution

Le volume ne doit pas être monté comme source de code exécutable.

---

# 67. Structure interne du filesystem

Exemple conceptuel :

```text
App_Data/
└── media/
    ├── objects/
    │   └── 01/
    │       └── JABC.../
    │           ├── original
    │           ├── thumbnail.webp
    │           └── medium.webp
    ├── staging/
    └── quarantine/
```

---

## 67.1 Dossiers utilisateur

Le système ne crée pas de répertoire à partir d’un email, nom ou slug utilisateur.

---

## 67.2 Nombre de fichiers

Une structure partitionnée évite un nombre excessif de fichiers dans un seul dossier.

---

# 68. Symlinks

Le provider filesystem ne doit pas suivre des liens symboliques inattendus.

---

## 68.1 Validation

Les chemins résolus doivent rester sous la racine autorisée.

---

## 68.2 Création

L’application ne crée pas de symlink pour stocker un média.

---

# 69. Concurrence

Deux uploads ne doivent pas pouvoir écrire sur la même Storage Key.

---

## 69.1 Génération de clé

La collision doit être extrêmement improbable et néanmoins détectée.

---

## 69.2 Écrasement

L’écriture par défaut utilise une sémantique :

```text
create new
```

et non :

```text
overwrite existing
```

---

# 70. Object storage futur

Le provider futur doit supporter :

- clés opaques ;
- streaming ;
- metadata ;
- vérification d’existence ;
- suppression ;
- intégrité ;
- timeouts ;
- retries bornés.

---

## 70.1 Bucket

Un bucket peut être :

- dédié par client ;
- partagé avec préfixes stricts ;
- séparé par environnement.

La décision dépendra de la sécurité et du fournisseur.

---

## 70.2 Baseline de sécurité

Staging et production ne partagent pas un préfixe permettant un accès croisé.

---

# 71. URLs signées

Un provider objet peut fournir des URLs signées temporaires.

---

## 71.1 Usage

Elles peuvent servir :

- téléchargement privé ;
- upload direct futur ;
- traitement externe contrôlé.

---

## 71.2 Durée

La durée doit être courte et adaptée à l’opération.

---

## 71.3 Logs

Une URL signée complète est un secret temporaire et ne doit pas être journalisée.

---

# 72. Upload direct vers object storage

L’upload direct n’appartient pas à la baseline initiale.

---

## 72.1 Conditions futures

Il nécessitera :

- URL signée ;
- limite de taille ;
- Content-Type ;
- checksum ;
- confirmation côté serveur ;
- scan ;
- expiration ;
- état `uploading` ;
- nettoyage des uploads abandonnés.

---

# 73. Migration de provider

Une migration suit :

```text
inventaire
→ gel ou suivi des modifications
→ copie
→ vérification des hashes
→ vérification des métadonnées
→ test de lecture
→ bascule du provider
→ surveillance
→ conservation temporaire de l’ancien stockage
→ retrait contrôlé
```

---

## 73.1 URLs

Les URLs publiques restent identiques.

---

## 73.2 Écriture pendant migration

La stratégie doit définir :

- arrêt temporaire des uploads ;
- double écriture contrôlée ;
- journal des changements ;
- copie incrémentale.

Le MVP peut utiliser une fenêtre de maintenance.

---

## 73.3 Rollback

L’ancien provider reste disponible pendant une fenêtre définie.

---

# 74. Backup PostgreSQL et médias

Les backups doivent couvrir :

```text
PostgreSQL
+ media storage
+ manifest de cohérence
```

---

## 74.1 Backup incomplet

Un backup PostgreSQL sans médias ne constitue pas un backup complet du site administrable.

---

## 74.2 Backup média seul

Un backup média sans métadonnées ne permet pas une restauration fonctionnelle complète.

---

# 75. Cohérence des backups

PostgreSQL et le filesystem ne fournissent pas une transaction de snapshot commune par défaut.

---

## 75.1 Stratégies possibles

- fenêtre de maintenance ;
- suspension temporaire des écritures ;
- watermark ;
- journal des uploads ;
- snapshot du volume ;
- stockage objet versionné.

---

## 75.2 Baseline MVP

Pour une sauvegarde complète cohérente :

```text
passage temporaire en mode de maintenance ou suspension des écritures média
→ backup PostgreSQL
→ backup App_Data
→ manifest
→ reprise
```

La procédure exacte est définie dans le runbook.

---

# 76. Manifest de backup média

Le backup doit pouvoir conserver :

```text
BackupId

ClientSlug

Environment

DatabaseBackupId

MediaSnapshotId

CreatedAtUtc

MediaCount

TotalBytes

ManifestChecksum

ApplicationVersion
```

---

## 76.1 Données interdites

Le manifest ne contient pas :

- secrets ;
- URLs signées ;
- credentials ;
- contenu des fichiers.

---

# 77. Vérification des backups

La vérification comprend :

- archive lisible ;
- nombre de fichiers ;
- checksums ;
- correspondance avec les métadonnées ;
- absence de fichiers temporaires inutiles ;
- capacité de restauration.

---

# 78. Restauration

Une restauration complète suit :

```text
nouvel environnement isolé
→ restauration PostgreSQL
→ restauration média
→ restauration de la configuration
→ réconciliation
→ vérification des hashes
→ health checks
→ smoke tests
```

---

## 78.1 Ordre

L’ordre peut dépendre de la procédure, mais le site ne doit pas être ouvert publiquement avant cohérence suffisante.

---

## 78.2 Médias manquants

La restauration échoue ou produit un état dégradé explicitement signalé.

---

## 78.3 Mauvais client

Un backup média ne doit pas être restauré dans le stockage d’un autre client sans procédure de migration explicite.

---

# 79. Restore drill

Les restore drills doivent vérifier :

- pages ;
- contenus ;
- images ;
- documents ;
- références ;
- URLs ;
- variantes ;
- permissions ;
- médias privés ;
- suppression différée.

---

# 80. Isolation client

Chaque client possède :

- sa base ;
- son namespace de stockage ;
- ses credentials ;
- ses backups ;
- ses quotas ;
- ses rapports.

---

## 80.1 VPS mutualisé

Même sur un VPS partagé, les volumes restent distincts.

---

## 80.2 Object storage partagé

Un bucket partagé futur nécessite :

- préfixes non ambigus ;
- policies ;
- credentials limités ;
- tests d’accès croisé.

---

# 81. Isolation des environnements

Staging et production ne partagent pas :

- volume ;
- bucket ;
- préfixe ;
- credentials ;
- base ;
- URLs publiques ;
- cache.

---

## 81.1 Copie de production

Une copie vers staging exige :

- minimisation ;
- anonymisation si nécessaire ;
- suppression des médias sensibles ;
- autorisation ;
- rapport.

---

# 82. Content delivery network

Un CDN n’est pas obligatoire dans le MVP.

---

## 82.1 Introduction future

Un CDN peut être ajouté pour :

- performance ;
- cache géographique ;
- réduction de charge ;
- protection.

---

## 82.2 Source de vérité

Le CDN n’est pas la source de vérité.

---

## 82.3 Purge

La stratégie doit gérer :

- URL immuable ;
- invalidation ;
- suppression ;
- média privé.

---

# 83. SEO des médias

Les contenus peuvent conserver :

- alt text ;
- title ;
- légende ;
- contexte ;
- nom SEO éventuel.

---

## 83.1 Alt text

L’alt text appartient au contexte éditorial et non nécessairement au média global.

Une même image peut avoir un alt text différent selon son usage.

---

## 83.2 Nom de fichier

Le nom d’origine ne doit pas être la seule information SEO.

---

# 84. Accessibilité

Les contrats de contenu doivent permettre :

- alt text ;
- légende ;
- indication décorative ;
- description longue lorsque nécessaire.

---

## 84.1 Image décorative

Une image décorative peut utiliser un alt vide selon les règles d’accessibilité.

---

## 84.2 Validation

L’administration peut avertir lorsqu’un média informatif ne possède pas d’alternative.

---

# 85. Documents privés

Un document privé nécessite :

- autorisation ;
- route contrôlée ;
- Content-Disposition ;
- audit éventuel ;
- durée de cache limitée.

---

## 85.1 Indexation

Les documents privés ne doivent pas être indexés par les moteurs de recherche.

---

## 85.2 URL devinable

Une URL non devinable ne remplace pas l’autorisation.

---

# 86. Uploads administrateur et publics

Les uploads depuis l’administration et les formulaires publics ne possèdent pas nécessairement la même politique.

---

## 86.1 Administration

L’administrateur peut déposer les types explicitement autorisés par le module média.

---

## 86.2 Formulaire public

La baseline SiteFactory ne permet pas les pièces jointes publiques sauf module et besoin validés.

---

## 86.3 Risque accru

Un upload public nécessite :

- rate limiting ;
- CAPTCHA ou protection adaptée ;
- scan ;
- quota ;
- taille stricte ;
- rétention ;
- aucune publication directe.

---

# 87. Fichiers temporaires métiers

Les exports ou documents générés temporairement ne doivent pas être confondus avec des médias permanents.

---

## 87.1 Durée de vie

Ils possèdent :

- TTL ;
- propriétaire ;
- job de nettoyage ;
- quota.

---

## 87.2 Promotion

Un fichier temporaire devient permanent uniquement par une opération explicite.

---

# 88. Exports

Un export contenant des données personnelles doit :

- être privé ;
- expirer ;
- être autorisé ;
- être audité ;
- ne pas être mis en cache publiquement.

---

## 88.1 Stockage

Il peut utiliser une zone dédiée :

```text
App_Data/exports
```

avec une politique distincte.

---

# 89. Logs

Les logs ne doivent pas contenir :

- contenu des fichiers ;
- noms sensibles ;
- URL signée ;
- path hôte complet ;
- données personnelles issues du contenu ;
- documents.

---

## 89.1 Propriétés autorisées

Exemples :

```text
MediaId

StorageProvider

ContentType

SizeBucket

Status

Operation

ErrorCode

Duration
```

---

# 90. Observabilité

Événements recommandés :

```text
MediaUploadStarted

MediaUploadValidated

MediaUploadRejected

MediaStored

MediaVariantCreated

MediaProcessingFailed

MediaDownloadAuthorized

MediaDeletionScheduled

MediaDeleted

MediaReconciliationStarted

MediaReconciliationCompleted

MediaBlobMissing

MediaOrphanDetected

MediaStorageMigrationStarted

MediaStorageMigrationCompleted
```

---

## 90.1 Métriques

Exemples :

```text
media uploads

media upload failures

media storage bytes

media processing duration

media missing blobs

media orphan count

media download duration
```

---

## 90.2 Cardinalité

Les métriques ne doivent pas utiliser :

- MediaId ;
- nom de fichier ;
- Storage Key ;
- TraceId ;
- UserId.

---

# 91. Alertes

Alertes possibles :

```text
Stockage presque plein

Taux d’upload rejeté anormal

Blob manquant

Nombre d’orphelins élevé

Job de suppression en échec

Backup média absent

Écart de réconciliation

Scanner indisponible

Volume App_Data non monté
```

---

# 92. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-CONTENT-001` | Type de contenu inconnu |
| `SF-CONTENT-002` | Schéma de contenu invalide |
| `SF-CONTENT-003` | Contenu obligatoire absent |
| `SF-CONTENT-004` | Contenu non sanitizé |
| `SF-CONTENT-005` | Transition éditoriale invalide |
| `SF-CONTENT-006` | Seed déjà modifié |
| `SF-CONTENT-007` | Import de contenu en conflit |
| `SF-CONTENT-008` | Version de contenu incompatible |
| `SF-CONTENT-009` | Référence média invalide |
| `SF-CONTENT-010` | Publication non autorisée |
| `SF-MEDIA-001` | Média inconnu |
| `SF-MEDIA-002` | Provider de stockage inconnu |
| `SF-MEDIA-003` | Storage Key invalide |
| `SF-MEDIA-004` | Fichier temporaire impossible à créer |
| `SF-MEDIA-005` | Fichier vide |
| `SF-MEDIA-006` | Taille maximale dépassée |
| `SF-MEDIA-007` | Quota dépassé |
| `SF-MEDIA-008` | Type déclaré interdit |
| `SF-MEDIA-009` | Type détecté interdit |
| `SF-MEDIA-010` | Extension et contenu incompatibles |
| `SF-MEDIA-011` | Signature de fichier invalide |
| `SF-MEDIA-012` | Dimensions d’image excessives |
| `SF-MEDIA-013` | Décompression dangereuse détectée |
| `SF-MEDIA-014` | Malware détecté |
| `SF-MEDIA-015` | Scanner indisponible |
| `SF-MEDIA-016` | Média en quarantaine |
| `SF-MEDIA-017` | Écriture du blob échouée |
| `SF-MEDIA-018` | Transaction des métadonnées échouée |
| `SF-MEDIA-019` | Blob orphelin détecté |
| `SF-MEDIA-020` | Blob manquant |
| `SF-MEDIA-021` | Hash incohérent |
| `SF-MEDIA-022` | Variante impossible à produire |
| `SF-MEDIA-023` | Média encore référencé |
| `SF-MEDIA-024` | Suppression non autorisée |
| `SF-MEDIA-025` | Suppression physique échouée |
| `SF-MEDIA-026` | Média privé non autorisé |
| `SF-MEDIA-027` | URL signée invalide |
| `SF-MEDIA-028` | Traversée de chemin détectée |
| `SF-MEDIA-029` | Lien symbolique interdit |
| `SF-MEDIA-030` | Stockage temporaire saturé |
| `SF-MEDIA-031` | App_Data non persistant |
| `SF-MEDIA-032` | Namespace du mauvais client |
| `SF-MEDIA-033` | Namespace du mauvais environnement |
| `SF-MEDIA-034` | Backup média incomplet |
| `SF-MEDIA-035` | Restauration média incohérente |
| `SF-MEDIA-036` | Migration de stockage incomplète |
| `SF-MEDIA-037` | Provider source indisponible |
| `SF-MEDIA-038` | Provider cible indisponible |
| `SF-MEDIA-039` | URL dépendante du provider |
| `SF-MEDIA-040` | Rapport média absent |

---

# 93. Tests unitaires

Tester notamment :

- Media ID ;
- Storage Key ;
- normalisation ;
- validation de type ;
- limite de taille ;
- hash ;
- états ;
- références ;
- transitions de suppression ;
- permissions ;
- URLs ;
- Content-Disposition.

---

# 94. Tests des contenus

Couvrir :

- brouillon ;
- publication ;
- archivage ;
- schéma invalide ;
- contenu trop long ;
- Markdown dangereux ;
- HTML interdit ;
- référence média absente ;
- seed idempotent ;
- seed modifié.

---

# 95. Tests d’upload

Tester :

```text
JPEG valide

PNG valide

WebP valide

PDF valide si autorisé

fichier vide

fichier trop grand

extension falsifiée

Content-Type falsifié

signature invalide

SVG interdit

exécutable renommé

fichier polyglotte

image aux dimensions excessives

annulation
```

---

# 96. Tests de path traversal

Tester des noms tels que :

```text
../../secret

..\..\secret

/etc/passwd

C:\Windows\system.ini

%2e%2e

nom avec séparateurs
```

Aucun ne doit influencer la Storage Key.

---

# 97. Tests de symlink

Le provider filesystem doit refuser ou neutraliser :

- racine média symlinkée de manière inattendue ;
- objet final symlink ;
- dossier intermédiaire sortant de la racine.

---

# 98. Tests de concurrence

Tester :

- deux uploads simultanés ;
- même nom d’origine ;
- même hash ;
- même ressource ;
- suppression pendant lecture ;
- remplacement pendant publication.

---

# 99. Tests d’atomicité

Simuler un échec :

- pendant streaming ;
- pendant validation ;
- après déplacement final ;
- pendant transaction PostgreSQL ;
- pendant génération de variante ;
- pendant suppression.

Vérifier :

- nettoyage ;
- état ;
- absence d’exposition ;
- réconciliation possible.

---

# 100. Tests de réconciliation

Créer volontairement :

- blob sans métadonnée ;
- métadonnée sans blob ;
- variante absente ;
- hash différent ;
- fichier temporaire ancien ;
- suppression bloquée.

Le rapport doit identifier chaque cas.

---

# 101. Tests d’autorisation

Tester :

```text
média public anonyme

média authenticated sans session

média private sans permission

média private avec permission

administrateur

kill switch actif

capability absente
```

---

# 102. Tests HTTP

Vérifier :

- Content-Type ;
- `nosniff` ;
- ETag ;
- cache ;
- range requests si supportées ;
- Content-Disposition ;
- réponse 404 contrôlée ;
- réponse 403 contrôlée ;
- absence de path physique.

---

# 103. Tests de variantes

Vérifier :

- dimensions ;
- ratio ;
- orientation ;
- format ;
- métadonnées retirées ;
- référence à l’original ;
- cache ;
- régénération ;
- échec isolé.

---

# 104. Tests de backup

Vérifier :

- PostgreSQL sauvegardé ;
- médias sauvegardés ;
- manifest ;
- counts ;
- checksums ;
- App_Data absent détecté ;
- backup incomplet refusé.

---

# 105. Tests de restauration

Restaurer dans un environnement isolé.

Vérifier :

- contenus ;
- références ;
- originaux ;
- variantes ;
- URLs ;
- fichiers privés ;
- permissions ;
- hashes ;
- réconciliation sans anomalie critique.

---

# 106. Tests de migration de provider

Flux de test :

```text
filesystem source
→ copie vers provider cible
→ vérification des hashes
→ bascule
→ lecture des mêmes Media IDs
→ URLs identiques
→ rollback vers source
```

---

# 107. Tests d’isolation

Déployer deux clients ou environnements.

Vérifier :

- volumes distincts ;
- namespaces distincts ;
- bases distinctes ;
- URLs distinctes ;
- absence de lecture croisée ;
- backups distincts ;
- quotas distincts.

---

# 108. Tests de sécurité

Vérifier notamment :

```text
Médias non présents dans Git

Médias non présents dans l’image

App_Data non servi directement

Nom utilisateur non utilisé comme chemin

SVG refusé par défaut

HTML actif refusé

Types validés par contenu

Uploads publics limités

Documents privés autorisés côté serveur

URLs signées non logguées

Filesystem racine non utilisé

Storage Key non exposée
```

---

# 109. Tests multiplateformes

Les contrats et providers sont testés sur :

```text
Windows x64

Linux x64
```

---

## 109.1 Production

Les validations de permissions, symlinks et volumes Docker sont exécutées sur Linux.

---

# 110. Quality gates

Une fonctionnalité média est bloquée lorsque :

```text
Le média runtime est committé dans Git.

Le média runtime est inclus dans l’image.

Le code utilise un chemin utilisateur.

Le module écrit directement dans App_Data.

Le type n’est pas vérifié.

La taille n’est pas bornée.

Un SVG non fiable est accepté.

Le fichier devient public avant validation.

Une URL expose la Storage Key.

Une route privée ne vérifie pas l’autorisation.

Une suppression ignore les références.

Un backup ne couvre pas les médias.

Une restauration n’est pas testable.

Un provider dépend du code Domain.

Les tests de traversée de chemin échouent.
```

---

# 111. Quality gates de production

La production est bloquée si :

```text
App_Data n’est pas persistant.

Le volume du mauvais client est monté.

Le volume staging est utilisé en production.

Le stockage est presque saturé sans plan.

Le backup média requis est absent.

Le job de réconciliation signale des erreurs critiques.

Un nouveau provider n’a pas été testé.

Les credentials du provider sont manquants.

La migration de stockage est incomplète.
```

---

# 112. CI/CD

La CI peut vérifier :

- absence de médias runtime suivis ;
- taille des assets build ;
- schémas de contenus ;
- migrations ;
- tests du provider ;
- tests de sécurité ;
- construction de l’image ;
- volume attendu ;
- fichiers `.dockerignore`.

---

## 112.1 Données réelles

La CI ne nécessite pas de média client réel.

---

## 112.2 Fixtures

Les tests utilisent de petits fichiers artificiels et contrôlés.

---

# 113. `.gitignore`

Le repository doit ignorer les zones runtime telles que :

```text
App_Data/

media/

uploads/

exports/

quarantine/

temp/
```

lorsqu’elles peuvent exister localement.

---

## 113.1 Assets build

Les assets statiques requis restent suivis dans leur dossier dédié.

---

## 113.2 Ambiguïté

Le projet ne doit pas utiliser le même dossier pour :

- assets de build ;
- uploads runtime.

---

# 114. `.dockerignore`

L’image doit exclure :

```text
App_Data

uploads

secrets

backups

exports

fichiers temporaires

médias runtime
```

---

# 115. Docker Compose

Le service web monte un volume dédié.

Exemple conceptuel :

```yaml
services:
  web:
    volumes:
      - app_data:/app/App_Data

volumes:
  app_data:
```

---

## 115.1 Lecture-écriture

Seul le service ayant besoin du stockage reçoit le montage en écriture.

---

## 115.2 Caddy

Caddy ne monte pas directement `App_Data` dans la baseline.

---

## 115.3 PostgreSQL

PostgreSQL ne monte pas le volume des médias.

---

# 116. Durcissement filesystem

Le conteneur peut utiliser :

```text
root filesystem en lecture seule
+ App_Data en lecture-écriture
+ tmpfs borné
```

---

## 116.1 Objectif

Limiter les écritures aux zones prévues.

---

# 117. Secrets du provider

Les credentials d’un object storage futur suivent l’ADR-0012.

---

## 117.1 Scope

Ils sont limités :

- au client ;
- à l’environnement ;
- au bucket ou préfixe ;
- aux opérations nécessaires.

---

## 117.2 Runtime

Le provider filesystem initial ne nécessite pas de secret réseau.

---

# 118. Règles pour les modules

Un module souhaitant utiliser un média doit :

- dépendre du contrat public du module média ;
- utiliser `MediaId` ;
- déclarer le type attendu ;
- respecter la visibilité ;
- ne pas accéder au blob storage directement.

---

## 118.1 Nouveau type média

Un module peut déclarer un profil :

```text
portfolio-image

company-logo

downloadable-document
```

avec :

- types ;
- taille ;
- dimensions ;
- visibilité ;
- transformations.

---

# 119. Règles pour les customizations

Une customization client utilise les mêmes contrats publics.

---

## 119.1 Interdictions

Elle ne doit pas :

- écrire dans un dossier arbitraire ;
- exposer `App_Data` ;
- créer une URL physique ;
- monter un stockage supplémentaire sans décision ;
- contourner le scanner ;
- servir directement un fichier privé.

---

# 120. Profils de médias

Chaque usage peut posséder un profil versionné.

Exemple conceptuel :

```yaml
mediaProfile:
  id: portfolio-image
  acceptedTypes:
    - image/jpeg
    - image/png
    - image/webp
  maximumBytes: 15728640
  maximumPixels: 24000000
  visibility: public
  variants:
    - thumbnail
    - medium
```

---

## 120.1 Source

Les profils structurels sont versionnés avec le module ou la plateforme.

---

## 120.2 Override client

Un override client doit rester dans les limites de sécurité autorisées.

---

# 121. Sécurité

## 121.1 Actifs

- contenus ;
- médias ;
- documents ;
- données personnelles ;
- chemins ;
- stockage ;
- backups ;
- credentials ;
- URLs privées.

---

## 121.2 Menaces

- path traversal ;
- malware ;
- XSS par SVG ou HTML ;
- fichier polyglotte ;
- bombe de décompression ;
- saturation disque ;
- upload public abusif ;
- exposition privée ;
- lien symbolique ;
- mauvais volume ;
- suppression accidentelle ;
- backup incomplet.

---

## 121.3 Contrôles

- allowlist ;
- streaming ;
- limites ;
- détection de type ;
- stockage opaque ;
- autorisation ;
- quarantaine ;
- hashes ;
- volumes persistants ;
- réconciliation ;
- sauvegardes ;
- tests ;
- isolation.

---

# 122. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| `../../` dans le nom | Storage Key générée |
| Fichier `.exe` renommé | Détection par contenu |
| SVG avec script | SVG refusé par défaut |
| Image décompressée gigantesque | Limite de pixels |
| Volume rempli | Quotas et alertes |
| Média privé deviné | Autorisation serveur |
| Fichier perdu au redéploiement | Volume persistant |
| Blob sans métadonnée | Réconciliation |
| Métadonnée sans blob | État `missing` et alerte |
| Migration storage cassant les liens | URL fondée sur MediaId |
| Backup DB sans fichiers | Manifest de backup complet |
| Accès croisé client | Namespace et credentials isolés |

---

# 123. Règles pour LogicGate

LogicGate doit :

- définir les profils de médias ;
- limiter les types ;
- surveiller le stockage ;
- maintenir les backups ;
- tester les restaurations ;
- surveiller les orphelins ;
- valider les migrations de provider ;
- préparer les transferts ;
- documenter les médias privés.

---

## 123.1 LogicGate ne doit pas

- placer les uploads dans Git ;
- exposer directement un volume ;
- accepter tous les types de fichiers ;
- utiliser un nom utilisateur comme chemin ;
- supprimer physiquement sans délai ;
- considérer le backup PostgreSQL comme suffisant ;
- migrer de provider sans vérification de hash.

---

# 124. Règles pour Codex

Codex peut :

- implémenter les contrats ;
- ajouter le provider filesystem ;
- créer les validateurs ;
- créer les tests ;
- ajouter les profils ;
- ajouter la réconciliation ;
- documenter les runbooks.

---

## 124.1 Codex ne doit pas

```text
Stocker les médias dans Git

Stocker les blobs dans PostgreSQL sans décision

Servir App_Data comme wwwroot

Utiliser OriginalFileName comme chemin

Autoriser un type par extension seule

Accepter SVG par défaut

Ajouter un upload public sans protection

Supprimer un média référencé

Modifier un volume de production

Migrer un stockage réel

Supprimer des blobs réels

Inventer une politique de rétention
```

---

## 124.2 Décision humaine

Une décision humaine est requise pour :

- nouveau type actif ;
- nouveau provider ;
- upload public ;
- nouveau quota de production ;
- suppression massive ;
- migration de stockage ;
- restauration ;
- modification de rétention ;
- stockage d’une donnée réglementée.

---

# 125. Anti-patterns interdits

## 125.1 Upload dans `wwwroot`

Le fichier devient directement public sans validation applicative.

---

## 125.2 Nom utilisateur comme chemin

```csharp
Path.Combine(root, uploadedFile.FileName)
```

---

## 125.3 URL physique en base

```text
/srv/sitefactory/clients/alpha/production/App_Data/...
```

---

## 125.4 Base64 dans le contenu

Un gros binaire est stocké directement dans un champ JSON ou HTML.

---

## 125.5 Blob dans Git

Une image administrable est ajoutée au repository à chaque modification.

---

## 125.6 Suppression immédiate

Un média est supprimé dès qu’une page est archivée.

---

## 125.7 Confiance dans le Content-Type

Le header navigateur est utilisé sans inspection.

---

## 125.8 Type universel

L’administration accepte :

```text
*/*
```

---

## 125.9 App_Data non sauvegardé

Seule la base PostgreSQL est incluse dans le plan de reprise.

---

## 125.10 URL de bucket publique

Les pages stockent directement une URL fournisseur permanente.

---

## 125.11 Écrasement en place

Une nouvelle image remplace les octets derrière la même clé et URL.

---

## 125.12 Scan après publication

Le document devient accessible avant la fin de sa validation.

---

# 126. Conséquences positives

La décision apporte :

- édition sans redéploiement ;
- stockage adapté aux binaires ;
- PostgreSQL concentré sur les métadonnées ;
- médias persistants ;
- URLs stables ;
- provider remplaçable ;
- sécurité des uploads ;
- suppressions contrôlées ;
- backups complets ;
- restauration testable ;
- isolation client ;
- préservation pendant les upgrades ;
- future compatibilité CDN et S3.

---

# 127. Conséquences négatives

La décision entraîne :

- deux stockages à gérer ;
- abstraction supplémentaire ;
- réconciliation ;
- suppressions différées ;
- backups coordonnés ;
- transformations ;
- quotas ;
- jobs ;
- davantage de tests ;
- risques d’orphelins ;
- procédures de migration de provider.

---

# 128. Risques acceptés

## 128.1 Incohérence temporaire

Un échec peut laisser un blob ou une métadonnée orpheline.

Réduction :

- compensation ;
- état ;
- réconciliation ;
- alertes.

---

## 128.2 App_Data sur VPS

Le stockage initial dépend du disque du VPS.

Réduction :

- monitoring ;
- backups hors VPS ;
- quotas ;
- migration future.

---

## 128.3 Instance unique

Le filesystem local limite une future mise à l’échelle horizontale.

Ce risque est accepté pour le profil initial.

---

## 128.4 Scanner facultatif initial

Certains types très limités peuvent être acceptés sans antivirus complet.

La politique doit rester restrictive et évoluer avant l’acceptation de documents plus risqués.

---

# 129. Mesures compensatoires

- abstraction de storage ;
- types allowlistés ;
- streaming ;
- quotas ;
- hashes ;
- originaux immuables ;
- variantes ;
- réconciliation ;
- suppression différée ;
- volumes persistants ;
- backups ;
- restore drills ;
- URLs par MediaId ;
- autorisation ;
- observabilité ;
- tests de sécurité.

---

# 130. Performance

## 130.1 Livraison

Les médias ne doivent pas être chargés intégralement en mémoire.

---

## 130.2 Streaming

La lecture et l’écriture utilisent des streams.

---

## 130.3 Cache

Les médias immuables permettent un cache long.

---

## 130.4 Variantes

Les variantes adaptées évitent d’envoyer une image originale trop lourde.

---

## 130.5 Hash

Le calcul du hash ajoute un coût linéaire accepté pour l’intégrité.

---

# 131. Coûts

## 131.1 Coûts supplémentaires

- stockage ;
- backups ;
- bande passante ;
- transformations ;
- scanner ;
- monitoring ;
- object storage futur ;
- CDN éventuel.

---

## 131.2 Coûts évités

- repository Git volumineux ;
- base PostgreSQL saturée par les blobs ;
- redéploiements éditoriaux ;
- médias perdus ;
- URLs cassées ;
- incidents d’upload ;
- migration de stockage impossible.

---

# 132. Maintenabilité

Pour chaque fichier, il doit être possible de répondre :

```text
Est-ce un asset de build ou un média runtime ?

Qui le possède ?

Quel est son MediaId ?

Quel provider le conserve ?

Quel est son hash ?

Est-il public ou privé ?

Où est-il référencé ?

Quand peut-il être supprimé ?

Est-il sauvegardé ?

Peut-il être restauré ?

Son URL survivra-t-elle à une migration ?
```

---

# 133. Compatibilité

## 133.1 Contrats

Les contrats de médias possèdent une version.

---

## 133.2 Providers

Un provider doit respecter les mêmes invariants fonctionnels.

---

## 133.3 URLs

Les URLs publiques ne dépendent pas de la version du provider.

---

## 133.4 Schéma PostgreSQL

Une modification des métadonnées suit les migrations explicites.

---

## 133.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration d’un projet stockant actuellement ses médias dans Git ou PostgreSQL nécessitera un plan dédié.

---

# 134. Migration depuis Git

Le processus est :

```text
inventorier les médias suivis
→ identifier leurs usages
→ créer les métadonnées
→ importer les blobs
→ remplacer les chemins par MediaId
→ vérifier les URLs
→ commit
→ conserver ou nettoyer l’historique selon le besoin
```

---

## 134.1 Historique

Retirer un fichier du dernier commit ne le retire pas de l’historique Git.

---

## 134.2 Donnée sensible

Un média sensible committé peut nécessiter une réécriture et une analyse d’exposition.

---

# 135. Migration depuis PostgreSQL blobs

Le processus est :

```text
inventorier les blobs
→ produire les Storage Keys
→ copier vers le provider
→ vérifier les hashes
→ mettre à jour les métadonnées
→ basculer les lectures
→ vérifier
→ supprimer les colonnes binaires dans une migration ultérieure
```

---

## 135.1 Double lecture

Une période de compatibilité peut lire :

- nouveau stockage ;
- ancien blob ;

pendant la migration.

Elle doit rester temporaire et testée.

---

# 136. Migration depuis un dossier public

Le processus est :

```text
mettre fin aux nouveaux uploads directs
→ inventorier
→ valider les types
→ calculer les hashes
→ créer les métadonnées
→ déplacer vers le stockage contrôlé
→ créer les routes MediaId
→ rediriger les anciennes URLs si nécessaire
→ retirer l’accès direct
```

---

# 137. Transfert client

Le handover doit inclure :

- schéma de contenu ;
- dump PostgreSQL ;
- backup média ;
- manifest ;
- provider ;
- structure logique ;
- quotas ;
- profils de médias ;
- runbooks ;
- credentials à recréer ;
- procédure de restauration ;
- politique de rétention.

---

## 137.1 Accès LogicGate

Les credentials de stockage appartenant à LogicGate ne sont pas transférés s’ils couvrent d’autres clients.

---

## 137.2 Provider client

La préférence est de migrer vers un provider ou credential appartenant au client lors d’un transfert complet.

---

# 138. Clôture de service

La clôture suit :

```text
gel des modifications
→ backup final PostgreSQL
→ backup final médias
→ manifest
→ transfert
→ vérification
→ révocation des credentials
→ délai de conservation
→ suppression contrôlée
```

---

## 138.1 Suppression

La suppression doit inclure :

- originaux ;
- variantes ;
- fichiers temporaires ;
- caches ;
- backups selon la politique ;
- métadonnées.

---

# 139. Production

```text
Production execution: out of scope
```

Cet ADR définit l’architecture et ne constitue pas une autorisation :

- d’importer des médias ;
- de supprimer des fichiers ;
- de migrer un volume ;
- de restaurer un backup ;
- de modifier un provider réel.

---

# 140. Réversibilité

## 140.1 Classification

```text
élevée entre providers de blobs

moyenne depuis PostgreSQL ou Git

variable après suppression physique
```

---

## 140.2 Provider

Le contrat et les Media IDs permettent de changer de provider.

---

## 140.3 Suppression

Une suppression physique après expiration des backups peut être irréversible.

---

# 141. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `MediaId` ;
- `StorageKey` ;
- `MediaStatus` ;
- `MediaVisibility` ;
- métadonnées ;
- erreurs.

---

## Étape 2 — Modèle PostgreSQL

Créer :

- médias ;
- variantes ;
- références ;
- états ;
- migrations.

---

## Étape 3 — Provider filesystem

Implémenter le stockage sous `App_Data/media`.

---

## Étape 4 — Upload pipeline

Ajouter :

- streaming ;
- limites ;
- hash ;
- type detection ;
- stockage temporaire ;
- promotion.

---

## Étape 5 — Service média

Ajouter :

- autorisation ;
- métadonnées ;
- références ;
- URLs ;
- suppression.

---

## Étape 6 — Livraison HTTP

Créer :

- route MediaId ;
- headers ;
- cache ;
- médias privés.

---

## Étape 7 — Images

Ajouter :

- dimensions ;
- orientation ;
- variantes ;
- suppression de métadonnées.

---

## Étape 8 — Réconciliation

Créer :

- scan en lecture seule ;
- rapport ;
- alertes ;
- correction explicite.

---

## Étape 9 — Backup et restore

Étendre les runbooks aux médias et manifests.

---

## Étape 10 — Provider abstraction

Ajouter les tests contractuels préparant un futur object storage.

---

# 142. Tâches liées proposées

```text
SF-TASK-174 — Define content and media storage contracts

SF-TASK-175 — Add PostgreSQL media metadata persistence

SF-TASK-176 — Implement the App_Data filesystem media provider

SF-TASK-177 — Add secure streaming media uploads

SF-TASK-178 — Add media type, size and path validation

SF-TASK-179 — Add immutable media identifiers and URLs

SF-TASK-180 — Add image metadata and variant generation

SF-TASK-181 — Add authorized private media delivery

SF-TASK-182 — Add media reference tracking

SF-TASK-183 — Add deferred media deletion

SF-TASK-184 — Add media storage reconciliation

SF-TASK-185 — Add coordinated database and media backups

SF-TASK-186 — Add media restore verification

SF-TASK-187 — Add storage provider contract tests

SF-TASK-188 — Add object-storage migration planning
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 143. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Les contenus éditables sont stockés dans PostgreSQL.

Les médias runtime ne sont pas dans Git.

Les médias runtime ne sont pas dans l’image.

Les binaires ne sont pas stockés dans PostgreSQL par défaut.

App_Data utilise un volume persistant.

Un média possède un MediaId stable.

Une page référence un MediaId et non un chemin.

Le nom utilisateur n’est pas utilisé comme Storage Key.

Les types sont contrôlés par leur contenu.

Les tailles et dimensions sont bornées.

Le SVG est refusé par défaut.

Un upload invalide n’est jamais publié.

Les originaux sont immuables.

Un remplacement crée une nouvelle version.

Les médias privés sont autorisés côté serveur.

Les URLs ne changent pas lors d’un changement de provider.

Une suppression vérifie les références.

La suppression physique est différée.

Les blobs et métadonnées orphelins sont détectés.

Le backup couvre PostgreSQL et les médias.

Une restauration complète est testée.

Deux clients ne partagent aucun volume média.

Staging et production restent séparés.

Les tests fonctionnent sur Windows et Linux.
```

---

# 144. Conditions de révision

Réexaminer cette décision si :

- plusieurs instances web deviennent la norme ;
- le volume dépasse les capacités du VPS ;
- un CDN devient obligatoire ;
- les uploads publics deviennent fréquents ;
- des vidéos doivent être gérées ;
- une conservation réglementaire forte est requise ;
- un object storage devient la baseline ;
- les médias doivent être traités dans plusieurs régions ;
- une collaboration éditoriale temps réel est introduite ;
- PostgreSQL doit stocker certains petits blobs pour une raison démontrée ;
- une plateforme DAM externe devient nécessaire ;
- des fichiers doivent être chiffrés individuellement par client.

---

# 145. Indicateurs à suivre

- nombre de médias ;
- volume total ;
- volume par client ;
- uploads réussis ;
- uploads rejetés ;
- répartition par type ;
- temps d’upload ;
- temps de transformation ;
- médias manquants ;
- blobs orphelins ;
- variantes manquantes ;
- stockage temporaire ;
- suppressions en attente ;
- durée de backup ;
- durée de restauration ;
- taux de cache ;
- croissance mensuelle ;
- quota restant.

---

# 146. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des uploads apparaissent dans Git

App_Data n’est pas persistant

des chemins physiques apparaissent dans les contenus

des SVG non fiables sont servis

les fichiers sont validés uniquement par extension

le stockage sature régulièrement

les médias privés utilisent des URLs publiques permanentes

les orphelins augmentent

les backups ne couvrent pas les médias

les restaurations produisent des médias manquants

les modules écrivent directement sur le disque

une migration de provider change les URLs
```

---

# 147. Conséquences observées

Cette section est append-only après mise en œuvre.

## 147.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète
du module média et du provider filesystem App_Data.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- provider ;
- volume ;
- type de média ;
- incident ou bénéfice ;
- action proposée.

---

# 148. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- Les frontières de stockage applicables aux pages, contenus et médias sont définies par les sections 17 à 20 du présent ADR ; aucun catalogue de pages extérieur n'est requis pour appliquer ces règles.
- [0022-browser-security-headers-and-content-security-policy.md](0022-browser-security-headers-and-content-security-policy.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- Les exigences SEO des médias restent définies dans la section 83 du présent ADR (alt text, titre, légende, contexte et nom éventuel) ; aucune documentation SEO privée n'est nécessaire pour les appliquer.
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
- [0002-yaml-client-configuration.md](0002-yaml-client-configuration.md)
- [0003-postgresql-as-primary-database.md](0003-postgresql-as-primary-database.md)
- [0004-explicit-production-migrations.md](0004-explicit-production-migrations.md)
- [0005-file-generation-strategies.md](0005-file-generation-strategies.md)
- [0007-module-composition-and-public-contracts.md](0007-module-composition-and-public-contracts.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](0012-runtime-configuration-and-secrets.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](0014-explicit-client-upgrades-and-version-pinning.md)
- [0015-deterministic-generation-and-optional-ai.md](0015-deterministic-generation-and-optional-ai.md)

---

# 149. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 144 |

---

# 150. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 151. Synthèse de la décision

```text
Repository Git
→ structure
→ code
→ schémas
→ assets statiques
→ seeds initiaux
→ aucun média runtime

PostgreSQL
→ contenus éditables
→ versions éditoriales
→ métadonnées SEO
→ métadonnées médias
→ références MediaId

Blob storage
→ originaux
→ variantes
→ documents
→ provider abstrait

Provider initial
→ App_Data/media
→ volume Docker persistant
→ client et environnement isolés

Identité
→ MediaId public stable
→ Storage Key interne opaque
→ nom utilisateur non utilisé comme chemin

Upload
→ autorisation
→ streaming
→ limite
→ hash
→ détection du type
→ validation
→ quarantaine éventuelle
→ stockage
→ métadonnées
→ publication

URLs
→ fondées sur MediaId
→ indépendantes du filesystem ou bucket
→ stables pendant une migration

Suppression
→ références vérifiées
→ pending-deletion
→ délai
→ suppression physique
→ audit

Cohérence
→ compensation
→ états
→ réconciliation
→ détection des orphelins

Backup
→ PostgreSQL
→ médias
→ manifest commun
→ restauration testée

Évolution
→ migration possible vers S3 ou équivalent
→ contrats et URLs préservés
```

> SiteFactory versionne la structure, persiste le contenu dans PostgreSQL et stocke les fichiers dans un espace conçu pour les blobs. Cette séparation permet d’éditer sans redéployer, de sécuriser les uploads et de changer d’infrastructure sans casser les contenus ni leurs URLs.
