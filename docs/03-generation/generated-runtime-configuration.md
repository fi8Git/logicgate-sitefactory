# Configuration runtime typée publique

## Contrats et binding séparés

[Runtime.Contracts](../../src/LogicGate.SiteFactory.Runtime.Contracts/RuntimeConfiguration.cs)
contient `RuntimeIdentity`, `RuntimeEnvironment`, `RuntimeComponent` et
`RuntimeConfiguration`, immuables et sans IConfiguration, provider, I/O ou secret.
[Runtime.Hosting](../../src/LogicGate.SiteFactory.Runtime.Hosting/RuntimeConfigurationReader.cs)
possède le binding concret et les diagnostics. Les bibliothèques pures Runtime
et Runtime.Contracts ne référencent ni DI/Hosting ni PostgreSQL.

Les adaptateurs sont réutilisables ; la CLI Documentation ne génère pas un host
client ni des `appsettings` Business. Les exemples ci-dessous sont des entrées
de host générique, pas des fichiers produits par le template documentaire.

## Sources et snapshot

`RuntimeConfigurationReader.Read(configuration, hostEnvironment, component)`
lit la section fermée `SiteFactory:Runtime` :

```json
{
  "SiteFactory": {
    "Runtime": {
      "ClientSlug": "sample-project",
      "PublicBaseUrl": "https://example.test"
    }
  }
}
```

Seules `ClientSlug` et `PublicBaseUrl`, scalaires, sont autorisées dans cette
section. Propriété inconnue/imbriquée refusée ; les autres sections de host
ne sont pas rejetées globalement. Aucun secret ni connexion dans ce snapshot.
Les providers et leur priorité sont choisis par le host .NET ; le reader ne
les crée pas et n'introduit aucun fallback. JSON, environnement et arguments
ne sont pas une politique de fusion supplémentaire de SiteFactory.

L'environnement explicite vient de la clé `environment` et doit correspondre
à celui déjà fixé par le host. Son défaut implicite Production ne suffit pas.
Le composant `Web` ou `Worker` vient du composition root, pas de la configuration.
`RuntimeComponent` ne possède pas de valeur Migrator : son environnement et
sa configuration de connexion sont traités séparément par le Migrator.

## Validation fidèle

- Slug 3–80 caractères ASCII minuscules/chiffres, tirets simples internes ;
  noms réservés refusés, aucune normalisation silencieuse.
- Environnement Development, Test, Staging ou Production, insensible à la casse.
- URL HTTP(S) absolue, host explicite, sans credential, fragment ni whitespace.
- Production : HTTPS sur 443, non locale ; loopback, localhost et adresses
  d'écoute indéfinies refusés, sans URL de remplacement.
- Environnement contradictoire refusé.

Aucun appel HTTP/DNS ni validation d'un serveur de production.
Codes fixes `SF-CONFIG-STARTUP-001/002/003/005/006` :
section absente, propriété inconnue, valeur invalide, URL, environnement divergent.
Le diagnostic indique règle/propriété, jamais la valeur reçue.
Les erreurs de programmation ne deviennent pas des erreurs utilisateur.

## Disponibilité dans le host

`AddRuntimeStartupValidation` enregistre le binding et le pipeline.
Le [lifecycle](generated-startup-validation.md) publie les snapshots validés dans
la DI après validation ; une résolution anticipée est refusée (`SF-STARTUP-008`).
Modifier IConfiguration après validation ne modifie pas les objets immuables.
Aucun `IOptionsMonitor`, reload métier ni réécriture de configuration.

La [configuration PostgreSQL](generated-postgresql-runtime-configuration.md)
est un snapshot Infrastructure distinct, lu depuis un fichier secret externe
du seul composant. Elle n'est jamais ajoutée à RuntimeIdentity ou au lock.

## Preuves existantes et limites

Les [tests de binding](../../tests/LogicGate.SiteFactory.Runtime.Hosting.Tests/RuntimeConfigurationBindingTests.cs),
[tests de hosts](../../tests/LogicGate.SiteFactory.Runtime.Hosting.Tests/RuntimeConfigurationHostTests.cs)
et tests Runtime.Contracts couvrent valeurs valides/invalides, contradictions,
propriétés inconnues, immutabilité, diagnostics, composants et absence de side effect.
Ils sont autonomes, sans host Business ni connexion PostgreSQL réelle.
Preuves déjà acquises, non rejouées ici ; CI distante non validée.

Les règles suivent [ADR-0012](../adr/0012-runtime-configuration-and-secrets.md)
et [ADR-0031](../adr/0031-application-startup-validation-and-runtime-compatibility.md).
Ni environnement production inventé, secret généré, provider externe ni
déploiement client complet n'est fourni par cette bibliothèque.
