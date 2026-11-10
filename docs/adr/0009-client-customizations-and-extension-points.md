---
adrId: ADR-0009
title: Isolate client customizations behind explicit extension points
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
  - ../03-generation/13-generation-pipeline.md
  - ../02-architecture/core-module-boundary.md
  - ../03-generation/repository-documentation-end-to-end.md
  - ../02-architecture/09-module-architecture.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ../03-generation/generated-runtime-database-model.md
  - ../04-quality-security/18-security-baseline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/21-observability-and-logging.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../05-operations/27-client-handover-and-service-closure.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0003-postgresql-as-primary-database.md
  - ./0004-explicit-production-migrations.md
  - ./0005-file-generation-strategies.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0008-private-package-feed-and-supply-chain.md
targetVersion: 0.1.0
---

# ADR-0009 — Personnalisations client et points d’extension explicites

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le moteur public applique les stratégies de fichiers et préserve les zones
custom selon son [contrat de régénération](../03-generation/regeneration-and-end-to-end-proof.md).
Les hooks, slots UI, registries métier et projets `Client.Customizations`
décrits ci-dessous sont des points d'extension cibles d'applications clientes,
pas des fonctionnalités générées par DocumentationModule.
Les identifiants `sample-project.*` et `ExampleProject.Customizations`
sont fictifs. Une customization peut consommer un contrat public ; elle ne
rend jamais le Core dépendant d'un type client ou d'un module privé.
Aucun overwrite silencieux ni accès internal inter-module n'est autorisé.

## 1. Statut

```text
accepted
```

Les personnalisations spécifiques à un client doivent être isolées du socle générique SiteFactory.

Elles utilisent, par ordre de préférence :

```text
configuration
→ contenu
→ thème et design tokens
→ options de modules
→ points d’extension publics
→ module client dédié
```

Le code générique SiteFactory ne doit pas contenir de branche conditionnelle spécifique à un client.

Exemple interdit :

```csharp
if (clientSlug == "sample-project")
{
    // comportement spécifique
}
```

Le code client ne doit pas modifier directement :

- les fichiers `generated` ;
- les implémentations internes des modules ;
- les tables privées d’un autre module ;
- les services internes non contractuels ;
- le pipeline SiteFactory ;
- les packages génériques publiés.

---

## 2. Résumé

Les projets clients peuvent nécessiter des adaptations qui dépassent :

- le contenu ;
- le thème ;
- les options ;
- les modules standards.

Ces adaptations sont conservées dans une zone dédiée du repository client, par exemple :

```text
src/
├── Client.Application/
├── Client.Infrastructure/
├── Client.Web/
└── Client.Customizations/
```

avec leurs tests :

```text
tests/
└── Client.Customizations.Tests/
```

Les customizations consomment uniquement :

- les contrats publics de la plateforme ;
- les contrats publics des modules ;
- les points d’extension déclarés ;
- les abstractions transversales approuvées.

La composition reste statique :

```text
configuration client
→ modules
→ customizations déclarées
→ composition root généré
→ compilation
→ application autonome
```

Aucun code spécifique client n’est :

- découvert dynamiquement ;
- téléchargé au runtime ;
- chargé depuis un dossier de plugins ;
- exécuté sans compilation et revue ;
- généralisé automatiquement dans SiteFactory.

---

## 3. Contexte

SiteFactory vise à industrialiser la production de sites et applications PME tout en permettant un niveau raisonnable de personnalisation.

La majorité des besoins doit être couverte par :

- les packs ;
- les templates ;
- les thèmes ;
- les sections ;
- les modules ;
- les options ;
- la configuration ;
- les contenus administrables.

Certains projets nécessiteront néanmoins des comportements spécifiques.

Exemples :

- calcul de devis propre à un métier ;
- routage personnalisé d’un lead ;
- validation métier spécifique ;
- intégration avec un logiciel client ;
- composant d’administration particulier ;
- section visuelle non présente dans le catalogue ;
- export propre à un processus ;
- règle de visibilité ou de classement spécifique.

Ces adaptations ne doivent pas provoquer la contamination du socle générique par :

- des noms de clients ;
- des exceptions métier locales ;
- des branches conditionnelles ;
- des dépendances vers un repository client ;
- des migrations spécifiques incluses dans tous les projets ;
- des modules génériques artificiels utilisés par un seul client.

Inversement, le projet client ne doit pas devenir un fork incontrôlé du socle SiteFactory.

---

## 4. Problème à résoudre

Comment permettre des adaptations spécifiques à un client sans :

- modifier le socle générique ;
- casser les upgrades ;
- exposer les implémentations internes ;
- créer un fork complet ;
- ajouter des conditions par client ;
- contourner les règles de module ;
- mélanger code générique et code contractuel ;
- rendre le projet impossible à transférer ;
- charger du code dynamique non maîtrisé ?

La solution doit permettre de répondre clairement à :

```text
Cette adaptation peut-elle être faite par configuration ?

Est-elle réellement propre au client ?

Quel contrat public utilise-t-elle ?

Quel composant possède les données ?

Comment est-elle testée ?

Quel impact aura un upgrade ?

Peut-elle être retirée ?

Doit-elle devenir un module générique ?
```

---

## 5. Portée

Cette décision couvre :

- code spécifique client ;
- configuration ;
- thèmes ;
- contenus ;
- points d’extension ;
- interfaces publiques ;
- slots UI ;
- stratégies métier ;
- validations ;
- intégrations externes ;
- événements ;
- handlers ;
- navigation ;
- services ;
- module client dédié ;
- données spécifiques ;
- migrations ;
- tests ;
- observabilité ;
- versionnement ;
- upgrades ;
- transfert client ;
- règles applicables à Codex.

---

## 6. Hors portée

Cette décision ne définit pas :

- chaque personnalisation future ;
- une marketplace de plugins ;
- le chargement dynamique d’assemblies ;
- une sandbox d’exécution ;
- un langage de script client ;
- une interface no-code publique ;
- un système SaaS multi-tenant ;
- le détail des contrats commerciaux ;
- le prix de chaque adaptation ;
- un mécanisme de hot reload en production ;
- l’exécution de code fourni directement par l’utilisateur final.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Préservation du socle générique**
2. **Protection des upgrades**
3. **Isolation du code client**
4. **Contrats publics explicites**
5. **Compilation et tests**
6. **Absence de branches spécifiques dans les packages génériques**
7. **Propriété claire des données**
8. **Maintenabilité**
9. **Transférabilité du projet**
10. **Déterminisme de la composition**
11. **Sécurité**
12. **Possibilité de généralisation future**
13. **Réversibilité**
14. **Limitation des points d’extension**
15. **Compatibilité avec une petite équipe**

---

## 8. Contraintes

La décision doit respecter :

