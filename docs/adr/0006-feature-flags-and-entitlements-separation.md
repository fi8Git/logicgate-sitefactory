---
adrId: ADR-0006
title: Separate modules, entitlements, feature flags, authorization and operational kill switches
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
  - ../02-architecture/module-catalog.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ../03-generation/13-generation-pipeline.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/09-module-architecture.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ../04-quality-security/18-security-baseline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../04-quality-security/19-testing-strategy.md
  - ../05-operations/21-observability-and-logging.md
  - ../05-operations/24-operational-runbooks.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0002-yaml-client-configuration.md
  - ./0005-file-generation-strategies.md
targetVersion: 0.1.0
---

# ADR-0006 — Séparation des modules, entitlements, feature flags, autorisations et kill switches

## Lecture publique et état d'implémentation — 2026-10-29

Repère éditorial, sans modification du statut historique ni de la décision
normative. Les exemples et contrats cibles ci-dessous ne valent pas preuve
de livraison ; alternatives, conséquences et restrictions restent conservées.

La décision sépare composition, entitlement, feature flag, permission et
kill switch ; elle n'est pas une annonce de leur implémentation complète.
Le [SDK/catalogue public](../02-architecture/module-sdk.md) fournit des
métadonnées et capabilities statiques, pas une autorisation runtime ni un
catalogue commercial. Les contrats, services et workflows ci-dessous sont
cibles d'une future application cliente, sans dépendance obligatoire du Core
à une offre Business ou à un service central distant.
Aucun entitlement privé n'est activé ni exporté par la composition publique.

## 1. Statut

```text
accepted
```

LogicGate SiteFactory distingue explicitement cinq préoccupations :

```text
Module installé
Entitlement commercial
Feature flag technique
Autorisation utilisateur
Kill switch opérationnel
```

Ces mécanismes ne sont pas interchangeables.

Aucun mécanisme ne doit être utilisé pour contourner la responsabilité d’un autre.

En particulier :

```text
Un feature flag ne peut pas accorder un droit commercial.

Un rôle administrateur ne peut pas accorder un module non souscrit.

Un entitlement ne peut pas remplacer une permission utilisateur.

Un kill switch ne peut qu’interdire ou dégrader une capacité.

Une interface masquée ne constitue pas une autorisation.
```

---

## 2. Résumé

Une capacité SiteFactory est utilisable uniquement lorsque toutes les conditions applicables sont satisfaites.

Modèle conceptuel :

```text
Capability available =
    module installed
    AND entitlement granted
    AND technical feature enabled
    AND operational kill switch inactive
    AND user authorized
```

Pour une capacité publique anonyme, la dernière condition peut représenter une politique autorisant l’accès anonyme.

Les responsabilités sont les suivantes :

| Mécanisme | Question traitée |
|---|---|
| Module installé | Le code et les ressources de cette capacité existent-ils dans le projet ? |
| Entitlement | Le projet client possède-t-il contractuellement cette capacité ? |
| Feature flag | Cette capacité technique est-elle actuellement activée dans cet environnement ? |
| Autorisation | Cet utilisateur ou appelant a-t-il le droit de l’utiliser ? |
| Kill switch | Devons-nous désactiver temporairement cette capacité pour des raisons opérationnelles ou de sécurité ? |

Les entitlements sont résolus pendant la génération ou un upgrade explicite.

Les feature flags et kill switches sont des paramètres techniques séparés.

Les autorisations sont évaluées côté serveur à chaque requête ou cas d’usage concerné.

---

## 3. Contexte

SiteFactory assemble des projets clients à partir :

- de packs ;
- de modules ;
- de templates ;
- de thèmes ;
- d’options ;
- de personnalisations ;
- de services mensuels.

Certaines capacités relèvent directement du périmètre commercial du client.

Exemples :

- gestion avancée des réalisations ;
- formulaire de devis métier ;
- SEO local renforcé ;
- export avancé ;
- automatisation métier ;
- fonctions de gestion supplémentaires.

D’autres contrôles répondent à des préoccupations techniques différentes :

- activer progressivement une fonctionnalité ;
- tester une nouvelle implémentation en staging ;
- désactiver une intégration en panne ;
- restreindre une action à certains rôles ;
- interrompre temporairement un export vulnérable ;
- conserver du code déployé sans l’exposer immédiatement.

Sans séparation claire, un unique booléen tel que :

```text
Enabled
```

pourrait signifier indistinctement :

- vendu au client ;
- installé ;
- actif techniquement ;
- visible dans l’interface ;
- autorisé à l’utilisateur ;
- non désactivé par les opérations.

Cette ambiguïté créerait des risques commerciaux, fonctionnels et de sécurité.

---

## 4. Problème à résoudre

Comment représenter et évaluer la disponibilité d’une capacité sans mélanger :

- le périmètre contractuel du client ;
- la présence du module dans l’application ;
- le déploiement progressif d’une fonctionnalité ;
- les permissions des utilisateurs ;
- la désactivation d’urgence ;
- l’état d’un fournisseur externe ?

La solution doit empêcher notamment :

- l’activation d’une option payante par un simple flag ;
- le contournement d’une permission en affichant un bouton ;
- l’utilisation d’un rôle pour accorder un entitlement ;
- l’activation en production d’une preview de staging ;
- l’usage d’un kill switch comme mécanisme permanent ;
- l’accès à une route d’un module absent ;
- la suppression de données lors d’une désactivation ;
- les contrôles dispersés et contradictoires.

---

## 5. Portée

Cette décision couvre :

- modules SiteFactory ;
- capacités métier ;
- packs et options ;
- entitlements ;
- feature flags ;
- kill switches ;
- permissions ;
- rôles ;
- accès public et administrateur ;
- configuration client ;
- lock file ;
- configuration par environnement ;
- génération ;
- runtime ;
- UI ;
- endpoints ;
- cas d’usage Application ;
- observabilité ;
- upgrades ;
- tests ;
- exploitation.

---

## 6. Hors portée

Cette décision ne définit pas :

- tous les identifiants d’entitlements ;
- tous les rôles et permissions ;
- une plateforme distante de feature management ;
- une interface d’administration commerciale ;
- la facturation automatique ;
- un licensing server ;
- la gestion d’abonnement SaaS en temps réel ;
- les conditions juridiques de suspension d’un service ;
- un moteur AB testing ;
- un déploiement canary multi-instance ;
- le contenu détaillé du catalogue commercial.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Respect du périmètre commercial**
2. **Autorisation serveur fiable**
3. **Refus par défaut**
4. **Séparation des responsabilités**
5. **Capacité de désactivation d’urgence**
6. **Traçabilité**
7. **Cohérence entre UI et serveur**
8. **Compatibilité avec les projets isolés par client**
9. **Stabilité des identifiants**
10. **Testabilité**
11. **Déploiement progressif**
12. **Maintenabilité**
13. **Réduction de la dette liée aux flags**
14. **Simplicité opérationnelle du MVP**
15. **Compatibilité avec les upgrades explicites**

---

## 8. Contraintes

La décision doit respecter les contraintes suivantes :

