---
adrId: ADR-0013
title: Initialize a local Git repository automatically and keep remote publishing explicit
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
  - ../03-generation/14-client-configuration-schema.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/18-security-baseline.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../05-operations/27-client-handover-and-service-closure.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0002-yaml-client-configuration.md
  - ./0005-file-generation-strategies.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
targetVersion: 0.1.0
---

# ADR-0013 — Initialisation Git locale et publication distante explicite

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La décision ci-dessous définit le comportement Git **cible** de la génération
d'une application cliente ; elle n'est pas révoquée par cette note.
Aujourd'hui la [CLI publique](../03-generation/initial-generation-cli.md)
valide les préférences Git V1 mais n'exécute **ni git init, ni premier commit,
ni remote, ni push**. Le scanner documentaire local lit un repository sans
modifier `.git`. Les commandes Git/remote et rapports détaillés ci-dessous
sont donc des exemples de futur comportement, pas des commandes publiques
déjà livrées ou une autorisation d'opération distante.
Les identifiants `sample-project` sont fictifs ; les URLs d'anti-pattern
illustrent des rejets, jamais des destinations à appeler.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory initialise automatiquement un repository Git local après la génération réussie d’un nouveau projet client.

La génération initiale produit :

```text
un repository Git local
+ une branche principale `main`
+ un premier commit
+ aucun remote
+ aucun push
+ aucune création de repository distant
```

Toute opération distante reste explicite.

SiteFactory ne doit jamais, pendant une simple génération :

- créer un repository GitHub ;
- ajouter un remote ;
- publier du code ;
- pousser une branche ;
- créer un tag ;
- créer une pull request ;
- configurer des secrets GitHub ;
- déclencher un déploiement ;
- modifier un repository distant existant.

---

## 2. Résumé

Le flux de référence est :

```text
configuration validée
→ génération dans un répertoire temporaire
→ validation du projet généré
→ contrôle des fichiers sensibles
→ initialisation Git locale
→ ajout des fichiers autorisés
→ vérification de l’index Git
→ premier commit
→ promotion vers le dossier cible
→ rapport
```

La publication distante suit un flux séparé :

```text
repository local propre
→ revue des fichiers
→ build et tests
→ secret scan
→ choix explicite du fournisseur
→ choix explicite du propriétaire
→ choix explicite du nom
→ choix explicite de la visibilité
→ création d’un remote vide
→ ajout explicite de `origin`
→ push explicite de `main`
→ vérification
→ configuration distante éventuelle
→ rapport
```

Le principe central est :

```text
générer localement
≠ publier à distance
```

---

## 3. Contexte

Conformément à l’ADR-0001, chaque projet client possède son propre repository.

Le repository contient notamment :

- la configuration client ;
- le lock file SiteFactory ;
- l’état de génération ;
- le code généré ;
- les customizations ;
- les migrations ;
- les tests ;
- la documentation ;
- les fichiers de déploiement ;
- les runbooks ;
- l’historique des évolutions.

Git doit être présent dès la création du projet afin de fournir :

- une baseline claire ;
- un historique ;
- des diffs ;
- un rollback des fichiers ;
- une base pour les upgrades ;
- une traçabilité des customizations ;
- une préparation aux revues ;
- une future publication distante.

Cependant, publier automatiquement le projet introduirait des effets de bord importants :

- création d’une ressource distante ;
- exposition possible du code ;
- choix irréversible de visibilité publique ;
- consommation de credentials ;
- mauvaise organisation GitHub ;
- mauvais nom de repository ;
- push vers le mauvais compte ;
- déclenchement involontaire de workflows ;
- publication de fichiers sensibles.

L’initialisation locale peut être déterministe et maîtrisée.

La publication distante doit rester une décision opérateur.

---

## 4. Problème à résoudre

Comment garantir que chaque projet client possède immédiatement un historique Git exploitable tout en empêchant :

- une publication non souhaitée ;
- un push vers le mauvais compte ;
- la création d’un repository public par erreur ;
- l’exposition de secrets ;
- la configuration implicite de credentials ;
- l’exécution de hooks Git non maîtrisés ;
- la création d’un historique incohérent ;
- un force push ;
- l’écrasement d’un repository distant existant ?

La solution doit également permettre :

- une génération sans accès réseau pour l’étape Git ;
- une publication GitHub future ;
- une utilisation d’un autre fournisseur Git ;
- un transfert client ;
- des tests Windows et Linux ;
- une première baseline de commit propre ;
- une procédure de reprise si une étape Git échoue.

---

## 5. Portée

Cette décision couvre :

- l’initialisation locale ;
- la branche initiale ;
- le premier commit ;
- l’identité Git ;
- les hooks ;
- les templates Git ;
- `.gitignore` ;
- `.gitattributes` ;
- les fichiers suivis ;
- les fichiers ignorés ;
- la détection de secrets ;
- les repositories imbriqués ;
- les repositories existants ;
- la configuration des remotes ;
- la création distante ;
- la visibilité ;
- le premier push ;
- les repositories non vides ;
- les force pushes ;
- les rapports ;
- les tests ;
- la sécurité ;
- le transfert client.

---

## 6. Hors portée

Cette décision ne définit pas :

- le fournisseur Git distant obligatoire ;
- le plan tarifaire GitHub ;
- toutes les règles de branch protection ;
- le nombre de reviewers obligatoire ;
- la convention complète de branches après le bootstrap ;
- la stratégie Git Flow ;
- l’usage systématique de Git LFS ;
- l’hébergement Git auto-géré ;
- la réplication de repositories ;
- la signature obligatoire de tous les commits ;
- la gestion juridique de la propriété intellectuelle ;
- la publication open source ;
- le déploiement depuis GitHub Actions.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Aucune publication implicite**
2. **Baseline locale versionnée immédiatement**
3. **Protection contre l’exposition de secrets**
4. **Choix explicite de la visibilité**
5. **Protection des repositories distants existants**
6. **Historique propre et reproductible**
7. **Compatibilité avec les upgrades SiteFactory**
8. **Absence de force push**
9. **Indépendance du fournisseur Git**
10. **Compatibilité Windows et Linux**
11. **Réversibilité**
12. **Transfert client**
13. **Faible dépendance réseau**
14. **Sécurité des hooks et templates Git**
15. **Traçabilité des opérations distantes**

---

## 8. Contraintes

La décision doit respecter :

