# LogicGate SiteFactory — Transfert client et clôture de service

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Transfert client et clôture de service |
| Version | 1.0 |
| Statut | Contrat cible ; capacités publiques livrées distinguées |
| Propriétaire | LogicGate |
| Public principal | LogicGate, clients, développeurs, opérateurs, contributeurs, Codex |
| Projets concernés | Applications clientes générées, hébergement, maintenance et exploitation |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `Conditions explicites du transfert : perimetre, droits, acces, responsabilites, services ouverts ou clos et couts eventuels convenus`, `../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md`, `../adr/0009-client-customizations-and-extension-points.md`, `../02-architecture/core-module-boundary.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `18-security-baseline.md`, `19-testing-strategy.md`, `20-quality-gates-and-ci.md`, `21-observability-and-logging.md`, `22-deployment-and-environments.md`, `23-backup-and-restore.md`, `24-operational-runbooks.md`, `25-upgrade-and-maintenance-strategy.md`, `26-release-and-versioning.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Portée publique et contrat futur de transfert

Ce document conserve les **exigences cibles de transfert et de réversibilité**,
pas un service d'exploitation client déjà livré par le produit public.
Le [README](../../README.md) décrit le moteur et DocumentationModule ainsi que
les bibliothèques runtime/PostgreSQL réutilisables. La CLI actuelle ne possède
ni handover, closure, déploiement, backup/restore ou suppression d'environnement.

Le code original public relève de LICENSE/NOTICE ; cela ne transfère ni ne
relicencie automatiquement les modules Business, sorties clientes ou contenus
tiers. [ADR-0034](../adr/0034-apache-licence-choice-for-publish-site-factory.md)
laisse leurs régimes de droits distincts. Aucun package SiteFactory n'est encore
publié et aucun feed privé n'est requis pour utiliser le public.

Les sections suivantes sont des procédures recommandées et contrats normatifs
à appliquer à une **future application cliente/opération autorisée** :
inventaire, versions exactes, propriétaires, compatibilité, licences,
transfert séparé des secrets, validation des données, backup/restore vérifié,
réconciliation, révocation des accès et preuve de clôture.
Les opérations exclusivement Business restent dans le privé ; aucune ressource
privée obligatoire n'est introduite ici.

Les exemples `sample-*`, états et rapports de transfert sont fictifs,
pas des fichiers déjà générés ou des preuves d'une clôture effectuée.
Un transfert ne supprime jamais une protection, ne déclenche aucune mutation
de production implicite et ne remplace pas une décision humaine.
Le modèle de support ne fixe aucun prix, SLA ni engagement commercial public.
CI distante et déploiement client ne sont pas déclarés validés.

---

## 1. Objet du document

Ce document définit les règles de transfert d’un projet SiteFactory au client et les procédures de clôture d’un service LogicGate.

Il précise notamment :

- les différents types de transfert ;
- les préconditions de livraison ;
- les responsabilités après livraison ;
- les éléments techniques à remettre ;
- le transfert du repository ;
- le transfert des domaines et DNS ;
- le transfert de l’hébergement ;
- le transfert des images et packages ;
- le transfert des secrets ;
- le transfert des comptes administrateurs ;
- le transfert des sauvegardes ;
- la remise des procédures de restauration ;
- la remise des runbooks ;
- la formation du client ;
- la recette de transfert ;
- la période d’accompagnement ;
- la clôture du service mensuel ;
- la suppression des accès LogicGate ;
- la conservation et la suppression des données ;
- la fermeture d’un environnement ;
- la désaffectation de l’infrastructure ;
- la preuve de transfert ;
- les incidents découverts pendant le transfert ;
- les contraintes applicables à Codex.

Le transfert ne doit pas être réduit à :

```text
envoyer une archive ZIP au client
```

Il doit permettre au nouveau responsable de répondre aux questions suivantes :

```text
Quel code constitue le projet ?

Quelle version est en production ?

Où sont les données ?

Qui gère le domaine ?

Qui gère les secrets ?

Comment sauvegarder ?

Comment restaurer ?

Comment déployer ?

Comment appliquer une migration ?

Comment corriger une vulnérabilité ?

Qui est désormais responsable de chaque opération ?
```

---

## 2. Vision

Un projet SiteFactory doit pouvoir être transféré sans dépendance cachée à LogicGate.

Le transfert doit rendre explicites :

```text
les actifs
+ les accès
+ les responsabilités
+ les procédures
+ les risques
+ les limites
+ les échéances
```

Le flux de référence est :

```text
Décision de transfert ou clôture
→ inventaire
→ qualification du périmètre
→ préparation des actifs
→ validation technique
→ transfert sécurisé des accès
→ formation
→ recette de reprise
→ retrait progressif de LogicGate
→ confirmation écrite
→ clôture
```

La clôture ne doit pas provoquer automatiquement :

- l’arrêt du site ;
- la suppression des données ;
- la suppression des sauvegardes ;
- l’expiration du domaine ;
- la perte du HTTPS ;
- la désactivation des protections ;
- la perte du repository ;
- l’absence de responsable opérationnel.

---

## 3. Principes fondamentaux

## 3.1 Les responsabilités doivent toujours avoir un propriétaire

À la fin du transfert, chaque responsabilité doit être attribuée.

Exemples :

```text
Repository
Hébergement
DNS
Certificats
PostgreSQL
Backups
Restaurations
Déploiements
Migrations
Mises à jour
Comptes administrateurs
Sécurité
Monitoring
Support utilisateur
```

Une responsabilité ne doit pas rester :

```text
à déterminer
```

après la date effective de transfert.

---

## 3.2 Le transfert des secrets est séparé des documents

Les secrets ne sont jamais transmis :

- dans le repository ;
- dans un ticket public ;
- dans un email non protégé ;
- dans le document de transfert ;
- dans une archive contenant le code ;
- dans un fichier Markdown versionné.

Le canal de transfert des secrets doit être distinct et sécurisé.

---

## 3.3 Le client reçoit une version identifiable

Le transfert doit préciser :

```text
ApplicationVersion
CommitSha
GitTag
ImageTag
ImageDigest éventuel
GeneratorVersion
CatalogVersion
TemplateVersion
ThemeVersion
ModuleVersions
LastMigration
```

Le client ne doit pas recevoir un projet dont l’état exact est inconnu.

---

## 3.4 Les données sont vérifiées avant remise

Une archive ou un backup ne doit pas être remis sans :

- manifest ;
- checksum ;
- date ;
- version ;
- procédure de restauration ;
- vérification d’intégrité.

---

## 3.5 La remise du code ne transfère pas automatiquement l’exploitation

Posséder le repository ne signifie pas automatiquement être capable de :

- déployer ;
- restaurer ;
- renouveler un domaine ;
- gérer PostgreSQL ;
- répondre à un incident ;
- appliquer un correctif de sécurité.

Les responsabilités opérationnelles doivent être transférées séparément.

---

## 3.6 La fin d’un service ne retire pas les protections

La clôture du service mensuel ne doit pas supprimer :

- HTTPS ;
- authentification ;
- autorisation ;
- antiforgery ;
- validation serveur ;
- rate limiting ;
- headers de sécurité ;
- isolation PostgreSQL ;
- Data Protection ;
- protections déjà intégrées.

La clôture modifie la responsabilité de leur maintien.

---

## 3.7 LogicGate retire ses accès après validation

Les accès LogicGate doivent être retirés lorsque :

- le client ou le nouvel opérateur possède les accès nécessaires ;
- la reprise a été testée ;
- la période de transition est terminée ;
- les responsabilités sont confirmées.

LogicGate ne doit pas conserver silencieusement un accès permanent sans finalité.

---

## 3.8 La suppression est distincte du transfert

Un transfert consiste à remettre la responsabilité.

Une suppression consiste à détruire des actifs ou données.

Aucune suppression ne doit être déduite implicitement d’une clôture de service.

---

# 4. Non-objectifs

Ce document ne constitue pas :

- un contrat juridique ;
- une cession automatique de propriété intellectuelle ;
- une licence logicielle ;
- une validation comptable ;
- un conseil juridique ;
- un engagement de support post-clôture ;
- une garantie de compétence du nouvel opérateur ;
- une autorisation de transfert de données à un tiers non identifié ;
- une procédure universelle pour un secteur réglementé.

Les dispositions contractuelles applicables restent prioritaires.

---

# 5. Terminologie

## 5.1 Livraison initiale

Remise du projet après réalisation et recette.

LogicGate peut continuer à assurer l’exploitation.

---

## 5.2 Handover

Transfert organisé des actifs, accès, connaissances et responsabilités vers :

- le client ;
- son équipe interne ;
- un prestataire tiers ;
- un nouvel hébergeur ;
- un repreneur désigné.

---

## 5.3 Transfert partiel

LogicGate conserve certaines responsabilités.

