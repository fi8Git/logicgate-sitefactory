# LogicGate SiteFactory — Modèle de revue Codex

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Modèle de revue Codex |
| Version | 1.0 |
| Statut | Guide public ; exemples pédagogiques non exécutés |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, contributeurs, reviewers, Codex |
| Type de document | Standard de revue et template réutilisable |
| Emplacement recommandé des revues | `docs/06-codex/reviews/` |
| Documents parents | `28-codex-operating-guide.md`, `29-codex-task-specification-template.md`, `AGENTS.md` |
| Documents associés | Documentation produit, architecture, sécurité, tests, opérations et tâche examinée |
| Dernière mise à jour | 2026-10-21 |

---

## Revue du périmètre public

Ce modèle applique [AGENTS.md](../../AGENTS.md), le
[guide opérationnel](28-codex-operating-guide.md) et le
[workflow public](../../CONTRIBUTING.md). Il ne donne aucun droit d'implémenter
une correction pendant une revue, de modifier le périmètre, les critères,
les classifications, allowlist/SHA, licences ou ressources privées.

Les revues comparent le contrat accepté, les vraies sources et le diff.
Les exemples de findings, symboles, tests et comptes rendus chiffrés ci-dessous
sont **fictifs**, pas des résultats actuels ou des défauts démontrés du public.
Leur scénario de slug à 48 caractères n'est pas la règle V1 actuelle (3–80).
Aucun package, pipeline de release ou validation CI distante n'est acquis
par leur simple mention.

Préserver les preuves rouges et distinguer non exécuté, échec, risque,
hypothèse et défaut démontré. Une campagne verte n'explique pas un échec ancien.
P03-F1 reste OPEN / UNKNOWN, cause non démontrée, risque accepté avec réserve,
749 HTTP 200 / 19 HTTP 503 historiques ; réexamen **2026-10-23** sans
prolongation tacite. Seule une décision humaine peut changer son traitement.

La frontière publique est vérifiée sans accès Business : contrats et références
fermées, composition statique, secrets et diagnostics expurgés, pas de migration
ou réparation au startup, pas d'effet production implicite.
Les obligations de sécurité et les critères de revue ci-dessous sont conservés.

---

## 1. Objet du document

Ce document définit le format standard des revues réalisées avec Codex dans le repository LogicGate SiteFactory.

Il précise notamment :

- le rôle du reviewer ;
- les préconditions d’une revue ;
- les informations à fournir au reviewer ;
- l’ordre de vérification ;
- les niveaux de sévérité ;
- le format des findings ;
- les preuves attendues ;
- la différence entre défaut, risque, suggestion et question ;
- la vérification des critères d’acceptation ;
- la vérification du périmètre ;
- la vérification de l’architecture ;
- la vérification de la sécurité ;
- la vérification des données et migrations ;
- la vérification des tests ;
- la vérification de la génération ;
- la vérification de la CI/CD ;
- la vérification de la documentation ;
- la vérification des opérations ;
- les commandes réellement exécutées ;
- les résultats non vérifiables ;
- la recommandation finale ;
- la gestion des corrections ;
- la seconde revue ;
- les contraintes applicables à Codex.

L’objectif est de produire une revue :

- structurée ;
- argumentée ;
- vérifiable ;
- proportionnée ;
- indépendante de l’implémentation ;
- exploitable par une tâche de correction.

---

## 2. Vision

Une revue ne doit pas seulement déterminer si le code compile.

Elle doit vérifier que l’implémentation :

```text
répond à la tâche
+ respecte le périmètre
+ préserve les contrats
+ protège les données
+ respecte l’architecture
+ possède des tests pertinents
+ ne crée pas de régression connue
+ reste maintenable
```

Le flux de référence est :

```text
Tâche validée
→ implémentation
→ compte-rendu d’implémentation
→ revue indépendante
→ findings
→ décision humaine
→ corrections acceptées
→ seconde revue
→ merge éventuel
```

Une revue ne doit pas fonctionner selon le modèle suivant :

```text
Build vert
→ note élevée
→ approbation automatique
```

---

## 3. Principes fondamentaux

## 3.1 La revue vérifie une implémentation par rapport à un contrat

Les principales sources de comparaison sont :

```text
Tâche
Critères d’acceptation
Décisions validées
Documentation propriétaire
ADR
Contrats publics
Tests
Code modifié
```

Le reviewer ne doit pas redéfinir silencieusement le besoin.

---

## 3.2 La revue est distincte de l’implémentation

Dans le mode revue, Codex doit principalement :

- inspecter ;
- comparer ;
- exécuter des validations ;
- rechercher des risques ;
- produire des findings.

Il ne doit pas modifier directement le code, sauf demande explicite.

---

## 3.3 Les findings sont prouvés

Un finding doit être accompagné d’au moins une preuve exploitable :

- emplacement ;
- scénario ;
- commande ;
- résultat ;
- contradiction documentaire ;
- test absent ou incorrect ;
- comportement reproductible.

Une impression générale ne constitue pas une preuve suffisante.

---

## 3.4 Les sévérités reflètent l’impact

La sévérité ne doit pas être utilisée pour :

- dramatiser une préférence ;
- obtenir une refactorisation ;
- imposer un style personnel ;
- augmenter artificiellement la valeur de la revue.

Elle représente l’impact réel du problème.

---

## 3.5 Les suggestions ne sont pas des défauts

Une amélioration facultative doit être identifiée comme :

```text
Optionnelle
```

Elle ne doit pas empêcher une validation lorsque tous les contrats obligatoires sont respectés.

---

## 3.6 Aucun succès sans exécution réelle

Le reviewer doit distinguer :

```text
Vérifié
Non vérifié
Non applicable
Impossible à vérifier
```

Il ne doit pas considérer un test comme réussi uniquement parce que l’implémenteur l’a déclaré.

---

## 3.7 La revue respecte le périmètre

Le reviewer peut signaler un problème hors périmètre important.

Il doit toutefois préciser qu’il est :

```text
préexistant
hors périmètre
ou
introduit par la modification
```

---

## 3.8 La décision finale reste humaine

Codex peut recommander :

- refus ;
- corrections ;
- acceptation avec réserves ;
- prêt à fusionner.

La fusion, la release et le déploiement restent des décisions humaines.

---

# 4. Non-objectifs

Le modèle de revue ne doit pas :

- remplacer la tâche ;
- remplacer les tests ;
- remplacer un audit juridique ;
- remplacer un pentest ;
- remplacer une recette client ;
- modifier la production ;
- imposer une préférence non documentée ;
- réécrire toute l’implémentation ;
- garantir l’absence totale de défaut ;
- considérer un score comme une preuve suffisante ;
- créer automatiquement une pull request de correction ;
- fusionner automatiquement le code.

---

# 5. Terminologie

## 5.1 Review ID

Identifiant stable de la revue.

Exemple :

```text
SF-REVIEW-012
```

---

## 5.2 Finding

Problème ou risque identifié pendant la revue.

---

## 5.3 Evidence

Élément vérifiable soutenant le finding.

---

## 5.4 Severity

Niveau d’impact du finding.

Valeurs :

```text
blocking
major
minor
optional
```

---

## 5.5 Blocking finding

Défaut empêchant raisonnablement le merge, la release ou le déploiement concerné.

---

## 5.6 Major finding

Défaut significatif devant normalement être corrigé avant le merge.

---

## 5.7 Minor finding

Défaut limité pouvant parfois être corrigé ultérieurement selon le contexte.

---

## 5.8 Optional improvement

Amélioration proposée ne constituant pas une non-conformité.

---

## 5.9 Review scope

Périmètre effectivement examiné.

---

## 5.10 Re-review

Nouvelle revue concentrée sur les corrections et les risques de régression.

---

## 5.11 Reservation

Écart connu accepté temporairement par une décision humaine.

---

