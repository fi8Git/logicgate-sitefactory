---
adrId: ADR-0005
title: Use explicit file ownership and generation strategies
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
  - ../02-architecture/09-module-architecture.md
  - ../03-generation/14-client-configuration-schema.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/18-security-baseline.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0002-yaml-client-configuration.md
targetVersion: 0.1.0
---

# ADR-0005 — Stratégies explicites de génération et de propriété des fichiers

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Les cinq stratégies ci-dessous sont canoniques. Le
[pipeline public](../03-generation/13-generation-pipeline.md) les contrôle
avant écriture ; un merge sans implémentation et une stratégie inconnue sont
refusés, jamais convertis en overwrite. Les destinations logiques et physiques
déterministement invalides sont refusées avant la première écriture.
L'atomicité est **par fichier**, pas une transaction filesystem générale.
La régénération exige un état reconnu et préserve les customizations ;
les mécanismes futurs d'upgrade, d'adoption ou de merge spécialisé décrits
ci-dessous ne sont pas annoncés comme disponibles.

## 1. Statut

```text
accepted
```

Chaque fichier ou zone de fichiers manipulé par LogicGate SiteFactory doit posséder une stratégie explicite parmi :

```text
generated
create-once
merge
custom
protected
```

SiteFactory ne doit jamais déterminer implicitement la propriété d’un fichier uniquement à partir :

- de son extension ;
- de son emplacement approximatif ;
- de son contenu ;
- de sa date de modification ;
- d’une convention non déclarée.

Aucun fichier existant ne peut être écrasé ou supprimé silencieusement pendant :

- une régénération ;
- un upgrade ;
- un changement de template ;
- un changement de thème ;
- un changement de module ;
- une mise à niveau du générateur.

---

## 2. Résumé

SiteFactory distingue cinq modèles de propriété.

| Stratégie | Propriétaire principal | Comportement général |
|---|---|---|
| `generated` | SiteFactory | Peut être remplacé si son état n’a pas été modifié manuellement |
| `create-once` | Client après création | Créé uniquement s’il est absent, puis préservé |
| `merge` | Partagé | Modifié uniquement par un merge structuré et déterministe |
| `custom` | Client | Jamais créé, remplacé ou supprimé automatiquement |
| `protected` | Client ou opérateur | Toute tentative d’écriture automatique est refusée |

Le pipeline suit obligatoirement :

```text
inspection
→ résolution des stratégies
→ comparaison avec l’état précédent
→ plan
→ détection des conflits
→ validation
→ application contrôlée
→ vérification
→ rapport
```

Un conflit doit interrompre l’application avant toute modification définitive lorsque le pipeline ne peut pas garantir un résultat sûr.

---

## 3. Contexte

SiteFactory doit générer des projets clients autonomes à partir :

- d’une configuration ;
- d’un pack ;
- d’un template ;
- d’un thème ;
- de modules ;
- de profils de déploiement ;
- de versions verrouillées.

Après la première génération, un projet client évolue.

Il peut recevoir :

- des contenus ;
- des personnalisations ;
- des migrations ;
- des corrections ;
- de nouveaux modules ;
- des mises à niveau ;
- des modifications opérateur ;
- des évolutions spécifiques au client.

Certains fichiers restent entièrement pilotés par SiteFactory.

D’autres deviennent la propriété du projet client.

D’autres encore doivent agréger des contributions provenant à la fois :

- du générateur ;
- des modules ;
- du client.

Sans stratégie explicite, une régénération peut provoquer :

- l’écrasement de code client ;
- la perte de configuration ;
- la suppression d’une migration ;
- la perte d’un runbook adapté ;
- une dérive silencieuse ;
- des conflits difficiles à diagnostiquer ;
- l’impossibilité de mettre à niveau un projet en sécurité.

---

## 4. Problème à résoudre

Comment permettre à SiteFactory de générer et mettre à niveau un projet client sans :

- écraser les personnalisations ;
- supprimer des fichiers importants ;
- remplacer un fichier modifié ;
- dépendre d’un merge textuel fragile ;
- considérer tous les fichiers comme entièrement générés ;
- empêcher toute évolution automatique ;
- créer des diffs massifs et imprévisibles ?

La solution doit permettre de savoir pour chaque fichier :

```text
qui le possède

qui peut le modifier

comment il doit évoluer

comment détecter une modification manuelle

comment gérer sa suppression

comment gérer un conflit

comment revenir en arrière
```

---

## 5. Portée

Cette décision couvre :

- génération initiale ;
- régénération ;
- upgrade ;
- changement de template ;
- changement de thème ;
- ajout ou retrait de module ;
- fichiers texte ;
- fichiers binaires ;
- répertoires réservés ;
- état de génération ;
- hashes ;
- stratégies de merge ;
- suppression ;
- renommage ;
- conflits ;
- plans ;
- rapports ;
- sécurité des chemins ;
- comportement Git ;
- règles applicables à Codex.

---

## 6. Hors portée

Cette décision ne définit pas :

- le contenu exact de chaque template ;
- toutes les règles de fusion de chaque format ;
- un merge Git généraliste ;
- la résolution automatique de conflits métier ;
- la fusion de code C# arbitraire ;
- la modification automatique de secrets ;
- l’application de migrations en production ;
- le transfert client ;
- le choix exact de la bibliothèque de diff ;
- une interface graphique de résolution des conflits.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Protection du code et des données du client**
2. **Absence d’écrasement silencieux**
3. **Déterminisme**
4. **Détection fiable des modifications manuelles**
5. **Capacité de mise à niveau**
6. **Lisibilité des responsabilités**
7. **Résolution contrôlée des conflits**
8. **Réversibilité**
9. **Compatibilité Git**
10. **Compatibilité Windows et Linux**
11. **Extensibilité par modules**
12. **Maintenabilité par une petite équipe**
13. **Automatisation future**
14. **Rapports auditables**

---

## 8. Contraintes

La décision doit respecter :

