# LogicGate SiteFactory — Observabilité et journalisation

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Observabilité et journalisation |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../02-architecture/core-module-boundary.md`, `09-module-architecture.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `13-generation-pipeline.md`, `15-cli-specification.md`, `18-security-baseline.md`, `19-testing-strategy.md`, `20-quality-gates-and-ci.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Périmètre livré et limites de preuve

Le public fournit des diagnostics CLI/startup bornés, des réponses health minimales,
et l'observabilité Activity de readiness : `sitefactory.readiness.refresh.completed`
et `sitefactory.readiness.http.decision`. Le
[harnais PostgreSQL public](../../tests/PostgreSql/README.md) conserve TRX admissibles,
preuves expurgées, métriques autorisées et observations Npgsql filtrées.

Les métriques de phases sont limitées à G04/P01/P02 ; l'Activity P03 est distincte.
Aucun contenu de formulaire, secret, connexion, SQL ou exception provider brute
ne doit devenir un log public. Les identifiants de corrélation sont des preuves,
pas des labels métriques à forte cardinalité.

P03-F1 reste **OPEN / UNKNOWN** : 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée ; risque accepté avec réserve et réexamen obligatoire
**2026-10-23**, sans prolongation tacite. Les campagnes 768/768 ultérieures
ne constituent pas une correction causale. CI distante non validée.

L'observabilité complète d'applications clientes (Identity, email, jobs, audit,
sauvegardes, alertes externes) ci-dessous est **une cible non livrée publiquement**.
Les opérations propres à des applications Business restent privées.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit la stratégie d’observabilité et de journalisation de LogicGate SiteFactory et des applications clientes générées.

Il précise notamment :

- les objectifs d’observabilité ;
- les responsabilités du moteur, des modules et des projets clients ;
- les logs structurés ;
- les niveaux de journalisation ;
- les événements techniques ;
- les identifiants de corrélation ;
- les traces distribuées ;
- les métriques ;
- les health checks ;
- les diagnostics opérateur ;
- la journalisation de la sécurité ;
- la séparation entre logs techniques et audit métier ;
- les données interdites ;
- la confidentialité ;
- la rétention ;
- la rotation des logs ;
- l’observabilité Docker, Caddy et PostgreSQL ;
- les alertes ;
- les procédures de diagnostic ;
- les tests ;
- les quality gates ;
- les contraintes applicables à Codex.

L’objectif est de permettre à LogicGate de comprendre rapidement :

```text
ce qui fonctionne
ce qui est dégradé
ce qui a échoué
quand l’échec s’est produit
quelle opération est concernée
quelle action corrective appliquer
```

sans exposer de secret ou de donnée personnelle.

---

## 2. Vision

L’observabilité doit être intégrée dès la génération du projet.

Une application cliente ne doit pas être livrée sans pouvoir répondre aux questions suivantes :

```text
L’application est-elle démarrée ?

Est-elle prête à recevoir du trafic ?

PostgreSQL est-il accessible ?

Le stockage persistant est-il disponible ?

Les formulaires sont-ils correctement enregistrés ?

Les emails sont-ils envoyés ?

Les sauvegardes réussissent-elles ?

Le disque approche-t-il de la saturation ?

Quelle version est actuellement déployée ?

Une erreur est-elle apparue après le dernier déploiement ?
```

Le flux de référence est :

```text
Événement
→ log structuré, métrique ou trace
→ collecte locale
→ diagnostic
→ alerte éventuelle
→ action opérateur
→ résolution
```

---

## 3. Principes fondamentaux

## 3.1 Observer sans exposer

L’observabilité ne doit pas devenir une source de fuite.

Les logs, métriques et traces ne doivent pas contenir :

- mot de passe ;
- token ;
- cookie ;
- clé privée ;
- chaîne de connexion ;
- corps complet de formulaire ;
- adresse email ;
- numéro de téléphone ;
- message de lead ;
- document ;
- contenu confidentiel ;
- prompt LLM complet ;
- réponse LLM complète.

---

## 3.2 Logs structurés

Les applications doivent produire des logs structurés.

Exemple conceptuel :

```json
{
  "timestampUtc": "2026-07-27T14:35:12.512Z",
  "level": "Information",
  "eventId": 4001,
  "eventName": "LeadCreated",
  "message": "A lead was created.",
  "application": "ExampleProject.Web",
  "version": "1.2.0",
  "environment": "Production",
  "moduleId": "lead-management",
  "correlationId": "7f50e9a8d6c54df6",
  "traceId": "0af7651916cd43dd8448eb211c80319c",
  "operationId": "79e1b584-7b72-41ed-932f-207d574e5a1f",
  "leadId": "f646d8c7-77dd-42db-a3df-03d88c15cf66"
}
```

Le log contient un identifiant technique.

Il ne contient pas les coordonnées du prospect.

---

## 3.3 Les logs utilisent des événements stables

Les événements significatifs possèdent :

- un identifiant numérique ou stable ;
- un nom ;
- un niveau ;
- une description ;
- des propriétés autorisées ;
- un module propriétaire.

Les messages textuels peuvent évoluer.

Les identifiants et propriétés structurantes doivent rester stables.

---

## 3.4 Les logs ne remplacent pas l’audit métier

Les logs techniques servent à :

- diagnostiquer ;
- surveiller ;
- comprendre une erreur ;
- analyser une indisponibilité.

L’audit métier sert à retracer des actions fonctionnelles importantes.

Exemple :

```text
Log technique :
LeadStatusUpdateCompleted

Audit métier :
Le statut du lead X a été modifié de New vers Qualified
par l’utilisateur technique Y.
```

---

## 3.5 L’application fonctionne sans fournisseur externe

Le MVP ne dépend pas obligatoirement :

- d’un SaaS de monitoring ;
- d’un collecteur OpenTelemetry ;
- de Grafana ;
- de Prometheus ;
- d’Application Insights ;
- d’Elastic ;
- d’un service de logs centralisé.

La baseline utilise :

- logs structurés ;
- stdout ;
- health checks ;
- rapports ;
- diagnostics opérateur.

Une centralisation pourra être ajoutée ultérieurement.

---

## 3.6 L’observabilité doit être exploitable

Un volume important de logs sans structure ni procédure ne constitue pas une bonne observabilité.

Chaque signal important doit répondre à un besoin :

```text
détecter
comprendre
corréler
agir
```

---

## 3.7 Pas de télémétrie externe par défaut

SiteFactory et les projets générés n’envoient aucune télémétrie externe par défaut.

Toute intégration future doit être :

- explicitement configurée ;
- documentée ;
- sécurisée ;
- limitée ;
- compatible avec la politique de confidentialité ;
- désactivable.

---

# 4. Non-objectifs du MVP

Le MVP ne cherche pas à fournir :

- un SOC ;
- un SIEM centralisé ;
- une plateforme d’APM complète ;
- des traces distribuées entre microservices ;
- une analyse comportementale avancée ;
- une détection automatique de fraude ;
- un système d’alerte multicanal complet ;
- des tableaux de bord commerciaux avancés ;
- une collecte centralisée multi-client contenant des données détaillées ;
- une rétention illimitée ;
- une supervision 24 heures sur 24 automatique ;
- un engagement de disponibilité contractuel.

Ces capacités peuvent être ajoutées selon l’offre de maintenance ou le niveau de criticité du projet.

---

# 5. Périmètre d’observabilité

La stratégie couvre :

```text
SiteFactory CLI

Moteur de génération

Catalogues et templates

Modules

Applications clientes

ASP.NET Core Identity

PostgreSQL

Caddy

Docker et Docker Compose

Scripts de backup

Scripts de restauration

CI/CD

Intégrations SMTP

Intégrations LLM éventuelles

