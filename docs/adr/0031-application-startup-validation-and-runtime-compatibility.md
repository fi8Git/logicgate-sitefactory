---
adrId: ADR-0031
title: Validate application startup and enforce runtime compatibility before accepting work
status: accepted
date: 2026-07-29
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
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ../03-generation/generated-runtime-database-model.md
  - ../04-quality-security/18-security-baseline.md
  - ../03-generation/14-client-configuration-schema.md
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
  - ./0005-file-generation-strategies.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0017-background-jobs-and-reliable-side-effects.md
  - ./0018-public-form-abuse-protection-and-rate-limiting.md
  - ./0019-audit-trail-and-sensitive-administrative-actions.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ./0021-administrator-provisioning-invitations-and-account-recovery.md
  - ./0022-browser-security-headers-and-content-security-policy.md
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
  - ./0024-client-data-export-and-portability.md
  - ./0025-third-party-integrations-and-egress-control.md
  - ./0026-container-image-build-and-runtime-hardening.md
  - ./0027-database-roles-and-runtime-least-privilege.md
  - ./0028-database-transaction-boundaries-and-concurrency-control.md
  - ./0029-http-api-contracts-problem-details-and-idempotent-commands.md
  - ./0030-cache-strategy-and-invalidation.md
targetVersion: 0.1.0
---

# ADR-0031 — Validation du démarrage et compatibilité du runtime

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le [startup public](../03-generation/generated-startup-validation.md) possède
contrats purs, orchestrateur déterministe, snapshot, lifecycle et adaptateurs
Web/Worker. `IRuntimeStartupValidator.ValidateAsync(CancellationToken)` est
l'API actuelle ; les contextes enrichis/interfaces ci-dessous sont conceptuels.
Le reader PostgreSQL collecte ; `DatabaseCompatibilityProbe` décide dans Runtime,
sans Npgsql/EF/Hosting. Aucune migration, réparation ou création d'identité
n'est autorisée au startup. Web reste live si la DB est incompatible, mais
non-ready ; Worker bloque avant tout travail, sans job métier.
La [preuve de concurrence](../../tests/readiness-concurrency-regression.md)
conserve décisions locales, publication cohérente, annulation et deadlines.
P03-F1 reste **OPEN / UNKNOWN** : 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve, réexamen obligatoire
**2026-10-23**, désormais échu sans prolongation ni réexamen humain supposé.
Les campagnes vertes 768/768 ne prouvent pas une correction causale.
`client.sample-project.*` désigne une extension fictive, pas un module livré.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory impose une validation structurée du runtime avant qu’un composant accepte du trafic ou revendique des jobs.

Le démarrage suit le principe :

```text
charger
→ valider
→ vérifier la compatibilité
→ déterminer les capacités disponibles
→ déclarer la readiness
```

La baseline interdit :

```text
les migrations automatiques au démarrage

la création automatique d’un administrateur

la création automatique de rôles PostgreSQL

l’envoi d’un email au démarrage

l’appel d’un provider externe au démarrage

le traitement d’un job avant validation complète

l’acceptation de trafic avant la readiness

le démarrage avec une base d’un autre client

le démarrage avec une base d’un autre environnement

le démarrage avec un rôle PostgreSQL trop privilégié

le démarrage avec une version de schéma incompatible

le démarrage après restauration sans réconciliation

la journalisation de la configuration complète

la journalisation des secrets ou chaînes de connexion
```

Chaque composant possède un profil de démarrage distinct :

```text
web

worker

migrator

operator-cli
```

---

## 2. Résumé

Le pipeline de démarrage de référence est :

```text
processus lancé
→ métadonnées de release chargées
→ configuration structurelle chargée
→ options typées validées
→ Secret IDs résolus
→ chemins runtime validés
→ identité client et environnement vérifiée
→ connexion PostgreSQL établie
→ rôle PostgreSQL vérifié
→ identité de la base vérifiée
→ version de schéma vérifiée
→ modules et lock vérifiés
→ contrats de jobs vérifiés
→ audit et privacy state vérifiés
→ cache epochs vérifiés
→ capacités runtime calculées
→ readiness activée
```

Le composant n’accepte son travail normal qu’après :

```text
StartupValidationStatus = ready
```

Les validations sont classées en :

```text
blocking
→ le composant reste non-ready ou s’arrête

capability-blocking
→ une fonctionnalité précise est désactivée

warning
→ le composant démarre avec une alerte

informational
→ information de diagnostic uniquement
```

Les services externes comme :

- SMTP ;
- CRM ;
- AI ;
- webhooks sortants ;
- collecteur OpenTelemetry ;

ne sont pas appelés automatiquement pendant le démarrage.

Leur disponibilité est évaluée séparément par des probes ou lors de leur premier usage.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Un composant ne devient ready qu’après validation.

La liveness ne dépend pas de PostgreSQL ou d’un fournisseur externe.

La readiness dépend des prérequis nécessaires
au travail du composant.

Une validation bloquante ne peut pas être transformée
silencieusement en warning par une configuration runtime.

Une validation ne modifie pas le schéma PostgreSQL.

Une validation ne crée pas de compte administrateur.

Une validation ne crée pas de donnée métier.

Une validation ne produit aucun effet réseau externe
non explicitement prévu comme probe.

Un rôle web trop privilégié bloque le web.

Un rôle worker trop privilégié bloque le worker.

Un rôle migrator utilisé par le web bloque le web.

Une base du mauvais client bloque le démarrage.

Une base du mauvais environnement bloque le démarrage.

Un schéma plus récent non supporté bloque le runtime.

Un schéma trop ancien bloque le runtime.

Une migration partiellement appliquée bloque le runtime.

Un restore pending bloque l’ouverture du service.

Un handler de job manquant ne doit pas provoquer
le traitement incorrect du job.

Une configuration invalide n’est jamais remplacée
par une valeur de production inventée.

Un secret absent n’est jamais remplacé
par une valeur par défaut faible.

Les rapports de démarrage ne contiennent aucun secret.

Un redémarrage répété produit le même diagnostic
tant que la cause n’a pas changé.
```

---

## 4. Contexte

Un processus applicatif peut réussir à démarrer techniquement tout en étant incompatible avec son environnement.

Exemples :

```text
L’image contient l’application 1.4.0,
mais PostgreSQL utilise un schéma prévu pour 1.6.0.

Le web reçoit le credential du migrator.

Le worker ne contient plus le handler
d’un Job Type encore en attente.

La base restaurée appartient au staging,
mais le service production tente de s’y connecter.

Le volume Data Protection est vide après un déploiement.

App_Data n’est pas inscriptible par l’utilisateur non-root.

Le sitefactory.lock.json ne correspond pas
aux modules réellement embarqués.

Le runtime attend un Cache Schema Version différent.

Une restauration a été exécutée,
mais les suppressions n’ont pas été réconciliées.
```

Sans validation systématique, les premiers symptômes peuvent apparaître seulement lors :

- de la première connexion administrateur ;
- du premier upload ;
- du premier job ;
- de la première suppression ;
- de la première génération d’export ;
- d’une tentative de rollback ;
- d’une opération critique.

Un démarrage réussi au sens du système d’exploitation ne suffit donc pas à considérer l’application comme opérationnelle.

---

## 5. Problème à résoudre

Comment vérifier avant l’acceptation de trafic ou de jobs que :

- la configuration est complète ;
- les secrets nécessaires sont présents ;
- les volumes possèdent les bonnes permissions ;
- le processus utilise la bonne base ;
- le rôle PostgreSQL est correct ;
- les migrations sont compatibles ;
- les modules et contrats sont cohérents ;
- les jobs en attente sont interprétables ;
- les mécanismes de sécurité sont disponibles ;
- la restauration et la privacy reconciliation sont terminées ?

La solution doit également éviter :

- les migrations implicites ;
- les boucles de retry infinies ;
- les probes produisant des effets externes ;
- les health checks divulguant les détails ;
- les démarrages partiels présentés comme sains ;
- les dépendances facultatives bloquant tout le site ;
- les configurations différentes entre le preflight et le processus réel ;
- les diagnostics reposant uniquement sur des messages de log non structurés.

---

## 6. Portée

Cette décision couvre :

- métadonnées de release ;
- manifests de compatibilité ;
- configuration ;
- secrets ;
- filesystem ;
- PostgreSQL ;
- rôles ;
- migrations ;
- schémas ;
- modules ;
- packages ;
- jobs ;
- outbox ;
- audit ;
- privacy ;
- cache ;
- Identity ;
- Data Protection ;
- médias ;
- intégrations ;
- health checks ;
- readiness ;
- liveness ;
- startup timeout ;
- retries de démarrage ;
- modes dégradés ;
- diagnostics ;
- tests ;
- quality gates ;
- handover.

---

## 7. Hors portée

Cette décision ne définit pas :

- l’ordre complet du rollout production ;
- le mode maintenance côté utilisateur ;
- le basculement du trafic entre deux releases ;
- le rollback de déploiement ;
- le rollback des migrations ;
- une haute disponibilité multi-instance ;
- un service discovery ;
- un orchestrateur Kubernetes ;
- un système de configuration distant obligatoire ;
- une détection complète de toutes les pannes métier futures ;
- la disponibilité continue des fournisseurs externes.

Le rollout, la maintenance et le rollback sont définis dans l’ADR-0032.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Ne jamais accepter du travail dans un état incompatible**
2. **Empêcher les connexions au mauvais client ou environnement**
3. **Empêcher l’usage d’un rôle PostgreSQL incorrect**
4. **Empêcher l’exécution avec un schéma incompatible**
5. **Aucune migration automatique**
6. **Diagnostics exploitables et non sensibles**
7. **Readiness adaptée au composant**
8. **Modes dégradés bornés**
9. **Compatibilité avec Docker Compose**
10. **Déploiements et rollbacks prévisibles**
11. **Validation des contrats de jobs**
12. **Vérification des volumes**
13. **Temps de démarrage borné**
14. **Testabilité**
15. **Portabilité client**
16. **Maintenabilité**

---

## 9. Contraintes

La décision doit respecter :

```text
.NET 10

ASP.NET Core

Docker Compose

PostgreSQL

migrations explicites

rôles PostgreSQL distincts

images immuables

root filesystem read-only

volumes explicites

manifests versionnés

health checks sans secret

readiness avant trafic

compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- chaque release possède des métadonnées de compatibilité ;
- PostgreSQL conserve une identité de base persistante ;
- les migrations sont appliquées avant l’activation de la nouvelle release ;
- les modules et packages sont verrouillés ;
- le web et le worker utilisent des credentials distincts ;
- les fournisseurs externes peuvent être temporairement indisponibles ;
- l’application peut désactiver une capability sans arrêter tout le site ;
- Docker Compose peut utiliser un health check pour piloter les dépendances ;
- un preflight opérateur peut être exécuté avant le déploiement.

---

# 11. Définitions

## 11.1 Startup validation

Ensemble des vérifications exécutées avant la readiness.

---

## 11.2 Runtime compatibility

Compatibilité entre :

- application ;
- configuration ;
- base ;
- modules ;
- données ;
- environnement.

---

## 11.3 Preflight

Validation en lecture seule exécutée avant une opération de déploiement ou migration.

---

## 11.4 Liveness

Indication que le processus fonctionne et n’est pas bloqué de manière irrécupérable.

---

## 11.5 Readiness

Indication que le composant peut recevoir le travail qui lui est destiné.

---

## 11.6 Capability

Fonctionnalité runtime pouvant être disponible, dégradée ou désactivée.

---

## 11.7 Blocking validation

Échec empêchant le composant de devenir ready.

---

## 11.8 Compatibility manifest

Artefact déclarant les versions et capacités supportées par une release.

---

## 11.9 Database identity

Identité persistante distinguant :

- client ;
- environnement ;
- instance de base.

---

## 11.10 Restore pending

État indiquant qu’une restauration nécessite encore des étapes de réconciliation.

---

# 12. Option A — Démarrage optimiste

## 12.1 Description

L’application démarre, écoute immédiatement et laisse les erreurs apparaître lors du premier usage.

---

## 12.2 Avantages

- démarrage rapide ;
- faible infrastructure ;
- peu de validations ;
- développement simple.

---

## 12.3 Inconvénients

- erreurs tardives ;
- trafic accepté trop tôt ;
- diagnostics dispersés ;
- effets partiels ;
- incompatibilités découvertes en production.

---

## 12.4 Risques

