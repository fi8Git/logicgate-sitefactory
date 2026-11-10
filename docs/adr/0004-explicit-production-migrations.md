---
adrId: ADR-0004
title: Keep production database migrations explicit and operator-controlled
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
  - ./0001-one-repository-and-database-per-client.md
  - ./0003-postgresql-as-primary-database.md
targetVersion: 0.1.0
---

# ADR-0004 — Migrations de production explicites et contrôlées par un opérateur

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Le [Migrator public](../../tests/PostgreSql/migrations.md) applique explicitement
la migration des seules métadonnées runtime, puis permet un bootstrap distinct,
transactionnel et idempotent. Son usage actuel est borné à Development/Test,
sur une base existante ; ce n'est pas un outil de migration production.
Web et Worker n'appellent jamais Migrate/EnsureCreated au démarrage.
Les images, rôles opérationnels, déploiements, backups et procédures de
production ci-dessous sont des exigences cibles, pas des opérations livrées
ou implicitement autorisées. `sample-project` est un identifiant fictif.

## 1. Statut

```text
accepted
```

Les migrations de base de données d’un environnement de production SiteFactory doivent être :

- préparées ;
- inspectées ;
- testées ;
- sauvegardées ;
- déclenchées explicitement ;
- exécutées avec des credentials dédiés ;
- vérifiées ;
- consignées.

L’application web de production ne doit pas modifier automatiquement le schéma PostgreSQL pendant son démarrage.

---

## 2. Résumé

LogicGate SiteFactory interdit l’application automatique des migrations EF Core au démarrage d’une application de production.

Les appels tels que :

```csharp
await database.Database.MigrateAsync();
```

ou :

```csharp
database.Database.EnsureCreated();
```

ne doivent pas être intégrés au chemin de démarrage de la production.

Le flux retenu est :

```text
migration préparée dans le repository client
→ tests sur base vide
→ tests d’upgrade
→ revue
→ release versionnée
→ plan de migration
→ backup de production vérifié
→ approbation humaine
→ exécution par un runner dédié
→ vérification PostgreSQL
→ déploiement de l’application compatible
→ smoke tests
→ surveillance
→ rapport
```

Le processus pourra être automatisé techniquement, mais l’application en production restera soumise à une approbation explicite.

---

## 3. Contexte

Les projets SiteFactory utilisent :

```text
PostgreSQL
+ Entity Framework Core
+ migrations stockées dans le repository client
```

Chaque client et environnement possède une base distincte conformément à l’ADR-0001.

Les migrations pourront modifier :

- tables ;
- colonnes ;
- index ;
- contraintes ;
- relations ;
- données de référence ;
- données métier ;
- structures Identity ;
- structures propres aux modules.

Une migration incorrecte peut provoquer :

- une indisponibilité ;
- une perte de données ;
- une corruption ;
- une incompatibilité applicative ;
- un verrouillage prolongé ;
- une impossibilité de rollback applicatif ;
- un échec affectant les formulaires ou l’administration.

L’application doit donc démarrer comme un processus applicatif et non comme un opérateur d’infrastructure disposant implicitement du droit de modifier la base.

---

## 4. Problème à résoudre

Comment appliquer les évolutions du schéma PostgreSQL de manière fiable et industrialisable tout en évitant :

- une migration automatique non inspectée ;
- une modification de données lors d’un simple redémarrage ;
- une exécution concurrente par plusieurs instances ;
- l’utilisation permanente de privilèges DDL par l’application ;
- une migration sans backup ;
- un déploiement applicatif incompatible avec le schéma ;
- une perte de traçabilité ;
- un rollback improvisé ?

La solution doit rester compatible avec :

- une petite équipe ;
- Docker Compose ;
- un VPS Linux ;
- PostgreSQL ;
- EF Core ;
- GitHub Actions ;
- un repository par client ;
- une maintenance progressivement automatisée.

---

## 5. Portée

Cette décision couvre :

- migrations EF Core ;
- migrations SQL complémentaires ;
- production ;
- staging ;
- environnements temporaires ;
- credentials de migration ;
- runner de migration ;
- backup préalable ;
- contrôles de version ;
- verrouillage ;
- transaction ;
- données existantes ;
- rapport de migration ;
- gestion des échecs ;
- stratégie de rollback ;
- migrations sensibles ;
- automatisation future.

---

## 6. Hors portée

Cette décision ne définit pas :

- toutes les conventions de modélisation EF Core ;
- le contenu précis de chaque migration ;
- le fournisseur VPS ;
- la haute disponibilité PostgreSQL ;
- la réplication ;
- le point-in-time recovery ;
- l’horaire contractuel des fenêtres de maintenance ;
- la stratégie détaillée de chaque projet sur mesure ;
- un système universel de migration multi-provider ;
- une autorisation d’exécuter une migration réelle.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Protection des données**
2. **Contrôle des opérations de production**
3. **Traçabilité**
4. **Séparation des privilèges**
5. **Compatibilité entre application et schéma**
6. **Capacité de rollback ou restauration**
7. **Prévention des exécutions concurrentes**
8. **Reproductibilité**
9. **Testabilité**
10. **Réduction du temps d’indisponibilité**
11. **Automatisation contrôlée**
12. **Lisibilité opérationnelle**
13. **Transfert au client**
14. **Compatibilité Docker et PostgreSQL**

---

## 8. Contraintes

La décision doit respecter les contraintes suivantes :

```text
PostgreSQL

Entity Framework Core

Migrations dans le repository client

Pas d’auto-migration en production

Backup avant migration de production

Compte runtime sans privilèges DDL

Compte de migration séparé

Validation humaine avant exécution production

Image et migrations versionnées

Rapport structuré

Rollback préparé

Aucun secret dans les logs
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- la majorité des migrations peut être préparée avant le déploiement ;
- les migrations sont liées à une release applicative identifiable ;
- un court mode maintenance reste acceptable pour certaines opérations ;
- LogicGate peut exécuter un runner ponctuel sur le réseau privé PostgreSQL ;
- les migrations complexes peuvent être divisées en plusieurs releases ;
- un backup logique peut être produit avant l’opération ;
- la base peut être testée avec des données représentatives en staging ;
- les déploiements de production restent opérés par un humain dans le MVP.

---

# 10. Option A — Migration automatique au démarrage

## 10.1 Description

Chaque instance applicative exécute automatiquement les migrations pendantes au démarrage.

Exemple :

```csharp
using var scope = app.Services.CreateScope();

var dbContext = scope.ServiceProvider
    .GetRequiredService<ApplicationDbContext>();