```text
Un repository par client

Monolithe modulaire

Composition statique

Packages génériques versionnés

Contrats publics versionnés

Fichiers custom préservés

Aucune modification des fichiers generated

Migrations dans le repository client

Tests obligatoires

Aucun chargement dynamique

Aucune donnée client dans le socle générique

Aucune dépendance du socle vers le projet client

Compatibilité Windows et Linux
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- la majorité des projets restera largement configurable ;
- le nombre de personnalisations par projet restera limité ;
- les personnalisations seront développées et revues comme du code de production ;
- elles pourront être compilées avec le reste de l’application ;
- les contrats publics SiteFactory évolueront selon SemVer ;
- une rupture de contrat sera détectée pendant l’upgrade ;
- le code spécifique client restera dans son repository ;
- les besoins réutilisables pourront être généralisés après retour d’expérience ;
- une personnalisation importante peut devenir un module client dédié.

---

# 10. Principes de personnalisation

## 10.1 Configuration avant code

Avant de créer du code spécifique, vérifier si le besoin peut être couvert par :

- `client.sitefactory.yml` ;
- une option existante ;
- une capacité existante ;
- un entitlement ;
- un feature flag ;
- un paramètre de module ;
- un contenu administrable.

---

## 10.2 Contenu avant composant

Une différence éditoriale ne doit pas produire du code spécifique.

Exemples :

```text
titre
texte
CTA
coordonnées
FAQ
réalisation
image
SEO
```

relèvent généralement du contenu.

---

## 10.3 Thème avant CSS spécifique

Une différence visuelle doit d’abord utiliser :

- design tokens ;
- variantes ;
- thème ;
- composants existants ;
- options du design system.

---

## 10.4 Module standard avant customization

Une capacité réutilisable déjà disponible ne doit pas être recodée dans le repository client.

---

## 10.5 Extension avant fork

Une adaptation ciblée utilise un point d’extension public.

Elle ne modifie pas directement le module concerné.

---

## 10.6 Module client avant logique dispersée

Une personnalisation importante possédant :

- plusieurs cas d’usage ;
- ses propres données ;
- ses propres permissions ;
- ses propres migrations ;
- plusieurs écrans ;

doit devenir un module client dédié plutôt qu’un ensemble de hooks dispersés.

---

# 11. Option A — Fork complet du projet généré

## 11.1 Description

Après la génération initiale, le projet est librement modifié.

SiteFactory n’essaie plus de séparer le code générique et le code client.

---

## 11.2 Avantages

- liberté totale ;
- développement direct ;
- aucune conception de point d’extension ;
- simple pour un projet ponctuel ;
- accès à toutes les implémentations.

---

## 11.3 Inconvénients

- upgrades très difficiles ;
- divergence ;
- comparaison complexe ;
- correctifs de sécurité difficiles à propager ;
- perte de la composition déclarative ;
- dépendance aux connaissances du développeur initial ;
- duplication du socle.

---

## 11.4 Risques

- projet bloqué sur une ancienne version ;
- customizations impossibles à distinguer ;
- correctif générique écrasant du code client ;
- transfert sans documentation ;
- dette importante.

---

## 11.5 Réversibilité

```text
faible
```

Le retour vers une structure maîtrisée nécessite d’identifier manuellement toutes les divergences.

---

# 12. Option B — Conditions spécifiques dans le socle

## 12.1 Description

Les packages et modules génériques connaissent les clients.

Exemple :

```csharp
if (clientId == KnownClients.Alpha)
{
    return AlphaSpecificBehavior();
}
```

---

## 12.2 Avantages

- code centralisé ;
- déploiement simple ;
- accès direct aux implémentations ;
- faible nombre de projets supplémentaires.

---

## 12.3 Inconvénients

- socle contaminé ;
- dépendances commerciales ;
- risques de régression croisée ;
- impossibilité de publier proprement les packages ;
- tests multipliés par client ;
- retrait de client complexe ;
- risque de fuite d’informations.

---

## 12.4 Risques

- comportement client A exécuté pour B ;
- identifiants clients dans les packages ;
- branches mortes ;
- données ou règles confidentielles diffusées ;
- maintenance exponentielle.

---

## 12.5 Réversibilité

```text
faible
```

Les branches spécifiques se dispersent rapidement.

---

# 13. Option C — Plugins dynamiques

## 13.1 Description

Les personnalisations sont livrées comme assemblies chargées depuis un dossier ou un feed au runtime.

---

## 13.2 Avantages

- ajout sans recompilation du socle ;
- distribution indépendante ;
- activation runtime ;
- extensibilité forte.

---

## 13.3 Inconvénients

- sécurité de chargement ;
- compatibilité binaire ;
- découverte ;
- isolation difficile ;
- diagnostics ;
- migrations ;
- gestion des versions ;
- risque de code non approuvé.

---

## 13.4 Risques

- exécution arbitraire ;
- assembly falsifiée ;
- dépendance manquante ;
- conflit de versions ;
- code différent entre environnements ;
- démarrage instable.

---

## 13.5 Réversibilité

```text
faible à moyenne
```

Les contrats de plugins deviendraient une architecture structurante.

---

# 14. Option D — Customizations isolées et points d’extension explicites

## 14.1 Description

Les customizations restent dans le repository client.

Elles sont compilées avec l’application et consomment des contrats publics stables.

---

## 14.2 Avantages

- socle générique préservé ;
- compilation ;
- tests ;
- propriété client claire ;
- upgrades vérifiables ;
- composition déterministe ;
- pas de chargement dynamique ;
- transfert simplifié ;
- surface publique maîtrisée.

---

## 14.3 Inconvénients

- points d’extension à concevoir ;
- contrats à maintenir ;
- projet supplémentaire ;
- certaines adaptations nécessitent une évolution du socle ;
- compilation complète lors d’un upgrade ;
- discipline architecturale.

---

## 14.4 Risques

- point d’extension trop puissant ;
- contrat trop spécifique ;
- accès indirect aux détails internes ;
- customizations trop nombreuses ;
- dépendances mal déclarées ;
- généralisation trop précoce.

---

## 14.5 Réversibilité

```text
élevée
```

Le code client reste identifiable, versionné et supprimable.

---

# 15. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Fork | Conditions client | Plugins runtime | Extensions explicites |
|---|---:|---:|---:|---:|---:|
| Protection du socle | 5 | 2 | 1 | 4 | 5 |
| Capacité d’upgrade | 5 | 1 | 2 | 3 | 5 |
| Isolation client | 5 | 3 | 1 | 4 | 5 |
| Sécurité | 5 | 3 | 2 | 1 | 5 |
| Compilation | 4 | 5 | 5 | 2 | 5 |
| Testabilité | 5 | 3 | 3 | 3 | 5 |
| Déterminisme | 5 | 3 | 4 | 2 | 5 |
| Transfert client | 4 | 4 | 2 | 3 | 5 |
| Maintenabilité | 5 | 2 | 1 | 2 | 5 |
| Réversibilité | 4 | 2 | 1 | 2 | 5 |

L’option D est retenue.

---

# 16. Décision

Nous décidons que les adaptations spécifiques à un client sont développées dans une zone dédiée de son repository.

Structure cible :

```text
src/
└── Client.Customizations/
```

ou, avec un nom généré :

```text
src/
└── ExampleProject.Customizations/
```

Tests :

```text
tests/
└── Client.Customizations.Tests/
```

Les customizations sont composées statiquement avec l’application.

---

## 16.1 Propriété

Les fichiers de cette zone utilisent :

```text
strategy: custom
```

conformément à l’ADR-0005.

SiteFactory ne les modifie ni ne les supprime automatiquement.

---

## 16.2 Référence inverse interdite

Les packages SiteFactory ne référencent jamais le projet `Client.Customizations`.

---

## 16.3 Composition

Le composition root généré peut appeler un contrat d’enregistrement stable fourni par le projet client.

---

# 17. Hiérarchie de décision

Avant toute personnalisation, appliquer l’ordre suivant :

```text
1. Contenu
2. Configuration
3. Thème
4. Composant ou variante existante
5. Option de module
6. Module standard
7. Point d’extension
8. Module d’intégration
9. Module client dédié
10. Projet sur mesure
```

---

## 17.1 Justification obligatoire

Le recours au code spécifique doit expliquer pourquoi les niveaux précédents ne suffisent pas.

---

# 18. Catégories de customizations

## 18.1 Customization de présentation

Exemples :

- section spécifique ;
- composant client ;
- variante d’affichage ;
- mise en page particulière.

---

## 18.2 Customization de politique métier

Exemples :

- stratégie de scoring ;
- règle d’éligibilité ;
- règle de tri ;
- règle de validation ;
- mapping métier.

---

## 18.3 Customization d’intégration

Exemples :

- CRM ;
- ERP ;
- API fournisseur ;
- export métier ;
- web service client.

---

## 18.4 Customization de workflow

Exemples :

- routage d’un lead ;
- notification ;
- enrichissement ;
- tâche planifiée ;
- transformation de données.

---

## 18.5 Module client

Capacité spécifique possédant son propre domaine fonctionnel.

---

# 19. Points d’extension

Un point d’extension est un contrat public volontairement prévu pour accepter une contribution.

Il doit être :

- identifié ;
- documenté ;
- versionné ;
- borné ;
- sécurisé ;
- testable ;
- observable ;
- déterministe.

---

## 19.1 Pas d’extension implicite

Le simple fait qu’un service soit enregistré dans DI ne signifie pas qu’il est remplaçable par un client.

---

## 19.2 Pas de dépendance à l’ordre accidentel

Une customization ne doit pas fonctionner uniquement parce qu’elle est enregistrée après un service générique.

---

# 20. Manifest d’un point d’extension

Chaque point d’extension doit pouvoir documenter :

```text
ExtensionPointId