- web connecté avec le migrator ;
- jobs inconnus dead-lettered ;
- upload échouant après plusieurs minutes ;
- mauvaise base modifiée ;
- sessions invalides.

---

## 12.5 Réversibilité

```text
élevée
```

---

# 13. Option B — Health check PostgreSQL uniquement

## 13.1 Description

La readiness vérifie simplement qu’une commande :

```sql
SELECT 1;
```

réussit.

---

## 13.2 Avantages

- simple ;
- rapide ;
- détecte l’indisponibilité de la base ;
- compatible avec Docker.

---

## 13.3 Inconvénients

- ne vérifie pas le rôle ;
- ne vérifie pas le client ;
- ne vérifie pas le schéma ;
- ne vérifie pas les modules ;
- ne vérifie pas les volumes ;
- ne vérifie pas les jobs.

---

## 13.4 Risques

Une base incorrecte mais accessible est déclarée saine.

---

## 13.5 Réversibilité

```text
élevée
```

---

# 14. Option C — Migration et réparation automatiques

## 14.1 Description

Le runtime corrige automatiquement :

- migrations ;
- seeds ;
- permissions ;
- dossiers ;
- comptes initiaux.

---

## 14.2 Avantages

- déploiement simple ;
- peu d’intervention ;
- environnement auto-réparé ;
- faible nombre de commandes opérateur.

---

## 14.3 Inconvénients

- effets destructifs au démarrage ;
- responsabilité ambiguë ;
- race entre instances ;
- rollback difficile ;
- credential puissant dans le runtime ;
- comportement imprévisible.

---

## 14.4 Risques

- migration partielle ;
- perte de données ;
- mauvais environnement modifié ;
- création d’un compte par défaut ;
- réinitialisation de permissions.

---

## 14.5 Réversibilité

```text
faible après mutation
```

---

# 15. Option D — Validation structurée et fail-fast

## 15.1 Description

Le runtime vérifie tous les contrats nécessaires, sans les réparer automatiquement.

---

## 15.2 Avantages

- état explicite ;
- aucune mutation cachée ;
- diagnostics ;
- sécurité ;
- compatibilité ;
- rollout prévisible ;
- tests automatisables ;
- modes dégradés contrôlés.

---

## 15.3 Inconvénients

- manifests ;
- validateurs ;
- checks supplémentaires ;
- startup plus long ;
- opérations préalables explicites ;
- davantage de tests.

---

## 15.4 Risques

- validation trop stricte ;
- faux blocage ;
- dépendance entre manifests ;
- timeout de démarrage trop court.

---

## 15.5 Réversibilité

```text
élevée
```

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Optimiste | PostgreSQL seul | Auto-réparation | Validation structurée |
|---|---:|---:|---:|---:|---:|
| Sécurité | 5 | 1 | 2 | 2 | 5 |
| Compatibilité | 5 | 1 | 2 | 3 | 5 |
| Diagnostic | 5 | 2 | 3 | 2 | 5 |
| Absence d’effets cachés | 5 | 4 | 5 | 1 | 5 |
| Rollback | 4 | 2 | 3 | 1 | 5 |
| Docker Compose | 4 | 4 | 5 | 4 | 5 |
| Modes dégradés | 4 | 2 | 2 | 2 | 5 |
| Maintenabilité | 5 | 2 | 3 | 2 | 5 |
| Testabilité | 4 | 2 | 4 | 3 | 5 |
| Transfert client | 4 | 2 | 3 | 2 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’implémenter un pipeline central :

```text
IRuntimeStartupValidator
```

capable d’exécuter des validations déclarées par :

- la plateforme ;
- les modules ;
- les customizations ;
- le composant courant.

Contrat conceptuel :

```csharp
public interface IRuntimeStartupValidator
{
    Task<RuntimeStartupReport> ValidateAsync(
        RuntimeStartupContext context,
        CancellationToken cancellationToken);
}
```

---

## 17.1 Contexte

Le contexte doit pouvoir contenir :

```text
ComponentType

ApplicationVersion

ReleaseManifest

ClientIdentity

EnvironmentIdentity

ConfigurationSnapshot

StartupDeadline

ValidationProfile
```

---

## 17.2 Résultat

Le rapport doit permettre de déterminer :

```text
ready

degraded

blocked

failed
```

---

# 18. Composants

Valeurs initiales :

```text
web

worker

migrator

operator-cli
```

---

## 18.1 Web

Valide les prérequis pour :

- trafic public ;
- administration ;
- Identity ;
- médias ;
- commandes HTTP.

---

## 18.2 Worker

Valide les prérequis pour :

- jobs ;
- leases ;
- outbox ;
- médias ;
- rétention ;
- intégrations.

---

## 18.3 Migrator

Valide :

- cible ;
- rôle ;
- compatibilité du plan ;
- état des migrations ;
- verrous opérateur.

---

## 18.4 Operator CLI

Valide selon la commande demandée.

Il ne devient pas un service permanent.

---

# 19. Phases du démarrage

Phases initiales :

```text
1. release metadata

2. configuration

3. secrets

4. filesystem

5. database connectivity

6. database identity

7. database role

8. database schema

9. modules and contracts

10. component capabilities

11. security state

12. readiness activation
```

---

## 19.1 Ordre

Les validations peu coûteuses et déterministes sont exécutées en premier.

---

## 19.2 Échec précoce

Une configuration structurelle invalide doit être détectée avant une connexion réseau.

---

# 20. Runtime Compatibility Manifest

Chaque release contient :

```text
runtime-compatibility-manifest.json
```

---

## 20.1 Contenu

Le manifest doit pouvoir contenir :

```text
SchemaVersion

ApplicationVersion

RepositoryCommit

ContainerImageDigest

RuntimeProfileVersion

ConfigurationSchemaVersion

ClientLockSchemaVersion

ModuleLockHash

MinimumDatabaseSchemaVersion

MaximumDatabaseSchemaVersion

ExpectedDatabaseMigrationIds

ModuleSchemaRequirements

JobContractVersions

AuditSchemaVersion

PrivacySchemaVersion

CacheSchemaVersion

ExportSchemaVersions

ApiVersions

BrowserSecurityProfileVersions

RequiredCapabilities

OptionalCapabilities

GeneratedAtUtc
```

---

## 20.2 Secret

Le manifest ne contient aucun :

- credential ;
- secret ;
- token ;
- endpoint signé ;
- chaîne de connexion.

---

## 20.3 Signature

Le manifest est relié à la release et à son digest conformément à l’ADR-0026.

---

# 21. Source des métadonnées

Les métadonnées sont produites pendant le build.

---

## 21.1 Pas de valeur runtime inventée

Le processus ne reconstruit pas son manifest depuis des versions de fichiers potentiellement incohérentes.

---

## 21.2 Vérification

Le runtime vérifie que :

```text
digest ou revision attendue
→ correspond aux métadonnées embarquées
```

lorsque cette information est disponible.

---

# 22. Configuration structurelle

Le runtime charge :

- configuration de plateforme ;
- configuration client ;
- configuration environnement ;
- feature configuration ;
- integration configuration ;
- profils opérationnels.

---

## 22.1 Options typées

Chaque section utilise un type et un validateur dédiés.

---

## 22.2 Aucune configuration libre

Les modules ne lisent pas directement des chaînes arbitraires dans `IConfiguration`.

---

## 22.3 Validation au démarrage

Utiliser les mécanismes de validation d’options et les validateurs SiteFactory.

---

# 23. Ordre de précédence de la configuration

L’ordre doit être documenté.

Exemple conceptuel :

```text
defaults de plateforme

configuration structurelle générée

configuration environnement non sensible

secret references

overrides opérateur explicitement autorisés
```

---

## 23.1 Override

Une valeur critique ne peut pas être remplacée par une variable arbitraire non déclarée.

---

## 23.2 Environnement

Le runtime refuse une configuration de développement dans un environnement production.

---

# 24. Propriétés inconnues

Les sections SiteFactory structurées doivent détecter les propriétés inconnues lorsque leur contrat est strict.

---

## 24.1 Objectif

Détecter :

- faute de frappe ;
- ancienne propriété ;
- option non supportée ;
- mauvais fichier.

---

## 24.2 Variables système

Le processus ne rejette pas toutes les variables d’environnement du système d’exploitation.

La validation s’applique aux namespaces SiteFactory concernés.

---

# 25. Valeurs par défaut

Les valeurs par défaut sont autorisées uniquement lorsqu’elles sont :

- sûres ;
- documentées ;
- identiques entre environnements ;
- non secrètes.

---

## 25.1 Interdictions

Ne jamais inventer par défaut :

- mot de passe ;
- clé de signature ;
- endpoint provider ;
- environnement ;
- client ;
- rôle PostgreSQL ;
- Public Base URL de production.

---

# 26. Validation des URLs

Les URLs runtime sont validées selon leur usage.

---

## 26.1 Public Base URL

Elle doit posséder :

- HTTPS en production ;
- host exact ;
- aucun credential ;
- aucun fragment ;
- port autorisé.

---

## 26.2 Providers

Ils suivent l’ADR-0025.

---

## 26.3 Host Header

Le Public Base URL n’est pas reconstruit depuis un Host Header entrant.

---

# 27. Secret references

Le runtime connaît uniquement :

```text
Secret IDs attendus
```

---

## 27.1 Résolution

Pour chaque secret requis par le composant :

- référence présente ;
- valeur résoluble ;
- non vide ;
- format minimal valide ;
- environnement compatible.

---

## 27.2 Valeur

La valeur n’est jamais ajoutée au rapport.

---

## 27.3 Secret facultatif

Un secret facultatif absent désactive uniquement la capability correspondante.

---

# 28. Secrets par composant

Exemples :

```text
web
→ postgres web
→ Data Protection protection éventuelle
→ provider synchrone strictement nécessaire

worker
→ postgres worker
→ SMTP
→ CRM
→ object storage
→ providers de jobs installés

migrator
→ postgres migrator

operator backup
→ postgres backup
→ destination de backup
```

---

## 28.1 Isolation

Un composant ne valide pas ni ne reçoit les secrets d’une capability qu’il ne possède pas.

---

# 29. Rotation de secret

La validation doit supporter qu’un secret ait été rotaté sans rebuild.

---

## 29.1 Startup

Le processus lit la valeur courante au démarrage.

---

## 29.2 Runtime

Les secrets devant être rechargés sans redémarrage suivent une stratégie dédiée.

---

# 30. Filesystem runtime

Le web et le worker valident leurs chemins d’écriture.

---

## 30.1 Chemins typiques

```text
App_Data

Data Protection Keys

tmpfs

zone de staging média

zone d’exports selon le composant
```

---

## 30.2 Root filesystem

La validation doit réussir avec :

```text
root filesystem read-only
```

---

## 30.3 Chemin inattendu

Une écriture nécessaire hors des chemins déclarés bloque le composant.

---

# 31. Probe d’écriture

Une validation peut créer un fichier temporaire vide ou aléatoire dans le répertoire prévu.

---

## 31.1 Contraintes

- nom opaque ;
- taille minimale ;
- aucune donnée métier ;
- suppression immédiate ;
- aucune persistance si le probe réussit.

---

## 31.2 Échec

Un fichier impossible à créer ou supprimer produit un échec bloquant pour la capability concernée.

---

# 32. Permissions et ownership

Le runtime vérifie que :

- l’utilisateur non-root peut lire les fichiers applicatifs ;
- il peut écrire uniquement dans les volumes autorisés ;
- les secrets montés sont lisibles par le bon service ;
- les fichiers ne sont pas world-readable sans raison ;
- aucun `chmod 777` n’est nécessaire.

---

# 33. Espace disque

Les opérations nécessitant du stockage vérifient l’espace minimal disponible.

---

## 33.1 Web

Peut vérifier l’espace minimal pour :

- uploads temporaires ;
- Data Protection ;
- petits artefacts.

---

## 33.2 Worker

Peut vérifier :

- médias ;
- exports ;
- traitements temporaires ;
- backups selon son profil.

---

## 33.3 Valeur

Un seuil est exprimé par profil et non codé arbitrairement dans les modules.

---

# 34. PostgreSQL connectivity

Le composant établit une connexion avec son propre credential.

---

## 34.1 Requête minimale

La validation ne se limite pas à :

```sql
SELECT 1;
```

Elle poursuit avec les vérifications d’identité et de rôle.

---

## 34.2 Timeout

