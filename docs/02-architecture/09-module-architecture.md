# LogicGate SiteFactory — Architecture des modules

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Architecture des modules |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, architectes, développeurs, contributeurs, Codex |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `module-catalog.md`, `../adr/0009-client-customizations-and-extension-points.md`, `core-module-boundary.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Périmètre public actuel et contrat cible

Le [ModuleSdk](module-sdk.md) décrit les modules ; le [catalogue](module-catalog.md)
les reçoit explicitement. Le [Core](core-module-boundary.md) ne connaît aucune
offre concrète. `GenerationComposition` associe le catalogue et les contributions ;
le point d'entrée public compose `DocumentationModule.CreateContribution()`.
Aucun scan d'assemblies, chargement de DLL, téléchargement de code ou fallback
Business n'est autorisé. Un module compilé est du code de confiance, pas une sandbox.

Le SDK livré expose `ISiteFactoryModule`, `ModuleDescriptor`, `ModuleId`,
`ModuleVersion` et `ModuleCapability`. Les dépendances, permissions, schémas,
jobs et autres contrats complets décrits ci-dessous restent des **contrats cibles**,
pas des champs déjà exposés par le descripteur minimal. La compatibilité par plages
d'ADR-0036 reste future ; le catalogue contrôle actuellement des versions exactes.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit l’architecture des modules de LogicGate SiteFactory.

Il précise :

- ce qu’est un module SiteFactory ;
- la différence entre module, capacité technique, template, section et extension client ;
- le contrat minimal d’un module ;
- son manifeste ;
- sa structure interne ;
- son enregistrement dans une application cliente ;
- ses dépendances ;
- sa configuration ;
- ses permissions ;
- ses routes et endpoints ;
- ses données et migrations ;
- ses événements ;
- ses tests ;
- sa distribution ;
- son versionnement ;
- son installation ;
- sa mise à niveau ;
- sa désactivation ;
- sa dépréciation ;
- les règles de sécurité ;
- les contraintes applicables à Codex.

Ce document constitue la référence architecturale commune à tous les modules SiteFactory.

Chaque module implémenté devra également posséder une spécification fonctionnelle et technique propre.

---

## 2. Vision des modules

Un module SiteFactory est une capacité fonctionnelle ou opérationnelle :

- identifiable ;
- versionnée ;
- configurable ;
- testable ;
- documentée ;
- installable de manière reproductible ;
- compatible avec un ou plusieurs packs ;
- intégrable dans une application client isolée.

Exemples :

```text
content-management
contact-form
email-notifications
lead-management
media-library
portfolio-basic
seo-baseline
legal-pages
operations-baseline
```

Un module peut apporter :

- des services ;
- des composants UI ;
- des pages ;
- des routes ;
- des endpoints ;
- des permissions ;
- des entités ;
- des configurations EF Core ;
- des options ;
- des événements ;
- des scripts ou documents d’exploitation ;
- des tests.

Un module ne constitue pas :

- une simple classe ;
- une section visuelle ;
- un thème ;
- un template sectoriel ;
- un projet client complet ;
- une condition dispersée dans le code ;
- un plugin arbitraire chargé dynamiquement ;
- une copie de code adaptée à un client précis.

---

## 3. Objectifs de l’architecture modulaire

L’architecture des modules doit permettre de :

- composer plusieurs packs à partir de capacités communes ;
- limiter la duplication entre projets clients ;
- publier des correctifs communs ;
- maîtriser les dépendances ;
- isoler les responsabilités ;
- faciliter les tests ;
- sécuriser les fonctionnalités ;
- appliquer des mises à niveau contrôlées ;
- préserver les personnalisations client ;
- rendre le catalogue exploitable par SiteFactory ;
- garder un monolithe simple à déployer.

---

## 4. Non-objectifs

Le système de modules du MVP ne cherche pas à fournir :

- un chargement dynamique de DLL ;
- un marketplace public ;
- une exécution de code tiers non vérifié ;
- une activation de modules téléchargés depuis une URL arbitraire ;
- une architecture microservices ;
- une isolation de processus par module ;
- une base de données indépendante par module ;
- un système de plugins à chaud ;
- une compatibilité avec des applications externes à SiteFactory ;
- une API publique stable pour des éditeurs tiers.

Les modules du MVP sont connus, validés et référencés à la compilation.

---

# 5. Terminologie

## 5.1 Module disponible

Le module existe dans le catalogue et son état permet éventuellement de l’utiliser.

```text
available
mvp
beta
planned
future
deprecated
retired
```

Un module `planned` ou `future` n’est pas utilisable dans une génération réelle.

---

## 5.2 Module sélectionné

Le module a été demandé explicitement dans la configuration client ou inclus par le pack.

---

## 5.3 Module résolu

Le moteur de catalogue a :

- vérifié son existence ;
- validé sa version ;
- résolu ses dépendances ;
- vérifié ses incompatibilités ;
- déterminé ses capacités finales.

---

## 5.4 Module installé

Le projet client contient :

- sa référence de package ;
- sa configuration ;
- ses enregistrements de services ;
- ses contributions au modèle ;
- ses routes ;
- ses fichiers nécessaires.

---

## 5.5 Module configuré

Les options obligatoires du module sont présentes et valides.

---

## 5.6 Module activé

Le module est autorisé à fournir ses capacités dans l’application.

L’activation ne remplace pas l’autorisation utilisateur.

---

## 5.7 Module autorisé

L’utilisateur courant possède les permissions nécessaires pour utiliser une capacité du module.

---

## 5.8 Capacité technique commune

Une capacité technique peut être obligatoire sans être vendue séparément.

Exemples :

```text
platform-core
public-site
theme-system
admin-identity
operations-baseline
```

Elle suit les mêmes principes de versionnement et de contrat qu’un module fonctionnel.

---

## 5.9 Extension client

Une extension client est un comportement spécifique :

- stocké dans le repository du client ;
- non distribué comme module générique ;
- isolé du socle ;
- composé par l’application cliente.

Une extension client peut consommer les contrats publics d’un module.

Un module générique ne dépend jamais d’une extension client.

---

# 6. Principes architecturaux

## 6.1 Un module possède une responsabilité cohérente

Un module doit couvrir un domaine fonctionnel ou opérationnel compréhensible.

Exemples cohérents :

```text
lead-management
portfolio-basic
email-notifications
```

Exemples à éviter :

```text
misc-tools
website-features
sample-common
various-services
```

Le nom et le périmètre doivent permettre de comprendre clairement ce que le module apporte.

---

## 6.2 Un package principal par module dans le MVP

La stratégie initiale est :

```text
un package NuGet principal versionné par module
```

Exemples :

```text
LogicGate.SiteFactory.Module.Leads
LogicGate.SiteFactory.Module.Content
LogicGate.SiteFactory.Module.Portfolio
```

Le package peut être structuré en namespaces internes :

```text
Domain
Application
Infrastructure
Presentation
Configuration
Contracts
```

Le MVP ne doit pas créer systématiquement :

```text
Module.Leads.Domain
Module.Leads.Application
Module.Leads.Infrastructure
Module.Leads.Web
```

si cette séparation n’apporte pas encore de valeur réelle.

---

## 6.3 Le module reste compatible avec le monolithe

Les modules sont composés dans une seule application ASP.NET Core.

Ils partagent :

- le processus ;
- le conteneur de dépendances ;
- le `DbContext` final ;
- les transactions locales ;
- la journalisation ;
- la configuration ;
- l’identité ;
- le déploiement.

Ils ne communiquent pas par appels réseau internes dans le MVP.

---

## 6.4 Les contrats sont explicites

Toute interaction entre modules doit passer par :

- un contrat public ;
- une dépendance déclarée ;
- un événement ;
- une interface ;
- un modèle partagé justifié.

Un module ne doit pas :

- utiliser directement une classe interne d’un autre module ;
- interroger directement les tables d’un autre module sans contrat ;
- accéder à ses services privés ;
- contourner ses permissions.

---

## 6.5 Les dépendances sont orientées

Une dépendance doit suivre une direction claire.

Exemple :

```text
sample-form
→ contact-form
→ lead-management
```

Une dépendance inverse ne doit pas être ajoutée pour faciliter ponctuellement une implémentation.

---

## 6.6 Pas de condition basée sur un client

Exemple interdit :

```csharp
if (clientSlug == "cabinet-alpha")
{
    // comportement particulier
}
```

Le comportement spécifique doit être fourni par :

- configuration ;
- stratégie ;
- extension client ;
- intégration isolée.

---

## 6.7 Sécurité et exploitation intégrées

Un module n’est pas considéré comme terminé s’il manque :

- ses protections ;
- ses permissions ;
- ses validations ;
- ses logs sobres ;
- ses tests ;
- ses impacts de migration ;
- sa documentation d’exploitation.

---

# 7. Structure recommandée d’un module

Structure conceptuelle :

```text
modules/
└── LogicGate.SiteFactory.Module.Leads/
    ├── Contracts/
    │   ├── ILeadService.cs
    │   ├── LeadSummary.cs
    │   └── LeadPermissions.cs
    │
    ├── Domain/
    │   ├── Entities/
    │   ├── ValueObjects/
    │   ├── Events/
    │   └── Errors/
    │
    ├── Application/
    │   ├── Commands/
    │   ├── Queries/
    │   ├── Handlers/
    │   ├── Services/
    │   └── Validation/
    │
    ├── Infrastructure/
    │   ├── Persistence/
    │   ├── Email/
    │   └── Services/
    │
    ├── Presentation/
    │   ├── Components/
    │   ├── Pages/
    │   ├── Endpoints/
    │   └── Navigation/
    │
    ├── Configuration/
    │   ├── LeadsOptions.cs
    │   ├── LeadsModuleRegistration.cs
    │   └── LeadsModelContributor.cs
    │
    ├── Resources/
    ├── module.yml
    ├── README.md
    └── LogicGate.SiteFactory.Module.Leads.csproj
