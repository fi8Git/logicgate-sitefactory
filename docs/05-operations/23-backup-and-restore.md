# LogicGate SiteFactory — Sauvegarde et restauration

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Sauvegarde et restauration |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Profil de déploiement initial | `docker-compose-vps` |
| Base de données | PostgreSQL |
| Stockage persistant | Volumes Docker et `App_Data` |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md`, `../02-architecture/core-module-boundary.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `18-security-baseline.md`, `19-testing-strategy.md`, `21-observability-and-logging.md`, `22-deployment-and-environments.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Contrat cible, pas un outil livré

SiteFactory public ne fournit pas encore d'outil de sauvegarde/restauration
client, de rôle backup/restore provisionné ni de preuve de restauration production.
Aucune commande `sitefactory backup` ou `restore` n'est disponible.
Le [modèle runtime](../03-generation/generated-runtime-database-model.md) peut
représenter des états bloquants ; cela ne livre pas le workflow de réconciliation.

Les exigences suivantes restent le **contrat opérationnel cible** : validation
humaine, isolation, chiffrement, rétention, privilèges distincts, réconciliation
avant réouverture et interdiction de restauration implicite. Les commandes et
chemins d'exploitation sont des exemples cibles, pas des procédures vérifiées
dans la distribution publique. Les pratiques Business demeurent privées.

Le cleanup des bases éphémères du [harnais](../../tests/PostgreSql/README.md)
n'est ni un backup ni une restauration. CI distante et exploitation client
ne sont pas démontrées par les preuves locales existantes.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit la stratégie de sauvegarde et de restauration des applications clientes générées avec LogicGate SiteFactory.

Il précise notamment :

- les données à sauvegarder ;
- les données à exclure ;
- les objectifs de reprise ;
- les fréquences ;
- la rétention ;
- le stockage local et hors VPS ;
- la protection des sauvegardes ;
- les sauvegardes PostgreSQL ;
- les sauvegardes de `App_Data` ;
- les clés ASP.NET Core Data Protection ;
- la configuration de déploiement nécessaire à une reprise ;
- la cohérence entre base de données et fichiers ;
- les manifests et checksums ;
- la vérification des sauvegardes ;
- les procédures de restauration ;
- les scénarios de sinistre ;
- les répétitions de restauration ;
- les alertes ;
- les rapports ;
- les responsabilités ;
- les tests ;
- les contraintes applicables à Codex.

Une sauvegarde n’est considérée comme fiable que si :

```text
elle existe
+ elle est lisible
+ son intégrité est vérifiée
+ sa restauration a été testée
```

---

## 2. Vision

La sauvegarde doit permettre de reconstruire une application cliente après :

- une suppression accidentelle ;
- une migration défaillante ;
- une corruption de données ;
- une perte de volume ;
- une panne du VPS ;
- une compromission ;
- une erreur opérateur ;
- un déploiement incompatible ;
- une défaillance de stockage.

Le flux de référence est :

```text
Données de production
→ sauvegarde locale contrôlée
→ vérification
→ copie hors VPS
→ vérification de la copie
→ rétention
→ surveillance
→ restauration périodiquement testée
```

La stratégie ne doit pas reposer uniquement sur :

- la présence d’un volume Docker ;
- un snapshot du fournisseur ;
- un dump jamais testé ;
- une copie conservée sur le même disque ;
- une procédure connue d’une seule personne ;
- une commande exécutée manuellement sans rapport.

---

## 3. Principes fondamentaux

## 3.1 Une sauvegarde n’est pas une archive

Une sauvegarde sert à restaurer un système après un incident.

Une archive sert à conserver une information pendant une durée déterminée.

Les sauvegardes ne doivent pas devenir un moyen de conserver indéfiniment des données qui auraient dû être supprimées ou anonymisées.

---

## 3.2 Une copie sur le même VPS ne suffit pas

Une sauvegarde stockée uniquement sur le VPS protège contre certains incidents locaux, mais pas contre :

- perte du serveur ;
- perte du disque ;
- compromission complète ;
- suppression accidentelle du répertoire ;
- panne du fournisseur ;
- chiffrement malveillant ;
- erreur affectant l’ensemble du filesystem.

Une production exploitée durablement doit disposer d’au moins une copie hors VPS.

---

## 3.3 Les sauvegardes sont des données sensibles

Une sauvegarde peut contenir :

- comptes utilisateurs ;
- leads ;
- messages ;
- documents ;
- médias ;
- données personnelles ;
- historiques ;
- clés Data Protection ;
- informations de configuration.

Elle doit être protégée au moins au même niveau que la production.

---

## 3.4 Une sauvegarde réussie doit être vérifiée

Le succès d’une commande ne suffit pas.

Une sauvegarde doit faire l’objet de contrôles tels que :

- fichier présent ;
- taille cohérente ;
- checksum calculé ;
- archive lisible ;
- liste PostgreSQL consultable ;
- copie hors VPS confirmée ;
- manifest produit.

---

## 3.5 Une restauration est une opération destructive

Une restauration peut remplacer ou supprimer des données existantes.

Elle nécessite :

- un plan ;
- une cible explicite ;
- une confirmation ;
- une sauvegarde de l’état actuel lorsque possible ;
- une vérification post-restauration ;
- un rapport.

---

## 3.6 La version applicative doit correspondre aux données

Une sauvegarde doit permettre d’identifier :

- version de l’application ;
- commit ;
- version du générateur ;
- versions des modules ;
- version du template ;
- version du thème ;
- dernière migration appliquée.

La restauration doit commencer avec une version applicative compatible avec le schéma sauvegardé.

---

## 3.7 La restauration précède l’upgrade

Après restauration d’un environnement ancien :

```text
restaurer la version compatible
→ vérifier
→ stabiliser
→ planifier un upgrade séparé
```

Il ne faut pas restaurer des données anciennes puis appliquer immédiatement et silencieusement la dernière version disponible.

---

## 3.8 Aucune suppression implicite

La suppression des anciennes sauvegardes doit être :

- limitée au répertoire prévu ;
- conditionnée par une politique de rétention ;
- précédée par une vérification ;
- journalisée ;
- testée.

---

# 4. Non-objectifs du MVP

Le MVP ne cherche pas à fournir :

- réplication PostgreSQL temps réel ;
- haute disponibilité multi-région ;
- point-in-time recovery complet ;
- snapshots applicatifs distribués ;
- archivage légal longue durée ;
- restauration automatique sans opérateur ;
- bascule automatique vers un site secondaire ;
- stockage immuable certifié ;
- coffre HSM ;
- continuité de service sans interruption ;
- garantie contractuelle universelle de RPO ou RTO.

Ces capacités pourront être ajoutées pour des projets renforcés ou sur mesure.

---

# 5. Terminologie

## 5.1 Backup

Copie d’un composant ou d’un ensemble de données destinée à une restauration.

---

## 5.2 Full backup

Sauvegarde complète d’un périmètre.

Exemples :

```text
Dump PostgreSQL complet
Archive complète des médias persistants
```

---

## 5.3 Incremental backup

Sauvegarde ne contenant que les différences depuis une sauvegarde précédente.

Les sauvegardes incrémentales ne font pas partie du MVP initial.

---

## 5.4 Snapshot

Copie à un instant donné d’un volume ou d’une machine fournie par l’infrastructure.

Un snapshot ne remplace pas nécessairement une sauvegarde logique vérifiable.

---

## 5.5 Restore

Opération de reconstruction d’un composant depuis une sauvegarde.

---

## 5.6 Restore drill

Répétition contrôlée d’une restauration dans un environnement isolé.

---

## 5.7 Recovery Point Objective

Le RPO représente la quantité maximale de données que le projet accepte potentiellement de perdre, exprimée en durée.

Exemple :

```text
RPO de 24 heures
→ perte potentielle maximale d’environ une journée
```

---

## 5.8 Recovery Time Objective

Le RTO représente la durée cible nécessaire pour restaurer le service après décision de reprise.

---

## 5.9 Application-consistent backup

Sauvegarde réalisée dans un état cohérent entre :

- PostgreSQL ;
- fichiers persistants ;
- métadonnées ;
- version applicative.

---

## 5.10 Backup manifest

Fichier décrivant le contenu, les versions, les checksums et le résultat d’une sauvegarde.

---

# 6. Objectifs de reprise initiaux

## 6.1 Baseline non contractuelle

Pour un projet standard exploité par LogicGate, la cible interne proposée est :

```text
RPO cible : 24 heures