La connexion possède :

- connect timeout ;
- deadline globale de démarrage ;
- retry borné.

---

# 35. Retry de connexion au démarrage

PostgreSQL peut démarrer quelques secondes après le web.

---

## 35.1 Baseline indicative

```text
durée totale maximale :
60 secondes

backoff exponentiel court

jitter

aucune boucle infinie
```

---

## 35.2 Erreurs rejouables

- connexion refusée ;
- DNS Docker temporairement indisponible ;
- PostgreSQL en cours de démarrage ;
- timeout réseau court.

---

## 35.3 Erreurs permanentes

Ne pas retenter indéfiniment :

- authentication failed ;
- rôle incorrect ;
- base absente après résolution stable ;
- permission refusée ;
- configuration invalide ;
- certificat invalide.

---

# 36. Database Identity

Chaque base possède une identité persistée.

Table conceptuelle :

```text
sitefactory_runtime.database_identity
```

---

## 36.1 Champs

```text
DatabaseInstanceId

ClientSlug

Environment

CreatedAtUtc

ProvisioningVersion

DatabaseOwnerRoleId
```

---

## 36.2 Unicité

Une base possède une seule identité active.

---

## 36.3 Valeurs attendues

Le runtime compare :

```text
ClientSlug attendu

Environment attendu

DatabaseInstanceId attendu lorsque configuré
```

---

# 37. Mauvais client

Si le client déclaré par la base diffère de celui de la release :

```text
startup blocked
```

---

## 37.1 Aucun override

Le runtime ne modifie pas l’identité de la base pour « corriger » la cible.

---

## 37.2 Opération

Une correction nécessite une procédure Infrastructure explicite.

---

# 38. Mauvais environnement

Un web production connecté à une base staging est bloqué.

---

## 38.1 Comparaison

```text
runtime environment = production

database environment = staging

→ incompatible
```

---

## 38.2 Protection

Cette vérification est indépendante du nom de host PostgreSQL.

---

# 39. Vérification du rôle PostgreSQL

Le runtime lit :

```sql
current_user;
session_user;
```

et les attributs nécessaires.

---

## 39.1 Web

Le web exige le profil :

```text
database.web-runtime.v1
```

---

## 39.2 Worker

Le worker exige :

```text
database.worker-runtime.v1
```

---

## 39.3 Migrator

Le migrator exige :

```text
database.migrator.v1
```

---

# 40. Rôle trop privilégié

Le démarrage est bloqué si le runtime détecte notamment :

```text
superuser

createdb

createrole

replication

bypassrls

ownership d’un objet

membership vers le database owner

CREATE sur un schéma applicatif
```

---

## 40.1 Rôle `postgres`

Le runtime refuse explicitement :

```text
current_user = postgres
```

---

# 41. Privilèges manquants

Un privilège nécessaire absent bloque :

- le composant entier ;
- ou la capability correspondante ;

selon son importance.

---

## 41.1 Exemple web

Absence d’accès aux contenus publics :

```text
web blocked
```

---

## 41.2 Exemple worker

Absence du privilège de traitement d’un Job Type optionnel :

```text
capability disabled
```

si aucun job correspondant n’est en attente.

---

# 42. État du schéma PostgreSQL

La base expose un état de schéma structuré.

Table ou vue conceptuelle :

```text
sitefactory_runtime.schema_state
```

---

## 42.1 Champs

```text
SchemaFamily

CurrentVersion

LastMigrationId

MigrationState

UpdatedAtUtc

UpdatedByOperationId
```

---

## 42.2 Familles

Exemples :

```text
platform

identity

jobs

audit

privacy

media

module:{moduleId}
```

---

# 43. État des migrations

Valeurs recommandées :

```text
stable

migration-planned

migration-running

migration-failed

restore-pending

reconciliation-required
```

---

## 43.1 Runtime normal

Seul l’état :

```text
stable
```

est compatible avec le mode normal.

---

## 43.2 Maintenance

Les autres états peuvent conduire au mode maintenance défini dans l’ADR-0032.

---

# 44. Compatibilité de schéma

La release déclare :

```text
MinimumDatabaseSchemaVersion

MaximumDatabaseSchemaVersion
```

---

## 44.1 Baseline MVP

Par défaut :

```text
minimum = maximum
```

Le runtime exige la version exacte.

---

## 44.2 Fenêtre de compatibilité

Une plage est autorisée uniquement lorsqu’une migration suit un pattern :

```text
expand
→ déploiement compatible
→ contract ultérieur
```

et que cette compatibilité est testée.

---

## 44.3 Schéma trop ancien

```text
database schema < minimum
→ migration required
→ runtime non-ready
```

---

## 44.4 Schéma trop récent

```text
database schema > maximum
→ runtime incompatible
→ démarrage bloqué
```

---

# 45. Migrations attendues

Le manifest peut déclarer :

```text
ExpectedDatabaseMigrationIds
```

---

## 45.1 Vérification

Le runtime compare :

- migrations attendues ;
- historique EF Core ;
- état SiteFactory ;
- hash ou version du plan lorsque disponible.

---

## 45.2 Migration inconnue

Une migration présente dans la base mais inconnue de l’image est considérée comme un schéma potentiellement plus récent.

---

# 46. Migration partielle

Un marker d’opération permet de détecter :

- migration démarrée ;
- migration non terminée ;
- rapport absent ;
- checksum post-migration invalide.

---

## 46.1 Comportement

```text
migration-running
ou
migration-failed
→ runtime normal bloqué
```

---

## 46.2 Réparation

Le web ne tente pas de terminer la migration.

---

# 47. Aucune migration automatique

Les méthodes suivantes ne sont pas appelées au démarrage du web ou worker :

```csharp
Database.Migrate()

Database.EnsureCreated()
```

---

## 47.1 Seed

Aucun seed privilégié ou création d’administrateur n’est exécuté automatiquement.

---

## 47.2 Migrator

Seule l’image migrator peut appliquer un plan validé.

---

# 48. SiteFactory Lock

Le runtime charge :

```text
sitefactory.lock.json
```

---

## 48.1 Vérifications

- Schema Version ;
- Generator Version ;
- Platform Version ;
- modules ;
- versions ;
- package references ;
- template ;
- hash de composition.

---

## 48.2 Hash

Le manifest de compatibilité contient le hash attendu.

---

## 48.3 Différence

Un lock différent de celui de la release bloque le runtime.

---

# 49. Modules embarqués

Le runtime inventorie les modules chargés.

---

## 49.1 Comparaison

```text
modules attendus dans le lock

modules présents dans l’image

modules déclarés dans la base
```

doivent être compatibles.

---

## 49.2 Module absent

Un module absent mais possédant :

- tables actives ;
- jobs en attente ;
- données utilisées ;

bloque le composant ou le déploiement.

---

## 49.3 Module supplémentaire

Un module non déclaré ne doit pas s’activer automatiquement.

---

# 50. Module Schema State

Chaque module persistant possède une version de schéma.

---

## 50.1 Compatibilité

Le module déclare :

```text
MinimumModuleSchemaVersion

MaximumModuleSchemaVersion
```

---

## 50.2 Suppression d’un module

Le retrait nécessite :

- drainage ;
- migration ;
- conservation ou suppression de données ;
- retrait des jobs ;
- mise à jour du lock.

---

# 51. Contrats publics des modules

Le démarrage vérifie les versions de contrats partagés.

Exemples :

- événements ;
- jobs ;
- exports ;
- permissions ;
- cache regions ;
- migrations.

---

## 51.1 Contrat incompatible

Une nouvelle version majeure non gérée bloque le composant consommateur.

---

# 52. Contrats de jobs

Le worker inventorie ses handlers.

Pour chaque handler :

```text
JobType

JobContractVersion

MinimumSupportedVersion

MaximumSupportedVersion

HandlerVersion
```

---

## 52.1 Jobs en attente

Le worker compare ses handlers aux jobs :

- pending ;
- retry-wait ;
- running récupérables ;
- dead-letter selon le besoin.

---

## 52.2 Job inconnu

Un job actif dont le type est inconnu provoque :

```text
worker blocked
```

ou une capability bloquée si le partitionnement permet une isolation sûre.

---

## 52.3 Version inconnue

Le worker ne tente pas de désérialiser un payload incompatible.

---

# 53. Drainage des jobs

Avant le retrait d’un handler, le plan de déploiement doit :

- terminer les jobs ;
- les annuler ;
- les migrer ;
- ou conserver un worker compatible.

---

## 53.1 Startup

Le démarrage détecte les violations, mais ne les corrige pas automatiquement.

---

# 54. Contrats de l’outbox

Le worker vérifie qu’il possède un dispatcher compatible avec les messages actifs.

---

## 54.1 Message inconnu

Un message outbox non interprétable ne doit pas être marqué comme envoyé ou supprimé.

---

## 54.2 Readiness

Une outbox incompatible bloque le dispatcher concerné.

---

# 55. Audit Schema

Le runtime compare :

```text
AuditSchemaVersion

fonctions requises

permissions

epoch state
```

---

## 55.1 Web

Le web doit pouvoir appeler la fonction append avec son rôle.

---

## 55.2 Worker

Le worker vérifie la même capacité pour ses actions auditées.

---

## 55.3 Failure mode

Une incapacité à enregistrer l’audit bloque :

- les actions sensibles ;
- ou le composant si elles sont fondamentales.

---

# 56. Audit integrity state

Le dernier état connu peut être :

```text
valid

invalid

incomplete

unknown
```

---

## 56.1 `invalid`

Une intégrité confirmée invalide bloque l’administration sensible et déclenche une alerte critique.

---

## 56.2 `unknown`

Le comportement dépend du profil :

- public éventuellement disponible ;
- administration critique bloquée ;
- opérations de revue autorisées.

---

# 57. Privacy Schema

Le runtime vérifie :

- Privacy Data Catalog Version ;
- Retention Policy Schema ;
- Deletion Ledger State ;
- checkpoint attendu ;
- Retention Holds.

---

## 57.1 Worker

Un worker de rétention ne devient ready que si les contrats sont compatibles.

---

## 57.2 Web

L’administration privacy peut être capability-blocked sans rendre le site public indisponible.

---

# 58. Restore State

Une restauration crée ou conserve un état :

```text
restore-pending
```

---

## 58.1 Conditions de sortie

Le mode normal exige :

- nouveaux Security Epochs ;
- Recovery Epoch ;
- Session Epoch ;
- Audit Epoch ;
- Cache Global Epoch ;
- Deletion Ledger réconcilié ;
- permissions vérifiées ;
- rapport de restore validé.

---

## 58.2 Runtime

Le web ne « répare » pas automatiquement ces états.

---

# 59. Cache State

Le runtime vérifie :

- Cache Schema Version ;
- Global Cache Epoch ;
- Cache Region Epoch table ;
- permissions nécessaires.

---

## 59.1 Cache incompatible

Le cache local peut être vidé.

---

## 59.2 PostgreSQL metadata incompatible

Une table ou version d’epoch incompatible peut bloquer les capabilities cacheables ou le web selon le besoin.

---

# 60. Identity State

Le web vérifie notamment :

- Session Epoch présent ;
- Recovery Epoch présent ;
- Data Protection disponible ;
- tables Identity compatibles ;
- fonctions de provisioning disponibles ;
- rôle web autorisé.

---

## 60.1 Bootstrap

L’absence de premier administrateur n’est pas une incompatibilité technique du site public.

Elle produit un état opérateur :

```text
initial-administrator-required
```

---

## 60.2 Aucun bootstrap automatique

Le runtime ne crée aucun compte.

---

# 61. Data Protection

Le web vérifie le key ring.

---

## 61.1 Conditions

- répertoire présent ;
- lecture autorisée ;
- écriture autorisée si rotation nécessaire ;
- isolation environnement ;
- application name attendu ;
- aucune clé embarquée dans l’image.

---

## 61.2 Key ring vide

Un projet neuf peut initialiser ses clés dans le volume prévu.

Cette initialisation technique ne crée aucune identité métier.

---

## 61.3 Key ring perdu

Une perte sur un projet existant produit :

- invalidation des cookies ;
- alerte ;
- procédure opérateur ;
- pas de restauration silencieuse des anciennes sessions.

---

# 62. Médias et App_Data

Le composant concerné vérifie :

- volume présent ;
- permissions ;
- structure attendue ;
- format de storage state ;
- espace minimal ;
- absence d’un marker d’incident bloquant.