await dbContext.Database.MigrateAsync();
```

---

## 10.2 Avantages

- simplicité apparente ;
- pas de commande opérateur séparée ;
- schéma automatiquement mis à jour ;
- pratique en développement ;
- déploiement rapide sur un projet simple.

---

## 10.3 Inconvénients

- le démarrage modifie les données ;
- l’application nécessite des privilèges DDL permanents ;
- plusieurs instances peuvent migrer simultanément ;
- le backup préalable n’est pas garanti ;
- une migration longue bloque le démarrage ;
- un simple redémarrage peut déclencher une opération destructive ;
- le rollback applicatif devient incertain ;
- l’échec peut créer une boucle de redémarrage ;
- la visibilité opérateur est réduite.

---

## 10.4 Risques

- migration accidentelle ;
- corruption ;
- lock PostgreSQL prolongé ;
- indisponibilité ;
- application partiellement démarrée ;
- credentials trop privilégiés ;
- plusieurs migrateurs concurrents ;
- déploiement d’une mauvaise version sur une mauvaise base.

---

## 10.5 Réversibilité

```text
faible
```

La modification peut déjà avoir transformé les données avant que l’opérateur ne puisse intervenir.

---

# 11. Option B — Migration automatique dans le pipeline de production

## 11.1 Description

Le pipeline CI/CD applique automatiquement les migrations après un merge ou avant le déploiement.

---

## 11.2 Avantages

- automatisation ;
- exécution unique ;
- logs centralisés ;
- intégration au déploiement ;
- moins d’actions manuelles.

---

## 11.3 Inconvénients

- un merge peut déclencher une modification de production ;
- credentials de migration présents dans la CI ;
- risque d’exécution sur une mauvaise cible ;
- backup pas nécessairement vérifié ;
- rollback complexe ;
- approbation insuffisante ;
- dépendance au fournisseur CI.

---

## 11.4 Risques

- compromission de la supply chain ;
- secret de migration exposé ;
- erreur de branche ou environnement ;
- pipeline relancé ;
- migration appliquée avant validation de l’image ;
- opération destructive automatisée.

---

## 11.5 Réversibilité

```text
faible à moyenne
```

Le pipeline peut être sécurisé avec des environnements protégés, mais l’opération reste automatique après son déclenchement.

---

# 12. Option C — Scripts SQL exécutés manuellement

## 12.1 Description

Les migrations EF Core sont converties en scripts SQL puis exécutées manuellement par un opérateur.

---

## 12.2 Avantages

- SQL inspectable ;
- contrôle humain ;
- possibilité d’utiliser les outils PostgreSQL ;
- séparation du runtime ;
- bonne traçabilité si le processus est maîtrisé.

---

## 12.3 Inconvénients

- exécution manuelle sujette aux erreurs ;
- risque de script tronqué ou partiel ;
- gestion de l’historique EF Core ;
- difficulté de garantir le bon script ;
- variables et environnements à contrôler ;
- processus plus difficile à standardiser seul.

---

## 12.4 Risques

- copier-coller incorrect ;
- exécution dans la mauvaise base ;
- script modifié après revue ;
- historique de migration incohérent ;
- relance partielle ;
- absence de rapport structuré.

---

## 12.5 Réversibilité

```text
moyenne
```

Le contrôle est explicite, mais la procédure manuelle doit être fortement encadrée.

---

# 13. Option D — Runner de migration versionné et déclenché explicitement

## 13.1 Description

Une release produit un exécutable, bundle ou conteneur de migration versionné.

L’opérateur :

1. vérifie le plan ;
2. vérifie le backup ;
3. autorise l’opération ;
4. injecte temporairement les credentials de migration ;
5. exécute le runner ;
6. vérifie le résultat.

---

## 13.2 Avantages

- séparation du runtime ;
- credentials temporaires ;
- version exacte ;
- application unique des migrations ;
- logs et rapports structurés ;
- automatisation possible ;
- compatibilité Docker ;
- contrôle humain ;
- tests reproductibles ;
- pas de SDK requis durablement sur le VPS.

---

## 13.3 Inconvénients

- composant supplémentaire à produire ;
- scripts et procédures à maintenir ;
- orchestration plus détaillée ;
- backup et vérification nécessaires ;
- risque opérateur toujours présent ;
- besoin de gérer les migrations complexes.

---

## 13.4 Risques

- mauvais environnement sélectionné ;
- runner incompatible ;
- credentials incorrects ;
- bundle incomplet ;
- verrouillage long ;
- rapport non collecté ;
- migration partiellement transactionnelle.

---

## 13.5 Réversibilité

```text
moyenne à élevée
```

L’opération reste contrôlée et peut être précédée d’un backup vérifié.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Startup | Pipeline automatique | SQL manuel | Runner explicite |
|---|---:|---:|---:|---:|---:|
| Protection des données | 5 | 1 | 3 | 4 | 5 |
| Contrôle humain | 5 | 1 | 3 | 5 | 5 |
| Séparation des privilèges | 5 | 1 | 4 | 5 | 5 |
| Traçabilité | 5 | 2 | 4 | 3 | 5 |
| Reproductibilité | 4 | 3 | 4 | 3 | 5 |
| Backup préalable | 5 | 1 | 3 | 4 | 5 |
| Prévention de concurrence | 4 | 2 | 4 | 4 | 5 |
| Automatisation future | 3 | 5 | 5 | 2 | 5 |
| Simplicité initiale | 3 | 5 | 4 | 3 | 3 |
| Rollback maîtrisable | 5 | 1 | 3 | 4 | 5 |
| Transfert client | 4 | 2 | 3 | 4 | 5 |

Le runner versionné et explicitement déclenché répond le mieux aux priorités SiteFactory.

---

# 15. Décision

Nous décidons que les migrations de production SiteFactory seront exécutées par une opération explicite, distincte du démarrage applicatif.

Le mécanisme cible est :

```text
runner de migration versionné
```

pouvant prendre la forme de :

- projet console .NET ;
- EF Core migration bundle ;
- conteneur one-shot ;
- commande SiteFactory future.

Le choix d’implémentation exact pourra évoluer sans remettre en cause les règles du présent ADR.

---

## 15.1 Interdictions

L’application web de production ne doit pas exécuter :

```csharp
Database.Migrate()
```

```csharp
Database.MigrateAsync()
```

```csharp
Database.EnsureCreated()
```

```csharp
Database.EnsureCreatedAsync()
```

dans son démarrage normal.

---

## 15.2 Pas de migration implicite

Aucune migration ne doit être déclenchée implicitement par :

- redémarrage ;
- health check ;
- login ;
- première requête ;
- seed administrateur ;
- chargement d’un module ;
- changement de configuration ;
- déploiement d’image sans approbation.

---

# 16. Justification

Le runner explicite permet de conserver :

- une application runtime peu privilégiée ;
- une décision humaine avant modification ;
- un backup préalable ;
- une version de migration exacte ;
- une seule exécution contrôlée ;
- une meilleure capacité de diagnostic ;
- une séparation claire entre déploiement et évolution du schéma.

Cette approche demande davantage de préparation, mais réduit fortement le risque qu’une opération de production importante soit cachée dans le démarrage applicatif.

---

# 17. Relation avec les releases

Chaque migration appartient à une release cliente identifiable.

La release doit préciser :

```text
ApplicationVersion

