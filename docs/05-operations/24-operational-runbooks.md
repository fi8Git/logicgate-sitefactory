# LogicGate SiteFactory — Runbooks opérationnels

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Runbooks opérationnels |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, opérateurs, développeurs, contributeurs, Codex |
| Profil de déploiement initial | `docker-compose-vps` |
| Environnements concernés | Staging, Production |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md`, `../02-architecture/core-module-boundary.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `18-security-baseline.md`, `19-testing-strategy.md`, `20-quality-gates-and-ci.md`, `21-observability-and-logging.md`, `22-deployment-and-environments.md`, `23-backup-and-restore.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Procédures disponibles et procédures futures

Dans le public, les seuls parcours exécutables décrits ici sont les commandes
[validate/plan/generate](../03-generation/initial-generation-cli.md),
le [Migrator explicite Development/Test](../03-generation/generated-explicit-migrator.md)
et le [harnais PostgreSQL de test](../../tests/PostgreSql/README.md).
Le runtime fournit liveness/readiness et des diagnostics structurés sans secret.
Aucun outil ne migre, répare ou restaure automatiquement.

Les runbooks numérotés ci-dessous sont des **procédures cibles d'exploitation
client non livrées** : VPS, SMTP, Identity, jobs, backup/restore et rollback
demandent une implémentation, une autorisation et des preuves propres.
Ils ne décrivent ni des commandes CLI existantes ni une production validée.
Les opérations et ressources Business restent privées.

P03-F1 **OPEN / UNKNOWN** (749 HTTP 200 / 19 HTTP 503 historiques) conserve
son risque accepté avec réserve et son réexamen obligatoire **2026-10-23**.
Aucune campagne verte n'explique rétroactivement ces refus ; CI distante
non validée, aucun incident réel à réparer implicitement.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit les runbooks opérationnels de référence des applications clientes générées avec LogicGate SiteFactory.

Il fournit un cadre commun pour :

- qualifier un incident ;
- diagnostiquer un dysfonctionnement ;
- limiter son impact ;
- restaurer le service ;
- vérifier le retour à la normale ;
- documenter l’intervention ;
- prévenir une répétition.

Il couvre notamment :

- application indisponible ;
- readiness en échec ;
- PostgreSQL indisponible ;
- conteneur en boucle de redémarrage ;
- saturation disque ;
- sauvegarde en échec ;
- restauration ;
- migration en échec ;
- déploiement en échec ;
- rollback ;
- Caddy et HTTPS ;
- certificats ;
- SMTP ;
- formulaire public ;
- Identity ;
- compte administrateur compromis ;
- secret compromis ;
- rate limiting et spam ;
- uploads ;
- médias ;
- fournisseur externe ;
- LLM optionnel ;
- incident de sécurité ;
- perte complète du VPS.

Ce document définit les procédures générales.

Chaque projet client doit générer ou maintenir des runbooks adaptés à :

- ses modules ;
- son infrastructure ;
- ses responsabilités contractuelles ;
- ses fournisseurs ;
- ses objectifs de reprise.

---

## 2. Vision

Un runbook doit permettre à un opérateur compétent d’intervenir sans devoir reconstruire la procédure pendant l’incident.

Le flux de référence est :

```text
Détection
→ qualification
→ limitation de l’impact
→ diagnostic
→ correction ou contournement
→ vérification
→ surveillance
→ compte-rendu
→ prévention
```

Une intervention ne doit pas se limiter à :

```text
redémarrer jusqu’à ce que cela fonctionne
```

Elle doit permettre de répondre aux questions suivantes :

```text
Quel est l’impact ?

Depuis quand l’incident existe-t-il ?

Quelle version est déployée ?

Qu’est-ce qui a changé récemment ?

Quelles données sont potentiellement affectées ?

Existe-t-il une sauvegarde valide ?

Le problème est-il applicatif, infrastructurel ou externe ?

Quelle action est la moins risquée ?

Comment vérifier le retour à la normale ?
```

---

## 3. Principes fondamentaux

## 3.1 Préserver les données avant d’agir

Avant une opération risquée, il faut déterminer si un backup de l’état actuel est nécessaire.

Cela concerne notamment :

- migration ;
- restauration ;
- suppression ;
- remplacement de volume ;
- correction SQL ;
- rollback de données ;
- import ;
- nettoyage manuel.

---

## 3.2 Ne pas aggraver un incident

Une intervention ne doit pas :

- supprimer des volumes sans nécessité ;
- modifier directement un conteneur ;
- appliquer une migration non inspectée ;
- exposer PostgreSQL ;
- désactiver globalement une protection ;
- effacer des logs utiles ;
- remplacer une sauvegarde valide ;
- déployer plusieurs corrections simultanément ;
- multiplier les redémarrages sans diagnostic.

---

## 3.3 Une action à la fois

Pendant un incident :

```text
une hypothèse
→ une action
→ une observation
```

Plusieurs modifications simultanées rendent difficile l’identification de la cause.

---

## 3.4 La production reste identifiable

Avant toute intervention, relever :

```text
Client
Environnement
Domaine
Version applicative
Commit
Image tag
Image digest éventuel
Dernière migration
Heure UTC
Opérateur
```

---

## 3.5 Les secrets restent secrets

Un runbook ne doit jamais demander d’afficher :

- mot de passe ;
- token ;
- chaîne de connexion ;
- clé privée ;
- cookie ;
- fichier `.env` complet ;
- variable d’environnement complète.

Les commandes de diagnostic doivent éviter :

```bash
env
printenv
cat .env.production
docker inspect
```

sans filtrage précis.

---

## 3.6 La source de vérité est privilégiée

Les modifications durables doivent être reportées dans :

- repository ;
- configuration ;
- scripts ;
- documentation ;
- migrations ;
- image versionnée.

Une correction uniquement appliquée sur le serveur crée une dérive.

---

## 3.7 Le rollback n’est pas toujours la restauration

Un rollback applicatif revient à une image précédente.

Une restauration remet des données depuis un backup.

Les deux opérations sont distinctes.

---

## 3.8 Un incident clos doit être vérifié

Le retour à la normale exige :

- health checks ;
- smoke tests ;
- logs ;
- parcours affecté ;
- surveillance après correction.

---

# 4. Non-objectifs

Ce document ne remplace pas :

- une analyse de sécurité approfondie ;
- un plan de continuité complet ;
- un contrat de support ;
- un SLA ;
- une procédure légale ;
- un pentest ;
- la documentation d’un fournisseur externe ;
- une expertise PostgreSQL avancée ;
- une procédure spécifique à un secteur réglementé.

---

# 5. Structure standard d’un runbook

Chaque runbook doit utiliser la structure suivante :

```text
Identifiant
Titre
Objectif
Déclencheurs
Impact potentiel
Préconditions
Informations à collecter
Diagnostic
Actions de limitation
Procédure de résolution
Vérifications
Rollback
Escalade
Compte-rendu
Prévention
```

---

## 5.1 Identifiant

Format recommandé :

```text
RB-{DOMAIN}-{NUMBER}
```

Exemples :

```text
RB-APP-001
RB-DB-001
RB-BACKUP-001
RB-SECURITY-001
```

---

## 5.2 Statut

Un runbook possède un statut :

```text
draft
validated
deprecated
retired
```

Seuls les runbooks `validated` doivent être présentés comme procédures opérationnelles de référence.

---

## 5.3 Dernier test

Chaque runbook doit indiquer :

```text
Dernière validation
Dernier exercice
Environnement testé
Résultat
Prochaine revue
```

---

# 6. Niveaux de sévérité

## 6.1 `SEV-1` — Critique

Exemples :

- site entièrement indisponible ;
- perte ou corruption probable de données ;
- compromission active ;
- exposition de secrets ;
- accès non autorisé confirmé ;
- PostgreSQL indisponible sans reprise rapide ;
- backup et production simultanément menacés.

Attentes :

```text
Prise en charge immédiate
Limitation prioritaire de l’impact
Communication adaptée
Suivi continu jusqu’à stabilisation
Post-mortem obligatoire
```

---

## 6.2 `SEV-2` — Majeur

Exemples :

- fonctionnalité essentielle indisponible ;
- formulaires non enregistrés ;
- administration inaccessible ;
- SMTP durablement indisponible ;
- readiness dégradé ;
- taux d’erreur important.

Attentes :

```text
Prise en charge rapide
Contournement ou correction prioritaire
Suivi jusqu’à validation
Compte-rendu
```

---

## 6.3 `SEV-3` — Modéré

Exemples :

- fonctionnalité secondaire indisponible ;
- erreur affectant un sous-ensemble d’utilisateurs ;
- backup ponctuellement échoué mais précédent encore valide ;
- avertissement disque ;
- intégration optionnelle en échec.

---

## 6.4 `SEV-4` — Mineur

Exemples :

- défaut cosmétique ;
- warning non bloquant ;
- erreur de contenu limitée ;
- métrique absente ;
- message d’erreur imprécis.

---

# 7. Informations initiales à collecter

Avant toute action :

```text
Client :
Environnement :
Domaine :
Date et heure UTC :
Heure locale :
Opérateur :
Sévérité :
Incident ID :
Version :
Commit :
Image :
Dernière migration :
Dernier déploiement :
Dernier backup réussi :
Correlation ID éventuel :
Première observation :
Impact utilisateur :
```

---

# 8. Commandes de diagnostic de base

Les commandes suivantes sont indicatives.

Elles doivent être adaptées au nom du projet Compose et au répertoire du client.

## 8.1 État Compose

```bash
docker compose ps
```

---

## 8.2 Logs récents

```bash
docker compose logs \
  --since 15m \
  --tail 300 \
  web
