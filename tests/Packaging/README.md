# Preuve locale de consommation du ModuleSdk

Cette preuve SF-S1-001 construit une préversion de **LogicGate.SiteFactory.ModuleSdk**
dans un feed de fichiers temporaire puis la consomme depuis un projet .NET indépendant.
Elle ne publie rien et ne modifie aucun projet du moteur.

Le SDK couvre les métadonnées des modules et, avec SF-S1-002.1, les déclarations
de fournisseurs de templates. Il ne fournit pas l’adaptateur Application, le
moteur de génération, la propriété des fichiers ou l’intégration Documentation.
Aucun chargement dynamique de DLL n’est utilisé.

## Exécution

Depuis un checkout public de fi8Git/logicgate-sitefactory :

```powershell
pwsh -NoProfile -File tests/Packaging/Test-ModuleSdkPackage.ps1
```

Version explicite par défaut : **0.1.0-localproof.1**. Une autre préversion de
preuve peut être choisie ; seules les versions major.minor.patch-localproof.N
sont admises, jamais une version stable ou flottante :

```powershell
pwsh -NoProfile -File tests/Packaging/Test-ModuleSdkPackage.ps1 -PackageVersion 0.1.0-localproof.2
```

Prérequis : PowerShell 7.4 ou ultérieur, Git et SDK .NET 10 compatible avec
[global.json](../../global.json), avec les packs de référence net10.0 installés.
Le SDK sélectionné est mesuré ; le runtime réellement chargé par le consommateur
est rapporté séparément. Aucun Docker, PostgreSQL, credential ou feed distant
n’est requis par la preuve.

Les entrées du SDK, les propriétés communes, global.json, LICENSE et NOTICE
doivent correspondre au HEAD vérifié. Une modification de ces entrées est refusée
pour ne pas attribuer du code non committé au RepositoryCommit du package.
Des changements étrangers à ces entrées sont préservés. Le script de preuve et
ce document peuvent être examinés/exécutés sans créer de commit.

L’inventaire de référence vient de l’arbre du commit (`ls-tree`), pas de l’index.
Le SDK est aussi énuméré physiquement, y compris les sources ignorées par Git,
sans suivre les liens ; seuls les dossiers de build bin/obj sont exclus.
Chaque entrée protégée est comparée à son identifiant de blob Git, calculé sur
les octets effectivement lus. Aucun diff/stat cache, marqueur assume-unchanged
ou skip-worktree, clean filter ou textconv ne certifie son contenu. Ajouts,
suppressions et substitutions hors du snapshot autorisé sont refusés.

Sur ce dépôt, `* text=auto eol=lf` produit normalement un checkout LF même avec
core.autocrlf=true sous Windows. Une entrée déjà identique au blob est acceptée
sans transformation. Pour un checkout CRLF, seule une conversion CRLF→LF est
admise : texte UTF-8 strict sans NUL, sans CR isolé ni mélange LF/CRLF, attributs
text/eol compatibles vérifiés avec check-attr --source sur le commit, et aucun
filter, ident ou working-tree-encoding applicable. Aucun filtre n’est exécuté.
Le résultat doit correspondre exactement au blob attendu ; BOM, espaces et
contenu Unicode ne sont ni retirés ni corrigés. Seule la copie scratch est alors
normalisée, pour compiler les octets du commit. Le checkout reste inchangé.
normalizedTextInputs indique le nombre de ces conversions. Une modification
réelle reste INPUTS ; un inventaire incohérent est INVENTORY ; une conversion
non prise en charge est CONVERSION. Ces codes restent fermés.

Pour vérifier les nouveaux contrats avant tout commit :

```powershell
pwsh -NoProfile -File tests/Packaging/Test-ModuleSdkPackage.ps1 -WorkingTreeSdk -TemplateContracts -PackageVersion 0.1.0-localproof.3
pwsh -NoProfile -File tests/Packaging/Test-ModuleSdkIsolation.ps1 -WorkingTreeSdk -TemplateContracts
```

