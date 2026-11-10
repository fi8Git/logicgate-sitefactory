---
adrId: ADR-0027
title: Separate PostgreSQL ownership, migration, runtime, backup and restore privileges
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
  - ./0019-audit-trail-and-sensitive-administrative-actions.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ./0021-administrator-provisioning-invitations-and-account-recovery.md
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
  - ./0024-client-data-export-and-portability.md
  - ./0025-third-party-integrations-and-egress-control.md
  - ./0026-container-image-build-and-runtime-hardening.md
targetVersion: 0.1.0
---

# ADR-0027 — Rôles PostgreSQL et moindre privilège à l’exécution

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La [baseline publique vérifiée](../../tests/PostgreSql/roles.md) distingue
Owner NOLOGIN, Migrator, Web et Worker. Le Migrator n'accepte que son élévation
explicite vers Owner (SET=true, INHERIT=false, ADMIN=false), aucun autre chemin
direct/transitif ni attribut administratif interdit ; Owner ne fournit aucun
chemin d'élévation supplémentaire. Ces contrôles précèdent toute mutation.
Les runtime hosts restent non propriétaires, sans DDL ni écriture des
métadonnées ; le reader est read-only et refuse les privilèges excessifs.
Les noms de rôles ci-dessous sont conceptuels, non une convention de
provisioning production déjà implémentée. Backup/restore et profils opérateur
restent futurs. La fixture PostgreSQL éphémère ne provisionne aucune production.
Les [contre-preuves](../../tests/PostgreSql/permissions-and-errors.md) restent
distinctes d'une validation de CI distante, encore non acquise.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory sépare les privilèges PostgreSQL selon les responsabilités techniques.

La baseline définit des identités distinctes pour :

```text
propriété des objets

migrations

application web

worker

sauvegarde

restauration

consultation opérateur éventuelle
```

Le compte utilisé par l’application web ne doit jamais :

- posséder la base ;
- posséder un schéma ;
- posséder une table ;
- créer ou supprimer un schéma ;
- créer, modifier ou supprimer une table ;
- créer une extension ;
- créer un rôle ;
- modifier un rôle ;
- créer une base ;
- lancer une réplication ;
- appliquer une migration ;
- modifier les ACL ;
- désactiver un trigger ;
- contourner les politiques de sécurité ;
- devenir superutilisateur ;
- utiliser `SET ROLE` vers le propriétaire.

Le worker et le web utilisent également des credentials distincts.

Le rôle de migration n’est disponible que pour une opération explicite exécutée par l’image migrator définie dans l’ADR-0026.

---

## 2. Résumé

La hiérarchie de référence est :

```text
cluster administrator
→ crée la base et les rôles
→ n’est utilisé ni par le web ni par le worker

database owner
→ rôle NOLOGIN
→ possède la base, les schémas et les objets

migrator
→ rôle LOGIN
→ privilèges de migration uniquement
→ peut assumer temporairement le database owner
→ credential monté uniquement dans le migrator

web runtime
→ rôle LOGIN
→ accès minimal aux objets du web
→ aucun DDL

worker runtime
→ rôle LOGIN
→ accès minimal aux jobs et traitements
→ aucun DDL

backup
→ rôle LOGIN
→ lecture nécessaire au backup
→ aucune écriture

restore
→ rôle LOGIN temporaire ou fortement protégé
→ utilisé uniquement sur une cible isolée
→ privilèges élevés et bornés à la restauration

read-only operator
→ facultatif
→ accès uniquement à des vues approuvées
```

Le modèle repose sur :

```text
rôles NOLOGIN propriétaires

rôles LOGIN spécialisés

ACL explicites

default privileges

schémas séparés

PUBLIC révoqué

search_path maîtrisé

fonctions sensibles contrôlées

credentials distincts

manifest de permissions

tests de privilèges négatifs

détection de dérive
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Le rôle runtime ne possède aucun objet.

Le rôle runtime ne possède aucun privilège DDL.

Le rôle runtime ne peut pas créer un objet
dans un schéma applicatif.

Le rôle runtime ne peut pas créer un objet
dans le schéma public.

Le rôle runtime ne peut pas appliquer
une migration EF Core.

Le rôle web et le rôle worker sont distincts.

Le rôle de migration n’est pas présent
dans les secrets du web ou du worker.

Le rôle de backup ne peut modifier aucune donnée.

Le rôle de restauration n’est pas utilisé
par un service permanent.

Le database owner ne possède pas LOGIN.

La base n’accorde pas CONNECT à PUBLIC.

Les schémas applicatifs n’accordent pas CREATE à PUBLIC.

Les fonctions SECURITY DEFINER ne sont pas
exécutables par PUBLIC.

Le search_path des fonctions privilégiées est fixe.

Une migration crée les objets avec le propriétaire attendu.

Les nouveaux objets héritent des ACL attendues
grâce aux default privileges.

Une restauration ne réintroduit pas des rôles
ou ACL d’un autre environnement sans plan.

Une connexion production n’utilise jamais
le rôle postgres ou un superutilisateur.

Une élévation de privilège est auditée
et limitée à une procédure opérateur.
```

---

## 4. Contexte

Une application SiteFactory utilise PostgreSQL pour conserver notamment :

- les contenus ;
- les leads ;
- l’identité ;
- les sessions ;
- les jobs ;
- l’outbox ;
- les métadonnées média ;
- les politiques de confidentialité ;
- le Deletion Ledger ;
- la piste d’audit.

La base doit également permettre :

- les migrations ;
- les sauvegardes ;
- les restaurations ;
- les exports ;
- les opérations de diagnostic.

Utiliser un seul compte PostgreSQL pour toutes ces opérations simplifie initialement la configuration, mais donne à une compromission applicative la possibilité de :

- supprimer les tables ;
- modifier le schéma ;
- créer des fonctions malveillantes ;
- désactiver les triggers ;
- modifier les ACL ;
- créer un compte PostgreSQL ;
- effacer la piste d’audit ;
- contourner les workflows de suppression ;
- empêcher une restauration ;
- modifier les migrations ;
- extraire toutes les données.

L’isolation Docker ne protège pas contre un credential PostgreSQL excessivement privilégié.

Un attaquant contrôlant le processus web peut utiliser tous les droits du rôle PostgreSQL connecté.

Le moindre privilège doit donc être appliqué dans PostgreSQL lui-même.

---

## 5. Problème à résoudre

Comment permettre au web, au worker, au migrator, au backup et au restore d’accomplir leurs tâches tout en garantissant :

- l’absence de privilèges inutiles ;
- la séparation des credentials ;
- l’absence de DDL au runtime ;
- la propriété stable des objets ;
- des migrations explicites ;
- une sauvegarde en lecture seule ;
- une restauration contrôlée ;
- une détection des dérives d’ACL ;
- une compatibilité avec EF Core ;
- une exploitation adaptée à Docker Compose ?

La solution doit également éviter :

- l’usage de `postgres` par l’application ;
- un rôle propriétaire avec LOGIN ;
- l’usage de `GRANT ALL` ;
- les privilèges accordés à `PUBLIC` ;
- des migrations exécutées au démarrage ;
- des credentials partagés ;
- les objets créés avec le mauvais propriétaire ;
- les fonctions `SECURITY DEFINER` vulnérables au `search_path` ;
- les permissions manuelles non reproductibles ;
- une restauration utilisant directement les rôles de la source.

---

## 6. Portée

Cette décision couvre :

- rôles PostgreSQL ;
- ownership ;
- schémas ;
- ACL ;
- default privileges ;
- connexions ;
- secrets ;
- web ;
- worker ;
- migrator ;
- backup ;
- restore ;
- audit ;
- jobs ;
- sessions ;
- fonctions ;
- vues ;
- séquences ;
- extensions ;
- `search_path` ;
- `pg_hba.conf` ;
- limites de connexion ;
- timeouts ;
- migrations ;
- restauration ;
- détection de dérive ;
- tests ;
- handover ;
- runbooks.

---

## 7. Hors portée

Cette décision ne définit pas :

- une plateforme PostgreSQL managée précise ;
- une réplication haute disponibilité ;
- un cluster multi-région ;
- un proxy de connexion obligatoire ;
- PgBouncer comme baseline ;
- le chiffrement champ par champ ;
- une politique RLS multi-tenant ;
- la stratégie complète de partitionnement ;
- les transactions métier détaillées ;
- le tuning complet de PostgreSQL ;
- le patching du serveur PostgreSQL ;
- un mécanisme universel de fédération d’identités PostgreSQL.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Réduction de l’impact d’une compromission applicative**
2. **Aucun DDL au runtime**
3. **Séparation des credentials**
4. **Migrations explicites**
5. **Protection de la piste d’audit**
6. **Sauvegarde en lecture seule**
7. **Restauration contrôlée**
8. **ACL reproductibles**
9. **Compatibilité EF Core**
10. **Isolation client et environnement**
11. **Détection de dérive**
12. **Rotation des credentials**
13. **Audit des opérations privilégiées**
14. **Compatibilité Docker Compose**
15. **Transfert client**
16. **Maintenabilité**

---

## 9. Contraintes

La décision doit respecter :