```

---

## 8.3 Logs PostgreSQL

```bash
docker compose logs \
  --since 15m \
  --tail 200 \
  postgres
```

---

## 8.4 Logs Caddy

```bash
docker compose logs \
  --since 15m \
  --tail 200 \
  caddy
```

Pour un Caddy mutualisé, utiliser la stack proxy correspondante.

---

## 8.5 Health checks

```bash
curl --fail --silent --show-error \
  https://client.fr/health/live
```

```bash
curl --fail --silent --show-error \
  https://client.fr/health/ready
```

---

## 8.6 Espace disque

```bash
df -h
```

```bash
docker system df
```

---

## 8.7 État des volumes

```bash
docker volume ls
```

Ne jamais supprimer un volume uniquement sur la base de cette liste.

---

## 8.8 Configuration Compose résolue

```bash
docker compose config
```

La sortie ne doit pas être publiée si elle contient des secrets résolus.

Utiliser une validation filtrée ou un environnement de test lorsque nécessaire.

---

# 9. Runbook — Application entièrement indisponible

## 9.1 Identifiant

```text
RB-APP-001
```

---

## 9.2 Déclencheurs

- domaine inaccessible ;
- erreur `502` ou `503` ;
- health check inaccessible ;
- conteneur web arrêté ;
- alerte de disponibilité ;
- signalement client confirmé.

---

## 9.3 Impact potentiel

```text
Site public indisponible
Administration indisponible
Formulaires indisponibles
Perte de demandes potentielles
```

---

## 9.4 Diagnostic

1. Vérifier la résolution DNS.
2. Vérifier HTTPS.
3. Vérifier Caddy.
4. Vérifier le conteneur web.
5. Vérifier `/health/live`.
6. Vérifier `/health/ready`.
7. Vérifier PostgreSQL.
8. Vérifier l’espace disque.
9. Vérifier les logs du dernier quart d’heure.
10. Vérifier le dernier déploiement ou changement.

---

## 9.5 Arbre de décision

```text
DNS invalide
→ corriger DNS ou incident fournisseur

Caddy arrêté
→ diagnostiquer Caddy

Web arrêté
→ diagnostiquer le conteneur

Liveness en échec
→ problème processus ou démarrage

Liveness sain + readiness en échec
→ dépendance essentielle indisponible

Tous les services sains + domaine inaccessible
→ réseau, firewall, certificat ou reverse proxy
```

---

## 9.6 Action de limitation

Pour un incident prolongé, envisager :

- page de maintenance ;
- information client ;
- arrêt des déploiements concurrents ;
- gel des migrations ;
- maintien des logs et preuves.

---

## 9.7 Résolution

La résolution dépend de la cause :

- redémarrage contrôlé d’un service ;
- rollback applicatif ;
- restauration ;
- correction Caddy ;
- libération d’espace ;
- correction de secret ;
- correction DNS ;
- redémarrage PostgreSQL ;
- remplacement du VPS.

---

## 9.8 Vérifications

```text
HTTP redirige vers HTTPS
Certificat valide
Accueil retourne 200
Liveness sain
Readiness sain
Admin protégée
Login accessible
Formulaire fonctionnel
Logs sans erreur critique
```

---

# 10. Runbook — Liveness en échec

## 10.1 Identifiant

```text
RB-APP-002
```

---

## 10.2 Signification

Le processus applicatif n’est pas considéré comme vivant.

Causes possibles :

- crash ;
- erreur au démarrage ;
- secret essentiel absent ;
- exception de configuration ;
- runtime incompatible ;
- image invalide ;
- volume requis inaccessible ;
- boucle de redémarrage.

---

## 10.3 Diagnostic

```bash
docker compose ps web
```

```bash
docker compose logs \
  --tail 300 \
  web
```

Vérifier :

- code de sortie ;
- nombre de redémarrages ;
- version de l’image ;
- message de démarrage ;
- validation des options ;
- accès aux volumes.

---

## 10.4 Actions

1. Ne pas relancer immédiatement en boucle.
2. Identifier la première erreur de démarrage.
3. Vérifier la présence des secrets requis sans afficher leur valeur.
4. Vérifier les volumes.
5. Vérifier la version déployée.
6. Vérifier la compatibilité de la migration.
7. Corriger ou rollback.

---

## 10.5 Critère de résolution

```text
Conteneur stable
Liveness sain
Aucun redémarrage répété
Logs de démarrage normaux
```

---

# 11. Runbook — Readiness en échec

## 11.1 Identifiant

```text
RB-APP-003
```

---

## 11.2 Signification

L’application fonctionne, mais une dépendance essentielle empêche de servir correctement le trafic.

Causes possibles :

- PostgreSQL indisponible ;
- App_Data inaccessible ;
- Data Protection inaccessible ;
- migration manquante ;
- configuration essentielle invalide.

---

## 11.3 Diagnostic

1. Vérifier le détail du diagnostic protégé.
2. Vérifier PostgreSQL.
3. Vérifier le montage d’App_Data.
4. Vérifier Data Protection.
5. Vérifier la migration attendue.
6. Vérifier les permissions filesystem.
7. Vérifier le dernier déploiement.

---

## 11.4 Actions

- corriger la dépendance ;
- restaurer le volume ;
- appliquer explicitement la migration attendue ;
- corriger les permissions ;
- rollback de l’application si le schéma est incompatible.

---

## 11.5 Interdiction

Ne pas modifier le readiness pour qu’il retourne artificiellement `Healthy`.

---

# 12. Runbook — Conteneur en boucle de redémarrage

## 12.1 Identifiant

```text
RB-DOCKER-001
```

---

## 12.2 Déclencheurs

- compteur de redémarrages élevé ;
- application inaccessible ;
- logs de démarrage répétés ;
- restart policy active sans stabilisation.

---

## 12.3 Diagnostic

```bash
docker compose ps
```

```bash
docker inspect <container-id>
```

Ne pas publier la sortie complète de `docker inspect` si elle contient des variables sensibles.

Vérifier :

- `State.ExitCode` ;
- `State.Error` ;
- `RestartCount` ;
- health ;
- image ;
- mounts.

---

## 12.4 Actions

1. Arrêter temporairement le service si la boucle sature le serveur.
2. Conserver les logs.
3. Identifier la première erreur.
4. Vérifier la configuration.
5. Vérifier l’image.
6. Vérifier les volumes.
7. Vérifier PostgreSQL.
8. Corriger ou revenir à l’image précédente.

---

## 12.5 Vérification

Le conteneur doit rester actif sans redémarrage pendant une période de stabilisation.

---

# 13. Runbook — PostgreSQL indisponible

## 13.1 Identifiant

```text
RB-DB-001
```

---

## 13.2 Déclencheurs

- readiness en échec ;
- erreurs de connexion ;
- conteneur PostgreSQL arrêté ;
- timeout base ;
- formulaire non persisté ;
- administration sans données.

---

## 13.3 Diagnostic

```bash
docker compose ps postgres
```

```bash
docker compose logs \
  --since 30m \
  --tail 300 \
  postgres
