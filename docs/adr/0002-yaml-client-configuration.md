---
adrId: ADR-0002
title: Use YAML for client-authored configuration and JSON for generated artifacts
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
  - ./0006-feature-flags-and-entitlements-separation.md
  - ../03-generation/13-generation-pipeline.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/09-module-architecture.md
  - ../03-generation/14-client-configuration-schema.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/18-security-baseline.md
  - ../04-quality-security/19-testing-strategy.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
targetVersion: 0.1.0
---

# ADR-0002 — YAML pour la configuration client et JSON pour les artefacts générés

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La [configuration V1 actuelle](../03-generation/14-client-configuration-schema.md)
est le YAML strict `client.sitefactory.yml`, sans secret. Les sorties disponibles
sont `sitefactory.lock.json`, `generation-report.json` et
`.sitefactory/generation-state.json` ; les autres manifests/rapports décrits
ci-dessous restent cibles. La [CLI publique](../03-generation/initial-generation-cli.md)
expose `validate --config <path>`, `plan --config <path>` et
`generate --config <path> --output <directory>`.
Les exemples enrichis de packs, de thèmes et de fluent API sont conceptuels,
non un schéma V1 accepté ou une API livrée ; `sample-*` désigne des valeurs
fictives, pas des offres propriétaires ou des modules enregistrés.
La contribution publique réelle est DocumentationModule, composée explicitement.

## 1. Statut

```text
accepted
```

Cette décision définit les formats de référence suivants :

```text
Configuration écrite et maintenue par un humain
→ YAML

Artefacts générés et consommés par des outils
→ JSON
```

Le fichier de configuration client canonique est :

```text
client.sitefactory.yml
```

Les principaux artefacts générés restent notamment :

```text
sitefactory.lock.json
generation-report.json
upgrade-report.json
deployment-report.json
backup-manifest.json
```

---

## 2. Résumé

LogicGate SiteFactory utilisera YAML comme format déclaratif pour la configuration source d’un projet client.

Cette configuration sera :

- lisible ;
- commentable ;
- versionnable dans Git ;
- validée avant toute génération ;
- dépourvue de secrets réels ;
- limitée à un sous-ensemble sûr et déterministe de YAML.

Les fichiers générés par SiteFactory et destinés à être consommés principalement par des outils utiliseront JSON.

Cette séparation permet de distinguer clairement :

```text
l’intention humaine
```

de :

```text
l’état résolu par la machine
```

Le YAML ne sera pas utilisé comme langage de programmation, système de template ou mécanisme implicite de fusion de fichiers.

---

## 3. Contexte

SiteFactory doit transformer une configuration déclarative en projet client isolé, testé, sécurisé et déployable.

La configuration doit pouvoir décrire notamment :

- l’identité technique du client ;
- le pack retenu ;
- le template ;
- le thème ;
- les modules ;
- les options ;
- les entitlements ;
- les paramètres de contenu structurel ;
- le profil de déploiement ;
- les domaines ;
- les fonctionnalités activées ;
- certaines références de secrets ;
- les paramètres nécessaires à la génération.

Cette configuration sera principalement maintenue par LogicGate.

Elle pourra également être :

- relue avec le client ;
- auditée ;
- modifiée lors d’un upgrade ;
- utilisée par Codex ;
- comparée dans une pull request ;
- archivée avec le repository client.

Le projet ne cherche pas à construire un outil public no-code.

La configuration doit donc privilégier :

- la précision ;
- la lisibilité technique ;
- la revue Git ;
- la stabilité ;
- la validation ;
- la sécurité ;

plutôt qu’une interface graphique destinée à masquer entièrement le modèle technique.

---

## 4. Problème à résoudre

Quel format utiliser comme source déclarative d’un projet client afin qu’il soit :

- suffisamment lisible pour un opérateur humain ;
- adapté aux structures imbriquées ;
- versionnable dans Git ;
- commentable ;
- validable par un schéma ;
- exploitable de manière déterministe par un CLI .NET ;
- sécurisé contre les fonctionnalités YAML dangereuses ou ambiguës ;
- distinct des artefacts calculés par SiteFactory ?

La solution doit également éviter :

- la duplication de configuration ;
- les valeurs implicites difficiles à comprendre ;
- les secrets dans Git ;
- les fusions cachées ;
- les comportements dépendant de l’ordre des clés ;
- les modifications automatiques silencieuses du fichier source.

---

## 5. Portée

Cette décision couvre :

- `client.sitefactory.yml` ;
- la syntaxe YAML autorisée ;
- le chargement de la configuration ;
- la validation syntaxique ;
- la validation structurelle ;
- la validation métier ;
- la normalisation ;
- le calcul d’empreinte ;
- les erreurs de configuration ;
- le versionnement du schéma ;
- les migrations de configuration ;
- les exemples de configuration ;
- les artefacts JSON dérivés ;
- les commandes CLI associées.

---

## 6. Hors portée

Cette décision ne définit pas :

- l’intégralité des propriétés du schéma client ;
- la bibliothèque YAML .NET exacte ;
- l’interface graphique d’un futur configurateur ;
- le stockage runtime des contenus éditoriaux ;
- la gestion des secrets en production ;
- le format interne des migrations EF Core ;
- le format des catalogues source si ceux-ci nécessitent une décision distincte ;
- un système général d’inclusion ou de templating YAML ;
- un langage de script ;
- une configuration multi-tenant ;
- une fusion automatique de plusieurs fichiers de configuration.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Lisibilité pour un opérateur humain**
2. **Validation stricte avant toute écriture**
3. **Diffs Git compréhensibles**
4. **Possibilité d’ajouter des commentaires**
5. **Support naturel des structures imbriquées**
6. **Déterminisme de la génération**
7. **Sécurité du parsing**
8. **Compatibilité avec un modèle typé .NET**
9. **Versionnement explicite du schéma**
10. **Facilité de migration**
11. **Séparation entre intention et état résolu**
12. **Compatibilité Windows et Linux**
13. **Outillage raisonnable dans l’écosystème .NET**
14. **Capacité future à générer ou éditer la configuration**

---

## 8. Contraintes

La décision doit respecter les contraintes suivantes :