```

Cette structure doit être adaptée à la complexité réelle du module.

Un module simple ne doit pas être artificiellement découpé en nombreux dossiers vides.

---

# 8. Manifeste du module

## 8.1 Fichier

Chaque module possède un manifeste versionné :

```text
module.yml
```

Le manifeste constitue la description machine du module.

Il ne remplace pas :

- le package ;
- les tests ;
- la documentation ;
- le catalogue commercial.

---

## 8.2 Exemple conceptuel

```yaml
schemaVersion: 1

module:
  id: lead-management
  displayName: Gestion des prospects
  packageId: LogicGate.SiteFactory.Module.Leads
  version: 1.0.0
  status: mvp
  category: commercial

compatibility:
  generator:
    minimumVersion: 0.1.0

  platform:
    minimumVersion: 1.0.0

  dotnet:
    minimumVersion: 10.0

packs:
  included:
    - sample-application
    - sample-automation

  optional:
    - site-pro

dependencies:
  required:
    - id: platform-core
      version: "[1.0.0,2.0.0)"

    - id: admin-identity
      version: "[1.0.0,2.0.0)"

    - id: contact-form
      version: "[1.0.0,2.0.0)"

incompatibilities: []

configuration:
  optionsType: LeadsOptions

permissions:
  - Leads.Read
  - Leads.Update
  - Leads.Export
  - Leads.AddNote

routes:
  admin:
    - /admin/leads
    - /admin/leads/{id}

data:
  entities:
    - Lead
    - LeadEvent
    - LeadNote

  contributesToDbContext: true
  requiresMigration: true

events:
  publishes:
    - LeadCreated
    - LeadStatusChanged

  consumes: []

operations:
  healthChecks: []
  backgroundServices: []

installation:
  requiresExplicitRegistration: true

upgrade:
  strategy: package-and-client-migration
```

Le format définitif sera validé par un schéma.

---

## 8.3 Champs obligatoires

Le manifeste doit contenir au minimum :

```text
schemaVersion
module.id
module.displayName
module.packageId
module.version
module.status
module.category
compatibility
dependencies
configuration
permissions
routes
data
installation
upgrade
```

Un module purement technique peut avoir certaines collections vides, mais elles doivent rester explicites lorsque le schéma l’exige.

---

## 8.4 Identifiant

L’identifiant du module :

- est stable ;
- utilise `kebab-case` ;
- ne contient pas le nom d’un client ;
- ne change pas avec le nom commercial ;
- est utilisé dans le catalogue, le lock file et les configurations.

Exemple :

```text
lead-management
```

---

## 8.5 Package ID

Le package ID identifie l’artefact NuGet.

Exemple :

```text
LogicGate.SiteFactory.Module.Leads
```

Le moteur ne doit pas déduire automatiquement l’identifiant produit depuis le nom du package.

Les deux informations restent explicites.

---

# 9. Validation du manifeste

La validation doit vérifier notamment :

- format du schéma ;
- identifiant ;
- unicité ;
- version SemVer ;
- statut ;
- catégorie ;
- package ID ;
- dépendances existantes ;
- plages de versions ;
- absence de cycle ;
- incompatibilités existantes ;
- permissions valides ;
- routes valides ;
- événements valides ;
- stratégie d’installation connue ;
- stratégie d’upgrade connue.

Un manifeste invalide empêche la publication du module.

---

# 10. Contrat d’enregistrement

## 10.1 Objectif

Chaque module doit fournir un point d’enregistrement explicite dans l’application cliente.

L’objectif est de centraliser :

- les services ;
- les options ;
- les validations ;
- les endpoints ;
- les composants de navigation ;
- les health checks ;
- les contributeurs EF Core.

---

## 10.2 Extension d’enregistrement recommandée

Exemple conceptuel :

```csharp
public static class LeadsModuleRegistration
{
    public static IServiceCollection AddLeadsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<LeadsOptions>()
            .Bind(configuration.GetSection(LeadsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<ILeadQueryService, LeadQueryService>();
        services.AddSingleton<IModelBuilderContributor, LeadsModelContributor>();

        return services;
    }
}
```

Cette forme est recommandée pour le MVP car elle reste :

- explicite ;
- facilement testable ;
- simple à comprendre ;
- compatible avec la compilation statique.

---

## 10.3 Enregistrement des endpoints

Exemple conceptuel :

```csharp
public static class LeadsEndpointRegistration
{
    public static IEndpointRouteBuilder MapLeadsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/admin/leads")
            .RequireAuthorization();

