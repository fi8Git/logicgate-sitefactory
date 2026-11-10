# ADR-0034 — Publier SiteFactory sous licence Apache-2.0

- **Statut :** Accepté
- **Date :** 2026-10-02

## Contexte

Le repository défini comme public par ADR-0033 doit pouvoir être :

- utilisé librement ;
- modifié ;
- redistribué ;
- utilisé commercialement ;
- consommé par des modules propriétaires.

La licence ne doit pas imposer l'ouverture du code des modules développés
à partir de `ModuleSdk`.

## Décision

Publier le repository public SiteFactory sous :

**Apache License 2.0**

```text
SiteFactory Public
Apache-2.0
      │
      ├── Core
      ├── ModuleSdk
      ├── CLI
      └── DocumentationModule
              │
              ▼
      ┌───────────────────┐
      │ Modules externes  │
      │                   │
      │ Open source   ✓   │
      │ Propriétaires ✓   │
      └───────────────────┘
```

Le repository :

`logicgate-sitefactory-business`

reste propriétaire à LogicGate.

## Invariants

- Le repository public contient un fichier `LICENSE` Apache-2.0.
- Les obligations de copyright et notices sont conservées lors des redistributions.
- Les composants propriétaires ne sont pas placés sous Apache-2.0 par dépendance au `ModuleSdk`.
- Aucun code privé LogicGate n'est copié dans le repository public sans décision explicite.
- Les dépendances tierces conservent leurs propres licences.

## Non couvert

Cet ADR ne définit pas la licence :

- des modules tiers ;
- des repositories clients générés ;
- des contenus générés ;
- des assets fournis par des tiers ;
- du repository Business privé.

Ces éléments doivent conserver ou définir leur propre régime de licence.

## Raisons du choix

Apache-2.0 permet :

- l'utilisation commerciale ;
- les modifications ;
- la redistribution ;
- les modules propriétaires ;
- une concession explicite de droits de brevet.

Elle correspond au modèle :

```text
Core ouvert
    +
écosystème de modules
    +
modules LogicGate propriétaires
```

## Alternatives rejetées

### MIT

Compatible avec le modèle, mais protection explicite sur les brevets
moins complète qu'Apache-2.0.

### GPL / AGPL

Non retenues car leur copyleft compliquerait l'objectif de permettre
des modules propriétaires indépendants.

## Conséquences

### Positives

- adoption facilitée ;
- usage commercial autorisé ;
- modules propriétaires possibles ;
- licence connue dans l'écosystème logiciel.

### Contraintes

- obligations Apache-2.0 à respecter lors des redistributions ;
- suivi des licences des dépendances et assets nécessaire.

Le point à laisser volontairement ouvert ici est **la licence du code client généré**. Je pense qu’il mérite sa propre décision plus tard, car il faut choisir si le client reçoit tous les droits sur le code généré, une licence spécifique LogicGate, ou simplement les licences héritées des composants utilisés.