```text
.NET 10

CLI interne

Repository Git par client

Configuration versionnée

Aucun secret réel dans Git

Validation avant planification

Validation avant écriture

Compatibilité Windows et Linux

Génération déterministe

Rapports structurés

Schéma versionné

Aucune exécution de code depuis la configuration
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- la configuration restera de taille raisonnable ;
- elle sera principalement modifiée par LogicGate ou un opérateur technique ;
- les contenus très volumineux resteront en dehors de ce fichier ;
- les secrets seront injectés par l’environnement ou un coffre séparé ;
- les modules et templates posséderont des identifiants stables ;
- un schéma machine-readable accompagnera la documentation ;
- le repository client restera la source de vérité de la configuration structurelle ;
- les artefacts générés pourront être recréés depuis la configuration et les versions verrouillées.

---

# 10. Option A — JSON pour la configuration et les artefacts

## 10.1 Description

Le fichier source client et tous les artefacts générés utilisent JSON.

Exemple :

```json
{
  "schemaVersion": 1,
  "client": {
    "slug": "sample-project"
  }
}
```

---

## 10.2 Avantages

- format strict ;
- très bon support machine ;
- parsing largement disponible ;
- validation par JSON Schema directe ;
- ambiguïtés syntaxiques limitées ;
- structure identique entre entrées et sorties ;
- sérialisation .NET native.

---

## 10.3 Inconvénients

- absence de commentaires standards ;
- bruit syntaxique important ;
- virgules et guillemets nombreux ;
- moins confortable pour une édition manuelle régulière ;
- diffs plus difficiles à lire sur des configurations longues ;
- risque d’utiliser des fichiers séparés pour expliquer les valeurs.

---

## 10.4 Risques

- commentaires déplacés dans des documents non synchronisés ;
- configuration moins accessible lors des revues ;
- tentation de modifier uniquement les artefacts générés ;
- répétition de champs descriptifs pour remplacer les commentaires.

---

## 10.5 Réversibilité

```text
élevée
```

La structure logique resterait proche du modèle YAML retenu.

---

# 11. Option B — YAML pour la configuration et JSON pour les artefacts

## 11.1 Description

Le fichier écrit par un humain utilise YAML.

Les fichiers générés et consommés principalement par les outils utilisent JSON.

Exemple :

```yaml
schemaVersion: 1

client:
  slug: sample-project

product:
  packId: sample-pack
```

---

## 11.2 Avantages

- bonne lisibilité ;
- commentaires ;
- structure imbriquée concise ;
- diffs Git généralement lisibles ;
- adaptation naturelle à une configuration déclarative ;
- séparation claire entre source humaine et résultat machine ;
- compatibilité conceptuelle avec JSON Schema ;
- format connu des outils d’infrastructure.

---

## 11.3 Inconvénients

- syntaxe sensible à l’indentation ;
- fonctionnalités avancées pouvant créer des ambiguïtés ;
- types implicites historiques ;
- risque de clés dupliquées ;
- risque d’expansion d’aliases ;
- parsing sécurisé nécessitant des restrictions explicites ;
- sérialisation moins native que JSON dans .NET.

---

## 11.4 Risques

- erreurs d’indentation ;
- interprétation inattendue d’une valeur ;
- utilisation d’anchors ou tags ;
- configuration excessivement complexe ;
- comportements différents selon les parsers ;
- confusion entre absence, chaîne vide et valeur nulle.

---

## 11.5 Réversibilité

```text
élevée
```

Le modèle typé peut être sérialisé vers un autre format si nécessaire.

---

# 12. Option C — TOML pour la configuration et JSON pour les artefacts

## 12.1 Description

La configuration humaine utilise TOML.

---

## 12.2 Avantages

- syntaxe relativement stricte ;
- bonne lisibilité pour des configurations simples ;
- commentaires ;
- types plus prévisibles que certains dialectes YAML ;
- format adapté à des paramètres plats ou moyennement imbriqués.

---

## 12.3 Inconvénients

- structures imbriquées et listes d’objets moins naturelles ;
- configuration de modules plus difficile à représenter ;
- écosystème et outillage moins répandus dans le contexte retenu ;
- mapping vers des compositions complexes moins lisible ;
- courbe d’apprentissage supplémentaire.

---

## 12.4 Risques

- schéma client devenant difficile à lire avec de nombreux modules ;
- besoin de conventions spécifiques ;
- outillage de validation supplémentaire ;
- faible bénéfice par rapport au YAML restreint.

---

## 12.5 Réversibilité

```text
élevée
```

---

# 13. Option D — DSL C# ou configuration exécutable

## 13.1 Description

La configuration est exprimée par du code C#.

Exemple conceptuel :

```csharp
SiteFactoryProject.Create()
    .ForClient("sample-project")
    .UsePack("sample-pack");
```

---

## 13.2 Avantages

- typage à la compilation ;
- autocomplétion ;
- refactoring ;
- expressivité ;
- réutilisation de code.

---

## 13.3 Inconvénients

- configuration exécutable ;
- nécessité de compiler ;
- surface d’attaque plus élevée ;
- moins accessible pour la revue non développeur ;
- risque de logique métier cachée ;
- déterminisme plus difficile à garantir ;
- séparation floue entre configuration et personnalisation ;
- migrations de format plus complexes.

---

## 13.4 Risques

- accès système ou réseau pendant l’évaluation ;
- comportement conditionnel ;
- configuration impossible à analyser sans exécution ;
- génération différente selon l’environnement ;
- code client injecté dans le pipeline de génération.

---

## 13.5 Réversibilité

```text
faible à moyenne
```

Une configuration exécutable est plus difficile à migrer automatiquement vers un modèle purement déclaratif.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | JSON | YAML + JSON | TOML + JSON | DSL C# |
|---|---:|---:|---:|---:|---:|
| Lisibilité humaine | 5 | 3 | 5 | 4 | 3 |
| Commentaires | 4 | 1 | 5 | 5 | 5 |
| Structures imbriquées | 5 | 5 | 5 | 3 | 5 |
| Validation stricte | 5 | 5 | 4 | 4 | 4 |
| Sécurité du format | 5 | 5 | 3 | 4 | 1 |
| Déterminisme | 5 | 5 | 4 | 5 | 2 |
| Diff Git | 4 | 3 | 5 | 4 | 3 |
| Simplicité opérateur | 4 | 3 | 5 | 4 | 2 |
| Outillage .NET | 3 | 5 | 4 | 3 | 5 |
| Séparation configuration/code | 5 | 5 | 5 | 5 | 1 |
| Évolutivité du schéma | 4 | 5 | 5 | 4 | 3 |

L’option YAML pour la source humaine et JSON pour les artefacts machine offre le meilleur équilibre pour le contexte SiteFactory.

---

# 15. Décision

Nous décidons d’utiliser :

```text
YAML
```

pour la configuration source maintenue par un humain :

```text
client.sitefactory.yml
```

Nous décidons d’utiliser :

```text
JSON
```

pour les artefacts générés, normalisés ou principalement consommés par des outils.

---

## 15.1 Fichier canonique

Le fichier canonique d’un projet client est :

```text
client.sitefactory.yml
```

Les extensions alternatives suivantes ne sont pas utilisées comme source canonique :

```text
.yaml
.json
.toml
.cs
```

---

## 15.2 Source de vérité

Le fichier YAML décrit l’intention validée du projet.

Le lock file décrit la résolution exacte des versions et composants.

Exemple :

```text
client.sitefactory.yml
→ ce qui est demandé

