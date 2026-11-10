# LogicGate SiteFactory — Pipeline de génération

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Pipeline de génération |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, architectes, développeurs, contributeurs, Codex |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `13-generation-pipeline.md`, `Contrat public des pages et sections du module concerne : identifiants stables, schemas, validation et strategies de fichiers explicites`, `Contrat public de rendu du module concerne : composants et styles versionnes, encodage, accessibilite et securite du contenu`, `../02-architecture/core-module-boundary.md`, `09-module-architecture.md`, `10-feature-flags-and-entitlements.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Pipeline public effectivement livré

Le point d'entrée public compose Documentation explicitement : configuration V1
→ validation → catalogue/contribution injectés → `GenerationPlan`/`PlannedFile`
→ préflight des destinations et stratégies → écriture atomique **par fichier**
→ lock, rapport et état de génération.
[Contrat exécutable](initial-generation-cli.md), [régénération](regeneration-and-end-to-end-proof.md)
et [repository documentaire par API C#](repository-documentation-end-to-end.md).

`validate` et `plan` n'écrivent rien ; `generate` matérialise la contribution
choisie sans lancer restore/build/test, Git, migration ni déploiement.
Toutes les erreurs déterministes de destination sont détectées avant la première
écriture. Cela ne constitue pas une transaction filesystem globale.
Les cinq stratégies restent celles d'ADR-0005 ; aucun overwrite de customization,
aucun contournement de `protected` ni adoption implicite d'une ancienne sortie.

Les sorties actuelles sont `sitefactory.lock.json`, `generation-report.json`
et `.sitefactory/generation-state.json`. Les autres manifests, zones de merge,
plans d'upgrade et orchestrations décrits ci-dessous sont des **contrats cibles**,
pas des fichiers ou commandes déjà produits.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit le pipeline de génération de LogicGate SiteFactory.

Il précise notamment :

- les entrées nécessaires ;
- les préconditions ;
- les différentes phases ;
- la séparation entre planification et exécution ;
- le mode dry-run ;
- la validation de la configuration ;
- la résolution du catalogue ;
- la résolution des modules ;
- la résolution du template et du thème ;
- la génération des fichiers ;
- la gestion des conflits ;
- les stratégies d’écrasement ;
- la création du lock file ;
- la génération de Docker et de la CI/CD ;
- la préparation des migrations ;
- l’exécution du restore, du build et des tests ;
- la vérification des secrets et placeholders ;
- la création du rapport ;
- l’initialisation Git ;
- les codes d’erreur ;
- les règles de sécurité ;
- les exigences de test ;
- les contraintes applicables à Codex.

Ce document concerne principalement :

```text
sitefactory validate
sitefactory plan
sitefactory generate
```

Les commandes de mise à niveau seront précisées dans une documentation dédiée.

---

## 2. Résumé du pipeline

Le pipeline de référence est :

```text
Configuration client
→ validation syntaxique
→ validation du schéma
→ chargement du catalogue
→ résolution du pack
→ résolution des entitlements
→ résolution des modules
→ résolution du template
→ résolution du thème
→ résolution des pages et sections
→ création du plan
→ analyse du dossier cible
→ détection des conflits
→ génération des fichiers
→ création du lock file
→ génération des artefacts opérationnels
→ préparation du modèle de données
→ restore
→ build Release
→ tests
→ contrôles de sécurité
→ contrôles de production
→ rapport final
→ initialisation Git optionnelle
```

Le pipeline doit être :

- déterministe ;
- reproductible ;
- observable ;
- testable ;
- sécurisé ;
- non destructif par défaut ;
- interrompu dès qu’une précondition critique échoue.

---

## 3. Principes fondamentaux

## 3.1 Planifier avant d’exécuter

Toute génération doit être précédée par un plan explicite.

Le pipeline est séparé en deux phases :

```text
Planification
→ aucune modification du projet cible

Exécution
→ application d’un plan validé
```

Le plan constitue la source de vérité de l’exécution.

Une opération non présente dans le plan ne doit pas être exécutée silencieusement.

---

## 3.2 Refus par défaut

En cas d’incertitude ou d’incohérence, le pipeline doit refuser l’exécution.

Exemples :

- module inconnu ;
- dépendance manquante ;
- version incompatible ;
- chemin de sortie dangereux ;
- conflit de fichier non résolu ;
- secret détecté ;
- configuration invalide ;
- dossier Git sale lors d’un upgrade ;
- placeholder interdit avant production.

Principe :

```text
Fail safe
```

---

## 3.3 Aucune confiance dans les entrées

Toutes les entrées doivent être considérées comme non fiables jusqu’à validation.

Cela concerne notamment :

- YAML client ;
- chemins ;
- identifiants ;
- versions ;
- noms de fichiers ;
- URLs ;
- variables d’environnement ;
- catalogues ;
- manifests ;
- templates ;
- thèmes ;
- paramètres CLI.

---

## 3.4 Une génération incomplète n’est pas un succès

La création de fichiers ne suffit pas.

Une génération est réussie uniquement si les étapes obligatoires sont validées.

Minimum attendu pour le MVP :

```text
Configuration valide
Catalogue valide
Dépendances résolues
Projet généré
Restore réussi
Build Release réussi
Tests réussis
Aucun secret détecté
Rapport produit
```

---

## 3.5 Aucune modification silencieuse des personnalisations

Le pipeline ne doit jamais écraser silencieusement :

- un fichier client ;
- une extension ;
- une configuration protégée ;
- une migration ;
- un secret ;
- un fichier marqué `custom` ;
- un fichier marqué `protected`.

---

## 3.6 Aucune migration automatique de production

Le pipeline peut préparer ou générer une migration.

Il ne doit jamais l’appliquer automatiquement à une base de production dans le cadre d’une génération.

---

# 4. Non-objectifs

Le pipeline MVP ne cherche pas à fournir :

- un déploiement de production automatique complet ;
- une orchestration multi-cloud ;
- un moteur de plugins dynamique ;
- une génération distribuée ;
- un service central de build ;
- un éditeur graphique ;
- une résolution automatique de tous les conflits ;
- une modification automatique des projets clients en production ;
- une publication automatique de packages ;
- une exécution de scripts tiers arbitraires ;
- une création automatique de repository GitHub sans confirmation ;
- une application automatique des migrations de production.

---

# 5. Acteurs

## 5.1 Opérateur LogicGate

L’opérateur :

- prépare la configuration ;
- déclenche les commandes ;
- consulte les plans ;
- valide les avertissements ;
- décide de l’exécution ;
- résout les conflits ;
- valide le résultat ;
- autorise éventuellement la création du repository distant.

---

## 5.2 SiteFactory CLI

Le CLI :

- analyse les arguments ;
- appelle les cas d’usage ;
- affiche les résultats ;
- retourne un code de sortie ;
- demande les confirmations nécessaires.

Il ne contient pas directement la logique métier du pipeline.

---

## 5.3 Moteur de génération

Le moteur :

- construit le plan ;
- applique les opérations ;
- produit les artefacts ;
- exécute les validations ;
- produit le rapport.

---

## 5.4 Codex

Codex peut :

- implémenter une étape ;
- créer des tests ;
- auditer le pipeline ;
- corriger un défaut ;
- améliorer les diagnostics ;
- documenter les comportements.

Codex ne doit pas :

- contourner une validation ;
- modifier le catalogue ;
- modifier les prix ;
- exécuter une opération de production non autorisée ;
- déclarer un succès sans avoir exécuté les validations demandées.

---

# 6. Entrées du pipeline

## 6.1 Configuration client

Entrée principale :

```text
client.sitefactory.yml
```

Elle contient notamment :

- version du schéma ;
- informations non sensibles du client ;
- pack ;
- template ;
- thème ;
- modules ;
- options ;
- pages ;
- sections ;
- profil de déploiement ;
- paramètres non secrets.

---

## 6.2 Catalogue produit

Source cible :

```text
catalog/sitefactory-catalog.yml
```

Il contient notamment :

- packs ;
- modules ;
- prix ;
- dépendances ;
- statuts ;
- entitlements ;
- plans mensuels ;
- compatibilités.

---

## 6.3 Catalogue pages et sections

Source cible :

```text
catalog/pages-and-sections.yml
```

Il contient notamment :

- types de pages ;
- types de sections ;
- variantes ;
- dépendances ;
- comportements vides ;
- règles de validation.

---

## 6.4 Manifests des modules

Chaque module possède :

```text
module.yml
```

---

## 6.5 Manifest du template

Chaque template possède :

```text
template.yml
```

---

## 6.6 Manifest du thème

Chaque thème possède un fichier de description versionné.

Exemple :

```text
theme.yml
```

---

## 6.7 Paramètres CLI

Exemple :

```bash
sitefactory generate \
  --config ./client.sitefactory.yml \
  --output ../client-project
