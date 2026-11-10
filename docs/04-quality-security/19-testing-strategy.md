# LogicGate SiteFactory — Stratégie de tests

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Stratégie de tests |
| Version | 1.0 |
| Statut | Proposition à valider |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, testeurs, opérateurs, contributeurs, Codex |
| Documents parents | [README public](../../README.md), [AGENTS](../../AGENTS.md), [frontière Core/Module](../02-architecture/core-module-boundary.md), [architecture des modules](../02-architecture/09-module-architecture.md), [entitlements](../02-architecture/10-feature-flags-and-entitlements.md), [pipeline de génération](../03-generation/13-generation-pipeline.md), [repository documentaire](../03-generation/repository-documentation-end-to-end.md), [configuration](../03-generation/14-client-configuration-schema.md), [CLI](../03-generation/15-cli-specification.md), [IA optionnelle](../adr/0015-deterministic-generation-and-optional-ai.md), [transactions](../adr/0028-database-transaction-boundaries-and-concurrency-control.md), [sessions administrateur](../adr/0020-administrator-session-security-and-step-up-authentication.md), [baseline de sécurité](18-security-baseline.md) |
| Dernière mise à jour | 2026-11-09 |

## Périmètre public et preuves acquises

[SiteFactory.slnx](../../SiteFactory.slnx) contient **12 projets de production
et 11 projets de tests publics**. Ses tests utilisent des sources et fixtures
publiques, sans module, manifest, DLL ou exécutable Business. DocumentationModule
est le module concret public ; les cas génériques emploient aussi des modules
fictifs explicites par les vrais contrats ModuleSdk.

La CI publique a exécuté les **onze suites offline : 2 726 cas Passed sous
Windows et les mêmes 2 726 sous Linux**, avec onze TRX contrôlés par plateforme.
Il s'agit d'exécutions multiplateformes d'un inventaire, pas de 5 452 scénarios
fonctionnels distincts. Les preuves ont été acquises sur le scope public isolé
dans le dépôt de préparation privé, sans attribuer de résultat aux suites Business.

Le [harnais PostgreSQL public](../../tests/PostgreSql/README.md) a exécuté deux
inventaires fermés distincts : **20 Facts Infrastructure + 3 Facts Hosts**, dans
cinq shards séquentiels **g04=5, g05=3, p01=8, p02=4, p03=3**. PostgreSQL Linux
**18.6 / 180006**, digest approuvé, TLS **VerifyFull**, credentials temporaires et
ressources possédées ; **23 Passed / cinq TRX**, zéro résultat manquant, ignoré,
échoué ou annulé. Le stress readiness conserve **32 × 24 = 768 HTTP 200, zéro
503**, les deadlines, les corrélations Activity, les observations filtrées et
le cleanup. Une campagne verte ne démontre pas la cause d'un refus historique.

Les **27 TRX** de la campagne complète, leurs identités et multiplicités,
SHA/run/tentative, archivage expurgé, cleanup et agrégation sont vérifiés ; un exit
code seul ne suffit pas. Les preuves rouges restent rouges, sans retry jusqu'au
vert. Aucun scénario Business historique n'est présenté comme rejoué ici.

Les sections suivantes définissent aussi les tests exigibles de composants
futurs (Identity, administration, email, jobs, données métier, Docker et
exploitation). Elles n'annoncent pas leur livraison publique. Le site documentaire
embarqué est statique : sa génération n'ajoute pas une application .NET cliente.
Après création du vrai dépôt public, la CI et **SourceLink public PENDING**
seront vérifiés avec ses véritables URL et commits. Les détails privés de campagne
restent dans le suivi privé. Voir les [gates CI](20-quality-gates-and-ci.md).

---

## 1. Objet du document

Ce document définit la stratégie de tests de LogicGate SiteFactory.

Il précise notamment :

- les objectifs de qualité ;
- les niveaux de tests ;
- la répartition entre tests unitaires, d’intégration et système ;
- les tests du moteur SiteFactory ;
- les tests des catalogues et schémas ;
- les tests des modules ;
- les tests du CLI ;
- les tests du pipeline de génération ;
- les tests des projets clients générés ;
- les tests PostgreSQL ;
- les tests de migrations ;
- les tests Identity et autorisation ;
- les tests de sécurité ;
- les tests SEO ;
- les tests de Docker et de déploiement ;
- les tests de sauvegarde et restauration ;
- les tests de performance ;
- les tests d’accessibilité ;
- les tests des fonctionnalités LLM ;
- les données de test ;
- l’isolation et le déterminisme ;
- les rapports ;
- les seuils de couverture ;
- les quality gates CI ;
- la gestion des tests instables ;
- les critères de livraison ;
- les règles applicables à Codex.

L’objectif n’est pas de maximiser artificiellement le nombre de tests.

L’objectif est de fournir des preuves proportionnées que SiteFactory et les projets générés respectent leurs contrats.

---

## 2. Vision

SiteFactory produit du code destiné à servir de base à plusieurs projets clients.

Une erreur dans le moteur, un template ou un module peut donc se propager à plusieurs applications.

La stratégie de tests doit couvrir deux dimensions distinctes :

```text
Tester SiteFactory lui-même

ET

Tester les applications produites par SiteFactory
```

Flux de qualité de référence :

```text
Règle ou fonctionnalité
→ test ciblé
→ build
→ tests du composant
→ génération d’un projet de référence
→ build du projet généré
→ tests du projet généré
→ contrôles de sécurité et qualité
→ validation humaine
```

---

## 3. Principes fondamentaux

## 3.1 Chaque règle importante possède une preuve

Une règle importante doit être vérifiée par au moins un mécanisme adapté :

- type et compilation ;
- validation ;
- contrainte de base ;
- test automatisé ;
- analyse statique ;
- revue humaine ;
- smoke test ;
- runbook testé.

Exemple :

```text
« Un fichier custom ne doit pas être écrasé »

→ test automatisé de régénération
→ comparaison du hash
→ résultat bloquant en cas de modification
```

---

## 3.2 Tester les comportements, pas les détails internes

Les tests doivent prioritairement vérifier :

- entrées ;
- sorties ;
- contrats ;
- effets de bord ;
- erreurs ;
- sécurité ;
- données persistées.

Ils ne doivent pas être excessivement couplés à :

- une méthode privée ;
- l’ordre interne de plusieurs appels ;
- une classe technique remplaçable ;
- une implémentation sans impact fonctionnel.

---

## 3.3 Les tests suivent la pyramide

La majorité des tests doivent être :

- rapides ;
- isolés ;
- déterministes ;
- ciblés.

Les tests plus coûteux restent moins nombreux, mais couvrent les intégrations réelles essentielles.

Répartition conceptuelle :

```text
Nombre élevé
Tests unitaires et de contrats
        │
        ▼
Tests de composants et modules
        │
        ▼
Tests d’intégration
        │
        ▼
Tests système et end-to-end
Nombre plus limité
```

La pyramide n’interdit pas les tests d’intégration.

SiteFactory nécessite au contraire des tests d’intégration significatifs pour :

- système de fichiers ;
- PostgreSQL ;
- CLI ;
- génération ;
- build ;
- Docker ;
- sécurité web.

---

## 3.4 Les tests critiques utilisent les technologies réelles

Les comportements spécifiques à PostgreSQL ne doivent pas être validés uniquement avec une base en mémoire.

Les comportements du CLI ne doivent pas être validés uniquement en appelant directement un handler.

Les comportements HTTP ne doivent pas être validés uniquement en appelant une méthode C#.

Les comportements Docker ne doivent pas être validés uniquement en inspectant une chaîne YAML.

---

## 3.5 La CI standard ne dépend pas de services externes instables

La CI standard ne doit pas nécessiter :

- un provider LLM réel ;
- un serveur SMTP réel ;
- un VPS réel ;
- une base de production ;
- un domaine public ;
- des credentials personnels ;
- un service tiers payant.

Ces dépendances sont remplacées par :

- fake contrôlé ;
- serveur local de test ;
- conteneur ;
- adaptateur déterministe ;
- environnement dédié.

---

## 3.6 Un bug corrigé doit être reproduit par un test

Lorsqu’un défaut est corrigé :

1. reproduire le défaut ;
2. ajouter un test échouant ;
3. corriger ;
4. vérifier que le test réussit ;
5. conserver le test contre la régression.

Une correction sans preuve automatisée doit être justifiée.

---

## 3.7 Aucun test ne dépend de l’ordre d’exécution

Les tests doivent pouvoir être exécutés :

- seuls ;
- dans un ordre différent ;
- en parallèle lorsque supporté ;
- plusieurs fois ;
- sur une machine propre.

---

## 3.8 Un test ignoré n’est pas un test réussi

Un test ignoré ou désactivé doit être visible.

