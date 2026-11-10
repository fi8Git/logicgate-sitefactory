---
adrId: ADR-0001
title: Use one repository, application and database per client
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
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/23-backup-and-restore.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/27-client-handover-and-service-closure.md
targetVersion: 0.1.0
---

# ADR-0001 — Un repository, une application et une base de données par client

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La décision d'isolation concerne les **applications clientes** : elle ne rend
pas une base PostgreSQL nécessaire au Core ou au site Documentation statique.
Le [périmètre public actuel](../../README.md) est un moteur et des bibliothèques
réutilisables, pas une application cliente Business générée ni un service
d'exploitation. Repositories clients, bases, secrets et opérations restent
isolés selon la décision ci-dessous lorsqu'ils sont effectivement déployés.
Aucune mutualisation implicite, aucun provisioning ou déploiement livré
n'est déduit de cet ADR.

## 1. Statut

```text
accepted
```

Cette décision constitue la baseline d’architecture des projets standards générés par LogicGate SiteFactory.

Toute exception doit faire l’objet :

- d’une analyse explicite ;
- d’un cadrage de projet sur mesure ;
- d’un nouvel ADR ;
- d’une validation LogicGate.

---

## 2. Résumé

LogicGate SiteFactory générera pour chaque client standard un projet logiciel isolé comprenant :

```text
un repository Git
+ une application déployable
+ une base PostgreSQL
+ des volumes persistants
+ une configuration
+ des secrets
+ un cycle de release
+ un cycle de sauvegarde
```

Les environnements d’un même client restent également séparés.

Exemple :

```text
Client Alpha / Staging
→ application staging
→ base PostgreSQL staging
→ volumes staging
→ secrets staging

Client Alpha / Production
→ application production
→ base PostgreSQL production
→ volumes production
→ secrets production
```

Le MVP SiteFactory ne proposera pas d’architecture applicative multi-tenant.

Plusieurs projets clients pourront néanmoins partager une infrastructure physique, comme un VPS ou un reverse proxy, à condition de conserver une isolation logique et opérationnelle explicite.

---

## 3. Contexte

SiteFactory doit industrialiser la création de sites et applications web pour des PME tout en conservant :

- un haut niveau de personnalisation ;
- une isolation claire des données ;
- des déploiements indépendants ;
- des sauvegardes restaurables par client ;
- une maintenance compréhensible ;
- une capacité de transfert au client ;
- un risque limité d’impact croisé ;
- une architecture adaptée à une petite équipe.

Les offres SiteFactory sont composées de :

- packs ;
- modules ;
- templates ;
- thèmes ;
- personnalisations client ;
- services mensuels optionnels.

Chaque projet client peut donc évoluer différemment en matière de :

- contenu ;
- identité visuelle ;
- modules activés ;
- règles métier ;
- données ;
- calendrier de livraison ;
- maintenance ;
- version applicative ;
- hébergement ;
- obligations de transfert.

Une architecture multi-tenant centralisée simplifierait certaines opérations globales, mais introduirait dès le MVP des contraintes importantes concernant :

- l’isolation des données ;
- l’autorisation par tenant ;
- les migrations globales ;
- les releases communes ;
- la restauration d’un seul client ;
- les personnalisations ;
- le transfert du projet ;
- le blast radius d’un incident ;
- la fermeture d’un service client.

Le contexte initial privilégie donc la sécurité, la lisibilité et la réversibilité plutôt que la mutualisation applicative maximale.

---

## 4. Problème à résoudre

Comment permettre à SiteFactory de produire et maintenir plusieurs projets clients personnalisables, sécurisés et transférables, sans introduire dès le MVP une plateforme multi-tenant complexe et un risque d’accès croisé aux données ?

La solution doit notamment permettre :

- d’isoler les données de chaque client ;
- de déployer un client sans affecter les autres ;
- de sauvegarder et restaurer un client indépendamment ;
- de personnaliser le code sans polluer le socle générique ;
- de transférer le repository et les données au client ;
- d’appliquer des versions différentes lorsque nécessaire ;
- de mutualiser certaines ressources physiques sans mélanger les responsabilités.

---

## 5. Portée

Cette décision couvre :

- les projets standards générés par SiteFactory ;
- les packs SiteFactory ;
- les modules génériques ;
- les personnalisations client ;
- les environnements staging et production ;
- PostgreSQL ;
- `App_Data` ;
- les clés ASP.NET Core Data Protection ;
- Docker Compose ;
- les images applicatives clientes ;
- les sauvegardes ;
- les migrations ;
- les releases clientes ;
- les transferts de service ;
- les opérations de maintenance.

---

## 6. Hors portée

Cette décision ne définit pas :

- le fournisseur VPS exact ;
- le nombre de clients par VPS ;
- le dimensionnement matériel ;
- la politique tarifaire d’hébergement ;
- le nom du provider de stockage de sauvegarde ;
- la haute disponibilité multi-région ;
- la réplication PostgreSQL ;
- l’architecture d’un futur produit SaaS ;
- les projets sur mesure soumis à des contraintes réglementaires particulières ;
- l’éventuelle plateforme interne d’inventaire des projets.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Isolation des données clientes**
2. **Réduction du risque d’accès croisé**
3. **Limitation du blast radius**
4. **Sauvegarde et restauration indépendantes**
5. **Transfert complet et réversibilité client**
6. **Personnalisation par configuration et code isolé**
7. **Déploiements et migrations indépendants**
8. **Lisibilité opérationnelle pour une petite équipe**
9. **Compatibilité avec les offres SiteFactory**
10. **Coût d’infrastructure raisonnable**
11. **Capacité d’automatisation future**
12. **Possibilité de mutualiser le VPS sans mutualiser les données**

