---
adrId: ADR-0012
title: Separate structural configuration, runtime settings and secrets
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
  - ./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ../03-generation/13-generation-pipeline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../03-generation/15-cli-specification.md
  - ../04-quality-security/18-security-baseline.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/21-observability-and-logging.md
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/23-backup-and-restore.md
  - ../05-operations/24-operational-runbooks.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../05-operations/27-client-handover-and-service-closure.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0002-yaml-client-configuration.md
  - ./0003-postgresql-as-primary-database.md
  - ./0004-explicit-production-migrations.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0033-split-public-sitefactory-and-private-logicgate-modules.md
  - ./0037-compose-modules-explicitly-without-dynamic-loading.md
targetVersion: 0.1.0
---

# ADR-0012 — Configuration runtime et gestion des secrets

## 1. Statut

```text
accepted
```

### Portée publique et état d'implémentation

Cet ADR conserve son statut, sa date et ses décisions de sécurité. Ses profils
de production, opérations de rotation/transfert, providers, interfaces et codes
**recommandés** décrivent une cible ; ils ne constituent pas l'annonce d'un
coffre, d'un déploiement client, d'Identity ou d'un service central disponible.
Les exemples opérationnels n'autorisent aucune action implicite.

Le périmètre public existant comprend les contrats runtime, l'orchestrateur et
les adaptateurs de host, ainsi que la configuration PostgreSQL générique.
`RuntimeConfigurationReader` lie explicitement `SiteFactory:Runtime` et
l'environnement du host, construit un snapshot validé et rejette les propriétés
inconnues ou contradictoires sans recopier les valeurs dans les diagnostics.
Les bibliothèques pures restent indépendantes d'`IConfiguration`.

`PostgreSqlConfigurationReader` lit uniquement le fichier explicite du composant
concerné, sans fallback vers des credentials d'environnement et sans connexion
réseau. Les fichiers sont lus en UTF-8 strict avec taille bornée ; liens,
répertoires et permissions Unix excessives sont refusés, l'annulation est
propagée et les erreurs restent contrôlées sans chemin ni contenu secret.
Le snapshot ne divulgue pas la connexion dans sa représentation publique.
Les credentials Migrator et ceux du runtime Web/Worker restent séparés.

La distribution publique se construit sans source ni secret Business selon
[ADR-0033](0033-split-public-sitefactory-and-private-logicgate-modules.md).
La composition demeure explicite selon
[ADR-0037](0037-compose-modules-explicitly-without-dynamic-loading.md) ; les
abstractions de secret et les mécanismes futurs cités plus loin ne sont pas
chargés ou provisionnés automatiquement. Les notices et frontières de
distribution ne changent pas les exigences de non-divulgation ci-dessous.

LogicGate SiteFactory distingue explicitement :

```text
configuration structurelle
configuration runtime non sensible
références de secrets
valeurs de secrets
état métier persistant
artefacts générés
```

Ces catégories possèdent des sources, cycles de vie et règles de sécurité différents.

Les valeurs sensibles ne doivent jamais être présentes dans :

- le repository Git ;
- `client.sitefactory.yml` ;
- les fichiers Compose versionnés ;
- les images OCI ;
- les packages NuGet ;
- les rapports SiteFactory ;
- les arguments de ligne de commande ;
- les logs ;
- les traces ;
- les métriques ;
- les exemples de configuration.

Le profil initial `docker-compose-vps` utilise préférentiellement des secrets stockés dans des fichiers protégés sur l’hôte et montés en lecture seule dans les conteneurs.

---

## 2. Résumé

La séparation retenue est la suivante :

| Catégorie | Exemple | Source principale |
|---|---|---|
| Configuration structurelle | pack, modules, template, domaine cible | `client.sitefactory.yml` |
| Composition résolue | versions exactes, modules installés | `sitefactory.lock.json` |
| Configuration runtime non sensible | timeout, URL publique, sampling | configuration d’environnement |
| Référence de secret | `smtp-primary` | configuration versionnée ou manifest |
| Valeur de secret | mot de passe SMTP | fichier secret ou coffre |
| État métier | contenu, leads, utilisateurs | PostgreSQL |
| Fichiers persistants | médias, documents | `App_Data` |
| Matériel cryptographique persistant | Data Protection Keys | volume sécurisé |
| Rapport | version, résultat, identifiants techniques | JSON sans secret |

Le flux de référence est :

```text
configuration structurelle
→ génération
→ artefacts versionnés

configuration runtime non sensible
→ déploiement
→ options typées

références de secrets
→ résolution au déploiement ou au démarrage
→ fichiers montés en lecture seule

valeurs de secrets
→ jamais intégrées aux artefacts
→ jamais journalisées
→ rotation explicite
```

---

## 3. Contexte

Un projet SiteFactory nécessite plusieurs catégories de valeurs.

Exemples structurels :

- pack ;
- modules ;
- template ;
- thème ;
- profil de déploiement ;
- capabilities ;
- entitlements ;
- domaine de production.

Exemples runtime non sensibles :

- niveau de logs ;
- taux de sampling ;
- timeout HTTP ;
- taille maximale d’upload ;
- URL publique ;
- cadence d’un job ;
- activation technique d’une fonctionnalité.

Exemples sensibles :

- mot de passe PostgreSQL ;
- credential de migration ;
- mot de passe SMTP ;
- clé API ;
- token registry ;
- credential de backup ;
- token OTLP ;
- clé privée ;
- secret d’intégration.

Ces valeurs ne doivent pas être traitées de manière uniforme.

Une configuration monolithique produirait notamment les risques suivants :

- secrets committés ;
- configuration structurelle modifiée directement en production ;
- divergence entre Git et le VPS ;
- mot de passe exposé dans `docker inspect` ;
- valeur sensible affichée pendant une erreur ;
- staging utilisant un secret de production ;
- token partagé entre plusieurs clients ;
- rotation nécessitant une modification du repository ;
- transfert client incomplet ;
- confusion entre valeur publique et secret.

---

## 4. Problème à résoudre

Comment fournir à chaque application cliente sa configuration et ses credentials tout en garantissant :

- la séparation entre intention produit et configuration d’environnement ;
- l’absence de secret dans Git ;
- l’isolation client et environnement ;
- la validation avant démarrage ;
- des valeurs par défaut sûres ;
- la rotation des credentials ;
- le moindre privilège ;
- la traçabilité des changements ;
- la reproductibilité des déploiements ;
- le transfert au client ;
- la compatibilité avec un coffre externe futur ?

La solution doit également éviter :

- l’utilisation directe et dispersée de `IConfiguration` ;
- les appels à `Environment.GetEnvironmentVariable` dans le code métier ;
- les secrets dans les arguments CLI ;
- l’interpolation implicite de secrets dans le YAML client ;
- le chargement d’un fichier `.env` par un shell ;
- les services distants obligatoires au runtime dans le MVP ;
- le rechargement partiel non maîtrisé des secrets.

---

## 5. Portée

Cette décision couvre :

- classification de la configuration ;
- sources de configuration ;
- précédence ;
- options typées ;
- validation ;
- configuration par environnement ;
- variables d’environnement ;
- fichiers de configuration montés ;
- fichiers de secrets ;
- références de secrets ;
- permissions filesystem ;
- Docker Compose ;
- développement local ;
- CI/CD ;
- production ;
- rotation ;
- révocation ;
- observabilité ;
- rapports ;
- backups ;
- transfert client ;
- clôture de service ;
- règles applicables aux modules et customizations.

---

## 6. Hors portée

Cette décision ne définit pas :

- un fournisseur obligatoire de coffre-fort ;
- un service central LogicGate de secrets ;
- une PKI complète ;
- la cryptographie interne de tous les fournisseurs ;
- les politiques contractuelles de conservation ;
- le détail de chaque credential futur ;
- la gestion des secrets dans une architecture Kubernetes ;
- un système dynamique de configuration distribuée ;
- la facturation ;
- les données métier administrables ;
- la politique complète de Data Protection ASP.NET Core.

---

## 7. Decision drivers

Par ordre de priorité :

1. **Absence de secrets dans Git**
2. **Isolation client et environnement**
3. **Moindre privilège**
4. **Validation avant utilisation**
5. **Rotation et révocation**
6. **Réduction des fuites dans les logs**
7. **Séparation entre structure et environnement**
8. **Reproductibilité**
9. **Transfert client**
10. **Compatibilité Docker Compose**
11. **Compatibilité avec un coffre futur**
12. **Simplicité d’exploitation**
13. **Configuration typée**
14. **Refus par défaut**
15. **Audit des changements**
16. **Limitation de la dépendance fournisseur**

---

## 8. Contraintes

La décision doit respecter :

```text
.NET 10

ASP.NET Core

Docker Compose

Linux en production

Un projet par client

Secrets distincts par environnement

Aucun secret dans Git

Aucun secret dans une image

Aucun secret dans un rapport

Aucun secret dans un argument CLI

Configuration typée

Validation au démarrage

Fichiers secrets en lecture seule

Permissions filesystem restrictives

Rotation explicite

Compatibilité Windows pour le développement
```

---

## 9. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le nombre initial de secrets par projet reste raisonnable ;
- un VPS Linux permet de créer des fichiers protégés ;
- les secrets peuvent être injectés avant le démarrage de la stack ;
- un redémarrage contrôlé reste acceptable pour certaines rotations ;
- les projets clients n’exigent pas de rechargement dynamique immédiat ;
- les fournisseurs externes permettent la rotation de leurs credentials ;
- un coffre distant pourra être ajouté derrière une abstraction Infrastructure ;
- les applications doivent continuer à fonctionner indépendamment d’un service LogicGate central.

---

# 10. Classification de référence

## 10.1 Configuration structurelle

Elle décrit ce que le projet est.

Exemples :

```text
pack

template

thème

modules

entitlements

profil de déploiement

domaine cible

composition
```

Source :

```text
client.sitefactory.yml
```

---

## 10.2 Configuration runtime non sensible

Elle décrit comment une instance fonctionne dans un environnement.

Exemples :

```text
timeouts

niveaux de logs

sampling

taille maximale d’upload

URL publique

cadence d’un job

feature flags techniques
```

---

## 10.3 Référence de secret

Elle désigne un secret sans contenir sa valeur.

Exemple :