---

## 62.1 Original manquant

Le démarrage ne rescane pas systématiquement tous les médias.

Une réconciliation média complète appartient à une commande opérateur.

---

## 62.2 Validation bornée

Le startup vérifie :

- racine ;
- version du layout ;
- capacité de lecture/écriture ;
- quelques invariants rapides.

---

# 63. Export Storage

Le worker d’export vérifie :

- répertoire privé ;
- permissions ;
- quota ;
- TTL job disponible ;
- aucun accès public direct.

---

## 63.1 Export non configuré

La capability d’export est désactivée.

Le site public peut rester ready.

---

# 64. Backups

Le web ne valide pas la capacité à produire un backup à chaque démarrage.

---

## 64.1 Opérateur

Un preflight de déploiement peut vérifier :

- dernier backup ;
- âge ;
- destination ;
- espace ;
- succès du dernier restore drill.

---

## 64.2 Readiness

Une sauvegarde en retard produit généralement :

```text
warning ou deployment block
```

et non une panne du processus web déjà en cours.

---

# 65. Intégrations externes

Les providers sont validés structurellement :

- Integration ID ;
- endpoint ;
- Secret ID ;
- policy ;
- kill switch ;
- catégories de données.

---

## 65.1 Aucun appel automatique

Le startup ne contacte pas :

- SMTP ;
- CRM ;
- AI ;
- webhook ;
- object storage externe ;

sauf probe explicitement déclarée et sans effet.

---

## 65.2 Raisons

- ralentissement ;
- coûts ;
- side effects ;
- indisponibilité transitoire ;
- amplification de panne ;
- fuite potentielle.

---

# 66. Provider probes

Des probes asynchrones séparées peuvent vérifier un fournisseur.

---

## 66.1 Conditions

- endpoint de santé documenté ;
- aucun effet métier ;
- timeout court ;
- rate limiting ;
- aucun payload personnel ;
- aucune dépendance de liveness.

---

## 66.2 Échec

La capability peut devenir :

```text
degraded
```

ou :

```text
unavailable
```

---

# 67. OpenTelemetry

L’indisponibilité du collecteur de télémétrie ne bloque pas le métier.

---

## 67.1 Comportement

```text
fail-open
```

avec :

- buffer borné ;
- métrique locale ;
- log de warning ;
- retry borné.

---

## 67.2 Configuration invalide

Une configuration pouvant exposer des données sensibles peut rester bloquante.

---

# 68. Capability Registry

Le runtime maintient un registre interne :

```text
IRuntimeCapabilityRegistry
```

---

## 68.1 État

Valeurs recommandées :

```text
available

degraded

unavailable

blocked

not-configured
```

---

## 68.2 Exemples

```text
public-site

administrator-ui

identity

sample-records

media-upload

email-delivery

crm-sync

ai-content-generation

data-export

privacy-retention

audit-review
```

---

## 68.3 Source

Une capability est calculée à partir :

- configuration ;
- secrets ;
- schéma ;
- permissions ;
- kill switches ;
- état des dépendances.

---

# 69. Capability et entitlement

Une capability technique ne remplace pas un entitlement.

---

## 69.1 Fonction disponible mais non achetée

```text
capability available
+ entitlement absent
→ fonctionnalité non autorisée
```

---

## 69.2 Fonction achetée mais indisponible

```text
entitlement présent
+ capability unavailable
→ erreur de disponibilité contrôlée
```

---

# 70. Validation blocking

Une validation bloquante empêche le composant d’accepter son travail.

Exemples :

- configuration structurelle invalide ;
- mauvaise base ;
- mauvais rôle PostgreSQL ;
- schéma incompatible ;
- restore pending ;
- App_Data obligatoire non accessible ;
- Data Protection obligatoire indisponible ;
- handler fondamental manquant.

---

# 71. Capability-blocking

Une validation capability-blocking désactive uniquement une fonction.

Exemples :

- SMTP absent ;
- CRM non configuré ;
- stockage d’export indisponible ;
- provider AI désactivé ;
- processor média optionnel absent.

---

## 71.1 Interface

L’interface doit afficher une indisponibilité contrôlée, pas une exception.

---

# 72. Warning

Un warning permet le démarrage.

Exemples :

- backup trop ancien ;
- provider facultatif non testé ;
- warming cache échoué ;
- télémétrie distante indisponible ;
- module optionnel non configuré.

---

## 72.1 Visibilité

Les warnings sont :

- structurés ;
- visibles opérateur ;
- associés à un Error Code ;
- suivis lorsqu’ils persistent.

---

# 73. Mode dégradé

Un composant peut devenir ready avec certaines capabilities indisponibles uniquement si :

- la source de vérité reste cohérente ;
- la sécurité n’est pas réduite ;
- l’utilisateur reçoit une erreur claire ;
- aucun effet n’est perdu silencieusement ;
- l’état est observable.

---

## 73.1 Exemples acceptables

- CRM indisponible, leads conservés localement ;
- SMTP indisponible, emails en attente ;
- analytics indisponible ;
- AI désactivée.

---

## 73.2 Exemples interdits

- autorisation non vérifiable ;
- audit critique indisponible ;
- mauvaise base ;
- migration incomplète ;
- restore non réconcilié ;
- rôle runtime excessif.

---

# 74. Runtime Modes

Valeurs conceptuelles :

```text
normal

degraded

maintenance

restore-reconciliation

migration-required

incompatible

failed
```

---

## 74.1 Normal

Toutes les capabilities obligatoires sont disponibles.

---

## 74.2 Degraded

Le service peut fonctionner avec des limitations connues.

---

## 74.3 Maintenance

Le trafic métier ordinaire est limité selon l’ADR-0032.

---

## 74.4 Incompatible

L’image et l’environnement ne peuvent pas fonctionner ensemble.

---

# 75. Liveness

Endpoint conceptuel :

```text
/health/live
```

---

## 75.1 Responsabilité

Il vérifie uniquement :

- processus actif ;
- runtime non bloqué ;
- boucle principale fonctionnelle.

---

## 75.2 Dépendances

Il ne vérifie pas :

- PostgreSQL ;
- SMTP ;
- CRM ;
- AI ;
- stockage externe.

---

## 75.3 Résultat

Une dépendance externe en panne ne doit pas déclencher des redémarrages continus du processus.

---

# 76. Readiness

Endpoint conceptuel :

```text
/health/ready
```

---

## 76.1 Responsabilité

Il vérifie que le composant peut recevoir son travail normal.

---

## 76.2 Web

Dépend notamment de :

- startup validation terminée ;
- base correcte ;
- rôle correct ;
- schéma compatible ;
- volumes requis ;
- Data Protection ;
- absence de restore pending.

---

## 76.3 Worker

Dépend notamment de :

- contrats de jobs ;
- rôle worker ;
- schéma jobs ;
- leases ;
- handlers fondamentaux ;
- storage requis.

---

# 77. Readiness par capability

Des endpoints internes peuvent distinguer :

```text
/health/ready/public

/health/ready/admin

/health/ready/worker
```

---

## 77.1 Exposition

Les détails complets ne sont pas exposés publiquement.

---

## 77.2 Caddy

Caddy utilise le profil nécessaire au routage global.

---

## 77.3 Administration

Une capability administrateur indisponible peut retourner `503` sur ses routes tout en conservant le site public.

---

# 78. Réponse publique des health checks

La réponse publique reste minimale.

Exemple :

```json
{
  "status": "healthy"
}
```

ou :

```json
{
  "status": "unavailable"
}
```

---

## 78.1 Données interdites

- hostname PostgreSQL ;
- nom du rôle ;
- version de schéma détaillée ;
- Secret ID ;
- chemin ;
- stack trace ;
- modules internes.

---

# 79. Diagnostic détaillé

Un diagnostic détaillé peut être accessible :

- localement ;
- par CLI ;
- via une permission opérateur ;
- dans un rapport privé.

---

## 79.1 Protection

Il utilise :

- authentification ;
- autorisation ;
- no-store ;
- audit selon la sensibilité.

---

# 80. Startup Timeout

Le pipeline possède une deadline globale.

---

## 80.1 Baseline indicative

```text
web :
60 secondes

worker :
90 secondes

migrator preflight :
120 secondes
```

---

## 80.2 Ajustement

Les valeurs sont configurables par profil, avec un maximum borné.

---

## 80.3 Expiration

À expiration :

- validation annulée ;
- readiness non activée ;
- rapport produit ;
- processus arrêté ou conservé non-ready selon le profil.

---

# 81. Échec permanent et restart loop

Une configuration invalide doit produire un échec rapide et stable.

---

## 81.1 Docker restart

Docker peut redémarrer le processus selon sa policy.

---

## 81.2 Interne

L’application ne boucle pas indéfiniment en masquant l’erreur.

---

## 81.3 Alerte

Des redémarrages répétés déclenchent une alerte opérateur.

---

# 82. Startup report

Le pipeline produit un rapport structuré en mémoire.

Artefact conceptuel pour le CLI :

```text
runtime-startup-report.json
```

---

## 82.1 Contenu

```text
SchemaVersion

StartupId

ComponentType

ApplicationVersion

ClientSlug

Environment

StartedAtUtc

CompletedAtUtc

OverallStatus

RuntimeMode

ValidationResults

Capabilities

Warnings

ErrorCodes

ReleaseManifestHash
```

---

## 82.2 Données interdites

```text
secret

connection string

password

token

cookie

private key

configuration complète

contenu métier
```

---

# 83. Validation Result

Chaque résultat doit pouvoir contenir :

```text
ValidationId

Category

Severity

Status

ErrorCode

Summary

Component

CapabilityId éventuel

Duration

RemediationRunbookId
```

---

## 83.1 Pas de secret dans Summary

Le résumé reste générique et sûr.

---

## 83.2 Diagnostic privé

Des détails supplémentaires peuvent être stockés dans un canal privé avec redaction.

---

# 84. Identifiants de validation

Exemples :

```text
startup.configuration.schema

startup.secret.postgres-web

startup.filesystem.app-data

startup.database.identity

startup.database.role

startup.database.schema

startup.jobs.handlers

startup.audit.compatibility

startup.privacy.restore-state
```

---

## 84.1 Stabilité

Ces identifiants sont stables pour les dashboards et tests.

---

# 85. Preflight opérateur

Commande conceptuelle :

```text
sitefactory runtime preflight
```

---

## 85.1 Objectif

Valider un environnement avant :

- déploiement ;
- migration ;
- rollout ;
- rollback ;
- restore ;
- handover.

---

## 85.2 Mode lecture seule

Le preflight ne modifie pas :

- base ;
- fichiers métier ;
- providers ;
- comptes ;
- jobs.

---

## 85.3 Résultat

- exit code ;
- rapport JSON ;
- résumé terminal ;
- liens vers runbooks.

---

# 86. Validation du runtime déployé

Commande conceptuelle :

```text
sitefactory runtime validate
```

---

## 86.1 Différence

`preflight` analyse la compatibilité avant l’activation.

`validate` confirme l’état du composant réellement déployé.

---

# 87. Compatibility report

Commande conceptuelle :

```text
sitefactory runtime compatibility report
```

---

## 87.1 Contenu

- release ;
- base ;
- schémas ;
- modules ;
- jobs ;
- API ;
- cache ;
- modes compatibles ;
- blockers ;
- warnings.

---

# 88. Exit codes

Codes conceptuels :

```text
0  → compatible et ready

10 → compatible avec warnings

20 → configuration invalide

21 → secret requis absent

22 → filesystem incompatible

23 → connexion PostgreSQL impossible

24 → identité de base incompatible

25 → rôle PostgreSQL incompatible

26 → schéma incompatible

27 → module ou lock incompatible

28 → contrat de job incompatible

29 → état de sécurité ou restore bloquant

30 → timeout de validation

31 → erreur interne du validateur
```

---

## 88.1 Stabilité

Les scripts opérateur utilisent les codes, pas les messages humains.

---

# 89. Compatibilité de rollback

Le manifest de la release précédente est comparé à l’état courant de la base.

---

## 89.1 Vérification

Un rollback est possible uniquement si :

```text
current database schema
∈ supported schema range of previous release
```

---

## 89.2 Résultat

Le preflight doit pouvoir produire :

```text
rollback-compatible

rollback-incompatible

rollback-unknown
```

---

## 89.3 Autorité

Cette validation ne remplace pas le plan de rollback de l’ADR-0032.

---

