---
adrId: ADR-0014
title: Pin client component versions and keep upgrades explicit
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
  - ../02-architecture/module-catalog.md
  - ../03-generation/13-generation-pipeline.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/09-module-architecture.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ../03-generation/generated-runtime-database-model.md
  - ../03-generation/14-client-configuration-schema.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/18-security-baseline.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
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
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0013-local-git-initialization-and-explicit-remote-publishing.md
targetVersion: 0.1.0
---

# ADR-0014 — Mises à niveau clientes explicites et verrouillage des versions

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le public valide des versions exactes et une composition résolue déterministe.
Sa [régénération reconnue](../03-generation/regeneration-and-end-to-end-proof.md)
n'est pas un upgrade : aucune commande `upgrade check/plan/apply`, migration
de composition ou déploiement client n'est livrée. Les manifests enrichis,
rapports et commandes ci-dessous restent cibles.
Les futurs packages publics suivent [ADR-0036](0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md) ;
aucun package ou release officielle n'est encore publié.
`sample-template`, `sample-pack` et `sample-project` sont des exemples fictifs.
Versions figées, compatibilité explicite, contrôle humain et rollback sûr
restent obligatoires ; aucun client n'est mis à jour implicitement.

## 1. Statut

```text
accepted
```

Chaque projet client SiteFactory doit conserver les versions exactes des composants ayant produit sa composition.

Une nouvelle version de :

- SiteFactory CLI ;
- générateur ;
- plateforme ;
- template ;
- thème ;
- module ;
- contrat public ;
- profil de déploiement ;
- schéma ;
- package ;
- outil d’exploitation ;

ne modifie jamais automatiquement un projet client existant.

Toute mise à niveau suit un processus explicite :

```text
upgrade check
→ upgrade plan
→ décision humaine
→ branche dédiée
→ upgrade apply
→ build et tests
→ revue
→ staging
→ migration explicite éventuelle
→ release cliente
→ déploiement explicite
```

---

## 2. Résumé

SiteFactory distingue :

```text
intention de composition
→ client.sitefactory.yml

composition résolue exacte
→ sitefactory.lock.json

graphe NuGet exact
→ packages.lock.json

état des fichiers générés
→ .sitefactory/generation-state.json
```

La configuration client peut exprimer une intention telle que :

```yaml
product:
  packId: sample-pack

template:
  id: sample-template

modules:
  - id: sample-records
```

Le lock file conserve les versions réellement utilisées :

```json
{
  "generatorVersion": "0.4.0",
  "platformVersion": "0.7.0",
  "template": {
    "id": "sample-template",
    "version": "1.3.0"
  },
  "modules": [
    {
      "id": "sample-records",
      "version": "1.4.2"
    }
  ]
}
```

Une nouvelle version publiée dans le catalogue ou le feed privé ne modifie pas ce lock file.

Les commandes de référence sont :

```text
sitefactory upgrade check
sitefactory upgrade plan
sitefactory upgrade apply
```

Leurs responsabilités sont distinctes :

| Commande | Responsabilité | Effet sur le projet |
|---|---|---|
| `upgrade check` | Détecter les versions disponibles et les incompatibilités | Aucun |
| `upgrade plan` | Calculer les changements proposés | Aucun |
| `upgrade apply` | Appliquer un plan validé au repository local | Oui |
| Release cliente | Versionner les changements validés | Explicite |
| Migration production | Modifier le schéma ou les données | Explicite et séparée |
| Déploiement | Modifier un environnement | Explicite et séparé |

---

## 3. Contexte

SiteFactory produit un repository autonome par client.

Les projets clients peuvent différer par :

- leur date de création ;
- leur pack ;
- leur template ;
- leur thème ;
- leurs modules ;
- leurs options ;
- leurs customizations ;
- leurs migrations ;
- leurs versions de packages ;
- leur profil de déploiement ;
- leur calendrier de maintenance.

La plateforme SiteFactory continue à évoluer après la génération d’un projet.

Ces évolutions peuvent comprendre :

- corrections de sécurité ;
- corrections fonctionnelles ;
- améliorations de performance ;
- nouveaux modules ;
- nouvelles options ;
- nouveaux contrats ;
- changements de schéma ;
- nouvelles versions .NET ;
- nouvelles versions PostgreSQL ;
- durcissement du déploiement ;
- améliorations du générateur.

Appliquer automatiquement les dernières versions créerait plusieurs risques :

- régression cliente ;
- modification non revue ;
- incompatibilité avec les customizations ;
- migration implicite ;
- changement d’entitlement ;
- nouveau comportement en production ;
- perte d’un fichier modifié ;
- impossibilité de reproduire une ancienne release ;
- déploiements différents selon la date de restore.

À l’inverse, ne fournir aucun mécanisme de mise à niveau conduirait à :

- des projets obsolètes ;
- des vulnérabilités non corrigées ;
- des opérations entièrement manuelles ;
- une forte divergence ;
- une maintenance non soutenable.

---

## 4. Problème à résoudre

Comment permettre aux projets clients de bénéficier des évolutions SiteFactory tout en garantissant :

- une composition reproductible ;
- une décision humaine ;
- un diff inspectable ;
- la préservation des customizations ;
- la détection des incompatibilités ;
- la maîtrise des migrations ;
- la stabilité de production ;
- la possibilité de rollback ;
- la propagation des correctifs critiques ;
- la traçabilité entre versions ?

La solution doit éviter :

- les versions flottantes ;
- les mises à jour silencieuses ;
- le choix automatique de `latest` ;
- la modification du lock file sans validation ;
- l’application directe en production ;
- le push distant implicite ;
- la fusion automatique ;
- la modification d’entitlements commerciaux ;
- la suppression automatique de données ;
- l’écrasement de fichiers clients ;
- l’installation implicite de versions preview.

---

## 5. Portée

Cette décision couvre :

- versions du générateur ;
- versions de plateforme ;
- templates ;
- thèmes ;
- modules ;
- contrats publics ;
- packages ;
- profils de déploiement ;
- schémas de configuration ;
- lock files ;
- catalogues ;
- compatibilité ;
- commandes d’upgrade ;
- planification ;
- application locale ;
- gestion Git ;
- customizations ;
- fichiers générés ;
- migrations ;
- tests ;
- rapports ;
- correctifs de sécurité ;
- dépréciations ;
- rollback du repository ;
- transfert client.

---

## 6. Hors portée

Cette décision ne définit pas :

- la fréquence contractuelle de maintenance de chaque client ;
- le prix des mises à niveau ;
- le calendrier de publication des modules ;
- l’exécution automatique d’une migration ;
- le déploiement automatique en production ;
- le remplacement de PostgreSQL ;
- l’upgrade majeur de l’OS du VPS ;
- une plateforme SaaS de mise à jour centralisée ;
- un service distant obligatoire d’entitlements ;
- la maintenance d’un projet après clôture du service ;
- un SLA universel de propagation des correctifs.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Reproductibilité des projets clients**
2. **Absence de changement automatique**
3. **Protection des customizations**
4. **Correction des vulnérabilités**
5. **Compatibilité explicite**
6. **Diffs inspectables**
7. **Traçabilité**
8. **Rollback par Git**
9. **Migrations contrôlées**
10. **Déterminisme**
11. **Support de plusieurs versions clientes**
12. **Automatisation progressive**
13. **Réduction de la dérive**
14. **Transfert client**
15. **Maintenabilité par une petite équipe**
16. **Séparation entre upgrade, release et déploiement**

---

## 8. Contraintes

La décision doit respecter :

