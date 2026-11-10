---
adrId: ADR-0030
title: Use bounded versioned caches with explicit post-commit invalidation
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
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0017-background-jobs-and-reliable-side-effects.md
  - ./0018-public-form-abuse-protection-and-rate-limiting.md
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
targetVersion: 0.1.0
---

# ADR-0030 — Stratégie de cache et invalidation

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La stratégie de cache ci-dessous est une **décision cible** : le public
ne livre pas encore `ISiteFactoryCache`, des caches métier, Redis ni une
politique d'invalidation opérationnelle. Voir le [périmètre livré](../../README.md).
PostgreSQL demeure source de vérité ; un cache ne décide ni des permissions
critiques ni de la compatibilité de la base. Les contrats et clés
`client.sample-project.*` sont conceptuels, pas des APIs disponibles.
Les exigences de classification, versions, TTL, bornes et invalidation
post-commit restent inchangées.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory considère tout cache comme une optimisation temporaire.

La source de vérité reste :

```text
PostgreSQL

le stockage média persistant

la configuration structurelle versionnée

les secrets runtime pour leurs valeurs sensibles
```

La baseline du MVP utilise :

```text
cache navigateur et HTTP pour les ressources publiques adaptées

cache mémoire local borné pour certains read models publics

clés de cache versionnées

versions ou epochs persistés dans PostgreSQL

invalidation après commit uniquement

expiration obligatoire

single-flight pour limiter les cache stampedes

aucun cache distribué obligatoire dans le MVP
```

Les principes suivants sont obligatoires :

```text
aucun cache ne devient source de vérité

aucune écriture métier ne dépend de la présence d’un cache

aucune donnée administrateur sensible dans un cache partagé

aucune décision d’autorisation critique basée sur un cache ancien

aucune invalidation avant le commit PostgreSQL

aucune clé contenant une donnée personnelle directe

aucun cache sans TTL ou mécanisme de version

aucun cache non borné en mémoire

aucune réponse personnalisée stockée comme réponse publique

aucune exception ou erreur transitoire mise en cache
comme résultat valide

aucun déploiement multi-instance avec un cache local mutable
sans stratégie de cohérence distribuée
```

---

## 2. Résumé

Le modèle de référence est :

```text
lecture
→ calcul de la Cache Key
→ résolution de la version ou epoch
→ recherche dans le cache local
→ hit : retour du read model
→ miss : lecture de la source de vérité
→ construction d’un DTO immuable
→ stockage borné avec expiration
→ retour
```

Une mutation suit :

```text
commande Application
→ transaction PostgreSQL
→ modification métier
→ incrément Version ou Cache Region Epoch
→ audit et outbox
→ commit
→ invalidation locale
→ publication éventuelle d’un signal d’invalidation
```

Les mécanismes d’invalidation principaux sont :

```text
Resource Version
→ pour une ressource individuelle

Cache Region Epoch
→ pour une collection ou projection composée

TTL
→ filet de sécurité obligatoire

Post-commit invalidation
→ retrait immédiat dans le processus courant

signal inter-processus
→ optimisation de propagation

relecture périodique de l’epoch PostgreSQL
→ garantie de cohérence bornée
```

La baseline d’infrastructure est :

```text
une instance web

un ou plusieurs processus worker

aucun Redis obligatoire

aucun cache HTTP partagé externe obligatoire

Caddy ne met pas en cache les réponses dynamiques
dans le profil MVP
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Une valeur mise en cache peut toujours être reconstruite.

La suppression du cache ne doit pas casser le métier.

Le redémarrage d’un conteneur peut vider le cache sans incident.

Une mutation rollbackée ne produit aucune invalidation durable.

Une mutation commitée rend l’ancienne version inaccessible.

Une ancienne entrée peut rester physiquement en mémoire,
mais ne doit plus être adressable par la clé courante.

Une donnée supprimée ne doit pas être servie
jusqu’à l’expiration générale d’un cache long.

Une réponse privée utilise no-store.

Une réponse contenant Set-Cookie ne doit pas entrer
dans un cache public partagé.

Une réponse dépendant d’un utilisateur ne doit pas être
mise en cache sous une clé anonyme.

Une clé distingue au minimum :
client, environnement, région, version et locale utile.

Une entrée possède une taille estimée.

Le cache mémoire possède une limite globale.

Un échec de chargement ne crée pas d’entrée valide.

Une annulation d’un appelant ne doit pas empoisonner le cache.

Une mise à jour de format de cache incompatible
change la Cache Schema Version.

Une nouvelle release ne suppose jamais
que les entrées de la release précédente existent.
```

---

## 4. Contexte

Les applications SiteFactory servent notamment :

- pages publiques ;
- navigation ;
- contenus publiés ;
- métadonnées SEO ;
- JSON-LD ;
- catalogues de sections ;
- médias ;
- administration ;
- leads ;
- permissions ;
- sessions ;
- exports ;
- résultats de jobs.

Plusieurs de ces données sont lues beaucoup plus souvent qu’elles ne sont modifiées.

Exemples :

```text
Une page publique
→ peut être lue des milliers de fois
→ mais modifiée rarement.

La navigation
→ est identique pour de nombreuses requêtes publiques.

Une configuration de module
→ change principalement lors d’une release.

Un lead administrateur
→ peut changer à tout moment
→ et dépend des permissions actuelles.
```

Un cache mal conçu peut cependant produire :

- contenu public obsolète ;
- ancienne version d’une page après publication ;
- média supprimé encore accessible ;
- permission retirée encore appliquée ;
- donnée d’un autre client ;
- réponse d’un utilisateur servie à un autre ;
- mémoire non bornée ;
- surcharge lors de l’expiration simultanée ;
- incohérence entre web et worker ;
- cache impossible à vider ;
- incidents difficiles à reproduire.

L’objectif n’est donc pas de « mettre du cache partout », mais de définir précisément :

```text
ce qui peut être mis en cache

où

pendant combien de temps

avec quelle clé

avec quelle invalidation

avec quelle tolérance d’obsolescence
```

---

## 5. Problème à résoudre

Comment réduire :

- les lectures PostgreSQL répétitives ;
- les reconstructions de read models ;
- la bande passante ;
- le temps de réponse des pages publiques ;

tout en garantissant :

- l’isolation client ;
- l’absence de fuite utilisateur ;
- une invalidation fiable ;
- une obsolescence bornée ;
- une suppression effective ;
- la compatibilité avec les workers ;
- la compatibilité avec un futur déploiement multi-instance ;
- une exploitation simple sur VPS ?

La solution doit également éviter :

- une dépendance Redis prématurée ;
- des TTL très longs sans invalidation ;
- des clés non versionnées ;
- des appels à `IMemoryCache` dispersés ;
- le cache d’entités EF Core suivies ;
- les invalidations déclenchées avant commit ;
- le `Vary: Cookie` incontrôlé ;
- les réponses privées stockées publiquement ;
- le cache comme mécanisme d’autorisation ;
- les entrées permanentes ;
- les cache stampedes.

---

## 6. Portée

Cette décision couvre :

- cache navigateur ;
- headers HTTP ;
- ETags ;
- output cache ;
- cache mémoire ;
- cache de read models ;
- clés ;
- TTL ;
- invalidation ;
- epochs ;
- versions ;
- stampede protection ;
- negative caching ;
- données publiques ;
- données privées ;
- contenus ;
- médias ;
- configuration ;
- permissions ;
- sessions ;
- suppressions ;
- déploiements ;
- multi-instance futur ;
- observabilité ;
- tests ;
- quality gates ;
- handover.

---

## 7. Hors portée

Cette décision ne définit pas :

- un CDN obligatoire ;
- Redis comme baseline ;
- un cache distribué multi-région ;
- un cache de requêtes PostgreSQL général ;
- un cache de second niveau EF Core ;
- un moteur de recherche ;
- une réplication en lecture ;
- une stratégie edge computing ;
- la configuration détaillée d’un futur fournisseur CDN ;
- un cache d’objets métier mutables dans Domain ;
- la mise en cache de secrets ;
- un système de session distribué.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Aucune fuite entre utilisateurs ou clients**
2. **Invalidation après commit**
3. **Source de vérité indépendante du cache**
4. **Obsolescence bornée**
5. **Suppression et confidentialité**
6. **Simplicité du MVP**
7. **Performance des pages publiques**
8. **Mémoire bornée**
9. **Protection contre le stampede**
10. **Compatibilité web et worker**
11. **Évolution multi-instance**
12. **Observabilité**
13. **Testabilité**
14. **Compatibilité HTTP**
15. **Faible dépendance fournisseur**
16. **Maintenabilité**

---

## 9. Contraintes

La décision doit respecter :