## 5.12 Recommendation

Conclusion du reviewer sur l’état de l’implémentation.

---

# 6. Emplacement des revues

Structure recommandée :

```text
docs/
└── 06-codex/
    └── reviews/
        ├── active/
        ├── completed/
        └── archived/
```

Une structure simplifiée peut être utilisée pendant le bootstrap :

```text
docs/06-codex/reviews/
```

---

# 7. Convention de nommage

Format recommandé :

```text
{review-id}-{task-id}-{short-title}.md
```

Exemple :

```text
SF-REVIEW-012-SF-TASK-012-validate-client-slug.md
```

Pour une seconde revue :

```text
SF-REVIEW-012-R2-SF-TASK-012-validate-client-slug.md
```

---

# 8. Identifiants

## 8.1 Review ID

Format :

```text
SF-REVIEW-{NUMBER}
```

---

## 8.2 Finding ID

Format :

```text
{review-id}-F{NUMBER}
```

Exemple :

```text
SF-REVIEW-012-F01
```

---

## 8.3 Stabilité

Un finding conserve son identifiant pendant :

- discussion ;
- acceptation ;
- correction ;
- seconde revue ;
- clôture.

---

## 8.4 Nouveau finding en seconde revue

Un problème nouveau utilise un nouvel identifiant.

Il ne doit pas être ajouté sous l’identifiant d’un finding déjà clôturé.

---

# 9. Statuts d’une revue

Valeurs possibles :

```text
draft
in-progress
completed
superseded
cancelled
```

---

## 9.1 `draft`

La revue est en préparation.

---

## 9.2 `in-progress`

L’inspection ou les validations sont en cours.

---

## 9.3 `completed`

Le rapport est finalisé.

Cela ne signifie pas que l’implémentation est acceptée.

---

## 9.4 `superseded`

La revue est remplacée par une revue plus récente.

---

## 9.5 `cancelled`

La revue a été arrêtée, par exemple parce que l’implémentation a été abandonnée.

---

# 10. Front matter recommandé

```yaml
---
reviewId: SF-REVIEW-012
taskId: SF-TASK-012
title: Review client slug validation
status: completed
reviewType: implementation
reviewIteration: 1
reviewer: Codex
implementationRef: feature/validate-client-slug
baseRef: main
commitSha: abc1234
createdAt: 2026-07-28
completedAt: 2026-07-28
recommendation: changes-required
---
```

---

## 10.1 Données interdites

Le front matter ne contient pas :

- secret ;
- token ;
- donnée personnelle ;
- URL privée avec credentials ;
- contenu client confidentiel.

---

# 11. Types de revue

Valeurs recommandées :

```text
implementation
security
architecture
data-migration
generated-project
ci-cd
documentation
operations
release
handover
```

Une revue peut combiner plusieurs profils, mais le profil principal doit être indiqué.

---

# 12. Préconditions d’une revue

Avant de commencer, le reviewer doit disposer de :

```text
[ ] Tâche
[ ] Critères d’acceptation
[ ] Décisions validées
[ ] Diff ou branche
[ ] Commit examiné
[ ] Base de comparaison
[ ] Compte-rendu d’implémentation
[ ] Tests annoncés
[ ] Documentation concernée
```

---

## 12.1 Implémentation non stabilisée

Une revue peut commencer sur une branche non terminée si elle est explicitement décrite comme :

```text
early review
```

Elle ne doit pas produire une recommandation finale de merge.

---

## 12.2 Diff absent

Sans diff, commit ou branche identifiable, le reviewer doit signaler :

```text
SF-REVIEW-VALIDATION-001 — Implementation reference missing.
```

---

# 13. Entrées obligatoires

## 13.1 Tâche

Le reviewer doit lire l’intégralité de la tâche.

---

## 13.2 Documentation requise

Le reviewer doit consulter les documents listés dans la tâche ainsi que ceux imposés par `28-codex-operating-guide.md`.

---

## 13.3 Code

Le reviewer inspecte :

- fichiers modifiés ;
- code environnant ;
- contrats utilisés ;
- tests ;
- configuration ;
- migrations ;
- scripts concernés.

---

## 13.4 Compte-rendu d’implémentation

Le compte-rendu aide à identifier :

- intentions ;
- commandes exécutées ;
- limites ;
- choix techniques ;
- éléments non testés.

Il ne constitue pas une preuve suffisante à lui seul.

---

# 14. État Git à relever

La revue doit enregistrer :

```text
BaseRef
ImplementationRef
CommitSha
WorkingTreeStatus
UntrackedFiles éventuels
```

---

## 14.1 Working tree non propre

Un working tree non propre peut rendre la revue ambiguë.

Le reviewer doit distinguer :

- modifications de la tâche ;
- modifications préexistantes ;
- fichiers non suivis ;
- artefacts générés.

---

# 15. Ordre recommandé de la revue

La revue suit cet ordre :

```text
1. Compréhension de la tâche
2. Périmètre
3. Critères d’acceptation
4. Correctness fonctionnelle
5. Sécurité
6. Données et migrations
7. Architecture
8. Contrats publics
9. Tests
10. Observabilité
11. Génération et reproductibilité
12. CI/CD
13. Opérations
14. Documentation
15. Maintenabilité
16. Style
```

Cet ordre évite qu’une préférence de style masque un défaut fonctionnel ou de sécurité.

---

# 16. Phase 1 — Compréhension de la tâche

Le reviewer doit reformuler brièvement :

- objectif ;
- comportement cible ;
- périmètre ;
- hors périmètre ;
- risques principaux.

---

## 16.1 Conflit dans la tâche

Si les critères se contredisent, la revue doit le signaler.

Le reviewer ne doit pas choisir silencieusement une interprétation.

---

# 17. Phase 2 — Vérification du périmètre

Le reviewer vérifie :

```text
Fichiers nécessaires uniquement

Absence de refonte opportuniste

Hors périmètre préservé

Pas de fonctionnalité cachée

Pas de dépendance ajoutée sans besoin

Pas de modification commerciale inattendue
```

---

## 17.1 Dépassement limité

Une modification hors périmètre peut être acceptable si elle est :

- indispensable ;
- minimale ;
- documentée ;
- sans nouvelle décision structurante.

Elle doit néanmoins être signalée.

---

## 17.2 Dépassement significatif

Un changement important hors périmètre constitue au minimum un finding majeur.

---

# 18. Phase 3 — Vérification des critères d’acceptation

Chaque critère doit recevoir un statut :

```text
passed
failed
partially-passed
not-tested
not-applicable
```

---

## 18.1 Matrice recommandée

| Critère | Statut | Preuve | Commentaire |
|---|---|---|---|
| `AC-01` | `passed` | Test unitaire | Valeur valide acceptée |
| `AC-02` | `failed` | Inspection et test | Double tiret encore accepté |
| `AC-03` | `not-tested` | — | Test d’intégration non exécuté |

---

## 18.2 Critère non testé

Un critère obligatoire non testé ne doit pas être considéré comme satisfait.

---

## 18.3 Critère impossible à vérifier

Le reviewer doit expliquer :

- pourquoi ;
- quel outil ou accès manque ;
- quel risque subsiste ;
- quelle validation humaine reste nécessaire.

---

# 19. Phase 4 — Correctness fonctionnelle

Le reviewer vérifie notamment :

- cas nominal ;
- cas invalides ;
- limites ;
- erreurs ;
- transitions d’état ;
- effets de bord ;
- idempotence ;
- annulation ;
- concurrence lorsque pertinente.

---

## 19.1 Règle métier

La règle doit appartenir à la bonne couche.

---

## 19.2 Erreur publique

Le résultat utilisateur doit être cohérent avec la tâche et ne pas exposer d’informations internes.

---

## 19.3 État partiel

Une opération ayant échoué ne doit pas être présentée comme réussie.

---