```text
.NET 10

Repository Git par client

Configuration déclarative

Versions verrouillées

Génération déterministe

Fichiers custom préservés

Fichiers protected intouchables

Aucune écriture hors du dossier cible

Aucun secret dans les états ou rapports

Compatibilité Windows et Linux

Plan avant exécution

Rapport après exécution
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- la majorité des fichiers générés reste raisonnablement petite ;
- Git est disponible dans les projets clients ;
- le projet client conserve l’état de génération dans son repository ;
- les personnalisations sont placées dans les zones prévues ;
- les modules déclarent leurs fichiers ;
- les fusions structurées restent limitées à des formats connus ;
- une modification client d’un fichier généré doit être considérée comme un conflit, et non comme une nouvelle source implicite ;
- les upgrades sont exécutés sur une branche dédiée avec un working tree propre.

---

# 10. Option A — Régénérer et écraser tous les fichiers

## 10.1 Description

Chaque génération remplace entièrement les fichiers correspondant au template et aux modules actifs.

---

## 10.2 Avantages

- implémentation simple ;
- résultat uniforme ;
- aucun état de génération à conserver ;
- mises à jour rapides ;
- peu de logique de conflit.

---

## 10.3 Inconvénients

- perte des personnalisations ;
- perte de documentation locale ;
- perte de corrections client ;
- impossibilité de faire évoluer un projet indépendamment ;
- risque élevé de suppression de données ou de configuration ;
- transfert client peu réaliste.

---

## 10.4 Risques

- écrasement de code ;
- perte de migrations ;
- perte de fichiers d’exploitation ;
- perte de paramètres ;
- impossibilité de restaurer le travail humain.

---

## 10.5 Réversibilité

```text
faible
```

Une fois un fichier écrasé, le retour dépend uniquement de Git ou d’un backup externe.

---

# 11. Option B — Ne jamais modifier un fichier existant

## 11.1 Description

SiteFactory crée uniquement les fichiers absents.

Toute évolution d’un fichier existant devient manuelle.

---

## 11.2 Avantages

- aucun écrasement ;
- sécurité simple ;
- propriété client claire ;
- implémentation initiale limitée.

---

## 11.3 Inconvénients

- upgrades presque entièrement manuels ;
- correctifs de sécurité difficiles à propager ;
- projets rapidement divergents ;
- impossibilité d’améliorer les fichiers générés ;
- charge de maintenance croissante ;
- bénéfices d’industrialisation limités après la création initiale.

---

## 11.4 Risques

- projets obsolètes ;
- correctifs non appliqués ;
- dérive importante ;
- documentation et scripts périmés ;
- versions difficiles à comparer.

---

## 11.5 Réversibilité

```text
élevée
```

mais la capacité d’évolution automatisée devient très faible.

---

# 12. Option C — Merge textuel générique

## 12.1 Description

SiteFactory compare l’ancien fichier, le fichier actuel et le nouveau fichier généré puis tente un merge textuel.

---

## 12.2 Avantages

- possibilité théorique de préserver les modifications ;
- applicable à de nombreux formats ;
- similaire à certaines opérations Git ;
- réduction apparente du travail manuel.

---

## 12.3 Inconvénients

- absence de compréhension sémantique ;
- conflits difficiles à interpréter ;
- formatage perturbant ;
- risque d’obtenir un fichier syntaxiquement valide mais fonctionnellement incorrect ;
- merge fragile sur C#, YAML, XML et scripts ;
- résultat dépendant fortement du diff.

---

## 12.4 Risques

- corruption logique ;
- conflits masqués ;
- duplication ;
- sections déplacées ;
- ordre incorrect ;
- modification de secrets ;
- résultat non déterministe.

---

## 12.5 Réversibilité

```text
moyenne
```

Le résultat doit être revu manuellement et peut nécessiter un retour Git.

---

# 13. Option D — Stratégie explicite par fichier

## 13.1 Description

Chaque fichier ou règle de chemin déclare :

- son propriétaire ;
- sa stratégie ;
- sa source ;
- sa version ;
- son comportement de mise à jour ;
- son comportement de suppression.

SiteFactory applique une règle différente selon :

```text
generated
create-once
merge
custom
protected
```

---

## 13.2 Avantages

- propriété explicite ;
- non-écrasement contrôlé ;
- upgrades possibles ;
- conflits détectés ;
- stratégies adaptées au type de fichier ;
- rapports précis ;
- meilleure isolation des customizations ;
- suppression maîtrisée ;
- extension progressive.

---

## 13.3 Inconvénients

- état de génération à maintenir ;
- manifests plus détaillés ;
- logique de planification plus importante ;
- tests nombreux ;
- merge handlers à développer ;
- conflits nécessitant parfois une action humaine.

---

## 13.4 Risques

- mauvaise stratégie déclarée ;
- état corrompu ;
- collision de règles ;
- merge handler défaillant ;
- complexité croissante du générateur.

---

## 13.5 Réversibilité

```text
élevée
```

Les changements sont planifiés, versionnés et suivis par Git.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Écrasement | Création seule | Merge générique | Stratégies explicites |
|---|---:|---:|---:|---:|---:|
| Protection client | 5 | 1 | 5 | 3 | 5 |
| Capacité d’upgrade | 5 | 5 | 1 | 4 | 5 |
| Déterminisme | 5 | 5 | 5 | 2 | 5 |
| Lisibilité | 4 | 3 | 4 | 2 | 5 |
| Détection de conflits | 5 | 1 | 3 | 3 | 5 |
| Réversibilité | 5 | 2 | 5 | 3 | 5 |
| Simplicité initiale | 3 | 5 | 5 | 3 | 2 |
| Sécurité des fichiers | 5 | 1 | 5 | 3 | 5 |
| Maintenabilité | 4 | 2 | 2 | 2 | 5 |
| Extensibilité modules | 4 | 3 | 2 | 3 | 5 |

Les stratégies explicites offrent le meilleur équilibre entre protection et capacité d’évolution.

---

# 15. Décision

Nous décidons que chaque fichier ou zone de fichiers géré par SiteFactory possède une stratégie explicite parmi :

```text
generated

create-once

merge

custom

protected
```

La stratégie est déclarée dans le manifest du composant propriétaire :

- plateforme ;
- module ;
- template ;
- thème ;
- profil de déploiement.

Aucune écriture n’est autorisée sans résolution préalable de cette stratégie.

---

# 16. Modèle de propriété

Les stratégies correspondent aux modèles de propriété suivants.

| Stratégie | Modèle de propriété |
|---|---|
| `generated` | Propriété SiteFactory |
| `create-once` | Propriété transférée au client après création |
| `merge` | Propriété partagée par contrat structuré |
| `custom` | Propriété exclusive du client |
| `protected` | Zone gardée contre toute modification automatique |

---

# 17. Stratégie `generated`

## 17.1 Définition

Un fichier `generated` est entièrement produit par SiteFactory.

Son contenu doit être modifié à travers :

- la configuration ;
- le template ;
- le thème ;
- un module ;
- le générateur ;
- un upgrade.

---

## 17.2 Création initiale

Si le fichier est absent, SiteFactory peut le créer.

---

## 17.3 Régénération sans modification locale

Si le fichier actuel correspond au dernier contenu généré connu, SiteFactory peut le remplacer par sa nouvelle version.

---

## 17.4 Modification locale détectée

Si le fichier actuel ne correspond plus à son dernier hash généré :

```text
conflict
```

SiteFactory ne doit pas l’écraser silencieusement.

---

## 17.5 Correction attendue

L’opérateur doit alors choisir parmi :

- déplacer la personnalisation vers une extension prévue ;
- revenir à la version générée ;
- transformer le fichier en `create-once` par migration explicite ;
- résoudre le conflit ;
- autoriser un remplacement forcé et ciblé.

---

## 17.6 Suppression

Un fichier `generated` absent du nouveau plan peut être supprimé uniquement si :

- il était enregistré comme généré ;
- son hash actuel correspond au dernier hash généré ;
- aucune règle plus protectrice ne s’applique ;
- la suppression apparaît dans le plan.

---

## 17.7 Fichier généré modifié puis retiré

S’il a été modifié localement, il devient :

```text
orphaned conflict
```

Il n’est pas supprimé automatiquement.

---

## 17.8 Exemples

```text
fichiers de composition de modules