---

## 8. Contraintes

La décision doit respecter les contraintes suivantes :

```text
.NET 10

Blazor

PostgreSQL

Docker Compose sur VPS Linux pour le profil initial

Git et GitHub

Un projet généré doit pouvoir être autonome

Les migrations de production restent explicites

Les secrets sont séparés par environnement

La sécurité fondamentale est obligatoire

Le transfert client doit rester possible

Le multi-tenant est hors périmètre du MVP
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le nombre initial de clients restera compatible avec une exploitation par projet ;
- les clients n’ont pas besoin de partager leurs données ;
- les projets standards peuvent utiliser le même socle technique sans partager le même runtime ;
- les mises à niveau pourront être automatisées progressivement ;
- plusieurs stacks pourront partager un VPS tout en conservant des volumes, bases et secrets distincts ;
- la simplicité d’isolation apporte plus de valeur au MVP que la mutualisation applicative maximale ;
- la majorité des sites PME n’exige pas de base centrale commune ;
- les cycles de livraison pourront varier d’un client à l’autre ;
- certains clients pourront demander un transfert complet de leur projet.

---

## 10. Définitions utilisées dans cette décision

## 10.1 Projet client

Ensemble des actifs appartenant au cycle logiciel d’un client :

```text
repository
configuration
code
customizations
migrations
tests
documentation
releases
```

---

## 10.2 Stack client

Ensemble des services déployés pour un client et un environnement :

```text
application
PostgreSQL
volumes
réseaux
configuration
secrets
health checks
```

---

## 10.3 Isolation logique

Séparation par :

- processus ;
- conteneurs ;
- base ;
- credentials ;
- volumes ;
- réseau ;
- configuration ;
- droits ;
- sauvegardes.

---

## 10.4 Isolation physique

Utilisation de ressources physiques distinctes :

- VPS ;
- machine ;
- stockage ;
- réseau ;
- compte fournisseur.

Cette décision impose l’isolation logique.

Elle n’impose pas systématiquement un VPS distinct par client.

---

## 10.5 Multi-tenant

Architecture dans laquelle une même instance applicative sert plusieurs clients et sélectionne le tenant à l’exécution.

Cette architecture n’est pas retenue pour le MVP.

---

# 11. Option A — Application et base multi-tenant partagées

## 11.1 Description

Une application unique dessert tous les clients.

Une base PostgreSQL unique contient les données de plusieurs clients, distinguées par un identifiant de tenant.

Exemple :

```text
Shared application

Shared PostgreSQL

TenantId on business tables

Shared deployment

Shared release cycle
```

---

## 11.2 Avantages

- mutualisation maximale de l’infrastructure ;
- déploiement unique ;
- mise à jour globale rapide ;
- supervision centralisée ;
- agrégation de données facilitée ;
- nombre réduit de stacks.

---

## 11.3 Inconvénients

- contrôle du tenant obligatoire sur chaque accès ;
- risque élevé d’accès croisé en cas d’erreur ;
- migrations globales ;
- release commune à tous les clients ;
- personnalisation plus complexe ;
- restauration d’un seul client difficile ;
- export et transfert client plus complexes ;
- incident applicatif affectant tous les clients ;
- suppression d’un tenant plus délicate ;
- tests d’isolation nombreux et critiques.

---

## 11.4 Risques

- fuite de données entre clients ;
- oubli d’un filtre de tenant ;
- cache partagé incorrectement ;
- background job exécuté pour le mauvais client ;
- mauvaise résolution du tenant ;
- migration globale bloquant tous les clients ;
- impossibilité de restaurer un tenant sans affecter les autres ;
- complexité des permissions et audits.

---

## 11.5 Impacts opérationnels

- une erreur de déploiement peut affecter tous les clients ;
- une saturation peut dégrader toute la plateforme ;
- les backups doivent supporter une restauration partielle ;
- le transfert client nécessite un export spécifique ;
- les versions clientes ne peuvent pas diverger facilement ;
- les personnalisations importantes nécessitent des branches conditionnelles ou extensions runtime.

---

## 11.6 Réversibilité

```text
faible
```

Une migration depuis un système multi-tenant partagé vers des projets isolés nécessite :

- extraction des données ;
- séparation des secrets ;
- création de repositories ;
- création de stacks ;
- validation client par client.

---

# 12. Option B — Application partagée et base séparée par client

## 12.1 Description

Une application partagée dessert plusieurs clients.

Chaque client possède une base PostgreSQL distincte.

Le runtime sélectionne la base selon le tenant.

Exemple :

```text
Shared application

Database per client

Runtime tenant resolution

