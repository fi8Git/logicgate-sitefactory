---
adrId: ADR-0021
title: Provision administrators through single-use invitations and controlled account recovery
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
  - ./0020-administrator-session-security-and-step-up-authentication.md
targetVersion: 0.1.0
---

# ADR-0021 — Provisionnement, invitations et récupération des comptes administrateur

## Portée publique et état d'implémentation

Le statut `accepted` et la date ci-dessous décrivent la décision architecturale
historique, pas des parcours disponibles. La distribution publique ne livre pas
d'Identity, d'interface administrateur, de provisioning d'administrateurs,
d'invitations ou de récupération de compte. Les commandes, tokens, tables et
écrans décrits sont des contrats et scénarios de référence pour leur implémentation future.

Les décisions, restrictions et preuves attendues sont conservées, notamment
l'interdiction de créer un administrateur ou un mot de passe par défaut.
Le bootstrap explicite de l'identité d'une base PostgreSQL n'est pas le bootstrap
du premier administrateur décrit ici. Aucun scénario ou commande conceptuelle
de cet ADR ne constitue une commande disponible de la CLI publique.

## 1. Statut

```text
accepted
```

La décision interdit de créer un compte administrateur possédant un mot de passe par défaut.

Le provisionnement des administrateurs repose sur :

```text
invitation nominative
+ jeton cryptographiquement aléatoire
+ usage unique
+ expiration courte
+ rôle préautorisé
+ définition du mot de passe par le destinataire
+ enrollment MFA obligatoire
+ activation atomique du compte
+ audit
```

La récupération d’un compte repose sur des mécanismes distincts selon la situation :

```text
mot de passe oublié
→ reset à usage unique
→ MFA existant toujours requis

facteur MFA perdu
→ recovery code
→ ou approbation administrative contrôlée

dernier administrateur inaccessible
→ procédure opérateur locale
→ jeton de récupération temporaire
→ aucune désactivation permanente du MFA
```

Les mécanismes suivants sont interdits :

- compte administrateur généré automatiquement au démarrage ;
- mot de passe administrateur fourni par variable d’environnement ;
- mot de passe administrateur committé dans Git ;
- mot de passe affiché dans les logs ;
- mot de passe transmis comme argument CLI ;
- invitation réutilisable ;
- invitation sans expiration ;
- reset de mot de passe permettant de contourner le MFA ;
- réinitialisation MFA uniquement avec le mot de passe courant ;
- endpoint public de bootstrap ;
- création directe d’un mot de passe par un opérateur ;
- secret de récupération dans un rapport JSON ;
- consommation d’un token par une simple requête GET ;
- réactivation silencieuse d’un token après restauration.

---

## 2. Résumé

Le premier administrateur suit le flux :

```text
migrations appliquées explicitement
→ vérification qu’aucun administrateur actif n’existe
→ commande opérateur locale
→ création d’une invitation bootstrap
→ remise sécurisée du lien
→ définition du mot de passe
→ enrollment MFA
→ génération des recovery codes
→ activation atomique du compte
→ clôture définitive du bootstrap
→ nouvelle connexion normale
```

Les administrateurs suivants suivent :

```text
administrateur autorisé
→ step-up authentication
→ choix du rôle délégable
→ création d’une invitation
→ envoi asynchrone
→ acceptation par le destinataire
→ mot de passe
→ MFA
→ activation du compte et du rôle
→ audit
```

La récupération de mot de passe suit :

```text
demande anonyme
→ réponse générique
→ rate limiting
→ création éventuelle d’un token
→ envoi asynchrone
→ vérification du token
→ nouveau mot de passe
→ MFA existant
→ révocation des sessions
→ Security Stamp modifié
→ nouvelle connexion
```

La récupération MFA suit, par ordre de préférence :

```text
1. Recovery code à usage unique

2. Réinitialisation approuvée par un autre administrateur
   avec step-up et raison

3. Procédure opérateur locale de dernier recours
   produisant un jeton de récupération à durée courte
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Aucun credential administrateur par défaut n’existe.

Une invitation ne crée pas immédiatement un compte actif.

Un rôle invité n’est actif qu’après la fin complète
du mot de passe et de l’enrollment MFA.

Un token n’est jamais stocké en clair dans PostgreSQL.

Un token n’est jamais écrit dans un log ou rapport.

Un token est lié à un client, un environnement et un purpose.

Un token est à usage unique.

Une nouvelle émission invalide la précédente.

Une requête GET ne consomme jamais un token.

Une invitation ne permet pas de choisir librement ses rôles.

Un administrateur ne peut déléguer qu’un rôle autorisé.

Un reset de mot de passe ne désactive pas le MFA.

Une récupération MFA ne crée pas une session administrateur directe.

Une récupération de compte révoque les sessions existantes.

Une restauration invalide tous les tokens de provisioning
et de récupération antérieurs.

Un opérateur ne définit jamais le mot de passe de l’utilisateur.

Une procédure break-glass reste temporaire et auditée.

Une réponse publique ne révèle pas si un email existe.
```

---

## 4. Contexte

Une application qui proposera une administration devra disposer d’au moins un administrateur pour gérer :

- les leads ;
- les contenus ;
- les médias ;
- les comptes ;
- les permissions ;
- les exports ;
- les opérations sensibles ;
- les feature flags ;
- les kill switches ;
- la piste d’audit.

Le premier administrateur pose un problème particulier.

Lors de son provisionnement :

- aucun administrateur applicatif n’existe encore ;
- aucune session applicative ne peut autoriser la création ;
- l’opération ne doit pas être exposée publiquement ;
- aucun mot de passe par défaut ne doit être créé ;
- le MFA doit être activé avant l’accès administratif.

Les comptes suivants peuvent être invités depuis l’administration, mais cette possibilité elle-même est sensible.

La récupération de compte doit également traiter plusieurs scénarios :

- oubli du mot de passe ;
- perte de l’appareil MFA ;
- épuisement des recovery codes ;
- compte verrouillé ;
- seul administrateur inaccessible ;
- email du compte modifié ;
- compromission suspectée.

Une procédure trop permissive pourrait annuler les garanties du MFA et du step-up définies dans l’ADR-0020.

---

## 5. Problème à résoudre

Comment créer et récupérer des comptes administrateur tout en garantissant :

- l’absence de credential par défaut ;
- la possession de l’adresse invitée ;
- l’activation obligatoire du MFA ;
- l’usage unique des tokens ;
- l’expiration ;
- la révocation ;
- l’absence de divulgation ;
- la résistance aux scanners d’emails ;
- l’absence d’énumération de comptes ;
- une récupération possible du dernier administrateur ;
- la traçabilité des opérations ;
- la révocation des sessions compromises ?

La solution doit également éviter :

- le bootstrap public ;
- la création directe de compte par une migration ;
- les tokens réutilisables ;
- les invitations envoyées à un rôle arbitraire ;
- les resets contournant le second facteur ;
- les réponses différentes selon l’existence du compte ;
- les recovery codes envoyés par email ;
- les liens consommés par des scanners de sécurité ;
- la présence de secrets d’enrollment dans les jobs ;
- la réactivation de tokens restaurés depuis un backup.

---

## 6. Portée

Cette décision couvre :

- premier administrateur ;
- bootstrap ;
- invitations ;
- rôles invités ;
- acceptation ;
- tokens ;
- password setup ;
- password reset ;
- MFA enrollment ;
- recovery codes ;
- MFA reset ;
- compte verrouillé ;
- changement d’email ;
- délivrance des liens ;
- jobs d’email ;
- rate limiting ;
- anti-enumeration ;
- restauration ;
- audit ;
- break-glass ;
- transfert client ;
- clôture de service ;
- tests ;
- runbooks.

---

## 7. Hors portée

Cette décision ne définit pas :

- un fournisseur d’identité externe obligatoire ;
- une preuve d’identité légale ;
- un processus RH complet ;
- la fédération SSO ;
- les passkeys comme facteur initial ;
- une récupération par SMS ;
- une récupération par question secrète ;
- une assistance téléphonique universelle ;
- les modalités juridiques d’identification de chaque client ;
- un service central LogicGate de gestion des identités ;
- un système de double approbation obligatoire pour tous les projets.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Aucun mot de passe par défaut**
2. **MFA obligatoire avant activation**
3. **Usage unique et expiration des tokens**
4. **Absence de contournement du MFA**
5. **Procédure sûre pour le premier administrateur**
6. **Récupération possible du dernier administrateur**
7. **Révocation des sessions**
8. **Anti-enumeration**
9. **Résistance aux scanners de liens**
10. **Délégation de rôles bornée**
11. **Audit**
12. **Absence de secrets dans les jobs et rapports**
13. **Compatibilité avec PostgreSQL**
14. **Autonomie du projet client**
15. **Accessibilité**
16. **Transfert client**

---

## 9. Contraintes

La décision doit respecter :

```text
ASP.NET Core Identity

PostgreSQL

MFA obligatoire en production

Registre serveur des sessions

Tokens à haute entropie

Tokens stockés sous forme de hash

Expiration courte

Antiforgery

Rate limiting

Jobs persistants pour les emails

Aucun secret dans les payloads de jobs

Audit transactionnel

Aucun endpoint public de bootstrap

Aucun secret dans Git

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- une adresse email administrateur peut recevoir les invitations ;
- un mode de remise sécurisé hors email reste nécessaire pour le bootstrap ;
- le nombre d’administrateurs reste limité ;
- l’application peut générer des tokens cryptographiquement sûrs ;
- les administrateurs privilégiés utilisent TOTP dans la baseline ;
- PostgreSQL est disponible avant le bootstrap ;
- les migrations Identity et Audit sont appliquées avant le provisionnement ;
- un opérateur disposant d’un accès Infrastructure peut exécuter une commande locale de récupération ;
- un second administrateur n’est pas toujours disponible dans les petites structures.

---

# 11. Définitions

## 11.1 Invitation

Autorisation temporaire donnée à une adresse email pour créer un compte administrateur avec un rôle défini.

---

## 11.2 Invitation bootstrap

Invitation spéciale permettant de créer le premier administrateur d’un projet.

---

## 11.3 Invitation ordinaire

Invitation créée par un administrateur déjà authentifié.

---

## 11.4 Pending account

Compte non encore autorisé à accéder à l’administration.

---

## 11.5 Activation

Transition atomique rendant le compte actif après :

- mot de passe conforme ;
- email confirmé par possession du token ;
- MFA configuré ;
- recovery codes générés ;
- invitation consommée.

---

## 11.6 Recovery token

Token à usage unique permettant une opération de récupération précise.

---

## 11.7 Token purpose

Finalité spécifique d’un token.

Exemples :

```text
admin-invitation

bootstrap-invitation

password-reset

mfa-recovery