# 90. Compatibilité de configuration

Une ancienne release peut ne pas comprendre une nouvelle propriété de configuration.

---

## 90.1 Rollback

Le plan doit :

- conserver l’ancienne configuration compatible ;
- ou générer une configuration pour la release cible ;
- ou bloquer le rollback.

---

# 91. Contrats de jobs et rollback

Une ancienne release peut ne pas comprendre les jobs créés par la nouvelle.

---

## 91.1 Vérification

Avant rollback :

- Job Types actifs ;
- versions de payload ;
- messages outbox ;
- opérations longues ;
- handlers disponibles.

---

## 91.2 Blocage

Un job incompatible non drainé peut rendre le rollback impossible sans migration.

---

# 92. Compatibilité cache et rollback

Le cache local est vidé au redémarrage.

---

## 92.1 Cache Schema Version

Un cache distribué futur doit être compatible ou utiliser un nouveau namespace.

---

## 92.2 Epochs

Les epochs PostgreSQL doivent rester compréhensibles par la release cible.

---

# 93. Compatibilité API

Le runtime vérifie que les versions API déclarées correspondent aux endpoints embarqués.

---

## 93.1 OpenAPI

L’artefact OpenAPI de release est relié au manifest.

---

## 93.2 Route absente

Une route requise par un composant client interne doit être détectée par les tests et preflights concernés.

---

# 94. Browser Security Profiles

Le runtime charge uniquement des profils de sécurité navigateur connus.

---

## 94.1 Version inconnue

Un profil CSP ou header incompatible bloque le web.

---

## 94.2 Développement

Un profil marqué `development` bloque la production.

---

# 95. Egress Manifest

Le runtime vérifie que les intégrations configurées existent dans :

```text
egress-manifest.json
```

---

## 95.1 Destination inconnue

Une destination absente du manifest bloque la capability concernée.

---

## 95.2 Aucun contact

Cette validation n’appelle pas le provider.

---

# 96. Container Release Manifest

Le runtime peut exposer ou vérifier en interne :

- Application Version ;
- Image Digest ;
- Repository Commit ;
- Build ID.

---

## 96.1 Handover

Ces informations alimentent le diagnostic et les rapports.

---

## 96.2 Secret

Aucune credential de registry.

---

# 97. Démarrage du web

Flux détaillé :

```text
charger release manifest

valider configuration web

résoudre secrets web

valider App_Data et Data Protection

se connecter avec le rôle web

vérifier client et environnement

vérifier rôle et privilèges

vérifier schémas

vérifier Identity et audit

vérifier modules web

calculer les capabilities

activer les endpoints

passer ready
```

---

## 97.1 Endpoints avant readiness

Le processus peut écouter afin que les health checks fonctionnent.

Les routes métier retournent :

```text
503 Service Unavailable
```

tant que le startup n’est pas terminé.

---

# 98. Démarrage du worker

Flux détaillé :

```text
charger release manifest

valider configuration worker

résoudre secrets worker

valider volumes nécessaires

se connecter avec le rôle worker

vérifier base, rôle et schéma

inventorier les handlers

comparer les jobs actifs

vérifier outbox

vérifier privacy et media capabilities

passer ready

commencer à revendiquer les jobs
```

---

## 98.1 Invariant

Le worker ne revendique aucun job avant la readiness.

---

# 99. Démarrage du migrator

Flux détaillé :

```text
charger le plan de migration

valider la release

résoudre le secret migrator

vérifier la cible

vérifier le rôle migrator

vérifier SET ROLE owner

vérifier l’état courant du schéma

vérifier le plan attendu

produire le preflight

attendre un apply explicite
```

---

## 99.1 Plan uniquement

Le mode `plan` ne modifie rien.

---

## 99.2 Apply

Le mode `apply` appartient à l’opération de déploiement explicite.

---

# 100. Startup side effects interdits

Le web et le worker ne doivent pas, au démarrage :

```text
envoyer un email

appeler un CRM

générer un contenu AI

lancer un export

supprimer des données expirées

purger un cache externe

créer un administrateur

rotater un secret

appliquer une migration

lancer une sauvegarde

réparer un média

traiter une dead letter
```

---

## 100.1 Après readiness

Ces opérations sont exécutées par leurs workflows normaux.

---

# 101. Actions techniques autorisées

Le startup peut :

- lire la configuration ;
- résoudre un secret local ;
- ouvrir une connexion PostgreSQL ;
- exécuter des lectures de métadonnées ;
- tester un répertoire temporaire ;
- initialiser le cache mémoire ;
- charger des certificats ;
- initialiser le key ring Data Protection ;
- produire un rapport sans secret.

---

# 102. Initialisation Data Protection

La création d’une nouvelle clé Data Protection dans un volume vide est autorisée pour un nouveau projet.

---

## 102.1 Conditions

- volume prévu ;
- permissions correctes ;
- environnement identifié ;
- application name stable ;
- aucune ancienne installation attendue.

---

## 102.2 Projet existant

Un volume soudainement vide produit un warning ou blocker selon le profil et l’historique connu.

---

# 103. Validation parallèle

Certaines validations indépendantes peuvent être exécutées en parallèle.

---

## 103.1 Limite

Le parallélisme reste borné.

---

## 103.2 Ordre logique

Les validations dépendantes attendent leurs prérequis.

Exemple :

```text
database role
```

ne s’exécute qu’après :

```text
database connectivity
```

---

# 104. Graphe de validations

Chaque validation déclare :

```text
ValidationId

Dependencies

ComponentTypes

SeverityPolicy

Timeout

RetryPolicy
```

---

## 104.1 Cycle

Un cycle de dépendances dans le graphe bloque le build ou le démarrage.

---

# 105. Timeout par validation

Chaque validation possède son propre timeout.

---

## 105.1 Exemple

```text
configuration :
quelques secondes

filesystem :
quelques secondes

database connection :
profil de retry borné

provider probe facultative :
timeout court
```

---

## 105.2 Deadline globale

Le timeout local ne dépasse pas la deadline globale.

---

# 106. Cancellation

Le pipeline propage un `CancellationToken`.

---

## 106.1 Arrêt Docker

Un SIGTERM pendant le startup annule proprement les validations.

---

## 106.2 Rapport

L’état devient :

```text
cancelled
```

et non `ready`.

---

# 107. Retry des validations

Une validation peut déclarer :

```text
none

transient-only
```

---

## 107.1 Configurations

Les erreurs de configuration ne sont pas rejouées.

---

## 107.2 PostgreSQL

Les erreurs transitoires de disponibilité peuvent être rejouées.

---

## 107.3 Permission

Une permission refusée n’est pas rejouée.

---

# 108. Cache des résultats de validation

Les résultats peuvent être conservés en mémoire pour les health checks.

---

## 108.1 Source de vérité

Une validation devant être réévaluée périodiquement possède :

- durée de validité ;
- probe dédiée ;
- état de capability.

---

## 108.2 Startup result

Le fait qu’un provider était disponible au démarrage ne garantit pas sa disponibilité future.

---

# 109. Probes runtime

Les dépendances changeantes peuvent être réévaluées après le démarrage.

---

## 109.1 Exemples

- SMTP ;
- CRM ;
- espace disque ;
- Data Protection volume ;
- PostgreSQL ;
- cache invalidation lag.

---

## 109.2 Fréquence

La fréquence est adaptée et bornée.

---

## 109.3 Effet

Une probe ne déclenche aucun effet métier.

---

# 110. PostgreSQL après readiness

Une perte PostgreSQL peut rendre le web non-ready.

---

## 110.1 Liveness

Le processus peut rester live.

---

## 110.2 Readiness

Le trafic métier est refusé ou limité selon le profil.

---

## 110.3 Rétablissement

La readiness peut revenir après une validation réussie.

---

# 111. Rôle ou schéma modifié au runtime

Une dérive de privilèges ou de schéma détectée après readiness doit :

- désactiver les opérations concernées ;
- rendre le composant non-ready si critique ;
- produire une alerte ;
- exiger une intervention.

---

## 111.1 Pas d’auto-correction

Le runtime ne réapplique pas automatiquement les ACL.

---

# 112. Changement de configuration runtime

Une configuration rechargée doit être validée avant activation.

---

## 112.1 Pattern

```text
charger la nouvelle valeur

valider

construire un snapshot

échanger atomiquement

invalider les capabilities concernées
```

---

## 112.2 Échec

La dernière configuration valide reste active lorsque le contrat le permet.

---

## 112.3 Valeur de sécurité

Une révocation ou un kill switch doit prendre effet selon sa propre policy, sans attendre un redémarrage.

---

# 113. Startup et mode maintenance

Lorsqu’un schéma est incompatible ou qu’une migration est requise, le processus peut :

- s’arrêter ;
- ou démarrer en mode maintenance limité.

---

## 113.1 Baseline

La décision dépend du profil de déploiement défini dans l’ADR-0032.

---

## 113.2 Interdiction

Le mode maintenance ne doit pas donner accès à l’administration métier avec un schéma incompatible.

---

# 114. Startup et trafic public

Le site public peut rester disponible dans certains états dégradés.

Exemples :

- SMTP indisponible ;
- CRM indisponible ;
- AI indisponible ;
- worker temporairement non-ready.

---

## 114.1 Conditions

Les formulaires nécessitant un effet différé doivent pouvoir :

- persister localement ;
- créer une outbox ;
- informer correctement l’utilisateur.

---

## 114.2 Base indisponible

Le public ne peut pas accepter une nouvelle soumission si sa persistance n’est pas garantie.

---

# 115. Startup et administration

L’administration nécessite :

- Identity compatible ;
- Data Protection ;
- sessions ;
- permissions ;
- audit selon les actions ;
- no restore pending.

---

## 115.1 Public disponible, admin bloquée

Ce mode peut être autorisé lorsque la panne est limitée à l’administration.

---

# 116. Startup et workers multiples

Avant une future activation multi-instance :

- les workers doivent partager le même schéma de jobs ;
- les handlers doivent être compatibles ;
- les leases doivent être valides ;
- les versions concurrentes doivent être autorisées.

---

## 116.1 Baseline

Le MVP privilégie un worker principal.

---

# 117. Compatibilité de versions coexistantes

Deux releases ne doivent fonctionner simultanément que si leurs manifests déclarent une compatibilité mutuelle avec :

- schéma ;
- jobs ;
- outbox ;
- cache ;
- API interne ;
- modules.

---

## 117.1 Baseline MVP

Le déploiement remplace les composants de façon contrôlée et ne suppose pas une coexistence longue.

---

# 118. Détection de dérive

Le runtime compare :

```text
attendu par la release

réel dans l’environnement
```

---

## 118.1 Périmètre

- configuration ;
- secrets attendus ;
- base ;
- rôle ;
- schéma ;
- modules ;
- jobs ;
- epochs ;
- manifests ;
- volumes.

---

## 118.2 Rapport

Une dérive possède :

- Validation ID ;
- attendu ;
- valeur réelle neutralisée ;
- sévérité ;
- runbook.

---

# 119. Valeurs réelles neutralisées

Le rapport peut afficher :

```text
expected role profile:
database.web-runtime.v1

actual role profile:
database.migrator.v1
```

mais pas :

- mot de passe ;
- chaîne de connexion ;
- hostname sensible si inutile.

---

# 120. Observabilité

Événements recommandés :

```text
RuntimeStartupStarted

RuntimeStartupValidationStarted

RuntimeStartupValidationCompleted

RuntimeStartupBlocked

RuntimeStartupReady

RuntimeCapabilityChanged

RuntimeCompatibilityMismatch

RuntimeDatabaseIdentityMismatch

RuntimeDatabaseRoleMismatch

RuntimeSchemaMismatch

RuntimeJobContractMismatch

RuntimeRestorePendingDetected
```

---

## 120.1 Propriétés autorisées

```text
StartupId

ComponentType

ValidationId

CapabilityId

ApplicationVersion

RuntimeMode

Result

ErrorCode

Environment

Duration
```

---

## 120.2 Données interdites

```text
Secret value

Connection string

Password

Token

Cookie

Private key

Full configuration

Business content
```

---

# 121. Métriques

Exemples :

```text
runtime startup duration

runtime startup failures

runtime startup validations

runtime readiness state

runtime capability state

runtime compatibility failures

runtime database mismatches

runtime job-contract mismatches

runtime restart count
```

---

## 121.1 Labels autorisés

```text
component

validation_category

capability

result

error_category

environment
```