Jobs d’arrière-plan
```

---

# 6. Les trois piliers

## 6.1 Logs

Ils décrivent des événements discrets.

Exemples :

```text
ApplicationStarted
LeadCreated
DatabaseUnavailable
EmailDeliveryFailed
BackupCompleted
```

---

## 6.2 Métriques

Elles fournissent des valeurs agrégées dans le temps.

Exemples :

```text
nombre de requêtes
durée des requêtes
taux d’erreur
nombre de formulaires enregistrés
durée des générations
dernier backup réussi
```

---

## 6.3 Traces

Elles relient les étapes d’une opération.

Exemple :

```text
requête formulaire
→ validation
→ persistance PostgreSQL
→ notification email
→ réponse HTTP
```

Le MVP doit préparer les identifiants de trace même si aucun collecteur externe n’est encore déployé.

---

# 7. Observabilité du moteur SiteFactory

## 7.1 Génération

Chaque génération possède :

```text
GenerationId
CorrelationId
StartedAtUtc
FinishedAtUtc
Status
GeneratorVersion
ConfigurationHash
```

---

## 7.2 Étapes observées

```text
ConfigurationLoaded
ConfigurationValidated
CatalogLoaded
PackResolved
ModulesResolved
TemplateResolved
ThemeResolved
GenerationPlanCreated
FilesGenerated
RestoreCompleted
BuildCompleted
TestsCompleted
SecurityChecksCompleted
GitInitialized
GenerationCompleted
```

---

## 7.3 Durées

Chaque étape significative doit pouvoir enregistrer :

```text
durationMilliseconds
```

---

## 7.4 Processus externes

Pour une commande telle que :

```text
dotnet build
```

les données observables incluent :

- exécutable ;
- arguments non sensibles ;
- répertoire de travail normalisé ;
- durée ;
- code de sortie ;
- statut ;
- timeout éventuel.

La sortie complète n’est pas copiée systématiquement dans le rapport.

---

## 7.5 Rapport de génération

Le fichier :

```text
generation-report.json
```

constitue le principal artefact d’observabilité du pipeline de génération.

Les logs console complètent ce rapport.

---

# 8. Observabilité des applications clientes

Chaque application cliente doit fournir au minimum :

```text
Logs structurés vers stdout

Correlation ID

Trace ID

Health checks

Version applicative

Environnement

Événements de démarrage et d’arrêt

Événements d’erreur

Diagnostics PostgreSQL

Diagnostics du stockage persistant

Diagnostics des opérations critiques
```

---

# 9. Infrastructure de journalisation .NET

## 9.1 Abstraction de référence

Le code utilise :

```text
Microsoft.Extensions.Logging
```

comme abstraction commune.

Les modules ne doivent pas dépendre directement d’un fournisseur de logs particulier.

---

## 9.2 Fournisseur

Le MVP peut utiliser le fournisseur console JSON fourni ou un fournisseur structuré approuvé.

L’ajout d’une bibliothèque structurante de journalisation doit être documenté.

---

## 9.3 Sortie de production

La production privilégie :

```text
JSON structuré vers stdout
```

Cette sortie peut être collectée par Docker.

---

## 9.4 Sortie de développement

Le développement peut utiliser une sortie console plus lisible.

Le comportement doit rester configurable.

---

## 9.5 Fichiers de logs applicatifs

L’application ne doit pas écrire par défaut ses propres fichiers de logs permanents dans `App_Data`.

Cette stratégie évite :

- duplication ;
- croissance disque non maîtrisée ;
- permissions supplémentaires ;
- confusion avec les logs Docker.

Un fichier dédié ne doit être ajouté que pour un besoin démontré.

---

# 10. Schéma commun d’un log

Propriétés communes recommandées :

```text
TimestampUtc
Level
EventId
EventName
Message
Application
ApplicationVersion
Environment
ModuleId
CorrelationId
TraceId
SpanId
OperationId
ErrorCode
Result
DurationMilliseconds
```

Propriétés optionnelles :

```text
RouteTemplate
HttpMethod
StatusCode
DependencyName
CommandName
GenerationId
EntityTechnicalId
UserTechnicalId
```

---

## 10.1 Propriétés interdites

```text
RequestBody
ResponseBody
Password
PasswordHash
Token
Cookie
AuthorizationHeader
ConnectionString
Email
Phone
LeadMessage
DocumentContent
PrivateKey
PromptContent
LlmResponseContent
```

---

# 11. Horodatage

## 11.1 UTC

Les logs utilisent des dates UTC.

Format recommandé :

```text
ISO 8601
```

Exemple :

```text
2026-07-27T14:35:12.512Z
```

---

## 11.2 Horloge

Les composants utilisent l’abstraction d’horloge prévue par l’architecture.

Cela facilite :

- cohérence ;
- tests ;
- génération de rapports ;
- corrélation.

---

# 12. Niveaux de logs

## 12.1 `Trace`

Utilisé pour des détails très fins de développement.

Il est désactivé par défaut en production.

Exemples :

```text
Étape interne d’un algorithme
Détail d’une résolution de catalogue
```

---

## 12.2 `Debug`

Utilisé pour le diagnostic local.

Il est désactivé par défaut en production.

Exemples :

```text
Nombre de modules candidats
Résultat d’une opération de cache
Configuration normalisée non sensible
```

---

## 12.3 `Information`

Utilisé pour les événements normaux significatifs.

Exemples :

```text
ApplicationStarted
LeadCreated
ContentPublished
BackupCompleted
GenerationCompleted
```

---

## 12.4 `Warning`

Utilisé pour une situation anormale mais contrôlée.

Exemples :

```text
EmailDeliveryFailed après persistance du lead
DiskSpaceWarningReached
FeatureDisabledByKillSwitch
InvalidLoginAttempt
DeprecatedConfigurationUsed
```

---

## 12.5 `Error`

Utilisé lorsqu’une opération attendue échoue.

Exemples :

```text
DatabaseOperationFailed
BackupFailed
RestoreFailed
GenerationFailed
UnhandledRequestFailure
```

---

## 12.6 `Critical`

Utilisé lorsqu’une situation compromet fortement :

- disponibilité ;
- intégrité ;
- sécurité ;
- restauration.

Exemples :

```text
ApplicationCannotStart
DatabaseCorruptionSuspected
NoWritablePersistentStorage
CriticalSecretConfigurationMissing
```

---

## 12.7 Utilisation cohérente

Un comportement attendu ne doit pas être journalisé comme une erreur.

Exemple :

```text
Permission refusée
```

peut être `Information` ou `Warning` selon le contexte.

Une ressource publique inconnue retournant `404` ne doit pas produire automatiquement une erreur.

---

# 13. Configuration des niveaux

Exemple conceptuel :

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System.Net.Http.HttpClient": "Warning"
    }
  }
}
```

Les niveaux peuvent être surchargés par environnement.

---

## 13.1 Production

La production utilise par défaut :

```text
Information
```

avec des overrides réduisant le bruit des frameworks.

---

## 13.2 Diagnostic temporaire

Un niveau plus détaillé peut être activé temporairement lors d’un incident.

Il doit :

- être limité ;
- être surveillé ;
- être remis à son niveau normal ;
- ne pas activer la journalisation de données sensibles.

---

## 13.3 Interdiction

Les options suivantes sont interdites en production :

```text
EF Core EnableSensitiveDataLogging

Journalisation systématique des corps de requêtes

Journalisation des cookies

Journalisation des headers Authorization
```

---

# 14. Message templates

Les logs structurés utilisent des message templates.

Exemple recommandé :

```csharp
logger.LogInformation(
    EventIds.LeadCreated,
    "Lead {LeadId} was created.",
    lead.Id);
```

Exemple à éviter :

```csharp
logger.LogInformation(
    $"Lead {lead.Id} was created.");
```

Le premier format préserve une propriété structurée exploitable.

---

# 15. Catalogue des événements

## 15.1 Ranges recommandés

| Plage | Domaine |
|---:|---|
| `1000–1999` | Plateforme et application |
| `2000–2999` | Identity et autorisation |
| `3000–3999` | Content et publication |
| `4000–4999` | Leads et formulaires |
| `5000–5999` | Media et fichiers |
| `6000–6999` | SEO et rendu public |
| `7000–7999` | SiteFactory et génération |
| `8000–8999` | Operations, backups et sécurité |
| `9000–9999` | Dépendances externes |

---

## 15.2 Exemple de registre

```csharp
public static class LeadEventIds
{
    public static readonly EventId LeadCreated =
        new(4001, nameof(LeadCreated));

    public static readonly EventId LeadStatusChanged =
        new(4002, nameof(LeadStatusChanged));

    public static readonly EventId LeadExportCompleted =
        new(4010, nameof(LeadExportCompleted));

    public static readonly EventId LeadExportFailed =
        new(4011, nameof(LeadExportFailed));
}
```

---

## 15.3 Unicité

Les identifiants doivent rester uniques dans leur plage.

Les modules sont responsables de leur registre.

---

## 15.4 Documentation

Chaque module doit documenter ses principaux événements.

---

# 16. Correlation ID

## 16.1 Objectif

Le correlation ID permet de suivre une opération à travers plusieurs logs.

---

## 16.2 Requêtes HTTP

Chaque requête reçoit un correlation ID.

Ordre recommandé :