```text
Un repository par client

Versions exactes dans le lock file

Packages NuGet verrouillés

Génération déterministe

Working tree propre avant apply

Branche dédiée

Stratégies de fichiers de l’ADR-0005

Customizations préservées

Migrations explicites

Aucun push automatique

Aucun déploiement automatique

Aucun changement d’entitlement automatique

Compatibilité Windows et Linux

Rapports sans secrets
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les projets clients peuvent exécuter des versions SiteFactory différentes ;
- LogicGate maintient un inventaire des versions supportées ;
- les upgrades sont suffisamment espacés pour être revus ;
- un projet peut rester temporairement sur une version antérieure ;
- les correctifs critiques peuvent être proposés rapidement sans être appliqués silencieusement ;
- le repository client reste disponible et dans un état Git exploitable ;
- les projets générés peuvent être reconstruits à partir de leurs versions verrouillées ;
- les packages supportés restent disponibles sur le feed privé ;
- les migrations intermédiaires peuvent être nécessaires pour certains upgrades majeurs.

---

# 10. Définitions

## 10.1 Upgrade

Modification contrôlée de la composition ou des fichiers d’un projet client afin d’adopter une ou plusieurs nouvelles versions SiteFactory.

---

## 10.2 Update

Terme générique pouvant désigner une correction ou mise à jour locale.

Dans cet ADR, le terme officiel pour les composants SiteFactory est :

```text
upgrade
```

---

## 10.3 Version pinning

Enregistrement et consommation d’une version exacte plutôt que d’une version flottante.

---

## 10.4 Current composition

Ensemble des composants exacts actuellement enregistrés dans le lock file.

---

## 10.5 Target composition

Ensemble des composants exacts proposés après l’upgrade.

---

## 10.6 Upgrade check

Analyse en lecture seule des nouvelles versions et de la compatibilité.

---

## 10.7 Upgrade plan

Artefact déterministe décrivant les modifications nécessaires.

---

## 10.8 Upgrade apply

Application locale d’un plan préalablement calculé et validé.

---

## 10.9 Supported version

Version encore couverte par la politique de support SiteFactory.

---

## 10.10 Deprecated version

Version encore utilisable pendant une période limitée, mais dont la migration est recommandée.

---

## 10.11 Retired version

Version qui n’est plus supportée pour de nouvelles opérations ordinaires.

---

## 10.12 Security upgrade

Upgrade motivé principalement par une vulnérabilité ou un durcissement de sécurité.

---

# 11. Option A — Toujours utiliser la dernière version

## 11.1 Description

Chaque restore ou génération résout automatiquement la dernière version disponible de tous les composants.

---

## 11.2 Avantages

- accès rapide aux nouveautés ;
- maintenance apparente simplifiée ;
- peu de versions anciennes ;
- correctifs appliqués rapidement ;
- lock files moins importants.

---

## 11.3 Inconvénients

- builds non reproductibles ;
- changements selon la date ;
- régressions imprévisibles ;
- incompatibilité avec les customizations ;
- preview potentiellement sélectionnée ;
- impossibilité de reconstruire une ancienne release ;
- migrations non maîtrisées.

---

## 11.4 Risques

- production différente sans changement Git ;
- rupture d’un contrat ;
- module incompatible ;
- image construite avec un composant inattendu ;
- comportement client modifié silencieusement.

---

## 11.5 Réversibilité

```text
faible
```

Il peut être difficile de retrouver toutes les versions précédemment utilisées.

---

# 12. Option B — Copier et figer définitivement les composants

## 12.1 Description

Le code généré et les composants sont copiés dans le repository client.

Aucun mécanisme d’upgrade n’est prévu.

---

## 12.2 Avantages

- forte autonomie ;
- aucune dépendance à un feed pour le code copié ;
- comportement stable ;
- contrôle complet du repository client.

---

## 12.3 Inconvénients

- correctifs difficiles à propager ;
- dérive ;
- maintenance manuelle ;
- duplication ;
- support coûteux ;
- vulnérabilités persistantes ;
- comparaison complexe avec le socle.

---

## 12.4 Risques

- projets abandonnés sur d’anciennes versions ;
- correctifs incomplets ;
- incompatibilité avec les versions futures ;
- dette non visible.

---

## 12.5 Réversibilité

```text
moyenne
```

Une adoption ultérieure nécessite de comparer et réconcilier chaque projet.

---

# 13. Option C — Mise à niveau automatique périodique

## 13.1 Description

Un service central ou un workflow met automatiquement à jour les repositories clients.

Il peut :

- modifier les versions ;
- pousser une branche ;
- créer une pull request ;
- fusionner ;
- déployer.

---

## 13.2 Avantages

- propagation rapide ;
- maintenance centralisée ;
- détection continue ;
- faible intervention opérateur ;
- inventaire à jour.

---

## 13.3 Inconvénients

- effets de bord distants ;
- consommation de credentials ;
- conflits clients ;
- pull requests nombreuses ;
- risque de merge automatique ;
- customizations cassées ;
- migrations difficiles à coordonner.

---

## 13.4 Risques

- code client modifié sans validation ;
- production cassée ;
- correction automatique incorrecte ;
- branche poussée vers le mauvais repository ;
- déploiement involontaire ;
- changement commercial implicite.

---

## 13.5 Réversibilité

```text
faible à moyenne
```

Les changements distants et déploiements peuvent déjà avoir produit des effets.

---

# 14. Option D — Versions verrouillées et upgrades explicites

## 14.1 Description

Chaque projet conserve sa composition exacte.

Un outil détecte, planifie et applique localement les évolutions après décision humaine.

---

## 14.2 Avantages

- builds reproductibles ;
- contrôle humain ;
- diff Git ;
- customizations vérifiables ;
- migrations séparées ;
- correctifs traçables ;
- support de plusieurs versions ;
- rollback local ;
- absence de déploiement implicite.

---

## 14.3 Inconvénients

- plusieurs versions à maintenir ;
- upgrades à planifier ;
- inventaire nécessaire ;
- temps de revue ;
- conflits possibles ;
- propagation moins immédiate ;
- logique d’upgrade à développer.

---

## 14.4 Risques

- client restant trop longtemps sur une ancienne version ;
- correctif critique retardé ;
- plan incomplet ;
- chemins de migration trop nombreux ;
- coûts de support croissants.

---

## 14.5 Réversibilité

```text
élevée
```

L’application locale des changements reste versionnée et peut être annulée avant déploiement.

---

# 15. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Latest automatique | Code figé | Upgrade automatique | Upgrade explicite |
|---|---:|---:|---:|---:|---:|
| Reproductibilité | 5 | 1 | 5 | 3 | 5 |
| Contrôle humain | 5 | 1 | 5 | 2 | 5 |
| Propagation des correctifs | 5 | 5 | 1 | 5 | 4 |
| Protection des customizations | 5 | 1 | 4 | 2 | 5 |
| Rollback | 5 | 2 | 4 | 3 | 5 |
| Migrations maîtrisées | 5 | 1 | 3 | 2 | 5 |
| Simplicité initiale | 3 | 5 | 4 | 2 | 3 |
| Maintenabilité durable | 5 | 2 | 1 | 3 | 5 |
| Transfert client | 4 | 2 | 4 | 3 | 5 |
| Sécurité | 5 | 2 | 2 | 4 | 5 |

L’option D est retenue.

---

# 16. Décision

Nous décidons que chaque projet client utilise des versions exactes et immuables pour sa composition.

Les upgrades sont :

```text
planifiés
explicites
locaux
revus
testés
versionnés
déployés séparément
```

Une publication SiteFactory n’entraîne aucun changement sur les projets existants.

---

## 16.1 Aucune résolution flottante

Les valeurs suivantes sont interdites dans la composition résolue :

```text
latest

*

1.*

>=1.0.0 sans borne

preview la plus récente

branche main

commit non verrouillé
```

---

## 16.2 Version exacte

Le lock file utilise des versions exactes :

```text
1.4.2
```

et non une simple plage de compatibilité.

---

## 16.3 Compatibilité

Les plages de compatibilité peuvent exister dans les manifests de modules.

La résolution finale reste exacte.

---

# 17. Éléments versionnés

Le lock file doit pouvoir conserver les versions exactes de :

```text
SiteFactory CLI

Generator

Configuration schema

Catalog

Platform

Template

Theme

Modules

Public contracts

Deployment profile

Operations assets

Generation state schema
```

---

## 17.1 Packages NuGet

Les versions de packages réellement restaurées restent également conservées dans :

```text
packages.lock.json
```

---

## 17.2 Images

La release cliente conserve les tags et digests d’images dans ses manifests de déploiement.

---

## 17.3 PostgreSQL

La version PostgreSQL appartient à la configuration et à l’inventaire du déploiement, mais son upgrade suit une procédure propre.

---

# 18. `client.sitefactory.yml`

La configuration exprime l’intention produit.

Elle ne doit pas être remplie de versions techniques lorsqu’un choix par défaut contrôlé suffit.

---

## 18.1 Exemple d’intention

```yaml
product:
  packId: sample-pack

template:
  id: sample-template

modules:
  - id: sample-records
  - id: portfolio-advanced
```

---

## 18.2 Contraintes explicites

La configuration peut demander une contrainte ou un canal spécifique lorsque nécessaire.

Exemple conceptuel :

```yaml
template:
  id: sample-template
  compatibilityChannel: stable
```

---

## 18.3 Lock file

Le lock file matérialise les versions exactes choisies.

---

# 19. `sitefactory.lock.json`

Le lock file est généré et versionné dans Git.

---

## 19.1 Responsabilités

Il permet de connaître :

- la composition exacte ;
- les sources ;
- les versions ;
- les dépendances ;
- les capabilities ;
- les entitlements ;
- les hashes ;
- les contrats ;
- le profil de déploiement.

---

## 19.2 Modification manuelle

Il ne doit pas être modifié manuellement afin de simuler un upgrade.

---

## 19.3 Régénération

Il est mis à jour par une commande SiteFactory après résolution et validation.

---

## 19.4 Incohérence

Une incohérence entre :

```text
configuration
lock file
packages.lock.json
generation-state.json
```

est bloquante.

---

# 20. Exemple conceptuel de lock file

```json
{
  "schemaVersion": 1,
  "projectId": "sample-project",
  "generator": {
    "version": "0.4.0"
  },
  "platform": {
    "version": "0.7.0"
  },
  "catalog": {
    "version": "1.2.0"
  },
  "template": {
    "id": "sample-template",
    "version": "1.3.0"
  },
  "theme": {
    "id": "sample-theme",
    "version": "1.1.0"
  },
  "modules": [
    {
      "id": "sample-records",
      "version": "1.4.2",
      "publicContractsVersion": "1.2.0"
    }
  ],
  "deploymentProfile": {
    "id": "docker-compose-vps",
    "version": "1.1.0"
  }
}
```

---

# 21. Semantic Versioning

Les composants publiés suivent SemVer lorsqu’il est applicable.

---

## 21.1 Patch

Exemples :

- correction de bug compatible ;
- correction de sécurité compatible ;
- optimisation interne ;
- documentation ;
- modification sans contrat observable.

---

## 21.2 Minor

Exemples :

- nouvelle capability compatible ;
- nouveau contrat facultatif ;
- nouvel endpoint non cassant ;
- nouvelle option avec valeur par défaut sûre ;
- nouveau point d’extension.

---

## 21.3 Major

Exemples :

- suppression ;
- changement de type ;
- renommage ;
- comportement incompatible ;
- changement de format ;
- migration obligatoire incompatible ;
- retrait d’un point d’extension.

---

## 21.4 Avant `1.0`

Avant `1.0.0`, la compatibilité reste documentée explicitement.

Un changement de version mineure peut représenter une évolution importante.

Les projets utilisent donc des versions exactes.

---

# 22. Canaux de versions

Valeurs recommandées :

```text
stable

preview