```text
PostgreSQL

EF Core

Npgsql

Migrations explicites

Un projet et une base par client

Un environnement distinct

Credentials runtime séparés

Rôles propriétaires NOLOGIN

PUBLIC révoqué

Search path explicite

PostgreSQL non exposé publiquement

Secrets runtime

Tests PostgreSQL réels

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- chaque projet client possède sa propre base ;
- le modèle multi-tenant n’appartient pas au MVP ;
- les migrations sont produites et revues avec le code ;
- un opérateur possède un accès suffisamment privilégié pour créer les rôles initiaux ;
- les services web et worker peuvent utiliser des chaînes de connexion distinctes ;
- les volumes et secrets sont configurés avant le démarrage ;
- PostgreSQL supporte les fonctions et ACL nécessaires ;
- les migrations peuvent assumer temporairement un rôle propriétaire ;
- les sauvegardes logiques sont suffisantes pour le profil initial.

---

# 11. Définitions

## 11.1 Cluster administrator

Rôle possédant les privilèges nécessaires à la gestion du cluster PostgreSQL.

---

## 11.2 Database owner

Rôle sans connexion possédant la base et ses objets.

---

## 11.3 Login role

Rôle autorisé à établir une connexion PostgreSQL.

---

## 11.4 Group role

Rôle généralement `NOLOGIN` utilisé pour regrouper des privilèges.

---

## 11.5 Runtime role

Rôle utilisé par un processus applicatif permanent.

---

## 11.6 Migrator role

Rôle utilisé uniquement pour les opérations explicites de migration.

---

## 11.7 Backup role

Rôle en lecture seule utilisé pour produire les sauvegardes.

---

## 11.8 Restore role

Rôle privilégié utilisé uniquement pendant une restauration contrôlée.

---

## 11.9 Ownership

Relation PostgreSQL permettant au propriétaire de modifier ou supprimer un objet.

---

## 11.10 ACL

Liste des privilèges accordés à des rôles sur un objet.

---

## 11.11 Default privileges

Règles appliquées automatiquement aux nouveaux objets créés par un propriétaire donné.

---

## 11.12 Privilege drift

Écart entre les permissions attendues et les permissions réellement présentes.

---

# 12. Option A — Un compte PostgreSQL unique

## 12.1 Description

Le web, le worker, les migrations, les backups et les restaurations utilisent le même compte.

---

## 12.2 Avantages

- configuration simple ;
- peu de secrets ;
- migrations faciles ;
- absence de problèmes d’ACL ;
- diagnostic direct.

---

## 12.3 Inconvénients

- privilèges excessifs ;
- responsabilité impossible à distinguer ;
- compromission totale ;
- rotation risquée ;
- backup pouvant écrire ;
- runtime pouvant migrer.

---

## 12.4 Risques

- suppression du schéma ;
- altération de l’audit ;
- création d’un rôle malveillant ;
- modification des fonctions ;
- données détruites par une erreur applicative.

---

## 12.5 Réversibilité

```text
moyenne
```

Les objets existants doivent être réattribués et leurs ACL corrigées.

---

# 13. Option B — Runtime propriétaire de ses objets

## 13.1 Description

Le web utilise un compte distinct, mais celui-ci possède les tables et schémas.

---

## 13.2 Avantages

- séparation du superutilisateur ;
- migrations possibles ;
- configuration simple ;
- bonne compatibilité EF Core.

---

## 13.3 Inconvénients

- le runtime peut modifier ou supprimer les objets ;
- le runtime peut créer des fonctions ;
- aucun contrôle DDL réel ;
- ownership difficile à retirer ensuite.

---

## 13.4 Risques

- compromission suivie d’un `DROP TABLE` ;
- création d’un trigger de persistance ;
- modification des ACL ;
- désactivation d’un contrôle.

---

## 13.5 Réversibilité

```text
moyenne
```

---

# 14. Option C — Un rôle runtime et un migrator distinct

## 14.1 Description

Le web et le worker utilisent le même rôle runtime.

Un second rôle applique les migrations.

---

## 14.2 Avantages

- aucun DDL au runtime ;
- migrations séparées ;
- complexité raisonnable ;
- meilleure sécurité.

---

## 14.3 Inconvénients

- worker et web partagent les mêmes droits ;
- backup et restore non séparés ;
- privilèges runtime parfois encore larges ;
- audit et jobs insuffisamment isolés.

---

## 14.4 Risques

- compromission web donnant les droits worker ;
- web pouvant modifier directement les jobs ;
- credential partagé entre plusieurs services.

---

## 14.5 Réversibilité

```text
élevée
```

---

# 15. Option D — Rôles spécialisés et propriétaires NOLOGIN

## 15.1 Description

Chaque responsabilité utilise un rôle distinct.

Les objets appartiennent à un rôle propriétaire sans LOGIN.

---

## 15.2 Avantages

- moindre privilège ;
- ownership séparé ;
- migrations explicites ;
- credentials distincts ;
- backup en lecture seule ;
- restauration isolée ;
- dérive mesurable ;
- responsabilité claire.

---

## 15.3 Inconvénients

- davantage de rôles ;
- scripts de bootstrap ;
- default privileges ;
- tests supplémentaires ;
- rotation plus complexe ;
- besoin de maintenir une matrice d’accès.

---

## 15.4 Risques

- ACL manquante après migration ;
- objet créé par le mauvais rôle ;
- credential monté au mauvais service ;
- fonction privilégiée mal sécurisée.

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

| Critère | Poids | Compte unique | Runtime owner | Runtime + migrator | Rôles spécialisés |
|---|---:|---:|---:|---:|---:|
| Moindre privilège | 5 | 1 | 2 | 4 | 5 |
| Protection contre DDL | 5 | 1 | 1 | 5 | 5 |
| Séparation web/worker | 4 | 1 | 1 | 1 | 5 |
| Backup sécurisé | 4 | 1 | 2 | 2 | 5 |
| Restore contrôlé | 4 | 1 | 2 | 3 | 5 |
| Compatibilité EF Core | 4 | 5 | 5 | 5 | 4 |
| Rotation | 4 | 2 | 3 | 4 | 5 |
| Auditabilité | 4 | 1 | 2 | 4 | 5 |
| Maintenabilité | 5 | 2 | 3 | 4 | 5 |
| Réversibilité | 3 | 3 | 3 | 5 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons de créer un ensemble de rôles PostgreSQL spécialisés par client et par environnement.

Convention conceptuelle :

```text
sf_{client}_{environment}_owner

sf_{client}_{environment}_migrator

sf_{client}_{environment}_web

sf_{client}_{environment}_worker

sf_{client}_{environment}_backup

sf_{client}_{environment}_restore

sf_{client}_{environment}_readonly
```

Les noms physiques doivent respecter les limites PostgreSQL et utiliser un identifiant normalisé.

---

## 17.1 Exemple

```text
sf_acme_prod_owner

sf_acme_prod_migrator

sf_acme_prod_web

sf_acme_prod_worker

sf_acme_prod_backup

sf_acme_prod_restore
```

---

## 17.2 Isolation

Les rôles de staging et de production sont distincts.

Un rôle d’un client ne possède aucun privilège sur la base d’un autre client.

---

# 18. Rôle cluster administrator

Le cluster administrator est utilisé uniquement pour :

- créer les bases ;
- créer les rôles ;
- attribuer les ownerships initiaux ;
- configurer les extensions ;
- exécuter certaines opérations de reprise ;
- gérer les ACL du cluster.

---

## 18.1 Interdiction runtime

Son credential n’est jamais fourni à :

- web ;
- worker ;
- migrator ordinaire ;
- Caddy ;
- outils front-end.

---

## 18.2 PostgreSQL officiel

Le compte `postgres` n’est pas utilisé par l’application.

---

## 18.3 Accès

Son accès est limité à :

- opérations initiales ;
- maintenance ;
- break-glass ;
- restauration exceptionnelle.

---

# 19. Database owner

Le database owner est créé avec :

```text
NOLOGIN

NOSUPERUSER

NOCREATEDB

NOCREATEROLE

NOREPLICATION

NOBYPASSRLS
```

---

## 19.1 Ownership

Il possède :

- la base client ;
- les schémas applicatifs ;
- les tables ;
- les séquences ;
- les vues ;
- les fonctions ;
- les types ;
- les migrations.

---

## 19.2 Connexion

Il ne peut pas établir une connexion directement.

---

## 19.3 Avantage

Aucun credential permanent ne correspond au propriétaire des objets.

---

# 20. Migrator role

Le migrator est un rôle LOGIN distinct.

Il possède :

```text
NOSUPERUSER

NOCREATEDB

NOCREATEROLE

NOREPLICATION

NOBYPASSRLS

NOINHERIT
```

---

## 20.1 Membership

Le migrator peut recevoir une membership contrôlée vers le database owner.

Il doit exécuter explicitement :

```sql
SET ROLE sf_acme_prod_owner;
```

avant l’application des migrations.

---

## 20.2 Interdiction runtime

Le web et le worker ne reçoivent jamais cette membership.

---

## 20.3 Secret

Le credential migrator est monté uniquement dans :

```text
migration image
```

pendant l’opération.

---

## 20.4 Fin d’opération

Le secret est retiré du conteneur une fois l’opération terminée.

---

# 21. Web runtime role

Le rôle web reçoit uniquement les privilèges nécessaires aux requêtes HTTP et à l’administration.

Il peut notamment avoir besoin de :

- lire et modifier les données métier ;
- gérer les sessions administrateur ;
- gérer les invitations et tokens via des fonctions contrôlées ;
- écrire dans l’outbox ;
- ajouter des événements d’audit via une fonction ;
- lire les données nécessaires à l’administration.

---

## 21.1 Interdictions

Le web ne peut pas :

- créer ou modifier un objet ;
- supprimer une table ;
- appliquer une migration ;
- revendiquer directement des jobs worker ;
- modifier la chaîne d’audit ;
- modifier les ACL ;
- modifier les rôles PostgreSQL.

---

# 22. Worker runtime role

Le rôle worker reçoit les privilèges nécessaires à :

- revendiquer les jobs ;
- renouveler les leases ;
- écrire les résultats ;
- dispatcher l’outbox ;
- effectuer les traitements média ;
- exécuter les nettoyages ;
- exécuter les politiques de rétention ;
- écrire les événements d’audit prévus.

---

## 22.1 Données métier

Le worker reçoit uniquement les accès métier nécessaires aux handlers installés.

---

## 22.2 Interdictions

Le worker ne reçoit pas automatiquement tous les privilèges du web.

---

## 22.3 Évolution

L’ajout d’un nouveau Job Type doit déclarer les besoins PostgreSQL supplémentaires.

---

# 23. Backup role

Le rôle backup est un rôle LOGIN en lecture seule.

Il reçoit au minimum :

```text
CONNECT sur la base

USAGE sur les schémas nécessaires

SELECT sur les tables

SELECT ou USAGE approprié sur les séquences

EXECUTE sur les fonctions strictement nécessaires au backup
```

---

## 23.1 Interdictions

Le rôle backup ne possède pas :

```text
INSERT

UPDATE

DELETE

TRUNCATE

CREATE

ALTER

DROP

TRIGGER

REFERENCES

SET ROLE owner
```

---

## 23.2 Backup logique

Il doit pouvoir utiliser l’outil de backup validé sans devenir propriétaire.

---

## 23.3 Données sensibles

Le backup peut contenir des données sensibles par nature.

Le rôle et les archives suivent les contrôles de l’ADR-0023.

---

# 24. Restore role

Le rôle restore est séparé du rôle backup.

---

## 24.1 Utilisation

Il est utilisé uniquement :

- dans un environnement isolé ;
- pendant une restauration ;
- par une procédure opérateur ;
- avec un credential temporaire ou fortement protégé.

---

## 24.2 Privilèges

Le rôle peut recevoir une membership contrôlée vers le database owner de la cible.

---

## 24.3 Pas de service permanent

Aucun conteneur permanent n’utilise le rôle restore.

---

## 24.4 Après restauration

Le credential est :

- révoqué ;
- rotaté ;
- ou désactivé ;

selon la procédure.

---

# 25. Read-only operator role

Un rôle de consultation facultatif peut être créé.

---

## 25.1 Accès

Il reçoit uniquement :

- CONNECT ;
- USAGE sur un schéma de reporting ;
- SELECT sur des vues approuvées.

---

## 25.2 Tables brutes

Il ne reçoit pas automatiquement SELECT sur toutes les tables.

---

## 25.3 Données personnelles

Les vues doivent :

- masquer ;
- minimiser ;
- limiter ;

les données selon le besoin opérateur.

---

# 26. Matrice de rôles

| Capacité | Owner | Migrator | Web | Worker | Backup | Restore | Read-only |
|---|---:|---:|---:|---:|---:|---:|---:|
| LOGIN | Non | Oui | Oui | Oui | Oui | Oui/Temporaire | Oui |
| Propriété des objets | Oui | Via `SET ROLE` | Non | Non | Non | Via `SET ROLE` | Non |
| DDL | Oui | Oui | Non | Non | Non | Oui | Non |
| DML métier | Oui | Selon migration | Oui | Limité | Non | Oui pendant restore | Non |
| Jobs | Oui | Schéma | Enqueue limité | Claim/Update | Lecture backup | Restore | Non |
| Audit append | Oui | Schéma | Fonction | Fonction | Lecture backup | Restore | Non |
| Audit modification | Owner uniquement via migration | Non hors migration | Non | Non | Non | Restore | Non |
| Backup | Non nécessaire | Non | Non | Non | Oui | Non | Non |
| Restore | Non direct | Non | Non | Non | Non | Oui | Non |
| Création rôle | Non | Non | Non | Non | Non | Non | Non |

---

# 27. Attributs interdits

Les rôles applicatifs utilisent :

```text
NOSUPERUSER

