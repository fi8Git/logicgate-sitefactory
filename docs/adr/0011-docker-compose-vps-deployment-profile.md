---
adrId: ADR-0011
title: Use Docker Compose on a Linux VPS as the initial deployment profile
status: accepted
date: 2026-07-28
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
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0010-structured-observability-with-opentelemetry.md
targetVersion: 0.1.0
---

# ADR-0011 — Profil de déploiement initial sur VPS Linux avec Docker Compose

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le profil structurel `docker-compose-vps` est reconnu par la configuration V1 ;
il ne déclenche aucun déploiement. Le public ne génère pas encore un
Docker Compose client complet, des images applicatives, Caddy ou un VPS.
Le [contrat opérationnel](../05-operations/22-deployment-and-environments.md)
et la décision ci-dessous restent des exigences de production futures.
Le PostgreSQL éphémère des tests publics n'est pas ce profil client.
Les noms `sample-project`, domaines et registres d'exemple sont fictifs ;
aucun port PostgreSQL public, secret embarqué ou effet de production implicite
n'est autorisé.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory adopte comme premier profil officiel de déploiement :

```text
docker-compose-vps
```

Ce profil repose sur :

```text
VPS Linux
+ Docker Engine
+ Docker Compose
+ images OCI immuables
+ PostgreSQL
+ Caddy
+ réseaux privés
+ volumes persistants
+ sauvegardes externes au VPS
```

Ce profil constitue la baseline des projets standards SiteFactory.

Il ne constitue pas l’unique architecture possible pour tous les projets futurs.

Toute dérogation significative, telle que :

- Kubernetes ;
- PaaS managé ;
- hébergement Windows ;
- base managée externe ;
- architecture distribuée ;
- plusieurs régions ;
- haute disponibilité active-active ;

nécessite une décision séparée.

---

## 2. Résumé

Chaque client et environnement est déployé comme un projet Docker Compose distinct.

Exemple :

```text
Client Alpha / Staging
→ Compose project sf-alpha-staging

Client Alpha / Production
→ Compose project sf-alpha-production

Client Beta / Production
→ Compose project sf-beta-production
```

Une stack cliente standard contient au minimum :

```text
application web
PostgreSQL
réseau applicatif privé
volumes persistants
health checks
configuration runtime
secrets montés
```

Le VPS possède également une passerelle HTTPS Caddy.

Architecture conceptuelle :

```text
Internet
→ ports 80 et 443
→ Caddy
→ réseau proxy partagé
→ application cliente
→ réseau backend privé
→ PostgreSQL
```

PostgreSQL ne publie aucun port sur l’hôte.

L’application ne publie pas directement de port Internet.

Les images applicatives sont :

- construites en CI ;
- testées ;
- scannées ;
- publiées dans un registry approuvé ;
- déployées avec une version et un digest identifiables.

La production ne compile pas le code source applicatif lors du déploiement normal.

---

## 3. Contexte

SiteFactory cible initialement des projets web pour PME nécessitant :

- un coût d’infrastructure raisonnable ;
- une architecture compréhensible ;
- une maintenance possible par une petite équipe ;
- PostgreSQL ;
- HTTPS ;
- sauvegardes ;
- observabilité ;
- isolation par client ;
- transfert du projet ;
- déploiements reproductibles ;
- possibilité d’hébergement mutualisé ou dédié.

LogicGate dispose déjà d’une expérience de déploiement avec :

- VPS Linux ;
- Docker Compose ;
- Caddy ;
- PostgreSQL ;
- volumes Docker ;
- DNS ;
- HTTPS automatique.

Les projets standards n’exigent pas initialement :

- un orchestrateur de cluster ;
- une mise à l’échelle automatique complexe ;
- plusieurs régions ;
- des centaines d’instances ;
- un maillage de services ;
- des déploiements canary distribués.

Un PaaS pourrait simplifier une partie des opérations, mais imposerait :

- des coûts et limites variables ;
- une dépendance fournisseur ;
- une stratégie différente selon l’hébergeur ;
- une réversibilité parfois plus faible.

Kubernetes fournirait davantage de capacités, mais ajouterait une complexité disproportionnée pour le MVP.

---

## 4. Problème à résoudre

Quel profil de déploiement utiliser pour les premiers projets SiteFactory afin de fournir :

- HTTPS ;
- isolation ;
- PostgreSQL ;
- volumes persistants ;
- déploiements versionnés ;
- sauvegardes ;
- health checks ;
- observabilité ;
- maintenance ;
- transfert client ;

sans introduire une plateforme d’orchestration trop complexe ou un verrouillage fort vers un fournisseur ?

La solution doit également empêcher :

- l’exposition publique de PostgreSQL ;
- le déploiement d’images non testées ;
- l’usage du tag `latest` ;
- les collisions entre clients ;
- le partage involontaire de volumes ;
- les secrets dans Git ;
- les migrations automatiques ;
- les builds non maîtrisés sur le serveur ;
- l’accès d’un conteneur au socket Docker ;
- les conteneurs privilégiés ;
- les sauvegardes présentes uniquement sur le VPS.

---

## 5. Portée

Cette décision couvre :

- le profil `docker-compose-vps` ;
- le système Linux cible ;
- Docker Engine ;
- Docker Compose ;
- registry d’images ;
- images applicatives ;
- PostgreSQL ;
- Caddy ;
- réseaux ;
- ports ;
- volumes ;
- secrets ;
- health checks ;
- configuration runtime ;
- déploiement ;
- rollback applicatif ;
- migrations ;
- sauvegardes ;
- observabilité ;
- durcissement des conteneurs ;
- structure des répertoires ;
- isolation des environnements ;
- transfert client.

---

## 6. Hors portée

Cette décision ne définit pas :

- le fournisseur VPS définitif ;
- le dimensionnement exact de chaque serveur ;
- le prix d’hébergement ;
- une architecture haute disponibilité complète ;
- un cluster Docker Swarm ;
- un cluster Kubernetes ;
- une base PostgreSQL managée obligatoire ;
- une politique DNS propre à un fournisseur ;
- le backend d’observabilité ;
- le stockage hors site exact ;
- les SLA contractuels ;
- le nombre maximal universel de clients par VPS ;
- une architecture multi-tenant.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Simplicité d’exploitation**
2. **Isolation par client et environnement**
3. **Sécurité réseau**
4. **Déploiement reproductible**
5. **Images immuables**
6. **Portabilité**
7. **Coût raisonnable**
8. **Compatibilité PostgreSQL**
9. **HTTPS automatisé**
10. **Sauvegarde et restauration**
11. **Transfert client**
12. **Observabilité**
13. **Automatisation progressive**
14. **Faible dépendance fournisseur**
15. **Compatibilité avec une petite équipe**
16. **Possibilité future de migration vers une autre plateforme**

---

## 8. Contraintes

La décision doit respecter :