```text
Un repository par client

Git disponible localement

Branche principale `main`

Premier commit après validation

Aucun secret suivi

Aucun remote créé pendant generate

Aucun réseau requis par l’étape Git locale

Aucun push implicite

Aucun force push depuis SiteFactory

Aucune écriture de credentials dans Git

Aucun hook arbitraire exécuté par le bootstrap

Compatibilité Windows et Linux

Rapports sans secrets
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- Git est installé sur la machine opérateur ;
- le projet est créé dans un dossier dédié ;
- le projet client ne doit pas être imbriqué dans un autre repository ;
- l’identité Git de l’opérateur peut être résolue ou fournie explicitement ;
- le premier commit ne nécessite pas obligatoirement une signature dans le MVP ;
- la publication distante intervient après une revue locale ;
- GitHub constitue le fournisseur initial probable sans être une dépendance du modèle local ;
- le remote distant peut être créé vide ;
- les projets sont suffisamment petits pour être versionnés sans Git LFS dans le MVP.

---

# 10. Option A — Aucun repository automatique

## 10.1 Description

SiteFactory génère uniquement les fichiers.

L’utilisateur initialise Git manuellement.

---

## 10.2 Avantages

- implémentation minimale ;
- aucun besoin de Git dans le générateur ;
- liberté complète pour l’utilisateur ;
- aucun échec lié à l’identité Git.

---

## 10.3 Inconvénients

- oubli possible ;
- baseline initiale imprécise ;
- fichiers modifiés avant le premier commit ;
- état de génération non versionné ;
- qualité variable entre les projets ;
- procédure de bootstrap répétitive.

---

## 10.4 Risques

- premier commit contenant déjà des modifications non identifiées ;
- fichier sensible ajouté ;
- branche initiale différente ;
- oubli du lock file ;
- historique difficile à interpréter.

---

## 10.5 Réversibilité

```text
élevée
```

mais la baseline initiale peut déjà être perdue.

---

# 11. Option B — Initialisation locale et publication automatique

## 11.1 Description

SiteFactory :

- initialise Git ;
- crée un repository GitHub ;
- configure `origin` ;
- pousse `main`.

---

## 11.2 Avantages

- projet immédiatement centralisé ;
- sauvegarde distante ;
- CI rapidement disponible ;
- peu d’actions opérateur.

---

## 11.3 Inconvénients

- credentials nécessaires ;
- effets de bord réseau ;
- choix de visibilité ;
- risque de mauvais compte ;
- risque de mauvais nom ;
- exposition prématurée ;
- erreurs difficiles à annuler ;
- dépendance au fournisseur.

---

## 11.4 Risques

- repository public ;
- code client exposé ;
- secrets publiés ;
- push vers une organisation incorrecte ;
- workflows déclenchés ;
- repository distant pollué.

---

## 11.5 Réversibilité

```text
faible
```

Une publication publique ou un push distant peut laisser des copies, caches et historiques hors du contrôle local.

---

# 12. Option C — Repository local sans premier commit

## 12.1 Description

SiteFactory exécute uniquement :

```bash
git init
```

Les fichiers restent non suivis.

---

## 12.2 Avantages

- initialisation simple ;
- aucun besoin d’identité Git ;
- l’utilisateur choisit le contenu du premier commit ;
- aucun remote.

---

## 12.3 Inconvénients

- absence de baseline ;
- fichiers sensibles pouvant être ajoutés plus tard ;
- état de génération non figé ;
- l’utilisateur peut oublier le commit ;
- upgrade impossible avant la création d’une référence stable.

---

## 12.4 Risques

- premier commit incomplet ;
- lock file oublié ;
- `.gitignore` modifié trop tard ;
- customizations mélangées à la génération initiale.

---

## 12.5 Réversibilité

```text
élevée
```

---

# 13. Option D — Repository local et premier commit, publication explicite

## 13.1 Description

SiteFactory :

- génère le projet ;
- le valide ;
- initialise Git localement ;
- crée la branche `main` ;
- contrôle les fichiers suivis ;
- crée le premier commit ;
- ne configure aucun remote.

La publication distante est une opération séparée.

---

## 13.2 Avantages

- baseline immédiate ;
- absence de réseau ;
- absence de publication implicite ;
- historique propre ;
- support des upgrades ;
- contrôle des fichiers sensibles ;
- choix distant conservé ;
- transfert facilité ;
- fonctionnement indépendant de GitHub.

---

## 13.3 Inconvénients

- Git requis ;
- identité Git requise ;
- gestion des erreurs de commit ;
- règles supplémentaires ;
- publication en plusieurs étapes ;
- tests Git nécessaires.

---

## 13.4 Risques

- identité incorrecte ;
- hook global exécuté ;
- fichier sensible indexé ;
- commit non signé malgré une politique locale ;
- échec après génération.

---

## 13.5 Réversibilité

```text
élevée
```

Le repository reste entièrement local tant que l’opérateur ne publie pas.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Aucun Git | Git sans commit | Git + publication auto | Git local + commit |
|---|---:|---:|---:|---:|---:|
| Baseline initiale | 5 | 1 | 2 | 5 | 5 |
| Absence de publication implicite | 5 | 5 | 5 | 1 | 5 |
| Protection des secrets | 5 | 2 | 3 | 2 | 5 |
| Support des upgrades | 5 | 2 | 3 | 5 | 5 |
| Indépendance fournisseur | 4 | 5 | 5 | 1 | 5 |
| Simplicité opérateur | 3 | 2 | 3 | 5 | 4 |
| Réversibilité | 5 | 4 | 5 | 2 | 5 |
| Traçabilité | 5 | 1 | 2 | 5 | 5 |
| Transfert client | 4 | 3 | 3 | 4 | 5 |
| Sécurité | 5 | 3 | 4 | 2 | 5 |

L’option D est retenue.

---

# 15. Décision

Nous décidons que la génération initiale d’un projet client initialise automatiquement un repository Git local et crée un premier commit.

La génération ne crée aucun remote.

Le résultat attendu après succès est :

```bash
git status
```

```text
On branch main
nothing to commit, working tree clean
```

et :

```bash
git remote
```

```text
aucune sortie
```

---

## 15.1 Branche initiale

La branche initiale est toujours :

```text
main
```

Le résultat ne dépend pas de la valeur globale :

```text
init.defaultBranch
```

de l’utilisateur.

---

## 15.2 Premier commit

Le message de référence est :

```text
chore: initialize SiteFactory client project
```

Un corps de commit peut enregistrer des métadonnées non sensibles :

```text
SiteFactory-Generator-Version: 0.1.0
SiteFactory-Configuration-SHA256: <sha256>
```

---

## 15.3 Aucun remote

Le premier commit ne contient aucune information de publication distante obligatoire.

---

# 16. Pipeline de bootstrap Git

Le pipeline complet est :

```text
préflight
→ génération temporaire
→ validation des fichiers
→ restore et build requis
→ tests requis
→ scan de secrets
→ création du repository Git
→ création de la branche main
→ indexation contrôlée
→ inspection de l’index
→ premier commit
→ vérification du repository
→ promotion du dossier
→ rapport
```

---

## 16.1 Répertoire temporaire

Le repository Git est initialisé dans le répertoire temporaire de génération avant sa promotion vers le chemin final.

Cette stratégie permet d’éviter de livrer un projet partiellement initialisé.

---

## 16.2 Même filesystem

Le répertoire temporaire doit se trouver sur le même filesystem que la destination lorsque la promotion atomique est attendue.

---

## 16.3 Promotion

Le dossier complet, y compris `.git`, est déplacé vers la destination finale après validation.

---

# 17. Préflight Git

Avant toute génération avec bootstrap Git, SiteFactory vérifie :

```text
[ ] Git installé
[ ] Version Git supportée
[ ] Exécutable accessible
[ ] Identité Git résolue ou fournie
[ ] Dossier cible valide
[ ] Dossier cible hors de tout repository existant
[ ] Aucun repository imbriqué
[ ] Aucun secret requis pour l’opération locale
```

---

## 17.1 Version Git

La version minimale doit supporter :

```text
git init --initial-branch
```

La version exacte supportée est définie dans la matrice de compatibilité SiteFactory.

---

## 17.2 Commande non disponible

L’absence de Git provoque un échec avant la création du projet final.

---

# 18. Résolution de l’identité Git

Le premier commit nécessite :

```text
user.name
user.email
```

---

## 18.1 Sources

L’identité peut provenir, par ordre de préférence :

1. d’arguments SiteFactory explicitement fournis ;
2. de la configuration Git locale du contexte opérateur ;
3. de la configuration Git globale.

---

## 18.2 Absence

SiteFactory ne doit pas inventer :

- un nom ;
- une adresse email ;
- une adresse LogicGate ;
- une identité client.

L’absence d’identité produit une erreur avant le premier commit.

---

## 18.3 Configuration globale

SiteFactory ne modifie jamais automatiquement :

```text
~/.gitconfig
```

ou la configuration système Git.

---

## 18.4 Configuration locale

Une identité fournie explicitement peut être enregistrée dans la configuration locale du nouveau repository.

---

## 18.5 Vie privée

L’opérateur peut utiliser une adresse de commit dédiée ou protégée selon la politique de son fournisseur Git.

---

# 19. Signature du premier commit

La signature cryptographique du premier commit n’est pas obligatoire dans le MVP.

---

## 19.1 Configuration existante

SiteFactory ne doit pas désactiver silencieusement une politique de signature configurée par l’utilisateur.

---

## 19.2 Échec de signature

Si la configuration exige une signature mais que celle-ci ne peut pas être produite :

- le commit échoue ;
- le projet final n’est pas promu comme génération réussie ;
- le rapport explique la précondition manquante.

---

## 19.3 Option explicite

Une future option peut permettre :

```text
--sign-initial-commit
```

ou une dérogation explicitement confirmée.

---

## 19.4 Pas de clé générée

SiteFactory ne génère pas de clé de signature Git.

---

# 20. Isolation des hooks Git

Le bootstrap automatique ne doit pas exécuter de hook Git provenant :

- de la configuration globale ;
- d’un template Git utilisateur ;
- d’un `core.hooksPath` global ;
- d’un outil tiers ;
- d’un repository parent.

---

## 20.1 Raisons

Un hook peut :

- exécuter du code ;
- modifier les fichiers ;
- envoyer des données ;
- effectuer un accès réseau ;
- faire échouer le commit ;
- introduire une dépendance locale invisible.

---

## 20.2 Répertoire de hooks contrôlé

L’initialisation et le premier commit utilisent un répertoire de hooks vide et contrôlé.

---

## 20.3 Après génération

Le repository généré ne contient pas de hook exécutable installé automatiquement.

---

## 20.4 CI avant hooks

Les quality gates reposent sur :

- la CI ;
- les tests ;
- les analyzers ;
- les scans ;

et non sur des hooks locaux obligatoires.

---

## 20.5 Hooks futurs

Un système de hooks gérés nécessiterait :

- opt-in explicite ;
- code versionné ;
- revue ;
- compatibilité multiplateforme ;
- décision séparée.

---

# 21. Template Git contrôlé

SiteFactory ne doit pas dépendre implicitement du template Git global de l’utilisateur.

---

## 21.1 Risque

Un template global peut injecter :

- hooks ;
- fichiers ;
- configuration ;
- contenu non prévu.

---

## 21.2 Initialisation

Le bootstrap utilise un template vide ou contrôlé par SiteFactory.

---

## 21.3 Contenu autorisé

Le repository reçoit uniquement les fichiers prévus par la génération.

---

# 22. Attributs Git globaux

Les attributs Git globaux peuvent définir :

- filtres ;
- encodages ;
- règles de diff ;
- transformations.

---

## 22.1 Clean filters

Un filtre `clean` peut exécuter une commande lors de :

```bash
git add
```

---

## 22.2 Baseline

Le bootstrap doit isoler autant que possible les attributs globaux non maîtrisés.

Les attributs propres au repository restent définis dans :

```text
.gitattributes
```

---

## 22.3 Filtres générés

Le fichier `.gitattributes` SiteFactory ne doit pas définir de filtre exécutable dans le MVP.

---

# 23. `.gitignore`

SiteFactory génère un `.gitignore` adapté aux technologies utilisées.

Il doit notamment exclure :

```text
bin/
obj/
.vs/
.idea/
*.user
*.suo
TestResults/
coverage/
artifacts/
*.log
.env
.env.*
!.env.*.example
.secrets/
secrets/
*.pfx
*.p12
*.key
*.pem
```

La liste exacte doit rester adaptée au repository et éviter d’ignorer un fichier obligatoire.

---

## 23.1 Fichiers d’exemple

Les fichiers suivants peuvent être suivis :

```text
.env.production.example
.env.staging.example
```

s’ils ne contiennent aucune valeur sensible.

---

## 23.2 `client.sitefactory.yml`

Le fichier canonique est suivi.

---

## 23.3 Lock files

Les fichiers suivants sont suivis :

```text
sitefactory.lock.json
packages.lock.json
.config/dotnet-tools.json
```

lorsqu’ils existent dans le projet.

---

## 23.4 État de génération

Le fichier suivant est suivi :

```text
.sitefactory/generation-state.json
```

---

## 23.5 Secrets locaux

Les répertoires et fichiers de secrets réels sont ignorés.

---

# 24. `.gitattributes`

SiteFactory génère un `.gitattributes` afin de stabiliser :

- fins de ligne ;
- fichiers binaires ;
- scripts ;
- diffs ;
- compatibilité Windows et Linux.

---

## 24.1 Baseline

Exemple conceptuel :

```gitattributes
* text=auto

