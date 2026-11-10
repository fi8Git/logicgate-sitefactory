---
adrId: ADR-0026
title: Build immutable container images and run them with least privilege
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
  - ../03-generation/14-client-configuration-schema.md
  - ../03-generation/15-cli-specification.md
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
  - ./0005-file-generation-strategies.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0013-local-git-initialization-and-explicit-remote-publishing.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0017-background-jobs-and-reliable-side-effects.md
  - ./0019-audit-trail-and-sensitive-administrative-actions.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ./0022-browser-security-headers-and-content-security-policy.md
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
  - ./0024-client-data-export-and-portability.md
  - ./0025-third-party-integrations-and-egress-control.md
targetVersion: 0.1.0
---

# ADR-0026 — Construction et durcissement des images et conteneurs

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le public ne livre pas encore les images Web/Worker/Migrator de production
décrites ci-dessous. Les exigences multi-stage, non-root, immutabilité,
capabilities réduites, filesystem read-only, scans/signatures et absence de
secrets restent normatives pour une future publication expressément autorisée.
Le [harnais PostgreSQL](../../tests/PostgreSql/README.md) utilise une image
éphémère épinglée par digest ; cela ne prouve ni le build, ni le durcissement,
ni le déploiement d'images clientes. Aucun Dockerfile ou pipeline de publication
n'est ajouté par cette adaptation documentaire.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory construit des images de production :

```text
immuables

minimales

reproductibles

traçables

scannées

signées

exécutées sans privilège
```

La baseline impose :

```text
build multi-stage

restore verrouillé

images de base approuvées et épinglées par digest

aucun SDK dans l’image d’exécution

utilisateur non-root

filesystem racine en lecture seule

capabilities Linux supprimées

no-new-privileges

volumes d’écriture explicites

tmpfs borné

aucun shell ou gestionnaire de packages lorsque possible

aucun secret dans les couches

SBOM

provenance de build

scan de vulnérabilités

signature des artefacts de release

déploiement par digest OCI
```

Les conteneurs de production ne doivent pas contenir les outils utilisés pour :

- compiler ;
- restaurer les packages ;
- exécuter les tests ;
- publier des packages ;
- accéder à Git ;
- administrer Docker ;
- modifier l’hôte ;
- déboguer de manière interactive.

---

## 2. Résumé

Le pipeline de référence est :

```text
source vérifiée
→ contexte Docker filtré
→ image SDK épinglée
→ restore verrouillé
→ build Release
→ tests
→ publish déterministe
→ image runtime minimale
→ inspection de l’image
→ SBOM
→ scan de vulnérabilités
→ secret scan
→ provenance
→ signature
→ publication dans le registry
→ promotion du même digest
→ vérification avant déploiement
```

L’exécution de référence est :

```text
image immuable
→ USER non-root
→ port interne non privilégié
→ root filesystem read-only
→ volumes nommés explicites
→ tmpfs pour les temporaires
→ cap_drop ALL
→ no-new-privileges
→ réseau limité
→ ressources bornées
→ health checks natifs
→ arrêt gracieux
→ logs stdout/stderr
```

Le profil produit initialement deux types d’images applicatives :

```text
application runtime image
→ modes web et worker

migration image
→ migrations explicites uniquement
→ jamais démarrée automatiquement
```

La même image applicative est promue de staging vers production.

Elle n’est pas reconstruite avec une configuration différente pour chaque environnement.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Une image de production ne contient pas le SDK .NET.

Une image de production ne contient pas le repository Git.

Une image de production ne contient pas les tests.

Une image de production ne contient aucun secret.

Une image de production ne contient aucun credential NuGet.

Une image de production ne contient aucune clé SSH.

Une image de production ne contient aucun fichier .env réel.

Une image de production ne s’exécute pas avec UID 0.

Un conteneur applicatif ne possède aucune capability Linux
par défaut.

Un conteneur applicatif n’est jamais privileged.

Un conteneur applicatif ne monte jamais le Docker socket.

Un conteneur applicatif ne monte jamais la racine de l’hôte.

Le filesystem racine est en lecture seule.

Toute écriture runtime possède une destination explicitement
déclarée.

Les données persistantes n’appartiennent pas à la couche
inscriptible du conteneur.

Les secrets sont injectés uniquement au runtime.

Le déploiement référence un digest d’image immuable.

Le tag `latest` est interdit en production.

Une image n’est pas reconstruite entre staging et production.

Une migration n’est jamais exécutée automatiquement
au démarrage du service web.

Une image non scannée ou dont la provenance est inconnue
ne peut pas devenir une release stable.

Un rollback applicatif réutilise un digest déjà vérifié.

Un rollback d’image ne constitue pas un rollback
automatique de base de données.
```

---

## 4. Contexte

Le profil de déploiement SiteFactory repose initialement sur :

- Linux ;
- Docker Engine ;
- Docker Compose ;
- Caddy ;
- ASP.NET Core ;
- worker en arrière-plan ;
- PostgreSQL ;
- volumes persistants.

Une image de conteneur peut introduire plusieurs catégories de risques :

- package système vulnérable ;
- package NuGet vulnérable ;
- image de base compromise ;
- secret présent dans une couche ;
- SDK ou compilateur disponible après compromission ;
- exécution root ;
- shell interactif ;
- package manager ;
- filesystem inscriptible ;
- capabilities Linux excessives ;
- accès au Docker socket ;
- montage hôte dangereux ;
- binaire téléchargé dynamiquement ;
- image modifiée entre staging et production ;
- tag mutable ;
- provenance inconnue ;
- outil de debug exploitable ;
- fichier temporaire contenant une donnée personnelle ;
- conteneur capable d’altérer l’hôte.

L’isolation d’un conteneur n’est pas équivalente à celle d’une machine virtuelle.

Un processus compromis peut exploiter :

- ses permissions ;
- ses volumes ;
- son réseau ;
- ses credentials ;
- ses capabilities ;
- une vulnérabilité du runtime ou du noyau.

SiteFactory doit donc limiter les capacités du conteneur avant tout incident.

---

## 5. Problème à résoudre

Comment produire et exécuter les images SiteFactory tout en garantissant :

- une supply chain traçable ;
- des images minimales ;
- l’absence de secrets ;
- une exécution non-root ;
- l’immutabilité ;
- la persistance contrôlée ;
- la compatibilité avec ASP.NET Core ;
- la compatibilité avec les workers ;
- des migrations explicites ;
- une mise à jour maîtrisée des images de base ;
- un rollback possible ;
- une exploitation adaptée à un VPS ?

La solution doit également éviter :

- les Dockerfiles monolithiques ;
- les images SDK en production ;
- les tags flottants ;
- les scripts shell d’entrée complexes ;
- les `chmod 777` ;
- les conteneurs `privileged` ;
- les capabilities ajoutées par commodité ;
- les outils réseau inutiles ;
- le téléchargement de binaires au démarrage ;
- les migrations implicites ;
- le stockage runtime dans la couche du conteneur ;
- les secrets passés par `ARG` ou copiés dans les layers ;
- les images différentes selon l’environnement.

---

## 6. Portée

Cette décision couvre :

- Dockerfiles ;
- contextes de build ;
- BuildKit ;
- images de base ;
- restore NuGet ;
- build ;
- tests ;
- publish ;
- images runtime ;
- image de migration ;
- utilisateurs et permissions ;
- filesystem ;
- volumes ;
- tmpfs ;
- capabilities ;
- seccomp ;
- ressources ;
- health checks ;
- arrêt gracieux ;
- SBOM ;
- provenance ;
- signature ;
- scans ;
- registry ;
- tags ;
- digests ;
- promotion ;
- rollback ;
- images Caddy et PostgreSQL ;
- procédures de debug ;
- tests ;
- handover.

---

## 7. Hors portée

Cette décision ne définit pas :

- le durcissement complet du noyau Linux ;
- la configuration SSH du VPS ;
- la sécurité physique de l’hébergeur ;
- un orchestrateur Kubernetes ;
- un service mesh ;
- une isolation par machine virtuelle ;
- un runtime de conteneurs confidentiels ;
- une conformité FIPS universelle ;
- une politique AppArmor personnalisée obligatoire ;
- une politique SELinux personnalisée obligatoire ;
- un scanner précis imposé ;
- un registry précis imposé ;
- une PKI de signature précise imposée ;
- la stratégie complète de patch du VPS ;
- le choix final d’une bibliothèque de traitement d’images.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Exécution avec le moindre privilège**
2. **Absence de secrets dans les images**
3. **Immutabilité**
4. **Provenance vérifiable**
5. **Réduction de la surface d’attaque**
6. **Déploiement du même digest entre environnements**
7. **Migrations explicites**
8. **Compatibilité avec le profil Docker Compose**
9. **Scan de la supply chain**
10. **Rollback fiable**
11. **Persistance maîtrisée**
12. **Reproductibilité du build**
13. **Observabilité**
14. **Gestion des vulnérabilités**
15. **Transfert client**
16. **Simplicité d’exploitation du MVP**

---

## 9. Contraintes

La décision doit respecter :

```text
.NET 10

Docker BuildKit

Docker Compose

Linux en production

Images OCI

Packages NuGet verrouillés

Utilisateur non-root

Port interne supérieur à 1024

Root filesystem read-only

Volumes persistants explicites

Aucun Docker socket

Aucune migration automatique

SBOM

Scan

Signature

Digest immuable

Compatibilité Windows pour le développement
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les builds de release sont exécutés dans une CI contrôlée ;
- un registry OCI privé ou contrôlé est disponible ;
- le profil de production cible principalement Linux x64 ;
- le runtime .NET officiel fournit une image compatible ;
- les dépendances natives restent limitées ;
- Caddy termine TLS ;
- l’application écoute sur un port interne non privilégié ;
- le worker peut utiliser la même image que le web ;
- les migrations peuvent être empaquetées dans un artefact ou une image dédiée ;
- un opérateur peut vérifier une signature avant le déploiement.

---

# 11. Définitions

## 11.1 Build image

Image contenant les outils nécessaires à :

- restore ;
- compilation ;
- tests ;
- publish.

---

## 11.2 Runtime image

Image minimale utilisée pour exécuter l’application.

---

## 11.3 Migrator image

Image dédiée à l’application explicite des migrations.

---

## 11.4 Base image

Image parente utilisée par une étape Docker.

---

## 11.5 Image digest

Empreinte immuable identifiant exactement une image OCI.

---

## 11.6 Tag

Alias humain pouvant être déplacé vers un autre digest.

---

## 11.7 SBOM

Inventaire machine-readable des composants présents dans une image.

---

## 11.8 Provenance

Attestation décrivant comment, où et à partir de quelle source l’image a été construite.

---

## 11.9 Image signature

Preuve cryptographique reliant une identité autorisée à un digest OCI.

---

## 11.10 Chiseled or distroless image

Image runtime réduite ne fournissant pas les outils d’un système généraliste.

---

## 11.11 Linux capability

Permission fine accordée à un processus au-delà des droits Unix ordinaires.

---

## 11.12 Read-only root filesystem

Filesystem racine du conteneur ne permettant pas d’écriture runtime.

---

# 12. Option A — Image SDK monolithique

## 12.1 Description

L’image finale est basée sur le SDK .NET et contient :

- source ;
- outils ;
- build ;
- runtime.

---

## 12.2 Avantages

- Dockerfile simple ;
- diagnostic facile ;
- commandes `dotnet` disponibles ;
- migrations simples ;
- shell disponible.

---

## 12.3 Inconvénients

