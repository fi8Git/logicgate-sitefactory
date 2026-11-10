# ADR-0038 — Rendre la CI publique autonome et la CI privée consommatrice

- **Statut :** Accepté
- **Date :** 2026-10-02

## Contexte

Après la séparation public / privé :

- le repository public ne doit dépendre d'aucune ressource privée ;
- le repository Business doit vérifier sa compatibilité avec les releases publiques ;
- les deux pipelines doivent rester reproductibles indépendamment.

## Décision

Adopter une CI asymétrique :

```text
Repository public
      │
      ├── restore
      ├── build
      ├── tests
      ├── security checks
      ├── pack
      └── publish
              │
              ▼
         NuGet public
              │
              ▼
Repository Business privé
      │
      ├── restore packages publics
      ├── build
      ├── tests PME
      ├── PostgreSQL E2E
      └── generated client E2E
```

## Repository public

La CI publique doit fonctionner sans accès à :

- repository Business ;
- packages privés LogicGate ;
- secrets Business ;
- templates PME ;
- infrastructure privée LogicGate.

Une PR publique ne publie pas de package stable.

## Repository privé

La CI privée consomme uniquement des packages SiteFactory versionnés.

Interdit :

```text
checkout repository public
+
ProjectReference cross-repository
```

Attendu :

```text
PackageReference
      ↓
restore
      ↓
build
      ↓
integration tests
```

## Release publique

Pipeline :

```text
tag/version
    ↓
quality gates
    ↓
tests
    ↓
pack
    ↓
publish NuGet
```

La publication n'a lieu qu'après succès des quality gates.

## Compatibilité Business

Pour chaque version Business publiée :

```text
Business version
      ↓
SiteFactory version explicite
      ↓
compatibility tests
      ↓
release
```

Une incompatibilité bloque la release privée.

## Prerelease

Pour tester une version SiteFactory non stable :

```text
public prerelease package
        ↓
feed contrôlé
        ↓
Business CI
```

Pas de dépendance directe aux sources publiques.

## Secrets

```text
Public CI
→ uniquement secrets nécessaires à la publication publique

Private CI
→ secrets Business uniquement
```

Aucun secret privé ne doit être requis par la CI publique.

## Hors portée

Cet ADR ne définit pas :

- le fournisseur exact du feed prerelease ;
- la stratégie de release automatique ;
- les environnements de production ;
- le déploiement client ;
- une matrice complète multi-version.

## Conséquences

### Positives

- open source réellement autonome ;
- aucune fuite de dépendance privée ;
- compatibilité Business testable ;
- releases reproductibles.

### Contraintes

- publication de packages nécessaire avant consommation privée ;
- pipelines séparés à maintenir ;
- validation inter-repositories basée sur versions publiées.
```

Le principe à retenir :

```text
PUBLIC
  │
  └── produit des packages
            │
            ▼
PRIVATE
  └── les consomme

PUBLIC ──X──► PRIVATE
```
