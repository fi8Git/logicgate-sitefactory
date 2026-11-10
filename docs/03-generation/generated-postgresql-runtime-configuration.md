# Configuration PostgreSQL publique et frontières des secrets

[PostgreSqlConfigurationReader](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Configuration/PostgreSqlConfigurationReader.cs)
et [PostgreSqlRuntimeConfiguration](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Configuration/PostgreSqlRuntimeConfiguration.cs)
appartiennent à Infrastructure.PostgreSql.Configuration.
Les contrats Runtime ne connaissent ni connexion concrète ni mot de passe.
Aucun fichier secret n'est fourni par le template Documentation.

Les adaptateurs Web/Worker exigent chacun leur référence de secret et un rôle
attendu indépendant, puis le probe réel. Un défaut local est fail-fast ;
un défaut DB laisse seulement le Web écouter pour live, sans readiness ni trafic.
Le Worker refuse tout blocker avant les services.

## Sources et isolation

Les variables suivantes contiennent uniquement le chemin d'un fichier secret
UTF-8 fourni par l'opérateur, jamais son contenu :

| Processus | Référence propre |
| --- | --- |
| Web | `SITEFACTORY_WEB_CONNECTION_FILE` |
| Worker | `SITEFACTORY_WORKER_CONNECTION_FILE` |
| Migrator | `SITEFACTORY_MIGRATOR_CONNECTION_FILE` |

Web et Worker utilisent le binding du host pour leur référence non sensible.
Leur composition root fixe le composant ; ni le YAML ni une option ne permettent
de sélectionner Migrator. La clé Migrator reste exclusivement dans son Program.cs,
pas dans le graphe de sources runtime. Aucun fallback vers une autre référence,
`ConnectionStrings`, `PGPASSWORD`, un passfile ou un credential codé en dur.

Le format reprend le fichier de connexion complet de SF-TASK-039, traité comme
un secret conformément à [ADR-0012 §§27–31](../adr/0012-runtime-configuration-and-secrets.md).
Il doit fournir explicitement `Host`, `Database`, `Username`, `Password`.
Aucun fichier secret, credential ou chemin local n'est généré dans appsettings,
le YAML, les samples, le lock ou le rapport. Les tests fabriquent leurs propres
valeurs éphémères dans leur sandbox temporaire, puis les détruisent.

Avant de démarrer un host consommateur Web/Worker, provisionner séparément le
fichier du composant et injecter sa référence dans son environnement de processus.
Le lancement sans cette préparation doit échouer. Ne pas fournir les valeurs en
arguments CLI ni réutiliser les identités entre composants ou environnements.
Monter seulement le fichier du composant ; des références distinctes ne peuvent
pas certifier que l'opérateur a fourni des valeurs réellement différentes.

Le lecteur n'écrit rien et respecte l'annulation. Limite : 16 KiB, UTF-8 strict,
une seule fin de ligne terminale LF/CRLF supprimée, sans Trim global ni normalisation
du mot de passe. Les liens/reparse points du chemin sont refusés. Sous Unix,
le fichier doit être en `0400` ou `0600` ; sous Windows, les ACL restrictives
relèvent de l'opérateur. La protection du répertoire et contre les échanges
concurrents de liens reste une responsabilité de provisioning, pas une garantie
de sandbox filesystem donnée par ce lecteur.

## Frontière du secret

`PostgreSqlRuntimeConfiguration` est une classe Infrastructure, pas un record.
La connexion est privée et la seule propriété publique est le composant.
`ToString()` et la sérialisation publique ne donnent ni mot de passe, ni chemin,
ni connexion. Le secret n'atteint aucun contrat Application ou Domain.

La configuration publique ne fournit **pas** CreateDbContext ni accès au secret.
[RuntimeDbContextFactory.Create(configuration)](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/RuntimeDbContextFactory.cs)
construit séparément un contexte non connecté dont le consommateur possède
la libération. Les API EF de ce contexte peuvent révéler la connexion :
ne jamais sérialiser ou journaliser le contexte ou ses options.
Le reader concret possède son accès interne Infrastructure, sans faire
remonter Npgsql ou les secrets dans Runtime.
Web/Worker ne demandent ni n'enregistrent un contexte EF pour leur startup.
Les snapshots deviennent disponibles après validation locale, distincte de
la compatibilité DB ; aucun reload ni effacement garanti des chaînes managées.

Les options Npgsql `Persist Security Info`, `Include Error Detail` et
`Log Parameters` actives sont refusées, ainsi que `Passfile` et `SSL Mode=Disable`.
Production exige `SSL Mode=VerifyFull`, refuse le nom `postgres`, `localhost`
et les placeholders connus dans les credentials. Les autres environnements
conservent le comportement TLS Npgsql configuré ; les tests utilisent VerifyFull.
Ce contrôle syntaxique ne certifie ni TLS réel, ni privilèges, ni identité de base.
Voir les [paramètres officiels Npgsql](https://www.npgsql.org/doc/connection-string-parameters).

## Erreurs et lifecycle

| Code | Cause |
| --- | --- |
| `SF-SECRET-001` | Référence obligatoire absente ou blanche. |
| `SF-SECRET-002` | Fichier vide. |
| `SF-SECRET-004` | Fichier illisible, UTF-8 invalide ou lien refusé. |
| `SF-SECRET-005` | Permissions Unix non conformes. |
| `SF-RUNTIME-CONFIG-004` | Connexion invalide, incomplète ou trop volumineuse. |
| `SF-RUNTIME-CONFIG-007` | Environnement non supporté. |
| `SF-RUNTIME-CONFIG-009` | Options sensibles ou configuration Production refusées. |

Les erreurs de document/lecture deviennent une exception applicative à message
fixe, sans inner exception contenant le chemin ou le contenu. L'annulation et
les erreurs de programmation ne sont pas absorbées par un catch global.
Web/Worker intègrent le résultat dans `startup.postgresql.configuration`, avec
chemin logique `PostgreSql`. Un blocker local empêche les services, Kestrel compris, sans activer readiness.
Le code de sortie d'un host consommateur dépend de son propre entrypoint ;
les anciens contrats d'exécutables clients restent privés. Les health checks ne publient toujours que leur
statut minimal, jamais le rapport interne.

Le Migrator appelle le même lecteur seulement après sa commande volontaire
validée et conserve `SF-MIGRATION-CONFIG-001`, exit `2`, en cas d'erreur de
configuration. Sa commande de migration existante n'est ni exécutée par cette
tâche ni étendue ; ses limites Development/Test restent inchangées.

## Preuves publiques et limites

[PostgreSqlConfigurationTests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSqlConfigurationTests.cs)
couvre références présentes/absentes, UTF-8, taille, fichiers/liens/permissions,
caractères spéciaux, isolation Web/Worker/Migrator, annulation, représentation
publique et diagnostics sans secret. La construction ne contacte aucun serveur.
Les tests de factory couvrent la responsabilité séparée du contexte.

Les preuves [réelles PostgreSQL](../../tests/PostgreSql/README.md) sont distinctes :
20 Facts Infrastructure + 3 Hosts, cinq shards, P04 23/23 acquis localement,
TLS VerifyFull et nettoyage vérifiés. Aucun test rejoué ici, aucune CI distante validée.
Ni base/rôle production, migration implicite, fallback localhost/credential
ou licence des sorties générées n'est introduit.