- image volumineuse ;
- SDK présent ;
- surface d’attaque ;
- source présente ;
- outils exploitables ;
- temps de transfert ;
- packages inutiles.

---

## 12.4 Risques

- compilation de code après compromission ;
- fuite de source ;
- installation de packages ;
- modification runtime ;
- présence de caches avec credentials.

---

## 12.5 Réversibilité

```text
élevée
```

---

# 13. Option B — Image multi-stage mais exécution root

## 13.1 Description

Le SDK est retiré, mais le processus s’exécute avec UID 0.

---

## 13.2 Avantages

- image réduite ;
- gestion simple des fichiers ;
- accès aux ports bas ;
- peu de problèmes de permissions.

---

## 13.3 Inconvénients

- privilèges excessifs ;
- volumes modifiables comme root ;
- impact renforcé d’une évasion ;
- permissions masquées ;
- habitudes dangereuses.

---

## 13.4 Risques

- altération de fichiers montés ;
- exploitation de capabilities ;
- création de fichiers root sur l’hôte ;
- administration involontaire.

---

## 13.5 Réversibilité

```text
moyenne
```

Les volumes peuvent déjà posséder de mauvaises permissions.

---

# 14. Option C — Image minimale non-root sans chaîne de provenance

## 14.1 Description

Le runtime est durci, mais :

- tags flottants ;
- pas de SBOM ;
- pas de signature ;
- pas de provenance.

---

## 14.2 Avantages

- bonne sécurité runtime ;
- faible taille ;
- mise en œuvre plus rapide ;
- faible complexité CI.

---

## 14.3 Inconvénients

- image exacte difficile à prouver ;
- base mutable ;
- composants inconnus ;
- rollback ambigu ;
- handover incomplet ;
- risque de substitution.

---

## 14.4 Risques

- tag déplacé ;
- image reconstruite différemment ;
- package vulnérable non identifié ;
- artefact non autorisé déployé.

---

## 14.5 Réversibilité

```text
élevée
```

---

# 15. Option D — Build attesté et runtime au moindre privilège

## 15.1 Description

La chaîne combine :

- build multi-stage ;
- dépendances verrouillées ;
- images épinglées ;
- image runtime minimale ;
- non-root ;
- filesystem read-only ;
- SBOM ;
- scans ;
- signature ;
- déploiement par digest.

---

## 15.2 Avantages

- surface réduite ;
- provenance ;
- rollback fiable ;
- secrets protégés ;
- promotion immuable ;
- runtime limité ;
- conformité facilitée ;
- handover plus complet.

---

## 15.3 Inconvénients

- CI plus complexe ;
- gestion des signatures ;
- permissions de volumes ;
- compatibilité native ;
- debug plus encadré ;
- tests d’intégration supplémentaires.

---

## 15.4 Risques

- image trop minimale ;
- faux positifs de scan ;
- signature indisponible ;
- volume mal préparé ;
- outils de diagnostic insuffisants.

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

| Critère | Poids | SDK monolithique | Runtime root | Minimal non signé | Build attesté |
|---|---:|---:|---:|---:|---:|
| Surface d’attaque | 5 | 1 | 3 | 5 | 5 |
| Moindre privilège | 5 | 1 | 1 | 5 | 5 |
| Provenance | 5 | 1 | 2 | 2 | 5 |
| Absence de secrets | 5 | 2 | 4 | 4 | 5 |
| Rollback | 4 | 2 | 3 | 3 | 5 |
| Reproductibilité | 4 | 2 | 3 | 3 | 5 |
| Diagnostic | 3 | 5 | 4 | 2 | 3 |
| Exploitation MVP | 4 | 4 | 4 | 4 | 4 |
| Transfert client | 4 | 2 | 3 | 3 | 5 |
| Maintenabilité | 5 | 2 | 3 | 4 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons de produire :

```text
une image applicative de production
+ une image de migration dédiée
```

à partir d’un build multi-stage.

---

## 17.1 Image applicative

L’image applicative supporte les modes :

```text
web

worker

healthcheck
```

Elle ne contient pas :

- SDK ;
- tooling EF Core ;
- client Git ;
- package manager ;
- scripts de déploiement ;
- outils d’administration.

---

## 17.2 Image de migration

L’image de migration contient uniquement :

- runtime nécessaire ;
- assembly ou bundle de migration ;
- client applicatif de vérification ;
- métadonnées de version.

Elle ne :

- démarre pas automatiquement ;
- publie pas de port ;
- contient pas le SDK ;
- contient pas les credentials de production.

---

## 17.3 Image unique par release

Les modes `web` et `worker` utilisent le même digest lorsque leurs dépendances sont identiques.

Une image distincte est autorisée si le worker nécessite des bibliothèques natives supplémentaires importantes.

---

# 18. Images de base

Les images de base doivent provenir d’un catalogue approuvé.

---

## 18.1 Baseline

Privilégier les images runtime officielles .NET :

- minimales ;
- supportées ;
- compatibles avec la version .NET cible ;
- compatibles avec la libc et les bibliothèques nécessaires.

---

## 18.2 Profils

Profils conceptuels :

```text
runtime.chiseled.v1

runtime.compatibility.v1

runtime.media-worker.v1

runtime.migrator.v1
```

---

## 18.3 Image chiseled

Le profil chiseled est privilégié lorsqu’il supporte :

- globalization requise ;
- certificats ;
- fuseaux horaires ;
- bibliothèques natives ;
- diagnostics nécessaires.

---

## 18.4 Profil compatibility

Une image runtime plus classique peut être utilisée lorsqu’une dépendance native ne fonctionne pas dans le profil minimal.

Cette exception doit être :

- documentée ;
- scannée ;
- bornée à l’image concernée ;
- accompagnée d’un plan de réduction.

---

# 19. Épinglage des images de base

Chaque image de base est référencée par :

```text
tag de version explicite
+ digest SHA-256
```

Exemple conceptuel :

```text
mcr.microsoft.com/dotnet/aspnet:10.0-<profile>@sha256:<digest>
```

---

## 19.1 Tags flottants

Sont interdits :

```text
latest

10.0

stable

main
```

lorsqu’ils ne sont pas accompagnés d’un digest verrouillé.

---

## 19.2 Mise à jour

Une mise à jour de digest produit :

- nouveau build ;
- nouveaux scans ;
- nouvelle SBOM ;
- nouvelle signature ;
- nouvelle release ou rebuild documenté.

---

# 20. Catalogue des images

Le projet doit disposer d’un inventaire machine-readable.

Exemple conceptuel :

```text
container-images.lock.json
```

---

## 20.1 Contenu

```text
ImageId

Repository

Tag

Digest

Purpose

Architecture

Source

License

UpdatedAt

ReviewAt
```

---

## 20.2 Aucun secret

Le lock ne contient aucun credential de registry.

---

# 21. Build multi-stage

Étapes recommandées :

```text
base SDK

restore

build

test

publish

runtime

migrator
```

---

## 21.1 Séparation

Les artefacts d’une étape ne sont copiés dans la suivante que s’ils sont nécessaires.

---

## 21.2 Image finale

Aucun cache de :

- NuGet ;
- compilation ;
- tests ;
- package manager ;

n’est copié.

---

# 22. Contexte Docker

Le contexte transmis au daemon doit être minimal.

---

## 22.1 `.dockerignore`

Il doit exclure au minimum :

```text
.git

.github

.vs

.idea

bin

obj

TestResults

coverage

App_Data

backups

exports

secrets

.env

.env.*

*.user

*.suo

artifacts privés

rapports contenant des données runtime
```

---

## 22.2 Exceptions

Les fichiers d’exemple non sensibles sont ajoutés explicitement lorsque nécessaires.

---

## 22.3 Validation

La CI vérifie que les fichiers sensibles ne sont pas inclus dans le contexte.

---

# 23. Secrets de build

Les secrets nécessaires au restore utilisent les mécanismes de secret de BuildKit ou un mécanisme équivalent.

---

## 23.1 Interdictions

Les secrets ne sont jamais transmis par :

```dockerfile
ARG NUGET_PASSWORD
ENV NUGET_PASSWORD=...
COPY NuGet.Config /root/.nuget/...
```

lorsque le fichier contient un credential réel.

---

## 23.2 Secret mount

Un credential temporaire est monté uniquement pendant l’étape concernée.

---

## 23.3 Couches

Le secret ne doit pas apparaître dans :

- layer ;
- history ;
- cache exporté ;
- logs ;
- provenance publique.

---

# 24. Restore NuGet

Le restore utilise :

```text
packages.lock.json

locked mode

feeds approuvés

versions exactes
```

---

## 24.1 Packages flottants

Les versions flottantes sont interdites dans les releases.

---

## 24.2 Feed public

L’accès à un feed public suit la politique de supply chain.

---

## 24.3 Feed privé

Le credential est limité à la lecture pour le build client.

---

## 24.4 Cache

Un cache BuildKit peut être utilisé sans être copié dans l’image finale.

---

# 25. Build déterministe

Le build doit utiliser des options cohérentes telles que :

```text
Release

ContinuousIntegrationBuild

Deterministic

PathMap ou équivalent

SourceRevisionId

SourceDateEpoch lorsque applicable
```

---

## 25.1 Chemins locaux

Les artefacts ne doivent pas intégrer :

- chemin personnel ;
- nom d’utilisateur ;
- répertoire de workstation ;
- hostname du runner.

---

## 25.2 Date dynamique

Les timestamps non fonctionnels utilisent une source déterministe, par exemple le commit source, lorsque cela est nécessaire à l’artefact.

---

# 26. Réseau pendant le build

Les étapes nécessitant le réseau sont limitées.

---

## 26.1 Restore

Le restore peut contacter les feeds approuvés.

---

## 26.2 Build, test et publish

Après restore, les étapes doivent pouvoir s’exécuter sans téléchargement externe.

---

## 26.3 Téléchargement arbitraire

Un Dockerfile de release ne doit pas exécuter :

```text
curl ... | sh

wget d’un binaire non vérifié

installation depuis une branche main
```

---

## 26.4 Artefact externe

Un artefact externe nécessaire doit être :

- versionné ;
- hashé ;
- déclaré ;
- scanné ;
- obtenu depuis une source approuvée.

---

# 27. Tests dans le pipeline

La CI exécute avant publication :

- tests unitaires ;
- tests d’intégration ;
- tests PostgreSQL ;
- tests de sécurité ;
- validation Dockerfile ;
- validation Compose.

---

## 27.1 Test stage

Un target Docker de test peut être utilisé.

---

## 27.2 Image finale

Les assemblies de tests ne sont pas copiées dans le runtime.

---

# 28. Publish .NET

Le publish utilise :

```text
dotnet publish
--configuration Release
--no-restore
```

avec les propriétés approuvées.

---

## 28.1 Self-contained

La publication self-contained n’est pas la baseline initiale.

---

## 28.2 Framework-dependent

La baseline utilise le runtime officiel correspondant exactement à la version cible.

---

## 28.3 Trimming

Le trimming n’est pas activé globalement par défaut en raison des usages possibles de :

- Blazor ;
- Identity ;
- EF Core ;
- réflexion ;
- serialization.

Son activation nécessite des tests spécifiques.

---

## 28.4 Native AOT

Native AOT n’est pas une exigence du MVP.

---

# 29. Symboles et informations de debug

Les symboles de production sont générés comme artefact séparé lorsque nécessaires.

---

## 29.1 Image runtime

L’image ne contient pas par défaut :

- source ;
- source maps sensibles ;
- fichiers de solution ;
- symboles complets ;
- documentation interne.

---

## 29.2 Stockage

