# LogicGate SiteFactory — Spécification du CLI

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Spécification du CLI |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Nom de l’exécutable | `sitefactory` |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../02-architecture/core-module-boundary.md`, `09-module-architecture.md`, `10-feature-flags-and-entitlements.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `13-generation-pipeline.md`, `14-client-configuration-schema.md` |
| Dernière mise à jour | 2026-10-21 |

---

## CLI publique disponible

Le [Program](../../src/LogicGate.SiteFactory.Cli/Program.cs) compose exclusivement
Documentation et injecte `GenerationComposition` dans `SiteFactoryCli`.
Aucun overload ne réintroduit une composition Business implicite.
Le host privé possède sa composition distincte ; il n'est pas une dépendance
de l'exécutable public.

Commandes actuellement acceptées :

```text
--help
--version
validate --config <path>
plan --config <path>
generate --config <path> --output <directory>
```

Depuis le checkout, utiliser `dotnet run --project src/LogicGate.SiteFactory.Cli
--configuration Release --no-build --no-restore -- <commande>` après compilation.
Aucun outil global ou package NuGet n'est encore publié.
[Exemple complet avec la fixture publique](initial-generation-cli.md).

`validate` écrit un résultat court ; `plan` écrit le JSON déterministe du plan.
Les diagnostics sont sur stderr, la console est UTF-8 et Ctrl+C propage l'annulation.
Codes actuels : 0 succès ; 1 interne ; 2 arguments ; 3 configuration ;
4 catalogue ; 5 plan non exécutable ; 6 conflit/état ; 7 génération ;
11 environnement ; 12 annulation ; 15 version non supportée.
[Constantes réelles](../../src/LogicGate.SiteFactory.Cli/CliExitCode.cs).

`generate` ne lance ni validation .NET de la sortie, ni Git, migration, déploiement
ou publication. L'import d'un repository documentaire est une API C#, **pas une
commande CLI**. Les autres commandes/options, formes JSON, prompts et codes
proposés dans les sections numérotées ci-dessous restent des contrats cibles ;
ils ne font pas partie du parseur livré et ne doivent pas être invoqués.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit le contrat de l’interface en ligne de commande de LogicGate SiteFactory.

Il précise notamment :

- les principes d’utilisation du CLI ;
- la syntaxe générale ;
- les commandes disponibles ;
- les arguments et options ;
- les valeurs par défaut ;
- les sorties humaines et JSON ;
- les codes de sortie ;
- les confirmations ;
- les comportements non interactifs ;
- les règles de dry-run ;
- la gestion des erreurs ;
- la sécurité des entrées ;
- l’exécution de processus externes ;
- l’initialisation Git ;
- les futures commandes de mise à niveau ;
- les exigences de tests ;
- les contraintes applicables à Codex.

Le CLI constitue le premier point d’entrée officiel de SiteFactory.

Le contrat livré de `generate` est celui du périmètre public ci-dessus ;
les variantes enrichies suivantes restent cibles.

Une future interface graphique devra réutiliser les mêmes cas d’usage applicatifs et ne devra pas réimplémenter les règles métier propres au CLI.

---

## 2. Vision du CLI

Le CLI permet à un opérateur LogicGate de transformer une configuration client validée en un projet exploitable.

Flux principal :

```text
sitefactory validate
→ sitefactory plan
→ sitefactory generate
→ revue du projet
→ recette
→ déploiement contrôlé
```

Le CLI doit être :

- lisible pour un humain ;
- scriptable dans une CI ;
- strict sur les entrées ;
- sûr par défaut ;
- non destructif sans demande explicite ;
- cohérent dans ses sorties ;
- stable dans ses codes d’erreur ;
- honnête sur les opérations réellement exécutées.

Le CLI ne doit pas devenir :

- un shell interactif complexe ;
- un outil de déploiement automatique incontrôlé ;
- un moyen de contourner les validations ;
- un stockage de secrets ;
- une seconde source de vérité fonctionnelle ;
- un emplacement pour la logique de catalogue ;
- un système de plugins dynamique.

---

## 3. Principes fondamentaux

## 3.1 Une commande possède une intention explicite

Les commandes de lecture, planification et modification doivent être distinctes.

Exemples :

```text
validate
→ vérifie

plan
→ prépare les opérations

generate
→ applique une génération

upgrade check
→ recherche les évolutions

upgrade plan
→ prépare une mise à niveau

upgrade apply
→ applique une mise à niveau
```

Une commande de consultation ne doit pas modifier silencieusement le repository ou l’environnement.

---

## 3.2 Les valeurs par défaut privilégient la sécurité

En cas d’ambiguïté, le CLI doit :

- refuser ;
- demander une confirmation ;
- produire un plan ;
- ou exécuter un dry-run.

Il ne doit pas choisir silencieusement l’option la plus destructive.

---

## 3.3 Le CLI ne contient pas la logique métier principale

Les handlers CLI doivent principalement :

1. analyser les arguments ;
2. produire une requête ou commande applicative ;
3. invoquer le cas d’usage ;
4. afficher le résultat ;
5. retourner un code de sortie.

Ils ne doivent pas directement :

- résoudre les packs ;
- calculer les prix ;
- parcourir les dépendances ;
- générer tous les fichiers ;
- appliquer les règles d’autorisation ;
- construire les migrations ;
- exécuter Git de manière dispersée.

---

## 3.4 Toute opération sensible est planifiée avant exécution

Les opérations suivantes doivent être précédées par un plan ou une confirmation adaptée :

- écriture dans un dossier non vide ;
- remplacement d’un fichier généré modifié ;
- ajout ou retrait d’un module ;
- mise à niveau ;
- modification de migrations ;
- initialisation Git dans un dossier existant ;
- suppression future ;
- création d’un repository distant ;
- déploiement futur.

---

## 3.5 Une commande échouée retourne un code non nul

Le CLI ne doit jamais afficher un résumé positif si :

- la validation échoue ;
- le plan est incohérent ;
- le restore échoue ;
- le build échoue ;
- un test échoue ;
- un secret est détecté ;
- une opération critique n’a pas été exécutée ;
- l’utilisateur annule une action.

---

# 4. Non-objectifs du MVP

Le CLI initial ne fournit pas :

- d’interface plein écran ;
- de wizard graphique ;
- de marketplace ;
- de serveur central ;
- de synchronisation cloud ;
- de déploiement automatique multi-cloud ;
- d’accès direct à une production sans runbook ;
- de gestion centralisée des secrets ;
- de télémétrie externe obligatoire ;
- de connexion utilisateur à SiteFactory ;
- de création automatique de comptes clients ;
- de mise à niveau automatique de tous les projets ;
- d’exécution dynamique de scripts fournis par les templates.

---

# 5. Nom de l’exécutable

Le nom de référence est :

```bash
sitefactory
```

Pendant le développement local, il peut être invoqué par :

```bash
dotnet run \
  --project src/LogicGate.SiteFactory.Cli \
  -- <command> <options>
```

Après installation comme outil .NET :

```bash
sitefactory <command> <options>
```

Un alias plus court ne doit pas être introduit avant stabilisation du nom officiel.

---

# 6. Syntaxe générale

```bash
sitefactory [global-options] <command> [command-options]
```

Exemple :

```bash
sitefactory \
  --output-format human \
  validate \
  --config ./client.sitefactory.yml \
  --profile production
```

Les options globales peuvent être placées avant la commande.

Les options propres à une commande suivent celle-ci.

---

# 7. Conventions de syntaxe

## 7.1 Noms des commandes

Les commandes utilisent :

```text
kebab-case
```

Exemples :

```text
validate
generate
upgrade
add-module
```

Les groupes utilisent un nom principal puis une sous-commande :

```text
upgrade check
upgrade plan
upgrade apply
```

---

## 7.2 Options

Les options longues utilisent :

```text
--kebab-case
```

Exemples :

```text
--config
--output
--dry-run
--output-format
--initialize-git
```

---

## 7.3 Options courtes