Owner

ContractVersion

Cardinality

ExecutionOrder

Lifetime

Input

Output

FailurePolicy

Authorization

DataAccess

Timeout

Observability

Compatibility
```

---

## 20.1 Exemple conceptuel

```yaml
extensionPoint:
  id: sample-records.routing-policy
  owner: sample-records
  contractVersion: "1.0.0"
  cardinality: single
  executionOrder: explicit
  failurePolicy: fail-closed
```

La structure définitive appartient aux manifests.

---

# 21. Cardinalité

Valeurs conceptuelles :

```text
single

multiple

ordered-multiple
```

---

## 21.1 `single`

Un seul contributeur peut être actif.

Exemple :

```text
devis-pricing-policy
```

---

## 21.2 `multiple`

Plusieurs contributeurs indépendants peuvent être exécutés.

Exemple :

```text
lead-created-observers
```

---

## 21.3 `ordered-multiple`

Plusieurs contributeurs sont exécutés dans un ordre déterministe.

Ce mode doit rester exceptionnel.

---

## 21.4 Collision

Deux implémentations d’un point `single` constituent une erreur de composition.

---

# 22. Ordre d’exécution

L’ordre ne doit pas dépendre :

- de l’ordre de découverte ;
- de la réflexion ;
- de l’ordre des fichiers ;
- de l’ordre d’enregistrement DI ;
- du système d’exploitation.

---

## 22.1 Ordre explicite

Lorsque nécessaire, chaque contribution déclare :

- priorité ;
- dépendance ;
- identifiant stable.

---

## 22.2 Priorités identiques

Deux contributions incompatibles de même priorité produisent une erreur ou utilisent un ordre secondaire documenté.

---

# 23. Extension point de stratégie

Un point d’extension de stratégie permet de remplacer un comportement variable.

Exemple :

```csharp
public interface IQuotePricingPolicy
{
    Task<QuotePricingResult> CalculateAsync(
        QuotePricingRequest request,
        CancellationToken cancellationToken);
}
```

---

## 23.1 Contrat métier

L’interface exprime une intention métier.

Elle n’expose pas :

- `DbContext` ;
- `HttpContext` complet ;
- `IServiceProvider` ;
- SQL ;
- entité interne.

---

## 23.2 Valeur par défaut

Le module peut fournir une implémentation par défaut.

La composition doit déclarer explicitement si la customization la remplace.

---

# 24. Décorateurs

Une customization peut décorer un comportement uniquement si le point d’extension l’autorise.

---

## 24.1 Décorateur explicite

Le contrat doit prévoir :

- l’ordre ;
- la responsabilité ;
- les erreurs ;
- les effets secondaires.

---

## 24.2 Décoration DI implicite

Une customization ne doit pas intercepter arbitrairement tous les services d’un type uniquement via l’ordre de DI.

---

# 25. Extension point de validation

Une validation client peut compléter les règles génériques.

Exemple :

```text
validation générique
+ validation métier client
→ résultat combiné
```

---

## 25.1 Réduction interdite

Une validation client ne peut pas désactiver une règle de sécurité ou un invariant générique obligatoire.

---

## 25.2 Erreurs

Les erreurs utilisent des codes stables propres au contrat client.

---

# 26. Extension point d’événement

Une customization peut réagir à un événement d’intégration public.

Exemple :

```text
LeadCreatedIntegrationEvent
→ notification vers le CRM client
```

---

## 26.1 Propriété des données

Le handler reçoit uniquement les données nécessaires.

Il ne charge pas directement l’entité interne du module émetteur.

---

## 26.2 Échec

La politique d’échec doit préciser :

- bloquant ;
- non bloquant ;
- retry ;
- dead-letter ;
- audit.

---

## 26.3 Durabilité

Une réaction critique nécessitant une reprise après crash doit utiliser une stratégie durable adaptée.

---

# 27. Extension point UI

Un module ou template peut exposer un slot UI.

Exemples :

```text
admin.dashboard.after-summary

lead.details.sidebar

public.home.after-hero
```

---

## 27.1 Contrat

Le slot définit :

- données disponibles ;
- contexte ;
- autorisation ;
- nombre de contributions ;
- ordre ;
- comportement en erreur.

---

## 27.2 Données minimales

Le composant client ne reçoit pas automatiquement l’intégralité d’une entité ou d’un contexte privé.

---

## 27.3 Isolation CSS

Les styles spécifiques doivent être :

- limités ;
- nommés ;
- compatibles avec le design system ;
- testés sur les pages concernées.

---

## 27.4 Remplacement total

Le remplacement complet d’une page standard n’est pas un slot ordinaire.

Il nécessite un contrat explicite de page override ou un module client.

---

# 28. Extension point de navigation

Une customization peut contribuer à la navigation via un contrat.

Elle déclare :

- identifiant ;
- label ;
- route ;
- ordre ;
- permission ;
- capability ;
- icône autorisée ;
- section.

---

## 28.1 Route

La route doit être connue et validée.

---

## 28.2 Autorisation

Masquer l’entrée ne remplace pas le contrôle de la page ou de l’endpoint.

---

# 29. Extension point d’intégration

Une intégration externe doit être placée derrière une abstraction métier ou technique dédiée.

Exemple :

```csharp
public interface IClientCrmGateway
{
    Task<CrmPushResult> PushLeadAsync(
        CrmLeadPayload payload,
        CancellationToken cancellationToken);
}
```

---

## 29.1 Secrets

Les credentials sont injectés par l’environnement.

Ils ne sont pas contenus dans :

- le code ;
- `client.sitefactory.yml` ;
- le lock file ;
- les rapports.

---

## 29.2 Résilience

L’intégration doit définir :

- timeout ;
- retry ;
- idempotence ;
- circuit breaker éventuel ;
- kill switch ;
- observabilité ;
- données journalisées.

---

# 30. Extension point de mapping

Un mapping client peut convertir un contrat public vers un format externe.

---

## 30.1 Minimisation

Le mapping ne doit pas recevoir des données non nécessaires.

---

## 30.2 Données personnelles

Tout transfert de données personnelles doit être :

- justifié ;
- documenté ;
- limité ;
- protégé ;
- conforme aux décisions applicables.

---

# 31. Extension point de stockage

Un remplacement de stockage n’est autorisé que via une abstraction de plateforme prévue.

---

## 31.1 Interdiction

Une customization ne doit pas remplacer arbitrairement :

- PostgreSQL ;
- Identity ;
- Data Protection ;
- le système de backup ;

par simple enregistrement DI.

Ces changements nécessitent une décision d’architecture propre.

---

# 32. Extension point de sécurité

Les règles de sécurité fondamentales ne sont pas extensibles vers une réduction de protection.

Une customization peut ajouter :

- validation ;
- permission ;
- audit ;
- restriction.

Elle ne peut pas :

- désactiver l’antiforgery ;
- rendre un endpoint public ;
- contourner l’autorisation ;
- élargir CORS ;
- exposer un secret ;
- ignorer un kill switch.

---

# 33. Registre des extensions

Le composition root généré conserve un registre explicite des customizations.

Exemple conceptuel :

```csharp
public static class ClientCustomizationRegistration
{
    public static IServiceCollection AddClientCustomizations(
        this IServiceCollection services,
        ClientCustomizationContext context)
    {
        services.AddSingleton<IQuotePricingPolicy, AlphaQuotePricingPolicy>();

        return services;
    }
}
```

---

## 33.1 Propriété du fichier

Le fichier contenant l’implémentation client est `custom`.

Le fichier généré appelant l’enregistrement reste `generated`.

---

## 33.2 Contrat stable

Le point d’entrée d’enregistrement doit rester minimal et versionné.

---

# 34. Découverte des customizations

La découverte runtime par scan d’assemblies est interdite dans le MVP.

---

## 34.1 Composition explicite

Le projet ou namespace à composer est connu pendant la génération.

---

## 34.2 Avantage

Les erreurs sont détectées :

- au build ;
- pendant la génération ;
- dans les tests ;
- avant le déploiement.

---

# 35. Configuration des customizations

Les paramètres non secrets peuvent être déclarés dans une section typée.

Exemple conceptuel :

```yaml
customizations:
  quotePricing:
    pricingModel: professional-services
    minimumAmount: 500