GitTag

CommitSha

MigrationFrom

MigrationTo

MigrationIds

RunnerVersion

ImageTag

ImageDigest éventuel
```

---

## 17.1 Migration sans release

Une migration isolée reste possible pour une correction opératoire exceptionnelle, mais elle doit posséder :

- une version ;
- une branche ;
- une revue ;
- des tests ;
- un rapport ;
- un artefact immuable.

---

## 17.2 Artefact modifié

Un runner publié ne doit jamais être remplacé sous la même version.

---

# 18. Propriété des migrations

Conformément à l’ADR-0003, les migrations EF Core appartiennent au repository client.

Elles reflètent :

- modules installés ;
- entités ;
- customizations ;
- historique ;
- version applicative.

---

## 18.1 Packages génériques

Un package générique ne décide pas seul du schéma final d’une application cliente composée.

---

## 18.2 Nom des migrations

Les migrations doivent utiliser des noms explicites.

Exemples :

```text
AddLeadArchiveState

AddPortfolioAdvancedModule

CreateContentRevisionHistory
```

Éviter :

```text
UpdateDatabase

Changes

Fix
```

---

# 19. Comptes PostgreSQL

## 19.1 Compte runtime

Le compte runtime ne possède pas les privilèges nécessaires pour modifier le schéma.

Il accède uniquement aux objets requis par l’application.

---

## 19.2 Compte migration

Le compte migration est distinct.

Il possède uniquement les droits nécessaires à l’application des migrations sur la base concernée.

---

## 19.3 Compte opérateur

Un compte opérateur plus privilégié peut être utilisé pour :

- création initiale ;
- récupération ;
- opérations exceptionnelles.

Il ne doit pas être injecté dans le runtime applicatif.

---

## 19.4 Durée de disponibilité

Le secret de migration ne doit pas rester accessible en permanence au conteneur web.

---

# 20. Runner de migration

## 20.1 Responsabilités

Le runner doit :

- identifier la release ;
- identifier le client ;
- identifier l’environnement ;
- vérifier la base cible ;
- vérifier l’historique actuel ;
- vérifier les migrations attendues ;
- acquérir un verrou ;
- appliquer les migrations ;
- produire un rapport ;
- retourner un code fiable.

---

## 20.2 Non-responsabilités

Le runner ne doit pas :

- déployer l’application ;
- modifier Caddy ;
- changer le DNS ;
- créer silencieusement une base de production ;
- générer un secret ;
- restaurer automatiquement après échec ;
- supprimer une donnée hors migration ;
- choisir seul une stratégie de rollback.

---

# 21. Modes du runner

Modes conceptuels :

```text
check

plan

apply

status

verify
```

---

## 21.1 `check`

Vérifie les préconditions sans modifier la base.

---

## 21.2 `plan`

Présente :

- version actuelle ;
- version cible ;
- migrations ;
- risques ;
- opérations ;
- backup requis ;
- interruption éventuelle.

---

## 21.3 `apply`

Applique uniquement le plan validé.

---

## 21.4 `status`

Affiche l’état courant sans révéler de secret.

---

## 21.5 `verify`

Vérifie l’historique, le schéma attendu et les contrôles post-migration.

---

# 22. Commandes SiteFactory futures

Syntaxe conceptuelle :

```bash
sitefactory migration check \
  --project ../client-project \
  --environment production
```

```bash
sitefactory migration plan \
  --project ../client-project \
  --environment production \
  --target-version 1.4.0
```

```bash
sitefactory migration apply \
  --project ../client-project \
  --environment production \
  --plan migration-plan.json \
  --execute \
  --confirm-migration
```

---

## 22.1 Confirmation

La production nécessite une confirmation spécifique.

Une option générique telle que :

```text
--yes
```

ne doit pas suffire.

---

## 22.2 Secrets

Les secrets ne sont jamais fournis comme arguments de ligne de commande.

---

# 23. Plan de migration

Un plan structuré doit pouvoir être produit :

```text
migration-plan.json
```

---

## 23.1 Contenu conceptuel

```json
{
  "schemaVersion": 1,
  "migrationPlanId": "f0cef537-1a28-40a0-82bc-b88fcd448119",
  "clientSlug": "sample-project",
  "environment": "production",
  "applicationVersion": {
    "from": "1.3.0",
    "to": "1.4.0"
  },
  "database": {
    "provider": "postgresql",
    "currentMigration": "AddLeadSearchIndex",
    "targetMigration": "AddLeadArchiveState"
  },
  "migrations": [
    {
      "id": "202607281200_AddLeadArchiveState",
      "classification": "additive",
      "destructive": false,
      "requiresMaintenanceMode": false
    }
  ],
  "backup": {
    "required": true
  },
  "rollback": {
    "strategy": "restore-backup-and-deploy-previous-version"
  }
}
```

---

## 23.2 Immutabilité

Le plan exécuté doit correspondre au plan présenté.

Une modification des migrations après création du plan invalide celui-ci.

---

# 24. Rapport de migration

Une exécution produit :

```text
migration-report.json
```

---

## 24.1 Contenu conceptuel

```json
{
  "schemaVersion": 1,
  "migrationRunId": "caaf4aa8-f4a5-4a22-840f-76c1b46b9ef8",
  "migrationPlanId": "f0cef537-1a28-40a0-82bc-b88fcd448119",
  "clientSlug": "sample-project",
  "environment": "production",
  "startedAtUtc": "2026-07-28T20:00:00Z",
  "completedAtUtc": "2026-07-28T20:00:12Z",
  "sourceVersion": "1.3.0",
  "targetVersion": "1.4.0",
  "sourceMigration": "AddLeadSearchIndex",
  "targetMigration": "AddLeadArchiveState",
  "backupId": "9e61ec5a-cccf-47be-870c-a9aebf525cb3",
  "status": "success",
  "appliedMigrations": [
    "202607281200_AddLeadArchiveState"
  ],
  "warnings": [],
  "errors": []
}
```

---

## 24.2 Données interdites

Le rapport ne contient pas :

- chaîne de connexion ;
- mot de passe ;
- données SQL ;
- contenu de table ;
- PII ;
- token ;
- valeur de secret.

---

# 25. Préconditions de production

Avant toute migration :

```text
[ ] Client identifié
[ ] Environnement identifié
[ ] Version source connue
[ ] Version cible connue
[ ] Base cible confirmée
[ ] Historique EF Core vérifié
[ ] Migrations inspectées
[ ] Release validée
[ ] Runner versionné disponible
[ ] Backup réussi
[ ] Backup vérifié
[ ] Espace disque suffisant
[ ] Lock opérationnel disponible
[ ] Rollback défini
[ ] Smoke tests définis
[ ] Approbation humaine obtenue
```

---

# 26. Vérification de la cible

Le runner doit refuser une cible incohérente.

Il doit vérifier selon les métadonnées disponibles :

```text
ClientSlug