        group.MapGet("/", GetLeads);
        group.MapGet("/{id:guid}", GetLead);
        group.MapPost("/{id:guid}/status", UpdateStatus);

        return endpoints;
    }
}
```

Les permissions détaillées doivent être appliquées par endpoint.

---

## 10.4 Composition dans l’application cliente

Exemple conceptuel :

```csharp
builder.Services.AddSiteFactoryPlatform(configuration);
builder.Services.AddIdentityModule(configuration);
builder.Services.AddContactModule(configuration);
builder.Services.AddLeadsModule(configuration);

var app = builder.Build();

app.MapSiteFactoryPlatform();
app.MapIdentityModule();
app.MapContactModule();
app.MapLeadsModule();
```

La génération doit produire uniquement les enregistrements correspondant aux modules résolus.

---

## 10.5 Pas de scan arbitraire

Le MVP ne doit pas rechercher automatiquement tous les modules dans les assemblies avec réflexion.

L’enregistrement explicite est privilégié.

Une génération de code peut produire la composition finale.

---

# 11. Architecture interne du module

## 11.1 Domain

Le domaine du module contient :

- entités ;
- value objects ;
- invariants ;
- événements métier ;
- erreurs métier ;
- règles pures.

Il ne dépend pas :

- d’ASP.NET Core ;
- de Blazor ;
- d’EF Core lorsque cela peut être évité ;
- de SMTP ;
- du système de fichiers ;
- de l’application cliente.

---

## 11.2 Application

La couche Application contient :

- cas d’usage ;
- commandes ;
- requêtes ;
- handlers ;
- interfaces ;
- validation applicative ;
- orchestration.

Elle dépend du domaine du module et de contrats explicitement déclarés.

---

## 11.3 Infrastructure

Infrastructure contient :

- implémentations EF Core ;
- appels SMTP ;
- accès fichiers ;
- adaptateurs externes ;
- services techniques ;
- repositories ciblés lorsqu’ils sont utiles.

Elle ne contient pas les règles commerciales principales.

---

## 11.4 Presentation

Presentation contient :

- composants Blazor ;
- pages ;
- endpoints ;
- modèles UI ;
- navigation ;
- formulaires ;
- mapping vers les cas d’usage.

Elle ne doit pas accéder directement au `DbContext` lorsque cela contourne les services applicatifs et validations du module.

---

## 11.5 Contracts

Le dossier Contracts contient uniquement ce que d’autres modules ou extensions peuvent consommer légitimement.

Il faut éviter d’y placer toutes les classes publiques du module.

Un contrat public doit être :

- stable ;
- documenté ;
- limité ;
- testable ;
- versionné.

---

# 12. Dépendances entre modules

## 12.1 Dépendance requise

Le module ne peut pas fonctionner sans la dépendance.

Exemple :

```text
lead-management
→ admin-identity
```

---

## 12.2 Dépendance optionnelle

Le module peut enrichir son comportement lorsqu’une autre capacité est présente.

Exemple futur :

```text
lead-management
→ ai-starter optionnel
```

Une dépendance optionnelle ne doit pas rendre le comportement principal instable.

---

## 12.3 Dépendance de présentation

Une section ou page dépend d’une capacité du module.

Exemple :

```text
portfolio-preview
→ portfolio-basic
```

Cette relation est déclarée dans le catalogue des pages et sections.

---

## 12.4 Dépendance technique

Une dépendance technique est nécessaire à l’implémentation mais n’est pas forcément visible commercialement.

Exemple :

```text
portfolio-basic
→ media-library
```

Elle reste déclarée dans le manifeste et dans le catalogue.

---

## 12.5 Résolution

Le moteur doit :

1. charger les modules demandés ;
2. ajouter les dépendances requises ;
3. valider les plages de versions ;
4. détecter les cycles ;
5. détecter les incompatibilités ;
6. produire un ordre de composition ;
7. produire un plan lisible.

---

## 12.6 Cycles

Les cycles sont interdits.

Exemple invalide :

```text
content-management
→ media-library

media-library
→ content-management
```

La responsabilité doit être revue ou un contrat plus bas niveau doit être extrait.

---

## 12.7 Communication entre modules

Ordre de préférence :

```text
1. Contrat public synchrone
2. Événement applicatif
3. Donnée de contexte explicite
4. Extension du host client
```

Il faut éviter :

- l’accès direct aux tables ;
- les références aux services internes ;
- les événements globaux sans contrat ;
- les dépendances implicites.

---

# 13. Configuration des modules

## 13.1 Options typées

Chaque module configurable doit utiliser une classe d’options typée.

Exemple :

```csharp
public sealed class LeadsOptions
{
    public const string SectionName = "Modules:Leads";

    public string DefaultStatus { get; init; } = "New";

    public bool AllowCsvExport { get; init; } = true;