email-change
```

---

## 11.8 Recovery Epoch

Valeur d’environnement utilisée pour invalider globalement les tokens antérieurs.

---

## 11.9 Delivery secret

Copie chiffrée et temporaire d’un token utilisée uniquement pour sa livraison asynchrone.

---

## 11.10 Acceptance context

Contexte serveur temporaire créé après ouverture du lien et avant la soumission finale.

---

# 12. Option A — Compte administrateur et mot de passe par défaut

## 12.1 Description

Le déploiement crée automatiquement :

```text
admin@example.test
+ mot de passe par défaut
```

---

## 12.2 Avantages

- mise en service immédiate ;
- procédure simple ;
- aucun email nécessaire ;
- aucun token d’invitation.

---

## 12.3 Inconvénients

- credential partagé ;
- credential prévisible ;
- stockage nécessaire ;
- risque d’oubli de changement ;
- secret dans les scripts ou variables ;
- MFA non configuré ;
- exposition lors du déploiement.

---

## 12.4 Risques

- connexion par un tiers ;
- même mot de passe sur plusieurs clients ;
- présence dans l’historique shell ;
- présence dans les logs ;
- compte actif avant remise sécurisée.

---

## 12.5 Réversibilité

```text
faible après exposition
```

---

# 13. Option B — Création directe par variables d’environnement

## 13.1 Description

L’application lit au démarrage :

```text
ADMIN_EMAIL
ADMIN_PASSWORD
```

et crée le compte.

---

## 13.2 Avantages

- automatisable ;
- simple dans Docker ;
- pas de workflow d’invitation ;
- pas d’interface supplémentaire.

---

## 13.3 Inconvénients

- credential dans l’environnement ;
- visible dans certains diagnostics ;
- recréation potentielle ;
- logique sensible au démarrage ;
- difficulté de distinguer premier déploiement et redémarrage ;
- mot de passe fourni par l’opérateur.

---

## 13.4 Risques

- compte recréé ;
- secret dans `docker inspect` ;
- credential oublié ;
- bootstrap toujours actif ;
- MFA contourné.

---

## 13.5 Réversibilité

```text
moyenne
```

---

# 14. Option C — Endpoint public de création du premier administrateur

## 14.1 Description

Tant qu’aucun administrateur n’existe, une page publique permet au premier visiteur de créer le compte.

---

## 14.2 Avantages

- expérience simple ;
- pas de CLI ;
- pas d’email obligatoire ;
- mot de passe choisi par l’utilisateur.

---

## 14.3 Inconvénients

- course au premier accès ;
- endpoint exposé ;
- dépendance au DNS et au timing ;
- découverte automatisable ;
- risque de réouverture après restauration ;
- protection complexe.

---

## 14.4 Risques

- attaquant devenant premier administrateur ;
- compte créé avant remise au client ;
- bootstrap réactivé après perte de données ;
- endpoint oublié en production.

---

## 14.5 Réversibilité

```text
faible
```

---

# 15. Option D — Invitations à usage unique et bootstrap opérateur local

## 15.1 Description

Le premier administrateur est invité par une commande locale privilégiée.

Les suivants sont invités par un administrateur autorisé.

---

## 15.2 Avantages

- aucun credential par défaut ;
- bootstrap non public ;
- MFA avant activation ;
- tokens révocables ;
- rôles bornés ;
- audit ;
- transfert ;
- récupération contrôlée ;
- aucune dépendance Identity externe.

---

## 15.3 Inconvénients

- commandes opérateur ;
- tables supplémentaires ;
- emails ;
- tokens ;
- procédures de récupération ;
- tests plus nombreux ;
- besoin d’un mode sans SMTP.

---

## 15.4 Risques

- lien transmis au mauvais destinataire ;
- token exposé ;
- invitation expirée ;
- email scanner ;
- opérateur ciblant le mauvais environnement ;
- dernier administrateur inaccessible.

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

| Critère | Poids | Mot de passe par défaut | Variables d’environnement | Bootstrap public | Invitations |
|---|---:|---:|---:|---:|---:|
| Absence de secret partagé | 5 | 1 | 2 | 4 | 5 |
| Sécurité du premier admin | 5 | 1 | 2 | 1 | 5 |
| MFA avant activation | 5 | 2 | 2 | 3 | 5 |
| Audit | 4 | 2 | 3 | 3 | 5 |
| Récupération | 5 | 2 | 2 | 2 | 5 |
| Autonomie client | 4 | 5 | 5 | 5 | 5 |
| Anti-replay | 4 | 1 | 1 | 2 | 5 |
| Transfert | 4 | 2 | 3 | 3 | 5 |
| Maintenabilité | 5 | 2 | 2 | 2 | 5 |
| Réversibilité | 4 | 2 | 3 | 2 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons que tous les comptes administrateur sont créés par :

```text
invitation
```

ou par un processus de récupération contrôlé.

Aucun administrateur actif n’est créé par :

- migration ;
- seed automatique ;
- variable d’environnement ;
- fichier de configuration ;
- démarrage de l’application ;
- endpoint public non authentifié.

---

## 17.1 Activation

Un nouveau compte administrateur devient actif uniquement après :

```text
invitation valide
+ mot de passe conforme
+ enrollment MFA réussi
+ recovery codes générés
+ consommation atomique de l’invitation
```

---

## 17.2 Nouvelle connexion

Après activation, l’utilisateur doit effectuer une connexion administrative normale.

L’acceptation de l’invitation ne crée pas directement une session durable.

---

# 18. État de provisioning

Le projet conserve un état de provisioning.

Valeurs conceptuelles :

```text
not-started

bootstrap-invitation-pending

bootstrap-completed
```

---

## 18.1 `not-started`

Aucun premier administrateur actif ni invitation bootstrap valide.

---

## 18.2 `bootstrap-invitation-pending`

Une invitation bootstrap non expirée existe.

---

## 18.3 `bootstrap-completed`

Au moins un premier administrateur a terminé son activation.

---

## 18.4 Irréversibilité logique

Une fois `bootstrap-completed`, le bootstrap ordinaire ne peut pas être réactivé.

Une récupération utilise un workflow distinct.

---

# 19. Aucun endpoint public de bootstrap

L’application web ne fournit pas une route permettant de créer le premier administrateur en fonction de l’absence actuelle de comptes.

---

## 19.1 Diagnostic

Un diagnostic opérateur protégé peut indiquer :

```text
initial administrator required
```

---

## 19.2 Page de login

La page de login ne doit pas révéler publiquement si le bootstrap est terminé.

---

# 20. Commande bootstrap

Commande conceptuelle :

```text
sitefactory admin bootstrap plan

sitefactory admin bootstrap apply
```

---

## 20.1 Paramètres

Le plan identifie explicitement :

```text
Client

Environment

Target database

Administrator email

Role template

Delivery mode

Expiration
```

---

## 20.2 Confirmation

L’application exige une confirmation renforcée indiquant :

```text
client
environnement
email
rôle
```

---

## 20.3 Accès

La commande est exécutée :

- localement sur un poste opérateur autorisé ;
- ou sur le VPS via SSH contrôlé ;
- avec un credential PostgreSQL opérateur adapté.

---

## 20.4 Réseau public

La commande n’expose aucun endpoint Internet supplémentaire.

---

# 21. Préconditions bootstrap

Avant création :

```text
[ ] Client confirmé
[ ] Environnement confirmé
[ ] PostgreSQL accessible
[ ] Migrations appliquées
[ ] Schéma Identity disponible
[ ] Schéma Audit disponible
[ ] Aucun administrateur actif
[ ] Aucune invitation bootstrap valide
[ ] Recovery Epoch courant
[ ] Adresse email validée
[ ] Mode de livraison disponible
```

---

## 21.1 Administrateur existant

Si un administrateur actif existe :

```text
bootstrap refused
```

---

## 21.2 Invitation pending

Une invitation existante doit être :

- utilisée ;
- révoquée ;
- ou renouvelée explicitement.

---

# 22. Rôle du premier administrateur

Le premier administrateur reçoit un rôle stable approuvé.

Exemple conceptuel :

```text
site-administrator
```

---

## 22.1 Permissions

Les permissions du rôle sont définies par la configuration Identity et non dans le token.

---

## 22.2 Rôle arbitraire

La commande bootstrap ne reçoit pas une liste libre de permissions.

---

## 22.3 Break-glass

Le premier administrateur n’est pas automatiquement un compte break-glass.

---

# 23. Modes de remise bootstrap

Modes autorisés :

```text
email

protected-file
```

---

## 23.1 Email

Utilisé lorsque SMTP est configuré et validé.

---

## 23.2 Protected file

Utilisé lorsque l’email n’est pas encore disponible.

Le lien est écrit dans un fichier :

- explicitement fourni ;
- hors Git ;
- avec permissions restrictives ;
- sans copie dans le rapport ;
- supprimé après remise.

---

## 23.3 Sortie standard

Le token ne doit pas être affiché sur la sortie standard par défaut.

---

## 23.4 Presse-papiers

La copie automatique vers le presse-papiers n’est pas la baseline.

---

# 24. Fichier protégé

Sur Linux, la cible utilise une permission conceptuelle :

```text
0600
```

---

## 24.1 Création atomique

Le fichier doit être créé sans écraser silencieusement un fichier existant.

---

## 24.2 Windows

Sous Windows, une ACL restreinte à l’utilisateur opérateur est utilisée lorsque l’outil le permet.

---

## 24.3 Rapport

Le rapport conserve :

```text
delivery mode = protected-file
output path redacted or relative
```

mais jamais le contenu.

---

# 25. Invitations administrateur ordinaires

Une invitation ordinaire nécessite une permission dédiée.

Exemple conceptuel :

```text
Identity.Administrators.Invite
```

---

## 25.1 Step-up

La création nécessite une authentification renforcée.

---

## 25.2 Raison

Une raison est obligatoire lorsque le rôle invité est privilégié.

---

## 25.3 Audit

Les actions suivantes sont auditées :

```text
identity.invitation.created

identity.invitation.revoked

identity.invitation.resent

identity.invitation.accepted
```

---

# 26. Rôles délégables

Un administrateur ne peut inviter que vers des rôles explicitement délégables.

---

## 26.1 Règle

Le rôle cible doit appartenir à :

```text
DelegableRoleIds
```

du rôle ou de la permission de l’invitant.

---

## 26.2 Auto-élévation indirecte

Un administrateur ne peut pas inviter un complice vers un rôle qu’il ne pourrait pas attribuer directement.

---

## 26.3 Rôles non invitables

Les rôles suivants peuvent nécessiter une procédure distincte :

- break-glass ;
- Audit Retention Operator ;
- Database Operator ;
- Recovery Operator.

---

# 27. Adresse email

L’invitation est liée à une adresse email normalisée.

---

## 27.1 Possession

La possession du lien confirme l’accès à l’adresse invitée.

---

## 27.2 Changement pendant l’acceptation

Le destinataire ne peut pas remplacer librement l’adresse email.

---

## 27.3 Adresse déjà utilisée

Une invitation vers une adresse déjà active est refusée.

Les changements de rôle d’un utilisateur existant utilisent le workflow d’autorisation de l’ADR-0020.

---

## 27.4 Adresse pending

Une seule invitation active par adresse et contexte de rôle est autorisée dans la baseline.

---

# 28. Modèle d’invitation

Une invitation doit pouvoir conserver :

```text
InvitationId

InvitationType

EmailNormalized

RoleTemplateId

CreatedAtUtc

ExpiresAtUtc

CreatedByActorId

Status

ConsumedAtUtc

RevokedAtUtc

RevocationReasonCode

TokenId

TokenHash

RecoveryEpoch

SecurityStampRequirement

DeliveryStatus

