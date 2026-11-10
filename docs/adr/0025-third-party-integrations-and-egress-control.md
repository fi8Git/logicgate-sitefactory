---
adrId: ADR-0025
title: Control third-party integrations through declared providers and restricted outbound access
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
  - ./0015-deterministic-generation-and-optional-ai.md
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
targetVersion: 0.1.0
---

# ADR-0025 — Intégrations tierces et contrôle des communications sortantes

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le public ne fournit pas encore de catalogue de providers, de contrôleur
d'egress opérationnel ou d'intégration métier distante.
Les interfaces et procédures ci-dessous sont des exigences cibles, pas des
appels réseau livrés par le [produit actuel](../../README.md).
Les URLs locales, credentials dans une URL et clés dans une query string
des anti-patterns sont des cas synthétiques à refuser ou expurger,
jamais des destinations à contacter ni des secrets à utiliser.
Aucun fallback cloud ou `HttpClient` métier arbitraire n'est autorisé ;
les protections SSRF, DNS, redirects, timeouts et TLS restent normatives.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory interdit les communications réseau sortantes arbitraires depuis les applications générées.

Toute intégration externe doit être :

```text
déclarée

identifiée

versionnée

autorisée

configurée

limitée

observable

désactivable

testée

documentée
```

La baseline repose sur :

```text
catalogue des fournisseurs

registre des opérations

clients réseau typés

origines exactes

validation DNS et IP

protection SSRF

credentials référencés

payloads minimisés

timeouts

retries bornés

circuit breakers

kill switches

audit des changements sensibles

manifest d’egress
```

Les principes suivants sont obligatoires :

```text
aucune URL externe arbitraire provenant d’un utilisateur

aucune URL de provider stockée dans un payload de job

aucun HttpClient créé librement dans un module métier

aucune validation TLS désactivée

aucun redirect HTTP automatique par défaut

aucun accès aux adresses loopback, privées, link-local
ou metadata depuis une intégration publique

aucun credential dans une URL

aucun corps de requête ou réponse complet dans les logs

aucun appel externe critique exécuté avant le commit métier

aucun provider activé sans kill switch

aucune donnée envoyée à un tiers sans catégorie autorisée
```

---

## 2. Résumé

L’architecture de référence est :

```text
Cas d’usage
→ Integration ID
→ Operation ID
→ vérification entitlement / feature / kill switch
→ vérification de la catégorie de données
→ résolution de la configuration approuvée
→ résolution du credential par Secret ID
→ construction d’une requête typée
→ validation de la destination
→ résolution DNS contrôlée
→ connexion vers une adresse autorisée
→ timeout et limites
→ validation de la réponse
→ résultat structuré
```

Pour les effets de bord fiables :

```text
transaction métier
→ outbox
→ job durable
→ client d’intégration typé
→ idempotency key
→ provider
→ retry ou dead-letter
```

Pour les webhooks entrants :

```text
requête reçue
→ limite de taille
→ rate limiting
→ vérification de signature
→ vérification du timestamp
→ détection de replay
→ validation du schéma
→ persistance durable
→ réponse rapide
→ traitement asynchrone
```

Le projet généré contient un inventaire machine-readable :

```text
egress-manifest.json
```

Ce manifest décrit les communications autorisées sans exposer de secret.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Toute communication externe possède un Integration ID.

Toute opération externe possède un Operation ID stable.

Une URL complète ne provient jamais d’un formulaire,
d’un contenu, d’un lead ou d’un payload de job.

Les credentials sont résolus séparément de la destination.

Les endpoints utilisent HTTPS par défaut.

La validation des certificats n’est jamais désactivée.

Les redirects automatiques sont désactivés.

Une redirection nécessaire est validée comme une nouvelle destination.

Une intégration publique ne peut pas joindre
une adresse privée ou locale.

Une résolution DNS contenant une adresse interdite est refusée.

La connexion utilise l’adresse validée,
pas une nouvelle résolution non contrôlée.

Les headers entrants du navigateur ne sont jamais
transmis automatiquement au fournisseur.

Les cookies administrateur ne sont jamais transmis.

Le header Authorization entrant n’est jamais recopié.

Un secret ne figure jamais dans une query string.

Une réponse externe est toujours considérée comme non fiable.

Un timeout est défini pour chaque opération.

La taille maximale de réponse est bornée.

Un retry est borné et limité aux erreurs transitoires.

Un fournisseur désactivé ne reçoit aucune nouvelle requête.

Une intégration retirée ne laisse pas de jobs
incompatibles silencieusement en attente.
```

---

## 4. Contexte

Les applications SiteFactory peuvent communiquer avec :

- un serveur SMTP ;
- un CRM ;
- un ERP ;
- un fournisseur de challenge humain ;
- un fournisseur AI ;
- un object storage ;
- un service de sauvegarde ;
- un endpoint webhook ;
- un service de cartographie ;
- un outil de mesure ;
- un collecteur OpenTelemetry ;
- un fournisseur de paiement futur ;
- un service de signature futur ;
- un fournisseur DNS ou ACME ;
- une API métier client.

Ces communications peuvent transmettre :

- données personnelles ;
- informations de leads ;
- documents ;
- contenu ;
- identifiants techniques ;
- métriques ;
- événements ;
- données d’authentification du provider.

Elles peuvent également exposer l’application à :

- Server-Side Request Forgery ;
- fuite de données ;
- redirection malveillante ;
- DNS rebinding ;
- fournisseur compromis ;
- credential exfiltration ;
- timeout ;
- rate limit ;
- réponse surdimensionnée ;
- schéma malveillant ;
- indisponibilité externe ;
- coûts incontrôlés ;
- effet dupliqué ;
- changement d’endpoint.

Une simple utilisation de :

```csharp
new HttpClient()
```

avec une URL provenant de la configuration ou d’un utilisateur ne fournit pas les garanties nécessaires.

---

## 5. Problème à résoudre

Comment permettre des intégrations externes tout en garantissant :

- la maîtrise des destinations ;
- la prévention SSRF ;
- la protection des credentials ;
- la minimisation des données ;
- la résilience ;
- l’idempotence ;
- la confidentialité ;
- la traçabilité ;
- la révocation rapide ;
- la transférabilité client ;
- une future restriction réseau plus forte ?

La solution doit également éviter :

- les URLs arbitraires ;
- les providers codés directement dans Domain ;
- les clients HTTP génériques utilisés partout ;
- les secrets dans les jobs ;
- les redirects vers une adresse interne ;
- les appels synchrones longs dans les endpoints ;
- les réponses fournisseur rendues directement au navigateur ;
- les retries infinis ;
- la création d’un open relay ;
- l’usage implicite des variables `HTTP_PROXY` ;
- le contournement des kill switches ;
- l’ajout d’une destination sans revue.

---

## 6. Portée

Cette décision couvre :

- intégrations HTTP et HTTPS ;
- SMTP sortant ;
- webhooks sortants ;
- webhooks entrants ;
- fournisseurs AI ;
- object storage externe ;
- télémétrie externe ;
- catalogues de providers ;
- clients typés ;
- DNS ;
- SSRF ;
- redirects ;
- TLS ;
- credentials ;
- payloads ;
- retries ;
- timeouts ;
- circuit breakers ;
- kill switches ;
- manifest d’egress ;
- Docker Compose ;
- observabilité ;
- audit ;
- tests ;
- handover ;
- clôture de service.

---

## 7. Hors portée

Cette décision ne définit pas :

- un proxy d’egress obligatoire pour le MVP ;
- un service mesh ;
- une architecture zero-trust réseau complète ;
- un WAF ;
- un firewall cloud managé ;
- tous les contrats propres à chaque fournisseur ;
- un broker de messages externe ;
- le choix définitif d’un fournisseur AI ;
- un fournisseur SMTP définitif ;
- un fournisseur object storage définitif ;
- une synchronisation bidirectionnelle universelle ;
- une plateforme centrale multi-client d’intégrations ;
- une garantie de disponibilité des tiers.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Prévention SSRF**
2. **Maîtrise des destinations sortantes**
3. **Protection des credentials**
4. **Minimisation des données transmises**
5. **Possibilité de désactivation immédiate**
6. **Résilience aux pannes externes**
7. **Idempotence des effets**
8. **Isolation des modules**
9. **Observabilité sans divulgation**
10. **Configuration versionnée**
11. **Compatibilité avec les jobs durables**
12. **Portabilité client**
13. **Préparation d’un contrôle réseau renforcé**
14. **Testabilité**
15. **Faible verrouillage fournisseur**
16. **Simplicité proportionnée au MVP**

---

## 9. Contraintes

La décision doit respecter :

```text
.NET 10

IHttpClientFactory

Clients typés

PostgreSQL

Jobs durables

Secrets référencés

HTTPS par défaut

Validation TLS obligatoire

Redirects désactivés

Destinations allowlistées

DNS contrôlé

Protection IPv4 et IPv6

Timeouts bornés

Tailles bornées

Observabilité OpenTelemetry

Audit

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le nombre de providers par projet reste limité ;
- les endpoints sont connus lors de la configuration ;
- les intégrations arbitraires configurées par les visiteurs ne sont pas nécessaires au MVP ;
- certains fournisseurs utilisent des IPs dynamiques ;
- une allowlist de hostnames reste donc nécessaire ;
- les résolutions DNS peuvent être validées à chaque nouvelle connexion ;
- les effets importants sont exécutés dans le worker ;
- PostgreSQL et les services Docker internes utilisent des contrats distincts des providers publics ;
- un proxy d’egress pourra être ajouté ultérieurement sans modifier les contrats métier.

---

# 11. Définitions

## 11.1 Third-party integration

Communication avec un service non possédé par le processus applicatif courant.

---

## 11.2 Egress

Communication réseau initiée depuis un composant SiteFactory vers une autre destination.

---

## 11.3 Integration ID

Identifiant stable d’une intégration.

Exemples :

```text
smtp.primary

crm.client

ai.content-provider

storage.object-primary
```

---

## 11.4 Operation ID

Identifiant stable d’une opération autorisée.

Exemples :

```text
crm.lead.create

crm.lead.delete

webhook.lead-created.deliver

ai.content-draft.generate
```

---

## 11.5 Provider profile

Contrat décrivant :

- destination ;
- protocole ;
- opérations ;
- credentials ;
- limites ;
- politiques.

---

## 11.6 Public integration

Intégration utilisant une destination externe publiquement routable.

---

## 11.7 Internal service integration

Communication avec un service interne explicitement déployé dans le même environnement.

---

## 11.8 Egress policy

Règles autorisant ou refusant une communication sortante.

---

## 11.9 Destination validation

Contrôles appliqués à :

- scheme ;
- host ;
- port ;
- DNS ;
- IP ;
- path ;
- redirects.

---

## 11.10 SSRF

Utilisation de l’application pour atteindre une destination réseau non prévue.

---

# 12. Option A — URLs et `HttpClient` libres

## 12.1 Description

Chaque module crée son propre client et lit une URL depuis :

- configuration ;
- base ;
- contenu ;
- requête.

---

## 12.2 Avantages

- très flexible ;
- rapide à implémenter ;
- peu de contrats ;
- facile pour les customizations.

---

## 12.3 Inconvénients

- SSRF ;
- timeouts incohérents ;
- credentials dispersés ;
- observabilité variable ;
- redirects ;
- fuites de headers ;
- difficultés de révocation ;
- tests incomplets.