Les options courtes doivent rester limitées aux options très fréquentes.

Exemples possibles :

```text
-c pour --config
-o pour --output
-h pour --help
-v pour --version
```

Une option courte ne doit pas avoir une signification différente selon les commandes sans justification forte.

---

## 7.4 Booléens

Une option booléenne est activée par sa présence.

Exemple :

```bash
--dry-run
```

Éviter les syntaxes ambiguës :

```bash
--dry-run=true
```

sauf lorsque la bibliothèque CLI impose ou facilite ce format.

Pour désactiver une valeur par défaut, utiliser une option explicite :

```text
--no-initialize-git
```

---

## 7.5 Listes

Une liste peut être fournie par répétition de l’option.

Exemple :

```bash
--module sample-form \
--module portfolio-advanced
```

La configuration client reste toutefois la source normale de sélection des modules.

---

# 8. Options globales

## 8.1 `--help`

```bash
sitefactory --help
sitefactory validate --help
sitefactory upgrade plan --help
```

Affiche :

- résumé ;
- syntaxe ;
- options ;
- valeurs par défaut ;
- exemples ;
- codes de sortie pertinents.

Le help ne doit pas nécessiter la présence d’une configuration valide.

---

## 8.2 `--version`

```bash
sitefactory --version
```

Affiche au minimum :

```text
SiteFactory CLI 0.1.0
```

Une sortie plus détaillée peut être fournie avec :

```bash
sitefactory version
```

---

## 8.3 `--output-format`

Valeurs initiales :

```text
human
json
```

Valeur par défaut :

```text
human
```

Exemple :

```bash
sitefactory \
  --output-format json \
  validate \
  --config client.sitefactory.yml
```

---

## 8.4 `--verbosity`

Valeurs initiales :

```text
quiet
normal
detailed
diagnostic
```

Valeur par défaut :

```text
normal
```

Règles :

- `quiet` affiche uniquement le résultat final et les erreurs ;
- `normal` affiche les étapes principales ;
- `detailed` affiche les opérations utiles au diagnostic ;
- `diagnostic` reste réservé au développement et ne doit jamais exposer de secret.

---

## 8.5 `--no-color`

Désactive les couleurs ANSI.

Le CLI doit également désactiver automatiquement les couleurs lorsque :

- la sortie n’est pas interactive ;
- `NO_COLOR` est défini ;
- le format JSON est sélectionné.

---

## 8.6 `--non-interactive`

Interdit toute question interactive.

Exemple :

```bash
sitefactory \
  --non-interactive \
  generate \
  --config client.sitefactory.yml \
  --output ../client-project \
  --execute
```

En mode non interactif :

- toute confirmation nécessaire doit être fournie par une option explicite ;
- sinon la commande échoue avec un code dédié.

---

## 8.7 `--working-directory`

Permet de définir le répertoire de travail de référence.

```bash
--working-directory /workspace/sitefactory
```

Par défaut :

```text
répertoire courant
```

Le chemin doit être normalisé et validé.

---

## 8.8 `--report`

Permet de définir le chemin du rapport machine.

```bash
--report ./artifacts/generation-report.json
```

Le chemin doit rester dans un emplacement autorisé.

---

# 9. Commande `version`

## 9.1 Syntaxe

```bash
sitefactory version
```

---

## 9.2 Objectif

Afficher les versions utiles au diagnostic.

Sortie humaine possible :

```text
SiteFactory CLI       0.1.0
Generator             0.1.0
Configuration schema  1
Catalog schema        1
Runtime               .NET 10.0
Operating system      Windows 11
```

---

## 9.3 Sortie JSON

```json
{
  "cliVersion": "0.1.0",
  "generatorVersion": "0.1.0",
  "configurationSchemaVersions": [
    1
  ],
  "catalogSchemaVersions": [
    1
  ],
  "dotnetRuntimeVersion": "10.0.0",
  "operatingSystem": "windows"
}
```

---

## 9.4 Effets de bord

Aucun.

---

# 10. Commande `doctor`

## 10.1 Syntaxe

```bash
sitefactory doctor
```

Options :

```text
--config
--output
--profile
--check-network
```

Exemple :

```bash
sitefactory doctor \
  --config ./client.sitefactory.yml \
  --output ../client-project
```

---

## 10.2 Objectif

Vérifier que l’environnement local permet d’exécuter SiteFactory.

---

## 10.3 Vérifications initiales

```text
Version du SDK .NET
Disponibilité de Git
Accès en lecture aux catalogues
Accès en lecture à la configuration
Accès en écriture au dossier cible parent
Sources NuGet nécessaires
Disponibilité de Docker si demandée
Espace disque lorsque vérifiable
Validité des chemins
```

---

## 10.4 Vérifications réseau

Les vérifications réseau sont désactivées par défaut lorsqu’elles ne sont pas nécessaires.

L’option :

```bash
--check-network
```

peut vérifier :

- disponibilité des sources NuGet ;
- accès au feed privé ;
- accès à un registry explicitement configuré.

Le CLI ne doit pas tester des services externes non déclarés.

---

## 10.5 Sortie humaine

```text
Environment diagnostics

[OK] .NET SDK 10.0.301
[OK] Git 2.51.0
[OK] Catalog directory accessible
[OK] Configuration readable
[OK] Output parent writable
[WARN] Docker not detected
[OK] NuGet source configuration found

Result: READY WITH WARNINGS
```

---

## 10.6 Résultat

Statuts possibles :

```text
ready
ready-with-warnings
not-ready
```

Un warning Docker ne bloque pas une simple validation si Docker n’est pas requis par la commande suivante.

---

## 10.7 Effets de bord

Aucun effet durable.

Le doctor peut créer puis supprimer un fichier temporaire dans le dossier cible parent afin de tester l’écriture.

---

# 11. Commande `validate`

### État d'implémentation — SF-TASK-008

Le sous-ensemble initial expose uniquement `validate --config <path>` et `validate --help`, sans bibliothèque CLI externe. `--config` est obligatoire, sans découverte automatique. Le chargement YAML strict Infrastructure puis la validation sémantique Application portent sur le contrat V1 initial de SF-TASK-005 à SF-TASK-007. Les options et responsabilités supplémentaires décrites ci-dessous restent des cibles futures, et ne sont pas acceptées silencieusement.

Succès court sur stdout : `La configuration SiteFactory est valide.` Diagnostics sur stderr : `CODE path message`, dans l'ordre du résultat Application, avec `-` si le chemin logique est absent. Depuis SF-TASK-009, stdout/stderr console utilisent explicitement UTF-8 sans BOM, indépendamment de la page de codes du poste. Les assertions publiques de validation utilisent la fixture de configuration publique. La commande validate reste read-only ; plan et generate sont les commandes séparées décrites dans le périmètre livré.

Les codes de la section 26 utilisés sont `0` (succès), `1` (erreur inattendue), `2` (arguments invalides), `3` (configuration invalide, y compris fichier absent/illisible et YAML invalide), `12` (annulation), et `15` pour le diagnostic `SF-CONFIG-SCHEMA-002` (schéma non supporté). Aucun code numérique nouveau n'est introduit. Voir le [guide d'utilisation initial](initial-generation-cli.md) pour le parsing et les sorties exactes.

## 11.1 Syntaxe

```bash
sitefactory validate \
  --config ./client.sitefactory.yml
```

Options :

```text
--config
--profile
--catalog
--pages-catalog
--output-normalized-config
--strict
```

---

## 11.2 Option `--config`

Chemin du fichier :

```text
client.sitefactory.yml
```

Option obligatoire sauf convention future de découverte automatique.

---

## 11.3 Option `--profile`

Valeurs :

```text
development
staging
production
```

Valeur par défaut :

```text
development
```

Exemple :

```bash
sitefactory validate \
  --config client.sitefactory.yml \
  --profile production
```

---

## 11.4 Option `--catalog`

Permet de remplacer explicitement le chemin du catalogue pendant le développement ou les tests.

