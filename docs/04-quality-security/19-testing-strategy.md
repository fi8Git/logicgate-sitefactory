# LogicGate SiteFactory — Stratégie de tests

Cette stratégie distingue les **tests exécutés par la CI publique** des exigences
à appliquer à une future fonctionnalité. Les contrats normatifs restent utiles
même lorsqu’un outil ou composant n’est pas livré ; leur mention n’est pas une
preuve d’exécution. Voir le [périmètre actuel](../../README.md), les
[gates CI](20-quality-gates-and-ci.md) et la [baseline de sécurité](18-security-baseline.md).

## Tests actuellement exécutés

[SiteFactory.slnx](../../SiteFactory.slnx) contient douze projets de production et
onze projets de tests .NET 10. Les projets utilisent xUnit et les versions de
[Directory.Packages.props](../../Directory.Packages.props). Les assertions natives
restent explicites ; toute bibliothèque supplémentaire doit être approuvée,
notamment pour sa licence. Ne pas créer un projet ou une dépendance uniquement
parce qu’un exemple de structure future les mentionne.

| Parcours CI | Inventaire fermé attendu |
|---|---|
| Offline Windows | Onze suites, **2 726 Passed**, onze TRX |
| Offline Linux | Les mêmes onze suites et **2 726 Passed**, onze TRX |
| PostgreSQL REQUIRED Linux | **23 Facts réels**, 20 Infrastructure + 3 Hosts, cinq shards et cinq TRX |
| P03 dans REQUIRED | **32 × 24 = 768/768 HTTP 200**, zéro 503, 256 requêtes par route |

