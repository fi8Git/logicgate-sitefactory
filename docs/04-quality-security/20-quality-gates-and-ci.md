# LogicGate SiteFactory — Quality gates et intégration continue

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Quality gates et intégration continue |
| Version | 1.0 |
| Statut | Proposition à valider |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Plateforme initiale | GitHub Actions |
| Runtime cible | .NET 10 |
| Formats de solution | `.slnx` |
| Plateformes principales | Windows x64, Linux x64 |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../02-architecture/core-module-boundary.md`, `13-generation-pipeline.md`, `15-cli-specification.md`, `18-security-baseline.md`, `19-testing-strategy.md` |
| Dernière mise à jour | 2026-11-09 |

## Périmètre public, exécution réelle et limites

La distribution publique comprend **12 projets de production et 11 projets de
tests**, déclarés par [SiteFactory.slnx](../../SiteFactory.slnx). Le
[workflow public réel](../../.github/workflows/ci.yml) appelle
[Invoke-PublicCi.ps1](../../scripts/public-ci/Invoke-PublicCi.ps1) et
[PublicCiProof.ps1](../../scripts/public-ci/PublicCiProof.ps1). Il ne requiert
ni source, fixture, assembly, secret ni repository Business.

Le parcours implémenté est le suivant :

| Étape | Contrôle réel |
|---|---|
| Scope et checkout | Liste fermée des sources publiques, checkout partiel/sparse, solution publique incluse, credentials non persistants, refus des entrées hors scope et des liens non sûrs |
| Isolation et composition | Copie temporaire possédée, projets et ressources évaluées, références directes/transitives publiques, absence de cycle ou de dépendance privée |
| Restore et build | SDK déclaré, cache NuGet isolé, restore `--locked-mode`, guards, format vérifié, build Release, provenance/checksum des ressources autorisées |
| Symboles et binaires | PDB requis, documents/checksums autorisés, refus des chemins et métadonnées privées, inspection des dépendances |
| Offline Windows/Linux | Discovery fermée des onze suites, identités et multiplicités contrôlées ; onze TRX par plateforme, résultats entièrement Passed |
| PostgreSQL REQUIRED Linux | Harnais public existant, cinq shards séquentiels et 23 Facts réels ; aucune preuve offline ou mock substituée |
| Preuves et agrégation | Même SHA/run/tentative, validation individuelle des TRX, compteurs, durée, archivage admissible expurgé, cleanup obligatoire ; refus des jobs, suites, shards ou résultats incomplets |

**Validation distante acquise dans le dépôt de préparation privé sur scope
public isolé** : Windows **11 suites / 2 726 Passed**, Linux **les mêmes 11 suites /
2 726 Passed**, PostgreSQL **23 Passed**, **27 TRX** au total, cleanup et
agrégation conformes. Les plateformes ne représentent pas 5 452 scénarios
fonctionnels distincts. Aucun résultat n'est attribué aux checks Business ;
les détails privés de run/commit et d'extraction restent dans le suivi privé.

PostgreSQL possède deux inventaires distincts **20 Infrastructure + 3 Hosts** :
**g04=5, g05=3, p01=8, p02=4, p03=3**. PostgreSQL **18.6/180006**, digest
approuvé, TLS **VerifyFull**, port loopback dynamique et credentials éphémères.
Budget **360 s par shard**, pas 360 s pour la campagne entière ; bornes auxiliaires
et cleanup possédé conservés. P03 garde **32 × 24 = 768 HTTP 200 / zéro 503**,
256 requêtes par route, corrélations Activity/événements readiness/Npgsql filtrés
et lifecycle complet. Toute réponse 503 fait échouer sa campagne ; aucune relance
identique ne doit masquer un échec ni expliquer rétroactivement un refus ancien.

Le workflow utilise les modes `Route`, `Checkout`, `Offline`, `PostgreSql` et
`Aggregate`. Dans le dépôt mixte, la preuve publique est manuelle et évite le
double déclenchement automatique avec la CI privée. Dans un véritable dépôt
public, les événements pull request, push sur main et workflow_dispatch sont
prévus ; leur validation dans ce nouveau dépôt reste à effectuer.
**SourceLink du futur dépôt public reste PENDING**, sans URL ni commit inventés.
La campagne acquise n'est ni une publication ni une garantie absolue de sécurité.

Les sections suivantes conservent les exigences de qualité et les architectures
cibles pour les capacités futures. Les workflows séparés, scans, releases,
packages, applications clientes et déploiements qui y sont envisagés ne doivent
pas être considérés comme tous implémentés ou validés par le workflow actuel.
Le site Documentation actuel est statique, sans solution .NET cliente générée.
Voir [CONTRIBUTING](../../CONTRIBUTING.md) et le
[contrat PostgreSQL public](../../tests/PostgreSql/README.md).

---

## 1. Objet du document

Ce document définit les règles d’intégration continue et les quality gates de LogicGate SiteFactory.

Il précise notamment :

- l’organisation des workflows GitHub Actions ;
- les événements déclencheurs ;
- les permissions accordées aux workflows ;
- les plateformes testées ;
- la restauration des dépendances ;
- le build ;
- les tests ;
- la couverture ;
- les validations de catalogues et schémas ;
- la génération des projets de référence ;
- les tests des projets générés ;
- les analyses de sécurité ;
- la construction des images Docker ;
- la production et la conservation des artefacts ;
- la protection des branches ;
- les règles de merge ;
- le processus de release ;
- la publication des packages et du CLI ;
- la gestion des secrets ;
- la gestion des dépendances ;
- les procédures de dérogation ;
- les contraintes applicables à Codex.

Ce document définit les conditions minimales nécessaires pour considérer une modification comme intégrable ou publiable.

---

## 2. Vision

L’intégration continue doit empêcher qu’une modification invalide ou dangereuse soit intégrée dans la branche principale.

Le flux de référence est :

```text
Modification
→ branche
→ pull request
→ validation CI
→ revue
→ merge
→ validation branche principale
→ release éventuelle
→ publication contrôlée
```

Pour SiteFactory, la CI doit valider deux produits distincts :

```text
Le moteur SiteFactory

ET

Les projets générés par SiteFactory
```

Une CI verte ne signifie donc pas uniquement que :

```text
SiteFactory.slnx compile
```

Elle doit également démontrer qu’une configuration de référence peut produire une application client :

- cohérente ;
- compilable ;
- testée ;
- sécurisée ;
- reproductible ;
- exploitable.

---

## 3. Objectifs

Les workflows doivent permettre de :

- détecter rapidement les erreurs ;
- empêcher les régressions ;
- valider les contrats structurants ;
- vérifier les catalogues ;
- vérifier les schémas ;
- vérifier les dépendances architecturales ;
- produire un projet client de référence ;
- compiler les projets générés ;
- exécuter leurs tests ;
- détecter les secrets ;
- détecter les dépendances vulnérables ;
- analyser le code ;
- construire les images Docker ;
- produire des rapports ;
- protéger les releases ;
- conserver une traçabilité ;
- fournir des résultats lisibles à LogicGate et Codex.

---

## 4. Non-objectifs

La CI initiale ne doit pas :

- déployer automatiquement chaque pull request en production ;
- appliquer une migration à une base de production ;
- accéder aux données d’un client ;
- utiliser des secrets de production ;
- nécessiter un VPS réel pour les tests standards ;
- nécessiter un provider LLM réel ;
- publier automatiquement un package depuis une branche de travail ;
- corriger automatiquement le code ;
- fusionner automatiquement une pull request ;
- accepter silencieusement une analyse de sécurité échouée ;
- remplacer la recette humaine ;
- remplacer les runbooks de production.

---

# 5. Principes fondamentaux

## 5.1 La CI est obligatoire

Toute modification destinée à la branche principale doit passer par les quality gates applicables.

Les modifications concernées incluent :

- code ;
- tests ;
- catalogues ;
- schémas ;
- templates ;
- thèmes ;
- modules ;
- prompts ;
- scripts ;
- workflows ;
- Docker ;
- documentation structurante.

---

## 5.2 Un quality gate échoué bloque l’intégration

Un contrôle bloquant ne doit pas être rendu non bloquant uniquement pour faciliter un merge.

Exemples de gates bloquants :

```text
Restore échoué
Build échoué
Test obligatoire échoué
Catalogue invalide
Schéma invalide
Secret détecté
Vulnérabilité critique non traitée
Analyse statique bloquante
Projet généré invalide
```

---

## 5.3 Les workflows suivent le privilège minimal

Chaque workflow doit déclarer explicitement ses permissions.

Valeur de base :

```yaml
permissions:
  contents: read
