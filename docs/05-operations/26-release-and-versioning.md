# LogicGate SiteFactory — Releases et versionnement

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Releases et versionnement |
| Version | 1.0 |
| Statut | Contrat cible ; capacités publiques livrées distinguées |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Runtime cible initial | .NET 10 |
| Gestionnaire de code source | Git et GitHub |
| Distribution cible | NuGet.org pour les composants publics ; privé séparé pour Business |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../03-generation/13-generation-pipeline.md`, `../02-architecture/core-module-boundary.md`, `09-module-architecture.md`, `13-generation-pipeline.md`, `15-cli-specification.md`, `18-security-baseline.md`, `19-testing-strategy.md`, `20-quality-gates-and-ci.md`, `22-deployment-and-environments.md`, `25-upgrade-and-maintenance-strategy.md` |
| Dernière mise à jour | 2026-10-21 |

---

## État public actuel et politique de publication cible

Le [README](../../README.md) et le [CHANGELOG](../../CHANGELOG.md) décrivent
le public implémenté. **Aucun package NuGet, release officielle ou repository
public n'est encore publié.** Les capacités restent sous Unreleased.
La version de développement est `0.1.0-dev`, définie par VersionPrefix/VersionSuffix
dans [Directory.Build.props](../../Directory.Build.props).
Les versions exactes des modules/templates sont distinctes ; ce ne sont pas
des releases de packages.

[ADR-0035](../adr/0035-partage-public-component-via-nuget.md) prévoit les packages
réutilisables publics sur NuGet.org ; les modules Business demeurent privés.
Aucun ProjectReference cross-repository après séparation, aucun accès privé
requis pour construire/tester le public, aucun fallback de source NuGet.
[ADR-0036](../adr/0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md)
impose initialement une version commune aux futurs packages publics ; les modules
peuvent avoir leur version propre et doivent déclarer leur compatibilité.
Le SDK actuel valide des versions exactes, pas encore des plages.

Les sections numérotées décrivent la **politique cible de release**,
pas un pipeline de publication déjà opérationnel. Exemples de tags, packages,
manifest enrichi, images, versions et canaux : illustratifs, sans disponibilité
affirmée ; les identifiants `sample-*` désignent des composants fictifs.
`--version` existe ; la commande `version`, ses sorties JSON détaillées et les
commandes d'installation ci-dessous restent cibles.
Pour le véritable CLI depuis le checkout :

```powershell
dotnet run --project src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj --configuration Release --no-build --no-restore -- --version
```

La CI Windows/Linux est préparée, non validée à distance ; SourceLink public
reste PENDING. Une preuve locale, y compris PostgreSQL, ne vaut ni release,
publication, déploiement client ni validation de production.
Les exigences de tag/commit exact, artefacts immuables, scans, preuves,
compatibilité et rollback restent obligatoires avant une future publication
expressément autorisée. Les droits/notices tiers et la licence des sorties
générées ne sont pas modifiés par ce document.

---

## 1. Objet du document

Ce document définit la stratégie de versionnement et de release de LogicGate SiteFactory.

Il précise notamment :

- les composants versionnés ;
- les règles SemVer ;
- les versions préliminaires ;
- les canaux de publication ;
- les branches ;
- les tags Git ;
- la source de vérité des versions ;
- le versionnement du CLI ;
- le versionnement du générateur ;
- le versionnement des catalogues ;
- le versionnement des schémas ;
- le versionnement des modules ;
- le versionnement des templates ;
- le versionnement des thèmes ;
- le versionnement des profils de déploiement ;
- le versionnement des projets clients ;
- la compatibilité entre composants ;
- les manifests de release ;
- les changelogs ;
- les notes de migration ;
- la construction des artefacts ;
- les checksums ;
- la SBOM ;
- la provenance ;
- les quality gates ;
- le workflow de release ;
- la publication ;
- la dépréciation ;
- le retrait d’une version ;
- les hotfixes ;
- les rollbacks ;
- les contraintes applicables à Codex.

L’objectif est de pouvoir répondre avec certitude aux questions suivantes :

```text
Quelle version a été publiée ?

Quel code contient-elle ?

Quels artefacts ont été produits ?

Quelles versions de modules contient-elle ?

Avec quelles versions est-elle compatible ?

Quels changements apporte-t-elle ?

Comment l’installer ?

Comment mettre à niveau un projet existant ?

Comment revenir à une version précédente ?
```

---

## 2. Vision

Une release SiteFactory doit être :

- identifiable ;
- reproductible ;
- immuable ;
- testée ;
- documentée ;
- traçable ;
- récupérable ;
- compatible avec une politique d’upgrade explicite.

Le flux de référence est :

```text
Modification
→ pull request
→ quality gates
→ merge sur main
→ préparation de version
→ release candidate éventuelle
→ suite complète
→ tag
→ construction des artefacts
→ vérification
→ publication
→ notes de release
→ surveillance
```

Une release ne doit pas être seulement :

```text
un numéro de version modifié
```

Elle constitue un ensemble cohérent comprenant :

```text
code source
+ dépendances
+ packages
+ CLI
+ catalogues
+ schémas
+ templates
+ thèmes
+ documentation
+ changelog
+ artefacts
+ preuves de validation
```

---

## 3. Principes fondamentaux

## 3.1 Toute version publiée est immuable

Une version publiée ne doit jamais être remplacée par un contenu différent.

Cela concerne :

- package NuGet ;
- outil .NET ;
- module ;
- template ;
- thème ;
- catalogue ;
- image Docker ;
- archive ;
- manifest ;
- schéma.

Une correction produit une nouvelle version.

---

## 3.2 Les versions flottantes sont interdites

Sont interdits comme références de production :

```text
latest
*
1.*
main
master
develop
```

Une référence doit être :

- une version exacte ;
- un tag exact ;
- un commit exact ;
- un digest exact.

---

## 3.3 Une release correspond à un commit exact

Chaque release doit être reliée à :

```text
CommitSha
GitTag
Version
WorkflowRun
```

Le repository doit être propre au moment de la création du tag.

---

## 3.4 Le même commit produit les artefacts

Les artefacts publiés doivent être construits depuis le commit portant le tag.

Il est interdit de :

- modifier le code après le tag ;
- publier un package construit depuis un autre commit ;
- reconstruire manuellement un artefact différent sous la même version.

---

## 3.5 Les composants peuvent évoluer indépendamment

Selon ADR-0036, les futurs packages publics SiteFactory partagent initialement
une version commune. Les modules/templates peuvent conserver leur identité
et leur version propre ; cela n'autorise pas un versionnement indépendant
par package public sans besoin démontré et décision explicite.

Exemple :

```text
SiteFactory CLI                 0.6.0
Generator                       0.6.0
Catalog                         1.2.0
Template sample-template       1.1.0
Theme sample-theme        1.0.3
Module sample-notes          1.4.1
Module sample-navigation             1.2.0
```

Le lock file du projet client conserve la composition exacte.

---

## 3.6 Une release coordonnée peut regrouper plusieurs composants

LogicGate peut publier une release SiteFactory regroupant plusieurs nouvelles versions.

Exemple :

```text
SiteFactory Release 0.6.0