RTO cible : 8 heures
```

Ces valeurs constituent une baseline opérationnelle et non un engagement contractuel automatique.

---

## 6.2 Facteurs influençant le RPO

- fréquence des sauvegardes ;
- fréquence des formulaires ;
- volume de modifications ;
- intégrations ;
- documents ;
- capacité de réplication ;
- maintenance souscrite.

---

## 6.3 Facteurs influençant le RTO

- disponibilité d’un opérateur ;
- taille de la base ;
- taille des médias ;
- disponibilité du fournisseur ;
- vitesse de transfert ;
- disponibilité des images ;
- état du DNS ;
- complexité des migrations ;
- qualité de la documentation.

---

## 6.4 Projet renforcé

Un projet nécessitant :

```text
RPO inférieur à 24 heures

ou

RTO inférieur à 8 heures
```

doit faire l’objet d’un cadrage complémentaire.

Il peut nécessiter :

- sauvegardes plus fréquentes ;
- PostgreSQL managé ;
- snapshots ;
- réplication ;
- automatisation ;
- infrastructure secondaire ;
- supervision renforcée.

---

# 7. Périmètre de sauvegarde

Le périmètre complet peut comprendre :

```text
PostgreSQL
App_Data
Médias
Documents
Data Protection Keys
Configuration de déploiement
Informations de version
Manifest du projet
Configuration Caddy
Données Caddy nécessaires
Inventaire des secrets
Rapports opérateur
```

Les secrets eux-mêmes suivent une politique distincte.

---

# 8. PostgreSQL

La sauvegarde PostgreSQL doit contenir :

- tables applicatives ;
- données Identity ;
- migrations EF Core ;
- contenus ;
- leads ;
- historiques ;
- métadonnées des médias ;
- configurations administrables ;
- données des modules ;
- extensions client.

---

## 8.1 Format recommandé

Le format logique PostgreSQL recommandé est un format restaurable par `pg_restore`.

Exemple conceptuel :

```bash
pg_dump \
  --format=custom \
  --no-owner \
  --no-privileges
```

La commande exacte dépend de la configuration du conteneur et du runbook.

---

## 8.2 Version de l’outil

L’outil utilisé doit être compatible avec la version PostgreSQL sauvegardée.

La version doit être enregistrée dans le manifest.

---

## 8.3 Propriétaires et privilèges

Le MVP privilégie une sauvegarde portable sans propriétaires ni privilèges spécifiques au serveur d’origine.

Les comptes et permissions nécessaires sont recréés par la procédure d’infrastructure.

---

## 8.4 Historique des migrations

La table d’historique EF Core doit être incluse.

Elle permet d’identifier le schéma restauré.

---

# 9. `App_Data`

Le périmètre `App_Data` peut contenir :

```text
media
documents
exports persistants nécessaires
données propres aux modules
Data Protection Keys selon la structure retenue
```

---

## 9.1 Éléments inclus

Inclure les fichiers :

- persistants ;
- référencés en base ;
- nécessaires au fonctionnement ;
- nécessaires à la restauration.

---

## 9.2 Éléments exclus

Exclure lorsque le contrat le permet :

```text
cache
fichiers temporaires
exports expirés
logs
fichiers de build
fichiers recomposables
miniatures régénérables
```

Les exclusions doivent être explicites.

---

## 9.3 Permissions

Les permissions, utilisateurs et groupes nécessaires doivent être documentés.

Après restauration, les fichiers doivent rester accessibles au processus applicatif sans être globalement exposés.

---

# 10. ASP.NET Core Data Protection

## 10.1 Périmètre

Les clés Data Protection doivent être sauvegardées lorsque leur perte aurait un impact sur :

- cookies ;
- sessions ;
- tokens Identity ;
- données protégées ;
- workflows de réinitialisation.

---

## 10.2 Isolation

Les clés restent isolées par :

```text
client
+ environnement
```

Elles ne sont jamais partagées entre plusieurs productions clientes.

---

## 10.3 Protection

Les clés Data Protection sont des données sensibles.

Elles doivent être :

- exclues de Git ;
- protégées au repos ;
- protégées pendant le transfert ;
- accessibles uniquement aux opérateurs autorisés.

---

## 10.4 Restauration sans clés

Une restauration sans les anciennes clés peut rester possible, mais elle peut provoquer :

- invalidation des sessions ;
- invalidation des tokens ;
- impossibilité de déchiffrer certaines données protégées.

Cette conséquence doit être explicitement évaluée.

---

# 11. Configuration de déploiement

Le repository constitue la source principale pour :

- Dockerfile ;
- Docker Compose ;
- Caddyfile d’exemple ;
- scripts ;
- runbooks ;
- configuration non sensible ;
- migrations ;
- lock file.

Il n’est donc pas nécessaire de recopier tout le repository dans chaque backup de données.

---

## 11.1 Informations à conserver

Le manifest doit toutefois enregistrer :

```text
Repository
CommitSha
ApplicationVersion
ImageTag
ImageDigest éventuel
GeneratorVersion
CatalogVersion
TemplateVersion
ThemeVersion
ModuleVersions
Migration courante
```

---

## 11.2 Configuration serveur spécifique

Les fichiers durables non présents dans le repository doivent être sauvegardés ou reconstruisibles.

Exemples :

- Caddyfile réel ;
- configuration mutualisée ;
- mapping des domaines ;
- planification cron ;
- fichiers systemd éventuels ;
- configuration de rotation ;
- inventaire des volumes.

---

# 12. Caddy

## 12.1 Configuration

Le Caddyfile réel et les configurations associées doivent être sauvegardés lorsqu’ils ne sont pas entièrement reconstruisibles depuis le repository.

---

## 12.2 Données Caddy

Les volumes Caddy peuvent contenir :

- certificats ;
- clés privées ;
- états ACME ;
- configuration interne.

Ils doivent être protégés.

---

## 12.3 Reconstruction

Les certificats publics peuvent souvent être réémis.

Le runbook doit néanmoins prévoir :

- réinstallation de Caddy ;
- restauration ou reconstruction de la configuration ;
- revalidation DNS ;
- réémission des certificats ;
- vérification HTTPS.

---

# 13. Secrets

## 13.1 Exclusion du backup standard

Les fichiers tels que :

```text
.env.production
.env.staging
```

ne doivent pas être ajoutés en clair à l’archive standard de données.

---

## 13.2 Reprise des secrets

Une reprise complète nécessite toutefois de pouvoir retrouver ou recréer :

- mot de passe PostgreSQL ;
- credentials SMTP ;
- tokens d’intégration ;
- credentials registry ;
- secrets de providers ;
- informations AdminSeed temporaires éventuelles.

---

## 13.3 Stratégies possibles

### Secret manager

Stratégie cible lorsque disponible.

### Coffre opérateur chiffré

Inventaire chiffré conservé séparément des sauvegardes.

### Rotation et recréation

Certains secrets peuvent être recréés après un sinistre.

---

## 13.4 Séparation des clés

La clé permettant de déchiffrer un coffre ou une sauvegarde ne doit pas être stockée avec l’archive protégée.

---

## 13.5 Inventaire

Le projet doit conserver un inventaire sans valeur sensible :

```text
Nom du secret
Responsable
Système fournisseur
Procédure de renouvellement
Date de dernière rotation
Nécessaire à la restauration
```

---

# 14. Éléments exclus

Sont exclus par défaut :

```text
conteneurs
images reconstruisibles
binaires de build
packages NuGet en cache
logs temporaires
caches
fichiers de tests
données de développement
artefacts CI expirés
dossiers obj
dossiers bin
```

---

## 14.1 Image Docker

L’image n’est pas nécessairement incluse dans le backup si elle reste disponible dans un registry fiable.

La version et le digest doivent néanmoins être enregistrés.

---

## 14.2 Image indisponible

Si aucun registry durable n’est utilisé, la stratégie doit garantir que l’image ou le code exact peut être reconstruit.

---

# 15. Cohérence entre PostgreSQL et fichiers

## 15.1 Risque

Une sauvegarde PostgreSQL et une archive App_Data créées à des instants différents peuvent devenir incohérentes.

Exemple :

```text
Média référencé en base
mais fichier absent de l’archive
```

ou :

```text
Fichier présent
mais métadonnée absente de la base
```

---

## 15.2 Baseline standard

Pour un projet à faible volume :

1. réduire ou suspendre temporairement les écritures ;
2. sauvegarder PostgreSQL ;
3. sauvegarder App_Data ;
4. enregistrer les heures ;
5. réactiver les écritures ;
6. vérifier les deux composants.

---

## 15.3 Mode maintenance

Un court mode maintenance peut être utilisé pour obtenir une sauvegarde cohérente avant :

- migration ;
- import ;
- restauration ;
- changement de stockage ;
- opération destructive.

---

## 15.4 Sauvegarde quotidienne

Une sauvegarde quotidienne peut accepter un faible décalage entre les composants si :

- le risque est documenté ;
- les écritures de fichiers sont rares ;
- la procédure de réconciliation existe ;
- le projet ne gère pas de documents critiques.

---

## 15.5 Projet renforcé

Un projet nécessitant une cohérence stricte doit utiliser une stratégie spécifique :

- mise en pause des écritures ;
- snapshot cohérent ;
- stockage transactionnel ;
- journal d’opérations ;
- architecture dédiée.

---

# 16. Fréquence

## 16.1 Baseline de production

Proposition initiale :

```text
Sauvegarde complète quotidienne
```

L’horaire doit correspondre à une période de faible activité.

---

## 16.2 Avant opération sensible

Une sauvegarde supplémentaire est créée avant :

- migration ;
- import ;
- suppression massive ;
- changement de module avec données ;
- upgrade majeur ;
- restauration partielle ;
- opération manuelle risquée.

---

## 16.3 Staging

Les sauvegardes de staging sont adaptées à son utilité.

Elles ne doivent pas être confondues avec les sauvegardes de production.

---

## 16.4 Development

Les sauvegardes de développement ne sont pas obligatoires.

Les données doivent être recréables.

---

# 17. Rétention

## 17.1 Baseline simple du MVP

La politique initiale proposée est :

```text
Sauvegardes quotidiennes conservées pendant 30 jours
```

Cette valeur correspond à la configuration opérationnelle de référence.

---

## 17.2 Sauvegardes pré-migration

Une sauvegarde pré-migration doit être conservée au minimum jusqu’à :

- validation de la version ;
- fin de la période de stabilisation ;
- expiration définie par le runbook.

---

## 17.3 Politique renforcée future

Une politique future peut distinguer :

```text
quotidiennes
hebdomadaires
mensuelles
annuelles
```

Elle ne doit pas être introduite sans besoin de conservation clairement défini.

---

## 17.4 Rétention non illimitée

Les sauvegardes ne doivent pas être conservées indéfiniment sans :

- finalité ;
- budget ;
- protection ;
- politique ;
- responsabilité.

---

## 17.5 Échec de la nouvelle sauvegarde

Le mécanisme de purge ne doit pas supprimer la dernière sauvegarde valide uniquement parce que la nouvelle sauvegarde a échoué.

---

# 18. Planification

La sauvegarde peut être planifiée avec :

- cron ;
- systemd timer ;
- outil d’orchestration futur ;
- tâche opérateur documentée.

---

## 18.1 Identité de la tâche

Chaque tâche doit préciser :

```text
client
environnement
fréquence
script
répertoire
rétention
destination hors VPS
```

---

## 18.2 Concurrence

Deux sauvegardes du même client et environnement ne doivent pas s’exécuter simultanément.

Un verrou opérationnel doit être utilisé.

---

## 18.3 Timeout

Chaque étape doit posséder une durée maximale raisonnable.

---

# 19. Structure des répertoires

Structure locale conceptuelle :

```text
/opt/logicgate/backups/
└── {client-slug}/
    └── {environment}/
        ├── 2026/
        │   └── 07/
        │       ├── 20260727T020000Z/
        │       └── 20260728T020000Z/
        └── latest-successful