```text
Un repository et une application par client

Configuration client en YAML

Composition exacte dans le lock file

Contrôles côté serveur

ASP.NET Core Identity

Rôles et permissions distincts

Aucun service central de licence requis au runtime

Aucune activation commerciale automatique

Aucun secret dans la configuration client

Compatibilité staging et production

Configuration typée et validée

Observabilité sans PII

Refus par défaut en cas d’incohérence
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le périmètre commercial d’un projet change relativement peu ;
- un changement d’entitlement peut passer par un upgrade explicite ;
- le MVP ne nécessite pas un service central de feature flags ;
- les feature flags peuvent initialement nécessiter un redémarrage contrôlé ;
- les kill switches peuvent être fournis par configuration opérationnelle ;
- les rôles et permissions sont stockés dans le périmètre Identity du client ;
- les modules déclarent leurs capacités et permissions ;
- la majorité des applications clientes utilise une seule instance web ;
- les données d’une capacité désactivée doivent généralement être conservées.

---

# 10. Définitions

## 10.1 Module installé

Un module est installé lorsque son code, ses contrats et ses ressources font partie de la composition générée du projet client.

Exemple :

```text
sample-records
portfolio-advanced
seo-local-enhanced
```

La présence d’un module est une décision structurelle.

Elle peut affecter :

- packages ;
- dépendances ;
- routes ;
- services ;
- modèles ;
- migrations ;
- tests ;
- opérations.

---

## 10.2 Entitlement

Un entitlement représente le droit contractuel d’un projet client à utiliser une capacité.

Il répond à la question :

```text
Cette capacité appartient-elle au périmètre acquis par ce client ?
```

Un entitlement peut provenir :

- du pack ;
- d’un module acheté ;
- d’une option ;
- d’une décision commerciale explicite ;
- d’un projet sur mesure.

---

## 10.3 Feature flag

Un feature flag représente l’état d’activation technique d’une capacité déjà autorisée par la composition et les entitlements.

Il répond à la question :

```text
Cette fonctionnalité est-elle techniquement active
dans cet environnement à cet instant ?
```

---

## 10.4 Autorisation

L’autorisation représente le droit d’un utilisateur ou d’un appelant à exécuter une action.

Elle répond à la question :

```text
Cet appelant peut-il utiliser cette capacité ?
```

Elle dépend notamment :

- de l’authentification ;
- des permissions ;
- des rôles ;
- du contexte de la ressource ;
- de la politique serveur.

---

## 10.5 Kill switch

Un kill switch est un contrôle opérationnel prioritaire permettant de désactiver temporairement une capacité.

Il répond à la question :

```text
Cette capacité doit-elle être immédiatement indisponible
malgré son activation normale ?
```

---

## 10.6 Capability

Une capability est une fonction métier ou technique identifiable et contrôlable.

Exemples :

```text
leads.read
leads.export
portfolio.advanced
seo.local-pages
automation.quote-routing
```

---

## 10.7 Module capability

Capacité fournie par un module donné.

---

## 10.8 Commercial capability

Capacité pouvant relever du périmètre vendu au client.

---

## 10.9 Technical capability

Capacité purement technique ne nécessitant pas nécessairement un entitlement commercial distinct.

---

# 11. Option A — Booléen unique `enabled`

## 11.1 Description

Chaque capacité possède un seul booléen :

```yaml
features:
  leadExport:
    enabled: true
```

Ce booléen contrôle :

- installation ;
- vente ;
- visibilité ;
- autorisation ;
- exploitation.

---

## 11.2 Avantages

- modèle simple ;
- peu de code ;
- configuration facile ;
- compréhension initiale rapide.

---

## 11.3 Inconvénients

- responsabilités mélangées ;
- impossible de distinguer droit commercial et activation technique ;
- impossible de distinguer client et utilisateur ;
- difficulté à désactiver temporairement ;
- risque de contournement ;
- audit insuffisant ;
- migrations ambiguës.

---

## 11.4 Risques

- capacité payante activée par erreur ;
- rôle utilisé comme licence ;
- bouton masqué sans contrôle serveur ;
- désactivation opérationnelle supprimant le périmètre contractuel ;
- réactivation involontaire après redémarrage.

---

## 11.5 Réversibilité

```text
faible à moyenne
```

À mesure que le produit évolue, le booléen accumulerait plusieurs sens difficiles à migrer.

---

# 12. Option B — Entitlements et autorisations uniquement

## 12.1 Description

Le système distingue :

- le droit du client ;
- le droit de l’utilisateur.

Il ne possède ni feature flags ni kill switches séparés.

---

## 12.2 Avantages

- séparation commerciale correcte ;
- sécurité utilisateur explicite ;
- modèle plus simple que cinq mécanismes ;
- peu de configuration opérationnelle.

---

## 12.3 Inconvénients

- aucune activation progressive ;
- impossibilité de désactiver rapidement une capacité sans modifier l’entitlement ;
- preview de staging difficile ;
- dépendance externe en panne difficile à isoler ;
- mélange entre contrat et incident.

---

## 12.4 Risques

- retrait temporaire d’un entitlement pour traiter un incident ;
- changement commercial inutile ;
- déploiement urgent pour désactiver une fonctionnalité ;
- impossibilité de distinguer non-souscription et panne technique.

---

## 12.5 Réversibilité

```text
moyenne
```

Des feature flags et kill switches pourraient être ajoutés, mais les comportements existants devraient être séparés.

---

# 13. Option C — Séparation explicite des cinq préoccupations

## 13.1 Description

Le système distingue :

```text
Module
Entitlement
Feature flag
Authorization
Kill switch
```

Une capability evaluator centrale combine ces informations.

---

## 13.2 Avantages

- responsabilités claires ;
- sécurité renforcée ;
- périmètre commercial protégé ;
- déploiement progressif ;
- désactivation d’urgence ;
- tests indépendants ;
- observabilité précise ;
- meilleure maintenance ;
- audit compréhensible.

---

## 13.3 Inconvénients

- modèle plus riche ;
- davantage de configuration ;
- besoin de conventions ;
- risque de multiplication des flags ;
- tests combinatoires ;
- nécessité d’un service d’évaluation central.

---

## 13.4 Risques

- incohérence entre sources ;
- mauvaise valeur par défaut ;
- flag jamais supprimé ;
- UI et serveur utilisant des évaluations différentes ;
- configuration opérationnelle oubliée.

---

## 13.5 Réversibilité

```text
élevée
```

Chaque préoccupation reste isolée et peut évoluer indépendamment.

---

# 14. Option D — Service distant central de licensing et feature flags

## 14.1 Description

Toutes les applications clientes interrogent un service central LogicGate pour déterminer :

- entitlements ;
- flags ;
- suspension ;
- kill switches ;
- éventuellement autorisations.

---

## 14.2 Avantages

- gestion centralisée ;
- activation rapide ;
- inventaire temps réel ;
- suspension immédiate ;
- reporting global ;
- campagnes de rollout.

---

## 14.3 Inconvénients

- dépendance runtime à LogicGate ;
- disponibilité supplémentaire requise ;
- latence ;
- sécurité du service central ;
- transfert client plus difficile ;
- complexité importante pour le MVP ;
- risque d’indisponibilité globale.

---

## 14.4 Risques

- panne centrale affectant plusieurs clients ;
- fuite de données commerciales ;
- erreur de tenant ;
- impossibilité pour un projet transféré de fonctionner ;
- verrouillage fournisseur ;
- mode offline complexe.

---

## 14.5 Réversibilité

```text
faible
```

Les applications deviendraient dépendantes d’un service runtime central.

---

# 15. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Booléen unique | Entitlement + auth | Séparation complète | Service central |
|---|---:|---:|---:|---:|---:|
| Séparation des responsabilités | 5 | 1 | 3 | 5 | 4 |
| Protection commerciale | 5 | 2 | 5 | 5 | 5 |
| Autorisation serveur | 5 | 2 | 5 | 5 | 4 |
| Kill switch | 4 | 2 | 1 | 5 | 5 |
| Rollout technique | 4 | 2 | 1 | 5 | 5 |
| Simplicité du MVP | 5 | 5 | 4 | 3 | 1 |
| Transfert client | 5 | 4 | 5 | 5 | 1 |
| Indépendance runtime | 5 | 5 | 5 | 5 | 1 |
| Audit | 4 | 2 | 3 | 5 | 5 |
| Maintenabilité | 4 | 2 | 3 | 5 | 3 |
| Réversibilité | 4 | 2 | 3 | 5 | 2 |

L’option C offre le meilleur équilibre pour le MVP SiteFactory.

---

# 16. Décision

Nous décidons de séparer explicitement :

```text
la composition structurelle
le droit commercial
l’activation technique
l’autorisation utilisateur
la désactivation opérationnelle
```

Le système utilisera les concepts suivants :

```text
Module manifest

