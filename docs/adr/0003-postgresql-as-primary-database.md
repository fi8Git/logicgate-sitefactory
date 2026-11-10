---
adrId: ADR-0003
title: Use PostgreSQL as the primary relational database
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
  - ../04-quality-security/19-testing-strategy.md
  - ../05-operations/21-observability-and-logging.md
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/23-backup-and-restore.md
  - ../05-operations/24-operational-runbooks.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0002-yaml-client-configuration.md
targetVersion: 0.1.0
---

# ADR-0003 — PostgreSQL comme base de données relationnelle principale

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La fondation [PostgreSQL publique](../../tests/PostgreSql/README.md) est réelle :
Infrastructure.PostgreSql, Npgsql/EF Core confinés à Infrastructure,
métadonnées runtime, migration initiale et Migrator explicite.
Runtime et Runtime.Contracts restent purs ; ni le Core ni DocumentationModule
n'exigent une connexion DB pour leur génération statique.
Le choix ci-dessous vise la persistence des applications clientes ; il
n'annonce pas leurs données métier, leur provisioning ou leur exploitation
PostgreSQL de production comme déjà livrés.

## 1. Statut

```text
accepted
```

PostgreSQL constitue la base de données relationnelle de référence des applications clientes générées par LogicGate SiteFactory.

Pour le MVP, SiteFactory ne cherchera pas à supporter plusieurs fournisseurs relationnels interchangeables.

Toute introduction d’un autre fournisseur de base de données pour les projets standards devra faire l’objet :

- d’un besoin démontré ;
- d’une analyse de compatibilité ;
- d’une stratégie de tests ;
- d’un plan de migration ;
- d’un nouvel ADR ;
- d’une validation LogicGate.

---

## 2. Résumé

Chaque application cliente standard générée avec SiteFactory utilisera PostgreSQL pour ses données relationnelles persistantes.

L’accès aux données reposera initialement sur :

```text
Entity Framework Core
+ provider PostgreSQL maintenu
+ migrations stockées dans le repository client
```

Chaque client et environnement possédera :

```text
une base PostgreSQL distincte
+ un compte applicatif distinct
+ des migrations distinctes
+ des sauvegardes distinctes
```

PostgreSQL sera utilisé pour :

- ASP.NET Core Identity ;
- contenus administrables ;
- leads ;
- configurations runtime persistantes ;
- données métier des modules ;
- historiques et métadonnées structurées ;
- références de médias et documents.

Les fichiers binaires volumineux resteront en dehors de PostgreSQL dans le profil initial, notamment dans :

```text
App_Data
```

ou dans un stockage objet futur.

Les comportements dépendant de PostgreSQL devront être testés avec une véritable instance PostgreSQL.

SQLite et les providers en mémoire ne seront pas considérés comme des substituts valides pour les tests d’intégration de persistance.

---

## 3. Contexte

SiteFactory doit générer des applications web destinées à des PME avec des besoins tels que :

- administration de contenu ;
- gestion des utilisateurs ;
- collecte de leads ;
- formulaires métier ;
- réalisations ;
- médias ;
- automatisations ;
- données spécifiques à certains modules ;
- audits opérationnels ;
- recherches et filtres ;
- pagination ;
- sauvegardes ;
- restauration ;
- transfert du projet au client.

La base de données principale doit être compatible avec :

- .NET 10 ;
- ASP.NET Core ;
- Entity Framework Core ;
- Identity ;
- Docker ;
- Linux ;
- VPS ;
- GitHub Actions ;
- tests automatisés ;
- opérations de sauvegarde et de restauration ;
- isolation par client.

LogicGate possède par ailleurs les contraintes suivantes :

- équipe initiale réduite ;
- infrastructure simple à exploiter ;
- préférence pour les technologies open source ;
- besoin de limiter les licences par client ;
- besoin de transférer facilement un projet ;
- besoin de conserver des migrations versionnées ;
- absence d’architecture multi-tenant dans le MVP ;
- priorité donnée à la sécurité, à la lisibilité et à la maintenance.

Une décision explicite est nécessaire afin d’éviter que chaque module ou projet client choisisse son propre fournisseur de persistance.

---

## 4. Problème à résoudre

Quel système de base de données relationnelle utiliser comme référence pour les projets SiteFactory afin de fournir :

- cohérence transactionnelle ;
- contraintes relationnelles ;
- migrations ;
- indexation ;
- recherche ;
- sauvegarde et restauration fiables ;
- compatibilité .NET ;
- exploitation sur Linux ;
- coût raisonnable ;
- portabilité ;
- testabilité ;
- isolation par client ?

La décision doit également déterminer si SiteFactory doit :

- supporter un seul provider ;
- supporter plusieurs providers ;
- viser un plus petit dénominateur commun ;
- autoriser SQLite en production ;
- permettre une dépendance PostgreSQL assumée dans Infrastructure.

---

## 5. Portée

Cette décision couvre :

- la base relationnelle principale ;
- les projets clients standards ;
- staging et production ;
- le développement local ;
- Entity Framework Core ;
- le provider PostgreSQL ;
- la propriété des migrations ;
- les comptes PostgreSQL ;
- les permissions ;
- les conventions de types ;
- les transactions ;
- les tests d’intégration ;
- les sauvegardes ;
- les restaurations ;
- la mise à niveau PostgreSQL ;
- l’observabilité de la base ;
- les contraintes de sécurité.

---

## 6. Hors portée

Cette décision ne définit pas :

- la version majeure PostgreSQL exacte de chaque projet ;
- le fournisseur PostgreSQL managé éventuel ;
- la taille du VPS ;
- la haute disponibilité ;
- la réplication ;
- le point-in-time recovery ;
- un data warehouse ;
- un moteur de recherche externe ;
- un cache distribué ;
- un stockage objet ;
- le stockage des fichiers binaires dans PostgreSQL ;
- l’architecture d’un futur produit SaaS multi-tenant ;
- la stratégie complète d’analytics ;
- le remplacement des fichiers `App_Data`.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Fiabilité transactionnelle**
2. **Intégrité relationnelle**
3. **Compatibilité avec .NET et EF Core**
4. **Compatibilité Linux et Docker**
5. **Absence de coût de licence par client**
6. **Sauvegarde et restauration éprouvées**
7. **Isolation par client et environnement**
8. **Portabilité vers différents hébergeurs**
9. **Capacités d’indexation et de recherche**
10. **Maintenabilité par une petite équipe**
11. **Support d’ASP.NET Core Identity**
12. **Outillage d’administration et de diagnostic**
13. **Capacité à monter en charge raisonnablement**
14. **Écosystème mature**
15. **Testabilité en CI**
16. **Transfert complet au client**
17. **Limitation de la complexité multi-provider**

---

## 8. Contraintes

La décision doit respecter les contraintes suivantes :