DeliveryAttemptCount
```

---

## 28.1 Statuts

Valeurs recommandées :

```text
pending

delivered

accepted

expired

revoked

delivery-failed
```

---

## 28.2 Données interdites

La table ne contient pas :

- mot de passe ;
- secret TOTP ;
- recovery codes ;
- token brut ;
- credential SMTP.

---

# 29. Format des tokens

Le token utilise une structure conceptuelle :

```text
{TokenId}.{TokenSecret}
```

---

## 29.1 Token ID

Identifiant non secret permettant de retrouver l’enregistrement.

---

## 29.2 Token Secret

Valeur aléatoire d’au moins :

```text
256 bits
```

---

## 29.3 Encodage

Le secret utilise un encodage URL-safe, par exemple Base64URL.

---

## 29.4 Longueur

La longueur maximale acceptée par le parser est bornée.

---

# 30. Stockage des tokens

Seul le digest du secret est conservé pour la validation.

Exemple conceptuel :

```text
SHA-256(TokenSecret)
```

---

## 30.1 Haute entropie

La haute entropie du token empêche une attaque réaliste par dictionnaire sur le hash.

---

## 30.2 Comparaison

La comparaison utilise un mécanisme résistant aux différences temporelles évitables.

---

## 30.3 Token brut

Le token brut n’est conservé que temporairement et chiffré lorsqu’une livraison asynchrone est nécessaire.

---

# 31. Delivery secret

L’envoi asynchrone nécessite que le worker puisse construire le lien.

---

## 31.1 Stockage spécialisé

Une copie chiffrée temporaire du token peut être conservée dans un store dédié :

```text
sitefactory_identity.token_delivery_secrets
```

---

## 31.2 Protection

La valeur est :

- chiffrée ;
- liée au Token ID et au purpose ;
- soumise à une expiration ;
- inaccessible aux requêtes ordinaires ;
- jamais ajoutée aux rapports.

---

## 31.3 Payload du job

Le job contient uniquement :

```text
InvitationId
```

ou :

```text
RecoveryRequestId
```

Il ne contient pas le token.

---

## 31.4 Nettoyage

Le Delivery Secret est supprimé après :

- consommation ;
- révocation ;
- expiration ;
- remplacement ;
- fin de sa nécessité de livraison.

---

# 32. Email d’invitation

L’email contient :

- la finalité ;
- le client ou site concerné ;
- l’expiration ;
- le lien d’acceptation ;
- une indication de sécurité ;
- un moyen de signaler une invitation inattendue.

---

## 32.1 Contenu interdit

Il ne contient pas :

- mot de passe ;
- recovery codes ;
- secret TOTP ;
- liste complète des permissions ;
- donnée client confidentielle inutile.

---

## 32.2 Expéditeur

L’expéditeur provient de la configuration approuvée.

---

# 33. Job d’envoi

L’envoi suit l’ADR-0017.

---

## 33.1 Idempotence

Une même invitation peut être envoyée plusieurs fois avec le même token en cas de réponse SMTP ambiguë.

Cela ne crée pas plusieurs invitations.

---

## 33.2 Resend explicite

Une opération « Renvoyer » :

- révoque l’ancien token ;
- génère un nouveau token ;
- crée une nouvelle expiration ;
- envoie un nouveau lien ;
- audite l’opération.

---

## 33.3 Retry

Les retries SMTP sont bornés.

---

# 34. Expiration des invitations

Valeurs initiales :

```text
Invitation bootstrap :
60 minutes

Invitation administrateur ordinaire :
24 heures
```

---

## 34.1 Configuration

Ces durées peuvent être raccourcies par un profil de sécurité.

---

## 34.2 Maximum

Une invitation administrateur ne doit pas rester valide indéfiniment.

---

## 34.3 Expiration

Une invitation expirée ne peut pas être réactivée.

Une nouvelle invitation est requise.

---

# 35. Pages contenant un token

Les pages d’invitation et récupération utilisent :

```text
Cache-Control: no-store

Pragma: no-cache

Referrer-Policy: no-referrer
```

---

## 35.1 Ressources tierces

Elles ne chargent pas :

- analytics ;
- pixels ;
- scripts publicitaires ;
- ressources tierces non nécessaires.

---

## 35.2 Query string

L’infrastructure d’observabilité et Caddy doivent éviter de journaliser la query string contenant le token.

---

## 35.3 Historique navigateur

Après validation initiale, l’application redirige vers une URL propre sans token.

---

# 36. Résistance aux scanners de liens

Les scanners d’emails peuvent ouvrir automatiquement le lien.

---

## 36.1 Invariant

Une requête GET ne consomme pas le token et n’active pas le compte.

---

## 36.2 GET initial

Le serveur peut :

- valider sommairement le token ;
- créer un Acceptance Context temporaire ;
- définir un cookie HttpOnly temporaire ;
- rediriger vers une URL propre.

---

## 36.3 Mutation

La consommation du token nécessite un POST :

- avec antiforgery ;
- avec données d’enrollment ;
- avec validation finale ;
- dans une transaction.

---

## 36.4 Plusieurs GET

Plusieurs ouvertures du lien ne doivent pas invalider l’invitation.

---

# 37. Acceptance Context

Un contexte temporaire est lié à :

```text
TokenId

InvitationId

Purpose

Client

Environment

CreatedAtUtc

ExpiresAtUtc

BrowserNonce
```

---

## 37.1 Durée

Baseline :

```text
15 minutes
```

---

## 37.2 Cookie

Le cookie temporaire est :

- HttpOnly ;
- Secure en production ;
- SameSite adapté ;
- sans privilège administrateur.

---

## 37.3 Limite

Le contexte ne constitue pas une session d’administration.

---

## 37.4 Expiration

L’utilisateur peut rouvrir le lien tant que l’invitation reste valide.

---

# 38. Acceptation d’invitation

Le workflow est :

```text
token valide
→ Acceptance Context
→ mot de passe
→ confirmation du mot de passe
→ enrollment TOTP
→ vérification du premier code
→ génération des recovery codes
→ confirmation finale
→ transaction d’activation
```

---

## 38.1 Antiforgery

Chaque POST du parcours est protégé.

---

## 38.2 Rate limiting

Les tentatives sont limitées par :

- source ;
- Token ID ;
- Acceptance Context.

---

## 38.3 Token invalide

Le message reste générique.

---

# 39. Mot de passe initial

Le mot de passe respecte l’ADR-0020.

Baseline :

```text
minimum 14 caractères
espaces autorisés
Unicode autorisé
mot de passe commun refusé
```

---

## 39.1 Opérateur

L’opérateur ne connaît ni ne choisit le mot de passe.

---

## 39.2 Email

Le mot de passe n’est jamais envoyé par email.

---

## 39.3 Gestionnaire de mots de passe

Le formulaire est compatible avec les gestionnaires de mots de passe et le collage.

---

# 40. MFA avant activation

Un compte administrateur privilégié ne peut pas terminer l’invitation sans MFA.

---

## 40.1 Enrollment

Le secret TOTP est créé dans le contexte d’activation.

---

## 40.2 Premier code

L’enrollment n’est valide qu’après saisie d’un code correct.

---

## 40.3 Échec

En cas d’abandon, le compte reste inactif.

---

## 40.4 Recovery codes

Les recovery codes sont générés après validation du facteur.

---

# 41. Activation atomique

La transaction finale réalise :

```text
création ou finalisation du compte Identity
→ hash du mot de passe
→ enregistrement du MFA
→ création des recovery codes protégés
→ attribution du rôle prévu
→ activation du compte
→ consommation du token
→ clôture de l’invitation
→ événement d’audit
→ commit
```

---

## 41.1 Échec

Si une étape échoue :

```text
aucun compte administrateur actif
```

---

## 41.2 Bootstrap

Pour le premier administrateur, la transaction passe également le provisioning à :

```text
bootstrap-completed
```

---

# 42. Après activation

Le système :

- supprime le Delivery Secret ;
- invalide les Acceptance Contexts ;
- affiche les recovery codes une seule fois ;
- redirige vers la page de connexion ;
- notifie éventuellement l’invitant ;
- audite l’activation.

---

## 42.1 Session

Aucune session administrateur durable n’est créée automatiquement.

---

# 43. Annulation d’une invitation

Une invitation pending peut être révoquée.

---

## 43.1 Permission

La révocation nécessite une permission Identity adaptée.

---

## 43.2 Step-up

La révocation d’une invitation privilégiée nécessite un step-up.

---

## 43.3 Effets

- statut revoked ;
- token invalide ;
- Delivery Secret supprimé ;
- Acceptance Contexts invalidés ;
- audit.

---

# 44. Suppression d’un compte pending

Un compte pending ne doit pas rester indéfiniment.

---

## 44.1 Nettoyage

Un job peut nettoyer les invitations expirées et les comptes pending associés.

---

## 44.2 Rétention

Le nettoyage conserve les événements d’audit nécessaires.

---

## 44.3 Compte partiellement configuré

Les secrets MFA incomplets sont supprimés.

---

# 45. Demande de reset de mot de passe

L’endpoint public reçoit une adresse email.

---

## 45.1 Réponse

La réponse est toujours générique.

Exemple :

```text
Si un compte correspondant existe,
un message de récupération sera envoyé.
```

---

## 45.2 Anti-enumeration

La réponse ne révèle pas :

- compte absent ;
- compte désactivé ;
- compte sans MFA ;
- email non confirmé ;
- compte verrouillé.

---

## 45.3 Rate limiting

La demande est limitée par :

- source ;
- fingerprint de l’adresse normalisée ;
- endpoint ;
- limite globale.

---

# 46. Fingerprint de compte

L’adresse normalisée peut être transformée par HMAC pour le rate limiting.

---

## 46.1 Donnée brute

L’email ne doit pas être utilisé comme label métrique.

---

## 46.2 Logs

L’email complet n’est pas journalisé dans les événements techniques de demande.

---

# 47. Création du reset

Si le compte est éligible :

- les anciens resets actifs sont révoqués ;
- un nouveau token est généré ;
- l’expiration est définie ;
- un Delivery Secret est créé ;
- un job d’email est persisté.

---

## 47.1 Compte inexistant

Le serveur peut exécuter un travail factice borné afin de réduire les différences temporelles.

---

## 47.2 Compte désactivé

Aucun reset ordinaire ne réactive automatiquement un compte désactivé.

---

# 48. Expiration du reset

Baseline :

```text
30 minutes
```

---

## 48.1 Usage unique

Le reset est consommé une seule fois.

---

## 48.2 Nouvelle demande

Une nouvelle demande invalide le token précédent.

---

# 49. Validation du reset

Le lien utilise le même principe que les invitations :

```text
GET non mutating
→ Acceptance Context
→ redirection propre
→ POST antiforgery
```

---

## 49.1 Scanner email

L’ouverture automatique du lien ne réinitialise pas le mot de passe.

---

# 50. Reset et MFA

Un reset de mot de passe ne contourne pas le MFA.

---

## 50.1 Compte avec MFA

Après définition du nouveau mot de passe, l’utilisateur doit valider :

- son TOTP ;
- ou un recovery code.

---

## 50.2 MFA indisponible

Le parcours s’arrête et oriente vers la récupération MFA.

---

## 50.3 Aucun fallback email

Le simple contrôle de l’email ne désactive pas le second facteur.

---

# 51. Finalisation du reset

La transaction réalise :

```text
nouveau hash de mot de passe
→ Security Stamp modifié
→ tokens de reset révoqués
→ sessions révoquées
→ step-up grants révoqués
→ lockout réinitialisé lorsque autorisé
→ événement d’audit
→ commit
```

---

## 51.1 Connexion

Une nouvelle connexion complète est requise.

---

## 51.2 Notification de sécurité

Un message de confirmation peut être envoyé après le reset.

Il ne contient aucun credential.

---

# 52. Lockout et reset

Une demande de reset ne déverrouille pas immédiatement le compte.

---

## 52.1 Succès complet

Un reset finalisé avec MFA peut remettre à zéro le compteur de lockout.

---

## 52.2 Compte désactivé

La désactivation administrative reste distincte du lockout.

---

# 53. Recovery codes

Les recovery codes constituent le premier mécanisme de récupération MFA.

---

## 53.1 Usage unique

Chaque code ne peut être utilisé qu’une fois.

---

## 53.2 Après utilisation

Le système :

- audite l’usage ;
- notifie l’utilisateur ;
- recommande de régénérer les codes ;
- peut exiger un nouveau step-up.

---

## 53.3 Stockage

Les recovery codes sont protégés par ASP.NET Core Identity ou une stratégie équivalente.

---

# 54. Perte du facteur MFA

Si l’utilisateur ne possède plus :

- son appareil TOTP ;
- ni recovery code ;

il doit utiliser une récupération administrative.

---

## 54.1 Interdiction

Le mot de passe et l’accès à l’email ne suffisent pas à désactiver le MFA d’un compte privilégié.

---

# 55. Réinitialisation MFA par un autre administrateur

Le workflow nécessite :

```text
permission dédiée
+ step-up critique
+ raison obligatoire
+ cible explicite
+ confirmation renforcée
+ audit
```

---

## 55.1 Permission conceptuelle

```text
Identity.MfaRecoveryApprove
```

---

## 55.2 Auto-récupération

Un administrateur ne peut pas approuver lui-même sa propre réinitialisation MFA par ce workflow.

---

## 55.3 Effet

L’opération ne désactive pas immédiatement le MFA et n’ouvre pas une session.

Elle crée une demande de récupération temporaire.

---

# 56. Invitation de récupération MFA

La demande crée un token :

```text
mfa-recovery
```

---

## 56.1 Expiration

Baseline :

```text
30 minutes
```

---

## 56.2 Finalisation

Le destinataire doit :

- définir ou confirmer son mot de passe ;
- enregistrer un nouveau facteur MFA ;
- générer de nouveaux recovery codes.

---

## 56.3 Effets

Après succès :

- ancien facteur révoqué ;
- anciens recovery codes révoqués ;
- toutes les sessions révoquées ;
- Security Stamp modifié ;
- audit.

---

# 57. Dernier administrateur inaccessible

Un projet peut ne posséder qu’un seul administrateur.

---

## 57.1 Procédure

La récupération utilise une commande locale distincte :

```text
sitefactory admin recovery plan