Generator                       0.6.0
Catalog                         1.2.0
Template sample-template       1.1.0
Module sample-notes          1.4.1
```

La release coordonnée ne supprime pas l’identité propre de chaque composant.

---

## 3.7 Le versionnement ne remplace pas la compatibilité

Deux composants possédant des versions valides ne sont pas nécessairement compatibles.

Une matrice de compatibilité reste obligatoire.

---

## 3.8 Une release stable nécessite des preuves

Avant publication stable, il faut au minimum :

```text
Restore réussi
Build Release réussi
Tests réussis
Projet de référence généré
Projet généré compilé
Scans réussis
Artefacts inspectés
Changelog à jour
Documentation à jour
```

---

# 4. Non-objectifs

La stratégie initiale ne cherche pas à fournir :

- un monorepo release train extrêmement complexe ;
- une publication quotidienne automatique ;
- un auto-versionnement opaque ;
- un auto-merge ;
- un remplacement automatique des versions clientes ;
- un déploiement production lors de chaque release ;
- une signature matérielle HSM dès le MVP ;
- un registry public ;
- une compatibilité infinie ;
- une publication de tous les composants à chaque modification ;
- une release stable depuis une pull request.

---

# 5. Terminologie

## 5.1 Version

Identifiant d’un état publié d’un composant.

Exemple :

```text
1.4.2
```

---

## 5.2 Release

Publication officielle d’une ou plusieurs versions accompagnées de leurs artefacts et documents.

---

## 5.3 Artefact

Fichier produit par la release.

Exemples :

- package NuGet ;
- package d’outil .NET ;
- archive ZIP ;
- image Docker ;
- SBOM ;
- checksum ;
- manifest.

---

## 5.4 Build metadata

Information facultative ajoutée à une version SemVer.

Exemple :

```text
1.4.2+build.153
```

Elle ne modifie pas la priorité de la version.

---

## 5.5 Pre-release

Version non stable destinée à la validation.

Exemples :

```text
1.4.0-alpha.1
1.4.0-beta.2
1.4.0-rc.1
```

---

## 5.6 Release candidate

Version considérée comme potentiellement stable si aucun défaut bloquant n’est détecté.

---

## 5.7 Stable release

Version destinée aux projets clients selon la politique de support.

---

## 5.8 Release manifest

Document structuré décrivant précisément la release et ses artefacts.

---

## 5.9 Provenance

Informations permettant d’identifier l’origine d’un artefact :

- repository ;
- commit ;
- workflow ;
- dépendances ;
- plateforme ;
- date.

---

## 5.10 Unlist

Action consistant à masquer une version d’un feed sans remplacer son contenu.

---

# 6. Composants versionnés

Les composants suivants doivent posséder une version explicite.

## 6.1 Produit principal

```text
LogicGate.SiteFactory.Cli
LogicGate.SiteFactory.Infrastructure
LogicGate.SiteFactory.Application
LogicGate.SiteFactory.ModuleSdk
```

---

## 6.2 Contrats structurants

```text
ClientConfigurationSchema
CatalogSchema
ModuleManifestSchema
TemplateManifestSchema
ThemeManifestSchema
GenerationPlanSchema
GenerationReportSchema
LockFileSchema
UpgradeReportSchema
DeploymentReportSchema
BackupManifestSchema
```

---

## 6.3 Bibliothèques runtime publiques

```text
LogicGate.SiteFactory.Runtime.Contracts
LogicGate.SiteFactory.Runtime
LogicGate.SiteFactory.Infrastructure.PostgreSql
LogicGate.SiteFactory.Migrator
```

Ces projets existent ; leurs futures unités de distribution restent à choisir,
sans transformer automatiquement chaque projet en package publié.
La CLI publique ne génère pas une plateforme cliente Business.

---

## 6.4 Modules

Module public enregistré : `documentation`.
Les modules `sample-*` des exemples enrichis ci-dessous sont fictifs,
pas une liste de fonctionnalités métier disponibles.

---

## 6.5 Templates

Exemple fictif, non enregistré :

```text
sample-template
```

---

## 6.6 Thèmes

Exemple fictif, non enregistré :

```text
sample-theme
```

---

## 6.7 Profils de déploiement

Profil structurel reconnu dans la V1, sans déploiement automatique :

```text
docker-compose-vps
```

---

## 6.8 Prompts LLM

Chaque tâche et prompt LLM possède une version propre.

Ils ne doivent pas être liés uniquement à la version du générateur.

---

# 7. Versionnement sémantique

La stratégie utilise :

```text
MAJOR.MINOR.PATCH
```

---

## 7.1 PATCH

Une version patch contient principalement :

- correction de défaut ;
- correction de sécurité compatible ;
- amélioration de diagnostic ;
- optimisation interne ;
- correction documentaire ;
- correction de génération ne modifiant pas le contrat public.

Exemple :

```text
1.4.1 → 1.4.2
```

---

## 7.2 MINOR

Une version mineure ajoute une capacité compatible.

Exemples :

- nouvelle option facultative ;
- nouvelle commande ;
- nouvelle section ;
- nouvelle permission non attribuée automatiquement ;
- nouveau champ facultatif ;
- nouveau module compatible ;
- nouvelle règle de validation non destructrice.

Exemple :

```text
1.4.2 → 1.5.0
```

---

## 7.3 MAJOR

Une version majeure peut contenir une incompatibilité.

Exemples :

- suppression d’un contrat ;
- suppression d’un champ ;
- changement de schéma ;
- renommage d’un identifiant stable ;
- nouvelle structure générée incompatible ;
- changement d’API publique ;
- modification de comportement imposant une migration ;
- arrêt d’une version .NET ;
- suppression d’un module.

Exemple :

```text
1.9.0 → 2.0.0
```

---

## 7.4 Versions avant `1.0.0`

Avant la version stable :

```text
0.MINOR.PATCH
```

Une version mineure peut comporter des modifications importantes.

Toute incompatibilité doit cependant être :

- annoncée ;
- documentée ;
- accompagnée d’un plan de migration ;
- testée.

---

# 8. Versions préliminaires

## 8.1 Alpha

Format :

```text
0.6.0-alpha.1
```

Usage :

- développement incomplet ;
- exploration ;
- API susceptible de changer ;
- tests internes.

Une alpha n’est pas destinée à la production cliente.

---

## 8.2 Beta

Format :

```text
0.6.0-beta.1
```

Usage :

- fonctionnalités principales présentes ;
- validation interne ;
- projet pilote ;
- retours d’usage.

---

## 8.3 Release candidate

Format :

```text
0.6.0-rc.1
```

Usage :

- candidat final ;
- aucune fonctionnalité majeure supplémentaire ;
- corrections uniquement ;
- suite de release complète.

---

## 8.4 Stable

Format :

```text
0.6.0
```

Même avant `1.0.0`, une version sans suffixe est considérée comme stable dans le périmètre de sa politique de support.

---

## 8.5 Incrémentation

Exemple :

```text
0.6.0-alpha.1
0.6.0-alpha.2
0.6.0-beta.1
0.6.0-rc.1
0.6.0-rc.2
0.6.0
```

---

## 8.6 Correction après release candidate

Une correction sur `0.6.0-rc.1` produit :

```text
0.6.0-rc.2
```

Elle ne remplace pas la première release candidate.

---

# 9. Canaux de publication

## 9.1 `internal`

Contient :

- builds de développement ;
- artefacts temporaires ;
- versions non destinées aux projets clients.

---

## 9.2 `preview`

Contient :

- alpha ;
- beta ;
- release candidates.

---

## 9.3 `stable`

Contient les versions validées pour les projets supportés.

---

## 9.4 Séparation

Une version preview ne doit pas être sélectionnée automatiquement par un projet stable.

---

## 9.5 Configuration explicite

L’usage d’une preview nécessite une configuration ou une option explicite.

Exemple conceptuel :

```bash
sitefactory install \
  --version 0.6.0-rc.1 \
  --allow-preview