```

La structure exacte appartient au schéma client.

---

## 35.1 Configuration inconnue

Une propriété inconnue est refusée.

---

## 35.2 Secrets

Les références de secrets restent séparées des valeurs.

---

## 35.3 Configuration opérationnelle

Les paramètres dépendant de l’environnement utilisent la configuration runtime prévue.

---

# 36. Projet `Customizations`

Le projet client doit rester aussi petit que possible.

Il peut contenir :

```text
Registration

Policies

Validators

Integrations

Handlers

Ui

Options

Contracts client internes

Diagnostics
```

---

## 36.1 Infrastructure spécifique

Une dépendance fournisseur peut être isolée dans :

```text
Client.Customizations.Infrastructure
```

lorsque la taille du projet le justifie.

---

## 36.2 Découpage excessif

Un projet ne doit pas être créé pour chaque classe ou extension mineure.

---

# 37. Dépendances autorisées

Le projet de customizations peut dépendre :

- des packages `Contracts` de la plateforme ;
- des packages `Contracts` des modules ;
- des abstractions transversales approuvées ;
- de bibliothèques externes approuvées ;
- de contrats propres au client.

---

## 37.1 Dépendances interdites

Il ne dépend pas :

- des projets internes SiteFactory non publics ;
- des namespaces `.Internal` ;
- des migrations génériques ;
- du CLI ;
- du générateur ;
- des implémentations privées de modules ;
- du `DbContext` d’un autre module.

---

# 38. Contrats consommés

Toute dépendance vers un module utilise ses contrats publics conformément à l’ADR-0007.

---

## 38.1 Build comme contrôle

Une rupture de contrat pendant un upgrade doit provoquer un échec de compilation visible.

---

## 38.2 Contournement par réflexion

La réflexion ne doit pas être utilisée pour accéder à :

- un membre interne ;
- un type privé ;
- un service non contractuel.

---

# 39. Propriété des données

Une customization ne devient pas propriétaire des données d’un module standard.

---

## 39.1 Lecture

Elle lit les données via :

- query contract ;
- DTO public ;
- événement ;
- projection publique.

---

## 39.2 Écriture

Elle modifie les données via :

- commande publique ;
- cas d’usage public ;
- contrat métier.

---

## 39.3 Accès direct interdit

Exemples interdits :

```text
SELECT direct sur une table privée

DbSet d’un module

repository interne

navigation EF privée

mise à jour SQL d’une table de module
```

---

# 40. Données propres à la customization

Une personnalisation peut posséder ses propres données.

Elle doit alors définir :

- modèle ;
- tables ;
- migrations ;
- contraintes ;
- index ;
- rétention ;
- suppression ;
- backup ;
- restauration ;
- autorisation.

---

## 40.1 Préfixe ou convention

Les objets persistants propres au client doivent être clairement identifiables.

---

## 40.2 Migrations

Les migrations restent dans le repository client.

Elles suivent l’ADR-0004 en production.

---

## 40.3 Clé étrangère vers un module

Les clés étrangères physiques vers les tables privées d’un module sont interdites par défaut.

Une référence utilise un identifiant public.

---

# 41. Module client dédié

Une customization doit devenir un module client lorsque plusieurs critères sont présents :

```text
plusieurs cas d’usage

données propres

permissions propres

routes propres

UI propre

migrations propres

cycle de vie propre