Environment

Database identifier

Current application version

Current migration

Expected source migration
```

---

## 26.1 Mauvaise base

Une divergence de client ou environnement provoque un arrêt avant toute écriture.

---

## 26.2 Version inattendue

Si la migration source réelle ne correspond pas au plan :

```text
fail closed
```

L’opérateur doit recréer ou réviser le plan.

---

# 27. Backup préalable

Toute migration de production nécessite un backup vérifié, sauf dérogation exceptionnelle documentée.

---

## 27.1 Contenu

Selon la migration :

- PostgreSQL ;
- `App_Data` si la cohérence fichiers-base peut être affectée ;
- Data Protection si l’opération touche Identity ou les clés ;
- métadonnées de version.

---

## 27.2 Vérification

Le runner ou l’opérateur vérifie :

- backup récent ;
- manifest valide ;
- checksum ;
- cible correcte ;
- copie hors VPS lorsque requise.

---

## 27.3 Échec du backup

Une migration ne doit pas commencer si le backup obligatoire échoue.

---

# 28. Verrouillage

Une migration doit acquérir :

```text
un verrou opérationnel
+ un verrou PostgreSQL
```

lorsque cela est possible.

---

## 28.1 Verrou opérationnel

Empêche les opérations concurrentes telles que :

- déploiement ;
- backup cohérent ;
- restauration ;
- import ;
- autre migration.

---

## 28.2 Verrou PostgreSQL

Un advisory lock PostgreSQL peut empêcher plusieurs runners de migrer simultanément la même base.

---

## 28.3 Timeout

Le verrou doit posséder :

- timeout ;
- propriétaire ;
- identifiant de migration ;
- mécanisme de libération.

---

# 29. Maintenance mode

Une migration nécessite un mode maintenance lorsqu’elle peut :

- modifier un contrat utilisé par la version active ;
- bloquer des tables importantes ;
- transformer des données ;
- supprimer ou renommer un objet ;
- rendre des écritures concurrentes dangereuses.

---

## 29.1 Lecture seule

Un mode lecture seule peut suffire pour certaines migrations.

---

## 29.2 Indisponibilité complète

Une courte indisponibilité peut être préférable à une incohérence de données.

---

## 29.3 Communication

La fenêtre doit être communiquée lorsque le contrat ou l’impact le nécessite.

---

# 30. Ordre migration et déploiement

L’ordre dépend du type de migration.

---

## 30.1 Migration additive compatible

Flux recommandé :

```text
backup
→ migration additive
→ déploiement de la nouvelle application
→ vérification
```

---

## 30.2 Migration nécessitant la nouvelle application

Une stratégie progressive peut utiliser :

```text
expand
→ déployer code compatible
→ migrer les données
→ activer le nouveau comportement
→ contract plus tard
```

---

## 30.3 Migration incompatible

Une migration incompatible peut nécessiter :

```text
mode maintenance
→ arrêt des écritures
→ backup
→ migration
→ déploiement
→ vérification
→ reprise du trafic
```

---

## 30.4 Aucun ordre universel

Le runner ne doit pas supposer qu’une migration précède toujours l’application sans plan explicite.

---

# 31. Classification des migrations

Valeurs recommandées :

```text
additive

data-backfill

indexing

behavioral

destructive

identity-sensitive

long-running

non-transactional
```

Une migration peut posséder plusieurs classifications.

---

## 31.1 Additive

Exemples :

- nouvelle table ;
- nouvelle colonne nullable ;
- nouvel index non bloquant selon la méthode ;
- nouvelle contrainte préparée.

---

## 31.2 Data backfill

Transformation ou initialisation de données existantes.

---

## 31.3 Destructive

Suppression ou transformation irréversible potentielle.

---

## 31.4 Long-running

Opération dont la durée ou le verrouillage peut affecter le service.

---

# 32. Migrations destructives

Une migration destructive nécessite :

- validation humaine renforcée ;
- backup ;
- test avec données représentatives ;
- plan de restauration ;
- analyse de la fenêtre ;
- documentation ;
- éventuellement une release dédiée.

---

## 32.1 Exemples

```text
DROP TABLE

DROP COLUMN

Réduction de longueur

Changement de type non réversible

Suppression de données

Ajout NOT NULL sans backfill