*.cs text eol=lf
*.csproj text eol=lf
*.slnx text eol=lf
*.json text eol=lf
*.yml text eol=lf
*.yaml text eol=lf
*.md text eol=lf
*.sh text eol=lf

*.png binary
*.jpg binary
*.jpeg binary
*.webp binary
*.pdf binary
```

---

## 24.2 Scripts PowerShell

Les règles doivent rester compatibles avec les scripts Windows générés.

---

## 24.3 Renormalisation

Les fichiers sont ajoutés après la création de `.gitattributes` afin que l’index initial utilise les règles prévues.

---

# 25. Fichiers suivis dans le premier commit

Le premier commit doit contenir au minimum, selon la composition :

```text
client.sitefactory.yml

sitefactory.lock.json

.sitefactory/generation-state.json

README.md

AGENTS.md

CHANGELOG.md

.gitignore

.gitattributes

global.json

Directory.Build.props

Directory.Packages.props

solution .slnx

src/

tests/

deploy/

docs/

schemas/
```

---

## 25.1 Configuration runtime

Seules les configurations non sensibles sont suivies.

---

## 25.2 Déploiement

Les fichiers Compose, Caddy et scripts suivis ne contiennent aucun secret réel.

---

## 25.3 Rapports

Un rapport de génération peut être suivi lorsqu’il est :

- explicitement prévu ;
- nettoyé ;
- indépendant du chemin local ;
- sans secret ;
- utile à la traçabilité.

Les rapports purement temporaires restent ignorés.

---

# 26. Fichiers interdits dans le premier commit

Le premier commit ne doit pas contenir :

```text
.env réel

fichier secret

mot de passe

token

clé privée

certificat privé

dump PostgreSQL

backup

log

artefact bin/obj

cache NuGet

donnée client réelle

configuration de production réelle

fichier IDE utilisateur

credential Git
```

---

# 27. Inspection de l’index Git

Avant le commit, SiteFactory doit inspecter les fichiers indexés.

---

## 27.1 Vérifications

```text
Chemins autorisés

Aucun fichier ignoré forcé

Aucun secret détecté

Aucun fichier hors racine

Aucun repository imbriqué

Aucun fichier trop volumineux inattendu

Aucun fichier temporaire

Aucun chemin absolu dans un artefact généré
```

---

## 27.2 `git add --force`

SiteFactory ne doit pas utiliser une option globale forçant l’ajout de fichiers ignorés.

---

## 27.3 Ajout ciblé

Le pipeline peut utiliser :

```bash
git add --all
```

uniquement après validation du contenu et du `.gitignore`.

---

# 28. Secret scanning avant le commit

Un contrôle de secrets doit être exécuté avant le premier commit.

---

## 28.1 Périmètre

Le scan couvre :

- fichiers suivis ;
- fichiers générés ;
- configurations ;
- scripts ;
- packages ou artefacts inclus ;
- fichiers cachés.

---

## 28.2 Finding

Un finding potentiellement réel bloque le commit jusqu’à résolution.

---

## 28.3 Faux positif

Une exception nécessite :

- preuve ;
- règle limitée ;
- justification ;
- revue.

---

## 28.4 Pas de commit puis correction

La baseline consiste à empêcher l’entrée du secret dans l’historique, pas à le supprimer après le premier commit.

---

# 29. Fichiers volumineux

Un fichier volumineux inattendu bloque ou avertit selon le seuil défini.

---

## 29.1 Données persistantes

Les médias et documents runtime appartiennent à :

```text
App_Data
```

ou au stockage prévu.

Ils ne sont pas committés dans le repository applicatif.

---

## 29.2 Assets de conception

Un asset nécessaire au build peut être suivi si :

- sa taille est raisonnable ;
- sa licence est connue ;
- son origine est documentée ;
- il est réellement requis.

---

# 30. Git LFS

Git LFS n’est pas activé par défaut dans le MVP.

---

## 30.1 Raisons

- service distant supplémentaire ;
- configuration ;
- quotas ;
- transfert client ;
- authentification ;
- dépendance fournisseur.

---

## 30.2 Introduction

Git LFS nécessite :

- besoin démontré ;
- fournisseur ;
- stratégie de transfert ;
- règles `.gitattributes` ;
- documentation ;
- validation humaine.

---

# 31. Submodules Git

Les submodules Git ne sont pas utilisés dans les repositories clients standards.

---

## 31.1 Raisons

- restore plus complexe ;
- credentials supplémentaires ;
- état détaché ;
- transfert ;
- CI ;
- mise à jour ;
- dépendance implicite.

---

## 31.2 Packages

Les composants génériques sont distribués par packages et versions conformément à l’ADR-0008.

---

# 32. Repository imbriqué

SiteFactory refuse de créer un repository client à l’intérieur d’un repository Git existant.

---

## 32.1 Détection

Le préflight vérifie les dossiers parents.

---

## 32.2 Raisons

Un repository imbriqué peut :

- ne pas apparaître correctement dans le parent ;
- être traité comme sous-module involontaire ;
- créer des commits incomplets ;
- compliquer le transfert.

---

## 32.3 Exception

Le support explicite d’un monorepo client nécessiterait un profil ou ADR distinct.

---

# 33. Repository déjà présent

La commande de génération initiale ne doit pas réinitialiser ou modifier un repository existant.

---

## 33.1 Dossier contenant `.git`

Le comportement est :

```text
fail closed
```

---

## 33.2 Adoption

Un projet existant utilise une future commande distincte telle que :

```text
sitefactory repository adopt
```

---

## 33.3 Aucun `git init` silencieux

SiteFactory ne doit pas transformer un dossier existant en repository sans que l’opérateur l’ait demandé explicitement.

---

# 34. Dossier cible

La génération initiale cible par défaut un dossier absent.

---

## 34.1 Dossier vide

Un dossier vide peut être accepté après vérification explicite.

---

## 34.2 Dossier non vide

Un dossier non vide est refusé pour une génération initiale.

---

## 34.3 Fichiers cachés

Un dossier contenant uniquement des fichiers cachés n’est pas considéré automatiquement comme vide.

---

# 35. Atomicité

L’initialisation Git fait partie de la transaction logique de génération initiale.

---

## 35.1 Succès

Le projet final n’est considéré comme généré avec succès que lorsque :

```text
fichiers validés
+ repository initialisé
+ premier commit réussi
+ working tree propre
```

---

## 35.2 Échec avant promotion

Le dossier temporaire est nettoyé ou conservé dans un emplacement de diagnostic contrôlé.

La destination finale reste inchangée.

---

## 35.3 Rapport d’échec

Le rapport indique :

- étape ;
- code ;
- commande logique ;
- cause ;
- action de reprise.

Il ne contient aucun credential.

---

# 36. Premier commit

Le premier commit représente :

```text
la baseline générée et validée
```

---

## 36.1 Contenu fonctionnel

Aucune customization développée après génération ne doit être incluse dans ce commit.

---

## 36.2 Date

La date du commit est la date réelle de génération.

Le commit hash n’est donc pas considéré comme un artefact déterministe entre deux générations séparées.

---

## 36.3 Métadonnées

Le hash de configuration et la version du générateur permettent de relier le commit à la composition.

---

## 36.4 Tag

Aucun tag n’est créé automatiquement.

---

# 37. Vérification après commit

SiteFactory vérifie :

```text
Branche = main

HEAD présent

Un commit initial présent

Working tree propre

Aucun remote

Fichiers obligatoires suivis

Fichiers interdits absents

Lock files suivis

État de génération suivi
```

---

# 38. Rapport Git local

Le rapport de génération peut contenir :

```json
{
  "git": {
    "initialized": true,
    "branch": "main",
    "initialCommitCreated": true,
    "initialCommitSha": "abc1234...",
    "remoteConfigured": false,
    "workingTreeClean": true
  }
}
```

---

## 38.1 Données interdites

Le rapport ne contient pas :

- email de commit si non nécessaire ;
- token ;
- credential ;
- chemin privé sensible ;
- remote contenant un secret.

---

# 39. Aucun accès réseau pendant le bootstrap Git

Les opérations suivantes sont interdites pendant le bootstrap local :

```text
git fetch

git pull

git push

