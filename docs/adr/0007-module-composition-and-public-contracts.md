---
adrId: ADR-0007
title: Compose modules statically through explicit dependencies and public contracts
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
  - ../02-architecture/module-catalog.md
  - ../02-architecture/core-module-boundary.md
  - ../03-generation/13-generation-pipeline.md
  - ../02-architecture/09-module-architecture.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ../03-generation/generated-runtime-database-model.md
  - ../04-quality-security/18-security-baseline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0003-postgresql-as-primary-database.md
  - ./0004-explicit-production-migrations.md
  - ./0005-file-generation-strategies.md
  - ./0006-feature-flags-and-entitlements-separation.md
targetVersion: 0.1.0
---

# ADR-0007 — Composition des modules et contrats publics

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le [ModuleSdk public](../02-architecture/module-sdk.md) expose actuellement
`ISiteFactoryModule`, `ModuleDescriptor`, `ModuleId`, `ModuleVersion` et
`ModuleCapability`. Le catalogue et les contributions de génération sont
explicites ; DocumentationModule est enregistré par le composition root.
Les manifests complets, services métier et contrats d'une application cliente
ci-dessous restent une architecture cible, pas l'API actuelle du SDK minimal.
[ADR-0033](0033-split-public-sitefactory-and-private-logicgate-modules.md) et
[ADR-0037](0037-compose-modules-explicitly-without-dynamic-loading.md) maintiennent
la dépendance privé → public et la composition statique : aucun scan de DLL,
chargement dynamique, téléchargement/exécution ou fallback vers Business.
Les exemples d'offres sont fictifs ; aucun module métier privé n'est requis.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory adopte une architecture de monolithe modulaire composé statiquement lors de la génération du projet client.

Chaque module doit :

- posséder une identité stable ;
- déclarer sa version ;
- déclarer ses dépendances ;
- déclarer ses capacités ;
- exposer uniquement des contrats publics explicites ;
- conserver ses implémentations internes ;
- respecter la propriété des données des autres modules ;
- être assemblé dans une composition déterministe.

Le MVP n’utilise pas :

- de chargement dynamique de plugins ;
- de découverte runtime par réflexion ;
- de téléchargement de modules au démarrage ;
- de microservice par module ;
- de dépendances implicites entre modules ;
- de références directes aux types internes d’un autre module.

---

## 2. Résumé

Les projets clients générés constituent des monolithes modulaires.

Le modèle de référence est :

```text
Configuration client
→ catalogue
→ résolution des modules
→ validation des dépendances
→ composition statique
→ génération du composition root
→ build
→ application cliente autonome
```

Chaque module est constitué conceptuellement de :

```text
Manifest
Contrats publics
Implémentation interne
Contributions de composition
Tests
Documentation
Version
```

Les interactions entre modules passent uniquement par :

- contrats publics ;
- interfaces applicatives publiques ;
- messages ou événements d’intégration versionnés ;
- identifiants stables ;
- capacités déclarées ;
- modules d’intégration explicitement composés.

Les modules ne doivent pas :

- accéder directement au `DbContext` d’un autre module ;
- requêter directement ses tables ;
- référencer ses entités internes ;
- modifier ses fichiers ;
- réutiliser ses services internes ;
- dépendre d’une convention non déclarée.

---

## 3. Contexte

SiteFactory doit permettre d’assembler des projets clients à partir de capacités réutilisables telles que :

- administration ;
- Identity ;
- gestion de contenus ;
- formulaires ;
- leads ;
- médias ;
- réalisations ;
- SEO ;
- automatisations ;
- observabilité ;
- opérations.

Ces capacités doivent pouvoir :

- être ajoutées à plusieurs projets ;
- évoluer indépendamment ;
- être versionnées ;
- déclarer leurs dépendances ;
- exposer des permissions ;
- apporter des routes ;
- apporter des données ;
- contribuer à la génération ;
- être retirées ou remplacées de manière contrôlée.

Les projets clients doivent néanmoins rester :

- des applications autonomes ;
- simples à déployer ;
- simples à diagnostiquer ;
- transférables ;
- adaptées à une petite équipe ;
- compatibles avec un repository et une base par client.

Une architecture sans frontières modulaires conduirait progressivement à :

- des dépendances circulaires ;
- des entités partagées partout ;
- des requêtes directes entre domaines ;
- des migrations difficiles à attribuer ;
- une impossibilité de faire évoluer un module indépendamment ;
- une forte régression lors des upgrades ;
- du code spécifique client dans le socle générique.

Une architecture de plugins runtime ou de microservices augmenterait en revanche fortement la complexité du MVP.

---

## 4. Problème à résoudre

Comment assembler des capacités réutilisables dans une application cliente unique tout en garantissant :

- des frontières explicites ;
- des dépendances déclarées ;
- une composition déterministe ;
- des contrats stables ;
- une propriété claire des données ;
- des upgrades maîtrisables ;
- une testabilité indépendante ;
- une application finale simple à exploiter ?

La solution doit éviter :

- le couplage direct entre implémentations ;
- la découverte runtime implicite ;
- les dépendances circulaires ;
- le partage généralisé d’entités ;
- les références vers les tables d’un autre module ;
- les plugins exécutables non maîtrisés ;
- l’explosion opérationnelle de microservices ;
- un projet client rempli de conditions propres à chaque client.

---

## 5. Portée

Cette décision couvre :

- les modules génériques SiteFactory ;
- leurs manifests ;
- leurs identifiants ;
- leurs versions ;
- leurs dépendances ;
- leurs contrats publics ;
- leurs services internes ;
- les interactions entre modules ;
- la composition de l’application cliente ;
- l’enregistrement dans l’injection de dépendances ;
- les routes ;
- les capacités ;
- les permissions ;
- les événements ;
- la propriété logique des données ;
- les tests d’architecture ;
- le versionnement ;
- les upgrades ;
- le retrait d’un module.

---

## 6. Hors portée

Cette décision ne définit pas :

- le contenu fonctionnel détaillé de chaque module ;
- l’ensemble des modules du catalogue ;
- le découpage physique exact de tous les packages ;
- un bus distribué ;
- une plateforme de plugins tierce ;
- une marketplace ;
- un chargement à chaud ;
- un système de microservices ;
- une base distincte par module ;
- un protocole d’intégration inter-entreprises ;
- la composition de projets sur mesure soumis à une autre architecture.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Frontières fonctionnelles explicites**
2. **Dépendances déclarées et vérifiables**
3. **Application cliente autonome**
4. **Simplicité d’exploitation**
5. **Contrats publics stables**
6. **Protection des implémentations internes**
7. **Propriété claire des données**
8. **Composition déterministe**
9. **Testabilité**
10. **Capacité d’upgrade**
11. **Versionnement indépendant**
12. **Réduction des dépendances circulaires**
13. **Compatibilité avec la génération**
14. **Compatibilité avec les personnalisations client**
15. **Sécurité de la supply chain**
16. **Absence de complexité distribuée prématurée**

---

## 8. Contraintes

La décision doit respecter :