```text
header entrant valide
→ réutilisation

header absent ou invalide
→ génération locale
```

---

## 16.3 Validation

Un identifiant reçu depuis Internet doit être validé sur :

- longueur ;
- caractères ;
- format.

Une valeur arbitrairement longue ou contenant des caractères de contrôle est rejetée ou remplacée.

---

## 16.4 Header

Nom possible :

```text
X-Correlation-ID
```

Le nom définitif doit être centralisé.

---

## 16.5 Réponse

Le correlation ID peut être retourné dans la réponse afin de faciliter le support.

Il ne constitue pas une information secrète.

---

## 16.6 Logs

Tous les logs produits pendant la requête doivent inclure le même correlation ID.

---

# 17. Trace ID et Activity

## 17.1 Standard

Les applications doivent utiliser le contexte de trace compatible avec :

```text
W3C Trace Context
```

---

## 17.2 ASP.NET Core

Le `TraceId` de l’activité ASP.NET Core doit être disponible dans les logs.

---

## 17.3 Appels sortants

Les appels réalisés via `HttpClient` doivent propager le contexte de trace lorsque le fournisseur externe l’accepte.

---

## 17.4 Jobs

Un job sans requête HTTP crée sa propre activité et son propre `OperationId`.

---

## 17.5 SMTP

Une opération d’envoi peut conserver le même `OperationId` que le traitement principal sans transmettre de donnée personnelle.

---

# 18. Operation ID

L’`OperationId` représente une opération métier ou technique identifiable.

Exemples :

```text
Soumission d’un formulaire

Génération SiteFactory

Backup

Restore

Export

Publication

Migration
```

Il peut être représenté par un GUID.

---

# 19. Scopes de logs

Les scopes permettent d’ajouter des propriétés communes à une opération.

Exemple conceptuel :

```csharp
using var scope = logger.BeginScope(
    new Dictionary<string, object?>
    {
        ["ModuleId"] = "lead-management",
        ["OperationId"] = operationId,
        ["CorrelationId"] = correlationId
    });
```

Les scopes ne doivent pas contenir de données personnelles.

---

# 20. Journalisation HTTP

## 20.1 Données autorisées

```text
Route template
Méthode HTTP
Status code
Durée
Trace ID
Correlation ID
Taille approximative éventuelle
```

---

## 20.2 Route template

Utiliser :

```text
/admin/leads/{id}
```

plutôt que :

```text
/admin/leads/f646d8c7-77dd-42db-a3df-03d88c15cf66
```

lorsque cela est possible.

---

## 20.3 Query string

La query string complète ne doit pas être logguée par défaut.

Elle peut contenir :

- email ;
- token ;
- filtre sensible ;
- identifiant externe ;
- données de tracking.

---

## 20.4 Headers

Les headers ne sont pas journalisés globalement.

Une liste autorisée très limitée peut être utilisée pour :

- correlation ID ;
- traceparent ;
- host technique ;
- content type.

---

## 20.5 Corps des requêtes

Le corps des requêtes n’est pas loggué en production.

---

## 20.6 Réponses

Le corps des réponses n’est pas loggué.

---

# 21. Gestion des exceptions

## 21.1 Middleware central

Les exceptions non gérées doivent être traitées par un middleware central.

---

## 21.2 Réponse publique

La réponse contient :

- message générique ;
- code d’erreur adapté ;
- correlation ID éventuel.

Elle ne contient pas :

- stack trace ;
- exception interne ;
- chemin ;
- SQL ;
- configuration.

---

## 21.3 Log interne

Le log peut contenir :

- type d’exception ;
- stack trace ;
- route template ;
- correlation ID ;
- opération ;
- code d’erreur.

Il ne doit pas contenir les données sensibles ayant provoqué l’erreur.

---

## 21.4 Éviter les doubles logs

Une exception ne doit pas être journalisée comme `Error` dans chaque couche.

Approche recommandée :

```text
couche métier
→ retourne une erreur métier structurée

frontière technique
→ journalise une seule fois si nécessaire
```

---

## 21.5 Exceptions attendues

Les erreurs de validation ou d’autorisation ne doivent pas être systématiquement représentées par une exception.

---

# 22. Journalisation EF Core et PostgreSQL

## 22.1 Production

Les logs EF Core sont limités à un niveau utile.

---

## 22.2 Données sensibles

L’option :

```csharp
EnableSensitiveDataLogging()
```

est interdite en production.

---

## 22.3 SQL

Les requêtes SQL complètes ne sont pas journalisées par défaut en production.

Elles pourraient contenir :

- données ;
- paramètres ;
- structures internes.

---

## 22.4 Événements utiles

```text
DatabaseConnectionFailed
DatabaseCommandTimeout
DatabaseMigrationRequired
DatabaseUnavailable
DatabaseRecovered
```

---

## 22.5 Durée des requêtes

Les requêtes anormalement lentes peuvent être signalées sans enregistrer leurs paramètres sensibles.

---

# 23. Journalisation Identity

Événements possibles :

```text
LoginSucceeded
LoginFailed
AccountLocked
AccountDisabled
LogoutSucceeded
PasswordResetRequested
AuthorizationDenied
RoleAssigned
RoleRemoved
```

---

## 23.1 Données autorisées

```text
UserTechnicalId
ReasonCode
RequiredPermission
RouteTemplate
CorrelationId
```

---

## 23.2 Données interdites

```text
Email
Mot de passe
Token de reset
Cookie
Claims complets
Adresse IP sans finalité validée
```

---

## 23.3 Tentatives de connexion

Un échec de login ne doit pas révéler dans le log public ou l’interface :

- si le compte existe ;
- le mot de passe attendu ;
- l’état détaillé du compte.

---

# 24. Journalisation des formulaires publics

Événements possibles :

```text
PublicFormSubmissionAccepted
PublicFormSubmissionRejected
LeadPersisted
LeadPersistenceFailed
NotificationEmailQueued
NotificationEmailFailed
RateLimitExceeded
HoneypotTriggered
```

---

## 24.1 Données autorisées

```text
FormId
LeadId après persistance
ValidationErrorCount
Result
Duration
CorrelationId
```

---

## 24.2 Données interdites

```text
Nom
Email
Téléphone
Message
Entreprise
Pièce jointe
Contenu complet des champs
```

---

# 25. Journalisation des emails

Événements possibles :

```text
EmailSendStarted
EmailSendCompleted
EmailSendFailed
EmailRetryScheduled
```

---

## 25.1 Données autorisées

```text
EmailTemplateId
ProviderId
OperationId
AttemptNumber
Result
Duration
```

---

## 25.2 Données interdites

```text
Destinataire
Expéditeur personnel
Sujet contenant une PII
Corps
Credentials SMTP
```

---

# 26. Journalisation des fichiers et médias

Événements possibles :

```text
MediaUploadAccepted
MediaUploadRejected
MediaStored
MediaDeleted
FileTypeRejected
FileSizeExceeded
```

---

## 26.1 Données autorisées

```text
MediaId
FileSize
ValidatedMediaType
StorageProvider
ErrorCode
```

---

## 26.2 Données interdites

```text
Nom original complet si sensible
Chemin système complet
Contenu du fichier
Données EXIF personnelles
```

---

# 27. Journalisation des backups

Événements obligatoires :

```text
BackupStarted
PostgreSqlBackupCompleted
AppDataBackupCompleted
BackupCompleted
BackupFailed
BackupRetentionApplied
```

---

## 27.1 Propriétés

```text
BackupId
BackupType
StartedAtUtc
Duration
Result
FileCount
TotalSizeBytes
RetentionDeletedCount
```

---

## 27.2 Données interdites

```text
Chemin distant contenant un credential
Mot de passe PostgreSQL
Contenu SQL
Nom de fichier contenant une donnée personnelle
```

---

# 28. Journalisation des restaurations

Événements :

```text
RestorePlanCreated
RestoreConfirmationRequired
RestoreStarted
PostgreSqlRestoreCompleted
AppDataRestoreCompleted
RestoreValidationCompleted
RestoreFailed
```

Une restauration doit être fortement traçable.

Elle ne doit jamais être lancée silencieusement.

---

# 29. Audit métier

## 29.1 Actions concernées

Les actions suivantes peuvent nécessiter un audit :

```text
Utilisateur créé

Utilisateur désactivé

Rôle attribué

Permission modifiée

Contenu publié

Lead exporté

Lead supprimé ou anonymisé

Automatisation activée

Module désactivé

Configuration critique modifiée
```

---

## 29.2 Modèle conceptuel

