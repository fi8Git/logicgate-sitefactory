# LogicGate SiteFactory — Baseline de sécurité

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Baseline de sécurité |
| Version | 1.0 |
| Statut | Proposition à valider |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../03-generation/13-generation-pipeline.md`, `../adr/0009-client-customizations-and-extension-points.md`, `../02-architecture/core-module-boundary.md`, `09-module-architecture.md`, `10-feature-flags-and-entitlements.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `13-generation-pipeline.md`, `14-client-configuration-schema.md`, `15-cli-specification.md`, `../adr/0015-deterministic-generation-and-optional-ai.md`, `Contrat SEO du site : title et description, H1, canonical, robots, sitemap, JSON-LD, slugs, redirections et domaine coherents ; contenu factuel sans promesse de classement` |
| Dernière mise à jour | 2026-11-09 |

## Périmètre public et niveau de preuve

Cette baseline conserve des **exigences de sécurité**, y compris pour des
capacités futures ; elle n'est pas un inventaire de fonctions déjà livrées.
Le périmètre public actuel comprend le Core, ModuleSdk, DocumentationModule,
la CLI et les bibliothèques runtime/PostgreSQL génériques. Les sources, fixtures,
modules et exécutables Business ne sont ni requis pour son build ni inclus dans
sa validation. La dépendance va du privé vers les contrats publics, jamais
l'inverse ([ADR-0033](../adr/0033-split-public-sitefactory-and-private-logicgate-modules.md)).

Les protections Identity, administration, formulaires, uploads, audit métier,
CSP, conteneurs applicatifs et exploitation décrites plus bas restent des
politiques à appliquer lorsqu'un composant les implémente. Elles ne constituent
pas une application métier publique disponible ni une preuve de production.
Les contrôles publics réellement implémentés incluent le confinement des chemins,
la génération déterministe, la protection des customizations, les diagnostics
non sensibles, le refus des secrets invalides et les frontières PostgreSQL
Owner/Migrator/Web/Worker. Aucune auto-migration runtime n'est autorisée.

La [CI publique](../../.github/workflows/ci.yml) isole les sources autorisées,
vérifie restore verrouillé, build Release, guards, symboles, binaires, discovery,
TRX, provenance et cleanup. Une campagne distante complète a validé **onze suites
offline / 2 726 cas Passed sous Windows** et les **mêmes 2 726 cas sous Linux**,
et **23 Facts PostgreSQL réels** dans cinq shards ; ce n'est pas 5 452 scénarios
fonctionnels distincts. L'agrégation refuse les preuves absentes, incohérentes ou
non Passed. Cette preuve porte sur le scope public isolé dans le dépôt de
préparation privé ; elle ne qualifie aucun check Business ni une disponibilité
ou une sécurité absolue.

Le véritable dépôt public n'est pas encore créé. Sa CI, ses protections de
branche et le **SourceLink public PENDING** devront être vérifiés dans ce dépôt,
sans URL inventée ni désactivation de SourceLink. Les détails de provenance et
de gouvernance restent dans le suivi privé, pas dans les artefacts publics.
Voir la [stratégie de tests](19-testing-strategy.md) et les
[gates réellement exécutés](20-quality-gates-and-ci.md).

---

## 1. Objet du document

Ce document définit la baseline de sécurité obligatoire pour :

- le moteur LogicGate SiteFactory ;
- les modules SiteFactory ;
- les templates ;
- les thèmes ;
- les projets clients générés ;
- les pipelines CI/CD ;
- les images Docker ;
- les déploiements VPS ;
- les opérations de maintenance.

Il précise notamment :

- le modèle de menace initial ;
- les responsabilités ;
- les contrôles obligatoires ;
- les configurations sûres par défaut ;
- la protection des applications web ;
- l’authentification et l’autorisation ;
- la protection des formulaires ;
- la gestion des secrets ;
- la sécurité des données ;
- les uploads ;
- les appels externes ;
- la journalisation ;
- les dépendances ;
- les conteneurs ;
- l’infrastructure ;
- les sauvegardes ;
- les contrôles CI ;
- les tests de sécurité ;
- la gestion des vulnérabilités ;
- les exceptions ;
- les contraintes applicables à Codex.

Cette baseline constitue un minimum.

Un projet sensible ou réglementé doit ajouter des contrôles spécifiques après analyse de risque.

---

## 2. Vision de sécurité

SiteFactory doit produire des applications sécurisées par défaut.

La sécurité ne doit pas dépendre :

- de la vigilance du client ;
- d’une option visuelle ;
- d’une action manuelle non documentée ;
- d’un service mensuel optionnel ;
- d’un feature flag commercial ;
- d’un contrôle uniquement effectué dans le navigateur ;
- d’un secret présent dans le repository ;
- d’un opérateur se souvenant de chaque réglage.

Le principe attendu est :

```text
configuration sûre
→ génération sûre
→ build contrôlé
→ tests
→ scans
→ déploiement contrôlé
→ exploitation surveillée
→ correctifs
```

---

## 3. Principes fondamentaux

## 3.1 Sécurité par défaut

La configuration par défaut doit être la plus restrictive compatible avec le fonctionnement attendu.

Exemples :

```text
Inscription publique désactivée

PostgreSQL non exposé publiquement

Cookies Secure et HttpOnly

Routes admin protégées

Antiforgery activé

CORS désactivé sauf besoin explicite

Uploads refusés sauf module prévu

AdminSeed désactivé en production

Détails d’erreur masqués en production
```

---

## 3.2 Refus par défaut

Le système applique :

```text
Default deny
```

En cas d’état inconnu, invalide ou incohérent :

- l’accès est refusé ;
- l’action n’est pas exécutée ;
- la fonctionnalité reste désactivée ;
- la génération échoue si la sécurité est affectée.

---

## 3.3 Défense en profondeur

Une protection importante ne doit pas reposer sur un seul contrôle.

Exemple pour une action d’export :

```text
Module entitled
+ feature activée
+ utilisateur authentifié
+ permission accordée
+ endpoint protégé
+ validation serveur
+ limite de volume
+ neutralisation CSV
+ audit
```

---

## 3.4 Privilège minimal

Chaque composant utilise uniquement les droits nécessaires.

Cela concerne :

- utilisateurs ;
- rôles ;
- processus ;
- compte PostgreSQL ;
- permissions GitHub Actions ;
- accès aux volumes ;
- comptes VPS ;
- tokens ;
- intégrations externes.

---

## 3.5 Séparation des responsabilités

Les responsabilités suivantes restent distinctes :

```text
Développement
Validation
Déploiement
Migration
Sauvegarde
Restauration
Gestion des secrets
Administration fonctionnelle
```

Une seule personne peut remplir plusieurs rôles dans une petite structure, mais les opérations restent conceptuellement séparées et documentées.

---

## 3.6 Aucun contournement silencieux

Une mesure de sécurité ne doit pas être désactivée pour :

- faire passer un test ;
- corriger rapidement un formulaire ;
- simplifier un déploiement ;
- rendre une CI verte ;
- contourner une incompatibilité ;
- gagner du temps.

Toute exception nécessite une décision explicite et documentée.

---

## 3.7 La sécurité fondamentale n’est pas une option commerciale

Les éléments suivants restent obligatoires, même sans service mensuel :

- validation serveur ;
- encodage des sorties ;
- authentification ;
- autorisation ;
- antiforgery ;
- HTTPS ;
- protection des secrets ;
- restrictions PostgreSQL ;
- erreurs de production sobres ;
- absence de mots de passe en clair ;
- dépendances versionnées ;
- sauvegarde ou transfert documenté de cette responsabilité.

---

# 4. Non-objectifs

La baseline MVP ne cherche pas à garantir :

- une sécurité absolue ;
- une certification réglementaire ;
- une conformité automatique à toutes les normes ;
- une protection contre tous les scénarios ;
- un SOC permanent ;
- une infrastructure Zero Trust complète ;
- un WAF obligatoire ;
- une détection comportementale avancée ;
- un SIEM centralisé ;
- une gestion de clés HSM ;
- une architecture multi-région ;
- un pentest systématique pour chaque petit projet ;
- une assurance juridique de conformité.

Ces éléments peuvent être requis pour des projets spécifiques.

---

# 5. Périmètre de confiance

## 5.1 Composants principaux

```text
Navigateur public
Navigateur administrateur
Caddy
Application ASP.NET Core
PostgreSQL
Volume App_Data
Service SMTP
Feed NuGet
GitHub Actions
Repository client
VPS
Opérateur LogicGate
```

---

## 5.2 Flux conceptuel

```text
Visiteur
→ HTTPS
→ Caddy
→ Application ASP.NET Core
→ PostgreSQL
→ App_Data
→ Service SMTP éventuel
```

Flux opérateur :

```text
Opérateur LogicGate
→ SSH
→ VPS
→ Docker Compose
→ Application et base client
```

Flux de construction :

```text
Repository
→ GitHub Actions
→ restore
→ build
→ tests
→ scans
→ image Docker éventuelle
```

---

## 5.3 Frontières de confiance

Frontières principales :

```text
Internet → Caddy

Caddy → application

Application → PostgreSQL

Application → stockage persistant

Application → fournisseur SMTP

Repository → pipeline CI

Pipeline → registry ou VPS

Opérateur → VPS

SiteFactory → repository généré
```

Toute donnée franchissant une frontière doit être :

- validée ;
- autorisée ;
- limitée ;
- journalisée sobrement lorsque nécessaire.

---

# 6. Actifs à protéger

Les actifs principaux sont :

- comptes administrateurs ;
- mots de passe ;
- cookies ;
- tokens ;
- clés Data Protection ;
- chaînes de connexion ;
- données de leads ;
- contenus non publiés ;
- documents ;
- médias ;
- base PostgreSQL ;
- backups ;
- code source ;
- pipelines ;
- packages ;
- images Docker ;
- configuration client ;
- intégrité des modules ;
- disponibilité du site ;
- réputation du client et de LogicGate.

---

# 7. Acteurs de menace

Le modèle initial considère notamment :

- visiteur malveillant ;
- bot automatisé ;
- utilisateur authentifié sans permission ;
- administrateur compromis ;
- attaquant ayant obtenu un secret ;
- dépendance compromise ;
- package malveillant ;
- pipeline compromis ;
- serveur compromis ;
- opérateur effectuant une mauvaise manipulation ;
- contenu ou fichier hostile ;
- fournisseur externe compromis ;
- source LLM contenant une injection de prompt.

