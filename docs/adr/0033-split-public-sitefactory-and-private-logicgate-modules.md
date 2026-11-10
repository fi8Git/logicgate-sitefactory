# ADR-0033 — Séparer SiteFactory public des modules LogicGate privés

- **Statut :** Accepté
- **Date :** 2026-10-02

## Contexte

SiteFactory est désormais un moteur modulaire.

Le Core, le Module SDK et le module Documentation peuvent être publiés
sans exposer les modules métier et assets propriétaires de LogicGate.

La séparation doit empêcher toute dépendance du produit public vers
le code privé.

## Décision

Adopter deux repositories indépendants.

```text
┌───────────────────────────────────────┐
│ Repository SiteFactory public         │
│ PUBLIC                                │
│                                       │
│ Core                                  │
│ ModuleSdk                             │
│ CLI générique                         │
│ DocumentationModule                   │
│ Infrastructure générique              │
│ PostgreSQL générique                  │
└──────────────────┬────────────────────┘
                   │
             packages publics
                   │
                   ▼
┌───────────────────────────────────────┐
│ Repository métier privé               │
│ PRIVATE                               │
│                                       │
│ modules de génération métier          │
│ modules de fonctionnalités métier     │
│ templates métier                      │
│ assets propriétaires                  │
│ futures fonctions métier              │
└──────────────────┬────────────────────┘
                   │
                   ▼
          repositories clients
```

### Dépendances autorisées

```text
Business ───────► SiteFactory public

Client ────────► Business / SiteFactory
```

### Dépendances interdites

```text
SiteFactory public ──X──► Business

DocumentationModule ─X──► Business
ModuleSdk            ─X──► Business
Core                 ─X──► module LogicGate concret
```

## Répartition

| Composant | Public | Privé |
|---|:---:|:---:|
| Core | ✓ | |
| ModuleSdk | ✓ | |
| CLI générique | ✓ | |
| Generation Engine | ✓ | |
| DocumentationModule | ✓ | |
| Repository Scanner | ✓ | |
| Infrastructure PostgreSQL générique | ✓ | |
| Modules de génération métier | | ✓ |
| Modules de fonctionnalités métier | | ✓ |
| Templates/Assets LogicGate | | ✓ |
| Modèles et fonctionnalités métier spécifiques | | ✓ |

## Invariants

- Le repository public build et teste sans accès au repository privé.
- Le repository privé consomme le public par des contrats/packages versionnés.
- Aucun `ProjectReference` entre les deux repositories.
- Un composant possède une seule source de vérité après migration.
- Les repositories clients restent indépendants.

## Hors portée

Cet ADR ne décide pas :

- de la licence open source ;
- du packaging NuGet ;
- du versioning ;
- de la composition des modules ;
- de la CI inter-repositories ;
- de la stratégie de publication Git.

Ces décisions sont couvertes par ADR-0034 à ADR-0039.

## Conséquences

### Positives

- Core publiable indépendamment ;
- modules LogicGate protégés ;
- modules tiers possibles ;
- frontière public/privé vérifiable par CI.

### Contraintes

- gestion de versions entre repositories ;
- publication de packages nécessaire ;
- CI privée dépendante des releases publiques.

La distinction importante de cet ADR est volontairement simple :

```text
Public
  ↑
  │ dépendance autorisée
Private

Private
  ✕
  │ jamais
  ▼
Public dépendant du privé
```