```text
AuditEventId
EventType
ActorUserId
TargetType
TargetId
OccurredAtUtc
Result
ReasonCode
CorrelationId
```

---

## 29.3 Contenu

L’audit ne doit pas recopier l’intégralité des données avant et après modification.

Il peut conserver des changements structurés limités lorsque cela est nécessaire.

---

## 29.4 Accès

L’audit nécessite une permission adaptée.

Exemple :

```text
Operations.ReadAudit
```

---

## 29.5 Immutabilité

Les événements d’audit ne doivent pas être modifiables par l’utilisateur fonctionnel normal.

---

# 30. Métriques

## 30.1 Statut du MVP

Les contrats de métriques doivent être prévus.

L’export vers une plateforme dédiée peut être ajouté après stabilisation.

---

## 30.2 Types de métriques

```text
Counter
Histogram
Observable gauge
```

---

## 30.3 Métriques HTTP

Exemples :

```text
http.server.requests.total
http.server.request.duration
http.server.active_requests
http.server.errors.total
```

---

## 30.4 Métriques applicatives

Exemples :

```text
sitefactory.public_form.submissions.total
sitefactory.leads.persisted.total
sitefactory.email.sends.total
sitefactory.email.failures.total
sitefactory.content.publications.total
sitefactory.security.rate_limits.total
```

---

## 30.5 Métriques du générateur

```text
sitefactory.generation.total
sitefactory.generation.duration
sitefactory.generation.files.created
sitefactory.generation.files.conflicts
sitefactory.build.failures.total
sitefactory.tests.failures.total
```

---

## 30.6 Métriques opérationnelles

```text
sitefactory.backup.last_success_timestamp
sitefactory.backup.failures.total
sitefactory.disk.free_bytes
sitefactory.restore.last_validation_timestamp
sitefactory.application.restarts.total
```

---

# 31. Cardinalité des métriques

## 31.1 Principe

Les labels doivent posséder un nombre limité de valeurs possibles.

---

## 31.2 Labels autorisés

Exemples :

```text
module
route_template
http_method
status_code_group
result
environment
provider
```

---

## 31.3 Labels interdits

```text
email
user_id
lead_id
correlation_id
trace_id
raw_url
file_name
message
exception_message
```

Ces valeurs créeraient une cardinalité excessive ou une fuite.

---

## 31.4 Client

Dans une application isolée par client, l’identité du projet peut être définie comme attribut de ressource plutôt que répétée comme label sur chaque métrique.

---

# 32. Traces

## 32.1 Instrumentation initiale

Le code doit pouvoir instrumenter :

- requêtes HTTP entrantes ;
- appels HTTP sortants ;
- opérations PostgreSQL ;
- traitements de formulaires ;
- emails ;
- jobs ;
- génération SiteFactory.

---

## 32.2 `ActivitySource`

Les modules peuvent exposer un `ActivitySource` stable.

Exemple conceptuel :

```csharp
public static readonly ActivitySource ActivitySource =
    new("LogicGate.SiteFactory.Module.Leads");
```

---

## 32.3 Noms de spans

Exemples :

```text
leads.create
leads.update-status
email.send
backup.postgresql
sitefactory.generate
sitefactory.build
```

---

## 32.4 Attributs

Les attributs doivent rester sobres.

Exemples autorisés :

```text
module.id
operation.result
error.code
db.system
email.template_id
```

---

## 32.5 Attributs interdits

```text
lead.email
lead.message
user.password
db.statement avec valeurs
http.request.body
```

---

# 33. Export OpenTelemetry futur

Une intégration OpenTelemetry pourra fournir :

- traces ;
- métriques ;
- logs ;
- contexte commun.

Elle n’est pas obligatoire dans le premier incrément.

Son ajout devra définir :

- collector ;
- exporter ;
- endpoint ;
- authentification ;
- échantillonnage ;
- rétention ;
- coûts ;
- confidentialité ;
- disponibilité.

---

## 33.1 Aucun exporter implicite

La présence des bibliothèques d’instrumentation ne doit pas envoyer de données vers un service externe sans configuration explicite.

---

## 33.2 Échantillonnage

Une stratégie d’échantillonnage pourra être nécessaire pour limiter :

- volume ;
- coût ;
- stockage.

Les erreurs importantes peuvent nécessiter une conservation renforcée.

---

# 34. Health checks

## 34.1 Objectifs

Les health checks doivent distinguer :

```text
Le processus est-il vivant ?

L’application est-elle prête ?

Les dépendances essentielles fonctionnent-elles ?
```

---

## 34.2 Liveness

Route recommandée :

```text
/health/live
```

Le liveness vérifie uniquement que le processus applicatif fonctionne.

Il ne doit pas échouer uniquement parce que PostgreSQL est temporairement indisponible.

---

## 34.3 Readiness

Route recommandée :

```text
/health/ready
```

Le readiness peut vérifier :

- PostgreSQL ;
- App_Data ;
- clés Data Protection ;
- migration attendue ;
- configuration essentielle.

---

## 34.4 Health public simplifié

Une route :

```text
/health
```

peut retourner un statut minimal.

Exemple :

```json
{
  "status": "Healthy"
}
```

---

## 34.5 Diagnostics détaillés

Une route ou page protégée peut fournir des détails supplémentaires.

Exemple :

```text
/admin/operations/health
```

Permission :

```text
Operations.ReadDiagnostics
```

---

# 35. Contenu des health checks

## 35.1 Données publiques autorisées

```text
status global
```

Éventuellement :

```text
version publique
```

si cela est jugé sans risque.

---

## 35.2 Données publiques interdites

```text
nom du serveur
nom de la base
connection string
utilisateur PostgreSQL
chemin du volume
exception
stack trace
modules internes détaillés
```

---

## 35.3 Diagnostic protégé

Le diagnostic protégé peut afficher :

- version ;
- commit ;
- environnement ;
- modules ;
- statut PostgreSQL ;
- statut App_Data ;
- dernier backup connu ;
- espace disque ;
- heure de démarrage ;
- durée de fonctionnement.

Il ne contient toujours aucun secret.

---

# 36. Health checks des dépendances

## 36.1 PostgreSQL

Le check exécute une opération simple et rapide.

---

## 36.2 App_Data

Le check peut vérifier :

- présence ;
- accès ;
- possibilité d’écriture contrôlée.

Il ne doit pas écrire un fichier permanent à chaque appel.

---

## 36.3 Data Protection

Le check peut vérifier que le dossier attendu est accessible et persistant.

---

## 36.4 SMTP

SMTP ne doit être considéré comme une dépendance de readiness que si l’application ne peut réellement pas fonctionner sans lui.

Pour un formulaire où la persistance est prioritaire :

```text
SMTP indisponible
→ application prête mais dégradée
```

---

## 36.5 LLM

Un provider LLM optionnel indisponible ne doit pas rendre l’application globalement indisponible.

---

## 36.6 Backups

L’état du dernier backup peut être présenté comme diagnostic ou alerte.

Il ne doit pas rendre le processus non vivant.

---

# 37. Tags de health checks

Tags conceptuels :

```text
live
ready
database
storage
external
optional
security
```

Ils permettent de sélectionner les checks selon la route.

---

# 38. Docker health check

Le conteneur applicatif doit utiliser de préférence :

```text
/health/live
```

afin d’éviter une boucle de redémarrage lorsque PostgreSQL est momentanément indisponible.

Le readiness est vérifié séparément par :

- runbook ;
- smoke test ;
- supervision ;
- futur orchestrateur.

---

# 39. Caddy et health checks

Caddy peut continuer à recevoir les requêtes pendant une dégradation.

Les stratégies futures de routage selon le readiness devront être testées avant activation.

Dans un déploiement avec une seule instance, retirer automatiquement l’unique backend peut rendre le diagnostic plus difficile.

---

# 40. Version et informations de build

Chaque application doit pouvoir connaître :

```text
ApplicationName
ApplicationVersion
CommitSha
BuildTimestampUtc éventuel
GeneratorVersion
CatalogVersion
```

---

## 40.1 Endpoint

Une route protégée ou un diagnostic peut exposer ces informations.

Une route publique dédiée n’est pas obligatoire.

---

## 40.2 Logs de démarrage

Exemple :

```text
ApplicationStarted

ApplicationVersion = 1.2.0
CommitSha = abc1234
Environment = Production
```

---

## 40.3 Secrets

Les informations de build ne doivent pas contenir :

- chemin local ;
- nom d’utilisateur de build ;
- token ;
- URL privée avec credentials.