```text
ASP.NET Core

PostgreSQL

une instance web dans le MVP

processus worker distinct

cache mémoire borné

clés versionnées

invalidations post-commit

Cache-Control

ETag

no-store pour le sensible

aucun secret

aucune PII directe dans les clés

OpenTelemetry

compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- une instance web sert le trafic public dans le MVP ;
- le worker peut modifier certains états visibles par le web ;
- les contenus publics changent peu fréquemment ;
- le redémarrage de l’application est acceptable sans préchauffage obligatoire ;
- PostgreSQL reste disponible pour reconstruire les données ;
- les médias publics peuvent utiliser des identifiants ou versions immuables ;
- les réponses administratives sont suffisamment rapides sans cache partagé ;
- une obsolescence de quelques secondes peut être acceptable pour certaines données publiques, mais pas pour les décisions de sécurité.

---

# 11. Définitions

## 11.1 Cache entry

Valeur temporaire stockée sous une clé.

---

## 11.2 Cache key

Identifiant déterministe d’une entrée.

---

## 11.3 Cache region

Groupe logique d’entrées partageant une stratégie d’invalidation.

---

## 11.4 Cache Schema Version

Version du format de clé et de sérialisation du cache.

---

## 11.5 Resource Version

Version métier d’une ressource, définie dans l’ADR-0028.

---

## 11.6 Cache Region Epoch

Compteur persistant invalidant logiquement toutes les entrées d’une région.

---

## 11.7 Hard TTL

Durée maximale après laquelle une entrée ne peut plus être utilisée.

---

## 11.8 Soft TTL

Durée après laquelle une actualisation peut être déclenchée, tout en autorisant éventuellement une ancienne valeur selon le profil.

---

## 11.9 Cache stampede

Grand nombre de requêtes recalculant simultanément la même entrée.

---

## 11.10 Single-flight

Mécanisme garantissant qu’un seul chargement d’une clé est exécuté à la fois dans un processus.

---

## 11.11 Negative caching

Cache temporaire de l’absence d’une ressource ou d’un résultat vide.

---

## 11.12 Shared cache

Cache susceptible de servir plusieurs utilisateurs ou instances.

---

# 12. Option A — Aucun cache applicatif

## 12.1 Description

Chaque requête lit systématiquement PostgreSQL ou le stockage.

---

## 12.2 Avantages

- cohérence simple ;
- aucune invalidation ;
- aucune fuite liée au cache ;
- faible infrastructure ;
- comportement facile à comprendre.

---

## 12.3 Inconvénients

- lectures répétitives ;
- latence ;
- charge PostgreSQL ;
- reconstruction de projections ;
- faible efficacité sur les pages publiques.

---

## 12.4 Risques

- saturation lors d’un pic ;
- coûts inutiles ;
- temps de réponse dégradé ;
- besoin urgent de cache ajouté sans architecture.

---

## 12.5 Réversibilité

```text
élevée
```

---

# 13. Option B — Cache mémoire avec TTL uniquement

## 13.1 Description

Les données sont conservées dans `IMemoryCache` pendant une durée fixe.

---

## 13.2 Avantages

- simple ;
- rapide ;
- aucune infrastructure externe ;
- efficace pour une instance.

---

## 13.3 Inconvénients

- données obsolètes jusqu’au TTL ;
- invalidation difficile ;
- incohérence avec le worker ;
- aucune coordination multi-instance ;
- risque mémoire.

---

## 13.4 Risques

- page publiée encore ancienne ;
- suppression retardée ;
- TTL allongé pour améliorer les performances ;
- stampede lors de l’expiration.

---

## 13.5 Réversibilité

```text
élevée
```

---

# 14. Option C — Cache distribué obligatoire

## 14.1 Description

Toutes les applications utilisent Redis ou un service équivalent.

---

## 14.2 Avantages

- cohérence multi-instance ;
- partage ;
- invalidation centralisée ;
- sessions ou verrous futurs ;
- bonnes performances.

---

## 14.3 Inconvénients

- infrastructure supplémentaire ;
- secrets ;
- sauvegarde ;
- monitoring ;
- coût ;
- panne supplémentaire ;
- complexité disproportionnée au MVP.

---

## 14.4 Risques

- cache devenu source de vérité ;
- données sensibles persistées ;
- indisponibilité Redis bloquant le site ;
- compatibilité de sérialisation ;
- mauvaise isolation des clients.

---

## 14.5 Réversibilité

```text
moyenne
```

---

# 15. Option D — Cache local versionné avec epochs PostgreSQL

## 15.1 Description

La stratégie combine :

- cache mémoire local ;
- versions métier ;
- epochs persistés ;
- invalidation post-commit ;
- TTL obligatoire ;
- possibilité d’ajouter un cache distribué plus tard.

---

## 15.2 Avantages

- faible infrastructure ;
- cohérence bornée ;
- invalidation fiable ;
- redémarrage sûr ;
- bonne performance publique ;
- évolution multi-instance possible ;
- source de vérité conservée.

---

## 15.3 Inconvénients

- table d’epochs ;
- clés plus complexes ;
- propagation pas toujours instantanée entre processus ;
- tests supplémentaires ;
- nécessité de déclarer les régions.

---

## 15.4 Risques

- oubli d’incrément d’epoch ;
- ancienne entrée physiquement présente ;
- lecture d’un epoch local brièvement obsolète ;
- trop grand nombre de régions.

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

| Critère | Poids | Aucun cache | TTL local | Distribué obligatoire | Local versionné |
|---|---:|---:|---:|---:|---:|
| Cohérence | 5 | 5 | 2 | 5 | 5 |
| Simplicité MVP | 5 | 5 | 5 | 1 | 4 |
| Performance publique | 4 | 2 | 5 | 5 | 5 |
| Invalidation | 5 | 5 | 2 | 5 | 5 |
| Isolation | 5 | 5 | 4 | 4 | 5 |
| Mémoire maîtrisée | 4 | 5 | 3 | 4 | 5 |
| Multi-instance futur | 4 | 3 | 1 | 5 | 4 |
| Exploitation VPS | 4 | 5 | 5 | 2 | 5 |
| Testabilité | 4 | 5 | 4 | 3 | 5 |
| Maintenabilité | 5 | 4 | 3 | 3 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’utiliser un service de cache centralisé au niveau Application ou Infrastructure.

Contrat conceptuel :

```csharp
public interface ISiteFactoryCache
{
    Task<T> GetOrCreateAsync<T>(
        CacheDescriptor descriptor,
        Func<CancellationToken, Task<T>> valueFactory,
        CancellationToken cancellationToken);

    void Remove(CacheKey key);

    void InvalidateRegion(CacheRegionId regionId);
}
```

Les modules ne doivent pas utiliser directement `IMemoryCache` de manière dispersée.

---

## 17.1 Cache Descriptor

Un descriptor doit pouvoir préciser :

```text
CacheRegionId

CacheKey

DataClassification

HardTtl

SoftTtl éventuel

Size

Version

Locale

AllowStaleOnFailure

NegativeCachePolicy

SingleFlightPolicy
```

---

## 17.2 Valeurs mises en cache

Les valeurs sont :

- DTOs immuables ;
- projections ;
- snapshots de contenu ;
- catalogues résolus.

Elles ne sont pas :

- entités EF Core suivies ;
- `DbContext` ;
- streams ouverts ;
- transactions ;
- objets contenant des secrets.

---

# 18. Catégories de cache

La baseline distingue :

```text
browser cache

HTTP response cache

application memory cache

persistent source metadata
```

---

## 18.1 Browser cache

Contrôlé par les headers HTTP.

---

## 18.2 HTTP response cache

Réponse complète mise en cache uniquement pour les routes publiques compatibles.

---

## 18.3 Application memory cache

Read models et métadonnées dans le processus web.

---

## 18.4 Persistent source metadata

Versions et epochs dans PostgreSQL.

Ils ne constituent pas eux-mêmes le cache métier.

---

# 19. Données cacheables dans le MVP

Données initialement éligibles :

```text
pages publiques publiées

navigation publique

footer public

métadonnées SEO publiques

JSON-LD public

configuration résolue de modules non sensible

catalogue public de pages et sections

métadonnées de médias publics immuables

réponses publiques déterministes et anonymes
```

---

## 19.1 Conditions

Une donnée est cacheable uniquement si :

- son propriétaire est identifié ;
- sa région est déclarée ;
- sa clé est définie ;
- sa stratégie d’invalidation existe ;
- sa classification l’autorise ;
- ses tests existent.

---

# 20. Données non cacheables par défaut

Ne sont pas mises en cache dans un cache partagé ou long :

```text
leads

messages de formulaire

notes internes

contenus en brouillon

sessions

tokens

recovery codes

invitations

audit sensible

exports privés

permissions utilisateur

résultats de step-up

kill switch de sécurité critique

secrets

données de quarantaine

données personnelles privées
```

---

## 20.1 Exception

Une exception nécessite :

- classification ;
- TTL très court ;
- isolation par utilisateur ;
- besoin démontré ;
- invalidation ;
- tests ;
- décision humaine.

---

# 21. Cache et autorisation

Une décision d’autorisation critique utilise les données actuelles.

---

## 21.1 Interdiction

Ne pas conserver pendant plusieurs minutes :

```text
User X possède Permission Y
```

comme vérité indépendante.

---

## 21.2 Session validation

Le cache court de validation de session défini dans l’ADR-0020 reste autorisé avec :

- durée maximale bornée ;
- révocation directe sur les actions critiques ;
- Security Stamp ;
- Session Epoch.

---

## 21.3 Catalogues statiques

Le catalogue des permissions disponibles peut être mis en cache.

L’attribution d’une permission à un utilisateur ne l’est pas de la même manière.

---

# 22. Cache et entitlements

Les définitions structurelles d’entitlements peuvent être mises en cache.

---

## 22.1 État client

L’état effectif d’un entitlement client doit posséder :

- Version ;
- invalidation immédiate ;
- TTL court de secours.

---

## 22.2 Action critique

Une opération commerciale ou sensible peut relire PostgreSQL directement.

---

# 23. Clé de cache

Une clé suit une structure logique telle que :

```text
sf:{cacheSchemaVersion}:{client}:{environment}:{region}:{locale}:{resource}:{version}
```

---

## 23.1 Exemple

```text
sf:1:acme:prod:public-page:fr-fr:01JABC:v17
```

---

## 23.2 Valeurs

Les segments sont :

- normalisés ;
- bornés ;
- validés ;
- produits par le serveur.

---

## 23.3 Données interdites

Une clé ne contient pas :

- email ;
- téléphone ;
- nom ;
- message ;
- token ;
- secret ;
- URL signée ;
- contenu libre.

---

# 24. Cache Schema Version

La baseline commence à :

```text
1
```

---

## 24.1 Changement

Elle est incrémentée lorsque changent de manière incompatible :

- format de clé ;
- sérialisation ;
- sémantique ;
- DTO cacheable ;
- région.

---

## 24.2 Effet

Les anciennes entrées deviennent inaccessibles.

---

## 24.3 Nettoyage

Elles sont éliminées naturellement par expiration ou pression mémoire.

---

# 25. Isolation client et environnement

Une clé inclut toujours :

```text
Client ID logique

Environment ID
```

---

## 25.1 Staging

Une entrée staging ne peut jamais être utilisée en production.

---

## 25.2 Autre client

Aucune valeur de client ne doit pouvoir sélectionner le namespace d’un autre client.

---

## 25.3 Source

Le client et l’environnement proviennent de la configuration validée, pas d’un header utilisateur libre.

---

# 26. Locale

Les contenus localisés incluent la locale dans la clé.

---

## 26.1 Normalisation

Exemple :

```text
fr-fr

en-gb
```

selon une convention stable.

---

## 26.2 Langue par défaut

La langue par défaut ne doit pas partager une clé avec une autre locale.

---

## 26.3 Culture du thread

La clé ne doit pas dépendre implicitement de la culture actuelle sans segment explicite.

---

# 27. Resource Version

Une ressource individuelle cacheable utilise sa Version métier.

---

## 27.1 Exemple

```text
public-page:{pageId}:v17
```

---

## 27.2 Mise à jour

Après une modification :

```text
Version 17
→ Version 18
```

La clé `v17` n’est plus utilisée.

---

## 27.3 Ancienne entrée

Elle peut rester en mémoire jusqu’à l’éviction.

Elle n’est plus retournée par le nouveau code de lecture.

---

# 28. Cache Region Epoch

Les collections et projections composées utilisent un epoch.

Exemples de régions :

```text
public-navigation

public-page-index

public-sitemap

public-seo-index

public-media-index

