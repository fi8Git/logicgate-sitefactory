# AGENTS.md — LogicGate SiteFactory

## 1. Objet

Ce fichier définit les règles de travail applicables aux agents de développement intervenant sur LogicGate SiteFactory.

Il s’adresse notamment à :

- Codex ;
- tout autre agent de développement assisté par IA ;
- tout outil automatisé modifiant le repository ;
- tout développeur utilisant un agent pour implémenter, revoir ou corriger du code.

Les agents doivent considérer ce document comme une instruction opérationnelle obligatoire.

SiteFactory est un moteur open source modulaire .NET 10, distribué sous Apache-2.0 pour son code original, avec les attributions de LICENSE et NOTICE. Le Core, le ModuleSdk, le module Documentation et les bibliothèques runtime/PostgreSQL génériques se construisent et se testent sans dépendance propriétaire obligatoire. Les consommateurs intègrent leurs propres modules par les contrats publics du ModuleSdk. Aucun package NuGet ni aucune release officielle ne sont publiés ; aucune autorisation de publication ne découle de ce document.

L’objectif d’un agent n’est pas uniquement de produire du code compilable.

Il doit produire un changement :

```text
borné

conforme aux décisions architecturales

testé

maintenable

documenté

sans affaiblissement de sécurité

sans effet de production implicite
```

---

## 2. Portée

Ce fichier s’applique à l’ensemble du repository.

```text
/
├── src/
├── tests/
├── templates/
├── modules/
├── samples/
├── deploy/
├── scripts/
├── docs/
└── fichiers racine
```

Un fichier `AGENTS.md` placé dans un sous-répertoire peut ajouter des règles plus spécifiques pour ce périmètre.

Un fichier enfant ne doit pas affaiblir les invariants non dérogeables du présent document sans :

- décision humaine explicite ;
- mise à jour documentaire ;
- ADR accepté lorsque nécessaire.

---

## 3. Langage normatif

Les termes suivants sont normatifs :

```text
DOIT
→ exigence obligatoire

NE DOIT PAS
→ interdiction

DEVRAIT
→ règle recommandée, sauf justification explicite

PEUT
→ possibilité facultative
```

En cas de doute, l’agent doit privilégier :

```text
le changement le plus petit

le comportement le plus sûr

la solution la plus explicite

l’absence d’effet irréversible
```

---

## 4. Contexte public et contributions

Le périmètre public implémenté comprend :

- Domain, Application et Infrastructure génériques ;
- ModuleSdk, catalogue et contributions de génération explicites ;
- CLI publique validate/plan/generate, composée avec DocumentationModule ;
- scanner Git local read-only, modèle documentaire et rendu de site statique ;
- Runtime.Contracts et Runtime purs, adaptateurs Hosting/ASP.NET Core ;
- Infrastructure PostgreSQL et Migrator explicite ;
- tests publics, guards architecturaux et harnais PostgreSQL REQUIRED.

La configuration structurelle V1 reste `client.sitefactory.yml`.
Les sorties de génération disponibles incluent `sitefactory.lock.json`,
`generation-report.json` et `.sitefactory/generation-state.json`.
Ne pas présenter les autres manifests/plans cibles comme déjà produits.

La CLI publique génère le template documentaire embarqué ; l'import complet
d'un repository documentaire est accessible par composition C#, pas par une
commande CLI inventée. Les bibliothèques runtime/PostgreSQL ne constituent pas
un générateur d'application Business dans la distribution publique.

Les modules consommateurs s'intègrent par les contrats publics du ModuleSdk.
Leurs dépendances vont vers ces contrats, jamais l'inverse : le Core reste
indépendant des implémentations métier concrètes. Aucun ProjectReference entre
repositories n'est autorisé.

Toute contribution doit posséder un périmètre et une responsabilité explicites.
Aucun accès à un repository privé ne doit être nécessaire pour construire ou
tester le public. Ne pas copier de code, fixture ou binaire privé dans le public,
ni modifier une classification, allowlist, SHA approuvé ou licence sans décision
humaine spécifique.

[ADR-0034](docs/adr/0034-apache-licence-choice-for-publish-site-factory.md) ne
relicencie automatiquement ni les modules consommateurs, ni les sorties générées,
ni les contenus tiers. Leurs droits/notices doivent être conservés.

---

## 5. État actuel et limites des preuves

La solution publique réelle est `SiteFactory.slnx` : 12 projets de production,
11 projets de tests. SDK .NET 10 déclaré dans `global.json`.
Le code et les capacités sont décrits dans README et CHANGELOG ; les capacités
développées restent sous Unreleased, sans release ou package NuGet publié.

La génération et la régénération contrôlée sont implémentées. Elles restent
déterministes, préservent les customizations et refusent les états/stratégies
inconnus sans overwrite implicite. Atomicité par fichier seulement, pas
transaction filesystem globale.

Le harnais public `tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1` possède
deux inventaires PostgreSQL fermés : 20 Facts Infrastructure et 3 Facts Hosts,
cinq shards séquentiels. Ses preuves locales acquises ne doivent pas être
présentées comme rejouées pendant une tâche documentaire.

