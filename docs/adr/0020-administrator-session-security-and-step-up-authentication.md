---
adrId: ADR-0020
title: Secure administrator sessions with server-side revocation, MFA and step-up authentication
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
  - ./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ./0028-database-transaction-boundaries-and-concurrency-control.md
  - ./0021-administrator-provisioning-invitations-and-account-recovery.md
  - ../03-generation/13-generation-pipeline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../04-quality-security/18-security-baseline.md
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
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0017-background-jobs-and-reliable-side-effects.md
  - ./0018-public-form-abuse-protection-and-rate-limiting.md
  - ./0019-audit-trail-and-sensitive-administrative-actions.md
targetVersion: 0.1.0
---

# ADR-0020 — Sécurité des sessions administrateur et authentification renforcée

## Portée publique et état d'implémentation

Le statut `accepted` et la date ci-dessous décrivent la décision architecturale
historique. La distribution publique ne fournit pas d'interface administrateur,
d'ASP.NET Core Identity, de registre de sessions administrateur ou de parcours
MFA/step-up livrés. Les flux, types et durées de ce document sont la baseline
prescrite pour les applications et modules qui implémenteront une administration.

Les décisions de sécurité, interdictions, alternatives, conséquences et preuves
attendues sont conservées. Les adaptateurs Hosting et les endpoints de santé
génériques ne constituent pas une implémentation d'Identity ou de cette baseline.
Les responsabilités d'un opérateur restent distinctes des dépendances du build public.

## 1. Statut

```text
accepted
```

La décision prescrit la protection des interfaces administratives avec :

```text
ASP.NET Core Identity
+ authentification par cookie sécurisé
+ registre de sessions côté serveur
+ expiration inactive et absolue
+ révocation explicite
+ authentification multifacteur
+ authentification renforcée avant les actions sensibles
+ limitation des tentatives
+ audit des opérations de sécurité
```

La baseline n’utilise pas de JWT stocké dans le navigateur pour l’administration.

Les éléments suivants sont interdits :

- token d’accès dans `localStorage` ;
- token d’accès dans `sessionStorage` ;
- cookie d’authentification lisible par JavaScript ;
- cookie sans attribut `Secure` en production ;
- cookie partagé volontairement entre plusieurs clients ;
- session sans expiration absolue ;
- option « Se souvenir de moi » activée par défaut ;
- action critique exécutée avec une authentification ancienne ;
- contournement de l’authentification multifacteur par un rôle administrateur ;
- autorisation basée uniquement sur les claims d’un cookie ancien ;
- redirection externe non validée après connexion ;
- mot de passe, code MFA ou recovery code dans les logs.

---

## 2. Résumé

Le flux d’authentification de référence est :

```text
requête de connexion
→ rate limiting
→ validation antiforgery
→ validation générique des credentials
→ vérification du verrouillage
→ vérification du mot de passe
→ challenge MFA si requis
→ création d’une session serveur
→ émission d’un cookie sécurisé
→ audit de la connexion
```

Le flux d’une action critique est :

```text
requête administrative
→ session valide
→ session non révoquée
→ permission actuelle
→ entitlement et capability
→ niveau d’authentification requis
→ authentification récente
→ MFA récent si requis
→ confirmation de l’action
→ antiforgery
→ transaction métier et audit
```

Les références temporelles initiales sont :

```text
Expiration par inactivité :
30 minutes

Expiration absolue :
8 heures

Fenêtre d’authentification renforcée critique :
10 minutes

Nombre maximal initial de sessions actives :
3 par compte administrateur

Verrouillage initial :
5 échecs sur une période courte
→ 15 minutes de verrouillage
```

Ces valeurs sont configurables dans les limites autorisées par le profil de sécurité.

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Le cookie ne constitue pas à lui seul la source de vérité de la session.

Une session révoquée ne peut plus effectuer d’action administrative.

Une réduction de privilèges invalide les sessions concernées.

Un changement de mot de passe révoque les sessions existantes.

Une réinitialisation MFA révoque les sessions existantes.

Une restauration de base révoque toutes les sessions administratives.

Une expiration absolue ne peut pas être prolongée par sliding expiration.

Une session inactive expire.

Une action critique exige une authentification récente.

Un rôle administrateur ne contourne pas le MFA.

Une confirmation UI ne remplace pas l’autorisation serveur.

Une route GET ne modifie jamais un état sensible.

Les requêtes administratives mutables utilisent l’antiforgery.

Les credentials et facteurs d’authentification ne sont jamais journalisés.

La réponse de connexion ne révèle pas si le compte existe.

Une session staging n’est jamais valide en production.

Une session d’un client n’est jamais valide pour un autre client.
```

---

## 4. Contexte

L'administration d'une application consommatrice pourra notamment permettre :

- de consulter les leads ;
- de modifier leurs statuts ;
- de publier des contenus ;
- de gérer des médias ;
- de créer des comptes ;
- d’attribuer des rôles ;
- de consulter des documents privés ;
- de générer des exports ;
- de relancer des jobs ;
- d’activer des feature flags ;
- d’activer des kill switches ;
- de consulter la piste d’audit ;
- d’effectuer des opérations sensibles.

Une session administrateur compromise peut permettre :

- l’accès aux données personnelles ;
- l’export de données ;
- la modification du contenu public ;
- l’ajout d’un autre administrateur ;
- la modification de permissions ;
- la suppression de données ;
- l’activation ou la désactivation de contrôles opérationnels ;
- la préparation d’une persistance malveillante.

Une authentification réussie une fois ne doit donc pas donner un accès indéfini à toutes les actions sensibles.

Une session uniquement représentée par un cookie chiffré est difficile à révoquer immédiatement sans :

- modifier le Security Stamp ;
- changer les clés Data Protection ;
- attendre l’expiration du cookie ;
- vérifier un état serveur.

L'architecture retenue prévoit par conséquent une session côté serveur afin de permettre :

- révocation ciblée ;
- liste des sessions ;
- expiration absolue ;
- contrôle du niveau d’authentification ;
- invalidation après un changement de sécurité ;
- invalidation après restauration.

---

## 5. Problème à résoudre

Comment sécuriser les sessions administrateur tout en garantissant :

- une authentification utilisable ;
- une révocation rapide ;
- une expiration maîtrisée ;
- une protection contre le vol de cookie ;
- une protection contre la fixation de session ;
- une authentification multifacteur ;
- une réauthentification pour les actions critiques ;
- une gestion correcte des rôles modifiés ;
- une restauration ne réactivant pas d’anciennes sessions ;
- une expérience compatible avec Blazor et ASP.NET Core ?

La solution doit également éviter :

- les sessions permanentes ;
- les tokens côté JavaScript ;
- les permissions obsolètes dans un cookie ;
- les erreurs de login révélant les comptes ;
- les lockouts facilement exploitables comme déni de service ;
- les codes MFA non limités ;
- la réutilisation d’un challenge de step-up ;
- la confiance excessive dans l’adresse IP ;
- la dépendance obligatoire à un fournisseur d’identité externe.

---

## 6. Portée

Cette décision couvre :

- authentification administrateur ;
- cookies ;
- registre de sessions ;
- expiration ;
- sliding expiration ;
- Security Stamp ;
- révocation ;
- connexion ;
- déconnexion ;
- verrouillage ;
- mots de passe ;
- MFA ;
- TOTP ;
- recovery codes ;
- authentification renforcée ;
- confirmation d’actions critiques ;
- antiforgery ;
- rate limiting ;
- redirections ;
- audit ;
- restauration ;
- environnements ;
- sessions multiples ;
- interface de gestion des sessions ;
- tests ;
- procédures d’incident.

---

## 7. Hors portée

Cette décision ne définit pas :

- un fournisseur d’identité externe obligatoire ;
- l’authentification client des visiteurs ;
- OAuth pour une API publique ;
- une fédération d’identité d’entreprise ;
- les passkeys comme baseline obligatoire ;
- une PKI client ;
- une authentification mutuelle TLS ;
- un SSO multi-tenant ;
- l’intégralité du processus d’invitation ;
- l’intégralité du processus de récupération de compte ;
- un centre de sécurité utilisateur complet ;
- un système de détection comportementale par AI ;
- une preuve d’identité légale.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Protection des données administratives**
2. **Révocation immédiate ou bornée**
3. **MFA pour les comptes privilégiés**
4. **Authentification renforcée avant les actions critiques**
5. **Expiration absolue**
6. **Protection contre les sessions restaurées**
7. **Résistance au vol de cookie**
8. **Absence de token dans le stockage JavaScript**
9. **Permissions actuelles**
10. **Protection contre la fixation de session**
11. **Prévention de l’énumération de comptes**
12. **Traçabilité**
13. **Compatibilité ASP.NET Core Identity**
14. **Autonomie client**
15. **Accessibilité**
16. **Testabilité**

---

## 9. Contraintes

La décision doit respecter :

```text
ASP.NET Core Identity

Cookie HttpOnly

Cookie Secure en production

Antiforgery

Data Protection persistée

PostgreSQL

Une base par client et environnement

MFA obligatoire pour les comptes privilégiés en production

Registre serveur des sessions

Expiration inactive

Expiration absolue

Révocation

Audit

Aucun token navigateur dans localStorage

Aucun secret dans les événements de sécurité

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- l’administration est utilisée par un nombre limité de comptes ;
- le profil initial possède une seule instance web ;
- PostgreSQL est disponible pendant les opérations administratives ;
- un court accès à PostgreSQL pour valider la session est acceptable ;
- les comptes administrateur peuvent utiliser une application TOTP ;
- les passkeys pourront être ajoutées ultérieurement ;
- les emails et SMS ne constituent pas des facteurs suffisamment forts pour la baseline MFA ;
- le projet client reste autonome sans Identity Provider LogicGate distant.

---

# 11. Définitions

## 11.1 Session administrateur

État serveur représentant une authentification administrative active.

---

## 11.2 Cookie d’authentification

Cookie protégé contenant les informations minimales nécessaires pour retrouver et valider une session.

---

## 11.3 Session ID

Identifiant opaque unique d’une session.

---

## 11.4 Session Epoch

Valeur propre à un environnement permettant d’invalider globalement les sessions antérieures.

---

## 11.5 Idle Timeout

Durée maximale sans activité administrative reconnue.

---

## 11.6 Absolute Timeout

Durée maximale de la session, indépendamment de son activité.

---

## 11.7 Sliding Expiration

Renouvellement borné de l’expiration inactive lorsque la session est utilisée.

---

## 11.8 Authentification primaire

Vérification initiale du mot de passe ou d’un futur facteur principal équivalent.