```

---

# 10. Stratégie Git

## 10.1 Branche principale

```text
main
```

représente la prochaine version intégrable.

Elle doit rester verte.

---

## 10.2 Branches de travail

Formats :

```text
feature/...
fix/...
security/...
docs/...
refactor/...
upgrade/...
release/...
hotfix/...
```

---

## 10.3 Branche de release

Une branche de release peut être utilisée lorsque :

- plusieurs corrections doivent être stabilisées ;
- une release candidate doit être préparée ;
- `main` doit continuer à recevoir des évolutions.

Format :

```text
release/0.6.0
```

Elle n’est pas obligatoire pour chaque patch simple.

---

## 10.4 Branche de hotfix

Format :

```text
hotfix/0.5.1-security-fix
```

Elle part du tag ou de la branche correspondant à la version affectée.

---

## 10.5 Historique

Pour une équipe réduite, le squash merge est recommandé pour les branches de fonctionnalité.

Les commits de release et tags restent explicites.

---

# 11. Tags Git

## 11.1 Format principal

```text
v{version}
```

Exemples :

```text
v0.6.0
v0.6.1
v1.0.0
```

---

## 11.2 Tags de composants indépendants

Si certains composants sont publiés indépendamment dans le même repository, formats possibles :

```text
cli/v0.6.0
module/sample-notes/v1.4.1
template/sample-template/v1.1.0
theme/sample-theme/v1.0.3
```

---

## 11.3 Décision initiale

Pendant le MVP, LogicGate peut utiliser :

```text
v{SiteFactoryVersion}
```

pour les releases coordonnées.

Les versions propres des composants restent inscrites dans le manifest.

Des tags spécifiques seront introduits lorsque les composants auront réellement des cycles indépendants.

---

## 11.4 Tags annotés

Les releases stables doivent utiliser des tags annotés.

Le tag doit contenir :

- version ;
- résumé ;
- éventuellement référence au changelog.

---

## 11.5 Signature

La signature cryptographique des tags peut être ajoutée après stabilisation de l’identité de publication.

Elle est recommandée pour les releases stables à terme.

---

## 11.6 Tag immuable

Un tag publié ne doit jamais être déplacé.

Une erreur produit :

- un nouveau commit ;
- un nouveau tag ;
- une nouvelle version.

---

# 12. Source de vérité de la version

## 12.1 Fichier central

La version principale SiteFactory doit provenir d’une source centralisée.

La source actuelle est `Directory.Build.props` :

```xml
<VersionPrefix>0.1.0</VersionPrefix>
<VersionSuffix>dev</VersionSuffix>
```

Ce repère de développement ne prouve aucune publication. Les versions des
descripteurs/template restent distinctes ; ne pas dupliquer cette source.

---

## 12.2 Pas de duplication manuelle

La même version ne doit pas être modifiée séparément dans :

- plusieurs `.csproj` ;
- README ;
- workflow ;
- script ;
- Dockerfile ;
- manifest.

Les autres emplacements doivent dériver de la source de vérité.

---

## 12.3 Version de build

La CI peut enrichir l’artefact avec :

```text
Version
CommitSha
BuildNumber
```

sans modifier la version fonctionnelle publiée.

---

# 13. Version du CLI

## 13.1 Commande

Le CLI doit exposer :

```bash
sitefactory --version
```

ou :

```bash
sitefactory version
```

---

## 13.2 Sortie humaine

Exemple :

```text
LogicGate SiteFactory CLI 0.6.0
Commit: abc1234
Runtime: .NET 10
```

---

## 13.3 Sortie JSON

```json
{
  "product": "LogicGate SiteFactory CLI",
  "version": "0.6.0",
  "commitSha": "abc1234",
  "runtime": ".NET 10"
}
```

---

## 13.4 Pas de données sensibles

La sortie ne contient pas :

- chemin local ;
- utilisateur de build ;
- endpoint privé ;
- token ;
- feed credential.

---

# 14. Version du générateur

Le générateur possède une version enregistrée dans :

```text
sitefactory.lock.json
generation-report.json
upgrade-report.json
```

Elle doit correspondre à l’artefact réellement utilisé.

---

## 14.1 Génération locale modifiée

Un binaire non publié ou modifié localement doit être identifiable comme version de développement.

Exemple conceptuel :

```text
0.6.0-dev+abc1234
```

Un projet client de production ne doit pas être généré avec un binaire local non traçable.

---

# 15. Version des schémas

## 15.1 Version indépendante

Les schémas structurants utilisent une version de schéma distincte de SemVer lorsque le format JSON le prévoit.

Exemple :

```json
{
  "schemaVersion": 1
}
```

---

## 15.2 Évolution compatible

Un nouveau champ facultatif peut rester dans :

```text
schemaVersion = 1
```

si les anciens lecteurs continuent de fonctionner.

---

## 15.3 Évolution incompatible

Une modification incompatible produit :

```text
schemaVersion = 2
```

et nécessite :

- migration ;
- compatibilité ;
- documentation ;
- tests.

---

## 15.4 Version de l’artefact de schéma

Le package ou fichier contenant le schéma peut également posséder une version SemVer.

Exemple :

```text
sample-schema-contracts 1.3.0
```

---

# 16. Version du catalogue

Le catalogue possède :

```text
CatalogVersion
SchemaVersion
PublishedAtUtc
```

Exemple :

```yaml
catalog:
  version: 1.2.0
  schemaVersion: 1
```

---

## 16.1 Prix et catalogue

Une modification de prix peut produire une nouvelle version du catalogue.

Elle ne modifie pas automatiquement :

- un devis signé ;
- un entitlement client ;
- un lock file ;
- un contrat existant.

---

## 16.2 Correction d’une erreur commerciale

Une correction d’un prix erroné doit :

- produire une nouvelle version ;
- mettre à jour les tests ;
- mettre à jour la documentation ;
- analyser les projets ou devis affectés.

---

# 17. Version des modules

Chaque module possède une version SemVer.

Exemple de manifest :

```yaml
module:
  id: sample-notes
  version: 1.4.1
  schemaVersion: 1
```

---

## 17.1 Version du package

Le package NuGet et le manifest doivent posséder la même version publiée.

---

## 17.2 Version de données

Un module peut déclarer une version interne de données ou de migration lorsque nécessaire.

Exemple conceptuel :

```yaml
data:
  schemaVersion: 3
```

Cette valeur ne remplace pas l’historique EF Core.

---

## 17.3 Module sans changement

Un module non modifié ne doit pas recevoir une nouvelle version uniquement parce qu’un autre module est publié, sauf stratégie de release coordonnée explicitement retenue.

---

# 18. Version des templates

Chaque template possède :

```text
TemplateId
TemplateVersion
ManifestSchemaVersion
CompatibleGeneratorRange
CompatiblePlatformRange
```

Exemple :

```yaml
template:
  id: sample-template
  version: 1.1.0
  schemaVersion: 1

compatibility:
  generator: ">=0.6.0 <0.7.0"
  platform: ">=1.2.0 <2.0.0"
