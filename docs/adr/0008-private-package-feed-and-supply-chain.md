---
adrId: ADR-0008
title: Distribute SiteFactory packages through a private feed with a controlled software supply chain
status: accepted
date: 2026-07-28
decisionOwners:
  - LogicGate
authors:
  - LogicGate
reviewers: []
supersedes: []
supersededBy: null
relatedTasks: []
relatedDocuments:
  - ../../README.md
  - ../../AGENTS.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/09-module-architecture.md
  - ../03-generation/13-generation-pipeline.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/18-security-baseline.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../05-operations/27-client-handover-and-service-closure.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0005-file-generation-strategies.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0033-split-public-sitefactory-and-private-logicgate-modules.md
  - ./0035-partage-public-component-via-nuget.md
  - ./0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md
targetVersion: 0.1.0
---

# ADR-0008 — Feed privé et chaîne d’approvisionnement logicielle contrôlée

## 1. Statut

```text
accepted
```

### Lecture dans la distribution publique

Le statut, la date et les décisions ci-dessous conservent la baseline historique
des **packages internes privés**. Elles n'annoncent ni feed effectivement livré,
ni package déjà publié, ni obligation d'accéder à un feed privé pour construire
SiteFactory public depuis ses sources.

[ADR-0033](0033-split-public-sitefactory-and-private-logicgate-modules.md) impose
l'autonomie du public et
[ADR-0035](0035-partage-public-component-via-nuget.md) distingue explicitement
les futurs composants publics distribués par NuGet des modules propriétaires
distribués par feed privé. Le `NuGet.config` public actuel ne configure que
`nuget.org`, avec les dépendances externes approuvées ; versions centrales,
locks et restore verrouillé restent obligatoires. La publication des packages
publics est une décision de distribution, pas une publication acquise.

### Amendement de portée — 2026-11-09