```

---

## 19.1 Dossier d’une sauvegarde

```text
20260727T020000Z/
├── postgresql.dump
├── app-data.tar.gz
├── data-protection.tar.gz
├── deployment-metadata.json
├── backup-manifest.json
└── checksums.sha256
```

La structure réelle peut regrouper certaines archives.

---

## 19.2 Permissions

Le répertoire doit être accessible uniquement aux opérateurs et processus autorisés.

Valeur conceptuelle :

```text
umask 077
```

---

# 20. Convention de nommage

Format recommandé :

```text
{client-slug}_{environment}_{component}_{timestamp-utc}_{backup-id}
```

Exemples :

```text
cabinet-alpha_production_postgresql_20260727T020000Z_8f4a.dump

cabinet-alpha_production_app-data_20260727T020000Z_8f4a.tar.gz
```

Les noms ne contiennent pas :

- nom de prospect ;
- email ;
- secret ;
- données métier ;
- chemin local sensible.

---

# 21. Backup manifest

## 21.1 Fichier

```text
backup-manifest.json
```

---

## 21.2 Structure conceptuelle

```json
{
  "schemaVersion": 1,
  "backupId": "8f4a412e-38fa-45c0-9457-c8eecfd886c5",
  "clientSlug": "cabinet-alpha",
  "environment": "production",
  "startedAtUtc": "2026-07-27T02:00:00Z",
  "completedAtUtc": "2026-07-27T02:03:42Z",
  "status": "success",
  "backupType": "full",
  "consistency": "application-consistent",
  "application": {
    "version": "1.4.0",
    "commitSha": "abc1234",
    "imageTag": "1.4.0",
    "imageDigest": "sha256:..."
  },
  "siteFactory": {
    "generatorVersion": "0.5.0",
    "catalogVersion": "1.0.0",
    "templateVersion": "1.0.0"
  },
  "database": {
    "engine": "postgresql",
    "serverMajorVersion": 18,
    "dumpToolVersion": "18.x",
    "lastMigration": "AddPortfolioModule",
    "file": "postgresql.dump",
    "sizeBytes": 4823412,
    "sha256": "..."
  },
  "appData": {
    "file": "app-data.tar.gz",
    "sizeBytes": 12783411,
    "sha256": "..."
  },
  "dataProtection": {
    "included": true,
    "file": "data-protection.tar.gz",
    "sha256": "..."
  },
  "offsiteCopy": {
    "required": true,
    "status": "success",
    "destinationId": "logicgate-backup-storage"
  },
  "warnings": [],
  "errors": []
}
```

---

## 21.3 Données interdites

Le manifest ne contient pas :

- mot de passe ;
- chaîne de connexion ;
- token ;
- nom de lead ;
- contenu de document ;
- adresse email ;
- valeur d’un secret ;
- endpoint avec credentials.

---

# 22. Checksums

## 22.1 Algorithme

Chaque fichier de sauvegarde doit disposer d’une empreinte telle que :

```text
SHA-256
```

---

## 22.2 Vérification locale

Le checksum est calculé après création du fichier.

---

## 22.3 Vérification hors VPS

La copie distante doit être vérifiée après transfert.

---

## 22.4 Limite

Un checksum confirme l’intégrité binaire.

Il ne confirme pas que le dump contient des données restaurables et cohérentes.

---

# 23. Écriture atomique

Une sauvegarde ne doit pas apparaître comme complète avant sa finalisation.

Approche recommandée :

```text
dossier temporaire
→ génération des fichiers
→ vérification
→ manifest success
→ renommage vers le dossier final
```

Un fichier incomplet doit conserver un statut clair :

```text
failed
incomplete
```

---

# 24. Sauvegarde PostgreSQL

## 24.1 Préconditions

```text
Conteneur PostgreSQL actif

Base connue

Credentials disponibles sans affichage

Espace disque suffisant

Version de pg_dump compatible

Répertoire cible protégé

Aucune autre sauvegarde active
```

---

## 24.2 Exécution

La commande doit :

- retourner un code non nul en cas d’échec ;
- ne pas afficher le mot de passe ;
- écrire dans un fichier temporaire ;
- mesurer la durée ;
- produire un fichier non vide ;
- permettre une inspection par `pg_restore`.

---

## 24.3 Vérifications minimales

```text
Fichier présent

Taille supérieure à zéro

Checksum calculé

pg_restore --list réussi

Historique de migration attendu présent lorsque vérifiable
```

---

## 24.4 Base importante

Une vérification plus avancée peut restaurer automatiquement le dump dans une base temporaire isolée.

---

# 25. Sauvegarde App_Data

## 25.1 Préconditions

```text
Volume monté

Dossier lisible

Liste d’exclusions validée

Espace disque suffisant

Écritures suspendues si cohérence stricte requise
```

---

## 25.2 Archive

L’archive doit préserver :

- arborescence ;
- fichiers ;
- métadonnées utiles ;
- permissions nécessaires lorsque le format le permet.

---

## 25.3 Vérifications

```text
Archive présente

Archive lisible

Nombre de fichiers cohérent

Taille cohérente

Checksum

Aucun chemin sortant

