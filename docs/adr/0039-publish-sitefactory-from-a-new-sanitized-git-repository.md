# ADR-0039 — Publier SiteFactory depuis un nouveau repository Git assaini

- **Statut :** Accepté
- **Date :** 2026-10-02

## Contexte

Le repository SiteFactory actuel contient l'historique de développement
du produit privé LogicGate.

Même si l'état courant est publiable, l'historique Git peut contenir :

- ancien code propriétaire ;
- anciens templates PME ;
- anciennes configurations ;
- fichiers supprimés ;
- chemins internes ;
- secrets supprimés ;
- éléments non destinés à être publics.

## Décision

Ne pas rendre public le repository privé actuel.

Créer un nouveau repository Git dédié au projet open source.

```text
Repository privé actuel
        │
        ├── historique complet conservé
        │
        └── extraction contrôlée
                 ↓
        contenu public validé
                 ↓
        nouveau repository Git
                 ↓
        audit
                 ↓
        repository PUBLIC
```

## Source du repository public

Le nouveau repository contient uniquement les composants définis
comme publics par ADR-0033.

Il démarre avec un nouvel historique Git.

Pas de copie de :

```text
.git/
```

depuis le repository privé.

## Audit avant publication

Avant le premier push public, vérifier au minimum :

- absence de modules Business ;
- absence de templates PME privés ;
- absence de secrets ;
- absence de credentials ;
- absence de noms clients ;
- absence de chemins locaux sensibles ;
- absence d'assets non publiables ;
- licences des dépendances ;
- fichiers de configuration ;
- exemples et fixtures.

## Historique

L'historique privé reste dans le repository d'origine.

Le repository public ne cherche pas à reproduire artificiellement
tout l'historique précédent.

```text
Private history
      │
      └── reste privé

Public history
      │
      └── commence à l'extraction open source
```

## Migration

Pendant la séparation :

1. identifier les composants publics ;
2. les exporter ;
3. créer le nouveau repository ;
4. exécuter les quality gates ;
5. effectuer l'audit sécurité/licence ;
6. publier seulement après validation.

## Source de vérité

Après migration :

```text
composant public
→ repository public uniquement

composant Business
→ repository privé uniquement
```

Aucune duplication permanente n'est autorisée.

## Repository privé

Le repository privé actuel peut être :

- conservé comme historique ;
- ou devenir le repository Business après nettoyage.

Il ne doit jamais être rendu public directement.

## Hors portée

Cet ADR ne définit pas :

- la procédure exacte de migration des commits ;
- le nom final du repository public ;
- la date de publication ;
- la publication NuGet ;
- le contenu marketing du lancement open source.

## Conséquences

### Positives

- historique propriétaire protégé ;
- risque de fuite fortement réduit ;
- frontière public/privé explicite ;
- première release open source maîtrisée.

### Contraintes

- historique public recommence à zéro ;
- audit initial nécessaire ;
- migration des fichiers à organiser.
```

Le principe essentiel de cet ADR est :

```text
ancien repo privé
      ≠
futur repo public

on exporte le code public,
pas l'historique privé
```