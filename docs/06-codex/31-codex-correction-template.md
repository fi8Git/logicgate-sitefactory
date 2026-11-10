# LogicGate SiteFactory — Modèle de correction après revue Codex

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Modèle de correction après revue Codex |
| Version | 1.0 |
| Statut | Guide public ; exemples pédagogiques non exécutés |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, contributeurs, reviewers, Codex |
| Type de document | Standard de correction et template réutilisable |
| Emplacement recommandé | `docs/06-codex/corrections/` |
| Documents parents | `28-codex-operating-guide.md`, `29-codex-task-specification-template.md`, `30-codex-review-template.md`, `AGENTS.md` |
| Documents associés | Tâche initiale, revue initiale, documentation du domaine concerné, tests et seconde revue |
| Dernière mise à jour | 2026-10-21 |

---

## Corrections du périmètre public

Ce modèle complète [AGENTS.md](../../AGENTS.md), le
[guide opérationnel](28-codex-operating-guide.md) et le
[modèle de revue](30-codex-review-template.md).
Une correction exige des findings et un périmètre explicitement acceptés ;
elle n'autorise ni refactoring opportuniste, extension de dépendances, changement
des critères, classification/allowlist/SHA/licence ou opération de production.

Les tâches, tests et résultats exemplaires ci-dessous sont **pédagogiques,
fictifs**, pas des preuves acquises ni une autorisation d'exécution.
Le scénario de slug à 48 caractères n'est pas la règle structurelle publique
actuelle de 3–80 caractères ; l'exemple ne change aucun contrat existant.
Les sources, dépendances et commandes doivent être inspectées réellement ;
une correction publique ne requiert jamais de code, binaire ou secret Business.

La revue initiale et les échecs restent conservés. Chaque finding possède une
preuve avant/après et une validation proportionnée au périmètre autorisé ;
une absence de reproduction ne vaut pas correction causale.
P03-F1 reste OPEN / UNKNOWN, 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve ; réexamen **2026-10-23**
sans prolongation tacite. Aucune campagne positive ne le clôture.

Les contrôles de sécurité, secrets, régression, annulation, indépendance de
la re-review et validation humaine ne sont pas assouplis.
Aucun package publié, CI distante validée, déploiement, migration production
ou licence automatique des sorties générées n'est prétendu par ces procédures.

---

## 1. Objet du document

Ce document définit le format standard des corrections réalisées après une revue Codex dans le repository LogicGate SiteFactory.

Il précise notamment :

- les préconditions d’une correction ;
- les informations nécessaires ;
- la sélection des findings à corriger ;
- la distinction entre findings acceptés, rejetés et reportés ;
- le périmètre autorisé ;
- la gestion des nouveaux problèmes découverts ;
- l’ordre de traitement des findings ;
- la traçabilité entre revue, correction et seconde revue ;
- les règles de modification du code ;
- les tests de régression ;
- les tests existants à réexécuter ;
- la gestion des migrations ;
- la gestion des contrats publics ;
- la mise à jour de la documentation ;
- le format du compte-rendu de correction ;
- les critères de seconde livraison ;
- les conditions d’arrêt ;
- les quality gates ;
- les opérations interdites ;
- les contraintes applicables à Codex.

L’objectif est d’éviter qu’une phase de correction devienne :

```text
une nouvelle implémentation non cadrée
```

ou :

```text
une réécriture complète motivée par quelques findings ciblés
```

---

## 2. Vision

Une correction après revue doit traiter précisément les écarts reconnus sans déstabiliser le reste de l’implémentation.

Le flux de référence est :

```text
Revue terminée
→ triage humain des findings
→ findings acceptés
→ spécification de correction
→ analyse ciblée
→ corrections limitées
→ tests de régression
→ validation globale adaptée
→ compte-rendu finding par finding
→ seconde revue
→ décision humaine
```

La correction ne doit pas fonctionner selon le modèle suivant :

```text
Revue reçue
→ tous les commentaires appliqués sans analyse
→ refactoring étendu
→ nouveaux comportements
→ tests historiques cassés
→ déclaration de conformité
```

---

## 3. Principes fondamentaux

## 3.1 Seuls les findings validés sont corrigés

Un finding produit par une revue ne devient pas automatiquement une instruction d’implémentation.

Chaque finding doit être trié comme :

```text
accepted
rejected
deferred
clarification-required
```

Codex corrige uniquement les findings :

```text
accepted
```

et les éléments indispensables à leur validation.

---

## 3.2 La revue originale reste immuable

La revue initiale ne doit pas être réécrite pour faire disparaître :

- un finding ;
- une preuve ;
- une erreur initiale ;
- une recommandation négative ;
- une limitation.

Les évolutions sont suivies dans :

- le document de correction ;
- le compte-rendu de correction ;
- la seconde revue.

---

## 3.3 Une correction possède un périmètre limité

Le périmètre de correction est défini par :

```text
findings acceptés
+ tests nécessaires
+ documentation directement affectée
+ corrections indispensables à la cohérence
```

Il n’inclut pas automatiquement :

- toutes les suggestions optionnelles ;
- tous les problèmes préexistants ;
- un refactoring global ;
- une nouvelle fonctionnalité ;
- un changement d’architecture.

---

## 3.4 Chaque finding est traité séparément

Le compte-rendu doit permettre de répondre à :

```text
Quel finding a été corrigé ?

Quel fichier a été modifié ?

Quel comportement a changé ?

Quel test prouve la correction ?

Quel résultat a été obtenu ?
```

---

## 3.5 Un finding corrigé doit être prouvé

La déclaration :

```text
Corrigé.
```

ne suffit pas.

La preuve peut être :

- test de régression ;
- test d’intégration ;
- test HTTP ;
- test PostgreSQL ;
- build ;
- scan ;
- inspection de rapport ;
- génération réelle ;
- validation de schéma.

---

## 3.6 La correction ne doit pas masquer le symptôme

Exemples interdits :

```text
Supprimer le test qui expose le défaut

Modifier le message sans corriger la règle

Désactiver l’analyseur

Ajouter continue-on-error

Rendre l’erreur non bloquante

Ignorer l’exception
```

La cause doit être corrigée lorsque le finding l’exige.

---

## 3.7 Les régressions introduites restent de nouveaux défauts

Une correction peut créer un nouveau problème.

Ce problème doit :

- être signalé ;
- recevoir un nouvel identifiant de finding lors de la seconde revue ;
- ne pas être masqué sous un ancien finding ;
- être corrigé avant validation lorsqu’il est bloquant ou majeur.

---

## 3.8 La seconde revue reste indépendante

L’implémenteur peut déclarer un finding :

```text
fixed
```

Seul le reviewer peut le déclarer :

```text
verified
```

après vérification.

---

# 4. Non-objectifs

Le modèle de correction ne doit pas :