```

Les permissions d’écriture sont accordées uniquement aux jobs qui en ont réellement besoin.

---

## 5.4 Les secrets de production ne sont pas disponibles dans les pull requests

Les workflows exécutant du code provenant d’une pull request ne doivent pas recevoir :

- token de production ;
- credentials VPS ;
- mot de passe PostgreSQL de production ;
- secret SMTP réel ;
- clé privée ;
- clé API LLM réelle ;
- token de publication à portée excessive.

---

## 5.5 Les actions sont maîtrisées

Toute action GitHub utilisée doit être :

- identifiée ;
- approuvée ;
- maintenue ;
- compatible avec les runners utilisés ;
- limitée aux permissions nécessaires ;
- versionnée de manière contrôlée.

Pour les workflows sensibles, l’épinglage sur une référence immuable est recommandé.

Les alertes de dépréciation des runtimes internes des actions doivent être traitées avant qu’elles ne rendent les workflows incompatibles.

---

## 5.6 La branche principale reste publiable

Après un merge réussi, la branche principale doit rester :

- compilable ;
- testée ;
- génératrice d’un projet valide ;
- sans secret ;
- sans vulnérabilité bloquante connue.

---

# 6. Architecture des workflows

Structure cible :

```text
.github/
├── workflows/
│   ├── ci.yml
│   ├── generated-project.yml
│   ├── security.yml
│   ├── nightly.yml
│   ├── release.yml
│   └── dependency-review.yml
│
├── actions/
│   └── setup-sitefactory/
│
└── dependabot.yml
```

Tous les workflows ne doivent pas nécessairement être créés dès le bootstrap.

L’introduction doit suivre les besoins réels du projet.

---

# 7. Workflow `ci.yml`

## 7.1 Rôle

`ci.yml` constitue le workflow principal de pull request et de branche principale.

Le parcours actuel est détaillé dans le tableau ci-dessus. La baseline cible
suivante conserve aussi les gates à appliquer lorsqu'ils sont implémentés :

```text
Checkout
Setup .NET
Restore
Build Release
Tests rapides
Tests de contrats
Tests d’architecture
Validation des catalogues
Validation des schémas
Secret scan
Analyse des dépendances
Analyse statique
```

Il peut appeler un workflow réutilisable pour la génération du projet de référence.

---

## 7.2 Déclencheurs

Déclencheurs recommandés :

```yaml
on:
  pull_request:
  push:
    branches:
      - main
  workflow_dispatch:
```

Les filtres de chemins peuvent être ajoutés après stabilisation.

Ils ne doivent pas permettre d’éviter un contrôle nécessaire.

---

## 7.3 Pull request

Sur pull request, le workflow doit vérifier :

- la branche proposée ;
- les différences ;
- les projets affectés ;
- les contrats ;
- la sécurité ;
- le projet généré lorsque nécessaire.

---

## 7.4 Branche principale

Sur `main`, le workflow doit exécuter au minimum la même baseline que la pull request.

Il peut ajouter :

- build Docker ;
- tests d’intégration plus larges ;
- génération de rapports ;
- artefacts de validation.

---

# 8. Workflow `generated-project.yml`

## 8.1 Rôle

Ce workflow valide la capacité réelle de SiteFactory à produire une application cliente.

Flux :

```text
Build du CLI
→ génération d’un projet exemple
→ validation des fichiers
→ restore du projet généré
→ build Release
→ tests
→ contrôles de sécurité
```

---

## 8.2 Projet de référence principal

Référence publique : **DocumentationModule**, template **documentation-minimal**,
[fixture publique valide](../../tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml).
Le CLI public produit un site HTML statique ; aucun module applicatif privé ni
projet .NET client n'est fourni par cette composition. Le workflow séparé de
cette section est une architecture cible, pas un workflow supplémentaire livré.

---

## 8.3 Projet de référence enrichi

Un second scénario doit être ajouté lorsque les contributions publiques
correspondantes sont disponibles. Il emploie des fixtures génériques explicites,
sans catalogue commercial privé. Les véritables scénarios Business restent
dans leur suite privée ; aucun résultat de cette suite n'est inféré ici.

---

## 8.4 Vérifications

Le workflow doit vérifier :

```text
Configuration valide

Plan exécutable

Génération réussie

Lock file valide

Rapport valide

Structure de solution attendue

Restore réussi

Build Release réussi

Tests réussis

Aucun secret

Aucun placeholder bloquant

Docker généré

CI générée

Documentation générée
```

---

# 9. Workflow `security.yml`

## 9.1 Rôle

Le workflow de sécurité regroupe les analyses pouvant être séparées du build fonctionnel.

Exemples :

```text
Secret scanning approfondi
Analyse statique
Analyse des dépendances
Analyse de l’image Docker
Validation des workflows
Validation de la supply chain
```

---

## 9.2 Déclencheurs

```text
Pull request
Push sur main
Exécution planifiée
Exécution manuelle
```

Certaines analyses plus lourdes peuvent être exécutées uniquement :

- sur `main` ;
- quotidiennement ;
- ou avant une release.

---

## 9.3 Résultats

Une analyse détectant un risque bloquant doit retourner un code d’échec.

Elle ne doit pas être configurée globalement avec :

```yaml
continue-on-error: true
```

sans dérogation formelle.

---

# 10. Workflow `nightly.yml`

## 10.1 Rôle

La suite planifiée exécute les contrôles plus coûteux ou moins nécessaires à chaque pull request.

Exemples :

```text
Suite complète des tests
Tests Docker Compose
Restore drill
Tests end-to-end navigateur
Tests de performance ciblés
Scans approfondis
Matrice de compatibilité
Vérification des dépendances
```

---

## 10.2 Résultat

Un échec de la suite nocturne doit :

- produire une alerte ;
- créer ou alimenter une tâche ;
- être analysé ;
- ne pas rester ignoré plusieurs jours.

---

# 11. Workflow `release.yml`

## 11.1 Rôle

Le workflow de release :

- valide la version ;
- exécute la suite complète ;
- génère les artefacts ;
- publie les packages ;
- publie éventuellement le CLI ;
- crée la release ;
- produit les checksums ;
- conserve les rapports.

---

## 11.2 Déclenchement

Le déclenchement initial recommandé est explicite :

```text
tag versionné
ou
workflow_dispatch avec version validée
```

Le simple merge sur `main` ne doit pas publier automatiquement une release stable.

---

## 11.3 Préconditions

```text
Branche principale verte
Version cohérente
Changelog à jour
Aucun changement non committé
Tests Windows réussis
Tests Linux réussis
Projet généré valide
Scans réussis
Packages construits
```

---

# 12. Workflow `dependency-review.yml`

## 12.1 Rôle

Analyser les nouvelles dépendances introduites dans une pull request.

Il doit signaler notamment :

- nouvelle dépendance directe ;
- modification importante d’une dépendance transitive ;
- vulnérabilité ;
- licence interdite ;
- source inconnue.

---

## 12.2 Gate

Une dépendance présentant un risque non accepté bloque le merge.

---

# 13. Nommage des jobs et checks

Les noms doivent être stables et explicites.

Exemples :

```text
Build / Linux
Build / Windows
Tests / Unit
Tests / Integration
Contracts / Catalogs
Contracts / Schemas
Architecture / Rules
Generated site / Documentation
Security / Secrets
Security / Dependencies
Security / Static analysis
Docker / Build
```

Les noms stables facilitent :

- la protection de branche ;
- le diagnostic ;
- l’automatisation ;
- le suivi des regressions.

---

# 14. Matrice de plateformes

## 14.1 Baseline

Les plateformes obligatoires sont :

```text
ubuntu-latest
windows-latest
```

Les libellés exacts utilisés doivent être validés et maintenus dans les workflows.

---

## 14.2 Linux

Linux constitue la plateforme principale pour :

- Docker ;
- déploiement VPS ;
- scripts shell ;
- tests Compose ;
- génération en environnement serveur.

---

## 14.3 Windows

Windows doit vérifier :

- développement local principal ;
- chemins ;
- `.slnx` ;
- Git ;
- génération ;
- exécution du CLI ;
- scripts PowerShell éventuels.

---

## 14.4 macOS

macOS reste optionnel.

Il ne doit pas être ajouté à la matrice sans besoin ou contribution réelle.

---

# 15. Version .NET

## 15.1 Version cible

La version initiale supportée est :

```text
.NET 10
```

---

## 15.2 `global.json`

Le SDK est défini dans :

```text
global.json
```

Le workflow doit respecter ce fichier.

---

## 15.3 Version absente

Le workflow doit échouer clairement si la version requise ne peut pas être installée.

---

## 15.4 Matrice .NET

Le MVP ne teste pas plusieurs versions de .NET.

Une matrice multi-version n’est introduite qu’après une décision de support explicite.

---

# 16. Restore

## 16.1 Commande de référence

```bash
dotnet restore SiteFactory.slnx --locked-mode
```

Le workflow doit utiliser le vrai fichier `.slnx`.

Il ne doit pas supposer l’existence d’un ancien fichier :

```text
SiteFactory.sln
```

---

## 16.2 Sources NuGet

Les sources doivent être :

- explicites ;
- approuvées ;
- cohérentes avec le package source mapping éventuel ;
- configurées sans exposer les credentials.

---

## 16.3 Credentials

Les credentials de feed privé utilisent les mécanismes sécurisés de la CI.

Ils ne sont pas écrits dans :

- `NuGet.Config` versionné ;
- logs ;
- arguments visibles ;
- artefacts.

---

## 16.4 Échec

Un restore échoué bloque le workflow.

---

# 17. Central Package Management

Les versions sont centralisées dans :

```text
Directory.Packages.props
```

Les workflows doivent vérifier :

- absence de version flottante ;
- cohérence des versions ;
- absence de duplication inutile ;
- sources autorisées.

---

## 17.1 Lock des dépendances

L’utilisation de fichiers :

```text
packages.lock.json
```

peut être retenue pour améliorer la reproductibilité.

Si cette stratégie est adoptée :

- les fichiers sont versionnés ;
- la CI utilise un mode verrouillé ;
- toute différence doit être explicite.

Cette décision peut faire l’objet d’une ADR.

---

# 18. Build

## 18.1 Commande

```bash
dotnet build SiteFactory.slnx \
  --configuration Release \
  --no-restore