```text
.NET 10

Application cliente monolithique

Un repository par client

Une base PostgreSQL par client et environnement

Modules versionnés

Packages et templates maîtrisés

Composition déterministe

Migrations dans le repository client

Aucun chargement dynamique non approuvé

Aucun secret dans les manifests

Compatibilité Windows et Linux

Build et tests du projet généré

Contrats publics versionnés
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les projets PME ciblés peuvent être exploités comme un monolithe ;
- les modules s’exécutent dans le même processus ;
- la plupart des interactions peuvent être synchrones ou in-process ;
- un broker de messages n’est pas nécessaire dans le MVP ;
- la composition peut être déterminée avant compilation ;
- les modules génériques sont contrôlés ou approuvés par LogicGate ;
- les contrats publics resteront significativement plus petits que les implémentations ;
- les modules peuvent partager une même base physique tout en conservant une propriété logique des données ;
- un besoin distribué futur pourra être traité par un nouvel ADR.

---

# 10. Option A — Monolithe sans frontières modulaires

## 10.1 Description

Tous les composants appartiennent à une application commune.

Les projets et services peuvent se référencer directement selon leurs besoins.

---

## 10.2 Avantages

- simplicité initiale ;
- développement rapide ;
- peu de manifests ;
- peu de contrats formels ;
- accès direct aux services et données ;
- faible quantité de code d’infrastructure.

---

## 10.3 Inconvénients

- couplage croissant ;
- dépendances difficiles à identifier ;
- cycles ;
- règles métier dispersées ;
- entités partagées ;
- changements à fort impact ;
- extraction future difficile ;
- réutilisation client incertaine ;
- tests d’isolation difficiles.

---

## 10.4 Risques

- module impossible à retirer ;
- modification locale cassant plusieurs domaines ;
- migration sans propriétaire ;
- accès direct aux données d’un autre domaine ;
- duplication de règles ;
- dette technique rapide.

---

## 10.5 Réversibilité

```text
faible
```

La modularisation a posteriori d’un monolithe fortement couplé est coûteuse.

---

# 11. Option B — Plugins chargés dynamiquement au runtime

## 11.1 Description

L’application découvre ou charge des assemblies de modules au démarrage.

Les modules peuvent être ajoutés sans recompilation de l’application principale.

---

## 11.2 Avantages

- extensibilité runtime ;
- ajout de plugins ;
- séparation physique ;
- possibilité de distribution indépendante ;
- découverte automatique.

---

## 11.3 Inconvénients

- chargement d’assemblies complexe ;
- sécurité accrue de la supply chain ;
- incompatibilités runtime ;
- diagnostics plus difficiles ;
- versionnement complexe ;
- déploiements moins prévisibles ;
- réflexion ;
- manque de garanties à la compilation ;
- migrations plus difficiles à composer.

---

## 11.4 Risques

- plugin non approuvé ;
- assembly falsifiée ;
- type incompatible ;
- ordre de chargement ;
- dépendance manquante ;
- collision ;
- exécution arbitraire ;
- différences entre environnements.

---

## 11.5 Réversibilité

```text
faible à moyenne
```

Les contrats de plugins deviennent rapidement structurants.

---

# 12. Option C — Microservice par module

## 12.1 Description

Chaque module est déployé dans un service indépendant avec sa propre API et potentiellement sa propre base.

---

## 12.2 Avantages

- isolation de déploiement ;
- scalabilité indépendante ;
- frontières techniques fortes ;
- équipes indépendantes ;
- bases séparées ;
- évolutions autonomes.

---

## 12.3 Inconvénients

- réseau ;
- authentification interservices ;
- résilience distribuée ;
- observabilité distribuée ;
- transactions distribuées ;
- coûts d’infrastructure ;
- nombreux déploiements ;
- forte charge opérationnelle ;
- complexité disproportionnée pour les projets PME.

---

## 12.4 Risques

- timeouts ;
- indisponibilités en chaîne ;
- duplication de données ;
- événements perdus ;
- configuration complexe ;
- coûts d’exploitation supérieurs à la valeur métier.

---

## 12.5 Réversibilité

```text
faible
```

Le retour vers un monolithe nécessiterait une recomposition importante.

---

# 13. Option D — Monolithe modulaire composé statiquement

## 13.1 Description

Les modules restent dans le même processus applicatif.

Ils possèdent des contrats et frontières explicites.

La sélection des modules est résolue pendant la génération et inscrite dans le projet client.

---

## 13.2 Avantages

- simplicité de déploiement ;
- garanties de compilation ;
- composition déterministe ;
- contrats explicites ;
- tests d’architecture ;
- absence de réseau interne ;
- transactions locales possibles ;
- application transférable ;
- upgrades contrôlés ;
- extraction future facilitée.

---

## 13.3 Inconvénients

- discipline architecturale nécessaire ;
- frontières logiques et non physiques ;
- tests d’architecture obligatoires ;
- composition à maintenir ;
- risque de contournement par référence directe ;
- processus unique ;
- scalabilité commune.

---

## 13.4 Risques

- shared kernel devenant trop large ;
- contrats exposant trop de détails ;
- module accédant directement aux tables d’un autre ;
- cycles ;
- événements utilisés sans garantie claire ;
- implémentations internes rendues publiques par commodité.

---

## 13.5 Réversibilité

```text
élevée
```

Les frontières explicites permettent une extraction ultérieure si un besoin réel apparaît.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Monolithe libre | Plugins runtime | Microservices | Monolithe modulaire |
|---|---:|---:|---:|---:|---:|
| Simplicité d’exploitation | 5 | 5 | 3 | 1 | 5 |
| Frontières explicites | 5 | 1 | 4 | 5 | 5 |
| Garanties de compilation | 5 | 5 | 2 | 3 | 5 |
| Sécurité supply chain | 5 | 4 | 2 | 3 | 5 |
| Composition déterministe | 5 | 3 | 2 | 3 | 5 |
| Coût d’infrastructure | 4 | 5 | 4 | 1 | 5 |
| Capacité d’upgrade | 5 | 2 | 3 | 4 | 5 |
| Testabilité | 4 | 3 | 3 | 3 | 5 |
| Transfert client | 5 | 4 | 3 | 2 | 5 |
| Réversibilité | 4 | 2 | 2 | 2 | 5 |
| Adaptation aux PME | 5 | 4 | 2 | 1 | 5 |

Le monolithe modulaire composé statiquement correspond le mieux aux contraintes SiteFactory.

---

# 15. Décision

Nous décidons que les applications clientes SiteFactory seront des monolithes modulaires composés statiquement.

La composition des modules est déterminée :

```text
avant compilation
```

à partir de :

- la configuration client ;
- du catalogue ;
- des manifests ;
- des versions ;
- des dépendances ;
- des entitlements.

Le générateur produit explicitement :

- les références de packages ou projets ;
- les enregistrements DI ;
- les routes ;
- les contributions UI ;
- les configurations ;
- les métadonnées ;
- le composition root.

---

## 15.1 Absence de découverte runtime

Le runtime ne doit pas rechercher automatiquement les modules par :

- scan d’assemblies ;
- réflexion globale ;
- dossier de plugins ;
- téléchargement ;
- convention de nommage ;
- attribut non référencé explicitement.

---

## 15.2 Absence de plugin tiers

Le MVP n’autorise pas l’installation de code arbitraire fourni directement par un client ou un tiers dans le processus de production.

---

## 15.3 Application finale

Après génération, le projet client possède une composition connue et compilée.

---

# 16. Module ID

Chaque module possède un identifiant stable en kebab-case.

Exemples :

```text
sample-records

sample-pages

sample-media

portfolio-advanced