```bash
--catalog ./catalog/sitefactory-catalog.yml
```

Cette option ne doit pas permettre de charger une URL distante.

---

## 11.5 Option `--pages-catalog`

```bash
--pages-catalog ./catalog/pages-and-sections.yml
```

---

## 11.6 Option `--output-normalized-config`

Écrit une représentation normalisée distincte.

```bash
--output-normalized-config ./artifacts/client.normalized.json
```

Le fichier original n’est pas modifié.

---

## 11.7 Responsabilités

La commande exécute :

1. lecture ;
2. parsing YAML ;
3. validation du schéma ;
4. détection des secrets ;
5. validation du catalogue ;
6. résolution du pack ;
7. résolution des entitlements ;
8. résolution des modules ;
9. validation du template ;
10. validation du thème ;
11. validation des pages ;
12. validation Identity ;
13. validation du profil de déploiement ;
14. validation du profil d’environnement.

---

## 11.8 Effets de bord

Aucun effet sur le projet cible.

L’écriture du rapport ou de la configuration normalisée est autorisée uniquement si explicitement demandée.

---

## 11.9 Sortie humaine

```text
Configuration validation

Configuration schema : 1
Catalog version      : 1.0.0
Client               : sample-project
Pack                 : sample-application
Template             : sample-application@1.0.0
Theme                : professional-clean@1.0.0
Modules resolved     : 14
Warnings             : 0
Errors               : 0

Result: VALID
```

---

## 11.10 Sortie invalide

```text
Configuration validation failed.

SF-CONFIG-016
The module "lead-management" is required by pack
"sample-application" and cannot be disabled.

Location:
modules.lead-management.enabled

Result: INVALID
```

---

# 12. Commande `plan`

## 12.0 Coupe disponible — SF-TASK-018

Le contrat minimal implémenté est :

```bash
sitefactory plan --config <path>
sitefactory plan --help
```

Le chemin est obligatoire, relatif au répertoire courant ou absolu, avec les mêmes
règles strictes que `validate`. Aucun défaut, découverte automatique ou alias.
Le pipeline réutilise le loader YAML, le validateur V1, le catalogue embarqué,
le résolveur, le builder et le sérialiseur JSON V1. Le succès retourne `0`, le
JSON seul sur stdout (UTF-8 sans BOM, LF, un LF final) et stderr vide.
L'ID logique est `plan:<slug>:<product>:<template>`, à partir des valeurs validées,
sans date, hash, aléatoire ni chemin physique ; il n'est pas une empreinte du contenu.

Une erreur de configuration conserve exactement les diagnostics et codes de
`validate`, avec stdout vide. Le template inconnu dans le YAML est rejeté en
amont avec `SF-CONFIG-SEMANTIC-006` et le code `3`. Une erreur de résolution après
validation (template absent du catalogue, produit incompatible, manifest invalide
ou résolution ambiguë) utilise `4` (`InvalidCatalog`). Un plan bloqué utilise `5`
(`NonExecutablePlan`), avec ses diagnostics sur stderr et aucun JSON partiel.
Les codes `1`, `2`, `12`, `15` et le format `CODE path message` restent inchangés.
Les diagnostics du plan non bloquants restent dans le JSON du succès.

Aucun dossier créé, fichier écrit/modifié, accès à un dossier cible, opération Git,
processus externe ou appel réseau. `git.initializeLocalRepository: true` reste une
préférence de génération future. Le plan minimal ne prouve pas qu'une écriture
est exécutable et ne résout pas encore les modules.

Les sections 12.1 à 12.9 ci-dessous décrivent la cible future, pas les options
disponibles : `--output`, `--apply`, `--force`, `--upgrade`, `--dry-run`,
`--plan-file`, `--profile` et les options Git sont refusés dans cette coupe.
L'[exemple README](initial-generation-cli.md) est testé
en processus réel, sans écriture client ni normalisation des octets stdout.

## 12.1 Syntaxe cible future

```bash
sitefactory plan \
  --config ./client.sitefactory.yml \
  --output ../client-project
```

Options :

```text
--config
--output
--profile
--plan-file
--initialize-git
--no-initialize-git
--include-commands
```

---

## 12.2 Objectif

Produire un plan de génération complet sans modifier le dossier cible.

---

## 12.3 Option `--output`

Chemin du projet cible.

Cette option est obligatoire.

---

## 12.4 Option `--plan-file`

```bash
--plan-file ./artifacts/generation-plan.json
```

Le plan machine doit être stable et ne contenir aucun secret.

---

## 12.5 Option `--initialize-git`

Force la présence de l’initialisation Git dans le plan.

Si aucune option CLI n’est fournie, la valeur de :

```text
generation.initializeGit
```

est utilisée.

---

## 12.6 Responsabilités

La commande :

1. exécute les validations ;
2. résout la configuration ;
3. analyse le dossier cible ;
4. produit les opérations de fichiers ;
5. produit les commandes prévues ;
6. détecte les conflits ;
7. liste les actions manuelles ;
8. calcule l’état exécutable du plan.

---

## 12.7 Effets de bord

Aucune modification du dossier cible.

L’écriture du plan dans un fichier explicitement demandé est autorisée.

---

## 12.8 Sortie humaine

```text
Generation plan

Output directory:
../sample-project

Pack:
sample-application

Template:
sample-application@1.0.0

Theme:
professional-clean@1.0.0

Modules:
14

File operations:
Create      82
Create once  6
Merge        2
Protected    3
Conflicts    0

Commands:
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
git init
git add .
git commit

Manual actions:
- Configure production secrets
- Create the first administrator
- Configure the domain
- Apply production migrations explicitly

Result: EXECUTABLE
```

---

## 12.9 Plan non exécutable

Un plan est non exécutable lorsque :

- un conflit bloquant existe ;
- une dépendance est incompatible ;
- un fichier protégé serait remplacé ;
- le chemin est dangereux ;
- une validation échoue.

---

# 13. Commande `generate`

## 13.1 Syntaxe

```bash
sitefactory generate \
  --config ./client.sitefactory.yml \
  --output ../client-project \
  --execute
```

Modes autorisés :

```text
--dry-run
--execute
```

L’absence des deux doit produire une erreur.

---

## 13.2 Mode dry-run

```bash
sitefactory generate \
  --config client.sitefactory.yml \
  --output ../client-project \
  --dry-run
```

Doit produire le même résultat fonctionnel que `plan`.

Il ne modifie aucun fichier du dossier cible.

---

## 13.3 Mode execute

```bash
sitefactory generate \
  --config client.sitefactory.yml \
  --output ../client-project \
  --execute
```

Applique le plan après validation.

---

## 13.4 Options

```text
--config
--output
--profile
--dry-run
--execute
--confirm-generation
--force
--initialize-git
--no-initialize-git
--create-initial-commit
--no-create-initial-commit
--keep-temp
--report
```

---

## 13.5 `--confirm-generation`

Requis en mode non interactif lorsque :

- le dossier cible n’est pas vide ;
- une opération nécessite une confirmation ;
- la politique de la commande l’exige.

Exemple :

```bash
sitefactory \
  --non-interactive \
  generate \
  --config client.sitefactory.yml \
  --output ../client-project \
  --execute \
  --confirm-generation
```

---

## 13.6 `--force`

Rôle limité.

Peut permettre :

- le remplacement explicite d’un fichier `generated` modifié ;
- l’utilisation contrôlée d’un dossier non vide ;
- la régénération d’un artefact SiteFactory.

Ne peut pas permettre :

- l’écrasement d’un fichier `custom` ;
- l’écrasement d’un fichier `protected` ;
- le contournement des tests ;
- le contournement d’un secret détecté ;
- l’écriture hors cible ;
- la suppression globale du dossier ;
- l’application d’une migration de production.

---

## 13.7 `--keep-temp`

Conserve le dossier temporaire après échec ou succès pour diagnostic.

Le chemin du dossier doit être affiché sans exposer d’information sensible.

---

## 13.8 Étapes exécutées