experimental
```

---

## 22.1 `stable`

Canal autorisé par défaut pour les projets standards.

---

## 22.2 `preview`

Nécessite une autorisation explicite.

Il n’est jamais sélectionné automatiquement par une composition stable.

---

## 22.3 `experimental`

Réservé aux prototypes et environnements isolés.

---

## 22.4 Downgrade de canal

Une version stable ne doit pas dépendre implicitement d’un composant preview.

---

# 23. Matrice de compatibilité

SiteFactory doit maintenir une matrice entre :

- CLI ;
- générateur ;
- schéma YAML ;
- schéma du lock file ;
- plateforme ;
- modules ;
- templates ;
- profils de déploiement ;
- .NET ;
- PostgreSQL.

---

## 23.1 Exemple conceptuel

| Composant | Version | Compatible avec |
|---|---|---|
| Generator | `0.4.x` | Schema config `1` |
| Platform | `0.7.x` | .NET `10` |
| Lead module | `1.4.x` | Platform `>=0.7.0 <0.8.0` |
| Deployment profile | `1.1.x` | Compose profile schema `1` |

---

## 23.2 Source

La matrice doit être machine-readable et versionnée.

---

## 23.3 Incompatibilité

Une composition incompatible est refusée avant tout changement de fichier.

---

# 24. Graphe d’upgrade

Un upgrade n’est pas toujours possible directement entre deux versions arbitraires.

---

## 24.1 Chemin direct

Exemple :

```text
1.3.0
→ 1.4.0
```

---

## 24.2 Versions intermédiaires

Exemple :

```text
1.1.0
→ 1.3.0
→ 2.0.0
```

peut être requis pour :

- migrer la configuration ;
- migrer les données ;
- modifier les contrats ;
- remplacer une stratégie de fichier.

---

## 24.3 Refus

Si aucun chemin supporté n’existe :

```text
upgrade plan failure
```

---

## 24.4 Sauts majeurs

Un saut de plusieurs versions majeures n’est pas autorisé sans chemin de migration documenté et testé.

---

# 25. Versions supportées

Chaque composant peut définir :

```text
current

supported

deprecated

retired
```

---

## 25.1 `current`

Version recommandée pour les nouveaux projets.

---

## 25.2 `supported`

Version bénéficiant encore des correctifs prévus par la politique de maintenance.

---

## 25.3 `deprecated`

Version encore reconstructible, mais dont la mise à niveau est recommandée.

---

## 25.4 `retired`

Version ne recevant plus de maintenance ordinaire.

---

## 25.5 Reconstruction

Une version retirée peut rester disponible dans le feed afin de permettre la reconstruction d’un projet historique, selon la politique de sécurité et de rétention.

---

# 26. Inventaire des projets

LogicGate doit maintenir un inventaire permettant d’identifier :

```text
ClientSlug

Repository

ApplicationVersion

GeneratorVersion

PlatformVersion

TemplateVersion

ThemeVersion

ModuleVersions

DeploymentProfileVersion

PostgreSqlVersion

MaintenanceStatus

LastUpgradeAtUtc
```

---

## 26.1 Valeurs sensibles

L’inventaire ne contient pas :

- secret ;
- chaîne de connexion ;
- donnée métier ;
- compte utilisateur.

---

## 26.2 Usage

L’inventaire permet :

- de détecter les versions obsolètes ;
- d’identifier les projets affectés ;
- de planifier une campagne ;
- de suivre les correctifs.

---

# 27. Commande `upgrade check`

## 27.1 Objectif

Détecter les upgrades disponibles sans modifier le projet.

---

## 27.2 Entrées

```text
client.sitefactory.yml

sitefactory.lock.json

packages.lock.json

generation-state.json

catalogues disponibles

manifests

matrice de compatibilité

état Git
```

---

## 27.3 Sorties

La commande indique :

- version actuelle ;
- version disponible ;
- sévérité ;
- compatibilité ;
- breaking changes ;
- migration potentielle ;
- dépréciations ;
- sécurité ;
- actions recommandées.

---

## 27.4 Effets de bord

La commande ne doit pas :

- modifier un fichier ;
- restaurer un package dans le projet de manière persistante ;
- créer une branche ;
- créer un commit ;
- ajouter un remote ;
- pousser ;
- déployer.

---

## 27.5 Réseau

La commande peut consulter en lecture :

- feed privé ;
- catalogue ;
- métadonnées de versions ;
- avis de sécurité.

Elle ne doit effectuer aucune écriture distante.

---

# 28. Exemple de sortie `upgrade check`

```text
Current SiteFactory platform:
0.7.0

Available stable version:
0.8.1

Upgrade classification:
minor

Security relevance:
high

Direct upgrade:
supported

Affected components:
- platform 0.7.0 → 0.8.1
- sample-records 1.4.2 → 1.5.0
- docker-compose-vps 1.1.0 → 1.2.0

Migration:
required

Customizations:
compatibility review required
```

---

# 29. Résultats de `upgrade check`

Valeurs recommandées :

```text
up-to-date

upgrade-available

security-upgrade-available

deprecated

unsupported

incompatible

unknown
```

---

## 29.1 `unknown`

En cas d’information insuffisante, la commande ne doit pas conclure à tort qu’un projet est à jour.

---

# 30. Commande `upgrade plan`

## 30.1 Objectif

Produire un plan complet et déterministe sans modifier le repository.

---

## 30.2 Cible

La cible peut être :

```text
latest stable compatible

version explicite

release SiteFactory explicite

composant précis
```

---

## 30.3 Version cible explicite

Exemple conceptuel :

```bash
sitefactory upgrade plan \
  --project . \
  --target-platform 0.8.1
```

---

## 30.4 Pas de `latest` implicite

Une option de commodité peut résoudre la dernière version stable compatible, mais le plan doit inscrire la version exacte.

---

# 31. Contenu du plan

Le plan doit pouvoir décrire :

```text
Versions source

Versions cible

Graphe de dépendances

Fichiers à créer

Fichiers à mettre à jour

Fichiers à fusionner

Fichiers à conserver

Fichiers à supprimer

Conflits

Actions manuelles

Customizations affectées

Contrats dépréciés

Packages modifiés

Migrations requises

Tests requis

Impact de déploiement

Rollback repository
```

---

# 32. Artefact `upgrade-plan.json`

Une commande `upgrade plan` produit sur demande :

```text
upgrade-plan.json
```

---

## 32.1 Exemple conceptuel

```json
{
  "schemaVersion": 1,
  "upgradePlanId": "b8804703-6f5a-40de-ab61-6a55eb6cf412",
  "projectId": "sample-project",
  "createdAtUtc": "2026-07-29T08:30:00Z",
  "source": {
    "generatorVersion": "0.4.0",
    "platformVersion": "0.7.0"
  },
  "target": {
    "generatorVersion": "0.5.0",
    "platformVersion": "0.8.1"
  },
  "components": [
    {
      "type": "module",
      "id": "sample-records",
      "fromVersion": "1.4.2",
      "toVersion": "1.5.0",
      "classification": "minor"
    }
  ],
  "fileOperations": [
    {
      "path": "src/Client.Web/Generated/ModuleRegistry.g.cs",
      "strategy": "generated",
      "action": "update"
    }
  ],
  "database": {
    "migrationRequired": true,
    "productionExecution": "out-of-scope"
  },
  "status": "ready"
}
```

---

## 32.2 Secret

Le plan ne contient pas :

- token ;
- credential ;
- chaîne de connexion ;
- contenu client ;
- valeur de secret ;
- clé privée.

---

## 32.3 Immutabilité

Le plan possède une empreinte.

Toute modification de :

- configuration ;
- lock file ;
- état des fichiers ;
- versions disponibles ;
- working tree ;

peut invalider le plan.

---

# 33. Statuts d’un plan

Valeurs recommandées :

```text
ready

manual-review-required

conflict

blocked

unsupported
```

---

## 33.1 `ready`

Le plan ne contient aucun conflit bloquant.

Il reste soumis à validation humaine.

---

## 33.2 `manual-review-required`

Le plan est techniquement applicable, mais nécessite une décision sur :

- customization ;
- migration ;
- contrat ;
- suppression ;
- changement majeur.

---

## 33.3 `conflict`

Un fichier ou contrat ne peut pas être fusionné automatiquement.

---

## 33.4 `blocked`

Une précondition externe manque.

---

## 33.5 `unsupported`

Aucun chemin de mise à niveau supporté n’existe.

---

# 34. Commande `upgrade apply`

## 34.1 Objectif

Appliquer localement un plan validé au repository client.

---

## 34.2 Préconditions

```text
[ ] Plan valide
[ ] Plan non expiré ou inchangé
[ ] Working tree propre
[ ] Branche dédiée
[ ] Source commit identique
[ ] Lock files cohérents
[ ] Aucun conflit bloquant
[ ] Packages accessibles
[ ] Espace disque suffisant
[ ] Tests prévus disponibles
[ ] Confirmation humaine
```

---

## 34.3 Entrée

L’application doit utiliser un plan identifié.

Exemple conceptuel :

```bash
sitefactory upgrade apply \
  --project . \
  --plan upgrade-plan.json \
  --confirm-upgrade