```text
Linux en production

Docker Engine

Docker Compose

PostgreSQL

Caddy

Un projet Compose par client et environnement

Aucun port PostgreSQL public

Aucun secret dans Git

Aucune migration automatique au démarrage

Images versionnées

Build et tests avant publication

Volumes persistants isolés

Sauvegarde hors VPS

Compatibilité avec un VPS mutualisé ou dédié
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- la majorité des projets initiaux utilise une seule instance web ;
- un court redémarrage contrôlé reste acceptable pour certaines releases ;
- la montée en charge verticale suffit initialement ;
- plusieurs stacks peuvent partager un VPS lorsque les ressources le permettent ;
- un client critique peut utiliser un VPS dédié ;
- les images peuvent être construites dans GitHub Actions ;
- un registry OCI privé ou approuvé est disponible ;
- Caddy peut gérer les certificats TLS ;
- le nombre de stacks reste inventoriable ;
- les données persistantes tiennent dans une stratégie PostgreSQL et `App_Data`.

---

# 10. Option A — Déploiement direct avec systemd

## 10.1 Description

L’application .NET est publiée puis copiée directement sur le VPS.

Elle est exécutée comme service systemd.

PostgreSQL et Caddy sont installés sur l’hôte.

---

## 10.2 Avantages

- peu de couches ;
- faible overhead ;
- intégration native Linux ;
- diagnostic direct ;
- pas de runtime de conteneur nécessaire.

---

## 10.3 Inconvénients

- dépendances installées sur l’hôte ;
- isolation plus faible ;
- déploiements plus spécifiques ;
- coexistence de plusieurs clients plus complexe ;
- rollback plus artisanal ;
- risque de dérive de serveur ;
- transfert et reproduction plus difficiles.

---

## 10.4 Risques

- différences entre VPS ;
- conflit de ports ;
- bibliothèques système différentes ;
- suppression accidentelle de fichiers ;
- configuration manuelle non versionnée ;
- migration vers un autre serveur plus difficile.

---

## 10.5 Réversibilité

```text
moyenne
```

---

# 11. Option B — PaaS managé

## 11.1 Description

L’application est déployée sur un PaaS proposant :

- runtime ;
- TLS ;
- base managée ;
- logs ;
- scaling ;
- déploiements.

---

## 11.2 Avantages

- exploitation simplifiée ;
- mises à jour d’infrastructure gérées ;
- observabilité intégrée ;
- déploiement rapide ;
- haute disponibilité parfois disponible ;
- base managée possible.

---

## 11.3 Inconvénients

- coût variable ;
- limites du fournisseur ;
- dépendance propriétaire ;
- différences entre prestataires ;
- transfert client plus complexe ;
- configuration de modules ou volumes parfois limitée ;
- contrôle réseau réduit.

---

## 11.4 Risques

- hausse de prix ;
- service supprimé ;
- lock-in ;
- limites de stockage ;
- comportement différent entre hébergeurs ;
- dépendance à une région ou un compte fournisseur.

---

## 11.5 Réversibilité

```text
moyenne
```

---

# 12. Option C — Kubernetes

## 12.1 Description

Les applications clientes sont déployées sur un cluster Kubernetes.

---

## 12.2 Avantages

- orchestration avancée ;
- auto-healing ;
- scaling ;
- rolling deployments ;
- secrets ;
- réseau ;
- ressources ;
- écosystème mature.

---

## 12.3 Inconvénients

- forte complexité ;
- coût d’exploitation ;
- cluster à maintenir ;
- nombreuses ressources ;
- diagnostics distribués ;
- expertise nécessaire ;
- disproportionné pour les premiers projets.

---

## 12.4 Risques

- mauvaise configuration du cluster ;
- surface d’attaque accrue ;
- certificats et opérateurs complexes ;
- coût supérieur à la valeur ;
- dépendance à une expertise rare.

---

## 12.5 Réversibilité

```text
moyenne à faible
```

---

# 13. Option D — VPS Linux avec Docker Compose

## 13.1 Description

Chaque environnement client est déployé comme un projet Docker Compose.

Le VPS héberge :

- Docker ;
- les stacks clientes ;
- Caddy ;
- les volumes ;
- les agents opérationnels nécessaires.

---

## 13.2 Avantages

- architecture lisible ;
- isolation par conteneurs et réseaux ;
- images portables ;
- faible coût initial ;
- déploiement reproductible ;
- bon support PostgreSQL ;
- transfert vers un autre VPS ;
- compatibilité avec Caddy ;
- faible nombre de composants ;
- adapté à une petite équipe.

---

## 13.3 Inconvénients

- pas d’orchestration multi-nœuds native ;
- mises à jour hôte à gérer ;
- déploiements et rollbacks à concevoir ;
- monitoring à installer ;
- panne du VPS pouvant affecter plusieurs stacks ;
- scaling manuel ;
- secrets à gérer sur l’hôte.

---

## 13.4 Risques

- saturation CPU, mémoire ou disque ;
- mauvaise cible opérateur ;
- collision de ressources ;
- mauvaise exposition de port ;
- panne unique ;
- dérive du VPS ;
- sauvegarde insuffisante ;
- image non mise à jour.

---

## 13.5 Réversibilité

```text
élevée
```

Les stacks peuvent être déplacées vers :

- un autre VPS ;
- une VM ;
- un service de conteneurs ;
- un orchestrateur futur.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | systemd | PaaS | Kubernetes | Docker Compose |
|---|---:|---:|---:|---:|---:|
| Simplicité initiale | 5 | 4 | 5 | 1 | 5 |
| Isolation multi-client | 5 | 2 | 4 | 5 | 4 |
| Portabilité | 5 | 3 | 2 | 4 | 5 |
| Coût initial | 4 | 5 | 3 | 1 | 5 |
| Reproductibilité | 5 | 2 | 4 | 5 | 5 |
| PostgreSQL | 5 | 5 | 4 | 4 | 5 |
| Exploitation petite équipe | 5 | 3 | 4 | 1 | 5 |
| Contrôle réseau | 4 | 4 | 2 | 5 | 5 |
| Transfert client | 4 | 3 | 2 | 3 | 5 |
| Réversibilité | 4 | 3 | 3 | 3 | 5 |
| Capacité future | 3 | 3 | 4 | 5 | 4 |

Docker Compose sur VPS est retenu comme profil initial.

---

# 15. Décision

Nous décidons de créer le profil officiel :

```text
docker-compose-vps
```

Ce profil produit les artefacts nécessaires au déploiement d’une application cliente sur un VPS Linux avec Docker Compose.

---

## 15.1 Système cible

La cible initiale est :

```text
distribution Linux LTS supportée
```

La baseline opérationnelle peut utiliser :

```text
Ubuntu Server 24.04 LTS
```

ou une distribution compatible validée.

---

## 15.2 Docker

Le VPS utilise :

- Docker Engine ;
- le plugin Docker Compose ;
- une configuration versionnée et supportée ;
- des mises à jour planifiées.

---

## 15.3 Pas de Swarm

Le profil n’active pas Docker Swarm.

---

## 15.4 Pas de Kubernetes

Le profil ne génère pas de manifests Kubernetes.

---

# 16. Architecture de référence

```text
VPS Linux
│
├── Firewall hôte
│   ├── 22/tcp — SSH contrôlé
│   ├── 80/tcp — HTTP vers Caddy
│   └── 443/tcp — HTTPS vers Caddy
│
├── Stack passerelle
│   └── Caddy
│       ├── réseau proxy
│       ├── volume caddy_data
│       └── volume caddy_config
│
├── Client Alpha / Production
│   ├── application
│   ├── PostgreSQL
│   ├── réseau proxy
│   ├── réseau backend privé
│   ├── app_data
│   ├── data_protection_keys
│   └── postgres_data
│
└── Client Beta / Production
    ├── application
    ├── PostgreSQL
    ├── réseau proxy
    ├── réseau backend privé
    └── volumes propres
```

---

# 17. Isolation des projets Compose

Chaque client et environnement possède un nom Compose unique.

Convention conceptuelle :

```text
sf-{client-slug}-{environment}
```

Exemples :

```text
sf-sample-project-staging

sf-sample-project-production
```

---

## 17.1 Nom explicite

Le nom du projet est fourni explicitement pendant l’opération.

Il ne dépend pas uniquement du nom du dossier courant.

---

## 17.2 Collision

Une collision avec un projet existant bloque le déploiement.

---

## 17.3 `container_name`

L’utilisation de :

```yaml
container_name:
```

est interdite par défaut.

Docker Compose doit gérer les noms afin de conserver :

- isolation ;
- absence de collision ;
- possibilité d’exécuter plusieurs projets ;
- cohérence avec le project name.

---

# 18. Services de la stack cliente

Une stack standard comprend :

```text
web
postgres
```

Des services one-shot ou optionnels peuvent inclure :

```text
migration
backup
restore
maintenance
```

---

## 18.1 Service web

Le service web contient l’application .NET publiée.

---

## 18.2 Service PostgreSQL

Le service PostgreSQL possède son propre volume persistant.

---

## 18.3 Service migration

Le service migration est explicitement déclenché conformément à l’ADR-0004.

---

## 18.4 Services one-shot

Les services d’opération ne possèdent pas de politique de redémarrage automatique.

---

# 19. PostgreSQL par stack

Le profil initial génère un service PostgreSQL dédié par client et environnement.

---

## 19.1 Avantages

- isolation claire ;
- sauvegarde directe ;
- transfert simple ;
- version identifiable ;
- credentials propres ;
- limitation du blast radius logique.

---

## 19.2 Coût

Cette approche consomme davantage de mémoire qu’une instance PostgreSQL partagée.

Le coût est accepté pour la baseline.

---

## 19.3 Instance partagée future

Une instance mutualisée avec plusieurs bases nécessitera :

- profil distinct ;
- permissions ;
- procédures ;
- analyse de capacité ;
- validation explicite.

Elle ne sera pas produite implicitement par `docker-compose-vps`.

---

# 20. Caddy

Caddy constitue le reverse proxy et le gestionnaire TLS du profil initial.

---

## 20.1 Responsabilités

Caddy assure :

- écoute HTTP et HTTPS ;
- redirection vers HTTPS ;
- certificats ;
- renouvellement ;
- routage par domaine ;
- headers proxy ;
- compression lorsque configurée ;
- logs d’accès maîtrisés.

---

## 20.2 Non-responsabilités

Caddy ne remplace pas :

- l’autorisation ;
- l’antiforgery ;
- la validation applicative ;
- le rate limiting applicatif ;
- l’observabilité métier ;
- les backups.

---

# 21. Passerelle mutualisée

La baseline utilise une passerelle Caddy par VPS.

Elle peut desservir plusieurs stacks clientes.

---

## 21.1 VPS dédié

Un VPS dédié à un client utilise la même architecture avec une seule stack cliente.

---

## 21.2 Configuration par site

Chaque client fournit un snippet de configuration séparé.

Structure conceptuelle :

```text
gateway/
├── Caddyfile
└── sites/
    ├── sample-project-production.caddy
    └── sample-secondary-production.caddy
