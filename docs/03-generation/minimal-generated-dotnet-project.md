# Sortie .NET minimale et frontière du produit public

## Générateur public et fixture d'arborescence

Le moteur est générique : il matérialise le `GenerationPlan` du template résolu,
pas une topologie codée en dur. La CLI publique sélectionne Documentation ;
elle ne génère donc pas une application Business .NET.

La [fixture publique réelle](../../tests/fixtures/public/PublicGenerationFixture.cs)
déclare `fixture-module`, `fixture-template` et des fichiers sentinelles
reprenant une arborescence de projet. `Client.slnx` est une solution vide,
Program.cs un commentaire et le lock de test un objet vide : cette fixture
ne prouve ni la génération d'une application .NET compilable ni son build.
Elle appartient aux tests, utilise les mêmes contrats et ne charge aucun
manifest, binaire ou module privé.
Elle démontre une capacité du moteur, pas une nouvelle offre CLI.

L'[E2E public](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.EndToEnd.cs)
génère Documentation puis cette fixture dans la même composition, vérifie
`Client.slnx`, le lock et la préservation d'une customization README.
Aucun fichier du résultat n'est écrit manuellement pour fabriquer la preuve
de génération ; seule la customization attendue est modifiée après génération.

## Frontières architecturales conservées

Le moteur ne choisit ni nom de projet client, ni package métier, ni schéma DB.
Le manifest/contributeur possède ces choix et les stratégies de fichiers.
Les tests d'architecture inspectent directement les `.csproj`, références
exactes et cycles ; un build seul ne prouve pas les frontières.

Le contrat architectural d'une application multicouche reste :
Domain sans couche externe ; Application → Domain ;
Infrastructure → Application/Domain ;
Web et Worker → Application/Infrastructure ;
Migrator explicite séparé, jamais dépendance de Web ou Worker.
Cette topologie historique Business reste privée, non une sortie Documentation.

Les bibliothèques publiques extraites respectent leurs propres frontières :
Runtime.Contracts et Runtime purs ; adaptateurs Hosting/ASP.NET Core séparés ;
EF/Npgsql confinés à Infrastructure.PostgreSql ; DDL explicite dans Migrator.
[Persistance publique](generated-persistence-dependencies.md).

## Génération et validation

L'[exécuteur](initial-generation-cli.md) applique les stratégies canoniques :
ordre déterministe, UTF-8/LF, chemins sûrs, lock/rapport/état, préflight complet.
Aucune dépendance de la sortie n'est restaurée ou exécutée implicitement.
L'opérateur ou le test doit restaurer, compiler Release et tester séparément
une éventuelle sortie .NET ; aucune preuve InMemory ne valide PostgreSQL.

La [régénération](regeneration-and-end-to-end-proof.md) exige l'état reconnu
et protège les fichiers client. Pas d'adoption/upgrade implicite, pas de
transaction filesystem globale. Une modification locale gérée est un conflit.

## Historique et limites

La génération réelle d'une solution cliente .NET complète a été démontrée dans
les sprints historiques Business. Ses versions, manifests, fichiers sensibles,
tests générés et E2E sont conservés privés, sans lien obligatoire depuis le public.
La fixture d'arborescence publique ne remplace ni ne supprime ces scénarios.

Le site documentaire public est statique : pas d'Identity, API métier, PostgreSQL
client provisionné, Worker métier, image Docker ni déploiement.
Les bibliothèques runtime/PostgreSQL sont réutilisables mais leur présence
n'annonce pas leur assemblage dans un template public d'application cliente.
CI distante et publication NuGet restent à valider/réaliser.
