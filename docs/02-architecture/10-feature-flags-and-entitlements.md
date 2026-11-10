# LogicGate SiteFactory — Feature flags et droits fonctionnels

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Feature flags et droits fonctionnels |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, architectes, développeurs, contributeurs, Codex |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../adr/0006-feature-flags-and-entitlements-separation.md`, `../adr/0006-feature-flags-and-entitlements-separation.md`, `core-module-boundary.md`, `09-module-architecture.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Périmètre public actuel et contrat cible

Une `ModuleCapability` du [SDK](module-sdk.md) est une métadonnée descriptive,
pas une autorisation, un entitlement ni un moteur d'activation. Le catalogue
liste les capacités déclarées et contrôle les identités/versions exactes.
La configuration V1 exige encore `modules.enabled: []`.

**Le moteur complet de feature flags, packs, facturation et entitlements décrit
ci-dessous n'est pas livré par la distribution publique.** Ses distinctions de
sécurité restent normatives : disponibilité technique, droit fonctionnel et
autorisation utilisateur ne sont jamais interchangeables. Aucun exemple de
pack ou module fictif ne constitue un produit disponible.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit la gestion des capacités fonctionnelles dans LogicGate SiteFactory.

Il précise notamment la distinction entre :

- fonctionnalité disponible dans le catalogue ;
- fonctionnalité vendue au client ;
- module sélectionné ;
- module installé ;
- module configuré ;
- module activé ;
- fonctionnalité temporairement désactivée ;
- utilisateur autorisé ;
- fonctionnalité opérationnellement disponible.

Il décrit également :

- les feature flags ;
- les entitlements, ou droits fonctionnels commerciaux ;
- les permissions utilisateurs ;
- les règles de calcul de l’état effectif ;
- les sources de vérité ;
- les contrôles côté serveur ;
- les contrôles de l’interface ;
- les mécanismes de désactivation d’urgence ;
- la gestion des environnements ;
- les exigences de sécurité ;
- les tests obligatoires ;
- les contraintes applicables à Codex.

L’objectif est d’éviter qu’une fonctionnalité soit considérée comme protégée uniquement parce qu’elle est absente d’un menu ou masquée dans l’interface.

---

## 2. Principe fondamental

Une fonctionnalité est utilisable uniquement lorsque toutes les conditions nécessaires sont réunies.

Formule conceptuelle :

```text
Fonctionnalité effective
=
Disponible dans le catalogue
ET compatible avec le projet
ET vendue ou autorisée commercialement
ET installée
ET configurée correctement
ET activée
ET non désactivée par un kill switch
ET autorisée pour l’utilisateur
ET opérationnellement disponible
```

Une interface masquée ne constitue pas une protection.

Une protection complète peut nécessiter des contrôles sur :

- la navigation ;
- les pages ;
- les routes ;
- les endpoints ;
- les commandes ;
- les handlers ;
- les services ;
- les traitements d’arrière-plan ;
- les exports ;
- les accès aux données ;
- les intégrations ;
- les événements.

---

## 3. Objectifs

Le système doit permettre de :

- garantir que seules les capacités vendues sont utilisables ;
- empêcher l’activation d’un module non installé ;
- empêcher l’activation d’un module non autorisé commercialement ;
- gérer les permissions par utilisateur ou rôle ;
- désactiver temporairement une capacité sans supprimer ses données ;
- protéger les routes et services côté serveur ;
- produire une configuration reproductible ;
- limiter les incohérences entre catalogue, projet et production ;
- faciliter les diagnostics ;
- simplifier les mises à niveau ;
- préserver la sécurité en cas de mauvaise configuration ;
- éviter la duplication des règles d’activation.

---

## 4. Non-objectifs

Le MVP ne cherche pas à fournir :

- un serveur central de licences ;
- une facturation automatique en fonction des usages ;
- une plateforme SaaS multi-tenant ;
- un moteur de feature flags distant ;
- des expérimentations A/B publiques complexes ;
- un ciblage comportemental ;
- une activation dynamique de packages non installés ;
- un marketplace de modules ;
- un système de licence cryptographique distribué ;
- une gestion commerciale automatisée sans validation humaine.

Chaque projet client reste une application isolée.

Les droits fonctionnels sont principalement résolus lors de la génération et du déploiement.

---

# 5. Terminologie

## 5.1 Feature

Une feature est une capacité fonctionnelle identifiable.

Exemples :

```text
leads.read
leads.export
portfolio.public-list
content.publish
quote-form.multi-step
automation.email-sequences
```

Une feature peut être :

- fournie par un module ;
- liée à un pack ;
- configurable ;
- soumise à permission ;
- temporairement désactivable.

---

## 5.2 Module

Un module est une capacité produit versionnée distribuée sous forme de package.

Exemples :

```text
lead-management
portfolio-basic
content-management
workflow-automation
```

Un module peut exposer plusieurs features.

Exemple :

```text
lead-management
├── leads.read
├── leads.update-status
├── leads.add-note
└── leads.export
```

---

## 5.3 Entitlement

Un entitlement représente un droit fonctionnel accordé au projet client.

Il répond à la question :

> Cette capacité fait-elle partie du périmètre commercial validé pour ce client ?

Exemples :

```text
Module lead-management autorisé
Export CSV autorisé
Portfolio avancé autorisé
Automatisations autorisées
```

Un entitlement ne correspond pas à une permission utilisateur.

Il s’applique au projet client dans son ensemble.

---

## 5.4 Permission

Une permission indique ce qu’un utilisateur authentifié peut faire.

Elle répond à la question :

> Cet utilisateur est-il autorisé à effectuer cette action ?

Exemples :

```text
Leads.Read
Leads.Update
Leads.AddNote
Leads.Export
Content.Publish
Portfolio.Manage
```

---

## 5.5 Feature flag

Un feature flag contrôle l’activation opérationnelle d’une fonctionnalité déjà connue de l’application.

Il peut servir à :

- activer progressivement une capacité ;
- désactiver temporairement une fonctionnalité ;
- protéger une fonctionnalité incomplète ;
- isoler un comportement en préproduction ;
- fournir un kill switch.

Un feature flag ne doit pas être utilisé pour contourner le catalogue ou les entitlements.

---

## 5.6 Kill switch

Un kill switch permet de désactiver rapidement une fonctionnalité présentant :

- un incident ;
- un risque de sécurité ;
- un comportement défectueux ;
- une dépendance externe indisponible ;
- un coût incontrôlé ;
- une automatisation dangereuse.

Un kill switch peut désactiver une capacité autorisée.

Il ne peut jamais activer une capacité non vendue ou non installée.

---

## 5.7 Module installé

Un module est installé lorsque :

- son package est référencé ;
- ses services sont enregistrés ;
- ses contributions sont composées ;
- ses données et migrations attendues sont présentes ;
- sa version apparaît dans le lock file.

---

## 5.8 Module configuré

Un module est configuré lorsque :