Il doit posséder :

- une raison ;
- un propriétaire ;
- une issue ou tâche ;
- une date ou condition de réactivation.

---

# 4. Non-objectifs

La stratégie ne cherche pas à :

- tester chaque ligne de code sans distinction ;
- atteindre 100 % de couverture à tout prix ;
- remplacer la revue humaine ;
- remplacer la recette client ;
- prouver l’absence totale de vulnérabilité ;
- lancer tous les tests lourds sur chaque frappe locale ;
- tester les frameworks .NET eux-mêmes ;
- créer des tests end-to-end pour chaque variante visuelle ;
- conserver des snapshots volumineux impossibles à revoir ;
- faire dépendre la qualité uniquement d’un score global.

---

# 5. Terminologie

## 5.1 Test unitaire

Vérifie une règle ou un composant isolé avec peu ou pas d’effets de bord réels.

Exemples :

- validation d’un slug ;
- résolution d’une version ;
- calcul d’un plan ;
- transition d’un statut ;
- règle d’entitlement.

---

## 5.2 Test de contrat

Vérifie qu’un format ou une interface respecte un contrat stable.

Exemples :

- JSON Schema ;
- manifeste de module ;
- sortie JSON du CLI ;
- format du lock file ;
- schéma du rapport.

---

## 5.3 Test de composant

Vérifie un ensemble cohérent de classes ou un module avec ses dépendances internes.

---

## 5.4 Test d’intégration

Vérifie l’interaction réelle entre plusieurs composants ou technologies.

Exemples :

- EF Core et PostgreSQL ;
- ASP.NET Core Identity ;
- système de fichiers ;
- exécution de `dotnet build` ;
- application HTTP.

---

## 5.5 Test système

Vérifie le comportement d’une application complète ou presque complète.

Exemple :

```text
configuration
→ génération
→ restore
→ build
→ tests du projet généré
```

---

## 5.6 Test end-to-end

Vérifie un parcours utilisateur complet à travers les couches réelles.

Exemple :

```text
Soumission d’un formulaire public
→ validation
→ stockage PostgreSQL
→ affichage dans l’administration
```

---

## 5.7 Smoke test

Vérifie rapidement les fonctions essentielles après un build ou un déploiement.

---

## 5.8 Golden master

Compare un résultat généré à une référence approuvée.

Il est adapté aux artefacts déterministes.

---

## 5.9 Snapshot test

Compare une représentation sérialisée ou visuelle avec une référence.

Les changements de snapshot doivent être revus explicitement.

---

## 5.10 Test de régression

Empêche le retour d’un défaut déjà rencontré.

---

## 5.11 Quality gate

Condition obligatoire pour permettre :

- merge ;
- publication ;
- release ;
- génération validée ;
- déploiement.

---

# 6. Organisation des projets de tests

Structure cible :

```text
tests/
├── LogicGate.SiteFactory.Domain.Tests/
├── LogicGate.SiteFactory.Application.Tests/
├── LogicGate.SiteFactory.Catalog.Tests/
├── LogicGate.SiteFactory.Generation.Tests/
├── LogicGate.SiteFactory.Infrastructure.Tests/
├── LogicGate.SiteFactory.Cli.Tests/
├── LogicGate.SiteFactory.Architecture.Tests/
├── LogicGate.SiteFactory.Module.Tests/
├── LogicGate.SiteFactory.GeneratedProject.Tests/
├── LogicGate.SiteFactory.Security.Tests/
└── LogicGate.SiteFactory.EndToEnd.Tests/
```

Les modules complexes peuvent disposer de projets dédiés :

```text
tests/
├── LogicGate.SiteFactory.Module.Leads.Tests/
├── LogicGate.SiteFactory.Module.Content.Tests/
└── LogicGate.SiteFactory.Module.Identity.Tests/
```

Cette structure est une cible.

Il ne faut pas créer tous les projets avant que les premiers tests justifient leur existence.

---

# 7. Frameworks et outils

## 7.1 Tests .NET

Le framework de tests de référence doit être choisi et utilisé de manière cohérente.

Baseline proposée :

```text
xUnit
```

Le choix doit être documenté dans une ADR si nécessaire.

---

## 7.2 Assertions

Les assertions doivent rester :

- lisibles ;
- explicites ;
- sans abstraction excessive ;
- compatibles avec la politique de licences LogicGate.

Les assertions natives peuvent suffire dans le MVP.

Une bibliothèque complémentaire doit être validée avant adoption.

---

## 7.3 Tests ASP.NET Core

Les tests web peuvent utiliser :

```text
Microsoft.AspNetCore.Mvc.Testing
WebApplicationFactory
TestServer
HttpClient
```

---

## 7.4 Tests PostgreSQL

Les tests nécessitant PostgreSQL utilisent une instance réelle isolée, idéalement dans un conteneur de test.

Une bibliothèque de gestion de conteneurs peut être utilisée après validation.

---

## 7.5 Tests de composants Blazor

Les composants peuvent être testés avec un outil spécialisé validé, ou par tests du rendu et tests HTTP lorsque cela suffit.

Le choix doit rester proportionné au nombre et à la complexité des composants.

---

## 7.6 Navigateurs

Les parcours end-to-end nécessitant un navigateur peuvent utiliser un outil tel que Playwright après validation.

Ils doivent rester peu nombreux et concentrés sur les parcours critiques.

---

## 7.7 Analyse de couverture

La couverture est produite dans un format exploitable par la CI.

Elle ne doit pas être le seul indicateur de qualité.

---

# 8. Tests du domaine

Les tests Domain doivent couvrir notamment :

- value objects ;
- identifiants ;
- versions ;
- règles d’état ;
- validations pures ;
- erreurs métier ;
- dépendances ;
- compatibilités ;
- transitions.

Exemples :

```text
Un slug valide est accepté.

Un slug contenant ../ est refusé.

Une version flottante est refusée.

Un module future ne peut pas être installé.

Un module inclus ne peut pas être désactivé.

Une dépendance cyclique est refusée.
```

---

## 8.1 Caractéristiques

Les tests Domain doivent être :

- très rapides ;
- sans base ;
- sans fichier ;
- sans réseau ;
- sans horloge réelle ;
- sans dépendance au CLI.

---

## 8.2 Couverture attendue

Toutes les règles métier critiques doivent posséder des tests positifs et négatifs.

---

# 9. Tests Application

Les tests Application couvrent :

- cas d’usage ;
- commandes ;
- requêtes ;
- orchestration ;
- validation ;
- gestion des erreurs ;
- interactions avec les ports.

Exemples :

```text
ValidateConfiguration
PlanGeneration
GenerateProject
CheckUpgrade
PlanUpgrade
```

---

## 9.1 Ports simulés

Les ports peuvent être remplacés par :

- fake en mémoire ;
- stub ;
- spy ciblé ;
- implémentation déterministe.

Il faut éviter de simuler toute l’architecture avec des mocks fragiles.

---

## 9.2 Cas obligatoires

Pour chaque cas d’usage important :

```text
cas nominal
entrée invalide
dépendance absente
annulation
erreur technique
résultat partiel interdit
```

---

# 10. Tests du catalogue

Les catalogues constituent une source de vérité critique.

Les tests doivent vérifier :

- syntaxe ;
- schéma ;
- identifiants uniques ;
- packs ;
- prix ;
- modules ;
- services ;
- statuts ;
- dépendances ;
- incompatibilités ;
- héritage ;
- cycles ;
- versions ;
- références de pages ;
- références de thèmes.

---

## 10.1 Valeurs de fixtures verrouillées

Les valeurs validées d'une fixture publique doivent être vérifiées sans copier
un catalogue commercial privé. Les valeurs synthétiques et leurs résultats
attendus restent explicites et indépendants des offres commerciales.
Une modification de ces valeurs doit provoquer une revue explicite du test ;
il ne faut ni recalculer automatiquement son oracle ni accepter une dérive.
Les tests des véritables catalogues commerciaux restent dans leur périmètre privé.

---

## 10.2 Tests d’héritage

Vérifier notamment :

```text
composition de fixture
→ capacités de la contribution requise
→ capacités propres
```

Le résultat final doit être stable et sans doublon.

---

# 11. Tests des schémas

Les schémas suivants doivent être testés :

```text
client-configuration.schema.json
catalog.schema.json
template.schema.json
theme.schema.json
module-manifest.schema.json
generation-plan.schema.json
generation-report.schema.json
sitefactory-lock.schema.json
```

---

## 11.1 Cas valides

Chaque schéma doit posséder :

- exemple minimal valide ;
- exemple complet valide ;
- exemples par version supportée.

---

## 11.2 Cas invalides

Vérifier :

