# Architecture Decision Records — LogicGate SiteFactory

## 1. Objet

Ce répertoire contient les **Architecture Decision Records**, ou ADR, du projet LogicGate SiteFactory.

Un ADR documente une décision structurante ayant un impact durable sur :

- l’architecture ;
- la sécurité ;
- les données ;
- la génération ;
- les modules ;
- les déploiements ;
- l’exploitation ;
- la maintenabilité ;
- la portabilité des projets clients.

Les ADR permettent de conserver :

```text
le contexte de la décision

le problème rencontré

les options étudiées

la décision retenue

les conséquences positives et négatives

les risques acceptés

les contrôles nécessaires

les conditions de révision
```

Ils constituent la source de vérité des décisions architecturales du projet.

### Portée de l'index public

Cet index référence les décisions destinées à la documentation publique,
pas un inventaire de fonctionnalités déjà livrées. Le Core, ModuleSdk,
DocumentationModule, la CLI et les bibliothèques runtime/PostgreSQL génériques
constituent le périmètre actuel décrit par le [README public](../../README.md)
et [CONTRIBUTING](../../CONTRIBUTING.md). Leur build et leurs tests ne requièrent
aucune source, fixture, DLL, secret ou infrastructure Business.

Les décisions relatives à Identity, l'administration, aux formulaires, à l'audit,
à la CSP ou à l'exploitation prescrivent aussi des capacités futures. Leur
statut accepté n'est ni une preuve d'implémentation, ni une autorisation de
publication ou d'action de production. Les packages et le véritable dépôt public
ne sont pas encore publiés ; SourceLink public reste PENDING dans ce futur dépôt.
La [CI publique](../04-quality-security/20-quality-gates-and-ci.md) distingue
la preuve acquise sur scope public dans le dépôt de préparation privé des
vérifications encore nécessaires après création du véritable dépôt public.

Le statut historique de chaque ADR est distinct de l'état de préparation de sa
documentation : cette mise à jour n'accepte, ne remplace et ne clôture aucune
décision par elle-même. Aucun lien vers une gouvernance privée n'est nécessaire
pour lire les décisions ci-dessous.

---

## 2. État du socle architectural

Le répertoire de décisions destinées au public comprend actuellement :

| Type | Nombre |
|---|---:|
| Template ADR | 1 |
| ADR acceptés | 39 |
| ADR proposés | 0 |
| ADR dépréciés | 0 |
| ADR remplacés | 0 |
| Total de fichiers ADR, hors cet index | 40 |

Les 32 décisions `0001` à `0032` constituent la baseline MVP historique,
complétée par les sept décisions `0033` à `0039` acceptées le 2026-10-02.
Le template `0000` n'est pas compté comme une décision proposée supplémentaire.
Les statuts et dates des fichiers sources restent inchangés.

Le socle MVP historique était considéré comme :

```text
architectural baseline complete
```

Cette complétude de décisions ne signifie pas que toutes les capacités décrites
soient implémentées, publiées ou validées en production.

La baseline couvre notamment :

- l’isolation par client ;
- la configuration déclarative ;
- PostgreSQL ;
- les migrations ;
- la génération déterministe ;
- les modules ;
- les customizations ;
- la supply chain ;
- la sécurité ;
- les données ;
- les jobs ;
- les intégrations ;
- les conteneurs ;
- les API ;
- le cache ;
- la validation du runtime ;
- le déploiement ;
- le rollback.

À partir de `ADR-0032`, de nouveaux ADR ne doivent être ajoutés que lorsqu’un besoin concret apparaît ou qu’une décision existante doit être révisée.

---

## 3. Rôle des ADR dans SiteFactory

Les ADR ne sont pas de simples notes de conception.

Ils servent de contrat entre :

```text
la vision produit

l’architecture

les tâches d’implémentation

les revues de code

les tests

les quality gates

les opérations de production

les procédures de handover
```