Entitlement set

Feature flag set

Authorization policies

Kill switch set

Capability evaluator
```

---

## 16.1 Service central

Le MVP ne dépendra pas d’un service distant LogicGate pour évaluer les entitlements au runtime.

Le projet client doit pouvoir fonctionner de manière autonome.

---

## 16.2 Décision locale

La composition et les entitlements résolus sont contenus dans le repository et les artefacts du projet client.

---

# 17. Règle d’évaluation

L’évaluation conceptuelle est :

```text
if module is not installed:
    deny

if entitlement is required and not granted:
    deny

if feature flag is required and disabled:
    deny

if kill switch is active:
    deny

if caller is not authorized:
    deny

allow
```

---

## 17.1 Priorité du kill switch

Un kill switch actif refuse toujours l’accès à sa capacité, indépendamment :

- du rôle ;
- de l’entitlement ;
- du feature flag ;
- du statut administrateur.

---

## 17.2 Administrateur

Un administrateur ne contourne pas :

- un module absent ;
- un entitlement absent ;
- un feature flag désactivé ;
- un kill switch actif.

---

## 17.3 Feature flag

Un feature flag ne peut pas rendre disponible une capacité non installée ou non acquise.

---

## 17.4 Entitlement

Un entitlement ne suffit pas à autoriser un utilisateur spécifique.

---

# 18. Résultats d’évaluation

Le service central d’évaluation doit pouvoir produire un résultat structuré.

Valeurs conceptuelles :

```text
Available

ModuleNotInstalled

NotEntitled

FeatureDisabled

OperationallyDisabled

Unauthorized

InvalidConfiguration
```

---

## 18.1 Réponse publique

Le détail interne du refus ne doit pas nécessairement être exposé au navigateur.

Le mapping HTTP dépend :

- du type de route ;
- de la sensibilité ;
- du besoin de ne pas révéler une capacité ;
- de la politique d’autorisation.

---

## 18.2 Logs

Le résultat interne peut être journalisé avec :

- CapabilityId ;
- EvaluationResult ;
- ClientSlug ;
- Environment ;
- CorrelationId.

Aucune PII n’est nécessaire.

---

# 19. Module installé

## 19.1 Source

La présence du module provient :

- de `client.sitefactory.yml` ;
- du catalogue ;
- de la résolution ;
- de `sitefactory.lock.json` ;
- de la composition générée.

---

## 19.2 Absence

Un module absent ne doit normalement pas fournir :

- route ;
- service ;
- composant ;
- migration ;
- permission ;
- background job.

---

## 19.3 Module désactivable

Un module installé peut contenir plusieurs capabilities contrôlées par flags ou kill switches.

---

## 19.4 Retrait

Le retrait d’un module ne supprime pas automatiquement ses données.

---

# 20. Entitlements

## 20.1 Source de vérité

Les entitlements proviennent du périmètre commercial validé puis sont résolus pendant :

- la génération initiale ;
- un upgrade ;
- une modification explicite de configuration.

---

## 20.2 Fichiers

La configuration peut exprimer l’intention.

Le lock file conserve le résultat résolu.

Exemple conceptuel :

```yaml
entitlements:
  - portfolio.advanced
  - seo.local-enhanced
```

La syntaxe exacte appartient au schéma officiel.

---

## 20.3 Immutabilité runtime

Un utilisateur administrateur de l’application ne peut pas modifier les entitlements depuis l’administration fonctionnelle dans le MVP.

---

## 20.4 Modification

Une modification d’entitlement nécessite :

```text
décision commerciale
→ configuration mise à jour
→ nouveau plan
→ revue
→ upgrade ou régénération
→ nouvelle release cliente
```

---

## 20.5 Révocation

Une révocation est explicite et traçable.

Elle ne supprime pas automatiquement les données liées à la capacité.

---

## 20.6 Catalogue

Une nouvelle version du catalogue ne modifie pas les entitlements d’un projet existant.

---

## 20.7 Prix

La modification d’un prix ne modifie pas rétroactivement :

- le lock file ;
- le périmètre signé ;
- les entitlements ;
- les données.

---

# 21. Granularité des entitlements

## 21.1 Module entier

Un entitlement peut représenter un module complet.

Exemple :

```text
portfolio-advanced
```

---

## 21.2 Capability

Un entitlement peut représenter une capacité précise.

Exemple :

```text
leads.advanced-export
```

---

## 21.3 Éviter les entitlements techniques

Un entitlement ne doit pas représenter un détail purement technique tel que :

```text
use-new-sql-query-v2
```

Ce type de contrôle appartient aux feature flags.

---

## 21.4 Éviter la micro-fragmentation

Les entitlements ne doivent pas être créés pour chaque bouton ou détail mineur sans justification commerciale.

---

# 22. Feature flags

## 22.1 Objectif

Les feature flags permettent notamment :

- activation progressive ;
- validation en staging ;
- déploiement de code inactif ;
- remplacement progressif d’une implémentation ;
- activation après migration ;
- rollback fonctionnel rapide.

---

## 22.2 Source initiale

Dans le MVP, les feature flags sont fournis par une configuration technique :

- typée ;
- validée ;
- propre à l’environnement ;
- versionnée ou inventoriée ;
- sans service distant obligatoire.

---

## 22.3 Redémarrage

Une modification de flag peut nécessiter un redémarrage contrôlé de l’application dans le MVP.

Le comportement doit être explicite.

---

## 22.4 Aucun flag implicite

Un flag absent utilise une valeur par défaut documentée.

En cas d’identifiant inconnu ou incohérent, le système doit refuser ou signaler clairement l’erreur.

---

# 23. Types de feature flags

## 23.1 Release flag

Permet de masquer une nouvelle fonctionnalité pendant son déploiement.

---

## 23.2 Experiment flag

Permet une expérimentation limitée.

Les expérimentations avancées ne font pas partie du MVP.

---

## 23.3 Operational feature flag

Permet d’activer une variante technique préparée.

Il ne doit pas remplacer le kill switch d’urgence.

---

## 23.4 Migration flag

Permet une transition progressive après une migration compatible.

---

# 24. Cycle de vie d’un feature flag

États conceptuels :

```text
planned
introduced-disabled
enabled-in-staging
enabled-in-production
permanent
removed
```

---

## 24.1 Métadonnées obligatoires

Chaque flag doit posséder :

- identifiant ;
- propriétaire ;
- objectif ;
- valeur par défaut ;
- date d’introduction ;
- environnements autorisés ;
- condition de retrait ;
- version cible de nettoyage.

---

## 24.2 Flag permanent

Un flag devenu permanent doit être supprimé du code lorsque sa branche alternative n’est plus nécessaire.

---

## 24.3 Dette

Un flag sans propriétaire ou date de retrait constitue une dette technique.

---

# 25. Valeurs par défaut des feature flags

## 25.1 Nouvelle capacité visible

Une nouvelle capacité non indispensable utilise généralement :

```text
false
```

comme valeur initiale sûre.

---

## 25.2 Correction compatible

Une amélioration interne compatible peut ne pas nécessiter de flag.

---

## 25.3 Ancien comportement

Le maintien de deux comportements ne doit pas devenir permanent sans besoin réel.

---

## 25.4 Sécurité

Une protection de sécurité ne doit pas être désactivée par défaut.

---

# 26. Kill switches

## 26.1 Objectif

Un kill switch permet de désactiver rapidement :

- une fonctionnalité vulnérable ;
- une intégration défaillante ;
- un export dangereux ;
- une automatisation produisant des doublons ;
- un appel fournisseur coûteux ;
- une opération destructive.

---

## 26.2 Sens unique

Un kill switch est :

```text
disable-only
```

Il ne permet jamais d’activer une capability normalement indisponible.

---

## 26.3 Priorité

Le kill switch prévaut sur :

- feature flag ;
- entitlement ;
- rôle ;
- permission ;
- configuration fonctionnelle.

---

## 26.4 Données

L’activation d’un kill switch ne supprime aucune donnée.

---

## 26.5 Reprise

La désactivation du kill switch doit nécessiter :

- résolution de la cause ;
- validation ;
- tests ;
- audit.

---

# 27. Portée des kill switches

Portées possibles :

```text
application
module
capability
integration
background-job
environment
```

---

## 27.1 Portée globale

Un kill switch global ne doit être utilisé que pour une capacité clairement identifiée.

Éviter un bouton :

```text
disable-everything
```

sans runbook précis.

---

## 27.2 Par environnement

Staging et production possèdent des contrôles distincts.

---

## 27.3 Par client

Les projets clients isolés possèdent leurs propres kill switches.

---

# 28. Source des kill switches

Dans le MVP, les kill switches proviennent d’une configuration opérationnelle séparée du périmètre commercial.

Options acceptables :

- variable d’environnement ;
- configuration d’environnement ;
- secret manager pour une valeur sensible ;
- fichier opérationnel non committé lorsque justifié.

---

## 28.1 Pas dans le catalogue commercial

Le catalogue ne constitue pas la source runtime d’un kill switch.

---

## 28.2 Pas dans les rôles

Un rôle ne doit pas permettre d’activer ou désactiver un kill switch.

---

## 28.3 Administration future

Une interface opérationnelle future nécessiterait :

- permission dédiée ;
- audit ;
- confirmation ;
- expiration ;
- nouvel ADR ou extension explicite.

---

# 29. Expiration des kill switches

Un kill switch actif doit posséder :

- raison ;
- incident ou ticket ;
- propriétaire ;
- date d’activation ;
- prochaine revue ;
- condition de désactivation.

---

## 29.1 Alerte

Un kill switch actif au-delà de sa durée attendue doit produire une alerte ou une tâche.

---

## 29.2 Fonctionnalité définitivement retirée

Une capacité définitivement retirée doit être :

- supprimée ;
- dépréciée ;
- ou désinstallée ;

et non laissée indéfiniment derrière un kill switch.

---

# 30. Autorisation

## 30.1 Contrôle serveur

L’autorisation est appliquée côté serveur dans :

- endpoints ;
- handlers ;
- services Application ;
- opérations sensibles.

---

## 30.2 Interface

L’interface peut masquer ou désactiver une action, mais cette décision reste uniquement ergonomique.

---

## 30.3 Permission

Les permissions possèdent des identifiants stables.

Exemples conceptuels :

```text
Leads.Read

