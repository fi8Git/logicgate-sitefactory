# LogicGate SiteFactory — Modèle de spécification des tâches Codex

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Modèle de spécification des tâches Codex |
| Version | 1.0 |
| Statut | Guide public ; exemples pédagogiques non exécutés |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, contributeurs, Codex |
| Type de document | Standard de rédaction et template réutilisable |
| Emplacement recommandé des tâches | `docs/06-codex/tasks/` |
| Document parent principal | `28-codex-operating-guide.md` |
| Documents associés | `AGENTS.md`, documentation produit, architecture, sécurité, tests et opérations |
| Dernière mise à jour | 2026-10-21 |

---

## Utilisation dans le public et statut des exemples

Ce modèle complète [AGENTS.md](../../AGENTS.md) et
[le guide opérationnel](28-codex-operating-guide.md), sans les remplacer.
[README](../../README.md) et [CONTRIBUTING](../../CONTRIBUTING.md) définissent
les capacités et commandes réelles. Le périmètre, le hors-périmètre, les
décisions humaines, preuves reproductibles et conditions d'arrêt restent requis.

Les exemples de tâches, front matter, codes documentaires, résultats et tests
sont **pédagogiques et fictifs**, pas des tâches approuvées, des fichiers à créer
automatiquement ou des preuves acquises. Les emplacements recommandés sont
des conventions : inspecter les fichiers réels avant de les utiliser.
Le schéma de SF-TASK-062 et ses statuts ne sont pas remplacés par ces modèles.

En particulier, le scénario de slug à 48 caractères ci-dessous illustre une
spécification hypothétique, **pas la règle publique actuelle** :
le validateur structurel V1 impose 3–80 caractères ; le record Domain ClientSlug
ne centralise pas cette validation complète. Les sources réelles sont
[ClientSlug](../../src/LogicGate.SiteFactory.Domain/ClientSlug.cs) et
[ClientConfigurationV1Validator](../../src/LogicGate.SiteFactory.Application/Configuration/ClientConfigurationV1Validator.cs).
Un exemple n'autorise pas leur refactoring ni un changement de contrat.
La commande de validation actuelle est `validate --config <path>` ;
`repository validate-tasks` reste une proposition future.

Toute tâche publique doit fonctionner sans module/fixture/binaire Business.
Aucune dépendance cloud/IA obligatoire ou fallback implicite ; aucune opération
production, mutation privée, classification, licence ou publication déduite
de la tâche. La licence des sorties générées n'est pas décidée ici.
Les validations sont proportionnées et autorisées ; une tâche documentaire
n'exécute pas implicitement les suites applicatives ou PostgreSQL.
Aucune CI distante ni release n'est annoncée par les exemples.

---

## 1. Objet du document

Ce document définit le format standard des tâches confiées à Codex dans le repository LogicGate SiteFactory.

Il précise notamment :

- la structure obligatoire d’une tâche ;
- les informations minimales à fournir ;
- les informations propres aux tâches complexes ;
- les conventions d’identification ;
- les statuts possibles ;
- la définition du périmètre ;
- la définition du hors périmètre ;
- la rédaction des critères d’acceptation ;
- les contraintes d’architecture ;
- les exigences de sécurité ;
- les tests attendus ;
- les commandes de validation ;
- les livrables ;
- le format du compte-rendu ;
- les conditions d’arrêt ;
- les décisions restant humaines ;
- les variantes pour l’implémentation, l’investigation, la documentation et la correction ;
- les critères permettant de déclarer une tâche prête à être transmise à Codex.

L’objectif est de transformer une intention humaine en une unité de travail :

- compréhensible ;
- limitée ;
- vérifiable ;
- testable ;
- réversible ;
- compatible avec une revue indépendante.

---

## 2. Vision

Une bonne tâche Codex ne doit pas seulement expliquer ce qu’il faut développer.

Elle doit préciser :

```text
Pourquoi le changement est nécessaire

Ce qui doit changer

Ce qui ne doit pas changer

Quelles règles doivent être respectées

Comment vérifier le résultat

Quelles décisions restent humaines
```

Le cycle de référence est :

```text
Besoin
→ décision
→ tâche structurée
→ validation du périmètre
→ implémentation Codex
→ preuves
→ revue
→ correction
→ acceptation humaine
```

Une tâche imprécise augmente le risque de :

- suppositions ;
- refonte excessive ;
- modification hors périmètre ;
- dette technique ;
- régression ;
- mauvaise décision d’architecture ;
- test insuffisant ;
- déclaration de succès non fondée.

---

## 3. Principes fondamentaux

## 3.1 Une tâche possède un objectif principal

Une tâche doit répondre à une intention principale.

Exemple :

```text
Ajouter la validation du slug client.
```

Elle ne doit pas combiner arbitrairement :

```text
validation du slug
+ nouvelle interface admin
+ changement de base
+ refonte CI
+ nouveau module
```

---

## 3.2 Le périmètre est explicite

La tâche doit définir les éléments concernés.

Exemples :

- projets ;
- couches ;
- modules ;
- fichiers ;
- commandes ;
- contrats ;
- données ;
- documentation.

---

## 3.3 Le hors périmètre est obligatoire

La tâche doit préciser les sujets proches qui ne doivent pas être traités.

Le hors périmètre protège contre :

- l’élargissement silencieux ;
- la sur-ingénierie ;
- les refactorings opportunistes ;
- les décisions non demandées.

---

## 3.4 Les critères d’acceptation sont observables

Un critère d’acceptation doit pouvoir être vérifié.

Exemple valide :

```text
La valeur `../client` est refusée avec le code
`SF-CONFIG-012`.
```

Exemple insuffisant :

```text
La validation doit être robuste.
```

---

## 3.5 Les contraintes importantes sont répétées dans la tâche

Même lorsqu’une règle existe dans la documentation générale, une contrainte critique doit être rappelée si elle est directement liée à la tâche.

Exemple :

```text
Ne jamais écrire hors du dossier de génération.
```

---

## 3.6 Les tests font partie de la tâche

Les tests ne doivent pas être ajoutés après l’implémentation comme une amélioration facultative.

La tâche doit indiquer :

- types de tests ;
- scénarios ;
- technologies réelles requises ;
- commandes attendues.

---

## 3.7 Les décisions humaines sont isolées

Une tâche ne doit pas demander à Codex de choisir implicitement :

- un prix ;
- une architecture structurante ;
- un fournisseur ;
- une version majeure ;
- un comportement commercial ;
- une politique de sécurité ;
- une suppression de données.

Les décisions déjà validées sont écrites dans la tâche.

Les décisions restantes sont explicitement listées.

---

## 3.8 Une tâche terminée produit des preuves

Les preuves peuvent inclure :

- build ;
- tests ;
- scans ;
- rapport ;
- diff ;
- projet généré ;
- capture ciblée ;
- commande reproduisant le comportement.

---

# 4. Non-objectifs

Le modèle de tâche ne doit pas :

- remplacer la documentation produit ;
- recopier tous les documents du repository ;
- devenir un cahier des charges complet pour chaque correction mineure ;
- contenir des secrets ;
- contenir des données client réelles ;
- imposer une solution technique lorsque seule l’intention est validée ;
- permettre à Codex d’exécuter une opération de production ;
- transformer toutes les idées du backlog en une seule tâche ;
- supprimer la nécessité d’une revue ;
- servir de preuve qu’une implémentation a réellement été validée.

---

# 5. Terminologie

## 5.1 Task ID

Identifiant stable de la tâche.