tests d’intégration importants
```

---

## 41.1 Identifiant

Convention conceptuelle :

```text
client-{client-technical-id}-{capability}
```

ou namespace propre au repository client.

L’identifiant ne doit pas prétendre être un module générique SiteFactory.

---

## 41.2 Manifest

Le module client possède un manifest et des contrats selon l’ADR-0007.

---

## 41.3 Distribution

Il reste généralement compilé depuis le repository client.

Il n’est pas publié sur le feed générique SiteFactory.

---

# 42. Module d’intégration client

Une intégration reliant un module standard à un système client peut être isolée dans un module dédié.

Exemple :

```text
sample-project-lead-crm-integration
```

---

## 42.1 Dépendances

Il déclare explicitement :

- le module standard ;
- les contrats d’intégration ;
- le fournisseur externe ;
- les permissions ;
- les flags ;
- les kill switches.

---

# 43. Customizations et fichiers générés

Une customization ne doit pas modifier directement un fichier `generated`.

---

## 43.1 Besoin de modification

Si un fichier généré ne permet pas l’adaptation :

1. vérifier si un point d’extension existe ;
2. proposer un point d’extension générique ;
3. créer une tâche SiteFactory ;
4. mettre à niveau le package ou template ;
5. consommer le nouveau contrat.

---

## 43.2 Fork local temporaire

Un fork local d’un fichier généré nécessite une décision explicite et devient une dette à éliminer.

Il ne doit pas être masqué par la modification de l’état de génération.

---

# 44. Customizations et fichiers `create-once`

Un fichier `create-once` peut être adapté par le projet client.

Il ne doit cependant pas contenir de logique métier cachée si un projet de customizations est plus approprié.

---

# 45. Customizations et fichiers `protected`

Une customization ne modifie pas :

- `client.sitefactory.yml` pendant une génération ;
- les migrations validées ;
- les secrets ;
- les artefacts de transfert ;
- les états sensibles ;

sans opération dédiée.

---

# 46. Styles client

## 46.1 Priorité au design system

Les styles utilisent d’abord :

- tokens ;
- variables ;
- variantes ;
- classes utilitaires approuvées ;
- composants publics.

---

## 46.2 CSS spécifique

Le CSS spécifique doit être :

- isolé ;
- préfixé ;
- limité ;
- documenté ;
- testé ;
- sans remplacement global fragile.

---

## 46.3 Sélecteurs interdits

Éviter les sélecteurs dépendant de la structure interne d’un composant.

Exemple fragile :

```css
.module-internal > div:nth-child(2) span
```

---

## 46.4 Contrat visuel

Un composant extensible doit exposer :

- slot ;
- classe publique ;
- token ;
- paramètre ;

plutôt que forcer un override interne.

---

# 47. Pages et sections spécifiques

Une page purement spécifique au client peut appartenir au projet `Customizations`.

---

## 47.1 Route

Elle déclare :

- route ;
- permission ;
- capability ;
- navigation éventuelle ;
- SEO éventuel.

---

## 47.2 Collision

Une route client ne peut pas entrer en collision avec une route générée.

---

## 47.3 Composants standards

La page doit réutiliser le design system et les composants publics lorsque possible.

---

# 48. Autorisation

Toute personnalisation sensible doit appliquer l’autorisation côté serveur.

---

## 48.1 Permissions client

Une permission propre au client peut être créée.

Elle doit posséder :

- identifiant stable ;
- description ;
- rôles par défaut éventuels ;
- tests ;
- migration ou seed.

---

## 48.2 Rôle administrateur

Le rôle administrateur ne contourne pas :

- entitlement ;
- feature flag ;
- kill switch ;
- règle de sécurité obligatoire.

---

# 49. Capabilities

Une customization peut déclarer une capability propre au client.

---

## 49.1 Entitlement

Une capability spécifique à un projet signé peut être considérée comme incluse dans le périmètre client sans créer automatiquement une entrée de catalogue générique.

---

## 49.2 Feature flag

Un rollout technique reste distinct du droit commercial.

---

## 49.3 Kill switch

Une intégration ou automatisation sensible doit posséder un kill switch lorsque nécessaire.

---

# 50. Gestion des erreurs

Les customizations utilisent :

- résultats structurés ;
- codes d’erreur ;
- exceptions contrôlées ;
- mapping HTTP centralisé.

---

## 50.1 Exceptions techniques

Une exception fournisseur ne doit pas être exposée directement à l’utilisateur.

---

## 50.2 Codes

Convention recommandée :

```text
SF-CLIENT-{CLIENT-ID}-{AREA}-{NUMBER}
```

ou une convention plus courte documentée dans le repository client.

---

# 51. Résilience

Une customization appelant un système externe doit définir :

- timeout ;
- cancellation ;
- retry ;
- idempotence ;
- circuit breaker éventuel ;
- traitement des doublons ;
- stratégie de reprise ;
- observabilité.

---

## 51.1 Retry

Un retry ne doit pas dupliquer :

- lead ;
- commande ;
- email ;
- paiement ;
- export.

---

## 51.2 Effet non critique

Un effet non critique ne doit pas nécessairement bloquer le cas d’usage principal.

La décision doit être explicite.

---

# 52. Background jobs

Un job client doit déclarer :

- identifiant ;
- cadence ;
- propriétaire ;
- lock ;
- idempotence ;
- timeout ;
- kill switch ;
- observabilité ;
- rétention.

---

## 52.1 Cadence

Une cadence ne doit pas être codée en dur si elle dépend de l’environnement.

---

## 52.2 Exécution concurrente

Les exécutions concurrentes doivent être empêchées lorsque nécessaires.

---

# 53. Sécurité du code client

Le code spécifique client s’exécute dans le même processus que l’application.

Il est donc considéré comme :

```text
code de confiance soumis aux mêmes exigences que le socle
```

Il n’est pas sandboxé.

---

## 53.1 Revue

Toute customization doit subir :

- revue de code ;
- build ;
- tests ;
- analyse statique ;
- secret scanning ;
- dependency scanning ;
- vérification d’autorisation.

---

## 53.2 APIs dangereuses

Les usages suivants nécessitent une revue renforcée :

- exécution de processus ;
- accès filesystem ;
- réflexion ;
- désérialisation ;
- cryptographie ;
- SQL brut ;
- appels réseau ;
- HTML non encodé ;
- chargement dynamique ;
- fichiers uploadés.

---

# 54. Dépendances externes des customizations

Une dépendance propre au client doit suivre l’ADR-0008.

Elle doit être :

- justifiée ;
- versionnée ;
- verrouillée ;
- scannée ;
- licenciée ;
- restaurée depuis une source approuvée.

---

## 54.1 Source spécifique client

Une source spécifique nécessite :

- mapping ;
- permissions ;
- transfert ;
- continuité ;
- validation humaine.

---

# 55. Tests unitaires

Chaque customization métier doit tester :

- comportement nominal ;
- validations ;
- erreurs ;
- limites ;
- cancellation ;
- données invalides ;
- règles spécifiques.

---

# 56. Tests de contrats

Les tests doivent prouver que la customization respecte :

- le contrat du point d’extension ;
- les entrées ;
- les sorties ;
- la politique d’erreur ;
- la cardinalité ;
- le timeout ;
- l’autorisation.

---

# 57. Tests d’architecture

Les tests doivent vérifier :

```text
Customizations ne référence pas les implémentations internes.

Customizations ne référence pas le générateur.

Customizations ne référence pas le CLI.

Customizations ne référence pas les DbContext privés.

Customizations ne dépend pas de namespaces Internal.

Le socle ne dépend pas de Customizations.

Aucun cycle n’existe.
```

---

# 58. Tests d’intégration

Tester selon le besoin :

- composition DI ;
- endpoint ;
- PostgreSQL ;
- migration ;
- événement ;
- fournisseur externe simulé ;
- autorisation ;
- feature flag ;
- kill switch ;
- UI.

---

# 59. Tests de contournement

Vérifier notamment :

- appel direct à l’endpoint ;
- utilisateur sans permission ;
- module absent ;
- entitlement absent ;
- kill switch actif ;
- configuration invalide ;
- fournisseur externe indisponible.

---

# 60. Tests d’upgrade

Chaque upgrade SiteFactory doit compiler et tester le projet de customizations.

---

## 60.1 Matrice

Le plan d’upgrade doit vérifier :

```text
ancienne plateforme
+ customization actuelle

nouvelle plateforme
+ customization actuelle

nouvelle plateforme
+ customization adaptée
```

selon le type de changement.

---

## 60.2 Contrat déprécié

L’usage d’un contrat déprécié doit être visible avant son retrait.

---

## 60.3 Rupture

Une rupture de contrat bloque l’upgrade jusqu’à adaptation.

---

# 61. Golden project

Le projet de référence SiteFactory doit inclure au moins une customization de démonstration.

Elle doit prouver :

- enregistrement explicite ;
- contrat public ;
- test ;
- absence de dépendance interne ;
- upgrade contrôlé.

---

# 62. Tests UI

Une personnalisation visuelle doit être testée sur :

- mobile ;
- tablette lorsque pertinent ;
- desktop ;
- états d’erreur ;
- accessibilité ;
- thème ;
- navigation clavier.

---

# 63. Quality gates

Une customization est bloquée lorsque :

```text
Référence à une implémentation interne

Modification d’un fichier generated

Accès direct à une table privée

Dépendance circulaire

Route en collision

Permission absente

Contrôle uniquement UI

Secret dans le code

Dépendance non approuvée

Migration non testée

Point d’extension inconnu

Contrat incompatible

Build échoué

Tests échoués
```

---

## 63.1 Upgrade

Un upgrade est bloqué si :

```text
Customizations ne compile plus

Contrat public incompatible

Test client échoué

Migration client non inspectée

Point d’extension supprimé sans migration