---

## 12.4 Risques

- requête vers localhost ;
- accès aux metadata cloud ;
- exfiltration d’un secret ;
- endpoint modifié depuis l’administration ;
- appels infinis ;
- réponse non bornée.

---

## 12.5 Réversibilité

```text
faible à moyenne
```

Les appels dispersés sont difficiles à inventorier.

---

# 13. Option B — Proxy d’egress uniquement

## 13.1 Description

Toutes les communications passent par un proxy réseau central.

---

## 13.2 Avantages

- destination centralisée ;
- logs réseau ;
- politiques ;
- contrôle de protocoles ;
- réduction du SSRF.

---

## 13.3 Inconvénients

- infrastructure supplémentaire ;
- point de panne ;
- credentials ;
- configuration TLS ;
- difficulté locale ;
- ne valide pas la sémantique métier ;
- ne minimise pas les payloads.

---

## 13.4 Risques

- proxy trop permissif ;
- provider non déclaré mais accessible ;
- données excessives envoyées ;
- contournement par un autre protocole.

---

## 13.5 Réversibilité

```text
moyenne
```

---

# 14. Option C — Clients typés sans validation réseau renforcée

## 14.1 Description

Chaque provider utilise `IHttpClientFactory` et une URL configurée.

---

## 14.2 Avantages

- centralisation ;
- timeouts ;
- résilience ;
- testabilité ;
- séparation Infrastructure.

---

## 14.3 Inconvénients

- URL malveillante toujours possible ;
- DNS rebinding ;
- redirects ;
- IP privée ;
- proxy ambiant ;
- host mal validé.

---

## 14.4 Risques

- configuration remplacée par localhost ;
- DNS pointant vers une IP interne ;
- redirect vers une metadata endpoint ;
- URL contenant des credentials.

---

## 14.5 Réversibilité

```text
élevée
```

---

# 15. Option D — Catalogue, clients typés et egress contrôlé

## 15.1 Description

Les intégrations sont déclarées dans un catalogue.

Un service central contrôle les destinations et crée les clients typés.

---

## 15.2 Avantages

- prévention SSRF ;
- inventaire ;
- timeouts ;
- credentials isolés ;
- kill switches ;
- minimisation ;
- observabilité ;
- audit ;
- possibilité d’ajouter un proxy plus tard ;
- compatibilité jobs.

---

## 15.3 Inconvénients

- catalogue ;
- validation DNS ;
- abstractions supplémentaires ;
- tests réseau ;
- règles par provider ;
- gestion des exceptions internes.

---

## 15.4 Risques

- faux positif sur un provider légitime ;
- changement DNS ;
- configuration trop stricte ;
- mauvaise classification d’un service interne ;
- complexité d’exploitation.

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

| Critère | Poids | Libre | Proxy uniquement | Clients typés | Catalogue contrôlé |
|---|---:|---:|---:|---:|---:|
| Prévention SSRF | 5 | 1 | 4 | 2 | 5 |
| Contrats métier | 5 | 1 | 2 | 4 | 5 |
| Protection credentials | 5 | 2 | 3 | 4 | 5 |
| Observabilité | 4 | 2 | 4 | 4 | 5 |
| Kill switches | 4 | 1 | 3 | 4 | 5 |
| Minimisation | 5 | 1 | 2 | 4 | 5 |
| Infrastructure MVP | 4 | 5 | 1 | 5 | 4 |
| Évolution proxy future | 4 | 2 | 5 | 4 | 5 |
| Testabilité | 5 | 2 | 3 | 5 | 5 |
| Maintenabilité | 5 | 1 | 3 | 4 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’utiliser :

```text
un catalogue d’intégrations versionné

un registre d’opérations autorisées

des clients réseau typés

une policy d’egress centrale

une validation DNS et IP

des providers désactivables
```

Les modules métier ne reçoivent pas un `HttpClient` générique.

Ils utilisent des contrats applicatifs tels que :

```text
ICrmLeadPublisher

IEmailSender

IExternalStorage

IAiContentProvider

IOutboundWebhookPublisher
```

---

## 17.1 Domain

Domain ne dépend pas :

- de `HttpClient` ;
- de SMTP ;
- d’un SDK fournisseur ;
- d’une URL ;
- d’un certificat ;
- d’un proxy.

---

## 17.2 Infrastructure

Infrastructure contient :

- adapters ;
- typed clients ;
- DNS validation ;
- TLS ;
- credentials ;
- retry ;
- observabilité.

---

# 18. Integration Catalog

Le catalogue doit pouvoir déclarer :

```text
IntegrationId

IntegrationVersion

ProviderType

Protocol

Environment

BaseEndpoint

AllowedOperations

AllowedMethods

AllowedPaths

AllowedPorts

DataCategories

CredentialReferences

TimeoutPolicy

RetryPolicy

ResponseLimits

RedirectPolicy

DnsPolicy

TlsPolicy

CircuitBreakerPolicy

KillSwitchId

Owner

ReviewAt
```

---

## 18.1 Source

Le catalogue est versionné avec :

- la plateforme ;
- les modules ;
- les customizations autorisées.

---

## 18.2 Résolution

Le générateur produit une composition propre au projet.

---

## 18.3 Manifest

Le catalogue résolu alimente :

```text
egress-manifest.json
```

---

# 19. Egress Manifest

Le manifest contient :

```text
SchemaVersion

ClientSlug

Environment

ApplicationVersion

IntegrationIds

Components

Protocols

ExactOrigins

Ports

OperationIds

DataCategories

SecretIds

KillSwitchIds

Timeouts

RetryPolicies

ReviewDates
```

---

## 19.1 Secret IDs

Le manifest peut référencer :

```text
SecretId
```

mais jamais sa valeur.

---

## 19.2 Destination

Il ne contient pas de query string ou credential.

---

## 19.3 Handover

Le manifest fait partie du handover opérationnel.

---

# 20. Profils d’intégration

Profils initiaux possibles :

```text
integration.smtp-submission.v1

integration.https-api.v1

integration.outbound-webhook.v1

integration.inbound-webhook.v1

integration.ai-provider.v1

integration.object-storage.v1

integration.otlp-exporter.v1

integration.internal-service.v1
```

---

## 20.1 Aucun profil universel

Un profil :

```text
integration.any-http.v1
```

n’est pas autorisé.

---

# 21. Registre des opérations

Chaque opération autorisée possède :

```text
OperationId

IntegrationId

Method

PathTemplate

RequestSchema

ResponseSchema

DataCategories

IdempotencyPolicy

Timeout

MaximumRequestBytes

MaximumResponseBytes

ErrorMapping
```

---

## 21.1 Path Template

Le path est défini par le contrat.

Les données utilisateur ne peuvent remplir que des segments encodés et attendus.

---

## 21.2 URL complète

Un paramètre d’opération ne peut pas remplacer :

- scheme ;
- host ;
- port ;
- base path.

---

# 22. Clients typés

Les providers HTTP utilisent :

```text
IHttpClientFactory
```

et des clients typés.

Exemple conceptuel :

```csharp
public sealed class ClientCrmLeadPublisher : ICrmLeadPublisher
{
    private readonly HttpClient _httpClient;
    private readonly IIntegrationPolicy _policy;
}
```

---

## 22.1 Création directe

Sont interdits dans les modules :

```csharp
new HttpClient()

new SocketsHttpHandler()

WebRequest.Create(...)

HttpClient.GetAsync(userProvidedUrl)
```

---

## 22.2 Lifetime

Le cycle de vie est géré par `IHttpClientFactory`.

---

# 23. Fabrique de clients d’egress

Une abstraction centrale peut être utilisée :

```csharp
public interface IEgressClientFactory
{
    TClient GetRequiredClient<TClient>(
        IntegrationId integrationId);
}
```

---

## 23.1 Usage

La fabrique valide :

- provider actif ;
- environnement ;
- policy ;
- credential ;
- endpoint ;
- kill switch.

---

## 23.2 Client inconnu

Une intégration inconnue est refusée au démarrage ou lors de la résolution.

---

# 24. URLs autorisées

Une destination publique doit être une URL absolue approuvée.

---

## 24.1 Schemes

Baseline :

```text
https
```

---

## 24.2 HTTP

```text
http
```

est interdit en production pour les integrations publiques.

---

## 24.3 Schemes interdits

```text
file

ftp

gopher

data

javascript

ws non sécurisé

dict

ldap non prévu

unix
```

---

## 24.4 Credentials dans l’URL

Une URL contenant :

```text
user:password@
```

est refusée.

---

## 24.5 Fragment

Un fragment `#...` n’appartient pas à une destination serveur et est refusé dans la configuration.

---

# 25. Canonicalisation du host

Le host est normalisé avant comparaison.

---

## 25.1 Règles

- casse normalisée ;
- trailing dot traité explicitement ;
- IDN converti en forme ASCII canonique ;
- IPv6 normalisée ;
- ports normalisés ;
- userinfo refusé.

---

## 25.2 Confusables

Un nom Unicode visuellement ambigu n’est pas enregistré sans revue explicite.

---

## 25.3 Wildcards

Les wildcards sont refusés par défaut.

---

# 26. Ports

Les ports sont allowlistés par profil.

---

## 26.1 HTTPS

Le port standard est préféré.

---

## 26.2 Port non standard

Il nécessite :

- déclaration ;
- justification ;
- tests ;
- revue.

---

## 26.3 Ports interdits

Un provider public ne peut pas cibler arbitrairement :

- PostgreSQL ;
- Redis ;
- SSH ;
- Docker API ;
- services d’administration ;
- ports internes.

---

# 27. Protection SSRF

Une destination publique est refusée lorsqu’elle résout vers une adresse :

```text
loopback

unspecified

private

link-local

multicast

broadcast

reserved

documentation-only

carrier-grade NAT selon la policy

metadata service

IPv4-mapped interdite
```

---

## 27.1 IPv4

Les plages privées et locales sont bloquées.

---

## 27.2 IPv6

Les plages équivalentes IPv6 sont également bloquées.

---

## 27.3 Metadata endpoints

Les endpoints metadata de plateformes cloud ou hébergeurs sont explicitement interdits.

---

# 28. Résolution DNS contrôlée

La policy ne doit pas effectuer :

```text
valider le hostname
→ laisser HttpClient refaire une résolution différente
```

sans contrôle.

---

## 28.1 Flux

```text
normaliser le host

résoudre DNS

valider toutes les adresses

sélectionner une adresse autorisée

établir la connexion vers cette adresse

conserver le host d’origine pour TLS et Host
```

---

## 28.2 Toutes les adresses

Si une résolution contient à la fois :

- une adresse publique ;
- une adresse interdite ;

la destination est refusée.

---

## 28.3 Nouvelle connexion

La validation est répétée à chaque nouvelle connexion physique.

---

## 28.4 Cache DNS

Un cache éventuel possède une durée bornée.

---

# 29. DNS rebinding

Les protections comprennent :

- résolution et connexion couplées ;
- validation de chaque nouvelle connexion ;
- durée de pool bornée ;
- redirects désactivés ;
- validation de toutes les adresses ;
- absence d’URL utilisateur.

---

## 29.1 Pooled connections

Une connexion existante peut être réutilisée pendant une durée limitée définie par le profil.

---

## 29.2 Changement DNS légitime

