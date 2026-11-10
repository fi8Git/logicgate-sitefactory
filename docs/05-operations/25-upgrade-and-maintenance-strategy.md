# LogicGate SiteFactory — Stratégie de mise à niveau et de maintenance

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Stratégie de mise à niveau et de maintenance |
| Version | 1.0 |
| Statut | Contrat cible ; capacités publiques livrées distinguées |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Projets concernés | SiteFactory, modules, templates, thèmes et projets clients générés |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../02-architecture/module-catalog.md`, `../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md`, `../02-architecture/core-module-boundary.md`, `09-module-architecture.md`, `10-feature-flags-and-entitlements.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `13-generation-pipeline.md`, `15-cli-specification.md`, `18-security-baseline.md`, `19-testing-strategy.md`, `20-quality-gates-and-ci.md`, `21-observability-and-logging.md`, `22-deployment-and-environments.md`, `23-backup-and-restore.md`, `24-operational-runbooks.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Périmètre public livré et maintenance cible

Le [README public](../../README.md) décrit les composants disponibles :
Core/ModuleSdk, Documentation, runtime/PostgreSQL génériques et Migrator explicite.
La [CLI actuelle](../03-generation/initial-generation-cli.md) propose validate,
plan et generate ; **aucune commande upgrade check/plan/apply** n'est livrée.
Une régénération reconnue à composition identique n'est pas un upgrade.

Les sections numérotées conservent le **contrat cible de maintenance** :
versions explicites, compatibilité, plan revu, customizations protégées,
migrations volontaires, rollback applicatif distinct du rollback de données.
Leur présence ne livre pas un déploiement, backup/restore, maintenance client
ou support opérationnel. Les procédures propres aux applications Business
et leurs preuves demeurent privées ; le public n'en dépend pas.

[ADR-0014](../adr/0014-explicit-client-upgrades-and-version-pinning.md) interdit
tout upgrade implicite. [ADR-0036](../adr/0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md)
fixe SemVer et la compatibilité explicite : aujourd'hui les versions exactes sont
validées, les plages et la matrice de support restent à réaliser.
Aucun package SiteFactory n'est publié ; aucun numéro illustratif ci-dessous
n'annonce une release. Les identifiants `sample-*` sont fictifs, pas des modules
installés ou des commandes exécutables.

Les préconditions de repository propre/branche dédiée visent un **futur apply**
autorisé. Un agent intervenant sur la documentation doit préserver le worktree
existant, jamais le nettoyer par restauration ou suppression.
Les opérations production exigent une décision humaine spécifique ; la lecture
de ce contrat ne constitue pas une autorisation.
Les licences Business, contenus tiers et sorties générées restent inchangées
selon [ADR-0034](../adr/0034-apache-licence-choice-for-publish-site-factory.md).

---

## 1. Objet du document

Ce document définit la stratégie de maintenance et de mise à niveau de LogicGate SiteFactory et des applications clientes générées.

Il précise notamment :

- les différents types de maintenance ;
- les responsabilités de LogicGate et du client ;
- le versionnement des composants SiteFactory ;
- les versions supportées ;
- la détection des mises à jour ;
- le processus `check`, `plan` et `apply` ;
- les branches de mise à niveau ;
- la préservation des personnalisations ;
- la mise à jour des packages ;
- la mise à jour des templates et thèmes ;
- les migrations de données ;
- les correctifs de sécurité ;
- les hotfixes ;
- les dépréciations ;
- les breaking changes ;
- les tests ;
- la recette ;
- le déploiement ;
- le rollback ;
- les fenêtres de maintenance ;
- la maintenance préventive ;
- les services mensuels ;
- la fin de maintenance ;
- les contraintes applicables à Codex.

L’objectif est d’éviter deux situations opposées :

```text
Projet jamais mis à jour
→ dette, vulnérabilités et obsolescence

Projet mis à jour automatiquement sans contrôle
→ régressions, conflits et perte de données
```

---

## 2. Vision

Un projet généré par SiteFactory reste un projet logiciel autonome.

Il ne doit pas dépendre en permanence de la dernière version du générateur pour fonctionner.

Le flux de référence d’une mise à niveau est :

```text
Nouvelle version disponible
→ détection
→ qualification
→ plan
→ branche dédiée
→ backup si nécessaire
→ application
→ migration éventuelle
→ build
→ tests
→ revue
→ staging
→ recette
→ déploiement contrôlé
→ surveillance
```

Une mise à niveau ne doit jamais être :

- automatique en production ;
- appliquée sans connaître les versions source et cible ;
- appliquée sans vérifier les personnalisations ;
- appliquée sans plan de migration ;
- considérée comme réussie sans build et tests ;
- confondue avec une simple mise à jour de package.

---

## 3. Principes fondamentaux

## 3.1 Les projets clients sont versionnés explicitement

Chaque projet client verrouille :

- version du générateur ;
- version du catalogue ;
- version du template ;
- version du thème ;
- versions des modules ;
- versions des dépendances principales.

Ces informations sont enregistrées dans :

```text
sitefactory.lock.json
```

---

## 3.2 Les mises à jour ne sont jamais flottantes

Sont interdits :

```text
latest
*
1.*
main
master
```

comme référence de production non maîtrisée.

Les packages, images et outils utilisent des versions explicites.

---

## 3.3 Une mise à niveau est planifiée avant application

Le cycle obligatoire est :

```text
upgrade check
→ upgrade plan
→ upgrade apply
```

Une commande `apply` ne doit pas résoudre silencieusement une nouvelle cible différente de celle présentée dans le plan.

---

## 3.4 Les personnalisations restent protégées

Une mise à niveau ne doit jamais écraser silencieusement :

- `ClientName.Customizations` ;
- les contenus validés ;
- les migrations existantes ;
- les fichiers `custom` ;
- les fichiers `protected` ;
- la configuration client ;
- les secrets ;
- les runbooks adaptés au client.

---

## 3.5 Une mise à jour de code n’est pas une migration de données

La stratégie distingue :

```text
mise à jour applicative
mise à jour des packages
migration de configuration
migration de données
migration de stockage
modification d’infrastructure
```

Chaque catégorie possède ses propres risques et validations.

---

## 3.6 Les correctifs de sécurité sont prioritaires

Une vulnérabilité critique ou haute exploitable doit être traitée avant les évolutions fonctionnelles non urgentes.

---

## 3.7 Une version publiée est immuable

Une version déjà publiée d’un :

- package ;
- CLI ;
- template ;
- thème ;
- module ;
- artefact ;

ne doit pas être remplacée silencieusement.

Toute correction produit une nouvelle version.

---

## 3.8 Une maintenance ne garantit pas l’absence d’incident

La maintenance réduit les risques par :

- surveillance ;
- mises à jour ;
- tests ;
- sauvegardes ;
- corrections ;
- procédures.

Elle ne constitue pas une garantie de disponibilité ou de sécurité absolue.

---

# 4. Non-objectifs

La stratégie initiale ne cherche pas à fournir :

- une mise à jour simultanée automatique de tous les clients ;
- une plateforme SaaS centralisée de patch management ;
- une compatibilité illimitée avec toutes les anciennes versions ;
- un support permanent de modules abandonnés ;
- une migration automatique des personnalisations complexes ;
- un auto-merge des mises à jour ;
- un déploiement automatique de chaque correctif ;
- une correction automatique des conflits ;
- une obligation de mise à niveau pour un client hors contrat ;
- une garantie de compatibilité avec des modifications manuelles non documentées.

---

# 5. Terminologie

## 5.1 Maintenance corrective

Correction d’un défaut existant.

Exemples :

- erreur fonctionnelle ;
- régression ;
- problème responsive ;
- erreur de validation ;
- problème de formulaire ;
- défaut de sécurité.

---

## 5.2 Maintenance préventive

Action destinée à réduire un risque futur.

Exemples :

- mise à jour d’une dépendance ;
- rotation d’un secret ;
- nettoyage d’un disque ;
- test de restauration ;
- remplacement d’une action GitHub dépréciée ;
- revue des comptes.

---

## 5.3 Maintenance évolutive

Ajout ou modification d’une capacité fonctionnelle.

Exemples :

- nouveau module ;
- nouvelle page ;
- nouveau workflow ;
- nouvelle intégration ;
- modification du design system.

Elle peut nécessiter un devis séparé.

---

## 5.4 Maintenance adaptative

Modification nécessaire pour rester compatible avec un environnement externe.

Exemples :

- nouvelle version .NET ;
- nouvelle version PostgreSQL ;
- changement GitHub Actions ;
- modification d’un fournisseur SMTP ;
- changement d’API tierce ;
- changement de navigateur ou de politique de sécurité.

---

## 5.5 Patch

Mise à jour compatible corrigeant principalement un défaut.

Exemple :

```text
1.2.3 → 1.2.4
```

---

## 5.6 Minor upgrade

Mise à jour ajoutant des capacités compatibles.

Exemple :

```text
1.2.4 → 1.3.0
```

---

## 5.7 Major upgrade

Mise à jour pouvant contenir des incompatibilités.

Exemple :

```text
1.9.0 → 2.0.0
```

---

## 5.8 Hotfix

Correctif urgent appliqué selon un processus accéléré, sans supprimer les contrôles essentiels.

---

## 5.9 Security fix

Correction d’une vulnérabilité ou d’un durcissement de sécurité.

---

## 5.10 Upgrade plan

Artefact décrivant précisément les changements prévus.

---

## 5.11 Maintenance window

Période prévue pendant laquelle une intervention peut être réalisée.

---

## 5.12 End of support

Date ou version à partir de laquelle LogicGate ne garantit plus la maintenance standard d’une version.

---

# 6. Composants versionnés

Les composants suivants évoluent indépendamment :

```text
SiteFactory CLI
SiteFactory Generator
Configuration Schema
Catalog
Module Manifest Schema
Template Manifest Schema
Theme Manifest Schema
Platform Core
Design System
Modules
Templates
Themes
Generated Client Project
Deployment Profile
Operational Scripts
Prompts LLM
```

Une mise à niveau doit identifier tous les composants concernés.

---

# 7. Versionnement sémantique

Les composants utilisent :

```text
MAJOR.MINOR.PATCH
```

## 7.1 PATCH

Exemples :

- correction de bug ;
- correction de sécurité compatible ;
- amélioration de diagnostic ;
- correction CSS ;
- optimisation interne ;
- documentation.

---

## 7.2 MINOR

Exemples :

- nouvelle capacité compatible ;
- nouvelle option facultative ;
- nouveau composant ;
- nouvelle permission non obligatoire ;
- nouvelle variante de section ;
- nouveau contrôle compatible.

---

## 7.3 MAJOR

Exemples :

- suppression de contrat ;
- renommage d’un identifiant stable ;
- modification incompatible du schéma ;
- changement de structure client ;
- migration manuelle obligatoire ;
- suppression d’une route ;
- changement de comportement incompatible ;
- abandon d’une version .NET.

---

## 7.4 Versions pré-1.0

Avant `1.0.0`, SiteFactory utilise :

```text
0.MINOR.PATCH
```

Un changement de version mineure peut contenir des évolutions importantes.

Les breaking changes doivent malgré tout être :

- documentés ;
- planifiés ;
- testés ;
- annoncés dans le changelog.

---

# 8. Source de vérité des versions

Les versions sont enregistrées dans :

```text
sitefactory.lock.json
Directory.Packages.props
global.json
image tag ou digest
migration history
CHANGELOG.md
```

Le projet ne doit pas maintenir plusieurs versions contradictoires du même composant.

---

# 9. Support des versions SiteFactory

## 9.1 Phase pré-1.0

Avant la version `1.0.0`, la politique proposée est :

```text
Dernière version mineure
→ support fonctionnel et correctifs