sitefactory.lock.json
→ ce qui a été résolu
```

---

## 15.3 Artefacts machine

Les artefacts suivants restent en JSON :

```text
sitefactory.lock.json

generation-plan.json

generation-report.json

upgrade-report.json

deployment-report.json

backup-manifest.json

restore-report.json

handover-report.json
```

La liste exacte dépend des fonctionnalités implémentées.

---

# 16. Justification

YAML est retenu pour la configuration humaine parce qu’il fournit :

- une syntaxe concise ;
- des commentaires ;
- une bonne lisibilité des structures imbriquées ;
- des diffs Git exploitables ;
- une représentation naturelle des listes de modules ;
- une séparation claire avec les artefacts calculés.

JSON reste retenu pour les sorties machine parce qu’il fournit :

- une sérialisation déterministe plus simple ;
- un support natif large ;
- une consommation directe par les outils ;
- une validation et une inspection prévisibles ;
- une absence de fonctionnalités telles que les anchors et tags.

Les risques propres à YAML sont acceptés uniquement avec un sous-ensemble strict, des limites de parsing et une validation obligatoire.

---

# 17. Exemple conceptuel

L’exemple suivant illustre le format, sans remplacer le schéma officiel défini dans :

```text
14-client-configuration-schema.md
```

```yaml
schemaVersion: 1

client:
  slug: sample-project
  displayName: Sample Project

product:
  packId: sample-pack

template:
  id: sample-template

theme:
  id: sample-theme

modules:
  - id: sample-feedback
    enabled: true

  - id: sample-navigation
    enabled: true

deployment:
  profileId: docker-compose-vps
  productionDomain: sample-project.example.test
```

---

## 17.1 Commentaires

Les commentaires sont autorisés :

```yaml
modules:
  # Requis pour le formulaire public de contact.
  - id: sample-feedback
    enabled: true
```

Ils ne font pas partie du modèle sémantique et ne sont pas copiés dans les artefacts JSON.

---

# 18. Sous-ensemble YAML autorisé

SiteFactory n’accepte pas toutes les capacités possibles de YAML.

Le sous-ensemble autorisé comprend :

- un document unique ;
- mappings simples ;
- séquences ;
- chaînes ;
- nombres lorsque le schéma les attend ;
- booléens explicites ;
- valeur `null` explicite lorsque le schéma l’autorise ;
- commentaires ;
- chaînes multilignes pour les champs de type chaîne ;
- caractères Unicode valides.

---

# 19. Capacités YAML interdites

Sont interdites :

```text
anchors

aliases

merge keys

custom tags

type tags

complex keys

multiple YAML documents

external includes

dynamic imports

executable expressions

environment interpolation

template expressions

object activation

duplicate keys
```

Exemples refusés :

```yaml
defaults: &defaults
  enabled: true

module:
  <<: *defaults
```

```yaml
value: !customType something
```

```yaml
---
schemaVersion: 1
---
schemaVersion: 2
```

---

## 19.1 Raisons

Ces fonctionnalités sont interdites afin de réduire :

- les ambiguïtés ;
- les attaques par expansion ;
- les différences de parser ;
- les dépendances implicites ;
- les difficultés d’audit ;
- les effets cachés de fusion ;
- la complexité de migration.

---

# 20. Document unique

Le fichier doit représenter exactement un document YAML.

Les marqueurs de document multiples sont refusés.

Un marqueur initial unique :

```yaml
---
```

peut être toléré par le parser, mais il n’est pas requis ni recommandé dans les exemples SiteFactory.

---

# 21. Clés dupliquées

Les clés dupliquées sont toujours refusées.

Exemple invalide :

```yaml
client:
  slug: sample-project
  slug: sample-secondary
```

SiteFactory ne doit pas accepter silencieusement :

- la première valeur ;
- la dernière valeur ;
- une fusion.

---

# 22. Casse et convention des propriétés

Les propriétés du schéma utilisent :

```text
lowerCamelCase
```

Exemples :

```text
schemaVersion
displayName
packId
profileId
productionDomain
```

Les identifiants utilisent généralement :

```text
kebab-case
```

Exemples :

```text
sample-project
sample-pack
sample-template
docker-compose-vps
```

---

# 23. Types scalaires

## 23.1 Booléens

Seules les valeurs explicites suivantes sont utilisées :

```yaml
enabled: true
enabled: false
```

Les formes historiques ambiguës ne doivent pas être interprétées comme booléens :

```text
yes
no
on
off
```

Elles doivent être :

- traitées comme chaînes ;
- ou refusées par le schéma ;

selon le champ concerné.

---

## 23.2 Valeurs nulles

La valeur recommandée est :

```yaml
value: null
```

La forme abrégée :

```yaml
value: ~
```

est refusée afin d’éviter les variantes inutiles.

Une valeur nulle n’est acceptée que lorsque le schéma l’autorise.

---

## 23.3 Nombres

Les nombres sont utilisés uniquement pour des valeurs réellement numériques :

```yaml
pageSize: 15
timeoutSeconds: 30
```

Les identifiants, codes postaux, numéros de téléphone et versions restent des chaînes.

---

## 23.4 Versions

Les versions sont toujours des chaînes :

```yaml
version: "1.2.0"
```

Les guillemets sont recommandés.

---

## 23.5 Dates et heures

Les dates et heures sont exprimées sous forme de chaînes explicites.

Exemple :

```yaml
effectiveAtUtc: "2026-07-28T10:00:00Z"
```

Le parser ne doit pas convertir automatiquement un scalaire en objet date sans contrat de schéma.

---

## 23.6 Chaînes ambiguës

Une chaîne ressemblant à :

- un booléen ;
- un nombre ;
- une date ;
- une valeur nulle ;

doit être entourée de guillemets lorsqu’elle représente du texte.

Exemple :

```yaml
postalCode: "01230"
version: "1.0"
answer: "no"
```

---

# 24. Chaînes multilignes

Les chaînes multilignes YAML sont autorisées uniquement pour les propriétés de type chaîne.

Exemple :

```yaml
description: |
  Cabinet de conseil spécialisé dans
  l’accompagnement des PME.
```

Le schéma reste responsable :

- de la longueur maximale ;
- du caractère obligatoire ou facultatif ;
- de l’utilisation du contenu.

---

## 24.1 Contenu volumineux

Le fichier client ne doit pas devenir le stockage principal de :

- longs articles ;
- nombreuses réalisations ;
- bibliothèque média ;
- documents ;
- contenus administrables volumineux.

Ces données relèvent du repository de contenu, de PostgreSQL ou d’`App_Data` selon leur nature.

---

# 25. Ordre des propriétés

L’ordre des propriétés YAML n’a aucune signification métier.

Deux configurations contenant les mêmes valeurs mais des clés dans un ordre différent doivent produire le même modèle normalisé.

L’ordre peut néanmoins être standardisé dans les exemples pour améliorer la lecture :

```text
schemaVersion