Exemple :

```text
Client :
contenus et utilisateurs

LogicGate :
hébergement, backups, sécurité et mises à jour
```

---

## 5.4 Transfert complet

Le client ou son prestataire reprend l’ensemble de l’exploitation.

---

## 5.5 Clôture de service

Fin d’une prestation LogicGate telle que :

- hébergement ;
- maintenance ;
- supervision ;
- support ;
- sauvegardes ;
- exploitation.

---

## 5.6 Décommissionnement

Arrêt et suppression contrôlés d’un environnement ou d’un système.

---

## 5.7 Période de transition

Période pendant laquelle LogicGate accompagne le nouveau responsable.

---

## 5.8 Acceptance certificate

Document confirmant que les actifs et responsabilités ont été reçus et vérifiés.

---

## 5.9 Asset inventory

Inventaire des actifs techniques, opérationnels et documentaires du projet.

---

# 6. Types de transfert

## 6.1 Livraison avec exploitation LogicGate

Le client reçoit :

- accès fonctionnels ;
- documentation utilisateur ;
- confirmation de version ;
- responsabilités client.

LogicGate conserve :

- infrastructure ;
- secrets techniques ;
- déploiements ;
- backups ;
- maintenance ;
- monitoring.

---

## 6.2 Transfert fonctionnel

Le client reprend :

- contenus ;
- comptes utilisateurs ;
- administration fonctionnelle ;
- publications ;
- gestion des leads.

LogicGate conserve :

- code ;
- infrastructure ;
- sécurité ;
- backups ;
- déploiement ;
- maintenance technique.

---

## 6.3 Transfert du repository

Le client reçoit ou contrôle le repository.

LogicGate peut continuer à assurer l’exploitation selon le contrat.

---

## 6.4 Transfert de l’exploitation

Le nouvel opérateur reprend :

- déploiement ;
- migrations ;
- backups ;
- monitoring ;
- sécurité ;
- incidents ;
- mises à jour.

---

## 6.5 Transfert complet

Le transfert complet couvre :

```text
Code
Repository
Packages ou accès aux packages
Images
Infrastructure
Domaines
DNS
Secrets
Données
Backups
Runbooks
Monitoring
Comptes
Responsabilités
```

---

## 6.6 Clôture avec arrêt du projet

Le site est arrêté et les actifs sont :

- remis ;
- archivés ;
- supprimés ;
- ou conservés selon les décisions validées.

---

# 7. Déclencheurs possibles

Un transfert peut être déclenché par :

- fin de projet ;
- livraison contractuelle ;
- fin d’un service mensuel ;
- changement de prestataire ;
- internalisation par le client ;
- changement d’hébergeur ;
- fusion ou acquisition ;
- fermeture d’activité ;
- litige ;
- indisponibilité durable de LogicGate ;
- évolution du besoin hors du périmètre SiteFactory.

---

# 8. Responsables du transfert

## 8.1 Responsable LogicGate

Coordonne :

- inventaire ;
- préparation ;
- vérification ;
- documentation ;
- retrait des accès ;
- preuve de transfert.

---

## 8.2 Responsable client

Confirme :

- destinataires ;
- habilitations ;
- accès reçus ;
- compréhension ;
- responsabilités acceptées ;
- date de reprise.

---

## 8.3 Nouvel opérateur

Lorsque distinct du client, il doit être explicitement identifié.

LogicGate ne doit pas transmettre des accès sensibles à un tiers sans validation du client.

---

## 8.4 Validateur sécurité

Pour un projet renforcé, un validateur peut vérifier :

- secrets ;
- comptes ;
- accès ;
- sauvegardes ;
- exposition ;
- retrait LogicGate.

---

# 9. Préconditions du transfert

Avant de commencer :

```text
[ ] Type de transfert défini
[ ] Périmètre contractuel vérifié
[ ] Destinataires identifiés
[ ] Nouvel opérateur identifié
[ ] Date de transfert définie
[ ] Version active identifiée
[ ] Incidents ouverts inventoriés
[ ] Sauvegarde récente disponible
[ ] Repository propre
[ ] Documentation existante recensée
[ ] Secrets recensés
[ ] Comptes recensés
[ ] Fournisseurs recensés
[ ] Responsabilités futures définies
```

---

# 10. Inventaire des actifs

L’inventaire doit inclure les actifs applicables parmi les catégories suivantes.

## 10.1 Code source

```text
Repository
Branches actives
Tags
Commit de production
Sous-modules éventuels
Historique utile
Licences
Dépendances privées
```

---

## 10.2 Artefacts

```text
Packages NuGet
CLI
Images Docker
Checksums
SBOM éventuelle
Release manifests
Archives de release
```

---

## 10.3 Configuration

```text
Configuration client
Configuration runtime non sensible
Compose
Dockerfile
Caddy
Scripts
Variables requises
Feature flags
Kill switches
Entitlements
```

---

## 10.4 Données

```text
PostgreSQL
App_Data
Médias
Documents
Data Protection Keys
Audit
Exports nécessaires
```

---

## 10.5 Infrastructure

```text
VPS
Docker
Volumes
Réseaux
Firewall
Caddy
DNS
Domaines
Registry
Stockage backup
```

---

## 10.6 Accès

```text
GitHub
VPS
SSH
DNS
Registrar
Hébergement
Registry
SMTP
Backups
Monitoring
Administration applicative
```

---

## 10.7 Documentation

```text
README
AGENTS
Architecture
Migrations
Déploiement
Backups
Restauration
Runbooks
Maintenance
Release
Journal des décisions
```

---

# 11. Fiche d’inventaire

Exemple conceptuel :

```yaml
handover:
  clientSlug: sample-project
  handoverType: full
  effectiveAtUtc: 2026-08-31T18:00:00Z

  application:
    version: 1.4.0
    gitTag: v1.4.0
    commitSha: abc1234
    imageTag: 1.4.0
    imageDigest: sha256:...

  siteFactory:
    generatorVersion: 0.6.0
    catalogVersion: 1.2.0
    template:
      id: sample-template
      version: 1.1.0
    theme:
      id: sample-theme
      version: 1.0.3

  responsibilities:
    hosting: client
    dns: client
    backups: client
    maintenance: client
    securityUpdates: client
    applicationUsers: client
```

Ce fichier ne contient aucun secret.

---

# 12. État technique avant transfert

Le projet doit être classé dans l’un des états suivants :

```text
ready-for-handover
ready-with-known-limitations
handover-blocked
```

---

## 12.1 `ready-for-handover`

- build vert ;
- tests verts ;
- version connue ;
- documentation disponible ;
- sauvegarde vérifiée ;
- aucun incident bloquant ;
- accès préparés.

---

## 12.2 `ready-with-known-limitations`

Le transfert est possible, mais certaines limites sont documentées.

Exemples :

- module déprécié ;
- upgrade disponible ;
- restore drill ancien ;
- dette technique identifiée ;
- tâche non bloquante ouverte.

---

## 12.3 `handover-blocked`

Exemples :

- version inconnue ;
- données non sauvegardées ;
- secret compromis ;
- migration incohérente ;
- repository incomplet ;
- accès administrateur absent ;
- incident critique actif ;
- backup non restaurable ;
- propriété du domaine incertaine.

---

# 13. Contrôles techniques avant transfert

Baseline :

```text
Restore
Build Release
Tests
Security scans
Dependency scan
Secret scan
Projet généré ou client validé
Docker build
Health checks
Smoke tests
Backup
Checksum
Restore drill récent ou vérification adaptée
```

---

## 13.1 Warnings

Les warnings connus doivent être :

- corrigés ;
- documentés ;
- ou acceptés explicitement.

---

## 13.2 Dette technique

La dette ne doit pas être masquée.

Elle doit préciser :

```text
Élément
Impact
Priorité
Contournement
Recommandation
```

---

# 14. Transfert du repository

## 14.1 Options

Le transfert peut utiliser :

- changement de propriétaire GitHub ;
- transfert vers une organisation client ;
- ajout du client comme administrateur ;
- miroir complet ;
- archive Git avec historique.

---

## 14.2 Historique

Le transfert complet doit préserver :

- commits ;
- tags ;
- branches utiles ;
- releases ;
- issues pertinentes ;
- pull requests lorsque la plateforme le permet.

---

## 14.3 Repository propre

Avant transfert :

```text
main verte
working tree propre
tags cohérents
branches temporaires nettoyées
secrets absents
artefacts sensibles absents
```

---

## 14.4 Protection de branche

Les règles de protection doivent être :

- transférées ;
- documentées ;
- recréées si la plateforme ne les conserve pas.

---

## 14.5 Secrets GitHub

Les secrets GitHub ne sont pas transférés automatiquement comme valeurs lisibles.

Ils doivent être :

- recréés ;
- remplacés ;
- ou renouvelés par le nouveau propriétaire.