Renommage cassant une ancienne version
```

---

## 32.2 Refus par défaut

Une suppression automatique de données est refusée par défaut.

---

# 33. Stratégie expand and contract

Pour réduire les breaking changes :

```text
1. Expand
2. Migrate
3. Switch
4. Observe
5. Contract
```

---

## 33.1 Expand

Ajouter la nouvelle structure sans supprimer l’ancienne.

---

## 33.2 Migrate

Copier ou transformer les données.

---

## 33.3 Switch

Déployer l’application utilisant la nouvelle structure.

---

## 33.4 Observe

Vérifier le fonctionnement sur une période suffisante.

---

## 33.5 Contract

Supprimer l’ancienne structure dans une release ultérieure.

---

# 34. Données de référence

Les données structurelles étroitement liées au schéma peuvent être introduites par migration lorsque :

- elles sont déterministes ;
- elles sont idempotentes ;
- elles ne contiennent pas de secret ;
- elles ne dépendent pas d’un environnement.

---

## 34.1 Données opérationnelles

Les données telles que :

- compte administrateur ;
- mot de passe ;
- configuration SMTP ;
- contenu client ;

ne doivent pas être insérées comme valeurs réelles dans une migration générique.

---

# 35. Seed applicatif

Les seeds opérationnels sont distincts des migrations.

Ils doivent être :

- explicites ;
- idempotents ;
- limités ;
- audités ;
- désactivables ;
- exécutés avec les permissions adaptées.

---

## 35.1 AdminSeed

La récupération ou création initiale d’un administrateur ne doit pas déclencher une migration de schéma.

---

# 36. Transactions

EF Core utilise une transaction lorsque le provider et les opérations le permettent.

---

## 36.1 Limites

Certaines opérations peuvent être :

- non transactionnelles ;
- longues ;
- exécutées hors transaction ;
- incompatibles avec un rollback SQL complet.

Elles doivent être signalées dans le plan.

---

## 36.2 Transaction globale

Une transaction globale trop longue peut provoquer :

- locks ;
- croissance WAL ;
- contention ;
- saturation disque.

La stratégie doit être adaptée au volume.

---

# 37. Migrations de données volumineuses

Une transformation importante peut être séparée du changement de schéma.

Approche possible :

```text
migration de structure
→ job de backfill contrôlé
→ vérification
→ activation
→ nettoyage futur
```

---

## 37.1 Lots

Le backfill peut utiliser :

- lots ;
- checkpoints ;
- reprise ;
- métriques ;
- idempotence.

---

## 37.2 Runtime web

Une transformation volumineuse ne doit pas être cachée dans le démarrage de l’application web.

---

# 38. Index

La création d’un index important doit analyser :

- durée ;
- locks ;
- espace disque ;
- trafic ;
- variante concurrente PostgreSQL ;
- transaction.

---

## 38.1 Index concurrent

L’utilisation de capacités PostgreSQL telles que :

```text
CREATE INDEX CONCURRENTLY
```

peut nécessiter une migration non transactionnelle et une orchestration particulière.

---

## 38.2 Reprise

Un échec d’index concurrent doit être diagnostiqué et nettoyé explicitement.

---

# 39. Concurrence applicative

Pendant la migration, les anciennes instances ne doivent pas écrire dans un schéma incompatible.

---

## 39.1 Arrêt contrôlé

Le déploiement peut nécessiter :

- arrêt des anciennes instances ;
- mode maintenance ;
- suspension des jobs ;
- suspension des formulaires.

---

## 39.2 Blue-green

Une stratégie blue-green future nécessite des migrations rétrocompatibles pendant la coexistence.

---

# 40. Vérification post-migration

Le runner doit vérifier :

```text
Migrations attendues présentes

Aucune migration inattendue

Historique EF Core cohérent

Objets essentiels présents

Contraintes essentielles présentes

Compte runtime toujours autorisé

Readiness futur possible
```

---

## 40.1 Vérification fonctionnelle

Après déploiement :

- login ;
- lecture ;
- écriture contrôlée ;
- formulaires ;
- modules affectés ;
- administration ;
- médias si concernés.

---

# 41. Readiness

L’application peut vérifier la compatibilité du schéma sans modifier celui-ci.

---

## 41.1 Migration manquante

Si le schéma est incompatible, le readiness peut être :

```text
unhealthy
```

et un diagnostic protégé peut indiquer :

```text
DatabaseMigrationRequired
```

---

## 41.2 Réponse publique

Le health check public ne doit pas révéler :

- nom de migration ;
- table ;
- SQL ;
- chaîne de connexion ;
- serveur.

---

## 41.3 Pas de correction automatique

La détection d’une migration manquante ne doit pas déclencher son application.

---

# 42. Liveness

Le liveness ne dépend pas de l’application des migrations.

Une migration manquante peut dégrader le readiness sans provoquer une boucle de redémarrage permanente.

---

# 43. Gestion des échecs

En cas d’échec :

1. arrêter le déploiement ;
2. maintenir le mode maintenance si nécessaire ;
3. conserver les logs ;
4. conserver le rapport ;
5. identifier la dernière migration appliquée ;
6. vérifier l’état transactionnel ;
7. ne pas relancer automatiquement ;
8. choisir entre correction, forward fix ou restauration.

---

## 43.1 Relance

Une relance n’est autorisée qu’après compréhension de l’état réel.

---

## 43.2 Migration partielle

Une migration partiellement appliquée doit être traitée comme un incident de données.

---

## 43.3 Fausse réussite

Le runner doit retourner un code non nul si :

- une migration échoue ;
- la vérification échoue ;
- le rapport ne peut pas être produit ;
- la cible est incohérente.

---

# 44. Rollback

## 44.1 Principe

Le rollback principal d’une migration production n’est pas l’appel automatique à :

```text
dotnet ef database update PreviousMigration
```

---

## 44.2 Raisons

Les méthodes `Down` peuvent :

- supprimer des données ;
- ne pas annuler une transformation ;
- ne pas correspondre aux changements runtime ;
- être insuffisamment testées ;
- rendre les opérations plus dangereuses.

---

## 44.3 Stratégies retenues

Selon le cas :

```text
forward fix

rollback applicatif si schéma compatible

restauration du backup
+ déploiement de la version précédente
```

---

## 44.4 Méthode `Down`

Les méthodes `Down` restent utiles pour :

- développement ;
- tests ;
- analyse ;
- certains scénarios explicitement validés.

Elles ne constituent pas une garantie de rollback production.

---

# 45. Forward fix

Un forward fix est préférable lorsque :

- la migration est déjà appliquée ;
- les données sont intactes ;
- une correction additive est possible ;
- une restauration ferait perdre des données récentes.

Le forward fix doit lui-même suivre :

- branche ;
- tests ;
- version ;
- plan ;
- exécution explicite.

---

# 46. Restauration

Une restauration devient nécessaire lorsque :

- les données sont corrompues ;
- la migration est irréversible ;
- le schéma reste incompatible ;
- le forward fix est trop risqué ;
- l’impact est supérieur à la perte correspondant au RPO.

La procédure suit :

```text
23-backup-and-restore.md
```

---

# 47. Données créées après le backup

Une restauration peut supprimer les données créées après le backup.

Avant restauration, il faut évaluer si certaines données peuvent être :

- exportées ;
- réconciliées ;
- réimportées ;
- communiquées au client.

---

# 48. Environnements

## 48.1 Développement

Les migrations sont explicites, mais peuvent être appliquées facilement par le développeur.

---

## 48.2 Tests automatisés

Les migrations peuvent être appliquées automatiquement sur des bases éphémères.

---

## 48.3 CI

La CI peut créer une base temporaire et appliquer toutes les migrations sans approbation humaine.

---

## 48.4 Staging

Staging utilise un processus proche de la production.

Une approbation peut être allégée, mais le plan et les rapports restent nécessaires pour les migrations importantes.

---

## 48.5 Production

Une approbation humaine et un backup vérifié sont obligatoires.

---

# 49. Tests requis

## 49.1 Base vide

Appliquer toutes les migrations sur une base vide.

---

## 49.2 Upgrade supporté

Appliquer les migrations depuis la version précédente supportée.

---

## 49.3 Données représentatives

Tester avec :

- utilisateurs ;
- leads ;
- contenus ;
- relations ;
- volumes réalistes lorsque nécessaire.

---

## 49.4 Contraintes

Vérifier :

- index ;
- clés étrangères ;
- unicité ;
- nullabilité ;
- permissions.

---

## 49.5 Idempotence opérationnelle

Une seconde exécution sans migration pendante doit :

- ne modifier aucune donnée ;
- produire un succès contrôlé ou un statut `no-op` ;
- générer un rapport cohérent.

---

## 49.6 Mauvaise cible

Vérifier qu’une divergence de client ou environnement est refusée.

---

## 49.7 Migration concurrente

Vérifier que deux runners ne peuvent pas migrer la même base simultanément.

---

## 49.8 Runtime non privilégié

Vérifier que le compte runtime ne peut pas créer ou supprimer une table.

---

## 49.9 Démarrage sans migration automatique

Vérifier que le démarrage de l’application ne modifie pas l’historique EF Core.

---

# 50. Tests de failure injection

Simuler :

```text
Connexion interrompue