module-public-configuration
```

---

## 28.1 Modèle PostgreSQL

Table conceptuelle :

```text
sitefactory_cache.cache_region_epochs
```

---

## 28.2 Champs

```text
CacheRegionId

Epoch

UpdatedAtUtc

UpdatedByOperationId
```

---

## 28.3 Incrément

L’epoch est incrémenté dans la même transaction que la mutation concernée.

---

# 29. Exemple d’invalidation de collection

Une nouvelle page publiée modifie :

- la page ;
- la navigation ;
- le sitemap ;
- l’index public.

La transaction peut :

```text
mettre à jour la page

incrémenter public-navigation

incrémenter public-sitemap

incrémenter public-page-index

écrire l’audit et l’outbox

commit
```

---

## 29.1 Après commit

Le processus courant invalide les entrées locales des régions concernées.

---

# 30. Résolution des epochs

Le service web doit connaître l’epoch courant.

---

## 30.1 Baseline

L’epoch PostgreSQL peut lui-même être mis en cache localement pendant une durée très courte.

Valeur initiale :

```text
5 secondes maximum
```

---

## 30.2 Staleness bound

En l’absence de signal immédiat inter-processus, une mutation du worker peut rester invisible dans le web pendant au maximum cette fenêtre, plus le temps de traitement normal.

---

## 30.3 Actions sensibles

Une donnée sensible ne doit pas utiliser ce mécanisme si une obsolescence de quelques secondes n’est pas acceptable.

---

# 31. Signal d’invalidation

Un signal peut accélérer la propagation.

Mécanismes possibles :

```text
notification PostgreSQL

table d’événements

signal applicatif

message interne futur
```

---

## 31.1 Garantie

Le signal n’est pas l’unique garantie.

La valeur persistée de l’epoch reste l’autorité.

---

## 31.2 Signal perdu

Un signal perdu ne doit pas produire une obsolescence non bornée.

---

# 32. Invalidation après commit

L’invalidation locale n’est exécutée qu’après un commit réussi.

---

## 32.1 Rollback

Une transaction rollbackée :

- ne change pas l’epoch ;
- ne supprime pas une valeur valide ;
- ne produit pas de faux rafraîchissement durable.

---

## 32.2 Hook post-commit

Le transaction runner de l’ADR-0028 peut enregistrer des actions post-commit bornées.

---

## 32.3 Échec de l’invalidation locale

La version ou l’epoch rend malgré tout l’ancienne entrée logiquement obsolète.

Le TTL reste le dernier filet de sécurité.

---

# 33. Pas d’invalidation avant commit

Le flux suivant est interdit :

```text
supprimer le cache

tenter la transaction

rollback

cache déjà vidé
```

---

## 33.1 Risque

Il ne crée généralement pas de corruption, mais provoque :

- travail inutile ;
- stampede ;
- incohérence d’observabilité ;
- comportement difficile à expliquer.

---

# 34. Invalidation par tag

Le cache local peut indexer les entrées par tags.

Exemples :

```text
page:01JABC

region:public-navigation

locale:fr-fr
```

---

## 34.1 Usage

Les tags accélèrent la suppression locale.

---

## 34.2 Autorité

La clé versionnée ou l’epoch reste la garantie principale.

---

## 34.3 Cardinalité

Les tags sont bornés et ne contiennent pas de PII.

---

# 35. TTL obligatoire

Toute entrée possède un Hard TTL.

---

## 35.1 Aucun TTL infini

Une entrée permanente est interdite.

---

## 35.2 Filet de sécurité

Le TTL protège notamment contre :

- signal perdu ;
- invalidation oubliée ;
- bug de tag ;
- ancien format ;
- entrée orpheline.

---

# 36. Valeurs initiales de TTL

Baseline indicative :

```text
Read model de page publique :
5 minutes

Navigation publique :
5 minutes

SEO et JSON-LD :
5 minutes

Configuration publique résolue :
15 minutes

Métadonnée de média public immuable :
30 minutes en mémoire

Negative cache public :
10 secondes

Cache d’epoch local :
5 secondes
```

---

## 36.1 Non contractuel

Les valeurs sont ajustées par profil après mesure.

---

## 36.2 Limite

Une hausse de TTL ne doit pas remplacer une invalidation manquante.

---

# 37. Expiration absolue et glissante

La baseline utilise :

```text
expiration absolue
```

---

## 37.1 Glissante

Une expiration glissante peut être ajoutée uniquement si :

- une expiration absolue reste présente ;
- la valeur ne peut pas rester indéfiniment ;
- le besoin est démontré.

---

## 37.2 Contenu public fréquent

Une page très fréquentée ne doit pas rester éternellement sans vérification de version.

---

# 38. Jitter

Les TTL peuvent recevoir un léger jitter.

---

## 38.1 Objectif

Éviter que de nombreuses entrées expirent simultanément.

---

## 38.2 Bornes

Le jitter reste faible et ne dépasse pas le Hard TTL maximal du profil.

---

# 39. Soft TTL

Un profil public peut définir :

```text
Soft TTL < Hard TTL
```

---

## 39.1 Comportement

Après le Soft TTL :

- un appel peut déclencher un refresh ;
- l’ancienne valeur peut éventuellement être servie selon la policy.

---

## 39.2 Baseline MVP

Le stale-while-refresh applicatif n’est pas obligatoire.

La priorité reste une implémentation simple et correcte.

---

# 40. Stale-on-error

Une ancienne valeur publique peut être servie temporairement lors d’une panne PostgreSQL uniquement si :

- la donnée est publique ;
- la valeur a déjà été validée ;
- le Hard TTL étendu reste borné ;
- le profil l’autorise ;
- aucun besoin de suppression urgente ne s’y oppose.

---

## 40.1 Interdictions

Stale-on-error est interdit pour :

- identité ;
- permissions ;
- sessions ;
- leads ;
- exports ;
- audit ;
- suppression ;
- données privées ;
- kill switches critiques.

---

## 40.2 Baseline initiale

Le profil MVP peut rester :

```text
AllowStaleOnFailure = false
```

jusqu’à l’existence d’un besoin opérationnel mesuré.

---

# 41. Negative caching

L’absence d’une ressource publique peut être mise en cache brièvement.

---

## 41.1 Exemple

```text
page publique inexistante
→ cache négatif de 10 secondes
```

---

## 41.2 Objectif

Réduire :

- scans ;
- requêtes répétitives ;
- trafic sur des URLs invalides.

---

## 41.3 Interdictions

Ne pas utiliser le negative caching pour révéler ou mémoriser :

- existence d’un compte ;
- invitation ;
- email ;
- ressource privée ;
- autorisation refusée ;
- token.

---

# 42. Cache stampede

Le service utilise une stratégie single-flight par clé.

---

## 42.1 Comportement

```text
premier appel
→ exécute le value factory

appels concurrents
→ attendent le même chargement
```

---

## 42.2 Périmètre

La baseline protège le processus local.

---

## 42.3 Multi-instance futur

Une stratégie distribuée pourra devenir nécessaire.

---

# 43. Single-flight et erreurs

Si le value factory échoue :

- aucune entrée valide n’est ajoutée ;
- les appelants reçoivent une erreur contrôlée ;
- un prochain appel peut retenter ;
- l’exception elle-même n’est pas mise en cache.

---

## 43.1 Cancellation

L’annulation d’un seul appelant ne doit pas nécessairement annuler le chargement partagé utile à d’autres appelants.

La stratégie exacte doit être testée.

---

# 44. Limite de concurrence des refreshes

Le nombre global de chargements de cache simultanés est borné.

---

## 44.1 Objectif

Éviter qu’une purge ou un redémarrage provoque :

- saturation PostgreSQL ;
- saturation CPU ;
- saturation mémoire.

---

## 44.2 Priorité

Les requêtes interactives ne doivent pas être bloquées indéfiniment par un warming massif.

---

# 45. Taille du cache mémoire

Le cache mémoire possède une limite globale.

---

## 45.1 Taille d’entrée

Chaque entrée fournit une estimation :

```text
Size
```

---

## 45.2 Unité

L’unité peut être logique, à condition d’être cohérente et documentée.

---

## 45.3 Objet volumineux

Une entrée dépassant le maximum est refusée.

---

## 45.4 Gros médias

Les fichiers binaires volumineux ne sont pas conservés intégralement dans `IMemoryCache`.

---

# 46. Éviction

Les évictions possibles comprennent :

- expiration ;
- pression mémoire ;
- invalidation ;
- changement de version ;
- changement d’epoch ;
- arrêt du processus.

---

## 46.1 Fonctionnel

Aucune éviction ne doit modifier le résultat fonctionnel final.

---

## 46.2 Callback

Un callback d’éviction ne doit pas déclencher un effet métier critique.

---

# 47. Préchargement

Le préchargement est facultatif.

---

## 47.1 Cibles possibles

- page d’accueil ;
- navigation ;
- pages principales ;
- configuration publique.

---

## 47.2 Bornes

Le warming est :

- borné ;
- annulable ;
- observé ;
- sans scan complet ;
- sans dépendance au succès du démarrage.

---

## 47.3 Démarrage

Une erreur de warming ne rend pas l’application non-ready si la source de vérité reste accessible.

---

# 48. Cache navigateur des assets statiques

Les assets hashés utilisent :

```text
Cache-Control: public, max-age=31536000, immutable
```

---

## 48.1 Conditions

Le nom du fichier doit changer lorsque son contenu change.

---

## 48.2 Exemples

- CSS versionné ;
- JavaScript versionné ;
- fontes self-hosted versionnées ;
- images de build immuables.

---

## 48.3 HTML

Le HTML dynamique n’utilise pas cette politique.

---

# 49. Médias publics

Un média public identifié par un Media ID et une version immuable peut utiliser un cache long.

---

## 49.1 URL immuable

Exemple conceptuel :

```text
/media/{mediaId}/{variant}/{contentHash}.{extension}
```

---

## 49.2 Cache

```text
public, max-age=31536000, immutable
```

---

## 49.3 Modification

Une modification du contenu crée :

- un nouveau hash ;
- une nouvelle version ;
- une nouvelle URL.

---

# 50. URLs média mutables

Une URL de commodité telle que :

```text
/media/logo
```

ne doit pas être mise en cache pendant un an.

---

## 50.1 Stratégies

- cache court ;
- redirection vers une URL immuable ;
- ETag ;
- revalidation.

---

## 50.2 Préférence

La redirection vers une ressource immuable est privilégiée.

---

# 51. Médias privés

Les médias privés utilisent :

```text
Cache-Control: no-store
```

ou un cache privé très strict lorsqu’un besoin est démontré.

---

## 51.1 Baseline

Aucun stockage public ou CDN partagé.

---

## 51.2 Révocation

Une permission retirée doit prendre effet sans dépendre d’un cache navigateur long.

---

# 52. HTML public

La baseline du HTML public utilise :

```text
Cache-Control: no-cache
```

ou :

```text
public, max-age=0, must-revalidate
```

avec ETag lorsque disponible.

---

## 52.1 Signification

Le navigateur peut conserver la représentation, mais doit la revalider avant réutilisation.

---

## 52.2 Données publiques

Aucun contenu personnalisé ni `Set-Cookie` ne doit être présent dans une réponse déclarée publique.

---

# 53. Pages administratives

Les pages et réponses administratives utilisent :

```text
Cache-Control: no-store
```

---

## 53.1 Couverture

- leads ;
- contenus en brouillon ;
- audit ;
- exports ;
- sessions ;
- rôles ;
- opérations ;
- configuration ;
- médias privés.

---

# 54. Pages Identity

Les pages suivantes utilisent `no-store` :

- login ;
- MFA ;
- step-up ;
- invitation ;
- reset ;
- récupération ;
- changement d’email.

---

## 54.1 Référence

Cette règle complète les ADR-0020, ADR-0021 et ADR-0022.

---

# 55. Erreurs

Une réponse d’erreur sensible n’est pas mise en cache.

---

## 55.1 Baseline

```text
Cache-Control: no-store
```

pour :

- 401 ;
- 403 ;
- 409 sensible ;
- 422 contenant des erreurs de saisie ;
- 500.

---

## 55.2 404 public

Un 404 public anonyme peut utiliser un cache très court lorsque le profil l’autorise.

---

# 56. Output cache

ASP.NET Core Output Caching peut être utilisé pour certaines réponses publiques anonymes.

---

## 56.1 Conditions

Une route output-cacheable doit être :

- GET ou HEAD ;
- publique ;
- déterministe ;
- sans cookie utilisateur ;
- sans antiforgery ;
- sans donnée personnalisée ;
- sans `Set-Cookie` ;
- associée à une région et une invalidation.

---

## 56.2 Interdictions

Ne pas appliquer de policy globale aux routes :

- `/admin` ;
- `/api/v1/admin` ;
- `/security` ;
- `/identity` ;
- `/exports` ;
- `/operations` privées.

---

# 57. Clé d’output cache

La clé doit distinguer au minimum :

```text
route