---

## 121.2 Labels interdits

```text
StartupId

DatabaseInstanceId

Role name libre

SecretId

Path

TraceId

Client data
```

---

# 122. Alertes

Alertes possibles :

```text
Démarrage bloqué

Boucle de redémarrage

Mauvais rôle PostgreSQL

Mauvais environnement

Schéma incompatible

Migration partielle

Restore pending

Handler de job manquant

Data Protection indisponible

App_Data non inscriptible

Capability critique désactivée

Release manifest incohérent
```

---

## 122.1 Runbook

Chaque validation bloquante doit référencer un runbook.

---

# 123. Audit

Les démarrages ordinaires ne produisent pas nécessairement un événement dans la piste d’audit métier.

---

## 123.1 Actions auditables

- activation d’un mode maintenance ;
- changement manuel de runtime mode ;
- dérogation de compatibilité ;
- validation forcée ;
- réconciliation après restore ;
- correction de rôle ;
- application de migration ;
- activation après échec bloquant.

---

## 123.2 Logs techniques

Les démarrages et échecs appartiennent principalement aux logs et métriques techniques.

---

# 124. Dérogations

Une incompatibilité bloquante ne peut pas être contournée par une simple variable :

```text
IGNORE_STARTUP_ERRORS=true
```

---

## 124.1 Exception opérateur

Une exception rare exige :

- Validation ID ;
- justification ;
- durée ;
- owner ;
- risques ;
- contrôles compensatoires ;
- approbation ;
- audit.

---

## 124.2 Invariants non dérogeables

Ne sont pas dérogeables dans la baseline :

```text
mauvais client

mauvais environnement

superuser runtime

schéma plus récent inconnu

restore pending non réconcilié

secret critique absent

migration partielle
```

---

# 125. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-STARTUP-001` | Pipeline de démarrage non initialisé |
| `SF-STARTUP-002` | Validation de démarrage échouée |
| `SF-STARTUP-003` | Validation bloquante |
| `SF-STARTUP-004` | Deadline de démarrage dépassée |
| `SF-STARTUP-005` | Démarrage annulé |
| `SF-STARTUP-006` | Graphe de validations invalide |
| `SF-STARTUP-007` | Dépendance de validation absente |
| `SF-STARTUP-008` | Rapport de démarrage absent |
| `SF-STARTUP-009` | Mode runtime inconnu |
| `SF-STARTUP-010` | Démarrage avec profil development interdit |
| `SF-COMPAT-001` | Manifest de compatibilité absent |
| `SF-COMPAT-002` | Version de manifest non supportée |
| `SF-COMPAT-003` | Release manifest incohérent |
| `SF-COMPAT-004` | Application Version incompatible |
| `SF-COMPAT-005` | Runtime Profile incompatible |
| `SF-COMPAT-006` | Configuration Schema incompatible |
| `SF-COMPAT-007` | Client Lock incompatible |
| `SF-COMPAT-008` | Module Lock Hash incompatible |
| `SF-COMPAT-009` | Compatibilité rollback inconnue |
| `SF-COMPAT-010` | Rollback incompatible |
| `SF-CONFIG-STARTUP-001` | Section de configuration absente |
| `SF-CONFIG-STARTUP-002` | Propriété inconnue |
| `SF-CONFIG-STARTUP-003` | Valeur invalide |
| `SF-CONFIG-STARTUP-004` | Valeur de développement en production |
| `SF-CONFIG-STARTUP-005` | Public Base URL invalide |
| `SF-CONFIG-STARTUP-006` | Override non autorisé |
| `SF-SECRET-STARTUP-001` | Secret requis absent |
| `SF-SECRET-STARTUP-002` | Secret non résoluble |
| `SF-SECRET-STARTUP-003` | Secret vide |
| `SF-SECRET-STARTUP-004` | Secret du mauvais environnement |
| `SF-SECRET-STARTUP-005` | Secret monté sur le mauvais composant |
| `SF-FS-STARTUP-001` | Chemin runtime absent |
| `SF-FS-STARTUP-002` | Chemin non lisible |
| `SF-FS-STARTUP-003` | Chemin non inscriptible |
| `SF-FS-STARTUP-004` | Ownership incompatible |
| `SF-FS-STARTUP-005` | Espace disque insuffisant |
| `SF-FS-STARTUP-006` | Probe d’écriture échouée |
| `SF-FS-STARTUP-007` | Root filesystem mutable inattendu |
| `SF-DB-STARTUP-001` | Connexion PostgreSQL impossible |
| `SF-DB-STARTUP-002` | Base attendue absente |
| `SF-DB-STARTUP-003` | Database Identity absente |
| `SF-DB-STARTUP-004` | Mauvais Client Slug |
| `SF-DB-STARTUP-005` | Mauvais environnement |
| `SF-DB-STARTUP-006` | Database Instance ID incompatible |
| `SF-DB-STARTUP-007` | Rôle PostgreSQL incompatible |
| `SF-DB-STARTUP-008` | Rôle PostgreSQL trop privilégié |
| `SF-DB-STARTUP-009` | Privilège PostgreSQL requis absent |
| `SF-SCHEMA-STARTUP-001` | Schema State absent |
| `SF-SCHEMA-STARTUP-002` | Schéma trop ancien |
| `SF-SCHEMA-STARTUP-003` | Schéma trop récent |
| `SF-SCHEMA-STARTUP-004` | Migration requise |
| `SF-SCHEMA-STARTUP-005` | Migration en cours |
| `SF-SCHEMA-STARTUP-006` | Migration échouée |
| `SF-SCHEMA-STARTUP-007` | Migration inconnue |
| `SF-SCHEMA-STARTUP-008` | Historique de migration incohérent |
| `SF-MODULE-STARTUP-001` | Module attendu absent |
| `SF-MODULE-STARTUP-002` | Module non déclaré présent |
| `SF-MODULE-STARTUP-003` | Version de module incompatible |
| `SF-MODULE-STARTUP-004` | Schéma module incompatible |
| `SF-MODULE-STARTUP-005` | Contrat public module incompatible |
| `SF-JOB-STARTUP-001` | Handler de job absent |
| `SF-JOB-STARTUP-002` | Version de Job Contract incompatible |
| `SF-JOB-STARTUP-003` | Job actif non interprétable |
| `SF-JOB-STARTUP-004` | Message outbox non interprétable |
| `SF-JOB-STARTUP-005` | Worker non-ready |
| `SF-IDENTITY-STARTUP-001` | Session Epoch absent |
| `SF-IDENTITY-STARTUP-002` | Recovery Epoch absent |
| `SF-IDENTITY-STARTUP-003` | Data Protection indisponible |
| `SF-IDENTITY-STARTUP-004` | Key ring incompatible |
| `SF-AUDIT-STARTUP-001` | Audit Schema incompatible |
| `SF-AUDIT-STARTUP-002` | Fonction append indisponible |
| `SF-AUDIT-STARTUP-003` | Audit integrity invalide |
| `SF-PRIVACY-STARTUP-001` | Privacy Schema incompatible |
| `SF-PRIVACY-STARTUP-002` | Deletion Ledger indisponible |
| `SF-PRIVACY-STARTUP-003` | Restore pending |
| `SF-PRIVACY-STARTUP-004` | Reconciliation requise |
| `SF-CACHE-STARTUP-001` | Cache Schema incompatible |
| `SF-CACHE-STARTUP-002` | Global Cache Epoch absent |
| `SF-CAPABILITY-001` | Capability inconnue |
| `SF-CAPABILITY-002` | Capability non configurée |
| `SF-CAPABILITY-003` | Capability indisponible |
| `SF-CAPABILITY-004` | Capability bloquée |
| `SF-HEALTH-STARTUP-001` | Liveness indisponible |
| `SF-HEALTH-STARTUP-002` | Readiness indisponible |
| `SF-HEALTH-STARTUP-003` | Détail de health check exposé |
| `SF-HEALTH-STARTUP-004` | Readiness activée trop tôt |

---

# 126. Tests unitaires

Tester notamment :

- graphe de validations ;
- ordre ;
- dépendances ;
- sévérité ;
- timeouts ;
- retry classification ;
- aggregation du rapport ;
- calcul du Runtime Mode ;
- calcul des capabilities ;
- exit codes ;
- redaction.

---

# 127. Tests de configuration

Couvrir :

```text
section absente

propriété inconnue

valeur invalide

profil development en production

Public Base URL HTTP

override interdit

configuration staging en production

valeur par défaut sûre

valeur critique sans défaut
```

---

# 128. Tests de secrets

Vérifier :

- secret présent ;
- secret absent ;
- secret vide ;
- secret inaccessible ;
- mauvais environnement ;
- secret monté au mauvais service ;
- absence de valeur dans les logs et rapports.

---

# 129. Tests filesystem

Exécuter avec :

- root filesystem read-only ;
- App_Data accessible ;
- App_Data inaccessible ;
- key ring read-only ;
- tmpfs absent ;
- espace insuffisant ;
- mauvais UID/GID ;
- permissions trop larges.

---

# 130. Tests PostgreSQL réels

Utiliser une vraie instance PostgreSQL.

Couvrir :

- base correcte ;
- mauvaise base ;
- mauvais client ;
- mauvais environnement ;
- mauvais rôle ;
- rôle superuser ;
- rôle owner ;
- privilège manquant ;
- timeout ;
- démarrage différé de PostgreSQL.

---

# 131. Tests de schéma

Couvrir :

```text
version exacte

version minimale compatible

version maximale compatible

schéma trop ancien

schéma trop récent

migration en cours

migration échouée

migration inconnue

historique EF incohérent
```

---

# 132. Tests d’absence de migration automatique

Démarrer le web et le worker avec un schéma ancien.

Résultat attendu :

```text
non-ready

aucune migration appliquée

aucun objet créé
```

---

# 133. Tests de modules

Vérifier :

- lock correct ;
- hash incorrect ;
- module absent ;
- module supplémentaire ;
- version incompatible ;
- schéma module incompatible ;
- contrat public incompatible.

---

# 134. Tests de jobs

Créer dans PostgreSQL :

- Job Type connu ;
- version compatible ;
- version future ;
- type inconnu ;
- outbox inconnue ;
- dead letter ancienne.

Vérifier le comportement du worker.

---

# 135. Tests de restore

Scénario :

1. restaurer une base ;
2. laisser `restore-pending` ;
3. démarrer le web.

Résultat :

```text
mode normal refusé
readiness bloquée
```

Puis :

1. terminer les reconciliations ;
2. créer les nouveaux epochs ;
3. valider le rapport ;
4. redémarrer.

Résultat :

```text
ready
```

---

# 136. Tests Data Protection

Couvrir :

- key ring existant ;
- volume neuf pour nouveau projet ;
- volume vide inattendu ;
- volume non inscriptible ;
- application name incompatible ;
- clés d’un autre environnement.

---

# 137. Tests des capabilities

Vérifier :

```text
SMTP absent
→ email unavailable
→ public site ready

CRM indisponible
→ CRM degraded
→ lead persistence available

audit critique absent
→ admin sensitive operations blocked

PostgreSQL absent
→ web non-ready
```

---

# 138. Tests d’absence d’effets externes

Instrumenter des providers fakes.

Démarrer le web et le worker.

Vérifier qu’aucun appel n’est envoyé à :

- SMTP ;
- CRM ;
- AI ;
- webhook ;
- object storage externe.

---

# 139. Tests de health checks

Vérifier :

```text
processus actif + DB indisponible
→ live
→ not ready

startup en cours
→ live
→ not ready

startup terminé
→ live
→ ready

validation bloquante
→ live ou process exited selon profil
→ not ready
```

---

# 140. Tests d’exposition

Le health check public ne doit pas exposer :

- base ;
- rôle ;
- schéma ;
- modules ;
- secrets ;
- paths ;
- exceptions.

---

# 141. Tests de timeout

Simuler :

- connexion PostgreSQL lente ;
- filesystem bloqué ;
- validateur ne répondant pas ;
- cancellation ;
- deadline globale.

Vérifier :

- arrêt borné ;
- rapport ;
- readiness non activée ;
- aucun thread bloqué indéfiniment.

---

# 142. Tests de restart loop

Démarrer plusieurs fois avec une configuration invalide.

Vérifier :

- diagnostic stable ;
- aucun effet accumulé ;
- aucun fichier métier créé ;
- aucune migration ;
- aucune notification.

---