- remplacer la tâche initiale ;
- remplacer la revue ;
- permettre d’appliquer tous les commentaires sans triage ;
- transformer une correction en refonte ;
- modifier la production ;
- appliquer une migration de production ;
- clôturer automatiquement les findings ;
- déclarer une seconde revue réussie ;
- supprimer l’historique d’un défaut ;
- accepter implicitement une suggestion optionnelle ;
- permettre à Codex de décider seul du report d’un finding majeur ;
- modifier les critères d’acceptation rétroactivement.

---

# 5. Terminologie

## 5.1 Correction ID

Identifiant stable de la spécification de correction.

Exemple :

```text
SF-CORRECTION-012
```

---

## 5.2 Source review

Revue ayant produit les findings à traiter.

Exemple :

```text
SF-REVIEW-012
```

---

## 5.3 Correction set

Ensemble des findings acceptés traités dans une même correction.

---

## 5.4 Accepted finding

Finding reconnu comme valide et devant être corrigé.

---

## 5.5 Rejected finding

Finding considéré comme incorrect, non applicable ou fondé sur une mauvaise interprétation.

---

## 5.6 Deferred finding

Finding valide dont la correction est reportée par décision humaine.

---

## 5.7 Clarification required

Finding nécessitant une décision ou information supplémentaire avant traitement.

---

## 5.8 Fixed

Statut déclaré par l’implémenteur lorsque la correction est prête à être revue.

---

## 5.9 Verified

Statut déclaré par le reviewer après validation réelle.

---

## 5.10 Regression test

Test démontrant que le défaut initial est corrigé et ne pourra pas revenir silencieusement.

---

## 5.11 Correction baseline

Commit ou référence correspondant à l’implémentation examinée par la revue initiale.

---

## 5.12 Correction commit

Commit ou ensemble de commits contenant les corrections.

---

## 5.13 Re-review

Seconde revue vérifiant les corrections et les éventuelles régressions.

---

# 6. Emplacement des corrections

Structure recommandée :

```text
docs/
└── 06-codex/
    ├── tasks/
    ├── reviews/
    └── corrections/
        ├── active/
        ├── completed/
        └── archived/
```

Une structure simplifiée peut être utilisée pendant le bootstrap :

```text
docs/06-codex/corrections/
```

---

# 7. Convention de nommage

Format recommandé :

```text
{correction-id}-{review-id}-{short-title}.md
```

Exemple :

```text
SF-CORRECTION-012-SF-REVIEW-012-client-slug-findings.md
```

---

## 7.1 Corrections successives

Si plusieurs cycles de correction sont nécessaires :

```text
SF-CORRECTION-012-R1-...
SF-CORRECTION-012-R2-...
```

ou un nouveau Correction ID peut être attribué.

La convention choisie doit rester cohérente.

---

# 8. Identifiants

## 8.1 Format principal

```text
SF-CORRECTION-{NUMBER}
```

---

## 8.2 Findings

Les findings conservent leurs identifiants d’origine.

Exemple :

```text
SF-REVIEW-012-F01
SF-REVIEW-012-F02
```

---

## 8.3 Nouveau problème

Un nouveau défaut découvert pendant la correction ne réutilise pas un ancien Finding ID.

Il est :

- signalé dans le compte-rendu ;
- ou attribué lors de la seconde revue.

---

# 9. Statuts d’une correction

Valeurs recommandées :

```text
draft
ready
in-progress
blocked
ready-for-re-review
completed
cancelled
superseded
```

---

## 9.1 `draft`

La sélection des findings ou le périmètre n’est pas encore validé.

---

## 9.2 `ready`

Les findings acceptés et les décisions nécessaires sont connus.

---

## 9.3 `in-progress`

Les corrections sont en cours.

---

## 9.4 `blocked`

Une décision ou dépendance empêche de poursuivre.

---

## 9.5 `ready-for-re-review`

Les corrections sont implémentées et les validations prévues ont été exécutées.

---

## 9.6 `completed`

La seconde revue a vérifié les corrections et la décision humaine de clôture est prise.

Codex ne positionne pas seul ce statut.

---

## 9.7 `cancelled`

La correction n’est plus requise.

---

## 9.8 `superseded`

La correction est remplacée par une autre tâche ou une nouvelle implémentation.

---

# 10. Front matter recommandé

```yaml
---
correctionId: SF-CORRECTION-012
taskId: SF-TASK-012
sourceReviewId: SF-REVIEW-012
title: Correct client slug review findings
status: ready
iteration: 1
priority: high
risk: medium
owner: LogicGate
implementer: Codex
reviewer: unassigned
baselineRef: abc1234
correctionRef: null
createdAt: 2026-07-28
targetVersion: 0.2.0
acceptedFindings:
  - SF-REVIEW-012-F01
  - SF-REVIEW-012-F02
rejectedFindings: []
deferredFindings: []
---
```

---

## 10.1 Données interdites

Le front matter ne doit pas contenir :

- secret ;
- donnée personnelle ;
- contenu client ;
- token ;
- URL avec credentials ;
- mot de passe.

---

# 11. Préconditions

Une correction ne peut passer à `ready` que si :

```text
[ ] Revue initiale terminée
[ ] Findings triés
[ ] Findings acceptés identifiés
[ ] Findings rejetés justifiés
[ ] Findings reportés validés humainement
[ ] Baseline identifiée
[ ] Périmètre défini
[ ] Critères de correction définis
[ ] Tests attendus définis
[ ] Aucun blocage non résolu
```

---

# 12. Références obligatoires

La spécification de correction doit référencer :

```text
Tâche initiale
Revue initiale
Compte-rendu d’implémentation initial
Branche ou commit initial
Findings acceptés
Documentation concernée
```

---

## 12.1 Documents requis

Codex doit relire au minimum :

```text
AGENTS.md
28-codex-operating-guide.md
29-codex-task-specification-template.md
30-codex-review-template.md
31-codex-correction-template.md
tâche initiale
revue initiale
```

Ainsi que les documents métier ou techniques affectés.

---

# 13. Triage des findings

Avant la correction, chaque finding reçoit une décision.

| Finding | Décision | Justification | Propriétaire |
|---|---|---|---|
| `F01` | `accepted` | Contrat non respecté | LogicGate |
| `F02` | `rejected` | Comportement volontaire documenté | LogicGate |
| `F03` | `deferred` | Correction planifiée en `SF-TASK-045` | LogicGate |
| `F04` | `clarification-required` | Décision produit manquante | LogicGate |

---

## 13.1 Décision explicite

L’absence de décision ne signifie pas :

```text
accepted
```

---

## 13.2 Finding bloquant

Un finding bloquant accepté doit être corrigé avant merge.

Il ne peut être reporté que par une dérogation humaine explicite et documentée.

---

## 13.3 Finding majeur

Un finding majeur est normalement corrigé avant merge.

Un report nécessite :

- justification ;
- risque ;
- mesure compensatoire ;
- propriétaire ;
- échéance.

---

## 13.4 Finding mineur

Il peut être :

- corrigé dans le cycle ;
- ou reporté par décision humaine.

---

## 13.5 Amélioration optionnelle

Elle n’est traitée que si elle est explicitement ajoutée au périmètre.

---

# 14. Findings rejetés

Un finding rejeté doit conserver une justification.