# 20. Phase 5 — Sécurité

La revue doit appliquer :

```text
18-security-baseline.md
```

Vérifications possibles :

```text
Authentification

Autorisation

Antiforgery

Validation serveur

XSS

SQL injection

Command injection

Path traversal

Uploads

SSRF

CORS

Redirections

Secrets

Logs

Rate limiting

Docker

Supply chain
```

---

## 20.1 Tests de contournement

Une protection importante doit être vérifiée sans dépendre uniquement de l’interface.

Exemple :

```text
appel HTTP direct
→ accès refusé
```

---

## 20.2 Secret détecté

Un secret réel ou vraisemblablement exploitable constitue généralement un finding bloquant.

---

## 20.3 Donnée personnelle dans les logs

Une PII introduite dans les logs constitue au minimum un finding majeur et peut devenir bloquant selon l’exposition.

---

# 21. Phase 6 — Données et migrations

Le reviewer vérifie :

- modèle ;
- nullabilité ;
- longueurs ;
- contraintes ;
- index ;
- clés étrangères ;
- concurrence ;
- rétention ;
- migration ;
- données existantes ;
- rollback ;
- backup.

---

## 21.1 Modification EF Core sans migration

Si le modèle persistant est modifié sans migration prévue, le défaut est généralement bloquant pour une release concernée.

---

## 21.2 Migration destructive

La revue doit identifier :

```text
DROP
suppression de colonne
réduction de taille
NOT NULL sans stratégie
transformation irréversible
```

---

## 21.3 Production

Le reviewer doit vérifier qu’aucune auto-migration de production n’a été ajoutée.

---

## 21.4 PostgreSQL réel

Un comportement PostgreSQL ne doit pas être considéré comme validé uniquement avec un provider en mémoire.

---

# 22. Phase 7 — Architecture

La revue vérifie :

```text
Domain sans dépendance technique

Application sans dépendance CLI

Infrastructure derrière des ports

CLI limité au parsing et à la présentation

Modules sans dépendance client

Customizations isolées

Pas de dépendance circulaire

Pas de logique commerciale dans l’UI
```

---

## 22.1 Tests d’architecture

Les règles structurelles doivent idéalement être couvertes automatiquement.

---

## 22.2 Nouvelle abstraction

Une nouvelle abstraction doit répondre à un besoin réel.

Une interface créée uniquement pour faciliter un mock peut être signalée comme sur-ingénierie.

---

# 23. Phase 8 — Contrats publics

Les contrats suivants nécessitent une attention renforcée :

```text
Configuration YAML

Schémas JSON

CLI

Codes d’erreur

Permissions

Routes

EventIds

Manifests

Lock file

Rapports

Interfaces de modules
```

---

## 23.1 Changement incompatible

Le reviewer doit vérifier :

- version ;
- migration ;
- dépréciation ;
- documentation ;
- compatibilité.

---

## 23.2 Contrat modifié sans documentation

Constitue généralement un finding majeur.

---

# 24. Phase 9 — Tests

Le reviewer inspecte :

- scénarios ;
- assertions ;
- cas négatifs ;
- isolation ;
- déterminisme ;
- données ;
- technologies réelles ;
- lisibilité ;
- durée ;
- tests ignorés.

---

## 24.1 Tests qui reproduisent l’implémentation

Un test trop couplé aux appels internes peut être signalé.

---

## 24.2 Assertion insuffisante

Exemple :

```csharp
Assert.NotNull(result);
```

alors que le comportement important n’est pas vérifié.

---

## 24.3 Régression

Une correction de bug sans test de régression constitue généralement un finding majeur ou mineur selon le risque.

---

## 24.4 Tests ignorés

Un test critique nouvellement ignoré constitue un finding bloquant.

---

## 24.5 Tests annoncés mais absents

Le reviewer doit signaler la différence entre le compte-rendu et la réalité.

---

# 25. Phase 10 — Observabilité

La revue applique :

```text
21-observability-and-logging.md
```

Vérifier :

- EventId ;
- niveau ;
- message template ;
- correlation ID ;
- propriétés ;
- absence de PII ;
- absence de secret ;
- absence de corps HTTP ;
- absence de haute cardinalité.

---

## 25.1 Objet métier journalisé

Exemple problématique :

```csharp
logger.LogInformation("Lead {@Lead}", lead);
```

---

## 25.2 Double log d’exception

Une exception journalisée à plusieurs niveaux peut produire du bruit et des alertes dupliquées.

---

# 26. Phase 11 — Génération

Pour une modification affectant SiteFactory, vérifier :

- déterminisme ;
- plan ;
- stratégies de fichiers ;
- chemins ;
- dossier temporaire ;
- rapport ;
- lock file ;
- projet généré ;
- non-écrasement.

---

## 26.1 Fichiers `custom`

Toute écriture automatique sur un fichier `custom` constitue un finding bloquant.

---

## 26.2 Fichiers `protected`

Toute tentative de remplacement est bloquante.

---

## 26.3 Projet généré

Un changement significatif du générateur doit être validé par une génération réelle et le build du projet produit.

---

# 27. Phase 12 — CI/CD

La revue applique :

```text
20-quality-gates-and-ci.md
```

Vérifier :

- trigger ;
- permissions ;
- solution `.slnx` ;
- actions ;
- secrets ;
- matrices ;
- timeouts ;
- artefacts ;
- checks ;
- `continue-on-error`.

---

## 27.1 Permissions excessives

Une permission globale telle que :

```yaml
permissions: write-all
```

constitue généralement un finding majeur ou bloquant.

---

## 27.2 Scan rendu non bloquant

L’ajout de :

```yaml
continue-on-error: true
```

sur un gate sécurité obligatoire constitue un finding bloquant.

---

# 28. Phase 13 — Déploiement et opérations

Pour une tâche opérationnelle, vérifier :

- environnement ;
- image ;
- tag ;
- secrets ;
- volumes ;
- réseau ;
- migrations ;
- health ;
- smoke tests ;
- backup ;
- rollback ;
- runbooks.

---

## 28.1 Production implicite

Un script généré ne doit pas déployer automatiquement en production sans confirmation spécifique.

---

## 28.2 Suppression de volume

Une commande destructrice non protégée constitue un finding bloquant.

---

# 29. Phase 14 — Documentation

Vérifier :

- comportement documenté ;
- exemples valides ;
- liens ;
- noms ;
- versions ;
- chemins ;
- changelog ;
- décisions ;
- absence de placeholder.

---

## 29.1 Documentation contredisant le code

Le reviewer doit déterminer quelle source est propriétaire.

Il ne doit pas considérer automatiquement le code comme correct.

---

## 29.2 Documentation absente

Une modification de contrat sans mise à jour documentaire constitue généralement un finding majeur.

---

# 30. Phase 15 — Maintenabilité

Vérifier notamment :

- lisibilité ;
- responsabilités ;
- duplication ;
- nommage ;
- complexité ;
- cohésion ;
- dépendances ;
- extension future raisonnable ;
- dette introduite.

---

## 30.1 Sur-ingénierie

Signaux :

- nombreuses abstractions sans besoin ;
- configuration excessivement générique ;
- système d’extension non demandé ;
- framework interne pour une règle simple.

---

## 30.2 Sous-ingénierie

Signaux :

- logique dupliquée ;
- règles dispersées ;
- chaîne magique ;
- absence de type métier ;
- absence de validation centralisée.

---

# 31. Phase 16 — Style

Le style est examiné après les éléments plus importants.

Vérifier :

- conventions du repository ;
- `.editorconfig` ;
- nommage ;
- formatage ;
- commentaires ;
- cohérence de langue.

Une simple préférence personnelle ne doit pas devenir un finding majeur.

---

# 32. Niveaux de sévérité

## 32.1 Bloquant

Un finding est bloquant lorsqu’il existe un risque tel que :

