---
adrId: ADR-0029
title: Standardize HTTP API contracts, Problem Details and idempotent commands
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
targetVersion: 0.1.0
---

# ADR-0029 — Contrats HTTP, Problem Details et commandes idempotentes

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Cet ADR reste le contrat cible des futures APIs métier ; il ne crée aucun
endpoint métier public. L'adaptateur actuel expose des
[health endpoints minimaux](../03-generation/generated-health-checks.md),
pas une API de données clientes, Identity ou une commande idempotente métier.
Les DTOs, Problem Details, codes, limites, permissions et exemples d'idempotence
ci-dessous restent normatifs lorsqu'une telle API sera explicitement réalisée.
Les signatures sont conceptuelles sauf indication liée aux vraies sources.
`client.sample-project.*` est un exemple fictif, sans module Business requis.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory standardise les endpoints HTTP exposés comme API au moyen de :

```text
contrats de requête et de réponse explicites

routes versionnées

méthodes HTTP cohérentes

codes de statut documentés

Problem Details pour les erreurs

codes d’erreur SiteFactory stables

contrôle de concurrence HTTP

Idempotency Keys pour les commandes rejouables

ressources Operation pour les traitements asynchrones

OpenAPI généré et vérifié
```

Les principes suivants sont obligatoires :

```text
aucune mutation par GET ou HEAD

aucune sérialisation directe d’une entité EF Core

aucune forme d’erreur JSON improvisée par endpoint

aucune stack trace dans une réponse

aucun redirect vers la page de connexion pour une route API

aucun succès HTTP lorsque l’opération métier a échoué

aucun écrasement silencieux d’une version concurrente

aucune commande à effet important rejouable
sans stratégie d’idempotence

aucune Idempotency Key considérée comme une autorisation

aucune réponse asynchrone sans Operation ID

aucun contrat public dépendant d’un message d’erreur localisé
```

Cet ADR s’applique aux endpoints HTTP explicitement exposés comme API.

Il n’impose pas de transformer tous les handlers Blazor internes en API REST.

---

## 2. Résumé

Le traitement d’une requête API suit :

```text
requête HTTP
→ sélection de version
→ limites HTTP
→ authentification
→ antiforgery lorsque nécessaire
→ autorisation
→ validation du Content-Type
→ désérialisation stricte
→ validation du contrat
→ contrôle de concurrence
→ contrôle d’idempotence
→ commande ou requête Application
→ transaction
→ réponse HTTP normalisée
```

Une erreur utilise :

```text
application/problem+json
```

avec au minimum :

```text
type

title

status

detail

instance

code

traceId

correlationId
```

Une commande idempotente suit :

```text
Idempotency-Key
→ validation
→ calcul du fingerprint de requête
→ transaction
→ création ou claim de la clé
→ exécution métier
→ persistance du résultat logique
→ commit
→ réponse
```

Une nouvelle tentative avec la même clé et la même requête retourne le résultat logique déjà obtenu.

Une même clé avec une requête différente est refusée.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Une route API retourne du JSON ou un contenu explicitement prévu.

Une erreur API utilise Problem Details.

Un code d’erreur est stable et non localisé.

Un message humain peut être localisé et évoluer.

Une réponse 2xx représente une opération réussie ou acceptée.

Une réponse 202 possède une ressource Operation consultable.

Une création réussie retourne un identifiant stable.

Une commande utilisant une version attendue
ne peut pas écraser une version plus récente.

Une Idempotency Key est liée à un périmètre précis.

La clé brute n’est pas journalisée.

Le fingerprint de requête est calculé de manière déterministe.

Une même clé et un payload différent produisent un conflit.

Un replay vérifie encore l’authentification et l’autorisation.

Une réponse rejouée ne retransmet jamais un Set-Cookie ancien.

Une erreur 5xx transitoire n’est pas mémorisée
comme succès idempotent.

Une commande asynchrone ne conserve pas
une requête HTTP ouverte jusqu’à sa terminaison.

Une réponse ne contient ni secret,
ni token, ni stack trace, ni SQL.

Un identifiant sensible n’est pas utilisé
comme Correlation ID ou Idempotency Key.

Une route publique ne révèle pas
l’existence d’une ressource inaccessible.
```

---

## 4. Contexte

Les applications SiteFactory peuvent exposer des endpoints pour :

- les formulaires publics ;
- l’administration ;
- les leads ;
- les contenus ;
- les médias ;
- les exports ;
- les opérations longues ;
- les invitations ;
- la récupération de compte ;
- les webhooks ;
- les intégrations ;
- les health checks ;
- une future API partenaire.

Sans convention commune, chaque module pourrait :

- retourner une forme d’erreur différente ;
- utiliser des statuts HTTP incohérents ;
- exposer une exception ;
- rediriger une API vers une page HTML ;
- accepter des champs inconnus ;
- sérialiser une entité complète ;
- dupliquer une commande après un timeout ;
- écraser une modification concurrente ;
- retourner `200 OK` alors qu’un job a seulement été planifié ;
- utiliser des messages localisés comme contrat machine.

Les clients doivent pouvoir distinguer de manière fiable :

```text
requête mal formée

validation fonctionnelle échouée

authentification requise

permission insuffisante

ressource absente

conflit métier

conflit de version

commande dupliquée

rate limiting

dépendance indisponible

erreur interne
```

---

## 5. Problème à résoudre

Comment fournir des contrats HTTP :

- cohérents ;
- versionnés ;
- documentés ;
- sécurisés ;
- observables ;
- compatibles avec Blazor ;
- compatibles avec des clients externes futurs ;
- résistants aux retries réseau ;
- compatibles avec les traitements asynchrones ?

La solution doit également éviter :

- les formes d’erreur ad hoc ;
- les dépendances aux textes localisés ;
- les ambiguïtés entre `400`, `409` et `500` ;
- les créations dupliquées ;
- les redirections HTML dans les APIs ;
- les payloads non bornés ;
- le mass assignment ;
- les endpoints génériques exécutant une commande arbitraire ;
- les APIs versionnées selon la version complète de l’application ;
- une documentation OpenAPI publique involontaire.

---

## 6. Portée

Cette décision couvre :

- routes API ;
- versions ;
- méthodes HTTP ;
- JSON ;
- Content-Type ;
- codes de statut ;
- Problem Details ;
- codes d’erreur ;
- validation ;
- concurrence ;
- ETags ;
- `If-Match` ;
- Idempotency Keys ;
- commandes asynchrones ;
- ressources Operation ;
- pagination ;
- filtrage ;
- antiforgery ;
- authentification ;
- autorisation ;
- rate limiting ;
- OpenAPI ;
- compatibilité ;
- observabilité ;
- tests ;
- handover.

---

## 7. Hors portée

Cette décision ne définit pas :

- une API publique commerciale complète ;
- OAuth ou OpenID Connect ;
- GraphQL ;
- gRPC ;
- WebSocket métier générique ;
- une API multi-tenant ;
- un SDK client officiel ;
- le cache HTTP complet ;
- les contrats détaillés de chaque module ;
- les règles métier des leads ou contenus ;
- un API Gateway obligatoire ;
- une politique universelle de webhooks, déjà couverte par l’ADR-0025.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Contrats prévisibles**
2. **Erreurs exploitables par machine**
3. **Aucune duplication après retry**
4. **Aucun écrasement concurrent**
5. **Absence de divulgation**
6. **Compatibilité HTTP**
7. **Traitements asynchrones explicites**
8. **Contrats versionnés**
9. **Validation stricte des commandes**
10. **Documentation OpenAPI**
11. **Compatibilité Blazor et navigateurs**
12. **Observabilité**
13. **Testabilité**
14. **Évolution additive**
15. **Faible dépendance à une bibliothèque**
16. **Maintenabilité**

---

## 9. Contraintes

La décision doit respecter :

```text
ASP.NET Core

JSON UTF-8

System.Text.Json

Problem Details

codes d’erreur SiteFactory

routes versionnées

Idempotency Keys persistées dans PostgreSQL

concurrence optimiste

outbox transactionnelle

antiforgery pour les APIs same-origin avec cookie

rate limiting

OpenTelemetry

OpenAPI

compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- la majorité des APIs sont same-origin ;
- l’administration utilise un cookie sécurisé ;
- une future API externe pourra utiliser une authentification différente ;
- les commandes métier utilisent la couche Application ;
- les commandes importantes utilisent PostgreSQL ;
- les résultats idempotents peuvent être représentés de manière bornée ;
- les gros traitements utilisent les jobs ;
- les clients peuvent générer des identifiants opaques ;
- la majorité des réponses sont en JSON.

---

# 11. Définitions

## 11.1 API endpoint

Route HTTP possédant un contrat machine explicite.

---

## 11.2 API version

Version majeure d’un ensemble de contrats HTTP.

---

## 11.3 Problem Details

Structure normalisée décrivant une erreur HTTP.

---

## 11.4 Error Code

Identifiant SiteFactory stable d’une erreur.

Exemple :

```text
SF-CONCURRENCY-002
```

---

## 11.5 Command

Requête demandant une modification d’état ou un effet.

---

## 11.6 Query

Requête en lecture seule.

---

## 11.7 Idempotency Key

Clé opaque identifiant une intention de commande précise.

---

## 11.8 Request Fingerprint

Empreinte déterministe du contrat et du contenu significatif de la requête.

---

## 11.9 Replay

Nouvelle réception d’une commande portant la même Idempotency Key.

---

## 11.10 Operation Resource

Ressource représentant l’état d’un traitement asynchrone.

---

## 11.11 Conditional Request

Requête HTTP utilisant notamment :

```text
ETag

If-Match
```

afin de contrôler la version ciblée.

---

# 12. Option A — Contrats ad hoc par endpoint

## 12.1 Description

Chaque endpoint décide :

- de ses statuts ;
- de son JSON ;
- de ses erreurs ;
- de ses retries.

---

## 12.2 Avantages

- rapide à implémenter ;
- souple ;
- peu d’infrastructure ;
- adapté à de petits prototypes.

---

## 12.3 Inconvénients

- incohérence ;
- duplication ;
- clients fragiles ;
- erreurs difficiles à traiter ;
- tests dispersés ;
- maintenance coûteuse.

---

## 12.4 Risques

- `200` avec `{ success: false }` ;
- message d’erreur utilisé comme code ;
- stack trace exposée ;
- commandes dupliquées ;
- statut HTTP différent selon le module.

---

## 12.5 Réversibilité

```text
moyenne
```

Les clients existants doivent être migrés.

---

# 13. Option B — Exceptions applicatives retournées directement

## 13.1 Description

Les exceptions sont sérialisées ou transformées en erreurs génériques.

---

## 13.2 Avantages

- peu de mapping ;
- diagnostic simple ;
- code réduit ;
- propagation naturelle.

---

## 13.3 Inconvénients

- couplage à l’implémentation ;
- divulgation ;
- stack traces ;
- messages instables ;
- statuts incohérents ;
- absence de contrat.