sitefactory admin recovery apply
```

---

## 57.2 Conditions

- accès Infrastructure autorisé ;
- client explicite ;
- environnement explicite ;
- utilisateur cible explicite ;
- raison obligatoire ;
- aucune autre récupération simple disponible ;
- confirmation renforcée ;
- rapport ;
- audit lorsque PostgreSQL est disponible.

---

## 57.3 Résultat

La commande crée une invitation de récupération temporaire.

Elle ne :

- définit pas le mot de passe ;
- désactive pas définitivement le MFA ;
- crée pas de session ;
- attribue pas un nouveau rôle ;
- affiche pas le token dans le rapport.

---

# 58. Identification de la cible opérateur

L’opérateur peut rechercher le compte par :

- User ID ;
- email normalisé.

---

## 58.1 Confirmation

Avant application, la commande affiche :

```text
User ID

email masqué

statut

rôles

MFA configuré

nombre de sessions
```

---

## 58.2 Pas de cible implicite

La commande ne sélectionne pas automatiquement « le premier administrateur ».

---

# 59. Livraison de la récupération opérateur

Modes :

```text
email

protected-file
```

---

## 59.1 Email compromis

Si l’adresse email est suspectée compromise, le mode email ne doit pas être utilisé sans analyse supplémentaire.

---

## 59.2 Protected file

Le lien est remis par un canal distinct et sécurisé.

---

## 59.3 Canal

Le canal de remise doit être documenté dans le rapport sans contenir la valeur.

---

# 60. Break-glass et récupération

La récupération opérateur peut être déclenchée dans un contexte break-glass.

---

## 60.1 Obligations

- raison ;
- rapport externe si nécessaire ;
- durée courte ;
- alerte ;
- revue après incident ;
- rotation des credentials opérateur ;
- audit de réconciliation.

---

## 60.2 Pas de bypass permanent

Le workflow ne crée pas un compte administrateur secondaire permanent à l’insu du client.

---

# 61. Compte sans autre administrateur

Après récupération, le client doit être invité à :

- vérifier ses sessions ;
- vérifier les rôles ;
- générer de nouveaux recovery codes ;
- considérer l’ajout d’un second administrateur ;
- vérifier la piste d’audit.

---

# 62. Changement d’adresse email

L’adresse email administrateur constitue un élément de récupération sensible.

---

## 62.1 Exigences

Le changement nécessite :

- session active ;
- step-up critique ;
- mot de passe actuel ;
- MFA récent ;
- nouvelle adresse ;
- token de confirmation ;
- confirmation finale ;
- audit.

---

## 62.2 Ancienne adresse

Une notification de sécurité est envoyée à l’ancienne adresse lorsque possible.

---

## 62.3 Activation

La nouvelle adresse n’est active qu’après confirmation.

---

## 62.4 Sessions

Le changement final :

- modifie le Security Stamp ;
- révoque les autres sessions ;
- peut conserver ou révoquer la session courante selon le profil ;
- invalide les anciens tokens.

---

# 63. Token de changement d’email

Le token :

- est lié à l’utilisateur ;
- est lié à la nouvelle adresse ;
- possède un purpose distinct ;
- expire après 30 minutes dans la baseline ;
- est à usage unique ;
- est invalidé par une nouvelle demande.

---

# 64. Recovery Epoch

L’environnement possède une valeur :

```text
RecoveryEpoch
```

---

## 64.1 Usage

Les tokens de :

- bootstrap ;
- invitation ;
- reset ;
- MFA recovery ;
- email change ;

sont liés à l’epoch courant.

---

## 64.2 Changement

Un nouvel epoch est créé après :

- restauration ;
- compromission globale ;
- fuite potentielle de tokens ;
- changement majeur du système de récupération.

---

## 64.3 Effet

Tous les tokens antérieurs sont invalidés.

---

## 64.4 Audit

Le changement est une action critique auditée.

---

# 65. Restauration

Une restauration peut réintroduire :

- invitations utilisées ;
- tokens révoqués ;
- resets expirés ;
- demandes MFA anciennes ;
- comptes pending.

---

## 65.1 Baseline

Après restauration :

```text
nouveau Session Epoch
+ nouveau Recovery Epoch
```

---

## 65.2 Conséquence

Tous les liens de provisioning et récupération antérieurs deviennent invalides.

---

## 65.3 Reprise

Les invitations nécessaires sont réémises explicitement.

---

## 65.4 Worker

Les jobs d’email liés à d’anciens tokens doivent être annulés ou rendus non exécutables.

---

# 66. Tokens et Security Stamp

Les tokens de récupération utilisateur peuvent également être liés au Security Stamp courant.

---

## 66.1 Changement de sécurité

Un changement de :

- mot de passe ;
- MFA ;
- email ;
- rôle privilégié ;
- compte actif ;

peut invalider les tokens en cours.

---

## 66.2 Invitation bootstrap

L’invitation initiale utilise principalement le Recovery Epoch et son propre état.

---

# 67. Token purpose binding

Un token ne peut servir qu’à son purpose.

---

## 67.1 Exemples interdits

Un token :

```text
password-reset
```

ne peut pas servir à :

```text
mfa-recovery
```

Un token :

```text
admin-invitation
```

ne peut pas servir à :

```text
email-change
```

---

# 68. Client et environnement

Le token est lié à :

```text
ClientSlug

Environment
```

---

## 68.1 Staging

Un token staging est refusé en production.

---

## 68.2 Autre client

Un token d’un autre client est refusé.

---

## 68.3 URL

Le host attendu peut également être vérifié.

---

# 69. URL de récupération

L’URL doit provenir d’une configuration publique approuvée.

---

## 69.1 Host header

L’application ne construit pas le lien uniquement à partir d’un Host Header non validé.

---

## 69.2 Configuration

Utiliser :

```text
PublicBaseUrl
```

validée par environnement.

---

## 69.3 Injection

Un attaquant ne doit pas pouvoir provoquer l’envoi d’un lien vers son propre domaine.

---

# 70. Redirects

Après validation d’un token, seules des destinations locales et allowlistées sont autorisées.

---

## 70.1 Query

Le token ne doit pas être propagé vers une autre page.

---

## 70.2 Fragment

Aucun credential ne doit être copié dans une URL de destination non maîtrisée.

---

# 71. Rate limiting des parcours

Les endpoints suivants sont limités :

- invitation accept ;
- password reset request ;
- password reset complete ;
- MFA recovery ;
- email change ;
- bootstrap validation ;
- recovery operator finalization.

---

## 71.1 Partitions

- source réseau pseudonymisée ;
- Token ID ;
- Acceptance Context ;
- compte pseudonymisé ;
- limite globale.

---

## 71.2 Token brute force

Un Token ID valide ne doit pas permettre des tentatives illimitées sur le secret.

---

# 72. Tentatives invalides

Après un nombre excessif d’échecs sur un token :

- le token peut être révoqué ;
- un événement de sécurité est produit ;
- une nouvelle émission peut être requise.

---

## 72.1 DoS

La policy doit limiter la capacité d’un attaquant à révoquer un token légitime uniquement en connaissant son Token ID.

---

## 72.2 Secret

Le Token ID seul ne suffit pas à cibler une invitation non connue.

---

# 73. Réponses publiques

Les réponses doivent rester génériques.

---

## 73.1 Invitation invalide

```text
Ce lien est invalide ou a expiré.
```

---

## 73.2 Reset demandé

```text
Si le compte existe, un message sera envoyé.
```

---

## 73.3 Compte désactivé

Le parcours public ne révèle pas la désactivation.

---

## 73.4 Détails internes

Les Reason Codes restent côté serveur.

---

# 74. Notifications de sécurité

Les notifications suivantes sont recommandées :

- invitation créée ;
- invitation acceptée ;
- mot de passe modifié ;
- reset finalisé ;
- MFA modifié ;
- recovery code utilisé ;
- adresse email modifiée ;
- récupération opérateur déclenchée ;
- sessions révoquées.

---

## 74.1 Contenu

Elles ne contiennent aucun secret.

---

## 74.2 Canal

Le canal doit être configuré et approuvé.

---

# 75. Audit

Événements recommandés :

```text
identity.bootstrap-invitation.created

identity.bootstrap.completed

identity.invitation.created

