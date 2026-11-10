# Bootstrap runtime public explicite

Le [Migrator public](generated-explicit-migrator.md) expose une opération
bootstrap distincte d'apply. Web/Worker ne créent, n'initialisent et ne réparent
aucune métadonnée. Le modèle public est inchangé : DatabaseIdentity et SchemaState.
Aucun nouveau grant n'est implicite.

## Décision et valeurs initiales

Décision humaine confirmée pour SF-TASK-045 :

| Champ | Origine / valeur |
| --- | --- |
| DatabaseInstanceId | UUID non vide fourni explicitement par l'opérateur |
| ClientSlug | Slug explicite, contrat runtime V1 : kebab-case minuscule, 3–80 caractères, noms réservés refusés |
| Environment | `Development` ou `Test`, explicitement choisi, sans normalisation |
| CreatedAtUtc | `TimeProvider.System.GetUtcNow()`, uniquement au premier bootstrap |
| ProvisioningVersion | `"1"`, version du contrat de bootstrap, indépendante du template |
| DatabaseOwnerRoleId | Nom du propriétaire `NOLOGIN` vérifié de la base sélectionnée, jamais un credential |
| SchemaFamily | `platform` |
| CurrentVersion | `"1"` |
| LastMigrationId | `20260923093309_CreateRuntimeMetadata`, historique exact vérifié |
| MigrationState | `stable` |
| UpdatedAtUtc | Même instant UTC que CreatedAtUtc |
| UpdatedByOperationId | UUID non vide de l'opération initiale, fourni explicitement |

Cette baseline précise les exemples d'[ADR-0031 §§36–43](../adr/0031-application-startup-validation-and-runtime-compatibility.md#36-database-identity)
pour le modèle initial, sans ajouter de famille métier ni de politique d'upgrade.
Le modèle et la migration SF-TASK-038/039 ne sont pas modifiés.

## Invocation volontaire