client

product

template

theme

modules

features

content

seo

deployment

operations
```

---

# 26. Valeurs par défaut

## 26.1 Valeur explicite ou défaut documenté

Toute valeur par défaut doit être :

- définie par le schéma ou le catalogue ;
- documentée ;
- stable pour une version donnée ;
- visible dans le plan de génération.

---

## 26.2 Pas de défaut caché

Le générateur ne doit pas choisir silencieusement une valeur dépendant :

- de la machine ;
- du système d’exploitation ;
- de l’utilisateur ;
- de la date ;
- du réseau ;
- de la version la plus récente disponible.

---

## 26.3 Matérialisation

Le plan ou le lock file doit matérialiser les valeurs résolues importantes, même lorsqu’elles proviennent d’un défaut.

---

# 27. Propriétés inconnues

La validation est stricte par défaut.

Une propriété inconnue doit produire une erreur.

Exemple :

```yaml
client:
  slgu: sample-project
```

Le système ne doit pas ignorer silencieusement la faute.

---

## 27.1 Extension points

Les extensions futures doivent être déclarées dans des zones explicitement prévues par le schéma.

Exemple conceptuel :

```yaml
extensions:
  my-extension:
    option: value
```

L’existence et le contrat d’une telle zone restent définis par le schéma officiel.

---

# 28. Secrets

## 28.1 Interdiction

Le fichier YAML ne doit pas contenir de valeur réelle pour :

- mot de passe PostgreSQL ;
- token ;
- clé API ;
- mot de passe SMTP ;
- clé privée ;
- secret GitHub ;
- credential de backup ;
- cookie ;
- chaîne de connexion complète.

---

## 28.2 Références logiques

Lorsque nécessaire, la configuration peut contenir un identifiant logique de secret.

Exemple conceptuel :

```yaml
smtp:
  credentialReference: smtp-production
```

La syntaxe exacte appartient au schéma officiel.

---

## 28.3 Résolution

La résolution d’un secret se produit :

- au déploiement ;
- au runtime ;
- ou dans une opération explicitement autorisée.

Elle ne se produit pas pendant une simple validation de configuration.

---

## 28.4 Rapports

Les rapports ne doivent jamais recopier la valeur résolue d’un secret.

---

# 29. Variables d’environnement

Le MVP ne réalise pas d’interpolation implicite telle que :

```yaml
password: ${POSTGRES_PASSWORD}
```

dans le fichier canonique.

---

## 29.1 Raisons

L’interpolation implicite rendrait plus difficile de connaître :

- la valeur réellement validée ;
- le caractère déterministe de la configuration ;
- l’origine d’une erreur ;
- la différence entre deux environnements ;
- le contenu exact du hash sémantique.

---

## 29.2 Configuration runtime

Les variables d’environnement restent utilisables dans :

- Docker Compose ;
- configuration ASP.NET Core ;
- secrets d’environnement ;
- scripts de déploiement.

Elles ne font pas partie du langage YAML SiteFactory.

---

# 30. Fusion de fichiers

Le MVP ne charge pas automatiquement :

```text
client.sitefactory.yml

client.sitefactory.production.yml

client.sitefactory.local.yml

client.sitefactory.override.yml
```

dans une fusion implicite.

---

## 30.1 Raisons

Les fusions implicites compliquent :

- la compréhension de la configuration finale ;
- le calcul du hash ;
- la revue ;
- les erreurs ;
- les migrations ;
- la reproductibilité.

---

## 30.2 Environnements

Les différences d’environnement doivent être représentées explicitement par :

- le schéma ;
- une commande ;
- un profil ;
- ou un mécanisme futur documenté.

Elles ne doivent pas dépendre d’une convention de fichiers chargés silencieusement.

---

# 31. Includes

Le YAML SiteFactory ne possède pas de directive :

```text
include
import
extends
```

dans le MVP.

---

## 31.1 Réutilisation

La réutilisation s’effectue par :

- catalogues ;
- templates ;
- thèmes ;
- modules ;
- valeurs par défaut versionnées ;
- génération ;

et non par copie ou inclusion de fragments YAML externes.

---

# 32. Taille et complexité du fichier

Le parser doit appliquer des limites configurées.

Baseline proposée :

```text
Taille maximale du fichier : 1 MiB

Profondeur maximale : limitée

Nombre de nœuds : limité

Longueur des chaînes : validée par champ

Nombre d’éléments dans une collection : validé par champ
```

Les valeurs exactes d’implémentation doivent être centralisées et testées.

---

## 32.1 Dépassement

Un dépassement doit produire une erreur contrôlée avant la construction complète du modèle.

---

# 33. Encodage

Le fichier utilise :

```text
UTF-8
```

Un BOM peut être toléré à la lecture si la bibliothèque le gère correctement, mais les exemples et fichiers générés par SiteFactory utilisent UTF-8 sans BOM.

---

## 33.1 Fins de ligne

Les fins de ligne ne modifient pas le sens de la configuration.

Git et l’outillage doivent éviter les changements massifs de fins de ligne entre Windows et Linux.

---

# 34. Architecture du chargement

Le flux de chargement est :

```text
Lecture bornée du fichier
→ parsing YAML sûr
→ modèle syntaxique
→ validation structurelle
→ mapping vers modèle typé
→ validation métier
→ validation inter-composants
→ normalisation
→ planification
```

---

## 34.1 Séparation des responsabilités

Composants conceptuels :

```text
ConfigurationFileReader

YamlConfigurationParser

ConfigurationSchemaValidator

ConfigurationMapper

ConfigurationSemanticValidator

ConfigurationCompatibilityValidator