NOCREATEDB

NOCREATEROLE

NOREPLICATION

NOBYPASSRLS
```

---

## 27.1 `INHERIT`

Les rôles runtime ne reçoivent aucune membership privilégiée à hériter.

---

## 27.2 `SET ROLE`

Les rôles runtime ne doivent pouvoir assumer aucun rôle plus privilégié.

---

## 27.3 Vérification

La CI et le diagnostic de production inspectent ces attributs.

---

# 28. Isolation par base

Chaque base client révoque :

```sql
REVOKE ALL ON DATABASE client_database FROM PUBLIC;
```

Puis accorde explicitement `CONNECT` aux rôles attendus.

---

## 28.1 Autres clients

Un rôle client ne possède aucun `CONNECT` sur les autres bases.

---

## 28.2 Base `postgres`

Les rôles applicatifs ne reçoivent pas d’accès fonctionnel à la base d’administration `postgres`.

---

# 29. Schémas PostgreSQL

Le projet utilise des schémas explicites.

Exemple conceptuel :

```text
app

identity

jobs

audit

privacy

media

reporting
```

---

## 29.1 `public`

Le schéma `public` n’héberge pas les objets applicatifs ordinaires.

---

## 29.2 Révocation

```sql
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
```

Les privilèges nécessaires sont ensuite accordés explicitement.

---

## 29.3 Ownership

Chaque schéma applicatif appartient au database owner ou à un owner NOLOGIN spécialisé.

---

# 30. Owners spécialisés

Le MVP peut utiliser un propriétaire unique de base.

Des propriétaires NOLOGIN spécialisés peuvent être ajoutés pour :

```text
audit

identity

jobs
```

si une isolation renforcée est nécessaire.

---

## 30.1 Baseline

Pour limiter la complexité initiale :

```text
un database owner NOLOGIN
```

possède les schémas applicatifs.

---

## 30.2 Audit

Les fonctions d’audit restent protégées même avec ce propriétaire commun.

---

# 31. Search path

Les rôles utilisent un `search_path` explicite.

Exemple conceptuel :

```text
pg_catalog, app, identity, jobs
```

---

## 31.1 `public`

Le schéma `public` n’est pas inclus dans le `search_path` des rôles privilégiés.

---

## 31.2 Qualification

Les migrations et requêtes sensibles qualifient explicitement les schémas.

Exemple :

```sql
audit.append_event(...)
```

---

## 31.3 Valeur utilisateur

Le `search_path` ne doit pas être modifiable depuis une donnée métier ou un paramètre utilisateur.

---

# 32. Fonctions `SECURITY DEFINER`

Une fonction `SECURITY DEFINER` est utilisée uniquement lorsque des privilèges contrôlés doivent être exposés.

Exemples :

- ajout d’un événement d’audit ;
- claim d’un job ;
- création d’une invitation ;
- mise à jour atomique d’un état de sécurité ;
- ajout d’une entrée au Deletion Ledger.

---

## 32.1 Propriétaire

La fonction appartient à un rôle NOLOGIN.

---

## 32.2 Search path fixe

La fonction définit :

```sql
SET search_path = pg_catalog, audit;
```

ou la liste minimale nécessaire.

---

## 32.3 PUBLIC

```sql
REVOKE ALL ON FUNCTION ... FROM PUBLIC;
```

---

## 32.4 EXECUTE

Seuls les rôles nécessaires reçoivent `EXECUTE`.

---

## 32.5 Validation

Les paramètres sont :

- typés ;
- bornés ;
- validés ;
- sans SQL dynamique arbitraire.

---

# 33. SQL dynamique

Le SQL dynamique dans une fonction privilégiée est évité.

---

## 33.1 Besoin exceptionnel

Lorsqu’il est nécessaire :

- identifiants allowlistés ;
- `format()` avec placeholders adaptés ;
- aucune concaténation utilisateur ;
- tests d’injection ;
- revue de sécurité.

---

# 34. Tables sensibles

Les tables suivantes ne doivent pas exposer un DML direct général au web :

```text
audit.audit_events

audit.audit_chain_state

audit.audit_epochs

jobs.job_leases

privacy.deletion_ledger

identity.recovery_epochs

identity.session_epochs
```

---

## 34.1 Accès

Les modifications passent par des fonctions ou services contrôlés.

---

## 34.2 Lecture

La lecture est accordée uniquement selon le besoin du service.

---

# 35. Audit

Le web et le worker ajoutent des événements via une fonction contrôlée.

---

## 35.1 Interdictions

Ils ne peuvent pas :

- mettre à jour un événement ;
- supprimer un événement ;
- modifier le hash précédent ;
- modifier l’epoch ;
- modifier le chain state.

---

## 35.2 Migrator

Seul le migrator peut faire évoluer la structure de l’audit.

---

# 36. Jobs

Le web peut :

- ajouter une intention outbox ;
- créer certains jobs directs autorisés.

Le worker peut :

- revendiquer ;
- renouveler ;
- terminer ;
- retry ;
- dead-letter.

---

## 36.1 Accès direct web

Le web ne reçoit pas le privilège général permettant de marquer arbitrairement un job comme réussi.

---

## 36.2 Fonctions

Les opérations de claim et transition peuvent être centralisées dans des fonctions.

---

# 37. Sessions administrateur

Le web gère les sessions via le schéma Identity.

---

## 37.1 État sensible

Les changements de :

- Session Epoch ;
- Security Stamp ;
- révocation globale ;

utilisent des opérations contrôlées.

---

## 37.2 Worker

Le worker ne reçoit pas d’accès aux sessions sauf job explicitement concerné.

---

# 38. Privacy et Deletion Ledger

Le worker de rétention peut ajouter des entrées au ledger via une fonction contrôlée.

---

## 38.1 Web

Le web peut créer et approuver une demande de confidentialité selon les permissions applicatives.

---

## 38.2 Ledger

Aucun rôle runtime ne peut modifier une entrée existante.

---

# 39. Tables métier

Les tables métier ordinaires peuvent accorder :

- SELECT ;
- INSERT ;
- UPDATE ;
- DELETE ;

selon les besoins du web ou du worker.

---

## 39.1 DELETE

Le privilège SQL `DELETE` ne remplace pas :

- l’autorisation applicative ;
- le workflow de suppression ;
- l’audit ;
- la rétention.

---

## 39.2 Accès réduit

Un module peut limiter le worker à :

```text
SELECT + UPDATE
```

sans `DELETE`.

---

# 40. Séquences

Les rôles recevant des insertions doivent disposer des privilèges strictement nécessaires sur les séquences.

---

## 40.1 Baseline

Pour une séquence utilisée par un insert :

```text
USAGE

SELECT lorsque nécessaire
```

---

## 40.2 `ALL`

`ALL PRIVILEGES` sur toutes les séquences n’est pas la baseline.

---

# 41. Vues

Les vues peuvent exposer une projection contrôlée.

---

## 41.1 Reporting

Le rôle read-only utilise préférentiellement des vues.

---

## 41.2 Security barrier

Une vue manipulant des filtres sensibles peut utiliser les protections PostgreSQL adaptées.

---

## 41.3 Ownership

La vue appartient au owner NOLOGIN.

---

# 42. Materialized views

Les vues matérialisées sont rafraîchies par :

- worker autorisé ;
- migrator ;
- job dédié.

---

## 42.1 Web

Le web ne reçoit pas automatiquement le privilège de refresh.

---

# 43. Extensions PostgreSQL

Une extension ne peut être créée que par :

- cluster administrator ;
- migrator autorisé ;

selon les privilèges requis.

---

## 43.1 Catalogue

Les extensions approuvées sont déclarées.

Exemple conceptuel :

```text
pgcrypto
```

uniquement si le besoin est démontré.

---

## 43.2 Runtime

Le web et le worker ne peuvent pas créer d’extension.

---

## 43.3 Extension non approuvée

Une migration demandant une extension inconnue est bloquée.

---

# 44. Default privileges

Le database owner configure les privilèges par défaut pour les objets futurs.

Exemple conceptuel :

```sql
ALTER DEFAULT PRIVILEGES
FOR ROLE sf_acme_prod_owner
IN SCHEMA app
GRANT SELECT, INSERT, UPDATE, DELETE
ON TABLES TO sf_acme_prod_web;
```

---

## 44.1 Par schéma

Les default privileges sont définis séparément pour :

- tables ;
- séquences ;
- fonctions ;
- types ;
- schémas.

---

## 44.2 Par créateur

Les default privileges s’appliquent au rôle qui crée les objets.

Le migrator doit donc créer les objets sous le database owner.

---

## 44.3 Test

Une migration de test créant un objet doit recevoir automatiquement les ACL attendues.

---

# 45. ACL explicites

Les ACL sont générées depuis un manifest.

Nom conceptuel :

```text
database-access-manifest.json
```

---

## 45.1 Contenu

```text
SchemaVersion

ClientSlug

Environment

DatabaseName

RoleDefinitions

RoleAttributes

SchemaOwners

TablePermissions

SequencePermissions

FunctionPermissions

DefaultPrivileges

ConnectionLimits

TimeoutProfiles
```

---

## 45.2 Secret

Le manifest ne contient aucun mot de passe ou chaîne de connexion.

---

# 46. Scripts de bootstrap

Le générateur peut produire :

```text
database-bootstrap-plan.sql

database-grants.sql

