# Validation initiale du démarrage : contrats, pipeline et lifecycle publics

## Frontières

[Runtime.Contracts](../../src/LogicGate.SiteFactory.Runtime.Contracts/StartupValidation.cs)
définit `StartupValidationId`, `StartupValidationResult`,
`StartupValidationSeverity`, `RuntimeStartupReport`,
`IStartupValidation` et `IRuntimeStartupValidator`.
[RuntimeStartupValidator](../../src/LogicGate.SiteFactory.Runtime/RuntimeStartupValidator.cs)
orchestre sans I/O, IConfiguration, DI, Hosting ou provider concret.
[Runtime.Hosting](../../src/LogicGate.SiteFactory.Runtime.Hosting/RuntimeStartupService.cs)
adapte ces contrats au lifecycle .NET et au binding existant.

Une validation ne répare rien, ne crée aucune donnée et ne contacte aucun
provider métier. Le reader PostgreSQL read-only est un port séparé, pas du SQL
dans l'orchestrateur pur. Cette première coupe applique
[ADR-0031](../adr/0031-application-startup-validation-and-runtime-compatibility.md).

## Ordre, agrégation et immutabilité

Les contrôles sont copiés et triés par ID avec comparaison ordinale.
Registre non vide, IDs uniques, aucun élément nul ; résultats non vides,
attribués au bon contrôle. Contrat invalide : `SF-STARTUP-006`, erreur de
programmation/composition, pas une erreur de configuration absorbée.

Exécution séquentielle, sans horloge, découverte filesystem ou parallélisme.
Tous les contrôles indépendants continuent après un blocker afin d'agréger
les diagnostics, sauf annulation. L'ordre interne d'un résultat est préservé.
Le rapport copie les résultats en lecture seule, schéma 1 :
un warning reste ready ; un blocker, un rapport vide ou Cancelled interdit readiness.

Le token appelant est propagé. Une annulation demandée conserve les résultats
achevés, ajoute `SF-STARTUP-005` et `Cancelled=true`.
Les autres exceptions et les annulations étrangères au token ne sont pas
absorbées par un catch global.

## Binding et lifecycle

`RuntimeConfigurationStartupValidation` (`startup.configuration.schema`)
réutilise le [reader strict](generated-runtime-configuration.md).
Seule l'exception de configuration attendue devient un diagnostic bloquant ;
aucun snapshot de remplacement n'est fabriqué.

`AddRuntimeStartupValidation` enregistre des singletons : contrôle de
configuration, orchestrateur, `RuntimeStartupService`, snapshots validés et identité.
Le même service est enregistré comme `IHostedLifecycleService`.
`StartingAsync` précède tous les `IHostedService.StartAsync`, Kestrel compris.

1. Construire la DI n'exécute pas la validation et ne rend pas ready.
2. Le démarrage déclenche la validation et conserve le rapport complet.
3. Succès/warning : snapshots accessibles, services démarrés, ApplicationStarted.
4. Blocker local : `RuntimeStartupException`, aucun service démarré, aucun
   ApplicationStarted ni fallback.
5. ApplicationStopping retire readiness ; arrêt/libération restent au Generic Host.

Résoudre les snapshots avant validation est refusé (`SF-STARTUP-008`).
Ne pas les demander dans le constructeur d'un hosted service instancié en amont.
Le rapport de startup appartient au service ; les résultats sont immuables.
Les diagnostics sont journalisés par champs/niveaux structurés, sans valeurs sensibles.
`RuntimeStartupException.ToString()` n'ajoute pas de stack trace aux erreurs
attendues ; les erreurs de programmation restent visibles comme telles.

## Web, Worker et PostgreSQL

Le [Web](generated-web-postgresql-readiness.md) valide les prérequis locaux puis
la base. **Seul un échec de la phase DB**, après configuration valide, laisse
HTTP actif pour la liveness ; readiness/traffic restent bloqués.
Une configuration, un secret ou un rôle attendu invalide reste fail-fast.
Le rapport courant DB complet appartient à WebDatabaseReadiness, distinct
du rapport de startup ; chaque requête décide de son propre résultat.

Le [Worker](generated-worker-postgresql-readiness.md) conserve le fail-fast pour
tout blocker local ou DB, avant les services. Après succès il reste idle,
sans boucle artificielle, job ou polling. Sa readiness est interne et dépend
aussi des signaux du lifecycle.
[Health HTTP](generated-health-checks.md) est un adaptateur séparé.

Aucune migration, bootstrap, réparation, création de rôle/base, retry automatique
ou connexion de remplacement au démarrage.
Le lifecycle n'invente pas de garantie d'appels répétés/concurrents au startup ;
les contrôles de refresh Web ont leur propre contrat explicite.

## Preuves et limites

[Tests purs](../../tests/LogicGate.SiteFactory.Runtime.Tests/RuntimeStartupValidatorTests.cs)
et [tests lifecycle](../../tests/LogicGate.SiteFactory.Runtime.Hosting.Tests/RuntimeStartupLifecycleTests.cs)
prouvent ordre, agrégation, warning/blocker, contrat invalide, annulation,
exceptions, DI, snapshots et absence de side effects avec synchronisation déterministe.
Les preuves PostgreSQL des hosts sont [séparées](../../tests/PostgreSql/README.md).
Pas de rapport runtime persisté automatiquement, graphe de jobs, validation
de restore, module compatibility ou certification production complète.
Aucun test rejoué ici ; CI distante non validée.