```

---

## 34.4 Pas de recalcul silencieux

`upgrade apply` ne doit pas remplacer silencieusement le plan fourni par un plan différent.

Une divergence provoque :

```text
plan invalidated
```

---

# 35. Effets autorisés de `upgrade apply`

La commande peut :

- restaurer des packages nécessaires ;
- produire des candidats ;
- modifier les fichiers du repository ;
- mettre à jour `sitefactory.lock.json` ;
- mettre à jour `packages.lock.json` ;
- mettre à jour l’état de génération ;
- produire un rapport ;
- exécuter les validations locales prévues.

---

## 35.1 Effets interdits

La commande ne doit pas :

- créer une branche distante ;
- pousser ;
- créer une pull request ;
- fusionner ;
- créer un tag ;
- publier une image ;
- migrer la production ;
- déployer ;
- modifier un entitlement commercial sans configuration validée ;
- supprimer des données ;
- modifier un secret réel.

---

# 36. Branche d’upgrade

Avant `upgrade apply`, l’opérateur crée une branche dédiée.

Convention recommandée :

```text
upgrade/sitefactory-{target-version}
```

Exemple :

```text
upgrade/sitefactory-0.8.1
```

---

## 36.1 Création automatique

Une future option peut proposer de créer la branche locale après confirmation.

Elle ne doit pas :

- pousser la branche ;
- écraser une branche existante ;
- partir d’un working tree sale.

---

## 36.2 Branche existante

Une branche portant déjà ce nom est refusée ou doit être choisie explicitement.

---

# 37. Working tree

Le working tree doit être propre avant application.

---

## 37.1 Modifications non committées

Elles sont refusées afin d’éviter :

- mélange avec l’upgrade ;
- rollback ambigu ;
- conflit caché ;
- perte de travail.

---

## 37.2 Fichiers ignorés

Les secrets ignorés peuvent rester présents localement.

Ils ne doivent pas être intégrés au plan.

---

## 37.3 Stash automatique

SiteFactory ne crée pas automatiquement de stash.

---

# 38. Commit source

Le plan enregistre le commit source.

---

## 38.1 Divergence

Si `HEAD` ne correspond plus :

```text
upgrade plan invalid
```

---

## 38.2 Rebase

Après un rebase ou changement de commit source, le plan doit être recalculé.

---

# 39. Stratégies de fichiers

`upgrade apply` respecte l’ADR-0005.

---

## 39.1 `generated`

Mis à jour uniquement si le fichier n’a pas été modifié localement depuis son dernier état généré.

---

## 39.2 `create-once`

Toujours préservé.

Une nouvelle version peut être signalée comme action manuelle.

---

## 39.3 `merge`

Fusionné uniquement par un handler structuré et versionné.

---

## 39.4 `custom`

Jamais modifié.

---

## 39.5 `protected`

Jamais modifié par `upgrade apply`.

---

## 39.6 Conflit

Un conflit bloque l’application globale par défaut.

---

# 40. Customizations

Les customizations sont compilées et testées contre les nouvelles versions.

---

## 40.1 Pas de réécriture automatique

SiteFactory ne réécrit pas automatiquement le code client.

---

## 40.2 Contrat déprécié

Le plan doit identifier :

- contrat utilisé ;
- version actuelle ;
- remplacement ;
- date ou version de retrait.

---

## 40.3 Contrat retiré

Un contrat public retiré bloque l’upgrade jusqu’à adaptation.

---

## 40.4 Point d’extension

Le plan doit vérifier la compatibilité des points d’extension consommés.

---

# 41. Entitlements

Un upgrade technique ne modifie pas automatiquement les entitlements commerciaux.

---

## 41.1 Nouvelle capability

Une nouvelle version de module peut introduire une capability.

Elle reste indisponible lorsque son entitlement n’est pas accordé.

---

## 41.2 Nouvel entitlement

Le plan peut signaler l’existence d’un nouvel entitlement.

Il ne l’ajoute pas à la configuration sans décision commerciale.

---

## 41.3 Prix

La mise à jour d’un catalogue de prix ne modifie pas rétroactivement le projet client.

---

# 42. Feature flags

Une nouvelle version peut introduire un feature flag.

---

## 42.1 Valeur par défaut

Le plan doit afficher :

- identifiant ;
- valeur par défaut ;
- environnements ;
- condition de retrait.

---

## 42.2 Production

Un flag de preview ne doit pas être activé automatiquement en production.

---

# 43. Kill switches

Un nouveau kill switch peut être ajouté à la configuration connue.

---

## 43.1 Valeur par défaut

Un kill switch reste inactif par défaut, sauf correctif de sécurité explicitement documenté nécessitant un refus sûr.

---

## 43.2 Runbook

Le plan doit signaler le runbook associé.

---

# 44. Modules

## 44.1 Upgrade d’un module

Le plan analyse :

- version ;
- dépendances ;
- contrats ;
- données ;
- routes ;
- permissions ;
- capabilities ;
- fichiers ;
- migrations.

---

## 44.2 Ajout de module

L’ajout d’un module appartient d’abord à la configuration client.

Il ne doit pas être provoqué uniquement par l’existence d’un nouveau package.

---

## 44.3 Retrait de module

Le retrait exige :

- modification explicite de configuration ;
- analyse des dépendants ;
- conservation des données par défaut ;
- plan de nettoyage séparé.

---

## 44.4 Dépendance transitive

Une mise à niveau peut mettre à jour une dépendance transitive.

Le plan doit la rendre visible.

---

# 45. Templates

Une nouvelle version de template peut modifier :

- structure ;
- pages ;
- sections ;
- styles ;
- composition ;
- fichiers de déploiement ;
- documentation.

---

## 45.1 Fichiers `create-once`

Les fichiers transférés au client ne sont pas écrasés par une nouvelle version du template.

---

## 45.2 Diff visuel

Une évolution visuelle significative nécessite une vérification UI et responsive.

---

## 45.3 Changement de template

Changer complètement de Template ID constitue une opération plus large qu’un upgrade ordinaire.

Elle peut nécessiter :

- projet de migration ;
- nouvelle génération ;
- analyse de contenu ;
- décision spécifique.

---

# 46. Thèmes

Un upgrade de thème doit préserver :

- tokens client ;
- identité visuelle ;
- accessibilité ;
- custom CSS ;
- responsive ;
- composants.

---

## 46.1 CSS client

Le CSS spécifique n’est pas réécrit automatiquement.

---

## 46.2 Breaking visual change

Un changement visuel incompatible nécessite :

- capture ;
- revue ;
- validation client lorsque nécessaire ;
- version majeure du thème.

---

# 47. Profil de déploiement

Un upgrade du profil `docker-compose-vps` peut modifier :

- Dockerfile ;
- Compose ;
- Caddy ;
- health checks ;
- secrets ;
- durcissement ;
- scripts.

---

## 47.1 Production

L’application locale des fichiers de déploiement ne modifie pas le VPS.

---

## 47.2 Revue renforcée

Une modification de :

- ports ;
- volumes ;
- réseaux ;
- utilisateurs ;
- permissions ;
- images ;
- secrets ;

nécessite une revue de sécurité.

---

# 48. Packages NuGet

Les versions sont mises à jour par le plan.

---

## 48.1 Central Package Management

Le fichier :

```text
Directory.Packages.props
```

peut être modifié via sa stratégie de merge.

---

## 48.2 Lock files

`packages.lock.json` est régénéré et versionné.

---

## 48.3 Locked restore

La validation finale utilise le restore verrouillé.

---

## 48.4 Preview

Une dépendance preview ne doit pas être introduite implicitement.

---

# 49. Migrations de configuration

Une nouvelle version de schéma YAML peut être nécessaire.

---

## 49.1 Fichier protégé

`client.sitefactory.yml` reste `protected`.

`upgrade apply` ne le remplace pas silencieusement.

---

## 49.2 Proposition

Le plan peut produire un candidat séparé :

```text
client.sitefactory.migrated.yml
```

ou un diff structuré.

---

## 49.3 Validation humaine

L’opérateur valide puis remplace explicitement la configuration.

---

## 49.4 Nouveau plan

Après migration de la configuration, le plan d’upgrade doit être recalculé.

---

# 50. Migrations de données

Un upgrade peut nécessiter une migration PostgreSQL.

---

## 50.1 Repository

Les fichiers de migration sont créés ou mis à jour dans le repository client selon la composition.

---

## 50.2 Production

`upgrade apply` n’applique jamais la migration production.

---

## 50.3 Rapport

Le plan indique :

```text
migration required

classification

destructive

maintenance mode

backup required

rollback strategy
```

---

## 50.4 ADR-0004

L’exécution suit :

```text
0004-explicit-production-migrations.md
```

---

# 51. Migration EF Core générée

Une migration nouvelle ou modifiée doit être :

- inspectée ;
- testée ;
- appliquée sur base vide ;
- appliquée depuis la version supportée ;
- testée avec PostgreSQL réel.

---

## 51.1 Migration existante publiée

Une migration déjà publiée et utilisée en production ne doit pas être modifiée.

Une correction produit une nouvelle migration.

---

# 52. Application atomique

Les changements sont préparés dans une zone temporaire.

---

## 52.1 Flux

```text
générer les candidats
→ vérifier les hashes
→ exécuter les merges
→ valider les fichiers
→ promouvoir
→ mettre à jour les états
→ vérifier
```

---

## 52.2 Échec

En cas d’échec avant validation finale :

- restaurer les fichiers locaux ;
- conserver un rapport ;
- ne pas déclarer l’upgrade réussi.

---

## 52.3 Git

Git reste le mécanisme principal de rollback des fichiers après l’application.

---

# 53. Mise à jour du lock file

Le nouveau `sitefactory.lock.json` n’est considéré comme valide qu’après :

- résolution ;
- génération ;
- validation des fichiers ;
- restore ;
- build ;
- tests prévus.

---

## 53.1 Application technique

Le fichier peut être écrit comme candidat pendant l’opération.

---

## 53.2 Échec final

Si les quality gates échouent, le repository doit être restauré à son état initial ou rester explicitement modifié avec un statut d’échec nécessitant un rollback Git.

La préférence reste une application transactionnelle locale.

---

# 54. Mise à jour de l’état de génération

Le fichier :

```text
.sitefactory/generation-state.json
```

est mis à jour pour refléter :

- nouvelles versions ;
- nouveaux hashes ;
- nouvelles stratégies ;
- nouveaux merge handlers ;
- fichiers retirés.

---

## 54.1 Modification manuelle

L’état n’est jamais modifié à la main afin de faire accepter un conflit.

---

# 55. Rapport d’upgrade

Une application produit :

```text
upgrade-report.json
```

---

## 55.1 Contenu conceptuel

```json
{
  "schemaVersion": 1,
  "upgradeRunId": "7ebd1462-799c-408e-ab0f-f704581c3ed6",
  "upgradePlanId": "b8804703-6f5a-40de-ab61-6a55eb6cf412",
  "projectId": "sample-project",
  "sourceCommitSha": "abc1234",
  "sourcePlatformVersion": "0.7.0",
  "targetPlatformVersion": "0.8.1",
  "startedAtUtc": "2026-07-29T09:00:00Z",
  "completedAtUtc": "2026-07-29T09:04:20Z",
  "changedFiles": 14,
  "preservedCustomFiles": 23,
  "manualActions": 2,
  "migrationRequired": true,
  "build": "success",
  "tests": "success",
  "status": "success"
}
```

---

## 55.2 Données interdites

Le rapport ne contient pas :

- secrets ;
- contenu complet des fichiers ;
- données client ;
- chaîne de connexion ;
- token ;
- clé privée.

---

# 56. Statuts d’application

Valeurs recommandées :

```text
success

