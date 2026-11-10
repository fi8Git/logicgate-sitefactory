# LogicGate SiteFactory — Modèle de décision d’architecture

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

Ce modèle conserve son statut historique « Proposition à valider » et ses
exemples de cycle de décision ; il n'approuve ni une tâche ni un ADR par lui-même.
[AGENTS.md](../../AGENTS.md) fixe les autorisations et la hiérarchie des sources.
Les décisions acceptées sont conservées, avec alternatives, conséquences et
conditions de réexamen ; une adaptation publique ne les réécrit pas.
Les exemples d'application cliente, de manifest enrichi et de production
sont des modèles, pas des capacités attestées par ce template.

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Modèle de décision d’architecture |
| Identifiant | ADR-0000 |
| Version | 1.0 |
| Statut | Proposition à valider |
| Propriétaire | LogicGate |
| Public principal | LogicGate, architectes, développeurs, contributeurs, Codex |
| Type de document | Template Architecture Decision Record |
| Emplacement des ADR | `docs/adr/` |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../02-architecture/core-module-boundary.md`, `28-codex-operating-guide.md`, `AGENTS.md` |
| Dernière mise à jour | 2026-07-28 |

---

## 1. Objet du document

Ce document définit le format standard des Architecture Decision Records, ou ADR, utilisés dans LogicGate SiteFactory.

Un ADR consigne une décision structurante afin de conserver :

- son contexte ;
- les contraintes du moment ;
- les options étudiées ;
- les raisons du choix ;
- les conséquences attendues ;
- les risques acceptés ;
- les conditions de révision ;
- les responsabilités ;
- les références vers l’implémentation.

L’objectif est de pouvoir répondre, plusieurs mois ou années plus tard, aux questions suivantes :

```text
Pourquoi cette décision a-t-elle été prise ?

Quelles alternatives ont été étudiées ?

Quelles contraintes existaient à ce moment-là ?

Quels compromis ont été acceptés ?

Quelles conséquences étaient anticipées ?

Dans quelles conditions la décision doit-elle être réexaminée ?
```

---

## 2. Vision

Une architecture ne se résume pas à son état actuel.

Elle résulte d’une succession de décisions prises dans un contexte donné.

Sans ADR, le repository montre souvent :

```text
ce qui existe
```

mais pas :

```text
pourquoi cela existe
```

Le cycle de référence est :

```text
Problème structurant
→ contexte
→ options
→ analyse
→ décision humaine
→ ADR accepté
→ tâches d’implémentation
→ tests
→ observation
→ révision éventuelle
```

---

## 3. Principes fondamentaux

## 3.1 Une décision importante doit être explicite

Une décision structurante ne doit pas exister uniquement :

- dans une conversation ;
- dans la mémoire du mainteneur ;
- dans un commentaire de code ;
- dans une pull request difficile à retrouver ;
- dans l’implémentation produite par Codex ;
- dans un ticket fermé.

Elle doit être consignée dans un ADR.

---

## 3.2 L’ADR décrit une décision, pas uniquement une solution

Un ADR doit expliquer :

```text
le problème
+ les contraintes
+ les options
+ la décision
+ les conséquences
```

Il ne doit pas seulement annoncer :

```text
Nous utilisons PostgreSQL.
```

---

## 3.3 Le contexte fait partie de la décision

Une décision peut être correcte dans un contexte et devenir moins adaptée lorsque ce contexte change.

L’ADR doit donc conserver :

- les besoins ;
- les hypothèses ;
- les limites ;
- les volumes ;
- les compétences ;
- les contraintes financières ;
- les contraintes de sécurité ;
- la phase du produit.

---

## 3.4 Les compromis doivent être visibles

Une décision d’architecture possède généralement :

- des avantages ;
- des coûts ;
- des limites ;
- des risques ;
- des conséquences opérationnelles.

Un ADR ne doit pas présenter une option comme parfaite.

---

## 3.5 Une décision acceptée n’est pas réécrite silencieusement

Après acceptation, l’ADR conserve l’historique de la décision.

Pour modifier la décision :

```text
nouvel ADR
→ remplacement ou évolution
→ ancien ADR marqué superseded ou deprecated
```

---

## 3.6 La décision reste humaine

Codex peut :

- analyser ;
- proposer ;
- comparer ;
- préparer un ADR ;
- rechercher les impacts.

Codex ne peut pas accepter seul une décision structurante.

---

## 3.7 L’implémentation doit rester liée à la décision

L’ADR doit pouvoir référencer :

- tâches ;
- pull requests ;
- commits ;
- modules ;
- tests ;
- migrations ;
- documentation.

---

# 4. Non-objectifs

Un ADR ne doit pas devenir :

- une spécification fonctionnelle complète ;
- une tâche Codex ;
- un compte-rendu de réunion ;
- un runbook ;
- une documentation d’API ;
- un guide utilisateur ;
- une procédure de déploiement ;
- un journal quotidien ;
- une justification a posteriori inventée ;
- un moyen de contourner une décision commerciale ;
- une approbation juridique ou réglementaire.

---

# 5. Terminologie

## 5.1 ADR

Architecture Decision Record.

Document conservant une décision d’architecture particulière.

---

## 5.2 Décision structurante

Décision ayant un impact durable, transversal ou difficilement réversible.

---

## 5.3 Decision driver

Élément influençant le choix.

Exemples :

- sécurité ;
- coût ;
- simplicité ;
- performance ;
- portabilité ;
- maintenabilité ;
- time-to-market ;
- isolation ;
- compétences disponibles.

---

## 5.4 Option

Solution possible étudiée avant la décision.

---

## 5.5 Conséquence

Effet positif, négatif ou neutre produit par la décision.

---

## 5.6 Trade-off

Compromis accepté entre plusieurs objectifs concurrents.

---

## 5.7 Revisit trigger

Événement ou condition nécessitant de réexaminer la décision.

---

## 5.8 Superseded

Statut d’un ADR remplacé par une décision plus récente.

---

## 5.9 Deprecated

Statut d’une décision encore observable mais dont l’utilisation n’est plus recommandée.

---

# 6. Quand créer un ADR

Un ADR est recommandé lorsqu’une décision affecte notamment :

- plusieurs projets ;
- plusieurs modules ;
- les projets clients générés ;
- la structure du repository ;
- les contrats publics ;
- les formats de configuration ;
- la persistance ;
- la sécurité ;
- l’Identity ;
- les permissions ;
- les entitlements ;
- le pipeline de génération ;
- les stratégies de fichiers ;
- la supply chain ;
- le déploiement ;
- les backups ;
- les releases ;
- la compatibilité ;
- la maintenance.

---

# 7. Décisions nécessitant généralement un ADR

Exemples SiteFactory :

```text
Un repository et une base par client