database-permissions-check.sql
```

---

## 46.1 Valeurs sensibles

Les scripts ne contiennent pas de mot de passe en clair.

---

## 46.2 Variables

Les noms et paramètres sont fournis par :

- variables `psql` ;
- outil opérateur ;
- configuration contrôlée.

---

## 46.3 Production

Le bootstrap reste une opération explicite.

---

# 47. Création des rôles

La création des rôles est une opération Infrastructure distincte des migrations métier.

---

## 47.1 Raisons

Les migrations EF Core ne doivent pas :

- créer des rôles de cluster ;
- modifier `pg_hba.conf` ;
- gérer les mots de passe ;
- créer les bases.

---

## 47.2 Bootstrap initial

Le cluster administrator crée :

- base ;
- owner ;
- login roles ;
- ownership ;
- ACL initiales.

---

# 48. Migrations EF Core

Les migrations sont générées dans le repository.

---

## 48.1 Application

Elles sont appliquées par :

```text
migration image
```

avec le migrator.

---

## 48.2 Interdiction

Le web et le worker ne doivent pas appeler :

```csharp
Database.Migrate()
```

au démarrage.

---

## 48.3 Historique

La table d’historique des migrations appartient au database owner.

---

## 48.4 Accès runtime

Le runtime peut lire la version de migration via une vue ou fonction dédiée.

Il ne reçoit pas de privilège de modification de l’historique.

---

# 49. Owner des objets migrés

Le migrator exécute :

```sql
SET ROLE database_owner;
```

avant de créer les objets.

---

## 49.1 Résultat

Les objets appartiennent au database owner, pas au login migrator.

---

## 49.2 Reset

À la fin :

```sql
RESET ROLE;
```

ou la connexion est fermée.

---

## 49.3 Échec

Une migration créant un objet avec le mauvais owner échoue au quality gate ou au contrôle post-migration.

---

# 50. Application des ACL après migration

Les default privileges couvrent les objets ordinaires.

Une migration peut également inclure des grants explicites pour :

- fonctions spécialisées ;
- vues ;
- objets sensibles ;
- nouveaux schémas.

---

## 50.1 Pas de grant manuel caché

Un privilège nécessaire doit être versionné.

---

## 50.2 Rapport

L’opération produit un contrôle des ACL après migration.

---

# 51. Migration transactionnelle

Les migrations utilisent une transaction lorsque l’opération PostgreSQL le permet.

---

## 51.1 Opération non transactionnelle

Une opération nécessitant l’absence de transaction doit être :

- explicitement identifiée ;
- revue ;
- documentée ;
- accompagnée d’un plan de reprise.

---

# 52. Connexions distinctes

Le web et le worker utilisent des chaînes de connexion différentes.

---

## 52.1 Secret IDs

Exemples :

```text
postgres.web.connection

postgres.worker.connection

postgres.migrator.connection

postgres.backup.connection

postgres.restore.connection
```

---

## 52.2 Partage

Les valeurs ne sont pas réutilisées entre environnements.

---

## 52.3 Handover

Les valeurs sont reprovisionnées séparément.

---

# 53. Secrets de connexion

Les credentials suivent l’ADR-0012.

---

## 53.1 Interdictions

Ils ne doivent pas apparaître dans :

- image ;
- repository ;
- logs ;
- traces ;
- manifest ;
- ligne de commande ;
- rapport ;
- export portable.

---

## 53.2 Fichier monté

La préférence est un fichier de secret en lecture seule ou un secret store approuvé.

---

# 54. Authentification PostgreSQL

Les rôles LOGIN utilisent une authentification forte.

---

## 54.1 Mot de passe

Le stockage serveur utilise :

```text
SCRAM-SHA-256
```

ou le mécanisme plus fort supporté et approuvé.

---

## 54.2 `trust`

La méthode :

```text
trust
```

est interdite en production.

---

## 54.3 Credential partagé

Aucun mot de passe partagé entre web, worker, backup et migrator.

---

# 55. `pg_hba.conf`

La configuration doit limiter :

- base ;
- rôle ;
- réseau ;
- méthode d’authentification.

---

## 55.1 Réseau

Les connexions sont autorisées uniquement depuis le réseau Docker ou les sources opérateur prévues.

---

## 55.2 Règle générale

Une règle large telle que :

```text
host all all 0.0.0.0/0
```

est interdite.

---

## 55.3 IPv6

Les règles IPv6 sont également explicites.

---

# 56. Exposition réseau PostgreSQL

PostgreSQL ne publie pas le port `5432` sur Internet.

---

## 56.1 Docker Compose

Le service utilise :

```text
expose
```

ou uniquement le réseau interne, sans `ports` public.

---

## 56.2 Accès opérateur

Un accès externe ponctuel utilise :

- SSH tunnel ;
- VPN ;
- réseau privé ;
- procédure contrôlée.

---

# 57. TLS PostgreSQL

Lorsque PostgreSQL traverse un réseau non strictement local ou partagé, TLS est obligatoire.

---

## 57.1 Réseau Docker local

Le profil VPS mono-hôte peut utiliser un réseau Docker interne sans TLS applicatif si :

- aucun port n’est publié ;
- l’hôte est maîtrisé ;
- la politique l’accepte.

---

## 57.2 Évolution

Un PostgreSQL distant exige :

- TLS ;
- validation du certificat ;
- hostname ;
- politique de rotation.

---

# 58. Application name

Chaque connexion définit un `application_name` borné.

Exemples :

```text
sitefactory-web

sitefactory-worker

sitefactory-migrator

sitefactory-backup

sitefactory-restore
```

---

## 58.1 Objectif

Faciliter :

- observabilité ;
- diagnostics ;
- audit opérateur ;
- limites ;
- terminaison ciblée.

---

## 58.2 Données interdites

Le nom ne contient pas :

- email ;
- token ;
- secret ;
- identifiant utilisateur.

---

# 59. Limites de connexion

Chaque rôle possède un `CONNECTION LIMIT` adapté.

Exemple conceptuel :

```text
web: 30

worker: 15

migrator: 2

backup: 1

restore: 1

readonly: 3
```

---

## 59.1 Valeurs

Les valeurs exactes dépendent :

- du VPS ;
- du pool Npgsql ;
- du nombre de workers ;
- de PostgreSQL.

---

## 59.2 Illimité

Une limite illimitée n’est pas la baseline des rôles applicatifs.

---

# 60. Pool Npgsql

Le pool est configuré par service.

---

## 60.1 Cohérence

La taille maximale du pool ne dépasse pas la capacité accordée au rôle.

---

## 60.2 Web et worker

Ils utilisent des pools indépendants.

---

## 60.3 Migrator

Le migrator utilise un pool minimal ou désactive le pooling selon le besoin.

---

# 61. Timeouts par rôle

Des valeurs de session limitent les requêtes anormales.

Profils conceptuels :

```text
database.web-runtime.v1

database.worker-runtime.v1

database.migrator.v1

database.backup.v1

database.restore.v1
```

---

## 61.1 Paramètres

Ils peuvent définir :

```text
statement_timeout

lock_timeout

idle_in_transaction_session_timeout
```

---

## 61.2 Web

Le profil web possède des limites courtes adaptées aux requêtes interactives.

---

## 61.3 Worker

Le profil worker peut autoriser des traitements plus longs, tout en restant borné.

---

## 61.4 Migrator

Le migrator utilise des limites spécifiques à l’opération.

---

# 62. Baseline indicative des timeouts

Exemple de démarrage :

```text
Web statement timeout:
15 secondes

Web lock timeout:
3 secondes

Web idle in transaction:
30 secondes

Worker statement timeout:
5 minutes maximum par opération déclarée

Worker lock timeout:
10 secondes

Migrator:
défini explicitement par migration

Backup:
défini par le runbook
```

---

## 62.1 Non contractuel

Les valeurs exactes sont validées avec les tests de charge et les opérations réelles.

---

## 62.2 Timeout nul

Un timeout illimité nécessite une justification explicite.

---

# 63. Transactions inactives

Une transaction runtime ne doit pas rester ouverte sans activité.

---

## 63.1 Protection

`idle_in_transaction_session_timeout` est activé.

---

## 63.2 Diagnostic

Les terminaisons sont observables.

---

# 64. Verrous

Le web ne doit pas attendre indéfiniment un verrou PostgreSQL.

---

## 64.1 `lock_timeout`

Un délai borné produit une erreur contrôlée.

---

## 64.2 Migration

Les migrations utilisent un plan spécifique pour les locks.

---

# 65. TEMP

Le privilège `TEMP` sur la base n’est pas accordé par défaut aux rôles runtime.

---

## 65.1 Besoin

Un besoin de table temporaire doit être :

- identifié ;
- limité ;
- testé ;
- ajouté au manifest.

---

## 65.2 Migrator

Le migrator peut recevoir TEMP si une migration le nécessite.

---

# 66. RLS

La Row-Level Security n’est pas la baseline de séparation client, car chaque client possède sa propre base.

---

## 66.1 Autorisation applicative

Les permissions métier restent appliquées dans Application.

---

## 66.2 Besoin futur

RLS peut être utilisée pour un besoin intra-client démontré.

---

## 66.3 Attribut

Les rôles applicatifs utilisent :

```text
NOBYPASSRLS
```

afin de ne pas contourner une future policy.

---

# 67. Triggers

Les triggers applicatifs appartiennent au database owner.

---

## 67.1 Runtime

Les rôles runtime ne peuvent pas :

- désactiver ;
- supprimer ;
- modifier ;

les triggers.

---

## 67.2 Réplication role

Les rôles runtime ne peuvent pas modifier leur `session_replication_role`.

---

# 68. Contraintes et intégrité

Les contraintes importantes sont appliquées dans PostgreSQL.

---

## 68.1 Runtime

Le rôle runtime ne peut pas les supprimer.

---

## 68.2 Migrations

Toute modification de contrainte passe par une migration.

---

# 69. Lecture du catalogue système

PostgreSQL expose certaines métadonnées aux utilisateurs connectés.

---

## 69.1 Limite

La sécurité ne doit pas reposer sur l’invisibilité des noms de tables.

---

## 69.2 Fonctions dangereuses

Les fonctions ou extensions permettant un accès système renforcé ne sont pas accordées aux rôles runtime.

---

# 70. Fonctions de fichiers serveur

Les rôles runtime ne reçoivent pas les rôles prédéfinis permettant :

- lecture de fichiers serveur ;
- écriture de fichiers serveur ;
- exécution de programmes ;
- signalement de processus ;
- modification de paramètres.

---

## 70.1 Exemples interdits

```text
pg_read_server_files

pg_write_server_files

pg_execute_server_program

pg_signal_backend
```

---

# 71. Rôles prédéfinis PostgreSQL

Les rôles prédéfinis à large portée ne sont pas accordés par commodité.

---

## 71.1 Backup

La baseline préfère des grants explicites plutôt que :

```text
pg_read_all_data
```

qui pourrait couvrir plus de bases que nécessaire.

---

## 71.2 Monitoring

Un rôle de monitoring distinct peut recevoir uniquement les rôles prédéfinis nécessaires.

Il n’est pas fourni au web.

---

# 72. Backup logique

Le backup role utilise un outil et une commande approuvés.

---

## 72.1 Snapshot

Le backup peut nécessiter :

- locks de faible niveau ;
- snapshot ;
- lecture des séquences ;
- lecture des large objects si utilisés.

---

## 72.2 Large objects

L’usage de Large Objects PostgreSQL n’est pas la baseline pour les médias.

---

## 72.3 Échec de privilège

Un backup échoue plutôt que d’élever automatiquement ses privilèges.

---

# 73. Backup des ACL

Le backup opérationnel doit préciser si :

- les ACL ;
- owners ;
- rôles ;

sont inclus.

---

## 73.1 Handover

Les rôles source ne doivent pas être recréés aveuglément chez le client cible.

---

## 73.2 Restore

Le restore plan mappe les owners et ACL vers les rôles de la cible.

---

# 74. Restore isolé

Une restauration complète s’effectue dans :

```text
nouvelle base