success-with-manual-actions

conflict

validation-failed

partially-applied

rolled-back

failed
```

---

## 56.1 `success-with-manual-actions`

La partie automatisable est appliquée, mais des actions restent nécessaires avant release.

La branche ne doit pas être fusionnée avant leur traitement.

---

## 56.2 `partially-applied`

Statut exceptionnel indiquant que le rollback local complet n’a pas été possible.

Il nécessite une inspection immédiate.

---

# 57. Quality gates après application

Baseline :

```text
Configuration validate

Restore locked

Build Release

Tests Domain

Tests Application

Tests Infrastructure adaptés

Tests PostgreSQL si concernés

Tests d’architecture

Tests du projet généré

Validation Compose si concerné

Validation Caddy si concerné

Secret scan

Dependency scan

Static analysis
```

---

## 57.1 Portée adaptée

Le plan indique les validations supplémentaires liées aux composants modifiés.

---

## 57.2 Pas uniquement les nouveaux tests

Les suites existantes adaptées au risque doivent être réexécutées.

---

# 58. Validation du projet généré

Lorsque le générateur ou un template change, il faut produire un projet de référence.

Le projet doit :

```text
restore
build Release
test
valider Compose
valider les artefacts principaux
```

---

# 59. Tests de customizations

Le projet `Client.Customizations` doit :

- compiler ;
- réussir ses tests ;
- respecter les tests d’architecture ;
- ne pas référencer de contrat retiré ;
- ne pas accéder à des APIs internes.

---

# 60. Revue Git

Après application, l’opérateur inspecte :

```bash
git status
git diff
git diff --stat
```

---

## 60.1 Fichiers inattendus

Un fichier hors plan doit être expliqué ou retiré.

---

## 60.2 Diff massif

Un diff disproportionné peut indiquer :

- formatage ;
- changement de fins de ligne ;
- mauvais merge ;
- régénération trop large ;
- mauvais target version.

---

# 61. Commit d’upgrade

SiteFactory ne crée pas automatiquement le commit d’upgrade dans le MVP, sauf commande explicite future.

---

## 61.1 Message recommandé

```text
chore: upgrade SiteFactory to 0.8.1
```

---

## 61.2 Corps

Peut référencer :

```text
Upgrade-Plan-Id
Source-Version
Target-Version
Migration-Required
```

---

## 61.3 Validation

Le commit intervient après :

- diff ;
- build ;
- tests ;
- rapport ;
- actions manuelles terminées.

---

# 62. Push et pull request

`upgrade apply` ne pousse aucune branche.

---

## 62.1 Publication distante

Le push reste explicite conformément à l’ADR-0013.

---

## 62.2 Pull request

La création d’une pull request reste une opération séparée.

---

## 62.3 Revue

Une revue Codex ou humaine suit les documents du dossier :

```text
docs/06-codex/
```

---

# 63. Staging

Une branche d’upgrade validée est déployée en staging avant la production lorsque l’impact le justifie.

---

## 63.1 Vérifications

- migrations ;
- login ;
- contenus ;
- leads ;
- médias ;
- customizations ;
- observabilité ;
- health checks ;
- sécurité ;
- responsive ;
- dépendances externes.

---

## 63.2 Production data

Les données réelles ne sont pas copiées sans anonymisation ou procédure adaptée.

---

# 64. Release cliente

L’upgrade devient effectif pour le client uniquement lorsqu’une nouvelle release cliente est créée.

---

## 64.1 Version applicative

La release cliente suit SemVer selon :

```text
26-release-and-versioning.md
```

---

## 64.2 SiteFactory version

La version de l’application cliente et la version de la plateforme SiteFactory restent distinctes.

Exemple :

```text
Application cliente:
1.6.0

SiteFactory platform:
0.8.1
```

---

# 65. Déploiement

La release cliente n’est pas automatiquement déployée.

---

## 65.1 Production

Le déploiement suit :

```text
22-deployment-and-environments.md
```

et l’ADR-0011.

---

## 65.2 Migration

Les migrations restent explicites.

---

## 65.3 Rollback

La stratégie de rollback doit tenir compte de la compatibilité du schéma.

---

# 66. Correctifs de sécurité

Un correctif de sécurité doit être proposé avec une priorité adaptée.

---

## 66.1 Notification

SiteFactory peut signaler :

```text
security-upgrade-available
```

et identifier les projets affectés.

---

## 66.2 Application

Même critique, le correctif ne doit pas être appliqué silencieusement à une production.

---

## 66.3 Processus accéléré

Un processus accéléré peut réduire :

- délai de planification ;
- délai de revue ;
- durée de staging ;

sans supprimer :

- le plan ;
- les tests pertinents ;
- le backup si requis ;
- la validation de cible ;
- le déploiement explicite.

---

## 66.4 Kill switch

Un kill switch peut servir de mitigation temporaire conformément à l’ADR-0006.

---

# 67. Sévérité des upgrades

Valeurs recommandées :

```text
informational

recommended

important

security-high

security-critical

mandatory-before-date
```

---

## 67.1 `mandatory-before-date`

Peut être utilisé lorsqu’une version sera retirée ou deviendra incompatible.

La date et la raison doivent être explicites.

---

# 68. Correctif de package vulnérable

Le plan doit indiquer :

- package affecté ;
- version actuelle ;
- version corrigée ;
- dépendance directe ou transitive ;
- exploitabilité connue ;
- tests nécessaires ;
- projets concernés.

---

## 68.1 Package retiré

Une version retirée du feed ne doit pas être remplacée silencieusement pendant un restore.

Le projet doit recevoir un plan d’upgrade.

---

# 69. Version compromise

Si un composant SiteFactory publié est compromis :

1. identifier les projets ;
2. empêcher les nouvelles consommations ;
3. publier une version corrigée ;
4. produire un advisory ;
5. proposer un plan prioritaire ;
6. vérifier les environnements ;
7. révoquer les credentials concernés ;
8. documenter l’incident.

---

## 69.1 Immutabilité

La version compromise n’est pas remplacée sous le même numéro.

---

# 70. Downgrade

Un downgrade n’est pas traité comme un upgrade ordinaire.

---

## 70.1 Risques

- contrat plus ancien ;
- données inconnues ;
- migration inverse ;
- suppression de capability ;
- fichier non reconnu ;
- incompatibilité de customizations.

---

## 70.2 Baseline

SiteFactory ne fournit pas automatiquement :

```text
upgrade apply --target-older-version
```

comme rollback universel.

---

## 70.3 Rollback local

Avant release et déploiement, utiliser Git pour annuler les changements du repository.

---

## 70.4 Production

Après déploiement, utiliser :

- rollback applicatif compatible ;
- forward fix ;
- restauration ;

selon le plan.

---

# 71. Upgrade partiel

Le plan peut cibler un composant précis uniquement si la compatibilité globale reste satisfaite.

---

## 71.1 Exemple

```text
module sample-records
1.4.2 → 1.4.3
```

---

## 71.2 Dépendances

Les dépendances transitives nécessaires apparaissent dans le plan.

---

## 71.3 Plateforme

Un module ne peut pas être mis à jour vers une version incompatible avec la plateforme actuelle.

---

# 72. Upgrade coordonné

Certains changements nécessitent une mise à niveau coordonnée de :

- plateforme ;
- modules ;
- template ;
- profil de déploiement ;
- configuration ;
- migrations.

---

## 72.1 Version de release

Le plan peut utiliser un identifiant de release SiteFactory agrégeant les versions exactes compatibles.

---

## 72.2 Pas de couplage caché

Les composants ne doivent pas dépendre d’un « ensemble courant » non versionné.

---

# 73. Catalogues

Une nouvelle version de catalogue peut :

- ajouter des composants ;
- déprécier des versions ;
- modifier des compatibilités ;
- publier des avis.

---

## 73.1 Projet existant

Elle ne modifie pas le lock file existant.

---

## 73.2 Prix

Les changements de prix n’appartiennent pas à l’upgrade technique du projet existant.

---

# 74. Dépréciation

Chaque dépréciation doit préciser :

```text
ComponentId

DeprecatedVersion

Replacement

DeprecatedAt

RemovalTarget

MigrationGuide