```text
.NET 10

Entity Framework Core

Linux en production

Docker Compose pour le profil initial

Un repository par client

Une base par client et environnement

Migrations explicites

Aucune auto-migration en production

PostgreSQL non exposé publiquement par défaut

Sauvegardes vérifiables

Tests automatisés

Aucun secret dans Git

Compatibilité Windows et Linux pour le développement
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les projets standards utilisent principalement des données relationnelles ;
- les volumes initiaux restent compatibles avec une instance PostgreSQL classique ;
- un VPS peut héberger plusieurs instances ou bases isolées selon son dimensionnement ;
- les fichiers volumineux sont stockés hors de la base ;
- les besoins de requêtes restent compatibles avec EF Core et du SQL PostgreSQL ciblé ;
- LogicGate accepte d’assumer PostgreSQL comme dépendance d’infrastructure ;
- les projets clients n’exigent pas de fournisseur SQL propriétaire spécifique ;
- un projet sur mesure pourra déroger à cette décision après analyse ;
- les tests d’intégration peuvent utiliser Docker ou Testcontainers.

---

# 10. Option A — SQLite comme base principale

## 10.1 Description

Chaque application utilise un fichier SQLite local comme base principale.

Exemple :

```text
Application
→ SQLite file
→ mounted persistent volume
```

---

## 10.2 Avantages

- installation simple ;
- faible consommation de ressources ;
- aucune instance serveur ;
- sauvegarde initialement simple par copie de fichier ;
- développement local rapide ;
- distribution facile ;
- coût d’exploitation limité.

---

## 10.3 Inconvénients

- concurrence en écriture plus limitée ;
- différence importante avec les systèmes serveur ;
- administration moins adaptée à une exploitation distante ;
- capacités de rôles et permissions réduites ;
- stratégie de sauvegarde cohérente plus délicate pendant les écritures ;
- montée en charge moins flexible ;
- fonctionnalités SQL différentes ;
- migrations et contraintes parfois différentes ;
- support opérationnel moins adapté à plusieurs services.

---

## 10.4 Risques

- verrouillage lors d’écritures concurrentes ;
- corruption ou copie incohérente du fichier ;
- tests locaux ne révélant pas les problèmes de production futurs ;
- difficulté à séparer compte applicatif et compte opérateur ;
- limites lors d’automatisations plus fréquentes ;
- migration ultérieure coûteuse vers une base serveur.

---

## 10.5 Impacts opérationnels

- volumes fichiers critiques ;
- sauvegarde nécessitant une coordination ;
- diagnostic limité ;
- pas de service DB indépendant ;
- difficulté à appliquer certaines politiques de moindre privilège.

---

## 10.6 Réversibilité

```text
moyenne
```

Une migration vers PostgreSQL nécessiterait :

- conversion du schéma ;
- adaptation des types ;
- transfert des données ;
- adaptation des requêtes ;
- nouveaux tests ;
- nouvelle exploitation.

---

# 11. Option B — Microsoft SQL Server

## 11.1 Description

Les applications utilisent SQL Server comme base principale avec le provider EF Core correspondant.

---

## 11.2 Avantages

- excellente intégration historique avec .NET ;
- outillage mature ;
- fonctionnalités relationnelles complètes ;
- compétences répandues dans l’écosystème Microsoft ;
- support Identity et EF Core mature ;
- capacités d’administration avancées.

---

## 11.3 Inconvénients

- licences et éditions à prendre en compte ;
- image et consommation de ressources potentiellement plus importantes ;
- dépendance plus forte à l’écosystème Microsoft ;
- déploiement Linux possible mais moins naturel pour la cible initiale ;
- portabilité et coût potentiellement moins favorables pour de petites PME ;
- transfert client pouvant nécessiter un environnement licencié.

---

## 11.4 Risques

- dépassement des limites d’une édition gratuite ;
- complexité de licence ;
- coût croissant ;
- contraintes d’hébergement ;
- hétérogénéité si certains clients disposent de licences et d’autres non.

---

## 11.5 Impacts opérationnels

- outillage et procédures spécifiques ;
- dimensionnement supérieur ;
- gestion des licences ;
- stratégie de backup propre à SQL Server ;
- images et pipelines différents.

---

## 11.6 Réversibilité

```text
moyenne
```

EF Core limite une partie du coût, mais les types, migrations et requêtes peuvent rester spécifiques.

---

# 12. Option C — PostgreSQL comme base unique supportée

## 12.1 Description

Tous les projets standards utilisent PostgreSQL comme base relationnelle de production.

L’accès applicatif utilise EF Core avec un provider PostgreSQL maintenu.

---

## 12.2 Avantages

- open source ;
- absence de licence par client ;
- excellente compatibilité Linux et Docker ;
- intégrité relationnelle forte ;
- transactions fiables ;
- types riches ;
- indexation avancée ;
- `jsonb` disponible pour les usages justifiés ;
- recherche textuelle native possible ;
- sauvegardes logiques éprouvées ;
- écosystème mature ;
- portabilité entre VPS et services managés ;
- provider .NET mature ;
- forte capacité d’évolution.

---

## 12.3 Inconvénients

- service supplémentaire à exploiter ;
- consommation de mémoire supérieure à SQLite ;
- sauvegardes et restauration à administrer ;
- besoin de compétences PostgreSQL ;
- différences avec SQL Server ;
- dépendance assumée à un provider ;
- mises à niveau majeures nécessitant une procédure.

---

## 12.4 Risques

- mauvaises migrations ;
- requêtes non indexées ;
- connexions excessives ;
- saturation du disque ;
- mauvaise configuration des permissions ;
- confusion de base entre clients ;
- incompatibilité lors d’une mise à niveau majeure ;
- dépendance à des extensions non portables.

---

## 12.5 Impacts opérationnels

- conteneur ou service PostgreSQL ;
- volume persistant ;
- comptes et permissions ;
- health checks ;
- sauvegardes ;
- monitoring ;
- migrations explicites ;
- restore drills ;
- upgrades planifiés.

---

## 12.6 Réversibilité

```text
moyenne
```

La base reste portable entre de nombreux fournisseurs PostgreSQL, mais une migration vers un autre moteur relationnel nécessiterait un projet dédié.

---

# 13. Option D — Abstraction multi-provider dès le MVP

## 13.1 Description

SiteFactory supporte plusieurs providers relationnels :

- PostgreSQL ;
- SQLite ;
- SQL Server ;
- éventuellement d’autres moteurs.

Le domaine, les migrations et les requêtes restent limités au plus petit dénominateur commun.

---

## 13.2 Avantages

- choix du provider par client ;
- capacité de répondre à des contraintes existantes ;
- réduction apparente du verrouillage technologique ;
- adaptation à différents hébergeurs.

---

## 13.3 Inconvénients

- multiplication des matrices de tests ;
- migrations propres à chaque provider ;
- types différents ;
- comportements transactionnels différents ;
- requêtes différentes ;
- index et recherche différents ;
- complexité du générateur ;
- documentation plus lourde ;
- risque de fonctionnalités non uniformes ;
- ralentissement important du MVP.

---

## 13.4 Risques

- tests incomplets sur certains providers ;
- bugs uniquement visibles en production ;
- abstractions excessives ;
- impossibilité d’utiliser correctement les capacités PostgreSQL ;
- modules compatibles avec seulement une partie des moteurs ;
- support coûteux.

---

## 13.5 Impacts opérationnels

- plusieurs profils de backup ;
- plusieurs procédures de restauration ;
- plusieurs outils ;
- plusieurs stratégies d’upgrade ;
- expertise accrue ;
- support client plus complexe.

---

## 13.6 Réversibilité

```text
faible
```

Une architecture multi-provider introduite tôt crée de nombreux contrats et branches conditionnelles difficiles à retirer.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | SQLite | SQL Server | PostgreSQL | Multi-provider |
|---|---:|---:|---:|---:|---:|
| Fiabilité relationnelle | 5 | 3 | 5 | 5 | 4 |
| Concurrence | 5 | 2 | 5 | 5 | 3 |
| Linux et Docker | 5 | 5 | 3 | 5 | 3 |
| Coût de licence | 4 | 5 | 2 | 5 | 3 |
| EF Core | 5 | 4 | 5 | 5 | 3 |
| Backup et restore | 5 | 3 | 5 | 5 | 2 |
| Portabilité hébergeur | 4 | 4 | 3 | 5 | 4 |
| Simplicité du MVP | 5 | 5 | 3 | 4 | 1 |
| Capacité d’évolution | 4 | 2 | 5 | 5 | 3 |
| Charge de tests | 4 | 5 | 4 | 4 | 1 |
| Sécurité opérationnelle | 5 | 3 | 5 | 5 | 3 |
| Transfert client | 4 | 4 | 3 | 5 | 3 |
| Maintenabilité | 5 | 4 | 4 | 5 | 2 |

PostgreSQL offre le meilleur équilibre entre capacités, coût, portabilité et simplicité de standardisation.

---

# 15. Décision

Nous décidons d’utiliser PostgreSQL comme unique base de données relationnelle principale supportée par les projets SiteFactory standards dans le MVP.

L’accès aux données utilisera :

```text
Entity Framework Core
+ provider PostgreSQL maintenu
```

---

## 15.1 Provider unique

SiteFactory ne maintiendra pas de compatibilité production officielle avec :

- SQLite ;
- SQL Server ;
- MySQL ;
- MariaDB ;
- une base en mémoire.

Ces technologies peuvent être utilisées dans un projet sur mesure uniquement après une décision distincte.

---

## 15.2 Dépendance assumée

La couche Infrastructure peut utiliser des capacités spécifiques à PostgreSQL lorsque celles-ci :

- apportent une valeur claire ;
- restent isolées ;
- sont testées ;
- sont documentées ;
- ne polluent pas le domaine.

SiteFactory ne cherchera pas à limiter PostgreSQL à un sous-ensemble SQL artificiellement neutre.

---

## 15.3 Abstraction

Domain et Application restent indépendants du provider.

La dépendance PostgreSQL reste dans :

```text
Infrastructure
Migrations
Tests d’intégration
Déploiement
Opérations
```

---

# 16. Justification

PostgreSQL est retenu parce qu’il répond aux besoins de SiteFactory sans imposer de licence par client.

Il fournit :

- transactions ;
- contraintes ;
- index ;
- concurrence ;
- recherche ;
- types fiables ;
- sauvegardes ;
- restauration ;
- portabilité ;
- maturité opérationnelle.

Le choix d’un provider unique réduit fortement :

- le nombre de scénarios à tester ;
- la complexité des migrations ;
- les branches conditionnelles ;
- les divergences fonctionnelles ;
- la charge de support.

La dépendance PostgreSQL est considérée comme un compromis acceptable et préférable à une abstraction multi-provider prématurée.

---

# 17. Relation avec l’isolation client

Conformément à l’ADR-0001 :

```text
un client
+ un environnement
→ une base PostgreSQL distincte
```

Exemple :

```text
Client Alpha / Staging
→ base staging distincte