environnement isolé

services applicatifs arrêtés

workers arrêtés
```

---

## 74.1 Rôles cibles

Les rôles de la cible sont créés avant ou pendant la procédure contrôlée.

---

## 74.2 Source roles

Les rôles de la source ne deviennent pas automatiquement des login roles dans la cible.

---

## 74.3 `--no-owner`

Un restore logique peut utiliser une stratégie sans owners source puis réattribuer les objets au owner cible.

---

# 75. Restore et privileges

Après restauration :

```text
réattribuer les objets

réappliquer les ACL cibles

réappliquer les default privileges

vérifier les attributs des rôles

vérifier les schémas

vérifier la dérive

créer les nouveaux epochs

réconcilier les suppressions
```

---

## 75.1 Ouverture

Le service ne s’ouvre pas avant validation.

---

# 76. Credentials de restore

Le credential restore :

- n’est pas stocké dans le Compose permanent ;
- n’est pas monté au web ;
- n’est pas monté au worker ;
- possède une durée de vie opérationnelle minimale ;
- est rotaté après usage.

---

# 77. Rotation des credentials runtime

La rotation suit un runbook explicite.

---

## 77.1 Processus

```text
générer un nouveau credential

mettre à jour le secret store

redémarrer ou reconnecter le service

vérifier les nouvelles connexions

terminer les anciennes connexions si nécessaire

révoquer l’ancien credential

auditer
```

---

## 77.2 Pools

Les pools Npgsql doivent être renouvelés après rotation.

---

## 77.3 Aucun secret dans Git

La rotation ne modifie pas le repository.

---

# 78. Rotation avec interruption réduite

Un profil renforcé peut utiliser deux login roles temporaires partageant les mêmes group roles.

---

## 78.1 Exemple

```text
sf_acme_prod_web_a

sf_acme_prod_web_b
```

---

## 78.2 Baseline MVP

La rotation en place avec redémarrage gracieux est suffisante pour le profil initial.

---

# 79. Révocation urgente

En cas de compromission d’un credential :

1. activer le kill switch ou arrêter le service concerné ;
2. terminer ses connexions ;
3. révoquer ou changer le credential ;
4. inspecter les opérations ;
5. vérifier les ACL ;
6. vérifier l’audit ;
7. reprovisionner ;
8. redémarrer ;
9. surveiller.

---

## 79.1 Credential web

La révocation du web ne doit pas nécessiter la rotation du migrator ou du backup.

---

# 80. Détection de dérive

La plateforme produit un contrôle comparant :

```text
permissions attendues

permissions réelles
```

---

## 80.1 Périmètre

- rôles ;
- attributs ;
- memberships ;
- CONNECT ;
- schémas ;
- tables ;
- séquences ;
- fonctions ;
- ownership ;
- default privileges ;
- paramètres de rôle.

---

## 80.2 Rapport

Nom conceptuel :

```text
database-permissions-report.json
```

---

# 81. États de conformité

Valeurs recommandées :

```text
compliant

warning

drift-detected

blocked

unknown
```

---

## 81.1 Production

Une dérive critique bloque un nouveau déploiement.

---

## 81.2 Correction

La correction utilise un plan explicite.

---

# 82. Dérives critiques

Exemples :

```text
web devenu owner

web possédant CREATE sur un schéma

web membre du owner

backup possédant UPDATE

PUBLIC possédant CREATE

fonction sensible exécutable par PUBLIC

migrator credential monté au web

rôle applicatif devenu superuser

PostgreSQL publiant 5432
```

---

# 83. Plan de correction des ACL

Une commande conceptuelle produit :

```text
sitefactory database permissions plan
```

---

## 83.1 Lecture seule

Le plan ne modifie pas la base.

---

## 83.2 Apply

```text
sitefactory database permissions apply
```

exige :

- client ;
- environnement ;
- plan ;
- confirmation ;
- rôle opérateur ;
- raison ;
- audit.

---

## 83.3 Suppression de privilège

Une révocation peut interrompre un service.

Le plan doit indiquer l’impact.

---

# 84. Validation au démarrage

Le web et le worker peuvent exécuter un self-check borné.

---

## 84.1 Vérifications positives

Le service peut confirmer :

- connexion ;
- lecture d’une table de version ;
- accès à ses fonctions essentielles ;
- schéma compatible.

---

## 84.2 Vérifications négatives

Les tests négatifs complets ne sont pas exécutés à chaque démarrage de production.

Ils appartiennent à :

- CI ;
- staging ;
- commande opérateur.

---

## 84.3 Aucun DDL de test en production

Le service ne tente pas de créer puis supprimer une table au démarrage.

---

# 85. Connexion au mauvais environnement

Le service doit vérifier :

- Client ID attendu ;
- Environment ID attendu ;
- Database ID attendu ;
- schema version ;
- migration version.

---

## 85.1 Table d’identité de base

Une table ou fonction peut fournir :

```text
ClientSlug

Environment

DatabaseInstanceId
```

---

## 85.2 Incohérence

Le démarrage échoue de manière fermée.

---

# 86. Mauvais rôle

Le service vérifie :

```sql
current_user
```

et éventuellement :

```sql
session_user
```

---

## 86.1 Web

Le web refuse de démarrer avec le rôle migrator, restore ou owner.

---

## 86.2 Worker

Le worker refuse le rôle web si son profil exige un rôle distinct.

---

## 86.3 Migrator

Le migrator vérifie sa capacité à `SET ROLE` vers le owner attendu.

---

# 87. Application configuration

La configuration déclare uniquement :

```text
Secret ID de connexion

profil de rôle attendu

nom logique de base
```

---

## 87.1 Mot de passe

La valeur ne figure pas dans `client.sitefactory.yml`.

---

## 87.2 Role name

Le nom attendu peut appartenir au manifest de déploiement.

---

# 88. Docker Compose

Chaque service reçoit son secret de connexion.

Exemple conceptuel :

```yaml
services:
  web:
    secrets:
      - postgres_web_connection

  worker:
    secrets:
      - postgres_worker_connection

  migrator:
    profiles:
      - operations
    secrets:
      - postgres_migrator_connection
```

---

## 88.1 Interdiction

Le secret migrator n’est pas déclaré sur le service web.

---

## 88.2 Backup

Le service ou job de backup utilise son propre secret.

---

# 89. Rôles PostgreSQL et images

L’image applicative ne contient aucune connaissance secrète des rôles.

---

## 89.1 Migrator image

L’image de migration contient le code des migrations, pas le credential.

---

## 89.2 Backup image

Un outil de backup séparé peut être utilisé avec son credential en lecture seule.

---

# 90. Autorisation applicative et privilèges PostgreSQL

Les privilèges PostgreSQL protègent les responsabilités de service.

Ils ne remplacent pas les permissions utilisateur.

---

## 90.1 Exemple

Le rôle web peut techniquement lire plusieurs leads afin de servir l’administration.

Application doit toujours vérifier :

- utilisateur ;
- rôle ;
- permission ;
- step-up ;
- audit.

---

## 90.2 Compromission web

Le moindre privilège limite les capacités structurelles, mais un web compromis peut toujours accéder aux données accordées à son rôle.

---

# 91. Séparation supplémentaire future

Une évolution pourra séparer :

```text
public web

administrator web

identity service

audit reader
```

avec des rôles PostgreSQL distincts.

---

## 91.1 MVP

Le profil initial utilise un web unique avec un rôle borné.

---

# 92. Accès direct opérateur

L’accès SQL direct est exceptionnel.

---

## 92.1 Préférence

Utiliser :

- interface administrateur ;
- CLI ;
- runbook ;
- vues read-only ;
- rapports.

---

## 92.2 Écriture directe

Une écriture SQL manuelle exige :

- procédure break-glass ;
- backup ;
- script revu ;
- transaction ;
- rapport ;
- audit de réconciliation.

---

# 93. SQL dans les runbooks

Les scripts opérateur doivent :

- être versionnés ;
- être paramétrés ;
- vérifier la cible ;
- utiliser une transaction lorsque possible ;
- ne contenir aucun secret ;
- produire un résultat borné.

---

# 94. Monitoring PostgreSQL

Un rôle de monitoring peut être créé séparément.

---

## 94.1 Accès

Il reçoit uniquement les statistiques nécessaires.

---

## 94.2 Données métier

Il ne reçoit pas automatiquement SELECT sur les tables métier.

---

## 94.3 Labels

Les métriques ne contiennent pas les requêtes complètes avec données sensibles.

---

# 95. Journalisation PostgreSQL

La configuration PostgreSQL doit éviter de journaliser :

- paramètres sensibles ;
- corps complet des requêtes métier ;
- chaînes de connexion ;
- mots de passe.

---

## 95.1 Slow queries

Une requête lente peut être identifiée par :

- fingerprint ;
- durée ;
- application name ;
- schéma ;

sans recopier ses paramètres personnels.

---

# 96. Audit des actions privilégiées

Actions recommandées :

```text
database.role.created

database.role.changed

database.role.revoked

database.privilege.granted

database.privilege.revoked

database.owner.changed

database.migration.started

database.migration.completed

database.restore.started

database.restore.completed

database.permission-drift.detected

database.break-glass.used
```

---

## 96.1 Metadata

- rôle ;
- base ;
- schéma ;
- privilège ;
- environnement ;
- rapport ;
- acteur.

---

## 96.2 Secret

Aucun mot de passe ou fragment de connexion.

---

# 97. Observabilité

Événements techniques recommandés :

```text
DatabaseConnectionOpened

DatabaseConnectionFailed

DatabaseRoleMismatchDetected

DatabaseEnvironmentMismatchDetected

DatabasePrivilegeCheckCompleted

DatabasePrivilegeDriftDetected

DatabaseStatementTimedOut

DatabaseLockTimedOut

DatabasePoolExhausted

DatabaseMigrationPermissionDenied
```

---

## 97.1 Propriétés autorisées

```text
Service

ExpectedRole

ActualRoleId borné

DatabaseProfile

Result

ErrorCode

Environment

Duration
```

---

## 97.2 Données interdites

```text
Connection string

Password

Query parameters

Lead data

Email

Raw SQL contenant des valeurs
```

---

# 98. Métriques

Exemples :

```text
database connections

database connection failures

database pool usage

database statement timeouts

database lock timeouts

database privilege drift

database migrations

database backup failures
```

---

## 98.1 Labels autorisés

```text
service

database_profile

result

error_category

environment
```

---

## 98.2 Labels interdits

```text
connection_string

database_password

user_id

lead_id

query_text complet

session_id
```

---

# 99. Alertes

Alertes possibles :

```text
Rôle runtime possédant du DDL

PUBLIC possédant CREATE

Credential migrator utilisé hors migration

Backup role ayant écrit

Restore role utilisé hors fenêtre

Connexion au mauvais environnement

Pool saturé

Timeouts PostgreSQL en hausse

Permission drift