- fonctionnalité principale incorrecte ;
- critère essentiel non satisfait ;
- build ou tests obligatoires échoués ;
- vulnérabilité critique ;
- secret exposé ;
- perte ou corruption de données ;
- migration dangereuse ;
- contournement d’autorisation ;
- fichier `custom` ou `protected` écrasé ;
- opération production implicite ;
- contrat majeur incohérent ;
- impossibilité de restaurer ou rollback lorsqu’obligatoire.

Recommandation habituelle :

```text
Refus
```

---

## 32.2 Majeur

Exemples :

- comportement important incomplet ;
- test de régression manquant ;
- validation serveur absente ;
- contrat public non documenté ;
- risque de maintenance significatif ;
- erreur d’architecture importante ;
- contrôle de sécurité incomplet ;
- erreur sur un cas utilisateur fréquent.

Recommandation habituelle :

```text
Corrections nécessaires
```

---

## 32.3 Mineur

Exemples :

- diagnostic imprécis ;
- test complémentaire utile ;
- duplication limitée ;
- documentation secondaire incomplète ;
- nommage trompeur sans impact immédiat ;
- cas limite peu probable non couvert.

Peut parfois être accepté comme réserve.

---

## 32.4 Optionnel

Exemples :

- simplification ;
- amélioration UX non requise ;
- refactoring futur ;
- optimisation sans mesure ;
- documentation supplémentaire.

Ne bloque pas le merge.

---

# 33. Distinction entre finding, risque, question et suggestion

## 33.1 Finding confirmé

Un défaut est démontré.

---

## 33.2 Risque

Un problème potentiel existe, mais la preuve complète manque.

Le reviewer doit préciser les conditions nécessaires à sa matérialisation.

---

## 33.3 Question

Information nécessaire pour comprendre une décision ou une intention.

Une question n’est pas automatiquement un finding.

---

## 33.4 Suggestion

Amélioration facultative.

---

## 33.5 Format du rapport

Ces catégories doivent être séparées pour éviter la confusion.

---

# 34. Structure d’un finding

```markdown
### SF-REVIEW-012-F01 — Le slug accepte encore les doubles tirets

- **Sévérité** : Majeur
- **Statut** : Ouvert
- **Critère affecté** : `AC-05`
- **Emplacement** :
  - `src/.../ClientSlug.cs`
  - méthode `TryCreate`
- **Problème** :
  La validation accepte `sample--project`, contrairement au format
  kebab-case défini par la tâche.
- **Impact** :
  Une configuration non conforme peut atteindre le pipeline de
  génération et produire des noms de ressources incohérents.
- **Preuve** :
  Le pattern utilisé autorise deux séparateurs consécutifs.
  Le test `ClientSlug_DoubleHyphen_IsRejected` est absent.
- **Correction attendue** :
  Refuser les tirets consécutifs et ajouter un test de régression.
```

---

# 35. Champs d’un finding

Champs recommandés :

```text
FindingId
Title
Severity
Status
AcceptanceCriterion
Location
Problem
Impact
Evidence
ExpectedCorrection
```

Champs optionnels :

```text
IntroducedBy
AffectedPlatforms
AffectedModules
SecurityClassification
SuggestedTest
```

---

# 36. Statuts d’un finding

Valeurs possibles :

```text
open
accepted
rejected
fixed
partially-fixed
verified
deferred
```

---

## 36.1 `open`

Finding produit par la revue, non encore trié.

---

## 36.2 `accepted`

Finding reconnu comme nécessitant une correction.

---

## 36.3 `rejected`

Finding considéré comme incorrect ou non applicable.

Une justification doit être conservée.

---

## 36.4 `fixed`

L’implémenteur déclare le finding corrigé.

---

## 36.5 `verified`

Le reviewer confirme la correction.

---

## 36.6 `deferred`

Correction reportée par décision humaine.

Elle doit posséder :

- justification ;
- risque ;
- propriétaire ;
- échéance.

---

# 37. Qualité des preuves

## 37.1 Bonne preuve

```text
Le test X échoue avec la valeur Y.

L’endpoint retourne 200 pour un utilisateur sans permission.

La migration supprime la colonne Z.

Le workflow accorde contents: write à tous les jobs.
```

---

## 37.2 Preuve insuffisante

```text
Cela semble risqué.

Je préfère une autre approche.

Ce code pourrait être amélioré.

Ce n’est pas très clean.
```

---

## 37.3 Extrait de code

Le finding peut citer un extrait court nécessaire à la compréhension.

Il ne doit pas recopier inutilement un fichier complet.

---

# 38. Emplacements

Le reviewer doit fournir l’emplacement le plus précis disponible :

```text
Chemin
Type
Méthode
Section
Ligne approximative
```

Les numéros de lignes peuvent changer après correction.

Le chemin et le symbole restent prioritaires.

---

# 39. Findings dupliqués

Plusieurs symptômes provenant d’une même cause peuvent être regroupés dans un finding.

Exemple :

```text
Même permission absente sur trois endpoints du même handler
```

Ils peuvent être séparés si :

- corrections distinctes ;
- sévérités distinctes ;
- causes distinctes ;
- validation séparée utile.

---

# 40. Findings hors périmètre

Un problème préexistant hors périmètre peut être placé dans une section :

```text
Observations hors périmètre
```

Il ne doit pas influencer injustement la recommandation sur la tâche, sauf s’il empêche une validation sûre.

---

# 41. Sévérité et préexistence

Un défaut préexistant peut rester bloquant pour une release même s’il n’a pas été introduit par la tâche.

Le rapport doit distinguer :

```text
Introduit par la tâche
Préexistant
Révélé par la tâche
```

---

# 42. Recommandations finales

Valeurs standard :

```text
refused
changes-required
acceptable-with-reservations
ready-to-merge
```

---

## 42.1 `refused`

Utilisé lorsque :

- bloquant ;
- implémentation largement hors sujet ;
- risque de données ou sécurité ;
- validation impossible ;
- code non compilable.

---

## 42.2 `changes-required`

Utilisé lorsque :

- aucun rejet total ;
- mais un ou plusieurs findings majeurs nécessitent correction.

---

## 42.3 `acceptable-with-reservations`

Utilisé lorsque :

- aucun finding bloquant ;
- pas de finding majeur non accepté ;
- quelques écarts mineurs sont explicitement acceptables ou reportables.

La décision de merge reste humaine.

---

## 42.4 `ready-to-merge`

Utilisé lorsque :

- critères satisfaits ;
- aucun finding obligatoire ouvert ;
- validations exécutées ;
- documentation cohérente ;
- risques résiduels acceptables.

---

# 43. Score de revue

## 43.1 Statut

Un score numérique est facultatif.

---

## 43.2 Utilité

Il peut fournir une synthèse rapide.

Exemple :

```text
8/10
```

---

## 43.3 Limite

Le score ne doit jamais contredire les findings.

Une implémentation avec un finding bloquant ne doit pas être présentée comme quasiment prête uniquement grâce à une note élevée.

---

## 43.4 Proposition de grille

| Score | Interprétation indicative |
|---:|---|
| `10` | Conforme, preuves complètes |
| `8–9` | Bon niveau, écarts mineurs |
| `6–7` | Corrections significatives |
| `4–5` | Plusieurs défauts majeurs |
| `1–3` | Implémentation non acceptable |

Cette grille reste indicative.

---

# 44. Commandes exécutées

Le rapport doit lister précisément les commandes exécutées.

Exemple :

```powershell
dotnet build SiteFactory.slnx  --configuration Release --no-restore
```

Résultat :

```text
0 warning
0 erreur
```

---

## 44.1 Tests

Indiquer :

```text
Total
Réussis
Échecs
Ignorés
Durée
```

uniquement à partir du résultat réel.

---

## 44.2 Commande non exécutée

Préciser :

