# ADR-0037 — Composer les modules explicitement sans chargement dynamique

- **Statut :** Accepté
- **Date :** 2026-10-02

## Contexte

SiteFactory doit permettre :

- des modules publics ;
- des modules privés LogicGate ;
- des modules tiers.

Le chargement dynamique de DLL ajouterait prématurément :

- exécution de code non maîtrisé ;
- complexité de résolution ;
- problèmes de compatibilité ;
- risques supply-chain.

## Décision

Les modules sont ajoutés comme dépendances NuGet puis enregistrés
explicitement dans le composition root.

```text
PackageReference
      ↓
Assembly connu au build
      ↓
Composition root
      ↓
ModuleCatalog
      ↓
SiteFactory Core
```

Exemple utilisant les API existantes du point d'entrée public :

```csharp
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;

DocumentationModule module = new(new TemplateManifestJsonReader());
ModuleCatalog modules = new([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]);
GenerationComposition composition = new(modules, [module.CreateContribution()]);
```

L'instance expose son `ModuleDescriptor` via `ISiteFactoryModule` ; les contributions
de génération sont fournies séparément au moteur. Le host reçoit explicitement
la composition. Un host privé peut enregistrer ses propres implémentations des
mêmes contrats, sans dépendance inverse du Core.
Le repository courant utilise des références de projets internes ; la décision
de distribution par packages n'affirme pas qu'une publication NuGet existe déjà.

## Architecture

```text
Public CLI
   │
   ├── SiteFactory Core
   └── DocumentationModule


Business CLI
   │
   ├── SiteFactory Core
   └── Module métier privé (ISiteFactoryModule)
```

Le Core ne sélectionne jamais directement un module concret.

## Interdit

Pendant cette phase :

```text
Assembly.LoadFrom(...)
Assembly.LoadFile(...)
plugins/*.dll
scan automatique des assemblies
installation NuGet au runtime
téléchargement puis exécution d'un module
```

Aucun dossier `plugins` implicite.

## ModuleCatalog

Le catalogue reçoit uniquement les modules enregistrés explicitement.

Il doit refuser :

- `ModuleId` dupliqué ;
- descriptor invalide ;
- version incompatible.

Un module absent n'est jamais découvert automatiquement.

## Modules tiers

Un développeur tiers construit son propre host :

```text
Acme.SiteFactory
       │
       ├── SiteFactory packages
       ├── AcmeBlogModule
       └── AcmeDocsModule
```

Le développeur choisit explicitement le code exécuté.

## Sécurité

L'installation d'un package implique l'exécution volontaire
du code de ce module.

SiteFactory ne prétend pas isoler ou sandboxer les modules.

```text
Package installé
      =
code de confiance du host
```

## Hors portée

Cet ADR ne définit pas :

- sandbox de plugins ;
- marketplace ;
- hot reload ;
- isolation de processus ;
- chargement de modules non fiables ;
- installation dynamique depuis NuGet.

Ces capacités nécessiteraient une décision distincte.

## Conséquences

### Positives

- composition prévisible ;
- dépendances visibles au build ;
- surface d'attaque réduite ;
- versioning NuGet reproductible ;
- intégration simple des modules privés.

### Contraintes

- ajouter un module nécessite de reconstruire le host ;
- pas d'installation de plugin à chaud ;
- chaque distribution choisit explicitement ses modules.

Le principe central est simplement :

```text
Module installé au build
        ✅

Module découvert/exécuté dynamiquement
        ❌
```