Client Alpha / Production
→ base production distincte

Client Beta / Production
→ base distincte de Client Alpha
```

---

## 17.1 Base partagée interdite

Les tables métier de plusieurs clients ne doivent pas être mélangées dans une base partagée pour les projets standards.

---

## 17.2 Serveur mutualisé

Plusieurs bases peuvent être hébergées sur :

- la même instance PostgreSQL ;
- le même VPS ;
- un service PostgreSQL managé partagé ;

à condition de préserver :

- comptes distincts ;
- permissions ;
- bases distinctes ;
- sauvegardes distinctes ;
- ciblage explicite ;
- capacité d’extraction indépendante.

---

# 18. Environnements

Les environnements suivants utilisent des bases distinctes :

```text
development
test
staging
production
restore-drill
```

---

## 18.1 Staging

Staging ne partage jamais la base de production.

---

## 18.2 Tests

Les tests n’utilisent jamais la base cliente réelle.

---

## 18.3 Restore drill

Une restauration de test utilise une base isolée et des credentials propres.

---

# 19. Comptes PostgreSQL

## 19.1 Compte applicatif

Chaque environnement possède un compte applicatif dédié.

Il doit disposer uniquement des droits nécessaires à l’application.

---

## 19.2 Interdictions

Le compte applicatif ne doit pas posséder :

```text
SUPERUSER

CREATEDB

CREATEROLE

REPLICATION

BYPASSRLS
```

sauf exception explicitement analysée et documentée.

---

## 19.3 Compte de migration

La cible opérationnelle consiste à utiliser un compte de migration distinct du compte runtime.

Le compte de migration possède uniquement les droits nécessaires pour :

- appliquer les migrations ;
- créer ou modifier les objets du schéma applicatif ;
- gérer l’historique EF Core.

Ses credentials ne doivent pas être injectés durablement dans l’application web.

---

## 19.4 Compte opérateur

Les opérations d’administration utilisent un compte opérateur séparé lorsque nécessaire.

---

## 19.5 Comptes partagés

Un compte ne doit pas donner accès aux bases de plusieurs clients sauf compte opérateur strictement contrôlé.

---

# 20. Permissions

## 20.1 Principe du moindre privilège

Les droits PostgreSQL doivent suivre :

```text
runtime
≠ migration
≠ administration
```

---

## 20.2 Schéma

L’application utilise un schéma explicitement défini et maîtrisé.

Les droits de création publics doivent être restreints lorsque nécessaires.

---

## 20.3 Objets futurs

Les permissions par défaut doivent être configurées afin que les nouveaux objets créés par les migrations restent accessibles au compte applicatif sans élargir les droits globaux.

---

## 20.4 Validation

Les permissions doivent être testées :

- après création ;
- après migration ;
- après restauration ;
- après changement de version majeure.

---

# 21. Connexion réseau

## 21.1 Production

PostgreSQL n’est pas exposé publiquement par défaut.

Dans le profil Docker Compose :

```text
application network
→ accès PostgreSQL