Version mineure précédente
→ correctifs critiques et de sécurité pendant 90 jours,
  lorsque techniquement raisonnable

Versions plus anciennes
→ upgrade requis avant correction standard
```

Cette politique reste adaptée à un produit interne en construction.

---

## 9.2 Après la version 1.0

Politique cible proposée :

```text
Version majeure courante
→ support actif

Dernière version mineure
→ corrections fonctionnelles et de sécurité

Version mineure précédente
→ correctifs de sécurité pendant 6 mois

Ancienne version majeure
→ migration planifiée ou support spécifique
```

Les durées exactes pourront être ajustées avant la première version stable.

---

## 9.3 Projet client

Un projet client peut continuer à fonctionner avec une ancienne version.

Cela ne signifie pas que cette version reste :

- supportée ;
- sécurisée ;
- compatible avec les nouveaux outils ;
- éligible à tous les correctifs.

---

# 10. Matrice de compatibilité

SiteFactory doit maintenir une matrice couvrant :

```text
GeneratorVersion
ConfigurationSchemaVersion
CatalogVersion
TemplateVersion
ThemeVersion
PlatformVersion
ModuleVersions
DotNetVersion
PostgreSqlMajorVersion
DeploymentProfileVersion
```

Exemple conceptuel :

| Générateur | Config schema | Catalogue | .NET | PostgreSQL |
|---|---:|---|---|---|
| `0.4.x` | `1` | `1.x` | `10` | version majeure supportée |
| `0.5.x` | `1` | `1.x` | `10` | version majeure supportée |

Une incompatibilité doit être détectée avant toute écriture.

---

# 11. Types de mises à niveau

## 11.1 Mise à niveau du générateur

Peut affecter :

- pipeline ;
- structure générée ;
- stratégies de fichiers ;
- CLI ;
- rapports ;
- lock file.

---

## 11.2 Mise à niveau du catalogue

Peut affecter :

- packs ;
- modules ;
- dépendances ;
- statuts ;
- prix ;
- services ;
- compatibilités.

Elle ne doit pas modifier silencieusement le périmètre commercial d’un projet existant.

---

## 11.3 Mise à niveau d’un module

Peut affecter :

- package ;
- configuration ;
- permissions ;
- données ;
- routes ;
- UI ;
- migrations ;
- opérations.

---

## 11.4 Mise à niveau du template

Peut affecter :

- pages ;
- sections ;
- navigation ;
- ordre ;
- recommandations éditoriales ;
- SEO.

---

## 11.5 Mise à niveau du thème

Peut affecter :

- tokens ;
- styles ;
- composants ;
- contrastes ;
- assets ;
- responsive.

---

## 11.6 Mise à niveau de la plateforme

Peut affecter plusieurs modules :

- contrats ;
- Identity ;
- erreurs ;
- observabilité ;
- design system ;
- données ;
- sécurité.

---

## 11.7 Mise à niveau de l’infrastructure

Peut affecter :

- .NET ;
- PostgreSQL ;
- Docker ;
- Caddy ;
- système Linux ;
- GitHub Actions ;
- scripts.

---

# 12. Commande `upgrade check`

## 12.1 Objectif

Détecter les mises à jour applicables sans modifier le projet.

Syntaxe conceptuelle :

```bash
sitefactory upgrade check \
  --project ../client-project
```

---

## 12.2 Entrées

La commande analyse :

```text
client.sitefactory.yml
sitefactory.lock.json
Directory.Packages.props
migrations
repository Git
catalogues disponibles
versions publiées
```

---

## 12.3 Résultat

Le rapport doit distinguer :

```text
Current
Available
Recommended
Security
Deprecated
Breaking
Unsupported
```

Exemple :

```text
Generator
0.4.0 → 0.4.2
Type: patch
Recommendation: apply

lead-management
1.2.0 → 1.3.0
Type: minor
Migration: no
Recommendation: review

template sample-template
1.0.0 → 2.0.0
Type: major
Breaking changes: yes
Recommendation: plan separately
```

---

## 12.4 Effets de bord

Aucun fichier client ne doit être modifié.

---

# 13. Commande `upgrade plan`

## 13.1 Objectif

Produire le plan détaillé d’une mise à niveau.

Syntaxe conceptuelle :

```bash
sitefactory upgrade plan \
  --project ../client-project \
  --target-generator 0.4.2