---

## 11.9 MFA

Vérification d’un facteur supplémentaire indépendant du mot de passe.

---

## 11.10 Step-up authentication

Nouvelle vérification de l’identité avant une action plus sensible que les opérations ordinaires de la session.

---

## 11.11 Authentication freshness

Âge de la dernière vérification primaire ou MFA.

---

## 11.12 Security Stamp

Valeur Identity modifiée lorsque l’état de sécurité du compte change.

---

## 11.13 Session revocation

Invalidation serveur d’une session existante.

---

# 12. Option A — Cookie ASP.NET Core sans registre serveur

## 12.1 Description

Le cookie protégé contient l’identité et les claims.

L’application n’enregistre pas les sessions actives.

---

## 12.2 Avantages

- architecture simple ;
- faible accès PostgreSQL ;
- comportement standard ;
- bonne scalabilité ;
- faible volume de données.

---

## 12.3 Inconvénients

- révocation ciblée difficile ;
- sessions actives non inventoriables ;
- expiration absolue plus difficile à contrôler ;
- rôle potentiellement obsolète ;
- restauration pouvant réactiver des cookies ;
- déconnexion de tous les appareils moins précise.

---

## 12.4 Risques

- cookie volé utilisable jusqu’à expiration ;
- permission retirée mais claim encore présent ;
- session ancienne restant valide ;
- impossibilité de révoquer un seul appareil.

---

## 12.5 Réversibilité

```text
élevée
```

mais les sessions déjà émises restent difficiles à gérer pendant la transition.

---

# 13. Option B — JWT stocké dans `localStorage`

## 13.1 Description

L’administration utilise un token JWT accessible au code JavaScript.

---

## 13.2 Avantages

- compatible avec les APIs ;
- indépendant des cookies ;
- transport simple ;
- architecture courante pour les SPA.

---

## 13.3 Inconvénients

- accessible en cas de XSS ;
- révocation difficile ;
- stockage navigateur ;
- refresh tokens ;
- rotation ;
- complexité inutile pour Blazor Server ;
- antiforgery remplacé par d’autres contraintes.

---

## 13.4 Risques

- extraction du token ;
- persistance après fermeture ;
- exposition par extension navigateur ;
- refresh token mal protégé ;
- claims obsolètes.

---

## 13.5 Réversibilité

```text
moyenne
```

---

# 14. Option C — Identity Provider externe obligatoire

## 14.1 Description

Tous les projets utilisent un fournisseur externe d’identité et MFA.

---

## 14.2 Avantages

- fonctionnalités de sécurité avancées ;
- MFA ;
- politiques ;
- journaux ;
- SSO ;
- récupération gérée.

---

## 14.3 Inconvénients

- dépendance runtime ;
- coût ;
- configuration par client ;
- transfert ;
- verrouillage fournisseur ;
- disponibilité ;
- complexité disproportionnée pour certains projets.

---

## 14.4 Risques

- projet inaccessible après perte du compte fournisseur ;
- mauvaise configuration du tenant ;
- relation client transférée difficilement ;
- panne externe bloquant l’administration.

---

## 14.5 Réversibilité

```text
faible à moyenne
```

---

# 15. Option D — Cookie sécurisé, registre serveur, MFA et step-up

## 15.1 Description

ASP.NET Core Identity émet un cookie protégé.

Le cookie référence une session persistée dans PostgreSQL.

Les actions sensibles exigent une authentification récente.

---

## 15.2 Avantages

- révocation ciblée ;
- sessions listables ;
- expiration absolue ;
- claims minimaux ;
- contrôle des privilèges actuels ;
- MFA ;
- step-up ;
- restauration sécurisée ;
- autonomie du projet ;
- compatibilité avec Blazor.

---

## 15.3 Inconvénients

- table de sessions ;
- validation supplémentaire ;
- migrations ;
- UI de gestion ;
- règles de fraîcheur ;
- davantage de tests ;
- administration temporairement limitée si PostgreSQL est indisponible.

---

## 15.4 Risques

- cache de session obsolète ;
- contention limitée ;
- sessions non nettoyées ;
- mauvaise rotation de Session Epoch ;
- expérience trop restrictive.

---

## 15.5 Réversibilité

```text
élevée
```

Les mécanismes restent compatibles avec ASP.NET Core Identity et un IdP futur.

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Cookie seul | JWT localStorage | IdP obligatoire | Cookie + registre |
|---|---:|---:|---:|---:|---:|
| Révocation | 5 | 2 | 1 | 5 | 5 |
| Protection XSS | 5 | 5 | 1 | 4 | 5 |
| MFA | 5 | 3 | 3 | 5 | 5 |
| Step-up | 5 | 2 | 3 | 5 | 5 |
| Autonomie client | 5 | 5 | 5 | 1 | 5 |
| Compatibilité Blazor | 5 | 5 | 2 | 4 | 5 |
| Simplicité | 3 | 5 | 3 | 2 | 3 |
| Gestion des sessions | 5 | 1 | 2 | 5 | 5 |
| Restauration sécurisée | 5 | 2 | 2 | 4 | 5 |
| Transfert client | 4 | 5 | 4 | 2 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’utiliser :

```text
ASP.NET Core Identity
+ cookie sécurisé
+ session PostgreSQL
+ MFA
+ step-up authentication
```

pour l’administration.

---

## 17.1 Navigateur

Le navigateur reçoit uniquement un cookie HttpOnly protégé.

---

## 17.2 Serveur

Le serveur conserve l’état de session et décide si celle-ci reste valide.

---

## 17.3 Claims minimaux

Le cookie ne doit pas conserver une copie complète et durable de toutes les permissions.

---

## 17.4 Autorisation actuelle

Les actions sensibles doivent utiliser :

- les permissions actuelles ;
- le Security Stamp actuel ;
- l’état serveur de la session ;
- le niveau d’authentification requis.

---

# 18. Cookie d’authentification

La baseline utilise un cookie dédié à l’administration.

Nom conceptuel :

```text
__Host-SiteFactory-Admin
```

---

## 18.1 Préfixe `__Host-`

Le cookie utilise :

```text
Secure = true
Path = /
Domain non configuré
```

---

## 18.2 HttpOnly

```text
HttpOnly = true
```

Le cookie n’est pas accessible par JavaScript.

---

## 18.3 SameSite

Baseline :

```text
SameSite = Lax
```

Cette valeur protège les requêtes cross-site ordinaires tout en restant compatible avec les navigations et certains parcours d’authentification.

Un profil sans authentification externe peut utiliser :

```text
SameSite = Strict
```

après tests.

---

## 18.4 Secure

En production :

```text
Secure = Always
```

---

## 18.5 Taille

Le contenu du cookie doit rester borné.

Il ne contient pas :

- liste complète de permissions ;
- secrets ;
- recovery codes ;
- token MFA ;
- données métier.

---

# 19. Contenu minimal du cookie

Le cookie peut contenir conceptuellement :

```text
UserId

SessionId

SessionEpoch

IssuedAtUtc

AuthenticationMethod

SecurityStampVersion
```

---

## 19.1 Rôles

Les rôles ou permissions présents comme claims ne doivent pas être considérés comme fiables indéfiniment.

---

## 19.2 Session ID

Le Session ID permet de retrouver l’état serveur.

---

## 19.3 Epoch

Une différence de Session Epoch invalide la session.

---

# 20. Registre de sessions

Le registre est stocké dans PostgreSQL.

Schéma logique conceptuel :

```text
sitefactory_identity.admin_sessions
sitefactory_identity.session_epochs
```

---

## 20.1 Responsabilité

Le registre permet :

- validation ;
- révocation ;
- expiration ;
- listing ;
- limite de concurrence ;
- authentification récente ;
- audit ;
- invalidation globale.

---

## 20.2 Accès

Les modules métier n’accèdent pas directement aux tables.

---

# 21. Modèle de session

Une session doit pouvoir conserver :

```text
AdminSessionId

UserId

SessionEpoch

SecurityStampVersion

CreatedAtUtc

LastSeenAtUtc

IdleExpiresAtUtc

AbsoluteExpiresAtUtc

LastPrimaryAuthenticationAtUtc

LastMfaAuthenticationAtUtc

AuthenticationMethod

MfaMethod

Status

RevokedAtUtc

RevocationReasonCode

UserAgentCategory

SourceFingerprint

CreatedByAuthenticationEventId
```

---

## 21.1 Modèle physique

Le modèle exact pourra évoluer.

Les garanties de cette section restent obligatoires.

---

# 22. État de session

Valeurs recommandées :

```text
active

revoked

expired

superseded

locked
```

---

## 22.1 `active`

La session peut être utilisée si les autres validations réussissent.

---

## 22.2 `revoked`

Une révocation explicite a eu lieu.

---

## 22.3 `expired`

La limite inactive ou absolue a été dépassée.

---

## 22.4 `superseded`

La session a été remplacée après :

- rotation ;
- élévation ;
- nouveau login ;
- nouvelle Session Epoch.

---

# 23. Session Epoch

Chaque environnement possède une valeur :

```text
SessionEpoch
```

---

## 23.1 Usage

Le cookie et la session doivent référencer l’epoch courant.

---

## 23.2 Incrément

Un nouvel epoch peut être créé après :

- restauration ;
- compromission ;
- changement majeur d’authentification ;
- rotation exceptionnelle de sécurité ;
- perte de maîtrise des sessions.

---

## 23.3 Effet

Toutes les sessions des epochs précédents sont refusées.

---

## 23.4 Audit

Le changement d’epoch est une action critique auditée.

---

# 24. Expiration inactive

La baseline initiale est :

```text
30 minutes
```

---

## 24.1 Activité reconnue

Une activité administrative authentifiée peut renouveler l’échéance inactive.

---

## 24.2 Requêtes ignorées

Ne doivent pas prolonger artificiellement la session :

- health checks ;
- polling anonyme ;
- assets statiques ;
- requêtes échouées ;
- trafic public ;
- appels d’observabilité.

---

## 24.3 Renouvellement borné

L’écriture de `LastSeenAtUtc` peut être limitée à une fréquence raisonnable afin d’éviter une écriture PostgreSQL à chaque requête.

---

# 25. Expiration absolue

La baseline initiale est :

```text
8 heures
```

---

## 25.1 Invariant

La sliding expiration ne prolonge jamais cette limite.

---

## 25.2 Expiration

Une session ayant atteint la limite absolue exige une nouvelle connexion complète.

---

## 25.3 Configuration

Le profil de production ne peut pas désactiver l’expiration absolue.

---