Les providers utilisant des IPs dynamiques restent compatibles lors de la création d’une nouvelle connexion validée.

---

# 30. Adresses IP littérales

Une destination publique utilisant directement une IP est refusée par défaut.

---

## 30.1 Raisons

- certificat ;
- rotation ;
- masquer une destination interne ;
- formats alternatifs ;
- portabilité.

---

## 30.2 Exception

Une IP publique fixe nécessite un profil spécifique et une justification.

---

## 30.3 Formats ambigus

Les formes décimales, octales ou hexadécimales ambiguës sont refusées.

---

# 31. Services internes

Les services internes utilisent un profil distinct :

```text
integration.internal-service.v1
```

---

## 31.1 Destinations

Ils peuvent utiliser :

- nom de service Docker exact ;
- port interne exact ;
- réseau Docker interne.

---

## 31.2 Séparation

Un provider public ne peut pas basculer vers le profil interne par configuration runtime.

---

## 31.3 Utilisateur

Aucune donnée utilisateur ne peut fournir le hostname interne.

---

# 32. Redirects

Les redirects automatiques sont désactivés.

---

## 32.1 Provider nécessitant un redirect

Un profil spécifique peut :

1. recevoir le statut ;
2. lire la destination ;
3. valider la nouvelle URL ;
4. retirer les credentials sensibles ;
5. limiter le nombre de hops ;
6. exécuter la nouvelle requête.

---

## 32.2 Cross-origin

Un header Authorization n’est jamais transmis automatiquement vers une nouvelle origine.

---

## 32.3 Destination privée

Un redirect vers une destination privée est refusé.

---

# 33. Proxies HTTP

Les clients d’intégration n’utilisent pas implicitement les variables :

```text
HTTP_PROXY

HTTPS_PROXY

ALL_PROXY
```

---

## 33.1 Baseline

```text
UseProxy = false
```

ou configuration équivalente est utilisée par défaut.

---

## 33.2 Proxy approuvé

Un proxy d’egress futur est configuré explicitement par :

- Proxy ID ;
- endpoint ;
- credential ;
- TLS ;
- policy ;
- audit.

---

## 33.3 NO_PROXY

Une configuration ambiante ne doit pas modifier silencieusement la destination réelle.

---

# 34. TLS

Toutes les integrations publiques utilisent TLS.

---

## 34.1 Validation

La validation standard du certificat et du hostname reste active.

---

## 34.2 Interdiction

Sont interdits :

```csharp
DangerousAcceptAnyServerCertificateValidator
```

et toute callback retournant systématiquement `true`.

---

## 34.3 Certificat invalide

La requête échoue.

---

## 34.4 Autorité privée

Une autorité privée nécessite :

- trust store explicite ;
- certificat approuvé ;
- profil interne ou fournisseur dédié ;
- procédure de rotation.

---

# 35. Certificate pinning

Le pinning n’est pas obligatoire dans la baseline.

---

## 35.1 Raisons

- rotations ;
- CDN ;
- disponibilité ;
- exploitation.

---

## 35.2 Profil renforcé

Un client réglementé peut exiger :

- SPKI pinning ;
- plusieurs pins ;
- procédure de rotation ;
- fallback contrôlé.

---

# 36. Mutual TLS

mTLS peut être activé par provider.

---

## 36.1 Credential

Le certificat client est référencé par un Secret ID.

---

## 36.2 Clé privée

Elle n’est pas :

- placée dans le repository ;
- passée dans un job ;
- journalisée ;
- incluse dans un export portable.

---

## 36.3 Rotation

Le profil doit permettre un chevauchement contrôlé des certificats.

---

# 37. Protocoles externes

Protocoles externes permis initialement :

```text
HTTPS

SMTP submission avec TLS

OTLP sécurisé lorsque configuré
```

---

## 37.1 Protocoles interdits par défaut

```text
FTP

Telnet

HTTP non sécurisé

SMTP sans chiffrement

TCP arbitraire

UDP arbitraire
```

---

## 37.2 Nouveau protocole

Un nouveau protocole nécessite :

- profil ;
- threat model ;
- limites ;
- observabilité ;
- tests ;
- décision humaine.

---

# 38. SMTP

SMTP constitue une intégration déclarée.

---

## 38.1 Configuration

Le profil précise :

- hostname ;
- port ;
- mode TLS ;
- Secret ID ;
- expéditeur autorisé ;
- timeout ;
- quota ;
- kill switch.

---

## 38.2 Open relay

Le destinataire d’un email ne doit pas devenir arbitraire en dehors des workflows prévus.

---

## 38.3 Emails de formulaire

Les règles de l’ADR-0018 restent applicables.

---

## 38.4 Jobs

Les emails sont envoyés par le worker.

---

# 39. Credentials

Chaque integration déclare ses credentials par référence.

Exemples :

```text
SecretId = crm.primary.api-key

SecretId = smtp.primary.password

SecretId = webhooks.partner-a.signing-key
```

---

## 39.1 Valeurs

La valeur suit l’ADR-0012.

---

## 39.2 URL

Aucun credential ne figure dans :

- scheme ;
- host ;
- userinfo ;
- path ;
- query string ;
- fragment.

---

## 39.3 Headers

Les headers sensibles sont construits par le client typé.

---

# 40. Scope des credentials

Un credential possède les permissions minimales.

---

## 40.1 Séparation

Les credentials sont distincts par :

- client ;
- environnement ;
- provider ;
- finalité.

---

## 40.2 Staging

Staging ne doit pas utiliser le credential production.

---

## 40.3 Partage

Un credential LogicGate commun à plusieurs clients est évité.

---

# 41. Rotation des credentials

Une rotation doit permettre :

```text
nouveau secret provisionné

test contrôlé

bascule

surveillance

révocation de l’ancien

audit
```

---

## 41.1 Chevauchement

Une courte période de chevauchement peut être autorisée.

---

## 41.2 Payloads

Les jobs conservent le Secret ID, jamais la valeur ni nécessairement une version exacte si la rotation doit être transparente.

---

# 42. Données autorisées

Chaque opération déclare les catégories de données qu’elle peut transmettre.

---

## 42.1 Exemple

```text
crm.lead.create
→ lead-contact-data
→ lead-message-content
```

---

## 42.2 Contrôle

Une tentative d’envoyer une catégorie non autorisée est refusée.

---

## 42.3 Sérialisation

Le client construit un DTO allowlisté.

Il ne sérialise pas directement l’entité EF Core complète.

---

# 43. Minimisation

Les payloads ne doivent contenir que les propriétés nécessaires.

---

## 43.1 Interdictions

Ne pas envoyer automatiquement :

- notes internes ;
- audit ;
- permissions ;
- sessions ;
- IP ;
- logs ;
- tokens ;
- secrets ;
- champs non prévus.

---

## 43.2 Texte libre

Les textes libres sont transmis uniquement si l’opération le nécessite.

---

# 44. Fournisseurs de données externes

Le Privacy Data Catalog de l’ADR-0023 doit référencer chaque fournisseur recevant des données.

---

## 44.1 Déclaration

Le provider indique :

- catégories ;
- finalité ;
- rétention connue ;
- procédure de suppression ;
- région ou destination lorsque pertinente ;
- owner.

---

## 44.2 Provider non déclaré

Une opération transmettant une donnée personnelle à un fournisseur non déclaré est bloquée.

---

# 45. Requêtes HTTP

Chaque opération définit :

- méthode ;
- path ;
- headers ;
- Content-Type ;
- taille ;
- timeout ;
- schéma.

---

## 45.1 Méthodes

Seules les méthodes nécessaires sont autorisées.

---

## 45.2 Méthodes interdites

```text
TRACE

CONNECT
```

sont interdites.

---

## 45.3 DELETE externe

Une méthode destructive nécessite une opération dédiée, idempotente et auditée lorsque sensible.

---

# 46. Headers sortants

Les headers sont construits depuis une allowlist.

---

## 46.1 Interdictions

Ne jamais transférer automatiquement :

```text
Cookie

Set-Cookie

Authorization entrant

X-Forwarded-For entrant

X-Real-IP

Origin

Referer

Baggage non filtré
```

---

## 46.2 Traçage

`traceparent` peut être propagé lorsque la policy l’autorise.

---

## 46.3 Baggage

Seules des clés bornées et non sensibles sont autorisées.

---

# 47. User-Agent sortant

L’application utilise un User-Agent stable.

Exemple conceptuel :

```text
LogicGate-SiteFactory/{application-version}
```

---

## 47.1 Données interdites

Le User-Agent ne contient pas :

- email ;
- client secret ;
- chemin ;
- hostname interne ;
- identifiant de session.

---

# 48. Corps de requête

La taille du corps est bornée par opération.

---

## 48.1 Fichiers

Un fichier volumineux est transmis en streaming lorsque le provider le permet.

---

## 48.2 Multipart

`multipart/form-data` nécessite un contrat explicite.

---

## 48.3 Base64

Un fichier ne doit pas être encodé en Base64 dans un JSON sans nécessité démontrée et limite adaptée.

---

# 49. Réponses externes

Une réponse est considérée comme non fiable.

---

## 49.1 Contrôles

- statut ;
- Content-Type ;
- Content-Length ;
- taille réelle ;
- encoding ;
- schéma ;
- profondeur ;
- timeout ;
- nombre d’éléments.

---

## 49.2 Réponse trop grande

La lecture est interrompue.

---

## 49.3 HTML

Une réponse HTML ne doit pas être rendue directement dans l’administration.

---

## 49.4 Type arbitraire

La désérialisation polymorphique arbitraire est interdite.

---

# 50. Compression des réponses

La décompression automatique doit être accompagnée d’une limite sur la taille décompressée.

---

## 50.1 Bombe de compression

Une petite réponse compressée ne doit pas pouvoir saturer la mémoire.

---

## 50.2 Streaming

Les réponses volumineuses sont traitées en streaming.

---

# 51. Timeouts

Chaque opération possède au minimum :

```text
connect timeout

request timeout

overall operation timeout
```

---

## 51.1 Aucun timeout infini

Un timeout infini est interdit.

---

## 51.2 Cancellation

Le `CancellationToken` est propagé.

---

## 51.3 Jobs

Le timeout de l’opération reste compatible avec le lease du job.

---

# 52. Retries

Les retries utilisent :

```text
exponential backoff

jitter

maximum attempts

maximum delay
```

---

## 52.1 Erreurs transitoires

Exemples :

- timeout ;
- connexion interrompue ;
- HTTP 408 ;
- HTTP 425 ;
- HTTP 429 ;
- HTTP 5xx selon contrat.

---

## 52.2 Erreurs permanentes

Exemples :

- payload invalide ;
- HTTP 400 ;
- HTTP 401 après refresh impossible ;
- HTTP 403 ;
- endpoint inconnu ;
- certificat invalide ;
- destination bloquée.

---

## 52.3 `Retry-After`

Il est respecté dans les limites de la policy.

---

# 53. Idempotence

Les effets externes doivent posséder une stratégie.

---

## 53.1 Header

Lorsque le provider le supporte :

```text
Idempotency-Key
```

est envoyé.

---

## 53.2 Valeur

La clé est stable pour le même effet logique.

---

## 53.3 Fallback local

Un registre local des deliveries peut compléter la protection.

---

## 53.4 Limite

L’idempotence locale ne garantit pas l’absence de doublon chez un provider sans support.