```text
Non exécutée
Raison
Impact
Action restante
```

---

# 45. Vérifications documentaires

Certaines vérifications peuvent être réalisées sans exécuter le code :

- contrat ;
- version ;
- schéma ;
- structure ;
- manifest ;
- documentation ;
- configuration.

Le rapport doit les distinguer des tests d’exécution.

---

# 46. Tests manuels

Une revue peut recommander ou enregistrer :

- navigation clavier ;
- responsive ;
- rendu visuel ;
- comportement navigateur ;
- recette fonctionnelle.

Codex ne doit pas les déclarer réussis sans les avoir réellement réalisés avec les outils disponibles.

---

# 47. Résultats de sécurité

Le rapport doit préciser :

```text
Secret scan
Dependency scan
Static analysis
Authorization tests
Antiforgery tests
Security headers
```

Valeurs possibles :

```text
success
failed
not-run
not-applicable
```

---

# 48. Revue de dépendance

Pour une nouvelle dépendance, vérifier :

- besoin ;
- package ID ;
- version ;
- licence ;
- maintenance ;
- vulnérabilités ;
- transitives ;
- alternative ;
- source NuGet.

Une dépendance structurante non validée constitue un finding majeur ou bloquant selon l’impact.

---

# 49. Revue de migration

Le rapport doit inclure une section spécifique :

```text
Migration required
Migration present
Destructive operations
Existing data strategy
Backup requirement
Rollback
Production execution
```

---

## 49.1 Migration production exécutée

Si Codex ou le workflow de validation applique une migration à une production, le finding est bloquant.

---

# 50. Revue de projet généré

Appliquer les contrôles à la sortie réellement contractée, sans supprimer
une preuve requise : Documentation statique exige rendu/liens/navigation/
sécurité/déterminisme ; une sortie .NET exige restore/build/tests réels.
Docker ou Identity ne sont pas des capacités de la sortie Documentation.
Les E2E d'applications Business restent privés et distincts.

Pour une modification du générateur, vérifier :

```text
Configuration de référence

Plan

Génération

Arborescence

Lock file

Rapport

Restore

Build Release

Tests

Docker

Secrets

Placeholders
```

---

# 51. Revue d’interface utilisateur

Vérifier :

- états ;
- responsive ;
- clavier ;
- focus ;
- labels ;
- erreurs ;
- chargement ;
- permission ;
- contraste ;
- composants du design system.

---

## 51.1 Capture ou observation

Un défaut visuel doit idéalement être accompagné :

- d’une capture ;
- d’un test ;
- d’un composant précis ;
- d’une largeur d’écran.

---

# 52. Revue SEO

Appliquer :

```text
Contrat SEO du site : title et description, H1, canonical, robots, sitemap, JSON-LD, slugs, redirections et domaine coherents ; contenu factuel sans promesse de classement
```

Vérifier :

- title ;
- description ;
- H1 ;
- canonical ;
- robots ;
- sitemap ;
- JSON-LD ;
- slugs ;
- redirections ;
- domaine d’environnement.

---

# 53. Revue de documentation

Vérifier :

- source de vérité ;
- structure ;
- terminologie ;
- liens ;
- exemples ;
- cohérence ;
- décisions ;
- absence de contenu inventé ;
- absence de secret.

---

## 53.1 Document sans code

Une tâche uniquement documentaire ne nécessite pas toujours un build complet.

La revue doit exécuter les validations documentaires prévues.

---

# 54. Revue CI/CD

Vérifier au minimum :

```text
Triggers
Permissions
Actions
Runtime des actions
Solution .slnx
Secrets
Cache
Artefacts
Checks
Timeouts
Concurrency
Publication
```

---

# 55. Revue opérationnelle

Pour un script ou runbook :

- contexte ;
- client ;
- environnement ;
- chemins ;
- confirmations ;
- codes de sortie ;
- idempotence ;
- verrouillage ;
- secrets ;
- rollback ;
- tests.

---

## 55.1 Commande destructive

Une commande destructive sans contrôle constitue un finding bloquant.

---

# 56. Revue de release

Appliquer :

```text
26-release-and-versioning.md
```

Vérifier :

- version ;
- SemVer ;
- tag ;
- changelog ;
- manifest ;
- checksums ;
- artifacts ;
- immutabilité ;
- canal ;
- quality gates.

---

# 57. Revue de transfert client

Appliquer :

```text
27-client-handover-and-service-closure.md
```

Vérifier :

- inventaire ;
- version ;
- backup ;
- responsabilités ;
- secrets séparés ;
- accès ;
- restauration ;
- retrait LogicGate ;
- rapport.

Codex ne doit pas déclarer un transfert effectif à partir des seuls documents.

---

# 58. Template complet de revue

```markdown
---
reviewId: SF-REVIEW-XXX
taskId: SF-TASK-XXX
title: Review short title
status: completed
reviewType: implementation
reviewIteration: 1
reviewer: Codex
implementationRef: feature/example
baseRef: main
commitSha: null
createdAt: YYYY-MM-DD
completedAt: YYYY-MM-DD
recommendation: changes-required
---

# SF-REVIEW-XXX — Review short title

## 1. Résumé

Résumer l’objectif de la tâche et l’état général de l’implémentation.

## 2. Références examinées

- Tâche :
- Branche ou commit :
- Base :
- Documents :
- Compte-rendu d’implémentation :

## 3. Périmètre réellement examiné

- Code
- Tests
- Documentation
- Migrations
- CI
- Artefacts générés

## 4. Éléments non examinés

- Élément
- Raison
- Risque restant

## 5. État Git

- Base ref :
- Implementation ref :
- Commit :
- Working tree :
- Fichiers non suivis :

## 6. Matrice des critères d’acceptation

| Critère | Statut | Preuve | Commentaire |
|---|---|---|---|
| AC-01 | passed | Test | |
| AC-02 | failed | Inspection | |

## 7. Vérifications exécutées

### Build

```bash
command
```

Résultat :

```text
...
```

### Tests

```bash
command
```

Résultat :

```text
Total :
Réussis :
Échecs :
Ignorés :
```

### Scans

- Secret scan :
- Dependency scan :
- Static analysis :

### Projet généré

- Génération :
- Restore :
- Build :
- Tests :

## 8. Findings bloquants

Aucun.

Ou :

### SF-REVIEW-XXX-F01 — Titre

- Sévérité :
- Statut :
- Critère affecté :
- Emplacement :
- Problème :
- Impact :
- Preuve :
- Correction attendue :

## 9. Findings majeurs

## 10. Findings mineurs

## 11. Améliorations optionnelles

## 12. Observations hors périmètre

## 13. Vérifications réussies

- Élément
- Preuve

## 14. Risques résiduels

- Risque
- Cause
- Validation restante

## 15. Documentation

- À jour :
- Manquante :
- Contradictoire :

## 16. Migrations et données

- Migration requise :
- Migration présente :
- Backup :
- Rollback :
- Production exécutée : non

## 17. Sécurité

- Authentification :
- Autorisation :
- Antiforgery :
- Validation serveur :
- Secrets :
- Logs :
- Dépendances :

## 18. Recommandation finale

Valeur :

```text
refused
changes-required
acceptable-with-reservations
ready-to-merge
```

Justification :

## 19. Note facultative

```text
X/10
```

## 20. Prochaine étape recommandée

Décrire une seule prochaine étape principale.
```

---

# 59. Template simplifié

Pour une tâche `XS` ou documentaire à faible risque :

```markdown
---
reviewId: SF-REVIEW-XXX
taskId: SF-TASK-XXX
status: completed
reviewType: implementation
reviewIteration: 1
recommendation: ready-to-merge
---

# SF-REVIEW-XXX — Short title

## Résumé

## Critères d’acceptation

| Critère | Statut | Preuve |
|---|---|---|

## Vérifications

- Build :
- Tests :
- Documentation :

## Findings

Aucun.

## Risques résiduels

Aucun identifié dans le périmètre examiné.

## Recommandation

`ready-to-merge`
```