PostgreSQL comme base de données de référence

Docker Compose VPS comme profil de déploiement initial

Absence de multi-tenant dans le MVP

Migrations stockées dans le repository client

Configuration client en YAML

Lock file en JSON

Séparation des entitlements et des feature flags

Publication des packages dans un feed privé

Choix du format de solution .slnx

Stratégies generated, create-once, merge, custom et protected

Absence de migration automatique en production

Abstraction des providers LLM

Adoption d’OpenTelemetry

Changement de version majeure .NET ou PostgreSQL
```

---

# 8. Décisions ne nécessitant généralement pas un ADR

Un ADR n’est généralement pas nécessaire pour :

- correction locale ;
- renommage mineur ;
- message d’erreur ;
- test supplémentaire ;
- correction CSS ;
- ajout d’une méthode privée ;
- organisation interne sans conséquence durable ;
- mise à jour patch compatible ;
- tâche d’implémentation déjà encadrée par une décision existante.

---

## 8.1 En cas de doute

Un ADR est utile si la réponse à plusieurs questions suivantes est positive :

```text
Le choix sera-t-il difficile à comprendre plus tard ?

Plusieurs options raisonnables existent-elles ?

Le choix affecte-t-il plusieurs composants ?

Le retour arrière est-il coûteux ?

Le choix crée-t-il une contrainte durable ?

Le choix comporte-t-il un compromis important ?

Codex pourrait-il prendre une autre direction sans contexte ?
```

---

# 9. Différence entre ADR et tâche Codex

## ADR

Répond principalement à :

```text
Pourquoi et quelle direction choisir ?
```

## Tâche Codex

Répond principalement à :

```text
Quoi implémenter maintenant et comment valider le résultat ?
```

Flux attendu :

```text
ADR accepté
→ une ou plusieurs tâches Codex
→ implémentation
→ revue
```

---

# 10. Différence entre ADR et documentation d’architecture

## ADR

Conserve une décision précise et historique.

## Documentation d’architecture

Décrit l’état actuel et consolidé du système.

Une décision acceptée doit conduire, si nécessaire, à la mise à jour de la documentation propriétaire.

---

# 11. Différence entre ADR et runbook

## ADR

Explique pourquoi une stratégie opérationnelle existe.

## Runbook

Explique comment exécuter une opération ou traiter un incident.

---

# 12. Emplacement

Les ADR sont stockés dans :

```text
docs/adr/
```

Structure recommandée :

```text
docs/adr/
├── 0000-adr-template.md
├── 0001-client-isolation-model.md
├── 0002-client-configuration-format.md
├── 0003-database-provider.md
└── README.md
```

---

# 13. Convention de nommage

Format :

```text
{number}-{short-kebab-title}.md
```

Exemples :

```text
0001-one-repository-per-client.md

0002-yaml-client-configuration.md

0003-postgresql-as-primary-database.md
```

---

## 13.1 Numéro

Le numéro comporte quatre chiffres :

```text
0000
0001
0002
...
```

---

## 13.2 Numéro immuable

Le numéro ne change pas après publication de l’ADR.

---

## 13.3 Identifiant lisible

Le document peut également utiliser :

```text
ADR-0001
```

dans son contenu et ses références.

---

# 14. Statuts des ADR

Valeurs autorisées :

```text
proposed
accepted
rejected
deprecated
superseded
withdrawn
```

---

## 14.1 `proposed`

La décision est en cours d’étude.

Elle ne constitue pas encore une règle obligatoire.

---

## 14.2 `accepted`

La décision a été validée humainement.

Elle devient applicable dans son périmètre.

---

## 14.3 `rejected`

L’option décrite n’a pas été retenue.

Conserver un ADR rejeté peut éviter de répéter la même analyse.

---

## 14.4 `deprecated`

La décision reste visible mais une évolution est recommandée.

---

## 14.5 `superseded`

La décision a été remplacée par un autre ADR.

Le nouvel ADR doit être référencé.

---

## 14.6 `withdrawn`

La proposition a été retirée avant acceptation.

---

# 15. Cycle de vie

Flux principal :

```text
proposed
→ accepted
→ deprecated
→ superseded
```

Autres flux :

```text
proposed
→ rejected

proposed
→ withdrawn
```

---

## 15.1 Modification du statut

Chaque changement de statut doit être :

- explicite ;
- daté ;
- versionné dans Git ;
- accompagné d’une référence si nécessaire.

---

# 16. Propriétaire de la décision

Chaque ADR doit identifier :

- auteur ;
- décideur ;
- contributeurs ;
- reviewers éventuels.

Le propriétaire de la décision reste humain.

---

# 17. Date

Les dates sont enregistrées au format :

```text
YYYY-MM-DD
```

Les événements techniques utilisent UTC lorsqu’une heure est nécessaire.

---

# 18. Front matter recommandé

```yaml
---
adrId: ADR-XXXX
title: Short decision title
status: proposed
date: YYYY-MM-DD
decisionOwners:
  - LogicGate
authors:
  - Name or role
reviewers: []
supersedes: []
supersededBy: null
relatedTasks: []
relatedDocuments: []
targetVersion: null
---
```

---

## 18.1 Données interdites

Le front matter ne contient pas :

- secret ;
- donnée personnelle inutile ;
- information client confidentielle ;
- token ;
- credential ;
- URL avec authentification.

---

# 19. Sections obligatoires

Un ADR proposé doit contenir au minimum :

```text
Titre
Statut
Contexte
Problème
Decision drivers
Contraintes
Options étudiées
Décision
Conséquences
Risques
Conditions de révision
Références
```

---

# 20. Titre

Le titre doit exprimer la décision ou le problème étudié.

Exemples :

```text
Use one repository and one database per client