```

---

## 13.2 Contenu du plan

```text
UpgradeId
Version source
Version cible
Packages
Templates
Thèmes
Modules
Fichiers affectés
Stratégies de fichiers
Conflits
Configuration
Permissions
Données
Migrations
Actions manuelles
Tests
Backup
Déploiement
Rollback
Risques
```

---

## 13.3 Classification des changements

Chaque changement est classé :

```text
automatic-safe
automatic-review-required
manual
conflict
unsupported
destructive
```

---

## 13.4 Plan non exécutable

Le plan est non exécutable si :

- conflit bloquant ;
- version incompatible ;
- repository incohérent ;
- schéma inconnu ;
- migration destructive non validée ;
- version cible indisponible ;
- personnalisation touchée sans stratégie.

---

# 14. Commande `upgrade apply`

## 14.1 Objectif

Appliquer un plan validé.

Syntaxe conceptuelle :

```bash
sitefactory upgrade apply \
  --project ../client-project \
  --execute \
  --confirm-upgrade
```

---

## 14.2 Préconditions

```text
Repository Git présent

Working tree propre

Branche dédiée

Plan valide

Version cible verrouillée

Backup préparé si nécessaire

Aucun conflit non résolu

Espace disque suffisant

Tests disponibles
```

---

## 14.3 Étapes

```text
1. Revalider le projet
2. Revalider le plan
3. Vérifier Git
4. Créer ou vérifier la branche
5. Appliquer les packages
6. Appliquer les fichiers gérés
7. Préserver les fichiers custom
8. Détecter les conflits
9. Préparer les migrations
10. Mettre à jour le lock file en attente
11. Restore
12. Build Release
13. Tests
14. Contrôles de sécurité
15. Contrôles du projet généré
16. Finaliser le lock file
17. Produire le rapport
```

---

## 14.4 Interdictions

`upgrade apply` ne doit pas :

- déployer en production ;
- appliquer une migration production ;
- supprimer une donnée ;
- supprimer un volume ;
- résoudre un conflit par écrasement ;
- modifier un secret ;
- fusionner automatiquement dans `main`.

---

# 15. Branche de mise à niveau

## 15.1 Convention

Format recommandé :

```text
upgrade/{component}-{target-version}
```

Exemples :

```text
upgrade/sitefactory-0.5.0
upgrade/lead-management-1.3.0
upgrade/dotnet-10
upgrade/postgresql-major
```

---

## 15.2 Une branche par objectif

Éviter de mélanger :

- upgrade SiteFactory ;
- refonte métier ;
- nouvelle fonctionnalité ;
- migration d’infrastructure ;
- correction éditoriale importante.

---

## 15.3 Pull request

La branche produit une pull request décrivant :

- versions ;
- changements ;
- migrations ;
- conflits ;
- tests ;
- risques ;
- rollback ;
- impact client.

---

# 16. État Git requis

Avant application :

```text
Working tree propre

Aucun fichier non suivi critique

Aucun conflit

Branche connue