---

# 41. Événements de démarrage

Événements attendus :

```text
ApplicationStarting
ConfigurationValidated
DatabaseConfigurationValidated
ModulesRegistered
ApplicationStarted
ApplicationStopping
ApplicationStopped
ApplicationStartupFailed
```

---

## 41.1 Informations autorisées

```text
version
environment
module count
port interne
profile de déploiement
```

---

## 41.2 Informations interdites

```text
connection string
variables d’environnement complètes
mots de passe
contenu des secrets
```

---

# 42. État des migrations

L’application doit pouvoir détecter :

- migration attendue ;
- migration en retard ;
- modèle incompatible.

Elle ne doit pas appliquer automatiquement la migration en production.

Événement possible :

```text
DatabaseMigrationRequired
```

Le health readiness peut devenir dégradé ou unhealthy selon l’impact.

---

# 43. Observabilité Docker

## 43.1 Logs applicatifs

Les logs de l’application sont écrits vers stdout et stderr.

Docker les collecte.

---

## 43.2 Rotation

La configuration Docker doit limiter la croissance des logs.

Exemple conceptuel :

```text
max-size
max-file
```

Les valeurs exactes dépendent du projet et du VPS.

---

## 43.3 Événements utiles

```text
ContainerStarted
ContainerRestarted
ContainerUnhealthy
ContainerExited
```

Ces informations peuvent être consultées via Docker et les runbooks.

---

## 43.4 Redémarrages répétés

Une boucle de redémarrage doit déclencher une investigation.

Elle ne doit pas être masquée par une politique de restart permanente.

---

# 44. Observabilité Caddy

Caddy peut produire :

- logs d’accès ;
- logs d’erreur ;
- événements de certificat ;
- erreurs de reverse proxy.

---

## 44.1 Logs d’accès

Les logs d’accès doivent être configurés avec prudence.

Ils peuvent contenir :

- adresse IP ;
- URL ;
- user agent ;
- paramètres.

---

## 44.2 Query strings

Les query strings sensibles doivent être évitées ou filtrées.

Les tokens ne doivent pas être placés dans les URLs.

---

## 44.3 Rétention

La rétention des logs Caddy doit être limitée et documentée.

---

## 44.4 Certificats

Les erreurs de renouvellement de certificat sont des événements opérationnels importants.

---

# 45. Observabilité PostgreSQL

Les signaux importants incluent :

```text
Disponibilité
Connexions
Espace disque
Temps de requête
Verrous
Erreurs
Backups
Croissance de la base
```

---

## 45.1 Logs PostgreSQL

Les logs PostgreSQL ne doivent pas enregistrer systématiquement :

- paramètres ;
- valeurs de requêtes ;
- données personnelles.

---

## 45.2 Connexions

Un nombre anormal de connexions ou des timeouts répétés doit être diagnostiqué.

---

## 45.3 Requêtes lentes

Une analyse ciblée peut être activée lors d’un incident.

Elle ne doit pas exposer les paramètres applicatifs.

---

# 46. Observabilité des scripts

Les scripts d’exploitation doivent produire :

- début ;
- résultat ;
- durée ;
- code de sortie ;
- identifiant d’opération ;
- fichier produit sans secret ;
- nombre d’éléments concernés.

---

## 46.1 Format

Une sortie humaine concise est obligatoire.

Une sortie JSON peut être ajoutée pour l’automatisation.

---

## 46.2 Erreur

Un script échoué doit retourner un code non nul.

---

## 46.3 Secrets

Les commandes ne doivent pas être affichées lorsqu’elles contiennent un secret.

---

# 47. Observabilité CI/CD

Les workflows doivent enregistrer :

- commit ;
- branche ;
- version ;
- plateforme ;
- durée ;
- nombre de tests ;
- résultat des gates ;
- artefacts.

---

## 47.1 Données interdites

Les logs CI ne contiennent pas :

- secrets ;
- environnement complet ;
- configuration client réelle ;
- données de production ;
- fichiers `.env`.

---

## 47.2 Résumés

Les job summaries doivent permettre d’identifier rapidement :

```text
build
tests
catalogues
schémas
projet généré
sécurité
Docker
```

---

# 48. Alertes

## 48.1 Statut MVP

Le MVP prépare les signaux et les runbooks.

La plateforme d’alerte peut être ajoutée selon le service d’exploitation retenu.

---

## 48.2 Alertes critiques

Exemples :

```text
Application indisponible

PostgreSQL indisponible durablement

Backup en échec répété

Disque au seuil critique

Certificat non renouvelé

Boucle de redémarrage

Secret de configuration essentiel absent
```

---

## 48.3 Alertes warning

Exemples :

```text
Taux d’erreur en hausse

SMTP en échec répété

Disque au seuil warning

Nombre élevé de rate limits

Tentatives de login anormales

Backup plus ancien que la durée attendue

Latence élevée
```

---

## 48.4 Pas d’alerte par événement isolé normal

Une simple :

```text
404
connexion invalide
email temporairement échoué
```

ne doit pas déclencher automatiquement une alerte critique.

Les alertes doivent utiliser :

- durée ;
- fréquence ;
- seuil ;
- impact.

---

# 49. Seuils d’espace disque

Configuration conceptuelle déjà prévue :

```yaml
operations:
  checks:
    diskWarningPercent: 80
    diskCriticalPercent: 90
```

Les seuils doivent être adaptés au volume et à la croissance du projet.

---

## 49.1 Événements

```text
DiskSpaceWarningReached
DiskSpaceCriticalReached
DiskSpaceRecovered
```

---

## 49.2 Volume concerné

Le diagnostic doit distinguer :

- filesystem système ;
- volume PostgreSQL ;
- App_Data ;
- backups ;
- logs.

---

# 50. Indicateurs de service

Indicateurs internes possibles :

```text
Disponibilité HTTP

Taux de réponses 5xx

Durée des requêtes

Taux de succès des formulaires

Taux de succès des emails

Âge du dernier backup réussi

Taux d’échec des jobs

Temps de génération SiteFactory
```

---

## 50.1 Pas de SLO contractuel implicite

La présence d’un indicateur ne crée pas automatiquement un engagement contractuel.

Les SLO ou SLA doivent être définis séparément.

---

## 50.2 Baseline

Les seuils précis doivent être définis après observation d’un projet pilote.

---

# 51. Diagnostics opérateur

## 51.1 Module

Le module :

```text
operations-baseline
```

peut fournir un écran protégé de diagnostic.

---

## 51.2 Permission

```text
Operations.ReadDiagnostics
```

---

## 51.3 Informations possibles

```text
Application version
Commit
Environment
Uptime
Modules installés
PostgreSQL status
App_Data status
Data Protection status
Last backup status
Disk space
Recent technical errors summary
```

---

## 51.4 Informations interdites

```text
Secrets
Connection string
Mots de passe
Tokens
Contenu des leads
Logs complets
Données d’autres utilisateurs
```

---

# 52. Accès aux logs

## 52.1 Application

Les logs bruts ne doivent pas être exposés dans l’administration standard du MVP.

---

## 52.2 Opérateur

L’accès initial se fait via :

- Docker ;
- SSH ;
- plateforme de logs future ;
- artefacts CI.

---

## 52.3 Client

Le client ne reçoit pas automatiquement un accès aux logs techniques du serveur.

Il peut disposer de diagnostics fonctionnels limités selon le contrat.

---

# 53. Rétention des logs

## 53.1 Principe

La rétention doit être :

- limitée ;
- configurable ;
- proportionnée ;
- documentée.

---

## 53.2 Logs locaux

Les logs Docker et Caddy utilisent une rotation par :

- taille ;
- nombre de fichiers ;
- durée lorsque supportée.

---

## 53.3 Logs centralisés futurs

Une plateforme externe devra définir :

- durée ;
- région de stockage ;
- contrôle d’accès ;
- suppression ;
- coût ;
- sous-traitant.

---

## 53.4 Audit métier

La rétention de l’audit peut différer de celle des logs techniques.

Elle doit être définie par le besoin métier et la politique de données.

---

# 54. Confidentialité et données personnelles

## 54.1 Minimisation

Les données observées doivent être minimisées dès la conception.

---

## 54.2 Adresse IP

L’adresse IP ne doit pas être ajoutée aux logs applicatifs par défaut.

Les logs Caddy peuvent la contenir.

Leur usage et leur rétention doivent être documentés.

---

## 54.3 User agent

Le user agent complet n’est pas nécessaire dans tous les logs.

Il peut être conservé dans les logs d’accès si la finalité est justifiée.

