---
adrId: ADR-0010
title: Use structured observability with OpenTelemetry and provider-neutral exports
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
  - ../02-architecture/09-module-architecture.md
  - ../03-generation/generated-runtime-database-model.md
  - ../04-quality-security/18-security-baseline.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/21-observability-and-logging.md
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/23-backup-and-restore.md
  - ../05-operations/24-operational-runbooks.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../05-operations/27-client-handover-and-service-closure.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0003-postgresql-as-primary-database.md
  - ./0004-explicit-production-migrations.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0009-client-customizations-and-extension-points.md
targetVersion: 0.1.0
---

# ADR-0010 — Observabilité structurée avec OpenTelemetry

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le public dispose de diagnostics bornés et d'événements Activity corrélés,
notamment les [preuves PostgreSQL/readiness](../../tests/readiness-concurrency-regression.md).
Cela ne signifie pas qu'un pipeline OTLP, collector, dashboard, alerting,
backend de logs ou exporteur OpenTelemetry de production est déjà livré.
L'architecture ILogger/ActivitySource/Meter/OTLP ci-dessous demeure la cible,
provider-agnostic, sans appel externe implicite ni données sensibles.
`sample-project-web` est un nom de service fictif, pas un client réel.
Les campagnes acquises restent des preuves locales, pas une CI distante validée.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory adopte une observabilité structurée et indépendante du fournisseur reposant sur :

```text
Microsoft.Extensions.Logging
+ OpenTelemetry
+ export OTLP
+ attributs et conventions SiteFactory
```

Les applications clientes générées doivent pouvoir produire :

- des logs structurés ;
- des traces distribuées ;
- des métriques ;
- des health checks ;
- des événements d’audit distincts ;
- des identifiants de corrélation ;
- des diagnostics exploitables sans exposer de secrets ou de données personnelles inutiles.

Le code métier et les modules ne doivent pas dépendre directement d’un fournisseur d’observabilité particulier.

---

## 2. Résumé

Le modèle de référence est :

```text
Application
→ ILogger pour les logs structurés
→ ActivitySource pour les traces
→ Meter pour les métriques
→ health checks ASP.NET Core
→ audit applicatif distinct

Signaux
→ instrumentation OpenTelemetry
→ export OTLP
→ collector ou backend approuvé
→ visualisation, alertes et rétention
```

La configuration initiale peut utiliser :

```text
Développement
→ console lisible
→ traces locales facultatives
→ métriques locales facultatives

Staging et production
→ logs structurés
→ traces et métriques OpenTelemetry
→ export OTLP sécurisé
→ backend remplaçable
```

Les signaux doivent respecter une politique de minimisation.

Sont interdits dans les logs, traces, métriques et rapports :

- mots de passe ;
- tokens ;
- chaînes de connexion ;
- clés API ;
- contenu complet de formulaires ;
- données personnelles non nécessaires ;
- corps complets de requêtes ;
- pièces jointes ;
- contenu des fichiers ;
- SQL contenant des valeurs sensibles.

---

## 3. Contexte

SiteFactory doit générer et maintenir plusieurs applications clientes isolées.

Chaque projet possède :

- son repository ;
- son application ;
- sa base PostgreSQL ;
- ses modules ;
- ses personnalisations ;
- ses environnements ;
- ses déploiements ;
- ses sauvegardes ;
- ses migrations.

L’exploitation doit pouvoir répondre à des questions telles que :

```text
L’application répond-elle ?

La base PostgreSQL est-elle disponible ?

Quelle version est déployée ?

Quelle opération a échoué ?

Quel module est concerné ?

Un fournisseur externe répond-il lentement ?

Une migration a-t-elle réussi ?

Un backup a-t-il été produit ?

Quel est le parcours d’une requête ?

Une fonctionnalité est-elle désactivée ?

Le taux d’erreur augmente-t-il ?
```

Les diagnostics ne doivent pas reposer uniquement sur :

- des messages texte libres ;
- une connexion SSH ;
- l’inspection manuelle des conteneurs ;
- des captures d’écran ;
- des logs non corrélés ;
- la mémoire de l’opérateur.

Une solution liée directement à un fournisseur spécifique rendrait les projets plus difficiles à :

- transférer ;
- déplacer ;
- héberger ailleurs ;
- exécuter hors du service LogicGate ;
- comparer entre environnements.

---

## 4. Problème à résoudre

Comment produire des diagnostics exploitables et corrélés tout en garantissant :

- une faible dépendance fournisseur ;
- une instrumentation homogène ;
- une corrélation entre les couches ;
- une cardinalité maîtrisée ;
- une collecte sécurisée ;
- une dégradation contrôlée si le backend est indisponible ;
- une absence de secrets et de PII inutiles ;
- une compatibilité avec les projets isolés ;
- une exploitation adaptée à une petite équipe ?

La solution doit également éviter :

- les logs texte impossibles à requêter ;
- les messages différents pour un même incident ;
- l’enregistrement multiple de la même exception ;
- les labels de métriques à cardinalité illimitée ;
- la capture automatique de données sensibles ;
- les SDK propriétaires dispersés dans le domaine ;
- une panne de télémétrie bloquant l’application ;
- des volumes et coûts incontrôlés.

---

## 5. Portée

Cette décision couvre :

- logs applicatifs ;
- traces ;
- spans ;
- métriques ;
- health checks ;
- identifiants de corrélation ;
- activités HTTP ;
- PostgreSQL ;
- appels externes ;
- jobs ;
- migrations ;
- backups ;
- génération ;
- déploiement ;
- capabilities ;
- modules ;
- customizations ;
- configuration OpenTelemetry ;
- exports ;
- sampling ;
- cardinalité ;
- rétention ;
- sécurité ;
- tests ;
- quality gates ;
- transfert client.

---

## 6. Hors portée

Cette décision ne définit pas :

- le fournisseur final de visualisation ;
- le fournisseur d’alertes ;
- le service de stockage long terme ;
- tous les dashboards ;
- tous les seuils d’alerte ;
- une plateforme SOC complète ;
- un SIEM obligatoire ;
- un service OpenTelemetry Collector central partagé dès le MVP ;
- une garantie réglementaire de conservation ;
- une politique juridique définitive sur chaque type de log ;
- le format complet de l’audit métier ;
- un APM propriétaire obligatoire.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Diagnostic fiable des incidents**
2. **Minimisation des données**
3. **Absence de secrets**
4. **Corrélation entre les opérations**
5. **Indépendance du fournisseur**
6. **Cardinalité maîtrisée**
7. **Compatibilité avec les projets isolés**
8. **Faible impact sur le runtime**
9. **Dégradation contrôlée**
10. **Configuration par environnement**
11. **Testabilité**
12. **Alertes exploitables**
13. **Transfert client**
14. **Coût maîtrisable**
15. **Standards ouverts**
16. **Extensibilité par modules**

---

## 8. Contraintes

La décision doit respecter :

