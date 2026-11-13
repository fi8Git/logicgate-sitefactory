# LogicGate SiteFactory — Déploiement et environnements

Ce guide distingue les capacités du moteur des **contrats de déploiement client
non livrés**. Les exigences ci-dessous restent applicables aux composants futurs ;
leur présence ne constitue ni une preuve de production ni une autorisation d’agir.

## Capacités disponibles

| Capacité | Portée actuelle et source |
|---|---|
| CLI validate / plan / generate | Validation V1, plan et génération/régénération du template documentaire statique. Voir les [commandes réelles](../03-generation/initial-generation-cli.md) et le [parseur](../../src/LogicGate.SiteFactory.Cli/SiteFactoryCli.cs). La génération ne déploie rien. |
| Configuration runtime | Identité explicite Development, Test, Staging ou Production et options validées ; reconnaître un environnement ne le provisionne pas. Voir [RuntimeIdentity](../../src/LogicGate.SiteFactory.Runtime.Contracts/RuntimeIdentity.cs) et [configuration](../../src/LogicGate.SiteFactory.Runtime.Contracts/RuntimeConfiguration.cs). |
| Liveness/readiness | Adaptateurs à composer dans un host : [endpoints publics minimaux](../../src/LogicGate.SiteFactory.Runtime.AspNetCore/RuntimeHealthEndpoints.cs) et [contrat actuel](../03-generation/generated-health-checks.md). Ils ne livrent pas une application cliente. |
| Migrator | apply/bootstrap explicites, **Development/Test uniquement**, sur une base existante, avec confirmations et credential distinct. Voir le [guide](../03-generation/generated-explicit-migrator.md) et le [parseur](../../src/LogicGate.SiteFactory.Migrator/MigrationCommand.cs). |
| PostgreSQL de test | [Harnais REQUIRED](../../tests/PostgreSql/README.md) : instance Linux éphémère, TLS VerifyFull, 20 Facts Infrastructure + 3 Hosts, cinq shards et cleanup. Aucun provisioning de production. |

Le profil structurel docker-compose-vps est reconnu par la
[validation V1](../../src/LogicGate.SiteFactory.Application/Configuration/ClientConfigurationV1Validator.cs) ;
cette valeur ne produit ni Compose, ni Caddy, ni déploiement.

## Capacités non livrées

La distribution ne fournit pas de déploiement client, image applicative cliente,
stack Compose/Caddy, orchestration de maintenance/drainage, provisioning de rôles
de production, sauvegarde/restauration ou rollback de production. Aucun groupe
CLI de déploiement ni script opérateur généré n’est disponible. Ne pas déduire
une commande ou un fichier exécutable des noms conceptuels d’un ADR.

La [CI Windows/Linux/PostgreSQL](../04-quality-security/20-quality-gates-and-ci.md)
est opérationnelle, avec SourceLink VERIFIED ; elle valide le périmètre public,
pas un service client déployé. Ses preuves restent distinctes des preuves locales.

**P03-F1 reste OPEN / UNKNOWN** : historique 749 HTTP 200 / 19 HTTP 503, cause
non démontrée, risque accepté avec réserve ; réexamen **2026-11-16**, sans
prolongation tacite. Une CI verte ne démontre aucune correction causale.
Voir les [observations historiques](../../tests/readiness-concurrency-regression.md).

## Contrats et exigences applicables

### Isolation et environnements