---

## 14.6 Apps et intégrations

Recenser :

- GitHub Apps ;
- webhooks ;
- Dependabot ;
- registry ;
- bots ;
- intégrations de sécurité.

---

# 15. Transfert des packages

## 15.1 Cas exclusivement Business : dépendances privées

Cette section ne définit pas une dépendance du public à un feed privé.
Si une future application Business en utilise, son transfert doit préciser
les droits applicables et si le client :

- conserve un droit d’accès au feed ;
- reçoit une licence d’utilisation ;
- doit migrer vers un feed client ;
- doit disposer d’une copie des versions nécessaires.

---

## 15.2 Version verrouillée

Le client doit conserver les versions exactes nécessaires à la reconstruction.

---

## 15.3 Indisponibilité future du feed

Le plan doit éviter qu’un projet transféré devienne impossible à restaurer uniquement parce qu’un feed LogicGate n’est plus accessible.

Options :

- feed client ;
- archive des packages autorisés ;
- cache de reprise ;
- contrat d’accès limité ;
- publication selon les droits applicables.

---

## 15.4 Licences

Le transfert doit respecter :

- licences LogicGate ;
- licences open source ;
- licences de composants tiers ;
- restrictions contractuelles.

---

# 16. Transfert des images Docker

Le client doit recevoir ou pouvoir reconstruire :

- image active ;
- image précédente utile au rollback ;
- tag ;
- digest ;
- Dockerfile ;
- pipeline de construction ;
- registry ou archive.

---

## 16.1 Registry LogicGate

Lorsque l’image reste dans un registry LogicGate :

- durée d’accès ;
- permissions ;
- politique de rétention ;
- date de fin ;
- procédure de copie ;

doivent être documentées.

---

## 16.2 Registry client

Le transfert vers un registry client doit :

1. identifier les images ;
2. vérifier les digests ;
3. copier ;
4. vérifier ;
5. mettre à jour Compose ;
6. tester ;
7. retirer l’accès LogicGate lorsque prévu.

---

# 17. Transfert des domaines

## 17.1 Propriété

La propriété ou le titulaire du domaine doit être clairement identifié.

Le domaine devrait idéalement appartenir au client.

---

## 17.2 Informations à transférer

```text
Registrar
Compte propriétaire
Contact administratif
Contact technique
Date d’expiration
Renouvellement automatique
Moyen de paiement
Verrouillage du domaine
Codes de transfert éventuels
```

---

## 17.3 Code de transfert

Un code de transfert de domaine est un secret temporaire.

Il doit être transmis par un canal sécurisé.

---

## 17.4 Renouvellement

La responsabilité du renouvellement doit être explicitement attribuée.

---

## 17.5 DNS

Avant transfert, inventorier :

- A ;
- AAAA ;
- CNAME ;
- MX ;
- TXT ;
- SPF ;
- DKIM ;
- DMARC ;
- sous-domaines ;
- staging ;
- anciennes redirections.

---

# 18. Transfert DNS

## 18.1 Export

Fournir un inventaire lisible des enregistrements nécessaires.

---

## 18.2 Secrets DNS

Les tokens API du fournisseur DNS ne sont pas transférés en clair.

Le nouveau propriétaire doit créer ses propres credentials.

---

## 18.3 Tests

Après transfert :

- résolution ;
- HTTPS ;
- email ;
- SPF ;
- DKIM ;
- DMARC ;
- sous-domaines ;
- staging ;

doivent être vérifiés selon le périmètre.

---

## 18.4 Ancien accès LogicGate

L’accès LogicGate au DNS doit être retiré après validation.

---

# 19. Transfert de l’hébergement

## 19.1 VPS conservé par le client

Le transfert doit couvrir :

- compte fournisseur ;
- facturation ;
- accès ;
- SSH ;
- firewall ;
- Docker ;
- volumes ;
- sauvegardes ;
- monitoring ;
- renouvellement.

---

## 19.2 Migration vers un nouveau VPS

Le processus suit :

```text
préparation
→ backup
→ nouvelle infrastructure
→ restauration
→ tests
→ DNS
→ surveillance
→ arrêt de l’ancien serveur
```

---

## 19.3 VPS mutualisé LogicGate

Un projet ne peut pas rester indéfiniment sur un VPS mutualisé LogicGate après fin complète de service sans accord spécifique.

Options :

- migration vers VPS client ;
- contrat d’hébergement distinct ;
- délai de transition ;
- arrêt planifié.

---

## 19.4 Isolation

Lors de l’extraction d’un client d’un VPS mutualisé, vérifier :

- volumes du bon client ;
- base du bon client ;
- secrets du bon client ;
- routes Caddy du bon client ;
- backups du bon client ;
- absence de données d’un autre client.

---

# 20. Transfert PostgreSQL

Le transfert doit fournir :

- version PostgreSQL ;
- nom logique de la base ;
- compte applicatif à recréer ;
- migrations ;
- backup logique ;
- manifest ;
- checksum ;
- procédure de restauration ;
- procédure de rotation du mot de passe.

---

## 20.1 Mot de passe PostgreSQL

Le mot de passe doit être :

- renouvelé ;
- créé par le nouvel opérateur ;
- ou transféré temporairement puis remplacé.

La rotation après transfert est recommandée.

---

## 20.2 Compte superutilisateur

Le compte applicatif ne doit pas être transformé en superutilisateur pour faciliter le transfert.

---

## 20.3 Validation

Le nouvel opérateur doit restaurer ou valider le backup dans un environnement isolé lorsque le transfert est complet.

---

# 21. Transfert de `App_Data`

Le transfert doit couvrir :

- médias ;
- documents ;
- fichiers persistants ;
- exclusions ;
- permissions ;
- structure ;
- taille ;
- checksum ;
- procédure de restauration.

---

## 21.1 Fichiers temporaires

Les fichiers temporaires ou expirés doivent être exclus selon la politique.

---

## 21.2 Données privées

Les documents privés restent protégés pendant :

- copie ;
- stockage ;
- transfert ;
- restauration.

---

## 21.3 Canal de transfert

Le canal doit être :

- chiffré ;
- limité ;
- vérifié ;
- temporaire si nécessaire.

---

# 22. Transfert Data Protection

Les clés Data Protection doivent être transférées lorsque cela est nécessaire à la continuité.

Le transfert doit préserver :

- confidentialité ;
- intégrité ;
- permissions ;
- nom d’application ;
- environnement.

---

## 22.1 Rotation après transfert

Selon l’architecture, une rotation ou nouvelle protection des clés peut être planifiée après reprise.

---

## 22.2 Conséquence de non-transfert

Le client doit être informé des impacts potentiels :

- sessions invalidées ;
- tokens invalides ;
- données protégées illisibles.

---

# 23. Transfert des secrets

## 23.1 Inventaire sans valeur

Fournir une liste telle que :

```text
Secret name
Purpose
Provider
Environment
Rotation procedure
New owner
Required after handover
```

---

## 23.2 Valeurs transférées

Les valeurs doivent être transmises par :

- gestionnaire de mots de passe ;
- coffre sécurisé ;
- secret manager ;
- canal chiffré approuvé.

---

## 23.3 Secrets à recréer

Il est préférable que le nouvel opérateur recrée :

- tokens GitHub ;
- tokens registry ;
- clés DNS ;
- clés de backup ;
- clés API ;
- comptes SMTP ;
- clés SSH.

---

## 23.4 Rotation

Après transfert :

```text
nouveau secret créé
→ configuration mise à jour
→ service recréé
→ fonctionnement vérifié
→ ancien secret révoqué
```

---

## 23.5 Confirmation

LogicGate ne doit pas conserver une copie active d’un secret lorsque sa responsabilité est terminée.

---

# 24. Transfert des comptes administrateurs

## 24.1 Comptes individuels

Le client doit posséder des comptes individuels.

Un compte LogicGate partagé ne doit pas devenir le compte principal du client.

---

## 24.2 Premier administrateur client

Avant retrait LogicGate :

- compte créé ;
- email confirmé si utilisé ;
- rôle attribué ;
- connexion testée ;
- récupération documentée ;
- protection du dernier administrateur vérifiée.

---

## 24.3 Comptes LogicGate

Les comptes LogicGate doivent être :

- désactivés ;
- supprimés ;
- ou conservés temporairement avec date d’expiration.

---

## 24.4 Audit

Le retrait ou la conservation doit être audité.

---

# 25. Transfert des accès SSH

## 25.1 Clés individuelles

Le client ou nouvel opérateur fournit ses propres clés.

---

## 25.2 Clés LogicGate

Les clés LogicGate doivent être retirées du serveur après validation de reprise.

---

## 25.3 Compte root

Le transfert ne doit pas conduire à l’usage permanent de `root`.

---

## 25.4 Groupe Docker