# 26. Sliding expiration

La sliding expiration est autorisée pour la limite inactive.

---

## 26.1 Renouvellement

Le cookie peut être renouvelé lorsque :

- la session est active ;
- le Security Stamp reste valide ;
- l’epoch est valide ;
- l’expiration absolue n’est pas atteinte.

---

## 26.2 Cookie permanent

Le renouvellement ne transforme pas la session en cookie permanent.

---

# 27. Option « Se souvenir de moi »

L’option est désactivée dans la baseline MVP.

---

## 27.1 Raisons

- poste partagé ;
- cookie longue durée ;
- risque de vol ;
- administration peu fréquente ;
- complexité de révocation.

---

## 27.2 Introduction future

Une session persistante nécessitera :

- appareil approuvé ;
- MFA ;
- durée absolue spécifique ;
- révocation ;
- indication explicite ;
- nouvelle analyse de sécurité.

---

# 28. Validation de session

Une requête administrative valide :

```text
cookie protégé

Session ID existant

session active

User ID cohérent

Session Epoch courant

Security Stamp courant

Idle Timeout non dépassé

Absolute Timeout non dépassé

compte actif

permissions nécessaires
```

---

## 28.1 Cache

Une validation de session peut être mise en cache localement pendant une courte durée.

---

## 28.2 Durée maximale

La durée du cache doit rester faible.

Baseline recommandée :

```text
60 secondes maximum
```

---

## 28.3 Actions critiques

Une action critique contourne le cache et vérifie directement PostgreSQL.

---

## 28.4 Révocation urgente

Un changement global de Session Epoch ou un mécanisme d’invalidation locale doit permettre de réduire davantage la fenêtre de révocation.

---

# 29. PostgreSQL indisponible

Lorsque PostgreSQL est indisponible :

```text
les actions administratives authentifiées échouent de manière fermée
```

---

## 29.1 Site public

Le comportement du site public dépend de ses propres besoins et caches.

Il ne doit pas devenir automatiquement indisponible uniquement à cause du registre de sessions.

---

## 29.2 Message

L’administrateur reçoit une erreur générique de disponibilité.

---

# 30. Création de session

Une session est créée uniquement après :

- mot de passe validé ;
- MFA validé si requis ;
- compte actif ;
- rôle autorisé ;
- absence de verrouillage ;
- Session Epoch courant.

---

## 30.1 Nouvel identifiant

Un nouvel Admin Session ID est généré à chaque connexion.

---

## 30.2 Fixation de session

Un identifiant ou cookie existant avant login n’est pas réutilisé comme session authentifiée.

---

## 30.3 Rotation

Une nouvelle identité de session est également générée après certaines transitions sensibles.

---

# 31. Fixation de session

Les transitions suivantes doivent renouveler le cookie et l’identité de session lorsque pertinent :

- connexion ;
- fin du challenge MFA ;
- élévation d’authentification ;
- changement de mot de passe ;
- modification importante du niveau de privilège.

---

## 31.1 Données anciennes

L’ancien cookie est invalidé.

---

# 32. Nombre de sessions actives

La baseline initiale autorise :

```text
3 sessions actives par compte
```

---

## 32.1 Dépassement

Une nouvelle connexion au-delà de la limite ne doit pas révoquer silencieusement une session existante.

L’utilisateur doit :

- consulter ses sessions ;
- en révoquer une ;
- ou utiliser un workflow explicite.

---

## 32.2 Compte break-glass

Un compte break-glass utilise une limite plus stricte.

---

# 33. Gestion des sessions

Un administrateur authentifié peut consulter ses propres sessions.

Informations autorisées :

```text
Session ID tronqué

Date de création

Dernière activité

Expiration

Catégorie d’appareil

Méthode d’authentification

Session actuelle

Statut
```

---

## 33.1 Informations réseau

L’adresse IP brute n’est pas affichée par défaut.

Une information approximative ou pseudonymisée peut être fournie si elle est nécessaire.

---

## 33.2 Révocation

L’utilisateur peut :

- révoquer une session sélectionnée ;
- révoquer toutes les autres sessions ;
- révoquer toutes les sessions.

---

## 33.3 Audit

La révocation est auditée.

---

# 34. Déconnexion

Une déconnexion :

1. révoque la session serveur ;
2. supprime le cookie ;
3. produit l’événement d’audit approprié ;
4. invalide les grants de step-up associés.

---

## 34.1 Déconnexion incomplète

Supprimer uniquement le cookie navigateur ne suffit pas.

---

## 34.2 Requête GET

La déconnexion modifiant l’état ne doit pas être déclenchée par un simple lien GET sans protection.

---

# 35. Révocation de toutes les sessions

Les événements suivants révoquent par défaut toutes les sessions de l’utilisateur :

- changement de mot de passe ;
- réinitialisation de mot de passe ;
- réinitialisation MFA ;
- désactivation du compte ;
- compromission suspectée ;
- modification d’un rôle privilégié ;
- retrait d’un privilège critique ;
- changement de Security Stamp.

---

## 35.1 Session courante

Après changement volontaire du mot de passe, la baseline exige une nouvelle connexion complète.

---

# 36. Security Stamp

Le Security Stamp est modifié lors des changements de sécurité du compte.

---

## 36.1 Exemples

- mot de passe ;
- MFA ;
- recovery codes ;
- rôle privilégié ;
- désactivation ;
- récupération de compte.

---

## 36.2 Validation

La valeur courante est vérifiée pendant la validation de session.

---

## 36.3 Claim ancien

Un cookie portant une ancienne version est rejeté.

---

# 37. Permissions et rôles

Les permissions ne doivent pas rester valides uniquement parce qu’elles étaient présentes au moment de la connexion.

---

## 37.1 Réduction

Une réduction de permission doit prendre effet rapidement.

---

## 37.2 Élévation

Une élévation de permission ne doit pas automatiquement élever une session existante sans :

- nouvelle authentification ;
- MFA ;
- renouvellement de session ;
- audit.

---

## 37.3 Cache

Les caches d’autorisation doivent être invalidés lors d’un changement de rôle.

---

# 38. Mot de passe administrateur

La baseline de mot de passe est :

```text
Longueur minimale :
14 caractères

Longueur maximale acceptée :
au moins 128 caractères

Espaces :
autorisés

Unicode :
autorisé

Gestionnaire de mots de passe :
supporté

Rotation périodique arbitraire :
non requise
```

---

## 38.1 Complexité

La baseline privilégie la longueur et l’unicité plutôt que des règles artificielles imposant systématiquement :

- majuscule ;
- minuscule ;
- chiffre ;
- symbole.

---

## 38.2 Mot de passe commun

Les mots de passe communs ou manifestement compromis doivent être refusés à partir d’une liste locale ou d’un mécanisme approuvé.

---

## 38.3 Données personnelles

Le mot de passe ne doit pas être égal ou trop proche :

- de l’email ;
- du nom de compte ;
- du nom du client ;
- de valeurs par défaut connues.

---

## 38.4 Hashing

ASP.NET Core Identity utilise son Password Hasher supporté.

La plateforme doit permettre le rehash lorsqu’une configuration plus forte devient disponible.

---

# 39. Mot de passe par défaut

Aucun projet de production ne doit posséder :

- compte administrateur avec mot de passe par défaut ;
- credential communiqué dans le repository ;
- mot de passe présent dans `.env.example` ;
- credential partagé entre clients.

---

## 39.1 Bootstrap

Le provisionnement initial suit un processus d’invitation ou de création sécurisé.

Il sera détaillé dans l’ADR suivant.

---

# 40. Réponses de connexion

Une connexion échouée retourne un message générique.

Exemple :

```text
Identifiants invalides.
```

---

## 40.1 Énumération

Le message ne doit pas différencier :

- utilisateur inconnu ;
- mot de passe incorrect ;
- compte désactivé ;
- MFA non configuré ;
- compte verrouillé.

---

## 40.2 Assistance

Un compte réellement verrouillé peut recevoir une information après une authentification ou un parcours de récupération adapté.

---

# 41. Rate limiting de la connexion

Les endpoints suivants sont limités :

- login ;
- MFA ;
- récupération ;
- invitation ;
- step-up ;
- recovery codes.

---

## 41.1 Partitions

La limitation combine selon le contexte :

```text
source réseau pseudonymisée

identifiant de compte pseudonymisé

endpoint

environnement
```

---

## 41.2 Protection contre le lockout abusif

Une source distante ne doit pas pouvoir verrouiller facilement un grand nombre de comptes sans être elle-même limitée.

---

## 41.3 CORS

CORS ne participe pas à cette protection.

---

# 42. Verrouillage de compte

La baseline initiale est :

```text
5 échecs
→ verrouillage de 15 minutes
```

---

## 42.1 Configuration

Les valeurs restent configurables dans les limites du profil.

---

## 42.2 Échec MFA

Les échecs MFA possèdent également un compteur et une limitation.

---

## 42.3 Verrouillage permanent

Un compte n’est pas verrouillé définitivement automatiquement après quelques tentatives.

---

## 42.4 Audit

L’événement de verrouillage et de déverrouillage est audité.

---

# 43. Temps de réponse

Le temps de réponse ne doit pas permettre de distinguer facilement :

- compte absent ;
- mot de passe invalide ;
- compte verrouillé.

---

## 43.1 Garantie

Une égalité parfaite n’est pas garantie, mais les différences évitables doivent être réduites.

---

# 44. Open redirect

Un paramètre de retour après connexion est considéré comme non fiable.

---

## 44.1 Autorisation

Seules les URLs locales ou identifiants de destination allowlistés sont acceptés.

---

## 44.2 URL externe

Une URL externe est refusée.

---

## 44.3 Credentials

Aucun token n’est ajouté à la query string de redirection.

---

# 45. MFA

L’authentification multifacteur est obligatoire en production pour les comptes possédant :

- rôle administrateur ;
- permissions sensibles ;
- permissions d’audit ;
- permissions d’export ;
- permissions de sécurité ;
- permissions opérationnelles.

---

## 45.1 Utilisateur sans MFA

Un compte privilégié sans MFA configuré ne reçoit pas un accès administratif ordinaire.

Il est limité au parcours d’enrollment ou de récupération autorisé.

---

## 45.2 Environnements locaux

Le développement local peut utiliser un profil adapté.

Il ne doit pas rendre possible une configuration de production sans MFA par erreur.

---

# 46. Facteur MFA initial

Le facteur initial supporté est :

```text
TOTP
```

compatible avec une application d’authentification.

---

## 46.1 Facteurs non acceptés comme baseline