---

# 54. Circuit breaker

Un circuit breaker peut protéger chaque provider ou opération.

---

## 54.1 Objectif

Réduire :

- saturation ;
- latence ;
- retries inutiles ;
- coûts ;
- effet domino.

---

## 54.2 État

États conceptuels :

```text
closed

open

half-open
```

---

## 54.3 Kill switch

Le circuit breaker ne remplace pas le kill switch opérateur.

---

# 55. Kill switches

Chaque intégration externe possède un kill switch.

Exemples :

```text
integration.crm.client.disabled

integration.smtp.primary.disabled

integration.ai.content-provider.disabled
```

---

## 55.1 Effet

Le kill switch empêche les nouveaux appels.

---

## 55.2 Jobs en attente

La policy précise s’ils restent :

- en attente ;
- retry-wait ;
- cancelled ;
- dead-lettered.

---

## 55.3 Audit

L’activation et la désactivation sont auditées.

---

# 56. Feature flags et entitlements

Un feature flag peut contrôler l’activation technique.

Un entitlement contrôle la capacité commerciale.

---

## 56.1 Limite

Aucun des deux :

- ne crée un credential ;
- n’ajoute une destination ;
- ne contourne la policy d’egress.

---

## 56.2 Provider actif

L’appel exige :

```text
module installé
+ entitlement
+ feature active
+ kill switch inactif
+ provider configuré
+ credential valide
```

---

# 57. Appels synchrones

Un appel externe peut rester synchrone uniquement lorsque :

- lecture nécessaire à la réponse ;
- timeout court ;
- effet non durable ;
- fallback défini ;
- charge bornée ;
- provider adapté.

---

## 57.1 Exemple possible

Validation d’un code fourni par un challenge humain.

---

## 57.2 Interdiction

Un appel de synchronisation CRM ou email ne doit pas bloquer une soumission publique.

---

# 58. Effets asynchrones

Les effets durables utilisent :

```text
outbox
+ job
```

---

## 58.1 Exemples

- email ;
- webhook sortant ;
- CRM ;
- suppression externe ;
- export vers un provider ;
- transformation distante.

---

## 58.2 Commit

L’appel externe a lieu après le commit métier.

---

# 59. Outbound webhooks

Un webhook sortant possède :

```text
WebhookSubscriptionId

IntegrationId

ExactDestination

EventTypes

PayloadSchemaVersion

SigningSecretId

RetryPolicy

Timeout

Status

KillSwitchId
```

---

## 59.1 MVP

Les destinations de webhook arbitraires configurables par un client final sont hors de la baseline MVP.

---

## 59.2 Ajout

Une destination est ajoutée par :

- configuration versionnée ;
- ou administration critique dédiée ;
- step-up ;
- validation SSRF ;
- audit.

---

# 60. Signature des webhooks sortants

La baseline utilise :

```text
HMAC-SHA256
```

sur une représentation documentée.

---

## 60.1 Données signées

Exemple conceptuel :

```text
version
timestamp
event id
body hash
```

---

## 60.2 Headers

Exemples conceptuels :

```text
X-SiteFactory-Webhook-Id

X-SiteFactory-Webhook-Timestamp

X-SiteFactory-Webhook-Signature
```

---

## 60.3 Secret

Le secret est référencé par Secret ID.

---

# 61. Payloads des webhooks

Les payloads sont :

- versionnés ;
- minimisés ;
- documentés ;
- stables ;
- dépourvus de secret.

---

## 61.1 Entité complète

Une entité EF Core complète ne doit pas être sérialisée directement.

---

## 61.2 PII

Les catégories transmises sont déclarées dans le catalogue.

---

# 62. Réponses des webhooks sortants

Un webhook considère généralement comme réussite :

```text
HTTP 2xx
```

---

## 62.1 Redirection

Une réponse 3xx n’est pas suivie automatiquement.

---

## 62.2 4xx

Une erreur 4xx est permanente par défaut, sauf contrat explicite.

---

## 62.3 429

Rate limit, donc potentiellement retryable.

---

## 62.4 5xx

Peut être retryable avec limites.

---

# 63. Delivery records

Chaque livraison peut conserver :

```text
WebhookDeliveryId

WebhookSubscriptionId

EventId

AttemptNumber

CreatedAtUtc

CompletedAtUtc

Result

StatusCode

ErrorCode
```

---

## 63.1 Données interdites

Ne pas conserver automatiquement :

- payload complet ;
- secret ;
- réponse complète ;
- headers sensibles.

---

# 64. Inbound webhooks

Un webhook entrant utilise un endpoint propre au provider ou au contrat.

---

## 64.1 Endpoint générique

Un endpoint permettant d’exécuter un handler arbitraire est interdit.

---

## 64.2 Méthode

Les mutations utilisent :

```text
POST
```

ou la méthode imposée par le contrat.

---

## 64.3 GET

Un GET ne doit pas produire un effet métier, sauf challenge de vérification explicitement prévu et sans mutation.

---

# 65. Vérification des webhooks entrants

Le pipeline est :

```text
taille

Content-Type

rate limiting

lecture bornée du corps brut

timestamp

signature

replay

schéma

persistance

réponse

traitement asynchrone
```

---

## 65.1 Signature avant parsing

La signature est vérifiée sur les octets bruts selon le contrat.

---

## 65.2 Comparaison

La comparaison utilise une fonction en temps constant.

---

# 66. Timestamp des webhooks entrants

La baseline utilise une tolérance initiale telle que :

```text
5 minutes
```

lorsque le provider fournit un timestamp signé.

---

## 66.1 Clock skew

Un léger décalage est accepté.

---

## 66.2 Ancien événement

Un événement trop ancien est refusé ou mis en quarantaine selon le contrat.

---

# 67. Replay protection

Le fournisseur doit fournir ou permettre de calculer un identifiant d’événement stable.

---

## 67.1 Registre

Une contrainte unique empêche le retraitement du même événement.

---

## 67.2 TTL

Les identifiants de replay possèdent une rétention adaptée.

---

## 67.3 Double livraison

Une double livraison valide retourne un résultat idempotent.

---

# 68. Rotation des secrets webhook

La validation peut accepter temporairement :

- secret courant ;
- secret précédent.

---

## 68.1 Fenêtre

La fenêtre de chevauchement est courte et définie.

---

## 68.2 Audit

La rotation est auditée.

---

## 68.3 Secret inconnu

Aucun secret fourni dans la requête n’est sélectionné dynamiquement sans identifiant contrôlé.

---

# 69. IP allowlist pour webhooks entrants

Une allowlist IP peut être utilisée comme signal complémentaire.

---

## 69.1 Limite

Elle ne remplace pas :

- signature ;
- timestamp ;
- replay ;
- rate limiting.

---

## 69.2 Proxies

Les forwarded headers suivent l’ADR-0018.

---

# 70. Réponse rapide aux webhooks entrants

Après validation :

```text
persister durablement
→ répondre rapidement
→ traiter par worker
```

---

## 70.1 Provider timeout

Le traitement métier long n’est pas effectué avant la réponse.

---

## 70.2 Persistance échouée

L’application ne retourne pas un faux succès.

---

# 71. Providers AI

Les providers AI suivent également cet ADR.

---

## 71.1 Destination

L’endpoint AI est déclaré.

---

## 71.2 Données

Les catégories et classifications autorisées suivent l’ADR-0015.

---

## 71.3 Outils

Le provider de génération de contenu ne reçoit pas un accès réseau secondaire arbitraire.

---

## 71.4 Search web

Une fonction de recherche externe constitue une nouvelle intégration distincte.

---

# 72. Object storage externe

Un object storage utilise :

- endpoint exact ;
- bucket ou container ;
- préfixe client ;
- credential restreint ;
- TLS ;
- taille ;
- retry ;
- checksum.

---

## 72.1 Endpoint configurable

Un endpoint S3-compatible ne peut pas être fourni librement par un utilisateur final.

---

## 72.2 SSRF

La destination suit les mêmes validations.

---

## 72.3 Presigned URLs

Une URL signée :

- possède une durée courte ;
- n’est pas journalisée ;
- n’est pas stockée dans un job ;
- est produite après autorisation.

---

# 73. OpenTelemetry externe

Un exporter OTLP constitue une integration.

---

## 73.1 Données

La télémétrie ne doit pas contenir :

- secret ;
- PII ;
- payload ;
- contenu de formulaire ;
- token.

---

## 73.2 Indisponibilité

Une panne du backend d’observabilité ne doit pas bloquer le traitement métier.

---

## 73.3 Buffer

Les buffers restent bornés.

---

# 74. Caddy et ACME

Les communications de Caddy vers un fournisseur ACME appartiennent à l’inventaire d’egress Infrastructure.

---

## 74.1 Séparation

Elles ne passent pas nécessairement par les clients applicatifs.

---

## 74.2 Manifest

Le manifest indique :

```text
component = caddy

purpose = certificate-issuance
```

---

## 74.3 DNS challenge

Un DNS provider nécessite un credential dédié et une revue.

---

# 75. Build-time egress

Les communications de build comprennent :

- package feeds ;
- registry ;
- source control ;
- scanners.

---

## 75.1 Séparation

Elles appartiennent principalement à la supply chain et à l’ADR-0008.

---

## 75.2 Runtime

Une dépendance build-time ne doit pas devenir automatiquement une destination runtime.

---

# 76. Docker Compose

Le profil initial sépare les réseaux.

Exemple conceptuel :

```text
edge network
→ Caddy
→ web

application internal network
→ web
→ worker
→ PostgreSQL

egress-capable components
→ web selon besoins stricts
→ worker pour les effets externes
```

---

## 76.1 PostgreSQL

PostgreSQL :

- n’expose aucun port public ;
- n’a pas besoin d’egress Internet ;
- reste sur un réseau interne.

---

## 76.2 Worker

Le worker porte la majorité des integrations externes.

---

## 76.3 Web

Le web ne reçoit que les capacités d’egress synchrones réellement nécessaires.

---

# 77. Réseau Docker interne

Le réseau PostgreSQL peut utiliser :

```yaml
internal: true
```

lorsque la topologie permet de conserver l’accès nécessaire aux composants.

---

## 77.1 Plusieurs réseaux

Un composant peut être attaché :

- au réseau interne ;
- et à un réseau autorisant l’egress ;

selon son rôle.

---

## 77.2 Host network

```text
network_mode: host
```

est interdit dans la baseline.

---

# 78. Egress proxy futur

Un proxy d’egress pourra renforcer :

- allowlists ;
- logs ;
- authentification ;
- contrôle des protocoles ;
- DNS ;
- isolation.

---

## 78.1 Compatibilité

Les Integration IDs et Operation IDs resteront inchangés.

---

## 78.2 Conditions de révision

Le proxy sera réévalué si :

- plusieurs clients partagent un cluster ;
- exigences réglementaires ;
- incidents SSRF ;
- nombreuses intégrations ;
- plusieurs instances ;
- besoin de logs réseau centralisés.

---

# 79. Restrictions réseau host

Lorsque les destinations possèdent des IPs stables, des règles firewall peuvent compléter les contrôles applicatifs.

---

## 79.1 Limite

Une allowlist IP statique n’est pas toujours compatible avec :

- CDN ;
- rotations ;
- providers globaux.

---

## 79.2 Baseline

Le contrôle applicatif est obligatoire.