```text
smtp-primary
```

ou :

```text
/run/secrets/smtp_password
```

---

## 10.4 Valeur de secret

Elle permet :

- d’authentifier ;
- de signer ;
- de chiffrer ;
- d’accéder à une ressource ;
- d’obtenir un privilège.

Exemples :

```text
mot de passe

token

clé privée

chaîne de connexion

credential de fournisseur
```

---

## 10.5 État métier

Il évolue pendant l’utilisation de l’application.

Exemples :

- leads ;
- contenus ;
- utilisateurs ;
- rôles ;
- réalisations ;
- historiques.

Il appartient à PostgreSQL ou au stockage fonctionnel concerné.

---

## 10.6 Matériel cryptographique persistant

Certaines données sensibles ne sont pas de simples paramètres.

Exemples :

- ASP.NET Core Data Protection Keys ;
- clés privées TLS ;
- clés de signature ;
- certificats privés.

Elles possèdent leur propre cycle de vie.

---

# 11. Option A — Toute la configuration dans `appsettings.json`

## 11.1 Description

Les valeurs non sensibles et sensibles sont stockées dans les fichiers JSON de l’application.

---

## 11.2 Avantages

- simplicité ;
- configuration visible ;
- support natif ASP.NET Core ;
- peu d’outillage supplémentaire ;
- exécution locale facile.

---

## 11.3 Inconvénients

- secrets dans Git ou l’image ;
- rotation nécessitant une nouvelle release ;
- risque d’exposition ;
- mélange entre environnements ;
- transfert difficile ;
- historique Git permanent.

---

## 11.4 Risques

- fuite de mot de passe ;
- secret dans une pull request ;
- secret dans un package ;
- réutilisation d’un credential ;
- accès historique après suppression apparente.

---

## 11.5 Réversibilité

```text
faible après commit d’un secret
```

---

# 12. Option B — Variables d’environnement pour toutes les valeurs

## 12.1 Description

Toutes les configurations et tous les secrets sont injectés par variables d’environnement.

---

## 12.2 Avantages

- séparation du code ;
- support Docker ;
- support ASP.NET Core ;
- mise en œuvre simple ;
- pas de fichier de secret obligatoire.

---

## 12.3 Inconvénients

- faible lisibilité pour les structures complexes ;
- valeurs visibles dans certains outils d’inspection ;
- risque dans les dumps de processus ;
- caractères spéciaux délicats ;
- taille et format limités ;
- configuration difficile à auditer ;
- secret potentiellement affiché par erreur.

---

## 12.4 Risques

- mot de passe présent dans `docker inspect` ;
- erreur de quoting ;
- confusion entre variable absente et vide ;
- variable exportée dans un shell ;
- fuite dans un diagnostic CI.

---

## 12.5 Réversibilité

```text
élevée
```

mais les risques d’exposition persistent.

---

# 13. Option C — Coffre distant obligatoire

## 13.1 Description

Chaque application cliente interroge un service distant de secrets au démarrage ou au runtime.

---

## 13.2 Avantages

- centralisation ;
- rotation ;
- audit ;
- contrôle d’accès ;
- révocation ;
- secrets non présents durablement sur le VPS.

---

## 13.3 Inconvénients

- dépendance runtime ;
- disponibilité supplémentaire ;
- authentification initiale ;
- coût ;
- transfert client plus complexe ;
- verrouillage fournisseur ;
- disproportionné pour le MVP.

---

## 13.4 Risques

- panne du coffre ;
- credential bootstrap compromis ;
- erreur de scope ;
- accès croisé ;
- projet client inutilisable après handover.

---

## 13.5 Réversibilité

```text
moyenne à faible
```

---

# 14. Option D — Configuration en couches et secrets montés

## 14.1 Description

La configuration non sensible utilise les mécanismes ASP.NET Core et des fichiers montés.

Les secrets sont fournis par fichiers protégés ou par un adaptateur de coffre.

---

## 14.2 Avantages

- séparation claire ;
- compatible Docker Compose ;
- secret absent de Git ;
- rotation possible ;
- transfert ;
- abstraction fournisseur ;
- validation typée ;
- exposition réduite dans l’environnement du processus.

---

## 14.3 Inconvénients

- provisioning des fichiers ;
- permissions à gérer ;
- abstraction de lecture ;
- runbooks ;
- redémarrage parfois requis ;
- inventaire supplémentaire.

---

## 14.4 Risques

- mauvais fichier monté ;
- permission trop large ;
- secret vide ;
- fichier de staging en production ;
- copie non sécurisée ;
- rotation incomplète.

---

## 14.5 Réversibilité

```text
élevée
```

Le backend peut évoluer vers un coffre sans modifier le domaine.

---

# 15. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | JSON versionné | Environnement uniquement | Coffre obligatoire | Couches + fichiers |
|---|---:|---:|---:|---:|---:|
| Absence de secret dans Git | 5 | 1 | 5 | 5 | 5 |
| Simplicité MVP | 5 | 5 | 4 | 1 | 4 |
| Rotation | 5 | 1 | 4 | 5 | 5 |
| Isolation | 5 | 2 | 4 | 5 | 5 |
| Transfert client | 4 | 3 | 4 | 2 | 5 |
| Validation | 4 | 4 | 3 | 4 | 5 |
| Faible dépendance fournisseur | 4 | 5 | 5 | 1 | 5 |
| Limitation de l’exposition | 5 | 1 | 3 | 5 | 4 |
| Maintenabilité | 4 | 2 | 3 | 3 | 5 |
| Réversibilité | 4 | 2 | 5 | 3 | 5 |

L’option D est retenue.

---

# 16. Décision

Nous décidons d’utiliser une configuration en couches avec séparation stricte entre valeurs non sensibles et valeurs sensibles.

La baseline du profil `docker-compose-vps` est :

```text
Configuration non sensible
→ JSON versionné
→ JSON monté
→ variables d’environnement

Secrets
→ fichiers protégés montés en lecture seule
→ adaptateur de coffre futur possible
```

---

## 16.1 Source canonique

`client.sitefactory.yml` reste la source de vérité de la configuration structurelle.

Il n’est pas utilisé comme magasin de secrets.

---

## 16.2 Runtime

Le runtime reçoit uniquement les valeurs nécessaires à son environnement.

---

## 16.3 Autonomie

Aucun service LogicGate distant n’est obligatoire pour démarrer l’application cliente dans le MVP.

---

# 17. Sources de configuration runtime

Ordre de précédence recommandé, de la priorité la plus faible à la plus élevée :

```text
1. Valeurs par défaut du code
2. appsettings.json
3. appsettings.{Environment}.json
4. Fichier runtime monté
5. Variables d’environnement
6. Résolution explicite des secrets
```

---

## 17.1 Valeurs par défaut

Les valeurs par défaut doivent être :

- sûres ;
- documentées ;
- indépendantes de la machine ;
- identiques entre environnements sauf configuration explicite.

---

## 17.2 `appsettings.json`

Il contient la baseline non sensible.

---

## 17.3 `appsettings.{Environment}.json`

Il peut être versionné uniquement s’il ne contient aucune donnée sensible.

---

## 17.4 Fichier monté

Un fichier tel que :

```text
/run/config/sitefactory.runtime.json
```

peut contenir la configuration non sensible propre au déploiement.

---

## 17.5 Variables d’environnement

Elles permettent des overrides simples et explicites.

---

## 17.6 Secrets

Les valeurs de secrets sont résolues séparément.

Elles ne doivent pas être mélangées au modèle général de précédence sans contrôle.

---

# 18. Configuration structurelle et runtime

## 18.1 Structurelle

Une modification structurelle nécessite normalement :

```text
modification YAML
→ plan
→ génération ou upgrade
→ build
→ tests
→ release
```

---

## 18.2 Runtime

Une modification runtime non structurelle peut nécessiter :

```text
modification de configuration d’environnement
→ validation
→ redémarrage contrôlé
→ health checks
→ rapport
```

---

## 18.3 Frontière

Une valeur modifiant :

- les modules installés ;
- les entitlements ;
- les migrations ;
- les routes structurelles ;
- les contrats ;

n’est pas une simple configuration runtime.

---

# 19. Options typées

Les composants utilisent des options typées.

Exemple conceptuel :

```csharp
public sealed class SmtpOptions
{
    public const string SectionName = "SiteFactory:Smtp";

    public bool Enabled { get; init; }

    public required Uri ServerUri { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    public string? CredentialReference { get; init; }
}
```

---

## 19.1 `IConfiguration`

`IConfiguration` reste limité au composition root et à l’Infrastructure de configuration.

---

## 19.2 Domain

Domain ne dépend pas de :

- `IConfiguration` ;
- variables d’environnement ;
- fichiers ;
- noms de secrets ;
- coffre.

---

## 19.3 Application

Application reçoit :

- options métier validées ;
- abstractions ;
- contrats ;

et non la configuration brute.

---

# 20. Validation des options

La validation s’effectue au démarrage.

Elle doit couvrir :

- présence ;
- types ;
- limites ;
- URL ;
- protocoles ;
- combinaisons ;
- environnement ;
- références de secrets ;
- valeurs placeholder.

---

## 20.1 `ValidateOnStart`

Les options critiques utilisent une validation au démarrage.

---

## 20.2 Message

Une erreur indique :

```text
le nom de la propriété
le code d’erreur
la règle
```

sans afficher la valeur sensible.

---

## 20.3 Configuration critique

Une configuration essentielle invalide empêche le démarrage.

---

## 20.4 Configuration optionnelle

Un module optionnel mal configuré doit :

- rester indisponible ;
- échouer de manière fermée ;
- produire un diagnostic ;
- ne pas exposer une fonctionnalité partiellement fonctionnelle.

---

# 21. Valeurs placeholder

Les valeurs suivantes sont interdites en production :

```text
changeme

password

replace-me

example-secret

TODO

localhost
```

lorsqu’elles correspondent à une configuration réelle attendue.

---

## 21.1 Détection

La validation doit détecter les placeholders connus.

---

## 21.2 Exemple

Les fichiers `.example` peuvent contenir des placeholders clairement identifiés, mais ils ne sont jamais utilisés directement en production.

---

# 22. Nommage des clés

Les clés runtime utilisent un namespace stable :

