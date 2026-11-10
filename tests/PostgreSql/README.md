# PostgreSQL réel — harnais public et preuves acquises

## Périmètre courant

Le harnais public est
[Public/Invoke-PostgreSqlTests.ps1](Public/Invoke-PostgreSqlTests.ps1), avec
[Public/PostgreSqlProof.ps1](Public/PostgreSqlProof.ps1) et
[Public/shards.json](Public/shards.json).
Il fonctionne avec les bibliothèques Infrastructure PostgreSQL/Migrator et les
hosts Web/Worker **génériques**, sans module métier, assembly client ou repository privé.

| Inventaire public fermé | Facts PostgreSQL REQUIRED |
|---|---:|
| LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests | 20 |
| LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests | 3 |
| Union inter-assemblies, identités distinctes | 23 |

| Shard, exécuté dans cet ordre | Lot | Assembly | Facts |
|---|---|---|---:|
| g04 | G04 | Infrastructure PostgreSQL | 5 |
| g05 | G05 | Infrastructure PostgreSQL | 3 |
| p01 | P01 | Infrastructure PostgreSQL | 8 |
| p02 | P02 | Infrastructure PostgreSQL | 4 |
| p03 | P03 | Hosts | 3 |

Les deux inventaires d'assemblies restent distincts. Chaque identité complète
appartient à un seul shard ; union exacte et intersections vides sont vérifiées
contre l'allowlist explicite, puis contre la discovery réelle. Aucun inventaire
attendu n'est déduit opportunément des tests trouvés.
Le filtre g04 utilise les cinq FullyQualifiedName exacts ; les autres filtres
sont `Category=PostgreSql&ExtractionLot=<lot>`, selon le JSON, pas une option
CLI de sélection libre.

## Exécution explicite, depuis la racine

Prérequis : SDK de global.json, PowerShell 7 et moteur Docker Linux pour REQUIRED.
Exemple de préparation et d'exécution d'une **future** campagne, non exécuté
lors de la présente adaptation documentaire :

```powershell
dotnet restore SiteFactory.slnx --locked-mode
dotnet build SiteFactory.slnx --configuration Release --no-restore
pwsh -NoProfile -File tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired
```

Paramètres publics réels : `-RepositoryRoot`, `-ManifestPath`, `-RunRequired`,
`-ProofDirectory`. Sans RunRequired, le script vérifie seulement la discovery
hors base et ne constitue **aucune preuve PostgreSQL**. Les binaires Release
doivent déjà être disponibles ; le harnais n'effectue pas leur build.
Il ne propose ni NoBuild, Shard, UseConfiguredInstance ni retry automatique.
Ces anciennes options appartiennent au harnais historique, pas au public.

## Instance éphémère, TLS et secrets

Baseline majeure : PostgreSQL 18, décision humaine historique du 2026-09-23.
La preuve P04 utilise **18.6 / server_version_num 180006**, image officielle :

```text
docker.io/library/postgres:18.6-bookworm@sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650
```

Un seul conteneur isolé et possédé par campagne, cinq shards séquentiels,
bases/rôles propres aux fixtures. Port loopback dynamique, stockage temporaire,
credentials aléatoires et certificats temporaires ; **TLS VerifyFull** avec
certificat approuvé et SAN loopback. Aucun latest, port DB public, secret
persistant, credential de production ou dépendance Docker de l'application.

La configuration administrative de tests est une **référence de fichier privé** :
`SITEFACTORY_TEST_POSTGRES_ADMIN_FILE`. Le harnais la fournit à ses processus ;
le secret n'est jamais un argument, log ou exemple copiable. Le gate exige
`SITEFACTORY_TEST_POSTGRES_REQUIRED=1`. Il ne remplace pas une absence de
configuration par localhost ou un credential par défaut.
Les fixtures refusent une cible non explicitement jetable, des noms/ACL
inattendus et un TLS incorrect ; cela ne certifie pas un cluster de production.

Sources : [configuration des tests](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlTestConfiguration.cs),
[gate](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlTestGate.cs),
[rôles et cleanup](roles.md).

## Fail-closed, TRX et conservation des preuves

360 secondes **par invocation dotnet test de shard**, pas un budget implicite
de 360 secondes pour la campagne. Budgets auxiliaires existants bornés :
discovery 180 s, commandes Docker 180 s, readiness PostgreSQL 60 s,
préparation 30 s, archivage 30 s, opérations cleanup 30 s,
cleanup de scope 10 s et marge transport du bridge 25 s.
Ce sont des budgets de harnais, pas un allongement des délais runtime.