```

Paramètres conceptuels :

```text
--config
--output
--dry-run
--execute
--force
--initialize-git
--report
--output-format
--no-build
--no-tests
```

Les options `--no-build` et `--no-tests` ne doivent pas être autorisées dans les scénarios de validation finale sans avertissement fort.

---

## 6.8 Environnement

Le pipeline peut utiliser :

- version du SDK .NET ;
- système d’exploitation ;
- Git ;
- variables d’environnement non secrètes nécessaires ;
- configuration des sources NuGet ;
- répertoires temporaires.

Il ne doit pas dépendre d’une configuration locale implicite non documentée.

---

# 7. Préconditions

Avant planification, SiteFactory doit vérifier :

```text
[ ] Fichier de configuration présent
[ ] Fichier lisible
[ ] Répertoire de travail valide
[ ] Version du SDK compatible
[ ] Catalogues disponibles
[ ] Manifests lisibles
[ ] Dossier cible autorisé
[ ] Espace disque suffisant lorsque vérifiable
[ ] Git disponible si demandé
[ ] Sources NuGet configurées si nécessaires
```

Une précondition manquante doit produire une erreur actionnable.

---

# 8. Commandes principales

## 8.1 `validate`

```bash
sitefactory validate \
  --config ./client.sitefactory.yml
```

Responsabilités :

- charger la configuration ;
- valider YAML ;
- valider le schéma ;
- charger le catalogue ;
- vérifier les identifiants ;
- résoudre les dépendances ;
- vérifier les compatibilités ;
- produire les erreurs et avertissements.

Cette commande ne modifie aucun fichier.

---

## 8.2 `plan`

```bash
sitefactory plan \
  --config ./client.sitefactory.yml \
  --output ../client-project
```

Responsabilités :

- exécuter toute la validation ;
- résoudre la configuration ;
- analyser le dossier cible ;
- produire les opérations prévues ;
- détecter les conflits ;
- afficher les commandes prévues ;
- produire éventuellement un plan JSON.

Cette commande ne doit pas modifier le dossier cible.

---

## 8.3 `generate`

```bash
sitefactory generate \
  --config ./client.sitefactory.yml \
  --output ../client-project \
  --execute
```

Responsabilités :

- reproduire la validation ;
- reconstruire le plan ;
- vérifier qu’il est exécutable ;
- appliquer les opérations ;
- exécuter les contrôles ;
- produire le rapport.

---

## 8.4 Mode dry-run

```bash
sitefactory generate \
  --config ./client.sitefactory.yml \
  --output ../client-project \
  --dry-run
```

Le dry-run doit être équivalent à `plan`.

Il ne doit pas :

- créer de fichier ;
- modifier Git ;
- exécuter une migration ;
- lancer un build dans le dossier cible ;
- publier un package ;
- lancer Docker ;
- créer un repository distant.

---

# 9. Phase 1 — Chargement de la configuration

## 9.1 Lecture

Le fichier est lu avec :

- encodage maîtrisé ;
- taille maximale raisonnable ;
- gestion claire des erreurs ;
- absence de log du contenu complet.

---

## 9.2 Sécurité YAML

Le parseur YAML doit être configuré de manière sûre.

Il ne doit pas :

- instancier arbitrairement des types ;
- exécuter du code ;
- autoriser des tags dangereux ;
- charger des ressources distantes ;
- résoudre des fichiers externes non prévus.

---

## 9.3 Erreurs

Exemple :

```text
SF-CONFIG-001

Le fichier de configuration ne peut pas être lu.

Fichier :
./client.sitefactory.yml

Correction :
Vérifiez le chemin et les droits de lecture.
```

Le contenu du fichier ne doit pas être affiché entièrement.

---

# 10. Phase 2 — Validation syntaxique

## 10.1 YAML valide

Le parseur vérifie :

- indentation ;
- types ;
- clés ;
- listes ;
- doublons lorsque détectables ;
- valeurs scalaires.

---

## 10.2 Clés inconnues

Stratégie recommandée :

```text
clé inconnue
→ erreur
```

pour les sections structurantes.

Cette règle évite qu’une faute de frappe soit ignorée.

Exemple :

```yaml
moduls:
```

au lieu de :

```yaml
modules:
```

---

## 10.3 Messages

Les erreurs doivent indiquer :

- ligne ;
- colonne ;
- propriété ;
- message ;
- correction possible.

---

# 11. Phase 3 — Validation du schéma

## 11.1 Version de schéma

La configuration contient :

```yaml
schemaVersion: 1
```

Le pipeline doit :

- reconnaître la version ;
- refuser une version inconnue ;
- proposer une migration de configuration si elle existe ;
- ne pas convertir silencieusement une version incompatible.

---

## 11.2 Champs obligatoires

Exemples :

```text
client.name
client.slug
offer.pack
template.id
template.version
theme.id
theme.version
deployment.profile
```

---

## 11.3 Formats

Vérifier notamment :

- slug ;
- domaine ;
- identifiants ;
- versions SemVer ;
- couleurs ;
- routes ;
- codes de devise ;
- URLs ;
- durées ;
- nombres.

---

## 11.4 Secrets interdits

Le validateur doit signaler les propriétés ressemblant à :

```text
password
secret
token
apiKey
privateKey
connectionString
```

lorsqu’elles contiennent une valeur réelle dans une zone non autorisée.

Le moteur ne doit pas afficher la valeur détectée.

---

# 12. Phase 4 — Chargement et validation du catalogue

## 12.1 Catalogue demandé

La configuration peut référencer une version :

```yaml
catalogVersion: 1.0.0
```

---

## 12.2 Vérifications

Le catalogue doit être validé avant utilisation :

- identifiants uniques ;
- versions valides ;
- statuts reconnus ;
- packs parents valides ;
- modules existants ;
- dépendances existantes ;
- absence de cycle ;
- prix valides ;
- services valides ;
- compatibilités valides.

---

## 12.3 Catalogue invalide

Un catalogue invalide bloque toute génération.

Le pipeline ne doit pas tenter de réparer automatiquement un catalogue.

---

# 13. Phase 5 — Résolution du pack

## 13.1 Vérifications

Le pipeline vérifie :

- existence ;
- statut ;
- disponibilité ;
- compatibilité ;
- pack parent ;
- modules inclus ;
- plan mensuel recommandé ;
- version du catalogue.

---

## 13.2 Héritage

Exemple :

```text
sample-application
→ hérite de site-pro
→ ajoute lead-management
→ ajoute portfolio-basic
→ ajoute conversion-toolkit
```

Le résultat doit être aplati et explicite.

---

## 13.3 Projet sur mesure

`custom-project` ne doit pas être résolu comme un pack automatique complet sans configuration technique validée.

---

# 14. Phase 6 — Résolution des entitlements

Le pipeline doit déterminer :

- modules inclus ;
- modules optionnels validés ;
- features autorisées ;
- modules sur devis déjà validés ;
- capacités explicitement désactivées ;
- éléments incompatibles.

Le résultat ne doit pas dépendre du prix transmis dans la configuration.

---

# 15. Phase 7 — Résolution des modules

## 15.1 Étapes

```text
1. Charger les modules demandés
2. Ajouter les modules hérités
3. Ajouter les dépendances requises
4. Vérifier les versions
5. Détecter les cycles
6. Détecter les incompatibilités
7. Vérifier les statuts
8. Vérifier les manifests
9. Produire l’ordre de composition
```

---

## 15.2 Module inconnu

Un module inconnu produit une erreur.

Il ne doit pas être ignoré.

---

## 15.3 Module planifié

Un module `planned` ou `future` ne peut pas être installé dans un projet réel.

---

## 15.4 Dépendance ajoutée automatiquement

Une dépendance ajoutée automatiquement doit apparaître dans le plan.

Exemple :

```text
Module demandé :
sample-form