```

---

## 18.2 Configuration

La CI utilise :

```text
Release
```

La configuration Debug ne constitue pas une preuve de release.

---

## 18.3 Warnings

Objectif :

```text
0 warning
0 erreur
```

Les warnings du compilateur et des analyseurs peuvent être traités comme erreurs selon la configuration du repository.

---

## 18.4 Warning externe

Un warning provenant d’un outil externe ou d’une action doit être :

- analysé ;
- suivi ;
- corrigé avant qu’il ne devienne une incompatibilité.

Il ne doit pas être ignoré uniquement parce que le build est encore vert.

---

# 19. Formatage et conventions

## 19.1 `.editorconfig`

La CI vérifie les conventions définies dans :

```text
.editorconfig
```

---

## 19.2 Formatage

Une commande de vérification peut être utilisée pour détecter les différences de formatage sans modifier les fichiers.

Le workflow ne doit pas committer automatiquement une correction dans la branche du contributeur.

---

## 19.3 Markdown

La documentation peut être vérifiée avec un linter Markdown configuré pour les conventions LogicGate.

Les règles doivent rester adaptées aux documents techniques longs.

---

## 19.4 YAML et JSON

Les workflows, catalogues et schémas doivent être validés syntaxiquement.

---

# 20. Tests automatisés

## 20.1 Commande de référence

```bash
dotnet test SiteFactory.slnx \
  --configuration Release \
  --no-build \
  --no-restore \
  --filter "Category!=PostgreSql"
```

---

## 20.2 Rapports

La CI doit produire :

- résultat des tests ;
- durée ;
- nombre de tests ;
- nombre de tests ignorés ;
- fichiers TRX ou format équivalent ;
- couverture lorsque activée.

---

## 20.3 Tests ignorés

Les tests ignorés doivent apparaître dans le résumé.

Un test critique ignoré peut bloquer une release.

---

## 20.4 Catégories

Les catégories peuvent permettre de séparer :

```text
Unit
Contract
Architecture
Integration
PostgreSql
Security
GeneratedProject
Docker
EndToEnd
Performance
```

---

# 21. Tests rapides de pull request

La suite rapide doit inclure au minimum :

```text
Tests Domain
Tests Application
Tests Catalog
Tests Schemas
Tests Architecture
Tests CLI essentiels
Tests Generation essentiels
```

Elle doit fournir un retour suffisamment rapide pour favoriser des pull requests petites.

---

# 22. Tests d’intégration

Les tests d’intégration doivent couvrir selon les changements :

- système de fichiers ;
- processus externes ;
- Git ;
- HTTP ;
- ASP.NET Core Identity ;
- PostgreSQL ;
- génération ;
- modules.

---

## 22.1 PostgreSQL

Une instance PostgreSQL isolée est utilisée pour les tests concernés.

Elle ne doit pas partager une base entre jobs concurrents.

---

## 22.2 Service containers

La CI peut utiliser un service PostgreSQL ou un conteneur créé par les tests.

La méthode choisie doit être :

- reproductible ;
- isolée ;
- nettoyée ;
- documentée.

---

# 23. Tests d’architecture

Les règles structurelles sont bloquantes.

Exemples :

```text
Domain ne référence pas Infrastructure.

Application ne référence pas CLI.

Les modules ne référencent pas un projet client.

Les migrations appartiennent au projet client.

Aucun package ne possède une version flottante.

Aucune route mutable n’utilise GET.

Aucun namespace client n’existe dans le code générique.
```

---

# 24. Validation des catalogues

La CI doit valider notamment :

- schéma ;
- identifiants ;
- prix ;
- statuts ;
- dépendances ;
- cycles ;
- compatibilités ;
- références.

Les montants verrouillés doivent être couverts par des tests.

Les fixtures publiques synthétiques doivent posséder des valeurs et oracles
stables, sans recopier une offre commerciale privée. Toute modification d'un
résultat attendu exige une revue explicite. Les prix des véritables catalogues
commerciaux sont vérifiés dans leur périmètre privé, pas dans la CI publique.

---

# 25. Validation des schémas

La CI valide :

```text
client configuration
catalog
module manifest
template manifest
theme manifest
generation plan
generation report
lock file
```

Elle doit vérifier :

- exemples valides ;
- exemples invalides ;
- versions supportées ;
- propriétés inconnues ;
- compatibilité documentaire.

---

# 26. Validation de la documentation

Les éléments suivants doivent être testés lorsque possible :

- liens internes ;
- chemins de fichiers ;
- exemples YAML ;
- exemples JSON ;
- identifiants de modules ;
- noms de documents ;
- références aux commandes CLI.

Un exemple présenté comme valide doit rester valide.

---

# 27. Génération d’un projet de référence

## 27.1 Invocation

Le workflow utilise le CLI réellement construit.

Invocation publique actuelle, après build, pour une cible absente :

```powershell
dotnet run --project src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj --configuration Release --no-build --no-restore -- generate --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml --output ./artifacts/documentation-site
```

Elle ne déclenche ni restore/build/test, ni déploiement. Les champs Git et
déploiement de la configuration ne déclenchent pas ces effets.

---

## 27.2 Pas de raccourci interne

Le workflow ne doit pas appeler directement une méthode privée du générateur pour contourner le CLI lorsqu’il prétend tester le parcours complet.

---

## 27.3 Dossier isolé

Chaque génération utilise un dossier propre au job.

---

# 28. Build du projet généré

Pour une future contribution générant réellement une solution .NET seulement :

```bash
dotnet restore GeneratedClient.slnx

dotnet build GeneratedClient.slnx \
  --configuration Release \
  --no-restore

dotnet test GeneratedClient.slnx \
  --configuration Release \
  --no-build