ConfigurationNormalizer
```

Les noms définitifs pourront évoluer pendant l’implémentation.

---

# 35. Dépendances d’architecture

## 35.1 Domain

Domain ne référence pas :

- la bibliothèque YAML ;
- le filesystem ;
- JSON Schema ;
- le CLI.

---

## 35.2 Application

Application manipule un modèle de configuration typé et des erreurs structurées.

---

## 35.3 Infrastructure

Infrastructure contient :

- lecture du fichier ;
- parsing YAML ;
- validation technique ;
- adaptation vers les contrats Application.

---

## 35.4 CLI

Le CLI :

- reçoit le chemin ;
- appelle le cas d’usage ;
- présente les erreurs ;
- ne contient pas les règles YAML centrales.

---

# 36. Bibliothèque YAML

La bibliothèque exacte doit faire l’objet d’une validation de dépendance.

Elle doit être :

- maintenue ;
- compatible avec .NET 10 ;
- configurable de manière sûre ;
- capable de refuser les clés dupliquées ;
- capable de désactiver anchors, aliases et tags ;
- compatible avec les limites de profondeur et taille ;
- utilisable sans activation arbitraire de types.

---

## 36.1 Parser personnalisé

La création d’un parser YAML maison est interdite.

---

## 36.2 Désérialisation directe dangereuse

Le parser ne doit pas activer automatiquement des types CLR provenant de tags ou d’informations contenues dans le fichier.

---

# 37. Version du schéma

La racine du fichier contient obligatoirement :

```yaml
schemaVersion: 1
```

---

## 37.1 Absence

Une configuration sans `schemaVersion` est refusée.

---

## 37.2 Version future

Un CLI ancien rencontrant une version plus récente doit refuser proprement.

Exemple :

```text
Configuration schema version 2 is not supported
by SiteFactory CLI 0.4.0.
```

---

## 37.3 Version ancienne

Une ancienne version supportée peut :

- être lue ;
- produire un warning ;
- proposer une migration.

Elle ne doit pas être modifiée automatiquement pendant une simple validation.

---

# 38. Schéma machine-readable

La structure YAML doit être décrite par un schéma machine-readable au format JSON.

Emplacement conceptuel :

```text
schemas/client-configuration.schema.v1.json
```

---

## 38.1 Objectifs

Le schéma permet :

- validation structurelle ;
- documentation ;
- intégration éditeur ;
- détection des propriétés inconnues ;
- génération de samples ;
- tests ;
- migration.

---

## 38.2 Limite

Le schéma structurel ne remplace pas les validations métier telles que :

- compatibilité de modules ;
- dépendances ;
- entitlement ;
- existence d’un template ;
- cohérence d’un domaine ;
- conflit de routes.

---

# 39. Niveaux de validation

## 39.1 Niveau 1 — Lecture

Vérifier :

- fichier présent ;
- fichier lisible ;
- taille acceptable ;
- encodage valide.

---

## 39.2 Niveau 2 — Syntaxe YAML

Vérifier :

- indentation ;
- document unique ;
- scalaires ;
- clés dupliquées ;
- fonctionnalités interdites.

---

## 39.3 Niveau 3 — Schéma

Vérifier :

- propriétés ;
- types ;
- propriétés obligatoires ;
- limites ;
- enums ;
- valeurs nulles.

---

## 39.4 Niveau 4 — Domaine

Vérifier :

- slug ;
- domaine ;
- identifiants ;
- formats ;
- invariants.

---

## 39.5 Niveau 5 — Catalogue et compatibilité

Vérifier :

- pack ;
- modules ;
- versions ;
- dépendances ;
- conflits ;
- entitlements ;
- template ;
- thème.

---

## 39.6 Niveau 6 — Génération

Vérifier les contraintes nécessaires à la création du plan :

- chemins ;
- noms de ressources ;
- collisions ;
- profil de déploiement ;
- environnement.

---

# 40. Ordre des erreurs

Les erreurs doivent être présentées dans un ordre stable.

Ordre recommandé :

```text
chemin de propriété
→ code d’erreur
→ message
```

L’ordre ne doit pas dépendre :

- du système d’exploitation ;
- d’un dictionnaire non ordonné ;
- d’un traitement parallèle non normalisé.

---

# 41. Format d’une erreur

Exemple humain :

```text
SF-CONFIG-YAML-004

Path:
modules[1].enabled

Message:
Expected a boolean value.
```

Exemple JSON :

```json
{
  "code": "SF-CONFIG-YAML-004",
  "path": "modules[1].enabled",
  "message": "Expected a boolean value.",
  "severity": "error"
}
```

---

## 41.1 Données sensibles

Une erreur ne doit pas recopier :

- un secret ;
- tout le document ;
- une longue chaîne personnelle ;
- une valeur potentiellement malveillante non neutralisée.

---

# 42. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-CONFIG-YAML-001` | Fichier introuvable |
| `SF-CONFIG-YAML-002` | Fichier illisible |
| `SF-CONFIG-YAML-003` | Fichier trop volumineux |
| `SF-CONFIG-YAML-004` | Syntaxe YAML invalide |
| `SF-CONFIG-YAML-005` | Plusieurs documents détectés |
| `SF-CONFIG-YAML-006` | Clé dupliquée |
| `SF-CONFIG-YAML-007` | Anchor ou alias interdit |
| `SF-CONFIG-YAML-008` | Tag YAML interdit |
| `SF-CONFIG-YAML-009` | Merge key interdite |
| `SF-CONFIG-YAML-010` | Profondeur maximale dépassée |
| `SF-CONFIG-YAML-011` | Encodage invalide |
| `SF-CONFIG-SCHEMA-001` | `schemaVersion` absent |
| `SF-CONFIG-SCHEMA-002` | Version de schéma non supportée |
| `SF-CONFIG-SCHEMA-003` | Propriété obligatoire absente |
| `SF-CONFIG-SCHEMA-004` | Propriété inconnue |
| `SF-CONFIG-SCHEMA-005` | Type invalide |
| `SF-CONFIG-SCHEMA-006` | Valeur hors limite |
| `SF-CONFIG-SEMANTIC-001` | Identifiant invalide |
| `SF-CONFIG-SEMANTIC-002` | Module inconnu |
| `SF-CONFIG-SEMANTIC-003` | Dépendance de module absente |
| `SF-CONFIG-SEMANTIC-004` | Modules incompatibles |
| `SF-CONFIG-SEMANTIC-005` | Entitlement manquant |
| `SF-CONFIG-SEMANTIC-006` | Template incompatible |
| `SF-CONFIG-SEMANTIC-007` | Thème incompatible |
| `SF-CONFIG-SEMANTIC-008` | Profil de déploiement incompatible |

---

# 43. Validation avant effet de bord

Aucune des opérations suivantes ne doit commencer avant validation complète :

- création du dossier cible ;
- écriture de fichier ;
- création de repository Git ;
- restauration NuGet ;
- exécution de processus ;
- accès réseau ;
- création de base ;
- création de volume ;
- génération de secret ;
- appel LLM.

Le flux obligatoire est :

```text
read
→ parse
→ validate
→ normalize
→ plan
→ approve or execute
```

---

# 44. Normalisation

La normalisation produit une représentation typée et canonique.

Elle peut notamment :

- appliquer les valeurs par défaut validées ;
- normaliser certains formats explicitement prévus ;
- ordonner les collections lorsque l’ordre n’a pas de sens ;
- résoudre les versions ;
- convertir les identifiants vers des value objects.

---

## 44.1 Pas de correction silencieuse

La normalisation ne doit pas :

- convertir une majuscule interdite ;
- corriger une faute de propriété ;
- remplacer un module inconnu ;
- inventer un domaine ;
- ajouter un module commercial ;
- supprimer une valeur invalide.

Une valeur invalide produit une erreur.

---

# 45. Empreinte de configuration

Le lock file peut enregistrer :

```text
configurationSha256
```

Cette empreinte doit représenter le contenu sémantique normalisé, et non les octets bruts du fichier YAML.

---

## 45.1 Raisons

Les modifications suivantes ne doivent pas changer l’empreinte sémantique :