client

environnement

locale

query allowlistée

epoch ou version
```

---

## 57.1 Query string

Une query non allowlistée ne doit pas produire une cardinalité illimitée.

---

## 57.2 Headers

Les headers utilisés pour varier la réponse sont explicitement allowlistés.

---

# 58. `Vary`

`Vary` est utilisé avec prudence.

---

## 58.1 Valeurs possibles

```text
Accept-Encoding

Accept-Language
```

selon le contrat.

---

## 58.2 Cookie

```text
Vary: Cookie
```

n’est pas la baseline, car il peut créer :

- cardinalité élevée ;
- cache privé ambigu ;
- risque de fuite.

---

## 58.3 User-Agent

Le cache ne varie pas par User-Agent complet.

---

# 59. ETags publics

Une page ou API publique peut retourner un ETag dérivé de :

- Version ;
- content hash ;
- Cache Region Epoch ;
- locale.

---

## 59.1 Revalidation

Une requête :

```text
If-None-Match
```

peut recevoir :

```text
304 Not Modified
```

---

## 59.2 Auth

Une réponse `304` ne contourne pas une autorisation lorsqu’une route est protégée.

---

# 60. `Last-Modified`

Le header peut être utilisé lorsque la date de dernière modification est fiable.

---

## 60.1 Précision

Il ne remplace pas une Version ou un ETag pour la concurrence.

---

## 60.2 Dates techniques

Une migration technique ne doit pas modifier artificiellement le `Last-Modified` public.

---

# 61. Caddy

Caddy ne met pas en cache les réponses dynamiques dans la baseline MVP.

---

## 61.1 Responsabilités

Caddy :

- termine TLS ;
- transmet les requêtes ;
- conserve les headers de cache applicatifs ;
- ne remplace pas les politiques route-specific.

---

## 61.2 Plugin de cache

L’ajout d’un plugin de cache Caddy nécessite :

- build d’image spécifique ;
- review supply chain ;
- stratégie d’invalidation ;
- nouvel examen architectural.

---

# 62. CDN futur

Un CDN pourra être introduit pour :

- assets statiques ;
- médias publics immuables ;
- éventuellement HTML public.

---

## 62.1 Conditions

- origines ;
- purge ;
- clés ;
- cookies ;
- `Vary` ;
- CSP ;
- confidentialité ;
- handover ;
- Deletion Ledger ;
- logs.

---

## 62.2 Pages sensibles

Les pages Identity et administration restent exclues.

---

# 63. Cache des requêtes PostgreSQL

Un cache générique de toutes les requêtes n’est pas utilisé.

---

## 63.1 Raisons

- invalidation inconnue ;
- tracking ;
- paramètres ;
- permissions ;
- forte cardinalité ;
- obsolescence.

---

## 63.2 Read models explicites

Seules des projections identifiées et testées sont mises en cache.

---

# 64. Entités EF Core

Une entité suivie par EF Core n’est jamais conservée dans un cache partagé entre requêtes.

---

## 64.1 Risques

- tracking obsolète ;
- contexte disposé ;
- navigation lazy ;
- mutation involontaire ;
- concurrence.

---

## 64.2 DTO

Le cache contient un objet immuable sans dépendance au DbContext.

---

# 65. Sérialisation

Le cache mémoire local peut conserver directement un objet immuable.

---

## 65.1 Cache distribué futur

Un cache distribué exigera :

- schéma versionné ;
- sérialisation explicite ;
- taille bornée ;
- compatibilité ;
- protection des données ;
- tests cross-version.

---

## 65.2 Sérialisation .NET native opaque

Une sérialisation binaire non documentée et non versionnée est interdite.

---

# 66. Cache des configurations

La configuration structurelle issue du repository peut être conservée pendant la durée du processus.

---

## 66.1 Version

Elle reste liée à :

- application version ;
- lock file ;
- configuration hash.

---

## 66.2 Runtime configuration mutable

Une configuration runtime modifiable utilise :

- Version ;
- Cache Region Epoch ;
- invalidation ;
- TTL.

---

## 66.3 Secrets

Les valeurs secrètes ne sont jamais placées dans un cache général observable.

---

# 67. Cache des secrets

Le secret provider peut posséder son propre cache technique très court.

---

## 67.1 Hors cache général

Ce mécanisme est distinct de `ISiteFactoryCache`.

---

## 67.2 Contraintes

- mémoire seulement ;
- TTL court ;
- aucune métrique de valeur ;
- effacement lors de rotation lorsque possible ;
- aucune persistance.

---

# 68. Cache et suppression

Une suppression approuvée doit :

```text
modifier ou supprimer la source

incrémenter les versions et epochs concernés

commit

invalider les caches locaux

supprimer les variantes et caches externes prévus
```

---

## 68.1 Ancienne entrée

Une entrée contenant une donnée supprimée ne doit plus être adressable après commit.

---

## 68.2 TTL

Le TTL ne doit pas être l’unique mécanisme de suppression d’une donnée personnelle.

---

## 68.3 CDN futur

Une purge CDN fait partie du workflow de suppression.

---

# 69. Cache et anonymisation

L’anonymisation :

- produit une nouvelle Version ;
- incrémente les régions concernées ;
- invalide les read models ;
- ne conserve pas l’ancienne représentation dans un cache partagé.

---

## 69.1 Mémoire

L’ancienne entrée mémoire devient inaccessible immédiatement dans le processus courant.

---

# 70. Cache et restauration

Après restauration :

```text
nouveaux epochs de sécurité

réconciliation Privacy

nouvelle Cache Global Epoch

redémarrage des services

cache mémoire vide
```

---

## 70.1 Cache Global Epoch

Un epoch global peut invalider toutes les régions applicatives.

---

## 70.2 Service

Le site ne réutilise aucun cache sauvegardé depuis l’ancien environnement.

---

# 71. Cache Global Epoch

Table ou configuration conceptuelle :

```text
GlobalCacheEpoch
```

---

## 71.1 Changement

Il peut être incrémenté après :

- restauration ;
- migration majeure ;
- incident ;
- changement incompatible ;
- import complet.

---

## 71.2 Clé

Le Global Cache Epoch peut appartenir au namespace de toute clé mutable.

---

# 72. Cache et déploiement

Une nouvelle release peut :

- conserver le Cache Schema Version ;
- ou l’incrémenter.

---

## 72.1 Cache local

Le redémarrage vide naturellement le cache mémoire.

---

## 72.2 Rolling deployment futur

Deux versions coexistantes ne doivent pas partager une sérialisation incompatible.

---

## 72.3 Namespace de release

Un futur cache distribué peut inclure :

```text
cacheSchemaVersion
```

et non nécessairement toute la version applicative.

---

# 73. Cache et rollback

Un rollback applicatif ne doit pas dépendre d’entrées produites par la version suivante.

---

## 73.1 Baseline locale

Le redémarrage sur l’ancien digest produit un cache vide.

---

## 73.2 Cache distribué futur

Le rollback utilise un namespace compatible ou vide le namespace concerné.

---

# 74. Cache et migrations

Une migration modifiant la sémantique des read models peut exiger :

- nouvel epoch ;
- nouvelle Cache Schema Version ;
- invalidation globale ;
- warming optionnel.

---

## 74.1 Pas de migration de cache obligatoire

Le cache doit pouvoir être abandonné plutôt que migré.

---

# 75. Cache et jobs

Le worker ne doit pas supposer que son cache local est partagé avec le web.

---

## 75.1 Mutation worker

Le worker incrémente l’epoch PostgreSQL dans sa transaction.

---

## 75.2 Signal

Il peut envoyer un signal d’invalidation après commit.

---

## 75.3 Lecture worker

Un worker peut utiliser un cache local pour une configuration stable, avec son propre lifecycle.

---

# 76. Cache et outbox

Un événement d’invalidation peut être écrit dans l’outbox.

Type conceptuel :

```text
cache.region-invalidated.v1
```

---

## 76.1 Contenu

```text
CacheRegionId

NewEpoch

OperationId