Leads.Export

Portfolio.Edit

Users.Manage
```

---

## 30.4 Rôles

Les rôles regroupent des permissions.

Ils ne contiennent pas directement la logique des entitlements ou feature flags.

---

## 30.5 Administrateur

Le rôle administrateur reste soumis aux entitlements et kill switches.

---

# 31. Autorisation des capacités publiques

Une capacité publique anonyme peut ne pas nécessiter de permission utilisateur.

Elle reste soumise à :

- présence du module ;
- entitlement ;
- feature flag ;
- kill switch ;
- rate limiting ;
- validation ;
- autres protections serveur.

---

## 31.1 Exemple

Un formulaire public peut être :

```text
module installed
AND entitlement granted
AND feature enabled
AND kill switch inactive
AND anonymous policy allowed
```

---

# 32. Service central d’évaluation

Le runtime doit utiliser un service central tel que :

```text
ICapabilityEvaluator
```

Le nom définitif peut évoluer.

---

## 32.1 Responsabilité

Le service combine :

- composition ;
- entitlements ;
- feature flags ;
- kill switches ;
- éventuellement le résultat d’autorisation.

---

## 32.2 Alternative de conception

L’autorisation ASP.NET Core peut rester un service distinct.

Le système doit néanmoins fournir une façade ou une politique cohérente évitant les contrôles dispersés.

---

## 32.3 Pas de booléens dispersés

Éviter :

```csharp
if (configuration.ExportEnabled &&
    user.IsAdmin &&
    moduleOptions.IsEnabled)
{
    ...
}
```

répété dans plusieurs couches.

---

# 33. Exemple conceptuel d’API

```csharp
public interface ICapabilityEvaluator
{
    CapabilityEvaluation Evaluate(
        CapabilityId capabilityId,
        ClaimsPrincipal user);
}
```

ou :

```csharp
public interface ICapabilityEvaluator
{
    ValueTask<CapabilityEvaluation> EvaluateAsync(
        CapabilityId capabilityId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}
```

La signature définitive dépendra de l’architecture d’autorisation retenue.

---

# 34. Evaluation result

Exemple conceptuel :

```csharp
public sealed record CapabilityEvaluation(
    CapabilityAvailability Availability,
    CapabilityId CapabilityId,
    string? ReasonCode);
```

Le résultat ne doit pas contenir :

- email ;
- rôle complet sérialisé ;
- données personnelles ;
- valeur de secret ;
- détail commercial non nécessaire.

---

# 35. Identifiants

## 35.1 Capability ID

Format recommandé :

```text
{module-id}.{capability}
```

Exemples :

```text
sample-records.read

sample-records.export

portfolio-advanced.manage

seo-local-enhanced.generate-pages
```

---

## 35.2 Entitlement ID

Peut correspondre :

- au module ;
- à une capability commerciale.

Il doit rester stable et documenté.

---

## 35.3 Feature flag ID

Format recommandé :

```text
{module-id}.{feature}.enabled
```

ou une convention équivalente centralisée.

Éviter les noms liés à une personne ou un ticket temporaire.

---

## 35.4 Kill switch ID

Format conceptuel :

```text
{module-id}.{capability}.disabled
```

La valeur et le nom doivent rendre le sens non ambigu.

---

## 35.5 Permission ID

Les permissions utilisent la convention Identity documentée.

Elles ne doivent pas être confondues avec les IDs commerciaux.

---

# 36. Stabilité des identifiants

Une fois publiés, les identifiants ne doivent pas être renommés sans :

- migration ;
- dépréciation ;
- compatibilité ;
- tests ;
- nouvelle version adaptée.

---

## 36.1 Nom commercial

Un changement de nom marketing ne nécessite pas de modifier l’identifiant technique.

---

## 36.2 Alias

Un alias temporaire peut être utilisé pendant une migration, avec une date de retrait.

---

# 37. Déclaration dans les modules

Chaque module doit déclarer :

```text
Capabilities

Required entitlements

Feature flags

Permissions

Kill switches

Default values

Dependencies
```

---

## 37.1 Exemple conceptuel

```yaml
module:
  id: sample-records
  version: "1.2.0"

capabilities:
  - id: sample-records.read
    entitlementRequired: sample-records
    permission: Leads.Read