Ne constituent pas le second facteur standard :

- email ;
- lien magique envoyé à la même adresse ;
- SMS ;
- question secrète.

---

## 46.2 Passkeys

Les passkeys ou WebAuthn constituent une évolution recommandée.

Elles nécessiteront une extension ou un nouvel ADR.

---

# 47. Enrollment MFA

L’enrollment suit :

```text
authentification primaire
→ création d’un secret TOTP
→ affichage du QR code ou clé
→ vérification d’un premier code
→ génération des recovery codes
→ confirmation
→ rotation de session
→ audit
```

---

## 47.1 Secret TOTP

Le secret TOTP :

- n’est jamais journalisé ;
- n’est jamais ajouté à un rapport ;
- est protégé au repos ;
- n’est affiché que pendant l’enrollment contrôlé.

---

## 47.2 Activation

Le MFA n’est considéré comme actif qu’après validation d’un code.

---

# 48. Codes TOTP

Les codes sont :

- temporaires ;
- limités en tentatives ;
- non journalisés ;
- non persistés en clair ;
- soumis à un léger clock skew contrôlé.

---

## 48.1 Replay

Le code TOTP ne constitue pas l’identifiant d’idempotence d’une action.

Le challenge de step-up possède son propre identifiant à usage unique.

---

# 49. Recovery codes

Les recovery codes sont :

- générés de manière sûre ;
- affichés une seule fois ;
- utilisables une seule fois ;
- protégés au repos ;
- renouvelables ;
- révoqués lors d’une réinitialisation MFA.

---

## 49.1 Logs

Ils ne sont jamais journalisés.

---

## 49.2 Téléchargement

Ils ne sont pas placés automatiquement dans un fichier public ou un email.

---

## 49.3 Utilisation

L’utilisation d’un recovery code :

- est auditée ;
- peut déclencher une alerte ;
- exige un renouvellement recommandé des codes.

---

# 50. Réinitialisation MFA

La réinitialisation est une action critique.

---

## 50.1 Conditions

Elle exige selon le contexte :

- un autre administrateur autorisé ;
- une procédure de récupération ;
- une preuve opérateur ;
- une raison ;
- une authentification renforcée ;
- un audit.

---

## 50.2 Effets

La réinitialisation :

- supprime l’ancien facteur ;
- révoque les recovery codes ;
- révoque toutes les sessions ;
- modifie le Security Stamp ;
- exige un nouvel enrollment.

---

# 51. Niveaux d’authentification

Le système distingue conceptuellement :

```text
anonymous

primary-authenticated

mfa-authenticated

step-up-fresh
```

---

## 51.1 `primary-authenticated`

Le mot de passe ou facteur primaire a été validé.

---

## 51.2 `mfa-authenticated`

Le MFA a été validé pour la session.

---

## 51.3 `step-up-fresh`

Une vérification récente satisfait le profil d’une action sensible.

---

# 52. Profils de sensibilité des actions

Les actions sont classées :

```text
ordinary

sensitive

critical
```

---

## 52.1 Ordinary

Exemples :

- consulter un tableau de bord ;
- consulter un lead autorisé ;
- éditer un brouillon non publié.

Exigences :

```text
session active
permissions actuelles
```

---

## 52.2 Sensitive

Exemples :

- publier un contenu ;
- télécharger un document privé ;
- générer un export limité ;
- archiver un lead ;
- relancer un job non critique.

Exigences :

```text
session active
MFA présent
authentification primaire récente recommandée
confirmation selon l’action
```

---

## 52.3 Critical

Exemples :

- attribuer un rôle administrateur ;
- réinitialiser le MFA ;
- exporter massivement des données ;
- supprimer définitivement ;
- activer un kill switch ;
- exporter la piste d’audit ;
- lancer une restauration ;
- purger des données ;
- modifier une politique de sécurité.

Exigences :

```text
session active
validation serveur directe
authentification primaire récente
MFA récent
confirmation renforcée
raison obligatoire
audit fail-closed
```

---

# 53. Fenêtre de step-up

La baseline critique est :

```text
10 minutes
```

---

## 53.1 Calcul

La fraîcheur utilise les horodatages serveur :

```text
LastPrimaryAuthenticationAtUtc

LastMfaAuthenticationAtUtc
```

---

## 53.2 Expiration

Après expiration, une nouvelle vérification est exigée.

---

## 53.3 Sliding step-up

La simple activité administrative ne prolonge pas automatiquement la fenêtre de step-up.

---

# 54. Challenge de step-up

Le step-up peut demander :

- le mot de passe actuel ;
- un code MFA ;
- un recovery code uniquement selon la procédure autorisée ;
- un futur passkey.

---

## 54.1 Compte MFA

Pour une action critique, un compte privilégié doit présenter un facteur MFA valide.

---

## 54.2 Mot de passe récent

Un MFA récent ne remplace pas toujours la vérification primaire selon le profil de l’action.

---

# 55. Grant de step-up

Après succès, le serveur émet un grant interne temporaire.

Le grant est lié à :

```text
UserId

SessionId

ActionCategory

TargetType éventuel

TargetId éventuel

IssuedAtUtc

ExpiresAtUtc

Nonce

ConsumedAtUtc éventuel
```

---

## 55.1 Stockage

Le grant est conservé côté serveur ou protégé par un token à usage strict.

La baseline privilégie un état serveur pour les actions critiques.

---

## 55.2 Usage unique

Un grant ciblé sur une action critique peut être consommé une seule fois.

---

## 55.3 Portée

Un grant pour :

```text
réinitialiser le MFA de l’utilisateur A
```

ne doit pas permettre :

```text
exporter tous les leads
```

---

# 56. Reprise après step-up

Le serveur ne conserve pas un payload sensible complet dans une query string.

---

## 56.1 Destination

La destination après step-up utilise :

- identifiant interne ;
- route locale allowlistée ;
- action et cible contrôlées.

---

## 56.2 Confirmation finale

Après le step-up, l’utilisateur confirme de nouveau l’action critique lorsque cela réduit le risque d’exécution involontaire.

---

# 57. Expiration du grant

Un grant expire après une courte durée.

---

## 57.1 Session

Il devient invalide lorsque :

- la session expire ;
- la session est révoquée ;
- le Security Stamp change ;
- l’epoch change ;
- l’utilisateur se déconnecte.

---

## 57.2 Rejeu

Un grant consommé ne peut pas être réutilisé.

---

# 58. Antiforgery

Toutes les actions administratives modifiant un état utilisent l’antiforgery.

---

## 58.1 POST, PUT, PATCH, DELETE

Les requêtes mutables sont protégées selon le modèle de l’application.

---

## 58.2 GET

Une route GET ne doit pas :

- supprimer ;
- publier ;
- archiver ;
- attribuer un rôle ;
- déclencher un export ;
- relancer un job ;
- se déconnecter sans protection.

---

## 58.3 Login

Le formulaire de connexion utilise également l’antiforgery afin de réduire le login CSRF.

---

## 58.4 Step-up

Le formulaire de step-up est protégé.

---

# 59. Cookies antiforgery

Le cookie antiforgery est distinct du cookie d’authentification.

---

## 59.1 Secret

Le token antiforgery complet n’est pas journalisé.

---

## 59.2 Data Protection

Les clés Data Protection sont persistées conformément à l’ADR-0011.

---

# 60. Data Protection Keys

Les clés ASP.NET Core Data Protection sont persistées dans un volume dédié.

---

## 60.1 Redéploiement

Un redéploiement ordinaire ne doit pas invalider toutes les sessions uniquement parce que le conteneur a été recréé.

---

## 60.2 Isolation

Les clés ne sont pas partagées entre :

- clients ;
- staging ;
- production.

---

## 60.3 Compromission

Une compromission des clés exige :

- nouvelle Session Epoch ;
- révocation des sessions ;
- rotation du key ring ;
- analyse ;
- audit.

---

# 61. Restauration

Une restauration PostgreSQL peut réintroduire des sessions qui étaient actives à la date du backup.

---

## 61.1 Baseline

Après toute restauration :

```text
toutes les sessions administratives sont invalidées
```

---

## 61.2 Procédure

```text
restaurer
→ maintenir l’administration sensible désactivée
→ créer un nouvel Audit Epoch
→ créer un nouveau Session Epoch
→ révoquer les sessions restaurées
→ vérifier les comptes privilégiés
→ réactiver l’administration
```

---

## 61.3 Data Protection Keys restaurées

Même si les anciennes clés sont restaurées, la Session Epoch empêche la réutilisation des anciens cookies.

---

# 62. Copie de production vers staging

Lorsqu’une base est copiée :

- toutes les sessions sont invalidées ;
- un nouvel epoch est créé ;
- les clés Data Protection sont distinctes ;
- les emails et destinations sont neutralisés ;
- le MFA et les comptes sont revus selon la procédure.

---

## 62.1 Cookie production

Un cookie de production ne doit jamais devenir valide en staging.

---

# 63. Changement de mot de passe

Le changement exige :

- session active ;
- mot de passe actuel ;
- MFA si requis ;
- nouveau mot de passe conforme ;
- audit.

---

## 63.1 Effets

Après succès :

- Security Stamp modifié ;
- toutes les sessions révoquées ;
- grants de step-up supprimés ;
- nouvelle connexion complète exigée.

---

# 64. Réinitialisation de mot de passe

Une réinitialisation :

- utilise un token à durée bornée ;
- possède un usage unique ;
- ne révèle pas l’existence du compte ;
- ne journalise pas le token ;
- révoque toutes les sessions ;
- modifie le Security Stamp ;
- est auditée lorsqu’elle aboutit.

---

## 64.1 Parcours détaillé

Le processus de récupération sera complété par l’ADR suivant.

---

# 65. Désactivation du compte

La désactivation :

- empêche toute nouvelle connexion ;
- révoque toutes les sessions ;
- invalide les grants ;
- modifie le Security Stamp ;
- est auditée ;
- exige une raison.

---

## 65.1 Suppression

La suppression du compte ne supprime pas les événements d’audit historiques.

---

# 66. Changement de rôle

Une attribution ou suppression de rôle privilégié :

- exige une permission actuelle ;
- exige un step-up critique ;
- exige une raison ;
- modifie le Security Stamp ;
- révoque les sessions de l’utilisateur cible ;
- est auditée.

---

## 66.1 Auto-élévation

Un utilisateur ne peut pas s’attribuer lui-même un privilège critique sauf procédure explicitement autorisée.

---

# 67. Sessions après réduction de permissions

Une réduction de privilèges doit prendre effet sans attendre l’expiration absolue du cookie.