Commit de départ identifié
```

Un projet modifié directement sur le serveur doit être réconcilié avant toute mise à niveau.

---

# 17. Stratégies de fichiers pendant un upgrade

## 17.1 `generated`

Peut être mis à jour selon le plan.

Une modification locale inattendue produit un conflit ou nécessite `--force` selon le contrat.

---

## 17.2 `create-once`

N’est pas remplacé s’il existe.

Une nouvelle version peut fournir un fichier exemple séparé.

---

## 17.3 `merge`

Utilise une stratégie structurée et déterministe.

Exemples :

- package references ;
- composition générée ;
- navigation ;
- configuration.

---

## 17.4 `custom`

Reste inchangé.

---

## 17.5 `protected`

Toute tentative de modification automatique est refusée.

---

# 18. Préservation des personnalisations

## 18.1 Code client

Les personnalisations restent dans :

```text
ClientName.Customizations
```

---

## 18.2 Contrats publics

Une personnalisation doit consommer les contrats publics des modules.

Elle ne doit pas dépendre de types internes susceptibles de changer.

---

## 18.3 Upgrade incompatible

Si un contrat utilisé par le client devient incompatible :

- le plan doit le signaler ;
- la personnalisation doit être adaptée manuellement ;
- les tests clients doivent être mis à jour ;
- la migration ne doit pas être considérée comme automatique.

---

## 18.4 Détection

Le build constitue une première détection.

Des tests d’architecture et d’API peuvent compléter cette protection.

---

# 19. Mise à jour des packages NuGet

## 19.1 Versions explicites

La mise à jour modifie :

```text
Directory.Packages.props
```

ou la source centralisée applicable.

---

## 19.2 Dépendances transitives

Le plan doit signaler les changements transitifs significatifs.

---

## 19.3 Package majeur

Une mise à jour majeure nécessite :

- changelog ;
- analyse ;
- tests ;
- migration éventuelle ;
- validation humaine.

---

## 19.4 Package non SiteFactory

Une dépendance externe n’est pas mise à jour uniquement parce qu’une version plus récente existe.

Il faut évaluer :

- sécurité ;
- compatibilité ;
- bénéfice ;
- licence ;
- maintenance ;
- impact transitif.

---

# 20. Mise à jour du catalogue

## 20.1 Projet existant

Un nouveau catalogue ne doit pas :

- ajouter automatiquement un module vendu ;
- modifier le pack d’un client ;
- changer rétroactivement le prix signé ;
- activer une feature ;
- remplacer un service mensuel.

---

## 20.2 Résolution

Le catalogue sert à déterminer les versions et compatibilités disponibles.

Le périmètre du client reste défini par sa configuration validée.

---

## 20.3 Prix

Une évolution de prix concerne les nouvelles propositions commerciales selon les règles LogicGate.

Elle ne modifie pas automatiquement un projet déjà contractualisé.

---

# 21. Mise à niveau des templates

## 21.1 Types de changements

```text
Nouvelle section
Nouvel ordre recommandé
Nouvelle page
Modification navigation
Amélioration SEO
Correction accessibilité
Correction responsive
```

---

## 21.2 Contenu client

Un upgrade de template ne doit pas écraser les contenus validés.

Il peut :

- proposer une nouvelle structure ;
- créer un fichier exemple ;
- produire une action manuelle ;
- signaler une section devenue obsolète.

---

## 21.3 Page obligatoire nouvelle

L’ajout d’une page obligatoire dans une version compatible doit être évité.

Une telle modification peut nécessiter une version majeure du template.

---

# 22. Mise à niveau des thèmes

## 22.1 Tokens

Un token renommé doit fournir :

- mapping ;
- migration ;
- warning ;
- période de dépréciation.

---

## 22.2 Overrides client

Les overrides sont validés contre la version cible.

Un token inconnu ou supprimé doit être signalé.

---

## 22.3 Contrastes et accessibilité

Une mise à niveau visuelle doit réexécuter :

- contrastes ;
- focus ;
- responsive ;
- snapshots éventuels ;
- tests de composants.

---

# 23. Mise à niveau des modules

## 23.1 Analyse minimale

Pour chaque module :

```text
Version source
Version cible
Dépendances
Permissions
Configuration
Routes
Données
Migrations
Events
Feature flags
Opérations
Breaking changes
```

---

## 23.2 Nouvelle permission

Une nouvelle permission ne doit pas être accordée automatiquement à tous les rôles, sauf règle explicitement définie.

La stratégie de seed recommandée reste :

```text
additive + rapport des écarts
```

---

## 23.3 Nouvelle feature

Une feature ajoutée par une version mineure doit avoir une valeur par défaut sûre.

Elle ne doit pas activer automatiquement une capacité commerciale non prévue.

---

## 23.4 Désactivation

Une mise à niveau ne doit pas supprimer automatiquement les données d’une feature retirée.

---

# 24. Migrations de configuration

## 24.1 Détection

Une version de générateur peut nécessiter une nouvelle version de :

```text
client.sitefactory.yml
```

---

## 24.2 Processus

```text
config migrate plan
→ nouveau fichier
→ comparaison
→ validation humaine
→ remplacement contrôlé
```

---

## 24.3 Original

Le fichier d’origine est conservé jusqu’à validation.

---

## 24.4 Perte ou ambiguïté

Une migration nécessitant une décision métier doit produire :

```text
manual action required
```

et non inventer une valeur.

---

# 25. Migrations de données

## 25.1 Responsabilité

Les migrations appartiennent au repository client.

---

## 25.2 Création

La migration est créée après composition de la version cible.

---

## 25.3 Inspection

Elle doit vérifier :

- tables ;
- colonnes ;
- nullabilité ;
- index ;
- clés étrangères ;
- suppressions ;
- transformations ;
- données existantes.

---

## 25.4 Production

Avant application :

```text
backup
→ vérification
→ mode maintenance si nécessaire
→ migration
→ contrôle
→ déploiement
```

---

## 25.5 Breaking migration

Une migration incompatible doit utiliser une stratégie telle que :

```text
expand
→ migrate
→ switch
→ contract
```

lorsque cela est raisonnable.

---

# 26. Mise à niveau PostgreSQL

## 26.1 Patch et minor

Les mises à jour compatibles sont appliquées selon les recommandations du fournisseur et les tests du projet.

---

## 26.2 Version majeure

Une nouvelle version majeure nécessite :

- projet dédié ;
- backup complet ;
- restore drill ;
- vérification des extensions ;
- compatibilité du provider EF Core ;
- staging ;
- tests ;
- rollback ;
- fenêtre de maintenance.

---

## 26.3 Interdiction

L’image PostgreSQL ne doit pas être modifiée vers une nouvelle version majeure avec le même volume sans procédure validée.

---

# 27. Mise à niveau .NET

## 27.1 Changement de version

Une nouvelle version majeure de .NET peut affecter :

- runtime ;
- SDK ;
- analyzers ;
- packages ;
- Blazor ;
- Dockerfile ;
- GitHub Actions ;
- générateur ;
- projets clients.

---

## 27.2 Processus

```text
branche dédiée
→ global.json
→ target framework
→ packages
→ build
→ tests
→ projet généré
→ Docker
→ staging
→ déploiement
```

---

## 27.3 Projet client

Les projets clients ne migrent pas automatiquement uniquement parce que SiteFactory utilise une nouvelle version .NET.

---

# 28. Mise à niveau Docker et Caddy

## 28.1 Docker

Les mises à jour Docker et Compose sont appliquées :

- après lecture des changements ;
- avec vérification des stacks ;
- avec capacité de reprise ;
- hors opération applicative importante simultanée.

---

## 28.2 Caddy

Une mise à jour Caddy nécessite :

- sauvegarde de la configuration ;
- validation ;
- vérification des certificats ;
- vérification des backends ;
- test multi-client si mutualisé.

---

## 28.3 Images de base

Les images de base sont mises à jour puis :

- reconstruites ;
- scannées ;
- testées ;
- publiées avec une nouvelle version.

---

# 29. Mises à jour GitHub Actions

Les actions doivent être maintenues afin d’éviter :

- runtime Node déprécié ;
- action non maintenue ;
- faille supply chain ;
- incompatibilité runner.

Une mise à jour d’action doit être testée par pull request.

---

# 30. Maintenance de sécurité

## 30.1 Sources de détection

- GitHub advisories ;
- Dependabot ;
- scans NuGet ;
- Semgrep ;
- CodeQL lorsqu’il est réellement configuré ;
- alertes d’images ;
- fournisseurs ;
- veille LogicGate.

---

## 30.2 Triage

Pour chaque vulnérabilité :

```text
Composant
Version affectée
Projets concernés
Exploitabilité
Exposition
Sévérité
Mitigation
Correctif
Délai
```

---

## 30.3 Critique

Une vulnérabilité critique exploitable peut nécessiter :

- kill switch ;
- désactivation de route ;
- restriction d’accès ;
- rotation de secret ;
- hotfix ;
- déploiement prioritaire.

---

## 30.4 Haute

Une vulnérabilité haute doit être corrigée avant une nouvelle release ou faire l’objet d’une mitigation formelle.

---

# 31. Hotfix

## 31.1 Branche

Format recommandé :

```text
security/{issue}
hotfix/{issue}
```

---

## 31.2 Processus accéléré

```text
reproduction
→ test
→ correction minimale
→ build
→ tests ciblés
→ suite essentielle
→ scans
→ version patch
→ staging rapide si possible
→ déploiement
→ surveillance
```

---

## 31.3 Contrôles non supprimables

Même en urgence :

- build ;
- tests ciblés ;
- secret scan ;
- analyse de dépendance concernée ;
- plan de rollback ;
- version explicite.

---

## 31.4 Post-hotfix

Après stabilisation :

- suite complète ;
- documentation ;
- mise à jour du runbook ;
- post-mortem si nécessaire ;
- propagation vers les branches concernées.

---

# 32. Fenêtres de maintenance

## 32.1 Standard

Les mises à jour non urgentes doivent être planifiées dans une période connue.

---

## 32.2 Choix

La fenêtre dépend :

- activité du client ;
- risque ;
- durée ;
- interruption possible ;
- disponibilité de l’opérateur ;
- disponibilité du client pour recette.

---

## 32.3 Communication

Une intervention pouvant provoquer une interruption doit être communiquée selon le contrat.

---

## 32.4 Urgence

Un correctif de sécurité critique peut être appliqué hors fenêtre standard.

---

# 33. Cadence de maintenance proposée

## 33.1 Hebdomadaire

- vérifier alertes de sécurité ;
- vérifier CI ;
- vérifier backups ;
- vérifier espace disque ;
- vérifier erreurs majeures.

---

## 33.2 Mensuelle

- revue des dépendances ;
- revue des images ;
- revue des comptes ;
- revue des certificats ;
- revue des logs critiques ;
- correctifs mineurs compatibles ;
- état des versions clientes.

---

## 33.3 Trimestrielle

- restore drill ;
- revue des versions supportées ;
- revue des modules dépréciés ;
- revue des permissions ;
- revue de la documentation ;
- revue des runbooks.

---

## 33.4 Annuelle

- revue de l’infrastructure ;
- revue des objectifs RPO/RTO ;
- revue du plan de reprise ;
- revue des fournisseurs ;
- revue des responsabilités client ;
- planification des upgrades majeurs.

---

# 34. Périmètre contractuel de maintenance

Le public ne distribue aucune offre commerciale de maintenance.
Les noms, prix et catalogues propriétaires restent dans le périmètre Business.
Les trois profils techniques suivants sont illustratifs, non des produits
ou engagements disponibles. Pour toute prestation future, le contrat
et la proposition approuvée doivent préciser le périmètre exact.

---

# 35. Profil technique standard

Orientation technique possible :

```text
Hébergement
HTTPS
Exploitation standard
Backups selon contrat
Surveillance de disponibilité
Mises à jour correctives courantes
Mises à jour de sécurité compatibles
Support opérationnel standard
```

Ne comprend pas automatiquement :

- nouvelle fonctionnalité ;
- refonte ;
- module supplémentaire ;
- migration majeure ;
- intégration externe ;
- réécriture de contenu ;
- changement de périmètre.

---

# 36. Profil technique étendu

Peut inclure le profil standard ainsi que, selon le contrat :

```text
Maintenance fonctionnelle renforcée
Revue périodique des dépendances
Interventions de contenu limitées
Suivi des leads et modules métier
Staging pour évolutions significatives
Rapports de maintenance
Priorité de traitement supérieure
```

---

# 37. Profil technique renforcé

Peut inclure le profil étendu ainsi que, selon le contrat :

```text
Surveillance renforcée
Revue de sécurité périodique
Correctifs prioritaires
Restore drills renforcés
Analyse d’événements sensibles
Maintenance des automatisations
Runbooks renforcés
Suivi des vulnérabilités
```

---

## 37.1 Limite

Un profil renforcé ne signifie pas :

- sécurité absolue ;
- conformité certifiée ;
- SOC permanent ;
- disponibilité garantie ;
- pentest continu.

---

# 38. Maintenance incluse versus évolution facturable

## 38.1 Généralement corrective

Peut relever de la maintenance :

- correction d’un bug ;
- correction de sécurité ;
- adaptation mineure compatible ;
- mise à jour technique prévue ;
- correction d’un script ;
- correction d’un déploiement.

---

## 38.2 Généralement évolutive

Peut nécessiter un devis :

- nouvelle page structurante ;
- nouveau module ;
- nouvelle intégration ;
- nouvelle automatisation ;
- nouveau rôle complexe ;
- import de données ;
- migration majeure ;
- refonte graphique ;
- nouveau secteur ;
- espace client ;
- multilingue ;
- changement d’hébergeur important.

---

## 38.3 Cas ambigu

Le diagnostic doit préciser :

```text
Défaut du périmètre initial
ou
nouvelle demande
ou
évolution nécessaire d’un fournisseur
```

---

# 39. Responsabilités LogicGate

Lorsque LogicGate assure la maintenance, LogicGate est notamment responsable de :

- surveiller les versions ;
- qualifier les alertes ;
- préparer les plans ;
- maintenir les packages SiteFactory ;
- tester les correctifs ;
- appliquer les upgrades convenus ;
- maintenir les runbooks ;
- vérifier les backups ;
- documenter les interventions ;
- signaler les versions obsolètes ;
- préparer les migrations ;
- conserver la traçabilité.

---

# 40. Responsabilités du client

Le client est notamment responsable de :

- gérer ses utilisateurs ;
- signaler les départs ;
- valider les contenus ;
- valider les évolutions ;
- signaler les incidents ;
- protéger ses comptes ;
- fournir les informations requises ;
- accepter ou refuser une mise à niveau non obligatoire ;
- valider les interruptions planifiées ;
- maintenir ses domaines et contrats fournisseurs selon l’accord.

---

# 41. Maintenance sans service mensuel

Si aucun service mensuel n’est souscrit :

- le projet reste fonctionnel dans son état livré ;
- LogicGate n’applique pas automatiquement les mises à jour ;
- les responsabilités d’exploitation doivent être transférées ;
- les correctifs futurs peuvent faire l’objet d’un devis ;
- les alertes de sécurité peuvent nécessiter une intervention ponctuelle ;
- les backups et mises à jour doivent avoir un responsable identifié.

---

## 41.1 Sécurité fondamentale

La fin du service ne retire pas les protections déjà présentes.

Elle modifie la responsabilité de leur maintien.

---

# 42. Inventaire des projets clients

LogicGate doit pouvoir maintenir un inventaire sans secret :

```text
ClientSlug
Repository
ProductionDomain
CurrentApplicationVersion
GeneratorVersion
CatalogVersion
TemplateVersion
ThemeVersion
ModuleVersions
DotNetVersion
PostgreSqlVersion
CarePlan
SupportStatus
LastUpgradeAtUtc
LastSecurityReviewAtUtc
LastRestoreDrillAtUtc
```

---

## 42.1 Confidentialité

L’inventaire ne contient pas :

- mot de passe ;
- token ;
- connection string ;
- contenu métier ;
- données de leads.

---

# 43. État de maintenance d’un projet

Statuts possibles :

```text
active
upgrade-available
security-fix-required
deprecated
unsupported
maintenance-paused
transferred
closed
```

---

## 43.1 `active`

Projet conforme à la baseline supportée.

---

## 43.2 `upgrade-available`

Une mise à niveau compatible est disponible mais non urgente.

---

## 43.3 `security-fix-required`

Une correction de sécurité doit être planifiée prioritairement.

---

## 43.4 `deprecated`

Le projet fonctionne sur une version approchant de la fin de support.

---

## 43.5 `unsupported`

La version n’est plus couverte par la maintenance standard.

---

# 44. Rapport de maintenance

Un rapport périodique peut contenir :

```text
Version déployée
Disponibilité observée
Backups
Restore drill
Incidents
Vulnérabilités
Mises à jour disponibles
Mises à jour appliquées
Espace disque
Certificats
Recommandations
Actions client
```

Le rapport ne contient aucun secret ou donnée personnelle.

---

# 45. Rapport d’upgrade

Fichier possible :

```text
upgrade-report.json
```

Structure conceptuelle :

```json
{
  "schemaVersion": 1,
  "upgradeId": "d649eb7d-8eb3-49d1-a898-ae052d8c90f4",
  "clientSlug": "sample-project",
  "status": "success",
  "source": {
    "generatorVersion": "0.4.0",
    "applicationVersion": "1.2.0"
  },
  "target": {
    "generatorVersion": "0.4.2",
    "applicationVersion": "1.2.1"
  },
  "packagesUpdated": 4,
  "filesCreated": 2,
  "filesUpdated": 7,
  "filesPreserved": 12,
  "conflicts": 0,
  "migration": {
    "required": false
  },
  "build": {
    "warnings": 0,
    "errors": 0
  },
  "tests": {
    "passed": 214,
    "failed": 0,
    "skipped": 0
  }
}
```

---

# 46. Changelog

Chaque composant SiteFactory doit maintenir un changelog adapté.

Les entrées doivent distinguer :

```text
Added
Changed
Fixed
Security
Deprecated
Removed
Migration
```

---

## 46.1 Projet client

Le projet client possède également son propre changelog.

Il enregistre :

- évolutions client ;
- upgrades SiteFactory ;
- migrations ;
- déploiements significatifs ;
- correctifs de sécurité.

---

# 47. Notes de migration

Une version nécessitant une action manuelle doit fournir une note décrivant :

```text
Préconditions
Changements
Fichiers affectés
Configuration
Données
Migration
Backup
Tests
Rollback
```

---

# 48. Dépréciation

## 48.1 Processus

Une dépréciation doit préciser :

- élément concerné ;
- version d’introduction ;
- remplacement ;
- warning ;
- date ou version de retrait ;
- guide de migration.

---

## 48.2 Pas de suppression silencieuse

Un identifiant public ne doit pas disparaître sans :

- version majeure ;
- documentation ;
- migration ;
- détection.

---

## 48.3 Période de transition

La durée dépend de l’usage et de la criticité.

Avant `1.0`, elle peut être plus courte mais doit rester explicite.

---

# 49. Module déprécié

Un module déprécié peut :

- rester supporté pour les projets existants ;
- ne plus être proposé aux nouveaux projets ;
- recevoir uniquement des correctifs de sécurité ;
- proposer un module de remplacement.

---

## 49.1 Retrait du feed

Une version ne doit pas être retirée du feed tant qu’un projet client supporté en dépend, sauf incident majeur nécessitant une procédure exceptionnelle.

---

# 50. Fin de support

## 50.1 Notification

LogicGate doit signaler :

- version concernée ;
- date ;
- risques ;
- upgrade recommandé ;
- effort estimé ;
- conséquences d’un refus.

---

## 50.2 Refus d’upgrade

Un client peut refuser une évolution non obligatoire.

La décision doit être documentée.

Pour une vulnérabilité critique ou une incompatibilité fournisseur, LogicGate peut exiger une action pour maintenir le service.

---

## 50.3 Projet unsupported

Un projet unsupported peut nécessiter :

- audit préalable ;
- upgrade intermédiaire ;
- devis spécifique ;
- migration manuelle ;
- staging dédiée.

---

# 51. Upgrade par étapes

Un projet ancien ne doit pas nécessairement passer directement à la dernière version.

Exemple :

```text
0.3
→ 0.4
→ 0.5
→ 1.0
```

Le plan doit indiquer les étapes intermédiaires obligatoires.

---

# 52. Upgrade majeur

## 52.1 Cadrage

Une mise à niveau majeure doit être traitée comme un mini-projet.

Elle nécessite :

- analyse ;
- estimation ;
- branche ;
- tests ;
- staging ;
- recette ;
- communication ;
- backup ;
- rollback.

---

## 52.2 Contenus

Elle peut inclure :

- changement de framework ;
- migration de base ;
- refonte de template ;
- changement d’Identity ;
- changement de stockage ;
- dépréciation de modules.

---

# 53. Mise à niveau des projets sans maintenance active

Processus :

```text
audit de l’état
→ inventaire des versions
→ détection des écarts
→ analyse des personnalisations
→ plan de remise à niveau
→ devis
→ staging
→ upgrade
```

Le projet ne doit pas être supposé compatible uniquement parce qu’il compile encore.

---

# 54. Modification manuelle d’un projet client

## 54.1 Autorisée dans les espaces prévus

Les personnalisations doivent rester dans :

```text
Customizations
configuration client
contenus
migrations client
```

---

## 54.2 Modification d’un fichier généré

Une modification directe peut créer un conflit lors d’un upgrade.

Le projet doit :

- déplacer la personnalisation ;
- ou accepter que le fichier devienne protégé ;
- ou documenter une stratégie de merge.

---

## 54.3 Audit avant upgrade

Le plan compare les hashes des fichiers gérés.

---

# 55. Quality gates d’un upgrade

Un upgrade ne peut pas être déclaré réussi si :

```text
Plan invalide