OccurredAtUtc
```

---

## 76.2 Données interdites

- contenu ;
- email ;
- payload métier ;
- secret ;
- ancienne valeur.

---

## 76.3 Garantie

L’epoch PostgreSQL reste l’autorité si le message n’est pas traité.

---

# 77. Cache et transactions

Le value factory ne doit pas conserver une transaction longue uniquement pour construire une valeur.

---

## 77.1 Lecture simple

Une lecture ordinaire utilise une requête bornée.

---

## 77.2 Snapshot

Une projection nécessitant un snapshot cohérent utilise le profil transactionnel prévu.

---

## 77.3 Appel imbriqué

Le chargement du cache ne doit pas ouvrir une transaction incompatible à l’intérieur d’une commande déjà transactionnelle.

---

# 78. Lecture après écriture

Après une commande réussie, l’appelant doit recevoir :

- le résultat de la transaction ;
- ou une relecture cohérente.

Il ne doit pas recevoir une ancienne valeur provenant du cache.

---

## 78.1 Stratégie

- utiliser le DTO produit par la commande ;
- ou invalider puis recharger avec la nouvelle Version.

---

# 79. Cache et concurrence optimiste

Une version obsolète mise en cache ne doit jamais être utilisée comme Expected Version implicite pour une commande.

---

## 79.1 Interface

Le formulaire possède la Version réellement chargée.

---

## 79.2 Revalidation

Une commande vérifie PostgreSQL, même si l’affichage initial provenait d’un cache public.

---

# 80. Cache et idempotence

Les résultats d’idempotence de l’ADR-0029 ne sont pas un cache ordinaire.

---

## 80.1 Persistance

Ils appartiennent à PostgreSQL et au contrat de commande.

---

## 80.2 Interdiction

Une éviction mémoire ne doit pas permettre la réexécution d’une commande idempotente.

---

# 81. Cache et rate limiting

Le rate limiting ne doit pas dépendre d’un cache général susceptible d’être vidé.

---

## 81.1 Profil mono-instance

Un compteur mémoire peut être utilisé selon l’ADR-0018.

---

## 81.2 Distinction

Il appartient à un mécanisme de contrôle, pas au cache de read models.

---

# 82. Cache et health checks

Les health checks ne doivent pas retourner une réponse saine uniquement parce qu’une ancienne valeur est en cache.

---

## 82.1 Readiness

La source de vérité requise doit être vérifiée selon le profil.

---

## 82.2 Public stale

Un mode stale public éventuel ne transforme pas PostgreSQL indisponible en état entièrement ready pour l’administration.

---

# 83. Cache et erreurs

Les exceptions ne sont pas mises en cache.

---

## 83.1 Résultat vide

Un résultat vide peut être caché uniquement via une Negative Cache Policy explicite.

---

## 83.2 Provider externe

Une erreur fournisseur ne devient pas une réponse publique valide durablement cachée.

---

# 84. Cache poisoning

Les entrées sont produites uniquement depuis :

- données validées ;
- configuration approuvée ;
- read models serveur.

---

## 84.1 Clé utilisateur

Une valeur utilisateur ne peut pas choisir librement :

- région ;
- client ;
- environnement ;
- type sérialisé.

---

## 84.2 Header poisoning

Les headers utilisés dans une variation sont allowlistés et normalisés.

---

# 85. Cache deception

Les routes privées ne doivent pas pouvoir être interprétées comme assets publics à cause d’une extension de path.

---

## 85.1 Exemple

```text
/admin/leads/01J/report.css
```

ne doit pas recevoir un cache public uniquement parce que le path se termine par `.css`.

---

## 85.2 Décision

La policy de cache dépend de l’endpoint sélectionné, pas uniquement de l’extension.

---

# 86. Clés à forte cardinalité

Une clé ne doit pas inclure librement :

- query complète ;
- User-Agent ;
- timestamp courant ;
- Correlation ID ;
- Trace ID ;
- session ;
- chaîne de recherche illimitée.

---

## 86.1 Filtres

Les filtres cacheables sont :

- bornés ;
- normalisés ;
- allowlistés.

---

## 86.2 Administration

Les recherches administratives ne sont pas mises en cache par défaut.

---

# 87. Cache d’une collection

Une collection cacheable doit définir :

```text
tri

filtres

locale

page ou cursor

Region Epoch

maximum d’éléments
```

---

## 87.1 Collection complète

Une collection publique très volumineuse ne doit pas être stockée comme un objet unique.

---

# 88. Cache et pagination

Les pages d’une collection peuvent partager un Region Epoch.

---

## 88.1 Mutation

Toute mutation affectant la collection incrémente l’epoch.

---

## 88.2 Curseur

Le cursor de pagination reste indépendant du cache.

---

# 89. Prévention de la fuite inter-utilisateurs

Une réponse partagée ne doit dépendre d’aucune donnée utilisateur.

---

## 89.1 Tests

Comparer :

- utilisateur A ;
- utilisateur B ;
- anonyme ;
- administrateur ;
- session expirée.

---

## 89.2 Header

Une réponse comportant :

```text
Set-Cookie
```

est exclue du cache public.

---

# 90. Observabilité

Événements recommandés :

```text
CacheLookupStarted

CacheHit

CacheMiss

CacheEntryCreated

CacheEntryEvicted

CacheRegionInvalidated

CacheEpochChanged

CacheLoadFailed

CacheSingleFlightWaited

CacheMemoryLimitReached

CacheStaleValueServed
```

---

## 90.1 Propriétés autorisées

```text
CacheRegionId

CacheProfileId

Result

EvictionReason

DataClassification

Environment

Duration

SizeBucket
```

---

## 90.2 Données interdites

```text
CacheKey complète si elle contient un Resource ID sensible

Email

Téléphone

Nom

Message

Valeur mise en cache

Token

Secret

Session ID

Trace ID comme label métrique
```

---

# 91. Métriques

Exemples :

```text
cache lookups

cache hits

cache misses

cache hit ratio

cache evictions

cache load duration

cache load failures

cache invalidations

cache epoch lag

cache entry count

cache estimated size

cache single-flight waiters
```

---

## 91.1 Labels autorisés

```text
cache_region

cache_profile

result

eviction_reason

environment
```

---

## 91.2 Labels interdits

```text
cache_key

resource_id

user_id

email

locale libre non bornée

query
```

---

# 92. Cache hit ratio

Un taux de hit élevé n’est pas un objectif absolu.

---

## 92.1 Priorité

Une entrée peu utile ou risquée ne doit pas être ajoutée uniquement pour augmenter le hit ratio.

---

## 92.2 Analyse

Le ratio est analysé avec :

- coût de chargement ;
- taille ;
- fréquence ;
- staleness ;
- invalidations.

---

# 93. Invalidation lag

La plateforme mesure le délai entre :

```text
commit de l’epoch

prise en compte par le processus web
```

---

## 93.1 Baseline

Le délai attendu reste inférieur à la durée du cache local d’epoch.

---

## 93.2 Alerte

Un délai anormal peut signaler :

- signal perdu ;
- worker bloqué ;
- lecture PostgreSQL en erreur ;
- mauvaise configuration.

---

# 94. Alertes

Alertes possibles :

```text
Cache mémoire proche de sa limite

Hit ratio anormalement faible

Chargements de cache très lents

Stampede répété

Invalidation lag élevé

Cache Region Epoch non mis à jour

Réponse privée avec Cache-Control public

Réponse publique variant par Cookie

Cache Schema Version incohérente

Donnée supprimée encore servie
```

---

## 94.1 Runbook

Chaque alerte critique référence une procédure.

---

# 95. Audit

Les hits et misses ne sont pas audités.

---

## 95.1 Actions auditables

- modification d’un profil de cache production ;
- activation d’un stale-on-error ;
- ajout d’un CDN ;
- changement d’un TTL sensible ;
- invalidation globale manuelle ;
- purge CDN ;
- modification de Cache Schema Version.

---

## 95.2 Invalidation métier

L’audit principal reste celui de la mutation métier.

---

# 96. Invalidation manuelle

Une commande opérateur peut demander une invalidation.

Exemple :

```text
sitefactory cache invalidate plan

sitefactory cache invalidate apply
```

---

## 96.1 Portée

- région ;
- locale ;
- ressource ;
- global.

---

## 96.2 Plan

Le plan affiche :

- client ;
- environnement ;
- région ;
- epoch courant ;
- nouvel epoch ;
- impact attendu.

---

## 96.3 Apply

Exige :

- permission ;
- environnement confirmé ;
- raison ;
- audit.

---

# 97. Cache inspection

Commande conceptuelle :

```text
sitefactory cache status
```

---

## 97.1 Données autorisées

- régions ;
- epochs ;
- nombre estimé d’entrées ;
- taille ;
- hit ratio ;
- dernière invalidation.

---

## 97.2 Données interdites

La commande ne retourne pas les valeurs sensibles du cache.

---

# 98. Cache warming opérateur

Commande conceptuelle :

```text
sitefactory cache warm plan