```

---

## 21.3 Validation

Toute modification doit suivre :

```text
génération du candidat
→ caddy validate
→ inspection
→ reload
→ health check
```

---

## 21.4 Reload

Le rechargement de Caddy ne doit pas nécessiter l’arrêt de toutes les applications clientes.

---

# 22. Certificats TLS

Caddy gère automatiquement les certificats lorsque :

- le DNS pointe vers le VPS ;
- les ports 80 et 443 sont accessibles ;
- le domaine est valide ;
- les volumes Caddy sont persistants.

---

## 22.1 Certificats dans Git

Les certificats et clés privées ne sont jamais versionnés.

---

## 22.2 Volumes

Les volumes suivants sont persistants :

```text
caddy_data
caddy_config
```

---

## 22.3 Sauvegarde

La configuration Caddy est sauvegardée.

Les certificats peuvent généralement être réémis, mais les volumes peuvent être conservés afin de réduire :

- les interruptions ;
- les risques de rate limiting ;
- les opérations de récupération.

---

# 23. Réseau proxy

Le VPS possède un réseau Docker partagé :

```text
sitefactory_proxy
```

ou un identifiant équivalent validé.

---

## 23.1 Réseau externe

Les stacks clientes rejoignent ce réseau comme réseau externe.

---

## 23.2 Services exposés

Seul le service web rejoint le réseau proxy.

PostgreSQL ne rejoint pas ce réseau.

---

## 23.3 Nom DNS interne

Caddy route vers un alias réseau propre au client et à l’environnement.

---

## 23.4 Collision

Les aliases doivent être uniques.

---

# 24. Réseau backend

Chaque stack possède un réseau privé propre.

Exemple :

```text
sf-sample-project-production_backend
```

---

## 24.1 Services

Le réseau backend relie :

- application ;
- PostgreSQL ;
- services internes éventuels.

---

## 24.2 Internet

Le réseau backend n’est pas exposé directement sur l’hôte.

---

## 24.3 `internal`

Lorsque compatible avec les besoins de la stack, un réseau strictement interne peut utiliser :

```yaml
internal: true
```

Toutefois, l’application ayant souvent besoin d’appels sortants :

- SMTP ;
- API ;
- OTLP ;

la topologie doit distinguer les besoins réels.

---

# 25. Ports publiés

La baseline autorise sur l’hôte :

```text
80/tcp
443/tcp
```

pour Caddy.

Le port SSH est géré au niveau de l’hôte.

---

## 25.1 Application

Le service web utilise :

```yaml
expose:
```

ou son port interne sans publication hôte.

Il n’utilise pas :

```yaml
ports:
  - "8080:8080"
```

en production standard.

---

## 25.2 PostgreSQL

Le service PostgreSQL ne publie jamais :

```text
5432
```

sur l’interface publique.

---

## 25.3 Diagnostic local

Un accès ponctuel utilise :

- tunnel SSH ;
- `docker compose exec` ;
- conteneur outil temporaire ;
- réseau privé.

---

# 26. Firewall hôte

Le firewall autorise uniquement les ports nécessaires.

Baseline :

```text
SSH contrôlé
HTTP
HTTPS
```

---

## 26.1 PostgreSQL

Aucune règle publique pour PostgreSQL.

---

## 26.2 Docker et firewall

Docker peut créer ses propres règles réseau.

La protection principale consiste donc à ne publier aucun port inutile dans Compose.

Le firewall hôte ne suffit pas à compenser une mauvaise publication de port.

---

## 26.3 Nouveaux ports

Tout nouveau port publié nécessite :

- justification ;
- analyse ;
- documentation ;
- test ;
- validation humaine.

---

# 27. Structure des répertoires du VPS

Structure conceptuelle :

```text
/srv/sitefactory/
├── gateway/
│   ├── compose.yaml
│   ├── config/
│   └── sites/
│
├── clients/
│   └── sample-project/
│       ├── staging/
│       │   ├── compose.yaml
│       │   ├── config/
│       │   ├── secrets/
│       │   ├── reports/
│       │   └── releases/
│       │
│       └── production/
│           ├── compose.yaml
│           ├── config/
│           ├── secrets/
│           ├── reports/
│           └── releases/
│
└── operations/
```

---

## 27.1 Données Docker

Les données persistantes principales restent dans des volumes Docker gérés.

---

## 27.2 Secrets

Les fichiers de secrets se trouvent hors du repository et possèdent des permissions strictes.

---

## 27.3 Rapports

Les rapports opérationnels ne contiennent aucun secret.

---

# 28. Artefacts générés

Le profil doit pouvoir générer :

```text
deploy/production/compose.yaml

deploy/production/.env.production.example

deploy/production/caddy-site.caddy

deploy/production/README.md

deploy/production/scripts/deploy.sh

deploy/production/scripts/migrate.sh

deploy/production/scripts/backup.sh

deploy/production/scripts/restore.sh

deploy/production/scripts/smoke-test.sh
```

La structure exacte peut évoluer.

---

## 28.1 Fichiers d’exemple

Les fichiers `.example` ne contiennent que :

- noms de variables ;
- valeurs fictives ;
- commentaires ;
- aucun secret réel.

---

## 28.2 Stratégies de fichiers

Les stratégies suivent l’ADR-0005.

---

# 29. Images applicatives

Les images sont construites hors du VPS de production dans le flux normal.

---

## 29.1 Pipeline

```text
commit
→ restore
→ build
→ tests
→ publish
→ image build
→ scan
→ registry
→ digest
```

---

## 29.2 Multi-stage build

Le `Dockerfile` utilise un build multi-stage.

L’image finale ne contient pas :

- SDK .NET ;
- code source ;
- fichiers de test ;
- cache NuGet ;
- secrets ;
- outils de compilation.

---

## 29.3 Base image

Les images de base utilisent :

- une version explicite ;
- une source approuvée ;
- une image supportée ;
- un digest lorsque la politique le permet.

---

# 30. Tags d’images

Les tags suivants peuvent être utilisés :

```text
version applicative
commit court
release candidate
```

Exemples conceptuels :

```text
1.4.0

1.4.0-rc.1

sha-a1b2c3d
```

---

## 30.1 Tag interdit

```text
latest
```

est interdit pour les déploiements de production.

---

## 30.2 Immutabilité

Un tag de version publié ne doit pas être remplacé par une autre image.

---

## 30.3 Digest

Le déploiement doit enregistrer le digest exact.

La cible recommandée est de déployer par :

```text
tag lisible
+ digest immuable
```

---

# 31. Registry

Les images sont publiées dans un registry OCI approuvé.

---

## 31.1 Authentification

Les credentials de lecture et de publication sont séparés.

---

## 31.2 VPS

Le VPS reçoit uniquement le droit de lecture nécessaire.

---

## 31.3 Pull request

Les pull requests ne disposent pas d’un token de publication de production.

---

## 31.4 Transfert

Le plan de handover doit prévoir :

- accès en lecture ;
- copie des images ;
- transfert vers un registry client ;
- reconstruction documentée.

---

# 32. Build sur le VPS

Le build applicatif sur le VPS de production est interdit dans le flux normal.

---

## 32.1 Raisons

- outil de compilation supplémentaire ;
- dépendances réseau ;
- durée ;
- reproductibilité réduite ;
- différences avec la CI ;
- surface d’attaque ;
- risque de build depuis un mauvais commit.

---

## 32.2 Exception temporaire

Une procédure de bootstrap ou secours peut utiliser un conteneur SDK temporaire uniquement si :

- le pipeline normal est indisponible ;
- l’opération est approuvée ;
- le commit est identifié ;
- les dépendances sont verrouillées ;
- l’image résultante reçoit une version distincte ;
- les contrôles nécessaires sont reproduits ;
- un rapport est produit.

Cette procédure ne devient pas la baseline.

---

# 33. Conteneur applicatif

L’application doit s’exécuter avec un utilisateur non root.

---

## 33.1 Filesystem

La cible est :

```yaml
read_only: true
```

pour le filesystem racine, avec des montages explicites pour :

- `App_Data` ;
- clés Data Protection ;
- répertoires temporaires requis.

---

## 33.2 `/tmp`

Un `tmpfs` borné peut être utilisé pour les fichiers temporaires.

---

## 33.3 Capacités

Le conteneur applicatif utilise :

```yaml
cap_drop:
  - ALL
```

sauf capacité explicitement nécessaire.

---

## 33.4 Privileged

```yaml
privileged: true
```

est interdit.

---

## 33.5 Docker socket

Le socket Docker n’est jamais monté dans l’application.

---

## 33.6 `no-new-privileges`

La baseline active :

```text
no-new-privileges
```

lorsque supporté par le profil.

---

# 34. Conteneur PostgreSQL

PostgreSQL utilise une image officielle ou approuvée.

---

## 34.1 Version

La version majeure est explicite.

---

## 34.2 `latest`

Le tag `latest` est interdit.

---

## 34.3 Volume

Le répertoire de données PostgreSQL utilise un volume dédié.

---

## 34.4 Accès

Seuls les services autorisés du réseau backend y accèdent.

---

## 34.5 Superutilisateur

L’application ne se connecte pas comme superutilisateur.

---

# 35. Volumes persistants

Une stack standard possède :

```text
postgres_data

app_data