SecurityImpact
```

---

## 74.1 Warning

`upgrade check` affiche les dépréciations.

---

## 74.2 Build

Une dépréciation peut également produire des warnings de compilation pour les contrats publics.

---

## 74.3 Quality gate

Un projet utilisant une version retirée peut être bloqué pour une nouvelle release selon la politique de support.

---

# 75. Version retirée

Une version retirée ne peut plus être sélectionnée pour un nouveau projet.

---

## 75.1 Projet historique

La reconstruction peut rester possible si l’artefact est conservé.

---

## 75.2 Maintenance

Aucun nouveau correctif ordinaire n’est garanti.

---

## 75.3 Sécurité

Une version retirée pour motif de sécurité peut être rendue indisponible selon une décision d’incident.

---

# 76. Absence d’accès au feed

`upgrade check` peut utiliser un cache de métadonnées pour produire un résultat partiel.

---

## 76.1 Résultat

La commande doit indiquer :

```text
freshness unknown
```

ou :

```text
remote metadata unavailable
```

---

## 76.2 Pas de conclusion trompeuse

Elle ne doit pas déclarer :

```text
up-to-date
```

sans source suffisamment récente.

---

## 76.3 Apply

`upgrade apply` échoue si les packages nécessaires ne sont pas disponibles et vérifiés.

---

# 77. Fonctionnement offline

Un upgrade planifié avec tous les artefacts présents localement peut être appliqué hors ligne si :

- versions exactes ;
- hashes ;
- packages ;
- manifests ;
- schémas ;

sont disponibles et validés.

---

## 77.1 Pas de résolution différente

Le mode offline n’utilise pas une version alternative parce que la cible exacte manque.

---

# 78. Déterminisme

À partir des mêmes :

- fichiers source ;
- commit ;
- lock files ;
- target versions ;
- packages ;
- manifests ;
- plan ;

`upgrade apply` doit produire le même résultat fonctionnel.

---

## 78.1 Données dynamiques

Les timestamps de rapport et identifiants d’exécution ne doivent pas modifier le code produit.

---

## 78.2 Ordre

Les opérations sont ordonnées de façon stable.

---

# 79. Sécurité du plan

Le plan est une entrée non fiable jusqu’à validation.

---

## 79.1 Vérifications

- schéma ;
- version ;
- project ID ;
- source commit ;
- chemins ;
- hashes ;
- target versions ;
- provenance ;
- expiration éventuelle ;
- absence de secret.

---

## 79.2 Path traversal

Un plan ne peut pas écrire hors du repository.

---

## 79.3 Signature future

Une signature ou attestation des plans pourra être introduite pour des workflows distants futurs.

---

# 80. Sources de packages

Les composants doivent provenir des sources approuvées conformément à l’ADR-0008.

---

## 80.1 Dependency confusion

Un package interne absent du feed privé ne doit pas être remplacé par une version publique.

---

## 80.2 Provenance

Les packages cibles doivent posséder une provenance suffisante.

---

# 81. Secrets

Un upgrade ne doit pas :

- lire inutilement les valeurs de secrets ;
- modifier les fichiers secrets ;
- copier les valeurs ;
- les inscrire dans les rapports ;
- les ajouter à Git.

---

## 81.1 Nouvel élément secret

Le plan peut ajouter une nouvelle référence ou exigence de secret.

La valeur réelle reste à provisionner séparément.

---

## 81.2 Validation

Le rapport indique :

```text
new secret required
```

sans valeur.

---

# 82. Configuration runtime

Une nouvelle option runtime peut être ajoutée.

---

## 82.1 Valeur par défaut

Elle doit posséder une valeur sûre ou être explicitement obligatoire.

---

## 82.2 Production

Un placeholder ne doit pas être accepté comme configuration finale.

---

## 82.3 Fichiers réels

`upgrade apply` modifie les exemples ou contrats versionnés, pas les fichiers secrets réels du VPS.

---

# 83. Observabilité

Événements recommandés :

```text
UpgradeCheckStarted

UpgradeCheckCompleted

UpgradeAvailable

SecurityUpgradeAvailable

UpgradePlanStarted

UpgradePlanCreated

UpgradePlanBlocked

UpgradeApplyStarted

UpgradeFileConflictDetected

UpgradeValidationStarted

UpgradeValidationCompleted

UpgradeApplied

UpgradeRolledBack

UpgradeFailed
```

---

## 83.1 Propriétés autorisées

```text
UpgradeRunId

UpgradePlanId

ProjectId

SourceVersion

TargetVersion

ComponentId

ComponentType

Result

Duration

ErrorCode

SecuritySeverity
```

---

## 83.2 Données interdites

```text
Secret

Token

ConnectionString

ClientData

FileContent

PrivateKey

Credential
```

---

# 84. Audit

Les actions suivantes doivent être auditables :

- plan créé ;
- cible choisie ;
- confirmation ;
- application ;
- conflit forcé éventuel ;
- action manuelle ;
- commit ;
- release ;
- migration ;
- déploiement.

---

## 84.1 Valeur commerciale

L’audit technique ne doit pas exposer inutilement les prix ou conditions commerciales.

---

# 85. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-UPGRADE-001` | Lock file absent |
| `SF-UPGRADE-002` | Lock file invalide |
| `SF-UPGRADE-003` | Configuration et lock file incohérents |
| `SF-UPGRADE-004` | État de génération invalide |
| `SF-UPGRADE-005` | Version actuelle inconnue |
| `SF-UPGRADE-006` | Version cible inconnue |
| `SF-UPGRADE-007` | Version cible non supportée |
| `SF-UPGRADE-008` | Canal preview non autorisé |
| `SF-UPGRADE-009` | Chemin d’upgrade introuvable |
| `SF-UPGRADE-010` | Version intermédiaire requise |
| `SF-UPGRADE-011` | Dépendance incompatible |
| `SF-UPGRADE-012` | Contrat public incompatible |
| `SF-UPGRADE-013` | Entitlement manquant |
| `SF-UPGRADE-014` | Changement d’entitlement non autorisé |
| `SF-UPGRADE-015` | Plan invalide |
| `SF-UPGRADE-016` | Plan expiré |
| `SF-UPGRADE-017` | Plan ne correspondant plus au projet |
| `SF-UPGRADE-018` | Commit source inattendu |
| `SF-UPGRADE-019` | Working tree non propre |
| `SF-UPGRADE-020` | Branche d’upgrade absente |
| `SF-UPGRADE-021` | Conflit de fichier généré |
| `SF-UPGRADE-022` | Conflit de merge |
| `SF-UPGRADE-023` | Customization incompatible |
| `SF-UPGRADE-024` | Migration de configuration requise |
| `SF-UPGRADE-025` | Migration PostgreSQL requise |
| `SF-UPGRADE-026` | Package cible indisponible |
| `SF-UPGRADE-027` | Source de package non approuvée |
| `SF-UPGRADE-028` | Restore verrouillé échoué |
| `SF-UPGRADE-029` | Build échoué |
| `SF-UPGRADE-030` | Tests échoués |
| `SF-UPGRADE-031` | Validation de déploiement échouée |
| `SF-UPGRADE-032` | Secret détecté |
| `SF-UPGRADE-033` | Application atomique échouée |
| `SF-UPGRADE-034` | Rollback local échoué |
| `SF-UPGRADE-035` | Rapport absent |
| `SF-UPGRADE-036` | Version retirée |
| `SF-UPGRADE-037` | Version dépréciée |
| `SF-UPGRADE-038` | Métadonnées distantes indisponibles |
| `SF-UPGRADE-039` | Downgrade non supporté |
| `SF-UPGRADE-040` | Validation humaine manquante |

---

# 86. Tests unitaires

Tester :

- comparaison de versions ;
- SemVer ;
- canaux ;
- résolution exacte ;
- compatibilité ;
- chemins d’upgrade ;
- version intermédiaire ;
- dépréciation ;
- version retirée ;
- validation de plan ;
- empreintes ;
- codes d’erreur.

---

# 87. Tests de `upgrade check`

Couvrir :

```text
projet à jour

patch disponible

minor disponible

major disponible

security patch

version preview

version deprecated

version retired

métadonnées distantes absentes

lock file incohérent
```

---

# 88. Tests de `upgrade plan`

Vérifier :

- aucun effet de bord ;
- source exacte ;
- cible exacte ;
- dépendances ;
- fichiers ;
- customizations ;
- migrations ;
- conflits ;
- ordre déterministe ;
- hash du plan ;
- secret absent.

---

# 89. Tests de `upgrade apply`

Vérifier :

```text
plan valide

plan modifié

source commit modifié

working tree sale

branche absente

package indisponible

fichier generated modifié

merge conflictuel

custom préservé

protected préservé

apply réussi

validation échouée

rollback réussi

rollback échoué
```

---

# 90. Tests de fichiers

Conformément à l’ADR-0005 :

```text
generated inchangé
→ mise à jour

generated modifié
→ conflit

create-once
→ préservé

merge compatible
→ fusion

merge conflictuel
→ blocage

custom
→ préservé

protected
→ préservé
```

---

# 91. Tests des customizations

Tester :

- contrat encore compatible ;
- contrat déprécié ;
- contrat retiré ;
- point d’extension versionné ;
- module client ;
- dépendance interne interdite ;
- compilation ;
- tests client.

---

# 92. Tests de modules

Tester :

- patch ;
- minor ;
- major ;
- dépendance transitive ;
- cycle nouveau ;
- version incompatible ;
- preview ;
- retrait ;
- nouvelles permissions ;
- nouvelles capabilities.

---

# 93. Tests d’entitlements

Vérifier que :

- un nouveau module n’est pas ajouté implicitement ;
- un nouvel entitlement n’est pas accordé ;
- un prix ne modifie pas le projet ;
- un flag ne contourne pas l’entitlement ;
- un administrateur ne reçoit pas une capability non acquise.

---

# 94. Tests de configuration

Couvrir :

- schema version identique ;
- migration de schéma ;
- propriété renommée ;
- propriété obligatoire ;
- candidat séparé ;
- fichier original préservé ;
- nouveau plan après migration.

---

# 95. Tests PostgreSQL

Lorsqu’une migration est requise :

- base vide ;
- ancienne version supportée ;
- données représentatives ;
- contraintes ;
- rollback ou restauration prévue ;
- production non touchée.

---

# 96. Tests du profil de déploiement

Lorsque le profil change :

- `docker compose config` ;
- ports ;
- réseaux ;
- volumes ;
- secrets ;
- utilisateur ;
- health checks ;
- Caddy ;
- images ;
- absence de `latest`.

---

# 97. Tests de sécurité

Vérifier :

- source de package ;
- dependency confusion ;
- secret dans le plan ;
- path traversal ;
- package compromis ;
- nouvelle permission ;
- nouvelle route ;
- exposition de port ;
- secret runtime requis ;
- code non approuvé.

---

# 98. Tests d’atomicité

Simuler un échec :

- pendant génération ;
- pendant merge ;
- pendant écriture du lock file ;
- pendant mise à jour de l’état ;
- pendant restore ;
- pendant build ;
- pendant rapport.

Le repository doit revenir à l’état source ou signaler précisément l’état partiel.

---

# 99. Tests multiplateformes

Les scénarios principaux s’exécutent sur :

```text
Windows x64

Linux x64
```

Ils vérifient :

- chemins ;
- fins de ligne ;
- Git ;
- fichiers temporaires ;
- atomicité ;
- outils ;
- permissions lorsque pertinentes.

---

# 100. Golden upgrade tests

Des golden tests doivent couvrir des projets de référence.

Exemples :

```text
Offre applicative illustrative sans customization
0.7.0 → 0.8.0

Offre applicative illustrative avec customization
0.7.0 → 0.8.0

Projet avec fichier generated modifié
→ conflit

Projet avec module retiré
→ plan manuel
```

---

## 100.1 Golden files

Les changements des golden files sont inspectés explicitement.

---