L’accès au groupe Docker est un accès privilégié.

Le client doit comprendre son niveau de pouvoir.

---

# 26. Transfert SMTP

Le transfert doit préciser :

- fournisseur ;
- domaine ;
- compte propriétaire ;
- adresse d’envoi ;
- Reply-To ;
- DNS ;
- quota ;
- logs ;
- procédure de rotation ;
- responsable de facturation.

---

## 26.1 Credentials

Les credentials sont recréés ou renouvelés.

---

## 26.2 Test

Après transfert :

- envoi ;
- réception ;
- Reply-To ;
- SPF ;
- DKIM ;
- DMARC ;
- absence de double envoi ;

doivent être vérifiés selon le périmètre.

---

# 27. Transfert des fournisseurs externes

Pour chaque fournisseur :

```text
Nom
Finalité
Compte propriétaire
Facturation
Secrets
Données transmises
Rétention
Procédure d’arrêt
Responsable
```

Exemples :

- SMTP ;
- DNS ;
- hébergeur ;
- registry ;
- backup ;
- analytics ;
- LLM ;
- stockage ;
- CAPTCHA.

---

# 28. Transfert des sauvegardes

Le transfert doit inclure :

- dernière sauvegarde complète ;
- historique selon contrat ;
- manifest ;
- checksums ;
- chiffrement ;
- clé transmise séparément ;
- politique de rétention ;
- destination hors VPS ;
- dernier restore drill ;
- procédure de restauration.

---

## 28.1 Backup final

Un backup final doit être créé à proximité de la date effective de transfert.

---

## 28.2 Vérification

Le backup final doit être :

- présent ;
- lisible ;
- vérifié ;
- copié ;
- identifié ;
- compatible avec la version remise.

---

## 28.3 Anciennes sauvegardes LogicGate

Leur devenir doit être décidé :

```text
transférées
conservées temporairement
supprimées après délai
placées en hold
```

---

# 29. Transfert de la restauration

Le client doit recevoir une procédure permettant de restaurer :

- PostgreSQL ;
- App_Data ;
- Data Protection ;
- configuration ;
- version applicative ;
- service complet.

---

## 29.1 Démonstration

Pour un transfert complet, une démonstration ou un restore drill partagé est recommandé.

---

## 29.2 Critère de succès

Le nouvel opérateur doit pouvoir :

```text
identifier un backup
→ vérifier son checksum
→ préparer un environnement
→ restaurer
→ démarrer l’application
→ exécuter les smoke tests
```

---

# 30. Transfert de la CI/CD

Le transfert doit couvrir :

- workflows ;
- permissions ;
- secrets ;
- environnements GitHub ;
- checks obligatoires ;
- branch protection ;
- feed ;
- registry ;
- version .NET ;
- actions ;
- artefacts ;
- releases.

---

## 30.1 Secrets CI

Ils doivent être recréés par le nouveau propriétaire.

---

## 30.2 Publication

Le nouvel opérateur doit comprendre que :

- une pull request ne publie pas ;
- une release ne déploie pas automatiquement ;
- les migrations restent explicites ;
- les environnements protégés doivent être conservés.

---

# 31. Transfert de la maintenance

Le transfert doit préciser :

```text
Versions supportées
Versions actuelles
Upgrades disponibles
Modules dépréciés
Vulnérabilités connues
Correctifs en attente
Fenêtres de maintenance
Cadence recommandée
```

---

## 31.1 Dépendances

Le nouvel opérateur doit recevoir les règles de mise à jour :

```text
check
→ plan
→ branche
→ tests
→ staging
→ déploiement
```

---

## 31.2 Correctifs de sécurité

La responsabilité de surveillance doit être explicitement transférée.

---

# 32. Transfert de l’observabilité

Le transfert doit couvrir :

- health checks ;
- logs ;
- rotation ;
- alertes ;
- monitoring ;
- dashboards ;
- accès ;
- rétention ;
- runbooks.

---

## 32.1 Logs historiques

Les logs historiques ne sont pas systématiquement transférés.

Le transfert dépend :

- du contrat ;
- de la finalité ;
- de la rétention ;
- de la confidentialité ;
- des incidents ouverts.

---

## 32.2 Alertes

Les destinataires d’alerte doivent être remplacés.

Une alerte ne doit pas continuer à être envoyée uniquement à LogicGate après la clôture.

---

# 33. Transfert des runbooks

Les runbooks remis doivent correspondre :

- aux modules installés ;
- à l’infrastructure réelle ;
- aux noms de services ;
- aux chemins ;
- aux domaines ;
- aux responsabilités.

Aucun placeholder ne doit rester.

---

## 33.1 Runbooks minimum

```text
Application indisponible
PostgreSQL indisponible
Déploiement en échec
Migration en échec
Backup en échec
Restauration
Disque saturé
SMTP en échec
Récupération administrateur
Perte du VPS
```

---

# 34. Documentation utilisateur

Le client fonctionnel doit recevoir selon le projet :

- connexion ;
- gestion des utilisateurs ;
- gestion des leads ;
- contenus ;
- médias ;
- portfolio ;
- publication ;
- exports ;
- diagnostic fonctionnel ;
- bonnes pratiques de sécurité.

---

# 35. Documentation technique

Le nouvel opérateur doit recevoir :

```text
Architecture
Structure du repository
Configuration
Build
Tests
Packages
Déploiement
Migrations
Backups
Restauration
Observabilité
Sécurité
Maintenance
Releases
Runbooks
```

---

# 36. Formation

## 36.1 Formation fonctionnelle

Peut couvrir :

- administration ;
- contenus ;
- leads ;
- utilisateurs ;
- médias ;
- publication.

---

## 36.2 Formation technique

Peut couvrir :

- repository ;
- CI ;
- Docker ;
- Caddy ;
- PostgreSQL ;
- déploiement ;
- migration ;
- backup ;
- restauration ;
- incidents.

---

## 36.3 Formation sécurité

Doit rappeler :

- comptes individuels ;
- MFA lorsque disponible ;
- secrets ;
- SSH ;
- permissions ;
- sauvegardes ;
- phishing ;
- signalement d’incident.

---

## 36.4 Preuve

La formation peut être consignée par :

- compte-rendu ;
- support remis ;
- liste des participants ;
- questions ouvertes ;
- validation.

---

# 37. Recette de transfert

La recette de transfert est distincte de la recette fonctionnelle initiale.

Elle vérifie la capacité de reprise.

---

## 37.1 Recette fonctionnelle

```text
Connexion admin
Utilisateurs
Contenus
Leads
Formulaires
Médias
Publication
```

---

## 37.2 Recette technique

```text
Clone du repository
Restore
Build Release
Tests
Accès au feed
Accès au registry
Docker
Health checks
Logs
```

---

## 37.3 Recette opérationnelle

```text
Déploiement staging
Migration contrôlée
Backup
Vérification backup
Restauration de test
Smoke tests
Rollback
```

---

## 37.4 Recette sécurité

```text
Accès LogicGate recensés
Comptes client actifs
Secrets renouvelés
SSH vérifié
PostgreSQL non public
AdminSeed désactivé
Diagnostics protégés
```

---

# 38. Critères d’acceptation du transfert

Le transfert complet peut être accepté lorsque :

```text
[ ] Repository reçu
[ ] Version vérifiée
[ ] Build réussi
[ ] Tests réussis
[ ] Accès fonctionnels reçus
[ ] Accès techniques reçus
[ ] Secrets recréés ou transférés
[ ] Domaine sous contrôle
[ ] DNS sous contrôle
[ ] Hébergement sous contrôle
[ ] PostgreSQL restaurable
[ ] App_Data restaurable
[ ] Data Protection traitée
[ ] Backup final vérifié
[ ] Runbooks reçus
[ ] Alertes réattribuées
[ ] Compte administrateur client testé
[ ] Responsabilités acceptées
[ ] Accès LogicGate retirables
```

---

# 39. Période de transition

## 39.1 Objectif

Permettre au nouvel opérateur de reprendre progressivement.

---

## 39.2 Durée

La durée doit être définie contractuellement ou dans le plan de transfert.

---

## 39.3 Pendant la transition

LogicGate peut :

- répondre aux questions ;
- assister une première opération ;
- superviser un premier déploiement ;
- assister un restore drill ;
- corriger une documentation incomplète.

---

## 39.4 Limites

La période de transition ne doit pas devenir une maintenance implicite sans fin.

---

# 40. Support post-transfert

Le support post-transfert doit préciser :

- durée ;
- heures ;
- canal ;
- périmètre ;
- temps de réponse ;
- inclusions ;
- exclusions ;
- tarification éventuelle.

---

## 40.1 Défaut antérieur

Un défaut relevant du périmètre livré peut suivre les règles contractuelles applicables.

---

## 40.2 Nouvelle modification