data_protection_keys
```

---

## 35.1 Nommage

Les volumes doivent être isolés par projet Compose.

Ils ne doivent pas utiliser un nom global partagé par erreur.

---

## 35.2 External volume

Un volume externe peut être utilisé si :

- son propriétaire ;
- son environnement ;
- son cycle de vie ;
- sa sauvegarde ;

sont explicitement définis.

---

## 35.3 Suppression

Une commande de déploiement ne doit pas utiliser automatiquement :

```bash
docker compose down --volumes
```

en production.

---

## 35.4 Recréation de conteneur

La recréation d’un conteneur ne doit pas supprimer les données persistantes.

---

# 36. `App_Data`

`App_Data` contient les fichiers persistants gérés par l’application, tels que :

- médias ;
- documents ;
- exports temporaires conservés selon contrat ;
- données de fichiers applicatives.

---

## 36.1 Uploads

Les uploads sont validés avant stockage.

---

## 36.2 Exécution

Les fichiers uploadés ne sont jamais exécutés comme code.

---

## 36.3 Publication publique

Les fichiers ne sont pas servis directement depuis un montage non contrôlé sans validation et autorisation.

---

# 37. Clés Data Protection

Les clés ASP.NET Core Data Protection sont persistées dans un volume dédié.

---

## 37.1 Objectifs

Elles permettent de préserver :

- cookies ;
- tokens protégés ;
- continuité des sessions selon la configuration.

---

## 37.2 Isolation

Staging et production ne partagent pas leurs clés.

Deux clients ne partagent pas leurs clés.

---

## 37.3 Protection

Le volume est accessible uniquement au service web concerné.

---

## 37.4 Sauvegarde

Les clés font partie du périmètre de sauvegarde lorsque leur conservation est nécessaire au plan de reprise.

---

# 38. Configuration non sensible

La configuration non sensible peut être fournie par :

- variables d’environnement ;
- fichiers de configuration montés en lecture seule ;
- options générées ;
- valeurs Compose.

---

## 38.1 Repository

Les valeurs publiques ou structurelles peuvent être versionnées.

---

## 38.2 Environnement

Les valeurs différant entre staging et production restent explicitement séparées.

---

# 39. Secrets

Les secrets ne doivent pas être présents dans :

- le repository ;
- l’image ;
- le fichier Compose versionné ;
- les arguments de ligne de commande ;
- les rapports ;
- les logs ;
- `client.sitefactory.yml`.

---

## 39.1 Mécanisme cible

Le profil utilise des fichiers secrets montés en lecture seule, notamment via les capacités Compose appropriées.

Emplacement runtime conceptuel :

```text
/run/secrets/
```

---

## 39.2 Configuration de l’application

L’application doit pouvoir résoudre les secrets depuis des fichiers.

---

## 39.3 `.env`

Un fichier `.env` réel contenant des secrets ne doit pas être committé.

Lorsqu’un fichier d’environnement est utilisé sur le VPS :

- il se trouve hors Git ;
- ses permissions sont restrictives ;
- il n’est pas interprété comme script shell ;
- sa présence est inventoriée ;
- sa sauvegarde est sécurisée.

---

## 39.4 Rotation

La rotation d’un secret suit une procédure contrôlée avec redémarrage ou rechargement lorsque nécessaire.

---

# 40. Fichiers `.env` et caractères spéciaux

Les fichiers d’environnement Docker Compose ne doivent pas être chargés avec :

```bash
source .env
```

ou :

```bash
. .env
```

dans les scripts de production.

---

## 40.1 Raisons

Un shell peut interpréter :

- `!` ;
- `$` ;
- espaces ;
- substitutions ;
- caractères de contrôle.

---

## 40.2 Utilisation

Les fichiers sont consommés par Docker Compose ou un parser dédié.

---

## 40.3 Secrets complexes

Les secrets contenant des caractères complexes privilégient les fichiers secrets montés plutôt qu’une valeur inline.

---

# 41. Health checks

La stack comprend des health checks.

---

## 41.1 Application

Le service web expose :

```text
liveness
readiness
```

conformément à l’ADR-0010.

---

## 41.2 PostgreSQL

Le service PostgreSQL possède un health check léger tel que :

```text
pg_isready
```

avec une configuration ne révélant pas le mot de passe.

---

## 41.3 Caddy

La passerelle possède un mécanisme de vérification adapté.

---

## 41.4 `depends_on`

`depends_on` ne constitue pas une garantie de disponibilité métier suffisante.

L’application doit gérer un démarrage où PostgreSQL devient disponible avec un léger délai.

---

# 42. Restart policies

Les services permanents peuvent utiliser :

```text
unless-stopped
```

ou une stratégie équivalente validée.

---

## 42.1 Services one-shot

Les services :

- migration ;
- backup ;
- restore ;

n’utilisent pas de redémarrage automatique infini.

---

## 42.2 Boucle de crash

Une boucle de redémarrage doit être observable et déclencher une investigation.

---

# 43. Resource limits

Les stacks doivent pouvoir définir des limites de ressources.

Exemples :

- mémoire ;
- CPU ;
- nombre de processus ;
- taille de fichiers temporaires.

---

## 43.1 Baseline

Le profil peut fournir des valeurs par défaut prudentes ou exiger une configuration de dimensionnement.

---

## 43.2 Mesure

Les limites finales doivent tenir compte :

- du trafic ;
- de PostgreSQL ;
- des uploads ;
- des jobs ;
- du nombre de stacks ;
- des métriques réelles.

---

## 43.3 OOM

Un incident Out Of Memory doit être observable.

---

# 44. Logging des conteneurs

Les applications écrivent leurs logs sur :

```text
stdout
stderr
```

---

## 44.1 Rotation

Le driver Docker ou la couche de collecte doit appliquer une rotation bornée.

---

## 44.2 Fichiers applicatifs

L’application n’écrit pas un fichier de log local non borné dans son conteneur.

---

## 44.3 Données sensibles

Les règles de l’ADR-0010 restent applicables.

---

# 45. Observabilité

Le profil doit permettre de configurer :

- logs structurés ;
- export OTLP ;
- métriques ;
- traces ;
- health checks ;
- version applicative ;
- environnement ;
- client slug technique.

---

## 45.1 Backend indisponible

Une panne OTLP ne doit pas empêcher le démarrage fonctionnel de l’application, sauf exigence d’audit distincte.

---

## 45.2 Diagnostic local

Les logs Docker restent disponibles comme mécanisme minimal de secours.

---

# 46. Déploiement par image immuable

Le déploiement normal suit :

```text
release approuvée
→ image publiée
→ digest connu
→ pull
→ configuration vérifiée
→ migration explicite
→ compose up
→ health checks
→ smoke tests
→ rapport
```

---

## 46.1 Pas de modification dans le conteneur

Le code applicatif ne doit pas être modifié directement dans un conteneur en production.

---

## 46.2 Conteneur jetable

Un conteneur peut être supprimé et recréé sans perdre les données persistantes.

---

# 47. Manifeste de déploiement

Chaque déploiement doit pouvoir produire :

```text
deployment-manifest.json
```

---

## 47.1 Contenu conceptuel

```json
{
  "schemaVersion": 1,
  "deploymentId": "29b11dfc-5378-4a33-9a3f-e74fd8b3a265",
  "clientSlug": "sample-project",
  "environment": "production",
  "composeProjectName": "sf-sample-project-production",
  "applicationVersion": "1.4.0",
  "image": {
    "repository": "registry.example/sample-project/web",
    "tag": "1.4.0",
    "digest": "sha256:..."
  },
  "database": {
    "provider": "postgresql",
    "targetMigration": "AddLeadArchiveState"
  }
}
```

---

## 47.2 Données interdites

Le manifeste ne contient pas :

- chaîne de connexion ;
- mot de passe ;
- token registry ;
- secret SMTP ;
- contenu client ;
- clé privée.

---

# 48. Déploiement initial

Le premier déploiement suit :

```text
provision VPS
→ durcissement
→ installation Docker
→ création de la passerelle
→ création des réseaux
→ préparation des secrets
→ création de la base
→ migration explicite
→ déploiement de l’image
→ configuration Caddy
→ DNS
→ HTTPS
→ smoke tests
→ backup initial
→ rapport
```

---

# 49. Mise à jour applicative

Une mise à jour sans migration incompatible suit :

```text
pull nouvelle image
→ vérifier digest
→ compose up -d
→ readiness
→ smoke tests
→ surveillance
```

---

## 49.1 Ancienne image

L’ancienne référence reste connue pour permettre un rollback applicatif.

---

## 49.2 Nettoyage

Les anciennes images ne sont pas supprimées avant la fin de la fenêtre de validation et la confirmation du rollback possible.

---

# 50. Déploiement avec migration

Le déploiement suit l’ADR-0004.

Flux conceptuel :

```text
plan
→ backup vérifié
→ verrou
→ maintenance éventuelle
→ migration explicite
→ image compatible
→ health checks
→ smoke tests
→ surveillance
→ rapport
```

---

## 50.1 Ordre

L’ordre migration/application dépend de la compatibilité du schéma.

Il est défini dans le plan de release.

---

## 50.2 Auto-migration

L’application ne migre jamais la production au démarrage.

---

# 51. Rollback applicatif

Un rollback d’image est possible lorsque l’ancienne application reste compatible avec le schéma actuel.

---

## 51.1 Procédure

```text
sélectionner ancien digest
→ mise à jour Compose
→ recréer web
→ readiness
→ smoke tests
→ rapport
```

---

## 51.2 Migration incompatible

Si le schéma n’est plus compatible, le rollback applicatif seul est interdit.

Il faut utiliser :

- forward fix ;
- stratégie expand-and-contract ;
- restauration validée.

---

# 52. Mise à jour de Caddy

Une modification Caddy suit :

```text
générer candidat
→ valider
→ inspecter le diff
→ recharger
→ tester le domaine
→ vérifier HTTPS
```

---

## 52.1 Échec

Une configuration invalide ne remplace pas la configuration active.

---

## 52.2 Impact multi-client

La passerelle étant mutualisée, toute modification doit vérifier qu’elle ne casse pas les routes existantes.

---

# 53. DNS

Le DNS reste une opération explicite.

---

## 53.1 Préconditions HTTPS

Avant l’émission du certificat :

- domaine correct ;
- enregistrements corrects ;
- ports accessibles ;
- Caddy prêt.

---

## 53.2 Changement

Un changement DNS doit prendre en compte :

- TTL ;
- propagation ;
- ancienne cible ;
- rollback ;
- surveillance.

---

# 54. Staging et production

Staging et production utilisent :

- projets Compose distincts ;
- bases distinctes ;
- volumes distincts ;
- secrets distincts ;
- domaines distincts ;
- clés Data Protection distinctes ;
- sauvegardes distinctes.

---

## 54.1 VPS partagé

Ils peuvent partager temporairement un VPS dans un projet à faible criticité, mais cette configuration augmente le blast radius.

---

## 54.2 Préférence

Pour une production critique, la séparation physique de staging et production est préférable.

---

## 54.3 Données

Les données réelles de production ne doivent pas être copiées vers staging sans procédure de minimisation ou anonymisation.

---

# 55. VPS mutualisé entre clients

Plusieurs productions clientes peuvent partager un VPS lorsque :

- le contrat le permet ;
- le dimensionnement est suffisant ;
- les stacks sont isolées ;
- les volumes sont distincts ;
- les sauvegardes sont distinctes ;
- l’observabilité permet d’identifier chaque stack ;
- le risque de panne commune est accepté.

---

## 55.1 Blast radius

Une panne hôte peut affecter tous les clients du VPS.

---

## 55.2 Inventaire

Le VPS doit posséder un inventaire des stacks hébergées.

---

## 55.3 Client critique

Un client peut être déplacé vers un VPS dédié sans modifier son architecture applicative.

---

# 56. Dimensionnement

Le dimensionnement doit prendre en compte :

- mémoire de l’application ;
- mémoire PostgreSQL ;
- CPU ;
- taille des bases ;
- taille d’`App_Data` ;
- trafic ;
- jobs ;
- backups ;
- images ;
- logs ;
- marge de mise à jour.

---

## 56.1 Surallocation

Le nombre de stacks ne doit pas être déterminé uniquement par le fait qu’elles démarrent.

---

## 56.2 Marge

Le VPS doit conserver une marge pour :

- pics ;
- migration ;
- backup ;
- vacuum ;
- mise à jour ;
- redémarrage simultané.

---

# 57. Stockage

Le stockage doit être surveillé pour :

- volumes PostgreSQL ;
- `App_Data` ;
- images ;
- logs Docker ;
- backups temporaires ;
- fichiers Caddy.

---

## 57.1 Seuils

Des alertes doivent être déclenchées avant saturation.

---

## 57.2 Backups locaux

Les backups temporaires ne doivent pas s’accumuler indéfiniment sur le VPS.

---

## 57.3 Suppression manuelle

Il est interdit de supprimer des fichiers internes d’un volume PostgreSQL pour libérer de l’espace.

---

# 58. Sauvegardes

La stratégie complète suit :

```text
23-backup-and-restore.md
```

Le profil doit sauvegarder selon le périmètre :

```text
PostgreSQL
App_Data
Data Protection Keys
configuration opératoire
métadonnées de déploiement
```

---

## 58.1 PostgreSQL

PostgreSQL est sauvegardé avec un outil PostgreSQL cohérent.

La copie brute du volume pendant l’exécution ne constitue pas la baseline.

---

## 58.2 Hors VPS

Une copie de sauvegarde doit être transférée hors du VPS.

---

## 58.3 Chiffrement

Les sauvegardes externes utilisent une protection adaptée.

---

## 58.4 Restore drill

Des restaurations de test sont exécutées selon la politique de maintenance.

---

# 59. Sauvegarde avant déploiement

Un backup préalable est requis avant :

- migration sensible ;
- changement de stockage ;
- upgrade PostgreSQL majeur ;
- opération destructive ;
- restauration ;
- modification importante d’Identity.

---

## 59.1 Déploiement applicatif sans données

Une mise à jour purement applicative compatible peut ne pas nécessiter un nouveau backup immédiat si la politique de backup existante est satisfaite.

La décision reste dans le plan de release.

---

# 60. Restauration

Une restauration utilise :

- une stack ou base cible explicitement identifiée ;
- un backup vérifié ;
- un manifest ;
- un verrou ;
- une procédure ;
- des smoke tests.

---

## 60.1 Mauvaise cible

Une restauration vers un autre client ou environnement doit être refusée.

---

## 60.2 Production

La restauration production nécessite une validation humaine.

---

## 60.3 Base et fichiers

La cohérence entre PostgreSQL et `App_Data` doit être prise en compte.

---

# 61. Durcissement du VPS

La baseline hôte comprend :

- mises à jour de sécurité ;
- utilisateur opérateur dédié ;
- authentification SSH par clé ;
- mot de passe SSH désactivé lorsque possible ;
- login root SSH désactivé ;
- firewall ;
- horloge synchronisée ;
- espace disque surveillé ;
- accès limités ;
- audit des connexions ;
- Docker maintenu.

---

## 61.1 Services inutiles

Les services non nécessaires sont supprimés ou désactivés.

---

## 61.2 Reboot

Les mises à jour nécessitant un reboot suivent une fenêtre et un runbook.

---

# 62. Accès SSH

L’accès SSH doit être limité aux personnes et systèmes autorisés.

---

## 62.1 Clés

Une clé par opérateur est préférable à une clé partagée.

---

## 62.2 Révocation

Le départ ou changement de rôle d’un opérateur entraîne la révocation de son accès.

---

## 62.3 Agent CI

La CI ne reçoit pas automatiquement un accès SSH permanent à tous les VPS.

---

# 63. Accès Docker

L’appartenance au groupe Docker équivaut pratiquement à un accès privilégié à l’hôte.

Elle doit être strictement limitée.

---

## 63.1 Application

Aucun conteneur applicatif ne reçoit l’accès au démon Docker.

---

## 63.2 Scripts

Les scripts opératoires vérifient :

- client ;
- environnement ;
- project name ;
- dossier ;
- image ;
- action.

---

# 64. Utilisateur opérateur

Les opérations peuvent utiliser un compte système tel que :

```text
sitefactory
```

avec des permissions limitées aux répertoires et commandes nécessaires.

---

## 64.1 Root

Les opérations ne doivent pas être exécutées systématiquement comme root sans nécessité.

---

## 64.2 Sudo

Les droits sudo éventuels sont limités et documentés.

---

# 65. Secrets du registry

Les credentials du registry sont stockés hors du repository.

---

## 65.1 Portée

Le VPS utilise un token de lecture limité au périmètre nécessaire.

---

## 65.2 Rotation

Le token peut être renouvelé sans reconstruire l’image.

---

## 65.3 Déconnexion

Lors d’un transfert ou arrêt de service, le token LogicGate est révoqué.

---

# 66. Supply chain des images

Chaque image doit être liée à :

- repository ;
- commit ;
- version ;
- workflow ;
- digest ;
- scan ;
- SBOM lorsque disponible.

---

## 66.1 Image inconnue

Une image sans provenance suffisante ne doit pas être déployée.

---

## 66.2 Scan

Les vulnérabilités de l’image finale sont analysées.

---

## 66.3 Base vulnérable

Une nouvelle image doit être produite lorsque l’image de base nécessite un correctif, même si le code applicatif n’a pas changé.

---

# 67. SBOM

Les images stables doivent progressivement être accompagnées d’une SBOM.

Elle doit permettre d’identifier :

- runtime ;
- packages ;
- bibliothèques ;
- base image ;
- versions.

---

# 68. Signature et attestation

La signature d’image ou l’attestation de provenance est une cible recommandée avant une distribution plus large.

Le MVP peut démarrer avec :

- registry privé ;
- digest ;
- workflow protégé ;
- checksums ;
- provenance ;
- scans.

---

# 69. Docker Compose

Les fichiers Compose doivent être :

- validés ;
- lisibles ;
- versionnés ;
- sans secret ;
- sans valeur client réelle inutile ;
- compatibles avec le plugin Compose supporté.

---

## 69.1 Champ `version`

Le fichier ne dépend pas d’un ancien champ `version` pour définir son comportement.

---

## 69.2 Extensions YAML

Les anchors YAML peuvent être utilisés dans les fichiers Compose uniquement si :

- le format Compose les supporte ;
- le résultat reste lisible ;
- la validation produit un modèle final prévisible.

Cette règle ne modifie pas l’interdiction des anchors dans `client.sitefactory.yml`.

---

## 69.3 Validation

La CI exécute une validation telle que :

```bash
docker compose config
```

avec des valeurs fictives sûres.

---

# 70. Configuration Compose résolue

La configuration résolue peut être inspectée pendant la CI ou le déploiement.

---

## 70.1 Secrets

La sortie contenant des valeurs réelles ne doit pas être enregistrée comme artefact public.

---

## 70.2 Vérifications

Le contrôle doit notamment identifier :

- ports publiés ;
- volumes ;
- réseaux ;
- images ;
- privilèges ;
- secrets ;
- restart policies ;
- health checks.

---

# 71. Profiles Compose

Les profils peuvent séparer :

```text
runtime