Modules ajoutés :
contact-form
lead-management
```

---

# 16. Phase 8 — Résolution du template

Le pipeline vérifie :

- identifiant ;
- version ;
- statut ;
- compatibilité avec le générateur ;
- compatibilité avec le pack ;
- modules requis ;
- modules recommandés ;
- pages ;
- navigation ;
- sections ;
- règles SEO.

Le template MVP est :

```text
sample-application
```

---

# 17. Phase 9 — Résolution du thème

Le pipeline vérifie :

- identifiant ;
- version ;
- statut ;
- compatibilité avec le design system ;
- compatibilité avec le template ;
- présence des tokens obligatoires ;
- validité des couleurs ;
- validité des polices ;
- variantes supportées.

Le thème MVP est :

```text
professional-clean
```

---

# 18. Phase 10 — Résolution des pages et sections

## 18.1 Pages

Le pipeline détermine :

- pages obligatoires ;
- pages optionnelles ;
- routes ;
- navigation ;
- modules requis ;
- indexation ;
- sections.

---

## 18.2 Sections

Pour chaque section :

- type connu ;
- variante connue ;
- propriétés obligatoires ;
- modules requis ;
- comportement vide ;
- compatibilité avec la page ;
- ordre valide.

---

## 18.3 Conditions

Les conditions doivent être déclaratives.

Exemple :

```yaml
enabledWhen:
  modulePresent: portfolio-basic
```

Une condition basée sur un client nommé est interdite.

---

## 18.4 Placeholders

Les placeholders sont autorisés pendant les phases de développement prévues.

Ils doivent être enregistrés dans le plan afin d’être vérifiés avant production.

---

# 19. Phase 11 — Création du modèle résolu

Le pipeline produit un modèle central, par exemple :

```text
ResolvedClientProject
```

Il contient notamment :

- configuration normalisée ;
- pack résolu ;
- entitlements ;
- modules résolus ;
- template ;
- thème ;
- pages ;
- sections ;
- profil de déploiement ;
- versions ;
- avertissements ;
- exigences de secrets ;
- exigences de migration.

Ce modèle ne doit contenir aucun secret réel.

---

# 20. Phase 12 — Création du plan de génération

Coupe implémentée par SF-TASK-015 : `GenerationPlanBuilder` est une fonction
Application sans I/O, alimentée par une configuration V1 validée, un manifest
résolu et un ID de plan fourni explicitement. Elle produit le contrat minimal
de plan (schéma `1`), pas encore le contenu cible complet décrit ci-dessous.
Les IDs de fichiers proviennent du manifest de schéma `2` et ne sont jamais
calculés depuis les chemins ou l'ordre d'entrée. Les actions initiales `Create`
restent soumises à une future vérification d'absence de cible ; `custom` et
`protected` restent `Preserve`. Le `merge` sans handler déclaré est bloqué par
`SF-GEN-FILE-006`. Aucun fichier, lock ou rapport n'est écrit, aucune commande
n'est exécutée et aucun état de repository cible n'est inspecté.

SF-TASK-016 ajoute avant construction des intentions un contrôle des cibles
logiques : doublons, collisions de casse (fichiers et répertoires implicites),
conflits fichier/répertoire et noms réservés Windows. Une erreur retourne un
plan sans fichiers avec diagnostics stables, sans inspection du dossier cible.
Les chemins non canoniques restent rejetés par les contrats, sans résolution
physique ni normalisation implicite. Les liens et contraintes du filesystem
réel restent à contrôler avant toute future exécution.

SF-TASK-017 expose ce contrat minimal sous forme de JSON en mémoire, via
`GenerationPlanJsonSerializer` dans Infrastructure. Le format de schéma `1`
est [documenté dans README](initial-generation-cli.md)
et vérifié par un golden file du pipeline réel. Il conserve l'ordre du plan et
son ID explicite, utilise UTF-8 sans BOM et LF, sans date ni donnée d'environnement.
Il ne représente pas encore toutes les informations cibles de la section 20.1,
ne valide pas une autorisation d'exécution et n'écrit aucun fichier client.

## 20.1 Contenu du plan

Le plan doit contenir :

```text
GenerationId
ClientSlug
OutputDirectory
GeneratorVersion
CatalogVersion
Template
Theme
Modules
Features
FileOperations
Commands
Warnings
Conflicts
RequiredManualActions
```

---

## 20.2 Opérations de fichiers

Chaque opération contient :

```text
TargetPath
Source
Strategy
Action
Reason
ExpectedHash
CurrentHash éventuel
```

---

## 20.3 Commandes prévues

Exemples :

```text
dotnet restore
dotnet build
dotnet test
git init
```

Les commandes doivent être déclarées avant exécution.

---

## 20.4 Actions manuelles

Exemples :

```text
Créer les secrets de production
Configurer le domaine
Valider les contenus
Créer la première migration
Créer le repository distant
```

---

# 21. Stratégies de fichiers

## 21.1 `generated`

Le fichier est entièrement géré par SiteFactory.

Il peut être remplacé si :

- la stratégie l’autorise ;
- aucun conflit non prévu n’existe ;
- le changement apparaît dans le plan.

Exemples :

```text
sitefactory.lock.json
generation-report.json
GeneratedModuleComposition.cs
```

---

## 21.2 `create-once`

Le fichier est créé uniquement s’il n’existe pas.

S’il existe, il est conservé.

Exemples :

```text
README.md client
fichier de personnalisation initial
```

---

## 21.3 `merge`

Le fichier peut être mis à jour par une stratégie de fusion contrôlée.

Exemples potentiels :

```text
Directory.Packages.props
configuration de services
navigation générée
```

Une fusion doit être déterministe et testée.

---

## 21.4 `custom`

Le fichier appartient au projet client.

SiteFactory ne le modifie pas automatiquement.

---

## 21.5 `protected`

Le fichier ne peut pas être écrasé automatiquement.

Exemples :

```text
client.sitefactory.yml
migrations client existantes
fichiers de secrets
extensions client
```

---

# 22. Phase 13 — Analyse du dossier cible

## 22.1 Nouveau dossier

Si le dossier n’existe pas :

- vérifier le parent ;
- vérifier les droits ;
- vérifier le chemin ;
- créer le dossier uniquement pendant l’exécution.

---

## 22.2 Dossier vide

Un dossier vide peut être utilisé après validation.

---

## 22.3 Dossier non vide

Le pipeline doit :

- lister les fichiers concernés ;
- détecter un repository Git ;
- calculer les conflits ;
- refuser l’écrasement non contrôlé ;
- produire un plan détaillé.

---

## 22.4 Dossier Git sale

Pour une génération initiale, un dossier Git sale peut être refusé selon le contexte.

Pour une mise à niveau, il doit être refusé par défaut.

---

# 23. Sécurité des chemins

## 23.1 Path traversal

Le pipeline doit empêcher :

```text
../
../../
```

ou toute écriture hors du dossier autorisé.

---

## 23.2 Chemins absolus

Un chemin absolu peut être accepté uniquement s’il est explicitement fourni et validé.

---

## 23.3 Liens symboliques

Les liens symboliques doivent être détectés lorsque leur suivi permettrait d’écrire hors du dossier cible.

La stratégie par défaut est de refuser les liens symboliques dangereux.

---

## 23.4 Dossiers interdits

Le pipeline ne doit pas cibler directement :

```text
/
C:\
/etc
/var/lib
home utilisateur complet
repository SiteFactory lui-même
```

sans confirmation et cas explicitement supporté.

---

# 24. Phase 14 — Détection des conflits

## 24.1 Types de conflits

```text
Fichier protégé existant
Fichier custom ciblé
Hash inattendu
Modification locale d’un fichier generated
Fusion impossible
Route dupliquée
Package incompatible
Migration existante contradictoire
```

---

## 24.2 Résolution

Le pipeline peut :

- arrêter ;
- ignorer selon stratégie ;
- conserver le fichier ;
- produire un fichier `.generated` séparé ;
- produire un diff ;
- demander une action humaine.

---

## 24.3 Résolution automatique limitée

Le moteur ne doit pas inventer une fusion lorsque le contrat n’est pas explicite.

---

# 25. Phase 15 — Préparation d’un espace temporaire

Pour une génération initiale, le pipeline devrait utiliser un dossier temporaire :

```text
output/.sitefactory-temp-{generationId}
```

ou un dossier temporaire système contrôlé.

Le résultat est déplacé vers la destination finale uniquement après les validations prévues.

Cette stratégie réduit le risque de laisser un projet partiellement généré.

---

# 26. Phase 16 — Génération des fichiers

## 26.1 Ordre conceptuel

```text
1. Structure de solution
2. Fichiers racine
3. Projets .NET
4. Configuration commune
5. Host Web
6. Projet Data
7. Projet Customizations
8. Tests
9. Composition des modules
10. Pages et navigation
11. Thème
12. Docker
13. CI/CD
14. Scripts
15. Documentation
16. Lock file
```

---

## 26.2 Encodage

Les fichiers texte utilisent :

```text
UTF-8
```

La présence ou non d’un BOM doit être cohérente avec les conventions du repository.

---

## 26.3 Fins de ligne

Le repository doit définir les conventions via :

```text
.gitattributes
.editorconfig
```

---

## 26.4 Ordre stable

Lorsque des collections sont générées, leur ordre doit être déterministe.

Exemples :

- modules triés ;
- permissions triées ;
- packages triés ;
- fichiers triés ;
- propriétés JSON stables.

---

# 27. Génération de la solution .NET

## 27.1 Format

La solution cible utilise :

```text
.slnx
```

si ce format est retenu par la version .NET du projet.

---

## 27.2 Projets initiaux

```text
ClientName.Web
ClientName.Data
ClientName.Customizations
ClientName.Tests
ClientName.IntegrationTests
```

---

## 27.3 Nommage

Le nom de solution et les namespaces sont dérivés d’un identifiant validé.

Le nom commercial ne doit pas être injecté directement sans normalisation.

---

## 27.4 Fichiers communs

Le pipeline génère notamment :

```text
Directory.Build.props
Directory.Packages.props
global.json
.editorconfig
.gitattributes
.gitignore
README.md
AGENTS.md
```

---

# 28. Génération des références de packages

## 28.1 Versions verrouillées

Les versions SiteFactory sont explicites.

Les versions flottantes sont interdites.

---

## 28.2 Central Package Management

Les versions sont centralisées dans :

```text
Directory.Packages.props
```

---

## 28.3 Sources NuGet

Les sources privées nécessaires doivent être documentées.

Les credentials ne doivent pas être générés dans le repository.

---

# 29. Génération de la composition des modules

Le pipeline produit une composition explicite.

Exemple conceptuel :

```csharp
public static class GeneratedModuleComposition
{
    public static IServiceCollection AddGeneratedModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSiteFactoryPlatform(configuration);
        services.AddIdentityModule(configuration);
        services.AddContentModule(configuration);
        services.AddContactModule(configuration);
        services.AddEmailModule(configuration);
        services.AddLeadsModule(configuration);
        services.AddPortfolioModule(configuration);
        services.AddSeoModule(configuration);
        services.AddLegalModule(configuration);