Aucun fichier exclu interdit
```

---

## 25.4 Protection contre les chemins dangereux

La restauration d’une archive doit vérifier qu’aucun chemin ne sort du dossier cible.

---

# 26. Copie hors VPS

## 26.1 Exigence

Une production gérée durablement par LogicGate doit disposer d’une copie hors VPS lorsque le niveau de service prévoit la responsabilité des sauvegardes.

---

## 26.2 Destination

Destinations possibles :

- stockage objet ;
- second serveur ;
- coffre de backup ;
- fournisseur spécialisé ;
- stockage chiffré contrôlé.

---

## 26.3 Credentials

Les credentials de backup doivent :

- être distincts des credentials applicatifs ;
- posséder des droits minimaux ;
- ne pas permettre de supprimer toutes les sauvegardes si cela peut être évité ;
- rester hors Git ;
- être renouvelables.

---

## 26.4 Transfert

Le transfert utilise un canal chiffré.

---

## 26.5 Vérification

Le système doit confirmer :

- objet distant présent ;
- taille attendue ;
- checksum attendu ;
- date ;
- destination.

---

# 27. Immutabilité

## 27.1 Objectif

Une sauvegarde hors VPS devrait être difficile à modifier ou supprimer depuis un compte applicatif compromis.

---

## 27.2 MVP

Le MVP peut commencer avec :

- credentials dédiés ;
- permissions limitées ;
- destination séparée ;
- historique de versions ;
- rétention contrôlée.

---

## 27.3 Évolution renforcée

Une capacité d’immutabilité ou d’object lock peut être étudiée pour les projets sensibles.

---

# 28. Chiffrement

## 28.1 En transit

Les transferts de sauvegarde utilisent un protocole sécurisé.

---

## 28.2 Au repos

Les sauvegardes hors VPS doivent être chiffrées au repos par :

- le fournisseur ;
- l’archive ;
- ou les deux.

---

## 28.3 Chiffrement d’archive

Un chiffrement applicatif de l’archive peut être utilisé si :

- l’outil est reconnu ;
- la clé est gérée séparément ;
- la restauration est documentée ;
- la rotation est possible ;
- les tests couvrent le déchiffrement.

---

## 28.4 Clé perdue

Une sauvegarde chiffrée dont la clé est perdue est inutilisable.

La récupération de la clé doit faire partie du restore drill.

---

# 29. Compression

La compression peut réduire :

- stockage ;
- durée de transfert ;
- coûts.

Elle ne doit pas empêcher :

- vérification ;
- restauration ;
- détection d’erreur.

La compression ne constitue pas un chiffrement.

---

# 30. Surveillance des sauvegardes

## 30.1 Événements obligatoires

```text
BackupStarted
PostgreSqlBackupCompleted
AppDataBackupCompleted
BackupVerificationCompleted
OffsiteCopyCompleted
BackupCompleted
BackupFailed
```

---

## 30.2 Indicateurs

```text
Date du dernier backup réussi

Âge du dernier backup

Durée

Taille

Résultat de copie hors VPS

Nombre d’échecs consécutifs

Espace disque utilisé

Dernier restore drill réussi
```

---

## 30.3 Seuils proposés

Pour une sauvegarde quotidienne :

```text
Warning :
aucune sauvegarde réussie depuis plus de 26 heures

Critical :
aucune sauvegarde réussie depuis plus de 48 heures
```

Ces valeurs doivent être adaptées à l’horaire et au RPO du projet.

---

## 30.4 Échec

Un échec de backup ne doit pas rester silencieux.

Il doit produire :

- code non nul ;
- événement `Error` ;
- alerte selon le service ;
- conservation des précédentes sauvegardes valides.

---

# 31. Rapport de sauvegarde

Un fichier structuré peut être produit :

```text
backup-report.json
```

Il peut reprendre les informations du manifest et ajouter :

- étapes ;
- durées ;
- statut de rétention ;
- statut de transfert ;
- warnings ;
- erreurs.

---

# 32. Purge et rétention

## 32.1 Ordre

La purge intervient uniquement après :

1. réussite de la sauvegarde courante ;
2. vérification locale ;
3. copie hors VPS réussie lorsque requise ;
4. validation de la politique.

---

## 32.2 Dry-run

Le script de purge doit pouvoir afficher :

```text
fichiers concernés
dates
tailles
nombre
```

sans les supprimer.

---

## 32.3 Chemins

La suppression doit vérifier que chaque cible appartient au répertoire de backup attendu.

---

## 32.4 Liens symboliques

Les liens symboliques dangereux doivent être refusés.

---

## 32.5 Fichier protégé

Une sauvegarde marquée :

```text
hold
incident
pre-migration
```

peut être exclue temporairement de la purge.

---

# 33. Données supprimées et sauvegardes

## 33.1 Délai d’expiration

Une donnée supprimée de la production peut rester présente dans les sauvegardes jusqu’à leur expiration.

Cette réalité doit être prise en compte dans :

- politique de confidentialité ;
- procédures de suppression ;
- rétention ;
- réponses aux demandes concernées.

---

## 33.2 Restauration ancienne

Une restauration peut réintroduire une donnée supprimée après la date du backup.

---

## 33.3 Réconciliation post-restauration

Après une restauration, il faut réappliquer les opérations réalisées depuis la sauvegarde lorsque nécessaire :

- suppressions ;
- anonymisations ;
- désactivations de comptes ;
- révocations ;
- changements de permissions ;
- rotations de secrets.

---

## 33.4 Registre externe

Pour les demandes de suppression importantes, un registre opérationnel séparé peut permettre d’identifier les actions à réappliquer après une restauration.

Il doit rester :

- protégé ;
- limité ;
- sans contenu personnel inutile ;
- distinct de la sauvegarde restaurée.

---

# 34. Types de restauration

## 34.1 Restauration PostgreSQL uniquement

Utilisée lorsque :

- base supprimée ;
- base corrompue ;
- migration défaillante ;
- données altérées.

---

## 34.2 Restauration App_Data uniquement

Utilisée lorsque :

- fichiers supprimés ;
- volume perdu ;
- médias corrompus ;
- permissions détruites.

---

## 34.3 Restauration Data Protection uniquement

Utilisée lorsque les clés ont été perdues mais que les autres données restent cohérentes.

---

## 34.4 Restauration complète

Comprend :

```text
infrastructure
+ application
+ PostgreSQL
+ App_Data
+ Data Protection
+ configuration
+ domaines
+ vérifications
```

---

## 34.5 Restauration d’un élément

La restauration d’un seul média, document ou enregistrement peut nécessiter une procédure spécifique.

Il ne faut pas restaurer toute la production uniquement pour récupérer un fichier sans évaluer les impacts.

---

# 35. Préconditions d’une restauration

Avant toute restauration :

```text
[ ] Incident qualifié
[ ] Cible identifiée
[ ] Backup identifié
[ ] Manifest valide
[ ] Checksums valides
[ ] Version applicative compatible disponible
[ ] Secrets nécessaires disponibles
[ ] Espace disque suffisant
[ ] Écritures arrêtées si nécessaire
[ ] Backup de l’état actuel réalisé lorsque possible
[ ] Plan validé
[ ] Rollback de la restauration défini
[ ] Opérateur identifié
```

---

# 36. Confirmation

Une restauration nécessite une confirmation explicite.

Exemple :

```text
Target:
cabinet-alpha / production

Current data will be replaced.

Backup:
20260727T020000Z