```text
SiteFactory:{Area}:{Property}
```

Exemples :

```text
SiteFactory:Smtp:Enabled

SiteFactory:Observability:SamplingRatio

SiteFactory:Uploads:MaximumBytes
```

En variables d’environnement :

```text
SiteFactory__Smtp__Enabled
```

---

## 22.1 Secrets

Les chemins ou références de secrets utilisent une section distincte.

Exemple :

```text
SiteFactory__Secrets__SmtpPasswordFile
```

---

## 22.2 Pas de doublon ambigu

Éviter de posséder simultanément :

```text
SmtpPassword
SmtpPasswordFile
SmtpCredential
SmtpSecret
```

sans règle de priorité claire.

---

# 23. Références de secrets

Une référence identifie un besoin sans contenir la valeur.

Exemples :

```text
postgres-runtime-password

smtp-primary-password

backup-storage-token
```

---

## 23.1 Stable

La référence peut rester stable pendant plusieurs rotations.

---

## 23.2 Valeur

La rotation modifie la valeur ou la version associée, pas nécessairement la référence.

---

## 23.3 Lock file

Le lock file peut contenir :

- le Secret ID ;
- son rôle ;
- son caractère obligatoire.

Il ne contient jamais sa valeur ni son hash.

---

# 24. Abstraction de lecture

Infrastructure peut exposer une abstraction minimale telle que :

```csharp
public interface ISecretReader
{
    ValueTask<string> ReadRequiredAsync(
        SecretReference reference,
        CancellationToken cancellationToken);
}
```

La signature exacte pourra évoluer.

---

## 24.1 Domaine

Cette abstraction ne doit pas être utilisée directement par Domain.

---

## 24.2 Composition

Les secrets nécessaires au démarrage sont résolus dans les adaptateurs ou factories appropriés.

---

## 24.3 Pas de cache global arbitraire

Un secret ne doit pas être placé dans un singleton générique accessible à tous les modules.

---

# 25. Stockage par fichiers

Le profil initial utilise un répertoire tel que :

```text
/srv/sitefactory/clients/{client}/{environment}/secrets/
```

monté dans le conteneur sous :

```text
/run/secrets/
```

---

## 25.1 Répertoire hôte

Le répertoire doit utiliser des permissions restrictives.

Baseline :

```text
0700
```

---

## 25.2 Fichiers

Les fichiers utilisent selon le besoin :

```text
0400
```

ou :

```text
0600
```

---

## 25.3 Propriétaire

Le propriétaire et le groupe doivent être explicitement contrôlés.

---

## 25.4 UID du conteneur

Le montage doit tenir compte de l’utilisateur non root de l’image.

Les fichiers ne doivent pas devenir globalement lisibles pour contourner un problème d’UID.

---

# 26. Montage dans Docker Compose

Exemple conceptuel :

```yaml
services:
  web:
    secrets:
      - postgres_runtime_password
      - smtp_password

secrets:
  postgres_runtime_password:
    file: ./secrets/postgres_runtime_password

  smtp_password:
    file: ./secrets/smtp_password
```

Le fichier Compose versionné peut décrire les noms et chemins relatifs attendus.

Les fichiers réels restent hors Git.

---

## 26.1 Lecture seule

Les secrets sont montés en lecture seule.

---

## 26.2 Absence

Un secret obligatoire absent bloque le démarrage ou l’opération concernée.

---

## 26.3 Valeur vide

Un fichier présent mais vide est considéré comme invalide.

---

# 27. Nouvelle ligne finale

La lecture d’un secret texte peut supprimer une unique fin de ligne terminale créée par l’outil d’écriture.

Elle ne doit pas appliquer automatiquement :

- `Trim()` complet ;
- suppression des espaces ;
- normalisation Unicode ;
- changement de casse.

---

## 27.1 Raisons

Un espace peut faire partie d’un secret valide.

---

## 27.2 Contrat

La convention d’écriture doit éviter les nouvelles lignes involontaires.

Exemple Bash :

```bash
printf '%s' "$SECRET_VALUE" > secret-file
```

---

# 28. Caractères spéciaux

Les secrets peuvent contenir :

```text
!

$

#

espaces

guillemets

backslashes

Unicode
```

Ils ne doivent pas être réinterprétés par un shell.

---

## 28.1 Commande interdite

```bash
source .env
```

est interdite dans les scripts de production.

---

## 28.2 Fichiers

Les valeurs sont écrites comme contenu brut d’un fichier avec permissions adaptées.

---

## 28.3 Arguments

Une valeur de secret ne doit pas être passée dans :

```bash
--password "..."
```

car elle pourrait apparaître dans :

- l’historique ;
- la liste des processus ;
- les logs ;
- les audits shell.

---

# 29. Variables d’environnement sensibles

Les secrets en variables d’environnement restent tolérés lorsqu’un fournisseur ou outil ne permet pas de fichier.

Ils ne constituent pas la méthode préférée.

---

## 29.1 Risques

Ils peuvent apparaître dans :

- inspection du conteneur ;
- dump de processus ;
- diagnostics ;
- outils d’administration ;
- scripts.

---

## 29.2 Mesures

Lorsque leur usage est nécessaire :

- scope minimal ;
- durée limitée ;
- pas de logs ;
- pas de commande affichée ;
- rotation ;
- restriction des accès Docker.

---

# 30. Chaînes de connexion

Une chaîne de connexion PostgreSQL complète est un secret.

---

## 30.1 Préférence

Construire la configuration depuis :

```text
host non sensible
port non sensible
database non sensible
username contrôlé
password secret
```

---

## 30.2 Fichier complet

Si un outil nécessite une chaîne complète, elle doit être montée comme fichier secret.

---

## 30.3 Logs

La chaîne complète ne doit jamais être affichée.

---

## 30.4 Runtime et migration

Les connexions runtime et migration utilisent des credentials distincts conformément aux ADR-0003 et ADR-0004.

---

# 31. PostgreSQL

Secrets concernés :

```text
mot de passe runtime

mot de passe migration

credential opérateur

credential backup éventuel
```

---

## 31.1 Isolation

Chaque client et environnement utilise des credentials distincts.

---

## 31.2 Réutilisation

Réutiliser le même mot de passe PostgreSQL entre plusieurs clients est interdit.

---

## 31.3 Superutilisateur

Le credential superutilisateur n’est pas fourni au conteneur web.

---

# 32. SMTP

Les credentials SMTP utilisent :

- une référence ;
- un fichier utilisateur si nécessaire ;
- un fichier mot de passe ;
- une configuration non sensible pour l’hôte et le port.

---

## 32.1 Adresse d’expéditeur

L’adresse d’expéditeur n’est pas nécessairement secrète.

---

## 32.2 Mot de passe

Le mot de passe ou token SMTP est secret.

---

## 32.3 Staging

Staging ne doit pas utiliser involontairement les credentials ou destinataires réels de production.

---

# 33. Registry

Le VPS utilise un token de lecture.

Le pipeline de release utilise un token de publication distinct.

---

## 33.1 Portée

Le token du VPS ne possède pas de droit d’écriture.

---

## 33.2 Client

Un token ne doit pas donner accès aux images privées d’autres clients sans nécessité.

---

## 33.3 Rotation

La rotation ne nécessite pas de reconstruire l’image.

---

# 34. Backups

Les credentials de stockage externe sont des secrets opérationnels.

---

## 34.1 Accès minimal

Ils doivent permettre uniquement les opérations nécessaires :

- écriture ;
- lecture ;
- suppression selon la politique.

---

## 34.2 Compartiment

La cible de backup doit être isolée par client ou préfixe sécurisé.

---

## 34.3 Logs

Le token, la clé ou l’URL signée ne doit pas apparaître dans le rapport de backup.

---

# 35. OpenTelemetry

L’endpoint OTLP peut être non sensible.

Le credential d’authentification reste secret.

---

## 35.1 Destination

L’endpoint doit être validé et approuvé.

---

## 35.2 Header

Un header d’authentification ne doit pas être enregistré dans les traces HTTP.

---

# 36. Data Protection Keys

Les clés ASP.NET Core Data Protection ne sont pas traitées comme une simple chaîne de configuration.

---

## 36.1 Stockage

Elles sont persistées dans un volume dédié.

---

## 36.2 Isolation

Elles sont distinctes entre :

- clients ;
- staging ;
- production.

---

## 36.3 Perte

Leur perte peut invalider :

- cookies ;
- tokens protégés ;
- données chiffrées par le mécanisme concerné.

---

## 36.4 Compromission

Une compromission peut nécessiter :

- remplacement du key ring ;
- invalidation des sessions ;
- rotation des secrets associés ;
- analyse d’incident.

---

## 36.5 Backup

Leur sauvegarde dépend du plan de continuité et doit rester chiffrée et limitée.

---

# 37. Certificats et clés TLS

Dans le profil Caddy :

- les certificats ;
- les comptes ACME ;
- les clés privées ;

sont gérés dans les volumes Caddy.

---

## 37.1 Repository

Ils ne sont jamais versionnés.

---

## 37.2 Accès

Seul le service Caddy et les opérations autorisées y accèdent.

---

## 37.3 Transfert

Un transfert peut :

- déplacer les volumes de manière sécurisée ;
- ou réémettre les certificats sur la nouvelle infrastructure.

---

# 38. Développement local

Le développement local peut utiliser :

- `.NET user-secrets` ;
- fichiers locaux sous `.secrets/` ;
- secrets Docker Compose locaux ;
- credentials de test.

---

## 38.1 `.gitignore`

Les chemins locaux sensibles sont ignorés.

---

## 38.2 Valeurs réelles

Un secret de production ne doit pas être utilisé sur un poste de développement sans nécessité exceptionnelle.

---

## 38.3 Données de test

Les tests utilisent des valeurs fictives générées.

---

## 38.4 Documentation

La configuration locale doit pouvoir être recréée sans partager une valeur réelle dans le README.

---

# 39. User Secrets .NET

`dotnet user-secrets` peut être utilisé pour le développement.

---

## 39.1 Limite

User Secrets :

- ne chiffre pas nécessairement les valeurs ;
- protège principalement contre un commit accidentel ;
- n’est pas une solution de production.

---

## 39.2 Identifiant

Le `UserSecretsId` ne constitue pas un secret.

---