- ajout d’un commentaire ;
- changement de fin de ligne ;
- réorganisation de clés sans signification ;
- modification d’indentation valide.

---

## 45.2 Pipeline conceptuel

```text
YAML source
→ modèle typé
→ valeurs par défaut appliquées
→ représentation canonique
→ JSON normalisé
→ SHA-256
```

---

## 45.3 Valeurs résolues

L’empreinte de la configuration source et celle du lock file peuvent être distinctes.

Exemple :

```text
configurationSha256
resolvedCompositionSha256
```

La nomenclature exacte sera définie dans le schéma du lock file.

---

# 46. Représentation canonique

La représentation canonique doit :

- utiliser des noms de propriétés stables ;
- ordonner les propriétés ;
- ordonner les ensembles non ordonnés ;
- préserver l’ordre des séquences lorsque l’ordre appartient au contrat ;
- utiliser des chaînes normalisées ;
- utiliser UTC pour les timestamps ;
- exclure les commentaires ;
- exclure les secrets ;
- exclure les données non déterministes.

---

# 47. Modification du fichier source

## 47.1 Validation

La commande de validation ne modifie jamais le fichier.

---

## 47.2 Génération

La génération ne reformate pas automatiquement le fichier source.

---

## 47.3 Upgrade

Un upgrade ne remplace pas silencieusement la configuration.

---

## 47.4 Migration

Une migration de schéma produit un nouveau fichier ou un diff explicite.

Exemple conceptuel :

```text
client.sitefactory.yml

client.sitefactory.migrated.yml
```

L’original reste conservé jusqu’à validation.

---

# 48. Commandes CLI

Commandes conceptuelles :

```bash
sitefactory config validate \
  --file client.sitefactory.yml
```

```bash
sitefactory config validate \
  --file client.sitefactory.yml \
  --format json
```

```bash
sitefactory config explain \
  --file client.sitefactory.yml
```

```bash
sitefactory config migrate \
  --file client.sitefactory.yml \
  --target-schema 2
```

Les commandes définitives restent régies par :

```text
15-cli-specification.md
```

---

## 48.1 `config validate`

La commande :

- lit ;
- parse ;
- valide ;
- normalise en mémoire ;
- produit un résultat ;
- ne crée aucun projet.

---

## 48.2 `config explain`

Une commande future peut afficher :

- valeurs explicites ;
- valeurs par défaut ;
- provenance ;
- versions résolues ;
- warnings ;
- aucune valeur de secret.

---

## 48.3 `config migrate`

Une migration :

- exige une version cible ;
- affiche un plan ;
- ne remplace pas l’original par défaut ;
- signale les décisions manuelles ;
- produit un rapport.

---

# 49. JSON pour les sorties machine

Les sorties JSON doivent être :

- valides ;
- versionnées ;
- déterministes lorsque possible ;
- sans commentaires ;
- sans secrets ;
- compatibles avec une consommation automatique ;
- accompagnées d’une propriété `schemaVersion`.

---

## 49.1 Indentation

Les fichiers conservés dans le repository peuvent utiliser une indentation lisible.

Les sorties temporaires du CLI peuvent être compactes lorsqu’une option le demande.

---

## 49.2 Ordre

L’ordre des propriétés JSON peut être stabilisé pour :

- lisibilité ;
- tests golden ;
- reproductibilité.

Il ne doit pas constituer un contrat métier sauf documentation contraire.

---

# 50. Différence entre configuration et lock file

## 50.1 Configuration

Peut contenir :

```text
packId
templateId
module IDs
options
contraintes
préférences
```

---

## 50.2 Lock file

Contient notamment :

```text
versions exactes
dépendances résolues
hashes
profil exact
schémas
versions de composants
```

---

## 50.3 Modification

Le lock file est généré.

Il ne doit pas être utilisé comme substitut au fichier YAML pour modifier le projet.

---

# 51. Git

Le fichier suivant est versionné :

```text
client.sitefactory.yml
```

Les artefacts suivants peuvent également être versionnés selon leur contrat :

```text
sitefactory.lock.json

schémas

samples

rapports nécessaires à la traçabilité
```

Les rapports d’exécution purement temporaires peuvent rester hors Git.

---

## 51.1 Secrets

Les fichiers secrets restent dans `.gitignore` ou dans un mécanisme externe.

---

## 51.2 Conflits Git

Les conflits YAML sont résolus humainement.

Le CLI ne doit pas tenter de lire un fichier contenant des marqueurs tels que :

```text
<<<<<<<
=======
>>>>>>>
```

---

# 52. Samples

Le repository SiteFactory doit fournir au minimum :

```text
client.sitefactory.minimal.yml

client.sitefactory.sample-pack.yml

client.sitefactory.invalid.yml
```

Les noms définitifs peuvent évoluer.

---

## 52.1 Validité

Tout sample présenté comme valide doit être testé automatiquement.

---

## 52.2 Données

Les samples utilisent :

```text
example.test

Example Documentation

sample-project

not-a-real-secret
```

et aucune donnée client réelle.

---

# 53. Documentation du schéma

Chaque propriété doit documenter :

- chemin ;
- type ;
- caractère obligatoire ;
- valeur par défaut ;
- format ;
- contraintes ;
- exemple ;
- impact ;
- sensibilité ;
- version d’introduction ;
- dépréciation éventuelle.

---

# 54. Éditeurs

Le schéma machine-readable doit permettre, lorsque l’éditeur le supporte :

- autocomplétion ;
- validation ;
- documentation inline ;
- détection de propriétés inconnues.

L’association automatique entre le YAML et le schéma reste un confort d’édition.

Elle ne remplace pas la validation du CLI.

---

# 55. Génération ou formatage assisté

Une commande future pourra produire un fichier exemple ou normalisé :

```bash
sitefactory config init
```

ou :

```bash
sitefactory config format
```

---

## 55.1 Préservation

Une commande de formatage doit :

- être explicite ;
- produire un diff ;
- préserver le sens ;
- ne pas ajouter de secret ;
- prévenir de la perte éventuelle de commentaires si le serializer ne les conserve pas.

---

## 55.2 Décision MVP

Aucun formatage automatique n’est exécuté pendant :

- validate ;
- plan ;
- generate ;
- upgrade check.

---

# 56. Migrations de schéma

Une nouvelle version incompatible de configuration nécessite :

- nouvelle `schemaVersion` ;
- schéma distinct ;
- guide de migration ;
- tests source-cible ;
- commande de migration ou procédure ;
- maintien temporaire des versions supportées.

---

## 56.1 Modification compatible

Un nouveau champ facultatif peut rester dans la même version de schéma si :

- les anciens fichiers restent valides ;
- la valeur par défaut est sûre ;
- les anciens lecteurs ne sont pas affectés selon la politique de support.

---

## 56.2 Modification incompatible

Exemples :

