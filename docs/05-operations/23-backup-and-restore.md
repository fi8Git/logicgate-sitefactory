# LogicGate SiteFactory — Sauvegarde et restauration

Ce document conserve le **contrat d’une future capacité d’exploitation**.
Il ne fournit pas une procédure de production immédiatement exécutable et
n’atteste aucun backup, restore drill ou engagement de reprise acquis.

## Capacités disponibles

Le [modèle runtime](../03-generation/generated-runtime-database-model.md) et les
[validations de démarrage](../03-generation/generated-startup-validation.md)
peuvent représenter/refuser des états bloquants. Le runtime ne les répare pas :
leur existence ne livre pas la réconciliation post-restauration.

Le [harnais PostgreSQL](../../tests/PostgreSql/README.md) crée et détruit des
ressources éphémères de test. Son cleanup ne constitue ni sauvegarde ni restore.
La [CI publique](../04-quality-security/20-quality-gates-and-ci.md) est opérationnelle,
avec SourceLink VERIFIED ; elle ne prouve pas une restauration client.

## Capacités non livrées

Aucun outil client de backup/restore, script opérateur généré, rôle de production
backup/restore provisionné, planification ou purge de sauvegardes n’est livré.
Aucun groupe CLI de sauvegarde/restauration n’existe. Les manifests et rapports
exigés ci-dessous restent des contrats à implémenter, pas des fichiers déjà produits.
PITR, réplication, bascule automatique, incrémental, stockage immuable certifié
et haute disponibilité ne sont pas annoncés disponibles.

## Contrats et exigences applicables

### Périmètre, versions et droits

Une sauvegarde n’est pas une archive de conservation ni une source fonctionnelle.
Elle doit permettre de reconstruire un système cohérent, pas seulement fournir
un dump. Documenter composants inclus/exclus et responsabilités pour chaque projet.

| Composant concerné | Exigences conservées |
|---|---|
| PostgreSQL | Données/schémas applicables et historique des migrations ; format logique restaurable, outil compatible avec le serveur et version enregistrée. Le format custom est recommandé. Éviter les propriétaires/privilèges spécifiques à la source ; recréer puis vérifier les ACL de la cible. |
| Fichiers persistants | App_Data, médias, documents et fichiers de modules nécessaires, avec arborescence, métadonnées et permissions utiles. Exclusions explicites pour caches, temporaires, exports expirés, logs et données recomposables ; un fichier requis ne devient pas facultatif par défaut. |
| Data Protection | Sauvegarder les clés nécessaires à la reprise, isolées par client/environnement, protégées au repos et en transit. Vérifier permissions et nom d’application après restore. Leur perte peut invalider sessions/tokens et rendre des données indéchiffrables ; évaluer cet impact. |
| Configuration et artefacts | Commit/repository, versions application/générateur/catalogue/template/thème/modules, migration, image/digest et configuration non sensible. Conserver ou rendre reconstruisibles les configurations serveur/Caddy, domaines, volumes et tâches durables. Ne pas recopier inutilement le repository ou des caches. |
| Secrets | Inventaire sans valeurs : identifiant, responsable, fournisseur, renouvellement/rotation et nécessité à la reprise. Valeurs récupérables ou recréables par une procédure séparée ; jamais de fichier d’environnement en clair dans l’archive standard. |
| Caddy et accès | Protéger certificats, clés privées et états nécessaires ; prévoir reconstruction de configuration, DNS, certificats et vérification HTTPS lorsque la réémission est retenue. Aucun credential dans les rapports. |