```

Le nom réel de la solution doit être dérivé de la configuration. Le site
documentaire statique actuel ne produit pas `GeneratedClient.slnx` ; ces commandes
ne lui sont pas applicables.

---

# 29. Contrats du projet généré

Une contribution d'application future devra déclarer les artefacts applicables.
La liste ci-dessous est une cible d'application, pas la sortie actuelle du
template documentaire :

```text
client.sitefactory.yml
sitefactory.lock.json
generation-report.json
README.md
AGENTS.md
Directory.Build.props
Directory.Packages.props
.env.example
Dockerfile
docker-compose
projets Web, Data et Customizations
projets de tests
documentation d’exploitation
```

---

# 30. Reproductibilité

## 30.1 Double génération

Le workflow de release doit pouvoir générer deux fois le même projet.

Il compare ensuite les artefacts déterministes.

---

## 30.2 Éléments normalisés

Les différences autorisées incluent :

```text
GenerationId
GeneratedAtUtc
Durées
Chemins temporaires
```

Elles doivent être exclues ou normalisées.

---

## 30.3 Échec

Une différence non expliquée dans un fichier déterministe bloque la release.

---

# 31. Non-écrasement des personnalisations

La CI doit tester :

1. génération initiale ;
2. modification d’un fichier `custom` ;
3. nouvelle génération ;
4. vérification du contenu ;
5. modification d’un fichier `protected` ;
6. tentative de génération ;
7. vérification du refus ;
8. vérification qu'aucun mécanisme de force ne contourne les stratégies ;
9. vérification que les protections critiques restent actives.

---

# 32. Tests de migrations

Les migrations doivent être testées avec PostgreSQL.

La CI doit vérifier :

- création d’une base vide ;
- modèle cohérent ;
- aucune migration manquante ;
- migration depuis une version supportée ;
- conservation des données ;
- absence d’auto-migration de production.

---

# 33. Build Docker

## 33.1 Image SiteFactory

Si le moteur est distribué sous forme de conteneur, l’image doit être construite et analysée.

---

## 33.2 Image du projet généré

Une future contribution d'application conteneurisée doit pouvoir produire une
image. Le template documentaire actuel ne fournit pas de Dockerfile.

Exemple conceptuel :

```bash
docker build \
  --tag sitefactory-generated-test:${GITHUB_SHA} \
  <GENERATED_APPLICATION_WITH_REVIEWED_DOCKERFILE>
```

---

## 33.3 Vérifications

```text
Build réussi
Image runtime
Aucun secret
Utilisateur non root lorsque prévu
Ports attendus
App_Data absent de l’image
Taille cohérente
```

---

# 34. Tests Docker Compose

Une suite dédiée peut démarrer :

```text
web
postgres
```

Puis vérifier :

- démarrage ;
- health check ;
- connexion PostgreSQL ;
- persistance ;
- arrêt propre ;
- volumes ;
- réseau ;
- absence de port PostgreSQL public.

Caddy peut être testé dans une suite système distincte.

---

# 35. Secret scanning

## 35.1 Périmètre

Le scan couvre :

- repository ;
- historique selon l’outil ;
- fichiers générés ;
- workflows ;
- scripts ;
- Docker ;
- documentation ;
- exemples ;
- artefacts avant publication.

---

## 35.2 Résultat

Une détection non autorisée bloque :

- merge ;
- release ;
- publication.

---

## 35.3 Exclusions

Une exclusion doit être :

- localisée ;
- documentée ;
- justifiée ;
- testée ;
- révisée.

---

## 35.4 Valeurs fictives

Les exemples doivent utiliser des valeurs clairement fictives.

Exemple :

```text
CHANGE_ME
example.test
not-a-real-secret
```

Ces valeurs ne doivent pas ressembler inutilement à un vrai credential.

---

# 36. Analyse des dépendances

La CI doit vérifier :

- vulnérabilités directes ;
- vulnérabilités transitives ;
- packages obsolètes lorsque pertinent ;
- package source ;
- licence selon la politique ;
- versions flottantes.

---

## 36.1 Commande .NET

Une commande adaptée au SDK retenu peut vérifier les packages vulnérables et transitifs.

Le résultat doit être interprété et archivé sans exposer les credentials des feeds.

---

## 36.2 Criticité

Baseline :

```text
Critique → blocage
Haute    → blocage sauf mitigation validée
Moyenne  → analyse et suivi
Faible   → backlog possible
```

---

# 37. Analyse statique

L’analyse statique doit rechercher notamment :

- injection ;
- path traversal ;
- commande shell ;
- SQL brut dangereux ;
- désérialisation ;
- secret ;
- autorisation manquante ;
- XSS ;
- cryptographie faible ;
- erreurs exposées ;
- vulnérabilités de configuration.

---

## 37.1 Semgrep

Semgrep peut être utilisé pour :

- règles génériques ;
- règles .NET ;
- règles personnalisées SiteFactory.

Une règle personnalisée peut notamment détecter :

```text
Database.Migrate en démarrage production

AllowAnyOrigin avec credentials

Process lancé via chaîne shell

MarkupString sur une donnée externe

port PostgreSQL public dans Compose
```

---

## 37.2 Analyseurs .NET

Les analyseurs sont activés dans le build.

Une alerte pertinente ne doit pas être désactivée globalement sans justification.

---

# 38. CodeQL

CodeQL peut compléter les autres analyses lorsqu’il est activé sur le repository.

Son activation doit être cohérente avec :

- le type de repository ;
- le plan GitHub ;
- les langages ;
- la durée de CI ;
- les autres outils.

CodeQL ne remplace pas :

- les tests ;
- Semgrep ;
- le secret scanning ;
- l’analyse des dépendances ;
- la revue.

Si CodeQL n’est pas actif, cette absence doit être documentée plutôt que présentée comme une analyse réussie.

---

# 39. Analyse des workflows

Les workflows eux-mêmes doivent être contrôlés.

Vérifications :

```text
Permissions explicites

Actions approuvées

Références maîtrisées

Pas de secret dans les arguments

Pas de pull_request_target dangereux

Pas de checkout de code non fiable avant exposition d’un secret

