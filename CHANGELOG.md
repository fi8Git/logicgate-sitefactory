# Changelog

Les évolutions du périmètre générique de LogicGate SiteFactory sont décrites ici.
Le format s'inspire de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et le
versionnement suit [Semantic Versioning](https://semver.org/lang/fr/).

Aucune version officielle ni aucun package NuGet n'a été publié. Les capacités
ci-dessous sont développées dans les sources et restent sous `Unreleased`.
La version de développement définie dans `Directory.Build.props` est
`0.1.0-dev` ; les versions des descripteurs de modules et des templates sont
distinctes. Les repères SF-TASK retracent le développement, pas des releases.

## [Unreleased]

### Added

#### Socle et commandes publiques

- SF-TASK-001 à SF-TASK-009 : solution .NET 10, architecture Domain/Application/
  Infrastructure/CLI, nullabilité, analyzers, versions centralisées et dépendances
  verrouillées ; diagnostics structurés et commande `validate --config <path>`.
- Configuration YAML V1 : lecture UTF-8 bornée, propriétés et types stricts,
  validation déterministe, rejet des doublons, tags, anchors, aliases et
  structures dépassant les limites. Application et Domain ne dépendent pas du
  parser YAML.
- SF-TASK-062 : solution publique de 12 projets de production et 11 projets de
  tests ; CLI composée explicitement avec le module Documentation et
  `GenerationComposition` injectée, sans composition implicite d'une autre offre.

#### Planification et génération génériques

- SF-TASK-010 à SF-TASK-019 : contrats immuables `GenerationPlan`, identifiants
  typés, manifests déclaratifs versionnés, résolution explicite du template,
  compatibilité produit et ordre ordinal stable.
- `plan --config <path>` : chaîne réelle YAML → validation → résolution →
  plan → JSON UTF-8 déterministe. Aucun accès en écriture à la cible.
- SF-TASK-020 à SF-TASK-028 : exécuteur, racine confinée, écriture atomique par
  fichier et application des stratégies
  [generated/create-once/merge/custom/protected](docs/adr/0005-file-generation-strategies.md).
  Le merge non pris en charge et toute stratégie inconnue échouent sans overwrite.
- `generate --config <path> --output <directory>` : préflight avant toute
  écriture, matérialisation via les contributions enregistrées,
  `sitefactory.lock.json` déterministe et `generation-report.json` fondé sur
  les opérations réellement effectuées, sans secret ni chemin local.
- SF-TASK-030 puis SF-TASK-060 : état `.sitefactory/generation-state.json`,
  régénération à composition identique, vérification des derniers contenus
  générés, préservation des customizations et mise à jour des sources autorisées.
  Une modification locale conflictuelle n'est jamais adoptée ou écrasée implicitement.

#### Module SDK et composition

- SF-TASK-051 à SF-TASK-054 : frontière générique du moteur,
  `ISiteFactoryModule`, `ModuleDescriptor`, `ModuleId`, `ModuleVersion` et
  `ModuleCapability` ; catalogue explicite, diagnostics stables et contributions
  de génération indépendantes des classes concrètes.
- Versions SemVer exactes, descripteurs et capacités immuables, doublons et
  enregistrements incohérents refusés. Aucun scan de DLL ni téléchargement de
  module au runtime.
- Décisions de [séparation des repositories](docs/adr/0033-split-public-sitefactory-and-private-logicgate-modules.md),
  de [compatibilité explicite](docs/adr/0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md)
  et de [composition statique](docs/adr/0037-compose-modules-explicitly-without-dynamic-loading.md).
  Ce sont des décisions architecturales, pas l'annonce d'une publication.

#### Documentation depuis un repository

- SF-TASK-056 : module `documentation`, capability `documentation.generation`
  et template embarqué `documentation-minimal`, avec deux pages statiques.
- SF-TASK-057/SF-TASK-058 : scanner de repository Git local en lecture seule,
  sélection explicite des README Markdown et de `docs/**/*.md`, exclusions,
  métadonnées ordonnées, modèle en mémoire, accueil, pages, navigation et liens
  internes. Les sources ne peuvent pas sortir de la racine via traversal ou lien.
- SF-TASK-059 : module `0.2.0`, contribution depuis `DocumentationSite` et
  manifest de rendu `0.2.0` ; le template embarqué reste `0.1.0`. Layout,
  navigation, CSS et Markdown rendus par le même moteur avec lock, rapport et état.
  Markdig est confiné au rendu ; HTML brut désactivé, contenus actifs et
  destinations dangereuses refusés.
- SF-TASK-060 : E2E repository temporaire → scanner → modèle → plan → site,
  sans modification de `.git`, avec régénération et déterminisme entre deux
  racines. L'import de repository utilise les API C# ; aucune commande CLI
  dédiée à cet import n'est annoncée.

#### Bibliothèques runtime et PostgreSQL

- SF-TASK-062 : extraction des contrats runtime purs, de l'orchestrateur de
  startup et de la politique pure de compatibilité des bases, sans EF Core,
  Npgsql, IConfiguration ou Hosting dans ces bibliothèques pures.
- Adaptateur Hosting : binding en snapshot immuable, validations de démarrage,
  rapport structuré, annulation et état de readiness. Adaptateur ASP.NET Core :
  endpoints liveness/readiness minimaux, `Cache-Control: no-store`, décision
  cohérente par requête et absence de détails sensibles.
- Infrastructure PostgreSQL : configuration par composant, lecture bornée de
  fichiers secrets, diagnostics sans valeurs sensibles, factory EF, DbContext,
  mappings explicites de `DatabaseIdentity`/`SchemaState` et migration initiale
  du schéma runtime.
- Migrator distinct : `apply` et `bootstrap` explicites en Development/Test,
  contrôle du rôle et des memberships directs/transitifs, élévation Owner
  strictement contrôlée, bootstrap transactionnel et refus d'identité incompatible.
  Aucune migration ni réparation automatique par les services runtime.
- Reader PostgreSQL read-only, probe de compatibilité et intégration startup/
  readiness Web et Worker, avec délais bornés et annulation.
- Harnais public PostgreSQL REQUIRED : inventaires fermés de 20 Facts Infrastructure
  et 3 Facts Hosts, cinq shards séquentiels, TRX stricts, métriques allowlistées,
  TLS VerifyFull, ressources éphémères possédées et cleanup contrôlé.

#### Validation du périmètre public

- Tests de contrats, frontières architecturales, génération, chemins, contenu
  non fiable, confidentialité, annulation et concurrence.
- Scripts public-ci autonomes et workflow Windows/Linux préparé, avec job
  PostgreSQL REQUIRED distinct réutilisant le harnais. Résultats manquants,
  incomplets ou ignorés refusés ; un exit code seul n'est pas une preuve.
  La présence de ce workflow n'atteste pas une CI distante verte sur une révision
  future ni un SourceLink vérifié dans un repository public encore à créer.

### Changed

- SF-TASK-011 : constructeur `PlannedFile` rendu explicite pour l'identifiant,
  le chemin relatif, l'origine, la stratégie et l'action prévue, sans défaut caché.
- SF-TASK-062 : composition console publique injectée, projets et fixtures de
  tests publics autonomes, séparation du CHANGELOG et exemples d'ADR recentrés
  sur les contrats publics. Aucune publication déclenchée par ces adaptations.

### Fixed

- YAML : rejet cohérent des scalaires implicites non textuels lorsqu'une chaîne
  est attendue et diagnostic stable pour Unicode ou syntaxe invalides
  (corrections du Sprint 1).
- Plans : protection de tout segment `.git`, indépendamment de la casse,
  sans interdire `.gitignore`, `.gitattributes` ou `.github`
  (SPRINT-002-CORR-001).
- Exécution : limites physiques prévalidées avant la première écriture et état
  de génération corrompu traduit en diagnostic métier, sans absorption globale
  des erreurs de programmation (SPRINT-003-CORR-001/002).
- Processus de tests : nettoyage limité au parent et aux descendants possédés,
  même lorsque le parent est déjà terminé, sans kill global
  (SPRINT-006-CORR-001).

### Security

- Refus des traversals, chemins absolus, collisions portables, liens et
  substitutions non autorisées ; fichiers temporaires exclusifs et promotion
  atomique, sans écrasement silencieux des customizations.
- SQL des décisions de sécurité qualifié contre un `search_path` hostile,
  préfixe système `pg_` comparé littéralement et graphe Migrator strict ;
  garanties conservées dans les extractions génériques.
- Readiness : seuls les rapports complets sont publiés ; la réponse utilise
  le résultat de sa vérification, et l'annulation n'invente pas un succès.
- Secrets exclus des sorties publiques, logs, diagnostics, lock et rapports.

## Limites actuelles

Les plages de compatibilité et la matrice multi-versions de modules restent
une politique cible : l'API actuelle utilise des versions exactes.
Le merge structuré, l'upgrade/adoption automatique de sorties, le provisioning
et l'exploitation PostgreSQL de production, les déploiements et la publication
de packages ne sont pas présentés comme implémentés. Les commandes de génération
ne lancent pas automatiquement les quality gates du résultat.

Toute release future doit être explicitement approuvée, datée et vérifiée.
Aucun tag, URL de release ou succès CI inexistant n'est créé par ce journal.