Les contrats de [stockage et cohérence DB/médias](../adr/0016-content-and-media-storage-boundaries.md#74-backup-postgresql-et-médias)
et de [secrets](../adr/0012-runtime-configuration-and-secrets.md#84-backup-des-secrets)
restent normatifs. Une image peut être exclue des archives si le digest exact
reste durablement récupérable ; sinon garantir sa disponibilité ou reconstruction.

Appliquer les [rôles backup](../adr/0027-database-roles-and-runtime-least-privilege.md#23-backup-role)
et [restore](../adr/0027-database-roles-and-runtime-least-privilege.md#24-restore-role) :
backup en lecture seule, distinct du runtime ; restore par opérateur dans une
cible isolée, credential temporaire/protégé, élévation contrôlée puis révocation,
rotation ou désactivation. Aucun service permanent n’utilise le rôle restore.

### Cohérence, finalisation et vérification

PostgreSQL et fichiers doivent représenter un état cohérent. Suspendre/réduire
les écritures selon le risque et enregistrer les instants de capture. Un faible
décalage n’est acceptable que pour faible activité, risque documenté, fichiers
non critiques et réconciliation prévue ; une cohérence stricte exige une
stratégie spécifique testée. Backup supplémentaire avant migration, import,
suppression, restauration partielle, changement de stockage/module ou autre
opération sensible affectant les données.

La sauvegarde ne devient complète qu’après vérification des composants requis :
fichiers non vides, tailles/nombres plausibles, archives lisibles, contenu dump
inspectable, migration attendue lorsque vérifiable, exclusions/permissions
correctes et empreinte **SHA-256 de chaque fichier**. Un checksum prouve
l’intégrité binaire, pas la restaurabilité ni la cohérence fonctionnelle.

Produire dans une zone temporaire protégée, puis finaliser/promouvoir seulement
après contrôle. Tout artefact partiel garde un statut failed/incomplete ;
exit non nul, composant requis absent, manifest/checksum manquant ou copie
obligatoire échouée interdisent success. Ne jamais modifier un backup existant
pour masquer un échec ni recycler un résultat manquant.

Le manifest doit identifier backup/projet/environnement, type et cohérence,
horodatages UTC, versions/digest/migration, composants, tailles/empreintes,
résultat et destination de copie, warnings/erreurs. Le rapport ajoute les étapes,
durées et statut de transfert/rétention réellement observés ; absent reste inconnu.
Aucune donnée restaurée, SQL, PII, secret, clé, chemin sensible ou destination
contenant des credentials dans noms de fichiers, manifests, logs ou rapports.

### Fréquence, rétention et purge

Les prescriptions spécifiques de ce guide restent la **baseline cible** :
sauvegarde complète quotidienne, rétention initiale **30 jours** et restore drill
complet **au minimum trimestriel** pour les productions exploitées par LogicGate.
Les objectifs internes **RPO 24 h / RTO 8 h** sont non contractuels ; ils ne
décrivent pas un service livré. Définir horaire/fuseau, volumes, disponibilité
opérateur et moyens nécessaires par projet. Objectifs plus stricts : cadrage
complémentaire, sans promesse automatique de réplication/PITR.

Conserver les sauvegardes pré-migration au moins jusqu’à validation de la version,
fin de stabilisation et expiration définie au runbook. Les sauvegardes de staging
restent distinctes ; les données de développement sont recréables, sans obligation
de backup de développement. Toute réduction de fréquence/rétention ou modification
des objectifs nécessite une décision humaine.

La [politique privacy des backups](../adr/0023-data-retention-deletion-and-privacy-lifecycle.md#59-backups)
interdit la conservation indéfinie sans finalité et responsabilité. Des données
supprimées peuvent subsister jusqu’à expiration : accès limité à la reprise,
protection/chiffrement et suppression réappliquée avant réouverture. Ne pas
altérer chaque archive historique au risque de détruire son intégrité.

Purge uniquement après nouvelle sauvegarde réussie, vérification locale et copie
hors serveur validée lorsqu’elle est requise, selon la politique approuvée.
Dry-run obligatoire : cibles/dates/tailles/nombre avant suppression. Valider les
chemins normalisés dans la racine autorisée, refuser traversal/liens dangereux ;
préserver les holds incident/pré-migration. Jamais supprimer la dernière copie
valide à cause d’un nouvel échec, ni purger des archives non expirées implicitement.
Conservation, suppression manuelle, changement de rétention/destination et hold
doivent être audités.

### Protection et copie hors serveur

Les archives sont protégées comme la production : aucune publication, aucun
commit Git, permissions minimales par client/environnement et accès opérateur
limité. Une production exploitée durablement doit disposer d’une copie hors VPS ;
un volume ou snapshot sur le même serveur ne couvre pas sa perte/compromission.

Transfert chiffré et chiffrement au repos, outil approuvé, clé séparée de l’archive,
récupération/rotation testées. La compression ne chiffre pas ; une clé perdue
rend la sauvegarde inutilisable. Credentials de destination distincts, limités,
renouvelables, avec réduction des capacités de destruction depuis un compte
compromis ; immutabilité renforcée à étudier selon le risque, sans la prétendre livrée.

Vérifier présence distante, taille, empreinte, date et destination après transfert.
Une copie hors site inaccessible interdit sa purge opportuniste : conserver les
copies valides, rétablir le transfert et alerter sur l’écart de RPO.

### Restauration et réouverture

Une restauration peut perdre les écritures postérieures au backup. Elle exige
incident qualifié, source/manifest/checksums et **cible explicites**, version
compatible disponible, secrets/clés accessibles, capacité suffisante, plan et
confirmation spécifique ; une approbation générique ne suffit pas en production.
Préserver l’état actuel lorsque possible et prévoir la reprise si le restore échoue.

Interdire les opérations concurrentes incompatibles : backup, restore, migration,
déploiement, purge ou import sur le même client/environnement. Arrêter/isoler
les écritures et workers, restaurer d’abord en environnement distinct lorsque
temps et gravité le permettent. Aucun test/drill sur une production réelle.

Préparer une base et des volumes propres pour éviter le mélange ancien/nouveau.
Extraction confinée : refuser chemins sortants, liens dangereux et fichiers
exécutables inattendus ; respecter exclusions, nombres/tailles et droits des
fichiers privés. Vérifier schéma, contraintes, séquences, propriétaires/ACL,
références DB/fichiers et accès effectif du runtime à droits minimaux.
Une reprise partielle conserve identifiants, relations, historique et autorisations.

Démarrer la version **compatible avec le backup**, vérifier puis stabiliser ;
l’upgrade est une opération séparée, jamais les dernières migrations appliquées
automatiquement pendant le restore. Le rollback applicatif ne restaure pas les données.

Avant trafic normal, respecter :

- [ADR-0023 — restauration et Deletion Ledger](../adr/0023-data-retention-deletion-and-privacy-lifecycle.md#61-restauration) :
  suppressions/anonymisations postérieures au backup réappliquées depuis le
  ledger externe protégé, suffisamment récent, et fournisseurs réconciliés ;
- [ADR-0031 — restore-pending](../adr/0031-application-startup-validation-and-runtime-compatibility.md#58-restore-state) :
  permissions et rapport validés, epochs de sécurité renouvelés, aucune réparation
  automatique par le web ;
- [ADR-0032 — maintenance et epochs](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md#85-restore-et-maintenance) :
  maintenance maintenue, sessions et anciens tokens de provisioning/récupération
  invalidés, cache réconcilié/invalidation, réouverture contrôlée ;
- [ADR-0019 — intégrité d’audit](../adr/0019-audit-trail-and-sensitive-administrative-actions.md#94-restauration) :
  chaîne restaurée vérifiée, nouvel epoch et rapprochement des rapports/anchors.

Réappliquer aussi désactivations, permissions, révocations, rotations, domaines et
configurations nécessaires depuis des sources indépendantes du backup restauré.
Un ledger requis absent, une clé nécessaire perdue, une permission incohérente
ou un échec de vérification bloque la reprise normale ; documenter/analyser,
jamais réouvrir par simple fin technique du restore.

### Restore drill, alertes et preuve

Le drill utilise réellement la copie hors VPS, son déchiffrement, PostgreSQL,
fichiers et clés requis, puis l’application compatible et les smoke tests.
SMTP sandbox/désactivé, LLM/webhooks et jobs destructifs désactivés, indexation
interdite, ressources/secrets de test isolés. Réévaluer après changement de format,
PostgreSQL majeur, stockage, chiffrement, layout, infrastructure ou fournisseur.

Mesurer RPO observé (incident moins dernier backup exploitable) et RTO observé
(décision de reprise jusqu’au service vérifié). Un exercice réussi ne crée aucun
SLA. Aucun drill déclaré réussi sans application démarrée, readiness, données,
permissions, médias applicables, smoke tests et rapport vérifiés.

Suivre dernier backup réussi/âge, durées/tailles, échecs consécutifs, copie,
espace et dernier drill. Tracer début, fin/échec, capture PostgreSQL/fichiers, vérification et copie.
Les propositions de seuils 26 h warning / 48 h critical
pour une fréquence quotidienne restent à adapter à l’horaire et au RPO, pas des
alertes configurées. Toute anomalie de taille/empreinte et tout échec sont
signalés, avec exit non nul et conservation des précédentes preuves valides.

Les futurs tests couvrent chemins/traversal/liens, mauvaise cible, permissions,
corruption, clé absente, version incompatible, disque/réseau/outil indisponible,
timeout, annulation, concurrence, purge/hold et absence de secret/PII.
Utiliser PostgreSQL réel et données synthétiques, jamais une production.
Voir la [stratégie de tests](../04-quality-security/19-testing-strategy.md#exigences-pour-capacités-futures).

## Références et responsabilités

L’opérateur de l’exploitation doit configurer, vérifier, surveiller, maintenir
les scripts et protéger les accès ; le projet identifie ses données critiques,
durées, responsables et risques. Transfert/fin de service : dernière sauvegarde
vérifiée, format/versions/checksums, procédures, rétention, destination, drill,
objectifs, accès et responsabilités transmis ; secrets par canal séparé,
copies supprimées seulement à l’échéance prévue. Voir le
[contrat de handover](27-client-handover-and-service-closure.md).

Toute nouvelle destination, chiffrement, format, rétention/fréquence, RPO/RTO,
donnée sensible, version majeure PostgreSQL, purge, restauration partielle,
transfert ou fin du service exige une décision humaine. Les
[limites d’action des agents](../../AGENTS.md#10-actions-interdites-à-un-agent)
et les [runbooks](24-operational-runbooks.md) s’appliquent ; aucun restore,
suppression réelle ou rotation de secret n’est autorisé par ce guide.

**P03-F1 reste OPEN / UNKNOWN** : 749 HTTP 200 / 19 HTTP 503 historiques, cause
non démontrée, risque accepté avec réserve ; réexamen **2026-11-16**, sans
prolongation tacite. Une CI verte ne démontre aucune correction causale.
Conserver les [observations historiques](../../tests/readiness-concurrency-regression.md).