Une implémentation ne doit pas contourner un ADR accepté sans décision humaine explicite.

Lorsqu’un ADR accepté et le code divergent :

```text
ne pas considérer automatiquement le code comme la nouvelle vérité

identifier la divergence

déterminer si le code est incorrect
ou si la décision doit évoluer

corriger le code
ou créer un nouvel ADR
```

---

## 4. Ordre de priorité documentaire

Lorsqu’une ambiguïté existe, utiliser l’ordre suivant :

1. ADR accepté applicable ;
2. décision humaine explicite plus récente ;
3. documents de vision et d’architecture ;
4. spécification de tâche ;
5. documentation du module ;
6. implémentation actuelle ;
7. comportement historique non documenté.

Une implémentation existante ne remplace pas silencieusement une décision acceptée.

---

# 5. Index des ADR

## 5.1 Template

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0000](./0000-adr-template.md) | Template | Structure de référence utilisée pour rédiger un nouvel ADR |

---

## 5.2 Isolation, configuration et persistance

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0001](./0001-one-repository-and-database-per-client.md) | Accepted | Un repository, une application, une base PostgreSQL et un déploiement isolés par client |
| [ADR-0002](./0002-yaml-client-configuration.md) | Accepted | Configuration client déclarative dans `client.sitefactory.yml` |
| [ADR-0003](./0003-postgresql-as-primary-database.md) | Accepted | PostgreSQL comme base de données principale |
| [ADR-0004](./0004-explicit-production-migrations.md) | Accepted | Migrations de production explicites, planifiées et séparées du démarrage |
| [ADR-0005](./0005-file-generation-strategies.md) | Accepted | Stratégies de génération de fichiers, ownership et préservation des customizations |

---

## 5.3 Modules, droits fonctionnels et extensions

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0006](./0006-feature-flags-and-entitlements-separation.md) | Accepted | Séparation entre feature flags techniques et entitlements commerciaux |
| [ADR-0007](./0007-module-composition-and-public-contracts.md) | Accepted | Composition des modules et contrats publics stables |
| [ADR-0008](./0008-private-package-feed-and-supply-chain.md) | Accepted | Packages privés, provenance et sécurisation de la supply chain |
| [ADR-0009](./0009-client-customizations-and-extension-points.md) | Accepted | Extension points contrôlés et isolation des customizations client |

---

## 5.4 Observabilité, déploiement et configuration runtime

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0010](./0010-structured-observability-with-opentelemetry.md) | Accepted | Logs structurés, traces, métriques et OpenTelemetry |
| [ADR-0011](./0011-docker-compose-vps-deployment-profile.md) | Accepted | Profil de déploiement Linux VPS, Docker Compose, PostgreSQL et Caddy |
| [ADR-0012](./0012-runtime-configuration-and-secrets.md) | Accepted | Configuration runtime et gestion des secrets |
| [ADR-0013](./0013-local-git-initialization-and-explicit-remote-publishing.md) | Accepted | Initialisation Git locale et publication distante explicite |
| [ADR-0014](./0014-explicit-client-upgrades-and-version-pinning.md) | Accepted | Versions client épinglées et upgrades explicites |
| [ADR-0015](./0015-deterministic-generation-and-optional-ai.md) | Accepted | Génération déterministe et usage optionnel de l’intelligence artificielle |

---

## 5.5 Contenus, médias et traitements asynchrones

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0016](./0016-content-and-media-storage-boundaries.md) | Accepted | Frontières de stockage entre configuration, contenu PostgreSQL et médias |
| [ADR-0017](./0017-background-jobs-and-reliable-side-effects.md) | Accepted | Jobs durables, outbox, idempotence et effets externes fiables |

---