Use YAML for client configuration

Keep production migrations explicit
```

Exemples à éviter :

```text
Architecture

Database

Important decision
```

---

# 21. Résumé

Le résumé présente en quelques lignes :

- le problème ;
- la décision ;
- son impact principal.

Exemple :

```text
SiteFactory générera une application, un repository et une base
PostgreSQL isolés pour chaque client. Le MVP ne proposera pas
d’architecture multi-tenant.
```

---

# 22. Contexte

Le contexte doit décrire :

- la phase du projet ;
- l’état actuel ;
- le besoin ;
- les contraintes connues ;
- l’impact de l’absence de décision.

---

## 22.1 Contexte temporel

Il peut préciser :

```text
MVP

Projet pilote

Avant première production

Après retour d’expérience

Lors d’un changement de version majeure
```

---

## 22.2 Contexte produit

Exemples :

- nombre prévisible de clients ;
- packs concernés ;
- besoin d’isolation ;
- besoin de personnalisation ;
- niveau d’automatisation ;
- capacités de l’équipe.

---

## 22.3 Contexte technique

Exemples :

- .NET 10 ;
- Blazor ;
- PostgreSQL ;
- Docker Compose ;
- VPS Linux ;
- GitHub Actions ;
- repository par client.

---

# 23. Problème à résoudre

Le problème doit être formulé indépendamment d’une solution particulière.

Exemple :

```text
Comment isoler les données, les personnalisations et les cycles
de déploiement de plusieurs projets clients tout en conservant
une exploitation compréhensible par une petite équipe ?
```

---

## 23.1 Mauvaise formulation

```text
Pourquoi faut-il utiliser une base par client ?
```

Cette formulation suppose déjà la décision.

---

# 24. Decision drivers

Les decision drivers sont classés par importance.

Exemple :

```text
1. Isolation des données
2. Réduction du risque de fuite entre clients
3. Simplicité des backups et restaurations
4. Personnalisation
5. Facilité de transfert
6. Coût d’infrastructure
7. Capacité de maintenance
```

---

## 24.1 Priorités concurrentes

L’ADR doit expliciter lorsque deux objectifs s’opposent.

Exemple :

```text
Isolation maximale
versus
Mutualisation des ressources
```

---

# 25. Contraintes

Les contraintes sont des limites non négociables ou difficiles à changer.

Exemples :

```text
Développement en .NET 10

Équipe initiale réduite

Déploiement sur VPS Linux

PostgreSQL comme préférence de persistance

Sécurité fondamentale obligatoire

Absence d’opérations automatiques de production
```

---

# 26. Hypothèses

Les hypothèses doivent être explicites.

Exemples :

```text
Le nombre initial de clients restera limité.

Chaque client acceptera son propre cycle de déploiement.

Les projets standards ne nécessitent pas de partage de données.

La mutualisation physique du VPS reste possible.
```

---

## 26.1 Hypothèse invalide

Une hypothèse devenue fausse peut déclencher la révision de l’ADR.

---

# 27. Portée de la décision

Définir ce que la décision couvre.

Exemple :

```text
- Projets standards générés
- Packs SiteFactory
- Staging et production
- PostgreSQL
- Backups
- Déploiements
```

---

# 28. Hors portée

Définir ce que l’ADR ne décide pas.

Exemple :

```text
- Fournisseur VPS exact
- Dimensionnement matériel
- Offre commerciale d’hébergement
- Architecture haute disponibilité future
- Projets sur mesure réglementés
```

---

# 29. Options étudiées

Chaque option doit disposer :

- d’un nom ;
- d’une description ;
- d’avantages ;
- d’inconvénients ;
- de risques ;
- d’impacts opérationnels ;
- d’une estimation de réversibilité.

---

# 30. Nombre d’options

Un ADR doit généralement examiner au moins :

```text
Option A — statu quo ou solution simple

Option B — solution retenue potentielle

Option C — alternative raisonnable
```

Il n’est pas nécessaire d’inventer des options artificielles.

---

# 31. Option du statu quo

L’absence de changement peut être une option valide.

Elle doit être analysée lorsqu’elle est réaliste.

---

# 32. Format d’une option

```markdown
### Option A — Nom

#### Description

#### Avantages

- ...

#### Inconvénients

- ...

#### Risques

- ...

#### Impacts opérationnels

- ...

#### Réversibilité

- Faible / Moyenne / Élevée
```

---

# 33. Critères de comparaison

Une matrice peut comparer les options.

Exemple :

| Critère | Poids | Option A | Option B | Option C |
|---|---:|---:|---:|---:|
| Isolation | 5 | 2 | 5 | 4 |
| Simplicité MVP | 5 | 4 | 4 | 2 |
| Coût | 3 | 5 | 3 | 2 |
| Portabilité | 4 | 2 | 5 | 4 |
| Maintenance | 4 | 3 | 4 | 2 |

---

## 33.1 Score indicatif

Une matrice aide à structurer l’analyse.

Elle ne doit pas remplacer la décision humaine.

---

# 34. Options rejetées rapidement

Une option manifestement incompatible peut être résumée sans analyse disproportionnée.

Exemple :

```text
Une base SQLite partagée a été écartée, car elle ne correspond pas
au profil de déploiement et aux besoins de concurrence retenus.
```

La justification doit rester factuelle.

---

# 35. Décision

La section Decision doit être explicite.

Format recommandé :

```text
Nous décidons de...
```

Exemple :

```text
Nous décidons de générer un repository, une application et une base
PostgreSQL isolés pour chaque client standard.
```

---

## 35.1 Décision négative

L’ADR peut aussi décider de ne pas adopter une solution.

Exemple :

```text
Nous décidons de ne pas introduire de multi-tenant dans le MVP.
```

---

## 35.2 Ambiguïté interdite

Éviter :

```text
Nous devrions probablement...