- propriété obligatoire absente ;
- type invalide ;
- propriété inconnue ;
- valeur hors plage ;
- enum inconnu ;
- format invalide ;
- version inconnue ;
- chemin dangereux ;
- secret potentiel.

---

## 11.3 Cohérence documentation-code

Les exemples publiés dans la documentation devraient être validés automatiquement lorsque cela est raisonnable.

---

# 12. Tests des templates

Chaque template doit être testé sur :

- manifeste ;
- version ;
- pages obligatoires ;
- sections ;
- variantes ;
- navigation ;
- modules requis ;
- thème compatible ;
- contenus initiaux ;
- placeholders ;
- SEO ;
- accessibilité structurelle.

Le template public actuel :

```text
documentation-minimal
```

possède des tests de résolution, composition et génération. Les templates
d'application supplémentaires ne sont pas inclus dans cette distribution.

---

## 12.1 Variantes minimales

Tester au minimum :

```text
configuration minimale
composition documentaire standard
composition de fixture avec contribution supplémentaire
contenu incomplet
module optionnel absent
module requis absent
thème incompatible
```

---

# 13. Tests du design system

Les tests doivent couvrir :

- tokens obligatoires ;
- composants ;
- variantes ;
- états ;
- classes CSS ;
- comportement responsive ;
- focus ;
- navigation clavier ;
- messages d’erreur ;
- composants de formulaire.

---

## 13.1 Tests de rendu

Les composants importants peuvent être testés sur :

- markup ;
- attributs ARIA ;
- classes ;
- contenu ;
- événements ;
- état disabled ;
- état loading ;
- état error.

---

## 13.2 Tests visuels

Des snapshots visuels peuvent être envisagés pour :

- boutons ;
- formulaires ;
- navigation ;
- cartes ;
- administration ;
- sections principales.

Ils restent hors du premier incrément si leur maintenance ralentit la mise en place du socle.

---

# 14. Tests des modules

Chaque module doit posséder une matrice adaptée.

Baseline :

```text
Manifest
Configuration
Enregistrement
Permissions
Routes
Endpoints
Données
Migrations
Événements
Logs
Feature flags
Désactivation
Upgrade
```

---

## 14.1 Exemple Leads

Tester notamment :

```text
Création d’un lead valide

Rejet d’un lead invalide

Historique créé

Changement de statut autorisé

Changement de statut refusé sans permission

Ajout de note

Export autorisé

Export refusé sans entitlement

Export refusé sans permission

CSV neutralisé

Données personnelles absentes des logs
```

---

## 14.2 Composition avec dépendances

Un module doit être testé :

- avec toutes ses dépendances ;
- avec une dépendance manquante ;
- avec une version incompatible ;
- dans les packs concernés ;
- avec une feature désactivée.

---

# 15. Tests des manifests de modules

Vérifier :

- identifiant ;
- package ;
- version ;
- statut ;
- catégorie ;
- dépendances ;
- permissions ;
- routes ;
- entités ;
- événements ;
- stratégie d’upgrade.

Un manifeste invalide doit empêcher la publication du module.

---

# 16. Tests d’architecture

Les tests d’architecture vérifient les règles structurelles.

Exemples :

```text
Domain ne référence pas Infrastructure.

Application ne référence pas CLI.

Les modules ne référencent pas un projet client.

Les projets génériques ne contiennent pas de namespace client.

Les migrations restent dans le repository client.

Les pages admin possèdent une protection.

Les actions mutables ne sont pas exposées par GET.

Les versions de packages ne sont pas flottantes.
```

---

## 16.1 Dépendances entre modules

Les tests doivent comparer :

- références de projets ou packages ;
- dépendances déclarées dans les manifests ;
- dépendances du catalogue.

Une référence technique non déclarée doit être signalée.

---

## 16.2 Code client dans le générique

Les tests peuvent rechercher :

- slugs clients connus ;
- namespaces clients ;
- conditions par nom ;
- configuration client codée en dur.

---

# 17. Tests du CLI

Les tests CLI couvrent trois niveaux.

## 17.1 Parsing

Vérifier :

- commande connue ;
- commande inconnue ;
- option inconnue ;
- option obligatoire ;
- enum ;
- combinaison incompatible ;
- help ;
- version ;
- dry-run et execute simultanés ;
- confirmation manquante.

---

## 17.2 Handler

Vérifier :

- mapping des options ;
- appel du cas d’usage ;
- mapping du résultat ;
- code de sortie ;
- writer utilisé.

---

## 17.3 Processus réel

Exécuter l’application CLI dans un processus isolé pour vérifier :

- stdout ;
- stderr ;
- exit code ;
- fichiers créés ;
- mode JSON ;
- mode non interactif ;
- Ctrl+C lorsque testable.

---

## 17.4 Sortie JSON

La sortie doit être validée contre son schéma.

Aucun texte parasite ne doit précéder ou suivre le document JSON.

---

# 18. Tests du pipeline de génération

Le pipeline constitue une zone critique.

Il doit être testé étape par étape et dans son ensemble.

---

## 18.1 Validation

Vérifier :

- configuration valide ;
- YAML invalide ;
- schéma inconnu ;
- catalogue invalide ;
- module inconnu ;
- secret ;
- profil production invalide.

---

## 18.2 Planification

Vérifier :

- plan déterministe ;
- liste de fichiers ;
- commandes ;
- warnings ;
- conflits ;
- actions manuelles ;
- absence d’écriture.

---

## 18.3 Exécution

Vérifier :

- dossier temporaire ;
- fichiers générés ;
- stratégies ;
- restore ;
- build ;
- tests ;
- rapport ;
- Git.

---

## 18.4 Échec intermédiaire

Simuler un échec lors de :

```text
écriture
restore
build
tests
secret scan
Git
```

Le statut final doit rester en échec.

Le dossier final ne doit pas être présenté comme valide.

---

# 19. Tests des stratégies de fichiers

Chaque stratégie possède des tests dédiés.

## 19.1 `generated`

- création ;
- remplacement autorisé ;
- modification locale détectée ;
- aucune option de force ne contourne une stratégie de fichiers ;
- hash mis à jour.

## 19.2 `create-once`

- créé s’il est absent ;
- préservé s’il existe ;
- contenu non modifié.

## 19.3 `merge`

- ajout déterministe ;
- absence de duplication ;
- conflit détecté ;
- ordre stable ;
- syntaxe finale valide.

## 19.4 `custom`

- jamais écrasé ;
- jamais supprimé ;
- conservé lors d’une régénération.

## 19.5 `protected`

- écriture refusée ;
- aucune option de force ne contourne la protection ;
- diagnostic explicite.

---

# 20. Tests de chemins

Tester notamment :

```text
../
chemin absolu
dossier autorisé
dossier interdit
lien symbolique
nom réservé
caractères spéciaux
chemin Windows
chemin Linux
chemin très long
```

Une écriture ne doit jamais sortir du dossier cible.

---

# 21. Tests de reproductibilité

## 21.1 Génération répétée

Exécuter deux générations avec :

- mêmes entrées ;
- mêmes versions ;
- même plateforme ou environnement contrôlé.

Comparer :

- arborescence ;
- contenus déterministes ;
- lock file ;
- composition ;
- packages ;
- plan.

---

## 21.2 Différences autorisées

Les champs non déterministes peuvent inclure :

```text
GenerationId
GeneratedAtUtc
durées
chemins temporaires
```

Ils doivent être exclus ou normalisés dans la comparaison.

---

## 21.3 Configuration modifiée

Une modification fonctionnelle doit produire uniquement les différences attendues.

---

# 22. Golden master tests

Les golden masters sont adaptés à :

- solution générée ;
- Dockerfile ;
- Compose ;
- workflow CI ;
- fichier de composition ;
- lock file ;
- rapport ;
- robots.txt ;
- sitemap ;
- JSON-LD.

---

## 22.1 Mise à jour

Une mise à jour d’un golden master doit :

- être visible dans la revue ;
- être expliquée ;
- correspondre à une modification attendue ;
- ne pas être acceptée automatiquement en masse.

---

## 22.2 Données dynamiques

Les valeurs dynamiques doivent être normalisées avant comparaison.

---

# 23. Projet généré de référence

La CI doit générer au minimum un projet de référence.

La référence publique est le template embarqué **documentation-minimal**,
composé explicitement par DocumentationModule. La
[fixture de configuration publique](../../tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml)
est une entrée valide ; ses valeurs sont fictives. Le CLI produit le site HTML,
le lock, le rapport et l'état de génération. Aucun manifest privé n'est requis.

---

## 23.1 Vérifications

Pour une contribution produisant réellement un projet .NET, vérifier ensuite :

```bash
dotnet restore --locked-mode
dotnet build --configuration Release
dotnet test --configuration Release
```