```text
Validation
Planification
Préparation du dossier temporaire
Génération
Restore
Build Release
Tests
Vérification des placeholders
Vérification des secrets
Rapport
Finalisation du dossier
Initialisation Git éventuelle
Commit initial éventuel
```

---

## 13.9 Conditions du commit initial

Le commit n’est créé que si :

- la génération réussit ;
- le restore réussit ;
- le build réussit ;
- les tests réussissent ;
- aucun secret n’est détecté ;
- Git est disponible ;
- aucun conflit ne subsiste.

---

## 13.10 Sortie humaine

```text
[1/11] Validating configuration... OK
[2/11] Resolving modules... OK
[3/11] Creating generation plan... OK
[4/11] Generating files... OK
[5/11] Restoring packages... OK
[6/11] Building Release... OK
[7/11] Running tests... OK
[8/11] Checking placeholders... OK
[9/11] Checking secrets... OK
[10/11] Writing report... OK
[11/11] Initializing Git... OK

Generation: SUCCESS
Output: ../sample-project
Build: 0 warnings, 0 errors
Tests: 152 passed
Report: ../sample-project/generation-report.json
```

---

# 14. Commande `config`

Le groupe `config` concerne les opérations sur le fichier client.

Commandes initiales ou futures :

```text
config validate
config normalize
config migrate
config hash
```

Dans le MVP, `validate` peut rester l’entrée principale sans dupliquer immédiatement toutes ces commandes.

---

## 14.1 `config normalize`

Commande future possible :

```bash
sitefactory config normalize \
  --config client.sitefactory.yml \
  --output client.normalized.json
```

Responsabilités :

- valider ;
- normaliser ;
- trier ;
- produire une représentation machine ;
- ne pas modifier l’original.

---

## 14.2 `config hash`

Commande possible :

```bash
sitefactory config hash \
  --config client.sitefactory.yml
```

Sortie :

```text
SHA256: 9f50...
```

L’empreinte porte sur la configuration normalisée.

---

## 14.3 `config migrate`

Commande future :

```bash
sitefactory config migrate \
  --config client.sitefactory.yml \
  --target-schema 2 \
  --output client.sitefactory.v2.yml
```

Règles :

- plan obligatoire ;
- fichier de sortie distinct ;
- original préservé ;
- décisions manuelles signalées ;
- aucune perte silencieuse.

---

# 15. Commandes de modules

Commandes futures :

```text
module list
module info
module add
module remove
```

Leur implémentation ne fait pas partie du premier incrément du MVP tant que la génération initiale n’est pas stable.

---

## 15.1 `module list`

```bash
sitefactory module list \
  --catalog ./catalog/sitefactory-catalog.yml
```

Filtres possibles :

```text
--status
--pack
--category
```

Sortie possible :

```text
lead-management       mvp        Included in sample-application
portfolio-advanced    planned    Optional
ai-starter             future     Not available
```

---

## 15.2 `module info`

```bash
sitefactory module info lead-management
```

Affiche :

- état ;
- version ;
- package ;
- dépendances ;
- permissions ;
- packs ;
- données ;
- migrations ;
- documentation.

---

## 15.3 `module add`

Commande future :

```bash
sitefactory module add sample-form \
  --project ../client-project \
  --dry-run
```

Puis :

```bash
sitefactory module add sample-form \
  --project ../client-project \
  --execute \
  --confirm-module-change
```

Cette commande doit réutiliser le pipeline d’upgrade et non modifier manuellement les fichiers de façon indépendante.

---

## 15.4 `module remove`

Le retrait est plus risqué.

Le MVP ne doit pas proposer une désinstallation destructrice automatique.

Une future commande doit distinguer :

```text
disable
remove-package
remove-data
```

La suppression des données doit rester une opération séparée et explicitement validée.

---

# 16. Commandes `upgrade`

Le groupe `upgrade` prépare les mises à niveau des projets existants.

```text
upgrade check
upgrade plan
upgrade apply
```

---

## 16.1 `upgrade check`

### Syntaxe

```bash
sitefactory upgrade check \
  --project ../client-project
```

### Responsabilités

Comparer le lock file avec :

- versions du générateur ;
- catalogue ;
- template ;
- thème ;
- modules ;
- schéma de configuration.

### Sortie

```text
Upgrade check

Generator:
0.3.0 → 0.4.0 available

Template:
sample-application 1.0.0 → 1.1.0 available

Modules:
lead-management 1.2.1 → 1.2.2 patch available
portfolio-basic 1.0.0 → current

Breaking changes:
none
```

### Effets de bord

Aucun.

---

## 16.2 `upgrade plan`

### Syntaxe

```bash
sitefactory upgrade plan \
  --project ../client-project \
  --target-generator 0.4.0
```

### Responsabilités

Produire :

- versions source et cible ;
- fichiers concernés ;
- packages ;
- migrations ;
- conflits ;
- actions manuelles ;
- tests requis ;
- risques ;
- rollback.

### Préconditions

- repository Git présent ;
- branche connue ;
- repository propre ;
- lock file valide ;
- configuration valide.

### Effets de bord

Aucune modification du projet.

---

## 16.3 `upgrade apply`

### Syntaxe

```bash
sitefactory upgrade apply \
  --project ../client-project \
  --execute \
  --confirm-upgrade
```

### Préconditions

```text
Repository Git propre
Branche dédiée recommandée
Plan exécutable
Configuration valide
Lock file valide
Aucun conflit bloquant
```

### Responsabilités

- appliquer le plan ;
- modifier les packages ;
- modifier les fichiers gérés ;
- préserver les personnalisations ;
- préparer les migrations ;
- exécuter restore, build et tests ;
- mettre à jour le lock file ;
- produire un rapport.

### Interdictions

La commande ne doit pas :

- appliquer la migration à la production ;
- déployer automatiquement ;
- écraser une extension ;
- supprimer une donnée ;
- créer directement un tag de production sans option future explicite.

---

# 17. Commande `repository`

Groupe futur :

```text
repository status
repository init
repository create
repository push
```

Le MVP peut se limiter à l’initialisation Git dans `generate`.

---

## 17.1 `repository create`

Une future commande pourra créer un repository distant.

Exemple :

```bash
sitefactory repository create \
  --project ../client-project \
  --provider github \
  --visibility private \
  --confirm-create
```

Cette commande nécessite :

- plugin ou intégration validée ;
- authentification sécurisée ;
- confirmation ;
- détection des secrets ;
- repository local propre ;
- build et tests réussis ;
- visibilité explicite.

Elle reste hors MVP initial.

---

# 18. Commande `deploy`

Le déploiement automatisé reste hors du premier MVP du CLI.

Une future commande :

```bash
sitefactory deploy plan
sitefactory deploy apply
```

devra être séparée de la génération.

Elle nécessitera :

- environnement ;
- version ;
- backup ;
- migration ;
- secrets ;
- confirmation ;
- smoke tests ;
- rollback.

Le CLI ne doit pas créer une commande unique :

```bash
sitefactory generate-and-deploy-production
```

---

# 19. Commandes d’exploitation

Les scripts propres aux projets clients restent les outils initiaux pour :

```text
backup
restore
health
disk checks
```

Le CLI SiteFactory pourra ultérieurement les piloter, mais ne doit pas remplacer prématurément les runbooks.

---

# 20. Découverte automatique des fichiers

## 20.1 Configuration

Le MVP recommande une option explicite :

```bash
--config ./client.sitefactory.yml
```

Une découverte automatique peut rechercher dans le répertoire courant :

```text
client.sitefactory.yml
```

Elle doit échouer si plusieurs fichiers candidats existent.

---

## 20.2 Projet client

Une commande utilisant :

```bash
--project ../client-project
```

recherche dans ce dossier :

```text
client.sitefactory.yml
sitefactory.lock.json
```

L’absence ou l’ambiguïté produit une erreur.

---

## 20.3 Catalogue

Le CLI peut utiliser le catalogue embarqué dans l’installation SiteFactory ou celui du repository courant selon le mode de développement.