Exemple :

```text
SF-TASK-012
```

---

## 5.2 Task type

Nature principale de l’intervention.

Valeurs initiales :

```text
implementation
investigation
documentation
refactoring
security
migration-preparation
operations-preparation
```

Les tâches de revue et de correction utilisent des templates dédiés.

---

## 5.3 Owner

Personne responsable de la décision et de l’acceptation.

---

## 5.4 Implementer

Agent ou développeur chargé de l’implémentation.

---

## 5.5 Reviewer

Personne ou agent distinct chargé de la revue.

---

## 5.6 Acceptance criterion

Résultat observable permettant de vérifier que la tâche est satisfaite.

---

## 5.7 Constraint

Règle que l’implémentation doit respecter.

---

## 5.8 Assumption

Hypothèse explicite utilisée pour cadrer la tâche.

---

## 5.9 Decision

Choix déjà validé qui ne doit pas être rediscuté pendant l’implémentation.

---

## 5.10 Open question

Question non résolue empêchant ou limitant l’implémentation.

---

## 5.11 Deliverable

Artefact attendu à la fin de la tâche.

---

# 6. Emplacement des tâches

Structure recommandée :

```text
docs/
└── 06-codex/
    ├── tasks/
    │   ├── backlog/
    │   ├── active/
    │   ├── review/
    │   ├── completed/
    │   └── cancelled/
    └── templates/
```

Une structure plus simple peut être utilisée pendant le bootstrap :

```text
docs/06-codex/tasks/
```

avec le statut conservé dans le front matter.

---

# 7. Convention de nommage

Format recommandé :

```text
{task-id}-{short-kebab-title}.md
```

Exemples :

```text
SF-TASK-001-bootstrap-solution.md

SF-TASK-012-validate-client-slug.md

SF-TASK-024-generate-lock-file.md
```

---

## 7.1 Titre court

Le titre doit décrire une action ou un résultat.

Exemples recommandés :

```text
Validate client configuration schema

Generate the SiteFactory lock file

Add PostgreSQL module manifest validation
```

Exemples à éviter :

```text
Various fixes

Improve project

Technical changes
```

---

# 8. Identifiants

## 8.1 Format

```text
SF-TASK-{NUMBER}
```

Exemple :

```text
SF-TASK-029
```

---

## 8.2 Unicité

Un identifiant n’est jamais réutilisé, même après :

- annulation ;
- fusion ;
- remplacement ;
- clôture.

---

## 8.3 Sous-tâches

Lorsqu’une tâche est décomposée :

```text
SF-TASK-029-A
SF-TASK-029-B
SF-TASK-029-C
```

ou de nouveaux identifiants indépendants peuvent être créés.

La stratégie choisie doit rester cohérente dans le repository.

---

# 9. Statuts

Valeurs recommandées :

```text
draft
ready
in-progress
blocked
in-review
changes-requested
completed
cancelled
superseded
```

---

## 9.1 `draft`

La tâche est en cours de rédaction.

Elle ne doit pas être transmise à Codex pour implémentation complète.

---

## 9.2 `ready`

Le contexte et les décisions nécessaires sont disponibles.

---

## 9.3 `in-progress`

L’implémentation est en cours.

---

## 9.4 `blocked`

Une décision, une dépendance ou un accès manque.

---

## 9.5 `in-review`

L’implémentation est terminée et en cours de revue.

---

## 9.6 `changes-requested`

Des corrections ont été demandées.

---

## 9.7 `completed`

Les critères d’acceptation sont validés humainement.

Une simple déclaration de Codex ne suffit pas à positionner ce statut.

---

## 9.8 `cancelled`

La tâche n’est plus nécessaire.

---

## 9.9 `superseded`

La tâche est remplacée par une ou plusieurs autres tâches.

---

# 10. Priorités

Valeurs possibles :

```text
critical
high
normal
low
```

---

## 10.1 `critical`

- vulnérabilité exploitable ;
- perte de données ;
- production bloquée ;
- défaut bloquant une release.

---

## 10.2 `high`

- capacité nécessaire au MVP ;
- régression majeure ;
- risque important ;
- dépendance bloquante.

---

## 10.3 `normal`

Priorité standard du backlog.

---

## 10.4 `low`

Amélioration non urgente ou optimisation.

---

# 11. Niveau de risque

Valeurs recommandées :

```text
low
medium
high
critical
```

---

## 11.1 Risque faible

Exemples :

- documentation ;
- message ;
- test supplémentaire ;
- correction locale sans données.

---

## 11.2 Risque moyen

Exemples :

- logique applicative ;
- configuration ;
- CLI ;
- génération de fichiers.

---

## 11.3 Risque élevé

Exemples :

- Identity ;
- autorisation ;
- migrations ;
- sécurité ;
- fichiers protégés ;
- Docker ;
- infrastructure.

---

## 11.4 Risque critique

Exemples :

- suppression de données ;
- restauration ;
- production ;
- changement majeur de chiffrement ;
- transfert de secrets.

Une tâche critique ne peut généralement pas être exécutée entièrement par Codex.

---

# 12. Taille estimée

Classification recommandée :

```text
XS
S
M
L
XL
```

---

## 12.1 `XS`

- modification locale ;
- peu de fichiers ;
- aucun changement de contrat ;
- tests simples.

---

## 12.2 `S`

- un composant ;
- tests unitaires ;
- documentation limitée.

---

## 12.3 `M`

- plusieurs couches ;
- tests d’intégration ;
- contrat ou configuration modifié.

---

## 12.4 `L`

- module ;
- migration ;
- plusieurs projets ;
- projet généré affecté.

Une tâche `L` doit être examinée pour décomposition.

---

## 12.5 `XL`

Une tâche `XL` doit être divisée avant implémentation.

---

# 13. Front matter recommandé

Chaque tâche peut commencer par un front matter YAML.

```yaml
---
taskId: SF-TASK-012
title: Validate client slug
status: ready
type: implementation
priority: high
risk: medium
size: S
owner: LogicGate
implementer: Codex
reviewer: unassigned
createdAt: 2026-07-28
targetVersion: 0.2.0
dependsOn: []
supersedes: []
---
```

---

## 13.1 Données interdites

Le front matter ne doit pas contenir :

- secret ;
- adresse personnelle ;
- mot de passe ;
- token ;
- donnée client confidentielle.

---

# 14. Sections obligatoires

Une tâche `ready` doit contenir au minimum :

```text
Résumé

Objectif

Contexte

Périmètre

Hors périmètre

Décisions déjà validées

Contraintes

Critères d’acceptation

Tests attendus

Commandes de validation

Livrables

Compte-rendu attendu

Conditions d’arrêt
```

---

# 15. Résumé

Le résumé doit présenter la tâche en quelques lignes.

Exemple :

```text
Ajouter un value object `ClientSlug` et l’utiliser pendant
la validation de `client.sitefactory.yml` afin de refuser
les slugs dangereux ou incompatibles avec les chemins générés.
```

Il ne doit pas contenir toute l’implémentation.

---

# 16. Objectif

L’objectif décrit le résultat attendu.

Il commence idéalement par un verbe d’action :

```text
Ajouter
Corriger
Valider
Extraire
Générer
Protéger
Documenter
Préparer
```

---

## 16.1 Exemple valide

```text
Permettre au moteur de refuser toute configuration dont le slug
client ne respecte pas le format kebab-case autorisé.
```

---

## 16.2 Exemple insuffisant

```text
Travailler sur les slugs.
```

---

# 17. Contexte