git clone

git ls-remote

gh repo create

appel API GitHub

appel API GitLab
```

---

## 39.1 Génération globale

D’autres étapes SiteFactory peuvent nécessiter des packages ou ressources approuvées.

La règle de cette section concerne spécifiquement le bootstrap Git.

---

# 40. Remote absent par défaut

Après génération :

```bash
git remote -v
```

doit retourner une liste vide.

---

## 40.1 Pas de placeholder

SiteFactory ne doit pas ajouter :

```text
origin https://example.invalid/...
```

comme placeholder.

---

## 40.2 Documentation

Le README peut expliquer comment publier, sans exécuter l’opération.

---

# 41. Publication distante séparée

La publication utilise une commande distincte ou une procédure manuelle.

Commande conceptuelle :

```bash
sitefactory repository publish \
  --provider github \
  --owner logicgate \
  --repository sample-project \
  --visibility private
```

La syntaxe finale reste définie dans la spécification CLI.

---

## 41.1 Effet de bord explicite

Le nom de la commande doit rendre la publication évidente.

---

## 41.2 Confirmation

Une exécution interactive demande confirmation.

Une exécution non interactive nécessite une option dédiée telle que :

```text
--confirm-publish
```

---

## 41.3 Aucun appel depuis `generate`

La commande `generate` ne doit pas appeler automatiquement `repository publish`.

---

# 42. Fournisseur distant

Le repository local reste compatible avec tout serveur Git standard.

---

## 42.1 Fournisseur initial

GitHub peut constituer le premier adaptateur de publication.

---

## 42.2 Domain

Les contrats de génération et de repository local ne dépendent pas de GitHub.

---

## 42.3 Nouveau fournisseur

Un adaptateur GitLab, Gitea ou autre peut être ajouté sans modifier la génération locale.

---

# 43. Paramètres obligatoires de publication

Une publication doit déterminer explicitement :

```text
Provider

Remote host

Owner ou organization

Repository name

Visibility

Remote name

Local branch

Remote branch
```

---

## 43.1 Valeurs implicites limitées

Le remote name peut utiliser :

```text
origin
```

comme valeur par défaut documentée.

La visibilité ne doit pas être ambiguë.

---

## 43.2 Mode non interactif

En mode non interactif, la visibilité doit être fournie explicitement.

---

# 44. Nom du repository distant

Le nom doit être :

- validé ;
- stable ;
- sans donnée sensible ;
- sans caractères interdits ;
- cohérent avec l’identité technique du projet.

---

## 44.1 Nom commercial

Le nom commercial du client ne doit pas être utilisé si cela révèle une relation confidentielle non prévue.

---

## 44.2 Convention conceptuelle

Exemples :

```text
client-sample-project

site-sample-project

sample-project-web
```

La convention définitive peut dépendre de l’organisation Git.

---

## 44.3 Collision

Un repository existant portant le même nom ne doit pas être réutilisé automatiquement.

---

# 45. Visibilité

Valeurs :

```text
private
internal
public
```

selon les capacités du fournisseur.

---

## 45.1 Baseline

Les projets clients sont privés par défaut sur le plan produit.

Toutefois, une commande non interactive ne doit pas déduire silencieusement cette valeur.

---

## 45.2 Publication publique

Une publication publique nécessite une confirmation renforcée :

```text
--visibility public
--confirm-public
--confirm-publish
```

ou un équivalent interactif explicite.

---

## 45.3 Contrôle préalable

Avant une publication publique, vérifier :

- licence ;
- propriété intellectuelle ;
- données client ;
- secrets ;
- historique ;
- packages privés ;
- documentation ;
- images ;
- noms de fournisseurs ;
- customizations.

---

## 45.4 Irréversibilité

Passer ultérieurement le repository en privé ne garantit pas la disparition des copies déjà récupérées.

---

# 46. Organisation distante

La publication doit identifier explicitement :

- compte personnel ;
- organisation LogicGate ;
- organisation client ;
- organisation partenaire.

---

## 46.1 Contexte d’authentification

Le fait que l’outil CLI soit authentifié auprès d’un compte ne signifie pas que ce compte constitue la cible correcte.

---

## 46.2 Vérification

L’opérateur doit voir avant confirmation :

```text
Provider
Host
Owner
Repository
Visibility
```

---

# 47. Création du repository distant

Le remote est créé vide.

---

## 47.1 Initialisation distante interdite

Lors de la création, ne pas ajouter automatiquement :

- README ;
- `.gitignore` ;
- licence ;
- premier commit ;
- branche par défaut distincte.

---

## 47.2 Raisons

Le repository local possède déjà son historique initial.

Une initialisation distante créerait deux historiques divergents.

---

# 48. Repository distant existant

SiteFactory ne pousse pas automatiquement vers un repository distant existant non vérifié.

---

## 48.1 Détection

L’outil doit vérifier si le repository existe et s’il contient :

- branches ;
- tags ;
- commits ;
- protections ;
- fichiers.

---

## 48.2 Repository non vide

Un repository non vide produit un refus par défaut.

---

## 48.3 Historique identique

Une reprise peut être autorisée uniquement si l’historique distant est compatible et qu’aucun push forcé n’est requis.

---

## 48.4 Adoption

La liaison à un repository distant préexistant utilise une procédure distincte et plus prudente.

---

# 49. Remote `origin`

Après création distante réussie, SiteFactory peut ajouter :

```text
origin
```

---

## 49.1 Remote existant

Un remote local existant n’est jamais remplacé silencieusement.

---

## 49.2 Remplacement

Modifier l’URL d’un remote nécessite une commande et confirmation distinctes.

---

## 49.3 Plusieurs remotes

La gestion de plusieurs remotes reste manuelle ou utilise une commande dédiée.

---

# 50. Validation de l’URL distante

Une URL de remote doit être validée avant ajout.

---

## 50.1 Formats possibles

Selon le fournisseur :

```text
https://host/owner/repository.git

ssh://git@host/owner/repository.git

git@host:owner/repository.git
```

---

## 50.2 Credentials intégrés

Les URLs suivantes sont refusées :

```text
https://user:password@host/repository.git

https://token@host/repository.git
```

---

## 50.3 Schémas interdits

Les schémas non prévus ou exécutables sont refusés.

---

## 50.4 Host

Le host doit correspondre au fournisseur sélectionné ou à une cible explicitement approuvée.

---

# 51. HTTPS ou SSH

Les deux modes peuvent être supportés.

---

## 51.1 HTTPS

Le credential utilise :

- gestionnaire de credentials ;
- token injecté ;
- authentification fournisseur.

Il n’est pas enregistré dans l’URL.

---

## 51.2 SSH

L’authentification utilise une clé configurée par l’opérateur.

SiteFactory ne génère pas automatiquement une clé SSH de publication.

---

## 51.3 Choix

Le choix du protocole reste explicite ou suit une configuration locale documentée.

---

# 52. Vérification de l’hôte SSH

La première connexion SSH peut demander la validation de l’empreinte de l’hôte.

---

## 52.1 Acceptation automatique

SiteFactory ne doit pas désactiver la vérification de l’hôte avec :

```text
StrictHostKeyChecking=no
```

dans la baseline.

---

## 52.2 Provisionnement

Les hôtes approuvés peuvent être provisionnés par une procédure opérateur contrôlée.

---

# 53. Credentials de publication

Les credentials de publication ne doivent pas être :

- stockés dans le repository ;
- enregistrés dans un rapport ;
- passés comme argument visible ;
- copiés dans une configuration cliente ;
- inclus dans un fichier `.env` versionné.

---

## 53.1 Permissions

Le credential utilise les permissions minimales nécessaires à :

- créer le repository lorsque demandé ;
- pousser le code ;
- configurer les éléments explicitement choisis.

---

## 53.2 Durée

Les credentials temporaires sont préférables lorsque le fournisseur le permet.

---

# 54. Préconditions avant publication

Avant toute publication :

```text
[ ] Repository local présent
[ ] Branche main présente
[ ] Premier commit présent
[ ] Working tree propre
[ ] Aucun remote inattendu
[ ] Build Release réussi
[ ] Tests réussis
[ ] Secret scan réussi
[ ] Fichiers volumineux inspectés
[ ] Visibilité explicitement choisie
[ ] Owner explicitement choisi
[ ] Nom validé
[ ] Credential disponible
[ ] Remote vide ou compatible
[ ] Aucun force push nécessaire
```

---

# 55. Working tree propre

La publication refuse un working tree contenant :

- fichiers modifiés ;
- fichiers indexés non committés ;
- fichiers non suivis inattendus ;
- conflits ;
- opération de merge ou rebase incomplète.

---

## 55.1 Justification

Le contenu publié doit correspondre exactement à un commit local identifiable.

---

## 55.2 Fichiers ignorés

Les secrets ignorés peuvent être présents localement.

Ils ne doivent pas apparaître dans l’index ou l’historique.

---

# 56. Build et tests avant publication

Le premier push doit être précédé des quality gates adaptées.

Baseline :

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

avec les options verrouillées prévues par le repository.

---

## 56.1 Échec

Un échec bloque la publication automatisée.

---

## 56.2 Dérogation

Une dérogation nécessite une décision humaine et reste visible dans le rapport.

---

# 57. Scan de secrets avant publication

Un second scan est exécuté avant le push afin de couvrir :

- les commits locaux ;
- les modifications depuis la génération ;
- les nouveaux fichiers ;
- l’historique local complet.

---

## 57.1 Finding

Un secret détecté bloque le push.

---

## 57.2 Secret déjà committé

Il faut :

- révoquer ;
- nettoyer l’historique si nécessaire ;
- recréer le commit ;
- rescanner.

---

# 58. Premier push

Le push de référence est conceptuellement :

```bash
git push --set-upstream origin main
```

---

## 58.1 Pas de force

SiteFactory n’utilise pas :

```text
--force