Shared release cycle
```

---

## 12.2 Avantages

- meilleure isolation des données que la base partagée ;
- backups PostgreSQL indépendants ;
- mutualisation de l’application ;
- nombre réduit d’images et déploiements ;
- possibilité de dimensionner les bases séparément.

---

## 12.3 Inconvénients

- résolution du tenant toujours critique ;
- cycle de release applicatif commun ;
- personnalisation du code limitée ;
- gestion de nombreuses chaînes de connexion ;
- migrations à orchestrer sur toutes les bases ;
- erreur runtime pouvant sélectionner la mauvaise base ;
- transfert du code moins autonome ;
- dépendance persistante à une plateforme partagée.

---

## 12.4 Risques

- connexion à la mauvaise base ;
- secret d’un client exposé au runtime partagé ;
- migration partiellement appliquée ;
- version applicative incompatible avec certaines bases ;
- incident applicatif affectant tous les clients ;
- impossibilité de livrer facilement une version spécifique.

---

## 12.5 Impacts opérationnels

- orchestration des migrations sur plusieurs bases ;
- inventaire des versions de schéma ;
- gestion complexe des secrets ;
- surveillance centralisée mais blast radius applicatif important ;
- transfert client nécessitant la création d’un runtime autonome.

---

## 12.6 Réversibilité

```text
moyenne à faible
```

Les données sont déjà séparées, mais il faut extraire :

- le code ;
- la configuration ;
- les images ;
- les secrets ;
- les personnalisations ;
- les procédures.

---

# 13. Option C — Repository, application et base isolés par client

## 13.1 Description

Chaque client possède :

```text
son repository

son application

sa base PostgreSQL

ses migrations

ses volumes

ses secrets

ses releases

ses sauvegardes

ses runbooks
```

Staging et production sont également isolés.

Les projets partagent uniquement les composants génériques versionnés :

- packages ;
- modules ;
- templates ;
- thèmes ;
- outils SiteFactory ;
- documentation commune.

---

## 13.2 Avantages

- isolation forte des données ;
- faible risque d’accès croisé ;
- déploiements indépendants ;
- migrations indépendantes ;
- backups indépendants ;
- restaurations indépendantes ;
- transfert client simplifié ;
- versions différentes possibles ;
- personnalisations isolées ;
- incident applicatif limité à un client ;
- configuration plus facile à comprendre ;
- suppression ou clôture d’un client plus simple.

---

## 13.3 Inconvénients

- multiplication des repositories ;
- multiplication des bases ;
- multiplication des stacks ;
- campagnes d’upgrade plus longues ;
- inventaire technique nécessaire ;
- davantage d’images et de releases ;
- risque de divergence entre projets ;
- charge d’exploitation croissante avec le nombre de clients ;
- consommation minimale de ressources par stack.

---

## 13.4 Risques

- clients restant sur une version ancienne ;
- correctif non propagé partout ;
- oubli d’un backup ou d’une mise à jour ;
- saturation d’un VPS mutualisé ;
- dérive de configuration ;
- scripts opératoires exécutés sur le mauvais client ;
- coût croissant de maintenance.

---

## 13.5 Impacts opérationnels

- besoin d’un inventaire central des versions ;
- besoin d’automatiser `upgrade check` et `upgrade plan` ;
- besoin de conventions de nommage strictes ;
- besoin de rapports de génération, déploiement et backup ;
- besoin de runbooks par projet ;
- besoin de surveiller les ressources mutualisées ;
- nécessité de distinguer l’infrastructure physique de la responsabilité client.

---

## 13.6 Réversibilité

```text
élevée
```

Chaque projet peut être :

- transféré ;
- migré ;
- archivé ;
- déplacé vers un autre VPS ;
- exécuté indépendamment ;
- restauré indépendamment.

Une évolution future vers une plateforme mutualisée reste possible, mais nécessiterait un nouvel ADR et une migration volontaire.

---

# 14. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Option A | Option B | Option C |
|---|---:|---:|---:|---:|
| Isolation des données | 5 | 2 | 4 | 5 |
| Risque d’accès croisé | 5 | 2 | 3 | 5 |
| Blast radius | 5 | 1 | 2 | 5 |
| Backup par client | 5 | 2 | 5 | 5 |
| Restauration par client | 5 | 1 | 4 | 5 |
| Transfert client | 5 | 1 | 3 | 5 |
| Personnalisation | 4 | 2 | 2 | 5 |
| Déploiement indépendant | 4 | 1 | 1 | 5 |
| Simplicité du MVP | 5 | 2 | 2 | 4 |
| Mutualisation des ressources | 3 | 5 | 4 | 2 |
| Mise à jour globale | 3 | 5 | 4 | 2 |
| Lisibilité opérationnelle | 4 | 2 | 2 | 4 |
| Réversibilité | 4 | 1 | 3 | 5 |

La matrice soutient l’option C pour les priorités initiales de SiteFactory.

---

# 15. Décision

Nous décidons que chaque projet client standard généré par SiteFactory possédera :

```text
un repository Git dédié

une application déployable dédiée

une base PostgreSQL dédiée

un compte PostgreSQL applicatif dédié

des migrations dédiées

des volumes persistants dédiés

des secrets dédiés

une configuration dédiée

un cycle de release dédié

une stratégie de sauvegarde dédiée
```

Le MVP ne comportera pas :

```text
d’application runtime multi-tenant

de base métier partagée entre plusieurs clients

de TenantId global obligatoire dans les entités métier