        return services;
    }
}
```

Un module absent du plan ne doit pas être enregistré.

---

# 30. Génération du modèle de données

## 30.1 Composition EF Core

Le pipeline génère la composition du modèle selon les modules installés.

---

## 30.2 Migrations

Pour la génération initiale, deux stratégies sont possibles :

### Stratégie A — Migration initiale générée automatiquement

SiteFactory crée la migration initiale après génération du `DbContext`.

### Stratégie B — Migration initiale préparée puis créée explicitement

SiteFactory génère le contexte et fournit la commande de migration.

Pour le MVP, la stratégie doit être choisie explicitement avant implémentation.

Aucune migration ne doit être appliquée à une production par ce pipeline.

---

## 30.3 Inspection

Si une migration est générée, elle doit être incluse dans les contrôles et le rapport.

---

# 31. Génération des contenus initiaux

Les contenus initiaux peuvent provenir :

- du template ;
- de la configuration ;
- de fichiers de contenu ;
- de placeholders ;
- d’une assistance éditoriale.

Ils doivent être classés :

```text
validated
draft
placeholder
demo
```

Le pipeline doit empêcher une validation de production avec des contenus `placeholder` ou `demo` non autorisés.

---

# 32. Génération du thème

Le pipeline doit :

- charger les tokens ;
- appliquer les overrides ;
- valider les formats ;
- vérifier les contrastes lorsque possible ;
- générer les fichiers de thème ;
- conserver les personnalisations dans un espace isolé.

Il ne doit pas produire de CSS spécifique au nom d’un client dans le package générique.

---

# 33. Génération de Docker

## 33.1 Dockerfile

Le pipeline génère un Dockerfile :

- multi-stage ;
- compatible avec .NET cible ;
- sans secret ;
- basé sur une image runtime ;
- avec utilisateur non privilégié lorsque possible ;
- sans App_Data de production.

---

## 33.2 Docker Compose

Le profil initial génère un exemple contenant :

```text
web
postgres
caddy
```

PostgreSQL ne publie pas son port.

---

## 33.3 Variables

Un fichier :

```text
.env.example
```

contient uniquement :

- noms de variables ;
- placeholders ;
- valeurs non sensibles.

---

## 33.4 Volumes

Le pipeline prépare notamment :

```text
postgres_data
app_data
data_protection_keys
caddy_data
caddy_config
```

Les noms doivent être préfixés par le projet afin d’éviter les collisions.

---

# 34. Génération de la CI/CD

## 34.1 Pipeline de validation

Étapes minimales :

```text
restore
build Release
tests
validation configuration
analyse secrets
analyse dépendances
analyse statique
```

---

## 34.2 Actions GitHub

Les actions doivent être :

- versionnées ;
- idéalement épinglées selon la politique LogicGate ;
- compatibles avec la version Node imposée par GitHub ;
- limitées aux permissions nécessaires.

---

## 34.3 Déploiement

La CI générée ne doit pas déployer automatiquement en production dans le MVP sans configuration explicite.

---

# 35. Génération des scripts d’exploitation

Le pipeline peut générer :

```text
backup-postgres.sh
backup-app-data.sh
backup-all.sh
prune-backups.sh
restore-postgres.sh
check-health.sh
check-disk-space.sh
check-production.sh
```

Ces scripts doivent :

- être sobres ;
- ne pas afficher de secret ;
- ne pas afficher de PII ;
- demander confirmation pour les restaurations ;
- protéger les chemins ;
- être documentés.

---

# 36. Génération de la documentation client

Chaque projet doit recevoir au minimum :

```text
README.md
AGENTS.md
docs/architecture/
docs/deployment/
docs/operations/
docs/security/
```

La documentation doit être adaptée aux modules réellement installés.

Un module absent ne doit pas apparaître comme disponible.

---

# 37. Phase 17 — Création du lock file

## 37.1 Contenu

Le lock file contient :

- version du schéma ;
- version du générateur ;
- version du catalogue ;
- pack ;
- template ;
- thème ;
- modules ;
- features résolues si nécessaire ;
- profil de déploiement ;
- hashes utiles.

---

## 37.2 Écriture

Le lock file est écrit uniquement après résolution réussie.

Lors d’un upgrade, il n’est remplacé qu’après application réussie.

---

## 37.3 Sécurité

Le lock file ne contient :

- ni secret ;
- ni donnée personnelle inutile ;
- ni credentials ;
- ni chemin local sensible.

---

# 38. Phase 18 — Restore des dépendances

Commande de référence :

```bash
dotnet restore ClientName.slnx
```

Le pipeline doit :

- capturer le code de sortie ;
- capturer une sortie limitée ;
- produire une erreur claire ;
- ne pas masquer les warnings ;
- respecter les sources NuGet configurées.

---

## 38.1 Échec

Un restore échoué marque la génération comme échouée.

---

## 38.2 Logs

Le rapport peut contenir :

```text
commande
durée
code de sortie
résumé
```

Il ne doit pas recopier un token de feed privé.

---

# 39. Phase 19 — Build Release

Commande de référence :

```bash
dotnet build ClientName.slnx \
  --configuration Release \
  --no-restore