Cette solution semble intéressante...

Nous pourrions...
```

dans un ADR accepté.

---

# 36. Justification

La justification doit relier la décision aux drivers.

Exemple :

```text
Cette option maximise l’isolation des données, simplifie les backups,
permet une remise client complète et réduit le risque d’impact
croisé entre plusieurs projets.
```

---

# 37. Conséquences positives

Exemples :

- isolation ;
- clarté ;
- transfert ;
- restauration ;
- déploiements indépendants ;
- limitation du blast radius ;
- personnalisation.

---

# 38. Conséquences négatives

Exemples :

- plus de repositories ;
- plus de bases ;
- plus de déploiements ;
- inventaire à maintenir ;
- coût d’exploitation supérieur ;
- mises à jour multiprojets.

---

## 38.1 Transparence

Les conséquences négatives ne doivent pas être minimisées.

---

# 39. Conséquences neutres ou indirectes

Une décision peut également provoquer :

- modification d’un workflow ;
- besoin de nouveaux scripts ;
- création d’un inventaire ;
- nouvelles tâches ;
- documentation supplémentaire.

---

# 40. Risques

Chaque risque doit préciser :

```text
Description
Probabilité
Impact
Mesure de réduction
Propriétaire
```

Exemple :

| Risque | Probabilité | Impact | Réduction |
|---|---|---|---|
| Multiplication des upgrades clients | Moyenne | Moyen | Automatiser `upgrade check` et `upgrade plan` |
| Saturation d’un VPS mutualisé | Moyenne | Élevé | Monitoring et VPS dédié si nécessaire |

---

# 41. Risques acceptés

Certains risques peuvent être explicitement acceptés.

Exemple :

```text
Le MVP accepte une charge opérationnelle supérieure en échange
d’une meilleure isolation et d’une réversibilité client plus forte.
```

---

# 42. Mesures compensatoires

Lorsqu’une option retenue possède une faiblesse, préciser les contrôles ajoutés.

Exemples :

- inventaire client ;
- scripts de maintenance ;
- automatisation des upgrades ;
- alertes disque ;
- tests de coexistence ;
- limites de ressources.

---

# 43. Sécurité

Toute décision affectant la sécurité doit analyser :

- actifs ;
- menaces ;
- frontières de confiance ;
- privilèges ;
- secrets ;
- données personnelles ;
- logs ;
- supply chain ;
- rollback.

---

## 43.1 Décision réduisant une protection

Une diminution de sécurité nécessite :

- justification forte ;
- mesure compensatoire ;
- durée ;
- propriétaire ;
- condition de suppression.

---

# 44. Données

Une décision affectant les données doit préciser :

- propriétaire ;
- classification ;
- stockage ;
- isolation ;
- rétention ;
- migrations ;
- backup ;
- restauration ;
- suppression ;
- portabilité.

---

# 45. Identité et autorisation

Une décision affectant Identity doit préciser :

- mode d’authentification ;
- rôles ;
- permissions ;
- sessions ;
- récupération ;
- dernier administrateur ;
- audit ;
- impact client.

---

# 46. Performance

Préciser lorsque pertinent :

- latence ;
- capacité ;
- mémoire ;
- CPU ;
- I/O ;
- nombre d’instances ;
- volume ;
- limites acceptées.

Une décision de performance doit idéalement s’appuyer sur :

- mesure ;
- hypothèse chiffrée ;
- benchmark ;
- risque identifié.

---

# 47. Coût

Une décision peut analyser :

- coût de développement ;
- coût d’infrastructure ;
- coût de maintenance ;
- coût de migration ;
- coût de réversibilité ;
- coût de fournisseur ;
- temps opérateur.

Les montants éventuels sont exprimés en euros lorsque le contexte l’exige.

---

# 48. Maintenabilité

Évaluer :

- complexité ;
- compétences requises ;
- testabilité ;
- documentation ;
- dépendances ;
- nombre de composants ;
- facilité de diagnostic ;
- dette future.

---

# 49. Opérations

Une décision opérationnelle doit préciser :

- déploiement ;
- migrations ;
- health checks ;
- monitoring ;
- backup ;
- restauration ;
- incident ;
- transfert client ;
- clôture.

---

# 50. Compatibilité

Préciser les versions et contrats affectés :

```text
.NET

PostgreSQL

Configuration schema

Catalog

Modules

Templates

Themes

CLI

Deployment profile
```

---

## 50.1 Breaking change

Le document doit indiquer :

```text
Breaking change expected: yes/no
```

---

# 51. Migration

Lorsqu’une décision modifie l’existant :

- version source ;
- version cible ;
- étapes ;
- données ;
- scripts ;
- compatibilité ;
- backup ;
- rollback ;
- projets concernés.

---

## 51.1 Production

Un ADR peut décider d’une stratégie de migration.

Il ne constitue pas l’autorisation de l’appliquer en production.

---

# 52. Réversibilité

Classer la réversibilité :

```text
élevée
moyenne
faible
très faible
```

---

## 52.1 Questions

```text
Peut-on revenir en arrière par simple revert ?

Une migration de données est-elle nécessaire ?

Des contrats publics seront-ils publiés ?

Des clients dépendront-ils de cette décision ?

Des données peuvent-elles devenir incompatibles ?
```

---

# 53. Plan d’implémentation

L’ADR peut identifier les grandes étapes.

Il ne doit pas remplacer les tâches détaillées.

Exemple :

```text
1. Définir les contrats.
2. Adapter le générateur.
3. Ajouter les tests.
4. Générer un projet pilote.
5. Mettre à jour la documentation.
```

---

# 54. Tâches liées

Référencer les tâches :

```text
SF-TASK-...
```

Exemple :

```text
- SF-TASK-042 — Add client project inventory
- SF-TASK-043 — Generate isolated Compose project names
- SF-TASK-044 — Add multi-client isolation tests
```

---

# 55. Tests et preuves

Préciser les preuves nécessaires à la validation de la décision.

Exemples :

- prototype ;
- benchmark ;
- test d’architecture ;
- projet pilote ;
- test PostgreSQL ;
- restore drill ;
- test Windows/Linux ;
- scan de sécurité.

---

# 56. Résultats du prototype

Si un prototype a été réalisé, consigner :

- périmètre ;
- configuration ;
- résultat ;
- limites ;
- lien vers le code ou rapport.

---

# 57. Conditions de révision

L’ADR doit préciser quand la décision doit être réexaminée.

Exemples :

```text
Plus de 20 clients actifs