La source sélectionnée doit apparaître dans le mode détaillé.

---

# 21. Configuration de priorité des options

Ordre de priorité recommandé :

```text
1. Arguments CLI explicites
2. Configuration client pour les choix durables
3. Valeurs du template ou catalogue
4. Valeurs par défaut SiteFactory
```

Les variables d’environnement ne doivent pas modifier silencieusement :

- pack ;
- modules vendus ;
- template ;
- thème ;
- entitlements ;
- routes ;
- pages.

Elles sont réservées aux aspects runtime ou à la configuration de l’outil lui-même.

---

# 22. Variables d’environnement du CLI

Variables possibles :

```text
SITEFACTORY_CATALOG_PATH
SITEFACTORY_PAGES_CATALOG_PATH
SITEFACTORY_NUGET_SOURCE
SITEFACTORY_OUTPUT_FORMAT
NO_COLOR
```

Elles ne doivent contenir :

- ni mot de passe client ;
- ni configuration de production ;
- ni entitlement commercial.

Les credentials de feed utilisent les mécanismes sécurisés de NuGet ou de la CI.

---

# 23. Fichier de configuration du CLI

Un fichier opérateur futur peut définir les préférences locales :

```text
.sitefactory/config.yml
```

Il pourrait contenir :

- chemins de catalogues ;
- format de sortie ;
- feed NuGet ;
- emplacement des artefacts ;
- préférences non sensibles.

Il ne doit pas contenir de secrets.

Ce fichier n’est pas nécessaire au premier MVP.

---

# 24. Sortie humaine

## 24.1 Principes

La sortie humaine doit être :

- concise ;
- structurée ;
- lisible ;
- actionnable ;
- stable dans les termes ;
- sans secret ;
- sans PII.

---

## 24.2 Étapes

Format recommandé :

```text
[1/8] Validating configuration... OK
[2/8] Resolving catalog... OK
[3/8] Planning generation... OK
```

États :

```text
OK
WARN
FAIL
SKIP
CANCELLED
```

---

## 24.3 Résumé final

Le résumé doit toujours indiquer :

- résultat ;
- commande ;
- projet ou configuration ;
- warnings ;
- erreurs ;
- rapport ;
- prochaine action éventuelle.

---

## 24.4 Pas de stack trace par défaut

Une stack trace n’est affichée que :

- en mode diagnostic ;
- pour une erreur inattendue ;
- sans secret ni donnée sensible.

Le mode normal affiche un code et un message actionnable.

---

# 25. Sortie JSON

## 25.1 Objectif

Permettre :

- intégration CI ;
- automatisation ;
- analyse des résultats ;
- tests du CLI.

---

## 25.2 Enveloppe commune

```json
{
  "schemaVersion": 1,
  "command": "validate",
  "status": "success",
  "exitCode": 0,
  "startedAtUtc": "2026-07-27T08:00:00Z",
  "finishedAtUtc": "2026-07-27T08:00:01Z",
  "warnings": [],
  "errors": [],
  "result": {}
}
```

---

## 25.3 Statuts

```text
success
success-with-warnings
failed
cancelled
```

---

## 25.4 Stabilité

Les propriétés d’une version de schéma JSON ne doivent pas être supprimées ou renommées sans nouvelle version.

---

## 25.5 Sortie standard et erreur standard

En mode JSON :

- le document JSON principal est écrit sur stdout ;
- les diagnostics non structurés doivent être évités ;
- stderr est réservé aux erreurs empêchant même la production du JSON.

---

# 26. Codes de sortie

Baseline proposée :

| Code | Constante conceptuelle | Signification |
|---:|---|---|
| `0` | `Success` | Succès |
| `1` | `GeneralError` | Erreur générale inattendue |
| `2` | `InvalidArguments` | Arguments invalides |
| `3` | `InvalidConfiguration` | Configuration client invalide |
| `4` | `InvalidCatalog` | Catalogue ou manifest invalide |
| `5` | `NonExecutablePlan` | Plan invalide ou non exécutable |
| `6` | `FileConflict` | Conflit de fichiers |
| `7` | `GenerationFailed` | Génération échouée |
| `8` | `BuildFailed` | Restore ou build échoué |
| `9` | `TestsFailed` | Tests échoués |
| `10` | `SecurityCheckFailed` | Contrôle de sécurité échoué |
| `11` | `EnvironmentInvalid` | Environnement non prêt |
| `12` | `Cancelled` | Opération annulée |
| `13` | `ConfirmationRequired` | Confirmation manquante |
| `14` | `UpgradeNotApplicable` | Mise à niveau non applicable |
| `15` | `UnsupportedVersion` | Version ou schéma non supporté |

Les codes doivent rester stables après publication du CLI 1.0.

---

# 27. Codes d’erreur métier

Les erreurs détaillées utilisent le format :

```text
SF-{DOMAIN}-{NUMBER}
```

Exemples :

```text
SF-CONFIG-004
SF-CATALOG-003
SF-MODULE-002
SF-GEN-005
SF-BUILD-002
SF-SECURITY-001
SF-UPGRADE-003
```

Le code de sortie donne la catégorie générale.

Le code métier identifie le problème précis.

---

# 28. Structure d’une erreur JSON

```json
{
  "code": "SF-CONFIG-016",
  "category": "configuration",
  "message": "An included module cannot be disabled.",
  "location": "modules.lead-management.enabled",
  "suggestion": "Remove the enabled:false property or choose another pack.",
  "isBlocking": true
}
```

L’erreur ne contient pas :

- secret ;
- contenu complet du fichier ;
- mot de passe ;
- token ;
- donnée personnelle inutile.

---

# 29. Confirmations interactives

## 29.1 Format

Une confirmation doit décrire précisément l’opération.

Exemple :

```text
The output directory is not empty.

SiteFactory plans to:
- create 12 files;
- replace 2 generated files;
- preserve 4 custom files.

Type GENERATE to continue:
```

---

## 29.2 Mots de confirmation

Utiliser une valeur explicite adaptée :

```text
GENERATE
UPGRADE
RESTORE
DELETE
CREATE
```

Éviter une simple réponse :

```text
y
```

pour les opérations sensibles.

---

## 29.3 Mode non interactif

La confirmation est fournie par une option spécifique :

```text
--confirm-generation
--confirm-upgrade
--confirm-create
```

Une option générique :

```text
--yes
```

est à éviter pour les opérations à risque élevé, car elle rend l’intention moins claire.

---

# 30. Mode non interactif

Le mode non interactif est destiné :

- aux pipelines ;
- aux tests ;
- aux scripts contrôlés.

Règles :

- aucune lecture depuis stdin ;
- aucune confirmation implicite ;
- erreur immédiate si une décision manque ;
- sortie stable ;
- codes de sortie fiables ;
- couleurs désactivées sauf option contraire future.

---

# 31. Gestion de `--force`

## 31.1 Principe

`--force` modifie une règle limitée, pas le modèle de sécurité complet.

---

## 31.2 Cas initialement supportés

`--force` peut permettre :

- remplacement d’un fichier `generated` dont le hash a changé ;
- utilisation d’un dossier cible non vide après confirmation ;
- régénération d’un rapport ou lock file contrôlé.

---

## 31.3 Cas interdits

`--force` ne peut pas :

- remplacer `custom` ;
- remplacer `protected` ;
- ignorer des tests ;
- ignorer un secret ;
- ignorer une incompatibilité ;
- ignorer une validation de chemin ;
- appliquer une migration production ;
- supprimer tout le dossier cible ;
- créer un remote sans confirmation ;
- modifier les prix ou entitlements.

---

## 31.4 Rapport

Toute opération forcée doit apparaître dans :

```text
generation-report.json
```

avec :

- fichier ;
- raison ;
- stratégie ;
- option utilisée.

---

# 32. Gestion de l’annulation

Les commandes longues doivent répondre à :

```text
Ctrl+C
```

Le premier signal doit :