Les 27 TRX et rapports sont validés par le [harnais public](../../scripts/public-ci/README.md),
pas par le seul exit code. [Get-PublicCiProjects](../../scripts/public-ci/PublicCiProof.ps1)
fixe les comptes offline par projet. La discovery du build exécuté fournit ses
identités ; les Facts PostgreSQL de la [partition fermée](../../tests/PostgreSql/Public/shards.json)
sont recoupés puis exclus de l’offline. Les TRX sont comparés à cette discovery,
jamais utilisés seuls pour construire l’attendu. L’inventaire PostgreSQL garde
ses 23 identités explicites, vérifiées contre la discovery.
Ce sont des critères d’acceptation de campagne, pas de nouvelles mesures locales.
Le [README](../../README.md#ci-limites-et-publication) renvoie à une preuve publique
acquise ; les deux OS n’ajoutent pas deux ensembles de scénarios fonctionnels distincts.

### Comportements couverts dans le code courant

| Domaine | Scénarios et sources actuels |
|---|---|
| Contrats et configuration | Identifiants/versions, YAML V1 strict, types/clés inconnus, limites et diagnostics : [Domain.Tests](../../tests/LogicGate.SiteFactory.Domain.Tests/), [Application.Tests](../../tests/LogicGate.SiteFactory.Application.Tests/), [Infrastructure.Tests](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Configuration/). |
| Modules et architecture | Métadonnées immuables, catalogue exact, composition explicite, dépendances et cycles : [CoreModuleBoundaryTests](../../tests/LogicGate.SiteFactory.Application.Tests/CoreModuleBoundaryTests.cs), [ProjectReferenceArchitectureTests](../../tests/LogicGate.SiteFactory.Application.Tests/ProjectReferenceArchitectureTests.cs), [ModuleSdk.Tests](../../tests/LogicGate.SiteFactory.ModuleSdk.Tests/). Le SDK minimal n’expose pas tout le manifest cible. |
| Génération et CLI | validate/plan/generate, parsing/aide/version, codes de sortie, vrais processus, sorties JSON sans texte parasite, confinement, stratégies, état/hash, conflits, annulation et régénération : [Cli.Tests](../../tests/LogicGate.SiteFactory.Cli.Tests/), [tests de génération](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Generation/). |
| DocumentationModule | Scanner Git read-only, modèle, navigation/liens, rendu sûr et génération par les contributions existantes : [Templates](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Templates/), [Repositories](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/), dont [E2E](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.EndToEnd.cs). Scénarios inclus dans Infrastructure.Tests ; le module utilise le même moteur de génération. |
| Runtime et HTTP | Contrats purs, snapshot, pipeline startup/lifecycle, rôles des adaptateurs, liveness/readiness, annulation et décisions par requête : [Runtime.Tests](../../tests/LogicGate.SiteFactory.Runtime.Tests/), [Hosting.Tests](../../tests/LogicGate.SiteFactory.Runtime.Hosting.Tests/), [AspNetCore.Tests](../../tests/LogicGate.SiteFactory.Runtime.AspNetCore.Tests/). Les readers contrôlés offline ne prouvent pas PostgreSQL. |
| PostgreSQL | Modèle/migration initiale, configuration et diagnostics offline ; rôles, privilèges, Migrator/bootstrap, reader/probe et hosts sur DB réelle dans REQUIRED : [Infrastructure.PostgreSql.Tests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/), [Runtime.PostgreSql.Hosts.Tests](../../tests/LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests/), [harnais](../../tests/PostgreSql/README.md). La frontière entre tests offline et Facts PostgreSql reste explicite. |
| Sécurité et provenance CI | YAML du workflow, inventaire/composition/ressources/dépendances autorisés, TRX, PDB/SourceLink et binaires ; contre-épreuves de mapping sur de vrais PDB compilés, échecs/cleanup/non-divulgation et agrégation multi-tentatives : [Test-PublicCi.ps1](../../tests/PublicCi/Test-PublicCi.ps1). Ces contrôles de harnais ne sont pas une douzième suite ajoutée aux 2 726 cas. |

Le template public documentation-minimal produit un **site statique**, pas une
solution .NET cliente à compiler. L’import documentaire est une API C#, pas une
commande CLI supplémentaire. Les tests emploient aussi une fixture de topologie
avec sentinelles ; une solution vide ne prouve pas une application .NET compilable.
Les tests E2E ci-dessus prouvent le chemin scanner → modèle → contribution →
plan/exécuteur, avec lock, rapport, état et préservation de Git/customizations.
Ils ne prouvent ni Identity, ni application métier ou déploiement client.

## Principes de conception et cas négatifs

- Chaque règle importante doit avoir une preuve adaptée : types/compilation,
  validation, contrainte DB, test, analyse ou revue ; smoke test/runbook testé
  pour les opérations concernées. Une majorité de tests rapides, isolés et
  déterministes ne dispense pas des intégrations réelles critiques.
- Tester entrées, sorties, contrats, erreurs, effets de bord, sécurité et données
  persistées ; ne pas coupler la suite à une méthode privée ou un ordre interne
  sans effet observable. Préférer objet réel simple, fake déterministe, stub
  ciblé, puis mock lorsque l’interaction est le contrat. Ne pas mocker toute
  l’application ni changer le code de production seulement pour faciliter un mock.
- Nommer le scénario de manière cohérente, organiser contexte/action/résultat et
  produire des assertions utiles. Plusieurs assertions peuvent décrire une même
  intention ; un test paramétré ne doit pas mélanger des comportements indépendants.
- Pour tout cas d’usage important : nominal, entrée invalide/limites, dépendance
  absente, erreur technique, annulation et refus du résultat partiel. Pour une
  correction : reproduire, constater le test rouge, corriger, constater le vert
  et conserver le test ; toute absence de preuve automatisée doit être justifiée.
- Vérifier les technologies réelles : processus CLI pour stdout/stderr/exit code,
  pipeline HTTP pour ses réponses, exécution d’un conteneur lorsqu’il est concerné,
  et PostgreSQL réel pour ses contraintes, transactions, rôles ou migrations.
  Une méthode C#, un handler, un YAML ou un mock seul ne prouve pas cette intégration.

### Génération et stratégies de fichiers

Les tests couvrent validation et planification read-only, chemins logiques et
physiques, collisions, traversal, absolus, segments Git, liens, noms réservés,
Unicode, longueurs et différences Windows/Linux. Aucune écriture hors cible ;
préflight de toutes les destinations avant la première écriture. Un échec
intermédiaire ou une annulation ne transforme pas la sortie partielle en résultat
valide ; l’atomicité reste par fichier, pas une transaction filesystem globale.

| Stratégie [ADR-0005](../adr/0005-file-generation-strategies.md) | Garantie à tester |
|---|---|
| generated | Créer/remplacer seulement selon la propriété et le dernier hash connu ; détecter une modification locale, actualiser le hash sur succès. |
| create-once | Créer si absent, préserver l’existant sans modifier son contenu. |
| custom | Ne pas créer, modifier ou supprimer automatiquement ; préserver lors de régénération et futures évolutions. |
| protected | Refuser toute écriture, création comprise, avec diagnostic explicite. |
| merge | Actuellement refusé sans implémentation ; une future fusion doit prouver déterminisme, absence de doublons, conflits, ordre et syntaxe finale valides. |

Aucune option de force ne contourne ces stratégies. Tester état invalide,
changement tardif d’un fichier/hash, composition inconnue et conflit, jamais
adoption ou overwrite silencieux. Pour mêmes entrées/versions, comparer deux
générations et deux racines sur arborescence, contenus structurels, plan, lock,
composition et état ; normaliser seulement les champs opérationnels explicitement
non déterministes. Une modification voulue ne produit que les écarts attendus.

Les fixtures publiques et leurs oracles restent explicites, verrouillés et
indépendants des données trouvées à l’exécution. Un changement de valeur doit
provoquer une revue, pas un recalcul automatique de l’attendu. Golden files et
snapshots sont adaptés aux sorties structurées, avec données fictives, paths et
champs dynamiques normalisés ; leurs diffs sont expliqués, revus et jamais acceptés
massivement. Les exemples présentés comme exécutables doivent suivre le contrat
réel, et être vérifiés automatiquement lorsque cela est raisonnable.

## Isolation, déterminisme et technologies réelles

- Domain reste rapide, pur, sans DB, fichier, réseau, horloge réelle ou CLI.
  Application peut simuler ses ports, pas remplacer l’architecture par des mocks.
- Dossier temporaire distinct par test ; Git temporaire sans mutation du checkout.
  Ports dynamiques, DB/schéma ou nettoyage fiable isolés par suite. Restaurer les
  variables d’environnement, isoler les processus et limiter localement le
  parallélisme incompatible avec état statique, ports fixes ou ressources partagées.
  Pas de désactivation globale du parallélisme sans nécessité et décision explicite.
- Horloge contrôlée, identifiants stables, aléatoire contrôlé ou seed reproductible
  conservé dans le diagnostic admissible ; culture/fuseau/locale explicites quand
  pertinents. Ne dépendre ni de l’ordre des tests ni de l’ordre non contractuel
  d’une collection ; tester l’ordre lorsqu’il fait partie du contrat.
- Attentes conditionnelles avec bornes plutôt que Thread.Sleep ou longs délais
  arbitraires ; timeout raisonnable des intégrations longues, annulation et cleanup
  testés après échec. Chaque test doit pouvoir tourner seul, répété, sur machine
  propre et en parallèle lorsque c’est sûr.
- Données fictives uniquement : aucune donnée personnelle réelle, dump, contrat,
  credential client ou de production. Fixtures couvrant null/vide, Unicode/accents,
  tailles min/max, volumes, relations et dates limites ; builders lisibles qui ne
  masquent pas les valeurs importantes. Les secrets de test sont marqueurs fictifs
  ou éphémères bornés à une instance jetable, jamais secrets de production.
- La CI standard ne contacte pas SMTP, LLM, VPS, domaine public ou service payant
  réel et ne dépend pas de credentials personnels. Fakes, serveurs locaux ou
  adaptateurs déterministes conviennent aux ports ; le restore NuGet et l’obtention
  de l’image approuvée sont des préparations explicites, pas des appels métier.

### PostgreSQL REQUIRED

**Jamais SQLite, EF InMemory ou mock à la place de PostgreSQL REQUIRED.** Les
suites offline n’exigent ni Docker ni credential DB ; le filtre Category!=PostgreSql
exclut les Facts réels sans les compter comme réussis. Une campagne réelle est
explicite, après restore/build, avec Docker Linux, TLS VerifyFull, ressources
éphémères possédées et [partitions fermées](../../tests/PostgreSql/Public/shards.json).

Le [contrat du harnais](../../tests/PostgreSql/README.md) et
[CONTRIBUTING](../../CONTRIBUTING.md#302-postgresql-required-distinct) définissent
inventaires, budgets par shard et cleanup. Contraintes/types/index/FK, conversions,
relations/cascades, transactions, requêtes, concurrence, rôles et migrations sont
testés avec la technologie concernée, pas un provider de substitution. Les tests
de modèle EF offline ne certifient pas ces comportements DB. Vérifier la dérive
modèle/migrations ; ne pas tester un détail comme AsNoTracking sans contrat utile.

Une absence de secret/instance, un skip, un résultat absent/dupliqué/inattendu,
un TRX absent/invalide/incomplet, un exit non nul ou un cleanup incomplet garde
la campagne rouge. Nettoyer même après échec, sans kill global ni suppression
hors périmètre ; préserver les ressources préexistantes. Les preuves P04 locales
acquises restent Windows .NET contre PostgreSQL Linux, pas de nouvelles campagnes
rejouées par ce document ni une validation PostgreSQL natif Windows.

### P03-F1

**OPEN / UNKNOWN** : historique **749 HTTP 200 / 19 HTTP 503**, cause non démontrée,
risque accepté avec réserve ; réexamen **2026-11-16**, sans prolongation tacite.
Une campagne verte, y compris P03 768/768, ne prouve pas la cause corrigée. Conserver
les [observations historiques](../../tests/readiness-concurrency-regression.md),
la charge, les deadlines et les preuves rouges, sans retry jusqu’au vert.

## Rapports, diagnostic et gestion des défauts

Suivre le [contrat des preuves CI](20-quality-gates-and-ci.md#échecs-cleanup-et-diagnostic) :
projet/suite, plateforme, provenance, code de sortie, compteurs/outcomes et durée
réellement disponibles. Quand une version .NET/environnement est rapportée,
distinguer valeur déclarée et valeur mesurée ; pas d’environnement complet publié.
Les valeurs absentes restent inconnues, un test ignoré/non exécuté n’est jamais
Passed et un processus en échec reste FAILED malgré un TRX partiel. Aucun log brut,
exception, stack trace, argument, pièce jointe, secret ou donnée client dans les
preuves publiques ; garder des identités expurgées distinctes. Le diagnostic
ne doit pas empêcher le cleanup ; rétention proportionnée à utilité et sensibilité.

Un défaut décrit version/environnement, préconditions, reproduction, résultat
attendu/observé, sévérité, impact et preuve sûre ; le diagnostic d’une assertion
indique scénario et code d’erreur utile sans divulgation. Chaque code important
est testé sur catégorie/sévérité/emplacement et absence de secret. Prioriser selon
exploitabilité, projets touchés, présence en production, contournement et risque
de propagation, pas un score de couverture. Un vert ne prouve pas « tout testé ».

Un test instable doit être démontré, identifié, analysé, corrigé et suivi par une
tâche ; pas de suppression d’un test critique, Skip permanent ou retries aveugles.
Une éventuelle quarantaine exige raison, propriétaire, tâche, validation humaine
et expiration/condition de réactivation ; elle ne vaut pas succès du REQUIRED.
Toute relance diagnostique ciblée doit rester justifiée et ne masquer aucun rouge.

## Exigences pour capacités futures

Les fonctionnalités suivantes **ne sont pas des suites supplémentaires exécutées
par la CI actuelle**. Lorsqu’elles sont introduites, leurs contraintes normatives
restent à tester avec les ADR applicables et la [baseline sécurité](18-security-baseline.md).
Aucune présence de nom de classe, outil ou manifest dans une proposition ne
justifie de créer le fichier ou de déclarer un résultat acquis.

| Capacité concernée | Tests exigés lors de sa réalisation, sans résultat prétendu acquis |
|---|---|
| Catalogues/manifests enrichis et modules | Syntaxe/types, IDs uniques, versions/statuts, dépendances/cycles, héritage/capacités résolues, références pages/thèmes, permissions, enregistrements/routes, données/migrations, événements/logs, désactivation et upgrade. Tester dépendance présente, absente, incompatible et feature désactivée ; manifest invalide bloquant. Capabilities descriptives et autorisation restent distinctes. |
| Formats versionnés | Exemple minimal/complet par version supportée ; refus des requis absents, propriétés supplémentaires, type/valeur/enum/format/version inconnus, chemins et secrets. Compatibilité configuration/lock/manifests/rapports ; aucun JSON Schema absent inventé. |
| Identité et autorisation | [Authentification](18-security-baseline.md#15-authentification), [contrôles serveur](18-security-baseline.md#17-autorisation), [CSRF](18-security-baseline.md#18-antiforgery-et-csrf), [ADR-0020](../adr/0020-administrator-session-security-and-step-up-authentication.md) et [ADR-0021](../adr/0021-administrator-provisioning-invitations-and-account-recovery.md) : invitation/récupération, MFA/session/révocation, lockout/logout, erreurs génériques, dernier admin et absence d’inscription/seed automatique. Matrice anonyme/permission, compte désactivé, module/droit/flag absent et kill switch ; refus HTTP direct même si bouton masqué. API : 401 sans redirection login. |
| Formulaires, HTTP et APIs | Preuves positives et de contournement de la [baseline sécurité](18-security-baseline.md) : validation/limites, rate limit, idempotence, antiforgery, XSS/injections/SSRF, erreurs, MIME/statuts/cache/cookies/headers, CORS et CSP. Tester DB/SMTP indisponible et demande persistée malgré notification échouée ; différences de middleware Development/Production simulées et diagnostics sans PII. |
| Uploads et exports de données | Tester tous les contrôles [uploads](18-security-baseline.md#24-uploads-de-fichiers) et [exports](18-security-baseline.md#32-exports), y compris falsification extension/MIME/signature, tailles/nom/accès hostile, contenu vide/corrompu/exécutable, droits/volume/colonnes/encodage/culture/CSV, stockage privé, temporaires/cleanup et non-divulgation. |
| UI/design system, SEO et accessibilité | Tokens, composants/variantes/états disabled/loading/error, labels/ARIA/focus/contrastes/clavier, responsive mobile/tablette/desktop/large et textes longs. Vérifier title, description, H1, canonical, robots, sitemap, Open Graph, JSON-LD, slugs, redirections, liens/domaines et staging/production ; contenu factuel sans promesse de classement. Inspecter le rendu/HTTP ; revue manuelle clavier/lecteur d’écran et multi-navigateurs en complément. [CSP/JSON-LD](../adr/0022-browser-security-headers-and-content-security-policy.md#30-json-ld). |
| Jobs, intégrations et IA optionnelle | [Jobs/outbox](../adr/0017-background-jobs-and-reliable-side-effects.md), [transactions](../adr/0028-database-transaction-boundaries-and-concurrency-control.md), [egress](../adr/0025-third-party-integrations-and-egress-control.md), [IA](../adr/0015-deterministic-generation-and-optional-ai.md) : providers déterministes ; succès, absence/modèle/capability inconnus, réponse vide/JSON invalide, timeout/annulation, secret/PII/injection et information/chiffre inventé refusés, draft et publication interdite. Aucun appel réel en CI standard ; une suite dédiée autorisée exige budget, limites, secret dédié, données fictives et absence de production. |
| Migrations et upgrades | Base initiale et versions de départ officiellement supportées, données représentatives aux limites, dérive EF, locks, intégrité/transformations/erreurs. Comparer source/cible, configuration, customizations, packages, lock, build/tests, migrations et rollback local. Backup et scénario de restauration pour migration destructive ; Down ne prouve pas rollback des données. Aucune migration sur base de production pendant un test. |
| Application .NET générée | Quand un vrai template la produit : config/plan valides, génération via contrats publics sans raccourci, fichiers/lock/rapport/état, pas de secret/placeholder bloquant, restore verrouillé/build Release/tests, sécurité, reproductibilité et non-écrasement. Identity/SEO/Docker/CI seulement si présents ; une fixture sentinelle ou un site HTML n’est pas cette preuve. |
| Conteneurs, HTTPS et opérations | Tests réels des [Dockerfile/Compose](18-security-baseline.md#47-dockerfile) : image épinglée, non-root/read-only, aucun secret/DB public, réseau/ports/volumes/health, App_Data/Data Protection persistés ; proxy/forwarded headers, HTTPS, limites et connexions persistantes. Pas de certificat public requis en CI. Backup confiné avec rétention et exit non nul sur échec ; restore drill isolé vérifiant données/identités/rôles/versions/clés, réconciliation, démarrage et smoke tests. Déploiement futur : commit exact, migrations inspectées, backup, configuration, runbook, rollback compatible et recette ; jamais production sur PR. |

Les [gates de release et workflows futurs](20-quality-gates-and-ci.md#contrôles-futurs-non-livrés-par-ce-workflow)
restent distincts du pipeline livré. Une release doit tester Windows/Linux,
reproductibilité, customizations, migrations et sorties réellement concernées,
packages/scans et documentation ; jamais publier malgré test critique ignoré ou
risque bloquant. Les suites planifiées/navigateur/visuelles/mutation testing et
mesures de performance ne sont pas annoncées exécutées. Définir seuils par
fonctionnalité après baseline stable, analyser toute régression importante.
Aucune nouvelle version .NET ou plateforme obligatoire sans décision de support.

## Exécution et revue

Les commandes ne sont maintenues qu’à un endroit :
[CONTRIBUTING — validations locales](../../CONTRIBUTING.md#30-commandes-de-validation),
[guards/preuve isolée](../../CONTRIBUTING.md#301-guards-et-preuve-publique-isolée),
[PostgreSQL REQUIRED](../../CONTRIBUTING.md#302-postgresql-required-distinct).
Ces parcours sont distincts des jobs GitHub ; aucun n’est exécuté par la lecture
de ce guide. Pour une PR purement documentaire, appliquer les
[contrôles documentaires](../../CONTRIBUTING.md#34-pull-request-documentaire).

Avant modification, lire [AGENTS](../../AGENTS.md), contrats/ADR du domaine, code,
tests, fixtures/schémas/migrations présents, workflow et revue concernés. La revue
vérifie nominal, refus/limites/erreurs, annulation, permissions, persistance,
déterminisme, plateformes, cleanup et diagnostic. La recette humaine/visuelle,
accessibilité réelle et validation des contenus/responsabilités restent nécessaires
lorsqu’une application les expose ; elles ne remplacent pas les tests techniques.

Couverture et pourcentages ne prouvent ni pertinence ni absence de bugs/sécurité.
Les exclusions restent limitées et documentées ; baseline mesurée et validée
avant tout seuil bloquant. Aucun test sans assertion utile, snapshot accepté en
masse ou assurance de « tout testé » à partir d’une suite existante verte.

Un agent ne peut supprimer une régression ou un test de sécurité, affaiblir une
assertion, modifier un gate/seuil, technologie/outil structurant, exclusion de
couverture importante, parallélisme global, acceptation d’instabilité, service
externe CI, golden contractuel, matrice OS ou critère de release sans décision
humaine explicite. Il ne doit jamais présenter non exécuté/ignoré comme réussi.
Le compte rendu distingue tests/fixtures/golden modifiés, plateformes réellement
exécutées, résultats/compteurs/durées observés, couverture mesurée le cas échéant,
risques non couverts et décisions restantes. Un check obligatoire rouge bloque
l’intégration ; conserver tous les cas pertinents plutôt qu’un score artificiel.