- renommage d’une propriété ;
- suppression d’une propriété ;
- changement de type ;
- nouvelle propriété obligatoire sans défaut ;
- modification d’un enum ;
- changement de sens.

Ces changements nécessitent une nouvelle version de schéma.

---

# 57. Compatibilité ascendante

Un CLI ne doit pas supposer qu’il comprend une version future.

Il doit refuser avant tout effet de bord.

---

# 58. Compatibilité descendante

Le CLI peut supporter plusieurs versions anciennes selon :

```text
25-upgrade-and-maintenance-strategy.md
```

Les versions supportées doivent être testées explicitement.

---

# 59. Sécurité

## 59.1 Menaces principales

- expansion d’aliases ;
- profondeur excessive ;
- fichier trop volumineux ;
- activation de types ;
- clés dupliquées ;
- valeurs ambiguës ;
- path traversal ;
- secret committé ;
- chaîne malveillante dans les logs ;
- déni de service du parser ;
- configuration future interprétée par un ancien CLI.

---

## 59.2 Contrôles

- taille limitée ;
- profondeur limitée ;
- aliases désactivés ;
- tags désactivés ;
- document unique ;
- clés dupliquées refusées ;
- modèle typé ;
- schéma strict ;
- validation métier ;
- logs neutralisés ;
- version obligatoire ;
- aucun effet de bord avant validation.

---

## 59.3 Fichier non fiable

Même versionné dans Git, le fichier reste une entrée non fiable.

Une pull request peut modifier :

- chemins ;
- domaines ;
- commandes indirectes ;
- providers ;
- volumes ;
- destinations.

La validation serveur et les allowlists restent nécessaires.

---

# 60. Tests requis

## 60.1 Parsing nominal

Tester :

- configuration minimale ;
- configuration complète ;
- commentaires ;
- chaînes multilignes ;
- Unicode ;
- listes ;
- booléens ;
- nombres.

---

## 60.2 Syntaxe invalide

Tester :

- indentation invalide ;
- clé sans valeur ;
- séquence mal formée ;
- document tronqué ;
- caractère invalide.

---

## 60.3 Capacités interdites

Tester :

- anchor ;
- alias ;
- merge key ;
- custom tag ;
- plusieurs documents ;
- clé complexe ;
- clé dupliquée.

---

## 60.4 Types ambigus

Tester :

```text
yes
no
on
off
0123
1.0
2026-07-28
~
null
```

---

## 60.5 Schéma

Tester :

- `schemaVersion` absent ;
- version inconnue ;
- propriété obligatoire absente ;
- propriété inconnue ;
- type incorrect ;
- collection trop longue ;
- chaîne trop longue.

---

## 60.6 Sécurité

Tester :

- profondeur excessive ;
- taille excessive ;
- caractères de contrôle ;
- secret ressemblant à un token ;
- path traversal ;
- message d’erreur sans valeur sensible.

---

## 60.7 Déterminisme

Tester que :

- commentaires différents ;
- ordre de clés différent ;
- fins de ligne différentes ;

produisent le même modèle normalisé et la même empreinte sémantique.

---

## 60.8 Plateformes

Exécuter les scénarios essentiels sur :

```text
Windows x64

Linux x64
```

---

# 61. Golden tests

Des golden tests peuvent vérifier :

```text
YAML source
→ modèle normalisé
→ plan
→ lock file
```

Les golden files doivent rester :

- lisibles ;
- versionnés ;
- sans donnée réelle ;
- mis à jour explicitement.

---

# 62. Tests de migration

Pour chaque évolution de schéma supportée :

```text
ancienne configuration
→ plan de migration
→ nouvelle configuration
→ validation
→ même intention métier
```

Les décisions manuelles doivent rester visibles.

---

# 63. Tests du projet généré

Une configuration valide de référence doit produire un projet qui :

```text
restore

build Release

exécute ses tests

conserve le lock file attendu
```

---

# 64. Conséquences positives

La décision apporte :

- une configuration lisible ;
- la possibilité de documenter les choix par commentaires ;
- des diffs Git plus simples ;
- une structure adaptée aux modules ;
- une séparation claire entre entrée et sortie ;
- un schéma versionné ;
- une validation progressive ;
- une migration future contrôlée ;
- une empreinte sémantique stable ;
- une meilleure collaboration avec Codex ;
- une configuration indépendante du runtime applicatif.

---

# 65. Conséquences négatives

La décision entraîne :

- une dépendance YAML supplémentaire ;
- des règles de sécurité spécifiques ;
- une syntaxe sensible à l’indentation ;
- des tests de parser supplémentaires ;
- une restriction volontaire des fonctionnalités YAML ;
- deux formats à maintenir, YAML et JSON ;
- une possible perte des commentaires lors d’une migration automatisée ;
- un besoin de documentation précise des types scalaires.

---

# 66. Risques acceptés

## 66.1 Erreurs d’indentation

YAML peut être mal indenté.

Réduction :

- validation CLI ;
- schéma éditeur ;
- samples ;
- messages avec chemins et positions.

---

## 66.2 Ambiguïté des scalaires

Réduction :

- sous-ensemble JSON-compatible ;
- guillemets recommandés ;
- modèle typé ;
- tests.

---

## 66.3 Complexité du parser

Réduction :

- bibliothèque maintenue ;
- configuration sûre ;
- pas de parser maison ;
- limites.

---

## 66.4 Différence entre source et état résolu

Réduction :

- plan ;
- lock file ;
- commande `explain` future ;
- valeurs par défaut documentées.

---

# 67. Mesures compensatoires

- `schemaVersion` obligatoire ;
- schéma JSON ;
- parsing sécurisé ;
- propriétés inconnues refusées ;
- duplicate keys refusées ;
- anchors et tags interdits ;
- aucun effet de bord avant validation ;
- hash sémantique ;
- samples testés ;
- migrations explicites ;
- rapports JSON ;
- secret scanning.

---

# 68. Performance

La configuration reste suffisamment petite pour être chargée entièrement en mémoire dans le MVP.

Cette hypothèse reste encadrée par :

- taille maximale ;
- profondeur maximale ;
- nombre de nœuds ;
- validation bornée.

Aucun streaming YAML complexe n’est nécessaire.

---

# 69. Coûts

## 69.1 Coûts supplémentaires

- intégration d’une bibliothèque YAML ;
- validation avancée ;
- schéma ;
- tests ;
- messages d’erreur ;
- migrations.

---

## 69.2 Coûts évités

- développement d’un DSL ;
- interface graphique prématurée ;
- commentaires externalisés ;
- configuration exécutable ;
- maintenance d’un parser personnalisé ;
- interprétation de multiples formats concurrents.

---

# 70. Maintenabilité

Le modèle typé reste indépendant de YAML.

Cette séparation permet :