- ses options obligatoires sont présentes ;
- ses valeurs sont valides ;
- ses dépendances techniques sont disponibles ;
- ses secrets requis existent dans l’environnement approprié.

---

## 5.9 Module activé

Un module est activé lorsque la configuration résolue autorise son exécution dans l’environnement courant.

---

## 5.10 Fonctionnalité opérationnelle

Une fonctionnalité est opérationnelle lorsque :

- son état produit est valide ;
- ses dépendances essentielles sont disponibles ;
- elle n’est pas bloquée par un incident ;
- elle peut effectuer son traitement normalement.

---

# 6. États distincts d’un module

Les états suivants doivent rester distincts.

| État | Signification |
|---|---|
| Catalogué | Le module existe dans le catalogue |
| Disponible | Son statut permet une utilisation réelle |
| Compatible | Il est compatible avec le pack, le template et les versions |
| Sélectionné | Il est demandé dans la configuration |
| Entitled | Il a été vendu ou explicitement autorisé |
| Résolu | Ses dépendances ont été résolues |
| Installé | Son package et ses contributions sont présents |
| Configuré | Ses options obligatoires sont valides |
| Activé | Il est autorisé à s’exécuter |
| Opérationnel | Ses dépendances d’exécution sont disponibles |
| Autorisé | L’utilisateur possède les permissions nécessaires |

Ces états ne doivent pas être remplacés par un unique booléen ambigu.

---

# 7. Modèle d’état effectif

## 7.1 Expression conceptuelle

```text
IsFeatureAvailable =
    IsCatalogAvailable
    && IsCompatible
    && IsEntitled
    && IsInstalled
    && IsConfigured
    && IsEnabled
    && !IsKilled
    && IsOperational
```

Pour une action utilisateur :

```text
CanUserExecute =
    IsFeatureAvailable
    && IsAuthenticatedWhenRequired
    && HasRequiredPermission
```

---

## 7.2 Règle de refus par défaut

En cas d’état inconnu, incomplet ou contradictoire, le système doit refuser l’accès.

Principe :

```text
Default deny
```

Exemples :

- entitlement absent : capacité refusée ;
- module non présent dans le lock file : capacité refusée ;
- option essentielle invalide : module non démarré ou application en échec ;
- permission inconnue : action refusée ;
- feature flag inconnu : état sûr défini explicitement, généralement désactivé.

---

# 8. Sources de vérité

## 8.1 Catalogue SiteFactory

Le catalogue définit :

- les modules disponibles ;
- les packs ;
- les inclusions ;
- les compatibilités ;
- les dépendances ;
- les statuts ;
- les capacités proposées.

Source cible :

```text
catalog/sitefactory-catalog.yml
```

---

## 8.2 Configuration client

Le fichier :

```text
client.sitefactory.yml
```

décrit le périmètre fonctionnel validé du projet.

Il contient notamment :

- pack ;
- modules sélectionnés ;
- options fonctionnelles ;
- features configurables ;
- profil de déploiement.

Il ne contient aucun secret.

---

## 8.3 Lock file

Le fichier :

```text
sitefactory.lock.json
```

enregistre :

- modules résolus ;
- versions ;
- dépendances ;
- template ;
- thème ;
- version du catalogue ;
- capacités résolues lorsque nécessaire.

Il constitue la référence de ce qui a été généré et installé.

---

## 8.4 Configuration runtime

Les variables d’environnement et fichiers applicatifs définissent :

- état actif dans l’environnement ;
- kill switches ;
- paramètres opérationnels ;
- secrets ;
- dépendances externes.

La configuration runtime ne peut pas ajouter une capacité absente du lock file.

---

## 8.5 Identité et permissions

ASP.NET Core Identity et les politiques d’autorisation définissent les permissions des utilisateurs.

Cette source ne détermine pas les modules vendus.

---

# 9. Hiérarchie des décisions

L’ordre de résolution est :

```text
1. Catalogue
2. Configuration client validée
3. Résolution SiteFactory
4. Lock file
5. Composition de l’application
6. Configuration runtime
7. Kill switches
8. Permissions utilisateur
```

Une couche inférieure ne doit pas pouvoir annuler une interdiction provenant d’une couche supérieure.

Exemple :

```text
Module non vendu
+ variable Enabled=true
= module toujours indisponible
```

---

# 10. Entitlements du projet

## 10.1 Origine

Les entitlements proviennent du périmètre commercial validé, représenté techniquement dans la configuration client.

Exemple :

```yaml
offer:
  pack: sample-application

modules:
  lead-management:
    enabled: true

  portfolio-basic:
    enabled: true

  portfolio-advanced:
    enabled: false
```

---

## 10.2 Résolution

SiteFactory doit :

1. charger le pack ;
2. ajouter les modules inclus ;
3. ajouter les modules optionnels validés ;
4. résoudre les dépendances ;
5. vérifier les incompatibilités ;
6. produire la liste des entitlements ;
7. produire le plan d’installation ;
8. enregistrer le résultat dans le lock file.

---

## 10.3 Entitlements implicites par pack

Un pack peut accorder automatiquement certains entitlements.

Exemple :

```text
sample-application
→ lead-management
→ portfolio-basic
→ conversion-toolkit
```

Le projet client n’a pas besoin de répéter manuellement toutes les inclusions héritées.

Le résultat résolu doit toutefois être explicite dans le lock file.

---

## 10.4 Entitlements optionnels

Un module optionnel est accordé uniquement après validation.

Exemple :

```yaml
modules:
  sample-form:
    enabled: true
```

Le navigateur public ne peut pas autoriser directement cette capacité dans le projet de production.

---

## 10.5 Entitlements fins

Le MVP doit privilégier des entitlements au niveau module.

Des entitlements plus fins peuvent être ajoutés lorsqu’une vraie distinction commerciale existe.

Exemple possible :

```text
lead-management
├── lead-core
└── lead-csv-export
```

Cette granularité ne doit pas être introduite uniquement pour des détails techniques.

---

# 11. Feature flags

## 11.1 Rôle

Un feature flag gère l’activation opérationnelle d’une capacité déjà installée et autorisée.

Exemple :

```yaml
features:
  Leads:CsvExport:
    enabled: true
```

ou dans l’environnement :

```env
Features__Leads__CsvExport__Enabled=false
```

---

## 11.2 Usages autorisés

Les feature flags peuvent être utilisés pour :

- fonctionnalité en cours de stabilisation ;
- activation en préproduction ;
- déploiement progressif interne ;
- désactivation d’urgence ;
- migration par étapes ;
- dépendance externe temporairement indisponible ;
- comportement expérimental strictement interne.

---

## 11.3 Usages interdits

Un feature flag ne doit pas :

- remplacer une permission ;
- remplacer un entitlement ;
- rendre public un endpoint protégé ;
- activer un package non installé ;
- modifier un prix ;
- contourner le catalogue ;
- stocker une décision commerciale non documentée ;
- servir de condition basée sur le nom d’un client dans le code générique.