Le contrôle host constitue une défense complémentaire.

---

# 80. Accès au Docker socket

Les conteneurs applicatifs ne montent jamais :

```text
/var/run/docker.sock
```

---

## 80.1 Raison

Un accès au socket permettrait de contourner les contrôles d’egress et d’isolation.

---

# 81. Fichiers système et sockets locales

Une integration ne doit pas pouvoir atteindre :

- sockets Unix ;
- Named Pipes arbitraires ;
- fichiers ;
- Docker API ;
- services d’administration locaux.

---

# 82. Validation au démarrage

En production, l’application valide :

```text
catalogue

providers

endpoints

schemes

ports

Secret IDs

kill switches

policies

operations

data categories
```

---

## 82.1 Endpoint interdit

Une configuration interdite bloque le démarrage du composant concerné ou désactive explicitement l’intégration selon le profil.

---

## 82.2 Pas de fallback

Le système ne remplace pas automatiquement un endpoint invalide par une destination par défaut distante.

---

# 83. Tests de connexion opérateur

Une commande conceptuelle peut tester une integration.

```text
sitefactory integrations test plan

sitefactory integrations test apply
```

---

## 83.1 Plan

Affiche :

- Integration ID ;
- environnement ;
- origine ;
- opération de test ;
- catégories de données ;
- Secret ID ;
- timeout.

---

## 83.2 Payload

Le test utilise des données fictives ou un endpoint de santé prévu.

---

## 83.3 Production

Un test produisant un effet réel exige une confirmation et un audit.

---

# 84. Commandes opérateur

Commandes conceptuelles :

```text
sitefactory integrations list

sitefactory integrations validate

sitefactory integrations status

sitefactory integrations test

sitefactory integrations disable

sitefactory integrations enable

sitefactory integrations rotate-secret plan

sitefactory integrations rotate-secret apply

sitefactory egress manifest
```

---

## 84.1 Secrets

Aucune commande n’accepte une valeur de secret en argument.

---

## 84.2 URLs

Une commande runtime ne permet pas de remplacer arbitrairement l’endpoint par `--url`.

---

# 85. Observabilité

Événements recommandés :

```text
IntegrationCallStarted

IntegrationCallCompleted

IntegrationCallFailed

IntegrationDestinationRejected

IntegrationDnsResolutionRejected

IntegrationCircuitOpened

IntegrationCircuitClosed

IntegrationKillSwitchActive

OutboundWebhookQueued

OutboundWebhookDelivered

InboundWebhookAccepted

InboundWebhookRejected

IntegrationCredentialResolutionFailed
```

---

## 85.1 Propriétés autorisées

```text
IntegrationId

OperationId

ProviderType

Protocol

Result

StatusCodeClass

ErrorCategory

RetryAttempt

CircuitState

Environment

Duration
```

---

## 85.2 Données interdites

```text
URL complète avec query

Credential

Authorization header

Request body

Response body

Email

Lead ID

Webhook signature

Signed URL

IP résolue comme label métrique
```

---

# 86. Logs

Les logs peuvent contenir :

- Integration ID ;
- Operation ID ;
- origine canonique ou Origin ID ;
- résultat ;
- Error Code ;
- durée ;
- Correlation ID.

---

## 86.1 Interdictions

Ne pas journaliser :

- query string ;
- payload ;
- credential ;
- réponse complète ;
- certificat client ;
- token ;
- signature webhook.

---

## 86.2 Erreurs fournisseur

Les messages externes sont neutralisés et bornés avant journalisation.

---

# 87. Métriques

Exemples :

```text
integration calls

integration failures

integration latency

integration retries

integration circuit state

webhook deliveries

webhook replays rejected

egress destinations rejected

DNS destinations rejected
```

---

## 87.1 Labels autorisés

```text
integration_id

operation_id

result

error_category

status_code_class

environment
```

---

## 87.2 Labels interdits

```text
full_url

resolved_ip

credential_id sensible

event_id

lead_id

user_id

webhook_id individuel

trace_id
```

---

# 88. Alertes

Alertes possibles :

```text
Provider indisponible

Circuit breaker ouvert

Credential invalide

Hausse de HTTP 401 ou 403

Hausse de HTTP 429

Destination SSRF refusée

Résolution DNS vers une adresse privée

Webhooks en dead-letter

Signature webhook invalide en forte hausse

Kill switch actif trop longtemps

Payload ou réponse dépassant les limites

Integration inconnue tentée
```

---

## 88.1 Runbook

Chaque alerte importante référence une procédure.

---

# 89. Audit

Actions recommandées :

```text
integration.provider.added

integration.provider.updated

integration.provider.disabled

integration.provider.enabled

integration.endpoint.changed

integration.credential.rotated

integration.webhook-subscription.created

integration.webhook-subscription.revoked

integration.test.executed

integration.egress-exception.approved
```

---

## 89.1 Raison

Les changements d’endpoint, credential, webhook et kill switch exigent une raison.

---

## 89.2 Metadata

L’audit conserve :

- Integration ID ;
- Operation ID éventuel ;
- Origin ID ;
- Secret ID ;
- résultat ;
- acteur.

Il ne conserve aucune valeur secrète.

---

# 90. Rétention des données d’intégration

Les delivery records, erreurs et réponses temporaires possèdent une rétention bornée.

---

## 90.1 Webhook deliveries

Conserver uniquement les informations nécessaires à :

- retry ;
- diagnostic ;
- audit ;
- réconciliation.

---

## 90.2 Payloads

Les payloads complets ne sont pas conservés indéfiniment.

---

## 90.3 Provider response

Une réponse fournisseur n’est persistée que si le métier l’exige.

---

# 91. Suppression chez un fournisseur externe

Les procédures de l’ADR-0023 utilisent les clients d’intégration déclarés.

---

## 91.1 Operation ID

Exemple :

```text
crm.lead.delete
```

---

## 91.2 Idempotence

Une suppression externe peut être rejouée.

---

## 91.3 Provider sans suppression

La limite est documentée et peut bloquer la complétion.

---

# 92. Import et handover

L’export de l’ADR-0024 inclut :

- Integration IDs ;
- manifests ;
- Secret IDs ;
- providers ;
- données de configuration non sensibles ;
- liste des credentials à recréer.

---

## 92.1 Secrets

Aucune valeur secrète n’est transférée dans le paquet portable.

---

## 92.2 Réactivation

Les integrations restent désactivées après import tant que :

- credentials ;
- endpoints ;
- policies ;
- tests ;

ne sont pas validés.

---

# 93. Clôture de service

La clôture doit :

```text
inventorier les providers

drainer ou annuler les jobs

révoquer les webhooks

révoquer les credentials LogicGate

transférer les comptes fournisseur

désactiver les integrations

exporter le manifest

documenter les données externes restantes

auditer
```

---

# 94. Provider appartenant à LogicGate

Un compte fournisseur LogicGate partagé ne doit pas rester une dépendance implicite après le handover.

---

## 94.1 Options

- transfert ;
- recréation client ;
- migration ;
- désactivation.

---

## 94.2 Données historiques

La récupération des données chez le fournisseur suit le contrat et la policy.

---

# 95. Gestion des erreurs

Les erreurs sont mappées vers des catégories stables :

```text
configuration

destination-rejected

dns-rejected

tls

authentication

authorization

rate-limited

timeout

transient-network

provider-error

invalid-request

invalid-response

payload-too-large

response-too-large

cancelled
```

---

## 95.1 Message public

Une erreur externe ne doit pas exposer les détails du fournisseur à un visiteur.

---

## 95.2 Administration

L’administration affiche un diagnostic contrôlé et un Correlation ID.

---

# 96. Fail-open et fail-closed

Chaque opération définit son comportement.

---

## 96.1 Sécurité

Les contrôles de destination, TLS et credential utilisent :

```text
fail-closed
```

---

## 96.2 Observabilité

Un exporter de télémétrie indisponible utilise :

```text
fail-open pour le métier
```

avec buffers bornés.

---

## 96.3 Challenge humain obligatoire

Peut utiliser :

```text
fail-closed
```

selon l’ADR-0018.

---

## 96.4 CRM

Une synchronisation peut rester en job sans annuler le lead déjà persisté.

---

# 97. Dégradation contrôlée

Une intégration indisponible peut conduire à :

- mise en file ;
- fonctionnalité temporairement désactivée ;
- réponse partielle ;
- affichage d’un message ;
- circuit ouvert ;
- kill switch.

---

## 97.1 Interdiction

Le système ne doit pas basculer vers un provider inconnu ou gratuit sans décision.

---

# 98. Configuration dynamique

Les endpoints et providers ne sont pas modifiables par un simple champ administrable dans le MVP.

---

## 98.1 Changement

Un changement utilise :

- repository ou configuration opérateur contrôlée ;
- validation ;
- step-up ;
- audit ;
- release ou procédure explicite.

---

## 98.2 Secrets

La rotation des credentials peut être indépendante d’une release.

---

# 99. Custom webhooks futurs

Un futur module autorisant des webhooks clients nécessitera :

- capability spécifique ;
- permission ;
- step-up ;
- validation SSRF ;
- confirmation du domaine ;
- secret de signature ;
- quota ;
- revue ;
- kill switch ;
- audit.

---

## 99.1 Baseline

Cette fonctionnalité est hors du MVP standard.

---

# 100. Confirmation de destination

Pour une destination client future, une preuve de contrôle peut utiliser :

- challenge HTTP ;
- token DNS ;
- réponse signée ;
- validation opérateur.

---

## 100.1 Limite

La preuve de contrôle du domaine ne remplace pas la validation SSRF.

---

# 101. Sécurité des SDK fournisseurs

Un SDK fournisseur est évalué pour :

- endpoints ;
- retries ;
- redirects ;
- télémétrie ;
- proxy ;
- logs ;
- credentials ;
- dépendances ;
- licence.

---

## 101.1 SDK opaque

Un SDK empêchant le contrôle des destinations et logs peut être encapsulé ou refusé.

---

## 101.2 HttpClient interne

Le SDK doit utiliser un client configuré lorsque son API le permet.

---

# 102. Télémétrie implicite des SDK

Une bibliothèque ne doit pas envoyer des données de diagnostic à son fournisseur sans déclaration.

---

## 102.1 Contrôle

La télémétrie optionnelle est désactivée par défaut.

---

## 102.2 Nouvelle destination

Elle constitue une intégration supplémentaire.

---

# 103. Résolution d’URL dans les contenus

Une URL de contenu peut être affichée au navigateur.

Elle ne doit pas être utilisée comme destination serveur.

---

## 103.1 Exemple

Une image externe saisie par un administrateur ne doit pas être téléchargée automatiquement par le backend sans workflow dédié.

---

## 103.2 Import d’URL

Une future fonction « importer depuis une URL » nécessite un ADR ou profil SSRF renforcé distinct.

---

# 104. Callbacks et return URLs

Une URL de callback appartenant au provider est déclarée.

---

## 104.1 URL utilisateur

Une return URL fournie par un utilisateur ne devient pas un endpoint d’intégration.

---

## 104.2 Redirect navigateur

Les redirects navigateur suivent leurs propres allowlists.

---

# 105. Tests unitaires

Tester notamment :

- Integration ID ;
- Operation ID ;
- profile validation ;
- URL parsing ;
- host canonicalization ;
- port ;
- scheme ;
- path ;
- data categories ;
- kill switch ;
- error mapping.