```text
.NET 10

ASP.NET Core et Blazor

PostgreSQL

Docker Compose

Linux en production

Un projet et environnement par client

Microsoft.Extensions.Logging

OpenTelemetry

Export OTLP

Aucune dépendance fournisseur dans Domain

Aucun secret dans les signaux

UTC pour les événements techniques

Configuration typée et validée

Mode dégradé si le backend est indisponible
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les projets initiaux utilisent peu d’instances ;
- le trafic initial reste compatible avec un sampling simple ;
- un backend ou collector OTLP peut être ajouté sans modifier le code métier ;
- la console Docker reste disponible comme mécanisme local de secours ;
- les applications n’ont pas besoin de conserver toutes les traces ;
- les métriques agrégées sont plus importantes que les dimensions par utilisateur ;
- les logs d’audit peuvent avoir une politique distincte ;
- l’observabilité ne doit pas être une dépendance fonctionnelle du site.

---

# 10. Option A — Logs texte uniquement

## 10.1 Description

L’application écrit des messages texte dans la console ou dans des fichiers.

Exemple :

```text
An error occurred while processing the lead.
```

---

## 10.2 Avantages

- mise en œuvre simple ;
- faible nombre de dépendances ;
- lisible localement ;
- compatible avec Docker ;
- coût initial limité.

---

## 10.3 Inconvénients

- requêtes difficiles ;
- contexte incohérent ;
- corrélation limitée ;
- absence de métriques standardisées ;
- absence de traces ;
- parsing fragile ;
- dashboards et alertes limités ;
- coûts élevés d’investigation manuelle.

---

## 10.4 Risques

- incident impossible à reconstituer ;
- messages ambigus ;
- données sensibles concaténées ;
- dépendance à des recherches textuelles ;
- doublons ;
- absence de dimension client ou environnement.

---

## 10.5 Réversibilité

```text
élevée
```

Une instrumentation structurée peut être ajoutée ultérieurement, mais l’historique restera incomplet.

---

# 11. Option B — SDK propriétaire intégré directement

## 11.1 Description

Les modules et services utilisent directement l’API d’un fournisseur APM ou de logging.

---

## 11.2 Avantages

- fonctionnalités avancées ;
- intégration rapide avec le fournisseur ;
- dashboards prêts à l’emploi ;
- support fournisseur ;
- diagnostics spécifiques.

---

## 11.3 Inconvénients

- couplage ;
- migration coûteuse ;
- code propriétaire dans les modules ;
- transfert client difficile ;
- instrumentation non portable ;
- coûts imposés ;
- différences entre projets.

---

## 11.4 Risques

- verrouillage fournisseur ;
- données envoyées vers une destination non prévue ;
- dépendance indisponible ;
- instrumentation différente selon les modules ;
- hausse de coût difficile à contrôler.

---

## 11.5 Réversibilité

```text
faible à moyenne
```

La suppression du SDK peut nécessiter de modifier toutes les couches.

---

# 12. Option C — Stack d’observabilité entièrement personnalisée

## 12.1 Description

LogicGate définit ses propres formats, bibliothèques, agents, transport et stockage.

---

## 12.2 Avantages

- contrôle complet ;
- adaptation exacte ;
- aucun fournisseur imposé ;
- format propre.

---

## 12.3 Inconvénients

- coût de développement important ;
- maintenance ;
- protocole propriétaire ;
- manque d’écosystème ;
- risques de sécurité ;
- compatibilité limitée ;
- peu adapté à une petite équipe.

---

## 12.4 Risques

- instrumentation incomplète ;
- backend fragile ;
- absence de standards ;
- coût supérieur à la valeur ;
- dépendance à un code interne peu maintenu.

---

## 12.5 Réversibilité

```text
faible
```

Les contrats propriétaires deviendraient structurants.

---

# 13. Option D — OpenTelemetry et logs structurés

## 13.1 Description

L’application utilise :

- `ILogger` ;
- `ActivitySource` ;
- `Meter` ;
- OpenTelemetry ;
- OTLP ;
- conventions standard et SiteFactory.

---

## 13.2 Avantages

- standard ouvert ;
- fournisseur remplaçable ;
- intégration .NET ;
- logs, traces et métriques corrélables ;
- instrumentation automatique disponible ;
- transfert client facilité ;
- backend adaptable ;
- support d’un collector ;
- architecture compatible avec plusieurs environnements.

---

## 13.3 Inconvénients

- configuration plus importante ;
- gestion du sampling ;
- maîtrise de la cardinalité ;
- backend ou collector à exploiter ;
- instrumentation à standardiser ;
- coût de collecte ;
- besoin de tester les exports.

---

## 13.4 Risques

- trop de données ;
- données sensibles capturées automatiquement ;
- labels à forte cardinalité ;
- instrumentation incohérente ;
- exporter indisponible ;
- buffer excessif.

---

## 13.5 Réversibilité

```text
élevée
```

Le backend peut être remplacé tout en conservant la majorité de l’instrumentation.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Texte seul | SDK propriétaire | Stack maison | OpenTelemetry |
|---|---:|---:|---:|---:|---:|
| Corrélation | 5 | 1 | 5 | 4 | 5 |
| Indépendance fournisseur | 5 | 5 | 1 | 5 | 5 |
| Standardisation | 5 | 2 | 4 | 2 | 5 |
| Facilité initiale | 3 | 5 | 4 | 1 | 3 |
| Transfert client | 4 | 4 | 2 | 3 | 5 |
| Métriques et traces | 5 | 1 | 5 | 3 | 5 |
| Écosystème | 4 | 3 | 5 | 1 | 5 |
| Maintenabilité | 5 | 2 | 3 | 1 | 5 |
| Maîtrise des coûts | 4 | 4 | 2 | 2 | 4 |
| Réversibilité | 4 | 5 | 2 | 2 | 5 |

L’option OpenTelemetry est retenue.

---

# 15. Décision

Nous décidons d’utiliser une observabilité structurée et indépendante du fournisseur.

Les mécanismes de référence sont :

```text
Logs
→ Microsoft.Extensions.Logging

Traces
→ System.Diagnostics.ActivitySource
→ OpenTelemetry tracing

Métriques
→ System.Diagnostics.Metrics.Meter
→ OpenTelemetry metrics

Export
→ OTLP

Contexte
→ W3C Trace Context
```

---

## 15.1 Dépendances par couche

```text
Domain
→ aucune dépendance d’observabilité technique obligatoire

Application
→ abstractions minimales et ILogger lorsque justifié

Infrastructure
→ instrumentation PostgreSQL, HTTP, stockage et providers

Web
→ HTTP, Blazor, endpoints, middleware et health checks

Operations
→ export, collector, dashboards et alertes
```

---

## 15.2 Fournisseur

Le backend d’observabilité peut être remplacé sans changer :

- les cas d’usage ;
- les modules ;
- les contrats métier ;
- les customizations.

---

## 15.3 SDK propriétaire

Un SDK propriétaire peut être ajouté uniquement dans un adaptateur Infrastructure ou d’export explicitement isolé.

Il ne doit pas devenir l’API utilisée par les modules.

---

# 16. Les quatre signaux

SiteFactory distingue :

```text
Logs
Traces
Metrics
Health
```

L’audit possède une responsabilité séparée.

---

## 16.1 Logs

Répondent principalement à :

```text
Que s’est-il passé ?
```

---

## 16.2 Traces

Répondent principalement à :

```text
Quel parcours a suivi l’opération ?
```

---

## 16.3 Métriques

Répondent principalement à :

```text
À quelle fréquence et avec quelle tendance ?
```

---

## 16.4 Health checks

Répondent principalement à :

```text
Cette instance peut-elle recevoir du trafic ?
```

---

## 16.5 Audit

Répond principalement à :

```text
Qui a effectué quelle action sensible et quand ?
```

Un log d’application ne remplace pas un audit.

---

# 17. Logs structurés

Les logs utilisent des templates structurés.

Exemple recommandé :

```csharp
logger.LogInformation(
    "Lead {LeadId} changed status from {PreviousStatus} to {NewStatus}",
    leadId,
    previousStatus,
    newStatus);
```

---

## 17.1 Interpolation à éviter

Éviter :

```csharp
logger.LogInformation(
    $"Lead {leadId} changed status to {newStatus}");
```

car la structure et les propriétés sont moins fiables.

---

## 17.2 Propriétés stables

Les noms de propriétés doivent rester stables entre versions.

---

## 17.3 Message

Le message doit rester compréhensible par un humain sans être le seul contrat d’analyse.

---

# 18. Niveaux de logs

## 18.1 `Trace`

Diagnostics très détaillés, désactivés par défaut en production.

---

## 18.2 `Debug`

Informations utiles au développement ou à une investigation ciblée.

---

## 18.3 `Information`

Événement normal et significatif.

Exemples :

- démarrage ;
- fin d’une opération importante ;
- déploiement détecté ;
- job terminé ;
- backup réussi.

---

## 18.4 `Warning`

Situation anormale mais récupérable.

Exemples :

- retry ;
- configuration dépréciée ;
- fournisseur lent ;
- feature flag expiré ;
- capacité désactivée pour incohérence.

---

## 18.5 `Error`

Opération échouée nécessitant une analyse ou ayant un impact fonctionnel.

---

## 18.6 `Critical`

Défaillance grave :

- démarrage impossible ;
- données potentiellement corrompues ;
- configuration de sécurité invalide ;
- restauration nécessaire ;
- perte d’une dépendance essentielle prolongée.

---

# 19. Exception logging

## 19.1 Une exception, un propriétaire de log

Une exception ne doit pas être enregistrée à chaque couche.

---

## 19.2 Boundary logging

L’exception est journalisée à la frontière qui :

- la transforme ;
- l’arrête ;
- renvoie une erreur ;
- déclenche un retry final ;
- la considère comme incident.

---

## 19.3 Rethrow

Une couche qui relance l’exception sans ajouter de décision significative ne doit généralement pas la relogguer.

---

## 19.4 Données

Le message d’exception peut contenir une donnée sensible.

L’application doit éviter de recopier automatiquement :

- contenu utilisateur ;
- SQL détaillé ;
- URL avec token ;
- credential ;
- corps HTTP.

---

# 20. Erreurs structurées

Les erreurs métier et applicatives utilisent des codes stables.

Exemples :

```text
SF-LEAD-ARCHIVE-001