enregistrements DI générés

fichiers de métadonnées

fichiers JSON machine

certains fichiers de déploiement non personnalisés
```

---

# 18. Stratégie `create-once`

## 18.1 Définition

Un fichier `create-once` est produit une seule fois par SiteFactory.

Après sa création, sa propriété est transférée au projet client.

---

## 18.2 Fichier absent

SiteFactory le crée.

---

## 18.3 Fichier présent

SiteFactory le préserve intégralement.

Il ne compare pas son contenu pour décider de le remplacer.

---

## 18.4 Nouvelle version disponible

Si une version plus récente du modèle existe, SiteFactory :

- conserve le fichier actuel ;
- signale la nouvelle version dans le plan ;
- produit une action manuelle ;
- peut générer un candidat séparé sur demande explicite.

---

## 18.5 Pas de sidecar implicite

SiteFactory ne doit pas créer automatiquement des fichiers tels que :

```text
README.md.new

README.md.updated

README.sitefactory.md
```

sans option ou contrat explicite.

---

## 18.6 Suppression

Un fichier `create-once` n’est jamais supprimé automatiquement.

---

## 18.7 Exemples

```text
README.md

AGENTS.md du projet client

CONTRIBUTING.md

documentation initiale

exemple d’extension client

runbook destiné à être adapté manuellement
```

---

# 19. Stratégie `merge`

## 19.1 Définition

Un fichier `merge` est partagé entre SiteFactory et le projet client.

La fusion doit être :

- structurée ;
- déterministe ;
- versionnée ;
- spécifique au format ;
- testée.

---

## 19.2 Pas de merge textuel générique

La stratégie `merge` ne doit pas utiliser un simple remplacement de blocs ou une regex non structurée comme mécanisme général.

---

## 19.3 Merge handler

Chaque fichier `merge` déclare un handler.

Exemples conceptuels :

```text
msbuild-central-packages-v1

slnx-project-list-v1

json-object-properties-v1

xml-owned-elements-v1
```

---

## 19.4 Identité des éléments

Les éléments gérés doivent posséder une identité stable.

Exemples :

- `PackageId` ;
- project path ;
- clé JSON ;
- identifiant de module ;
- nom d’élément XML.

---

## 19.5 Propriété partielle

Le merge handler doit distinguer :

- éléments SiteFactory ;
- éléments client ;
- éléments inconnus ;
- conflits.

---

## 19.6 Valeur modifiée des deux côtés

Si SiteFactory et le client modifient le même élément depuis la dernière génération :

```text
merge conflict
```

---

## 19.7 Suppression

SiteFactory peut retirer uniquement les éléments qu’il possède explicitement.

Il ne doit pas supprimer un élément client ou inconnu.

---

## 19.8 Formatage

Le merge doit limiter les modifications de formatage aux zones nécessaires.

---

## 19.9 Commentaires

Un merge handler doit définir clairement s’il préserve les commentaires.

Si cette préservation n’est pas garantie, le fichier ne doit pas utiliser `merge` sans décision explicite.

---

## 19.10 Exemples

```text
Directory.Packages.props

fichier .slnx

certains manifests structurés