---

## 11.4 Flags permanents

Un flag temporaire ne doit pas devenir permanent sans revue.

Lorsqu’une fonctionnalité est stable et toujours active pour tous les projets concernés, le flag doit être supprimé ou transformé en configuration produit explicite.

---

# 12. Types de feature flags

## 12.1 Release flag

Utilisé pour une capacité en cours de déploiement.

Exemple :

```text
Features:Content:DraftPreview
```

Il doit être supprimé après stabilisation.

---

## 12.2 Operational flag

Utilisé pour contrôler un comportement opérationnel durable.

Exemple :

```text
Features:Email:SendAcknowledgement
```

Il peut rester configurable s’il représente un choix fonctionnel valide.

---

## 12.3 Kill switch

Utilisé pour désactiver rapidement une fonctionnalité.

Exemple :

```text
KillSwitches:EmailSequences
```

La valeur sûre est généralement :

```text
false pour l’activation
```

c’est-à-dire fonctionnalité désactivée en cas d’incertitude.

---

## 12.4 Environment flag

Permet d’activer une fonctionnalité uniquement dans certains environnements.

Exemple :

```text
Development
Staging
Production
```

Un comportement de développement ne doit pas être activé automatiquement en production.

---

## 12.5 Migration flag

Permet de gérer temporairement une transition.

Exemple conceptuel :

```text
ancienne lecture
→ double lecture
→ nouvelle lecture
→ suppression de l’ancien chemin
```

Le flag doit avoir :

- une date de suppression prévue ;
- un propriétaire ;
- des tests ;
- un plan de retrait.

---

# 13. Convention de nommage des features

## 13.1 Identifiants techniques

Format recommandé :

```text
{module}.{capability}
```

Exemples :

```text
leads.csv-export
leads.notes
content.publish
portfolio.advanced-gallery
automation.email-sequences
```

---

## 13.2 Configuration .NET

Format de section :

```text
Features:{Module}:{Capability}
```

Exemples :

```text
Features:Leads:CsvExport
Features:Content:Publish
Features:Portfolio:AdvancedGallery
```

---

## 13.3 Kill switches

Format :

```text
KillSwitches:{Module}:{Capability}
```

Exemples :

```text
KillSwitches:Leads:CsvExport
KillSwitches:Automation:EmailSequences
```

---

## 13.4 Noms à éviter

```text
NewFeature
SpecialMode
UseNewSystem
EnableStuff
ClientAlphaMode
TemporaryFix
```

Les noms doivent décrire une capacité stable ou une transition explicitement documentée.

---

# 14. Modèle de configuration conceptuel

```yaml
modules:
  lead-management:
    enabled: true

    features:
      notes:
        enabled: true

      csv-export:
        enabled: true

      permanent-delete:
        enabled: false

runtime:
  killSwitches:
    leads:
      csv-export: false
```

La configuration client ne doit pas contenir les valeurs opérationnelles d’urgence propres à la production lorsque celles-ci doivent rester dans l’environnement.

---

# 15. Modèle de résolution conceptuel

```csharp
public sealed record FeatureContext(
    string ModuleId,
    string FeatureId,
    bool IsCatalogAvailable,
    bool IsCompatible,
    bool IsEntitled,
    bool IsInstalled,
    bool IsConfigured,
    bool IsEnabled,
    bool IsKilled,
    bool IsOperational);

public sealed record FeatureEvaluationResult(
    bool IsAvailable,
    string? DenialReason,
    string? ErrorCode);
```

Ce modèle est indicatif.

Il ne constitue pas encore une API stabilisée.

---

# 16. Service d’évaluation central

La logique d’évaluation ne doit pas être dupliquée dans chaque composant.

Interface conceptuelle :

```csharp
public interface IFeatureAvailabilityService
{
    ValueTask<FeatureAvailabilityResult> EvaluateAsync(
        string featureId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsAvailableAsync(
        string featureId,
        CancellationToken cancellationToken = default);
}
```

Pour l’autorisation utilisateur :

```csharp
public interface IUserCapabilityService
{
    Task<UserCapabilityResult> CanExecuteAsync(
        ClaimsPrincipal user,
        string featureId,
        string permission,
        CancellationToken cancellationToken = default);
}
```

---

# 17. Séparation feature et permission

Exemple :

```text
Feature :
leads.csv-export

Permission :
Leads.Export
```

Évaluation complète :

```text
Le module Leads est-il autorisé pour le projet ?
L’export est-il installé ?
L’export est-il activé ?
L’export est-il opérationnel ?
L’utilisateur possède-t-il Leads.Export ?
```

Un utilisateur disposant de `Leads.Export` ne peut pas utiliser l’export si la feature n’est pas disponible pour le projet.

Inversement, une feature disponible ne donne pas automatiquement la permission à tous les utilisateurs.

---

# 18. Ordre des contrôles lors d’une requête

Ordre recommandé :

```text
1. Authentification si requise
2. Entitlement et availability de la feature
3. Permission utilisateur
4. Validation de la requête
5. Règles métier
6. Exécution
7. Audit ou événement technique sobre
```

Selon ASP.NET Core, feature et permission peuvent être regroupées dans des policies ou handlers dédiés.

---

# 19. Contrôle de la navigation

Un élément de navigation peut déclarer :

```text
Feature requise
Permission requise
Ordre
Route
```

Exemple conceptuel :

```csharp
public sealed record AdminNavigationItem(
    string Id,
    string Label,
    string Href,
    string RequiredFeature,
    string RequiredPermission,
    int Order);
```

Une entrée n’est affichée que si :

```text
feature disponible
ET permission accordée
```

Masquer l’entrée n’est qu’une amélioration UX.

La route reste protégée indépendamment.

---

# 20. Contrôle des pages

Une page liée à une feature doit vérifier son availability côté serveur.

Comportements possibles :

- retourner `404` si la capacité ne fait pas partie du produit ;
- retourner `403` si la capacité existe mais que l’utilisateur n’est pas autorisé ;
- rediriger vers la connexion si l’utilisateur n’est pas authentifié ;
- afficher un état indisponible si la fonctionnalité est temporairement désactivée.

La stratégie doit rester cohérente.

---

## 20.1 Module non vendu

Réponse recommandée :

```text
404 Not Found
```

Cela évite d’exposer inutilement les capacités non disponibles.

---

## 20.2 Utilisateur non autorisé

Réponse recommandée :

```text
403 Forbidden
```

---

## 20.3 Utilisateur non authentifié

Réponse recommandée selon le type d’interface :

```text
redirection vers login
ou
401 Unauthorized pour une API
```

---

## 20.4 Fonctionnalité temporairement indisponible

Réponse possible :

```text
503 Service Unavailable
```

si la capacité est essentielle et réellement indisponible.

Pour une désactivation fonctionnelle contrôlée, une page ou un message sobre peut être utilisé.

---

# 21. Contrôle des endpoints

Les endpoints doivent appliquer une protection explicite.