# 40. Tests automatisés

Les tests utilisent des secrets éphémères.

Exemples :

- mot de passe PostgreSQL généré ;
- faux token ;
- certificat de test ;
- clé aléatoire locale.

---

## 40.1 Production

Aucun secret de production n’est nécessaire pour exécuter les tests standards.

---

## 40.2 Intégration externe

Une intégration réelle facultative utilise un environnement de test séparé et des secrets dédiés.

---

## 40.3 Snapshots

Les snapshots et rapports de tests ne doivent pas inclure les valeurs.

---

# 41. CI/CD

Les secrets CI sont fournis par :

- GitHub Actions Secrets ;
- GitHub Environments ;
- OIDC lorsque le fournisseur le supporte ;
- mécanisme approuvé équivalent.

---

## 41.1 Pull requests non fiables

Les secrets sensibles ne sont pas exposés aux workflows issus de code non approuvé.

---

## 41.2 Publication

Les credentials de publication sont réservés au workflow protégé.

---

## 41.3 Masquage

Le masquage fourni par la CI est une protection complémentaire.

Le code ne doit pas volontairement afficher les valeurs.

---

## 41.4 OIDC

Un credential temporaire obtenu par OIDC est préférable à un token longue durée lorsque disponible.

---

# 42. Déploiement

Avant le démarrage, l’opérateur doit vérifier :

```text
[ ] Secrets obligatoires présents
[ ] Permissions correctes
[ ] Propriétaire correct
[ ] Client correct
[ ] Environnement correct
[ ] Fichiers non vides
[ ] Aucun placeholder
[ ] Aucun secret de staging
[ ] Aucune valeur dans Git
[ ] Rotation ou expiration vérifiée
```

---

## 42.1 Plan

Le plan de déploiement liste :

- Secret IDs requis ;
- présence attendue ;
- propriétaire ;
- méthode de rotation.

Il ne liste pas les valeurs.

---

## 42.2 Rapport

Le rapport enregistre :

- secret présent ou absent ;
- version logique éventuelle ;
- statut de validation.

Il ne contient aucun hash de la valeur.

---

# 43. Hash des secrets

Le hash d’une valeur de secret ne doit pas être ajouté aux rapports ou au lock file.

---

## 43.1 Raisons

Un hash peut faciliter :

- une attaque hors ligne ;
- la comparaison entre clients ;
- la détection de réutilisation par une personne non autorisée.

---

## 43.2 Métadonnées

Utiliser plutôt :

- version logique ;
- identifiant fournisseur ;
- date de rotation ;
- statut.

---

# 44. Inventaire des secrets

LogicGate doit maintenir un inventaire sans valeurs.

Exemple :

| Secret ID | Client | Environnement | Usage | Propriétaire | Rotation |
|---|---|---|---|---|---|
| `postgres-runtime-password` | `<CLIENT_TECHNICAL_ID>` | Production | Runtime DB | Operations | 90 jours |
| `smtp-primary-password` | `<CLIENT_TECHNICAL_ID>` | Production | SMTP | Operations | fournisseur |
| `backup-storage-token` | `<CLIENT_TECHNICAL_ID>` | Production | Backup | Operations | 90 jours |

---

## 44.1 Métadonnées recommandées

```text
SecretId

ClientSlug

Environment

Purpose

Provider

Owner

CreatedAtUtc

LastRotatedAtUtc

NextReviewAtUtc

ExpirationAtUtc

RevocationProcedure

RequiredBy
```

---

## 44.2 Valeurs interdites

L’inventaire ne contient pas :

- secret ;
- hash ;
- mot de passe précédent ;
- token ;
- chaîne de connexion.

---

# 45. Rotation

Une rotation suit :

```text
plan
→ création du nouveau credential
→ distribution sécurisée
→ mise à jour atomique
→ redémarrage ou rechargement
→ vérification
→ révocation de l’ancien
→ rapport
```

---

## 45.1 Validation

La rotation n’est pas complète tant que l’ancien credential reste actif sans justification.

---

## 45.2 Fenêtre de chevauchement

Une courte période de coexistence peut être nécessaire.

Elle doit être :

- bornée ;
- documentée ;
- surveillée.

---

## 45.3 Rollback

Le plan précise si l’ancien credential peut être réactivé pendant la fenêtre.

---

# 46. Rotation atomique des fichiers

Un secret ne doit pas être remplacé par une écriture partielle.

Approche conceptuelle :

```text
écrire nouveau fichier temporaire
→ définir permissions
→ fsync lorsque nécessaire
→ rename atomique
→ redémarrer ou recharger
```

---

## 46.1 Lecture pendant la rotation

L’application ne doit pas lire un fichier partiellement écrit.

---

## 46.2 Backup du secret précédent

La valeur précédente ne doit pas être conservée en clair dans le même répertoire sans besoin explicite.

---

# 47. Rotation PostgreSQL

La rotation PostgreSQL doit tenir compte :

- du pool de connexions ;
- des nouvelles connexions ;
- du credential runtime ;
- du credential migration ;
- des opérations en cours.

---

## 47.1 Approche recommandée

Lorsque possible :

```text
créer un nouveau rôle ou credential
→ accorder les droits
→ déployer la nouvelle configuration
→ vérifier
→ révoquer l’ancien
```

---

## 47.2 Changement de mot de passe direct

Un changement direct peut laisser des connexions existantes actives tout en faisant échouer les nouvelles.

Il nécessite une surveillance et un redémarrage contrôlé.

---

# 48. Rotation d’un token fournisseur

La procédure doit vérifier :

- nouvelle clé créée ;
- scopes corrects ;
- application mise à jour ;
- appel de test réussi ;
- ancienne clé révoquée ;
- audit fournisseur.

---

# 49. Rotation d’un secret CI

La rotation d’un secret CI doit vérifier :

- environnements concernés ;
- workflows ;
- permissions ;
- branches ;
- dernier usage ;
- ancienne valeur révoquée ;
- absence de fork ou copie.

---

# 50. Expiration

Un secret avec expiration connue doit posséder une alerte avant échéance.

---

## 50.1 Seuils

Exemple :

```text
30 jours
14 jours
7 jours
1 jour
```

selon la criticité.

---

## 50.2 Secret expiré

Un secret expiré est traité comme un incident de configuration.

---

# 51. Révocation

Une révocation immédiate est requise en cas de :

- suspicion de fuite ;
- départ d’un opérateur ;
- compromission de poste ;
- exposition dans Git ;
- exposition dans un log ;
- exposition dans un package ;
- transfert de service ;
- fin de contrat ;
- changement de fournisseur.

---

## 51.1 Suppression Git insuffisante

Supprimer le secret du dernier commit ne suffit pas.

Il faut :

```text
révoquer
→ remplacer
→ analyser l’historique
→ nettoyer si nécessaire
→ notifier selon le processus
```

---

# 52. Incident de secret

Le runbook doit couvrir :

1. identifier le secret ;
2. déterminer son périmètre ;
3. révoquer ;
4. générer une nouvelle valeur ;
5. mettre à jour les environnements ;
6. redémarrer ;
7. vérifier ;
8. analyser les accès ;
9. corriger la cause ;
10. documenter.

---

## 52.1 Priorité

La révocation ne doit pas attendre la fin complète de l’analyse lorsqu’une exploitation reste possible.

---

# 53. Configuration dynamique

Le MVP ne met pas en place de rechargement dynamique général des configurations et secrets.

---

## 53.1 Baseline

Une modification nécessite :

```text
mise à jour contrôlée
→ recréation ou redémarrage du conteneur
→ health checks
→ smoke tests
```

---

## 53.2 Raisons

Le rechargement dynamique peut créer :

- état partiel ;
- pool utilisant une ancienne valeur ;
- services singleton non mis à jour ;
- incohérence entre modules ;
- absence d’audit.

---

## 53.3 Exception

Un mécanisme dynamique futur devra définir :

- atomicité ;
- notification ;
- cache ;
- version ;
- erreur ;
- rollback ;
- observabilité.

---

# 54. Feature flags et kill switches

Les feature flags et kill switches suivent l’ADR-0006.

---

## 54.1 Non secrets

Leur valeur n’est généralement pas secrète.

---

## 54.2 Sensibilité opérationnelle

Ils restent sensibles en termes d’intégrité.

Une modification doit être :

- contrôlée ;
- auditée ;
- validée.

---

## 54.3 Séparation

Un kill switch ne doit pas être stocké dans le même fichier qu’un mot de passe uniquement parce qu’ils sont tous deux propres à la production.

---

# 55. URLs et endpoints

Une URL n’est pas toujours secrète.

Elle peut toutefois contenir :

- token ;
- signature ;
- identifiant confidentiel ;
- query string sensible.

---

## 55.1 Validation

Les endpoints doivent être :

- structurés ;
- validés ;
- limités aux schémas autorisés ;
- compatibles avec l’environnement ;
- dépourvus de credential inline.

---

## 55.2 Credentials inline

Exemple interdit :

```text
https://user:password@example.test
```

---

# 56. Arguments de ligne de commande

Les secrets ne sont jamais transmis par arguments.

---

## 56.1 Raisons

Les arguments peuvent apparaître dans :

- historique ;
- processus ;
- audit ;
- logs CI ;
- rapports.

---

## 56.2 CLI SiteFactory

Le CLI reçoit :

- un Secret ID ;
- un chemin de fichier ;
- une référence d’environnement ;

et non la valeur.

---

# 57. Standard input

La saisie via standard input peut être utilisée dans un outil opérateur ponctuel si :

- l’écho est désactivé ;
- la valeur n’est pas journalisée ;
- aucun historique n’est créé ;
- la commande ne la recopie pas.

Elle ne constitue pas la méthode de déploiement automatisé de référence.

---

# 58. Fichiers `.env`

## 58.1 Fichier exemple

Le repository peut contenir :

```text
.env.production.example
```

sans valeur réelle.

---

## 58.2 Fichier réel

Le fichier réel :

- reste hors Git ;
- utilise des permissions restrictives ;
- ne contient que les valeurs nécessaires ;
- n’est jamais envoyé comme artefact public.

---

## 58.3 Préférence

Les secrets complexes privilégient les fichiers montés.

---

## 58.4 Shell

Un fichier `.env` n’est jamais considéré comme un script shell.