---

# 106. Tests SSRF IPv4

Couvrir :

```text
127.0.0.1

0.0.0.0

10.0.0.0/8

172.16.0.0/12

192.168.0.0/16

169.254.0.0/16

100.64.0.0/10

multicast

broadcast

metadata endpoints

formats numériques alternatifs
```

---

# 107. Tests SSRF IPv6

Couvrir :

```text
::1

::

fc00::/7

fe80::/10

ff00::/8

IPv4-mapped IPv6

zone identifiers

formes compressées

formes étendues
```

---

# 108. Tests DNS

Simuler :

- host public ;
- host privé ;
- plusieurs A records ;
- plusieurs AAAA records ;
- mélange public et privé ;
- changement après cache ;
- DNS rebinding ;
- NXDOMAIN ;
- timeout DNS ;
- trailing dot ;
- IDN.

---

# 109. Tests de connexion couplée

Vérifier que l’application se connecte à l’adresse validée.

---

## 109.1 TOCTOU

Le test doit empêcher une nouvelle résolution implicite différente entre validation et connexion.

---

# 110. Tests de redirects

Couvrir :

- aucun redirect ;
- redirect même origine ;
- redirect autre origine ;
- redirect vers localhost ;
- redirect vers metadata ;
- boucle ;
- trop de hops ;
- fuite du header Authorization.

---

# 111. Tests TLS

Vérifier :

- certificat valide ;
- certificat expiré ;
- hostname incorrect ;
- certificat auto-signé non approuvé ;
- trust store explicite ;
- validation désactivée interdite ;
- mTLS lorsqu’activé.

---

# 112. Tests des proxies

Vérifier :

- `HTTP_PROXY` défini ;
- `HTTPS_PROXY` défini ;
- proxy non configuré ;
- proxy approuvé ;
- proxy credential absent ;
- `NO_PROXY` inattendu.

Le client ne doit pas utiliser un proxy ambiant non approuvé.

---

# 113. Tests des headers

Vérifier qu’une requête externe ne reçoit pas :

- cookie administrateur ;
- Authorization entrant ;
- forwarded headers ;
- user headers arbitraires ;
- baggage sensible.

---

# 114. Tests de payload

Couvrir :

- schéma valide ;
- champ inconnu ;
- taille excessive ;
- PII non autorisée ;
- fichier Base64 excessif ;
- Content-Type inattendu ;
- Unicode ;
- texte libre.

---

# 115. Tests de réponse

Couvrir :

- JSON valide ;
- JSON invalide ;
- Content-Type incorrect ;
- réponse vide ;
- réponse surdimensionnée ;
- compression excessive ;
- profondeur excessive ;
- HTML ;
- charset invalide ;
- timeout.

---

# 116. Tests de retry

Tester :

```text
timeout

connexion interrompue

HTTP 408

HTTP 429 avec Retry-After

HTTP 500

HTTP 400

HTTP 401

certificat invalide

destination rejetée

maximum attempts
```

---

# 117. Tests d’idempotence

Exécuter deux fois :

- CRM create ;
- webhook ;
- suppression externe ;
- upload objet.

Vérifier :

- clé stable ;
- registre local éventuel ;
- absence de duplication évitable.

---

# 118. Tests de circuit breaker

Couvrir :

- closed ;
- seuil atteint ;
- open ;
- requête refusée ;
- half-open ;
- succès ;
- nouvel échec ;
- kill switch simultané.

---

# 119. Tests des webhooks sortants

Vérifier :

- signature ;
- timestamp ;
- Event ID ;
- payload versionné ;
- 2xx ;
- 3xx ;
- 4xx ;
- 429 ;
- 5xx ;
- retry ;
- dead-letter ;
- secret rotation.

---

# 120. Tests des webhooks entrants

Couvrir :

```text
signature valide

signature invalide

timestamp ancien

timestamp futur excessif

replay

body modifié

Content-Type interdit

body trop grand

rate limiting

secret courant

secret précédent

persistance échouée
```

---

# 121. Tests SMTP

Vérifier :

- TLS ;
- hostname ;
- port ;
- credential ;
- expéditeur ;
- destinataire non autorisé ;
- timeout ;
- retry ;
- doublon ;
- kill switch.

---

# 122. Tests AI

Vérifier :

- endpoint approuvé ;
- provider désactivé ;
- classification interdite ;
- contexte trop volumineux ;
- credential absent ;
- réseau indisponible ;
- aucun fallback arbitraire.

---

# 123. Tests object storage

Vérifier :

- endpoint ;
- bucket ;
- préfixe ;
- credential limité ;
- checksum ;
- taille ;
- signed URL ;
- expiration ;
- mauvais environnement.

---

# 124. Tests Docker

Vérifier :

- PostgreSQL sans port public ;
- réseau interne ;
- worker et web séparés ;
- aucun host network ;
- aucun Docker socket ;
- manifest cohérent avec Compose ;
- egress non nécessaire absent du composant.

---

# 125. Tests de non-divulgation

Injecter des marqueurs représentant :

- API key ;
- Authorization header ;
- cookie ;
- token ;
- email ;
- payload ;
- réponse fournisseur.

Vérifier leur absence dans :

- logs ;
- traces ;
- métriques ;
- audit ;
- rapports ;
- manifest ;
- dead letters exposées.

---

# 126. Tests multiplateformes

Les tests de policy, URL, DNS et clients fonctionnent sur :

```text
Windows x64

Linux x64
```

Les tests Docker, firewall, certificats et réseau de production sont validés sur Linux.

---

# 127. Quality gates

Une contribution est bloquée lorsque :

```text
Un module crée librement un HttpClient.

Une URL utilisateur est utilisée côté serveur.

Une destination n’a pas d’Integration ID.

Une opération n’a pas d’Operation ID.

Une destination utilise HTTP en production.

La validation TLS est désactivée.

Les redirects automatiques sont actifs.

Une adresse privée est autorisée dans un profil public.

Le DNS n’est pas revalidé à la connexion.

Un credential apparaît dans l’URL.

Un header entrant est transmis automatiquement.

Un timeout est absent.

La réponse n’est pas bornée.

Un retry est infini.

Un provider n’a pas de kill switch.

Une donnée non déclarée est transmise.

Un webhook n’est pas signé.

Un test SSRF requis échoue.
```

---

# 128. Quality gates de production

La production est bloquée lorsque :

```text
Le manifest d’egress est absent.

Une intégration inconnue est active.

Un endpoint public résout vers une adresse interdite.

Un provider requis n’a pas de credential.

Un credential staging est utilisé.

Une validation TLS permissive est détectée.

Un proxy ambiant non approuvé est utilisé.

Le kill switch n’est pas disponible.

La policy de retry est absente.

Une intégration personnelle n’apparaît pas
dans le Privacy Data Catalog.

Le worker possède des jobs incompatibles.

Le runbook provider est absent.
```

---

# 129. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-INTEGRATION-001` | Integration ID inconnu |
| `SF-INTEGRATION-002` | Version d’intégration incompatible |
| `SF-INTEGRATION-003` | Operation ID inconnu |
| `SF-INTEGRATION-004` | Opération non autorisée |
| `SF-INTEGRATION-005` | Provider désactivé |
| `SF-INTEGRATION-006` | Kill switch actif |
| `SF-INTEGRATION-007` | Feature désactivée |
| `SF-INTEGRATION-008` | Entitlement absent |
| `SF-INTEGRATION-009` | Environnement incompatible |
| `SF-INTEGRATION-010` | Configuration provider invalide |
| `SF-EGRESS-001` | Scheme interdit |
| `SF-EGRESS-002` | Host invalide |
| `SF-EGRESS-003` | Port interdit |
| `SF-EGRESS-004` | Userinfo interdit dans l’URL |
| `SF-EGRESS-005` | Fragment interdit |
| `SF-EGRESS-006` | Wildcard de destination interdit |
| `SF-EGRESS-007` | Destination non allowlistée |
| `SF-EGRESS-008` | Adresse IP littérale interdite |
| `SF-EGRESS-009` | Destination loopback interdite |
| `SF-EGRESS-010` | Destination privée interdite |
| `SF-EGRESS-011` | Destination link-local interdite |
| `SF-EGRESS-012` | Destination metadata interdite |
| `SF-EGRESS-013` | Destination multicast interdite |
| `SF-EGRESS-014` | Résolution DNS invalide |
| `SF-EGRESS-015` | Résolution DNS mixte interdite |
| `SF-EGRESS-016` | DNS rebinding suspecté |
| `SF-EGRESS-017` | Redirect interdit |
| `SF-EGRESS-018` | Redirect vers une origine interdite |
| `SF-EGRESS-019` | Proxy non approuvé |
| `SF-EGRESS-020` | Profil interne requis |
| `SF-TLS-001` | TLS requis |
| `SF-TLS-002` | Certificat invalide |
| `SF-TLS-003` | Hostname certificat invalide |
| `SF-TLS-004` | Validation TLS désactivée |
| `SF-TLS-005` | Autorité privée non approuvée |
| `SF-TLS-006` | Certificat client absent |
| `SF-SECRET-INTEGRATION-001` | Secret ID absent |
| `SF-SECRET-INTEGRATION-002` | Secret introuvable |
| `SF-SECRET-INTEGRATION-003` | Secret du mauvais environnement |
| `SF-SECRET-INTEGRATION-004` | Credential présent dans l’URL |
| `SF-SECRET-INTEGRATION-005` | Rotation de credential requise |
| `SF-REQUEST-001` | Méthode interdite |
| `SF-REQUEST-002` | Path non autorisé |
| `SF-REQUEST-003` | Header interdit |
| `SF-REQUEST-004` | Content-Type interdit |
| `SF-REQUEST-005` | Payload invalide |
| `SF-REQUEST-006` | Payload trop volumineux |
| `SF-REQUEST-007` | Catégorie de données non autorisée |
| `SF-REQUEST-008` | Idempotency Key absente |
| `SF-RESPONSE-001` | Content-Type de réponse invalide |
| `SF-RESPONSE-002` | Réponse trop volumineuse |
| `SF-RESPONSE-003` | Réponse vide |
| `SF-RESPONSE-004` | Schéma de réponse invalide |
| `SF-RESPONSE-005` | Décompression excessive |
| `SF-RESPONSE-006` | Profondeur excessive |
| `SF-PROVIDER-001` | Provider indisponible |
| `SF-PROVIDER-002` | Provider timeout |
| `SF-PROVIDER-003` | Provider rate-limited |
| `SF-PROVIDER-004` | Authentication provider échouée |
| `SF-PROVIDER-005` | Authorization provider échouée |
| `SF-PROVIDER-006` | Erreur permanente du provider |
| `SF-PROVIDER-007` | Circuit breaker ouvert |
| `SF-WEBHOOK-001` | Webhook inconnu |
| `SF-WEBHOOK-002` | Signature absente |
| `SF-WEBHOOK-003` | Signature invalide |
| `SF-WEBHOOK-004` | Timestamp absent |
| `SF-WEBHOOK-005` | Timestamp hors fenêtre |
| `SF-WEBHOOK-006` | Replay détecté |
| `SF-WEBHOOK-007` | Payload webhook invalide |
| `SF-WEBHOOK-008` | Webhook non persisté |
| `SF-WEBHOOK-009` | Delivery en dead-letter |
| `SF-WEBHOOK-010` | Destination webhook interdite |
| `SF-SMTP-001` | Serveur SMTP inconnu |
| `SF-SMTP-002` | TLS SMTP requis |
| `SF-SMTP-003` | Expéditeur interdit |
| `SF-SMTP-004` | Destinataire interdit par la policy |
| `SF-SMTP-005` | Envoi SMTP échoué |