WorkingTreeSdk est un mode local explicite, distinct du mode strict HEAD par
défaut. Seuls les fichiers C# du SDK peuvent alors différer de HEAD ou être
non suivis ; le projet, le lock, les propriétés communes et les notices restent
vérifiés contre HEAD. Chaque entrée est copiée et son hash est vérifié avant
et après copie. Le package contient PROOF-SOURCE.json (liste relative des
entrées/hashes et baseCommit) dont le SHA-256 est contrôlé puis rapporté.
Les fichiers C# SDK ajoutés, modifiés ou retirés composent le snapshot physique ;
les autres entrées restent comparées aux blobs HEAD, même avec un marqueur
d’index. sha256 décrit les octets compilés et checkoutSha256 ceux du checkout,
y compris lorsqu’une entrée protégée CRLF a été normalisée dans le scratch.
Le champ commit NuGet reste vide et repositoryCommit n’est pas émis ; baseCommit
désigne seulement la base, jamais les octets non committés. Aucun contexte Git
fictif n’est fourni au build de ce snapshot. Ce mode ne prouve pas un mapping
SourceLink vers des sources committées. Il ne modifie pas la CI publique.

TemplateContracts ajoute au consommateur la fixture autonome
[TemplateProviderConsumer.cs.txt](TemplateProviderConsumer.cs.txt), qui référence
uniquement le package SDK. Le mode strict conserve son consommateur de métadonnées
historique ; après un commit humain des nouveaux contrats, TemplateContracts
pourra aussi être utilisé sans WorkingTreeSdk.

Tous les appels Git utilisent le même lanceur : aucune variable GIT_* héritée
n’est conservée, y compris les redirections de dépôt, index, objets, configuration
injectée et traces. Le lanceur applique seulement ses réglages non interactifs et
des configurations globales/système vides, sans modifier l’environnement appelant.
Seul le chemin demandé est admis comme safe.directory ; la racine retournée par
Git doit être exactement RepositoryRoot et l’origin local doit être celui du dépôt
public. Un sous-répertoire ou un origin étranger est refusé avant toute copie.

## Parcours HEAD vérifié

1. Créer un dossier temporaire possédé **hors du checkout** ; copier les entrées
   suivies du SDK et vérifier leurs octets, sans bin/obj ni historique Git.
   Écrire directement les deux fichiers de métadonnées Git minimales (HEAD et
   config avec l’URL publique vérifiée) pour conserver le contexte du SDK.
   Aucun helper CI ou PostgreSQL n’est chargé ni exécuté ; leurs modifications
   locales n’affectent donc pas le code exécuté par cette preuve.
2. Restaurer le vrai projet SDK en mode verrouillé, puis effectuer son pack
   **Release**. Un overlay MSBuild temporaire fournit seulement les métadonnées
   du package, sa préversion et les fichiers README/LICENSE/NOTICE.
   Le projet et les contrats originaux restent inchangés.
3. Inspecter le nupkg : ID/version, repository/commit publics, licence, aucune
   dépendance, assembly net10.0 et liste fermée de fichiers. Calculer son SHA-256.
4. Générer un consommateur séparé, sans ProjectReference, Reference/HintPath,
   import de propriétés SiteFactory ou source C# copiée du moteur. Sa seule
   dépendance est un PackageReference au SDK avec **intervalle exact [version]**.
   L’implémentation locale d’ISiteFactoryModule construit et vérifie un
   ModuleDescriptor et ses valeurs publiques.
5. Générer le lock initial, puis refaire un restore **--locked-mode** dans
   un deuxième cache vide. Vérifier assets, absence de références projet,
   source inscrite dans les métadonnées NuGet et hash du package installé.
   Vérifier aussi les imports et entrées Compile effectivement évalués :
   aucun import ni fichier source ne doit provenir du checkout SiteFactory.
6. Compiler le consommateur en Release sans restore implicite, l’exécuter et
   comparer le hash de l’assembly réellement chargée au fichier contenu dans le
   nupkg. Aucune intégration au moteur/Documentation n’est déduite de ce succès.

Le NuGet.config temporaire utilise clear, un feed local explicite et un mapping
exact du seul ID ModuleSdk. Les caches packages/HTTP/plugins sont propres à
l’exécution ; les sources utilisateur et dossiers fallback ne sont pas utilisés.
La source implicite library-packs du SDK est aussi désactivée pour ces processus.
Aucun accès public de secours n’est configuré, même pour une version absente.

Le mapping seul ne suffit pas avec un cache déjà rempli ; c’est pourquoi le
restore verrouillé et les contre-tests emploient des caches distincts neufs.
Voir [Package Source Mapping](https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping)
et la [commande dotnet pack](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack).