Après le [provisioning des rôles et l'application explicite de la migration](./generated-explicit-migrator.md),
depuis le checkout, après compilation du Migrator public :

```sh
dotnet run --project src/LogicGate.SiteFactory.Migrator --configuration Release --no-build --no-restore -- bootstrap --environment Test --client-slug example-client --database-instance-id 11111111-1111-4111-8111-111111111111 --operation-id 22222222-2222-4222-8222-222222222222 --confirm-bootstrap
```

Les UUID et le slug ci-dessus sont fictifs : fournir les identifiants attendus de
la base isolée, pas des valeurs par défaut. Syntaxe exacte, options dans cet ordre.
`--help` présente les deux commandes. Sans confirmation, identité valide ou toutes
les options, refus avant lecture de secret. Pas de `--force`, auto-apply ou fallback.

`SITEFACTORY_MIGRATOR_CONNECTION_FILE` référence le fichier secret externe du seul
Migrator. Même préflight SF-TASK-044 : authentification par login Migrator distinct,
appartenance contrôlée, `SET ROLE` du owner `NOLOGIN`, connexion non poolée fermée
en sortie. Jamais de connexion directe owner, runtime ou superuser acceptée.
Le bootstrap ne crée ni base, schéma, historique, rôle ou grant.

## Transaction, cohérence et idempotence

L'historique doit contenir exactement la migration initiale. Après cette première
vérification, une transaction explicite `READ COMMITTED` prend les verrous dans
l'ordre historique EF → DatabaseIdentity → SchemaState et revérifie l'historique.
Le verrou `SHARE ROW EXCLUSIVE` sur ces trois tables sérialise les bootstraps,
y compris lorsque les tables sont vides, et empêche leurs modifications concurrentes.
Les lectures ordinaires restent possibles. `lock_timeout = 3s` et
`statement_timeout = 10s` sont locaux à la transaction, sans retry automatique.
Il s'agit de l'exception procédure opérateur d'[ADR-0028 §46](../adr/0028-database-transaction-boundaries-and-concurrency-control.md#46-verrous-de-table),
pas d'un mécanisme de transaction métier général.

Si les deux tables sont vides, deux étapes `SaveChanges` sont couvertes par **un
seul commit** : identité, puis état. Tout refus, échec de la seconde étape ou
annulation avant commit annule l'ensemble. Aucune initialisation partielle n'est
conservée. Aucun appel externe ni écriture filesystem n'a lieu dans la transaction.

Si les deux lignes existent et correspondent exactement à cette baseline et à
l'identité attendue, succès sans UPDATE. Les dates, le propriétaire et l'identifiant
d'opération initial restent inchangés, même si une nouvelle invocation fournit un
autre OperationId. Aucune nouvelle ligne n'est créée. Un client, environnement ou
DatabaseInstanceId différent est refusé. Un état partiel, une famille/version/état
inattendu, un owner divergent ou des dates incohérentes est refusé sans réparation.
Une perte de connexion au commit peut rendre son résultat incertain : inspecter
l'état puis réexécuter avec la même identité, jamais le remplacer silencieusement.

## Sorties stables

Succès : exit `0`, stdout `Runtime metadata bootstrap completed.`, stderr vide.
Ces codes appartiennent au Migrator public, pas à la CLI SiteFactory.

| Diagnostic | Exit | Situation |
| --- | --- | --- |
| SF-BOOTSTRAP-USAGE-001 | 2 | Arguments / identité / confirmation invalides |
| SF-MIGRATION-CONFIG-001 | 2 | Connexion dédiée absente, invalide ou illisible |
| SF-MIGRATION-MODEL-001 | 1 | Composition embarquée divergente |
| SF-DB-MIGRATOR-003 | 1 | Login / propriétaire / graphe d'appartenances non conformes |
| SF-BOOTSTRAP-HISTORY-001 | 1 | Migration initiale absente ou historique incompatible |
| SF-BOOTSTRAP-CLIENT-001 | 1 | Client différent |
| SF-BOOTSTRAP-ENVIRONMENT-001 | 1 | Environnement différent |
| SF-BOOTSTRAP-INSTANCE-001 | 1 | DatabaseInstanceId différent |
| SF-BOOTSTRAP-STATE-001 | 1 | Métadonnées partielles ou incompatibles |
| SF-BOOTSTRAP-FAILED-001 | 1 | Échec du provider |
| SF-BOOTSTRAP-CANCELLED-001 | 130 | Annulation demandée |

Erreurs uniquement sur stderr, messages fixes : ni SQL, valeur d'identité,
connexion, chemin, secret ou stack trace. Seules les exceptions du provider
(dont DbUpdateException avec cause DbException) et l'annulation demandée sont
traduites ; les erreurs de programmation ne sont pas absorbées globalement.

## Preuves publiques et limites

[PostgreSqlBootstrapTests](../../tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/PostgreSql/PostgreSqlBootstrapTests.cs)
utilise le Migrator et le modèle publics : première initialisation,
seconde identique/nouvelle opération sans réécriture, trois mismatches,
état partiel/incompatible, refus Web/Worker et rollback de seconde écriture.
La base jetable reçoit un trigger de test refusant SchemaState ; une séquence
confirme la première insertion avant échec, puis les deux tables doivent être vides.
Les sources de production ne sont pas modifiées.

G04 conserve également les refus du graphe Migrator et du search_path hostile
sur les deux chemins apply/bootstrap, avant toute mutation.
Les tests offline d'arguments, configuration et annulation sont distincts.
Le [harnais public](../../tests/PostgreSql/README.md) exige 20 Facts Infrastructure
et 3 Hosts en REQUIRED ; P04 23/23 acquis, non rejoué ici.
Les E2E clients Business et leurs compteurs restent privés.

Le bootstrap reste réservé au développement/test isolé. Le libellé environnement
ne certifie pas le serveur : l'opérateur vérifie sa cible.
Pas de Staging/Production, changement d'identité, clone/restore, Deletion Ledger,
upgrade ou réparation. Le probe/readiness consomme les métadonnées sans les modifier.
CI distante non validée ; aucune exploitation PostgreSQL client complète annoncée.