---

# 130. CI/CD

La CI doit vérifier :

- catalogue ;
- manifest ;
- clients typés ;
- absence de `new HttpClient` non autorisé ;
- URLs ;
- schemes ;
- DNS ;
- SSRF ;
- redirects ;
- TLS ;
- credentials ;
- payloads ;
- timeouts ;
- retries ;
- webhooks ;
- Docker Compose ;
- non-divulgation.

---

## 130.1 Analyse statique

La CI peut rechercher :

```text
new HttpClient

DangerousAcceptAnyServerCertificateValidator

ServerCertificateCustomValidationCallback

AllowAutoRedirect = true

http://

WebRequest.Create
```

---

## 130.2 Exceptions

Une occurrence légitime doit être centralisée, documentée et testée.

---

# 131. Sécurité

## 131.1 Actifs

- données personnelles ;
- credentials ;
- réseau interne ;
- metadata host ;
- services Docker ;
- providers ;
- webhooks ;
- SMTP ;
- object storage ;
- télémétrie.

---

## 131.2 Menaces

- SSRF ;
- DNS rebinding ;
- credential exfiltration ;
- redirection malveillante ;
- TLS bypass ;
- open relay ;
- réponse malveillante ;
- fuite de données ;
- provider compromis ;
- retry amplification ;
- destination runtime modifiée ;
- webhook replay.

---

## 131.3 Contrôles

- catalogue ;
- origines exactes ;
- DNS contrôlé ;
- IP validation ;
- TLS ;
- typed clients ;
- payload allowlist ;
- timeouts ;
- retries bornés ;
- signatures ;
- replay protection ;
- kill switches ;
- audit ;
- manifest ;
- tests.

---

# 132. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| URL `http://localhost` fournie | Aucune URL utilisateur |
| DNS public pointant vers une IP privée | Validation de toutes les IPs |
| DNS changé après validation | Résolution et connexion couplées |
| Redirect vers metadata | Redirects désactivés |
| Certificat frauduleux | Validation TLS |
| API key dans l’URL | Secrets séparés |
| Cookie admin envoyé au provider | Headers sortants allowlistés |
| Payload excessif | DTO et taille bornée |
| Réponse gigantesque | Streaming et limite |
| Provider en panne | Timeout, retry et circuit breaker |
| Webhook rejoué | Timestamp et Event ID |
| CRM recréant une donnée supprimée | Deletion Marker et Operation ID |
| Proxy ambiant malveillant | Proxy désactivé par défaut |

---

# 133. Règles pour les modules

Chaque module utilisant une intégration doit déclarer :

```text
IntegrationId

OperationIds

ProviderContract

DataCategories

Timeouts

RetryPolicies

IdempotencyPolicies

KillSwitchIds

PrivacyProcessorIds

VerificationTests
```

---

## 133.1 Interdictions

Un module ne doit pas :

- utiliser un client générique ;
- construire une URL complète ;
- recevoir un credential ;
- sérialiser une entité entière ;
- suivre les redirects ;
- désactiver TLS ;
- journaliser les payloads.

---

# 134. Règles pour les customizations

Une customization client peut ajouter un provider via les contrats publics.

---

## 134.1 Obligations

- Integration ID namespace client ;
- profil ;
- manifest ;
- Secret IDs ;
- tests SSRF ;
- kill switch ;
- documentation ;
- Privacy Descriptor.

---

## 134.2 Interdictions

Elle ne doit pas :

- lire une URL depuis un contenu administrable ;
- utiliser un endpoint libre ;
- contourner la policy ;
- activer un proxy ambiant ;
- autoriser une IP privée dans un profil public ;
- transmettre une donnée non déclarée.

---

# 135. Règles pour LogicGate

LogicGate doit :

- inventorier les providers ;
- valider les endpoints ;
- limiter les credentials ;
- maintenir les kill switches ;
- surveiller les erreurs ;
- tester les rotations ;
- documenter les processors ;
- préparer le handover ;
- révoquer les accès à la clôture.

---

## 135.1 LogicGate ne doit pas

- ajouter une URL directement en production ;
- désactiver TLS pour résoudre un incident ;
- mettre un secret dans une query string ;
- utiliser un compte fournisseur partagé sans justification ;
- suivre un redirect inconnu ;
- promettre une disponibilité dépendant d’un tiers ;
- conserver des payloads pour faciliter le diagnostic.

---

# 136. Règles pour Codex

Codex peut :

- définir les contrats ;
- créer les clients typés ;
- ajouter la validation DNS ;
- ajouter les tests ;
- implémenter les webhooks ;
- ajouter le manifest ;
- produire les runbooks.

---

## 136.1 Codex ne doit pas

```text
Appeler une URL réelle de production

Inventer un credential

Désactiver TLS

Ajouter unsafe certificate validation

Ajouter une URL utilisateur

Activer un provider réel

Modifier un endpoint de production

Envoyer un webhook réel

Utiliser un proxy ambiant

Contourner un kill switch

Appliquer une migration de production
```

---

## 136.2 Décision humaine

Une décision humaine est requise pour :

- nouveau provider ;
- nouvelle destination ;
- nouveau protocole ;
- nouvelle catégorie de données transmise ;
- nouvel endpoint privé ;
- mTLS ;
- certificate pinning ;
- webhook client configurable ;
- proxy d’egress ;
- désactivation exceptionnelle d’une policy.

---

# 137. Anti-patterns interdits

## 137.1 URL utilisateur

```csharp
await httpClient.GetAsync(request.Url);
```

---

## 137.2 TLS bypass

```csharp
handler.ServerCertificateCustomValidationCallback =
    (_, _, _, _) => true;
```

---

## 137.3 Redirect automatique

Le client suit une réponse vers `127.0.0.1`.

---

## 137.4 Credential query string

```text
https://provider.test/api?apiKey=secret
```

---

## 137.5 Forwarding de headers

Le cookie administrateur est recopié vers le CRM.

---

## 137.6 Payload d’entité

```csharp
JsonSerializer.Serialize(leadEntity);
```

envoie toutes les propriétés.

---

## 137.7 Logs complets

Le corps du webhook ou la réponse provider est journalisé.

---

## 137.8 Retry universel

Toutes les erreurs 4xx sont réessayées.

---

## 137.9 Proxy ambiant

Le comportement change parce que `HTTP_PROXY` existe sur l’hôte.

---

## 137.10 Webhook non signé

Une URL secrète constitue la seule protection.

---

## 137.11 Integration sans kill switch

Aucun moyen rapide n’existe pour arrêter l’exfiltration ou les coûts.

---

## 137.12 Provider dans Domain

Le code métier dépend directement d’un SDK tiers.

---

# 138. Conséquences positives

La décision apporte :

- prévention SSRF ;
- inventaire complet ;
- credentials maîtrisés ;
- payloads minimisés ;
- résilience ;
- kill switches ;
- clients testables ;
- meilleure observabilité ;
- compatibilité jobs ;
- transfert facilité ;
- préparation d’un proxy d’egress ;
- réduction du verrouillage fournisseur ;
- webhooks sécurisés.

---

# 139. Conséquences négatives

La décision entraîne :

- catalogue ;
- manifests ;
- validation DNS ;
- clients typés ;
- tests réseau ;
- policies ;
- maintenance des providers ;
- gestion des exceptions ;
- complexité des providers internes ;
- davantage de procédures opérationnelles.

---

# 140. Risques acceptés

## 140.1 Provider utilisant des IPs dynamiques

L’allowlist hostname reste nécessaire.

Réduction :

- validation à chaque nouvelle connexion ;
- contrôle des adresses ;
- pool borné.

---

## 140.2 Compromission d’un provider autorisé

Une destination légitime peut être compromise.

Réduction :

- payload minimisé ;
- credential limité ;
- kill switch ;
- monitoring ;
- revue fournisseur.

---

## 140.3 Contrôle réseau non total

Le MVP ne dispose pas nécessairement d’un proxy d’egress bloquant tout trafic au niveau réseau.

Réduction :

- clients typés ;
- images durcies ;
- réseau Docker ;
- absence d’outils inutiles ;
- future défense réseau.

---

## 140.4 Doublon externe

Un provider sans idempotence peut recevoir un effet plusieurs fois.

Réduction :

- delivery registry ;
- contenus tolérants ;
- réconciliation.

---

# 141. Mesures compensatoires

- Integration Catalog ;
- Egress Manifest ;
- typed clients ;
- DNS et IP validation ;
- TLS ;
- redirects désactivés ;
- proxy ambiant désactivé ;
- payload allowlist ;
- timeouts ;
- retries bornés ;
- circuit breaker ;
- idempotency keys ;
- signatures webhook ;
- kill switches ;
- audit ;
- tests SSRF ;
- runbooks.

---

# 142. Performance

## 142.1 DNS

La validation DNS ajoute un coût à la création d’une connexion.

---

## 142.2 Pooling

Les connexions validées peuvent être réutilisées pendant une durée bornée.

---

## 142.3 Streaming

Les grands payloads et réponses utilisent le streaming.

---

## 142.4 Retries

Les retries ne doivent pas amplifier durablement une panne.

---

## 142.5 Observabilité

Les métriques restent à faible cardinalité.

---

# 143. Coûts

## 143.1 Coûts supplémentaires

- développement ;
- tests ;
- gestion de providers ;
- monitoring ;
- credentials ;
- opérations ;
- éventuel proxy futur.

---

## 143.2 Coûts évités

- exfiltration ;
- SSRF ;
- fuite de credentials ;
- provider incontrôlé ;
- appels infinis ;
- coûts API excessifs ;
- synchronisations dupliquées ;
- incidents difficiles à stopper ;
- transfert client incomplet.

---

# 144. Maintenabilité

Pour chaque intégration, il doit être possible de répondre :

```text
Quel est son Integration ID ?

Quelles opérations sont autorisées ?

Quelle destination utilise-t-elle ?

Quelles données envoie-t-elle ?

Quel credential utilise-t-elle ?

Quels timeouts et retries ?

Quelle stratégie d’idempotence ?

Quel kill switch ?

Comment est-elle testée ?

Comment est-elle supprimée ou transférée ?
```

---

# 145. Compatibilité

## 145.1 Integration Version

Chaque intégration possède une version.

---

## 145.2 Operation Version

Une évolution incompatible crée un nouvel Operation ID ou une nouvelle version.

---

## 145.3 Endpoint

Un changement d’endpoint ne modifie pas nécessairement le contrat métier, mais nécessite une revue et un audit.

---

## 145.4 Proxy futur

Les contrats restent compatibles avec un proxy d’egress.

---

## 145.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

La migration d’appels libres nécessite un inventaire et un refactoring.

---

# 146. Migration depuis des `HttpClient` libres

Le processus est :

```text
inventorier les appels

identifier les providers

identifier les données transmises

définir les Integration IDs

définir les Operation IDs

créer les clients typés

ajouter les policies

ajouter les tests SSRF

retirer les URLs runtime libres

produire le manifest
```