Diff hors périmètre
```

---

# 64. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-CUSTOM-001` | Projet de customizations invalide |
| `SF-CUSTOM-002` | Point d’extension inconnu |
| `SF-CUSTOM-003` | Version de contrat incompatible |
| `SF-CUSTOM-004` | Plusieurs implémentations pour un point `single` |
| `SF-CUSTOM-005` | Ordre d’extension ambigu |
| `SF-CUSTOM-006` | Dépendance vers une implémentation interne |
| `SF-CUSTOM-007` | Accès direct aux données d’un module |
| `SF-CUSTOM-008` | Modification d’un fichier généré requise |
| `SF-CUSTOM-009` | Route client en collision |
| `SF-CUSTOM-010` | Permission client inconnue |
| `SF-CUSTOM-011` | Capability client invalide |
| `SF-CUSTOM-012` | Configuration client invalide |
| `SF-CUSTOM-013` | Secret présent dans une configuration versionnée |
| `SF-CUSTOM-014` | Dépendance externe non approuvée |
| `SF-CUSTOM-015` | Migration client manquante |
| `SF-CUSTOM-016` | Propriété de données ambiguë |
| `SF-CUSTOM-017` | Point d’extension déprécié |
| `SF-CUSTOM-018` | Point d’extension retiré |
| `SF-CUSTOM-019` | Cycle de dépendance |
| `SF-CUSTOM-020` | Enregistrement implicite interdit |
| `SF-CUSTOM-021` | API de sécurité obligatoire contournée |
| `SF-CUSTOM-022` | Personnalisation générique mal localisée |
| `SF-CUSTOM-023` | Customization trop large pour un hook |
| `SF-CUSTOM-024` | Module client requis |
| `SF-CUSTOM-025` | Build client incompatible avec la plateforme |

---

# 65. Observabilité

Événements recommandés :

```text
ClientCustomizationRegistered

ClientCustomizationExecuted

ClientCustomizationFailed

ClientExtensionPointResolved

ClientExtensionPointConflict

ClientIntegrationCallStarted

ClientIntegrationCallCompleted

ClientIntegrationCallFailed
```

---

## 65.1 Propriétés autorisées

```text
CustomizationId

ExtensionPointId

ContractVersion

ClientSlug

Environment

Operation

Result

Duration

ErrorCode

CorrelationId
```

---

## 65.2 Données interdites

```text
Secret

Token

Mot de passe

Corps complet d’une requête

Données métier inutiles

Données personnelles non nécessaires
```

---

# 66. Identifiant d’une customization

Chaque customization significative possède un identifiant stable.

Convention conceptuelle :

```text
{client-technical-id}.{area}.{behavior}
```

Exemples :

```text
sample-project.quote.pricing-policy

sample-project.leads.crm-export

sample-project.admin.custom-dashboard
```

---

## 66.1 Nom commercial

Le nom commercial du client ne doit pas nécessairement apparaître dans les packages génériques, logs centralisés ou contrats publics SiteFactory.

---

# 67. Version des customizations

Le code client suit la version de l’application cliente.

Une customization complexe peut également posséder une version interne explicite.

---

## 67.1 Contrats

Le repository doit pouvoir identifier les versions des contrats publics consommés.

---

## 67.2 Lock file

Le lock file peut enregistrer :

- projet de customizations présent ;
- extension points consommés ;
- versions de contrats ;
- module client éventuel.

Il ne contient pas le code ni les secrets.

---

# 68. Changelog

Les changements significatifs de customization apparaissent dans le changelog client.

Catégories possibles :

```text
Added

Changed

Fixed

Security

Removed
```

---

## 68.1 Changelog SiteFactory

Une adaptation spécifique ne doit pas apparaître dans le changelog générique SiteFactory sauf évolution réelle d’un contrat public.

---

# 69. Upgrade SiteFactory

Le flux recommandé est :

```text
upgrade check
→ identifier les contrats consommés
→ détecter les dépréciations
→ créer une branche
→ générer le nouveau plan
→ mettre à jour les packages
→ compiler les customizations
→ adapter si nécessaire
→ exécuter les tests
→ staging
→ revue
→ production
```

---

## 69.1 Pas d’adaptation silencieuse

SiteFactory ne réécrit pas automatiquement le code client pour suivre une nouvelle API.

---

## 69.2 Aide à la migration

L’upgrade peut produire :

- contrat ancien ;
- contrat cible ;
- guide ;
- chemins affectés ;
- diagnostics.

---

# 70. Dépréciation d’un point d’extension

Un point d’extension public ne doit pas être retiré immédiatement.

---

## 70.1 Métadonnées

La dépréciation indique :

- version d’introduction ;
- remplacement ;
- version de retrait ;
- guide de migration.

---

## 70.2 Période de transition

Une période de compatibilité doit permettre d’adapter les projets clients supportés.

---

## 70.3 Suppression

Le retrait nécessite une version majeure du contrat lorsque le changement est incompatible.

---

# 71. Évolution d’un point d’extension

## 71.1 Ajout compatible

Exemples :

- nouvelle propriété facultative ;
- nouveau contrat indépendant ;
- nouvelle surcharge correctement versionnée.

---

## 71.2 Changement incompatible

Exemples :

- suppression ;
- renommage ;
- changement de sens ;
- changement d’ordre ;
- nouvelle propriété obligatoire ;
- nouvelle politique d’échec.

---

## 71.3 Version

Une évolution incompatible produit une nouvelle version du point d’extension ou du package de contrats.

---

# 72. Généralisation d’une customization

Une personnalisation peut devenir une capacité générique si :

- plusieurs projets ont le même besoin ;
- le modèle est stable ;
- aucune donnée client n’est intégrée ;
- la capacité est configurable ;
- les contrats sont généralisables ;
- les tests sont génériques ;
- le catalogue peut l’exprimer.

---

## 72.1 Processus

```text
retour d’expérience
→ analyse
→ proposition
→ ADR ou tâche
→ extraction
→ anonymisation
→ module générique
→ version
→ migration éventuelle
```

---

## 72.2 Pas de déplacement direct

Le code client ne doit pas être copié tel quel dans le socle sans :

- revue ;
- nettoyage ;
- généralisation ;
- sécurité ;
- documentation ;
- tests.

---

# 73. Refus de généralisation

Une personnalisation reste cliente lorsque :

- elle dépend du processus interne du client ;
- elle possède peu de chances de réutilisation ;
- son contrat n’est pas stable ;
- elle utilise un fournisseur exclusif ;
- elle contient des règles confidentielles ;
- sa maintenance générique serait disproportionnée.

---

# 74. Suppression d’une customization

Le retrait suit :

```text
identifier les appels
→ désactiver la capability
→ sauvegarder les données
→ retirer l’enregistrement
→ retirer le code
→ retirer les routes
→ retirer les jobs
→ conserver ou migrer les données
→ mettre à jour le lock file
→ tester
```

---

## 74.1 Données

La suppression du code ne supprime jamais automatiquement ses données.

---

## 74.2 Secrets

Les secrets devenus inutiles sont révoqués séparément.

---

## 74.3 Fournisseur externe

Les webhooks, comptes et tokens externes doivent être désactivés selon le runbook.

---

# 75. Transfert client

Le transfert doit inclure :

- code source des customizations selon le contrat ;
- documentation ;
- contrats consommés ;
- dépendances ;
- versions ;
- migrations ;
- secrets à recréer ;
- runbooks ;
- tests ;
- responsabilités.

---

## 75.1 Packages privés

Les packages nécessaires doivent rester disponibles conformément à l’ADR-0008.

---

## 75.2 Points d’extension

Le dossier de handover doit préciser que les customizations dépendent de contrats publics versionnés.

---

# 76. Documentation obligatoire

Chaque customization significative doit documenter :

```text
Objectif

Périmètre

Point d’extension

Contrats consommés

Configuration

Secrets nécessaires

Données

Permissions

Capabilities

Feature flags

Kill switches

Erreurs

Tests

Runbook

Procédure de retrait

Impact upgrade
```

---

# 77. Règles pour LogicGate

LogicGate doit :

- privilégier la configuration ;
- limiter le code spécifique ;
- préserver les contrats publics ;
- identifier le propriétaire des données ;
- revoir les dépendances ;
- tester les upgrades ;
- décider des généralisations ;
- documenter les customizations importantes.

---

## 77.1 LogicGate ne doit pas

- ajouter une condition client dans le socle ;
- exposer une implémentation interne par commodité ;
- modifier un fichier généré sans traiter la cause ;
- créer un point d’extension uniquement pour contourner une mauvaise frontière ;
- généraliser trop tôt une règle locale.