Les symboles sont conservés dans un stockage privé avec :

- version ;
- commit ;
- hash ;
- rétention.

---

## 29.3 Stack traces

Les logs de production restent utilisables sans afficher de chemins personnels.

---

# 30. Assemblies et fichiers runtime

L’image finale ne contient que :

```text
assemblies publiées

configuration non sensible nécessaire

assets statiques

certificats racine du système

bibliothèques natives requises

health probe

métadonnées de release
```

---

## 30.1 Fichiers exclus

```text
*.cs

*.csproj non nécessaire

*.sln

*.slnx

tests

docs internes

.git

NuGet.Config sensible

Dockerfile

docker-compose de production
```

---

# 31. Utilisateur non-root

L’image crée ou utilise un utilisateur dédié.

Exemple conceptuel :

```text
UID 10001
GID 10001
```

---

## 31.1 Identité stable

L’UID et le GID sont fixes afin de préparer correctement les volumes.

---

## 31.2 Nom

Exemple conceptuel :

```text
sitefactory
```

---

## 31.3 Instruction Docker

L’image finale utilise :

```dockerfile
USER 10001:10001
```

ou un utilisateur non-root officiel équivalent.

---

# 32. Propriété des fichiers

Les fichiers runtime appartiennent à l’utilisateur applicatif ou sont lisibles sans écriture.

---

## 32.1 `COPY --chown`

Les copies vers l’image finale utilisent un propriétaire explicite lorsque nécessaire.

---

## 32.2 Permissions

La baseline privilégie :

```text
répertoires : 0750 ou plus restrictif

fichiers : 0640 ou plus restrictif

exécutables : 0750
```

selon le besoin.

---

## 32.3 Interdiction

```text
chmod -R 777
```

est interdit.

---

# 33. Umask

Le processus utilise une umask restrictive.

Baseline conceptuelle :

```text
027
```

---

## 33.1 Fichiers runtime

Les fichiers créés ne doivent pas être lisibles par tous les utilisateurs du système par défaut.

---

# 34. Port applicatif

L’application écoute sur un port interne non privilégié.

Baseline :

```text
8080
```

---

## 34.1 Caddy

Caddy reverse-proxy vers ce port sur le réseau Docker interne.

---

## 34.2 Pas de capability

L’application n’a pas besoin de :

```text
CAP_NET_BIND_SERVICE
```

---

## 34.3 Publication

Le service web ne publie pas directement le port sur Internet.

---

# 35. Entrypoint

L’entrypoint utilise la forme exec.

Exemple conceptuel :

```dockerfile
ENTRYPOINT ["/app/client-app"]
CMD ["web"]
```

---

## 35.1 Pas de shell

La forme suivante est interdite :

```dockerfile
ENTRYPOINT sh -c "..."
```

dans la baseline de production.

---

## 35.2 Raisons

- propagation correcte des signaux ;
- absence d’expansion inattendue ;
- moins de surface d’injection ;
- processus .NET comme PID principal.

---

# 36. Modes applicatifs

L’exécutable comprend des modes explicites :

```text
web

worker

healthcheck

version
```

---

## 36.1 Migration

Les migrations utilisent l’image ou l’exécutable migrator distinct.

---

## 36.2 Commande inconnue

Une commande inconnue échoue sans lancer le serveur.

---

## 36.3 Aucune exécution arbitraire

L’entrypoint n’accepte pas :

- script shell ;
- commande OS ;
- assembly arbitraire ;
- SQL arbitraire.

---

# 37. SDK absent du runtime

La commande suivante ne doit pas être disponible dans l’image finale :

```text
dotnet build
```

---

## 37.1 Runtime

Le host .NET nécessaire à l’exécution peut rester présent pour une publication framework-dependent.

---

## 37.2 Tooling EF

Les commandes :

```text
dotnet ef
```

ne sont pas installées dans l’image web ou worker.

---

# 38. Shell et outils système

Le profil minimal ne contient pas :

- Bash ;
- curl ;
- wget ;
- Git ;
- SSH client ;
- netcat ;
- ping ;
- package manager ;
- éditeur ;
- compilateur.

---

## 38.1 Exception

Une dépendance runtime peut imposer certains composants.

L’exception doit être :

- minimale ;
- documentée ;
- scannée ;
- limitée à une image spécifique.

---

## 38.2 Diagnostic

Le diagnostic ordinaire utilise :

- logs ;
- traces ;
- métriques ;
- health checks ;
- commandes applicatives bornées.

---

# 39. Package manager

Un package manager ne doit pas être disponible dans l’image runtime lorsque le profil minimal le permet.

---

## 39.1 Installation au démarrage

L’installation d’un package au lancement est interdite.

---

## 39.2 Correctif

Toute correction produit une nouvelle image.

---

# 40. Root filesystem en lecture seule

Les services applicatifs utilisent :

```yaml
read_only: true
```

---

## 40.1 Écritures autorisées

Les écritures se limitent à :

```text
App_Data

Data Protection Keys lorsque nécessaires

tmpfs

volumes d’exports ou backups selon le service
```

---

## 40.2 Test

L’application doit démarrer avec le root filesystem en lecture seule.

---

## 40.3 Échec d’écriture inattendue

Une écriture hors des chemins prévus constitue une erreur à corriger, pas une raison pour désactiver `read_only`.

---

# 41. Répertoires persistants

Les données persistantes utilisent des volumes nommés.

Exemples :

```text
app_data

data_protection_keys

postgres_data

caddy_data

caddy_config
```

---

## 41.1 Couche du conteneur

Aucune donnée importante ne doit uniquement résider dans la couche inscriptible du conteneur.

---

## 41.2 Isolation

Les volumes sont distincts par :

- client ;
- environnement ;
- responsabilité.

---

# 42. App_Data

Le volume `App_Data` peut contenir :

- médias ;
- exports temporaires ;
- artefacts opérationnels autorisés ;
- fichiers de réconciliation.

---

## 42.1 Permissions

Le volume est préparé avec l’UID et GID du conteneur.

---

## 42.2 Pas de `777`

Un problème de permissions doit être corrigé par :

- ownership ;
- groupe ;
- ACL ;
- procédure d’initialisation.

---

## 42.3 Backup

Le volume suit l’ADR-0016 et les runbooks de backup.

---

# 43. Data Protection Keys

Les clés ASP.NET Core utilisent un volume dédié.

---

## 43.1 Web

Le service web possède l’accès nécessaire.

---

## 43.2 Worker

Le worker ne reçoit ce volume que si une fonctionnalité le nécessite explicitement.

---

## 43.3 Isolation

Les clés ne sont jamais intégrées à l’image.

---

# 44. Répertoires temporaires

Les fichiers temporaires utilisent un tmpfs.

Exemple conceptuel :

```yaml
tmpfs:
  - /tmp:size=64m,mode=1770,noexec,nosuid,nodev
```

---

## 44.1 Taille

La taille est bornée par service.

---

## 44.2 Données volumineuses

Les traitements nécessitant plus d’espace utilisent un volume temporaire dédié et soumis à quota.

---

## 44.3 Nettoyage

Un redémarrage supprime le contenu tmpfs.

---

# 45. Options `noexec`, `nosuid` et `nodev`

Elles sont utilisées lorsque le moteur et le type de montage le permettent.

---

## 45.1 Limite

Elles complètent les contrôles applicatifs.

Elles ne remplacent pas :

- validation des fichiers ;
- contrôle des types ;
- sandbox des traitements.

---

# 46. Capabilities Linux

Les services applicatifs utilisent :

```yaml
cap_drop:
  - ALL
```

---

## 46.1 Ajout de capability

Aucune capability n’est ajoutée par défaut.

---

## 46.2 Exception

Une capability supplémentaire exige :

- justification ;
- service concerné ;
- threat model ;
- test ;
- date de revue.

---

## 46.3 Caddy

Caddy doit préférentiellement écouter sur des ports internes supérieurs à 1024 avec mapping hôte :

```text
80:8080

443:8443
```

afin d’éviter `NET_BIND_SERVICE`.

---

# 47. `no-new-privileges`

Tous les services utilisent :

```yaml
security_opt:
  - no-new-privileges:true
```

---

## 47.1 Objectif

Empêcher un binaire d’acquérir de nouveaux privilèges via :

- setuid ;
- setgid ;
- file capabilities.

---

# 48. Mode privileged

La configuration suivante est interdite :

```yaml
privileged: true
```

---

## 48.1 Exception

Aucune exception n’est prévue pour les services SiteFactory du MVP.

---

# 49. Docker socket

Le montage suivant est interdit :

```text
/var/run/docker.sock
```

---

## 49.1 Raisons

Il permettrait au conteneur :

- de lancer des conteneurs privilégiés ;
- de monter l’hôte ;
- d’extraire des secrets ;
- de contourner l’isolation réseau.

---

# 50. Namespaces hôte

Les options suivantes sont interdites :

```text
network_mode: host

pid: host

ipc: host
```

---

## 50.1 Raisons

Elles affaiblissent l’isolation et augmentent la visibilité sur l’hôte.

---

# 51. Devices

Aucun device hôte n’est monté dans les conteneurs standards.

---

## 51.1 GPU futur

Un traitement GPU nécessitera :

- image dédiée ;
- runtime dédié ;
- devices explicites ;
- limites ;
- nouvel examen de sécurité.

---

# 52. Seccomp

Le profil seccomp par défaut du moteur de conteneurs reste actif.

---

## 52.1 Unconfined

La configuration suivante est interdite :

```text
seccomp=unconfined
```

---

## 52.2 Profil personnalisé

Un profil plus strict pourra être introduit après observation des syscalls nécessaires.

---

# 53. AppArmor et SELinux

Lorsque l’hôte les supporte, un profil peut compléter le durcissement.

---

## 53.1 Baseline

Ils ne sont pas obligatoires pour le premier profil VPS.

---

## 53.2 Désactivation

Le conteneur ne doit pas demander la désactivation des politiques de l’hôte sans justification.

---

# 54. Ressources CPU et mémoire

Chaque service possède des limites ou réservations documentées.

---

## 54.1 Services

- web ;
- worker ;
- migrator ;
- PostgreSQL ;
- Caddy.

---

## 54.2 Worker média

Les transformations d’images utilisent une concurrence et une mémoire bornées.

---

## 54.3 OOM

Un dépassement doit produire :

- redémarrage contrôlé ;
- métrique ;
- alerte ;
- diagnostic.

---

# 55. Limite de processus

Les services utilisent une limite de PIDs adaptée.

Exemple conceptuel :

```yaml
pids_limit: 128
```

La valeur exacte dépend du service.

---

## 55.1 Fork bomb

La limite réduit l’impact d’une création excessive de processus.

---

## 55.2 .NET

La valeur doit rester compatible avec :

- threads runtime ;
- diagnostics ;
- bibliothèques natives.

---

# 56. Ulimits

Les limites pertinentes peuvent inclure :

```text
nofile

nproc

core
```

---

## 56.1 Core dumps

Les core dumps sont désactivés par défaut :

```text
core = 0
```

---

## 56.2 Données sensibles

Un core dump peut contenir :

- secrets ;
- données personnelles ;
- contenu de requêtes ;
- clés.

---

# 57. Diagnostics runtime .NET

Les interfaces de diagnostic invasives sont désactivées par défaut en production lorsqu’elles ne sont pas nécessaires.

---

## 57.1 Profil standard

La configuration doit empêcher l’ouverture involontaire d’un diagnostic port accessible.

---

## 57.2 Observabilité