Besoin d’agrégation de données

Coût d’exploitation trop élevé

Introduction de plusieurs instances par client

Exigence réglementaire

Nouvelle version majeure du framework

Échec répété du modèle opérationnel
```

---

## 57.1 Date de revue

Une date de revue peut être définie pour une décision à risque ou temporaire.

Exemple :

```text
Review by: 2027-01-31
```

---

# 58. Indicateurs de validité

Une décision peut prévoir des indicateurs tels que :

- durée de déploiement ;
- fréquence des incidents ;
- coût VPS ;
- temps d’upgrade ;
- nombre de conflits ;
- temps de restauration ;
- utilisation mémoire.

---

# 59. Conséquences non anticipées

Après implémentation, l’ADR peut recevoir une section append-only :

```text
Observed consequences
```

Elle permet de consigner un retour d’expérience sans modifier la décision historique.

---

# 60. Références

L’ADR doit référencer :

- documents ;
- tâches ;
- pull requests ;
- prototypes ;
- rapports ;
- standards internes ;
- ADR liés.

---

## 60.1 Liens internes

Utiliser des chemins relatifs lorsque possible.

---

# 61. ADR liés

Relations possibles :

```text
Depends on
Supersedes
Superseded by
Refines
Conflicts with
Related to
```

---

# 62. ADR superseded

Lorsqu’un ADR est remplacé :

Dans l’ancien ADR :

```text
Status: superseded
Superseded by: ADR-XXXX
```

Dans le nouvel ADR :

```text
Supersedes: ADR-YYYY
```

---

## 62.1 Historique conservé

L’ancien ADR reste dans le repository.

---

# 63. ADR rejeté

Un ADR rejeté doit conserver :

- problème ;
- options ;
- raisons du rejet ;
- date ;
- décideur.

Il peut devenir une référence utile si l’option est reproposée.

---

# 64. ADR temporaire

Une décision temporaire doit préciser :

- raison ;
- durée ;
- risque ;
- date de revue ;
- solution cible ;
- propriétaire.

Exemple :

```text
Le build sur VPS est toléré uniquement pour le premier projet pilote.
La cible reste la construction d’image en CI et la promotion depuis
un registry privé.
```

---

# 65. Décision propre à un client

Une décision spécifique à un projet client ne doit pas forcément être ajoutée aux ADR généraux SiteFactory.

Emplacement possible :

```text
client-repository/docs/adr/
```

---

## 65.1 Généralisation

Une décision client ne doit être généralisée au socle qu’après :

- analyse ;
- réutilisabilité démontrée ;
- décision LogicGate ;
- ADR SiteFactory distinct.

---

# 66. Décision commerciale

Les choix de :

- prix ;
- pack ;
- remise ;
- conditions contractuelles ;

ne sont pas des ADR d’architecture.

Une conséquence technique d’une décision commerciale peut toutefois nécessiter un ADR.

---

# 67. Décision juridique

Un ADR ne valide pas juridiquement :

- conservation ;
- conformité ;
- contrat ;
- base légale ;
- transfert international ;
- mentions légales.

Il peut documenter l’architecture appliquant une décision validée par la personne compétente.

---

# 68. Processus de proposition

## Étape 1 — Identifier le problème

Formuler le problème sans supposer la solution.

---

## Étape 2 — Collecter le contexte

Relever :

- documentation ;
- code ;
- contraintes ;
- incidents ;
- volumes ;
- retours ;
- coûts.

---

## Étape 3 — Identifier les options

Inclure uniquement les options crédibles.

---

## Étape 4 — Comparer

Utiliser les decision drivers.

---

## Étape 5 — Proposer

Rédiger la décision recommandée et ses conséquences.

---

## Étape 6 — Revoir

Faire examiner :

- cohérence ;
- sécurité ;
- données ;
- opérations ;
- maintenabilité ;
- réversibilité.

---

## Étape 7 — Décider

LogicGate accepte, rejette ou demande des modifications.

---

## Étape 8 — Implémenter

Créer les tâches nécessaires.

---

# 69. Processus d’acceptation

Un ADR peut passer à `accepted` lorsque :

```text
[ ] Problème clair
[ ] Contexte suffisant
[ ] Drivers identifiés
[ ] Options crédibles étudiées
[ ] Compromis explicites
[ ] Conséquences décrites
[ ] Risques décrits
[ ] Sécurité analysée
[ ] Données analysées si nécessaire
[ ] Opérations analysées
[ ] Réversibilité analysée
[ ] Conditions de révision définies
[ ] Décideur humain identifié
```

---

# 70. Revue d’un ADR

Une revue d’ADR doit vérifier :

- neutralité de la formulation ;
- options réelles ;
- cohérence des hypothèses ;
- impacts oubliés ;
- sécurité ;
- données ;
- exploitation ;
- dépendances ;
- compatibilité ;
- coûts ;
- réversibilité.

---

## 70.1 Revue distincte du code

L’ADR peut être accepté avant l’implémentation.

L’implémentation reste ensuite soumise à ses propres revues.

---

# 71. Template complet

```markdown
---
adrId: ADR-XXXX
title: Short decision title
status: proposed
date: YYYY-MM-DD
decisionOwners:
  - LogicGate
authors:
  - LogicGate
reviewers: []
supersedes: []
supersededBy: null
relatedTasks: []
relatedDocuments: []
targetVersion: null
---

# ADR-XXXX — Short decision title

## 1. Statut

```text
proposed
```

## 2. Résumé

Résumer le problème, la décision proposée et son impact principal.

## 3. Contexte

Décrire :