---

# 147. Migration depuis des webhooks non signés

Le processus est :

```text
définir un secret

versionner le payload

ajouter timestamp

ajouter Event ID

signer

supporter une période de transition

surveiller

rendre la signature obligatoire

retirer l’ancien mode
```

---

# 148. Migration vers un proxy d’egress

Le processus futur est :

```text
déployer le proxy

configurer les origines

configurer l’authentification

diriger les clients approuvés

désactiver l’accès direct

tester

observer

durcir les règles host
```

---

# 149. Transfert client

Le handover doit inclure :

- Egress Manifest ;
- Integration Catalog résolu ;
- providers ;
- Operation IDs ;
- Secret IDs à recréer ;
- kill switches ;
- webhooks ;
- contrats ;
- data processors ;
- runbooks ;
- tests de connexion.

---

## 149.1 Credentials

Les valeurs sont transférées séparément ou recréées.

---

## 149.2 Endpoints LogicGate

Les endpoints appartenant à LogicGate sont retirés ou transférés explicitement.

---

# 150. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- d’appeler un provider réel ;
- de modifier un endpoint ;
- d’ajouter un webhook ;
- de faire tourner un credential ;
- d’activer un kill switch ;
- d’ajouter une règle réseau ;
- de modifier Docker Compose ;
- d’envoyer des données réelles ;
- d’appliquer une migration.

---

# 151. Réversibilité

## 151.1 Classification

```text
élevée avant activation

moyenne après synchronisation externe

faible après diffusion de données à un tiers
```

---

## 151.2 Désactivation

Un provider peut être désactivé sans retirer son module.

---

## 151.3 Changement de provider

L’abstraction permet un remplacement sous réserve :

- de migration ;
- de données ;
- de contrats ;
- de suppression chez l’ancien fournisseur.

---

# 152. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `IntegrationId` ;
- `OperationId` ;
- profiles ;
- errors ;
- policy results.

---

## Étape 2 — Catalogue

Créer :

- Integration Catalog ;
- Operation Registry ;
- Egress Manifest.

---

## Étape 3 — URL et DNS

Ajouter :

- canonicalisation ;
- validation des schemes ;
- ports ;
- DNS ;
- IP ;
- connexion couplée.

---

## Étape 4 — Clients HTTP

Ajouter :

- typed clients ;
- factory ;
- headers ;
- timeouts ;
- response limits ;
- proxy policy.

---

## Étape 5 — Résilience

Ajouter :

- retry ;
- jitter ;
- circuit breaker ;
- kill switches ;
- error mapping.

---

## Étape 6 — Credentials

Intégrer :

- Secret IDs ;
- rotation ;
- mTLS futur ;
- non-divulgation.

---

## Étape 7 — Premier provider

Migrer :

```text
SMTP
```

ou :

```text
notification provider
```

vers le catalogue.

---

## Étape 8 — Webhooks

Ajouter :

- outbound signing ;
- inbound verification ;
- replay protection ;
- jobs.

---

## Étape 9 — Docker

Ajouter :

- réseaux ;
- séparation web/worker ;
- manifest ;
- validations de production.

---

## Étape 10 — Quality gates

Ajouter :

- tests SSRF ;
- TLS ;
- redirects ;
- proxies ;
- payloads ;
- non-divulgation ;
- runbooks.

---

# 153. Tâches liées proposées

```text
SF-TASK-332 — Define third-party integration contracts

SF-TASK-333 — Create the versioned integration catalog

SF-TASK-334 — Create the operation registry

SF-TASK-335 — Generate the runtime egress manifest

SF-TASK-336 — Add outbound URL canonicalization

SF-TASK-337 — Add IPv4 and IPv6 SSRF destination blocking

SF-TASK-338 — Add validated DNS resolution and connection handling

SF-TASK-339 — Add provider-specific typed HTTP clients

SF-TASK-340 — Disable ambient HTTP proxy usage

SF-TASK-341 — Add bounded request and response handling

SF-TASK-342 — Add provider retry and circuit-breaker policies

SF-TASK-343 — Add provider-level kill switches

SF-TASK-344 — Add integration credential references and rotation

SF-TASK-345 — Add outbound webhook signing

SF-TASK-346 — Add inbound webhook signature and replay verification

SF-TASK-347 — Add SMTP to the integration catalog

SF-TASK-348 — Add external-provider privacy declarations

SF-TASK-349 — Add Docker egress-network separation

SF-TASK-350 — Add integration observability and auditing

SF-TASK-351 — Add SSRF, DNS-rebinding and redirect security tests

SF-TASK-352 — Add integration operational runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 154. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Toutes les integrations possèdent un Integration ID.

Toutes les opérations possèdent un Operation ID.

Les modules n’utilisent pas de HttpClient générique.

Les URLs utilisateur ne sont jamais appelées côté serveur.

Les endpoints publics utilisent HTTPS.

La validation TLS ne peut pas être désactivée.

Les redirects automatiques sont désactivés.

Les destinations loopback sont refusées.

Les destinations privées sont refusées.

Les destinations link-local et metadata sont refusées.

IPv4 et IPv6 sont traitées.

Une résolution mixte public/privé est refusée.

La connexion utilise l’adresse DNS validée.

Le proxy ambiant est désactivé.

Aucun credential n’apparaît dans une URL.

Les headers entrants ne sont pas transmis.

Les payloads utilisent des DTOs allowlistés.

Les catégories de données sont vérifiées.

Les requêtes et réponses sont bornées.

Chaque opération possède un timeout.

Les retries sont bornés.

Chaque provider possède un kill switch.

Les effets durables utilisent les jobs.

Les webhooks sortants sont signés.

Les webhooks entrants vérifient signature, timestamp et replay.

PostgreSQL ne possède aucun egress Internet nécessaire.

Le Egress Manifest est généré.

Les logs, métriques et audits ne contiennent aucun secret.

Les tests SSRF couvrent IPv4, IPv6, DNS et redirects.

Les tests fonctionnent sur Windows et Linux.
```

---

# 155. Conditions de révision

Réexaminer cette décision si :

- un proxy d’egress devient obligatoire ;
- plusieurs instances deviennent la norme ;
- une architecture multi-tenant est introduite ;
- des clients configurent eux-mêmes leurs webhooks ;
- un service mesh est ajouté ;
- des private endpoints cloud sont utilisés ;
- plusieurs régions sont déployées ;
- un fournisseur exige du TCP non HTTP ;
- une conformité impose du certificate pinning ;
- des incidents SSRF sont observés ;
- les integrations deviennent suffisamment nombreuses pour nécessiter une plateforme centrale ;
- les applications utilisent un cluster orchestré.

---

# 156. Indicateurs à suivre

- providers actifs ;
- opérations actives ;
- appels externes ;
- erreurs ;
- timeouts ;
- retries ;
- circuits ouverts ;
- kill switches actifs ;
- destinations refusées ;
- résolutions DNS refusées ;
- webhooks livrés ;
- webhooks en dead-letter ;
- replays rejetés ;
- credentials expirés ;
- providers non revus ;
- integrations absentes du Privacy Catalog ;
- composants possédant un egress inutile.

---

# 157. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des URLs utilisateur sont appelées

des HttpClient libres réapparaissent

la validation TLS est contournée

des redirects sont suivis sans contrôle

des destinations privées sont accessibles

les credentials apparaissent dans les logs

les payloads s’agrandissent sans revue

les retries saturent un provider

les kill switches ne sont jamais testés

les webhooks ne sont pas signés

des providers transmettent des données non déclarées

le manifest ne correspond plus au runtime

des composants possèdent un accès Internet inutile
```

---

# 158. Conséquences observées

Cette section est append-only après mise en œuvre.

## 158.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
du catalogue d’intégrations,
du manifest d’egress
et des protections SSRF.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- Integration ID ;
- Operation ID ;
- provider ;
- volume ;
- résultat ;
- incident ou bénéfice ;
- action proposée.

---

# 159. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0031-application-startup-validation-and-runtime-compatibility.md](0031-application-startup-validation-and-runtime-compatibility.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](0028-database-transaction-boundaries-and-concurrency-control.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- Une éventuelle intégration SEO reste soumise aux destinations approuvées, limites d'egress, prévention SSRF et contrôles de secrets du présent ADR ; aucun document SEO privé n'est requis.
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
- [0006-feature-flags-and-entitlements-separation.md](0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](0007-module-composition-and-public-contracts.md)
- [0008-private-package-feed-and-supply-chain.md](0008-private-package-feed-and-supply-chain.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](0012-runtime-configuration-and-secrets.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](0014-explicit-client-upgrades-and-version-pinning.md)
- [0015-deterministic-generation-and-optional-ai.md](0015-deterministic-generation-and-optional-ai.md)
- [0016-content-and-media-storage-boundaries.md](0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](0017-background-jobs-and-reliable-side-effects.md)
- [0018-public-form-abuse-protection-and-rate-limiting.md](0018-public-form-abuse-protection-and-rate-limiting.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](0019-audit-trail-and-sensitive-administrative-actions.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](0021-administrator-provisioning-invitations-and-account-recovery.md)
- [0022-browser-security-headers-and-content-security-policy.md](0022-browser-security-headers-and-content-security-policy.md)
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0024-client-data-export-and-portability.md](0024-client-data-export-and-portability.md)

---

# 160. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 155 |

---

# 161. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 162. Synthèse de la décision

```text
Catalogue
→ Integration IDs
→ Operation IDs
→ profiles
→ destinations exactes
→ catégories de données
→ Secret IDs
→ kill switches

Code
→ contrats Application
→ clients typés Infrastructure
→ aucun HttpClient libre
→ aucune URL utilisateur

Destination
→ HTTPS
→ host canonique
→ port allowlisté
→ aucune URL avec credential
→ aucun wildcard

SSRF
→ DNS contrôlé
→ toutes les adresses validées
→ loopback refusé
→ private refusé
→ link-local refusé
→ metadata refusé
→ IPv4 et IPv6
→ connexion vers l’IP validée

Transport
→ TLS vérifié
→ redirects désactivés
→ proxy ambiant désactivé
→ timeouts
→ tailles bornées
→ streaming

Données
→ DTOs allowlistés
→ catégories déclarées
→ minimisation
→ aucun secret
→ aucun header entrant transmis

Résilience
→ retries bornés
→ jitter
→ idempotency keys
→ circuit breaker
→ kill switch
→ dead-letter

Webhooks sortants
→ destination approuvée
→ payload versionné
→ HMAC
→ timestamp
→ Event ID
→ retry durable

Webhooks entrants
→ taille
→ rate limit
→ signature
→ timestamp
→ replay
→ persistance
→ traitement par worker

Infrastructure
→ PostgreSQL interne
→ worker chargé des effets externes
→ manifest d’egress
→ réseaux séparés
→ aucun Docker socket
→ proxy futur possible

Opérations
→ validation au démarrage
→ tests de provider
→ audit des changements
→ observabilité sans payload
→ handover avec liste des secrets à recréer
```

> Une intégration externe ne doit pas être considérée comme sûre uniquement parce qu’elle utilise HTTPS ou un SDK officiel. SiteFactory doit savoir précisément quelle destination peut être jointe, quelles données peuvent lui être transmises, avec quel credential, pendant combien de temps et comment couper immédiatement la communication en cas d’incident.