de résolution dynamique de tenant à chaque requête
```

---

## 15.1 Environnements

Pour un même client, staging et production possèdent également :

- des bases distinctes ;
- des secrets distincts ;
- des volumes distincts ;
- des clés Data Protection distinctes ;
- des noms Compose distincts ;
- des domaines distincts ;
- des sauvegardes distinctes.

---

## 15.2 Infrastructure physique

Plusieurs stacks clientes peuvent partager :

- un VPS ;
- une installation Docker ;
- un reverse proxy Caddy ;
- un registry ;
- un stockage de backup ;
- un système de monitoring.

Cette mutualisation n’autorise pas :

- le partage des bases ;
- le partage des credentials ;
- le partage des volumes métier ;
- le partage des clés Data Protection ;
- le partage des comptes applicatifs ;
- le mélange des sauvegardes.

---

## 15.3 Composants génériques

Les projets clients peuvent consommer les mêmes versions de :

- packages SiteFactory ;
- contrats ;
- modules ;
- design system ;
- templates ;
- thèmes ;
- profils de déploiement.

Ces composants restent versionnés et publiés séparément.

---

# 16. Justification

L’option retenue répond le mieux aux priorités du MVP :

- elle réduit fortement le risque de fuite de données entre clients ;
- elle simplifie le raisonnement sur les autorisations ;
- elle limite l’impact d’un défaut applicatif ;
- elle rend les backups et restaurations plus directs ;
- elle permet un transfert complet au client ;
- elle supporte des personnalisations importantes ;
- elle évite de construire prématurément une plateforme SaaS ;
- elle correspond aux capacités opérationnelles d’une petite équipe ;
- elle permet d’industrialiser progressivement les opérations répétitives.

La charge opérationnelle supplémentaire est considérée comme acceptable au regard des bénéfices d’isolation, de sécurité et de réversibilité.

---

# 17. Structure de référence

Exemple conceptuel :

```text
SiteFactory repository
├── CLI
├── Generator
├── Catalog
├── Modules
├── Templates
└── Themes

Client Alpha repository
├── configuration
├── application
├── customizations
├── migrations
├── tests
├── deployment
└── operations

Client Beta repository
├── configuration
├── application
├── customizations
├── migrations
├── tests
├── deployment
└── operations
```

---

# 18. Base PostgreSQL

Chaque client et environnement possède une base distincte.

Exemple :

```text
alpha_staging
alpha_production

beta_staging
beta_production
```

Les noms exacts doivent respecter les conventions d’exploitation et éviter d’exposer inutilement des informations commerciales.

---

## 18.1 Credentials

Chaque base possède un compte applicatif distinct.

Un credential ne doit pas permettre l’accès aux bases d’autres clients.

---

## 18.2 Superutilisateur

L’application ne s’exécute pas avec un compte PostgreSQL superutilisateur.

---

## 18.3 Port public

PostgreSQL n’est pas exposé publiquement par défaut.

---

# 19. Migrations

Les migrations EF Core appartiennent au repository client.

Cette règle permet de conserver :

- la composition exacte des modules ;
- les personnalisations ;
- l’historique du schéma ;
- la version applicative associée ;
- la capacité de transfert.

---

## 19.1 Application

Les migrations de production restent explicites.

Le démarrage de l’application ne doit pas appeler automatiquement :

```text
Database.Migrate()
```

---

## 19.2 Divergence

Deux clients peuvent posséder des historiques de migration différents si :

- leurs modules diffèrent ;
- leurs personnalisations diffèrent ;
- leurs versions diffèrent.

Cette divergence est autorisée et doit rester traçable.

---

# 20. Isolation des volumes

Chaque environnement possède ses propres volumes :

```text
PostgreSQL data

App_Data

Data Protection Keys

Caddy data lorsque applicable
```

Un volume métier ne doit pas être monté par deux clients différents.

---

## 20.1 Convention de nommage

Les noms doivent inclure un identifiant technique stable du projet et de l’environnement.

Exemple conceptuel :

```text
sf_alpha_production_postgres_data

sf_alpha_production_app_data
```

---

## 20.2 Suppression

Une commande ne doit jamais supprimer un volume uniquement à partir d’un préfixe ambigu.

---

# 21. Isolation des secrets

Chaque client et environnement possède ses propres valeurs pour :

- PostgreSQL ;
- SMTP ;
- registry si nécessaire ;
- stockage backup ;
- providers externes ;
- Data Protection ;
- administration initiale.

---

## 21.1 Réutilisation interdite

Le même mot de passe PostgreSQL ou token ne doit pas être réutilisé entre plusieurs clients.

---

## 21.2 Inventaire

Un inventaire peut enregistrer les noms et responsables des secrets, mais jamais leurs valeurs.

---

# 22. Identity

Chaque application possède son propre périmètre Identity.

Les comptes utilisateurs d’un client ne sont pas partagés avec un autre client dans le MVP.

---

## 22.1 Administrateurs

Chaque projet possède ses propres :

- administrateurs ;
- rôles ;
- permissions ;
- sessions ;
- clés Data Protection ;
- procédures de récupération.

---

## 22.2 Authentification centralisée future

Une éventuelle fédération d’identité nécessitera un ADR distinct.

Elle ne doit pas introduire implicitement un partage de données métier.

---

# 23. Déploiement

Chaque projet client possède :

- son fichier Compose ;
- son image ;
- son tag ;
- ses variables ;
- ses volumes ;
- ses health checks ;
- ses scripts ;
- son rapport de déploiement.

---

## 23.1 Caddy mutualisé

Caddy peut être mutualisé.

Chaque route doit pointer explicitement vers le service du bon client.

---

## 23.2 Réseau

Un réseau proxy partagé peut être utilisé.

Les bases restent sur des réseaux privés non exposés au reverse proxy lorsque cela n’est pas nécessaire.

---

## 23.3 Noms Compose

Chaque projet utilise un nom Compose unique afin d’éviter :

- collisions de conteneurs ;
- collisions de réseaux ;
- collisions de volumes ;
- erreurs opérateur.

---

# 24. Versions

Chaque projet client possède sa propre version applicative.

Exemple :

```text
Client Alpha application : 1.4.0