OpenTelemetry instrumenté dans l’application reste le mécanisme ordinaire.

---

## 57.3 Profil break-glass

Un diagnostic avancé nécessite :

- configuration temporaire ;
- fenêtre limitée ;
- accès opérateur ;
- destination protégée ;
- audit ;
- retour au profil standard.

---

# 58. Processus enfants

L’application ne doit pas lancer de processus arbitraires.

---

## 58.1 Traitement natif

Si une fonctionnalité nécessite un binaire externe :

- image worker dédiée ;
- commande fixe ;
- arguments structurés ;
- aucun shell ;
- timeout ;
- sandbox ;
- quotas ;
- fichiers non fiables validés.

---

## 58.2 Préférence

Privilégier les bibliothèques managées ou natives appelées directement plutôt que des commandes shell.

---

# 59. Traitement des médias

Les opérations média suivent l’ADR-0016.

---

## 59.1 Image dédiée

Une image `media-worker` distincte peut être utilisée si elle nécessite :

- codecs ;
- bibliothèques natives ;
- outils plus volumineux.

---

## 59.2 Isolation

Cette image ne doit pas devenir l’image web générale.

---

## 59.3 Ressources

Elle possède :

- CPU borné ;
- mémoire bornée ;
- tmpfs borné ;
- aucune exposition réseau non nécessaire ;
- aucun accès aux secrets web.

---

# 60. Logs

Les conteneurs écrivent les logs applicatifs vers :

```text
stdout

stderr
```

---

## 60.1 Fichiers de logs

L’application ne conserve pas des logs permanents dans l’image.

---

## 60.2 Rotation

La rotation des logs Docker appartient à l’exploitation de l’hôte.

---

## 60.3 Données sensibles

Les règles de l’ADR-0010 restent applicables.

---

# 61. Health checks

Les services disposent de checks adaptés.

---

## 61.1 Web

```text
liveness
readiness
```

---

## 61.2 Worker

```text
process liveness
PostgreSQL access
handler compatibility
queue readiness
```

---

## 61.3 Migrator

Le migrator retourne un code de sortie et un rapport.

Il ne possède pas de health check permanent.

---

# 62. Health probe native

L’image ne dépend pas de `curl` ou `wget` pour vérifier sa santé.

---

## 62.1 Mode dédié

Exemple conceptuel :

```text
/app/client-app healthcheck --mode web
```

---

## 62.2 Endpoint

La probe peut interroger un endpoint interne avec :

- timeout court ;
- réponse bornée ;
- aucune authentification sensible.

---

## 62.3 Secret

Aucun credential n’est transmis dans la ligne de commande.

---

# 63. Liveness

La liveness indique uniquement que le processus peut continuer à fonctionner.

---

## 63.1 Dépendances externes

Une panne CRM ou SMTP ne rend pas le web non-live.

---

## 63.2 Redémarrage en boucle

Un contrôle trop strict ne doit pas provoquer une boucle de redémarrage inutile.

---

# 64. Readiness

La readiness indique si le service peut recevoir du trafic ou des jobs.

---

## 64.1 Web

Peut dépendre de :

- configuration valide ;
- PostgreSQL accessible ;
- migrations compatibles ;
- volumes accessibles ;
- Data Protection disponible.

---

## 64.2 Worker

Peut dépendre de :

- PostgreSQL ;
- contrats de jobs ;
- storage requis ;
- migrations compatibles.

---

## 64.3 Provider externe

Un provider tiers indisponible ne rend pas nécessairement tout le service non-ready.

---

# 65. Health checks et données

Les endpoints de santé ne doivent pas exposer :

- chaîne de connexion ;
- version détaillée de packages ;
- secrets ;
- hostname interne ;
- exception complète ;
- données personnelles.

---

# 66. Arrêt gracieux

Les conteneurs reçoivent :

```text
SIGTERM
```

---

## 66.1 Web

Le web :

- cesse d’accepter de nouvelles requêtes ;
- termine les requêtes courtes ;
- ferme les connexions ;
- arrête proprement.

---

## 66.2 Worker

Le worker suit l’ADR-0017 :

- ne revendique plus de jobs ;
- annule les handlers ;
- laisse les leases reprendre en cas d’interruption.

---

## 66.3 Délai

Docker Compose définit un :

```text
stop_grace_period
```

adapté.

---

# 67. Signal et PID 1

Le processus applicatif reçoit directement les signaux.

---

## 67.1 Shell wrapper

Un wrapper shell qui absorbe les signaux est interdit.

---

## 67.2 Init

`init: true` peut être activé si le service crée réellement des processus enfants nécessitant un reaping.

Il n’est pas obligatoire pour les services n’en créant pas.

---

# 68. Politique de redémarrage

Les services utilisent une policy adaptée.

Exemple :

```text
unless-stopped
```

ou une policy explicitement définie.

---

## 68.1 Migrator

L’image de migration ne possède pas de restart automatique.

---

## 68.2 Échec permanent

Un conteneur échouant pour une configuration invalide ne doit pas redémarrer indéfiniment sans alerte.

---

# 69. Configuration runtime

La configuration d’environnement est injectée au démarrage.

---

## 69.1 Image unique

La même image fonctionne en :

- staging ;
- production ;

avec des configurations différentes.

---

## 69.2 Image spécialisée par environnement

Les images suivantes sont interdites :

```text
client-app:staging-build

client-app:production-build
```

lorsqu’elles sont reconstruites depuis la même source uniquement pour changer la configuration.

---

## 69.3 Assets client

Les éléments structurels propres au client sont compilés dans son image.

Les secrets et endpoints environnementaux restent runtime.

---

# 70. Secrets runtime

Les secrets suivent l’ADR-0012.

---

## 70.1 Préférence

Privilégier :

- fichiers de secrets montés en lecture seule ;
- secret store ;
- mécanisme Docker approuvé.

---

## 70.2 Variables d’environnement

Elles peuvent être utilisées lorsque le profil le permet, mais elles ne doivent pas :

- être intégrées à l’image ;
- apparaître dans les logs ;
- être transmises à des processus enfants inutiles.

---

## 70.3 Build arguments

Un secret runtime n’est jamais un build argument.

---

# 71. Montages de secrets

Les secrets sont montés :

- en lecture seule ;
- avec permissions restrictives ;
- uniquement dans les services concernés.

---

## 71.1 Caddy

Caddy ne reçoit pas les credentials PostgreSQL.

---

## 71.2 Web

Le web ne reçoit pas les credentials de backup.

---

## 71.3 Worker

Le worker ne reçoit que les credentials des intégrations qu’il exécute.

---

# 72. Compose hardening

Exemple conceptuel pour le service web :

```yaml
services:
  web:
    image: registry.example/client-app@sha256:<digest>
    user: "10001:10001"
    read_only: true
    cap_drop:
      - ALL
    security_opt:
      - no-new-privileges:true
    tmpfs:
      - /tmp:size=64m,mode=1770,noexec,nosuid,nodev
    volumes:
      - app_data:/app/App_Data
      - data_protection_keys:/app/Keys
    expose:
      - "8080"
    pids_limit: 128
    restart: unless-stopped
```

---

## 72.1 Exemple non contractuel

Les valeurs exactes sont résolues par le profil de déploiement.

---

## 72.2 Port public

Seul Caddy publie les ports hôte nécessaires.

---

# 73. Service worker

Le worker utilise une configuration similaire :

```yaml
services:
  worker:
    image: registry.example/client-app@sha256:<digest>
    command:
      - worker
    user: "10001:10001"
    read_only: true
    cap_drop:
      - ALL
    security_opt:
      - no-new-privileges:true
```

---

## 73.1 Volumes

Le worker reçoit uniquement :

- App_Data si nécessaire ;
- stockage temporaire ;
- secrets d’intégration nécessaires.

---

## 73.2 Port

Il ne publie aucun port Internet.

---

# 74. Service migrator

Le migrator est exécuté explicitement.

Exemple conceptuel :

```yaml
services:
  migrator:
    image: registry.example/client-migrator@sha256:<digest>
    profiles:
      - operations
    restart: "no"
    read_only: true
    cap_drop:
      - ALL
    security_opt:
      - no-new-privileges:true
```

---

## 74.1 Réseau

Il peut joindre PostgreSQL.

Il ne reçoit pas un egress Internet général.

---

## 74.2 Secrets

Il reçoit uniquement le credential PostgreSQL de migration approprié.

---

## 74.3 Démarrage

Le migrator n’est pas une dépendance automatique de `web`.

---

# 75. Rôles PostgreSQL

Le web, le worker et le migrator peuvent utiliser des rôles PostgreSQL différents.

---

## 75.1 Runtime

Le rôle runtime ne doit pas posséder les privilèges de migration.

---

## 75.2 Migrator

Le rôle migrator possède uniquement les privilèges nécessaires à la migration.

---

## 75.3 Décision associée

Le détail des rôles PostgreSQL pourra être formalisé dans un ADR dédié.

---

# 76. Caddy

L’image Caddy est :

- officielle ou construite depuis une base approuvée ;
- épinglée par digest ;
- scannée ;
- non privilégiée lorsque possible ;
- limitée à ses volumes ;
- séparée de PostgreSQL.

---

## 76.1 Ports

Privilégier des ports internes non privilégiés avec mappings hôte.

---

## 76.2 Volumes

Caddy possède uniquement :

- configuration ;
- certificats ;
- données ACME.

---

## 76.3 App_Data

Caddy ne monte pas directement les médias ou documents privés dans la baseline.

---

# 77. PostgreSQL

L’image PostgreSQL est :

- officielle ou approuvée ;
- épinglée par digest ;
- scannée ;
- isolée sur le réseau interne ;
- sans port public ;
- dotée d’un volume persistant.

---

## 77.1 Root filesystem

Le durcissement read-only est appliqué uniquement après avoir déclaré tous les répertoires d’écriture nécessaires.

---

## 77.2 Temporaire

Les répertoires temporaires requis peuvent utiliser tmpfs.

---

## 77.3 Outils

L’image PostgreSQL ne doit pas être utilisée comme conteneur d’administration général.

---

# 78. Images tierces

Toute image tierce du Compose doit posséder :

```text
Image ID

version

digest

source

licence

SBOM ou inventaire disponible

scan

owner

review date
```

---

## 78.1 Image inconnue

Une image provenant d’un registry ou auteur non approuvé est refusée.

---

## 78.2 Tag mutable

Une image tierce n’est pas déployée uniquement par tag.

---

# 79. OCI metadata

Les images applicatives utilisent les labels OCI pertinents :

```text
org.opencontainers.image.title

org.opencontainers.image.description

org.opencontainers.image.source

org.opencontainers.image.revision

org.opencontainers.image.version

org.opencontainers.image.licenses

org.opencontainers.image.vendor
```

---

## 79.1 Données interdites

Les labels ne contiennent pas :

- secret ;
- chemin local ;
- nom d’utilisateur ;
- credential ;
- donnée client privée.

---

## 79.2 Timestamp

Le timestamp de création doit être produit de manière cohérente avec la politique de reproductibilité.

---

# 80. Tags d’image

Tags autorisés comme alias humains :

```text
application version

commit SHA

release identifier
```

Exemples conceptuels :

```text
client-app:1.4.0

client-app:sha-abc1234
```

---

## 80.1 Déploiement

Le Compose déploie :

```text
repository@sha256:digest
```

---

## 80.2 `latest`

`latest` est interdit dans :

- production ;
- staging de validation ;
- rapports de release ;
- runbooks.

---

# 81. Promotion

Le flux est :