SF-MIGRATION-014

SF-SUPPLY-003
```

Les signaux peuvent contenir :

```text
ErrorCode
ErrorCategory
Operation
Result
```

---

## 20.1 Message utilisateur

Le message retourné à l’utilisateur reste distinct du diagnostic interne.

---

## 20.2 Stack trace

Une stack trace ne doit jamais être envoyée dans une réponse publique.

---

# 21. Activités et traces

Chaque opération significative peut créer une `Activity`.

Exemples :

```text
SiteFactory.GenerateProject

SampleRecords.CreateLead

SampleRecords.ChangeStatus

Migration.Apply

Backup.Create

ExternalCrm.PushLead
```

---

## 21.1 Activités automatiques

OpenTelemetry peut instrumenter automatiquement :

- requêtes ASP.NET Core ;
- appels `HttpClient` ;
- accès PostgreSQL via le provider et EF Core selon la configuration ;
- runtime .NET lorsque pertinent.

---

## 21.2 Activités manuelles

Une activité manuelle est ajoutée pour un cas d’usage métier ou opérationnel réellement utile.

---

## 21.3 Pas de span par méthode

Il ne faut pas créer un span pour chaque méthode privée.

---

# 22. Nommage des `ActivitySource`

Convention recommandée :

```text
LogicGate.SiteFactory.{Component}
```

Exemples :

```text
LogicGate.SiteFactory.Generator

LogicGate.SiteFactory.Migration

LogicGate.SiteFactory.SampleRecords

LogicGate.SiteFactory.ClientCustomizations
```

---

## 22.1 Version

L’`ActivitySource` peut déclarer la version du composant lorsqu’elle est disponible.

---

## 22.2 Identifiant stable

Le nom ne dépend pas :

- du client ;
- de l’environnement ;
- de la machine ;
- du nom commercial.

Ces éléments utilisent des attributs de ressource.

---

# 23. Nommage des activités

Format recommandé :

```text
{Component}.{Operation}
```

Exemples :

```text
SampleRecords.Create

SampleRecords.Export

Migration.Plan

Migration.Apply

Backup.Verify

Generation.ResolveModules
```

---

## 23.1 Pas d’identifiant dans le nom

Exemple interdit :

```text
SampleRecords.Get.3f909ec2
```

Les identifiants variables ne doivent pas créer un nouveau nom de span.

---

# 24. Attributs de ressource

Chaque instance doit publier des attributs stables tels que :

```text
service.name

service.namespace

service.version

service.instance.id

deployment.environment

sitefactory.client.slug
```

La nomenclature exacte doit privilégier les conventions OpenTelemetry existantes lorsqu’elles couvrent le besoin.

---

## 24.1 `service.name`

Exemple conceptuel :

```text
sample-project-web
```

ou un identifiant technique équivalent.

---

## 24.2 Client slug

Le `ClientSlug` est un identifiant technique et non une donnée personnelle.

Il peut être utilisé comme attribut statique de ressource.

---

## 24.3 Environnement

Valeurs contrôlées :

```text
development
test
staging
production
```

---

## 24.4 Version

La version doit correspondre à la release applicative déployée.

---

# 25. Attributs opérationnels autorisés

Selon le signal :

```text
sitefactory.module.id

sitefactory.capability.id

sitefactory.operation

sitefactory.result

sitefactory.error.code

sitefactory.feature.flag.id

sitefactory.kill_switch.id

sitefactory.migration.run_id

sitefactory.backup.id

sitefactory.generation.run_id
```

---

## 25.1 Identifiants techniques

Un identifiant d’exécution généré peut être utilisé dans les logs et traces.

Il ne doit pas être utilisé comme label de métrique à forte cardinalité.

---

# 26. Données interdites dans les attributs

Sont interdits par défaut :

```text
Email

Nom complet

Téléphone

Adresse

Message du formulaire

Description libre

Contenu HTML

Token

Cookie

Mot de passe

Clé API

Chaîne de connexion

Corps HTTP

SQL avec valeurs

Nom de fichier uploadé non neutralisé

Chemin local utilisateur
```

---

## 26.1 Exception métier

Une donnée personnelle ne peut être ajoutée que si :

- le besoin est démontré ;
- la politique de sécurité l’autorise ;
- la rétention est adaptée ;
- l’accès est limité ;
- la donnée est minimisée.

La baseline reste :

```text
ne pas la collecter
```

---

# 27. Baggage

Le baggage distribué doit rester minimal.

Il peut contenir uniquement des informations :

- non sensibles ;
- stables ;
- nécessaires à plusieurs composants.

---

## 27.1 Interdictions

Ne jamais propager par baggage :

- email ;
- utilisateur ;
- token ;
- rôle complet ;
- contenu ;
- secret ;
- données métier.

---

## 27.2 Client

Le projet étant isolé par client, le client slug n’a normalement pas besoin d’être propagé dynamiquement dans chaque requête.

Il appartient à la ressource du service.

---

# 28. Corrélation

Les signaux d’une même opération doivent partager :

```text
TraceId
SpanId lorsque applicable
CorrelationId métier ou opérateur lorsque nécessaire
```

---

## 28.1 Trace ID

Le Trace ID est l’identifiant principal pour corréler :

- requête HTTP ;
- appels internes ;
- PostgreSQL ;
- appels externes ;
- logs.

---

## 28.2 Correlation ID

Un `CorrelationId` applicatif peut être utilisé lorsque :

- l’opération dépasse la durée d’une trace ;
- un job reprend plus tard ;
- un rapport doit être rapproché ;
- une opération métier doit être recherchée indépendamment.

---

## 28.3 Header externe

Un identifiant de corrélation entrant n’est pas accepté aveuglément.

Il doit être :

- borné ;
- validé ;
- neutralisé ;
- remplacé si invalide.

---

# 29. Propagation

Les appels HTTP utilisent le standard W3C Trace Context.

---

## 29.1 Services externes

La propagation vers un tiers doit être compatible avec :

- le contrat ;
- la sécurité ;
- la politique du fournisseur.

---

## 29.2 Fournisseur non fiable

Les headers de tracing provenant d’un tiers sont traités comme une entrée non fiable.

---

## 29.3 Jobs

Un job créé à partir d’une requête peut conserver :

- un lien de trace ;
- un Correlation ID ;
- un Job ID.

Il ne doit pas conserver l’ensemble du contexte utilisateur dans les attributs.

---

# 30. Métriques

Les métriques sont créées avec `System.Diagnostics.Metrics`.

Exemples :

```text
sitefactory.http.requests

sitefactory.operation.duration

sitefactory.operation.errors

sitefactory.leads.created

sitefactory.backup.duration

sitefactory.migration.duration

sitefactory.external.calls
```

---

## 30.1 Métriques techniques

Exemples :

- requêtes ;
- latence ;
- erreurs ;
- connexions ;
- mémoire ;
- durée des jobs ;
- durée des backups.

---

## 30.2 Métriques métier

Une métrique métier peut exister si elle aide réellement à l’exploitation.

Exemple :

```text
nombre de leads créés
```

Elle ne doit pas contenir de donnée individuelle.

---

## 30.3 Analytics

Les métriques d’observabilité ne remplacent pas un produit d’analytics métier ou marketing.

---

# 31. Nommage des `Meter`

Convention :

```text
LogicGate.SiteFactory.{Component}
```

Exemples :

```text
LogicGate.SiteFactory.Web

LogicGate.SiteFactory.PostgreSql

LogicGate.SiteFactory.SampleRecords

LogicGate.SiteFactory.Backup
```

---

# 32. Types de métriques

Utiliser selon le besoin :

- counter ;
- up/down counter ;
- histogram ;
- observable gauge.

---

## 32.1 Counter

Pour des événements cumulables.

Exemple :

```text
leads created
```

---

## 32.2 Histogram

Pour des durées ou tailles.

Exemple :

```text
external request duration
```

---

## 32.3 Gauge

Pour un état courant observable.

Exemple :

```text
active jobs
```

---

# 33. Cardinalité des métriques

La cardinalité doit rester bornée.

---

## 33.1 Labels autorisés

Exemples contrôlés :

```text
environment

module

operation

result

error_category

provider
```

---

## 33.2 Labels interdits

Ne jamais utiliser comme label de métrique :

```text
UserId

Email

LeadId

RequestId

TraceId

CorrelationId

URL complète

Exception message

Nom de fichier

Valeur libre

