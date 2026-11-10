---
adrId: ADR-0028
title: Define explicit transaction boundaries and control concurrent database writes
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
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
  - ./0024-client-data-export-and-portability.md
  - ./0025-third-party-integrations-and-egress-control.md
  - ./0026-container-image-build-and-runtime-hardening.md
  - ./0027-database-roles-and-runtime-least-privilege.md
targetVersion: 0.1.0
---

# ADR-0028 — Frontières transactionnelles et contrôle de concurrence PostgreSQL

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La décision reste normative pour les futurs cas d'usage persistants.
L'implémentation publique actuelle comprend le
[bootstrap des métadonnées](../03-generation/generated-runtime-metadata-bootstrap.md)
avec transaction, verrou explicite, idempotence et refus des identités
incompatibles ; le reader utilise une transaction strictement read-only.
Cela ne livre ni agrégats métier éditables, ni gestion globale de Version,
ni jobs/outbox. Les contrats et exemples de concurrence ci-dessous sont
cibles ; aucun appel provider ou effet externe n'est autorisé dans une
transaction PostgreSQL.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory définit les transactions PostgreSQL au niveau des cas d’usage applicatifs.

La baseline repose sur :

```text
une commande applicative
→ une frontière transactionnelle explicite
→ un commit ou un rollback atomique
```

Le niveau d’isolation par défaut est :

```text
READ COMMITTED
```

La concurrence sur les agrégats administrables est contrôlée principalement par :

```text
concurrence optimiste
+ colonne Version explicite
+ contraintes PostgreSQL
```

Les verrous pessimistes sont réservés à des cas précis :

```text
claim de jobs

allocation de ressources rares

mise à jour d’un invariant multi-lignes

séquence ou état global critique

opération opérateur exclusive
```

Les appels externes, emails, webhooks et traitements longs ne sont jamais exécutés à l’intérieur d’une transaction métier PostgreSQL.

Ils utilisent :

```text
transaction métier
→ outbox
→ commit
→ job durable
→ effet externe
```

---

## 2. Résumé

Le flux transactionnel de référence est :

```text
requête ou job
→ authentification et autorisation
→ validation
→ chargement des données
→ transaction explicite
→ vérification de la version attendue
→ modification des agrégats
→ contraintes PostgreSQL
→ événement d’audit
→ message outbox
→ SaveChanges
→ commit
→ réponse ou traitement post-commit
```

En présence de concurrence :

```text
version attendue différente
→ aucun écrasement silencieux
→ rollback
→ erreur de concurrence
→ rechargement ou nouvelle tentative utilisateur
```

En présence d’une erreur PostgreSQL transitoire :

```text
serialization failure ou deadlock
→ rollback complet
→ retry borné de l’unité transactionnelle
→ uniquement si la commande est rejouable
```

Les principaux niveaux d’isolation sont utilisés ainsi :

```text
READ COMMITTED
→ commandes et requêtes ordinaires

REPEATABLE READ
→ snapshots cohérents et exports

SERIALIZABLE
→ invariants critiques rares et explicitement testés
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Une transaction appartient à un cas d’usage applicatif.

Une transaction ne traverse pas plusieurs requêtes HTTP.

Une transaction ne traverse pas plusieurs exécutions de job.

Une transaction ne reste jamais ouverte
pendant une interaction utilisateur.

Une transaction ne contient aucun appel réseau externe.

Une transaction ne contient aucun envoi d’email.

Une transaction ne contient aucun appel CRM.

Une transaction ne contient aucun appel AI.

Une transaction ne contient aucun webhook.

Une transaction ne contient aucun délai volontaire.

Une transaction ne contient aucun traitement CPU long.

Un conflit de concurrence ne provoque jamais
un écrasement silencieux.

Une commande rejouée ne doit pas créer
un second effet métier logique.

Une contrainte d’unicité appartient à PostgreSQL,
pas uniquement à une vérification applicative préalable.

Une transaction annulée ne laisse ni audit,
ni outbox, ni modification métier partielle.

Une annulation ou un timeout provoque un rollback.

Un retry réexécute toute la frontière transactionnelle,
pas uniquement la dernière requête SQL.

Un retry automatique n’est pas appliqué
à une commande non idempotente sans stratégie dédiée.

Une opération de lecture ordinaire n’ouvre pas
de transaction explicite inutile.

Une transaction longue fait l’objet
d’un profil et d’un plan spécifiques.
```

---

## 4. Contexte

Plusieurs acteurs peuvent modifier simultanément une application SiteFactory :

- deux administrateurs ;
- un administrateur et un worker ;
- deux workers ;
- un job de rétention et une action métier ;
- un import et une opération d’administration ;
- deux soumissions publiques ;
- une restauration ou migration opérateur ;
- une synchronisation externe.

Les anomalies possibles comprennent :

```text
lost update

double insertion

double notification

write skew

lecture incohérente

état partiellement persisté

deadlock

attente de verrou indéfinie

job revendiqué par plusieurs workers

suppression pendant une modification

publication d’une version obsolète
```

Exemple de lost update :

```text
Administrateur A charge Version 4.

Administrateur B charge Version 4.

A modifie puis enregistre Version 5.

B modifie ensuite et écrase silencieusement
les changements de A.
```

Une transaction seule ne résout pas automatiquement ce problème.

Sous `READ COMMITTED`, chaque commande peut toujours écraser une modification concurrente si aucune version attendue n’est vérifiée.

---

## 5. Problème à résoudre

Comment garantir :

- l’atomicité des cas d’usage ;
- la protection contre les lost updates ;
- l’unicité ;
- la cohérence entre données métier, audit et outbox ;
- la sécurité des jobs concurrents ;
- des transactions courtes ;
- des retries maîtrisés ;
- une absence de verrouillage excessif ;
- une expérience utilisateur claire en cas de conflit ?

La solution doit également éviter :

- les transactions ambiantes invisibles ;
- les appels externes avant commit ;
- les transactions ouvertes dans les composants UI ;
- les `SaveChanges` dispersés ;
- les verrous de table ;
- les transactions sérialisables utilisées partout ;
- les retries universels ;
- les pré-vérifications d’unicité sans contrainte ;
- les mises à jour « dernière écriture gagnante » ;
- les locks PostgreSQL conservés pendant un traitement long.

---

## 6. Portée

Cette décision couvre :

- frontières transactionnelles ;
- EF Core ;
- Npgsql ;
- isolation PostgreSQL ;
- concurrence optimiste ;
- tokens de version ;
- verrous pessimistes ;
- contraintes ;
- deadlocks ;
- retries ;
- idempotence ;
- outbox ;
- audit ;
- jobs ;
- médias ;
- fichiers ;
- exports ;
- imports ;
- suppressions ;
- migrations ;
- observabilité ;
- tests ;
- qualité ;
- runbooks.

---

## 7. Hors portée

Cette décision ne définit pas :

- une transaction distribuée entre plusieurs bases ;
- un protocole two-phase commit ;
- une base multi-tenant ;
- un système de verrou distribué externe ;
- une architecture event sourcing ;
- un consensus distribué ;
- un broker transactionnel ;
- le versionnement détaillé des contrats HTTP ;
- le cache et son invalidation ;
- la stratégie complète de réplication PostgreSQL ;
- une réplication multi-région.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Aucun écrasement silencieux**
2. **Atomicité métier**
3. **Cohérence avec l’audit et l’outbox**
4. **Transactions courtes**
5. **Contraintes fortes dans PostgreSQL**
6. **Retries bornés**
7. **Idempotence**
8. **Faible contention**
9. **Gestion explicite des conflits**
10. **Compatibilité EF Core**
11. **Compatibilité avec les jobs**
12. **Observabilité**
13. **Testabilité sous concurrence réelle**
14. **Simplicité proportionnée au MVP**
15. **Prévisibilité opérationnelle**
16. **Portabilité des données**

---

## 9. Contraintes

La décision doit respecter :