```text
build une seule fois

tester

signer

déployer le digest en staging

valider

promouvoir le même digest en production
```

---

## 81.1 Rebuild

Une reconstruction depuis le même commit produit un nouvel artefact qui doit être :

- rescanné ;
- resigné ;
- réidentifié.

Elle ne remplace pas silencieusement le digest validé.

---

# 82. Registry

Les images de production sont publiées dans un registry contrôlé.

---

## 82.1 Accès CI

Le pipeline possède des permissions minimales :

- push sur le repository d’image concerné ;
- aucune administration globale.

---

## 82.2 Accès VPS

Le VPS possède un accès :

```text
pull only
```

lorsque le registry le permet.

---

## 82.3 Credential

Le credential du registry suit l’ADR-0012.

---

# 83. Immutabilité du registry

Les tags de release ne doivent pas être déplacés après publication.

---

## 83.1 Protection

Utiliser lorsque disponible :

- tag immutability ;
- retention rules ;
- repository permissions ;
- audit.

---

## 83.2 Suppression

La suppression d’un digest utilisé en production est interdite avant expiration de la période de rollback.

---

# 84. SBOM

Chaque image de release produit une SBOM.

Formats acceptables :

```text
CycloneDX

SPDX
```

---

## 84.1 Couverture

La SBOM doit inclure lorsque possible :

- packages OS ;
- runtime .NET ;
- packages NuGet ;
- bibliothèques natives ;
- application ;
- licences.

---

## 84.2 Publication

La SBOM est :

- attachée comme attestation OCI ;
- ou stockée comme artefact de release vérifiable.

---

## 84.3 Secret

La SBOM ne contient aucun credential ou chemin local sensible.

---

# 85. Provenance de build

La CI produit une attestation décrivant :

```text
repository

commit

workflow

runner ou builder

Dockerfile

base image digests

build parameters non sensibles

output digest
```

---

## 85.1 Builder autorisé

Une release stable doit provenir d’un workflow approuvé.

---

## 85.2 Build local

Une image construite localement peut servir au développement.

Elle ne devient pas une release stable sans passer par la chaîne approuvée.

---

# 86. Signature d’image

Les digests de release sont signés.

---

## 86.1 Approches

La signature peut utiliser :

- identité CI sans clé longue durée ;
- clé d’organisation protégée ;
- mécanisme OCI compatible.

---

## 86.2 Cible

La signature porte sur le digest, pas uniquement sur le tag.

---

## 86.3 Attestations

Les SBOM et provenances sont également liées au digest.

---

# 87. Vérification avant déploiement

Le processus opérateur vérifie :

```text
digest attendu

signature valide

identité du signataire

provenance approuvée

SBOM disponible

scan accepté

client et version

architecture
```

---

## 87.1 Compose

Docker Compose ne constitue pas à lui seul la vérification de signature.

La vérification est effectuée par :

- pipeline de déploiement ;
- outil opérateur ;
- script approuvé.

---

## 87.2 Échec

Une signature absente ou invalide bloque la production stable.

---

# 88. Scan de vulnérabilités

Les images sont scannées après construction.

---

## 88.1 Couverture

- base OS ;
- runtime ;
- packages NuGet lorsque détectables ;
- bibliothèques natives ;
- configuration de l’image.

---

## 88.2 Source de vérité

Le scan complète :

- Dependabot ou équivalent ;
- audit NuGet ;
- CodeQL ;
- scanners de packages.

---

## 88.3 Re-scan

Les images encore déployables sont rescannées périodiquement, car de nouvelles vulnérabilités peuvent être publiées après le build.

---

# 89. Politique de vulnérabilités

Une release est bloquée lorsqu’une vulnérabilité dépasse les seuils définis et qu’aucune dérogation valide n’existe.

---

## 89.1 Évaluation

L’évaluation tient compte :

- de la sévérité ;
- de l’exploitabilité ;
- de la présence réelle du composant ;
- de la fonction utilisée ;
- de l’exposition ;
- de la correction disponible.

---

## 89.2 Pas de seuil unique aveugle

Un score seul ne remplace pas l’analyse.

---

## 89.3 Vulnérabilité critique exploitable

Elle bloque la production sauf procédure exceptionnelle documentée.

---

# 90. Dérogations de sécurité

Une dérogation doit contenir :

```text
Vulnerability ID

Image digest

Justification

Exploitability assessment

Compensating controls

Owner

ApprovedAt

ExpiresAt

Remediation task
```

---

## 90.1 Durée

Une dérogation possède une date d’expiration.

---

## 90.2 Audit

L’approbation est auditée.

---

## 90.3 Portée

La dérogation s’applique à un digest ou ensemble explicitement défini.

---

# 91. Secret scanning de l’image

La CI vérifie :

- filesystem extrait ;
- layers ;
- history ;
- labels ;
- environment metadata ;
- artefacts publiés.

---

## 91.1 Marqueurs

Les tests utilisent des secrets factices afin de prouver leur absence.

---

## 91.2 Finding

Un secret réel ou suspect bloque la publication.

---

# 92. Licences

La SBOM alimente la vérification des licences.

---

## 92.1 Dépendance inconnue

Un composant sans licence identifiable ou incompatible doit être revu.

---

## 92.2 Redistribution

L’image transférée au client doit respecter les obligations de redistribution des composants inclus.

---

# 93. Taille de l’image

La taille est suivie comme indicateur de surface et de coût.

---

## 93.1 Budget

Chaque profil peut définir un budget indicatif.

---

## 93.2 Hausse

Une hausse significative doit être expliquée par :

- nouvelle dépendance ;
- nouveau runtime ;
- bibliothèque native ;
- assets.

---

## 93.3 Compression

Une petite image n’est pas automatiquement sûre.

Le contenu et les permissions restent prioritaires.

---

# 94. Inspection de configuration

La CI vérifie l’image finale :

```text
User non-root

Entrypoint exec

Port non privilégié

Aucun secret ENV

Aucun health check shell inattendu

Labels attendus

Layers attendus

Aucun SDK

Aucun package manager
```

---

# 95. Scan de configuration Compose

La CI vérifie :

```text
aucun privileged

aucun Docker socket

aucun host network

cap_drop ALL

no-new-privileges

read_only

volumes explicites

secrets read-only

PostgreSQL non publié

images épinglées par digest
```

---

# 96. Démarrage sans privilège

Les tests doivent démarrer l’application avec :

```text
UID non-root

root filesystem read-only

capabilities vides

tmpfs borné

volumes vides préparés
```

---

## 96.1 Résultat

Le démarrage doit réussir sans correction manuelle dans le conteneur.

---

# 97. Tests de permissions

Vérifier :

- lecture des assemblies ;
- écriture App_Data ;
- écriture Data Protection ;
- refus d’écriture `/app` ;
- refus d’écriture `/etc` ;
- refus d’écriture `/root` ;
- absence d’accès aux volumes non montés.

---

# 98. Tests d’absence d’outils

Vérifier l’absence ou l’inutilisabilité de :

```text
bash

sh lorsque le profil est chiseled

curl

wget

git

ssh

apt

apk

dnf

gcc

dotnet SDK

docker
```

---

## 98.1 Profil compatibility

Lorsqu’un shell existe dans une image de compatibilité, sa présence doit être justifiée et compensée.

---

# 99. Tests de réseau

Vérifier :

- service web accessible uniquement via Caddy ;
- worker sans port public ;
- PostgreSQL sans port public ;
- migrator sans port ;
- réseaux internes ;
- aucun host network ;
- egress conforme à l’ADR-0025.

---

# 100. Tests des volumes

Vérifier :

- données persistantes après recréation du conteneur ;
- absence dans la couche de l’image ;
- ownership ;
- séparation staging/production ;
- séparation clients ;
- backup ;
- restauration.

---

# 101. Tests d’arrêt

Tester :

- SIGTERM web ;
- SIGTERM worker ;
- requête en cours ;
- job en cours ;
- expiration du délai ;
- redémarrage ;
- reprise après lease.

---

# 102. Tests de health checks

Couvrir :

- application saine ;
- PostgreSQL indisponible ;
- volume absent ;
- migrations incompatibles ;
- provider externe indisponible ;
- worker sans handler ;
- probe timeout.

---

# 103. Tests du migrator

Vérifier :

```text
aucun démarrage automatique

aucun port publié

plan en lecture seule

apply explicite

mauvaise base refusée

mauvais environnement refusé

migration échouée

rapport produit

code de sortie non nul
```

---

# 104. Tests de reproductibilité

Deux builds contrôlés depuis les mêmes entrées doivent produire des artefacts fonctionnellement identiques.

---

## 104.1 Sources de variation

Vérifier :

- ordre des fichiers ;
- timestamps ;
- chemins ;
- UID ;
- mode des fichiers ;
- metadata OCI ;
- base image digest ;
- architecture.

---

## 104.2 Digest identique

Le digest byte-for-byte identique est un objectif lorsque la chaîne le permet.

Toute variation connue doit être documentée.

---

# 105. Tests de signature

Vérifier :

- signature valide ;
- signature absente ;
- mauvais signataire ;
- digest modifié ;
- attestation d’un autre digest ;
- clé révoquée ;
- identity CI inattendue.

---

# 106. Tests de SBOM

Vérifier :

- présence ;
- format ;
- digest associé ;
- packages principaux ;
- absence de secret ;
- version applicative ;
- licences.

---

# 107. Tests de scans

Les tests de pipeline doivent couvrir :

- vulnérabilité bloquante ;
- dérogation valide ;
- dérogation expirée ;
- secret détecté ;
- base inconnue ;
- package interdit ;
- licence bloquée.

---

# 108. Multi-architecture

La baseline de production initiale cible :

```text
linux/amd64
```

---

## 108.1 ARM64

`linux/arm64` peut être ajouté avec :

- build natif ou contrôlé ;
- tests ;
- SBOM par architecture ;
- signature du manifest ;
- validation des bibliothèques natives.

---

## 108.2 Manifest list

Chaque architecture possède son propre digest.

Le déploiement doit connaître l’architecture ciblée.

---

# 109. Cross-compilation

L’émulation peut être utilisée pour des tests ou builds contrôlés.

---

## 109.1 Release

Une release multi-architecture doit être testée sur l’architecture réelle lorsque le risque le justifie.

---

# 110. Mise à jour des images de base

Une tâche récurrente doit identifier :

- nouveau digest ;
- correctif de sécurité ;
- fin de support ;
- changement de distribution ;
- nouvelle version runtime.

---

## 110.1 Mise à jour automatique

La détection peut être automatique.

La publication et le déploiement restent explicites.

---

## 110.2 Rebuild sans code

Un changement de base peut justifier une nouvelle release d’image avec la même version fonctionnelle mais un identifiant de build distinct.

---

# 111. Support des images

Une image possède un état :

```text
current

supported

deprecated

retired
```

---

## 111.1 Retired

Une image retirée ne peut plus être sélectionnée pour un nouveau déploiement.

---

## 111.2 Déploiement existant

Une image retirée encore déployée produit une alerte et un plan de migration.

---

# 112. Rollback

Le rollback applicatif utilise un digest antérieur déjà vérifié.

---

## 112.1 Conditions

Avant rollback :

- compatibilité de base ;
- migrations appliquées ;
- configuration ;
- volumes ;
- secrets ;
- jobs en attente.

---

## 112.2 Base de données

Une migration non rétrocompatible peut rendre le rollback de l’image impossible.

---

## 112.3 Rapport

Le plan de release doit indiquer la compatibilité de rollback.

---

# 113. Debug en production

L’image de production n’est pas modifiée pour ajouter des outils pendant un incident.