Exemple conceptuel :

```csharp
group.MapGet("/export.csv", ExportCsv)
    .RequireAuthorization(LeadPermissions.Export)
    .RequireFeature(LeadFeatures.CsvExport);
```

`RequireFeature` est une abstraction conceptuelle.

L’ordre d’application doit être testé.

---

## 21.1 Refus côté serveur

Même si le bouton d’export est masqué, l’endpoint doit refuser :

- l’appel manuel ;
- l’appel via navigateur ;
- l’appel via outil HTTP ;
- l’appel depuis un script ;
- l’appel depuis une ancienne version de l’UI.

---

# 22. Contrôle des services applicatifs

Les handlers et services doivent protéger les actions sensibles.

Exemple :

```csharp
public sealed class ExportLeadsHandler
{
    private readonly IFeatureAvailabilityService _features;
    private readonly IAuthorizationService _authorization;

    public async Task<ExportResult> HandleAsync(
        ExportLeadsCommand command,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var availability = await _features.EvaluateAsync(
            LeadFeatures.CsvExport,
            cancellationToken);

        if (!availability.IsAvailable)
        {
            return ExportResult.FeatureUnavailable();
        }

        var authorization = await _authorization.AuthorizeAsync(
            user,
            LeadPermissions.Export);

        if (!authorization.Succeeded)
        {
            return ExportResult.Forbidden();
        }

        // Traitement.
    }
}
```

La forme exacte peut être centralisée par pipeline behavior ou décorateur si cela reste simple et lisible.

---

# 23. Contrôle des traitements d’arrière-plan

Un job ou service d’arrière-plan doit vérifier :

- entitlement ;
- activation ;
- kill switch ;
- configuration ;
- dépendances ;
- idempotence.

Exemple :

```text
email-sequences installé
+ entitlement présent
+ feature activée
+ kill switch inactif
+ SMTP opérationnel
= traitement autorisé
```

Un traitement ne doit pas continuer silencieusement après désactivation d’urgence.

La stratégie de prise en compte du changement doit être documentée :

- au prochain cycle ;
- après redémarrage ;
- immédiatement via fournisseur dynamique.

Le MVP peut exiger un redémarrage pour les changements de configuration si cela simplifie l’exploitation.

---

# 24. Contrôle des événements

Un consommateur d’événement lié à une feature doit vérifier l’état de cette feature.

Exemple :

```text
LeadCreated
→ EmailSequenceStarter
```

Le consommateur doit ignorer proprement l’événement si :

- la feature n’est pas entitled ;
- elle est désactivée ;
- le kill switch est actif.

Il ne doit pas considérer cela comme une erreur technique si la désactivation est attendue.

---

# 25. Contrôle des données

## 25.1 Données conservées après désactivation

La désactivation d’une feature ne doit pas supprimer automatiquement ses données.

Exemple :

```text
portfolio-advanced désactivé
→ galeries conservées
→ affichage avancé désactivé
```

---

## 25.2 Données non exposées

Une feature désactivée ne doit pas permettre un accès indirect à ses données par :

- endpoint oublié ;
- export ;
- requête générique ;
- composant ancien ;
- API interne non protégée.

---

## 25.3 Nettoyage des données

Le nettoyage définitif suit une procédure distincte :

```text
plan
→ backup
→ validation
→ migration
→ vérification
→ rollback disponible
```

---

# 26. Entitlements et génération

## 26.1 Génération statique privilégiée

Dans le MVP, SiteFactory doit éviter d’installer tous les modules dans tous les projets.

La stratégie recommandée est :

```text
module non vendu
→ package non référencé
→ services non enregistrés
→ routes absentes
→ données absentes
```

Cette stratégie réduit :

- surface d’attaque ;
- dépendances ;
- complexité ;
- taille du projet ;
- risques de mauvaise activation.

---

## 26.2 Feature conditionnelle dans un module installé

Les flags sont pertinents lorsqu’un module installé possède plusieurs capacités configurables.

Exemple :

```text
lead-management installé

leads.read             toujours disponible
leads.notes            configurable
leads.csv-export       configurable
```

---

## 26.3 Composition générée

Le générateur peut produire une classe explicite :

```csharp
public static class GeneratedModuleComposition
{
    public static IServiceCollection AddGeneratedModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentityModule(configuration);
        services.AddContactModule(configuration);
        services.AddLeadsModule(configuration);

        return services;
    }
}
```

Aucun module non résolu ne doit apparaître dans cette composition.

---

# 27. Entitlements et lock file

Exemple conceptuel :

```json
{
  "modules": {
    "lead-management": {
      "version": "1.0.0",
      "entitled": true,
      "features": {
        "notes": true,
        "csv-export": true,
        "permanent-delete": false
      }
    }
  }
}
```

Le format définitif doit éviter les données dupliquées lorsqu’elles peuvent être déduites de manière fiable.

Le lock file doit néanmoins permettre de comprendre l’état effectivement résolu lors de la génération.

---

# 28. Runtime configuration

Exemple conceptuel :

```json
{
  "Features": {
    "Leads": {
      "CsvExport": {
        "Enabled": true
      }
    }
  },
  "KillSwitches": {
    "Leads": {
      "CsvExport": false
    }
  }
}
```

Variables d’environnement correspondantes :

```env
Features__Leads__CsvExport__Enabled=true
KillSwitches__Leads__CsvExport=false
```

La valeur runtime ne peut pas activer une feature absente des entitlements.

---

# 29. Validation au démarrage

Au démarrage, l’application doit détecter les incohérences importantes.

Exemples :

- feature activée mais module non installé ;
- feature activée mais entitlement absent ;
- feature exigeant une option manquante ;
- module activé sans configuration obligatoire ;
- permission déclarée sans module ;
- kill switch inconnu ;
- dépendance essentielle absente.

Selon la gravité :

- l’application refuse de démarrer ;
- le module est désactivé ;
- un warning sobre est produit ;
- un health check est dégradé.

Les règles doivent être documentées par feature.

---

# 30. Évaluation statique ou dynamique

## 30.1 MVP

Le MVP peut charger les flags au démarrage.

Une modification de configuration nécessite alors :

```text
modification de l’environnement
→ recréation ou redémarrage du conteneur
→ vérification
```

Cette stratégie est acceptable pour :

- simplicité ;
- reproductibilité ;
- audit ;
- faible fréquence de changement.

---

## 30.2 Évaluation dynamique future

Un fournisseur dynamique peut être étudié si LogicGate a besoin :

- d’un kill switch sans redémarrage ;
- d’un grand nombre de projets ;
- d’un pilotage centralisé ;
- d’une activation progressive.

Ce besoin n’est pas retenu dans le MVP.

---

# 31. Cache

Lorsque l’évaluation est locale et immuable pendant le cycle de vie de l’application, son résultat peut être mis en cache.

Le cache ne doit pas empêcher la prise en compte :