---

## 67.1 Baseline

Révocation de toutes les sessions du compte concerné.

---

## 67.2 Alternative future

Une invalidation ciblée des claims pourra être introduite si le besoin le justifie.

---

# 68. Sessions après augmentation de permissions

Une session existante ne reçoit pas automatiquement les nouvelles permissions.

---

## 68.1 Nouvelle connexion

La baseline exige :

- révocation ;
- nouvelle connexion ;
- MFA ;
- session renouvelée.

---

# 69. Détection de session suspecte

Le système peut conserver des signaux faibles :

- changement majeur de User-Agent ;
- changement fréquent de source ;
- usage simultané inhabituel ;
- nombreuses actions sensibles ;
- MFA échoué ;
- session très ancienne.

---

## 69.1 Pas de binding strict à l’IP

Une session n’est pas liée strictement à une adresse IP.

---

## 69.2 Raisons

- réseaux mobiles ;
- VPN ;
- NAT ;
- changement IPv6 ;
- proxy d’entreprise.

---

## 69.3 Décision

Les signaux peuvent déclencher :

- step-up ;
- alerte ;
- révocation ;
- revue.

---

# 70. Source fingerprint

Une adresse source peut être pseudonymisée par HMAC pour les besoins de sécurité à court terme.

---

## 70.1 Donnée brute

L’adresse IP brute n’est pas stockée dans la table de session principale par défaut.

---

## 70.2 Métriques

Le fingerprint n’est pas utilisé comme label métrique.

---

## 70.3 Rétention

Sa rétention est bornée.

---

# 71. User-Agent

Le User-Agent complet n’est pas nécessairement conservé.

---

## 71.1 Catégorie

Une catégorie bornée peut être utilisée :

```text
desktop-browser

mobile-browser

unknown
```

---

## 71.2 Affichage

L’interface ne doit pas présenter un User-Agent non fiable comme une description certaine de l’appareil.

---

# 72. Session hijacking suspecté

La procédure peut :

1. révoquer la session ;
2. révoquer toutes les sessions ;
3. modifier le Security Stamp ;
4. imposer un changement de mot de passe ;
5. réinitialiser le MFA si nécessaire ;
6. créer une nouvelle Session Epoch pour un incident global ;
7. auditer ;
8. informer l’administrateur.

---

# 73. Audit des événements de session

Événements recommandés :

```text
authentication.session.started

authentication.session.ended

authentication.session.revoked

authentication.session.expired

authentication.session.limit-reached

authentication.step-up.succeeded

authentication.step-up.failed

authentication.mfa.enrolled

authentication.mfa.reset

authentication.recovery-code.used

authentication.account.locked

authentication.account.unlocked

authentication.password.changed

authentication.password-reset.completed

authentication.session-epoch.changed
```

---

## 73.1 Échecs de login

Les échecs individuels à fort volume appartiennent principalement aux événements de sécurité et métriques.

Un verrouillage ou incident significatif est audité.

---

## 73.2 Données interdites

L’audit ne contient pas :

- mot de passe ;
- code TOTP ;
- recovery code ;
- cookie ;
- token reset ;
- token step-up.

---

# 74. Observabilité

Événements techniques recommandés :

```text
AdminLoginAttempted

AdminLoginSucceeded

AdminLoginFailed

AdminSessionCreated

AdminSessionValidationFailed

AdminSessionRevoked

AdminSessionExpired

AdminStepUpRequired

AdminStepUpCompleted

AdminMfaChallengeFailed

AdminAccountLocked
```

---

## 74.1 Propriétés autorisées

```text
SessionId tronqué ou technique

UserId technique

AuthenticationMethod

MfaMethod

FailureCategory

ActionCategory

Environment

Result

ErrorCode

Duration
```

---

## 74.2 Données interdites

```text
Password

MfaCode

RecoveryCode

Cookie

Token

Email complet dans les métriques

Adresse IP brute dans les métriques
```

---

# 75. Métriques

Exemples :

```text
admin login attempts

admin login failures

admin login success

admin sessions active

admin sessions revoked

admin step-up challenges

admin step-up failures

admin account lockouts

admin mfa failures

admin session validation duration
```

---

## 75.1 Labels autorisés

```text
result

failure_category

authentication_method

mfa_method

environment

action_category
```

---

## 75.2 Labels interdits

```text
UserId

SessionId

Email

IP

TraceId

CorrelationId
```

---

# 76. Alertes

Alertes possibles :

```text
Échecs de login en forte hausse

Verrouillages multiples

MFA échoué de manière répétée

Sessions révoquées en masse

Session Epoch modifié

Usage d’un recovery code

Compte privilégié sans MFA

Step-up failures inhabituels

Tentatives de rôle sans autorisation
```

---

## 76.1 Runbook

Chaque alerte critique doit référencer une procédure.

---

# 77. Authentification récente

Les horodatages de fraîcheur sont conservés côté serveur.

---

## 77.1 Cookie

Un simple claim dans le cookie ne suffit pas pour une action critique.

---

## 77.2 Validation

L’action critique relit l’état de session depuis PostgreSQL.

---

## 77.3 MFA récent

Une session MFA ancienne peut exiger un nouveau code sans exiger nécessairement une déconnexion complète.

---

# 78. Action pending

Une action sensible ne doit pas stocker arbitrairement son payload complet dans la session.

---

## 78.1 Référence

Le challenge peut conserver :

- Action ID ;
- Target ID ;
- hash des paramètres importants ;
- expiration ;
- nonce.

---

## 78.2 Après step-up

Le serveur exige une nouvelle soumission de l’action avec :

- antiforgery ;
- paramètres validés ;
- grant correspondant.

---

# 79. Confirmation renforcée

Une action critique doit présenter :

- nom de l’action ;
- cible ;
- impact ;
- caractère réversible ou non ;
- raison ;
- authentification requise.

---

## 79.1 Saisie de confirmation

Une saisie textuelle du nom de cible peut être utilisée pour certaines suppressions.

Elle ne remplace pas l’autorisation.

---

## 79.2 Changement de select

Une action critique ne doit pas être exécutée immédiatement lors d’un simple changement de liste déroulante.

---

# 80. Protection de la page de connexion

La page de connexion doit utiliser :

- HTTPS ;
- antiforgery ;
- rate limiting ;
- headers de sécurité ;
- aucune ressource tierce non nécessaire ;
- aucune préremplissage de mot de passe ;
- aucune distinction de compte.

---

## 80.1 Cache

La réponse de connexion ne doit pas être mise en cache publiquement.

---

## 80.2 Autofill

Le formulaire doit rester compatible avec les gestionnaires de mots de passe.

---

# 81. Gestionnaire de mots de passe

Les attributs HTML doivent permettre :

- `username` ;
- `current-password` ;
- `new-password` ;
- `one-time-code` lorsque pertinent.

---

## 81.1 Collage

Le collage dans les champs de mot de passe et MFA ne doit pas être bloqué.

---

## 81.2 Raisons

Bloquer le collage pénalise :

- gestionnaires de mots de passe ;
- accessibilité ;
- mots de passe forts.

---

# 82. Accessibilité

Les parcours de :

- connexion ;
- MFA ;
- step-up ;
- récupération ;
- gestion de session ;

doivent être accessibles au clavier et aux technologies d’assistance.

---

## 82.1 Timeout

L’utilisateur doit être averti avant une expiration imminente lorsque l’interface le permet.

---

## 82.2 Extension arbitraire

L’avertissement ne doit pas permettre de prolonger indéfiniment l’expiration absolue.

---

# 83. Administration Blazor

Les composants Blazor masquant une action ne constituent pas le contrôle de sécurité.

---

## 83.1 Serveur

Le handler, endpoint ou service Application applique :

- session ;
- permission ;
- capability ;
- step-up ;
- antiforgery ;
- audit.

---

## 83.2 Reconnexion Blazor

Une reconnexion du circuit ne doit pas recréer une session révoquée.

---

# 84. APIs administratives

Les APIs utilisées par l’administration utilisent le même cookie sécurisé lorsque same-origin.

---

## 84.1 JWT

Un JWT navigateur distinct n’est pas ajouté par défaut.

---

## 84.2 CORS

CORS ne constitue pas une autorisation.

---

## 84.3 Antiforgery

Les requêtes mutables same-origin restent protégées.

---

# 85. Administration multi-instance future

Le profil initial utilise une instance web.

---

## 85.1 Plusieurs instances

Une architecture multi-instance nécessitera :

- registre partagé PostgreSQL ;
- cache distribué ou invalidation ;
- Data Protection partagée dans le périmètre du client ;
- Session Epoch partagé ;
- validation cohérente ;
- step-up grants partagés.

---

## 85.2 Sticky sessions

Les sticky sessions ne doivent pas être l’unique garantie de sécurité.

---

# 86. Nettoyage des sessions

Un job récurrent supprime ou archive les sessions anciennes.

---

## 86.1 Rétention

Les données de session ne sont pas conservées indéfiniment.

---

## 86.2 Sessions actives

Le nettoyage ne supprime pas une session active valide.

---

## 86.3 Audit

L’historique de sécurité significatif reste dans la piste d’audit, pas dans la table de sessions.

---

# 87. Protection des tables Identity

Le rôle runtime possède uniquement les droits nécessaires.

---

## 87.1 Modification directe

Les rôles, MFA, Security Stamps et sessions ne doivent pas être modifiés par SQL manuel ordinaire.

---

## 87.2 Opération d’urgence

Une correction directe exige :

- procédure break-glass ;
- raison ;
- rapport ;
- audit après rétablissement.

---

# 88. Backup

Les tables Identity et sessions font partie du backup PostgreSQL.

---

## 88.1 Sessions

Les sessions sauvegardées ne doivent pas être réactivées après restauration.

---

## 88.2 Secrets MFA

Les données nécessaires au MFA restent protégées dans le backup chiffré.

---

## 88.3 Recovery codes

Les recovery codes ne doivent pas apparaître dans les manifests de backup.

---

# 89. Transfert client

Le handover doit inclure :

- modèle Identity ;
- politiques de session ;
- durées ;
- MFA ;
- Session Epoch ;
- procédures de révocation ;
- gestion des sessions ;
- configuration Data Protection ;
- runbooks ;
- comptes à transférer ;
- comptes LogicGate à révoquer.

---

## 89.1 Credentials LogicGate

Les comptes LogicGate ne doivent pas rester actifs après transfert sans contrat explicite.

---

## 89.2 Session finale

Toutes les sessions opérateur LogicGate sont révoquées au moment défini dans le handover.

---