sitefactory cache warm apply
```

---

## 98.1 Données

Le warming cible uniquement des ressources publiques approuvées.

---

## 98.2 Production

Une exécution importante nécessite un plan afin d’éviter une surcharge PostgreSQL.

---

# 99. Fail-open et fail-closed

Le cache de read models utilise :

```text
fail-open vers la source de vérité
```

---

## 99.1 Cache indisponible

L’application charge depuis PostgreSQL.

---

## 99.2 Source indisponible

Le comportement dépend du profil stale-on-error.

---

## 99.3 Sécurité

Une décision d’autorisation ou de révocation utilise :

```text
fail-closed
```

lorsque l’état actuel ne peut pas être vérifié.

---

# 100. Futur cache distribué

Un cache distribué sera envisagé si :

- plusieurs instances web ;
- forte charge ;
- grosse reconstruction de projections ;
- besoin de partage ;
- besoin de cohérence plus rapide ;
- cache mémoire insuffisant.

---

## 100.1 Conditions

Avant introduction :

- authentification ;
- TLS ;
- isolation client ;
- TTL ;
- quotas ;
- sérialisation versionnée ;
- politique de panne ;
- backup ou absence de backup assumée ;
- invalidation ;
- observabilité ;
- handover.

---

## 100.2 Source de vérité

Même avec Redis :

```text
PostgreSQL reste la source de vérité
```

---

# 101. Multi-instance

Le profil local du MVP n’est pas déclaré compatible avec plusieurs instances web actives si des données mutables sont mises en cache sans mécanisme partagé complet.

---

## 101.1 Gate

Le passage à plusieurs instances exige :

- signal d’invalidation fiable par instance ;
- epochs ;
- cache distribué ou caches locaux cohérents ;
- tests multi-instance ;
- mesure du lag.

---

## 101.2 Sticky sessions

Les sticky sessions ne résolvent pas la cohérence du cache.

---

# 102. Sécurité d’un futur Redis

Un Redis futur ne doit pas :

- être exposé publiquement ;
- utiliser une base commune sans namespace ;
- contenir de secrets durables ;
- utiliser des clés avec PII ;
- être utilisé comme stockage métier ;
- fonctionner sans limite mémoire.

---

# 103. Backup du cache

Le cache mémoire n’est pas sauvegardé.

---

## 103.1 Cache distribué futur

La baseline considère qu’un cache peut être perdu entièrement.

---

## 103.2 Reconstruction

Toutes les données doivent être reconstructibles.

---

# 104. Privacy et rétention

Une entrée de cache possède une rétention inférieure ou égale à celle de sa donnée source.

---

## 104.1 Suppression

L’invalidation intervient au moment de la suppression logique ou physique selon le contrat.

---

## 104.2 Logs

Les clés et métriques ne doivent pas recréer une nouvelle conservation personnelle.

---

# 105. Sécurité

## 105.1 Actifs

- contenus ;
- médias ;
- sessions ;
- permissions ;
- données privées ;
- mémoire ;
- headers ;
- clients ;
- versions.

---

## 105.2 Menaces

- fuite inter-client ;
- fuite inter-utilisateur ;
- réponse privée cachée ;
- cache poisoning ;
- cache deception ;
- donnée supprimée encore servie ;
- autorisation obsolète ;
- mémoire saturée ;
- stampede ;
- incohérence multi-instance.

---

## 105.3 Contrôles

- namespaces ;
- classifications ;
- no-store ;
- versions ;
- epochs ;
- TTL ;
- invalidation post-commit ;
- taille bornée ;
- single-flight ;
- tests ;
- observabilité ;
- quality gates.

---

# 106. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Page d’un client servie à un autre | Namespace client et environnement |
| Réponse admin mise en cache | `no-store` et exclusion output cache |
| Permission retirée encore active | Relecture et bypass sur action critique |
| Page publiée obsolète | Version et Region Epoch |
| Média supprimé encore accessible | URL versionnée et invalidation |
| Stampede après déploiement | Single-flight et warming borné |
| Cache mémoire saturé | Size limit et quotas |
| Query libre créant des millions de clés | Filtres et variation allowlistés |
| Réponse avec cookie stockée publiquement | Exclusion `Set-Cookie` |
| Signal d’invalidation perdu | Epoch PostgreSQL et TTL |
| Cache distribué perdu | Reconstruction depuis la source |
| Restore réutilisant un ancien cache | Global Epoch et redémarrage |

---

# 107. Tests unitaires

Tester notamment :

- construction des clés ;
- normalisation ;
- namespace ;
- version ;
- epoch ;
- TTL ;
- jitter ;
- classifications ;
- policy no-store ;
- negative cache ;
- single-flight ;
- limites.

---

# 108. Tests des clés

Vérifier :

```text
deux clients → deux clés

staging et production → deux clés

deux locales → deux clés

Version 4 et Version 5 → deux clés

Cache Schema Version différente → deux clés

aucune PII directe
```

---

# 109. Tests d’invalidation post-commit

Scénario :

1. charger une valeur ;
2. commencer une mutation ;
3. préparer une invalidation ;
4. rollback.

Résultat :

```text
ancienne valeur toujours valide
epoch inchangé
```

Puis :

1. exécuter la mutation ;
2. commit ;
3. incrémenter l’epoch ;
4. relire.

Résultat :

```text
nouvelle valeur
ancienne clé non réutilisée
```

---

# 110. Tests du worker

Scénario :

1. web met une projection en cache ;
2. worker modifie la source ;
3. worker incrémente l’epoch ;
4. web relit.

Résultat :

```text
nouvelle valeur dans la fenêtre maximale documentée
```

---

# 111. Tests de suppression

Injecter une valeur marqueur :

1. mettre en cache ;
2. supprimer ou anonymiser ;
3. commit ;
4. relire toutes les routes concernées.

La valeur ne doit plus apparaître.

---

# 112. Tests de fuite utilisateur

Tester :

- anonyme ;
- administrateur A ;
- administrateur B ;
- utilisateur sans permission ;
- session révoquée.

Une réponse privée ne doit jamais provenir du cache d’un autre contexte.

---

# 113. Tests HTTP

Vérifier :

```text
assets hashés → immutable

HTML public → revalidation

administration → no-store

Identity → no-store

média public immuable → cache long

média privé → no-store

erreurs sensibles → no-store
```

---

# 114. Tests Output Cache

Vérifier :

- route publique ;
- réponse anonyme ;
- locale ;
- query allowlistée ;
- invalidation ;
- réponse avec `Set-Cookie` ;
- route administrateur ;
- session ;
- erreur.

---

# 115. Tests ETag

Couvrir :

- ETag courant ;
- ETag ancien ;
- `If-None-Match` ;
- `304` ;
- changement de Version ;
- changement d’epoch ;
- locale différente.

---

# 116. Tests single-flight

Exécuter plusieurs lectures simultanées de la même clé.

Résultat :

```text
un seul value factory

plusieurs appelants servis
```

---

## 116.1 Échec

Simuler un value factory en échec.

Vérifier :

- aucune entrée ;
- aucune exception mise en cache ;
- retry futur possible.

---

# 117. Tests de pression mémoire

Simuler :

- nombreuses entrées ;
- grosses entrées ;
- dépassement de limite ;
- évictions ;
- redémarrage.

---

## 117.1 Résultat

Le service reste fonctionnel et recharge depuis PostgreSQL.

---

# 118. Tests de negative caching

Vérifier :

- absence publique ;
- expiration courte ;
- création de la ressource pendant le TTL ;
- invalidation par epoch ;
- absence de cache sur un compte ou token.

---

# 119. Tests de cache poisoning

Tenter d’influencer :

- client ;
- environnement ;
- région ;
- locale invalide ;
- query ;
- header ;
- path.

La valeur utilisateur ne doit pas permettre de sélectionner un namespace arbitraire.

---

# 120. Tests de cache deception

Tester une route privée avec une fausse extension statique.

Résultat :

```text
no-store
aucun cache public
```

---

# 121. Tests de déploiement

Vérifier :

- cache vide au démarrage ;
- warming facultatif ;
- nouvelle Cache Schema Version ;
- rollback ;
- anciens entries inaccessibles ;
- même résultat fonctionnel sans cache.

---

# 122. Tests multi-instance futurs

Avant activation multi-instance, tester :

- deux web ;
- invalidation par l’un ;
- mutation par worker ;
- signal perdu ;
- epoch ;
- lag ;
- cache distribué indisponible.

---

# 123. Tests de non-divulgation

Injecter des marqueurs représentant :

- email ;
- téléphone ;
- token ;
- session ;
- secret ;
- message.

Vérifier leur absence dans :

- cache keys ;
- métriques ;
- logs ;
- événements d’invalidation ;
- manifests.

---

# 124. Tests multiplateformes

Les contrats et tests de cache local fonctionnent sur :

```text
Windows x64

Linux x64
```

Les tests d’intégration PostgreSQL, web, worker et Docker sont validés sur Linux.

---

# 125. Quality gates

Une contribution est bloquée lorsque :

```text
Un module utilise directement IMemoryCache
sans passer par le contrat central.

Une entrée ne possède pas de TTL.

Une clé contient une PII directe.

Le client ou l’environnement manque dans la clé.

Une donnée privée utilise un cache partagé.

Une route admin possède Cache-Control public.

Une route Identity n’utilise pas no-store.

Une invalidation est exécutée avant commit.

Une mutation n’incrémente pas la Version ou l’epoch prévu.

Un cache contient une entité EF Core suivie.

Le cache mémoire n’est pas borné.

Un value factory effectue un traitement externe non prévu.

Une exception est mise en cache.

Une réponse avec Set-Cookie est output-cacheable.

Une route dépendant d’un Cookie est stockée publiquement.

Les tests de suppression ou d’isolation échouent.
```

---

# 126. Quality gates de production

La production est bloquée lorsque :

```text
Les régions de cache ne sont pas déclarées.

La table des Cache Region Epochs est absente.

Le Global Cache Epoch n’est pas initialisé.

Les headers de cache sensibles sont absents.

Le cache mémoire ne possède pas de limite.

Une policy stale-on-error sensible est active.

Le worker modifie des données cacheables
sans mettre à jour leur epoch.

La Cache Schema Version est incompatible.

Un déploiement multi-instance utilise uniquement
un cache local non coordonné.

Les métriques d’invalidation sont absentes.

Le runbook d’invalidation globale est absent.
```

---

# 127. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-CACHE-001` | Région de cache inconnue |
| `SF-CACHE-002` | Profil de cache inconnu |
| `SF-CACHE-003` | Clé de cache invalide |
| `SF-CACHE-004` | Namespace client absent |
| `SF-CACHE-005` | Namespace environnement absent |
| `SF-CACHE-006` | Locale de cache invalide |
| `SF-CACHE-007` | Cache Schema Version incompatible |
| `SF-CACHE-008` | Donnée non cacheable |
| `SF-CACHE-009` | Classification incompatible |
| `SF-CACHE-010` | TTL absent |
| `SF-CACHE-011` | TTL excessif |
| `SF-CACHE-012` | Taille d’entrée absente |
| `SF-CACHE-013` | Entrée trop volumineuse |
| `SF-CACHE-014` | Limite mémoire atteinte |
| `SF-CACHE-015` | Value factory échoué |
| `SF-CACHE-016` | Valeur invalide |
| `SF-CACHE-017` | Entité suivie interdite |
| `SF-CACHE-018` | Secret détecté dans une entrée |
| `SF-CACHE-019` | PII détectée dans une clé |
| `SF-CACHE-020` | Stale value interdite |
| `SF-CACHE-REGION-001` | Cache Region Epoch absent |
| `SF-CACHE-REGION-002` | Cache Region Epoch invalide |
| `SF-CACHE-REGION-003` | Incrément d’epoch manquant |
| `SF-CACHE-REGION-004` | Invalidation exécutée avant commit |
| `SF-CACHE-REGION-005` | Invalidation locale échouée |
| `SF-CACHE-REGION-006` | Signal d’invalidation invalide |
| `SF-CACHE-REGION-007` | Lag d’invalidation excessif |
| `SF-CACHE-GLOBAL-001` | Global Cache Epoch absent |
| `SF-CACHE-GLOBAL-002` | Invalidation globale requise |
| `SF-CACHE-STAMPEDE-001` | Single-flight absent |
| `SF-CACHE-STAMPEDE-002` | Trop de chargements simultanés |
| `SF-CACHE-STAMPEDE-003` | Attente de refresh expirée |
| `SF-HTTP-CACHE-001` | Cache-Control absent |
| `SF-HTTP-CACHE-002` | Réponse privée marquée publique |
| `SF-HTTP-CACHE-003` | Réponse sensible sans no-store |
| `SF-HTTP-CACHE-004` | Set-Cookie dans une réponse publique |
| `SF-HTTP-CACHE-005` | Vary non autorisé |
| `SF-HTTP-CACHE-006` | Query de cache non allowlistée |
| `SF-HTTP-CACHE-007` | Output cache interdit sur cette route |
| `SF-HTTP-CACHE-008` | ETag invalide |
| `SF-HTTP-CACHE-009` | Ressource mutable marquée immutable |
| `SF-MEDIA-CACHE-001` | URL média non immuable |
| `SF-MEDIA-CACHE-002` | Média privé marqué public |
| `SF-DISTRIBUTED-CACHE-001` | Cache distribué non configuré |
| `SF-DISTRIBUTED-CACHE-002` | Multi-instance non supporté |
| `SF-DISTRIBUTED-CACHE-003` | Sérialisation non versionnée |
| `SF-DISTRIBUTED-CACHE-004` | Namespace distribué invalide |