  - id: sample-records.export
    entitlementRequired: sample-records.advanced-export
    featureFlag: sample-records.export.enabled
    killSwitch: sample-records.export.disabled
    permission: Leads.Export
```

La structure exacte appartient au schéma des manifests.

---

# 38. Validation de configuration

La génération doit refuser :

- entitlement inconnu ;
- capability inconnue ;
- feature flag inconnu ;
- kill switch inconnu ;
- permission inconnue ;
- entitlement pour un module absent ;
- flag ciblant une capability absente ;
- identifiant dupliqué ;
- dépendance incohérente.

---

## 38.1 Warning ou erreur

Les incohérences structurelles sont des erreurs.

Les flags devenus inutilisés peuvent produire un warning avant suppression.

---

# 39. Valeurs résolues

Le lock file doit conserver les éléments structurels résolus.

Exemple conceptuel :

```json
{
  "entitlements": [
    "sample-records",
    "sample-records.advanced-export"
  ],
  "capabilities": [
    "sample-records.read",
    "sample-records.export"
  ]
}
```

---

## 39.1 Flags runtime

Les valeurs opérationnelles propres à l’environnement ne doivent pas nécessairement être inscrites comme valeurs actives dans le lock file.

Le lock file peut néanmoins enregistrer :

- identifiants connus ;
- valeurs par défaut ;
- contrats ;
- versions.

---

# 40. Sources de vérité

| Élément | Source principale |
|---|---|
| Module installé | Configuration, catalogue et lock file |
| Entitlement | Configuration commerciale validée et lock file |
| Feature flag | Configuration technique de l’environnement |
| Autorisation | Identity, rôles, permissions et politiques serveur |
| Kill switch | Configuration opérationnelle de l’environnement |

---

## 40.1 Pas de duplication contradictoire

Une même décision ne doit pas être maintenue dans plusieurs sources concurrentes.

---

# 41. Environnements

## 41.1 Entitlements

Les entitlements représentent normalement le périmètre client et restent cohérents entre staging et production.

---

## 41.2 Preview

Une future capacité peut être testée en staging uniquement dans :

- une branche ;
- une configuration de preview ;
- une release candidate ;
- un environnement isolé.

Elle ne doit pas être activée en production par simple changement de flag si l’entitlement n’a pas été validé.

---

## 41.3 Feature flags

Les valeurs peuvent différer entre :

```text
development
test
staging
production
```

---

## 41.4 Kill switches

Les valeurs sont propres à chaque environnement.

---

## 41.5 Permissions

Les données Identity restent séparées par environnement.

---

# 42. Promotion staging vers production

Le passage staging vers production doit vérifier :

- entitlements identiques au périmètre prévu ;
- flags de preview désactivés ou validés ;
- kill switches attendus ;
- permissions seedées ;
- aucune capacité non acquise ;
- aucune configuration de test.

---

# 43. Interface utilisateur

## 43.1 Navigation

Une fonctionnalité indisponible ne doit pas apparaître comme utilisable.

---

## 43.2 Bouton désactivé

Un bouton peut être désactivé avec une explication lorsque le contexte le justifie.

---

## 43.3 Capacité non souscrite

L’administration fonctionnelle ne doit pas nécessairement afficher les détails commerciaux d’une capacité non acquise.

Le comportement dépend du produit.

---

## 43.4 Contrôle serveur

Même si un élément est absent de l’UI, l’endpoint ou le cas d’usage reste protégé.

---

# 44. Routes

## 44.1 Module absent

Une route propre à un module absent ne doit normalement pas être enregistrée.

---

## 44.2 Entitlement absent

Une route partagée peut refuser l’opération via l’évaluateur central.

---

## 44.3 Feature désactivée

L’opération est refusée ou remplacée par son comportement stable précédent selon le contrat du flag.

---

## 44.4 Kill switch actif

L’opération est refusée avec un diagnostic opérationnel contrôlé.

---

# 45. Background jobs

Les jobs doivent évaluer les mêmes contrôles applicables.

Un job ne doit pas continuer à exécuter une capability lorsque :

- son kill switch est actif ;
- son feature flag est désactivé ;
- son module est retiré ;
- son entitlement est révoqué.

---

## 45.1 Travail déjà en cours

La procédure doit définir si un job en cours :

- termine ;
- est annulé ;
- effectue un checkpoint ;
- est repris plus tard.

---

# 46. Intégrations externes

Une intégration peut posséder :

- entitlement ;
- feature flag ;
- kill switch ;
- credentials ;
- état fournisseur.

---

## 46.1 Fournisseur indisponible

Une panne fournisseur ne doit pas modifier l’entitlement.

---

## 46.2 Kill switch

Le kill switch permet de suspendre les appels tout en conservant :

- le contrat ;
- la configuration ;
- les données ;
- la capacité de reprise.

---

# 47. Données

## 47.1 Désactivation

Désactiver une capability ne supprime pas ses données.

---

## 47.2 Révocation d’entitlement

La révocation doit définir séparément :

- conservation ;
- export ;
- anonymisation ;
- suppression ;
- réactivation éventuelle.

---

## 47.3 Retrait de module

Le retrait de code et la suppression des données constituent deux opérations distinctes.

---

## 47.4 Lecture historique

Une capacité désactivée peut nécessiter un accès en lecture à ses données historiques.

Ce comportement doit être défini par le module.

---

# 48. Migrations

Une nouvelle capability peut nécessiter :

- code installé ;
- migration appliquée ;
- entitlement accordé ;
- feature flag encore désactivé ;
- activation après vérification.

---

## 48.1 Ordre recommandé

```text
déployer structure compatible
→ migrer
→ déployer code
→ valider
→ activer feature flag
```

L’ordre exact dépend du plan de migration.

---

## 48.2 Entitlement avant migration

Un entitlement ne doit pas rendre accessible une capability dont le schéma requis n’est pas encore disponible.

---

# 49. Upgrades

## 49.1 Nouvel entitlement

Une mise à niveau de module ne doit pas accorder automatiquement un nouvel entitlement commercial.

---

## 49.2 Nouveau feature flag

Le module doit définir :

- valeur par défaut ;
- objectif ;
- date de retrait ;
- compatibilité.

---

## 49.3 Nouveau kill switch

Le module doit fournir :

- portée ;
- valeur par défaut inactive ;
- runbook ;
- effet sur les opérations en cours.

---

## 49.4 Nouvelle permission

Une nouvelle permission n’est pas automatiquement accordée à tous les rôles sauf règle additive explicitement validée.

---

# 50. Suppression d’un flag

La suppression d’un flag nécessite :

1. confirmer le comportement permanent ;
2. supprimer la branche inactive ;
3. supprimer la configuration ;
4. supprimer les tests devenus inutiles ;
5. conserver les tests du comportement final ;
6. mettre à jour le manifest ;
7. mettre à jour la documentation.

---

# 51. Suppression d’un entitlement

Un entitlement public ne doit pas être supprimé silencieusement.

Il nécessite :

- dépréciation ;
- migration ;
- identification des projets concernés ;
- changement commercial validé ;
- nouvelle version compatible.

---

# 52. Cache

## 52.1 Entitlements

Les entitlements peuvent être chargés comme état immuable du processus dans le MVP.

---

## 52.2 Feature flags

Les flags peuvent être chargés au démarrage.

---

## 52.3 Kill switches

Les kill switches peuvent initialement nécessiter un redémarrage.

Un besoin de rechargement dynamique fera l’objet d’une évolution explicite.

---

## 52.4 Autorisation

Les permissions sont évaluées par requête ou cas d’usage selon Identity.

---

## 52.5 Fail-open interdit

Une erreur de chargement ou de cache ne doit pas rendre une capacité disponible par défaut.

---

# 53. Redémarrage

Une modification de configuration technique nécessitant un redémarrage doit suivre :

- changement tracé ;
- recréation du conteneur ;
- health checks ;
- smoke tests ;
- audit.

---

# 54. Observabilité

Événements recommandés :

```text
CapabilityEvaluationDenied

EntitlementMissing

FeatureFlagDisabled

OperationalKillSwitchActive

CapabilityAuthorizationDenied

FeatureFlagConfigurationInvalid

KillSwitchActivated

KillSwitchDeactivated
```

---

## 54.1 Propriétés autorisées

```text
CapabilityId

ModuleId

EntitlementId

FeatureFlagId

KillSwitchId

EvaluationResult

ClientSlug

Environment

CorrelationId
```

---

## 54.2 Données interdites

```text
Email

Nom utilisateur

Données métier

Token

Secret

Corps de requête

Prix contractuel
```

---

## 54.3 Volume de logs

Un refus attendu ne doit pas créer un volume excessif de logs.

Les niveaux sont adaptés :

- Debug ou Information pour certains refus normaux ;
- Warning pour configuration inattendue ;
- Error pour incohérence ou défaillance.

---

# 55. Audit

Les opérations suivantes doivent être auditées lorsqu’elles sont modifiables :

- entitlement ajouté ;
- entitlement retiré ;
- feature flag modifié ;
- kill switch activé ;
- kill switch désactivé ;
- permission ajoutée à un rôle ;
- permission retirée ;
- capacité refusée pour incohérence.

---

## 55.1 Identité opérateur

L’audit utilise un identifiant technique d’opérateur sans journaliser inutilement ses données personnelles.

---

# 56. Alertes

Alertes possibles :

```text
Kill switch actif trop longtemps

Feature flag de preview actif en production

Entitlement inconnu

Flag sans propriétaire

Flag arrivé à expiration

Capability installée sans contrat complet

Permission obligatoire absente

Configuration différente du lock file
```

---

# 57. Sécurité

## 57.1 Menaces principales

- activation commerciale par flag ;
- contrôle uniquement client-side ;
- administrateur contournant un entitlement ;
- flag inconnu traité comme actif ;
- kill switch désactivé sans audit ;
- incohérence staging/production ;
- capability appelée directement ;
- rôle recevant implicitement une nouvelle permission ;
- configuration modifiée hors Git ;
- absence de refus en cas d’erreur.

---

## 57.2 Contrôles

- évaluateur central ;
- validation au démarrage ;
- refus par défaut ;
- contrôle serveur ;
- sources distinctes ;
- identifiants stables ;
- audit ;
- tests de contournement ;
- configuration typée ;
- aucun entitlement modifiable depuis l’administration fonctionnelle ;
- kill switch disable-only.

---

# 58. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Activation d’un module payant par variable d’environnement | L’entitlement reste requis |
| Utilisateur appelant directement l’API | Autorisation serveur |
| Administrateur contournant un kill switch | Kill switch prioritaire |
| Module absent mais route appelée | Route non enregistrée ou refus |
| Flag absent | Valeur sûre et validation |
| Configuration falsifiée | Git, CI, lock file et validation |
| Permission UI uniquement | Test endpoint direct |
| Désactivation fournisseur prise pour révocation commerciale | Kill switch séparé |

---

# 59. Tests unitaires

Tester la matrice d’évaluation.

## 59.1 Module absent

```text
module absent
+ entitlement présent
+ flag actif
+ permission présente
→ refus
```

---

## 59.2 Entitlement absent

```text
module présent
+ entitlement absent
+ flag actif
+ permission présente
→ refus
```

---

## 59.3 Flag désactivé

```text
module présent
+ entitlement présent
+ flag désactivé
+ permission présente
→ refus
```

---

## 59.4 Kill switch actif

```text
toutes les autres conditions satisfaites
+ kill switch actif
→ refus
```

---

## 59.5 Permission absente

```text
capability disponible
+ utilisateur non autorisé
→ refus
```

---

## 59.6 Toutes conditions satisfaites

```text
module présent
+ entitlement présent
+ flag actif
+ kill switch inactif
+ permission présente
→ disponible
```

---

# 60. Tests de priorité

Vérifier que :

- l’administrateur ne contourne pas l’entitlement ;
- l’administrateur ne contourne pas le kill switch ;
- un flag ne contourne pas un module absent ;
- une permission ne contourne pas un entitlement ;
- un entitlement ne contourne pas l’autorisation ;
- une erreur de configuration refuse l’accès.

---

# 61. Tests d’intégration

Tester :

- endpoint direct ;
- handler Application ;
- UI ;
- background job ;
- configuration ;
- changement d’environnement ;
- seed de permissions ;
- module absent ;
- capability désactivée.

---

# 62. Tests d’autorisation

Vérifier au minimum :

```text
utilisateur anonyme

utilisateur authentifié sans permission

utilisateur avec permission

administrateur

compte désactivé

rôle modifié
```

---

# 63. Tests de contournement

Les tests doivent appeler directement :

- route ;
- endpoint ;
- handler ;
- service ;

sans passer par l’interface.

---

# 64. Tests de configuration

Tester :

- entitlement inconnu ;
- entitlement dupliqué ;
- feature flag inconnu ;
- kill switch inconnu ;
- permission inconnue ;
- capability dupliquée ;
- module absent ;
- valeur par défaut ;
- configuration future non supportée.

---

# 65. Tests staging et production

Vérifier :

- preview active seulement en staging ;
- preview refusée en production lorsque non validée ;
- kill switches indépendants ;
- secrets et configurations séparés ;
- entitlements cohérents avec le lock file.

---

# 66. Tests de données

Vérifier que :

- désactiver un flag ne supprime aucune donnée ;
- activer un kill switch ne supprime aucune donnée ;
- retirer un entitlement ne déclenche pas une suppression implicite ;
- réactiver une capability retrouve ses données selon le contrat.

---

# 67. Tests de module

Chaque module doit tester :

- déclaration des capabilities ;
- entitlements requis ;
- flags ;
- kill switches ;
- permissions ;
- valeurs par défaut ;
- incohérences.

---

# 68. Tests d’upgrade

Tester notamment :

```text
ancienne version sans nouveau flag
→ valeur par défaut sûre

nouvelle capability
→ entitlement non accordé automatiquement

nouvelle permission
→ rôle non élargi silencieusement

flag devenu permanent
→ suppression contrôlée
```

---

# 69. Tests d’observabilité

Vérifier :

- EventIds ;
- raison interne ;
- absence de PII ;
- absence de prix ;
- absence de token ;
- niveau de log ;
- absence de spam.

---

# 70. Quality gates

Une génération ou release est bloquée lorsque :

```text
Capability sans identifiant

Entitlement inconnu

Flag inconnu

Kill switch inconnu

Permission inconnue

Module absent requis

Identifiant dupliqué

Valeur par défaut absente

Feature flag sans propriétaire

Kill switch sans runbook lorsque requis

UI protégée sans contrôle serveur

Nouvel entitlement accordé implicitement
```

---

## 70.1 Production

Le déploiement est bloqué si :

```text
Flag de preview non approuvé actif

Configuration d’entitlement différente du lock file

Kill switch incohérent

Validation de capability en échec

Permissions obligatoires absentes
```

---

# 71. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-CAPABILITY-001` | Capability inconnue |
| `SF-CAPABILITY-002` | Capability dupliquée |
| `SF-CAPABILITY-003` | Module non installé |
| `SF-CAPABILITY-004` | Entitlement requis absent |
| `SF-CAPABILITY-005` | Feature flag désactivé |
| `SF-CAPABILITY-006` | Kill switch actif |
| `SF-CAPABILITY-007` | Appelant non autorisé |
| `SF-CAPABILITY-008` | Configuration incohérente |
| `SF-ENTITLEMENT-001` | Entitlement inconnu |
| `SF-ENTITLEMENT-002` | Entitlement dupliqué |
| `SF-ENTITLEMENT-003` | Entitlement incompatible avec le module |
| `SF-ENTITLEMENT-004` | Modification runtime interdite |
| `SF-FEATURE-001` | Feature flag inconnu |
| `SF-FEATURE-002` | Valeur de flag invalide |
| `SF-FEATURE-003` | Flag de preview interdit en production |
| `SF-FEATURE-004` | Flag expiré |
| `SF-FEATURE-005` | Propriétaire de flag absent |
| `SF-KILLSWITCH-001` | Kill switch inconnu |
| `SF-KILLSWITCH-002` | Configuration de kill switch invalide |
| `SF-KILLSWITCH-003` | Désactivation non autorisée |
| `SF-KILLSWITCH-004` | Kill switch actif au-delà de la durée prévue |
| `SF-AUTHORIZATION-001` | Permission inconnue |
| `SF-AUTHORIZATION-002` | Permission requise absente |

---

# 72. Configuration conceptuelle

Exemple illustratif :

```yaml
modules:
  - id: sample-records

entitlements:
  - sample-records
  - sample-records.advanced-export

features:
  sample-records.export.enabled: false
```

Les kill switches et valeurs propres à la production restent dans la configuration opérationnelle de l’environnement.

---

## 72.1 Pas de secret

Aucune de ces valeurs ne constitue un secret.

Leur modification reste néanmoins sensible sur le plan fonctionnel et commercial.

---

# 73. Configuration opérationnelle conceptuelle

Exemple indicatif :

```text
SiteFactory__KillSwitches__SampleRecordsExportDisabled=true
```

La convention définitive doit éviter les doubles négations ambiguës.

Une représentation structurée est préférable lorsque l’outillage le permet.

---

## 73.1 Nommage clair

Éviter :

```text
ExportEnabled=false
```

lorsqu’il est impossible de savoir s’il s’agit :

- d’un entitlement ;
- d’un feature flag ;
- d’un kill switch.

---

# 74. Runbooks

Un kill switch important doit posséder un runbook précisant :

- symptôme ;
- raison d’activation ;
- impact ;
- opérations interrompues ;
- données conservées ;
- vérification ;
- condition de désactivation ;
- communication éventuelle.

---

# 75. Déploiement

Le déploiement doit enregistrer :

- version de l’application ;
- entitlements résolus ;
- identifiants de flags connus ;
- valeurs des flags non sensibles ;
- kill switches actifs ;
- validation des permissions.

Le rapport ne doit pas exposer de données contractuelles inutiles.

---

# 76. Releases

Une release doit documenter :

- nouvelles capabilities ;
- nouveaux entitlements ;
- nouveaux flags ;
- nouveaux kill switches ;
- nouvelles permissions ;
- valeurs par défaut ;
- migrations ;
- actions manuelles.

---

# 77. Maintenance

La maintenance périodique doit vérifier :

- flags expirés ;
- flags permanents non nettoyés ;
- kill switches actifs ;
- entitlements obsolètes ;
- permissions inutilisées ;
- capabilities sans tests ;
- incohérences avec le lock file.

---

# 78. Transfert client

Un transfert complet doit préciser :

- modules installés ;
- entitlements acquis ;
- feature flags existants ;
- kill switches ;
- permissions ;
- rôles ;
- configuration opérationnelle ;
- responsabilités de modification.

---

## 78.1 Feed ou service central

Le projet transféré ne doit pas perdre ses entitlements uniquement parce que l’accès à LogicGate est interrompu.

---

# 79. Conséquences positives

La décision apporte :

- protection claire du périmètre commercial ;
- sécurité utilisateur indépendante ;
- capacité de rollout ;
- kill switch d’urgence ;
- audit précis ;
- meilleure lisibilité ;
- réduction des contrôles dispersés ;
- tests structurés ;
- autonomie du projet client ;
- meilleure gestion des upgrades ;
- séparation entre incident et contrat ;
- conservation des données lors d’une désactivation.

---

# 80. Conséquences négatives

La décision entraîne :

- modèle plus complexe ;
- nombreux identifiants ;
- configuration supplémentaire ;
- service d’évaluation ;
- tests combinatoires ;
- besoin de nettoyer les flags ;
- risque de mauvaise configuration ;
- documentation et manifests plus détaillés ;
- formation opérateur ;
- rapports supplémentaires.

---

# 81. Risques acceptés

## 81.1 Dette de flags

Des flags peuvent rester après leur utilité.

Réduction :

- propriétaire ;
- échéance ;
- maintenance périodique ;
- quality gate.

---

## 81.2 Configuration divergente

Staging et production peuvent diverger involontairement.

Réduction :

- rapports ;
- validation ;
- promotion contrôlée ;
- tests.

---

## 81.3 Évaluation dispersée

Des contrôles peuvent être recodés localement.

Réduction :

- service central ;
- tests d’architecture ;
- conventions ;
- revue.

---

## 81.4 Complexité du modèle

Les développeurs peuvent confondre les mécanismes.

Réduction :

- terminologie stable ;
- documentation ;
- exemples ;
- API typée ;
- codes d’erreur.

---

# 82. Mesures compensatoires

- identifiants stables ;
- manifeste de module ;
- évaluateur central ;
- refus par défaut ;
- configuration typée ;
- validation au démarrage ;
- lock file ;
- logs structurés ;
- audit ;
- tests de contournement ;
- nettoyage des flags ;
- runbooks de kill switches ;
- contrôles CI.

---

# 83. Sécurité

## 83.1 Actifs

- capacités commerciales ;
- données métier ;
- opérations administratives ;
- intégrations ;
- exports ;
- automatisations ;
- comptes ;
- permissions.

---

## 83.2 Frontières de confiance

- configuration du repository ;
- configuration d’environnement ;
- utilisateur ;
- Identity ;
- opérateur ;
- fournisseur externe.

---

## 83.3 Refus par défaut

Toute incohérence doit conduire à :

```text
deny
```

et non à :

```text
allow
```

---

# 84. Performance

## 84.1 Évaluation locale

L’évaluation utilise des données locales et typées dans le MVP.

Aucun appel réseau n’est requis.

---

## 84.2 Coût

Le coût de l’évaluation doit rester faible et compatible avec une vérification par requête.

---

## 84.3 Cache

Les collections immuables peuvent être préparées au démarrage.

---

## 84.4 Optimisation

Une optimisation supplémentaire doit être basée sur des mesures.

---

# 85. Coûts

## 85.1 Coûts supplémentaires

- modélisation ;
- manifests ;
- configuration ;
- tests ;
- audit ;
- maintenance des flags ;
- formation.

---

## 85.2 Coûts évités

- fonctionnalités payantes activées par erreur ;
- contournements d’autorisation ;
- incidents nécessitant un redéploiement urgent ;
- suppression de données lors d’une désactivation ;
- dépendance à un service de licence central ;
- logique conditionnelle dispersée.

---

# 86. Maintenabilité

La décision impose une séparation claire :

```text
commerce
≠ technique
≠ sécurité utilisateur
≠ exploitation
```

---

## 86.1 Nouvelle capacité

Toute nouvelle capability doit répondre à cinq questions :

```text
Quel module la fournit ?

Quel entitlement la gouverne éventuellement ?

Quel feature flag la contrôle éventuellement ?

Quelle permission est nécessaire ?

Quel kill switch est prévu éventuellement ?
```

---

## 86.2 Valeur non applicable

Tous les mécanismes ne sont pas obligatoires pour chaque capability.

Une capacité peut, par exemple :

- ne nécessiter aucun entitlement séparé ;
- ne nécessiter aucun feature flag ;
- ne nécessiter aucun kill switch spécifique ;
- être publique.

Ces absences doivent être explicites.

---

# 87. Compatibilité

## 87.1 Lock file

Le schéma du lock file devra supporter les entitlements et capabilities résolus.

---

## 87.2 Configuration

Le schéma YAML devra distinguer clairement les entitlements des paramètres techniques.

---

## 87.3 Identity

Les permissions restent compatibles avec le modèle Identity retenu.

---

## 87.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline initiale.

La migration d’un ancien booléen générique vers les cinq mécanismes peut constituer un breaking change de configuration.

---

# 88. Migration depuis un booléen générique

Si un prototype utilise :

```yaml
advancedPortfolioEnabled: true
```

la migration doit déterminer explicitement s’il représente :

- un module ;
- un entitlement ;
- un feature flag ;
- un kill switch ;
- une simple option fonctionnelle.

---

## 88.1 Pas de mapping automatique universel

Le sens doit être déterminé par la documentation et le produit.

---

## 88.2 Étapes

```text
inventorier les booléens
→ classifier leur responsabilité
→ créer les identifiants
→ migrer la configuration
→ migrer le code
→ ajouter les tests
→ supprimer l’ancien booléen
```

---

## 88.3 Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation de modifier les configurations ou données d’un projet réel.

---

# 89. Réversibilité

## 89.1 Classification

```text
élevée
```

Les responsabilités séparées peuvent être adaptées indépendamment.

---

## 89.2 Simplification future

Certains contrôles peuvent être supprimés lorsqu’ils deviennent inutiles, par exemple un feature flag devenu permanent.

---

## 89.3 Retour au booléen unique

Un retour vers un booléen générique réduirait les garanties et nécessiterait un nouvel ADR.

---

# 90. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `CapabilityId` ;
- `EntitlementId` ;
- `FeatureFlagId` ;
- `KillSwitchId` ;
- résultats d’évaluation.

---

## Étape 2 — Manifests

Étendre les manifests de modules avec :

- capabilities ;
- entitlements ;
- flags ;
- permissions ;
- kill switches.

---

## Étape 3 — Configuration

Étendre :

```text
client.sitefactory.yml
```

et son schéma.

---

## Étape 4 — Lock file

Enregistrer :

- modules ;
- capabilities ;
- entitlements résolus ;
- contrats techniques.

---

## Étape 5 — Evaluator

Créer un service central d’évaluation.

---

## Étape 6 — Autorisation

Intégrer les politiques ASP.NET Core et les permissions.

---

## Étape 7 — UI et endpoints

Appliquer les contrôles :

- navigation ;
- composants ;
- endpoints ;
- handlers ;
- jobs.

---

## Étape 8 — Kill switches

Ajouter la configuration opérationnelle et les runbooks.

---

## Étape 9 — Tests

Ajouter :

- truth table ;
- contournements ;
- intégration ;
- upgrades ;
- environnements.

---

## Étape 10 — Maintenance

Ajouter :

- inventaire ;
- expiration ;
- alertes ;
- nettoyage des flags.

---

# 91. Tâches liées proposées

```text
SF-TASK-050 — Define capability and entitlement identifiers

SF-TASK-051 — Extend module manifests with capabilities

SF-TASK-052 — Add entitlement resolution to generation

SF-TASK-053 — Persist resolved entitlements in the lock file

SF-TASK-054 — Implement the capability evaluator

SF-TASK-055 — Integrate capability evaluation with authorization

SF-TASK-056 — Add environment feature flag configuration

SF-TASK-057 — Add operational kill switches

SF-TASK-058 — Add capability evaluation tests

SF-TASK-059 — Add feature flag lifecycle validation

SF-TASK-060 — Add kill switch operational runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 92. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Un module absent refuse sa capability.

Un entitlement absent refuse la capability.

Un flag désactivé refuse la capability.

Un kill switch actif refuse la capability.

Une permission absente refuse la capability.

Un administrateur ne contourne pas l’entitlement.

Un administrateur ne contourne pas le kill switch.

Un flag ne peut pas accorder un entitlement.

L’UI et l’endpoint appliquent un résultat cohérent.

Un appel direct est refusé.

Une configuration inconnue échoue en sécurité.

Les entitlements sont enregistrés dans le lock file.

Les entitlements ne sont pas modifiables depuis l’admin fonctionnelle.

Les flags staging et production sont isolés.

Une désactivation ne supprime aucune donnée.

Les logs ne contiennent aucune PII.

Les flags expirés sont détectés.

Les kill switches actifs sont visibles opérationnellement.
```

---

# 93. Conditions de révision

Réexaminer cette décision si :

- SiteFactory devient une plateforme SaaS centralisée ;
- les entitlements doivent être modifiés en temps réel ;
- un service de facturation pilote le runtime ;
- une activation immédiate sans release devient nécessaire ;
- les projets clients utilisent plusieurs instances distribuées ;
- un système de feature management externe est adopté ;
- les kill switches doivent fonctionner sans redémarrage ;
- le nombre de flags devient difficile à maintenir ;
- les besoins d’expérimentation deviennent importants ;
- une fédération d’identité modifie les autorisations ;
- le transfert client exige une autre gestion des licences.

---

# 94. Indicateurs à suivre

- nombre de capabilities ;
- nombre d’entitlements ;
- nombre de feature flags actifs ;
- nombre de flags expirés ;
- durée moyenne de vie d’un flag ;
- nombre de kill switches actifs ;
- durée d’activation des kill switches ;
- refus par type ;
- erreurs de configuration ;
- incidents de capacité mal activée ;
- permissions manquantes ;
- incohérences staging/production ;
- entitlements modifiés par upgrade ;
- flags supprimés après stabilisation.

---

# 95. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des entitlements sont modifiés directement en production

des features commerciales sont contrôlées uniquement par flags

des endpoints contournent l’évaluateur central

des administrateurs accèdent à des capacités non souscrites

des kill switches restent actifs sans propriétaire

des flags expirés s’accumulent

des valeurs staging atteignent la production

des permissions sont accordées automatiquement à tous les rôles

des données sont supprimées lors d’une désactivation

des contrôles UI et serveur divergent
```

---

# 96. Conséquences observées

Cette section est append-only après mise en œuvre.

## 96.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation du modèle centralisé
de capabilities et entitlements.
```

Les observations futures devront préciser :

- date ;
- version ;
- module ;
- capability ;
- mécanisme concerné ;
- incident ou bénéfice ;
- action proposée.

---

# 97. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- `09-module-architecture.md`
- `10-feature-flags-and-entitlements.md`
- [0020-administrator-session-security-and-step-up-authentication.md](0020-administrator-session-security-and-step-up-authentication.md)
- `13-generation-pipeline.md`
- `14-client-configuration-schema.md`
- `18-security-baseline.md`
- `19-testing-strategy.md`
- `21-observability-and-logging.md`
- `24-operational-runbooks.md`
- `25-upgrade-and-maintenance-strategy.md`
- `26-release-and-versioning.md`
- `28-codex-operating-guide.md`
- `0000-adr-template.md`
- `0001-one-repository-and-database-per-client.md`
- `0002-yaml-client-configuration.md`
- `0005-file-generation-strategies.md`

---

# 98. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 93 |

---

# 99. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 100. Synthèse de la décision

```text
Module installé
→ le code et les ressources existent

Entitlement
→ le client possède la capacité

Feature flag
→ la capacité technique est activée

Autorisation
→ l’utilisateur peut effectuer l’action

Kill switch
→ les opérations peuvent désactiver la capacité

Disponibilité
→ toutes les conditions applicables sont satisfaites

Priorité de refus
→ module absent
→ entitlement absent
→ feature désactivée
→ kill switch actif
→ utilisateur non autorisé

Règles invariantes
→ un flag n’accorde jamais un entitlement
→ un rôle n’accorde jamais un entitlement
→ un administrateur ne contourne pas un kill switch
→ l’UI ne remplace jamais le contrôle serveur
→ une désactivation ne supprime jamais automatiquement les données
```

> SiteFactory sépare ce que le client a acquis, ce que le projet contient, ce que la technique active, ce que l’utilisateur peut faire et ce que les opérations doivent temporairement arrêter. Cette séparation évite qu’un simple booléen devienne à la fois une licence, une permission et un mécanisme de sécurité.