Conflit non résolu

Fichier custom écrasé

Restore échoué

Build Release échoué

Test échoué

Migration non inspectée

Secret détecté

Vulnérabilité bloquante

Lock file incohérent

Rapport absent
```

---

# 56. Tests obligatoires

## 56.1 Tests du check

- aucune mise à jour ;
- patch disponible ;
- minor disponible ;
- major disponible ;
- security fix ;
- version inconnue ;
- version unsupported.

---

## 56.2 Tests du plan

- fichiers ;
- packages ;
- conflits ;
- migration ;
- actions manuelles ;
- rollback ;
- déterminisme ;
- absence d’écriture.

---

## 56.3 Tests de l’application

- restore ;
- build Release ;
- tests unitaires ;
- tests d’intégration ;
- tests client ;
- sécurité ;
- SEO ;
- Docker.

---

## 56.4 Tests de non-écrasement

- custom conservé ;
- protected conservé ;
- generated contrôlé ;
- merge déterministe.

---

## 56.5 Tests de migration

- base vide ;
- base existante ;
- données représentatives ;
- version source supportée ;
- backup ;
- restauration éventuelle.

---

# 57. Tests de compatibilité

La CI SiteFactory doit maintenir des projets de référence pour les versions encore supportées lorsque cela reste raisonnable.

Elle peut vérifier :

```text
ancienne configuration supportée
→ nouveau générateur
→ plan valide
```

Les matrices trop larges doivent être limitées par une politique de support explicite.

---

# 58. Tests de rollback

Tester :

- retour package ;
- retour image ;
- compatibilité base ;
- restauration ;
- lock file ;
- customizations ;
- smoke tests.

Un rollback non testé ne doit pas être présenté comme une garantie.

---

# 59. Staging

Une mise à niveau doit passer par staging lorsqu’elle affecte :

- données ;
- Identity ;
- routes ;
- sécurité ;
- Docker ;
- Caddy ;
- modules métier ;
- intégrations ;
- template majeur.

---

## 59.1 Patch simple

Un patch purement interne et fortement testé peut suivre une procédure allégée selon le risque.

---

# 60. Recette

## 60.1 Recette LogicGate

Vérifie :

- fonctionnalités affectées ;
- régressions ;
- administration ;
- formulaires ;
- données ;
- sécurité ;
- SEO ;
- responsive ;
- observabilité.

---

## 60.2 Recette client

Requise lorsque l’évolution affecte :

- contenus ;
- parcours ;
- identité visuelle ;
- rôles ;
- fonctionnement métier ;
- intégration externe.

---

# 61. Déploiement

Le déploiement suit :

```text
22-deployment-and-environments.md
```

Il doit identifier :

- version source ;
- version cible ;
- image ;
- migration ;
- backup ;
- smoke tests ;
- rollback.

---

# 62. Surveillance post-upgrade

Après déploiement, surveiller :

- liveness ;
- readiness ;
- erreurs 5xx ;
- PostgreSQL ;
- formulaires ;
- SMTP ;
- redémarrages ;
- logs ;
- performances ;
- fonctionnalités mises à jour.

---

## 62.1 Période de stabilisation

Pendant la stabilisation :

- conserver l’ancienne image ;
- ne pas purger le backup pré-upgrade ;
- éviter une nouvelle évolution non urgente ;
- documenter les anomalies.

---

# 63. Rollback

## 63.1 Code compatible

Si la base reste compatible :

```text
retour à l’image précédente
```

---

## 63.2 Données incompatibles

Si les données ont été modifiées de manière incompatible :

```text
restauration du backup
+ version applicative correspondante
```

---

## 63.3 Configuration

La configuration précédente doit rester récupérable.

---

## 63.4 Lock file

Un rollback doit rétablir un lock file cohérent avec la version active.

---

# 64. Échec partiel

En cas d’échec :

- ne pas déclarer le nouveau lock file comme actif ;
- conserver les conflits ;
- conserver le rapport ;
- identifier les fichiers modifiés ;
- rollback Git ;
- restaurer si nécessaire.

---

# 65. Maintenance des backups

La stratégie de maintenance inclut :

- vérification quotidienne ;
- copie hors VPS ;
- rétention ;
- restore drill ;
- revue des tailles ;
- revue des alertes ;
- mise à jour des scripts.

---

# 66. Maintenance des secrets

Inclut :

- inventaire ;
- rotation ;
- retrait des anciens accès ;
- vérification des comptes ;
- séparation staging/production ;
- vérification des secrets inutilisés.

---

# 67. Maintenance des comptes

Inclut :

- utilisateurs actifs ;
- derniers administrateurs ;
- comptes inactifs ;
- rôles ;
- permissions ;
- départs ;
- MFA future selon besoin.

---

# 68. Maintenance des certificats et domaines

Inclut :

- expiration des domaines ;
- renouvellement ;
- DNS ;
- certificats ;
- Caddy ;
- sous-domaines ;
- staging ;
- anciens domaines et redirections.

---

# 69. Maintenance de l’espace disque

Inclut :

- volumes PostgreSQL ;
- App_Data ;
- backups ;
- logs ;
- images Docker ;
- caches ;
- alertes ;
- croissance.

---

# 70. Maintenance des logs et observabilité

Inclut :

- niveaux ;
- rotation ;
- health checks ;
- événements ;
- alertes ;
- diagnostics ;
- absence de PII ;
- absence de secrets ;
- exporter externe éventuel.

---

# 71. Maintenance des modules IA

Si un module IA est installé :

- provider ;
- modèle ;
- coût ;
- quota ;
- prompts ;
- données ;
- sécurité ;
- kill switch ;
- fallbacks ;
- tests.

Une modification de modèle peut changer le résultat sans changement de code.

Elle doit être versionnée ou documentée.

---

# 72. Automatisation de maintenance

## 72.1 Autorisée

SiteFactory peut automatiser :

- détection ;
- rapports ;
- plans ;
- tests ;
- scans ;
- génération de branches ;
- vérification des versions.

---

## 72.2 Non autorisée dans le MVP

SiteFactory ne doit pas automatiser sans validation :

- merge ;
- migration production ;
- déploiement production ;
- suppression ;
- changement d’entitlement ;
- remplacement de contenu ;
- acceptation d’un conflit.

---

# 73. Notifications de mises à jour

Le système peut signaler :

```text
Patch disponible