- demander une annulation propre ;
- transmettre le `CancellationToken` ;
- arrêter les processus externes ;
- finaliser le rapport si possible.

Un second signal peut forcer l’arrêt selon les capacités de la plateforme.

Résultat :

```text
status: cancelled
exit code: 12
```

---

# 33. Timeouts

Options futures ou internes :

```text
--restore-timeout
--build-timeout
--test-timeout
```

Les valeurs durables peuvent provenir de la configuration SiteFactory interne, pas de la configuration fonctionnelle client.

Un timeout doit :

- arrêter le processus ;
- éviter les processus orphelins ;
- produire un code d’erreur ;
- apparaître dans le rapport.

---

# 34. Sécurité des arguments

## 34.1 Chemins

Les chemins doivent être :

- normalisés ;
- validés ;
- résolus avant utilisation ;
- contrôlés contre le path traversal ;
- contrôlés contre les liens symboliques dangereux.

---

## 34.2 Commandes externes

Les arguments sont transmis comme liste structurée.

Exemple recommandé :

```csharp
new ProcessStartInfo
{
    FileName = "dotnet",
    ArgumentList =
    {
        "build",
        solutionPath,
        "--configuration",
        "Release"
    }
};
```

Exemple interdit :

```csharp
$"dotnet build {solutionPath}"
```

exécuté par un shell.

---

## 34.3 Données sensibles

Aucun secret ne doit être fourni comme argument CLI lorsque cela l’exposerait dans :

- historique du terminal ;
- liste des processus ;
- logs ;
- rapport.

---

# 35. Journalisation

## 35.1 Logs structurés

Événements possibles :

```text
CliCommandStarted
CliCommandCompleted
CliCommandFailed
CliConfirmationRequired
CliOperationCancelled
```

Propriétés autorisées :

```text
CommandName
Duration
ExitCode
ErrorCode
OutputFormat
NonInteractive
```

---

## 35.2 Données interdites

```text
Mot de passe
Token
Contenu complet du YAML
Données de leads
Valeurs d’environnement
Connection string
Clé privée
```

---

## 35.3 Client slug

Le slug technique peut être utilisé dans les logs si cela ne constitue pas une donnée sensible dans le contexte concerné.

Le nom commercial complet n’est pas nécessaire dans les logs techniques.

---

# 36. Configuration des couleurs

Les couleurs ANSI ne doivent pas être utilisées lorsque :

- `--no-color` est présent ;
- `NO_COLOR` est défini ;
- stdout n’est pas un terminal ;
- le format JSON est actif.

Les couleurs ne doivent pas être la seule manière de distinguer :

- succès ;
- warning ;
- erreur.

Les libellés textuels restent obligatoires.

---

# 37. Progression

Pour une opération longue, le CLI peut afficher :

```text
[3/9] Building Release...
```

Le MVP ne nécessite pas une barre de progression animée.

Une sortie ligne par ligne est préférable car elle reste :

- lisible ;
- testable ;
- compatible CI ;
- simple.

---

# 38. Redirection des sorties des processus

## 38.1 Mode normal

Afficher :

- étape ;
- résultat ;
- résumé ;
- dernière erreur pertinente.

---

## 38.2 Mode détaillé

Afficher davantage de sortie, avec filtrage des secrets.

---

## 38.3 Rapport

Le rapport peut conserver :

- code de sortie ;
- durée ;
- nombre de warnings ;
- nombre d’erreurs ;
- extrait limité.

Il ne doit pas copier intégralement une sortie contenant potentiellement des credentials.

---

# 39. Help d’une commande

Exemple attendu :

```text
COMMAND
  sitefactory validate

DESCRIPTION
  Validates a SiteFactory client configuration without modifying files.

USAGE
  sitefactory validate --config <path> [options]

REQUIRED OPTIONS
  -c, --config <path>
      Path to client.sitefactory.yml.

OPTIONS
  --profile <development|staging|production>
      Validation profile. Default: development.

  --output-normalized-config <path>
      Writes the normalized configuration to a separate file.

GLOBAL OPTIONS
  --output-format <human|json>
  --verbosity <quiet|normal|detailed|diagnostic>
  --no-color
  --non-interactive
  -h, --help

EXAMPLES
  sitefactory validate --config client.sitefactory.yml

  sitefactory validate \
    --config client.sitefactory.yml \
    --profile production
```

---

# 40. Localisation

## 40.1 MVP

Le CLI peut utiliser l’anglais pour :

- commandes ;
- options ;
- codes ;
- sorties techniques.

La documentation utilisateur peut rester en français.

---

## 40.2 Messages

Les messages du MVP peuvent être en anglais afin de :

- rester cohérents avec l’écosystème .NET ;
- faciliter la recherche d’erreurs ;
- simplifier les tests ;
- éviter une couche de traduction prématurée.

Une localisation future ne doit pas modifier les codes d’erreur ou les propriétés JSON.

---

# 41. Compatibilité des plateformes

Le CLI doit fonctionner au minimum sur :

```text
Windows x64
Linux x64
```

macOS peut être supporté si les tests restent raisonnables.

Les chemins, permissions et fins de ligne doivent être testés sur les plateformes officiellement supportées.

---

# 42. Distribution du CLI

## 42.1 Développement

```bash
dotnet run --project src/LogicGate.SiteFactory.Cli
```

---

## 42.2 Outil .NET

Distribution cible possible :

```bash
dotnet tool install \
  --global LogicGate.SiteFactory.Cli \
  --version 0.1.0
```

Le package est publié dans un feed privé LogicGate.

---

## 42.3 Version verrouillée

Les pipelines et projets ne doivent pas installer implicitement la dernière version.

Exemple recommandé :

```text
version exacte
```

Un manifest d’outil local peut être utilisé :

```text
.config/dotnet-tools.json
```

---

## 42.4 Mise à jour

La mise à jour du CLI reste explicite.

Une commande peut signaler qu’une version plus récente existe, sans l’installer automatiquement.

---

# 43. Télémétrie

Le MVP n’envoie aucune télémétrie externe par défaut.

Le CLI peut produire localement :

- rapports ;
- durées ;
- compteurs ;
- codes d’erreur.

Toute télémétrie future doit être :

- explicitement documentée ;
- configurable ;
- limitée ;
- sans secret ;
- sans contenu client ;
- conforme aux règles de confidentialité.

---

# 44. Historique des commandes

Le CLI ne doit pas écrire un historique contenant :

- contenu des configurations ;
- secrets ;
- chemins sensibles inutiles ;
- données personnelles.

Les shells peuvent conserver les commandes saisies.

C’est une raison supplémentaire pour ne pas accepter les secrets dans les arguments.

---

# 45. Configuration de test

Les tests du CLI doivent utiliser :

- répertoires temporaires ;
- configurations fictives ;
- domaines `.test` ;
- faux catalogues ;
- faux runners ;
- aucun repository client réel ;
- aucune production.

---

# 46. Architecture interne du CLI

Structure recommandée :

```text
LogicGate.SiteFactory.Cli/
├── Commands/
│   ├── Doctor/
│   ├── Validate/
│   ├── Plan/
│   ├── Generate/
│   ├── Upgrade/
│   └── Version/
├── Formatting/
├── Mapping/
├── ExitCodes/
├── Composition/
├── Program.cs
└── README.md
```

---

## 46.1 Handler

Un handler doit rester court.

Exemple conceptuel :

```csharp
public sealed class ValidateCommandHandler
{
    private readonly IValidateClientConfigurationUseCase _useCase;
    private readonly ICliResultWriter _resultWriter;

    public async Task<int> ExecuteAsync(
        ValidateCommandOptions options,
        CancellationToken cancellationToken)
    {
        var request = ValidateClientConfigurationRequest.From(options);

        var result = await _useCase.ExecuteAsync(
            request,
            cancellationToken);

        await _resultWriter.WriteAsync(
            result,
            cancellationToken);

        return CliExitCodeMapper.Map(result);
    }
}
```

---

## 46.2 Composition root