seo-local-enhanced
```

---

## 16.1 Unicité

Deux modules ne peuvent pas partager le même identifiant.

---

## 16.2 Stabilité

Un identifiant publié ne doit pas être renommé silencieusement.

---

## 16.3 Nom marketing

Le nom commercial ou d’affichage peut évoluer sans changer le Module ID.

---

# 17. Version du module

Chaque module possède une version SemVer.

Exemple :

```text
sample-records 1.4.0
```

---

## 17.1 Version du manifest

Le manifest possède également :

```text
schemaVersion
```

distincte de la version fonctionnelle du module.

---

## 17.2 Version exacte

Le projet client enregistre la version exacte du module dans :

```text
sitefactory.lock.json
```

---

# 18. Statut du module

Valeurs recommandées :

```text
experimental

preview

stable

deprecated

retired
```

---

## 18.1 `experimental`

Le contrat peut évoluer rapidement.

Il n’est pas destiné à une production standard.

---

## 18.2 `preview`

Le module peut être utilisé dans un projet pilote après validation explicite.

---

## 18.3 `stable`

Le module suit la politique de compatibilité et de support.

---

## 18.4 `deprecated`

Le module reste utilisable pour les projets existants mais n’est plus recommandé.

---

## 18.5 `retired`

Le module n’est plus proposé et nécessite un plan de remplacement pour les projets concernés.

---

# 19. Manifest de module

Chaque module possède un manifest machine-readable.

Exemple conceptuel :

```yaml
schemaVersion: 1

module:
  id: sample-records
  version: "1.4.0"
  status: stable

compatibility:
  platform: ">=1.2.0 <2.0.0"
  generator: ">=0.6.0 <0.7.0"

dependencies:
  requires:
    - id: sample-access
      version: ">=1.1.0 <2.0.0"

capabilities:
  - id: sample-records.read
  - id: sample-records.manage
  - id: sample-records.export

permissions:
  - Leads.Read
  - Leads.Manage
  - Leads.Export

composition:
  registrationContract: sample-records-v1
```

La structure exacte appartient au schéma officiel.

---

# 20. Contenu obligatoire du manifest

Le manifest doit pouvoir déclarer :

- identifiant ;
- version ;
- statut ;
- compatibilité ;
- dépendances ;
- capacités ;
- entitlements ;
- feature flags ;
- kill switches ;
- permissions ;
- routes ;
- données ;
- contributions de génération ;
- contrats publics ;
- opérations ;
- migrations attendues ;
- tests ou métadonnées de validation.

---

# 21. Validation du manifest

Le manifest est validé avant toute composition.

Il doit refuser notamment :

- identifiant invalide ;
- version invalide ;
- dépendance inconnue ;
- plage invalide ;
- capability inconnue ;
- permission dupliquée ;
- conflit de route ;
- contrat inexistant ;
- fichier sans stratégie ;
- cycle de dépendance.

---

# 22. Dépendances obligatoires

Une dépendance obligatoire indique que le module ne peut pas fonctionner sans un autre module.

Exemple :

```text
sample-records
→ requires
→ sample-access
```

---

## 22.1 Résolution transitive

Le générateur résout les dépendances transitives.

---

## 22.2 Version

Chaque dépendance déclare une plage de versions compatible.

---

## 22.3 Absence

Une dépendance obligatoire absente produit une erreur avant toute génération.

---

## 22.4 Ajout automatique

Le configurateur peut proposer d’ajouter automatiquement une dépendance requise.

La décision doit rester visible dans le plan.

---

# 23. Dépendances optionnelles

Le MVP ne supporte pas de référence directe optionnelle ambiguë entre modules.

Une intégration facultative utilise l’une des stratégies suivantes :

- contrat public neutre ;
- événement d’intégration ;
- capability détectable ;
- module d’intégration dédié.

---

## 23.1 Module d’intégration

Exemple :

```text
lead-email-notifications
```

peut dépendre explicitement de :

```text
sample-records
email-notifications
```

---

## 23.2 Avantage

Cette approche évite que le module principal contienne de nombreuses conditions telles que :

```csharp
if (emailModuleIsInstalled)
{
    ...
}
```

---

# 24. Dépendances circulaires

Les cycles sont interdits.

Exemple refusé :

```text
Module A
→ Module B
→ Module C
→ Module A
```

---

## 24.1 Détection

La détection s’effectue pendant la résolution du graphe.

---

## 24.2 Correction

Un cycle doit être corrigé par :

- extraction d’un contrat ;
- inversion d’une dépendance ;
- événement ;
- orchestration supérieure ;
- module d’intégration ;
- redéfinition des responsabilités.

---

## 24.3 Pas de contournement

Un service locator ou une résolution dynamique ne doit pas être utilisé pour masquer le cycle.

---

# 25. Ordre de composition

Le générateur produit un ordre topologique stable.

---

## 25.1 Déterminisme

À configuration et versions identiques, l’ordre reste identique.

---

## 25.2 Ordre sans signification métier

Les modules ne doivent pas dépendre de leur position arbitraire dans la configuration YAML.

---

## 25.3 Initialisation ordonnée

Lorsqu’un ordre d’initialisation est réellement nécessaire, il doit être représenté par une dépendance ou un contrat explicite.

---

# 26. Composition root

L’application générée possède un composition root explicite.

Il enregistre notamment :

- services ;
- options ;
- endpoints ;
- routes ;
- navigation ;
- health checks ;
- background jobs ;
- policies ;
- contributions EF Core ;
- observabilité.

---

## 26.1 Génération

Le composition root est un fichier :

```text
generated
```

au sens de l’ADR-0005.

---

## 26.2 Modification manuelle

Une modification manuelle produit un conflit lors d’un upgrade.

---

## 26.3 Extensions client

Les extensions client sont enregistrées dans un point d’extension séparé appartenant à :

```text
ClientName.Customizations
```

---

# 27. Enregistrement des modules

Chaque module expose un point d’enregistrement public minimal.

Exemple conceptuel :

```csharp
public interface ISiteFactoryModule
{
    ModuleDescriptor Descriptor { get; }