identity.invitation.resent

identity.invitation.revoked

identity.invitation.accepted

identity.password-reset.completed

identity.mfa-recovery.requested

identity.mfa-recovery.completed

identity.email-change.requested

identity.email-change.completed

identity.recovery-operator.invoked

identity.recovery-epoch.changed
```

---

## 75.1 Demande anonyme de reset

Chaque demande anonyme n’est pas nécessairement inscrite dans la piste principale.

Elle appartient aux métriques et événements de sécurité.

---

## 75.2 Finalisation

Une finalisation réussie est auditée.

---

## 75.3 Données interdites

L’audit ne contient pas :

- token ;
- password ;
- MFA secret ;
- recovery code ;
- lien complet ;
- email complet lorsque non nécessaire.

---

# 76. Observabilité

Événements techniques recommandés :

```text
AdministratorInvitationCreated

AdministratorInvitationDeliveryStarted

AdministratorInvitationDelivered

AdministratorInvitationDeliveryFailed

AdministratorInvitationAccepted

PasswordResetRequested

PasswordResetCompleted

MfaRecoveryStarted

MfaRecoveryCompleted

AdministratorRecoveryOperatorInvoked

RecoveryTokenRejected

RecoveryEpochChanged
```

---

## 76.1 Propriétés autorisées

```text
InvitationType

TokenPurpose

RoleTemplateId

DeliveryMode

Result

ErrorCode

Environment

ClientSlug

Duration
```

---

## 76.2 Données interdites

```text
Token

DeliverySecret

Password

MfaSecret

RecoveryCode

Email complet dans les métriques

Lien d’acceptation
```

---

# 77. Métriques

Exemples :

```text
administrator invitations created

administrator invitations accepted

administrator invitations expired

administrator invitation delivery failures

password reset requests

password reset completions

MFA recovery requests

MFA recovery completions

operator recovery invocations

invalid recovery tokens
```

---

## 77.1 Labels autorisés

```text
invitation_type

token_purpose

delivery_mode

result

environment
```

---

## 77.2 Labels interdits

```text
Email

UserId

InvitationId

TokenId

IP

TraceId

CorrelationId
```

---

# 78. Alertes

Alertes possibles :

```text
Récupérations opérateur répétées

Échecs de livraison d’invitations

Nombre élevé de resets

Tokens invalides en forte hausse

Compte privilégié pending trop longtemps

Compte privilégié sans MFA

Recovery Epoch modifié

Invitations bootstrap répétées

Dernier administrateur désactivé
```

---

## 78.1 Runbook

Les alertes critiques référencent une procédure.

---

# 79. Expiration et nettoyage

Un job récurrent traite :

- invitations expirées ;
- tokens expirés ;
- Delivery Secrets ;
- Acceptance Contexts ;
- comptes pending abandonnés ;
- anciens états de récupération.

---

## 79.1 Bornes

Le nettoyage est :

- idempotent ;
- observable ;
- borné ;
- sans suppression de compte actif.

---

## 79.2 Audit

L’expiration ordinaire de chaque token ne nécessite pas forcément un événement d’audit individuel.

Une métrique agrégée peut suffire.

---

# 80. Rétention

Les tokens consommés ou expirés ne sont pas conservés indéfiniment.

---

## 80.1 Données minimales

Une trace historique peut conserver :

- Invitation ID ;
- purpose ;
- dates ;
- statut ;
- acteur ;
- rôle.

---

## 80.2 Hash

Le Token Hash peut être supprimé après expiration et délai opérationnel.

---

## 80.3 Delivery Secret

Il utilise la rétention la plus courte.

---

# 81. Pending accounts

Un compte pending ne possède pas :

- session active ;
- accès administrateur ;
- rôle effectif ;
- permission ;
- entitlement propre.

---

## 81.1 Unicité

L’adresse email reste réservée pendant la durée de l’invitation.

---

## 81.2 Expiration

Après nettoyage, une nouvelle invitation peut être créée.

---

# 82. Invitations concurrentes

Deux invitations concurrentes vers la même adresse ne doivent pas activer deux comptes.

---

## 82.1 Contraintes

Utiliser des contraintes PostgreSQL adaptées sur :

- email normalisé ;
- statut ;
- client ;
- environnement.

---

## 82.2 Race d’acceptation

La consommation atomique du token garantit une seule activation.

---

# 83. Resend concurrent

Deux demandes de renvoi concurrentes ne doivent pas laisser deux tokens actifs.

---

## 83.1 Transaction

```text
révoquer l’ancien
→ créer le nouveau
→ commit
```

---

# 84. Compte actif pendant une invitation

Si un compte actif est créé par un autre workflow avant l’acceptation :

```text
invitation rejected
```

---

## 84.1 Pas de merge implicite

L’invitation ne fusionne pas silencieusement ses rôles avec le compte.

---

# 85. Protection contre le mass assignment

Les payloads d’acceptation ne peuvent pas définir :

- rôle ;
- permission ;
- compte actif ;
- MFA required ;
- email cible ;
- Client ID ;
- Recovery Epoch ;
- Security Stamp.

---

## 85.1 Source

Ces valeurs proviennent du record d’invitation validé.

---

# 86. Protection des pages de récupération

Les pages utilisent :

- HTTPS ;
- antiforgery ;
- no-store ;
- no-referrer ;
- rate limiting ;
- CSP adaptée ;
- aucun script tiers non nécessaire.

---

## 86.1 Indexation

Elles ne doivent pas être indexées par les moteurs de recherche.

---

# 87. Session et récupération

Une Acceptance Context n’est pas une session administrateur.

---

## 87.1 Compte déjà connecté

Un utilisateur connecté ne doit pas pouvoir appliquer par erreur le token d’une autre identité.

---

## 87.2 Isolation

Le parcours peut exiger la déconnexion ou utiliser un contexte navigateur séparé.

---

## 87.3 Existing session

Une session administrative existante n’augmente pas la portée du token.

---

# 88. Récupération du dernier administrateur supprimé

Si aucun compte privilégié actif n’existe, l’opérateur ne doit pas restaurer directement un ancien rôle par SQL.

---

## 88.1 Procédure

Utiliser :

- récupération d’un compte désactivé lorsque son identité est valide ;
- ou invitation bootstrap de récupération exceptionnelle selon un plan validé.

---

## 88.2 Nouveau premier administrateur

La création d’un nouvel administrateur après perte complète des comptes constitue une action break-glass critique.

Elle ne réactive pas le bootstrap ordinaire.

---

# 89. Désactivation du dernier administrateur

L’application doit empêcher ou renforcer la désactivation du dernier administrateur actif.

---

## 89.1 Contrôle

Avant l’action, vérifier qu’un autre compte :

- actif ;
- MFA configuré ;
- rôle suffisant ;

restera disponible.

---

## 89.2 Exception

Une clôture de service ou procédure break-glass peut déroger avec :

- raison ;
- confirmation renforcée ;
- plan de récupération ;
- audit.

---

# 90. Suppression du dernier administrateur

La suppression ordinaire est interdite tant qu’aucun successeur actif et sécurisé n’existe.

---

# 91. Account recovery et compromission

Une récupération ne doit pas rétablir automatiquement toutes les permissions si le compte est suspecté compromis.

---

## 91.1 Mode de sécurité

Le compte peut être réactivé avec :

- sessions révoquées ;
- MFA renouvelé ;
- mot de passe renouvelé ;
- rôles revus ;
- permissions temporairement réduites.

---

## 91.2 Raison

L’incident est documenté dans l’audit et le runbook.

---

# 92. Email compromis

Si l’adresse email est compromise :

- ne pas utiliser le reset email ordinaire ;
- utiliser un autre administrateur ou la procédure opérateur ;
- modifier l’adresse après authentification forte ;
- révoquer les tokens ;
- révoquer les sessions ;
- vérifier les rôles ;
- notifier par un canal alternatif lorsque possible.

---

# 93. Mot de passe et MFA compromis

Si les deux facteurs sont suspectés compromis :

```text
désactiver le compte
→ révoquer les sessions
→ modifier le Security Stamp
→ révoquer les tokens
→ lancer une récupération opérateur
→ renouveler mot de passe et MFA
→ revoir l’audit
```

---

# 94. Break-glass account

La création d’un compte break-glass n’est pas automatique.

---

## 94.1 Existence

Lorsqu’un projet en possède un, il suit l’ADR-0020 :

- usage exceptionnel ;
- MFA lorsque possible ;
- session courte ;
- alerte ;
- rotation après usage.

---

## 94.2 Invitation

Un rôle break-glass n’est pas attribué par une invitation ordinaire.

---

# 95. Configuration

Les paramètres sont typés et validés.

Exemple conceptuel :

```text
AdministratorProvisioning:
  BootstrapInvitationLifetime
  InvitationLifetime
  PasswordResetLifetime
  MfaRecoveryLifetime
  AcceptanceContextLifetime
  MaximumPendingInvitations
  DeliveryModes
  MfaRequired
```

---

## 95.1 Production

La configuration de production ne peut pas :

- désactiver l’expiration ;
- autoriser un token réutilisable ;
- désactiver le MFA des rôles privilégiés ;
- activer un bootstrap public ;
- stocker les tokens en clair.

---

# 96. Valeurs initiales

Baseline :

```text
Bootstrap invitation lifetime :
60 minutes

Regular invitation lifetime :
24 hours

Password reset lifetime :
30 minutes

MFA recovery lifetime :
30 minutes

Email change lifetime :
30 minutes

Acceptance context lifetime :
15 minutes
```

---

## 96.1 Ajustement

Les valeurs peuvent être raccourcies selon la criticité.

---

# 97. Secrets de chiffrement des Delivery Secrets

Les Delivery Secrets utilisent :

- ASP.NET Core Data Protection ;
- ou une abstraction cryptographique approuvée.

---

## 97.1 Isolation

Les protections sont isolées par :

- client ;
- environnement ;
- purpose.

---

## 97.2 Rotation

Une rotation des clés ne doit pas rendre impossible le traitement contrôlé des messages déjà en attente sans plan.

---

## 97.3 Compromission

Une compromission entraîne :

- nouveau Recovery Epoch ;
- révocation des tokens ;
- rotation ;
- audit.

---

# 98. Backups

Les tables de provisioning et récupération font partie de PostgreSQL.

---

## 98.1 Delivery Secrets

Les valeurs chiffrées peuvent apparaître dans un backup tant qu’elles ne sont pas expirées.

---

## 98.2 Compensation

- chiffrement ;
- backup chiffré ;
- accès limité ;
- expiration ;
- nouvel epoch après restauration.

---

## 98.3 Manifest

Le manifest ne contient pas de token ou Delivery Secret.

---

# 99. Restauration et jobs d’email

Après restauration :

- ancien Recovery Epoch invalide ;
- anciens tokens inutilisables ;
- jobs d’envoi anciens annulés ou ignorés ;
- nouvelles invitations émises si nécessaire.

---

## 99.1 Worker

Le worker ne doit pas envoyer un ancien lien après changement d’epoch.

---

# 100. Consultation des invitations

Une interface autorisée peut afficher :

- email masqué ;
- rôle ;
- état ;
- création ;
- expiration ;
- invitant ;
- delivery status.

---

## 100.1 Token

Le token et le lien complet ne sont jamais réaffichés.

---

## 100.2 Resend

Renvoyer crée un nouveau token.

---

# 101. Consultation des récupérations

Une vue de sécurité peut afficher :

- type ;
- cible ;
- état ;
- date ;
- expiration ;
- initiateur ;
- résultat.

---

## 101.1 Accès

Permission dédiée requise.

---

## 101.2 Auto-récupération

Un utilisateur ne voit que ses propres parcours ordinaires, pas les secrets ni demandes opérateur complètes.

---

# 102. Commandes opérateur

Commandes conceptuelles :

```text
sitefactory admin bootstrap status