    public int MaximumExportRows { get; init; } = 10_000;
}
```

---

## 13.2 Validation au démarrage

Les options essentielles doivent être validées au démarrage.

Exemple :

```csharp
services
    .AddOptions<LeadsOptions>()
    .Bind(configuration.GetSection(LeadsOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.MaximumExportRows > 0,
        "MaximumExportRows must be greater than zero.")
    .ValidateOnStart();
```

---

## 13.3 Configuration cliente

Exemple conceptuel dans `client.sitefactory.yml` :

```yaml
modules:
  lead-management:
    enabled: true

    settings:
      defaultStatus: new
      allowCsvExport: true
      maximumExportRows: 10000
```

Le générateur transforme cette configuration vers le format applicatif attendu.

---

## 13.4 Secrets

Un module peut déclarer les noms de secrets requis.

Exemple :

```yaml
secrets:
  required:
    - Smtp__Password
```

Le manifeste et la configuration ne contiennent jamais la valeur du secret.

---

## 13.5 Valeurs par défaut

Les valeurs par défaut doivent être :

- sûres ;
- documentées ;
- compatibles avec le pack ;
- non destructrices ;
- testées.

Une valeur par défaut ne doit pas activer une capacité payante non sélectionnée.

---

# 14. Activation des modules

## 14.1 Installation versus activation

Un package peut être présent dans le projet sans que toutes ses capacités soient activées.

Toutefois, le MVP doit éviter de référencer inutilement des modules non vendus.

La stratégie recommandée est :

```text
module sélectionné
→ package référencé
→ module enregistré
→ configuration validée
→ capacités activées
```

---

## 14.2 Activation déclarative

L’activation provient de la configuration résolue.

Exemple :

```yaml
modules:
  portfolio-basic:
    enabled: true
```

---

## 14.3 Activation côté serveur

L’activation doit être vérifiée côté serveur.

Masquer une page ou un menu ne suffit pas.

Les protections s’appliquent aussi aux :

- endpoints ;
- handlers ;
- services ;
- tâches ;
- exports ;
- composants admin ;
- opérations de données.

---

## 14.4 Module désactivé

Un module désactivé ne doit pas :

- exposer ses routes fonctionnelles ;
- exécuter ses traitements ;
- lancer ses jobs ;
- afficher ses menus ;
- accepter ses commandes.

Ses données peuvent rester présentes pour permettre une réactivation ou un rollback.

La suppression physique des données suit un processus distinct.

---

# 15. Entitlements et droits commerciaux

La sélection commerciale d’un module ne doit pas reposer uniquement sur un booléen UI.

Le projet doit distinguer :

```text
module vendu
module installé
module activé
permission utilisateur
```

Le détail de cette distinction sera défini dans :

```text
10-feature-flags-and-entitlements.md
```

Un module générique doit fournir les points de contrôle nécessaires.

---

# 16. Permissions et autorisation

## 16.1 Convention de nommage

Format recommandé :

```text
{Module}.{Action}
```

Exemples :

```text
Leads.Read
Leads.Update
Leads.AddNote
Leads.Export

Content.Read
Content.Write
Content.Publish

Portfolio.Read
Portfolio.Manage
```

---

## 16.2 Déclaration

Les permissions sont déclarées dans :

- le manifeste ;
- le code du module ;
- les tests ;
- la documentation.

La liste doit rester cohérente.

---

## 16.3 Application

Les permissions doivent être appliquées à plusieurs niveaux lorsque nécessaire :

```text
Navigation
Page
Endpoint
Commande
Service
Export
Action
```

---

## 16.4 Policy-based authorization

L’autorisation ASP.NET Core par politiques est recommandée.

Exemple conceptuel :

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy(
        LeadPermissions.Read,
        policy => policy.RequireClaim(
            SiteFactoryClaimTypes.Permission,
            LeadPermissions.Read));
});
```

L’implémentation exacte sera définie dans le document Identity et autorisation.

---

## 16.5 Absence d’autorisation

En cas d’autorisation insuffisante :

- l’action est refusée côté serveur ;
- l’UI peut masquer l’action ;
- aucune donnée sensible n’est retournée ;
- l’événement de sécurité reste sobre.

---

# 17. Routes et endpoints

## 17.1 Préfixes

Les routes doivent respecter des conventions prévisibles.

Exemples :

```text
/admin/leads
/admin/content
/admin/portfolio

/api/admin/leads
/api/admin/content
```

Les routes publiques dépendent des pages et templates.

---

## 17.2 Routes admin

Les routes admin doivent :

- exiger une authentification ;
- appliquer les permissions ;
- être exclues de l’indexation ;
- ne pas exposer de PII dans l’URL ;
- utiliser des identifiants techniques.

Exemple à éviter :

```text
/admin/leads/jean.dupont@example.com
```

Exemple recommandé :

```text
/admin/leads/0bbf52f1-1fd5-4e12-bda7-bd2ad7db73f3
```

---

## 17.3 Endpoints mutables

Les modifications doivent utiliser les verbes HTTP adaptés.

Exemples :

```text
POST
PUT
PATCH
DELETE
```

Une action destructive ou mutable ne doit pas être réalisée par un simple GET.

---

## 17.4 Antiforgery

Les formulaires et endpoints concernés doivent appliquer l’antiforgery selon le mode d’hébergement et la stratégie ASP.NET Core retenus.

---

## 17.5 Versionnement d’API

Un versionnement public d’API n’est pas obligatoire dans le MVP si les endpoints restent internes à l’application.

Les contrats exposés à des intégrations externes nécessitent une stratégie dédiée.

---

# 18. Navigation fournie par un module

Un module peut contribuer à la navigation d’administration.

Exemple conceptuel :

```csharp
public sealed record AdminNavigationItem(
    string Id,
    string Label,
    string Href,
    string RequiredPermission,
    int Order);
```

Le host client compose les entrées des modules installés.

Le module ne doit pas modifier directement un menu global avec du code caché.

La contribution doit être explicite et testable.

---

# 19. Données et EF Core

## 19.1 DbContext final

Le projet client possède le `DbContext` final.

Les modules contribuent au modèle sans posséder un historique de migrations séparé appliqué indépendamment dans le MVP.

---

## 19.2 Contributeur de modèle

Interface conceptuelle :

```csharp
public interface IModelBuilderContributor
{
    string ModuleId { get; }

    void Configure(ModelBuilder modelBuilder);
}
```

Exemple :

```csharp
public sealed class LeadsModelContributor
    : IModelBuilderContributor
{
    public string ModuleId => "lead-management";

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(
            new LeadEntityConfiguration());

        modelBuilder.ApplyConfiguration(
            new LeadEventEntityConfiguration());
    }
}
```

---

## 19.3 Enregistrement dans le DbContext

Exemple conceptuel :

```csharp
public sealed class ClientDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IEnumerable<IModelBuilderContributor> _contributors;

    public ClientDbContext(
        DbContextOptions<ClientDbContext> options,
        IEnumerable<IModelBuilderContributor> contributors)
        : base(options)
    {
        _contributors = contributors;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var contributor in _contributors)
        {
            contributor.Configure(modelBuilder);
        }
    }
}
```

Cette solution devra être vérifiée vis-à-vis des contraintes de conception EF Core et de création de migrations.

Une approche explicite générée peut être préférée si elle simplifie les outils EF.

---

## 19.4 Migrations client

Le générateur doit produire ou préparer le `DbContext` final.

Les migrations sont créées dans :

```text
ClientName.Data/Migrations/
```

Elles représentent la composition réelle des modules installés.

---

## 19.5 Installation d’un module avec données

Le processus est :

```text
Ajouter le package
→ enregistrer le module
→ enregistrer son contributeur EF
→ mettre à jour le DbContext
→ créer une migration client
→ inspecter la migration
→ tester
→ déployer de manière contrôlée
```

---

## 19.6 Désinstallation

La désinstallation ne doit pas supprimer automatiquement :

- tables ;
- colonnes ;
- données ;
- fichiers ;
- médias.

Le retrait du package et la suppression des données sont deux opérations distinctes.

Une procédure de nettoyage éventuelle nécessite :

- plan ;
- backup ;
- validation ;
- migration explicite ;
- rollback.

---

# 20. Conventions de données d’un module

Chaque module doit documenter :

- ses entités ;
- ses identifiants ;
- ses dates ;
- ses index ;
- ses contraintes ;
- ses données personnelles ;
- sa rétention ;
- ses règles de suppression ;
- ses événements ;
- ses besoins de transaction.

Les conventions transversales sont définies dans :

```text
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
```

---

# 21. Événements

## 21.1 Objectif

Les événements permettent de signaler qu’un fait significatif s’est produit.

Exemples :

```text
LeadCreated
LeadStatusChanged
ContentPublished
PortfolioItemPublished
EmailDeliveryFailed
```

---

## 21.2 Événement métier

Un événement métier décrit un fait du domaine.

Exemple conceptuel :

```csharp
public sealed record LeadCreated(
    Guid LeadId,
    DateTimeOffset CreatedAtUtc);
```

L’événement ne doit pas transporter inutilement :

- nom ;
- email ;
- téléphone ;
- message ;
- contenu personnel.

---

## 21.3 Événement applicatif

Un événement applicatif peut déclencher une action au sein du monolithe.

Exemple :

```text
LeadCreated
→ notification interne
```

---

## 21.4 Pas de bus distribué dans le MVP

Les événements sont traités dans le processus de l’application.

Le MVP n’utilise pas :

- Kafka ;
- RabbitMQ ;
- Azure Service Bus ;
- bus distribué externe.

Un mécanisme interne simple et explicite est suffisant.

---

## 21.5 Fiabilité

Lorsqu’une action secondaire ne doit pas compromettre l’action principale, les deux résultats doivent être séparés.

Exemple :

```text
Lead persisté avec succès
Email de notification échoué
```

La soumission ne doit pas être annulée si la règle produit prévoit que la persistance reste prioritaire.

---

## 21.6 Outbox future

Une stratégie Outbox peut être étudiée si des automatisations fiables ou intégrations externes le nécessitent.

Elle est hors du MVP initial tant qu’aucun besoin concret ne la justifie.

---

# 22. Services d’arrière-plan

## 22.1 MVP

Les modules du MVP doivent limiter les services d’arrière-plan.

Ils sont autorisés lorsqu’un besoin réel existe :

- nettoyage planifié ;
- envoi différé ;
- traitement d’automatisation ;
- reprise après échec.

---

## 22.2 Déclaration

Un module apportant un service d’arrière-plan doit déclarer :

- son identifiant ;
- son rôle ;
- sa fréquence ;
- sa stratégie de concurrence ;
- son idempotence ;
- ses retries ;
- son arrêt propre ;
- son observabilité ;
- son impact sur les données.

---

## 22.3 Hébergement multiple

Un traitement planifié doit tenir compte du fait que plusieurs instances applicatives pourraient exister à l’avenir.

Le MVP peut fonctionner sur une instance unique, mais la limitation doit être documentée.

---

# 23. Fichiers et médias

Un module peut contribuer à des dossiers persistants.

Exemples :

```text
App_Data/media
App_Data/exports
App_Data/documents
```

Le module doit documenter :

- emplacement ;
- formats ;
- taille ;
- permissions ;
- backup ;
- restore ;
- nettoyage ;
- données personnelles éventuelles.

Un module ne doit pas écrire arbitrairement dans le système de fichiers.

---

# 24. Health checks

Un module dépendant d’une ressource essentielle peut fournir un health check.

Exemples :

```text
PostgreSQL
stockage persistant
service SMTP critique selon besoin
```

Le health check public ne doit pas exposer :

- serveur ;
- credentials ;
- noms de fichiers sensibles ;
- stack trace ;
- informations internes détaillées.

Les détails peuvent être limités aux opérateurs autorisés.

---

# 25. Logs

## 25.1 Événements structurés

Chaque module doit définir ses principaux événements techniques.

Exemple pour Leads :

```text
LeadCreated
LeadStatusUpdated
LeadExportGenerated
LeadExportFailed
```

---

## 25.2 Données interdites

Les logs d’un module ne doivent pas contenir :

- contenu de formulaire ;
- nom complet ;
- email ;
- téléphone ;
- adresse ;
- contenu d’un document ;
- secret ;
- token ;
- connection string.

---

## 25.3 Identifiants techniques

Les identifiants internes peuvent être utilisés lorsque nécessaire :

```text
LeadId
CorrelationId
ModuleId
OperationId
```

---

## 25.4 Niveaux de logs

Utilisation recommandée :

```text
Trace       détails de développement très limités
Debug       diagnostic local
Information événements normaux significatifs
Warning     situation dégradée mais contrôlée
Error       opération échouée
Critical    disponibilité ou intégrité fortement menacée
```

Un comportement attendu ne doit pas produire des erreurs répétées inutiles.

---

# 26. Erreurs

Chaque module doit définir des codes d’erreur stables.

Format recommandé :

```text
SF-{MODULE}-{NUMBER}
```

Exemples :

```text
SF-LEADS-001
SF-CONTENT-004
SF-PORTFOLIO-002
```

Une erreur doit préciser :

- code ;
- message public ;
- détail interne sobre ;
- action corrective lorsque possible.

Elle ne doit pas exposer de données sensibles.

---

# 27. Dépendances NuGet externes

Un module peut ajouter une dépendance externe uniquement si elle est justifiée.

L’analyse doit couvrir :

- licence ;
- maintenance ;
- sécurité ;
- compatibilité .NET ;
- dépendances transitives ;
- taille ;
- usage ;
- alternatives natives ;
- impact sur les projets clients.

Une dépendance externe structurante nécessite une validation humaine et éventuellement une ADR.

---

# 28. Distribution des modules

## 28.1 Feed privé

Les packages SiteFactory sont publiés dans un feed NuGet privé contrôlé par LogicGate.

---

## 28.2 Contenu d’un package

Un package peut contenir :

- assemblies ;
- composants Razor ;
- CSS isolé ou assets ;
- fichiers de contenu nécessaires ;
- documentation package ;
- métadonnées ;
- analyzers ciblés si justifiés.

Il ne contient pas :

- secret ;
- configuration de production ;
- données client ;
- dump ;
- médias clients ;
- fichiers `.env` réels.

---

## 28.3 Source Link et symboles

La publication de symboles et Source Link peut être envisagée pour faciliter le diagnostic interne.

Elle doit rester compatible avec la confidentialité du repository.

---

## 28.4 Immutabilité

Une version publiée ne doit pas être remplacée silencieusement.

Toute correction produit une nouvelle version.

---

# 29. Versionnement

Les modules utilisent SemVer :

```text
MAJOR.MINOR.PATCH
```

## 29.1 PATCH

Exemples :

- correction de bug compatible ;
- correction de sécurité compatible ;
- amélioration de logs ;
- correction responsive ;
- optimisation sans changement de contrat.

---

## 29.2 MINOR

Exemples :

- nouvelle capacité compatible ;
- nouveau champ optionnel ;
- nouvelle permission optionnelle ;
- nouvel endpoint compatible ;
- nouvelle configuration avec valeur par défaut sûre.

---

## 29.3 MAJOR

Exemples :

- suppression d’un contrat ;
- renommage d’une permission ;
- modification incompatible d’une entité ;
- changement nécessitant une migration manuelle ;
- suppression d’une route ;
- changement de comportement fonctionnel incompatible.

---

## 29.4 Version pré-1.0

Pendant la stabilisation :

```text
0.x.y
```

les changements peuvent être fréquents.

Ils doivent toutefois rester documentés et ne pas justifier des ruptures silencieuses.

---

# 30. Compatibilité

Un module doit déclarer sa compatibilité avec :

- générateur ;
- plateforme ;
- .NET ;
- ASP.NET Core ;
- design system lorsque pertinent ;
- autres modules ;
- template lorsque nécessaire ;
- schéma de configuration.

Une incompatibilité doit être détectée avant génération ou upgrade.

---

# 31. Installation d’un module

## 31.1 Plan d’installation

L’installation doit produire un plan incluant :

- package à ajouter ;
- version ;
- dépendances ;
- enregistrements ;
- configuration ;
- permissions ;
- pages ;
- routes ;
- migrations ;
- fichiers ;
- tests ;
- documentation ;
- risques.

---

## 31.2 Étapes

```text
1. Vérifier le catalogue
2. Vérifier le pack
3. Vérifier les dépendances
4. Vérifier les versions
5. Ajouter le package
6. Mettre à jour client.sitefactory.yml
7. Mettre à jour la composition du host
8. Mettre à jour le DbContext si nécessaire
9. Créer la migration client
10. Mettre à jour les permissions
11. Générer ou activer les pages
12. Exécuter le build
13. Exécuter les tests
14. Mettre à jour le lock file
15. Produire le rapport
```

---

## 31.3 Validation humaine

L’ajout d’un module à un projet client nécessite une validation commerciale et technique lorsqu’il modifie :

- le prix ;
- le périmètre ;
- les données ;
- les permissions ;
- l’exploitation ;
- les migrations.

---

# 32. Mise à niveau d’un module

## 32.1 Vérification

```bash
sitefactory upgrade check
```

doit identifier :

- version installée ;
- versions disponibles ;
- compatibilités ;
- correctifs de sécurité ;
- changements majeurs.

---

## 32.2 Plan

```bash
sitefactory upgrade plan
```

doit présenter :

- version source ;
- version cible ;
- dépendances affectées ;
- contrats modifiés ;
- configuration modifiée ;
- permissions modifiées ;
- migration nécessaire ;
- fichiers affectés ;
- tests requis ;
- actions manuelles ;
- rollback.

---

## 32.3 Application

L’upgrade est appliqué :

- sur une branche dédiée ;
- avec un repository propre ;
- après validation ;
- avec backup si données affectées ;
- sans déploiement automatique.

---

## 32.4 Mise à jour du lock file

Le lock file est mis à jour uniquement après résolution et application réussies.

---

## 32.5 Échec

En cas d’échec :

- le lock file ne doit pas déclarer la nouvelle version comme installée ;
- le rapport doit être en échec ;
- les conflits doivent être conservés explicitement ;
- le projet ne doit pas être déployé.

---

# 33. Désactivation d’un module

La désactivation peut être temporaire ou définitive.

## 33.1 Désactivation temporaire

Elle peut :

- masquer les entrées de navigation ;
- refuser les actions ;
- arrêter les jobs ;
- rendre certaines routes indisponibles.

Les données restent conservées.

---

## 33.2 Retrait commercial

Un module peut ne plus être vendu tout en restant installé pour un client existant.

Le catalogue doit distinguer :

```text
non disponible pour nouveaux projets
encore supporté pour projets existants
```

---

## 33.3 Désinstallation technique

Elle doit être précédée d’un plan.

Elle ne supprime jamais automatiquement les données dans le MVP.

---

# 34. Dépréciation et retrait

## 34.1 Dépréciation

Un module déprécié doit préciser :

- version de dépréciation ;
- raison ;
- remplacement ;
- période de support ;
- guide de migration ;
- impacts.

---

## 34.2 Retrait

Un module `retired` :

- n’est pas installable sur un nouveau projet ;
- peut rester référencé par un ancien projet ;
- nécessite une décision de support ;
- ne doit pas disparaître du feed si cela casse les restaurations reproductibles.

---

## 34.3 Suppression d’une version

Une ancienne version ne doit pas être supprimée du feed tant qu’un projet client supporté en dépend, sauf incident de sécurité majeur et procédure contrôlée.

---

# 35. Documentation propre au module

Chaque module doit posséder un `README.md` couvrant au minimum :

```text
Objectif
Périmètre
Hors périmètre
Identifiant
Package
Version
Dépendances
Configuration
Permissions
Routes
Données
Migrations
Événements
Logs
Health checks
Installation
Tests
Upgrade
Désactivation
Risques
```

Les modules complexes peuvent posséder une documentation dédiée sous :

```text
docs/modules/{module-id}/
```

---

# 36. Tests obligatoires d’un module

## 36.1 Tests du manifeste

Vérifier :

- identifiant ;
- version ;
- statut ;
- dépendances ;
- compatibilités ;
- permissions ;
- routes ;
- données ;
- stratégie d’upgrade.

---

## 36.2 Tests unitaires

Couvrir :

- invariants ;
- services métier ;
- validation ;
- cas d’usage ;
- erreurs ;
- transitions.

---

## 36.3 Tests d’intégration

Couvrir selon le module :

- EF Core ;
- PostgreSQL ;
- endpoints ;
- Identity ;
- permissions ;
- fichiers ;
- SMTP simulé ;
- médias ;
- événements.

---

## 36.4 Tests d’autorisation

Vérifier :

- utilisateur anonyme ;
- utilisateur authentifié sans permission ;
- utilisateur autorisé ;
- route ;
- endpoint ;
- action ;
- export.

---

## 36.5 Tests de migration

Lorsqu’un module affecte les données :

- création initiale ;
- upgrade ;
- contraintes ;
- index ;
- données existantes ;
- rollback ou restauration.

---

## 36.6 Tests de composition

Vérifier le module :

- seul lorsque possible ;
- avec ses dépendances ;
- dans le pack prévu ;
- avec les templates compatibles ;
- avec les personnalisations prévues.

---

## 36.7 Tests du projet généré

La CI SiteFactory doit générer un projet de référence incluant les modules MVP puis exécuter :

```text
restore
build Release
tests
migration validation
```

---

# 37. Tests d’architecture

Les tests doivent notamment vérifier :

```text
Un module ne référence pas le CLI.

Un module générique ne référence aucun projet client.

Un module ne dépend pas d’un autre module non déclaré.

Les namespaces internes ne sont pas consommés directement
par les autres modules.

Les contrats publics sont limités.

Les migrations restent dans le repository client.

Les composants de présentation ne contiennent pas
la persistance principale.
```

---

# 38. Sécurité d’un module

Chaque spécification de module doit analyser :

- données personnelles ;
- authentification ;
- autorisation ;
- validation ;
- antiforgery ;
- rate limiting ;
- uploads ;
- secrets ;
- logs ;
- exports ;
- rétention ;
- suppression ;
- dépendances externes ;
- sauvegarde ;
- restauration.

Un module ne peut pas passer à l’état `available` sans revue de sécurité adaptée.

---

# 39. Données personnelles

Un module traitant des données personnelles doit documenter :

- données collectées ;
- finalité ;
- champs obligatoires ;
- stockage ;
- accès ;
- export ;
- rétention ;
- suppression ;
- sauvegarde ;
- journalisation ;
- services tiers.

Les données personnelles ne doivent pas être ajoutées au manifeste ou au lock file.

---

# 40. Opérations et exploitation

Un module ayant un impact opérationnel doit contribuer à la documentation du projet client.

Exemples :

```text
variables requises
health checks
backup
restore
logs
dossiers persistants
tâches planifiées
incidents courants
dépendances externes
```

Le générateur doit pouvoir inclure ces informations dans le runbook client.

---

# 41. Catalogue initial des modules MVP

## Capacités techniques

```text
platform-core
public-site
theme-system
admin-identity
operations-baseline
```

## Modules fonctionnels

```text
content-management
contact-form
email-notifications
seo-baseline
legal-pages
lead-management
media-library
portfolio-basic
conversion-toolkit
```

## Modules planifiés

```text
sample-form
portfolio-advanced
seo-local-enhanced
lead-qualification-rules
workflow-automation
email-sequences
```

## Modules futurs ou sur devis

```text
appointment-booking
blog
multilingual
crm-connector
client-portal
payments
ai-starter
```

Un module planifié ne doit pas être simulé comme disponible dans le code de production.

---

# 42. Exemple d’architecture du module Leads

## Responsabilités

```text
Stocker les demandes
Lister les demandes
Consulter une demande
Modifier son statut
Ajouter des notes
Conserver un historique
Exporter selon permission
```

## Hors périmètre

```text
Campagnes marketing
CRM complet
Synchronisation externe
Automatisations avancées
IA obligatoire
Suppression définitive publique
```

## Dépendances

```text
platform-core
admin-identity
contact-form
```

## Permissions

```text
Leads.Read
Leads.Update
Leads.AddNote
Leads.Export
```

## Données

```text
Lead
LeadEvent
LeadNote
```

## Événements

```text
LeadCreated
LeadStatusChanged
LeadNoteAdded
```

## Routes

```text
/admin/leads
/admin/leads/{id}
```

Cet exemple n’est pas une spécification complète du module Leads.

---

# 43. Règles pour Codex

## 43.1 Lecture préalable

Avant toute modification d’un module, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
module-catalog.md
../adr/0009-client-customizations-and-extension-points.md
core-module-boundary.md
09-module-architecture.md
AGENTS.md
```

Ainsi que :

- le manifeste du module ;
- sa documentation ;
- ses tests ;
- les modules dépendants ;
- le modèle de données ;
- les ADR applicables.

---

## 43.2 Ce que Codex peut faire

Codex peut :

- auditer un module ;
- créer un manifeste demandé ;
- implémenter un contrat validé ;
- ajouter une permission ;
- ajouter un cas d’usage ;
- créer un contributeur EF Core ;
- ajouter des endpoints ;
- ajouter des tests ;
- corriger une dépendance ;
- préparer un plan d’upgrade ;
- documenter les impacts.

---

## 43.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- créer un nouveau module sans validation ;
- ajouter un module au catalogue sans demande ;
- renommer un identifiant stable ;
- modifier un prix ;
- ajouter une dépendance implicite ;
- introduire un cycle ;
- utiliser les internals d’un autre module ;
- ajouter une condition basée sur un client ;
- déplacer les migrations dans le package générique ;
- charger dynamiquement des assemblies ;
- supprimer des données lors d’une désinstallation ;
- rendre une route publique pour résoudre un problème d’accès ;
- supprimer une permission ;
- désactiver l’antiforgery ;
- exposer de PII dans les logs ;
- publier un package sans tests ;
- appliquer un upgrade directement en production.

---

## 43.4 Décision requise

Codex doit interrompre ou signaler le travail lorsqu’il identifie :

- une nouvelle capacité générique ;
- une dépendance structurante ;
- un breaking change ;
- une nouvelle donnée personnelle ;
- une nouvelle intégration externe ;
- une migration destructive ;
- une nouvelle permission sensible ;
- une incompatibilité entre modules ;
- une décision de dépréciation ;
- une modification du modèle de distribution.

---

## 43.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Module concerné
Version actuelle
Contrats modifiés
Dépendances modifiées
Configuration modifiée
Permissions modifiées
Routes modifiées
Données affectées
Migration nécessaire
Événements affectés
Tests ajoutés
Build
Tests
Breaking changes
Impact upgrade
Impact exploitation
Décisions restant humaines
```

---

# 44. Anti-patterns interdits

## 44.1 Module fourre-tout

```text
general-features
sample-common
misc
```

---

## 44.2 Dépendance non déclarée

Un module utilise directement une classe d’un autre package sans déclaration dans le manifeste.

---

## 44.3 Accès direct aux tables d’un autre module

Exemple :

```csharp
dbContext.Set<Lead>()
```

depuis un module non autorisé à dépendre de Leads.

---

## 44.4 Migration embarquée appliquée automatiquement

Le package ne doit pas appliquer ses migrations au démarrage.

---

## 44.5 Permission uniquement visuelle

Masquer un bouton sans protéger le handler ou l’endpoint.

---

## 44.6 Module spécifique à un client

```text
LogicGate.SiteFactory.Module.CabinetAlpha
```

La fonctionnalité doit rester dans `ClientName.Customizations` si elle n’est pas générique.

---

## 44.7 Service statique global

Un état mutable global partagé par plusieurs modules sans contrat ou cycle de vie explicite.

---

## 44.8 Événement contenant des données personnelles

```csharp
public record LeadCreated(
    string Email,
    string Phone,
    string Message);
```

---

## 44.9 Options non validées

Un module démarre avec une configuration incohérente et échoue plus tard silencieusement.

---

## 44.10 Suppression automatique à la désinstallation

Le retrait d’un package ne doit pas déclencher la suppression de ses tables ou fichiers.

---

# 45. Critères de publication d’un module

Un module ne peut passer à l’état `available` que si :

```text
[ ] Identifiant stable
[ ] Version définie
[ ] Manifeste valide
[ ] Responsabilité claire
[ ] Hors périmètre défini
[ ] Dépendances déclarées
[ ] Compatibilités déclarées
[ ] Configuration validée
[ ] Permissions définies
[ ] Routes protégées
[ ] Données documentées
[ ] Migration client validée
[ ] Événements documentés
[ ] Logs sobres
[ ] Health checks définis si nécessaires
[ ] Tests unitaires
[ ] Tests d’intégration
[ ] Tests d’autorisation
[ ] Tests de composition
[ ] Documentation
[ ] Installation documentée
[ ] Upgrade documenté
[ ] Désactivation documentée
[ ] Revue sécurité
[ ] Build Release réussi
[ ] Aucun warning injustifié
[ ] Aucun secret
[ ] Aucune donnée client réelle
```

---

# 46. Critères d’acceptation de l’architecture modulaire MVP

L’architecture des modules sera considérée comme opérationnelle lorsque :

1. un schéma de manifeste existe ;
2. les manifests peuvent être validés ;
3. les dépendances peuvent être résolues ;
4. les cycles sont détectés ;
5. les incompatibilités sont détectées ;
6. chaque module possède un package explicite ;
7. l’enregistrement dans le host est explicite ;
8. les options sont typées et validées ;
9. les permissions sont déclarées ;
10. les routes sont protégées ;
11. les modules peuvent contribuer au modèle EF Core ;
12. les migrations restent dans le projet client ;
13. les événements ne transportent pas de PII inutile ;
14. les logs restent sobres ;
15. le lock file enregistre les versions ;
16. un module peut être ajouté par un plan contrôlé ;
17. un upgrade peut être planifié ;
18. une désactivation ne supprime pas les données ;
19. les tests d’architecture empêchent les dépendances interdites ;
20. le projet de référence compile avec les modules MVP.

---

# 47. Décisions validées

La baseline de l’architecture des modules est la suivante :

```text
Un module représente une capacité cohérente et versionnée.

Le MVP utilise un package principal par module.

Les modules sont référencés à la compilation.

Le MVP n’utilise pas de chargement dynamique de plugins.

Chaque module possède un manifeste YAML validé.

Les dépendances sont déclarées dans le manifeste et le catalogue.

Les cycles de dépendance sont interdits.

L’enregistrement des modules est explicite.

Les modules utilisent des options typées et validées.

Les modules déclarent leurs permissions.

La protection des routes et services est obligatoire.

Le DbContext final appartient au projet client.

Les modules contribuent au modèle EF Core.

Les migrations appartiennent au repository client.

La désactivation d’un module ne supprime pas ses données.

Les mises à niveau sont planifiées et jamais automatiques en production.

Les événements internes ne doivent pas transporter de PII inutile.

Les modules génériques ne dépendent jamais d’une extension client.

Une fonctionnalité propre à un client reste dans Customizations.

Codex ne crée pas seul un nouveau module ou un breaking change.
```

---

# 48. Checklist de revue d’un module

## Contrat

```text
[ ] Responsabilité claire
[ ] Hors périmètre défini
[ ] Identifiant stable
[ ] Version SemVer
[ ] Manifeste valide
[ ] Package ID cohérent
```

## Dépendances

```text
[ ] Dépendances déclarées
[ ] Versions compatibles
[ ] Aucun cycle
[ ] Aucune dépendance implicite
[ ] Aucun accès aux internals d’un autre module
[ ] Aucune dépendance vers un projet client
```

## Configuration

```text
[ ] Options typées
[ ] Valeurs par défaut sûres
[ ] Validation au démarrage
[ ] Aucun secret dans la configuration
[ ] Configuration documentée
```

## Sécurité

```text
[ ] Permissions définies
[ ] Routes protégées
[ ] Endpoints protégés
[ ] Antiforgery appliqué
[ ] Validation serveur
[ ] Aucun secret loggué
[ ] Aucune PII logguée
[ ] Exports protégés
```

## Données

```text
[ ] Entités documentées
[ ] Index définis
[ ] Données personnelles identifiées
[ ] Contributeur EF Core présent
[ ] Migration client générée
[ ] Backup et restore considérés
[ ] Pas de suppression automatique
```

## Qualité

```text
[ ] Tests unitaires
[ ] Tests d’intégration
[ ] Tests d’autorisation
[ ] Tests de migration
[ ] Tests de composition
[ ] Build Release réussi
[ ] Aucun warning injustifié
```

## Cycle de vie

```text
[ ] Installation documentée
[ ] Activation documentée
[ ] Upgrade documenté
[ ] Désactivation documentée
[ ] Dépréciation prévue
[ ] Lock file mis à jour
[ ] Documentation à jour
```

---

# 49. Conclusion

L’architecture des modules SiteFactory doit permettre de construire des applications clientes composables sans introduire la complexité d’un système de plugins dynamique.

Elle repose sur :

- des packages versionnés ;
- des manifests explicites ;
- des dépendances déclarées ;
- une composition statique ;
- des contrats limités ;
- des permissions ;
- des contributions EF Core contrôlées ;
- des migrations appartenant au projet client ;
- des upgrades planifiés ;
- des tests systématiques.

> Un module SiteFactory n’est pas seulement du code réutilisable. C’est une capacité produit complète, versionnée, sécurisée, documentée et exploitable tout au long du cycle de vie des projets clients.