Type RESTORE to continue:
```

En mode non interactif futur, une option spécifique sera requise :

```text
--confirm-restore
```

Une option générique `--yes` ne suffit pas pour une production.

---

# 37. Environnement isolé

## 37.1 Règle par défaut

Une sauvegarde doit être restaurée dans un environnement isolé avant d’être utilisée pour remplacer la production, lorsque le temps et la gravité de l’incident le permettent.

---

## 37.2 Isolation

L’environnement de vérification utilise :

- base distincte ;
- réseau distinct ;
- volumes distincts ;
- domaine non public ;
- SMTP désactivé ou sandbox ;
- LLM externe désactivé ;
- aucun job destructif ;
- secrets propres au test.

---

## 37.3 Objectif

Vérifier que :

- le dump se restaure ;
- l’application démarre ;
- les migrations correspondent ;
- les données essentielles existent ;
- les médias sont accessibles ;
- les smoke tests passent.

---

# 38. Restauration PostgreSQL

## 38.1 Ordre

```text
1. Arrêter ou isoler les écritures
2. Sauvegarder l’état actuel si possible
3. Créer une base cible propre
4. Créer le rôle applicatif
5. Restaurer le dump
6. Vérifier pg_restore
7. Vérifier l’historique EF Core
8. Vérifier les tables et contraintes
9. Démarrer la version compatible
10. Exécuter les smoke tests
```

---

## 38.2 Base existante

La restauration dans une base existante doit éviter les mélanges.

Stratégies recommandées :

- nouvelle base ;
- suppression et recréation contrôlées ;
- restauration sur nouvel environnement puis bascule.

---

## 38.3 Droits

Après restauration, vérifier :

- propriétaire ;
- accès du compte applicatif ;
- permissions ;
- séquences ;
- connexions.

---

## 38.4 Migrations

Ne pas exécuter automatiquement les dernières migrations.

Commencer avec la version correspondant au backup.

---

# 39. Restauration App_Data

## 39.1 Ordre

```text
1. Arrêter les écritures
2. Sauvegarder l’état actuel
3. Vérifier l’archive
4. Préparer un dossier cible propre
5. Extraire avec protection des chemins
6. Vérifier le nombre et la taille des fichiers
7. Restaurer les permissions
8. Monter le volume
9. Vérifier les références applicatives
```

---

## 39.2 Écrasement

L’extraction directement sur un volume actif doit être évitée lorsque cela risque de mélanger l’ancien et le nouveau contenu.

---

## 39.3 Validation

Vérifier notamment :

- médias lisibles ;
- documents accessibles aux utilisateurs autorisés ;
- fichiers privés non publics ;
- absence de fichiers exécutables inattendus ;
- absence de chemins sortants.

---

# 40. Restauration Data Protection

## 40.1 Dossier

Les clés sont restaurées dans le volume attendu.

---

## 40.2 Permissions

Le processus applicatif doit pouvoir les lire et éventuellement y écrire.

Les autres utilisateurs ne doivent pas disposer d’un accès inutile.

---

## 40.3 Nom d’application

Le nom d’application Data Protection doit correspondre à l’environnement restauré.

---

## 40.4 Vérification

Tester :

- connexion ;
- cookies existants lorsque pertinents ;
- tokens ;
- démarrage ;
- écriture d’une nouvelle clé si nécessaire.

---

# 41. Restauration complète après perte du VPS

Flux de référence :

```text
1. Créer ou réinstaller le VPS
2. Appliquer les mises à jour
3. Créer l’utilisateur opérateur
4. Configurer SSH
5. Configurer le firewall
6. Installer Docker et Compose
7. Restaurer la configuration Caddy
8. Restaurer ou reconstruire les secrets
9. Récupérer l’image applicative exacte
10. Créer les réseaux et volumes
11. Démarrer PostgreSQL
12. Restaurer la base
13. Restaurer App_Data
14. Restaurer Data Protection
15. Démarrer l’application compatible
16. Vérifier le readiness
17. Configurer ou vérifier DNS
18. Vérifier HTTPS
19. Exécuter les smoke tests
20. Réappliquer les suppressions et révocations nécessaires
21. Réactiver le trafic
22. Surveiller
23. Produire le rapport
```

---

# 42. Restauration après compromission

## 42.1 Principe

Une restauration sur une infrastructure compromise sans correction de la cause est insuffisante.

---

## 42.2 Étapes supplémentaires

```text
Isoler l’ancien serveur

Préserver les éléments nécessaires à l’analyse

Créer une nouvelle infrastructure propre

Renouveler les secrets

Renouveler les clés SSH

Vérifier les images

Vérifier les packages

Vérifier le repository

Restaurer depuis une sauvegarde considérée saine

Analyser les comptes et permissions

Invalider les sessions

Surveiller après remise en service
```

---

## 42.3 Sauvegarde potentiellement compromise

Une sauvegarde créée après le début de la compromission peut contenir :

- données altérées ;
- fichiers malveillants ;
- comptes ajoutés ;
- configuration modifiée.

Sa date et son intégrité fonctionnelle doivent être analysées.

---

# 43. Restauration après migration défaillante

## 43.1 Cas compatible

Si l’ancienne application reste compatible avec la base :

```text
rollback applicatif
```

peut suffire.

---

## 43.2 Cas incompatible

Si le schéma ou les données ont été modifiés de manière incompatible :

```text
arrêt des écritures
→ restauration de la base
→ restauration de la version applicative correspondante
→ smoke tests
```

---

## 43.3 Données créées après la migration

La restauration peut supprimer les données créées après le backup.

Cette perte doit être évaluée et, si possible, les données doivent être exportées avant restauration.

---

# 44. Restauration après suppression accidentelle

Approches possibles :

- restauration complète dans un environnement isolé ;
- extraction de la donnée concernée ;
- réimport contrôlé dans la production ;
- restauration complète si l’intégrité globale est affectée.

Une restauration partielle doit préserver :

- identifiants ;
- relations ;
- historique ;
- autorisations ;
- règles métier.

---

# 45. Vérifications post-restauration

## 45.1 Infrastructure

```text
Conteneurs actifs
Volumes montés
PostgreSQL privé
Caddy valide
HTTPS actif
Espace disque suffisant
```

---

## 45.2 Application

```text
Version attendue
Liveness sain
Readiness sain
Logs sans erreur critique
Configuration Production
```

---

## 45.3 Identity

```text
Administrateurs présents
Rôles présents
Permissions cohérentes
Login
Logout
Sessions selon le scénario
Comptes révoqués réappliqués
```

---

## 45.4 Données

```text
Migrations cohérentes
Contenus présents
Leads présents
Médias présents
Documents présents
Historique cohérent
```

---

## 45.5 Fonctionnel

```text
Accueil
Administration
Formulaire
Persistance
SMTP contrôlé
SEO
Pages légales
```

---

## 45.6 Sécurité

```text
Secrets renouvelés si nécessaire
AdminSeed désactivé
PostgreSQL non public
Headers
Logs sans PII
Permissions
```

---

# 46. Smoke tests post-restauration

La baseline comprend :

```text
Accueil retourne 200

HTTPS fonctionne

Health live est sain

Health ready est sain

Admin refuse l’accès anonyme

Login administrateur fonctionne

Logout fonctionne

Lecture de données fonctionne

Écriture de test contrôlée fonctionne

Média restauré est lisible

Formulaire test fonctionne

PostgreSQL reste non public
```

---

# 47. Réconciliation après restauration

## 47.1 Opérations à réappliquer

Selon la date du backup :

- suppression ou anonymisation ;
- désactivation de compte ;
- retrait de rôle ;
- rotation de secret ;
- changement de domaine ;
- changement de configuration ;
- révocation de token ;
- ajout d’une redirection ;
- correction de contenu.

---

## 47.2 Sources de réconciliation

- tickets opérateur ;
- journal d’incident ;
- audit externe ;
- registre de demandes ;
- changelog ;
- rapports de déploiement ;
- inventaire de secrets.

---

## 47.3 Vérification

La restauration n’est pas terminée avant la fin de la réconciliation.

---

# 48. Restore drill

## 48.1 Fréquence proposée

Pour une production gérée par LogicGate :

```text
Restore drill complet au minimum tous les trimestres
```

Cette fréquence peut être augmentée pour un projet renforcé.

---

## 48.2 Déclencheurs supplémentaires

Un restore drill doit être envisagé après :

- modification du format de backup ;
- changement de version PostgreSQL majeure ;
- changement de stockage ;
- changement de chiffrement ;
- modification importante de `App_Data` ;
- migration structurante ;
- changement d’infrastructure ;
- changement de fournisseur de backup.

---

## 48.3 Périmètre minimal

```text
Téléchargement de la copie hors VPS

Vérification des checksums

Déchiffrement éventuel

Restauration PostgreSQL

Restauration App_Data

Restauration Data Protection

Démarrage de l’application compatible

Smoke tests

Rapport
```

---

## 48.4 Pas de production

Le restore drill ne doit pas écraser la production.

---

## 48.5 Intégrations

Dans l’environnement de drill :

```text
SMTP désactivé ou sandbox

LLM externe désactivé

Webhooks désactivés

Jobs destructifs désactivés