```

Vérifier :

- conteneur ;
- espace disque ;
- volume ;
- permissions ;
- mémoire ;
- corruption ;
- configuration ;
- nombre de connexions ;
- version.

---

## 13.4 Causes fréquentes

```text
Disque plein
Volume non monté
Mot de passe modifié sans synchronisation
Conteneur arrêté
OOM
Version PostgreSQL incompatible
Permissions du volume
Corruption
```

---

## 13.5 Actions

### Conteneur arrêté sans erreur de données

- corriger la configuration ;
- démarrer PostgreSQL ;
- vérifier les logs ;
- vérifier readiness.

### Disque plein

- appliquer `RB-SYSTEM-001`.

### Volume absent

- ne pas recréer immédiatement une base vide ;
- identifier le volume attendu ;
- vérifier les mounts ;
- restaurer si le volume est perdu.

### Corruption suspectée

- arrêter les écritures ;
- préserver l’état ;
- préparer une restauration ;
- éviter les corrections improvisées sur la production.

---

## 13.6 Vérifications

```text
PostgreSQL healthy
Connexion applicative
Migration attendue
Lecture
Écriture de contrôle
Readiness sain
Formulaire fonctionnel
```

---

# 14. Runbook — Migration requise

## 14.1 Identifiant

```text
RB-DB-002
```

---

## 14.2 Déclencheurs

- événement `DatabaseMigrationRequired` ;
- readiness dégradé ;
- erreur de table ou colonne absente ;
- nouvelle version déployée avant migration.

---

## 14.3 Actions

1. Identifier la version applicative active.
2. Identifier la migration attendue.
3. Vérifier que la migration appartient à la même release.
4. Inspecter la migration.
5. Réaliser un backup.
6. Appliquer la migration explicitement.
7. Vérifier l’historique.
8. Vérifier readiness.
9. Exécuter les smoke tests.

---

## 14.4 Interdiction

Ne pas activer l’auto-migration pour corriger l’incident.

---

# 15. Runbook — Migration en échec

## 15.1 Identifiant

```text
RB-DB-003
```

---

## 15.2 Déclencheurs

- commande de migration non nulle ;
- schéma partiellement modifié ;
- erreur SQL ;
- timeout ;
- conflit de données ;
- violation de contrainte.

---

## 15.3 Actions immédiates

1. Interrompre le déploiement.
2. Maintenir ou activer le mode maintenance.
3. Ne pas relancer automatiquement.
4. Conserver les logs de migration.
5. Identifier si la transaction a rollback.
6. Vérifier l’historique EF Core.
7. Vérifier l’état réel du schéma.
8. Vérifier le backup pré-migration.

---

## 15.4 Décision

```text
Migration totalement rollback
→ corriger puis retester

Migration partiellement appliquée
→ plan de correction ou restauration

Données transformées irréversiblement
→ restauration probablement nécessaire
```

---

## 15.5 Vérifications après correction

- schéma cohérent ;
- historique EF Core cohérent ;
- version applicative compatible ;
- readiness sain ;
- smoke tests ;
- données principales vérifiées.

---

# 16. Runbook — Déploiement en échec

## 16.1 Identifiant

```text
RB-DEPLOY-001
```

---

## 16.2 Déclencheurs

- image indisponible ;
- Compose invalide ;
- conteneur non démarré ;
- health en échec ;
- smoke tests en échec ;
- secret absent ;
- migration échouée.

---

## 16.3 Diagnostic

1. Identifier la dernière étape réussie.
2. Vérifier la version réellement active.
3. Vérifier si la migration a été appliquée.
4. Vérifier l’image précédente.
5. Vérifier le backup.
6. Vérifier les conteneurs.
7. Vérifier les health checks.
8. Vérifier les logs.

---

## 16.4 Décision de rollback

Un rollback est recommandé si :

- la cause n’est pas rapidement identifiable ;
- le service est indisponible ;
- les smoke tests essentiels échouent ;
- une régression de sécurité existe ;
- la version précédente reste compatible.

---

## 16.5 Rapport

Le rapport doit indiquer :

```text
Étape en échec
Version active
Version cible
Migration
Backup
Rollback exécuté ou non
Résultat
```

---

# 17. Runbook — Rollback applicatif

## 17.1 Identifiant

```text
RB-DEPLOY-002
```

---

## 17.2 Préconditions

```text
Image précédente disponible
Version précédente identifiée
Compatibilité avec la base vérifiée
Configuration précédente disponible
Aucune migration irréversible incompatible
```

---

## 17.3 Procédure

1. Consigner l’incident.
2. Vérifier l’état de la base.
3. Modifier la référence d’image.
4. Recréer le service web.
5. Attendre liveness.
6. Attendre readiness.
7. Exécuter les smoke tests.
8. Vérifier les logs.
9. Surveiller.
10. Documenter le rollback.

---

## 17.4 Limite

Le rollback applicatif ne restaure pas les données.

---

# 18. Runbook — Restauration de données

## 18.1 Identifiant

```text
RB-RESTORE-001
```

---

## 18.2 Référence

La procédure complète est définie dans :

```text
23-backup-and-restore.md
```

---

## 18.3 Actions essentielles

1. Qualifier l’incident.
2. Identifier le backup.
3. Vérifier le manifest.
4. Vérifier les checksums.
5. Identifier la version compatible.
6. Arrêter les écritures.
7. Sauvegarder l’état actuel si possible.
8. Restaurer dans un environnement isolé lorsque possible.
9. Vérifier.
10. Restaurer la cible.
11. Exécuter les smoke tests.
12. Réappliquer les révocations et suppressions.
13. Produire le rapport.

---

# 19. Runbook — Saturation disque

## 19.1 Identifiant

```text
RB-SYSTEM-001
```

---

## 19.2 Déclencheurs

- seuil warning ;
- seuil critique ;
- PostgreSQL en erreur ;
- backup échoué ;
- Docker impossible à démarrer ;
- filesystem en lecture seule ;
- logs non écrits.

---

## 19.3 Diagnostic

```bash
df -h
```

```bash
du -x -h /opt/logicgate \
  | sort -h \
  | tail
```

```bash
docker system df
```

Identifier la croissance de :

- PostgreSQL ;
- App_Data ;
- backups ;
- logs Docker ;
- logs Caddy ;
- images ;
- caches ;
- fichiers temporaires.

---

## 19.4 Actions sûres prioritaires

- arrêter une génération ou un backup non essentiel ;
- purger des fichiers temporaires identifiés ;
- appliquer la rotation prévue ;
- supprimer des images non utilisées après vérification ;
- transférer des backups validés ;
- augmenter le disque si nécessaire.

---

## 19.5 Actions interdites

```text
Supprimer un volume inconnu

Supprimer le dernier backup valide

Supprimer les fichiers PostgreSQL manuellement

Lancer docker system prune --volumes sans analyse

Supprimer App_Data
```

---

## 19.6 Vérifications

- espace libre suffisant ;
- PostgreSQL sain ;
- backup fonctionnel ;
- conteneurs stables ;
- logs repris ;
- cause de croissance corrigée.

---

# 20. Runbook — Backup en échec

## 20.1 Identifiant

```text
RB-BACKUP-001
```

---

## 20.2 Déclencheurs

- code non nul ;
- manifest `failed` ;
- copie hors VPS échouée ;
- checksum invalide ;
- backup trop ancien ;
- fichier vide.

---

## 20.3 Diagnostic

Vérifier :

- espace disque ;
- permissions ;
- PostgreSQL ;
- version `pg_dump` ;
- répertoire ;
- verrouillage ;
- réseau ;
- credentials de destination ;
- taille anormale ;
- logs.

---

## 20.4 Actions

1. Conserver les backups précédents.
2. Ne pas lancer la purge.
3. Corriger la cause.
4. Relancer une fois la précondition vérifiée.
5. Vérifier localement.
6. Vérifier la copie hors VPS.
7. Vérifier le manifest.
8. Mettre à jour l’alerte.

---

## 20.5 Escalade

Passer en sévérité supérieure si :

- aucun backup récent ;
- plusieurs échecs consécutifs ;
- production à risque ;
- migration prévue ;
- copie hors VPS absente.

---

# 21. Runbook — Restore drill en échec

## 21.1 Identifiant

```text
RB-BACKUP-002
```

---

## 21.2 Signification

Une sauvegarde existe mais ne permet pas de reconstruire correctement le service.

---

## 21.3 Actions

1. Conserver tous les artefacts.
2. Identifier le composant en échec.
3. Vérifier les versions.
4. Vérifier les checksums.
5. Vérifier les secrets et clés.
6. Corriger le processus.
7. Créer une nouvelle sauvegarde si nécessaire.
8. Refaire le drill.
9. Considérer les backups similaires comme potentiellement affectés.
10. Documenter l’écart au RPO/RTO.

---

# 22. Runbook — Caddy indisponible

## 22.1 Identifiant

```text
RB-CADDY-001
```

---

## 22.2 Déclencheurs

- HTTP/HTTPS indisponible ;
- erreur `502` ;
- conteneur Caddy arrêté ;
- configuration invalide ;
- backend inaccessible.

---

## 22.3 Diagnostic

```bash
docker compose ps caddy
```

```bash
docker compose logs \
  --tail 300 \
  caddy