Exemples de justification :

```text
Le comportement est conforme à la tâche.

La preuve était incorrecte.

Le code concerné est hors périmètre et préexistant.

Le point est une préférence de style.

Le risque est déjà couvert par un autre contrôle.
```

---

## 14.1 Codex

Codex ne doit pas appliquer un finding rejeté.

---

## 14.2 Seconde revue

Le reviewer ne doit pas rouvrir un finding rejeté sans nouvel élément concret.

---

# 15. Findings reportés

Un finding reporté doit référencer :

- une nouvelle tâche ;
- une issue ;
- une échéance ;
- un propriétaire ;
- une mesure compensatoire.

Exemple :

```text
Deferred to:
SF-TASK-045 — Replace deprecated deployment script

Owner:
LogicGate

Target:
0.4.0
```

---

## 15.1 Pas de report silencieux

Un finding ne doit pas disparaître uniquement parce qu’il n’a pas été corrigé.

---

# 16. Dépendances entre findings

Certains findings peuvent partager une cause.

Exemple :

```text
F01 — Validation incorrecte
F02 — Test de régression absent
```

La correction peut traiter les deux ensemble, mais le compte-rendu doit fournir une preuve pour chacun.

---

## 16.1 Ordre

Ordre recommandé :

```text
1. Bloquants de sécurité
2. Bloquants de données
3. Autres bloquants
4. Findings majeurs
5. Findings mineurs acceptés
6. Améliorations optionnelles ajoutées explicitement
```

---

# 17. Résumé de correction

Le résumé doit expliquer :

- le nombre de findings acceptés ;
- leur nature ;
- le résultat attendu ;
- l’absence d’extension du périmètre.

Exemple :

```text
Corriger les deux findings majeurs de `SF-REVIEW-012` :
le format du slug doit refuser les doubles tirets et un test
d’intégration doit démontrer qu’aucun fichier n’est écrit après
une validation en échec.
```

---

# 18. Objectif

L’objectif principal d’une correction est :

```text
rendre l’implémentation conforme aux findings acceptés
sans modifier les autres comportements validés
```

---

# 19. Périmètre

Le périmètre doit lister :

- findings acceptés ;
- fichiers ou composants concernés ;
- tests ;
- documentation ;
- rapports éventuels.

Exemple :

```text
- `SF-REVIEW-012-F01`
- `SF-REVIEW-012-F02`
- Validation `ClientSlug`
- Tests Domain
- Test d’intégration du pipeline
- Documentation du comportement
```

---

# 20. Hors périmètre

Le hors périmètre doit rappeler notamment :

```text
- Findings rejetés
- Findings reportés
- Améliorations optionnelles non retenues
- Refactoring général
- Nouvelle fonctionnalité
- Migration de production
- Déploiement
```

---

# 21. Comportements à préserver

La correction doit identifier les comportements déjà validés qui ne doivent pas changer.

Exemple :

```text
- Les slugs valides existants restent acceptés.
- Le code d’erreur reste `SF-CONFIG-012`.
- Le CLI conserve son format JSON.
- Aucun package externe n’est ajouté.
- Domain reste indépendant du parser YAML.
```

---

## 21.1 Importance

Cette section protège contre les régressions produites par une correction trop large.

---

# 22. Analyse préalable

Avant modification, Codex doit :

1. relire le finding ;
2. vérifier sa preuve ;
3. reproduire le défaut lorsque possible ;
4. identifier la cause ;
5. identifier les tests concernés ;
6. identifier les comportements à préserver ;
7. préparer une correction limitée.

---

## 22.1 Reproduction impossible

Codex doit signaler :

- pourquoi ;
- ce qui a été inspecté ;
- les preuves encore disponibles ;
- la décision nécessaire.

Il ne doit pas corriger au hasard.

---

# 23. Plan de correction

Le plan doit être structuré finding par finding.

Exemple :

```text
1. F01 — Corriger la validation des tirets consécutifs.
2. F01 — Ajouter le test unitaire de régression.
3. F02 — Ajouter un test pipeline avec dossier temporaire.
4. Réexécuter les tests ciblés.
5. Réexécuter le build et la suite adaptée.
6. Mettre à jour le compte-rendu.
```

---

# 24. Diff minimal

## 24.1 Principe

Une correction doit produire le plus petit diff sûr permettant de satisfaire les findings acceptés.

---

## 24.2 Modifications opportunistes

Sont interdites sans validation :

- renommage large ;
- déplacement de dossiers ;
- changement de bibliothèque ;
- nouveau framework ;
- réécriture complète ;
- formatage global ;
- modification de contrats non concernés.

---

## 24.3 Correction nécessitant un refactoring

Si un refactoring plus large devient nécessaire :

1. expliquer pourquoi ;
2. décrire les alternatives ;
3. analyser les risques ;
4. demander une décision ;
5. créer éventuellement une tâche séparée.

---

# 25. Gestion des conflits avec la tâche initiale

Si un finding demande une correction contredisant la tâche initiale :

- ne pas appliquer silencieusement ;
- identifier la contradiction ;
- demander une décision ;
- corriger la tâche, la revue ou le finding selon la décision humaine.

---

# 26. Gestion des conflits documentaires

Si la revue s’appuie sur une documentation obsolète :

- signaler la source propriétaire ;
- conserver le finding ouvert jusqu’à décision ;
- ne pas modifier le code uniquement pour suivre un document dépassé.

---

# 27. Nouveaux problèmes découverts

Pendant une correction, Codex peut découvrir :

- un défaut connexe ;
- une dette préexistante ;
- un risque de sécurité ;
- une incompatibilité ;
- une migration nécessaire.

---

## 27.1 Problème indispensable à la correction

S’il empêche une correction sûre :

- arrêter ;
- expliquer ;
- demander une décision ;
- étendre formellement le périmètre ou créer une nouvelle tâche.

---

## 27.2 Problème non bloquant

Le signaler dans :

```text
Observations hors périmètre
```

Ne pas le corriger silencieusement.

---

## 27.3 Nouveau défaut introduit

Le corriger avant livraison s’il résulte directement des modifications en cours.

---

# 28. Règles d’architecture

La correction doit préserver les règles applicables :

```text
Domain ne dépend pas d’Infrastructure.

Application ne dépend pas du CLI.

Le CLI ne contient pas la logique métier.

Les modules ne dépendent pas d’un projet client.

Les personnalisations restent isolées.

Les migrations appartiennent au repository client.
```

---

## 28.1 Correction d’un finding d’architecture

La correction doit idéalement ajouter ou renforcer un test d’architecture.

---

# 29. Règles de sécurité

La correction doit respecter :

```text
18-security-baseline.md
```

---

## 29.1 Finding de sécurité

Une correction de sécurité doit inclure :

- scénario d’attaque ;
- cause ;
- contrôle ajouté ou corrigé ;
- test de contournement ;
- absence de régression ;
- analyse des versions affectées si applicable.

---

## 29.2 Interdictions

Codex ne doit pas :