---

# 78. Règles pour Codex

Codex peut :

- implémenter une customization définie ;
- consommer un point d’extension public ;
- créer les tests ;
- documenter le comportement ;
- signaler l’absence de contrat adapté ;
- proposer un module client.

---

## 78.1 Codex ne doit pas

```text
Modifier les packages génériques pour un seul client

Ajouter if clientSlug dans le socle

Référencer un namespace Internal

Accéder directement au DbContext d’un module

Modifier les fichiers generated

Modifier generation-state.json pour masquer un conflit

Créer un plugin runtime

Ajouter une source NuGet non approuvée

Généraliser automatiquement la customization

Appliquer une migration de production

Inventer un point d’extension non documenté
```

---

## 78.2 Point d’extension manquant

Si le besoin ne peut pas être implémenté proprement :

1. arrêter ;
2. identifier le contrat manquant ;
3. proposer une évolution générique ;
4. demander une décision ;
5. créer une tâche distincte.

---

# 79. Anti-patterns interdits

## 79.1 `if client`

```csharp
if (clientSlug == "alpha")
```

dans un package générique.

---

## 79.2 Héritage d’une classe interne

Une customization hérite d’une implémentation non contractuelle pour modifier son comportement.

---

## 79.3 Service locator

La customization utilise `IServiceProvider` pour rechercher des services internes non déclarés.

---

## 79.4 Accès SQL direct

La customization requête directement les tables d’un module.

---

## 79.5 Modification d’un `.g.cs`

Le code généré est édité pour ajouter une règle client.

---

## 79.6 CSS global fragile

Une customization remplace des sélecteurs internes dans toute l’application.

---

## 79.7 Configuration cachée dans le code

Une valeur dépendant du client est codée en dur alors qu’elle devrait être configurée.

---

## 79.8 Extension universelle

Un point d’extension fournit un accès complet à :

```text
IServiceProvider

DbContext

HttpContext

filesystem

configuration brute
```

sans limites.

---

## 79.9 Customization sans tests

Le code client est considéré comme trop spécifique pour justifier des tests.

---

## 79.10 Généralisation prématurée

Une règle utilisée par un seul client devient immédiatement un module générique.

---

## 79.11 Plugin dynamique

Une assembly est copiée sur le serveur et chargée sans rebuild.

---

## 79.12 Désactivation de sécurité

Une customization réduit une protection générique afin de satisfaire un besoin local.

---

# 80. Quality gates architecturaux

Une customization ne peut pas être acceptée si :

```text
Le besoin est déjà couvert par configuration.

Le socle contient une condition client.

Une implémentation interne est référencée.

Un fichier generated est modifié.

Une table privée est accédée directement.

Un point d’extension public manque.

La propriété des données est ambiguë.

La sécurité est réduite.

La dépendance n’est pas approuvée.

Le projet ne compile pas indépendamment.

Les tests d’architecture échouent.

L’impact d’upgrade n’est pas documenté.
```

---

# 81. Conséquences positives

La décision apporte :

- socle générique préservé ;
- code client identifiable ;
- upgrades vérifiables ;
- contrats explicites ;
- testabilité ;
- transfert simplifié ;
- meilleure propriété des données ;
- réduction des forks ;
- absence de plugins runtime ;
- généralisation contrôlée ;
- customizations supprimables ;
- meilleure traçabilité commerciale et technique.

---

# 82. Conséquences négatives

La décision entraîne :

- besoin de concevoir des points d’extension ;
- projets supplémentaires ;
- contrats à versionner ;
- compilation des customizations ;
- adaptations lors de certaines mises à niveau ;
- tests propres au client ;
- documentation supplémentaire ;
- risque de multiplication des hooks ;
- coût de maintien des anciennes versions de contrats ;
- discipline de développement.

---

# 83. Risques acceptés

## 83.1 Code client dans le même processus

Une customization défaillante peut affecter l’application entière.

Réduction :

- revue ;
- tests ;
- timeouts ;
- résilience ;
- kill switches ;
- observabilité.

---

## 83.2 Contrats trop nombreux

Les besoins clients peuvent multiplier les points d’extension.

Réduction :

- validation ;
- généralisation prudente ;
- modules clients ;
- suppression des contrats inutilisés.

---

## 83.3 Rupture pendant upgrade

Une nouvelle version peut casser une customization.

Réduction :

- SemVer ;
- dépréciation ;
- compilation ;
- tests ;
- upgrade explicite.

---

## 83.4 Dette client

Une adaptation peut rester longtemps spécifique.

Ce risque est accepté lorsqu’elle ne justifie pas un module générique.

---

# 84. Mesures compensatoires

- hiérarchie de personnalisation ;
- zone `custom` ;
- composition statique ;
- contrats publics ;
- tests d’architecture ;
- versionnement ;
- dépréciation ;
- module client ;
- observabilité ;
- quality gates ;
- documentation ;
- upgrade plan ;
- code review ;
- scans de sécurité.

---

# 85. Sécurité

## 85.1 Actifs

- code client ;
- données ;
- secrets ;
- intégrations ;
- permissions ;
- contrats ;
- packages ;
- migrations.

---

## 85.2 Menaces

- accès aux données d’un autre module ;
- secret en code ;
- intégration non sécurisée ;
- contournement d’autorisation ;
- dependency confusion ;
- route publique involontaire ;
- personnalisation malveillante ;
- SQL non paramétré ;
- code dynamique.

---

## 85.3 Contrôles

- contrats minimaux ;
- composition statique ;
- sources approuvées ;
- moindre privilège ;
- tests directs ;
- secrets hors Git ;
- analyse statique ;
- revue ;
- kill switches ;
- logs minimisés.

---

# 86. Performance

## 86.1 Même processus

Une customization n’ajoute pas de coût réseau interne.

---

## 86.2 Point d’extension critique

Une extension exécutée sur chaque requête doit être :

- bornée ;
- mesurée ;
- sans I/O inutile ;
- sans allocation excessive ;
- sans appel externe non contrôlé.

---

## 86.3 Intégration externe

Un appel externe ne doit pas bloquer indéfiniment le traitement principal.

---

## 86.4 Mesure

Les performances sont vérifiées sur les customizations critiques ou fréquentes.

---

# 87. Coûts

## 87.1 Coûts supplémentaires

- conception de contrats ;
- tests client ;
- maintenance ;
- upgrades ;
- documentation ;
- support d’intégrations externes.

---

## 87.2 Coûts évités

- fork complet ;
- socle contaminé ;
- régressions croisées ;
- upgrades manuels globaux ;
- dépendances internes ;
- transfert impossible ;
- dette dispersée dans les fichiers générés.

---

# 88. Maintenabilité

Pour chaque adaptation, le repository doit permettre de répondre à :

```text
Pourquoi existe-t-elle ?

Quel besoin client couvre-t-elle ?

Quel point d’extension utilise-t-elle ?

Quels contrats consomme-t-elle ?

Quelles données possède-t-elle ?

Comment la tester ?

Comment la désactiver ?

Comment la supprimer ?

Quel impact aura un upgrade ?
```

---

# 89. Compatibilité

## 89.1 Plateforme

Les customizations déclarent la version de plateforme supportée lorsque nécessaire.

---

## 89.2 Contrats

Les dépendances utilisent des versions explicites.

---

## 89.3 Build

Le projet client est reconstruit pendant chaque upgrade.

---

## 89.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration d’un projet existant fortement forké vers ce modèle peut être structurante.

---

# 90. Migration depuis un projet forké

Le processus d’adoption est :

```text
inventorier les divergences
→ classifier contenu/configuration/thème/code
→ identifier les customizations réelles
→ restaurer les fichiers générés
→ créer les points d’extension nécessaires
→ déplacer le code client
→ créer un module client si nécessaire
→ ajouter les tests
→ construire l’état de génération
→ exécuter un upgrade pilote
```