```text
PostgreSQL

EF Core

Npgsql

READ COMMITTED par défaut

Transactions explicites

Concurrence optimiste

Version bigint explicite

Contraintes PostgreSQL

Outbox transactionnelle

Audit transactionnel

Aucun appel externe dans une transaction

Timeouts bornés

Tests PostgreSQL réels

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- chaque projet client possède une base PostgreSQL ;
- les principales commandes affectent peu de lignes ;
- les opérations administratives concurrentes restent modérées ;
- les effets externes passent par les jobs ;
- les transactions peuvent être rejouées dans certains cas ;
- les agrégats administrables peuvent recevoir une colonne de version ;
- les longues opérations peuvent être découpées ;
- les invariants importants peuvent être représentés par des contraintes ou verrous explicites.

---

# 11. Définitions

## 11.1 Transaction métier

Unité atomique correspondant à un cas d’usage.

---

## 11.2 Frontière transactionnelle

Début et fin explicites de la transaction.

---

## 11.3 Lost update

Écrasement silencieux d’une modification concurrente.

---

## 11.4 Optimistic concurrency

Stratégie supposant que les conflits sont rares et les détectant au moment de l’écriture.

---

## 11.5 Pessimistic locking

Verrou pris avant ou pendant la modification afin d’empêcher une écriture concurrente.

---

## 11.6 Concurrency token

Valeur utilisée pour vérifier qu’une ressource n’a pas changé depuis son chargement.

---

## 11.7 Expected Version

Version connue par l’appelant lorsqu’il demande une modification.

---

## 11.8 Write skew

Deux transactions valident individuellement leurs changements, mais enfreignent ensemble un invariant multi-lignes.

---

## 11.9 Serialization failure

Erreur PostgreSQL indiquant que la transaction doit être rejouée pour préserver la sérialisabilité.

---

## 11.10 Deadlock

Cycle de dépendances entre transactions attendant mutuellement leurs verrous.

---

## 11.11 Uncertain commit

Situation où le client ne sait pas avec certitude si le commit a été accepté avant une rupture de connexion.

---

# 12. Option A — Dernière écriture gagnante

## 12.1 Description

Les mises à jour écrasent toujours la valeur courante.

---

## 12.2 Avantages

- simple ;
- aucune erreur de concurrence ;
- expérience apparemment fluide ;
- peu de colonnes supplémentaires.

---

## 12.3 Inconvénients

- perte de changements ;
- absence de détection ;
- comportement difficile à expliquer ;
- audit incohérent ;
- erreurs silencieuses.

---

## 12.4 Risques

- statut écrasé ;
- contenu publié avec une ancienne version ;
- restauration d’une valeur obsolète ;
- suppression annulant une mise à jour légitime.

---

## 12.5 Réversibilité

```text
faible après perte d’une modification
```

---

# 13. Option B — Verrou pessimiste généralisé

## 13.1 Description

Toute modification charge les lignes avec :

```sql
FOR UPDATE
```

---

## 13.2 Avantages

- empêche les mises à jour simultanées ;
- logique explicite ;
- utile pour certains invariants ;
- détection immédiate des conflits.

---

## 13.3 Inconvénients

- contention ;
- attente ;
- deadlocks ;
- transactions plus longues ;
- mauvaise expérience administrateur ;
- faible capacité à monter en charge.

---

## 13.4 Risques

- verrou conservé pendant une interaction utilisateur ;
- saturation du pool ;
- jobs bloqués ;
- indisponibilité partielle.

---

## 13.5 Réversibilité

```text
élevée
```

---

# 14. Option C — Isolation SERIALIZABLE généralisée

## 14.1 Description

Toutes les commandes utilisent :

```text
SERIALIZABLE
```

---

## 14.2 Avantages

- modèle de cohérence fort ;
- prévention de plusieurs anomalies ;
- raisonnement théorique simplifié.

---

## 14.3 Inconvénients

- serialization failures ;
- retries fréquents ;
- coût supérieur ;
- conflits inutiles ;
- complexité des commandes non idempotentes.

---

## 14.4 Risques

- retry d’un effet externe ;
- dégradation sous charge ;
- comportements difficiles à diagnostiquer ;
- dépendance à des retries généralisés.

---

## 14.5 Réversibilité

```text
élevée
```

---

# 15. Option D — READ COMMITTED, versions et verrous ciblés

## 15.1 Description

La stratégie combine :

- `READ COMMITTED` ;
- concurrence optimiste ;
- contraintes ;
- verrous ciblés ;
- `SERIALIZABLE` uniquement lorsque nécessaire.

---

## 15.2 Avantages

- bonne performance ;
- conflits visibles ;
- faible contention ;
- compatibilité EF Core ;
- traitement adapté par cas ;
- transactions courtes ;
- retries limités.

---

## 15.3 Inconvénients

- discipline nécessaire ;
- colonne Version ;
- traitement des conflits ;
- tests de concurrence ;
- analyse des invariants multi-lignes.

---

## 15.4 Risques

- oubli d’un concurrency token ;
- verrou manquant ;
- retry non idempotent ;
- ordre de verrou incohérent.

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

| Critère | Poids | Last-write-wins | Verrous généralisés | SERIALIZABLE global | Stratégie ciblée |
|---|---:|---:|---:|---:|---:|
| Protection lost update | 5 | 1 | 5 | 5 | 5 |
| Performance | 4 | 5 | 2 | 3 | 5 |
| Faible contention | 4 | 5 | 1 | 3 | 5 |
| Expérience utilisateur | 4 | 2 | 2 | 3 | 5 |
| Compatibilité EF Core | 4 | 5 | 4 | 4 | 5 |
| Simplicité des retries | 4 | 5 | 4 | 2 | 4 |
| Invariants multi-lignes | 5 | 1 | 4 | 5 | 5 |
| Observabilité | 3 | 2 | 4 | 4 | 5 |
| Maintenabilité | 5 | 2 | 2 | 3 | 5 |
| Testabilité | 4 | 2 | 4 | 4 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons que :

```text
READ COMMITTED
```

est le niveau d’isolation par défaut.

Les agrégats administrables utilisent :

```text
Version bigint
```

comme concurrency token explicite.

Les invariants sont protégés en priorité par :

```text
contraintes PostgreSQL

index uniques

foreign keys

check constraints