```

Vérifier :

- ports ;
- configuration ;
- réseau ;
- domaine ;
- backend ;
- certificat ;
- espace disque.

---

## 22.4 Actions

- valider la configuration avant reload ;
- restaurer la dernière configuration valide ;
- redémarrer Caddy ;
- vérifier le réseau proxy ;
- vérifier le service web ;
- rollback du changement Caddy.

---

## 22.5 Vérifications

```text
HTTP vers HTTPS
Certificat
Accueil
Headers
Backend correct
Autres clients non affectés si Caddy mutualisé
```

---

# 23. Runbook — Certificat HTTPS en erreur

## 23.1 Identifiant

```text
RB-CADDY-002
```

---

## 23.2 Causes possibles

- DNS incorrect ;
- ports fermés ;
- domaine non propagé ;
- quota ACME ;
- Caddy sans accès au stockage ;
- horloge système incorrecte ;
- configuration invalide.

---

## 23.3 Diagnostic

1. Vérifier DNS.
2. Vérifier ports 80 et 443.
3. Vérifier l’heure serveur.
4. Vérifier les logs Caddy.
5. Vérifier l’accès aux volumes Caddy.
6. Vérifier la configuration du domaine.
7. Vérifier l’absence d’un autre service sur les ports.

---

## 23.4 Actions

- corriger DNS ;
- corriger firewall ;
- restaurer les volumes ;
- corriger le Caddyfile ;
- relancer une demande après correction ;
- éviter les tentatives répétées non contrôlées.

---

# 24. Runbook — SMTP indisponible

## 24.1 Identifiant

```text
RB-EMAIL-001
```

---

## 24.2 Impact

Selon l’architecture :

```text
Lead persisté
Email non envoyé
```

L’application peut rester opérationnelle mais dégradée.

---

## 24.3 Diagnostic

Vérifier :

- configuration SMTP ;
- présence du secret sans afficher sa valeur ;
- DNS ;
- port ;
- TLS ;
- credentials ;
- quota ;
- blocage fournisseur ;
- logs d’envoi ;
- taux d’échec.

---

## 24.4 Actions

- activer un kill switch d’envoi si doubles envois ;
- corriger la configuration ;
- renouveler le secret si compromis ;
- basculer vers un provider approuvé ;
- traiter les notifications en attente si un mécanisme existe ;
- ne pas renvoyer en masse sans idempotence.

---

## 24.5 Vérifications

- envoi de test ;
- lead toujours persisté ;
- aucune duplication ;
- logs sans adresse ou corps ;
- retour à un taux normal.

---

# 25. Runbook — Formulaire public non fonctionnel

## 25.1 Identifiant

```text
RB-FORM-001
```

---

## 25.2 Déclencheurs

- erreur utilisateur ;
- aucun lead enregistré ;
- erreur 500 ;
- antiforgery ;
- rate limit inattendu ;
- spam massif ;
- base inaccessible.

---

## 25.3 Diagnostic

1. Reproduire avec une donnée de test.
2. Relever correlation ID.
3. Vérifier la réponse HTTP.
4. Vérifier `PublicFormSubmissionAccepted` ou `Rejected`.
5. Vérifier `LeadPersisted`.
6. Vérifier PostgreSQL.
7. Vérifier SMTP séparément.
8. Vérifier rate limiting.
9. Vérifier honeypot.
10. Vérifier la configuration antiforgery.

---

## 25.4 Scénarios

```text
Validation refusée
→ vérifier les règles et le message

Persistance échouée
→ incident base

Persistance réussie + email échoué
→ incident SMTP, lead conservé

429
→ rate limiting ou abus