Une modification réalisée par le nouveau responsable peut nécessiter un diagnostic ou devis distinct.

---

# 41. Clôture d’un service mensuel

La clôture d'un contrat d'hébergement, de maintenance ou de support doit
suivre une procédure explicite. Aucun nom ni prix d'offre propriétaire
n'est requis pour appliquer ces exigences de réversibilité.

---

## 41.1 Préavis

La date de fin doit être connue suffisamment tôt pour organiser :

- transfert ;
- sauvegarde ;
- accès ;
- hébergement ;
- facturation ;
- renouvellement ;
- responsabilités.

---

## 41.2 Services à requalifier

```text
Hébergement
Backups
Monitoring
Maintenance
Support
Mises à jour
Gestion DNS
Gestion certificats
Gestion SMTP
Gestion incidents
```

---

## 41.3 Site maintenu en ligne

Le site peut continuer à fonctionner après la fin du service si :

- un hébergement reste actif ;
- les factures sont prises en charge ;
- le domaine reste renouvelé ;
- un responsable reprend l’exploitation ;
- les secrets et accès restent valides ;
- les backups sont repris.

---

## 41.4 Fin de maintenance sans fin d’hébergement

LogicGate peut éventuellement conserver uniquement l’hébergement selon un nouveau contrat.

Les responsabilités doivent être redéfinies.

---

## 41.5 Fin d’hébergement sans fin du projet

Le client reprend l’hébergement ou désigne un nouveau fournisseur.

---

# 42. Matrice de responsabilités après clôture

Exemple :

| Responsabilité | Avant clôture | Après clôture |
|---|---|---|
| Hébergement | LogicGate | Client |
| Domaine | Client | Client |
| DNS | LogicGate | Client |
| Backups | LogicGate | Client |
| Restauration | LogicGate | Client |
| Correctifs de sécurité | LogicGate | Client |
| Utilisateurs | Client | Client |
| Contenus | Client | Client |
| Déploiements | LogicGate | Prestataire client |
| Migrations | LogicGate | Prestataire client |

La matrice doit être adaptée à chaque projet.

---

# 43. Date effective

Une date et une heure UTC doivent définir le changement de responsabilité.

Exemple :

```text
Effective handover:
2026-08-31T18:00:00Z
```

Après cette date, les responsabilités suivent la matrice confirmée.

---

# 44. Gel de changement avant transfert

Une courte période de gel peut être nécessaire avant un transfert complet.

Pendant le gel :

- aucune évolution non urgente ;
- aucun changement de secret non coordonné ;
- aucune migration ;
- aucun nouveau compte ;
- aucune modification d’infrastructure importante.

---

# 45. Backup final

## 45.1 Moment

Le backup final doit être réalisé :

- après le gel ;
- avant la remise définitive ;
- ou juste avant la bascule d’infrastructure.

---

## 45.2 Contenu

```text
PostgreSQL
App_Data
Data Protection si nécessaire
Manifest
Checksums
Version
```

---

## 45.3 Validation

Le client ou nouvel opérateur doit confirmer la réception et l’intégrité.

---

# 46. Transfert des données

## 46.1 Minimisation

Seules les données du projet concerné sont transférées.

---

## 46.2 Client correct

Sur une infrastructure mutualisée, des contrôles renforcés doivent empêcher le transfert de données d’un autre client.

---

## 46.3 Canal

Les données sont transférées par un canal sécurisé.

---

## 46.4 Accusé de réception

Le destinataire confirme :

- réception ;
- taille ;
- checksum ;
- capacité de lecture ;
- stockage sûr.

---

# 47. Conservation temporaire par LogicGate

LogicGate peut conserver temporairement une copie pour :

- sécurité de transition ;
- reprise ;
- obligation contractuelle ;
- résolution d’un incident.

---

## 47.1 Conditions

La conservation doit préciser :

```text
Périmètre
Finalité
Durée
Emplacement
Protection
Date de suppression
Responsable
```

---

## 47.2 Pas de conservation indéfinie

La copie ne doit pas être conservée sans date de fin.

---

# 48. Suppression des données LogicGate

Après la période prévue :

1. vérifier que le client possède les données ;
2. vérifier l’absence d’incident ouvert ;
3. vérifier les obligations de conservation ;
4. supprimer les copies applicables ;
5. supprimer les credentials associés ;
6. produire une preuve ou un compte-rendu ;
7. mettre à jour l’inventaire.

---

## 48.1 Backups en rotation

Une donnée peut rester dans les backups jusqu’à expiration de leur rétention.

Cette limite doit être documentée.

---

# 49. Retrait des accès LogicGate

Inventaire à traiter :

```text
GitHub
VPS
SSH
Docker
DNS
Registrar
Registry
SMTP
Backups
Monitoring
Administration applicative
Fournisseurs externes
```

---

## 49.1 Ordre

Les accès doivent être retirés après que le nouveau responsable a confirmé les siens.

---

## 49.2 Validation

Le client doit vérifier qu’il conserve au moins :

- un administrateur GitHub ;
- un administrateur applicatif ;
- un accès infrastructure ;
- un accès domaine ;
- un accès backup ;
- un accès aux secrets.

---

## 49.3 Session et token

Après retrait :

- révoquer tokens ;
- supprimer clés SSH ;
- désactiver comptes ;
- invalider sessions ;
- vérifier les audits.

---

# 50. Clôture des fournisseurs LogicGate

Lorsque LogicGate possède un compte utilisé uniquement pour le client :

- transférer ;
- migrer ;
- arrêter ;
- ou supprimer.

Exemples :

- VPS ;
- bucket backup ;
- SMTP ;
- monitoring ;
- DNS ;
- registry.

---

## 50.1 Fournisseur mutualisé

Le client ne reçoit pas les credentials d’un compte mutualisé LogicGate.

Il doit utiliser :

- son propre compte ;
- un espace isolé ;
- une migration contrôlée.

---

# 51. Décommissionnement d’un environnement

## 51.1 Préconditions

```text
[ ] Décision validée
[ ] Date définie
[ ] Backup final
[ ] Données transférées
[ ] Rétention définie
[ ] DNS préparé
[ ] Fournisseurs préparés
[ ] Accès inventoriés
[ ] Confirmation client
```

---

## 51.2 Procédure

1. arrêter les écritures ;
2. créer le backup final ;
3. vérifier ;
4. transférer ;
5. retirer le trafic ;
6. arrêter les conteneurs ;
7. conserver temporairement les volumes si prévu ;
8. retirer Caddy ;
9. retirer DNS lorsque approprié ;
10. retirer secrets ;
11. supprimer les volumes après validation ;
12. archiver le repository ;
13. produire le rapport.

---

## 51.3 Suppression des volumes

La suppression doit être :

- explicite ;
- ciblée ;
- confirmée ;
- consignée.

---

## 51.4 Commande interdite par défaut

```bash
docker compose down --volumes
```

ne doit pas être utilisée comme étape générique de clôture sans validation des volumes.

---

# 52. Fermeture du domaine

Lorsqu’un domaine n’est plus utilisé :

- vérifier sa propriété ;
- vérifier les emails ;
- vérifier les redirections ;
- vérifier les risques d’usurpation ;
- décider du renouvellement ;
- retirer les DNS obsolètes ;
- protéger les données historiques.

Un domaine abandonné peut être racheté par un tiers.

---

# 53. Fermeture des emails

Avant suppression d’un domaine ou compte SMTP :

- vérifier les formulaires ;
- vérifier les destinataires ;
- vérifier les comptes utilisateurs ;
- vérifier les alertes ;
- vérifier les messages de récupération ;
- remplacer les adresses nécessaires.

---

# 54. Archivage du repository

## 54.1 Statut

Le repository peut être :

```text
active
read-only
archived
transferred
deleted
```

---

## 54.2 Archive

Avant archivage :

- tag final ;
- changelog final ;
- README de clôture ;
- version finale ;
- état des responsabilités ;
- suppression des secrets ;
- fermeture des branches inutiles.

---

## 54.3 Suppression

La suppression complète du repository nécessite une décision explicite.

Une archive peut rester nécessaire pour :

- maintenance ;
- preuve ;
- sécurité ;
- restauration ;
- obligations contractuelles.

---

# 55. README de clôture

Exemple :

```markdown
# Project closure

Status: transferred

Effective date: 2026-08-31

Final application version: 1.4.0

Final commit: abc1234

New technical owner: Client technical team

LogicGate operational responsibility ended:
2026-08-31T18:00:00Z

See:
- HANDOVER.md
- docs/operations/
- CHANGELOG.md
```

Aucune donnée personnelle inutile ne doit être ajoutée.

---

# 56. Rapport de transfert

Fichier possible :

```text
handover-report.json
```

Structure conceptuelle :

