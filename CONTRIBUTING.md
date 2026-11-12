# Contribuer à LogicGate SiteFactory

## 1. Objet

Ce guide s’applique aux développeurs, reviewers, mainteneurs, agents IA et outils
automatisés. Une contribution doit être bornée, compréhensible, testée, conforme
aux ADR, sécurisée, documentée, réversible lorsque nécessaire et sans effet
implicite sur la production.

Le code original est sous [Apache-2.0](LICENSE), avec [NOTICE](NOTICE).
Le public se construit sans dépendance propriétaire obligatoire; les contrats
permettent aux consommateurs de composer leurs propres modules. La licence
ne présume aucun droit sur les contenus tiers ou les sorties générées.

Contribuer n’autorise ni accès à un projet client ou à des données réelles, ni
publication de package, création de repository distant, déploiement, migration
ou modification d’infrastructure. Ces opérations nécessitent leur autorisation.

## 2. Documents à lire

Lire [README](README.md), [AGENTS](AGENTS.md) et l’[index ADR](docs/adr/README.md),
puis les documents applicables au périmètre; pas tous les ADR pour chaque correction.

### 2.1 Vision et produit

[Capacités et limites](README.md#capacités-disponibles), [CHANGELOG](CHANGELOG.md).

### 2.2 Architecture

[Composants, frontières et contrats](docs/02-architecture/).

### 2.3 Génération

[CLI et guides actuels](README.md#cli-publique--validate-plan-generate).

### 2.4 Qualité et sécurité

[Exigences](docs/04-quality-security/), [contrat CI actuel](scripts/public-ci/README.md).

### 2.5 Exploitation

[Opérations](docs/05-operations/): contrats cibles, pas outils ou exploitation livrés.

### 2.6 Travail avec Codex

[Instructions](AGENTS.md), [guide et modèles](docs/06-codex/).

### 2.7 Décisions architecturales

[ADR](docs/adr/README.md): lire les décisions du domaine concerné; leur acceptation
ne prouve pas l’implémentation des capacités cibles.

## 3. Sources de vérité

Appliquer l’[ordre des sources de vérité](AGENTS.md#6-sources-de-vérité) et la
conduite à tenir en cas de conflit. Le code ne remplace pas silencieusement une
décision acceptée. Identifier si la divergence vient du code ou d’une documentation
obsolète; corriger le défaut ou proposer une nouvelle décision, sans réécrire
l’histoire ni arbitrer implicitement un conflit architectural.

## 4. État actuel du projet

[SiteFactory.slnx](SiteFactory.slnx) contient **12 projets de production et 11 de
tests**. Le [README](README.md) décrit les capacités livrées: moteur .NET 10,
CLI validate/plan/generate, DocumentationModule, API documentaire C# et
bibliothèques runtime/PostgreSQL génériques. Runtime.Contracts/Runtime restent
purs, sans IConfiguration/DI/Hosting, EF ou Npgsql; composition explicite au
build, sans découverte de DLL au runtime ([ADR-0037](docs/adr/0037-compose-modules-explicitly-without-dynamic-loading.md)).

La version de développement est déclarée dans [Directory.Build.props](Directory.Build.props);
les évolutions restent sous [Unreleased](CHANGELOG.md#unreleased). Aucun package
NuGet ni aucune release officielle ne sont publiés. La CI publique Windows/Linux/PostgreSQL est
opérationnelle, SourceLink VERIFIED; voir [CI et provenance](#321-checks-attendus).
Les documents cibles ne valent ni livraison ni autorisation d’exécution.

Inspecter les projets, fichiers et conventions réels avant de choisir un nom de
solution, créer un projet, ajouter une dépendance, modifier une convention ou
exécuter une commande.

# 5. Principes de contribution

## 5.1 Une contribution, un objectif principal

Une branche et une pull request doivent avoir un objectif principal clairement identifiable.

Éviter de mélanger :

```text
nouvelle fonctionnalité

refactoring global

mise à jour de dépendances

formatage massif

changement d’architecture

documentation sans rapport
```

Une correction locale ne doit pas devenir une réécriture complète du module.

---

## 5.2 Main toujours intégrable

La branche :

```text
main
```

doit rester :

- compilable ;
- testable ;
- documentée ;
- compatible avec les quality gates ;
- utilisable comme base d’une nouvelle tâche.

Une pull request ne doit pas être fusionnée si elle laisse volontairement `main` dans un état cassé.

---

## 5.3 Changements courts

Les branches doivent rester courtes.

Une tâche longue doit être découpée en étapes verticales pouvant être :

- revues ;
- testées ;
- fusionnées ;
- utilisées indépendamment.

---

## 5.4 Pas de production implicite

Une contribution peut créer :

- un script de déploiement ;
- une commande de migration ;
- un outil de backup ;
- un orchestrateur ;
- un runbook.

Elle ne doit pas exécuter ces opérations sur un environnement réel sans autorisation distincte.

---

## 5.5 Sécurité non négociable

Une contribution ne doit pas affaiblir un contrôle pour faciliter l’implémentation ou faire passer la CI.

Sont notamment interdits :

```text
désactiver un analyzer

désactiver TLS

ignorer un test de sécurité

ajouter chmod 777

utiliser latest

exposer PostgreSQL

ajouter un secret dans Git

rendre un conteneur privileged

exécuter automatiquement les migrations
```

---

# 6. Préparer une tâche

## 6.1 Definition of Ready

Une tâche est prête à être développée lorsqu’elle indique suffisamment clairement :

```text
contexte

objectif

périmètre

hors portée

ADR applicables

comportements attendus

critères d’acceptation

tests attendus

risques ou contraintes

format du compte rendu
```

Le template recommandé est :

```text
docs/06-codex/29-codex-task-specification-template.md
```

---

## 6.2 Identifiant de tâche

Les tâches SiteFactory utilisent une convention telle que :

```text
SF-TASK-001
```

L’identifiant doit être conservé dans :

- le titre ou la description de la branche ;
- la pull request ;
- les rapports pertinents ;
- le changelog lorsque nécessaire.

Il n’est pas obligatoire de répéter l’identifiant dans chaque commit si la pull request le référence clairement.

---

## 6.3 Périmètre

Une tâche doit distinguer :

### Inclus

Ce qui doit être implémenté ou modifié.

### Hors portée

Ce qui ne doit pas être traité dans la même contribution.

### Décisions déjà prises

Les comportements imposés par les ADR.

### Décisions restantes

Les points nécessitant encore une validation humaine.

---

## 6.4 Tâche trop large

Une tâche doit être découpée lorsqu’elle demande simultanément, par exemple :

```text
le bootstrap complet

tous les modules

l’administration

la sécurité

Docker

la production

les 500 tâches du backlog
```

Le découpage doit privilégier une vertical slice démontrable.

---

# 7. Environnement de développement

## 7.1 Prérequis actuels

- SDK .NET 10 selon [global.json](global.json), actuellement 10.0.301,
  avec sa politique de roll-forward ; pas de version choisie implicitement.
- Git ; PowerShell 7 pour les scripts publics.
- Éditeur/IDE compatible .NET ; développement Windows x64 ou Linux x64.

**Ni Docker ni PostgreSQL ne sont requis pour restore/build et les suites
offline ordinaires.** Docker Engine Linux ou Docker Desktop en mode conteneurs
Linux devient nécessaire seulement pour une campagne PostgreSQL REQUIRED
explicitement demandée. Docker Compose client n'est pas un prérequis livré.

Les politiques de production du présent guide sont des contraintes de sécurité,
pas l'annonce d'un déploiement client disponible.

---

## 7.2 Vérification des versions

Avant une modification structurante, vérifier les versions déclarées dans le repository :

```text
global.json

Directory.Build.props

Directory.Packages.props

packages.lock.json

Dockerfiles

workflows CI
```

Ne pas mettre à jour une version majeure dans une tâche sans rapport.

---

## 7.3 Configuration locale

La configuration locale doit utiliser :

- des valeurs fictives ;
- des secrets locaux non commités ;
- des bases de développement isolées ;
- des providers fakes ou locaux.

Ne jamais réutiliser :

- une base de production ;
- un credential client ;
- un SMTP réel non prévu ;
- un token de registry production ;
- une donnée client.

---

# 8. Modèle de branches

## 8.1 Branche principale

La seule branche longue durée du MVP est :

```text
main
```

Une branche `develop` permanente n’est pas prévue par défaut.

---

## 8.2 Branches de travail

Conventions recommandées :

```text
feat/sf-task-001-bootstrap-solution

fix/sf-task-123-preserve-custom-files

docs/sf-task-150-update-cli-contract

test/sf-task-210-add-postgres-role-tests

refactor/sf-task-250-split-generation-planner

security/sf-task-300-harden-webhook-signature

ci/sf-task-350-add-container-scan

hotfix/sf-task-400-fix-production-blocker
```

---

## 8.3 Règles de nommage

Une branche doit utiliser :

- des minuscules ;
- des tirets ;
- un identifiant de tâche lorsque disponible ;
- une description courte ;
- aucun espace ;
- aucun nom de personne ;
- aucune donnée client sensible.

---

## 8.4 Une branche par tâche

Une branche ne doit pas accumuler plusieurs tâches non liées.

Lorsqu’une seconde correction indépendante est découverte :

- créer une nouvelle tâche ;
- créer une nouvelle branche ;
- ou demander explicitement l’élargissement du périmètre.

---

## 8.5 Synchronisation avec `main`

Avant d’ouvrir la pull request :

```bash
git fetch origin
git rebase origin/main
```

ou utiliser la stratégie validée par le repository.

Le contributeur doit résoudre les conflits localement et relancer les tests.

Éviter les commits répétés de type :

```text
merge main into feature
```

lorsqu’un rebase propre est possible et autorisé.

---

# 9. Commits

## 9.1 Convention

Les commits utilisent une convention proche de Conventional Commits.

Types recommandés :

```text
feat

fix

docs

test

refactor

perf

build

ci

chore

revert
```

---

## 9.2 Exemples

```text
feat(cli): add client configuration validation

fix(generator): preserve custom files

docs(adr): index accepted architecture decisions

test(postgres): cover web runtime denied privileges

refactor(application): extract generation plan builder

build(container): pin runtime image by digest

ci(security): add image secret scanning
```

---

## 9.3 Scope

Le scope doit identifier un domaine stable.

Exemples :

```text
cli

generator

configuration

modules

postgres

identity

jobs

security

container

deployment

docs
```

---

## 9.4 Message

Le sujet du commit doit :

- être court ;
- décrire le résultat ;
- utiliser l’impératif ou une formulation directe ;
- éviter « update », « changes » ou « fix stuff » sans précision.

---

## 9.5 Corps du commit

Ajouter un corps lorsque le changement nécessite d’expliquer :

- le contexte ;
- une décision ;
- une incompatibilité ;
- une migration ;
- un risque ;
- un comportement non évident.

---

## 9.6 Commits de travail

Les commits intermédiaires sont autorisés sur la branche.

Avant fusion, la pull request peut être squashée afin de conserver un historique principal lisible.

---

## 9.7 Secrets

Ne jamais inclure un secret dans :

- un commit ;
- un message ;
- un diff ;
- une capture de logs ;
- un fichier supprimé ensuite.

Supprimer le fichier dans un commit ultérieur ne retire pas le secret de l’historique.

---

# 10. Stratégie de fusion

## 10.1 Pull request obligatoire

Les modifications de `main` doivent passer par une pull request.

Cela s’applique également au mainteneur principal lorsqu’il travaille seul.

---

## 10.2 Squash merge

La stratégie par défaut est :

```text
squash merge
```

Le titre de la pull request devient le message principal du commit fusionné.

---

## 10.3 Merge commit

Un merge commit peut être utilisé exceptionnellement pour :

- une branche de release ;
- une intégration conservant volontairement plusieurs commits significatifs ;
- un historique de migration nécessitant cette structure.

La justification doit apparaître dans la pull request.

---

## 10.4 Rebase merge

Le rebase merge n’est pas la stratégie par défaut.

Il peut être utilisé lorsqu’un historique de commits propre a été préparé et que cela apporte une valeur réelle.

---

## 10.5 Suppression de la branche

Après fusion, la branche distante doit être supprimée sauf besoin opérationnel documenté.

---

# 11. Contribution lorsque le mainteneur est seul

SiteFactory peut être maintenu initialement par une seule personne.

Dans ce cas, la baseline reste :

```text
branche dédiée

pull request

CI complète

auto-revue formalisée

fusion explicite
```

---

## 11.1 Protection de branche

La protection de `main` devrait imposer :

- passage par pull request ;
- checks obligatoires ;
- résolution des conversations ;
- branche à jour selon le besoin ;
- interdiction des force pushes ;
- interdiction de suppression.

Elle ne doit pas exiger une approbation externe impossible à obtenir en permanence.

---

## 11.2 Auto-revue

Le mainteneur doit effectuer une seconde lecture distincte de l’implémentation.

La pull request doit inclure une section :

```text
Auto-revue effectuée :
oui
```

et indiquer :

- ADR relus ;
- tests ;
- risques ;
- éléments hors portée.

---

## 11.3 Revue par agent

Une revue Codex peut compléter l’auto-revue.

Elle ne remplace pas une décision humaine pour :

- la production ;
- une migration destructive ;
- une dérogation de sécurité ;
- un changement d’architecture majeur.

---

## 11.4 Changements sensibles

Une revue humaine indépendante doit être recherchée lorsqu’elle est raisonnablement disponible pour :

- authentification ;
- autorisation ;
- cryptographie ;
- migrations destructives ;
- scripts de production ;
- suppression de données ;
- secrets ;
- conteneurs ;
- rollback ;
- supply chain.

---

# 12. Workflow de développement

## 12.1 Créer la branche

```bash
git switch main
git pull --ff-only
git switch -c feat/sf-task-001-bootstrap-solution
```

Adapter le préfixe au type de tâche.

---

## 12.2 Inspecter avant de modifier

Rechercher :

- les conventions ;
- les projets existants ;
- les patterns similaires ;
- les tests ;
- les ADR ;
- les fichiers `AGENTS.md` locaux ;
- les dépendances existantes.

---

## 12.3 Implémenter par petits incréments

Ordre recommandé :

```text
contrats

test ou scénario de preuve

implémentation minimale

cas d’erreur

documentation

validation globale
```

---

## 12.4 Vérifier le diff

Avant commit :

```bash
git status
git diff
```

Avant pull request :

```bash
git diff origin/main...HEAD
```

Vérifier notamment :

- fichiers inattendus ;
- formatage global ;
- secrets ;
- artefacts temporaires ;
- données locales ;
- modifications hors périmètre.

---

# 13. Style de code

## 13.1 Langue du code

Les identifiants de code sont rédigés en anglais :

- namespaces ;
- classes ;
- méthodes ;
- variables ;
- Error Codes ;
- contrats ;
- noms de projets.

La documentation utilisateur et architecturale est principalement rédigée en français.

---

## 13.2 Conventions .NET

En l’absence de règle locale plus précise :

```text
nullable activé

warnings traités

async pour les I/O

CancellationToken propagé

types explicites pour les contrats

immutabilité privilégiée

dépendances injectées

méthodes courtes et cohérentes
```

---

## 13.3 Commentaires

Ajouter un commentaire lorsqu’il explique :

- une contrainte ;
- un invariant ;
- un compromis ;
- une limitation ;
- une raison non visible dans le code.

Éviter les commentaires répétant simplement l’instruction.

---

## 13.4 Formatage

Le formatage doit suivre :

```text
.editorconfig
```

et les règles du repository.

Le formatage global d’un fichier ou projet sans lien avec la tâche doit être évité.

---

## 13.5 Warnings

Un warning nouveau doit être :

- corrigé ;
- ou justifié par une règle explicite et localisée.

Ne pas désactiver globalement un warning pour corriger un seul cas.

---

# 14. Architecture du code

## 14.1 Domain

Domain contient :

- concepts métier ;
- value objects ;
- invariants ;
- événements métier ;
- contrats indépendants de l’infrastructure.

Domain ne dépend pas :

- d’EF Core ;
- d’ASP.NET Core ;
- de YAML ;
- de Docker ;
- de `HttpClient` ;
- du filesystem ;
- d’un provider.

---

## 14.2 Application

Application contient :

- commands ;
- queries ;
- handlers ;
- ports ;
- validation fonctionnelle ;
- autorisation métier ;
- transactions déclarées ;
- résultats typés.

---

## 14.3 Infrastructure

Infrastructure contient :

- EF Core ;
- Npgsql ;
- fichiers ;
- Git ;
- YAML concret ;
- clients HTTP ;
- SMTP ;
- cache ;
- jobs ;
- secrets ;
- génération.

---

## 14.4 Presentation et CLI

La couche d’entrée contient :

- parsing ;
- binding ;
- validation syntaxique ;
- affichage ;
- statuts HTTP ;
- codes de sortie.

La logique métier principale ne doit pas être implémentée directement dans les endpoints ou handlers CLI.

---

# 15. Génération de fichiers

## 15.1 Corriger la source

Lorsqu’un fichier généré est incorrect, corriger :

- le template ;
- le moteur ;
- le module ;
- le modèle de génération.

Ne pas corriger uniquement l’output si celui-ci est régénérable.

---

## 15.2 Ownership

Respecter exactement les cinq stratégies de [ADR-0005](docs/adr/0005-file-generation-strategies.md) :

```text
generated

create-once

merge

custom

protected
```

Les anciens noms `owned`, `preserve`, `extension-point` et `manual` ne sont pas des alias de stratégie. `preserve` reste une action de plan, pas une stratégie de propriété.

---

## 15.3 Fichiers `generated`

Une modification manuelle d'un fichier de stratégie `generated` doit être évitée.

Le générateur doit produire directement le bon résultat.

Un remplacement exige la preuve que le fichier correspond encore au dernier contenu généré ; aucune modification locale ne doit être écrasée silencieusement.

---

## 15.4 Fichiers `custom`

Les fichiers de stratégie `custom` appartiennent exclusivement au client. SiteFactory ne doit pas les créer, modifier ou supprimer automatiquement.

Les tests doivent vérifier leur préservation pendant :

- une régénération ;
- un upgrade ;
- un changement de template.

---

## 15.5 Golden files

Les golden files doivent :

- contenir des données fictives ;
- être déterministes ;
- exclure les secrets ;
- normaliser les chemins et timestamps ;
- être mis à jour uniquement pour un changement voulu.

---

# 16. Configuration et YAML

Toute nouvelle propriété de configuration doit inclure :

```text
type

description

valeur requise ou facultative

valeur par défaut éventuelle

validation

exemple

impact sur le lock

impact sur la génération

tests
```

---

## 16.1 Secrets

Une propriété secrète n’appartient pas à :

```text
client.sitefactory.yml
```

Le fichier référence éventuellement un `Secret ID`, jamais la valeur.

---

## 16.2 Propriétés inconnues

Les contrats stricts doivent détecter les propriétés inconnues.

---

## 16.3 Compatibilité

Une propriété requise ajoutée à un schéma existant peut constituer un breaking change.

Un plan d’upgrade est alors nécessaire.

---

# 17. PostgreSQL

## 17.1 Tests réels

Toute contribution dépendant d’un comportement PostgreSQL doit utiliser une instance réelle pour ses tests d’intégration.

Cela concerne notamment :

- contraintes ;
- transactions ;
- concurrence ;
- locks ;
- rôles ;
- fonctions ;
- migrations ;
- isolation ;
- default privileges.

---

## 17.2 Pas de base en mémoire comme preuve

Une base en mémoire peut servir à un test local de logique indépendante.

Elle ne remplace pas les tests PostgreSQL lorsqu’une garantie dépend du moteur.

---

## 17.3 Schémas

Les objets doivent utiliser les schémas prévus.

Ne pas placer tous les objets applicatifs dans :

```text
public
```

---

# 18. Migrations

## 18.1 Création

Une migration doit être :

- générée depuis le modèle attendu ;
- relue ;
- nommée clairement ;
- testée sur PostgreSQL ;
- accompagnée de son impact.

---

## 18.2 Contenu attendu

La pull request doit indiquer :

```text
schémas ou tables affectés

risque de lock

risque de rewrite

compatibilité avec la release précédente

données transformées

réversibilité

backup requis ou non
```

---

## 18.3 Application

Le contributeur ne doit jamais appeler dans le runtime web ou worker :

```csharp
Database.Migrate();
Database.EnsureCreated();
```

---

## 18.4 Production

Une migration présente dans une pull request ne doit pas être appliquée à la production dans le cadre de la revue de code.

---

## 18.5 Migration destructive

Une migration supprimant ou transformant irréversiblement des données nécessite :

- plan explicite ;
- tests ;
- backup ;
- décision humaine ;
- stratégie de correction ou restore.

---

# 19. Transactions et concurrence

Une commande métier doit définir :

- sa frontière transactionnelle ;
- sa stratégie de concurrence ;
- son idempotence ;
- son audit ;
- son outbox.

---

## 19.1 Appels externes

Ne jamais exécuter dans une transaction PostgreSQL :

- SMTP ;
- CRM ;
- webhook ;
- AI ;
- object storage externe ;
- attente longue.

---

## 19.2 Concurrence optimiste

Les agrégats éditables utilisent une Version attendue.

Un conflit doit être retourné explicitement.

---

## 19.3 Contraintes

Une validation applicative d’unicité ne remplace pas un index ou une contrainte PostgreSQL.

---

# 20. Jobs et outbox

Toute évolution de Job Type doit préciser :

```text
JobType

ContractVersion

compatibilité

retry

timeout

idempotence

interruptibilité

payload

données personnelles
```

---

## 20.1 Payload

Le payload doit rester minimal.

Éviter :

- entité complète ;
- fichier ;
- secret ;
- URL arbitraire ;
- ancien snapshot inutile.

---

## 20.2 Retrait d’un handler

Un handler ne doit pas être retiré tant que des jobs compatibles peuvent encore exister.

Le déploiement et le rollback doivent être analysés.

---

# 21. HTTP et API

Une contribution API doit préciser :

```text
route

version

méthode

request DTO

response DTO

statuts

Error Codes

permission

concurrence

idempotence

rate limit

cache
```

---

## 21.1 Problem Details

Les erreurs API utilisent :

```text
application/problem+json
```

et un Error Code stable.

---

## 21.2 Mutation

Une mutation ne doit jamais utiliser :

```text
GET
```

ou :

```text
HEAD
```

---

## 21.3 Entités

Ne pas utiliser directement une entité EF Core comme contrat HTTP.

---

## 21.4 Contrats publics

Une modification incompatible doit :

- être détectée ;
- être documentée ;
- utiliser une nouvelle version ;
- ou posséder un plan de migration.

---

# 22. Sécurité

## 22.1 Données de test

Utiliser exclusivement :

- données fictives ;
- domaines réservés aux exemples ;
- tokens factices ;
- secrets marqueurs clairement invalides.

---

## 22.2 Secret scanning

Avant la pull request, vérifier qu’aucun secret n’a été ajouté.

Une détection doit être traitée avant la fusion.

---

## 22.3 Authentification et autorisation

Toute nouvelle action administrative doit préciser :

- permission ;
- antiforgery ;
- step-up éventuel ;
- audit ;
- concurrence ;
- validation.

---

## 22.4 Cryptographie

Ne pas créer un algorithme cryptographique maison.

Utiliser les primitives et bibliothèques approuvées.

---

## 22.5 Validation TLS

Ne jamais ajouter une callback acceptant tous les certificats.

---

# 23. Données personnelles

Une contribution ajoutant une donnée persistante doit mettre à jour lorsque nécessaire :

- Privacy Data Catalog ;
- classification ;
- finalité ;
- Retention Policy ;
- export ;
- suppression ;
- backup treatment ;
- documentation.

---

## 23.1 Logs

Les logs ne doivent pas conserver les données métier comme substitut d’un historique.

---

## 23.2 Suppression

Une suppression ne doit pas être considérée complète uniquement parce qu’une ligne principale a disparu.

Examiner :

- relations ;
- médias ;
- jobs ;
- exports ;
- caches ;
- fournisseurs ;
- backups.

---

# 24. Intégrations externes

Toute nouvelle intégration doit posséder :

```text
Integration ID

Operation IDs

destination

Secret IDs

Data Categories

timeouts

retries

idempotence

kill switch

tests
```

---

## 24.1 URLs utilisateur

Une URL fournie par un utilisateur ne doit pas être appelée directement par le serveur.

---

## 24.2 Clients typés

Utiliser les clients et abstractions prévus.

Ne pas disperser :

```csharp
new HttpClient()
```

dans les modules.

---

## 24.3 Providers réels

Les tests utilisent :

- fakes ;
- serveurs locaux ;
- mocks contrôlés.

Ils ne doivent pas appeler un provider de production.

---

# 25. Conteneurs

Une modification de Dockerfile ou Compose doit vérifier :

```text
image de base épinglée

runtime non-root

root filesystem read-only

cap_drop ALL

no-new-privileges

aucun privileged

aucun Docker socket

aucun port PostgreSQL public

volumes explicites

health checks
```

---

## 25.1 Images

Les images de production ne doivent pas contenir :

- SDK ;
- source ;
- tests ;
- credentials ;
- package manager inutile ;
- outils d’administration inutiles.

---

## 25.2 Build secrets

Les secrets de build utilisent le mécanisme prévu par BuildKit.

Ils ne doivent pas être passés par un `ARG` persistant.

---

# 26. Documentation

Une modification de contrat doit mettre à jour la documentation correspondante.

Exemples :

```text
nouvelle commande CLI

nouvelle propriété YAML

nouveau Module ID

nouvel Error Code

nouvelle route

nouveau manifest

nouvelle procédure

nouvelle migration
```

---

## 26.1 Langue

La documentation principale est rédigée en français.

Les noms de fichiers techniques, identifiants et contrats restent généralement en anglais.

---

## 26.2 Liens

Les liens Markdown doivent être relatifs lorsque cela est adapté.

Un lien cassé doit être corrigé avant fusion.

---

## 26.3 Exemples

Les exemples doivent rester :

- fictifs ;
- cohérents ;
- exécutables lorsque présentés comme tels ;
- sans secret ;
- à jour.

---

# 27. Architecture Decision Records

## 27.1 Quand créer un ADR

Un nouvel ADR est généralement requis pour :

- nouvelle technologie structurante ;
- nouvelle base ou stockage ;
- nouvelle frontière de module ;
- nouveau modèle de sécurité ;
- nouvelle stratégie de déploiement ;
- nouvelle dépendance Infrastructure majeure ;
- changement d’un invariant accepté ;
- décision coûteuse à inverser.

---

## 27.2 Quand ne pas créer un ADR

Un ADR n’est généralement pas nécessaire pour :

- correction locale ;
- renommage interne ;
- test supplémentaire ;
- refactoring sans changement de contrat ;
- implémentation déjà définie par un ADR existant.

---

## 27.3 Modification d’un ADR accepté

Une décision acceptée ne doit pas être réécrite silencieusement.

Créer un nouvel ADR lorsqu’une décision change réellement.

---

# 28. Dépendances

Toute nouvelle dépendance doit être justifiée dans la pull request.

Vérifier :

- utilité ;
- alternative standard ;
- licence ;
- maintenance ;
- vulnérabilités ;
- transitives ;
- taille ;
- compatibilité .NET ;
- compatibilité runtime ;
- supply chain.

---

## 28.1 Version

Les versions de release ne doivent pas être flottantes.

---

## 28.2 Mise à jour

Une mise à jour de dépendance doit indiquer :

- version source ;
- version cible ;
- raison ;
- breaking changes ;
- vulnérabilités corrigées ;
- tests réalisés.

---

## 28.3 Dépendance abandonnée

Une bibliothèque peu maintenue ou sans provenance claire doit être évitée.

---

# 29. Tests

## 29.1 Tests requis

Une modification fonctionnelle doit posséder les tests adaptés.

Types possibles :

```text
unitaires

intégration

PostgreSQL

génération

golden files

contrats

sécurité

concurrence

Docker

CLI

documentation
```

---

## 29.2 Cas négatifs

Tester également les refus et échecs.

Exemples :

```text
configuration invalide

permission refusée

propriété inconnue

version concurrente

timeout

provider indisponible

secret absent

path traversal

mauvais environnement
```

---

## 29.3 Correction de bug

Une correction devrait inclure un test reproduisant le bug avant correction.

---

## 29.4 Tests déterministes

Les tests ne doivent pas dépendre inutilement :

- de l’heure réelle ;
- du réseau Internet ;
- de la locale de la machine ;
- de l’ordre d’exécution ;
- d’un provider réel ;
- d’un fichier utilisateur.

---

## 29.5 Tests ignorés

Un test ne doit pas être désactivé pour faire passer la CI.

Une quarantaine temporaire nécessite :

- une justification ;
- une tâche de correction ;
- un propriétaire ;
- une expiration ;
- une validation humaine.

---

# 30. Commandes de validation

Depuis la racine, sur la solution publique :

```powershell
dotnet restore SiteFactory.slnx --locked-mode
dotnet format SiteFactory.slnx --verify-no-changes --no-restore
dotnet build SiteFactory.slnx --configuration Release --no-restore
dotnet test SiteFactory.slnx --configuration Release --no-build --no-restore --filter "Category!=PostgreSql"
```

Le filtre conserve les onze suites offline et exclut les Facts PostgreSQL réels :
il n'affirme pas leur réussite. Les tests ordinaires ne doivent ni recevoir de
credential DB ni dépendre de Docker. Un changement fonctionnel exige toujours
ses tests pertinents et les cas négatifs, sans suppression de scénario ou
assouplissement pour obtenir du vert.

---

## 30.1 Guards et preuve publique isolée

Les tests architecturaux sont déjà inclus dans les suites offline. Pour cibler
leurs véritables classes après build :

```powershell
dotnet test tests/LogicGate.SiteFactory.Application.Tests/LogicGate.SiteFactory.Application.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~ProjectReferenceArchitectureTests|FullyQualifiedName~CoreModuleBoundaryTests"
dotnet test tests/LogicGate.SiteFactory.ModuleSdk.Tests/LogicGate.SiteFactory.ModuleSdk.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~DependencyBoundaryTests"
pwsh -NoProfile -File tests/PublicCi/Test-PublicCi.ps1
```

Le [guard public](tests/PublicCi/Test-PublicCi.ps1) utilise YamlDotNet déjà
restauré et vérifie composition fermée, workflows/scripts, références/ressources
et contre-exemples synthétiques. Conserver les assertions exactes et négatives.

Pour une validation complète isolée, locale et sans PostgreSQL, utiliser un
dossier de preuves neuf et l’identité du repository public:

```powershell
pwsh -NoProfile -File scripts/public-ci/Invoke-PublicCi.ps1 -Mode Offline -PublicRepository fi8Git/logicgate-sitefactory -ProofDirectory ./artifacts/public-ci-local-proof
```

Le dossier ne doit pas déjà exister. Le harnais vérifie le checkout et son SHA,
les 23 projets et les entrées autorisées avant copie dans un scratch possédé,
extérieur au Git source. Il vérifie les octets copiés, utilise un cache NuGet
neuf, puis exécute restore verrouillé, guards, format, build, contrôle des
symboles/binaires, discovery et onze suites offline. Aucun PostgreSQL n’est lancé.
La preuve SourceLink porte sur les PDB réellement produits et leur mapping;
un passage local ne prouve pas l’autre OS ni un résultat GitHub Actions.
Voir le [contrat CI](scripts/public-ci/README.md) pour provenance, diagnostics
expurgés, isolation et cleanup.

## 30.2 PostgreSQL REQUIRED distinct

Après restore/build Release, avec PowerShell 7 et Docker Linux, seulement pour
une vraie campagne explicitement voulue:

```powershell
pwsh -NoProfile -File tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired
```

Le [harnais P04](tests/PostgreSql/README.md) crée PostgreSQL **18.6 / 180006**,
image par digest approuvé, port loopback dynamique, credentials/certificats
éphémères et **TLS VerifyFull**. Docker doit pouvoir obtenir cette image et
gérer uniquement les ressources possédées. Aucune base développeur, partagée ou
de production; aucune désactivation TLS.

Inventaires fermés: **20 Facts Infrastructure + 3 Hosts**; cinq shards séquentiels
**g04=5, g05=3, p01=8, p02=4, p03=3**. Budget **360 s par invocation dotnet test**,
avec budgets auxiliaires distincts, pas 360 s pour toute la campagne. Aucun
paramètre NoBuild/Shard/UseConfiguredInstance. Sans RunRequired: discovery hors
base uniquement, jamais une preuve PostgreSQL.

Secrets transmis par référence de fichier au processus, jamais en argument ou
dans Git. Exiger **23 Passed** exacts et distincts, cinq TRX neufs: provenance,
identités, multiplicités, résultats, compteurs et durées contrôlés, XML décodé
vérifié avant archivage expurgé. Ne jamais publier un TRX sensible. Secret ou
instance absent/invalide, skip, résultat manquant/échoué, timeout, TRX incohérent
ou cleanup incomplet restent des échecs, même si l’exit code est nul.

Cleanup: zéro base/rôle/session possédés résiduels avant destruction, puis
suppression du conteneur exact, secrets/certificats et processus possédés;
préserver les ressources préexistantes. Conserver les preuves rouges expurgées,
sans retry jusqu’au vert ni réutilisation d’un ancien TRX.

Preuve locale P04 acquise, non rejouée par ce guide: **23/23 Passed, cinq TRX**,
Windows .NET contre PostgreSQL Linux, TLS VerifyFull et cleanup vérifiés.
P03 reste **32 × 24 = 768/768 HTTP 200**, 256 par route, sans réduction de
charge ni allongement des deadlines.

**P03-F1 OPEN / UNKNOWN**: **749 HTTP 200 / 19 HTTP 503 historiques**, cause
non démontrée, risque accepté avec réserve; réexamen obligatoire **2026-11-16**,
sans prolongation tacite. Une campagne verte ne prouve aucune correction causale.
Conserver les [observations et preuves historiques](tests/readiness-concurrency-regression.md).

## 30.3 Documentation

Les contrôles peuvent inclure :

```text
markdownlint

validation des liens

validation des ADR

validation des manifests

validation des exemples YAML
```

---

## 30.4 Conteneurs

Aucun Docker Compose client ou déploiement production n'est livré dans le
périmètre public courant. Ne pas inventer un fichier Compose ou une commande
pour satisfaire un exemple documentaire.

Une future modification de conteneur explicitement allouée doit valider sa
configuration réelle et ses builds/tests runtime, avec les invariants de la
section 25 et l'autorisation humaine requise. Cela n'autorise aucun déploiement.

---

## 30.5 Commandes réellement exécutées

Le compte rendu doit distinguer :

```text
commande exécutée et réussie

commande exécutée et échouée

commande non exécutée avec raison
```

---

# 31. Couverture

La couverture de code est un indicateur, pas une preuve suffisante.

Une contribution doit surtout couvrir :

- les branches critiques ;
- les erreurs ;
- les invariants ;
- les régressions ;
- les permissions ;
- les frontières de sécurité.

Un pourcentage élevé ne compense pas l’absence de test pertinent.

---

# 32. CI

## 32.1 Checks attendus

Le [workflow Public CI](.github/workflows/ci.yml) exécute le parcours complet sur
chaque PR et push main: Windows/Linux offline, PostgreSQL REQUIRED sous Linux,
puis **Require complete public proof**. Le [contrat CI](scripts/public-ci/README.md)
fait référence pour checkout, permissions minimales, actions SHA, scratch/cache
isolés et provenance. Aucun accès, secret, package ou source propriétaire requis.

| Preuve | Exigence |
|---|---|
| Windows | 11 suites, 2 726 Passed, 11 TRX |
| Linux | Même inventaire, 2 726 Passed, 11 TRX |
| PostgreSQL | 20 Facts Infrastructure + 3 Hosts, 5 shards, 5 TRX |
| P03 | 32 × 24 = 768/768 HTTP 200, 256 par route, zéro HTTP 503 |
| Ensemble | SourceLink VERIFIED, 27 TRX, checksums, cleanup et agrégation conformes |

**Preuve publique acquise:** [run main 43228653055](https://github.com/fi8Git/logicgate-sitefactory/actions/runs/43228653055),
tentative 1, SHA exécuté `e001093a1cfce5c5d1716b182fc84a785f5696f8`: les quatre
jobs sont en succès, agrégation SF-CI-PASSED. Cette preuve n’est pas une nouvelle
exécution locale. Windows .NET contre PostgreSQL Linux ne prouve pas .NET Linux
distant. Aucun badge, run ou SHA ne doit être inventé.

L’agrégation exige même SHA exécuté/runId, tentative entière positive au plus
égale à la tentative courante et tous les inventaires/TRX exacts. Aucun rapport
rouge, étranger, futur, incomplet ou dupliqué ne vaut succès; un résultat partiel
reste partiel. Diagnostic expurgé avant cleanup, sans empêcher celui-ci.

Selon la tâche, les contrôles applicables comprennent restore verrouillé, format,
build, tests, analyzers, secret/dependency scan, SAST, documentation, déterminisme,
PostgreSQL, Docker, SBOM, container scan et OpenAPI diff. Ce catalogue ne prétend
pas que chaque outil ou workflow futur est déjà implémenté.

## 32.2 Checks requis

Une pull request ne doit pas être fusionnée tant qu’un check obligatoire échoue.

---

## 32.3 Rerun

Ne pas relancer répétitivement une CI instable jusqu’à obtenir un résultat vert.

Identifier la cause :

- flakiness ;
- dépendance externe ;
- race ;
- timeout ;
- erreur réelle.

---

## 32.4 Échec antérieur

Lorsqu’un échec existait avant la branche :

- le reproduire sur `main` lorsque possible ;
- le documenter ;
- ne pas masquer un nouvel échec ;
- créer une tâche distincte si nécessaire.

---

# 33. Pull requests

## 33.1 Titre

Le titre suit la convention de commit.

Exemple :

```text
feat(cli): add client configuration validation
```

---

## 33.2 Taille

Une pull request doit rester suffisamment petite pour être relue avec précision.

Une PR très volumineuse doit être découpée lorsque les étapes peuvent rester cohérentes indépendamment.

---

## 33.3 Description obligatoire

La description doit indiquer :

```text
contexte

réalisé

hors portée

ADR applicables

tests

risques

migration éventuelle

impact de compatibilité

captures ou exemples lorsque utiles
```

---

## 33.4 Template recommandé

```markdown
## Contexte

Référence : SF-TASK-XXX

## Réalisé

- ...

## Hors portée

- ...

## Architecture

ADR applicables :

- ADR-XXXX

Dérogations :

- aucune

## Données et migrations

- Migration : oui/non
- Données personnelles ajoutées : oui/non
- Compatibilité rollback : ...

## Sécurité

- Permissions affectées : ...
- Secrets affectés : ...
- Intégrations affectées : ...

## Tests

- `commande` — réussite
- `commande` — non exécutée : raison

## Risques et limites

- ...

## Checklist

- [ ] Diff relu
- [ ] Tests ajoutés
- [ ] Documentation mise à jour
- [ ] Aucun secret
- [ ] Quality gates verts
- [ ] Auto-revue effectuée
```

---

# 34. Pull request documentaire

Une PR uniquement documentaire peut ne pas exécuter tous les tests applicatifs.

Elle doit néanmoins vérifier :

- Markdown ;
- liens ;
- index ;
- cohérence des noms ;
- ADR ;
- exemples ;
- absence de secrets.

---

# 35. Pull request de migration

Une PR contenant une migration doit ajouter une section :

```markdown
## Migration PostgreSQL

### Objets affectés

- ...

### Compatibilité

- Release précédente : compatible/incompatible
- Maintenance requise : oui/non

### Risques

- Locks :
- Table rewrite :
- Transformation de données :

### Retour

- Rollback applicatif :
- Migration corrective :
- Restore requis :

### Tests

- ...
```

---

# 36. Pull request Infrastructure

Une PR modifiant :

- Docker ;
- Caddy ;
- workflows ;
- registry ;
- secrets ;
- déploiement ;

doit préciser :

- environnement concerné ;
- privileges ;
- ports ;
- volumes ;
- réseau ;
- images ;
- rollback ;
- tests de sécurité.

---

# 37. Auto-revue de l’auteur

Avant de demander une revue, vérifier :

## 37.1 Fonctionnel

- critères d’acceptation couverts ;
- erreurs prévues ;
- cas négatifs ;
- régressions raisonnables.

## 37.2 Architecture

- bonne couche ;
- ADR respectés ;
- pas de duplication d’infrastructure ;
- contrats cohérents.

## 37.3 Sécurité

- aucun secret ;
- aucune donnée client ;
- permissions ;
- logs ;
- entrées non fiables ;
- dépendances.

## 37.4 Qualité

- code lisible ;
- méthodes bornées ;
- tests ;
- warnings ;
- documentation.

## 37.5 Périmètre

- aucun fichier inattendu ;
- aucun refactoring non lié ;
- aucun artefact local ;
- aucun changement de production.

---

# 38. Revue de code

Le template recommandé est :

```text
docs/06-codex/30-codex-review-template.md
```

---

## 38.1 Ordre de priorité

Une revue doit rechercher en priorité :

1. perte ou corruption de données ;
2. vulnérabilité ;
3. violation d’ADR ;
4. erreur fonctionnelle ;
5. problème de concurrence ;
6. problème d’exploitation ;
7. test manquant ;
8. maintenabilité ;
9. amélioration optionnelle.

---

## 38.2 Niveaux de findings

### Bloquant

Empêche la fusion.

Exemples :

- vulnérabilité exploitable ;
- perte de données ;
- build ou test obligatoire en échec ;
- violation d’un invariant non dérogeable ;
- migration dangereuse non planifiée.

### Majeur

Doit être corrigé avant fusion sauf décision humaine explicite.

Exemples :

- erreur fonctionnelle importante ;
- permission manquante ;
- test critique absent ;
- contrat incompatible non documenté.

### Mineur

Correction souhaitée dans la PR lorsqu’elle reste locale.

Exemples :

- diagnostic insuffisant ;
- cas limite ;
- maintenabilité localisée.

### Optionnel

Amélioration non nécessaire à l’acceptation.

---

## 38.3 Format d’un finding

Un finding doit indiquer :

```text
niveau

fichier et ligne ou symbole

scénario

impact

raison

correction attendue
```

---

## 38.4 Finding démontrable

Le reviewer doit expliquer le chemin d’exécution ou la règle violée.

Il ne doit pas produire un finding uniquement sur une préférence personnelle.

---

# 39. Réponse aux findings

Pour chaque finding, l’auteur indique :

```text
corrigé

non corrigé avec justification

hors périmètre avec tâche créée

désaccord avec démonstration
```

Les corrections doivent rester ciblées.

Le template recommandé est :

```text
docs/06-codex/31-codex-correction-template.md
```

---

# 40. Critères de fusion

Une pull request peut être fusionnée lorsque :

```text
le périmètre est respecté

les conversations bloquantes sont résolues

les ADR sont respectés

les checks obligatoires sont verts

les tests pertinents existent

la documentation est à jour

aucun secret n’est présent

les migrations sont documentées

les risques sont déclarés

l’auto-revue est terminée
```

---

## 40.1 Fusion interdite

Ne pas fusionner lorsque :

- un test obligatoire échoue ;
- une migration n’est pas comprise ;
- un secret a été exposé ;
- une décision humaine manque ;
- une incompatibilité est inconnue ;
- une dérogation n’est pas formalisée ;
- la production a été modifiée manuellement pour faire fonctionner la branche.

---

# 41. Après fusion

Après fusion :

1. vérifier les checks de `main` ;
2. supprimer la branche ;
3. mettre à jour la tâche ;
4. noter les suites nécessaires ;
5. ne pas déployer automatiquement sauf workflow explicitement autorisé ;
6. surveiller les éventuelles régressions de CI.

---

# 42. Changelog

Les changements visibles doivent mettre à jour :

```text
CHANGELOG.md
```

selon la politique de versionnement.

Catégories recommandées :

```text
Added

Changed

Deprecated

Removed

Fixed

Security
```

---

## 42.1 Pas de changelog requis

Une entrée n’est généralement pas nécessaire pour :

- correction typographique ;
- refactoring interne sans impact ;
- amélioration de test ;
- maintenance invisible.

---

## 42.2 Changement notable

Une entrée est attendue pour :

- nouvelle commande ;
- nouveau module ;
- nouveau contrat ;
- changement de génération ;
- changement de sécurité ;
- migration ;
- comportement d’upgrade ;
- changement de déploiement.

---

# 43. Versionnement et releases

## 43.1 Versioning

SiteFactory suit le versionnement défini dans la documentation de release.

La baseline utilise une approche compatible avec Semantic Versioning.
Aucune release officielle ni publication NuGet n'est acquise ; les capacités
développées restent sous Unreleased. La publication exige une décision distincte,
pas une simple contribution ou un build réussi.

---

## 43.2 Release

Une release doit notamment posséder :

- version ;
- changelog ;
- commit ;
- tests ;
- artefacts ;
- SBOM ;
- scans ;
- digests ;
- provenance ;
- signatures lorsque prévues.

---

## 43.3 Tags

Exemple de convention cible, pas un tag déjà créé :

```text
v0.1.0
```

Les tags de release ne doivent pas être déplacés.

---

## 43.4 Publication

Une release stable ne doit pas être publiée depuis un poste local sans workflow approuvé.

---

# 44. Hotfix

## 44.1 Définition

Un hotfix corrige un problème urgent affectant une release déployée.

---

## 44.2 Processus

```text
identifier la release affectée

créer une tâche

créer une branche courte

implémenter le correctif minimal

ajouter un test de régression

exécuter les quality gates

ouvrir une pull request

publier une version patch

réintégrer le correctif dans main
```

---

## 44.3 Pas de patch manuel

Ne pas :

- modifier une DLL dans un conteneur ;
- éditer un fichier de production non versionné ;
- exécuter un SQL improvisé ;
- déplacer un tag.

---

# 45. Production

Les contributions et pull requests ne donnent pas l’autorisation d’exécuter une action de production.

Les opérations réelles nécessitent :

- plan ;
- permissions ;
- confirmation ;
- runbook ;
- audit ;
- décision humaine.

---

## 45.1 Actions interdites dans une contribution ordinaire

```text
déployer

migrer

restaurer

purger

rotater

publier

arrêter un site

modifier un DNS

envoyer un email client
```

---

# 46. Signalement de vulnérabilité

Une vulnérabilité potentielle ne doit pas être publiée dans une issue publique contenant :

- un secret ;
- une méthode d’exploitation détaillée ;
- une donnée client ;
- une cible réelle.

Utiliser le canal privé défini par LogicGate.

---

## 46.1 Traitement

Le signalement doit inclure :

- composant ;
- scénario ;
- impact ;
- préconditions ;
- reproduction sûre ;
- correction potentielle ;
- urgence.

---

## 46.2 Secret exposé

En cas de secret réel découvert :

1. ne pas le recopier ;
2. interrompre la diffusion ;
3. avertir le responsable ;
4. rotater le secret ;
5. nettoyer l’historique si nécessaire ;
6. vérifier les accès ;
7. documenter l’incident.

Supprimer seulement le fichier courant est insuffisant.

---

# 47. Données client et confidentialité

Il est interdit d’ajouter au repository générique :

- export client ;
- lead réel ;
- email réel non public ;
- backup ;
- média privé ;
- fichier contractuel ;
- credential ;
- configuration de production.

Les fixtures doivent utiliser des organisations et personnes fictives.

---

# 48. Contribution par agent IA

Un agent doit suivre :

```text
AGENTS.md
```

et les documents :

```text
docs/06-codex/
```

---

## 48.1 Responsabilité humaine

Le contributeur humain reste responsable :

- du périmètre ;
- de la revue ;
- des décisions ;
- de la fusion ;
- de la production.

---

## 48.2 Transparence

La pull request peut indiquer qu’un agent a contribué, notamment lorsque :

- une grande partie du code a été générée ;
- une revue automatisée a été utilisée ;
- des fichiers ont été régénérés.

---

## 48.3 Vérification

Le code généré par un agent doit être relu et testé comme tout autre code.

---

# 49. Definition of Done

Une tâche est terminée lorsque :

```text
l’objectif est atteint

le périmètre est respecté

le code compile

les tests applicables passent

les cas négatifs importants sont couverts

les ADR sont respectés

aucun secret n’est présent

la sécurité n’est pas affaiblie

les erreurs sont structurées

les logs sont sûrs

la documentation est mise à jour

le diff est relu

les limites sont déclarées

la pull request est fusionnable
```

---

<a id="50-repère-historique-du-bootstrap"></a>

# 50. Découpage des contributions

Suivre les [changements courts](#53-changements-courts) et garder
[main intégrable](#52-main-toujours-intégrable). Chaque étape doit être revue,
testée et utilisable indépendamment, sans rouvrir des lots déjà validés.

# 51. Checklist rapide du contributeur

Avant de pousser :

```text
[ ] J’ai lu AGENTS.md

[ ] J’ai identifié les ADR applicables

[ ] Ma branche traite un objectif principal

[ ] Mon diff ne contient aucun fichier local

[ ] Aucun secret ou donnée client n’est présent

[ ] Les tests pertinents existent

[ ] Le build passe

[ ] La documentation est à jour

[ ] Les erreurs et logs sont sûrs

[ ] Aucun effet de production n’a été exécuté
```

Avant de fusionner :

```text
[ ] Les checks obligatoires sont verts

[ ] Les findings bloquants sont corrigés

[ ] La migration éventuelle est documentée

[ ] La compatibilité est connue

[ ] L’auto-revue est terminée

[ ] Le changelog est mis à jour si nécessaire

[ ] Le titre de PR convient au squash commit
```

---

<a id="52-historique-de-ce-document"></a>

# 52. Suivi documentaire

L’historique des modifications est conservé dans Git. Les évolutions notables
suivent la [politique de changelog](#42-changelog), sans transformer une ancienne
preuve en état courant ni réécrire une décision architecturale acceptée.

# 53. Synthèse

Une contribution suit le [workflow de développement](#12-workflow-de-développement)
et les [critères de fusion](#40-critères-de-fusion): objectif borné, branche
courte, PR relue, ADR respectés, tests positifs/négatifs et checks verts,
fusion explicite et aucune opération de production implicite. Les changements
doivent préserver génération cohérente, customizations et compréhension des
contrats, sans secret implicite.