# 143. Tests de compatibilité rollback

Pour deux releases fictives :

- release N ;
- release N+1 ;

tester :

- schéma compatible avec N ;
- schéma uniquement compatible avec N+1 ;
- nouveaux jobs incompatibles ;
- nouvelle configuration incompatible ;
- Cache Schema Version.

---

# 144. Tests de démarrage web/worker

Vérifier que :

- le web n’utilise pas les secrets worker ;
- le worker ne revendique rien avant readiness ;
- le web refuse le rôle worker ;
- le worker refuse le rôle web lorsque requis ;
- les capabilities diffèrent correctement.

---

# 145. Tests multiplateformes

Les validateurs, manifests et commandes fonctionnent sur :

```text
Windows x64

Linux x64
```

Les tests Docker, PostgreSQL, permissions et health checks de production sont validés sur Linux.

---

# 146. Quality gates

Une contribution est bloquée lorsque :

```text
Le web appelle Database.Migrate au démarrage.

Le worker commence à revendiquer des jobs
avant la readiness.

Un module exécute un appel externe au démarrage.

Une validation imprime une configuration complète.

Une valeur secrète apparaît dans un rapport.

Une validation bloquante peut être ignorée
par une option non contrôlée.

Le runtime ne vérifie pas Client et Environment.

Le runtime ne vérifie pas current_user.

Le runtime accepte un rôle owner ou migrator.

Le runtime accepte un schéma plus récent inconnu.

Le lock de modules n’est pas comparé.

Les contrats de jobs ne sont pas vérifiés.

Un restore pending est ignoré.

La readiness est activée avant la fin du pipeline.

La liveness dépend d’un provider externe.
```

---

# 147. Quality gates de production

La production est bloquée lorsque :

```text
Le runtime compatibility manifest est absent.

Le release manifest ne correspond pas à l’image.

La Database Identity est absente.

La base cible ne correspond pas au client.

La base cible ne correspond pas à l’environnement.

Le rôle PostgreSQL attendu n’est pas utilisé.

Une migration requise n’est pas appliquée.

Une migration est en état failed ou running.

Le restore state n’est pas stable.

Les nouveaux epochs requis sont absents.

Le worker ne reconnaît pas des jobs actifs.

Le Data Protection key ring est incompatible.

Une capability de sécurité critique est indisponible.

Le preflight n’a pas produit un résultat compatible.
```

---

# 148. CI/CD

La CI doit exécuter :

```text
validation du manifest

validation de configuration

validation des Secret IDs

tests filesystem read-only

tests Database Identity

tests des rôles PostgreSQL

tests de versions de schéma

tests des modules et locks

tests des contrats de jobs

tests de restore pending

tests des health checks

tests de redaction

tests d’absence d’effets externes

tests de rollback compatibility
```

---

## 148.1 Artefacts

La release conserve :

```text
runtime-compatibility-manifest.json

container-release-manifest.json

sitefactory.lock.json

OpenAPI

database migration plan

startup validation schemas
```

---

# 149. Analyse statique

La CI peut rechercher :

```text
Database.Migrate

EnsureCreated

appel provider dans IHostedService.StartAsync

envoi SMTP dans le startup

création de compte dans le bootstrap web

logs de IConfiguration

logs de variables d’environnement

health check retournant une exception complète

worker démarré avant validation
```

---

# 150. Règles pour les modules

Chaque module doit déclarer :

```text
StartupValidationIds

RequiredConfiguration

RequiredSecretIds

RequiredSchemas

SupportedSchemaVersions

RequiredDatabasePrivileges

RequiredFilesystemPaths

JobContractVersions

RequiredCapabilities

OptionalCapabilities
```

---

## 150.1 Interdictions

Un module ne doit pas :

- réparer automatiquement son schéma ;
- créer un rôle ;
- créer un administrateur ;
- appeler un provider au startup ;
- rendre tout le site indisponible pour une capability optionnelle ;
- ignorer une version inconnue.

---

# 151. Règles pour les customizations

Une customization client suit les mêmes contrats.

---

## 151.1 Validation

Elle doit fournir :

- Validation ID namespace client ;
- sévérité ;
- timeout ;
- dépendances ;
- tests ;
- runbook.

---

## 151.2 Exemple

```text
client.sample-project.startup.quote-provider
```

---

# 152. Règles pour LogicGate

LogicGate doit :

- maintenir les manifests ;
- maintenir les validateurs ;
- exécuter les preflights ;
- surveiller les readiness failures ;
- corriger les dérives ;
- documenter les runbooks ;
- vérifier les restores ;
- tester les rollbacks ;
- préparer le handover.

---

## 152.1 LogicGate ne doit pas

- ignorer un blocker pour accélérer un déploiement ;
- appliquer une migration depuis le web ;
- remplacer un secret absent par une valeur faible ;
- modifier l’identité de la base pour correspondre à l’application ;
- lancer les workers avec des handlers manquants ;
- exposer les détails des health checks publiquement.

---

# 153. Règles pour Codex

Codex peut :

- définir les contrats ;
- créer les validateurs ;
- générer les manifests ;
- ajouter les health checks ;
- ajouter les tests ;
- créer les commandes preflight ;
- documenter les runbooks.

---

## 153.1 Codex ne doit pas

```text
Démarrer une migration réelle

Modifier une Database Identity réelle

Inventer un secret

Contourner une validation bloquante

Ajouter un flag ignore-all-errors

Appeler un provider de production

Modifier une readiness de production

Créer un administrateur

Réconcilier un restore réel

Déployer une release
```

---

## 153.2 Décision humaine

Une décision humaine est requise pour :

- nouvelle validation bloquante ;
- nouvelle fenêtre de compatibilité ;
- dérogation ;
- changement d’un Runtime Mode ;
- nouvelle capability critique ;
- modification de la deadline production ;
- changement de Database Identity ;
- activation après restore ;
- rollback déclaré compatible.

---

# 154. Anti-patterns interdits

## 154.1 Migration automatique

```csharp
await dbContext.Database.MigrateAsync();
```

dans `Program.cs`.

---

## 154.2 `SELECT 1` uniquement

La readiness ignore le client, le rôle et le schéma.

---

## 154.3 Admin seed

Le web crée un compte si la table est vide.

---

## 154.4 Appel SMTP au startup

Le processus envoie un email de test à chaque redémarrage.

---

## 154.5 Dump de configuration

```csharp
logger.LogInformation(configuration.AsEnumerable());
```

---

## 154.6 Health check détaillé public

La réponse expose :

- PostgreSQL host ;
- role ;
- version ;
- stack trace.

---

## 154.7 Rôle trop puissant toléré

Le web démarre avec le migrator parce que « cela fonctionne ».

---

## 154.8 Ignore flag

```text
SKIP_COMPATIBILITY_CHECKS=true
```

dans la production.

---

## 154.9 Worker optimiste

Le worker revendique les jobs puis découvre que leur payload est incompatible.

---

## 154.10 Restore automatique

Le web détecte un restore et tente de recréer les epochs sans procédure.

---

## 154.11 Provider bloquant

Une panne analytics empêche le site public de démarrer.

---

## 154.12 Readiness immédiate

Le service est déclaré ready avant les validations PostgreSQL.

---

# 155. Conséquences positives

La décision apporte :

- démarrages prévisibles ;
- incompatibilités détectées tôt ;
- protection contre la mauvaise base ;
- protection contre les rôles excessifs ;
- migrations explicites ;
- jobs protégés ;
- restore sécurisé ;
- health checks cohérents ;
- modes dégradés contrôlés ;
- diagnostics standardisés ;
- meilleure préparation des rollbacks ;
- handover facilité.

---

# 156. Conséquences négatives

La décision entraîne :

- manifests supplémentaires ;
- validateurs ;
- temps de démarrage ;
- rapports ;
- tests d’intégration ;
- gestion des capabilities ;
- états runtime ;
- davantage de discipline sur les versions ;
- risque de faux blocage si un validateur est mal conçu.

---

# 157. Risques acceptés

## 157.1 Démarrage légèrement plus long

Les validations ajoutent quelques secondes.

Ce coût est accepté afin d’éviter une incompatibilité découverte après exposition au trafic.

---

## 157.2 Capability facultative indisponible

Une fonctionnalité peut être désactivée temporairement.

Le site reste utilisable lorsqu’aucune garantie critique n’est réduite.

---

## 157.3 PostgreSQL transitoirement lent

Le composant peut rester non-ready pendant la fenêtre de retry.

Le délai reste borné.

---

## 157.4 Faux positif de compatibilité

Un validateur peut bloquer un cas réellement compatible.

Réduction :

- tests ;
- versionnement ;
- preflight staging ;
- revue ;
- dérogation structurée hors invariants non dérogeables.

---

# 158. Mesures compensatoires

- manifest versionné ;
- Database Identity ;
- rôle PostgreSQL vérifié ;
- schema range ;
- migrations explicites ;
- module lock ;
- job contract checks ;
- restore state ;
- capability registry ;
- readiness ;
- deadlines ;
- retries bornés ;
- reports ;
- runbooks ;
- quality gates.

---

# 159. Performance

## 159.1 Requêtes de métadonnées

Les validations PostgreSQL utilisent des requêtes bornées et indexées.

---

## 159.2 Pas de scans complets

Le startup ne scanne pas :

- tous les leads ;
- tous les médias ;
- tous les événements d’audit ;
- tous les backups.

---

## 159.3 Jobs actifs

La vérification des contrats utilise des agrégats ou index adaptés.

---

## 159.4 Parallélisme

Les validations indépendantes peuvent réduire le temps total, avec concurrence bornée.

---

# 160. Coûts

## 160.1 Coûts supplémentaires

- développement ;
- maintenance ;
- tests ;
- rapports ;
- instrumentation ;
- preflight ;
- support opérationnel.

---

## 160.2 Coûts évités

- mauvaise base modifiée ;
- migration implicite ;
- production partiellement compatible ;
- jobs perdus ;
- rollback impossible découvert trop tard ;
- secrets manquants ;
- incident de permissions ;
- restore ouvert trop tôt.

---

# 161. Maintenabilité

Pour chaque validation, il doit être possible de répondre :

```text
Quel est son Validation ID ?

Quel composant concerne-t-elle ?

De quoi dépend-elle ?

Est-elle bloquante ?

Quelle capability affecte-t-elle ?

Quel timeout ?

Quel retry ?

Quelle donnée lit-elle ?

Produit-elle un effet ?

Quel Error Code ?

Quel runbook ?

Quels tests ?
```

---

# 162. Compatibilité

## 162.1 Manifest Schema

Le manifest possède une version.

---

## 162.2 Ancienne release

Une ancienne release rencontrant une version de manifest future doit refuser de l’interpréter arbitrairement.

---

## 162.3 Schémas

Les fenêtres de compatibilité sont explicites.

---

## 162.4 Modules

Chaque module possède ses propres versions de contrats et schémas.

---

## 162.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

L’introduction dans un projet existant peut révéler des incompatibilités auparavant ignorées.

---

# 163. Migration depuis un démarrage optimiste

Le processus est :

```text
inventorier les dépendances

créer le release manifest

créer la Database Identity

créer le Schema State

ajouter les validations de rôle

ajouter les validations modules et jobs

séparer liveness et readiness

bloquer le trafic avant ready

retirer les initialisations automatiques

ajouter les tests
```

---

# 164. Migration depuis les migrations automatiques

Le processus est :

```text
retirer Database.Migrate du runtime

créer l’image migrator

créer plan et apply

créer Schema State

ajouter le preflight

tester le déploiement

tester le rollback
```

---

# 165. Migration depuis un health check simple

Le processus est :

```text
conserver /health/live minimal

créer /health/ready

ajouter startup report

ajouter Database Identity

ajouter rôle et schéma

ajouter les capabilities

neutraliser les détails publics
```

---

# 166. Transfert client

Le handover doit inclure :

- runtime compatibility manifest ;
- release manifest ;
- Database Identity ;
- Schema State ;
- module lock ;
- Validation IDs ;
- Runtime Modes ;
- capability definitions ;
- health endpoints ;
- startup deadlines ;
- exit codes ;
- rapports ;
- runbooks.

---

## 166.1 Secrets

Les valeurs de secrets ne sont pas incluses.

Seuls les Secret IDs à reprovisionner sont documentés.

---

# 167. Clôture de service

La clôture doit :