Internet
→ aucun accès direct PostgreSQL
```

---

## 21.2 Port `5432`

Le port PostgreSQL ne doit pas être publié sur l’interface publique du VPS.

---

## 21.3 Accès opérateur

Un accès ponctuel peut utiliser :

- SSH ;
- tunnel ;
- réseau privé ;
- outil d’administration sécurisé ;
- conteneur temporaire.

---

## 21.4 TLS

Lorsque l’application se connecte à un service PostgreSQL distant ou managé, la connexion doit utiliser un chiffrement et une validation adaptés au fournisseur.

---

# 22. Configuration de connexion

Les chaînes de connexion sont des secrets.

Elles ne doivent pas être :

- committées ;
- logguées ;
- ajoutées à un rapport ;
- intégrées à une image ;
- présentes dans `client.sitefactory.yml`.

---

## 22.1 Résolution

La chaîne de connexion est fournie par :

- environnement ;
- secret Docker ;
- coffre ;
- mécanisme de déploiement approuvé.

---

## 22.2 Validation

L’application valide la présence de sa configuration sans en afficher la valeur.

---

# 23. Entity Framework Core

## 23.1 Rôle

EF Core constitue l’outil principal pour :

- mapping ;
- requêtes ;
- transactions ;
- migrations ;
- tests ;
- intégration Identity.

---

## 23.2 Limite

EF Core ne dispense pas de comprendre :

- le SQL produit ;
- les index ;
- les transactions ;
- les plans d’exécution ;
- les particularités PostgreSQL ;
- les migrations.

---

## 23.3 Requêtes spécifiques

Du SQL ou des capacités PostgreSQL spécifiques peuvent être utilisés lorsque :

- le besoin est démontré ;
- le code reste isolé dans Infrastructure ;
- les paramètres sont sécurisés ;
- des tests PostgreSQL existent ;
- le comportement est documenté.

---

## 23.4 Provider en mémoire

Le provider EF Core InMemory ne doit pas être utilisé pour valider :

- contraintes relationnelles ;
- transactions ;
- SQL ;
- index ;
- concurrence ;
- migrations ;
- comportement PostgreSQL.

---

# 24. Propriété des migrations

Les migrations appartiennent au repository client généré.

Cette décision permet de conserver :

- la composition exacte des modules ;
- les personnalisations ;
- le schéma final ;
- l’historique ;
- les données propres au projet ;
- la capacité de transfert.

---

## 24.1 Packages génériques

Les packages génériques peuvent fournir :

- modèles ;
- configurations EF ;
- contrats ;
- services.

Ils ne publient pas une migration de production universelle représentant toutes les compositions clientes possibles.

---

## 24.2 Composition

La migration est créée après composition du projet client.

---

## 24.3 Historique

La table d’historique EF Core appartient à la base cliente concernée.

---

# 25. Application des migrations

## 25.1 Développement

Les migrations peuvent être appliquées explicitement par le développeur.

---

## 25.2 CI

La CI peut appliquer les migrations sur une base temporaire de test.

---

## 25.3 Staging

Les migrations sont appliquées par une opération contrôlée et tracée.

---

## 25.4 Production

Les migrations sont appliquées explicitement pendant le processus de déploiement.

---

## 25.5 Auto-migration interdite

L’application de production ne doit pas appeler automatiquement :

```csharp
Database.Migrate()
```

au démarrage.

---

## 25.6 Raisons

L’auto-migration peut :

- modifier les données sans validation ;
- démarrer plusieurs fois en parallèle ;
- bloquer le démarrage ;
- rendre le rollback complexe ;
- cacher une opération destructive ;
- utiliser des droits excessifs ;
- prolonger une indisponibilité ;
- empêcher un backup préalable.

---

# 26. Migration sensible

Une migration est sensible lorsqu’elle contient notamment :

- suppression de colonne ;
- suppression de table ;
- changement de type ;
- réduction de taille ;
- ajout `NOT NULL` sans stratégie ;
- transformation massive ;
- réécriture importante ;
- création d’index bloquante ;
- modification Identity ;
- données irréversibles.

---

## 26.1 Processus

```text
inspection
→ test sur données représentatives
→ backup
→ fenêtre éventuelle
→ migration
→ vérification
→ smoke tests
```

---

## 26.2 Stratégie progressive

Lorsque raisonnable :

```text
expand
→ migrate
→ switch
→ contract
```

---

# 27. Transactions

## 27.1 Principe

Une opération métier devant être atomique utilise une transaction adaptée.

---

## 27.2 Périmètre

La transaction doit rester aussi courte que possible.

Elle ne doit pas contenir inutilement :

- appel réseau ;
- envoi SMTP ;
- appel LLM ;
- traitement fichier long ;
- attente utilisateur.

---

## 27.3 Effets externes

Les effets externes utilisent une stratégie adaptée telle que :

- persistance avant notification ;
- outbox future ;
- idempotence ;
- retry contrôlé.

---

## 27.4 Isolation

Le niveau d’isolation n’est pas augmenté globalement sans besoin démontré.

Les cas de concurrence sont traités au niveau du cas d’usage concerné.

---

# 28. Concurrence

Les données susceptibles d’être modifiées simultanément doivent définir une stratégie.

Options possibles :

- concurrence optimiste ;
- version explicite ;
- contrainte unique ;
- verrou ciblé ;
- transaction ;
- idempotency key.

---

## 28.1 Exception de concurrence

Une erreur de concurrence attendue doit être transformée en résultat applicatif exploitable.

---

## 28.2 Contrainte unique

Une contrainte métier d’unicité doit exister en base, même si une validation applicative est également présente.

---

# 29. Identifiants

## 29.1 UUID

Les UUID constituent le choix par défaut pour les identifiants techniques exposés entre couches ou systèmes lorsque cela est pertinent.

---

## 29.2 Entiers

Des identifiants entiers peuvent être utilisés pour des besoins locaux internes après analyse.

---

## 29.3 Génération

La stratégie de génération doit éviter :

- collisions ;
- prédictibilité problématique lorsque l’identifiant est public ;
- dépendance à une séquence partagée entre clients.

---

# 30. Dates et heures

Les dates techniques sont stockées en UTC.

Le type PostgreSQL recommandé pour un instant est :

```text
timestamp with time zone
```

---

## 30.1 Modèle .NET

Le code utilise :

- `DateTimeOffset` ;
- ou une représentation UTC explicitement contrôlée.

---

## 30.2 Heure locale

Une heure locale métier doit être accompagnée :

- de son fuseau ;
- de sa règle ;
- de son sens métier.

---

## 30.3 Fuseau du serveur

La logique métier ne doit pas dépendre implicitement du fuseau horaire du VPS ou du conteneur.

---

# 31. Valeurs monétaires

Les valeurs monétaires ne doivent jamais utiliser :

```text
float
double
real
```

pour leur montant métier.

---

## 31.1 Représentation

Utiliser selon le besoin :

- `numeric` avec précision explicite ;
- montant en unité minimale entière ;
- code devise séparé si plusieurs devises sont possibles.

---

## 31.2 Prix SiteFactory

Les prix commerciaux verrouillés restent dans le catalogue et ne sont pas recalculés depuis des valeurs flottantes.

---

# 32. Chaînes

Les longueurs métier importantes doivent être validées :

- dans l’application ;
- et, lorsque pertinent, par la base.

---

## 32.1 `text` et `varchar`

Le choix entre `text` et `varchar` est guidé par :

- contrainte métier ;
- indexation ;
- compatibilité ;
- lisibilité du modèle.

Une longueur maximale applicative ne nécessite pas systématiquement un type `varchar`, mais la contrainte doit rester testable et explicite.

---

## 32.2 Normalisation

Les valeurs telles que :

- email ;
- slug ;
- domaine ;
- identifiant externe ;

doivent disposer d’une stratégie de normalisation explicite.

---

# 33. JSONB

## 33.1 Autorisé avec justification

`jsonb` peut être utilisé pour :

- données d’extension peu structurées ;
- snapshots ;
- payloads versionnés ;
- métadonnées variables ;
- rapports persistés lorsque cela est pertinent.

---

## 33.2 Interdit comme raccourci systématique

`jsonb` ne doit pas remplacer :

- un modèle relationnel connu ;
- les contraintes ;
- les relations ;
- les index métier ;
- les migrations nécessaires.

---

## 33.3 Contrat

Toute donnée JSON persistée doit posséder :

- un schéma ou contrat ;
- une version ;
- une taille limitée ;
- une politique de migration ;
- des tests.

---

# 34. Extensions PostgreSQL

## 34.1 Baseline

Aucune extension non indispensable n’est activée par défaut dans tous les projets.

---

## 34.2 Introduction

Une extension nécessite :

- besoin démontré ;
- disponibilité sur les cibles ;
- impact backup et restore ;
- impact mise à niveau ;
- licence ;
- tests ;
- documentation.

---

## 34.3 Portabilité

L’usage d’une extension spécifique peut réduire la portabilité vers certains services managés.

---

## 34.4 UUID

Les UUID doivent privilégier les capacités natives disponibles sans imposer une extension uniquement pour leur génération lorsque l’application peut les générer.

---

# 35. Recherche textuelle

PostgreSQL peut fournir une recherche textuelle native pour des besoins limités ou intermédiaires.

---

## 35.1 Usage

Elle peut être utilisée pour :

- contenus ;
- leads ;
- réalisations ;
- administration.

---

## 35.2 Moteur externe

Un moteur de recherche externe ne sera introduit que si :

- volume ;
- pertinence ;
- facettes ;
- analyse linguistique ;
- scalabilité ;

le justifient.

---

# 36. Index

Tout index doit répondre à un besoin de requête identifié.

---

## 36.1 Index obligatoires courants

Selon les modules :

- clés étrangères ;
- colonnes de recherche ;
- colonnes de tri fréquent ;
- clés d’idempotence ;
- contraintes uniques ;
- timestamps utilisés par la rétention.

---

## 36.2 Sur-indexation

Trop d’index augmente :

- stockage ;
- coût d’écriture ;
- durée des migrations ;
- maintenance.

---

## 36.3 Revue

Une nouvelle requête importante doit être examinée avec :

- cardinalité ;
- pagination ;
- index ;
- plan d’exécution si nécessaire.

---

# 37. Pagination

Les listes administratives non bornées sont interdites.

La pagination devient obligatoire lorsque le volume peut croître.

---

## 37.1 Exemple de baseline

Les tableaux tels que les leads utilisent une pagination à partir du seuil fonctionnel défini par le produit.

---

## 37.2 Offset et keyset

La pagination par offset peut suffire pour les volumes initiaux.

Une pagination par curseur ou keyset peut être introduite si :

- volume important ;
- tri stable ;
- performances mesurées.

---

# 38. Requêtes

## 38.1 Requêtes bornées

Les requêtes doivent limiter :

- nombre de lignes ;
- colonnes ;
- jointures ;
- profondeur de chargement.

---

## 38.2 Projections

Les écrans de liste privilégient des projections adaptées plutôt que le chargement complet des agrégats.

---

## 38.3 N+1

Les requêtes N+1 doivent être évitées et couvertes lorsque le risque est significatif.

---

## 38.4 Tracking

Les lectures sans modification utilisent `AsNoTracking` lorsque cela est approprié.

---

# 39. SQL brut

## 39.1 Paramétrage obligatoire

Toute requête SQL dynamique doit être paramétrée.

---

## 39.2 Identifiants SQL

Les noms de table, schéma ou colonne ne doivent pas provenir directement d’une entrée utilisateur.

---

## 39.3 Revue renforcée

Le SQL brut doit être :

- limité ;
- documenté ;
- testé ;
- relu pour l’injection ;
- compatible avec la version PostgreSQL supportée.

---

# 40. Connexions

## 40.1 Pooling

Le pooling de connexions est utilisé selon les capacités du provider.

---

## 40.2 Taille

La taille du pool doit être compatible avec :

- ressources PostgreSQL ;
- nombre de stacks ;
- concurrence ;
- VPS mutualisé.

---

## 40.3 Fuite de connexion

Les connexions et transactions doivent être correctement libérées.

---

## 40.4 Saturation

Une saturation du pool doit produire :

- timeout contrôlé ;
- logs sans chaîne de connexion ;
- alerte ;
- diagnostic.

---

# 41. Résilience

## 41.1 Défaillances transitoires

Les retries ne doivent être utilisés que pour des erreurs transitoires identifiées.

---

## 41.2 Écritures

Un retry d’écriture doit prendre en compte :

- transaction ;
- idempotence ;
- doublon ;
- effets externes.

---

## 41.3 Retry illimité

Les retries illimités sont interdits.

---

# 42. Timeouts

Les commandes PostgreSQL doivent posséder des timeouts adaptés.

---

## 42.1 Timeout global

Une valeur unique très élevée ne doit pas masquer les requêtes problématiques.

---

## 42.2 Opérations longues

Les migrations, imports et tâches d’administration peuvent utiliser des timeouts distincts de ceux du runtime web.

---

# 43. Health checks

## 43.1 Readiness

Le readiness vérifie la capacité à accéder à PostgreSQL lorsque la base est essentielle au fonctionnement.

---

## 43.2 Liveness

Le liveness ne doit pas dépendre directement de PostgreSQL.

Une panne PostgreSQL ne doit pas nécessairement faire redémarrer continuellement le processus applicatif.

---

## 43.3 Requête

Le health check doit utiliser une requête légère.

---

## 43.4 Réponse publique

Le résultat public ne doit pas exposer :

- nom de base ;
- serveur ;
- utilisateur ;
- chaîne de connexion ;
- erreur SQL détaillée.

---

# 44. Journalisation EF Core

## 44.1 Sensitive data logging

Le sensitive data logging est désactivé dans les environnements contenant des données réelles.

---

## 44.2 Paramètres

Les valeurs de paramètres SQL peuvent contenir :

- PII ;
- contenus ;
- secrets ;
- messages.

Elles ne doivent pas être journalisées en production.

---

## 44.3 Requêtes lentes

La détection des requêtes lentes peut enregistrer :

- durée ;
- opération ;
- type de requête ;
- correlation ID ;
- nombre de lignes lorsque disponible.

Elle ne doit pas recopier les données.

---

# 45. Observabilité PostgreSQL

Indicateurs recommandés :

```text
Disponibilité