- l’état actuel ;
- le besoin ;
- la phase du produit ;
- les contraintes importantes ;
- l’impact de l’absence de décision.

## 4. Problème à résoudre

Formuler le problème sans imposer la solution.

## 5. Portée

- Élément couvert
- Projet ou module concerné
- Environnements concernés

## 6. Hors portée

- Élément non décidé
- Évolution future
- Sujet contractuel ou opérationnel séparé

## 7. Decision drivers

Par ordre de priorité :

1. Driver
2. Driver
3. Driver

## 8. Contraintes

- Contrainte technique
- Contrainte produit
- Contrainte sécurité
- Contrainte opérationnelle

## 9. Hypothèses

- Hypothèse
- Hypothèse

## 10. Options étudiées

### Option A — Nom

#### Description

#### Avantages

- ...

#### Inconvénients

- ...

#### Risques

- ...

#### Réversibilité

```text
élevée / moyenne / faible
```

### Option B — Nom

#### Description

#### Avantages

- ...

#### Inconvénients

- ...

#### Risques

- ...

#### Réversibilité

### Option C — Nom

#### Description

#### Avantages

- ...

#### Inconvénients

- ...

#### Risques

- ...

#### Réversibilité

## 11. Comparaison

| Critère | Option A | Option B | Option C |
|---|---:|---:|---:|
| Critère | | | |

## 12. Décision

Nous décidons de...

## 13. Justification

Expliquer pourquoi l’option retenue répond le mieux aux drivers.

## 14. Conséquences positives

- ...

## 15. Conséquences négatives

- ...

## 16. Risques acceptés

- ...

## 17. Mesures compensatoires

- ...

## 18. Sécurité

- Actifs
- Menaces
- Contrôles
- Secrets
- Données personnelles
- Logs

## 19. Données

- Modèle
- Isolation
- Migrations
- Backup
- Restore
- Rétention
- Portabilité

## 20. Performance et capacité

- Hypothèses
- Limites
- Mesures

## 21. Coûts

- Développement
- Infrastructure
- Maintenance
- Migration

## 22. Exploitation

- Déploiement
- Observabilité
- Backups
- Incidents
- Transfert

## 23. Compatibilité

- Versions
- Contrats
- Breaking change expected:
- Upgrade requis:

## 24. Migration

- Source
- Cible
- Étapes
- Backup
- Rollback
- Production execution: out of scope

## 25. Réversibilité

Décrire comment revenir sur la décision et son coût.

## 26. Plan d’implémentation

1. Étape
2. Étape
3. Étape

## 27. Tests et preuves attendues

- Test
- Prototype
- Projet pilote
- Mesure

## 28. Tâches liées

- `SF-TASK-...`

## 29. Conditions de révision

Réexaminer la décision si :

- condition ;
- condition.

## 30. Indicateurs

- indicateur ;
- seuil éventuel.

## 31. Références

- Document
- ADR
- Rapport
- Pull request

## 32. Décision humaine

- Décideur :
- Date :
- Résultat :

## 33. Conséquences observées

Section append-only après mise en œuvre.

## 34. Historique

| Date | Changement |
|---|---|
| YYYY-MM-DD | Proposition initiale |
```

---

# 72. Template simplifié

Pour une décision limitée mais durable :

```markdown
---
adrId: ADR-XXXX
title: Short title
status: proposed
date: YYYY-MM-DD
decisionOwners:
  - LogicGate
---

# ADR-XXXX — Short title

## Contexte

## Problème

## Drivers

## Options

### Option A

### Option B

## Décision

## Conséquences positives

## Conséquences négatives

## Risques

## Conditions de révision

## Références
```

---

## 72.1 Cas où le template simplifié est interdit

Le template complet est requis pour les décisions affectant :

- sécurité ;
- données ;
- Identity ;
- migrations ;
- infrastructure ;
- déploiement ;
- backup ;
- multi-client ;
- supply chain ;
- contrats publics majeurs ;
- changements de version majeure.

---

# 73. Exemple condensé

```markdown
---
adrId: ADR-0001
title: Use one repository and one database per client
status: proposed
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
  - ../02-architecture/core-module-boundary.md
targetVersion: 0.1.0
---

# ADR-0001 — Use one repository and one database per client

## Contexte

SiteFactory doit générer des applications PME pouvant être
personnalisées, déployées et transférées indépendamment.

Le MVP est exploité par une équipe réduite et privilégie
la sécurité, la lisibilité et la réversibilité.

## Problème

Comment isoler les données, les personnalisations et les cycles
de déploiement de plusieurs clients sans introduire dès le MVP
une plateforme multi-tenant complexe ?

## Drivers

1. Isolation des données
2. Limitation de l’impact croisé
3. Transfert client
4. Backups et restaurations
5. Simplicité de compréhension
6. Coût d’exploitation

## Options

### Option A — Application multi-tenant et base partagée

Avantages :

- mutualisation forte ;
- déploiement unique.

Inconvénients :

- isolation plus complexe ;
- risques d’accès croisé ;
- transfert client difficile ;
- migrations globales.

### Option B — Application et base isolées par client

Avantages :

- forte isolation ;
- backups indépendants ;
- transfert facilité ;
- déploiements indépendants.

Inconvénients :

- davantage de stacks ;
- davantage d’upgrades ;
- inventaire à maintenir.

### Option C — Application partagée et bases séparées

Avantages :

- données isolées ;
- application mutualisée.

Inconvénients :

- orchestration plus complexe ;
- cycle de release commun ;
- personnalisation limitée.

## Décision

Nous décidons d’utiliser un repository, une application,
une base PostgreSQL et des volumes isolés par client.

Le MVP ne proposera pas de multi-tenant.

## Conséquences positives

- isolation renforcée ;
- restauration indépendante ;
- transfert facilité ;
- personnalisation indépendante.

## Conséquences négatives

- davantage de déploiements ;
- coût opérationnel supérieur ;
- besoin d’automatiser les upgrades.

## Risques

- saturation d’un VPS mutualisé ;
- divergence de versions ;
- temps de maintenance multiplié.

## Mesures compensatoires