--force-with-lease

+
```

dans une refspec de publication.

---

## 58.2 Fast-forward

Le serveur doit accepter un push normal.

---

## 58.3 Échec

Un rejet est présenté à l’opérateur sans tentative automatique de contournement.

---

# 59. Force push

Les commandes SiteFactory de publication n’effectuent jamais de force push.

---

## 59.1 Même sur repository vide

Un repository correctement créé vide ne nécessite pas de force.

---

## 59.2 Historique distant divergent

Une divergence nécessite :

- inspection ;
- adoption ;
- merge ;
- recréation du remote ;
- décision humaine.

---

## 59.3 Branches protégées

SiteFactory ne contourne pas une protection distante.

---

# 60. Tags

La publication initiale ne crée pas automatiquement de tag.

---

## 60.1 Release

Un tag appartient au processus de release défini dans :

```text
26-release-and-versioning.md
```

---

## 60.2 Push de tags

SiteFactory ne pousse pas tous les tags avec :

```bash
git push --tags
```

pendant la publication initiale.

---

# 61. Configuration distante après push

Après le premier push, des opérations optionnelles peuvent être réalisées :

- définir la branche par défaut ;
- configurer la protection ;
- activer les scans ;
- configurer les environnements ;
- ajouter des secrets ;
- configurer les permissions ;
- créer des labels ;
- activer les issues.

---

## 61.1 Explicite

Chaque famille d’opérations doit être explicitement demandée ou appartenir à un profil de publication préalablement validé.

---

## 61.2 Pas de secret automatique

SiteFactory ne copie pas automatiquement des secrets locaux vers GitHub.

---

# 62. Branche par défaut distante

La branche par défaut doit être :

```text
main
```

---

## 62.1 Vérification

Après push, l’outil vérifie que le fournisseur utilise bien `main`.

---

## 62.2 Divergence

Une branche par défaut différente produit un diagnostic.

---

# 63. Protection de branche

La baseline recommandée comprend selon le contexte :

- interdiction des force pushes ;
- interdiction des suppressions ;
- status checks requis ;
- revue par pull request ;
- historique lisible ;
- restrictions d’accès.

---

## 63.1 Développeur unique

Le nombre obligatoire d’approbations peut être adapté au fait que LogicGate soit initialement maintenu par une seule personne.

---

## 63.2 Quality gates

L’absence de reviewer distinct ne justifie pas la suppression :

- des tests ;
- de la CI ;
- des analyses ;
- des revues Codex.

---

## 63.3 Configuration automatique

Une future commande peut appliquer un profil de protection après confirmation explicite.

---

# 64. Workflows GitHub Actions

Les workflows peuvent être présents dans le premier commit.

---

## 64.1 Premier push

Le premier push peut déclencher les workflows.

L’opérateur doit le savoir avant confirmation.

---

## 64.2 Secrets absents

Les workflows doivent échouer proprement ou ignorer les jobs optionnels nécessitant des secrets encore absents.

---

## 64.3 Permissions

Les workflows suivent l’ADR-0008.

---

# 65. Secrets GitHub

Les secrets GitHub sont configurés après la création du repository.

---

## 65.1 Valeurs

SiteFactory ne lit ni ne transfère automatiquement les secrets locaux du projet.

---

## 65.2 Configuration

L’outil peut produire une liste de Secret IDs requis.

L’opérateur configure les valeurs via le fournisseur.

---

## 65.3 Pull requests

Les secrets sensibles ne sont pas disponibles dans les workflows non approuvés.

---

# 66. Publication et déploiement

Publier le repository ne déploie pas l’application.

---

## 66.1 Séparation

```text
repository publish
≠ release
≠ image publish
≠ deploy
```

---

## 66.2 Effets de bord

Chaque opération possède :

- sa commande ;
- son plan ;
- ses confirmations ;
- son rapport.

---

# 67. Rapport de publication

Une publication produit :

```text
remote-publish-report.json
```

---

## 67.1 Contenu conceptuel

```json
{
  "schemaVersion": 1,
  "publishRunId": "eb74fc73-9857-458a-a54e-0f83868f74c8",
  "provider": "github",
  "host": "github.com",
  "owner": "logicgate",
  "repository": "sample-project-web",
  "visibility": "private",
  "remoteName": "origin",
  "branch": "main",
  "commitSha": "abc1234...",
  "repositoryCreated": true,
  "remoteConfigured": true,
  "pushCompleted": true,
  "defaultBranchVerified": true,
  "status": "success"
}
```

---

## 67.2 Données interdites

Le rapport ne contient pas :

- token ;
- credential ;
- adresse contenant une authentification ;
- clé SSH ;
- données client ;
- contenu des commits.

---

# 68. Publication partielle

Une publication distante ne peut pas toujours être atomique.

Exemples :

```text
repository créé
+ push échoué