---

## 13.4 Risques

- noms de classes exposés ;
- SQL ou chemin local visible ;
- erreur métier transformée en 500 ;
- dépendance des clients aux textes.

---

## 13.5 Réversibilité

```text
élevée
```

---

# 14. Option C — REST générique sans commandes explicites

## 14.1 Description

Toutes les actions sont modélisées uniquement par des opérations CRUD sur des ressources.

---

## 14.2 Avantages

- routes uniformes ;
- faible nombre d’endpoints ;
- conventions simples ;
- bon support d’outillage.

---

## 14.3 Inconvénients

- actions métier complexes mal représentées ;
- archivage ou export ambigu ;
- step-up difficile à exprimer ;
- effets asynchrones cachés ;
- contrats de transition faibles.

---

## 14.4 Risques

- `PATCH status=archived` contournant une confirmation ;
- effets secondaires invisibles ;
- idempotence mal définie ;
- audit insuffisant.

---

## 14.5 Réversibilité

```text
élevée
```

---

# 15. Option D — Ressources et commandes HTTP explicites

## 15.1 Description

Les lectures et CRUD simples utilisent des ressources.

Les actions métier utilisent des commandes explicites, versionnées et idempotentes lorsque nécessaire.

---

## 15.2 Avantages

- contrats clairs ;
- erreurs cohérentes ;
- concurrence explicite ;
- step-up ;
- audit ;
- opérations asynchrones ;
- idempotence ;
- bonne documentation.

---

## 15.3 Inconvénients

- davantage de contrats ;
- infrastructure de mapping ;
- stockage d’idempotence ;
- tests supplémentaires ;
- discipline de versionnement.

---

## 15.4 Risques

- multiplication excessive d’endpoints ;
- commandes trop techniques ;
- mauvaise définition des périmètres d’idempotence.

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

| Critère | Poids | Ad hoc | Exceptions | CRUD générique | Contrats explicites |
|---|---:|---:|---:|---:|---:|
| Cohérence | 5 | 1 | 2 | 4 | 5 |
| Sécurité | 5 | 2 | 1 | 3 | 5 |
| Idempotence | 5 | 1 | 1 | 3 | 5 |
| Concurrence | 5 | 2 | 1 | 3 | 5 |
| Actions métier | 5 | 3 | 2 | 2 | 5 |
| Documentation | 4 | 2 | 2 | 4 | 5 |
| Évolution | 4 | 2 | 2 | 4 | 5 |
| Observabilité | 4 | 2 | 2 | 4 | 5 |
| Maintenabilité | 5 | 1 | 2 | 4 | 5 |
| Testabilité | 4 | 2 | 2 | 4 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons que chaque endpoint API possède :

```text
Route ID

API Version

Operation ID

méthode HTTP

contrat de requête

contrat de réponse

statuts possibles

codes d’erreur

permission

profil de concurrence

profil d’idempotence

profil de cache

limites
```

Les contrats sont définis dans la couche Application ou dans un package de contrats dédié.

Les types d’infrastructure et entités EF Core ne deviennent jamais des contrats HTTP publics.

---

# 18. Familles de routes

Familles initiales :

```text
/api/v1/public/...

/api/v1/admin/...

/api/v1/operations/...

/api/v1/webhooks/...

/api/v1/security/...
```

---

## 18.1 Pages HTML

Les pages Razor ou Blazor n’utilisent pas nécessairement ce préfixe.

---

## 18.2 Health checks

Les health checks utilisent des routes techniques distinctes.

---

## 18.3 Webhooks

Les webhooks suivent également leurs contrats spécialisés définis dans l’ADR-0025.

---

# 19. Versionnement des routes

La baseline utilise un segment d’URL majeur :

```text
/api/v1
```

---

## 19.1 Version majeure

Une modification incompatible crée :

```text
/api/v2
```

---

## 19.2 Version applicative

La version API n’est pas :

```text
/api/1.4.3
```

La version fonctionnelle de l’application évolue indépendamment.

---

## 19.3 Versions concurrentes

Plusieurs versions peuvent coexister temporairement pendant une migration.

---

# 20. Convention de routes

Les routes utilisent :

- noms en minuscules ;
- tirets lorsque nécessaire ;
- noms pluriels pour les collections ;
- identifiants opaques ;
- aucun email ou secret dans le path.

Exemples :

```text
/api/v1/admin/leads/{leadId}

/api/v1/admin/leads/{leadId}/archive

/api/v1/admin/exports

/api/v1/operations/{operationId}
```

---

## 20.1 Commandes explicites

Une action métier complexe peut utiliser :

```text
POST /{resource}/{id}/{command}
```

---

## 20.2 Endpoint générique interdit

Un endpoint tel que :

```text
POST /api/v1/commands
```

avec un nom de commande et un payload arbitraires est interdit.

---

# 21. Méthodes HTTP

## 21.1 GET

Réservé aux lectures sans mutation métier.

---

## 21.2 HEAD

Même sémantique que GET sans corps de réponse.

---

## 21.3 POST

Utilisé pour :

- création ;
- commande métier ;
- lancement d’opération ;
- recherche complexe sensible en lecture seule.

---

## 21.4 PUT

Utilisé pour le remplacement complet idempotent d’une ressource lorsque ce contrat est réellement adapté.

---

## 21.5 PATCH

Utilisé pour une modification partielle explicitement définie.

---

## 21.6 DELETE

Utilisé pour une suppression de ressource lorsque le contrat de cycle de vie le permet.

---

## 21.7 OPTIONS

Ne produit aucun effet métier.

---

## 21.8 Interdictions

```text
TRACE

CONNECT
```

ne sont pas exposés par l’application.

---

# 22. Aucune mutation par GET

Les actions suivantes ne doivent jamais utiliser GET :

- logout ;
- archivage ;
- restauration ;
- suppression ;
- publication ;
- invitation ;
- export ;
- retry de job ;
- activation de feature ;
- changement de rôle.

---

## 22.1 Préchargement

Les navigateurs, crawlers et scanners peuvent exécuter des GET automatiquement.

---

# 23. Content-Types

Les APIs JSON acceptent :

```text
application/json
```

avec encodage UTF-8.

Les erreurs retournent :

```text
application/problem+json
```

---

## 23.1 Type absent

Un body nécessitant JSON sans Content-Type correct est refusé.

---

## 23.2 Multipart

```text
multipart/form-data
```

n’est accepté que par les endpoints d’upload déclarés.

---

## 23.3 XML

XML n’appartient pas à la baseline.

---

## 23.4 Requête compressée

Les corps HTTP compressés ne sont pas acceptés par défaut.

Une exception nécessite des limites sur la taille décompressée.

---

# 24. Convention JSON

Les contrats utilisent :

```text
camelCase

UTF-8

enums textuelles

dates ISO 8601

identifiants opaques

ordre des propriétés non significatif
```

---

## 24.1 Casse

```json
{
  "leadId": "01J...",
  "createdAtUtc": "2026-07-29T15:00:00Z"
}
```

---

## 24.2 Noms internes

Les noms de colonnes PostgreSQL ou propriétés privées ne doivent pas dicter le contrat.

---

# 25. Propriétés inconnues

Les contrats d’écriture stables refusent par défaut les propriétés inconnues.

---

## 25.1 Objectifs

- détecter les fautes ;
- empêcher le mass assignment ;
- éviter les clients croyant modifier un champ ignoré ;
- maintenir un contrat précis.

---

## 25.2 Réponses

Les clients lisant les réponses doivent ignorer les nouvelles propriétés inconnues.

---

## 25.3 Asymétrie volontaire

```text
request strict
response extensible
```

---

# 26. DTOs explicites

Chaque endpoint utilise un type de requête dédié.

---

## 26.1 Interdictions

Ne pas accepter directement :

- entité EF Core ;
- agrégat Domain ;
- dictionnaire libre ;
- `object` ;
- `JsonElement` générique sans schéma ;
- modèle complet avec propriétés internes.

---

## 26.2 Commandes

Les DTOs ne contiennent que les propriétés que l’appelant peut fournir.

---

# 27. Identifiants

Les identifiants exposés sont :

- opaques ;
- stables ;
- bornés ;
- validés.

---

## 27.1 Séquentiels

Les identifiants séquentiels peuvent être évités lorsqu’ils facilitent l’énumération.

---

## 27.2 Secrets

Un ID n’est pas considéré comme un secret ou une autorisation.

---

# 28. Dates et heures

Les timestamps utilisent UTC.

Exemple :

```text
2026-07-29T15:00:00.000Z
```

---

## 28.1 Fuseau horaire

Une date métier locale utilise un contrat distinct indiquant son fuseau ou sa nature civile.

---

## 28.2 Format local

Les formats dépendant de la locale sont interdits dans JSON.

---

# 29. Montants monétaires

Un montant utilise un objet structuré.

Exemple :

```json
{
  "amount": "6400.00",
  "currency": "EUR"
}
```

---

## 29.1 Précision

Le montant canonique n’utilise pas un type flottant binaire.

---

# 30. Enums

Les enums sont exposées par identifiants textuels stables.

Exemple :

```text
archived
```

et non :

```text
4
```

---

## 30.1 Valeur inconnue

Un client doit pouvoir détecter une valeur inconnue.

Une valeur future ne doit pas être mappée arbitrairement vers la première valeur de l’enum.

---

# 31. Valeurs nulles

Chaque propriété définit explicitement si :

- elle est obligatoire ;
- elle est nullable ;
- son absence signifie « ne pas modifier » ;
- `null` signifie « effacer ».

---

## 31.1 PATCH

La distinction entre :

```text
propriété absente

propriété présente avec null
```

doit être représentable.

---

# 32. Limites des requêtes

Chaque endpoint déclare :

```text
MaximumBodyBytes

MaximumCollectionItems

MaximumStringLength

MaximumJsonDepth

AllowedContentTypes
```

---

## 32.1 Profils initiaux indicatifs

```text
Public form JSON :
64 KiB

Admin JSON ordinaire :
256 KiB

Batch :
profil explicite

Upload :
contrat séparé
```

---

## 32.2 Trop volumineux

Réponse :

```text
413 Payload Too Large
```

---

# 33. Réponses réussies

Une réponse réussie utilise un statut cohérent avec l’opération.

---

## 33.1 Pas de wrapper universel

Le format suivant n’est pas imposé :

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

Les statuts HTTP et contrats typés portent déjà cette information.

---

# 34. `200 OK`

Utilisé lorsque :

- une lecture retourne une représentation ;
- une mise à jour retourne la ressource ;
- une commande synchrone retourne un résultat.

---

# 35. `201 Created`

Utilisé lorsqu’une nouvelle ressource est créée.

La réponse inclut :

```text
Location
```

et un identifiant stable.

Exemple :

```text
Location: /api/v1/admin/leads/01J...
```

---

# 36. `202 Accepted`

Utilisé lorsqu’un traitement est accepté mais non terminé.