Pas de script shell concaténé avec une entrée non fiable
```

---

# 40. Analyse de l’image Docker

Une analyse d’image peut vérifier :

- vulnérabilités système ;
- packages ;
- configuration ;
- utilisateur ;
- secrets ;
- permissions ;
- taille ;
- provenance.

Une vulnérabilité bloquante dans l’image empêche la release concernée.

---

# 41. SBOM

Une Software Bill of Materials peut être générée lors d’une release.

Elle peut décrire :

- packages NuGet ;
- composants système ;
- image Docker ;
- versions.

La SBOM ne doit pas contenir :

- secret ;
- configuration client ;
- chemin privé inutile ;
- donnée personnelle.

---

# 42. Provenance des artefacts

Les artefacts de release doivent être reliés à :

- commit ;
- tag ;
- workflow ;
- version ;
- plateforme ;
- checksums.

Une provenance renforcée peut être ajoutée progressivement.

---

# 43. Artefacts CI

## 43.1 Artefacts autorisés

```text
Rapports de tests
Rapports de couverture
Rapports de scans
Projet généré de démonstration
Packages NuGet non publiés
CLI compilé
SBOM
Logs de diagnostic limités
```

---

## 43.2 Artefacts interdits

```text
.env réel
Secret
Token
Dump client
Base PostgreSQL
Clé Data Protection
Backup client
Contenu confidentiel
Configuration production réelle
```

---

## 43.3 Rétention

La durée de conservation doit être proportionnée.

Exemples conceptuels :

```text
PR : courte durée
Main : durée moyenne
Release : durée longue ou attachée à la release
```

La valeur exacte doit être définie dans les workflows.

---

# 44. Rapports

Les workflows doivent publier des résumés lisibles.

Exemple :

```text
Build              Success
Tests              312 passed
Skipped            0
Coverage            84.3 %
Catalogs            Valid
Schemas             Valid
Generated project   Success
Secrets             None
Dependencies        No blocking vulnerability
Static analysis     Success
Docker              Success
```

---

## 44.1 Résumé GitHub

Le job summary peut regrouper les informations utiles.

Il ne doit pas contenir de secret ni de données client.

---

## 44.2 Annotations

Les erreurs peuvent être annotées sur les fichiers concernés lorsque les outils le permettent.

---

# 45. Couverture

## 45.1 Production du rapport

La CI produit un rapport de couverture pour les projets concernés.

---

## 45.2 Seuils progressifs

Les seuils sont introduits après mesure d’une baseline réelle.

Objectifs proposés :

| Périmètre | Lignes | Branches |
|---|---:|---:|
| Domain | 90 % | 80 % |
| Application | 85 % | 75 % |
| Catalog | 90 % | 85 % |
| Generation | 85 % | 75 % |
| Modules critiques | 80 % | 70 % |
| CLI | 75 % | 65 % |

---

## 45.3 Gate de régression

Avant d’imposer tous les seuils absolus, la CI peut bloquer une baisse importante de couverture sur les lignes modifiées.

---

## 45.4 Interdiction

Il est interdit d’ajouter des tests sans assertions utiles uniquement pour augmenter la couverture.

---

# 46. Cache CI

## 46.1 NuGet

Un cache NuGet peut réduire la durée du restore.

La clé doit dépendre notamment de :

- système d’exploitation ;
- fichiers de packages ;
- `global.json` ;
- configuration NuGet pertinente.

---

## 46.2 Build outputs

Le cache des sorties de build doit être utilisé avec prudence.

Une restauration propre reste nécessaire pour les releases.

---

## 46.3 Secrets

Les caches ne doivent pas contenir :

- credentials ;
- `.env` ;
- configuration réelle ;
- artefacts sensibles.

---

## 46.4 Empoisonnement de cache

Les workflows manipulant des caches doivent tenir compte du niveau de confiance de l’événement déclencheur.

---

# 47. Concurrence des workflows

## 47.1 Pull requests

Une nouvelle exécution sur la même pull request peut annuler l’exécution précédente.

Exemple conceptuel :

```yaml
concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
```

---

## 47.2 Releases

Une release ne doit pas être annulée automatiquement par une exécution plus récente sans décision explicite.

---

## 47.3 Déploiements

Les futurs déploiements de production devront utiliser une concurrence stricte par environnement.

---

# 48. Timeouts

Chaque job doit posséder un timeout raisonnable.

Exemples de jobs concernés :

- restore ;
- build ;
- tests ;
- génération ;
- Docker ;
- scans ;
- end-to-end.

Un workflow ne doit pas consommer indéfiniment un runner.

---

# 49. Annulation

Les commandes SiteFactory et les tests doivent répondre aux signaux d’annulation.

Une annulation ne doit pas être présentée comme un succès.

---

# 50. Retries

## 50.1 Interdiction du retry global

Les tests ne doivent pas être relancés automatiquement en boucle pour masquer une instabilité.

---

## 50.2 Retry ciblé

Un retry peut être autorisé pour :

- téléchargement transitoire ;
- service de test explicitement instable ;
- opération réseau contrôlée.

Il doit être :

- limité ;
- journalisé ;
- justifié.

---

# 51. Tests instables

Un test instable détecté doit :

1. être identifié ;
2. être suivi ;
3. être corrigé ;
4. ne pas devenir une exception permanente.

Un test critique ne doit pas être simplement supprimé ou ignoré.

---

# 52. Gestion des secrets GitHub

## 52.1 Secrets repository

Utilisés uniquement pour les opérations qui ne nécessitent pas une séparation par environnement.

---

## 52.2 Secrets d’environnement

Les secrets de publication ou de production doivent être associés à un environnement protégé.

Exemples futurs :

```text
nuget-release
staging
production
```

---

## 52.3 Approbation

L’environnement `production` doit pouvoir imposer une approbation humaine avant utilisation des secrets.

---

## 52.4 Minimisation

Chaque job reçoit uniquement les secrets nécessaires.

---

## 52.5 Rotation

Les secrets doivent être remplaçables sans modifier les workflows.

---

# 53. Environnements GitHub

Environnements possibles :

```text
development
staging
nuget-release
production
```

Le MVP SiteFactory peut commencer avec :

```text
nuget-release
```

pour la publication des packages.

Le déploiement client reste séparé tant que le processus n’est pas stabilisé.

---

# 54. Protection de la branche principale

## 54.1 Règles minimales

La branche principale doit exiger :

- pull request ;
- checks obligatoires réussis ;
- branche à jour selon la politique retenue ;
- conversations résolues ;
- absence de merge avec CI échouée ;
- historique propre selon la stratégie Git.

---

## 54.2 Checks obligatoires

Exemples :

```text
Build / Linux
Build / Windows
Tests / Unit
Contracts / Catalogs and schemas
Architecture / Rules
Generated site / Documentation
Security / Secrets
Security / Dependencies
```

Les noms doivent rester stables.

---

## 54.3 Repository avec un seul mainteneur

Pour un repository maintenu principalement par une seule personne :

- la pull request reste recommandée ;
- les checks automatiques restent obligatoires ;
- une approbation externe ne doit pas être imposée si elle bloque tout travail ;
- le mainteneur peut fusionner sa propre pull request après revue personnelle et CI verte ;
- les changements sensibles doivent recevoir une revue Codex dédiée ou une seconde revue humaine lorsque possible.

Les changements sensibles incluent notamment :

- sécurité ;
- migrations ;
- prix ;
- catalogues ;
- permissions ;
- déploiement ;
- release.

---

## 54.4 Push direct

Le push direct sur `main` doit être interdit ou réservé à une procédure exceptionnelle documentée.

---

# 55. Stratégie de merge

Approches possibles :

```text
Squash merge
Rebase merge
Merge commit
```

Pour une équipe réduite, le squash merge est recommandé afin de :

- garder un historique lisible ;
- regrouper les corrections de revue ;
- produire un commit par tâche ou fonctionnalité.

Le titre final du commit doit être explicite.

---

# 56. Convention de branches

Format recommandé :

```text
feature/...
fix/...
security/...
docs/...
refactor/...
release/...
```

Exemples :

```text
feature/config-validation
fix/generated-file-conflict
security/harden-admin-cookie
docs/testing-strategy
```

---

# 57. Pull request

Chaque pull request doit préciser :

```text
Objectif
Périmètre
Hors périmètre
Fichiers principaux
Tests exécutés
Résultats
Risques
Migration éventuelle
Impact sécurité
Impact génération
Documentation
```

---

## 57.1 Taille

Les pull requests doivent rester suffisamment petites pour être revues.

Une refonte importante doit être divisée en étapes lorsque cela est possible.

---

## 57.2 Draft

Une pull request incomplète peut être ouverte en draft.

Elle ne doit pas être fusionnée tant que les gates ne sont pas satisfaits.

---

# 58. CODEOWNERS

Un fichier `CODEOWNERS` peut être ajouté lorsque plusieurs mainteneurs ou domaines apparaissent.

Il peut notamment distinguer :

```text
architecture
security
catalog
templates
modules
workflows
```

Il n’est pas nécessaire dans le premier bootstrap avec un seul mainteneur.

---

# 59. Publication des packages

## 59.1 Packages concernés

Exemples futurs :

```text
LogicGate.SiteFactory.Platform
LogicGate.SiteFactory.DesignSystem
LogicGate.SiteFactory.Module.Leads
LogicGate.SiteFactory.Module.Content
LogicGate.SiteFactory.Cli
```

---

## 59.2 Source

Les packages sont publiés dans un feed privé contrôlé par LogicGate.

---

## 59.3 Conditions

Un package ne peut être publié que si :

```text
Version valide
Tag valide
Build Release réussi
Tests réussis
Scans réussis
Package construit
Contenu inspecté
Aucun secret
```

---

## 59.4 Immutabilité

Une version publiée n’est jamais remplacée silencieusement.

Une correction produit une nouvelle version.

---

## 59.5 Publication depuis une pull request

La publication depuis une pull request est interdite.

---

# 60. Publication du CLI

Le CLI peut être publié comme outil .NET.

La release doit produire :

- package ;
- version ;
- checksum éventuel ;
- notes ;
- commandes d’installation ;
- compatibilités ;
- changelog.

Les pipelines et développeurs doivent utiliser une version explicite.

---

# 61. Versionnement

## 61.1 SemVer

SiteFactory, le CLI et les packages utilisent :

```text
MAJOR.MINOR.PATCH
```

---

## 61.2 Version source

La version doit provenir d’une source explicite.

Exemples :

- propriété centralisée ;
- tag ;
- outil de versionnement validé.

Il ne doit pas exister plusieurs versions contradictoires dans le repository.

---

## 61.3 Pré-release

Formats possibles :

```text
0.2.0-alpha.1
0.2.0-beta.1
0.2.0-rc.1
```

---

## 61.4 Tag

Format recommandé :

```text
v0.2.0
```

Le tag doit correspondre exactement aux artefacts produits.

---

# 62. Changelog

Une release doit mettre à jour :

```text
CHANGELOG.md
```

Le changelog doit distinguer :

```text
Added
Changed
Fixed
Security
Deprecated
Removed
```

Les breaking changes doivent être clairement identifiés.

---

# 63. Notes de release

Les notes doivent préciser :

- nouvelles fonctionnalités ;
- correctifs ;
- sécurité ;
- compatibilités ;
- migrations ;
- upgrades ;
- actions manuelles ;
- limitations ;
- versions dépréciées.

---

# 64. Release candidates

Une release candidate peut être utilisée avant une version importante.

Elle doit subir :

- tests complets ;
- génération ;
- projet pilote ;
- upgrade depuis une version supportée ;
- validation manuelle.

---

# 65. Dépendances automatisées

Un outil automatisé peut proposer des pull requests pour :

- packages NuGet ;
- actions GitHub ;
- images Docker ;
- outils de build.

---

## 65.1 Fréquence

Une fréquence hebdomadaire est adaptée au démarrage.

Les alertes de sécurité restent traitées plus rapidement.

---

## 65.2 Regroupement

Les mises à jour mineures compatibles peuvent être regroupées.

Les mises à jour majeures ou sensibles restent séparées.

---

## 65.3 Merge automatique

L’auto-merge peut être étudié pour des mises à jour à faible risque, mais il n’est pas activé dans le MVP initial.

---

# 66. Validation des actions GitHub

Les actions de workflows doivent également faire l’objet de mises à jour.

Une action utilisant un runtime déprécié doit être remplacée avant son retrait par la plateforme.

La CI ne doit pas conserver durablement des warnings de dépréciation.

---

# 67. Quality gates par contexte

## 67.1 Pull request standard

Obligatoire :

```text
Restore
Build Release
Tests unitaires
Tests de contrats
Tests d’architecture
Validation catalogues et schémas
Secret scan
Analyse des dépendances
Analyse statique
```

---

## 67.2 Modification du générateur

Ajouter :

```text
Génération du projet de référence
Reproductibilité ciblée
Non-écrasement
Build du projet généré
Tests du projet généré
```

---

## 67.3 Modification d’un module

Ajouter :

```text
Tests du module
Tests d’autorisation
Tests de données
Tests de migration
Composition dans le projet généré
```

---

## 67.4 Modification Docker ou opérations

Ajouter :

```text
Build image
Analyse image
Compose validation
Health check
Scripts de backup
```

---

## 67.5 Modification sécurité

Ajouter :

```text
Tests de contournement
Scans complets
Revue sécurité
Documentation du risque
```

---

## 67.6 Release

Ajouter :

```text
Windows
Linux
Suite complète
Deux générations
Docker
Migrations
SBOM
Packages
Changelog
Artefacts
```

---

# 68. Gates bloquants de pull request

Une pull request ne peut pas être fusionnée lorsque :

```text
Restore échoué