Indexation désactivée
```

---

# 49. Rapport de restore drill

Structure conceptuelle :

```json
{
  "schemaVersion": 1,
  "restoreDrillId": "992f9e9a-4e2a-40d1-b4bd-70fc85293c48",
  "backupId": "8f4a412e-38fa-45c0-9457-c8eecfd886c5",
  "clientSlug": "cabinet-alpha",
  "sourceEnvironment": "production",
  "targetEnvironment": "restore-drill",
  "startedAtUtc": "2026-07-30T08:00:00Z",
  "completedAtUtc": "2026-07-30T08:42:00Z",
  "postgresql": "success",
  "appData": "success",
  "dataProtection": "success",
  "applicationStartup": "success",
  "smokeTests": "success",
  "measuredRtoMinutes": 42,
  "status": "success",
  "warnings": []
}
```

---

## 49.1 Données interdites

Le rapport ne contient pas :

- données restaurées ;
- secret ;
- mot de passe ;
- contenu client ;
- chemins contenant des credentials.

---

# 50. Mesure du RPO et du RTO

## 50.1 RPO observé

Le RPO observé peut être calculé à partir de :

```text
heure de l’incident
- date de la dernière sauvegarde exploitable
```

---

## 50.2 RTO observé

Le RTO observé mesure :

```text
décision de restauration
→ service vérifié comme opérationnel
```

---

## 50.3 Utilisation

Ces mesures permettent de vérifier si la stratégie réelle répond aux objectifs internes.

---

## 50.4 Pas de garantie automatique

Un restore drill réussi en 42 minutes ne crée pas automatiquement un RTO contractuel de 42 minutes.

---

# 51. Tests automatisés

## 51.1 Scripts

Tester :

- arguments ;
- chemins ;
- verrouillage ;
- codes de sortie ;
- timeout ;
- échec PostgreSQL ;
- échec App_Data ;
- copie hors VPS ;
- checksum ;
- purge ;
- absence de secret.

---

## 51.2 PostgreSQL

Tester :

- dump ;
- liste ;
- restauration ;
- contraintes ;
- migrations ;
- comptes ;
- données synthétiques.

---

## 51.3 Archives

Tester :

- création ;
- lecture ;
- extraction ;
- path traversal ;
- fichier corrompu ;
- permissions ;
- exclusions.

---

## 51.4 Manifest

Tester :

- JSON valide ;
- propriétés obligatoires ;
- status ;
- checksums ;
- versions ;
- absence de données sensibles.

---

# 52. Tests de sécurité

Vérifier :

- fichiers avec permissions trop larges ;
- archive accessible publiquement ;
- secret dans le manifest ;
- mot de passe affiché ;
- clé de chiffrement stockée avec l’archive ;
- path traversal pendant extraction ;
- restauration vers la mauvaise cible ;
- suppression hors du dossier de backup ;
- lien symbolique ;
- archive modifiée ;
- backup dans Git.

---

# 53. Tests de défaillance

Simuler :

```text
espace disque insuffisant

pg_dump échoué

archive App_Data échouée

checksum échoué

copie distante échouée

réseau interrompu

fichier corrompu

clé de chiffrement absente

PostgreSQL incompatible

permissions invalides

timeout
```

La procédure doit produire un échec clair et conserver les sauvegardes valides précédentes.

---

# 54. Tests de concurrence

Vérifier que :

- deux backups simultanés sont empêchés ;
- deux restaurations simultanées sont empêchées ;
- un backup ne s’exécute pas pendant une restauration ;
- une migration et une restauration ne s’exécutent pas simultanément.

---

# 55. Scripts générés

Le projet client peut recevoir :

```text
deploy/scripts/backup-postgresql.sh
deploy/scripts/backup-app-data.sh
deploy/scripts/backup-all.sh
deploy/scripts/verify-backup.sh
deploy/scripts/copy-backup-offsite.sh
deploy/scripts/prune-backups.sh
deploy/scripts/backup-status.sh
deploy/scripts/restore-postgresql.sh
deploy/scripts/restore-app-data.sh
deploy/scripts/restore-all.sh
deploy/scripts/restore-verify.sh
```

Les scripts doivent rester simples et documentés.

---

# 56. Règles des scripts

Chaque script doit :

- utiliser un mode strict ;
- valider ses arguments ;
- normaliser les chemins ;
- utiliser un répertoire de travail explicite ;
- protéger les permissions ;
- transmettre les codes de sortie ;
- gérer l’annulation ;
- produire un rapport ;
- ne pas afficher de secret ;
- ne pas afficher de PII.

Exemple conceptuel pour Bash :

```bash
set -Eeuo pipefail
umask 077
```

---

# 57. Secrets dans les scripts

Les mots de passe PostgreSQL ne doivent pas être concaténés dans la commande affichée.

Mécanismes possibles :

- variable d’environnement protégée ;
- fichier `.pgpass` aux permissions restrictives ;
- secret Docker ;
- mécanisme opérateur approuvé.

---

# 58. Commandes SiteFactory futures

Groupe futur :

```text
sitefactory backup check
sitefactory backup plan
sitefactory backup run
sitefactory backup status
sitefactory backup verify

sitefactory restore plan
sitefactory restore apply
sitefactory restore verify
```

Ces commandes restent hors du premier MVP tant que les scripts et runbooks ne sont pas stabilisés.

---

## 58.1 `backup check`

Vérifierait :

- configuration ;
- espace disque ;
- outils ;
- destination ;
- credentials disponibles ;
- dernière sauvegarde ;
- verrouillage.

---

## 58.2 `backup plan`

Afficherait :

- composants ;
- destination ;
- cohérence ;
- rétention ;
- copie hors VPS ;
- estimations ;
- actions.

---

## 58.3 `backup run`

Nécessiterait :

```text
--environment
--execute
```

La sauvegarde n’est pas une opération destructive, mais son environnement doit rester explicite.

---

## 58.4 `restore plan`

Afficherait :

- source ;
- cible ;
- versions ;
- composants ;
- impacts ;
- pertes potentielles ;
- opérations de réconciliation ;
- confirmation requise.

---

## 58.5 `restore apply`

Nécessiterait :

```text
--environment
--backup-id
--execute
--confirm-restore
```

---

# 59. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-BACKUP-001` | Configuration de backup invalide |
| `SF-BACKUP-002` | Répertoire inaccessible |
| `SF-BACKUP-003` | Espace disque insuffisant |
| `SF-BACKUP-004` | Backup déjà en cours |
| `SF-BACKUP-005` | Dump PostgreSQL échoué |
| `SF-BACKUP-006` | Archive App_Data échouée |
| `SF-BACKUP-007` | Backup Data Protection échoué |
| `SF-BACKUP-008` | Checksum échoué |
| `SF-BACKUP-009` | Manifest invalide |
| `SF-BACKUP-010` | Copie hors VPS échouée |
| `SF-BACKUP-011` | Purge échouée |
| `SF-BACKUP-012` | Dernier backup trop ancien |
| `SF-BACKUP-013` | Archive incomplète |
| `SF-BACKUP-014` | Clé de chiffrement indisponible |
| `SF-BACKUP-015` | Destination non autorisée |
| `SF-RESTORE-001` | Backup inconnu |
| `SF-RESTORE-002` | Checksum invalide |
| `SF-RESTORE-003` | Version incompatible |
| `SF-RESTORE-004` | Cible invalide |
| `SF-RESTORE-005` | Confirmation manquante |
| `SF-RESTORE-006` | Restauration PostgreSQL échouée |
| `SF-RESTORE-007` | Restauration App_Data échouée |
| `SF-RESTORE-008` | Restauration Data Protection échouée |
| `SF-RESTORE-009` | Smoke tests échoués |
| `SF-RESTORE-010` | Réconciliation requise |
| `SF-RESTORE-011` | Restauration concurrente |
| `SF-RESTORE-012` | Chemin d’archive dangereux |
| `SF-RESTORE-013` | Rollback de restauration échoué |

---

# 60. Logs de sauvegarde

Propriétés autorisées :

```text
BackupId
ClientSlug
Environment
Component
Duration
SizeBytes
FileCount
Result
ErrorCode
OffsiteStatus
```

---

## 60.1 Données interdites

```text
Contenu SQL
Données personnelles
Nom de document sensible
Mot de passe
Token
Destination avec credential
Clé de chiffrement
```

---

# 61. Logs de restauration

Propriétés autorisées :

```text
RestoreId
BackupId
TargetEnvironment
Component
Version
Duration
Result
ErrorCode
```

La restauration d’une production doit également produire un audit opérateur.

---

# 62. Audit opérateur

Les opérations suivantes doivent être auditées :

```text
Restauration lancée

Restauration annulée

Restauration terminée

Backup placé en hold

Backup supprimé manuellement

Rétention modifiée

Destination hors VPS modifiée

Restore drill exécuté
```

---

# 63. Alertes

Alertes recommandées :

```text
Backup quotidien absent

Deux backups consécutifs échoués

Copie hors VPS échouée

Espace disque critique

Checksum invalide

Restore drill en retard

Dernier restore drill échoué

Rétention non appliquée

Volume de backup inhabituel
```