---

## 113.1 `docker exec`

L’absence de shell est acceptée.

---

## 113.2 Diagnostics ordinaires

Utiliser :

- logs ;
- traces ;
- métriques ;
- endpoints de santé ;
- commandes applicatives ;
- inspections Docker côté hôte.

---

## 113.3 Debug exceptionnel

Un profil break-glass peut utiliser une image de diagnostic séparée :

- construite et signée ;
- sans données par défaut ;
- durée limitée ;
- réseau limité ;
- montage en lecture seule ;
- audit.

---

## 113.4 Image debug

Elle n’est jamais utilisée comme image de production permanente.

---

# 114. SSH dans les conteneurs

Aucun serveur SSH n’est installé dans les images SiteFactory.

---

## 114.1 Accès opérateur

L’accès se fait au VPS, puis via les commandes Docker autorisées.

---

# 115. Mise à jour en place

La modification d’un conteneur en cours est interdite.

Exemples :

```text
apt install dans le conteneur

copie manuelle d’une DLL

édition d’un appsettings

patch d’un script
```

---

## 115.1 Correction

Toute modification produit :

```text
nouveau commit
→ nouvelle image
→ nouveaux tests
→ nouveau digest
→ nouvelle signature
→ déploiement explicite
```

---

# 116. Conteneur mutable détecté

Une dérive entre l’image et le conteneur runtime doit être traitée comme un incident ou une intervention break-glass.

---

## 116.1 Recréation

La résolution ordinaire consiste à recréer le conteneur depuis l’image approuvée.

---

# 117. Observabilité

Événements recommandés :

```text
ContainerImageBuildStarted

ContainerImageBuildCompleted

ContainerImageScanCompleted

ContainerImageScanFailed

ContainerImageSigned

ContainerImagePublished

ContainerImageVerificationFailed

ContainerStarted

ContainerReadinessFailed

ContainerExited

ContainerResourceLimitReached

ContainerBaseImageUpdateDetected
```

---

## 117.1 Propriétés autorisées

```text
ImageId

ImageDigest

ApplicationVersion

Architecture

BuildWorkflowId

Result

ErrorCode

Environment

Duration

SizeBucket
```

---

## 117.2 Données interdites

```text
Registry credential

Build secret

Environment secret

Source path personnel

Container environment complet

Command line sensible
```

---

# 118. Métriques

Exemples :

```text
container image build duration

container image size

container scan findings

container starts

container restarts

container health failures

container OOM events

container CPU usage

container memory usage
```

---

## 118.1 Labels autorisés

```text
service

image_version

architecture

result

severity

environment
```

---

## 118.2 Labels interdits

```text
container_id individuel

digest complet comme forte cardinalité permanente

secret_id

user_id

trace_id
```

---

# 119. Alertes

Alertes possibles :

```text
Image non signée

Digest inattendu

Vulnérabilité critique

Dérogation expirée

Conteneur exécuté root

Filesystem read-only désactivé

Capability ajoutée

Docker socket monté

Redémarrages fréquents

OOM

Health check en échec

Base image retirée

SBOM absente
```

---

## 119.1 Runbook

Chaque alerte critique référence une procédure.

---

# 120. Audit

Actions recommandées :

```text
container.image.promoted

container.image.deployed

container.image.rolled-back

container.base-image.changed

container.security-exception.approved

container.debug-profile.activated

container.runtime-hardening.changed

container.image-retired
```

---

## 120.1 Metadata

L’audit conserve :

- digest ;
- version ;
- environnement ;
- acteur ;
- rapport ;
- raison.

---

## 120.2 Secret

Aucune valeur de credential ou configuration sensible.

---

# 121. Manifest de release

Une release doit produire un manifest.

Nom conceptuel :

```text
container-release-manifest.json
```

---

## 121.1 Contenu

```text
SchemaVersion

ApplicationVersion

RepositoryCommit

ImageDigests

Architectures

BaseImageDigests

SbomReferences

ProvenanceReferences

SignatureReferences

ScanReportReferences

MigrationImageDigest

Compatibility

CreatedAtUtc
```

---

## 121.2 Handover

Le manifest fait partie des artefacts transférables.

---

# 122. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-IMAGE-001` | Image ID inconnue |
| `SF-IMAGE-002` | Image de base inconnue |
| `SF-IMAGE-003` | Image de base non approuvée |
| `SF-IMAGE-004` | Digest de base absent |
| `SF-IMAGE-005` | Tag flottant interdit |
| `SF-IMAGE-006` | Architecture non supportée |
| `SF-IMAGE-007` | Image SDK utilisée en production |
| `SF-IMAGE-008` | Outil de build détecté dans le runtime |
| `SF-IMAGE-009` | Source détectée dans le runtime |
| `SF-IMAGE-010` | Package manager détecté |
| `SF-IMAGE-011` | Shell inattendu détecté |
| `SF-IMAGE-012` | Taille d’image excessive |
| `SF-IMAGE-013` | OCI metadata absente |
| `SF-IMAGE-014` | Manifest de release absent |
| `SF-IMAGE-015` | Image différente entre staging et production |
| `SF-IMAGE-016` | Image retirée |
| `SF-BUILD-001` | Restore non verrouillé |
| `SF-BUILD-002` | Dépendance flottante |
| `SF-BUILD-003` | Feed non approuvé |
| `SF-BUILD-004` | Secret transmis par build argument |
| `SF-BUILD-005` | Secret présent dans une layer |
| `SF-BUILD-006` | Contexte Docker contenant un fichier interdit |
| `SF-BUILD-007` | Téléchargement externe non approuvé |
| `SF-BUILD-008` | Build non déterministe |
| `SF-BUILD-009` | Test de release échoué |
| `SF-BUILD-010` | Builder non approuvé |
| `SF-RUNTIME-001` | Conteneur exécuté root |
| `SF-RUNTIME-002` | UID runtime invalide |
| `SF-RUNTIME-003` | Root filesystem inscriptible |
| `SF-RUNTIME-004` | Capability Linux interdite |
| `SF-RUNTIME-005` | `no-new-privileges` absent |
| `SF-RUNTIME-006` | Mode privileged interdit |
| `SF-RUNTIME-007` | Docker socket monté |
| `SF-RUNTIME-008` | Host network interdit |
| `SF-RUNTIME-009` | Host PID namespace interdit |
| `SF-RUNTIME-010` | Device hôte interdit |
| `SF-RUNTIME-011` | Seccomp désactivé |
| `SF-RUNTIME-012` | Port privilégié non justifié |
| `SF-RUNTIME-013` | Volume d’écriture inconnu |
| `SF-RUNTIME-014` | Permission de volume invalide |
| `SF-RUNTIME-015` | `chmod 777` détecté |
| `SF-RUNTIME-016` | Tmpfs absent |
| `SF-RUNTIME-017` | Tmpfs non borné |
| `SF-RUNTIME-018` | Limite de ressources absente |
| `SF-RUNTIME-019` | Core dump autorisé |
| `SF-RUNTIME-020` | Diagnostic runtime non maîtrisé |
| `SF-HEALTH-001` | Health check absent |
| `SF-HEALTH-002` | Health check dépendant d’un outil absent |
| `SF-HEALTH-003` | Liveness trop stricte |
| `SF-HEALTH-004` | Readiness invalide |
| `SF-HEALTH-005` | Health endpoint divulguant des données |
| `SF-SBOM-001` | SBOM absente |
| `SF-SBOM-002` | SBOM invalide |
| `SF-SBOM-003` | SBOM ne correspondant pas au digest |
| `SF-SCAN-001` | Scan absent |
| `SF-SCAN-002` | Vulnérabilité bloquante |
| `SF-SCAN-003` | Dérogation absente |
| `SF-SCAN-004` | Dérogation expirée |
| `SF-SCAN-005` | Secret détecté dans l’image |
| `SF-SCAN-006` | Licence incompatible |
| `SF-SIGN-001` | Signature absente |
| `SF-SIGN-002` | Signature invalide |
| `SF-SIGN-003` | Signataire non approuvé |
| `SF-SIGN-004` | Provenance absente |
| `SF-SIGN-005` | Provenance incompatible |
| `SF-DEPLOY-IMAGE-001` | Déploiement par tag mutable |
| `SF-DEPLOY-IMAGE-002` | Digest inattendu |
| `SF-DEPLOY-IMAGE-003` | Image non vérifiée |
| `SF-DEPLOY-IMAGE-004` | Rollback incompatible avec la base |
| `SF-MIGRATOR-001` | Image de migration absente |
| `SF-MIGRATOR-002` | Migrator démarré automatiquement |
| `SF-MIGRATOR-003` | Migrator possédant un port public |
| `SF-MIGRATOR-004` | Credential de migration absent |
| `SF-MIGRATOR-005` | Mauvaise base cible |

---

# 123. Tests unitaires

Tester notamment :

- Image ID ;
- profils runtime ;
- digests ;
- architecture ;
- release manifest ;
- validation des tags ;
- chemins d’écriture ;
- permissions ;
- health modes ;
- erreur de commande inconnue.

---

# 124. Tests du Dockerfile

La CI doit vérifier :

```text
multi-stage

base images digests

USER non-root

ENTRYPOINT exec

aucun secret ARG

aucun package manager runtime

aucun SDK runtime

aucune source runtime

WORKDIR défini

port non privilégié
```

---

# 125. Tests du contexte de build

Injecter des fichiers marqueurs dans :

- `.env` ;
- `secrets/` ;
- `App_Data/` ;
- `.git/`.

Vérifier qu’ils n’apparaissent pas dans :

- contexte effectif ;
- layers ;
- image finale ;
- SBOM ;
- build logs.

---

# 126. Tests runtime non-root

Vérifier :

```text
id != 0

écriture /app refusée

écriture /etc refusée

écriture App_Data autorisée

binding 8080 autorisé

binding port 80 refusé sans capability
```

---

# 127. Tests read-only

Démarrer avec `read_only: true`.

Vérifier :

- web ;
- worker ;
- healthcheck ;
- logs ;
- Data Protection ;
- exports ;
- médias ;
- erreurs.

---

# 128. Tests des capabilities

Vérifier :

```text
capabilities effectives vides

aucune capability ambient

aucune file capability inattendue

no-new-privileges actif
```

---

# 129. Tests des montages

Vérifier l’absence de :

```text
Docker socket

racine hôte

/etc hôte

/home hôte

host PID

host network

devices
```

---

# 130. Tests des ressources

Simuler :

- CPU élevé ;
- mémoire élevée ;
- tmpfs saturé ;
- pids limit ;
- disque App_Data presque plein.

Vérifier :

- échec contrôlé ;
- métrique ;
- alerte ;
- absence de corruption silencieuse.

---

# 131. Tests de promotion

Vérifier :

```text
digest staging

digest production

égalité exacte

signature identique

SBOM identique

provenance identique
```

---

# 132. Tests de rollback

Déployer un digest antérieur dans un environnement isolé.

Vérifier :

- compatibilité de configuration ;
- compatibilité DB ;
- volumes ;
- jobs ;
- santé ;
- rapport.

---

# 133. Tests de debug break-glass

Vérifier :

- image distincte ;
- signature ;
- durée limitée ;
- aucun port public ;
- montages read-only ;
- audit ;
- suppression après intervention.

---

# 134. Tests multiplateformes

Les validations Dockerfile, manifests et artefacts sont exécutées sur :

```text
Windows x64

Linux x64
```

---

## 134.1 Production

Les tests runtime de :

- capabilities ;
- seccomp ;
- permissions ;
- volumes ;
- signaux ;