Le contexte explique :

- le problème actuel ;
- son origine ;
- son impact ;
- la place de la tâche dans le produit ;
- les éléments déjà implémentés.

---

## 17.1 Questions auxquelles répondre

```text
Pourquoi cette tâche existe-t-elle ?

Quel comportement existe actuellement ?

Quel risque ou manque doit être corrigé ?

Quels documents ou décisions encadrent le changement ?
```

---

## 17.2 Limite

Le contexte ne doit pas contenir une histoire inutile du projet.

Il doit aider Codex à prendre les bonnes décisions locales.

---

# 18. État actuel

Pour une modification d’un comportement existant, décrire ce qui est observé.

Exemple :

```text
Le champ `client.slug` est actuellement traité comme une chaîne
sans validation métier. Il peut donc contenir des séparateurs
de chemin ou des caractères incompatibles avec Docker Compose.
```

---

# 19. Comportement cible

Décrire le résultat fonctionnel attendu.

Exemple :

```text
Un slug valide utilise uniquement des lettres minuscules,
des chiffres et des tirets simples.

Les valeurs invalides produisent une erreur structurée
avant la création d’un plan de génération.
```

---

# 20. Périmètre

Le périmètre doit lister précisément les zones concernées.

Exemple :

```text
- Value object `ClientSlug`
- Validation du modèle de configuration
- Mapping YAML vers Domain
- Codes d’erreur associés
- Tests Domain
- Tests Application
- Documentation du schéma
```

---

## 20.1 Périmètre technique

Peut indiquer :

- projets ;
- namespaces ;
- modules ;
- endpoints ;
- commandes ;
- schémas ;
- rapports.

---

## 20.2 Périmètre fonctionnel

Peut indiquer :

- rôle ;
- parcours ;
- données ;
- résultat utilisateur.

---

# 21. Hors périmètre

Exemple :

```text
- Renommer les projets clients existants
- Migrer un ancien slug
- Modifier le nom de la solution générée
- Modifier Docker Compose
- Ajouter une commande CLI dédiée
```

---

## 21.1 Principe

Lorsqu’un élément proche semble pertinent mais n’est pas nécessaire pour atteindre l’objectif, il doit être inscrit dans le hors périmètre.

---

# 22. Préconditions

Décrire ce qui doit déjà être disponible.

Exemples :

```text
- Le bootstrap de la solution est terminé.
- Le modèle de configuration existe.
- Le chargeur YAML est déjà disponible.
- Les tests Domain sont opérationnels.
```

---

## 22.1 Précondition manquante

Si une précondition n’est pas satisfaite, la tâche peut :

- devenir `blocked` ;
- être décomposée ;
- créer une nouvelle tâche préalable.

---

# 23. Dépendances

Lister les tâches ou composants nécessaires.

Exemple :

```text
Depends on:
- SF-TASK-004 — Load client configuration
- SF-TASK-007 — Add structured validation errors
```

---

## 23.1 Dépendance technique externe

Une dépendance externe doit préciser :

- package ;
- version ;
- état d’approbation ;
- nécessité.

---

# 24. Décisions déjà validées

Cette section empêche Codex de rediscuter ou modifier des décisions confirmées.

Exemple :

```text
- Le format retenu est kebab-case.
- Le slug est limité à 48 caractères.
- Les majuscules sont refusées et non converties silencieusement.
- L’erreur doit apparaître avant la planification.
- Aucun package externe ne doit être ajouté.
```

---

## 24.1 Décision absente

Une décision structurante non validée doit apparaître dans :

```text
Questions ouvertes
```

et peut bloquer la tâche.

---

# 25. Hypothèses autorisées

Les hypothèses doivent être limitées, réversibles et explicites.

Exemple :

```text
- Les slugs existants de démonstration sont déjà conformes.
- Aucun mécanisme de migration de slug n’existe dans le MVP.
```

---

## 25.1 Interdiction

Une hypothèse ne doit pas remplacer une décision concernant :

- prix ;
- sécurité ;
- suppression ;
- données ;
- entitlement ;
- architecture majeure ;
- production.

---

# 26. Questions ouvertes

Une tâche `ready` ne devrait contenir aucune question bloquante.

Les questions non bloquantes peuvent rester visibles.

Exemple :

```text
Non bloquant :
le message d’erreur public sera-t-il traduit dans une version future ?
```

---

## 26.1 Question bloquante

Exemple :

```text
Bloquant :
faut-il normaliser automatiquement les majuscules ou refuser la valeur ?
```

La tâche ne peut pas passer à `ready` avant décision.

---

# 27. Contraintes générales

Les contraintes doivent être directement applicables.

Exemples :

```text
- Respecter les couches Domain et Application.
- Ne pas ajouter de dépendance NuGet.
- Propager `CancellationToken`.
- Ne pas modifier le schéma PostgreSQL.
- Ne pas modifier les fichiers générés à la main.
- Conserver la compatibilité Windows et Linux.
```

---

# 28. Contraintes d’architecture

Exemples :

```text
- Le value object appartient à Domain.
- Le parsing YAML reste dans Infrastructure.
- Application ne dépend pas du parser YAML.
- Le CLI ne contient aucune nouvelle règle métier.
```

---

# 29. Contraintes de sécurité

Exemples :

```text
- Refuser les séparateurs de chemin.
- Refuser `.` et `..`.
- Refuser les chemins absolus.
- Ne pas logguer la configuration complète.
- Ne pas inclure de secret dans les erreurs.
```

---

# 30. Contraintes de données

Exemples :

```text
- Aucune migration n’est requise.
- Les données existantes ne doivent pas être modifiées.
- PostgreSQL réel est requis pour les tests d’intégration.
- Les migrations de production restent hors périmètre.
```

---

# 31. Contraintes de compatibilité

Préciser les compatibilités à préserver :

```text
- .NET 10
- Windows x64
- Linux x64
- Schéma de configuration version 1
- Sortie JSON du CLI version 1
```

---

# 32. Contraintes de performance

Exemples :

```text
- La validation ne doit pas effectuer d’accès réseau.
- La validation doit rester synchrone et déterministe.
- Aucun parcours de table complet ne doit être ajouté.
- La pagination reste obligatoire.
```

---

# 33. Contraintes d’observabilité

Exemples :

```text
- Réutiliser les EventIds du domaine Configuration.
- Journaliser uniquement le code d’erreur et l’emplacement.
- Ne pas journaliser la configuration complète.
- Conserver le CorrelationId existant.
```

---

# 34. Fichiers ou zones probables

Cette section oriente Codex sans imposer une liste définitive.

Exemple :

```text
Probablement concernés :

- `src/LogicGate.SiteFactory.Domain/ClientSlug.cs`
- `src/LogicGate.SiteFactory.Application/Configuration/...`
- `tests/LogicGate.SiteFactory.Domain.Tests/...`
- `docs/03-generation/14-client-configuration-schema.md`
```

---

## 34.1 Formulation

Utiliser :

```text
Fichiers probables
```

plutôt que :

```text
Modifie uniquement exactement ces fichiers
```

sauf contrainte particulière.

---

# 35. Contrats affectés

Lister les contrats publics pouvant évoluer :

```text
- YAML `client.slug`
- Code d’erreur
- JSON de validation
- Value object public
- Schéma JSON
```

---

## 35.1 Breaking change

Préciser explicitement :

```text
Breaking change expected: no
```

ou :

```text
Breaking change expected: yes
```

---

# 36. Modèle de données et migrations

Pour une tâche touchant les données, préciser :