- d’un redémarrage ;
- d’une modification de configuration ;
- d’un changement d’identité utilisateur ;
- d’un changement de permission.

Les entitlements projet peuvent être singleton.

Les permissions utilisateur doivent être évaluées selon le contexte de requête.

---

# 32. Feature flags expérimentaux

Une feature expérimentale doit déclarer :

```text
Identifiant
Type du flag
Propriétaire
Date d’introduction
Environnements autorisés
Valeur par défaut
Critères de stabilisation
Date ou condition de retrait
Risques
Tests
```

Exemple :

```yaml
feature:
  id: content.draft-preview
  type: release
  owner: LogicGate
  introducedIn: 0.4.0
  default: false
  productionAllowed: false
  removalTarget: 0.6.0
```

---

# 33. Cycle de vie d’un feature flag

## 33.1 Création

Le flag est créé avec :

- justification ;
- nom stable ;
- valeur sûre ;
- tests ;
- documentation ;
- propriétaire ;
- condition de suppression.

---

## 33.2 Activation contrôlée

L’activation suit :

```text
développement
→ tests
→ préproduction
→ validation
→ production éventuelle
```

---

## 33.3 Stabilisation

Lorsque la capacité est stable :

- le flag est retiré ;
- ou transformé en option fonctionnelle durable ;
- les chemins morts sont supprimés ;
- les tests temporaires sont adaptés.

---

## 33.4 Retrait

Le retrait doit supprimer :

- l’ancien chemin ;
- la configuration temporaire ;
- les branches conditionnelles inutiles ;
- les tests obsolètes ;
- la documentation du flag.

---

# 34. Dette liée aux flags

Les flags permanents multiplient les combinaisons possibles.

Chaque flag supplémentaire augmente :

- les chemins de code ;
- les tests nécessaires ;
- les risques de configuration ;
- la complexité de support ;
- les risques de régression.

Une revue périodique doit identifier :

```text
flags temporaires expirés
flags toujours faux
flags toujours vrais
flags non référencés
flags sans propriétaire
flags sans test
```

---

# 35. Kill switches

## 35.1 Valeur sûre

Un kill switch doit avoir une valeur par défaut sûre.

Exemple :

```text
KillSwitches:Automation:EmailSequences=true
```

signifie que la capacité est coupée.

La convention doit être explicite pour éviter une inversion dangereuse.

Une alternative peut être :

```text
Features:Automation:EmailSequences:Enabled=false
```

Le projet doit choisir une convention unique.

---

## 35.2 Documentation opérateur

Chaque kill switch doit préciser :

- motif ;
- impact ;
- variable ;
- valeur d’activation ;
- valeur de désactivation ;
- besoin de redémarrage ;
- vérification ;
- rollback ;
- conséquences sur les traitements en cours.

---

## 35.3 Exemple

```text
Incident :
Les emails automatiques sont envoyés en double.

Action :
KillSwitches__Automation__EmailSequences=true

Effet :
Les nouvelles séquences ne démarrent plus.

Les données et historiques sont conservés.
```

---

# 36. Entitlements commerciaux et changements de contrat

## 36.1 Ajout d’une capacité

Le processus est :

```text
demande client
→ diagnostic
→ validation commerciale
→ devis ou avenant
→ mise à jour client.sitefactory.yml
→ plan d’installation
→ migration éventuelle
→ tests
→ mise à jour du lock file
→ déploiement
```

---

## 36.2 Retrait d’une capacité

Le retrait commercial ne doit pas déclencher automatiquement :

- suppression de package ;
- suppression de données ;
- suppression de tables ;
- suppression de médias.

Un plan explicite doit déterminer :

- désactivation ;
- conservation ;
- export ;
- archivage ;
- nettoyage futur ;
- responsabilités.

---

## 36.3 Fin de service mensuel

La fin d’un service mensuel ne doit pas désactiver les protections techniques de base.

Exemple :

```text
Fin de sample-support
≠ suppression de HTTPS
≠ suppression des backups sans transfert de responsabilité
≠ suppression de la validation serveur
```

Les responsabilités d’exploitation doivent être redéfinies.

---

# 37. Permissions et rôles

## 37.1 Rôles

Les rôles peuvent fournir des groupes de permissions.

Exemples :

```text
Administrator
ContentManager
SalesManager
Viewer
```

---

## 37.2 Permissions explicites

Les modules déclarent leurs permissions.

Les rôles associent les permissions selon la configuration du projet.

Exemple :

```text
SalesManager
├── Leads.Read
├── Leads.Update
├── Leads.AddNote
└── Leads.Export
```

---

## 37.3 Administrateur

Le rôle Administrator peut recevoir toutes les permissions disponibles dans le projet.

Il ne reçoit pas les permissions d’un module absent ou non entitled.

---

## 37.4 Changement de permission

Toute modification de permission doit être :

- tracée ;
- testée ;
- protégée ;
- appliquée côté serveur ;
- reflétée dans l’UI.

---

# 38. Policies ASP.NET Core

Les permissions peuvent être représentées par des policies.

Exemple :

```csharp
public static class LeadPermissions
{
    public const string Read = "Leads.Read";
    public const string Update = "Leads.Update";
    public const string Export = "Leads.Export";
}
```

Enregistrement conceptuel :

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy(
        LeadPermissions.Export,
        policy => policy.RequireClaim(
            SiteFactoryClaimTypes.Permission,
            LeadPermissions.Export));
});
```

Une policy peut également intégrer la disponibilité d’une feature si cela reste lisible et testable.

---

# 39. Feature authorization handler

Approche conceptuelle :

```csharp
public sealed class FeatureRequirement : IAuthorizationRequirement
{
    public FeatureRequirement(string featureId)
    {
        FeatureId = featureId;
    }