```

Résultat attendu :

```text
0 warning
0 erreur
```

Un warning connu peut être signalé, mais ne doit pas être ignoré silencieusement.

---

# 40. Phase 20 — Tests

Commande de référence :

```bash
dotnet test ClientName.slnx \
  --configuration Release \
  --no-build
```

Le rapport doit enregistrer :

- nombre total ;
- réussis ;
- échoués ;
- ignorés ;
- durée ;
- projets concernés.

---

## 40.1 Échec de test

Un test échoué marque la génération finale comme échouée.

---

## 40.2 Tests ignorés

Des tests ignorés doivent apparaître dans le rapport.

Ils ne doivent pas être considérés comme exécutés avec succès.

---

# 41. Phase 21 — Validation des catalogues et manifests générés

Le pipeline doit vérifier :

- lock file ;
- configuration client ;
- manifests recopiés ou référencés ;
- composition générée ;
- cohérence entre modules et packages ;
- cohérence entre features et modules ;
- cohérence entre permissions et modules.

---

# 42. Phase 22 — Vérification des placeholders

## 42.1 Valeurs détectées

Exemples :

```text
Lorem ipsum
[Nom de l’entreprise]
[À compléter]
contact@example.test
example.test
TODO CLIENT
DEMO CONTENT
```

---

## 42.2 Modes

### Génération de développement

Les placeholders peuvent produire un warning.

### Validation de production

Les placeholders bloquants produisent une erreur.

---

## 42.3 Dérogations

Une dérogation doit être explicite, localisée et documentée.

---

# 43. Phase 23 — Détection des secrets

Le pipeline doit vérifier les fichiers générés avec les outils ou règles prévues.

Exemples de détection :

- mot de passe ;
- token ;
- clé privée ;
- connection string réelle ;
- secret SMTP ;
- credentials de feed ;
- certificat privé.

Les outils peuvent inclure :

```text
Gitleaks
Secret scanning GitHub
règles internes
```

Le moteur ne doit pas afficher la valeur complète détectée.

---

# 44. Phase 24 — Contrôles de sécurité

Selon le niveau du projet :

```text
analyse statique
analyse des dépendances
validation des routes admin
validation antiforgery
validation absence inscription publique
validation des cookies
validation de la configuration Docker
validation PostgreSQL non public
```

Une erreur bloquante empêche le statut final `SUCCESS`.

---

# 45. Phase 25 — Contrôles de production

Avant de qualifier le projet comme prêt à être déployé, vérifier :

```text
[ ] Aucun placeholder bloquant
[ ] Aucun secret
[ ] Pages légales présentes
[ ] Health check présent
[ ] Docker présent
[ ] PostgreSQL privé
[ ] Data Protection persistée
[ ] AdminSeed désactivé par défaut
[ ] Backups préparés
[ ] Restore documenté
[ ] Migrations contrôlées
[ ] Rollback documenté
```

Le pipeline ne doit pas prétendre que la production est prête lorsque ces contrôles n’ont pas été exécutés.

---

# 46. Phase 26 — Rapport de génération

## 46.1 Fichier

```text
generation-report.json
```

---

## 46.2 Statuts

```text
planned
running
success
success-with-warnings
failed
cancelled
```

---

## 46.3 Structure conceptuelle

```json
{
  "schemaVersion": 1,
  "generationId": "8d7f9911-4d5b-45e6-b850-8bc146eb31a3",
  "status": "success",
  "generatorVersion": "0.1.0",
  "catalogVersion": "1.0.0",
  "clientSlug": "sample-project",
  "template": {
    "id": "sample-application",
    "version": "1.0.0"
  },
  "theme": {
    "id": "professional-clean",
    "version": "1.0.0"
  },
  "modules": [
    {
      "id": "lead-management",
      "version": "1.0.0",
      "source": "pack"
    }
  ],
  "steps": [
    {
      "id": "restore",
      "status": "success",
      "durationMilliseconds": 3200
    }
  ],
  "build": {
    "status": "success",
    "warnings": 0,
    "errors": 0
  },
  "tests": {
    "status": "success",
    "total": 152,
    "passed": 152,
    "failed": 0,
    "skipped": 0
  },
  "warnings": [],
  "errors": []
}
```

---

## 46.4 Données interdites

Le rapport ne contient pas :

- secrets ;
- mots de passe ;
- tokens ;
- connection strings ;
- données de leads ;
- contenus confidentiels ;
- sorties complètes de fichiers sensibles ;
- variables d’environnement complètes.

---

## 46.5 Rapport humain

Le CLI peut également produire :

```text
Generation: SUCCESS
Client: sample-project
Pack: sample-application
Template: sample-application@1.0.0
Theme: professional-clean@1.0.0
Modules: 11
Files created: 84
Files skipped: 3
Warnings: 0
Build: SUCCESS
Tests: 152 passed
Report: ./generation-report.json
```

---

# 47. Phase 27 — Finalisation du dossier

Pour une génération initiale utilisant un dossier temporaire :

1. vérifier le succès ;
2. vérifier l’absence de conflit ;
3. déplacer ou renommer le dossier temporaire ;
4. vérifier la destination ;
5. supprimer les fichiers temporaires inutiles ;
6. conserver le rapport.

En cas d’échec, le dossier temporaire peut être conservé pour diagnostic selon une option explicite.

---

# 48. Phase 28 — Initialisation Git

## 48.1 Option

```bash
--initialize-git
```

---

## 48.2 Étapes

```text
git init
git add .
git commit initial
```

Le commit initial ne doit être créé que si :

- le build réussit ;
- les tests réussissent ;
- aucun secret n’est détecté ;
- le repository est cohérent.

---

## 48.3 Remote

Le remote Git n’est pas créé automatiquement par défaut.

---

## 48.4 Repository distant

Une commande future peut proposer :

```bash
sitefactory repository create
```

Elle nécessitera une confirmation explicite et une revue de sécurité.

---

# 49. Atomicité et rollback local

## 49.1 Génération initiale

L’utilisation d’un dossier temporaire permet un rollback simple :

```text
échec
→ destination finale non remplacée
→ dossier temporaire conservé ou supprimé
```

---

## 49.2 Projet existant

Pour une modification d’un projet existant :

- branche Git dédiée ;
- repository propre ;
- plan ;
- modifications contrôlées ;
- possibilité de `git diff` ;
- rollback par Git.

---

## 49.3 Limites

Git ne remplace pas :

- backup de base ;
- backup de médias ;
- rollback de migration ;
- restauration de secrets.

---

# 50. Reprise après échec

## 50.1 MVP

Le MVP peut recommencer le pipeline depuis le début.

La reprise automatique à une étape intermédiaire n’est pas obligatoire.

---

## 50.2 Idempotence

Les étapes de validation et planification doivent être idempotentes.

Les étapes de création doivent être déterministes et respecter les stratégies de fichiers.

---

## 50.3 Fichiers partiels

Les fichiers partiels ne doivent pas être considérés comme valides.

L’écriture atomique fichier par fichier est recommandée :

```text
écrire dans un fichier temporaire
→ flush
→ remplacer
```

---

# 51. Annulation

Le pipeline doit transmettre un :

```csharp
CancellationToken
```

aux opérations longues :

- lecture ;
- génération ;
- processus externes ;
- restore ;
- build ;
- tests.

Une annulation doit produire :

```text
status = cancelled
```

Elle ne doit pas être présentée comme un échec métier.

---

# 52. Timeouts

Les processus externes doivent posséder des timeouts configurables.

Exemples :

```text
restore
build
tests
git
```

Un timeout doit :

- interrompre le processus ;
- produire une erreur claire ;
- ne pas laisser un processus orphelin ;
- être enregistré dans le rapport.

---

# 53. Exécution de processus externes

## 53.1 Abstraction

Interface conceptuelle :

```csharp
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken);
}
```

---

## 53.2 Sécurité

Le moteur doit utiliser :

- fichier exécutable explicite ;
- arguments structurés ;
- répertoire de travail explicite ;
- environnement limité ;
- capture de sortie ;
- code de sortie.

Il ne doit pas construire une commande shell par concaténation de valeurs non fiables.

---

## 53.3 Secrets

Un secret ne doit pas être passé sur la ligne de commande lorsqu’une variable d’environnement ou un mécanisme plus sûr existe.

---

# 54. Codes d’erreur

Convention :

```text
SF-{DOMAIN}-{NUMBER}
```

Exemples :

| Code | Signification |
|---|---|
| `SF-GEN-001` | Dossier cible invalide |
| `SF-GEN-002` | Plan impossible |
| `SF-GEN-003` | Conflit de fichier |
| `SF-GEN-004` | Écriture échouée |
| `SF-GEN-005` | Chemin non autorisé |
| `SF-GEN-006` | Fichier protégé |
| `SF-GEN-007` | Génération annulée |
| `SF-CONFIG-001` | Configuration illisible |
| `SF-CONFIG-002` | YAML invalide |
| `SF-CONFIG-003` | Schéma invalide |
| `SF-CATALOG-001` | Catalogue invalide |
| `SF-MODULE-001` | Module inconnu |
| `SF-MODULE-006` | Dépendance de module manquante (ADR-0007) |
| `SF-MODULE-008` | Cycle de dépendances de modules détecté (ADR-0007) |
| `SF-TEMPLATE-001` | Template inconnu |
| `SF-TEMPLATE-002` | Produit non supporté par le template |
| `SF-TEMPLATE-003` | Manifest de template invalide |
| `SF-TEMPLATE-004` | Résolution de template ambiguë |
| `SF-THEME-001` | Thème incompatible |
| `SF-BUILD-001` | Restore échoué |
| `SF-BUILD-002` | Build échoué |
| `SF-TEST-001` | Tests échoués |
| `SF-SECURITY-001` | Secret détecté |
| `SF-CONTENT-001` | Placeholder bloquant |

---

# 55. Niveaux d’avertissement

## Information

Exemple :

```text
Une page optionnelle n’est pas activée.
```

## Warning

Exemple :

```text
Le module portfolio-basic est installé mais aucune réalisation
n’est encore publiée.
```

## Error

Exemple :

```text
Le template exige contact-form, mais le module est absent.
```

## Blocking error

Exemple :

```text
Un secret potentiel a été détecté dans un fichier généré.
```

---

# 56. Journalisation

## 56.1 Événements

Exemples :

```text
GenerationStarted
ConfigurationValidated
CatalogResolved
GenerationPlanCreated
FileGenerated
FileConflictDetected
RestoreCompleted
BuildCompleted
TestsCompleted
SecretDetected
GenerationCompleted
GenerationFailed
```

---

## 56.2 Données autorisées

```text
GenerationId
ClientSlug technique
ModuleId
TemplateId
ThemeId
StepId
Duration
FileCount
ErrorCode
```

---

## 56.3 Données interdites

```text
Mot de passe
Token
Connection string
Contenu complet des fichiers
Données personnelles
Contenu de formulaire
Clé privée
```

---

# 57. Sortie console

## 57.1 Mode humain

La sortie doit être concise et structurée.

Exemple :

```text
[1/12] Validating configuration... OK
[2/12] Resolving catalog... OK
[3/12] Resolving modules... OK
[4/12] Planning files... OK
[5/12] Generating project... OK
[6/12] Restoring packages... OK
[7/12] Building Release... OK
[8/12] Running tests... OK
[9/12] Checking placeholders... OK
[10/12] Checking secrets... OK
[11/12] Writing report... OK
[12/12] Initializing Git... OK
```

---

## 57.2 Mode JSON

Une option future ou MVP peut proposer :

```bash
--output-format json
```

Elle doit produire un JSON stable et sans textes décoratifs parasites.

---

# 58. Codes de sortie CLI

Proposition :

| Code | Signification |
|---:|---|
| `0` | Succès |
| `1` | Erreur générale |
| `2` | Arguments invalides |
| `3` | Configuration invalide |
| `4` | Catalogue ou manifest invalide |
| `5` | Plan non exécutable |
| `6` | Conflit de fichiers |
| `7` | Génération échouée |
| `8` | Restore ou build échoué |
| `9` | Tests échoués |
| `10` | Contrôle de sécurité échoué |
| `11` | Environnement invalide |
| `12` | Opération annulée |
| `13` | Confirmation manquante |

Les valeurs exactes doivent être stabilisées dans la spécification CLI.

---

# 59. Option `--force`

## 59.1 Rôle limité

`--force` ne doit pas supprimer toutes les protections.

Il peut autoriser uniquement des comportements documentés, par exemple :

- remplacer un fichier `generated` modifié ;
- réutiliser un dossier non vide dans un scénario supporté.

---

## 59.2 Interdictions

`--force` ne doit pas permettre :

- d’écraser un fichier `protected` ;
- de supprimer un fichier client ;
- de contourner un secret détecté ;
- de contourner un test échoué ;
- de contourner une incompatibilité ;
- de lancer une migration production ;
- d’écrire hors du dossier cible.

---

## 59.3 Compte-rendu

Toute action forcée doit apparaître dans le rapport.

---

# 60. Confirmations

Une confirmation explicite est requise lorsque le pipeline :

- modifie un dossier non vide ;
- remplace un fichier généré modifié ;
- initialise Git dans un dossier existant ;
- supprime un fichier temporaire conservé pour diagnostic ;
- applique une opération potentiellement destructive future.

La génération initiale dans un nouveau dossier vide peut rester non interactive si tous les paramètres sont explicites.

---

# 61. Sécurité du pipeline

## 61.1 Templates de confiance

Le MVP utilise uniquement des templates validés par LogicGate.

---

## 61.2 Pas de scripts arbitraires

Un template ou module ne peut pas déclarer librement :

```text
run: ./script.sh
```

sans contrat et validation.

---

## 61.3 Pas de téléchargement arbitraire

Le pipeline ne télécharge pas :

- template depuis une URL libre ;
- script ;
- archive ;
- package non autorisé ;
- binaire externe.

---

## 61.4 Sources autorisées

Les sources NuGet et artefacts doivent être explicitement configurées.

---

## 61.5 Permissions système

Le pipeline doit s’exécuter avec les permissions minimales nécessaires.

Il ne doit pas exiger un compte administrateur système pour une génération locale normale.

---

# 62. Confidentialité

Le pipeline ne doit pas envoyer automatiquement :

- configuration client ;
- contenus ;
- noms de clients ;
- rapports ;
- code généré ;
- logs ;

vers un service externe.

Toute intégration externe future doit être documentée.

---

# 63. Utilisation de Codex pendant la génération

Codex peut être utilisé pour :

- préparer une configuration ;
- rédiger une tâche ;
- corriger une erreur de build ;
- créer une extension ;
- améliorer les tests ;
- analyser un rapport.

Codex ne doit pas recevoir :

- secrets ;
- fichiers `.env.production` ;
- dumps ;
- données de leads ;
- clés ;
- mots de passe ;
- contenus confidentiels non nécessaires.

---

# 64. Tests du pipeline

## 64.1 Tests de validation

Vérifier :

- YAML valide ;
- YAML invalide ;
- schéma inconnu ;
- clé inconnue ;
- champ obligatoire absent ;
- secret détecté ;
- identifiant invalide ;
- version invalide.

---

## 64.2 Tests de catalogue

Vérifier :

- catalogue valide ;
- pack inconnu ;
- module inconnu ;
- dépendance manquante ;
- cycle ;
- incompatibilité ;
- module planifié ;
- prix invalides.

---

## 64.3 Tests de résolution

Vérifier :

- Site Pro ;
- application métier cible ;
- automatisation métier cible ;
- héritage ;
- modules optionnels ;
- dépendances transitives ;
- entitlements ;
- features.

---

## 64.4 Tests de template

Vérifier :

- template valide ;
- template inconnu ;
- version incompatible ;
- module requis absent ;
- thème incompatible ;
- section inconnue ;
- variante inconnue.

---

## 64.5 Tests du plan

Vérifier :

- ordre stable ;
- fichiers attendus ;
- opérations attendues ;
- commandes attendues ;
- warnings ;
- conflits ;
- aucune modification en dry-run.

---

## 64.6 Tests de fichiers

Vérifier :

- création ;
- create-once ;
- merge ;
- custom ;
- protected ;
- hash ;
- conflit ;
- écriture atomique ;
- encodage ;
- fins de ligne.

---

## 64.7 Tests de chemins

Vérifier :

- path traversal ;
- chemin absolu ;
- lien symbolique ;
- dossier parent interdit ;
- écriture hors cible ;
- normalisation.

---

## 64.8 Tests d’exécution

Vérifier :

- restore réussi ;
- restore échoué ;
- build réussi ;
- build échoué ;
- tests réussis ;
- tests échoués ;
- timeout ;
- annulation ;
- processus orphelin.

---

## 64.9 Tests de rapport

Vérifier :

- statut ;
- étapes ;
- durées ;
- erreurs ;
- warnings ;
- build ;
- tests ;
- absence de secret ;
- JSON valide ;
- ordre stable lorsque nécessaire.

---

## 64.10 Tests Git

Vérifier :

- init demandé ;
- init non demandé ;
- commit seulement après succès ;
- repository existant ;
- dossier sale ;
- absence de secret avant commit.

---

# 65. Golden master tests

Des tests de référence peuvent comparer :

- structure de solution ;
- fichiers de configuration ;
- Dockerfile ;
- Compose ;
- workflow CI ;
- lock file ;
- composition des modules.

Ils ne doivent pas rendre toute modification légitime difficile en comparant des détails non significatifs.

Les fichiers de référence doivent être revus explicitement lors d’une évolution.

---

# 66. Projet généré de référence

La CI SiteFactory doit générer au minimum :

```text
Pack :
sample-application