```text
Entities affected
Tables affected
Columns affected
Indexes affected
Foreign keys affected
Migration required
Existing data strategy
Rollback strategy
Backup requirement
```

---

## 36.1 Exemple

```text
Migration required: yes

The migration must:
- add a nullable column first;
- populate existing rows;
- add the final constraint in a later step.

Production execution is outside the task.
```

---

# 37. Permissions et autorisation

Pour une tâche sensible, préciser :

```text
Permission required
Roles affected
Anonymous access
Authenticated access
Entitlement
Feature flag
Kill switch
```

---

## 37.1 Exemple

```text
Permission:
Leads.Delete

Entitlement:
lead-management.delete

Anonymous:
forbidden

Administrator:
still subject to entitlement
```

---

# 38. Feature flags et entitlements

Une tâche ajoutant une capacité doit définir :

- module propriétaire ;
- entitlement ;
- feature flag ;
- valeur par défaut ;
- comportement si absent ;
- comportement si désactivé ;
- comportement du kill switch.

---

# 39. Routes et API

Préciser lorsque pertinent :

```text
HTTP method
Route
Authentication
Authorization
Antiforgery
Request model
Response model
Status codes
Rate limiting
```

---

## 39.1 Actions mutables

Toute action mutable doit préciser :

```text
POST, PUT, PATCH ou DELETE
```

et sa stratégie antiforgery ou d’authentification API.

---

# 40. Interface utilisateur

Une tâche UI doit préciser :

- page ;
- composant ;
- états ;
- responsive ;
- accessibilité ;
- chargement ;
- erreur ;
- confirmation ;
- permission.

---

## 40.1 États minimums

```text
loading
empty
success
validation-error
technical-error
disabled
unauthorized
```

---

## 40.2 Accessibilité

Préciser :

- label ;
- focus ;
- clavier ;
- nom accessible ;
- contraste ;
- erreurs ;
- sémantique.

---

# 41. CLI

Une tâche CLI doit préciser :

```text
Command
Options
Arguments
Required options
Examples
Exit codes
Human output
JSON output
Dry-run
Non-interactive behavior
Confirmation
```

---

## 41.1 Exemple

```text
Command:
sitefactory validate --config <path>

Exit codes:
0 success
1 unexpected technical error
2 invalid arguments
3 invalid configuration
4 invalid catalog
12 cancelled
15 unsupported schema version
```

---

# 42. Génération

Une tâche de génération doit préciser :

```text
Inputs
Plan changes
Files created
Files updated
File strategies
Lock file changes
Report changes
Determinism
Failure behavior
```

---

## 42.1 Stratégies

Pour chaque fichier :

```text
generated
create-once
merge
custom
protected
```

---

## 42.2 Reproductibilité

Préciser les éléments dynamiques autorisés et ceux devant rester identiques.

---

# 43. Modules

Une tâche de module doit préciser :

```text
Module ID
Version
Status
Dependencies
Permissions
Features
Entitlements
Routes
Entities
Migrations
Events
Operations
Tests
```

---

# 44. Infrastructure et Docker

Une tâche infrastructure doit préciser :

```text
Services affected
Ports
Networks
Volumes
Secrets
Health checks
User
Permissions
Backup impact
Rollback
```

---

## 44.1 Production

La tâche doit clairement indiquer :

```text
Production execution: out of scope
```

lorsqu’elle ne prépare que les artefacts.

---

# 45. CI/CD

Une tâche CI doit préciser :

```text
Workflow
Triggers
Permissions
Jobs
Platforms
Secrets
Artifacts
Quality gates
Timeout
Concurrency
```

---

## 45.1 Secret exposure

Préciser si le workflow s’exécute sur une pull request non fiable.

---

# 46. Documentation

Une tâche documentaire doit préciser :

- documents à créer ;
- documents à mettre à jour ;
- source de vérité ;
- décisions validées ;
- exemples ;
- liens ;
- absence d’implémentation code.

---

# 47. Critères d’acceptation

Les critères doivent être :

- numérotés ;
- observables ;
- indépendants autant que possible ;
- écrits avant implémentation ;
- suffisamment précis pour la revue.

---

## 47.1 Format recommandé

```text
AC-01
AC-02
AC-03
```

---

## 47.2 Exemple

```text
AC-01 — `sample-project` est accepté.

AC-02 — `Sample-Project` est refusé.

AC-03 — `../sample-project` est refusé.

AC-04 — l’erreur utilise le code `SF-CONFIG-012`.

AC-05 — aucune écriture fichier n’est effectuée
lorsque la configuration est invalide.
```

---

## 47.3 Critère technique

Exemple :

```text
AC-06 — Domain ne référence aucun package YAML.
```

---

## 47.4 Critère de preuve

Exemple :

```text
AC-07 — `dotnet test` réussit sans test ignoré ajouté.
```

---

# 48. Mauvais critères d’acceptation

Exemples à éviter :

```text
Le code est propre.

La fonctionnalité fonctionne bien.

La sécurité est correcte.

Les tests sont suffisants.

L’UX est moderne.
```

Ces formulations doivent être remplacées par des résultats observables.

---

# 49. Tests attendus

La tâche doit définir une matrice de tests.

Exemple :

| Niveau | Scénario | Résultat |
|---|---|---|
| Unit | `sample-project` | Accepté |
| Unit | `Sample-Project` | Refusé |
| Unit | `../client` | Refusé |
| Integration | YAML valide | Configuration chargée |
| Integration | YAML invalide | Plan non créé |
| Architecture | Domain | Aucune dépendance YAML |

---

## 49.1 Tests de régression

Lors d’une correction :

```text
Le test doit reproduire le défaut avant la correction.
```

---

## 49.2 Cas nominal et négatif

Une tâche ne doit pas définir uniquement le cas de succès.

---

## 49.3 Annulation

Une opération longue doit inclure un scénario `CancellationToken`.

---

## 49.4 Sécurité

Une tâche exposée doit inclure des tests de contournement.

---

# 50. Données de test

La tâche doit utiliser des données fictives.

Exemples :

```text
Example Documentation
sample-project
contact@example.test
<TEST_SECRET>
```

Les données réelles sont interdites.

---

# 51. Commandes de validation

Les commandes doivent être directement exécutables lorsque possible.

Baseline :

```powershell
dotnet restore SiteFactory.slnx --locked-mode
```

```powershell
dotnet build SiteFactory.slnx  --configuration Release  --no-restore
```

```powershell
dotnet test SiteFactory.slnx  --configuration Release  --no-build --no-restore --filter "Category!=PostgreSql"
```

---

## 51.1 Commandes ciblées

Une tâche peut ajouter :

```powershell
dotnet test  tests/LogicGate.SiteFactory.Domain.Tests  --configuration Release
```

---

## 51.2 Projet généré

Pour une tâche affectant la génération :

```text
Generate reference project
→ restore
→ build Release
→ tests
```

---

## 51.3 Scans

Préciser les scans requis :

```text
Secret scan
Dependency scan
Static analysis
Container scan
```

---

# 52. Livrables

Exemples :

```text
- Code source
- Tests
- Migration préparée
- Schéma mis à jour
- Documentation
- Rapport
- Golden master
- Script
- Exemple de configuration
```

---

## 52.1 Absence de livrable implicite

Si une migration, une documentation ou un rapport est attendu, il doit être explicitement listé.

---

# 53. Documentation à mettre à jour

Exemple :

```text
- `14-client-configuration-schema.md`
- exemple YAML minimal
- `CHANGELOG.md` sous `Unreleased`
```