---

# 59. Placeholders dans les fichiers d’exemple

Exemple :

```text
POSTGRES_PASSWORD_FILE=/run/secrets/postgres_runtime_password
```

plutôt que :

```text
POSTGRES_PASSWORD=ChangeMe123!
```

---

## 59.1 Secret fictif

Un secret fictif ne doit pas ressembler à une valeur réellement utilisable.

---

# 60. Écriture des fichiers secrets

Une procédure peut utiliser :

```bash
install \
  --mode 0600 \
  --owner sitefactory \
  --group sitefactory \
  /dev/null \
  ./secrets/postgres_runtime_password
```

puis une saisie ou copie sécurisée.

La commande exacte dépend de l’utilisateur et du déploiement.

---

## 60.1 `echo`

Éviter :

```bash
echo "$PASSWORD" > file
```

lorsque le shell, l’historique ou les expansions ne sont pas maîtrisés.

---

## 60.2 Presse-papiers

Le presse-papiers constitue une surface d’exposition.

Son usage pour les secrets de production doit rester limité.

---

# 61. Permissions des répertoires

Structure conceptuelle :

```text
/srv/sitefactory/clients/
└── cabinet-alpha/
    └── production/
        ├── config/
        ├── secrets/
        ├── reports/
        └── releases/
```

Permissions :

```text
secrets/
→ accès opérateur et runtime strictement nécessaires

reports/
→ aucune valeur sensible

config/
→ lecture opérateur et runtime selon besoin
```

---

# 62. Comptes système

Le compte opérateur et l’utilisateur du conteneur doivent être identifiés.

---

## 62.1 Root

Les fichiers ne doivent pas être rendus globalement lisibles uniquement parce que le conteneur ne peut pas lire un fichier détenu par root.

---

## 62.2 Groupe

Un groupe dédié peut fournir l’accès nécessaire.

---

## 62.3 ACL

Les ACL peuvent être utilisées lorsque les permissions classiques ne suffisent pas, avec documentation et tests.

---

# 63. Accès Docker

Une personne ayant accès au démon Docker peut généralement inspecter ou remplacer les montages.

Cet accès doit être considéré comme privilégié.

---

## 63.1 Groupe Docker

L’appartenance au groupe Docker est strictement limitée.

---

## 63.2 Application

Le conteneur web ne reçoit jamais le socket Docker.

---

# 64. Secrets dans les images

La CI doit vérifier que l’image ne contient pas :

- `.env` ;
- fichiers secrets ;
- configuration de production ;
- tokens NuGet ;
- tokens registry ;
- clés privées ;
- secrets de test réutilisables.

---

## 64.1 Build args

Les secrets ne doivent pas être injectés comme simples `ARG` persistants dans l’historique de l’image.

---

## 64.2 Build secrets

Lorsqu’un secret est nécessaire au build, utiliser un mécanisme de build secret adapté et vérifier qu’il n’est pas copié dans une couche.

---

# 65. Secrets dans les packages

Les packages NuGet et archives de templates doivent être inspectés conformément à l’ADR-0008.

---

## 65.1 Exemples

Les samples utilisent des références ou valeurs fictives.

---

# 66. Secrets dans les rapports

Les rapports peuvent indiquer :

```text
secret required

secret present

secret missing

secret expired

secret rotation required
```

Ils ne contiennent jamais :

```text
secret value

secret hash

token prefix distinctif non nécessaire

connection string
```

---

# 67. Secrets dans les logs

Les règles de l’ADR-0010 s’appliquent.

---

## 67.1 Redaction

La redaction est une protection complémentaire.

Le code ne doit pas volontairement journaliser la valeur en comptant sur un filtre.

---

## 67.2 Exceptions

Une exception de fournisseur peut recopier un endpoint ou un header.

Elle doit être inspectée avant journalisation.

---

# 68. Secrets dans les traces HTTP

Les instrumentations automatiques doivent exclure :

- `Authorization` ;
- cookies ;
- headers API ;
- query strings sensibles ;
- corps.

---

# 69. Sérialisation des options

Une classe d’options contenant une valeur sensible ne doit pas être sérialisée ou journalisée intégralement.

---

## 69.1 `ToString`

Éviter les records ou méthodes `ToString()` exposant automatiquement toutes les propriétés sensibles.

---

## 69.2 Debugger

Les propriétés sensibles peuvent utiliser des attributs ou wrappers limitant l’affichage accidentel dans les outils lorsque pertinent.

Cela ne constitue pas une garantie de sécurité.

---

# 70. Durée de vie en mémoire

Une valeur lue par l’application peut rester présente dans la mémoire managée.

---

## 70.1 Réduction

Réduire :

- le nombre de copies ;
- le nombre de composants y accédant ;
- la durée de cache ;
- la sérialisation ;
- la journalisation.

---

## 70.2 Garantie impossible

SiteFactory ne doit pas prétendre garantir l’effacement complet d’une chaîne de caractères managée.

---

# 71. Secret scopes

Les scopes recommandés sont :

```text
par client

par environnement

par usage

par privilège
```

---

## 71.1 Exemple

```text
alpha-production-postgres-runtime

alpha-production-postgres-migration

alpha-staging-postgres-runtime
```

---

## 71.2 Secret partagé

Un secret partagé entre plusieurs clients nécessite une justification et augmente le blast radius.

---

# 72. Mot de passe unique

Chaque credential doit utiliser une valeur unique.

---

## 72.1 Réutilisation

La réutilisation entre :

- clients ;
- environnements ;
- fournisseurs ;
- runtime et migration ;

est interdite.

---

# 73. Génération des secrets

Les secrets générés automatiquement utilisent un générateur cryptographiquement sûr.

---

## 73.1 Longueur

La longueur et l’alphabet doivent respecter le fournisseur cible.

---

## 73.2 Affichage

Une valeur générée ne doit pas être affichée en clair dans un rapport.

---

## 73.3 Transmission

Elle doit être écrite directement dans sa destination sécurisée lorsque possible.

---

# 74. Valeurs fournies par un client

Un client peut fournir un credential pour un système dont il est propriétaire.

---

## 74.1 Transmission

Le canal de transmission doit être approuvé.

---

## 74.2 Email ou ticket

Un secret ne doit pas être demandé dans un ticket ou email non sécurisé.

---

## 74.3 Rotation initiale

Lorsque possible, une valeur temporaire est remplacée après installation.

---

# 75. Configuration manuelle sur VPS

Les modifications manuelles doivent rester exceptionnelles.

---

## 75.1 Flux normal

```text
modifier la source contrôlée
→ valider
→ déployer
→ rapport
```

---

## 75.2 Urgence

Une modification urgente sur le VPS doit être :

- enregistrée ;
- réconciliée avec la source ;
- revue ;
- testée ;
- associée à un incident ou changement.

---

## 75.3 Drift

Une modification non réconciliée constitue une dérive de configuration.

---

# 76. Empreinte de configuration

Le déploiement peut enregistrer une empreinte de la configuration runtime non sensible.

---

## 76.1 Exclusions

L’empreinte exclut :

- valeurs de secrets ;
- contenu de fichiers secrets ;
- tokens ;
- matériel cryptographique.

---

## 76.2 Secret metadata

Le rapport peut inclure les Secret IDs et versions logiques, sans valeur ni hash.

---

## 76.3 Usage

L’empreinte permet de détecter une dérive entre deux déploiements.

---

# 77. Configuration par environnement

Les environnements autorisés sont :

```text
development

test

staging

production
```

---

## 77.1 Valeur inconnue

Un environnement inconnu est refusé.

---

## 77.2 Casse

La représentation canonique est en minuscules dans les artefacts SiteFactory.

---

## 77.3 Production

Les contraintes de production doivent être plus strictes.

Exemples :

- HTTPS obligatoire ;
- placeholders interdits ;
- sensitive logging interdit ;
- secrets de test interdits ;
- endpoints locaux interdits.

---

# 78. Isolation staging et production

Staging ne partage pas avec production :

- mots de passe ;
- tokens ;
- Data Protection Keys ;
- credential SMTP ;
- credential PostgreSQL ;
- credential backup ;
- credential OTLP ;
- cookie secrets.

---

## 78.1 Fournisseur commun

Même lorsqu’un compte fournisseur est commun, les sous-credentials ou scopes doivent être séparés lorsque possible.

---

# 79. Modules

Chaque module déclare :

- options runtime ;
- valeurs par défaut ;
- validation ;
- secrets requis ;
- Secret IDs ;
- environnement ;
- stratégie de rotation ;
- comportement si absent.

---

## 79.1 Lecture directe interdite

Un module ne doit pas appeler directement :

```csharp
Environment.GetEnvironmentVariable(...)
```

dans sa logique métier.

---

## 79.2 Contrats

Le module reçoit des options typées ou une abstraction limitée.

---

## 79.3 Secret facultatif

Un secret facultatif doit correspondre à une capability réellement désactivable.

---

# 80. Customizations client

Les customizations suivent les mêmes règles.

---

## 80.1 Secret client

Un secret propre à une customization reste dans le périmètre du projet client.

---

## 80.2 Déclaration

La customization documente :

- Secret ID ;
- fournisseur ;
- scope ;
- rotation ;
- runbook ;
- kill switch éventuel.

---

## 80.3 SDK fournisseur

La valeur n’est jamais intégrée dans le code ou le package.

---

# 81. Permissions applicatives

Un administrateur fonctionnel ne doit pas pouvoir lire les secrets runtime.

---

## 81.1 Administration

L’interface peut indiquer :

```text
Configured
Missing
Invalid
```

sans afficher la valeur.

---

## 81.2 Rotation

La rotation reste une opération d’infrastructure dans le MVP.

---

# 82. Endpoints diagnostics

Un endpoint de diagnostic protégé peut indiquer :

- option valide ;
- secret requis présent ;
- version de configuration ;
- dernier redémarrage.

---

## 82.1 Interdictions

Il ne doit pas afficher :

- valeur ;
- chemin hôte complet ;
- permissions détaillées publiquement ;
- fournisseur confidentiel inutile ;
- token prefix.

---

# 83. Health checks

Un secret obligatoire absent peut rendre :

```text
readiness = unhealthy
```

ou empêcher le démarrage.

---

## 83.1 Liveness