Client Beta application : 1.2.3
```

Ils peuvent avoir été générés ou mis à niveau avec des versions SiteFactory différentes.

---

## 24.1 Lock file

Chaque repository client contient :

```text
sitefactory.lock.json
```

Il enregistre la composition exacte :

- générateur ;
- catalogue ;
- template ;
- thème ;
- modules ;
- profil de déploiement.

---

# 25. Releases

Une release SiteFactory ne déclenche pas automatiquement :

- une release client ;
- un merge ;
- une migration ;
- un déploiement.

Chaque client suit :

```text
upgrade check
→ upgrade plan
→ branche
→ tests
→ staging
→ déploiement
```

---

# 26. Sauvegardes

Les sauvegardes sont isolées par :

```text
client
+ environnement
+ composant
+ timestamp
```

---

## 26.1 Restauration

Une restauration d’un client ne doit pas nécessiter de restaurer les données d’un autre client.

---

## 26.2 Stockage mutualisé

Un stockage hors VPS peut être mutualisé à condition d’utiliser :

- préfixes distincts ;
- permissions adaptées ;
- chiffrement ;
- manifests ;
- checksums ;
- contrôles de destination.

---

## 26.3 Interdiction

Un backup d’un client ne doit jamais être restauré dans le projet d’un autre client sans procédure explicite, autorisation et mesures de protection.

---

# 27. Observabilité

Les métriques et logs peuvent être centralisés.

Ils doivent conserver :

```text
ClientSlug technique

Environment

ApplicationVersion

CorrelationId
```

sans mélanger les données métier.

---

## 27.1 PII

La centralisation ne justifie pas l’envoi de données personnelles dans les logs.

---

## 27.2 Accès

Les accès aux logs doivent suivre les responsabilités d’exploitation.

---

# 28. Customizations

Les personnalisations spécifiques appartiennent au repository client.

Emplacement de référence :

```text
ClientName.Customizations
```

---

## 28.1 Socle générique

Le socle SiteFactory ne contient pas :

```text
if client == "alpha"
```

---

## 28.2 Généralisation

Une personnalisation peut devenir un module générique uniquement après :

- analyse de réutilisabilité ;
- documentation ;
- décision LogicGate ;
- versionnement ;
- tests génériques.

---

# 29. Entitlements

Les entitlements sont évalués dans le périmètre de chaque application cliente.

Il n’existe pas dans le MVP de service runtime central chargé d’autoriser tous les clients.

---

## 29.1 Source

Les entitlements proviennent de la configuration et du lock file du projet concerné.

---

## 29.2 Modification

Une modification du catalogue n’active pas rétroactivement une capacité dans les projets existants.

---

# 30. Transfert client

L’architecture retenue permet de remettre au client :

- son repository ;
- ses images ;
- ses données ;
- ses migrations ;
- ses secrets recréés ou transférés ;
- ses backups ;
- ses runbooks ;
- sa configuration ;
- son historique de release.

---

## 30.1 Pas de dépendance runtime obligatoire

Après transfert, le projet ne doit pas dépendre d’un service runtime multi-tenant appartenant uniquement à LogicGate pour fonctionner.

Des packages privés ou services externes autorisés peuvent nécessiter un plan de continuité documenté.

---

# 31. Clôture de service

La clôture d’un client peut être réalisée sans arrêter les autres projets.

Elle peut inclure :

- backup final ;
- transfert ;
- retrait Caddy ;
- retrait DNS ;
- arrêt de stack ;
- suppression contrôlée des volumes ;
- révocation des secrets ;
- archivage du repository.

---

# 32. Mutualisation physique du VPS

## 32.1 Autorisée

Plusieurs projets peuvent partager un VPS si :

- les ressources sont suffisantes ;
- les volumes sont distincts ;
- les bases sont distinctes ;
- les secrets sont distincts ;
- les stacks sont identifiables ;
- les backups sont isolés ;
- les routes Caddy sont testées ;
- la saturation est surveillée.

---

## 32.2 Risque accepté

Un incident du VPS peut affecter plusieurs clients.

Cette décision accepte donc :

```text
une isolation applicative et de données forte
sans imposer une isolation physique systématique
```

---

## 32.3 Mesures compensatoires

- monitoring CPU, mémoire, disque et I/O ;
- limites de ressources lorsque pertinentes ;
- sauvegardes hors VPS ;
- runbook multi-client ;
- possibilité de migrer un client vers un VPS dédié ;
- inventaire des stacks ;
- séparation des réseaux et volumes.

---

# 33. VPS dédié

Un VPS dédié peut être requis lorsque :

- le client possède une criticité supérieure ;
- le trafic ou stockage est important ;
- la réglementation l’impose ;
- le risque de voisin bruyant est inacceptable ;
- le contrat le prévoit ;
- les ressources mutualisées deviennent insuffisantes.

La décision d’utiliser un VPS dédié ne remet pas en cause le présent ADR.

---

# 34. Infrastructure interne SiteFactory

Le repository SiteFactory reste distinct des repositories clients.

Il contient :

- générateur ;
- CLI ;
- packages ;
- modules ;
- templates ;
- thèmes ;
- catalogues ;
- documentation générique.

Il ne contient pas :

- secrets clients ;
- données clientes ;
- dumps clients ;
- customizations confidentielles non généralisées.

---

# 35. Inventaire central

LogicGate peut maintenir un inventaire central contenant uniquement des métadonnées techniques.

Exemples :

```text
ClientSlug