---

## 53.1 Aucune mise à jour requise

Lorsque réellement applicable :

```text
Documentation update: none
Reason: internal refactoring without behavior or contract change.
```

---

# 54. Compte-rendu attendu

La tâche doit préciser le format de réponse de Codex.

Baseline :

```text
Résumé
Fichiers modifiés
Décisions d’implémentation
Tests exécutés
Résultats
Build
Scans
Documentation
Éléments non exécutés
Risques restants
Décisions humaines restantes
```

---

## 54.1 Valeurs exactes

Codex doit indiquer :

- nombre de tests ;
- échecs ;
- warnings ;
- commandes réellement exécutées.

Il ne doit pas inventer ces informations.

---

# 55. Conditions d’arrêt

Cette section indique quand Codex doit suspendre la tâche.

Exemples :

```text
Arrêter si :
- un nouveau package structurant semble nécessaire ;
- le format de slug validé ne peut pas être respecté ;
- un changement de schéma incompatible est découvert ;
- une migration de données existantes devient nécessaire ;
- un accès production est requis ;
- une décision commerciale est nécessaire.
```

---

# 56. Décisions restant humaines

Exemple :

```text
- Validation du message d’erreur final.
- Validation de la longueur maximale du slug.
- Décision de migration des anciens projets.
```

Une tâche `ready` ne doit pas contenir de décision humaine bloquant son objectif principal.

---

# 57. Risques

La tâche doit identifier les principaux risques.

Exemple :

```text
- Collision avec les slugs utilisés dans Docker Compose.
- Différence de comportement entre Windows et Linux.
- Rejet de configurations de démonstration existantes.
```

---

## 57.1 Mesures de réduction

Exemple :

```text
- Tests multiplateformes.
- Tests des configurations de samples.
- Validation avant toute écriture.
```

---

# 58. Compatibilité et rollback

Préciser :

```text
Backward compatible: yes/no

Data migration required: yes/no

Rollback:
- revert the branch;
- no data rollback required.
```

---

# 59. Définition de Done

Une tâche peut inclure une définition de Done propre au périmètre.

Exemple :

```text
- Tous les AC sont satisfaits.
- Build Release à 0 warning et 0 erreur.
- Tests réussis.
- Aucun secret détecté.
- Documentation mise à jour.
- Revue prête.
```

---

# 60. Template complet

Le template suivant constitue la baseline prête à copier.

```markdown
---
taskId: SF-TASK-XXX
title: Short task title
status: draft
type: implementation
priority: normal
risk: medium
size: S
owner: LogicGate
implementer: Codex
reviewer: unassigned
createdAt: YYYY-MM-DD
targetVersion: null
dependsOn: []
supersedes: []
---

# SF-TASK-XXX — Short task title

## 1. Résumé

Décrire la tâche en quelques lignes.

## 2. Objectif

Décrire le résultat principal attendu.

## 3. Contexte

Expliquer le problème, l’état actuel et son impact.

## 4. État actuel

Décrire le comportement observé.

## 5. Comportement cible

Décrire précisément le comportement attendu.

## 6. Périmètre

- Élément concerné
- Projet concerné
- Test concerné
- Documentation concernée

## 7. Hors périmètre

- Élément explicitement exclu
- Évolution future non demandée
- Opération de production

## 8. Préconditions

- Précondition 1
- Précondition 2

## 9. Dépendances

- Task ou composant requis

## 10. Décisions déjà validées

- Décision 1
- Décision 2

## 11. Hypothèses autorisées

- Hypothèse limitée et réversible

## 12. Questions ouvertes

Aucune question bloquante.

## 13. Contraintes d’architecture

- Respecter les couches existantes.
- Ne pas déplacer la logique métier dans le CLI.
- Ne pas introduire de dépendance circulaire.

## 14. Contraintes de sécurité

- Aucune donnée sensible dans les logs.
- Aucun secret dans le repository.
- Refus par défaut en cas d’entrée invalide.

## 15. Contraintes de données

- Migration requise : non
- Données existantes modifiées : non
- Exécution production : hors périmètre

## 16. Contraintes de compatibilité

- .NET 10
- Windows x64
- Linux x64
- Schéma version 1

## 17. Observabilité

- EventIds concernés
- Propriétés autorisées
- Données interdites

## 18. Fichiers ou zones probables

- `src/...`
- `tests/...`
- `docs/...`

## 19. Contrats affectés

- Contrat concerné
- Breaking change attendu : non

## 20. Critères d’acceptation

### AC-01

Critère observable.

### AC-02

Critère observable.

### AC-03

Critère observable.

## 21. Tests attendus

### Tests unitaires

- Scénario
- Résultat attendu

### Tests d’intégration

- Scénario
- Résultat attendu

### Tests de sécurité

- Scénario
- Résultat attendu

### Tests de régression

- Défaut reproduit
- Résultat attendu

## 22. Commandes de validation

```powershell
dotnet restore SiteFactory.slnx --locked-mode
```

```powershell
dotnet build SiteFactory.slnx  --configuration Release  --no-restore
```

```powershell
dotnet test SiteFactory.slnx  --configuration Release  --no-build --no-restore --filter "Category!=PostgreSql"
```

## 23. Livrables

- Code
- Tests
- Documentation
- Rapport éventuel

## 24. Documentation à mettre à jour

- Document
- Exemple
- Changelog éventuel

## 25. Risques

- Risque
- Mesure de réduction

## 26. Compatibilité et rollback

- Backward compatible :
- Migration :
- Rollback :

## 27. Conditions d’arrêt

Arrêter et demander une décision si :

- condition 1 ;
- condition 2.

## 28. Compte-rendu attendu

Le compte-rendu doit inclure :

- résumé ;
- fichiers modifiés ;
- tests ;
- build ;
- scans ;
- documentation ;
- limites ;
- décisions restantes.

## 29. Définition de Done

- [ ] Tous les critères d’acceptation sont satisfaits.
- [ ] Le périmètre est respecté.
- [ ] Le build Release réussit.
- [ ] Les tests réussissent.
- [ ] Aucun secret n’est présent.
- [ ] La documentation est à jour.
- [ ] Le compte-rendu est produit.
```

---

# 61. Template simplifié

Pour une tâche à faible risque et taille `XS`, un format simplifié peut être utilisé.

```markdown
---
taskId: SF-TASK-XXX
title: Short title
status: ready
type: implementation
priority: normal
risk: low
size: XS
owner: LogicGate
---

# SF-TASK-XXX — Short title

## Objectif

## Périmètre

## Hors périmètre

## Contraintes

## Critères d’acceptation

- AC-01
- AC-02

## Tests

## Commandes de validation

## Compte-rendu attendu
```

---

## 61.1 Limite

Le template simplifié ne doit pas être utilisé pour :

- sécurité ;
- données ;
- migration ;
- Identity ;
- infrastructure ;
- CI sensible ;
- module complet ;
- breaking change.

---

# 62. Profil de tâche — Investigation

Une tâche d’investigation ne demande pas nécessairement une correction.

Sections spécifiques :

```text
Symptômes
Étapes de reproduction
Hypothèses
Informations disponibles
Informations à collecter
Expériences autorisées
Environnements interdits
Résultat attendu
```

---

## 62.1 Résultat d’investigation

Le livrable doit distinguer :

- cause confirmée ;
- cause probable ;
- hypothèse écartée ;
- preuve ;
- recommandation ;
- prochaine tâche.

---

## 62.2 Template