Identifiant d’un backup
```

---

## 33.3 Routes HTTP

Les métriques HTTP utilisent des routes normalisées.

Exemple :

```text
/admin/leads/{id}
```

et non :

```text
/admin/leads/3f909ec2
```

---

# 34. Budget de cardinalité

Chaque nouvelle métrique doit documenter :

- nom ;
- type ;
- unité ;
- description ;
- labels ;
- valeurs possibles ;
- propriétaire ;
- usage ;
- durée de vie.

---

## 34.1 Label non borné

Un label dont les valeurs ne peuvent pas être énumérées ou raisonnablement limitées est refusé.

---

## 34.2 Revue

Toute métrique ajoutée à une boucle fréquente doit être revue pour son coût.

---

# 35. Sampling des traces

Le sampling dépend de l’environnement.

---

## 35.1 Développement

Sampling élevé ou total acceptable.

---

## 35.2 Staging

Sampling élevé afin de valider les parcours.

---

## 35.3 Production

Sampling contrôlé afin de maîtriser :

- volume ;
- coût ;
- charge ;
- rétention.

---

## 35.4 Parent-based

La décision de sampling doit respecter autant que possible le contexte parent afin de conserver des traces cohérentes.

---

## 35.5 Erreurs

Une stratégie future peut privilégier la conservation des traces en erreur si le backend ou le collector le permet sans fragiliser le runtime.

---

## 35.6 Configuration

Le taux de sampling est une configuration opérationnelle.

Il ne nécessite pas une recompilation.

---

# 36. Export OTLP

Le protocole de référence est :

```text
OTLP
```

Le transport peut être configuré selon l’environnement et le backend retenu.

---

## 36.1 Endpoint

L’endpoint est fourni par la configuration d’environnement.

---

## 36.2 Authentification

Les credentials éventuels sont des secrets.

Ils ne sont pas présents dans :

- le repository ;
- `client.sitefactory.yml` ;
- le lock file ;
- les rapports ;
- les logs.

---

## 36.3 TLS

Les exports distants utilisent une connexion chiffrée et une validation de certificat appropriée.

---

# 37. OpenTelemetry Collector

Un OpenTelemetry Collector peut être utilisé comme intermédiaire.

Architecture conceptuelle :

```text
Application cliente
→ OTLP
→ OpenTelemetry Collector
→ backend de logs, métriques et traces
```

---

## 37.1 Avantages

- gestion centralisée des exports ;
- filtrage ;
- batching ;
- redaction ;
- changement de backend ;
- limitation de charge ;
- authentification ;
- routage.

---

## 37.2 MVP

Le collector n’est pas nécessairement obligatoire dans tous les environnements du premier incrément.

L’application doit néanmoins rester compatible OTLP.

---

## 37.3 Collector mutualisé

Un collector mutualisé entre plusieurs clients doit :

- conserver l’isolation logique ;
- limiter les accès ;
- appliquer les attributs de ressource ;
- ne pas mélanger les credentials ;
- éviter les routes dynamiques basées sur des données non fiables.

---

# 38. Export des logs

Les logs peuvent être :

- écrits en console structurée ;
- collectés par le runtime de conteneur ;
- exportés par OpenTelemetry ;
- envoyés vers un backend approuvé.

---

## 38.1 Baseline Docker

La console reste le mécanisme de secours minimal.

---

## 38.2 Fichiers locaux

L’application ne doit pas gérer par défaut une rotation complexe de fichiers de logs dans son conteneur.

---

## 38.3 Sortie structurée

La production privilégie un format structuré compatible avec la collecte.

---

# 39. Mode dégradé

Une panne d’observabilité ne doit normalement pas interrompre :

- les pages publiques ;
- la création d’un lead ;
- l’administration ;
- les opérations métier.

---

## 39.1 Export non bloquant

L’export doit utiliser :

- batching ;
- buffers bornés ;
- timeouts ;
- retries bornés ;
- abandon contrôlé.

---

## 39.2 Interdictions

L’application ne doit pas :

- attendre indéfiniment l’export ;
- remplir le disque sans limite ;
- conserver les signaux indéfiniment en mémoire ;
- effectuer des retries illimités ;
- bloquer une transaction PostgreSQL pour exporter une trace.

---

## 39.3 Exception

Un audit obligatoire peut posséder des exigences plus fortes.

Il doit alors être conçu comme une fonctionnalité distincte et non comme un simple export de télémétrie.

---

# 40. Audit applicatif

L’audit est distinct des logs d’observabilité.

---

## 40.1 Exemples d’actions auditées

- connexion administrateur ;
- changement de rôle ;
- export de données ;
- suppression ;
- changement d’entitlement ;
- activation d’un kill switch ;
- migration ;
- restauration ;
- archivage d’un lead.

---

## 40.2 Propriétés

Un audit peut enregistrer :

- action ;
- acteur technique ;
- ressource ;
- résultat ;
- timestamp UTC ;
- raison ;
- Correlation ID.

---

## 40.3 Rétention

La rétention de l’audit peut être différente de celle des logs techniques.

---

## 40.4 Immutabilité

Les exigences d’intégrité de l’audit peuvent être supérieures à celles des logs.

---

# 41. Données personnelles

## 41.1 Principe

Les signaux techniques doivent fonctionner sans données personnelles directement identifiantes dans la majorité des cas.

---

## 41.2 Identifiant d’utilisateur

Un identifiant utilisateur interne ne doit pas être ajouté systématiquement.

Lorsqu’un besoin d’audit l’exige, il appartient au signal d’audit et à sa politique dédiée.

---

## 41.3 Lead

Un Lead ID peut être présent dans un log ciblé uniquement si :

- il s’agit d’un identifiant technique ;
- le besoin de diagnostic le justifie ;
- il n’est pas utilisé comme label de métrique ;
- la rétention est maîtrisée.

La préférence reste un `OperationId` ou `CorrelationId`.

---

## 41.4 Redaction

Les valeurs libres provenant de l’utilisateur doivent être :

- exclues ;
- tronquées ;
- neutralisées ;
- ou classifiées avant journalisation.

---

# 42. Secrets

Les secrets ne sont jamais journalisés.

Cela inclut :

```text
Authorization header

Cookie

Antiforgery token

JWT

API key

SMTP password

PostgreSQL password

Connection string

Private key

Backup credential

OTLP credential
```

---

## 42.1 Configuration invalide

Une erreur de configuration indique le nom de la clé manquante, jamais sa valeur.

---

## 42.2 URL

Les URLs peuvent contenir des query strings sensibles.

La journalisation doit préférer :

- host ;
- route normalisée ;
- méthode ;
- statut.

---

# 43. Requêtes et réponses HTTP

La capture complète des corps HTTP est désactivée par défaut.

---

## 43.1 Formulaires publics

Le contenu des formulaires ne doit pas être ajouté aux traces ou logs.

---

## 43.2 Headers

Les headers sensibles sont exclus ou redactés.

---

## 43.3 Query string

La query string complète n’est pas collectée par défaut.

---

## 43.4 Uploads

Le contenu d’un fichier n’est jamais journalisé.

Les métadonnées techniques minimales peuvent inclure :

- taille ;
- type autorisé ;
- résultat de validation.

---

# 44. PostgreSQL

L’instrumentation PostgreSQL peut enregistrer :

- durée ;
- résultat ;
- type d’opération ;
- nombre de lignes lorsque pertinent ;
- timeout ;
- erreur catégorisée.

---

## 44.1 SQL

Le SQL complet avec paramètres sensibles ne doit pas être collecté en production.

---

## 44.2 Sensitive data logging

Le sensitive data logging EF Core est désactivé dans les environnements contenant des données réelles.

---

## 44.3 Chaîne de connexion

Elle ne doit jamais apparaître dans :

- log ;
- span ;
- exception publique ;
- health check ;
- rapport.

---

# 45. Appels HTTP externes

L’instrumentation doit capturer :

- fournisseur technique ;
- opération ;
- durée ;
- résultat ;
- statut HTTP ;
- timeout ;
- retry final.

---

## 45.1 URL complète

Ne pas enregistrer les query strings sensibles ou identifiants personnels.

---

## 45.2 Corps

Les corps de requête et de réponse ne sont pas capturés par défaut.

---

## 45.3 Provider

Le nom du fournisseur peut être utilisé comme label borné.

---

# 46. Jobs

Chaque job significatif doit posséder :

```text
JobId
JobType
Operation
Result
Duration
Attempt
CorrelationId éventuel
```

---

## 46.1 Cardinalité

`JobId` peut apparaître dans un log ou span, mais pas comme label de métrique.

---

## 46.2 Retries

Les métriques distinguent :

- tentative ;
- succès final ;
- échec final.

---

## 46.3 Concurrence

Une exécution refusée par un verrou doit produire un signal contrôlé.

---

# 47. Migrations

Les migrations suivent l’ADR-0004 et produisent des signaux tels que :

```text
MigrationPlanCreated