---

## 54.4 Identifiants techniques

Un GUID interne peut être utilisé pour corréler une opération.

Il ne doit pas être exposé publiquement sans nécessité.

---

## 54.5 Hashing

Hasher une donnée personnelle ne la rend pas automatiquement anonyme.

Les emails ne doivent pas être hashés uniquement pour être ajoutés aux logs.

---

# 55. Redaction et filtrage

## 55.1 Défense supplémentaire

Un mécanisme de redaction peut empêcher la journalisation accidentelle de certaines propriétés.

Exemples de noms sensibles :

```text
password
secret
token
authorization
cookie
connectionString
apiKey
privateKey
```

---

## 55.2 Principe

La redaction complète les bonnes pratiques.

Elle ne justifie pas la journalisation volontaire d’un objet contenant des secrets.

---

## 55.3 Objets complets

Éviter :

```csharp
logger.LogInformation(
    "Configuration: {@Configuration}",
    configuration);
```

ou :

```csharp
logger.LogInformation(
    "Lead: {@Lead}",
    lead);
```

---

# 56. Exceptions et données sensibles

Le message d’une exception externe peut contenir :

- URL ;
- nom de serveur ;
- détail SMTP ;
- contenu d’une réponse ;
- donnée utilisateur.

Il doit être filtré ou remplacé par une erreur structurée lorsque nécessaire.

---

# 57. Contrôle de la qualité des logs

Les contrôles doivent détecter :

- message sans EventId pour un événement majeur ;
- chaîne interpolée ;
- objet métier complet journalisé ;
- propriété sensible ;
- niveau incohérent ;
- exception journalisée plusieurs fois ;
- log dans une boucle très fréquente ;
- cardinalité excessive.

---

# 58. Performance des logs

## 58.1 Volume

Les logs ne doivent pas saturer :

- CPU ;
- disque ;
- réseau ;
- stdout ;
- stockage central futur.

---

## 58.2 Événements fréquents

Les événements fréquents doivent être :

- agrégés par métrique ;
- limités ;
- ou placés à un niveau inférieur.

---

## 58.3 Source-generated logging

Les méthodes de logging générées à la compilation peuvent être utilisées pour les chemins fréquents.

Exemple conceptuel :

```csharp
[LoggerMessage(
    EventId = 4001,
    Level = LogLevel.Information,
    Message = "Lead {LeadId} was created.")]
public static partial void LeadCreated(
    ILogger logger,
    Guid leadId);
```

Cette optimisation n’est pas obligatoire pour tous les événements.

---

# 59. Backpressure et échec du provider de logs

Une application ne doit pas devenir totalement indisponible uniquement parce qu’un provider de logs externe est indisponible.

La journalisation distante future doit prévoir :

- buffer limité ;
- timeout ;
- abandon contrôlé ;
- circuit breaker éventuel ;
- fallback local.

Elle ne doit pas bloquer indéfiniment une requête métier.

---

# 60. Configuration conceptuelle

```json
{
  "Observability": {
    "Logging": {
      "Format": "json",
      "IncludeScopes": true,
      "MinimumLevel": "Information"
    },
    "Health": {
      "PublicPath": "/health",
      "LivenessPath": "/health/live",
      "ReadinessPath": "/health/ready",
      "DetailedDiagnosticsEnabled": true
    },
    "Metrics": {
      "Enabled": false,
      "PublicEndpointEnabled": false
    },
    "Tracing": {
      "Enabled": true,
      "ExternalExporterEnabled": false
    }
  }
}
```

Les valeurs exactes devront être validées lors de l’implémentation.

---

# 61. Options typées

Exemple conceptuel :

```csharp
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public LoggingOptions Logging { get; init; } = new();

    public HealthOptions Health { get; init; } = new();

    public MetricsOptions Metrics { get; init; } = new();

    public TracingOptions Tracing { get; init; } = new();
}
```

Les options critiques doivent être validées au démarrage.

---

# 62. Configuration par environnement

## 62.1 Development

```text
Console lisible
Niveau Debug possible
Détails d’exception locaux
Health détaillé local
Aucun exporter externe par défaut
```

---

## 62.2 Staging

```text
Format proche de la production
Niveau Information
Health checks
Logs structurés
Noindex
Diagnostics opérateur
```

---

## 62.3 Production

```text
JSON structuré
Niveau Information
Pas de données sensibles
Pas de Developer Exception Page
Health public minimal
Diagnostics détaillés protégés
Rotation des logs
```

---

# 63. Métriques publiques

Un endpoint de métriques ne doit pas être exposé publiquement dans le MVP.

Une intégration future doit utiliser :

- réseau privé ;
- authentification ;
- filtrage ;
- proxy ;
- collector.

---

# 64. Monitoring externe futur

Une plateforme future pourra centraliser :

- logs ;
- métriques ;
- traces ;
- alertes.

Avant adoption, vérifier :

- coût ;
- rétention ;
- région ;
- conformité ;
- sécurité ;
- disponibilité ;
- export ;
- suppression ;
- verrouillage fournisseur ;
- capacité multi-client ;
- isolation.

---

# 65. Isolation entre clients

Une centralisation future doit empêcher :

- consultation croisée ;
- recherche globale non autorisée ;
- mélange des données ;
- dashboards partagés ;
- erreurs de routage.

Chaque événement doit porter un identifiant de ressource projet contrôlé.

---

# 66. Runbooks associés

La documentation opérationnelle doit prévoir des runbooks pour :

```text
Application indisponible

Readiness en échec

PostgreSQL indisponible

Disque presque plein

Backup en échec

SMTP en échec

Boucle de redémarrage

Erreur après déploiement

Migration manquante

Certificat Caddy en erreur
```

---

# 67. Diagnostic d’un incident applicatif

Ordre recommandé :

```text
1. Relever le correlation ID
2. Vérifier /health/live
3. Vérifier /health/ready
4. Vérifier l’état Docker
5. Consulter les logs applicatifs
6. Consulter les logs Caddy
7. Vérifier PostgreSQL
8. Vérifier l’espace disque
9. Vérifier le dernier déploiement
10. Vérifier la dernière migration
11. Appliquer le runbook adapté
```

---

# 68. Diagnostic d’un formulaire

```text
1. Identifier l’heure et le correlation ID
2. Vérifier la réponse HTTP
3. Rechercher PublicFormSubmissionAccepted ou Rejected
4. Vérifier LeadPersisted
5. Vérifier NotificationEmailCompleted ou Failed
6. Vérifier le lead dans l’administration
7. Vérifier la configuration SMTP si nécessaire
```

Aucune donnée personnelle ne doit être demandée dans les logs pour effectuer ce diagnostic.

---

# 69. Diagnostic d’une génération SiteFactory

```text
1. Lire generation-report.json
2. Identifier GenerationId
3. Trouver la première étape en échec
4. Vérifier le code d’erreur
5. Vérifier le dossier temporaire si conservé
6. Reproduire avec --verbosity detailed
7. Vérifier restore, build ou tests
8. Corriger sans contourner le gate
```

---

# 70. Alertes de sécurité

Événements pouvant nécessiter une agrégation ou alerte :

```text
Nombre élevé de logins échoués

Multiples comptes verrouillés

Rate limiting fréquent

Accès répétés à des routes interdites

Uploads rejetés en volume

Détection de secret

Erreur de permission inhabituelle

Modification du dernier administrateur

Kill switch activé
```

Un événement unique ne constitue pas nécessairement une attaque.

---

# 71. Tests unitaires

Tester notamment :

- création du contexte de log ;
- validation du correlation ID ;
- génération d’un correlation ID ;
- mapping des niveaux ;
- EventId ;
- redaction ;
- propriété sensible interdite ;
- options d’observabilité ;
- métriques sans haute cardinalité.

---

# 72. Tests d’intégration des logs

Vérifier :

- format JSON valide ;
- propriétés communes ;
- trace ID ;
- correlation ID ;
- module ID ;
- EventId ;
- niveau ;
- exception ;
- absence de PII ;
- absence de secret.

---

# 73. Tests HTTP

Vérifier :

- correlation ID accepté lorsqu’il est valide ;
- correlation ID remplacé lorsqu’il est invalide ;
- correlation ID retourné ;
- même correlation ID dans les logs ;
- route template journalisée ;
- query string non journalisée ;
- corps non journalisé.

---

# 74. Tests Identity

Vérifier :

- login réussi sans email dans le log ;
- login échoué sans email ;
- compte verrouillé ;
- permission refusée ;
- UserTechnicalId éventuel ;
- aucune donnée de credential.