Rôle applicatif devenu superuser
```

---

## 99.1 Runbook

Chaque alerte critique référence une procédure.

---

# 100. Sécurité

## 100.1 Actifs

- base ;
- données ;
- schémas ;
- audit ;
- jobs ;
- rôles ;
- credentials ;
- backups ;
- migrations ;
- restore.

---

## 100.2 Menaces

- credential runtime compromis ;
- élévation de privilège ;
- DDL malveillant ;
- suppression de tables ;
- modification d’audit ;
- rôle partagé ;
- restore vers mauvaise cible ;
- ACL dérivées ;
- fonction privilégiée injectable ;
- accès inter-client.

---

## 100.3 Contrôles

- rôles distincts ;
- owner NOLOGIN ;
- PUBLIC révoqué ;
- ACL explicites ;
- default privileges ;
- search path ;
- SECURITY DEFINER contrôlé ;
- secrets distincts ;
- réseau interne ;
- migrations explicites ;
- drift detection ;
- audit ;
- tests.

---

# 101. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Web compromis exécutant `DROP TABLE` | Aucun DDL runtime |
| Worker compromis lisant les sessions | Rôle distinct et grants limités |
| Backup modifiant les données | Rôle read-only |
| Credential migrator volé depuis le web | Secret non monté au web |
| Fonction privilégiée détournée | Search path fixe et inputs typés |
| Objet créé avec mauvais owner | `SET ROLE` owner et contrôles |
| Compte PostgreSQL partagé | Login roles distincts |
| Restore recréant les ACL source | Mapping vers les rôles cibles |
| Accès inter-client | Base et CONNECT distincts |
| PUBLIC créant un objet | Révocation CREATE |
| Runtime devenu superuser | Drift detection et quality gate |
| Migration automatique malveillante | Migrator explicite |

---

# 102. Tests unitaires

Tester notamment :

- nommage des rôles ;
- matrice de permissions ;
- manifest ;
- profils ;
- génération SQL ;
- search path ;
- default privileges ;
- mappings de restore.

---

# 103. Tests PostgreSQL réels

Les garanties sont testées sur une vraie instance PostgreSQL.

---

## 103.1 Création

Le test crée :

- base ;
- owner ;
- rôles ;
- schémas ;
- objets ;
- ACL.

---

## 103.2 Nettoyage

L’environnement est isolé et supprimé après test.

---

# 104. Tests positifs web

Vérifier que le rôle web peut :

- lire les données autorisées ;
- créer un lead ;
- modifier un contenu ;
- gérer une session ;
- écrire une outbox ;
- appeler la fonction d’audit.

---

# 105. Tests négatifs web

Vérifier que le rôle web ne peut pas :

```text
CREATE TABLE

ALTER TABLE

DROP TABLE

CREATE SCHEMA

CREATE ROLE

SET ROLE owner

TRUNCATE audit.audit_events

UPDATE audit.audit_events

DELETE audit.audit_events

ALTER DEFAULT PRIVILEGES

CREATE EXTENSION
```

---

# 106. Tests positifs worker

Vérifier que le worker peut :

- revendiquer un job ;
- renouveler un lease ;
- terminer un job ;
- traiter une variante média ;
- mettre à jour une rétention autorisée ;
- écrire un événement d’audit.

---

# 107. Tests négatifs worker

Vérifier qu’il ne peut pas :

- modifier les sessions sans contrat ;
- attribuer un rôle administrateur ;
- créer un objet ;
- modifier une ACL ;
- effacer la piste d’audit ;
- assumer le owner.

---

# 108. Tests du backup role

Vérifier :

```text
SELECT autorisé

pg_dump logique autorisé

INSERT refusé

UPDATE refusé

DELETE refusé

TRUNCATE refusé

CREATE refusé

SET ROLE owner refusé
```

---

# 109. Tests du migrator

Vérifier :

- connexion ;
- `SET ROLE owner` ;
- création d’un objet ;
- owner attendu ;
- default privileges ;
- historique migration ;
- reset ou fermeture ;
- absence de superuser.

---

# 110. Tests du restore role

Vérifier :

- refus dans une cible non isolée ;
- mapping des owners ;
- ACL cibles ;
- absence de rôles source actifs ;
- credential temporaire ;
- vérification post-restore.

---

# 111. Tests du search path

Couvrir :

- schéma public avec objet malveillant ;
- fonction homonyme ;
- table homonyme ;
- fonction SECURITY DEFINER ;
- search path fixe ;
- qualification explicite.

---

## 111.1 Résultat

Un objet créé dans `public` ne doit pas détourner une fonction privilégiée.

---

# 112. Tests des default privileges

Créer après configuration :

- table ;
- séquence ;
- fonction ;
- vue.

Vérifier les ACL automatiques.

---

# 113. Tests de fonctions privilégiées

Vérifier :

- rôle autorisé ;
- rôle non autorisé ;
- paramètres invalides ;
- SQL injection ;
- search path ;
- EXECUTE PUBLIC absent ;
- owner NOLOGIN.

---

# 114. Tests de drift

Modifier volontairement :

- membership ;
- owner ;
- grant ;
- default privilege ;
- rôle attribute.

Le rapport doit détecter l’écart.

---

# 115. Tests des secrets

Vérifier que :

- web ne reçoit pas le secret migrator ;
- worker ne reçoit pas le secret restore ;
- backup ne reçoit pas le secret web ;
- aucun secret n’est présent dans l’image ;
- aucune chaîne n’est logguée.

---

# 116. Tests Docker Compose

Vérifier :

```text
PostgreSQL sans port public

secrets séparés

migrator sous profile operations

restore absent du Compose permanent

worker et web distincts

réseau interne
```

---

# 117. Tests de mauvaise cible

Démarrer le web avec :

- base staging ;
- rôle worker ;
- rôle migrator ;
- base d’un autre client.

Chaque cas doit échouer.

---

# 118. Tests de timeouts

Couvrir :

- requête trop longue ;
- lock ;
- transaction inactive ;
- pool saturé ;
- cancellation ;
- worker long autorisé ;
- migration longue explicite.

---

# 119. Tests de rotation

Vérifier :

- nouveau secret ;
- reconnexion ;
- fermeture du pool ;
- ancien credential refusé ;
- aucune interruption excessive ;
- audit.

---

# 120. Tests de backup et restore

Exécuter :

```text
backup avec le rôle backup
→ restore avec le rôle restore
→ attribution au owner cible
→ ACL cibles
→ runtime reconnecté
→ drift check
```

---

# 121. Tests multiplateformes

Les générateurs, manifests et clients sont testés sur :

```text
Windows x64

Linux x64
```

Les tests PostgreSQL, Docker et permissions de production sont validés sur Linux.

---

# 122. Quality gates

Une contribution est bloquée lorsque :

```text
Le web possède un privilège DDL.

Le worker possède un privilège DDL.

Un rôle runtime possède un objet.

Le database owner possède LOGIN.

Le web est membre du owner.

Le worker est membre du owner.

Le rôle backup possède un privilège d’écriture.

PUBLIC possède CREATE sur un schéma applicatif.

PUBLIC possède EXECUTE sur une fonction sensible.

Une fonction SECURITY DEFINER utilise un search_path non fixé.

Le migrator credential est monté au web.

Le runtime appelle Database.Migrate.

Une migration crée un objet avec le mauvais owner.

Les default privileges ne sont pas testés.

PostgreSQL publie le port 5432.

Un rôle applicatif est superuser.
```

---

# 123. Quality gates de production

La production est bloquée lorsque :

```text
Le database access manifest est absent.

Le rapport de permissions contient une dérive critique.

Le rôle runtime attendu ne correspond pas à current_user.

La base cible ne correspond pas au client et environnement.

Le rôle migrator est utilisé par un service permanent.

Le rôle restore est actif hors procédure.

Le backup role ne peut pas produire un backup vérifié.

Le schéma public possède des grants inattendus.

Les migrations requises ne sont pas appliquées.

Les ACL post-migration ne sont pas conformes.

Le credential postgres est utilisé par l’application.
```

---

# 124. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-DB-ROLE-001` | Rôle PostgreSQL inconnu |
| `SF-DB-ROLE-002` | Rôle attendu différent du rôle courant |
| `SF-DB-ROLE-003` | Rôle du mauvais client |
| `SF-DB-ROLE-004` | Rôle du mauvais environnement |
| `SF-DB-ROLE-005` | Attribut LOGIN invalide |
| `SF-DB-ROLE-006` | Superuser interdit |
| `SF-DB-ROLE-007` | CREATEDB interdit |
| `SF-DB-ROLE-008` | CREATEROLE interdit |
| `SF-DB-ROLE-009` | REPLICATION interdite |
| `SF-DB-ROLE-010` | BYPASSRLS interdit |
| `SF-DB-ROLE-011` | Membership privilégiée interdite |
| `SF-DB-ROLE-012` | Connection limit invalide |
| `SF-DB-OWNER-001` | Database owner inconnu |
| `SF-DB-OWNER-002` | Database owner possède LOGIN |
| `SF-DB-OWNER-003` | Objet possédé par le runtime |
| `SF-DB-OWNER-004` | Owner d’objet incorrect |
| `SF-DB-OWNER-005` | Schéma possédé par le mauvais rôle |
| `SF-DB-GRANT-001` | Privilège runtime excessif |
| `SF-DB-GRANT-002` | Privilège runtime manquant |
| `SF-DB-GRANT-003` | PUBLIC possède un privilège interdit |
| `SF-DB-GRANT-004` | CREATE sur schéma interdit |
| `SF-DB-GRANT-005` | DDL runtime détecté |
| `SF-DB-GRANT-006` | Privilège backup excessif |
| `SF-DB-GRANT-007` | Privilège worker excessif |
| `SF-DB-GRANT-008` | Default privilege absent |
| `SF-DB-GRANT-009` | Default privilege incohérent |
| `SF-DB-SCHEMA-001` | Schéma inconnu |
| `SF-DB-SCHEMA-002` | Schéma public utilisé incorrectement |
| `SF-DB-SCHEMA-003` | Search path invalide |
| `SF-DB-FUNCTION-001` | Fonction privilégiée inconnue |
| `SF-DB-FUNCTION-002` | SECURITY DEFINER non autorisé |
| `SF-DB-FUNCTION-003` | Search path de fonction non fixé |
| `SF-DB-FUNCTION-004` | EXECUTE accordé à PUBLIC |
| `SF-DB-FUNCTION-005` | Owner de fonction invalide |
| `SF-DB-FUNCTION-006` | SQL dynamique non sécurisé |
| `SF-DB-MIGRATOR-001` | Credential migrator absent |
| `SF-DB-MIGRATOR-002` | Migrator utilisé par le runtime |
| `SF-DB-MIGRATOR-003` | SET ROLE owner impossible |
| `SF-DB-MIGRATOR-004` | Migration automatique interdite |
| `SF-DB-MIGRATOR-005` | Migration appliquée avec le mauvais owner |
| `SF-DB-BACKUP-001` | Backup role inconnu |
| `SF-DB-BACKUP-002` | Backup role possède un privilège d’écriture |
| `SF-DB-BACKUP-003` | Backup incomplet par manque de privilège |
| `SF-DB-RESTORE-001` | Restore role inconnu |
| `SF-DB-RESTORE-002` | Restore hors environnement isolé |
| `SF-DB-RESTORE-003` | ACL source réappliquée sans mapping |
| `SF-DB-RESTORE-004` | Owner cible incorrect |
| `SF-DB-RESTORE-005` | Credential restore encore actif |
| `SF-DB-CONNECTION-001` | Base cible incompatible |
| `SF-DB-CONNECTION-002` | Connexion au mauvais environnement |
| `SF-DB-CONNECTION-003` | PostgreSQL publiquement exposé |
| `SF-DB-CONNECTION-004` | Méthode `trust` interdite |
| `SF-DB-CONNECTION-005` | Pool trop grand |
| `SF-DB-CONNECTION-006` | Connection limit atteinte |
| `SF-DB-TIMEOUT-001` | Statement timeout |
| `SF-DB-TIMEOUT-002` | Lock timeout |
| `SF-DB-TIMEOUT-003` | Idle transaction terminée |
| `SF-DB-DRIFT-001` | Dérive de rôle détectée |
| `SF-DB-DRIFT-002` | Dérive d’ownership détectée |
| `SF-DB-DRIFT-003` | Dérive d’ACL détectée |
| `SF-DB-DRIFT-004` | Dérive de default privileges détectée |
| `SF-DB-DRIFT-005` | Rapport de permissions absent |