Le mode REQUIRED exige les 23 résultats exacts, Passed, une fois chacun.
Secret absent/invalide, instance inaccessible, discovery vide/incomplète,
résultat manquant/dupliqué/inattendu, Failed/Skipped/NotExecuted/Aborted,
timeout, exit non nul, TRX absent/corrompu/incohérent ou cleanup incomplet :
**campagne FAILED**. Aucun succès déduit d'un seul exit code ni relance jusqu'au vert.
NotObserved ne distingue pas queued/running/unreported et ne prouve pas un blocage.

Chaque dossier `pg-proof-<identifiant>` est neuf, sous le parent ProofDirectory
(par défaut `artifacts/postgresql-public`). Il conserve manifest, discovery,
cinq preuves par shard, TRX natifs admissibles, preuves expurgées, métriques
autorisées et observations P03. Les TRX sont liés à l'invocation courante,
validés sur identités/multiplicités/résultats/compteurs/durées, jamais recyclés.

Avant archivage, les valeurs XML **décodées** sont contrôlées pour les marqueurs
sensibles. Un TRX détecté sensible n'est jamais archivé. Les incoming non
contrôlés sont nettoyés ; preuves rouges expurgées et manifeste FAILED sont
conservés. Un échec d'archivage après lancement reste FAILED, jamais NOT_RUN.

Les [tests offline des preuves](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlProofTests.cs)
contrôlent partitions, discovery, provenance/TRX et métriques ; les
[tests d'exécution du harnais](../LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlHarnessExecutionTests.cs)
contrôlent notamment supervision et contre-preuves.
Les métriques restent allowlistées pour G04/P01/P02 ; ne pas inventer de mesures
G05/P03. Les observations Activity P03 sont une preuve distincte.

## Cleanup et preuve locale acquise

Avant destruction : **zéro base, rôle et session possédés résiduels**.
Ensuite : suppression du conteneur exact, des certificats/secrets temporaires
et processus possédés ; ressources préexistantes conservées. Aucun kill global
ou nettoyage d'une base développeur/partagée/production.
Tout défaut de cleanup rend la campagne rouge.

P04 final a acquis **23/23 Passed**, zéro Failed/Skipped/NotExecuted/Aborted,
**cinq TRX natifs complets**, Windows .NET contre PostgreSQL Linux 18.6 réel,
TLS VerifyFull et cleanup vérifié. P02 avait aussi sa preuve indépendante
4/4 REQUIRED ; ces preuves ne sont pas de nouvelles exécutions documentaires.
[Budgets et attribution des mesures](ci-runtime-stabilization.md).

P03 : **32 workers × 24 = 768 HTTP 200**, zéro 503, 256 par route
`/`, `/health`, `/health/ready`. 768 requêtes et paires Activity distinctes
corrélées, 768 refresh.completed, 768 http.decision,
12 288 observations Npgsql filtrées et lifecycle complet, sans connexion
corrélée résiduelle. Pas de retry, baisse de charge ou délai augmenté.

**P03-F1 OPEN / UNKNOWN** : historique **749 HTTP 200 / 19 HTTP 503**,
cause non démontrée, risque accepté avec réserve. Propriétaire : responsable
technique LogicGate SiteFactory ; réexamen **2026-10-23**.
Les campagnes vertes et l'investigation terminée ne corrigent ni n'expliquent F1.
[Concurrence et limites causales](../readiness-concurrency-regression.md).

## Business historique : inventaire séparé

La suite historique privée conserve **20 Facts** et cinq anciens shards :
**core=12, migrator=2, compatibility=2, hosts=3, e2e=1**.
Elle inclut les vrais exécutables clients métier, génération/build/tests,
régénération et les deux tests du véritable processus Worker.
Ce n'est ni l'inventaire Infrastructure public de 20 ni celui inter-assemblies de 23.

Les scripts historiques du dossier parent ne constituent pas la commande
publique ; aucun lien vers un code, template, secret ou binaire privé n'est
nécessaire aux commandes ci-dessus. Les résultats historiques ne sont pas
recomptés comme publics. [Frontière E2E](foundation-e2e.md).

## Documents et CI

- [Rôles/default privileges](roles.md), [matrice et diagnostics](permissions-and-errors.md).
- [Migration/bootstrap explicites](migrations.md), [graphe Migrator](migrator-role-graph-regression.md).
- [Résolution hostile des catalogues](catalog-resolution-regression.md), [préfixe pg_](schema-privilege-regression.md).

Le workflow public Windows/Linux est préparé ; **CI distante non encore validée**
dans cette preuve documentaire. La preuve locale Windows/.NET vers serveur Linux
ne certifie ni .NET Linux distant ni PostgreSQL natif Windows.
Aucun PostgreSQL, test applicatif ou CI rejoué ici ; P05/P06 restent à revue humaine.
Hors portée : provisioning/exploitation production, backup/restore, upgrade
client, déploiement et fonctionnalités métier.