La réponse inclut :

```text
Operation ID

Location

état initial
```

Exemple :

```json
{
  "operationId": "01J...",
  "status": "queued",
  "statusUrl": "/api/v1/operations/01J..."
}
```

---

# 37. `204 No Content`

Utilisé lorsqu’une opération réussit sans représentation nécessaire.

---

## 37.1 Corps

Une réponse `204` ne contient aucun corps.

---

# 38. `400 Bad Request`

Utilisé pour :

- JSON invalide ;
- binding impossible ;
- format d’identifiant invalide ;
- syntaxe de requête invalide ;
- structure non interprétable.

---

## 38.1 Validation métier

Une requête syntaxiquement correcte mais sémantiquement invalide utilise plutôt `422`.

---

# 39. `401 Unauthorized`

Signifie :

```text
authentification absente ou invalide
```

---

## 39.1 Pas de redirect API

Une route API ne retourne pas :

```text
302 vers /login
```

---

## 39.2 Cookie

Le handler d’authentification distingue les routes HTML et API.

---

# 40. `403 Forbidden`

Signifie :

```text
acteur authentifié mais non autorisé
```

---

## 40.1 Ressource sensible

Une route peut retourner `404` plutôt que `403` lorsqu’il est nécessaire de ne pas révéler l’existence de la ressource.

---

# 41. `404 Not Found`

Utilisé lorsque :

- route absente ;
- ressource absente ;
- ressource volontairement masquée à l’acteur.

---

## 41.1 Énumération

Le contrat ne doit pas permettre de distinguer facilement une ressource existante mais inaccessible.

---

# 42. `405 Method Not Allowed`

Utilisé lorsque la route existe mais pas pour la méthode demandée.

La réponse inclut le header :

```text
Allow
```

---

# 43. `406 Not Acceptable`

Peut être utilisé lorsque le client exige une représentation non supportée.

---

# 44. `409 Conflict`

Utilisé notamment pour :

- conflit d’état métier ;
- conflit de concurrence avec version dans le payload ;
- Idempotency Key réutilisée avec une autre requête ;
- opération incompatible avec l’état courant ;
- ressource duplicate non représentée uniquement par une contrainte de validation.

---

# 45. `412 Precondition Failed`

Utilisé lorsqu’une requête contenant :

```text
If-Match
```

cible une version qui n’est plus courante.

---

## 45.1 Différence avec `409`

```text
If-Match HTTP
→ 412

Expected Version dans une commande métier
→ 409
```

---

# 46. `428 Precondition Required`

Utilisé lorsqu’une opération exige :

```text
If-Match
```

mais que le header est absent.

---

# 47. `415 Unsupported Media Type`

Utilisé lorsque le Content-Type n’est pas autorisé.

---

# 48. `422 Unprocessable Content`

Utilisé lorsque :

- JSON valide ;
- contrat compris ;
- validation fonctionnelle échouée.

Exemples :

- email invalide ;
- champ obligatoire vide ;
- transition non conforme à une règle de saisie ;
- longueur métier dépassée.

---

# 49. `429 Too Many Requests`

Utilisé lorsque le rate limiting bloque la requête.

La réponse peut inclure :

```text
Retry-After
```

---

# 50. `500 Internal Server Error`

Utilisé pour une erreur inattendue non divulguée.

---

## 50.1 Corps

Le corps reste générique et fournit :

- code stable ;
- Trace ID ;
- Correlation ID.

---

# 51. `502 Bad Gateway`

Peut être utilisé lorsqu’un proxy ou provider intermédiaire retourne une réponse invalide.

---

# 52. `503 Service Unavailable`

Utilisé lorsque :

- service temporairement indisponible ;
- dépendance obligatoire indisponible ;
- maintenance ;
- kill switch de disponibilité ;
- base non accessible.

---

## 52.1 Retry

La réponse peut inclure `Retry-After`.

---

# 53. `504 Gateway Timeout`

Utilisé lorsqu’une dépendance synchrone dépasse son délai.

---

## 53.1 Traitement asynchrone

Une opération longue ne doit pas produire un `504` uniquement parce qu’elle aurait dû être traitée par un job.

---

# 54. Problem Details

Toutes les erreurs API utilisent un document de type :

```text
application/problem+json
```

---

## 54.1 Exemple

```json
{
  "type": "urn:sitefactory:problem:sf-concurrency-002",
  "title": "La ressource a été modifiée.",
  "status": 409,
  "detail": "Rechargez la ressource avant de recommencer.",
  "instance": "/api/v1/admin/leads/01JABC",
  "code": "SF-CONCURRENCY-002",
  "traceId": "7f944ee51d4f430e",
  "correlationId": "01JXYZ",
  "expectedVersion": 6,
  "currentVersion": 7
}
```

---

# 55. Champ `type`

Le champ `type` utilise un identifiant stable.

Convention :

```text
urn:sitefactory:problem:{error-code-normalized}
```

Exemple :

```text
urn:sitefactory:problem:sf-concurrency-002
```

---

## 55.1 About blank

Une erreur sans type spécifique peut utiliser un type générique, mais les erreurs fonctionnelles importantes possèdent leur propre type.

---

# 56. Champ `title`

`title` contient un résumé humain court.

---

## 56.1 Localisation

Le titre peut être localisé.

Il ne constitue pas un identifiant machine.

---

# 57. Champ `status`

`status` reprend le code de statut HTTP.

---

# 58. Champ `detail`

`detail` explique l’erreur de manière utile sans exposer l’implémentation.

---

## 58.1 Non contractuel

Le texte peut évoluer entre deux versions mineures.

---

## 58.2 Données sensibles

Le détail ne contient pas :

- valeur secrète ;
- SQL ;
- stack trace ;
- chemin local ;
- données d’une autre ressource ;
- message complet d’un provider.

---

# 59. Champ `instance`

`instance` identifie la requête ou la ressource concernée de manière sûre.

---

## 59.1 Query string

La query string sensible est retirée.

---

## 59.2 Alternative

Une route peut utiliser un Request ID opaque plutôt que le chemin complet.

---

# 60. Champ `code`

Extension obligatoire :

```json
{
  "code": "SF-CONCURRENCY-002"
}
```

---

## 60.1 Stabilité

Le code reste stable tant que la signification fonctionnelle reste identique.

---

## 60.2 Réutilisation interdite

Un code retiré ne doit pas être réutilisé avec une autre signification.

---

# 61. Trace et corrélation

Extensions recommandées :

```text
traceId

correlationId
```

---

## 61.1 `traceId`

Correspond au contexte de trace OpenTelemetry.

---

## 61.2 `correlationId`

Permet de rapprocher plusieurs actions fonctionnelles.

---

## 61.3 Non-secret

Aucune de ces valeurs ne doit encoder une donnée personnelle.

---

# 62. Erreurs de validation

Une erreur `422` peut inclure :

```json
{
  "type": "urn:sitefactory:problem:sf-validation-001",
  "title": "Certaines données sont invalides.",
  "status": 422,
  "code": "SF-VALIDATION-001",
  "errors": {
    "/email": [
      {
        "code": "SF-VALIDATION-EMAIL-INVALID",
        "message": "L’adresse email n’est pas valide."
      }
    ]
  }
}
```

---

## 62.1 Chemins

Les clés utilisent des chemins JSON Pointer ou une convention stable équivalente.

---

## 62.2 Messages

Les messages sont localisables.

Les codes restent stables.

---

## 62.3 Valeur rejetée

La valeur saisie n’est pas recopiée par défaut dans la réponse.

---

# 63. Localisation

Les APIs peuvent utiliser :

```text
Accept-Language
```

pour choisir les messages humains.

---

## 63.1 Langue par défaut

Le profil initial utilise :

```text
fr-FR
```

---

## 63.2 Contrats

Les clients doivent utiliser :

```text
code
```

et non les textes localisés.

---

## 63.3 Header

La réponse peut indiquer :

```text
Content-Language
```

---

# 64. Gestion globale des exceptions

Un middleware ou handler global transforme les exceptions connues.

---

## 64.1 Catégories

- validation ;
- autorisation ;
- not found ;
- conflit ;
- concurrence ;
- idempotence ;
- rate limit ;
- timeout ;
- dépendance ;
- erreur inattendue.

---

## 64.2 Endpoint local

Un endpoint ne doit pas répéter manuellement toute la logique de mapping.

---

## 64.3 Exception inconnue

Elle devient :

```text
500
SF-HTTP-500
```

ou un code stable équivalent.

---

# 65. Mapping des erreurs

| Catégorie | Statut principal |
|---|---:|
| JSON invalide | `400` |
| Authentification absente | `401` |
| Permission absente | `403` |
| Ressource absente ou masquée | `404` |
| Méthode interdite | `405` |
| Conflit métier | `409` |
| Concurrence métier | `409` |
| `If-Match` invalide | `412` |
| `If-Match` requis | `428` |
| Validation fonctionnelle | `422` |
| Payload trop volumineux | `413` |
| Content-Type invalide | `415` |
| Rate limiting | `429` |
| Dépendance temporairement indisponible | `503` |
| Timeout de dépendance | `504` |
| Erreur inattendue | `500` |

---

# 66. Validation

La validation possède plusieurs niveaux :

```text
syntaxe HTTP

désérialisation JSON

validation structurelle

validation fonctionnelle

autorisation

invariants transactionnels
```

---

## 66.1 Ordre

Les validations peu coûteuses sont exécutées avant l’ouverture d’une transaction.

---

## 66.2 Invariants

Une validation préalable ne remplace pas une contrainte PostgreSQL.

---

# 67. Correlation ID entrant

Un client peut fournir un Correlation ID selon le profil.

---

## 67.1 Validation

La valeur est :

- bornée ;
- limitée à un alphabet approuvé ;
- sans retour à la ligne ;
- sans donnée personnelle attendue.

---

## 67.2 Valeur invalide

Une nouvelle valeur serveur est générée.

---

## 67.3 Logs

La valeur est structurée, jamais concaténée librement dans un message.

---

# 68. Trace Context

Le système utilise le contexte W3C :

```text
traceparent
```

lorsqu’il est valide.

---

## 68.1 Baggage

Le baggage entrant est filtré.

Aucune clé non approuvée ou donnée personnelle n’est propagée automatiquement.

---

# 69. ETag

Les ressources versionnées peuvent retourner :

```text
ETag: "v7"
```

---

## 69.1 Source

L’ETag est dérivé de la Version métier.

---

## 69.2 Pas de secret

Il ne contient pas de donnée personnelle.

---

## 69.3 Réponse de mise à jour

Une mise à jour réussie retourne le nouvel ETag lorsque la représentation est concernée.

---

# 70. `If-Match`

Les modifications critiques de ressources peuvent exiger :

```text
If-Match
```

---

## 70.1 Wildcard

```text
If-Match: *
```

n’est accepté que si le contrat autorise explicitement « ressource existante, toute version ».

---

## 70.2 Baseline

