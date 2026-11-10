# DbContext et métadonnées runtime publics

[RuntimeDbContext](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/RuntimeDbContext.cs)
déclare exactement DatabaseIdentity et SchemaState dans
`LogicGate.SiteFactory.Infrastructure.PostgreSql.Persistence`.
Les mappings explicites et la migration initiale appartiennent à cette bibliothèque,
pas au template Documentation ni à un host client privé.

## Contrat de stockage

Les noms de schéma et de tables viennent d'[ADR-0031 §§36 et 42](../adr/0031-application-startup-validation-and-runtime-compatibility.md#36-database-identity).
Cette décision spécifique prévaut sur la possibilité générale d'utiliser `public`
évoquée dans l'ancienne documentation des données. Les objets sont explicitement
nommés en `snake_case` ; cette convention fait partie du contrat autonome de ce modèle.
Les chaînes ci-dessous ont un `HasMaxLength` et un type `character varying(n)`
explicites ; aucun type de colonne important n'est laissé à la convention EF.

| Table dans `sitefactory_runtime` | Propriété / colonne | Type PostgreSQL | Requis |
| --- | --- | --- | --- |
| database_identity | DatabaseInstanceId / database_instance_id | uuid, clé primaire | Oui |
| database_identity | ClientSlug / client_slug | character varying(80) | Oui |
| database_identity | Environment / environment | character varying(32) | Oui |
| database_identity | CreatedAtUtc / created_at_utc | timestamp with time zone | Oui |
| database_identity | ProvisioningVersion / provisioning_version | character varying(64) | Oui |
| database_identity | DatabaseOwnerRoleId / database_owner_role_id | character varying(100) | Oui |
| database_identity | Singleton (shadow EF) / singleton | boolean, défaut TRUE | Oui |
| schema_state | SchemaFamily / schema_family | character varying(100), clé primaire | Oui |
| schema_state | CurrentVersion / current_version | character varying(64) | Oui |
| schema_state | LastMigrationId / last_migration_id | character varying(150) | Non |
| schema_state | MigrationState / migration_state | character varying(32) | Oui |
| schema_state | UpdatedAtUtc / updated_at_utc | timestamp with time zone | Oui |
| schema_state | UpdatedByOperationId / updated_by_operation_id | uuid | Oui |

`DatabaseOwnerRoleId` et `UpdatedByOperationId` reprennent les deux champs d'ADR-0031
en plus du minimum de la tâche. Le premier est un identifiant logique borné,
pas un credential ou un OID PostgreSQL ; aucune gestion de rôles n'est ajoutée.
Le second est une référence d'opération fournie par le [bootstrap explicite SF-TASK-045](./generated-runtime-metadata-bootstrap.md),
sans créer une table d'audit ou une clé étrangère vers un modèle inexistant.

Le slug reprend la borne runtime de 80 caractères. Les familles et identifiants
logiques suivent la borne technique de 100 ; versions opaques bornées à 64,
environnement et état à 32. Le dernier ID de migration est borné à 150 caractères
et peut être absent avant toute migration. Ces champs ne déclenchent ni résolution
`latest`, ni comparaison de versions, ni validation de compatibilité.

## Clés, contraintes et temps

- `pk_database_identity` porte sur le GUID explicitement fourni, sans génération
  de valeur automatique. `ux_database_identity_singleton`, unique, associé à
  `NOT NULL` et `ck_database_identity_singleton` (`singleton = TRUE`), déclare
  au plus une identité dans la base. Le bootstrap Migrator crée cette identité ;
  le modèle ne la crée pas et ne prétend pas garantir l'existence d'une ligne.
- `pk_schema_state` utilise la famille comme clé naturelle : un état par famille,
  aucun index supplémentaire sans besoin de requête.
- Des checks nommés déclarent le refus des GUID vides et des chaînes vides après
  `btrim` ; les longueurs et la nullabilité sont explicites. Les six codes d'état
  sont ceux d'ADR-0031 §43 : `stable`, `migration-planned`, `migration-running`,
  `migration-failed`, `restore-pending`, `reconciliation-required`.
- Les instants sont des `DateTimeOffset` fournis par l'appelant. Les converters EF
  les normalisent à un offset nul, sans changer l'instant. Aucun appel à l'horloge,
  GUID aléatoire, défaut de date SQL ou remplacement implicite d'environnement.
  [Npgsql exige un offset nul pour ce mapping](https://www.npgsql.org/doc/types/datetime.html#timestamps-and-timezones).

## Factory, preuves et limites

[RuntimeDbContextFactory](../../src/LogicGate.SiteFactory.Infrastructure.PostgreSql/Persistence/RuntimeDbContextFactory.cs)
construit un contexte indépendant offline à partir de la configuration validée.
Le contexte possède sa connexion ; l'appelant doit le disposer.
Il ne migre, n'ouvre et ne provisionne rien implicitement.

[RuntimeDatabaseModelTests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/RuntimeDatabaseModelTests.cs)
inspecte le vrai modèle provider Npgsql : tables, schéma, types, longueurs,
nullabilité, clés, index/checks, absence d'entités métier/relations/seed,
conversion UTC. Ni InMemory ni SQLite, aucune connexion réelle :
déclarer les contraintes ne prouve pas leur exécution PostgreSQL.

[RuntimeMigrationTests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/RuntimeMigrationTests.cs)
vérifie migration/snapshot/SQL ; les preuves
[PostgreSQL publiques](../../tests/PostgreSql/README.md) prouvent séparément
droits, application réelle, valeurs, rollback transactionnel et probe read-only.
Les inventaires sont 20 Facts Infrastructure + 3 Hosts ; P04 23/23 local acquis,
non rejoué ici, CI distante non validée.

Le contexte n'est pas enregistré par les adaptateurs Web/Worker ; aucun endpoint,
startup validator ou health check ne migre, ne seed ou ne provisionne.
Seul le [Migrator explicite](generated-explicit-migrator.md) applique la migration,
puis le [bootstrap distinct](generated-runtime-metadata-bootstrap.md) initialise.
Aucun modèle métier, exploitation PostgreSQL production ou générateur public
d'application cliente n'est annoncé.