Security fix disponible

Version dépréciée

Version bientôt unsupported

Migration majeure requise
```

Les notifications doivent éviter le bruit excessif.

---

# 74. Inventaire des écarts

Un rapport peut comparer :

```text
Projet client
↔ baseline SiteFactory supportée
```

Écarts possibles :

- package ancien ;
- module déprécié ;
- schéma ancien ;
- runtime ancien ;
- PostgreSQL ancien ;
- action GitHub dépréciée ;
- backup non conforme ;
- restore drill en retard ;
- sécurité insuffisante.

---

# 75. Maintenance préventive planifiée

Une tâche de maintenance doit contenir :

```text
Objectif
Projet
Composants
Versions
Risques
Backup
Tests
Fenêtre
Rollback
Responsable
```

---

# 76. Fin d’un service mensuel

## 76.1 Préparation

Avant fin du service :

- inventaire ;
- version ;
- dernier backup ;
- secrets ;
- domaines ;
- responsabilité ;
- documentation ;
- accès ;
- risques connus.

---

## 76.2 Transfert

Le client reçoit selon le contrat :

- repository ;
- artefacts ;
- procédures ;
- sauvegardes ;
- inventaire ;
- responsabilités ;
- instructions de maintenance ;
- état des versions.

---

## 76.3 Retrait LogicGate

LogicGate retire :

- accès SSH ;
- accès registry ;
- accès DNS ;
- accès backup ;
- accès administration ;
- secrets partagés.

---

# 77. Fermeture d’un projet

Un projet fermé doit posséder :

```text
Version finale
Backup final
Données transférées
Secrets révoqués
DNS traité
Infrastructure arrêtée
Volumes supprimés après validation
Repository archivé
Contrat et responsabilités clôturés
```

---

# 78. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-UPGRADE-001` | Projet non reconnu |
| `SF-UPGRADE-002` | Lock file invalide |
| `SF-UPGRADE-003` | Version source non supportée |
| `SF-UPGRADE-004` | Version cible inconnue |
| `SF-UPGRADE-005` | Compatibilité invalide |
| `SF-UPGRADE-006` | Repository Git non propre |
| `SF-UPGRADE-007` | Branche invalide |
| `SF-UPGRADE-008` | Plan non exécutable |
| `SF-UPGRADE-009` | Conflit de fichier |
| `SF-UPGRADE-010` | Personnalisation affectée |
| `SF-UPGRADE-011` | Migration requise |
| `SF-UPGRADE-012` | Migration destructive |
| `SF-UPGRADE-013` | Backup requis |
| `SF-UPGRADE-014` | Build échoué |
| `SF-UPGRADE-015` | Tests échoués |
| `SF-UPGRADE-016` | Contrôle de sécurité échoué |
| `SF-UPGRADE-017` | Lock file non finalisé |
| `SF-UPGRADE-018` | Rollback impossible |
| `SF-UPGRADE-019` | Upgrade intermédiaire requis |
| `SF-UPGRADE-020` | Composant déprécié |
| `SF-UPGRADE-021` | Composant unsupported |
| `SF-MAINT-001` | Maintenance non active |
| `SF-MAINT-002` | Fenêtre de maintenance requise |
| `SF-MAINT-003` | Responsabilité non définie |