```

La syntaxe exacte des plages doit être validée.

---

## 18.1 Changement éditorial

Une simple correction de texte d’exemple peut produire un patch.

---

## 18.2 Nouvelle section facultative

Peut produire une version mineure.

---

## 18.3 Structure incompatible

Produit une version majeure.

---

# 19. Version des thèmes

Chaque thème possède :

```text
ThemeId
ThemeVersion
TokenSchemaVersion
CompatibleDesignSystemRange
```

---

## 19.1 Correction visuelle

Patch :

- correction de contraste ;
- correction responsive ;
- correction d’un état focus.

---

## 19.2 Nouveau token facultatif

Minor.

---

## 19.3 Suppression ou renommage de token

Major, sauf période de compatibilité avec alias.

---

# 20. Version des profils de déploiement

Le profil :

```text
docker-compose-vps
```

possède une version.

Cette version couvre notamment :

- Dockerfile ;
- Compose ;
- Caddy ;
- scripts ;
- health checks ;
- conventions de volumes ;
- conventions réseau.

---

## 20.1 Changement de script compatible

Patch.

---

## 20.2 Nouvelle option facultative

Minor.

---

## 20.3 Changement de structure ou procédure obligatoire

Major.

---

# 21. Version du projet client

## 21.1 Version applicative propre

Chaque projet client possède sa propre version applicative.

Exemple :

```text
1.3.0
```

Cette version ne correspond pas nécessairement à la version du générateur.

---

## 21.2 Évolutions clientes

La version applicative augmente lors de :

- ajout de fonctionnalité ;
- correction ;
- upgrade de modules ;
- changement de contenu structurel ;
- migration ;
- modification de sécurité significative.

---

## 21.3 Exemple

```text
Client application             1.3.0
Generated with SiteFactory     0.6.0
Template                       1.1.0
sample-notes                1.4.1
sample-navigation                   1.2.0
```

---

## 21.4 Tags du repository client

Format recommandé :

```text
v1.3.0
```

---

## 21.5 Déploiement

Le tag client permet d’identifier l’artefact déployé.

---

# 22. `sitefactory.lock.json`

Le lock file enregistre la composition exacte.

Exemple conceptuel :

```json
{
  "schemaVersion": 1,
  "generator": {
    "version": "0.6.0"
  },
  "catalog": {
    "version": "1.2.0"
  },
  "template": {
    "id": "sample-template",
    "version": "1.1.0"
  },
  "theme": {
    "id": "sample-theme",
    "version": "1.0.3"
  },
  "modules": [
    {
      "id": "sample-notes",
      "version": "1.4.1"
    },
    {
      "id": "sample-navigation",
      "version": "1.2.0"
    }
  ],
  "deploymentProfile": {
    "id": "docker-compose-vps",
    "version": "1.0.0"
  }
}
```

---

## 22.1 Immutabilité logique

Le lock file ne doit pas être modifié manuellement pour simuler une mise à niveau.

Il est produit ou mis à jour par :

- génération ;
- upgrade validé ;
- migration de schéma contrôlée.

---

# 23. Compatibilité

## 23.1 Matrice

La release doit publier ou mettre à jour une matrice contenant :

```text
Composant
Version
Compatible avec
Incompatible avec
Version minimale
Version maximale
Migration requise
```

---

## 23.2 Compatibilité du générateur

Le générateur doit vérifier :

- version du schéma de configuration ;
- version du catalogue ;
- version du template ;
- version du thème ;
- version des manifests ;
- version de la plateforme.

---

## 23.3 Compatibilité du projet client

L’upgrade doit vérifier :

- version source ;
- version cible ;
- étapes intermédiaires ;
- modules dépréciés ;
- runtime ;
- PostgreSQL ;
- migrations.

---

## 23.4 Échec précoce

Une incompatibilité connue doit être refusée avant toute écriture.

---

# 24. Release coordonnée

## 24.1 Définition

Une release coordonnée regroupe plusieurs composants compatibles.

Exemple :

```text
SiteFactory 0.6.0
```

avec :

```text
Generator 0.6.0
Catalog 1.2.0
Platform 1.2.0
Template sample-template 1.1.0
Theme sample-theme 1.0.3
```

---

## 24.2 Release manifest

La composition exacte est enregistrée dans le release manifest.

---

## 24.3 Composant inchangé

Un composant inchangé conserve sa version précédente dans le manifest.

---

# 25. Release manifest

## 25.1 Fichier

```text
sitefactory-release-manifest.json
```

---

## 25.2 Structure conceptuelle

```json
{
  "schemaVersion": 1,
  "releaseId": "f1f6552a-9a56-43a4-a6d7-dae4b1bc6c3e",
  "product": "LogicGate SiteFactory",
  "version": "0.6.0",
  "channel": "stable",
  "git": {
    "tag": "v0.6.0",
    "commitSha": "abc1234"
  },
  "runtime": {
    "dotnet": "10"
  },
  "components": [
    {
      "id": "generator",
      "version": "0.6.0"
    },
    {
      "id": "catalog",
      "version": "1.2.0"
    },
    {
      "id": "template:sample-template",
      "version": "1.1.0"
    }
  ],
  "artifacts": [
    {
      "name": "LogicGate.SiteFactory.Cli.0.6.0.nupkg",
      "type": "nuget",
      "sha256": "..."
    }
  ],
  "quality": {
    "buildWarnings": 0,
    "buildErrors": 0,
    "testsPassed": 428,
    "testsFailed": 0,
    "testsSkipped": 0
  },
  "security": {
    "secretScan": "success",
    "dependencyScan": "success",
    "staticAnalysis": "success"
  },
  "publishedAtUtc": "2026-07-28T10:00:00Z"
}
```

---

## 25.3 Données interdites

Le manifest ne contient pas :

- secret ;
- token ;
- chemin local privé ;
- credential ;
- donnée client ;
- contenu confidentiel.

---

# 26. Changelog

## 26.1 Fichier principal

```text
CHANGELOG.md
```

---

## 26.2 Format

Catégories recommandées :

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

## 26.3 Entrée `Unreleased`

Les changements intégrés mais non publiés sont placés sous :

```markdown
## [Unreleased]
```

---

## 26.4 Publication

Lors de la release :

```text
Unreleased
→ version et date
```

Puis une nouvelle section `Unreleased` vide est préparée.

---

## 26.5 Contenu utile

Une entrée doit expliquer l’impact.

Exemple recommandé :

```text
Fixed
- Preserve custom files during project regeneration when a module
  updates its generated composition files.
```

Exemple insuffisant :

```text
- Fix generator.
```

---

## 26.6 Changelog par composant

Les modules ayant un cycle propre peuvent posséder leur propre changelog.

Le changelog principal résume les changements coordonnés.

---

# 27. Notes de release

Les notes de release doivent contenir :

```text
Résumé
Nouveautés
Corrections
Sécurité
Breaking changes
Dépréciations
Compatibilité
Migrations
Actions manuelles
Installation
Upgrade
Artefacts
Limitations connues
```

---

## 27.1 Sécurité

Une vulnérabilité peut être décrite sans fournir immédiatement un niveau de détail facilitant son exploitation, selon le contexte.

---

## 27.2 Limitation connue

Une limitation connue doit être publiée si elle affecte raisonnablement l’usage.

---

# 28. Notes de migration

Une release nécessitant une migration doit fournir :

```text
Source supportée
Cible
Préconditions
Backup
Configuration
Données
Commandes
Tests
Rollback
```

---

## 28.1 Emplacement

```text
docs/migrations/{source}-to-{target}.md
```

Exemple :

```text
docs/migrations/0.5-to-0.6.md
```

---

# 29. Artefacts d’une release SiteFactory

Artefacts possibles :

```text
CLI NuGet Tool package
Packages NuGet
Archive des catalogues
Archive des templates
Archive des thèmes
Release manifest
Checksums
SBOM
Documentation
Notes de release
```

---

## 29.1 Artefacts minimum du MVP

```text
LogicGate.SiteFactory.Cli.{version}.nupkg

Packages SiteFactory modifiés

sitefactory-release-manifest.json

checksums.sha256

CHANGELOG.md

Notes de release
```

---

# 30. Package du CLI

## 30.1 Distribution

Le CLI peut être distribué comme outil .NET.

Installation conceptuelle :

```bash
dotnet tool install \
  LogicGate.SiteFactory.Cli \
  --version 0.6.0 \
  --add-source <feed-configuré>
```

---

## 30.2 Mise à jour

```bash
dotnet tool update \
  LogicGate.SiteFactory.Cli \
  --version 0.6.0 \
  --add-source <feed-configuré>
```

---

## 30.3 Manifest d’outil local

Les repositories peuvent verrouiller le CLI via un manifest d’outil local.

Exemple conceptuel :

```text
.config/dotnet-tools.json
```

Cette stratégie est recommandée pour la CI et les projets clients nécessitant une version précise.

---

# 31. Packages NuGet

## 31.1 Métadonnées obligatoires

Chaque package doit définir :

- PackageId ;
- Version ;
- Authors ;
- Description ;
- Repository URL ;
- Repository commit ;
- licence ;
- tags ;
- documentation éventuelle.

---

## 31.2 Symbol packages

Les symbol packages peuvent être publiés dans le feed adapté pour faciliter le diagnostic.

Ils ne doivent contenir aucun secret ou source confidentielle non prévue.

---

## 31.3 SourceLink

SourceLink peut être utilisé lorsque le repository et la politique d’accès le permettent.

---

## 31.4 Contenu du package

La CI doit inspecter :

- fichiers ;
- dépendances ;
- assemblies ;
- documentation ;
- manifests ;
- absence de secret ;
- absence de fichiers temporaires.

---

# 32. Images Docker

## 32.1 Statut

SiteFactory n’est pas obligatoirement distribué sous forme d’image dans le MVP.

Les projets clients, eux, produisent leur propre image.

---

## 32.2 Tags

Une image cliente utilise :

```text
1.3.0
sha-abc1234
```

et éventuellement :

```text
1.3
```

comme alias contrôlé.

La production doit verrouiller une version ou un digest.

---

## 32.3 `latest`

Le tag `latest` peut exister comme commodité de développement.

Il ne constitue jamais l’unique référence de production.

---

## 32.4 Digest

Le digest peut être enregistré dans :

```text
deployment-report.json
```

---

# 33. Nommage des artefacts

Format recommandé :

```text
{ProductOrComponent}.{Version}.{Platform}.{Extension}
```

Exemples :

```text
LogicGate.SiteFactory.Cli.0.6.0.nupkg