## Contre-tests et résultat

La preuve exige un exit non nul **et le code NuGet attendu** pour :

| Cas | Refus attendu |
|---|---|
| Préversion exacte absente du feed autorisé | NU1102 |
| Feed mappé vide, nupkg présent dans un second feed local non mappé | NU1101 |
| PackageReference divergent du lock existant | NU1004 |

Le deuxième cas prouve le refus du package SDK sur une source non mappée ;
il ne constitue pas la campagne future complète de substitution d’un package
Business privé par un homonyme public. Aucun feed Business n’est contacté.

Les régressions d’isolation se vérifient séparément :

```powershell
pwsh -NoProfile -File tests/Packaging/Test-ModuleSdkIsolation.ps1
```

Ce test crée des clones locaux temporaires à partir du HEAD public existant,
sans nouveau commit et sans modifier les métadonnées du checkout d’origine.
Il vérifie cinq scénarios : consommation complète avec variables Git parasites,
refus d’un sous-répertoire, refus d’un origin étranger malgré une configuration
injectée, absence d’exécution des deux helpers CI volontairement altérés, et
refus d’une entrée SDK modifiée même avec les variables parasites. Les deux
consommations complètes conservent tous les contrôles et refus NuGet ci-dessus.
Les marqueurs des helpers et traces Git doivent rester absents. Le JSON borné
vérifie aussi le cleanup et l’absence de sortie brute ou de chemins de test.
Le paramètre ProofScript permet de comparer explicitement une version locale
antérieure du script ; il exécute ce fichier et doit désigner du code de confiance.

Avec WorkingTreeSdk, les tests recopient les seuls fichiers C# du SDK dans leur
checkout temporaire et vérifient aussi le refus d’un projet modifié (six cas).
Le dernier cas revient aux sources HEAD dans cette fixture pour conserver le
contre-test historique de refus d’une source modifiée en mode strict.

Pour les contre-tests F2 de provenance :

```powershell
pwsh -NoProfile -File tests/Packaging/Test-ModuleSdkIsolation.ps1 -Provenance
```

Ce parcours comporte 17 cas, dont les deux marqueurs d’index en mode HEAD et
sur une entrée protégée WorkingTreeSdk, source SDK autorisée, checkout CRLF
matérialisé par Git, modification réelle sous CRLF, filtre/encodage non pris en
charge, source supprimée sous skip-worktree et source ajoutée ignorée par Git.
Toutes les manipulations d’index portent sur des clones temporaires possédés ;
l’empreinte de leur index est comparée avant/après la preuve avec marqueur.
Le checkout utilisateur et ses marqueurs ne sont jamais réécrits.

Le script émet un résumé JSON borné : PASSED/FAILED, versions observées, commit,
empreinte du package, contrôles effectués, refus observés et cleanup. Il retourne
0 seulement si tous les contrôles et le cleanup réussissent. En échec, phase,
code fermé, dernier exit de processus disponible et codes d’outil bornés sont conservés sans stdout/stderr
brut, exception, environnement complet ou chemin personnel.

Le finally supprime uniquement le dossier temporaire exact, avec son marqueur
de propriété ; il ne vide aucun cache global, ne modifie pas la configuration
NuGet/Git du checkout et ne supprime aucun dossier fourni par l’appelant.
Les réglages d’environnement sont limités aux processus enfants. Un cleanup
incomplet garde la preuve FAILED. Tous les packages et artefacts sont temporaires
et ne sont jamais ajoutés au suivi Git.
Les serveurs de build persistants et la compilation partagée sont désactivés
pour ne pas réutiliser ni arrêter ceux d’une autre tâche.

## Contrat de fournisseur SF-S1-002.1

ISiteFactoryModule et ses cinq types de métadonnées sont inchangés. Les neuf
types publics supplémentaires restent dans le même package, sans dépendance
Application/Domain/Infrastructure/CLI, package tiers ou framework supplémentaire :