    void RegisterServices(
        IServiceCollection services,
        ModuleRegistrationContext context);
}
```

La signature définitive peut évoluer.

---

## 27.1 Pas de réflexion

Le générateur connaît explicitement les implémentations à appeler.

---

## 27.2 Pas de logique métier dans l’enregistrement

L’enregistrement configure les services.

Il ne doit pas :

- modifier la base ;
- effectuer un appel réseau ;
- créer des fichiers ;
- envoyer un email ;
- lancer un job ;
- résoudre des secrets.

---

# 28. Contrats publics

Un contrat public est un type qu’un autre module est autorisé à utiliser.

Exemples :

- interface applicative ;
- commande publique ;
- requête publique ;
- DTO immuable ;
- identifiant ;
- événement d’intégration ;
- capability ID ;
- permission ID ;
- résultat public ;
- descripteur de module.

---

## 28.1 Explicite

Un type n’est pas public uniquement parce que sa classe C# utilise le mot-clé :

```csharp
public
```

Il doit appartenir à un namespace, assembly ou package de contrats déclaré.

---

## 28.2 Minimum nécessaire

Les contrats doivent rester aussi petits que possible.

---

## 28.3 Stabilité

Les contrats publics suivent SemVer.

---

# 29. Organisation des contrats

Convention cible :

```text
LogicGate.SiteFactory.Modules.{Module}.Contracts
```

Exemple :

```text
LogicGate.SiteFactory.Modules.SampleRecords.Contracts
```

---

## 29.1 Projet ou assembly séparé

Lorsqu’un contrat est consommé par un autre module, il doit être isolé dans un projet ou package de contrats distinct.

---

## 29.2 Contrat uniquement interne

Un type utilisé uniquement dans le module reste dans l’implémentation.

Il n’est pas déplacé vers `Contracts` par anticipation.

---

# 30. Éléments interdits dans les contrats publics

Les contrats publics ne doivent pas exposer directement :

- entités EF Core ;
- `DbContext` ;
- `IQueryable` ;
- repositories internes ;
- types de provider PostgreSQL ;
- classes Razor internes ;
- services d’infrastructure ;
- options contenant des secrets ;
- exceptions techniques internes ;
- chemins filesystem privés ;
- classes de migration ;
- types propres au CLI.

---

## 30.1 `IQueryable`

Un contrat ne retourne pas :

```csharp
IQueryable<T>
```

car cela expose la persistance et permet à l’appelant de construire des requêtes non maîtrisées.

---

## 30.2 Entité persistante

Une entité interne n’est pas utilisée comme DTO public.

---

# 31. DTO publics

Les DTO publics doivent être :

- minimaux ;
- immuables lorsque possible ;
- versionnables ;
- sans comportement métier interne ;
- sans propriété technique inutile ;
- sans donnée sensible excessive.

---

## 31.1 Pagination

Les requêtes de liste publiques sont bornées et paginées lorsque le volume peut croître.

---

## 31.2 Identifiants

Les DTO utilisent des identifiants publics stables plutôt que des références d’entités.

---

# 32. Interfaces applicatives publiques

Un module peut exposer des opérations telles que :

```csharp
public interface ILeadLookup
{
    Task<LeadSummary?> FindAsync(
        LeadId leadId,
        CancellationToken cancellationToken);
}
```

---

## 32.1 Cas d’usage

L’interface expose une intention métier, pas un accès générique aux données.

---

## 32.2 CancellationToken

Les opérations asynchrones propagent un `CancellationToken`.

---

## 32.3 Erreurs

Les résultats attendus utilisent des erreurs structurées ou des résultats publics adaptés.

---

# 33. Appels synchrones entre modules

Les appels synchrones sont autorisés lorsque :

- la dépendance est déclarée ;
- le contrat public est stable ;
- l’appel reste local ;
- la transaction est maîtrisée ;
- le risque de cycle est absent.

---

## 33.1 Limite

Une chaîne profonde d’appels synchrones entre modules doit être évitée.

---

## 33.2 Orchestration

Une opération impliquant plusieurs modules peut être coordonnée par :

- Application ;
- un module d’intégration ;
- une orchestration cliente explicite.

---

# 34. Événements internes et événements d’intégration

## 34.1 Événement de domaine

Un événement de domaine reste interne au module.

Il peut utiliser les entités et concepts privés du module.

---

## 34.2 Événement d’intégration

Un événement d’intégration est un contrat public consommable par d’autres modules.

Exemple :

```text
LeadCreatedIntegrationEvent
```

---

## 34.3 Immutabilité

Les événements d’intégration sont immuables.

---

## 34.4 Version

Un changement incompatible nécessite une nouvelle version ou un nouveau contrat.

---

## 34.5 Données

Un événement ne doit contenir que les données nécessaires.

Il ne doit pas transporter un agrégat complet.

---

# 35. Distribution des événements

Le MVP peut distribuer les événements d’intégration dans le même processus.

---

## 35.1 Pas de promesse implicite

Une distribution in-process ne garantit pas automatiquement :

- durabilité ;
- reprise après crash ;
- exactly-once ;
- ordre global ;
- livraison distribuée.

---

## 35.2 Besoin de durabilité

Un besoin de livraison durable nécessitera :

- outbox ;
- persistance ;
- retry ;
- idempotence ;
- runbook ;
- éventuellement un nouvel ADR.

---

# 36. Gestion des handlers

Un module consommateur enregistre explicitement ses handlers.

---

## 36.1 Échec

La politique d’échec doit être définie :

- bloquer la transaction ;
- journaliser ;
- mettre en file ;
- retry ;
- ignorer explicitement.

---

## 36.2 Pas de swallow

Une exception d’un handler ne doit pas être ignorée silencieusement.

---

# 37. Shared kernel

## 37.1 Définition

Un shared kernel minimal peut contenir uniquement des primitives réellement transversales et stables.

Exemples possibles :

- abstractions de résultat ;
- identifiants techniques de base ;
- horloge applicative ;
- primitives d’erreur ;
- abstractions d’événements.

---

## 37.2 Interdictions

Le shared kernel ne doit pas contenir :

- entités métier ;
- concepts propres aux leads ;
- concepts propres aux contenus ;
- repositories génériques métier ;
- services clients ;
- règles commerciales ;
- dépendances Infrastructure.

---

## 37.3 Nouvelle primitive

Toute nouvelle primitive partagée nécessite la preuve qu’elle est :

- transversale ;
- stable ;
- indépendante d’un module ;
- utile à plusieurs composants.

---

# 38. Propriété des données

Chaque table ou ensemble de données possède un module propriétaire logique.

---

## 38.1 Propriétaire

Le module propriétaire est responsable :

- du modèle ;
- des migrations ;
- des règles ;
- des écritures ;
- des contraintes ;
- de la rétention ;
- de la suppression ;
- des contrats d’accès.

---

## 38.2 Lecture par un autre module

Un autre module lit les données via :

- contrat public ;
- projection publique ;
- événement ;
- copie locale explicitement gérée.

---

## 38.3 Écriture par un autre module

Un autre module ne modifie jamais directement la table propriétaire.

Il utilise un cas d’usage public.

---

# 39. Accès direct aux tables d’un autre module

L’accès direct est interdit.

Exemples interdits :

```text
DbContext d’un autre module

DbSet d’un autre module

SQL ciblant une table privée

JOIN direct sur une table d’un autre module

Repository interne