---

# 79. Observabilité

Événements possibles :

```text
UpgradeCheckStarted
UpgradeCheckCompleted
UpgradePlanCreated
UpgradeStarted
PackageUpdated
FileConflictDetected
MigrationPrepared
UpgradeBuildCompleted
UpgradeTestsCompleted
UpgradeCompleted
UpgradeFailed
ProjectDeprecated
SecurityFixRequired
```

---

## 79.1 Propriétés autorisées

```text
UpgradeId
ClientSlug
ComponentId
VersionFrom
VersionTo
ChangeType
Result
Duration
ErrorCode
```

---

## 79.2 Données interdites

```text
Secret
Données client
Contenu des personnalisations
Connection string
Mot de passe
```

---

# 80. Quality gates

Une release de maintenance ou un upgrade est bloqué lorsque :

```text
Version inconnue

Plan non validé

Conflit non résolu

Personnalisation écrasée

Migration non inspectée

Backup requis absent

Build échoué

Test échoué

Secret détecté

Vulnérabilité bloquante

Staging en échec lorsque requise

Rollback non défini

Rapport absent
```

---

# 81. Tests automatisés

## 81.1 Versions

Tester :

- version identique ;
- patch ;
- minor ;
- major ;
- pré-release ;
- version inconnue ;
- version unsupported.

---

## 81.2 Compatibilité

Tester :

- générateur ;
- schéma ;
- catalogue ;
- template ;
- thème ;
- modules ;
- .NET ;
- PostgreSQL.

---

## 81.3 Fichiers

Tester toutes les stratégies :

```text
generated
create-once
merge
custom
protected
```

---

## 81.4 Git

Tester :

- repository propre ;
- repository sale ;
- branche absente ;
- conflit ;
- rollback local ;
- rapport.

---

## 81.5 Migrations

Tester :

- aucune migration ;
- migration compatible ;
- migration destructive ;
- données existantes ;
- backup requis ;
- restauration.

---

# 82. Tests de sécurité

Vérifier :

- correctif critique détecté ;
- ancienne version vulnérable ;
- secret dans un artefact ;
- package non autorisé ;
- source NuGet inconnue ;
- action GitHub dépréciée ;
- image vulnérable ;
- downgrade non sûr.

---

# 83. Tests du projet client

Après upgrade :

```text
restore
build Release
tests
architecture
PostgreSQL
Identity
formulaires
SEO
sécurité
Docker
smoke tests
```

---

# 84. Critères d’acceptation du MVP

La stratégie sera considérée comme opérationnelle lorsque :

1. tous les composants SiteFactory sont versionnés ;
2. les versions utilisent SemVer ;
3. les versions sont verrouillées ;
4. `upgrade check` ne modifie aucun fichier ;
5. `upgrade plan` ne modifie aucun fichier ;
6. `upgrade apply` exige une confirmation ;
7. un plan identifie les versions source et cible ;
8. les packages affectés sont listés ;
9. les fichiers affectés sont listés ;
10. les personnalisations sont protégées ;
11. les conflits sont détectés ;
12. Git propre est requis ;
13. une branche dédiée est utilisée ;
14. les migrations sont identifiées ;
15. les backups requis sont identifiés ;
16. restore, build et tests sont obligatoires ;
17. le lock file est mis à jour uniquement après succès ;
18. un rapport d’upgrade est produit ;
19. les correctifs de sécurité sont prioritaires ;
20. les hotfixes conservent les contrôles essentiels ;
21. les composants dépréciés sont signalés ;
22. les composants unsupported sont refusés ou nécessitent un plan spécifique ;
23. les upgrades majeurs passent par staging ;
24. les versions précédentes restent disponibles pour rollback ;
25. la maintenance préventive est planifiée ;
26. les responsabilités LogicGate et client sont définies ;
27. les services mensuels ne sont pas confondus avec les évolutions facturables ;
28. un inventaire des projets clients existe ;
29. les projets sans maintenance active ont une responsabilité identifiée ;
30. aucune mise à jour production n’est automatique.

---

# 85. Décisions validées

La baseline de maintenance est la suivante :

```text
Chaque composant SiteFactory possède une version explicite.

Les versions utilisent SemVer.

Les projets clients verrouillent leurs versions.

Les versions flottantes sont interdites.

Une version publiée est immuable.

Les mises à niveau utilisent check, plan et apply.

Check et plan ne modifient aucun fichier client.

Apply nécessite un repository propre et une branche dédiée.

Les fichiers custom et protected sont préservés.

Une mise à niveau ne déploie pas automatiquement.

Une mise à niveau n’applique pas les migrations de production.

Les migrations appartiennent au repository client.

Les migrations sensibles nécessitent un backup.

Le lock file n’est finalisé qu’après build et tests réussis.

Les correctifs de sécurité sont prioritaires.

Une vulnérabilité critique exploitable peut déclencher un hotfix.

Les hotfixes conservent build, tests, scans et rollback.

Les mises à niveau majeures sont traitées comme des mini-projets.

Staging est requise pour les changements importants.

Le rollback applicatif et le rollback de données restent distincts.

Les personnalisations doivent consommer des contrats publics.

Le catalogue ne modifie pas rétroactivement
le périmètre commercial d’un client.

Les prix mis à jour ne modifient pas automatiquement
un projet déjà contractualisé.

La maintenance corrective est distincte
de la maintenance évolutive.

Les services mensuels doivent préciser leur périmètre contractuel.

Sans maintenance active, aucune mise à jour
n’est appliquée automatiquement.

LogicGate conserve un inventaire technique
des versions des projets maintenus.

Les composants dépréciés possèdent un remplacement
et une période de transition.

Les versions unsupported nécessitent un audit
ou un plan de remise à niveau spécifique.

Avant 1.0, la dernière version mineure reçoit
le support principal.

Après 1.0, la politique de support cible maintient
la version mineure courante et une transition limitée
pour la précédente.

Codex ne peut pas appliquer seul une mise à niveau,
une migration ou un déploiement de production.
```

---

# 86. Règles pour Codex

## 86.1 Lecture préalable