push réussi
+ protection de branche échouée
```

---

## 68.1 Pas de suppression automatique

SiteFactory ne supprime pas automatiquement un repository distant créé lorsque l’étape suivante échoue.

---

## 68.2 Raisons

La suppression distante est une opération destructive pouvant :

- supprimer une ressource utile ;
- masquer un incident ;
- échouer partiellement ;
- nécessiter une validation différente.

---

## 68.3 Rapport

Le rapport doit indiquer précisément les étapes réussies et échouées.

---

## 68.4 Reprise

Une seconde exécution peut reprendre uniquement après vérification de l’état distant.

---

# 69. Repository distant créé mais vide

Si la création réussit et le push échoue :

- conserver le repository ;
- conserver le remote local si son URL est correcte ;
- résoudre la cause ;
- réexécuter un push normal ;
- ne pas utiliser force.

---

# 70. Push réussi mais configuration incomplète

Si le push réussit mais qu’une configuration optionnelle échoue :

- le code reste publié ;
- la publication reçoit un statut partiel ;
- les protections manquantes sont listées ;
- aucune tentative destructive n’est effectuée.

---

# 71. Remote local incorrect

Si un remote a été ajouté avec une URL incorrecte avant le push :

- interrompre ;
- ne pas pousser ;
- demander une correction explicite ;
- produire un rapport.

---

## 71.1 Après push

Une publication vers la mauvaise destination est traitée comme un incident.

Elle peut nécessiter :

- restriction immédiate de visibilité ;
- suppression distante validée ;
- révocation ;
- analyse de fuite ;
- nouvelle publication.

---

# 72. Détection d’un mauvais compte

Avant création ou push, afficher :

```text
Authenticated account
Target owner
Repository
Visibility
```

---

## 72.1 Incohérence

Une incohérence entre le compte authentifié et le propriétaire cible doit être confirmée ou refusée selon les permissions attendues.

---

# 73. Publication publique accidentelle

Une publication publique accidentelle constitue un incident de sécurité.

---

## 73.1 Réponse

```text
rendre privé si possible
→ révoquer les secrets potentiels
→ analyser le contenu
→ identifier les clones et caches possibles
→ nettoyer l’historique
→ documenter
```

---

## 73.2 Limite

Rendre le repository privé ne garantit pas que le contenu n’a pas été copié.

---

# 74. Historique local avant publication

L’opérateur doit pouvoir inspecter :

```bash
git log --oneline --decorate
```

et :

```bash
git status
```

avant publication.

---

## 74.1 Nombre de commits

Le premier push peut contenir :

- uniquement le commit initial ;
- ou plusieurs commits locaux revus.

---

## 74.2 Pas de squash automatique

SiteFactory ne réécrit pas automatiquement l’historique local avant le premier push.

---

# 75. Amend et rebase

Les opérations d’historique local restent manuelles.

---

## 75.1 Avant publication

Un amend ou rebase local est possible tant que le repository n’a pas été publié.

---

## 75.2 Automatisation

SiteFactory ne lance pas automatiquement :

```text
git rebase
git commit --amend
git filter-repo
```

---

# 76. Convention de commits

Le premier commit utilise une convention stable.

Les commits suivants peuvent suivre :

```text
Conventional Commits
```

ou la convention définie dans `CONTRIBUTING.md`.

---

## 76.1 SiteFactory

Les outils de génération ne doivent pas créer de nombreux commits internes sans contrat explicite.

---

# 77. Upgrades SiteFactory

Un upgrade utilise une branche dédiée.

Exemple :

```text
upgrade/sitefactory-0.5.0
```

---

## 77.1 Working tree

Le repository doit être propre avant application.

---

## 77.2 Commit d’upgrade

L’upgrade produit un ou plusieurs commits explicites uniquement après validation.

---

## 77.3 Push

L’upgrade ne pousse pas automatiquement la branche distante dans le MVP.

---

## 77.4 Pull request

La création d’une pull request est une opération distante distincte.

---

# 78. Customizations client

Les customizations sont committées dans le repository client.

---

## 78.1 Premier commit

Le premier commit contient uniquement les exemples ou structures initiales générées.

---

## 78.2 Code ultérieur

Le code spécifique réel est ajouté dans des commits séparés afin de conserver une baseline identifiable.

---

# 79. Migrations

Les migrations sont versionnées dans Git.

---

## 79.1 Production

Le fait qu’une migration soit poussée ne l’applique pas en production.

---

## 79.2 Revue

Les migrations doivent rester inspectables dans la pull request correspondante.

---

# 80. Fichiers sensibles dans l’historique

Une valeur supprimée dans un commit ultérieur reste dans l’historique.

---

## 80.1 Réponse

Un secret committé doit être :

- révoqué ;
- remplacé ;
- retiré de l’historique si nécessaire ;
- rescanné.

---

## 80.2 Repository non publié

Même si le repository est encore local, la valeur est considérée comme exposée au périmètre des personnes ou outils ayant eu accès au poste.

---

# 81. Réécriture d’historique

Une réécriture peut être utilisée pour nettoyer un secret avant publication.

---

## 81.1 Outil

L’outil et la procédure doivent être approuvés.

---

## 81.2 Backup

Une copie sécurisée peut être conservée temporairement pour éviter une perte de travail.

---

## 81.3 Révocation

La réécriture ne remplace jamais la révocation.

---

# 82. Transfert client

Le repository local ou distant peut être transféré au client.

---

## 82.1 Historique

Le transfert inclut l’historique nécessaire à :

- comprendre les évolutions ;
- reconstruire ;
- maintenir ;
- auditer.

---

## 82.2 Remote

Le transfert peut utiliser :

- transfert de propriété du repository ;
- nouveau repository client ;
- miroir ;
- bundle Git ;
- archive avec historique.

---

## 82.3 Credentials

Les credentials LogicGate ne sont jamais transférés avec le repository.

---

## 82.4 Nouveau remote

Lors d’un transfert vers un nouveau remote :

- vérifier la cible ;
- vérifier la visibilité ;
- pousser sans force si possible ;
- recréer les secrets ;
- vérifier la CI ;
- retirer les accès LogicGate selon le contrat.

---

# 83. Git bundle

Un bundle Git peut être utilisé comme mécanisme de transfert ou d’archive.

---

## 83.1 Vérification

Le bundle doit être :

- créé depuis les références attendues ;
- vérifié ;
- accompagné d’un checksum ;
- stocké de manière sécurisée.

---

## 83.2 Secrets

Le bundle contient l’historique complet.

Il doit donc être traité comme sensible même si le working tree actuel ne contient aucun secret.

---

# 84. Clôture de service

Lors de la clôture :

- effectuer un dernier push si prévu ;
- vérifier la propriété du repository ;
- remettre les informations de build ;
- transférer ou révoquer les accès ;
- archiver selon le contrat ;
- supprimer les credentials LogicGate ;
- documenter l’état final.

---

## 84.1 Suppression distante

La suppression d’un repository distant nécessite une confirmation distincte et n’est jamais déclenchée automatiquement par l’arrêt du service.

---

# 85. Sécurité

## 85.1 Actifs

- code source ;
- historique ;
- customizations ;
- migrations ;
- configuration ;
- propriété intellectuelle ;
- credentials ;
- repository distant ;
- organisation Git.

---

## 85.2 Menaces

- repository public ;
- mauvais owner ;
- mauvais remote ;
- secret dans l’historique ;
- hook malveillant ;
- template Git global ;
- filter Git exécutable ;
- credential dans l’URL ;
- force push ;
- repository distant non vide ;
- workflow déclenché involontairement.

---

## 85.3 Contrôles

- bootstrap local ;
- aucun remote ;
- hooks isolés ;
- template contrôlé ;
- secret scan ;
- working tree propre ;
- visibilité explicite ;
- remote vide ;
- URL validée ;
- aucun force push ;
- confirmations ;
- rapports ;
- permissions minimales.

---

# 86. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Repository public par erreur | visibilité explicite et confirmation renforcée |
| Push vers la mauvaise organisation | affichage et confirmation du propriétaire |
| Secret dans le premier commit | scan avant commit |
| Secret ajouté après génération | scan avant publication |
| Hook local malveillant | hooks désactivés pendant le bootstrap |
| Template Git injectant des fichiers | template contrôlé |
| Package ou filtre exécuté pendant `git add` | attributs contrôlés |
| Repository distant déjà initialisé | remote vide obligatoire |
| Historique écrasé | aucun force push |
| Token dans l’URL | validation et refus |
| Projet créé dans un autre repository | détection des parents Git |
| Workflow distant exécuté prématurément | avertissement avant premier push |

---

# 87. Safe directory Git

Git peut refuser un repository dont le propriétaire filesystem est inattendu.

---

## 87.1 Baseline

SiteFactory ne doit jamais ajouter automatiquement :

```text
safe.directory=*
```

---

## 87.2 Incohérence

Une incohérence de propriété doit être corrigée au niveau :

- utilisateur ;
- permissions ;
- filesystem ;
- environnement de build.

---

## 87.3 Exception ciblée

Une configuration `safe.directory` ciblée peut être ajoutée explicitement par l’opérateur lorsque le contexte est compris.

---

# 88. Git config

SiteFactory ne modifie pas automatiquement :

```text
user.name global

user.email global

credential.helper global

core.autocrlf global

safe.directory global

init.defaultBranch global

core.hooksPath global
```

---

## 88.1 Configuration locale

Seules les valeurs nécessaires et explicitement fournies peuvent être enregistrées localement.

---

# 89. Credential helper

Le credential helper appartient à l’environnement utilisateur ou opérateur.

---

## 89.1 Secret

SiteFactory ne lit ni n’exporte les credentials stockés par le helper.

---

## 89.2 CI

La CI utilise des credentials éphémères injectés par son environnement.

---

# 90. Tests unitaires

Tester notamment :

- validation du nom de branche ;
- validation du remote name ;
- validation de l’URL ;
- détection de credentials intégrés ;
- classification de visibilité ;
- préconditions ;
- mapping des erreurs Git ;
- contenu du rapport.

---

# 91. Tests d’intégration du bootstrap

Dans un répertoire temporaire :

```text
générer
→ initialiser
→ committer
→ vérifier
```

Le test doit prouver :

```text
branche main

un premier commit

working tree propre

aucun remote

fichiers obligatoires suivis

secrets ignorés

état de génération suivi
```

---

# 92. Tests des hooks

Configurer un hook Git global ou `core.hooksPath` de test tentant :

- d’écrire un fichier ;
- d’échouer ;
- d’effectuer une action observable.

Le bootstrap ne doit pas l’exécuter.

---

# 93. Tests du template Git

Configurer un template Git utilisateur contenant un fichier ou hook.

Le repository SiteFactory ne doit pas recevoir ce contenu.

---

# 94. Tests des attributs Git

Configurer un attribut global avec un filtre de test.

Le bootstrap ne doit pas exécuter un filtre global non approuvé.

---

# 95. Tests d’identité

Tester :

- identité globale valide ;
- identité fournie explicitement ;
- identité absente ;
- email invalide selon le contrat ;
- configuration locale ;
- absence de modification globale.

---

# 96. Tests de secret scanning

Créer un fichier contenant un marqueur sensible.

Le premier commit doit être bloqué.

---

## 96.1 Fichier ignoré

Un secret local ignoré ne doit pas être ajouté.

Le scan peut néanmoins avertir de sa présence selon la politique retenue.

---

# 97. Tests de repository imbriqué

Tester une génération :

- dans un repository parent ;
- dans un sous-dossier de worktree ;
- dans un dossier contenant `.git` ;
- dans un dossier non vide.

Les cas non supportés doivent être refusés.

---

# 98. Tests de publication distante

Utiliser un serveur Git ou adaptateur de test.

Tester :

```text
remote vide

remote non vide

remote absent

credential absent

URL invalide

owner incorrect

visibilité absente

publication publique non confirmée

push réussi

push rejeté

remote déjà configuré
```

---

# 99. Tests d’absence de force push

Les commandes générées ou invoquées ne doivent jamais contenir :

```text
--force
--force-with-lease
```

ou une refspec forcée.

---

# 100. Tests de publication partielle

Simuler :

- création distante réussie puis push échoué ;
- push réussi puis protection échouée ;
- remote ajouté puis réseau indisponible.

Le rapport doit refléter l’état réel sans supprimer automatiquement la ressource distante.

---

# 101. Tests multiplateformes

Les scénarios essentiels s’exécutent sur :

```text
Windows x64

Linux x64
```

Ils vérifient :

- Git disponible ;
- fins de ligne ;
- paths ;
- promotion du repository ;
- hooks ;
- identité ;
- commit ;
- absence de remote.

---

# 102. Quality gates du bootstrap

La génération est bloquée lorsque :

```text
Git absent

Version Git non supportée

Identité Git absente

Repository parent détecté

Repository cible existant

Secret détecté

Fichier interdit indexé

Hook non isolé

Template Git non maîtrisé

Premier commit échoué

Working tree non propre

Remote présent après génération

Rapport impossible
```

---

# 103. Quality gates de publication

La publication est bloquée lorsque :

```text
Working tree non propre

Commit absent

Build échoué

Tests échoués

Secret détecté

Visibilité non choisie

Publication publique non confirmée

Owner ambigu

URL contenant un credential

Remote non vide

Historique incompatible

Force push nécessaire

Credential absent