    public string FeatureId { get; }
}
```

```csharp
public sealed class FeatureAuthorizationHandler
    : AuthorizationHandler<FeatureRequirement>
{
    private readonly IFeatureAvailabilityService _features;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        FeatureRequirement requirement)
    {
        if (await _features.IsAvailableAsync(requirement.FeatureId))
        {
            context.Succeed(requirement);
        }
    }
}
```

L’implémentation finale doit éviter des accès réseau ou traitements coûteux par requête dans le MVP.

---

# 40. Interface publique

Les features publiques peuvent ne pas nécessiter de permission utilisateur.

Exemple :

```text
portfolio.public-list
```

Elles nécessitent néanmoins :

- entitlement ;
- installation ;
- activation ;
- disponibilité opérationnelle ;
- validation de contenu.

Une page publique ne doit pas afficher un module non autorisé pour le projet.

---

# 41. Administration

L’administration doit composer :

- navigation disponible ;
- features ;
- permissions ;
- états opérationnels.

Exemple :

```text
Lead management entitled et installé
+ utilisateur avec Leads.Read
= menu Leads visible
```

Le menu ne doit pas indiquer une capacité non vendue uniquement pour encourager un achat, sauf si un futur mode d’upsell est explicitement conçu.

Le MVP ne prévoit pas d’interface d’upsell dans l’administration client.

---

# 42. Messages utilisateur

## 42.1 Feature non incluse

Message public ou admin possible :

```text
Cette fonctionnalité n’est pas disponible dans la configuration actuelle.
```

Éviter d’exposer :

- détails du catalogue ;
- prix internes ;
- noms de packages ;
- règles techniques.

---

## 42.2 Permission insuffisante

```text
Vous n’êtes pas autorisé à effectuer cette action.
```

---

## 42.3 Fonction temporairement désactivée

```text
Cette fonctionnalité est temporairement indisponible.
Réessayez ultérieurement ou contactez l’administrateur.
```

---

## 42.4 Configuration invalide

En production, ne pas afficher le détail technique à l’utilisateur.

Le détail est destiné à l’opérateur, sans secret.

---

# 43. Logs et audit

## 43.1 Événements techniques

Événements possibles :

```text
FeatureEvaluationDenied
FeatureDisabledByKillSwitch
FeatureConfigurationInvalid
PermissionDenied
EntitlementMismatchDetected
```

---

## 43.2 Données autorisées

```text
FeatureId
ModuleId
ReasonCode
Environment
CorrelationId
UserTechnicalId lorsque nécessaire et autorisé
```

---

## 43.3 Données interdites

Les logs ne doivent pas contenir :

- nom ;
- email ;
- téléphone ;
- contenu de lead ;
- mot de passe ;
- token ;
- secret ;
- configuration complète ;
- claims sensibles complets.

---

## 43.4 Audit métier

Certaines actions sensibles peuvent nécessiter un historique métier.

Exemples :

- modification des permissions ;
- activation d’une automatisation ;
- déclenchement d’un export ;
- changement d’un entitlement interne ;
- désactivation d’un module.

L’audit métier doit rester distinct des logs techniques.

---

# 44. Health checks

Un health check peut signaler :

- incohérence d’entitlements ;
- configuration manquante ;
- module installé mais non opérationnel ;
- dépendance externe indisponible ;
- kill switch actif.

Le endpoint public doit rester sobre.

Exemple public :

```json
{
  "status": "Degraded"
}
```

Les détails sont réservés aux opérateurs autorisés.

---

# 45. Cohérence au démarrage

Une validation de cohérence devrait comparer :

```text
catalogue résolu
lock file
packages référencés
composition générée
options runtime
permissions déclarées
```

Exemples d’anomalies :

```text
Module dans le lock file mais package absent

Package présent mais module absent du lock file

Feature activée mais entitlement absent

Permission enregistrée pour un module absent

Route active pour une feature désactivée
```

Certaines anomalies peuvent bloquer le démarrage.

---

# 46. Erreurs et codes recommandés

| Code | Signification |
|---|---|
| `SF-FEATURE-001` | Feature inconnue |
| `SF-FEATURE-002` | Module non installé |
| `SF-FEATURE-003` | Entitlement absent |
| `SF-FEATURE-004` | Feature désactivée |
| `SF-FEATURE-005` | Kill switch actif |
| `SF-FEATURE-006` | Configuration invalide |
| `SF-FEATURE-007` | Dépendance indisponible |
| `SF-FEATURE-008` | État incohérent |
| `SF-AUTH-001` | Utilisateur non authentifié |
| `SF-AUTH-002` | Permission insuffisante |
| `SF-AUTH-003` | Permission inconnue |

Ces codes ne doivent pas révéler de données sensibles.

---

# 47. Tests obligatoires

## 47.1 Tests de résolution

Vérifier :

- module inclus par le pack ;
- module optionnel validé ;
- module non vendu ;
- module incompatible ;
- dépendance ajoutée ;
- entitlement fin ;
- valeur inconnue ;
- état par défaut.

---

## 47.2 Tests de feature flags

Vérifier :

- feature activée ;
- feature désactivée ;
- kill switch actif ;
- kill switch inactif ;
- flag absent ;
- flag inconnu ;
- environnement non autorisé ;
- changement nécessitant un redémarrage.

---

## 47.3 Tests de permissions

Vérifier :

- utilisateur anonyme ;
- utilisateur authentifié sans permission ;
- utilisateur autorisé ;
- administrateur ;
- permission inconnue ;
- permission présente pour module absent.

---

## 47.4 Tests combinés

Matrice minimale :

| Entitled | Installé | Enabled | Killed | Permission | Résultat |
|---:|---:|---:|---:|---:|---|
| Non | Non | Non | Non | Oui | Refusé |
| Oui | Non | Oui | Non | Oui | Refusé |
| Oui | Oui | Non | Non | Oui | Refusé |
| Oui | Oui | Oui | Oui | Oui | Refusé |
| Oui | Oui | Oui | Non | Non | Refusé |
| Oui | Oui | Oui | Non | Oui | Autorisé |

---

## 47.5 Tests HTTP

Vérifier :

- route absente ou refusée si module non vendu ;
- `401` pour API anonyme protégée ;
- `403` sans permission ;
- succès avec permission ;
- export non retourné lorsque feature désactivée ;
- aucune donnée sensible dans la réponse refusée.

---

## 47.6 Tests UI

Vérifier :

- menu masqué si feature indisponible ;
- bouton masqué ou désactivé selon le besoin ;
- message explicite ;
- ancienne URL protégée ;
- état temporairement indisponible ;
- navigation clavier.

---

## 47.7 Tests des jobs

Vérifier :

- job non démarré sans entitlement ;
- job non démarré si disabled ;
- arrêt ou non-exécution avec kill switch ;
- aucune double exécution après réactivation ;
- historique cohérent.

---

## 47.8 Tests de configuration

Vérifier :

- incohérence détectée ;
- option essentielle manquante ;
- feature activée sans module ;
- entitlement absent ;
- valeur par défaut sûre ;
- aucun secret affiché dans les erreurs.

---

# 48. Tests de sécurité

Les tests doivent tenter de contourner l’interface via :

- URL directe ;
- requête HTTP manuelle ;
- formulaire modifié ;
- ancien lien ;
- paramètre manipulé ;
- endpoint d’export ;
- appel de service interne lorsque pertinent.

Le résultat doit rester refusé côté serveur.

---

# 49. Tests d’architecture

Les tests doivent vérifier notamment :

```text
La logique de feature n’est pas dupliquée dans les composants UI.

Les modules utilisent le service central d’évaluation.

Les endpoints sensibles possèdent une protection.

Les permissions sont déclarées dans le module.

Les feature IDs sont centralisés.

Aucune condition basée sur un client nommé n’existe
dans les packages génériques.
```

---

# 50. Feature registry

Un registre central peut contenir les identifiants connus.

Exemple conceptuel :

```csharp
public static class LeadFeatures
{
    public const string Notes = "leads.notes";
    public const string CsvExport = "leads.csv-export";
}
```

Les identifiants ne doivent pas être recopiés sous forme de chaînes dans de nombreux fichiers.

Le registre reste organisé par module pour éviter un fichier global fourre-tout.

---

# 51. Manifeste du module

Le manifeste peut déclarer les features.

Exemple :

```yaml
features:
  - id: leads.notes
    defaultEnabled: true
    requiresPermission: Leads.AddNote

  - id: leads.csv-export
    defaultEnabled: true
    requiresPermission: Leads.Export

  - id: leads.permanent-delete
    defaultEnabled: false
    status: future