```json
{
  "schemaVersion": 1,
  "handoverId": "a0da3671-6577-4d9c-8154-d9fbc96c6c01",
  "clientSlug": "sample-project",
  "handoverType": "full",
  "effectiveAtUtc": "2026-08-31T18:00:00Z",
  "application": {
    "version": "1.4.0",
    "gitTag": "v1.4.0",
    "commitSha": "abc1234",
    "imageTag": "1.4.0",
    "imageDigest": "sha256:..."
  },
  "assets": {
    "repositoryTransferred": true,
    "databaseBackupTransferred": true,
    "appDataTransferred": true,
    "dataProtectionTransferred": true,
    "runbooksTransferred": true
  },
  "access": {
    "clientAdminVerified": true,
    "clientSshVerified": true,
    "logicGateAccessRemoved": true
  },
  "validation": {
    "build": "success",
    "tests": "success",
    "restoreDrill": "success",
    "smokeTests": "success"
  },
  "status": "completed"
}
```

---

## 56.1 Données interdites

Le rapport ne contient pas :

- secrets ;
- mots de passe ;
- tokens ;
- chaînes de connexion ;
- données métier ;
- coordonnées personnelles non nécessaires.

---

# 57. Document d’acceptation

Le document d’acceptation doit confirmer :

```text
Actifs reçus
Accès reçus
Version reçue
Backup reçu
Documentation reçue
Formation réalisée
Responsabilités acceptées
Date effective
Limites connues
Accès LogicGate retirés ou date de retrait
```

---

## 57.1 Réserves

Le client peut accepter avec réserves.

Chaque réserve doit contenir :

- élément ;
- impact ;
- responsable ;
- date cible ;
- condition de clôture.

---

# 58. Incident découvert pendant le transfert

## 58.1 Blocant

Un incident critique peut suspendre le transfert.

Exemples :

- backup invalide ;
- secret compromis ;
- version inconnue ;
- donnée manquante ;
- accès impossible ;
- corruption.

---

## 58.2 Non bloquant

Un défaut mineur peut être consigné comme réserve.

---

## 58.3 Responsabilité

La date effective doit être repoussée si le nouvel opérateur ne peut pas assumer raisonnablement la reprise.

---

# 59. Litige ou transfert contraint

Même en cas de désaccord :

- préserver les données ;
- préserver le repository ;
- éviter les suppressions irréversibles ;
- maintenir une trace ;
- transférer selon les obligations applicables ;
- retirer les accès de manière contrôlée.

Codex ne doit pas prendre de décision contractuelle ou juridique.

---

# 60. Perte de contact avec le client

Une procédure spécifique doit être définie lorsque :

- le client ne répond plus ;
- un domaine arrive à expiration ;
- un VPS reste facturé ;
- des données sont encore hébergées ;
- un service doit être clôturé.

Les décisions doivent suivre :

- contrat ;
- communications précédentes ;
- obligations applicables ;
- prudence sur la suppression.

---

# 61. Continuité LogicGate

SiteFactory doit prévoir la possibilité qu’un autre opérateur reprenne un projet en cas d’indisponibilité durable de LogicGate.

Cela nécessite :

- repositories complets ;
- versions ;
- runbooks ;
- backups ;
- accès recensés ;
- documentation ;
- absence de connaissance uniquement orale.

---

# 62. Bus factor

Les informations critiques ne doivent pas exister uniquement :

- dans la mémoire du fondateur ;
- sur un poste personnel ;
- dans un gestionnaire non partagé ;
- dans un terminal historique ;
- dans des messages privés non structurés.

---

# 63. Vérifications après transfert

Pendant la période de transition, vérifier :

- site accessible ;
- HTTPS ;
- backups ;
- alertes ;
- formulaire ;
- SMTP ;
- administration ;
- accès client ;
- accès LogicGate retirés ;
- facturation fournisseur ;
- renouvellement domaine.

---

# 64. Premier déploiement du nouvel opérateur

Il est recommandé que le nouvel opérateur réalise au moins un déploiement de staging.

Le scénario doit couvrir :

```text
Build
Image
Configuration
Migration sans impact ou exercice
Health
Smoke tests
Rollback
```

---

# 65. Premier backup du nouvel opérateur

Avant la fin de transition, vérifier que le nouveau responsable peut :

- lancer un backup ;
- vérifier le manifest ;
- vérifier les checksums ;
- copier hors VPS ;
- vérifier l’âge du dernier backup.

---

# 66. Premier restore drill du nouvel opérateur

Pour un transfert complet important, le nouvel opérateur doit idéalement participer à un restore drill.

Cela constitue une preuve plus forte qu’une simple lecture de documentation.

---

# 67. Sécurité du transfert

## 67.1 Accès temporaire

Les accès temporaires doivent posséder :

- durée limitée ;
- droits minimaux ;
- propriétaire ;
- révocation prévue.

---

## 67.2 Canal

Les canaux de transfert doivent être approuvés.

---

## 67.3 Vérification d’identité

Avant remise d’un secret ou accès critique, vérifier le destinataire selon le processus établi.

---

## 67.4 Journalisation

Les opérations sensibles doivent être auditées sans enregistrer la valeur du secret.

---

# 68. Données personnelles

Le transfert de données personnelles doit être limité :

- au bon client ;
- au bon destinataire ;
- au bon périmètre ;
- au canal prévu.

---

## 68.1 Prestataire tiers

Un prestataire tiers ne doit recevoir les données qu’après validation de son rôle et du client.

---

## 68.2 Copies de travail

Les copies temporaires doivent être supprimées après transfert et vérification.

---

# 69. Quality gates du transfert

Le transfert est bloqué lorsque :

```text
Version active inconnue

Repository incomplet

Secret détecté dans les documents

Backup final absent

Checksum invalide

Restauration impossible

Compte administrateur client absent

Accès infrastructure client non vérifié

Responsabilités non définies

Incident critique ouvert

Données d’un autre client potentiellement présentes

Documentation essentielle absente
```

---

# 70. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-HANDOVER-001` | Type de transfert inconnu |
| `SF-HANDOVER-002` | Destinataire non validé |
| `SF-HANDOVER-003` | Inventaire incomplet |
| `SF-HANDOVER-004` | Version active inconnue |
| `SF-HANDOVER-005` | Repository non transférable |
| `SF-HANDOVER-006` | Artefact manquant |
| `SF-HANDOVER-007` | Backup final absent |
| `SF-HANDOVER-008` | Checksum invalide |
| `SF-HANDOVER-009` | Restauration non vérifiée |
| `SF-HANDOVER-010` | Secret présent dans un document |
| `SF-HANDOVER-011` | Accès client non vérifié |
| `SF-HANDOVER-012` | Compte administrateur client absent |
| `SF-HANDOVER-013` | Responsabilité non attribuée |
| `SF-HANDOVER-014` | Accès LogicGate encore actif |
| `SF-HANDOVER-015` | Données croisées suspectées |
| `SF-HANDOVER-016` | Transfert bloqué par incident |
| `SF-CLOSURE-001` | Clôture non confirmée |
| `SF-CLOSURE-002` | Backup final non transféré |
| `SF-CLOSURE-003` | Suppression non autorisée |
| `SF-CLOSURE-004` | Rétention non définie |
| `SF-CLOSURE-005` | Fournisseur encore actif sans responsable |
| `SF-CLOSURE-006` | Environnement non décommissionnable |
| `SF-CLOSURE-007` | Domaine sans propriétaire confirmé |
| `SF-CLOSURE-008` | Archive finale incomplète |

---

# 71. Observabilité

Événements possibles :

```text
HandoverStarted
AssetInventoryCompleted
FinalBackupCompleted
RepositoryTransferred
SecretsRotationStarted
ClientAccessVerified
LogicGateAccessRemoved
HandoverAccepted
ServiceClosureStarted
EnvironmentDecommissioned
DataRetentionStarted
RetainedDataDeleted
ServiceClosureCompleted
```

---

## 71.1 Propriétés autorisées

```text
HandoverId
ClientSlug
HandoverType
Environment
ApplicationVersion
Result
EffectiveAtUtc
ErrorCode
```

---

## 71.2 Propriétés interdites

```text
Secret
Token
Password
Connection string
Contenu client
Données personnelles
```

---

# 72. Tests automatisés

Les tests doivent vérifier :

- génération de l’inventaire ;
- manifest ;
- absence de secret ;
- propriétés obligatoires ;
- versions ;
- chemins ;
- checksums ;
- rapports ;
- résolution des placeholders ;
- classification des actifs.

---

# 73. Tests opérationnels

Un transfert complet doit pouvoir être testé en staging ou environnement isolé.

Scénarios :

```text
Clone repository
Restore
Build
Tests
Docker
Backup
Restore
Health
Smoke tests
Rotation d’un secret fictif
Retrait d’un accès fictif
```

---

# 74. Tests de sécurité

Vérifier notamment :

