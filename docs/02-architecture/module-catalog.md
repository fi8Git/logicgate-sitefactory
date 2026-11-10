# Catalogue explicite de modules — SF-TASK-053

Le catalogue Application résout les métadonnées d'un module par `ModuleId`, sans
connaître sa classe concrète. Il reçoit des instances explicitement fournies par le
composition root et réutilise le [Module SDK](module-sdk.md), sans modifier ses
contrats publics. Aucun module n'est activé dans la CLI ou la génération par cette tâche.

SF-TASK-054 utilise ensuite ce catalogue dans la [composition de génération](core-module-boundary.md#découplage-livré--sf-task-054)
pour vérifier les propriétaires et versions des contributions de templates. Cela
n'active pas de module métier dans la configuration ou le lock du projet client.

La seule nouvelle référence de production est `Application → ModuleSdk`. Le SDK
reste indépendant du moteur. Aucun projet, package, accès disque, chargement de DLL
ou enregistrement de services n'est ajouté.

## Composition et résolution

Avec le `SampleModule` défini dans la documentation du SDK :

```csharp
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.ModuleSdk;

IModuleCatalog catalog = new ModuleCatalog([
    new ModuleRegistration("sample-module", "1.0.0", new SampleModule()),
]);

ModuleResolutionResult result = catalog.Resolve(new ModuleId("sample-module"));
// result.IsSuccess ; result.Module contient le ModuleDescriptor capturé.
```

`ModuleRegistration` déclare un ID attendu, une version exacte attendue et une
instance `ISiteFactoryModule`. Ces déclarations brutes sont validées lors de la
construction du catalogue, puis comparées au descripteur fourni. Elles ne sont pas
un second modèle de manifest : elles fixent les identités attendues par la composition.

La syntaxe est celle du SDK, sans nouvelle regex ni conversion implicite : ID canonique
et SemVer exacte, métadonnées de build comprises. Le SDK refuse déjà un descripteur
invalide à la construction ; les déclarations permettent donc de diagnostiquer une
version d'enregistrement invalide sans intercepter arbitrairement les exceptions du module.

`IModuleCatalog` expose `IsValid`, `Modules`, `Capabilities`, `Diagnostics` et `Resolve`.
Une résolution réussie contient uniquement le descripteur capturé ; elle n'exécute
pas de fonctionnalité du module. Un catalogue vide est valide, mais ne résout aucun ID.

## Validation et diagnostics

Les codes suivent [ADR-0007, section 64](../adr/0007-module-composition-and-public-contracts.md).
Les diagnostics réutilisent les contrats Application existants, avec sévérité `Error`.

| Code | Condition | Chemin logique |
|---|---|---|
| `SF-MODULE-001` | ID inconnu dans un catalogue valide | `modules[id]` |
| `SF-MODULE-002` | ID déclaré invalide | `modules.id` |
| `SF-MODULE-003` | Version déclarée invalide | `modules[id].version` |
| `SF-MODULE-005` | ID déclaré plusieurs fois, même avec des versions différentes | `modules[id]` |
| `SF-MODULE-029` | Registration nulle, instance/descripteur absent ou identité/version incohérente | `modules` ou `modules[id]` |

Pour chaque entrée : présence de la registration, ID, version, puis instance/descripteur.
Un ID valide est comptabilisé pour les doublons même si sa version est invalide.
Les erreurs identiques sont dédupliquées ; leur ordre est celui du contrat existant :
chemin logique, code, message (comparaisons ordinales), puis sévérité. Aucun index
d'entrée ni valeur brute invalide n'est inclus dans les diagnostics.

Toute erreur bloque le catalogue entier : listes de modules/capacités vides,
résolution en échec avec les diagnostics de composition, sans résultat partiel
ni choix implicite d'une version parmi des doublons.

Une collection de registrations nulle ou un ID de recherche nul reste une erreur
de programmation. Les exceptions du getter `Descriptor` ne sont pas absorbées.

## Déterminisme et limites

Le catalogue copie la liste d'entrée et lit `Descriptor` une fois par registration
dont l'ID et la version sont valides. Les descripteurs du SDK sont immuables ; changer
ensuite l'instance ou la collection fournie ne modifie pas le catalogue. Une recherche
ne rappelle jamais le module.

Les modules sont triés par ID et les capacités par valeur avec `StringComparer.Ordinal`,
dans des collections en lecture seule. Les diagnostics sont également ordonnés : aucune
dépendance à la culture, au filesystem, à la réflexion ou à l'ordre d'un dictionnaire.
Les tests vérifient les permutations de compositions valides et invalides.

Le getter reste soumis au contrat du SDK : métadonnées stables, sans I/O ni effet de
bord. Ce n'est pas une sandbox pour plugins non fiables. La génération exige toujours
`modules.enabled` vide. Résolution de dépendances, plages de versions, contributions,
téléchargement NuGet, marketplace et hot reload restent hors portée.