---

# 128. CI/CD

La CI doit exécuter :

```text
validation des régions

validation des clés

validation des TTL

tests d’isolation

tests d’invalidation

tests post-commit

tests worker

tests HTTP cache

tests ETag

tests single-flight

tests de pression mémoire

tests de suppression

tests de non-divulgation

analyse statique des accès IMemoryCache
```

---

## 128.1 Tests sans cache

Une suite doit pouvoir exécuter l’application avec :

```text
cache désactivé
```

afin de vérifier que le métier reste fonctionnel.

---

## 128.2 Tests avec cache

La même suite fonctionnelle doit produire les mêmes résultats avec le cache activé.

---

# 129. Analyse statique

La CI peut rechercher :

```text
IMemoryCache injecté directement dans un module

MemoryCacheEntryOptions sans expiration

Cache-Control public dans /admin

Vary: Cookie

clé contenant Email ou Phone

entité EF Core placée en cache

cache invalidé avant SaveChanges ou commit

TTL de plusieurs jours sur une donnée mutable
```

---

# 130. Règles pour les modules

Chaque module souhaitant mettre une donnée en cache doit déclarer :

```text
CacheRegionId

CacheProfileId

DataClassification

KeySchema

VersionSource

EpochSource

TTL

InvalidationTriggers

MaximumEntrySize

NegativeCachePolicy

Tests
```

---

## 130.1 Baseline

Une donnée non déclarée n’est pas cacheable.

---

## 130.2 Interdictions

Un module ne doit pas :

- injecter directement le cache concret ;
- cacher une entité suivie ;
- inventer un namespace ;
- ignorer l’environnement ;
- invalider avant commit ;
- utiliser une clé libre fournie par un utilisateur ;
- utiliser un TTL infini.

---

# 131. Règles pour les customizations

Une customization client suit les mêmes contrats.

---

## 131.1 Nouvelle région

Le nom utilise un namespace client explicite.

Exemple :

```text
client.sample-project.public-practitioner-index
```

---

## 131.2 Donnée personnelle

Une customization ne peut pas mettre une donnée personnelle en cache partagé sans décision explicite.

---

# 132. Règles pour LogicGate

LogicGate doit :

- maintenir les régions ;
- revoir les TTL ;
- surveiller le hit ratio ;
- surveiller la mémoire ;
- tester les invalidations ;
- maintenir le Global Epoch ;
- tester les suppressions ;
- préparer le multi-instance ;
- documenter les exceptions.

---

## 132.1 LogicGate ne doit pas

- augmenter un TTL pour masquer une requête lente ;
- cacher les permissions pendant une longue période ;
- activer un CDN sans purge ;
- mettre un cache public devant l’administration ;
- considérer Redis comme une source de vérité ;
- ignorer un lag d’invalidation ;
- conserver une donnée supprimée dans un cache durable.

---

# 133. Règles pour Codex

Codex peut :

- définir les contrats ;
- créer les régions ;
- implémenter le cache central ;
- ajouter les epochs ;
- ajouter les tests ;
- instrumenter ;
- documenter les runbooks.

---

## 133.1 Codex ne doit pas

```text
Activer un cache distribué réel

Inventer un endpoint Redis

Ajouter un secret de cache

Modifier un TTL production réel

Invalider un cache de production

Activer un CDN

Mettre des données privées dans un cache public

Supprimer un quality gate

Ajouter un cache sans tests

Appliquer une migration production
```

---

## 133.2 Décision humaine

Une décision humaine est requise pour :

- cache distribué ;
- CDN ;
- stale-on-error ;
- cache de données personnelles ;
- TTL supérieur aux profils ;
- multi-instance ;
- nouvelle sérialisation partagée ;
- invalidation globale production ;
- changement de Cache Schema Version.

---

# 134. Anti-patterns interdits

## 134.1 `IMemoryCache` dispersé

Chaque service crée ses propres clés et TTL.

---

## 134.2 Cache sans expiration

```csharp
cache.Set(key, value);
```

sans policy.

---

## 134.3 Clé personnelle

```text
lead:{email}
```

---

## 134.4 Cache d’entité EF Core

Une entité suivie est réutilisée entre plusieurs requêtes.

---

## 134.5 Invalidation avant commit

Le cache est vidé puis la transaction échoue.

---

## 134.6 Permission en cache long

Un rôle retiré continue d’autoriser l’utilisateur.

---

## 134.7 Réponse admin publique

```text
Cache-Control: public
```

sur une page de leads.

---

## 134.8 `Vary: Cookie`

Une réponse partagée crée une entrée par cookie.

---

## 134.9 TTL comme seule invalidation

Une page publiée reste ancienne pendant plusieurs heures.

---

## 134.10 Cache comme stockage

Une opération réussit uniquement si une entrée cache existe.

---

## 134.11 Gros fichiers en mémoire

Des médias complets saturent le processus web.

---

## 134.12 Multi-instance improvisé

Deux web utilisent chacun un cache local sans epochs ni signal.

---

# 135. Conséquences positives

La décision apporte :

- meilleure performance publique ;
- réduction des lectures répétitives ;
- invalidation cohérente ;
- isolation client ;
- suppression sûre ;
- mémoire bornée ;
- meilleure observabilité ;
- faible infrastructure MVP ;
- redémarrage sans risque ;
- préparation au multi-instance ;
- comportement HTTP clair ;
- stampede réduit.

---

# 136. Conséquences négatives

La décision entraîne :

- contrats de cache ;
- table des epochs ;
- clés structurées ;
- invalidations post-commit ;
- tests supplémentaires ;
- instrumentation ;
- limites de mémoire ;
- complexité pour les collections ;
- légère fenêtre d’obsolescence inter-processus.

---

# 137. Risques acceptés

## 137.1 Obsolescence inter-processus courte

Une mutation réalisée par le worker peut rester invisible pendant quelques secondes.

Réduction :

- epoch PostgreSQL ;
- signal ;
- TTL court de l’epoch ;
- exclusion des données sensibles.

---

## 137.2 Cache froid après redémarrage

Les premières requêtes peuvent être plus lentes.

Réduction :

- single-flight ;
- warming optionnel ;
- requêtes optimisées.

---

## 137.3 Éviction sous pression

Des entrées utiles peuvent disparaître.

Le système les reconstruit depuis la source de vérité.

---

## 137.4 Pas de cache distribué

Le profil MVP ne bénéficie pas d’un partage multi-instance.

Ce risque est accepté tant qu’une seule instance web reste la baseline.

---

# 138. Mesures compensatoires

- clés versionnées ;
- epochs PostgreSQL ;
- invalidation post-commit ;
- TTL ;
- Global Epoch ;
- no-store ;
- size limit ;
- single-flight ;
- negative cache court ;
- tests de suppression ;
- métriques ;
- runbooks ;
- gate multi-instance.

---

# 139. Performance

## 139.1 Objectif

Le cache doit réduire un coût mesuré.

---

## 139.2 Avant ajout

Mesurer :

- fréquence ;
- durée de requête ;
- coût de projection ;
- taille ;
- taux de modification.

---

## 139.3 Après ajout

Mesurer :

- hit ratio ;
- latence ;
- mémoire ;
- invalidations ;
- staleness ;
- charge PostgreSQL.

---

## 139.4 Suppression

Une entrée peu utilisée ou trop coûteuse à maintenir doit pouvoir être retirée.

---

# 140. Coûts

## 140.1 Coûts supplémentaires

- développement ;
- mémoire ;
- instrumentation ;
- tests ;
- maintenance ;
- future infrastructure distribuée éventuelle.

---

## 140.2 Coûts évités

- surcharge PostgreSQL ;
- latence publique ;
- reconstruction répétitive ;
- CDN prématuré ;
- incidents d’invalidation improvisée ;
- fuite par mauvaise policy HTTP.

---

# 141. Maintenabilité

Pour chaque entrée cacheable, il doit être possible de répondre :

```text
Quelle est sa région ?

Quelle est sa classification ?

Quelle est sa clé ?

Quelle Version utilise-t-elle ?

Quel Epoch utilise-t-elle ?

Quel est son TTL ?

Quelle est sa taille ?

Quelles mutations l’invalident ?

Que se passe-t-il après un rollback ?

Que se passe-t-il après un restore ?

Peut-elle être reconstruite sans cache ?

Quels tests la couvrent ?
```

---

# 142. Compatibilité

## 142.1 Cache Schema Version

Elle versionne les clés et représentations.

---

## 142.2 DTO cacheable

Une évolution incompatible exige :

- nouvelle version ;
- ou nouvelle région.

---

## 142.3 Multi-instance

Le contrat des régions et epochs reste compatible avec un cache distribué futur.

---

## 142.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

L’introduction dans un projet existant doit éviter de modifier le comportement fonctionnel.

---

# 143. Migration depuis un cache TTL simple

Le processus est :

```text
inventorier les entrées

attribuer les régions

ajouter client et environnement aux clés

ajouter Cache Schema Version

ajouter Resource Version ou Epoch

ajouter invalidation post-commit

ajouter size limits

ajouter tests

réduire les TTL excessifs
```

---

# 144. Migration depuis un cache d’entités

Le processus est :

```text
identifier les entités suivies

créer des read models immuables

retirer les références au DbContext

ajouter les versions

adapter les clés

ajouter les tests de concurrence
```

---

# 145. Migration vers un cache distribué

Le processus futur est :

```text
définir la sérialisation

versionner les payloads

sécuriser l’instance

configurer les namespaces

définir les politiques de panne

implémenter les invalidations

tester multi-instance

déployer progressivement

conserver PostgreSQL comme source
```

---

# 146. Transfert client

Le handover doit inclure :