Les suppressions et remplacements sensibles utilisent une version exacte.

---

# 71. Version dans le payload

Les commandes métier Blazor peuvent utiliser :

```json
{
  "expectedVersion": 7
}
```

---

## 71.1 Cohérence

Un endpoint ne doit pas accepter simultanément des versions différentes dans :

- `If-Match` ;
- body.

Si les deux sont présents, elles doivent correspondre.

---

# 72. Idempotence des commandes

Les commandes susceptibles d’être rejouées utilisent une stratégie d’idempotence.

---

## 72.1 Cas obligatoires

- création depuis un formulaire public ;
- création d’un export ;
- lancement d’une opération longue ;
- webhook entrant ;
- création d’un effet financier futur ;
- commande exposée à un uncertain commit ;
- commande explicitement déclarée retryable.

---

## 72.2 Cas facultatifs

Une petite mise à jour versionnée et synchrone peut s’appuyer sur :

- version attendue ;
- résultat déterministe ;

sans Idempotency Key supplémentaire si le contrat le justifie.

---

# 73. Header `Idempotency-Key`

Les clients API utilisent :

```text
Idempotency-Key
```

---

## 73.1 Format

La clé est :

- opaque ;
- aléatoire ;
- entre 16 et 128 caractères ;
- ASCII borné ;
- sans espace de contrôle.

---

## 73.2 Recommandation

Un UUID ou ULID aléatoire convient.

---

## 73.3 Interdictions

Ne pas utiliser comme clé :

- email ;
- téléphone ;
- nom ;
- message ;
- secret ;
- token d’accès.

---

# 74. Périmètre d’une Idempotency Key

Le périmètre comprend :

```text
client

environnement

Operation ID

méthode HTTP

acteur ou contexte anonyme autorisé
```

---

## 74.1 Même clé sur une autre opération

Elle peut être considérée comme une intention distincte si le périmètre inclut l’Operation ID.

---

## 74.2 Même acteur

Une clé créée par un acteur authentifié ne peut pas être rejouée par un autre acteur.

---

# 75. Stockage de la clé

La clé brute n’est pas stockée.

Le système conserve :

```text
hash de la clé
```

---

## 75.1 Entropie

Cette stratégie suppose une clé aléatoire à forte entropie.

---

## 75.2 Logs

Les logs peuvent contenir un identifiant technique tronqué ou le hash borné, jamais la clé brute.

---

# 76. Fingerprint de requête

Le fingerprint comprend conceptuellement :

```text
CanonicalizationVersion

OperationId

HTTP Method

Route contract

Canonical JSON body

relevant conditional headers
```

---

## 76.1 Exclusions

Ne pas inclure :

- header de trace ;
- date de réception ;
- cookie ;
- Authorization ;
- Correlation ID.

---

## 76.2 Canonicalisation

La canonicalisation est :

- déterministe ;
- versionnée ;
- indépendante de l’ordre des propriétés JSON ;
- indépendante des espaces ;
- indépendante de la locale.

---

# 77. Modèle d’idempotence

Une entrée doit pouvoir conserver :

```text
IdempotencyRecordId

Scope

KeyHash

RequestFingerprint

CanonicalizationVersion

OperationId

ActorId

CreatedAtUtc

CompletedAtUtc

ExpiresAtUtc

ResultType

StatusCode

ResponseMediaType

ReplayBody

ReplayHeaders

ResourceId

AsyncOperationId
```

---

## 77.1 Données sensibles

Le Replay Body reste :

- borné ;
- minimisé ;
- dépourvu de secret ;
- soumis à rétention.

---

## 77.2 Gros résultat

Un fichier ou gros résultat n’est pas copié dans l’entrée.

Conserver :

- Resource ID ;
- Operation ID ;
- statut logique.

---

# 78. Écriture atomique

Le flux recommandé est :

```text
ouvrir la transaction

tenter d’insérer le claim idempotent

exécuter la commande

persister les données métier

persister audit et outbox

persister le résultat idempotent

commit
```

---

## 78.1 Rollback

En cas de rollback :

- aucune ressource ;
- aucune outbox ;
- aucun audit métier réussi ;
- aucun résultat idempotent réussi.

---

## 78.2 Uncertain commit

Une nouvelle tentative avec la même clé détermine si le résultat a été commit.

---

# 79. Requêtes concurrentes avec la même clé

Une contrainte unique empêche deux exécutions logiques.

---

## 79.1 Première requête

Elle possède le claim transactionnel.

---

## 79.2 Seconde requête

Elle :

- attend brièvement ;
- récupère le résultat après commit ;
- ou reçoit une erreur `in-progress` bornée.

---

## 79.3 Attente

L’attente suit le `lock_timeout`.

---

# 80. Replay réussi

Si :

```text
même périmètre
+ même clé
+ même fingerprint
+ résultat terminé
```

le serveur retourne le résultat logique déjà persisté.

---

## 80.1 Header indicatif

La réponse peut inclure :

```text
Idempotency-Replayed: true
```

---

## 80.2 Statut

Le statut reste cohérent avec la réponse initiale.

---

# 81. Headers rejouables

Seuls des headers allowlistés sont persistés.

Exemples :

```text
Location

ETag

Retry-After lorsque pertinent
```

---

## 81.1 Headers interdits

Ne jamais rejouer :

```text
Set-Cookie

Authorization

Date historique comme contrat

headers de session

token temporaire expiré
```

---

# 82. Authentification au replay

Un replay exige toujours :

- authentification valide ;
- session non révoquée ;
- permission actuelle ;
- même acteur ou scope compatible.

---

## 82.1 Ancienne autorisation

Un résultat mémorisé ne doit pas contourner une révocation de permission.

---

## 82.2 Ressource devenue inaccessible

Le serveur peut refuser de restituer la représentation mémorisée tout en indiquant que la commande a déjà été traitée.

---

# 83. Clé réutilisée avec un autre payload

Si le fingerprint diffère :

```text
409 Conflict
SF-IDEMPOTENCY-003
```

---

## 83.1 Aucun remplacement

La nouvelle requête ne remplace pas l’ancienne entrée.

---

## 83.2 Client

Le client doit générer une nouvelle clé pour une nouvelle intention.

---

# 84. Validation et Idempotency Key

Les erreurs suivantes ne sont pas persistées par défaut comme résultat idempotent :

- JSON invalide ;
- authentification absente ;
- permission absente ;
- validation fonctionnelle ;
- Content-Type invalide ;
- payload trop volumineux.

---

## 84.1 Raisons

Ces erreurs se produisent avant la commande métier et peuvent être corrigées.

---

# 85. Erreurs transitoires

Une erreur :

- `500` ;
- `502` ;
- `503` ;
- `504` ;
- deadlock ;
- serialization failure ;

ne devient pas un succès mémorisé.

---

## 85.1 Retry

Le client peut rejouer la même clé.

---

## 85.2 Commit incertain

Le serveur vérifie d’abord l’entrée persistée.

---

# 86. Échecs métier déterministes

Un profil peut mémoriser un résultat métier terminal lorsqu’il est important que le replay retourne exactement la même décision.

---

## 86.1 Baseline

Les conflits dépendant d’un état susceptible d’évoluer ne sont pas mémorisés par défaut.

---

## 86.2 Exemple terminal

Une commande déjà définitivement consommée peut conserver :

```text
already-completed
```

comme résultat stable.

---

# 87. Durée de conservation des clés

Baseline initiale :

```text
24 heures
```

pour les commandes ordinaires.

---

## 87.1 Opérations longues

La durée doit au minimum couvrir la période pendant laquelle le client peut raisonnablement rejouer la commande.

---

## 87.2 Export

Une commande d’export peut conserver son résultat aussi longtemps que la ressource Operation correspondante.

---

## 87.3 Pas de conservation indéfinie

Les entrées suivent une politique de rétention.

---

# 88. Nettoyage des entrées

Un job idempotent supprime les entrées expirées.

---

## 88.1 Résultat métier

La suppression de l’entrée d’idempotence ne supprime jamais la ressource métier créée.

---

## 88.2 Ancienne clé

Après expiration, une clé réutilisée peut être traitée comme nouvelle.

Les clients ne doivent pas dépendre d’une conservation au-delà du contrat.

---

# 89. Formulaires publics

Les formulaires server-rendered peuvent utiliser :

```text
Submission ID
```

défini dans l’ADR-0018.

---

## 89.1 Mapping

Le Submission ID joue le rôle d’Idempotency Key dans le périmètre du formulaire.

---

## 89.2 Header

Le navigateur n’est pas obligé de fournir un header lorsque le contrat HTML utilise un champ signé.

---

# 90. Commandes asynchrones

Une commande longue utilise :

```text
Idempotency Key
→ création atomique d’une Operation
→ outbox ou job
→ 202 Accepted
```

---

## 90.1 Replay

Le replay retourne le même Operation ID.

---

## 90.2 Pas de second job

Une même intention ne crée pas un second job logique.

---

# 91. Uncertain commit

Une coupure réseau peut survenir après le commit.

---

## 91.1 Client

Le client rejoue avec la même Idempotency Key.

---

## 91.2 Serveur

Le serveur retourne :

- la ressource créée ;
- le même Operation ID ;
- ou le résultat mémorisé.

---

## 91.3 Nouvelle clé

Une nouvelle clé pourrait créer un doublon et ne doit pas être utilisée pour un simple retry.

---

# 92. La clé n’est pas une autorisation

Connaître une Idempotency Key ne donne aucun droit sur :

- la ressource ;
- l’Operation ;
- le résultat ;
- le téléchargement.

---

## 92.1 Contrôles

Tous les contrôles d’identité et permission sont réexécutés.

---

# 93. Ressource Operation

Les opérations longues utilisent une ressource HTTP dédiée.

Route :

```text
GET /api/v1/operations/{operationId}
```

---

## 93.1 Contrat

```json
{
  "operationId": "01J...",
  "operationType": "data-export",
  "status": "running",
  "createdAtUtc": "2026-07-29T15:00:00Z",
  "updatedAtUtc": "2026-07-29T15:00:05Z",
  "progress": {
    "current": 2,
    "total": 5
  }
}
```

---

## 93.2 Informations sensibles

La ressource ne contient pas :

- payload complet ;
- secret ;
- chemin de fichier ;
- données du lead ;
- stack trace.

---

# 94. États des opérations

Valeurs recommandées :

```text
queued

running

succeeded

failed

cancel-requested

cancelled

expired
```

---

## 94.1 État terminal

```text
succeeded

failed

cancelled

expired
```

---

# 95. Résultat d’une opération

Une opération réussie peut fournir :

```text
resultResourceUrl
```

ou une petite représentation de résultat.

---

## 95.1 Téléchargement

Un export utilise un mécanisme de téléchargement autorisé distinct.

---

## 95.2 Erreur

Une opération échouée peut référencer un Problem Details minimisé.

---

# 96. Polling

Le polling utilise une fréquence bornée.

---

## 96.1 Header

La réponse peut inclure :