- désactiver un contrôle ;
- élargir CORS ;
- ajouter un secret ;
- logguer une PII ;
- rendre un endpoint public ;
- supprimer un scan ;
- réduire un rate limit sans analyse.

---

# 30. Données et migrations

## 30.1 Finding données

Une correction affectant les données doit préciser :

```text
Modèle affecté
Migration requise
Données existantes
Opérations destructives
Backup
Rollback
```

---

## 30.2 Migration découverte pendant correction

Si une migration non prévue devient nécessaire :

- arrêter si elle est structurante ou destructive ;
- demander une décision ;
- préparer une tâche de migration distincte si nécessaire.

---

## 30.3 Production

La correction ne doit pas appliquer de migration en production.

---

## 30.4 Test PostgreSQL

Un finding spécifique à PostgreSQL doit être vérifié avec PostgreSQL réel.

---

# 31. Contrats publics

Une correction peut affecter :

- configuration YAML ;
- schéma JSON ;
- CLI ;
- codes d’erreur ;
- permissions ;
- routes ;
- manifests ;
- lock file ;
- rapports ;
- interfaces.

---

## 31.1 Compatibilité

La correction doit préciser :

```text
Backward compatible: yes/no
Breaking change: yes/no
Version impact: patch/minor/major
```

---

## 31.2 Finding sur contrat

La documentation et les tests de contrat doivent être mis à jour.

---

# 32. Tests de régression

## 32.1 Obligation

Chaque finding fonctionnel confirmé doit idéalement produire un test reproduisant le problème.

---

## 32.2 Test avant correction

Lorsque le processus le permet :

```text
test échoue avant correction
→ correction
→ test réussit
```

---

## 32.3 Nom du test

Le test doit identifier le scénario.

Exemple :

```csharp
TryCreate_WhenSlugContainsConsecutiveHyphens_ReturnsInvalid()
```

---

## 32.4 Test insuffisant

Éviter :

```csharp
Assert.NotNull(result);
```

lorsque le contrat porte sur :

- erreur ;
- code ;
- absence d’écriture ;
- refus d’accès ;
- état persistant.

---

# 33. Réexécution des tests existants

Une correction doit réexécuter :

- tests ciblés du finding ;
- tests du composant ;
- tests de la couche ;
- suite globale adaptée au risque.

---

## 33.1 Correction à faible risque

Peut commencer par :

```text
tests ciblés
+ build Release
+ suite du projet concerné
```

---

## 33.2 Correction à risque élevé

Doit ajouter selon le contexte :

```text
tests PostgreSQL
tests sécurité
projet généré
Docker
Windows
Linux
scans
```

---

# 34. Tests des critères initiaux

La correction doit vérifier que les critères déjà passés dans la revue initiale restent satisfaits.

Elle ne doit pas uniquement tester les findings corrigés.

---

# 35. Tests de contournement

Pour une correction de sécurité :

```text
UI protégée
+ endpoint direct refusé
+ service protégé
```

La preuve ne doit pas reposer uniquement sur un bouton masqué.

---

# 36. Tests de non-écrasement

Pour une correction du générateur, vérifier selon le périmètre :

```text
generated
create-once
merge
custom
protected
```

Une correction ne doit pas fragiliser les protections de fichiers.

---

# 37. Tests d’idempotence

Lorsque le finding concerne :

- retry ;
- génération ;
- email ;
- migration ;
- job ;
- commande ;

la correction doit vérifier l’idempotence si elle appartient au contrat.

---

# 38. Documentation

Une correction doit mettre à jour la documentation lorsque :

- le comportement change ;
- un contrat est précisé ;
- une erreur est ajoutée ;
- une limitation disparaît ;
- une procédure est corrigée ;
- la revue a révélé une contradiction.

---

## 38.1 Revue originale

Ne pas modifier la revue originale pour marquer les findings comme corrigés.

Utiliser :

- compte-rendu de correction ;
- seconde revue.

---

## 38.2 Tâche initiale

Ne pas réécrire la tâche initiale pour rendre l’implémentation conforme rétroactivement.

Une clarification peut être ajoutée sous forme d’addendum daté si une décision humaine a réellement changé.

---

# 39. Changelog

Une correction destinée à une release peut nécessiter une entrée :

```text
Fixed
Security
```

sous :

```text
Unreleased
```

---

## 39.1 Finding documentaire uniquement

Une correction purement documentaire peut ne pas nécessiter de changelog applicatif.

La raison doit être indiquée.

---

# 40. Git

## 40.1 Baseline

Avant correction, relever :

```text
branche
baseline commit
working tree
modifications préexistantes
```

---

## 40.2 Branche

La correction est généralement appliquée sur la branche de la tâche ou de la pull request existante.

Une branche dédiée peut être utilisée si :

- l’implémentation initiale a déjà été fusionnée ;
- le correctif est séparé ;
- le finding concerne une release antérieure.

---

## 40.3 Commit

Convention possible :

```text
fix: address SF-REVIEW-012 findings
```

ou plusieurs commits ciblés :

```text
fix: reject consecutive hyphens in client slugs

test: verify invalid configuration creates no files
```

Codex ne crée pas de commit sans demande explicite.

---

## 40.4 Modifications préexistantes

Codex ne doit pas annuler ou réécrire les changements de l’utilisateur.

---

# 41. Compte-rendu finding par finding

Le compte-rendu doit contenir une matrice.

| Finding | Décision | Correction | Test | Résultat déclaré |
|---|---|---|---|---|
| `F01` | Accepted | Validation modifiée | Test Domain | Fixed |
| `F02` | Accepted | Test pipeline ajouté | Test Integration | Fixed |
| `F03` | Rejected | Aucune | — | Not applied |
| `F04` | Deferred | `SF-TASK-045` | — | Deferred |

---

## 41.1 Statut déclaré

L’implémenteur utilise :

```text
fixed
not-fixed
not-applicable
blocked
```

Il ne doit pas utiliser :

```text
verified
```

---

# 42. Compte-rendu des commandes

Pour chaque commande :

```text
Commande
Résultat
Warnings
Erreurs
Durée éventuelle
```

---

## 42.1 Résultats exacts

Exemple :

```text
Build Release :
0 warning
0 erreur

Tests :
Total   : 46
Réussis : 46
Échecs  : 0
Ignorés : 0
```

---

## 42.2 Commande non exécutée

Préciser :

- raison ;
- impact ;
- validation restante.

---

# 43. Revue avant seconde livraison

Avant `ready-for-re-review`, Codex doit examiner :

```text
[ ] Tous les findings acceptés traités
[ ] Aucun finding rejeté appliqué
[ ] Findings reportés documentés
[ ] Diff limité
[ ] Tests de régression ajoutés
[ ] Critères initiaux préservés
[ ] Build réussi
[ ] Tests réussis
[ ] Documentation mise à jour
[ ] Aucun secret
[ ] Aucun warning injustifié
[ ] Compte-rendu complet
```

---

# 44. Seconde livraison

La seconde livraison comprend :