migration

backup

restore

maintenance
```

---

## 71.1 Runtime

Le déploiement standard ne démarre pas automatiquement :

- restore ;
- migration ;
- maintenance.

---

## 71.2 Opération explicite

Les profils sensibles sont déclenchés par une commande dédiée et confirmée.

---

# 72. Scripts d’exploitation

Les scripts générés doivent utiliser :

```text
set -Eeuo pipefail
```

lorsqu’ils sont écrits en Bash et que cela est compatible avec leur contenu.

---

## 72.1 Paramètres

Les scripts reçoivent explicitement :

- client ;
- environnement ;
- version ;
- action.

---

## 72.2 Secrets

Un secret n’est pas passé comme argument de commande.

---

## 72.3 Sorties

Les scripts produisent des diagnostics sans afficher de secrets.

---

## 72.4 Idempotence

Les opérations répétables doivent être idempotentes ou détecter leur état.

---

# 73. Déploiement automatisé

L’automatisation peut progressivement exécuter :

- validation ;
- pull ;
- préparation ;
- health checks ;
- smoke tests ;
- rapport.

---

## 73.1 Production

Les opérations destructives ou les migrations restent soumises à approbation.

---

## 73.2 Push direct

Un push sur la branche principale ne doit pas déployer automatiquement toutes les productions clientes.

---

## 73.3 Cible

Le client et l’environnement doivent être explicites.

---

# 74. Rapport de déploiement

Une exécution produit :

```text
deployment-report.json
```

---

## 74.1 Contenu conceptuel

```json
{
  "schemaVersion": 1,
  "deploymentId": "29b11dfc-5378-4a33-9a3f-e74fd8b3a265",
  "clientSlug": "sample-project",
  "environment": "production",
  "startedAtUtc": "2026-07-28T20:00:00Z",
  "completedAtUtc": "2026-07-28T20:01:30Z",
  "applicationVersion": "1.4.0",
  "imageDigest": "sha256:...",
  "migrationReportId": "caaf4aa8-f4a5-4a22-840f-76c1b46b9ef8",
  "health": "healthy",
  "smokeTests": "success",
  "status": "success"
}
```

---

## 74.2 Secret

Le rapport ne contient aucun secret ou détail d’authentification.

---

# 75. Smoke tests

Les smoke tests de production vérifient au minimum :

- HTTPS ;
- certificat ;
- page publique ;
- health public ;
- accès à l’administration ;
- PostgreSQL via readiness ;
- fonctionnalité critique sélectionnée ;
- absence d’erreur immédiate.

---

## 75.1 Données

Les tests ne doivent pas créer de donnée réelle non nettoyée.

---

## 75.2 Formulaire

Un test de formulaire peut utiliser :

- une adresse contrôlée ;
- un mode de test ;
- une donnée fictive identifiable ;
- une procédure de nettoyage.

---

# 76. Détection de version

L’application expose dans un diagnostic protégé :

- version ;
- commit ;
- environnement ;
- composition ;
- date de build éventuelle.

---

## 76.1 Réponse publique

La version détaillée ne doit pas nécessairement être exposée publiquement.

---

# 77. Incidents

Les runbooks doivent couvrir :

```text
Application indisponible