LogicGate.SiteFactory.Templates.0.6.0.zip

LogicGate.SiteFactory.0.6.0.release-manifest.json

LogicGate.SiteFactory.0.6.0.checksums.sha256
```

---

## 33.1 Données interdites

Les noms ne contiennent pas :

- secret ;
- nom client confidentiel ;
- chemin local ;
- nom d’utilisateur de build.

---

# 34. Checksums

## 34.1 Algorithme

Les artefacts de release doivent recevoir une empreinte :

```text
SHA-256
```

---

## 34.2 Fichier

```text
checksums.sha256
```

---

## 34.3 Vérification

Les checksums sont calculés après construction finale.

Ils doivent être publiés avec les artefacts.

---

## 34.4 Limite

Un checksum prouve l’intégrité binaire relative à la valeur publiée.

Il ne prouve pas l’identité du producteur sans mécanisme de signature.

---

# 35. Signature des artefacts

## 35.1 Statut MVP

La signature n’est pas obligatoire dans le premier incrément.

---

## 35.2 Cible

À terme, envisager :

- signature des packages ;
- signature des tags ;
- signature des checksums ;
- provenance attestée.

---

## 35.3 Clés

Les clés de signature doivent être :

- protégées ;
- limitées au workflow de release ;
- associées à un environnement protégé ;
- renouvelables ;
- absentes des pull requests.

---

# 36. SBOM

## 36.1 Objectif

La Software Bill of Materials décrit les composants inclus.

---

## 36.2 Périmètre

Elle peut inclure :

- packages NuGet ;
- dépendances transitives ;
- composants système ;
- image Docker ;
- versions.

---

## 36.3 Production

Une SBOM est recommandée pour les releases stables avant `1.0` si l’outillage reste raisonnable.

Elle devient obligatoire pour une version stable destinée à plusieurs clients lorsque la supply chain est stabilisée.

---

## 36.4 Confidentialité

La SBOM ne contient pas :

- secret ;
- donnée client ;
- credential ;
- configuration de production.

---

# 37. Provenance

La release doit pouvoir être reliée à :

```text
Repository
CommitSha
Tag
Workflow
Runner
SDK
Dépendances
Artefacts
Checksums
```

---

## 37.1 Build manuel

Un build manuel local ne doit pas être publié comme release stable officielle.

---

## 37.2 Reproductibilité

La release doit permettre de reconstruire les artefacts depuis :

- le tag ;
- le SDK verrouillé ;
- les sources NuGet autorisées ;
- les scripts versionnés.

---

# 38. Préparation d’une release

## 38.1 Préconditions

```text
[ ] Branche principale verte
[ ] Scope de release défini
[ ] Version cible décidée
[ ] Changelog à jour
[ ] Documentation à jour
[ ] Breaking changes identifiés
[ ] Migrations documentées
[ ] Compatibilité mise à jour
[ ] Dépréciations documentées
[ ] Aucun secret
[ ] Aucun défaut bloquant connu
```

---

## 38.2 Gel fonctionnel

Pour une release candidate, seules les corrections nécessaires sont intégrées.

Aucune nouvelle fonctionnalité non indispensable ne doit être ajoutée tardivement.

---

# 39. Détermination de la version

## 39.1 Questions

Avant de choisir la version :

```text
Le contrat public change-t-il ?

Une migration est-elle nécessaire ?

Une configuration existante devient-elle invalide ?

Une fonctionnalité compatible est-elle ajoutée ?

S’agit-il uniquement d’une correction ?
```

---

## 39.2 Décision

```text
Correction compatible
→ PATCH

Capacité compatible
→ MINOR

Incompatibilité
→ MAJOR
```

---

## 39.3 Sécurité

Un correctif de sécurité compatible utilise généralement PATCH.

Une suppression urgente incompatible peut nécessiter MAJOR ou une dérogation documentée selon la gravité.

---

# 40. Pull request de release

Une pull request de préparation peut contenir :

- version ;
- changelog ;
- notes ;
- manifests ;
- compatibilité ;
- documentation ;
- aucun changement fonctionnel inattendu.

---

## 40.1 Titre

Exemple :

```text
release: prepare SiteFactory 0.6.0
```

---

## 40.2 Revue

La revue doit vérifier :

- version ;
- contenu ;
- migrations ;
- artefacts attendus ;
- qualité ;
- sécurité ;
- documentation.

---

# 41. Workflow de release

## 41.1 Déclenchement

Options autorisées :

```text
tag Git
workflow_dispatch avec version contrôlée
```

Baseline recommandée :

```text
tag Git validé
→ workflow de release
```

---

## 41.2 Environnement protégé

La publication utilise un environnement :

```text
nuget-release
```

avec :

- approbation humaine ;
- secrets limités ;
- branche ou tag autorisé.

---

## 41.3 Jobs

Flux recommandé :

```text
Validate tag
→ Checkout tag
→ Setup SDK
→ Restore
→ Build Release
→ Tests
→ Generated project
→ Security scans
→ Pack
→ Inspect packages
→ Generate SBOM
→ Generate checksums
→ Generate release manifest
→ Publish preview or stable
→ Create GitHub release
```

---

## 41.4 Aucun rebuild après validation

Le job de publication doit publier les artefacts construits et vérifiés dans le même workflow.

Il ne doit pas reconstruire différemment après les scans.

---

# 42. Validation du tag

Le workflow doit vérifier :

- format ;
- version ;
- correspondance avec la source de vérité ;
- commit sur une branche autorisée ;
- absence de tag existant ;
- canal preview ou stable ;
- changelog.

---

## 42.1 Exemple

```text
Tag : v0.6.0
Version source : 0.6.0
→ valide
```

---

## 42.2 Incohérence

```text
Tag : v0.6.0
Version source : 0.5.3
→ échec
```

---

# 43. Quality gates de release

Une release est bloquée si :

```text
Tag invalide

Version incohérente

Restore échoué

Build Release échoué

Warning bloquant

Test échoué

Test critique ignoré

Projet généré invalide

Reproductibilité échouée

Secret détecté

Vulnérabilité bloquante

Analyse statique échouée

Package invalide

Artefact manquant

Checksum absent

Manifest invalide

Changelog absent

Migration non documentée
```

---

# 44. Tests de release

## 44.1 SiteFactory

```text
Domain
Application
Catalog
Schemas
Generation
CLI
Architecture
Security
```

---

## 44.2 Projet généré

```text
Génération d'application métier privée (preuve Business distincte)
Restore
Build Release
Tests
PostgreSQL
Identity
SEO
Docker
```

---

## 44.3 Plateformes

```text
Windows x64
Linux x64
```

---

## 44.4 Reproductibilité

Deux générations doivent produire les mêmes éléments déterministes.

---

## 44.5 Upgrade

Pour une release affectant le générateur ou les formats, tester au moins un upgrade depuis une version supportée.

---

# 45. Inspection des packages

Avant publication, vérifier :

```text
Package ID
Version
Dépendances
Assemblies
Fichiers
Licences
Repository metadata
Commit
Absence de secret
Absence de binaires inattendus
```

---

## 45.1 Commande de pack

Exemple conceptuel :

```bash
dotnet pack \
  SiteFactory.slnx \
  --configuration Release \
  --no-build \
  --output ./artifacts/packages