```text
Code corrigé
Tests de régression
Documentation mise à jour
Compte-rendu de correction
Référence du commit de correction
Commandes et résultats
Observations hors périmètre
```

---

# 45. Seconde revue

La seconde revue suit :

```text
30-codex-review-template.md
```

Elle vérifie :

1. chaque finding accepté ;
2. les tests de régression ;
3. les comportements initialement valides ;
4. les nouveaux changements ;
5. les nouvelles régressions ;
6. la cohérence du compte-rendu.

---

## 45.1 Résultats possibles

Pour chaque finding :

```text
verified
partially-fixed
not-fixed
not-applicable
```

---

## 45.2 Nouveaux findings

Ils reçoivent de nouveaux identifiants.

---

# 46. Correction partielle

Une correction partielle ne doit pas être présentée comme complète.

Exemple :

```text
F01 :
cause corrigée, mais test de contournement absent

Status:
partially-fixed
```

---

# 47. Finding impossible à corriger dans le périmètre

Si la correction exige :

- changement d’architecture ;
- migration majeure ;
- nouveau fournisseur ;
- modification produit ;
- changement commercial ;

Codex doit :

1. arrêter ;
2. expliquer ;
3. proposer une nouvelle tâche ;
4. conserver le finding ouvert.

---

# 48. Conditions d’arrêt

Codex doit suspendre la correction lorsque :

```text
Finding ambigu

Décision humaine manquante

Contradiction avec la tâche initiale

Migration destructive découverte

Dépendance structurante nécessaire

Accès production requis

Secret réel requis

Périmètre devenu trop large

Correction affectant un prix ou entitlement

Risque de perte de données

Régression majeure non résolue
```

---

# 49. Décisions humaines requises

Nécessitent une validation :

- acceptation ou rejet d’un finding ;
- report d’un finding ;
- modification de la tâche ;
- extension du périmètre ;
- nouveau package ;
- migration destructive ;
- breaking change ;
- modification de prix ;
- modification d’entitlement ;
- dérogation de sécurité ;
- acceptation avec réserves ;
- fusion ;
- release ;
- déploiement.

---

# 50. Template complet de correction

```markdown
---
correctionId: SF-CORRECTION-XXX
taskId: SF-TASK-XXX
sourceReviewId: SF-REVIEW-XXX
title: Correct review findings
status: ready
iteration: 1
priority: high
risk: medium
owner: LogicGate
implementer: Codex
reviewer: unassigned
baselineRef: null
correctionRef: null
createdAt: YYYY-MM-DD
targetVersion: null
acceptedFindings: []
rejectedFindings: []
deferredFindings: []
---

# SF-CORRECTION-XXX — Correct review findings

## 1. Résumé

Résumer les findings acceptés et le résultat attendu.

## 2. Références

- Tâche initiale :
- Revue initiale :
- Compte-rendu initial :
- Baseline commit :
- Branche :

## 3. Triage des findings

| Finding | Décision | Justification |
|---|---|---|
| SF-REVIEW-XXX-F01 | accepted | |
| SF-REVIEW-XXX-F02 | rejected | |
| SF-REVIEW-XXX-F03 | deferred | |

## 4. Objectif

Rendre l’implémentation conforme aux findings acceptés
sans modifier les autres comportements validés.

## 5. Périmètre

- Finding accepté
- Fichier ou composant
- Test
- Documentation

## 6. Hors périmètre

- Findings rejetés
- Findings reportés
- Refactoring global
- Nouvelle fonctionnalité
- Production

## 7. Comportements à préserver

- Comportement
- Contrat
- Code d’erreur
- Format
- Compatibilité

## 8. Analyse des findings

### Finding SF-REVIEW-XXX-F01

- Cause :
- Comportement actuel :
- Comportement attendu :
- Correction prévue :
- Test prévu :

### Finding SF-REVIEW-XXX-F02

- Cause :
- Correction prévue :
- Test prévu :

## 9. Plan de correction

1. Étape
2. Étape
3. Validation

## 10. Contraintes d’architecture

- Respecter les couches existantes.
- Ne pas déplacer la logique métier dans le CLI.
- Ne pas introduire de dépendance circulaire.

## 11. Contraintes de sécurité

- Aucun secret.
- Aucune PII dans les logs.
- Refus par défaut.
- Ajouter les tests de contournement nécessaires.

## 12. Données et migrations

- Migration requise :
- Données existantes affectées :
- Backup requis :
- Production : hors périmètre
- Rollback :

## 13. Contrats publics

- Contrats affectés :
- Breaking change :
- Impact de version :
- Documentation :

## 14. Tests de régression

### Finding F01

- Test :
- Résultat attendu :

### Finding F02

- Test :
- Résultat attendu :

## 15. Tests existants à réexécuter

- Tests ciblés
- Tests du composant
- Tests d’intégration
- Suite globale adaptée

## 16. Commandes de validation

```powershell
dotnet restore SiteFactory.slnx --locked-mode
```

```powershell
dotnet build SiteFactory.slnx  --configuration Release  --no-restore
```

```powershell
dotnet test SiteFactory.slnx  --configuration Release  --no-build --no-restore --filter "Category!=PostgreSql"
```

## 17. Documentation à mettre à jour

- Document
- Changelog
- Exemple

## 18. Risques

- Risque
- Mesure de réduction

## 19. Conditions d’arrêt

Arrêter et demander une décision si :

- condition ;
- condition.

## 20. Compte-rendu attendu

Le compte-rendu doit inclure :

- matrice finding par finding ;
- fichiers modifiés ;
- tests ajoutés ;
- commandes ;
- résultats ;
- documentation ;
- éléments non exécutés ;
- nouveaux risques ;
- décisions restantes.

## 21. Définition de Done

- [ ] Tous les findings acceptés sont traités.
- [ ] Aucun finding rejeté n’est appliqué.
- [ ] Les findings reportés sont tracés.
- [ ] Les tests de régression sont ajoutés.
- [ ] Les critères initiaux restent satisfaits.
- [ ] Le build Release réussit.
- [ ] Les tests réussissent.
- [ ] La documentation est à jour.
- [ ] Aucun secret n’est présent.
- [ ] Le compte-rendu est produit.
- [ ] La correction est prête pour seconde revue.
```

---

# 51. Template simplifié

Pour une correction `XS` ou `S` à faible risque :

```markdown
---
correctionId: SF-CORRECTION-XXX
taskId: SF-TASK-XXX
sourceReviewId: SF-REVIEW-XXX
status: ready
acceptedFindings:
  - SF-REVIEW-XXX-F01
---

# SF-CORRECTION-XXX — Short correction title

## Findings à corriger

- `SF-REVIEW-XXX-F01`

## Périmètre

## Hors périmètre

## Correction attendue

## Test de régression

## Commandes de validation

## Compte-rendu attendu
```

---

## 51.1 Cas interdits

Le template simplifié ne doit pas être utilisé pour :

- sécurité ;
- données ;
- migration ;
- Identity ;
- autorisation ;
- CI sensible ;
- génération structurante ;
- infrastructure ;
- release ;
- backup ;
- restauration.

---