- inventaire des versions ;
- commandes `upgrade check` et `upgrade plan` ;
- monitoring des ressources ;
- possibilité de VPS dédié.

## Conditions de révision

Réexaminer la décision si :

- le nombre de clients rend la maintenance non soutenable ;
- une forte mutualisation métier devient nécessaire ;
- un besoin d’agrégation temps réel apparaît ;
- le coût d’exploitation dépasse les bénéfices d’isolation.
```

---

# 74. Historique

L’historique conserve uniquement les changements du document lui-même.

Exemple :

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-08-02 | Ajout de l’analyse de sécurité |
| 2026-08-05 | Statut passé à `accepted` |

---

## 74.1 Ne pas utiliser l’historique pour réécrire la décision

Une nouvelle décision utilise un nouvel ADR.

---

# 75. Automatisation future

Une commande future pourrait valider les ADR :

```bash
sitefactory repository validate-adrs
```

Elle pourrait vérifier :

- identifiant ;
- numéro ;
- front matter ;
- statut ;
- sections obligatoires ;
- références ;
- relations superseded ;
- placeholders ;
- doublons ;
- liens.

---

## 75.1 Limite de l’automatisation

La validation ne peut pas déterminer :

- si les options sont réellement pertinentes ;
- si les risques sont correctement évalués ;
- si la décision est commercialement acceptable ;
- si le décideur humain a suffisamment d’informations.

---

# 76. Règles pour LogicGate

LogicGate doit :

- identifier les décisions structurantes ;
- nommer un décideur ;
- challenger les hypothèses ;
- valider les compromis ;
- accepter ou rejeter ;
- maintenir les liens ;
- créer les tâches d’implémentation ;
- réexaminer les ADR lorsque leurs hypothèses deviennent invalides.

---

# 77. Règles pour Codex

Codex peut :

- analyser le contexte ;
- identifier les options ;
- préparer la comparaison ;
- relever les impacts ;
- rédiger une proposition ;
- mettre à jour les références ;
- préparer les tâches liées ;
- ajouter des tests de validation documentaire.

---

## 77.1 Codex ne doit pas

```text
Accepter un ADR

Choisir seul l’option finale

Modifier un ADR accepted pour changer la décision

Supprimer un ADR historique

Inventer des contraintes métier

Décider d’un prix

Décider d’une politique juridique

Appliquer une migration

Déployer

Publier une release

Présenter une hypothèse comme un fait
```

---

## 77.2 Décision humaine requise

Codex doit attendre une décision pour :

- statut `accepted` ;
- choix final ;
- compromis de sécurité ;
- risque accepté ;
- breaking change ;
- nouvelle dépendance structurante ;
- changement de technologie ;
- modification de données ;
- changement opérationnel majeur ;
- remplacement d’un ADR.

---

# 78. Compte-rendu Codex attendu

Lors de la préparation d’un ADR, Codex doit préciser :

```text
Problème étudié

Sources consultées

Hypothèses

Options identifiées

Drivers

Recommandation proposée

Avantages

Inconvénients

Risques

Éléments non vérifiés

Décisions humaines restantes
```

Codex ne doit pas déclarer l’ADR accepté.

---

# 79. Anti-patterns interdits

## 79.1 Décision sans alternatives

```text
Nous utilisons PostgreSQL parce que PostgreSQL est retenu.
```

---

## 79.2 Justification par popularité uniquement

```text
Tout le monde utilise cette technologie.
```

---

## 79.3 Décision rédigée après coup

L’ADR invente des raisons qui n’ont pas réellement guidé le choix.

---

## 79.4 Conséquences uniquement positives

Aucune limite ou charge opérationnelle n’est mentionnée.

---

## 79.5 ADR transformé en tutoriel

Le document contient principalement du code d’installation.

---

## 79.6 ADR transformé en tâche

Il détaille chaque fichier à modifier sans expliquer le choix.

---

## 79.7 Option artificiellement faible

Les alternatives sont caricaturées pour rendre le choix évident.

---

## 79.8 Statut accepté par Codex

L’agent attribue seul le statut final.

---

## 79.9 Ancien ADR supprimé

L’historique de la décision disparaît.

---

## 79.10 Ancien ADR réécrit

Le texte est modifié pour faire croire que la nouvelle décision avait toujours été retenue.

---

## 79.11 Pas de condition de révision

La décision est présentée comme définitive indépendamment du contexte.

---

## 79.12 Décision sans impact opérationnel

Une technologie est retenue sans analyser :

- déploiement ;
- monitoring ;
- backup ;
- maintenance ;
- transfert.

---

## 79.13 Décision client généralisée

Une contrainte propre à un client devient une règle SiteFactory sans analyse.

---

# 80. Quality gates d’un ADR

Un ADR ne peut pas passer à `accepted` si :

```text
Problème absent

Contexte insuffisant

Drivers absents

Une seule option crédible décrite sans justification

Décision ambiguë

Conséquences négatives absentes

Risques absents

Sécurité non analysée lorsqu’elle est affectée

Données non analysées lorsqu’elles sont affectées

Réversibilité absente

Conditions de révision absentes

Décideur humain absent