Navigation EF vers une entité privée
```

---

## 39.1 Raisons

Ces accès :

- contournent les invariants ;
- créent du couplage ;
- empêchent l’évolution du schéma ;
- rendent les dépendances invisibles ;
- compliquent les tests ;
- compliquent l’extraction future.

---

# 40. Références entre modules

Lorsqu’un module doit référencer une ressource d’un autre module, il conserve :

- un identifiant public ;
- éventuellement un snapshot minimal ;
- aucune navigation EF vers l’entité privée.

---

## 40.1 Validation

La validité de la référence est vérifiée par un contrat public ou un événement.

---

## 40.2 Suppression

Le comportement lors de la suppression de la ressource externe doit être explicitement défini :

- interdire ;
- détacher ;
- conserver l’historique ;
- anonymiser ;
- réagir à un événement.

---

# 41. Clés étrangères inter-modules

Les clés étrangères physiques entre tables appartenant à des modules différents sont interdites par défaut dans le MVP.

---

## 41.1 Raisons

Elles créent :

- couplage de migration ;
- dépendance de suppression ;
- ordre de déploiement ;
- dépendance au schéma interne ;
- difficulté de retrait d’un module.

---

## 41.2 Exception

Une exception nécessite :

- dépendance déclarée ;
- justification ;
- analyse de suppression ;
- tests ;
- documentation ;
- décision d’architecture explicite.

---

# 42. Transactions inter-modules

## 42.1 Baseline

Un cas d’usage peut coordonner plusieurs contrats publics dans le même processus.

---

## 42.2 Atomicité

Une transaction commune ne doit pas nécessiter l’accès direct aux contextes privés d’autres modules.

---

## 42.3 Besoin complexe

Si une atomicité inter-module forte devient fréquente, les frontières doivent être réévaluées.

---

## 42.4 Événements

Les effets secondaires non essentiels peuvent être traités après la transaction principale.

---

# 43. DbContext et composition

La structure physique exacte du ou des `DbContext` pourra évoluer.

La règle obligatoire est :

```text
la propriété logique des données reste au module
```

---

## 43.1 DbContext composé

Un projet client peut utiliser un `DbContext` composé à partir des contributions des modules.

---

## 43.2 DbContext par module

Une future architecture peut utiliser plusieurs `DbContext` dans la même base.

---

## 43.3 Invariant

Dans les deux cas :

- un module ne consomme pas directement les entités privées d’un autre ;
- les migrations finales appartiennent au repository client ;
- les dépendances restent déclarées.

---

# 44. Migrations

Chaque module peut fournir des métadonnées ou contributions nécessaires à son modèle persistant.

La migration EF Core finale est générée dans le repository client après composition.

---

## 44.1 Module versionné

Une nouvelle version de module peut nécessiter une migration.

---

## 44.2 Documentation

Le changelog du module doit indiquer :

- migration requise ;
- données affectées ;
- compatibilité ;
- action manuelle ;
- rollback.

---

## 44.3 Production

Les migrations suivent l’ADR-0004.

---

# 45. Routes

Chaque module déclare les routes qu’il contribue.

---

## 45.1 Collision

Deux modules ne peuvent pas déclarer la même combinaison :

```text
HTTP method
+ route
```

sans mécanisme explicite de composition.

---

## 45.2 Préfixes

Les routes administratives peuvent utiliser un préfixe de module stable.

---

## 45.3 Route publique

Une route publique reste soumise :

- au module ;
- à l’entitlement ;
- au feature flag ;
- au kill switch ;
- aux protections applicables.

---

# 46. Interface utilisateur

Un module peut contribuer :

- pages ;
- composants ;
- navigation ;
- formulaires ;
- sections ;
- actions administratives.

---

## 46.1 Contribution déclarée

Les contributions UI doivent être déclarées ou enregistrées explicitement.

---

## 46.2 Modification d’un autre module

Un module ne modifie pas directement :

- les fichiers Razor ;
- les styles privés ;
- la navigation interne ;
- les composants ;

d’un autre module.

---

## 46.3 Points d’extension

Les extensions utilisent des contrats prévus :

- slots ;
- descriptors ;
- navigation contributions ;
- composants publics ;
- modules d’intégration.

---

# 47. Navigation

Une entrée de navigation doit déclarer :

- identifiant ;
- label ;
- route ;
- ordre ;
- capability ;
- permission ;
- section.

---

## 47.1 Ordre

L’ordre est déterministe.

---

## 47.2 Doublon

Deux entrées ne peuvent pas utiliser le même identifiant.

---

# 48. Permissions et capabilities

Conformément à l’ADR-0006, le module déclare :

- capabilities ;
- entitlements éventuels ;
- feature flags éventuels ;
- kill switches éventuels ;
- permissions.

---

## 48.1 Absence de module

Un module absent ne doit pas laisser de permission active orpheline dans une nouvelle génération.

---

## 48.2 Retrait

Les permissions existantes doivent être gérées par une migration ou une procédure de nettoyage explicite.

---

# 49. Options de module

Chaque module possède des options typées et validées.

---

## 49.1 Namespace de configuration

Convention conceptuelle :

```text
SiteFactory:Modules:{ModuleId}
```

---

## 49.2 Secrets

Les options publiques ne doivent pas contenir de secrets dans :

- le manifest ;
- le lock file ;
- les rapports.

Les références de secrets restent séparées.

---

## 49.3 Valeurs inconnues

Une propriété inconnue est refusée selon le contrat du schéma.

---

# 50. Contributions de fichiers

Les modules déclarent leurs fichiers conformément à l’ADR-0005.

Ils ne peuvent pas :

- écrire dans une zone `custom` ;
- écrire dans une zone `protected` ;
- acquérir un fichier appartenant à un autre module ;
- utiliser un chemin sortant ;
- modifier implicitement un fichier non déclaré.

---

# 51. Composition des dépendances

Le résolveur produit un graphe comprenant :

```text
ModuleId
Version
Dependencies
Capabilities
Compatibility
Source
```

---

## 51.1 Résolution exacte

Les versions résolues sont conservées dans le lock file.

---

## 51.2 Pas de `latest`

Les dépendances flottantes sont interdites.

---

## 51.3 Résolution conflictuelle

Si deux modules exigent des versions incompatibles d’une même dépendance :

```text
composition failure
```

---

# 52. Ordre de priorité des versions

La résolution ne choisit pas silencieusement :

- la version la plus récente ;
- la première version trouvée ;
- une version preview ;
- une version déjà en cache.

Elle suit une politique explicite.

---

## 52.1 Preview

Une version preview nécessite une autorisation explicite.

---

## 52.2 Stable

Une composition stable ne sélectionne pas une preview automatiquement.

---

# 53. Lock file

Le lock file doit conserver :

- module ID ;
- version exacte ;
- statut ;
- dépendances résolues ;
- contrats ;
- capabilities ;
- entitlements ;
- source de package ;
- hashes lorsque pertinents.

---

## 53.1 Exemple conceptuel

```json
{
  "modules": [
    {
      "id": "sample-records",
      "version": "1.4.0",
      "dependencies": [
        {
          "id": "sample-access",
          "version": "1.2.0"
        }
      ],
      "publicContractsVersion": "1.1.0"
    }
  ]
}
```

---

# 54. Versionnement des contrats publics

Les contrats publics suivent SemVer.

---

## 54.1 Patch

- correction documentaire ;
- correction interne sans changement visible ;
- attribut non fonctionnel compatible.

---

## 54.2 Minor

- nouvelle méthode compatible ;
- nouveau champ facultatif ;
- nouveau contrat ;
- nouvel événement versionné.

---

## 54.3 Major

- suppression ;
- renommage ;
- changement de type ;
- changement de sens ;
- nouveau champ obligatoire ;
- changement incompatible de résultat.

---

# 55. Compatibilité source et binaire

La politique initiale privilégie la compatibilité source des projets recompilés.

La compatibilité binaire reste souhaitable pour les packages, mais elle ne doit pas masquer une incompatibilité sémantique.

---

## 55.1 Projet client

Les projets clients sont reconstruits pendant un upgrade.

---

## 55.2 Contrat sémantique

Une méthode conservant sa signature mais changeant son sens peut nécessiter une version majeure.

---

# 56. Dépréciation des contrats

Un contrat public ne doit pas être supprimé silencieusement.

---

## 56.1 Dépréciation

Elle précise :

- version d’introduction ;
- remplacement ;
- version de retrait ;
- guide de migration.

---

## 56.2 Avertissement

Les projets consommateurs peuvent recevoir un warning de compilation contrôlé.

---

# 57. Évolution des événements

Un événement d’intégration incompatible doit utiliser :

- un nouvel Event ID ;
- un nouveau type ;
- ou une nouvelle version explicite.

---

## 57.1 Double publication

Une période de transition peut publier l’ancienne et la nouvelle version si nécessaire.

---

## 57.2 Consommateurs

Les consommateurs sont migrés avant le retrait de l’ancien contrat.

---

# 58. Personnalisations client

Les personnalisations consomment les mêmes contrats publics que les autres modules.

---

## 58.1 Interdiction

Une customization ne doit pas référencer :

- types internes ;
- DbContext privé ;
- entités privées ;
- classes de migration ;
- services Infrastructure internes.

---

## 58.2 Upgrade

Une customization cassée par un changement de contrat doit être détectée au build.

---

## 58.3 Contrat client dédié

Un contrat exclusivement nécessaire à un client reste dans le repository client.

Il ne devient pas automatiquement un contrat public SiteFactory.

---

# 59. Modules d’intégration

Un module d’intégration relie explicitement plusieurs modules.

Exemples conceptuels :

```text
lead-email-notifications