# 52. Profil de correction — Sécurité

Une correction sécurité doit ajouter :

```text
Menace
Scénario d’exploitation
Version affectée
Contrôle actuel
Contrôle corrigé
Test de contournement
Backport éventuel
Mitigation temporaire
```

---

## 52.1 Exemple de critères

```text
- L’endpoint retourne 403 sans permission.
- Le bouton reste masqué.
- Un appel HTTP direct est refusé.
- Aucun détail sensible n’est loggué.
```

---

## 52.2 Confidentialité

Le rapport largement partagé peut limiter les détails d’exploitation.

---

# 53. Profil de correction — Données ou migration

Doit préciser :

```text
Modèle source
Modèle cible
Données existantes
Migration
Opérations destructives
Backup
Rollback
Tests PostgreSQL
```

---

## 53.1 Production

```text
Production execution: out of scope
```

reste obligatoire.

---

# 54. Profil de correction — Générateur

Doit vérifier :

```text
Plan
Stratégies de fichiers
Déterminisme
Non-écrasement
Lock file
Rapport
Projet généré
Restore
Build
Tests
```

---

# 55. Profil de correction — CI/CD

Doit préciser :

```text
Workflow
Trigger
Permissions
Secrets
Job
Gate
Action version
Runner
```

La correction doit être testée par :

- validation YAML ;
- pull request ;
- exécution réelle lorsque possible.

---

# 56. Profil de correction — Documentation

Une correction documentaire doit préciser :

- contradiction initiale ;
- source de vérité ;
- documents affectés ;
- liens ;
- exemples ;
- terminologie ;
- absence de modification du code.

---

# 57. Profil de correction — Interface utilisateur

Doit préciser :

- finding visuel ou fonctionnel ;
- largeur d’écran ;
- navigateur si pertinent ;
- état ;
- accessibilité ;
- test ou observation ;
- comportements à préserver.

---

# 58. Exemple complet

```markdown
---
correctionId: SF-CORRECTION-012
taskId: SF-TASK-012
sourceReviewId: SF-REVIEW-012
title: Correct client slug validation findings
status: ready
iteration: 1
priority: high
risk: medium
owner: LogicGate
implementer: Codex
reviewer: unassigned
baselineRef: abc1234
correctionRef: null
createdAt: 2026-07-28
targetVersion: 0.2.0
acceptedFindings:
  - SF-REVIEW-012-F01
  - SF-REVIEW-012-F02
rejectedFindings: []
deferredFindings: []
---

# SF-CORRECTION-012 — Correct client slug validation findings

## 1. Résumé

Corriger les deux findings majeurs produits par `SF-REVIEW-012`.

La validation doit refuser les tirets consécutifs et un test
d’intégration doit démontrer qu’aucune écriture n’est effectuée
lorsque la configuration est rejetée.

## 2. Références

- Tâche : `SF-TASK-012`
- Revue : `SF-REVIEW-012`
- Baseline : `abc1234`
- Branche : `feature/validate-client-slug`

## 3. Triage

| Finding | Décision | Justification |
|---|---|---|
| `SF-REVIEW-012-F01` | `accepted` | Contrat kebab-case non respecté |
| `SF-REVIEW-012-F02` | `accepted` | `AC-09` insuffisamment prouvé |

## 4. Objectif

Satisfaire `AC-05` et `AC-09` sans modifier les autres règles
de validation du slug ni le format des erreurs.

## 5. Périmètre

- Validation de `ClientSlug`
- Test unitaire des doubles tirets
- Test d’intégration du pipeline
- Documentation du format si nécessaire

## 6. Hors périmètre

- Migration d’anciens slugs
- Normalisation automatique
- Modification Docker Compose
- Nouvelle commande CLI
- Refactoring du chargeur YAML

## 7. Comportements à préserver

- `sample-project` reste accepté.
- Les majuscules restent refusées.
- `../client` reste refusé.
- Le code d’erreur reste `SF-CONFIG-012`.
- Domain ne dépend pas d’Infrastructure.
- Aucun package externe n’est ajouté.

## 8. Analyse

### SF-REVIEW-012-F01

Cause :

```text
La validation vérifie les caractères autorisés,
mais pas l’enchaînement des séparateurs.
```

Correction :

```text
Utiliser une règle garantissant qu’un tiret est toujours entouré
de segments alphanumériques non vides.
```

Test :

```text
sample--project
→ rejet
```

### SF-REVIEW-012-F02

Cause :

```text
Le test actuel utilise un spy sur le planner,
mais n’exécute pas le pipeline avec un filesystem temporaire.
```

Correction :

```text
Ajouter un test d’intégration avec un dossier temporaire
et vérifier l’absence de fichier après l’échec.
```

## 9. Plan

1. Corriger la règle Domain.
2. Ajouter le test Domain de régression.
3. Ajouter le test pipeline avec dossier temporaire.
4. Réexécuter les tests ciblés.
5. Exécuter build Release et suite complète.
6. Produire le compte-rendu.

## 10. Contraintes

- Aucun nouveau package.
- Aucun accès réseau.
- Aucun changement de schéma.
- Aucun fichier ne doit être créé avant validation.
- Compatibilité Windows et Linux.

## 11. Tests attendus

### Finding F01

```text
ClientSlug_TryCreate_WhenConsecutiveHyphens_ReturnsInvalid
```

### Finding F02

```text
Generate_WhenClientSlugIsInvalid_DoesNotWriteAnyFile
```

Le test utilise un dossier temporaire propre et vérifie :

```text
directory exists or not according to the existing contract
file count = 0
```

## 12. Commandes

```powershell
dotnet restore SiteFactory.slnx --locked-mode
```

```powershell
dotnet build SiteFactory.slnx  --configuration Release  --no-restore
```

```powershell
dotnet test SiteFactory.slnx  --configuration Release  --no-build --no-restore --filter "Category!=PostgreSql"
```

## 13. Conditions d’arrêt

Arrêter si :

- la correction nécessite une modification du format public ;
- une configuration existante utilise les doubles tirets ;
- la création du dossier avant validation est un contrat volontaire ;
- une dépendance structurante devient nécessaire.

## 14. Définition de Done

- [ ] `F01` déclaré fixed avec test.
- [ ] `F02` déclaré fixed avec test d’intégration.
- [ ] Tous les critères initialement passés restent satisfaits.
- [ ] Build Release réussi.
- [ ] Tests réussis.
- [ ] Compte-rendu prêt pour seconde revue.
```

---

# 59. Modèle de compte-rendu de correction

```markdown
# Compte-rendu de correction

## Références

- Correction :
- Tâche :
- Revue initiale :
- Baseline :
- Commit de correction :

## Résumé

## Findings

| Finding | Décision | Modification | Preuve | Statut déclaré |
|---|---|---|---|---|
| F01 | accepted | | | fixed |
| F02 | rejected | Aucune | | not-applied |

## Fichiers modifiés

## Détails des corrections

### Finding F01

- Cause :
- Correction :
- Test :
- Résultat :

## Tests exécutés

### Tests ciblés

```bash
...
```

Résultat :

```text
...
```

### Suite globale

```bash
...
```

Résultat :

```text
Total :
Réussis :
Échecs :
Ignorés :
```

## Build

```text
Warnings :
Erreurs :
```

## Scans

- Secret scan :
- Dependency scan :
- Static analysis :

## Documentation

## Migrations

## Éléments non exécutés

## Observations hors périmètre

## Risques restants

## Décisions humaines restantes

## Statut

```text
ready-for-re-review
```
```