`Program.cs` configure :

- parser ;
- commandes ;
- services ;
- formatters ;
- ports d’infrastructure ;
- logging ;
- cancellation.

Il ne contient pas les règles de génération.

---

## 46.3 Writer

Une abstraction de sortie peut gérer :

```text
human
json
```

Exemple conceptuel :

```csharp
public interface ICliResultWriter
{
    Task WriteAsync(
        CliCommandResult result,
        CancellationToken cancellationToken);
}
```

---

# 47. Bibliothèque CLI

Le choix de la bibliothèque CLI doit être documenté avant implémentation.

Critères :

- compatibilité .NET 10 ;
- sous-commandes ;
- parsing strict ;
- help ;
- validations ;
- complétion éventuelle ;
- stabilité ;
- licence ;
- maintenance ;
- tests ;
- support de l’annulation.

L’ajout d’une bibliothèque structurante peut nécessiter une ADR.

Le parser ne doit pas devenir la source de la logique métier.

---

# 48. Tests du parsing

Vérifier notamment :

- commande inconnue ;
- sous-commande inconnue ;
- option inconnue ;
- option obligatoire absente ;
- valeur d’enum invalide ;
- chemin absent ;
- option répétée ;
- combinaison incompatible ;
- `--dry-run` et `--execute` simultanés ;
- `--force` sans commande supportée ;
- confirmation absente ;
- help ;
- version.

---

# 49. Tests de `doctor`

Vérifier :

- environnement prêt ;
- SDK absent ;
- SDK incompatible ;
- Git absent ;
- dossier non inscriptible ;
- catalogue absent ;
- Docker absent ;
- warning non bloquant ;
- sortie JSON ;
- aucun effet durable.

---

# 50. Tests de `validate`

Vérifier :

- configuration minimale valide ;
- configuration complète valide ;
- YAML invalide ;
- schéma invalide ;
- module inconnu ;
- secret détecté ;
- production avec domaine `.test` ;
- aucun fichier cible modifié ;
- sortie JSON valide ;
- code de sortie correct.

---

# 51. Tests de `plan`

Vérifier :

- nouveau dossier ;
- dossier vide ;
- dossier non vide ;
- fichier custom ;
- fichier protected ;
- conflit ;
- ordre déterministe ;
- plan JSON ;
- aucune modification du dossier cible ;
- action Git prévue ou non.

---

# 52. Tests de `generate`

Vérifier :

- refus sans `--dry-run` ou `--execute` ;
- dry-run sans écriture ;
- génération réussie ;
- restore échoué ;
- build échoué ;
- tests échoués ;
- secret détecté ;
- confirmation manquante ;
- annulation ;
- timeout ;
- dossier temporaire ;
- commit après succès uniquement ;
- aucun remote créé.

---

# 53. Tests de `--force`

Vérifier :

- remplacement d’un fichier generated autorisé ;
- fichier custom préservé ;
- fichier protected refusé ;
- secret toujours bloquant ;
- tests toujours bloquants ;
- path traversal toujours bloquant ;
- action forcée présente dans le rapport.

---

# 54. Tests des sorties

## 54.1 Sortie humaine

Vérifier :

- titres ;
- étapes ;
- résumé ;
- erreurs actionnables ;
- absence de codes techniques inutiles pour un succès simple.

---

## 54.2 Sortie JSON

Vérifier :

- JSON valide ;
- schéma stable ;
- aucun texte parasite ;
- exit code cohérent ;
- warnings et erreurs ;
- absence de secret ;
- dates UTC.

---

## 54.3 `quiet`

Vérifier que seules les informations nécessaires sont affichées.

---

## 54.4 `diagnostic`

Vérifier que le niveau diagnostic n’expose toujours pas de secret.

---

# 55. Tests de sécurité

Tester notamment :

```text
--output ../../outside
--config avec path traversal
lien symbolique vers un dossier externe
argument contenant une commande shell
nom de projet contenant des séparateurs
secret dans le YAML
token dans une variable affichée
sortie de processus contenant un credential
```

Le CLI doit refuser ou neutraliser ces cas.

---

# 56. Tests de compatibilité plateforme

Au minimum :

```text
Windows
Linux
```

Vérifier :

- chemins ;
- fins de ligne ;
- exécution de processus ;
- Git ;
- permissions ;
- fichiers temporaires ;
- annulation ;
- couleurs ANSI.

---

# 57. Tests d’intégration

Les tests d’intégration doivent exécuter le CLI dans un processus isolé lorsque pertinent.

Ils vérifient :

- stdout ;
- stderr ;
- code de sortie ;
- fichiers ;
- rapport ;
- comportement non interactif.

---

# 58. Snapshot tests

Des snapshots peuvent être utilisés pour :

- help ;
- sortie JSON ;
- résumé de plan ;
- erreurs principales.

Ils ne doivent pas bloquer toute évolution de formulation sans revue possible.

Les codes, propriétés JSON et comportements restent plus importants que la ponctuation exacte.

---

# 59. Critères d’acceptation du CLI MVP

Le CLI sera considéré comme opérationnel lorsque :

1. `--help` fonctionne ;
2. `--version` fonctionne ;
3. `version` affiche les versions utiles ;
4. `doctor` vérifie l’environnement ;
5. `validate` ne modifie aucun fichier ;
6. `plan` ne modifie aucun fichier ;
7. `generate --dry-run` ne modifie aucun fichier ;
8. `generate --execute` génère un projet ;
9. `generate` refuse sans mode explicite ;
10. les options inconnues sont refusées ;
11. les options obligatoires sont validées ;
12. les chemins sont sécurisés ;
13. les sorties humaines sont lisibles ;
14. la sortie JSON est stable ;
15. les codes de sortie sont cohérents ;
16. le mode non interactif fonctionne ;
17. les confirmations explicites fonctionnent ;
18. `--force` reste limité ;
19. Ctrl+C annule proprement ;
20. les processus externes sont contrôlés ;
21. le restore est exécuté ;
22. le build Release est exécuté ;
23. les tests sont exécutés ;
24. un échec produit un code non nul ;
25. un secret bloque la génération ;
26. le rapport est généré ;
27. Git peut être initialisé ;
28. le commit initial n’est créé qu’après succès ;
29. aucun remote n’est créé automatiquement ;
30. aucune migration production n’est appliquée ;
31. les tests Windows et Linux réussissent ;
32. le build du CLI réussit sans warning injustifié.

---

# 60. Règles pour Codex

## 60.1 Lecture préalable

Avant toute modification du CLI, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../02-architecture/core-module-boundary.md
09-module-architecture.md
10-feature-flags-and-entitlements.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
13-generation-pipeline.md
14-client-configuration-schema.md
15-cli-specification.md
AGENTS.md
```

Ainsi que :

- commandes existantes ;
- handlers ;
- cas d’usage ;
- writers ;
- codes de sortie ;
- tests CLI ;
- documentation utilisateur.

---

## 60.2 Ce que Codex peut faire

Codex peut :

- implémenter une commande validée ;
- ajouter une option demandée ;
- améliorer le parsing ;
- ajouter un formatter ;
- ajouter une sortie JSON ;
- ajouter un code d’erreur ;
- améliorer un diagnostic ;
- ajouter des tests ;
- corriger un comportement non interactif ;
- documenter une commande.

---

## 60.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- ajouter une commande destructive sans validation ;
- mettre la logique métier dans un handler ;
- ajouter un `--yes` global contournant toutes les confirmations ;
- rendre `--force` omnipotent ;
- modifier les prix ;
- modifier les entitlements ;
- ignorer une option inconnue ;
- accepter une ambiguïté de chemin ;
- afficher un secret ;
- passer un secret dans une ligne de commande ;
- créer automatiquement un repository distant ;
- appliquer une migration production ;
- déployer automatiquement ;
- considérer un test non exécuté comme réussi ;
- masquer un code de sortie non nul ;
- transformer une erreur en warning pour rendre la commande verte ;
- ajouter une dépendance CLI structurante sans justification.

---

## 60.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouvelle commande principale ;
- nouvelle commande destructive ;
- changement de code de sortie ;
- changement du schéma JSON de sortie ;
- changement du comportement de `--force` ;
- ajout d’un provider distant ;
- ajout de télémétrie ;
- création de repository distant ;
- déploiement ;
- suppression de module ;
- modification automatique des migrations ;
- changement du parser CLI ;
- changement de langue des sorties ;
- suppression ou renommage d’une option publique.

---

## 60.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Commande concernée
Sous-commandes affectées
Options ajoutées ou modifiées
Valeurs par défaut modifiées
Codes de sortie affectés
Codes d’erreur affectés
Sorties humaines affectées
Schéma JSON affecté
Confirmations affectées
Mode non interactif affecté
Effets de bord
Tests de parsing
Tests d’intégration
Tests de sécurité
Build
Tests
Breaking changes
Décisions restant humaines
```