Timeout

Lock indisponible

Disque insuffisant

SQL invalide

Backup absent

Historique inattendu

Runner interrompu

Rapport impossible à écrire
```

Le système doit conserver un état et un diagnostic exploitables.

---

# 51. Tests de compatibilité applicative

Une release doit tester les scénarios utiles parmi :

```text
Ancienne application + ancien schéma

Nouvelle application + ancien schéma

Ancienne application + nouveau schéma

Nouvelle application + nouveau schéma
```

---

## 51.1 Matrice

La matrice dépend de la stratégie de déploiement.

Elle doit être documentée pour les migrations importantes.

---

# 52. Tests PostgreSQL réels

Les tests de migrations utilisent PostgreSQL réel.

SQLite ou EF Core InMemory ne prouvent pas :

- DDL ;
- transaction ;
- locks ;
- index concurrent ;
- types ;
- comportement PostgreSQL.

---

# 53. Tests du runner

Vérifier :

- parsing des arguments ;
- client ;
- environnement ;
- plan ;
- confirmation ;
- codes de sortie ;
- timeout ;
- cancellation ;
- logs ;
- rapport ;
- secret absent ;
- verrouillage.

---

# 54. Observabilité

Événements recommandés :

```text
MigrationCheckStarted

MigrationPlanCreated

MigrationStarted

MigrationLockAcquired

MigrationApplied

MigrationVerificationStarted

MigrationCompleted

MigrationFailed

MigrationRollbackDecisionRequired
```

---

## 54.1 Propriétés autorisées

```text
MigrationRunId

MigrationPlanId

ClientSlug

Environment

ApplicationVersion

SourceMigration

TargetMigration

Duration

Result

ErrorCode
```

---

## 54.2 Données interdites

```text
SQL complet

Données de table

Chaîne de connexion

Mot de passe

Email

Contenu client

Token
```

---

# 55. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-MIGRATION-001` | Configuration de migration invalide |
| `SF-MIGRATION-002` | Client ou environnement inconnu |
| `SF-MIGRATION-003` | Base cible incohérente |
| `SF-MIGRATION-004` | Version source inattendue |
| `SF-MIGRATION-005` | Migration cible inconnue |
| `SF-MIGRATION-006` | Plan invalide ou expiré |
| `SF-MIGRATION-007` | Backup requis absent |
| `SF-MIGRATION-008` | Backup non vérifié |
| `SF-MIGRATION-009` | Verrou opérationnel indisponible |
| `SF-MIGRATION-010` | Verrou PostgreSQL indisponible |
| `SF-MIGRATION-011` | Confirmation manquante |
| `SF-MIGRATION-012` | Credentials de migration indisponibles |
| `SF-MIGRATION-013` | Privilèges insuffisants |
| `SF-MIGRATION-014` | Migration échouée |
| `SF-MIGRATION-015` | Historique EF Core incohérent |
| `SF-MIGRATION-016` | Vérification post-migration échouée |
| `SF-MIGRATION-017` | Migration concurrente détectée |
| `SF-MIGRATION-018` | Migration destructive non approuvée |
| `SF-MIGRATION-019` | Mode maintenance requis |
| `SF-MIGRATION-020` | Rapport de migration non produit |
| `SF-MIGRATION-021` | Rollback ou restauration requis |
| `SF-MIGRATION-022` | Migration déjà appliquée |
| `SF-MIGRATION-023` | Opération non transactionnelle non approuvée |

---

# 56. Quality gates

Une migration ne peut pas être proposée pour la production si :

```text
Migration non versionnée

Migration non revue

Tests base vide échoués

Tests d’upgrade échoués

Opération destructive non documentée

Backup non planifié

Rollback absent

Runner non versionné

Client ou environnement non vérifiable

Permissions non testées

Rapport absent
```

---

## 56.1 Gate avant exécution

L’exécution est bloquée si :

```text
Backup échoué

Checksum invalide

Plan différent de l’artefact

Lock indisponible

Version source inattendue

Confirmation absente

Secret de migration indisponible
```

---

## 56.2 Gate après exécution

Le déploiement reste bloqué si :

```text
Historique incohérent

Vérification échouée

Readiness impossible

Migration partielle

Rapport absent

Erreur critique non résolue
```

---

# 57. CI/CD

La CI doit vérifier :

- compilation du runner ;
- tests ;
- création du bundle ou conteneur ;
- version ;
- manifest ;
- absence de secret ;
- compatibilité de migration ;
- artefact immuable.

---

## 57.1 Production

Le workflow peut préparer et publier le runner.

Il ne l’exécute pas automatiquement sur la production dans le MVP.

---

## 57.2 Environnement protégé

Une automatisation future devra utiliser :

- environnement GitHub protégé ;
- approbation ;
- secrets limités ;
- cible explicite ;
- logs ;
- rapport.

---

# 58. Scripts SQL

Un script SQL peut être généré comme artefact de revue ou solution de secours.

---

## 58.1 Propriétés

Le script doit être :

- lié à une version ;
- immuable ;
- inspecté ;
- testé ;
- accompagné de son checksum ;
- accompagné de ses préconditions.

---

## 58.2 Exécution manuelle