```markdown
# SF-TASK-XXX — Investigate ...

## Symptômes

## Reproduction

## Périmètre d’analyse

## Hors périmètre

## Hypothèses initiales

## Informations à collecter

## Expériences autorisées

## Environnements interdits

## Résultat attendu

- Cause ou hypothèses classées
- Preuves
- Recommandation
- Proposition de tâche corrective

## Conditions d’arrêt
```

---

# 63. Profil de tâche — Documentation

Sections spécifiques :

```text
Documents sources
Décisions validées
Terminologie
Structure attendue
Niveau de détail
Public cible
Exemples
Éléments hors périmètre
```

---

## 63.1 Interdiction

Une tâche documentaire ne doit pas modifier le code sauf demande explicite.

---

# 64. Profil de tâche — Refactoring

Une tâche de refactoring doit préciser :

```text
Comportement à préserver
Contrats à préserver
Motivation
Mesure de succès
Tests existants
Tests à ajouter
Éléments pouvant changer
Éléments ne pouvant pas changer
```

---

## 64.1 Critère principal

```text
No intended functional behavior change.
```

Tout changement fonctionnel découvert doit être signalé.

---

# 65. Profil de tâche — Sécurité

Une tâche sécurité doit préciser :

```text
Threat
Attack scenario
Affected assets
Current controls
Expected controls
Bypass tests
Logging constraints
Incident impact
Backport requirement
```

---

## 65.1 Classification

Préciser :

```text
Severity
Exploitability
Exposure
Projects affected
```

---

## 65.2 Contenu sensible

Les détails facilitant une exploitation réelle peuvent être limités dans les documents publics.

---

# 66. Profil de tâche — Préparation de migration

La tâche doit préciser :

```text
Source schema
Target schema
Existing data
Migration steps
Destructive operations
Backup
Rollback
Staging
Production execution out of scope
```

---

## 66.1 Livrable

Codex peut préparer :

- migration ;
- tests ;
- script ;
- plan ;
- documentation.

Codex ne l’applique pas en production.

---

# 67. Profil de tâche — Module

Une tâche module devrait être décomposée.

Exemple :

```text
A. Manifest et contrats
B. Domain et Application
C. Persistance et migration
D. API et autorisation
E. UI
F. Tests et documentation
```

---

## 67.1 Tâche unique interdite

Éviter :

```text
Créer entièrement le module Leads.
```

sans découpage.

---

# 68. Exemple complet

```markdown
---
taskId: SF-TASK-012
title: Validate client slug
status: ready
type: implementation
priority: high
risk: medium
size: S
owner: LogicGate
implementer: Codex
reviewer: unassigned
createdAt: 2026-07-28
targetVersion: 0.2.0
dependsOn:
  - SF-TASK-004
supersedes: []
---

# SF-TASK-012 — Validate client slug

## 1. Résumé

Ajouter un value object `ClientSlug` et l’utiliser pendant
la validation de la configuration client afin de refuser les valeurs
incompatibles avec les noms de dossiers, de projets Docker Compose
et de ressources générées.

## 2. Objectif

Garantir qu’un projet ne peut pas atteindre la planification
avec un slug invalide ou dangereux.

## 3. Contexte

Le champ `client.slug` est actuellement représenté par une chaîne.
Aucune règle métier centralisée ne vérifie les séparateurs de chemin,
les majuscules ou les longueurs excessives.

Cette valeur est destinée à être réutilisée dans plusieurs noms
de ressources générées.

## 4. État actuel

Les valeurs suivantes peuvent actuellement atteindre Application :

```text
Sample Project
../sample-project
sample_project
```

## 5. Comportement cible

Les slugs valides respectent :

```text
^[a-z0-9]+(?:-[a-z0-9]+)*$
```

Longueur :

```text
3 à 48 caractères
```

La valeur est refusée avant toute planification ou écriture.

## 6. Périmètre

- Value object `ClientSlug`
- Validation Domain
- Mapping depuis la configuration
- Erreur structurée
- Tests Domain
- Test Application
- Documentation du schéma

## 7. Hors périmètre

- Migration des anciens projets
- Conversion automatique des majuscules
- Renommage de dossiers existants
- Modification de Docker Compose
- Nouvelle commande CLI

## 8. Préconditions

- Le chargeur de configuration est disponible.
- Le modèle applicatif de configuration existe.
- Les erreurs structurées sont disponibles.

## 9. Dépendances

- `SF-TASK-004 — Load client configuration`

## 10. Décisions déjà validées

- Utiliser kebab-case.
- Refuser les majuscules.
- Ne pas normaliser silencieusement.
- Limiter la longueur à 48 caractères.
- Ne pas ajouter de package externe.

## 11. Hypothèses autorisées

- Les samples actuels peuvent être corrigés dans la tâche.
- Aucun projet de production ne dépend encore d’un slug invalide.

## 12. Questions ouvertes

Aucune question bloquante.

## 13. Contraintes d’architecture

- `ClientSlug` appartient à Domain.
- Le parser YAML reste dans Infrastructure.
- Le CLI ne contient pas la regex.
- Application consomme le value object.

## 14. Contraintes de sécurité

- Refuser `.` et `..`.
- Refuser `/` et `\`.
- Refuser les caractères de contrôle.
- Ne pas logguer la configuration complète.

## 15. Contraintes de données

- Migration requise : non.
- Modification de données existantes : non.
- Production : hors périmètre.

## 16. Compatibilité

- .NET 10
- Windows x64
- Linux x64
- Configuration schema version 1

## 17. Observabilité

En cas d’échec, utiliser :

```text
SF-CONFIG-012
```

Propriétés autorisées :

```text
FieldPath
ErrorCode
```

Ne pas logguer la valeur complète du slug si elle contient
des caractères de contrôle.

## 18. Fichiers probables

- `src/LogicGate.SiteFactory.Domain/ClientSlug.cs`
- `src/LogicGate.SiteFactory.Application/Configuration/...`
- `tests/LogicGate.SiteFactory.Domain.Tests/IdentifierTests.cs`
- `tests/LogicGate.SiteFactory.Application.Tests/Configuration/...`
- `docs/03-generation/14-client-configuration-schema.md`

## 19. Contrats affectés

- `client.slug`
- erreur `SF-CONFIG-012`

Breaking change expected:

```text
No for valid configurations.
Invalid configurations will now be rejected.
```

## 20. Critères d’acceptation

### AC-01

`sample-project` est accepté.

### AC-02

`sample-123` est accepté.

### AC-03

`Sample-Project` est refusé.

### AC-04

`../sample-project` est refusé.

### AC-05

`sample_project` est refusé.

### AC-06

Une valeur de plus de 48 caractères est refusée.

### AC-07

L’erreur utilise le code `SF-CONFIG-012`.

### AC-08

Aucun plan n’est produit après l’échec.

### AC-09

Aucune écriture fichier n’est effectuée après l’échec.

### AC-10

Domain ne référence aucun package YAML.

## 21. Tests attendus

### Tests unitaires

- valeur minimale valide ;
- valeur maximale valide ;
- majuscules ;
- tiret initial ;
- tiret final ;
- double tiret ;
- underscore ;
- slash ;
- backslash ;
- point ;
- Unicode ;
- caractère de contrôle.

### Tests Application

- configuration valide ;
- configuration invalide ;
- planificateur non appelé en cas d’échec.

### Tests d’architecture

- Domain ne référence pas Infrastructure.

## 22. Commandes de validation

```powershell
dotnet restore SiteFactory.slnx --locked-mode
```

```powershell
dotnet build SiteFactory.slnx  --configuration Release  --no-restore
```

```powershell
dotnet test SiteFactory.slnx  --configuration Release  --no-build --no-restore --filter "Category!=PostgreSql"
```

## 23. Livrables

- `ClientSlug`
- validation
- erreurs
- tests
- documentation

## 24. Documentation à mettre à jour

- `14-client-configuration-schema.md`
- samples invalides éventuels
- `CHANGELOG.md` si la release est préparée

## 25. Risques

- Rejet d’un sample historique invalide.
- Différence de traitement des chemins Windows/Linux.

Mesures :

- tests multiplateformes ;
- validation indépendante des APIs de chemin.

## 26. Compatibilité et rollback

Backward compatible:

```text
Yes for valid configurations.
```

Migration:

```text
No.
```

Rollback:

```text
Revert the task commit.
```

## 27. Conditions d’arrêt

Arrêter si :

- un projet existant utilise un slug invalide ;
- une migration devient nécessaire ;
- Docker Compose impose une contrainte supplémentaire ;
- la regex validée doit être modifiée.

## 28. Compte-rendu attendu

Le compte-rendu doit préciser :

- fichiers modifiés ;
- règles implémentées ;
- tests ajoutés ;
- résultats ;
- samples modifiés ;
- éventuelles configurations historiques invalides.

## 29. Définition de Done

- [ ] Tous les AC sont satisfaits.
- [ ] Build Release réussi.
- [ ] Tests réussis.
- [ ] Aucun warning ajouté.
- [ ] Documentation mise à jour.
- [ ] Aucun secret ou donnée réelle.
```