Template :
sample-application

Thème :
professional-clean

Modules :
platform-core
public-site
theme-system
admin-identity
content-management
contact-form
email-notifications
seo-baseline
legal-pages
lead-management
media-library
portfolio-basic
conversion-toolkit
operations-baseline
```

Puis exécuter :

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

---

# 67. Tests de reproductibilité

Le pipeline doit être exécuté deux fois avec :

- mêmes entrées ;
- mêmes versions ;
- même environnement maîtrisé.

Les résultats doivent être fonctionnellement équivalents.

Les différences autorisées doivent être documentées.

Exemples potentiels :

```text
GenerationId
GeneratedAtUtc
durées d’exécution
```

Les fichiers déterministes ne doivent pas varier inutilement.

---

# 68. Tests de non-écrasement

Vérifier :

1. générer un projet ;
2. modifier un fichier `custom` ;
3. relancer la génération ;
4. vérifier que le fichier reste inchangé ;
5. modifier un fichier `protected` ;
6. vérifier que l’opération est refusée ;
7. modifier un fichier `generated` ;
8. vérifier que le conflit est signalé selon la stratégie.

---

# 69. Tests de sécurité

Vérifier :

- secret dans YAML ;
- token dans fichier généré ;
- path traversal ;
- commande injectée ;
- URL dangereuse ;
- template non autorisé ;
- lien symbolique ;
- écriture hors dossier ;
- log de secret ;
- PII dans rapport.

---

# 70. Performance

## 70.1 Priorité

La priorité est :

```text
fiabilité
→ déterminisme
→ diagnostic
→ performance
```

Le pipeline n’a pas besoin de générer des milliers de projets simultanément.

---

## 70.2 Objectifs initiaux

Le pipeline doit rester suffisamment rapide pour une utilisation locale normale.

Les étapes longues doivent afficher une progression.

---

## 70.3 Parallélisme

Le parallélisme peut être utilisé pour :

- validation indépendante ;
- calcul de hash ;
- certains tests.

Il ne doit pas compliquer :

- l’ordre des logs ;
- les écritures ;
- le diagnostic ;
- l’annulation.

---

# 71. Observabilité du pipeline

Indicateurs possibles :

```text
durée totale
durée de validation
durée de génération
durée de restore
durée de build
durée des tests
nombre de fichiers
nombre de conflits
nombre de warnings
taux de succès
```

Ces mesures ne doivent pas exposer de contenu client.

---

# 72. Versionnement du pipeline

Le moteur suit SemVer.

Les changements suivants peuvent être breaking :

- changement du schéma de configuration ;
- changement du format du plan ;
- changement du lock file ;
- changement des stratégies de fichiers ;
- suppression d’une commande ;
- changement des codes de sortie ;
- changement incompatible des manifests.

Les formats structurants doivent posséder leur propre `schemaVersion`.

---

# 73. Compatibilité ascendante

Une nouvelle version du générateur doit idéalement pouvoir lire les configurations récentes supportées.

La matrice de compatibilité doit préciser :

```text
GeneratorVersion
ConfigurationSchemaVersion
CatalogVersion
TemplateVersion
ThemeVersion
ModuleManifestVersion
```

Une incompatibilité doit être détectée avant toute écriture.

---

# 74. Migrations de configuration

Une commande future peut proposer :

```bash
sitefactory config migrate
```

Elle devra :

- lire l’ancienne version ;
- produire un plan ;
- écrire un nouveau fichier séparé ;
- ne pas écraser l’original par défaut ;
- expliquer les changements ;
- signaler les pertes ou décisions manuelles.

Cette capacité est hors du premier MVP si elle retarde la génération initiale.

---

# 75. Critères d’acceptation du pipeline MVP

Le pipeline sera considéré comme opérationnel lorsque :

1. `validate` ne modifie aucun fichier ;
2. `plan` ne modifie aucun fichier ;
3. `generate --dry-run` ne modifie aucun fichier ;
4. la configuration YAML est validée ;
5. le schéma est validé ;
6. le catalogue est validé ;
7. les modules sont résolus ;
8. les cycles sont détectés ;
9. le template est résolu ;
10. le thème est résolu ;
11. les pages et sections sont résolues ;
12. un plan déterministe est produit ;
13. les chemins sont sécurisés ;
14. les conflits sont détectés ;
15. les stratégies de fichiers sont appliquées ;
16. les personnalisations sont protégées ;
17. le projet client est généré ;
18. les modules résolus sont composés ;
19. le lock file est généré ;
20. Docker est généré ;
21. la CI est générée ;
22. la documentation est générée ;
23. le restore réussit ;
24. le build Release réussit ;
25. les tests réussissent ;
26. les placeholders sont contrôlés ;
27. les secrets sont contrôlés ;
28. le rapport final est généré ;
29. le repository Git peut être initialisé ;
30. aucune création de remote n’est automatique ;
31. aucun secret n’est écrit ;
32. aucune migration production n’est appliquée ;
33. les tests de reproductibilité réussissent ;
34. les tests de non-écrasement réussissent ;
35. les tests de sécurité réussissent.

---

# 76. Décisions validées

La baseline du pipeline est la suivante :

```text
La planification précède toujours l’exécution.