L’exécution manuelle reste exceptionnelle et suit les mêmes obligations de :

- backup ;
- cible ;
- confirmation ;
- rapport ;
- vérification.

---

# 59. Relation avec Docker

Le runner peut être exécuté comme conteneur one-shot sur le réseau privé du projet.

Exemple conceptuel :

```text
docker compose run --rm migration
```

La commande réelle appartient au runbook généré.

---

## 59.1 Pas de port public

Le runner accède à PostgreSQL via le réseau privé.

---

## 59.2 Image

L’image doit être versionnée et compatible avec la release cible.

---

## 59.3 Secret

Le credential de migration est injecté uniquement pendant l’opération.

---

# 60. Mode opératoire de référence

```text
1. Identifier le client et l’environnement
2. Vérifier la release
3. Vérifier le plan
4. Lire les migrations
5. Vérifier le backup
6. Vérifier le rollback
7. Acquérir le verrou
8. Activer maintenance si nécessaire
9. Exécuter le runner
10. Vérifier l’historique
11. Déployer ou finaliser la release
12. Exécuter les smoke tests
13. Réactiver le trafic
14. Surveiller
15. Produire le rapport
16. Libérer le verrou
```

---

# 61. Runbooks

Les runbooks suivants doivent être cohérents avec cette décision :

```text
Migration required

Migration failure

Deployment failure

Rollback

Database unavailable

Backup failure

Restore
```

---

# 62. Transfert client

Un projet transféré doit inclure :

- migrations ;
- runner ou procédure ;
- versions ;
- comptes requis ;
- backup ;
- rollback ;
- runbook ;
- dernier rapport de migration significatif.

---

## 62.1 Nouvel opérateur

Le nouvel opérateur doit comprendre que :

- démarrer l’application ne migre pas la base ;
- une migration constitue une opération séparée ;
- le compte runtime ne suffit pas ;
- le backup et la vérification restent obligatoires.

---

# 63. Conséquences positives

La décision apporte :

- réduction du risque de migration accidentelle ;
- séparation des privilèges ;
- backup préalable systématique ;
- meilleure traçabilité ;
- contrôle de la cible ;
- meilleure gestion des migrations longues ;
- capacité de test et de revue ;
- prévention des migrations concurrentes ;
- runtime applicatif moins privilégié ;
- déploiement plus compréhensible ;
- transfert client plus clair ;
- rapports structurés.

---

# 64. Conséquences négatives

La décision entraîne :

- davantage d’étapes opératoires ;
- runner supplémentaire ;
- besoin de credentials dédiés ;
- temps de préparation ;
- besoin d’un plan de migration ;
- besoin de synchroniser application et schéma ;
- procédures plus longues ;
- automatisation moins immédiate ;
- formation opérateur ;
- maintenance d’artefacts supplémentaires.

---

# 65. Risques acceptés

## 65.1 Erreur opérateur

Un humain peut choisir la mauvaise cible.

Réduction :

- vérification client/environnement ;
- plan ;
- confirmation ;
- noms explicites ;
- rapport.

---

## 65.2 Temps d’indisponibilité

Certaines migrations nécessiteront une maintenance.

Réduction :

- expand and contract ;
- tests ;
- mesure ;
- fenêtre ;
- index concurrent lorsque pertinent.

---

## 65.3 Complexité

Le processus est plus complexe que `Database.Migrate()`.

Cette complexité est acceptée car elle rend les risques visibles et contrôlables.

---

## 65.4 Échec partiel

Certaines opérations ne sont pas totalement transactionnelles.

Réduction :

- classification ;
- tests ;
- étapes séparées ;
- backup ;
- forward fix ou restauration.

---

# 66. Mesures compensatoires

- runner versionné ;
- plan structuré ;
- backup vérifié ;
- compte migration séparé ;
- verrou opérationnel ;
- advisory lock ;
- tests PostgreSQL ;
- rapport ;
- mode maintenance ;
- smoke tests ;
- runbooks ;
- restore drill ;
- inventaire des versions.

---

# 67. Sécurité

## 67.1 Actifs

- schéma ;
- données ;
- credentials ;
- historique ;
- backups ;
- identité ;
- données personnelles.

---

## 67.2 Menaces

- credential de migration compromis ;
- mauvaise base ciblée ;
- migration non autorisée ;
- SQL modifié ;
- double exécution ;
- logs sensibles ;
- runner falsifié ;
- pipeline compromis.

---

## 67.3 Contrôles

- secret temporaire ;
- environnement protégé ;
- checksum ;
- image versionnée ;
- cible vérifiée ;
- confirmation ;
- moindre privilège ;
- audit ;
- verrou ;
- rapport ;
- absence de SQL sensible dans les logs.

---

# 68. Performance et capacité

Une migration doit évaluer :

- durée estimée ;
- nombre de lignes ;
- locks ;
- espace disque ;
- WAL ;
- index ;
- mémoire ;
- timeout.

---

## 68.1 Mesure

Les migrations importantes doivent être testées avec un volume représentatif.

---

## 68.2 Timeouts

Le timeout de migration peut être supérieur au timeout runtime, mais il reste borné et documenté.

---

# 69. Coûts

## 69.1 Coûts supplémentaires

- développement du runner ;
- tests ;
- backups ;
- staging ;
- temps opérateur ;
- documentation ;
- rapports.

---

## 69.2 Coûts évités

- perte de données ;
- migrations accidentelles ;
- indisponibilité prolongée ;
- application sur-privilégiée ;
- restauration improvisée ;
- incohérence entre clients ;
- incidents de release difficiles à diagnostiquer.

---

# 70. Maintenabilité

Le processus permet de standardiser :

- création des migrations ;
- classification ;
- tests ;
- plans ;
- exécution ;
- rapports ;
- runbooks.

---

## 70.1 Pas de logique de migration dans le runtime

Le code de démarrage reste concentré sur :

- configuration ;
- services ;
- endpoints ;
- health checks.

---

## 70.2 Abstraction raisonnable

Le runner peut partager les contrats et la configuration de persistance sans devenir un second produit complexe.

---

# 71. Compatibilité

## 71.1 Application

La release doit déclarer la migration minimale et maximale compatible lorsque nécessaire.

---

## 71.2 PostgreSQL

Le runner doit utiliser une version d’outillage compatible avec la base cible.

---

## 71.3 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, car cette décision constitue la baseline.

L’introduction de cette règle dans un projet utilisant déjà les auto-migrations nécessitera une migration opérationnelle.

---

# 72. Migration depuis une auto-migration existante

Si un prototype possède actuellement :

```csharp
Database.Migrate()
```