Antiforgery
→ configuration proxy, cookie ou rendu
```

---

## 25.5 Vérifications

- soumission valide ;
- validation invalide correctement refusée ;
- lead visible dans l’administration ;
- email contrôlé ;
- aucune PII dans les logs.

---

# 26. Runbook — Spam ou abus de formulaire

## 26.1 Identifiant

```text
RB-FORM-002
```

---

## 26.2 Déclencheurs

- volume inhabituel ;
- nombreuses soumissions invalides ;
- honeypot déclenché ;
- rate limits fréquents ;
- augmentation des coûts SMTP.

---

## 26.3 Actions graduées

1. Mesurer le volume.
2. Vérifier l’origine sans collecter excessivement.
3. Ajuster le rate limiting.
4. Vérifier le honeypot.
5. Bloquer temporairement un motif évident.
6. Activer un CAPTCHA approuvé si nécessaire.
7. Désactiver temporairement une automatisation secondaire.
8. Préserver les soumissions légitimes.

---

## 26.4 Interdiction

Ne pas désactiver entièrement la validation ou publier une liste d’IP personnelles sans politique.

---

# 27. Runbook — Administration inaccessible

## 27.1 Identifiant

```text
RB-IDENTITY-001
```

---

## 27.2 Causes possibles

- compte verrouillé ;
- compte désactivé ;
- dernier administrateur absent ;
- mot de passe oublié ;
- Data Protection perdue ;
- cookie invalide ;
- migration Identity manquante ;
- base indisponible.

---

## 27.3 Diagnostic

1. Vérifier l’application et PostgreSQL.
2. Vérifier les logs Identity sans email.
3. Vérifier le compte administrateur par identifiant technique.
4. Vérifier son état actif.
5. Vérifier le lockout.
6. Vérifier les rôles.
7. Vérifier Data Protection.
8. Vérifier les migrations.

---

## 27.4 Récupération contrôlée

Pour réinitialiser un administrateur :

1. préparer une procédure opérateur ;
2. activer temporairement le reset du seed ;
3. fournir le secret par canal sécurisé ;
4. recréer le conteneur ;
5. vérifier la connexion ;
6. désactiver le reset ;
7. retirer le mot de passe ;
8. recréer le conteneur ;
9. invalider les anciennes sessions si nécessaire.

---

# 28. Runbook — Dernier administrateur désactivé

## 28.1 Identifiant

```text
RB-IDENTITY-002
```

---

## 28.2 Prévention

Le système doit normalement empêcher cet état.

---

## 28.3 Récupération

- utiliser la procédure de récupération AdminSeed ;
- ne pas créer un endpoint public ;
- vérifier les rôles et permissions ;
- corriger la règle ayant permis l’incident ;
- ajouter un test de régression.

---

# 29. Runbook — Compte administrateur compromis

## 29.1 Identifiant

```text
RB-SECURITY-001
```

---

## 29.2 Sévérité

```text
SEV-1 ou SEV-2
```

selon l’accès et l’activité observée.

---

## 29.3 Actions immédiates

1. Désactiver le compte.
2. Invalider ses sessions.
3. Retirer ses rôles.
4. Changer son mot de passe.
5. Identifier les actions récentes.
6. Vérifier les autres comptes.
7. Vérifier les secrets accessibles.
8. Renouveler les secrets potentiellement exposés.
9. Préserver les logs et audits.
10. Vérifier les contenus, exports et suppressions.

---

## 29.4 Vérifications

- aucun accès persistant ;
- aucun compte supplémentaire ;
- aucune permission ajoutée ;
- aucune automatisation activée ;
- aucun export anormal ;
- aucun secret encore valide.

---

## 29.5 Post-mortem

Obligatoire.

---

# 30. Runbook — Secret compromis

## 30.1 Identifiant

```text
RB-SECURITY-002
```

---

## 30.2 Secrets concernés

- PostgreSQL ;
- SMTP ;
- registry ;
- GitHub ;
- SSH ;
- API ;
- LLM ;
- certificat ;
- stockage backup.

---

## 30.3 Actions immédiates

1. Identifier le secret.
2. Identifier les environnements affectés.
3. Révoquer ou remplacer.
4. Mettre à jour la configuration.
5. Recréer les services concernés.
6. Vérifier le fonctionnement.
7. Vérifier les accès et logs.
8. Supprimer le secret des artefacts.
9. Nettoyer l’historique si nécessaire.
10. analyser l’usage malveillant éventuel.

---

## 30.4 Interdiction

Supprimer uniquement le secret du dernier commit ne suffit pas.

---

# 31. Runbook — Secret détecté dans Git

## 31.1 Identifiant

```text
RB-SECURITY-003
```

---

## 31.2 Actions

1. Considérer le secret comme compromis.
2. Le révoquer.
3. Bloquer les déploiements concernés.
4. Identifier les commits et artefacts.
5. Corriger les fichiers.
6. Nettoyer l’historique si nécessaire.
7. Vérifier les forks, caches et artefacts.
8. Réexécuter le secret scan.
9. Documenter.
10. ajouter une règle de prévention.

---

# 32. Runbook — Suspicion de fuite de données

## 32.1 Identifiant

```text
RB-SECURITY-004
```

---

## 32.2 Sévérité

Potentiellement `SEV-1`.

---

## 32.3 Actions

1. Limiter l’accès affecté.
2. Préserver les éléments d’analyse.
3. Identifier les données concernées.
4. Identifier la période.
5. Identifier la cause.
6. Désactiver le compte, endpoint ou intégration.
7. Renouveler les secrets.
8. Vérifier les backups et logs.
9. Corriger.
10. évaluer les obligations de notification et de communication.

---

## 32.4 Interdiction

Ne pas supprimer les preuves avant l’analyse.

---

# 33. Runbook — Vulnérabilité critique d’une dépendance

## 33.1 Identifiant

```text
RB-SECURITY-005
```

---

## 33.2 Actions

1. Identifier les versions affectées.
2. Identifier les projets clients concernés.
3. Évaluer l’exploitabilité réelle.
4. Désactiver la fonctionnalité si nécessaire.
5. Préparer un correctif.
6. Exécuter build, tests et scans.
7. publier une nouvelle version.
8. planifier les upgrades clients.
9. vérifier les productions.
10. documenter.

---

## 33.3 Gate

Une nouvelle release affectée reste bloquée sans mitigation formelle.

---

# 34. Runbook — Rate limiting excessif

## 34.1 Identifiant

```text
RB-SECURITY-006
```

---

## 34.2 Symptômes

- utilisateurs légitimes reçoivent `429` ;
- login fréquemment bloqué ;
- formulaire inutilisable ;
- trafic derrière une IP partagée.

---

## 34.3 Diagnostic

- politique concernée ;
- clé utilisée ;
- volume ;
- durée ;
- IP partagée ;
- utilisateur ;
- erreur de proxy ;
- forwarded headers ;
- modification récente.

---

## 34.4 Actions

- corriger la clé ;
- ajuster le seuil ;
- préserver la protection ;
- ajouter une politique par utilisateur ;
- vérifier les proxys connus ;
- tester avant production.

---

# 35. Runbook — Upload malveillant ou invalide

## 35.1 Identifiant

```text
RB-FILE-001
```

---

## 35.2 Déclencheurs

- fichier exécutable ;
- MIME incohérent ;
- fichier volumineux ;
- fichier corrompu ;
- contenu suspect ;
- path traversal ;
- antivirus positif.

---

## 35.3 Actions

1. Refuser le fichier.
2. Ne pas l’exécuter.
3. Isoler l’artefact si une analyse est nécessaire.
4. Identifier l’utilisateur technique.
5. Vérifier les autres uploads.
6. vérifier les règles.
7. supprimer de manière contrôlée.
8. analyser une éventuelle exploitation.

---

## 35.4 Logs

Ne pas logguer le contenu du fichier.

---

# 36. Runbook — Média absent ou corrompu

## 36.1 Identifiant

```text
RB-FILE-002
```

---

## 36.2 Diagnostic

- métadonnée en base ;
- fichier sur volume ;
- permissions ;
- backup ;
- lien ;
- type ;
- taille ;
- changement récent.

---

## 36.3 Actions

- restaurer le média depuis le backup ;
- corriger les permissions ;
- régénérer une miniature ;
- réconcilier base et stockage ;
- vérifier les autres médias.

---

# 37. Runbook — Provider LLM indisponible

## 37.1 Identifiant

```text
RB-LLM-001
```

---

## 37.2 Impact

L’intégration LLM est optionnelle.

L’application principale doit rester disponible.

---

## 37.3 Diagnostic

- kill switch ;
- provider ;
- endpoint ;
- secret ;
- quota ;
- timeout ;
- modèle ;
- réponse invalide ;
- réseau.

---

## 37.4 Actions

- utiliser le fallback manuel ;
- désactiver temporairement la génération ;
- corriger la configuration ;
- rétablir le provider ;
- utiliser un fallback uniquement s’il est approuvé ;
- ne pas transmettre les données à un autre provider implicitement.

---

## 37.5 Vérification

- application principale saine ;
- aucune donnée perdue ;
- aucun brouillon publié ;
- provider de nouveau disponible ;
- coûts maîtrisés.

---

# 38. Runbook — Génération LLM suspecte

## 38.1 Identifiant

```text
RB-LLM-002
```

---

## 38.2 Déclencheurs

- fait inventé ;
- secret reproduit ;
- instruction de source suivie ;
- sortie non conforme ;
- contenu juridique risqué ;
- publication accidentelle.

---

## 38.3 Actions

1. Bloquer la publication.
2. Rejeter le brouillon.
3. Identifier les sources.
4. Vérifier l’exposition de données.
5. désactiver la tâche ou le provider si nécessaire.
6. corriger le prompt ou les contrôles.
7. ajouter un test.
8. vérifier les autres contenus produits par la même version.

---

# 39. Runbook — SEO ou domaine de test en production

## 39.1 Identifiant

```text
RB-SEO-001
```

---

## 39.2 Déclencheurs

- canonical `.test` ;
- sitemap incorrect ;
- staging indexable ;
- robots bloquant production ;
- placeholder publié ;
- domaine de preview.

---

## 39.3 Actions

- corriger la configuration ;
- régénérer ou redéployer ;
- vérifier robots ;
- vérifier sitemap ;
- vérifier canonical ;
- demander une réindexation si nécessaire ;
- contrôler toutes les pages.

---

# 40. Runbook — Staging indexable

## 40.1 Identifiant

```text
RB-SEO-002
```

---

## 40.2 Actions

1. Ajouter `noindex`.
2. Corriger `robots.txt`.
3. retirer le sitemap.
4. protéger l’accès.
5. vérifier les canonicals.
6. demander la suppression des URLs si nécessaire.
7. corriger le template ou profil ayant permis l’incident.
8. ajouter un test.

---

# 41. Runbook — CORS incorrect

## 41.1 Identifiant

```text
RB-SECURITY-007
```

---

## 41.2 Symptômes

- appels navigateur bloqués ;
- origine inattendue autorisée ;
- credentials refusés ;
- exposition potentielle.

---

## 41.3 Actions

- identifier le besoin réel ;
- vérifier l’origine ;
- retirer les wildcards ;
- vérifier les credentials ;
- ne pas utiliser CORS comme autorisation ;
- tester depuis une origine autorisée et interdite.

---

# 42. Runbook — Data Protection perdue

## 42.1 Identifiant

```text
RB-IDENTITY-003
```

---

## 42.2 Symptômes

- déconnexion de tous les utilisateurs ;
- cookies invalides ;
- tokens de reset invalides ;
- erreurs de déchiffrement ;
- nouvelle clé créée après recréation de volume.

---

## 42.3 Diagnostic

- volume monté ;
- contenu du volume ;
- permissions ;
- backup ;
- nom d’application ;
- changement d’environnement.

---

## 42.4 Actions

### Backup disponible

- restaurer les clés ;
- vérifier les permissions ;
- recréer le conteneur ;
- tester.

### Backup indisponible

- accepter l’invalidation des sessions ;
- renouveler les tokens ;
- vérifier l’impact sur les données protégées ;
- corriger la persistance ;
- informer les utilisateurs si nécessaire.

---

# 43. Runbook — Perte du volume App_Data

## 43.1 Identifiant

```text
RB-STORAGE-001
```

---

## 43.2 Actions

1. Arrêter les écritures.
2. Vérifier le volume attendu.
3. Ne pas créer un nouveau volume avec le même nom sans analyse.
4. Identifier le dernier backup.
5. restaurer dans un volume distinct.
6. vérifier les fichiers.
7. basculer l’application.
8. exécuter les smoke tests.
9. vérifier les références en base.

---

# 44. Runbook — Perte complète du VPS

## 44.1 Identifiant

```text
RB-DR-001
```

---

## 44.2 Sévérité

```text
SEV-1
```

---

## 44.3 Préconditions de reprise

- backups hors VPS ;
- repository ;
- image ou capacité de reconstruction ;
- inventaire des secrets ;
- configuration Caddy ;
- DNS ;
- runbooks ;
- accès fournisseur.

---

## 44.4 Procédure

1. Confirmer la perte ou indisponibilité durable.
2. Préserver les informations fournisseur.
3. Créer un nouveau VPS.
4. appliquer le durcissement initial.
5. installer Docker et Compose.
6. restaurer la configuration Caddy.
7. restaurer ou recréer les secrets.
8. récupérer l’image exacte.
9. créer réseaux et volumes.
10. démarrer PostgreSQL.
11. restaurer la base.
12. restaurer App_Data.
13. restaurer Data Protection.
14. démarrer l’application compatible.
15. vérifier liveness et readiness.
16. vérifier DNS.
17. vérifier HTTPS.
18. exécuter les smoke tests.
19. réappliquer les révocations et suppressions.
20. remettre le trafic.
21. surveiller.
22. produire le rapport de reprise.

---

# 45. Runbook — Compromission du VPS

## 45.1 Identifiant

```text
RB-DR-002
```

---

## 45.2 Principe

Ne pas restaurer simplement sur le serveur compromis.

---

## 45.3 Actions

1. Isoler le serveur.
2. préserver les preuves.
3. révoquer les secrets.
4. créer une infrastructure propre.
5. vérifier le repository et les images.
6. sélectionner une sauvegarde saine.
7. restaurer.
8. renouveler SSH, DB, SMTP, registry et autres secrets.
9. invalider les sessions.
10. auditer les comptes.
11. surveiller après reprise.
12. produire un post-mortem.

---

# 46. Runbook — DNS incorrect

## 46.1 Identifiant

```text
RB-NETWORK-001
```

---

## 46.2 Symptômes

- domaine ne résout pas ;
- mauvaise IP ;
- certificat impossible ;
- trafic vers ancien serveur ;
- certains utilisateurs voient une ancienne version.

---

## 46.3 Diagnostic

- enregistrements DNS ;
- propagation ;
- TTL ;
- autorité ;
- IP du VPS ;
- Caddy ;
- domaine configuré.

---

## 46.4 Actions

- corriger l’enregistrement ;
- attendre la propagation ;
- conserver l’ancien serveur si possible pendant la transition ;
- vérifier HTTPS après propagation ;
- documenter la bascule.

---

# 47. Runbook — Ressources VPS insuffisantes

## 47.1 Identifiant

```text
RB-SYSTEM-002
```

---

## 47.2 Symptômes

- OOM ;
- redémarrages ;
- latence ;
- PostgreSQL tué ;
- build impossible ;
- swap excessive.

---

## 47.3 Diagnostic

- mémoire ;
- CPU ;
- I/O ;
- disque ;
- conteneurs ;
- nombre de clients ;
- processus ;
- croissance récente.

---

## 47.4 Actions

- arrêter les tâches non essentielles ;
- réduire temporairement une charge ;
- déplacer un build hors VPS ;
- ajouter des limites de ressources ;
- augmenter le VPS ;
- isoler un client ;
- migrer un projet vers un VPS dédié.

---

# 48. Runbook — Logs trop volumineux

## 48.1 Identifiant

```text
RB-OBS-001
```

---

## 48.2 Causes

- niveau Debug ;
- exception répétée ;
- attaque ;
- boucle ;
- rotation absente ;
- logs Caddy trop détaillés ;
- processus en crash loop.

---

## 48.3 Actions

1. Identifier la source.
2. réduire temporairement le niveau si sûr.
3. corriger la cause.
4. activer ou corriger la rotation.
5. conserver un extrait utile.
6. supprimer uniquement les logs expirés.
7. vérifier le disque.
8. ajouter une alerte.

---

## 48.4 Interdiction

Ne pas supprimer les logs critiques nécessaires à un incident de sécurité sans les préserver.

---

# 49. Runbook — Health check public trop détaillé

## 49.1 Identifiant

```text
RB-OBS-002
```

---

## 49.2 Actions

- limiter immédiatement la réponse ;
- protéger le diagnostic détaillé ;
- vérifier les caches ;
- vérifier les logs d’accès ;
- déterminer les informations exposées ;
- corriger ;
- déployer ;
- ajouter un test de régression.

---

# 50. Runbook — Fournisseur externe indisponible

## 50.1 Identifiant

```text
RB-EXTERNAL-001
```

---

## 50.2 Exemples

- SMTP ;
- LLM ;
- stockage ;
- API métier ;
- analytics ;
- paiement futur.

---

## 50.3 Actions

1. Vérifier la portée de l’incident.
2. vérifier le statut du fournisseur.
3. activer un kill switch si nécessaire.
4. préserver l’opération principale.
5. mettre en file ou journaliser une reprise si supportée.
6. utiliser un fallback uniquement s’il est approuvé.
7. surveiller.
8. réexécuter les opérations idempotentes après retour.

---

# 51. Runbook — Erreur après changement de configuration

## 51.1 Identifiant

```text
RB-CONFIG-001
```

---

## 51.2 Diagnostic

- différence de configuration ;
- valeur précédente ;
- conteneur réellement recréé ;
- option validée ;
- secret ;
- environnement ;
- logs de démarrage.

---

## 51.3 Actions

- revenir à la configuration précédente ;
- recréer le service ;
- vérifier ;
- corriger la configuration dans le repository ou runbook ;
- éviter les modifications non tracées.

---

# 52. Runbook — Incapacité à identifier la version active

## 52.1 Identifiant

```text
RB-DEPLOY-003
```

---

## 52.2 Risque

Un opérateur ne peut pas savoir quel code ou schéma est en production.

---

## 52.3 Actions

- relever l’image ;
- relever le digest ;
- consulter les labels ;
- consulter le diagnostic protégé ;
- comparer avec le repository ;
- bloquer un nouveau déploiement ;
- rétablir la traçabilité avant de continuer.

---

# 53. Runbook — Espace de backup presque plein

## 53.1 Identifiant

```text
RB-BACKUP-003
```

---

## 53.2 Actions

1. Vérifier la dernière sauvegarde valide.
2. vérifier la copie hors VPS.
3. vérifier la rétention.
4. exécuter le dry-run de purge.
5. exclure les backups en hold.
6. purger uniquement les éléments expirés.
7. vérifier l’espace.
8. corriger la croissance.
9. ne pas supprimer la dernière sauvegarde valide.

---

# 54. Runbook — Sauvegarde hors VPS inaccessible

## 54.1 Identifiant

```text
RB-BACKUP-004
```

---

## 54.2 Actions

- vérifier le réseau ;
- vérifier les credentials sans les afficher ;
- vérifier le quota ;
- vérifier la destination ;
- conserver la sauvegarde locale ;
- ne pas purger les anciennes copies ;
- rétablir le transfert ;
- vérifier le checksum distant ;
- alerter si le RPO hors site est dépassé.

---

# 55. Runbook — Incident sur plusieurs clients mutualisés

## 55.1 Identifiant

```text
RB-MULTI-001
```

---

## 55.2 Exemples

- VPS indisponible ;
- Caddy mutualisé en échec ;
- disque saturé ;
- Docker indisponible ;
- réseau partagé défaillant.

---

## 55.3 Actions

1. Identifier tous les clients affectés.
2. établir une priorité selon criticité.
3. éviter les corrections propres à un client sur la couche mutualisée.
4. restaurer la couche partagée.
5. vérifier chaque stack séparément.
6. communiquer individuellement.
7. vérifier qu’aucune donnée ou route n’est croisée.
8. produire un rapport global et des rapports clients si nécessaire.

---

# 56. Communication pendant un incident

## 56.1 Interne

Le suivi doit préciser :

```text
Incident ID
Sévérité
Impact
Heure de début
Statut
Action en cours
Prochaine mise à jour
Responsable
```

---

## 56.2 Client

Une communication client doit rester :

- factuelle ;
- concise ;
- sans hypothèse non vérifiée ;
- sans détail de sécurité exploitable ;
- sans promesse de délai non maîtrisée.

Exemple :

```text
Nous avons identifié une indisponibilité affectant le site.
L’intervention est en cours.
Les données enregistrées font l’objet de vérifications.
Une nouvelle mise à jour sera transmise à [heure].
```

---

## 56.3 Fin d’incident

La communication de clôture doit préciser :

- service restauré ;
- période d’impact ;
- fonctions affectées ;
- état des données ;
- actions correctives ;
- suivi éventuel.

---

# 57. Escalade

## 57.1 Technique

Escalade vers :

- expertise PostgreSQL ;
- fournisseur VPS ;
- fournisseur DNS ;
- fournisseur SMTP ;
- fournisseur de stockage ;
- expert sécurité ;
- conseil juridique selon l’incident.

---

## 57.2 Critères

Escalader lorsque :

- données potentiellement perdues ;
- corruption ;
- compromission ;
- restauration non fonctionnelle ;
- certificat ou DNS hors contrôle ;
- incident fournisseur majeur ;
- cause inconnue après diagnostic initial ;
- durée supérieure au RTO cible ;
- secteur réglementé.

---

# 58. Clôture d’un incident

Un incident est clos lorsque :

```text
Impact arrêté
Service restauré
Données vérifiées
Sécurité vérifiée
Health sain
Smoke tests réussis
Surveillance stabilisée
Communication envoyée
Compte-rendu créé
Actions préventives enregistrées
```

---

# 59. Compte-rendu d’incident

Structure recommandée :

```markdown
# Incident INC-YYYY-NNN

