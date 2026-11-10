# ADR-0035 — Distribuer les composants publics SiteFactory via NuGet

- **Statut :** Accepté
- **Date :** 2026-10-02

## Contexte

Après la séparation définie par ADR-0033, le repository Business privé
ne doit pas dépendre directement des sources du repository public.

Les composants publics nécessaires aux modules et hosts externes
doivent donc être distribués comme dépendances versionnées.

## Clarification de portée — 2026-11-09

**Décision humaine confirmée le 2026-11-09**, sans modification du statut
accepté ni de la date initiale du **2026-10-02**. Cet ADR prévaut sur la
réservation générale d'[ADR-0008](0008-private-package-feed-and-supply-chain.md#amendement-de-portée--2026-11-09)
uniquement pour les Package IDs explicitement admis à la distribution publique.

L'identifiant déjà décidé est **`LogicGate.SiteFactory.ModuleSdk`**. Tout autre
package public SiteFactory nécessite une admission explicite par **ID exact** ;
la nécessité pour un consommateur ou un exemple de package dans un ADR ne vaut
pas admission. Ni `LogicGate.SiteFactory.*` ni un autre préfixe entier n'est
automatiquement public, et ce préfixe n'est pas une classification de confidentialité.
Les autres packages internes restent réservés au feed privé, sans fallback
public. Aucun projet, namespace ou Package ID n'est renommé.

ADR-0008 reste intégralement applicable aux packages propriétaires et aux
exigences de sécurité de supply chain : sources/mappings approuvés, versions
explicites, locks, immutabilité/provenance, publication protégée, absence de
credentials dans le dépôt et refus de substitution. Pour un futur consommateur
Business, les IDs publics admis exactement sont mappés à la source publique
approuvée, les autres IDs internes au feed privé, et les dépendances tierces à
leurs propres sources/mappings approuvés. Tout extrait XML est conceptuel et
incomplet sans la définition des sources et des mappings tiers.

La preuve future du mapping devra utiliser un cache isolé et démontrer le refus
de substitution d'un package privé par un homonyme public. **Elle n'est pas
déclarée exécutée ici. Aucun package n'est déclaré effectivement publié.**
Cette clarification ne commence pas le packaging et ne modifie pas
`NuGet.config`, les projets ou les locks. Elle conserve les décisions ci-dessous :
packages versionnés entre repositories sans ProjectReference, aucun packaging
automatique de chaque projet, versions explicites et publication distincte des PR.
Les familles de noms et composants futurs cités dans les schémas suivants
n'accordent aucune admission publique supplémentaire.

## Décision

Distribuer les composants publics réutilisables sous forme de packages NuGet.

```text
logicgate-sitefactory
PUBLIC
      │
      ├── ModuleSdk
      ├── Core / contrats nécessaires
      └── composants publics réutilisables
                 │
                 ▼
              NuGet.org
                 │
        ┌────────┴────────┐
        ▼                 ▼
SiteFactory Business   Modules tiers
PRIVATE
```

Les modules LogicGate propriétaires restent distribués via le
feed NuGet privé défini par l'architecture existante.

## Règle de dépendance

Interdit :

```text
Business
   │
   └── ProjectReference ../logicgate-sitefactory/...
```

Autorisé :

```text
Business
   │
   └── PackageReference
         LogicGate.SiteFactory.*
         Version = explicite
```

## Packages publics

Au minimum :

```text
LogicGate.SiteFactory.ModuleSdk
```

Les autres composants du moteur sont publiés comme packages distincts
uniquement lorsqu'ils sont nécessaires à un consommateur externe.

Ne pas transformer automatiquement chaque projet interne en package public.

## Publication

Les packages publics sont publiés :

- depuis le repository public ;
- par la CI de release ;
- à partir d'une version Git explicite ;
- sans version flottante.

```text
tag/release
    ↓
build
    ↓
tests
    ↓
pack
    ↓
publish
```

Un build de PR ne publie pas de version stable.

## Consommation

Les consommateurs utilisent des versions explicites :

```xml
<PackageReference
    Include="LogicGate.SiteFactory.ModuleSdk"
    Version="0.x.y" />
```

Interdits :

```text
*
latest
version implicite
référence source cross-repository
```

Les lock files NuGet restent utilisés.

## Sources

```text
Packages publics SiteFactory
→ source publique approuvée

Packages Business LogicGate
→ feed privé LogicGate
```

Le Package Source Mapping doit empêcher un package privé LogicGate
de tomber silencieusement sur une source publique.

ADR-0008 reste applicable pour les règles de supply-chain.

## Développement inter-repositories

Pour tester une version non publiée :

```text
build public
   ↓
package prerelease/local feed contrôlé
   ↓
restore Business
```

Ne pas recréer une dépendance par chemin source entre repositories.

## Hors portée

Cet ADR ne définit pas :

- la politique SemVer ;
- les plages de compatibilité ModuleSdk ;
- le chargement dynamique de modules ;
- la signature de packages ;
- un marketplace.

Le versioning et la compatibilité sont définis par ADR-0036.

## Conséquences

### Positives

- repositories réellement indépendants ;
- dépendances reproductibles ;
- modules tiers possibles ;
- releases publiques consommables par le privé.

### Contraintes

- pipeline de publication NuGet nécessaire ;
- coordination des versions entre Core et modules ;
- développement cross-repository basé sur packages.
```

Le point essentiel est :

```text
Repository ≠ dépendance

Package versionné = dépendance
```