Build Release échoué

Warning compilateur ou analyseur bloquant

Test obligatoire échoué

Test critique ignoré

Test d’architecture échoué

Catalogue invalide

Schéma invalide

Secret détecté

Dépendance critique ou haute non traitée

Analyse statique bloquante

Projet généré invalide lorsque concerné

Documentation obligatoire absente
```

---

# 69. Gates bloquants de release

Une release ne peut pas être publiée lorsque :

```text
Branche principale non verte

Tag incohérent

Version incohérente

Changelog absent

Test Windows échoué

Test Linux échoué

Projet généré invalide

Reproductibilité échouée

Migration non validée

Secret détecté

Vulnérabilité bloquante

Analyse statique échouée

Image Docker invalide

Package incomplet

Artefact non traçable
```

---

# 70. Gates non bloquants initiaux

Certains indicateurs peuvent être non bloquants pendant leur introduction :

- couverture sous la cible ;
- performance sans baseline ;
- duplication de contenu faible ;
- warning de documentation non critique ;
- analyse visuelle future.

Ils doivent toutefois :

- être visibles ;
- être suivis ;
- posséder une date ou condition de passage en gate bloquant.

---

# 71. Dérogation

## 71.1 Conditions

Un gate bloquant ne peut être contourné que par une dérogation explicite.

La dérogation doit préciser :

```text
Gate concerné
Erreur concernée
Raison
Risque
Mesure compensatoire
Propriétaire
Date
Expiration
Plan de correction
```

---

## 71.2 Vulnérabilité

Une vulnérabilité critique ne peut pas être contournée pour publier une nouvelle release exposée sans mitigation formelle.

---

## 71.3 Traçabilité

La dérogation doit être :

- versionnée ;
- liée à une issue ;
- visible dans la pull request ou la release ;
- temporaire.

---

# 72. Bypass de protection de branche

Le bypass doit être réservé à :

- incident urgent ;
- panne de CI indépendante du code ;
- correctif critique ;
- récupération du repository.

Après bypass :

1. documenter ;
2. exécuter les checks dès que possible ;
3. vérifier la branche ;
4. corriger la cause ;
5. produire un compte-rendu.

---

# 73. Gestion d’un workflow défaillant

Lorsqu’un workflow échoue pour une raison d’infrastructure :

- distinguer l’échec produit de l’échec runner ;
- relancer de manière ciblée ;
- conserver le code non fusionné ;
- ouvrir une tâche si récurrent ;
- ne pas désactiver définitivement le gate.

---

# 74. Logs CI

Les logs doivent rester utiles sans exposer :

- secret ;
- token ;
- configuration d’environnement ;
- contenu client ;
- chaîne de connexion ;
- données personnelles.

Les commandes affichées doivent être inspectées pour éviter l’exposition d’arguments sensibles.

---

# 75. Masquage des secrets

Le mécanisme de masquage de la plateforme constitue une protection complémentaire.

Il ne justifie pas d’afficher volontairement un secret.

Les valeurs dérivées, encodées ou transformées peuvent ne pas être correctement masquées.

---

# 76. Scripts de CI

Les scripts complexes doivent être déplacés dans :

```text
scripts/public-ci/
```

plutôt que d’être entièrement écrits dans le YAML.

Avantages :

- tests locaux ;
- lisibilité ;
- versionnement ;
- réutilisation ;
- réduction des erreurs de quoting.

---

# 77. Parité locale

Les commandes utilisées en CI doivent pouvoir être exécutées localement.

Le script multiplateforme existant peut produire une preuve offline dans un
répertoire de sortie neuf :

```powershell
pwsh -NoProfile -File scripts/public-ci/Invoke-PublicCi.ps1 -Mode Offline -ProofDirectory ./artifacts/public-offline-proof
```

Pour les Facts PostgreSQL réels uniquement, avec Docker Linux disponible et une
campagne explicitement autorisée :

```powershell
pwsh -NoProfile -File tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired
```

Les commandes ne publient ni n'exportent le produit. Le premier parcours utilise
la discovery et les validateurs réels ; le second ne remplace pas les suites
offline. Un dossier de preuve existant n'est pas écrasé.

Le workflow ne doit pas contenir une logique impossible à reproduire hors GitHub.

---

# 78. Scripts multiplateformes

Lorsque les mêmes étapes doivent fonctionner sous Windows et Linux :

- privilégier un outil .NET ;
- ou fournir un script PowerShell multiplateforme ;
- ou maintenir deux scripts simples et testés.

Éviter une dépendance implicite à Bash dans un job Windows.

---

# 79. Tests des workflows

Les workflows doivent être revus sur :

- syntaxe YAML ;
- triggers ;
- permissions ;
- conditions ;
- secrets ;
- chemins ;
- noms des checks ;
- scripts ;
- expressions.

---

## 79.1 Validation locale

Une validation statique des workflows peut être ajoutée.

---

## 79.2 Branches de test

Les changements importants de workflow doivent être testés dans une pull request dédiée lorsque possible.

---

## 79.3 Publication

Un workflow de release ne doit pas être testé en publiant un vrai package stable depuis une branche de test.

Utiliser :

- dry-run ;
- feed de test ;
- package pré-release ;
- environnement isolé.

---

# 80. Performance et coûts CI

## 80.1 Priorité

Ordre de priorité :

```text
Fiabilité
→ sécurité
→ diagnostic
→ durée
→ coût
```

---

## 80.2 Optimisations autorisées

- cache NuGet ;
- séparation des jobs ;
- annulation des anciennes exécutions ;
- filtres de chemins prudents ;
- réutilisation des artefacts ;
- parallélisme ;
- tests rapides en PR ;
- suites lourdes planifiées.

---

## 80.3 Optimisations interdites

- supprimer un test critique ;
- ignorer Windows ;
- ne plus générer le projet de référence ;
- masquer une analyse ;
- utiliser un artefact ancien non vérifié ;
- éviter un restore propre sur une release.

---

# 81. Durée cible

Les objectifs initiaux doivent rester pragmatiques.

Exemple conceptuel :

```text
Feedback initial de build : quelques minutes