```text
Retry-After
```

---

## 96.2 Long polling

Le long polling n’appartient pas à la baseline.

---

## 96.3 Rate limiting

Les endpoints Operation sont rate-limited afin d’éviter un polling excessif.

---

# 97. Annulation d’une opération

Route conceptuelle :

```text
POST /api/v1/operations/{operationId}/cancel
```

---

## 97.1 Contrôles

- permission ;
- step-up selon l’opération ;
- état compatible ;
- idempotence ;
- audit.

---

## 97.2 Coopérative

L’annulation est coopérative.

Elle ne garantit pas qu’un effet externe déjà réalisé puisse être annulé.

---

# 98. Expiration des opérations

Les ressources Operation possèdent une rétention.

---

## 98.1 Résultat expiré

Une opération expirée ne donne plus accès à son résultat temporaire.

---

## 98.2 Audit

L’historique métier ou d’audit nécessaire reste séparé.

---

# 99. Pagination

Les collections volumineuses utilisent une pagination bornée.

---

## 99.1 Baseline

Privilégier :

```text
cursor ou keyset pagination
```

---

## 99.2 Contrat

```json
{
  "items": [],
  "nextCursor": "opaque-value",
  "hasMore": true
}
```

---

## 99.3 Cursor

Le cursor est :

- opaque ;
- signé ou protégé lorsque nécessaire ;
- borné ;
- lié au tri et aux filtres.

---

## 99.4 Total Count

`totalCount` est facultatif car son calcul peut être coûteux ou incohérent avec une pagination non snapshot.

---

# 100. Taille de page

Chaque endpoint définit :

```text
defaultPageSize

maximumPageSize
```

---

## 100.1 Valeur excessive

Une taille supérieure au maximum est refusée ou réduite selon le contrat explicite.

La baseline privilégie le refus avec une erreur claire.

---

# 101. Tri

Les champs de tri sont allowlistés.

---

## 101.1 SQL arbitraire

Un nom de colonne provenant directement du client n’est jamais concaténé dans SQL.

---

## 101.2 Ordre stable

Le tri inclut un identifiant stable comme clé secondaire.

---

# 102. Filtrage

Les filtres sont explicitement définis par endpoint.

---

## 102.1 Dictionnaire libre

Une structure générique :

```text
filter[field][operator]
```

n’est pas la baseline.

---

## 102.2 Données sensibles dans l’URL

Les recherches pouvant contenir :

- email ;
- téléphone ;
- message ;

doivent tenir compte du fait que les query strings peuvent être journalisées.

---

## 102.3 Recherche sensible

Une recherche complexe sensible peut utiliser un endpoint POST en lecture seule avec :

```text
Cache-Control: no-store
```

---

# 103. Requêtes de recherche

Une recherche POST en lecture seule :

- ne modifie pas l’état ;
- n’exige pas d’Idempotency Key ;
- utilise un contrat borné ;
- n’est pas cachée publiquement ;
- reste autorisée comme query Application.

---

# 104. Cache HTTP

Les règles complètes seront définies dans l’ADR-0030.

---

## 104.1 Baseline sensible

Les réponses de :

- l’administration ;
- l’identité ;
- l’audit ;
- l’export ;
- la récupération ;

utilisent :

```text
Cache-Control: no-store
```

---

## 104.2 GET public

Les pages ou APIs publiques pourront utiliser un cache contrôlé et des ETags.

---

# 105. Antiforgery

Les APIs same-origin utilisant un cookie d’authentification utilisent l’antiforgery pour les méthodes mutables.

---

## 105.1 Méthodes

- POST ;
- PUT ;
- PATCH ;
- DELETE.

---

## 105.2 Login et step-up

Les endpoints Identity restent protégés conformément aux ADR-0020 et ADR-0021.

---

## 105.3 Bearer futur

Une API utilisant exclusivement un bearer token hors cookie pourra employer une stratégie différente.

---

# 106. CORS

CORS reste distinct de :

- authentification ;
- autorisation ;
- antiforgery ;
- rate limiting ;
- idempotence.

---

## 106.1 Baseline

Les APIs d’administration sont same-origin.

---

## 106.2 API externe future

Une origine externe doit être allowlistée exactement.

---

# 107. Rate limiting

Chaque endpoint possède un profil adapté.

---

## 107.1 Réponse

Une limitation utilise :

```text
429
Problem Details
Retry-After
```

---

## 107.2 Idempotence

Une requête rate-limited ne crée aucune entrée métier ou résultat idempotent réussi.

---

# 108. Authentification

Les routes API ne retournent pas de page HTML de connexion.

---

## 108.1 Cookie admin

L’authentification repose sur l’ADR-0020.

---

## 108.2 API partenaire future

Une nouvelle méthode d’authentification nécessitera un contrat dédié.

---

# 109. Autorisation

Chaque opération déclare une permission.

---

## 109.1 Route visible

La présence d’un bouton dans l’interface ne constitue pas l’autorisation.

---

## 109.2 Conditions complémentaires

Une commande peut également exiger :

- entitlement ;
- feature flag ;
- step-up ;
- raison ;
- confirmation ;
- kill switch inactif.

---

# 110. Mass assignment

Les propriétés internes suivantes ne sont jamais modifiables par désérialisation libre :

- statut interne non prévu ;
- rôle ;
- permission ;
- entitlement ;
- Client ID ;
- Environment ;
- Audit Status ;
- Owner ID ;
- Security Stamp ;
- Version courante ;
- destination externe.

---

# 111. Command Query Separation

Les opérations HTTP sont mappées vers :

```text
Application Command

Application Query
```

---

## 111.1 Endpoint mince

L’endpoint est responsable de :

- binding ;
- auth ;
- validation ;
- mapping HTTP.

La logique métier appartient au handler.

---

## 111.2 Résultat Application

Application retourne un résultat typé indépendant d’ASP.NET Core.

---

# 112. Effets externes

Un endpoint ne réalise pas directement :

- SMTP ;
- CRM ;
- webhook ;
- AI ;
- génération longue d’export.

---

## 112.1 Pattern

```text
commande
→ transaction
→ outbox
→ 200, 201 ou 202
→ worker
```

---

## 112.2 Exception synchrone

Un contrôle court requis pour la réponse, comme une vérification de challenge, suit son propre timeout et sa policy.

---

# 113. Audit

Une action administrative sensible produit l’événement défini dans l’ADR-0019.

---

## 113.1 Problem Details

L’Audit Event ID n’est pas exposé publiquement par défaut.

Un Correlation ID permet le diagnostic.

---

## 113.2 Action refusée

Certaines tentatives refusées peuvent également être auditées.

---

# 114. Webhooks

Les endpoints webhook :

- utilisent leurs propres signatures ;
- ne dépendent pas d’un cookie ;
- ne redirigent pas ;
- retournent des statuts cohérents ;
- utilisent la replay protection ;
- persistent avant de répondre.

---

# 115. Uploads

Les uploads utilisent un contrat distinct de l’API JSON.

---

## 115.1 Contrôles

- limite ;
- Content-Type ;
- streaming ;
- métadonnées typées ;
- idempotence ou Upload ID ;
- traitement asynchrone ;
- sécurité média.

---

## 115.2 Fichier Base64

La baseline interdit le transport de gros fichiers en Base64 dans un JSON.

---

# 116. Downloads

Les téléchargements privés utilisent :

- permission ;
- session ;
- step-up selon le contenu ;
- `Content-Disposition` ;
- `nosniff` ;
- `no-store` ;
- aucun redirect public permanent.

---

# 117. Batch APIs

Un endpoint batch doit déclarer :

```text
MaximumItems

Atomicity

PerItemResult

IdempotencyPolicy

Ordering

FailurePolicy
```

---

## 117.1 Baseline

Les batches génériques ne sont pas inclus dans le MVP.

---

## 117.2 Atomicité

Le client doit savoir si le batch est :

- tout ou rien ;
- partiellement appliqué ;
- asynchrone.

---

# 118. OpenAPI

Les endpoints API génèrent un document OpenAPI.

---

## 118.1 Contenu

- routes ;
- versions ;
- schémas ;
- statuts ;
- Problem Details ;
- auth ;
- headers ;
- Idempotency Key ;
- ETag ;
- exemples fictifs.

---

## 118.2 Données réelles

Aucun exemple ne contient de donnée client réelle.

---

# 119. Exposition de la documentation

L’artefact OpenAPI peut être publié dans la CI ou le handover.

---

## 119.1 Production

L’interface interactive Swagger ou équivalente n’est pas exposée publiquement par défaut.

---

## 119.2 Administration

Une exposition protégée peut être activée pour un besoin opérateur.

---

# 120. Versionnement des contrats

Une modification est additive lorsqu’elle :

- ajoute une propriété optionnelle de réponse ;
- ajoute un nouvel endpoint ;
- ajoute un nouveau code d’erreur possible documenté ;
- ajoute une nouvelle enum lorsque les clients doivent déjà tolérer l’inconnu.

---

## 120.1 Breaking change

Exemples :

- suppression d’une propriété ;
- renommage ;
- changement de type ;
- changement de sens ;
- propriété requise ajoutée à une requête ;
- nouvel enum non tolérable ;
- changement de statut principal ;
- nouvelle authentification obligatoire ;
- changement du périmètre d’idempotence.

---

# 121. Compatibilité des requêtes

Les propriétés de requête restent strictes.

---

## 121.1 Nouvelle propriété optionnelle

Elle peut être ajoutée sans casser les anciens clients.

---

## 121.2 Ancien serveur

Un client ne doit pas envoyer une nouvelle propriété à une ancienne version ne la connaissant pas.

La version de route évite cette ambiguïté.

---

# 122. Compatibilité des réponses

Les clients doivent ignorer les propriétés inconnues.

---

## 122.1 Champs obligatoires

Les champs fondamentaux documentés ne sont pas retirés dans une même version majeure.

---

# 123. Codes d’erreur

Un nouveau code peut être ajouté dans une version compatible si :

- le statut HTTP reste cohérent ;
- le client est supposé gérer une catégorie générique ;
- la documentation est mise à jour.

---

## 123.1 Suppression

Un code ne doit pas disparaître sans plan de compatibilité lorsqu’il fait partie d’un contrat utilisé.

---

# 124. Dépréciation

Une API dépréciée doit indiquer :

- version de remplacement ;
- date cible ;
- documentation ;
- migration ;
- métriques d’usage.

---

## 124.1 Headers

Des headers de dépréciation et de fin de disponibilité peuvent être utilisés lorsque le profil le prévoit.

---

## 124.2 Suppression immédiate

Une API utilisée n’est pas supprimée sans période de migration, sauf incident de sécurité critique.

---

# 125. Clients résilients

Un client doit :

- utiliser les codes ;
- accepter des propriétés de réponse supplémentaires ;
- respecter `Retry-After` ;
- conserver l’Idempotency Key pendant un retry ;
- ne pas rejouer aveuglément un 4xx ;
- recharger après un conflit ;
- traiter un `202` comme non terminé.

