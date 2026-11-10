# Squelette applicatif : frontière des preuves publiques

## Ce qui est public

La solution SiteFactory contient douze projets de production et onze projets
de tests. Elle livre le moteur, DocumentationModule, les bibliothèques runtime
et PostgreSQL, et le Migrator explicite. Ce ne sont pas six projets d'application
cliente produits par la CLI publique.

La [génération publique](initial-generation-cli.md) crée la documentation
embarquée ; l'[E2E repository documentaire](repository-documentation-end-to-end.md)
utilise le même moteur via composition C#.
Les tests utilisent aussi `fixture-template`, une fixture publique en mémoire,
pour prouver une arborescence fictive indépendante d'une offre.
Sa solution vide et ses sources sentinelles ne constituent pas un projet .NET
compilable ; ce n'est pas un produit distribué par la CLI.

## Scénario public existant

Les [tests documentaires E2E](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.EndToEnd.cs)
créent un repository Git temporaire avec README et pages imbriquées, puis
scannent, construisent le modèle, composent Documentation et génèrent le site.
Ils vérifient home, navigation, liens, sources autorisées, préservation de `.git`,
régénération, deux racines et refus adversariaux.
Une contribution fictive publique utilise ensuite le même catalogue/exécuteur :
aucun chemin privilégié réservé à une famille de sites.

Les tests de runtime sont autonomes et ne nécessitent pas cette génération :
[configuration et lifecycle](../../tests/LogicGate.SiteFactory.Runtime.Hosting.Tests/RuntimeStartupLifecycleTests.cs),
[health HTTP](generated-health-checks.md),
[modèle/migrations](generated-runtime-database-model.md).
Ils construisent des hosts génériques et injectent les ports appropriés.
Un test offline avec reader contrôlé ne prétend jamais valider PostgreSQL.

## Preuve PostgreSQL distincte

Le [harnais public](../../tests/PostgreSql/README.md) possède deux inventaires
fermés : 20 Facts Infrastructure et 3 Facts Hosts.
Cinq shards séquentiels `g04/g05/p01/p02/p03`, cinq TRX admissibles.
La preuve P04 acquise est **23/23 Passed** sur Windows .NET vers PostgreSQL Linux
18.6, TLS VerifyFull et cleanup vérifié ; elle n'est pas rejouée ici.

P03 conserve 32 workers × 24 requêtes = 768 HTTP 200, 256 par route.
La startup Web, la readiness Worker, la sécurité, la non-mutation, l'annulation
et l'arrêt sont prouvés dans des hosts génériques, pas dans une DLL cliente privée.

P03-F1 reste **OPEN / UNKNOWN** : 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée ; risque accepté avec réserve, réexamen obligatoire
**2026-10-23**. Les campagnes vertes ne démontrent aucune correction causale.

## Historique privé et limites

Le scénario historique de solution cliente Domain/Application/Infrastructure/
Web/Worker/Migrator, ses commandes restore/build/test, migrations et véritables
processus clients restent dans la suite Business privée.
Ses compteurs, versions de template et assertions ne sont pas l'inventaire public.
Ils ne sont ni supprimés ni présentés comme rejoués ici.

La documentation statique publique ne possède pas de projet .NET à restaurer.
La solution publique elle-même a une preuve locale autonome séparée ;
CI distante Windows/Linux non validée. Aucun Identity, contenu métier, jobs,
Docker client, Caddy ou déploiement complet n'est annoncé.