---

# 69. Critères de préparation d’une tâche

Une tâche peut passer de `draft` à `ready` lorsque :

```text
[ ] Objectif principal défini
[ ] Contexte suffisant
[ ] État actuel décrit
[ ] Comportement cible décrit
[ ] Périmètre explicite
[ ] Hors périmètre explicite
[ ] Décisions structurantes validées
[ ] Aucune question bloquante
[ ] Contraintes définies
[ ] Critères d’acceptation observables
[ ] Tests définis
[ ] Commandes définies
[ ] Conditions d’arrêt définies
[ ] Compte-rendu défini
```

---

# 70. Critères de découpage

Une tâche doit être découpée si elle contient plusieurs des éléments suivants :

```text
Plusieurs modules indépendants

Plusieurs parcours utilisateurs

Migration importante

Changement d’architecture

Nouvelle UI complète

Nouvelle infrastructure

Nouvelle CI

Nouveau provider externe

Plus de deux objectifs principaux

Critères d’acceptation impossibles à revoir ensemble
```

---

# 71. Décomposition recommandée

Exemple de grande fonctionnalité :

```text
Créer le système de modules
```

Découpage :

```text
SF-TASK-030 — Define module manifest contract

SF-TASK-031 — Validate module manifests

SF-TASK-032 — Resolve module dependencies

SF-TASK-033 — Register module services

SF-TASK-034 — Generate module composition

SF-TASK-035 — Test module integration
```

---

# 72. Gestion des changements de périmètre

Lorsqu’une nouvelle exigence apparaît pendant l’implémentation :

1. identifier le changement ;
2. expliquer son impact ;
3. vérifier s’il est indispensable ;
4. mettre à jour la tâche ;
5. créer une nouvelle tâche si nécessaire ;
6. ne pas l’implémenter silencieusement.

---

# 73. Gestion des découvertes hors périmètre

Codex doit consigner :

```text
Découverte
Impact
Sévérité
Emplacement
Recommandation
```

La découverte ne doit pas automatiquement être corrigée.

---

# 74. Liens vers la documentation

La tâche doit référencer uniquement les documents utiles.

Exemple :

```text
Required reading:
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
```

---

## 74.1 Pas de copie massive

La tâche ne doit pas recopier intégralement les documents parents.

---

# 75. Liens vers des décisions

Une tâche affectée par un ADR doit le référencer.

Exemple :

```text
ADR:
- `ADR-0004-client-isolation.md`
```

---

# 76. Lien avec le backlog

La tâche peut référencer :

- epic ;
- milestone ;
- sprint ;
- issue GitHub ;
- version cible.

Exemple :

```yaml
milestone: MVP-0.2
epic: generation-foundations
githubIssue: null
```

---

# 77. Versionnement des tâches

## 77.1 Modifications avant démarrage

La tâche peut être modifiée librement tant qu’elle reste `draft`.

---

## 77.2 Après démarrage

Toute modification substantielle doit être visible dans Git.

---

## 77.3 Après achèvement

Une tâche `completed` ne doit pas être réécrite pour correspondre rétroactivement à l’implémentation.

Une correction produit :

- une nouvelle tâche ;
- ou un addendum clairement daté.

---

# 78. Traçabilité

Une tâche terminée doit pouvoir être reliée à :

```text
Branch
Pull request
Commit
Review
Correction
Target version
Changelog entry
```

---

# 79. Règles pour LogicGate

LogicGate doit :

- valider les décisions ;
- limiter le périmètre ;
- fournir les contraintes métier ;
- distinguer correction et évolution ;
- accepter ou refuser le résultat ;
- éviter les tâches trop générales ;
- préserver la documentation comme source de vérité.

---

# 80. Règles pour Codex

Codex doit :

- lire la tâche complète ;
- lire les documents requis ;
- inspecter le repository ;
- confirmer le périmètre ;
- signaler les blocages ;
- respecter le hors périmètre ;
- implémenter les critères ;
- ajouter les tests ;
- exécuter les validations ;
- produire le compte-rendu demandé.

---

## 80.1 Codex ne doit pas

```text
Réinterpréter une décision validée

Modifier un prix

Modifier un entitlement

Ajouter une dépendance structurante sans validation

Exécuter une production

Élargir silencieusement la tâche

Déclarer une tâche completed

Inventer un résultat de test

Masquer un quality gate
```

---

# 81. Anti-patterns interdits

## 81.1 Tâche sans objectif

```text
Améliorer SiteFactory.
```

---

## 81.2 Tâche sans hors périmètre

Codex traite tous les problèmes voisins.

---

## 81.3 Critères subjectifs

```text
Le code doit être élégant.
```

---

## 81.4 Solution imposée sans justification

```text
Ajouter tel package
```

alors qu’aucune décision de dépendance n’a été prise.

---

## 81.5 Production incluse

```text
Implémenter, migrer et déployer en production.
```

---

## 81.6 Plusieurs fonctionnalités indépendantes

Une seule tâche couvre tout un sprint.

---

## 81.7 Tests facultatifs

```text
Ajouter des tests si nécessaire.
```

---

## 81.8 Décision bloquante cachée

La tâche ne précise pas qu’une nouvelle permission doit être choisie.

---

## 81.9 Donnée réelle

Une tâche contient un vrai email, mot de passe, dump ou message client.

---

## 81.10 Tâche écrite après l’implémentation

La spécification est modifiée afin de présenter le résultat existant comme conforme.

---

## 81.11 Compte-rendu non demandé

Codex livre des changements sans indiquer les commandes exécutées.

---

# 82. Quality gates de la tâche

Une tâche ne doit pas passer à `ready` si :

```text
Objectif ambigu

Périmètre absent

Hors périmètre absent

Décision structurante manquante

Critère non observable

Test non défini

Secret présent

Donnée réelle présente

Production requise

Migration destructive non validée

Dépendance structurante non approuvée
```