---

## 63.1 Variation de taille

Une forte baisse ou hausse de taille peut signaler :

- dump incomplet ;
- perte de données ;
- croissance anormale ;
- inclusion accidentelle ;
- problème d’archive.

Elle doit produire un warning à analyser.

---

# 64. Quality gates

Un backup ne peut pas être déclaré réussi si :

```text
Dump PostgreSQL absent

Archive requise absente

Checksum absent ou invalide

Manifest absent

Copie hors VPS échouée lorsqu’elle est obligatoire

Fichier incomplet

Code de sortie non nul
```

---

## 64.1 Restore drill

Un restore drill ne peut pas être déclaré réussi si :

```text
La base ne se restaure pas

L’application ne démarre pas

Le readiness échoue

Les médias sont absents

Les permissions sont invalides

Les smoke tests échouent

Le rapport n’est pas produit
```

---

# 65. Protection de la production

Les scripts de test ou de CI ne doivent jamais :

- restaurer une production ;
- supprimer un volume de production ;
- utiliser un secret de production ;
- lancer une purge réelle ;
- utiliser la base cliente réelle.

Les tests utilisent :

- environnement temporaire ;
- base fictive ;
- volumes fictifs ;
- données synthétiques.

---

# 66. Responsabilités LogicGate

Lorsque LogicGate assure l’exploitation, LogicGate est responsable de :

- configurer les sauvegardes ;
- vérifier leur exécution ;
- surveiller les alertes ;
- protéger les credentials ;
- conserver une copie hors VPS ;
- appliquer la rétention ;
- exécuter les restore drills ;
- documenter les incidents ;
- maintenir les scripts ;
- vérifier les backups avant migration.

---

# 67. Responsabilités du client

Selon le contrat, le client est notamment responsable de :

- signaler les données critiques ;
- valider les durées de conservation ;
- informer LogicGate d’une demande de suppression ;
- signaler un incident ;
- valider les responsabilités après transfert ;
- protéger ses comptes administrateurs.

---

# 68. Transfert de responsabilité

Lorsque LogicGate transfère l’exploitation au client, le transfert doit inclure :

- dernière sauvegarde ;
- méthode de backup ;
- méthode de restauration ;
- politique de rétention ;
- destination hors serveur ;
- accès nécessaires ;
- inventaire des secrets ;
- restore drill le plus récent ;
- objectifs de reprise ;
- responsabilités ;
- risques connus.

---

# 69. Fin de service

À la fin du service :

1. produire une sauvegarde finale ;
2. vérifier son intégrité ;
3. remettre ou transférer les archives prévues ;
4. remettre les procédures ;
5. retirer les accès LogicGate selon le contrat ;
6. supprimer les copies LogicGate après la période prévue ;
7. documenter la fin de responsabilité.

---

# 70. Fermeture d’un projet

La fermeture doit vérifier :

```text
Données à remettre

Données à conserver

Données à supprimer

Backups hors VPS

Secrets

DNS

Caddy

Volumes

Repository

Responsabilités légales et contractuelles
```

---

# 71. Documentation par projet

Chaque projet client doit documenter :

```text
Composants sauvegardés
Fréquence
Horaire
RPO cible
RTO cible
Rétention
Répertoire local
Destination hors VPS
Chiffrement
Credentials
Scripts
Alertes
Dernier restore drill
Responsables
```

---

# 72. Fiche de backup d’un client

Exemple conceptuel :

```yaml
backup:
  clientSlug: cabinet-alpha
  environment: production

  schedule:
    frequency: daily
    timeLocal: "02:00"
    timeZone: Europe/Paris

  objectives:
    rpoHours: 24
    rtoHours: 8

  components:
    postgresql: true
    appData: true
    dataProtectionKeys: true

  retention:
    dailyDays: 30

  offsite:
    required: true
    destinationId: logicgate-backup-storage

  restoreDrill:
    frequency: quarterly
```

Ce fichier ne contient aucun secret.

---

# 73. Critères d’acceptation du MVP

La stratégie sera considérée comme opérationnelle lorsque :

1. le périmètre PostgreSQL est défini ;
2. le périmètre App_Data est défini ;
3. les clés Data Protection sont incluses lorsque nécessaire ;
4. les secrets suivent une stratégie distincte ;
5. les versions applicatives sont enregistrées ;
6. un backup PostgreSQL peut être créé ;
7. un backup App_Data peut être créé ;
8. les fichiers possèdent des checksums ;
9. un manifest est produit ;
10. les sauvegardes utilisent des timestamps UTC ;
11. les répertoires possèdent des permissions restrictives ;
12. une fréquence quotidienne est configurable ;
13. une rétention de 30 jours est supportée ;
14. une sauvegarde pré-migration est possible ;
15. une copie hors VPS est supportée ;
16. la copie hors VPS est vérifiée ;
17. les sauvegardes peuvent être chiffrées ;
18. la clé reste séparée de l’archive ;
19. les échecs produisent un code non nul ;
20. l’âge du dernier backup est surveillé ;
21. la purge respecte un dry-run ;
22. la purge ne sort pas du dossier autorisé ;
23. la restauration PostgreSQL est documentée ;
24. la restauration App_Data est documentée ;
25. la restauration Data Protection est documentée ;
26. la restauration complète est documentée ;
27. une restauration nécessite une confirmation explicite ;
28. une restauration est testable en environnement isolé ;
29. les smoke tests post-restauration existent ;
30. la réconciliation des suppressions est prévue ;
31. un restore drill trimestriel est possible ;
32. le RPO et le RTO observés peuvent être mesurés ;
33. les rapports ne contiennent aucun secret ;
34. les scripts sont testés ;
35. les quality gates bloquent une sauvegarde incomplète ;
36. la CI ne touche jamais à la production.

---

# 74. Décisions validées

La baseline de sauvegarde et restauration est la suivante :

```text
Chaque production possède une stratégie de backup documentée.

La baseline interne non contractuelle vise un RPO de 24 heures
et un RTO de 8 heures.

Une sauvegarde complète est réalisée quotidiennement.

La rétention initiale est de 30 jours.

Une sauvegarde supplémentaire est créée avant
toute migration ou opération sensible.

PostgreSQL est sauvegardé dans un format logique restaurable.

App_Data est sauvegardé séparément.

Les clés Data Protection sont sauvegardées
lorsqu’elles sont nécessaires à la reprise.

Les secrets ne sont pas inclus en clair
dans la sauvegarde standard.

Un inventaire et une procédure de récupération
des secrets sont obligatoires.

Une production gérée durablement possède une copie hors VPS.

La copie hors VPS utilise un transfert sécurisé.

Chaque fichier possède un checksum SHA-256.

Chaque sauvegarde possède un manifest.

Une sauvegarde n’est pas déclarée réussie
avant vérification des composants requis.

La purge n’est exécutée qu’après une nouvelle sauvegarde réussie.

La dernière sauvegarde valide n’est pas supprimée
à la suite d’un nouvel échec.

Les sauvegardes sont protégées comme des données de production.

Une restauration nécessite un plan et une confirmation explicite.

La restauration commence avec une version applicative
compatible avec le backup.

L’upgrade intervient après la restauration et la stabilisation.

Une restauration complète inclut PostgreSQL,
App_Data, Data Protection, configuration et vérifications.

Les suppressions, anonymisations et révocations
postérieures au backup sont réappliquées après restauration.

Un restore drill complet est réalisé au minimum chaque trimestre
pour les productions exploitées par LogicGate.

Un backup jamais restauré n’est pas considéré comme fiable.

Les tests et la CI n’utilisent jamais une production réelle.

Codex ne peut pas supprimer seul une sauvegarde,
réduire la rétention ou lancer une restauration de production.
```

---

# 75. Règles pour Codex

## 75.1 Lecture préalable

