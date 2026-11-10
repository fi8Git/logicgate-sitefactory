# ADR-0036 — Versionner SiteFactory et ses modules avec SemVer et une compatibilité explicite

- **Statut :** Accepté
- **Date :** 2026-10-02

## Contexte

Après séparation des repositories, les modules ne doivent pas dépendre
implicitement de la version courante du Core ou du ModuleSdk.

La compatibilité doit être :

- explicite ;
- vérifiable ;
- reproductible.

## Décision

Utiliser Semantic Versioning pour :

- SiteFactory public ;
- ModuleSdk ;
- modules LogicGate ;
- modules tiers.

```text
SiteFactory / ModuleSdk
        version X
            │
            ▼
      contrat public
            │
      ┌─────┴─────┐
      ▼           ▼
Module A        Module B
version A       version B
requires X      requires X
```

## Version des packages publics

Pendant la phase initiale, les packages publics SiteFactory partagent
une version commune.

Exemple de version commune pour les futurs packages des projets existants
(numéros illustratifs, pas des publications disponibles) :

```text
LogicGate.SiteFactory.ModuleSdk       0.20.0
LogicGate.SiteFactory.Application     0.20.0
LogicGate.SiteFactory.Infrastructure  0.20.0
```

Ne pas introduire de versioning indépendant par package
tant qu'un besoin réel ne le justifie.

## Compatibilité module

Chaque module déclare la plage de version du contrat SiteFactory
qu'il supporte.

Cette règle est une décision de compatibilité, pas l'annonce d'une API de plages
déjà livrée : le ModuleSdk actuel expose une version exacte `ModuleVersion`
dans `ModuleDescriptor`, et `ModuleCatalog` contrôle la version exacte attendue
à l'enregistrement. La déclaration et l'évaluation des plages restent à réaliser.

Exemple conceptuel pour un module tiers (identifiant et version compatibles
avec les types publics ; notation de plage illustrative, non une API existante) :

```text
ModuleId: example-docs
ModuleVersion: 1.3.0

requires SiteFactory Module API:

>= 0.20.0
< 0.21.0
```

Une incompatibilité doit être détectée avant génération.

Aucun fallback silencieux.

## Règles avant 1.0

Pendant la série `0.x` :

```text
0.20.1
→ correctif compatible attendu

0.20.x → 0.21.0
→ peut contenir un breaking change
```

Un changement cassant du contrat public impose donc au minimum
un changement de version mineure.

## Règles à partir de 1.0

Appliquer SemVer classique :

```text
1.2.0 → 1.3.0
compatible attendu

1.x → 2.0
breaking change autorisé
```

## Version de module

La version d'un module est indépendante de celle de SiteFactory.

Exemple illustratif, sans annoncer de release :

```text
SiteFactory 0.20.0

example-docs 1.3.0
documentation 0.8.0
example-blog 2.1.0
```

La compatibilité est déterminée par la plage déclarée,
pas par l'égalité des versions.

## Lock

Lorsqu'un projet client est généré, les versions résolues nécessaires
à la reproductibilité doivent être enregistrées dans le lock existant.

Conceptuellement :

```text
SiteFactory version
ModuleId
Module version
Module API compatibility
```

Ne pas utiliser :

```text
latest
*
version implicite
```

## CI

Un module doit être testé contre les versions SiteFactory
qu'il déclare supporter.

Au minimum :

```text
version minimale supportée
+
version utilisée pour la release
```

La CI doit refuser une combinaison déclarée incompatible.

## Hors portée

Cet ADR ne définit pas :

- le protocole de chargement des modules ;
- un resolver automatique depuis NuGet ;
- un marketplace ;
- la durée de support des anciennes versions ;
- une politique LTS.

## Conséquences

### Positives

- upgrades prévisibles ;
- incompatibilités détectables ;
- modules indépendamment versionnés ;
- releases reproductibles.

### Contraintes

- matrice de compatibilité à maintenir ;
- breaking changes du ModuleSdk à contrôler ;
- tests multi-versions nécessaires à terme.

Le point central est :

```text
Version du module
        ≠
Version de SiteFactory

Compatibilité
        =
plage explicitement déclarée
```