Pull request standard : durée suffisamment courte
pour encourager les petites itérations

Suite de release : plus complète et potentiellement plus longue
```

Les seuils chiffrés seront établis après mesure.

---

# 82. Observabilité de la CI

Indicateurs possibles :

```text
Taux de succès
Durée moyenne
Étape la plus lente
Nombre de tests
Tests ignorés
Tests instables
Couverture
Vulnérabilités
Temps de génération
Taille des artefacts
Taille de l’image
```

Ces informations peuvent guider l’amélioration du pipeline.

---

# 83. Badges

Le README peut afficher des badges sobres :

```text
Build
Tests
Security
Latest release
```

Un badge vert ne doit pas masquer l’absence d’un contrôle.

Exemple :

```text
CodeQL non configuré
```

ne doit pas être présenté comme une analyse CodeQL réussie.

---

# 84. Documentation des workflows

Chaque workflow important doit préciser :

```text
Rôle
Déclencheurs
Permissions
Secrets
Jobs
Artefacts
Gates
Procédure de relance
```

Une documentation synthétique peut être placée dans :

```text
docs/04-quality-security/ci-workflows/
```

---

# 85. Gestion des incidents de CI

Exemples :

- secret exposé dans un log ;
- package erroné publié ;
- image compromise ;
- workflow modifié de manière malveillante ;
- token à portée excessive ;
- artefact contenant des données sensibles.

Procédure :

```text
Désactiver le workflow concerné si nécessaire
→ révoquer les secrets
→ supprimer ou déprécier l’artefact
→ analyser les exécutions
→ corriger
→ réexécuter les gates
→ documenter
```

---

# 86. Critères d’acceptation du MVP

La stratégie CI sera considérée comme opérationnelle lorsque :

1. un workflow principal existe ;
2. les pull requests déclenchent la CI ;
3. `main` déclenche la CI ;
4. les permissions sont explicites ;
5. le SDK .NET 10 est configuré ;
6. la solution `.slnx` est restaurée ;
7. le build Release réussit ;
8. les tests sont exécutés ;
9. les tests ignorés sont visibles ;
10. les catalogues sont validés ;
11. les schémas sont validés ;
12. les tests d’architecture sont exécutés ;
13. un projet de référence est généré ;
14. le projet généré est compilé ;
15. les tests du projet généré sont exécutés ;
16. les stratégies de fichiers sont testées ;
17. le secret scanning est bloquant ;
18. les dépendances sont analysées ;
19. l’analyse statique est bloquante selon les règles ;
20. l’image Docker peut être construite ;
21. PostgreSQL n’est pas publié dans Compose ;
22. les rapports sont produits ;
23. les artefacts ne contiennent aucun secret ;
24. Windows est testé ;
25. Linux est testé ;
26. la branche principale est protégée ;
27. les checks obligatoires sont définis ;
28. le push direct sur `main` est interdit ou exceptionnel ;
29. le workflow de release est explicite ;
30. les packages ne sont publiés que depuis une release ;
31. les versions publiées sont immuables ;
32. les secrets de publication utilisent un environnement protégé ;
33. aucune pull request non fiable ne reçoit de secret sensible ;
34. les workflows sont documentés ;
35. les commandes peuvent être reproduites localement.

---

# 87. Décisions validées

La baseline de CI est la suivante :

```text
GitHub Actions est la plateforme CI initiale.

Les pull requests et main déclenchent la CI.

Les workflows déclarent des permissions minimales.

Le runtime cible est .NET 10.

La solution utilise le format .slnx.

Linux et Windows sont testés.

Le restore, le build Release et les tests sont obligatoires.

L’objectif de build est 0 warning et 0 erreur.

Les catalogues et schémas sont validés.

Les tests d’architecture sont bloquants.

La CI génère un projet client de référence.

Le projet généré doit réussir restore, build et tests.

La reproductibilité est vérifiée avant release.

Les personnalisations sont protégées par des tests
de non-écrasement.

Le secret scanning est obligatoire et bloquant.

Les dépendances directes et transitives sont analysées.

L’analyse statique est obligatoire.

CodeQL est utilisé uniquement lorsqu’il est réellement configuré.

Les actions GitHub sont approuvées et versionnées.

Les warnings de dépréciation des actions sont traités.

Les images Docker sont construites et contrôlées.

Les artefacts ne contiennent aucun secret ni donnée client.

La branche main est protégée.

Une pull request reste recommandée même avec un seul mainteneur.

Une approbation externe n’est pas obligatoire
si elle bloque le mainteneur unique.

Les checks automatisés restent obligatoires.

Les releases sont explicites et versionnées.

Une pull request ne publie jamais un package stable.

Une version de package publiée est immuable.

Les migrations de production ne sont jamais appliquées par la CI
de validation ou de release SiteFactory.

Les déploiements clients restent séparés de la génération.

Toute dérogation est explicite, temporaire et documentée.

Codex ne peut pas supprimer seul un quality gate
ou affaiblir un contrôle de sécurité.
```

---

# 88. Règles pour Codex

## 88.1 Lecture préalable

Avant toute modification de la CI, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../02-architecture/core-module-boundary.md
13-generation-pipeline.md
15-cli-specification.md
18-security-baseline.md
19-testing-strategy.md
20-quality-gates-and-ci.md
AGENTS.md
```

Ainsi que :

- workflows existants ;
- scripts CI ;
- fichiers de solution ;
- `global.json` ;
- `Directory.Build.props` ;
- `Directory.Packages.props` ;
- configuration des scans ;
- protection de branche documentée.

---

## 88.2 Ce que Codex peut faire

Codex peut :

- créer un workflow prévu ;
- ajouter un job ;
- corriger un trigger ;
- corriger le chemin d’une solution `.slnx` ;
- ajouter un test ;
- ajouter un scan ;
- réduire les permissions ;
- améliorer un cache ;
- améliorer un rapport ;
- ajouter un artefact non sensible ;
- corriger un warning de dépréciation ;
- ajouter un workflow réutilisable ;
- documenter une gate.

---

## 88.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- supprimer un gate pour rendre la CI verte ;
- ajouter `continue-on-error` sur un scan bloquant ;
- ajouter des permissions d’écriture globales ;
- exposer un secret à une pull request ;
- publier depuis une branche ;
- utiliser une version flottante non contrôlée d’une action ;
- désactiver Windows sans validation ;
- désactiver le projet généré de référence ;
- considérer un test ignoré comme réussi ;
- masquer une vulnérabilité ;
- modifier un prix catalogue pour corriger un test ;
- ajouter un déploiement production automatique ;
- appliquer une migration production ;
- ajouter un secret dans un script ;
- afficher toutes les variables d’environnement ;
- créer un auto-merge sans validation ;
- modifier un check obligatoire sans signalement ;
- accepter en masse des golden masters.

---

## 88.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouveau workflow principal ;
- nouvelle plateforme CI ;
- retrait d’un OS de la matrice ;
- changement d’un quality gate ;
- modification d’un seuil de couverture ;
- publication automatique ;
- auto-merge ;
- nouveau secret ;
- nouvelles permissions d’écriture ;
- nouveau registry ;
- nouveau feed ;
- activation d’un déploiement ;
- changement de stratégie de version ;
- modification de la protection de branche ;
- modification de la politique de vulnérabilités ;
- bypass d’une analyse de sécurité.

---