MigrationStarted

MigrationApplied

MigrationVerificationCompleted

MigrationFailed
```

---

## 47.1 Propriétés

```text
MigrationRunId

SourceVersion

TargetVersion

SourceMigration

TargetMigration

Result

Duration
```

---

## 47.2 Interdictions

Ne pas journaliser :

- SQL complet ;
- contenu de table ;
- chaîne de connexion ;
- backup secret.

---

# 48. Backups et restaurations

Les signaux peuvent inclure :

```text
BackupId

BackupType

Environment

Result

Duration

SizeBucket

ChecksumVerified

RestoreDrill
```

---

## 48.1 Chemin

Le chemin exact contenant des informations d’infrastructure sensibles n’est pas exposé dans les signaux centralisés sans nécessité.

---

## 48.2 Taille

Une taille exacte peut être utile dans un rapport opérationnel.

Pour une métrique, utiliser une valeur numérique sans identifiant de backup.

---

# 49. Générateur SiteFactory

Le CLI et le générateur doivent produire des signaux pour :

- validation ;
- résolution ;
- planification ;
- génération ;
- conflits ;
- build généré ;
- tests ;
- rapport.

---

## 49.1 CLI interactif

Le CLI présente des messages lisibles à l’opérateur.

Il peut également produire des logs structurés ou rapports JSON.

---

## 49.2 Pas de télémétrie distante implicite

Le CLI ne doit pas envoyer de télémétrie distante sans configuration explicite.

---

## 49.3 Données client

Le contenu de `client.sitefactory.yml` n’est pas exporté en télémétrie.

---

# 50. Modules

Chaque module peut déclarer :

- `ActivitySource` ;
- `Meter` ;
- événements structurés ;
- métriques ;
- dashboards recommandés ;
- alertes ;
- attributs autorisés.

---

## 50.1 Convention commune

Les modules utilisent les conventions SiteFactory plutôt que leur propre modèle incompatible.

---

## 50.2 Propriétaire

Chaque signal possède un module ou composant propriétaire.

---

## 50.3 Suppression

Un signal devenu inutile doit être supprimé ou déprécié afin de ne pas conserver une dette d’observabilité.

---

# 51. Customizations client

Les customizations suivent les mêmes règles.

Elles ne peuvent pas :

- journaliser des secrets ;
- exporter directement vers un service non approuvé ;
- introduire un SDK propriétaire dans Domain ;
- créer des labels à cardinalité illimitée ;
- capturer tous les corps HTTP.

---

## 51.1 Intégration spécifique

Une intégration client peut ajouter :

- spans ;
- métriques ;
- logs ;
- kill switch ;
- alertes.

Elle reste liée à un fournisseur d’export approuvé.

---

# 52. Health checks

Les applications exposent au minimum :

```text
liveness

readiness
```

---

## 52.1 Liveness

Vérifie que le processus fonctionne.

Il ne dépend pas directement de toutes les dépendances externes.

---

## 52.2 Readiness

Vérifie que l’instance peut traiter du trafic.

Il peut dépendre de :

- PostgreSQL ;
- configuration ;
- compatibilité de schéma ;
- stockage essentiel.

---

## 52.3 Health détaillé

Une vue détaillée est protégée et réservée aux opérations.

---

## 52.4 Réponse publique

La réponse publique n’expose pas :

- serveur PostgreSQL ;
- nom de base ;
- chemin de fichier ;
- exception ;
- fournisseur ;
- version sensible.

---

# 53. Health check et alertes

Un health check n’est pas une alerte.

Une alerte doit s’appuyer sur :

- durée ;
- fréquence ;
- persistance ;
- métriques ;
- contexte.

---

## 53.1 Flapping

Les alertes doivent éviter de notifier pour chaque transition brève.

---

# 54. Configuration

La configuration doit être typée et validée.

Exemple conceptuel :

```text
Observability:
  ServiceName
  Environment
  OtlpEndpoint
  TracingEnabled
  MetricsEnabled
  LogsExportEnabled
  SamplingRatio
```

---

## 54.1 Secrets

Les credentials OTLP sont injectés séparément.

---

## 54.2 Valeurs invalides

Une valeur invalide doit :

- empêcher l’export concerné ;
- produire un diagnostic local sûr ;
- ne pas révéler le credential.

---

## 54.3 Production

Une production ne doit pas envoyer des signaux vers un endpoint de développement.

---

# 55. Valeurs par défaut

Baseline proposée :

```text
Logs console
→ activés

Tracing
→ activé en staging et production si un endpoint est configuré

Metrics
→ activées en staging et production si un endpoint est configuré

Export distant
→ désactivé sans configuration explicite

Corps HTTP
→ non capturés

Sensitive data
→ désactivée

Sampling production
→ borné et configurable
```

---

# 56. Rétention

Les durées doivent être définies selon :

- environnement ;
- type de signal ;
- coût ;
- incident ;
- données personnelles ;
- obligations contractuelles.

---

## 56.1 Développement

Rétention courte.

---

## 56.2 Production

Rétention suffisante pour :

- diagnostic ;
- suivi de tendance ;
- incident ;
- maintenance.

---

## 56.3 Audit

Peut utiliser une durée distincte.

---

## 56.4 Indéfinie

Une conservation indéfinie par défaut est interdite.

---

# 57. Accès

Les accès au backend d’observabilité suivent le moindre privilège.

Rôles conceptuels :

```text
viewer

operator

administrator
```

---

## 57.1 Données inter-clients

Un opérateur ne doit pas recevoir plus d’accès que nécessaire.

---

## 57.2 Client transféré

Les accès LogicGate peuvent être retirés ou redéfinis pendant le transfert.

---

# 58. Résidence et destination des données

La destination des signaux doit être connue.

La configuration doit documenter :

- fournisseur ;
- région lorsque pertinente ;
- environnement ;
- type de données ;
- rétention ;
- propriétaire.

---

## 58.1 Nouvelle destination

Une nouvelle destination nécessite une validation avant activation.

---

## 58.2 Endpoint arbitraire

Une URL OTLP fournie par une entrée client non fiable ne doit pas être utilisée directement sans allowlist ou validation opérateur.

---

# 59. Coûts et volume

L’observabilité doit disposer de budgets.

Exemples :

```text
volume journalier de logs

nombre de spans

nombre de séries métriques

rétention

taille des attributs

sampling
```

---

## 59.1 Message volumineux

Les messages et attributs doivent être bornés.

---

## 59.2 Exception répétée

Une erreur répétée ne doit pas produire un volume incontrôlé.

Des mécanismes possibles :

- agrégation ;
- sampling ;
- rate limiting des logs ;
- alerte sur métrique.

---

# 60. Dashboards de référence

Chaque projet standard doit pouvoir fournir au minimum des vues pour :

```text
Disponibilité

Latence HTTP

Taux d’erreur

PostgreSQL

Jobs

Appels externes

Backups

Migrations

Version déployée

Ressources système
```

---

## 60.1 Module

Un module peut proposer un dashboard complémentaire.

---

## 60.2 Pas de dashboard sans source

Une visualisation ne doit pas dépendre d’un signal non versionné ou non documenté.

---

# 61. Alertes de référence

Exemples :

```text
Application non prête durablement

Taux d’erreur HTTP élevé

Latence anormale

PostgreSQL indisponible

Disque proche de la saturation

Backup absent ou en échec

Restore drill en retard

Migration échouée

Kill switch actif trop longtemps

Fournisseur externe en échec durable
```

---

## 61.1 Alerte exploitable

Chaque alerte doit référencer :

- service ;
- environnement ;
- seuil ;
- durée ;
- impact ;
- runbook ;
- propriétaire.

---

## 61.2 Alerte sans action

Une alerte sans action possible doit être revue ou supprimée.

---

# 62. Runbooks

Les alertes importantes doivent référencer :

```text
24-operational-runbooks.md
```

Le runbook doit permettre de passer de :

```text
signal
→ diagnostic
→ action
→ vérification
→ clôture
```

---

# 63. Démarrage de l’application

Au démarrage, l’application peut journaliser :

```text
ServiceName

ApplicationVersion

Environment

CommitSha éventuel

ConfigurationProfile

ModulesCount