Validate, plan et dry-run ne modifient aucun fichier.

La configuration YAML est considérée comme non fiable
jusqu’à validation complète.

Les catalogues et manifests sont validés avant résolution.

Les packs, entitlements et modules sont résolus séparément.

Le template et le thème sont résolus après les modules.

Les pages et sections sont validées avant génération.

Le plan décrit toutes les opérations prévues.

Les chemins sont normalisés et sécurisés.

Les fichiers utilisent les stratégies generated, create-once,
merge, custom et protected.

Les fichiers personnalisés ne sont jamais écrasés silencieusement.

Une génération initiale utilise idéalement un dossier temporaire.

La composition des modules est explicite.

Les versions sont verrouillées dans sitefactory.lock.json.

Le projet généré doit réussir restore, build Release et tests.

Les placeholders et secrets sont contrôlés.

Le rapport final ne contient aucun secret ni donnée personnelle.

Git peut être initialisé localement après succès.

Le repository distant n’est jamais créé automatiquement par défaut.

Les migrations de production ne sont jamais appliquées
par le pipeline de génération.

L’option --force ne contourne pas les protections critiques.

Codex ne doit jamais déclarer une génération réussie
sans avoir exécuté les contrôles prévus.
```

---

# 77. Checklist de revue du plan

```text
[ ] Configuration validée
[ ] Catalogue validé
[ ] Pack résolu
[ ] Entitlements résolus
[ ] Modules résolus
[ ] Dépendances visibles
[ ] Template résolu
[ ] Thème résolu
[ ] Pages résolues
[ ] Sections résolues
[ ] Fichiers listés
[ ] Stratégies listées
[ ] Commandes listées
[ ] Conflits listés
[ ] Actions manuelles listées
[ ] Aucun secret
[ ] Aucun chemin dangereux
```

---

# 78. Checklist de revue d’une génération

## Entrées

```text
[ ] Configuration correcte
[ ] Version du schéma connue
[ ] Catalogue connu
[ ] Template connu
[ ] Thème connu
[ ] Dossier cible validé
```

## Résolution

```text
[ ] Pack valide
[ ] Entitlements valides
[ ] Modules valides
[ ] Dépendances résolues
[ ] Aucun cycle
[ ] Aucune incompatibilité
[ ] Features cohérentes
```

## Fichiers

```text
[ ] Plan produit
[ ] Conflits traités
[ ] Fichiers custom préservés
[ ] Fichiers protected préservés
[ ] Encodage cohérent
[ ] Ordre déterministe
```

## Qualité

```text
[ ] Restore réussi
[ ] Build Release réussi
[ ] 0 warning injustifié
[ ] Tests réussis
[ ] Aucun placeholder bloquant
[ ] Aucun secret
[ ] Analyse de sécurité réussie
```

## Sorties

```text
[ ] Lock file produit
[ ] Rapport produit
[ ] Docker produit
[ ] CI produite
[ ] Documentation produite
[ ] Git initialisé seulement si demandé
```

---

# 79. Règles pour Codex

## 79.1 Lecture préalable

Avant toute modification du pipeline, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
13-generation-pipeline.md
Contrat public des pages et sections du module concerne : identifiants stables, schemas, validation et strategies de fichiers explicites
Contrat public de rendu du module concerne : composants et styles versionnes, encodage, accessibilite et securite du contenu
../02-architecture/core-module-boundary.md
09-module-architecture.md
10-feature-flags-and-entitlements.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
13-generation-pipeline.md
AGENTS.md
```