- Cache Schema Version ;
- régions ;
- epochs ;
- TTL ;
- profils HTTP ;
- clés logiques ;
- métriques ;
- runbooks ;
- CDN éventuel ;
- cache distribué éventuel ;
- procédure d’invalidation globale.

---

## 146.1 Valeurs

Les entrées mémoire elles-mêmes ne font pas partie du handover.

---

## 146.2 Secrets

Aucun secret d’un futur cache distribué ne figure dans les manifests portables.

---

# 147. Clôture de service

La clôture doit :

```text
inventorier les caches externes

purger les contenus LogicGate éventuels

retirer les accès CDN

transférer les comptes fournisseurs

invalider les namespaces

documenter les TTL résiduels

révoquer les credentials

auditer
```

---

# 148. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- d’invalider un cache réel ;
- de modifier un TTL réel ;
- d’ajouter Redis ;
- d’activer un CDN ;
- de purger une ressource publique ;
- de modifier Caddy ;
- d’appliquer une migration ;
- de changer une policy de production.

---

# 149. Réversibilité

## 149.1 Classification

```text
élevée pour le cache mémoire

moyenne pour un CDN externe

moyenne pour un cache distribué partagé
```

---

## 149.2 Désactivation

Le cache applicatif doit pouvoir être désactivé par configuration dans un environnement de diagnostic.

---

## 149.3 Résultat

La désactivation peut réduire les performances, mais ne doit pas modifier le métier.

---

# 150. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- Cache Region IDs ;
- Cache Profile IDs ;
- descriptors ;
- keys ;
- erreurs.

---

## Étape 2 — Cache local

Implémenter :

- cache mémoire borné ;
- TTL ;
- size ;
- single-flight ;
- métriques.

---

## Étape 3 — Epochs PostgreSQL

Créer :

- Cache Region Epochs ;
- Global Cache Epoch ;
- migrations ;
- accès contrôlé.

---

## Étape 4 — Invalidation post-commit

Intégrer :

- transaction runner ;
- versions ;
- epochs ;
- hooks post-commit ;
- signaux.

---

## Étape 5 — Premiers read models

Ajouter :

- navigation ;
- pages publiques ;
- SEO ;
- configuration publique.

---

## Étape 6 — HTTP cache

Ajouter :

- Cache-Control ;
- ETags ;
- assets immuables ;
- no-store sensible ;
- Output Cache public ciblé.

---

## Étape 7 — Médias

Ajouter :

- URLs immuables ;
- hashes ;
- cache long public ;
- no-store privé.

---

## Étape 8 — Suppression et restore

Ajouter :

- invalidation privacy ;
- Global Epoch ;
- restore reconciliation.

---

## Étape 9 — Observabilité

Ajouter :

- hits ;
- misses ;
- mémoire ;
- invalidations ;
- lag ;
- alertes.

---

## Étape 10 — Quality gates

Ajouter :

- analyse statique ;
- tests d’isolation ;
- tests sans cache ;
- tests worker ;
- tests de sécurité.

---

# 151. Tâches liées proposées

```text
SF-TASK-436 — Define versioned cache-region and cache-profile contracts

SF-TASK-437 — Add the centralized SiteFactory cache abstraction

SF-TASK-438 — Add bounded in-process memory caching

SF-TASK-439 — Add per-key single-flight cache loading

SF-TASK-440 — Add PostgreSQL cache-region epochs

SF-TASK-441 — Add the global application cache epoch

SF-TASK-442 — Add post-commit cache invalidation hooks

SF-TASK-443 — Add cross-process cache invalidation signals

SF-TASK-444 — Add versioned public-page read-model caching

SF-TASK-445 — Add public-navigation and SEO caching

SF-TASK-446 — Add secure HTTP cache-control profiles

SF-TASK-447 — Add ETag and conditional public responses

SF-TASK-448 — Add immutable public-media cache URLs

SF-TASK-449 — Add cache invalidation to privacy deletion workflows

SF-TASK-450 — Add restore-time global cache invalidation

SF-TASK-451 — Add cache observability and memory-limit alerts

SF-TASK-452 — Add cache isolation, stampede and invalidation tests

SF-TASK-453 — Add cache operation and troubleshooting runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 152. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
PostgreSQL reste la source de vérité.

Le système fonctionne lorsque le cache est désactivé.

Toutes les entrées possèdent un TTL.

Le cache mémoire possède une limite.

Chaque entrée possède une taille déclarée.

Les clés incluent client et environnement.

Les clés ne contiennent aucune PII directe.

Les données localisées utilisent une clé distincte par locale.

Les ressources individuelles utilisent leur Version.

Les collections utilisent un Cache Region Epoch.

Les mutations incrémentent les epochs dans leur transaction.

Les invalidations locales ont lieu après commit.

Un rollback ne modifie ni le cache logique ni l’epoch.

Le worker peut invalider une projection utilisée par le web.

Le lag inter-processus reste dans la fenêtre documentée.

Le single-flight empêche les chargements simultanés identiques.

Une erreur de chargement n’est pas mise en cache.

Les entités EF Core suivies ne sont jamais mises en cache.

Les routes administratives utilisent no-store.

Les routes Identity utilisent no-store.

Les assets hashés utilisent immutable.

Les médias publics immuables possèdent un cache long.

Les médias privés ne sont pas mis en cache publiquement.

Une réponse contenant Set-Cookie n’entre pas dans l’Output Cache.

Une suppression rend immédiatement l’ancienne clé inutilisable.

Une restauration change le Global Cache Epoch.

Un redémarrage avec un cache vide ne casse aucune fonctionnalité.

Le multi-instance reste bloqué tant que sa cohérence n’est pas validée.

Les tests fonctionnent sur Windows et Linux.
```

---

# 153. Conditions de révision

Réexaminer cette décision si :

- plusieurs instances web deviennent nécessaires ;
- Redis ou un cache distribué est introduit ;
- un CDN devient la baseline ;
- le trafic public augmente fortement ;
- les contenus changent très fréquemment ;
- un cache multi-région est nécessaire ;
- un read replica est ajouté ;
- des pages personnalisées publiques apparaissent ;
- le stale-on-error devient nécessaire ;
- des fichiers volumineux sont servis depuis un object storage ;
- une application Blazor WebAssembly utilise davantage d’APIs cacheables ;
- les invalidations inter-processus dépassent les limites acceptées.

---

# 154. Indicateurs à suivre

- hit ratio par région ;
- misses ;
- load duration ;
- entry count ;
- taille estimée ;
- évictions ;
- invalidations ;
- Global Epoch changes ;
- invalidation lag ;
- stampede waits ;
- refresh failures ;
- stale values served ;
- output cache hits ;
- réponses `304` ;
- cache memory pressure ;
- régions sans utilisation ;
- erreurs de classification.

---

# 155. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des données privées apparaissent dans un cache partagé

les TTL augmentent régulièrement

des mutations oublient l’epoch

les clés utilisent des emails

la mémoire du web augmente continuellement

des stampedes surviennent après chaque déploiement

les pages publiques restent obsolètes

des suppressions ne purgent pas les caches externes

les permissions reposent sur un cache ancien

les hit ratios sont faibles malgré une forte complexité

le multi-instance est activé sans coordination

le cache devient nécessaire au fonctionnement métier
```

---

# 156. Conséquences observées

Cette section est append-only après mise en œuvre.

## 156.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
du cache mémoire borné,
des Cache Region Epochs
et de l’invalidation post-commit.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- région ;
- profil ;
- hit ratio ;
- taille ;
- invalidation ;
- incident ou bénéfice ;
- action proposée.

---

# 157. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- Les catégories de cache pour pages, navigation, assets et métadonnées publiques, ainsi que leurs versions, TTL et invalidations, sont définies dans le présent ADR ; aucun catalogue de pages ou design system privé n'est requis.
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- Le cache des métadonnées SEO et du JSON-LD publics relève des catégories, TTL indicatifs, versions et invalidations définis par le présent ADR ; cette référence n'impose aucun document SEO extérieur.
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
- [0006-feature-flags-and-entitlements-separation.md](0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](0007-module-composition-and-public-contracts.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](0012-runtime-configuration-and-secrets.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](0014-explicit-client-upgrades-and-version-pinning.md)
- [0016-content-and-media-storage-boundaries.md](0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](0017-background-jobs-and-reliable-side-effects.md)
- [0018-public-form-abuse-protection-and-rate-limiting.md](0018-public-form-abuse-protection-and-rate-limiting.md)
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

---

# 158. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 153 |

---

# 159. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 160. Synthèse de la décision

```text
Source de vérité
→ PostgreSQL
→ stockage média
→ configuration versionnée
→ jamais le cache

Cache MVP
→ mémoire locale bornée
→ une instance web
→ aucun Redis obligatoire
→ aucun cache dynamique Caddy

Clés
→ Cache Schema Version
→ client
→ environnement
→ région
→ locale
→ ressource
→ Version ou Epoch
→ aucune PII

Invalidation
→ modification métier
→ Version ou Cache Region Epoch
→ même transaction
→ commit
→ invalidation locale
→ signal inter-processus
→ TTL de secours

Ressources individuelles
→ Resource Version

Collections
→ Cache Region Epoch

Restore et migration majeure
→ Global Cache Epoch

TTL
→ obligatoire
→ absolu
→ borné
→ jitter possible
→ aucune entrée permanente

Stampede
→ single-flight
→ limite globale de chargements
→ aucun cache d’exception

Mémoire
→ size limit
→ taille par entrée
→ éviction acceptable
→ aucun gros média

HTTP public
→ assets hashés immutable
→ médias publics immuables
→ HTML revalidé
→ ETag et 304
→ Output Cache ciblé

HTTP sensible
→ administration no-store
→ Identity no-store
→ exports no-store
→ médias privés no-store
→ aucune réponse personnalisée partagée

Sécurité
→ aucune permission critique en cache long
→ aucune entité EF Core suivie
→ aucune clé libre
→ aucune réponse Set-Cookie dans le cache public

Privacy
→ suppression incrémente les versions
→ ancienne clé inaccessible
→ purge externe future
→ aucun cache dans les backups

Multi-instance
→ non activé avec un cache local incohérent
→ cache distribué ou coordination requis
→ PostgreSQL reste la source de vérité

Qualité
→ tests avec et sans cache
→ tests d’isolation
→ tests post-commit
→ tests worker
→ tests stampede
→ tests suppression
→ tests mémoire
```

> Un cache correct n’est pas seulement rapide. Il doit être facultatif, borné, isolé, reconstruisible et rendu obsolète au même moment logique que la donnée qu’il représente. SiteFactory utilise donc des versions et des epochs PostgreSQL afin que la suppression physique d’une entrée de cache ne soit jamais l’unique garantie de cohérence.
