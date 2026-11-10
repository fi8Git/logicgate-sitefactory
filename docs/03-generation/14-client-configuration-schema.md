# LogicGate SiteFactory — Schéma de configuration client

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Schéma de configuration client |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, architectes, développeurs, contributeurs, Codex |
| Fichier concerné | `client.sitefactory.yml` |
| Schéma machine cible | `schemas/client-configuration.schema.json` |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `Contrat public du scenario : identifiants, versions, capacites, dependances et oracles synthetiques explicites`, `Regles du scenario public : options autorisees, dependances, validite et montants synthetiques avec oracles explicites`, `13-generation-pipeline.md`, `Contrat public des pages et sections du module concerne : identifiants stables, schemas, validation et strategies de fichiers explicites`, `Contrat public de rendu du module concerne : composants et styles versionnes, encodage, accessibilite et securite du contenu`, `../02-architecture/core-module-boundary.md`, `09-module-architecture.md`, `10-feature-flags-and-entitlements.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `13-generation-pipeline.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Configuration publique V1 réellement acceptée

La [fixture publique](../../tests/fixtures/public/configuration/valid-minimal/client.sitefactory.yml)
est l'exemple exécutable de `client.sitefactory.yml` :

```yaml
schemaVersion: 1
client:
  slug: fictitious-documentation
  displayName: Fictitious Documentation
site:
  product: documentation
  template: documentation-minimal
  defaultLocale: fr-FR
  publicBaseUrl: https://example.test
modules:
  enabled: []
deployment:
  profile: docker-compose-vps
git:
  initializeLocalRepository: true
  publishRemote: false
```

Le chargement est strict : clés/types/propriétés inconnus et YAML non supporté
sont refusés ; diagnostics bornés, sans echo du document utilisateur.
`site.product`/`site.template` sont résolus par la composition injectée.
La CLI publique compose seulement Documentation ; aucun fallback d'offre.
Les clés Git et deployment sont validées mais ne déclenchent aucune opération.
`modules.enabled` doit rester vide. Aucun secret ni connexion dans le YAML.