Ces commandes ne s'appliquent pas au site documentaire statique actuel.
Pour une future application .NET, les vérifications pertinentes comprennent :

- configuration valide ;
- lock file valide ;
- migrations présentes ou préparées ;
- Docker valide ;
- Identity configuré ;
- admin protégé ;
- SEO valide ;
- aucun secret ;
- aucun placeholder bloquant.

---

## 23.2 Projet enrichi

Un second scénario public couvre les contributions explicites avec des fixtures
génériques, sans importer un catalogue commercial. Les scénarios d'applications
et d'offres privées restent vérifiés dans leur propre suite, sans revendication
de résultat dans l'inventaire public.

---

# 24. Tests PostgreSQL

## 24.1 Base réelle

Les tests PostgreSQL utilisent une vraie instance PostgreSQL isolée.

Ils doivent vérifier :

- types ;
- contraintes ;
- index ;
- clés étrangères ;
- transactions ;
- conversions ;
- requêtes ;
- concurrence ;
- migrations.

---

## 24.2 Base par suite

Chaque suite ou collection de tests doit posséder :

- une base dédiée ;
- ou un schéma isolé ;
- ou une stratégie fiable de nettoyage.

---

## 24.3 Données de production

Aucune donnée de production réelle n’est utilisée.

---

## 24.4 Nettoyage

Le nettoyage doit s’exécuter même après un test échoué lorsque cela est possible.

---

# 25. Tests EF Core

Tester notamment :

- configuration du modèle ;
- longueurs ;
- nullabilité ;
- index ;
- valeurs par défaut ;
- conversions ;
- relations ;
- cascades ;
- concurrence ;
- projections ;
- requêtes principales.

---

## 25.1 Modèle en attente de migration

Un test doit détecter lorsque le modèle EF Core ne correspond plus aux migrations du projet de référence.

---

## 25.2 `AsNoTracking`

Il n’est pas nécessaire de tester la présence de `AsNoTracking` partout.

Les tests de performance ou revues peuvent vérifier les requêtes importantes.

---

# 26. Tests de migrations

## 26.1 Création initiale

Vérifier qu’une base vide peut être créée depuis les migrations.

---

## 26.2 Upgrade

Vérifier la migration depuis chaque version officiellement supportée.

---

## 26.3 Données existantes

Les migrations doivent être testées avec des données représentatives.

Exemples :

- valeurs nulles existantes ;
- statut historique ;
- contenu long ;
- plusieurs relations ;
- données proches des limites.

---

## 26.4 Migration destructive

Un test doit vérifier :

- backup requis signalé ;
- données préservées ou explicitement transformées ;
- erreur contrôlée ;
- scénario de restauration.

---

## 26.5 `Down`

La méthode `Down` peut être testée techniquement.

Elle ne doit pas être présentée comme une garantie de rollback des données.

---

## 26.6 Migration de production

Aucun test ne doit appliquer une migration à une base de production.

---

# 27. Tests Identity

Les tests doivent couvrir :

- création du premier administrateur ;
- idempotence du seed ;
- seed désactivé ;
- mot de passe invalide ;
- connexion ;
- connexion échouée ;
- verrouillage ;
- compte désactivé ;
- logout POST ;
- antiforgery ;
- réinitialisation contrôlée ;
- dernier administrateur ;
- invalidation de session.

---

## 27.1 Inscription publique

Tester l’absence de :

```text
/register
/signup
```

et de tout endpoint équivalent non prévu.

---

## 27.2 Message générique

Les tests doivent vérifier qu’un compte inconnu et un mot de passe invalide produisent un message public équivalent.

---

# 28. Tests d’autorisation

Matrice minimale :

| État | Résultat attendu |
|---|---|
| Anonyme | `401` ou redirection login |
| Authentifié sans permission | `403` |
| Permission accordée | Succès |
| Module absent | `404` ou indisponibilité |
| Entitlement absent | Refus |
| Feature désactivée | Refus |
| Kill switch actif | Refus |
| Compte désactivé | Refus |

---

## 28.1 Protection multicouche

Pour une action critique, tester :

- navigation ;
- page ;
- endpoint ;
- handler ou service.

---

## 28.2 Bouton masqué

Un test HTTP direct doit vérifier que l’action reste refusée même lorsque l’interface n’affiche pas le bouton.

---

# 29. Tests des formulaires publics

Tester notamment :

- demande valide ;
- champ obligatoire absent ;
- email invalide ;
- téléphone invalide ;
- longueur dépassée ;
- honeypot rempli ;
- antiforgery invalide ;
- rate limit ;
- double soumission ;
- base indisponible ;
- SMTP indisponible ;
- message public ;
- absence de PII dans les logs.

---

## 29.1 Priorité à la persistance

Vérifier :

```text
stockage réussi
+ email échoué
→ demande conservée
```

---

# 30. Tests HTTP

Les tests HTTP doivent vérifier :

- codes de réponse ;
- headers ;
- redirections ;
- cookies ;
- cache ;
- contenu ;
- authentification ;
- antiforgery ;
- MIME types.

---

## 30.1 Erreurs

Tester :

```text
404 réel
500 sobre
aucune stack trace
correlation ID
```

---

## 30.2 Production

Les tests utilisent une configuration de production simulée afin de vérifier les différences de middleware.

---

# 31. Tests de sécurité

Les exigences détaillées proviennent de :

```text
18-security-baseline.md
```

Catégories minimales :

```text
authentification
autorisation
CSRF
XSS
SQL injection
command injection
path traversal
uploads
redirections
CORS
SSRF
secrets
headers
Docker
logs
```

---

## 31.1 Tests de contournement

Le test doit reproduire la manière dont un attaquant pourrait contourner l’UI :

- requête HTTP directe ;
- payload modifié ;
- ancien endpoint ;
- URL devinée ;
- claim absent ;
- token antiforgery supprimé ;
- extension falsifiée.

---

## 31.2 Secret scan

La suite doit vérifier que les fichiers d’exemple ne déclenchent pas de vrais secrets.

Les valeurs fictives doivent être clairement non exploitables.

---

# 32. Tests des headers de sécurité

En profil production, vérifier notamment :

```text
Strict-Transport-Security
Content-Security-Policy
X-Content-Type-Options
Referrer-Policy
Permissions-Policy
frame-ancestors ou protection équivalente
```

---

## 32.1 Développement

Le comportement peut différer pour :

- HSTS ;
- détails d’erreur ;
- CSP de développement.

Ces différences doivent être testées explicitement.

---

# 33. Tests CSP

Vérifier :

- politique présente ;
- absence de `unsafe-eval` ;
- sources attendues ;
- compatibilité Blazor ;
- blocage des domaines inconnus ;
- protection iframe ;
- scripts inline interdits ou protégés.

Un test manuel navigateur peut compléter les tests HTTP.

---

# 34. Tests CORS

Vérifier :

- absence de politique par défaut ;
- origine autorisée ;
- origine non autorisée ;
- méthode ;
- headers ;
- credentials ;
- impossibilité d’utiliser wildcard avec credentials.

---

# 35. Tests des uploads

Scénarios :

```text
fichier valide
extension invalide
MIME trompeur
signature invalide
fichier trop volumineux
nom hostile
path traversal
double extension
fichier vide
image corrompue
fichier exécutable
accès non autorisé
```

---

## 35.1 Stockage

Vérifier :

- nom interne généré ;
- emplacement contrôlé ;
- absence du web root pour les fichiers privés ;
- permissions ;
- suppression temporaire ;
- backup lorsque nécessaire.

---

# 36. Tests des exports

Vérifier :

- entitlement ;
- feature ;
- permission ;
- limite de volume ;
- encodage ;
- culture ;
- CSV injection ;
- colonnes autorisées ;
- suppression du fichier temporaire ;
- absence de PII dans les logs.

---

# 37. Tests SEO