---

## 90.1 Divergence générique

Une correction réellement générique doit être proposée au socle SiteFactory.

---

## 90.2 Divergence locale

Une règle propre au client reste dans `Customizations`.

---

## 90.3 Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation de modifier un projet de production.

---

# 91. Réversibilité

## 91.1 Classification

```text
élevée
```

Une customization peut être retirée sans modifier le socle lorsqu’elle utilise correctement ses contrats.

---

## 91.2 Limites

La réversibilité diminue si la customization possède :

- beaucoup de données ;
- intégration externe ;
- migration destructive ;
- contrat client ;
- processus opérationnel dépendant.

---

## 91.3 Retour au fork

Un retour au fork complet réduirait les garanties et nécessiterait une décision distincte.

---

# 92. Plan d’implémentation

## Étape 1 — Structure

Ajouter au template de référence :

```text
src/Client.Customizations
tests/Client.Customizations.Tests
```

---

## Étape 2 — Contrat d’enregistrement

Définir un point de composition explicite.

---

## Étape 3 — Règles d’architecture

Interdire les dépendances vers :

- Internal ;
- Infrastructure privée ;
- DbContext privés ;
- CLI ;
- Generator.

---

## Étape 4 — Premiers points d’extension

Implémenter uniquement les points nécessaires au premier projet de référence.

---

## Étape 5 — Manifest

Définir les métadonnées d’un point d’extension.

---

## Étape 6 — Configuration

Ajouter une section client typée si un besoin réel existe.

---

## Étape 7 — Tests

Ajouter :

- architecture ;
- composition ;
- contrats ;
- upgrade ;
- sécurité.

---

## Étape 8 — Exemple

Créer une customization de démonstration sans dépendance interne.

---

## Étape 9 — Upgrade

Intégrer la compilation et les tests des customizations au plan d’upgrade.

---

## Étape 10 — Documentation

Ajouter un guide de création et de retrait d’une customization.

---

# 93. Tâches liées proposées

```text
SF-TASK-084 — Add the client customizations project template

SF-TASK-085 — Define the customization registration contract

SF-TASK-086 — Define extension point metadata

SF-TASK-087 — Add client customization architecture tests

SF-TASK-088 — Add an application strategy extension point

SF-TASK-089 — Add a UI slot extension point

SF-TASK-090 — Add an integration event customization example

SF-TASK-091 — Add customization compatibility checks to upgrade planning

SF-TASK-092 — Add client customization observability

SF-TASK-093 — Document when to create a client module

SF-TASK-094 — Add a client customization removal runbook
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 94. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Le template contient un projet de customizations isolé.

Le socle ne référence pas le projet client.

Le projet client consomme uniquement des contrats publics.

Une référence vers Internal est refusée.

Un accès direct au DbContext d’un module est refusé.

Une customization est enregistrée explicitement.

Un point single refuse plusieurs implémentations.

L’ordre multiple est déterministe.

Une customization UI respecte l’autorisation.

Un appel direct à l’endpoint reste protégé.

Une intégration possède timeout et cancellation.

Un secret n’est pas présent dans Git.

Une dépendance externe est verrouillée et scannée.

Le projet client compile avec la version de plateforme attendue.

Un contrat incompatible bloque l’upgrade.

Les fichiers custom sont préservés par la régénération.

Les fichiers generated ne sont pas modifiés.

Une customization peut être retirée sans altérer le socle.

Le projet généré complet restaure, compile et teste.
```

---

# 95. Conditions de révision

Réexaminer cette décision si :

- des clients doivent installer du code sans recompilation ;
- une marketplace de plugins devient nécessaire ;
- des tiers doivent développer des extensions indépendamment ;
- le nombre de points d’extension devient trop élevé ;
- les customizations nécessitent fréquemment les détails internes ;
- les modules clients deviennent majoritaires ;
- une sandbox d’exécution devient nécessaire ;
- un langage de règles métier est introduit ;
- les contrats publics évoluent trop rapidement ;
- les upgrades restent trop coûteux malgré l’isolation ;
- SiteFactory devient une plateforme SaaS centralisée.

---

# 96. Indicateurs à suivre

- nombre de customizations par client ;
- nombre de points d’extension ;
- points `single` et `multiple` ;
- dépendances publiques consommées ;
- customizations cassées par upgrade ;
- contrats dépréciés utilisés ;
- durée d’adaptation d’un upgrade ;
- modules clients dédiés ;
- customizations généralisées ;
- références internes bloquées ;
- incidents liés au code client ;
- couverture de tests ;
- intégrations sous kill switch ;
- temps d’exécution des extensions critiques.

---

# 97. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des if client apparaissent dans le socle

les fichiers generated sont régulièrement modifiés

les customizations utilisent la réflexion sur les internes

les points d’extension exposent IServiceProvider ou DbContext

les modules clients accèdent directement aux tables standards

les upgrades cassent la majorité des projets clients

les customizations sans tests se multiplient

les secrets sont codés en dur

les dépendances spécifiques utilisent des sources non approuvées

les intégrations externes n’ont pas de kill switch

la zone custom devient un second monolithe non structuré
```

---

# 98. Conséquences observées

Cette section est append-only après mise en œuvre.

## 98.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation du projet
de customizations et des premiers points d’extension.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- point d’extension ;
- customization ;
- bénéfice ou incident ;
- action proposée.

---

# 99. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- Les frontières client/socle, contrats consommés et projets de customizations sont définis dans les sections 10, 16 et 36 à 38 du présent ADR ; aucun document de périmètre propriétaire n'est requis pour appliquer ces restrictions.
- Les contraintes sur les pages et sections spécifiques relèvent de la section 47 du présent ADR ; aucun catalogue propriétaire de pages n'est nécessaire pour les appliquer.
- Les obligations UI, navigation et tests UI sont définies dans les sections 27, 28 et 62 du présent ADR ; les contrats du host s'appliquent sans importer son catalogue propriétaire dans SiteFactory public.
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- `10-feature-flags-and-entitlements.md`
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `21-observability-and-logging.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `27-client-handover-and-service-closure.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0003-postgresql-as-primary-database.md`
- `0004-explicit-production-migrations.md`
- `0005-file-generation-strategies.md`
- `0006-feature-flags-and-entitlements-separation.md`
- `0007-module-composition-and-public-contracts.md`
- `0008-private-package-feed-and-supply-chain.md`

---

# 100. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 95 |

---

# 101. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 102. Synthèse de la décision

```text
Besoin client
→ contenu si possible
→ configuration si possible
→ thème si possible
→ module standard si possible
→ point d’extension si nécessaire
→ module client si la capacité devient importante

Code spécifique
→ repository client
→ projet Client.Customizations
→ fichiers custom
→ compilation statique
→ tests
→ contrats publics uniquement

Socle générique
→ aucun if client
→ aucune dépendance vers le projet client
→ aucune donnée client
→ aucun accès aux customizations

Point d’extension
→ ID stable
→ propriétaire
→ contrat versionné
→ cardinalité
→ ordre déterministe
→ politique d’échec
→ sécurité
→ observabilité

Interdictions
→ pas de modification des fichiers generated
→ pas d’accès aux tables privées
→ pas de namespace Internal
→ pas de plugin runtime
→ pas de réduction des protections
→ pas de généralisation automatique

Upgrade
→ vérifier les contrats
→ compiler les customizations
→ exécuter les tests client
→ adapter explicitement
→ valider en staging
```

> SiteFactory doit rester générique sans empêcher les projets clients d’être réellement adaptés. La personnalisation est donc autorisée comme une extension compilée et isolée du projet client, jamais comme une exception cachée dans le socle.