```text
produire un dernier compatibility report

identifier les capabilities externes

transférer les manifests

transférer les runbooks

valider les health checks côté client

révoquer les accès LogicGate

documenter les blockers résiduels

auditer le handover
```

---

# 168. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de démarrer ou arrêter un service réel ;
- d’appliquer une migration ;
- de modifier une Database Identity ;
- de modifier un Runtime Mode ;
- d’ignorer un blocker ;
- de réconcilier une restauration ;
- de rotater un secret ;
- de déployer une release.

---

# 169. Réversibilité

## 169.1 Classification

```text
élevée avant activation

moyenne après dépendance des déploiements aux manifests

faible si les validations sont supprimées
et qu’une incompatibilité produit des écritures
```

---

## 169.2 Désactivation

La désactivation globale des validations n’est pas un rollback acceptable.

---

## 169.3 Correction

Une validation défectueuse est corrigée ou versionnée.

---

# 170. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- Runtime Component ;
- Runtime Mode ;
- Validation ID ;
- Startup Result ;
- Capability State ;
- Error Codes.

---

## Étape 2 — Manifest

Créer :

```text
runtime-compatibility-manifest.json
```

et son schéma.

---

## Étape 3 — Configuration et secrets

Ajouter :

- validation typée ;
- propriétés inconnues ;
- Secret IDs ;
- redaction.

---

## Étape 4 — Filesystem

Ajouter :

- probes ;
- permissions ;
- espace ;
- Data Protection ;
- App_Data.

---

## Étape 5 — PostgreSQL

Ajouter :

- Database Identity ;
- rôle ;
- privilèges ;
- Schema State ;
- migrations.

---

## Étape 6 — Modules et jobs

Ajouter :

- lock hash ;
- modules ;
- contrats ;
- jobs actifs ;
- outbox.

---

## Étape 7 — Security state

Ajouter :

- audit ;
- privacy ;
- restore state ;
- epochs ;
- cache state.

---

## Étape 8 — Capabilities

Créer :

- registre ;
- calcul ;
- modes dégradés ;
- exposition contrôlée.

---

## Étape 9 — Health checks

Ajouter :

- liveness ;
- readiness ;
- profils composant ;
- réponses publiques minimales.

---

## Étape 10 — Preflight et quality gates

Ajouter :

- CLI ;
- rapports ;
- exit codes ;
- tests ;
- compatibilité rollback.

---

# 171. Tâches liées proposées

```text
SF-TASK-454 — Define runtime-startup validation contracts

SF-TASK-455 — Define runtime modes and capability states

SF-TASK-456 — Generate the runtime compatibility manifest

SF-TASK-457 — Add strict startup configuration validation

SF-TASK-458 — Add component-scoped secret validation

SF-TASK-459 — Add startup filesystem and volume probes

SF-TASK-460 — Add persistent database-instance identity

SF-TASK-461 — Add runtime PostgreSQL role verification

SF-TASK-462 — Add database schema compatibility validation

SF-TASK-463 — Add partial-migration state detection

SF-TASK-464 — Add module-lock and module-schema validation

SF-TASK-465 — Add active job-contract compatibility checks

SF-TASK-466 — Add audit and privacy startup validation

SF-TASK-467 — Add restore-pending startup blocking

SF-TASK-468 — Add runtime capability registry

SF-TASK-469 — Add separate liveness and readiness endpoints

SF-TASK-470 — Add component-specific readiness profiles

SF-TASK-471 — Add bounded startup retries and deadlines

SF-TASK-472 — Add runtime preflight and compatibility commands

SF-TASK-473 — Add sanitized startup validation reports

SF-TASK-474 — Add rollback-compatibility preflight checks

SF-TASK-475 — Add startup compatibility and no-side-effect tests
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 172. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Chaque release possède un runtime compatibility manifest.

Le manifest correspond au digest de la release.

Les configurations sont validées avant readiness.

Les propriétés SiteFactory inconnues sont détectées.

Les secrets requis sont validés sans être divulgués.

Le root filesystem peut rester read-only.

App_Data et les volumes requis sont vérifiés.

La Database Identity correspond au client.

La Database Identity correspond à l’environnement.

Le web utilise le rôle PostgreSQL attendu.

Le worker utilise le rôle PostgreSQL attendu.

Un rôle owner, migrator ou superuser bloque le runtime.

Le schéma doit appartenir à la plage supportée.

Un schéma trop ancien bloque le démarrage.

Un schéma trop récent bloque le démarrage.

Une migration partielle bloque le démarrage.

Le web et le worker n’appliquent aucune migration.

Le module lock correspond aux modules embarqués.

Les versions de schémas des modules sont compatibles.

Le worker reconnaît tous les jobs actifs requis.

Un job inconnu n’est ni revendiqué ni supprimé.

L’état audit est vérifié.

Le restore pending bloque le mode normal.

Les nouveaux epochs sont vérifiés après restore.

Les capabilities facultatives peuvent être désactivées
sans bloquer le site public.

Les capabilities critiques utilisent fail-closed.

Aucun provider externe n’est appelé automatiquement.

La liveness ne dépend pas des fournisseurs.

La readiness n’est activée qu’après les validations.

Le worker ne revendique aucun job avant readiness.

Les health checks publics ne divulguent aucun détail.

Les retries de démarrage sont bornés.

Le preflight produit un rapport sans secret.

La compatibilité de rollback peut être évaluée.

Les tests fonctionnent sur Windows et Linux.
```

---

# 173. Conditions de révision

Réexaminer cette décision si :

- Kubernetes devient la baseline ;
- plusieurs instances de versions différentes coexistent longtemps ;
- une plateforme de configuration distante est introduite ;
- PostgreSQL devient managé avec des contraintes spécifiques ;
- un système de service discovery est ajouté ;
- les jobs sont transférés vers un broker externe ;
- une architecture multi-tenant est adoptée ;
- des bases multiples participent au runtime ;
- un feature store ou moteur de recherche devient obligatoire ;
- un contrôle de policy central est introduit ;
- la durée de démarrage devient incompatible avec l’exploitation ;
- un orchestrateur fournit des startup probes avancées.

---

# 174. Indicateurs à suivre

- durée de démarrage ;
- validations exécutées ;
- validations échouées ;
- blockers ;
- warnings ;
- readiness delay ;
- restart loops ;
- incompatibilités de schéma ;
- rôles incorrects ;
- mauvaises bases ;
- jobs incompatibles ;
- restore pending ;
- capabilities indisponibles ;
- health check failures ;
- deadline expirée ;
- preflights échoués ;
- rollbacks incompatibles.

---

# 175. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
les validations sont régulièrement ignorées

les démarrages dépassent souvent la deadline

les health checks deviennent trop détaillés

des providers sont appelés au startup

les workers revendiquent avant readiness

des rôles excessifs sont tolérés

des migrations automatiques réapparaissent

les manifests divergent des images

les jobs incompatibles s’accumulent

des restores sont ouverts avant réconciliation

les faux positifs conduisent à désactiver les contrôles

les rollbacks sont déclarés compatibles sans test
```

---

# 176. Conséquences observées

Cette section est append-only après mise en œuvre.

## 176.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
du runtime compatibility manifest,
du pipeline de startup validation
et des readiness profiles.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- composant ;
- Validation ID ;
- durée ;
- résultat ;
- Runtime Mode ;
- incident ou bénéfice ;
- action proposée.

---

# 177. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [generated-runtime-database-model.md](../03-generation/generated-runtime-database-model.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- [15-cli-specification.md](../03-generation/15-cli-specification.md)
- [18-security-baseline.md](../04-quality-security/18-security-baseline.md)
- [19-testing-strategy.md](../04-quality-security/19-testing-strategy.md)
- [20-quality-gates-and-ci.md](../04-quality-security/20-quality-gates-and-ci.md)
- [21-observability-and-logging.md](../05-operations/21-observability-and-logging.md)
- [22-deployment-and-environments.md](../05-operations/22-deployment-and-environments.md)
- [23-backup-and-restore.md](../05-operations/23-backup-and-restore.md)
- [24-operational-runbooks.md](../05-operations/24-operational-runbooks.md)
- [25-upgrade-and-maintenance-strategy.md](../05-operations/25-upgrade-and-maintenance-strategy.md)
- [26-release-and-versioning.md](../05-operations/26-release-and-versioning.md)
- [27-client-handover-and-service-closure.md](../05-operations/27-client-handover-and-service-closure.md)
- [28-codex-operating-guide.md](../06-codex/28-codex-operating-guide.md)
- [0000-adr-template.md](0000-adr-template.md)
- [0001-one-repository-and-database-per-client.md](0001-one-repository-and-database-per-client.md)
- [0003-postgresql-as-primary-database.md](0003-postgresql-as-primary-database.md)
- [0004-explicit-production-migrations.md](0004-explicit-production-migrations.md)
- [0005-file-generation-strategies.md](0005-file-generation-strategies.md)
- [0006-feature-flags-and-entitlements-separation.md](0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](0007-module-composition-and-public-contracts.md)
- [0008-private-package-feed-and-supply-chain.md](0008-private-package-feed-and-supply-chain.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](0012-runtime-configuration-and-secrets.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](0014-explicit-client-upgrades-and-version-pinning.md)
- [0016-content-and-media-storage-boundaries.md](0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](0017-background-jobs-and-reliable-side-effects.md)
- [0018-public-form-abuse-protection-and-rate-limiting.md](0018-public-form-abuse-protection-and-rate-limiting.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](0019-audit-trail-and-sensitive-administrative-actions.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](0021-administrator-provisioning-invitations-and-account-recovery.md)
- [0022-browser-security-headers-and-content-security-policy.md](0022-browser-security-headers-and-content-security-policy.md)
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0024-client-data-export-and-portability.md](0024-client-data-export-and-portability.md)
- [0025-third-party-integrations-and-egress-control.md](0025-third-party-integrations-and-egress-control.md)
- [0026-container-image-build-and-runtime-hardening.md](0026-container-image-build-and-runtime-hardening.md)
- [0027-database-roles-and-runtime-least-privilege.md](0027-database-roles-and-runtime-least-privilege.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- [0029-http-api-contracts-problem-details-and-idempotent-commands.md](0029-http-api-contracts-problem-details-and-idempotent-commands.md)
- [0030-cache-strategy-and-invalidation.md](0030-cache-strategy-and-invalidation.md)

---

# 178. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 173 |

---

# 179. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 180. Synthèse de la décision

```text
Release
→ runtime compatibility manifest
→ versions
→ digests
→ modules
→ schémas
→ contrats
→ capabilities

Startup
→ configuration
→ secrets
→ filesystem
→ PostgreSQL
→ identité de base
→ rôle
→ schéma
→ modules
→ jobs
→ sécurité
→ readiness

Configuration
→ options typées
→ propriétés inconnues détectées
→ aucun secret loggué
→ aucun défaut faible

PostgreSQL
→ bonne base
→ bon client
→ bon environnement
→ bon rôle
→ privilèges minimaux
→ schéma compatible
→ aucune migration automatique

Modules
→ lock vérifié
→ modules embarqués
→ schémas modules
→ contrats publics
→ aucun module implicite

Jobs
→ handlers présents
→ versions compatibles
→ aucun claim avant readiness
→ job inconnu non traité

Security state
→ Identity
→ Data Protection
→ Audit
→ Privacy
→ restore state
→ epochs
→ cache state

Capabilities
→ available
→ degraded
→ unavailable
→ blocked
→ fonctionnement partiel uniquement
  sans réduction de sécurité

Health
→ liveness minimale
→ readiness complète
→ profils public, admin et worker
→ aucun détail sensible public

External providers
→ validation structurelle
→ aucun appel automatique
→ probes séparées
→ panne facultative non bloquante

Preflight
→ lecture seule
→ rapport JSON
→ exit codes
→ compatibilité rollout et rollback

Restore
→ restore-pending
→ nouveaux epochs
→ réconciliation
→ ouverture uniquement après validation

Qualité
→ tests configuration
→ tests secrets
→ tests filesystem
→ tests PostgreSQL
→ tests modules
→ tests jobs
→ tests restore
→ tests absence d’effets externes
```

> Un processus capable d’ouvrir un port n’est pas nécessairement prêt à servir une application. SiteFactory ne déclare un composant opérationnel qu’après avoir démontré qu’il utilise la bonne configuration, la bonne base, le bon rôle, le bon schéma et les bons contrats, sans avoir modifié silencieusement l’environnement pour y parvenir.