## 88.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Workflows modifiés
Déclencheurs modifiés
Permissions modifiées
Secrets utilisés
Jobs ajoutés ou retirés
Plateformes affectées
Commandes exécutées
Quality gates affectés
Artefacts produits
Caches modifiés
Scans affectés
Publication affectée
Tests du workflow
Build
Tests
Durée approximative
Risques
Décisions humaines restantes
```

Codex ne doit pas déclarer un workflow fonctionnel uniquement parce que le YAML est syntaxiquement valide.

---

# 89. Anti-patterns interdits

## 89.1 Workflow sans trigger valide

Un fichier workflow ne contient aucun événement exploitable.

---

## 89.2 Mauvais nom de solution

```bash
dotnet restore SiteFactory.sln
```

alors que le repository utilise :

```text
SiteFactory.slnx
```

---

## 89.3 Permissions globales d’écriture

```yaml
permissions: write-all
```

---

## 89.4 Scan non bloquant silencieux

```yaml
continue-on-error: true
```

sur le secret scanning ou l’analyse de sécurité sans dérogation.

---

## 89.5 Secret sur pull request non fiable

Un workflow expose un token de publication au code proposé.

---

## 89.6 Publication sur chaque merge

Toute modification de `main` publie automatiquement des packages stables.

---

## 89.7 Action non maîtrisée

Une action tierce peu connue est ajoutée sans analyse.

---

## 89.8 Référence flottante

Une action sensible utilise une référence non contrôlée.

---

## 89.9 Cache des secrets

Un cache contient la configuration NuGet avec credentials.

---

## 89.10 Artefact contenant `.env`

Le workflow publie toute l’arborescence sans exclusion.

---

## 89.11 Test ignoré masqué

Le résumé ne signale pas les tests ignorés.

---

## 89.12 Build uniquement Linux

Le CLI destiné principalement à un développement Windows n’est jamais testé sous Windows.

---

## 89.13 Projet généré non compilé

La CI vérifie uniquement que des fichiers ont été créés.

---

## 89.14 Auto-acceptation des snapshots

Le workflow met automatiquement à jour les golden masters.

---

## 89.15 Déploiement dans le workflow de génération

La validation de SiteFactory pousse directement une application client en production.

---

## 89.16 Migration automatique

Un job CI applique les migrations sur une base de production lors d’un simple push.

---

## 89.17 Echo des variables

```bash
env
```

ou équivalent expose l’ensemble de l’environnement dans les logs.

---

# 90. Roadmap d’implémentation

## Phase CI 1 — Build principal

```text
ci.yml
checkout
setup .NET
restore .slnx
build Release
tests
```

---

## Phase CI 2 — Contrats

```text
catalogues
schémas
architecture
documentation structurée
```

---

## Phase CI 3 — Sécurité

```text
secret scan
dependency scan
analyse statique
permissions workflows
```

---

## Phase CI 4 — Projet généré

```text
build CLI
generate sample
restore
build
tests
lock file
report
```

---

## Phase CI 5 — Plateformes

```text
Linux
Windows
chemins
processus
Git
```

---

## Phase CI 6 — Docker

```text
build image
scan
Compose
health check
PostgreSQL
```

---

## Phase CI 7 — Couverture

```text
rapport
baseline
gate de régression
seuils progressifs
```

---

## Phase CI 8 — Release

```text
version
tag
packages
CLI
changelog
artefacts
checksums
```

---

## Phase CI 9 — Suites avancées

```text
nightly
end-to-end
restore drill
performance
DAST
SBOM
provenance
```

---

# 91. Exemple conceptuel de workflow principal

L’exemple suivant illustre la structure attendue.

Les références des actions doivent être remplacées par les versions ou commits validés par LogicGate.

```yaml
name: CI

on:
  pull_request:
  push:
    branches:
      - main
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: ci-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build-and-test:
    name: Build / ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os:
          - ubuntu-latest
          - windows-latest

    runs-on: ${{ matrix.os }}

    timeout-minutes: 30

    steps:
      - name: Checkout
        uses: actions/checkout@<PINNED_REFERENCE>

      - name: Setup .NET
        uses: actions/setup-dotnet@<PINNED_REFERENCE>
        with:
          global-json-file: global.json
          cache: true

      - name: Restore
        run: dotnet restore SiteFactory.slnx --locked-mode

      - name: Build
        run: >
          dotnet build SiteFactory.slnx
          --configuration Release
          --no-restore

      - name: Test
        run: >
          dotnet test SiteFactory.slnx
          --configuration Release
          --no-build
          --no-restore
          --filter "Category!=PostgreSql"
          --logger trx
```

Cet exemple ne représente pas encore le workflow complet du produit.

---

# 92. Exemple conceptuel de génération de référence

Cet exemple documentaire utilise le CLI public existant et le template statique.
Il illustre une future étape de workflow, pas un job supplémentaire actuellement
présent. Les références d'actions placeholders doivent être remplacées par des
commits approuvés ; le véritable workflow demeure `ci.yml`.

```yaml
generated-project:
  name: Generated site / Documentation
  runs-on: ubuntu-latest
  timeout-minutes: 45

  permissions:
    contents: read

  steps:
    - name: Checkout
      uses: actions/checkout@<PINNED_REFERENCE>

    - name: Setup .NET
      uses: actions/setup-dotnet@<PINNED_REFERENCE>
      with:
        global-json-file: global.json
        cache: true

    - name: Build SiteFactory
      run: >
        dotnet build SiteFactory.slnx
        --configuration Release

    - name: Generate reference documentation
      run: >
        dotnet run
        --project src/LogicGate.SiteFactory.Cli/LogicGate.SiteFactory.Cli.csproj
        --configuration Release
        --no-build
        --no-restore
        --
        generate
        --config tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml
        --output artifacts/documentation-site
```

La cible doit être absente au premier appel. Les sorties statiques comprennent
`index.html`, `guide.html`, `sitefactory.lock.json`, `generation-report.json` et
`.sitefactory/generation-state.json`. Aucun restore/build/test d'application
cliente n'est inventé : une contribution générant un vrai projet .NET devra
ajouter ces contrôles avec le nom réel de sa solution.

---

# 93. Checklist de revue d’un workflow

## Déclencheurs

```text
[ ] Événements définis
[ ] Branches correctes
[ ] Pas de trigger dangereux
[ ] Workflow manuel lorsque pertinent
```

## Permissions

```text
[ ] Permissions explicites
[ ] Contents read par défaut
[ ] Écriture limitée
[ ] Aucun secret sur code non fiable
```

## Outils

```text
[ ] Actions approuvées
[ ] Références maîtrisées
[ ] Runtime des actions supporté
[ ] SDK défini par global.json
[ ] Solution .slnx correcte
```

## Exécution

```text
[ ] Restore
[ ] Build Release
[ ] Tests
[ ] Timeout
[ ] Annulation
[ ] Code de sortie contrôlé
```

## Sécurité

```text
[ ] Aucun secret affiché
[ ] Secret scan
[ ] Dependency scan
[ ] Analyse statique
[ ] Artefacts inspectés
```

## Sorties

```text
[ ] Résumé clair
[ ] Rapports de tests
[ ] Artefacts utiles
[ ] Rétention adaptée
[ ] Aucun fichier sensible
```

---

# 94. Checklist de protection de branche

```text
[ ] Pull request obligatoire
[ ] Checks obligatoires configurés
[ ] Checks nommés de manière stable
[ ] Conversations résolues
[ ] Push direct interdit
[ ] Bypass limité
[ ] Historique de merge cohérent
[ ] Règle adaptée au mainteneur unique
```

---

# 95. Checklist de release

```text
[ ] Main verte
[ ] Version validée
[ ] Tag validé
[ ] Changelog
[ ] Windows
[ ] Linux
[ ] Suite complète
[ ] Projet généré
[ ] Reproductibilité
[ ] Non-écrasement
[ ] PostgreSQL
[ ] Migrations
[ ] Docker
[ ] Secret scan
[ ] Dependency scan
[ ] Analyse statique
[ ] SBOM si activée
[ ] Packages inspectés
[ ] Artefacts signés ou avec checksums si prévu
[ ] Notes de release
[ ] Aucun secret
```

---

# 96. Checklist d’analyse d’un échec CI

```text
[ ] Étape réellement en échec identifiée
[ ] Erreur produit ou infrastructure distinguée
[ ] Code de sortie vérifié
[ ] Logs examinés sans exposer de secret
[ ] Reproduction locale tentée
[ ] Test de régression ajouté si bug
[ ] Workflow corrigé si nécessaire
[ ] Gate non désactivé
[ ] Résultat réexécuté
```

---

# 97. Conclusion

L’intégration continue de SiteFactory doit démontrer que :

```text
le moteur compile
→ les règles sont respectées
→ les catalogues sont cohérents
→ les schémas sont valides
→ les modules fonctionnent
→ un projet client peut être généré
→ ce projet compile et passe ses tests
→ aucun risque bloquant connu n’est ignoré
```

Les quality gates ne constituent pas une formalité administrative.

Ils protègent :

- le moteur ;
- les projets clients ;
- les données ;
- la supply chain ;
- les releases ;
- la réputation de LogicGate.

> Une branche principale verte doit signifier davantage que « le code compile ». Elle doit signifier que SiteFactory est encore capable de produire un projet client cohérent, testable, sécurisé et reproductible.