## Résumé

## Sévérité

## Clients et environnements affectés

## Chronologie UTC

## Impact

## Détection

## Cause racine

## Facteurs contributifs

## Actions de limitation

## Résolution

## Vérifications

## Données affectées

## Communication

## Ce qui a fonctionné

## Ce qui a ralenti la résolution

## Actions préventives

## Propriétaires et échéances
```

---

# 60. Post-mortem

## 60.1 Obligatoire pour

- `SEV-1` ;
- perte de données ;
- compromission ;
- restauration ;
- indisponibilité majeure ;
- échec de backup durable ;
- incident multi-client ;
- migration destructive.

---

## 60.2 Sans recherche de culpabilité

Le post-mortem doit analyser :

- système ;
- procédure ;
- signal ;
- outil ;
- décision ;
- manque de test ;
- manque de documentation.

---

## 60.3 Actions

Chaque action doit posséder :

```text
Description
Priorité
Propriétaire
Échéance
Preuve attendue
```

---

# 61. Tests des runbooks

## 61.1 Principe

Un runbook non testé reste théorique.

---

## 61.2 Tests possibles

```text
Simulation en staging

Restore drill

Arrêt du conteneur web

Indisponibilité PostgreSQL

SMTP désactivé

Espace disque simulé

Secret manquant