---

# 8. Menaces principales

Menaces prioritaires :

```text
Vol de compte

Brute force

Accès non autorisé

Élévation de privilèges

CSRF

XSS

Injection SQL

Injection de commande

Path traversal

Upload malveillant

Exposition de secrets

Fuite de données personnelles

Redirection ouverte

SSRF

Dépendance vulnérable

Image Docker compromise

Base exposée publiquement

Backup non protégé

Logs contenant des données sensibles

Mauvaise configuration de production

Migration destructive

Suppression accidentelle

Déni de service applicatif

Spam de formulaires
```

---

# 9. Niveaux de risque des projets

## 9.1 Niveau standard

Exemples :

- site vitrine PME ;
- formulaire de contact ;
- gestion simple des leads ;
- contenus administrables ;
- portfolio.

La baseline de ce document s’applique intégralement.

---

## 9.2 Niveau renforcé

Exemples :

- documents clients ;
- portail ;
- données métier confidentielles ;
- exports importants ;
- automatisations ;
- plusieurs administrateurs ;
- intégrations externes.

Le projet nécessite :

- analyse de risque complémentaire ;
- revue d’architecture ;
- journalisation métier adaptée ;
- MFA envisagée ;
- scans supplémentaires ;
- procédures d’incident renforcées.

---

## 9.3 Projet spécifique ou réglementé

Exemples :

- données de santé ;
- données financières détaillées ;
- données judiciaires ;
- pièces d’identité ;
- données de mineurs ;
- données biométriques ;
- systèmes critiques ;
- informations hautement confidentielles.

Le projet doit être reclassé en :

```text
Projet sur mesure
```

Il ne peut pas être généré comme un projet standard sans cadrage complémentaire.

---

# 10. Responsabilités

## 10.1 SiteFactory

SiteFactory fournit :

- configurations sûres ;
- protections applicatives ;
- tests ;
- scripts ;
- documentation ;
- contrôles du pipeline ;
- artefacts de déploiement sécurisés ;
- diagnostics.

---

## 10.2 LogicGate

LogicGate est responsable notamment de :

- validation du projet ;
- gestion du repository ;
- gestion des secrets ;
- application des migrations ;
- déploiement ;
- mises à jour ;
- contrôle des backups ;
- gestion des incidents ;
- revue des alertes ;
- correction des vulnérabilités ;
- transfert documenté des responsabilités si le client reprend l’exploitation.

---

## 10.3 Client

Selon le contrat, le client est responsable notamment de :

- comptes de ses utilisateurs ;
- qualité des mots de passe ;
- attribution des rôles ;
- validation des contenus ;
- signalement des départs ;
- confidentialité de ses accès ;
- légitimité des données collectées ;
- validation des informations publiées.

---

# 11. Configuration sûre du framework

## 11.1 Environnement de production

La production utilise :

```text
ASPNETCORE_ENVIRONMENT=Production
```

---

## 11.2 Pages d’erreur

La production ne doit pas exposer :

- Developer Exception Page ;
- stack trace ;
- chemins locaux ;
- détails SQL ;
- noms de packages internes ;
- configuration ;
- secrets ;
- code source.

---

## 11.3 HTTPS

La production utilise HTTPS.

Le trafic HTTP public est redirigé vers HTTPS.

---

## 11.4 Forwarded headers

Lorsque Caddy termine TLS, l’application doit traiter correctement les forwarded headers.

La configuration doit :

- être appliquée avant les middlewares dépendant du schéma ou de l’IP ;
- n’accepter que les proxys ou réseaux connus ;
- ne pas faire confiance arbitrairement à tout header provenant d’Internet.

---

## 11.5 HSTS

HSTS doit être activé en production après validation HTTPS.

L’option :

```text
includeSubDomains
```

ne doit être activée que si tous les sous-domaines concernés utilisent correctement HTTPS.

Le mode développement ne doit pas imposer HSTS de production.

---

# 12. HTTPS et terminaison TLS

## 12.1 Caddy

Le profil MVP utilise Caddy pour :

- certificat ;
- renouvellement ;
- redirection HTTPS ;
- reverse proxy ;
- headers adaptés.

---

## 12.2 Ports publics

Seuls les ports nécessaires sont exposés :

```text
22
80
443
```

Le port SSH peut être restreint davantage selon l’exploitation.

---

## 12.3 Application

Le port interne de l’application n’est pas publié directement sur Internet si Caddy est utilisé.

---

## 12.4 PostgreSQL

Le port PostgreSQL :

```text
5432
```

ne doit pas être publié publiquement.

---

## 12.5 Certificats

Les clés privées de certificats :

- restent hors Git ;
- restent hors image ;
- restent hors logs ;
- sont gérées par Caddy ou un mécanisme approuvé.

---

# 13. Headers de sécurité

## 13.1 Baseline

Le profil de production doit définir selon compatibilité :

```text
Strict-Transport-Security
Content-Security-Policy
X-Content-Type-Options
Referrer-Policy
Permissions-Policy
Cross-Origin-Opener-Policy
Cross-Origin-Resource-Policy
```

Un fallback :

```text
X-Frame-Options
```

peut être conservé en complément de `frame-ancestors`.

---

## 13.2 `X-Content-Type-Options`

Valeur :

```text
nosniff
```

---

## 13.3 `Referrer-Policy`

Valeur initiale recommandée :

```text
strict-origin-when-cross-origin
```

---

## 13.4 Protection contre l’intégration en iframe

Valeur CSP recommandée par défaut :

```text
frame-ancestors 'none'
```

Si un projet doit être intégré dans un domaine tiers, cette décision nécessite un cadrage spécifique.

---

## 13.5 `Permissions-Policy`

La politique doit désactiver les capacités navigateur inutilisées.

Exemples :

```text
camera
microphone
geolocation
payment
usb
```

Une capacité ne doit être autorisée que si le projet l’utilise réellement.

---

## 13.6 Test

Les headers doivent être vérifiés par des tests HTTP sur l’application générée.

---

# 14. Content Security Policy

## 14.1 Objectif

La CSP réduit notamment les risques :

- XSS ;
- chargement de scripts non prévus ;
- intégration dans une iframe ;
- ressources externes incontrôlées.

---

## 14.2 Baseline conceptuelle

```text
default-src 'self'
base-uri 'self'
object-src 'none'
frame-ancestors 'none'
form-action 'self'
```

Les directives suivantes sont adaptées au mode de rendu :

```text
script-src
style-src
img-src
font-src
connect-src
```

---

## 14.3 Blazor

La CSP doit être compatible avec :

- fichiers Blazor ;
- SignalR ou WebSocket si nécessaire ;
- ressources statiques ;
- Bootstrap ;
- polices ;
- médias.

La politique ne doit pas être désactivée globalement en cas d’incompatibilité.

---

## 14.4 Scripts inline

Les scripts inline doivent être évités.

Lorsqu’ils sont indispensables, privilégier :

- nonce ;
- hash ;
- fichier statique versionné.

---

## 14.5 `unsafe-eval`

`unsafe-eval` doit être interdit par défaut.

Toute exception nécessite :

- justification ;
- périmètre ;
- analyse ;
- durée ;
- plan de suppression.

---

## 14.6 `unsafe-inline`

`unsafe-inline` ne doit pas être ajouté par réflexe.

Une exception temporaire doit être documentée et remplacée par :

- nonce ;
- hash ;
- CSS externe ;
- composant adapté.

---

## 14.7 Déploiement progressif

Une nouvelle CSP peut être testée en mode rapport avant blocage.

Elle ne doit pas rester indéfiniment en mode rapport sans propriétaire ni date de bascule.

---

# 15. Authentification

Les exigences détaillées sont définies dans :

```text
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
```

Baseline obligatoire :

```text
ASP.NET Core Identity

Aucune inscription publique

Comptes individuels

Cookies sécurisés

Lockout

Logout POST

Antiforgery

Provisionnement explicite des administrateurs selon ADR-0021 — contrat cible, non livré

Data Protection persistée
```