Le profil cible Linux VPS / Docker Compose / Caddy suit
[ADR-0011 — décision](../adr/0011-docker-compose-vps-deployment-profile.md#15-décision)
et [ADR-0001](../adr/0001-one-repository-and-database-per-client.md).
Chaque client et environnement garde application, base, réseaux, volumes, secrets,
comptes, sauvegardes et clés Data Protection isolés. Aucun partage fonctionnel de
données, schéma, DbContext, credential ou compte administrateur entre clients.

- Development/Test : données fictives, recréables ; SMTP capturé/désactivé et
  providers de test, sans envoi réel involontaire. Aucun secret ou PII dans les logs.
- Staging : domaine, base, volumes, secrets et clés distincts ; données fictives
  ou anonymisation validée, jamais dump de production brut. Noindex, robots
  restrictif, absence de sitemap public et accès adapté. SMTP sandbox/allowlist,
  intégrations de test ; aucune clé de production réutilisée.
- Production : configuration explicite, HTTPS/domaine réels, aucun placeholder
  ou contenu de démonstration, logs structurés protégés et Debug désactivé par
  défaut. Données réelles exclues des tests, captures et prompts non autorisés.
  Backups, restauration, supervision et responsabilités doivent être définis.

La parité staging/production porte sur artefact testé, versions PostgreSQL,
modules, migrations, volumes et protections ; les différences légitimes de
domaine, ressources, données et secrets sont documentées. Une modification
durable du serveur revient dans la source de vérité, pas dans un conteneur édité.

### Artefacts, configuration et secrets

Appliquer [ADR-0012 — configuration et secrets](../adr/0012-runtime-configuration-and-secrets.md#16-décision)
et [ADR-0026 — images](../adr/0026-container-image-build-and-runtime-hardening.md#17-décision).
Promouvoir le même artefact testé, identifiable par version, commit et digest.
Conserver les anciennes images pendant la fenêtre de rollback, avec rétention
du registry bornée. Le build applicatif sur VPS de production est interdit dans
le flux normal ; seule l’[exception temporaire de secours strictement encadrée](../adr/0011-docker-compose-vps-deployment-profile.md#32-build-sur-le-vps)
reste applicable, sans devenir la baseline.
Images de base approuvées, build multi-stage Release, provenance/scans/signatures,
runtime sans SDK, utilisateur non-root, filesystem read-only et écritures limitées
aux montages prévus ; aucun privileged, Docker socket ou secret dans les couches.
Les détails de durcissement restent normatifs dans ADR-0026 et
[AGENTS](../../AGENTS.md#41-conteneurs), sans scan d’image prétendu livré ici.

Configuration structurelle et runtime restent distinctes ; options essentielles
validées au démarrage, sans valeur sensible dans les diagnostics. Les secrets
sont externes à Git et aux artefacts, propres au composant/environnement,
référencés et lisibles seulement par les identités nécessaires. Rotation sans
rebuild, puis révocation contrôlée des anciens accès. Ne pas publier une
configuration résolue, un environnement complet ou un hash de secret.

Le provisioning/rétablissement administrateur suit
[ADR-0021 — invitation ou récupération contrôlée](../adr/0021-administrator-provisioning-invitations-and-account-recovery.md#17-décision) :
aucun administrateur actif créé ou réinitialisé par seed automatique,
configuration ou démarrage. Ce contrat futur ne livre pas Identity dans le public.

### Préconditions et contrôle d’une opération de production

Le [plan exact et sa confirmation](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md#17-décision)
et le [verrou exclusif](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md#25-verrou-de-déploiement)
sont obligatoires. Identifier client/environnement, opérateur, versions source/cible,
commit/digests, composition, configuration, migration, backup, étapes, smoke tests,
risques et rollback ; valider hash et validité du plan avant application.

Avant toute mutation : autorisation humaine explicite, release exacte et tests/
scans applicables verts, artefacts actuel/cible disponibles, configuration et
secrets vérifiés sans affichage, volumes/permissions et espace suffisants,
migration inspectée, backup requis vérifié, fenêtre de maintenance et reprise
préparées. La recette staging est fortement recommandée pour toute évolution
sensible. Aucun déploiement implicite depuis génération, PR ou push main.

Le cycle cible suit [ADR-0032](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md),
avec les contrôles nécessaires à chaque profil : preflight/verrou,
[write freeze et maintenance edge vérifiée](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md#39-activation-sûre-de-la-maintenance),
[drainage borné des requêtes/jobs](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md#42-drainage-du-worker),
backup cohérent avant mutation, migration explicite de la release exacte,
activation, startup validation, smoke tests, réouverture contrôlée et stabilisation.
Pas de maintenance retirée automatiquement à échéance, de kill aveugle ou de
claim d’un contrat de job inconnu. Cette séquence n’est pas un outil exécutable.

Les [rôles PostgreSQL](../adr/0027-database-roles-and-runtime-least-privilege.md#17-décision)
restent séparés : Owner NOLOGIN, Migrator, Web/Worker à droits minimaux ;
runtime jamais superuser/owner. Aucune auto-migration ou réparation au démarrage.
Le Migrator livré n’est pas étendu à la production par ce contrat.

Toute erreur arrête la progression ; examiner étape atteinte, schéma/historique,
version réellement active et preuves avant reprise. Compose n’offre pas de
transaction globale. Annulation ou interruption ne signifie pas succès, et une
opération partielle n’est reprise que depuis un état connu.

Après activation : liveness et readiness distincts, attentes bornées, HTTPS/
certificat/headers et parcours des modules réellement concernés vérifiés.
Identity, formulaire, persistance et notifications ne sont testés que lorsqu’ils
existent, avec données d’essai contrôlées et supprimables. Surveiller ensuite
erreurs, redémarrages, base, disque et parcours critiques ; conserver l’artefact
précédent pendant la fenêtre de validation. Un health check immédiat ne suffit pas.

### Compatibilité et reprise

Le [rollback est une nouvelle opération planifiée](../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md#76-rollback),
pas un simple changement de tag. Vérifier schéma/données, configuration/secrets,
médias, jobs/outbox, cache, contrats API/CSP et disponibilité de l’ancienne image.
Expand-contract facilite une fenêtre compatible ; un changement exclusif peut
imposer correction forward, phase intermédiaire ou restauration contrôlée.
Aucun rollback automatique vers un schéma incompatible ; Down seul ne protège
pas les données. Évaluer la perte d’écritures postérieures au backup.

Les [sauvegardes et la réconciliation](23-backup-and-restore.md) conditionnent
la reprise des données. Garder maintenance et accès fermés tant que les
conditions de sécurité, compatibilité et smoke tests ne sont pas vérifiées.
Un échec de readiness ne justifie ni de le rendre artificiellement vert,
ni de réparer la base depuis le runtime.

### Infrastructure et cycle de vie

| Contrat cible | Source normative et compléments conservés |
|---|---|
| Réseau/VPS | [Baseline §49](../04-quality-security/18-security-baseline.md#49-vps) : correctifs, SSH par clés individuelles, pas d’exploitation root courante, accès Docker traité comme privilège élevé. Ports publics standards limités à SSH autorisé et HTTP/HTTPS (22/80/443) ; PostgreSQL et port applicatif restent privés. |
| Reverse proxy | [Baseline §50](../04-quality-security/18-security-baseline.md#50-caddy) : administration non publique, configuration valide avant reload, HTTPS/DNS/headers cohérents et logs sans données sensibles. Seul le web rejoint un proxy partagé ; DB/réseau client restent isolés. |
| Volumes et clés | [Stockage](../adr/0016-content-and-media-storage-boundaries.md#74-backup-postgresql-et-médias) et [Data Protection](../04-quality-security/18-security-baseline.md#37-data-protection) : persistance, permissions minimales, cohérence et sauvegarde ; aucune suppression implicite ou donnée permanente uniquement dans l’image. |
| Mutualisation physique | Évaluation des risques/ressources, noms Compose distincts (container_name évité par défaut), routes et certificats maîtrisés, logs/backups identifiables. Vérifier chaque stack ; aucun client ne doit saturer silencieusement les autres. VPS dédié à étudier pour données sensibles, charge ou obligation particulière. |
| Configuration changée | Une nouvelle image, variable, référence de secret, réseau ou montage peut exiger une recréation ; un restart ne garantit pas la prise en compte. L’action attendue et son contrôle doivent être documentés. |
| Arrêt/transfert/fermeture | [Contrat de handover](27-client-handover-and-service-closure.md) : backup/export final, obligations de conservation, responsabilités et accès transmis par canaux adaptés. Suppression de volumes/données explicite seulement ; fin de service ne désactive ni HTTPS ni protections. |

## Références et décisions restantes

Les [runbooks](24-operational-runbooks.md) précisent diagnostic, comptes rendus
expurgés et critères de validation. Les tests requis pour les futures capacités
restent dans la [stratégie de tests](../04-quality-security/19-testing-strategy.md#exigences-pour-capacités-futures) ;
les commandes locales sont maintenues dans [CONTRIBUTING](../../CONTRIBUTING.md#30-commandes-de-validation).
Les futurs scripts de déploiement sont testés sous Linux ; leurs outils destinés
au poste opérateur sont aussi vérifiés sur la plateforme Windows/PowerShell prévue.

Tout nouveau profil/fournisseur, registry, automatisation, exposition réseau,
mutualisation, méthode de secrets, version majeure PostgreSQL, modification de
volumes, suppression d’environnement, transfert d’exploitation, réduction de
backup ou politique de rollback exige la décision humaine prévue par
[AGENTS](../../AGENTS.md#10-actions-interdites-à-un-agent).
Aucune procédure client ou garantie de disponibilité n’est validée par ce guide.