---

# 125. CI/CD

La CI doit exécuter :

```text
validation du manifest

création des rôles dans PostgreSQL de test

application du bootstrap

application des migrations

vérification des owners

vérification des ACL

tests positifs web

tests négatifs web

tests positifs worker

tests négatifs worker

test backup read-only

test restore isolé

test search_path

test SECURITY DEFINER

test default privileges

drift check
```

---

## 125.1 PostgreSQL réel

Une base simulée ou en mémoire ne suffit pas.

---

## 125.2 Credentials CI

Les credentials sont éphémères et propres au job.

---

# 126. Sécurité des scripts SQL

Les scripts générés doivent :

- utiliser des identifiants normalisés ;
- éviter la concaténation libre ;
- utiliser des variables `psql` contrôlées ;
- refuser les noms invalides ;
- être idempotents lorsque prévu ;
- échouer à la première erreur.

---

## 126.1 `ON_ERROR_STOP`

Les opérations `psql` utilisent :

```text
ON_ERROR_STOP
```

ou un mécanisme équivalent.

---

# 127. Règles pour les modules

Chaque module doit déclarer ses besoins PostgreSQL :

```text
Schemas

Tables

ReadPrivileges

WritePrivileges

SequencePrivileges

FunctionExecutions

WorkerPrivileges

RuntimeFunctions

MigrationRequirements
```

---

## 127.1 Interdictions

Un module ne doit pas :

- accorder `ALL` ;
- créer un rôle ;
- modifier un login ;
- accorder CREATE au runtime ;
- rendre une fonction sensible publique ;
- exécuter une migration au runtime.

---

# 128. Règles pour les customizations

Une customization suit le même manifest.

---

## 128.1 Nouveau schéma

Elle doit déclarer :

- owner ;
- web grants ;
- worker grants ;
- backup grants ;
- default privileges ;
- tests.

---

## 128.2 SQL personnalisé

Tout SQL privilégié fait l’objet d’une revue.

---

# 129. Règles pour LogicGate

LogicGate doit :

- maintenir le manifest ;
- créer les rôles ;
- protéger les credentials ;
- tester les ACL ;
- surveiller les dérives ;
- appliquer les migrations explicitement ;
- vérifier les backups ;
- rotater les secrets ;
- préparer les restores ;
- documenter le handover.

---

## 129.1 LogicGate ne doit pas

- utiliser `postgres` dans l’application ;
- donner le migrator au web ;
- utiliser `GRANT ALL` par commodité ;
- corriger une permission avec `chmod` ou Docker ;
- exposer `5432` ;
- restaurer les ACL source aveuglément ;
- modifier directement les rôles sans rapport.

---

# 130. Règles pour Codex

Codex peut :

- générer le manifest ;
- générer les scripts ;
- créer les migrations ;
- ajouter les tests ;
- analyser une dérive ;
- documenter les runbooks.

---

## 130.1 Codex ne doit pas

```text
Créer un rôle de production

Inventer un mot de passe PostgreSQL

Modifier pg_hba.conf réel

Appliquer une migration production

Accorder superuser

Accorder GRANT ALL

Donner le rôle owner au web

Utiliser postgres dans une connection string

Exposer le port 5432

Lancer une restauration réelle

Corriger une ACL de production sans confirmation
```

---

## 130.2 Décision humaine

Une décision humaine est requise pour :

- nouveau rôle ;
- nouveau schéma ;
- extension PostgreSQL ;
- changement d’ownership ;
- privilège runtime supplémentaire ;
- opération restore ;
- SQL direct ;
- modification de `pg_hba.conf` ;
- rotation d’un credential réel ;
- correction de dérive production.

---

# 131. Anti-patterns interdits

## 131.1 `postgres` comme runtime

```text
Host=postgres;Username=postgres;Password=...
```

---

## 131.2 Compte unique

Le web, le worker et le migrator utilisent la même chaîne.

---

## 131.3 Runtime owner

Les tables appartiennent au rôle web.

---

## 131.4 Auto-migration

```csharp
await database.MigrateAsync();
```

au démarrage du web.

---

## 131.5 `GRANT ALL`

```sql
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA app TO runtime;
```

---

## 131.6 PUBLIC permissif

```sql
GRANT CREATE ON SCHEMA public TO PUBLIC;
```

---

## 131.7 Fonction privilégiée non sécurisée

Une fonction `SECURITY DEFINER` utilise le `search_path` de l’appelant.

---

## 131.8 Secret migrator dans Compose web

Le web peut accéder au credential de migration.

---

## 131.9 Backup write-enabled

Le compte de backup peut mettre à jour les tables.

---

## 131.10 Restore permanent

Un conteneur restore fonctionne en permanence.

---

## 131.11 Grants manuels non versionnés

Une correction existe uniquement dans l’historique shell de l’opérateur.

---

## 131.12 Port PostgreSQL public

```yaml
ports:
  - "5432:5432"
```

dans le profil production.

---

# 132. Conséquences positives

La décision apporte :

- moindre privilège réel ;
- protection contre le DDL runtime ;
- séparation web et worker ;
- migrations explicites ;
- backup read-only ;
- restauration isolée ;
- credentials distincts ;
- meilleure auditabilité ;
- rotation ciblée ;
- détection de dérive ;
- réduction de l’impact d’une compromission ;
- handover plus clair.

---

# 133. Conséquences négatives

La décision entraîne :

- davantage de rôles ;
- plusieurs secrets ;
- bootstrap Infrastructure ;
- default privileges ;
- tests PostgreSQL ;
- gestion des ownerships ;
- scripts supplémentaires ;
- rotation plus structurée ;
- éventuels problèmes d’ACL après migration ;
- procédures de restore plus longues.

---

# 134. Risques acceptés

## 134.1 Web toujours capable de lire ses données

Un web compromis peut accéder aux tables accordées à son rôle.

Réduction :

- minimisation des grants ;
- autorisation applicative ;
- séparation future des services ;
- audit ;
- détection.

---

## 134.2 Migrator puissant

Le migrator peut assumer le owner.

Réduction :

- credential non permanent ;
- image dédiée ;
- procédure explicite ;
- audit ;
- aucune exposition réseau publique.

---

## 134.3 Complexité des ACL

Une nouvelle migration peut oublier un grant.

Réduction :

- default privileges ;
- manifest ;
- tests ;
- drift check.

---

## 134.4 Restore privilégié

Le restore possède un pouvoir important.

Réduction :

- environnement isolé ;
- credential temporaire ;
- plan ;
- rapport ;
- audit ;
- rotation après usage.

---

# 135. Mesures compensatoires

- owner NOLOGIN ;
- runtime distinct ;
- migrator distinct ;
- backup read-only ;
- restore isolé ;
- PUBLIC révoqué ;
- search path fixe ;
- SECURITY DEFINER contrôlé ;
- default privileges ;
- manifests ;
- tests négatifs ;
- secrets distincts ;
- network isolation ;
- drift detection ;
- audit ;
- runbooks.

---

# 136. Performance

## 136.1 Rôles distincts

La séparation des rôles n’ajoute pas de coût significatif aux requêtes.

---

## 136.2 Validation de session

Les fonctions privilégiées ajoutent un coût limité accepté pour les opérations sensibles.

---

## 136.3 Pools

Les pools séparés consomment davantage de connexions.

Les connection limits doivent être calibrées.

---

## 136.4 Search path

La qualification explicite améliore la prévisibilité.

---

# 137. Coûts

## 137.1 Coûts supplémentaires

- administration PostgreSQL ;
- secrets ;
- tests ;
- scripts ;
- monitoring ;
- rotation ;
- restore drills.

---

## 137.2 Coûts évités

- destruction de schéma ;
- migration accidentelle ;
- credential partagé compromis ;
- modification d’audit ;
- restore dangereux ;
- difficulté d’attribution ;
- fuite inter-environnements.

---

# 138. Maintenabilité

Pour chaque rôle, il doit être possible de répondre :

```text
Pourquoi existe-t-il ?

Peut-il se connecter ?

Quels objets possède-t-il ?

Quels schémas peut-il utiliser ?

Quelles tables peut-il lire ?

Quelles tables peut-il modifier ?

Quelles fonctions peut-il exécuter ?

Peut-il faire du DDL ?

Quel secret utilise-t-il ?

Quel service l’utilise ?

Comment est-il rotaté et révoqué ?
```

---

# 139. Compatibilité

## 139.1 PostgreSQL

Les scripts ciblent les versions PostgreSQL supportées par SiteFactory.

---

## 139.2 EF Core

Les migrations restent compatibles avec un owner NOLOGIN via `SET ROLE`.

---

## 139.3 Managed PostgreSQL

Une plateforme managée peut imposer des limites sur :

- ownership ;
- superuser ;
- extensions ;
- rôles prédéfinis.

Le profil devra adapter le bootstrap sans affaiblir les invariants.

---

## 139.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

La migration d’une base existante possédée par le runtime nécessite une réattribution contrôlée.

---

# 140. Migration depuis un compte unique

Le processus est :

```text
inventorier les objets

inventorier les privilèges

créer le owner NOLOGIN

créer les rôles spécialisés

réattribuer les objets au owner

configurer les default privileges

accorder les ACL minimales

mettre à jour les secrets

déployer staging

tester les permissions négatives

rotater l’ancien credential

supprimer ou désactiver l’ancien rôle
```

---

## 140.1 Sauvegarde

Une sauvegarde vérifiée est requise avant la réattribution.

---

## 140.2 Ancien rôle

Il ne doit pas rester owner après migration.

---