sont validés sur Linux.

---

# 135. Quality gates

Une contribution est bloquée lorsque :

```text
L’image finale contient le SDK.

Une image de base n’est pas épinglée par digest.

Le tag latest est utilisé.

Un secret est transmis par ARG.

Le contexte Docker contient App_Data ou .env.

Le runtime s’exécute root.

Le root filesystem est inscriptible sans justification.

Une capability est ajoutée sans décision.

no-new-privileges est absent.

Le Docker socket est monté.

Le mode privileged est utilisé.

Le runtime contient un package manager inutile.

Une migration est exécutée au démarrage.

Une image ne possède pas de SBOM.

Une image n’est pas scannée.

Une image stable n’est pas signée.

Le déploiement utilise uniquement un tag mutable.

Les tests non-root ou read-only échouent.
```

---

# 136. Quality gates de production

La production est bloquée lorsque :

```text
Le digest n’est pas dans le manifest de release.

La signature est absente ou invalide.

La provenance n’est pas approuvée.

Une vulnérabilité bloquante existe.

Une dérogation est expirée.

Le Compose ne respecte pas le profil hardened.

Le conteneur s’exécute root.

Un volume staging est monté en production.

La migration requise n’a pas été appliquée explicitement.

L’image production diffère de l’image validée en staging.

Le registry pull utilise un credential trop privilégié.

Le rollback compatible n’est pas documenté.
```

---

# 137. CI/CD

La CI doit exécuter :

```text
validation Dockerfile

validation .dockerignore

restore locked

build Release

tests

publish

build OCI

inspection runtime

secret scan

SBOM

vulnerability scan

licence scan

provenance

signature

publication

verification post-push
```

---

## 137.1 Pull request

Les pull requests non approuvées ne reçoivent pas :

- credential registry de production ;
- clé de signature ;
- secrets de feeds sensibles inutiles.

---

## 137.2 Release

Seul un workflow protégé peut publier une image stable.

---

# 138. Sécurité

## 138.1 Actifs

- source ;
- packages ;
- image ;
- registry ;
- signatures ;
- credentials ;
- données ;
- volumes ;
- hôte ;
- réseau ;
- migrations.

---

## 138.2 Menaces

- image substituée ;
- base compromise ;
- secret dans une layer ;
- exécution root ;
- évasion ;
- montage hôte ;
- package malveillant ;
- image non patchée ;
- tag déplacé ;
- build non autorisé ;
- diagnostic abusif ;
- conteneur mutable.

---

## 138.3 Contrôles

- digests ;
- lock files ;
- BuildKit secrets ;
- multi-stage ;
- non-root ;
- read-only ;
- capabilities vides ;
- seccomp ;
- volumes ;
- SBOM ;
- scans ;
- signatures ;
- provenance ;
- promotion immuable ;
- audit ;
- tests.

---

# 139. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Secret dans une image | Build secrets et secret scan |
| Image remplacée derrière un tag | Déploiement par digest |
| Build malveillant | Provenance et workflow protégé |
| Processus compromis | Non-root et capabilities vides |
| Écriture dans l’image | Root filesystem read-only |
| Contrôle de Docker depuis l’app | Aucun Docker socket |
| Outil installé après compromission | Aucun package manager |
| Compilation dans le conteneur | Aucun SDK |
| Données perdues à la recréation | Volumes persistants |
| Session invalidée à chaque déploiement | Volume Data Protection |
| Migration involontaire | Migrator explicite |
| CVE découverte après release | Re-scan et rebuild |
| Debug persistant | Image break-glass distincte |

---

# 140. Règles pour les modules

Un module nécessitant une dépendance runtime supplémentaire doit déclarer :

```text
RuntimePackageId

NativeDependencies

RequiredFiles

RequiredWritePaths

RequiredCapabilities

RequiredPorts

RequiredProcesses

RequiredNetworkAccess

RuntimeProfileCompatibility
```

---

## 140.1 Baseline

Une capability ou un processus externe requis déclenche une revue renforcée.

---

## 140.2 Interdictions

Un module ne doit pas :

- installer un package au démarrage ;
- modifier le Dockerfile sans contrat ;
- ajouter un shell script arbitraire ;
- écrire hors des paths déclarés ;
- demander privileged ;
- monter le Docker socket.

---

# 141. Règles pour les customizations

Une customization client suit les mêmes contrats.

---

## 141.1 Bibliothèque native

Elle doit fournir :

- version ;
- source ;
- licence ;
- hash ;
- scan ;
- architecture ;
- justification.

---

## 141.2 Image distincte

Une customization lourde peut nécessiter une image worker distincte au lieu d’élargir l’image web.

---

# 142. Règles pour LogicGate

LogicGate doit :

- maintenir les digests de base ;
- revoir les scans ;
- renouveler les images ;
- vérifier les signatures ;
- protéger le registry ;
- tester les restaurations ;
- maintenir les permissions de volumes ;
- conserver les manifests ;
- préparer les handovers.

---

## 142.1 LogicGate ne doit pas

- déployer `latest` ;
- modifier un conteneur en place ;
- utiliser l’image SDK pour gagner du temps ;
- ajouter `chmod 777` ;
- désactiver read-only sans analyse ;
- monter le Docker socket ;
- ignorer une dérogation expirée ;
- reconstruire différemment pour la production.

---

# 143. Règles pour Codex

Codex peut :

- créer les Dockerfiles ;
- créer les profils ;
- ajouter les tests ;
- générer les manifests ;
- ajouter les scans ;
- documenter les runbooks ;
- corriger un finding.

---

## 143.1 Codex ne doit pas

```text
Publier une image réelle

Signer une release réelle

Inventer un credential registry

Déployer en production

Ajouter privileged

Ajouter le Docker socket

Désactiver TLS du registry

Ajouter chmod 777

Utiliser latest

Désactiver les quality gates

Appliquer une migration production

Activer une dérogation de sécurité
```

---

## 143.2 Décision humaine

Une décision humaine est requise pour :

- nouvelle image de base ;
- nouvelle bibliothèque native ;
- capability supplémentaire ;
- profil compatibility ;
- image debug ;
- dérogation de vulnérabilité ;
- nouvelle autorité de signature ;
- architecture supplémentaire ;
- modification des limites de production ;
- retrait d’un digest de rollback.

---

# 144. Anti-patterns interdits

## 144.1 SDK en production

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk
```

comme stage final.

---

## 144.2 Tag flottant

```yaml
image: client-app:latest
```

---

## 144.3 Secret build arg

```dockerfile
ARG API_KEY
ENV API_KEY=$API_KEY
```

---

## 144.4 Exécution root

Absence de `USER` ou utilisation explicite de root.

---

## 144.5 `chmod 777`

Les permissions sont ouvertes pour contourner un problème de volume.

---

## 144.6 Root filesystem mutable

L’application écrit des fichiers temporaires dans `/app`.

---

## 144.7 Docker socket

Le worker lance ou inspecte d’autres conteneurs.

---

## 144.8 Migration au démarrage

Le web applique les migrations avant d’écouter.

---

## 144.9 Shell entrypoint

Un script modifie la configuration, télécharge un outil et démarre le service.

---

## 144.10 Patch manuel

Une DLL est copiée avec `docker cp` dans un conteneur actif.

---

## 144.11 Debug image en production

Une image contenant curl, Bash et le SDK devient la release permanente.

---

## 144.12 Rebuild entre environnements

Le staging et la production utilisent deux digests construits séparément.

---

# 145. Conséquences positives

La décision apporte :

- surface d’attaque réduite ;
- absence de SDK ;
- exécution non-root ;
- volumes maîtrisés ;
- promotion immuable ;
- provenance ;
- SBOM ;
- signatures ;
- scans ;
- rollback précis ;
- meilleure isolation ;
- exploitation reproductible ;
- handover plus complet ;
- détection des dérives.

---

# 146. Conséquences négatives

La décision entraîne :

- Dockerfiles plus structurés ;
- CI plus complète ;
- gestion des signatures ;
- permissions de volumes ;
- debug plus encadré ;
- gestion des digests ;
- rebuilds de sécurité ;
- tests Linux ;
- maintenance des images tierces ;
- éventuelles incompatibilités avec des bibliothèques natives.

---

# 147. Risques acceptés

## 147.1 Même noyau que l’hôte

Le conteneur partage le noyau Linux.

Réduction :

- patch du VPS ;
- non-root ;
- capabilities vides ;
- seccomp ;
- pas de montages dangereux.

---

## 147.2 Registry indisponible

Une panne du registry peut empêcher un nouveau déploiement.

Les images déjà présentes peuvent continuer à fonctionner.

---

## 147.3 Faux positifs des scanners

Un finding peut être non exploitable.

Réduction :

- analyse ;
- dérogation bornée ;
- justification ;
- re-scan.

---

## 147.4 Image minimale difficile à diagnostiquer

La production ne possède pas les outils interactifs habituels.

Réduction :

- observabilité ;
- health probes ;
- image debug séparée ;
- runbooks.

---

# 148. Mesures compensatoires

- images épinglées ;
- multi-stage ;
- locked restore ;
- BuildKit secrets ;
- non-root ;
- read-only ;
- capabilities vides ;
- no-new-privileges ;
- volumes ;
- tmpfs ;
- resource limits ;
- SBOM ;
- scans ;
- signature ;
- provenance ;
- promotion par digest ;
- audit ;
- debug break-glass.

---

# 149. Performance

## 149.1 Taille

Une image minimale réduit :

- pull ;
- démarrage ;
- stockage ;
- bande passante ;
- surface de scan.

---

## 149.2 ReadyToRun

Les optimisations de publish doivent être évaluées selon :

- taille ;
- temps de démarrage ;
- architecture ;
- reproductibilité.

Elles ne sont pas activées globalement sans mesure.

---

## 149.3 Read-only

Le filesystem read-only ne doit pas pénaliser significativement le runtime lorsque les paths temporaires sont correctement déclarés.

---

## 149.4 Scans

Les scans ajoutent du temps au pipeline, accepté pour les releases.

---

# 150. Coûts

## 150.1 Coûts supplémentaires

- registry ;
- stockage ;
- scans ;
- signature ;
- CI ;
- maintenance ;
- SBOM ;
- tests ;
- images de debug ponctuelles.

---

## 150.2 Coûts évités

- incident root ;
- fuite de secret ;
- image substituée ;
- rollback ambigu ;
- conteneur mutable ;
- package vulnérable ignoré ;
- différence staging-production ;
- diagnostic sans provenance.

---

# 151. Maintenabilité

Pour chaque image, il doit être possible de répondre :

```text
Quelle source l’a produite ?

Quel commit ?

Quel workflow ?

Quelles images de base ?

Quels digests ?

Quels packages ?

Quelle SBOM ?

Quelle signature ?

Quel utilisateur runtime ?

Quels paths sont inscriptibles ?

Quelles capabilities ?

Quel profil Compose ?

Comment la vérifier, la mettre à jour et la retirer ?
```

---

# 152. Compatibilité

## 152.1 Version .NET

Le runtime doit correspondre à la version de publication supportée.

---

## 152.2 Base image

Un changement de distribution ou de profil peut affecter :

- ICU ;
- certificats ;
- fuseaux horaires ;
- bibliothèques natives ;
- permissions.

---

## 152.3 Architectures

Chaque architecture possède ses propres digests et tests.

---

## 152.4 Compose

Le profil runtime possède une version.

---

## 152.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

La migration depuis une image root ou mutable nécessite une préparation des volumes.

---

# 153. Migration depuis une image SDK

Le processus est :

```text
inventorier les fichiers réellement nécessaires