La mention historique « AdminSeed contrôlé » appartient à l'ancienne baseline ;
elle ne revendique aucune capacité AdminSeed livrée dans le périmètre public.
Un ancien mécanisme AdminSeed n'est pas présumé équivalent à une invitation
bootstrap. Le contrat cible, non déclaré implémenté, est le
[provisionnement explicite défini par ADR-0021](../adr/0021-administrator-provisioning-invitations-and-account-recovery.md#17-décision) :
invitation ou récupération contrôlée, sans création automatique au démarrage,
sans mot de passe par défaut, fourni par variable d'environnement ou défini
directement par l'opérateur. Les autres interdictions de cet ADR restent
applicables ; aucune de ces mentions historiques ne les contourne.

---

# 16. Cookies

## 16.1 Cookies d’authentification

Propriétés obligatoires en production :

```text
HttpOnly = true
Secure = true
SameSite adapté
Path maîtrisé
Durée limitée
```

---

## 16.2 Cookie `__Host-`

Le préfixe :

```text
__Host-
```

peut être utilisé lorsqu’il est compatible avec la configuration.

Il impose notamment :

- Secure ;
- Path `/` ;
- absence d’attribut Domain.

---

## 16.3 Données dans les cookies

Un cookie ne doit pas contenir :

- mot de passe ;
- secret ;
- donnée métier sensible ;
- contenu de lead ;
- information inutile.

---

## 16.4 Consentement

Les cookies strictement nécessaires à l’authentification ou au fonctionnement suivent le cadre documenté du projet.

Les outils de mesure ou marketing nécessitent une gestion distincte.

---

# 17. Autorisation

## 17.1 Contrôles serveur

Les permissions sont appliquées sur :

- pages ;
- endpoints ;
- commandes ;
- handlers ;
- services ;
- exports ;
- opérations sensibles.

---

## 17.2 Masquage visuel

Un bouton masqué n’est pas une protection.

---

## 17.3 Administrator

Le rôle Administrator ne contourne pas :

- entitlements ;
- features désactivées ;
- modules absents ;
- règles métier ;
- protections de production.

---

## 17.4 Dernier administrateur

La désactivation ou le retrait de rôle du dernier administrateur actif doit être refusé ou fortement contrôlé.

---

# 18. Antiforgery et CSRF

## 18.1 Actions concernées

Toute action utilisant un cookie et modifiant un état doit être protégée.

Exemples :

- logout ;
- création d’utilisateur ;
- changement de rôle ;
- publication ;
- changement de statut ;
- ajout de note ;
- suppression ;
- upload ;
- modification de configuration.

---

## 18.2 Verbes HTTP

Une action mutable utilise :

```text
POST
PUT
PATCH
DELETE
```

Un GET ne doit pas modifier l’état.

---

## 18.3 Interdiction

La protection antiforgery ne doit pas être désactivée globalement pour corriger une erreur de formulaire.

---

## 18.4 APIs externes

Une API utilisant un autre mécanisme d’authentification doit posséder une stratégie distincte.

Elle ne doit pas être exposée automatiquement par un module standard.

---

# 19. Validation des entrées

## 19.1 Principe

Toute entrée externe est non fiable.

Sont notamment concernées :

- formulaires ;
- paramètres de route ;
- query strings ;
- fichiers ;
- headers ;
- configuration ;
- JSON ;
- YAML ;
- résultats d’API ;
- résultats LLM ;
- messages SMTP ;
- noms de fichiers.

---

## 19.2 Validation serveur

La validation côté navigateur améliore l’UX.

La validation serveur reste obligatoire.

---

## 19.3 Listes autorisées

Lorsque possible, utiliser une liste de valeurs autorisées.

Exemples :

- statuts ;
- rôles ;
- permissions ;
- types de page ;
- extensions ;
- variantes ;
- providers ;
- profils de déploiement.

---

## 19.4 Longueurs

Toute chaîne persistée ou traitée doit posséder une taille maximale adaptée.

---

## 19.5 Normalisation

Normaliser avant utilisation lorsque nécessaire :

- espaces ;
- casse ;
- email ;
- slug ;
- chemin ;
- numéro de téléphone ;
- URL.

La normalisation ne remplace pas la validation.

---

## 19.6 Mass assignment

Les modèles de requête doivent exposer uniquement les propriétés modifiables.

Une entité EF Core ne doit pas être bindée directement depuis une requête externe lorsque cela permettrait de modifier :

- identifiant ;
- rôle ;
- état interne ;
- dates techniques ;
- propriétaire ;
- permission ;
- entitlement.

---

# 20. Encodage et protection XSS

## 20.1 Rendu Razor

Le rendu Razor encode les chaînes par défaut.

Cette protection ne doit pas être contournée sans nécessité.

---

## 20.2 `MarkupString`

L’utilisation de :

```csharp
MarkupString
```

ou d’un équivalent de rendu HTML brut est interdite pour du contenu non fiable.

---

## 20.3 Contenu HTML administrable

Si un module permet du HTML enrichi, il doit utiliser :

- un format limité ;
- une sanitisation reconnue ;
- une liste de tags et attributs autorisés ;
- des tests XSS ;
- une CSP adaptée.

---

## 20.4 JavaScript

Les données utilisateur ne doivent pas être concaténées dans du JavaScript.

---

## 20.5 Attributs et URLs

Les URLs et attributs HTML doivent être validés indépendamment de l’encodage.

---

# 21. Injection SQL

## 21.1 EF Core

EF Core et les requêtes paramétrées sont privilégiés.

---

## 21.2 SQL brut

Le SQL brut doit être :

- rare ;
- paramétré ;
- testé ;
- documenté ;
- revu.

---

## 21.3 Interdiction

Exemple interdit :

```csharp
var sql = $"SELECT * FROM leads WHERE email = '{email}'";
```

---

## 21.4 Compte PostgreSQL

L’application utilise un compte dédié non superutilisateur.

---

# 22. Injection de commande

## 22.1 Processus externes

Les commandes sont exécutées avec :

- exécutable explicite ;
- liste d’arguments structurée ;
- répertoire de travail validé ;
- environnement contrôlé ;
- timeout ;
- code de sortie.

---

## 22.2 Shell

L’utilisation d’un shell doit être évitée lorsque l’API de processus permet de lancer directement l’exécutable.

---

## 22.3 Entrées utilisateur

Une valeur externe ne doit jamais être concaténée dans une commande shell.

---

## 22.4 SiteFactory CLI

Les mêmes règles s’appliquent au moteur de génération et aux scripts générés.

---

# 23. Path traversal et système de fichiers

## 23.1 Normalisation

Tout chemin doit être :

- normalisé ;
- résolu ;
- comparé au dossier racine autorisé ;
- rejeté s’il sort du périmètre.

---

## 23.2 Noms de fichiers

Les noms fournis par un utilisateur ne sont pas utilisés directement pour le stockage.

Le système génère un nom interne.

Le nom original peut être conservé comme métadonnée après validation.

---

## 23.3 Liens symboliques

Les liens symboliques dangereux doivent être refusés lorsque leur suivi permet de sortir du dossier autorisé.

---

## 23.4 Dossiers persistants

Les écritures sont limitées aux dossiers prévus :

```text
App_Data/media
App_Data/documents
App_Data/exports
App_Data/DataProtection-Keys
```

---

# 24. Uploads de fichiers

## 24.1 Activation conditionnelle

Les uploads ne sont disponibles que si un module les prévoit explicitement.

Ils sont désactivés par défaut.

---

## 24.2 Taille

Chaque type d’upload possède :

- taille maximale ;
- nombre maximal ;
- timeout ;
- limite de requête ;
- politique de stockage.

---

## 24.3 Type de fichier

Le système doit vérifier :

- extension ;
- type MIME déclaré ;
- signature ou contenu lorsque possible ;
- cohérence entre les informations ;
- format attendu.

Le header `Content-Type` fourni par le navigateur n’est pas suffisant.

---

## 24.4 Extensions autorisées

La stratégie utilise une liste autorisée.

Exemple pour des médias :

```text
.jpg
.jpeg
.png
.webp
```

Les formats exécutables sont interdits.

---

## 24.5 Stockage

Les fichiers sont stockés :

- hors du web root lorsque possible ;
- avec un nom généré ;
- sans droit d’exécution ;
- dans un volume persistant ;
- avec permissions minimales.

---

## 24.6 Téléchargement

Les fichiers privés sont servis via une action autorisée.

Ils ne doivent pas être accessibles uniquement parce que leur URL est connue.

---

## 24.7 Images

Les images peuvent être :

- décodées ;
- réencodées ;
- redimensionnées ;
- débarrassées de métadonnées inutiles selon le besoin.

---

## 24.8 Antivirus

Un scan antivirus ou service équivalent peut être requis pour :

- documents ;
- espaces clients ;
- fichiers confidentiels ;
- volumes importants.

Il n’est pas obligatoire pour le simple MVP d’images contrôlées, mais le risque doit être évalué.

---

## 24.9 Fichier ZIP

Les archives sont refusées par défaut.

Si elles sont acceptées, il faut notamment prévenir :

- Zip Slip ;
- décompression excessive ;
- nombre excessif de fichiers ;
- fichiers imbriqués ;
- extensions interdites.

---

# 25. Formulaires publics

## 25.1 Contrôles obligatoires

Un formulaire public doit posséder :

- validation serveur ;
- limites de longueur ;
- antiforgery lorsque pertinent ;
- rate limiting ;
- protection anti-automatisation ;
- messages d’erreur sobres ;
- persistance contrôlée ;
- absence de données dans les logs.

---

## 25.2 Honeypot

Un honeypot discret peut être utilisé contre les bots simples.

Il ne constitue pas la seule protection.

---

## 25.3 CAPTCHA

Un CAPTCHA externe n’est pas obligatoire par défaut.

Il peut être ajouté si :

- le spam devient significatif ;
- les mesures moins intrusives ne suffisent pas ;
- les impacts de confidentialité et d’accessibilité sont acceptés.

---

## 25.4 Confirmation

Le succès public doit rester générique.

Il ne doit pas exposer :

- identifiant interne ;
- adresse technique ;
- détail SMTP ;
- nom de table ;
- exception.

---

## 25.5 Persistance et email

La persistance et l’envoi d’email doivent être traités séparément.

Exemple :

```text
Lead enregistré
Email de notification échoué
```

La demande ne doit pas être perdue uniquement à cause de l’échec SMTP.

---

# 26. Rate limiting

## 26.1 Périmètre

Le rate limiting doit couvrir selon le projet :

- login ;
- formulaire de contact ;
- formulaire de devis ;
- reset de mot de passe futur ;
- exports ;
- endpoints coûteux ;
- génération IA ;
- uploads.

---

## 26.2 Clés de limitation

Les politiques peuvent utiliser :

- adresse IP ;
- utilisateur ;
- endpoint ;
- combinaison adaptée.

Une limitation uniquement par IP peut pénaliser plusieurs utilisateurs derrière la même connexion.

---

## 26.3 Valeurs

Les valeurs doivent être :

- configurables ;
- testées ;
- documentées par module ;
- suffisamment strictes contre l’abus ;
- suffisamment souples pour les usages normaux.

---

## 26.4 Réponse

Réponse standard :

```text
429 Too Many Requests
```

La réponse peut inclure un délai de nouvelle tentative sans exposer de détail interne.

---

## 26.5 Stockage distribué

Le MVP peut utiliser un limiter en mémoire sur une seule instance.

Cette limitation doit être documentée si plusieurs instances sont déployées ultérieurement.

---

# 27. Protection contre le brute force

La protection du login combine :

```text
ASP.NET Core Identity lockout
+ rate limiting
+ message générique
+ logs sobres
+ mots de passe robustes
```

Le système ne doit pas révéler :

- compte existant ;
- nombre exact d’essais restants ;
- état du compte ;
- email confirmé ou non.

---

# 28. CORS

## 28.1 Baseline

CORS est désactivé si aucune origine externe ne doit appeler l’application depuis un navigateur.

---

## 28.2 Activation

Si CORS est nécessaire, définir explicitement :

- origines ;
- méthodes ;
- headers ;
- usage des credentials ;
- environnements.

---

## 28.3 Interdictions

Sont interdits :

```text
AllowAnyOrigin avec credentials

Origines provenant directement d’une entrée utilisateur

Wildcard permissif en production sans besoin

CORS utilisé comme mécanisme d’autorisation
```

CORS ne protège pas contre les appels depuis Postman, un serveur ou un script non navigateur.

---

# 29. Redirections ouvertes

Les valeurs telles que :

```text
returnUrl
redirectUrl
callbackUrl
```

doivent être :

- locales ;
- validées ;
- comparées à une liste autorisée si externes ;
- refusées lorsqu’elles utilisent un schéma dangereux.

---

## 29.1 Login

Après connexion, `returnUrl` doit pointer vers une route locale autorisée.

---

## 29.2 Liens externes

Une redirection externe nécessite un cas d’usage explicite.

---

# 30. SSRF et appels HTTP sortants

## 30.1 Principe

Une URL fournie par un utilisateur ne doit pas être appelée directement par le serveur.

---

## 30.2 URLs autorisées

Les intégrations doivent utiliser :

- endpoint configuré ;
- HTTPS ;
- domaine approuvé ;
- timeout ;
- limite de réponse ;
- redirections maîtrisées.

---

## 30.3 Destinations interdites

Le système doit éviter les appels vers :

- localhost non prévu ;
- réseau Docker interne ;
- IP privées non autorisées ;
- metadata cloud ;
- fichiers locaux ;
- schémas autres que HTTP/HTTPS.

---

## 30.4 Contenus distants

Le MVP ne télécharge pas arbitrairement :

- template ;
- image ;
- document ;
- script ;
- contenu LLM ;
- package ;

depuis une URL fournie par un visiteur.

---

# 31. Emails

## 31.1 Validation des adresses

Les adresses destinataires et expéditeurs doivent provenir :

- d’une configuration validée ;
- ou d’une donnée utilisateur contrôlée selon le cas.

---

## 31.2 Header injection

Les champs utilisés dans les headers doivent être validés contre :

- retours ligne ;
- caractères de contrôle ;
- contenu arbitraire.

---

## 31.3 Expéditeur

Un email saisi par un visiteur ne doit pas devenir directement l’adresse `From`.

Utiliser :

```text
From = adresse du domaine ou service configuré
Reply-To = adresse utilisateur validée
```

---

## 31.4 Logs

Ne pas logguer :

- corps ;
- adresse complète ;
- téléphone ;
- données du formulaire ;
- credentials SMTP.

---

## 31.5 Retries

Les retries doivent être contrôlés afin d’éviter :

- doubles envois ;
- boucle ;
- spam ;
- coût incontrôlé.

---

# 32. Exports

## 32.1 Autorisation

Les exports nécessitent :

- entitlement ;
- feature ;
- permission ;
- validation serveur.

---

## 32.2 Volume

Les exports doivent être limités.

---

## 32.3 CSV injection

Les cellules commençant par des caractères pouvant être interprétés comme une formule doivent être neutralisées.

Exemples de caractères à analyser :

```text
=
+
-
@
```

---

## 32.4 Fichiers temporaires

Les fichiers temporaires doivent être :

- protégés ;
- supprimés rapidement ;
- exclus du web root ;
- absents des logs.

---

# 33. Gestion des secrets

## 33.1 Secrets concernés

Exemples :

- mots de passe ;
- tokens ;
- clés API ;
- chaînes de connexion ;
- credentials SMTP ;
- clés privées ;
- certificats ;
- credentials NuGet ;
- tokens GitHub ;
- secrets de déploiement.

---

## 33.2 Stockage

Les secrets sont fournis par :

- variables d’environnement ;
- secrets Docker ;
- secrets de CI ;
- secret manager futur ;
- configuration VPS sécurisée.

---

## 33.3 Repository

Le repository contient uniquement :

```text
.env.example
```

avec les noms de variables et des valeurs fictives.

Les fichiers réels sont ignorés par Git.

---

## 33.4 Images Docker

Un secret ne doit pas être :

- copié dans l’image ;
- fourni comme `ARG` persistant ;
- présent dans une couche ;
- écrit dans un fichier publié.

---

## 33.5 Arguments CLI

Les secrets ne doivent pas être passés dans une ligne de commande lorsque cela les expose dans :

- historique ;
- liste des processus ;
- logs.

---

## 33.6 Rotation

Les secrets doivent pouvoir être remplacés sans modifier le code.

---

## 33.7 Secret exposé

Lorsqu’un secret est exposé :

1. révoquer ou remplacer le secret ;
2. analyser son utilisation ;
3. corriger la source de la fuite ;
4. nettoyer l’historique si nécessaire ;
5. vérifier les logs et artefacts ;
6. documenter l’incident.

Supprimer le secret uniquement du dernier commit ne suffit pas.

---

# 34. Détection des secrets

## 34.1 Local et CI

Le projet doit intégrer un contrôle tel que :

```text
Gitleaks
```

ou une solution équivalente approuvée.

---

## 34.2 Périmètre

Le scan couvre :

- code ;
- configuration ;
- historique selon l’outil ;
- workflows ;
- Docker ;
- scripts ;
- documentation ;
- fichiers générés.

---

## 34.3 Faux positifs

Une exclusion doit être :

- minimale ;
- documentée ;
- testée ;
- localisée.

Une règle globale ne doit pas désactiver un type complet de détection sans justification.

---

# 35. Protection des données

Les exigences détaillées sont définies dans :

```text
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
```

Baseline :

```text
Base isolée par client

Compte PostgreSQL dédié

Données minimisées

Rétention définie

Suppression contrôlée

Backups protégés

Aucune PII dans les logs

Exports autorisés

Migrations explicites
```

---

# 36. Chiffrement

## 36.1 En transit

Les communications externes utilisent HTTPS ou un protocole sécurisé adapté.

---

## 36.2 Au repos

Le chiffrement au repos dépend de l’infrastructure :

- disque VPS ;
- backup ;
- stockage externe ;
- fournisseur managé.

Il doit être analysé pour les projets renforcés.

---

## 36.3 Chiffrement applicatif

Le chiffrement applicatif de champs n’est pas ajouté automatiquement.

Il nécessite :

- modèle de menace ;
- gestion des clés ;
- rotation ;
- backup ;
- restauration ;
- recherche ;
- permissions ;
- ADR.

---

## 36.4 Cryptographie maison

Toute cryptographie personnalisée est interdite.

---

# 37. Data Protection

Les clés ASP.NET Core Data Protection doivent être :

- persistées ;
- isolées par client ;
- absentes de Git ;
- protégées ;
- sauvegardées lorsque nécessaire ;
- restaurables.

La perte des clés peut invalider :

- cookies ;
- tokens ;
- données protégées.

---

# 38. PostgreSQL

## 38.1 Isolation réseau

PostgreSQL reste sur un réseau privé.

---

## 38.2 Compte applicatif

Le compte applicatif :

- n’est pas superutilisateur ;
- possède uniquement les droits nécessaires ;
- n’est pas partagé entre plusieurs clients.

---

## 38.3 Mot de passe

Le mot de passe :

- est généré de manière robuste ;
- reste hors Git ;
- reste hors logs ;
- peut être renouvelé.

---

## 38.4 Requêtes

Les requêtes sont paramétrées.

---

## 38.5 Migrations

Les migrations de production sont explicites.

Aucun :

```csharp
Database.Migrate()
```

automatique au démarrage en production.

---

## 38.6 Backup

Les backups PostgreSQL sont protégés comme la base elle-même.

---

# 39. Gestion des erreurs

## 39.1 Production

Les réponses publiques ne contiennent pas :

- stack trace ;
- exception ;
- nom de classe ;
- chemin ;
- SQL ;
- secret ;
- nom de serveur ;
- configuration.

---

## 39.2 Correlation ID

Une erreur peut fournir un identifiant de corrélation.

Exemple :

```text
Une erreur est survenue.
Référence : 7a2f...
```

---

## 39.3 Logs internes

Les logs internes peuvent contenir :

- type d’erreur ;
- code ;
- route ;
- identifiant technique ;
- correlation ID.

Ils ne doivent pas contenir les données sensibles de la requête.

---

## 39.4 Codes métier

Les erreurs attendues utilisent des codes stables.

Les exceptions sont réservées aux erreurs inattendues ou techniques.

---

# 40. Journalisation

## 40.1 Logs structurés

Les logs doivent être structurés.

---

## 40.2 Données interdites

Ne pas logguer :

- mot de passe ;
- token ;
- cookie ;
- chaîne de connexion ;
- clé ;
- corps complet de formulaire ;
- email ;
- téléphone ;
- message ;
- document ;
- prompt LLM ;
- réponse LLM complète.

---

## 40.3 Identifiants autorisés

Selon le contexte :

```text
CorrelationId
OperationId
UserTechnicalId
LeadId
ModuleId
ErrorCode
Route
```

---

## 40.4 Niveaux

Les niveaux doivent être cohérents :

```text
Information
Warning
Error
Critical
```

Un comportement attendu ne doit pas produire une erreur répétitive.

---

## 40.5 Injection de logs

Les valeurs externes doivent être journalisées comme propriétés structurées.

Elles ne doivent pas permettre de falsifier le format des logs par des retours ligne ou caractères de contrôle.

---

# 41. Audit métier

Les opérations sensibles peuvent nécessiter un audit distinct des logs techniques.

Exemples :

```text
UserCreated
RoleAssigned
LeadExported
ContentPublished
AutomationActivated
DataDeleted
ModuleDisabled
```

L’audit doit enregistrer :

- acteur technique ;
- action ;
- cible technique ;
- date UTC ;
- résultat ;
- raison éventuelle.

Il ne doit pas stocker le contenu sensible complet.

---

# 42. Cache et données privées

## 42.1 Administration

Les pages d’administration et réponses contenant des données privées ne doivent pas être mises en cache publiquement.

---

## 42.2 Headers

Une politique telle que :

```text
Cache-Control: no-store
```

peut être utilisée pour les réponses sensibles.

---

## 42.3 Output cache

Le cache de sortie ne doit pas servir une réponse personnalisée à un autre utilisateur.

---

## 42.4 Données d’authentification

Les pages login, reset ou administration nécessitent des règles de cache adaptées.

---

# 43. Stockage navigateur

## 43.1 Tokens

Les tokens d’authentification ou secrets ne doivent pas être stockés dans :

```text
localStorage
sessionStorage
IndexedDB
```

dans le modèle cookie du MVP.

---

## 43.2 Préférences

Le stockage navigateur peut être utilisé pour des préférences non sensibles.

---

## 43.3 Contenu métier

Les données de leads ou documents ne doivent pas être conservés durablement côté navigateur sans besoin explicite.

---

# 44. Dépendances NuGet

## 44.1 Versions exactes

Les packages utilisent des versions explicites.

Les versions flottantes sont interdites.

---

## 44.2 Central Package Management

Les versions sont centralisées dans :

```text
Directory.Packages.props
```

---

## 44.3 Analyse de vulnérabilités

La CI doit analyser :

- dépendances directes ;
- dépendances transitives ;
- packages obsolètes lorsque pertinent ;
- advisories connus.

---

## 44.4 Nouvelle dépendance

Avant ajout, vérifier :

- besoin ;
- licence ;
- maintenance ;
- vulnérabilités ;
- popularité ou maturité ;
- permissions ;
- dépendances transitives ;
- alternative native.

---

## 44.5 Package abandonné

Un package abandonné ou non maintenu ne doit pas devenir une dépendance structurante sans décision explicite.

---

# 45. Supply chain

## 45.1 Sources de packages

Les sources NuGet doivent être explicitement autorisées.

---

## 45.2 Dependency confusion

Les noms des packages internes doivent être réservés dans les feeds utilisés lorsque cela est possible.

L’ordre des sources et le mapping doivent éviter de télécharger un package public portant le même nom qu’un package privé.

---

## 45.3 GitHub Actions

Les actions doivent être :

- approuvées ;
- versionnées ;
- limitées aux permissions nécessaires ;
- idéalement épinglées à une référence immuable selon la politique du repository.

---

## 45.4 Permissions du workflow

La valeur par défaut doit être restrictive.

Exemple :

```yaml
permissions:
  contents: read
```

Les droits d’écriture sont accordés uniquement aux jobs qui en ont besoin.

---

## 45.5 Pull requests externes

Les secrets ne doivent pas être exposés à du code non approuvé exécuté dans une pull request.

---

## 45.6 Artefacts CI

Les artefacts ne doivent pas contenir :

- `.env` ;
- secrets ;
- dumps ;
- clés ;
- données client ;
- configuration réelle de production.

---

# 46. Scans CI obligatoires

Pipeline initial recommandé :

```text
restore
build Release
tests
secret scan
dependency scan
static analysis
generated project validation
container checks lorsque Docker est utilisé
```

---

## 46.1 Secret scanning

Outil possible :

```text
Gitleaks
```

---

## 46.2 Analyse statique

Outils possibles :

```text
Semgrep
CodeQL
analyseurs .NET
```

Le choix exact dépend du repository et des capacités disponibles.

---

## 46.3 Dépendances

Outils ou commandes possibles :

```text
dotnet list package --vulnerable --include-transitive
Dependabot
audit NuGet
```

---

## 46.4 Conteneur

Un scan d’image peut être ajouté lorsque l’image est construite dans la CI.

---

## 46.5 Résultat

Une vulnérabilité bloquante ne doit pas être ignorée silencieusement.

---

# 47. Dockerfile

## 47.1 Build multi-stage

L’image finale contient uniquement :

- runtime ;
- artefacts publiés ;
- fichiers nécessaires.

---

## 47.2 Utilisateur non privilégié

L’application doit s’exécuter avec un utilisateur non root lorsque cela est possible.

---

## 47.3 Image minimale

Utiliser une image runtime adaptée.

Ne pas installer d’outils inutiles dans l’image finale.

---

## 47.4 Secrets

Aucun secret dans :

- Dockerfile ;
- couche ;
- ARG persistant ;
- fichier copié ;
- historique.

---

## 47.5 App_Data

Les données persistantes sont montées via volume.

Elles ne sont pas incluses dans l’image.

---

## 47.6 Permissions

Les dossiers d’écriture sont limités et possèdent les droits nécessaires uniquement.

---

## 47.7 Health check

Un health check peut être défini selon le mode de déploiement.

Il ne doit pas révéler de détail sensible.

---

# 48. Docker Compose

## 48.1 Réseau

Les services utilisent un réseau privé par projet.

---

## 48.2 Ports

Seuls les ports publics nécessaires sont publiés.

---

## 48.3 Volumes

Les volumes sont préfixés par le slug client.

---

## 48.4 Secrets

Le fichier Compose versionné utilise :

- noms de variables ;
- placeholders ;
- références non sensibles.

---

## 48.5 Restart policy

La stratégie de redémarrage doit être adaptée et documentée.

Elle ne doit pas masquer une boucle de crash permanente sans diagnostic.

---

## 48.6 Ressources

Des limites de ressources peuvent être configurées selon le VPS et la criticité du projet.

---

## 48.7 Capabilities

Les capabilities Linux inutiles peuvent être retirées lorsqu’une configuration testée le permet.

---

## 48.8 Read-only filesystem

Un root filesystem en lecture seule peut être envisagé si les seuls dossiers d’écriture sont montés explicitement.

Cette mesure reste conditionnelle à la compatibilité du projet.

---

# 49. VPS

## 49.1 Utilisateur

L’exploitation quotidienne ne doit pas utiliser directement le compte root.

---

## 49.2 SSH

Baseline recommandée :

- authentification par clé ;
- mot de passe SSH désactivé lorsque possible ;
- root login désactivé ;
- clés individuelles ;
- retrait des anciennes clés ;
- permissions de fichiers correctes.

---

## 49.3 Firewall

Le firewall autorise uniquement les ports nécessaires.

---

## 49.4 Mises à jour

Les correctifs de sécurité du système doivent être appliqués régulièrement.

Les mises à jour automatiques de sécurité peuvent être activées selon la stratégie opérateur.

---

## 49.5 Services inutiles

Les services non nécessaires doivent être désactivés ou non installés.

---

## 49.6 Docker

Docker et Compose doivent être maintenus.

L’accès au socket Docker équivaut à un privilège élevé et doit rester limité.

---

## 49.7 Espace disque

Des alertes doivent prévenir :

- saturation ;
- croissance des logs ;
- croissance des backups ;
- croissance des volumes PostgreSQL ;
- croissance des médias.

---

# 50. Caddy

## 50.1 Administration

L’interface d’administration Caddy ne doit pas être exposée publiquement.

---

## 50.2 Configuration

Le Caddyfile versionné ne contient pas de secret.

---

## 50.3 Headers

Les headers appliqués par Caddy et l’application doivent être cohérents.

Ils ne doivent pas produire des valeurs contradictoires.

---

## 50.4 Logs

Les logs d’accès doivent être configurés selon le besoin et la politique de confidentialité.

Ils ne doivent pas conserver inutilement :

- query strings sensibles ;
- tokens ;
- données personnelles.

---

# 51. Backups

## 51.1 Contenu

Les backups peuvent contenir :

- PostgreSQL ;
- médias ;
- documents ;
- clés Data Protection ;
- données personnelles ;
- configuration sensible.

Ils doivent être protégés comme la production.

---

## 51.2 Accès

Seuls les opérateurs autorisés peuvent accéder aux backups.

---

## 51.3 Git

Les backups ne sont jamais committés.

---

## 51.4 Stockage hors serveur

Une copie hors VPS doit être envisagée pour les projets exploités par LogicGate.

---

## 51.5 Chiffrement

Le chiffrement des backups doit être envisagé selon :

- sensibilité ;
- emplacement ;
- fournisseur ;
- transfert ;
- contrat.

---

## 51.6 Restauration

Une restauration doit être testée périodiquement dans un environnement isolé.

---

# 52. Migrations et sécurité

## 52.1 Inspection

Toute migration doit être inspectée.

---

## 52.2 Backup

Une migration de production nécessite un backup préalable lorsque les données sont affectées.

---

## 52.3 Compte de migration

Un compte distinct peut être utilisé pour les migrations lorsqu’un projet nécessite une séparation plus stricte.

---

## 52.4 Auto-migration

Les auto-migrations au démarrage sont interdites en production.

---

## 52.5 Migration destructive

Une migration destructive nécessite :

- analyse ;
- plan ;
- backup ;
- validation ;
- test ;
- rollback ou restauration.

---

# 53. LLM et IA

Les règles détaillées sont définies dans :

```text
../adr/0015-deterministic-generation-and-optional-ai.md
```

Baseline de sécurité :

```text
LLM optionnel

Aucun secret transmis

Données personnelles refusées par défaut pour les providers externes

Sources considérées comme non fiables

Protection contre l’injection de prompt

Sorties validées

Aucune publication automatique

Provider externe explicitement approuvé
```

---

# 54. Feature flags

Un feature flag ne doit pas désactiver :

- authentification ;
- autorisation ;
- antiforgery ;
- validation serveur ;
- protection des secrets ;
- restriction PostgreSQL ;
- encodage ;
- contrôles de sécurité du pipeline.

Un kill switch peut désactiver une fonctionnalité présentant un risque.

Il ne doit jamais activer un module non autorisé.

---

# 55. Health checks

## 55.1 Endpoint public

Le health check public reste minimal.

Exemple :

```json
{
  "status": "Healthy"
}
```

---

## 55.2 Détails

Les détails techniques sont réservés :

- aux logs ;
- aux opérateurs ;
- à une route protégée éventuelle.

---

## 55.3 Dépendances

Les checks peuvent couvrir :

- application ;
- PostgreSQL ;
- App_Data ;
- clés Data Protection ;
- fournisseur externe essentiel.

---

## 55.4 Déni de service

Le health check ne doit pas exécuter un traitement coûteux ou exposer une liste détaillée de dépendances à chaque appel public.

---

# 56. Disponibilité et déni de service

## 56.1 Limites

Les opérations coûteuses doivent posséder :

- pagination ;
- limite ;
- timeout ;
- annulation ;
- taille maximale ;
- rate limiting si exposées.

---

## 56.2 Requêtes

Éviter :

- chargement de tables complètes ;
- exports illimités ;
- uploads sans limite ;
- génération IA sans plafond ;
- regex vulnérables à des entrées excessives ;
- traitements synchrones très longs.

---

## 56.3 Base

Les requêtes fréquentes doivent posséder des index adaptés.

---

## 56.4 Logs

Une attaque ne doit pas provoquer une saturation disque uniquement par accumulation incontrôlée de logs.

---

# 57. Sécurité des médias publics

Les médias publics doivent :

- avoir un identifiant ou chemin contrôlé ;
- ne pas révéler un chemin système ;
- posséder un type correct ;
- être servis avec `nosniff` ;
- ne pas être exécutables ;
- respecter les droits d’utilisation ;
- ne pas contenir de métadonnées sensibles inutiles.

---

# 58. Sécurité des contenus administrables

## 58.1 Brouillons

Les brouillons ne sont pas accessibles publiquement.

---

## 58.2 Prévisualisation

La prévisualisation est :

- authentifiée ;
- autorisée ;
- non indexable ;
- absente du sitemap.

---

## 58.3 Publication

La publication nécessite une permission distincte.

---

## 58.4 Données structurées

Le contenu administrable ne doit pas pouvoir injecter arbitrairement un script JSON-LD non contrôlé.

---

# 59. Pages légales et confidentialité

La génération des pages légales ne constitue pas une validation juridique.

La politique de confidentialité doit refléter les traitements réels :

- formulaires ;
- rétention ;
- prestataires ;
- hébergement ;
- cookies ;
- analytics éventuels ;
- LLM éventuel ;
- transferts éventuels.

Une intégration ne doit pas être activée sans mise à jour des informations correspondantes lorsque nécessaire.

---

# 60. Analytics et scripts tiers

## 60.1 Désactivation par défaut

Les scripts analytics ou marketing ne sont pas ajoutés automatiquement.

---

## 60.2 Activation

Une activation nécessite :

- fournisseur explicite ;
- finalité ;
- configuration ;
- consentement lorsque nécessaire ;
- CSP adaptée ;
- politique de confidentialité ;
- contrôle des données transmises.

---

## 60.3 Scripts arbitraires

Un client ne doit pas pouvoir injecter librement un script dans une zone de contenu standard.

---

# 61. Sécurité des modules

Chaque module doit documenter :

- données ;
- permissions ;
- routes ;
- endpoints ;
- menaces ;
- validation ;
- rate limiting ;
- logs ;
- secrets ;
- fichiers ;
- dépendances ;
- impacts d’exploitation ;
- tests.

Un module ne peut pas être déclaré `available` sans revue adaptée.

---

# 62. Sécurité des templates

Un template ne doit pas :

- exécuter une commande ;
- écrire hors du projet ;
- télécharger un script ;
- intégrer un secret ;
- ajouter une origine CORS ;
- désactiver une protection ;
- injecter une dépendance non déclarée ;
- ajouter un script tiers non approuvé.

Les templates sont considérés comme du code de confiance contrôlé par LogicGate.

---

# 63. Sécurité du moteur SiteFactory

Le moteur doit notamment protéger contre :

- path traversal ;
- liens symboliques dangereux ;
- overwrite de fichiers protégés ;
- commande injectée ;
- template hostile ;
- package non autorisé ;
- secret dans les sorties ;
- écriture hors dossier ;
- génération partielle présentée comme un succès.

---

# 64. Sécurité du CLI

Baseline :

```text
Parsing strict

Options inconnues refusées

Chemins validés

Commandes structurées

Aucun secret en argument

Confirmations spécifiques

Aucune option de force ne contourne les protections

Exit code non nul en cas d’échec

Aucun remote ou déploiement automatique
```

---

# 65. Contrôles de production

La validation de production doit vérifier au minimum :

```text
[ ] ASPNETCORE_ENVIRONMENT=Production
[ ] HTTPS actif
[ ] Redirection HTTP vers HTTPS
[ ] HSTS configuré
[ ] Headers de sécurité présents
[ ] CSP validée
[ ] Cookies Secure et HttpOnly
[ ] AdminSeed désactivé
[ ] Mot de passe AdminSeed absent
[ ] Inscription publique absente
[ ] Developer Exception Page absente
[ ] Routes admin protégées
[ ] Logout POST avec antiforgery
[ ] Rate limiting configuré
[ ] PostgreSQL non public
[ ] Compte PostgreSQL non superutilisateur
[ ] Secrets hors Git et image
[ ] Data Protection persistée
[ ] Backups configurés
[ ] Restauration documentée
[ ] Logs sans PII
[ ] Health check sobre
[ ] Aucun placeholder
[ ] Aucun domaine .test
[ ] Build Release réussi
[ ] Tests réussis
[ ] Secret scan réussi
[ ] Dependency scan réussi
[ ] Analyse statique réussie
```

---

# 66. Security gates du pipeline

## 66.1 Avant génération

```text
Configuration sans secret
Catalogues valides
Chemins sûrs
Modules approuvés
Dépendances compatibles
```

---

## 66.2 Après génération

```text
Restore
Build Release
Tests
Analyse des secrets
Analyse statique
Analyse des dépendances
Validation Docker
Validation Identity
Validation antiforgery
Validation production
```

---

## 66.3 Avant déploiement

```text
Secrets présents dans l’environnement
AdminSeed désactivé ou procédure contrôlée
Backup
Migration inspectée
Configuration HTTPS
Firewall
Volumes
Health check
Rollback
```

---

## 66.4 Après déploiement

```text
Smoke tests
Connexion admin
Accès anonyme refusé
Formulaire public
Headers
Health check
Logs
Backup
PostgreSQL non public
```

---

# 67. Tests obligatoires

## 67.1 Tests d’authentification

Vérifier :

- login valide ;
- login invalide ;
- compte inconnu ;
- compte verrouillé ;
- compte désactivé ;
- message générique ;
- cookie sécurisé ;
- logout POST ;
- session invalidée.

---

## 67.2 Tests d’autorisation

Vérifier :

- utilisateur anonyme ;
- utilisateur authentifié sans permission ;
- rôle limité ;
- Administrator ;
- module absent ;
- feature désactivée ;
- endpoint direct ;
- ancien lien.

---

## 67.3 Tests antiforgery

Vérifier les actions mutables :

- logout ;
- création ;
- modification ;
- suppression ;
- publication ;
- changement de rôle ;
- upload.

---

## 67.4 Tests XSS

Tester notamment :

```text
<script>
javascript:
attributs événementiels
HTML dans les champs
contenu enrichi hostile
```

---

## 67.5 Tests d’injection

Tester :

- caractères SQL ;
- path traversal ;
- commande shell ;
- headers email ;
- redirection ;
- JSON-LD ;
- noms de fichiers.

---

## 67.6 Tests de rate limiting

Vérifier :

- seuil ;
- réponse `429` ;
- récupération après délai ;
- politique par utilisateur ou IP ;
- absence d’effet sur les routes non concernées.

---

## 67.7 Tests des uploads

Vérifier :

- extension interdite ;
- type MIME trompeur ;
- fichier trop volumineux ;
- nom hostile ;
- path traversal ;
- double extension ;
- fichier vide ;
- image invalide ;
- stockage hors web root ;
- téléchargement non autorisé.

---

## 67.8 Tests des headers

Vérifier :

- HSTS en production ;
- absence de HSTS en développement si prévu ;
- CSP ;
- `nosniff` ;
- referrer policy ;
- permissions policy ;
- frame protection.

---

## 67.9 Tests CORS

Vérifier :

- CORS absent par défaut ;
- origine autorisée ;
- origine refusée ;
- credentials ;
- wildcard interdit avec credentials.

---

## 67.10 Tests de secrets

Vérifier :

- secret dans YAML ;
- secret dans appsettings ;
- secret dans Dockerfile ;
- secret dans workflow ;
- secret dans rapport ;
- secret dans logs ;
- secret dans historique d’exemple.

---

## 67.11 Tests des erreurs

Vérifier :

- erreur 404 ;
- erreur 500 ;
- aucune stack trace ;
- aucune donnée sensible ;
- correlation ID ;
- code HTTP correct.

---

## 67.12 Tests Docker

Vérifier :

- image construite ;
- aucun secret ;
- utilisateur non root lorsque configuré ;
- App_Data monté ;
- port attendu ;
- PostgreSQL non public ;
- health check ;
- démarrage en Production.

---

# 68. Tests de contournement

Les tests doivent tenter de contourner l’interface par :

- URL directe ;
- appel HTTP manuel ;
- ancien endpoint ;
- manipulation du formulaire ;
- suppression du token antiforgery ;
- modification du rôle côté client ;
- appel d’une feature masquée ;
- upload forgé ;
- return URL externe ;
- accès direct à un fichier privé.

Le serveur doit rester la source de vérité.

---

# 69. Tests d’architecture

Les tests doivent notamment vérifier :

```text
Aucune référence Infrastructure depuis Domain.

Aucune route admin publique.

Aucune action mutable en GET.

Aucun module générique ne désactive l’antiforgery.

Aucun module ne loggue un modèle contenant des données personnelles.

Aucun secret n’est présent dans les fichiers versionnés.

Aucun Database.Migrate automatique en production.

Aucun module générique ne dépend d’un projet client.

Les endpoints sensibles utilisent une autorisation.

Les fichiers privés ne sont pas servis directement depuis wwwroot.

Les versions de packages ne sont pas flottantes.
```

---

# 70. Analyse statique

Les règles doivent chercher notamment :

- injection ;
- désérialisation dangereuse ;
- path traversal ;
- commande shell ;
- secret ;
- cryptographie faible ;
- validation manquante ;
- XSS ;
- exception exposée ;
- SQL brut ;
- contrôle d’autorisation absent ;
- code mort de sécurité.

Une alerte doit être :

- corrigée ;
- justifiée ;
- ou exclue de manière ciblée.

---

# 71. Analyse dynamique future

Une analyse dynamique sur staging peut être ajoutée avec un outil approprié.

Elle peut couvrir :

- headers ;
- pages publiques ;
- authentification ;
- formulaires ;
- erreurs ;
- redirections ;
- cookies ;
- endpoints exposés.

Elle ne remplace pas les tests applicatifs.

Elle reste optionnelle dans le premier incrément du MVP si elle retarde la génération initiale.

---

# 72. Pentest

Un pentest est recommandé lorsque :

- données sensibles ;
- portail client ;
- paiements ;
- documents confidentiels ;
- exposition importante ;
- obligation contractuelle ;
- projet réglementé ;
- intégrations critiques.

Il n’est pas automatiquement inclus dans les packs standards.

---

# 73. Gestion des vulnérabilités

## 73.1 Critique

Une vulnérabilité critique nécessite :

- triage immédiat ;
- mitigation ou désactivation ;
- analyse d’exposition ;
- correctif prioritaire ;
- information des clients concernés selon le contexte.

Elle bloque une nouvelle mise en production.

---

## 73.2 Haute

Une vulnérabilité haute doit être corrigée ou formellement mitigée avant une mise en production concernée.

---

## 73.3 Moyenne

Une vulnérabilité moyenne doit être :

- analysée ;
- planifiée ;
- suivie ;
- corrigée selon son exploitabilité réelle.

---

## 73.4 Faible

Une vulnérabilité faible peut être placée dans le backlog avec justification.

---

## 73.5 Faux positif

Un faux positif doit être documenté avec :

- règle ;
- emplacement ;
- raison ;
- preuve ;
- date de revue.

---

# 74. Mises à jour de sécurité

## 74.1 Dépendances

Les mises à jour doivent être :

- détectées ;
- revues ;
- testées ;
- déployées de manière contrôlée.

---

## 74.2 Packages SiteFactory

Un correctif commun produit une nouvelle version de package.

Une version publiée n’est pas remplacée silencieusement.

---

## 74.3 Projets clients

Les projets clients ne sont pas mis à jour automatiquement en production.

Le processus est :

```text
upgrade check
→ upgrade plan
→ branche
→ application
→ tests
→ recette
→ déploiement
```

---

# 75. Gestion des incidents

## 75.1 Étapes

```text
Détecter
→ qualifier
→ contenir
→ préserver les éléments utiles
→ corriger
→ restaurer
→ vérifier
→ documenter
→ prévenir la répétition
```

---

## 75.2 Actions possibles

- désactiver un compte ;
- invalider les sessions ;
- activer un kill switch ;
- retirer une clé ;
- bloquer une route ;
- couper une intégration ;
- restaurer un backup ;
- déployer un correctif ;
- limiter le trafic.

---

## 75.3 Secrets compromis

Les secrets concernés sont remplacés.

---

## 75.4 Données personnelles

Un incident impliquant des données personnelles nécessite une analyse et les démarches appropriées au contexte.

---

## 75.5 Post-mortem

Un incident significatif doit produire :

- cause ;
- impact ;
- chronologie ;
- détection ;
- correction ;
- actions préventives ;
- responsabilités.

---

# 76. Exceptions de sécurité

## 76.1 Dérogation

Une dérogation doit contenir :

```text
Identifiant
Contrôle concerné
Projet
Justification
Risque
Mesures compensatoires
Propriétaire
Date de validation
Date d’expiration
Plan de suppression
```

---

## 76.2 Expiration

Une dérogation possède une date d’expiration.

Elle ne doit pas devenir permanente silencieusement.

---

## 76.3 Interdictions

Aucune dérogation ne doit autoriser :

- mot de passe en clair ;
- secret dans Git ;
- base publique sans besoin exceptionnel validé ;
- suppression de toute autorisation ;
- publication automatique de données sensibles ;
- désactivation générale des scans.

---

# 77. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-SECURITY-001` | Secret potentiel détecté |
| `SF-SECURITY-002` | Chemin non autorisé |
| `SF-SECURITY-003` | Configuration de production non sûre |
| `SF-SECURITY-004` | Header de sécurité absent |
| `SF-SECURITY-005` | CSP invalide |
| `SF-SECURITY-006` | Action mutable non protégée |
| `SF-SECURITY-007` | Antiforgery absent |
| `SF-SECURITY-008` | Autorisation absente |
| `SF-SECURITY-009` | Upload interdit |
| `SF-SECURITY-010` | Type de fichier invalide |
| `SF-SECURITY-011` | Taille de fichier dépassée |
| `SF-SECURITY-012` | URL externe non autorisée |
| `SF-SECURITY-013` | Redirection ouverte |
| `SF-SECURITY-014` | Origine CORS non autorisée |
| `SF-SECURITY-015` | Base de données exposée |
| `SF-SECURITY-016` | Compte base trop privilégié |
| `SF-SECURITY-017` | Clés Data Protection non persistées |
| `SF-SECURITY-018` | Dépendance vulnérable |
| `SF-SECURITY-019` | Image Docker non conforme |
| `SF-SECURITY-020` | Donnée sensible dans les logs |
| `SF-SECURITY-021` | Contrôle de production non satisfait |
| `SF-SECURITY-022` | Dérogation requise |
| `SF-SECURITY-023` | Rate limit dépassé |
| `SF-SECURITY-024` | Contenu HTML non sûr |
| `SF-SECURITY-025` | Migration de production automatique détectée |

---

# 78. Rapport de sécurité

Les résultats peuvent être intégrés à :

```text
generation-report.json
```

ou dans :

```text
security-validation-report.json
```

Structure conceptuelle :

```json
{
  "status": "success-with-warnings",
  "profile": "production",
  "checks": [
    {
      "id": "secure-cookies",
      "status": "success"
    },
    {
      "id": "content-security-policy",
      "status": "warning",
      "code": "SF-SECURITY-005",
      "message": "The policy still allows an inline style exception."
    }
  ],
  "summary": {
    "passed": 28,
    "warnings": 1,
    "failed": 0
  }
}
```

Le rapport ne contient aucun secret ou donnée personnelle.

---

# 79. Critères de publication d’un module

Un module ne peut être publié comme `available` que si :

```text
[ ] Menaces identifiées
[ ] Routes documentées
[ ] Permissions documentées
[ ] Entitlements définis
[ ] Validation serveur
[ ] Antiforgery adapté
[ ] Rate limiting évalué
[ ] Données personnelles identifiées
[ ] Logs sobres
[ ] Secrets identifiés
[ ] Uploads sécurisés si présents
[ ] Appels externes sécurisés si présents
[ ] Tests d’autorisation
[ ] Tests d’injection
[ ] Tests de sécurité
[ ] Dépendances analysées
[ ] Documentation opérateur
[ ] Build Release réussi
[ ] Scans réussis
```

---

# 80. Critères d’acceptation du MVP

La baseline sera considérée comme opérationnelle lorsque :

1. HTTPS est obligatoire en production ;
2. HSTS est configuré ;
3. les forwarded headers sont maîtrisés ;
4. les headers de sécurité sont présents ;
5. une CSP adaptée est définie ;
6. les cookies sont sécurisés ;
7. aucune inscription publique n’existe ;
8. le lockout fonctionne ;
9. le logout utilise POST ;
10. l’antiforgery protège les actions mutables ;
11. les routes admin sont protégées ;
12. les permissions sont appliquées côté serveur ;
13. la validation serveur est obligatoire ;
14. les sorties sont encodées ;
15. le HTML brut non fiable est interdit ;
16. les requêtes SQL sont paramétrées ;
17. les commandes externes sont structurées ;
18. les chemins sont protégés ;
19. les uploads sont désactivés par défaut ;
20. les uploads autorisés sont validés ;
21. les formulaires publics sont limités ;
22. le rate limiting existe ;
23. CORS est désactivé par défaut ;
24. les redirections sont validées ;
25. les appels sortants sont contrôlés ;
26. les secrets restent hors Git, logs et images ;
27. le secret scanning est intégré ;
28. PostgreSQL n’est pas public ;
29. le compte PostgreSQL n’est pas superutilisateur ;
30. les erreurs de production sont sobres ;
31. aucune PII n’est logguée ;
32. les packages sont versionnés ;
33. les dépendances sont analysées ;
34. les workflows ont des permissions minimales ;
35. l’image Docker ne contient aucun secret ;
36. l’application s’exécute non root lorsque possible ;
37. Data Protection est persistée ;
38. les backups sont protégés ;
39. les migrations de production sont explicites ;
40. les contrôles de production sont automatisés ;
41. les tests de contournement réussissent ;
42. les tests Windows et Linux réussissent ;
43. le build Release réussit ;
44. les tests réussissent sans warning injustifié.

---

# 81. Règles pour Codex

## 81.1 Lecture préalable

Avant toute modification liée à la sécurité, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../03-generation/13-generation-pipeline.md
../adr/0009-client-customizations-and-extension-points.md
../02-architecture/core-module-boundary.md
09-module-architecture.md
10-feature-flags-and-entitlements.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
13-generation-pipeline.md
14-client-configuration-schema.md
15-cli-specification.md
../adr/0015-deterministic-generation-and-optional-ai.md
Contrat SEO du site : title et description, H1, canonical, robots, sitemap, JSON-LD, slugs, redirections et domaine coherents ; contenu factuel sans promesse de classement
18-security-baseline.md
AGENTS.md
```

Ainsi que :

- module concerné ;
- routes ;
- permissions ;
- données ;
- tests ;
- workflows ;
- Docker ;
- documentation opérateur.

---

## 81.2 Ce que Codex peut faire

Codex peut :

- ajouter une validation ;
- ajouter une policy ;
- protéger un endpoint ;
- ajouter un header ;
- ajouter un test antiforgery ;
- ajouter un rate limiter ;
- corriger un upload ;
- retirer un secret ;
- ajouter un scan ;
- durcir un Dockerfile ;
- ajouter un test de contournement ;
- corriger une requête ;
- améliorer un diagnostic ;
- documenter une procédure.

---

## 81.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- désactiver l’antiforgery ;
- rendre une route admin publique ;
- ajouter `AllowAnyOrigin` avec credentials ;
- ajouter un secret dans le repository ;
- logguer une donnée personnelle ;
- désactiver un scan globalement ;
- utiliser `--force` pour contourner une alerte ;
- ajouter une migration automatique de production ;
- exposer PostgreSQL ;
- exécuter l’application root sans justification ;
- ajouter `unsafe-eval` ;
- ajouter `unsafe-inline` sans décision ;
- accepter tous les types d’upload ;
- stocker un token dans localStorage ;
- concaténer une commande shell ;
- concaténer une requête SQL ;
- ajouter une URL externe libre ;
- rendre un endpoint d’export public ;
- supprimer un test de sécurité pour rendre la CI verte ;
- reclasser une vulnérabilité en faible sans analyse ;
- modifier une politique sensible sans validation.

---

## 81.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouvelle origine CORS ;
- nouvelle intégration externe ;
- nouvelle catégorie de donnée sensible ;
- upload de documents ;
- désactivation d’un header ;
- exception CSP ;
- exposition réseau supplémentaire ;
- chiffrement applicatif ;
- nouveau secret ;
- nouveau fournisseur d’identité ;
- MFA ;
- stockage de token côté navigateur ;
- base exposée ;
- script tiers ;
- analytics ;
- CAPTCHA ;
- migration destructive ;
- vulnérabilité critique ou haute ;
- dérogation de sécurité.

---

## 81.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Menace traitée
Contrôle modifié
Routes affectées
Endpoints affectés
Données affectées
Permissions affectées
Secrets affectés
Headers affectés
CSP affectée
CORS affecté
Rate limiting affecté
Uploads affectés
Dépendances affectées
Docker affecté
Infrastructure affectée
Tests de sécurité ajoutés
Scans exécutés
Build
Tests
Résultats restants
Risques résiduels
Décisions humaines restantes
```

Codex ne doit pas déclarer une correction de sécurité réussie sans avoir testé le scénario de contournement concerné.

---

# 82. Anti-patterns interdits

## 82.1 Protection uniquement visuelle

Un bouton est masqué mais l’endpoint reste accessible.

---

## 82.2 Validation uniquement navigateur

Le serveur accepte directement les données reçues.

---

## 82.3 CORS comme autorisation

Une origine refusée est considérée comme la seule protection d’un endpoint.

---

## 82.4 Secret dans `appsettings.json`

Un mot de passe SMTP réel est écrit en clair dans `appsettings.json` au lieu
d'être résolu par une référence de secret. Cet anti-pattern reste interdit ;
aucune valeur de credential n'est reproduite dans l'exemple documentaire.

---

## 82.5 Secret dans Docker

```dockerfile
ENV DATABASE_PASSWORD=<POSTGRES_PASSWORD>
```

---

## 82.6 PostgreSQL public

```yaml
ports:
  - "5432:5432"
```

dans le profil de production standard.

---

## 82.7 Migration automatique

```csharp
await dbContext.Database.MigrateAsync();
```

au démarrage de production.

---

## 82.8 HTML brut non filtré

Le contenu administrable est rendu comme HTML sans sanitisation.

---

## 82.9 Upload dans `wwwroot`

Le fichier utilisateur est stocké sous son nom original et servi directement.

---

## 82.10 Commande shell concaténée

```csharp
$"convert {fileName}"
```

---

## 82.11 Requête SQL concaténée

```csharp
$"SELECT * FROM users WHERE email = '{email}'"
```

---

## 82.12 Logs de formulaires

```text
Lead received from john@example.com: ...
```

---

## 82.13 `AllowAnyOrigin` avec credentials

Configuration CORS permissive en production.

---

## 82.14 Token dans localStorage

Un token d’administration est conservé dans le navigateur alors que l’architecture utilise des cookies sécurisés.

---

## 82.15 `unsafe-inline` ajouté sans analyse

La CSP est affaiblie pour éviter de corriger une ressource.

---

## 82.16 Backup dans le repository

Un dump PostgreSQL est committé pour faciliter un test.

---

## 82.17 Compte administrateur partagé

Toute l’équipe utilise le même compte et le même mot de passe.

---

## 82.18 Scan ignoré silencieusement

Une règle CI utilise :

```text
continue-on-error
```

pour une analyse bloquante sans justification.

---

# 83. Roadmap d’implémentation

## Phase sécurité 1 — Socle applicatif

```text
HTTPS
forwarded headers
HSTS
errors
headers
cookies
Data Protection
```

---

## Phase sécurité 2 — Identity

```text
login
lockout
logout POST
antiforgery
permissions
AdminSeed
```

---

## Phase sécurité 3 — Entrées

```text
validation serveur
encodage
rate limiting
formulaires
redirections
```

---

## Phase sécurité 4 — Données

```text
PostgreSQL privé
compte dédié
migrations
rétention
logs
backups
```

---

## Phase sécurité 5 — Fichiers

```text
médias
uploads
stockage
types
tailles
exports
```

---

## Phase sécurité 6 — Supply chain

```text
secret scanning
dependency scanning
analyse statique
actions permissions
packages exacts
```

---

## Phase sécurité 7 — Docker et VPS

```text
non-root
réseau
volumes
firewall
SSH
patching
health
```

---

## Phase sécurité 8 — Contrôles avancés

```text
CSP renforcée
DAST
container scan
MFA
audit renforcé
pentest selon projet
```

---

# 84. Décisions validées

La baseline de sécurité est la suivante :

```text
La sécurité est activée par défaut.

La sécurité fondamentale n’est pas une option commerciale.

Le système applique un refus par défaut.

Les contrôles serveur sont obligatoires.

La validation navigateur ne suffit pas.

HTTPS est obligatoire en production.

Caddy assure la terminaison TLS dans le profil VPS.

PostgreSQL n’est pas exposé publiquement.

L’application utilise un compte PostgreSQL non superutilisateur.

Les headers de sécurité sont validés.

Une CSP adaptée est fournie.

Les cookies d’authentification sont HttpOnly et Secure.

Aucune inscription publique n’est disponible dans le MVP.

Le lockout est activé.

La déconnexion utilise POST avec antiforgery.

Les actions mutables sont protégées contre le CSRF.

Les sorties non fiables sont encodées.

Le HTML brut est refusé ou nettoyé explicitement.

Les requêtes SQL sont paramétrées.

Les commandes externes utilisent des arguments structurés.

Les chemins sont normalisés et limités au dossier autorisé.

Les uploads sont désactivés par défaut.

Les fichiers acceptés sont strictement validés.

CORS est désactivé sans besoin explicite.

Les redirections sont limitées aux destinations autorisées.

Les appels HTTP sortants utilisent des endpoints approuvés.

Les secrets restent hors Git, logs, rapports et images Docker.

Le secret scanning est obligatoire.

Les dépendances sont versionnées et analysées.

Les workflows CI possèdent des permissions minimales.

L’application s’exécute non root lorsque possible.

Les clés Data Protection sont persistées et isolées par client.

Les migrations de production ne sont jamais automatiques.

Les backups sont protégés et restaurables.

Les logs ne contiennent pas de données personnelles.

Les contrôles de production sont exécutés avant déploiement.

Une vulnérabilité critique ou haute bloque une nouvelle
mise en production sans mitigation formelle.

Toute dérogation est documentée et temporaire.

Codex ne peut pas affaiblir seul un contrôle de sécurité.
```

---

# 85. Checklist de revue applicative

## Authentification

```text
[ ] Inscription publique absente
[ ] Login protégé
[ ] Lockout
[ ] Cookies sécurisés
[ ] Logout POST
[ ] Antiforgery
[ ] AdminSeed désactivé
[ ] Data Protection persistée
```

## Autorisation

```text
[ ] Permissions centralisées
[ ] Routes protégées
[ ] Endpoints protégés
[ ] Services protégés
[ ] Feature et entitlement vérifiés
[ ] Dernier administrateur protégé
```

## Entrées

```text
[ ] Validation serveur
[ ] Longueurs
[ ] Listes autorisées
[ ] Encodage
[ ] Pas de mass assignment
[ ] Redirections locales
[ ] Rate limiting
```

## Données

```text
[ ] PostgreSQL privé
[ ] Compte dédié
[ ] Requêtes paramétrées
[ ] Rétention
[ ] Logs sans PII
[ ] Backups
[ ] Migrations explicites
```

## Fichiers

```text
[ ] Upload nécessaire
[ ] Taille limitée
[ ] Extensions autorisées
[ ] Signature vérifiée
[ ] Nom généré
[ ] Hors web root
[ ] Pas d’exécution
[ ] Accès autorisé
```

---

# 86. Checklist de revue infrastructure

```text
[ ] HTTPS
[ ] Certificat valide
[ ] HSTS
[ ] Firewall
[ ] Ports minimaux
[ ] SSH par clé
[ ] Root login désactivé
[ ] PostgreSQL non public
[ ] Docker maintenu
[ ] Application non root
[ ] Volumes isolés
[ ] Secrets hors image
[ ] Backups hors Git
[ ] Espace disque surveillé
[ ] Logs maîtrisés
[ ] Health check
```

---

# 87. Checklist de revue CI/CD

```text
[ ] Restore
[ ] Build Release
[ ] Tests
[ ] Secret scan
[ ] Dependency scan
[ ] Analyse statique
[ ] Permissions minimales
[ ] Actions approuvées
[ ] Aucun secret sur PR non fiable
[ ] Aucun artefact sensible
[ ] Image sans secret
[ ] Échec bloquant non masqué
```

---

# 88. Checklist avant production

```text
[ ] Configuration production
[ ] HTTPS
[ ] Headers
[ ] CSP
[ ] Cookies
[ ] Identity
[ ] AdminSeed désactivé
[ ] Secrets installés
[ ] PostgreSQL privé
[ ] Data Protection persistée
[ ] Backup réalisé
[ ] Restore documenté
[ ] Migration inspectée
[ ] Firewall
[ ] Health check
[ ] Rate limiting
[ ] Logs sobres
[ ] Aucun placeholder
[ ] Aucun domaine de test
[ ] Build réussi
[ ] Tests réussis
[ ] Secret scan réussi
[ ] Dependency scan réussi
[ ] Analyse statique réussie
[ ] Rollback disponible
```

---

# 89. Checklist après déploiement

```text
[ ] HTTP redirige vers HTTPS
[ ] Certificat valide
[ ] Headers présents
[ ] CSP fonctionnelle
[ ] Accueil accessible
[ ] Admin inaccessible anonymement
[ ] Login fonctionne
[ ] Logout fonctionne
[ ] Formulaire fonctionne
[ ] Rate limiting fonctionne
[ ] Health check sain
[ ] PostgreSQL non accessible publiquement
[ ] Logs sans erreur critique
[ ] Aucun secret dans les logs
[ ] Backup de production vérifié
[ ] Smoke tests réussis
```

---

# 90. Conclusion

La sécurité SiteFactory doit être reproductible, testable et intégrée à l’ensemble du cycle de vie du projet.

Elle ne se limite pas à l’authentification.

Elle couvre :

```text
conception
→ configuration
→ code
→ données
→ dépendances
→ génération
→ CI
→ conteneurs
→ infrastructure
→ exploitation
→ maintenance
→ incidents
```

> SiteFactory ne doit pas seulement générer une application fonctionnelle. Il doit générer une application dont les protections essentielles sont déjà présentes, vérifiables et difficiles à désactiver accidentellement.