Le liveness ne dépend pas d’un secret optionnel.

---

## 83.2 Réponse publique

Le détail exact reste protégé.

---

# 84. Backup des secrets

Les repositories et backups applicatifs ne suffisent pas à reconstruire tous les secrets.

---

## 84.1 Stratégies

Selon le secret :

- recréation ;
- export chiffré ;
- coffre client ;
- stockage sécurisé ;
- transfert opérateur.

---

## 84.2 Mot de passe fournisseur

Lorsqu’un secret peut être recréé, la préférence va à la recréation plutôt qu’à la conservation indéfinie.

---

## 84.3 Data Protection

Les clés nécessitant une continuité fonctionnelle peuvent être sauvegardées de manière chiffrée.

---

## 84.4 Manifeste

Le backup indique quels secrets doivent être :

- restaurés ;
- recréés ;
- rotatés.

Il ne contient pas leurs valeurs.

---

# 85. Restauration

Une restauration doit vérifier :

- secrets disponibles ;
- versions compatibles ;
- scopes ;
- permissions ;
- client ;
- environnement.

---

## 85.1 Ancien secret révoqué

Un backup ne rend pas automatiquement valide un ancien token fournisseur.

---

## 85.2 Recréation

La procédure doit pouvoir remplacer le credential par une nouvelle valeur.

---

# 86. Transfert client

Le handover doit identifier :

- Secret IDs ;
- usages ;
- fournisseurs ;
- propriétaires ;
- valeurs à recréer ;
- scopes ;
- procédures de rotation ;
- procédures de révocation ;
- Data Protection Keys ;
- certificats ;
- tokens registry.

---

## 86.1 Valeurs LogicGate

Les credentials appartenant à LogicGate ne sont pas remis tels quels si leur scope couvre d’autres projets.

---

## 86.2 Comptes client

Le transfert privilégie la création de credentials appartenant au client.

---

## 86.3 Rotation finale

Après bascule :

```text
nouveau credential client
→ vérification
→ révocation du credential LogicGate
```

---

# 87. Clôture de service

La clôture doit prévoir :

- backup final ;
- transfert ;
- révocation ;
- suppression des fichiers secrets ;
- suppression des accès ;
- nettoyage du registry ;
- arrêt des webhooks ;
- révocation SMTP ;
- révocation backup ;
- audit final.

---

## 87.1 Effacement

La suppression filesystem ne constitue pas toujours un effacement cryptographique garanti.

La stratégie doit tenir compte du support et du fournisseur.

---

# 88. Configuration de secours

Un secret ne doit pas posséder une valeur de secours codée en dur.

Exemple interdit :

```csharp
password ??= "DefaultPassword123!";
```

---

## 88.1 Refus

L’absence d’une valeur obligatoire produit un échec contrôlé.

---

# 89. Valeur vide

Les distinctions suivantes doivent être explicites :

```text
absent

vide

whitespace

placeholder

invalide

présent
```

---

## 89.1 Secret vide

Un secret vide est invalide.

---

## 89.2 Configuration vide

Une chaîne vide peut être autorisée uniquement si son contrat le prévoit.

---

# 90. Encodage

Les fichiers de secrets texte utilisent :

```text
UTF-8
```

sauf format imposé par le fournisseur.

---

## 90.1 Binaire

Les clés et certificats binaires ne doivent pas être convertis arbitrairement en texte.

---

## 90.2 Base64

Base64 n’est pas un chiffrement.

Une valeur Base64 reste sensible.

---

# 91. Liens symboliques

Les fichiers de secrets ne doivent pas être des liens symboliques pointant hors de la zone contrôlée sans décision explicite.

---

## 91.1 Validation

Le déploiement doit vérifier la cible réelle lorsque possible.

---

# 92. Fichiers temporaires

Un outil créant un fichier temporaire contenant un secret doit :

- utiliser un emplacement protégé ;
- définir les permissions avant l’écriture ;
- supprimer le fichier ;
- éviter les noms prévisibles ;
- ne pas le placer dans `/tmp` partagé sans protection.

---

# 93. Core dumps

Les dumps de processus peuvent contenir des secrets en mémoire.

---

## 93.1 Production

La génération de core dumps doit être contrôlée.

---

## 93.2 Collecte

Un dump utilisé pour diagnostic devient un artefact hautement sensible.

---

# 94. Crash reports

Les rapports de crash ne doivent pas être envoyés automatiquement vers un fournisseur externe avec :

- mémoire ;
- variables ;
- corps de requête ;
- secrets ;

sans configuration et validation explicites.

---

# 95. Sécurité

## 95.1 Actifs

- credentials ;
- clés ;
- tokens ;
- connexions ;
- données ;
- configuration ;
- registry ;
- backups ;
- sessions ;
- intégrations.

---

## 95.2 Menaces

- secret committé ;
- secret dans une image ;
- secret dans un log ;
- lecture filesystem ;
- accès Docker ;
- réutilisation ;
- mauvaise cible ;
- staging vers production ;
- rotation incomplète ;
- endpoint de coffre falsifié ;
- backup non chiffré ;
- transfert incomplet.

---

## 95.3 Contrôles

- classification ;
- fichiers montés ;
- permissions ;
- isolation ;
- options typées ;
- refus par défaut ;
- scans ;
- rotation ;
- audit ;
- moindre privilège ;
- pas de secret en CLI ;
- pas de secret dans les rapports ;
- handover planifié.

---

# 96. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Secret ajouté à Git | scan, revue et `.gitignore` |
| Mot de passe affiché par un script | fichiers montés et sorties contrôlées |
| Credential staging utilisé en production | isolation et validation d’environnement |
| Token partagé entre clients | scope par client |
| Mot de passe dans `docker inspect` | préférence aux fichiers secrets |
| Package contenant un secret | inspection des packages |
| Image contenant un `.env` | scan d’image et `.dockerignore` |
| Rotation incomplète | inventaire et révocation finale |
| Admin fonctionnel lisant un secret | séparation fonctionnel/opérations |
| Handover dépendant d’un token LogicGate | recréation de credentials client |

---

# 97. Secret scanning

Le scan couvre :

- repository ;
- historique pertinent ;
- fichiers générés ;
- packages ;
- images ;
- rapports ;
- templates ;
- workflows.

---

## 97.1 Faux positifs

Un faux positif est traité par :

- preuve ;
- règle limitée ;
- justification ;
- revue.

---

## 97.2 Allowlist

Une allowlist globale large est interdite.

---

## 97.3 Secret réel détecté

Le secret doit être considéré comme compromis jusqu’à preuve contraire.

---

# 98. Tests unitaires

Tester :

- parsing des références ;
- lecture de fichier ;
- fichier absent ;
- fichier vide ;
- fin de ligne terminale ;
- espaces préservés ;
- permissions ou erreurs ;
- validation ;
- redaction ;
- options typées.

---

# 99. Tests d’intégration

Tester :

```text
secret monté
→ application démarre

secret absent
→ échec sûr

secret staging en production
→ refus

rotation de fichier
→ redémarrage
→ nouveau credential utilisé

ancienne valeur révoquée
→ fonctionnement maintenu
```

---

# 100. Tests Docker Compose

Vérifier :

- fichiers montés ;
- lecture seule ;
- absence de valeur inline ;
- absence dans l’image ;
- absence dans les rapports ;
- utilisateur non root ;
- permissions compatibles ;
- secret manquant.

---

# 101. Tests de caractères spéciaux

Tester des valeurs contenant :

```text
!

$

#

espaces

guillemets

apostrophes

backslashes

Unicode

nouvelle ligne terminale
```

La valeur lue doit correspondre au contrat sans interprétation shell.

---

# 102. Tests de non-divulgation

Injecter des valeurs marqueurs et inspecter :

- logs ;
- traces ;
- métriques ;
- rapports ;
- exceptions ;
- health checks ;
- sorties CLI ;
- artefacts.

Aucune valeur ne doit apparaître.

---

# 103. Tests de rotation

Couvrir :

- création du nouveau secret ;
- remplacement atomique ;
- redémarrage ;
- vérification ;
- révocation ;
- rollback pendant la fenêtre ;
- rapport sans valeur.

---

# 104. Tests de configuration

Tester :

- option inconnue ;
- type invalide ;
- placeholder ;
- URL non sécurisée ;
- environnement inconnu ;
- clé obligatoire absente ;
- valeur hors limite ;
- propriété secrète dans le mauvais fichier.

---

# 105. Tests d’isolation

Vérifier que :

- client A ne monte pas les secrets B ;
- staging ne monte pas les secrets de production ;
- le compte runtime n’accède pas au credential migration ;
- le service web n’accède pas au token de publication ;
- Caddy n’accède pas aux credentials PostgreSQL.

---

# 106. Tests CI

Vérifier :

- aucun secret de publication en pull request ;
- OIDC ou secret limité ;
- logs masqués ;
- fichiers temporaires supprimés ;
- package et image inspectés ;
- absence dans les artefacts.

---

# 107. Tests multiplateformes

Le chargement de configuration est testé sur :

```text
Windows x64

Linux x64
```

Les tests de permissions filesystem spécifiques sont exécutés sur Linux.

---

# 108. Quality gates

Une release ou un déploiement est bloqué lorsque :

```text
Secret détecté dans Git

Secret détecté dans un package

Secret détecté dans une image

Secret détecté dans un rapport

Secret transmis en argument CLI

Secret obligatoire absent

Secret vide

Placeholder de production

Configuration inconnue

Configuration non validée

Credential partagé non justifié

Credential staging en production

Permissions trop larges

Token de publication disponible au runtime

Chaîne de connexion journalisée

Fichier `.env` réel committé

Configuration critique modifiable par un administrateur fonctionnel
```

---