le passage à la baseline nécessite :

1. retirer l’appel du démarrage ;
2. créer le runner ;
3. séparer les credentials ;
4. ajouter le diagnostic de migrations pendantes ;
5. tester le déploiement ;
6. documenter la commande explicite ;
7. vérifier la CI ;
8. mettre à jour les runbooks.

---

## 72.1 Production

```text
Production execution: out of scope
```

Cet ADR définit le processus, sans autoriser une modification réelle d’environnement.

---

# 73. Réversibilité

## 73.1 Classification

```text
élevée pour le contrôle opérationnel

moyenne pour une migration déjà appliquée
```

---

## 73.2 Retour à l’auto-migration

Techniquement possible, mais contraire aux drivers de sécurité et d’exploitation.

Un nouvel ADR serait nécessaire.

---

## 73.3 Retour sur les données

La réversibilité dépend du contenu de chaque migration.

Elle repose principalement sur :

- forward fix ;
- rollback applicatif compatible ;
- restauration.

---

# 74. Plan d’implémentation

## Étape 1 — Retirer les auto-migrations

Ajouter un test d’architecture ou d’inspection interdisant les appels de migration dans le startup de production.

---

## Étape 2 — Définir le runner

Créer un projet console ou bundle versionné.

---

## Étape 3 — Séparer les comptes

Définir :

- runtime ;
- migration ;
- administration.

---

## Étape 4 — Ajouter `check` et `plan`

Produire les diagnostics sans écriture.

---

## Étape 5 — Ajouter le verrouillage

Ajouter :

- verrou opérationnel ;
- advisory lock PostgreSQL.

---

## Étape 6 — Ajouter les rapports

Créer :

```text
migration-plan.json
migration-report.json
```

---

## Étape 7 — Ajouter les tests

Couvrir :

- base vide ;
- upgrade ;
- mauvaise cible ;
- concurrence ;
- échec ;
- permissions ;
- absence d’auto-migration.

---

## Étape 8 — Générer les runbooks

Adapter les projets clients.

---

## Étape 9 — Préparer l’automatisation contrôlée

Intégrer le runner aux procédures de déploiement sans exécution production automatique.

---

# 75. Tâches liées proposées

```text
SF-TASK-029 — Remove automatic migrations from application startup

SF-TASK-030 — Create the client migration runner

SF-TASK-031 — Add migration account configuration

SF-TASK-032 — Implement migration check and plan

SF-TASK-033 — Add PostgreSQL migration locking

SF-TASK-034 — Generate migration reports

SF-TASK-035 — Test upgrade from the previous supported version

SF-TASK-036 — Add migration failure runbook

SF-TASK-037 — Add pending migration readiness diagnostics
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 76. Tests et preuves attendus

Pour considérer la décision correctement implémentée :

```text
L’application démarre sans modifier la base.

Le compte runtime ne peut pas créer une table.

Le runner peut appliquer une migration avec le compte dédié.

Une migration sans backup obligatoire est refusée.

Une mauvaise cible est refusée.

Deux runners concurrents sont empêchés.

Le plan correspond aux migrations exécutées.

La base vide atteint la dernière migration.

Une ancienne version supportée atteint la cible.

Une seconde exécution est un no-op contrôlé.

Une migration échouée retourne un code non nul.

Le rapport ne contient aucun secret.

Le readiness signale une incompatibilité sans la corriger.

Le runner fonctionne sur Linux.

Le projet reste testable sur Windows.
```

---

# 77. Conditions de révision

Réexaminer cette décision si :

- le nombre de clients rend les migrations manuelles non soutenables ;
- une plateforme centrale d’orchestration est introduite ;
- un service PostgreSQL managé impose un workflow différent ;
- des déploiements sans interruption deviennent obligatoires ;
- plusieurs instances applicatives doivent coexister durablement pendant les migrations ;
- les migrations volumineuses deviennent fréquentes ;
- une solution de schema registry est introduite ;
- les approbations humaines deviennent un goulot d’étranglement ;
- un outil de migration autre qu’EF Core devient nécessaire ;
- une exigence réglementaire impose un niveau d’audit supérieur.

---

# 78. Indicateurs à suivre

- nombre de migrations ;
- durée moyenne ;
- durée maximale ;
- migrations en échec ;
- restaurations après migration ;
- temps de préparation ;
- temps d’indisponibilité ;
- nombre de migrations destructives ;
- nombre de forward fixes ;
- nombre de mauvaises cibles évitées ;
- âge du backup pré-migration ;
- temps de propagation des migrations clients ;
- nombre de versions de schéma supportées.

---

# 79. Signaux d’alerte

La décision ou son implémentation doit être renforcée si les observations montrent :

```text
des migrations régulièrement appliquées sans plan

des credentials DDL conservés dans le runtime

des backups absents ou non vérifiés

des runners exécutés sur la mauvaise base

des rapports manquants

des migrations longues non détectées

des restaurations fréquentes après release

des branches clientes restant durablement incompatibles

des procédures opérateur trop complexes ou ambiguës
```

---

# 80. Conséquences observées

Cette section est append-only après mise en œuvre.

## 80.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation du runner
de migration SiteFactory.
```

Les observations futures devront préciser :

- date ;
- version ;
- client technique concerné ;
- type de migration ;
- durée ;
- résultat ;
- incident éventuel ;
- amélioration proposée.

---

# 81. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [generated-explicit-migrator.md](../03-generation/generated-explicit-migrator.md)
- `13-generation-pipeline.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `20-quality-gates-and-ci.md`
- `22-deployment-and-environments.md`
- `23-backup-and-restore.md`
- `24-operational-runbooks.md`
- `25-upgrade-and-maintenance-strategy.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0003-postgresql-as-primary-database.md`

---

# 82. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 77 |

---

# 83. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 84. Synthèse de la décision

```text
Application web
→ ne migre jamais automatiquement la production
→ compte runtime sans DDL

Migration
→ repository client
→ release versionnée
→ runner dédié
→ credentials temporaires

Avant exécution
→ plan
→ tests
→ backup vérifié
→ cible contrôlée
→ approbation humaine

Pendant l’exécution
→ verrou opérationnel
→ verrou PostgreSQL
→ rapport
→ code de sortie fiable

Après exécution
→ vérification
→ déploiement compatible
→ smoke tests
→ surveillance

Échec
→ pas de relance aveugle
→ forward fix, rollback compatible ou restauration
```

> Une migration de production est une opération sur les données, pas un effet secondaire du démarrage applicatif. SiteFactory la rend visible, versionnée, vérifiable et explicitement autorisée.