La CI publique Windows/Linux est opérationnelle et validée à distance dans
fi8Git/logicgate-sitefactory, avec SourceLink VERIFIED. Dernier run `main`
validé : [43009023135](https://github.com/fi8Git/logicgate-sitefactory/actions/runs/43009023135),
SHA `4be284f4fdfdf621e7bc850833e02c4820b062a9` : 2 726 Passed par plateforme,
23 Facts PostgreSQL sur cinq shards, P03 768/768, 27 TRX, cleanup et agrégation
conformes. Ne pas confondre preuve locale Windows .NET vers PostgreSQL Linux
et validation multiplateforme distante.

**P03-F1 reste OPEN / UNKNOWN** : 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve. Réexamen obligatoire
le **2026-11-16**, sans prolongation tacite ni correction causale prétendue
à partir de campagnes vertes. Voir les
[observations et preuves historiques](tests/readiness-concurrency-regression.md).

Les politiques ci-dessous restent obligatoires même lorsqu'elles concernent
des fonctions futures : elles n'annoncent pas leur implémentation. En particulier,
aucun déploiement client complet, provisioning/exploitation PostgreSQL production,
backup/restore, job métier ou publication ne doit être inféré des exemples.

L’agent doit inspecter le repository réel avant d’utiliser un nom de solution,
de projet ou de fichier. Il ne doit pas inventer un fichier absent uniquement
parce que la documentation décrit son existence future.

---

## 6. Sources de vérité

En cas d’ambiguïté, appliquer l’ordre suivant :

1. décision humaine explicite dans la tâche courante ;
2. ADR accepté applicable ;
3. présent `AGENTS.md` ;
4. documents de vision et d’architecture ;
5. spécification de la tâche ;
6. documentation du module ;
7. tests existants ;
8. implémentation actuelle ;
9. comportement historique non documenté.

Une implémentation existante ne remplace pas silencieusement une décision acceptée.

Lorsqu’un conflit est détecté, l’agent doit :

```text
arrêter la partie concernée

décrire le conflit

citer les documents contradictoires

proposer l’option la plus sûre

demander une décision humaine si nécessaire
```

---

## 7. Documents obligatoires

Avant une tâche structurante, l’agent doit identifier et lire les documents applicables.

### 7.1 Documentation générale

```text
README.md

CONTRIBUTING.md

CHANGELOG.md
```

### 7.2 Documentation architecturale

```text
docs/02-architecture/
```

### 7.3 Génération

```text
docs/03-generation/
```

### 7.4 Qualité et sécurité

```text
docs/04-quality-security/
```

### 7.5 Opérations

```text
docs/05-operations/
```

### 7.6 Règles Codex

```text
docs/06-codex/28-codex-operating-guide.md

docs/06-codex/29-codex-task-specification-template.md

docs/06-codex/30-codex-review-template.md

docs/06-codex/31-codex-correction-template.md
```

### 7.7 Décisions

```text
docs/adr/README.md

docs/adr/
```

L’agent ne doit pas lire systématiquement les 32 ADR pour une modification locale.

Il doit identifier ceux qui s’appliquent réellement à la tâche.

---

## 8. ADR fondamentaux par domaine

### 8.1 Génération et configuration

```text
ADR-0001
ADR-0002
ADR-0005
ADR-0007
ADR-0009
ADR-0014
ADR-0015
```

### 8.2 PostgreSQL et données

```text
ADR-0003
ADR-0004
ADR-0016
ADR-0023
ADR-0027
ADR-0028
```

### 8.3 Sécurité

```text
ADR-0008
ADR-0012
ADR-0018
ADR-0019
ADR-0020
ADR-0021
ADR-0022
ADR-0025
ADR-0026
```

### 8.4 API, jobs et runtime

```text
ADR-0017
ADR-0028
ADR-0029
ADR-0030
ADR-0031
```

### 8.5 Production

```text
ADR-0011
ADR-0014
ADR-0024
ADR-0026
ADR-0027
ADR-0031
ADR-0032
```

---

## 9. Rôle de l’agent

L’agent est un exécutant technique guidé par :

- une tâche ;
- les documents ;
- les ADR ;
- les tests ;
- les décisions humaines.

Il peut :

- analyser le repository ;
- proposer un plan ;
- implémenter une tâche bornée ;
- ajouter ou corriger des tests ;
- mettre à jour une documentation ;
- effectuer une revue ;
- signaler un risque ;
- proposer un ADR lorsqu’une nouvelle décision est nécessaire.

Il ne doit pas devenir le décideur implicite du produit ou de l’architecture.

---

## 10. Actions interdites à un agent

Un agent ne doit jamais, sans demande et confirmation humaines explicites :

```text
déployer en production

appliquer une migration de production

restaurer une base réelle

supprimer des données réelles

rotater un secret réel

créer un compte administrateur réel

modifier un rôle PostgreSQL de production

activer un mode maintenance réel

publier une image de production

signer une release réelle

créer ou pousser un repository distant

envoyer un email réel

appeler un fournisseur réel

modifier des DNS

modifier un VPS réel

forcer un push

réécrire l’historique Git partagé
```

Une tâche de développement ou de documentation ne constitue jamais une autorisation implicite de production.

---

## 11. Invariants non dérogeables

L’agent ne doit jamais introduire :

```text
une migration automatique au démarrage

un secret dans Git

un secret dans une image

un runtime PostgreSQL superuser

un runtime propriétaire des objets PostgreSQL

un conteneur applicatif root

un conteneur privileged

un montage du Docker socket

une validation TLS désactivée

une URL utilisateur appelée par le serveur

une mutation HTTP par GET

un audit sensible modifiable par le runtime

un restore rouvert avant réconciliation

un déploiement avec latest

un rollback vers un schéma incompatible

un appel externe dans une transaction PostgreSQL

un cache comme source de vérité
```

Lorsqu’une tâche semble exiger l’un de ces comportements, l’agent doit s’arrêter et demander une décision humaine.

---

# 12. Workflow obligatoire

## 12.1 Étape 1 — Comprendre la tâche

Avant toute modification, l’agent doit identifier :

```text
objectif

périmètre

fichiers concernés

documents applicables

ADR applicables

critères d’acceptation

tests attendus

actions explicitement hors portée
```

L’agent ne doit pas élargir silencieusement la tâche.

---

## 12.2 Étape 2 — Inspecter le repository

L’agent doit rechercher :

- les projets réels ;
- la solution réelle ;
- les conventions existantes ;
- les types similaires ;
- les tests existants ;
- les fichiers `AGENTS.md` plus proches ;
- les dépendances déjà disponibles ;
- les patterns déjà utilisés.

Il ne doit pas créer une deuxième infrastructure équivalente uniquement parce qu’il n’a pas recherché l’existante.

---

## 12.3 Étape 3 — Produire un plan

Pour une tâche non triviale, le plan doit indiquer :

```text
fichiers à créer

fichiers à modifier

contrats concernés

migrations éventuelles

tests

documentation

risques

éléments volontairement hors portée
```

Le plan doit rester proportionné à la tâche.

---

## 12.4 Étape 4 — Implémenter le plus petit changement cohérent

L’agent doit :

- respecter les couches existantes ;
- limiter les modifications ;
- éviter les refactorings sans lien ;
- conserver la compatibilité lorsque requise ;
- écrire du code lisible ;
- ajouter les validations ;
- ajouter les tests.

---

## 12.5 Étape 5 — Vérifier

L’agent doit exécuter les contrôles applicables :

```text
format

restore

build

tests ciblés

tests globaux lorsque raisonnable

analyse statique

validation documentaire

validation des fichiers générés
```

Il ne doit pas annoncer un contrôle comme réussi s’il ne l’a pas exécuté.

---

## 12.6 Étape 6 — Auto-revue

Avant de terminer, vérifier :

- conformité avec les ADR ;
- sécurité ;
- régression ;
- tests négatifs ;
- erreurs et logs ;
- compatibilité ;
- fichiers oubliés ;
- documentation ;
- absence de secret ;
- diff hors périmètre.

---

## 12.7 Étape 7 — Rendre compte

Le compte rendu doit distinguer :

```text
ce qui a été fait

ce qui a été testé

ce qui n’a pas été testé

les risques restants

les décisions humaines encore nécessaires
```

---

# 13. Taille des tâches

Les tâches doivent être suffisamment petites pour être :

- comprises ;
- implémentées ;
- testées ;
- revues ;
- corrigées ;

dans un même cycle.

L’agent doit refuser de traiter comme une seule tâche une demande équivalant à :

```text
implémenter toute la plateforme

implémenter tous les modules

implémenter les 500 tâches

créer toute la production
```

Il doit proposer une vertical slice bornée.

---

## 13.1 Vertical slice initiale

La première vertical slice recommandée est :

```text
solution .NET

CLI minimale

lecture YAML

validation de configuration

plan de génération

génération d’un projet minimal

lock

rapport

build du projet généré

tests du projet généré
```

---

# 14. Architecture des couches

La solution publique existe ; les responsabilités et frontières suivantes doivent être respectées.

## 14.1 Domain

Domain contient :

- concepts métier ;
- value objects ;
- invariants ;
- événements métier ;
- contrats sans dépendance Infrastructure.

Domain ne dépend pas :

- d’EF Core ;
- d’ASP.NET Core ;
- de `HttpClient` ;
- du filesystem ;
- de Docker ;
- d’un fournisseur externe ;
- du format YAML.

---

## 14.2 Application

Application contient :

- commandes ;
- queries ;
- handlers ;
- interfaces de ports ;
- autorisation métier ;
- validation fonctionnelle ;
- transactions déclarées ;
- résultats typés.

Application ne doit pas dépendre directement :

- d’un provider externe concret ;
- de PostgreSQL spécifique lorsque l’abstraction suffit ;
- de détails de sérialisation HTTP ;
- de Docker.

---

## 14.3 Infrastructure

Infrastructure contient :

- EF Core ;
- Npgsql ;
- filesystem ;
- providers ;
- SMTP ;
- HTTP clients ;
- cache concret ;
- secrets ;
- jobs ;
- génération ;
- Git ;
- Docker-related tooling.

---

## 14.4 Presentation ou CLI

La couche d’entrée contient :

- binding ;
- parsing ;
- validation syntaxique ;
- mapping des résultats ;
- codes de sortie ;
- affichage.

Elle ne contient pas les règles métier principales.

---

## 14.5 Tests

Les tests doivent suivre la même séparation :

- Domain ;
- Application ;
- Infrastructure ;
- contrats ;
- génération ;
- intégration ;
- sécurité.

---

# 15. Dépendances entre couches

La direction attendue est conceptuellement :

```text
Presentation
→ Application
→ Domain

Infrastructure
→ Application
→ Domain
```

Domain ne doit dépendre d’aucune couche extérieure.

Application peut définir les interfaces qu’Infrastructure implémente.

Une référence circulaire entre projets est interdite.

---

# 16. Style de code .NET

L’agent doit respecter les conventions existantes du repository.

En l’absence de convention plus précise :

```text
nullable activé

warnings traités

async/await pour les opérations I/O

CancellationToken propagé

types explicites pour les contrats publics

immutabilité privilégiée

constructeurs courts

méthodes bornées

noms descriptifs

pas d’abréviation obscure
```

---

## 16.1 Nullabilité

Ne pas désactiver globalement la nullabilité pour résoudre des warnings.

Les valeurs nulles doivent être modélisées explicitement.

---

## 16.2 Exceptions

Les exceptions sont réservées aux erreurs exceptionnelles.

Les résultats métier attendus utilisent des types ou erreurs stables.

---

## 16.3 `CancellationToken`

Toute opération asynchrone longue ou I/O doit accepter et propager un `CancellationToken`.

Ne pas remplacer arbitrairement le token appelant par :

```csharp
CancellationToken.None
```

---

## 16.4 Temps

Utiliser :

```text
TimeProvider
```

ou l’abstraction temporelle retenue pour le code testable.

Les dates persistées sont en UTC.

---

## 16.5 Identifiants

Privilégier des types dédiés lorsque cela améliore la sécurité :

```text
ClientId

ModuleId

TemplateId

GenerationPlanId

OperationId
```

Éviter les chaînes interchangeables pour tous les identifiants.

---

# 17. Gestion des erreurs

Les erreurs doivent être :

```text
structurées

stables

bornées

sans secret

sans contenu personnel inutile
```

Les bibliothèques ne doivent pas simplement propager au client :

- stack traces ;
- SQL ;
- chemins locaux ;
- messages complets de provider ;
- chaînes de connexion.

Les APIs suivent l’ADR-0029 et utilisent Problem Details.

Les commandes CLI utilisent des codes de sortie stables.

---

# 18. Logs et observabilité

Les logs doivent être structurés.

Exemple :

```csharp
logger.LogInformation(
    "Generation completed for {ClientSlug} with {FileCount} files",
    clientSlug,
    fileCount);
```

Éviter :

```csharp
logger.LogInformation(
    $"Generation completed for {clientSlug}...");
```

lorsque la journalisation structurée est possible.

---

## 18.1 Données interdites dans les logs

Ne pas journaliser :

```text
password

token

cookie

secret

connection string

clé privée

contenu complet d’un formulaire

message complet d’un lead

payload complet d’un job

réponse complète d’un provider

fichier client
```

---

## 18.2 Cardinalité

Ne pas utiliser comme labels métriques :

- User ID ;
- Lead ID ;
- Trace ID ;
- Media ID ;
- email ;
- chemin libre ;
- URL complète.

---

# 19. Configuration

La configuration structurelle client appartient à :

```text
client.sitefactory.yml
```

Elle ne doit jamais contenir :

- mot de passe ;
- clé API ;
- chaîne de connexion ;
- secret TOTP ;
- credential SMTP ;
- token de registry ;
- private key.

Les propriétés doivent être :

- documentées ;
- validées ;
- versionnées ;
- bornées.

Une propriété inconnue doit être détectée dans les contrats stricts.

---

## 19.1 Valeurs par défaut

Une valeur par défaut doit être :

- sûre ;
- non secrète ;
- documentée ;
- indépendante de l’environnement.

Ne jamais inventer par défaut :

- un mot de passe ;
- un endpoint de production ;
- un email administrateur ;
- un client ;
- un environnement.

---

# 20. Secrets

Les secrets sont référencés par :

```text
Secret ID
```

Ils sont résolus au runtime.

L’agent ne doit pas :

- ajouter de secret réel ;
- écrire un exemple qui ressemble à un secret réel ;
- copier un `.env` réel ;
- passer un secret par argument CLI ;
- ajouter un secret dans un Docker `ARG` ;
- ajouter un secret à un rapport.

Les exemples utilisent des placeholders explicites :

```text
<POSTGRES_WEB_PASSWORD>

<SMTP_PASSWORD>

<REGISTRY_TOKEN>
```

---

# 21. Génération déterministe

La génération doit produire un résultat stable à partir des mêmes entrées structurelles.

L’agent doit contrôler :

- ordre des fichiers ;
- ordre des collections ;
- encodage ;
- fins de ligne ;
- dates ;
- identifiants ;
- tri des propriétés ;
- chemins ;
- culture.

---

## 21.1 Données non déterministes

Une date de génération ou un Generation ID peut apparaître dans un rapport opérationnel.

Elle ne doit pas modifier inutilement tous les fichiers générés.

---

## 21.2 Tests

Un test déterministe doit pouvoir :

1. générer un projet ;
2. générer de nouveau avec les mêmes entrées ;
3. comparer les sorties pertinentes ;
4. constater l’absence de dérive non justifiée.

---

# 22. Stratégies de fichiers générés

Chaque fichier ou zone de fichiers doit déclarer l'une des cinq stratégies canoniques définies par [ADR-0005](docs/adr/0005-file-generation-strategies.md) :

```text
generated

create-once

merge

custom

protected
```

Les anciens noms `owned`, `preserve`, `extension-point` et `manual` ne sont pas des alias de stratégie. L'action de plan `preserve` reste distincte de la stratégie de propriété.

---

## 22.1 `generated`

Le fichier est produit par SiteFactory ; sa correction appartient à la source de génération.

Son remplacement exige la preuve qu'il correspond encore au dernier contenu généré. Une modification locale ne doit jamais être écrasée silencieusement.

---

## 22.2 `create-once`

Le fichier est créé uniquement lorsqu'il n'existe pas, puis appartient au client.

Un fichier existant est préservé, sans remplacement ni suppression automatique.

---

## 22.3 `merge`

La fusion doit être structurée, déterministe, versionnée, spécifique au format et testée.

Une fusion par concaténation de texte fragile est interdite pour les formats structurés importants.

---

## 22.4 `custom`

Le fichier ou répertoire appartient exclusivement aux customizations client.

SiteFactory ne doit pas le créer, modifier ou supprimer automatiquement.

---

## 22.5 `protected`

Toute écriture automatique est interdite dans le pipeline normal de génération ou d'upgrade, y compris la création, la modification, le renommage, la suppression et la fusion.

Une option générale `--force` ne contourne jamais cette protection.

---

# 23. Plans avant application

Les opérations pouvant modifier un projet existant doivent préférer :

```text
plan

puis apply
```

Le plan doit préciser :

- entrées ;
- fichiers ;
- créations ;
- modifications ;
- suppressions ;
- conflits ;
- risques ;
- compatibilité ;
- hash.

L’agent ne doit pas implémenter un `apply` qui recalcule silencieusement un plan différent.

---

# 24. `sitefactory.lock.json`

Le lock doit représenter la composition résolue :

- générateur ;
- plateforme ;
- template ;
- modules ;
- packages ;
- schémas ;
- contrats ;
- versions.

Il ne contient aucun secret.

Il doit être :

- déterministe ;
- versionné ;
- validé ;
- mis à jour explicitement.

---

# 25. `generation-report.json`

Le rapport doit distinguer :

```text
fichiers créés

fichiers modifiés

fichiers préservés

conflits

warnings

erreurs

versions

durée

résultat
```

Il ne doit pas inclure :

- contenu client complet ;
- secrets ;
- credentials ;
- données personnelles ;
- fichiers privés.

---

# 26. Modules

Chaque module doit définir au minimum :

```text
ModuleId

Version

Dependencies

PublicContracts

Configuration

Permissions

DatabaseSchemas

Migrations

DataCategories

RetentionPolicies

JobContracts

CacheRegions

IntegrationIds

DeploymentImpact

Tests
```

---

## 26.1 Dépendances

Les dépendances doivent être explicites.

Un module ne doit pas accéder directement aux types internes d’un autre module.

---

## 26.2 Contrats publics

Les contrats publics sont versionnés.

Une modification incompatible nécessite :

- une nouvelle version ;
- un plan de migration ;
- éventuellement un ADR.

---

## 26.3 Données

Un module qui persiste une donnée doit déclarer :

- propriétaire ;
- classification ;
- finalité ;
- rétention ;
- export ;
- suppression.

---

## 26.4 Composition statique explicite

Les modules compilés sont enregistrés explicitement dans le composition root,
selon [ADR-0037](docs/adr/0037-compose-modules-explicitly-without-dynamic-loading.md).
Réutiliser les contrats ModuleSdk/catalogue/contributions ; ne pas rendre le
Core dépendant d'une classe concrète Business.

Aucun scan d'assemblies, chargement dynamique de DLL, installation NuGet au
runtime ou téléchargement puis exécution implicite de code n'est autorisé.
Un module absent doit produire le diagnostic prévu, sans fallback caché vers
une offre Business. Un module installé est du code de confiance du host,
pas un plugin prétendument sandboxé.

---

# 27. Customizations client

Le code spécifique au client doit être isolé.

Il doit utiliser :

- interfaces publiques ;
- événements publics ;
- slots UI ;
- registries ;
- hooks documentés ;
- configuration prévue.

Il ne doit pas :

- modifier un package générique ;
- copier un module complet pour changer une ligne ;
- utiliser un type `internal` d’un autre module ;
- créer une table cachée sans descriptor ;
- appeler une URL externe non déclarée ;
- contourner les permissions.

---

# 28. Intelligence artificielle

L’IA est optionnelle.

L’agent ne doit pas introduire une dépendance runtime obligatoire à :

- Codex ;
- OpenAI ;
- un fournisseur LLM particulier.

Les fonctions AI doivent utiliser des abstractions provider-agnostic lorsque le produit le nécessite.

Une génération sans AI doit rester possible.

Aucun fallback cloud, appel fournisseur, clone distant ou comportement de
remplacement non prévu par les contrats ne doit être ajouté pour compenser
une configuration, source ou contribution absente/invalide. Une erreur doit
rester explicite ; toute intégration externe nécessite sa décision et ses
contrôles. Cette règle ne remplace pas le restore NuGet explicitement demandé.

Le résultat AI doit être considéré comme :

```text
une proposition

non une décision

non un artefact de confiance
```

---

# 29. PostgreSQL

PostgreSQL est la base principale.

Les tests dépendant de comportements PostgreSQL doivent utiliser une vraie instance PostgreSQL.

Une base en mémoire ne prouve pas :

- les contraintes ;
- les verrous ;
- les transactions ;
- les rôles ;
- les fonctions ;
- les migrations ;
- la concurrence.

---

## 29.1 Schémas

Les objets applicatifs doivent utiliser des schémas explicites.

Le schéma `public` ne doit pas devenir le répertoire par défaut de tous les objets métier.

---

## 29.2 Noms

Les noms doivent être :

- déterministes ;
- stables ;
- compatibles PostgreSQL ;
- explicitement limités.

---

# 30. Rôles PostgreSQL

Les responsabilités sont séparées :

```text
owner NOLOGIN

migrator

web

worker

backup

restore

read-only éventuel
```

L’agent ne doit pas :

- utiliser `postgres` dans l’application ;
- donner le rôle migrator au web ;
- rendre le web owner ;
- accorder `GRANT ALL` par commodité ;
- exposer le port 5432 publiquement.

---

# 31. Migrations

Les migrations sont :

```text
générées dans le repository

revues

planifiées

appliquées explicitement
```

Le web et le worker ne doivent jamais appeler au démarrage :

```csharp
Database.Migrate();
Database.EnsureCreated();
```

Une migration doit préciser :

- compatibilité ;
- impact ;
- transaction ;
- locks ;
- rollback ;
- tests.

---

# 32. Transactions

La frontière transactionnelle appartient au cas d’usage Application.

Une transaction ne doit pas traverser :

- une interaction utilisateur ;
- un appel HTTP externe ;
- un envoi SMTP ;
- un traitement AI ;
- une transformation longue ;
- un job complet.

Les effets externes utilisent :

```text
transaction métier

outbox

commit

worker

provider
```

---

## 32.1 Concurrence

Les agrégats éditables utilisent une Version explicite.

Un conflit ne doit jamais être résolu silencieusement par :

```text
last write wins
```

---

## 32.2 Contraintes

L’unicité et les invariants persistants doivent être protégés dans PostgreSQL lorsque possible.

Une simple vérification applicative préalable ne suffit pas face à la concurrence.

---

# 33. Jobs et outbox

Un job doit déclarer :

```text
JobType

ContractVersion

HandlerVersion

IdempotencyPolicy

RetryPolicy

Timeout

Interruptibility

DataCategories
```

Le worker ne doit pas revendiquer un job :

- inconnu ;
- d’une version incompatible ;
- avant la readiness ;
- pendant le drainage.

---

## 33.1 Retries

Les retries doivent être :

- bornés ;
- classifiés ;
- avec backoff ;
- avec jitter ;
- idempotents.

Un retry infini est interdit.

---

## 33.2 Payloads

Les payloads doivent contenir des références minimales.

Ils ne doivent pas contenir :

- secret ;
- contenu métier complet inutile ;
- URL de provider arbitraire ;
- fichier complet ;
- entité EF Core sérialisée.

---

# 34. HTTP et API

Les routes API utilisent :

```text
/api/v1
```

ou la convention définie par l’ADR-0029.

Les APIs doivent :

- utiliser des DTOs explicites ;
- retourner des statuts cohérents ;
- utiliser Problem Details ;
- exposer des Error Codes stables ;
- limiter les corps ;
- contrôler la concurrence ;
- utiliser l’idempotence lorsque nécessaire.

---

## 34.1 Méthodes

Une mutation ne doit jamais utiliser :

```text
GET

HEAD
```

---

## 34.2 Erreurs

Une API ne retourne pas :

```json
{
  "success": false,
  "message": "Erreur"
}
```

comme contrat générique improvisé.

---

## 34.3 Authentification API

Une route API non authentifiée retourne :

```text
401
```

et non une redirection HTML vers le login.

---

# 35. Identity et administration

Les règles comprennent :

- cookie sécurisé ;
- sessions serveur ;
- MFA ;
- step-up ;
- antiforgery ;
- révocation ;
- provisioning par invitation ;
- aucun compte par défaut.

L’agent ne doit pas créer :

- mot de passe initial dans `.env` ;
- admin automatique ;
- reset MFA automatique ;
- recovery par email seul ;
- cookie accessible en JavaScript.

---

# 36. Audit

Une action sensible doit être auditée selon l’ADR-0019.

Le runtime ordinaire ne doit pas pouvoir :

- modifier un événement ;
- supprimer un événement ;
- réécrire la chaîne ;
- modifier les epochs.

Une action sensible qui exige un audit utilise :

```text
même transaction métier
```

lorsque possible.

---

# 37. Privacy et cycle de vie

Une donnée persistante doit appartenir au Privacy Data Catalog.

Elle doit posséder :

```text
DataCategoryId

classification

finalité

owner

Retention Policy

Deletion Strategy

Export Strategy

Backup Treatment
```

L’agent ne doit pas considérer :

```text
archivé

IsDeleted = true

email remplacé
```

comme une suppression ou anonymisation automatiquement complète.

---

# 38. Médias et fichiers

Les médias utilisent :

- identifiants opaques ;
- noms physiques serveur ;
- metadata PostgreSQL ;
- stockage persistant ;
- validation ;
- états de lifecycle.

Les noms fournis par l’utilisateur ne doivent pas déterminer directement un chemin physique.

---

## 38.1 Uploads

Les uploads doivent être :

- bornés ;
- streamés ;
- validés ;
- stockés en staging ;
- promus après commit ;
- indépendants du nom original.

---

## 38.2 Fichiers temporaires

Les temporaires utilisent :

- tmpfs ;
- zone dédiée ;
- quotas ;
- cleanup ;
- aucune conservation implicite.

---

# 39. Cache

Le cache est facultatif et reconstruisible.

PostgreSQL reste la source de vérité.

Les caches doivent utiliser :

- clés versionnées ;
- client ;
- environnement ;
- TTL ;
- taille ;
- Version ou Epoch ;
- invalidation post-commit.

L’agent ne doit pas :

- mettre une entité EF Core suivie en cache ;
- cacher une permission critique longtemps ;
- mettre une réponse admin en cache public ;
- utiliser une PII dans une clé ;
- invalider avant le commit.

---

# 40. Intégrations externes

Toute intégration doit posséder :

```text
IntegrationId

OperationId

destination approuvée

Secret IDs

Data Categories

timeouts

retry policy

kill switch
```

L’agent ne doit pas :

```csharp
await httpClient.GetAsync(userProvidedUrl);
```

Il ne doit jamais désactiver la validation TLS.

Les redirects automatiques vers une origine inconnue sont interdits.

---

# 41. Conteneurs

Les images de production doivent être :

- multi-stage ;
- minimales ;
- non-root ;
- read-only ;
- sans SDK ;
- sans secret ;
- scannées ;
- signées ;
- déployées par digest.

Les services applicatifs utilisent :

```yaml
cap_drop:
  - ALL

security_opt:
  - no-new-privileges:true
```

L’agent ne doit pas ajouter :

```yaml
privileged: true

network_mode: host
```

ou un montage de :

```text
/var/run/docker.sock
```

---

# 42. Docker Compose

Le profil production doit notamment garantir :

- PostgreSQL sans port public ;
- Caddy seul exposé ;
- web et worker séparés ;
- volumes nommés ;
- secrets séparés ;
- images par digest ;
- health checks ;
- restart policies adaptées.

L’agent ne doit pas introduire :

```yaml
ports:
  - "5432:5432"
```

dans le profil production.

---

# 43. Déploiement

Le déploiement suit :

```text
plan

preflight

verrou

backup

maintenance

drainage

migration

activation

startup validation

smoke tests

stabilisation
```

L’agent peut implémenter les outils et scripts correspondants.

Il ne doit pas les exécuter sur un environnement réel sans autorisation humaine.

---

# 44. Git

L’agent doit conserver un historique compréhensible.

Il ne doit pas :

- forcer un push ;
- réécrire une branche partagée ;
- supprimer des tags ;
- créer un remote sans demande ;
- pousser automatiquement après génération.

---

## 44.1 Commits

Un commit doit rester :

- cohérent ;
- limité ;
- testable ;
- descriptif.

Éviter de mélanger :

```text
nouvelle fonctionnalité

refactoring massif

formatage global

mise à jour de dépendances

documentation sans rapport
```

dans le même commit.

---

## 44.2 Fichiers générés

Ne pas modifier manuellement un fichier `generated` si la correction appartient au générateur ou au template.

Corriger la source de génération, puis régénérer.

---

# 45. Dépendances

Avant d’ajouter une dépendance, vérifier :

- besoin ;
- alternative standard .NET ;
- licence ;
- maintenance ;
- sécurité ;
- taille ;
- transitives ;
- compatibilité ;
- supply chain.

L’agent ne doit pas ajouter une bibliothèque uniquement pour éviter quelques lignes simples et lisibles.

---

## 45.1 Versions

Les versions doivent être :

- explicites ;
- verrouillées ;
- compatibles avec la politique du repository.

Les versions flottantes sont interdites pour les releases.

---

## 45.2 Sources

Pour une décision technique ou de sécurité dépendant d’une bibliothèque, privilégier :

- documentation officielle ;
- repository officiel ;
- publication primaire.

---

# 46. Tests

Toute modification fonctionnelle doit posséder des tests adaptés.

---

## 46.1 Types de tests

Selon la tâche :

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

## 46.2 Tests positifs et négatifs

Ne pas tester uniquement le cas nominal.

Exemples :

```text
configuration valide
+ propriété inconnue

génération réussie
+ conflit de fichier

permission accordée
+ permission refusée

version courante
+ conflit de concurrence

provider disponible
+ timeout

archive sûre
+ path traversal
```

---

## 46.3 Régressions

Une correction de bug doit inclure un test qui échoue avant la correction lorsque cela est raisonnable.

---

## 46.4 Tests déterministes

Les tests ne doivent pas dépendre inutilement :

- de l’heure système ;
- du réseau ;
- de l’ordre d’exécution ;
- de la locale ;
- d’un provider réel ;
- d’une donnée externe mutable.

---

# 47. Tests de génération

Les tests du générateur doivent vérifier :

- arborescence ;
- contenu essentiel ;
- stratégies de fichiers ;
- lock ;
- rapport ;
- déterminisme ;
- build du résultat ;
- tests du résultat.

Une génération n’est pas valide uniquement parce que des fichiers ont été créés.

---

# 48. Golden files

Les golden files peuvent être utilisés pour les sorties structurées.

Règles :

- données fictives ;
- format stable ;
- review explicite des diffs ;
- absence de secrets ;
- normalisation des dates et paths ;
- mise à jour uniquement lorsqu’un changement est voulu.

---

# 49. Commandes de validation

Les commandes des suites publiques offline, depuis la racine, sont :

```powershell
dotnet restore SiteFactory.slnx --locked-mode
dotnet build SiteFactory.slnx --configuration Release --no-restore
dotnet test SiteFactory.slnx --configuration Release --no-build --no-restore --filter "Category!=PostgreSql"
```

Le filtre exclut les Facts PostgreSQL réels sans les présenter comme réussis.
Docker/PostgreSQL ne sont pas requis pour ces suites offline. Les guards de
frontières font partie des tests publics ; ne pas les assouplir.

Pour une preuve PostgreSQL explicitement demandée, après restore/build et avec
Docker Linux disponible :

```powershell
pwsh -NoProfile -File tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired
```

[Contrat du harnais](tests/PostgreSql/README.md) : PostgreSQL 18.6, TLS VerifyFull,
23 Facts exacts, cinq shards, 360 s par shard, TRX contrôlés et cleanup strict.
Une absence, un skip, un échec ou un cleanup incomplet ne vaut jamais succès.
Aucun retry jusqu'au vert, réduction des 32 × 24 requêtes P03 ou allongement
des deadlines ne doit masquer un refus.

L’agent doit utiliser le nom réel présent dans le repository.

Il ne doit pas créer artificiellement `SiteFactory.slnx` uniquement pour correspondre à ce document si une autre convention a été validée.

---

## 49.1 Tests ciblés

Pendant le développement, l’agent peut commencer par les tests du projet concerné.

Avant de terminer une tâche structurante, il doit exécuter une validation suffisamment large pour détecter les régressions raisonnables.

---

## 49.2 Échec existant

Si un test échoue avant la modification :

- le confirmer ;
- le documenter ;
- ne pas prétendre l’avoir causé ou corrigé sans preuve ;
- éviter d’utiliser cet échec comme excuse pour ignorer les nouveaux tests.

---

# 50. Quality gates

Une tâche n’est pas terminée si l’un des contrôles applicables échoue :

```text
format

build

tests

analyzers

secret scan

dependency scan

SAST

validation documentaire

validation des manifests

validation Docker

validation OpenAPI
```

L’agent ne doit pas :

- désactiver un analyzer ;
- exclure un fichier ;
- ignorer un warning ;
- baisser un seuil ;
- supprimer un test ;

uniquement pour obtenir un pipeline vert.

---

# 51. Documentation

Toute modification de contrat ou comportement visible doit mettre à jour la documentation correspondante.

Exemples :

- commande CLI ;
- option YAML ;
- Module ID ;
- Error Code ;
- format de manifest ;
- endpoint ;
- procédure ;
- migration ;
- opération de production.

---

## 51.1 Liens

Les liens Markdown doivent rester valides.

---

## 51.2 Exemples

Les exemples doivent être :

- fictifs ;
- cohérents ;
- sans secret ;
- synchronisés avec le contrat réel.

---

# 52. ADR

Un nouvel ADR est nécessaire lorsqu’une tâche introduit notamment :

- nouvelle base ou stockage ;
- nouvelle frontière architecturale ;
- nouvelle dépendance Infrastructure majeure ;
- nouveau modèle de sécurité ;
- nouvelle stratégie de déploiement ;
- nouveau contrat public structurant ;
- changement d’un invariant accepté ;
- décision coûteuse à inverser.

Une correction locale ou un refactoring interne ne nécessite généralement pas d’ADR.

---

## 52.1 Modification d’un ADR accepté

Ne pas réécrire silencieusement la décision.

Créer un nouvel ADR ou demander une décision humaine lorsque la signification change.

---

# 53. Accessibilité et interface

Les interfaces générées doivent viser :

- navigation clavier ;
- labels ;
- focus visible ;
- contrastes suffisants ;
- messages d’erreur compréhensibles ;
- structure sémantique ;
- responsive.

L’agent ne doit pas sacrifier l’accessibilité pour reproduire exactement un effet visuel.

---

# 54. SEO

Les pages publiques doivent conserver :

- titres ;
- descriptions ;
- canonical ;
- robots ;
- sitemap ;
- données structurées ;
- URLs stables ;
- performances ;
- contenu sémantique.

Un module ou template ne doit pas introduire :

- duplicate content involontaire ;
- canonical incohérent ;
- JSON-LD invalide ;
- indexation de pages privées ;
- email exposé sans nécessité.

---

# 55. Performance

L’agent ne doit pas ajouter une optimisation sans mesure ou raison claire.

Avant d’ajouter :

- cache ;
- parallélisme ;
- pooling particulier ;
- préchargement ;
- index complexe ;

identifier :

- coût actuel ;
- bénéfice attendu ;
- invalidation ;
- risques ;
- tests.

La lisibilité et la correction priment sur une micro-optimisation non mesurée.

---

# 56. Compatibilité multiplateforme

Le développement doit rester compatible avec :

```text
Windows x64

Linux x64
```

La production cible principalement Linux.

Ne pas introduire sans abstraction :

- path Windows absolu ;
- path Linux absolu ;
- séparateur codé ;
- script Bash obligatoire pour le développement Windows ;
- script PowerShell obligatoire pour la production Linux.

Utiliser les APIs de paths de .NET.

---

# 57. Scripts

Les scripts doivent :

- utiliser un mode strict lorsque disponible ;
- échouer sur erreur ;
- valider leurs entrées ;
- vérifier la cible ;
- ne contenir aucun secret ;
- produire des messages structurés ;
- être idempotents lorsque prévu.

Un script de production doit proposer :

```text
plan

puis apply
```

pour les opérations sensibles.

---

# 58. Revue de code par l’agent

Lorsqu’il effectue une revue, l’agent doit prioriser :

1. blocants ;
2. vulnérabilités ;
3. perte ou corruption de données ;
4. violation d’ADR ;
5. régression fonctionnelle ;
6. tests insuffisants ;
7. maintenabilité ;
8. améliorations facultatives.

La revue doit citer :

- fichier ;
- ligne ou symbole ;
- impact ;
- scénario ;
- correction attendue.

Elle ne doit pas inventer un problème sans démontrer le chemin d’exécution concerné.

---

# 59. Correction après revue

Une tâche de correction doit :

- reprendre chaque finding ;
- indiquer s’il est corrigé ;
- éviter les refactorings non demandés ;
- ajouter les tests manquants ;
- exécuter les validations ;
- documenter les findings non corrigés.

Ne pas modifier la spécification uniquement pour masquer une non-conformité.

---

# 60. Format attendu d’une tâche

Une tâche de qualité devrait contenir :

```text
contexte

objectif

périmètre

hors portée

ADR applicables

fichiers probables

comportements attendus

critères d’acceptation

tests requis

interdictions

format du compte rendu
```

Si une information peut être résolue en inspectant le repository, l’agent doit l’inspecter avant de poser une question.

---

# 61. Conditions d’arrêt

L’agent doit arrêter l’implémentation et demander une décision lorsque :

```text
deux ADR se contredisent

la tâche demande une action de production

un secret réel serait nécessaire

une migration destructive n’est pas spécifiée

une suppression de données est implicite

un contrat public doit être cassé

un invariant non dérogeable serait violé

un provider réel doit être contacté

la bonne cible ne peut pas être déterminée

un changement dépasse clairement le périmètre
```

---

# 62. Conditions de poursuite sans question

L’agent peut poursuivre sans clarification lorsque :

- la tâche est bornée ;
- les conventions existent ;
- les fichiers sont identifiables ;
- les critères sont objectifs ;
- aucun effet irréversible n’est requis ;
- le repository permet de résoudre les détails.

Il doit alors choisir la solution :

```text
la plus locale

la plus cohérente

la plus testable

la plus sûre
```

---

# 63. Réponse finale de l’agent

Le compte rendu final doit rester factuel.

Format recommandé :

```markdown
## Réalisé

- ...

## Tests

- `commande` — réussite
- `commande` — non exécutée : raison

## Points d’attention

- ...

## Fichiers principaux

- `path`
```

Ne pas déclarer :

```text
tout est parfait

production-ready

sécurisé à 100 %

aucune régression
```

sans preuve suffisante.

---

# 64. Definition of Done d’une tâche

Une tâche est terminée lorsque :

```text
le périmètre est respecté

le code compile

les tests attendus passent

les tests négatifs pertinents existent

les ADR sont respectés

la sécurité n’est pas affaiblie

les erreurs sont structurées

les logs ne divulguent rien

les documents sont à jour

le diff est revu

les limites restantes sont déclarées
```

---

# 65. Repère historique du bootstrap

La séquence suivante rappelle le bootstrap déjà réalisé ; elle n'est pas une
liste de capacités encore absentes ni une autorisation de recommencer ces lots.
Les nouvelles contributions doivent conserver le principe de tâches courtes :

```text
1. Bootstrap de la solution et des projets

2. Conventions de build et tests

3. CLI minimale

4. Modèle de résultat et codes de sortie

5. Chargement YAML

6. Validation du schéma client

7. Génération du plan

8. Abstraction de génération de fichiers

9. Lock SiteFactory

10. Rapport de génération

11. Premier projet généré minimal

12. Build du projet généré

13. Test de déterminisme

14. CI initiale
```

Ne pas commencer simultanément :

- l’administration complète ;
- les 32 ADR ;
- les 500 tâches ;
- le déploiement production ;
- tous les modules commerciaux.

---

# 66. Résumé opérationnel

```text
Lire la tâche

Inspecter le repository

Identifier les ADR

Planifier un petit changement

Implémenter dans la bonne couche

Ajouter les tests

Exécuter les quality gates

Auto-revoir le diff

Rendre compte honnêtement

Ne jamais agir en production implicitement
```

> L’agent doit optimiser la qualité de la décision déjà prise, pas remplacer la décision humaine. Dans SiteFactory, un changement réussi est un changement petit, prouvé, conforme aux contrats et suffisamment explicite pour être maintenu ou repris sans dépendre de la mémoire de son auteur.