La validation effective appartient à
[ClientConfigurationV1Validator](../../src/LogicGate.SiteFactory.Application/Configuration/ClientConfigurationV1Validator.cs)
et au chargement Infrastructure. Le YAML enrichi des sections numérotées
ci-dessous (`sector`, `pack`, `carePlan`, formulaires, providers, variantes
d'environnement) est un **contrat cible non accepté par cette V1**.
Il ne remplace pas les contrats runtime typés ni les fichiers secrets externes.
Le schéma machine indiqué dans les métadonnées reste une cible, pas une ressource
dont la présence serait requise pour exécuter le public.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit le contrat fonctionnel et technique du fichier :

```text
client.sitefactory.yml
```

Ce fichier décrit de manière déclarative un projet client destiné à être validé, résolu et généré par LogicGate SiteFactory.

Il précise notamment :

- la structure YAML ;
- les propriétés obligatoires ;
- les propriétés optionnelles ;
- les identifiants autorisés ;
- les formats attendus ;
- les valeurs par défaut ;
- les règles de validation ;
- les références au catalogue ;
- la sélection du pack ;
- la sélection des modules ;
- les réglages des modules ;
- le template ;
- le thème ;
- les pages ;
- les sections ;
- les contenus initiaux ;
- les paramètres SEO ;
- les rôles et permissions ;
- les règles de conservation ;
- les paramètres opérationnels ;
- le profil de déploiement ;
- les données interdites ;
- la stratégie de versionnement ;
- la relation avec le lock file ;
- les exigences applicables à Codex.

Ce document constitue la référence humaine du schéma.

Le schéma JSON machine devra appliquer les mêmes règles sans les contredire.

---

## 2. Rôle de `client.sitefactory.yml`

Le fichier de configuration client représente la source de vérité humaine du projet généré.

Il répond notamment aux questions suivantes :

```text
Pour quel client le projet est-il généré ?

Quel pack a été validé ?

Quel template sectoriel est utilisé ?

Quel thème est appliqué ?

Quels modules optionnels ont été sélectionnés ?

Quelles pages sont activées ?

Comment les pages sont-elles composées ?

Quelles options fonctionnelles sont configurées ?

Quel profil de déploiement est prévu ?
```

Le fichier ne représente pas :

- un devis signé ;
- un catalogue de prix ;
- un fichier de secrets ;
- un fichier `.env` ;
- un lock file ;
- un rapport de génération ;
- un dump de données ;
- une configuration complète de production ;
- un remplacement des contenus administrables ;
- une autorisation automatique de déploiement.

Le flux de référence est :

```text
Diagnostic
→ devis validé
→ configuration client
→ validation
→ résolution
→ plan de génération
→ génération
→ lock file
→ build et tests
→ recette
→ déploiement contrôlé
```

---

## 3. Principes du schéma

## 3.1 Déclaratif

La configuration décrit le résultat souhaité.

Elle ne doit pas contenir :

- de script ;
- de commande système ;
- de code C# ;
- de fragment SQL ;
- de logique conditionnelle arbitraire ;
- de téléchargement distant ;
- d’action de déploiement.

Exemple attendu :

```yaml
modules:
  portfolio-basic:
    enabled: true
```

Exemple interdit :

```yaml
modules:
  portfolio-basic:
    installCommand: dotnet add package Some.Package
```

---

## 3.2 Versionné

La configuration possède une version de schéma explicite :

```yaml
schemaVersion: 1
```

Une version inconnue doit être refusée.

Aucune conversion incompatible ne doit être appliquée silencieusement.

---

## 3.3 Reproductible

Une configuration identique, utilisée avec les mêmes versions du catalogue, du générateur, du template, du thème et des modules, doit produire un résultat fonctionnellement équivalent.

Les éléments susceptibles de modifier le résultat doivent être explicites ou verrouillés.

---

## 3.4 Sans secret

Le fichier ne doit jamais contenir :

- mot de passe ;
- token ;
- clé API ;
- clé privée ;
- secret SMTP ;
- chaîne de connexion réelle ;
- credentials NuGet ;
- certificat privé ;
- valeur d’un secret Docker.

Le fichier peut déclarer qu’un secret est attendu, mais pas sa valeur.

---

## 3.5 Strict

Les propriétés inconnues sont refusées par défaut.

Cette règle permet de détecter les fautes de frappe.

Exemple invalide :

```yaml
moduls:
  lead-management:
    enabled: true
```

Le système doit signaler que `moduls` est inconnu et proposer `modules`.

---

## 3.6 Minimal

La configuration doit contenir uniquement les décisions propres au projet.

Les informations pouvant être dérivées du catalogue ou des manifests ne doivent pas être recopiées inutilement.

Exemples à ne pas dupliquer :

- prix du pack ;
- prix des modules ;
- dépendances ;
- package NuGet ;
- permissions internes d’un module ;
- modules inclus dans le pack ;
- version résolue d’une dépendance transitive.

Ces informations sont enregistrées après résolution dans :

```text
sitefactory.lock.json
```

---

## 3.7 Séparation fonctionnel et opérationnel

Le fichier contient les choix fonctionnels durables.

Les paramètres opérationnels variables selon l’environnement sont fournis par :

- variables d’environnement ;
- fichiers de production non versionnés ;
- configuration opérateur ;
- secret manager futur.

Une variable runtime ne peut pas modifier le pack ou ajouter un entitlement commercial absent de la configuration validée.

---

# 4. Fichiers associés

## 4.1 Configuration humaine

```text
client.sitefactory.yml
```

Modifié par LogicGate après validation humaine.

---

## 4.2 Lock file

```text
sitefactory.lock.json
```

Généré par SiteFactory.

Il contient les versions réellement résolues.

---

## 4.3 Rapport de génération

```text
generation-report.json
```

Généré par SiteFactory.

Il décrit les étapes et résultats de la génération.

---

## 4.4 Contenus initiaux

Les contenus volumineux peuvent être stockés séparément.

Structure recommandée :

```text
content/
├── site.yml
├── home/
│   ├── hero.yml
│   ├── services.yml
│   └── faq.yml
├── services/
├── portfolio/
└── legal/
```

La configuration principale référence ces contenus sans les recopier intégralement.

---

## 4.5 Configuration d’environnement

Les secrets et paramètres runtime ne sont pas stockés dans le fichier client.

Exemples :

```text
.env
.env.production
Docker secrets
variables GitHub Actions
variables du VPS
```

Ces fichiers restent hors Git lorsqu’ils contiennent des valeurs sensibles.

---

# 5. Structure générale

Structure normative de haut niveau :

```yaml
schemaVersion: 1
catalogVersion: 1.0.0

client:
  # Informations du projet client

offer:
  # Pack et service mensuel

template:
  # Template sectoriel

theme:
  # Thème et personnalisations visuelles

modules:
  # Modules optionnels et réglages

navigation:
  # Navigation publique

pages:
  # Pages et composition

content:
  # Sources des contenus initiaux

seo:
  # Paramètres SEO transversaux

identity:
  # Rôles et règles d’administration

dataRetention:
  # Règles de conservation configurables

operations:
  # Baseline opérationnelle

deployment:
  # Profil de déploiement

generation:
  # Options durables de génération
```

Toutes les sections ne sont pas obligatoires.

Les sections obligatoires du MVP sont :

```text
schemaVersion
catalogVersion
client
offer
template
theme
deployment
```

---

# 6. Propriétés de premier niveau

| Propriété | Type | Obligatoire | Description |
|---|---|---:|---|
| `schemaVersion` | entier | Oui | Version du schéma de configuration |
| `catalogVersion` | chaîne SemVer | Oui | Version du catalogue commercial et fonctionnel |
| `client` | objet | Oui | Identification non sensible du client |
| `offer` | objet | Oui | Pack et service mensuel validés |
| `template` | objet | Oui | Template sectoriel |
| `theme` | objet | Oui | Thème et personnalisations |
| `modules` | map | Non | Modules optionnels et réglages |
| `navigation` | objet | Non | Navigation publique personnalisée |
| `pages` | map | Non | Composition des pages |
| `content` | objet | Non | Références vers les contenus initiaux |
| `seo` | objet | Non | Configuration SEO transversale |
| `identity` | objet | Non | Rôles et permissions initiales |
| `dataRetention` | objet | Non | Politiques de conservation |
| `operations` | objet | Non | Configuration opérationnelle non secrète |
| `deployment` | objet | Oui | Profil de déploiement |
| `generation` | objet | Non | Options techniques durables |

---

# 7. `schemaVersion`

## 7.1 Type

```yaml
schemaVersion: 1
```

Type :

```text
integer
```

Valeur minimale :

```text
1
```

---

## 7.2 Rôle

`schemaVersion` versionne la structure du fichier YAML.

Il ne doit pas être confondu avec :

- version du générateur ;
- version du catalogue ;
- version du template ;
- version du thème ;
- version des modules.

---

## 7.3 Version inconnue

Exemple :

```yaml
schemaVersion: 99
```

Résultat attendu :

```text
SF-CONFIG-007

La version 99 du schéma client n’est pas supportée
par cette version de SiteFactory.
```

---

# 8. `catalogVersion`

## 8.1 Type

```yaml
catalogVersion: 1.0.0
```

Format :

```text
SemVer
```

---

## 8.2 Rôle

Cette version détermine notamment :

- les packs ;
- les modules ;
- les inclusions ;
- les entitlements ;
- les compatibilités ;
- les services mensuels ;
- les prix de référence utilisés lors du cadrage.

Les prix eux-mêmes ne sont pas enregistrés dans la configuration.

---

## 8.3 Version obligatoire

Le MVP exige une version explicite.

Il ne doit pas utiliser automatiquement :

```text
latest
current
*
```

---

# 9. Section `client`

## 9.1 Structure

```yaml
client:
  name: Example Project
  slug: sample-project
  domain: example.test
  sector: sample-application
```

---

## 9.2 Propriétés

| Propriété | Type | Obligatoire | Description |
|---|---|---:|---|
| `name` | chaîne | Oui | Nom d’affichage du client |
| `slug` | chaîne | Oui | Identifiant technique stable du projet |
| `domain` | chaîne | Oui | Domaine public prévu |
| `sector` | chaîne | Oui | Identifiant du secteur |
| `legalName` | chaîne | Non | Raison sociale, si différente |
| `locale` | chaîne | Non | Locale principale |
| `timeZone` | chaîne | Non | Fuseau horaire d’affichage |

---

## 9.3 `name`

Contraintes recommandées :

```text
longueur : 2 à 200 caractères
valeur non vide
aucun caractère de contrôle
```

Cette propriété peut contenir des espaces, accents et caractères de marque légitimes.

---

## 9.4 `slug`

Format :

```text
kebab-case
```

Expression conceptuelle :

```text
^[a-z0-9]+(?:-[a-z0-9]+)*$
```

Contraintes :

- 3 à 80 caractères ;
- commence par une lettre ou un chiffre ;
- aucun espace ;
- aucun accent ;
- aucun underscore ;
- aucun chemin ;
- aucun point ;
- aucun nom réservé.

Exemples valides :

```text
sample-project
cabinet-alpha
dupont-renovation
```

Exemples invalides :

```text
Cabinet Alpha
cabinet_alpha
../cabinet-alpha
cabinet.alpha
```

Le slug sert notamment à dériver :

- nom du repository ;
- préfixe Docker ;
- noms de volumes ;
- identifiants de déploiement.

---

## 9.5 `domain`

Exemples :

```yaml
domain: cabinet-alpha.fr
```

ou pour une démonstration :

```yaml
domain: cabinet-alpha.test
```

Contraintes :

- nom de domaine uniquement ;
- aucun protocole ;
- aucun chemin ;
- aucune query string ;
- longueur valide ;
- caractères normalisés.

Exemple invalide :

```yaml
domain: https://cabinet-alpha.fr/contact
```

Les sous-domaines sont autorisés :

```yaml
domain: preview.cabinet-alpha.fr
```

---

## 9.6 `sector`

Le secteur doit correspondre à un identifiant connu.

MVP :

```text
sample-application
```

Il ne doit pas être un texte libre lorsqu’il pilote la sélection du template.

---

## 9.7 `locale`

Valeur par défaut :

```text
fr-FR
```

Exemple :

```yaml
locale: fr-FR
```

Le multilingue reste hors MVP.

---

## 9.8 `timeZone`

Valeur recommandée pour les projets français :

```text
Europe/Paris
```

Le fuseau doit utiliser un identifiant IANA valide.

La machine serveur ne doit pas devenir implicitement la source du fuseau d’affichage.

---

# 10. Section `offer`

## 10.1 Structure

```yaml
offer:
  pack: sample-application
  carePlan: sample-support
```

---

## 10.2 Propriétés

| Propriété | Type | Obligatoire | Description |
|---|---|---:|---|
| `pack` | chaîne | Oui | Identifiant du pack validé |
| `carePlan` | chaîne ou `null` | Non | Service mensuel prévu |
| `commercialReference` | chaîne | Non | Référence interne du devis ou dossier |

---

## 10.3 Packs autorisés

```text
site-pro
sample-application
sample-automation
custom-project
```

Un pack doit :

- exister dans le catalogue demandé ;
- être disponible ;
- être compatible avec le template ;
- être cohérent avec les modules sélectionnés.

---

## 10.4 Plans mensuels

Valeurs initiales :

```text
care-essential
sample-support
care-secure
null
```

Exemple sans service mensuel :

```yaml
offer:
  pack: sample-application
  carePlan: null
```

Le plan mensuel ne doit pas activer ou désactiver les protections techniques fondamentales.

---

## 10.5 Prix interdits

Les propriétés suivantes sont interdites :

```yaml
offer:
  price: 6400
  monthlyPrice: 249
  discount: 500
```

Les montants appartiennent au processus commercial et au catalogue, pas au schéma technique du projet.

---

## 10.6 `commercialReference`

Exemple :

```yaml
commercialReference: DEV-2026-0042
```

Cette référence :

- reste optionnelle ;
- ne contient pas de secret ;
- ne constitue pas une autorisation automatique de génération ;
- peut faciliter la traçabilité.

---

# 11. Section `template`

## 11.1 Structure

```yaml
template:
  id: sample-application
  version: 1.0.0
```

---

## 11.2 Propriétés

| Propriété | Type | Obligatoire |
|---|---|---:|
| `id` | chaîne | Oui |
| `version` | SemVer | Oui |

---

## 11.3 Validation

Le template doit :

- exister ;
- être dans un statut utilisable ;
- être compatible avec le générateur ;
- être compatible avec le pack ;
- être compatible avec les modules ;
- être compatible avec le thème.

Le MVP supporte :

```text
sample-application
```

---

## 11.4 Version flottante interdite

Exemples interdits :

```yaml
version: latest
```

```yaml
version: 1.*
```

---

# 12. Section `theme`

## 12.1 Structure

```yaml
theme:
  id: professional-clean
  version: 1.0.0

  overrides:
    colors:
      brandPrimary: "#2563EB"
      brandSecondary: "#14B8A6"

    typography:
      headingFamily: Inter
      bodyFamily: Inter

    radius:
      preset: soft
```

---

## 12.2 Propriétés

| Propriété | Type | Obligatoire |
|---|---|---:|
| `id` | chaîne | Oui |
| `version` | SemVer | Oui |
| `overrides` | objet | Non |

---

## 12.3 Thème MVP

```text
professional-clean
```

---

## 12.4 Overrides autorisés

Les catégories initiales sont :

```text
colors
typography
radius
elevation
layout
```

Les valeurs autorisées doivent être définies par le schéma du thème.

Une propriété CSS arbitraire ne doit pas être acceptée.

---

## 12.5 Couleurs

Exemple :

```yaml
overrides:
  colors:
    brandPrimary: "#123A63"
    brandSecondary: "#D97706"
```

Formats autorisés initialement :

```text
#RRGGBB
```

Les valeurs alpha ou fonctions CSS peuvent être interdites dans le MVP afin de simplifier la validation.

---

## 12.6 Validation de contraste

Une couleur valide syntaxiquement peut rester invalide fonctionnellement si elle produit un contraste insuffisant.

La validation doit vérifier au minimum :

- texte sur bouton principal ;
- focus ;
- liens ;
- surfaces principales ;
- états.

---

## 12.7 Typographies

Exemple :

```yaml
typography:
  headingFamily: Manrope
  bodyFamily: Inter
```

Les polices doivent :

- appartenir à une liste autorisée ;
- disposer d’une licence compatible ;
- être disponibles dans le projet ;
- avoir un fallback.

Une URL de police arbitraire est interdite.

---

## 12.8 Radius

Exemple recommandé :

```yaml
radius:
  preset: soft
```

Valeurs possibles :

```text
square
subtle
soft
rounded
```

Les valeurs CSS libres sont hors MVP sauf mécanisme explicitement validé.

---

# 13. Section `modules`

## 13.1 Structure

Les modules utilisent une map indexée par identifiant.

```yaml
modules:
  lead-management:
    settings:
      allowCsvExport: true

  sample-form:
    enabled: true

    settings:
      mode: multi-step
      allowAttachments: false

  portfolio-advanced:
    enabled: true

  seo-local-enhanced:
    enabled: true
```

---

## 13.2 Règles générales

Chaque clé doit correspondre à un module connu.

Un module peut être :

- inclus par le pack ;
- ajouté comme option ;
- configuré ;
- explicitement désactivé uniquement si le catalogue l’autorise.

---

## 13.3 Propriétés d’un module

| Propriété | Type | Obligatoire | Description |
|---|---|---:|---|
| `enabled` | booléen | Non | Sélection explicite d’un module optionnel |
| `version` | SemVer | Non | Contrainte de version demandée |
| `settings` | objet | Non | Réglages fonctionnels validés par le module |
| `features` | objet | Non | Capacités durables configurables |

---

## 13.4 Module inclus dans le pack

Un module inclus n’a pas besoin de :

```yaml
enabled: true
```

Il peut être présent uniquement pour fournir ses réglages.

Exemple :

```yaml
modules:
  lead-management:
    settings:
      defaultStatus: new
```

---

## 13.5 Désactivation d’un module inclus

Exemple :

```yaml
modules:
  lead-management:
    enabled: false
```

Cette configuration doit être refusée si `lead-management` est obligatoire dans le pack sélectionné.

Le retrait d’un module inclus nécessite :

- un autre pack ;
- une exception commerciale validée ;
- une configuration spécifique sur mesure ;
- une décision explicite.

---

## 13.6 Module optionnel

Exemple :

```yaml
modules:
  sample-form:
    enabled: true
```

Le module doit :

- exister ;
- être disponible ;
- être compatible ;
- avoir ses dépendances résolues ;
- être validé commercialement.

---

## 13.7 Version demandée

Exemple :

```yaml
modules:
  lead-management:
    version: 1.2.0
```

Cette propriété peut représenter une version exacte ou une contrainte autorisée.

Pour le MVP, une version exacte est recommandée.

Le résultat final est enregistré dans le lock file.

---

## 13.8 `settings`

Les réglages sont validés par le schéma propre au module.

Exemple :

```yaml
modules:
  lead-management:
    settings:
      defaultStatus: new
      allowCsvExport: true
      maximumExportRows: 10000
```

Les clés inconnues sont refusées.

Un module ne doit pas utiliser un dictionnaire libre sans validation.

---

## 13.9 `features`

Exemple :

```yaml
modules:
  lead-management:
    features:
      notes: true
      csv-export: true
      permanent-delete: false
```

Cette section représente des choix fonctionnels durables.

Elle ne contient pas les kill switches opérationnels de production.

---

## 13.10 Prix interdits

Exemple interdit :

```yaml
modules:
  portfolio-advanced:
    enabled: true
    price: 600
```

---

# 14. Modules résolus automatiquement

La configuration ne doit pas répéter toutes les dépendances.

Exemple :

```yaml
modules:
  sample-form:
    enabled: true
```

Le moteur peut résoudre :

```text
contact-form
lead-management
```

Les modules ajoutés doivent apparaître dans :

- le plan ;
- le lock file ;
- le rapport.

---

# 15. Section `navigation`

## 15.1 Structure

```yaml
navigation:
  main:
    - page: home
      label: Accueil
      order: 10

    - page: services
      label: Services
      order: 20

    - page: portfolio-list
      label: Réalisations
      order: 30

    - page: contact
      label: Contact
      order: 40

  footer:
    - page: legal-notice
      label: Mentions légales
      order: 10

    - page: privacy-policy
      label: Politique de confidentialité
      order: 20
```

---

## 15.2 Propriétés d’une entrée

| Propriété | Type | Obligatoire |
|---|---|---:|
| `page` | chaîne | Oui |
| `label` | chaîne | Non |
| `order` | entier | Non |
| `feature` | chaîne | Non |
| `externalUrl` | URI | Non |

---

## 15.3 Page ou URL externe

Une entrée doit référencer :

- une page ;
- ou une URL externe ;

mais pas les deux.

---

## 15.4 URLs externes

Les URLs externes doivent :

- utiliser HTTPS sauf exception documentée ;
- être validées ;
- ne pas contenir de secret ;
- ne pas utiliser `javascript:` ;
- ne pas contenir de donnée personnelle.

---

## 15.5 Ordre

L’ordre doit être :

- entier positif ;
- unique lorsque nécessaire ;
- déterministe.

En cas d’égalité, un ordre secondaire stable doit être utilisé.

---

# 16. Section `pages`

## 16.1 Structure

```yaml
pages:
  home:
    enabled: true
    route: /

    seo:
      title: Example Project — Documentation
      description: Découvrez nos services professionnels.

    sections:
      - id: home-hero
        type: hero
        variant: split
        contentRef: content/pages/home-hero.yml

      - id: home-services
        type: service-grid
        variant: cards
        contentRef: content/pages/home-services.yml

      - id: home-portfolio
        type: portfolio-preview
        variant: grid

        enabledWhen:
          modulePresent: portfolio-basic

      - id: home-cta
        type: call-to-action
        variant: primary
        contentRef: content/pages/home-cta.yml
```

---

## 16.2 Clé de page

La clé doit correspondre à un type de page connu ou à une page custom explicitement autorisée.

Exemples :

```text
home
services
about
portfolio-list
contact
legal-notice
privacy-policy
```

---

## 16.3 Propriétés d’une page

| Propriété | Type | Obligatoire |
|---|---|---:|
| `enabled` | booléen | Non |
| `route` | chaîne | Non |
| `navigation` | objet | Non |
| `seo` | objet | Non |
| `sections` | liste | Non |
| `contentRef` | chemin relatif | Non |

---

## 16.4 `enabled`

Valeur par défaut :

- définie par le template ;
- non systématiquement `true`.

Une page obligatoire ne peut pas être désactivée.

Exemple :

```yaml
pages:
  contact:
    enabled: false
```

doit être refusé si la page Contact est obligatoire pour le projet.

---

## 16.5 `route`

Contraintes :

- commence par `/` ;
- unique ;
- aucune query string ;
- aucun fragment ;
- aucun domaine ;
- aucun chemin réservé ;
- aucun `..`.

Exemples valides :

```text
/
/services
/a-propos
/realisations
```

---

## 16.6 Routes réservées

La liste initiale comprend :

```text
/admin
/api
/health
/login
/logout
/error
```

Une page publique ne peut pas réutiliser ces routes.

---

# 17. Sections d’une page

## 17.1 Structure

```yaml
- id: home-hero
  type: hero
  variant: split
  contentRef: content/pages/home-hero.yml

  settings:
    mediaPosition: right

  enabledWhen:
    modulePresent: public-site
```

---

## 17.2 Propriétés

| Propriété | Type | Obligatoire |
|---|---|---:|
| `id` | chaîne | Oui |
| `type` | chaîne | Oui |
| `variant` | chaîne | Oui lorsque plusieurs variantes existent |
| `contentRef` | chemin relatif | Non |
| `content` | objet | Non |
| `settings` | objet | Non |
| `enabledWhen` | objet | Non |

---

## 17.3 Identifiant d’instance

L’identifiant doit être unique dans le projet ou au minimum dans la page selon la convention retenue.

Format recommandé :

```text
{page}-{role}
```

Exemples :

```text
home-hero
home-services
home-main-cta
contact-form
```

---

## 17.4 Type de section

Le type doit exister dans le catalogue des pages et sections.

Exemple inconnu :

```yaml
type: super-modern-hero
```

Résultat :

```text
erreur de validation
```

---

## 17.5 Variante

La variante doit appartenir au type de section.

Exemple invalide :

```yaml
type: faq
variant: split-hero
```

---

## 17.6 `contentRef` et `content`

Une section peut utiliser :

- `contentRef` ;
- ou `content` ;

mais pas les deux, sauf contrat explicite.

Le MVP recommande `contentRef` pour les contenus volumineux.

---

## 17.7 `settings`

Les settings sont validés par le schéma de la section.

Exemple :

```yaml
settings:
  columns: 3
  showIcons: true
```

Les propriétés CSS arbitraires sont interdites.

---

# 18. Conditions `enabledWhen`

## 18.1 Rôle

Les conditions permettent une composition déclarative limitée.

Exemples :

```yaml
enabledWhen:
  modulePresent: portfolio-basic
```

```yaml
enabledWhen:
  contentCollectionNotEmpty: portfolio
```

---

## 18.2 Conditions initiales autorisées

```text
modulePresent
featureEnabled
contentCollectionNotEmpty
packIs
all
any
not
```

---

## 18.3 Exemple composé

```yaml
enabledWhen:
  all:
    - modulePresent: portfolio-basic
    - contentCollectionNotEmpty: portfolio
```

---

## 18.4 Interdictions

Les conditions ne doivent pas contenir :

- expression C# ;
- JavaScript ;
- SQL ;
- commande ;
- accès au système ;
- comparaison avec le nom réel d’un client.

Exemple interdit :

```yaml
enabledWhen:
  expression: client.name == "Cabinet Alpha"
```

---

# 19. Section `content`

## 19.1 Structure

```yaml
content:
  rootDirectory: content
  defaultLocale: fr-FR
  seedMode: create-if-empty

  sources:
    site: content/site.yml
    services: content/services/index.yml
    faq: content/faq.yml
```

---

## 19.2 Propriétés

| Propriété | Type | Obligatoire |
|---|---|---:|
| `rootDirectory` | chemin relatif | Non |
| `defaultLocale` | locale | Non |
| `seedMode` | enum | Non |
| `sources` | map de chemins | Non |

---

## 19.3 `rootDirectory`

Valeur par défaut :

```text
content
```

Le chemin :

- doit être relatif au repository ;
- ne doit pas contenir `..` ;
- ne doit pas pointer hors du projet ;
- ne doit pas être une URL.

---

## 19.4 `seedMode`

Valeurs initiales :

```text
create-if-empty
always-for-development
manual
```

### `create-if-empty`

Crée les contenus initiaux uniquement si la cible ne contient pas déjà de données correspondantes.

### `always-for-development`

Autorisé uniquement pour les environnements de développement et projets d’exemple.

### `manual`

Le projet génère les fichiers ou commandes nécessaires, mais n’insère pas automatiquement les contenus.

---

## 19.5 Contenus interdits

Les sources ne doivent pas contenir :

- secret ;
- mot de passe ;
- données de lead ;
- document confidentiel ;
- faux témoignage ;
- faux chiffre ;
- vraie donnée client non validée ;
- contenu juridique présenté comme validé automatiquement.

---

# 20. Contenu inline

Un contenu inline est acceptable pour des valeurs courtes.

Exemple :

```yaml
sections:
  - id: home-alert
    type: alert-banner
    variant: info

    content:
      message: Fermeture exceptionnelle le 15 août.
```

Pour des contenus longs ou fortement éditoriaux, utiliser un fichier séparé.

---

# 21. Section `seo`

## 21.1 Structure

```yaml
seo:
  siteName: Example Project
  titlePattern: "{pageTitle} — {siteName}"
  defaultDescription: Services professionnels pour les entreprises.
  defaultRobots: index,follow
  generateSitemap: true
  generateRobots: true

  structuredData:
    organization: true
    localBusiness: false
```

---

## 21.2 Propriétés initiales

| Propriété | Type | Obligatoire |
|---|---|---:|
| `siteName` | chaîne | Non |
| `titlePattern` | chaîne | Non |
| `defaultDescription` | chaîne | Non |
| `defaultRobots` | enum | Non |
| `generateSitemap` | booléen | Non |
| `generateRobots` | booléen | Non |
| `structuredData` | objet | Non |

---

## 21.3 Valeurs par défaut

```text
generateSitemap = true
generateRobots = true
```

Les valeurs par défaut doivent venir du module SEO et du template.

---

## 21.4 `titlePattern`

Variables autorisées initialement :

```text
{pageTitle}
{siteName}
```

Une variable inconnue doit être refusée.

---

## 21.5 Données structurées

Une donnée structurée ne doit être activée que si les données nécessaires sont disponibles et vérifiées.

Exemple :

```yaml
localBusiness: true
```

nécessite notamment des coordonnées et une activité compatibles.

---

## 21.6 Promesses interdites

La configuration SEO ne doit pas contenir une instruction telle que :

```yaml
guaranteedRanking: 1
```

SiteFactory ne garantit pas de positionnement.

---

# 22. SEO par page

Exemple :

```yaml
pages:
  services:
    seo:
      title: Nos services
      description: Découvrez nos services pour les PME.
      canonicalPath: /services
      robots: index,follow
```

Contraintes :

- title non vide ;
- description dans une longueur raisonnable ;
- canonical local ou URL validée ;
- directive robots connue ;
- aucun secret ou donnée personnelle.

---

# 23. Section `identity`

## 23.1 Structure

```yaml
identity:
  publicRegistration: false

  roles:
    Administrator:
      includeAllInstalledModulePermissions: true

    ContentManager:
      permissions:
        - Content.Read
        - Content.Write
        - Content.Publish
        - Portfolio.Read
        - Portfolio.Manage

    SalesManager:
      permissions:
        - Leads.Read
        - Leads.Update
        - Leads.AddNote
        - Leads.Export
```

---

## 23.2 Propriétés

| Propriété | Type | Obligatoire |
|---|---|---:|
| `publicRegistration` | booléen | Non |
| `roles` | map | Non |

---

## 23.3 Inscription publique

Valeur obligatoire dans le MVP :

```yaml
publicRegistration: false
```

Une valeur `true` doit être refusée tant qu’un module et une spécification dédiés ne sont pas disponibles.

---

## 23.4 Rôles

Les identifiants de rôles utilisent un format stable tel que :

```text
Administrator
ContentManager
SalesManager
Viewer
```

---

## 23.5 Permissions

Chaque permission doit :

- être connue ;
- appartenir à un module installé ;
- être compatible avec les entitlements ;
- être autorisée pour le rôle.

Une permission inconnue est refusée.

---

## 23.6 Administrator

```yaml
Administrator:
  includeAllInstalledModulePermissions: true
```

Cette option accorde les permissions des modules réellement installés.

Elle n’ajoute pas les permissions de modules absents.

---

## 23.7 Utilisateurs et mots de passe interdits

Le fichier ne doit pas contenir :

```yaml
users:
  - email: admin@client.fr
    password: Secret123!
```

La création des comptes relève du seed contrôlé ou de l’administration.

---

# 24. Section `dataRetention`

## 24.1 Structure

```yaml
dataRetention:
  leads:
    nonCustomerRetentionDays: 365

  exports:
    temporaryFileRetentionHours: 24

  audit:
    retentionDays: 730
```

---

## 24.2 Rôle

Cette section contient uniquement les durées fonctionnelles validées pour le projet.

Elle ne doit pas définir une politique juridiquement non validée par défaut.

---

## 24.3 Contraintes

Les valeurs doivent être :

- entières ;
- positives ;
- dans une plage raisonnable ;
- compatibles avec le module ;
- documentées.

---

## 24.4 Absence de valeur

En l’absence d’une valeur explicite :

- le module applique sa valeur par défaut documentée ;
- ou exige une décision ;
- mais ne conserve pas les données indéfiniment par accident.

---

## 24.5 Purge

La présence d’une durée ne déclenche pas automatiquement une suppression au moment de la génération.

Elle configure le comportement du module ou prépare la procédure de purge.

---

# 25. Section `operations`

## 25.1 Structure

```yaml
operations:
  health:
    path: /health

  backups:
    enabled: true
    retentionDays: 30
    includeAppData: true
    includeDataProtectionKeys: true

  checks:
    diskWarningPercent: 80
    diskCriticalPercent: 90
```

---

## 25.2 Principes

Cette section contient des paramètres opérationnels non secrets et reproductibles.

Elle ne contient pas :

- chemin de backup externe avec credentials ;
- mot de passe ;
- token ;
- adresse privée sensible ;
- commande shell personnalisée.

---

## 25.3 `health`

Le chemin par défaut est :

```text
/health
```

Il doit :

- être une route locale ;
- ne pas entrer en conflit ;
- ne pas exposer de détails sensibles.

---

## 25.4 `backups`

Propriétés initiales :

| Propriété | Type |
|---|---|
| `enabled` | booléen |
| `retentionDays` | entier |
| `includeAppData` | booléen |
| `includeDataProtectionKeys` | booléen |

Le projet ne doit pas désactiver les sauvegardes sans réattribuer explicitement la responsabilité opérationnelle.

---

## 25.5 Espace disque

Valeurs valides :

```text
diskWarningPercent : 1 à 99
diskCriticalPercent : supérieur au warning
```

---

# 26. Section `deployment`

## 26.1 Structure

```yaml
deployment:
  profile: docker-compose-vps
  environmentName: Production
  applicationPort: 8080
  reverseProxy: caddy

  database:
    provider: postgresql
    exposePublicPort: false

  volumes:
    appData: true
    dataProtectionKeys: true
    postgresData: true
```

---

## 26.2 Propriétés obligatoires

| Propriété | Type | Obligatoire |
|---|---|---:|
| `profile` | chaîne | Oui |
| `environmentName` | chaîne | Non |
| `applicationPort` | entier | Non |
| `reverseProxy` | chaîne | Non |
| `database` | objet | Non |
| `volumes` | objet | Non |

---

## 26.3 Profil MVP

```text
docker-compose-vps
```

Une autre valeur doit être refusée tant que le profil correspondant n’est pas implémenté.

---

## 26.4 Port applicatif

Valeur par défaut :

```text
8080
```

Le port décrit le port interne du conteneur applicatif.

Il ne doit pas être confondu avec le port public HTTPS.

---

## 26.5 PostgreSQL

Configuration attendue :

```yaml
database:
  provider: postgresql
  exposePublicPort: false
```

Dans le MVP :

```text
exposePublicPort doit rester false
```

Une valeur `true` doit produire une erreur de sécurité ou exiger une dérogation formelle.

---

## 26.6 Reverse proxy

Valeur MVP :

```text
caddy
```

---

## 26.7 Domaine

Le domaine public est défini dans :

```text
client.domain
```

Il ne doit pas être dupliqué dans la section deployment.

---

## 26.8 Secrets de déploiement

Les secrets nécessaires sont dérivés des modules et du profil.

Exemples :

```text
ConnectionStrings__MainDatabase
POSTGRES_PASSWORD
Smtp__Password
AdminSeed__Password
```

Leurs valeurs restent hors du fichier.

---

# 27. Section `generation`

## 27.1 Structure

```yaml
generation:
  initializeGit: true
  createInitialCommit: true
  solutionFormat: slnx
  rootNamespace: EntrepriseDemoB2B
```

---

## 27.2 Propriétés initiales

| Propriété | Type | Obligatoire |
|---|---|---:|
| `initializeGit` | booléen | Non |
| `createInitialCommit` | booléen | Non |
| `solutionFormat` | enum | Non |
| `rootNamespace` | chaîne | Non |

---

## 27.3 Valeurs par défaut

```text
initializeGit = true
createInitialCommit = true
solutionFormat = slnx
```

Le commit initial n’est créé qu’après :

- restore ;
- build ;
- tests ;
- contrôle des secrets.

---

## 27.4 `rootNamespace`

Si absent, le namespace est dérivé de `client.slug`.

Exemple :

```text
sample-project
→ EntrepriseDemoB2B
```

Un namespace explicite est recommandé lorsque la dérivation est ambiguë.

Contraintes :

- identifiant C# valide ;
- pas d’espace ;
- pas de caractère spécial ;
- pas de mot clé réservé non échappé ;
- longueur raisonnable.

---

## 27.5 Options interdites dans le fichier

Les options de sécurité ou d’exécution ponctuelle restent des arguments CLI.

Exemples interdits :

```yaml
generation:
  force: true
  skipTests: true
  ignoreSecretScan: true
```

---

# 28. Valeurs par défaut

## 28.1 Principe

Une valeur par défaut est acceptable lorsqu’elle est :

- sûre ;
- stable ;
- documentée ;
- sans conséquence commerciale cachée ;
- sans effet destructif ;
- compatible avec le template.

---

## 28.2 Valeurs par défaut initiales

| Propriété | Valeur |
|---|---|
| `client.locale` | `fr-FR` |
| `client.timeZone` | `Europe/Paris` |
| `identity.publicRegistration` | `false` |
| `content.rootDirectory` | `content` |
| `content.defaultLocale` | `fr-FR` |
| `content.seedMode` | `create-if-empty` |
| `operations.health.path` | `/health` |
| `operations.backups.enabled` | `true` |
| `operations.backups.retentionDays` | `30` |
| `deployment.environmentName` | `Production` |
| `deployment.applicationPort` | `8080` |
| `deployment.reverseProxy` | `caddy` |
| `deployment.database.provider` | `postgresql` |
| `deployment.database.exposePublicPort` | `false` |
| `generation.initializeGit` | `true` |
| `generation.createInitialCommit` | `true` |
| `generation.solutionFormat` | `slnx` |

---

## 28.3 Valeurs sans défaut automatique

Les propriétés suivantes doivent rester explicites :

```text
catalogVersion
client.name
client.slug
client.domain
client.sector
offer.pack
template.id
template.version
theme.id
theme.version
deployment.profile
```

---

# 29. Normalisation

## 29.1 Principe

La configuration résolue peut normaliser certaines valeurs.

Exemples :

- suppression des espaces inutiles ;
- normalisation de la casse des identifiants ;
- déduplication de listes ;
- tri stable ;
- normalisation des routes ;
- validation des codes de locale.

---

## 29.2 Valeurs originales

La normalisation ne doit pas modifier silencieusement une valeur métier ambiguë.

Exemple :

```yaml
slug: Cabinet Alpha
```

doit produire une erreur plutôt que devenir automatiquement :

```text
cabinet-alpha
```

Une commande d’assistance peut proposer une correction, mais ne doit pas l’imposer.

---

## 29.3 Configuration résolue

Le moteur peut produire une représentation normalisée interne :

```text
ResolvedClientConfiguration
```

Cette représentation est utilisée pour créer le plan.

---

# 30. Validation en plusieurs niveaux

## Niveau 1 — Lecture

- fichier présent ;
- fichier lisible ;
- taille valide ;
- encodage valide.

## Niveau 2 — YAML

- syntaxe ;
- indentation ;
- types ;
- doublons ;
- clés.

## Niveau 3 — Schéma

- propriétés obligatoires ;
- propriétés inconnues ;
- formats ;
- longueurs ;
- plages.

## Niveau 4 — Catalogue

- pack ;
- service ;
- modules ;
- features ;
- permissions ;
- versions.

## Niveau 5 — Compatibilité

- template et pack ;
- thème et template ;
- modules et pack ;
- modules entre eux ;
- profil de déploiement.

## Niveau 6 — Sémantique

- page obligatoire ;
- route unique ;
- section compatible ;
- rôle cohérent ;
- rétention valide ;
- module inclus non désactivé.

## Niveau 7 — Sécurité

- absence de secret ;
- PostgreSQL non public ;
- aucune inscription publique ;
- aucun chemin dangereux ;
- aucune URL dangereuse.

## Niveau 8 — Production readiness

- domaine non `.test` ;
- aucun placeholder ;
- pages légales ;
- backups ;
- health check ;
- AdminSeed désactivé par défaut ;
- données de démonstration absentes.

---

# 31. Profils de validation

## 31.1 `development`

Autorise notamment :

- domaines `.test` ;
- contenus de démonstration ;
- placeholders signalés ;
- médias temporaires ;
- données seed de développement.

---

## 31.2 `staging`

Exige :

- build et tests ;
- secrets fournis par l’environnement ;
- routes protégées ;
- contenu suffisamment complet ;
- configuration proche de la production.

---

## 31.3 `production`

Refuse notamment :

- domaine `.test` ;
- placeholders bloquants ;
- contenu de démonstration ;
- inscription publique ;
- PostgreSQL exposé ;
- backup désactivé sans dérogation ;
- secret dans les fichiers ;
- AdminSeed activé par défaut ;
- page légale manquante.

Commande conceptuelle :

```bash
sitefactory validate \
  --config client.sitefactory.yml \
  --profile production
```

---

# 32. Clés inconnues

## 32.1 Politique

La politique par défaut est :

```text
additionalProperties = false
```

dans le schéma machine.

---

## 32.2 Exception

Les seules propriétés extensibles sont celles explicitement définies comme telles.

Exemple :

```text
modules.{module-id}.settings
```

Mais ces settings doivent être validés par le schéma du module.

---

## 32.3 Message

Exemple :

```text
SF-CONFIG-014

La propriété "allowCsvExports" est inconnue.

Propriété proche :
allowCsvExport
```

---

# 33. Références de fichiers

## 33.1 Chemins relatifs

Les références doivent être relatives à la configuration ou à la racine définie.

Exemple :

```yaml
contentRef: content/pages/home-hero.yml
```

---

## 33.2 Chemins interdits

```text
../
chemin absolu non prévu
partage réseau arbitraire
URL distante
lien symbolique dangereux
```

---

## 33.3 Extension

Les extensions autorisées dépendent du type de contenu.

Exemples :

```text
.yml
.yaml
.md
.json
```

---

## 33.4 Fichier absent

Un fichier référencé et obligatoire doit produire une erreur.

Un fichier optionnel peut produire un warning selon le contrat.

---

# 34. Références distantes

Le MVP interdit dans la configuration :

```yaml
contentRef: https://example.com/content.yml
```

ou :

```yaml
template:
  source: https://unknown.example/template.zip
```

Tous les catalogues, templates et contenus doivent provenir de sources locales ou de packages approuvés.

---

# 35. Variables et interpolation

## 35.1 Interpolation limitée

Le fichier ne doit pas devenir un langage de template générique.

Une interpolation limitée peut être autorisée dans les contenus ou métadonnées.

Exemples :

```text
{siteName}
{pageTitle}
```

---

## 35.2 Variables d’environnement

Le YAML ne doit pas injecter directement une variable d’environnement dans une valeur fonctionnelle sans contrat.

Exemple à éviter :

```yaml
client:
  name: ${CLIENT_NAME}
```

La configuration client doit rester versionnée et reproductible.

---

## 35.3 Secrets

Les références de secrets sont dérivées des modules.

Le fichier n’a pas besoin de contenir :

```yaml
smtpPassword: ${SMTP_PASSWORD}
```

---

# 36. Configuration par environnement

## 36.1 Baseline MVP

Le MVP utilise une configuration client fonctionnelle unique.

Il ne définit pas :

```text
client.sitefactory.Development.yml
client.sitefactory.Production.yml
```

pour modifier le pack, les modules ou les pages.

---

## 36.2 Différences d’environnement

Les différences concernent principalement :

- secrets ;
- URLs techniques ;
- logs ;
- kill switches ;
- connexions ;
- diagnostics.

Elles sont gérées par la configuration runtime.

---

## 36.3 Interdiction de dérive commerciale

La production ne doit pas disposer de modules ou entitlements différents de la configuration et du lock file sans nouveau cycle de génération ou d’upgrade.

---

# 37. Relation avec le lock file

## 37.1 Configuration

Le fichier YAML exprime :

```text
ce qui est demandé
```

## 37.2 Lock file

Le fichier JSON exprime :

```text
ce qui a été résolu et installé
```

---

## 37.3 Exemple

Configuration :

```yaml
offer:
  pack: sample-application

modules:
  sample-form:
    enabled: true
```

Lock file :

```json
{
  "pack": {
    "id": "sample-application",
    "version": "1.0.0"
  },
  "modules": {
    "contact-form": "1.0.0",
    "lead-management": "1.0.0",
    "sample-form": "1.0.0"
  }
}
```

---

## 37.4 Modification manuelle

Le lock file ne doit pas être modifié manuellement pour activer un module.

La configuration doit être modifiée puis résolue à nouveau.

---

# 38. Empreinte de configuration

Le pipeline peut produire une empreinte SHA-256 de la configuration normalisée.

Exemple conceptuel :

```text
configurationHash
```

Cette empreinte peut être enregistrée dans :

- le lock file ;
- le rapport.

Elle permet de détecter :

- modification depuis la dernière génération ;
- incohérence entre configuration et lock file ;
- génération obsolète.

L’empreinte ne doit pas inclure de secret puisque la configuration n’en contient pas.

---

# 39. Versionnement du schéma

## 39.1 Modification compatible

L’ajout d’une propriété optionnelle avec une valeur par défaut sûre peut rester compatible avec :

```text
schemaVersion: 1
```

à condition que les anciens fichiers restent valides.

---

## 39.2 Modification incompatible

Une nouvelle version de schéma est requise en cas de :

- renommage ;
- suppression ;
- changement de type ;
- changement de structure ;
- nouvelle propriété obligatoire sans défaut ;
- changement de signification ;
- nouvelle règle rendant les anciens fichiers invalides.

---

## 39.3 Migration

Une commande future peut proposer :

```bash
sitefactory config migrate \
  --config client.sitefactory.yml \
  --target-schema 2
```

Elle doit :

- produire un plan ;
- écrire un nouveau fichier ;
- conserver l’original ;
- signaler les décisions manuelles ;
- ne pas masquer une perte.

---

# 40. Dépréciation de propriétés

Une propriété dépréciée doit préciser :

- version de dépréciation ;
- remplacement ;
- version de retrait prévue ;
- règle de migration.

Exemple de diagnostic :

```text
La propriété "theme.primaryColor" est dépréciée.

Utilisez :
theme.overrides.colors.brandPrimary
```

Une propriété dépréciée peut produire un warning avant son retrait.

---

# 41. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-CONFIG-001` | Fichier absent |
| `SF-CONFIG-002` | YAML invalide |
| `SF-CONFIG-003` | Propriété obligatoire absente |
| `SF-CONFIG-004` | Propriété inconnue |
| `SF-CONFIG-005` | Type invalide |
| `SF-CONFIG-006` | Format invalide |
| `SF-CONFIG-007` | Version de schéma inconnue |
| `SF-CONFIG-008` | Version de catalogue incompatible |
| `SF-CONFIG-009` | Identifiant inconnu |
| `SF-CONFIG-010` | Valeur interdite |
| `SF-CONFIG-011` | Référence de fichier invalide |
| `SF-CONFIG-012` | Route dupliquée |
| `SF-CONFIG-013` | Secret potentiel détecté |
| `SF-CONFIG-014` | Propriété module inconnue |
| `SF-CONFIG-015` | Permission inconnue |
| `SF-CONFIG-016` | Module inclus désactivé |
| `SF-CONFIG-017` | Profil de déploiement inconnu |
| `SF-CONFIG-018` | Configuration de production non sûre |
| `SF-CONFIG-019` | Page obligatoire désactivée |
| `SF-CONFIG-020` | Condition non autorisée |

---

# 42. Messages d’erreur

Les messages doivent être actionnables.

Exemple :

```text
SF-CONFIG-016

Le module "lead-management" est inclus dans le pack
"sample-application" et ne peut pas être désactivé.

Solutions :
- retirez "enabled: false" ;
- ou sélectionnez un pack compatible avec le périmètre souhaité.
```

Le message ne doit pas afficher :

- secret ;
- mot de passe ;
- contenu complet du fichier ;
- donnée personnelle inutile.

---

# 43. Exemple minimal valide

```yaml
schemaVersion: 1
catalogVersion: 1.0.0

client:
  name: Example Project
  slug: sample-project
  domain: example.test
  sector: sample-application
  locale: fr-FR
  timeZone: Europe/Paris

offer:
  pack: sample-application
  carePlan: sample-support

template:
  id: sample-application
  version: 1.0.0

theme:
  id: professional-clean
  version: 1.0.0

deployment:
  profile: docker-compose-vps
```

Le catalogue et le template fournissent les valeurs par défaut restantes.

---

# 44. Exemple application métier cible enrichi

```yaml
schemaVersion: 1
catalogVersion: 1.0.0

client:
  name: Example Project
  legalName: Example Project Ltd
  slug: sample-project
  domain: example.test
  sector: sample-application
  locale: fr-FR
  timeZone: Europe/Paris

offer:
  pack: sample-application
  carePlan: sample-support
  commercialReference: DEV-2026-0042

template:
  id: sample-application
  version: 1.0.0

theme:
  id: professional-clean
  version: 1.0.0

  overrides:
    colors:
      brandPrimary: "#123A63"
      brandSecondary: "#D97706"

    typography:
      headingFamily: Manrope
      bodyFamily: Inter

    radius:
      preset: soft

modules:
  lead-management:
    settings:
      defaultStatus: new
      allowCsvExport: true
      maximumExportRows: 10000

    features:
      notes: true
      csv-export: true
      permanent-delete: false

  sample-form:
    enabled: true

    settings:
      mode: multi-step
      allowAttachments: false

  portfolio-advanced:
    enabled: true

  seo-local-enhanced:
    enabled: true

navigation:
  main:
    - page: home
      label: Accueil
      order: 10

    - page: services
      label: Services
      order: 20

    - page: portfolio-list
      label: Réalisations
      order: 30

    - page: about
      label: À propos
      order: 40

    - page: contact
      label: Contact
      order: 50

  footer:
    - page: legal-notice
      label: Mentions légales
      order: 10

    - page: privacy-policy
      label: Politique de confidentialité
      order: 20

pages:
  home:
    enabled: true
    route: /

    seo:
      title: Example Project — Documentation
      description: Découvrez nos services professionnels.

    sections:
      - id: home-hero
        type: hero
        variant: split
        contentRef: content/pages/home-hero.yml

      - id: home-trust
        type: trust-strip
        variant: compact
        contentRef: content/pages/home-trust.yml

      - id: home-services
        type: service-grid
        variant: cards
        contentRef: content/pages/home-services.yml

      - id: home-process
        type: process-steps
        variant: numbered
        contentRef: content/pages/home-process.yml

      - id: home-portfolio
        type: portfolio-preview
        variant: grid

        enabledWhen:
          all:
            - modulePresent: portfolio-basic
            - contentCollectionNotEmpty: portfolio

      - id: home-faq
        type: faq
        variant: accordion
        contentRef: content/pages/home-faq.yml

      - id: home-cta
        type: call-to-action
        variant: primary
        contentRef: content/pages/home-cta.yml

  services:
    enabled: true
    route: /services
    contentRef: content/services/index.yml

  portfolio-list:
    enabled: true
    route: /realisations

  about:
    enabled: true
    route: /a-propos
    contentRef: content/about.yml

  contact:
    enabled: true
    route: /contact

  legal-notice:
    enabled: true
    route: /mentions-legales
    contentRef: content/legal/legal-notice.yml

  privacy-policy:
    enabled: true
    route: /politique-de-confidentialite
    contentRef: content/legal/privacy-policy.yml

content:
  rootDirectory: content
  defaultLocale: fr-FR
  seedMode: create-if-empty

  sources:
    site: content/site.yml
    services: content/services/index.yml
    faq: content/pages/home-faq.yml
    portfolio: content/portfolio/index.yml

seo:
  siteName: Example Project
  titlePattern: "{pageTitle} — {siteName}"
  defaultDescription: Services professionnels pour les PME.
  defaultRobots: index,follow
  generateSitemap: true
  generateRobots: true

  structuredData:
    organization: true
    localBusiness: false

identity:
  publicRegistration: false

  roles:
    Administrator:
      includeAllInstalledModulePermissions: true

    ContentManager:
      permissions:
        - Content.Read
        - Content.Write
        - Content.Publish
        - Portfolio.Read
        - Portfolio.Manage

    SalesManager:
      permissions:
        - Leads.Read
        - Leads.Update
        - Leads.AddNote
        - Leads.Export

dataRetention:
  leads:
    nonCustomerRetentionDays: 365

  exports:
    temporaryFileRetentionHours: 24

operations:
  health:
    path: /health

  backups:
    enabled: true
    retentionDays: 30
    includeAppData: true
    includeDataProtectionKeys: true

  checks:
    diskWarningPercent: 80
    diskCriticalPercent: 90

deployment:
  profile: docker-compose-vps
  environmentName: Production
  applicationPort: 8080
  reverseProxy: caddy

  database:
    provider: postgresql
    exposePublicPort: false

  volumes:
    appData: true
    dataProtectionKeys: true
    postgresData: true

generation:
  initializeGit: true
  createInitialCommit: true
  solutionFormat: slnx
  rootNamespace: EntrepriseDemoB2B
```

---

# 45. Exemple officiel des options commerciales

La configuration correspondant à l’exemple commercial de référence est :

```yaml
schemaVersion: 1
catalogVersion: 1.0.0

client:
  name: Example Project
  slug: sample-project
  domain: example.test
  sector: sample-application

offer:
  pack: sample-application
  carePlan: sample-support

template:
  id: sample-application
  version: 1.0.0

theme:
  id: professional-clean
  version: 1.0.0

modules:
  sample-form:
    enabled: true

  portfolio-advanced:
    enabled: true

  seo-local-enhanced:
    enabled: true

deployment:
  profile: docker-compose-vps
```

Le fichier ne contient pas les montants.

Le catalogue permet de retrouver l’estimation de référence :

```text
9 100 € HT
249 € HT/mois
```

---

# 46. Exemples invalides

## 46.1 Prix dans la configuration

```yaml
offer:
  pack: sample-application
  price: 6400
```

Erreur :

```text
Propriété "price" interdite.
```

---

## 46.2 Secret

```yaml
modules:
  email-notifications:
    settings:
      smtpPassword: <EXTERNAL_SECRET>
```

Erreur bloquante :

```text
Secret potentiel détecté.
```

---

## 46.3 Module inconnu

```yaml
modules:
  super-ai-marketing:
    enabled: true
```

Erreur :

```text
Module inconnu dans le catalogue 1.0.0.
```

---

## 46.4 Désactivation d’un module inclus

```yaml
offer:
  pack: sample-application

modules:
  lead-management:
    enabled: false
```

Erreur :

```text
lead-management est requis par sample-application.
```

---

## 46.5 PostgreSQL public

```yaml
deployment:
  profile: docker-compose-vps

  database:
    provider: postgresql
    exposePublicPort: true
```

Erreur de sécurité pour le profil MVP.

---

## 46.6 Inscription publique

```yaml
identity:
  publicRegistration: true
```

Erreur :

```text
L’inscription publique n’est pas supportée dans le MVP.
```

---

## 46.7 Path traversal

```yaml
content:
  rootDirectory: ../../private
```

Erreur :

```text
Le chemin sort du repository autorisé.
```

---

## 46.8 Condition par client

```yaml
enabledWhen:
  clientNameEquals: Cabinet Alpha
```

Erreur :

```text
Condition non autorisée.
```

---

# 47. Schéma JSON machine

## 47.1 Emplacement

```text
schemas/client-configuration.schema.json
```

---

## 47.2 Exigences

Le schéma doit notamment définir :

- `required` ;
- `type` ;
- `enum` ;
- `pattern` ;
- `minimum` ;
- `maximum` ;
- `minLength` ;
- `maxLength` ;
- `additionalProperties: false` ;
- définitions réutilisables ;
- descriptions ;
- exemples lorsque pertinents.

---

## 47.3 Validation complémentaire

Le JSON Schema ne suffit pas pour valider :

- l’existence d’un module ;
- les dépendances ;
- les compatibilités ;
- l’unicité globale des routes ;
- les permissions des modules installés ;
- le contraste ;
- la présence de secrets ;
- la cohérence avec le catalogue.

Ces contrôles appartiennent aux validateurs sémantiques SiteFactory.

---

# 48. Tests obligatoires

## 48.1 Tests de parsing

Vérifier :

- YAML minimal valide ;
- YAML complet valide ;
- indentation invalide ;
- type invalide ;
- clé dupliquée ;
- fichier vide ;
- fichier trop volumineux.

---

## 48.2 Tests du schéma

Vérifier :

- propriété obligatoire absente ;
- propriété inconnue ;
- slug invalide ;
- domaine invalide ;
- version invalide ;
- route invalide ;
- couleur invalide ;
- permission inconnue ;
- profil inconnu.

---

## 48.3 Tests du catalogue

Vérifier :

- pack inconnu ;
- service inconnu ;
- module inconnu ;
- module planifié ;
- dépendance ;
- incompatibilité ;
- module inclus désactivé ;
- version incompatible.

---

## 48.4 Tests des pages

Vérifier :

- page obligatoire présente ;
- page obligatoire désactivée ;
- route dupliquée ;
- section inconnue ;
- variante inconnue ;
- module requis absent ;
- condition invalide ;
- contenu référencé absent.

---

## 48.5 Tests du thème

Vérifier :

- thème inconnu ;
- version incompatible ;
- token inconnu ;
- couleur invalide ;
- contraste invalide ;
- police inconnue ;
- preset inconnu.

---

## 48.6 Tests Identity

Vérifier :

- inscription publique refusée ;
- rôle connu ;
- permission connue ;
- permission de module absent refusée ;
- Administrator limité aux modules installés ;
- aucun utilisateur ou mot de passe dans le YAML.

---

## 48.7 Tests de sécurité

Vérifier :

- mot de passe ;
- token ;
- clé privée ;
- chaîne de connexion ;
- path traversal ;
- URL dangereuse ;
- PostgreSQL public ;
- commande ou script arbitraire ;
- condition par client.

---

## 48.8 Tests des profils

Vérifier :

- développement avec domaine `.test` ;
- staging ;
- production avec `.test` refusé ;
- production avec placeholder refusé ;
- production sans pages légales refusée ;
- production avec backup désactivé signalée ou refusée.

---

## 48.9 Tests de reproductibilité

Deux résolutions identiques doivent produire :

- la même configuration normalisée ;
- la même empreinte ;
- le même plan fonctionnel ;
- le même ensemble de modules.

---

# 49. Règles pour Codex

## 49.1 Lecture préalable

Avant toute modification du schéma, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
Contrat public du scenario : identifiants, versions, capacites, dependances et oracles synthetiques explicites
13-generation-pipeline.md
Contrat public des pages et sections du module concerne : identifiants stables, schemas, validation et strategies de fichiers explicites
Contrat public de rendu du module concerne : composants et styles versionnes, encodage, accessibilite et securite du contenu
../02-architecture/core-module-boundary.md
09-module-architecture.md
10-feature-flags-and-entitlements.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
13-generation-pipeline.md
14-client-configuration-schema.md
AGENTS.md
```

Ainsi que :

- le schéma JSON ;
- les exemples ;
- les catalogues ;
- les manifests ;
- les tests de validation.

---

## 49.2 Ce que Codex peut faire

Codex peut :

- implémenter une propriété validée ;
- créer le schéma JSON ;
- créer les modèles C# ;
- ajouter un validateur ;
- ajouter un message d’erreur ;
- ajouter un exemple ;
- créer des tests ;
- corriger une incohérence ;
- préparer une migration de schéma.

---

## 49.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- ajouter une propriété sans besoin validé ;
- accepter les clés inconnues globalement ;
- ajouter un prix ;
- ajouter un secret ;
- modifier un identifiant stable ;
- permettre une inscription publique ;
- permettre PostgreSQL public ;
- ajouter une expression exécutable ;
- ajouter une interpolation libre ;
- autoriser une URL distante arbitraire ;
- ajouter une option permettant d’ignorer les tests ;
- activer une fonctionnalité payante ;
- changer une valeur par défaut sensible sans validation ;
- migrer silencieusement une configuration ;
- modifier le schéma sans adapter les tests et exemples.

---

## 49.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouvelle propriété de premier niveau ;
- nouveau format de version ;
- propriété obligatoire supplémentaire ;
- changement incompatible ;
- nouvelle méthode de secret ;
- nouveau profil de déploiement ;
- nouvel entitlement ;
- nouvelle condition ;
- nouvelle méthode d’identité ;
- autorisation d’une référence distante ;
- changement du modèle de contenus ;
- changement du comportement des valeurs inconnues.

---

## 49.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Propriétés ajoutées
Propriétés modifiées
Propriétés dépréciées
Valeurs par défaut modifiées
Schéma JSON modifié
Modèles C# modifiés
Validateurs modifiés
Compatibilités affectées
Exemples modifiés
Tests ajoutés
Tests de sécurité
Impact sur les configurations existantes
Migration de schéma nécessaire
Build
Tests
Décisions restant humaines
```

---

# 50. Anti-patterns interdits

## 50.1 Dictionnaire libre global

```yaml
custom:
  anything: anything
```

sans contrat ni validation.

---

## 50.2 Prix dans la configuration

```yaml
packPrice: 6400
```

---

## 50.3 Secrets versionnés

```yaml
databasePassword: secret
```

---

## 50.4 Version flottante

```yaml
template:
  version: latest
```

---

## 50.5 Logique exécutable

```yaml
condition: clientRevenue > 100000
```

---

## 50.6 Chemin hors repository

```yaml
contentRef: ../../confidential/document.yml
```

---

## 50.7 Option technique destructrice

```yaml
generation:
  overwriteEverything: true
```

---

## 50.8 Configuration par client dans le socle

```yaml
specialClientMode: cabinet-alpha
```

---

## 50.9 Duplication des dépendances

Forcer l’utilisateur à déclarer manuellement toutes les dépendances transitives.

---

## 50.10 Duplication du lock file

Enregistrer dans le YAML toutes les versions résolues alors qu’elles appartiennent au lock file.

---

# 51. Critères d’acceptation du MVP

Le schéma sera considéré comme opérationnel lorsque :

1. `schemaVersion` est requis ;
2. `catalogVersion` est requis ;
3. les propriétés de premier niveau sont définies ;
4. les propriétés inconnues sont refusées ;
5. le client peut être identifié sans secret ;
6. le pack est validé par le catalogue ;
7. le service mensuel est validé ;
8. le template est validé ;
9. le thème est validé ;
10. les modules optionnels peuvent être sélectionnés ;
11. les modules inclus ne peuvent pas être retirés sans règle ;
12. les settings des modules sont validés ;
13. les features durables peuvent être configurées ;
14. les pages peuvent être composées ;
15. les sections peuvent être configurées ;
16. les références de contenu sont sécurisées ;
17. les routes sont uniques ;
18. les permissions sont validées ;
19. l’inscription publique est interdite ;
20. les règles de rétention peuvent être configurées ;
21. le profil `docker-compose-vps` est supporté ;
22. PostgreSQL public est refusé ;
23. aucun secret n’est accepté ;
24. aucune commande arbitraire n’est acceptée ;
25. la configuration peut être normalisée ;
26. une empreinte peut être calculée ;
27. le lock file peut être produit ;
28. les profils development, staging et production sont validables ;
29. les exemples passent les tests ;
30. le build Release réussit sans warning injustifié.

---

# 52. Décisions validées

La baseline du schéma est la suivante :

```text
Le fichier principal est client.sitefactory.yml.

Le format humain est YAML.

Le schéma machine est un JSON Schema versionné.

La version du schéma est un entier explicite.

La version du catalogue est obligatoire et utilise SemVer.

Les propriétés inconnues sont refusées.

La configuration ne contient aucun prix.

La configuration ne contient aucun secret.

La configuration ne contient aucune commande exécutable.

Le client est identifié par un nom, un slug, un domaine et un secteur.

Le pack, le template et le thème sont explicites.

Les versions du template et du thème sont exactes.

Les modules sont déclarés par identifiant stable.

Les dépendances transitives sont résolues automatiquement.

Les réglages d’un module sont validés par son propre schéma.

Les pages et sections utilisent les catalogues SiteFactory.

Les contenus longs sont référencés par des fichiers relatifs.

Les chemins ne peuvent pas sortir du repository.

Les rôles et permissions ne concernent que les modules installés.

L’inscription publique est interdite dans le MVP.

Le profil de déploiement initial est docker-compose-vps.

PostgreSQL n’est jamais exposé publiquement dans le profil MVP.

Les différences de secrets et de runtime ne sont pas stockées
dans la configuration fonctionnelle.

Le lock file décrit les versions réellement résolues.

Les options ponctuelles telles que --force ou --skip-tests
ne sont pas stockées dans la configuration.

Codex ne peut pas modifier seul un contrat structurant du schéma.
```

---

# 53. Checklist de revue d’une configuration

## Structure

```text
[ ] schemaVersion connu
[ ] catalogVersion connue
[ ] Aucune propriété inconnue
[ ] Types corrects
[ ] Formats corrects
```

## Client

```text
[ ] Nom présent
[ ] Slug valide
[ ] Domaine valide
[ ] Secteur connu
[ ] Locale valide
[ ] Fuseau valide
```

## Offre

```text
[ ] Pack connu
[ ] Service mensuel connu ou null
[ ] Aucun prix
[ ] Référence commerciale non sensible
```

## Template et thème

```text
[ ] Template connu
[ ] Version exacte
[ ] Thème connu
[ ] Version exacte
[ ] Overrides valides
[ ] Contrastes valides
```

## Modules

```text
[ ] Modules connus
[ ] Statuts compatibles
[ ] Dépendances résolues
[ ] Aucun module inclus désactivé
[ ] Settings valides
[ ] Features valides
[ ] Aucun prix
```

## Pages et contenus

```text
[ ] Pages connues
[ ] Routes uniques
[ ] Sections connues
[ ] Variantes valides
[ ] Conditions valides
[ ] Fichiers référencés présents
[ ] Aucun path traversal
[ ] Aucun contenu de démonstration en production
```

## Identity

```text
[ ] Inscription publique désactivée
[ ] Rôles cohérents
[ ] Permissions connues
[ ] Aucun utilisateur
[ ] Aucun mot de passe
```

## Opérations

```text
[ ] Health check configuré
[ ] Backups configurés
[ ] Rétention cohérente
[ ] Seuils disque cohérents
```

## Déploiement

```text
[ ] Profil supporté
[ ] PostgreSQL utilisé
[ ] Port public PostgreSQL désactivé
[ ] Volumes persistants prévus
[ ] Aucun secret
```

## Production

```text
[ ] Domaine réel
[ ] Aucun placeholder
[ ] Pages légales présentes
[ ] Aucun contenu de démonstration
[ ] AdminSeed non activé dans le projet généré
[ ] Backup et restore prévus
```

---

# 54. Conclusion

Le fichier `client.sitefactory.yml` constitue le contrat déclaratif principal entre le besoin validé et le moteur de génération.

Il doit rester :

- lisible par un humain ;
- strict ;
- versionné ;
- sans secret ;
- sans prix ;
- sans logique exécutable ;
- indépendant de l’environnement ;
- suffisamment précis pour produire un plan reproductible.

> La configuration client décrit ce que le projet doit devenir. Le catalogue explique ce qui est disponible. Le lock file enregistre ce qui a réellement été résolu et installé.