```

Les projets non publiables doivent avoir :

```xml
<IsPackable>false</IsPackable>
```

---

# 46. Publication NuGet

## 46.1 Sources publiques et privées séparées

Selon ADR-0035, les composants réutilisables publics seront distribués sur
NuGet.org depuis le repository public ; les packages Business restent privés.
Aucun package SiteFactory n'est encore publié. Le mapping doit empêcher
qu'un package privé soit résolu sur une source publique de remplacement.
Une PR ou un build local ne publie rien.

---

## 46.2 Credentials

Le token de publication :

- reste dans les secrets de l’environnement de release ;
- possède une portée limitée ;
- n’est pas disponible dans les pull requests ;
- n’est pas affiché.

---

## 46.3 Skip duplicate

L’utilisation d’une option permettant d’ignorer un doublon peut masquer une erreur de processus.

La publication doit plutôt vérifier qu’une version n’existe pas déjà.

---

## 46.4 Version existante

Si la version existe déjà :

```text
échec de publication
```

Il faut créer une nouvelle version.

---

# 47. Publication GitHub Release

La release GitHub peut contenir :

- notes ;
- manifest ;
- checksums ;
- SBOM ;
- archives ;
- liens vers les packages.

---

## 47.1 Draft

Le workflow peut créer une draft release avant publication finale.

---

## 47.2 Pre-release

Les versions alpha, beta et RC doivent être marquées comme pré-release.

---

## 47.3 Stable

Une version stable ne doit pas être marquée comme pré-release.

---

# 48. Validation post-publication

Après publication :

1. vérifier la présence des packages ;
2. vérifier leur version ;
3. installer le CLI depuis le feed ;
4. exécuter `sitefactory --version` ;
5. générer un projet minimal ;
6. vérifier les checksums ;
7. vérifier la release GitHub ;
8. vérifier le manifest ;
9. surveiller les premiers usages.

---

# 49. Smoke test du CLI publié

Dans un environnement propre :

```text
Installer le CLI
→ afficher la version
→ valider une configuration
→ produire un plan
→ générer un projet de référence
→ build du projet
```

Le smoke test doit utiliser le package réellement publié, pas le projet source local.

---

# 50. Release stable après preview

Flux recommandé :

```text
0.6.0-rc.1
→ validation
→ corrections éventuelles
→ 0.6.0-rc.2
→ validation
→ 0.6.0
```

La stable doit être reconstruite depuis son propre tag.

Elle ne doit pas simplement renommer le package RC.

---

# 51. Hotfix

## 51.1 Déclenchement

Un hotfix est utilisé pour :

- vulnérabilité ;
- défaut bloquant ;
- régression majeure ;
- incompatibilité de production.

---

## 51.2 Version

Exemple :

```text
0.6.0 → 0.6.1
```

---

## 51.3 Processus

```text
Branche depuis v0.6.0
→ test de régression
→ correction minimale
→ quality gates
→ tag v0.6.1
→ publication
→ propagation vers main
```

---

## 51.4 Pas de correction cachée

Le hotfix doit apparaître dans :

- changelog ;
- notes ;
- manifest ;
- inventaire des versions clientes.

---

# 52. Publication d’un correctif de sécurité

## 52.1 Informations

La release doit préciser :

- composants affectés ;
- versions vulnérables ;
- version corrigée ;
- urgence ;
- mitigation éventuelle ;
- procédure d’upgrade.

---

## 52.2 Détail de vulnérabilité

Le niveau de détail publié dépend :

- de l’exploitation connue ;
- de la disponibilité du correctif ;
- du risque pour les clients.

---

## 52.3 Propagation

LogicGate doit identifier les projets clients affectés via l’inventaire des versions.

---

# 53. Échec de publication

## 53.1 Avant publication

Si l’échec survient avant tout push d’artefact :

- corriger ;
- relancer depuis le même tag uniquement si aucun artefact différent n’a été publié et si le processus reste traçable ;
- sinon créer une nouvelle version candidate.

---

## 53.2 Publication partielle

Si certains artefacts ont été publiés :

1. arrêter la publication ;
2. identifier les artefacts publiés ;
3. ne pas les remplacer ;
4. créer une nouvelle version ;
5. unlist ou marquer la version incomplète ;
6. documenter.

---

## 53.3 Release GitHub sans packages

La release doit être marquée comme incomplète ou retirée selon le contexte.

Une nouvelle version reste préférable à un remplacement silencieux.

---

# 54. Retrait ou unlist d’une version

## 54.1 Conditions

Une version peut être masquée si :

- artefact corrompu ;
- secret exposé ;
- vulnérabilité critique ;
- package incomplet ;
- métadonnées dangereusement erronées.

---

## 54.2 Immutabilité

Le retrait ne permet pas de republier un contenu différent sous le même numéro.

---

## 54.3 Projet existant

Un projet utilisant déjà la version doit recevoir :

- alerte ;
- plan d’upgrade ;
- mitigation ;
- version de remplacement.

---

## 54.4 Suppression complète

La suppression complète d’un package doit être évitée lorsque des projets supportés en dépendent.

---

# 55. Dépréciation

## 55.1 Annonce

Une dépréciation doit apparaître dans :

- changelog ;
- notes de release ;
- diagnostics ;
- documentation ;
- éventuellement sortie `upgrade check`.

---

## 55.2 Informations

```text
Composant
Version de dépréciation
Remplacement
Date ou version de retrait
Migration
```

---

## 55.3 Retrait

Le retrait effectif nécessite généralement une version majeure.

Avant `1.0`, un changement important reste possible dans une version mineure, mais doit être explicitement signalé.

---

# 56. Fin de support

La release doit pouvoir préciser :

```text
Supported
Maintenance
Deprecated
Unsupported
```

---

## 56.1 Version unsupported

Une version unsupported ne reçoit plus de correction standard.

Un correctif exceptionnel peut nécessiter :

- upgrade ;
- devis ;
- branche spécifique ;
- analyse dédiée.

---

# 57. Downgrade

## 57.1 Principe

Le downgrade d’un composant n’est pas automatiquement supporté.

---

## 57.2 Code uniquement

Un retour applicatif est possible si les données et contrats restent compatibles.

---

## 57.3 Données modifiées

Si une migration incompatible a été appliquée, le downgrade peut nécessiter une restauration.

---

## 57.4 CLI

Un ancien CLI peut ne pas lire un lock file ou une configuration plus récente.

Il doit refuser proprement au lieu de modifier le projet.

---

# 58. Release du projet client

## 58.1 Cycle

```text
Modification ou upgrade
→ branche
→ CI
→ recette
→ version client
→ tag
→ image
→ staging
→ production
```

---

## 58.2 Changelog client

Le changelog client contient :

- corrections ;
- nouvelles fonctionnalités ;
- upgrades SiteFactory ;
- migrations ;
- sécurité ;
- changements opérationnels.

---

## 58.3 Notes de déploiement

Elles précisent :

```text
Version source
Version cible
Migration
Backup
Configuration
Smoke tests
Rollback
```

---

## 58.4 Indépendance

La publication de SiteFactory ne déclenche pas automatiquement une nouvelle release client.

---

# 59. Versionnement du contenu

Le contenu éditorial ne nécessite pas toujours une nouvelle version applicative complète.

Toutefois, une modification publiée doit rester traçable dans :

- base ;
- audit ;
- repository si contenu versionné ;
- historique de publication.

---

## 59.1 Changement structurel

Une modification de page, navigation ou composant peut produire une nouvelle version applicative.

---

## 59.2 Simple correction éditoriale

Peut utiliser l’historique du module Content sans nouvelle release technique, selon l’exploitation.

---

# 60. Versionnement des migrations

## 60.1 Nom

Une migration doit posséder un nom explicite.

Exemple :

```text
202607281030_AddPortfolioAdvanced
```

---

## 60.2 Release associée

Les notes de release client doivent indiquer les migrations incluses.

---

## 60.3 Migration absente

Une release modifiant le modèle EF Core sans migration doit être bloquée.

---

# 61. Versionnement des prompts LLM

Les prompts possèdent :

```text
PromptId
PromptVersion
TaskVersion
OutputSchemaVersion
```

---

## 61.1 Modification compatible

Correction ou précision sans changement de format :

```text
PATCH
```

---

## 61.2 Nouveau champ facultatif

```text
MINOR
```

---

## 61.3 Changement du contrat de sortie

```text
MAJOR
```

---

## 61.4 Modèle utilisé

Une génération doit également enregistrer l’identifiant du modèle utilisé lorsque disponible.

---

# 62. Documentation versionnée

Chaque release doit publier la documentation correspondant à la version.

Une documentation décrivant uniquement la dernière version peut être insuffisante pour maintenir un projet ancien.

---

## 62.1 Documentation minimale conservée

- notes de release ;
- guides de migration ;
- schémas ;
- CLI ;
- compatibilité ;
- runbooks structurants.

---

# 63. Versions dans les logs et rapports

Les éléments suivants doivent inclure les versions pertinentes :

```text
generation-report.json
upgrade-report.json
deployment-report.json
backup-manifest.json
restore-drill-report.json
application startup logs
```

---

# 64. Inventaire des releases

LogicGate doit pouvoir conserver un inventaire sans secret :

```text
Version
Canal
Tag
Commit
Date
Composants
Artefacts
Checksums
Statut de support
Vulnérabilités connues
```

---

# 65. Convention de statut d’une release

Statuts possibles :

```text
draft
preview
stable
deprecated
withdrawn
unsupported
```

---

## 65.1 `withdrawn`

Utilisé pour une version retirée en raison d’un problème important.

Elle reste identifiable dans l’historique.

---

# 66. Tests automatisés du versionnement

Tester notamment :

- parsing SemVer ;
- comparaison ;
- pré-release ;
- build metadata ;
- version invalide ;
- version source/tag incohérente ;
- versions flottantes ;
- plages de compatibilité ;
- version déjà publiée.

---

# 67. Tests du CLI

Vérifier :

```text
--version
version --json
version preview
commit metadata
absence de donnée sensible
```

---

# 68. Tests des manifests

Tester :

- schéma valide ;
- composant dupliqué ;
- version invalide ;
- artefact absent ;
- checksum absent ;
- commit invalide ;
- canal incohérent ;
- date UTC ;
- données interdites.

---

# 69. Tests de pack

Vérifier :

- package créé ;
- version correcte ;
- fichiers attendus ;
- dépendances ;
- métadonnées ;
- absence de secret ;
- installation dans un feed temporaire ;
- utilisation réelle.

---

# 70. Tests de publication

La publication doit être testée vers :

- feed temporaire ;
- namespace de test ;
- package preview.

Elle ne doit pas utiliser le feed stable pour tester le workflow.

---

# 71. Tests d’immutabilité

Vérifier qu’une version déjà publiée ne peut pas être remplacée.

Le workflow doit échouer avant toute tentative ambiguë.

---

# 72. Tests de compatibilité

Vérifier :

- générateur compatible ;
- template incompatible ;
- module trop récent ;
- schéma ancien supporté ;
- schéma futur refusé ;
- upgrade intermédiaire requis ;
- version dépréciée ;
- version unsupported.

---

# 73. Tests de release client

Tester :

- tag client ;
- version applicative ;
- image ;
- lock file ;
- migration ;
- changelog ;
- deployment report ;
- rollback.

---

# 74. Quality gates

Une release ne peut pas être publiée si :

```text
Version invalide