Repository

ApplicationVersion

GeneratorVersion

ModuleVersions

ProductionDomain

HostingLocation

MaintenanceStatus

LastBackupAtUtc

LastRestoreDrillAtUtc
```

---

## 35.1 Non multi-tenant

Cet inventaire ne transforme pas les applications clientes en architecture multi-tenant.

---

## 35.2 Données interdites

L’inventaire ne contient pas :

- leads ;
- contenus ;
- comptes ;
- mots de passe ;
- chaînes de connexion ;
- documents clients ;
- secrets.

---

# 36. Conséquences positives

La décision apporte :

- une frontière de sécurité claire ;
- une réduction du risque de fuite croisée ;
- une limitation du blast radius applicatif ;
- des sauvegardes simples à identifier ;
- une restauration par client ;
- des cycles de release autonomes ;
- une forte capacité de personnalisation ;
- une meilleure réversibilité ;
- un transfert client simplifié ;
- une clôture de service isolée ;
- une meilleure lisibilité du modèle de responsabilité ;
- une migration vers un VPS dédié facilitée.

---

# 37. Conséquences négatives

La décision entraîne :

- davantage de repositories ;
- davantage de pipelines ;
- davantage de bases PostgreSQL ;
- davantage de stacks Compose ;
- davantage de secrets ;
- davantage de sauvegardes ;
- davantage de versions clientes ;
- une charge de maintenance potentiellement répétitive ;
- un besoin d’inventaire ;
- un besoin d’automatisation des upgrades ;
- une consommation mémoire supérieure à une application unique ;
- une surveillance plus détaillée.

---

# 38. Risques acceptés

## 38.1 Divergence des versions

Certains clients pourront rester sur des versions anciennes.

---

## 38.2 Propagation des correctifs

Un correctif SiteFactory devra être appliqué à plusieurs projets.

---

## 38.3 Charge opérationnelle

Le nombre d’opérations augmente avec le nombre de clients.

---

## 38.4 Mutualisation du VPS

Une panne physique peut affecter plusieurs stacks.

---

## 38.5 Dérive de configuration

Une modification manuelle non reportée peut créer une divergence.

---

# 39. Mesures compensatoires

## 39.1 Inventaire des versions

Maintenir un inventaire technique des projets supportés.

---

## 39.2 Upgrade automation

Développer progressivement :

```text
sitefactory upgrade check
sitefactory upgrade plan
sitefactory upgrade apply
```

L’application en production reste humaine.

---

## 39.3 Quality gates

Chaque projet possède une CI vérifiant :

- build ;
- tests ;
- sécurité ;
- image ;
- projet généré lorsque pertinent.

---

## 39.4 Rapports

Produire :

- `generation-report.json` ;
- `upgrade-report.json` ;
- `deployment-report.json` ;
- `backup-manifest.json`.

---

## 39.5 Conventions

Utiliser des noms stables pour :

- repositories ;
- projets Compose ;
- bases ;
- volumes ;
- images ;
- backups ;
- environnements.

---

## 39.6 Maintenance

Définir une cadence de maintenance et de revue des versions.

---

# 40. Sécurité

## 40.1 Actifs protégés

- données métier ;
- données personnelles ;
- comptes administrateurs ;
- secrets ;
- documents ;
- médias ;
- backups ;
- clés Data Protection.

---

## 40.2 Frontières de confiance

Chaque projet client constitue une frontière de confiance distincte.

---

## 40.3 Menaces principales

- accès à la base d’un autre client ;
- montage du mauvais volume ;
- restauration croisée ;
- mauvaise route Caddy ;
- mauvais secret injecté ;
- commande opérateur exécutée dans le mauvais dossier ;
- backup envoyé dans le mauvais préfixe ;
- logs mélangeant des données.

---

## 40.4 Contrôles

- credentials PostgreSQL dédiés ;
- bases distinctes ;
- volumes distincts ;
- noms Compose uniques ;
- secrets distincts ;
- validation du client et de l’environnement ;
- confirmations sur les opérations destructives ;
- manifests et checksums ;
- tests d’isolation ;
- accès minimaux.

---

# 41. Tests de sécurité requis

Des tests doivent couvrir lorsque applicable :

```text
Un compte DB client A ne peut pas lire client B.

Le service web client A ne monte pas App_Data client B.

Une restauration refuse un manifest d’un autre client.

Les routes Caddy pointent vers les bons backends.

Les secrets de staging ne sont pas utilisés en production.

Les logs ne contiennent pas de chaîne de connexion.