---

# 126. Observabilité

Événements recommandés :

```text
ApiRequestStarted

ApiRequestCompleted

ApiProblemReturned

ApiContractValidationFailed

ApiVersionRejected

ApiConcurrencyConflict

ApiIdempotencyClaimed

ApiIdempotencyReplayed

ApiIdempotencyConflict

ApiOperationAccepted

ApiOperationCompleted
```

---

## 126.1 Propriétés autorisées

```text
RouteId

OperationId

ApiVersion

Method

StatusCode

ErrorCode

Result

IdempotencyReplayed

Environment

Duration
```

---

## 126.2 Données interdites

```text
Body complet

Idempotency Key brute

Authorization

Cookie

Email

Téléphone

Message

Token

Query string sensible
```

---

# 127. Métriques

Exemples :

```text
api requests

api request duration

api problems

api validation failures

api concurrency conflicts

api idempotency replays

api idempotency conflicts

api asynchronous operations

api rate limited requests
```

---

## 127.1 Labels autorisés

```text
route_id

operation_id

api_version

method

status_code_class

error_category

environment
```

---

## 127.2 Labels interdits

```text
ResourceId

UserId

IdempotencyKey

CorrelationId

TraceId

Email

URL complète
```

---

# 128. Alertes

Alertes possibles :

```text
Hausse des erreurs 500

Hausse des 409 de concurrence

Idempotency conflicts répétés

Routes retournant des erreurs non Problem Details

API renvoyant des redirects de login

Opérations 202 restant bloquées

Rate limiting inhabituel

Version API inconnue appelée fréquemment

Payloads rejetés par taille en hausse
```

---

# 129. Audit

Les événements API techniques n’appartiennent pas tous à la piste d’audit.

---

## 129.1 Actions sensibles

Une commande sensible produit son événement d’audit métier.

---

## 129.2 Idempotent replay

Un replay réussi ne doit pas produire un second événement d’audit métier identique.

---

## 129.3 Download

Le téléchargement d’un export sensible reste audité, même s’il provient d’une Operation existante.

---

# 130. Sécurité

## 130.1 Actifs

- données métier ;
- sessions ;
- permissions ;
- opérations ;
- résultats ;
- clés d’idempotence ;
- contrats ;
- exports ;
- audit.

---

## 130.2 Menaces

- mass assignment ;
- duplication ;
- replay ;
- lost update ;
- divulgation d’exception ;
- open redirect ;
- énumération ;
- payload excessif ;
- injection ;
- autorisation obsolète ;
- réponse mise en cache ;
- opération asynchrone accessible à un tiers.

---

## 130.3 Contrôles

- DTOs ;
- validation stricte ;
- Problem Details ;
- versions ;
- ETag ;
- Idempotency Key ;
- auth ;
- antiforgery ;
- rate limiting ;
- outbox ;
- no-store ;
- audit ;
- tests.

---

# 131. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Double clic créant deux leads | Submission ID ou Idempotency Key |
| Timeout puis seconde création | Replay de la même clé |
| Même clé pour une autre commande | Fingerprint et `409` |
| Admin écrasant une modification | Version et `If-Match` |
| Entité complète modifiable | DTO explicite |
| Stack trace exposée | Problem Details global |
| API redirigée vers login | Suppression des redirects API |
| Recherche sensible dans les logs | POST read-only et no-store |
| Opération async accessible par ID | Autorisation sur Operation |
| Ancienne permission rejouant un résultat | Autorisation réévaluée |
| Payload énorme | Limites avant désérialisation |
| Cookie utilisé en CSRF | Antiforgery et SameSite |

---

# 132. Tests unitaires

Tester notamment :

- mapping des statuts ;
- Error Codes ;
- construction Problem Details ;
- localisation ;
- route version ;
- fingerprint ;
- validation de clé ;
- scope ;
- ETag ;
- pagination ;
- modèles Operation.

---

# 133. Tests de contrats

Chaque endpoint possède des tests vérifiant :

```text
request schema

response schema

status codes

Problem Details

permissions

headers

limits

OpenAPI
```

---

## 133.1 Snapshots

Des snapshots de contrats peuvent être utilisés.

Toute modification doit être revue.

---

# 134. Tests d’intégration HTTP

Utiliser un serveur ASP.NET Core réel ou une factory adaptée.

Tester :

- binding ;
- Content-Type ;
- antiforgery ;
- cookie ;
- JSON ;
- statuts ;
- headers ;
- exception handler ;
- OpenAPI.

---

# 135. Tests des erreurs

Couvrir :

```text
400

401

403

404

405

409

412

413

415

422

428

429

500

503

504
```

---

## 135.1 Content-Type

Chaque erreur API utilise :

```text
application/problem+json
```

---

# 136. Tests anti-divulgation

Injecter :

- secret ;
- token ;
- SQL ;
- chemin ;
- stack trace ;
- email ;
- message provider.

Vérifier leur absence dans :

- Problem Details ;
- headers ;
- logs ;
- OpenAPI ;
- exemples.

---

# 137. Tests d’idempotence

Couvrir :

```text
première commande

replay identique

même clé avec autre payload

même clé autre acteur

même clé autre endpoint

deux requêtes concurrentes

rollback puis retry

commit incertain

réponse 202 rejouée

expiration
```

---

# 138. Tests d’autorisation au replay

Scénario :

1. acteur exécute une commande ;
2. sa permission est retirée ;
3. il rejoue la même clé.

Résultat :

```text
accès refusé
aucune représentation sensible rejouée
aucun nouvel effet
```

---

# 139. Tests de concurrence HTTP

Couvrir :

- ETag valide ;
- ETag obsolète ;
- `If-Match` absent ;
- version body valide ;
- version body obsolète ;
- header et body incompatibles.

---

# 140. Tests des opérations asynchrones

Vérifier :

- `202` ;
- `Location` ;
- Operation ID ;
- progression ;
- succès ;
- échec ;
- annulation ;
- permission ;
- expiration ;
- replay idempotent.

---

# 141. Tests de pagination

Couvrir :

- taille par défaut ;
- taille maximale ;
- cursor valide ;
- cursor invalide ;
- filtre différent ;
- ordre stable ;
- données ajoutées entre deux pages.

---

# 142. Tests d’OpenAPI

Vérifier :

- schémas ;
- versions ;
- Problem Details ;
- headers d’idempotence ;
- ETags ;
- permissions documentées ;
- exemples fictifs ;
- absence d’entité Infrastructure.

---

# 143. Tests de sécurité

Vérifier notamment :

```text
mass assignment

propriété inconnue

JSON trop profond

collection excessive

méthode GET mutante

open redirect

cookie CSRF

route masquée

Idempotency Key brute logguée

opération d’un autre utilisateur

API redirigée vers HTML
```

---

# 144. Tests de performance

Mesurer :

- coût du mapping ;
- désérialisation ;
- validation ;
- fingerprint ;
- claim d’idempotence ;
- replay ;
- pagination ;
- polling Operation ;
- taille des Problem Details.

---

# 145. Tests multiplateformes

Les contrats et tests HTTP fonctionnent sur :

```text
Windows x64

Linux x64
```

Les scénarios PostgreSQL d’idempotence et concurrence sont validés avec PostgreSQL réel sur Linux.

---

# 146. Quality gates

Une contribution est bloquée lorsque :

```text
Un endpoint API retourne une forme d’erreur ad hoc.

Une exception est exposée.

Une route API redirige vers une page de login.

Une mutation utilise GET.

Une entité EF Core est utilisée comme DTO.

Un endpoint d’écriture accepte des champs inconnus.

Une création retryable n’a pas de stratégie d’idempotence.

Une commande async retourne 200 sans Operation ID.

Un endpoint ignore la version attendue.

Une réponse 2xx contient une erreur métier.

Un message localisé est utilisé comme code.

Une Idempotency Key brute est logguée.

Une réponse rejouée contient Set-Cookie.

Une grosse réponse est stockée dans l’entrée d’idempotence.

OpenAPI ne correspond pas aux contrats.
```

---

# 147. Quality gates de production

La production est bloquée lorsque :

```text
Les routes API ne possèdent pas de version.

Le middleware Problem Details est absent.

Une erreur 500 divulgue des détails.

Le handler cookie redirige les APIs.

Les endpoints sensibles n’utilisent pas no-store.

Les commandes critiques ne possèdent pas de concurrence.

Les entrées d’idempotence ne sont pas nettoyées.

Les ressources Operation ne sont pas autorisées.

Les limites de corps sont absentes.

L’OpenAPI de release n’est pas généré.

Les tests de replay et uncertain commit échouent.
```

---

# 148. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-HTTP-001` | Requête HTTP invalide |
| `SF-HTTP-002` | Méthode HTTP interdite |
| `SF-HTTP-003` | Content-Type absent |
| `SF-HTTP-004` | Content-Type non supporté |
| `SF-HTTP-005` | Corps de requête vide |
| `SF-HTTP-006` | Corps de requête trop volumineux |
| `SF-HTTP-007` | JSON invalide |
| `SF-HTTP-008` | Profondeur JSON excessive |
| `SF-HTTP-009` | Propriété inconnue |
| `SF-HTTP-010` | Représentation demandée non supportée |
| `SF-HTTP-011` | Redirect API interdit |
| `SF-HTTP-500` | Erreur interne inattendue |
| `SF-API-001` | Version API absente |
| `SF-API-002` | Version API non supportée |
| `SF-API-003` | Route API inconnue |
| `SF-API-004` | Operation ID inconnu |
| `SF-API-005` | Contrat de requête inconnu |
| `SF-API-006` | Contrat de réponse inconnu |
| `SF-API-007` | Réponse incompatible avec le contrat |
| `SF-API-008` | Mutation par méthode sûre interdite |
| `SF-API-009` | Entité Infrastructure exposée |
| `SF-API-010` | Operation ID de handler absent |
| `SF-PROBLEM-001` | Problem Details absent |
| `SF-PROBLEM-002` | Type de problème absent |
| `SF-PROBLEM-003` | Code d’erreur absent |
| `SF-PROBLEM-004` | Statut incohérent |
| `SF-PROBLEM-005` | Détail sensible détecté |
| `SF-PROBLEM-006` | Instance contenant une query sensible |
| `SF-PROBLEM-007` | Erreur non localisable |
| `SF-VALIDATION-001` | Validation fonctionnelle échouée |
| `SF-VALIDATION-002` | Champ obligatoire absent |
| `SF-VALIDATION-003` | Valeur invalide |
| `SF-VALIDATION-004` | Collection trop grande |
| `SF-VALIDATION-005` | Propriété non modifiable |
| `SF-CONDITIONAL-001` | ETag invalide |
| `SF-CONDITIONAL-002` | `If-Match` requis |
| `SF-CONDITIONAL-003` | Précondition échouée |
| `SF-CONDITIONAL-004` | Version header et body incompatibles |
| `SF-IDEMPOTENCY-001` | Idempotency Key requise |
| `SF-IDEMPOTENCY-002` | Idempotency Key invalide |
| `SF-IDEMPOTENCY-003` | Payload incompatible avec la clé |
| `SF-IDEMPOTENCY-004` | Résultat idempotent existant |
| `SF-IDEMPOTENCY-005` | Commit incertain |
| `SF-IDEMPOTENCY-006` | Commande identique encore en cours |
| `SF-IDEMPOTENCY-007` | Entrée idempotente expirée |
| `SF-IDEMPOTENCY-008` | Scope idempotent incompatible |
| `SF-IDEMPOTENCY-009` | Résultat rejouable indisponible |
| `SF-IDEMPOTENCY-010` | Fingerprint de requête invalide |
| `SF-OPERATION-001` | Operation inconnue |
| `SF-OPERATION-002` | Operation non accessible |
| `SF-OPERATION-003` | Operation non terminée |
| `SF-OPERATION-004` | Operation échouée |
| `SF-OPERATION-005` | Operation non annulable |
| `SF-OPERATION-006` | Annulation déjà demandée |
| `SF-OPERATION-007` | Operation expirée |
| `SF-OPERATION-008` | Résultat temporaire expiré |
| `SF-PAGINATION-001` | Taille de page invalide |
| `SF-PAGINATION-002` | Taille maximale dépassée |
| `SF-PAGINATION-003` | Cursor invalide |
| `SF-PAGINATION-004` | Cursor expiré |
| `SF-PAGINATION-005` | Cursor incompatible avec les filtres |
| `SF-PAGINATION-006` | Champ de tri interdit |
| `SF-PAGINATION-007` | Filtre interdit |
| `SF-CONTRACT-001` | Version de contrat incompatible |
| `SF-CONTRACT-002` | Modification incompatible détectée |
| `SF-CONTRACT-003` | Schéma OpenAPI invalide |
| `SF-CONTRACT-004` | Exemple contenant une donnée interdite |
| `SF-CONTRACT-005` | API dépréciée |
| `SF-CONTRACT-006` | API retirée |