Version déjà publiée

Tag incohérent

Commit non identifié

Working tree non propre

Changelog incomplet

Migration non documentée

Compatibilité invalide

Build échoué

Tests échoués

Scans échoués

Projet généré invalide

Package invalide

Artefact manquant

Checksum absent

Manifest invalide

Secret détecté

Provenance insuffisante
```

---

# 75. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-RELEASE-001` | Version invalide |
| `SF-RELEASE-002` | Tag invalide |
| `SF-RELEASE-003` | Version et tag incohérents |
| `SF-RELEASE-004` | Version déjà publiée |
| `SF-RELEASE-005` | Commit non autorisé |
| `SF-RELEASE-006` | Changelog incomplet |
| `SF-RELEASE-007` | Migration non documentée |
| `SF-RELEASE-008` | Compatibilité invalide |
| `SF-RELEASE-009` | Build échoué |
| `SF-RELEASE-010` | Tests échoués |
| `SF-RELEASE-011` | Contrôle de sécurité échoué |
| `SF-RELEASE-012` | Package invalide |
| `SF-RELEASE-013` | Artefact absent |
| `SF-RELEASE-014` | Checksum absent ou invalide |
| `SF-RELEASE-015` | Manifest invalide |
| `SF-RELEASE-016` | Publication partielle |
| `SF-RELEASE-017` | Canal invalide |
| `SF-RELEASE-018` | Version retirée |
| `SF-RELEASE-019` | Version unsupported |
| `SF-VERSION-001` | Version flottante interdite |
| `SF-VERSION-002` | Composants incompatibles |
| `SF-VERSION-003` | Upgrade intermédiaire requis |

---

# 76. Critères d’acceptation du MVP

La stratégie sera considérée comme opérationnelle lorsque :

1. SiteFactory utilise SemVer ;
2. les versions préliminaires sont définies ;
3. les canaux internal, preview et stable sont distingués ;
4. la version principale possède une source de vérité ;
5. le CLI expose sa version ;
6. le générateur expose sa version ;
7. les schémas possèdent une version ;
8. le catalogue possède une version ;
9. les modules possèdent une version ;
10. les templates possèdent une version ;
11. les thèmes possèdent une version ;
12. le profil de déploiement possède une version ;
13. les projets clients possèdent une version propre ;
14. le lock file conserve toutes les versions ;
15. une matrice de compatibilité existe ;
16. les tags Git sont standardisés ;
17. les tags publiés sont immuables ;
18. le changelog est obligatoire ;
19. les notes de release sont produites ;
20. les migrations sont documentées ;
21. un release manifest est produit ;
22. les artefacts sont nommés de manière stable ;
23. les artefacts possèdent un checksum SHA-256 ;
24. le CLI est distribuable comme outil .NET ;
25. les packages NuGet sont inspectés ;
26. les versions existantes ne sont jamais remplacées ;
27. les releases preview sont séparées des stables ;
28. la CI de release utilise un environnement protégé ;
29. une release stable nécessite les quality gates complets ;
30. le projet généré de référence est validé ;
31. le CLI publié est testé depuis le feed ;
32. un hotfix produit une nouvelle version patch ;
33. une publication partielle produit une nouvelle version ;
34. une version peut être dépréciée ou retirée sans être remplacée ;
35. une release SiteFactory ne déploie pas automatiquement les clients.

---

# 77. Décisions validées

La baseline de release et versionnement est la suivante :

```text
SiteFactory utilise SemVer.

Les versions publiées sont immuables.

Les versions flottantes sont interdites.

Chaque release correspond à un commit et un tag exacts.

Les artefacts sont construits depuis le commit tagué.

Le CLI, le générateur, les modules, les templates,
les thèmes et les profils possèdent des versions explicites.

Les composants peuvent évoluer indépendamment.

Une release coordonnée peut regrouper plusieurs versions.

Le lock file conserve la composition exacte du projet client.

Le projet client possède sa propre version applicative.

Les versions preview utilisent alpha, beta et rc.

Les canaux preview et stable restent distincts.

Les tags stables utilisent initialement le format v{version}.

Les tags publiés ne sont jamais déplacés.

La version principale possède une source de vérité centralisée.

Le CLI expose sa version et son commit.

Les schémas structurants possèdent une schemaVersion.

Une modification de format incompatible change la schemaVersion.

Les catalogues sont versionnés.

Un changement de prix produit une nouvelle version de catalogue
sans modifier les contrats existants.

Les packages NuGet utilisent des versions exactes.

Une version de package existante n’est jamais republiée
avec un contenu différent.

Le changelog est obligatoire.

Les breaking changes et migrations sont documentés.

Chaque release produit un manifest.

Chaque artefact possède un checksum SHA-256.

La SBOM est introduite progressivement
et devient une cible pour les releases stables.

Le workflow de release utilise un environnement protégé.

Une pull request ne publie jamais une version stable.

La publication stable nécessite les quality gates complets.

Le CLI réellement publié est testé après publication.

Une publication partielle ne réutilise pas le même numéro.

Un hotfix produit une nouvelle version patch.

Une version problématique peut être retirée ou unlisted,
mais jamais remplacée.

Une release SiteFactory ne déclenche pas automatiquement
une release ou un déploiement client.

Codex ne peut pas choisir seul une version,
créer un tag stable ou publier un artefact.
```

---

# 78. Règles pour Codex

## 78.1 Lecture préalable