Migration échouée contrôlée

Rollback

Certificat local

DNS de test
```

---

## 61.3 Production

Les tests destructifs ne sont pas exécutés directement en production sans plan spécifique.

---

# 62. Exercices recommandés

## Mensuel

- vérifier les contacts opérateurs ;
- vérifier les accès ;
- vérifier le dernier backup ;
- vérifier les alertes ;
- vérifier les runbooks modifiés.

## Trimestriel

- restore drill ;
- test de récupération administrateur ;
- test de rollback ;
- vérification des secrets ;
- exercice d’indisponibilité PostgreSQL en environnement isolé.

## Annuel

- exercice de perte du VPS ;
- revue complète des responsabilités ;
- revue des RPO/RTO ;
- revue des fournisseurs ;
- revue de la mutualisation.

---

# 63. Documentation par projet

Chaque projet client doit générer ou compléter :

```text
docs/operations/runbooks/
├── application-unavailable.md
├── database-unavailable.md
├── deployment-failure.md
├── migration-failure.md
├── backup-failure.md
├── restore.md
├── disk-capacity.md
├── smtp-failure.md
├── identity-recovery.md
└── disaster-recovery.md
```

Seuls les runbooks pertinents aux modules installés doivent être inclus.

---

# 64. Variables projet dans les runbooks

Les runbooks générés doivent remplacer les placeholders suivants :

```text
{ClientSlug}
{Environment}
{ComposeProjectName}
{DeploymentDirectory}
{Domain}
{ApplicationServiceName}
{PostgreSqlServiceName}
{CaddyServiceName}
{BackupDirectory}
{HealthLiveUrl}
{HealthReadyUrl}
```

Aucun placeholder ne doit rester avant une validation de production.

---

# 65. Commandes et sécurité

## 65.1 Commandes non destructives

Elles peuvent être exécutées après vérification du contexte.

Exemples :

```text
docker compose ps
docker compose logs
df -h
health checks
```

---

## 65.2 Commandes sensibles

Elles nécessitent une validation plus forte :

```text
docker compose down
migration
restore
prune
rotation de secret
modification Caddy
```

---

## 65.3 Commandes destructives

Elles nécessitent une confirmation explicite et un plan :

```text
suppression de volume
suppression de base
restore sur production
purge manuelle
suppression de backup
```

---

## 65.4 Commandes interdites sans runbook dédié

```bash
docker compose down --volumes
```

```bash
docker system prune --all --volumes
```

```bash
rm -rf <dynamic-path>
```

```sql
DROP DATABASE ...
```

---

# 66. Verrouillage opérationnel

Les opérations suivantes ne doivent pas s’exécuter simultanément sur le même client et environnement :

- déploiement ;
- migration ;
- backup cohérent ;
- restauration ;
- import ;
- purge ;
- suppression.

Un verrou doit identifier :

```text
Opération
Opérateur
Début
Expiration
Client
Environnement
```

---

# 67. Annulation

Une opération annulée doit laisser un état connu.

Le runbook doit préciser :

- quand l’annulation est sûre ;
- quand elle est interdite ;
- comment vérifier l’état ;
- comment reprendre.

Une migration ou restauration ne doit pas être interrompue arbitrairement sans comprendre son état transactionnel.

---

# 68. Quality gates opérationnels

Une intervention ne peut pas être déclarée réussie si :

```text
Liveness en échec

Readiness en échec

Smoke tests essentiels en échec

Version active inconnue

Logs critiques persistants

Données non vérifiées

Rapport absent pour une opération sensible

Secret encore exposé

Backup requis absent
```

---

# 69. Tests automatisés des runbooks et scripts

Les tests doivent vérifier :

- chemins ;
- arguments ;
- environnement ;
- codes de sortie ;
- timeouts ;
- confirmations ;
- verrouillage ;
- erreurs ;
- absence de secrets ;
- absence de données personnelles ;
- absence de suppression hors périmètre.

---

# 70. Codes d’erreur opérationnels

| Code | Signification |
|---|---|
| `SF-OPS-001` | Contexte opérateur incomplet |
| `SF-OPS-002` | Client ou environnement non identifié |
| `SF-OPS-003` | Opération concurrente |
| `SF-OPS-004` | Confirmation manquante |
| `SF-OPS-005` | Version active inconnue |
| `SF-OPS-006` | Health check en échec |
| `SF-OPS-007` | Smoke tests en échec |
| `SF-OPS-008` | Espace disque insuffisant |
| `SF-OPS-009` | Runbook non applicable |
| `SF-OPS-010` | État intermédiaire incohérent |
| `SF-OPS-011` | Escalade requise |
| `SF-OPS-012` | Rapport opérationnel absent |
| `SF-OPS-013` | Délai de reprise dépassé |
| `SF-OPS-014` | Action destructive non autorisée |
| `SF-OPS-015` | Runbook non validé |

---

# 71. Critères de validation d’un runbook

Un runbook peut passer à `validated` lorsque :

```text
[ ] Objectif clair
[ ] Déclencheurs définis
[ ] Impact défini
[ ] Préconditions définies
[ ] Commandes contextualisées
[ ] Actions non destructives identifiées
[ ] Actions sensibles signalées
[ ] Rollback défini
[ ] Vérifications définies
[ ] Escalade définie
[ ] Données sensibles protégées
[ ] Test en environnement isolé
[ ] Résultat documenté
[ ] Propriétaire défini
[ ] Date de revue définie
```

---

# 72. Critères d’acceptation du MVP

Le dispositif sera considéré comme opérationnel lorsque :

1. un format standard de runbook existe ;
2. les niveaux de sévérité sont définis ;
3. les informations initiales à collecter sont définies ;
4. le runbook d’indisponibilité applicative existe ;
5. le runbook liveness existe ;
6. le runbook readiness existe ;
7. le runbook PostgreSQL existe ;
8. le runbook migration en échec existe ;
9. le runbook déploiement en échec existe ;
10. le runbook rollback existe ;
11. le runbook restauration existe ;
12. le runbook saturation disque existe ;
13. le runbook backup en échec existe ;
14. le runbook Caddy existe ;
15. le runbook certificat existe ;
16. le runbook SMTP existe ;
17. le runbook formulaire existe ;
18. le runbook récupération Identity existe ;
19. le runbook secret compromis existe ;
20. le runbook fuite de données existe ;
21. le runbook perte du VPS existe ;
22. les commandes dangereuses sont identifiées ;
23. un mécanisme de verrouillage est prévu ;
24. les health checks sont intégrés ;
25. les smoke tests sont définis ;
26. les comptes-rendus d’incident sont standardisés ;
27. les post-mortems sont requis pour les incidents majeurs ;
28. les runbooks sont testables en staging ;
29. chaque projet génère ses runbooks pertinents ;
30. les placeholders sont résolus avant production ;
31. aucune procédure ne demande d’afficher un secret ;
32. les scripts associés retournent des codes fiables ;
33. les runbooks possèdent un propriétaire et une date de revue.

---

# 73. Décisions validées

La baseline des runbooks opérationnels est la suivante :

```text
Les runbooks font partie du projet généré.

Chaque runbook possède un identifiant stable.

Les incidents utilisent des niveaux de sévérité.

Le diagnostic commence par identifier le client,
l’environnement, la version et l’impact.

Les données sont préservées avant une opération risquée.

Une seule modification est appliquée à la fois.

Les secrets ne sont jamais affichés pendant le diagnostic.

Les commandes destructives nécessitent
une confirmation et un plan explicites.

Les volumes ne sont jamais supprimés implicitement.

Un redémarrage ne remplace pas un diagnostic.

Le liveness et le readiness sont distingués.

Le rollback applicatif et la restauration des données
sont des procédures distinctes.

Les migrations de production restent explicites.