portfolio-seo-integration

content-media-integration
```

---

## 59.1 Dépendances

Il déclare toutes ses dépendances.

---

## 59.2 Propriété

Il possède sa logique d’orchestration, mais ne devient pas propriétaire des données des modules intégrés.

---

## 59.3 Avantage

Les modules principaux restent indépendants des intégrations facultatives.

---

# 60. Services transversaux

Les services transversaux tels que :

- horloge ;
- journalisation ;
- observabilité ;
- stockage ;
- email ;
- autorisation ;

sont exposés par des abstractions de plateforme stables.

---

## 60.1 Pas de dépendance inverse implicite

La plateforme ne doit pas dépendre d’un module métier.

---

## 60.2 Adaptateurs

Les modules consomment les abstractions.

Infrastructure fournit les adaptateurs.

---

# 61. Sécurité de la supply chain

Les modules sont chargés depuis des sources approuvées.

---

## 61.1 Source

Le manifest doit permettre d’identifier :

- package ;
- version ;
- repository ;
- hash ou provenance lorsque disponible.

---

## 61.2 Feed privé

Les modules LogicGate peuvent être distribués par un feed privé.

---

## 61.3 Dépendance inconnue

Une dépendance provenant d’une source non approuvée est refusée.

---

## 61.4 Scripts post-install

Les modules ne peuvent pas exécuter arbitrairement des scripts pendant leur installation ou génération.

---

# 62. Sécurité des contrats

Les contrats publics doivent éviter :

- données personnelles excessives ;
- secrets ;
- chaînes de connexion ;
- tokens ;
- accès générique au filesystem ;
- exécution de commandes ;
- SQL arbitraire.

---

## 62.1 Principe de minimisation

Un consommateur ne reçoit que les données nécessaires à son cas d’usage.

---

# 63. Observabilité

Événements recommandés :

```text
ModuleResolutionStarted

ModuleResolved

ModuleDependencyResolved

ModuleCompositionCompleted

ModuleCompositionFailed

ModuleContractCompatibilityFailed

ModuleRouteConflictDetected

ModuleCapabilityConflictDetected

ModuleRegistrationCompleted
```

---

## 63.1 Propriétés autorisées

```text
ModuleId

ModuleVersion

DependencyId

DependencyVersion

ContractVersion

Result

ErrorCode
```

---

## 63.2 Données interdites

```text
Secret

Données client

ConnectionString

Token

Contenu métier
```

---

# 64. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-MODULE-001` | Module inconnu |
| `SF-MODULE-002` | Module ID invalide |
| `SF-MODULE-003` | Version de module invalide |
| `SF-MODULE-004` | Manifest invalide |
| `SF-MODULE-005` | Module dupliqué |
| `SF-MODULE-006` | Dépendance obligatoire absente |
| `SF-MODULE-007` | Version de dépendance incompatible |
| `SF-MODULE-008` | Dépendance circulaire |
| `SF-MODULE-009` | Ordre de composition impossible |
| `SF-MODULE-010` | Contrat public absent |
| `SF-MODULE-011` | Version de contrat incompatible |
| `SF-MODULE-012` | Référence à un type interne |
| `SF-MODULE-013` | Accès aux données d’un autre module |
| `SF-MODULE-014` | Collision de route |
| `SF-MODULE-015` | Collision de capability |
| `SF-MODULE-016` | Collision de permission |
| `SF-MODULE-017` | Collision de fichier |
| `SF-MODULE-018` | Source de package non approuvée |
| `SF-MODULE-019` | Module preview non autorisé |
| `SF-MODULE-020` | Module deprecated |
| `SF-MODULE-021` | Module retired |
| `SF-MODULE-022` | Contribution de composition invalide |
| `SF-MODULE-023` | Script de module interdit |
| `SF-MODULE-024` | Propriété de données ambiguë |
| `SF-MODULE-025` | Clé étrangère inter-module non autorisée |
| `SF-MODULE-026` | Contrat exposant une implémentation privée |
| `SF-MODULE-027` | Événement d’intégration incompatible |
| `SF-MODULE-028` | Module d’intégration requis |
| `SF-MODULE-029` | Point d’enregistrement invalide |
| `SF-MODULE-030` | Composition non déterministe |

---

# 65. Tests unitaires des modules

Chaque module doit couvrir :

- règles métier ;
- cas d’usage ;
- erreurs ;
- capacités ;
- permissions ;
- options ;
- contrats ;
- événements ;
- données.

---

# 66. Tests de contrats

Les contrats publics doivent posséder des tests vérifiant :

- sérialisation ;
- valeurs obligatoires ;
- compatibilité ;
- immutabilité ;
- erreurs ;
- limites ;
- absence de types Infrastructure.

---

## 66.1 Contract tests consommateurs

Un module consommateur peut disposer de tests prouvant ses attentes vis-à-vis d’un contrat public.

---

# 67. Tests d’architecture

Les tests doivent vérifier notamment :

```text
Un module ne référence pas l’implémentation d’un autre module.

Contracts ne référence pas Infrastructure.

Domain ne référence pas Contracts d’Infrastructure.

Aucun DbContext privé n’est exposé.

Aucune entité EF n’est exposée.

Aucun cycle de projet ou package n’existe.

Customizations consomme uniquement des contrats publics.
```

---

# 68. Tests du graphe de dépendances

Tester :

- dépendance simple ;
- dépendance transitive ;
- dépendance absente ;
- plage incompatible ;
- cycle direct ;
- cycle indirect ;
- versions multiples ;
- preview ;
- module retiré ;
- ordre déterministe.

---

# 69. Tests de composition

Générer plusieurs compositions de référence :

```text
Site Pro Administrable

Offre applicative illustrative

Offre illustrative automatisée

Projet avec module optionnel

Projet avec module d’intégration
```

---

## 69.1 Vérifications

Pour chaque composition :

- restore ;
- build Release ;
- tests ;
- routes ;
- permissions ;
- migrations ;
- lock file ;
- composition root ;
- absence de collision.

---

# 70. Tests inter-modules

Les intégrations réelles entre deux modules doivent être testées par :

- contrats publics ;
- comportement de l’orchestration ;
- événements ;
- erreurs ;
- absence du module ;
- version incompatible.

---

# 71. Tests de propriété des données

Vérifier que :

- un module ne peut pas écrire directement les données d’un autre ;
- les interfaces publiques protègent les invariants ;
- aucune requête directe vers une table privée n’est introduite ;
- les références externes utilisent des identifiants publics ;
- les suppressions sont gérées selon le contrat.

---

# 72. Tests des événements

Vérifier :

- type public ;
- version ;
- minimisation des données ;
- handler enregistré ;
- comportement en échec ;
- idempotence lorsque nécessaire ;
- absence de secret ;
- absence d’entité interne.