# 90. Clôture de service

La clôture suit :

```text
inventorier les comptes
→ transférer les comptes client
→ vérifier le MFA
→ révoquer les sessions LogicGate
→ retirer les rôles LogicGate
→ modifier la Session Epoch si nécessaire
→ produire un rapport
→ auditer
```

---

# 91. Break-glass

La procédure break-glass de l’ADR-0019 doit s’intégrer aux sessions.

---

## 91.1 Compte

Le compte break-glass :

- n’est pas utilisé quotidiennement ;
- possède un credential distinct ;
- exige MFA lorsque techniquement possible ;
- possède une durée de session réduite ;
- ne possède qu’une session active ;
- déclenche une alerte immédiate.

---

## 91.2 Durée

Une session break-glass utilise des expirations plus strictes que la baseline ordinaire.

---

## 91.3 Fin

Après l’incident :

- session révoquée ;
- credential rotaté ;
- action revue ;
- audit vérifié.

---

# 92. Sécurité

## 92.1 Actifs

- comptes ;
- cookies ;
- mots de passe ;
- facteurs MFA ;
- recovery codes ;
- sessions ;
- permissions ;
- données administratives ;
- piste d’audit ;
- Data Protection Keys.

---

## 92.2 Menaces

- credential stuffing ;
- brute force ;
- session fixation ;
- vol de cookie ;
- XSS ;
- CSRF ;
- MFA fatigue ;
- recovery abuse ;
- permission obsolète ;
- open redirect ;
- session restaurée ;
- compte administrateur persistant ;
- réutilisation de mot de passe.

---

## 92.3 Contrôles

- cookies HttpOnly ;
- Secure ;
- SameSite ;
- antiforgery ;
- rate limiting ;
- lockout ;
- MFA ;
- step-up ;
- expiration absolue ;
- registre serveur ;
- Security Stamp ;
- Session Epoch ;
- audit ;
- révocation ;
- Data Protection persistée.

---

# 93. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Token volé par XSS | Cookie HttpOnly |
| Cookie envoyé en clair | Secure et HTTPS |
| CSRF | Antiforgery et SameSite |
| Session volée | Registre serveur et révocation |
| Cookie ancien après changement de rôle | Security Stamp et révocation |
| Session réactivée après restore | Session Epoch |
| Credential stuffing | Rate limiting, lockout et alertes |
| Mot de passe compromis | MFA |
| Session ancienne effectuant une suppression | Step-up |
| Fixation de session | Nouvel ID après authentification |
| Open redirect | Return URL locale allowlistée |
| Admin sans MFA | Enrollment obligatoire |
| Compte supprimé encore connecté | Révocation serveur |

---

# 94. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-AUTH-001` | Authentification requise |
| `SF-AUTH-002` | Credentials invalides |
| `SF-AUTH-003` | Compte désactivé |
| `SF-AUTH-004` | Compte verrouillé |
| `SF-AUTH-005` | Mot de passe expiré selon une politique exceptionnelle |
| `SF-AUTH-006` | Mot de passe non conforme |
| `SF-AUTH-007` | Mot de passe commun ou compromis |
| `SF-AUTH-008` | MFA requis |
| `SF-AUTH-009` | Enrollment MFA requis |
| `SF-AUTH-010` | Code MFA invalide |
| `SF-AUTH-011` | Trop de tentatives MFA |
| `SF-AUTH-012` | Recovery code invalide |
| `SF-AUTH-013` | Recovery code déjà utilisé |
| `SF-AUTH-014` | Réinitialisation MFA requise |
| `SF-AUTH-015` | Token de récupération invalide |
| `SF-AUTH-016` | Token de récupération expiré |
| `SF-AUTH-017` | Redirection non autorisée |
| `SF-SESSION-001` | Session inconnue |
| `SF-SESSION-002` | Session révoquée |
| `SF-SESSION-003` | Session expirée par inactivité |
| `SF-SESSION-004` | Session expirée absolument |
| `SF-SESSION-005` | Session Epoch invalide |
| `SF-SESSION-006` | Security Stamp invalide |
| `SF-SESSION-007` | Session du mauvais utilisateur |
| `SF-SESSION-008` | Session du mauvais client |
| `SF-SESSION-009` | Session du mauvais environnement |
| `SF-SESSION-010` | Limite de sessions atteinte |
| `SF-SESSION-011` | Registre de sessions indisponible |
| `SF-SESSION-012` | Cookie d’authentification invalide |
| `SF-SESSION-013` | Cookie non sécurisé |
| `SF-SESSION-014` | Session superseded |
| `SF-SESSION-015` | Révocation échouée |
| `SF-SESSION-016` | Renouvellement de session refusé |
| `SF-STEPUP-001` | Authentification renforcée requise |
| `SF-STEPUP-002` | Authentification primaire trop ancienne |
| `SF-STEPUP-003` | MFA trop ancien |
| `SF-STEPUP-004` | Challenge inconnu |
| `SF-STEPUP-005` | Challenge expiré |
| `SF-STEPUP-006` | Challenge consommé |
| `SF-STEPUP-007` | Challenge lié à une autre session |
| `SF-STEPUP-008` | Action incompatible avec le grant |
| `SF-STEPUP-009` | Cible incompatible avec le grant |
| `SF-STEPUP-010` | Confirmation finale requise |
| `SF-STEPUP-011` | Raison obligatoire absente |
| `SF-STEPUP-012` | Grant révoqué |
| `SF-CSRF-001` | Token antiforgery absent |
| `SF-CSRF-002` | Token antiforgery invalide |
| `SF-CSRF-003` | Action mutable par GET interdite |
| `SF-IDENTITY-001` | Rôle actuel insuffisant |
| `SF-IDENTITY-002` | Permission actuelle insuffisante |
| `SF-IDENTITY-003` | Changement de rôle nécessitant une reconnexion |
| `SF-IDENTITY-004` | Compte privilégié sans MFA |
| `SF-IDENTITY-005` | Session de compte supprimé |

---

# 95. Tests unitaires

Tester notamment :

- calcul de l’expiration inactive ;
- expiration absolue ;
- sliding expiration ;
- Session Epoch ;
- niveaux d’authentification ;
- fraîcheur ;
- profils d’actions ;
- validation des grants ;
- transitions de session ;
- limite de sessions ;
- redirections locales.

---

# 96. Tests du cookie

Vérifier :

```text
HttpOnly

Secure en production

SameSite attendu

Path correct

Domain absent

préfixe __Host-

expiration attendue

aucune donnée sensible

taille bornée
```

---

# 97. Tests de session

Couvrir :

```text
création

validation

inactivité

expiration absolue

renouvellement

révocation ciblée

révocation globale

Security Stamp modifié

Session Epoch modifié

compte désactivé

rôle retiré

registre indisponible
```

---

# 98. Tests de fixation

Vérifier qu’un identifiant de session anonyme ou pré-authentification n’est pas conservé après :

- login ;
- MFA ;
- step-up ;
- changement de mot de passe.

---

# 99. Tests de login

Couvrir :

- compte valide ;
- utilisateur inconnu ;
- mot de passe invalide ;
- compte désactivé ;
- compte verrouillé ;
- MFA requis ;
- erreur générique ;
- antiforgery ;
- return URL externe ;
- rate limiting ;
- lockout.

---

# 100. Tests d’énumération

Comparer les réponses pour :

```text
compte absent

mot de passe invalide

compte désactivé

compte verrouillé
```

Vérifier :

- message générique ;
- statut cohérent ;
- absence de détail ;
- pas de différence fonctionnelle évidente évitable.

---

# 101. Tests de mot de passe

Tester :

- moins de 14 caractères ;
- 14 caractères ;
- phrase longue ;
- espaces ;
- Unicode ;
- 128 caractères ;
- mot de passe commun ;
- mot de passe proche de l’email ;
- copier-coller ;
- gestionnaire de mots de passe.

---

# 102. Tests MFA

Couvrir :

```text
enrollment

premier code valide

code invalide

code expiré

tentatives excessives

recovery code

recovery code réutilisé

réinitialisation

compte privilégié sans MFA

rotation de session
```

---

# 103. Tests de step-up

Vérifier :

- action ordinaire ;
- action sensible ;
- action critique ;
- mot de passe récent ;
- mot de passe ancien ;
- MFA récent ;
- MFA ancien ;
- challenge expiré ;
- grant consommé ;
- mauvaise cible ;
- mauvaise action ;
- autre session ;
- autre utilisateur ;
- confirmation absente.

---

# 104. Tests de contournement

Appeler directement l’endpoint critique sans passer par l’UI.

Vérifier :

```text
permission

session serveur

MFA

fraîcheur

grant

antiforgery

raison

audit
```

---

# 105. Tests CSRF

Tester :

- token absent ;
- token invalide ;
- cookie absent ;
- requête cross-site ;
- endpoint GET ;
- login CSRF ;
- logout CSRF ;
- step-up CSRF.

---

# 106. Tests de révocation

Vérifier :

```text
logout

révocation d’une session

révocation de toutes les sessions

changement de mot de passe

reset MFA

désactivation

retrait de rôle

changement d’epoch
```

---

## 106.1 Délai

Les actions critiques doivent constater immédiatement la révocation.

Les actions ordinaires respectent au maximum la fenêtre de cache documentée.

---

# 107. Tests de restauration

Scénario :

1. créer une session ;
2. produire un backup ;
3. révoquer la session ;
4. restaurer le backup ;
5. conserver les Data Protection Keys ;
6. créer un nouvel epoch ;
7. réutiliser l’ancien cookie.

Résultat attendu :

```text
session refusée
```

---

# 108. Tests multi-environnements

Vérifier :

- cookie staging refusé en production ;
- cookie d’un client refusé chez un autre ;
- Data Protection Keys distinctes ;
- Session Epoch distinct ;
- MFA secrets isolés ;
- base distincte.

---

# 109. Tests de concurrence

Tester :

- plusieurs logins simultanés ;
- limite de sessions ;
- révocation pendant une requête ;
- renouvellement concurrent ;
- deux step-up simultanés ;
- grant consommé deux fois.

---

# 110. Tests de sécurité

Vérifier notamment :

```text
Token absent de localStorage

Token absent de sessionStorage

Cookie inaccessible par JavaScript

Aucun mot de passe loggué

Aucun code MFA loggué

Aucun token dans l’URL

Open redirect refusé

Action mutable par GET refusée

Claims obsolètes refusés

Session restaurée refusée

Admin sans MFA refusé
```

---

# 111. Tests d’audit

Vérifier :