Secret ou donnée confidentielle présent
```

---

# 81. Codes documentaires recommandés

| Code | Signification |
|---|---|
| `SF-ADR-VALIDATION-001` | Identifiant absent |
| `SF-ADR-VALIDATION-002` | Statut invalide |
| `SF-ADR-VALIDATION-003` | Contexte absent |
| `SF-ADR-VALIDATION-004` | Problème absent |
| `SF-ADR-VALIDATION-005` | Drivers absents |
| `SF-ADR-VALIDATION-006` | Options insuffisantes |
| `SF-ADR-VALIDATION-007` | Décision ambiguë |
| `SF-ADR-VALIDATION-008` | Conséquences absentes |
| `SF-ADR-VALIDATION-009` | Risques absents |
| `SF-ADR-VALIDATION-010` | Sécurité non analysée |
| `SF-ADR-VALIDATION-011` | Données non analysées |
| `SF-ADR-VALIDATION-012` | Réversibilité absente |
| `SF-ADR-VALIDATION-013` | Condition de révision absente |
| `SF-ADR-VALIDATION-014` | Décideur absent |
| `SF-ADR-VALIDATION-015` | Relation superseded incohérente |
| `SF-ADR-VALIDATION-016` | Secret potentiel détecté |
| `SF-ADR-VALIDATION-017` | Placeholder non résolu |
| `SF-ADR-VALIDATION-018` | ADR accepté modifié de manière incompatible |

---

# 82. Critères d’acceptation du modèle

Le modèle sera considéré comme opérationnel lorsque :

1. les ADR possèdent un identifiant stable ;
2. les fichiers possèdent un numéro stable ;
3. les statuts sont définis ;
4. le cycle de vie est défini ;
5. les décisions nécessitant un ADR sont identifiées ;
6. la différence avec une tâche est définie ;
7. la différence avec un runbook est définie ;
8. le contexte est obligatoire ;
9. le problème est formulé sans supposer la solution ;
10. les decision drivers sont obligatoires ;
11. les contraintes sont explicites ;
12. les hypothèses sont explicites ;
13. plusieurs options crédibles sont examinées ;
14. les compromis sont documentés ;
15. les conséquences positives et négatives sont documentées ;
16. les risques sont documentés ;
17. la sécurité est analysée lorsque concernée ;
18. les données sont analysées lorsque concernées ;
19. l’exploitation est analysée ;
20. la réversibilité est analysée ;
21. les conditions de révision sont obligatoires ;
22. un template complet existe ;
23. un template simplifié existe ;
24. un exemple existe ;
25. les ADR remplacés restent conservés ;
26. le statut `accepted` nécessite une décision humaine ;
27. Codex ne peut pas accepter seul une décision ;
28. une automatisation future peut valider la structure.

---

# 83. Décisions validées par ce template

La baseline des ADR SiteFactory est la suivante :

```text
Les décisions structurantes sont consignées dans docs/adr.

Chaque ADR possède un numéro sur quatre chiffres.

Le fichier 0000 constitue le template et ne représente
pas une décision produit.

Les ADR utilisent les statuts proposed, accepted, rejected,
deprecated, superseded et withdrawn.

Une décision acceptée n’est pas réécrite silencieusement.

Une nouvelle décision incompatible produit un nouvel ADR.

Les anciens ADR restent dans le repository.

Un ADR distingue contexte, problème, drivers,
contraintes, options, décision et conséquences.

Le problème est formulé avant la solution.

Les alternatives crédibles sont étudiées honnêtement.

Les conséquences négatives sont obligatoires.

Les risques acceptés sont explicitement documentés.

La sécurité, les données et l’exploitation sont analysées
lorsqu’elles sont affectées.

La réversibilité est explicitement évaluée.

Chaque ADR possède des conditions de révision.

Les décisions temporaires possèdent une date
ou une condition de réexamen.

Un ADR accepté peut générer plusieurs tâches Codex.

Un ADR ne constitue pas une autorisation
d’exécuter une opération de production.

Les décisions spécifiques à un client restent
dans le repository client sauf généralisation validée.

Codex peut préparer et analyser un ADR.

Seul LogicGate peut accepter, rejeter
ou remplacer une décision structurante.
```

---

# 84. Checklist de rédaction

```text
[ ] ADR ID
[ ] Numéro de fichier
[ ] Titre
[ ] Statut
[ ] Date
[ ] Décideur
[ ] Résumé
[ ] Contexte
[ ] Problème
[ ] Portée
[ ] Hors portée
[ ] Drivers
[ ] Contraintes
[ ] Hypothèses
[ ] Options
[ ] Comparaison
[ ] Décision
[ ] Justification
[ ] Conséquences positives
[ ] Conséquences négatives
[ ] Risques
[ ] Mesures compensatoires
[ ] Sécurité
[ ] Données
[ ] Exploitation
[ ] Compatibilité
[ ] Réversibilité
[ ] Conditions de révision
[ ] Références
```

---

# 85. Checklist avant passage à `accepted`

```text
[ ] Le problème est réel et compris.
[ ] Les hypothèses sont explicites.
[ ] Les options sont crédibles.
[ ] Les options ne sont pas caricaturées.
[ ] Les drivers sont priorisés.
[ ] Les compromis sont compris.
[ ] Les conséquences négatives sont acceptées.
[ ] Les risques ont un propriétaire.
[ ] Les impacts sécurité sont acceptables.
[ ] Les impacts données sont acceptables.
[ ] Les opérations futures sont réalistes.
[ ] La décision est suffisamment réversible ou assumée.
[ ] Les conditions de révision sont définies.
[ ] LogicGate a explicitement validé la décision.
```

---

# 86. Checklist d’une décision remplacée

```text
[ ] Nouvel ADR créé
[ ] Ancien ADR marqué superseded
[ ] Nouvel ADR référence l’ancien
[ ] Ancien ADR référence le nouveau
[ ] Documentation d’architecture mise à jour
[ ] Tâches de migration créées
[ ] Compatibilité documentée
[ ] Ancien historique conservé
```

---

# 87. Checklist de revue périodique

```text
[ ] Hypothèses encore valides
[ ] Drivers encore prioritaires
[ ] Volumes réels conformes
[ ] Coûts acceptables
[ ] Risques observés
[ ] Incidents liés
[ ] Technologie encore supportée
[ ] Compétences disponibles
[ ] Réversibilité encore réaliste
[ ] Nouvel ADR nécessaire
```

---

# 88. Conclusion

Les ADR SiteFactory doivent conserver la logique des décisions structurantes, et pas uniquement leur résultat final.

Ils reposent sur :

```text
contexte
+ problème
+ drivers
+ options
+ compromis
+ décision
+ conséquences
+ risques
+ conditions de révision
```

Un bon ADR ne cherche pas à prouver qu’une décision était parfaite.

Il doit permettre de comprendre pourquoi elle était raisonnable dans son contexte et comment reconnaître le moment où elle devra évoluer.

> Le code montre ce que SiteFactory fait. Les ADR doivent expliquer pourquoi SiteFactory a été conçu de cette manière.