---

# 149. CI/CD

La CI doit exécuter :

```text
tests des contrats

validation OpenAPI

diff de contrats

tests Problem Details

tests des statuts

tests d’idempotence

tests de concurrence

tests antiforgery

tests rate limiting

tests de non-divulgation

tests des opérations asynchrones

analyse des DTOs

analyse des routes mutantes
```

---

## 149.1 Diff OpenAPI

Une modification incompatible doit être :

- détectée ;
- revue ;
- associée à une nouvelle version majeure ou un plan.

---

## 149.2 Artefact

L’OpenAPI de release est conservé comme artefact de version.

---

# 150. Analyse statique

La CI peut détecter :

```text
Results.BadRequest avec objet libre

Results.Problem sans Error Code

retour d’entité EF Core

redirect dans /api

HttpGet sur méthode mutante

endpoint POST sans Operation ID

Idempotency Key logguée

exception.ToString dans la réponse

object ou JsonElement comme contrat libre
```

---

# 151. Règles pour les modules

Chaque endpoint de module doit déclarer :

```text
RouteId

OperationId

ApiVersion

RequestContract

ResponseContract

StatusCodes

ErrorCodes

Permission

TransactionProfile

ConcurrencyProfile

IdempotencyProfile

RateLimitProfile

CacheProfile
```

---

## 151.1 Commande module

Une commande métier ne doit pas être cachée derrière un simple changement de propriété générique si elle exige :

- confirmation ;
- step-up ;
- raison ;
- audit ;
- effet externe.

---

# 152. Règles pour les customizations

Une customization client suit les mêmes contrats.

---

## 152.1 Namespace

Ses Operation IDs utilisent un namespace explicite.

Exemple :

```text
client.sample-project.quote.approve.v1
```

---

## 152.2 Interdictions

Elle ne doit pas :

- retourner une erreur libre ;
- exposer une entité ;
- accepter une URL arbitraire ;
- ignorer l’idempotence ;
- contourner les versions ;
- rendre Swagger public sans décision.

---

# 153. Règles pour LogicGate

LogicGate doit :

- maintenir les conventions ;
- revoir les changements de contrat ;
- surveiller les erreurs ;
- vérifier les replays ;
- maintenir l’OpenAPI ;
- documenter les versions ;
- préparer les handovers ;
- conserver les tests de compatibilité.

---

## 153.1 LogicGate ne doit pas

- utiliser `200` pour toute réponse ;
- dépendre d’un texte d’erreur ;
- modifier silencieusement un statut ;
- supprimer une version utilisée ;
- conserver les clés d’idempotence indéfiniment ;
- exposer les APIs d’administration publiquement sans contrôle.

---

# 154. Règles pour Codex

Codex peut :

- créer les contrats ;
- implémenter Problem Details ;
- ajouter les mappings ;
- ajouter l’idempotence ;
- créer les Operations ;
- produire l’OpenAPI ;
- ajouter les tests.

---

## 154.1 Codex ne doit pas

```text
Modifier un contrat public sans tâche explicite

Inventer une nouvelle forme d’erreur

Exposer une exception

Retourner une entité EF Core

Supprimer un code d’erreur utilisé

Désactiver l’antiforgery

Rendre Swagger public

Rejouer une commande réelle de production

Purger des entrées d’idempotence réelles

Changer une version API de production
```

---

## 154.2 Décision humaine

Une décision humaine est requise pour :

- nouvelle version majeure ;
- suppression d’un endpoint ;
- changement de statut principal ;
- nouvelle API externe ;
- nouvelle stratégie d’authentification ;
- modification du périmètre d’idempotence ;
- publication publique d’OpenAPI ;
- endpoint batch ;
- changement d’une rétention de clés.

---

# 155. Anti-patterns interdits

## 155.1 Erreur ad hoc

```json
{
  "success": false,
  "message": "Une erreur est survenue."
}
```

---

## 155.2 Exception exposée

```json
{
  "exceptionType": "NpgsqlException",
  "stackTrace": "..."
}
```

---

## 155.3 Toujours `200`

L’erreur est cachée dans un champ du body.

---

## 155.4 Redirect API

Une session expirée retourne une page HTML de login.

---

## 155.5 Entité exposée

L’endpoint accepte directement un modèle EF Core.

---

## 155.6 POST non idempotent rejoué

Un timeout crée un second export ou un second lead.

---

## 155.7 Clé comme autorisation

Connaître l’Idempotency Key permet de récupérer le résultat.

---

## 155.8 Clé réutilisée silencieusement

Un nouveau payload remplace le résultat précédent.

---

## 155.9 Async caché

L’endpoint retourne `200` alors que le job n’a pas commencé.

---

## 155.10 Conflit ignoré

Le serveur recharge la version actuelle puis écrase avec les nouvelles valeurs.

---

## 155.11 Propriétés inconnues ignorées en écriture

Une faute de frappe produit un faux succès.

---

## 155.12 Swagger public

L’ensemble des endpoints d’administration est documenté publiquement en production.

---

# 156. Conséquences positives

La décision apporte :

- contrats cohérents ;
- erreurs exploitables ;
- meilleure sécurité ;
- retries sûrs ;
- absence de duplication évitable ;
- concurrence explicite ;
- opérations longues visibles ;
- OpenAPI vérifiable ;
- meilleure observabilité ;
- intégration future facilitée ;
- meilleure compatibilité client ;
- handover plus clair.

---

# 157. Conséquences négatives

La décision entraîne :

- DTOs supplémentaires ;
- mappings ;
- middleware ;
- stockage d’idempotence ;
- ressources Operation ;
- tests de contrats ;
- versionnement ;
- gestion des replays ;
- surveillance de compatibilité ;
- discipline accrue dans les modules.

---

# 158. Risques acceptés

## 158.1 Taille du stockage d’idempotence

Les résultats temporaires consomment PostgreSQL.

Réduction :

- body borné ;
- références pour les gros résultats ;
- TTL ;
- job de nettoyage.

---

## 158.2 Conflits plus visibles

Les utilisateurs peuvent recevoir davantage de `409`.

Ce comportement est préférable à une perte silencieuse.

---

## 158.3 Évolution des clients

Un client ancien peut ne pas comprendre une nouvelle propriété ou valeur.

Réduction :

- compatibilité additive ;
- versions majeures ;
- tests ;
- dépréciation.

---

## 158.4 Complexité des Operations

Les traitements longs nécessitent une ressource supplémentaire.

Cette complexité est acceptée pour rendre leur état explicite.

---

# 159. Mesures compensatoires

- Problem Details ;
- Error Codes ;
- routes versionnées ;
- DTOs ;
- validation stricte ;
- ETags ;
- Idempotency Keys ;
- stockage transactionnel ;
- Operations ;
- outbox ;
- rate limiting ;
- antiforgery ;
- OpenAPI ;
- tests de contrat ;
- observabilité ;
- quality gates.

---

# 160. Performance

## 160.1 Problem Details

Le coût de sérialisation est faible.

---

## 160.2 Idempotence

Chaque commande concernée ajoute :

- hash ;
- lecture ou insertion ;
- stockage borné.

---

## 160.3 Replay

Un replay réussi évite la réexécution du métier.

---

## 160.4 Pagination

La pagination par cursor réduit les coûts des grands offsets.

---

## 160.5 OpenAPI

La génération appartient principalement au build et au démarrage, pas à chaque requête.

---

# 161. Coûts

## 161.1 Coûts supplémentaires

- développement ;
- stockage ;
- tests ;
- documentation ;
- maintenance de versions ;
- nettoyage ;
- support client.

---

## 161.2 Coûts évités

- doublons ;
- pertes de modifications ;
- clients cassés ;
- erreurs opaques ;
- enquêtes longues ;
- contrats divergents ;
- intégrations improvisées ;
- exports lancés plusieurs fois.

---

# 162. Maintenabilité

Pour chaque endpoint, il doit être possible de répondre :

```text
Quelle route ?

Quelle version ?

Quelle méthode ?

Quel Operation ID ?

Quel contrat d’entrée ?

Quel contrat de sortie ?

Quels statuts ?

Quels Error Codes ?

Quelle permission ?

Quelle stratégie de concurrence ?

Quelle stratégie d’idempotence ?

Quelle limite ?

Quel profil de cache ?

Quels tests ?
```

---

# 163. Compatibilité

## 163.1 API Version

La version majeure appartient à la route.

---

## 163.2 Contract Version

Certains payloads peuvent posséder une version interne lorsque nécessaire.

---

## 163.3 Problem Details

Les extensions peuvent être ajoutées.

Les champs fondamentaux et Error Codes restent compatibles.

---

## 163.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

La migration d’endpoints existants ad hoc peut modifier leurs formes d’erreur et nécessite un plan.

---

# 164. Migration depuis des erreurs ad hoc

Le processus est :