sitefactory admin bootstrap plan

sitefactory admin bootstrap apply

sitefactory admin recovery plan

sitefactory admin recovery apply

sitefactory admin invitations status
```

---

## 102.1 Pas de token dans les arguments

Le CLI ne reçoit ni n’affiche un token comme argument.

---

## 102.2 Pas de mot de passe

Le CLI n’accepte pas :

```text
--password
```

pour créer ou récupérer un administrateur.

---

## 102.3 Rapports

Les rapports contiennent :

- Operation ID ;
- Client ;
- Environment ;
- User ID ou Invitation ID ;
- purpose ;
- expiration ;
- mode de livraison ;
- résultat.

Ils ne contiennent pas le secret.

---

# 103. Confirmation opérateur

Une opération critique affiche :

```text
Client
Environment
Database target
User or email masked
Action
Expiration
Delivery mode
Reason
```

---

## 103.1 Production

Une confirmation spécifique à la production est exigée.

---

## 103.2 Mauvaise cible

Une incohérence entre la configuration, la base et l’environnement bloque l’opération.

---

# 104. Audit fail-closed

Les opérations applicatives sensibles échouent si l’événement d’audit obligatoire ne peut pas être enregistré.

---

## 104.1 Bootstrap

La création de l’invitation bootstrap doit être auditée.

---

## 104.2 Audit indisponible

Une procédure break-glass peut produire un rapport externe puis réconcilier l’audit après rétablissement.

---

# 105. Sécurité

## 105.1 Actifs

- comptes ;
- rôles ;
- mots de passe ;
- MFA ;
- recovery codes ;
- tokens ;
- emails ;
- sessions ;
- Recovery Epoch ;
- Delivery Secrets ;
- opérateurs.

---

## 105.2 Menaces

- credential par défaut ;
- vol d’invitation ;
- token rejoué ;
- scanner consommant le lien ;
- brute force ;
- reset d’un autre compte ;
- contournement MFA ;
- opérateur malveillant ;
- restauration réactivant un token ;
- lien construit avec un host malveillant ;
- email compromis ;
- mass assignment.

---

## 105.3 Contrôles

- bootstrap local ;
- token 256 bits ;
- hash ;
- purpose ;
- expiration ;
- client et environnement ;
- POST final ;
- antiforgery ;
- MFA ;
- epoch ;
- role templates ;
- step-up ;
- audit ;
- rate limiting ;
- no-store ;
- no-referrer.

---

# 106. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Mot de passe par défaut découvert | Aucun credential par défaut |
| Premier visiteur devenant admin | Aucun bootstrap public |
| Token récupéré dans la base | Hash seulement |
| Token exposé dans un job | Job contenant uniquement l’Invitation ID |
| Scanner d’email ouvrant le lien | GET non mutating |
| Token staging en production | Binding environnement |
| Ancien token après restore | Recovery Epoch |
| Reset contournant MFA | MFA toujours requis |
| Admin réinitialisant son propre MFA | Approbation distincte |
| Lien vers domaine malveillant | PublicBaseUrl allowlistée |
| Invitation accordant un rôle arbitraire | Rôles délégables |
| Token dans les logs | Redaction et query logging désactivé |

---

# 107. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-PROVISION-001` | État de provisioning inconnu |
| `SF-PROVISION-002` | Bootstrap déjà terminé |
| `SF-PROVISION-003` | Administrateur actif déjà présent |
| `SF-PROVISION-004` | Invitation bootstrap déjà active |
| `SF-PROVISION-005` | Migrations requises |
| `SF-PROVISION-006` | Schéma Identity indisponible |
| `SF-PROVISION-007` | Schéma Audit indisponible |
| `SF-PROVISION-008` | Mode de livraison invalide |
| `SF-PROVISION-009` | Fichier protégé impossible à créer |
| `SF-PROVISION-010` | Confirmation opérateur absente |
| `SF-INVITE-001` | Invitation inconnue |
| `SF-INVITE-002` | Invitation expirée |
| `SF-INVITE-003` | Invitation révoquée |
| `SF-INVITE-004` | Invitation déjà consommée |
| `SF-INVITE-005` | Adresse email invalide |
| `SF-INVITE-006` | Adresse déjà active |
| `SF-INVITE-007` | Invitation concurrente |
| `SF-INVITE-008` | Rôle non délégable |
| `SF-INVITE-009` | Permission d’invitation absente |
| `SF-INVITE-010` | Step-up requis |
| `SF-INVITE-011` | Envoi impossible |
| `SF-INVITE-012` | Activation atomique échouée |
| `SF-INVITE-013` | MFA non configuré |
| `SF-INVITE-014` | Compte pending invalide |
| `SF-TOKEN-001` | Token absent |
| `SF-TOKEN-002` | Format de token invalide |
| `SF-TOKEN-003` | Token inconnu |
| `SF-TOKEN-004` | Secret de token invalide |
| `SF-TOKEN-005` | Token expiré |
| `SF-TOKEN-006` | Token consommé |
| `SF-TOKEN-007` | Purpose incompatible |
| `SF-TOKEN-008` | Client incompatible |
| `SF-TOKEN-009` | Environnement incompatible |
| `SF-TOKEN-010` | Recovery Epoch incompatible |
| `SF-TOKEN-011` | Security Stamp incompatible |
| `SF-TOKEN-012` | Trop de tentatives |
| `SF-TOKEN-013` | Delivery Secret indisponible |
| `SF-TOKEN-014` | Déchiffrement du Delivery Secret impossible |
| `SF-TOKEN-015` | Acceptance Context invalide |
| `SF-TOKEN-016` | Acceptance Context expiré |
| `SF-RECOVERY-001` | Reset non disponible |
| `SF-RECOVERY-002` | Reset expiré |
| `SF-RECOVERY-003` | Nouveau mot de passe invalide |
| `SF-RECOVERY-004` | MFA requis pour finaliser |
| `SF-RECOVERY-005` | MFA indisponible |
| `SF-RECOVERY-006` | Recovery code requis |
| `SF-RECOVERY-007` | Approbation administrative requise |
| `SF-RECOVERY-008` | Auto-approbation MFA interdite |
| `SF-RECOVERY-009` | Récupération opérateur non autorisée |
| `SF-RECOVERY-010` | Raison de récupération absente |
| `SF-RECOVERY-011` | Cible de récupération ambiguë |
| `SF-RECOVERY-012` | Compte désactivé |
| `SF-RECOVERY-013` | Anciennes sessions non révoquées |
| `SF-RECOVERY-014` | Security Stamp non modifié |
| `SF-RECOVERY-015` | Recovery Epoch non initialisé |
| `SF-EMAIL-CHANGE-001` | Nouvelle adresse invalide |
| `SF-EMAIL-CHANGE-002` | Adresse déjà utilisée |
| `SF-EMAIL-CHANGE-003` | Confirmation expirée |
| `SF-EMAIL-CHANGE-004` | Step-up requis |
| `SF-EMAIL-CHANGE-005` | Changement final impossible |

---

# 108. Tests unitaires

Tester notamment :

- génération aléatoire ;
- format du token ;
- hash ;
- comparaison ;
- purpose ;
- expiration ;
- client ;
- environnement ;
- Recovery Epoch ;
- transitions d’invitation ;
- rôles délégables ;
- Acceptance Context ;
- nettoyage.

---

# 109. Tests du bootstrap

Couvrir :

```text
aucun administrateur

administrateur déjà actif

invitation pending

migrations absentes

audit absent

email delivery

protected-file delivery

mauvais environnement

confirmation absente

bootstrap terminé
```

---

# 110. Tests des invitations

Vérifier :

- invitation ordinaire ;
- rôle autorisé ;
- rôle interdit ;
- email actif ;
- invitation concurrente ;
- expiration ;
- révocation ;
- resend ;
- acceptation ;
- double acceptation ;
- activation atomique.

---

# 111. Tests des scanners de liens

Simuler :

```text
GET initial par scanner

plusieurs GET

HEAD request

préchargement navigateur

GET sans POST final
```

Résultat attendu :

```text
invitation toujours disponible
compte non activé
token non consommé
```

---

# 112. Tests des pages sensibles

Vérifier :

```text
Cache-Control no-store

Referrer-Policy no-referrer

aucune ressource tierce

query string non logguée

redirection vers URL propre

token absent de l’historique applicatif suivant
```

---

# 113. Tests d’acceptation

Couvrir :

- mot de passe trop court ;
- mot de passe commun ;
- TOTP invalide ;
- TOTP valide ;
- abandon ;
- recovery codes ;
- transaction rollback ;
- rôle appliqué uniquement à la fin ;
- nouvelle connexion exigée.

---

# 114. Tests du reset de mot de passe

Couvrir :

```text
compte existant

compte inexistant

compte désactivé

réponse générique

rate limiting

token expiré

token rejoué

nouveau mot de passe

MFA valide

MFA indisponible

sessions révoquées

Security Stamp modifié
```

---

# 115. Tests anti-enumeration

Comparer :

- compte existant ;
- compte absent ;
- compte désactivé ;
- compte pending ;
- compte sans MFA.

Vérifier :

- réponse identique ;
- statut cohérent ;
- différence temporelle évitable réduite ;
- aucun email dans les logs.

---

# 116. Tests MFA recovery

Couvrir :

- recovery code valide ;
- recovery code réutilisé ;
- autre administrateur autorisé ;
- autre administrateur sans step-up ;
- auto-approbation ;
- opérateur local ;
- expiration ;
- nouveau TOTP ;
- sessions révoquées ;
- anciens codes invalidés.

---

# 117. Tests du dernier administrateur

Vérifier :

- désactivation refusée ;
- suppression refusée ;
- autre administrateur pending seulement ;
- autre administrateur actif sans MFA ;
- autre administrateur actif avec MFA ;
- dérogation break-glass ;
- récupération opérateur.

---

# 118. Tests de changement d’email

Couvrir :

- step-up ;
- mot de passe actuel ;
- MFA ;
- nouvelle adresse ;
- token ;
- ancienne adresse notifiée ;
- Security Stamp ;
- sessions ;
- adresse déjà utilisée.

---

# 119. Tests de restauration

Scénario :

1. créer une invitation ;
2. créer un reset ;
3. produire un backup ;
4. consommer les deux ;
5. restaurer le backup ;
6. changer le Recovery Epoch ;
7. tenter les anciens liens.

Résultat :

```text
tous les anciens tokens sont refusés
```

---

# 120. Tests des jobs d’email

Vérifier :

- payload sans token ;
- lecture du Delivery Secret ;
- retry SMTP ;
- envoi idempotent ;
- token révoqué avant envoi ;
- epoch modifié ;
- invitation expirée ;
- Delivery Secret supprimé.