Les scripts refusent un client ou environnement inconnu.
```

---

# 42. Données

## 42.1 Propriété

Les données d’un projet appartiennent au périmètre du client concerné selon les règles contractuelles applicables.

---

## 42.2 Classification

La classification est réalisée dans chaque projet.

---

## 42.3 Rétention

La rétention est définie par client et type de donnée.

---

## 42.4 Suppression

Une suppression client n’affecte pas les autres bases.

---

## 42.5 Portabilité

Les données peuvent être exportées via :

- dump PostgreSQL ;
- archive `App_Data` ;
- manifest ;
- checksums ;
- documentation de restauration.

---

# 43. Performance et capacité

## 43.1 Coût minimal par stack

Chaque application et base consomment :

- mémoire ;
- CPU ;
- stockage ;
- file descriptors ;
- connexions ;
- logs.

---

## 43.2 Dimensionnement

Le nombre de stacks par VPS doit être décidé à partir de mesures réelles.

---

## 43.3 Monitoring

Surveiller :

- CPU ;
- mémoire ;
- disque ;
- I/O ;
- taille PostgreSQL ;
- taille App_Data ;
- taille des backups ;
- redémarrages ;
- latence.

---

## 43.4 Voisin bruyant

Un client consommant trop de ressources peut être :

- limité ;
- optimisé ;
- déplacé ;
- isolé sur un VPS dédié.

---

# 44. Coûts

## 44.1 Coûts supplémentaires

- davantage de stockage ;
- davantage de mémoire ;
- davantage de maintenance ;
- davantage de sauvegardes ;
- automatisation à développer ;
- inventaire à maintenir.

---

## 44.2 Coûts évités

- développement d’une architecture multi-tenant complète ;
- tests permanents d’isolation tenant ;
- extraction complexe lors d’un transfert ;
- restauration partielle complexe ;
- personnalisation conditionnelle dans un runtime partagé ;
- gestion d’un incident affectant tous les clients.

---

## 44.3 Décision économique

Pour le MVP, le coût opérationnel supplémentaire est jugé inférieur au coût et au risque d’une plateforme multi-tenant prématurée.

---

# 45. Maintenabilité

## 45.1 Avantage

Chaque projet reste compréhensible comme une application autonome.

---

## 45.2 Risque

Des duplications peuvent apparaître entre repositories.

---

## 45.3 Réponse

Les éléments génériques doivent être extraits dans :

- packages ;
- modules ;
- templates ;
- scripts versionnés ;
- documentation commune.

Les personnalisations restent dans les projets clients.

---

# 46. Exploitation

Cette décision nécessite :

- conventions strictes ;
- inventaire ;
- scripts contextuels ;
- health checks ;
- backups automatisés ;
- runbooks ;
- rapports ;
- alertes par client et environnement.

---

## 46.1 Commandes opérateur

Toute commande sensible doit identifier explicitement :

```text
client
+ environnement
```

---

## 46.2 Verrouillage

Les déploiements, migrations, backups et restaurations doivent être verrouillés par client et environnement.

---

# 47. Compatibilité

## 47.1 Application

Chaque projet peut évoluer indépendamment dans les limites de la politique de support.

---

## 47.2 Packages

Les packages SiteFactory utilisent des versions explicites.

---

## 47.3 PostgreSQL

Les versions majeures PostgreSQL peuvent varier temporairement entre projets pendant une migration planifiée.

Cette divergence doit rester inventoriée et supportée.

---

## 47.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque la décision constitue la baseline initiale.

Une migration depuis une future architecture mutualisée serait un breaking change majeur.

---

# 48. Migration

## 48.1 Projets existants

Au moment de l’acceptation de cet ADR, aucun projet SiteFactory multi-tenant de production n’est à migrer.

---

## 48.2 Projets futurs

Les nouveaux projets utilisent directement la baseline isolée.

---

## 48.3 Projet pilote existant

Tout prototype utilisant des ressources partagées doit être analysé avant intégration au standard.

---

## 48.4 Production

```text
Production execution: out of scope
```

Cet ADR définit une architecture, pas une opération de production.

---

# 49. Réversibilité

## 49.1 Classification

```text
élevée pour le transfert et l’isolation supplémentaire

moyenne à faible pour une future convergence vers du multi-tenant
```

---

## 49.2 Vers un VPS dédié

Le déplacement d’une stack isolée vers un nouveau VPS est direct :

```text
backup
→ nouvelle infrastructure
→ restauration
→ déploiement
→ DNS
```

---

## 49.3 Vers une plateforme multi-tenant

Une évolution vers le multi-tenant nécessiterait :

- nouveau modèle d’identité tenant ;
- contrats d’isolation ;
- stratégie de données ;
- migration de chaque client ;
- stratégie de personnalisation ;
- stratégie de transfert ;
- nouvel ADR.

---

# 50. Plan d’implémentation

## Étape 1 — Conventions

Définir :

- client slug ;
- environment ID ;
- noms de repository ;
- noms Compose ;
- noms de base ;
- noms de volumes ;
- chemins de backup.

---

## Étape 2 — Configuration

Créer le schéma `client.sitefactory.yml` avec :

- identité client ;
- environnement ;
- modules ;
- déploiement ;
- base ;
- stockage.

---

## Étape 3 — Génération

Générer :

- repository autonome ;
- solution ;
- Compose ;
- configuration ;
- tests ;
- opérations.

---

## Étape 4 — PostgreSQL

Créer :

- base ;
- compte applicatif ;
- migrations ;
- volumes ;
- tests d’isolation.

---

## Étape 5 — Déploiement

Définir :

- stack ;
- réseaux ;
- Caddy ;
- secrets ;
- health checks ;
- rapports.

---

## Étape 6 — Opérations

Ajouter :

- backup ;
- restore ;
- upgrades ;
- inventaire ;
- runbooks ;
- transfert.

---

# 51. Tâches liées proposées

```text
SF-TASK-001 — Bootstrap the SiteFactory solution