---

# 75. Tests des formulaires

Vérifier :

- lead créé avec `LeadId` ;
- aucune coordonnée dans les logs ;
- échec SMTP séparé ;
- correlation ID conservé ;
- résultat public cohérent.

---

# 76. Tests des health checks

## 76.1 Liveness

Vérifier :

```text
processus actif
→ Healthy

PostgreSQL indisponible
→ liveness reste Healthy
```

---

## 76.2 Readiness

Vérifier :

```text
PostgreSQL disponible
+ App_Data disponible
→ Healthy

PostgreSQL indisponible
→ Unhealthy ou Degraded selon le contrat
```

---

## 76.3 Public

Vérifier que la réponse publique ne contient aucun détail sensible.

---

## 76.4 Diagnostic protégé

Vérifier :

- anonyme refusé ;
- utilisateur sans permission refusé ;
- opérateur autorisé ;
- aucun secret.

---

# 77. Tests Docker

Vérifier :

- logs vers stdout ;
- rotation configurée lorsque prévue ;
- health check ;
- liveness utilisé par Docker ;
- redémarrage ;
- version applicative visible ;
- aucun secret dans les logs.

---

# 78. Tests des métriques

Vérifier :

- compteur incrémenté ;
- histogramme ;
- résultat ;
- labels autorisés ;
- absence d’identifiant unique ;
- absence de PII ;
- métriques désactivables ;
- aucun endpoint public non prévu.

---

# 79. Tests des traces

Vérifier :

- activité créée ;
- trace ID ;
- propagation `HttpClient` ;
- span enfant ;
- erreur enregistrée ;
- aucune donnée sensible comme attribut ;
- absence d’export externe par défaut.

---

# 80. Tests des backups

Vérifier :

- événement de début ;
- événement de succès ;
- durée ;
- taille ;
- échec ;
- code non nul ;
- aucun mot de passe ;
- aucun contenu SQL.

---

# 81. Tests de redaction

Jeux de données de test :

```text
password
token
authorization
cookie
connectionString
apiKey
privateKey
email
phone
```

Vérifier qu’aucune valeur réelle ou fictive sensible ne ressort dans la sortie finale.

---

# 82. Tests de charge des logs

Une suite ciblée peut vérifier :

- absence de croissance incontrôlée ;
- coût d’un événement fréquent ;
- absence de blocage ;
- comportement lorsque stdout est lent ;
- agrégation par métrique.

Cette suite est hors du premier incrément si aucune régression n’est encore mesurable.

---

# 83. Tests de production readiness

Le profil production doit vérifier :

```text
[ ] Logs structurés activés
[ ] Niveau minimum cohérent
[ ] Debug et Trace désactivés par défaut
[ ] Sensitive data logging désactivé
[ ] Correlation middleware actif
[ ] Health checks présents
[ ] Health public sobre
[ ] Diagnostic détaillé protégé
[ ] Rotation des logs prévue
[ ] Aucun exporter externe implicite
[ ] Aucun secret
[ ] Aucune PII
[ ] Version de build disponible
```

---

# 84. Quality gates

Une modification ne peut être considérée comme validée si :

```text
Un log contient une donnée personnelle

Un secret est journalisé

Le health check expose une configuration

Le correlation ID n’est pas validé

Une route de diagnostic détaillé est publique

Une métrique contient un identifiant unique comme label

Une exception est exposée au client

Un exporter externe est activé implicitement

Les logs production restent en niveau Debug sans justification
```

---

# 85. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-OBS-001` | Configuration d’observabilité invalide |
| `SF-OBS-002` | Correlation ID invalide |
| `SF-OBS-003` | Propriété sensible détectée dans un log |
| `SF-OBS-004` | Health check public trop détaillé |
| `SF-OBS-005` | Diagnostic non protégé |
| `SF-OBS-006` | Cardinalité de métrique excessive |
| `SF-OBS-007` | Exporter externe non autorisé |
| `SF-OBS-008` | Niveau de production non sûr |
| `SF-OBS-009` | Rotation des logs absente |
| `SF-OBS-010` | Version applicative inconnue |
| `SF-OBS-011` | Readiness indisponible |
| `SF-OBS-012` | Tracing configuration invalide |
| `SF-OBS-013` | Événement sans identifiant stable |
| `SF-OBS-014` | Log de corps HTTP détecté |
| `SF-OBS-015` | Sensitive data logging EF Core activé |

---

# 86. Critères de publication d’un module

Un module ne peut être déclaré `available` que si :

```text
[ ] Événements principaux identifiés
[ ] EventIds définis
[ ] Niveaux cohérents
[ ] Logs structurés
[ ] Aucune PII
[ ] Aucun secret
[ ] Correlation ID conservé
[ ] Métriques principales définies si nécessaires
[ ] Health check défini si nécessaire
[ ] Erreurs structurées
[ ] Tests des logs
[ ] Tests de redaction
[ ] Documentation opérateur
```

---

# 87. Critères d’acceptation du MVP

L’observabilité sera considérée comme opérationnelle lorsque :

1. le CLI produit des sorties et rapports structurés ;
2. chaque génération possède un `GenerationId` ;
3. les applications produisent des logs JSON en production ;
4. les événements majeurs possèdent un `EventId` ;
5. les modules peuvent déclarer leurs événements ;
6. chaque requête possède un correlation ID ;
7. le trace ID est disponible ;
8. les appels sortants propagent le contexte prévu ;
9. les erreurs publiques restent génériques ;
10. les exceptions sont journalisées une seule fois au bon niveau ;
11. aucune donnée personnelle n’apparaît dans les logs ;
12. aucun secret n’apparaît dans les logs ;
13. les corps HTTP ne sont pas journalisés ;
14. EF Core sensitive data logging est désactivé ;
15. `/health/live` existe ;
16. `/health/ready` existe ;
17. `/health` reste minimal ;
18. les diagnostics détaillés sont protégés ;
19. PostgreSQL est vérifié par le readiness ;
20. App_Data est vérifié ;
21. Data Protection est vérifiée ;
22. l’indisponibilité SMTP optionnelle ne tue pas le liveness ;
23. la version applicative est observable ;
24. les événements de backup sont produits ;
25. les événements de restauration sont produits ;
26. les logs Docker utilisent stdout ;
27. une stratégie de rotation existe ;
28. les métriques ne contiennent pas de labels à haute cardinalité ;
29. aucun exporter externe n’est activé par défaut ;
30. les tests d’observabilité réussissent ;
31. les quality gates bloquent une fuite de données ;
32. le build Release réussit sans warning injustifié.

---

# 88. Décisions validées

La baseline d’observabilité est la suivante :

```text
L’observabilité est intégrée aux projets générés.

Le MVP fonctionne sans service de monitoring externe.

Aucune télémétrie externe n’est envoyée par défaut.

Microsoft.Extensions.Logging constitue l’abstraction commune.

Les logs de production sont structurés en JSON.

Les logs sont écrits vers stdout.

L’application n’écrit pas de fichiers de logs permanents
dans App_Data par défaut.

Les événements importants possèdent un EventId et un nom stable.

Les logs utilisent des message templates structurés.

Chaque requête possède un correlation ID.

Les correlation IDs entrants sont validés.

Les traces utilisent le contexte W3C.

Les jobs créent leur propre opération et activité.

Les logs ne contiennent aucune donnée personnelle.

Les logs ne contiennent aucun secret.

Les corps HTTP ne sont pas journalisés en production.

Les query strings ne sont pas journalisées globalement.

EF Core sensitive data logging est interdit en production.

Les exceptions publiques restent génériques.

Une exception n’est pas journalisée plusieurs fois inutilement.

L’audit métier reste distinct des logs techniques.

Le liveness vérifie uniquement la vie du processus.

Le readiness vérifie les dépendances essentielles.

Docker utilise le liveness pour éviter les boucles
de redémarrage liées à une dépendance externe.

Le health check public reste minimal.

Les diagnostics détaillés sont protégés par permission.

SMTP et LLM optionnels peuvent dégrader une capacité
sans rendre toute l’application indisponible.

Les métriques utilisent des labels à faible cardinalité.

Les identifiants utilisateurs, leads et requêtes
ne sont jamais utilisés comme labels de métriques.

Les logs Docker et Caddy possèdent une rotation.

La rétention est limitée et documentée.

Les alertes reposent sur des seuils et des durées,
pas uniquement sur un événement isolé.

Les runbooks utilisent correlation IDs, health checks,
logs et informations de version.

Codex ne peut pas activer seul un exporter externe
ou affaiblir les règles de confidentialité des logs.
```