Destination non approuvée
```

---

# 104. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-GIT-001` | Git non installé |
| `SF-GIT-002` | Version Git non supportée |
| `SF-GIT-003` | Identité Git absente |
| `SF-GIT-004` | Nom d’auteur invalide |
| `SF-GIT-005` | Email d’auteur invalide |
| `SF-GIT-006` | Dossier cible dans un repository existant |
| `SF-GIT-007` | Repository cible déjà présent |
| `SF-GIT-008` | Dossier cible non vide |
| `SF-GIT-009` | Initialisation Git échouée |
| `SF-GIT-010` | Création de la branche `main` échouée |
| `SF-GIT-011` | Indexation Git échouée |
| `SF-GIT-012` | Fichier interdit indexé |
| `SF-GIT-013` | Secret détecté avant commit |
| `SF-GIT-014` | Premier commit échoué |
| `SF-GIT-015` | Signature de commit échouée |
| `SF-GIT-016` | Working tree non propre après commit |
| `SF-GIT-017` | Remote inattendu après génération |
| `SF-GIT-018` | Hook Git non isolé |
| `SF-GIT-019` | Template Git non contrôlé |
| `SF-GIT-020` | Attribut Git externe non contrôlé |
| `SF-GIT-021` | Promotion du repository échouée |
| `SF-GIT-022` | Rapport Git local non produit |
| `SF-GIT-PUBLISH-001` | Fournisseur inconnu |
| `SF-GIT-PUBLISH-002` | Propriétaire distant absent |
| `SF-GIT-PUBLISH-003` | Nom de repository invalide |
| `SF-GIT-PUBLISH-004` | Visibilité absente |
| `SF-GIT-PUBLISH-005` | Publication publique non confirmée |
| `SF-GIT-PUBLISH-006` | Credential de publication absent |
| `SF-GIT-PUBLISH-007` | URL distante invalide |
| `SF-GIT-PUBLISH-008` | Credential intégré à l’URL |
| `SF-GIT-PUBLISH-009` | Remote local déjà présent |
| `SF-GIT-PUBLISH-010` | Repository distant déjà existant |
| `SF-GIT-PUBLISH-011` | Repository distant non vide |
| `SF-GIT-PUBLISH-012` | Historique distant incompatible |
| `SF-GIT-PUBLISH-013` | Création distante échouée |
| `SF-GIT-PUBLISH-014` | Ajout du remote échoué |
| `SF-GIT-PUBLISH-015` | Push rejeté |
| `SF-GIT-PUBLISH-016` | Force push requis et interdit |
| `SF-GIT-PUBLISH-017` | Branche par défaut inattendue |
| `SF-GIT-PUBLISH-018` | Protection de branche non appliquée |
| `SF-GIT-PUBLISH-019` | Secret détecté avant publication |
| `SF-GIT-PUBLISH-020` | Publication partielle |
| `SF-GIT-PUBLISH-021` | Rapport de publication absent |
| `SF-GIT-PUBLISH-022` | Hôte distant non approuvé |
| `SF-GIT-PUBLISH-023` | Compte authentifié inattendu |
| `SF-GIT-PUBLISH-024` | Working tree non propre |
| `SF-GIT-PUBLISH-025` | Quality gates locaux échoués |

---

# 105. Observabilité

Événements recommandés :

```text
GitBootstrapStarted

GitRepositoryInitialized

GitInitialBranchCreated

GitIndexValidated

GitInitialCommitCreated

GitBootstrapCompleted

GitBootstrapFailed

RemotePublishPlanCreated

RemoteRepositoryCreated

GitRemoteConfigured

GitPushStarted

GitPushCompleted

RemotePublishCompleted

RemotePublishPartiallyCompleted

RemotePublishFailed
```

---

## 105.1 Propriétés autorisées

```text
GitOperationId

Branch

CommitSha

Provider

RemoteHost

RemoteOwner

RepositoryName

Visibility

RemoteName

Result

Duration

ErrorCode
```

---

## 105.2 Données interdites

```text
Token

Credential

SSH private key

Password

Remote URL with credentials

File content

Client secret
```

---

# 106. Audit

Les opérations suivantes doivent être auditables :

- création du repository distant ;
- choix de la visibilité ;
- publication publique ;
- ajout ou modification d’un remote ;
- premier push ;
- transfert de propriété ;
- suppression distante ;
- modification des protections ;
- ajout d’un accès opérateur.

---

## 106.1 Bootstrap local

Le premier commit fournit la trace locale de la génération.

---

## 106.2 Distant

Le fournisseur distant conserve également son propre journal d’audit selon ses capacités.

---

# 107. Règles pour LogicGate

LogicGate doit :

- vérifier l’identité Git ;
- inspecter le premier commit ;
- conserver le working tree propre ;
- choisir explicitement le remote ;
- choisir explicitement la visibilité ;
- scanner avant publication ;
- vérifier la CI après le premier push ;
- protéger la branche ;
- préparer le transfert client.

---

## 107.1 LogicGate ne doit pas

- publier par simple effet de génération ;
- pousser vers un repository préexistant sans inspection ;
- utiliser un force push pour résoudre une divergence ;
- intégrer un token dans l’URL ;
- publier publiquement sans revue juridique et sécurité ;
- utiliser un repository partagé entre plusieurs clients.

---

# 108. Règles pour Codex

Codex peut :

- créer les fichiers Git de baseline ;
- ajouter `.gitignore` ;
- ajouter `.gitattributes` ;
- préparer les commandes documentées ;
- créer les tests ;
- analyser un état Git ;
- préparer un plan de publication.

---

## 108.1 Codex ne doit pas

```text
Créer un repository distant sans demande explicite

Ajouter un remote sans demande explicite

Pousser une branche sans demande explicite

Créer un tag

Créer une pull request

Effectuer un force push

Réécrire l’historique distant

Ajouter un token

Publier un repository public

Modifier la configuration Git globale

Installer un hook Git

Ajouter safe.directory=*

Supprimer un repository distant
```

---

## 108.2 Commande distante

Une demande d’opération distante doit identifier :

- fournisseur ;
- propriétaire ;
- repository ;
- visibilité ;
- branche ;
- effet attendu.

---

# 109. Anti-patterns interdits

## 109.1 `generate --push`

Une option de génération qui publie implicitement le projet.

---

## 109.2 Remote placeholder

Ajouter une URL fictive comme `origin`.

---

## 109.3 Token dans l’URL

```text
https://token@github.com/...
```

---

## 109.4 Repository distant initialisé

Créer un README distant avant le premier push local.

---

## 109.5 Force push de bootstrap

Utiliser `--force` pour écraser l’historique distant.

---

## 109.6 Repository imbriqué

Créer un projet client sous le repository SiteFactory.

---

## 109.7 Hook automatique

Installer un hook local exécutable pendant la génération.

---

## 109.8 Modification globale

Changer `user.email` ou `init.defaultBranch` globalement.

---

## 109.9 Secret puis nettoyage

Créer le premier commit avec un secret puis le supprimer dans un second commit.

---

## 109.10 Public par défaut

Créer un repository public lorsque la visibilité n’a pas été fournie.

---

## 109.11 Auto-déploiement

Le premier push déclenche une production sans confirmation séparée.

---

# 110. Conséquences positives

La décision apporte :

- baseline Git immédiate ;
- historique propre ;
- absence de publication implicite ;
- indépendance de GitHub ;
- support des upgrades ;
- secret scan avant historique ;
- choix explicite de visibilité ;
- protection des remotes existants ;
- absence de force push ;
- transfert client facilité ;
- séparation entre génération, publication et déploiement ;
- meilleure traçabilité.

---

# 111. Conséquences négatives

La décision entraîne :

- dépendance locale à Git ;
- identité obligatoire ;
- pipeline de génération plus long ;
- tests supplémentaires ;
- publication en plusieurs étapes ;
- gestion des erreurs partielles distantes ;
- contrôles de hooks et templates ;
- procédure de création distante ;
- configuration ultérieure de la CI et des secrets.

---

# 112. Risques acceptés

## 112.1 Repository uniquement local

Avant publication, le repository peut être perdu avec le poste.

Réduction :

- publication rapide après revue ;
- backup du poste ;
- transfert sécurisé.

---

## 112.2 Identité incorrecte

Un opérateur peut utiliser une identité Git non souhaitée.

Réduction :

- préflight ;
- affichage ;
- correction avant publication.

---

## 112.3 Publication partielle

Une ressource distante peut être créée sans push réussi.

Réduction :

- rapport ;
- reprise ;
- absence de suppression automatique.

---

## 112.4 Premier push déclenchant la CI

Le premier push peut démarrer les workflows.

Ce comportement est accepté après avertissement explicite.

---

# 113. Mesures compensatoires

- génération temporaire ;
- validation ;
- hooks isolés ;
- template Git contrôlé ;
- `.gitignore` ;
- `.gitattributes` ;
- secret scan ;
- premier commit ;
- aucun remote ;
- visibilité explicite ;
- remote vide ;
- aucun force push ;
- rapports ;
- branch protection ;
- quality gates ;
- handover documenté.

---

# 114. Performance

L’initialisation Git et le premier commit doivent rester rapides pour les tailles prévues.

---

## 114.1 Gros repository

Une durée excessive peut révéler :

- fichiers volumineux ;
- caches ajoutés ;
- assets inutiles ;
- secret scanning trop large ;
- génération incorrecte.

---

## 114.2 Mesure

Les durées suivantes peuvent être mesurées :