| Type | Invariants et limites |
|---|---|
| ISiteFactoryTemplateProvider | Interface indépendante de ISiteFactoryModule ; catalogue et contenu asynchrones, annulation transmise |
| TemplateIdentity | ID non vide sans contrôle, comparaison ordinale ; version distincte, stable et canonique major.minor.patch, comme le format actuellement accepté par le moteur |
| TemplateDefinition | Identité, produits et fichiers obligatoires ; collections non vides, copiées, sans élément invalide ; aucun OwnerModuleId |
| TemplateFileDefinition | FileId stable distinct du chemin et de ContentKey ; valeurs non vides sans contrôle ; aucune résolution de destination |
| TemplateFilePolicy | Generated et CreateOnce uniquement ; valeur inconnue refusée ; aucune permission d’écriture accordée |
| TemplateRenderContext | DisplayName et DefaultLocale en lecture seule ; le constructeur à deux arguments doit rester disponible lors d’extensions additives futures |
| TemplateCatalogResult | Snapshot complet, éventuellement vide ; conserve ordre et doublons, y compris plusieurs versions ou doublons exacts |
| TemplateContentResult | Copie d’un byte[] à l’entrée et à chaque lecture ReadOnlyMemory ; octets vides valides, aucune normalisation texte implicite |
| TemplateReadFailure | InvalidDefinition, UnsupportedTemplate, UnknownContent, ContentUnavailable ; codes inconnus refusés, aucun message libre ni exception |

Les échecs ne portent aucun contenu partiel. Les erreurs de construction des
contrats restent des exceptions d’argument ; elles ne sont pas des diagnostics
bruts à publier. L’interface exige la propagation de l’annulation par son
implémentation ; le SDK n’exécute ni ne sandboxe un provider.

Le SDK ne valide pas les chemins portables, collisions, droits de propriété,
stratégies d’exécution ou état de régénération. Les déclarations dupliquées
restent visibles : le futur adaptateur moteur devra les refuser, jamais les
résoudre par priorité ou déduplication. Il associera explicitement le fournisseur
au module, vérifiera identité/version/source et dérivera la propriété. Aucun
chemin physique, secret, service moteur ou ClientConfigurationV1 n’est exposé.

La preuve Templates compile et exécute un provider fictif indépendant des
métadonnées de module : deux templates, versions distinctes, Generated/CreateOnce,
contenus binaires, copies défensives, échecs explicites et annulation. Les huit
contrôles historiques restent exécutés : copie/provenance des entrées, contenu
fermé du package, restore verrouillé isolé, imports/compilation isolés, assembly
chargée et les trois refus NuGet. En mode snapshot, le contrôle de copie/provenance
est nommé working-tree-sdk-snapshot-copied-and-verified ; il n’est jamais nommé
committed-sdk-inputs-copied-and-verified. Les diagnostics JSON restent fermés.

Seul le pin NuGet exact est prouvé. Les plages de compatibilité d’ADR-0036,
l’intégration réelle au moteur, l’adaptateur Application, les autres stratégies
du moteur et l’extraction de Documentation ne sont pas implémentés ici.

La correction F1 porte l’inventaire exécutable à 132 cas SDK et 2 766 cas offline,
vérifiés par découverte des onze suites. Cette découverte ne vaut pas exécution
des 2 766 cas. Les nombres des campagnes historiques restent inchangés ; aucun
cas n’est retiré pour conserver l’ancien total.

## Limites

- Reproductibilité du parcours avec les entrées et versions mesurées, pas promesse
  d’identité binaire du zip nupkg entre deux exécutions.
- Métadonnées de packaging propres à cette preuve, pas configuration de release
  officielle du projet SDK. Aucun push NuGet, tag, release, commit ou PR.
- Aucun autre ID SiteFactory n’est admis par cette preuve. Elle respecte
  [ADR-0035](../../docs/adr/0035-partage-public-component-via-nuget.md),
  [ADR-0008](../../docs/adr/0008-private-package-feed-and-supply-chain.md) et la
  [composition explicite](../../docs/adr/0037-compose-modules-explicitly-without-dynamic-loading.md).
- Aucun consommateur Business, contribution de génération, compatibilité
  multi-versions, signature de release ou package de symboles validé ici.
- Le script est une vérification locale explicitement invoquée ; il n’ajoute
  aucun job GitHub ni projet à la solution publique.
- Les tests du moteur se vérifient séparément avec les
  [commandes CONTRIBUTING](../../CONTRIBUTING.md#30-commandes-de-validation).
  Un passage Windows ne prouve pas Linux ou PostgreSQL REQUIRED.