Une migration échouée n’est pas relancée automatiquement.

Une sauvegarde est vérifiée avant une restauration.

Une restauration utilise une version applicative compatible.

Les smoke tests sont obligatoires après correction,
déploiement, rollback ou restauration.

Les incidents de sécurité conservent les preuves utiles.

Les incidents SEV-1 produisent un post-mortem.

Les runbooks sont testés en environnement isolé.

Les runbooks sont adaptés aux modules réellement installés.

Une commande manuelle durable doit être reportée
dans le repository ou la documentation.

Codex ne peut pas lancer seul une opération de production,
une restauration ou une suppression de volume.
```

---

# 74. Règles pour Codex

## 74.1 Lecture préalable

Avant toute modification liée aux runbooks, Codex doit consulter :

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
24-operational-runbooks.md
AGENTS.md
```

Ainsi que :

- Docker Compose ;
- Caddy ;
- scripts ;
- health checks ;
- modules ;
- configuration du client ;
- procédures existantes ;
- tests.

---

## 74.2 Ce que Codex peut faire

Codex peut :

- rédiger un runbook prévu ;
- adapter un runbook à un module ;
- ajouter un diagnostic ;
- ajouter une vérification ;
- créer un script non destructif ;
- ajouter un smoke test ;
- ajouter un rapport ;
- améliorer un message ;
- ajouter des tests ;
- corriger une commande dangereuse ;
- documenter une escalade ;
- ajouter un template de post-mortem.

---

## 74.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- exécuter une commande de production ;
- lancer une restauration ;
- supprimer un volume ;
- lancer une migration ;
- afficher un secret ;
- proposer `docker compose down --volumes` comme correction standard ;
- proposer `docker system prune --volumes` sans analyse ;
- proposer `rm -rf` sur un chemin variable ;
- désactiver une protection pour restaurer le service ;
- effacer des logs d’incident ;
- considérer un redémarrage comme une résolution définitive ;
- déclarer un rollback sûr sans vérifier la base ;
- déclarer un restore réussi sans smoke tests ;
- modifier les RPO ou RTO ;
- modifier une sévérité pour minimiser un incident ;
- fermer un incident avec health checks en échec.

---

## 74.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- opération de production ;
- restauration ;
- suppression de données ;
- suppression de volume ;
- migration destructive ;
- rotation d’un secret réel ;
- communication client ;
- changement de sévérité ;
- bascule DNS ;
- changement d’infrastructure ;
- rollback de données ;
- incident de sécurité ;
- données personnelles potentiellement exposées ;
- escalade externe ;
- clôture d’un incident majeur.

---

## 74.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Runbook concerné
Incident ou scénario concerné
Déclencheurs
Commandes ajoutées ou modifiées
Actions sensibles
Confirmations
Rollback
Health checks
Smoke tests
Logs utilisés
Données potentiellement affectées
Tests réalisés
Environnement de test
Résultats
Risques
Décisions humaines restantes
```

Codex ne doit pas déclarer un runbook validé s’il n’a pas été testé dans un environnement approprié.

---

# 75. Anti-patterns interdits

## 75.1 Redémarrage systématique

```text
Incident
→ restart
→ incident fermé
```

sans cause ni vérification.

---

## 75.2 Suppression de volumes

Un runbook recommande :

```bash
docker compose down --volumes
```

pour corriger un problème de démarrage.

---

## 75.3 Secret affiché

Le diagnostic demande :

```bash
cat .env.production
```

ou :

```bash
printenv
```

sans filtrage.

---

## 75.4 Plusieurs changements simultanés

L’opérateur modifie :

- image ;
- secret ;
- migration ;
- Caddy ;

avant de retester.

---

## 75.5 Migration relancée en boucle

Une migration échouée est réexécutée sans vérifier l’état du schéma.

---

## 75.6 Logs supprimés

Les logs sont effacés pour libérer de l’espace avant d’en conserver les éléments utiles.

---

## 75.7 Production utilisée comme environnement de test

Une procédure destructive est essayée directement sur le client.

---

## 75.8 Rollback fictif

Le runbook annonce un rollback alors que l’image précédente n’est pas conservée.

---

## 75.9 Restauration sans réconciliation

Les suppressions et révocations postérieures au backup ne sont pas réappliquées.

---

## 75.10 Incident clôturé sans smoke tests

Le service semble accessible, mais les parcours essentiels ne sont pas vérifiés.

---

## 75.11 Commande non contextualisée

Le runbook contient :

```bash
docker compose down
```

sans préciser :

- répertoire ;
- client ;
- environnement ;
- impact.

---

## 75.12 Correction manuelle non reportée

Le serveur est modifié, mais le repository reste inchangé.

---

# 76. Roadmap d’implémentation

## Phase runbooks 1 — Baseline applicative

```text
application unavailable
liveness
readiness
container restart loop
```

---

## Phase runbooks 2 — Données

```text
PostgreSQL
migration required
migration failure
restore
```

---

## Phase runbooks 3 — Déploiement

```text
deployment failure
rollback
configuration error
version unknown
```

---

## Phase runbooks 4 — Infrastructure

```text
disk
resources
Caddy
certificate
DNS
```

---

## Phase runbooks 5 — Fonctionnel

```text
forms
SMTP
Identity
media
uploads
```

---

## Phase runbooks 6 — Sécurité

```text
compromised account
compromised secret
data breach
dependency vulnerability
```

---

## Phase runbooks 7 — Continuité

```text
backup failure
offsite failure
restore drill failure
VPS loss
VPS compromise
```

---

## Phase runbooks 8 — Exercices

```text
staging simulations
quarterly restore drill
annual VPS recovery exercise
post-mortem improvements
```

---

# 77. Checklist de prise en charge

```text
[ ] Incident ID créé
[ ] Client identifié
[ ] Environnement identifié
[ ] Sévérité définie
[ ] Impact défini
[ ] Version relevée
[ ] Dernier changement relevé
[ ] Correlation ID recherché
[ ] Backup vérifié si données concernées
[ ] Opérations concurrentes arrêtées
[ ] Communication initiale préparée
```

---

# 78. Checklist avant action sensible

```text
[ ] Cause ou hypothèse définie
[ ] Action limitée
[ ] Impact compris
[ ] Cible vérifiée
[ ] Backup disponible
[ ] Rollback défini
[ ] Confirmation obtenue
[ ] Logs préservés
[ ] Un seul opérateur responsable
[ ] Heure UTC relevée
```

---

# 79. Checklist de retour à la normale

```text
[ ] Liveness sain
[ ] Readiness sain
[ ] Version attendue
[ ] Conteneurs stables
[ ] PostgreSQL sain
[ ] App_Data disponible
[ ] Data Protection disponible
[ ] HTTPS
[ ] Administration protégée
[ ] Login
[ ] Formulaire
[ ] Modules affectés
[ ] Logs sans erreur critique
[ ] Espace disque suffisant
[ ] Backup fonctionnel
[ ] Surveillance stabilisée
```

---

# 80. Checklist de clôture

```text
[ ] Impact terminé
[ ] Heure de fin UTC
[ ] Données vérifiées
[ ] Communication envoyée
[ ] Compte-rendu créé
[ ] Cause racine identifiée ou investigation ouverte
[ ] Test de régression prévu
[ ] Runbook mis à jour
[ ] Action préventive assignée
[ ] Post-mortem planifié si nécessaire
```

---

# 81. Modèle de fiche runbook

```markdown
# RB-{DOMAIN}-{NUMBER} — {Titre}

## Informations

| Élément | Valeur |
|---|---|
| Statut | draft |
| Sévérité possible | SEV-X |
| Propriétaire | LogicGate |
| Dernier test | À définir |
| Prochaine revue | À définir |

## Objectif

## Déclencheurs

## Impact potentiel

## Préconditions

## Informations à collecter

## Diagnostic

## Actions de limitation

## Procédure de résolution

## Vérifications

## Rollback

## Escalade

## Compte-rendu

## Prévention
```

---

# 82. Conclusion

Les runbooks opérationnels SiteFactory doivent transformer une situation de stress en une suite d’actions contrôlées.

Ils reposent sur :

```text
contexte identifié
+ impact qualifié
+ données préservées
+ diagnostic structuré
+ action limitée
+ vérification
+ rollback
+ documentation
```

Un runbook utile ne se contente pas d’indiquer une commande.

Il précise :

- quand l’utiliser ;
- quand ne pas l’utiliser ;
- ce qu’elle peut modifier ;
- comment vérifier son résultat ;
- comment revenir en arrière.

> Un incident bien géré ne dépend pas de la mémoire de l’opérateur. Il dépend d’un contexte fiable, de signaux exploitables et d’une procédure déjà pensée avant l’urgence.