concurrence optimiste
```

Les verrous pessimistes et `SERIALIZABLE` sont utilisés seulement pour les cas qui ne peuvent pas être protégés correctement par ces mécanismes.

---

# 18. Propriétaire de la transaction

La couche Application définit la frontière du cas d’usage.

---

## 18.1 Domain

Domain ne démarre pas de transaction.

---

## 18.2 UI

Les composants Razor et Blazor ne démarrent pas de transaction.

---

## 18.3 Endpoints

Les endpoints délèguent à un handler Application.

---

## 18.4 Infrastructure

Infrastructure implémente le mécanisme transactionnel avec EF Core et Npgsql.

---

# 19. Transaction runner

Une abstraction conceptuelle est utilisée :

```csharp
public interface IApplicationTransactionRunner
{
    Task<T> ExecuteAsync<T>(
        TransactionDescriptor descriptor,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
```

---

## 19.1 Descriptor

Il peut préciser :

```text
OperationId

IsolationLevel

RetryProfile

TimeoutProfile

ReadOnly

IdempotencyRequirement
```

---

## 19.2 Pas de transaction arbitraire

Un module ne choisit pas librement une isolation ou un retry en dehors des profils approuvés.

---

# 20. Transactions explicites

La baseline privilégie :

```text
DbContext.Database.BeginTransactionAsync
```

ou un mécanisme Npgsql explicite équivalent.

---

## 20.1 `TransactionScope`

Les transactions ambiantes avec :

```text
TransactionScope
```

sont interdites par défaut.

---

## 20.2 Raisons

- frontières invisibles ;
- promotion potentielle ;
- async complexe ;
- connexion supplémentaire ;
- transaction distribuée involontaire.

---

## 20.3 Exception

Une utilisation exige une justification et des tests spécifiques.

---

# 21. Durée de vie du DbContext

Un `DbContext` appartient à une unité d’exécution :

- commande HTTP ;
- handler de job ;
- opération CLI ;
- transaction d’import.

---

## 21.1 Threads

Un `DbContext` n’est jamais partagé entre plusieurs threads concurrents.

---

## 21.2 Singleton

Un `DbContext` n’est jamais singleton.

---

## 21.3 Transaction

Le contexte participant à une transaction n’est pas réutilisé après un échec irréversible.

---

# 22. Unité transactionnelle d’une commande

Une commande métier possède idéalement :

```text
un chargement

une modification

un SaveChanges

un commit
```

---

## 22.1 Plusieurs SaveChanges

Plusieurs appels sont autorisés dans une même transaction uniquement lorsque :

- une clé générée est nécessaire ;
- une fonction PostgreSQL impose une étape ;
- l’algorithme est documenté ;
- le rollback couvre toutes les étapes.

---

## 22.2 SaveChanges dispersé

Les entités et repositories ne doivent pas appeler `SaveChanges` de manière autonome.

---

# 23. Transactions multi-modules

Plusieurs modules peuvent participer à la même transaction lorsque :

- ils utilisent la même base ;
- le cas d’usage exige l’atomicité ;
- leurs dépendances restent explicites ;
- ils utilisent la même connexion et transaction.

---

## 23.1 Interdiction

Une transaction PostgreSQL ne couvre pas :

- un autre serveur de base ;
- SMTP ;
- CRM ;
- object storage ;
- provider AI.

---

## 23.2 Coordination externe

Utiliser l’outbox.

---

# 24. Niveau `READ COMMITTED`

Le niveau par défaut fournit un snapshot par statement PostgreSQL.

---

## 24.1 Usage

- création de lead ;
- édition de contenu ;
- gestion de session ;
- archivage ;
- création d’outbox ;
- gestion de job ;
- administration ordinaire.

---

## 24.2 Limite

Il ne protège pas automatiquement contre :

- lost update ;
- write skew ;
- lecture répétée différente ;
- phantom.

Ces risques sont traités explicitement.

---

# 25. Niveau `REPEATABLE READ`

Il est utilisé pour :

- export logique cohérent ;
- lecture multi-étapes devant partager un snapshot ;
- génération de rapports cohérents ;
- certains plans en lecture seule.

---

## 25.1 Read-only

Les snapshots d’export utilisent de préférence :

```text
READ ONLY
REPEATABLE READ
```

---

## 25.2 Durée

Une transaction snapshot longue doit être surveillée en raison de :

- MVCC ;
- vacuum ;
- stockage ;
- WAL ;
- charge.

---

# 26. Niveau `SERIALIZABLE`

Il est réservé aux invariants critiques difficiles à exprimer autrement.

---

## 26.1 Exemples possibles

- maintien garanti d’au moins un administrateur actif ;
- allocation unique d’une ressource métier rare ;
- workflow multi-lignes sensible au write skew ;
- planification exclusive critique.

---

## 26.2 Préférence

Avant `SERIALIZABLE`, évaluer :

- contrainte unique ;
- index partiel ;
- exclusion constraint ;
- ligne sentinelle ;
- verrou ciblé ;
- fonction transactionnelle.

---

## 26.3 Retry obligatoire

Une commande sérialisable doit définir son comportement sur :

```text
SQLSTATE 40001
```

---

# 27. `READ UNCOMMITTED`

PostgreSQL le traite comme `READ COMMITTED`.

Il n’est pas utilisé comme profil distinct.

---

# 28. Concurrence optimiste

Les agrégats éditables utilisent une version explicite.

---

## 28.1 Modèle

```text
Version bigint NOT NULL
```

---

## 28.2 Valeur initiale

La valeur initiale recommandée est :

```text
1
```

---

## 28.3 Incrément

Chaque modification métier incrémente la version.

---

## 28.4 Requête conceptuelle

```sql
UPDATE app.content_pages
SET title = @title,
    version = version + 1
WHERE content_page_id = @id
  AND version = @expected_version;
```

---

## 28.5 Aucun résultat

Si aucune ligne n’est modifiée :

```text
concurrency conflict
```

---

# 29. Pourquoi ne pas utiliser uniquement `xmin`

La baseline n’utilise pas `xmin` comme contrat public de concurrence.

---

## 29.1 Raisons

- colonne système PostgreSQL ;
- valeur modifiée lors d’un restore ;
- contrat difficile à exposer ;
- faible portabilité ;
- sémantique liée à l’implémentation PostgreSQL.

---

## 29.2 Usage interne

`xmin` peut être utilisé dans un diagnostic ou une optimisation explicitement bornée.

Il ne remplace pas la colonne Version du contrat métier.

---

# 30. Configuration EF Core

La propriété Version est configurée comme :

```text
concurrency token
```

---

## 30.1 Original value

EF Core inclut la valeur originale dans la clause de mise à jour.

---

## 30.2 Mise à jour

Le modèle incrémente la nouvelle version avant ou pendant la persistance.

---

## 30.3 Exception

Un échec produit une exception de concurrence transformée en erreur applicative stable.

---

# 31. Agrégats nécessitant une version

Au minimum :

```text
contenus éditables

leads administrables

médias modifiables

rôles et configurations d’identité

feature flags

kill switches

politiques runtime

Retention Holds

demandes de confidentialité

configurations d’intégrations
```

---

## 31.1 Données append-only

Une table réellement append-only n’a pas nécessairement besoin d’un concurrency token.

Exemples :

- événements d’audit ;
- tentatives de jobs ;
- historiques.

---

# 32. Version attendue dans l’interface

Les formulaires administratifs transmettent une version attendue.

---

## 32.1 Blazor

La version peut être conservée dans le modèle de formulaire.

---

## 32.2 HTTP futur

Une API peut utiliser :

```text
ETag
If-Match
```

---

## 32.3 Protection

La version fournie par le client n’est pas considérée comme une autorisation.

Toutes les permissions restent vérifiées.

---

# 33. ETag

La représentation conceptuelle peut être :

```text
"v17"
```

---

## 33.1 Faible ou fort

Le contrat doit préciser le type d’ETag.

Pour une version métier exacte, un ETag fort peut être utilisé.

---

## 33.2 Donnée sensible

L’ETag ne doit pas encoder :

- email ;
- secret ;
- contenu ;
- hash personnel.

---

# 34. Réponse en cas de conflit

Un conflit de concurrence retourne une erreur stable.

Pour HTTP :

```text
409 Conflict
```

---

## 34.1 Réponse

Elle peut inclure :

- Error Code ;
- Resource ID ;
- Expected Version ;
- Current Version ;
- Correlation ID.

---

## 34.2 Données

La réponse ne retourne pas automatiquement toute la ressource actuelle.

---

## 34.3 Interface

L’administrateur peut :

- recharger ;
- comparer ;
- recommencer ;
- abandonner.

---

# 35. Pas de merge automatique universel

L’application ne fusionne pas automatiquement deux modifications concurrentes.

---

## 35.1 Raisons

Une fusion générique peut :

- perdre une intention ;
- contourner une validation ;
- produire un état incohérent ;
- masquer un conflit.

---

## 35.2 Fusion métier

Un module peut définir une stratégie explicite champ par champ.

---

# 36. Archivage et suppression

Une action d’archivage, de restauration ou de suppression utilise la Version attendue.

---

## 36.1 Exemple

Un lead modifié après l’ouverture de la modal ne doit pas être archivé sur la base d’un état devenu obsolète sans avertissement.

---

## 36.2 Action critique

Les contrôles de step-up et audit restent applicables.

---

# 37. Contraintes PostgreSQL

Les invariants pouvant être exprimés dans PostgreSQL doivent l’être.

---

## 37.1 Exemples

```text
NOT NULL

UNIQUE

FOREIGN KEY

CHECK

EXCLUDE

index unique partiel
```

---

## 37.2 Validation applicative

La validation applicative améliore l’erreur utilisateur.

La contrainte PostgreSQL reste l’autorité face à la concurrence.

---

# 38. Unicité

Une vérification telle que :

```text
cet email existe-t-il ?
```

ne suffit pas.

---

## 38.1 Race

Deux transactions peuvent obtenir :

```text
non
```

puis insérer simultanément.

---

## 38.2 Décision

Utiliser :

```text
index unique
```

puis transformer la violation en erreur métier.

---

# 39. Index uniques partiels

Un index partiel peut protéger un invariant dépendant d’un état.

Exemple conceptuel :

```sql
CREATE UNIQUE INDEX ...
ON identity.admin_invitations(normalized_email)
WHERE status IN ('pending', 'delivered');
```

---

## 39.1 Tests

Le prédicat et les transitions doivent être testés sous concurrence.

---

# 40. Exclusion constraints

Une exclusion constraint peut protéger :

- intervalles ;
- réservations ;
- plages temporelles ;
- chevauchements interdits.

---

## 40.1 Extension

Son usage peut nécessiter une extension approuvée.

---

# 41. Verrous pessimistes ciblés

Les verrous pessimistes sont utilisés uniquement à l’intérieur d’une transaction courte.

---

## 41.1 Types principaux

```text
FOR UPDATE

FOR NO KEY UPDATE

FOR SHARE

FOR KEY SHARE

SKIP LOCKED

NOWAIT
```

---

## 41.2 Choix

Le niveau le moins contraignant satisfaisant l’invariant doit être utilisé.

---

# 42. `FOR UPDATE`

Utilisé lorsqu’une ligne existante doit être modifiée de manière exclusive.

---

## 42.1 Exemple

- transition d’un état global ;
- consommation d’un token ;
- allocation d’un identifiant métier ;
- clôture d’une demande.

---

## 42.2 Durée

Le traitement après le lock doit rester court.

---

# 43. `SKIP LOCKED`

Utilisé notamment pour le claim concurrent de jobs défini dans l’ADR-0017.

---

## 43.1 Usage

```text
plusieurs workers
→ chaque worker ignore les lignes déjà verrouillées
→ chaque job n’est revendiqué que par un worker à la fois
```

---

## 43.2 Limite

`SKIP LOCKED` ne constitue pas une stratégie de lecture métier ordinaire.

---

# 44. `NOWAIT`

Peut être utilisé lorsque l’application préfère :

```text
échec immédiat
```

à :

```text
attente d’un verrou
```

---

## 44.1 Réponse

L’erreur est transformée en :

- conflit ;
- busy ;
- retry possible.

---

# 45. Ordre des verrous

Les lignes et ressources sont verrouillées dans un ordre stable.

---

## 45.1 Exemple

```text
tri par type de ressource
puis par identifiant
```

---

## 45.2 Objectif

Réduire les deadlocks.

---

## 45.3 Module

Un module manipulant plusieurs agrégats documente son ordre de verrouillage.

---

# 46. Verrous de table

Les verrous explicites de table sont interdits dans les commandes métier ordinaires.

---

## 46.1 Exceptions

- migration ;
- maintenance ;
- import contrôlé ;
- procédure opérateur.

---

## 46.2 Plan

L’impact et le temps d’attente doivent être documentés.

---

# 47. Advisory locks

Les advisory locks PostgreSQL peuvent être utilisés pour une coordination rare ne correspondant pas directement à une ligne.

---

## 47.1 Type retenu

Privilégier :

```text
transaction-level advisory lock
```

---

## 47.2 Session locks

Les advisory locks de session sont interdits par défaut.

---

## 47.3 Clé

La clé est dérivée d’un namespace et d’un identifiant stable.

---

## 47.4 Données

La clé ne doit pas être un hash non maîtrisé permettant des collisions silencieuses non analysées.

---

## 47.5 Exemples

- une génération d’export complet par environnement ;
- une opération de rétention globale ;
- un restore reconciliation ;
- une transition d’epoch.

---

# 48. Deadlocks

Les deadlocks peuvent survenir malgré les précautions.

---

## 48.1 Prévention

- transactions courtes ;
- ordre stable ;
- index adaptés ;
- aucune interaction réseau ;
- aucun délai ;
- batch limité ;
- faible nombre de lignes.

---

## 48.2 Détection

PostgreSQL retourne notamment :

```text
SQLSTATE 40P01
```

---

## 48.3 Traitement

La transaction entière est annulée puis éventuellement rejouée.

---

# 49. Lock timeout

Les profils PostgreSQL de l’ADR-0027 imposent un `lock_timeout`.

---

## 49.1 Web

Une commande interactive ne doit pas attendre longtemps.

---

## 49.2 Worker

Un worker peut utiliser un délai différent, mais borné.

---

## 49.3 Timeout

Un lock timeout n’est pas toujours rejoué automatiquement.

Le cas d’usage détermine la stratégie.

---

# 50. Statement timeout

Chaque rôle possède un `statement_timeout`.

---

## 50.1 Transaction

Le timeout d’une requête annule la transaction PostgreSQL en cours.

---

## 50.2 Code

Le handler doit :

- arrêter ses traitements ;
- rollback ;
- retourner une erreur stable ;
- ne pas tenter de continuer avec le même contexte transactionnel.

---

# 51. Durée cible des transactions

Les transactions web ordinaires doivent généralement rester :

```text
nettement inférieures à une seconde
```

---

## 51.1 Seuil d’observation

Une transaction web dépassant régulièrement quelques secondes doit être analysée.

---

## 51.2 Worker

Les transitions d’état d’un worker restent courtes.

Le traitement lourd a lieu hors transaction.

---

## 51.3 Export

Un snapshot `REPEATABLE READ` constitue une exception documentée et surveillée.

---

# 52. Aucun appel externe dans une transaction

Sont interdits avant le commit :

```text
SMTP

HTTP provider

CRM

ERP

AI

webhook

object storage externe

analytics

DNS métier

attente utilisateur
```

---

## 52.1 Raison

Un appel externe :

- augmente la durée ;
- garde des verrous ;
- peut timeout ;
- peut réussir malgré un rollback ;
- ne participe pas à PostgreSQL.

---

# 53. Outbox

Une commande nécessitant un effet externe écrit un message outbox dans la transaction.

---

## 53.1 Atomique

États possibles :

```text
commit
→ donnée métier et outbox présentes

rollback
→ aucune des deux
```

---

## 53.2 Après commit

Le worker traite l’effet externe.

---

## 53.3 Duplication

Les handlers externes restent idempotents conformément à l’ADR-0017.

---

# 54. Audit transactionnel

Un événement d’audit obligatoire est ajouté dans la même transaction que l’action sensible.

---

## 54.1 Échec

Si l’audit échoue :

```text
rollback de l’action
```

---

## 54.2 Opérations longues

Les étapes d’une opération longue produisent plusieurs événements séparés.

---

# 55. Fichiers et transactions

PostgreSQL ne peut pas rendre atomique une modification de filesystem.

---

## 55.1 Pattern retenu

```text
écrire ou recevoir le fichier dans une zone staging

valider le fichier

transaction PostgreSQL
→ créer les métadonnées et l’intention de promotion

commit

job
→ promouvoir ou finaliser le fichier

réconciliation en cas d’échec
```

---

## 55.2 Suppression

La suppression utilise :

```text
marquage ou intention transactionnelle
→ commit
→ suppression physique idempotente
→ vérification
```

---

# 56. État des médias

Les médias utilisent des états explicites.

Exemples :

```text
staged

pending-promotion

available

deletion-pending

deleted

failed
```

---

## 56.1 Visibilité

Un média n’est pas considéré comme disponible avant la réussite de sa promotion.

---

# 57. Jobs longs

Un job long ne conserve pas une transaction PostgreSQL pendant son calcul.

---

## 57.1 Pattern

```text
transaction courte
→ claim et lecture des références

hors transaction
→ traitement long

transaction courte
→ validation du lease et écriture du résultat
```

---

## 57.2 Lease

La finalisation vérifie que le worker possède encore le bon Execution Token.

---

# 58. Suppressions et rétention

Une suppression multi-stockages utilise plusieurs checkpoints transactionnels.

---

## 58.1 Pas de transaction globale

Le workflow n’essaie pas d’englober :

- PostgreSQL ;
- filesystem ;
- backups ;
- fournisseurs ;

dans une transaction unique.

---

## 58.2 Compensation

Il utilise :

- états ;
- idempotence ;
- retries ;
- rapport ;
- réconciliation.

---

# 59. Imports

Un import volumineux utilise des batches.

---

## 59.1 Chaque batch

- transaction bornée ;
- idempotency key ;
- checkpoint ;
- validation ;
- rapport.

---

## 59.2 Transaction unique

Une transaction unique peut être utilisée uniquement pour un import petit et borné.

---

## 59.3 Visibilité

La cible reste fermée jusqu’à vérification finale.

---

# 60. Exports

Un export logique cohérent peut utiliser :

```text
READ ONLY
REPEATABLE READ
```

---

## 60.1 Effet

L’export n’écrit aucune donnée métier dans la transaction de snapshot.

---

## 60.2 État de l’export

Les transitions d’état de l’export utilisent des transactions ordinaires séparées.

---

# 61. Migrations

Les migrations suivent l’ADR-0004 et l’ADR-0027.

---

## 61.1 Frontière

La migration constitue une opération opérateur distincte.

---

## 61.2 Appel externe

Une migration ne dépend pas d’un appel réseau externe pour modifier le schéma.

---

## 61.3 Transaction

Chaque migration indique si elle est :

- transactionnelle ;
- partiellement transactionnelle ;
- non transactionnelle.

---

# 62. Transactions et interface utilisateur

Une transaction ne doit jamais rester ouverte pendant :

- une modal ;
- une confirmation ;
- une saisie ;
- un step-up ;
- un challenge MFA ;
- un upload navigateur.

---

## 62.1 Pattern

```text
charger la ressource et sa Version

afficher

recevoir la confirmation

ouvrir une nouvelle transaction

vérifier la Version

appliquer
```

---

# 63. Annulation

Le `CancellationToken` est propagé jusqu’à EF Core et Npgsql.

---

## 63.1 Client déconnecté

Une déconnexion HTTP peut annuler une commande non encore commitée.

---

## 63.2 Après commit

Une annulation reçue après le commit ne doit pas être interprétée comme un rollback réussi.

---

## 63.3 Réponse incertaine

L’idempotence permet au client de vérifier ou rejouer sans duplication.

---

# 64. Idempotence des commandes

Une commande susceptible d’être rejouée possède :

```text
Idempotency Key
```

---

## 64.1 Cas concernés

- soumission publique ;
- création d’un export ;
- création d’un job opérateur ;
- création d’un paiement futur ;
- opération déclenchée par webhook ;
- commande pouvant subir un uncertain commit.

---

## 64.2 Persistance

La clé et le résultat logique sont persistés dans la même transaction que l’effet métier.

---

## 64.3 Nouvelle payload

La même clé avec un payload différent est refusée.

---

# 65. Uncertain commit

Une connexion peut être perdue après que PostgreSQL a accepté le commit, mais avant que l’application ne reçoive la confirmation.

---

## 65.1 Interdiction

Le client ne doit pas recréer aveuglément la ressource avec une nouvelle clé.

---

## 65.2 Traitement

```text
rejouer avec la même Idempotency Key

ou interroger l’état par Operation ID
```

---

## 65.3 Identifiants

Les IDs applicatifs peuvent être générés avant la transaction afin de faciliter la vérification.

---

# 66. Retries transactionnels

Les retries sont gérés au niveau du transaction runner.

---

## 66.1 Erreurs potentiellement rejouables

```text
40001 — serialization_failure

40P01 — deadlock_detected
```

Selon le cas :

```text
55P03 — lock_not_available
```

peut aussi être traité comme retryable.

---

## 66.2 Erreurs non transitoires

Ne sont pas rejouées automatiquement :

```text
23505 — unique_violation

23503 — foreign_key_violation

23514 — check_violation

42501 — insufficient_privilege

erreur de validation métier
```

---

# 67. Profil de retry

Baseline conceptuelle :

```text
maximum 3 tentatives

backoff exponentiel court

jitter

nouveau DbContext

nouvelle transaction
```

---

## 67.1 Valeurs

Les valeurs exactes peuvent varier selon le cas d’usage.

---

## 67.2 Échec final

Après épuisement :

- erreur stable ;
- métrique ;
- log ;
- diagnostic ;
- aucun retry infini.

---

# 68. Conditions préalables au retry

Une transaction n’est rejouée que si :

```text
toute l’unité est rejouable

aucun effet externe n’a été exécuté

aucun état local non reconstructible n’a été consommé

l’idempotence est suffisante

le CancellationToken reste valide
```

---

## 68.1 Aléatoire

Les valeurs aléatoires nécessaires doivent être :

- générées avant ;
- ou stabilisées par Operation ID ;

afin que le retry conserve la même intention.

---

## 68.2 Horodatage

Les timestamps métier importants doivent rester cohérents pendant les retries.

---

# 69. Execution strategies EF Core

Une execution strategy EF Core peut être utilisée uniquement à travers le transaction runner.

---

## 69.1 Interdiction

Un retry global transparent autour de tous les appels `SaveChanges` est interdit.

---

## 69.2 Transaction utilisateur

La stratégie doit réexécuter l’ensemble du delegate transactionnel.

---

## 69.3 Effets externes

Ils restent hors du delegate.

---

# 70. Lecture seule

Les requêtes de consultation ordinaires utilisent :

```text
AsNoTracking
```

lorsque le tracking n’est pas nécessaire.

---

## 70.1 Transaction explicite

Aucune transaction explicite n’est ouverte pour une lecture simple.

---

## 70.2 Snapshot cohérent

Une transaction read-only est utilisée lorsque plusieurs lectures doivent représenter le même instant logique.

---

# 71. Pagination

La pagination de grands jeux de données privilégie :

```text
keyset pagination
```

---

## 71.1 Offset

L’offset reste possible pour de petits volumes administratifs.

---

## 71.2 Concurrence

Une pagination best-effort peut voir apparaître ou disparaître des éléments.

Le contrat doit l’accepter ou utiliser un snapshot/export.

---

# 72. Projections et read models

Une projection peut être légèrement décalée par rapport à la transaction métier lorsqu’elle est mise à jour de manière asynchrone.

---

## 72.1 Contrat

L’interface doit distinguer :

- donnée transactionnelle ;
- projection éventuellement différée.

---

## 72.2 Écriture

Une projection ne devient pas la source de vérité d’une commande sans décision explicite.

---

# 73. Transactions et caches

Un cache ne doit pas être invalidé avant le commit.

---

## 73.1 Pattern

```text
commit
→ événement post-commit ou outbox
→ invalidation
```

---

## 73.2 Échec

Une invalidation échouée utilise une stratégie de réconciliation.

---

## 73.3 ADR dédié

La politique complète de cache sera définie dans l’ADR-0030.

---

# 74. Sécurité

## 74.1 Actifs

- intégrité des données ;
- audit ;
- jobs ;
- sessions ;
- rôles ;
- médias ;
- suppressions ;
- états métier ;
- contraintes.

---

## 74.2 Menaces

- lost update ;
- double effet ;
- suppression concurrente ;
- lock abusif ;
- transaction longue ;
- retry dupliqué ;
- external call avant commit ;
- write skew ;
- deadlock provoqué ;
- contournement d’une contrainte.

---

## 74.3 Contrôles

- version ;
- contraintes ;
- locks ciblés ;
- isolation ;
- timeouts ;
- outbox ;
- idempotence ;
- retries bornés ;
- audit ;
- tests concurrentiels.

---

# 75. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Deux admins écrasent un contenu | Version attendue |
| Deux créations avec même email | Index unique |
| Deux workers prennent le même job | `FOR UPDATE SKIP LOCKED` |
| Email envoyé avant rollback | Outbox |
| Appel CRM pendant un lock | Appel externe après commit |
| Deadlock | Ordre de verrou et retry borné |
| Retry créant deux leads | Idempotency Key |
| Restore d’un formulaire obsolète | Concurrency token |
| Write skew sur invariant critique | Contrainte, lock ou SERIALIZABLE |
| Transaction bloquée | Lock timeout et statement timeout |
| Média DB sans fichier | État staging et réconciliation |
| Suppression partielle | Workflow idempotent à checkpoints |

---

# 76. Observabilité

Événements recommandés :

```text
DatabaseTransactionStarted

DatabaseTransactionCommitted

DatabaseTransactionRolledBack

DatabaseTransactionRetried

DatabaseConcurrencyConflict

DatabaseDeadlockDetected

DatabaseSerializationFailure

DatabaseLockTimeout

DatabaseStatementTimeout

DatabaseConstraintViolation

DatabaseUncertainCommitDetected
```

---

## 76.1 Propriétés autorisées

```text
OperationId

CommandType

IsolationLevel

Attempt

Result

SqlState

ErrorCategory

Environment

Duration
```

---

## 76.2 Données interdites

```text
SQL complet avec valeurs

paramètres

email

téléphone

contenu de lead

connection string

secret
```

---

# 77. Métriques

Exemples :

```text
database transactions

database transaction duration

database transaction rollbacks

database transaction retries

database concurrency conflicts

database deadlocks

database serialization failures

database lock timeouts

database statement timeouts
```

---

## 77.1 Labels autorisés

```text
operation_type

isolation_level

result

retry_reason

environment
```

---

## 77.2 Labels interdits

```text
ResourceId

UserId

LeadId

TransactionId individuel

SQL

TraceId
```

---

# 78. Alertes

Alertes possibles :

```text
Hausse des conflits de concurrence

Deadlocks répétés

Retries sérialisables fréquents

Transactions web longues

Lock timeouts en hausse

Statement timeouts en hausse

Transactions rollbackées après audit attendu

SaveChanges concurrents anormaux

Pool saturé par transactions longues
```

---

## 78.1 Runbook

Chaque alerte importante référence une procédure.

---

# 79. Audit

Les détails techniques de chaque transaction n’appartiennent pas à la piste d’audit.

---

## 79.1 Audit métier

La piste conserve l’action sensible et son résultat.

---

## 79.2 Conflit refusé

Une tentative sensible refusée pour conflit peut être auditée lorsque cela apporte une valeur opérationnelle.

---

## 79.3 Deadlock

Un deadlock appartient principalement aux logs et métriques techniques.

---

# 80. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-TX-001` | Transaction requise |
| `SF-TX-002` | Transaction imbriquée non autorisée |
| `SF-TX-003` | Isolation non autorisée |
| `SF-TX-004` | Transaction read-only requise |
| `SF-TX-005` | Transaction trop longue |
| `SF-TX-006` | Transaction annulée |
| `SF-TX-007` | Commit échoué |
| `SF-TX-008` | Rollback échoué |
| `SF-TX-009` | Appel externe détecté dans la transaction |
| `SF-TX-010` | Retry transactionnel impossible |
| `SF-TX-011` | Nombre maximal de retries atteint |
| `SF-TX-012` | DbContext invalide après erreur |
| `SF-TX-013` | Transaction distribuée interdite |
| `SF-CONCURRENCY-001` | Version attendue absente |
| `SF-CONCURRENCY-002` | Conflit de version |
| `SF-CONCURRENCY-003` | Ressource supprimée pendant la modification |
| `SF-CONCURRENCY-004` | Ressource archivée pendant la modification |
| `SF-CONCURRENCY-005` | Fusion automatique interdite |
| `SF-CONCURRENCY-006` | Concurrency token absent |
| `SF-CONCURRENCY-007` | Version invalide |
| `SF-CONSTRAINT-001` | Violation d’unicité |
| `SF-CONSTRAINT-002` | Violation de clé étrangère |
| `SF-CONSTRAINT-003` | Violation de check constraint |
| `SF-CONSTRAINT-004` | Invariant non protégé par PostgreSQL |
| `SF-LOCK-001` | Verrou impossible à obtenir |
| `SF-LOCK-002` | Lock timeout |
| `SF-LOCK-003` | Deadlock détecté |
| `SF-LOCK-004` | Ordre de verrou incohérent |
| `SF-LOCK-005` | Verrou de table interdit |
| `SF-LOCK-006` | Advisory lock invalide |
| `SF-LOCK-007` | Advisory session lock interdit |
| `SF-SERIALIZATION-001` | Serialization failure |
| `SF-SERIALIZATION-002` | Commande non rejouable |
| `SF-IDEMPOTENCY-001` | Idempotency Key requise |
| `SF-IDEMPOTENCY-002` | Idempotency Key invalide |
| `SF-IDEMPOTENCY-003` | Payload incompatible avec la clé |
| `SF-IDEMPOTENCY-004` | Résultat existant |
| `SF-IDEMPOTENCY-005` | Commit incertain |
| `SF-OUTBOX-TX-001` | Message outbox absent |
| `SF-OUTBOX-TX-002` | Outbox non écrite dans la transaction |
| `SF-AUDIT-TX-001` | Audit absent de la transaction |
| `SF-MEDIA-TX-001` | Média disponible avant promotion |
| `SF-MEDIA-TX-002` | État média incohérent |
| `SF-IMPORT-TX-001` | Batch d’import non transactionnel |
| `SF-EXPORT-TX-001` | Snapshot d’export incohérent |

---

# 81. Tests unitaires

Tester notamment :

- Transaction Descriptor ;
- sélection de l’isolation ;
- retry profile ;
- classification des SQLSTATE ;
- incrément de Version ;
- conflit ;
- mapping d’erreur ;
- Idempotency Key ;
- ordre de lock.

---

# 82. Tests PostgreSQL réels

Les garanties doivent être testées sur PostgreSQL réel.

---

## 82.1 Scénarios

- transactions concurrentes ;
- constraints ;
- locks ;
- deadlocks ;
- timeouts ;
- isolation ;
- retries ;
- snapshots ;
- advisory locks.

---

# 83. Test de lost update

Scénario :

1. transaction A charge Version 4 ;
2. transaction B charge Version 4 ;
3. A commit Version 5 ;
4. B tente son update avec Version 4.

Résultat :

```text
B reçoit un conflit
la modification de A est préservée
```

---

# 84. Test d’unicité

Deux transactions tentent de créer la même valeur unique.

Résultat :

```text
une seule réussit

l’autre reçoit une erreur métier contrôlée
```

---

# 85. Test d’archivage concurrent

Scénario :

1. A ouvre la modal d’archivage ;
2. B modifie le lead ;
3. A confirme avec l’ancienne version.

Résultat :

```text
archivage refusé pour conflit
```

---

# 86. Test de suppression concurrente

Vérifier :

- modification contre suppression ;
- restauration contre suppression ;
- export contre anonymisation ;
- média remplacé contre suppression.

---

# 87. Tests de contraintes multi-lignes

Couvrir les invariants nécessitant :

- index partiel ;
- lock ;
- SERIALIZABLE ;
- ligne sentinelle.

---

## 87.1 Exemple

Deux administrateurs ne doivent pas pouvoir supprimer simultanément les deux derniers comptes privilégiés.

---

# 88. Test de deadlock

Créer volontairement deux transactions prenant les lignes dans un ordre opposé.

Vérifier :

- détection ;
- rollback ;
- retry borné ;
- métrique ;
- absence d’effet dupliqué.

---

# 89. Test de serialization failure

Exécuter une commande `SERIALIZABLE` concurrente.

Vérifier :

- SQLSTATE ;
- nouveau DbContext ;
- nouvelle transaction ;
- même Operation ID ;
- maximum attempts.

---

# 90. Test de lock timeout

Maintenir un verrou puis exécuter une commande concurrente.

Vérifier :

- délai borné ;
- rollback ;
- erreur contrôlée ;
- absence de retry non prévu.

---

# 91. Test d’annulation

Annuler le `CancellationToken` pendant :

- lecture ;
- modification ;
- SaveChanges ;
- attente de lock.

Résultat :

```text
aucun commit partiel
```

---

# 92. Test outbox atomique

Vérifier :

```text
donnée commitée
→ outbox présente

transaction rollbackée
→ donnée absente
→ outbox absente
```

---

# 93. Test audit atomique

Vérifier :

```text
action sensible commitée
→ audit présent

audit échoué
→ action rollbackée
```

---

# 94. Test d’effet externe

Un fake provider doit confirmer qu’aucun appel n’est exécuté avant le commit.

---

## 94.1 Commit échoué

Aucun appel externe.

---

## 94.2 Commit réussi

Le job peut ensuite exécuter l’effet.

---

# 95. Test média

Simuler :

- fichier staged ;
- transaction commit ;
- promotion échouée ;
- retry ;
- promotion réussie ;
- suppression concurrente.

---

# 96. Test de job long

Vérifier qu’aucune transaction PostgreSQL ne reste ouverte pendant le traitement CPU ou réseau long.

---

# 97. Test d’export snapshot

Pendant un export `REPEATABLE READ`, modifier les données dans une autre transaction.

Le dataset exporté doit correspondre au snapshot annoncé.

---

# 98. Test d’uncertain commit

Simuler une perte de connexion autour du commit.

Vérifier que la même Idempotency Key permet de retrouver le résultat sans duplication.

---

# 99. Test de retry non idempotent

Une commande sans stratégie d’idempotence ne doit pas être rejouée automatiquement après une erreur de résultat incertain.

---

# 100. Tests de performance

Mesurer :

- durée des transactions ;
- locks ;
- deadlocks ;
- retries ;
- contention ;
- pool ;
- transactions snapshot ;
- batches d’import.

---

# 101. Tests multiplateformes

Les contrats et tests unitaires fonctionnent sur :

```text
Windows x64

Linux x64
```

Les garanties de transaction et concurrence sont validées avec PostgreSQL réel sur Linux.

---

# 102. Quality gates

Une contribution est bloquée lorsque :

```text
Un appel externe est exécuté dans une transaction.

Un composant UI démarre une transaction.

Un repository appelle SaveChanges de manière autonome.

Une entité administrable n’a pas de concurrency token.

Une mise à jour ignore la Version attendue.

Une contrainte d’unicité existe uniquement dans le code.

Un retry réexécute seulement SaveChanges.

Un retry non borné est ajouté.

Une commande non idempotente est rejouée automatiquement.

Un verrou de table est utilisé dans une commande ordinaire.

Un advisory lock de session est utilisé.

Une transaction reste ouverte pendant un traitement long.

Un conflit est converti en succès last-write-wins.

Une outbox est écrite après le commit.

Un audit obligatoire est écrit après le commit.
```

---

# 103. Quality gates de production

La production est bloquée lorsque :

```text
Les profils transactionnels sont inconnus.

Les migrations de Version requises sont absentes.

Les contraintes attendues ne sont pas présentes.

Les timeouts PostgreSQL sont illimités.

Les tests de concurrence critiques échouent.

Un handler externe s’exécute avant commit.

Le transaction runner n’est pas utilisé.

Les retries sérialisables ne sont pas bornés.

Les opérations critiques ne possèdent pas d’idempotence.

Les métriques de conflits et deadlocks sont absentes.

Le rôle runtime peut modifier les contraintes.
```

---

# 104. CI/CD

La CI doit exécuter :

```text
tests unitaires transactionnels

tests PostgreSQL

tests de lost update

tests de constraints

tests de deadlocks

tests de serialization

tests de lock timeout

tests d’idempotence

tests outbox

tests audit

tests média

tests import et export

analyse statique des appels externes
```

---

## 104.1 Parallélisme réel

Les tests de concurrence doivent réellement exécuter plusieurs connexions et transactions.

---

## 104.2 Base simulée

Une base en mémoire ne suffit pas.

---

# 105. Analyse statique

La CI peut rechercher ou interdire :

```text
TransactionScope

Database.Migrate dans le runtime

BeginTransaction dans UI

SaveChanges dans Domain

SaveChanges dans une boucle

appel HttpClient dans un bloc transactionnel

Task.Delay dans un bloc transactionnel
```

---

## 105.1 Limite

L’analyse statique complète les tests et revues.

---

# 106. Règles pour les modules

Chaque commande modifiant des données doit déclarer :

```text
TransactionProfile

IsolationLevel

ConcurrencyPolicy

Constraints

LockPolicy éventuelle

RetryPolicy

IdempotencyPolicy

OutboxEvents

AuditEvents
```

---

## 106.1 Interdictions

Un module ne doit pas :

- ouvrir une transaction dans Domain ;
- utiliser `SERIALIZABLE` sans justification ;
- ignorer les conflits ;
- appeler un provider externe ;
- ajouter un retry universel ;
- dépendre d’un lock global.

---

# 107. Règles pour les customizations

Une customization suit les mêmes contrats.

---

## 107.1 Nouvelle entité éditable

Elle doit fournir :

- Version ;
- mapping EF Core ;
- tests de lost update ;
- réponse de conflit ;
- contraintes.

---

## 107.2 SQL personnalisé

Tout verrou ou isolation spécifique doit être documenté et testé.

---

# 108. Règles pour LogicGate

LogicGate doit :

- maintenir les profils ;
- analyser les conflits ;
- surveiller les deadlocks ;
- vérifier les constraints ;
- garder les transactions courtes ;
- tester les retries ;
- revoir les imports ;
- maintenir les runbooks.

---

## 108.1 LogicGate ne doit pas

- résoudre un conflit en écrasant les données ;
- augmenter les timeouts sans diagnostic ;
- passer tout le système en SERIALIZABLE ;
- ajouter des locks globaux ;
- mettre un appel SMTP dans une transaction ;
- désactiver un test de concurrence instable.

---

# 109. Règles pour Codex

Codex peut :

- implémenter le transaction runner ;
- ajouter les Versions ;
- créer les contraintes ;
- ajouter les tests ;
- analyser un deadlock ;
- créer les mappings d’erreur ;
- documenter les runbooks.

---

## 109.1 Codex ne doit pas

```text
Modifier une transaction de production

Lancer un lock réel

Appliquer une migration production

Augmenter un timeout production

Ajouter un retry infini

Ajouter last-write-wins

Appeler un provider dans une transaction

Désactiver une contrainte

Ajouter un verrou de table sans décision
```

---

## 109.2 Décision humaine

Une décision humaine est requise pour :

- nouveau profil SERIALIZABLE ;
- verrou pessimiste global ;
- advisory lock ;
- nouvelle stratégie d’idempotence ;
- modification des timeouts ;
- suppression d’un concurrency token ;
- changement d’une contrainte critique ;
- migration de versions existantes.

---

# 110. Anti-patterns interdits

## 110.1 Last-write-wins

```csharp
entity.Title = request.Title;
await dbContext.SaveChangesAsync();
```

sans Version attendue.

---

## 110.2 Unicité applicative uniquement

```csharp
if (!await ExistsAsync(email))
{
    await InsertAsync(email);
}
```

sans index unique.

---

## 110.3 Appel HTTP dans une transaction

```text
BeginTransaction
→ sauvegarder
→ appeler CRM
→ commit
```

---

## 110.4 Transaction utilisateur

La transaction reste ouverte pendant une modal de confirmation.

---

## 110.5 Retry de `SaveChanges` seulement

Le contexte et les données précédemment chargées restent obsolètes.

---

## 110.6 `SERIALIZABLE` partout

Tous les cas d’usage paient le coût d’invariants qu’ils ne possèdent pas.

---

## 110.7 Verrou de table

Une mise à jour de lead bloque toute la table.

---

## 110.8 Advisory lock de session

Un lock survit plus longtemps que la transaction attendue.

---

## 110.9 SaveChanges dans une boucle

Chaque élément commit indépendamment sans stratégie.

---

## 110.10 Outbox après commit

Une donnée existe mais sa notification n’est jamais planifiée.

---

## 110.11 Audit après commit

L’action réussit mais sa piste d’audit manque.

---

## 110.12 Traitement long sous lock

Une transformation d’image garde un verrou PostgreSQL pendant plusieurs secondes ou minutes.

---

# 111. Conséquences positives

La décision apporte :

- protection contre les lost updates ;
- atomicité ;
- contraintes robustes ;
- faible contention ;
- conflits explicites ;
- outbox fiable ;
- audit cohérent ;
- retries contrôlés ;
- meilleure sécurité des jobs ;
- opérations longues découplées ;
- meilleure observabilité ;
- tests de concurrence reproductibles.

---

# 112. Conséquences négatives

La décision entraîne :

- colonne Version ;
- gestion des conflits ;
- transaction runner ;
- tests concurrentiels ;
- contraintes supplémentaires ;
- analyse des locks ;
- retries ciblés ;
- états intermédiaires pour les médias ;
- davantage de discipline applicative.

---

# 113. Risques acceptés

## 113.1 Conflits utilisateur

Deux administrateurs peuvent recevoir une erreur de conflit.

Ce comportement est préférable à une perte silencieuse.

---

## 113.2 Retry dupliqué mal conçu

Une commande déclarée à tort rejouable peut dupliquer un effet.

Réduction :

- idempotence ;
- outbox ;
- tests ;
- profils explicites.

---

## 113.3 Deadlocks résiduels

Des deadlocks rares peuvent rester possibles.

Réduction :

- ordre de verrou ;
- transactions courtes ;
- retry ;
- métriques.

---

## 113.4 Snapshot long

Un export complet peut maintenir un snapshot pendant une durée importante.

Réduction :

- maintenance window ;
- surveillance ;
- limites ;
- profil spécifique.

---

# 114. Mesures compensatoires

- Version explicite ;
- contraintes ;
- index uniques ;
- locks ciblés ;
- isolation ciblée ;
- outbox ;
- audit transactionnel ;
- idempotence ;
- retries bornés ;
- timeouts ;
- order de lock ;
- métriques ;
- tests PostgreSQL ;
- runbooks.

---

# 115. Performance

## 115.1 Concurrency token

Le coût de la colonne Version et de sa comparaison est faible.

---

## 115.2 Index

Les contraintes et recherches doivent utiliser des index appropriés.

---

## 115.3 Transactions courtes

Elles réduisent :

- locks ;
- contention ;
- MVCC ;
- pool usage ;
- deadlocks.

---

## 115.4 Retries

Un taux élevé de retry signale un problème d’architecture ou de charge.

Il ne doit pas être considéré comme un fonctionnement normal.

---

# 116. Coûts

## 116.1 Coûts supplémentaires

- développement ;
- tests ;
- diagnostics ;
- colonnes Version ;
- monitoring ;
- gestion des conflits.

---

## 116.2 Coûts évités

- perte de données ;
- états partiels ;
- doubles effets ;
- deadlocks durables ;
- imports incohérents ;
- audit absent ;
- suppression concurrente incorrecte.

---

# 117. Maintenabilité

Pour chaque commande, il doit être possible de répondre :

```text
Quelle est sa frontière transactionnelle ?

Quel niveau d’isolation utilise-t-elle ?

Quelles lignes modifie-t-elle ?

Quel concurrency token vérifie-t-elle ?

Quelles contraintes protègent ses invariants ?

Prend-elle un verrou ?

Peut-elle être rejouée ?

Quelle est son Idempotency Key ?

Quels événements outbox produit-elle ?

Quel audit produit-elle ?

Que se passe-t-il en cas de conflit ?
```

---

# 118. Compatibilité

## 118.1 PostgreSQL

Les profils utilisent les niveaux et erreurs PostgreSQL supportés.

---

## 118.2 EF Core

Les concurrency tokens et transactions sont configurés avec les versions EF Core supportées.

---

## 118.3 Import

Les exports et imports incluent la Version lorsqu’elle appartient au contrat portable ou réinitialisent explicitement la version selon le profil.

---

## 118.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

L’ajout de Version à un projet existant nécessite une migration et l’adaptation des formulaires.

---

# 119. Migration depuis last-write-wins

Le processus est :

```text
identifier les agrégats éditables

ajouter Version

initialiser Version = 1

configurer EF Core

ajouter Expected Version aux commandes

adapter les formulaires

mapper les conflits

ajouter les tests concurrentiels

déployer
```

---

# 120. Migration depuis des transactions longues

Le processus est :

```text
instrumenter les durées

identifier les appels externes

identifier les traitements lourds

découper les étapes

ajouter outbox et jobs

réduire la transaction aux écritures

tester
```

---

# 121. Migration depuis des retries automatiques

Le processus est :

```text
inventorier les retries

identifier les effets non idempotents

désactiver les retries génériques

créer les profils

ajouter les Idempotency Keys

rejouer toute la transaction

tester les uncertain commits
```

---

# 122. Transfert client

Le handover doit inclure :

- profils transactionnels ;
- agrégats versionnés ;
- contraintes ;
- locks spécifiques ;
- retry policies ;
- idempotency tables ;
- runbooks deadlock ;
- métriques ;
- rapports de tests ;
- opérations longues particulières.

---

# 123. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- d’ouvrir une transaction réelle ;
- de prendre un verrou ;
- de tuer une transaction ;
- de modifier les timeouts ;
- d’appliquer une migration ;
- de rejouer une commande réelle ;
- de modifier une contrainte ;
- de résoudre manuellement un conflit.

---

# 124. Réversibilité

## 124.1 Classification

```text
élevée avant déploiement

moyenne après ajout des Versions

faible après un écrasement silencieux déjà produit
```

---

## 124.2 Retrait de Version

Retirer un concurrency token réduirait les garanties.

Un nouvel ADR serait nécessaire.

---

# 125. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- transaction descriptors ;
- isolation profiles ;
- retry profiles ;
- erreurs ;
- Operation IDs.

---

## Étape 2 — Transaction runner

Implémenter :

- transactions explicites ;
- rollback ;
- cancellation ;
- nouveau contexte par retry ;
- classification SQLSTATE.

---

## Étape 3 — Concurrence optimiste

Ajouter Version aux premiers agrégats :

- leads ;
- contenus ;
- configurations administratives.

---

## Étape 4 — Contraintes

Inventorier et créer :

- unique ;
- foreign keys ;
- checks ;
- index partiels.

---

## Étape 5 — Outbox et audit

Vérifier leur inclusion dans chaque transaction concernée.

---

## Étape 6 — Jobs

Formaliser :

- claims ;
- locks ;
- leases ;
- finalisation transactionnelle.

---

## Étape 7 — Médias

Ajouter :

- staging ;
- promotion ;
- états ;
- réconciliation.

---

## Étape 8 — Retries

Ajouter :

- deadlock ;
- serialization ;
- idempotence ;
- uncertain commit.

---

## Étape 9 — Observabilité

Ajouter :

- durées ;
- conflits ;
- locks ;
- retries ;
- alertes.

---

## Étape 10 — Tests

Ajouter les scénarios PostgreSQL concurrents et les quality gates.

---

# 126. Tâches liées proposées

```text
SF-TASK-395 — Define transaction and isolation profiles

SF-TASK-396 — Add the application transaction runner

SF-TASK-397 — Add PostgreSQL SQLSTATE error classification

SF-TASK-398 — Add explicit aggregate Version concurrency tokens

SF-TASK-399 — Add lead optimistic-concurrency handling

SF-TASK-400 — Add content optimistic-concurrency handling

SF-TASK-401 — Add administrator conflict-resolution responses

SF-TASK-402 — Add database uniqueness and invariant constraints

SF-TASK-403 — Add bounded transaction retry profiles

SF-TASK-404 — Add deadlock and serialization retry handling

SF-TASK-405 — Add command idempotency persistence

SF-TASK-406 — Add uncertain-commit recovery

SF-TASK-407 — Verify transactional outbox boundaries

SF-TASK-408 — Verify transactional audit boundaries

SF-TASK-409 — Add media staging and post-commit promotion

SF-TASK-410 — Add transaction-duration observability

SF-TASK-411 — Add PostgreSQL lost-update tests

SF-TASK-412 — Add PostgreSQL deadlock and lock-timeout tests

SF-TASK-413 — Add serializable-invariant tests

SF-TASK-414 — Add transaction-boundary quality gates
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 127. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
READ COMMITTED est le niveau par défaut.

Les agrégats éditables possèdent une Version explicite.

Les commandes transmettent la Version attendue.

Deux modifications concurrentes ne s’écrasent pas.

Un conflit retourne une erreur stable.

Les contraintes d’unicité existent dans PostgreSQL.

Le transaction runner possède des frontières explicites.

Les composants UI n’ouvrent pas de transaction.

Domain n’ouvre pas de transaction.

Aucun appel externe n’a lieu dans une transaction.

L’outbox est écrite atomiquement.

L’audit est écrit atomiquement.

Les jobs utilisent des transactions courtes.

Les traitements longs ont lieu hors transaction.

Les verrous sont ciblés et ordonnés.

Les verrous de table ordinaires sont absents.

Les advisory locks sont transactionnels et documentés.

Les deadlocks sont détectés et rejoués de manière bornée.

Les serialization failures sont rejouées uniquement
pour les commandes compatibles.

Les retries utilisent un nouveau DbContext.

Les Idempotency Keys empêchent les doublons.

Un uncertain commit peut être réconcilié.

Les médias utilisent staging et promotion post-commit.

Les exports cohérents utilisent un snapshot explicite.

Les timeouts et annulations provoquent un rollback.

Les métriques de durée, conflits et retries existent.

Les tests utilisent plusieurs connexions PostgreSQL réelles.

Les tests fonctionnent sur Windows et Linux.
```

---

# 128. Conditions de révision

Réexaminer cette décision si :

- plusieurs bases doivent participer à une commande ;
- une architecture event sourcing est introduite ;
- une réplication en lecture devient la source des requêtes ;
- les write conflicts deviennent fréquents ;
- une base multi-tenant est adoptée ;
- un broker transactionnel externe est introduit ;
- un système de locks distribués devient nécessaire ;
- les imports deviennent massifs et fréquents ;
- une synchronisation active-active est ajoutée ;
- plusieurs régions écrivent simultanément ;
- une fonctionnalité nécessite des transactions longues ;
- un invariant ne peut plus être protégé localement dans PostgreSQL.

---

# 129. Indicateurs à suivre

- transactions ;
- durée moyenne ;
- durée p95 et p99 ;
- rollbacks ;
- conflicts ;
- deadlocks ;
- serialization failures ;
- retries ;
- retries épuisés ;
- lock timeouts ;
- statement timeouts ;
- uncertain commits ;
- Idempotency Key conflicts ;
- snapshots longs ;
- transactions worker longues ;
- violations de contraintes.

---

# 130. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
les conflits deviennent très fréquents

des updates ignorent la Version

les deadlocks augmentent

les retries masquent une mauvaise conception

les transactions web durent plusieurs secondes

les exports bloquent vacuum

des appels HTTP apparaissent dans les transactions

des verrous de table sont ajoutés

les timeouts sont régulièrement augmentés

des SaveChanges apparaissent dans des boucles

les uncertain commits créent des doublons

l’outbox est écrite après le commit
```

---

# 131. Conséquences observées

Cette section est append-only après mise en œuvre.

## 131.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
du transaction runner,
des concurrency tokens
et des tests de concurrence PostgreSQL.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet de référence ;
- commande ;
- isolation ;
- durée ;
- conflit ;
- retry ;
- incident ou bénéfice ;
- action proposée.

---

# 132. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
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
- [0023-data-retention-deletion-and-privacy-lifecycle.md](0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0024-client-data-export-and-portability.md](0024-client-data-export-and-portability.md)
- [0025-third-party-integrations-and-egress-control.md](0025-third-party-integrations-and-egress-control.md)
- [0026-container-image-build-and-runtime-hardening.md](0026-container-image-build-and-runtime-hardening.md)
- [0027-database-roles-and-runtime-least-privilege.md](0027-database-roles-and-runtime-least-privilege.md)

---

# 133. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 128 |

---

# 134. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 135. Synthèse de la décision

```text
Frontière
→ une commande Application
→ une transaction explicite
→ un commit ou rollback

Isolation
→ READ COMMITTED par défaut
→ REPEATABLE READ pour les snapshots
→ SERIALIZABLE uniquement pour invariants critiques

Concurrence
→ Version bigint
→ Expected Version
→ update conditionnel
→ aucun écrasement silencieux
→ 409 Conflict

Contraintes
→ unique
→ foreign key
→ check
→ index partiel
→ PostgreSQL comme autorité

Locks
→ ciblés
→ transactions courtes
→ ordre stable
→ SKIP LOCKED pour les jobs
→ advisory xact locks seulement si nécessaire
→ aucun verrou de table ordinaire

Effets externes
→ jamais dans la transaction
→ outbox
→ commit
→ job
→ provider

Audit
→ même transaction
→ échec audit = rollback

Retries
→ deadlock
→ serialization failure
→ maximum borné
→ nouveau DbContext
→ commande rejouable uniquement

Idempotence
→ Idempotency Key
→ résultat persistant
→ protection uncertain commit
→ aucun double effet logique

Médias
→ staging
→ transaction metadata
→ promotion post-commit
→ réconciliation

Transactions longues
→ interdites pour le web
→ traitement lourd hors transaction
→ snapshots d’export surveillés

Qualité
→ tests lost update
→ tests contraintes
→ tests deadlock
→ tests serialization
→ tests outbox
→ tests audit
→ PostgreSQL réel
```

> Une transaction garantit que plusieurs modifications sont validées ensemble. Elle ne garantit pas qu’un utilisateur travaille encore sur la dernière version d’une ressource, ni qu’un effet externe sera cohérent avec le commit. SiteFactory combine donc transactions explicites, versions attendues, contraintes PostgreSQL, outbox et idempotence afin qu’aucune concurrence ne se transforme silencieusement en perte de données.