Codex ne doit pas déclarer une commande opérationnelle sans avoir testé :

- le succès ;
- l’erreur ;
- le code de sortie ;
- le mode non interactif lorsque pertinent.

---

# 61. Anti-patterns interdits

## 61.1 Handler monolithique

Un handler lit directement le YAML, résout les modules, génère les fichiers, lance Git et affiche le résultat.

---

## 61.2 Option `--yes` globale

Une seule option confirme :

- génération ;
- suppression ;
- upgrade ;
- déploiement ;
- restauration.

---

## 61.3 `--force` destructif

```bash
sitefactory generate --force
```

supprime le dossier cible et recrée tout.

---

## 61.4 Succès malgré échec

Le build échoue mais le CLI retourne :

```text
exit code 0
```

---

## 61.5 Help incomplet

Une option existe dans le code mais n’est pas documentée dans le help.

---

## 61.6 Option silencieusement ignorée

L’utilisateur écrit :

```bash
--ouput
```

et la commande continue sans signaler l’erreur.

---

## 61.7 Secret en argument

```bash
sitefactory deploy --password <EXTERNAL_SECRET>
```

---

## 61.8 Sortie JSON polluée

Le CLI écrit des messages colorés avant le JSON.

---

## 61.9 Logique dépendant du terminal

Une commande fonctionne uniquement dans un terminal interactif et échoue sans explication en CI.

---

## 61.10 Commande shell concaténée

Une valeur d’argument est concaténée dans une commande shell libre.

---

## 61.11 Téléchargement arbitraire

```bash
sitefactory generate --template-url https://unknown.example/template.zip
```

dans le MVP.

---

## 61.12 Déploiement caché dans generate

La commande `generate` pousse automatiquement en production après réussite.

---

# 62. Roadmap d’implémentation

## Phase CLI 1 — Bootstrap

```text
--help
--version
version
doctor
codes de sortie
writers human/json
```

---

## Phase CLI 2 — Validation

```text
validate
parsing strict
profil de validation
erreurs structurées
sortie normalisée
```

---

## Phase CLI 3 — Planification

```text
plan
analyse du dossier cible
plan JSON
conflits
actions manuelles
```

---

## Phase CLI 4 — Génération

```text
generate --dry-run
generate --execute
confirmations
dossier temporaire
rapport
```

---

## Phase CLI 5 — Validation technique

```text
restore
build
tests
timeouts
annulation
```

---

## Phase CLI 6 — Git

```text
git init
commit initial
repository status
```

---

## Phase CLI 7 — Upgrade

```text
upgrade check
upgrade plan
upgrade apply
```

---

## Phase CLI 8 — Modules

```text
module list
module info
module add
désactivation contrôlée
```

Les commandes de déploiement et de repository distant restent hors MVP initial.

---

# 63. Décisions validées

La baseline du CLI est la suivante :

```text
Le nom de l’exécutable est sitefactory.

Le CLI est le premier point d’entrée officiel du produit.

Les commandes utilisent des noms et options en kebab-case.

Les handlers CLI restent fins.

Les cas d’usage appartiennent à la couche Application.

Validate, plan et dry-run ne modifient aucun fichier cible.

Generate exige un mode explicite : --dry-run ou --execute.

Les sorties supportent human et json.

Les codes de sortie sont stables et non nuls en cas d’échec.

Les propriétés inconnues et options inconnues sont refusées.

Le mode non interactif exige des confirmations explicites.

Les confirmations sensibles utilisent des options spécifiques.

--force possède un périmètre limité.

--force ne contourne pas les contrôles critiques.

Les chemins sont normalisés et sécurisés.

Les processus externes utilisent des arguments structurés.

Les secrets ne sont jamais acceptés dans les arguments.

Le CLI répond à l’annulation et aux timeouts.

Le commit initial n’est créé qu’après restore, build,
tests et secret scan réussis.

La génération peut initialiser Git localement.

Le repository distant n’est pas créé automatiquement.

Le déploiement reste séparé de la génération.

Les migrations de production ne sont jamais appliquées par le CLI
de génération.

Aucune télémétrie externe n’est envoyée par défaut.

Le CLI est testé au minimum sur Windows et Linux.

Codex ne peut pas ajouter seul une commande destructive
ou modifier un contrat public du CLI.
```

---

# 64. Checklist de revue d’une commande

## Contrat

```text
[ ] Nom cohérent
[ ] Intention claire
[ ] Effets de bord identifiés
[ ] Options documentées
[ ] Valeurs par défaut sûres
[ ] Help disponible
```

## Parsing

```text
[ ] Options inconnues refusées
[ ] Arguments obligatoires vérifiés
[ ] Enums validés
[ ] Combinaisons incompatibles refusées
[ ] Chemins validés
```

## Sécurité

```text
[ ] Aucun secret accepté
[ ] Aucun secret affiché
[ ] Aucun shell concaténé
[ ] Path traversal refusé
[ ] Confirmations adaptées
[ ] --force limité
```

## Exécution

```text
[ ] CancellationToken transmis
[ ] Timeout défini lorsque nécessaire
[ ] Code de sortie contrôlé
[ ] Processus externe terminé
[ ] Effets partiels traités
```

## Sortie

```text
[ ] Sortie humaine lisible
[ ] Sortie JSON valide
[ ] Stderr maîtrisé
[ ] Aucun texte parasite en JSON
[ ] Résumé final cohérent
```

## Tests

```text
[ ] Succès
[ ] Arguments invalides
[ ] Échec métier
[ ] Échec technique
[ ] Mode non interactif
[ ] Annulation
[ ] Sécurité
[ ] Code de sortie
```

---

# 65. Checklist de revue du CLI complet

```text
[ ] Help global
[ ] Help par commande
[ ] Version
[ ] Doctor
[ ] Validate
[ ] Plan
[ ] Generate dry-run
[ ] Generate execute
[ ] Output human
[ ] Output JSON
[ ] Codes de sortie
[ ] Confirmations
[ ] Non-interactive
[ ] Annulation
[ ] Timeouts
[ ] Sécurité des chemins
[ ] Sécurité des processus
[ ] Aucun secret
[ ] Tests Windows
[ ] Tests Linux
[ ] Build Release
[ ] Aucun warning injustifié
[ ] Documentation à jour
```

---

# 66. Conclusion

Le CLI SiteFactory doit fournir une interface claire entre l’opérateur et les cas d’usage du moteur.

Il doit permettre de :

- vérifier ;
- planifier ;
- générer ;
- diagnostiquer ;
- préparer les mises à niveau ;

sans masquer les risques ni contourner les validations.

Le contrat principal du CLI repose sur quatre règles :

```text
Une commande décrit clairement son intention.

Une opération sensible est planifiée avant exécution.

Une erreur produit un code non nul et un diagnostic actionnable.

Une commande ne prétend jamais avoir exécuté
ce qu’elle n’a pas réellement validé.
```

> Le CLI ne doit pas rendre les opérations dangereuses plus faciles. Il doit rendre les opérations correctes, reproductibles et vérifiables.