Caddy indisponible

Certificat TLS en échec

PostgreSQL indisponible

Disque plein

Image pull impossible

Déploiement échoué

Migration échouée

Backup échoué

Restore requis

VPS perdu

Secret compromis
```

---

# 78. Panne du VPS

Le profil accepte qu’un VPS unique constitue un point de défaillance physique.

---

## 78.1 Mesures compensatoires

- backups hors VPS ;
- configuration versionnée ;
- images dans le registry ;
- manifests ;
- runbooks ;
- restauration testée ;
- DNS maîtrisé ;
- inventaire des secrets à recréer.

---

## 78.2 Reconstruction

Il doit être possible de reconstruire la stack sur un nouveau VPS à partir de :

```text
repository
images
configuration
secrets recréés ou transférés
backups
runbook
```

---

# 79. Haute disponibilité

La haute disponibilité n’est pas incluse dans le profil initial.

---

## 79.1 Besoin client

Un client nécessitant :

- RTO très faible ;
- plusieurs nœuds ;
- réplication ;
- bascule automatique ;

doit utiliser un profil ou projet sur mesure.

---

## 79.2 Évolution

Une future architecture HA fera l’objet d’un ADR distinct.

---

# 80. Mise à l’échelle

La stratégie initiale est :

```text
vertical scaling
```

puis, si nécessaire :

- optimisation ;
- VPS dédié ;
- augmentation CPU/mémoire ;
- séparation PostgreSQL ;
- plusieurs instances compatibles ;
- migration vers un autre profil.

---

## 80.1 Plusieurs instances web

Une seconde instance nécessite notamment :

- Data Protection partagée dans le périmètre du client ;
- stockage de fichiers compatible ;
- sessions compatibles ;
- migrations séparées ;
- load balancing ;
- jobs coordonnés.

Elle n’est pas activée implicitement.

---

# 81. Maintenance du VPS

La maintenance doit inclure :

- mises à jour Linux ;
- mises à jour Docker ;
- nettoyage contrôlé ;
- surveillance disque ;
- rotation logs ;
- inventaire images ;
- sauvegardes ;
- restore drills ;
- revue des accès ;
- revue des secrets.

---

## 81.1 `docker system prune`

Une commande globale de nettoyage ne doit pas être exécutée aveuglément en production.

---

## 81.2 Volumes

Les volumes ne doivent jamais être inclus dans un nettoyage automatique générique.

---

# 82. Nettoyage des images

Les anciennes images peuvent être retirées après :

- validation de la release ;
- expiration de la fenêtre de rollback ;
- confirmation de leur présence dans le registry ;
- vérification qu’aucune stack active ne les utilise.

---

# 83. Mise à niveau PostgreSQL

Une mise à niveau majeure PostgreSQL ne consiste pas à modifier uniquement le tag de l’image.

Elle nécessite :

- backup ;
- compatibilité ;
- procédure PostgreSQL ;
- staging ;
- tests ;
- migration ou restore ;
- rollback.

---

## 83.1 Volume

Un volume d’une version majeure ne doit pas être réutilisé directement par une version majeure incompatible.

---

# 84. Mise à niveau Caddy

Une nouvelle version Caddy suit :

- revue ;
- scan ;
- staging ;
- validation de configuration ;
- test TLS ;
- sauvegarde de configuration ;
- procédure de rollback.

---

# 85. Mise à niveau Docker

Une mise à jour Docker ou Compose doit être testée contre :

- fichiers Compose ;
- réseaux ;
- volumes ;
- health checks ;
- scripts ;
- passerelle Caddy.

---

# 86. Transfert client

Le handover doit inclure :

- fichiers Compose ;
- Dockerfiles ;
- images ou procédure de reconstruction ;
- registry ;
- configuration Caddy ;
- volumes à restaurer ;
- backups ;
- secrets à recréer ;
- noms Compose ;
- domaines ;
- runbooks ;
- versions Docker supportées ;
- rapports.

---

## 86.1 Dépendance LogicGate

Le projet ne doit pas dépendre irrévocablement d’un VPS LogicGate.

---

## 86.2 Nouveau VPS

La stack doit pouvoir être restaurée sur une infrastructure équivalente.

---

# 87. Clôture de service

La clôture suit :

```text
backup final
→ transfert
→ vérification client
→ retrait DNS
→ retrait Caddy
→ arrêt stack
→ révocation secrets
→ archivage
→ suppression contrôlée des volumes
```

---

## 87.1 Suppression

Les volumes ne sont supprimés qu’après :

- confirmation ;
- délai applicable ;
- backup final ;
- contrôle de la cible.

---

# 88. Sécurité

## 88.1 Actifs

- application ;
- base PostgreSQL ;
- médias ;
- clés Data Protection ;
- secrets ;
- images ;
- registry ;
- backups ;
- VPS ;
- DNS ;
- certificats.

---

## 88.2 Menaces

- port PostgreSQL public ;
- conteneur privilégié ;
- image compromise ;
- secret dans Git ;
- mauvais volume ;
- mauvaise cible ;
- path traversal ;
- accès Docker ;
- saturation disque ;
- backup absent ;
- route Caddy incorrecte ;
- tag mutable.

---

## 88.3 Contrôles

- ports minimaux ;
- réseaux privés ;
- images immuables ;
- digest ;
- non-root ;
- `cap_drop` ;
- `no-new-privileges` ;
- secrets montés ;
- backups hors VPS ;
- project names ;
- validations ;
- scans ;
- runbooks ;
- inventaire.

---

# 89. Tests de sécurité

Vérifier notamment :

```text
PostgreSQL n’est pas publié.

L’application n’est pas directement publiée.

Le conteneur web n’est pas root.

Le conteneur web n’est pas privilégié.

Le socket Docker n’est pas monté.

Les secrets ne sont pas dans l’image.

Les secrets ne sont pas dans Compose.

Les volumes sont propres au projet.

Les networks sont propres au projet.

Caddy route vers le bon client.

Les images n’utilisent pas latest.

Le digest est enregistré.

Les health checks publics ne révèlent pas de secret.
```

---

# 90. Tests Compose

La CI doit couvrir :

- parsing ;
- `docker compose config` ;
- variables obligatoires ;
- réseaux ;
- volumes ;
- ports ;
- images ;
- health checks ;
- secrets ;
- profiles ;
- absence de `container_name` ;
- absence de `privileged`.

---

# 91. Tests d’isolation multi-client

Déployer deux stacks de test.

Vérifier :

- project names distincts ;
- conteneurs distincts ;
- réseaux distincts ;
- volumes distincts ;
- bases distinctes ;
- secrets distincts ;
- domaines distincts ;
- aucun accès PostgreSQL croisé ;
- aucun partage d’`App_Data`.

---

# 92. Tests de déploiement

Tester :

```text
premier déploiement