## 5.6 Sécurité publique et administration

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0018](./0018-public-form-abuse-protection-and-rate-limiting.md) | Accepted | Protection des formulaires publics, anti-abus et rate limiting |
| [ADR-0019](./0019-audit-trail-and-sensitive-administrative-actions.md) | Accepted | Piste d’audit append-only et actions administratives sensibles |
| [ADR-0020](./0020-administrator-session-security-and-step-up-authentication.md) | Accepted | Sessions administrateur, MFA et step-up authentication |
| [ADR-0021](./0021-administrator-provisioning-invitations-and-account-recovery.md) | Accepted | Provisioning administrateur, invitations et récupération de compte |
| [ADR-0022](./0022-browser-security-headers-and-content-security-policy.md) | Accepted | Headers navigateur, CSP et profils de sécurité par route |

---

## 5.7 Confidentialité, portabilité et intégrations

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0023](./0023-data-retention-deletion-and-privacy-lifecycle.md) | Accepted | Conservation, suppression, anonymisation et cycle de vie des données |
| [ADR-0024](./0024-client-data-export-and-portability.md) | Accepted | Export structuré, portabilité et handover des données client |
| [ADR-0025](./0025-third-party-integrations-and-egress-control.md) | Accepted | Intégrations tierces, contrôle d’egress et prévention SSRF |

---

## 5.8 Conteneurs et sécurité PostgreSQL

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0026](./0026-container-image-build-and-runtime-hardening.md) | Accepted | Construction des images, runtime non-root et durcissement des conteneurs |
| [ADR-0027](./0027-database-roles-and-runtime-least-privilege.md) | Accepted | Rôles PostgreSQL spécialisés et moindre privilège |
| [ADR-0028](./0028-database-transaction-boundaries-and-concurrency-control.md) | Accepted | Transactions, concurrence optimiste, verrous et idempotence |

---

## 5.9 Contrats HTTP, cache et exploitation

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0029](./0029-http-api-contracts-problem-details-and-idempotent-commands.md) | Accepted | Contrats HTTP, Problem Details, ETags et commandes idempotentes |
| [ADR-0030](./0030-cache-strategy-and-invalidation.md) | Accepted | Cache mémoire borné, versions, epochs et invalidation post-commit |
| [ADR-0031](./0031-application-startup-validation-and-runtime-compatibility.md) | Accepted | Validation du démarrage, readiness et compatibilité du runtime |
| [ADR-0032](./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md) | Accepted | Rollout, maintenance, migrations, smoke tests et rollback sécurisé |

---

## 5.10 Distribution publique, modules et frontières de repositories

| ADR | Statut | Objet |
|---|---|---|
| [ADR-0033](./0033-split-public-sitefactory-and-private-logicgate-modules.md) | Accepté | Séparer SiteFactory public des modules LogicGate privés |
| [ADR-0034](./0034-apache-licence-choice-for-publish-site-factory.md) | Accepté | Publier SiteFactory sous licence Apache-2.0 |
| [ADR-0035](./0035-partage-public-component-via-nuget.md) | Accepté | Distribuer les composants publics SiteFactory via NuGet |
| [ADR-0036](./0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md) | Accepté | Versionner SiteFactory et ses modules avec SemVer et une compatibilité explicite |
| [ADR-0037](./0037-compose-modules-explicitly-without-dynamic-loading.md) | Accepté | Composer les modules explicitement sans chargement dynamique |
| [ADR-0038](./0038-make-the-public-ci-autonomous-and-the-private-ci-consuming.md) | Accepté | Rendre la CI publique autonome et la CI privée consommatrice |
| [ADR-0039](./0039-publish-sitefactory-from-a-new-sanitized-git-repository.md) | Accepté | Publier SiteFactory depuis un nouveau repository Git assaini |

