# LogicGate SiteFactory — Runbooks opérationnels

Ce guide définit les exigences de diagnostic et de rédaction des runbooks.
**Aucune procédure de production client n’est validée par ce texte.**
Une procédure reste théorique tant que ses composants et son exercice n’existent pas.

## Capacités disponibles

| Parcours actuel | Portée et source |
|---|---|
| validate / plan / generate | [CLI publique](../03-generation/initial-generation-cli.md) et [parseur réel](../../src/LogicGate.SiteFactory.Cli/SiteFactoryCli.cs) : diagnostic de configuration, plan read-only, génération/régénération documentaire confinée. Ce n’est pas un déploiement. |
| Runtime liveness/readiness | [Endpoints composables](../../src/LogicGate.SiteFactory.Runtime.AspNetCore/RuntimeHealthEndpoints.cs) : /health/live, /health/ready et /health de compatibilité ; réponses minimales, sans cache ni diagnostic sensible. |
| Migrator apply/bootstrap | [Parcours explicite Development/Test](../03-generation/generated-explicit-migrator.md), base existante, confirmations et credential distinct. Aucun mode production autorisé par ce guide. |
| PostgreSQL REQUIRED | [Harnais réel de test](../../tests/PostgreSql/README.md), inventaires fermés, ressources éphémères et cleanup ; ni backup ni restauration client. |
| Diagnostic CI | [Contrat CI](../04-quality-security/20-quality-gates-and-ci.md#échecs-cleanup-et-diagnostic) : preuves contrôlées et expurgées, résultats incomplets distingués et cleanup même après échec. Aucun log brut publié. |

Dans la composition runtime actuelle, le liveness vérifie le pipeline vivant
sans exécuter les checks de dépendances. Le readiness applique les validations
enregistrées de démarrage/lifecycle et la vérification PostgreSQL lorsqu’elle
est composée : Healthy donne 200, dégradé/non prêt donne 503. Le gate de trafic
refuse aussi le travail hors état prêt. Cela n’atteste pas des checks Identity,
SMTP, App_Data ou jobs qui ne sont pas enregistrés/livrés.

Pour diagnostiquer ces parcours : identifier version/configuration et première
étape réellement en échec ; rapprocher le code stable et l’état observé du
contrat concerné. Ne pas inventer de résultat absent, changer une assertion,
forcer readiness à Healthy ou activer une migration/réparation automatique.

**P03-F1 reste OPEN / UNKNOWN** : historique 749 HTTP 200 / 19 HTTP 503, cause
non démontrée, risque accepté avec réserve ; réexamen **2026-11-16**, sans
prolongation tacite. Une CI verte ne démontre aucune correction causale.
Les [observations historiques](../../tests/readiness-concurrency-regression.md)
restent distinctes d’un incident de production à réparer.

## Capacités non livrées

VPS client, déploiement/rollback, backup/restore, exploitation SMTP/Identity,
formulaires, jobs métier et reprise après sinistre ne sont pas des runbooks
exécutables livrés. Aucun générateur de runbooks client, catalogue de commandes
opérationnelles ou famille de codes d’exploitation n’est annoncé implémenté.
Leurs procédures doivent être réalisées, contextualisées, autorisées et testées
avant de devenir une référence opérationnelle.

Les scénarios suivants sont des **contrats futurs**, pas des instructions
d’intervention immédiate. Un ADR accepté ne prouve ni service opéré, ni SLA,
ni habilitation de production.

## Contrats et exigences applicables

### Cadre d’intervention

Suivre [AGENTS](../../AGENTS.md#10-actions-interdites-à-un-agent) et les
[préconditions de déploiement](22-deployment-and-environments.md#préconditions-et-contrôle-dune-opération-de-production).
Une contribution ne permet aucune mutation de production, migration, restauration,
suppression de volume/données, rotation réelle, bascule DNS, communication client
ou escalade externe sans demande et validation humaines explicites.

Le diagnostic commence par un contexte vérifié : incident/projet/client,
environnement et domaine, opérateur, heure UTC, sévérité/impact, versions,
commit/digest, migration, dernier changement, dernière sauvegarde exploitable et
corrélation disponible. Inconnu reste inconnu ; distinguer observation, hypothèse
et cause démontrée. Une version active inconnue bloque un nouveau déploiement.

Préserver données et preuves avant action risquée ; évaluer un backup de l’état
courant. Une hypothèse, une action limitée, une observation. Aucun redémarrage en
boucle, modification durable dans un conteneur, correction SQL improvisée,
suppression d’un volume inconnu ou désactivation générale d’une protection.

Les diagnostics sont ciblés, bornés et expurgés. Ne pas publier environnement
complet, fichier de secrets, configuration résolue, inspection/log brut,
exception, stack trace, argument sensible, contenu SQL/client, cookie ou clé.
L’accès protégé aux preuves nécessaires reste contrôlé ; conserver les traces
utiles d’un incident de sécurité avant rotation/purge, jamais les recopier dans
un rapport public. Voir [baseline sécurité](../04-quality-security/18-security-baseline.md)
et [observabilité](21-observability-and-logging.md).

Un verrou par client/environnement coordonne déploiement, migration, backup
cohérent, restore, import, purge et suppression. Il identifie opération,
opérateur, début et expiration. Les durées sont bornées ; documenter annulation
sûre, état transactionnel, contrôle et reprise. Ne pas interrompre arbitrairement
une mutation persistante ni reprendre en supposant sa réussite.

Les actions sensibles/destructives exigent plan, cible vérifiée, impacts,
backup requis, rollback et confirmation spécifiques. Ni purge globale de volumes,
ni suppression récursive construite depuis un chemin libre, ni suppression DB
ne constitue une correction standard. Les changements durables reviennent dans
repository/configuration/migrations/runbook ; aucun état serveur caché.

### Format et validation d’un runbook

Chaque runbook pertinent aux composants installés doit conserver :

- identifiant stable, titre, objectif, propriétaire et statut ;
- déclencheurs/impact, contexte/préconditions et informations à collecter ;
- diagnostic, actions de limitation, résolution, rollback et critères d’arrêt ;
- commandes contextualisées (répertoire, cible, environnement, impact), actions
  non destructives/sensibles distinctes, confirmations et vérifications ;
- escalade, compte rendu, prévention et prochaine revue ;
- date de validation/dernier exercice, environnement, résultat et preuve.

Statuts : draft, validated, deprecated, retired. **Validated exige un exercice
réussi dans un environnement approprié et isolé**, pas la seule revue du texte.
Aucun placeholder ne reste dans un runbook validé pour une cible réelle.
Ni un agent ni une campagne CI verte ne peut déclarer un exercice non exécuté.

### Sévérité, suivi et escalade

| Niveau | Qualification et attentes |
|---|---|
| SEV-1 | Indisponibilité totale, perte/corruption, compromission/exposition ou production et backup menacés : prise en charge immédiate, limitation d’impact, communication adaptée, suivi continu et post-mortem. |
| SEV-2 | Fonction essentielle indisponible ou forte dégradation : correction/contournement prioritaire, suivi jusqu’à validation et compte rendu. |
| SEV-3 | Sous-ensemble ou fonction secondaire affecté, backup ponctuellement échoué avec précédent exploitable : suivi selon impact réel. |
| SEV-4 | Défaut mineur, contenu ou warning non bloquant : correction planifiée. |

Ne pas réduire une sévérité pour minimiser un incident ; changement validé
humainement. Escalader perte de données, corruption, compromission, reprise
défaillante, cause inconnue après diagnostic initial, RTO dépassé, dépendance
hors contrôle ou obligation réglementaire vers l’expertise appropriée.
La communication autorisée reste factuelle, sans hypothèse présentée comme
cause, détail exploitable ou promesse de délai non maîtrisée. Indiquer
responsable, statut, impact, action et prochaine mise à jour.

### Garde-fous par scénario futur

Ces limites complètent les contrats de [déploiement](22-deployment-and-environments.md),
[sauvegarde/restauration](23-backup-and-restore.md) et la baseline ; elles ne
livrent ni les composants métier ni leurs procédures.

| Domaine | Règles à conserver lors de l’implémentation du runbook |
|---|---|
| Démarrage/DB | Chercher la première erreur et l’état des versions, options, volumes/permissions, ressources et connexions. Volume absent : identifier l’existant, ne pas recréer une base vide à sa place. Corruption suspectée : arrêter les écritures, préserver l’état, aucune réparation improvisée. Liveness indépendant des dépendances ; readiness jamais rendu artificiellement vert. |
| Migration/déploiement interrompu | Arrêter la progression, conserver la preuve, vérifier transaction, schéma/historique et version réellement active. Pas de retry automatique ; plan de correction/forward fix ou restore selon l’état. Ancienne image/configuration disponibles et compatibilité vérifiée avant rollback. [ADR-0032 — rollback](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md#76-rollback). |
| Disque/logs/ressources | Identifier source de croissance/OOM avant action ; rotation prévue et fichiers expirés identifiés seulement. Préserver logs critiques, fichiers PostgreSQL, App_Data et dernière sauvegarde valide. Aucune purge aveugle de volumes/images. Corriger la cause et vérifier stabilité, espace et reprise des logs. |
| Backup/drill | Échec : conserver les précédentes copies, bloquer purge, vérifier outils/versions, permissions, clés, checksums et destination. Un drill échoué rend les backups similaires potentiellement suspects ; conserver la preuve et documenter l’écart RPO/RTO. Hors site inaccessible : conserver le local, vérifier transfert puis empreinte distante. |
| HTTPS/DNS/proxy | Vérifier domaine, backend, réseau, horloge, certificats/quota et stockage. Configuration valide avant reload ; pas de TLS désactivé pour contourner l’échec. Proxy partagé : vérifier chaque client, routes/certificats et absence de donnée croisée, sans correctif propre à un client appliqué globalement. |
| Administration/sessions | [ADR-0021 — récupération contrôlée](../adr/0021-administrator-provisioning-invitations-and-account-recovery.md#88-récupération-du-dernier-administrateur-supprimé) : identité vérifiée, invitation/récupération ou break-glass exceptionnel audité ; jamais reset par AdminSeed, endpoint public de secours ou réactivation directe de rôle par SQL. Protéger le dernier administrateur ; vérifier sessions/MFA/permissions selon [ADR-0020](../adr/0020-administrator-session-security-and-step-up-authentication.md). |
| Compte, secret ou données compromis | Limiter/isoler l’accès, préserver les preuves, identifier période/environnements/données, révoquer/rotater sous autorisation et vérifier comptes, rôles, sessions et accès persistants. Un secret dans Git est compromis ; retirer la dernière occurrence ne suffit pas. Examiner historiques, forks/caches et artefacts sans réécriture autonome ; notifications/obligations évaluées humainement. |
| VPS ou backup compromis | Ne pas restaurer simplement sur la cible compromise. Infrastructure propre, sauvegarde réputée saine, images/packages/repository vérifiés, secrets/SSH renouvelés, sessions invalidées et comptes audités ; analyser aussi les archives créées pendant la compromission. |
| Formulaire/SMTP/intégration | Distinguer validation, persistance et notification. Une notification échouée ne perd pas la demande persistée ; aucun renvoi massif ni reprise d’effet externe sans idempotence. Kill switch/fallback uniquement approuvés, aucune destination/provider substitué implicitement. [ADR-0025](../adr/0025-third-party-integrations-and-egress-control.md). |
| Abus/CORS/upload | Préserver validation, autorisation, antiforgery et protections serveur. CORS n’est pas une autorisation ; contrôler proxys/origines et rate limit sans publier de listes personnelles. Upload suspect : refuser, ne pas exécuter, isoler l’analyse, vérifier les autres fichiers ; suppression contrôlée seulement. |
| Fichiers/Data Protection | Vérifier volumes, clés, nom d’application, permissions et références DB/fichiers ; restore confiné dans une cible distincte. Perte de clés : évaluer invalidation de sessions/tokens et données indéchiffrables, jamais recréer aveuglément un volume. [Contrat de reprise](23-backup-and-restore.md#restauration-et-réouverture). |
| IA optionnelle/contenu | Application principale indépendante du provider ; aucun transfert à un autre provider non approuvé. Bloquer publication de brouillon suspect, examiner exposition et contenus de même version, conserver validation humaine. [ADR-0015](../adr/0015-deterministic-generation-and-optional-ai.md). |
| SEO et staging | Corriger domaine/canonical, robots/sitemap, placeholders et indexation ; staging reste non indexable et protégé, sans affecter le domaine de production. Aucun succès SEO ou contenu métier livré déduit de cette règle. |

Une vulnérabilité critique exige analyse des versions/exploitabilité et projets
touchés, mitigation, correctif testé/scanné et publication/upgrade explicitement
autorisés. Une nouvelle release affectée reste bloquée sans mitigation formelle.
Ne pas rendre un contrôle de sécurité non bloquant pour rétablir un service.

### Vérification, clôture et prévention

Succès exige état/version connus, impact arrêté, données et sécurité vérifiées,
liveness/readiness, smoke tests des parcours concernés, absence d’erreur critique,
backup requis et rapport, puis surveillance stabilisée. Un restart ou un seul
HTTP 200 ne suffit pas. La [réconciliation post-restore](23-backup-and-restore.md#restauration-et-réouverture)
précède toute réouverture.

Le compte rendu conserve chronologie UTC, cible/version/impact, détection,
observations/hypothèses/cause démontrée ou investigation ouverte, actions,
résultats vérifiés, données affectées, communication et risques restants.
Les actions préventives ont priorité, propriétaire, échéance et preuve attendue.
Ne pas clôturer avec health/smoke tests en échec, version inconnue, donnée non
vérifiée, secret exposé ou rapport sensible absent.

Post-mortem obligatoire pour SEV-1, perte de données, compromission, restauration,
indisponibilité majeure, échec durable des backups, incident multi-client ou
migration destructive. Analyser système/procédures/signaux/outils/décisions et
tests manquants, sans recherche de culpabilité ; mettre à jour le runbook et
conserver un test de régression pertinent.

## Références et limites à traiter

Les contrats actifs sont [ADR-0031 — startup/readiness](../adr/0031-application-startup-validation-and-runtime-compatibility.md#75-liveness),
[ADR-0032 — rollout](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md),
[ADR-0023 — privacy/restore](../adr/0023-data-retention-deletion-and-privacy-lifecycle.md#61-restauration)
et [ADR-0019 — audit](../adr/0019-audit-trail-and-sensitive-administrative-actions.md).
Leur acceptation n’atteste pas les fonctionnalités futures.

Les futurs tests de scripts/runbooks couvrent arguments, chemins, cibles,
confirmations, verrouillage, timeouts/annulation, codes de sortie, erreurs,
non-divulgation et suppression confinée ; exercices destructifs isolés, pas sur
la production cliente. Voir la [stratégie de tests](../04-quality-security/19-testing-strategy.md#exigences-pour-capacités-futures).

Recommandations d’exercice conservées : revue mensuelle des contacts, accès,
backups, alertes et runbooks modifiés ; exercices trimestriels de restore,
récupération administrateur, rollback et panne PostgreSQL isolée ; revue annuelle
de reprise VPS, responsabilités, RPO/RTO, fournisseurs et mutualisation.
Aucun de ces exercices n’est prétendu exécuté par ce guide.

Chaque projet devra réaliser/tester ses procédures et nommer leurs responsables.
Un agent ne modifie pas seul sévérité, RPO/RTO, règles de reprise, permissions ou
clôture d’incident majeur ; il rend compte des vérifications réellement effectuées
et des décisions restantes. Ce texte ne constitue ni SLA, ni service de support,
ni substitution à l’expertise PostgreSQL, sécurité ou obligations juridiques.