# 141. Migration depuis un runtime owner

Le processus est :

```text
créer le owner NOLOGIN

REASSIGN OWNED

corriger les owners de schémas

corriger les fonctions

corriger les séquences

appliquer les grants runtime

vérifier les default privileges

retirer les privilèges DDL

tester
```

---

# 142. Migration depuis des grants manuels

Le processus est :

```text
capturer les ACL actuelles

comparer au manifest cible

identifier les dépendances réelles

produire un plan

appliquer les grants et revokes

tester

produire un rapport de dérive nul
```

---

# 143. Transfert client

Le handover doit inclure :

- database access manifest ;
- définitions de rôles sans credentials ;
- owners ;
- schémas ;
- ACL ;
- default privileges ;
- profils de connexion ;
- migrations ;
- rapports de conformité ;
- runbooks backup et restore ;
- Secret IDs à recréer.

---

## 143.1 Credentials LogicGate

Les credentials LogicGate ne sont pas transférés lorsqu’ils couvrent d’autres environnements ou clients.

---

## 143.2 Registry de secrets

Le client recrée les mots de passe ou credentials dans son propre système.

---

# 144. Clôture de service

La clôture doit :

```text
identifier les rôles LogicGate

transférer les rôles opérateur nécessaires

créer ou vérifier les rôles client

rotater les credentials

révoquer les accès LogicGate

terminer les connexions

vérifier les ACL

produire un rapport

auditer
```

---

# 145. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de créer des rôles réels ;
- de modifier des passwords ;
- de changer des ACL ;
- de réattribuer des objets ;
- de modifier `pg_hba.conf` ;
- d’appliquer une migration ;
- d’exécuter un backup ;
- de restaurer une base ;
- de terminer des connexions.

---

# 146. Réversibilité

## 146.1 Classification

```text
élevée avant application

moyenne après changement d’ownership

faible après suppression d’un ancien rôle
sans inventaire complet
```

---

## 146.2 Rollback

Un rollback de permissions doit être planifié.

Restaurer aveuglément les anciens privilèges excessifs n’est pas la stratégie recommandée.

---

# 147. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- Role IDs ;
- role profiles ;
- grants ;
- ownership ;
- erreurs.

---

## Étape 2 — Manifest

Créer :

```text
database-access-manifest.json
```

---

## Étape 3 — Bootstrap SQL

Générer :

- owner ;
- login roles ;
- database grants ;
- schema grants ;
- default privileges.

---

## Étape 4 — Schémas

Créer :

- app ;
- identity ;
- jobs ;
- audit ;
- privacy ;
- media ;
- reporting.

---

## Étape 5 — Fonctions contrôlées

Ajouter les fonctions nécessaires pour :

- audit ;
- jobs ;
- epochs ;
- ledger.

---

## Étape 6 — Services

Configurer :

- web secret ;
- worker secret ;
- migrator secret ;
- backup secret ;
- restore secret.

---

## Étape 7 — Migrations

Appliquer les migrations sous le owner via le migrator.

---

## Étape 8 — Tests

Ajouter :

- positifs ;
- négatifs ;
- ownership ;
- default privileges ;
- search path ;
- backup ;
- restore.

---

## Étape 9 — Drift detection

Créer :

- inspect ;
- plan ;
- apply ;
- rapport.

---

## Étape 10 — Opérations

Ajouter :

- rotation ;
- incident ;
- restore ;
- handover ;
- runbooks.

---

# 148. Tâches liées proposées

```text
SF-TASK-374 — Define versioned PostgreSQL role profiles

SF-TASK-375 — Generate the database-access manifest

SF-TASK-376 — Add the NOLOGIN database-owner role

SF-TASK-377 — Add the dedicated migration login role

SF-TASK-378 — Add separate web and worker runtime roles

SF-TASK-379 — Add the read-only backup role

SF-TASK-380 — Add the isolated restore role

SF-TASK-381 — Revoke default PUBLIC database and schema privileges

SF-TASK-382 — Add schema-specific runtime grants

SF-TASK-383 — Add versioned PostgreSQL default privileges

SF-TASK-384 — Add hardened SECURITY DEFINER functions

SF-TASK-385 — Add role-specific PostgreSQL timeout profiles

SF-TASK-386 — Add separate runtime connection secrets

SF-TASK-387 — Add PostgreSQL role and environment startup checks

SF-TASK-388 — Add database-permission drift detection

SF-TASK-389 — Add database-permission plan and apply commands

SF-TASK-390 — Add negative PostgreSQL privilege tests

SF-TASK-391 — Add read-only backup-role integration tests

SF-TASK-392 — Add isolated restore-role integration tests

SF-TASK-393 — Add PostgreSQL credential-rotation runbooks

SF-TASK-394 — Add database-role handover documentation
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 149. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Le database owner possède NOLOGIN.

Le web utilise un rôle distinct.

Le worker utilise un rôle distinct.

Le migrator utilise un rôle distinct.

Le backup utilise un rôle read-only.

Le restore n’est utilisé que dans un environnement isolé.

Le web ne possède aucun objet.

Le worker ne possède aucun objet.

Le web ne peut pas créer ou supprimer une table.

Le worker ne peut pas créer ou supprimer une table.

Le web ne peut pas SET ROLE vers le owner.

Le worker ne peut pas SET ROLE vers le owner.

Le migrator peut appliquer les migrations explicitement.

Les objets migrés appartiennent au owner NOLOGIN.

Le runtime n’appelle jamais Database.Migrate.

PUBLIC ne possède pas CONNECT par défaut.

PUBLIC ne possède pas CREATE sur les schémas.

PUBLIC ne peut pas exécuter les fonctions sensibles.

Les fonctions SECURITY DEFINER ont un search path fixe.

Les default privileges s’appliquent aux nouveaux objets.

Le backup peut produire une sauvegarde complète.

Le backup ne peut modifier aucune donnée.

Une restauration réapplique les rôles et ACL de la cible.

Le web et le worker possèdent des secrets distincts.

Le secret migrator n’est pas monté au runtime.

Le rôle courant est vérifié au démarrage.

Le mauvais client ou environnement est refusé.

PostgreSQL n’expose pas le port 5432 publiquement.

Le rapport de dérive est conforme.

Les tests utilisent PostgreSQL réel.

Les tests fonctionnent sur Windows et Linux.
```

---

# 150. Conditions de révision

Réexaminer cette décision si :

- une plateforme PostgreSQL managée devient la baseline ;
- plusieurs services applicatifs distincts apparaissent ;
- une base multi-tenant est introduite ;
- PgBouncer devient obligatoire ;
- une réplication en lecture est ajoutée ;
- un data warehouse est introduit ;
- une politique RLS devient nécessaire ;
- les backups deviennent physiques ou continus ;
- une haute disponibilité multi-nœuds est requise ;
- des extensions nécessitent des privilèges élevés ;
- un gestionnaire d’identités PostgreSQL externe est adopté ;
- le nombre de rôles devient trop complexe à exploiter manuellement.

---

# 151. Indicateurs à suivre

- rôles actifs ;
- rôles LOGIN ;
- rôles propriétaires ;
- connexions par rôle ;
- échecs de connexion ;
- grants excessifs ;
- dérives ;
- migrations ;
- backups ;
- restores ;
- timeouts ;
- locks ;
- pools saturés ;
- rotations ;
- usages du migrator ;
- usages du restore ;
- connexions au mauvais environnement ;
- fonctions SECURITY DEFINER.

---

# 152. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
le web devient propriétaire d’un objet

le worker reçoit les mêmes grants que le web par défaut

le rôle migrator est monté en permanence

des GRANT ALL apparaissent

PUBLIC récupère CREATE

les fonctions privileged se multiplient

le search path n’est plus qualifié

le backup nécessite un superuser

les restores réintroduisent les rôles source

les dérives ne sont pas vérifiées

des connexions utilisent postgres

des scripts SQL manuels remplacent le manifest
```

---

# 153. Conséquences observées

Cette section est append-only après mise en œuvre.

## 153.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède la création
des rôles PostgreSQL spécialisés,
des default privileges
et de la détection de dérive.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- rôle ;
- schéma ;
- privilège ;
- dérive ;
- incident ou bénéfice ;
- action proposée.

---

# 154. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0031-application-startup-validation-and-runtime-compatibility.md](0031-application-startup-validation-and-runtime-compatibility.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- Les schémas, propriétés d'objets et privilèges applicables sont définis par la décision, la baseline et les matrices du présent ADR ; aucun modèle métier privé n'est nécessaire pour appliquer la séparation des rôles.
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
- [0019-audit-trail-and-sensitive-administrative-actions.md](0019-audit-trail-and-sensitive-administrative-actions.md)
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](0021-administrator-provisioning-invitations-and-account-recovery.md)
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0024-client-data-export-and-portability.md](0024-client-data-export-and-portability.md)
- [0025-third-party-integrations-and-egress-control.md](0025-third-party-integrations-and-egress-control.md)
- [0026-container-image-build-and-runtime-hardening.md](0026-container-image-build-and-runtime-hardening.md)

---

# 155. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 150 |

---

# 156. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 157. Synthèse de la décision

```text
Cluster administrator
→ création de la base
→ création des rôles
→ aucune utilisation applicative

Database owner
→ NOLOGIN
→ propriétaire des schémas et objets
→ aucun credential permanent

Migrator
→ LOGIN distinct
→ NOINHERIT
→ SET ROLE owner explicite
→ image de migration uniquement
→ aucun démarrage automatique

Web
→ LOGIN distinct
→ DML strictement nécessaire
→ sessions et administration
→ outbox
→ audit via fonction
→ aucun DDL

Worker
→ LOGIN distinct
→ jobs et handlers
→ traitements média et rétention
→ aucun DDL
→ aucun accès Identity inutile

Backup
→ lecture seule
→ aucune écriture
→ aucune membership owner

Restore
→ rôle distinct
→ environnement isolé
→ credential temporaire
→ réapplication des owners et ACL cibles

PostgreSQL
→ PUBLIC révoqué
→ schéma public non utilisé
→ search path explicite
→ default privileges
→ fonctions privilégiées protégées
→ aucun port public

Secrets
→ connexion web
→ connexion worker
→ connexion migrator
→ connexion backup
→ connexion restore
→ aucune valeur dans Git ou image

Migrations
→ explicites
→ objets appartenant au owner
→ ACL vérifiées
→ aucun Database.Migrate au runtime

Contrôle
→ database-access-manifest.json
→ tests de privilèges positifs
→ tests de privilèges négatifs
→ database-permissions-report.json
→ détection de dérive

Restore
→ rôles de la cible
→ owners réattribués
→ ACL réappliquées
→ nouveaux epochs
→ réconciliation avant ouverture
```

> Un compte PostgreSQL utilisé par l’application doit être considéré comme compromis dès que le processus applicatif l’est. Le moindre privilège ne consiste donc pas seulement à éviter le superutilisateur : il consiste à empêcher le web et le worker de posséder les objets, d’appliquer des migrations ou d’accéder aux responsabilités qui ne leur appartiennent pas.
