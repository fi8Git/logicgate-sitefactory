# Dépendances de la fondation PostgreSQL publique

## Confinement réel

[Infrastructure.PostgreSql](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/LogicGate.SiteFactory.Infrastructure.PostgreSql.csproj)
porte les dépendances EF/Npgsql, la configuration secrète, le contexte, la factory
et le reader concret. Runtime.Contracts et Runtime sont purs, sans Npgsql, EF,
IConfiguration ou Hosting. Domain et Application génériques n'accèdent pas à
la connexion PostgreSQL.

| Référence directe Infrastructure.PostgreSql | Version centralisée |
|---|---|
| Microsoft.EntityFrameworkCore.Relational | 10.0.12 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 |
| Npgsql | 10.0.3 |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.12 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 |

Le projet référence Runtime.Contracts, Runtime et Runtime.Hosting.
La référence Npgsql est directe, nécessaire au reader/configuration, pas seulement
transitive du provider. EF Core/Abstractions/Analyzers sont apportés par la fermeture
EF ; aucune version flottante. Central Package Management et locks exacts résident
dans le checkout ; [Directory.Packages.props](../../Directory.Packages.props)
est la source de versions, pas les anciens compteurs d'une solution cliente.

`Microsoft.EntityFrameworkCore.Design` n'est pas une dépendance runtime.
Les sources de [migration](generated-explicit-migrator.md) sont figées dans
Infrastructure.PostgreSql ; aucun scaffolding automatique au lancement.
Le Migrator utilise la fermeture de cette bibliothèque, séparément des hosts.

## Supply chain et droits

Les sources et mappings NuGet sont explicites, sans élargissement implicite
vers un feed privé ou une source publique de remplacement.
Les locks se restaurent en mode verrouillé ; aucune mise à jour de package
n'est introduite par cette adaptation documentaire.
Les licences/notices tierces restent applicables ; une référence de package
ne signifie pas incorporation de son code source dans SiteFactory.

Les bibliothèques publiques sont implémentées mais aucun package SiteFactory
n'est encore publié. La distribution versionnée future suit ADR-0035 ;
aucun ProjectReference cross-repository après le split.

## Modèle, factory et hosts

[RuntimeDbContext](generated-runtime-database-model.md) fournit uniquement
DatabaseIdentity et SchemaState, dans sitefactory_runtime.
`RuntimeDbContextFactory.Create(configuration)` construit un contexte indépendant
sans connexion réseau ; le consommateur en possède la libération.
La configuration publique n'expose pas CreateDbContext ni la chaîne de connexion.

Web/Worker utilisent le port de lecture et le probe, pas un contexte EF dans
un endpoint ou dans l'orchestrateur pur. Ils ne migrent ni ne créent la base.
La CLI Documentation ne produit pas une solution .NET cliente PostgreSQL.

## Preuves et limites

[DependencyBoundaryTests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/DependencyBoundaryTests.cs)
contrôle projets, packages et fermeture effective ; les guards Runtime
préservent les bibliothèques pures. Les
[tests du modèle](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/RuntimeDatabaseModelTests.cs)
et de factory sont offline ; ils ne prouvent pas PostgreSQL.

Les preuves réelles sont [séparées](../../tests/PostgreSql/README.md) :
20 Facts Infrastructure + 3 Hosts, cinq shards publics, P04 23/23 Passed
acquis localement avec TLS VerifyFull et cleanup.
Aucun test rejoué ici, aucune validation CI distante prétendue.
Les E2E d'applications générées Business restent privés, inventaire distinct.
Pas de PostgreSQL production, données métier, provisioning client ou Docker client livré.