redéploiement identique

nouvelle image compatible

image inexistante

digest incorrect

health check échoué

smoke test échoué

rollback applicatif

configuration Caddy invalide
```

---

# 93. Tests de persistance

Vérifier que :

- recréer `web` conserve `App_Data` ;
- recréer `web` conserve Data Protection ;
- recréer `postgres` conserve les données ;
- `compose down` sans volumes conserve les volumes ;
- une opération destructive demande confirmation.

---

# 94. Tests de secrets

Vérifier :

- secret monté ;
- permissions ;
- absence dans `docker inspect` lorsque le mécanisme choisi le permet ;
- absence dans les logs ;
- absence dans les rapports ;
- rotation ;
- secret absent provoquant un échec sûr.

---

# 95. Tests de panne

Simuler :

- PostgreSQL arrêté ;
- Caddy arrêté ;
- disque presque plein ;
- registry indisponible ;
- endpoint OTLP indisponible ;
- conteneur web en crash ;
- secret absent ;
- certificat non disponible.

Le système doit produire un diagnostic et un comportement contrôlés.

---

# 96. Tests de restauration

Créer une stack de reprise à partir de :

- Compose ;
- image publiée ;
- backup PostgreSQL ;
- archive `App_Data` ;
- clés nécessaires ;
- secrets recréés.

La stack doit :

- démarrer ;
- être healthy ;
- réussir les smoke tests.

---

# 97. Tests multiplateformes

Les fichiers générés et scripts sont au minimum validés depuis :

```text
Windows x64 pour la génération

Linux x64 pour l’exécution
```

---

## 97.1 Fins de ligne

Les scripts Linux utilisent des fins de ligne compatibles.

---

## 97.2 Permissions exécutables

Le repository conserve les permissions nécessaires aux scripts.

---

# 98. Quality gates

Un profil ou déploiement est bloqué lorsque :

```text
Image non versionnée

Image latest

Digest inconnu

Build ou tests échoués

Scan bloquant

Compose invalide

Port PostgreSQL publié

Port web publié directement

Secret dans Git

Secret dans l’image

Conteneur privilégié

Socket Docker monté

Volume partagé sans décision

Network collision

Project name ambigu

Health check absent

Backup requis absent

Migration automatique détectée

Rapport impossible
```

---

## 98.1 Production

Le déploiement de production est bloqué si :

```text
Client ou environnement non confirmé

Image non approuvée

Migration inattendue

Backup obligatoire non vérifié

Caddy invalide

Readiness échoue

Smoke tests échouent

Rollback non défini
```

---

# 99. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-DEPLOY-001` | Profil de déploiement inconnu |
| `SF-DEPLOY-002` | Configuration Compose invalide |
| `SF-DEPLOY-003` | Project name invalide |
| `SF-DEPLOY-004` | Collision de projet Compose |
| `SF-DEPLOY-005` | Image absente |
| `SF-DEPLOY-006` | Tag d’image interdit |
| `SF-DEPLOY-007` | Digest d’image absent |
| `SF-DEPLOY-008` | Digest inattendu |
| `SF-DEPLOY-009` | Registry inaccessible |
| `SF-DEPLOY-010` | Credential registry absent |
| `SF-DEPLOY-011` | Port PostgreSQL publié |
| `SF-DEPLOY-012` | Port applicatif publié directement |
| `SF-DEPLOY-013` | Réseau proxy absent |
| `SF-DEPLOY-014` | Collision d’alias réseau |
| `SF-DEPLOY-015` | Volume partagé non autorisé |
| `SF-DEPLOY-016` | Secret manquant |
| `SF-DEPLOY-017` | Secret détecté dans un fichier versionné |
| `SF-DEPLOY-018` | Conteneur privilégié |
| `SF-DEPLOY-019` | Socket Docker monté |
| `SF-DEPLOY-020` | Health check absent |
| `SF-DEPLOY-021` | Readiness échoué |
| `SF-DEPLOY-022` | Smoke test échoué |
| `SF-DEPLOY-023` | Configuration Caddy invalide |
| `SF-DEPLOY-024` | Domaine en collision |
| `SF-DEPLOY-025` | HTTPS non disponible |
| `SF-DEPLOY-026` | Backup requis absent |
| `SF-DEPLOY-027` | Migration requise |
| `SF-DEPLOY-028` | Rollback incompatible avec le schéma |
| `SF-DEPLOY-029` | Espace disque insuffisant |
| `SF-DEPLOY-030` | Rapport de déploiement absent |
| `SF-DEPLOY-031` | Mauvais client ou environnement |
| `SF-DEPLOY-032` | Build production sur hôte interdit |
| `SF-DEPLOY-033` | Volume Data Protection partagé |
| `SF-DEPLOY-034` | Version PostgreSQL incompatible |
| `SF-DEPLOY-035` | Configuration hôte non supportée |

---

# 100. Observabilité

Événements recommandés :

```text
DeploymentPlanCreated

DeploymentStarted

ImagePullStarted

ImagePullCompleted

ComposeValidationCompleted

MigrationRequired

ContainerRecreated

ReadinessCheckCompleted

SmokeTestCompleted

DeploymentCompleted

DeploymentFailed

DeploymentRollbackStarted

DeploymentRollbackCompleted

CaddyReloadCompleted
```

---

## 100.1 Propriétés autorisées

```text
DeploymentId

ClientSlug

Environment

ApplicationVersion

ImageDigest

ComposeProjectName

Result

Duration

ErrorCode
```

---

## 100.2 Données interdites

```text
Token registry

Mot de passe PostgreSQL

Chaîne de connexion

Secret SMTP

Clé privée

Contenu client
```

---

# 101. Conséquences positives

La décision apporte :

- architecture simple ;
- portabilité ;
- coût initial raisonnable ;
- isolation par projet Compose ;
- images immuables ;
- PostgreSQL non public ;
- HTTPS automatisé ;
- volumes persistants ;
- transfert facilité ;
- registry et provenance ;
- automatisation progressive ;
- possibilité de VPS dédié ;
- capacité de reconstruction.

---

# 102. Conséquences négatives

La décision entraîne :

- VPS à maintenir ;
- mises à jour Linux et Docker ;
- point de défaillance unique ;
- secrets à gérer sur l’hôte ;
- monitoring à configurer ;
- sauvegardes hors site ;
- dimensionnement manuel ;
- scaling limité ;
- passerelle mutualisée sensible ;
- scripts et runbooks ;
- responsabilité opérationnelle LogicGate.

---

# 103. Risques acceptés

## 103.1 Panne unique du VPS

Un incident hôte peut interrompre plusieurs stacks.

Réduction :

- backups ;
- reconstruction ;
- VPS dédié pour les clients critiques ;
- monitoring.

---

## 103.2 Scaling manuel

La montée en charge n’est pas automatique.

---

## 103.3 Passerelle mutualisée

Une erreur Caddy peut affecter plusieurs clients.

Réduction :

- snippets séparés ;
- validation ;
- reload ;
- tests ;
- rollback.

---

## 103.4 Charge opérationnelle

LogicGate doit maintenir :

- hôte ;
- Docker ;
- volumes ;
- backups ;
- registry ;
- accès.

Cette charge est acceptée pour conserver la simplicité et le contrôle.

---

# 104. Mesures compensatoires

- images immuables ;
- digest ;
- registry ;
- réseaux privés ;
- port minimal ;
- conteneurs durcis ;
- project names ;
- volumes distincts ;
- secrets montés ;
- health checks ;
- backups hors VPS ;
- restore drills ;
- observabilité ;
- runbooks ;
- rapports ;
- inventaire ;
- VPS dédié lorsque nécessaire.

---

# 105. Performance

## 105.1 Overhead

L’overhead Docker Compose est considéré comme acceptable pour les projets ciblés.

---

## 105.2 Mesure

Le dimensionnement s’appuie sur :

- CPU ;
- mémoire ;
- latence ;
- I/O ;
- stockage ;
- connexions ;
- trafic ;
- durée des jobs.

---

## 105.3 Optimisation

Avant de changer d’architecture :

1. mesurer ;
2. optimiser l’application ;
3. optimiser PostgreSQL ;
4. ajuster les ressources ;
5. isoler le client ;
6. réévaluer le profil.

---

# 106. Coûts

## 106.1 Coûts supplémentaires

- VPS ;
- stockage ;
- backups ;
- registry ;
- monitoring ;
- temps opérateur ;
- maintenance ;
- support.

---

## 106.2 Coûts évités

- cluster Kubernetes ;
- PaaS plus coûteux ;
- licence d’orchestrateur ;
- infrastructure par service ;
- dépendance forte fournisseur ;
- déploiements manuels non reproductibles.

---

# 107. Maintenabilité

Le profil doit permettre de répondre clairement :

```text
Quelle image est déployée ?

Quel digest ?

Quel client ?

Quel environnement ?

Quel projet Compose ?

Quels volumes ?

Quelle version PostgreSQL ?

Quel domaine ?

Quel backup ?

Quel rapport de déploiement ?

Comment reconstruire la stack ?
```