---

## 59.1 Cas où le template simplifié est interdit

Il ne doit pas être utilisé pour :

- sécurité ;
- Identity ;
- autorisation ;
- données ;
- migration ;
- génération structurante ;
- infrastructure ;
- CI sensible ;
- release ;
- backup ;
- restauration ;
- transfert client.

---

# 60. Exemple de revue

```markdown
---
reviewId: SF-REVIEW-012
taskId: SF-TASK-012
title: Review client slug validation
status: completed
reviewType: implementation
reviewIteration: 1
reviewer: Codex
implementationRef: feature/validate-client-slug
baseRef: main
commitSha: abc1234
createdAt: 2026-07-28
completedAt: 2026-07-28
recommendation: changes-required
---

# SF-REVIEW-012 — Review client slug validation

## 1. Résumé

L’implémentation introduit correctement le value object `ClientSlug`
dans Domain et l’utilise avant la création du plan de génération.

La majorité des règles demandées est couverte. Deux écarts majeurs
restent à corriger : les doubles tirets sont encore acceptés et le
test d’intégration ne démontre pas l’absence d’écriture après erreur.

## 2. Références examinées

- Tâche : `SF-TASK-012-validate-client-slug.md`
- Branche : `feature/validate-client-slug`
- Base : `main`
- Commit : `abc1234`
- Documents :
  - `14-client-configuration-schema.md`
  - `18-security-baseline.md`
  - `19-testing-strategy.md`

## 3. Périmètre examiné

- `ClientSlug`
- Mapping de configuration
- Validation applicative
- Tests Domain
- Tests Application
- Documentation du schéma

## 4. Éléments non examinés

- CI Windows distante : non disponible localement
- Pipeline complet de release : hors périmètre

## 5. Matrice des critères

| Critère | Statut | Preuve | Commentaire |
|---|---|---|---|
| `AC-01` | `passed` | Test | `sample-project` accepté |
| `AC-02` | `passed` | Test | `sample-123` accepté |
| `AC-03` | `passed` | Test | Majuscules refusées |
| `AC-04` | `passed` | Test | Path traversal refusé |
| `AC-05` | `failed` | Test ajouté lors de la revue | Double tiret accepté |
| `AC-06` | `passed` | Test | Longueur contrôlée |
| `AC-07` | `passed` | Inspection | Code attendu |
| `AC-08` | `passed` | Test Application | Planner non appelé |
| `AC-09` | `partially-passed` | Inspection | Aucune preuve filesystem réelle |
| `AC-10` | `passed` | Architecture test | Aucune dépendance YAML |

## 6. Vérifications

### Build

```powershell
dotnet build SiteFactory.slnx  --configuration Release --no-restore
```

Résultat :

```text
0 warning
0 erreur
```

### Tests

```powershell
dotnet test SiteFactory.slnx  --configuration Release  --no-build --no-restore --filter "Category!=PostgreSql"
```

Résultat :

```text
Total : 42
Réussis : 42
Échecs : 0
Ignorés : 0
```

## 7. Findings bloquants

Aucun.

## 8. Findings majeurs

### SF-REVIEW-012-F01 — Les doubles tirets sont acceptés

- **Sévérité** : Majeur
- **Statut** : Ouvert
- **Critère affecté** : `AC-05`
- **Emplacement** :
  `ClientSlug.cs`, méthode `TryCreate`
- **Problème** :
  La validation contrôle les caractères mais pas les séparateurs
  consécutifs.
- **Impact** :
  `sample--project` ne respecte pas le contrat kebab-case validé.
- **Preuve** :
  L’expression utilisée accepte deux tirets successifs.
- **Correction attendue** :
  Corriger la validation et ajouter un test de régression.

### SF-REVIEW-012-F02 — L’absence d’écriture n’est pas testée réellement

- **Sévérité** : Majeur
- **Statut** : Ouvert
- **Critère affecté** : `AC-09`
- **Emplacement** :
  tests Application de validation de configuration
- **Problème** :
  Le test vérifie que le planner n’est pas appelé, mais ne vérifie
  pas le filesystem lorsque le pipeline est exécuté.
- **Impact** :
  Une future modification pourrait produire un fichier avant
  la validation sans faire échouer le test actuel.
- **Correction attendue** :
  Ajouter un test d’intégration avec un dossier temporaire et vérifier
  qu’il reste vide.

## 9. Findings mineurs

Aucun.

## 10. Améliorations optionnelles

- Ajouter le code d’erreur dans l’exemple JSON de documentation.

## 11. Vérifications réussies

- Domain ne référence pas le parseur YAML.
- Les majuscules sont refusées.
- Les séparateurs de chemin sont refusés.
- Le message ne loggue pas la configuration complète.
- La documentation du format a été mise à jour.

## 12. Risques résiduels

- Le comportement Windows n’a pas été exécuté localement.
  La CI Windows reste nécessaire.

## 13. Recommandation finale

```text
changes-required
```

Deux corrections majeures sont nécessaires avant fusion.

## 14. Note facultative

```text
8/10
```
```

---

# 61. Revue des corrections

La seconde revue doit se concentrer sur :

```text
Findings acceptés
Corrections
Tests de régression
Régressions introduites
Diff supplémentaire
```

---

## 61.1 Références

La seconde revue doit identifier :

- revue initiale ;
- findings acceptés ;
- commit de correction ;
- nouveaux changements.

---

## 61.2 Finding corrigé

Le statut devient :

```text
verified
```

uniquement après vérification réelle.

---

## 61.3 Finding partiellement corrigé

Le reviewer doit expliquer ce qui reste insuffisant.

---

## 61.4 Finding rejeté

Le reviewer vérifie la justification humaine.

Il ne doit pas le réintroduire sans nouvel élément.

---

# 62. Template de seconde revue

```markdown
# SF-REVIEW-XXX-R2 — Re-review short title

## Références

- Revue initiale :
- Commit initial :
- Commit de correction :
- Findings acceptés :

## Vérification des findings

### SF-REVIEW-XXX-F01

- Statut précédent : accepted
- Correction observée :
- Test :
- Résultat :
- Nouveau statut : verified

### SF-REVIEW-XXX-F02

- Statut précédent :
- Correction observée :
- Résultat :
- Nouveau statut :

## Régressions recherchées

- Périmètre
- Résultat

## Nouveaux findings

Aucun.

## Build et tests

## Recommandation finale
```

---

# 63. Nouvelle régression découverte pendant la seconde revue

Une correction peut introduire un nouveau défaut.

Le reviewer doit :

1. créer un nouvel identifiant ;
2. fournir la preuve ;
3. ne pas rouvrir artificiellement un ancien finding ;
4. réviser la recommandation finale.

---

# 64. Gestion des findings refusés

Un finding peut être refusé si :

- preuve incorrecte ;
- contrat mal interprété ;
- comportement volontaire documenté ;
- problème hors périmètre sans impact ;
- suggestion présentée comme défaut.

La justification du refus doit rester dans la trace de revue.

---

# 65. Réserves

Une réserve acceptable doit préciser :

```text
Finding
Risque
Raison du report
Mesure compensatoire
Propriétaire
Échéance
```

Une réserve sans propriétaire ni échéance ne doit pas devenir un moyen de contourner une correction obligatoire.

---

# 66. Findings de sécurité confidentiels

Pour une vulnérabilité exploitable, le rapport partagé largement peut limiter :

- payload précis ;
- secret ;
- endpoint sensible ;
- procédure d’exploitation.

Une version détaillée peut être conservée dans un espace restreint.

La correction et l’impact doivent rester suffisamment clairs.

---

# 67. Revue indépendante

## 67.1 Nouveau contexte