---

# 73. Tests des modules d’intégration

Vérifier :

- deux dépendances présentes ;
- dépendance absente ;
- capability désactivée ;
- événement ;
- autorisation ;
- données conservées ;
- absence de modification directe des tables.

---

# 74. Tests de sécurité

Vérifier :

- package non approuvé ;
- manifest malveillant ;
- chemin sortant ;
- script post-install ;
- assembly runtime inattendue ;
- contrat contenant un secret ;
- type Infrastructure exposé ;
- dépendance non verrouillée ;
- package confusion.

---

# 75. Tests de versionnement

Tester :

- patch compatible ;
- minor compatible ;
- major incompatible ;
- contrat déprécié ;
- version preview ;
- version retirée ;
- upgrade intermédiaire ;
- double version incompatible d’une dépendance.

---

# 76. Quality gates

Une composition est bloquée lorsque :

```text
Manifest invalide

Module inconnu

Version invalide

Dépendance absente

Cycle

Plage incompatible

Contrat public manquant

Version de contrat incompatible

Référence à une implémentation interne

Collision de route

Collision de permission

Collision de capability

Collision de fichier

Source non approuvée

Composition non déterministe
```

---

## 76.1 Release de module

Une release est bloquée si :

```text
Tests du module échoués

Tests de contrats échoués

Tests d’architecture échoués

Changelog absent

Breaking change non versionné

Migration non documentée

Package invalide

Secret détecté
```

---

# 77. Retrait d’un module

Le retrait d’un module suit :

```text
désactivation ou retrait de la configuration
→ plan
→ analyse des dépendants
→ retrait du code
→ retrait des routes
→ retrait des contributions
→ conservation des données par défaut
→ migration de nettoyage séparée si validée
```

---

## 77.1 Module dépendant

Un module ne peut pas être retiré tant qu’un autre module le requiert.

---

## 77.2 Données

Les données ne sont pas supprimées automatiquement.

---

## 77.3 Contrats

Les contrats peuvent rester disponibles pendant une période de transition.

---

# 78. Upgrade d’un module

Le flux est :

```text
upgrade check
→ compatibilité
→ plan
→ branche
→ mise à jour de package
→ composition
→ migration éventuelle
→ build
→ tests
→ staging
→ déploiement
```

---

## 78.1 Upgrade indirect

La mise à niveau d’un module peut imposer la mise à niveau d’une dépendance.

Le plan doit rendre cet impact visible.

---

## 78.2 Breaking change

Un upgrade majeur nécessite :

- notes de migration ;
- adaptations des consommateurs ;
- tests ;
- staging ;
- validation humaine.

---

# 79. Dépréciation d’un module

Un module deprecated doit déclarer :

- version de dépréciation ;
- remplacement ;
- date ou version de retrait ;
- guide de migration ;
- projets affectés.

---

## 79.1 Nouveaux projets

Le module n’est plus proposé par défaut aux nouveaux projets.

---

## 79.2 Projets existants

Il peut recevoir des correctifs selon la politique de support.

---

# 80. Conséquences positives

La décision apporte :

- frontières explicites ;
- dépendances vérifiables ;
- application unique simple à exploiter ;
- contrats publics stables ;
- réduction du couplage ;
- meilleure propriété des données ;
- meilleure testabilité ;
- composition déterministe ;
- capacité d’upgrade ;
- personnalisation plus sûre ;
- extraction future facilitée ;
- réduction de la complexité distribuée ;
- meilleure sécurité de la supply chain.

---

# 81. Conséquences négatives

La décision entraîne :

- manifests détaillés ;
- projets ou packages de contrats ;
- tests d’architecture ;
- discipline de développement ;
- gestion du graphe de dépendances ;
- versionnement des contrats ;
- modules d’intégration supplémentaires ;
- risque de multiplication des DTO ;
- refactoring nécessaire pour éviter les accès directs ;
- composition root généré plus riche.

---

# 82. Risques acceptés

## 82.1 Frontières uniquement logiques

Les modules partagent le même processus et potentiellement la même base physique.

Une discipline et des tests sont nécessaires.

---

## 82.2 Contrats trop larges

Un module peut exposer trop de détails.

Réduction :

- revue ;
- minimisation ;
- tests d’architecture ;
- versionnement.

---

## 82.3 Shared kernel excessif

Le shared kernel peut devenir un monolithe caché.

Réduction :

- primitives limitées ;
- validation humaine ;
- interdiction des concepts métier.

---

## 82.4 Nombre de modules d’intégration

Les interactions facultatives peuvent multiplier les modules d’intégration.

Ce coût est accepté pour préserver l’indépendance des modules principaux.

---

# 83. Mesures compensatoires

- manifests versionnés ;
- graphe validé ;
- composition statique ;
- contrats séparés ;
- tests d’architecture ;
- propriété des données ;
- modules d’intégration ;
- shared kernel minimal ;
- lock file ;
- packages approuvés ;
- quality gates ;
- golden projects ;
- upgrades explicites.

---

# 84. Sécurité

## 84.1 Actifs

- code ;
- données ;
- permissions ;
- secrets ;
- routes ;
- packages ;
- modules ;
- contrats ;
- migrations.

---

## 84.2 Menaces

- module malveillant ;
- dependency confusion ;
- contournement de permission ;
- accès direct aux données ;
- package non approuvé ;
- contrat exposant un secret ;
- route conflictuelle ;
- script arbitraire ;
- version incompatible.

---

## 84.3 Contrôles

- feed approuvé ;
- versions exactes ;
- manifest validé ;
- composition statique ;
- pas de chargement dynamique ;
- pas de script post-install ;
- contrats minimaux ;
- tests de sécurité ;
- provenance et checksums lorsque disponibles.

---

# 85. Performance

## 85.1 Processus unique

Les appels synchrones entre modules n’ajoutent pas de coût réseau.

---

## 85.2 Démarrage

La composition statique réduit le coût de découverte runtime.

---

## 85.3 Nombre de services

Un nombre excessif d’enregistrements ou de handlers doit être surveillé.

---

## 85.4 Mesure

L’optimisation reste basée sur :

- temps de démarrage ;
- mémoire ;
- latence ;
- nombre de modules ;
- profilage.

---

# 86. Coûts

## 86.1 Coûts supplémentaires

- conception des contrats ;
- manifests ;
- tests ;
- versionnement ;
- modules d’intégration ;
- maintenance du résolveur.

---

## 86.2 Coûts évités

- microservices prématurés ;
- plugins non maîtrisés ;
- couplage global ;
- refonte future ;
- incidents cross-module ;
- upgrades imprévisibles ;
- dépendances cachées ;
- exploitation distribuée.

---

# 87. Maintenabilité

Chaque évolution doit permettre de répondre à :

```text
Quel module possède cette règle ?

Quel module possède ces données ?

Quels contrats sont publics ?

Quels modules consomment ces contrats ?

Quelle version est utilisée ?

Quelle dépendance est requise ?
```

---

## 87.1 Nouvelle dépendance

Une nouvelle dépendance doit être déclarée dans le manifest et justifiée.

---

## 87.2 Raccourci interdit

Un développeur ne doit pas ajouter une référence directe vers une implémentation pour éviter de créer un contrat approprié.

---

# 88. Compatibilité

## 88.1 Générateur

Le générateur doit supporter la version de schéma du manifest.