# 109. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-RUNTIME-CONFIG-001` | Section de configuration absente |
| `SF-RUNTIME-CONFIG-002` | Propriété obligatoire absente |
| `SF-RUNTIME-CONFIG-003` | Propriété inconnue |
| `SF-RUNTIME-CONFIG-004` | Type invalide |
| `SF-RUNTIME-CONFIG-005` | Valeur hors limite |
| `SF-RUNTIME-CONFIG-006` | Placeholder interdit |
| `SF-RUNTIME-CONFIG-007` | Environnement invalide |
| `SF-RUNTIME-CONFIG-008` | Endpoint invalide |
| `SF-RUNTIME-CONFIG-009` | Configuration de production non sécurisée |
| `SF-RUNTIME-CONFIG-010` | Dérive de configuration détectée |
| `SF-SECRET-001` | Secret obligatoire absent |
| `SF-SECRET-002` | Secret vide |
| `SF-SECRET-003` | Référence de secret inconnue |
| `SF-SECRET-004` | Fichier secret illisible |
| `SF-SECRET-005` | Permissions de secret invalides |
| `SF-SECRET-006` | Secret du mauvais environnement |
| `SF-SECRET-007` | Secret du mauvais client |
| `SF-SECRET-008` | Secret expiré |
| `SF-SECRET-009` | Rotation requise |
| `SF-SECRET-010` | Révocation requise |
| `SF-SECRET-011` | Secret détecté dans Git |
| `SF-SECRET-012` | Secret détecté dans un rapport |
| `SF-SECRET-013` | Secret détecté dans une image |
| `SF-SECRET-014` | Secret transmis par argument |
| `SF-SECRET-015` | Valeur de secret journalisée |
| `SF-SECRET-016` | Credential partagé non autorisé |
| `SF-SECRET-017` | Secret de staging utilisé en production |
| `SF-SECRET-018` | Remplacement atomique échoué |
| `SF-SECRET-019` | Ancien credential non révoqué |
| `SF-SECRET-020` | Inventaire de secret incomplet |
| `SF-SECRET-021` | Matériel cryptographique manquant |
| `SF-SECRET-022` | Secret non transférable |
| `SF-SECRET-023` | Source de secret non approuvée |
| `SF-SECRET-024` | Rechargement dynamique non supporté |
| `SF-SECRET-025` | Scope de credential excessif |

---

# 110. Observabilité

Événements recommandés :

```text
RuntimeConfigurationValidationStarted

RuntimeConfigurationValidated

RuntimeConfigurationValidationFailed

SecretRequirementValidated

SecretMissing

SecretRotationStarted

SecretRotationCompleted

SecretRotationFailed

SecretRevoked

ConfigurationDriftDetected
```

---

## 110.1 Propriétés autorisées

```text
SecretId

ConfigurationSection

ClientSlug

Environment

Provider

Operation

Result

ErrorCode

RotationRequired
```

---

## 110.2 Données interdites

```text
SecretValue

SecretHash

TokenPrefix

ConnectionString

Password

PrivateKey

SignedUrl
```

---

# 111. Audit

Les actions suivantes doivent être auditables :

- création d’un secret ;
- rotation ;
- révocation ;
- changement de scope ;
- modification de configuration production ;
- changement de fournisseur ;
- transfert ;
- lecture exceptionnelle d’une valeur ;
- ajout ou retrait d’un opérateur.

---

## 111.1 Valeur

L’audit enregistre l’action, jamais la valeur.

---

# 112. Règles pour LogicGate

LogicGate doit :

- classifier chaque valeur ;
- créer des Secret IDs stables ;
- limiter les scopes ;
- maintenir l’inventaire ;
- planifier les rotations ;
- protéger les accès ;
- vérifier les déploiements ;
- préparer les transferts ;
- révoquer les accès inutiles.

---

## 112.1 LogicGate ne doit pas

- demander un secret dans un ticket non sécurisé ;
- réutiliser un credential ;
- ajouter un secret dans un fichier d’exemple ;
- conserver un token partagé sans justification ;
- modifier un secret sans rapport ;
- considérer `.env` comme un coffre.

---

# 113. Règles pour Codex

Codex peut :

- créer les options typées ;
- ajouter la validation ;
- ajouter la lecture depuis un fichier ;
- générer les fichiers `.example` ;
- ajouter les tests ;
- créer les runbooks ;
- signaler un besoin de secret.

---

## 113.1 Codex ne doit pas

```text
Inventer une valeur de production

Ajouter un secret dans Git

Afficher une valeur dans le compte-rendu

Passer un secret dans une commande

Ajouter un fallback codé en dur

Lire directement les variables d’environnement dans Domain

Ajouter IConfiguration partout

Désactiver une validation de production

Modifier un fichier secret réel

Créer un token fournisseur

Révoquer un credential réel

Appliquer une rotation en production
```

---

## 113.2 Valeur manquante

Si une tâche nécessite un secret réel, Codex doit :

- utiliser une référence ;
- utiliser une valeur de test ;
- documenter la valeur attendue ;
- laisser l’opération réelle à LogicGate.

---

# 114. Anti-patterns interdits

## 114.1 Secret dans `appsettings.json`

Les valeurs ci-dessous sont des placeholders explicites, pas des credentials.
L'anti-pattern reste l'emplacement d'une **valeur réelle** à leur place dans
un artefact versionné ; remplacer le nom de cette valeur ne l'autorise pas.

```json
{
  "Password": "<SECRET_VALUE_FORBIDDEN_IN_GIT>"
}
```

---

## 114.2 Secret dans `client.sitefactory.yml`

```yaml
smtp:
  password: <SECRET_VALUE_FORBIDDEN_IN_GIT>
```

---

## 114.3 Secret dans un argument

```bash
deploy --postgres-password '<SECRET_VALUE_FORBIDDEN_IN_ARGUMENTS>'
```

---

## 114.4 Secret dans une image

```dockerfile
ENV SMTP_PASSWORD=<SECRET_VALUE_FORBIDDEN_IN_IMAGE>
```

---

## 114.5 Secret partagé

Le même token est utilisé pour plusieurs clients et environnements.

---

## 114.6 Fallback dangereux

```csharp
var password = configuredPassword ?? "admin";
```

---

## 114.7 `.env` chargé par le shell

```bash
source .env
```

---

## 114.8 Configuration brute dans Domain

Une entité métier reçoit `IConfiguration`.

---

## 114.9 Secret dans le health check

La réponse affiche la chaîne de connexion.

---

## 114.10 Rotation sans révocation

Le nouveau credential est créé mais l’ancien reste actif indéfiniment.

---

## 114.11 Hash de secret dans le rapport

Une empreinte permet de comparer les secrets de plusieurs clients.

---

## 114.12 Redaction comme unique protection

Le code journalise délibérément la valeur en espérant que le backend la masque.

---

# 115. Conséquences positives

La décision apporte :

- séparation claire ;
- absence de secrets dans Git ;
- isolation par client ;
- rotation ;
- moindre privilège ;
- options typées ;
- validation ;
- transfert facilité ;
- compatibilité avec un coffre futur ;
- réduction des fuites ;
- meilleure traçabilité ;
- configuration runtime reproductible ;
- images indépendantes de l’environnement.

---

# 116. Conséquences négatives

La décision entraîne :

- provisioning de fichiers ;
- permissions à gérer ;
- inventaire ;
- rotation ;
- runbooks ;
- abstraction de lecture ;
- davantage de tests ;
- redémarrage pour certaines modifications ;
- gestion du handover ;
- configuration plus détaillée ;
- risque d’erreur opérateur.

---

# 117. Risques acceptés

## 117.1 Secrets présents sur le VPS

Les fichiers secrets restent présents sur l’hôte.

Réduction :

- permissions ;
- accès restreint ;
- chiffrement disque éventuel ;
- isolation ;
- rotation ;
- VPS dédié si nécessaire.

---

## 117.2 Accès Docker privilégié

Un opérateur Docker peut accéder aux montages.

Réduction :

- accès strict ;
- audit ;
- révocation ;
- comptes séparés.

---

## 117.3 Redémarrage requis

Certaines rotations nécessitent une recréation de conteneur.

Ce coût est accepté pour éviter un rechargement dynamique fragile.

---

## 117.4 Absence de coffre central

Le MVP ne bénéficie pas de toutes les fonctions d’un coffre.

Cette limite est acceptée afin de préserver la simplicité et l’autonomie client.

---

# 118. Mesures compensatoires

- fichiers montés ;
- permissions restrictives ;
- Secret IDs ;
- inventaire ;
- options typées ;
- validation ;
- isolation ;
- scans ;
- rotation ;
- révocation ;
- rapports sans valeur ;
- audit ;
- backups sécurisés ;
- runbooks ;
- handover ;
- possibilité future de coffre.

---

# 119. Performance

La lecture des secrets essentiels se produit principalement au démarrage.

---

## 119.1 Hot path

Un fichier secret ne doit pas être relu à chaque requête.

---

## 119.2 Cache

Les adaptateurs peuvent conserver les valeurs nécessaires pendant la durée du processus.

---

## 119.3 Rotation

Une rotation nécessite alors un redémarrage ou mécanisme explicite.

---

# 120. Coûts

## 120.1 Coûts supplémentaires

- stockage sécurisé ;
- temps opérateur ;
- rotation ;
- audit ;
- tests ;
- documentation ;
- éventuel coffre futur.

---

## 120.2 Coûts évités

- fuite de credentials ;
- exposition Git ;
- compromission inter-client ;
- rebuild pour rotation ;
- transfert incomplet ;
- incident lié à un mauvais environnement ;
- secret dans une image permanente.

---

# 121. Maintenabilité

Pour chaque valeur, il doit être possible de répondre :

```text
Est-elle structurelle ?

Est-elle propre à un environnement ?

Est-elle sensible ?

Qui la possède ?

Où est-elle stockée ?

Qui peut la lire ?

Comment est-elle validée ?

Comment est-elle rotatée ?

Comment est-elle révoquée ?

Comment est-elle transférée ?
```

---

# 122. Compatibilité

## 122.1 ASP.NET Core

La solution reste compatible avec le système de configuration standard.

---

## 122.2 Docker Compose

Les fichiers secrets montés sont compatibles avec le profil initial.

---

## 122.3 Coffre futur

Un adaptateur futur pourra résoudre les mêmes Secret IDs depuis un coffre.

---

## 122.4 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

La migration d’un projet contenant des secrets versionnés constitue une opération de sécurité importante.

---

# 123. Migration depuis des secrets versionnés

Le processus est :

```text
identifier les secrets
→ considérer les valeurs compromises
→ révoquer
→ générer de nouvelles valeurs
→ créer les fichiers sécurisés
→ adapter la configuration
→ nettoyer le repository
→ analyser l’historique
→ scanner les artefacts
→ redéployer
→ vérifier
```