- secret dans le rapport ;
- fichier `.env` dans l’archive documentaire ;
- clé SSH LogicGate restante ;
- compte administrateur LogicGate actif ;
- backup accessible publiquement ;
- données d’un autre client ;
- token registry partagé ;
- compte mutualisé transféré ;
- permissions excessives.

---

# 75. Tests de décommissionnement

Dans un environnement fictif :

- backup final ;
- arrêt ;
- retrait du trafic ;
- suppression ciblée des conteneurs ;
- conservation contrôlée des volumes ;
- suppression explicite ;
- retrait des secrets ;
- rapport.

La CI ne doit jamais décommissionner une production réelle.

---

# 76. Critères d’acceptation du MVP

La stratégie sera considérée comme opérationnelle lorsque :

1. les types de transfert sont définis ;
2. les responsabilités peuvent être attribuées ;
3. un inventaire des actifs peut être produit ;
4. la version active est identifiable ;
5. le repository peut être transféré ;
6. les packages nécessaires sont identifiés ;
7. les images nécessaires sont identifiées ;
8. les domaines et DNS sont recensés ;
9. l’hébergement est recensé ;
10. PostgreSQL est exportable et restaurable ;
11. App_Data est exportable ;
12. Data Protection est traitée ;
13. les secrets sont inventoriés sans valeur ;
14. les secrets sont transférés par canal séparé ;
15. les comptes administrateurs clients sont vérifiés ;
16. les accès LogicGate peuvent être retirés ;
17. un backup final peut être produit ;
18. le backup possède un manifest et des checksums ;
19. la restauration est documentée ;
20. les runbooks sont transférés ;
21. la CI/CD est documentée ;
22. la maintenance future est attribuée ;
23. la formation peut être consignée ;
24. une recette de transfert existe ;
25. un rapport de transfert peut être produit ;
26. une date effective peut être enregistrée ;
27. une période de transition peut être définie ;
28. la fin d’un service mensuel ne supprime pas automatiquement le site ;
29. la conservation temporaire des données est limitée ;
30. la suppression finale est traçable ;
31. le décommissionnement est explicite ;
32. les quality gates bloquent un transfert incomplet ;
33. aucun secret n’est présent dans les documents ;
34. aucune donnée d’un autre client n’est transférée ;
35. la CI ne touche jamais aux environnements réels pendant les tests.

---

# 77. Décisions validées

La baseline de transfert et clôture est la suivante :

```text
Chaque transfert possède un périmètre explicite.

Les transferts peuvent être fonctionnels, techniques,
opérationnels, partiels ou complets.

Chaque responsabilité possède un propriétaire
après la date effective.

La remise du repository ne transfère pas automatiquement
la responsabilité d’exploitation.

Le projet transféré possède une version,
un commit et des artefacts identifiables.

Les versions SiteFactory et modules sont enregistrées.

Les secrets sont transférés séparément des documents.

Les secrets sont idéalement recréés par le nouvel opérateur.

Les secrets LogicGate sont révoqués après validation.

Les comptes administrateurs sont individuels.

Un compte administrateur client est vérifié
avant retrait des comptes LogicGate.

Le repository est propre, versionné et sans secret.

Les packages nécessaires à la reconstruction restent accessibles.

L’image active et une image utile au rollback sont identifiées.

Le domaine possède un titulaire clairement identifié.

La responsabilité du renouvellement du domaine est explicite.

Les DNS sont inventoriés et testés après transfert.

PostgreSQL, App_Data et Data Protection sont traités séparément.

Un backup final est produit avant la date effective.

Le backup final possède un manifest et des checksums.

La restauration fait partie du transfert complet.

Les runbooks correspondent à l’infrastructure réelle.

Une recette de transfert vérifie les capacités de reprise.

La fin d’un service mensuel ne provoque pas
automatiquement l’arrêt ou la suppression du site.

La fin du service transfère les responsabilités de maintenance,
sécurité, backup et exploitation.

Les accès LogicGate sont retirés après validation de la reprise.

La conservation temporaire des données possède
une finalité et une date de suppression.

La suppression d’un volume ou d’une donnée est explicite.

Le décommissionnement produit un rapport.

Une clôture de service ne laisse aucun fournisseur,
domaine, backup ou environnement sans responsable.

Codex ne peut pas transférer un secret,
supprimer un environnement ou clôturer seul un service.
```

---

# 78. Règles pour Codex

## 78.1 Lecture préalable

Avant toute modification liée au transfert ou à la clôture, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
Conditions explicites du transfert : perimetre, droits, acces, responsabilites, services ouverts ou clos et couts eventuels convenus
../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
../adr/0009-client-customizations-and-extension-points.md
../02-architecture/core-module-boundary.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
18-security-baseline.md
19-testing-strategy.md
20-quality-gates-and-ci.md
21-observability-and-logging.md
22-deployment-and-environments.md
23-backup-and-restore.md
24-operational-runbooks.md
25-upgrade-and-maintenance-strategy.md
26-release-and-versioning.md
27-client-handover-and-service-closure.md
AGENTS.md
```

Ainsi que :

- contrat ou périmètre communiqué ;
- repository ;
- lock file ;
- versions ;
- infrastructure ;
- fournisseurs ;
- backups ;
- comptes ;
- secrets inventoriés ;
- runbooks ;
- incidents ouverts.

---

## 78.2 Ce que Codex peut faire

Codex peut :

- produire un inventaire ;
- préparer une checklist ;
- créer un rapport sans secret ;
- générer un document de transfert ;
- adapter les runbooks ;
- préparer un export de configuration non sensible ;
- préparer un script de vérification ;
- ajouter des tests ;
- détecter des placeholders ;
- détecter des accès à retirer ;
- préparer une matrice de responsabilités ;
- documenter un décommissionnement.

---

## 78.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- transférer un secret ;
- afficher un secret ;
- envoyer un backup ;
- ajouter un tiers comme administrateur ;
- transférer un repository ;
- changer le propriétaire d’un domaine ;
- supprimer un accès réel ;
- supprimer un compte ;
- supprimer une donnée ;
- supprimer un volume ;
- arrêter une production ;
- décommissionner un environnement ;
- clôturer un service ;
- confirmer juridiquement un transfert ;
- décider du propriétaire d’un actif ;
- transférer un compte mutualisé LogicGate ;
- considérer un transfert complet sans restore test ;
- effacer les copies LogicGate sans validation.

---

## 78.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- type de transfert ;
- destinataire ;
- nouvel opérateur ;
- date effective ;
- propriété du repository ;
- propriété du domaine ;
- propriété de l’hébergement ;
- transfert de données ;
- transfert de secrets ;
- durée de conservation ;
- suppression finale ;
- retrait des accès ;
- fermeture d’un fournisseur ;
- arrêt du site ;
- décommissionnement ;
- acceptation avec réserves ;
- clôture du service ;
- communication au client.

---

## 78.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Projet concerné
Type de transfert
Date prévue
Version active
Actifs identifiés
Accès identifiés
Secrets identifiés sans valeur
Responsabilités actuelles
Responsabilités futures
Backup final
Restauration
Runbooks
Formation
Tests réalisés
Risques
Éléments bloquants
Accès LogicGate restant à retirer
Données restant à supprimer
Décisions humaines restantes
```

Codex ne doit pas déclarer un transfert terminé uniquement parce que la documentation a été générée.

---

# 79. Anti-patterns interdits

## 79.1 Archive ZIP sans historique

Le client reçoit uniquement le dossier courant sans repository Git.

---

## 79.2 Secrets dans le document de transfert

```text
POSTGRES_PASSWORD=<EXTERNAL_SECRET>
```

apparaît dans un fichier remis avec le code.

---

## 79.3 Repository sans packages

Le client reçoit le code, mais ne peut pas restaurer les dépendances privées.

---

## 79.4 Version inconnue

Aucun tag, commit ou lock file ne correspond à la production.

---

## 79.5 Domaine au nom du prestataire sans clarification

Le client ne sait pas qui contrôle ou renouvelle le domaine.

---

## 79.6 Compte administrateur LogicGate unique

Le client ne possède aucun compte administrateur autonome.

---

## 79.7 Backup non vérifié

Une archive est remise sans checksum ni procédure de restauration.

---

## 79.8 Compte mutualisé transféré

Le client reçoit des credentials permettant d’accéder à d’autres clients.

---

## 79.9 Fin de service avec suppression immédiate

Les volumes sont supprimés le jour de la fin de maintenance sans transfert validé.

---

## 79.10 Accès LogicGate conservés silencieusement

Des clés SSH ou comptes restent actifs sans date ni finalité.

---

## 79.11 Responsabilité vide

Personne n’est désigné pour :

- domaine ;
- backups ;
- sécurité ;
- mises à jour.

---

## 79.12 Documentation générique non adaptée

Les runbooks contiennent encore :