- login réussi ;
- logout ;
- révocation ;
- lockout ;
- MFA enrollment ;
- MFA reset ;
- recovery code utilisé ;
- step-up réussi ;
- changement d’epoch ;
- absence de secret.

---

# 112. Tests de performance

Mesurer :

- validation de session ;
- cache ;
- accès PostgreSQL ;
- renouvellement ;
- listing de sessions ;
- révocation ;
- step-up.

---

## 112.1 Objectif

La sécurité ne doit pas provoquer une écriture PostgreSQL à chaque asset ou requête publique.

---

# 113. Tests multiplateformes

Les tests fonctionnels sont exécutés sur :

```text
Windows x64

Linux x64
```

Les tests Docker, cookies HTTPS et Data Protection sont validés sur Linux.

---

# 114. Quality gates

Une fonctionnalité administrative est bloquée lorsque :

```text
Le cookie n’est pas HttpOnly.

Le cookie n’est pas Secure en production.

Un token est stocké dans localStorage.

L’expiration absolue est absente.

La session n’est pas révocable.

Les permissions reposent uniquement sur des claims anciens.

Une action critique n’exige pas de step-up.

Un rôle administrateur contourne le MFA.

Une action mutable utilise GET.

L’antiforgery est absent.

Une return URL externe est acceptée.

Un mot de passe ou code MFA est journalisé.

Une restauration peut réactiver une session.

Un compte privilégié peut fonctionner sans MFA en production.

Les Data Protection Keys ne sont pas persistées.
```

---

# 115. Quality gates de production

La production est bloquée lorsque :

```text
MFA n’est pas obligatoire pour les rôles privilégiés.

Le cookie Secure n’est pas activé.

HTTPS n’est pas disponible.

Le registre de sessions est absent.

La Session Epoch n’est pas initialisée.

Les migrations Identity nécessaires ne sont pas appliquées.

Les clés Data Protection sont éphémères.

Les durées de session sont illimitées.

L’option Remember Me est active sans décision.

Les actions critiques ne sont pas cataloguées.

La procédure de restauration n’invalide pas les sessions.
```

---

# 116. CI/CD

La CI doit vérifier :

- configuration du cookie ;
- tests Identity ;
- tests de session ;
- tests MFA ;
- tests step-up ;
- antiforgery ;
- permissions ;
- audit ;
- migrations ;
- secret scanning ;
- absence de stockage JavaScript.

---

## 116.1 Tests navigateur

Une suite ciblée doit vérifier les attributs réels des cookies sur un serveur HTTPS de test.

---

## 116.2 Secrets MFA réels

La CI utilise des secrets de test éphémères.

---

# 117. Sécurité des diagnostics

Un endpoint de diagnostic peut indiquer :

```text
session active

MFA configured

step-up required

session expiration
```

---

## 117.1 Interdictions

Il ne doit pas exposer :

- Security Stamp ;
- cookie ;
- secret TOTP ;
- recovery codes ;
- source fingerprint ;
- autres sessions sans permission.

---

# 118. Règles pour les modules

Un module ajoutant une action administrative doit déclarer :

```text
ActionId

RequiredPermission

Sensitivity

MfaRequired

StepUpProfile

ReasonRequired

ConfirmationRequired

AuditActionId
```

---

## 118.1 Valeur par défaut

Une action non déclarée ne doit pas être considérée implicitement comme ordinaire si elle modifie :

- permissions ;
- données ;
- sécurité ;
- disponibilité ;
- confidentialité.

---

# 119. Règles pour les customizations

Une customization client utilise les contrats publics de :

- session ;
- autorisation ;
- step-up ;
- audit.

---

## 119.1 Interdictions

Elle ne doit pas :

- lire le cookie manuellement ;
- contourner le registre serveur ;
- stocker un token dans JavaScript ;
- créer son propre système de MFA ;
- réduire la fraîcheur d’une action critique ;
- désactiver l’antiforgery ;
- accepter une return URL arbitraire.

---

# 120. Règles pour LogicGate

LogicGate doit :

- imposer le MFA ;
- surveiller les verrouillages ;
- revoir les sessions actives ;
- révoquer les accès inutiles ;
- tester les restaurations ;
- maintenir les durées ;
- protéger Data Protection ;
- documenter les actions critiques ;
- maintenir les runbooks.

---

## 120.1 LogicGate ne doit pas

- partager un compte administrateur ;
- réutiliser un mot de passe ;
- désactiver le MFA pour faciliter un incident ordinaire ;
- laisser une session active après transfert ;
- allonger indéfiniment les durées ;
- créer un mot de passe par défaut ;
- demander un code MFA dans un ticket.

---

# 121. Règles pour Codex

Codex peut :

- implémenter les contrats ;
- ajouter le registre de sessions ;
- configurer les cookies ;
- ajouter les tests ;
- créer les écrans de gestion ;
- ajouter les profils step-up ;
- documenter les runbooks.

---

## 121.1 Codex ne doit pas

```text
Créer un compte de production

Inventer un mot de passe

Désactiver MFA

Stocker un token dans localStorage

Désactiver l’antiforgery

Rendre un cookie accessible à JavaScript

Allonger les sessions sans décision

Révoquer une session réelle

Modifier les Data Protection Keys réelles

Créer un compte break-glass

Contourner une action critique

Appliquer une migration production
```

---

## 121.2 Décision humaine

Une décision humaine est requise pour :

- modification des durées de production ;
- désactivation exceptionnelle du MFA ;
- nouveau facteur MFA ;
- nouveau fournisseur d’identité ;
- création d’un break-glass ;
- changement global de Session Epoch ;
- compromission ;
- step-up d’une nouvelle action critique ;
- collecte d’une nouvelle donnée réseau.

---

# 122. Anti-patterns interdits

## 122.1 JWT dans `localStorage`

Le navigateur conserve un bearer token accessible à JavaScript.

---

## 122.2 Cookie permanent illimité

Une session reste valide tant que l’utilisateur utilise le site.

---

## 122.3 Claims de rôle permanents

Le cookie conserve une permission retirée plusieurs jours auparavant.

---

## 122.4 MFA facultatif pour l’administrateur

Le rôle le plus puissant utilise uniquement un mot de passe.

---

## 122.5 Step-up uniquement UI

Le bouton demande un mot de passe, mais l’endpoint direct ne le vérifie pas.

---

## 122.6 Logout par GET

Un lien externe peut déclencher une déconnexion.

---

## 122.7 Return URL libre

Le login redirige vers une URL externe fournie par l’appelant.

---

## 122.8 Mot de passe loggué

Une exception ou un objet d’options sérialise les credentials.

---

## 122.9 Session liée strictement à l’IP

Un changement de réseau déconnecte systématiquement l’utilisateur légitime.

---

## 122.10 Remember Me activé par défaut

Une session privilégiée survit longtemps sur un poste partagé.

---

## 122.11 Restore sans révocation

Les cookies de la date du backup redeviennent valides.

---

## 122.12 Changement de rôle sans reconnexion

Une session existante obtient immédiatement un nouveau privilège critique.

---

# 123. Conséquences positives

La décision apporte :

- révocation ciblée ;
- MFA ;
- step-up ;
- expiration absolue ;
- sessions inventoriables ;
- contrôle des permissions actuelles ;
- protection après restauration ;
- réduction du risque de cookie volé ;
- meilleure traçabilité ;
- gestion des appareils ;
- meilleure sécurité des actions critiques ;
- compatibilité avec ASP.NET Core Identity ;
- autonomie du projet.

---

# 124. Conséquences négatives

La décision entraîne :

- tables de sessions ;
- migrations ;
- accès PostgreSQL ;
- UI supplémentaire ;
- MFA à déployer ;
- recovery codes ;
- parcours de step-up ;
- tests nombreux ;
- risques de faux verrouillages ;
- administration indisponible si la base est indisponible ;
- procédures de révocation et récupération.

---

# 125. Risques acceptés

## 125.1 Fenêtre de cache

Une révocation ordinaire peut nécessiter quelques secondes pour être constatée sur certaines requêtes non critiques.

Les actions critiques vérifient directement le registre.

---

## 125.2 Perte d’un facteur MFA

Un administrateur peut perdre son appareil.

Réduction :

- recovery codes ;
- second administrateur lorsque possible ;
- procédure de récupération ;
- break-glass contrôlé.

---

## 125.3 PostgreSQL requis

L’administration dépend de PostgreSQL.

Ce risque est accepté car les opérations administratives dépendent déjà majoritairement de la base.

---

## 125.4 Friction

Le step-up ajoute une interaction pour les actions critiques.

Ce coût est accepté pour réduire l’impact d’une session abandonnée ou volée.

---

# 126. Mesures compensatoires

- cookie sécurisé ;
- registre serveur ;
- expiration inactive ;
- expiration absolue ;
- Session Epoch ;
- Security Stamp ;
- MFA ;
- recovery codes ;
- step-up ;
- rate limiting ;
- lockout ;
- antiforgery ;
- audit ;
- gestion des sessions ;
- alertes ;
- runbooks ;
- tests de restauration.

---

# 127. Performance

## 127.1 Validation

La validation utilise un cache court pour les opérations ordinaires.

---

## 127.2 Actions critiques

Un accès PostgreSQL direct est accepté.

---

## 127.3 Last Seen

La mise à jour est amortie afin d’éviter une écriture par requête.

---

## 127.4 Nettoyage

Les sessions expirées sont supprimées ou archivées par batch.

---

# 128. Coûts

## 128.1 Coûts supplémentaires

- développement ;
- stockage ;
- MFA ;
- support ;
- récupération ;
- tests ;
- maintenance ;
- audit.

---

## 128.2 Coûts évités

- compromission prolongée ;
- export non autorisé ;
- élévation persistante ;
- session restaurée ;
- perte de données ;
- incident sans capacité de révocation ;
- comptes partagés.

---

# 129. Maintenabilité

Pour chaque action administrative, il doit être possible de répondre :

```text
Quelle permission est requise ?

Quel niveau de sensibilité ?

Le MFA est-il requis ?

Quelle fraîcheur est exigée ?

Une raison est-elle obligatoire ?

Le grant est-il ciblé ?

L’action est-elle antiforgery-protected ?

Quel événement d’audit est produit ?

Une session révoquée peut-elle encore l’exécuter ?
```

---

# 130. Compatibilité

## 130.1 ASP.NET Core Identity

La solution reste fondée sur les abstractions supportées d’Identity.

---

## 130.2 Cookie

Le format interne du cookie n’est pas considéré comme un contrat public.

---

## 130.3 Session Schema