---

## 123.1 Réécriture Git

Une réécriture de l’historique peut être nécessaire, mais elle ne remplace jamais la révocation.

---

## 123.2 Clones existants

Une ancienne valeur peut rester présente dans :

- clones ;
- forks ;
- caches ;
- artefacts ;
- backups.

---

## 123.3 Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation de modifier ou révoquer des credentials réels.

---

# 124. Migration depuis des variables d’environnement

Une valeur sensible peut être déplacée vers un fichier monté :

```text
inventorier
→ créer le fichier
→ modifier la référence
→ déployer
→ vérifier
→ supprimer la variable
→ recréer le conteneur
→ vérifier docker inspect
```

---

# 125. Migration vers un coffre

Le processus futur pourra être :

```text
conserver les Secret IDs
→ ajouter un adaptateur
→ configurer l’identité machine
→ tester
→ migrer les valeurs
→ vérifier
→ retirer les fichiers
→ révoquer les anciens accès
```

---

## 125.1 Compatibilité

Les modules ne doivent pas changer pour connaître le fournisseur du coffre.

---

# 126. Réversibilité

## 126.1 Classification

```text
élevée
```

Les Secret IDs et abstractions permettent de changer la méthode de stockage.

---

## 126.2 Retour aux secrets versionnés

Un retour vers des valeurs dans Git est interdit sans nouvel ADR et justification exceptionnelle.

---

# 127. Plan d’implémentation

## Étape 1 — Classification

Définir les catégories de configuration dans les contrats SiteFactory.

---

## Étape 2 — Options

Créer les options typées et validations.

---

## Étape 3 — Secret references

Définir :

- `SecretId` ;
- `SecretReference` ;
- erreurs ;
- métadonnées.

---

## Étape 4 — File reader

Implémenter un adaptateur de lecture de fichiers sécurisé.

---

## Étape 5 — Docker Compose

Générer :

- noms de secrets ;
- montages ;
- chemins ;
- fichiers `.example`.

---

## Étape 6 — Permissions

Ajouter les validations Linux et les scripts de provisioning.

---

## Étape 7 — Configuration PostgreSQL

Séparer :

- host ;
- database ;
- username ;
- password file.

---

## Étape 8 — Providers

Appliquer le modèle à :

- SMTP ;
- registry ;
- backups ;
- OTLP ;
- intégrations.

---

## Étape 9 — Rotation

Créer les premiers runbooks de rotation.

---

## Étape 10 — Quality gates

Ajouter :

- scans ;
- tests de non-divulgation ;
- placeholders ;
- isolation ;
- inspection d’image.

---

# 128. Tâches liées proposées

```text
SF-TASK-120 — Define runtime configuration classifications

SF-TASK-121 — Add typed runtime options and startup validation

SF-TASK-122 — Define secret references and identifiers

SF-TASK-123 — Implement the mounted file secret reader

SF-TASK-124 — Generate Docker Compose secret declarations

SF-TASK-125 — Add Linux secret permission validation

SF-TASK-126 — Separate PostgreSQL connection settings and credentials

SF-TASK-127 — Add SMTP secret file configuration

SF-TASK-128 — Add runtime secret non-disclosure tests

SF-TASK-129 — Add secret rotation inventory and runbooks

SF-TASK-130 — Add configuration drift reporting

SF-TASK-131 — Add client handover secret inventory generation
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 129. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
La configuration structurelle reste dans le YAML.

Les secrets ne sont pas dans le YAML.

Les secrets ne sont pas dans Git.

Les secrets ne sont pas dans les images.

Les secrets ne sont pas dans les packages.

Les secrets ne sont pas dans les rapports.

Les options sont typées.

Les options critiques sont validées au démarrage.

Les placeholders sont refusés en production.

Un secret absent provoque un échec sûr.

Un secret vide est refusé.

Les caractères spéciaux sont préservés.

Les fichiers secrets sont montés en lecture seule.

Les permissions Linux sont vérifiées.

Staging et production utilisent des secrets distincts.

Runtime et migration PostgreSQL utilisent des credentials distincts.

Une rotation atomique est testée.

L’ancien credential est révoqué.

Une panne de secret optionnel désactive la capability concernée.

Un administrateur fonctionnel ne peut pas lire les secrets.

Les logs et traces ne contiennent aucune valeur.

Le handover identifie les secrets à recréer.

Le stockage pourra être remplacé par un coffre sans modifier Domain.
```

---

# 130. Conditions de révision

Réexaminer cette décision si :

- les secrets doivent être rechargés sans redémarrage ;
- le nombre de VPS rend les fichiers difficiles à maintenir ;
- un coffre central devient économiquement pertinent ;
- des clients imposent leur propre gestionnaire de secrets ;
- une architecture Kubernetes est adoptée ;
- plusieurs instances doivent partager des rotations immédiates ;
- une PKI interne devient nécessaire ;
- des secrets à durée très courte deviennent la norme ;
- les exigences réglementaires interdisent le stockage sur le VPS ;
- le handover nécessite un mécanisme standardisé de coffre ;
- l’identité machine par OIDC remplace la majorité des secrets statiques.

---

# 131. Indicateurs à suivre

- nombre de secrets par projet ;
- secrets expirant bientôt ;
- secrets non rotatés ;
- secrets partagés ;
- secrets manquants ;
- placeholders détectés ;
- fuites détectées ;
- incidents de permissions ;
- échecs de rotation ;
- anciens credentials encore actifs ;
- temps moyen de rotation ;
- credentials longue durée ;
- projets sans inventaire ;
- dérives de configuration ;
- secrets devant être recréés lors d’un handover.

---

# 132. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des secrets apparaissent dans Git

des mots de passe sont passés en ligne de commande

des conteneurs utilisent des variables sensibles inutilement

des secrets sont partagés entre clients

des credentials de staging atteignent la production

les permissions sont régulièrement élargies

les rotations laissent les anciennes valeurs actives

les rapports contiennent des empreintes de secrets

les administrateurs fonctionnels accèdent aux credentials

les projets ne peuvent pas être reconstruits après transfert

les équipes utilisent source .env

les modules lisent directement les variables d’environnement
```

---

# 133. Conséquences observées

Cette section est append-only après mise en œuvre.

## 133.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète
du système de configuration runtime et de secrets SiteFactory.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- projet technique ;
- type de secret ;
- stockage ;
- rotation ;
- incident ou bénéfice ;
- action proposée.

---

# 134. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0023-data-retention-deletion-and-privacy-lifecycle.md](./0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0020-administrator-session-security-and-step-up-authentication.md](./0020-administrator-session-security-and-step-up-authentication.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- [15-cli-specification.md](../03-generation/15-cli-specification.md)
- [18-security-baseline.md](../04-quality-security/18-security-baseline.md)
- [19-testing-strategy.md](../04-quality-security/19-testing-strategy.md)
- [20-quality-gates-and-ci.md](../04-quality-security/20-quality-gates-and-ci.md)
- [21-observability-and-logging.md](../05-operations/21-observability-and-logging.md)
- [22-deployment-and-environments.md](../05-operations/22-deployment-and-environments.md)
- [23-backup-and-restore.md](../05-operations/23-backup-and-restore.md)
- [24-operational-runbooks.md](../05-operations/24-operational-runbooks.md)
- [25-upgrade-and-maintenance-strategy.md](../05-operations/25-upgrade-and-maintenance-strategy.md)
- [26-release-and-versioning.md](../05-operations/26-release-and-versioning.md)
- [27-client-handover-and-service-closure.md](../05-operations/27-client-handover-and-service-closure.md)
- [28-codex-operating-guide.md](../06-codex/28-codex-operating-guide.md)
- [0000-adr-template.md](./0000-adr-template.md)
- [0001-one-repository-and-database-per-client.md](./0001-one-repository-and-database-per-client.md)
- [0002-yaml-client-configuration.md](./0002-yaml-client-configuration.md)
- [0003-postgresql-as-primary-database.md](./0003-postgresql-as-primary-database.md)
- [0004-explicit-production-migrations.md](./0004-explicit-production-migrations.md)
- [0006-feature-flags-and-entitlements-separation.md](./0006-feature-flags-and-entitlements-separation.md)
- [0008-private-package-feed-and-supply-chain.md](./0008-private-package-feed-and-supply-chain.md)
- [0009-client-customizations-and-extension-points.md](./0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](./0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](./0011-docker-compose-vps-deployment-profile.md)
- [0037-compose-modules-explicitly-without-dynamic-loading.md](./0037-compose-modules-explicitly-without-dynamic-loading.md)
- [0033-split-public-sitefactory-and-private-logicgate-modules.md](./0033-split-public-sitefactory-and-private-logicgate-modules.md)

---

# 135. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-28 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 130 |

---

# 136. Historique

| Date | Changement |
|---|---|
| 2026-07-28 | Proposition initiale |
| 2026-07-28 | Décision acceptée comme baseline du MVP |

---

# 137. Synthèse de la décision

```text
Configuration structurelle
→ client.sitefactory.yml
→ versionnée
→ sans secret

Composition résolue
→ sitefactory.lock.json
→ versions et modules
→ sans secret

Configuration runtime non sensible
→ appsettings
→ fichier monté
→ variables d’environnement
→ options typées
→ validation au démarrage

Secrets
→ Secret IDs
→ fichiers protégés
→ montés en lecture seule
→ jamais dans Git
→ jamais dans l’image
→ jamais dans les rapports
→ jamais en argument CLI

Isolation
→ un client
→ un environnement
→ un secret propre
→ un scope minimal

Rotation
→ nouvelle valeur
→ remplacement atomique
→ redémarrage
→ vérification
→ révocation de l’ancienne valeur
→ audit

Production
→ placeholders refusés
→ secrets manquants refusés
→ staging séparé
→ configuration critique validée

Architecture
→ Domain indépendant
→ IConfiguration limité à la composition
→ modules utilisant des options typées
→ coffre futur remplaçable

Transfert
→ inventaire sans valeurs
→ credentials client recréés
→ accès LogicGate révoqués
```

> SiteFactory ne traite pas un secret comme une simple chaîne de configuration. Il le traite comme un actif opérationnel isolé, limité, rotatable et absent de tous les artefacts destinés à être versionnés, distribués ou observés.