---

# 121. Tests de non-divulgation

Injecter des valeurs marqueurs pour :

- token ;
- password ;
- TOTP secret ;
- recovery code ;
- lien complet ;
- Delivery Secret.

Vérifier leur absence dans :

- logs ;
- traces ;
- métriques ;
- audit ;
- rapports ;
- exceptions ;
- payloads de jobs.

---

# 122. Tests de concurrence

Tester :

- deux acceptations ;
- deux resends ;
- deux resets ;
- reset et changement de mot de passe simultanés ;
- invitation et création concurrente ;
- Recovery Epoch changé pendant l’acceptation.

---

# 123. Tests de sécurité

Vérifier notamment :

```text
Bootstrap public absent

Mot de passe CLI interdit

Token dans argument interdit

Token GET non consommé

Open redirect refusé

Host Header non utilisé librement

Mass assignment refusé

Role escalation refusée

Reset sans MFA refusé

Self MFA reset refusé

Token staging en production refusé

Token d’un autre client refusé
```

---

# 124. Tests de performance

Mesurer :

- hash et validation de token ;
- recherche par Token ID ;
- création de l’Acceptance Context ;
- rate limiting ;
- activation ;
- nettoyage ;
- delivery job.

---

# 125. Tests multiplateformes

Les tests fonctionnels s’exécutent sur :

```text
Windows x64

Linux x64
```

Les permissions de fichier, Docker, Data Protection et SMTP sont validées sur Linux.

---

# 126. Quality gates

Une fonctionnalité de provisioning est bloquée lorsque :

```text
Un mot de passe par défaut existe.

Un bootstrap public est exposé.

Un token est stocké en clair.

Un token est écrit dans un log.

Un token est consommé par GET.

Une invitation ne possède pas d’expiration.

Une invitation peut être réutilisée.

Un rôle peut être choisi librement par le destinataire.

Un compte devient actif avant le MFA.

Un reset de mot de passe contourne le MFA.

Une récupération MFA peut être auto-approuvée.

Un opérateur définit directement un mot de passe.

Un job contient un token.

Une restauration peut réactiver un token.

Une URL est construite depuis un Host Header non validé.

Une réponse révèle l’existence d’un compte.
```

---

# 127. Quality gates de production

La production est bloquée lorsque :

```text
Le provisioning state n’est pas initialisé.

Le Recovery Epoch est absent.

Le MFA est facultatif pour le premier administrateur.

Les tables de tokens sont absentes.

Les clés Data Protection sont éphémères.

Les pages de token peuvent être mises en cache.

Les query strings sensibles sont journalisées.

Le SMTP requis n’est pas validé.

Le mode protected-file possède des permissions trop larges.

Le dernier administrateur peut être supprimé sans contrôle.

La procédure de restauration ne change pas le Recovery Epoch.
```

---

# 128. CI/CD

La CI doit vérifier :

- absence de credentials par défaut ;
- génération de tokens ;
- hash ;
- expiration ;
- antiforgery ;
- rate limiting ;
- anti-enumeration ;
- MFA ;
- audit ;
- jobs d’email ;
- non-divulgation ;
- restauration ;
- migrations.

---

## 128.1 Email réel

Les tests standards utilisent un fake SMTP.

Les tests d’intégration réels utilisent un environnement protégé et des comptes de test.

---

# 129. Règles pour les modules

Un module ne doit pas créer directement un administrateur.

---

## 129.1 Identité

Les besoins d’administration passent par les contrats Identity publics.

---

## 129.2 Invitation spécifique

Une customization peut demander un profil de rôle existant, sans inventer un nouveau workflow de token.

---

# 130. Règles pour les customizations

Une customization ne doit pas :

- créer un mot de passe ;
- créer son propre token ;
- désactiver le MFA ;
- exposer un bootstrap ;
- modifier le Recovery Epoch ;
- ajouter une récupération par question secrète ;
- écrire dans les tables Identity directement.

---

# 131. Règles pour LogicGate

LogicGate doit :

- vérifier la cible bootstrap ;
- remettre les liens par un canal sûr ;
- supprimer les fichiers temporaires ;
- maintenir les rôles délégables ;
- vérifier le dernier administrateur ;
- traiter les récupérations opérateur ;
- changer les epochs après restauration ;
- revoir les comptes au handover.

---

## 131.1 LogicGate ne doit pas

- communiquer un mot de passe par email ;
- réutiliser une invitation ;
- copier un token dans un ticket ;
- désactiver le MFA pour gagner du temps ;
- créer un admin par SQL ordinaire ;
- partager un compte ;
- conserver un fichier de bootstrap après usage.

---

# 132. Règles pour Codex

Codex peut :

- implémenter les contrats ;
- créer les tables ;
- créer les commandes ;
- ajouter les tests ;
- créer les parcours UI ;
- ajouter les jobs ;
- produire les runbooks.

---

## 132.1 Codex ne doit pas

```text
Créer un administrateur de production

Inventer un email de production

Inventer un mot de passe

Afficher un token réel

Envoyer une invitation réelle

Modifier un Recovery Epoch réel

Réinitialiser un MFA réel

Révoquer des sessions réelles

Créer un compte break-glass

Ajouter un bootstrap public

Appliquer une migration production
```

---

## 132.2 Décision humaine

Une décision humaine est requise pour :

- bootstrap réel ;
- invitation privilégiée ;
- récupération opérateur ;
- reset MFA ;
- changement de Recovery Epoch ;
- modification des expirations ;
- rôle délégable ;
- désactivation du dernier administrateur ;
- canal de remise exceptionnel.

---

# 133. Anti-patterns interdits

## 133.1 Mot de passe dans `.env`

```text
ADMIN_PASSWORD=...
```

---

## 133.2 Seed administrateur

Une migration crée un compte actif.

---

## 133.3 Premier visiteur

La première personne ouvrant `/setup` devient administrateur.

---

## 133.4 Token en clair en base

Le token peut être récupéré par une lecture PostgreSQL.

---

## 133.5 Token dans un job

Le payload durable contient le lien complet.

---

## 133.6 Token consommé au GET

Un scanner d’email invalide l’invitation.

---

## 133.7 Reset désactivant le MFA

Le contrôle de l’email remplace tous les facteurs.

---

## 133.8 Admin réinitialisant son propre MFA

Le mot de passe courant suffit à retirer le second facteur.

---

## 133.9 Rôle dans le formulaire

Le destinataire modifie la valeur du rôle dans le POST.

---

## 133.10 Return URL externe

Le token est transmis à un autre domaine.

---

## 133.11 Resend réutilisant le même token

Une fuite ancienne reste exploitable.

---

## 133.12 Restore conservant les tokens

Un lien précédemment consommé redevient valide.

---

# 134. Conséquences positives

La décision apporte :

- aucun credential par défaut ;
- premier administrateur provisionné de manière contrôlée ;
- MFA avant activation ;
- invitations révocables ;
- récupération sans bypass silencieux ;
- anti-enumeration ;
- résistance aux scanners d’emails ;
- révocation des sessions ;
- bootstrap fermé définitivement ;
- rôles bornés ;
- autonomie client ;
- transfert facilité ;
- audit complet.

---

# 135. Conséquences négatives

La décision entraîne :

- tables de provisioning ;
- tokens ;
- chiffrement temporaire ;
- emails ;
- commandes opérateur ;
- procédures de récupération ;
- parcours MFA ;
- davantage de tests ;
- nécessité de gérer les invitations expirées ;
- difficulté opérationnelle si le seul administrateur perd tous ses facteurs.

---

# 136. Risques acceptés

## 136.1 Email compromis

Un attaquant contrôlant l’email peut recevoir un lien.

Le MFA existant reste requis pour un reset de mot de passe.

---

## 136.2 Token dans l’historique navigateur

Le lien initial peut apparaître brièvement dans l’historique.

Réduction :

- expiration ;
- usage unique ;
- no-referrer ;
- redirection rapide ;
- MFA ;
- query logging désactivé.

---

## 136.3 Delivery Secret dans un backup

La valeur est chiffrée et temporaire.

Le Recovery Epoch invalide les tokens après restauration.

---

## 136.4 Dernier administrateur

La récupération opérateur augmente le pouvoir de l’accès Infrastructure.

Réduction :

- accès limité ;
- raison ;
- rapport ;
- audit ;
- token temporaire ;
- aucune création directe de credential.

---

# 137. Mesures compensatoires

- bootstrap local ;
- tokens 256 bits ;
- hashes ;
- Delivery Secrets chiffrés ;
- purpose binding ;
- expiration ;
- Recovery Epoch ;
- MFA ;
- recovery codes ;
- anti-enumeration ;
- rate limiting ;
- no-store ;
- no-referrer ;
- redirection propre ;
- audit ;
- révocation ;
- runbooks ;
- contrôles du dernier administrateur.

---

# 138. Performance

## 138.1 Validation de token

La recherche utilise le Token ID indexé.

---

## 138.2 Hash

Le coût SHA-256 d’un token à haute entropie est négligeable.

---

## 138.3 Rate limiting

Les contrôles sont exécutés avant les opérations PostgreSQL coûteuses.

---

## 138.4 Nettoyage

Le nettoyage utilise des batches bornés.

---

# 139. Coûts

## 139.1 Coûts supplémentaires

- développement ;
- stockage ;
- SMTP ;
- support ;
- récupération ;
- audit ;
- tests ;
- documentation.

---

## 139.2 Coûts évités

- credential par défaut compromis ;
- premier administrateur usurpé ;
- mot de passe transmis ;
- reset contournant MFA ;
- compte inaccessible sans procédure ;
- tokens réutilisables ;
- incidents non traçables.

---

# 140. Maintenabilité

Pour chaque parcours, il doit être possible de répondre :

```text
Quel est le purpose du token ?

Quelle est son expiration ?

Quel Recovery Epoch utilise-t-il ?

Comment est-il livré ?

Où son hash est-il stocké ?

Comment est-il révoqué ?

Le MFA est-il requis ?

Quelles sessions sont révoquées ?

Quel événement d’audit est produit ?

Comment le parcours réagit-il après restauration ?
```

---

# 141. Compatibilité

## 141.1 Token schema

Le format possède une version ou un purpose stable.

---

## 141.2 Anciennes invitations

Une nouvelle release doit continuer à interpréter les invitations encore valides ou les révoquer explicitement pendant l’upgrade.

---

## 141.3 Data Protection

Une transition de key ring doit traiter les Delivery Secrets en attente.

---

## 141.4 IdP futur

Un fournisseur externe pourra remplacer la vérification primaire, mais les invitations, rôles et contrôles de récupération devront être réévalués.

---

## 141.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La suppression d’un ancien compte par défaut nécessite sa désactivation, sa rotation et une nouvelle invitation.

---

# 142. Migration depuis un administrateur par défaut

Le processus est :

```text
identifier le compte
→ créer un nouvel administrateur par invitation
→ vérifier le MFA
→ transférer les responsabilités
→ révoquer les sessions de l’ancien compte
→ désactiver ou supprimer l’ancien compte
→ révoquer le mot de passe
→ analyser les logs
→ auditer
```

---

## 142.1 Credential exposé