configuration JSON non sensible avec zones possédées
```

---

# 20. Stratégie `custom`

## 20.1 Définition

Un fichier ou dossier `custom` appartient exclusivement au projet client.

SiteFactory peut connaître son emplacement, mais ne gère pas son contenu.

---

## 20.2 Création

Le générateur ne crée pas de fichier `custom`.

Un éventuel exemple initial utilise la stratégie :

```text
create-once
```

puis le client crée ses propres fichiers dans la zone `custom`.

---

## 20.3 Modification

Toute tentative de modification automatique est refusée.

---

## 20.4 Suppression

Aucun fichier `custom` n’est supprimé automatiquement.

---

## 20.5 Lecture

SiteFactory peut inspecter certaines métadonnées nécessaires à :

- la validation ;
- la détection de collisions ;
- l’architecture ;
- le build.

Il ne doit pas réinterpréter le contenu comme source générée.

---

## 20.6 Exemples

```text
ClientName.Customizations/**

composants propres au client

services métier spécifiques

styles client additionnels

tests propres au client
```

---

# 21. Stratégie `protected`

## 21.1 Définition

Une zone `protected` constitue une interdiction explicite d’écriture automatique.

Elle protège des fichiers dont la modification exige une opération distincte et contrôlée.

---

## 21.2 Comportement

SiteFactory peut :

- vérifier la présence ;
- calculer un hash ;
- produire un diagnostic ;
- signaler une incompatibilité.

SiteFactory ne peut pas :

- créer ;
- modifier ;
- renommer ;
- supprimer ;
- fusionner ;

un fichier `protected` dans le pipeline normal de génération ou d’upgrade.

---

## 21.3 Force

Une option générale `--force` ne contourne jamais la stratégie `protected`.

---

## 21.4 Modification légitime

Une modification nécessite :

- une tâche explicite ;
- une commande dédiée ;
- un plan ;
- une validation humaine ;
- des tests adaptés.

---

## 21.5 Exemples

```text
client.sitefactory.yml pendant une génération normale

migrations validées

fichiers de secrets locaux

certificats ou clés

artefacts de transfert

zones opératoires sensibles
```

Les secrets eux-mêmes restent hors Git et hors de l’état de génération.

---

# 22. Différence entre `create-once`, `custom` et `protected`

## 22.1 `create-once`

SiteFactory crée le fichier initial, puis le préserve.

---

## 22.2 `custom`

SiteFactory ne crée et ne possède jamais le contenu.

---

## 22.3 `protected`

SiteFactory interdit explicitement toute écriture automatisée, y compris lors d’un upgrade.

---

## 22.4 Exemple

```text
README initial
→ create-once

ClientName.Customizations/MyService.cs
→ custom

client.sitefactory.yml
→ protected pendant generate et upgrade apply
```

Une commande explicite `config migrate` peut posséder son propre contrat pour proposer une nouvelle configuration sans modifier l’original.

---

# 23. Déclaration dans les manifests

Chaque composant peut déclarer ses fichiers.

Exemple conceptuel :

```yaml
files:
  - source: templates/application/ModuleRegistry.cs.scriban
    target: src/Client.Web/Generated/ModuleRegistry.g.cs
    strategy: generated
    encoding: utf-8
    lineEndings: lf

  - source: templates/project/README.md
    target: README.md
    strategy: create-once

  - source: templates/project/Directory.Packages.props
    target: Directory.Packages.props
    strategy: merge
    mergeHandler: msbuild-central-packages-v1

  - target: src/Client.Customizations/**
    strategy: custom

  - target: client.sitefactory.yml
    strategy: protected
```

La structure exacte du manifest reste définie dans son schéma officiel.

---

# 24. Règles de priorité

## 24.1 Stratégie explicite

Une règle précise possède priorité sur une règle de répertoire plus large.

Exemple :

```text
src/Client.Customizations/**
→ custom

src/Client.Customizations/README.md
→ create-once
```

---

## 24.2 Ambiguïté

Deux règles de même priorité attribuant des stratégies différentes constituent une erreur.

---

## 24.3 Propriété multiple

Deux composants ne peuvent pas déclarer la propriété exclusive du même fichier.

---

## 24.4 Fichier `merge`

Plusieurs composants peuvent contribuer à un fichier `merge` uniquement si :

- le merge handler le permet ;
- chaque contribution possède une identité stable ;
- les contributions ne se chevauchent pas.

---

# 25. État de génération

SiteFactory doit conserver un état machine-readable relatif aux fichiers gérés.

Emplacement retenu :

```text
.sitefactory/generation-state.json
```

---

## 25.1 Rôle

Ce fichier permet de connaître :

- le chemin ;
- la stratégie ;
- le composant source ;
- la version source ;
- le dernier hash généré ;
- le merge handler ;
- les éléments possédés ;
- la date ou génération logique de référence.

---

## 25.2 Versionnement Git

Le fichier est versionné dans Git.

Il ne contient ni secret ni donnée métier.

---

## 25.3 Différence avec le lock file

```text
sitefactory.lock.json
→ versions et composition résolue

.sitefactory/generation-state.json
→ propriété et état des fichiers gérés
```

---

## 25.4 Modification manuelle

Le fichier d’état est généré.

Il ne doit pas être modifié manuellement afin de masquer un conflit.

---

# 26. Structure conceptuelle de l’état

```json
{
  "schemaVersion": 1,
  "generatorVersion": "0.4.0",
  "files": [
    {
      "path": "src/Client.Web/Generated/ModuleRegistry.g.cs",
      "strategy": "generated",
      "sourceComponent": "platform",
      "sourceVersion": "1.2.0",
      "lastGeneratedSha256": "...",
      "encoding": "utf-8",
      "lineEndings": "lf"
    },
    {
      "path": "Directory.Packages.props",
      "strategy": "merge",
      "mergeHandler": "msbuild-central-packages-v1",
      "lastGeneratedSha256": "...",
      "ownedElements": [
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "Microsoft.EntityFrameworkCore"
      ]
    }
  ]
}
```

---

# 27. Hashes

## 27.1 Algorithme

Les fichiers utilisent :

```text
SHA-256
```

---

## 27.2 Fichiers `generated`

Le hash porte sur les octets canoniques générés.

---

## 27.3 Fichiers `merge`

Le système peut conserver :

- un hash global ;
- un hash des éléments possédés ;
- un modèle structuré précédent.

Le choix dépend du merge handler.

---

## 27.4 Fichiers `create-once`

Le hash initial peut être conservé à titre d’information, mais il ne doit pas servir à autoriser un remplacement automatique.

---

## 27.5 Fichiers `custom` et `protected`

SiteFactory peut calculer un hash pour vérifier une précondition, sans acquérir la propriété du fichier.

---

# 28. Encodage

Le format texte de référence est :

```text
UTF-8 sans BOM
```

sauf contrat contraire explicite.

---

## 28.1 Fichier existant

Un fichier client existant ne doit pas être réencodé automatiquement uniquement pour appliquer une règle de style.

---

## 28.2 Fichier généré

Un fichier `generated` peut être normalisé selon son contrat d’encodage.

---

# 29. Fins de ligne

La stratégie de fins de ligne doit être déclarée ou définie par le repository.

Valeurs possibles :

```text
lf
crlf
preserve
```

---

## 29.1 Baseline

Les fichiers générés multiplateformes privilégient :

```text
lf
```

avec une configuration `.gitattributes` cohérente.

---

## 29.2 Hash

Le hash d’un fichier généré est calculé après normalisation des fins de ligne prévues.

---

## 29.3 Fichiers client

Les fichiers `create-once`, `custom` et `protected` ne sont pas reformattés pour modifier leurs fins de ligne.

---

# 30. Fichiers binaires

## 30.1 `generated`

Un fichier binaire peut être `generated`.

Il est remplacé uniquement si :

- son hash actuel correspond au dernier hash généré ;
- ou un remplacement forcé ciblé est validé.

---

## 30.2 `merge`

Le merge de fichiers binaires n’est pas supporté dans le MVP.

---

## 30.3 `custom` et `protected`

Ils restent intouchables.

---

# 31. Sécurité des chemins

Chaque chemin doit être :

- relatif à la racine cible ;
- normalisé ;
- sans `..` ;
- sans racine absolue ;
- sans préfixe réseau ;
- sans caractère nul ;
- compatible avec les plateformes supportées.

---

## 31.1 Path traversal

Les chemins tels que :

```text
../outside.txt

../../.ssh/authorized_keys

C:\Windows\file.txt

\\server\share\file.txt
```

sont refusés.

---

## 31.2 Racine cible

Après résolution, le chemin final doit rester sous la racine du projet.

---

## 31.3 Chemins réservés

Les chemins système, Git internes et répertoires SiteFactory sensibles doivent être protégés selon leur contrat.

---

# 32. Liens symboliques

## 32.1 Baseline

SiteFactory ne suit pas un lien symbolique permettant de sortir de la racine cible.

---

## 32.2 Écriture sur symlink

Une écriture visant un symlink est refusée par défaut.

---

## 32.3 Support futur

Un support plus avancé nécessiterait :

- cas d’usage réel ;
- résolution sécurisée ;
- tests Windows et Linux ;
- nouvel ADR ou extension explicite.

---

# 33. Collisions de casse

Windows et Linux peuvent traiter différemment :

```text
File.cs

file.cs
```

SiteFactory doit détecter les collisions de chemins en mode insensible à la casse avant toute écriture.

---

## 33.1 Refus

Deux fichiers ne doivent pas être générés avec des chemins ne différant que par la casse.

---

# 34. Noms réservés

Le générateur doit refuser ou normaliser selon une règle explicite les noms incompatibles avec Windows.

Exemples :

```text
CON

PRN

AUX

NUL

COM1

LPT1
```

La génération doit rester portable entre Windows et Linux.

---

# 35. Phases du pipeline

## 35.1 Inspection

Lire :

- configuration ;
- lock file ;
- état de génération ;
- manifests ;
- fichiers existants ;
- état Git.

---

## 35.2 Résolution

Déterminer pour chaque chemin :

- propriétaire ;
- stratégie ;
- action attendue ;
- conflit éventuel.

---

## 35.3 Planification

Produire un plan sans écrire dans le projet.

---

## 35.4 Validation

Vérifier :

- chemins ;
- stratégies ;
- hashes ;
- merge handlers ;
- collisions ;
- Git ;
- capacité de rollback.

---

## 35.5 Staging

Produire les candidats dans un répertoire temporaire isolé.

---

## 35.6 Vérification

Valider :

- syntaxe ;
- schémas ;
- contenu ;
- hashes ;
- cohérence ;
- éventuellement build et tests.

---

## 35.7 Application

Promouvoir uniquement les opérations validées.

---

## 35.8 Rapport

Produire un rapport complet, y compris en cas d’échec contrôlé.

---

# 36. Actions du plan

Valeurs recommandées :

```text
create

update

merge

preserve

delete

rename

conflict

manual-action

no-op
```

---

## 36.1 Exemple

| Chemin | Stratégie | Action |
|---|---|---|
| `ModuleRegistry.g.cs` | `generated` | `update` |
| `README.md` | `create-once` | `preserve` |
| `Directory.Packages.props` | `merge` | `merge` |
| `Customizations/X.cs` | `custom` | `preserve` |
| `client.sitefactory.yml` | `protected` | `no-op` |

---

# 37. Plan sans effet de bord

Les commandes suivantes ne doivent pas modifier le projet :

```text
generate plan

upgrade check

upgrade plan

repository validate
```

Elles peuvent écrire leur rapport uniquement dans un emplacement explicitement demandé ou temporaire.

---

# 38. Conflits bloquants

Constituent notamment des conflits bloquants :

```text
Fichier generated modifié localement

Merge sémantique impossible

Tentative d’écriture custom

Tentative d’écriture protected

Propriété multiple

Collision de casse

Path traversal

Symlink dangereux

État de génération incohérent

Suppression non autorisée
```

---

## 38.1 Application partielle

En présence d’un conflit bloquant, SiteFactory ne doit pas appliquer les autres modifications du même plan par défaut.

---

## 38.2 Rapport

Tous les conflits détectables doivent être regroupés afin d’éviter une correction itérative fichier par fichier lorsque cela est possible.

---

# 39. Résolution des conflits

Processus recommandé :

```text
1. Lire le plan et le rapport
2. Examiner le diff
3. Identifier le propriétaire réel
4. Déplacer les personnalisations vers une zone custom
5. Résoudre le merge structuré
6. Restaurer ou accepter explicitement un fichier generated
7. Régénérer le plan
8. Exécuter après disparition des conflits
```

---

## 39.1 Pas de correction de l’état

Modifier manuellement :

```text
.sitefactory/generation-state.json
```

pour faire disparaître un conflit est interdit.

---

# 40. Remplacement forcé

## 40.1 Principe

Un remplacement forcé reste exceptionnel.

---

## 40.2 Portée

Une option `--force` éventuelle peut agir uniquement sur les fichiers :

```text
generated
```

Elle ne contourne jamais :

```text
create-once

merge conflict

custom

protected
```

---

## 40.3 Confirmation

Pour remplacer un fichier généré modifié, l’opérateur doit disposer :

- du plan ;
- du chemin exact ;
- du diff ;
- d’une confirmation explicite ;
- d’une capacité de rollback Git.

---

## 40.4 Mode non interactif

Une exécution non interactive doit nécessiter une confirmation spécifique, distincte d’une option générique.

Exemple conceptuel :

```text
--force
--confirm-generated-overwrite
```

La syntaxe définitive appartient à la spécification CLI.

---

## 40.5 Rapport

Chaque remplacement forcé doit être enregistré.

---

# 41. Suppression

## 41.1 `generated`

Suppression possible sous conditions de hash et de propriété.

---

## 41.2 `create-once`

Jamais supprimé automatiquement.

---

## 41.3 `merge`

Seuls les éléments possédés par SiteFactory peuvent être retirés.

---

## 41.4 `custom`

Jamais supprimé automatiquement.

---

## 41.5 `protected`

Jamais supprimé automatiquement.

---

## 41.6 Répertoire non vide

Un répertoire ne doit pas être supprimé s’il contient des éléments non possédés par SiteFactory.

---

# 42. Renommage

Un renommage généré est traité comme :

```text
create target
+ validate
+ delete source
```

---

## 42.1 Préconditions

La source ne peut être supprimée que si :

- elle appartient à SiteFactory ;
- son hash est inchangé ;
- la cible n’entre pas en conflit ;
- le plan présente le renommage.

---

## 42.2 Fichier modifié

Un fichier source modifié localement ne doit pas être renommé automatiquement.

---

# 43. Retrait d’un module

Le retrait d’un module peut provoquer :

- suppression de fichiers générés ;
- retrait d’éléments fusionnés ;
- conservation de fichiers `create-once` ;
- conservation des customizations ;
- conservation des données et migrations.

---

## 43.1 Données

Le retrait d’un module ne supprime jamais automatiquement ses données PostgreSQL.

---

## 43.2 Rapport

Les fichiers conservés devenus orphelins doivent être signalés.

---

# 44. Transition entre stratégies

Une stratégie ne doit pas changer silencieusement entre deux versions.

---

## 44.1 `generated` vers `create-once`

Possible comme transfert de propriété.

Le plan doit signaler que les futures mises à jour ne seront plus automatiques.

---

## 44.2 `create-once` vers `generated`

Nécessite :

- adoption explicite ;
- comparaison ;
- validation humaine ;
- baseline de hash ;
- absence de perte de modifications.

---

## 44.3 `custom` vers `generated`

Interdit par défaut.

Nécessite une migration et une décision explicites.

---

## 44.4 `generated` vers `protected`

Possible pour figer un fichier après génération.

Le changement doit être documenté.

---

## 44.5 `protected` vers une autre stratégie

Nécessite une commande ou migration dédiée.

---

# 45. Versionnement des merge handlers

Chaque merge handler possède un identifiant et une version.

Exemple :

```text
msbuild-central-packages-v1
```

---

## 45.1 Changement incompatible

Une modification incompatible du comportement produit une nouvelle version :

```text
msbuild-central-packages-v2
```

---

## 45.2 Migration

Le changement de handler peut nécessiter :

- plan ;
- état précédent ;
- migration ;
- tests golden ;
- action manuelle.

---

# 46. Déterminisme

Les mêmes entrées, versions et fichiers client doivent produire :

- le même plan ;
- les mêmes candidats générés ;
- les mêmes hashes ;
- les mêmes conflits ;
- le même rapport déterministe hors données explicitement dynamiques.

---

## 46.1 Ordre

Les opérations du plan sont ordonnées par chemin canonique.

---

## 46.2 Métadonnées dynamiques

Les timestamps et identifiants d’exécution ne doivent pas influencer le contenu fonctionnel généré.

---

# 47. Git

## 47.1 Génération initiale

La génération initiale cible :

- un dossier absent ;
- ou un dossier vide explicitement validé.

Git est initialisé après succès de la génération.

---

## 47.2 Régénération et upgrade

Le working tree doit être propre par défaut.

---

## 47.3 Working tree sale

Les commandes de plan peuvent fonctionner en lecture seule.

Les commandes d’application sont refusées si l’état Git est ambigu.

---

## 47.4 Branche dédiée

Un upgrade doit utiliser une branche telle que :

```text
upgrade/sitefactory-0.5.0
```

---

## 47.5 Git comme rollback

Git constitue le mécanisme principal de rollback du code et des fichiers versionnés.

Il ne remplace pas les backups de données.

---

# 48. Application atomique

## 48.1 Nouvelle génération

Pour un nouveau projet :

```text
génération dans un dossier temporaire
→ validation
→ promotion du dossier complet
```

---

## 48.2 Projet existant

Pour une régénération :

- sauvegarder temporairement les fichiers ciblés ;
- écrire les nouveaux fichiers de manière atomique ;
- restaurer l’état précédent en cas d’échec ;
- ne pas laisser un état partiellement promu.

---

## 48.3 Échec de rollback local

Un échec de restauration des fichiers constitue un incident bloquant.

Le rapport doit fournir la liste des chemins potentiellement affectés.

---

# 49. Candidats de conflit

Une commande future peut produire un bundle de résolution.

Exemple conceptuel :

```text
.sitefactory/conflicts/{runId}/
```

Il pourrait contenir :

- ancienne version générée ;
- version actuelle ;
- nouvelle version proposée ;
- métadonnées ;
- diff.

---

## 49.1 Contenu sensible

Le bundle ne doit pas inclure de secrets.

---

## 49.2 Versionnement Git

Le bundle de conflit reste temporaire et n’est pas committé par défaut.

---

# 50. Rapport de génération

Le rapport doit inclure pour chaque opération :

```text
Path

Strategy

Action

SourceComponent

SourceVersion

PreviousHash

CurrentHash

ProposedHash

Result

ConflictCode
```

---

## 50.1 Contenu interdit

Le rapport ne contient pas :

- contenu complet du fichier ;
- secret ;
- token ;
- mot de passe ;
- données personnelles ;
- clé privée.

---

# 51. Exemple de rapport

```json
{
  "schemaVersion": 1,
  "generationRunId": "6e9349aa-0b82-4c4b-97e4-74cae4658266",
  "status": "conflict",
  "operations": [
    {
      "path": "src/Client.Web/Generated/ModuleRegistry.g.cs",
      "strategy": "generated",
      "action": "conflict",
      "sourceComponent": "platform",
      "sourceVersion": "1.2.0",
      "conflictCode": "SF-GEN-FILE-004"
    },
    {
      "path": "README.md",
      "strategy": "create-once",
      "action": "preserve",
      "result": "success"
    }
  ]
}
```

---

# 52. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-GEN-FILE-001` | Chemin cible invalide |
| `SF-GEN-FILE-002` | Stratégie inconnue |
| `SF-GEN-FILE-003` | Plusieurs propriétaires exclusifs |
| `SF-GEN-FILE-004` | Fichier `generated` modifié localement |
| `SF-GEN-FILE-005` | Source de génération absente |
| `SF-GEN-FILE-006` | Merge handler absent |
| `SF-GEN-FILE-007` | Conflit de merge |
| `SF-GEN-FILE-008` | Tentative d’écriture dans une zone `custom` |
| `SF-GEN-FILE-009` | Tentative d’écriture dans une zone `protected` |
| `SF-GEN-FILE-010` | Suppression non autorisée |
| `SF-GEN-FILE-011` | Lien symbolique dangereux |
| `SF-GEN-FILE-012` | Collision de casse |
| `SF-GEN-FILE-013` | Hash incohérent |
| `SF-GEN-FILE-014` | État de génération absent ou invalide |
| `SF-GEN-FILE-015` | Application atomique échouée |
| `SF-GEN-FILE-016` | Restauration locale échouée |
| `SF-GEN-FILE-017` | Working tree non propre |
| `SF-GEN-FILE-018` | Merge binaire non supporté |
| `SF-GEN-FILE-019` | Transition de stratégie invalide |
| `SF-GEN-FILE-020` | Renommage non sécurisé |
| `SF-GEN-FILE-021` | Fichier généré orphelin modifié |
| `SF-GEN-FILE-022` | Confirmation de remplacement absente |
| `SF-GEN-FILE-023` | Élément fusionné de propriété inconnue |
| `SF-GEN-FILE-024` | Écriture hors de la racine cible |
| `SF-GEN-FILE-025` | Nom incompatible avec une plateforme supportée |

---

# 53. Observabilité

Événements recommandés :

```text
GenerationPlanStarted

GenerationPlanCompleted

FileCreatePlanned

FileUpdatePlanned

FileMergePlanned

FilePreserved

FileConflictDetected

FileDeletePlanned

GenerationApplyStarted

GenerationApplyCompleted

GenerationApplyFailed

GenerationRollbackStarted

GenerationRollbackCompleted
```

---

## 53.1 Propriétés autorisées

```text
GenerationRunId

Path

Strategy

Action

SourceComponent

SourceVersion

Result

ErrorCode
```

---

## 53.2 Données interdites

```text
FileContent

Secret

Token

PersonalData

ConnectionString
```

---

# 54. Tests unitaires

Tester chaque stratégie.

## 54.1 `generated`

- absent ;
- inchangé ;
- modifié ;
- mise à jour ;
- suppression ;
- remplacement forcé.

---

## 54.2 `create-once`

- absent ;
- présent inchangé ;
- présent modifié ;
- nouveau modèle disponible ;
- suppression demandée.

---

## 54.3 `merge`

- ajout SiteFactory ;
- ajout client ;
- modification indépendante ;
- modification concurrente ;
- retrait d’un élément SiteFactory ;
- élément inconnu ;
- ordre déterministe.

---

## 54.4 `custom`

- lecture autorisée lorsque nécessaire ;
- écriture refusée ;
- suppression refusée ;
- renommage refusé.

---

## 54.5 `protected`

- création refusée ;
- modification refusée ;
- suppression refusée ;
- `--force` refusé.

---

# 55. Tests d’intégration

Tester :

```text
génération initiale

régénération identique

régénération après modification generated

upgrade avec create-once modifié

upgrade avec merge compatible

upgrade avec merge conflictuel

retrait de module

renommage sécurisé

rollback après erreur d’écriture

working tree sale

état de génération corrompu
```

---

# 56. Tests de sécurité

Vérifier :

- path traversal ;
- chemin absolu ;
- lien symbolique ;
- collision de casse ;
- nom Windows réservé ;
- répertoire Git ;
- fichier hors racine ;
- manifest malveillant ;
- archive de template contenant un chemin sortant ;
- contenu sensible absent des rapports.

---

# 57. Tests multiplateformes

Les scénarios essentiels doivent être exécutés sur :

```text
Windows x64

Linux x64
```

Ils doivent vérifier :

- séparateurs ;
- casse ;
- noms réservés ;
- fins de ligne ;
- permissions ;
- atomicité des remplacements.

---

# 58. Golden tests

Les golden tests peuvent comparer :

```text
configuration
+ versions
+ état initial
→ plan attendu
→ projet attendu
→ état de génération attendu
```

---

## 58.1 Mise à jour

Une mise à jour des golden files doit être explicite et revue.

---

## 58.2 Pas d’acceptation globale aveugle

Une commande ne doit pas remplacer tous les golden files sans afficher les différences.

---

# 59. Tests des merge handlers

Chaque handler doit disposer de tests propres couvrant :

- parsing ;
- format invalide ;
- éléments client ;
- éléments SiteFactory ;
- conflits ;
- suppression ;
- ordre ;
- commentaires ;
- sérialisation ;
- déterminisme.

---

# 60. Tests d’atomicité

Simuler un échec :

- après la création d’un fichier ;
- pendant un remplacement ;
- pendant une suppression ;
- pendant l’écriture de l’état ;
- pendant le rapport.

Le projet doit revenir à l’état antérieur ou produire un diagnostic indiquant précisément les chemins non restaurés.

---

# 61. Tests de transition de stratégie

Tester :

```text
generated → create-once

generated → protected

create-once → generated avec adoption

custom → generated refusé

protected → generated refusé
```

---

# 62. Quality gates

Une génération ou un upgrade est bloqué lorsque :

```text
Stratégie absente

Propriété ambiguë

Chemin non sécurisé

Fichier generated modifié

Merge conflictuel

Écriture custom

Écriture protected

Working tree ambigu

État invalide

Suppression non autorisée

Transition de stratégie non validée

Rapport impossible
```

---

## 62.1 Après application

L’opération n’est pas déclarée réussie si :

```text
Hash final inattendu

État non écrit

Build requis échoué

Tests requis échoués

Rollback local incomplet

Rapport absent
```

---

# 63. Conséquences positives

La décision apporte :

- protection explicite des customizations ;
- capacité de mise à niveau ;
- absence d’écrasement silencieux ;
- propriété claire des fichiers ;
- conflits détectables ;
- suppression maîtrisée ;
- meilleure réversibilité ;
- plans lisibles ;
- rapports auditables ;
- extension par modules ;
- compatibilité avec Git ;
- base solide pour `upgrade apply`.

---

# 64. Conséquences négatives

La décision entraîne :

- manifests plus complexes ;
- fichier d’état supplémentaire ;
- nombreux tests ;
- merge handlers spécialisés ;
- gestion de conflits ;
- opérations de mise à niveau plus longues ;
- besoin de former les opérateurs ;
- transitions de stratégie à maintenir ;
- complexité d’application atomique ;
- risque d’état corrompu.

---

# 65. Risques acceptés

## 65.1 Conflits manuels

Certaines personnalisations nécessiteront une résolution humaine.

---

## 65.2 État obsolète

Une modification manuelle du fichier d’état peut perturber la détection.

---

## 65.3 Merge handlers

Un handler peut introduire un défaut sémantique.

---

## 65.4 Complexité

La solution est plus complexe qu’un générateur écrasant tout.

Cette complexité est acceptée afin de protéger les projets clients.

---

# 66. Mesures compensatoires

- manifests validés ;
- hashes SHA-256 ;
- état versionné ;
- plans sans effet de bord ;
- working tree propre ;
- branches dédiées ;
- merge handlers versionnés ;
- golden tests ;
- tests multiplateformes ;
- application atomique ;
- rapports ;
- règles Codex ;
- confirmations ciblées.

---

# 67. Sécurité

## 67.1 Actifs

- code client ;
- configurations ;
- migrations ;
- secrets locaux ;
- documents ;
- scripts ;
- fichiers de déploiement.

---

## 67.2 Menaces

- path traversal ;
- écrasement de fichier ;
- suppression de données ;
- symlink ;
- manifest compromis ;
- archive malveillante ;
- collision de chemin ;
- fuite de secret dans un rapport ;
- exécution d’un fichier généré falsifié.

---

## 67.3 Contrôles

- chemins normalisés ;
- racine imposée ;
- stratégies explicites ;
- hashes ;
- état ;
- plans ;
- permissions ;
- absence d’exécution pendant la génération ;
- rapports sans contenu ;
- scan des packages et templates.

---

# 68. Performance

## 68.1 Hashes

Le calcul de hash reste acceptable pour les tailles prévues du repository.

---

## 68.2 Optimisation future

Un cache peut être ajouté si les mesures démontrent un coût important.

Il ne doit pas réduire la fiabilité de détection.

---

## 68.3 Gros fichiers

Les fichiers volumineux ou médias ne doivent pas être intégrés inutilement au pipeline de génération de code.

---

# 69. Coûts

## 69.1 Coûts supplémentaires

- développement de l’état ;
- merge handlers ;
- tests ;
- rapports ;
- conflits ;
- maintenance.

---

## 69.2 Coûts évités

- perte de customizations ;
- upgrades manuels complets ;
- incidents clients ;
- duplication de projets ;
- impossibilité de maintenir le socle ;
- restauration de fichiers supprimés ;
- dérive silencieuse.

---

# 70. Maintenabilité

La décision rend visibles les frontières entre :

```text
code générique

code généré

code partagé

code client

fichiers sensibles
```

Elle évite les conditions implicites dans le générateur.

---

## 70.1 Nouveaux formats

Un nouveau format de merge nécessite un handler dédié.

---

## 70.2 Pas de framework universel

SiteFactory ne doit pas construire un moteur universel de fusion de tous les formats.

---

# 71. Compatibilité

## 71.1 Schéma d’état

Le fichier d’état possède :

```text
schemaVersion
```

---

## 71.2 Ancien générateur

Un ancien générateur rencontrant une version d’état future doit refuser avant toute écriture.

---

## 71.3 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

Le changement futur de stratégie d’un fichier peut constituer une migration incompatible.

---

# 72. Migration

## 72.1 Projets existants sans état

Un projet existant sans état de génération nécessite une procédure d’adoption.

---

## 72.2 Adoption

Le processus doit :

1. inventorier les fichiers ;
2. comparer avec une génération de référence ;
3. classer chaque chemin ;
4. identifier les modifications client ;
5. produire un plan ;
6. obtenir une validation humaine ;
7. créer l’état initial.

---

## 72.3 Pas d’adoption aveugle

SiteFactory ne doit pas considérer automatiquement tous les fichiers identiques au template courant comme sa propriété sans rapport.

---

## 72.4 Production

```text
Production execution: out of scope
```

Cette décision concerne le repository et non les opérations de production.

---

# 73. Réversibilité

## 73.1 Classification

```text
élevée
```

Les modifications sont versionnées et peuvent être annulées par Git.

---

## 73.2 Limite

La suppression forcée d’un fichier modifié réduit la réversibilité.

Elle nécessite une confirmation et un historique Git exploitable.

---

## 73.3 Changement de modèle

Un retour vers un générateur écrasant tout nécessiterait un nouvel ADR et constituerait une réduction majeure des garanties.

---

# 74. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `FileGenerationStrategy` ;
- `FileOwnership` ;
- `FileOperation` ;
- erreurs ;
- manifests.

---

## Étape 2 — État

Créer :

```text
.sitefactory/generation-state.json
```

et son schéma.

---

## Étape 3 — Inspection

Ajouter :

- lecture des fichiers ;
- hashes ;
- résolution des règles ;
- collisions ;
- sécurité des chemins.

---

## Étape 4 — Planification

Produire :

- actions ;
- conflits ;
- suppressions ;
- actions manuelles.

---

## Étape 5 — Stratégies simples

Implémenter :

```text
generated
create-once
custom
protected
```

---

## Étape 6 — Merge

Ajouter les premiers handlers nécessaires :

```text
Directory.Packages.props

.slnx
```

---

## Étape 7 — Application atomique

Ajouter :

- staging ;
- sauvegarde temporaire ;
- promotion ;
- rollback.

---

## Étape 8 — Rapports

Étendre :

```text
generation-report.json

upgrade-report.json
```

---

## Étape 9 — Tests

Ajouter :

- unitaires ;
- intégration ;
- sécurité ;
- golden ;
- multiplateformes.

---

# 75. Tâches liées proposées

```text
SF-TASK-038 — Define file generation strategies

SF-TASK-039 — Define file ownership manifest schema

SF-TASK-040 — Add generation state schema

SF-TASK-041 — Implement secure target path validation

SF-TASK-042 — Implement generated file handling

SF-TASK-043 — Implement create-once file handling

SF-TASK-044 — Protect custom and protected paths

SF-TASK-045 — Implement MSBuild package merge handler

SF-TASK-046 — Implement .slnx merge handler

SF-TASK-047 — Add generation conflict reporting

SF-TASK-048 — Add atomic file application and rollback

SF-TASK-049 — Add multi-platform file strategy tests
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 76. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Un fichier generated inchangé peut être mis à jour.

Un fichier generated modifié produit un conflit.

Un fichier create-once existant est conservé.

Un fichier custom n’est jamais écrit.

Un fichier protected n’est jamais écrit, même avec force.

Un merge conserve les éléments client.

Un conflit de merge est signalé.

Un fichier generated retiré et inchangé peut être supprimé.

Un fichier modified retiré n’est pas supprimé.

Un path traversal est refusé.

Un symlink sortant est refusé.

Une collision de casse est refusée.

L’état de génération est versionné.

Le plan ne modifie aucun fichier.

Une application échouée restaure l’état initial.

Les rapports ne contiennent aucun secret.

Les résultats sont identiques sur Windows et Linux.
```

---

# 77. Conditions de révision

Réexaminer cette décision si :

- le nombre de conflits rend les upgrades impraticables ;
- les merge handlers deviennent trop nombreux ;
- un moteur de patch structurel mature répond mieux au besoin ;
- les projets clients ne modifient jamais les fichiers générés ;
- une interface graphique de résolution est introduite ;
- Git worktree devient la stratégie principale de staging ;
- les fichiers générés deviennent majoritairement du code source modifié manuellement ;
- les performances de hashing deviennent problématiques ;
- la gestion des symlinks devient un besoin réel ;
- un autre modèle de personnalisation remplace les repositories clients.

---

# 78. Indicateurs à suivre

- nombre de fichiers par stratégie ;
- nombre de conflits par upgrade ;
- fichiers générés modifiés manuellement ;
- durée de planification ;
- durée de hashing ;
- nombre de rollbacks locaux ;
- taux de réussite des merges ;
- nombre d’actions manuelles ;
- nombre de fichiers orphelins ;
- transitions de stratégie ;
- incidents d’écrasement ;
- différences Windows/Linux.

---

# 79. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des fichiers client sont écrasés

des fichiers protected sont modifiés

des conflits sont ignorés

des états sont régulièrement édités manuellement

des merges produisent des fichiers invalides

des upgrades génèrent des diffs massifs

des suppressions automatiques retirent du contenu client

des projets Windows et Linux divergent

des rollbacks locaux échouent
```

---

# 80. Conséquences observées

Cette section est append-only après mise en œuvre.

## 80.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation du moteur
de propriété et de stratégie des fichiers.
```

Les observations futures devront préciser :

- date ;
- version ;
- projet de référence ;
- stratégie concernée ;
- conflit ou résultat ;
- action proposée.

---

# 81. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `15-cli-specification.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0002-yaml-client-configuration.md`

---

# 82. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 77 |

---

# 83. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 84. Synthèse de la décision

```text
generated
→ propriété SiteFactory
→ remplacement si hash inchangé
→ conflit si modification locale

create-once
→ création initiale
→ propriété transférée au client
→ aucune mise à jour automatique

merge
→ propriété partagée
→ handler structuré et versionné
→ aucun merge textuel générique

custom
→ propriété exclusive du client
→ aucune écriture ou suppression automatique

protected
→ zone gardée
→ aucune écriture, même avec force

Toute génération
→ inspection
→ plan
→ conflits
→ staging
→ validation
→ application contrôlée
→ rapport
```

> SiteFactory doit être capable de faire évoluer ce qu’il possède sans altérer ce que le client possède. La stratégie d’un fichier constitue donc un contrat de propriété, pas un simple détail du générateur.