Lorsque possible, le reviewer doit utiliser un contexte distinct de l’implémentation.

Cela réduit le risque de reproduire les mêmes suppositions.

---

## 67.2 Même agent

Si le même agent réalise la revue :

- le mode doit être explicitement changé ;
- la tâche et le diff doivent être relus depuis le début ;
- le reviewer ne doit pas se fier à ses intentions d’implémentation ;
- les preuves doivent rester indépendantes.

---

# 68. Revue par le mainteneur unique

Dans un repository maintenu par une seule personne :

```text
CI verte
+ auto-revue humaine
+ revue Codex distincte
```

peuvent constituer la baseline.

Les changements suivants méritent une vigilance renforcée :

- sécurité ;
- migrations ;
- Identity ;
- prix ;
- entitlements ;
- release ;
- production ;
- backup ;
- restauration.

---

# 69. Review checklist générale

```text
[ ] Tâche lue
[ ] Critères identifiés
[ ] Documentation lue
[ ] Diff inspecté
[ ] Code environnant inspecté
[ ] Tests inspectés
[ ] Périmètre respecté
[ ] Hors périmètre respecté
[ ] Fonctionnel vérifié
[ ] Sécurité vérifiée
[ ] Données vérifiées
[ ] Architecture vérifiée
[ ] Contrats vérifiés
[ ] Observabilité vérifiée
[ ] Documentation vérifiée
[ ] Build exécuté
[ ] Tests exécutés
[ ] Éléments non testés signalés
[ ] Findings prouvés
[ ] Recommandation formulée
```

---

# 70. Checklist sécurité

```text
[ ] Authentification
[ ] Autorisation serveur
[ ] Antiforgery
[ ] Validation serveur
[ ] Encodage
[ ] Requêtes paramétrées
[ ] Chemins contrôlés
[ ] Uploads contrôlés
[ ] URLs contrôlées
[ ] CORS
[ ] Rate limiting
[ ] Secrets
[ ] PII dans les logs
[ ] Headers
[ ] Docker
[ ] Dépendances
```

---

# 71. Checklist données et migrations

```text
[ ] Modèle cohérent
[ ] Nullabilité
[ ] Longueurs
[ ] Index
[ ] Relations
[ ] Migration présente
[ ] Données existantes
[ ] Opérations destructives
[ ] Backup
[ ] Rollback
[ ] PostgreSQL réel
[ ] Aucune auto-migration production
```

---

# 72. Checklist génération

```text
[ ] Configuration
[ ] Plan
[ ] Déterminisme
[ ] Chemins
[ ] Dossier temporaire
[ ] Stratégies de fichiers
[ ] Custom préservé
[ ] Protected préservé
[ ] Lock file
[ ] Rapport
[ ] Projet généré
[ ] Build du projet
[ ] Tests du projet
```

---

# 73. Checklist CI/CD

```text
[ ] Trigger
[ ] Permissions minimales
[ ] Actions maîtrisées
[ ] Runtime supporté
[ ] `.slnx`
[ ] Secrets
[ ] Pull requests non fiables
[ ] Quality gates
[ ] `continue-on-error`
[ ] Windows
[ ] Linux
[ ] Artefacts
[ ] Publication
```

---

# 74. Checklist documentation

```text
[ ] Document propriétaire mis à jour
[ ] Exemples valides
[ ] Liens valides
[ ] Terminologie cohérente
[ ] Versions cohérentes
[ ] Changelog
[ ] Migration
[ ] Aucun placeholder
[ ] Aucun secret
[ ] Aucune donnée réelle
```

---

# 75. Checklist avant `ready-to-merge`

```text
[ ] Tous les critères obligatoires satisfaits
[ ] Aucun finding bloquant ouvert
[ ] Aucun finding majeur ouvert
[ ] Build Release réussi
[ ] Tests obligatoires réussis
[ ] Aucun test critique ignoré
[ ] Scans nécessaires réussis
[ ] Documentation à jour
[ ] Migrations contrôlées
[ ] Projet généré validé si nécessaire
[ ] Risques résiduels documentés
```

---

# 76. Conditions d’arrêt de la revue

Le reviewer doit suspendre la conclusion lorsque :

- tâche contradictoire ;
- diff incomplet ;
- commit inconnu ;
- accès requis manquant ;
- tests essentiels impossibles ;
- secret réel découvert ;
- risque de données nécessitant une décision ;
- migration destructive non documentée ;
- périmètre non identifiable ;
- production nécessaire à la vérification.

---

## 76.1 Rapport de blocage

```text
Blocage
Élément manquant
Impact sur la revue
Vérifications possibles malgré le blocage
Décision ou action requise
```

---

# 77. Codes documentaires recommandés

| Code | Signification |
|---|---|
| `SF-REVIEW-VALIDATION-001` | Référence d’implémentation absente |
| `SF-REVIEW-VALIDATION-002` | Tâche absente |
| `SF-REVIEW-VALIDATION-003` | Critères d’acceptation absents |
| `SF-REVIEW-VALIDATION-004` | Base de comparaison inconnue |
| `SF-REVIEW-VALIDATION-005` | Diff ambigu |
| `SF-REVIEW-VALIDATION-006` | Tests essentiels non exécutables |
| `SF-REVIEW-VALIDATION-007` | Documentation contradictoire |
| `SF-REVIEW-VALIDATION-008` | Secret détecté |
| `SF-REVIEW-VALIDATION-009` | Production requise |
| `SF-REVIEW-VALIDATION-010` | Preuve insuffisante |
| `SF-REVIEW-VALIDATION-011` | Sévérité non justifiée |
| `SF-REVIEW-VALIDATION-012` | Finding dupliqué |
| `SF-REVIEW-VALIDATION-013` | Recommandation incohérente |
| `SF-REVIEW-VALIDATION-014` | Élément non examiné non signalé |
| `SF-REVIEW-VALIDATION-015` | Résultat de test non vérifié |

---

# 78. Quality gates de la revue

Une revue ne peut pas être considérée comme complète si :

```text
Tâche non lue

Critères non examinés

Diff non identifié

Findings sans preuve

Tests annoncés comme réussis sans exécution

Éléments non examinés dissimulés

Sécurité non vérifiée pour une tâche sensible

Migration non vérifiée pour une tâche données

Recommandation contredisant les findings

Secret présent dans le rapport
```

---

# 79. Critères d’acceptation du modèle

Le modèle sera considéré comme opérationnel lorsque :

1. les revues possèdent un identifiant stable ;
2. les findings possèdent un identifiant stable ;
3. les types de revue sont définis ;
4. les préconditions sont définies ;
5. les sources de comparaison sont définies ;
6. l’ordre de revue est défini ;
7. les critères d’acceptation sont vérifiés un par un ;
8. les sévérités sont définies ;
9. les défauts et suggestions sont distingués ;
10. les findings possèdent des preuves ;
11. les emplacements sont précis ;
12. les commandes exécutées sont consignées ;
13. les éléments non exécutés sont signalés ;
14. les recommandations finales sont standardisées ;
15. un template complet existe ;
16. un template simplifié existe ;
17. un exemple complet existe ;
18. une seconde revue est définie ;
19. les findings rejetés restent traçables ;
20. les réserves possèdent un propriétaire et une échéance ;
21. les quality gates de revue sont définis ;
22. la décision finale reste humaine ;
23. aucun secret ou donnée réelle n’est autorisé dans le rapport ;
24. Codex ne modifie pas le code pendant une revue sans demande ;
25. une revue ne déclare jamais des validations non exécutées.

---

# 80. Décisions validées

La baseline des revues Codex est la suivante :