ObservabilityExportEnabled
```

---

## 63.1 Modules

La liste complète des modules peut être disponible dans un diagnostic protégé ou rapport de release.

Elle ne doit pas produire un log excessivement volumineux à chaque démarrage.

---

## 63.2 Secrets

Aucune valeur de configuration sensible n’est affichée.

---

# 64. Arrêt de l’application

L’application doit tenter un flush borné des signaux.

---

## 64.1 Timeout

L’arrêt ne doit pas rester bloqué indéfiniment pour vider les buffers.

---

## 64.2 Perte possible

Une petite perte de télémétrie lors d’un arrêt brutal est acceptable.

Elle ne doit pas être compensée par un buffer local non borné.

---

# 65. Tests unitaires

Tester notamment :

- templates de logs ;
- codes d’erreur ;
- attributs autorisés ;
- absence de données sensibles ;
- noms de métriques ;
- labels ;
- évaluateurs de cardinalité ;
- configuration ;
- valeur par défaut.

---

# 66. Tests d’intégration

Vérifier :

```text
requête HTTP
→ trace créée
→ logs corrélés
→ span PostgreSQL
→ résultat observable
```

---

## 66.1 Exporteur de test

Un exporteur en mémoire ou endpoint de test peut collecter :

- spans ;
- métriques ;
- logs ;

sans appeler un backend réel.

---

## 66.2 OTLP

Un test d’intégration peut utiliser un collector local ou endpoint OTLP de test.

---

# 67. Tests de corrélation

Vérifier que :

- le Trace ID est présent dans les logs d’une requête ;
- un appel HTTP sortant propage le contexte ;
- un job conserve un Correlation ID adapté ;
- une erreur peut être retrouvée entre plusieurs signaux.

---

# 68. Tests de données sensibles

Les tests doivent injecter des valeurs ressemblant à :

- email ;
- token ;
- mot de passe ;
- chaîne de connexion ;
- message de formulaire.

Le résultat exporté ne doit pas contenir ces valeurs.

---

## 68.1 Snapshots

Des snapshots ou assertions peuvent inspecter les propriétés exportées.

---

# 69. Tests de cardinalité

Vérifier que :

- les routes sont normalisées ;
- les IDs ne sont pas utilisés comme labels ;
- les messages d’exception ne sont pas des dimensions ;
- les labels autorisés utilisent des valeurs bornées.

---

# 70. Tests de panne de backend

Simuler :

```text
Endpoint OTLP indisponible

Timeout

Erreur d’authentification

Collector arrêté

Connexion interrompue

Buffer saturé
```

L’application doit :

- continuer son fonctionnement métier ;
- respecter les timeouts ;
- limiter les retries ;
- produire un diagnostic local borné ;
- ne pas saturer la mémoire.

---

# 71. Tests de performance

Mesurer l’impact de l’instrumentation sur :

- latence ;
- allocations ;
- CPU ;
- mémoire ;
- démarrage ;
- opérations fréquentes.

---

## 71.1 Désactivation

La désactivation d’un signal doit réduire son coût sans modifier le comportement fonctionnel.

---

# 72. Tests des health checks

Vérifier :

- liveness sans PostgreSQL ;
- readiness avec PostgreSQL indisponible ;
- réponse publique minimale ;
- diagnostic protégé ;
- absence de secret ;
- absence de boucle de redémarrage.

---

# 73. Tests des modules

Chaque module ajoutant des signaux doit vérifier :

- noms ;
- attributs ;
- cardinalité ;
- données sensibles ;
- corrélation ;
- version ;
- coût.

---

# 74. Tests des customizations

Une customization d’intégration doit vérifier :

- span externe ;
- métrique de résultat ;
- timeout ;
- erreur ;
- kill switch ;
- absence de payload ;
- absence de secret.

---

# 75. Tests multiplateformes

Les tests essentiels s’exécutent sur :

```text
Windows x64

Linux x64
```

Ils vérifient notamment :

- console ;
- configuration ;
- Activity ;
- Meter ;
- timestamps UTC ;
- arrêt et flush.

---

# 76. Quality gates

Une contribution est bloquée lorsque :

```text
Log contenant un secret

Log contenant une PII non justifiée

Corps HTTP capturé

Sensitive data logging activé en production

Métrique avec label non borné

Span nommé avec un identifiant dynamique

SDK fournisseur utilisé dans Domain

Endpoint d’export non validé

Retry d’export illimité

Buffer non borné

Health check public trop détaillé

Exception logguée plusieurs fois sans justification

Signal sans propriétaire

Alerte sans runbook pour un incident critique
```

---

## 76.1 CI

La CI doit au minimum vérifier :

- build ;
- tests ;
- analyse statique ;
- secret scanning ;
- configuration ;
- tests d’observabilité ciblés ;
- absence de SDK non approuvé.

---

# 77. Règles d’analyse statique

Des règles peuvent interdire ou signaler :

```text
Console.WriteLine dans l’application web

String interpolation dans les logs structurés

Log d’une chaîne de connexion

SensitiveDataLogging hors tests locaux

Metric labels avec IDs

Usage direct d’un SDK fournisseur dans les modules

Capture du corps HTTP
```

Le CLI peut utiliser une sortie console adaptée à son interface utilisateur sans contourner les règles de journalisation interne.

---

# 78. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-OBS-001` | Configuration d’observabilité invalide |
| `SF-OBS-002` | Endpoint OTLP invalide |
| `SF-OBS-003` | Credential d’export manquant |
| `SF-OBS-004` | Exporteur indisponible |
| `SF-OBS-005` | Signal abandonné après saturation |
| `SF-OBS-006` | Attribut interdit |
| `SF-OBS-007` | Donnée sensible détectée |
| `SF-OBS-008` | Métrique à forte cardinalité |
| `SF-OBS-009` | Nom de span invalide |
| `SF-OBS-010` | Source d’activité non déclarée |
| `SF-OBS-011` | Meter non déclaré |
| `SF-OBS-012` | Contexte de trace invalide |
| `SF-OBS-013` | Sampling invalide |
| `SF-OBS-014` | Health check trop détaillé |
| `SF-OBS-015` | Backend non approuvé |
| `SF-OBS-016` | Retry d’export excessif |
| `SF-OBS-017` | Buffer d’export saturé |
| `SF-OBS-018` | SDK fournisseur hors adaptateur |
| `SF-OBS-019` | Signal sans propriétaire |
| `SF-OBS-020` | Rétention non définie |
| `SF-OBS-021` | Audit utilisé comme log technique |
| `SF-OBS-022` | Label métrique non borné |
| `SF-OBS-023` | Log dupliqué d’une exception |
| `SF-OBS-024` | Capture de payload interdite |
| `SF-OBS-025` | Corrélation absente sur une opération critique |

---

# 79. Observabilité de l’observabilité

La chaîne de collecte doit elle-même être surveillée.

Indicateurs possibles :

```text
exports réussis

exports échoués

signaux abandonnés

taille des files

temps d’export

collector disponible

authentification refusée

volume collecté
```

---

## 79.1 Boucle interdite

Les erreurs d’export ne doivent pas produire une boucle infinie de nouveaux signaux exportés.

---

# 80. Configuration générée

SiteFactory doit générer une baseline comprenant :

- enregistrement OpenTelemetry ;
- `ActivitySource` central ;
- `Meter` central ;
- enrichissement de ressource ;
- configuration typée ;
- logs structurés ;
- health checks ;
- tests ;
- variables d’environnement documentées.

---

## 80.1 Strategy

Les fichiers entièrement produits par SiteFactory utilisent la stratégie :

```text
generated
```

---

## 80.2 Extension client

Les customizations peuvent enregistrer leurs propres sources à travers un point d’extension public.

---

# 81. Paquets

Les packages d’observabilité doivent être :

- centralisés ;
- versionnés ;
- verrouillés ;
- scannés ;
- compatibles avec .NET 10 ;
- distribués selon l’ADR-0008.

---

## 81.1 Dépendances minimales

Un package de contrats métier ne dépend pas d’OpenTelemetry si ce n’est pas nécessaire.

---

## 81.2 Abstraction

Les abstractions SiteFactory ne doivent pas recréer une copie complète d’OpenTelemetry.

---

# 82. Sécurité

## 82.1 Actifs

- logs ;
- traces ;
- métriques ;
- audits ;
- credentials OTLP ;
- données de diagnostic ;
- informations d’infrastructure.

---

## 82.2 Menaces

- fuite de secrets ;
- fuite de PII ;
- endpoint falsifié ;
- interception ;
- injection de log ;
- cardinalité malveillante ;
- saturation ;
- accès inter-clients ;
- export vers un fournisseur non approuvé ;
- conservation excessive.

---

## 82.3 Contrôles

- minimisation ;
- redaction ;
- TLS ;
- credentials séparés ;
- allowlist ;
- buffers bornés ;
- sampling ;
- validation des attributs ;
- accès limité ;
- rétention ;
- tests ;
- audit des configurations.