- `git init` ;
- scan ;
- `git add` ;
- commit ;
- publication ;
- push.

---

# 115. Coûts

## 115.1 Coûts supplémentaires

- intégration Git ;
- tests ;
- publication contrôlée ;
- audit ;
- gestion des credentials ;
- configuration distante.

---

## 115.2 Coûts évités

- projets sans historique ;
- publication accidentelle ;
- secrets exposés ;
- mauvais repository ;
- historique divergent ;
- perte de baseline ;
- force push ;
- transfert client difficile.

---

# 116. Maintenabilité

Pour chaque repository, il doit être possible de répondre :

```text
Quel générateur l’a créé ?

Quelle configuration a été utilisée ?

Quel est le premier commit ?

Quelle est la branche principale ?

Quel remote est configuré ?

Qui possède le repository distant ?

Quelle est sa visibilité ?

Quels quality gates sont actifs ?

Comment le transférer ?
```

---

# 117. Compatibilité

## 117.1 Git

SiteFactory maintient une version minimale supportée.

---

## 117.2 Windows et Linux

Les commandes ne doivent pas dépendre d’un shell unique lorsqu’elles sont exécutées depuis le CLI .NET.

---

## 117.3 Fournisseur distant

La génération locale reste indépendante du fournisseur.

---

## 117.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

L’adoption d’un repository existant nécessite un workflow distinct.

---

# 118. Migration d’un projet sans Git

Le processus est :

```text
inventorier les fichiers
→ vérifier les secrets
→ ajouter .gitignore
→ ajouter .gitattributes
→ valider le projet
→ initialiser main
→ inspecter l’index
→ créer un commit d’adoption
→ produire un rapport
```

---

## 118.1 Baseline inconnue

Le commit d’adoption ne doit pas être présenté comme le commit généré original si le projet a déjà évolué.

---

# 119. Migration d’un repository avec remote implicite

Le processus est :

```text
inspecter origin
→ identifier le propriétaire
→ vérifier la visibilité
→ vérifier l’historique
→ retirer ou conserver après décision
→ documenter
```

---

## 119.1 Suppression de remote

Retirer un remote local ne supprime pas le repository distant.

---

## 119.2 Production

```text
Production execution: out of scope
```

Cet ADR ne déclenche aucune publication ou modification réelle de repository distant.

---

# 120. Réversibilité

## 120.1 Classification

```text
élevée avant publication

moyenne après publication privée

faible après publication publique
```

---

## 120.2 Repository local

Le dossier `.git` peut être retiré et recréé avant publication, mais cette opération détruit l’historique local.

Elle n’est pas effectuée automatiquement.

---

## 120.3 Remote

Un remote peut être modifié explicitement sans changer l’historique local.

---

# 121. Plan d’implémentation

## Étape 1 — Préflight Git

Ajouter :

- détection de Git ;
- version ;
- identité ;
- repository parent ;
- dossier cible.

---

## Étape 2 — Initialisation isolée

Ajouter :

- branche `main` ;
- template contrôlé ;
- hooks désactivés ;
- attributs globaux maîtrisés.

---

## Étape 3 — Fichiers Git

Générer :

```text
.gitignore
.gitattributes
```

---

## Étape 4 — Validation de l’index

Ajouter :

- inspection ;
- secret scan ;
- fichiers obligatoires ;
- fichiers interdits ;
- taille.

---

## Étape 5 — Premier commit

Créer le commit et enregistrer ses métadonnées.

---

## Étape 6 — Rapport

Étendre `generation-report.json` avec l’état Git local.

---

## Étape 7 — Commande de statut

Créer une commande conceptuelle :

```text
sitefactory repository status
```

---

## Étape 8 — Plan de publication

Créer :

```text
sitefactory repository publish --plan
```

sans effet de bord.

---

## Étape 9 — Adaptateur GitHub

Ajouter la création explicite d’un repository vide et le premier push.

---

## Étape 10 — Sécurité distante

Ajouter :

- validation d’URL ;
- visibilité ;
- owner ;
- remote vide ;
- aucun force push ;
- rapports.

---

# 122. Tâches liées proposées

```text
SF-TASK-132 — Add Git bootstrap preflight validation

SF-TASK-133 — Generate repository .gitignore and .gitattributes

SF-TASK-134 — Initialize client repositories with the main branch

SF-TASK-135 — Isolate Git templates, hooks and global attributes

SF-TASK-136 — Validate staged files before the initial commit

SF-TASK-137 — Create the initial SiteFactory project commit

SF-TASK-138 — Add Git metadata to the generation report

SF-TASK-139 — Add repository status validation

SF-TASK-140 — Add remote publishing plan generation

SF-TASK-141 — Add explicit GitHub repository publication

SF-TASK-142 — Reject non-empty and incompatible remotes

SF-TASK-143 — Add public repository confirmation safeguards

SF-TASK-144 — Add Git bootstrap and publication security tests
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 123. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Un nouveau projet possède un repository Git local.

La branche initiale est main.

Un premier commit existe.

Le working tree est propre.

Aucun remote n’est configuré.

La génération Git ne nécessite pas de réseau.

Les fichiers obligatoires sont suivis.

Les secrets sont ignorés et non committés.

Un secret détecté bloque le premier commit.

Les hooks globaux ne sont pas exécutés.

Le template Git utilisateur n’est pas injecté.

Les attributs globaux dangereux sont isolés.

Un repository imbriqué est refusé.

Un repository existant est refusé.

L’identité Git n’est pas inventée.

La configuration Git globale n’est pas modifiée.

La publication nécessite une commande séparée.

La visibilité est explicitement choisie.

Une publication publique nécessite une confirmation renforcée.

Un repository distant non vide est refusé.

Aucun force push n’est exécuté.

Une URL contenant un token est refusée.

Une publication partielle produit un rapport exact.

Publier le repository ne déclenche pas directement un déploiement SiteFactory.

Les tests fonctionnent sur Windows et Linux.
```

---

# 124. Conditions de révision

Réexaminer cette décision si :

- SiteFactory devient un service central générant directement dans GitHub ;
- les projets sont créés exclusivement dans des organisations gérées ;
- un monorepo client devient la norme ;
- Git n’est plus le système de versionnement principal ;
- les repositories doivent être signés obligatoirement ;
- Git LFS devient nécessaire pour les assets ;
- la création distante doit être entièrement automatisée par contrat ;
- plusieurs remotes deviennent la baseline ;
- un portail client crée et publie directement les projets ;
- un fournisseur Git impose un autre workflow ;
- la génération locale n’est plus proposée.

---

# 125. Indicateurs à suivre

- générations avec bootstrap Git réussi ;
- échecs d’identité ;
- secrets bloqués avant commit ;
- repositories imbriqués refusés ;
- durées du premier commit ;
- projets publiés ;
- publications partielles ;
- remotes non vides détectés ;
- tentatives de publication publique ;
- push rejetés ;
- protections de branche manquantes ;
- projets sans remote après une durée donnée ;
- transferts de repository ;
- incidents de mauvaise destination.

---

# 126. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des générations créent automatiquement un remote

des repositories publics apparaissent sans validation

des secrets entrent dans le premier commit

des hooks locaux modifient les projets générés

des repositories clients sont imbriqués dans SiteFactory

des force pushes sont utilisés pour publier

des tokens apparaissent dans les URLs de remote

des repositories distants sont initialisés avec un historique divergent

des projets sont publiés vers le mauvais owner

des pushes déclenchent des productions non prévues

des configurations Git globales sont modifiées par le CLI
```

---

# 127. Conséquences observées

Cette section est append-only après mise en œuvre.

## 127.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète
du bootstrap Git local et de la publication distante explicite.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- système d’exploitation ;
- fournisseur Git ;
- étape ;
- résultat ;
- incident ou bénéfice ;
- action proposée.

---

# 128. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `15-cli-specification.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `22-deployment-and-environments.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `27-client-handover-and-service-closure.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0002-yaml-client-configuration.md`
- `0005-file-generation-strategies.md`
- `0008-private-package-feed-and-supply-chain.md`
- `0009-client-customizations-and-extension-points.md`
- `0011-docker-compose-vps-deployment-profile.md`
- `0012-runtime-configuration-and-secrets.md`

---

# 129. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 124 |

---

# 130. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 131. Synthèse de la décision

```text
Génération
→ répertoire temporaire
→ validation
→ secret scan
→ git init local
→ branche main
→ premier commit
→ working tree propre
→ aucun remote
→ promotion finale

Premier commit
→ baseline générée
→ lock file
→ état de génération
→ code
→ tests
→ documentation
→ aucun secret

Sécurité Git locale
→ identité explicite
→ aucune modification globale
→ template contrôlé
→ hooks désactivés
→ attributs maîtrisés
→ repository imbriqué refusé

Publication
→ commande séparée
→ fournisseur explicite
→ owner explicite
→ repository explicite
→ visibilité explicite
→ confirmation
→ remote vide
→ origin
→ push normal de main
→ aucun force push

Publication publique
→ confirmation renforcée
→ revue des licences
→ revue des secrets
→ revue de l’historique

Séparation
→ generate ne publie pas
→ publish ne crée pas de release
→ push ne déploie pas
→ deploy reste une opération distincte
```

> SiteFactory crée immédiatement une historique locale fiable, mais ne prend jamais seul la décision de rendre cette historique distant. La publication du code est une opération explicite, auditable et distincte de la génération comme du déploiement.