```text
{ClientSlug}
{Domain}
{DeploymentDirectory}
```

---

## 79.13 Données croisées

Une archive issue d’un VPS mutualisé contient les données de plusieurs clients.

---

## 79.14 Clôture sans preuve

Aucun inventaire, rapport ou accusé de réception n’est conservé.

---

## 79.15 Suppression du repository

Le code est supprimé immédiatement après livraison sans politique d’archivage.

---

# 80. Roadmap d’implémentation

## Phase handover 1 — Inventaire

```text
assets
versions
access
providers
responsibilities
```

---

## Phase handover 2 — Rapport

```text
handover-report
checks
status
known limitations
```

---

## Phase handover 3 — Documentation

```text
technical documentation
functional documentation
runbooks
responsibility matrix
```

---

## Phase handover 4 — Données

```text
final backup
manifest
checksums
secure transfer
restore validation
```

---

## Phase handover 5 — Accès

```text
client accounts
SSH
GitHub
DNS
hosting
registry
monitoring
```

---

## Phase handover 6 — Formation

```text
functional
technical
security
operational exercise
```

---

## Phase handover 7 — Clôture

```text
effective date
LogicGate access removal
temporary retention
data deletion
provider closure
```

---

## Phase handover 8 — Automatisation contrôlée

```text
inventory command
handover check
handover plan
report generation
placeholder validation
```

Toute modification réelle d’accès reste humaine.

---

# 81. Commandes SiteFactory futures

Commandes conceptuelles :

```text
sitefactory handover check
sitefactory handover inventory
sitefactory handover plan
sitefactory handover report
sitefactory closure check
sitefactory closure plan
```

---

## 81.1 `handover check`

Vérifierait :

- version ;
- Git ;
- documentation ;
- backups ;
- restore ;
- comptes ;
- secrets inventoriés ;
- responsabilités ;
- placeholders ;
- incidents ouverts.

---

## 81.2 `handover inventory`

Produirait un inventaire non sensible des actifs.

---

## 81.3 `handover plan`

Produirait les actions de transfert sans les exécuter.

---

## 81.4 `closure check`

Vérifierait qu’aucun actif ne reste sans propriétaire.

---

## 81.5 Interdictions

Ces commandes ne doivent jamais :

- transmettre un secret ;
- retirer un accès ;
- supprimer un volume ;
- transférer un domaine ;
- arrêter une production.

---

# 82. Checklist de préparation du transfert

```text
[ ] Type de transfert
[ ] Date effective
[ ] Destinataire
[ ] Nouvel opérateur
[ ] Version active
[ ] Tag
[ ] Commit
[ ] Lock file
[ ] Inventaire
[ ] Incidents ouverts
[ ] Limitations connues
[ ] Repository propre
[ ] Build vert
[ ] Tests verts
[ ] Scans verts
[ ] Backup récent
```

---

# 83. Checklist des actifs

```text
[ ] Repository
[ ] Historique Git
[ ] Tags
[ ] Releases
[ ] Packages
[ ] Images
[ ] Registry
[ ] Domaine
[ ] DNS
[ ] VPS
[ ] PostgreSQL
[ ] App_Data
[ ] Data Protection
[ ] Backups
[ ] SMTP
[ ] Monitoring
[ ] Documentation
[ ] Runbooks
```

---

# 84. Checklist des accès

```text
[ ] GitHub
[ ] Repository admin
[ ] VPS
[ ] SSH
[ ] Docker
[ ] Registrar
[ ] DNS
[ ] Registry
[ ] SMTP
[ ] Backup storage
[ ] Monitoring
[ ] Application admin
[ ] Providers externes
```

---

# 85. Checklist des responsabilités

```text
[ ] Hébergement
[ ] Facturation VPS
[ ] Domaine
[ ] DNS
[ ] Certificats
[ ] PostgreSQL
[ ] Backups
[ ] Restore drills
[ ] Déploiements
[ ] Migrations
[ ] Monitoring
[ ] Alertes
[ ] Correctifs de sécurité
[ ] Maintenance
[ ] Utilisateurs
[ ] Contenus
[ ] Support
```

---

# 86. Checklist du transfert technique

```text
[ ] Clone du repository réussi
[ ] Restore réussi
[ ] Build Release réussi
[ ] Tests réussis
[ ] Packages disponibles
[ ] Image disponible
[ ] Docker fonctionne
[ ] Configuration non sensible reçue
[ ] Secrets recréés
[ ] Staging déployée
[ ] Health sain
[ ] Smoke tests réussis
```

---

# 87. Checklist du transfert des données

```text
[ ] Backup final
[ ] PostgreSQL
[ ] App_Data
[ ] Data Protection
[ ] Manifest
[ ] Checksums
[ ] Chiffrement
[ ] Canal sécurisé
[ ] Réception confirmée
[ ] Restore test réussi
[ ] Données d’autres clients absentes
```

---

# 88. Checklist de retrait LogicGate

```text
[ ] Compte GitHub
[ ] Clé SSH
[ ] Compte VPS
[ ] Accès DNS
[ ] Accès registrar
[ ] Token registry
[ ] Compte SMTP
[ ] Accès backup
[ ] Monitoring
[ ] Compte administrateur
[ ] Sessions
[ ] Tokens
[ ] Accès temporaires
```

---

# 89. Checklist de clôture du service mensuel

```text
[ ] Date de fin
[ ] Préavis
[ ] Responsabilités après clôture
[ ] Hébergement
[ ] Domaine
[ ] Backups
[ ] Maintenance
[ ] Sécurité
[ ] Monitoring
[ ] Support
[ ] Backup final
[ ] Accès transférés
[ ] Accès LogicGate retirés
[ ] Facturation fournisseurs
[ ] Conservation temporaire
[ ] Date de suppression
[ ] Rapport de clôture
```

---

# 90. Checklist de décommissionnement

```text
[ ] Décision validée
[ ] Backup final
[ ] Transfert confirmé
[ ] DNS traité
[ ] Emails traités
[ ] Conteneurs arrêtés
[ ] Caddy retiré
[ ] Secrets révoqués
[ ] Volumes identifiés
[ ] Suppression explicitement confirmée
[ ] Repository archivé
[ ] Fournisseurs clôturés
[ ] Données supprimées selon politique
[ ] Rapport produit
```

---

# 91. Modèle de matrice de responsabilités

```markdown
# Responsibility matrix

| Responsibility | Current owner | Future owner | Effective date | Evidence |
|---|---|---|---|---|
| Repository | LogicGate | Client | YYYY-MM-DD | Repository transfer |
| Hosting | LogicGate | Client | YYYY-MM-DD | Provider account |
| DNS | LogicGate | Client | YYYY-MM-DD | DNS access test |
| Backups | LogicGate | Client | YYYY-MM-DD | Backup execution |
| Security updates | LogicGate | Client | YYYY-MM-DD | Maintenance acceptance |
| Application users | Client | Client | YYYY-MM-DD | Admin account test |
```

---

# 92. Modèle d’acceptation de transfert

```markdown
# Handover acceptance

## Project

- Client:
- Project:
- Application version:
- Git tag:
- Commit:
- Effective date:

## Assets received

- [ ] Repository
- [ ] Packages or package access
- [ ] Docker images
- [ ] Deployment configuration
- [ ] Database backup
- [ ] App_Data archive
- [ ] Data Protection archive
- [ ] Documentation
- [ ] Runbooks

## Access verified

- [ ] GitHub
- [ ] Hosting
- [ ] SSH
- [ ] DNS
- [ ] Registry
- [ ] Backups
- [ ] Application administration

## Operational validation

- [ ] Build
- [ ] Tests
- [ ] Deployment
- [ ] Health checks
- [ ] Backup
- [ ] Restore
- [ ] Smoke tests

## Responsibilities

The responsibility matrix has been reviewed and accepted.

## Reservations

## Acceptance

- LogicGate representative:
- Client representative:
- Date:
```

---

# 93. Conclusion

Le transfert client et la clôture de service doivent permettre au projet SiteFactory de continuer à fonctionner dans un cadre clair, même lorsque LogicGate ne conserve plus son exploitation.

Ils reposent sur :

```text
inventaire
+ versions
+ accès
+ secrets séparés
+ données vérifiées
+ documentation
+ formation
+ restauration
+ responsabilités
+ retrait des accès
+ preuve de clôture
```

Un transfert réussi ne consiste pas uniquement à remettre des fichiers.

Il doit démontrer que le nouveau responsable peut :

```text
comprendre
→ construire
→ déployer
→ sauvegarder
→ restaurer
→ sécuriser
→ maintenir
→ diagnostiquer
```

> La clôture d’un service ne doit jamais laisser un site sans propriétaire technique, un domaine sans responsable, une sauvegarde sans procédure ou un accès LogicGate actif sans raison.