Temps de connexion

Connexions actives

Saturation du pool

Durée des requêtes

Erreurs

Deadlocks

Taille de la base

Croissance

Taille des index

Durée des migrations

Âge du dernier backup
```

---

## 45.1 Données client

La centralisation des métriques ne doit pas exposer les données métier.

---

# 46. Sauvegarde

La stratégie complète est définie dans :

```text
23-backup-and-restore.md
```

---

## 46.1 Format logique

Le format recommandé est un dump PostgreSQL restaurable par `pg_restore`.

Exemple conceptuel :

```bash
pg_dump \
  --format=custom \
  --no-owner \
  --no-privileges
```

---

## 46.2 Vérification

Un backup PostgreSQL doit au minimum vérifier :

- fichier présent ;
- taille non nulle ;
- checksum ;
- `pg_restore --list` ;
- manifest ;
- version de l’outil.

---

## 46.3 Hors VPS

Une production gérée durablement doit disposer d’une copie hors VPS selon le périmètre de service.

---

# 47. Restauration

Une restauration doit pouvoir reconstruire :

- schéma ;
- données ;
- contraintes ;
- index ;
- historique EF Core ;
- comptes et permissions nécessaires.

---

## 47.1 Portabilité

Les dumps standards évitent de dépendre inutilement :

- du propriétaire source ;
- des ACL source ;
- du nom exact du serveur.

---

## 47.2 Version applicative

L’application déployée après restauration doit être compatible avec le schéma sauvegardé.

---

## 47.3 Restore drill

Une sauvegarde PostgreSQL n’est pas considérée comme pleinement fiable avant un restore drill réussi.

---

# 48. Mise à niveau PostgreSQL

## 48.1 Patch

Les correctifs compatibles peuvent être appliqués selon la procédure d’exploitation.

---

## 48.2 Version majeure

Une version majeure PostgreSQL nécessite :

- analyse de compatibilité ;
- provider EF Core compatible ;
- backup complet ;
- restore drill ;
- vérification des extensions ;
- staging ;
- tests ;
- fenêtre de maintenance ;
- rollback.

---

## 48.3 Tag d’image

Les images PostgreSQL utilisent une version explicite.

Le tag suivant est interdit en production :

```text
latest
```

---

## 48.4 Volume existant

Il est interdit de changer simplement le tag vers une nouvelle version majeure en réutilisant le même volume sans procédure validée.

---

# 49. Développement local

Le développement local utilise préférentiellement :

- Docker Compose ;
- Testcontainers ;
- une instance PostgreSQL locale compatible.

---

## 49.1 Données locales

Les données de développement sont fictives.

---

## 49.2 Réinitialisation

La base locale peut être recréée par un script explicite.

Cette facilité ne doit pas être copiée vers la production.

---

# 50. Tests unitaires

Les tests Domain et Application purs ne nécessitent pas PostgreSQL lorsqu’ils ne dépendent pas de la persistance.

Ils peuvent utiliser :

- objets en mémoire ;
- fakes ciblés ;
- ports de test.

---

## 50.1 Limite

Un fake ne prouve pas :

- la contrainte SQL ;
- la transaction ;
- l’index ;
- le mapping EF ;
- la concurrence ;
- la migration.

---

# 51. Tests d’intégration

Les tests d’intégration de persistance utilisent une véritable instance PostgreSQL.

---

## 51.1 Isolation

Chaque suite ou test doit disposer d’une base ou d’un schéma isolé selon la stratégie retenue.

---

## 51.2 Cycle

Le cycle doit être reproductible :

```text
création
→ migrations
→ seed fictif
→ test
→ nettoyage
```

---

## 51.3 Parallélisme

Le parallélisme ne doit pas provoquer de collision entre bases ou schémas de test.

---

# 52. Testcontainers

Testcontainers constitue l’approche recommandée lorsque l’environnement permet Docker.

---

## 52.1 Avantages

- version PostgreSQL contrôlée ;
- isolation ;
- reproductibilité ;
- environnement proche de la production ;
- nettoyage automatique.

---

## 52.2 Limites

- Docker requis ;
- démarrage plus lent ;
- ressources CI ;
- gestion des images.

---

## 52.3 Alternative CI

Un service PostgreSQL fourni par la CI peut être utilisé si son comportement et sa version sont explicitement contrôlés.

---

# 53. SQLite dans les tests

SQLite ne doit pas être utilisé comme remplacement général de PostgreSQL dans les tests d’intégration.

---

## 53.1 Raisons

Les différences peuvent concerner :

- types ;
- contraintes ;
- nullabilité ;
- index ;
- collation ;
- casse ;
- SQL ;
- transactions ;
- concurrence ;
- migrations ;
- fonctions ;
- dates ;
- JSON.

---

## 53.2 Usage possible

SQLite peut être utilisé dans un outil local distinct uniquement si :

- le comportement testé ne dépend pas de PostgreSQL ;
- l’usage est documenté ;
- il n’est pas présenté comme preuve de compatibilité PostgreSQL.

---

# 54. Tests de migration

La stratégie de tests doit couvrir :

```text
base vide
→ application de toutes les migrations