---

# 83. Codes documentaires recommandés

| Code | Signification |
|---|---|
| `SF-TASK-VALIDATION-001` | Objectif absent |
| `SF-TASK-VALIDATION-002` | Périmètre absent |
| `SF-TASK-VALIDATION-003` | Hors périmètre absent |
| `SF-TASK-VALIDATION-004` | Critère non observable |
| `SF-TASK-VALIDATION-005` | Test attendu absent |
| `SF-TASK-VALIDATION-006` | Décision bloquante absente |
| `SF-TASK-VALIDATION-007` | Secret potentiel détecté |
| `SF-TASK-VALIDATION-008` | Donnée personnelle détectée |
| `SF-TASK-VALIDATION-009` | Tâche trop large |
| `SF-TASK-VALIDATION-010` | Opération de production incluse |
| `SF-TASK-VALIDATION-011` | Migration destructive non validée |
| `SF-TASK-VALIDATION-012` | Dépendance non approuvée |
| `SF-TASK-VALIDATION-013` | Commande de validation absente |
| `SF-TASK-VALIDATION-014` | Condition d’arrêt absente |
| `SF-TASK-VALIDATION-015` | Compte-rendu non défini |

---

# 84. Automatisation future

Une commande future pourrait valider les tâches :

```bash
sitefactory repository validate-tasks
```

Elle pourrait vérifier :

- front matter ;
- identifiant ;
- statut ;
- sections obligatoires ;
- critères ;
- commandes ;
- secrets potentiels ;
- placeholders ;
- liens ;
- doublons.

---

## 84.1 Limite

La validation automatique ne peut pas confirmer :

- la pertinence métier ;
- la qualité du découpage ;
- la justesse d’une décision ;
- le niveau de risque réel.

---

# 85. Critères d’acceptation du modèle

Le modèle sera considéré comme opérationnel lorsque :

1. les tâches possèdent un identifiant stable ;
2. les statuts sont définis ;
3. les priorités sont définies ;
4. les risques sont définis ;
5. les tailles sont définies ;
6. un front matter standard existe ;
7. les sections obligatoires sont définies ;
8. le périmètre est obligatoire ;
9. le hors périmètre est obligatoire ;
10. les décisions validées sont explicites ;
11. les questions bloquantes empêchent le statut `ready` ;
12. les critères d’acceptation sont observables ;
13. les tests sont définis avant implémentation ;
14. les commandes de validation sont indiquées ;
15. les conditions d’arrêt sont définies ;
16. le compte-rendu attendu est défini ;
17. un template complet existe ;
18. un template simplifié existe ;
19. des profils spécialisés existent ;
20. une tâche complète d’exemple existe ;
21. les tâches trop larges doivent être découpées ;
22. les données réelles sont interdites ;
23. les opérations de production sont exclues ;
24. les quality gates d’une tâche sont définis ;
25. une automatisation future peut valider la structure.

---

# 86. Décisions validées

La baseline de spécification des tâches Codex est la suivante :

```text
Chaque tâche possède un identifiant stable.

Les tâches sont stockées dans docs/06-codex/tasks.

Une tâche possède un statut explicite.

Une tâche possède un objectif principal.

Le périmètre est obligatoire.

Le hors périmètre est obligatoire.

Les décisions structurantes sont prises avant l’implémentation.

Les décisions déjà validées sont consignées dans la tâche.

Une question bloquante empêche le statut ready.

Les hypothèses restent limitées, réversibles et explicites.

Les critères d’acceptation sont numérotés et observables.

Les tests attendus sont écrits avant l’implémentation.

Les scénarios négatifs font partie des tests.

Les commandes de validation sont explicitement listées.

Les livrables sont explicitement listés.

La documentation à mettre à jour est identifiée.

Les conditions d’arrêt sont obligatoires.

Le format du compte-rendu est défini dans la tâche.

Une tâche XL doit être décomposée.

Les tâches de sécurité, données, Identity et infrastructure
utilisent le template complet.

Les tâches d’investigation ne modifient pas nécessairement le code.

Les tâches de refactoring définissent les comportements
et contrats à préserver.

Les tâches de migration n’incluent jamais
l’exécution en production.

Les tâches ne contiennent ni secret réel,
ni donnée client réelle.

Codex ne peut pas déclarer seul une tâche completed.

Une tâche terminée reste reliée à une branche,
une pull request, des commits et une revue.

La spécification ne doit pas être réécrite rétroactivement
pour masquer un écart d’implémentation.
```

---

# 87. Checklist de rédaction

```text
[ ] Identifiant attribué
[ ] Titre actionnable
[ ] Statut défini
[ ] Type défini
[ ] Priorité définie
[ ] Risque défini
[ ] Taille définie
[ ] Objectif principal
[ ] Contexte
[ ] État actuel
[ ] Comportement cible
[ ] Périmètre
[ ] Hors périmètre
[ ] Préconditions
[ ] Dépendances
[ ] Décisions validées
[ ] Aucune question bloquante
[ ] Contraintes
[ ] Critères observables
[ ] Tests
[ ] Commandes
[ ] Livrables
[ ] Documentation
[ ] Risques
[ ] Conditions d’arrêt
[ ] Compte-rendu
```

---

# 88. Checklist avant passage à `ready`

```text
[ ] Le besoin est compris.
[ ] L’objectif tient en une phrase.
[ ] La tâche ne contient qu’un objectif principal.
[ ] Le périmètre est suffisamment précis.
[ ] Le hors périmètre protège les sujets voisins.
[ ] Les décisions structurantes sont prises.
[ ] Les critères peuvent être revus indépendamment.
[ ] Les tests prouvent les critères essentiels.
[ ] Les opérations sensibles restent humaines.
[ ] Aucun secret ou donnée réelle n’est présent.
[ ] Codex peut commencer sans nouvelle décision bloquante.
```

---

# 89. Checklist de découpage

```text
[ ] Plusieurs modules ?
[ ] Plusieurs migrations ?
[ ] Plusieurs interfaces utilisateur ?
[ ] Plusieurs contrats publics ?
[ ] Infrastructure et métier mélangés ?
[ ] Plus de deux objectifs principaux ?
[ ] Revue difficile en une seule pull request ?
[ ] Rollback différent selon les parties ?
```

Si plusieurs réponses sont positives, la tâche doit probablement être décomposée.

---

# 90. Checklist de remise à Codex

```text
[ ] Tâche en statut ready
[ ] AGENTS.md disponible
[ ] Documents requis listés
[ ] Repository dans un état connu
[ ] Branche prévue
[ ] Tests existants disponibles
[ ] Environnement local disponible
[ ] Aucune opération production demandée
[ ] Compte-rendu attendu défini
```

---

# 91. Conclusion

Une tâche Codex de qualité doit rendre l’intention humaine difficile à mal interpréter.

Elle repose sur :

```text
objectif
+ contexte
+ périmètre
+ hors périmètre
+ décisions
+ contraintes
+ critères observables
+ tests
+ preuves
+ conditions d’arrêt
```

Le template ne cherche pas à prédire chaque ligne de code.

Il doit fournir suffisamment de cadre pour que Codex puisse :

- inspecter le repository ;
- proposer un plan limité ;
- implémenter sans inventer des décisions ;
- prouver le résultat ;
- préparer une revue fiable.

> Une tâche bien rédigée ne dit pas seulement à Codex quoi construire. Elle définit aussi les frontières qu’il ne doit pas franchir et les preuves nécessaires pour démontrer que le résultat est acceptable.