créer les stages restore/build/test/publish

créer l’image runtime

retirer le SDK

retirer les sources

ajouter le non-root

ajouter les tests

produire SBOM et scans

forcer la reconnexion à l’image approuvée
```

---

# 154. Migration depuis une exécution root

Le processus est :

```text
choisir UID/GID

inventorier les paths d’écriture

préparer les volumes

corriger l’ownership

retirer les permissions larges

passer USER non-root

tester read-only

retirer les capabilities
```

---

## 154.1 Volumes existants

Les fichiers root existants doivent être migrés par une procédure opérateur.

---

# 155. Migration depuis des tags flottants

Le processus est :

```text
résoudre les digests actuels

enregistrer les digests

produire le manifest

signer les images approuvées ou les reconstruire

modifier Compose

tester staging

déployer explicitement
```

---

# 156. Migration depuis une image mutable

Le processus est :

```text
inventorier les modifications runtime

rapatrier les modifications légitimes dans le repository

supprimer les patches manuels

reconstruire

comparer

recréer les conteneurs

vérifier les volumes
```

---

# 157. Transfert client

Le handover doit inclure :

- Dockerfiles ;
- profils de build ;
- Compose ;
- image digests ;
- tags ;
- SBOM ;
- provenance ;
- signatures ou procédure de vérification ;
- registry cible ;
- manifests ;
- runbooks ;
- images tierces ;
- dérogations actives ;
- procédure de rebuild.

---

## 157.1 Registry LogicGate

Les images peuvent être :

- transférées ;
- republiées ;
- reconstruites dans le registry client.

---

## 157.2 Credentials

Les credentials de registry LogicGate ne sont pas transférés s’ils donnent accès à d’autres clients.

---

# 158. Clôture de service

La clôture doit :

```text
identifier les digests actifs

transférer les artefacts

faire vérifier les signatures

transférer ou reconstruire les images

révoquer les credentials LogicGate

définir la rétention du registry LogicGate

retirer les images client selon le contrat

produire un rapport

auditer
```

---

# 159. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de publier une image ;
- de signer une release ;
- de modifier le registry ;
- de changer les permissions d’un volume réel ;
- de redéployer ;
- de retirer une image ;
- de modifier Docker Compose en production ;
- d’exécuter une migration ;
- d’activer une image de debug.

---

# 160. Réversibilité

## 160.1 Classification

```text
élevée avant déploiement

moyenne après modification des permissions de volumes

faible après suppression d’un digest sans copie
```

---

## 160.2 Rollback

Les anciens digests restent conservés pendant la fenêtre prévue.

---

## 160.3 Image de base

Un changement de base peut être annulé en redéployant l’ancien digest si la base de données reste compatible.

---

# 161. Plan d’implémentation

## Étape 1 — Profils d’images

Définir :

- image applicative ;
- image migrator ;
- image media worker éventuelle ;
- catalogues ;
- locks.

---

## Étape 2 — Dockerfile multi-stage

Créer :

- restore ;
- build ;
- test ;
- publish ;
- runtime ;
- migrator.

---

## Étape 3 — Runtime non-root

Ajouter :

- UID/GID ;
- ownership ;
- port 8080 ;
- entrypoint exec ;
- modes.

---

## Étape 4 — Compose hardening

Ajouter :

- read-only ;
- cap drop ;
- no-new-privileges ;
- tmpfs ;
- pids ;
- volumes ;
- réseaux.

---

## Étape 5 — Health et shutdown

Ajouter :

- probes natives ;
- liveness ;
- readiness ;
- arrêt gracieux ;
- tests.

---

## Étape 6 — Supply chain

Ajouter :

- base digests ;
- image lock ;
- OCI labels ;
- SBOM ;
- provenance.

---

## Étape 7 — Scans

Ajouter :

- vulnérabilités ;
- secrets ;
- licences ;
- configuration.

---

## Étape 8 — Signature

Ajouter :

- signature du digest ;
- signature ou attestation des SBOM ;
- vérification avant déploiement.

---

## Étape 9 — Migrations

Créer l’image migrator et retirer toute migration automatique du runtime.

---

## Étape 10 — Opérations

Ajouter :

- rotation des images ;
- debug break-glass ;
- rollback ;
- handover ;
- runbooks.

---

# 162. Tâches liées proposées

```text
SF-TASK-353 — Define versioned container-image profiles

SF-TASK-354 — Add the pinned base-image lock manifest

SF-TASK-355 — Create the multi-stage application Dockerfile

SF-TASK-356 — Create the dedicated migration image

SF-TASK-357 — Run application images as a fixed non-root user

SF-TASK-358 — Add read-only root-filesystem support

SF-TASK-359 — Add explicit writable volumes and tmpfs mounts

SF-TASK-360 — Drop all Linux container capabilities

SF-TASK-361 — Add no-new-privileges and seccomp validation

SF-TASK-362 — Add bounded container resource limits

SF-TASK-363 — Add native web and worker health probes

SF-TASK-364 — Add graceful container shutdown tests

SF-TASK-365 — Add OCI image metadata and release manifests

SF-TASK-366 — Generate container SBOMs

SF-TASK-367 — Add container vulnerability and secret scanning

SF-TASK-368 — Add signed container images and attestations

SF-TASK-369 — Verify signatures before deployment

SF-TASK-370 — Add immutable image promotion by digest

SF-TASK-371 — Add hardened Caddy and PostgreSQL image validation

SF-TASK-372 — Add container runtime security integration tests

SF-TASK-373 — Add container rollback and debugging runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 163. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Le build utilise plusieurs stages.

Le restore utilise packages.lock.json en mode verrouillé.

Les images de base sont épinglées par digest.

Le contexte Docker exclut Git, secrets et App_Data.

Aucun secret n’apparaît dans une layer ou l’historique.

L’image runtime ne contient pas le SDK.

L’image runtime ne contient pas les tests.

L’image runtime ne contient pas les sources.

Le processus s’exécute avec un UID non-root fixe.

Le service écoute sur un port non privilégié.

Le root filesystem est read-only.

Les écritures utilisent uniquement des volumes ou tmpfs déclarés.

Toutes les capabilities sont supprimées.

no-new-privileges est actif.

Le mode privileged est absent.

Le Docker socket n’est jamais monté.

Le host network et le host PID sont absents.

Les ressources et PIDs sont bornés.

Les core dumps sont désactivés.

Les health checks ne dépendent pas de curl ou wget.

Le web et le worker s’arrêtent proprement.

Le migrator ne démarre jamais automatiquement.

Chaque image possède une SBOM.

Chaque image est scannée.

Chaque release possède une provenance.

Chaque digest stable est signé.

Le déploiement vérifie le digest et la signature.

Staging et production utilisent exactement le même digest.

Aucun déploiement n’utilise latest.

Les anciens digests de rollback sont conservés.

Les tests runtime sont validés sur Linux.
```

---

# 164. Conditions de révision

Réexaminer cette décision si :

- Kubernetes devient la baseline ;
- un runtime rootless est imposé ;
- une plateforme de signature différente est adoptée ;
- une architecture ARM64 devient obligatoire ;
- Native AOT devient la publication standard ;
- plusieurs images worker spécialisées sont nécessaires ;
- un traitement GPU est introduit ;
- une conformité impose AppArmor ou SELinux personnalisé ;
- un proxy de registry est ajouté ;
- des images confidentielles deviennent nécessaires ;
- les builds sont déplacés vers une infrastructure client ;
- un scanner ou registry impose un nouveau format d’attestation.

---

# 165. Indicateurs à suivre

- taille des images ;
- durée de build ;
- durée de pull ;
- âge des bases ;
- images non signées ;
- SBOM absentes ;
- vulnérabilités ;
- dérogations ;
- images exécutées root ;
- capabilities ;
- redémarrages ;
- OOM ;
- health failures ;
- images retirées encore actives ;
- digests déployés ;
- divergence staging-production ;
- volumes avec mauvaises permissions ;
- temps de rollback.

---

# 166. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
les images grossissent fortement

un shell réapparaît dans le profil minimal

des secrets sont détectés dans les layers

des conteneurs sont exécutés root

read-only est régulièrement désactivé

des chmod 777 sont ajoutés

des capabilities s’accumulent

des tags flottants sont déployés

les signatures ne sont plus vérifiées

les dérogations expirent sans correction

les conteneurs sont patchés manuellement

staging et production utilisent des digests différents

l’image migrator devient un service permanent
```

---

# 167. Conséquences observées

Cette section est append-only après mise en œuvre.

## 167.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
des images multi-stage,
du runtime non-root,
des SBOM
et de la signature des images.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- image ;
- base ;
- architecture ;
- digest ;
- taille ;
- finding ;
- incident ou bénéfice ;
- action proposée.

---

# 168. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [0027-database-roles-and-runtime-least-privilege.md](0027-database-roles-and-runtime-least-privilege.md)
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
- [0008-private-package-feed-and-supply-chain.md](0008-private-package-feed-and-supply-chain.md)
- [0010-structured-observability-with-opentelemetry.md](0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](0012-runtime-configuration-and-secrets.md)
- [0013-local-git-initialization-and-explicit-remote-publishing.md](0013-local-git-initialization-and-explicit-remote-publishing.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](0014-explicit-client-upgrades-and-version-pinning.md)
- [0016-content-and-media-storage-boundaries.md](0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](0017-background-jobs-and-reliable-side-effects.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](0019-audit-trail-and-sensitive-administrative-actions.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- [0022-browser-security-headers-and-content-security-policy.md](0022-browser-security-headers-and-content-security-policy.md)
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0024-client-data-export-and-portability.md](0024-client-data-export-and-portability.md)
- [0025-third-party-integrations-and-egress-control.md](0025-third-party-integrations-and-egress-control.md)

---

# 169. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 164 |

---

# 170. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 171. Synthèse de la décision

```text
Build
→ contexte filtré
→ images de base épinglées
→ restore verrouillé
→ build déterministe
→ tests
→ publish Release
→ aucun secret dans les layers

Image applicative
→ web
→ worker
→ healthcheck
→ aucun SDK
→ aucun package manager
→ aucun outil d’administration

Image migrator
→ artefact distinct
→ aucun port
→ aucun démarrage automatique
→ credential de migration séparé

Runtime
→ UID/GID non-root
→ port 8080
→ root filesystem read-only
→ volumes explicites
→ tmpfs borné
→ umask restrictive

Isolation
→ cap_drop ALL
→ no-new-privileges
→ aucun privileged
→ aucun Docker socket
→ aucun host network
→ seccomp actif
→ ressources et PIDs bornés

Données
→ App_Data persistant
→ Data Protection volume dédié
→ aucun état métier dans la couche du conteneur
→ secrets montés au runtime

Supply chain
→ SBOM
→ scan de vulnérabilités
→ scan de secrets
→ licences
→ provenance
→ signature

Release
→ tags humains facultatifs
→ digest immuable obligatoire
→ même digest staging et production
→ aucune utilisation de latest
→ rollback vers un digest vérifié

Opérations
→ health checks natifs
→ arrêt gracieux
→ logs stdout/stderr
→ aucune modification en place
→ image debug séparée et temporaire
→ handover avec manifests et attestations
```

> Une image de conteneur de production ne doit pas être une petite machine virtuelle remplie d’outils. Elle doit être un artefact immuable et vérifiable contenant uniquement ce qui est nécessaire à un processus non privilégié, avec des chemins d’écriture, des ressources et des communications explicitement limités.