**Décision humaine confirmée le 2026-11-09**, sans effet rétroactif sur
l'acceptation initiale du **2026-07-28**, dont le statut reste `accepted`.
Cet amendement réconcilie la réservation générale initiale avec
[ADR-0035](0035-partage-public-component-via-nuget.md#clarification-de-portée--2026-11-09) :

- ADR-0035 prévaut sur cette réservation **uniquement pour les identifiants
  explicitement admis à la distribution publique**.
- L'identifiant public déjà décidé est **`LogicGate.SiteFactory.ModuleSdk`**.
  Tout autre Package ID public SiteFactory exige une admission explicite par
  **identifiant exact** ; aucun préfixe entier ne devient public automatiquement.
- Les autres packages internes non admis au public restent réservés au feed
  privé, **sans fallback public**, même si une version homonyme est disponible
  sur une source publique.
- Le préfixe `LogicGate.SiteFactory.*` reste une famille de noms, **pas à lui
  seul une classification de confidentialité**. Les exemples de projets,
  namespaces et futurs packages ne valent pas admission publique. Aucun de ces
  noms ou identifiants n'est renommé par cette décision.

**Portée de lecture des sections historiques suivantes :** les prescriptions
de feed privé pour les « packages internes » visent désormais les identifiants
**non admis explicitement au public**. Cela s'applique aussi au résumé, aux
sections 16–18, à l'outil CLI de 38 et 80, aux tests/codes de refus, au plan et
aux synthèses. La réservation historique globale ne s'applique donc plus au
ModuleSdk public ; les textes initiaux ne sont pas une seconde prescription
contraire simultanément applicable à cet identifiant.

Les exigences de sécurité restent intégrales : sources approuvées et mapping
contrôlé, versions explicites et locks, immutabilité, provenance, publication
protégée, absence de credentials dans le dépôt, protection contre la substitution
et absence de fallback public pour un package privé. L'admission ne dispense
d'aucun de ces contrôles ni des revues de publication prévues en section 67.

**Principe du futur consommateur Business**, pas configuration déjà appliquée :

| Identifiants | Source à mapper |
|---|---|
| `LogicGate.SiteFactory.ModuleSdk`, puis chaque autre ID public admis exactement | Source publique approuvée |
| Autres IDs internes non admis au public | Feed privé approuvé, sans fallback public |
| Dépendances tierces | Sources et mappings tiers approuvés |

Le futur mapping concret devra être vérifié avec un **cache isolé** et une
contre-preuve de substitution : un package privé absent de sa source approuvée
doit être refusé, y compris si un homonyme existe sur une source publique.
**Cette preuve n'a pas été exécutée** par l'amendement. Les extraits XML de
l'ADR sont conceptuels et incomplets sans définition des sources, credentials
externes lorsqu'ils sont nécessaires et mappings des dépendances tierces ;
ils ne sont pas le `NuGet.config` courant du public ou du futur consommateur.
Aucun package n'est déclaré publié, aucun packaging ou changement de
configuration, projet ou lock n'est réalisé ici.

Les packages internes LogicGate SiteFactory seront distribués depuis un feed NuGet privé contrôlé par LogicGate.

Les projets SiteFactory et les projets clients utiliseront :

- des sources de packages explicitement autorisées ;
- un mapping des identifiants vers leur source ;
- des versions explicites ;
- un graphe de dépendances verrouillé ;
- des workflows de publication protégés ;
- des packages immuables ;
- des contrôles de vulnérabilité, licence, contenu et provenance.

Les packages internes ne doivent jamais être restaurés depuis une source publique par fallback.

---

## 2. Résumé

La chaîne d’approvisionnement de référence est :

```text
Code source versionné
→ pull request
→ quality gates
→ merge protégé
→ tag ou déclenchement de release approuvé
→ build Release
→ tests
→ scans
→ pack
→ inspection des packages
→ génération des checksums et métadonnées
→ publication sur le feed privé
→ smoke test du package publié
```

Les identifiants des packages internes utilisent un namespace réservé :

```text
LogicGate.SiteFactory.*
```

Exemples conceptuels de packages internes, pas un inventaire publié :

```text
LogicGate.SiteFactory.Cli

LogicGate.SiteFactory.Platform

LogicGate.SiteFactory.DesignSystem

LogicGate.SiteFactory.Modules.ExampleModule

LogicGate.SiteFactory.Modules.ExampleModule.Contracts
```

Les principales sources autorisées sont :

```text
Feed privé LogicGate
→ packages internes SiteFactory

nuget.org
→ packages publics externes approuvés
```

Le restore doit échouer si un package interne n’est pas disponible sur le feed privé.

Il ne doit pas rechercher une version portant le même identifiant sur une source publique.

---

## 3. Contexte

SiteFactory doit réutiliser des composants génériques dans plusieurs projets clients :

- CLI ;
- générateur ;
- contrats ;
- plateforme ;
- design system ;
- modules ;
- outils opérationnels ;
- templates éventuellement packagés ;
- profils de déploiement ;
- bibliothèques de test.

Ces composants ne doivent pas être copiés manuellement d’un projet client à l’autre.

Ils doivent être :

- versionnés ;
- publiés ;
- restaurables ;
- traçables ;
- testables ;
- compatibles avec les upgrades ;
- disponibles pour les projets supportés ;
- protégés contre les substitutions malveillantes.

Les projets clients peuvent contenir du code propriétaire ou spécifique.

Les packages SiteFactory peuvent également représenter une partie de la valeur commerciale de LogicGate.

Une publication publique par défaut n’est donc pas adaptée au MVP.

Par ailleurs, l’écosystème NuGet permet à un restore de consulter plusieurs sources.

Sans configuration stricte, un attaquant pourrait publier sur une source publique un package utilisant le même identifiant qu’un package interne, avec une version plus élevée.

Ce scénario de dependency confusion doit être empêché par conception.

---

## 4. Problème à résoudre

Comment distribuer les composants SiteFactory à plusieurs repositories clients tout en garantissant :

- confidentialité du code interne ;
- versions exactes ;
- immutabilité des artefacts ;
- reproductibilité du restore ;
- disponibilité pour les projets supportés ;
- protection contre la dependency confusion ;
- contrôle des sources ;
- publication uniquement depuis un pipeline approuvé ;
- traçabilité entre package, commit et release ;
- révocation ou remplacement contrôlé d’une version vulnérable ;
- transfert possible vers un client ou un nouveau feed ?

La solution doit également éviter :

- les packages construits manuellement sur un poste local ;
- les versions flottantes ;
- les tokens de publication dans les repositories ;
- les feeds arbitraires ;
- les packages internes téléchargés depuis une source publique ;
- le remplacement silencieux d’une version existante ;
- la suppression d’une version encore utilisée par un client supporté.

---

## 5. Portée

Cette décision couvre :

- packages NuGet internes ;
- outil .NET SiteFactory ;
- packages de contrats ;
- packages de modules ;
- packages de plateforme ;
- packages de design system ;
- sources NuGet ;
- `NuGet.config` ;
- Package Source Mapping ;
- versions ;
- lock files ;
- restore ;
- cache ;
- publication ;
- authentification au feed ;
- GitHub Actions ;
- provenance ;
- métadonnées ;
- checksums ;
- SBOM ;
- signatures futures ;
- vulnérabilités ;
- licences ;
- retrait d’une version ;
- transfert client.

---

## 6. Hors portée

Cette décision ne définit pas :

- le fournisseur définitif du feed privé ;
- sa tarification ;
- le fournisseur Git exact si GitHub est remplacé ;
- une marketplace publique ;
- la publication open source de certains composants ;
- la licence commerciale exacte des packages ;
- une infrastructure HSM dès le MVP ;
- une garantie de disponibilité du fournisseur de feed ;
- le format complet de la SBOM ;
- la signature obligatoire de tous les packages dès la version `0.1.0` ;
- les packages propres à un projet sur mesure ne réutilisant pas SiteFactory.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Protection contre la dependency confusion**
2. **Immutabilité des packages**
3. **Traçabilité entre source et artefact**
4. **Confidentialité du code interne**
5. **Versions reproductibles**
6. **Contrôle des droits de publication**
7. **Contrôle des dépendances externes**
8. **Compatibilité avec plusieurs repositories clients**
9. **Capacité d’upgrade**
10. **Transfert ou continuité client**
11. **Sécurité de la CI/CD**
12. **Détection des vulnérabilités**
13. **Gestion des licences**
14. **Simplicité d’exploitation pour une petite équipe**
15. **Possibilité future de signature et d’attestation**

---

## 8. Contraintes

La décision doit respecter :

```text
.NET 10

NuGet

GitHub et GitHub Actions dans le MVP

Packages internes non publics par défaut

Versions explicites

Central Package Management

Repository Git par client

Lock file SiteFactory

Quality gates obligatoires

Publication séparée des pull requests

Aucun token de publication dans Git

Aucune version existante remplacée

Compatibilité Windows et Linux

Feed accessible aux projets supportés
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- LogicGate contrôle les repositories produisant les packages ;
- les projets clients supportés peuvent obtenir un accès en lecture au feed ;
- le nombre initial de packages reste raisonnable ;
- les releases sont suffisamment peu fréquentes pour conserver une validation humaine ;
- les packages publics nécessaires sont disponibles sur `nuget.org` ;
- le fournisseur du feed permet des permissions en lecture et en écriture distinctes ;
- les projets clients peuvent utiliser un fichier `NuGet.config` versionné sans credential ;
- les tokens peuvent être injectés par la CI ou un gestionnaire de credentials ;
- un changement de fournisseur de feed reste possible sans changer les Package IDs.

---

# 10. Menaces principales

La décision doit traiter notamment les menaces suivantes.

## 10.1 Dependency confusion

Un attaquant publie sur une source publique :

```text
LogicGate.SiteFactory.Platform
```

avec une version supérieure à celle du feed privé.

Un restore mal configuré pourrait sélectionner le package malveillant.

---

## 10.2 Package typosquatting

Un développeur ajoute par erreur :

```text
LogicGate.SiteFacotry.Platform
```

ou un package externe au nom similaire.

---

## 10.3 Publication compromise

Un token de publication compromis permet d’ajouter un package malveillant sur le feed privé.

---

## 10.4 Build local non maîtrisé

Un package est compilé sur un poste local contenant :

- modifications non committées ;
- outil compromis ;
- dépendance non validée ;
- secret ;
- fichier inattendu.

---

## 10.5 Version remplacée

Le contenu d’une version déjà publiée est modifié sans changement de numéro.

---

## 10.6 Dépendance transitive vulnérable

Un package approuvé introduit une dépendance transitive vulnérable.

---

## 10.7 Action CI compromise

Une action GitHub tierce référencée par un tag mutable exécute un contenu différent.

---

## 10.8 Cache empoisonné

Un cache de packages contient un artefact inattendu ou obsolète.

---

## 10.9 Package contenant un secret

Un fichier de configuration, un token ou une clé est inclus dans le `.nupkg`.

---

## 10.10 Package supprimé

Une version utilisée par un client supporté devient indisponible, empêchant la reconstruction ou la restauration du projet.

---

# 11. Option A — Copier le code source dans chaque projet client

## 11.1 Description

Les composants génériques sont copiés dans chaque repository client.

---

## 11.2 Avantages

- aucun feed ;
- code visible dans le projet ;
- aucun credential de restore ;
- projet autonome ;
- personnalisation facile.

---

## 11.3 Inconvénients

- duplication ;
- propagation des correctifs difficile ;
- dérive ;
- historique incohérent ;
- upgrades manuels ;
- distinction floue entre code générique et client ;
- transfert de propriété intellectuelle potentiellement excessif.

---

## 11.4 Risques

- correctifs de sécurité non propagés ;
- modifications locales du socle ;
- versions impossibles à inventorier ;
- conflits d’upgrade ;
- maintenance par copier-coller.

---

## 11.5 Réversibilité

```text
moyenne
```

Une migration ultérieure vers des packages nécessiterait d’identifier les divergences de chaque client.

---

# 12. Option B — Publier tous les packages sur nuget.org

## 12.1 Description

Les packages SiteFactory sont publics et consommés directement depuis `nuget.org`.

---

## 12.2 Avantages

- distribution simple ;
- aucun token de lecture ;
- haute disponibilité ;
- intégration NuGet standard ;
- transfert client facilité ;
- namespace visible publiquement.

---

## 12.3 Inconvénients

- code et métadonnées publics ;
- publication irréversible ;
- difficulté de protéger les composants commerciaux ;
- namespace exposé ;
- gestion publique des versions ;
- risque de publier accidentellement du contenu interne.

---

## 12.4 Risques

- fuite de propriété intellectuelle ;
- package sensible publié définitivement ;
- nom de client ou configuration inclus ;
- confusion entre composants open source et privés ;
- impossibilité de retirer complètement une publication.

---

## 12.5 Réversibilité

```text
faible
```

Une publication publique ne peut pas être rendue réellement secrète a posteriori.

---

# 13. Option C — Feed privé sans mapping strict

## 13.1 Description

Les projets utilisent :

- le feed privé ;
- `nuget.org` ;

sans règle de mapping entre les identifiants et leur source.

---

## 13.2 Avantages

- configuration simple ;
- fallback automatique ;
- ajout de packages publics facile ;
- faible maintenance initiale.

---

## 13.3 Inconvénients

- risque de dependency confusion ;
- source réelle d’un package moins visible ;
- comportement dépendant des versions disponibles ;
- package interne manquant pouvant être recherché publiquement ;
- audits plus difficiles.

---

## 13.4 Risques

- restauration d’un package malveillant ;
- version publique plus élevée ;
- cache masquant temporairement le défaut ;
- différence entre local et CI.

---

## 13.5 Réversibilité

```text
élevée
```

mais le risque existe tant que le mapping n’est pas appliqué.

---

# 14. Option D — Feed privé avec sources, versions et publication contrôlées

## 14.1 Description

Les packages internes sont publiés sur un feed privé.

Les projets utilisent :

- Package Source Mapping ;
- versions explicites ;
- lock files ;
- pipeline protégé ;
- métadonnées de provenance ;
- scans ;
- publication immuable.

---

## 14.2 Avantages

- protection forte contre la dependency confusion ;
- confidentialité ;
- traçabilité ;
- restore reproductible ;
- contrôle de publication ;
- séparation lecture/écriture ;
- inventaire des versions ;
- possibilité de transfert ;
- compatibilité avec les upgrades.

---

## 14.3 Inconvénients

- authentification nécessaire ;
- dépendance au fournisseur ;
- configuration NuGet plus détaillée ;
- gestion des accès clients ;
- workflow de release plus complexe ;
- indisponibilité du feed pouvant bloquer les builds.

---

## 14.4 Risques

- mauvais mapping ;
- token expiré ;
- package non transféré lors d’un handover ;
- publication partielle ;
- rétention incorrecte ;
- feed indisponible.

---

## 14.5 Réversibilité

```text
élevée
```

Les packages peuvent être copiés vers un autre feed en conservant leurs IDs et versions.

---

# 15. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Code copié | nuget.org | Feed sans mapping | Feed contrôlé |
|---|---:|---:|---:|---:|---:|
| Confidentialité | 5 | 3 | 1 | 5 | 5 |
| Dependency confusion | 5 | 5 | 4 | 1 | 5 |
| Propagation des correctifs | 5 | 1 | 5 | 5 | 5 |
| Reproductibilité | 5 | 2 | 4 | 2 | 5 |
| Traçabilité | 5 | 2 | 4 | 3 | 5 |
| Transfert client | 4 | 4 | 5 | 3 | 4 |
| Contrôle des publications | 5 | 2 | 3 | 4 | 5 |
| Simplicité initiale | 3 | 4 | 5 | 4 | 3 |
| Maintenabilité | 5 | 1 | 4 | 3 | 5 |
| Réversibilité | 4 | 3 | 2 | 4 | 5 |

Le feed privé avec mapping et chaîne de publication contrôlée est retenu.

---

# 16. Décision

**Décision initiale conservée ; portée amendée le 2026-11-09 :** la réservation
ci-dessous s'applique aux packages internes non admis explicitement au public,
pas à `LogicGate.SiteFactory.ModuleSdk` admis par ADR-0035.

Nous décidons d’utiliser un feed NuGet privé contrôlé par LogicGate pour distribuer les packages internes SiteFactory.

Le fournisseur initial pourra être :

```text
GitHub Packages
```

ou tout autre service approuvé offrant les garanties définies dans cet ADR.

Le fournisseur constitue un détail d’infrastructure.

Les invariants sont :

- Package IDs stables ;
- versions immuables ;
- sources mappées ;
- accès limités ;
- publication protégée ;
- conservation des versions supportées ;
- portabilité vers un autre feed.

---

## 16.1 Packages publics

Les packages publics externes sont restaurés depuis :

```text
nuget.org
```

sauf exception documentée et approuvée.

---

## 16.2 Sources supplémentaires

Une nouvelle source nécessite :

- justification ;
- propriétaire ;
- analyse de confiance ;
- Package Source Mapping ;
- documentation ;
- validation humaine.

---

## 16.3 Sources locales

Les dossiers locaux ou feeds temporaires sont autorisés uniquement pour :

- développement ;
- tests ;
- validation de packages avant publication.

Ils sont interdits dans les restores officiels de release et de production.

---

# 17. Namespace des Package IDs

Depuis l'amendement du 2026-11-09, ce préfixe est un nommage commun, pas une
classification de confidentialité. Seule l'admission par ID exact détermine
la distribution publique ; les exemples suivants ne sont pas des admissions.

Tous les packages internes utilisent le préfixe :

```text
LogicGate.SiteFactory.
```

---

## 17.1 Exemples

```text
LogicGate.SiteFactory.Cli

LogicGate.SiteFactory.Core

LogicGate.SiteFactory.Platform

LogicGate.SiteFactory.DesignSystem

LogicGate.SiteFactory.Operations

LogicGate.SiteFactory.Modules.ExampleModule

LogicGate.SiteFactory.Modules.ExampleModule.Contracts
```

---

## 17.2 Identifiants clients

Les packages spécifiques à un client ne doivent pas utiliser le namespace générique comme s’ils étaient des composants réutilisables.

Convention conceptuelle :

```text
LogicGate.Clients.{ClientTechnicalId}.*
```

ou conservation directe dans le repository client.

La décision exacte dépend du projet.

---

## 17.3 Stabilité

Un Package ID publié ne doit pas être renommé silencieusement.

---

## 17.4 Typosquatting interne

Les noms proches ou ambigus doivent être refusés par la revue et les outils de validation.

---

# 18. Package Source Mapping

Le repository doit contenir un :

```text
NuGet.config
```

définissant :

- les sources ;
- le mapping ;
- l’absence de credentials ;
- la politique de restore.

---

## 18.1 Principe

Les packages internes sont mappés exclusivement vers le feed privé.

**Exemple initial historique, conceptuel et incomplet** : sa réservation du
préfixe entier ne doit pas être appliquée au ModuleSdk public depuis l'amendement
du 2026-11-09. Ce n'est pas le mapping courant d'une distribution mixte ; les
sources et mappings des dépendances tierces doivent aussi être définis.

```xml
<packageSourceMapping>
  <packageSource key="logicgate-private">
    <package pattern="LogicGate.SiteFactory.*" />
  </packageSource>

  <packageSource key="nuget.org">
    <package pattern="*" />
  </packageSource>
</packageSourceMapping>
```

Dans cet exemple historique, la configuration réelle devait être testée afin
de confirmer que le pattern interne plus spécifique était résolu exclusivement
depuis le feed privé. Pour le futur consommateur mixte, la vérification porte
sur les IDs publics admis exactement et les autres IDs internes privés selon
l'amendement, avec cache isolé et refus de substitution ; elle reste à exécuter.

---

## 18.2 Échec attendu

Si :

```text
LogicGate.SiteFactory.Platform 1.2.0
```

est absent du feed privé, le restore échoue.

Il ne doit pas utiliser une version publique portant le même identifiant.

---

## 18.3 Mapping par défaut

Tout package doit correspondre à une source autorisée.

Une source non mappée n’est pas consultée.

---

## 18.4 Validation automatisée

La CI doit vérifier que :

- tous les packages internes correspondent au feed privé ;
- aucune source inconnue n’est présente ;
- aucun credential n’est committé ;
- le Package Source Mapping est actif.

---

# 19. Ordre des sources

L’ordre des sources ne constitue pas une protection suffisante.

La sécurité repose sur :

```text
mapping explicite
+ versions verrouillées
+ sources approuvées
```

Le feed privé placé avant `nuget.org` sans mapping ne satisfait pas cette décision.

---

# 20. Désactivation des sources utilisateur

Les restores officiels ne doivent pas dépendre de sources ajoutées globalement sur le poste de l’utilisateur.

---

## 20.1 Configuration repository

Le repository fournit sa propre configuration NuGet.

---

## 20.2 CI

La CI utilise explicitement la configuration du repository.

---

## 20.3 Audit

Le restore doit pouvoir afficher ou enregistrer les sources réellement utilisées sans exposer leurs credentials.

---

# 21. Central Package Management

Les versions des dépendances des projets utilisent :

```text
Directory.Packages.props
```

lorsque cela correspond à la structure du repository.

---

## 21.1 Objectifs

- centraliser les versions ;
- simplifier les upgrades ;
- détecter les divergences ;
- faciliter les scans ;
- améliorer la revue.

---

## 21.2 Version directe

Une dépendance doit utiliser une version explicite.

Les références suivantes sont interdites :

```text
*

1.*

latest

prerelease la plus récente
```

---

## 21.3 Override local

Un override local nécessite :

- justification ;
- commentaire ou documentation ;
- test ;
- plan de suppression.

---

# 22. Lock files NuGet

Les repositories destinés à être reconstruits de manière reproductible doivent utiliser :

```text
packages.lock.json
```

selon la stratégie applicable au type de projet.

---

## 22.1 Versionnement Git

Les lock files sont versionnés.

---

## 22.2 Locked mode

La CI utilise le restore en mode verrouillé pour les branches et releases où la reproductibilité est requise.

---

## 22.3 Modification du graphe

Un changement de dépendance transitive doit apparaître dans le diff du lock file.

---

## 22.4 Échec

Si le graphe restauré ne correspond pas au lock file :

```text
restore failure
```

---

## 22.5 Projets générés

Les projets clients générés doivent recevoir une stratégie cohérente de lock files adaptée à leur solution.

---

# 23. Différence avec `sitefactory.lock.json`

```text
packages.lock.json
→ graphe NuGet résolu

sitefactory.lock.json
→ composition fonctionnelle SiteFactory
```

Les deux fichiers répondent à des responsabilités différentes.

---

## 23.1 Cohérence

Une version de module enregistrée dans `sitefactory.lock.json` doit correspondre à la version NuGet réellement restaurée.

Une incohérence est bloquante.

---

# 24. Versions des dépendances internes

## 24.1 Références projet

Les projets clients utilisent des versions internes explicites.

---

## 24.2 Dépendances publiées

Les packages internes fortement couplés utilisent :

- une version exacte ;
- ou une plage strictement bornée et testée.

---

## 24.3 Avant `1.0`

Avant la stabilisation des contrats publics, les dépendances internes privilégient des versions exactes.

---

## 24.4 Plage autorisée

Une plage n’est autorisée que si :

- la compatibilité est contractuelle ;
- les versions concernées sont testées ;
- la borne supérieure est définie ;
- aucune preview n’est sélectionnée implicitement.

---

## 24.5 Plage interdite

Exemples interdits :

```text
>=1.0.0 sans borne supérieure

*

latest

toute preview
```

---

# 25. Versions des dépendances externes

Les dépendances publiques utilisent des versions explicites dans les projets.

---

## 25.1 Upgrades

Une nouvelle version est introduite par :

```text
pull request
→ analyse
→ lock file
→ build
→ tests
→ scans
→ revue
```

---

## 25.2 Mise à jour automatique

Un outil peut proposer une pull request.

Il ne doit pas :

- fusionner automatiquement ;
- publier automatiquement ;
- déployer automatiquement.

---

## 25.3 Preview

Une version preview externe nécessite une validation explicite.

---

# 26. Dépendances transitives

Les dépendances transitives doivent être :

- visibles dans les lock files ;
- incluses dans les scans ;
- incluses dans la SBOM ;
- examinées lors d’une alerte.

---

## 26.1 Pinning transitif

Le pinning transitif peut être utilisé lorsqu’un besoin de correction ou de contrôle est démontré.

Il doit rester documenté afin d’éviter un override permanent oublié.

---

## 26.2 Conflit de versions

Les downgrades, conflits ou résolutions inattendues sont traités comme warnings bloquants selon la configuration du repository.

---

# 27. Authentification au feed

## 27.1 Repository

Le fichier `NuGet.config` ne contient aucune valeur de credential.

---

## 27.2 Développement local

L’authentification utilise :

- gestionnaire de credentials ;
- token injecté temporairement ;
- mécanisme fourni par le feed ;
- configuration utilisateur hors repository.

---

## 27.3 CI

La CI utilise un secret ou token de portée minimale.

---

## 27.4 Logs

Le token ne doit jamais être :

- affiché ;
- passé en clair dans une commande journalisée ;
- écrit dans un rapport ;
- conservé dans un artefact.

---

# 28. Séparation lecture et publication

## 28.1 Token de lecture

Les projets clients et les jobs de build utilisent uniquement un droit :

```text
read packages
```

---

## 28.2 Token de publication

Le droit :

```text
write packages
```

est réservé au workflow de release protégé.

---

## 28.3 Suppression

Le droit de suppression ou administration du feed n’est pas accordé au job de publication courant sauf nécessité explicite.

---

## 28.4 Compte personnel

Une publication stable ne dépend pas d’un token personnel permanent d’un développeur.

---

# 29. Environnement de release protégé

La publication utilise un environnement CI dédié.

Exemple :

```text
nuget-release
```

Il possède :

- approbation humaine ;
- secrets limités ;
- restrictions de branche ou de tag ;
- logs ;
- historique ;
- droits minimaux.

---

## 29.1 Pull requests

Les pull requests n’obtiennent jamais le token de publication.

---

## 29.2 Code non fiable

Un workflow exécutant du code provenant d’une contribution non approuvée ne reçoit pas de credential sensible.

---

# 30. Workflow de publication

Le workflow de référence est :

```text
Validate version and tag
→ checkout exact commit
→ setup locked SDK
→ restore locked dependencies
→ build Release
→ tests
→ generated project validation
→ security scans
→ pack
→ inspect package
→ generate checksums
→ generate SBOM
→ generate release manifest
→ approval
→ publish
→ install from private feed
→ smoke test
```

---

## 30.1 Même workflow

Les artefacts publiés sont ceux qui ont été :

- construits ;
- testés ;
- scannés ;
- inspectés ;

dans le même workflow.

---

## 30.2 Rebuild interdit

Le job de publication ne reconstruit pas un nouvel artefact différent après les contrôles.

---

# 31. Publication depuis un tag

La release stable est déclenchée depuis un tag ou une opération explicitement approuvée.

Le tag doit correspondre :

- à la version ;
- au commit ;
- au changelog ;
- au release manifest.

---

## 31.1 Pull request

Une pull request ne publie jamais un package stable.

---

## 31.2 Branche de développement

Une branche de fonctionnalité ne publie pas sur le canal stable.

---

# 32. Publication locale

La publication d’une version officielle depuis un poste local est interdite.

---

## 32.1 Package de test

Un développeur peut construire un package local pour :

- inspection ;
- feed temporaire ;
- test d’installation.

Il ne doit pas utiliser la version stable officielle si le package n’a pas été publié par le pipeline.

---

## 32.2 Marquage

Les packages locaux éventuels utilisent une version de développement clairement identifiable.

Exemple :

```text
0.6.0-dev.abc1234
```

---

# 33. Métadonnées NuGet obligatoires

Chaque package doit définir selon son type :

```text
PackageId

Version

Authors

Description

RepositoryUrl

RepositoryType

RepositoryCommit

PackageTags

PackageLicenseExpression ou fichier de licence

Readme éventuel

PackageReleaseNotes ou référence adaptée
```

---

## 33.1 Repository commit

Le commit doit correspondre au code réellement packagé.

---

## 33.2 Description

La description ne doit pas contenir :

- nom de client confidentiel ;
- secret ;
- information interne non nécessaire ;
- engagement commercial non validé.

---

# 34. SourceLink et symboles

SourceLink peut être activé lorsque :

- la politique d’accès au repository le permet ;
- les chemins ne révèlent pas d’information sensible ;
- le package de symboles est contrôlé.

---

## 34.1 Symbol packages

Les symbol packages sont publiés uniquement sur une destination approuvée.

---

## 34.2 Repository privé

Un consommateur sans accès au repository ne doit pas recevoir implicitement un accès à son contenu par une mauvaise configuration.

---

# 35. Contenu des packages

Les packages internes peuvent contenir :

- assemblies ;
- documentation XML ;
- fichiers nécessaires au build ;
- assets de génération ;
- manifests ;
- schémas ;
- composants statiques prévus ;
- métadonnées.

---

## 35.1 Contenu interdit

Un package ne doit pas contenir :

```text
.env

secrets

tokens

clés privées

certificats privés

backups

dumps PostgreSQL

données client

logs

fichiers temporaires

binaires non nécessaires

configuration de production

historique Git
```

---

## 35.2 Inspection

La CI doit inspecter la liste des fichiers du package avant publication.

---

## 35.3 Fichier inattendu

Un fichier non déclaré ou sensible bloque la release.

---

# 36. Packages de contrats

Les packages de contrats sont séparés des implémentations lorsque plusieurs modules ou customizations doivent les consommer.

Exemple :

```text
LogicGate.SiteFactory.Modules.ExampleModule.Contracts
```

---

## 36.1 Dépendances minimales

Un package de contrats ne doit pas dépendre :

- d’EF Core ;
- du provider PostgreSQL ;
- d’ASP.NET Core complet sans besoin ;
- d’Infrastructure ;
- du CLI ;
- de bibliothèques lourdes inutiles.

---

## 36.2 Stabilité

Les contrats publics suivent les règles SemVer définies dans l’ADR-0007.

---

# 37. Packages d’implémentation

Un package d’implémentation peut dépendre :

- de son package de contrats ;
- de la plateforme ;
- d’abstractions approuvées ;
- de dépendances externes nécessaires.

Il ne doit pas exposer accidentellement ses types internes comme contrats publics.

---

# 38. CLI comme outil .NET

Cette possibilité historique vise un outil interne **non admis** au public.
L'existence du projet CLI ne constitue pas une admission de son Package ID ;
seul ModuleSdk est actuellement admis, sans package effectivement publié.

Le CLI peut être distribué comme :

```text
dotnet tool
```

depuis le feed privé.

---

## 38.1 Manifest local

Les repositories peuvent verrouiller sa version dans :

```text
.config/dotnet-tools.json
```

---

## 38.2 CI

La CI utilise la version déclarée par le repository.

Elle ne récupère pas automatiquement la dernière version disponible.

---

# 39. Templates et thèmes

Les templates et thèmes peuvent être distribués :

- dans des packages NuGet dédiés ;
- dans des archives attachées à une release ;
- dans le package du générateur pour le MVP.

Le mode retenu doit conserver :

- version ;
- provenance ;
- checksum ;
- immutabilité ;
- source approuvée.

---

## 39.1 Code exécutable

Un template ne doit pas embarquer ou exécuter arbitrairement un script post-install.

---

## 39.2 Chemins

Le contenu est soumis aux protections de chemins de l’ADR-0005.

---

# 40. Actions GitHub

## 40.1 Pinning

Les actions externes utilisées dans les workflows sensibles doivent être référencées par un commit SHA immuable.

Exemple conceptuel :

```yaml
uses: actions/checkout@<commit-sha> # vX.Y.Z
```

---

## 40.2 Commentaire de version

Le commentaire conserve la version lisible afin de faciliter les upgrades.

---

## 40.3 Mise à jour

Les actions sont mises à jour par pull request et testées.

---

## 40.4 Action non approuvée

Une nouvelle action tierce nécessite :

- besoin ;
- éditeur ;
- maintenance ;
- permissions ;
- historique ;
- analyse de sécurité.

---

# 41. Permissions GitHub Actions

La baseline est :

```yaml
permissions:
  contents: read
```

Les permissions supplémentaires sont ajoutées par job.

---

## 41.1 Publication

Le job de publication peut recevoir :

```text
packages: write
```

uniquement lorsqu’il en a besoin.

---

## 41.2 `write-all`

La permission :

```yaml
permissions: write-all
```

est interdite.

---

## 41.3 Pull requests

Les workflows de pull request utilisent les droits minimaux et n’exposent aucun secret de publication.

---

# 42. SDK .NET

La version du SDK est verrouillée dans :

```text
global.json
```

selon la politique de support.

---

## 42.1 Image runner

Le workflow ne doit pas dépendre uniquement de la dernière version implicite installée sur le runner.

---

## 42.2 Mise à jour

Une mise à jour du SDK utilise :

- branche ;
- build ;
- tests ;
- génération ;
- package ;
- smoke test.

---

# 43. Reproductibilité

Pour une release donnée, il doit être possible d’identifier :

```text
repository
commit
tag
SDK
sources NuGet
lock files
versions
workflow
artefacts
checksums
```

---

## 43.1 Build déterministe

Les options de build déterministe et de path mapping doivent être activées lorsque compatibles avec le projet.

---

## 43.2 Données dynamiques

Les timestamps ou informations du poste local ne doivent pas modifier inutilement les assemblies.

---

# 44. Checksums

Les artefacts de release reçoivent une empreinte :

```text
SHA-256
```

---

## 44.1 Manifest

Le checksum est enregistré dans :

```text
sitefactory-release-manifest.json
```

---

## 44.2 Limite

Le checksum publié dans le même système que le package ne remplace pas une signature indépendante.

Il améliore néanmoins :

- l’intégrité ;
- la comparaison ;
- le transfert ;
- les restore drills de packages.

---

# 45. SBOM

Les releases stables doivent progressivement produire une Software Bill of Materials.

Elle doit inclure :

- packages directs ;
- packages transitifs ;
- versions ;
- hashes lorsque disponibles ;
- licences ;
- composants de l’image lorsqu’elle est concernée.

---

## 45.1 MVP

La SBOM est une cible recommandée dès les premières releases et devient obligatoire au plus tard pour la première version stable largement distribuée.

---

## 45.2 Données interdites

La SBOM ne contient aucun secret ni donnée client.

---

# 46. Provenance

La release doit fournir des informations permettant de relier l’artefact à son origine.

Minimum :

```text
Repository

CommitSha

GitTag

WorkflowRun

PackageId

PackageVersion

Checksum

PublishedAtUtc
```

---

## 46.1 Attestation future

Une attestation de provenance signée pourra être ajoutée lorsque l’outillage de release sera stabilisé.

---

# 47. Signature des packages

## 47.1 MVP

La signature NuGet n’est pas obligatoire pour le premier incrément du MVP si les garanties suivantes sont présentes :

- feed privé ;
- workflow protégé ;
- version immuable ;
- Package Source Mapping ;
- checksums ;
- provenance ;
- lock files ;
- accès limités.

---

## 47.2 Cible stable

La signature des packages ou une attestation équivalente devient une exigence à étudier avant la première version stable largement distribuée.

---

## 47.3 Clés

Toute clé de signature doit être :

- protégée ;
- hors des pull requests ;
- limitée au workflow de release ;
- renouvelable ;
- accompagnée d’une procédure de révocation.

---

# 48. Scans de dépendances

La CI doit analyser :

- dépendances directes ;
- dépendances transitives ;
- packages dépréciés ;
- versions vulnérables ;
- sources inconnues.

---

## 48.1 Outils

Les outils peuvent comprendre :

- alertes GitHub ;
- Dependabot ;
- audit NuGet ;
- scanner de SBOM ;
- scanner d’image.

La politique ne dépend pas d’un seul outil.

---

## 48.2 Finding critique

Une vulnérabilité critique exploitable bloque une release stable.

---

## 48.3 Finding élevé

Une vulnérabilité élevée bloque normalement la release ou nécessite une mitigation formelle approuvée.

---

# 49. Scan de secrets

Le scan de secrets couvre :

- code ;
- historique pertinent ;
- fichiers de configuration ;
- contenu des packages ;
- artefacts de release ;
- workflows.

---

## 49.1 Package final

Le `.nupkg` doit lui-même être inspecté.

Un scan uniquement sur le repository peut manquer un fichier généré pendant le pack.

---

# 50. Analyse statique

Les packages sont construits uniquement après les analyses requises :

- analyzers .NET ;
- analyse statique ;
- tests d’architecture ;
- contrôles des APIs interdites ;
- règles de sécurité applicables.

---

# 51. Licences

Chaque nouvelle dépendance externe doit être analysée.

Informations minimales :

```text
PackageId

Version

Licence

Usage

Distribution

Dépendances transitives

Contraintes
```

---

## 51.1 Licence inconnue

Une dépendance dont la licence est absente, ambiguë ou incompatible est refusée jusqu’à clarification.

---

## 51.2 Copyleft

Une licence imposant des obligations incompatibles avec le mode de distribution prévu nécessite une décision humaine.

---

## 51.3 Inventaire

Les licences doivent être intégrables dans la SBOM ou le rapport de release.

---

# 52. Ajout d’une dépendance

Une pull request ajoutant un package doit expliquer :

- le besoin ;
- pourquoi les APIs .NET existantes ne suffisent pas ;
- la maintenance du package ;
- sa licence ;
- sa taille ;
- ses dépendances transitives ;
- son impact sécurité ;
- son usage prévu.

---

## 52.1 Dépendance structurante

Une dépendance structurante nécessite une décision humaine et éventuellement un ADR.

---

## 52.2 Dépendance de test

Une dépendance utilisée uniquement dans les tests reste soumise :

- aux versions ;
- aux licences ;
- aux vulnérabilités ;
- aux sources autorisées.

---

# 53. Packages inutilisés

Les packages inutilisés doivent être supprimés.

---

## 53.1 Analyse

La suppression doit vérifier :

- usage direct ;
- usage généré ;
- reflection éventuelle ;
- build ;
- tests ;
- packaging.

---

## 53.2 Lock file

La suppression doit mettre à jour le graphe verrouillé.

---

# 54. Caches

Les caches de packages servent à accélérer les builds.

Ils ne constituent pas une source de vérité.

---

## 54.1 Clé de cache

La clé doit prendre en compte notamment :

- système ;
- SDK ;
- fichiers de projets ;
- `Directory.Packages.props` ;
- lock files ;
- `NuGet.config`.

---

## 54.2 Cache corrompu

Le pipeline doit pouvoir effectuer un restore propre sans cache.

---

## 54.3 Artefact non autorisé

Un cache ne doit pas permettre de contourner Package Source Mapping ou la validation du lock file.

---

# 55. Feed de test

Un feed ou dossier temporaire peut être utilisé pour tester un package avant publication stable.

---

## 55.1 Isolation

Le feed de test ne doit pas partager le canal stable de façon ambiguë.

---

## 55.2 Versions

Les versions de test utilisent un suffixe preview ou développement.

---

## 55.3 Smoke test

Le package doit être restauré depuis le feed de test dans un environnement propre.

---

# 56. Smoke test post-publication

Après publication sur le feed privé :

1. nettoyer ou isoler le cache ;
2. configurer uniquement les sources prévues ;
3. installer ou restaurer le package publié ;
4. vérifier sa version ;
5. générer un projet si nécessaire ;
6. exécuter restore, build et tests ;
7. vérifier les métadonnées ;
8. consigner le résultat.

---

## 56.1 Package réellement publié

Le smoke test ne doit pas utiliser l’artefact local d’avant publication.

---

# 57. Immutabilité

Une version publiée ne doit jamais être remplacée par un contenu différent.

---

## 57.1 Échec ou défaut

Une correction produit :

```text
nouveau commit
→ nouvelle version
→ nouveau package
```

---

## 57.2 Version existante

La publication échoue si la version existe déjà.

---

## 57.3 Option de duplication

Une option telle que :

```text
skip duplicate
```

ne doit pas masquer une tentative de republier une version.

Elle peut être utilisée uniquement dans un scénario contrôlé où le contenu existant est déjà confirmé identique, ce qui n’est pas la baseline.

---

# 58. Publication partielle

Si certains packages sont publiés et d’autres échouent :

1. arrêter la publication ;
2. inventorier les artefacts publiés ;
3. ne pas remplacer les versions ;
4. marquer la release incomplète ;
5. créer une nouvelle version coordonnée ;
6. documenter l’incident.

---

## 58.1 Release coordonnée

Une release coordonnée ne doit pas être présentée comme complète si l’un de ses composants obligatoires manque.

---

# 59. Rétention des packages

Les versions utilisées par un projet supporté doivent rester disponibles.

---

## 59.1 Inventaire

LogicGate doit pouvoir identifier :

- les projets ;
- les Package IDs ;
- les versions ;
- le statut de support.

---

## 59.2 Suppression interdite

Une version utilisée par un client supporté ne doit pas être supprimée afin de libérer simplement de l’espace.

---

## 59.3 Nettoyage

Le nettoyage peut viser :

- builds internes temporaires ;
- versions preview expirées ;
- artefacts non référencés ;

selon une politique documentée.

---

# 60. Version vulnérable

Une version vulnérable peut être :

```text
deprecated
unlisted
withdrawn
```

selon les capacités du feed.

---

## 60.1 Remplacement interdit

Elle n’est jamais remplacée sous le même numéro.

---

## 60.2 Nouvelle version

Un correctif produit une nouvelle version patch ou adaptée à SemVer.

---

## 60.3 Projets affectés

L’inventaire des projets doit permettre d’identifier les clients à mettre à niveau.

---

## 60.4 Suppression exceptionnelle

Une suppression complète peut être envisagée si le package contient :

- secret ;
- malware ;
- contenu légalement problématique.

La décision reste humaine et doit analyser les projets dépendants.

---

# 61. Feed indisponible

Une indisponibilité du feed peut empêcher :

- restore ;
- build ;
- release ;
- reconstruction d’un projet.

Elle ne doit pas interrompre une application cliente déjà déployée.

---

## 61.1 Mesures

- cache de CI non considéré comme sauvegarde unique ;
- conservation des packages de release ;
- export ou réplication possible ;
- runbook ;
- fournisseur alternatif préparé pour un incident prolongé.

---

## 61.2 Déploiement urgent

Un déploiement ne doit pas utiliser un package reconstruit localement sous une version existante pour contourner la panne du feed.

---

# 62. Sauvegarde du feed

La politique de continuité doit permettre de conserver :

- packages ;
- versions ;
- métadonnées ;
- checksums ;
- manifests ;
- SBOM ;
- notes de release.

---

## 62.1 Export

Les packages peuvent être exportés vers un stockage sécurisé indépendant du feed.

---

## 62.2 Restauration

Un exercice de continuité peut vérifier qu’un package exporté peut être publié sur un feed temporaire et restauré.

---

# 63. Changement de fournisseur

Un changement de fournisseur de feed doit préserver :

- Package IDs ;
- versions ;
- checksums ;
- métadonnées ;
- disponibilité des versions supportées ;
- mapping ;
- credentials ;
- historique.

---

## 63.1 Migration

Flux :

```text
inventaire
→ export
→ vérification
→ nouveau feed
→ import
→ comparaison des checksums
→ mise à jour NuGet.config
→ restore locked
→ tests
→ retrait de l’ancien accès
```

---

## 63.2 Pas de republication différente

Les packages importés conservent leurs octets et checksums d’origine.

---

# 64. Transfert client

Un transfert complet doit déterminer comment le client pourra reconstruire le projet après la fin de la maintenance LogicGate.

Options :

- maintien d’un accès en lecture au feed ;
- copie des packages nécessaires vers un feed client ;
- remise d’une archive vérifiée des packages autorisés ;
- publication de composants devenus open source ;
- contrat d’accès spécifique.

---

## 64.1 Condition de transfert

Le client doit disposer de toutes les versions nécessaires à la reconstruction de la version remise.

---

## 64.2 Token LogicGate

Le client ne reçoit pas un token mutualisé donnant accès aux packages ou projets d’autres clients.

---

## 64.3 Feed client

Le transfert vers un feed client doit vérifier les checksums.

---

# 65. Propriété intellectuelle et licences internes

Le transfert d’un package ne modifie pas automatiquement :

- sa licence ;
- sa propriété ;
- les droits de redistribution ;
- les droits de modification.

Ces éléments relèvent du contrat ou de la licence applicable.

L’ADR définit la distribution technique, non les droits juridiques.

---

# 66. Packages spécifiques client

Un package propre à un client ne doit pas être accessible aux autres clients.

---

## 66.1 Préférence initiale

Le code spécifique reste préférentiellement dans le repository client.

---

## 66.2 Publication privée

Une publication séparée peut être utilisée si :

- plusieurs applications du même client en dépendent ;
- la réutilisation est réelle ;
- l’isolation du feed ou des permissions est garantie.

---

## 66.3 Données

Un package client ne contient aucune donnée métier ou secret du client.

---

# 67. Packages open source futurs

LogicGate pourra décider de publier certains composants publiquement.

Cette publication nécessite :

- revue du code ;
- revue des secrets ;
- revue de licence ;
- nettoyage de l’historique pertinent ;
- documentation ;
- Package ID ;
- nouvel ADR ou décision explicite.

---

## 67.1 Pas de bascule automatique

Un package privé ne devient jamais public uniquement parce qu’il est générique.

---

# 68. Environnements non fiables

Les contributions externes ou forks ne doivent pas accéder automatiquement :

- au feed privé ;
- aux packages confidentiels ;
- aux tokens ;
- aux artefacts de release.

---

## 68.1 Validation

Une contribution nécessitant les packages privés peut être exécutée après approbation dans un environnement contrôlé.

---

## 68.2 Secret de lecture

Même un token en lecture peut exposer les noms, versions ou contenus internes.

Il doit être protégé.

---

# 69. Package restore en CI

Le restore doit :

- utiliser `NuGet.config` du repository ;
- utiliser le lock file ;
- utiliser les credentials injectés ;
- échouer en cas de package absent ;
- ne pas afficher le token ;
- ne pas ajouter une source de fallback.

---

## 69.1 Source ajoutée par script

Un script ne doit pas ajouter silencieusement une source globale persistante sur le runner.

---

## 69.2 Runner éphémère

Les credentials doivent disparaître avec le job.

---

# 70. Package restore en développement

Le développeur doit pouvoir :

- authentifier son poste ;
- vérifier les sources ;
- restaurer le projet ;
- supprimer ses credentials ;
- changer de compte.

---

## 70.1 Documentation

Le README doit expliquer :

- la source ;
- la méthode d’authentification ;
- les permissions nécessaires ;
- le diagnostic ;
- la révocation.

Il ne contient aucun token d’exemple réaliste.

---

# 71. Source de package approuvée

Chaque source doit posséder un inventaire :

```text
SourceId

Provider

Purpose

Owner

Authentication

AllowedPackagePatterns

ReadPermissions

WritePermissions

Retention

BackupStrategy
```

---

## 71.1 Source inconnue

Une source inconnue dans `NuGet.config`, un script ou un workflow constitue un finding bloquant jusqu’à validation.

---

# 72. Audit des publications

Les publications doivent permettre d’identifier :

- Package ID ;
- version ;
- auteur technique ;
- workflow ;
- approbateur ;
- commit ;
- date ;
- checksum ;
- résultat du smoke test.

---

## 72.1 Journalisation

L’audit ne contient aucun token.

---

# 73. Observabilité

Événements recommandés :

```text
PackageRestoreStarted

PackageRestoreCompleted

PackageRestoreFailed

PackageSourceMappingViolation

PrivatePackageMissing

PackageBuildCompleted

PackageInspectionCompleted

PackagePublicationStarted

PackagePublished

PackagePublicationFailed

PublishedPackageSmokeTestCompleted

PackageDeprecated

PackageWithdrawn
```

---

## 73.1 Propriétés autorisées

```text
PackageId

PackageVersion

SourceId

Repository

CommitSha

WorkflowRunId

Result

ErrorCode

Duration
```

---

## 73.2 Données interdites

```text
Token

Credential

FeedPassword

Secret

PackageContent

ClientData
```

---

# 74. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-SUPPLY-001` | Source NuGet inconnue |
| `SF-SUPPLY-002` | Package Source Mapping absent |
| `SF-SUPPLY-003` | Package interne mappé vers une source publique |
| `SF-SUPPLY-004` | Package interne absent du feed privé |
| `SF-SUPPLY-005` | Version flottante interdite |
| `SF-SUPPLY-006` | Lock file absent |
| `SF-SUPPLY-007` | Restore locked échoué |
| `SF-SUPPLY-008` | Credential présent dans le repository |
| `SF-SUPPLY-009` | Permission de feed excessive |
| `SF-SUPPLY-010` | Package provenant d’une source non approuvée |
| `SF-SUPPLY-011` | Dépendance vulnérable bloquante |
| `SF-SUPPLY-012` | Licence inconnue ou incompatible |
| `SF-SUPPLY-013` | Package contenant un fichier interdit |
| `SF-SUPPLY-014` | Métadonnées de package invalides |
| `SF-SUPPLY-015` | Version déjà publiée |
| `SF-SUPPLY-016` | Artefact différent après validation |
| `SF-SUPPLY-017` | Publication partielle |
| `SF-SUPPLY-018` | Checksum absent ou invalide |
| `SF-SUPPLY-019` | Provenance insuffisante |
| `SF-SUPPLY-020` | Package retiré encore utilisé |
| `SF-SUPPLY-021` | Action CI non épinglée |
| `SF-SUPPLY-022` | Workflow de publication non protégé |
| `SF-SUPPLY-023` | Token de publication exposé |
| `SF-SUPPLY-024` | Smoke test post-publication échoué |
| `SF-SUPPLY-025` | Package ID hors namespace autorisé |
| `SF-SUPPLY-026` | Divergence entre lock file SiteFactory et NuGet |
| `SF-SUPPLY-027` | Package preview sélectionné implicitement |
| `SF-SUPPLY-028` | Source locale interdite dans une release |
| `SF-SUPPLY-029` | Package de contrat dépendant d’Infrastructure |
| `SF-SUPPLY-030` | Cache utilisé comme source non vérifiée |

---

# 75. Tests de Package Source Mapping

Tester :

```text
Package interne présent sur le feed privé
→ restore réussi

Package interne absent du feed privé
+ package homonyme public présent
→ restore échoué

Package public approuvé
→ restore depuis nuget.org

Source inconnue
→ validation échouée

Package interne mappé sur plusieurs sources
→ validation échouée
```

---

# 76. Test de dependency confusion

Un test de sécurité doit utiliser un environnement isolé avec :

- un faux package interne ;
- une source publique ou de test contenant une version supérieure ;
- le feed privé ne contenant pas la version demandée.

Résultat attendu :

```text
restore failure
```

Le package de la source non autorisée ne doit jamais être téléchargé comme remplacement.

---

# 77. Tests de lock files

Tester :

- restore verrouillé réussi ;
- modification directe de version sans lock file mis à jour ;
- modification transitive ;
- package absent ;
- checksum ou contenu incohérent selon les capacités de l’outil ;
- Windows ;
- Linux.

---

# 78. Tests de package

Pour chaque package publié :

```text
pack réussi

Package ID correct

Version correcte

RepositoryCommit correct

Dépendances correctes

Contenu attendu

Contenu interdit absent

Installation depuis feed temporaire

Build d’un consommateur

Tests d’un consommateur
```

---

# 79. Tests des packages de contrats

Vérifier :

- absence d’EF Core ;
- absence d’Infrastructure ;
- absence de secrets ;
- sérialisation ;
- compatibilité ;
- surface publique attendue ;
- dépendances minimales.

---

# 80. Tests du CLI publié

Installer le CLI depuis le feed privé dans un environnement propre.

Vérifier :

```text
sitefactory --version

validation de configuration

plan de génération

génération d’un projet de référence

restore du projet

build Release

tests
```

---

# 81. Tests de publication

Tester :

- tag valide ;
- tag incohérent ;
- version existante ;
- token absent ;
- permission insuffisante ;
- artefact manquant ;
- package invalide ;
- scan échoué ;
- publication partielle ;
- smoke test échoué.

---

# 82. Tests de contenu sensible

Créer des fichiers de test ressemblant à :

- token ;
- `.env` ;
- clé privée ;
- chaîne de connexion.

Le pack ou le scan doit les refuser.

---

# 83. Tests de provenance

Vérifier que le package ou le release manifest permet de retrouver :

- commit ;
- tag ;
- repository ;
- workflow ;
- checksum ;
- version.

---

# 84. Tests d’immutabilité

Une tentative de publier un contenu différent sous une version existante doit échouer.

---

# 85. Tests de migration de feed

Un exercice doit vérifier :

```text
export du package
→ checksum
→ import dans un feed temporaire
→ restore locked
→ build
→ tests
```

---

# 86. Quality gates

Une release de package est bloquée lorsque :

```text
Source non approuvée

Mapping absent ou invalide

Version flottante

Lock file incohérent

Build échoué

Test échoué

Scan de secret échoué

Vulnérabilité bloquante

Licence non validée

Package invalide

Fichier interdit

Métadonnées absentes

Version déjà publiée

Checksum absent

Provenance absente

Workflow non protégé

Action critique non épinglée

Smoke test impossible
```

---

## 86.1 Projet client

Le build d’un projet client est bloqué si :

```text
Package interne restauré depuis une source inattendue

Version différente du lock file

Feed privé non accessible

Preview non autorisée

Package SiteFactory inconnu

Divergence avec sitefactory.lock.json
```

---

# 87. Conséquences positives

La décision apporte :

- confidentialité des composants internes ;
- protection contre la dependency confusion ;
- versions reproductibles ;
- publication contrôlée ;
- traçabilité source-package ;
- séparation lecture/écriture ;
- packages immuables ;
- upgrades maîtrisés ;
- inventaire des versions ;
- analyse des vulnérabilités ;
- contrôle des licences ;
- possibilité de migration vers un autre feed ;
- meilleure continuité client.

---

# 88. Conséquences négatives

La décision entraîne :

- gestion d’un feed privé ;
- authentification des développeurs et CI ;
- configuration NuGet plus complexe ;
- dépendance à un fournisseur ;
- gestion des tokens ;
- procédures de transfert ;
- lock files supplémentaires ;
- temps de publication plus long ;
- scans et inspections supplémentaires ;
- nécessité de conserver les anciennes versions ;
- coût éventuel du stockage.

---

# 89. Risques acceptés

## 89.1 Indisponibilité du feed

Un build peut être bloqué même si le code est correct.

Réduction :

- export des packages ;
- runbook ;
- fournisseur alternatif ;
- artefacts de release conservés.

---

## 89.2 Token de lecture nécessaire

Les projets clients ont besoin d’un accès au feed.

Réduction :

- permissions minimales ;
- tokens séparés ;
- transfert planifié.

---

## 89.3 Lock-in fournisseur

Le feed peut utiliser des mécanismes d’authentification propres.

Réduction :

- Package IDs standards ;
- packages NuGet portables ;
- export ;
- checksums ;
- migration de feed testée.

---

## 89.4 Charge de maintenance

Les règles de mapping, lock et publication ajoutent du travail.

Cette charge est acceptée afin de réduire le risque de supply chain.

---

# 90. Mesures compensatoires

- namespace réservé ;
- Package Source Mapping ;
- `NuGet.config` versionné ;
- versions explicites ;
- `packages.lock.json` ;
- restore verrouillé ;
- feed privé ;
- permissions minimales ;
- environnement de release protégé ;
- actions épinglées ;
- scan de secrets ;
- scan de dépendances ;
- contrôle des licences ;
- inspection des packages ;
- checksums ;
- provenance ;
- smoke test post-publication ;
- inventaire des projets ;
- export de continuité.

---

# 91. Sécurité

## 91.1 Actifs

- code interne ;
- packages ;
- tokens ;
- projets clients ;
- contrats ;
- versions ;
- chaîne de build ;
- feed ;
- artefacts de release.

---

## 91.2 Frontières de confiance

- repository ;
- pull request ;
- runner CI ;
- action GitHub ;
- feed privé ;
- nuget.org ;
- poste développeur ;
- repository client.

---

## 91.3 Refus par défaut

Une dépendance dont :

- la source ;
- la version ;
- la licence ;
- la provenance ;

ne peut pas être déterminée est refusée.

---

# 92. Performance

## 92.1 Restore

Le mapping et les lock files ne doivent pas être supprimés pour gagner un temps marginal de restore.

---

## 92.2 Cache

Le cache peut réduire le temps sans modifier les garanties.

---

## 92.3 Packages

Les packages doivent éviter d’embarquer des fichiers volumineux inutiles.

---

## 92.4 Mesure

Les temps suivants peuvent être suivis :

- restore ;
- pack ;
- scan ;
- publication ;
- smoke test.

---

# 93. Coûts

## 93.1 Coûts supplémentaires

- abonnement ou stockage du feed ;
- temps de CI ;
- maintenance ;
- credentials ;
- audits ;
- export de continuité ;
- formation.

---

## 93.2 Coûts évités

- compromission par dependency confusion ;
- publication accidentelle publique ;
- packages non reproductibles ;
- corrections non traçables ;
- perte d’accès aux versions ;
- maintenance par copie manuelle ;
- contamination de plusieurs projets clients.

---

# 94. Maintenabilité

La décision permet de répondre clairement :

```text
Quel package ?

Quelle version ?

Quelle source ?

Quel commit ?

Quelle release ?

Quels projets le consomment ?

Quelle vulnérabilité l’affecte ?

Comment le remplacer ?
```

---

## 94.1 Inventaire

La maintenance dépend d’un inventaire fiable des versions clientes.

---

## 94.2 Nombre de packages

Le découpage des packages doit rester justifié.

Un nouveau package ne doit pas être créé uniquement pour chaque classe ou détail interne.

---

# 95. Compatibilité

## 95.1 NuGet

Les packages restent compatibles avec les outils NuGet standard.

---

## 95.2 Feed

Un changement de fournisseur ne modifie pas les Package IDs.

---

## 95.3 Projets clients

Les repositories clients conservent les versions exactes nécessaires.

---

## 95.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, cette décision constituant la baseline initiale.

La migration d’un système de références projet ou de code copié vers des packages nécessitera un plan explicite.

---

# 96. Migration depuis des références de projets internes

Si un prototype client référence directement les projets du repository SiteFactory :

1. identifier les références ;
2. créer les packages appropriés ;
3. définir les versions ;
4. publier sur un feed de test ;
5. remplacer les références projet ;
6. générer les lock files ;
7. exécuter build et tests ;
8. publier les versions stables ;
9. mettre à jour `sitefactory.lock.json`.

---

## 96.1 Code copié

Le code copié doit être comparé au package source afin d’identifier les modifications client avant remplacement.

---

## 96.2 Production

```text
Production execution: out of scope
```

Cet ADR définit la distribution des artefacts et ne déclenche aucune opération sur un projet de production.

---

# 97. Réversibilité

## 97.1 Classification

```text
élevée entre feeds privés NuGet

moyenne vers du code directement inclus

faible après publication publique accidentelle
```

---

## 97.2 Changement de feed

Les packages peuvent être déplacés sans changer leurs IDs ou versions.

---

## 97.3 Retour au code source copié

Possible techniquement, mais contraire aux objectifs de versionnement et de maintenance.

Un nouvel ADR serait nécessaire.

---

# 98. Plan d’implémentation

Le plan historique ci-dessous ne constitue pas une autorisation de packaging.
Depuis l'amendement du 2026-11-09, sa réservation et ses étapes de feed privé
concernent les IDs internes non admis au public ; le préfixe de l'étape 1 ne
classe pas, à lui seul, un package comme privé.

## Étape 1 — Namespace

Définir et valider :

```text
LogicGate.SiteFactory.*
```

---

## Étape 2 — Feed privé

Créer :

- source ;
- permissions ;
- compte de publication ;
- accès de lecture ;
- politique de rétention.

---

## Étape 3 — `NuGet.config`

Ajouter :

- sources ;
- `clear` ;
- Package Source Mapping ;
- aucun credential.

---

## Étape 4 — Central Package Management

Créer ou finaliser :

```text
Directory.Packages.props
```

---

## Étape 5 — Lock files

Activer les lock files et le restore verrouillé dans la CI.

---

## Étape 6 — Métadonnées de packages

Ajouter les propriétés de package et de repository.

---

## Étape 7 — Workflow de pack

Créer :

- build ;
- tests ;
- pack ;
- inspection ;
- scans ;
- checksums ;
- manifest.

---

## Étape 8 — Workflow de publication

Ajouter :

- tag ;
- environnement protégé ;
- approbation ;
- publication ;
- smoke test.

---

## Étape 9 — Supply chain checks

Ajouter :

- source validation ;
- dependency confusion test ;
- licence ;
- vulnérabilité ;
- secrets ;
- action pinning.

---

## Étape 10 — Continuité

Préparer :

- export ;
- inventaire ;
- migration de feed ;
- transfert client.

---

# 99. Tâches liées proposées

```text
SF-TASK-072 — Define internal NuGet package naming conventions

SF-TASK-073 — Configure the private NuGet feed

SF-TASK-074 — Add repository NuGet.config and package source mapping

SF-TASK-075 — Enable central package management

SF-TASK-076 — Enable NuGet lock files and locked restore

SF-TASK-077 — Add package metadata and repository provenance

SF-TASK-078 — Add package content inspection

SF-TASK-079 — Add the protected package release workflow

SF-TASK-080 — Add dependency confusion security tests

SF-TASK-081 — Add dependency vulnerability and license checks

SF-TASK-082 — Add published package smoke tests

SF-TASK-083 — Add package feed continuity export
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 100. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Un package interne est restauré uniquement depuis le feed privé.

Un package homonyme public n’est jamais utilisé comme fallback.

Un package interne absent provoque un échec.

Une source inconnue est refusée.

Les versions flottantes sont refusées.

Le restore verrouillé réussit avec les lock files.

Une divergence de lock file provoque un échec.

Les pull requests ne disposent pas du token de publication.

Le workflow stable utilise un environnement protégé.

Les actions sensibles sont épinglées à des commits.

Un package est construit depuis un commit identifié.

Le package contient les métadonnées requises.

Le package ne contient aucun secret ou fichier interdit.

Une version existante ne peut pas être remplacée.

Les checksums correspondent aux artefacts publiés.

Le package publié peut être restauré dans un environnement propre.

Le CLI publié peut générer un projet de référence.

Les dépendances transitives sont scannées.

Les licences sont inventoriées.

Les versions utilisées par les clients supportés restent disponibles.

Un export peut être restauré sur un feed temporaire.
```

---

# 101. Conditions de révision

Réexaminer cette décision si :

- SiteFactory devient entièrement open source ;
- tous les packages sont destinés à une publication publique ;
- le fournisseur de feed ne répond plus aux exigences ;
- la gestion des tokens devient trop complexe ;
- une infrastructure d’attestation standardisée est adoptée ;
- la signature des packages devient obligatoire ;
- un client doit opérer sans aucun accès LogicGate ;
- le nombre de feeds spécifiques client augmente fortement ;
- une marketplace de modules est créée ;
- des contributeurs externes doivent exécuter toute la CI sans approbation ;
- la disponibilité du feed devient insuffisante ;
- un autre format de distribution remplace NuGet.

---

# 102. Indicateurs à suivre

- nombre de packages internes ;
- nombre de versions publiées ;
- projets consommant chaque version ;
- restores échoués ;
- erreurs d’authentification ;
- violations de source mapping ;
- vulnérabilités par sévérité ;
- dépendances obsolètes ;
- licences non reconnues ;
- durée du workflow de release ;
- publications partielles ;
- smoke tests échoués ;
- versions retirées ;
- espace de stockage du feed ;
- temps de migration de feed ;
- actions CI non à jour.

---

# 103. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des packages internes sont restaurés depuis nuget.org

les builds dépendent d’un cache non reproductible

des tokens sont ajoutés aux repositories

des packages sont publiés localement

des versions existantes sont remplacées

des actions CI utilisent des références mutables non maîtrisées

des vulnérabilités critiques atteignent les releases

des packages contiennent des fichiers inattendus

des clients ne peuvent plus reconstruire leur projet

des versions supportées sont supprimées

des sources NuGet supplémentaires apparaissent sans validation
```

---

# 104. Conséquences observées

Cette section est append-only après mise en œuvre.

## 104.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède la mise en place du feed privé,
du Package Source Mapping et du workflow de publication.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- package ;
- source ;
- workflow ;
- incident ou bénéfice ;
- action proposée.

---

# 105. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](./0014-explicit-client-upgrades-and-version-pinning.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [15-cli-specification.md](../03-generation/15-cli-specification.md)
- [18-security-baseline.md](../04-quality-security/18-security-baseline.md)
- [19-testing-strategy.md](../04-quality-security/19-testing-strategy.md)
- [20-quality-gates-and-ci.md](../04-quality-security/20-quality-gates-and-ci.md)
- [22-deployment-and-environments.md](../05-operations/22-deployment-and-environments.md)
- [25-upgrade-and-maintenance-strategy.md](../05-operations/25-upgrade-and-maintenance-strategy.md)
- [26-release-and-versioning.md](../05-operations/26-release-and-versioning.md)
- [27-client-handover-and-service-closure.md](../05-operations/27-client-handover-and-service-closure.md)
- [28-codex-operating-guide.md](../06-codex/28-codex-operating-guide.md)
- [0000-adr-template.md](./0000-adr-template.md)
- [0001-one-repository-and-database-per-client.md](./0001-one-repository-and-database-per-client.md)
- [0005-file-generation-strategies.md](./0005-file-generation-strategies.md)
- [0007-module-composition-and-public-contracts.md](./0007-module-composition-and-public-contracts.md)
- [0033-split-public-sitefactory-and-private-logicgate-modules.md](./0033-split-public-sitefactory-and-private-logicgate-modules.md)
- [0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md](./0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md)
- [0035-partage-public-component-via-nuget.md](./0035-partage-public-component-via-nuget.md)

---

# 106. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 101 |

---

# 107. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |
| 2026-11-09 | Amendement humain de portée avec ADR-0035 : ModuleSdk public admis exactement, autres admissions explicites par ID, autres packages internes privés sans fallback ; date/statut d'acceptation initiaux conservés |

---

# 108. Synthèse de la décision

**Synthèse initiale conservée, à lire avec l'amendement du 2026-11-09 :**
« packages internes » désigne les IDs non admis au public ; le préfixe commun
ne réserve plus ModuleSdk au privé. L'admission publique exacte ne signifie
pas qu'une publication a déjà eu lieu.

```text
Packages internes
→ namespace LogicGate.SiteFactory.*
→ feed privé LogicGate
→ versions immuables

Packages publics
→ nuget.org
→ dépendances approuvées

Restore
→ NuGet.config du repository
→ Package Source Mapping
→ versions explicites
→ packages.lock.json
→ locked mode

Dependency confusion
→ package interne mappé uniquement vers le feed privé
→ absence sur le feed privé = échec
→ aucun fallback public

Publication
→ tag et commit exacts
→ workflow protégé
→ build et tests
→ scans
→ pack
→ inspection
→ checksums
→ provenance
→ publication
→ smoke test du package publié

Credentials
→ lecture et écriture séparées
→ permissions minimales
→ aucun token dans Git
→ aucun token de publication dans les pull requests

Continuité
→ versions supportées conservées
→ export vérifié
→ migration de feed possible
→ transfert client préparé
```

> Un package SiteFactory ne doit pas être considéré comme fiable uniquement parce que son nom et sa version semblent corrects. Sa source, son contenu, son commit d’origine et son chemin de publication doivent également être connus et contrôlés.