Avant toute modification liée aux releases ou versions, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../03-generation/13-generation-pipeline.md
../02-architecture/core-module-boundary.md
09-module-architecture.md
13-generation-pipeline.md
15-cli-specification.md
18-security-baseline.md
19-testing-strategy.md
20-quality-gates-and-ci.md
22-deployment-and-environments.md
25-upgrade-and-maintenance-strategy.md
26-release-and-versioning.md
AGENTS.md
```

Ainsi que :

- fichiers de version ;
- `.csproj` ;
- manifests ;
- changelog ;
- workflows ;
- packages ;
- tags existants ;
- matrices de compatibilité ;
- release précédente.

---

## 78.2 Ce que Codex peut faire

Codex peut :

- proposer une version ;
- mettre à jour la source de version après validation ;
- préparer un changelog ;
- préparer des notes de release ;
- créer un release manifest ;
- ajouter des checksums ;
- configurer un pack ;
- ajouter des métadonnées NuGet ;
- ajouter des tests ;
- corriger un workflow ;
- préparer une pull request de release ;
- documenter une migration ;
- préparer un hotfix.

---

## 78.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- choisir seul la version finale ;
- créer ou pousser un tag stable ;
- publier un package ;
- republier une version existante ;
- déplacer un tag ;
- remplacer un artefact ;
- ignorer une incompatibilité ;
- publier depuis une pull request ;
- exposer un token de feed ;
- utiliser une version flottante ;
- marquer une preview comme stable ;
- publier un changement incompatible comme patch ;
- retirer une version sans validation ;
- supprimer un changelog ;
- modifier un checksum après publication ;
- déclarer une release réussie sans tester le package publié ;
- déployer un projet client à la suite d’une release SiteFactory.

---

## 78.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- numéro de version ;
- canal de publication ;
- création de tag ;
- publication stable ;
- publication preview ;
- breaking change ;
- release candidate ;
- retrait d’une version ;
- unlist d’un package ;
- changement de feed ;
- changement de stratégie Git ;
- signature d’artefacts ;
- politique de support ;
- release d’un correctif de sécurité ;
- publication partielle ;
- modification d’une matrice de compatibilité.

---

## 78.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Version proposée
Justification SemVer
Canal
Composants affectés
Versions des composants
Tag proposé
Changelog modifié
Breaking changes
Migrations
Compatibilité
Packages produits
Artefacts produits
Checksums
SBOM
Quality gates
Tests Windows
Tests Linux
Projet généré
Scans
Publication effectuée ou non
Risques
Décisions humaines restantes
```

Codex ne doit pas déclarer une release publiée s’il a uniquement préparé les fichiers de release.

---

# 79. Anti-patterns interdits

## 79.1 Version flottante

```xml
<PackageReference
    Include="LogicGate.SiteFactory.Platform"
    Version="*" />
```

---

## 79.2 Tag déplacé

```text
v0.6.0
```

est repointé vers un nouveau commit.

---

## 79.3 Package republié

Le contenu de :

```text
LogicGate.SiteFactory.Cli 0.6.0
```

change après publication.

---

## 79.4 Version dupliquée

La version est différente dans :

- tag ;
- package ;
- manifest ;
- CLI.

---

## 79.5 Release sans changelog

La version est publiée sans description des changements.

---

## 79.6 Breaking change en patch

Une API est supprimée dans :

```text
1.4.1 → 1.4.2
```

---

## 79.7 Preview présentée comme stable

Une RC est sélectionnée automatiquement par les projets clients.

---

## 79.8 Build local publié

Un package construit sur un poste local est envoyé manuellement dans le feed stable.

---

## 79.9 Rebuild après scan

Les artefacts scannés sont remplacés par de nouveaux artefacts reconstruits avant publication.

---

## 79.10 Tag `latest` comme référence de production

Aucune version ou digest n’est conservé.

---

## 79.11 Publication partielle réparée silencieusement

Les artefacts manquants sont ajoutés plus tard sous la même version sans rapport.

---

## 79.12 Version client égale au générateur

Le projet client change de version uniquement pour suivre SiteFactory, même sans release cliente.

---

## 79.13 Retrait sans migration

Une version utilisée par des clients est supprimée du feed sans alternative.

---

## 79.14 Checksum recalculé après modification

L’artefact est modifié puis le checksum remplacé sans nouvelle version.

---

# 80. Roadmap d’implémentation

## Phase release 1 — Version centrale

```text
source de vérité
version CLI
commit metadata
tests
```

---

## Phase release 2 — Changelog

```text
CHANGELOG.md
Unreleased
catégories
validation
```

---

## Phase release 3 — Pack NuGet

```text
CLI tool
packages
métadonnées
inspection
feed de test
```

---

## Phase release 4 — Tags et workflow

```text
tag validation
release workflow
environment protégé
preview
stable
```

---

## Phase release 5 — Manifest et checksums

```text
release manifest
SHA-256
artefacts
résumé
```

---

## Phase release 6 — Compatibilité

```text
matrice
versions de schémas
versions de modules
templates
thèmes
```

---

## Phase release 7 — Projet client

```text
version applicative
tags
image
changelog
deployment report
```

---

## Phase release 8 — Supply chain renforcée

```text
SBOM
signature
provenance
tags signés
packages signés
```

---

# 81. Checklist de préparation

```text
[ ] Scope défini
[ ] Version proposée
[ ] Justification SemVer
[ ] Main verte
[ ] Changelog à jour
[ ] Documentation à jour
[ ] Compatibilité à jour
[ ] Breaking changes identifiés
[ ] Migrations documentées
[ ] Dépréciations documentées
[ ] Aucun secret
[ ] Aucun défaut bloquant
```

---

# 82. Checklist de release candidate

```text
[ ] Version RC
[ ] Gel fonctionnel
[ ] Build Windows
[ ] Build Linux
[ ] Suite complète
[ ] Projet généré
[ ] Upgrade testé
[ ] Docker testé
[ ] Scans réussis
[ ] Packages inspectés
[ ] Notes de release
[ ] Limitations connues
[ ] Projet pilote validé
```

---

# 83. Checklist de publication stable

```text
[ ] Version stable validée
[ ] Tag exact
[ ] Commit exact
[ ] Changelog
[ ] Notes de release
[ ] Restore
[ ] Build Release
[ ] Tests
[ ] Projet généré
[ ] Reproductibilité
[ ] Security scans
[ ] Packages
[ ] Manifest
[ ] Checksums
[ ] SBOM si activée
[ ] Environnement protégé
[ ] Approbation humaine
[ ] Publication feed
[ ] GitHub Release
```

---

# 84. Checklist post-publication

```text
[ ] Package visible
[ ] Version correcte
[ ] CLI installable
[ ] sitefactory --version correct
[ ] Projet minimal généré
[ ] Build du projet réussi
[ ] Checksums valides
[ ] Manifest publié
[ ] Notes visibles
[ ] Inventaire mis à jour
[ ] Aucun incident de publication
```

---

# 85. Checklist hotfix

```text
[ ] Version affectée identifiée
[ ] Branche depuis la bonne version
[ ] Défaut reproduit
[ ] Test de régression
[ ] Correction minimale
[ ] Build
[ ] Tests
[ ] Scans
[ ] Version patch
[ ] Changelog Security ou Fixed
[ ] Tag
[ ] Publication
[ ] Projets clients affectés identifiés
[ ] Propagation vers main
```

---

# 86. Checklist de retrait

```text
[ ] Motif validé
[ ] Version concernée
[ ] Projets concernés
[ ] Version de remplacement
[ ] Mitigation
[ ] Communication
[ ] Unlist ou retrait contrôlé
[ ] Version jamais remplacée
[ ] Inventaire mis à jour
[ ] Post-mortem si nécessaire
```

---

# 87. Conclusion

La stratégie de release et de versionnement SiteFactory doit garantir qu’un numéro de version désigne toujours un état logiciel précis, traçable et immuable.

Elle repose sur :

```text
source de version unique
+ SemVer
+ tag exact
+ commit exact
+ quality gates
+ artefacts inspectés
+ checksums
+ manifest
+ changelog
+ compatibilité
+ publication contrôlée
```

Une release réussie doit permettre de reconstruire l’histoire complète :

```text
quel code
→ quelles versions
→ quels artefacts
→ quels contrôles
→ quelle publication
→ quels projets compatibles
```

> Une version ne doit pas seulement permettre d’installer SiteFactory. Elle doit permettre de savoir exactement ce qui a été installé, validé et publié.