```text
Une revue est distincte de l’implémentation.

La tâche et ses critères constituent le contrat principal.

Le reviewer inspecte le diff et le code environnant.

La revue vérifie d’abord le fonctionnel, la sécurité,
les données et l’architecture avant le style.

Chaque critère d’acceptation reçoit un statut explicite.

Un critère non testé n’est pas considéré comme satisfait.

Les findings possèdent un identifiant stable.

Les sévérités sont blocking, major, minor et optional.

Un finding bloquant empêche une recommandation de merge.

Une amélioration facultative n’est pas présentée comme un défaut.

Un finding contient un emplacement, un impact,
une preuve et une correction attendue.

Les problèmes préexistants sont distingués
des régressions introduites.

Les observations hors périmètre sont séparées.

Les commandes réellement exécutées sont consignées.

Les résultats de tests ne sont jamais inventés.

Les validations impossibles sont explicitement signalées.

La revue de sécurité vérifie les contournements serveur.

La revue de données vérifie migrations, backup et rollback.

La revue du générateur vérifie un projet réellement produit
lorsque le changement le nécessite.

La revue CI vérifie triggers, permissions, secrets et gates.

La revue opérationnelle interdit les actions destructives implicites.

Les recommandations finales sont refused,
changes-required, acceptable-with-reservations
ou ready-to-merge.

Un score numérique reste facultatif
et ne remplace jamais les findings.

Les corrections sont vérifiées finding par finding.

Un finding devient verified uniquement après preuve.

Une seconde revue recherche également les régressions
introduites par les corrections.

La décision de merge, release ou déploiement reste humaine.

Codex ne peut pas déclarer une revue positive
sans avoir examiné et vérifié les éléments obligatoires.
```

---

# 81. Règles pour Codex

## 81.1 Lecture préalable

Avant une revue, Codex doit consulter :

```text
AGENTS.md
28-codex-operating-guide.md
29-codex-task-specification-template.md
30-codex-review-template.md
tâche concernée
documentation requise par la tâche
```

Ainsi que :

- diff ;
- code environnant ;
- tests ;
- compte-rendu d’implémentation ;
- migration éventuelle ;
- workflows éventuels.

---

## 81.2 Ce que Codex peut faire

Codex peut :

- inspecter le repository ;
- comparer le diff ;
- exécuter le build ;
- exécuter les tests ;
- exécuter les scans disponibles ;
- générer un projet de test ;
- produire des findings ;
- proposer une sévérité ;
- recommander une correction ;
- produire une recommandation finale.

---

## 81.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- modifier le code pendant une revue sans demande ;
- fusionner la pull request ;
- accepter automatiquement l’implémentation ;
- inventer un résultat de test ;
- masquer un finding ;
- augmenter une sévérité pour imposer une préférence ;
- considérer une suggestion comme bloquante ;
- exécuter une production ;
- appliquer une migration production ;
- afficher un secret ;
- communiquer au client ;
- clôturer un incident ;
- déclarer un restore test réussi sans l’avoir exécuté ;
- déclarer un transfert client terminé sur la seule base des documents.

---

## 81.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- acceptation ou refus d’un finding contesté ;
- report d’un finding majeur ;
- dérogation de sécurité ;
- acceptation d’une migration destructive ;
- acceptation d’un breaking change ;
- merge ;
- release ;
- déploiement ;
- restauration ;
- clôture de service ;
- acceptation avec réserves.

---

## 81.5 Compte-rendu attendu

Le rapport doit préciser :

```text
Tâche examinée
Référence examinée
Base de comparaison
Critères vérifiés
Commandes exécutées
Résultats
Findings
Sévérités
Éléments non examinés
Risques résiduels
Recommandation finale
Prochaine étape
```

---

# 82. Anti-patterns interdits

## 82.1 Revue basée uniquement sur le compte-rendu

Le code et les tests ne sont pas inspectés.

---

## 82.2 Note sans findings

```text
9/10, tout est bon.
```

sans preuves ni matrice de critères.

---

## 82.3 Finding de préférence

```text
Utiliser une autre bibliothèque car je la préfère.
```

---

## 82.4 Sévérité excessive

Une amélioration de nommage est classée bloquante.

---

## 82.5 Finding sans emplacement

Le développeur ne peut pas identifier le code concerné.

---

## 82.6 Finding sans impact

Le rapport indique uniquement que le code n’est pas idéal.

---

## 82.7 Test annoncé sans exécution

Le rapport reprend les chiffres de l’implémenteur comme preuve indépendante.

---

## 82.8 Revue transformée en refactoring

Le reviewer modifie toute l’architecture.

---

## 82.9 Problème hors périmètre confondu avec une régression

Une dette ancienne dégrade injustement la recommandation.

---

## 82.10 Suggestion présentée comme obligatoire

Une optimisation non mesurée bloque le merge.

---

## 82.11 Finding rouvert sans nouvel élément

Un finding rejeté est présenté de nouveau sous un autre nom.

---

## 82.12 Réserve sans échéance

Un finding majeur est reporté indéfiniment.

---

## 82.13 Sécurité limitée à l’UI

Le bouton est masqué, mais l’endpoint n’est pas testé.

---

## 82.14 Migration vérifiée uniquement visuellement

Aucun test PostgreSQL n’est exécuté.

---

## 82.15 `ready-to-merge` avec un bloquant

La recommandation contredit les findings.

---

# 83. Roadmap de mise en place

## Phase revue 1 — Format principal

```text
review ID
findings
severity
evidence
recommendation
```

---

## Phase revue 2 — Matrice des critères

```text
acceptance criteria
status
proof
comments
```

---

## Phase revue 3 — Profils spécialisés

```text
security
data
generation
CI
operations
documentation
```

---

## Phase revue 4 — Seconde revue

```text
accepted findings
fix verification
regression search
final recommendation
```

---

## Phase revue 5 — Automatisation

```text
front matter validation
finding IDs
required sections
links
status consistency
```

---

## Phase revue 6 — Mesure de qualité

```text
findings by severity
reopened findings
defects after merge
review duration
correction cycles
```

---

# 84. Checklist avant démarrage de revue

```text
[ ] Review ID attribué
[ ] Tâche disponible
[ ] Critères disponibles
[ ] Diff identifiable
[ ] Base identifiable
[ ] Commit identifiable
[ ] Compte-rendu disponible
[ ] Documents disponibles
[ ] Tests existants disponibles
[ ] Périmètre de revue défini
```

---

# 85. Checklist avant publication du rapport

```text
[ ] Tous les critères examinés
[ ] Sévérités cohérentes
[ ] Findings sans doublon
[ ] Findings avec preuve
[ ] Emplacements précis
[ ] Tests réellement exécutés
[ ] Tests non exécutés signalés
[ ] Risques résiduels signalés
[ ] Observations hors périmètre séparées
[ ] Recommandation cohérente
[ ] Aucun secret
[ ] Aucune donnée personnelle
```

---

# 86. Checklist de seconde revue

```text
[ ] Revue initiale relue
[ ] Findings acceptés identifiés
[ ] Commit de correction identifié
[ ] Diff de correction inspecté
[ ] Chaque finding vérifié
[ ] Tests de régression exécutés
[ ] Build exécuté
[ ] Régressions recherchées
[ ] Nouveaux findings identifiés séparément
[ ] Recommandation mise à jour
```

---

# 87. Conclusion

Une revue Codex de qualité doit transformer une impression générale en constats vérifiables.

Elle repose sur :

```text
tâche
+ critères
+ diff
+ inspection
+ exécution
+ preuves
+ sévérités
+ recommandation
```

Une bonne revue ne cherche pas à démontrer que le reviewer aurait implémenté autrement.

Elle cherche à déterminer si l’implémentation actuelle :

- répond au besoin ;
- respecte les décisions ;
- protège le système ;
- possède les preuves attendues ;
- peut être corrigée de manière ciblée.

> Une revue utile ne dit pas seulement qu’un problème existe. Elle montre où il se trouve, pourquoi il compte, comment le reproduire et ce qui doit être vérifié après sa correction.