Cette section définit de manière autonome les exigences de test SEO pour les
capacités qui les implémentent ; elle ne suppose pas un moteur SEO déjà livré.
La sécurité des données structurées et des scripts SEO relève aussi des
[sections 30 et 102 d'ADR-0022](../adr/0022-browser-security-headers-and-content-security-policy.md#30-json-ld).

Ils couvrent notamment :

- title ;
- description ;
- H1 ;
- canonical ;
- robots ;
- sitemap ;
- Open Graph ;
- données structurées ;
- slugs ;
- redirections ;
- liens internes ;
- staging ;
- production.

---

## 37.1 Projet généré

Le projet de référence doit être inspecté via HTTP ou rendu final, pas uniquement par lecture de configuration.

---

# 38. Tests d’accessibilité

La baseline doit couvrir :

- structure des titres ;
- labels ;
- noms accessibles ;
- focus ;
- navigation clavier ;
- contrastes ;
- messages d’erreur ;
- attributs ARIA ;
- liens ;
- boutons ;
- modales ;
- formulaires.

---

## 38.1 Tests automatisés

Les contrôles automatisés peuvent détecter une partie des défauts.

Ils ne remplacent pas une vérification manuelle.

---

## 38.2 Tests clavier

Les parcours critiques doivent être vérifiés sans souris :

```text
navigation publique
formulaire de contact
login
administration principale
modale éventuelle
```

---

## 38.3 Lecteur d’écran

Une vérification manuelle ciblée doit être prévue pour les composants importants.

---

# 39. Tests responsive

Tester au minimum des largeurs représentatives :

```text
mobile
tablette
desktop
large desktop
```

Les tests doivent couvrir :

- navigation ;
- Hero ;
- formulaires ;
- grilles ;
- tableaux admin ;
- modales ;
- textes longs ;
- médias.

---

# 40. Tests de performance

## 40.1 Objectif

Détecter les régressions significatives.

Le MVP ne nécessite pas une plateforme complète de load testing.

---

## 40.2 Mesures possibles

```text
temps de génération
temps de démarrage
temps de réponse
nombre de requêtes SQL
taille des pages
taille des images
taille de l’image Docker
durée du build
durée des tests
```

---

## 40.3 Scénarios de charge ciblés

Pour un formulaire public :

- soumissions concurrentes ;
- rate limiting ;
- persistance ;
- absence de doublons ;
- disponibilité.

Pour l’administration :

- liste paginée de leads ;
- export limité ;
- recherche ;
- filtres.

---

## 40.4 Seuils

Les seuils doivent être définis par fonctionnalité lorsque les mesures deviennent stables.

Une variation importante doit produire un warning ou une investigation.

---

# 41. Tests Docker

Tester :

- build de l’image ;
- démarrage ;
- profil Production ;
- utilisateur non root lorsque prévu ;
- port ;
- health check ;
- volumes ;
- absence de secret ;
- App_Data persistant ;
- Data Protection persistée.

---

## 41.1 Analyse statique du Compose

Vérifier :

- PostgreSQL sans port public ;
- variables attendues ;
- volumes ;
- réseau privé ;
- services attendus ;
- noms préfixés.

---

## 41.2 Test d’exécution Compose

Une suite dédiée peut démarrer :

```text
web
postgres
```

Caddy peut être inclus dans une suite de smoke test séparée.

---

# 42. Tests Caddy et HTTPS

Dans un environnement de test adapté, vérifier :

- reverse proxy ;
- forwarded headers ;
- redirection HTTP vers HTTPS ;
- headers ;
- taille maximale des requêtes ;
- routes ;
- WebSocket ou connexion persistante lorsque nécessaire.

Un certificat public réel n’est pas requis dans la CI standard.

---

# 43. Tests des sauvegardes

Vérifier que les scripts :

- produisent un backup PostgreSQL ;
- produisent un backup App_Data ;
- produisent un code de sortie non nul en cas d’échec ;
- n’affichent pas de secret ;
- respectent le dossier cible ;
- respectent la rétention ;
- ne suppriment pas un fichier hors périmètre.

---

# 44. Tests de restauration

Une restauration doit être testée sur un environnement isolé.

Scénario :

```text
Créer des données de test
→ sauvegarder
→ détruire l’environnement de test
→ restaurer
→ appliquer les vérifications
→ démarrer l’application
→ exécuter les smoke tests
```

---

## 44.1 Vérifications

- migrations cohérentes ;
- utilisateurs présents ;
- rôles présents ;
- contenus présents ;
- leads présents ;
- médias présents ;
- clés Data Protection présentes lorsque attendues ;
- application démarrable.

---

# 45. Tests de déploiement

Le déploiement complet peut être testé dans un environnement de staging.

Vérifier :

- image ;
- configuration ;
- secrets ;
- migrations ;
- volumes ;
- health ;
- rollback ;
- smoke tests.

La CI standard de pull request ne doit pas déployer en production.

---

# 46. Smoke tests après déploiement

Baseline :

```text
Accueil accessible

HTTPS actif

Health check sain

Admin inaccessible anonymement

Login accessible

Connexion admin possible

Logout possible

Formulaire public fonctionnel

Données persistées

PostgreSQL non public

Headers présents

Aucun domaine de test

Aucun placeholder
```

---

# 47. Tests LLM

Les tests suivent [ADR-0015 — génération déterministe et IA optionnelle](../adr/0015-deterministic-generation-and-optional-ai.md).
Les scénarios ci-dessous restent requis lorsqu'un provider IA est explicitement
implémenté ; cette référence n'annonce pas un provider livré.

Ils utilisent un provider déterministe.

Scénarios :

- succès ;
- réponse vide ;
- JSON invalide ;
- timeout ;
- annulation ;
- provider absent ;
- modèle inconnu ;
- capability absente ;
- secret détecté ;
- donnée personnelle refusée ;
- injection de prompt ;
- information manquante ;
- faux chiffre ;
- statut draft ;
- publication impossible.

---

## 47.1 Aucun appel réel en CI standard

Un provider réel peut être testé dans une suite manuelle ou planifiée avec :

- budget ;
- secret dédié ;
- données fictives ;
- absence de production ;
- limites.

---

# 48. Données de test

## 48.1 Données fictives

Utiliser notamment :

```text
Fictitious Documentation
contact@example.test
admin@example.test
cabinet-alpha.test
```

---

## 48.2 Données réelles interdites

Ne pas utiliser :

- email réel ;
- téléphone réel ;
- adresse personnelle ;
- dump client ;
- message de lead réel ;
- secret réel ;
- contrat réel.

---

## 48.3 Builders

Des builders ou factories peuvent faciliter la création de données valides.

Exemple conceptuel :

```csharp
var lead = LeadTestData.Valid()
    .WithStatus("new")
    .Build();
```

Ils ne doivent pas masquer les valeurs importantes du scénario.

---

## 48.4 Cas limites

Les jeux de données doivent couvrir :

- taille minimale ;
- taille maximale ;
- accents ;
- caractères spéciaux ;
- Unicode ;
- valeur nulle ;
- valeur vide ;
- volume ;
- relations ;
- dates limites.

---

# 49. Déterminisme

## 49.1 Horloge

Les tests utilisent une horloge contrôlée.

Éviter :

```csharp
DateTimeOffset.UtcNow
```

directement dans les assertions.

---

## 49.2 Identifiants

Les identifiants peuvent être prédéfinis lorsque leur valeur affecte l’assertion.

---

## 49.3 Aléatoire

Toute génération aléatoire doit utiliser :

- seed fixe ;
- générateur contrôlé ;
- valeur enregistrée dans le diagnostic.

---

## 49.4 Culture et fuseau

Les tests doivent définir explicitement :

- culture ;
- fuseau ;
- locale ;

lorsque ces valeurs affectent le résultat.

---

## 49.5 Ordre

Les assertions ne doivent pas dépendre de l’ordre d’une collection lorsque le contrat ne garantit pas cet ordre.

Lorsque l’ordre est un contrat, il doit être testé explicitement.

---

# 50. Isolation

## 50.1 Système de fichiers

Chaque test utilise un dossier temporaire distinct.

---

## 50.2 PostgreSQL

Chaque suite utilise une base ou un schéma isolé.

---

## 50.3 Variables d’environnement

Les tests qui modifient les variables doivent :

- restaurer les anciennes valeurs ;
- éviter le parallélisme incompatible ;
- isoler les processus lorsque nécessaire.

---

## 50.4 Ports

Les services de test utilisent des ports dynamiques lorsque possible.

---

## 50.5 Git

Les tests Git utilisent un repository temporaire.

Ils ne doivent jamais modifier le repository de travail réel.

---

# 51. Parallélisme

Les tests doivent être parallélisables lorsque cela est sûr.

Le parallélisme peut être désactivé localement pour les collections utilisant :

- variables d’environnement globales ;
- ressources système partagées ;
- ports fixes ;
- Docker Compose commun ;
- configuration statique mutable.

Une désactivation globale du parallélisme doit être évitée sans nécessité.

---

# 52. Mocks, stubs et fakes

## 52.1 Préférence

Ordre de préférence :

```text
objet réel simple
→ fake déterministe
→ stub ciblé
→ mock lorsque l’interaction est le contrat
```

---

## 52.2 Sur-mocking

Éviter les tests où :

- chaque dépendance est mockée ;
- aucune logique réelle n’est exécutée ;
- le test reproduit l’implémentation ;
- un refactoring sans changement fonctionnel casse toute la suite.

---

## 52.3 Interactions importantes

Un mock est adapté lorsque le contrat concerne :

- email envoyé une seule fois ;
- commande Git appelée avec des arguments sûrs ;
- provider LLM non appelé ;
- fichier protégé non écrit ;
- audit produit.

---

# 53. Convention de nommage des tests

Format recommandé :

```text
MethodOrScenario_Condition_ExpectedResult
```

Exemples :

```csharp
ResolveModules_WhenDependencyIsMissing_ReturnsBlockingError()

Generate_WhenCustomFileExists_PreservesFile()

Login_WhenPasswordIsInvalid_ReturnsGenericError()

Export_WhenUserLacksPermission_ReturnsForbidden()
```

Une formulation française ou anglaise peut être choisie.

Le repository doit rester cohérent.

---

# 54. Structure d’un test

Organisation recommandée :

```text
Arrange
Act
Assert
```

Le test doit permettre d’identifier rapidement :

- contexte ;
- action ;
- résultat attendu.

---

## 54.1 Une intention principale

Un test doit généralement vérifier un comportement principal.

Plusieurs assertions sont acceptables lorsqu’elles décrivent le même résultat.

---

## 54.2 Messages d’assertion

Une assertion doit produire un diagnostic utile.

---

# 55. Tests paramétrés

Les tests paramétrés sont adaptés aux :

- formats ;
- enums ;
- slugs ;
- routes ;
- statuts ;
- valeurs limites ;
- codes HTTP ;
- permissions.

Ils ne doivent pas devenir une matrice illisible contenant plusieurs comportements indépendants.

---

# 56. Couverture de code

## 56.1 Rôle

La couverture aide à identifier les zones peu testées.

Elle ne prouve pas :

- la qualité des assertions ;
- la couverture des risques ;
- la sécurité ;
- l’absence de bugs ;
- la pertinence des scénarios.

---

## 56.2 Baseline progressive

Proposition après stabilisation du socle :

| Périmètre | Couverture de lignes cible | Couverture de branches cible |
|---|---:|---:|
| Domain | 90 % | 80 % |
| Application | 85 % | 75 % |
| Catalog | 90 % | 85 % |
| Generation | 85 % | 75 % |
| Modules critiques | 80 % | 70 % |
| CLI | 75 % | 65 % |

Ces valeurs constituent des objectifs de maintenance, pas une justification pour écrire des tests sans valeur.

---

## 56.3 Premier incrément

Pendant le bootstrap, la CI peut d’abord :

- produire le rapport ;
- suivre la tendance ;
- refuser une forte régression ;
- ne pas bloquer sur un seuil global arbitraire.

Les seuils deviennent bloquants une fois la baseline mesurée et validée.

---

## 56.4 Code exclu

Les exclusions doivent être limitées et documentées.

Exemples possibles :

- code généré ;
- modèles sans logique ;
- composition root triviale ;
- migrations.

---

# 57. Mutation testing

Le mutation testing peut être introduit ultérieurement sur :

- Domain ;
- Catalog ;
- règles de sécurité ;
- génération.

Il permet de vérifier si les tests détectent réellement une modification du comportement.

Il reste hors du premier MVP si son coût ralentit excessivement la CI.

---

# 58. Tests instables

## 58.1 Définition

Un test est instable s’il échoue sans modification pertinente du code.

---

## 58.2 Politique

Un test instable doit être :

1. identifié ;
2. analysé ;
3. corrigé rapidement ;
4. éventuellement isolé temporairement ;
5. suivi par une tâche.

---

## 58.3 Interdiction du retry aveugle

Relancer automatiquement tous les tests plusieurs fois ne doit pas devenir la solution par défaut.

Un retry ciblé peut être utilisé pour diagnostiquer une dépendance externe de test, pas pour masquer un défaut.

---

## 58.4 Causes fréquentes

- temps réel ;
- ordre ;
- données partagées ;
- port fixe ;
- fichier partagé ;
- timeout trop court ;
- attente arbitraire ;
- processus non terminé ;
- dépendance réseau.

---

# 59. Timeouts de tests

Chaque test d’intégration long doit posséder une limite raisonnable.

Les tests ne doivent pas utiliser des délais fixes importants tels que :

```csharp
await Task.Delay(5000);
```

lorsqu’une attente conditionnelle est possible.

---

# 60. Catégorisation des tests

Catégories possibles :

```text
Unit
Contract
Architecture
Integration
PostgreSql
Security
GeneratedProject
Docker
EndToEnd
Performance
Manual
```

Cette catégorisation permet :

- exécution locale rapide ;
- CI par étapes ;
- suites nocturnes ;
- diagnostic.

---

# 61. Suites locales

## 61.1 Suites publiques offline

Après restore verrouillé et build Release de la solution publique :

```bash
dotnet test SiteFactory.slnx \
  --configuration Release \
  --no-build \
  --no-restore \
  --filter "Category!=PostgreSql"
```

Cette commande couvre les onze projets publics offline sans exiger Docker ni
PostgreSQL. Les Facts PostgreSQL exclus ne sont pas comptés comme exécutés.

---

## 61.2 Preuve PostgreSQL explicite

Avec Docker Linux disponible, après restore/build et autorisation de campagne :

```powershell
pwsh -NoProfile -File tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1 -RunRequired
```

Le [contrat du harnais](../../tests/PostgreSql/README.md) impose PostgreSQL 18.6,
TLS VerifyFull, cinq shards et 23 Facts exacts, budgets bornés, TRX neufs et cleanup.
Cette preuve complète les suites offline ; aucune absence, skip ou erreur ne
devient un succès, et aucun retry ne masque une campagne rouge.

---

# 62. Pipeline CI de pull request

Le parcours public actuel est décrit dans le préambule et dans
[les gates CI](20-quality-gates-and-ci.md). Les exigences ci-dessous définissent
aussi les contrôles à ajouter lorsqu'une capacité future est implémentée.

Étapes minimales :

```text
restore
build Release
tests unitaires
tests de contrats
tests d’architecture
tests d’intégration essentielles
validation des catalogues
validation des schémas
secret scan
dependency scan
analyse statique
```

---

## 62.1 Projet généré

Une génération de référence doit être exécutée sur les changements affectant :

- moteur ;
- catalogues ;
- templates ;
- thèmes ;
- modules ;
- génération ;
- CI générée.

Elle peut être exécutée systématiquement si la durée reste acceptable.

---

# 63. Pipeline CI de branche principale

Pour une future contribution applicative possédant ces capacités, la baseline
de branche principale exige au minimum la liste ci-dessous. Elle n'annonce
ni Identity ni image applicative dans le site documentaire statique actuel :

```text
suite de pull request
+ projet généré de référence
+ build du projet généré
+ tests PostgreSQL
+ tests HTTP
+ tests Identity
+ tests de sécurité
+ build Docker
```

---

# 64. Pipeline de release

Une release SiteFactory doit ajouter :

```text
toutes les suites précédentes
tests Windows
tests Linux
tests de reproductibilité
tests de non-écrasement
tests de migrations
analyse des packages
analyse de l’image Docker si produite
rapport de couverture
```

---

# 65. Suite nocturne ou planifiée

Une suite planifiée peut exécuter :

- tests end-to-end navigateur ;
- restore drill ;
- tests de performance ;
- scans approfondis ;
- tests sur plusieurs versions supportées ;
- tests de provider réel avec données fictives ;
- vérification des liens externes approuvés.

La CI standard ne doit pas attendre cette suite pour détecter les erreurs essentielles.

---

# 66. Matrice des plateformes

Baseline :

```text
Windows x64
Linux x64
```

Les tests doivent vérifier :

- chemins ;
- Git ;
- processus ;
- fins de ligne ;
- permissions ;
- fichiers temporaires ;
- génération ;
- CLI.

Les tests Docker principaux peuvent être exécutés sur Linux.

---

# 67. Matrice des versions

Pendant le MVP, une seule version .NET cible est supportée :

```text
.NET 10
```

L’ajout d’autres versions nécessite une décision explicite.

Le SDK est verrouillé par :

```text
global.json
```

---

# 68. Quality gates de pull request

Une pull request ne peut pas être fusionnée si :

```text
[ ] Restore échoué
[ ] Build Release échoué
[ ] Warning bloquant
[ ] Test obligatoire échoué
[ ] Test d’architecture échoué
[ ] Catalogue invalide
[ ] Schéma invalide
[ ] Secret détecté
[ ] Vulnérabilité bloquante
[ ] Analyse statique bloquante
[ ] Projet généré invalide lorsque concerné
[ ] Documentation obligatoire manquante
```

---

# 69. Quality gates de génération client

Une génération client ne peut pas être déclarée réussie si :

```text
[ ] Configuration invalide
[ ] Plan non exécutable
[ ] Conflit non résolu
[ ] Restore échoué
[ ] Build Release échoué
[ ] Test échoué
[ ] Secret détecté
[ ] Placeholder bloquant
[ ] Contrôle de sécurité échoué
[ ] Rapport non produit
```

---

# 70. Quality gates de release

Une release SiteFactory nécessite :

```text
[ ] Version définie
[ ] Changelog
[ ] Build Windows
[ ] Build Linux
[ ] Tous les tests obligatoires
[ ] Projet généré de référence
[ ] Reproductibilité
[ ] Non-écrasement
[ ] Migrations validées
[ ] Secret scan
[ ] Dependency scan
[ ] Analyse statique
[ ] Packages vérifiés
[ ] Documentation à jour
[ ] Aucun risque critique non traité
```

---

# 71. Quality gates avant production client

Avant déploiement :

```text
[ ] Build du commit exact
[ ] Tests du projet client
[ ] Migrations inspectées
[ ] Backup réalisé
[ ] Secret scan
[ ] Configuration production
[ ] Tests Identity
[ ] Tests formulaires
[ ] Tests SEO
[ ] Tests de sécurité
[ ] Docker validé
[ ] Runbook présent
[ ] Rollback présent
```

---

# 72. Rapports de tests

Les rapports doivent inclure :

- total ;
- réussis ;
- échoués ;
- ignorés ;
- durée ;
- projet ;
- plateforme ;
- version .NET ;
- commit ;
- artefacts.

---

## 72.1 Formats

Formats possibles :

```text
TRX
JUnit XML
coverage XML
JSON SiteFactory
```

---

## 72.2 Données sensibles

Les rapports ne contiennent pas :

- secrets ;
- mots de passe ;
- tokens ;
- contenu de leads ;
- dumps ;
- configuration réelle de production.

---

## 72.3 Conservation

La rétention des artefacts de CI doit être adaptée à leur utilité et leur sensibilité.

---

# 73. Diagnostic d’un échec

Un échec doit permettre d’identifier :

- test ;
- scénario ;
- résultat attendu ;
- résultat obtenu ;
- environnement ;
- artefact utile ;
- correlation ID éventuel.

Éviter les messages génériques tels que :

```text
Expected true but was false.
```

sans contexte.

---

# 74. Tests manuels

Les tests automatisés ne couvrent pas complètement :

- qualité éditoriale ;
- cohérence visuelle ;
- compréhension ;
- fluidité ;
- accessibilité réelle ;
- pertinence commerciale ;
- comportements multi-navigateurs complexes.

Une checklist manuelle reste nécessaire avant la recette client.

---

# 75. Recette interne

La recette LogicGate vérifie notamment :

```text
Navigation
Responsive
Formulaires
Administration
Contenus
SEO
Accessibilité
Sécurité visible
Emails
Médias
Erreurs
Production readiness
```

---

# 76. Recette client

Le client valide notamment :

- contenus ;
- services ;
- identité visuelle ;
- coordonnées ;
- parcours ;
- formulaires ;
- mentions ;
- utilisateurs ;
- responsabilités.

La recette client ne remplace pas les tests techniques.

---

# 77. Gestion des défauts

Chaque défaut doit préciser :

```text
Titre
Version
Environnement
Préconditions
Étapes
Résultat attendu
Résultat observé
Sévérité
Impact
Preuves
```

---

## 77.1 Sévérité

Proposition :

```text
Bloquant
Critique
Majeur
Mineur
Cosmétique
```

---

## 77.2 Priorisation

La priorité dépend :

- sévérité ;
- exploitabilité ;
- nombre de projets affectés ;
- présence en production ;
- contournement ;
- risque de propagation par SiteFactory.

---

# 78. Tests et upgrades

Chaque upgrade doit tester :

- version source ;
- version cible ;
- configuration existante ;
- personnalisations ;
- packages ;
- migrations ;
- build ;
- tests ;
- rollback local ;
- lock file.

---

## 78.1 Versions supportées

Il n’est pas nécessaire de tester toutes les anciennes versions indéfiniment.

La politique de support doit définir les versions de départ supportées.

---

# 79. Tests de compatibilité descendante

Pour les formats structurants :

- configuration ;
- lock file ;
- manifests ;
- rapports ;

tester les versions supportées.

Une version inconnue doit être refusée proprement.

---

# 80. Tests des erreurs et diagnostics

Chaque code d’erreur important doit posséder au moins un test vérifiant :

- code ;
- catégorie ;
- sévérité ;
- emplacement ;
- message utile ;
- absence de donnée sensible.

---

# 81. Sécurité des tests

## 81.1 Secrets

Les tests utilisent uniquement des secrets fictifs.

---

## 81.2 Logs

Une suite ne doit pas imprimer toutes les variables d’environnement.

---

## 81.3 Conteneurs

Les conteneurs sont supprimés après utilisation.

---

## 81.4 Dossiers temporaires

Les fichiers temporaires sensibles sont supprimés.

---

## 81.5 Production

Aucun test automatisé standard ne possède les credentials d’une production.

---

# 82. Critères d’acceptation du MVP

La stratégie sera considérée comme opérationnelle lorsque :

1. les projets de tests essentiels existent ;
2. les tests Domain couvrent les règles principales ;
3. les cas d’usage Application sont testés ;
4. les catalogues sont validés ;
5. les schémas sont validés ;
6. les templates sont testés ;
7. les manifests de modules sont testés ;
8. les dépendances architecturales sont testées ;
9. le CLI est testé en processus réel ;
10. validate et plan sont sans effet de bord ;
11. le pipeline de génération est testé ;
12. les stratégies de fichiers sont testées ;
13. les chemins dangereux sont testés ;
14. la reproductibilité est testée ;
15. les personnalisations sont préservées ;
16. un projet de référence est généré ;
17. le projet généré compile en Release ;
18. les tests du projet généré réussissent ;
19. PostgreSQL réel est utilisé pour les tests concernés ;
20. les migrations sont testées ;
21. Identity est testée ;
22. les permissions sont testées ;
23. les formulaires publics sont testés ;
24. les contrôles de sécurité sont testés ;
25. le SEO est testé ;
26. Docker est construit et testé ;
27. les scripts de backup sont testés ;
28. une restauration peut être vérifiée ;
29. les tests LLM utilisent un provider déterministe ;
30. les rapports sont produits ;
31. les quality gates bloquent les échecs ;
32. Windows et Linux sont couverts ;
33. aucun secret ni donnée réelle n’est utilisé ;
34. les tests ignorés sont visibles ;
35. les défauts corrigés possèdent un test de régression.

---

# 83. Décisions validées

La baseline de tests est la suivante :

```text
SiteFactory et les projets générés sont testés séparément.

La stratégie suit une pyramide de tests.

Les règles métier utilisent principalement des tests unitaires.

Les intégrations critiques utilisent leurs technologies réelles.

PostgreSQL réel est utilisé pour les comportements PostgreSQL.

Les tests CLI incluent des exécutions en processus réel.

Validate, plan et dry-run sont testés comme non destructifs.

Les stratégies generated, create-once, merge,
custom et protected possèdent des tests dédiés.

La reproductibilité et le non-écrasement sont testés.

La CI génère un projet client de référence.

Le projet généré doit réussir restore, build Release et tests.

Les migrations appartiennent au projet client et sont testées.

Identity, permissions, antiforgery et rate limiting sont testés.

Les contrôles de sécurité testent également les contournements.

Les tests SEO inspectent le rendu final lorsque nécessaire.

Les tests LLM utilisent un provider déterministe en CI.

La CI standard ne dépend pas d’un service externe payant.

Les données de production réelles sont interdites dans les tests.

Les tests utilisent une horloge et des identifiants contrôlables.

Les tests ne dépendent pas de leur ordre d’exécution.

Un test ignoré n’est pas considéré comme réussi.

Un bug corrigé doit produire un test de régression.

La couverture est un indicateur, pas une preuve suffisante.

Les seuils de couverture sont introduits progressivement.

Une pull request ne peut pas être fusionnée
si un quality gate obligatoire échoue.

Codex ne peut pas supprimer ou affaiblir seul
un test de sécurité ou un quality gate.
```

---

# 84. Règles pour Codex

## 84.1 Lecture préalable

Avant toute modification de tests, Codex doit consulter :

- [Périmètre public](../../README.md) et [invariants des agents](../../AGENTS.md) ;
- [frontière Core/Module](../02-architecture/core-module-boundary.md), [architecture des modules](../02-architecture/09-module-architecture.md) et [entitlements](../02-architecture/10-feature-flags-and-entitlements.md) ;
- [transactions et concurrence](../adr/0028-database-transaction-boundaries-and-concurrency-control.md), [confidentialité et cycle des données](../adr/0023-data-retention-deletion-and-privacy-lifecycle.md), [sessions](../adr/0020-administrator-session-security-and-step-up-authentication.md) et [provisioning](../adr/0021-administrator-provisioning-invitations-and-account-recovery.md) ;
- [pipeline](../03-generation/13-generation-pipeline.md), [configuration](../03-generation/14-client-configuration-schema.md) et [CLI](../03-generation/15-cli-specification.md) ;
- [IA optionnelle](../adr/0015-deterministic-generation-and-optional-ai.md), [sécurité](18-security-baseline.md) et présente stratégie de tests ;
- pour les tests SEO, les exigences autonomes de la section 37 et [CSP/JSON-LD](../adr/0022-browser-security-headers-and-content-security-policy.md#30-json-ld).

Ainsi que :

- code concerné ;
- tests existants ;
- catalogues ;
- schémas ;
- migrations ;
- workflow CI ;
- rapport de revue.

---

## 84.2 Ce que Codex peut faire

Codex peut :

- ajouter un test ;
- reproduire un défaut ;
- créer un fake ;
- créer une fixture ;
- ajouter un projet de tests justifié ;
- améliorer une assertion ;
- ajouter un scénario négatif ;
- ajouter un test PostgreSQL ;
- ajouter un test de sécurité ;
- ajouter un test de génération ;
- corriger un test instable ;
- améliorer un diagnostic ;
- mettre à jour un golden master après validation.

---

## 84.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- supprimer un test échoué pour rendre la CI verte ;
- désactiver une suite de sécurité ;
- ajouter `Skip` sans justification ;
- réduire une assertion à un simple `NotNull` insuffisant ;
- remplacer PostgreSQL par une base en mémoire pour éviter un problème ;
- accepter automatiquement tous les snapshots ;
- utiliser une donnée de production ;
- appeler un provider LLM réel dans la CI standard ;
- rendre les tests dépendants de l’ordre ;
- ajouter des délais fixes inutiles ;
- masquer un test instable avec des retries globaux ;
- modifier un seuil de couverture sans demande ;
- supprimer un quality gate ;
- considérer des tests non exécutés comme réussis ;
- modifier le code de production uniquement pour faciliter un mock sans raison architecturale ;
- créer un test qui reproduit exactement l’implémentation sans vérifier le comportement.

---

## 84.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- suppression d’un test de régression ;
- modification d’un quality gate ;
- modification d’un seuil de couverture ;
- remplacement d’une technologie de test ;
- ajout d’un outil structurant ;
- exclusion de couverture importante ;
- désactivation du parallélisme global ;
- acceptation d’un test instable ;
- ajout d’un service externe en CI ;
- modification d’un golden master affectant le contrat ;
- réduction de la matrice Windows/Linux ;
- retrait d’un test de sécurité ;
- changement des critères de release.

---

## 84.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Fonctionnalité testée
Projets de tests modifiés
Tests unitaires ajoutés
Tests d’intégration ajoutés
Tests PostgreSQL ajoutés
Tests de sécurité ajoutés
Tests de régression ajoutés
Fixtures ajoutées
Golden masters modifiés
Plateformes testées
Build
Nombre total de tests
Tests réussis
Tests échoués
Tests ignorés
Couverture affectée
Durée
Risques non couverts
Décisions humaines restantes
```

Codex ne doit pas conclure qu’une fonctionnalité est complètement testée uniquement parce que tous les tests existants sont verts.

---

# 85. Anti-patterns interdits

## 85.1 Test sans assertion utile

```csharp
await service.ExecuteAsync();
```

sans vérifier le résultat ou les effets.

---

## 85.2 Test de méthode privée

Le test dépend directement d’un détail interne sans contrat fonctionnel.

---

## 85.3 Mock de toute l’application

Le test vérifie uniquement que plusieurs mocks ont été appelés.

---

## 85.4 Base en mémoire comme unique test PostgreSQL

Les migrations et contraintes PostgreSQL ne sont jamais réellement exécutées.

---

## 85.5 `Thread.Sleep`

Un délai fixe est utilisé pour attendre arbitrairement une opération.

---

## 85.6 Test dépendant de la date courante

Le test échoue selon le jour ou le fuseau de la machine.

---

## 85.7 Test dépendant d’Internet

La CI échoue lorsqu’un site ou fournisseur tiers est indisponible.

---

## 85.8 Snapshot accepté en masse

Une commande met à jour toutes les références sans revue des différences.

---

## 85.9 Test ignoré indéfiniment

```csharp
[Fact(Skip = "À corriger")]
```

sans tâche ni propriétaire.

---

## 85.10 Retry global

Tous les tests sont relancés plusieurs fois pour masquer des instabilités.

---

## 85.11 Données réelles

Un dump client est utilisé pour simplifier les scénarios.

---

## 85.12 Assertion sur l’implémentation

Le test exige un ordre précis de méthodes internes alors que le résultat fonctionnel reste inchangé.

---

## 85.13 Couverture artificielle

Des tests appellent des méthodes sans vérifier leur comportement afin d’augmenter le pourcentage.

---

## 85.14 Tests de sécurité uniquement visuels

Le bouton est masqué, mais aucun test HTTP ne vérifie le refus serveur.

---

## 85.15 Succès avec tests ignorés critiques

Une release est approuvée alors qu’une suite critique n’a pas été exécutée.

---

# 86. Roadmap d’implémentation

## Phase tests 1 — Socle

```text
framework de tests
conventions
Domain
Application
Catalog
schémas
architecture
```

---

## Phase tests 2 — CLI et planification

```text
parsing
codes de sortie
sorties human/json
validate
plan
dry-run
```

---

## Phase tests 3 — Génération

```text
stratégies de fichiers
chemins
dossier temporaire
lock file
rapport
reproductibilité
```

---

## Phase tests 4 — Projet généré

```text
solution
restore
build
tests
Identity
SEO
Docker
```

---

## Phase tests 5 — Modules

```text
Content
Contact
Leads
Portfolio
Media
Operations
```

---

## Phase tests 6 — PostgreSQL

```text
modèle
contraintes
migrations
concurrence
rétention
```

---

## Phase tests 7 — Sécurité

```text
authentification
autorisation
antiforgery
rate limiting
XSS
uploads
headers
secrets
```

---

## Phase tests 8 — Système

```text
end-to-end
Docker Compose
backup
restore
staging
smoke tests
```

---

## Phase tests 9 — Qualité avancée

```text
couverture bloquante
mutation testing
tests visuels
performance
DAST
tests multi-navigateurs
```

---

# 87. Checklist de revue d’un test

```text
[ ] Comportement clair
[ ] Nom explicite
[ ] Préconditions lisibles
[ ] Résultat utile
[ ] Pas de dépendance à l’ordre
[ ] Pas de date réelle
[ ] Pas de réseau non contrôlé
[ ] Pas de donnée réelle
[ ] Pas de secret
[ ] Nettoyage prévu
[ ] Diagnostic utile
[ ] Durée raisonnable
```

---

# 88. Checklist de revue d’une suite

```text
[ ] Cas nominal
[ ] Cas invalides
[ ] Limites
[ ] Erreurs techniques
[ ] Annulation
[ ] Autorisation
[ ] Sécurité
[ ] Persistance
[ ] Logs sobres
[ ] Régression
[ ] Plateformes concernées
```

---

# 89. Checklist de pull request

```text
[ ] Build Release réussi
[ ] Nouveaux comportements testés
[ ] Défauts corrigés couverts
[ ] Catalogues valides
[ ] Schémas valides
[ ] Tests d’architecture réussis
[ ] Tests d’intégration réussis
[ ] Projet généré testé si concerné
[ ] Tests de sécurité ajoutés si nécessaire
[ ] Aucun test critique ignoré
[ ] Aucun secret
[ ] Documentation mise à jour
```

---

# 90. Checklist de release

```text
[ ] Tests Windows
[ ] Tests Linux
[ ] Suite complète
[ ] Projet généré de référence
[ ] Reproductibilité
[ ] Non-écrasement
[ ] PostgreSQL
[ ] Migrations
[ ] Identity
[ ] Sécurité
[ ] SEO
[ ] Docker
[ ] Backup
[ ] Restore
[ ] Couverture analysée
[ ] Aucun test instable accepté
[ ] Aucun test critique ignoré
[ ] Changelog
[ ] Documentation
```

---

# 91. Conclusion

La stratégie de tests SiteFactory doit fournir des preuves à plusieurs niveaux :

```text
règle métier
→ composant
→ module
→ intégration
→ projet généré
→ application déployée
```

Elle doit protéger à la fois :

- le moteur de génération ;
- les catalogues ;
- les modules ;
- les personnalisations ;
- les données ;
- les projets clients ;
- les opérations de production.

> SiteFactory n’est pas validé uniquement parce que son propre code compile. Il est validé lorsque les projets qu’il génère sont également cohérents, compilables, testés, sécurisés et reproductibles.