---

# 83. Injection de log

Les entrées utilisateur pouvant contenir :

```text
retours à la ligne
séquences de contrôle
markup
```

ne doivent pas être concaténées directement dans des messages ou formats non structurés.

---

## 83.1 Valeur structurée

Une valeur non fiable reste une propriété structurée et bornée.

---

## 83.2 Interface de consultation

Le backend ou dashboard doit encoder correctement les valeurs affichées.

---

# 84. Performance

## 84.1 Hot path

Les opérations fréquentes doivent éviter :

- allocation excessive ;
- construction de message lorsque le niveau est désactivé ;
- activité inutile ;
- attributs volumineux ;
- labels dynamiques.

---

## 84.2 Source-generated logging

Le logging source-generated peut être utilisé pour les hot paths ou événements stables.

---

## 84.3 Sampling

Le sampling reste le levier principal de contrôle des traces.

---

## 84.4 Mesure

Toute optimisation doit être basée sur un benchmark ou une métrique.

---

# 85. Coûts

## 85.1 Coûts supplémentaires

- instrumentation ;
- backend ;
- stockage ;
- collector ;
- dashboards ;
- alertes ;
- maintenance ;
- rétention ;
- tests.

---

## 85.2 Coûts évités

- investigation SSH longue ;
- incidents non corrélés ;
- erreurs répétées ;
- sauvegardes silencieusement cassées ;
- dépendances lentes non identifiées ;
- debugging en production ;
- enfermement dans un fournisseur.

---

# 86. Maintenabilité

Pour chaque signal, il doit être possible de répondre :

```text
Quel composant le produit ?

À quelle question répond-il ?

Quelle est sa cardinalité ?

Contient-il une donnée sensible ?

Combien coûte-t-il ?

Quelle est sa rétention ?

Quel dashboard l’utilise ?

Quelle alerte en dépend ?

Peut-il être supprimé ?
```

---

## 86.1 Signal inutilisé

Un signal qui n’alimente :

- aucun diagnostic ;
- aucun dashboard ;
- aucune alerte ;
- aucune investigation ;

doit être réévalué.

---

# 87. Compatibilité

## 87.1 OpenTelemetry

Les APIs utilisées doivent rester compatibles avec les versions de packages approuvées.

---

## 87.2 Backend

Le backend peut évoluer sans modifier l’instrumentation métier.

---

## 87.3 Contrats de signaux

Les noms de métriques et attributs consommés par des dashboards constituent des contrats opérationnels.

Ils ne doivent pas être renommés silencieusement.

---

## 87.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, cette décision constituant la baseline.

Le remplacement d’une instrumentation propriétaire existante nécessite un plan de migration.

---

# 88. Dépréciation d’un signal

Un signal utilisé par un dashboard ou une alerte doit être déprécié avant suppression.

---

## 88.1 Transition

Une période peut produire :

- ancien signal ;
- nouveau signal ;

jusqu’à migration des consommateurs.

---

## 88.2 Suppression

La suppression doit mettre à jour :

- dashboards ;
- alertes ;
- runbooks ;
- tests ;
- documentation.

---

# 89. Migration depuis des logs texte

Processus :

```text
inventorier les logs
→ identifier les événements utiles
→ définir les propriétés
→ attribuer des codes
→ supprimer les données sensibles
→ ajouter la corrélation
→ ajouter traces et métriques nécessaires
→ mettre à jour dashboards et tests
```

---

## 89.1 Pas de conversion automatique complète

Tous les anciens messages ne doivent pas devenir des événements structurés.

Les logs inutiles doivent être supprimés.

---

## 89.2 Production

```text
Production execution: out of scope
```

Cet ADR ne déclenche aucune modification immédiate d’un environnement réel.

---

# 90. Migration depuis un SDK propriétaire

Processus :

1. inventorier l’usage ;
2. identifier les signaux ;
3. remplacer les appels par `ILogger`, `ActivitySource` et `Meter` ;
4. isoler l’export propriétaire ;
5. ajouter OTLP ;
6. comparer les résultats ;
7. migrer les dashboards ;
8. retirer le SDK des modules ;
9. vérifier la rétention ;
10. mettre à jour les runbooks.

---

# 91. Transfert client

Le handover doit inclure :

- configuration d’observabilité ;
- noms de services ;
- variables d’environnement ;
- liste des signaux importants ;
- dashboards exportables lorsque possible ;
- alertes ;
- runbooks ;
- credentials à recréer ;
- politique de rétention ;
- responsabilités.

---

## 91.1 Backend LogicGate

Le projet transféré ne doit pas dépendre irrévocablement d’un backend LogicGate.

---

## 91.2 Historique

Le transfert de l’historique d’observabilité dépend :

- du contrat ;
- du fournisseur ;
- de la rétention ;
- de la confidentialité ;
- des capacités d’export.

---

# 92. Règles pour LogicGate

LogicGate doit :

- définir les signaux nécessaires ;
- maintenir les conventions ;
- limiter la cardinalité ;
- protéger les données ;
- maintenir les dashboards ;
- relier les alertes aux runbooks ;
- surveiller les coûts ;
- tester les exports ;
- réexaminer la rétention.

---

## 92.1 LogicGate ne doit pas

- collecter des données par précaution sans usage ;
- utiliser les logs comme stockage métier ;
- ajouter des labels par utilisateur ;
- activer le sensitive data logging en production ;
- dépendre d’un backend propriétaire dans les modules ;
- conserver indéfiniment tous les signaux.

---

# 93. Règles pour Codex

Codex peut :

- ajouter des logs structurés ;
- créer des activités ;
- créer des métriques bornées ;
- ajouter des tests ;
- mettre à jour les conventions ;
- ajouter un dashboard ou un runbook documenté.

---

## 93.1 Codex ne doit pas

```text
Logguer un secret

Logguer le contenu d’un formulaire

Logguer une chaîne de connexion

Ajouter UserId ou LeadId comme label de métrique

Capturer tous les corps HTTP

Ajouter un SDK propriétaire dans Domain

Créer un span par méthode privée

Relogguer une exception à chaque couche

Activer SensitiveDataLogging en production

Ajouter un endpoint OTLP arbitraire

Créer une alerte sans runbook

Inventer une politique de rétention
```

---

## 93.2 Décision humaine

Une décision humaine est requise pour :

- nouveau backend ;
- nouvelle destination de données ;
- augmentation significative de rétention ;
- collecte d’une PII ;
- nouveau SDK fournisseur ;
- changement de sampling production important ;
- nouvelle alerte critique ;
- collector mutualisé entre clients.

---

# 94. Anti-patterns interdits

## 94.1 Logs concaténés

```csharp
logger.LogInformation("User: " + email);
```

---

## 94.2 Métrique par utilisateur

```text
requests{user_id="..."}
```

---

## 94.3 Span avec ID dans le nom

```text
GetLead-12345
```

---

## 94.4 Exception logguée trois fois

Infrastructure, Application et Web journalisent la même stack trace.

---

## 94.5 Corps complet

Le payload du formulaire est ajouté au span.

---

## 94.6 Health check détaillé public

La réponse expose le serveur PostgreSQL et l’exception.

---

## 94.7 Audit dans les logs

Une action sensible repose uniquement sur un message `Information`.

---

## 94.8 Export bloquant

La requête utilisateur attend que le backend de télémétrie réponde.

---

## 94.9 Retry illimité

L’exporteur tente indéfiniment de joindre un collector indisponible.

---

## 94.10 Tag libre

Le message d’exception ou une URL complète devient un label de métrique.

---

# 95. Quality gates architecturaux

Une contribution d’observabilité ne peut être acceptée si :

```text
Le signal n’a pas de propriétaire.

Le besoin n’est pas expliqué.

La donnée collectée n’est pas minimisée.

La cardinalité n’est pas bornée.

Une donnée personnelle est collectée sans décision.

Le backend est utilisé directement dans un module.

Le code métier dépend du transport OTLP.

Le signal n’est pas testé.

Le health check expose des détails sensibles.

L’export peut bloquer le cas d’usage.

La rétention ou destination est inconnue.
```

---

# 96. Conséquences positives

La décision apporte :

- diagnostic corrélé ;
- logs interrogeables ;
- traces ;
- métriques ;
- alertes fiables ;
- indépendance du fournisseur ;
- standardisation des modules ;
- meilleure exploitation ;
- transfert client facilité ;
- analyse de performance ;
- suivi des backups et migrations ;
- capacité de changement de backend ;
- réduction du debugging manuel.

---

# 97. Conséquences négatives