- de changer de bibliothèque ;
- d’ajouter un autre frontend ;
- d’introduire une UI ;
- de générer du YAML ;
- de migrer vers un autre format ;

sans déplacer les règles métier hors des couches prévues.

---

# 71. Compatibilité

## 71.1 CLI

Le CLI doit indiquer les versions de schéma qu’il supporte.

---

## 71.2 Templates et modules

La configuration référence des identifiants et contraintes.

Les versions exactes sont résolues et conservées dans le lock file.

---

## 71.3 Breaking change

```text
Breaking change expected: no
```

pour les premiers projets, puisque cette décision constitue la baseline initiale.

Toute modification incompatible future du format utilisera une nouvelle `schemaVersion`.

---

# 72. Migration

## 72.1 Projets existants

Au moment de cette décision, aucun projet SiteFactory stable n’utilise un autre format canonique.

Aucune migration de production n’est requise.

---

## 72.2 Prototypes

Les éventuels prototypes JSON ou configurations codées doivent être convertis vers :

```text
client.sitefactory.yml
```

avant leur intégration au standard.

---

## 72.3 Production

```text
Production execution: out of scope
```

Cet ADR définit un contrat de configuration et non une opération de production.

---

# 73. Réversibilité

## 73.1 Classification

```text
élevée
```

Le modèle typé et le schéma restent indépendants du serializer.

---

## 73.2 Vers JSON

Une migration vers JSON pourrait sérialiser le modèle canonique.

Les commentaires seraient perdus ou devraient être déplacés vers la documentation.

---

## 73.3 Vers une interface graphique

Une future UI pourrait éditer le même modèle typé puis produire une configuration YAML validée.

---

## 73.4 Vers un autre format

Un nouvel ADR serait requis pour :

- analyser la migration ;
- préserver les commentaires ;
- gérer les versions ;
- adapter les outils ;
- définir la période de transition.

---

# 74. Plan d’implémentation

## Étape 1 — Contrat

Définir :

- modèle typé ;
- `schemaVersion` ;
- propriétés initiales ;
- erreurs.

---

## Étape 2 — Schéma

Créer :

```text
schemas/client-configuration.schema.v1.json
```

---

## Étape 3 — Parsing

Ajouter :

- lecture bornée ;
- parser sécurisé ;
- document unique ;
- duplicate key detection ;
- rejet des fonctionnalités interdites.

---

## Étape 4 — Validation

Ajouter :

- validation structurelle ;
- mapping ;
- validation métier ;
- validation de compatibilité.

---

## Étape 5 — CLI

Implémenter :

```text
sitefactory config validate
```

---

## Étape 6 — Normalisation

Créer :

- modèle canonique ;
- sérialisation déterministe ;
- empreinte SHA-256.

---

## Étape 7 — Samples

Ajouter et tester les configurations de référence.

---

## Étape 8 — Migrations

Préparer le mécanisme de migration avant l’introduction de `schemaVersion: 2`.

---

# 75. Tâches liées proposées

```text
SF-TASK-009 — Define the client configuration domain model

SF-TASK-010 — Add client configuration schema version 1

SF-TASK-011 — Select and secure the YAML parser

SF-TASK-012 — Implement YAML syntax validation

SF-TASK-013 — Implement schema validation

SF-TASK-014 — Map YAML configuration to the typed model

SF-TASK-015 — Add semantic configuration validation

SF-TASK-016 — Implement `sitefactory config validate`

SF-TASK-017 — Add canonical configuration hashing

SF-TASK-018 — Add valid and invalid configuration samples
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 76. Tests et preuves attendus

Pour considérer la décision correctement implémentée, les preuves suivantes sont attendues :

```text
Configuration minimale valide

Configuration complète valide

Propriété inconnue refusée

Clé dupliquée refusée

Anchor refusée

Alias refusé

Custom tag refusé

Plusieurs documents refusés

Fichier trop volumineux refusé

Profondeur excessive refusée

Secret non loggué

Aucune écriture après échec

Même hash malgré les commentaires et l’ordre des clés

Validation Windows

Validation Linux

Projet de référence généré et compilé
```

---

# 77. Conditions de révision

Réexaminer cette décision si :

- YAML provoque un nombre important d’erreurs opérateur ;
- une interface graphique devient la source canonique ;
- des utilisateurs non techniques doivent modifier fréquemment la configuration ;
- les restrictions rendent le format trop difficile à utiliser ;
- l’écosystème .NET ne fournit plus de parser maintenu adapté ;
- un besoin réel de composition de plusieurs fichiers apparaît ;
- la configuration dépasse régulièrement la taille ou la complexité prévue ;
- une plateforme SaaS centralisée remplace les repositories clients ;
- les commentaires ne peuvent pas être préservés lors des migrations nécessaires ;
- la double maintenance YAML/JSON crée un coût disproportionné.

---

# 78. Indicateurs à suivre

- nombre d’erreurs de parsing ;
- nombre d’erreurs de schéma ;
- nombre de propriétés inconnues ;
- temps moyen de correction d’une configuration ;
- fréquence des migrations de schéma ;
- nombre de configurations invalides détectées en CI ;
- stabilité des hashes ;
- temps de validation ;
- taille moyenne des fichiers ;
- nombre de décisions manuelles pendant une migration ;
- fréquence des modifications directes du lock file ;
- nombre de secrets détectés dans des configurations.

---

# 79. Signaux d’alerte

La décision doit être réexaminée si les observations montrent :

```text
des configurations régulièrement incompréhensibles

des erreurs d’indentation fréquentes

des contournements du schéma

des fichiers dépassant largement le rôle de configuration

des besoins répétés de logique conditionnelle

des fusions manuelles difficiles entre environnements

des divergences entre la configuration et le lock file

des secrets régulièrement ajoutés par erreur
```

---

# 80. Conséquences observées

Cette section est append-only après mise en œuvre.

## 80.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.
La décision précède l’implémentation du chargeur de configuration.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- scénario ;
- résultat ;
- métrique ;
- action proposée.

---

# 81. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- Les règles de syntaxe et de validation structurelle sont définies dans les sections 18 à 39 du présent ADR et dans le schéma public de configuration ; aucun catalogue tarifaire privé n'est requis pour appliquer ces contraintes.
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `15-cli-specification.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`

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
Configuration écrite par LogicGate
→ client.sitefactory.yml
→ YAML restreint et sécurisé
→ sans secret
→ schemaVersion obligatoire
→ validation stricte
→ aucune écriture avant validation

Configuration validée
→ modèle typé
→ normalisation
→ plan de génération
→ artefacts JSON
→ lock file
→ hash sémantique

YAML
→ intention humaine

JSON
→ état résolu et preuve machine
```

> SiteFactory utilise YAML comme interface déclarative pour les humains, mais ne lui confie ni logique exécutable, ni fusion implicite, ni gestion de secrets. La machine résout cette intention dans des artefacts JSON stricts, versionnés et vérifiables.