ancienne version supportée
→ upgrade vers la version cible

données existantes représentatives
→ migration

backup de test
→ restauration
→ migration
```

---

## 54.1 Migration manquante

La CI doit détecter, lorsque possible, un modèle EF Core modifié sans migration correspondante.

---

# 55. Tests de contraintes

Les contraintes importantes doivent être vérifiées directement avec PostgreSQL :

- unicité ;
- clés étrangères ;
- suppression ;
- nullabilité ;
- longueur ;
- concurrence ;
- transaction ;
- index lorsque pertinent.

---

# 56. Tests d’isolation client

Conformément à l’ADR-0001 :

- le compte du client A ne peut pas lire la base B ;
- staging ne peut pas accéder à production ;
- une migration cible la bonne base ;
- un backup contient le bon client et environnement ;
- un restore refuse une cible incohérente.

---

# 57. Tests de sécurité

Vérifier notamment :

- injection SQL ;
- chaîne de connexion absente des logs ;
- mot de passe absent des rapports ;
- port non public ;
- compte non superutilisateur ;
- credentials séparés ;
- migration non accessible au runtime ;
- backup protégé.

---

# 58. Tests de performance

Des tests ou mesures sont requis lorsque :

- volume significatif ;
- requête complexe ;
- import ;
- recherche ;
- pagination ;
- export ;
- index nouveau ;
- migration longue.

---

## 58.1 Pas de benchmark artificiel

Les données de test doivent représenter raisonnablement le scénario attendu.

---

# 59. Tests multiplateformes

Les tests Domain et Application s’exécutent sur :

```text
Windows x64
Linux x64
```

Les tests PostgreSQL critiques s’exécutent au minimum sur l’environnement Linux de CI et localement lorsque nécessaire.

---

# 60. Fichiers binaires

Les fichiers binaires volumineux ne sont pas stockés dans PostgreSQL dans le profil initial.

Ils sont stockés dans :

```text
App_Data
```

avec leurs métadonnées et références en base.

---

## 60.1 Raisons

- sauvegardes séparables ;
- volume de base maîtrisé ;
- accès fichiers simple ;
- transfert de médias ;
- possibilité future de stockage objet.

---

## 60.2 Cohérence

La stratégie de backup doit coordonner :

- métadonnées PostgreSQL ;
- fichiers `App_Data`.

---

## 60.3 Petites données binaires

Une donnée binaire limitée peut être stockée en base si :

- le besoin est justifié ;
- la taille est bornée ;
- l’impact backup est évalué ;
- le contrat est testé.

---

# 61. Contenus administrables

Les contenus éditoriaux administrables sont stockés dans PostgreSQL lorsqu’ils nécessitent :

- publication ;
- historique ;
- requêtes ;
- relations ;
- autorisation ;
- audit ;
- recherche.

---

## 61.1 Configuration structurelle

La configuration structurelle du projet reste dans :

```text
client.sitefactory.yml
```

Elle ne doit pas être déplacée en base uniquement pour permettre une modification runtime.

---

# 62. Identity

ASP.NET Core Identity utilise la base PostgreSQL du client.

Chaque projet possède ses propres :

- utilisateurs ;
- rôles ;
- claims ;
- tokens ;
- historiques ;
- migrations.

---

## 62.1 Normalisation

Les champs Identity sensibles à la casse et à l’unicité doivent être testés avec PostgreSQL.

---

## 62.2 Seed

Le seed administrateur reste idempotent et contrôlé.

---

# 63. Modules

Chaque module persistant doit documenter :

- entités ;
- tables ;
- relations ;
- index ;
- contraintes ;
- rétention ;
- migration ;
- backup ;
- suppression ;
- permissions.

---

## 63.1 Nommage

Les noms de tables et contraintes doivent éviter les collisions entre modules.

---

## 63.2 Désactivation

La désactivation d’un module ne supprime pas automatiquement ses données.

---

# 64. Conventions de nommage

La convention exacte sera centralisée dans la documentation d’architecture ou dans un ADR dédié si nécessaire.

Elle doit couvrir :

```text
tables