```text
inventorier les endpoints

inventorier les statuts

inventorier les erreurs

attribuer les Error Codes

ajouter Problem Details

supprimer les stack traces

adapter les clients

ajouter les tests
```

---

# 165. Migration depuis des formulaires HTML

Les formulaires HTML peuvent continuer à utiliser un rendu serveur.

---

## 165.1 Application

Ils doivent néanmoins réutiliser :

- les mêmes commandes Application ;
- les mêmes Error Codes ;
- les mêmes règles d’idempotence ;
- les mêmes validations.

---

## 165.2 Erreur HTML

Une page HTML n’est pas obligée de retourner Problem Details au navigateur.

Le contrat Application reste commun.

---

# 166. Migration depuis last-write-wins

Suivre l’ADR-0028 :

```text
ajouter Version

ajouter ETag ou Expected Version

retourner 409 ou 412

adapter les clients

tester
```

---

# 167. Migration depuis les opérations synchrones longues

Le processus est :

```text
créer une ressource Operation

créer un job

ajouter l’Idempotency Key

retourner 202

ajouter le polling

ajouter le résultat et l’expiration
```

---

# 168. Transfert client

Le handover doit inclure :

- OpenAPI ;
- versions ;
- Error Codes ;
- profils d’authentification ;
- profils d’idempotence ;
- ressources Operation ;
- conventions ETag ;
- exemples fictifs ;
- tests de contrats ;
- procédures de dépréciation.

---

## 168.1 Secrets

Aucune clé d’idempotence réelle, token ou credential n’est transféré dans la documentation.

---

# 169. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- d’appeler une API réelle ;
- de rejouer une commande réelle ;
- d’annuler une Operation réelle ;
- de purger des entrées ;
- de publier OpenAPI ;
- de modifier une version ;
- de déprécier un endpoint ;
- d’appliquer une migration.

---

# 170. Réversibilité

## 170.1 Classification

```text
élevée avant publication

moyenne après consommation par des clients

faible après suppression d’une version utilisée
```

---

## 170.2 Problem Details

Le retour vers des erreurs ad hoc réduirait les garanties et nécessiterait un nouvel ADR.

---

## 170.3 Idempotence

Retirer l’idempotence d’une commande déjà documentée constitue un breaking change.

---

# 171. Plan d’implémentation

## Étape 1 — Conventions

Définir :

- Route IDs ;
- Operation IDs ;
- versions ;
- statuts ;
- JSON ;
- headers.

---

## Étape 2 — Problem Details

Ajouter :

- factory ;
- middleware ;
- mappings ;
- localisation ;
- redaction.

---

## Étape 3 — Validation

Ajouter :

- désérialisation stricte ;
- propriétés inconnues ;
- erreurs par champ ;
- limites.

---

## Étape 4 — Concurrence HTTP

Ajouter :

- ETag ;
- `If-Match` ;
- Expected Version ;
- mappings `409`, `412`, `428`.

---

## Étape 5 — Idempotence

Créer :

- modèle PostgreSQL ;
- scopes ;
- fingerprint ;
- transaction ;
- replay ;
- nettoyage.

---

## Étape 6 — Operations

Créer :

- ressources ;
- statuts ;
- polling ;
- annulation ;
- expiration.

---

## Étape 7 — Premiers endpoints

Appliquer aux :

- formulaires publics ;
- leads ;
- exports ;
- jobs opérateur.

---

## Étape 8 — OpenAPI

Ajouter :

- génération ;
- validation ;
- diff ;
- artefact.

---

## Étape 9 — Observabilité

Ajouter :

- métriques ;
- événements ;
- alertes ;
- dashboards.

---

## Étape 10 — Quality gates

Ajouter :

- analyse statique ;
- tests de replay ;
- tests de contrat ;
- tests de compatibilité.

---

# 172. Tâches liées proposées

```text
SF-TASK-415 — Define SiteFactory HTTP API conventions

SF-TASK-416 — Define stable API route and operation identifiers

SF-TASK-417 — Add centralized Problem Details generation

SF-TASK-418 — Add stable SiteFactory API error-code mappings

SF-TASK-419 — Add strict JSON request deserialization

SF-TASK-420 — Add structured field-validation Problem Details

SF-TASK-421 — Suppress cookie-auth redirects for API routes

SF-TASK-422 — Add HTTP ETag and If-Match concurrency support

SF-TASK-423 — Add PostgreSQL idempotency-record persistence

SF-TASK-424 — Add deterministic request fingerprinting

SF-TASK-425 — Add transactional idempotent-command execution

SF-TASK-426 — Add safe idempotent-response replay

SF-TASK-427 — Add idempotency retention and cleanup jobs

SF-TASK-428 — Add asynchronous HTTP Operation resources

SF-TASK-429 — Add operation polling, cancellation and expiration

SF-TASK-430 — Add cursor-based API pagination contracts

SF-TASK-431 — Generate versioned OpenAPI artifacts

SF-TASK-432 — Add OpenAPI compatibility-diff quality gates

SF-TASK-433 — Add API Problem Details and status-code tests

SF-TASK-434 — Add concurrent idempotency and uncertain-commit tests

SF-TASK-435 — Add HTTP API observability and runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 173. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Les routes API sont versionnées.

Chaque endpoint possède un Route ID et Operation ID.

Les contrats utilisent des DTOs explicites.

Les propriétés d’écriture inconnues sont refusées.

Les erreurs API utilisent application/problem+json.

Chaque erreur fonctionnelle possède un Error Code stable.

Les titres et détails peuvent être localisés.

Aucune stack trace n’apparaît.

Une API non authentifiée retourne 401 et non 302.

Les statuts HTTP correspondent aux résultats réels.

Les créations retournent 201 et Location.

Les traitements longs retournent 202 et une Operation.

Les opérations peuvent être consultées selon les permissions.

Les mises à jour concurrentes retournent 409 ou 412.

Les suppressions sensibles peuvent exiger If-Match.

Les commandes retryables utilisent une Idempotency Key.

La clé brute n’est ni stockée ni logguée.

Le fingerprint est déterministe et versionné.

Un replay identique ne crée aucun nouvel effet.

Un payload différent avec la même clé est refusé.

Un acteur différent ne peut pas rejouer le résultat.

Un replay ne retransmet aucun Set-Cookie.

Un uncertain commit peut être réconcilié.

Les entrées idempotentes possèdent un TTL.

Les commandes asynchrones rejouées retournent la même Operation.

Les endpoints same-origin mutables utilisent l’antiforgery.

Les limites de corps et collections sont appliquées.

L’OpenAPI correspond aux contrats réels.

Les modifications incompatibles sont détectées.

Les tests fonctionnent sur Windows et Linux.
```

---

# 174. Conditions de révision

Réexaminer cette décision si :

- une API publique commerciale est lancée ;
- OAuth ou OpenID Connect est introduit ;
- GraphQL est ajouté ;
- gRPC est ajouté ;
- plusieurs services exposent leurs propres APIs ;
- un API Gateway devient obligatoire ;
- les clients hors navigateur deviennent nombreux ;
- les commandes batch deviennent fréquentes ;
- une API multi-tenant est adoptée ;
- une signature de requête devient nécessaire ;
- plusieurs versions majeures doivent coexister longtemps ;
- un SDK client officiel est publié.

---

# 175. Indicateurs à suivre

- requêtes par version ;
- statuts HTTP ;
- Problem Details ;
- erreurs par code ;
- validation failures ;
- conflits de concurrence ;
- Idempotency Keys utilisées ;
- replays ;
- conflits de fingerprint ;
- uncertain commits ;
- Operations queued ;
- Operations failed ;
- polling rate ;
- endpoints dépréciés ;
- incompatibilités OpenAPI ;
- payloads trop volumineux.

---

# 176. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des endpoints retournent encore des erreurs ad hoc

des APIs redirigent vers le login

des messages localisés sont parsés par les clients

les Idempotency Keys sont logguées

des commandes dupliquées apparaissent

les 409 deviennent fréquents sans UX adaptée

les Operations restent bloquées

les statuts 500 augmentent

des entités Infrastructure apparaissent dans OpenAPI

les contrats changent sans version

Swagger devient accessible publiquement

les clés expirées s’accumulent
```

---

# 177. Conséquences observées

Cette section est append-only après mise en œuvre.

## 177.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
des contrats HTTP normalisés,
de Problem Details,
des Idempotency Keys
et des ressources Operation.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- Route ID ;
- Operation ID ;
- statut ;
- Error Code ;
- replay ;
- incident ou bénéfice ;
- action proposée.

---

# 178. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0031-application-startup-validation-and-runtime-compatibility.md](0031-application-startup-validation-and-runtime-compatibility.md)
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

---

# 179. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 174 |

---

# 180. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 181. Synthèse de la décision

```text
Routes
→ /api/v1
→ Route IDs
→ Operation IDs
→ méthodes cohérentes
→ aucune mutation par GET

Contrats
→ DTOs explicites
→ JSON camelCase
→ UTF-8
→ enums textuelles
→ dates UTC
→ requests strictes
→ responses extensibles

Succès
→ 200 lecture ou résultat
→ 201 création + Location
→ 202 Operation asynchrone
→ 204 sans contenu

Erreurs
→ application/problem+json
→ type URN
→ title localisable
→ status
→ detail sûr
→ instance neutralisée
→ Error Code stable
→ Trace ID
→ Correlation ID

Validation
→ 400 syntaxe
→ 422 validation fonctionnelle
→ 409 conflit
→ 412 If-Match
→ 428 précondition requise
→ 429 rate limiting
→ 500 générique

Concurrence
→ ETag
→ If-Match
→ Expected Version
→ aucun écrasement silencieux

Idempotence
→ Idempotency-Key
→ scope client/environnement/opération/acteur
→ clé hashée
→ fingerprint canonique
→ même transaction que l’effet
→ replay du même résultat
→ conflit si payload différent
→ TTL et nettoyage

Sécurité du replay
→ auth réévaluée
→ permission actuelle
→ aucun Set-Cookie rejoué
→ aucun gros body sensible stocké
→ clé non utilisée comme autorisation

Asynchrone
→ 202
→ Operation ID
→ polling borné
→ résultat séparé
→ annulation coopérative
→ expiration

HTTP navigateur
→ cookie sécurisé
→ antiforgery
→ aucune redirection HTML dans /api
→ CORS distinct
→ no-store sur le sensible

Documentation
→ OpenAPI versionné
→ Problem Details documenté
→ diff de compatibilité
→ aucun exemple réel
→ Swagger non public par défaut

Qualité
→ tests contrats
→ tests statuts
→ tests Problem Details
→ tests idempotence
→ tests uncertain commit
→ tests concurrence
→ tests OpenAPI
```

> Un contrat HTTP fiable ne se limite pas à choisir une route et à retourner du JSON. Il doit permettre au client de comprendre précisément le résultat, de rejouer une commande après une coupure sans la dupliquer, de détecter une modification concurrente et de diagnostiquer une erreur sans dépendre d’un texte ou d’un détail interne.