Le mot de passe par défaut est considéré comme compromis.

---

# 143. Migration depuis des tokens en clair

Le processus est :

```text
révoquer tous les tokens
→ créer un nouveau Recovery Epoch
→ supprimer les valeurs en clair
→ introduire les hashes
→ introduire les Delivery Secrets chiffrés
→ réémettre les invitations nécessaires
→ scanner les backups et logs
```

---

# 144. Migration depuis un reset sans MFA

Le processus est :

```text
séparer password reset et MFA recovery
→ imposer le MFA après reset
→ ajouter les recovery codes
→ ajouter le workflow administrateur
→ ajouter le workflow opérateur
→ révoquer les sessions
→ tester
```

---

# 145. Transfert client

Le handover doit inclure :

- administrateurs actifs ;
- rôles ;
- MFA configuré ;
- invitations pending ;
- comptes LogicGate ;
- Recovery Epoch ;
- politiques ;
- runbooks ;
- Delivery Modes ;
- accès SMTP ;
- procédures du dernier administrateur.

---

## 145.1 Comptes LogicGate

Les comptes LogicGate doivent être :

- transférés si contractuellement prévu ;
- ou désactivés et révoqués.

---

## 145.2 Administrateur client

Au moins un administrateur client actif avec MFA doit être validé avant le retrait des accès LogicGate.

---

# 146. Clôture de service

Le processus suit :

```text
annuler les invitations LogicGate inutiles
→ vérifier les administrateurs client
→ vérifier le MFA
→ révoquer les sessions LogicGate
→ retirer les rôles LogicGate
→ transférer les procédures
→ changer le Recovery Epoch si nécessaire
→ auditer
```

---

# 147. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de créer une invitation réelle ;
- de créer un administrateur ;
- de réinitialiser un mot de passe ;
- de réinitialiser un MFA ;
- de changer une adresse email ;
- de modifier le Recovery Epoch ;
- de révoquer un compte ;
- d’appliquer une migration.

---

# 148. Réversibilité

## 148.1 Classification

```text
élevée avant activation

moyenne après création d’un compte

faible après compromission d’un credential
```

---

## 148.2 Invitation

Une invitation peut être révoquée avant consommation.

---

## 148.3 Compte actif

Un compte actif peut être désactivé et ses sessions révoquées.

---

## 148.4 Bootstrap

Le bootstrap ordinaire ne doit pas être réactivé après sa clôture.

---

# 149. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `InvitationId` ;
- `TokenId` ;
- purposes ;
- statuts ;
- `RecoveryEpoch` ;
- erreurs.

---

## Étape 2 — PostgreSQL

Créer :

- provisioning state ;
- invitations ;
- recovery tokens ;
- Delivery Secrets ;
- Acceptance Contexts ;
- indexes ;
- migrations.

---

## Étape 3 — Token service

Ajouter :

- génération 256 bits ;
- encodage ;
- hash ;
- validation ;
- purpose binding ;
- epoch binding.

---

## Étape 4 — Bootstrap CLI

Créer :

- status ;
- plan ;
- apply ;
- email ;
- protected-file ;
- rapport.

---

## Étape 5 — Invitations ordinaires

Ajouter :

- permission ;
- rôles délégables ;
- step-up ;
- envoi ;
- révocation ;
- resend.

---

## Étape 6 — Acceptation

Ajouter :

- GET non mutating ;
- Acceptance Context ;
- URL propre ;
- mot de passe ;
- TOTP ;
- activation atomique.

---

## Étape 7 — Password reset

Ajouter :

- anti-enumeration ;
- rate limiting ;
- email ;
- nouveau mot de passe ;
- MFA ;
- révocation des sessions.

---

## Étape 8 — MFA recovery

Ajouter :

- recovery codes ;
- approbation administrative ;
- récupération opérateur ;
- nouvel enrollment.

---

## Étape 9 — Recovery Epoch

Ajouter :

- initialisation ;
- rotation ;
- intégration restore ;
- invalidation des jobs.

---

## Étape 10 — Opérations

Ajouter :

- nettoyage ;
- observabilité ;
- alertes ;
- handover ;
- runbooks.

---

# 150. Tâches liées proposées

```text
SF-TASK-253 — Define administrator invitation and recovery contracts

SF-TASK-254 — Add administrator provisioning state persistence

SF-TASK-255 — Add hashed one-time token persistence

SF-TASK-256 — Add encrypted temporary token delivery storage

SF-TASK-257 — Add administrator recovery epochs

SF-TASK-258 — Implement the local first-administrator bootstrap plan

SF-TASK-259 — Implement protected bootstrap invitation delivery

SF-TASK-260 — Add privileged administrator invitations

SF-TASK-261 — Add delegable administrator role policies

SF-TASK-262 — Add scanner-safe invitation acceptance

SF-TASK-263 — Add atomic password and MFA account activation

SF-TASK-264 — Add anti-enumeration password reset requests

SF-TASK-265 — Require MFA during administrator password reset

SF-TASK-266 — Add administrator MFA recovery approval

SF-TASK-267 — Add last-administrator operator recovery

SF-TASK-268 — Add administrator email-change confirmation

SF-TASK-269 — Add restore-time recovery-token invalidation

SF-TASK-270 — Add invitation and recovery cleanup jobs

SF-TASK-271 — Add provisioning non-disclosure security tests

SF-TASK-272 — Add provisioning and recovery operational runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 151. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Aucun compte administrateur par défaut n’existe.

Aucun mot de passe administrateur n’est lu depuis l’environnement.

Aucun endpoint public de bootstrap n’existe.

Le bootstrap exige une commande locale explicite.

Le bootstrap échoue lorsqu’un administrateur actif existe.

L’invitation bootstrap expire.

Une invitation ordinaire exige une permission et un step-up.

Un administrateur ne peut inviter qu’un rôle délégable.

Les tokens possèdent au moins 256 bits d’entropie.

Seul le hash du token est stocké pour validation.

Les jobs d’email ne contiennent aucun token.

Une requête GET ne consomme pas l’invitation.

La page retire le token de l’URL active.

Les pages utilisent no-store et no-referrer.

Le compte reste inactif tant que le MFA n’est pas configuré.

L’activation du compte est atomique.

Le destinataire choisit lui-même son mot de passe.

Les recovery codes ne sont affichés qu’une fois.

Un reset de mot de passe ne contourne pas le MFA.

Une récupération MFA ne peut pas être auto-approuvée.

La procédure opérateur ne définit aucun mot de passe.

Une récupération révoque les sessions existantes.

Un nouveau Recovery Epoch invalide tous les anciens tokens.

Une restauration ne réactive aucun lien.

Les réponses publiques ne révèlent pas l’existence d’un compte.

Les query strings sensibles ne sont pas journalisées.

Les tokens et secrets n’apparaissent dans aucun rapport.

Le dernier administrateur ne peut pas être supprimé sans contrôle.

Les tests fonctionnent sur Windows et Linux.
```

---

# 152. Conditions de révision

Réexaminer cette décision si :

- un fournisseur d’identité externe devient obligatoire ;
- les passkeys remplacent le mot de passe ;
- l’email n’est plus un canal acceptable ;
- une preuve d’identité forte devient obligatoire ;
- les invitations doivent être approuvées par deux personnes ;
- une administration centralisée multi-client est introduite ;
- un service de provisioning LogicGate distant est créé ;
- les comptes sont fédérés depuis un annuaire client ;
- un stockage HSM des tokens devient nécessaire ;
- les rôles administrateur deviennent très nombreux ;
- les récupérations opérateur deviennent fréquentes.

---

# 153. Indicateurs à suivre

- invitations créées ;
- invitations acceptées ;
- invitations expirées ;
- échecs de livraison ;
- temps moyen d’acceptation ;
- comptes pending ;
- demandes de reset ;
- resets finalisés ;
- récupérations MFA ;
- usages de recovery codes ;
- récupérations opérateur ;
- changements de Recovery Epoch ;
- tentatives de tokens invalides ;
- comptes privilégiés sans MFA ;
- projets avec un seul administrateur ;
- invitations bootstrap répétées.

---

# 154. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des mots de passe par défaut apparaissent

des tokens sont présents dans les logs

des scanners consomment les invitations

des resets désactivent le MFA

les récupérations opérateur deviennent ordinaires

des comptes pending restent indéfiniment

les invitations accordent des rôles excessifs

les anciens tokens survivent aux restaurations

les liens sont construits depuis le Host Header

les query strings sensibles sont collectées

le dernier administrateur peut être supprimé sans successeur

les comptes LogicGate restent actifs après transfert
```

---

# 155. Conséquences observées

Cette section est append-only après mise en œuvre.

## 155.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
du bootstrap du premier administrateur,
des invitations à usage unique
et des parcours de récupération contrôlés.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- type de parcours ;
- mode de livraison ;
- durée ;
- résultat ;
- incident ou bénéfice ;
- action proposée.

---

# 156. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0028-database-transaction-boundaries-and-concurrency-control.md](./0028-database-transaction-boundaries-and-concurrency-control.md)
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
- [0020-administrator-session-security-and-step-up-authentication.md](./0020-administrator-session-security-and-step-up-authentication.md)

---

# 157. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 152 |

---

# 158. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 159. Synthèse de la décision

```text
Premier administrateur
→ aucun compte par défaut
→ aucune variable ADMIN_PASSWORD
→ aucune page bootstrap publique
→ commande opérateur locale
→ invitation bootstrap
→ durée 60 minutes

Administrateurs suivants
→ permission dédiée
→ step-up
→ rôle délégable
→ invitation nominative
→ durée 24 heures

Token
→ 256 bits
→ Token ID + secret
→ hash stocké
→ purpose
→ client
→ environnement
→ Recovery Epoch
→ usage unique
→ expiration

Livraison
→ email par job avec Invitation ID seulement
→ Delivery Secret chiffré et temporaire
→ ou fichier protégé
→ aucun token dans le rapport

Lien
→ GET non mutating
→ no-store
→ no-referrer
→ aucun script tiers
→ Acceptance Context
→ redirection vers URL propre
→ consommation uniquement par POST

Activation
→ mot de passe choisi par l’utilisateur
→ MFA TOTP obligatoire
→ recovery codes
→ rôle prévu
→ compte actif
→ token consommé
→ transaction et audit
→ nouvelle connexion normale

Password reset
→ réponse générique
→ token 30 minutes
→ nouveau mot de passe
→ MFA toujours requis
→ sessions révoquées
→ Security Stamp modifié

MFA recovery
→ recovery code
→ autre administrateur avec step-up
→ ou procédure opérateur locale
→ aucun bypass permanent
→ nouvel enrollment
→ sessions révoquées

Restore
→ nouvel Audit Epoch
→ nouveau Session Epoch
→ nouveau Recovery Epoch
→ anciens liens invalides
→ invitations réémises si nécessaire

Protection
→ rate limiting
→ antiforgery
→ rôles bornés
→ no mass assignment
→ aucun secret dans les logs, jobs ou rapports
```

> Le provisionnement d’un administrateur ne doit jamais commencer par la création d’un mot de passe connu de l’opérateur. Il commence par une autorisation temporaire, nominative et révocable, puis laisse le destinataire établir lui-même son mot de passe et son second facteur avant que le moindre privilège devienne actif.