---

# 60. Matrice de preuve

| Type de finding | Preuve minimale recommandée |
|---|---|
| Règle métier | Test unitaire ciblé |
| Régression fonctionnelle | Test de régression |
| Autorisation | Test HTTP direct |
| Antiforgery | Test de requête sans token |
| PostgreSQL | Test avec PostgreSQL réel |
| Migration | Migration appliquée sur base de test |
| Génération | Projet réellement généré |
| Fichier protégé | Test de non-écrasement |
| CI | Exécution de workflow ou validation adaptée |
| Docker | Build ou Compose de test |
| Documentation | Validation des exemples et liens |
| Sécurité | Test de contournement et scan adapté |

---

# 61. Quality gates de correction

Une correction ne peut pas passer à `ready-for-re-review` si :

```text
Finding accepté non traité

Finding rejeté appliqué

Finding reporté non tracé

Test de régression absent sans justification

Build obligatoire échoué

Test obligatoire échoué

Secret détecté

PII ajoutée aux logs

Migration non inspectée

Contrat modifié sans documentation

Diff hors périmètre important

Compte-rendu absent

Résultat de test inventé ou non vérifiable
```

---

# 62. Critères d’acceptation d’une correction

Une correction est prête pour seconde revue lorsque :

1. chaque finding accepté possède une réponse ;
2. chaque finding corrigé possède une preuve ;
3. les findings rejetés ne sont pas appliqués ;
4. les findings reportés possèdent une trace ;
5. le diff reste limité ;
6. les comportements à préserver restent valides ;
7. les tests de régression existent ;
8. les tests adaptés réussissent ;
9. le build Release réussit ;
10. la documentation concernée est à jour ;
11. aucun secret n’est présent ;
12. le compte-rendu est complet ;
13. les nouveaux risques sont signalés ;
14. aucun statut `verified` n’est auto-attribué.

---

# 63. Critères de clôture

Une correction peut être déclarée `completed` lorsque :

```text
[ ] Seconde revue terminée
[ ] Findings acceptés vérifiés
[ ] Nouveaux findings bloquants absents
[ ] Findings reportés tracés
[ ] Décision humaine d’acceptation
[ ] Branche fusionnée ou décision de clôture prise
```

---

# 64. Codes documentaires recommandés

| Code | Signification |
|---|---|
| `SF-CORRECTION-VALIDATION-001` | Revue source absente |
| `SF-CORRECTION-VALIDATION-002` | Findings non triés |
| `SF-CORRECTION-VALIDATION-003` | Finding accepté sans correction |
| `SF-CORRECTION-VALIDATION-004` | Finding rejeté appliqué |
| `SF-CORRECTION-VALIDATION-005` | Finding reporté sans tâche |
| `SF-CORRECTION-VALIDATION-006` | Périmètre de correction absent |
| `SF-CORRECTION-VALIDATION-007` | Comportement à préserver absent |
| `SF-CORRECTION-VALIDATION-008` | Test de régression absent |
| `SF-CORRECTION-VALIDATION-009` | Baseline inconnue |
| `SF-CORRECTION-VALIDATION-010` | Diff hors périmètre |
| `SF-CORRECTION-VALIDATION-011` | Nouveau package non approuvé |
| `SF-CORRECTION-VALIDATION-012` | Migration non prévue |
| `SF-CORRECTION-VALIDATION-013` | Secret détecté |
| `SF-CORRECTION-VALIDATION-014` | Compte-rendu absent |
| `SF-CORRECTION-VALIDATION-015` | Statut verified auto-attribué |
| `SF-CORRECTION-VALIDATION-016` | Résultat de test non vérifié |
| `SF-CORRECTION-VALIDATION-017` | Nouvelle régression détectée |
| `SF-CORRECTION-VALIDATION-018` | Seconde revue requise |

---

# 65. Règles pour LogicGate

LogicGate doit :

- trier les findings ;
- accepter ou rejeter explicitement ;
- valider les reports ;
- limiter le périmètre ;
- prendre les décisions structurantes ;
- vérifier les risques ;
- demander une seconde revue ;
- décider de la fusion.

---

## 65.1 LogicGate ne doit pas

- demander d’appliquer tous les commentaires sans triage ;
- reporter un bloquant sans justification ;
- modifier rétroactivement la tâche pour éviter la correction ;
- considérer le compte-rendu Codex comme seconde revue.

---

# 66. Règles pour Codex

Codex doit :

- relire la tâche et la revue ;
- respecter le triage ;
- corriger uniquement les findings acceptés ;
- préserver les autres comportements ;
- ajouter les tests nécessaires ;
- exécuter les validations ;
- produire un compte-rendu finding par finding ;
- signaler toute extension nécessaire ;
- préparer la seconde revue.

---

## 66.1 Codex ne doit pas

```text
Corriger un finding rejeté

Clôturer un finding comme verified

Modifier la revue initiale

Réécrire la tâche initiale

Élargir silencieusement le périmètre

Supprimer un test

Masquer un warning

Désactiver un scan

Appliquer une migration production

Déployer

Créer un tag

Fusionner la branche

Inventer un résultat
```

---

# 67. Décisions humaines requises

Codex doit attendre une décision pour :

- triage d’un finding contesté ;
- report d’un bloquant ou majeur ;
- extension du périmètre ;
- nouvelle dépendance structurante ;
- migration destructive ;
- breaking change ;
- modification de prix ;
- modification d’entitlement ;
- dérogation sécurité ;
- acceptation avec réserve ;
- merge ;
- release ;
- déploiement.

---

# 68. Anti-patterns interdits

## 68.1 Application automatique de tous les findings

Même les suggestions facultatives sont implémentées sans validation.

---

## 68.2 Revue initiale modifiée

Les findings disparaissent du document après correction.

---

## 68.3 Tâche réécrite

Les critères sont modifiés pour correspondre au code existant.

---

## 68.4 Refactorisation générale

Deux findings locaux produisent une réécriture de plusieurs projets.

---

## 68.5 Test supprimé

Le test révélant le défaut est retiré.

---

## 68.6 Assertion affaiblie

Le test reste présent mais vérifie moins de choses.

---

## 68.7 Finding déclaré `verified` par l’implémenteur

La vérification indépendante est contournée.

---

## 68.8 Finding rejeté appliqué

La décision humaine n’est pas respectée.

---

## 68.9 Report sans tâche

Le finding disparaît du cycle sans propriétaire ni échéance.

---

## 68.10 Nouveau défaut masqué

Une régression est présentée comme faisant partie de la correction.

---

## 68.11 Build partiel présenté comme global