Avant toute modification liée aux upgrades ou à la maintenance, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../02-architecture/module-catalog.md
../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
../02-architecture/core-module-boundary.md
09-module-architecture.md
10-feature-flags-and-entitlements.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
13-generation-pipeline.md
15-cli-specification.md
18-security-baseline.md
19-testing-strategy.md
20-quality-gates-and-ci.md
21-observability-and-logging.md
22-deployment-and-environments.md
23-backup-and-restore.md
24-operational-runbooks.md
25-upgrade-and-maintenance-strategy.md
AGENTS.md
```

Ainsi que :

- lock file ;
- changelog ;
- manifests ;
- packages ;
- migrations ;
- personnalisations ;
- tests ;
- workflows ;
- runbooks du projet concerné.

---

## 86.2 Ce que Codex peut faire

Codex peut :

- analyser les versions ;
- créer un rapport de check ;
- produire un plan ;
- créer une branche proposée ;
- mettre à jour des packages validés ;
- adapter des fichiers generated ;
- préserver les customizations ;
- préparer une migration ;
- ajouter des tests ;
- corriger un conflit ciblé ;
- mettre à jour un changelog ;
- documenter un breaking change ;
- préparer un rollback.

---

## 86.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- appliquer un upgrade en production ;
- appliquer une migration production ;
- déployer ;
- modifier un entitlement ;
- modifier un prix ;
- écraser un fichier custom ;
- écraser un fichier protected ;
- ignorer un conflit ;
- utiliser une version flottante ;
- remplacer une version publiée ;
- finaliser un lock file après un échec ;
- supprimer une donnée ;
- supprimer un module avec ses données ;
- déclarer une version compatible sans tests ;
- réduire la politique de support ;
- désactiver un quality gate ;
- accepter automatiquement un breaking change ;
- mettre à jour une version majeure PostgreSQL sans plan ;
- considérer un hotfix comme une raison de supprimer les scans.

---

## 86.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- choix de version cible ;
- upgrade majeur ;
- migration destructive ;
- nouveau module ;
- retrait de module ;
- changement d’entitlement ;
- changement de prix ;
- changement de politique de support ;
- dépréciation ;
- fin de support ;
- changement .NET majeur ;
- changement PostgreSQL majeur ;
- changement de profil de déploiement ;
- application d’un hotfix en production ;
- fenêtre de maintenance ;
- communication client ;
- rollback de données.

---

## 86.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Projet concerné
Version source
Version cible
Composants affectés
Packages affectés
Fichiers affectés
Personnalisations affectées
Conflits
Configuration affectée
Permissions affectées
Données affectées
Migration requise
Backup requis
Tests exécutés
Build
Scans
Staging
Rollback
Breaking changes
Risques
Décisions humaines restantes
```

Codex ne doit pas déclarer un upgrade réussi sans avoir vérifié le build, les tests et le lock file final.

---

# 87. Anti-patterns interdits

## 87.1 Mise à jour automatique de production

Une nouvelle version de package déclenche immédiatement un déploiement client.

---

## 87.2 Version flottante

```xml
<PackageReference Include="..." Version="*" />
```

---

## 87.3 Lock file modifié manuellement

Un opérateur change une version dans le JSON sans exécuter le pipeline d’upgrade.

---

## 87.4 Upgrade sur `main`

La mise à niveau est appliquée directement sans branche.

---

## 87.5 Working tree sale

Un upgrade mélange des modifications locales non identifiées.

---

## 87.6 Customization écrasée

Le générateur remplace un composant client pour appliquer un nouveau template.

---

## 87.7 Migration automatique

Une mise à jour de package déclenche `Database.Migrate()` au démarrage.

---

## 87.8 Catalogue rétroactif

Une modification de prix ou pack change automatiquement les entitlements d’un ancien client.

---

## 87.9 Hotfix sans test

Une correction urgente est déployée sans test de reproduction.

---

## 87.10 Package remplacé

Une version NuGet existante est republiée avec un contenu différent.

---

## 87.11 Upgrade majeur déguisé en patch

Un changement incompatible utilise uniquement une version patch.

---

## 87.12 Support indéfini implicite

Aucune version n’est déclarée deprecated ou unsupported.

---

## 87.13 Maintenance évolutive gratuite implicite

Toute nouvelle demande est considérée comme une correction incluse.

---

## 87.14 Fin de maintenance sans transfert

Les responsabilités de backup, sécurité et mises à jour ne sont attribuées à personne.

---

# 88. Roadmap d’implémentation

## Phase upgrade 1 — Détection

```text
lock file reader
version comparison
compatibility matrix
upgrade check
```

---

## Phase upgrade 2 — Planification

```text
target versions
files
packages
conflicts
manual actions
report
```

---

## Phase upgrade 3 — Application locale

```text
Git checks
branch
file strategies
package update
build
tests
lock file
```

---

## Phase upgrade 4 — Modules

```text
manifests
configuration
permissions
data
migrations
```

---

## Phase upgrade 5 — Templates et thèmes

```text
composition
tokens
pages
sections
SEO
non-overwrite
```

---

## Phase upgrade 6 — Sécurité

```text
security fixes
hotfix flow
inventory
client impact
```

---

## Phase upgrade 7 — Maintenance client

```text
project inventory
status
reports
cadence
care plans
```

---

## Phase upgrade 8 — Infrastructure

```text
.NET
PostgreSQL
Docker
Caddy
GitHub Actions
```

---

## Phase upgrade 9 — Automatisation contrôlée

```text
scheduled check
notification
branch creation
PR preparation
```

L’application et le déploiement restent soumis à validation humaine.

---

# 89. Checklist `upgrade check`

```text
[ ] Projet identifié
[ ] Lock file valide
[ ] Configuration valide
[ ] Versions actuelles connues
[ ] Versions disponibles connues
[ ] Security fixes identifiés
[ ] Dépréciations identifiées
[ ] Versions unsupported identifiées
[ ] Aucun fichier modifié
[ ] Rapport produit
```

---

# 90. Checklist `upgrade plan`

```text
[ ] Version source
[ ] Version cible
[ ] Composants
[ ] Packages
[ ] Fichiers
[ ] Customizations
[ ] Conflits
[ ] Configuration
[ ] Permissions
[ ] Données
[ ] Migration
[ ] Backup
[ ] Tests
[ ] Staging
[ ] Déploiement
[ ] Rollback
[ ] Breaking changes
[ ] Actions manuelles
```

---

# 91. Checklist `upgrade apply`

```text
[ ] Repository propre
[ ] Branche dédiée
[ ] Plan validé
[ ] Backup préparé
[ ] Versions verrouillées
[ ] Fichiers custom préservés
[ ] Fichiers protected préservés
[ ] Conflits résolus
[ ] Migration préparée
[ ] Restore réussi
[ ] Build Release réussi
[ ] Tests réussis
[ ] Scans réussis
[ ] Lock file finalisé
[ ] Rapport produit
[ ] Aucun déploiement automatique
```

---

# 92. Checklist avant production

```text
[ ] Pull request approuvée
[ ] CI verte
[ ] Staging validée
[ ] Recette validée
[ ] Migration inspectée
[ ] Backup réussi
[ ] Image cible disponible
[ ] Image précédente disponible
[ ] Fenêtre de maintenance
[ ] Smoke tests
[ ] Rollback
[ ] Communication préparée
```

---

# 93. Checklist de maintenance mensuelle

```text
[ ] Alertes de sécurité
[ ] Packages vulnérables
[ ] Images de base
[ ] GitHub Actions
[ ] Certificats
[ ] Domaines
[ ] Backups
[ ] Restore drill
[ ] Espace disque
[ ] Logs
[ ] Comptes
[ ] Versions client
[ ] Modules dépréciés
[ ] Rapport
```

---

# 94. Checklist de fin de support

```text
[ ] Version concernée
[ ] Projets concernés
[ ] Risques identifiés
[ ] Date communiquée
[ ] Upgrade proposé
[ ] Effort estimé
[ ] Refus éventuel documenté
[ ] Responsabilités clarifiées
[ ] Statut unsupported appliqué
```

---

# 95. Conclusion

La maintenance SiteFactory doit permettre de faire évoluer les projets clients sans perdre les bénéfices de l’industrialisation initiale.

Elle repose sur :

```text
versions explicites
+ compatibilité documentée
+ détection
+ planification
+ branche dédiée
+ protection des personnalisations
+ migrations contrôlées
+ tests
+ staging
+ rollback
+ traçabilité
```

Une mise à niveau réussie ne consiste pas uniquement à installer une version plus récente.

Elle doit démontrer que :

- le projet fonctionne encore ;
- les données sont cohérentes ;
- les personnalisations sont préservées ;
- les protections sont intactes ;
- le retour arrière reste maîtrisé.

> Maintenir un projet SiteFactory, ce n’est pas le garder systématiquement à la dernière version. C’est le garder dans une version connue, supportée, sécurisée et compatible avec ses besoins réels.