```

Un `defaultEnabled` ne peut pas contourner un entitlement absent.

---

# 52. Compatibilité avec les packs

Le catalogue définit les entitlements initiaux.

Exemple conceptuel :

```yaml
packs:
  sample-application:
    modules:
      lead-management:
        features:
          leads.notes: true
          leads.csv-export: true
```

Les règles commerciales restent dans le catalogue.

Le manifeste décrit ce que le module sait fournir.

---

# 53. Feature flags et migrations

Une feature ne doit pas permettre d’utiliser un schéma de données qui n’existe pas encore.

Ordre recommandé :

```text
1. Déployer une migration compatible
2. Déployer le code compatible avec ancien et nouveau schéma si nécessaire
3. Activer la feature
4. Vérifier
5. Supprimer l’ancien chemin lors d’une version ultérieure
```

Le flag ne remplace pas l’inspection de la migration.

---

# 54. Feature flags et rollback

Un rollback fonctionnel peut consister à désactiver un flag.

Cela ne constitue un rollback complet que si :

- l’ancien chemin reste compatible ;
- les données restent lisibles ;
- aucune migration destructive n’a supprimé l’ancien schéma ;
- le comportement a été testé.

Un kill switch ne remplace pas un backup ou un rollback applicatif.

---

# 55. Feature flags et performance

L’évaluation d’une feature doit rester peu coûteuse.

Dans le MVP :

- les entitlements sont chargés au démarrage ;
- les flags sont des options typées ;
- les permissions utilisent le système d’autorisation ASP.NET Core ;
- aucune requête externe n’est nécessaire par feature.

Une feature ne doit pas effectuer une requête PostgreSQL à chaque rendu de composant uniquement pour savoir si elle est activée.

---

# 56. Feature flags et cache de rendu

Le rendu public mis en cache doit tenir compte des features.

Dans le modèle initial, chaque application correspond à un seul client, ce qui simplifie la variation.

Il faut néanmoins éviter qu’une page construite avec une feature active reste servie après désactivation si un cache persistant est introduit.

Toute stratégie de cache future devra définir son invalidation.

---

# 57. Feature flags et frontend

Le frontend peut recevoir une représentation limitée des features publiques.

Il ne doit pas recevoir :

- secrets ;
- règles commerciales complètes ;
- permissions d’autres utilisateurs ;
- configuration interne ;
- détails des kill switches.

La décision serveur reste prioritaire.

Un booléen frontend sert uniquement à adapter l’interface.

---

# 58. Feature flags et JavaScript

Le code JavaScript ne doit pas constituer la seule protection.

Exemple interdit :

```javascript
if (!features.csvExport) {
    document.querySelector("#export").remove();
}
```

si l’endpoint reste accessible.

Le serveur doit refuser indépendamment.

---

# 59. Gestion des incohérences

## 59.1 Module installé sans entitlement

Comportement recommandé :

- module non enregistré lorsque la composition est générée correctement ;
- ou module désactivé avec warning opérateur ;
- routes non exposées ;
- aucune action disponible.

---

## 59.2 Entitlement sans package

Comportement recommandé :

```text
échec de validation
```

La génération ou le démarrage doit signaler l’incohérence.

---

## 59.3 Feature activée sans configuration

Comportement recommandé :

- échec au démarrage si essentiel ;
- feature désactivée si optionnelle et comportement sûr ;
- health check dégradé ;
- message opérateur.

---

## 59.4 Permission inconnue

Comportement recommandé :

```text
refus
```

La permission ne doit pas être créée silencieusement à partir d’une chaîne inconnue.

---

# 60. Administration des flags

Le MVP ne nécessite pas une interface graphique d’administration des flags.

Les changements s’effectuent par :

- configuration versionnée pour les options durables ;
- variables d’environnement pour les états opérationnels ;
- procédure de déploiement ;
- runbook.

Cette stratégie favorise :

- traçabilité ;
- reproductibilité ;
- simplicité ;
- sécurité.

Une interface centrale pourra être étudiée plus tard.

---

# 61. Runbook opérateur

Pour chaque flag opérationnel, la documentation doit fournir :

```text
Nom
Description
Valeur par défaut
Environnements concernés
Variable d’environnement
Impact
Besoin de redémarrage
Commande de vérification
Procédure d’activation
Procédure de désactivation
Rollback
Propriétaire
```

Exemple :

```text
Feature :
leads.csv-export

Variable :
Features__Leads__CsvExport__Enabled

Valeur sûre :
false

Redémarrage :
oui

Vérification :
connexion admin
→ page Leads
→ bouton Export absent
→ endpoint export refusé
```

---

# 62. Sécurité

Le système doit empêcher :

- activation d’une feature non vendue ;
- accès par URL directe ;
- accès à un endpoint masqué ;
- accès par permission visuelle uniquement ;
- modification d’entitlement depuis le navigateur ;
- contournement par paramètre ;
- exposition de la configuration interne ;
- activation par variable d’un module absent ;
- accès à des données d’une feature désactivée.

---

# 63. Données sensibles

Les entitlements et flags ne doivent pas contenir :

- données personnelles ;
- secrets ;
- tokens ;
- mots de passe ;
- contenu métier confidentiel ;
- valeurs de connexion.

Ils peuvent contenir :

- identifiants techniques ;
- booléens ;
- versions ;
- états ;
- raisons techniques sobres.

---

# 64. Règles pour Codex

## 64.1 Lecture préalable

Avant toute modification liée aux features ou permissions, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../adr/0006-feature-flags-and-entitlements-separation.md
core-module-boundary.md
09-module-architecture.md
10-feature-flags-and-entitlements.md
AGENTS.md
```

Ainsi que :

- le manifeste du module ;
- le catalogue ;
- le lock file d’exemple ;
- les permissions ;
- les tests d’autorisation ;
- les endpoints concernés.

---

## 64.2 Ce que Codex peut faire

Codex peut :

- ajouter une feature validée ;
- centraliser un identifiant ;
- implémenter un service d’évaluation ;
- ajouter une policy ;
- protéger un endpoint ;
- protéger un handler ;
- ajouter un kill switch demandé ;
- ajouter des tests ;
- documenter le runbook ;
- corriger une incohérence.

---