Seul un test ciblé est exécuté, mais le compte-rendu annonce une validation complète.

---

## 68.12 Production utilisée pour vérifier

Une correction est testée directement sur une production.

---

## 68.13 `continue-on-error`

Un gate est rendu non bloquant pour valider la correction.

---

## 68.14 Secret dans le rapport

Un log ou fichier d’environnement est recopié pour expliquer le défaut.

---

# 69. Roadmap de mise en place

## Phase correction 1 — Triage

```text
accepted
rejected
deferred
clarification-required
```

---

## Phase correction 2 — Template

```text
scope
preserved behaviors
finding analysis
tests
report
```

---

## Phase correction 3 — Traçabilité

```text
task
review
correction
re-review
commits
```

---

## Phase correction 4 — Automatisation

```text
front matter
finding IDs
status consistency
required sections
```

---

## Phase correction 5 — Quality metrics

```text
correction cycles
reopened findings
new regressions
time to verification
```

---

# 70. Checklist avant démarrage

```text
[ ] Tâche initiale lue
[ ] Revue initiale lue
[ ] Findings triés
[ ] Findings acceptés identifiés
[ ] Findings rejetés justifiés
[ ] Findings reportés tracés
[ ] Baseline identifiée
[ ] Périmètre défini
[ ] Comportements à préserver définis
[ ] Tests attendus définis
[ ] Aucun blocage
```

---

# 71. Checklist pendant la correction

```text
[ ] Correction finding par finding
[ ] Diff limité
[ ] Architecture préservée
[ ] Sécurité préservée
[ ] Contrats préservés
[ ] Aucun package non approuvé
[ ] Tests de régression ajoutés
[ ] Documentation mise à jour
[ ] Nouveaux problèmes signalés
```

---

# 72. Checklist avant seconde livraison

```text
[ ] Tous les findings acceptés traités
[ ] Aucun finding rejeté appliqué
[ ] Findings reportés documentés
[ ] Tests ciblés réussis
[ ] Tests du composant réussis
[ ] Build Release réussi
[ ] Suite globale adaptée réussie
[ ] Scans requis réussis
[ ] Aucun test critique ignoré
[ ] Aucun warning injustifié
[ ] Aucun secret
[ ] Compte-rendu finding par finding
[ ] Commit de correction identifié
[ ] Statut ready-for-re-review
```

---

# 73. Checklist de seconde revue

```text
[ ] Baseline connue
[ ] Correction commit connu
[ ] Diff de correction inspecté
[ ] Chaque finding accepté vérifié
[ ] Tests de régression exécutés
[ ] Critères initiaux revérifiés
[ ] Nouveaux risques recherchés
[ ] Nouveaux findings séparés
[ ] Recommandation finale produite
```

---

# 74. Checklist de clôture

```text
[ ] Findings acceptés verified
[ ] Findings rejetés tracés
[ ] Findings reportés associés à une tâche
[ ] Aucun bloquant ouvert
[ ] Aucun majeur ouvert non accepté
[ ] Décision humaine prise
[ ] Correction documentée
[ ] Historique conservé
```

---

# 75. Modèle de matrice de correction

```markdown
| Finding | Sévérité | Décision | Cause | Correction | Test | Statut implémenteur | Statut reviewer |
|---|---|---|---|---|---|---|---|
| F01 | Major | Accepted | | | | Fixed | Verified |
| F02 | Minor | Deferred | | SF-TASK-045 | — | Deferred | Deferred |
| F03 | Optional | Rejected | | — | — | Not applied | Rejected |
```

---

# 76. Critères d’acceptation du modèle

Le modèle sera considéré comme opérationnel lorsque :

1. les corrections possèdent un identifiant ;
2. la revue source est obligatoire ;
3. le triage des findings est obligatoire ;
4. seuls les findings acceptés sont corrigés ;
5. les findings rejetés restent tracés ;
6. les findings reportés possèdent une tâche ;
7. le périmètre est défini ;
8. le hors périmètre est défini ;
9. les comportements à préserver sont identifiés ;
10. chaque finding possède une analyse ;
11. chaque finding corrigé possède une preuve ;
12. les tests de régression sont attendus ;
13. les critères initiaux sont revérifiés ;
14. les nouveaux problèmes sont signalés ;
15. la revue originale reste immuable ;
16. la tâche initiale n’est pas réécrite rétroactivement ;
17. un template complet existe ;
18. un template simplifié existe ;
19. un exemple complet existe ;
20. une seconde revue est obligatoire ;
21. l’implémenteur ne peut pas déclarer `verified` ;
22. les quality gates de correction sont définis ;
23. aucun secret ou donnée réelle n’est autorisé ;
24. les opérations de production restent interdites ;
25. le compte-rendu finding par finding est obligatoire.

---

# 77. Décisions validées

La baseline des corrections Codex est la suivante :

```text
Une correction est distincte de l’implémentation initiale
et de la seconde revue.

La revue initiale reste immuable.

Les findings sont triés humainement avant correction.

Seuls les findings acceptés sont corrigés.

Les findings rejetés ne sont pas appliqués.

Les findings reportés possèdent une tâche,
un propriétaire et une échéance.

Le périmètre de correction reste limité.

Les comportements déjà validés sont explicitement préservés.

Chaque finding accepté est analysé séparément.

Chaque finding corrigé possède une preuve.

Les corrections fonctionnelles ajoutent
des tests de régression lorsque pertinent.

Les critères d’acceptation initialement satisfaits
sont revérifiés après correction.

Une correction ne supprime ni n’affaiblit
un test révélant le défaut.

Une correction ne désactive pas un quality gate.

Les nouveaux problèmes découverts sont signalés
et ne sont pas corrigés silencieusement hors périmètre.

Une extension importante produit une décision
ou une nouvelle tâche.

Les migrations de production restent interdites.

Les déploiements de production restent interdits.

L’implémenteur peut déclarer un finding fixed.

Seul le reviewer peut le déclarer verified.

La seconde revue vérifie les findings
et recherche de nouvelles régressions.

Les nouveaux findings possèdent de nouveaux identifiants.

Le compte-rendu de correction traite
chaque finding individuellement.

Codex ne peut pas clôturer seul
la correction, la revue ou la pull request.
```

---

# 78. Conclusion

Une correction après revue doit transformer des findings validés en modifications ciblées et prouvées.

Elle repose sur :

```text
triage
+ périmètre limité
+ analyse finding par finding
+ comportements préservés
+ tests de régression
+ preuves
+ compte-rendu
+ seconde revue
```

Une correction réussie ne consiste pas simplement à faire disparaître les findings du rapport.

Elle doit démontrer que :

- la cause a été traitée ;
- le comportement attendu est respecté ;
- les autres comportements restent stables ;
- aucun nouveau risque majeur n’a été introduit ;
- le reviewer peut vérifier chaque correction indépendamment.

> Une correction fiable ne cherche pas à rendre la revue plus favorable. Elle cherche à rendre l’implémentation réellement conforme aux décisions, aux contrats et aux preuves attendues.