colonnes

index

contraintes

clés étrangères

séquences

migrations
```

---

## 64.1 Stabilité

Un renommage d’objet persistant doit être réalisé par migration explicite.

---

# 65. Nettoyage et rétention

Les données temporaires ou expirables doivent posséder :

- une date ;
- une politique ;
- une opération de purge ;
- un index adapté ;
- des tests ;
- un runbook si nécessaire.

---

## 65.1 Suppression massive

Une purge importante ne doit pas bloquer inutilement la production.

Elle peut nécessiter :

- lots ;
- limites ;
- monitoring ;
- fenêtre ;
- vacuum ;
- analyse.

---

# 66. Suppression et confidentialité

Une donnée supprimée de la base peut rester présente dans :

- backups ;
- exports ;
- logs autorisés ;
- systèmes externes.

La procédure de suppression doit prendre en compte la rétention des backups conformément à :

```text
23-backup-and-restore.md
```

---

# 67. Maintenance PostgreSQL

La maintenance opérationnelle doit prendre en compte :

- mises à jour ;
- espace disque ;
- vacuum ;
- statistiques ;
- index ;
- connexions ;
- requêtes lentes ;
- backups ;
- restore drills ;
- logs.

---

## 67.1 Automatisation native

Les mécanismes PostgreSQL automatiques ne doivent pas être désactivés sans analyse.

---

# 68. Saturation disque

PostgreSQL doit disposer d’une marge suffisante pour :

- croissance ;
- WAL ;
- index ;
- vacuum ;
- migrations ;
- dumps ;
- fichiers temporaires.

---

## 68.1 Alertes

Des alertes doivent exister avant le seuil critique.

---

## 68.2 Action interdite

Il est interdit de supprimer manuellement des fichiers dans le volume de données PostgreSQL afin de libérer de l’espace.

---

# 69. Incidents

Les runbooks doivent couvrir :

- conteneur arrêté ;
- disque plein ;
- volume absent ;
- corruption suspectée ;
- connexions saturées ;
- migration en échec ;
- backup en échec ;
- restauration ;
- perte du VPS.

---

# 70. Conséquences positives

La décision apporte :

- un moteur relationnel robuste ;
- une forte compatibilité Linux ;
- un coût de licence maîtrisé ;
- des migrations standardisées ;
- des transactions fiables ;
- des contraintes métier en base ;
- des sauvegardes portables ;
- une restauration indépendante par client ;
- un provider unique à tester ;
- une bonne capacité d’évolution ;
- une architecture transférable ;
- une réduction de la complexité multi-provider ;
- une meilleure cohérence des modules.

---

# 71. Conséquences négatives

La décision entraîne :

- dépendance à PostgreSQL ;
- dépendance au provider EF Core correspondant ;
- besoin de compétences PostgreSQL ;
- service supplémentaire à exploiter ;
- consommation de ressources ;
- backups et restore drills obligatoires ;
- migrations à maintenir ;
- upgrades majeurs à planifier ;
- tests d’intégration plus lents ;
- Docker requis pour certains scénarios locaux ;
- portabilité limitée vers d’autres moteurs SQL.

---

# 72. Risques acceptés

## 72.1 Verrouillage technologique relatif

Les projets utilisent des capacités PostgreSQL et ne sont pas garantis portables vers un autre moteur sans migration.

---

## 72.2 Charge opérationnelle

Chaque projet nécessite :

- base ;
- credentials ;
- sauvegarde ;
- monitoring ;
- maintenance.

---

## 72.3 Migrations

Une mauvaise migration peut affecter les données d’un client.

---

## 72.4 Version majeure

Les montées de version majeures nécessitent une procédure dédiée.

---

## 72.5 Ressources

Plusieurs bases ou instances sur un même VPS peuvent consommer davantage de mémoire et de stockage.

---

# 73. Mesures compensatoires

- isolation par client ;
- compte applicatif limité ;
- compte migration distinct ;
- port non public ;
- migrations explicites ;
- backup avant opération sensible ;
- restore drills ;
- tests PostgreSQL réels ;
- version PostgreSQL explicite ;
- inventaire des versions ;
- monitoring disque et connexions ;
- runbooks ;
- rapports ;
- upgrade planifié.

---

# 74. Sécurité

## 74.1 Actifs

- données personnelles ;
- leads ;
- comptes ;
- contenus ;
- documents ;
- permissions ;
- secrets ;
- backups.

---

## 74.2 Menaces

- injection SQL ;
- accès croisé client ;
- credential compromis ;
- port public ;
- compte superutilisateur ;
- fuite de chaîne de connexion ;
- backup exposé ;
- mauvaise restauration ;
- migration mal ciblée ;
- suppression accidentelle.

---

## 74.3 Contrôles

- requêtes paramétrées ;
- credentials isolés ;
- moindre privilège ;
- réseau privé ;
- validation du client et de l’environnement ;
- logs sans PII ;
- secrets hors Git ;
- backups chiffrés ;
- confirmations ;
- tests ;
- audits opérateur.

---

# 75. Performance et capacité

## 75.1 Baseline

PostgreSQL est considéré comme suffisamment performant pour les volumes initiaux des projets PME ciblés.

---

## 75.2 Mesure

Toute optimisation importante doit s’appuyer sur :

- métriques ;
- plan d’exécution ;
- volume ;
- scénario ;
- objectif.

---

## 75.3 Scaling

Les options futures peuvent inclure :

- augmentation des ressources ;
- tuning ;
- index ;
- cache ;
- read replica ;
- service managé ;
- VPS dédié.

Ces options nécessitent une analyse propre au projet.

---

# 76. Coûts

## 76.1 Coûts supplémentaires

- ressources VPS ;
- stockage ;
- sauvegardes ;
- monitoring ;
- maintenance ;
- expertise ;
- temps de CI.

---

## 76.2 Coûts évités

- licences SQL par client ;
- support multi-provider ;
- migrations multiples ;
- matrices de tests complexes ;
- limitations d’une base fichier ;
- migration précoce vers un moteur serveur.

---

## 76.3 Décision économique

Le coût d’exploitation de PostgreSQL est considéré comme inférieur au coût total d’un support multi-provider ou d’une migration future depuis une base insuffisante.

---

# 77. Maintenabilité

Un provider unique permet :

- conventions communes ;
- tests communs ;
- scripts communs ;
- backups communs ;
- runbooks communs ;
- expertise concentrée ;
- diagnostic standardisé.

---

## 77.1 Abstraction raisonnable

Domain et Application restent testables sans PostgreSQL lorsque la persistance n’est pas concernée.

Infrastructure assume le provider réel.

---

## 77.2 Sur-abstraction interdite

Il ne faut pas introduire une abstraction générique de base de données uniquement pour simuler un support multi-provider non prévu.

---

# 78. Compatibilité

## 78.1 .NET

La version du provider doit être compatible avec :

```text
.NET 10
Entity Framework Core utilisé par le projet
```

---

## 78.2 PostgreSQL

La version majeure utilisée par chaque projet doit appartenir à la matrice de support SiteFactory.

---

## 78.3 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, car cette décision constitue la baseline initiale.

Le remplacement futur de PostgreSQL constituerait une évolution majeure.

---

# 79. Migration

## 79.1 Projets existants

Au moment de cette décision, aucun projet SiteFactory stable n’utilise un autre moteur relationnel comme contrat officiel.

---

## 79.2 Prototype

Un prototype utilisant SQLite ou une base en mémoire doit être migré vers PostgreSQL avant d’être considéré comme projet de référence production.

---

## 79.3 Étapes générales

```text
modèle EF Core
→ provider PostgreSQL
→ migration initiale
→ PostgreSQL de test
→ tests
→ backup
→ staging
```

---

## 79.4 Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation d’appliquer une migration sur une production.

---

# 80. Réversibilité

## 80.1 Classification

```text
moyenne
```

---

## 80.2 Changement d’hébergeur PostgreSQL

La migration vers un autre hébergeur PostgreSQL reste relativement directe :

```text
backup
→ nouvelle instance compatible
→ restore
→ tests
→ bascule
```

---

## 80.3 Changement de moteur

Une migration vers SQL Server, SQLite ou un autre moteur nécessiterait :

- conversion du schéma ;
- conversion des types ;
- réécriture des migrations ;
- adaptation SQL ;
- export et import ;
- tests ;
- nouvelle stratégie opérationnelle ;
- nouvel ADR.

---

# 81. Plan d’implémentation

## Étape 1 — Dépendances

Ajouter les packages EF Core et PostgreSQL approuvés avec versions centralisées.

---

## Étape 2 — Infrastructure

Créer :

- DbContext ;
- configurations d’entités ;
- factory de développement ;
- options ;
- health checks.

---

## Étape 3 — Modèle initial

Configurer :

- Identity ;
- contenus initiaux ;
- modules MVP ;
- conventions communes.

---

## Étape 4 — Migration initiale

Créer la migration dans le repository client ou le projet de référence prévu.

---

## Étape 5 — Développement local

Ajouter PostgreSQL au profil Docker Compose local.

---

## Étape 6 — Tests

Ajouter :

- Testcontainers ;
- migration de base vide ;
- contraintes ;
- transactions ;
- Identity ;
- isolation.

---

## Étape 7 — Déploiement

Générer :

- service PostgreSQL ;
- volume ;
- réseau ;
- variables ;
- health check ;
- scripts de migration.

---

## Étape 8 — Opérations

Ajouter :

- backup ;
- restore ;
- monitoring ;
- runbooks ;
- upgrade PostgreSQL.

---

# 82. Tâches liées proposées

```text
SF-TASK-019 — Add PostgreSQL infrastructure packages