# 101. Quality gates d’un upgrade

Un upgrade ne peut pas être considéré comme prêt à fusionner si :

```text
Plan absent

Version cible ambiguë

Lock files incohérents

Conflit ouvert

Customization cassée

Migration non inspectée

Build échoué

Test échoué

Scan échoué

Contrat incompatible

Entitlement modifié implicitement

Fichier custom modifié

Fichier protected modifié

Rapport absent

Action manuelle non traitée
```

---

# 102. Quality gates de production

La production n’est pas mise à niveau si :

```text
Release cliente absente

Staging requis non validé

Backup obligatoire absent

Migration inattendue

Image non approuvée

Readiness échoue

Smoke tests échouent

Rollback non défini

Secret requis non provisionné
```

---

# 103. CI/CD

La CI peut exécuter :

- `upgrade check` ;
- validation d’un plan ;
- build d’une branche d’upgrade ;
- tests ;
- scans ;
- projet généré ;
- image candidate.

---

## 103.1 Modification automatique

La CI ne doit pas modifier automatiquement la branche principale d’un projet client.

---

## 103.2 Pull request automatique future

Un système futur pourra proposer une pull request après décision explicite.

Il ne pourra pas :

- fusionner automatiquement ;
- modifier les entitlements ;
- migrer ;
- déployer ;

sans nouvelles décisions et protections.

---

# 104. Notifications

LogicGate peut recevoir des notifications pour :

- correctif disponible ;
- version dépréciée ;
- vulnérabilité ;
- fin de support ;
- incompatibilité prochaine.

---

## 104.1 Pas d’application

Une notification n’est pas une autorisation d’application.

---

## 104.2 Priorisation

Elle doit fournir :

- composants affectés ;
- projets concernés ;
- sévérité ;
- version corrigée ;
- délai recommandé.

---

# 105. Fréquence de contrôle

La fréquence peut dépendre :

- du niveau de service ;
- de la criticité ;
- du nombre de clients ;
- des avis de sécurité.

---

## 105.1 Production maintenue

Une revue régulière des versions est obligatoire.

---

## 105.2 Projet sans maintenance

Le handover doit préciser que le client devient responsable des contrôles futurs.

---

# 106. Transfert client

Le handover doit inclure :

- versions exactes ;
- lock files ;
- packages requis ;
- matrice de support ;
- procédure `upgrade check` ;
- procédure `upgrade plan` ;
- procédure `upgrade apply` ;
- customizations ;
- migrations ;
- derniers rapports ;
- dépendances privées ;
- responsabilités.

---

## 106.1 Accès au feed

Le client doit pouvoir reconstruire la version transférée conformément à l’ADR-0008.

---

## 106.2 Support futur

L’accès aux futures versions dépend du contrat ou de la licence applicable.

---

# 107. Clôture de service

Lors de la clôture :

- produire un dernier inventaire ;
- indiquer les versions actuelles ;
- indiquer les versions disponibles ;
- signaler les dépréciations ;
- remettre les lock files ;
- transférer les packages nécessaires ;
- documenter la dernière campagne de maintenance ;
- définir les responsabilités futures.

---

# 108. Sécurité

## 108.1 Actifs

- code client ;
- packages ;
- données ;
- migrations ;
- configuration ;
- customizations ;
- images ;
- feed ;
- lock files.

---

## 108.2 Menaces

- package malveillant ;
- dependency confusion ;
- latest implicite ;
- plan falsifié ;
- path traversal ;
- entitlements modifiés ;
- migration non revue ;
- fichier client écrasé ;
- correctif critique retardé ;
- version vulnérable conservée.

---

## 108.3 Contrôles

- versions exactes ;
- lock files ;
- provenance ;
- check ;
- plan ;
- confirmation ;
- stratégies de fichiers ;
- Git ;
- tests ;
- feed privé ;
- inventaire ;
- alertes ;
- migrations séparées.

---

# 109. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Dernière version malveillante sélectionnée | version exacte et source approuvée |
| Plan appliqué au mauvais commit | Source Commit SHA |
| Fichier client écrasé | hash et stratégie `custom` |
| Entitlement ajouté par upgrade | séparation technique/commerciale |
| Migration exécutée pendant apply | séparation avec ADR-0004 |
| Upgrade poussé sans revue | aucune écriture distante |
| Package public homonyme | Package Source Mapping |
| Plan modifié | empreinte et validation |
| Correctif critique ignoré | inventaire et notification |
| Version retirée reconstruite différemment | artefacts immuables |

---

# 110. Règles pour LogicGate

LogicGate doit :

- maintenir les catalogues ;
- publier des versions immuables ;
- maintenir la matrice de compatibilité ;
- identifier les correctifs ;
- inventer les chemins d’upgrade nécessaires ;
- planifier les campagnes ;
- revoir les diffs ;
- tester les customizations ;
- séparer l’upgrade du déploiement.

---

## 110.1 LogicGate ne doit pas

- modifier un lock file à la main ;
- utiliser `latest` ;
- sauter une version intermédiaire requise ;
- traiter une preview comme stable ;
- fusionner une branche cassée ;
- retarder sans décision un correctif critique ;
- modifier un entitlement par commodité technique.

---

# 111. Règles pour Codex

Codex peut :

- exécuter `upgrade check` ;
- analyser un plan ;
- préparer une branche locale si demandé ;
- appliquer un plan explicitement validé ;
- corriger les incompatibilités cadrées ;
- ajouter les tests ;
- produire une revue ;
- produire un rapport.

---

## 111.1 Codex ne doit pas

```text
Choisir seul une version majeure

Modifier le lock file manuellement

Sélectionner latest

Ajouter un entitlement

Réécrire une customization sans tâche

Forcer un conflit de fichier

Pousser la branche

Créer une pull request

Fusionner

Créer un tag

Migrer la production

Déployer

Modifier un secret réel

Masquer un test échoué
```

---

## 111.2 Décision humaine

Une décision humaine est requise pour :

- version cible majeure ;
- conflit de customization ;
- changement d’entitlement ;
- migration destructive ;
- retrait de module ;
- changement de template ;
- transition de stratégie de fichier ;
- dérogation à un quality gate ;
- release ;
- déploiement.

---

# 112. Anti-patterns interdits

## 112.1 `latest`

```text
LogicGate.SiteFactory.Platform: latest
```

---

## 112.2 Upgrade au démarrage

L’application vérifie et installe une nouvelle version au runtime.

---

## 112.3 Lock file régénéré pendant un simple restore

Le graphe change sans pull request.

---

## 112.4 Upgrade directement sur `main`

Une commande modifie la branche principale sans branche ou validation.

---

## 112.5 Push automatique

`upgrade apply` pousse les changements.

---

## 112.6 Merge automatique

Une pull request d’upgrade est fusionnée parce que les tests principaux sont verts sans revue des migrations et customizations.

---

## 112.7 Entitlement implicite

Un nouveau module ou une nouvelle capability devient disponible après une mise à jour technique.

---

## 112.8 Migration implicite

Une nouvelle application applique sa migration au démarrage.

---

## 112.9 Fichier custom modifié

Le générateur adapte automatiquement le code client.

---

## 112.10 Downgrade comme rollback universel

La version précédente est réinstallée sans vérifier le schéma.

---

## 112.11 Plan recalculé pendant apply

La commande applique une cible différente de celle revue.

---

## 112.12 Tests ciblés uniquement

Seuls les nouveaux tests sont exécutés alors que les changements affectent plusieurs couches.

---

# 113. Conséquences positives

La décision apporte :

- builds reproductibles ;
- versions clientes identifiables ;
- upgrades contrôlés ;
- customizations protégées ;
- correctifs traçables ;
- migrations séparées ;
- rollback Git ;
- support de plusieurs clients ;
- inventaire des vulnérabilités ;
- absence de déploiement implicite ;
- transfert facilité ;
- meilleure gestion des dépréciations ;
- réduction des changements silencieux.

---

# 114. Conséquences négatives

La décision entraîne :

- plusieurs versions à maintenir ;
- catalogues et matrices ;
- moteur d’upgrade ;
- plans ;
- rapports ;
- branches ;
- tests supplémentaires ;
- conflits ;
- campagnes de maintenance ;
- temps de staging ;
- inventaire des projets ;
- support d’anciens chemins de migration.

---

# 115. Risques acceptés

## 115.1 Retard d’un client

Un client peut rester temporairement sur une ancienne version.

---

## 115.2 Coût de support

Plusieurs versions supportées augmentent la charge.

---

## 115.3 Conflits manuels

Certaines customizations nécessiteront une adaptation humaine.

---

## 115.4 Délai de correctif

Le processus explicite peut être plus lent qu’une mise à jour automatique.

Ce coût est accepté pour éviter une modification non maîtrisée de production.

---

# 116. Mesures compensatoires

- inventaire ;
- alertes ;
- politiques de support ;
- versions exactes ;
- plans déterministes ;
- branches dédiées ;
- tests ;
- staging ;
- processus accéléré sécurité ;
- kill switches ;
- packages conservés ;
- migration guides ;
- rapports ;
- handover.

---

# 117. Performance

## 117.1 Check

`upgrade check` doit rester suffisamment rapide pour une exécution régulière.

---

## 117.2 Plan

Le plan peut nécessiter :

- récupération de manifests ;
- génération en mémoire ;
- comparaison de fichiers ;
- analyse du graphe.

---

## 117.3 Apply

Le coût principal peut provenir :

- restore ;
- build ;
- tests ;
- génération ;
- Docker.

---

## 117.4 Cache

Un cache peut être utilisé sans compromettre :

- versions ;
- provenance ;
- lock files ;
- déterminisme.

---

# 118. Coûts

## 118.1 Coûts supplémentaires

- développement du moteur ;
- maintenance des chemins ;
- tests ;
- revues ;
- staging ;
- support multi-version ;
- inventaire.

---

## 118.2 Coûts évités

- régressions silencieuses ;
- production cassée ;
- forks permanents ;
- mise à jour manuelle totale ;
- perte de customizations ;
- migrations accidentelles ;
- builds non reproductibles ;
- failles non inventoriées.