---

# 108. Compatibilité

## 108.1 Docker Compose

Le profil déclare une matrice de versions supportées.

---

## 108.2 Linux

La cible utilise une distribution Linux supportée et testée.

---

## 108.3 Architecture CPU

La baseline est :

```text
linux-x64
```

Un support ARM64 nécessite :

- image multi-architecture ;
- tests ;
- dépendances compatibles.

---

## 108.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline initiale.

---

# 109. Migration depuis un déploiement non conteneurisé

Le processus général est :

```text
inventorier l’application
→ inventorier PostgreSQL
→ inventorier les fichiers
→ créer l’image
→ créer Compose
→ isoler les secrets
→ créer les volumes
→ tester la restauration
→ staging
→ bascule DNS
→ validation
```

---

## 109.1 Données

La migration des données suit les runbooks PostgreSQL et `App_Data`.

---

## 109.2 Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation d’effectuer une migration réelle.

---

# 110. Migration vers un autre profil

Une future migration vers :

- PaaS ;
- Kubernetes ;
- service de conteneurs ;
- infrastructure client ;

doit préserver :

- image OCI ;
- configuration ;
- secrets ;
- PostgreSQL ;
- volumes ;
- health checks ;
- OTLP ;
- backups ;
- versions.

---

## 110.1 Avantage

L’usage d’images OCI et de standards ouverts réduit le coût de migration.

---

# 111. Réversibilité

## 111.1 Classification

```text
élevée vers un autre VPS ou service de conteneurs

moyenne vers un PaaS

moyenne vers Kubernetes
```

---

## 111.2 Nouveau VPS

La reconstruction doit pouvoir suivre :

```text
provision
→ Docker
→ gateway
→ secrets
→ image
→ restore
→ DNS
```

---

## 111.3 Retour à systemd

Possible techniquement, mais réduirait l’isolation et la reproductibilité.

Un nouvel ADR serait nécessaire.

---

# 112. Plan d’implémentation

## Étape 1 — Contrat du profil

Définir :

- `docker-compose-vps` ;
- options ;
- erreurs ;
- artefacts ;
- compatibilité.

---

## Étape 2 — Dockerfile

Créer une image :

- multi-stage ;
- non-root ;
- minimale ;
- versionnée ;
- scannée.

---

## Étape 3 — Compose

Générer :

- web ;
- PostgreSQL ;
- réseaux ;
- volumes ;
- secrets ;
- health checks ;
- profiles.

---

## Étape 4 — Caddy

Créer :

- passerelle ;
- réseau proxy ;
- snippets client ;
- validation ;
- reload.

---

## Étape 5 — Secrets

Ajouter le support des secrets montés et de la configuration par fichiers.

---

## Étape 6 — CI image

Créer :

- build ;
- tests ;
- scan ;
- registry ;
- digest ;
- SBOM progressive.

---

## Étape 7 — Déploiement

Créer :

- plan ;
- pull ;
- migration explicite ;
- Compose ;
- health checks ;
- smoke tests ;
- rapport.

---

## Étape 8 — Backup et restore

Ajouter :

- PostgreSQL ;
- `App_Data` ;
- clés ;
- manifest ;
- stockage hors site ;
- restore drill.

---

## Étape 9 — Sécurité

Ajouter :

- validation des ports ;
- non-root ;
- capabilities ;
- socket Docker ;
- secrets ;
- chemins ;
- permissions.

---

## Étape 10 — Golden deployment

Déployer deux stacks clientes de test sur un VPS ou environnement Linux isolé.

---

# 113. Tâches liées proposées

```text
SF-TASK-107 — Define the docker-compose-vps deployment profile

SF-TASK-108 — Add the production multi-stage Dockerfile

SF-TASK-109 — Generate isolated client Compose stacks

SF-TASK-110 — Add private application and PostgreSQL networks

SF-TASK-111 — Add persistent application and database volumes

SF-TASK-112 — Add mounted runtime secret support

SF-TASK-113 — Add the shared Caddy gateway profile

SF-TASK-114 — Generate and validate client Caddy routes

SF-TASK-115 — Add the immutable image build and registry workflow

SF-TASK-116 — Add deployment plan and reporting

SF-TASK-117 — Add deployment security validation

SF-TASK-118 — Add multi-client Compose isolation tests

SF-TASK-119 — Add VPS reconstruction and restore drill
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 114. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Le profil génère une stack Compose valide.

L’application utilise une image construite en CI.

L’image utilise une version explicite.

Le digest est enregistré.

Le tag latest est refusé.

L’application s’exécute sans root.

PostgreSQL n’est pas exposé publiquement.

L’application n’est pas exposée directement.

Seul Caddy publie 80 et 443.

Le réseau PostgreSQL est privé.

Les volumes sont isolés par client et environnement.

Les clés Data Protection sont isolées.

Les secrets ne sont ni dans Git ni dans l’image.

Le socket Docker n’est pas monté.

Le conteneur n’est pas privilégié.

Les health checks fonctionnent.

Une panne OTLP ne bloque pas l’application.

Deux clients peuvent coexister sans collision.

Un redéploiement conserve les données.

Un rollback applicatif compatible fonctionne.

Une migration reste explicite.

Une configuration Caddy invalide est refusée.

Un backup hors VPS est produit.

Une restauration sur un nouvel environnement réussit.

Les smoke tests HTTPS réussissent.
```

---

# 115. Conditions de révision

Réexaminer cette décision si :

- le nombre de clients rend l’exploitation VPS non soutenable ;
- plusieurs instances par client deviennent la norme ;
- des déploiements sans interruption stricte sont obligatoires ;
- la haute disponibilité devient une exigence standard ;
- la séparation physique systématique est requise ;
- un PaaS devient économiquement plus avantageux ;
- une base managée devient obligatoire ;
- Docker Compose ne répond plus aux besoins ;
- l’équipe adopte un orchestrateur ;
- plusieurs régions deviennent nécessaires ;
- les temps de reconstruction sont incompatibles avec les RTO ;
- les contraintes réglementaires imposent une autre plateforme.

---

# 116. Indicateurs à suivre

- nombre de VPS ;
- nombre de stacks par VPS ;
- CPU ;
- mémoire ;
- stockage ;
- I/O ;
- redémarrages ;
- durée de déploiement ;
- déploiements échoués ;
- rollbacks ;
- durée des backups ;
- durée des restaurations ;
- âge des images ;
- vulnérabilités d’images ;
- espace registry ;
- erreurs Caddy ;
- renouvellements TLS ;
- incidents de mauvaise cible ;
- temps de reconstruction d’un VPS.

---

# 117. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
PostgreSQL est publié sur l’hôte

des conteneurs sont privilégiés

des secrets apparaissent dans Compose

des images latest sont déployées

les stacks partagent des volumes involontairement

les VPS saturent régulièrement

les backups restent uniquement sur le VPS

les restore drills échouent

les déploiements construisent le code sur la production

les routes Caddy provoquent des impacts cross-client

les project names ne sont pas fiables

le socket Docker est monté dans une application

les productions ne peuvent pas être reconstruites
```

---

# 118. Conséquences observées

Cette section est append-only après mise en œuvre.

## 118.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète
du profil docker-compose-vps de SiteFactory.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- VPS ;
- projet technique ;
- nombre de stacks ;
- consommation ;
- incident ou bénéfice ;
- action proposée.

---

# 119. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [0027-database-roles-and-runtime-least-privilege.md](0027-database-roles-and-runtime-least-privilege.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](0021-administrator-provisioning-invitations-and-account-recovery.md)
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `15-cli-specification.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `21-observability-and-logging.md`
- `22-deployment-and-environments.md`
- `23-backup-and-restore.md`
- `24-operational-runbooks.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `27-client-handover-and-service-closure.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0003-postgresql-as-primary-database.md`
- `0004-explicit-production-migrations.md`
- `0005-file-generation-strategies.md`
- `0008-private-package-feed-and-supply-chain.md`
- `0010-structured-observability-with-opentelemetry.md`

---

# 120. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 115 |

---

# 121. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 122. Synthèse de la décision

```text
Profil initial
→ docker-compose-vps

Infrastructure
→ VPS Linux
→ Docker Engine
→ Docker Compose
→ Caddy

Client + environnement
→ projet Compose distinct
→ application
→ PostgreSQL dédié
→ réseaux propres
→ volumes propres
→ secrets propres

Internet
→ 80 et 443
→ Caddy
→ réseau proxy
→ application

PostgreSQL
→ réseau backend privé
→ aucun port public
→ compte applicatif limité
→ volume persistant

Application
→ image construite en CI
→ tag explicite
→ digest immuable
→ non-root
→ pas de socket Docker
→ pas de mode privilégié
→ health checks

Persistance
→ postgres_data
→ app_data
→ data_protection_keys

Déploiement
→ image approuvée
→ plan
→ backup si requis
→ migration explicite
→ Compose
→ readiness
→ smoke tests
→ rapport

Reprise
→ images dans le registry
→ configuration versionnée
→ secrets recréés
→ backups hors VPS
→ reconstruction sur un nouveau serveur
```

> Le profil Docker Compose sur VPS privilégie une infrastructure explicite, portable et proportionnée aux premiers projets SiteFactory. Il conserve la simplicité d’une stack unique par client tout en imposant les garanties nécessaires sur les images, les réseaux, les secrets, les volumes et la reprise.