Les tables possèdent des migrations explicites.

---

## 130.4 IdP futur

Un fournisseur externe pourra produire une authentification primaire ou MFA.

Le registre de sessions et le step-up pourront être conservés ou adaptés.

---

## 130.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration de sessions purement stateless nécessitera l’invalidation des cookies existants.

---

# 131. Migration depuis un cookie sans registre

Le processus est :

```text
créer les tables
→ créer la Session Epoch
→ déployer le registre
→ invalider les anciens cookies
→ exiger une nouvelle connexion
→ vérifier MFA
→ activer la validation serveur
```

---

## 131.1 Ancien cookie

Aucun ancien cookie ne doit être converti silencieusement en session serveur active.

---

# 132. Migration depuis un JWT navigateur

Le processus est :

```text
désactiver l’émission
→ révoquer les refresh tokens
→ supprimer le stockage navigateur
→ introduire le cookie sécurisé
→ créer les sessions serveur
→ ajouter antiforgery
→ tester
→ forcer une reconnexion
```

---

## 132.1 Nettoyage navigateur

L’application doit retirer les anciens tokens lors de la transition lorsque possible.

---

# 133. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de révoquer des sessions réelles ;
- de créer un administrateur ;
- de désactiver le MFA ;
- de modifier la Session Epoch ;
- de remplacer les Data Protection Keys ;
- d’appliquer une migration ;
- de réinitialiser un compte.

---

# 134. Réversibilité

## 134.1 Classification

```text
élevée avant déploiement

moyenne après émission de sessions

faible pour une session compromise déjà exploitée
```

---

## 134.2 Désactivation du registre

Revenir vers des sessions uniquement stateless réduirait les garanties.

Un nouvel ADR serait requis.

---

## 134.3 Nouveau facteur

TOTP pourra être complété ou remplacé par un facteur plus fort sans modifier les permissions métier.

---

# 135. Plan d’implémentation

## Étape 1 — Contrats de session

Définir :

- `AdminSessionId` ;
- statuts ;
- expirations ;
- Session Epoch ;
- erreurs.

---

## Étape 2 — PostgreSQL

Créer :

- sessions ;
- epochs ;
- index ;
- migrations ;
- rétention.

---

## Étape 3 — Cookie

Configurer :

- nom ;
- HttpOnly ;
- Secure ;
- SameSite ;
- expiration ;
- claims minimaux.

---

## Étape 4 — Validation

Ajouter :

- registre ;
- Security Stamp ;
- epoch ;
- expirations ;
- compte actif ;
- cache court.

---

## Étape 5 — Révocation

Ajouter :

- logout ;
- session ciblée ;
- toutes les sessions ;
- changement de sécurité ;
- restauration.

---

## Étape 6 — MFA

Ajouter :

- enrollment TOTP ;
- challenge ;
- recovery codes ;
- reset ;
- audit.

---

## Étape 7 — Step-up

Créer :

- profils ;
- fraîcheur ;
- grants ;
- confirmation ;
- endpoints ;
- audit.

---

## Étape 8 — Actions critiques

Appliquer le step-up à :

- rôles ;
- exports ;
- suppressions ;
- kill switches ;
- audit ;
- jobs opérateur.

---

## Étape 9 — Interface

Créer :

- sessions actives ;
- révocation ;
- MFA ;
- expiration ;
- avertissements.

---

## Étape 10 — Opérations

Ajouter :

- métriques ;
- alertes ;
- restore runbook ;
- incident session hijacking ;
- handover.

---

# 136. Tâches liées proposées

```text
SF-TASK-237 — Define administrator session contracts

SF-TASK-238 — Add PostgreSQL administrator session persistence

SF-TASK-239 — Add environment session epochs

SF-TASK-240 — Configure hardened administrator cookies

SF-TASK-241 — Add server-side administrator session validation

SF-TASK-242 — Add targeted and global session revocation

SF-TASK-243 — Add bounded idle and absolute session expiration

SF-TASK-244 — Add administrator TOTP enrollment

SF-TASK-245 — Add administrator MFA challenges and recovery codes

SF-TASK-246 — Add step-up authentication profiles

SF-TASK-247 — Add one-time step-up grants

SF-TASK-248 — Protect critical administrative actions with step-up

SF-TASK-249 — Add administrator active-session management

SF-TASK-250 — Add session restore invalidation

SF-TASK-251 — Add administrator authentication security tests

SF-TASK-252 — Add session-security observability and runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 137. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
L’administration utilise un cookie HttpOnly.

Le cookie est Secure en production.

Le cookie utilise SameSite selon le profil.

Aucun token administrateur n’est stocké dans localStorage.

Chaque session possède un état PostgreSQL.

Une session possède une expiration inactive.

Une session possède une expiration absolue.

La sliding expiration ne dépasse pas l’expiration absolue.

Une session ciblée peut être révoquée.

Toutes les sessions d’un utilisateur peuvent être révoquées.

Le changement de mot de passe révoque les sessions.

Le changement de rôle révoque les sessions concernées.

Le compte désactivé ne conserve aucune session active.

Le Session Epoch invalide les anciennes sessions.

Une restauration invalide les sessions du backup.

Un compte privilégié nécessite le MFA en production.

L’enrollment TOTP exige un premier code valide.

Les recovery codes sont à usage unique.

Une réinitialisation MFA révoque toutes les sessions.

Une action critique exige un step-up récent.

Un grant de step-up est lié à la session et à l’action.

Un grant consommé ne peut pas être rejoué.

Une action mutable par GET est refusée.

L’antiforgery protège le login, le step-up et les actions.

Une redirection externe après login est refusée.

Les messages de login ne permettent pas l’énumération.

Le rate limiting et le lockout fonctionnent.

Aucun mot de passe, code MFA ou token n’apparaît dans les logs.

Les actions sensibles produisent les événements d’audit attendus.

Les tests fonctionnent sur Windows et Linux.
```

---

# 138. Conditions de révision

Réexaminer cette décision si :

- un IdP externe devient obligatoire ;
- les passkeys deviennent la baseline ;
- plusieurs instances web deviennent la norme ;
- un SSO d’entreprise est requis ;
- une authentification sans mot de passe est adoptée ;
- les sessions doivent être partagées entre plusieurs applications ;
- un niveau réglementaire impose une durée plus stricte ;
- une approbation à deux personnes devient nécessaire ;
- une authentification continue par risque est introduite ;
- l’administration devient une application séparée ;
- les comptes clients doivent utiliser leur propre fédération.

---

# 139. Indicateurs à suivre

- sessions actives ;
- durée moyenne de session ;
- sessions expirées ;
- sessions révoquées ;
- comptes sans MFA ;
- enrollments MFA ;
- échecs MFA ;
- recovery codes utilisés ;
- step-up demandés ;
- step-up échoués ;
- comptes verrouillés ;
- connexions refusées ;
- changements d’epoch ;
- sessions multiples ;
- actions critiques refusées ;
- incidents de session.

---

# 140. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des comptes privilégiés restent sans MFA

les sessions restent valides après changement de rôle

les cookies survivent à une restauration

des tokens apparaissent dans localStorage

les expirations absolues sont désactivées

les grants de step-up sont réutilisables

des actions critiques n’exigent pas de réauthentification

les recovery codes sont partagés par email

les sessions LogicGate restent actives après transfert

les Data Protection Keys sont perdues à chaque déploiement

les logs contiennent des credentials

les lockouts sont utilisés comme déni de service
```

---

# 141. Conséquences observées

Cette section est append-only après mise en œuvre.

## 141.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation du registre de sessions,
du MFA administrateur et de l’authentification renforcée.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- méthode d’authentification ;
- durée ;
- révocation ;
- step-up ;
- incident ou bénéfice ;
- action proposée.

---

# 142. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](./0028-database-transaction-boundaries-and-concurrency-control.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](./0021-administrator-provisioning-invitations-and-account-recovery.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
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
- [0000-adr-template.md](./0000-adr-template.md)
- [0001-one-repository-and-database-per-client.md](./0001-one-repository-and-database-per-client.md)
- [0003-postgresql-as-primary-database.md](./0003-postgresql-as-primary-database.md)
- [0004-explicit-production-migrations.md](./0004-explicit-production-migrations.md)
- [0006-feature-flags-and-entitlements-separation.md](./0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](./0007-module-composition-and-public-contracts.md)
- [0009-client-customizations-and-extension-points.md](./0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](./0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](./0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](./0012-runtime-configuration-and-secrets.md)
- [0017-background-jobs-and-reliable-side-effects.md](./0017-background-jobs-and-reliable-side-effects.md)
- [0018-public-form-abuse-protection-and-rate-limiting.md](./0018-public-form-abuse-protection-and-rate-limiting.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](./0019-audit-trail-and-sensitive-administrative-actions.md)

---

# 143. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 138 |

---

# 144. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 145. Synthèse de la décision

```text
Authentification
→ ASP.NET Core Identity
→ mot de passe robuste
→ MFA obligatoire pour les comptes privilégiés

Navigateur
→ cookie __Host-
→ HttpOnly
→ Secure
→ SameSite
→ aucun token dans localStorage

Session
→ état PostgreSQL
→ Session ID
→ Security Stamp
→ Session Epoch
→ révocation

Expiration
→ inactivité : 30 minutes
→ absolue : 8 heures
→ sliding expiration bornée
→ Remember Me désactivé

MFA
→ TOTP
→ recovery codes à usage unique
→ email et SMS non utilisés comme facteur standard
→ reset critique et audité

Step-up
→ action sensible cataloguée
→ mot de passe récent
→ MFA récent
→ fenêtre critique : 10 minutes
→ grant lié à la session, à l’action et à la cible
→ confirmation finale
→ audit fail-closed

Actions critiques
→ rôles
→ permissions
→ exports
→ suppressions
→ kill switches
→ audit
→ restaurations
→ opérations sensibles

Protection
→ antiforgery
→ aucune mutation par GET
→ rate limiting
→ lockout
→ erreurs génériques
→ return URL locale

Révocation
→ logout
→ changement de mot de passe
→ reset MFA
→ compte désactivé
→ rôle modifié
→ Session Epoch

Restauration
→ anciennes sessions refusées
→ nouvel Audit Epoch
→ nouveau Session Epoch
→ réactivation contrôlée
```

> Une session administrateur ne doit pas rester puissante uniquement parce qu’un mot de passe a été correctement saisi plusieurs heures auparavant. SiteFactory vérifie la validité actuelle de la session, exige un second facteur et redemande une preuve récente avant toute action capable de modifier durablement les accès, les données ou la sécurité.