SF-TASK-002 — Define the client project identity model

SF-TASK-003 — Validate client and environment slugs

SF-TASK-004 — Define isolated resource naming conventions

SF-TASK-005 — Generate a client-specific Compose project

SF-TASK-006 — Generate PostgreSQL configuration per environment

SF-TASK-007 — Add multi-client isolation tests

SF-TASK-008 — Generate client backup metadata
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 52. Tests et preuves attendus

## 52.1 Génération

Générer deux clients distincts depuis deux configurations.

---

## 52.2 Repositories

Vérifier que les projets générés ne contiennent aucune référence métier à l’autre client.

---

## 52.3 Compose

Vérifier :

- noms différents ;
- volumes différents ;
- réseaux privés distincts ;
- services distincts.

---

## 52.4 PostgreSQL

Vérifier que les credentials d’un client ne permettent pas d’accéder à la base de l’autre.

---

## 52.5 App_Data

Vérifier que chaque application monte uniquement son propre volume.

---

## 52.6 Backups

Vérifier que les manifests refusent une incohérence de client ou d’environnement.

---

## 52.7 Restore

Restaurer le client A sans modifier le client B.

---

## 52.8 Déploiement

Déployer ou redémarrer une stack de test sans interrompre l’autre.

---

## 52.9 Transfert

Vérifier qu’un repository généré peut être construit et déployé sans le repository source SiteFactory.

---

# 53. Golden test multi-client

Le projet doit disposer d’un scénario de référence :

```text
Client Alpha
+ Offre applicative illustrative
+ modules standards

Client Beta
+ Site Pro Administrable
+ configuration différente
```

Le test vérifie :

- absence de collision ;
- absence de chemin partagé ;
- absence de secret partagé ;
- absence de volume partagé ;
- composition indépendante ;
- builds indépendants.

---

# 54. Conditions de révision

Réexaminer cette décision si l’une des situations suivantes apparaît :

- l’exploitation par projet devient économiquement non soutenable ;
- les campagnes d’upgrade deviennent trop longues ou trop risquées ;
- le nombre de stacks rend l’inventaire difficile à maintenir ;
- un besoin réel de données partagées entre clients apparaît ;
- SiteFactory évolue vers un produit SaaS standardisé ;
- tous les clients doivent utiliser le même cycle de release ;
- un portail central nécessite des transactions métier cross-client ;
- le coût mémoire par stack devient dominant ;
- un besoin réglementaire impose une isolation physique plus forte ;
- des incidents répétés révèlent une mauvaise maîtrise de la mutualisation du VPS ;
- l’automatisation des opérations ne suffit plus à contenir la charge.

---

# 55. Indicateurs à suivre

- nombre de projets actifs ;
- nombre de stacks par VPS ;
- temps moyen de mise à niveau d’un client ;
- temps total d’une campagne de correctifs ;
- nombre de versions clientes supportées ;
- nombre d’échecs de déploiement ;
- incidents liés à une mauvaise cible opérateur ;
- consommation mémoire par stack ;
- stockage par client ;
- durée de restauration ;
- coût mensuel d’exploitation ;
- nombre de conflits d’upgrade ;
- nombre de clients transférés avec succès.

---

# 56. Signaux d’alerte

La décision doit être réexaminée si les observations montrent :

```text
des correctifs critiques non propagés dans les délais prévus

un inventaire de versions non fiable

des erreurs fréquentes de ciblage client

une consommation excessive de ressources

des scripts d’exploitation devenus trop complexes

des transferts clients toujours difficiles malgré l’isolation

un besoin croissant de logique métier réellement partagée
```

---

# 57. Alternatives futures possibles

Une révision pourrait envisager :

- automatisation centralisée des déploiements ;
- control plane interne sans données métier ;
- portail d’inventaire ;
- Kubernetes ou orchestrateur ;
- application partagée avec bases séparées ;
- modèle SaaS multi-tenant ;
- services mutualisés spécialisés ;
- infrastructure dédiée par client.

Ces évolutions ne sont pas autorisées implicitement par le présent ADR.

---

# 58. Conséquences observées

Cette section est append-only après mise en œuvre.

## 58.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.
La décision précède le premier incrément d’implémentation.
```

Les observations futures devront préciser :

- date ;
- projet concerné ;
- bénéfice ou problème ;
- métrique ;
- action proposée.

---

# 59. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0009-client-customizations-and-extension-points.md](0009-client-customizations-and-extension-points.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- [0027-database-roles-and-runtime-least-privilege.md](0027-database-roles-and-runtime-least-privilege.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](0021-administrator-provisioning-invitations-and-account-recovery.md)
- `13-generation-pipeline.md`
- `22-deployment-and-environments.md`
- `23-backup-and-restore.md`
- `25-upgrade-and-maintenance-strategy.md`
- `27-client-handover-and-service-closure.md`
- `0000-adr-template.md`

---

# 60. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 54 |

---

# 61. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 62. Synthèse de la décision

```text
Un client standard
→ un repository

Un client et un environnement
→ une application
→ une base PostgreSQL
→ des volumes
→ des secrets
→ des sauvegardes

Plusieurs clients
→ infrastructure physique éventuellement mutualisée
→ aucune donnée métier mutualisée
→ aucun runtime multi-tenant dans le MVP
```

> SiteFactory mutualise les capacités génériques au moment de la conception et de la génération. Il n’impose pas la mutualisation des données et du runtime des clients en production.