---

# 89. Règles pour Codex

## 89.1 Lecture préalable

Avant toute modification liée à l’observabilité, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../02-architecture/core-module-boundary.md
09-module-architecture.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
13-generation-pipeline.md
15-cli-specification.md
18-security-baseline.md
19-testing-strategy.md
20-quality-gates-and-ci.md
21-observability-and-logging.md
AGENTS.md
```

Ainsi que :

- module concerné ;
- événements existants ;
- options ;
- middleware ;
- health checks ;
- tests ;
- Docker ;
- runbooks.

---

## 89.2 Ce que Codex peut faire

Codex peut :

- ajouter un EventId ;
- structurer un log ;
- ajouter un scope ;
- ajouter un correlation ID ;
- ajouter un health check ;
- ajouter une métrique ;
- instrumenter une activité ;
- ajouter un diagnostic protégé ;
- ajouter une redaction ;
- ajouter des tests ;
- corriger un niveau ;
- retirer une donnée sensible ;
- documenter un runbook.

---

## 89.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- logguer un objet métier complet ;
- logguer un corps de requête ;
- logguer un email ;
- logguer un téléphone ;
- logguer un token ;
- logguer une connection string ;
- activer `EnableSensitiveDataLogging` en production ;
- exposer un health check détaillé publiquement ;
- utiliser un utilisateur ou un lead comme label de métrique ;
- activer un exporter externe sans validation ;
- ajouter une télémétrie vers un fournisseur tiers ;
- augmenter le niveau production vers Debug de manière permanente ;
- écrire des logs permanents dans App_Data sans besoin ;
- dupliquer une exception à plusieurs niveaux ;
- considérer un log comme un audit complet ;
- modifier une rétention sans validation ;
- ajouter une adresse IP sans finalité documentée.

---

## 89.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouveau fournisseur de logs ;
- plateforme APM ;
- exporter OpenTelemetry ;
- endpoint de métriques ;
- centralisation multi-client ;
- nouvelle donnée observée ;
- journalisation d’adresse IP ;
- modification de rétention ;
- modification d’un diagnostic public ;
- nouveau système d’alerte ;
- nouveau SLA ou SLO ;
- collecte de contenu métier ;
- accès client aux logs ;
- changement du schéma commun des événements.

---

## 89.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Événements ajoutés ou modifiés
EventIds affectés
Niveaux affectés
Propriétés structurées affectées
Correlation ID affecté
Tracing affecté
Métriques affectées
Health checks affectés
Diagnostics affectés
Données potentiellement sensibles
Redaction ajoutée
Configuration modifiée
Docker affecté
Tests ajoutés
Build
Tests
Impact production
Rétention affectée
Décisions humaines restantes
```

Codex ne doit pas déclarer un log conforme sans vérifier les propriétés réellement émises.

---

# 90. Anti-patterns interdits

## 90.1 Interpolation directe

```csharp
logger.LogInformation(
    $"Lead created for {lead.Email}");
```

---

## 90.2 Objet complet

```csharp
logger.LogInformation(
    "Lead: {@Lead}",
    lead);
```

---

## 90.3 Corps de requête

Le middleware enregistre systématiquement le JSON ou les données de formulaire.

---

## 90.4 Health check détaillé public

```json
{
  "databaseServer": "postgres",
  "databaseName": "client_prod",
  "connectionString": "...",
  "exception": "..."
}
```

---

## 90.5 `EnableSensitiveDataLogging`

L’option est activée en production pour diagnostiquer une requête.

---

## 90.6 Label à haute cardinalité

```text
lead_id
user_id
trace_id
email
raw_url
```

utilisés comme labels de métriques.

---

## 90.7 Alertes sur toutes les erreurs utilisateur

Chaque login invalide déclenche une alerte critique.

---

## 90.8 Double journalisation

Le repository, le service, le handler et le middleware journalisent tous la même exception en `Error`.

---

## 90.9 Fichiers de logs non bornés

L’application écrit un fichier permanent sans rotation.

---

## 90.10 Dashboard public

Une page publique expose :

- version interne ;
- modules ;
- base ;
- backups ;
- erreurs.

---

## 90.11 Télémétrie implicite

L’ajout d’un package active automatiquement un export vers un service externe.

---

## 90.12 IP systématique

Toutes les actions métier stockent l’adresse IP sans finalité définie.

---

# 91. Roadmap d’implémentation

## Phase observabilité 1 — Logs structurés

```text
Microsoft.Extensions.Logging
JSON production
EventIds
scopes
niveaux
redaction
```

---

## Phase observabilité 2 — Corrélation

```text
correlation middleware
TraceId
OperationId
réponse HTTP
tests
```

---

## Phase observabilité 3 — Health checks

```text
/health
/health/live
/health/ready
PostgreSQL
App_Data
Data Protection
```

---

## Phase observabilité 4 — Diagnostics opérateur

```text
version
uptime
modules
health détaillé
permissions
```

---

## Phase observabilité 5 — Operations

```text
backups
restore
disk
Docker
Caddy
SMTP
```

---

## Phase observabilité 6 — Métriques

```text
HTTP
formulaires
emails
génération
backups
cardinalité
```

---

## Phase observabilité 7 — Traces

```text
ActivitySource
HTTP sortant
PostgreSQL
jobs
SMTP
```

---

## Phase observabilité 8 — Centralisation optionnelle

```text
OpenTelemetry
collector
logs
metrics
traces
alertes
dashboards
```

La centralisation ne doit être introduite qu’après stabilisation des signaux locaux.

---

# 92. Checklist de revue d’un événement

```text
[ ] EventId stable
[ ] EventName explicite
[ ] Module propriétaire
[ ] Niveau cohérent
[ ] Message template
[ ] Propriétés structurées
[ ] Correlation ID
[ ] Aucune PII
[ ] Aucun secret
[ ] Pas de forte cardinalité
[ ] Test présent
[ ] Documentation présente si événement opérateur
```

---

# 93. Checklist de revue des health checks

```text
[ ] Liveness simple
[ ] Readiness distinct
[ ] PostgreSQL vérifié
[ ] App_Data vérifié
[ ] Data Protection vérifiée
[ ] Dépendances optionnelles traitées comme telles
[ ] Endpoint public minimal
[ ] Diagnostic détaillé protégé
[ ] Aucun secret
[ ] Aucun détail d’infrastructure public
[ ] Tests de succès
[ ] Tests d’échec
```

---

# 94. Checklist de production

```text
[ ] Logs JSON
[ ] Niveau Information
[ ] Debug désactivé
[ ] Trace désactivé
[ ] SensitiveDataLogging désactivé
[ ] Correlation ID actif
[ ] Trace ID actif
[ ] EventIds définis
[ ] Logs sans PII
[ ] Logs sans secrets
[ ] /health disponible
[ ] /health/live disponible
[ ] /health/ready disponible
[ ] Diagnostics détaillés protégés
[ ] Version observable
[ ] Rotation Docker
[ ] Rotation Caddy
[ ] Événements backup
[ ] Événements restore
[ ] Runbooks disponibles
[ ] Tests réussis
```

---

# 95. Checklist de diagnostic d’incident

```text
[ ] Heure de l’incident connue
[ ] Correlation ID connu
[ ] Version déployée connue
[ ] Health live vérifié
[ ] Health ready vérifié
[ ] Docker vérifié
[ ] Logs applicatifs vérifiés
[ ] Logs Caddy vérifiés
[ ] PostgreSQL vérifié
[ ] Espace disque vérifié
[ ] Dernier backup vérifié
[ ] Dernier déploiement vérifié
[ ] Dernière migration vérifiée
[ ] Runbook adapté appliqué
[ ] Cause et résolution documentées
```

---

# 96. Conclusion

L’observabilité SiteFactory doit permettre de diagnostiquer les applications clientes sans créer une nouvelle surface de fuite ou de complexité.

Elle repose sur :

```text
logs structurés
+ événements stables
+ correlation IDs
+ traces
+ métriques maîtrisées
+ health checks
+ diagnostics protégés
+ runbooks
```

La priorité n’est pas de collecter le plus de données possible.

La priorité est de collecter les signaux nécessaires pour :

- détecter une dégradation ;
- comprendre un échec ;
- corréler les événements ;
- agir rapidement ;
- vérifier le retour à la normale.

> Une bonne observabilité ne révèle pas les données du client. Elle révèle le comportement du système.