Ainsi que :

- les schémas ;
- les catalogues ;
- les manifests ;
- les tests ;
- les formats du plan ;
- le format du rapport.

---

## 79.2 Ce que Codex peut faire

Codex peut :

- implémenter une étape validée ;
- ajouter une validation ;
- améliorer un diagnostic ;
- créer une stratégie de fichier ;
- ajouter un test de sécurité ;
- créer le rapport ;
- corriger un conflit ;
- améliorer l’idempotence ;
- ajouter un test de reproductibilité ;
- documenter une commande.

---

## 79.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- écrire avant la création du plan ;
- modifier le catalogue sans demande ;
- modifier les prix ;
- ignorer une clé inconnue silencieusement ;
- écraser un fichier custom ;
- écraser un fichier protected ;
- désactiver un test pour faire passer la génération ;
- masquer un warning sans justification ;
- exécuter une commande shell concaténée avec une entrée non fiable ;
- écrire hors du dossier cible ;
- appliquer une migration production ;
- créer automatiquement un repository distant ;
- publier un package ;
- ajouter un téléchargement arbitraire ;
- passer un secret en argument de commande ;
- considérer un build non exécuté comme réussi ;
- considérer un test ignoré comme réussi ;
- déclarer un succès partiel comme succès complet.

---

## 79.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouveau format de configuration ;
- nouvelle stratégie de fichier ;
- changement du format du lock file ;
- changement du format du rapport ;
- nouvelle commande destructive ;
- nouvelle source externe ;
- génération automatique de migrations ;
- modification de la stratégie Git ;
- publication automatique ;
- introduction de scripts exécutables issus des templates ;
- nouvelle étape de production ;
- modification des codes de sortie ;
- suppression de compatibilité.

---

## 79.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Étapes du pipeline modifiées
Entrées affectées
Formats affectés
Stratégies de fichiers affectées
Commandes affectées
Codes d’erreur affectés
Contrôles ajoutés
Tests ajoutés
Tests de sécurité
Tests de reproductibilité
Build
Tests
Impact sur les projets existants
Breaking changes
Décisions restant humaines
```

---

# 80. Anti-patterns interdits

## 80.1 Génération directe sans plan

Le CLI écrit immédiatement les fichiers pendant la lecture de la configuration.

---

## 80.2 Handler CLI monolithique

Un seul handler :

- parse ;
- valide ;
- résout ;
- écrit ;
- lance Git ;
- lance Docker ;
- produit le rapport.

---

## 80.3 Écrasement global avec `--force`

`--force` supprime ou remplace tout le dossier cible.

---

## 80.4 Modification silencieuse d’un fichier custom

Le générateur réécrit une personnalisation client.

---

## 80.5 Commande shell concaténée

```csharp
var command = $"dotnet build {userProvidedPath}";
```

exécutée par un shell.

---

## 80.6 Succès sans build

Le pipeline termine avec `SUCCESS` après création des fichiers sans compiler le projet.

---

## 80.7 Succès avec tests échoués

Les tests échouent mais le rapport final reste positif.

---

## 80.8 Secrets dans le rapport

Le rapport contient la configuration complète de l’environnement.

---

## 80.9 Différences non déterministes

L’ordre des modules ou fichiers change à chaque génération sans raison.

---

## 80.10 Exécution de scripts de template

Un template peut lancer une commande arbitraire.

---

## 80.11 Auto-migration de production

Le pipeline applique une migration à une base désignée par une chaîne de connexion de production.

---

## 80.12 Création automatique du remote GitHub

Le pipeline pousse le code sans validation humaine.

---

# 81. Roadmap d’implémentation du pipeline

## Étape 1 — Validation

```text
load config
validate YAML
validate schema
load catalog
resolve pack
resolve modules
```

## Étape 2 — Planification

```text
resolve template
resolve theme
resolve pages
create GenerationPlan
dry-run output
```

## Étape 3 — Génération minimale

```text
solution
Web
Data
Customizations
Tests
lock file
report
```

## Étape 4 — Validation technique

```text
restore
build
tests
```

## Étape 5 — Artefacts opérationnels

```text
Docker
CI/CD
scripts
documentation
```

## Étape 6 — Sécurité

```text
path security
secret scan
placeholder scan
conflict handling
```

## Étape 7 — Reproductibilité

```text
golden master
second generation
non-overwrite tests
Git initialization
```

L’implémentation doit avancer par incréments utilisables.

---

# 82. Conclusion

Le pipeline de génération constitue le cœur opérationnel de SiteFactory.

Il doit transformer une configuration validée en un projet client complet sans sacrifier :

- la sécurité ;
- la reproductibilité ;
- la lisibilité ;
- la maintenabilité ;
- les personnalisations ;
- la qualité du résultat.

Le pipeline repose sur quatre idées centrales :

```text
Valider avant de résoudre

Résoudre avant de planifier

Planifier avant d’écrire

Tester avant de déclarer un succès
```

> Une génération réussie n’est pas un ensemble de fichiers créés. C’est un projet cohérent, traçable, compilable, testé, sécurisé et prêt à poursuivre son cycle de livraison.
