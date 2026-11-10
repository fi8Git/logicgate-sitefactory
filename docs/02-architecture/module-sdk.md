# Contrat public du Module SDK SiteFactory

`LogicGate.SiteFactory.ModuleSdk` permet à une assembly externe de décrire un module
sans référencer Domain, Application, Infrastructure ou la CLI. Ce contrat initial
de SF-TASK-052 se limite aux métadonnées : il n'enregistre pas de services et
ne contribue pas encore de fichiers au plan de génération.

Il applique [ADR-0007](../adr/0007-module-composition-and-public-contracts.md),
notamment la composition statique et les contrats publics minimaux, ainsi que la
[frontière Core / Module](core-module-boundary.md). Le descripteur n'est pas le
manifest complet cible de l'ADR. Aucun ADR ni contrat CLI existant n'est modifié.

## Dépendances et utilisation externe

Le [projet SDK](../../src/LogicGate.SiteFactory.ModuleSdk/LogicGate.SiteFactory.ModuleSdk.csproj)
cible .NET 10, comme la solution. Il dépend uniquement de la bibliothèque standard
.NET : aucun `ProjectReference`, `PackageReference` ou framework ASP.NET Core.
Son lock NuGet ne contient aucune dépendance.

Un consommateur externe utilise le contrat public versionné, sans référence au
moteur interne. Après séparation physique, aucun `ProjectReference` entre
repositories n'est autorisé : distribution par packages selon
[ADR-0033](../adr/0033-split-public-sitefactory-and-private-logicgate-modules.md) et
[ADR-0035](../adr/0035-partage-public-component-via-nuget.md).
Aucun package n'est encore publié ; la consommation inter-assembly est démontrée
par les tests du SDK, pas par une installation NuGet déjà disponible.

```csharp
using LogicGate.SiteFactory.ModuleSdk;

public sealed class SampleModule : ISiteFactoryModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        new ModuleId("sample-module"),
        new ModuleVersion("1.0.0"),
        [new ModuleCapability("sample-module.read")]);
}
```

L'appelant reçoit explicitement l'instance et lit `Descriptor`. Il ne recherche
pas de DLL et ne scanne pas les assemblies. Le getter doit rendre des métadonnées
stables sans I/O, résolution de secret ou effet d'enregistrement. C'est une obligation
du fournisseur, pas une sandbox capable d'isoler du code arbitraire.

Le [projet de tests SDK](../../tests/LogicGate.SiteFactory.ModuleSdk.Tests/LogicGate.SiteFactory.ModuleSdk.Tests.csproj)
est une assembly consommatrice séparée. Sa seule référence de projet est le SDK ;
une implémentation de test utilise tous les contrats sans accès `internal` ni
`InternalsVisibleTo`. Cela prouve la consommation inter-assembly, pas une installation
NuGet ou un mécanisme d'extension runtime.

## Surface publique

| Type | Contenu |
|---|---|
| `ISiteFactoryModule` | Propriété en lecture seule `ModuleDescriptor Descriptor` |
| `ModuleDescriptor` | `Id`, `Version`, `Capabilities` en lecture seule |
| `ModuleId` | Valeur canonique d'identité |
| `ModuleVersion` | Version exacte du module |
| `ModuleCapability` | Identifiant qualifié d'une capacité déclarée |

Les trois valeurs sont des records scellés, avec `Value`, égalité ordinale et
`ToString()` retournant la valeur exacte. Les constructeurs refusent les valeurs
invalides par `ArgumentException` ou `ArgumentNullException` ; aucune correction,
conversion de casse ou suppression d'espaces n'est implicite. Ces erreurs concernent
la construction du contrat par son fournisseur ; elles ne changent pas les diagnostics
du chargement YAML existant.

## Identifiants et capacités

`ModuleId` utilise le même format que les identifiants de modules déjà acceptés par
la configuration V1 : 1 à 80 caractères ASCII minuscules ou chiffres, avec des tirets
simples entre segments. Tirets initiaux/finals, espaces, doubles tirets, séparateurs
de chemin, points et caractères Unicode sont refusés. La règle n'impose pas une lettre
initiale et ne dépend pas de la culture du processus.

`ModuleCapability` suit la convention `{module-id}.{capability}` d'[ADR-0006](../adr/0006-feature-flags-and-entitlements-separation.md).
Les deux segments utilisent chacun ce même format borné à 80 caractères, séparés par
un unique point (161 caractères maximum). Il n'y a pas de hiérarchie supplémentaire
dans ce contrat initial. Le préfixe doit correspondre exactement à l'ID du descripteur.

Une capacité ne constitue ni une permission, ni un entitlement, ni une décision
d'activation. Le SDK ne résout pas encore un catalogue de capacités ou de modules.

## Versions exactes

`ModuleVersion` valide la syntaxe [SemVer 2.0](https://semver.org/spec/v2.0.0.html) :
`major.minor.patch`, avec préversion et métadonnées de build facultatives. Les zéros
initiaux sont refusés dans les composantes numériques principales et les identifiants
de préversion purement numériques. Une plage, `latest`, une version flottante ou un
préfixe `v` ne sont pas des versions exactes.

Le SDK conserve toute la valeur, métadonnées comprises. L'égalité exprime l'identité
exacte, pas la précédence SemVer : `1.0.0+one` et `1.0.0+two` sont distinctes ici.
Aucun tri de versions, résolution de plage ou choix automatique d'une version n'est ajouté.

## Validation du descripteur

L'identifiant, la version et la collection de capacités sont obligatoires. Une collection
vide est autorisée ; les éléments nuls, doublons de valeur et capacités d'un autre module
sont refusés. L'unicité des IDs entre plusieurs modules relève du [catalogue Application](module-catalog.md),
pas d'un descripteur isolé.

La collection est copiée, triée avec `StringComparer.Ordinal` et exposée en lecture seule.
Modifier la collection d'entrée ne modifie donc pas le descripteur. Aucune information
temporelle, dépendance d'environnement ou référence de service ne fait partie du contrat.

## Vérification et limites

Les tests couvrent formats valides et invalides, limites, versions, doublons, propriété
des capacités, copie défensive, ordre indépendant de la culture et consommation par
une assembly séparée. Les frontières sont contrôlées dans les `.csproj`, les références
d'assembly et le résultat effectif du restore, y compris les dépendances héritées.
SF-TASK-052 ajoute `ModuleSdk → aucune couche`. SF-TASK-053 ajoute ensuite
`Application → ModuleSdk` pour le catalogue explicite ; le SDK reste autonome.

La génération continue à exiger `modules.enabled` vide. Le catalogue et les
contributions existent dans Application, pas dans le SDK ; Documentation les
consomme explicitement. Le SDK ne fournit ni DI, chargement dynamique,
installation NuGet runtime, feed ni marketplace. Les permissions, entitlements,
dépendances complètes et plages de compatibilité d'ADR-0036 restent des contrats
cibles distincts du descripteur minimal livré.