La licence du code original public ne relicencie automatiquement ni les modules
propriétaires ni les sorties générées ou contenus tiers. Les contrats publics
versionnés et la composition statique ne nécessitent pas de package déjà publié.
**Clarification humaine du 2026-11-09** :
[l'amendement de portée 0008](0008-private-package-feed-and-supply-chain.md#amendement-de-portée--2026-11-09)
et [la clarification réciproque 0035](0035-partage-public-component-via-nuget.md#clarification-de-portée--2026-11-09)
admettent exactement `LogicGate.SiteFactory.ModuleSdk` au public ; tout autre
ID public exige une admission explicite. Le préfixe commun ne classe pas la
confidentialité, les autres packages internes restent privés sans fallback.
L'exemple initial de mapping est qualifié historique, pas configuration actuelle
du consommateur mixte. Sources, versions/locks, provenance et sécurité de
publication restent obligatoires ; preuve du mapping/cache isolé/substitution
encore à exécuter, aucun package déclaré publié. Statuts et compteurs inchangés.

---

# 6. Parcours de lecture recommandés

## 6.1 Comprendre le produit et l’architecture générale

Lire dans cet ordre :

```text
ADR-0001
→ ADR-0002
→ ADR-0003
→ ADR-0005
→ ADR-0006
→ ADR-0007
→ ADR-0009
→ ADR-0014
→ ADR-0015
```

Ce parcours permet de comprendre :

- l’isolation client ;
- la configuration ;
- la génération ;
- les modules ;
- les customizations ;
- les upgrades ;
- la place optionnelle de l’IA.

---

## 6.2 Implémenter le runtime applicatif

Lire dans cet ordre :

```text
ADR-0003
→ ADR-0004
→ ADR-0012
→ ADR-0016
→ ADR-0017
→ ADR-0027
→ ADR-0028
→ ADR-0029
→ ADR-0030
→ ADR-0031
```

Ce parcours couvre :

- PostgreSQL ;
- les migrations ;
- la configuration runtime ;
- les données ;
- les jobs ;
- les rôles ;
- les transactions ;
- les API ;
- le cache ;
- le démarrage.

---

## 6.3 Implémenter la sécurité

Lire dans cet ordre :

```text
ADR-0008
→ ADR-0012
→ ADR-0018
→ ADR-0019
→ ADR-0020
→ ADR-0021
→ ADR-0022
→ ADR-0023
→ ADR-0025
→ ADR-0026
→ ADR-0027
```

Ce parcours couvre :

- la supply chain ;
- les secrets ;
- l’anti-abus ;
- l’audit ;
- l’authentification ;
- le provisioning ;
- la sécurité navigateur ;
- la confidentialité ;
- l’egress ;
- les conteneurs ;
- PostgreSQL.

---

## 6.4 Implémenter les opérations de production

Lire dans cet ordre :

```text
ADR-0010
→ ADR-0011
→ ADR-0012
→ ADR-0013
→ ADR-0014
→ ADR-0017
→ ADR-0023
→ ADR-0024
→ ADR-0026
→ ADR-0027
→ ADR-0031
→ ADR-0032
```

Ce parcours couvre :

- l’observabilité ;
- Docker Compose ;
- les secrets ;
- Git ;
- les upgrades ;
- les jobs ;
- les données ;
- le handover ;
- les images ;
- PostgreSQL ;
- la readiness ;
- le rollout et le rollback.

---

## 6.5 Préparer un handover client

Lire dans cet ordre :

```text
ADR-0001
→ ADR-0012
→ ADR-0013
→ ADR-0014
→ ADR-0016
→ ADR-0023
→ ADR-0024
→ ADR-0025
→ ADR-0026
→ ADR-0027
→ ADR-0031
→ ADR-0032
```

---

## 6.6 Contribuer au moteur public ou à un module externe

Lire d'abord les décisions de frontière et de distribution :

```text
ADR-0033
→ ADR-0034
→ ADR-0035
→ ADR-0036
→ ADR-0037
→ ADR-0038
→ ADR-0039
```

Elles imposent l'autonomie du public, les contrats/packages versionnés, la
composition explicite sans chargement dynamique, la séparation des pipelines et
un nouvel historique public assaini. Elles ne donnent aucune autorisation
implicite de publier, d'exporter ou de lancer une release.

---

# 7. Relations structurantes entre ADR

## 7.1 Génération et personnalisation

```text
ADR-0002 — configuration YAML
→ ADR-0005 — génération de fichiers
→ ADR-0007 — composition des modules
→ ADR-0009 — customizations
→ ADR-0015 — génération déterministe
```

---

## 7.2 Persistance et exécution

```text
ADR-0003 — PostgreSQL
→ ADR-0004 — migrations explicites
→ ADR-0027 — rôles PostgreSQL
→ ADR-0028 — transactions
→ ADR-0031 — validation de compatibilité
→ ADR-0032 — rollout
```

---

## 7.3 Effets externes

```text
ADR-0017 — jobs et outbox
→ ADR-0025 — intégrations tierces
→ ADR-0028 — transaction avant effet externe
→ ADR-0029 — commandes idempotentes
→ ADR-0032 — drainage des jobs
```

---

## 7.4 Identité et administration

```text
ADR-0019 — audit
→ ADR-0020 — sessions et step-up
→ ADR-0021 — provisioning et récupération
→ ADR-0022 — sécurité navigateur
```

---

## 7.5 Confidentialité et suppression

```text
ADR-0016 — stockage
→ ADR-0023 — cycle de vie des données
→ ADR-0024 — export et portabilité
→ ADR-0030 — invalidation des caches
→ ADR-0031 — restore pending
→ ADR-0032 — réouverture après restore
```

---

## 7.6 Supply chain et production

```text
ADR-0008 — packages et supply chain
→ ADR-0026 — images signées
→ ADR-0031 — manifest de compatibilité
→ ADR-0032 — déploiement par digest
```

---

# 8. Statuts autorisés

Les valeurs recommandées sont :

```text
proposed

accepted

rejected

deprecated

superseded
```

---

## 8.1 `proposed`

La décision est en cours de discussion.

Elle ne constitue pas encore une règle obligatoire.

---

## 8.2 `accepted`

La décision est adoptée.

Les implémentations nouvelles doivent la respecter.

---

## 8.3 `rejected`

L’option décrite a été étudiée mais n’a pas été retenue.

---

## 8.4 `deprecated`

La décision ne doit plus être utilisée pour de nouvelles implémentations.

Elle peut rester présente pour documenter l’historique.

---

## 8.5 `superseded`

La décision a été remplacée par un nouvel ADR.

Le champ :

```yaml
supersededBy:
```

doit pointer vers son remplaçant.

---

# 9. Convention de nommage

Le nom d’un ADR utilise :

```text
NNNN-description-en-kebab-case.md
```

Exemple :

```text
0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
```

Règles :

- numéro sur quatre chiffres ;
- identifiant jamais réutilisé ;
- titre descriptif ;
- nom de fichier en anglais ;
- contenu rédigé en français pour la baseline actuelle ;
- aucune renumérotation après publication.

---

# 10. Création d’un nouvel ADR

Utiliser :

```text
0000-adr-template.md
```

Puis :

1. choisir le prochain identifiant disponible ;
2. copier le template ;
3. définir le contexte ;
4. formuler le problème ;
5. comparer plusieurs options ;
6. expliciter la décision ;
7. décrire les conséquences ;
8. ajouter les risques et contrôles ;
9. ajouter les tests et quality gates ;
10. associer les tâches ;
11. obtenir une décision humaine ;
12. mettre à jour cet index.

---

## 10.1 Condition de création

Créer un ADR lorsqu’une décision :

- affecte plusieurs modules ;
- modifie un invariant de sécurité ;
- change la persistance ;
- modifie un contrat public ;
- engage durablement l’exploitation ;
- introduit une nouvelle dépendance Infrastructure ;
- modifie la stratégie de déploiement ;
- modifie la portabilité ;
- change une décision acceptée ;
- serait coûteuse à inverser.

---

## 10.2 ADR non nécessaire

Un ADR n’est généralement pas nécessaire pour :

- une correction de bug locale ;
- un renommage sans impact ;
- une refactorisation interne ;
- une implémentation déjà complètement définie par un ADR ;
- un choix facilement réversible dans un seul fichier ;
- une modification purement éditoriale.

---

# 11. Modification d’un ADR accepté

## 11.1 Correction éditoriale

Une correction peut être faite directement lorsqu’elle ne change pas la décision :

- faute ;
- lien ;
- format ;
- clarification sans nouvelle règle.

Ajouter une ligne dans l’historique lorsque la correction est significative.

---

## 11.2 Changement de décision

Un changement architectural important ne doit pas réécrire silencieusement l’ancien ADR.

Créer un nouvel ADR qui :

- référence l’ancien ;
- explique le nouveau contexte ;
- décrit la nouvelle décision ;
- marque l’ancien comme `superseded` ou `deprecated`.

---

## 11.3 Conséquences observées

La section :

```text
Conséquences observées
```

est append-only après l’implémentation.

Elle sert à documenter :

- bénéfices réels ;
- incidents ;
- limites ;
- coûts ;
- révisions proposées.

---

# 12. Definition of Done d’un ADR

Un ADR est complet lorsqu’il contient au minimum :

```text
metadata

statut

contexte

problème

portée

hors portée

decision drivers

contraintes

options évaluées

comparaison

décision

invariants

conséquences positives

conséquences négatives

risques acceptés

mesures compensatoires

sécurité

tests

quality gates

règles pour les modules

règles pour les customizations

règles pour LogicGate

règles pour Codex

plan d’implémentation

tâches proposées

conditions de révision

décision humaine

historique
```

---

# 13. Utilisation dans les tâches d’implémentation

Une tâche SiteFactory doit référencer les ADR applicables.

Exemple :

```markdown
## ADR applicables

- ADR-0017
- ADR-0027
- ADR-0028
- ADR-0031
```

La tâche doit distinguer :

```text
ce qui est déjà décidé par les ADR

ce qui reste à décider humainement

ce qui appartient uniquement à l’implémentation
```

Une tâche ne doit pas demander à Codex de redéfinir une décision déjà acceptée.

---

# 14. Utilisation dans les pull requests

Une pull request doit indiquer :

```text
ADR respectés

ADR affectés

écarts éventuels

nouvel ADR nécessaire ou non
```

Exemple :

```markdown
## Architecture

ADR respectés :

- ADR-0027
- ADR-0028

Dérogation :

- aucune
```

---

## 14.1 Écart détecté

Une PR est bloquée lorsque :

- elle enfreint un invariant ;
- elle ajoute une dérogation non documentée ;
- elle désactive un quality gate ;
- elle modifie un contrat structurel sans mise à jour documentaire ;
- elle ajoute une nouvelle dépendance majeure sans décision.

---

# 15. Utilisation par Codex

Codex doit :

1. lire la tâche ;
2. identifier les ADR cités ;
3. lire les ADR applicables ;
4. appliquer leurs invariants ;
5. respecter leurs interdictions ;
6. produire les tests attendus ;
7. signaler toute contradiction ;
8. ne pas prendre une décision humaine à la place de LogicGate.

Codex ne doit pas :

```text
ignorer un ADR accepté

inventer une dérogation

modifier un ADR pour faire correspondre un code incorrect

supprimer un quality gate

exécuter une action de production

considérer une tâche comme autorisation de déploiement
```

---

# 16. Utilisation pendant les revues de code

Une revue doit vérifier :

- conformité avec les invariants ;
- respect des frontières ;
- respect des rôles ;
- absence d’anti-patterns interdits ;
- présence des tests ;
- respect des quality gates ;
- absence de secrets ;
- compatibilité avec les procédures de production.

Une revue peut utiliser le format :

```text
Conforme

Partiellement conforme

Non conforme

ADR non applicable

Décision complémentaire requise
```

---

# 17. Dérogations

Une dérogation à un ADR accepté doit être :

```text
explicite

limitée

justifiée

temporaire lorsque possible

approuvée humainement

associée à une tâche corrective
```

Elle doit préciser :

- ADR concerné ;
- invariant concerné ;
- raison ;
- périmètre ;
- durée ;
- risques ;
- mesures compensatoires ;
- propriétaire ;
- date d’expiration ;
- tâche de suppression de la dérogation.

---

## 17.1 Interdictions

Une dérogation ne peut pas être créée par :

- simple commentaire de code ;
- variable `IGNORE_SECURITY=true` ;
- modification locale non commitée ;
- choix autonome de Codex ;
- manipulation directe de production.

---

# 18. Invariants non dérogeables du socle MVP

Les invariants suivants ne doivent pas être contournés dans la baseline :

```text
isolation par client et environnement

aucune migration automatique au démarrage

aucun secret dans Git ou dans une image

aucun runtime PostgreSQL superuser ou owner

aucun conteneur applicatif root ou privileged

aucun Docker socket dans l’application

aucune validation TLS désactivée

aucun appel vers une URL utilisateur arbitraire

aucune mutation HTTP par GET

aucun audit sensible modifiable par le runtime

aucun restore rouvert avant réconciliation

aucun déploiement production par tag latest

aucun rollback vers un schéma incompatible
```

Une évolution nécessitant de revenir sur l’un de ces invariants exige un nouvel ADR et une revue de sécurité complète.

---

# 19. ADR et production

Les ADR décrivent les règles et procédures.

Ils ne constituent jamais à eux seuls une autorisation :

- de déployer ;
- de migrer ;
- de restaurer ;
- de supprimer des données ;
- de rotater un secret ;
- de modifier un rôle ;
- d’activer un mode maintenance ;
- de publier une image ;
- de contacter un client ou fournisseur.

Les actions de production exigent toujours :

```text
une tâche ou opération explicite

les permissions nécessaires

les confirmations requises

les plans et rapports attendus

une décision humaine
```

---

# 20. Vérification documentaire

Le répertoire ADR doit être vérifié par la CI.

Contrôles recommandés :

```text
numéros uniques

noms de fichiers valides

metadata présentes

statuts valides

liens relatifs valides

supersedes cohérents

supersededBy cohérents

targetVersion présente

decisionOwners présents

historique présent

décision humaine présente

index à jour
```

---

## 20.1 Markdown

Les ADR doivent respecter les règles Markdown du repository.

---

## 20.2 Liens

Un lien vers un ADR inexistant bloque la CI.

---

## 20.3 Index

Un fichier ADR absent de cet index doit être signalé.

Un index référençant un fichier absent doit être bloquant.

---

# 21. Revue périodique

Les ADR ne nécessitent pas tous une revue calendaire systématique.

Une revue doit être déclenchée lorsque :

- les hypothèses ne sont plus vraies ;
- un trigger de révision est atteint ;
- une technologie structurante change ;
- un incident révèle une limite ;
- un projet client exige un autre profil ;
- le MVP passe à plusieurs instances ;
- un nouveau provider central est ajouté ;
- PostgreSQL devient managé ;
- Kubernetes est introduit ;
- une API publique est lancée.

---

# 22. État de maturité du socle MVP

Les ADR `0001` à `0032` décrivent une architecture volontairement plus robuste qu’un simple générateur de site.
Le portrait ci-dessous conserve ce contexte historique ; les décisions `0033`
à `0039` étendent désormais le moteur modulaire à un périmètre public autonome,
sans dépendance du public vers les modules propriétaires. Il ne prouve pas
l'implémentation des capacités futures d'application ou d'exploitation.

Ils définissent SiteFactory comme :

```text
une usine logicielle interne

déclarative

modulaire

sécurisée

déterministe

testable

versionnée

exploitable

transférable
```

La prochaine étape n’est plus d’ajouter des décisions générales sans besoin concret.

La prochaine étape consiste à :

```text
prioriser les capacités du MVP

transformer les décisions en backlog

implémenter verticalement

tester sur un premier projet généré

observer les conséquences réelles

mettre à jour les ADR à partir des résultats
```

---

# 23. Décisions futures possibles

Les sujets suivants pourront nécessiter un nouvel ADR lorsqu’ils deviendront concrets :

```text
cache distribué ou Redis

déploiement multi-instance

Kubernetes

PostgreSQL managé

réplication ou haute disponibilité

API publique OAuth/OIDC

CDN

moteur de recherche

stockage objet externe

paiement en ligne

multi-région

traitements GPU

RLS ou multi-tenant

signature réglementée des exports

archivage légal

application Blazor WebAssembly distincte
```

Ils ne doivent pas être décidés prématurément pour le MVP.

---

# 24. Journal synthétique des décisions

| Période | Décisions principales |
|---|---|
| ADR-0001 à ADR-0005 | Isolation client, configuration, PostgreSQL, migrations et génération |
| ADR-0006 à ADR-0009 | Modules, entitlements, packages et customizations |
| ADR-0010 à ADR-0015 | Observabilité, déploiement VPS, secrets, Git, upgrades et IA |
| ADR-0016 à ADR-0017 | Stockage des contenus, médias, jobs et effets externes |
| ADR-0018 à ADR-0022 | Anti-abus, audit, sessions, provisioning et sécurité navigateur |
| ADR-0023 à ADR-0025 | Privacy, portabilité et intégrations tierces |
| ADR-0026 à ADR-0028 | Conteneurs, rôles PostgreSQL, transactions et concurrence |
| ADR-0029 à ADR-0032 | API, cache, startup validation, rollout et rollback |
| ADR-0033 à ADR-0039 | Frontière public/privé, licence, NuGet, SemVer, composition statique, CI autonome et nouvel historique public |

---

# 25. Responsabilité

| Élément | Responsable |
|---|---|
| Décision architecturale | LogicGate |
| Rédaction initiale | LogicGate, avec assistance éventuelle |
| Validation | Décideur humain LogicGate |
| Mise en œuvre | Développeur ou agent assigné |
| Revue de conformité | Reviewer humain ou workflow de revue |
| Mise à jour de l’index | Auteur de l’ADR |
| Suivi des conséquences | Équipe projet ou opérateur |
| Dérogation | Décideur humain autorisé |

---

# 26. Historique de cet index

| Date | Changement |
|---|---|
| 2026-07-30 | Création de l’index des ADR |
| 2026-07-30 | Référencement du template et des ADR-0001 à ADR-0032 |
| 2026-07-30 | Déclaration du socle architectural MVP comme complet |
| 2026-11-09 | Mise à jour de l'index public : ADR-0033 à ADR-0039, 39 décisions acceptées, distinction décisions/capacités livrées et statuts historiques préservés |
| 2026-11-09 | Réconciliation humaine de la portée ADR-0008/0035 : admission publique par ID exact, sans nouvelle décision numérotée ni changement de statut/compteur |

---

# 27. Synthèse

```text
39 ADR acceptés

dont les 32 décisions de la baseline MVP historique

1 template

baseline architecturale MVP historique complète

pas une déclaration de fonctionnalités toutes implémentées

aucune décision générale supplémentaire
sans besoin concret

ADR = source de vérité architecturale

implémentation = conformité aux ADR

écart = correction ou nouvelle décision

production = toujours explicite et humaine

prochaine étape = backlog priorisé et implémentation
```

> Les ADR de SiteFactory ne cherchent pas à prédire tous les besoins futurs. Ils définissent les invariants nécessaires pour construire le premier produit de manière cohérente, sécurisée et transférable. Les nouvelles décisions devront désormais être motivées par l’implémentation, l’exploitation ou un besoin client réel.