## 64.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- modifier les entitlements commerciaux sans demande ;
- activer une fonctionnalité payante ;
- modifier un pack ;
- utiliser un flag comme remplacement d’une permission ;
- protéger uniquement l’UI ;
- créer une condition par client nommé ;
- ajouter un fournisseur distant de flags ;
- masquer une route sans protéger le serveur ;
- rendre un endpoint public pour corriger un problème ;
- supprimer les données à la désactivation ;
- introduire un flag sans plan de retrait ;
- utiliser une valeur par défaut permissive sans justification ;
- afficher les flags internes au public ;
- modifier les prix ;
- créer une interface admin de flags sans validation.

---

## 64.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouvel entitlement commercial ;
- granularité commerciale plus fine ;
- nouvelle permission sensible ;
- nouveau kill switch de production ;
- changement de valeur par défaut ;
- feature expérimentale en production ;
- modification du modèle de lock file ;
- introduction d’un fournisseur dynamique ;
- retrait d’une feature ;
- changement affectant les contrats clients.

---

## 64.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Feature concernée
Module concerné
Entitlement affecté
Flag affecté
Valeur par défaut
Kill switch éventuel
Permission requise
Routes protégées
Endpoints protégés
Services protégés
Jobs protégés
Configuration modifiée
Tests ajoutés
Build
Tests
Impact production
Besoin de redémarrage
Plan de retrait du flag
Décisions restant humaines
```

---

# 65. Anti-patterns interdits

## 65.1 Masquage UI uniquement

```razor
@if (ShowExport)
{
    <button>Exporter</button>
}
```

sans protection de l’endpoint ou du service.

---

## 65.2 Booléen unique ambigu

```csharp
IsLeadsEnabled
```

utilisé à la fois pour :

- vente ;
- installation ;
- activation ;
- permission ;
- disponibilité.

---

## 65.3 Flag par client dans le socle

```csharp
if (clientSlug == "cabinet-alpha")
{
    EnableAdvancedPortfolio();
}
```

---

## 65.4 Permission utilisée comme entitlement

```text
L’utilisateur possède Leads.Export
→ donc le module est vendu
```

Ce raisonnement est invalide.

---

## 65.5 Entitlement utilisé comme permission

```text
Le module Leads est vendu
→ tous les utilisateurs peuvent exporter
```

Ce raisonnement est invalide.

---

## 65.6 Flag sans propriétaire

Un flag temporaire ne possède ni responsable ni date de retrait.

---

## 65.7 Flag toujours actif

Un flag stable reste dans le code pendant plusieurs versions sans raison.

---

## 65.8 Activation runtime d’un package absent

Une variable d’environnement tente d’activer une capacité non compilée dans l’application.

---

## 65.9 Suppression des données à la désactivation

Désactiver un module entraîne automatiquement la suppression de ses tables.

---

## 65.10 Feature ID dispersé

```text
"leads-export"
"lead.csv"
"Leads:Export"
```

utilisés pour la même capacité dans différents fichiers.

---

# 66. Critères d’acceptation du MVP

Le système sera considéré comme opérationnel lorsque :

1. les concepts d’entitlement, installation, activation et permission sont séparés ;
2. le catalogue définit les capacités disponibles ;
3. la configuration client définit les capacités validées ;
4. le lock file enregistre les modules résolus ;
5. la composition n’enregistre que les modules installés ;
6. les flags runtime ne peuvent pas activer un module absent ;
7. les entitlements ne peuvent pas être modifiés par le navigateur ;
8. les features sont évaluées par un service central ;
9. les routes sensibles sont protégées ;
10. les endpoints sensibles sont protégés ;
11. les services sensibles sont protégés ;
12. les jobs respectent les flags ;
13. les permissions sont appliquées côté serveur ;
14. les menus respectent feature et permission ;
15. les données restent conservées après désactivation ;
16. les kill switches sont documentés ;
17. les valeurs par défaut sont sûres ;
18. les incohérences sont détectées ;
19. les tests combinés sont présents ;
20. aucune donnée sensible n’est logguée ;
21. le projet généré compile ;
22. les tests réussissent sans warning injustifié.

---

# 67. Décisions validées

La baseline est la suivante :

```text
Un module catalogué n’est pas automatiquement vendu.

Un module vendu n’est pas automatiquement installé.

Un module installé n’est pas automatiquement activé.

Un module activé n’accorde pas automatiquement les permissions.

Les entitlements s’appliquent au projet client.

Les permissions s’appliquent aux utilisateurs.

Les feature flags contrôlent l’état opérationnel.

Les kill switches peuvent désactiver, jamais autoriser.

Le navigateur n’est jamais une source de vérité.

Les contrôles serveur sont obligatoires.

Masquer un menu ou un bouton ne constitue pas une protection.

Le MVP installe uniquement les modules résolus.

Les flags runtime ne peuvent pas activer un package absent.

Les données ne sont pas supprimées lors d’une désactivation.

Les flags temporaires possèdent un plan de retrait.

Les entitlements sont issus de la configuration validée.

Les versions résolues sont conservées dans le lock file.

Le MVP n’utilise pas de fournisseur distant de feature flags.

Les changements runtime peuvent nécessiter un redémarrage.

Codex ne modifie pas seul un entitlement commercial
ou une permission sensible.
```

---

# 68. Checklist de revue d’une feature

## Produit

```text
[ ] Feature identifiée
[ ] Module propriétaire identifié
[ ] Entitlement défini
[ ] Pack ou option défini
[ ] Statut produit connu
[ ] Pas de modification commerciale implicite
```

## Installation

```text
[ ] Package présent
[ ] Version présente dans le lock file
[ ] Dépendances résolues
[ ] Services enregistrés
[ ] Configuration valide
```

## Activation

```text
[ ] Valeur par défaut sûre
[ ] Flag centralisé
[ ] Kill switch éventuel
[ ] Environnements autorisés
[ ] Besoin de redémarrage documenté
[ ] Plan de retrait défini si temporaire
```

## Autorisation

```text
[ ] Permission définie
[ ] Policy définie
[ ] Route protégée
[ ] Endpoint protégé
[ ] Service protégé
[ ] UI cohérente
```

## Données

```text
[ ] Données conservées à la désactivation
[ ] Accès indirect protégé
[ ] Aucun nettoyage automatique
[ ] Backup pris en compte
[ ] Migration compatible
```

## Qualité

```text
[ ] Tests de résolution
[ ] Tests de flags
[ ] Tests de permissions
[ ] Tests HTTP
[ ] Tests UI
[ ] Tests de contournement
[ ] Build Release réussi
[ ] Aucun warning injustifié
[ ] Documentation à jour
```

---

# 69. Conclusion

La gestion des feature flags et des entitlements doit garantir que le périmètre vendu, le projet généré, l’application déployée et les droits des utilisateurs restent cohérents.

Elle repose sur une séparation claire entre :

- catalogue ;
- entitlement commercial ;
- installation ;
- configuration ;
- activation ;
- état opérationnel ;
- permission utilisateur.

> Une fonctionnalité n’est réellement disponible que lorsqu’elle est prévue par le produit, autorisée pour le client, présente dans l’application, correctement configurée, activée, opérationnelle et autorisée pour l’utilisateur courant.