---

# 119. Maintenabilité

Pour chaque projet, il doit être possible de répondre :

```text
Quelles versions sont utilisées ?

Quelles nouvelles versions existent ?

Quelle version est supportée ?

Quel plan permet de migrer ?

Quels fichiers seront modifiés ?

Quelles customizations sont affectées ?

Une migration est-elle nécessaire ?

Quels tests ont été exécutés ?

Quelle release cliente contient l’upgrade ?

La production a-t-elle été déployée ?
```

---

# 120. Compatibilité

## 120.1 Schéma du lock file

Le lock file possède une `schemaVersion`.

---

## 120.2 CLI ancien

Un CLI ancien rencontrant un lock file futur doit refuser avant écriture.

---

## 120.3 Plan ancien

Un plan produit par une version incompatible du CLI doit être refusé.

---

## 120.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration d’un projet utilisant des versions flottantes nécessite un verrouillage initial explicite.

---

# 121. Migration depuis des versions flottantes

Le processus est :

```text
inventorier les versions réellement restaurées
→ vérifier les sources
→ produire packages.lock.json
→ produire sitefactory.lock.json
→ vérifier le projet
→ créer une baseline Git
→ désactiver les plages flottantes
→ exécuter restore locked
```

---

## 121.1 Version inconnue

Une version impossible à identifier doit être traitée comme une incertitude bloquante.

---

# 122. Migration d’un projet sans lock file SiteFactory

Le processus d’adoption doit :

1. identifier le générateur supposé ;
2. comparer les fichiers ;
3. inventorier les modules ;
4. inventorier les packages ;
5. identifier les customizations ;
6. produire un lock file candidat ;
7. valider humainement ;
8. créer l’état de génération ;
9. tester un upgrade pilote.

---

## 122.1 Pas d’adoption automatique

SiteFactory ne doit pas prétendre connaître la composition historique uniquement à partir du code courant.

---

## 122.2 Production

```text
Production execution: out of scope
```

Cet ADR ne déclenche aucune mise à niveau ou migration réelle.

---

# 123. Réversibilité

## 123.1 Classification

```text
élevée avant release et déploiement

moyenne après release

variable après migration de données
```

---

## 123.2 Avant commit

L’application locale peut être annulée par le mécanisme atomique ou Git.

---

## 123.3 Après commit

Un revert ou une branche de correction peut annuler les fichiers, sous réserve de compatibilité.

---

## 123.4 Après production

Le rollback dépend :

- du schéma ;
- des données ;
- de l’image ;
- des secrets ;
- des opérations réalisées.

---

# 124. Plan d’implémentation

## Étape 1 — Contrats de versions

Définir :

- Component ID ;
- Component Type ;
- Version ;
- Channel ;
- Compatibility ;
- Support status.

---

## Étape 2 — Lock file

Finaliser le schéma de :

```text
sitefactory.lock.json
```

---

## Étape 3 — Compatibilité

Créer une matrice machine-readable.

---

## Étape 4 — `upgrade check`

Implémenter la comparaison sans effet de bord.

---

## Étape 5 — `upgrade plan`

Créer :

- résolution ;
- graphe ;
- changements ;
- migrations ;
- conflits ;
- artefact JSON.

---

## Étape 6 — Validation du plan

Ajouter :

- source commit ;
- hashes ;
- versions ;
- chemins ;
- provenance.

---

## Étape 7 — `upgrade apply`

Intégrer :

- stratégies de fichiers ;
- application atomique ;
- lock files ;
- état ;
- rapports.

---

## Étape 8 — Quality gates

Exécuter :

- restore ;
- build ;
- tests ;
- scans ;
- projet généré ;
- Compose.

---

## Étape 9 — Inventaire

Maintenir les versions clientes et leur support.

---

## Étape 10 — Sécurité

Ajouter :

- avis ;
- priorisation ;
- campagnes ;
- notifications ;
- kill switches de mitigation.

---

# 125. Tâches liées proposées

```text
SF-TASK-145 — Define versioned SiteFactory component contracts

SF-TASK-146 — Finalize the SiteFactory lock file schema

SF-TASK-147 — Add the machine-readable compatibility matrix

SF-TASK-148 — Implement `sitefactory upgrade check`

SF-TASK-149 — Implement deterministic upgrade planning

SF-TASK-150 — Define the upgrade plan schema

SF-TASK-151 — Validate upgrade plans against repository state

SF-TASK-152 — Implement explicit local upgrade application

SF-TASK-153 — Add upgrade atomicity and rollback

SF-TASK-154 — Add customization compatibility diagnostics

SF-TASK-155 — Add migration impact reporting

SF-TASK-156 — Add upgrade quality-gate orchestration

SF-TASK-157 — Add client version inventory reporting

SF-TASK-158 — Add security upgrade advisories
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 126. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Chaque projet possède un lock file exact.

Une version flottante est refusée.

Une nouvelle version publiée ne modifie pas un projet existant.

Upgrade check ne modifie aucun fichier.

Upgrade plan ne modifie aucun fichier.

Le plan contient les versions source et cible.

Le plan contient le commit source.

Le plan est invalidé si le repository change.

Upgrade apply exige un working tree propre.

Upgrade apply exige une branche dédiée.

Les fichiers custom sont préservés.

Les fichiers protected sont préservés.

Un fichier generated modifié produit un conflit.

Les entitlements ne sont pas accordés automatiquement.

Les customizations sont compilées et testées.

Les packages sont restaurés depuis les sources approuvées.

Les lock files NuGet sont mis à jour.

Une migration requise est signalée sans être appliquée.

Un échec de validation empêche une release.

Upgrade apply ne pousse aucune branche.

Upgrade apply ne crée aucun tag.

Upgrade apply ne déploie aucun environnement.

Un rollback Git reste possible avant production.

Un correctif de sécurité est identifiable par projet.

Les résultats sont reproductibles sur Windows et Linux.
```

---

# 127. Conditions de révision

Réexaminer cette décision si :

- le nombre de clients rend les upgrades explicites non soutenables ;
- une plateforme centrale de gestion devient nécessaire ;
- les customizations disparaissent presque totalement ;
- toutes les productions doivent recevoir immédiatement les mêmes versions ;
- un modèle SaaS remplace les repositories isolés ;
- les mises à niveau doivent être appliquées sans intervention humaine ;
- un service de control plane distant est introduit ;
- le nombre de chemins de migration devient trop important ;
- les clients exigent un mécanisme de mise à jour autonome ;
- des contraintes réglementaires imposent un patching automatique ;
- SiteFactory adopte un autre modèle de distribution que NuGet et Git.

---

# 128. Indicateurs à suivre

- versions de plateforme actives ;
- versions de modules actives ;
- projets à jour ;
- projets deprecated ;
- projets unsupported ;
- upgrades disponibles ;
- correctifs de sécurité en attente ;
- durée moyenne d’un upgrade ;
- conflits par upgrade ;
- customizations cassées ;
- migrations nécessaires ;
- upgrades échoués ;
- rollbacks ;
- temps entre publication et adoption ;
- nombre de versions supportées simultanément ;
- plans invalidés ;
- projets sans inventaire récent.

---

# 129. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des versions flottantes apparaissent

les lock files ne correspondent pas aux packages

des projets restent sur des versions vulnérables

upgrade apply modifie main directement

des customizations sont réécrites automatiquement

des entitlements changent pendant un upgrade technique

des migrations sont appliquées au démarrage

des plans ne correspondent pas au commit source

des upgrades poussent ou déploient automatiquement

les conflits sont forcés sans revue

les rapports ne permettent pas d’identifier la cible

les versions retirées restent utilisées sans plan
```

---

# 130. Conséquences observées

Cette section est append-only après mise en œuvre.

## 130.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète
des commandes upgrade check, upgrade plan et upgrade apply.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- source ;
- cible ;
- durée ;
- conflits ;
- migrations ;
- résultat ;
- action proposée.

---

# 131. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- `10-feature-flags-and-entitlements.md`
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `15-cli-specification.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `22-deployment-and-environments.md`
- `23-backup-and-restore.md`
- `24-operational-runbooks.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `27-client-handover-and-service-closure.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0002-yaml-client-configuration.md`
- `0003-postgresql-as-primary-database.md`
- `0004-explicit-production-migrations.md`
- `0005-file-generation-strategies.md`
- `0006-feature-flags-and-entitlements-separation.md`
- `0007-module-composition-and-public-contracts.md`
- `0008-private-package-feed-and-supply-chain.md`
- `0009-client-customizations-and-extension-points.md`
- `0010-structured-observability-with-opentelemetry.md`
- `0011-docker-compose-vps-deployment-profile.md`
- `0012-runtime-configuration-and-secrets.md`
- `0013-local-git-initialization-and-explicit-remote-publishing.md`

---

# 132. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 127 |

---

# 133. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 134. Synthèse de la décision

```text
Configuration
→ exprime l’intention

Lock file
→ conserve les versions exactes

Nouvelle version publiée
→ aucun effet automatique sur les clients

Upgrade check
→ détecte
→ lecture seule

Upgrade plan
→ résout les versions exactes
→ calcule les changements
→ signale conflits et migrations
→ lecture seule

Upgrade apply
→ plan validé
→ working tree propre
→ branche dédiée
→ application locale
→ customizations préservées
→ lock files mis à jour
→ build et tests
→ aucun push
→ aucun déploiement

Entitlements
→ jamais accordés automatiquement

Migrations
→ signalées
→ générées dans le repository
→ jamais appliquées automatiquement en production

Release
→ décision séparée

Déploiement
→ décision séparée

Sécurité
→ correctifs prioritaires
→ inventaire des projets affectés
→ processus accéléré possible
→ aucune mutation silencieuse de production

Rollback
→ Git avant production
→ image, forward fix ou restauration après production
```

> SiteFactory doit pouvoir proposer et automatiser une mise à niveau sans décider seul de modifier un projet client. Les versions exactes garantissent la reproductibilité ; le plan, la branche, les tests et la revue garantissent que l’évolution reste volontaire et maîtrisée.