Avant toute modification liée aux sauvegardes ou restaurations, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
../02-architecture/core-module-boundary.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
18-security-baseline.md
19-testing-strategy.md
21-observability-and-logging.md
22-deployment-and-environments.md
23-backup-and-restore.md
AGENTS.md
```

Ainsi que :

- Docker Compose ;
- volumes ;
- scripts existants ;
- configuration PostgreSQL ;
- configuration Data Protection ;
- runbooks ;
- tests ;
- politique de rétention du projet concerné.

---

## 75.2 Ce que Codex peut faire

Codex peut :

- créer un script de backup prévu ;
- créer un script de vérification ;
- créer un manifest ;
- ajouter un checksum ;
- ajouter un verrou ;
- ajouter un dry-run de purge ;
- ajouter un script de restauration ;
- ajouter des tests ;
- améliorer un diagnostic ;
- ajouter un rapport ;
- documenter un restore drill ;
- corriger une fuite de secret ;
- améliorer la validation des chemins.

---

## 75.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- lancer une restauration de production ;
- supprimer une sauvegarde réelle ;
- réduire la rétention ;
- afficher un mot de passe ;
- inclure `.env.production` en clair ;
- stocker une clé de chiffrement avec l’archive ;
- modifier un backup existant ;
- considérer un dump non testé comme valide ;
- supprimer la dernière sauvegarde réussie ;
- exécuter une purge sans dry-run ;
- utiliser `rm -rf` sur un chemin non validé ;
- restaurer directement par-dessus une production sans plan ;
- appliquer les dernières migrations automatiquement après restauration ;
- utiliser une donnée de production dans les tests ;
- ignorer un checksum invalide ;
- déclarer un restore drill réussi sans démarrer l’application ;
- partager les backups entre clients sans isolation.

---

## 75.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouvelle destination de backup ;
- nouvelle méthode de chiffrement ;
- modification de rétention ;
- réduction de fréquence ;
- changement du RPO ou RTO ;
- sauvegarde de nouvelles données sensibles ;
- restauration de production ;
- suppression manuelle d’une sauvegarde ;
- changement de format PostgreSQL ;
- changement de version majeure PostgreSQL ;
- restauration partielle de données métier ;
- procédure de purge ;
- changement de la politique hors VPS ;
- transfert au client ;
- arrêt du service de backup.

---

## 75.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Client ou projet concerné
Environnement concerné
Composants sauvegardés
Scripts modifiés
Fréquence affectée
Rétention affectée
Destination affectée
Chiffrement affecté
Manifest affecté
Checksums affectés
Secrets requis
Procédure de restauration affectée
Tests ajoutés
Tests de défaillance
Tests de sécurité
Build
Résultats
Risques
Décisions humaines restantes
```

Codex ne doit pas déclarer une procédure restaurable sans avoir validé au minimum la création, la lecture et l’extraction des artefacts concernés.

---

# 76. Anti-patterns interdits

## 76.1 Backup uniquement sur le même disque

Aucune copie hors VPS n’existe pour une production exploitée durablement.

---

## 76.2 Dump non vérifié

La commande retourne `0`, mais le fichier n’est jamais inspecté.

---

## 76.3 Backup sans version

Il est impossible d’identifier l’application compatible.

---

## 76.4 Secret dans l’archive

```text
.env.production
```

est copié en clair dans chaque backup.

---

## 76.5 Clé avec archive

La clé de chiffrement est stockée dans le même répertoire distant que les sauvegardes.

---

## 76.6 Purge avant vérification

Les anciennes sauvegardes sont supprimées avant la réussite de la nouvelle.

---

## 76.7 Rétention illimitée

Toutes les sauvegardes contenant des données personnelles sont conservées sans date de fin.

---

## 76.8 `rm -rf` non contrôlé

Un script construit dynamiquement un chemin puis le supprime sans validation.

---

## 76.9 Restauration directe en production

Une archive non testée remplace immédiatement la base active.

---

## 76.10 Upgrade pendant la restauration

La restauration applique automatiquement toutes les migrations disponibles.

---

## 76.11 Restore drill fictif

Le rapport indique un succès après simple vérification de présence des fichiers.

---

## 76.12 Suppressions non réappliquées

Une ancienne sauvegarde réintroduit des données précédemment supprimées sans réconciliation.

---

## 76.13 Backup dans Git

Un dump ou une archive est committé dans le repository.

---

## 76.14 Backup depuis une base non identifiée

La commande ne vérifie pas le client ou l’environnement cible.

---

## 76.15 Volumes supprimés pour tester la restauration

Une production utilise :

```bash
docker compose down --volumes
```

sans procédure explicite.

---

# 77. Roadmap d’implémentation

## Phase backup 1 — PostgreSQL

```text
script pg_dump
répertoire
permissions
manifest minimal
checksum
rapport
```

---

## Phase backup 2 — App_Data

```text
inclusions
exclusions
archive
permissions
checksum
```

---

## Phase backup 3 — Data Protection

```text
volume
archive
protection
restauration
```

---

## Phase backup 4 — Vérification

```text
pg_restore --list
lecture archive
taille
checksum
status
```

---

## Phase backup 5 — Rétention

```text
30 jours
dry-run
purge sécurisée
hold pré-migration
```

---

## Phase backup 6 — Hors VPS

```text
destination
credentials limités
transfert
checksum distant
rapport
```

---

## Phase backup 7 — Restauration

```text
PostgreSQL
App_Data
Data Protection
application compatible
smoke tests
```

---

## Phase backup 8 — Restore drill

```text
environnement isolé
automatisation
rapport
RPO
RTO
```

---

## Phase backup 9 — Supervision

```text
âge du dernier backup
échecs
taille
espace disque
alertes
```

---

## Phase backup 10 — Renforcement

```text
chiffrement applicatif
immutabilité
snapshots
PITR
réplication
```

Cette phase reste hors du MVP initial.

---

# 78. Checklist quotidienne

```text
[ ] Dernière sauvegarde réussie
[ ] PostgreSQL présent
[ ] App_Data présent
[ ] Data Protection présente si requise
[ ] Checksums valides
[ ] Manifest valide
[ ] Copie hors VPS réussie
[ ] Espace disque suffisant
[ ] Aucun échec non traité
```

---

# 79. Checklist avant migration

```text
[ ] Version actuelle connue
[ ] Version cible connue
[ ] Migration inspectée
[ ] Sauvegarde PostgreSQL créée
[ ] Sauvegarde App_Data créée si nécessaire
[ ] Data Protection sauvegardée
[ ] Checksums valides
[ ] Copie hors VPS validée
[ ] Backup marqué pre-migration
[ ] Restauration documentée
[ ] Rollback préparé
```

---

# 80. Checklist d’une restauration

```text
[ ] Incident qualifié
[ ] Backup sélectionné
[ ] Manifest lu
[ ] Checksums valides
[ ] Version compatible disponible
[ ] Secrets disponibles
[ ] Cible vérifiée
[ ] Backup de l’état actuel réalisé
[ ] Écritures arrêtées
[ ] Confirmation RESTORE obtenue
[ ] PostgreSQL restauré
[ ] App_Data restauré
[ ] Data Protection restaurée
[ ] Permissions restaurées
[ ] Application démarrée
[ ] Readiness sain
[ ] Smoke tests réussis
[ ] Suppressions et révocations réappliquées
[ ] Rapport produit
```

---

# 81. Checklist du restore drill

```text
[ ] Copie hors VPS utilisée
[ ] Checksum vérifié
[ ] Déchiffrement vérifié
[ ] Environnement isolé
[ ] SMTP désactivé
[ ] LLM externe désactivé
[ ] PostgreSQL restauré
[ ] App_Data restauré
[ ] Data Protection restaurée
[ ] Version compatible déployée
[ ] Health checks réussis
[ ] Smoke tests réussis
[ ] RPO mesuré
[ ] RTO mesuré
[ ] Rapport archivé
[ ] Corrections planifiées
```

---

# 82. Checklist de transfert au client

```text
[ ] Dernier backup fourni
[ ] Checksum fourni
[ ] Format documenté
[ ] Version applicative documentée
[ ] Procédure de restauration fournie
[ ] Scripts fournis
[ ] Politique de rétention transférée
[ ] Destination hors VPS transférée
[ ] Secrets transférés par canal séparé
[ ] Restore drill communiqué
[ ] Responsabilités signées ou documentées
[ ] Accès LogicGate retirés selon le contrat
```

---

# 83. Conclusion

La stratégie de sauvegarde et de restauration SiteFactory doit garantir que les données d’un client peuvent être récupérées après un incident réel.

Elle repose sur :

```text
périmètre connu
+ fréquence définie
+ rétention limitée
+ checksums
+ manifest
+ copie hors VPS
+ protection des archives
+ restauration documentée
+ restore drills
+ réconciliation
```

Une sauvegarde ne doit pas être évaluée uniquement sur sa création.

Elle doit être évaluée sur la capacité réelle à reconstruire :

- la base ;
- les fichiers ;
- les clés ;
- la version applicative ;
- les accès ;
- le service complet.

> Une sauvegarde non vérifiée est une hypothèse. Une sauvegarde restaurée avec succès est une preuve.