---

## 88.2 Plateforme

Chaque module déclare sa plage de compatibilité avec la plateforme.

---

## 88.3 Contrats

Les dépendances déclarent les versions de contrats attendues.

---

## 88.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration d’un ancien monolithe fortement couplé peut constituer une évolution structurante.

---

# 89. Migration depuis des références directes

Si un prototype possède des références directes entre modules :

1. inventorier les références ;
2. identifier la direction fonctionnelle ;
3. identifier le propriétaire des données ;
4. extraire les contrats nécessaires ;
5. créer un module d’intégration si pertinent ;
6. remplacer les accès directs ;
7. ajouter les tests d’architecture ;
8. mettre à jour les manifests ;
9. vérifier le graphe ;
10. supprimer les références interdites.

---

## 89.1 Accès aux tables

Les requêtes directes doivent être remplacées par :

- query contract ;
- projection ;
- événement ;
- copie locale ;
- orchestration.

---

## 89.2 Production

```text
Production execution: out of scope
```

Cet ADR définit la structure du code et non une opération de production.

---

# 90. Réversibilité

## 90.1 Classification

```text
élevée vers une extraction future

faible vers un monolithe sans frontières
```

---

## 90.2 Extraction future

Un module dont :

- les contrats ;
- les données ;
- les dépendances ;
- les événements ;

sont clairement définis peut être candidat à une extraction future.

---

## 90.3 Microservice futur

Une extraction nécessitera néanmoins :

- transport réseau ;
- authentification ;
- résilience ;
- outbox ;
- observabilité distribuée ;
- nouvel ADR.

---

# 91. Plan d’implémentation

## Étape 1 — Contrats de module

Définir :

- `ModuleId` ;
- `ModuleVersion` ;
- `ModuleManifest` ;
- `ModuleDependency` ;
- erreurs.

---

## Étape 2 — Schéma de manifest

Créer et versionner le schéma machine-readable.

---

## Étape 3 — Résolveur

Implémenter :

- validation ;
- dépendances transitives ;
- plages de versions ;
- cycles ;
- ordre topologique.

---

## Étape 4 — Composition root

Générer :

- références ;
- enregistrements ;
- routes ;
- contributions ;
- métadonnées.

---

## Étape 5 — Contrats publics

Créer les premiers projets ou packages `Contracts` nécessaires.

---

## Étape 6 — Tests d’architecture

Interdire :

- références internes ;
- cycles ;
- entités exposées ;
- accès directs aux données.

---

## Étape 7 — Propriété des données

Documenter pour chaque module :

- tables ;
- écritures ;
- contrats de lecture ;
- rétention ;
- suppression.

---

## Étape 8 — Modules d’intégration

Créer les intégrations nécessaires sans coupler les modules principaux.

---

## Étape 9 — Versionnement

Ajouter :

- compatibilité ;
- changelog ;
- lock file ;
- upgrade plan.

---

# 92. Tâches liées proposées

```text
SF-TASK-061 — Define module identity and version contracts

SF-TASK-062 — Define module manifest schema version 1

SF-TASK-063 — Implement module dependency resolution

SF-TASK-064 — Reject circular module dependencies

SF-TASK-065 — Generate the client module composition root

SF-TASK-066 — Define public module contract conventions

SF-TASK-067 — Add module architecture tests

SF-TASK-068 — Add module data ownership validation

SF-TASK-069 — Add route and permission collision validation

SF-TASK-070 — Add module compatibility information to the lock file

SF-TASK-071 — Create the first explicit integration module
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 93. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Un module possède un ID et une version.

Un manifest invalide est refusé.

Une dépendance obligatoire est résolue.

Une dépendance absente est refusée.

Une plage incompatible est refusée.

Un cycle direct est refusé.

Un cycle indirect est refusé.

L’ordre de composition est déterministe.

Le composition root ne dépend pas de la réflexion.

Un module ne référence pas l’implémentation d’un autre.

Un contrat public n’expose pas d’entité EF Core.

Un contrat public n’expose pas IQueryable.

Un module ne requête pas directement une table privée.

Une collision de route est refusée.

Une collision de permission est refusée.

Une source de package inconnue est refusée.

Les versions exactes apparaissent dans le lock file.

Un projet de référence composé restaure, compile et teste.

Une customization consomme uniquement des contrats publics.
```

---

# 94. Conditions de révision

Réexaminer cette décision si :

- des modules doivent être installés sans recompilation ;
- des tiers doivent fournir des plugins ;
- une marketplace de modules devient nécessaire ;
- plusieurs équipes doivent déployer indépendamment ;
- un module nécessite une scalabilité très différente ;
- un broker distribué devient indispensable ;
- les transactions inter-modules deviennent trop fréquentes ;
- les frontières logiques ne suffisent plus à la sécurité ;
- la base partagée empêche l’évolution des modules ;
- le composition root devient trop complexe ;
- les contrats publics évoluent trop rapidement ;
- l’application doit charger des extensions client au runtime.

---

# 95. Indicateurs à suivre

- nombre de modules ;
- nombre moyen de dépendances ;
- profondeur du graphe ;
- cycles détectés ;
- conflits de versions ;
- nombre de contrats publics ;
- changements majeurs de contrats ;
- accès directs interdits détectés ;
- modules d’intégration ;
- collisions de routes ;
- collisions de permissions ;
- temps de composition ;
- durée des upgrades ;
- modules deprecated ;
- customizations cassées par upgrade.

---

# 96. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des modules référencent régulièrement les entités des autres

le shared kernel accumule des concepts métier

les contrats exposent les DbContext

les dépendances circulaires sont contournées par service locator

les modules modifient directement les tables des autres

la réflexion remplace la composition explicite

les manifests ne correspondent plus au code

les modules d’intégration contiennent trop de logique métier

les upgrades nécessitent des modifications globales fréquentes

la suppression d’un module devient impossible
```

---

# 97. Conséquences observées

Cette section est append-only après mise en œuvre.

## 97.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète du résolveur
et du composition root des modules.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- module ;
- dépendance ;
- contrat concerné ;
- bénéfice ou incident ;
- action proposée.

---

# 98. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- `10-feature-flags-and-entitlements.md`
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `15-cli-specification.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0003-postgresql-as-primary-database.md`
- `0004-explicit-production-migrations.md`
- `0005-file-generation-strategies.md`
- `0006-feature-flags-and-entitlements-separation.md`

---

# 99. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 94 |

---

# 100. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 101. Synthèse de la décision

```text
Projet client
→ monolithe modulaire

Sélection des modules
→ configuration
→ catalogue
→ manifests
→ graphe de dépendances
→ composition statique
→ compilation

Module
→ ID stable
→ version
→ manifest
→ contrats publics
→ implémentation interne
→ propriété de ses données

Interaction entre modules
→ contrat public
→ interface applicative
→ événement d’intégration
→ module d’intégration

Interdictions
→ pas de plugin runtime
→ pas de scan par réflexion
→ pas de cycle
→ pas d’accès direct aux tables d’un autre module
→ pas d’entité EF dans les contrats
→ pas d’IQueryable public
→ pas de dépendance interne non déclarée

Application finale
→ composition connue
→ versions verrouillées
→ build déterministe
→ déploiement unique
```

> SiteFactory compose les modules avant la compilation afin de conserver la simplicité opérationnelle d’un monolithe, tout en imposant les frontières, les contrats et la propriété des données nécessaires à une évolution durable.