SF-TASK-020 — Create the SiteFactory reference DbContext

SF-TASK-021 — Add PostgreSQL configuration validation

SF-TASK-022 — Add PostgreSQL to local Docker Compose

SF-TASK-023 — Create the initial client migration

SF-TASK-024 — Add PostgreSQL integration test infrastructure

SF-TASK-025 — Test database migrations from an empty database

SF-TASK-026 — Generate isolated PostgreSQL resources per client

SF-TASK-027 — Generate explicit migration scripts

SF-TASK-028 — Add PostgreSQL backup scripts
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 83. Tests et preuves attendus

Pour considérer cette décision correctement implémentée, les preuves suivantes sont attendues :

```text
PostgreSQL démarre localement

Application se connecte avec un compte non superutilisateur

Migration initiale s’applique sur une base vide

Suite d’intégration utilise PostgreSQL réel

Contraintes uniques sont vérifiées

Clés étrangères sont vérifiées

Transactions sont vérifiées

Identity fonctionne sur PostgreSQL

Staging et production utilisent des bases distinctes

Le port PostgreSQL n’est pas public

Une chaîne de connexion n’apparaît pas dans les logs

Un dump est créé

Le dump est inspectable avec pg_restore

Un restore de test réussit

Le projet fonctionne sur Linux

Les tests Domain restent indépendants du provider
```

---

# 84. Conditions de révision

Réexaminer cette décision si :

- un client stratégique impose un autre moteur ;
- PostgreSQL n’est plus compatible avec la cible d’hébergement ;
- le coût d’exploitation devient disproportionné ;
- un besoin offline local devient central ;
- un produit desktop autonome remplace l’application web ;
- une base managée spécifique devient obligatoire ;
- le provider EF Core n’est plus maintenu ;
- un besoin de données non relationnelles dominant apparaît ;
- les volumes exigent une architecture spécialisée ;
- une contrainte réglementaire impose une technologie particulière ;
- les projets sur mesure nécessitent régulièrement SQL Server ;
- l’exploitation multi-base devient non soutenable.

---

# 85. Indicateurs à suivre

- taille des bases ;
- croissance mensuelle ;
- nombre de connexions ;
- saturation du pool ;
- durée moyenne des requêtes ;
- requêtes lentes ;
- erreurs PostgreSQL ;
- deadlocks ;
- durée des migrations ;
- nombre de migrations en échec ;
- durée des backups ;
- durée des restaurations ;
- taux de réussite des restore drills ;
- espace disque ;
- versions PostgreSQL actives ;
- temps d’exécution des tests d’intégration.

---

# 86. Signaux d’alerte

La décision doit être réexaminée ou son implémentation renforcée si les observations montrent :

```text
des connexions régulièrement saturées

des migrations trop longues

des bases mal isolées

des tests PostgreSQL trop coûteux pour la CI

des backups non restaurables

des versions majeures trop dispersées

des requêtes lentes récurrentes

une croissance de stockage non maîtrisée

des besoins majoritairement documentaires non relationnels

des projets sur mesure utilisant systématiquement un autre moteur
```

---

# 87. Conséquences observées

Cette section est append-only après mise en œuvre.

## 87.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète
de l’infrastructure de persistance SiteFactory.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet ;
- volume ;
- comportement ;
- résultat ;
- action proposée.

---

# 88. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- [generated-runtime-database-model.md](../03-generation/generated-runtime-database-model.md)
- [0027-database-roles-and-runtime-least-privilege.md](0027-database-roles-and-runtime-least-privilege.md)
- `13-generation-pipeline.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `21-observability-and-logging.md`
- `22-deployment-and-environments.md`
- `23-backup-and-restore.md`
- `24-operational-runbooks.md`
- `25-upgrade-and-maintenance-strategy.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0002-yaml-client-configuration.md`

---

# 89. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 84 |

---

# 90. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 91. Synthèse de la décision

```text
Projet client standard
→ PostgreSQL

Client + environnement
→ base distincte
→ compte distinct
→ migrations distinctes
→ backup distinct

Application runtime
→ compte non superutilisateur
→ réseau privé
→ aucun port PostgreSQL public

Migrations
→ repository client
→ application explicite
→ jamais automatiques en production

Tests de persistance
→ PostgreSQL réel
→ pas de substitution SQLite ou InMemory

Domain et Application
→ indépendants du provider

Infrastructure et opérations
→ dépendance PostgreSQL assumée
```

> SiteFactory ne cherche pas à être compatible avec toutes les bases de données. Il choisit PostgreSQL comme standard afin de concentrer ses tests, ses procédures et son expertise sur une plateforme relationnelle robuste, portable et exploitable pour chaque projet client.