La décision entraîne :

- dépendances supplémentaires ;
- configuration ;
- collecte ;
- backend ;
- stockage ;
- sampling ;
- dashboards ;
- alertes ;
- tests ;
- gouvernance des signaux ;
- coût d’exploitation ;
- risque de surcollecte ;
- besoin de formation.

---

# 98. Risques acceptés

## 98.1 Perte de signaux

Une panne brutale peut perdre une partie de la télémétrie en mémoire.

Ce risque est accepté pour préserver la disponibilité métier.

---

## 98.2 Sampling

Toutes les traces de production ne seront pas conservées.

---

## 98.3 Backend indisponible

Une indisponibilité peut réduire les capacités de diagnostic sans interrompre le site.

---

## 98.4 Coût

Le coût peut augmenter avec le trafic.

Réduction :

- sampling ;
- rétention ;
- cardinalité ;
- budgets ;
- revue.

---

# 99. Mesures compensatoires

- console de secours ;
- OTLP ;
- collector possible ;
- buffers bornés ;
- retries bornés ;
- redaction ;
- tests ;
- conventions ;
- sampling ;
- budgets ;
- dashboards ;
- alertes ;
- runbooks ;
- audit séparé ;
- accès limités ;
- rétention définie.

---

# 100. Réversibilité

## 100.1 Classification

```text
élevée pour le backend

moyenne pour les noms de signaux devenus publics
```

---

## 100.2 Changement de backend

Le transport OTLP permet de remplacer le backend avec une modification de configuration ou d’adaptateur.

---

## 100.3 Suppression d’OpenTelemetry

Techniquement possible, mais elle réduirait fortement les garanties.

Un nouvel ADR serait nécessaire.

---

# 101. Plan d’implémentation

## Étape 1 — Conventions

Définir :

- noms de services ;
- ActivitySource ;
- Meter ;
- attributs ;
- niveaux ;
- données interdites.

---

## Étape 2 — Baseline .NET

Ajouter :

- `ILogger` ;
- OpenTelemetry ;
- instrumentation ASP.NET Core ;
- instrumentation HTTP ;
- instrumentation PostgreSQL adaptée.

---

## Étape 3 — Resource

Configurer :

- service ;
- version ;
- environnement ;
- client slug ;
- instance.

---

## Étape 4 — OTLP

Ajouter l’export configurable et sécurisé.

---

## Étape 5 — Métriques

Créer les métriques de référence :

- HTTP ;
- opérations ;
- erreurs ;
- backups ;
- migrations ;
- appels externes.

---

## Étape 6 — Traces métier

Instrumenter uniquement les opérations critiques.

---

## Étape 7 — Health checks

Ajouter :

- liveness ;
- readiness ;
- diagnostic protégé.

---

## Étape 8 — Sécurité

Ajouter :

- redaction ;
- tests de secrets ;
- limites de cardinalité ;
- restrictions HTTP.

---

## Étape 9 — Dashboards

Créer les premières vues opérationnelles.

---

## Étape 10 — Alertes et runbooks

Relier les alertes importantes aux procédures.

---

# 102. Tâches liées proposées

```text
SF-TASK-095 — Define SiteFactory observability conventions

SF-TASK-096 — Add OpenTelemetry resource configuration

SF-TASK-097 — Add ASP.NET Core and HTTP tracing

SF-TASK-098 — Add PostgreSQL observability instrumentation

SF-TASK-099 — Add provider-neutral OTLP export

SF-TASK-100 — Add SiteFactory operation metrics

SF-TASK-101 — Add structured error and correlation logging

SF-TASK-102 — Add liveness and readiness health checks

SF-TASK-103 — Add observability sensitive-data tests

SF-TASK-104 — Add metric cardinality validation

SF-TASK-105 — Add baseline operational dashboards

SF-TASK-106 — Add observability failure-mode tests
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 103. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Les logs sont structurés.

Une requête possède un Trace ID.

Les logs d’une requête sont corrélés à sa trace.

Un appel HTTP sortant propage le contexte.

PostgreSQL apparaît dans la trace sans données sensibles.

Les métriques utilisent des labels bornés.

Aucun UserId, LeadId ou TraceId n’est un label métrique.

Les corps HTTP ne sont pas capturés.

Les chaînes de connexion ne sont pas exportées.

Le sensitive data logging est désactivé en production.

L’endpoint OTLP est configurable.

Une panne OTLP ne bloque pas le site.

Les buffers et retries sont bornés.

Le liveness ne dépend pas directement de PostgreSQL.

Le readiness détecte PostgreSQL indisponible.

Le health check public ne révèle aucun détail sensible.

Les événements critiques ont un code stable.

Les modules utilisent des sources et meters déclarés.

Un backend peut être remplacé sans modifier Domain.

Les tests fonctionnent sur Windows et Linux.
```

---

# 104. Conditions de révision

Réexaminer cette décision si :

- SiteFactory adopte un backend obligatoire unique ;
- une contrainte réglementaire impose une collecte spécifique ;
- les clients doivent gérer eux-mêmes toute l’observabilité ;
- le volume rend OTLP ou le collector insuffisant ;
- des exigences temps réel nécessitent une autre architecture ;
- plusieurs instances distribuées deviennent la norme ;
- une architecture microservices est introduite ;
- les coûts dépassent les bénéfices ;
- la rétention doit être fortement augmentée ;
- une collecte de PII devient nécessaire ;
- l’audit doit offrir une garantie d’intégrité renforcée ;
- un SIEM central devient obligatoire.

---

# 105. Indicateurs à suivre

- volume de logs ;
- nombre de spans ;
- sampling effectif ;
- nombre de séries métriques ;
- cardinalité ;
- coût du backend ;
- durée de rétention ;
- exports échoués ;
- signaux abandonnés ;
- temps d’export ;
- alertes déclenchées ;
- alertes sans action ;
- incidents diagnostiqués ;
- temps moyen de résolution ;
- données sensibles détectées ;
- dashboards inutilisés ;
- health checks en échec.

---

# 106. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des secrets apparaissent dans les logs

des PII sont collectées sans justification

les coûts augmentent sans usage opérationnel

la cardinalité explose

le backend indisponible ralentit le site

les erreurs sont logguées plusieurs fois

les dashboards dépendent de messages texte fragiles

les alertes n’ont pas de runbook

les traces ne sont pas corrélées

les modules utilisent des SDK fournisseurs directement

le health endpoint expose l’infrastructure

la télémétrie devient indispensable au fonctionnement métier
```

---

# 107. Conséquences observées

Cette section est append-only après mise en œuvre.

## 107.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’intégration complète
d’OpenTelemetry dans le socle SiteFactory.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- signal ;
- backend ;
- volume ;
- coût ;
- incident ou bénéfice ;
- action proposée.

---

# 108. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](0019-audit-trail-and-sensitive-administrative-actions.md)
- `13-generation-pipeline.md`
- `15-cli-specification.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `21-observability-and-logging.md`
- `22-deployment-and-environments.md`
- `23-backup-and-restore.md`
- `24-operational-runbooks.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `27-client-handover-and-service-closure.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0003-postgresql-as-primary-database.md`
- `0004-explicit-production-migrations.md`
- `0006-feature-flags-and-entitlements-separation.md`
- `0007-module-composition-and-public-contracts.md`
- `0008-private-package-feed-and-supply-chain.md`
- `0009-client-customizations-and-extension-points.md`

---

# 109. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 104 |

---

# 110. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 111. Synthèse de la décision

```text
Logs
→ ILogger
→ structurés
→ propriétés stables
→ aucune donnée sensible

Traces
→ ActivitySource
→ W3C Trace Context
→ corrélation HTTP, PostgreSQL, jobs et appels externes

Métriques
→ Meter
→ labels bornés
→ aucune dimension utilisateur ou requête individuelle

Health
→ liveness
→ readiness
→ réponse publique minimale
→ diagnostic détaillé protégé

Export
→ OpenTelemetry
→ OTLP
→ collector ou backend remplaçable
→ TLS et credentials séparés

Résilience
→ batching
→ buffers bornés
→ retries bornés
→ panne d’export non bloquante

Gouvernance
→ minimisation
→ rétention
→ budgets
→ dashboards
→ alertes
→ runbooks

Architecture
→ aucun SDK fournisseur dans Domain
→ modules instrumentés avec les conventions SiteFactory
→ backend modifiable sans changer les cas d’usage
```

> L’observabilité SiteFactory doit permettre de comprendre rapidement le fonctionnement d’un projet sans transformer les données clientes en matière première de diagnostic ni rendre l’application dépendante d’un fournisseur particulier.
