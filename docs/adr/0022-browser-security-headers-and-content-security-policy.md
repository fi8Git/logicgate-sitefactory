---
adrId: ADR-0022
title: Enforce browser security headers and route-specific Content Security Policies
status: accepted
date: 2026-07-29
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
  - ./0029-http-api-contracts-problem-details-and-idempotent-commands.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/09-module-architecture.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ../03-generation/13-generation-pipeline.md
  - ../03-generation/14-client-configuration-schema.md
  - ../04-quality-security/18-security-baseline.md
  - ../04-quality-security/19-testing-strategy.md
  - ../04-quality-security/20-quality-gates-and-ci.md
  - ../05-operations/21-observability-and-logging.md
  - ../05-operations/22-deployment-and-environments.md
  - ../05-operations/24-operational-runbooks.md
  - ../05-operations/25-upgrade-and-maintenance-strategy.md
  - ../05-operations/26-release-and-versioning.md
  - ../05-operations/27-client-handover-and-service-closure.md
  - ../06-codex/28-codex-operating-guide.md
  - ./0001-one-repository-and-database-per-client.md
  - ./0005-file-generation-strategies.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0008-private-package-feed-and-supply-chain.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0015-deterministic-generation-and-optional-ai.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0018-public-form-abuse-protection-and-rate-limiting.md
  - ./0019-audit-trail-and-sensitive-administrative-actions.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
  - ./0021-administrator-provisioning-invitations-and-account-recovery.md
targetVersion: 0.1.0
---

# ADR-0022 — En-têtes de sécurité navigateur et Content Security Policy

## Portée publique et état d'implémentation

Le statut `accepted` et la date ci-dessous décrivent la décision architecturale
historique, pas une preuve de déploiement des en-têtes. La distribution publique
peut générer un site documentaire statique ; cela ne démontre pas l'application
des profils CSP, de Caddy, d'Identity, de Blazor ou des tests navigateur décrits ici.
Les profils et exemples de routes restent l'architecture prescrite aux applications
et hôtes qui implémenteront ces capacités, sans les annoncer comme livrées.

La portée normative de la baseline, les restrictions, les alternatives et les
preuves attendues sont conservées. La politique de rendu du Markdown non fiable
du module Documentation ne remplace ni la CSP ni les contrôles de l'hôte HTTP.
Les responsabilités d'exploitation ne créent aucune dépendance du build public
vers une distribution privée.

## 1. Statut

```text
accepted
```

La décision impose une baseline d’en-têtes de sécurité navigateur à toutes les applications générées ; elle ne constate pas leur déploiement actuel.

La baseline comprend notamment :

```text
Content-Security-Policy

Strict-Transport-Security

X-Content-Type-Options

Referrer-Policy

Permissions-Policy

Cross-Origin-Opener-Policy

Cross-Origin-Resource-Policy

X-Frame-Options

X-Permitted-Cross-Domain-Policies

Cache-Control adapté à la sensibilité
```

Les politiques sont :

- générées depuis des profils versionnés ;
- appliquées par réponse HTTP ;
- différenciées selon les zones fonctionnelles ;
- testées avec le navigateur réel ;
- déployées progressivement lorsque leur impact n’est pas encore connu ;
- strictes par défaut ;
- extensibles uniquement par allowlist explicite.

Les zones suivantes utilisent des profils distincts :

```text
pages publiques

administration

authentification

invitations et récupération

médias publics

médias privés

endpoints techniques

pages d’erreur
```

---

## 2. Résumé

Le modèle de référence est :

```text
Caddy
→ HTTPS
→ HSTS
→ suppression ou neutralisation des headers serveur inutiles
→ headers statiques applicables à toutes les réponses

Application ASP.NET Core
→ sélection du profil selon la route
→ génération éventuelle d’un nonce
→ Content Security Policy
→ politique de cache
→ Referrer-Policy
→ Cross-Origin policies
→ Permissions-Policy
→ réponse
```

Les profils initiaux sont :

```text
browser.public-standard.v1

browser.public-approved-embed.v1

browser.admin-strict.v1

browser.authentication-strict.v1

browser.private-resource.v1

browser.error-minimal.v1
```

Les principes essentiels sont :

```text
aucun `unsafe-eval`

aucun `unsafe-inline` dans script-src

aucun wildcard de domaine général

aucun script tiers sur les pages d’authentification

aucune iframe par défaut

aucun plugin objet

aucun formulaire envoyé vers un domaine arbitraire

aucune ressource HTTP depuis une page HTTPS

aucune politique définie uniquement par une balise meta

aucune CSP utilisée comme remplacement de l’encodage et de la validation
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
La CSP est envoyée par header HTTP.

Une page d’authentification ne charge aucun script tiers.

Une page de récupération ne transmet aucun referrer.

Une page d’administration ne peut pas être encadrée
dans une iframe par défaut.

Un script inline autorisé possède un nonce ou hash approuvé.

Un event handler HTML inline est interdit.

`unsafe-eval` est interdit.

Un nouveau domaine tiers nécessite une décision explicite.

Un wildcard tel que `https:` ne constitue pas une allowlist acceptable.

Un nonce est généré avec une source cryptographique sûre.

Un nonce n’est pas réutilisé volontairement entre réponses dynamiques.

Une violation CSP ne crée pas automatiquement une action métier.

Un rapport CSP est considéré comme une entrée non fiable.

Les pages sensibles utilisent `Cache-Control: no-store`.

Les headers sont présents sur les réponses d’erreur.

L’application et Caddy ne produisent pas
de politiques CSP contradictoires ou dupliquées.

Une exception CSP possède un propriétaire,
une justification et une date de révision.
```

---

## 4. Contexte

Une application générée par SiteFactory exécute du contenu dans un navigateur.

Elle peut contenir :

- HTML ;
- CSS ;
- JavaScript ;
- Blazor ;
- SignalR ;
- médias ;
- formulaires ;
- administration ;
- authentification ;
- récupération de compte ;
- scripts de mesure ;
- cartes ;
- vidéos intégrées ;
- composants tiers.

Les principaux risques côté navigateur comprennent :

- cross-site scripting ;
- injection de script ;
- HTML stocké malveillant ;
- script tiers compromis ;
- dépendance CDN modifiée ;
- clickjacking ;
- fuite de referrer ;
- contenu mixte HTTP/HTTPS ;
- accès inutile à des APIs du navigateur ;
- exécution d’objets ou plugins ;
- chargement d’une ressource depuis un domaine non prévu ;
- soumission d’un formulaire vers un domaine tiers ;
- mise en cache d’une page contenant un token ;
- récupération d’informations depuis l’historique ou le cache.

Les contrôles serveur tels que :

- authentification ;
- autorisation ;
- validation ;
- antiforgery ;
- sanitization ;

restent obligatoires.

Les en-têtes navigateur constituent une couche de défense supplémentaire.

---

## 5. Problème à résoudre

Comment limiter les capacités d’un contenu injecté ou compromis tout en garantissant :

- le fonctionnement de Blazor ;
- le fonctionnement de SignalR ;
- le SEO ;
- les données structurées JSON-LD ;
- les médias ;
- l’administration ;
- les parcours d’authentification ;
- l’intégration explicite de certains fournisseurs ;
- une évolution contrôlée des politiques ?

La solution doit également éviter :

- une CSP tellement permissive qu’elle ne protège plus ;
- une CSP tellement stricte qu’elle casse l’application ;
- des exceptions dispersées ;
- des scripts inline non identifiés ;
- des headers différents entre applications clientes sans raison ;
- des pages sensibles mises en cache ;
- des rapports CSP contenant des données personnelles ;
- une dépendance à un domaine tiers pour se connecter à l’administration.

---

## 6. Portée

Cette décision couvre :

- Content Security Policy ;
- CSP Report-Only ;
- nonces ;
- hashes ;
- scripts ;
- styles ;
- images ;
- fontes ;
- connexions ;
- WebSockets ;
- iframes ;
- formulaires ;
- HSTS ;
- anti-framing ;
- MIME sniffing ;
- referrer ;
- Permissions Policy ;
- Cross-Origin policies ;
- cache des pages sensibles ;
- scripts tiers ;
- CDN ;
- Subresource Integrity ;
- pages d’authentification ;
- pages de récupération ;
- Blazor Server ;
- Blazor WebAssembly futur ;
- rapports CSP ;
- Caddy ;
- tests navigateur ;
- exceptions.

---

## 7. Hors portée

Cette décision ne définit pas :

- la configuration TLS complète ;
- la liste de chiffrement TLS ;
- un WAF obligatoire ;
- un CDN obligatoire ;
- une politique CORS complète pour toutes les APIs ;
- l’encodage et la sanitization détaillés de chaque champ ;
- la sécurité de chaque fournisseur tiers ;
- un consent management platform obligatoire ;
- un sandbox navigateur complet ;
- une isolation par sous-domaines obligatoire ;
- une politique universelle pour les extensions navigateur ;
- une garantie contre toutes les attaques XSS.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Réduction de l’impact d’une injection**
2. **Protection des parcours d’administration et d’identité**
3. **Interdiction des scripts arbitraires**
4. **Compatibilité avec Blazor**
5. **Absence de dépendance tierce sur les pages sensibles**
6. **Protection contre le clickjacking**
7. **Protection des tokens et URLs sensibles**
8. **Contrôle des intégrations tierces**
9. **Déploiement progressif et observable**
10. **Profils versionnés**
11. **Compatibilité SEO**
12. **Headers présents sur les erreurs**
13. **Faible divergence entre clients**
14. **Testabilité dans un navigateur**
15. **Possibilité de durcissement futur**
16. **Maintenabilité**

---

## 9. Contraintes

La décision doit respecter :

```text
ASP.NET Core

Blazor

Caddy

HTTPS en production

CSP par header HTTP

Nonce cryptographique

JSON-LD compatible CSP

Aucun unsafe-eval

Aucun script inline sans nonce ou hash

Aucun script tiers sur les pages Identity

Headers sur les réponses d’erreur

Profils versionnés

Exceptions allowlistées

Aucun secret dans les rapports CSP

Compatibilité Windows et Linux
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- les applications utilisent principalement des ressources self-hosted ;
- Blazor Server est le profil interactif initial probable ;
- les scripts tiers restent facultatifs ;
- un nombre limité de pages peut nécessiter une iframe ;
- les pages HTML dynamiques ne sont pas mises en cache publiquement par défaut ;
- un nonce par réponse est compatible avec le rendu serveur ;
- les applications utilisent un domaine HTTPS propre ;
- les pages d’identité peuvent fonctionner sans analytics ni widgets externes ;
- les politiques peuvent être testées en Report-Only avant enforcement.

---

# 11. Définitions

## 11.1 Content Security Policy

Politique HTTP indiquant au navigateur quelles sources et capacités sont autorisées pour une page.

---

## 11.2 Enforced CSP

Politique appliquée par le navigateur.

Header :

```text
Content-Security-Policy
```

---

## 11.3 Report-Only CSP

Politique observée sans blocage.

Header :

```text
Content-Security-Policy-Report-Only
```

---

## 11.4 Nonce

Valeur aléatoire générée pour une réponse et attachée aux éléments inline explicitement autorisés.

---

## 11.5 Hash CSP

Empreinte autorisant un contenu inline exact et immuable.

---

## 11.6 Source expression

Expression autorisant une origine, un schéma ou une catégorie de ressource.

---

## 11.7 Browser security profile

Ensemble versionné d’en-têtes et directives correspondant à une catégorie de route.

---

## 11.8 Third-party resource

Ressource chargée depuis une origine qui n’appartient pas au projet client.

---

## 11.9 Sensitive page

Page liée notamment à :

- authentification ;
- récupération ;
- administration ;
- export ;
- média privé ;
- opération critique.

---

# 12. Option A — Aucun en-tête supplémentaire

## 12.1 Description

L’application repose uniquement sur :

- HTTPS ;
- validation ;
- encodage ;
- authentification.

---

## 12.2 Avantages

- aucune incompatibilité CSP ;
- implémentation simple ;
- intégrations tierces faciles ;
- aucun besoin de nonce.

---

## 12.3 Inconvénients

- impact d’une injection plus important ;
- scripts arbitraires ;
- clickjacking ;
- plugins objets ;
- fuites de referrer ;
- cache sensible ;
- contenu mixte moins contrôlé.

---

## 12.4 Risques

- script injecté chargé depuis un domaine externe ;
- token de récupération transmis par le referrer ;
- administration affichée dans une iframe malveillante ;
- ressource interprétée avec un mauvais MIME type.

---

## 12.5 Réversibilité

```text
élevée
```

mais les vulnérabilités restent présentes jusqu’au déploiement du durcissement.

---

# 13. Option B — Headers statiques permissifs

## 13.1 Description

Toutes les pages utilisent la même politique avec notamment :

```text
script-src 'self' 'unsafe-inline' 'unsafe-eval' https:
```

---

## 13.2 Avantages

- faible risque de casse ;
- configuration simple ;
- compatibilité avec de nombreuses bibliothèques ;
- peu de profils.

---

## 13.3 Inconvénients

- protection faible ;
- scripts inline arbitraires ;
- évaluation dynamique ;
- tout domaine HTTPS potentiellement autorisé ;
- impossible de distinguer les pages sensibles.

---

## 13.4 Risques

- CSP présente uniquement pour satisfaire une checklist ;
- injection toujours exploitable ;
- fournisseur tiers compromis ;
- permissions excessives.

---

## 13.5 Réversibilité

```text
élevée
```

mais les exceptions permissives peuvent devenir difficiles à retirer.

---

# 14. Option C — CSP extrêmement stricte unique

## 14.1 Description

Toutes les pages utilisent une politique minimale telle que :

```text
default-src 'none'
```

avec très peu d’exceptions.

---

## 14.2 Avantages

- surface réduite ;
- protection forte ;
- politique facile à expliquer.

---

## 14.3 Inconvénients

- Blazor ou SignalR cassés ;
- médias non chargés ;
- JSON-LD bloqué ;
- difficulté d’intégrer une carte ou vidéo ;
- administration inutilisable ;
- dérogations urgentes.

---

## 14.4 Risques

- désactivation totale de la CSP en urgence ;
- ajout d’`unsafe-inline` pour résoudre rapidement une régression ;
- manque de tests.

---

## 14.5 Réversibilité

```text
élevée
```

---

# 15. Option D — Profils stricts, route-specific et versionnés

## 15.1 Description

Chaque catégorie de route utilise une politique adaptée, construite à partir d’une baseline stricte.

---

## 15.2 Avantages

- protection forte ;
- pages Identity très strictes ;
- compatibilité avec les besoins publics ;
- exceptions localisées ;
- tests ciblés ;
- évolution progressive ;
- intégrations tierces contrôlées ;
- meilleure observabilité.

---

## 15.3 Inconvénients

- moteur de profil ;
- nonces ;
- tests navigateur ;
- catalogues d’origines ;
- gestion des exceptions ;
- risque de divergence.

---

## 15.4 Risques

- mauvaise sélection du profil ;
- ressource oubliée ;
- policy trop longue ;
- doublon entre Caddy et l’application ;
- pages sensibles utilisant un profil public.

---

## 15.5 Réversibilité

```text
élevée
```

---

# 16. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Aucun header | CSP permissive | CSP unique stricte | Profils versionnés |
|---|---:|---:|---:|---:|---:|
| Protection XSS | 5 | 1 | 2 | 5 | 5 |
| Compatibilité | 5 | 5 | 5 | 2 | 5 |
| Protection Identity | 5 | 1 | 2 | 5 | 5 |
| Intégrations maîtrisées | 4 | 1 | 2 | 2 | 5 |
| Testabilité | 4 | 2 | 3 | 4 | 5 |
| Maintenabilité | 5 | 2 | 2 | 3 | 5 |
| Déploiement progressif | 4 | 2 | 3 | 3 | 5 |
| SEO | 4 | 5 | 5 | 2 | 5 |
| Observabilité | 4 | 2 | 3 | 4 | 5 |
| Réversibilité | 4 | 5 | 4 | 4 | 5 |

L’option D est retenue.

---

# 17. Décision

Nous décidons d’utiliser des profils versionnés d’en-têtes de sécurité, sélectionnés selon la route ou la catégorie de ressource.

La CSP de chaque profil est :

- construite par le serveur ;
- validée au démarrage ;
- attachée à la réponse ;
- testée avec les fonctionnalités réelles ;
- documentée dans le projet généré.

---

## 17.1 Baseline restrictive

Toute source non explicitement autorisée est refusée.

---

## 17.2 Exceptions

Une exception est ajoutée au profil concerné, et non à toutes les applications ou routes.

---

## 17.3 Enforcement

Une politique stable est déployée avec :

```text
Content-Security-Policy
```

---

## 17.4 Report-Only

Une évolution risquée peut d’abord utiliser :

```text
Content-Security-Policy-Report-Only
```

---

# 18. Profils initiaux

## 18.1 `browser.public-standard.v1`

Pour les pages publiques sans intégration tierce.

---

## 18.2 `browser.public-approved-embed.v1`

Pour une page publique contenant une iframe ou ressource externe explicitement approuvée.

---

## 18.3 `browser.admin-strict.v1`

Pour l’administration authentifiée.

---

## 18.4 `browser.authentication-strict.v1`

Pour :

- login ;
- MFA ;
- step-up ;
- invitation ;
- reset ;
- récupération ;
- changement d’email.

---

## 18.5 `browser.private-resource.v1`

Pour les ressources et téléchargements privés.

---

## 18.6 `browser.error-minimal.v1`

Pour les erreurs générées par l’application ou Caddy.

---

# 19. Profil CSP public standard

Exemple conceptuel :

```text
default-src 'self';

base-uri 'none';

object-src 'none';

frame-ancestors 'none';

form-action 'self';

script-src 'self' 'nonce-{nonce}';

script-src-attr 'none';

style-src 'self' 'nonce-{nonce}';

img-src 'self';

font-src 'self';

connect-src 'self' wss://{public-host};

media-src 'self';

manifest-src 'self';

worker-src 'self';

frame-src 'none';

upgrade-insecure-requests;

report-to sitefactory-csp;
```

---

## 19.1 Exemple non contractuel

La politique exacte est produite par le profil et adaptée au mode de rendu.

Elle ne doit pas être copiée sans validation de l’application générée.

---

## 19.2 Host exact

Le host WebSocket provient de la configuration publique validée.

Il n’est pas dérivé librement d’un header non fiable.

---

# 20. Profil CSP administration

Le profil administration est plus strict.

Exemple conceptuel :

```text
default-src 'self';

base-uri 'none';

object-src 'none';

frame-ancestors 'none';

form-action 'self';

script-src 'self' 'nonce-{nonce}';

script-src-attr 'none';

style-src 'self' 'nonce-{nonce}';

img-src 'self' blob:;

font-src 'self';

connect-src 'self' wss://{public-host};

media-src 'self';

manifest-src 'none';

worker-src 'self';

frame-src 'none';

upgrade-insecure-requests;

report-to sitefactory-csp;
```

---

## 20.1 Tiers

Aucun script, font, image de tracking ou iframe tiers n’est autorisé dans la baseline administration.

---

## 20.2 `blob:`

`blob:` peut être autorisé uniquement pour les aperçus locaux de médias lorsque le besoin est démontré.

---

# 21. Profil CSP authentification et récupération

Le profil Identity est le plus strict.

Exemple conceptuel :

```text
default-src 'self';

base-uri 'none';

object-src 'none';

frame-ancestors 'none';

form-action 'self';

script-src 'self' 'nonce-{nonce}';

script-src-attr 'none';

style-src 'self' 'nonce-{nonce}';

img-src 'self';

font-src 'self';

connect-src 'self';

media-src 'none';

manifest-src 'none';

worker-src 'none';

frame-src 'none';

upgrade-insecure-requests;

report-to sitefactory-csp;
```

---

## 21.1 Aucun tiers

Les pages Identity ne chargent pas :

- analytics ;
- pixels ;
- cartes ;
- vidéos ;
- chat ;
- fontes externes ;
- tag manager ;
- scripts marketing.

---

## 21.2 Challenge humain

Un challenge externe éventuellement nécessaire à un parcours Identity exige un profil spécifique et une revue distincte.

Il ne doit pas être ajouté implicitement au profil Identity global.

---

# 22. `default-src`

La valeur par défaut initiale est :

```text
'self'
```

---

## 22.1 `none`

Certaines pages minimales peuvent utiliser :

```text
default-src 'none'
```

et réautoriser chaque catégorie.

---

## 22.2 Pas de schéma large

Les expressions suivantes sont interdites dans la baseline :

```text
https:

http:

*
```

---

# 23. `base-uri`

La baseline utilise :

```text
base-uri 'none'
```

---

## 23.1 Balise base

Les applications générées ne doivent pas dépendre d’une balise `<base>` dynamique non maîtrisée.

---

## 23.2 Blazor

Lorsque le mode Blazor impose une balise `<base>`, la valeur peut devenir :

```text
base-uri 'self'
```

dans le profil correspondant.

Cette exception est explicite et testée.

---

# 24. `object-src`

La baseline utilise :

```text
object-src 'none'
```

---

## 24.1 Plugins

Les contenus de type :

- object ;
- embed ;
- applet ;

ne sont pas utilisés.

---

# 25. `frame-ancestors`

La baseline utilise :

```text
frame-ancestors 'none'
```

---

## 25.1 Objectif

Empêcher l’encadrement du site ou de l’administration par une page externe.

---

## 25.2 Partenaire

Un besoin d’intégration dans un portail partenaire exige :

- origines exactes ;
- route dédiée ;
- revue de clickjacking ;
- mécanisme d’authentification adapté ;
- nouveau profil.

---

## 25.3 Wildcard

Un wildcard d’organisation ou de sous-domaines n’est pas autorisé sans justification.

---

# 26. `X-Frame-Options`

La baseline ajoute :

```text
X-Frame-Options: DENY
```

---

## 26.1 Complément

`frame-ancestors` constitue la politique principale.

`X-Frame-Options` apporte une défense complémentaire pour certains contextes.

---

## 26.2 Duplication cohérente

Les deux mécanismes ne doivent pas se contredire.

---

# 27. `form-action`

La baseline utilise :

```text
form-action 'self'
```

---

## 27.1 Objectif

Empêcher une injection de modifier la destination d’un formulaire vers un domaine externe.

---

## 27.2 Fournisseur externe

Une soumission de formulaire directement vers un fournisseur tiers est interdite dans le profil standard.

L’application doit préférer un endpoint serveur contrôlé.

---

# 28. `script-src`

La baseline utilise :

```text
script-src 'self' 'nonce-{nonce}'
```

---

## 28.1 `unsafe-inline`

```text
'unsafe-inline'
```

est interdit pour les scripts.

---

## 28.2 `unsafe-eval`

```text
'unsafe-eval'
```

est interdit.

---

## 28.3 Inline autorisé

Un bloc inline nécessaire utilise :

- un nonce ;
- ou un hash exact pour un bloc réellement immuable.

---

## 28.4 URL externe

Une origine externe doit être exacte.

Exemple :

```text
https://scripts.example-client.test
```

et non :

```text
https:
```

---

# 29. `script-src-attr`

La baseline utilise :

```text
script-src-attr 'none'
```

---

## 29.1 Handlers inline

Les attributs suivants sont interdits :

```html
onclick=""
onload=""
onerror=""
```

---

## 29.2 Blazor

Les interactions sont attachées par le framework ou le code JavaScript versionné, et non par handlers HTML inline.

---

# 30. JSON-LD

Les données structurées SEO utilisent généralement :

```html
<script type="application/ld+json">
```

---

## 30.1 CSP

Le bloc JSON-LD reçoit :

- le nonce de la réponse ;
- ou un hash validé lorsqu’il est immuable.

---

## 30.2 Contenu

Les données JSON-LD restent :

- structurées ;
- encodées ;
- produites à partir de données validées ;
- indépendantes d’un contenu HTML libre.

---

## 30.3 Injection

Une chaîne client ne doit pas permettre de fermer la balise et d’injecter un script.

---

# 31. Nonces

Le nonce est généré avec un générateur cryptographiquement sûr.

---

## 31.1 Entropie

La valeur possède une entropie suffisante, avec une baseline d’au moins :

```text
128 bits
```

---

## 31.2 Portée

Un nonce est propre à une réponse HTML dynamique.

---

## 31.3 Réutilisation

Il n’est pas réutilisé volontairement entre plusieurs réponses.

---

## 31.4 Logs

Le nonce n’est pas ajouté aux logs, traces ou métriques.

---

## 31.5 Secret

Le nonce n’est pas considéré comme un secret durable après livraison de la page.

Il doit néanmoins rester imprévisible avant la réponse.

---

# 32. Service de contexte navigateur

L’application expose un service scoped conceptuel :

```csharp
public interface IBrowserSecurityContext
{
    string ScriptNonce { get; }

    BrowserSecurityProfileId ProfileId { get; }
}
```

---

## 32.1 Création

Le contexte est créé une fois par réponse ou requête HTML adaptée.

---

## 32.2 Razor

Les composants nécessitant un nonce l’obtiennent par le contexte serveur.

---

## 32.3 Pas de singleton

Le nonce ne doit pas être stocké dans un singleton.

---

# 33. Cache des pages contenant un nonce

Une page HTML dynamique contenant un nonce ne doit pas être mise en cache publiquement sans stratégie explicite.

---

## 33.1 Baseline

Les réponses HTML dynamiques utilisent :

```text
Cache-Control: no-cache
```

ou une politique plus stricte selon leur sensibilité.

---

## 33.2 Pages sensibles

Les pages Identity et administration utilisent :

```text
Cache-Control: no-store
```

---

## 33.3 Assets statiques

Les assets versionnés utilisent un cache long et immuable.

---

# 34. Hashes CSP

Un hash peut être utilisé pour un script ou style inline :

- petit ;
- réellement immuable ;
- généré pendant le build ;
- vérifié dans la CI.

---

## 34.1 Contenu dynamique

Un contenu dépendant du client, de la page ou de la requête utilise un nonce.

---

## 34.2 Maintenance

Toute modification du contenu exige la mise à jour du hash.

---

# 35. `strict-dynamic`

`strict-dynamic` n’est pas activé dans la baseline initiale.

---

## 35.1 Introduction future

Son ajout nécessite :

- compatibilité navigateur cible ;
- compréhension du changement de confiance ;
- tests ;
- fallback ;
- ADR ou évolution explicite du profil.

---

# 36. `style-src`

La cible de sécurité est :

```text
style-src 'self' 'nonce-{nonce}'
```

---

## 36.1 Styles inline

Les blocs `<style>` autorisés utilisent un nonce.

---

## 36.2 Attributs style

Les attributs :

```html
style=""
```

sont interdits dans la cible stricte.

---

## 36.3 Compatibilité

Certaines bibliothèques UI peuvent nécessiter des styles inline.

Toute exception doit être :

- identifiée ;
- limitée au profil concerné ;
- testée ;
- documentée ;
- accompagnée d’un plan de retrait.

---

# 37. `style-src-attr`

Lorsque le navigateur cible et la politique le permettent, la cible est :

```text
style-src-attr 'none'
```

---

## 37.1 Exception transitoire

Une exception peut autoriser temporairement :

```text
style-src-attr 'unsafe-inline'
```

sans autoriser les scripts inline.

---

## 37.2 Interdiction de banalisation

Cette exception ne doit pas devenir la valeur globale par défaut uniquement pour simplifier le développement.

---

# 38. `img-src`

La baseline utilise :

```text
img-src 'self'
```

---

## 38.1 `data:`

`data:` n’est ajouté que si une fonctionnalité identifiée l’exige.

---

## 38.2 `blob:`

`blob:` est réservé aux previews locales ou traitements explicitement prévus.

---

## 38.3 Object storage futur

Un CDN ou object storage externe utilise une origine exacte et un profil versionné.

---

## 38.4 Tracking pixels

Les pixels tiers sont interdits sur les pages Identity et administration.

---

# 39. `font-src`

La baseline utilise :

```text
font-src 'self'
```

---

## 39.1 Self-hosting

Les fontes nécessaires sont préférentiellement :

- self-hosted ;
- versionnées ;
- licenciées ;
- incluses dans les contrôles supply chain.

---

## 39.2 Fournisseur externe

Une fonte externe nécessite une exception explicite et une analyse :

- disponibilité ;
- confidentialité ;
- CSP ;
- CORS ;
- transfert.

---

# 40. `connect-src`

La baseline autorise :

```text
'self'
```

et les connexions exactes nécessaires au mode Blazor.

---

## 40.1 Blazor Server

Le profil peut autoriser :

```text
wss://{public-host}
```

pour SignalR en production.

---

## 40.2 Développement

Le profil de développement peut autoriser les endpoints localhost nécessaires.

Il n’est jamais utilisé en production.

---

## 40.3 Fournisseurs externes

Une API appelée directement depuis le navigateur nécessite :

- origine exacte ;
- justification ;
- CORS côté fournisseur ;
- protection des credentials ;
- analyse de confidentialité.

La préférence reste un appel serveur.

---

# 41. Blazor Server

Le profil Blazor Server doit tester au minimum :

- chargement du boot script ;
- négociation SignalR ;
- WebSocket ;
- fallback éventuel ;
- reconnexion ;
- navigation ;
- gestion des erreurs ;
- antiforgery ;
- JSON-LD.

---

## 41.1 Inline scripts

Les scripts de bootstrap inline éventuels utilisent un nonce.

---

## 41.2 WebSocket

Le host WebSocket doit être exact et issu de la configuration publique approuvée.

---

## 41.3 Circuit errors

La page d’erreur du circuit conserve la politique de sécurité.

---

# 42. Blazor WebAssembly futur

Un profil Blazor WebAssembly distinct pourra être nécessaire.

---

## 42.1 WebAssembly

Si le runtime cible exige une capacité CSP dédiée à WebAssembly, le profil peut autoriser :

```text
'wasm-unsafe-eval'
```

---

## 42.2 `unsafe-eval`

Cette exception ne doit jamais être remplacée par :

```text
'unsafe-eval'
```

sans nouvelle décision.

---

## 42.3 Profil distinct

Une application WASM ne doit pas réutiliser silencieusement le profil Blazor Server.

---

# 43. `worker-src`

La baseline autorise :

```text
worker-src 'self'
```

uniquement lorsque l’application utilise un worker ou service worker.

---

## 43.1 Absence

Le profil Identity peut utiliser :

```text
worker-src 'none'
```

---

## 43.2 `blob:`

Un worker créé depuis `blob:` nécessite une exception explicite.

---

# 44. `manifest-src`

Les pages publiques utilisant un manifest peuvent autoriser :

```text
manifest-src 'self'
```

---

## 44.1 Identity

Les pages Identity utilisent :

```text
manifest-src 'none'
```

lorsque le manifest n’est pas nécessaire.

---

# 45. `frame-src`

La baseline utilise :

```text
frame-src 'none'
```

---

## 45.1 Embed approuvé

Une iframe externe nécessite :

- profil distinct ;
- origine exacte ;
- sandbox ;
- Permissions Policy ;
- politique de referrer ;
- consentement éventuel ;
- test responsive.

---

# 46. Iframes tierces

Une iframe approuvée utilise l’attribut :

```html
sandbox
```

avec les capacités minimales.

---

## 46.1 Permissions

Les valeurs `allow` sont limitées aux fonctionnalités réellement nécessaires.

---

## 46.2 `allow-same-origin`

Ne pas combiner inutilement :

```text
allow-scripts
+ allow-same-origin
```

lorsque cela réduit fortement l’isolation attendue.

---

## 46.3 Navigation

Une iframe ne doit pas pouvoir naviguer librement la fenêtre principale sans besoin démontré.

---

# 47. Vidéos et cartes

Les vidéos, cartes et widgets externes utilisent :

- une activation explicite ;
- une origine exacte ;
- un profil dédié ;
- un placeholder local ;
- un chargement différé ;
- une information confidentialité lorsque nécessaire.

---

## 47.1 Pages sensibles

Ils sont interdits dans :

- login ;
- MFA ;
- récupération ;
- administration ;
- export privé.

---

# 48. `media-src`

La baseline utilise :

```text
media-src 'self'
```

pour les projets utilisant des médias audio ou vidéo locaux.

---

## 48.1 Absence

Un profil sans média peut utiliser :

```text
media-src 'none'
```

---

# 49. Contenu mixte

Les pages de production sont servies en HTTPS.

---

## 49.1 Directive

La CSP peut inclure :

```text
upgrade-insecure-requests
```

---

## 49.2 Source HTTP

Une origine explicitement en HTTP n’est jamais autorisée en production.

---

## 49.3 Validation

Les tests recherchent également les URLs absolues `http://` dans :

- HTML ;
- CSS ;
- JavaScript ;
- configuration ;
- contenus administrables.

---

# 50. HSTS

Caddy applique :

```text
Strict-Transport-Security
```

sur les réponses HTTPS de production.

---

## 50.1 Baseline stabilisée

Valeur cible :

```text
max-age=31536000
```

---

## 50.2 Déploiement progressif

Pour un nouveau domaine, la durée peut évoluer progressivement :

```text
durée courte
→ validation
→ durée intermédiaire
→ cible annuelle
```

---

## 50.3 `includeSubDomains`

Cette option n’est activée que lorsque tous les sous-domaines concernés utilisent durablement HTTPS.

---

## 50.4 `preload`

L’inscription dans une liste preload nécessite une décision explicite.

Elle n’est pas activée automatiquement par SiteFactory.

---

## 50.5 HTTP

Le header HSTS ne doit pas être considéré comme utile lorsqu’il est envoyé sur une réponse HTTP non sécurisée.

---

# 51. `X-Content-Type-Options`

La baseline utilise :

```text
X-Content-Type-Options: nosniff
```

---

## 51.1 Content-Type

Les réponses doivent également fournir un Content-Type correct.

---

## 51.2 Médias

La détection et validation des médias suivent l’ADR-0016.

---

# 52. `Referrer-Policy`

Le profil public utilise initialement :

```text
strict-origin-when-cross-origin
```

---

## 52.1 Identity

Les pages contenant ou ayant contenu un token utilisent :

```text
no-referrer
```

---

## 52.2 Administration

Le profil administration utilise :

```text
same-origin
```

ou :

```text
no-referrer
```

selon la page et la sensibilité.

---

## 52.3 Token dans l’URL

La politique de referrer ne remplace pas :

- la redirection vers une URL propre ;
- l’absence de ressources tierces ;
- la non-journalisation des query strings.

---

# 53. `Permissions-Policy`

La baseline désactive toutes les fonctionnalités navigateur non utilisées.

Exemple conceptuel :

```text
camera=()

microphone=()

geolocation=()

payment=()

usb=()

accelerometer=()

gyroscope=()

magnetometer=()

display-capture=()
```

---

## 53.1 Liste exacte

La liste émise est maintenue dans le profil et adaptée aux fonctionnalités réellement supportées.

---

## 53.2 Réactivation

Une fonctionnalité est réactivée uniquement :

- pour une route ou origine identifiée ;
- avec une justification ;
- avec des tests ;
- sans wildcard excessif.

---

## 53.3 Passkeys

Une future utilisation des passkeys pourra nécessiter une autorisation ciblée pour l’API correspondante.

---

# 54. `Cross-Origin-Opener-Policy`

Le profil administration et Identity utilise :

```text
Cross-Origin-Opener-Policy: same-origin
```

---

## 54.1 Objectif

Réduire les relations de fenêtre inattendues avec des origines externes.

---

## 54.2 Authentification externe future

Un flux d’authentification utilisant une popup peut nécessiter :

```text
same-origin-allow-popups
```

dans un profil spécifique.

---

## 54.3 Pas de dérogation globale

Une intégration future ne doit pas affaiblir tous les profils.

---

# 55. `Cross-Origin-Resource-Policy`

La baseline utilise :

```text
Cross-Origin-Resource-Policy: same-origin
```

pour les ressources privées et administratives.

---

## 55.1 Ressources publiques

Une ressource publique destinée à être chargée depuis un autre site peut utiliser un profil distinct.

---

## 55.2 CDN futur

Une migration CDN nécessite de revoir :

- CORP ;
- CORS ;
- CSP ;
- cache ;
- URLs.

---

# 56. `Cross-Origin-Embedder-Policy`

La baseline ne force pas :

```text
Cross-Origin-Embedder-Policy
```

sur toutes les pages.

---

## 56.1 Raisons

Cette isolation impose des contraintes sur toutes les ressources chargées et n’est pas nécessaire au MVP.

---

## 56.2 Introduction future

Elle nécessite :

- besoin fonctionnel ;
- revue des ressources ;
- tests de compatibilité ;
- nouveau profil.

---

# 57. `X-Permitted-Cross-Domain-Policies`

La baseline utilise :

```text
X-Permitted-Cross-Domain-Policies: none
```

---

# 58. `X-XSS-Protection`

La baseline utilise :

```text
X-XSS-Protection: 0
```

---

## 58.1 CSP

La protection repose sur :

- CSP ;
- encodage ;
- sanitization ;
- validation ;
- frameworks supportés.

---

# 59. Header `Server`

Les réponses ne doivent pas exposer inutilement :

- version ASP.NET Core ;
- version du serveur ;
- détails de plateforme.

---

## 59.1 Caddy

Caddy doit retirer ou neutraliser le header `Server` lorsque le profil le permet.

---

## 59.2 Garantie

La suppression d’un header d’identification ne constitue pas à elle seule un contrôle de sécurité.

---

# 60. Headers de cache des pages sensibles

Les pages suivantes utilisent :

```text
Cache-Control: no-store
```

- login ;
- MFA ;
- step-up ;
- invitation ;
- reset ;
- récupération ;
- changement d’email ;
- export privé ;
- audit ;
- session management.

---

## 60.1 Headers complémentaires

Selon le profil :

```text
Pragma: no-cache
Expires: 0
```

peuvent être ajoutés pour les intermédiaires anciens.

---

## 60.2 Contenu public

Les pages publiques utilisent une politique séparée.

---

# 61. Cache des assets statiques

Les assets possédant un nom versionné ou hashé peuvent utiliser :

```text
Cache-Control: public, max-age=31536000, immutable
```

---

## 61.1 HTML

Le HTML ne doit pas utiliser cette politique.

---

## 61.2 Médias administrables

Leur politique suit l’ADR-0016 et leur caractère immuable par Media ID.

---

# 62. `Clear-Site-Data`

Le header peut être utilisé dans certains parcours de sécurité.

Exemples :

- compromission ;
- révocation globale ;
- reset finalisé ;
- clôture de session exceptionnelle.

---

## 62.1 Baseline

Il n’est pas ajouté systématiquement à chaque réponse.

---

## 62.2 Effet

Il peut effacer :

- cookies ;
- cache ;
- storage ;

pour l’origine.

---

## 62.3 Décision

Son utilisation doit tenir compte des préférences publiques légitimes stockées sur la même origine.

---

# 63. Fetch Metadata

L’administration peut utiliser les headers :

```text
Sec-Fetch-Site

Sec-Fetch-Mode

Sec-Fetch-Dest
```

comme défense complémentaire.

---

## 63.1 Requêtes mutables

Une requête cross-site inhabituelle vers une action administrative peut être refusée.

---

## 63.2 Limite

Fetch Metadata ne remplace pas :

- antiforgery ;
- SameSite ;
- autorisation ;
- step-up.

---

## 63.3 Client absent

L’absence de ces headers ne doit pas casser arbitrairement les clients supportés sans stratégie explicite.

---

# 64. CORS

CORS reste distinct de la CSP.

---

## 64.1 CSP

Contrôle principalement ce que la page peut charger et exécuter.

---

## 64.2 CORS

Contrôle ce qu’un navigateur autorise à lire entre origines.

---

## 64.3 Limite

Aucun des deux ne remplace :

- authentification ;
- autorisation ;
- rate limiting ;
- validation.

---

# 65. Scripts tiers

La baseline privilégie :

```text
aucun script tiers
```

---

## 65.1 Introduction

Un script tiers nécessite :

- besoin métier ;
- propriétaire ;
- fournisseur ;
- version ;
- origine exacte ;
- politique de données ;
- CSP ;
- SRI lorsque applicable ;
- kill switch ou retrait possible ;
- tests ;
- revue de transfert client.

---

## 65.2 Tag managers

Un tag manager pouvant charger arbitrairement d’autres scripts est considéré comme une délégation de contrôle importante.

Il n’est pas ajouté à la baseline.

---

## 65.3 Pages sensibles

Les scripts tiers restent interdits sur les pages Identity et administration.

---

# 66. Self-hosting

Lorsque la licence le permet, SiteFactory privilégie le self-hosting des bibliothèques front-end.

---

## 66.1 Avantages

- CSP simplifiée ;
- disponibilité ;
- versions contrôlées ;
- réduction des connexions externes ;
- transfert facilité.

---

## 66.2 Supply chain

Le self-hosting ne supprime pas le besoin :

- de vérifier la provenance ;
- de verrouiller la version ;
- de scanner ;
- de respecter la licence.

---

# 67. CDN tiers

Un CDN externe n’est utilisé que lorsque :

- la version est exacte ;
- la ressource est immuable ;
- l’origine est allowlistée ;
- SRI est configuré lorsque applicable ;
- le fallback est compris ;
- la confidentialité est acceptable.

---

## 67.1 Domaine large

Un domaine hébergeant de nombreux contenus utilisateurs ou packages différents nécessite une analyse renforcée.

---

# 68. Subresource Integrity

Une ressource tierce statique peut utiliser :

```html
integrity="sha384-..."
crossorigin="anonymous"
```

---

## 68.1 Modification

Une modification du fichier exige un nouvel hash.

---

## 68.2 Limite

SRI ne s’applique pas à tous les types de ressources ou chargements dynamiques.

---

## 68.3 Ressources self-hosted

SRI peut également être utilisé si cela apporte une valeur démontrée, mais n’est pas obligatoire pour chaque asset local.

---

# 69. Scripts dynamiques

Une bibliothèque ne doit pas pouvoir charger des scripts depuis une origine arbitraire.

---

## 69.1 Loader

Les loaders dynamiques nécessitent :

- origine exacte ;
- justification ;
- tests ;
- policy adaptée.

---

## 69.2 URL utilisateur

Une URL de script ne provient jamais d’un contenu administrable.

---

# 70. Analytics

Les analytics publics sont facultatifs.

---

## 70.1 Activation

Ils nécessitent :

- décision client ;
- politique de confidentialité ;
- configuration ;
- CSP ;
- consentement lorsque nécessaire ;
- absence des pages sensibles.

---

## 70.2 Self-hosted analytics

Une solution self-hosted reste soumise :

- à la minimisation ;
- aux permissions ;
- à la rétention ;
- à la CSP.

---

# 71. Consentement et CSP

Le gestionnaire de consentement ne doit pas charger tous les scripts avant le consentement sous prétexte qu’ils sont bloqués fonctionnellement.

---

## 71.1 Profils dynamiques

Une politique CSP ne doit pas devenir arbitrairement différente selon du contenu utilisateur non validé.

---

## 71.2 Scripts après consentement

Les origines possibles restent déclarées dans un profil approuvé.

---

# 72. Contenus administrables

Un administrateur ne doit pas pouvoir injecter :

- `<script>` ;
- event handlers ;
- iframe arbitraire ;
- URL JavaScript ;
- balise object ;
- style actif non contrôlé.

---

## 72.1 Sanitization

La CSP complète la sanitization.

Elle ne la remplace pas.

---

## 72.2 Rich text

Le format et la sanitization suivent l’ADR-0016.

---

# 73. Liens administrables

Les liens provenant du contenu sont validés.

---

## 73.1 Schémas

Les schémas autorisés sont bornés.

Exemples :

```text
https

mailto

tel
```

selon le champ.

---

## 73.2 `javascript:`

Le schéma :

```text
javascript:
```

est toujours interdit.

---

## 73.3 `data:`

Les liens `data:` sont interdits sauf contrat très spécifique.

---

# 74. Liens externes

Une ouverture dans un nouvel onglet utilise les protections adaptées.

Exemple :

```html
rel="noopener noreferrer"
```

---

## 74.1 Automatisation

Les composants de lien externes doivent appliquer ces attributs de manière centralisée.

---

# 75. Downloads privés

Les téléchargements privés utilisent :

- autorisation serveur ;
- Cache-Control privé ou no-store ;
- Content-Disposition ;
- nosniff ;
- Referrer-Policy adaptée ;
- CORP restrictive.

---

## 75.1 CSP

La route de téléchargement n’a pas besoin d’une CSP HTML complète si elle ne retourne pas de document HTML.

Les headers pertinents restent appliqués.

---

# 76. PDFs

Un PDF uploadé est servi selon son profil.

---

## 76.1 Public

Un PDF public validé peut être rendu inline si le produit l’autorise.

---

## 76.2 Privé

Un PDF privé utilise préférentiellement :

```text
Content-Disposition: attachment
```

selon le besoin.

---

## 76.3 PDF actif

La validation d’upload reste nécessaire ; les headers navigateur ne suffisent pas à rendre un document sûr.

---

# 77. Pages d’erreur

Les réponses :

- 400 ;
- 401 ;
- 403 ;
- 404 ;
- 429 ;
- 500 ;
- 502 ;
- 503 ;

doivent recevoir les headers adaptés.

---

## 77.1 Erreur Caddy

Les pages générées directement par Caddy utilisent un profil minimal.

---

## 77.2 Erreur application

Les pages d’erreur de l’application utilisent le profil de leur zone ou un profil minimal plus strict.

---

## 77.3 Données

Aucune stack trace, nonce, chemin ou secret ne doit être affiché.

---

# 78. Répartition des responsabilités

## 78.1 Caddy

Caddy est responsable de :

- HTTPS ;
- redirection HTTP vers HTTPS ;
- HSTS en production ;
- headers statiques globaux convenus ;
- headers des erreurs générées par Caddy ;
- suppression de headers serveur inutiles.

---

## 78.2 Application

L’application est responsable de :

- sélection du profil ;
- CSP dynamique ;
- nonce ;
- Cache-Control route-specific ;
- Referrer-Policy route-specific ;
- permissions route-specific ;
- rapport CSP ;
- cohérence avec le rendu.

---

## 78.3 Contrat

La répartition doit être documentée dans le projet généré.

---

# 79. Headers dupliqués

Plusieurs headers CSP peuvent être appliqués simultanément par le navigateur et produire une politique plus restrictive que prévu.

---

## 79.1 Interdiction

Caddy et l’application ne doivent pas chacun ajouter une CSP complète aux réponses proxifiées.

---

## 79.2 Tests

Les tests vérifient :

- une seule CSP enforced ;
- au maximum une CSP Report-Only attendue ;
- aucune directive contradictoire.

---

# 80. Pages d’erreur Caddy

Caddy peut appliquer un fallback strict uniquement aux réponses qu’il génère lui-même.

---

## 80.1 Exemple conceptuel

```text
default-src 'none';

style-src 'self';

img-src 'self';

frame-ancestors 'none';

base-uri 'none';

form-action 'none';
```

---

## 80.2 Pas de script

Une page d’erreur proxy ne nécessite pas JavaScript dans la baseline.

---

# 81. Configuration des profils

Les profils sont décrits par une configuration structurelle versionnée.

Exemple conceptuel :

```yaml
browserSecurity:
  profile: browser.public-standard.v1
  approvedOrigins: []
  approvedEmbeds: []
```

---

## 81.1 Secrets

Une configuration CSP ne contient aucun secret.

---

## 81.2 Runtime

Les origines de production sont validées depuis la configuration d’environnement approuvée.

---

## 81.3 Contenu utilisateur

Un administrateur fonctionnel ne peut pas ajouter librement une origine CSP.

---

# 82. Catalogue des origines tierces

Chaque origine tierce approuvée doit posséder :

```text
OriginId

ExactOrigin

Purpose

ResourceTypes

AllowedProfiles

Owner

PrivacyReview

SecurityReview

AddedAt

ReviewAt

RemovalProcedure
```

---

## 82.1 Query ou path

La CSP autorise principalement des origines.

Une validation applicative supplémentaire reste nécessaire pour les paths ou ressources.

---

## 82.2 Sous-domaines

Les wildcards de sous-domaines sont évités.

---

# 83. Exceptions CSP

Une exception doit indiquer :

```text
ProfileId

Directive

SourceExpression

Reason

AffectedFeature

Owner

SecurityImpact

Tests

ExpirationOrReviewDate
```

---

## 83.1 Expiration

Une exception temporaire possède une date de retrait ou de révision.

---

## 83.2 `unsafe-inline`

Une demande d’ajout de `unsafe-inline` pour les scripts est refusée dans la baseline.

---

## 83.3 `unsafe-eval`

Une demande d’ajout de `unsafe-eval` nécessite un nouvel ADR ou une dérogation de sécurité exceptionnelle.

---

# 84. CSP Report-Only

Une nouvelle politique peut être déployée en Report-Only.

---

## 84.1 Objectif

Identifier :

- ressources oubliées ;
- incompatibilités ;
- scripts inline ;
- connexions ;
- extensions navigateur ;
- faux positifs.

---

## 84.2 Durée

Le mode Report-Only reste temporaire.

---

## 84.3 Enforcement existant

Une nouvelle politique Report-Only ne doit pas retirer la CSP enforced stable déjà en place.

---

## 84.4 Passage en enforcement

Le passage nécessite :

- analyse ;
- correction ;
- tests ;
- absence de violations légitimes critiques ;
- décision explicite.

---

# 85. Endpoint de rapport CSP

L’application peut exposer :

```text
POST /security/csp-report
```

---

## 85.1 Anonyme

L’endpoint peut être anonyme car le navigateur doit pouvoir soumettre une violation sans session.

---

## 85.2 Protection

Il utilise :

- limite de taille ;
- Content-Type allowlisté ;
- rate limiting ;
- concurrence limitée ;
- validation ;
- aucune action métier ;
- aucune notification par rapport individuel.

---

## 85.3 Pas d’antiforgery

Un endpoint de reporting navigateur cross-context ne repose pas nécessairement sur l’antiforgery.

Il possède ses propres limites et n’effectue aucune mutation fonctionnelle sensible.

---

# 86. Taille des rapports CSP

La taille est bornée.

Exemple initial :

```text
32 KiB
```

---

## 86.1 Payload trop grand

Le rapport est refusé avant désérialisation coûteuse.

---

## 86.2 Collections

Le nombre de rapports dans une enveloppe est également borné.

---

# 87. Données des rapports CSP

Les champs d’un rapport sont considérés comme non fiables.

---

## 87.1 URLs

Les URLs peuvent contenir :

- query strings ;
- fragments ;
- identifiants ;
- données personnelles.

---

## 87.2 Minimisation

Avant stockage ou journalisation :

- supprimer les query strings ;
- supprimer les fragments ;
- normaliser l’origine ;
- limiter la longueur ;
- retirer les valeurs non nécessaires.

---

## 87.3 Script sample

Les échantillons de script ne sont pas conservés par défaut.

---

# 88. Stockage des violations

La baseline privilégie :

- métriques agrégées ;
- événements structurés bornés ;
- échantillonnage ;
- rétention courte.

---

## 88.1 Pas de piste d’audit

Une violation CSP ordinaire n’est pas un événement de la piste d’audit.

---

## 88.2 Incident

Une violation répétée et exploitable peut devenir un incident de sécurité distinct.

---

# 89. Métriques CSP

Exemples :

```text
csp reports received

csp violations accepted

csp reports rejected

csp violations by directive

csp violations by profile

csp report rate limited
```

---

## 89.1 Labels autorisés

```text
profile_id

directive

disposition

resource_category

environment
```

---

## 89.2 Labels interdits

```text
full_url

blocked_url

document_url

source_file

user_id

session_id

nonce

script_sample
```

---

# 90. Observabilité

Événements recommandés :

```text
BrowserSecurityProfileSelected

BrowserSecurityHeaderGenerationFailed

CspReportReceived

CspViolationRecorded

CspReportRejected

CspPolicyChanged

CspExceptionActivated

CspExceptionExpired

BrowserSecurityHeaderMissing
```

---

## 90.1 Données autorisées

```text
ProfileId

ProfileVersion

Directive

ResourceCategory

Result

ErrorCode

Environment

Duration
```

---

## 90.2 Données interdites

```text
Nonce

Token

Cookie

Full blocked URL

Query string

Script sample

Form content
```

---

# 91. Alertes

Alertes possibles :

```text
Header CSP absent

Hausse des violations script-src

Violation sur une page Identity

Unsafe directive détectée

Nouveau domaine non approuvé

Rapports CSP en forte hausse

Endpoint de reporting saturé

Headers absents sur les réponses d’erreur

HSTS absent en production
```

---

## 91.1 Signal

Les extensions navigateur et outils de sécurité locaux peuvent générer des violations.

Une alerte doit être basée sur :

- fréquence ;
- profil ;
- directive ;
- environnement ;
- origine normalisée.

---

# 92. Audit

Les opérations suivantes sont auditables :

- changement de profil production ;
- ajout d’une origine tierce ;
- ajout d’une iframe ;
- ajout d’une exception ;
- activation de `unsafe-inline` pour les styles ;
- passage Report-Only vers enforced ;
- activation HSTS `includeSubDomains` ;
- demande de preload ;
- désactivation temporaire de CSP.

---

## 92.1 Metadata

L’audit conserve :

- Profile ID ;
- version ;
- directive ;
- origine ;
- raison ;
- acteur ;
- rapport de validation.

---

## 92.2 Aucun nonce

Les nonces ne sont jamais audités.

---

# 93. Développement local

Le profil développement peut autoriser :

- localhost ;
- hot reload ;
- WebSocket de développement ;
- tooling navigateur.

---

## 93.1 Séparation

Le profil de développement possède un identifiant distinct.

---

## 93.2 Production

Une application de production refuse un profil marqué :

```text
development
```

---

## 93.3 Exceptions

Les exceptions de développement ne doivent pas être copiées dans la configuration cliente de production.

---

# 94. Staging

Staging utilise la politique de production cible, éventuellement en Report-Only pour une nouvelle version.

---

## 94.1 Tiers

Staging ne doit pas appeler par erreur les fournisseurs de production sans configuration adaptée.

---

## 94.2 Validation

Le passage en production nécessite une validation staging du profil enforced.

---

# 95. Rollout progressif

Le processus recommandé est :

```text
définir le profil cible
→ ajouter en Report-Only
→ collecter
→ corriger les violations légitimes
→ exécuter les tests navigateur
→ activer enforcement en staging
→ valider
→ activer enforcement en production
→ surveiller
```

---

## 95.1 Urgence

Une correction urgente ne doit pas consister par défaut à :

```text
script-src *
```

ou :

```text
unsafe-inline
unsafe-eval
```

---

# 96. Rollback

Le rollback d’une politique consiste à revenir vers la dernière version approuvée.

---

## 96.1 Version

Les profils sont versionnés afin de permettre cette opération.

---

## 96.2 Pas de suppression totale

Désactiver complètement tous les headers n’est pas le rollback ordinaire.

---

# 97. Upgrades SiteFactory

Un upgrade peut modifier :

- profil ;
- directives ;
- middleware ;
- nonce ;
- headers Caddy ;
- tests.

---

## 97.1 Plan

L’upgrade plan signale :

- modification de CSP ;
- nouvelle origine ;
- suppression d’une directive ;
- besoin de Report-Only ;
- compatibilité Blazor ;
- impact des intégrations.

---

## 97.2 Customizations

Le code client ajoutant des scripts ou styles doit être analysé.

---

## 97.3 Aucune mutation silencieuse

Une politique de production ne change pas sans release et déploiement explicites.

---

# 98. Modules

Un module front-end doit déclarer ses besoins navigateur.

Exemple conceptuel :

```text
Scripts

Styles

Connections

Frames

Images

Fonts

Workers

BrowserFeatures
```

---

## 98.1 Composition

Le générateur combine les besoins compatibles avec le profil.

---

## 98.2 Besoin interdit

Un module exigeant `unsafe-eval` est refusé ou nécessite une décision explicite.

---

## 98.3 Origine tierce

Le module ne peut pas ajouter une origine arbitraire sans catalogue approuvé.

---

# 99. Customizations client

Une customization doit déclarer :

- ressources ;
- origines ;
- CSP ;
- cache ;
- scripts ;
- iframe ;
- Permissions Policy.

---

## 99.1 Interdictions

Elle ne doit pas :

- modifier le header directement dans plusieurs middlewares ;
- ajouter `unsafe-inline` globalement ;
- dériver une origine depuis du contenu administrable ;
- désactiver la CSP d’une route sensible ;
- charger un script tiers sur la page de login.

---

# 100. Pages générées par AI

Conformément à l’ADR-0015, l’AI ne modifie pas la politique CSP.

---

## 100.1 Contenu proposé

Une proposition AI ne peut pas ajouter :

- script ;
- iframe ;
- balise object ;
- style actif ;
- origine externe.

---

## 100.2 Validation

Tout contenu accepté suit les validateurs et composants structurés ordinaires.

---

# 101. Administration des contenus

L’administration ne doit pas offrir un champ permettant d’éditer directement :

```text
Content-Security-Policy
```

ou :

```text
script-src
```

---

## 101.1 Configuration technique

La policy appartient au repository et au déploiement.

---

# 102. SEO

Les mécanismes suivants doivent continuer à fonctionner :

- title ;
- meta description ;
- canonical ;
- robots ;
- Open Graph ;
- JSON-LD ;
- sitemap.

---

## 102.1 Social images

Les images utilisent des URLs publiques autorisées.

---

## 102.2 Scripts SEO

Le SEO ne nécessite pas l’autorisation générale de scripts tiers.

---

# 103. CSP et cache CDN futur

Un CDN mettant en cache du HTML contenant un nonce doit conserver de manière cohérente :

- le HTML ;
- le header CSP correspondant.

---

## 103.1 Baseline

Le HTML dynamique avec nonce n’est pas mis en cache dans un CDN dans le MVP.

---

## 103.2 Évolution

Une stratégie future pourra utiliser :

- rendu statique ;
- hashes ;
- génération edge ;
- cache de réponse complète cohérente.

---

# 104. Sécurité des redirects

Les pages sensibles n’utilisent que :

- URLs locales ;
- destinations allowlistées.

---

## 104.1 CSP

`form-action` et les politiques navigateur complètent la validation serveur.

---

# 105. Headers sur les redirections

Les réponses 3xx sensibles utilisent également :

- Cache-Control adapté ;
- Referrer-Policy ;
- X-Content-Type-Options ;
- autres headers statiques pertinents.

---

## 105.1 Token

Une redirection vers une URL propre ne doit pas recopier le token dans un autre paramètre.

---

# 106. Headers sur les APIs JSON

Les APIs JSON utilisent notamment :

```text
X-Content-Type-Options: nosniff
Cache-Control adapté
Cross-Origin-Resource-Policy adapté
```

---

## 106.1 CSP

Une CSP complète est principalement utile aux documents pouvant créer un contexte de navigation.

Elle ne doit pas remplacer la sécurité des APIs.

---

# 107. Headers sur les médias

Les médias utilisent :

- Content-Type validé ;
- nosniff ;
- Content-Disposition adapté ;
- Cache-Control ;
- CORP ;
- Referrer-Policy si nécessaire.

---

## 107.1 HTML uploadé

Un média uploadé ne doit pas être servi comme document HTML actif depuis l’origine principale sans décision spécifique.

---

# 108. Isolation des contenus actifs

Les contenus actifs fournis par des utilisateurs ne sont pas acceptés dans le profil standard.

---

## 108.1 Futur besoin

Un besoin d’héberger des contenus actifs tiers nécessiterait :

- origine séparée ;
- domaine dédié ;
- sandbox ;
- CSP indépendante ;
- nouveau threat model.

---

# 109. Tests unitaires

Tester notamment :

- sélection de profil ;
- composition de directives ;
- validation des origines ;
- validation des schemes ;
- génération de nonce ;
- profils Identity ;
- profils administration ;
- erreurs de configuration ;
- absence de directives interdites.

---

# 110. Tests de nonce

Vérifier :

```text
nonce présent dans le header

nonce présent sur le script autorisé

nonce différent entre deux réponses

nonce absent des logs

nonce non réutilisé par singleton

script sans nonce bloqué dans le test navigateur
```

---

# 111. Tests CSP structurels

Vérifier :

```text
default-src présent

base-uri présent

object-src none

frame-ancestors présent

form-action présent

script-src sans unsafe-inline

script-src sans unsafe-eval

script-src-attr none

origines exactes

reporting configuré selon le profil
```

---

# 112. Tests Blazor Server

Couvrir :

- premier chargement ;
- navigation ;
- SignalR ;
- WebSocket ;
- reconnexion ;
- formulaire ;
- antiforgery ;
- erreur circuit ;
- JSON-LD ;
- administration.

---

# 113. Tests Blazor WebAssembly futurs

Lorsque le profil existe, tester :

- chargement WASM ;
- runtime ;
- worker ;
- service worker ;
- absence d’`unsafe-eval` général ;
- policy dédiée.

---

# 114. Tests Identity

Vérifier les pages :

```text
login

MFA

step-up

invitation

password reset

MFA recovery

email change
```

Résultats attendus :

```text
aucun script tiers

no-store

no-referrer

frame-ancestors none

form-action self

aucun token dans le referrer
```

---

# 115. Tests d’anti-framing

Un navigateur doit refuser l’affichage dans une iframe depuis :

- une origine externe ;
- une origine same-site non autorisée ;
- une page partenaire non allowlistée.

---

# 116. Tests de ressources interdites

Tenter de charger :

- script externe ;
- script inline sans nonce ;
- event handler inline ;
- iframe externe ;
- object ;
- image externe ;
- WebSocket non autorisé.

Le navigateur doit appliquer le blocage attendu.

---

# 117. Tests des styles

Vérifier :

- feuille locale ;
- bloc style avec nonce ;
- bloc sans nonce ;
- attribut style ;
- bibliothèque UI ;
- rendu responsive ;
- pages d’erreur.

---

# 118. Tests JSON-LD

Vérifier :

- nonce ;
- JSON valide ;
- contenu client encodé ;
- absence de fermeture de balise injectée ;
- visibilité pour les outils de test SEO.

---

# 119. Tests des headers

Vérifier sur chaque profil :

```text
Content-Security-Policy

Strict-Transport-Security

X-Content-Type-Options

Referrer-Policy

Permissions-Policy

Cross-Origin-Opener-Policy

Cross-Origin-Resource-Policy

X-Frame-Options

X-Permitted-Cross-Domain-Policies

Cache-Control
```

---

# 120. Tests des erreurs

Couvrir :

- 400 ;
- 401 ;
- 403 ;
- 404 ;
- 429 ;
- 500 ;
- 502 ;
- 503.

---

## 120.1 Caddy

Tester une erreur générée sans application disponible.

---

## 120.2 Application

Tester une exception contrôlée et une route inconnue.

---

# 121. Tests des headers dupliqués

Vérifier :

- une seule CSP enforced ;
- une seule valeur HSTS attendue ;
- aucune directive contradictoire ;
- aucun header ajouté deux fois par des chemins différents.

---

# 122. Tests du reporting CSP

Tester :

- report valide ;
- Content-Type inconnu ;
- corps trop grand ;
- batch trop grand ;
- URL avec query ;
- script sample ;
- rate limiting ;
- métriques ;
- absence d’action métier.

---

# 123. Tests de cache

Vérifier :

```text
Identity → no-store

Administration sensible → no-store

HTML public dynamique → politique contrôlée

Assets hashés → immutable

Média privé → non public

Token page → no-store
```

---

# 124. Tests HSTS

Vérifier en production simulée :

- header présent sur HTTPS ;
- durée attendue ;
- includeSubDomains selon profil ;
- absence de configuration preload implicite ;
- redirection HTTP vers HTTPS.

---

# 125. Tests d’origines tierces

Pour chaque origine :

- profile autorisé ;
- type de ressource ;
- exactitude du host ;
- suppression de l’origine ;
- mode offline ;
- indisponibilité fournisseur ;
- transfert client.

---

# 126. Tests de sécurité

Vérifier notamment :

```text
script inline injecté

event handler injecté

javascript: dans un lien

iframe externe

form action externe

data URI inattendue

unsafe-eval absent

unsafe-inline script absent

wildcard absent

token de récupération non transmis par referrer

page admin non framable
```

---

# 127. Tests multiplateformes

Les tests de génération et de configuration fonctionnent sur :

```text
Windows x64

Linux x64
```

Les tests de navigateur, Caddy et HTTPS sont exécutés sur Linux dans la CI adaptée.

---

# 128. Quality gates

Une contribution est bloquée lorsque :

```text
`unsafe-eval` apparaît dans une CSP de production.

`unsafe-inline` apparaît dans script-src.

Une CSP utilise `https:` comme source générale.

Une CSP utilise `*` sans exception approuvée.

Une page Identity charge un script tiers.

Un script inline ne possède ni nonce ni hash.

Un event handler HTML inline est ajouté.

Une iframe est ajoutée sans profil approuvé.

Un header CSP est ajouté uniquement par meta tag.

Les pages sensibles n’utilisent pas no-store.

Le token peut être transmis par referrer.

Une policy ne couvre pas les pages d’erreur.

Caddy et l’application produisent des CSP contradictoires.

Les tests navigateur échouent.
```

---

# 129. Quality gates de production

La production est bloquée lorsque :

```text
HTTPS n’est pas disponible.

HSTS cible n’est pas configuré après validation du domaine.

La CSP enforced est absente.

Le profil production est inconnu.

Une exception CSP n’a pas de propriétaire.

Une origine tierce n’est pas approuvée.

Les pages Identity utilisent un profil public permissif.

Le nonce est réutilisé globalement.

Les headers sont absents sur les erreurs.

Le mode development est actif.

Les reports indiquent une violation légitime critique non résolue.
```

---

# 130. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-BROWSER-001` | Profil de sécurité navigateur inconnu |
| `SF-BROWSER-002` | Version de profil non supportée |
| `SF-BROWSER-003` | Profil interdit dans cet environnement |
| `SF-BROWSER-004` | Header obligatoire absent |
| `SF-BROWSER-005` | Header dupliqué |
| `SF-BROWSER-006` | Header contradictoire |
| `SF-BROWSER-007` | Profil Identity insuffisamment strict |
| `SF-BROWSER-008` | Profil administration insuffisamment strict |
| `SF-BROWSER-009` | Header de cache sensible absent |
| `SF-BROWSER-010` | Politique de referrer insuffisante |
| `SF-CSP-001` | CSP absente |
| `SF-CSP-002` | Directive CSP inconnue |
| `SF-CSP-003` | Directive obligatoire absente |
| `SF-CSP-004` | Source CSP invalide |
| `SF-CSP-005` | Wildcard interdit |
| `SF-CSP-006` | `unsafe-inline` script interdit |
| `SF-CSP-007` | `unsafe-eval` interdit |
| `SF-CSP-008` | Origine tierce non approuvée |
| `SF-CSP-009` | Script inline sans nonce |
| `SF-CSP-010` | Style inline non autorisé |
| `SF-CSP-011` | Event handler inline interdit |
| `SF-CSP-012` | Nonce absent |
| `SF-CSP-013` | Nonce invalide |
| `SF-CSP-014` | Nonce réutilisé |
| `SF-CSP-015` | Hash inline invalide |
| `SF-CSP-016` | Form action externe interdite |
| `SF-CSP-017` | Frame ancestor non autorisé |
| `SF-CSP-018` | Iframe externe non approuvée |
| `SF-CSP-019` | Connexion WebSocket non approuvée |
| `SF-CSP-020` | Ressource tierce interdite |
| `SF-CSP-021` | Policy Report-Only expirée |
| `SF-CSP-022` | Violation légitime non résolue |
| `SF-CSP-023` | CSP fournie uniquement par meta |
| `SF-CSP-024` | Profil Blazor incompatible |
| `SF-CSP-025` | JSON-LD sans nonce ou hash |
| `SF-CSP-REPORT-001` | Rapport CSP invalide |
| `SF-CSP-REPORT-002` | Rapport trop volumineux |
| `SF-CSP-REPORT-003` | Content-Type de rapport invalide |
| `SF-CSP-REPORT-004` | Trop de rapports |
| `SF-CSP-REPORT-005` | Endpoint rate-limited |
| `SF-CSP-REPORT-006` | URL sensible non neutralisée |
| `SF-HEADER-001` | HSTS absent |
| `SF-HEADER-002` | HSTS incohérent |
| `SF-HEADER-003` | `nosniff` absent |
| `SF-HEADER-004` | Referrer-Policy absente |
| `SF-HEADER-005` | Permissions-Policy absente |
| `SF-HEADER-006` | Anti-framing absent |
| `SF-HEADER-007` | COOP incompatible |
| `SF-HEADER-008` | CORP incompatible |
| `SF-HEADER-009` | Cache-Control sensible absent |
| `SF-HEADER-010` | Header Server exposé |
| `SF-THIRD-PARTY-001` | Origine tierce inconnue |
| `SF-THIRD-PARTY-002` | Revue de sécurité absente |
| `SF-THIRD-PARTY-003` | Revue de confidentialité absente |
| `SF-THIRD-PARTY-004` | SRI absent lorsque requis |
| `SF-THIRD-PARTY-005` | Exception expirée |

---

# 131. CI/CD

La CI doit vérifier :

- génération des profils ;
- absence de directives interdites ;
- nonces ;
- CSP Identity ;
- headers ;
- pages d’erreur ;
- reporting ;
- tests navigateur ;
- HSTS dans le profil de production ;
- origines tierces ;
- fichiers front-end ;
- scripts inline ;
- event handlers inline.

---

## 131.1 Analyse statique

La CI peut rechercher :

```text
onclick=

onload=

onerror=

javascript:

unsafe-inline

unsafe-eval

script-src *
```

---

## 131.2 Limite

Une recherche textuelle ne remplace pas les tests de rendu et navigateur.

---

# 132. Audit des dépendances front-end

Chaque ressource scriptée doit pouvoir être reliée à :

- package ;
- version ;
- source ;
- licence ;
- hash ;
- profil CSP.

---

## 132.1 Ressource inconnue

Un fichier JavaScript minifié non relié à une dépendance approuvée est refusé.

---

# 133. Sécurité

## 133.1 Actifs

- sessions ;
- tokens ;
- formulaires ;
- données ;
- administration ;
- contenu public ;
- scripts ;
- ressources tierces ;
- réputation du domaine.

---

## 133.2 Menaces

- XSS ;
- clickjacking ;
- script tiers compromis ;
- fuite de referrer ;
- contenu mixte ;
- MIME sniffing ;
- iframe malveillante ;
- formulaire redirigé ;
- accès à une API navigateur inutile ;
- cache d’une page sensible.

---

## 133.3 Contrôles

- CSP ;
- nonces ;
- encodage ;
- sanitization ;
- anti-framing ;
- HSTS ;
- no-store ;
- no-referrer ;
- SRI ;
- self-hosting ;
- Permissions Policy ;
- COOP ;
- CORP ;
- tests ;
- allowlists.

---

# 134. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Script inline injecté | Nonce et interdiction `unsafe-inline` |
| Script distant injecté | Origines exactes |
| `eval` utilisé par une dépendance | Interdiction `unsafe-eval` |
| Administration dans une iframe | `frame-ancestors 'none'` |
| Formulaire modifié vers un tiers | `form-action 'self'` |
| Token transmis par referrer | `no-referrer` et URL propre |
| Plugin actif | `object-src 'none'` |
| Fausse extension MIME | `nosniff` |
| Domaine HTTP chargé | HTTPS, HSTS et CSP |
| Fonction navigateur abusive | Permissions Policy |
| Script CDN modifié | Version pinning et SRI |
| Reporting CSP abusé | Taille, validation et rate limiting |

---

# 135. Règles pour les modules

Chaque module front-end doit déclarer :

```text
BrowserSecurityRequirements

RequiredScripts

RequiredStyles

RequiredConnections

RequiredFrames

RequiredImages

RequiredWorkers

RequiredBrowserFeatures
```

---

## 135.1 Baseline

L’absence de déclaration signifie :

```text
self only
aucune iframe
aucun tiers
aucune feature navigateur supplémentaire
```

---

## 135.2 Origine dynamique

Un module ne peut pas construire une origine CSP depuis une valeur métier libre.

---

# 136. Règles pour les customizations

Une customization peut demander une exception via le catalogue prévu.

---

## 136.1 Interdictions

Elle ne doit pas :

- éditer directement la CSP depuis le contenu ;
- ajouter un wildcard ;
- ajouter `unsafe-eval` ;
- charger un script sur les pages Identity ;
- ajouter une iframe non sandboxée ;
- désactiver `frame-ancestors`.

---

# 137. Règles pour LogicGate

LogicGate doit :

- maintenir les profils ;
- limiter les origines ;
- tester les navigateurs ;
- surveiller les violations ;
- revoir les exceptions ;
- maintenir HSTS ;
- contrôler les scripts tiers ;
- vérifier les pages Identity ;
- préparer le handover.

---

## 137.1 LogicGate ne doit pas

- désactiver la CSP pour corriger rapidement une intégration ;
- autoriser tout HTTPS ;
- ajouter un tag manager sans revue ;
- utiliser une fonte distante sur le login ;
- ignorer les headers des pages d’erreur ;
- activer preload HSTS sans vérifier les sous-domaines.

---

# 138. Règles pour Codex

Codex peut :

- implémenter les profils ;
- générer le middleware ;
- ajouter les nonces ;
- adapter les composants ;
- ajouter les tests ;
- détecter les scripts inline ;
- documenter les exceptions.

---

## 138.1 Codex ne doit pas

```text
Ajouter unsafe-eval

Ajouter unsafe-inline dans script-src

Ajouter un wildcard global

Désactiver la CSP

Ajouter un script tiers sans décision

Modifier HSTS de production

Activer includeSubDomains ou preload

Ajouter un iframe sans sandbox

Charger analytics sur une page Identity

Journaliser un nonce ou rapport complet

Modifier un profil production sans tâche explicite
```

---

## 138.2 Décision humaine

Une décision humaine est requise pour :

- nouveau domaine tiers ;
- script marketing ;
- iframe ;
- challenge Identity externe ;
- exception de style inline ;
- profil WASM ;
- HSTS includeSubDomains ;
- HSTS preload ;
- désactivation temporaire d’un contrôle ;
- ajout de COEP.

---

# 139. Anti-patterns interdits

## 139.1 CSP décorative

```text
default-src *;
script-src * 'unsafe-inline' 'unsafe-eval';
```

---

## 139.2 Meta CSP uniquement

```html
<meta http-equiv="Content-Security-Policy" ...>
```

utilisée comme mécanisme principal.

---

## 139.3 Nonce global

Le même nonce est utilisé pendant toute la durée du processus.

---

## 139.4 Script inline sans nonce

Un composant ajoute un script directement dans le markup.

---

## 139.5 Handler inline

```html
<button onclick="submitForm()">
```

---

## 139.6 Domaine dérivé du contenu

Un administrateur ajoute une URL dans un champ et celle-ci entre dans `script-src`.

---

## 139.7 Analytics sur récupération

La page contenant un token charge un fournisseur analytics.

---

## 139.8 Page sensible cachée publiquement

Le login ou l’export utilise un cache public.

---

## 139.9 CSP uniquement sur les réponses 200

Les erreurs ne reçoivent aucun header de sécurité.

---

## 139.10 CSP dupliquée

Caddy et ASP.NET Core ajoutent chacun une politique différente.

---

## 139.11 `unsafe-eval` pour une bibliothèque

Une dépendance incompatible est conservée sans revue uniquement pour éviter son remplacement.

---

## 139.12 Wildcard d’iframe

```text
frame-src https:;
```

---

# 140. Conséquences positives

La décision apporte :

- réduction de l’impact des injections ;
- contrôle des scripts ;
- protection des pages Identity ;
- anti-clickjacking ;
- réduction des fuites de referrer ;
- protection du cache ;
- maîtrise des tiers ;
- meilleure détection des régressions ;
- profils adaptés à Blazor ;
- capacité de rollout progressif ;
- meilleure transférabilité ;
- baseline uniforme entre projets.

---

# 141. Conséquences négatives

La décision entraîne :

- gestion des nonces ;
- profils supplémentaires ;
- tests navigateur ;
- incompatibilités avec certaines bibliothèques ;
- effort de migration du code inline ;
- catalogue des origines ;
- reporting ;
- gestion des exceptions ;
- coordination Caddy/application ;
- complexité pour les embeds.

---

# 142. Risques acceptés

## 142.1 CSP imparfaite

Une erreur de configuration peut laisser une capacité excessive.

Réduction :

- profils ;
- tests ;
- review ;
- scanners ;
- reporting.

---

## 142.2 Faux positifs

Des extensions navigateur peuvent générer des rapports.

Réduction :

- agrégation ;
- filtrage ;
- analyse par profil.

---

## 142.3 Incompatibilité de bibliothèque

Une dépendance peut nécessiter des styles inline.

Réduction :

- remplacement ;
- exception limitée ;
- plan de retrait.

---

## 142.4 Absence de COEP

La baseline ne bénéficie pas de l’isolation complète correspondante.

Ce risque est accepté car les fonctionnalités concernées ne sont pas nécessaires au MVP.

---

# 143. Mesures compensatoires

- encodage ;
- sanitization ;
- CSP stricte ;
- nonces ;
- SRI ;
- self-hosting ;
- HSTS ;
- no-store ;
- no-referrer ;
- antiforgery ;
- permissions ;
- tests navigateur ;
- reporting ;
- audit ;
- catalogue d’exceptions ;
- quality gates.

---

# 144. Performance

## 144.1 Nonce

Le coût de génération est négligeable par rapport au rendu.

---

## 144.2 Headers

La taille de la CSP doit rester maîtrisée.

---

## 144.3 Origines

Une liste excessive d’origines augmente :

- taille ;
- complexité ;
- risque ;
- maintenance.

---

## 144.4 Reporting

Les rapports sont rate-limited et agrégés afin de ne pas saturer le service.

---

# 145. Coûts

## 145.1 Coûts supplémentaires

- développement ;
- tests ;
- maintenance ;
- revue des tiers ;
- observabilité ;
- adaptation des composants.

---

## 145.2 Coûts évités

- exploitation XSS ;
- fuite de token ;
- script tiers compromis ;
- clickjacking ;
- incident de cache ;
- dépendances incontrôlées ;
- correction urgente en production.

---

# 146. Maintenabilité

Pour chaque page ou module, il doit être possible de répondre :

```text
Quel profil utilise-t-il ?

Quels scripts charge-t-il ?

Quels styles inline nécessite-t-il ?

Quelles connexions réseau ouvre-t-il ?

Quelles iframes utilise-t-il ?

Quelles APIs navigateur demande-t-il ?

Pourquoi chaque origine est-elle autorisée ?

Comment la fonctionnalité est-elle testée ?

Comment l’exception peut-elle être retirée ?
```

---

# 147. Compatibilité

## 147.1 Version des profils

Chaque profil possède une version.

---

## 147.2 Navigateurs

La matrice de support navigateur appartient à la documentation qualité.

---

## 147.3 Anciennes directives

Une directive non supportée par un navigateur ne doit pas être l’unique contrôle protégeant une action sensible.

---

## 147.4 Blazor

Les profils sont testés contre les versions .NET et Blazor supportées.

---

## 147.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets.

L’introduction d’une CSP stricte dans un projet existant peut révéler des incompatibilités et doit suivre un plan Report-Only.

---

# 148. Migration depuis une application sans CSP

Le processus est :

```text
inventorier les ressources

identifier les scripts inline

identifier les styles inline

identifier les connexions

identifier les tiers

créer le profil

déployer Report-Only

corriger

tester

activer enforcement

surveiller
```

---

# 149. Migration depuis `unsafe-inline`

Le processus est :

```text
identifier les blocs inline
→ déplacer le code vers des fichiers
→ ajouter des nonces aux rares blocs nécessaires
→ retirer les handlers inline
→ activer script-src-attr none
→ tester
→ retirer unsafe-inline
```

---

# 150. Migration depuis `unsafe-eval`

Le processus est :

```text
identifier la dépendance

identifier la fonctionnalité

mettre à jour ou remplacer

tester

retirer unsafe-eval

ajouter un quality gate
```

---

## 150.1 WebAssembly

Une nécessité WebAssembly utilise une directive dédiée dans un profil distinct, et non `unsafe-eval` général.

---

# 151. Migration des scripts tiers

Le processus est :

```text
inventorier

justifier

self-hoster lorsque possible

verrouiller la version

ajouter SRI

limiter l’origine

exclure les pages sensibles

documenter le transfert

tester la panne fournisseur
```

---

# 152. Transfert client

Le handover doit inclure :

- profils ;
- versions ;
- origines tierces ;
- exceptions ;
- HSTS ;
- CSP reporting ;
- scripts ;
- SRI ;
- Caddy ;
- tests ;
- fournisseurs ;
- procédures de modification.

---

## 152.1 Comptes tiers

Les comptes ou configurations appartenant à LogicGate ne doivent pas rester une dépendance implicite après transfert.

---

# 153. Clôture de service

La clôture doit vérifier :

- scripts LogicGate retirés ;
- analytics transférés ou supprimés ;
- reporting CSP transféré ;
- origines obsolètes retirées ;
- compte fournisseur révoqué ;
- profil final documenté ;
- tests exécutés.

---

# 154. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de modifier les headers d’un site réel ;
- d’ajouter un domaine tiers ;
- d’activer HSTS preload ;
- de désactiver une CSP ;
- de modifier Caddy ;
- d’ajouter un script marketing ;
- de déployer une exception.

---

# 155. Réversibilité

## 155.1 Classification

```text
élevée pour les profils CSP

moyenne pour les intégrations tierces

faible pour HSTS preload
```

---

## 155.2 Rollback CSP

Une version précédente peut être restaurée.

---

## 155.3 HSTS

Une durée longue ou un preload ne disparaît pas immédiatement des navigateurs.

Cette décision exige donc une progression prudente.

---

# 156. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `BrowserSecurityProfileId` ;
- directives ;
- origines ;
- exceptions ;
- erreurs.

---

## Étape 2 — Catalogue

Créer les profils initiaux :

```text
public-standard

public-approved-embed

admin-strict

authentication-strict

private-resource

error-minimal
```

---

## Étape 3 — Nonce

Ajouter :

- générateur ;
- contexte scoped ;
- intégration Razor ;
- JSON-LD ;
- tests.

---

## Étape 4 — Middleware

Ajouter :

- sélection de profil ;
- génération des headers ;
- validation ;
- pages d’erreur application.

---

## Étape 5 — Caddy

Configurer :

- HSTS ;
- headers globaux ;
- erreurs Caddy ;
- suppression des doublons.

---

## Étape 6 — Refactor front-end

Retirer :

- scripts inline ;
- event handlers ;
- styles non maîtrisés ;
- ressources non approuvées.

---

## Étape 7 — Identity

Appliquer le profil strict à :

- login ;
- MFA ;
- step-up ;
- invitations ;
- récupération.

---

## Étape 8 — Reporting

Créer :

- endpoint ;
- validation ;
- rate limiting ;
- métriques ;
- rétention.

---

## Étape 9 — Tests navigateur

Ajouter :

- CSP ;
- anti-framing ;
- cache ;
- Blazor ;
- erreurs ;
- Identity.

---

## Étape 10 — Quality gates

Ajouter :

- analyse statique ;
- contrôle des profils ;
- contrôle des origines ;
- audit des exceptions.

---

# 157. Tâches liées proposées

```text
SF-TASK-273 — Define versioned browser security profiles

SF-TASK-274 — Add CSP source and directive validation

SF-TASK-275 — Add per-response CSP nonce generation

SF-TASK-276 — Add CSP nonce support to Razor and JSON-LD

SF-TASK-277 — Add route-specific browser security middleware

SF-TASK-278 — Add strict administrator and Identity CSP profiles

SF-TASK-279 — Add public-site CSP profiles

SF-TASK-280 — Add hardened Caddy response headers

SF-TASK-281 — Add secure Caddy error responses

SF-TASK-282 — Remove inline browser event handlers

SF-TASK-283 — Add CSP reporting with rate limiting

SF-TASK-284 — Add approved third-party origin catalog

SF-TASK-285 — Add SRI validation for approved external assets

SF-TASK-286 — Add browser security integration tests

SF-TASK-287 — Add Blazor Server CSP compatibility tests

SF-TASK-288 — Add Identity no-store and no-referrer tests

SF-TASK-289 — Add CSP exception auditing and review workflow
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 158. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
Toutes les pages HTML possèdent une CSP.

Les pages Identity utilisent un profil strict.

Les pages d’administration utilisent un profil strict.

Aucun script tiers n’est chargé sur les pages Identity.

Aucun `unsafe-eval` n’apparaît en production.

Aucun `unsafe-inline` n’apparaît dans script-src.

Les scripts inline autorisés possèdent un nonce ou hash.

Les handlers HTML inline sont absents.

Le nonce change entre deux réponses.

Le nonce n’apparaît pas dans les logs.

JSON-LD fonctionne avec la CSP.

Blazor Server et SignalR fonctionnent avec le profil.

Les pages ne peuvent pas être encadrées par défaut.

Les formulaires ne peuvent pas cibler une origine externe.

Les pages de récupération utilisent no-referrer.

Les pages sensibles utilisent no-store.

HSTS est appliqué après validation du domaine.

`nosniff` est présent.

Permissions Policy désactive les fonctions inutiles.

Les ressources tierces utilisent une origine exacte.

SRI est utilisé lorsque requis.

Les réponses d’erreur possèdent les headers attendus.

Caddy et l’application ne dupliquent pas la CSP.

L’endpoint de reporting est borné et rate-limited.

Les rapports ne conservent pas les query strings.

Les tests navigateur réussissent sur les profils supportés.
```

---

# 159. Conditions de révision

Réexaminer cette décision si :

- Blazor WebAssembly devient la baseline ;
- un SSO externe est introduit ;
- des passkeys nécessitent de nouvelles permissions ;
- un CDN met en cache le HTML ;
- une architecture multi-domaines est adoptée ;
- plusieurs scripts tiers deviennent indispensables ;
- un portail partenaire doit encadrer l’application ;
- COEP devient nécessaire ;
- une isolation cross-origin renforcée devient obligatoire ;
- un WAF ou edge provider gère une partie des headers ;
- les applications deviennent des PWA ;
- un sous-domaine séparé héberge des contenus actifs.

---

# 160. Indicateurs à suivre

- profils actifs ;
- violations CSP ;
- violations par directive ;
- violations Identity ;
- origines tierces ;
- exceptions actives ;
- exceptions expirées ;
- scripts inline détectés ;
- pages sans CSP ;
- erreurs sans headers ;
- rapports rate-limited ;
- taux d’erreur Blazor lié à CSP ;
- durée du mode Report-Only ;
- versions de profils en production.

---

# 161. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
unsafe-eval apparaît

unsafe-inline script apparaît

des wildcards sont ajoutés

les pages Identity chargent du tiers

les nonces sont réutilisés

les rapports exposent des URLs sensibles

les exceptions ne sont jamais retirées

les headers disparaissent sur les erreurs

Caddy et l’application divergent

la CSP casse fréquemment les releases

les scripts tiers se multiplient

le profil development atteint la production
```

---

# 162. Conséquences observées

Cette section est append-only après mise en œuvre.

## 162.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation
des profils de sécurité navigateur,
des nonces CSP et du reporting des violations.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- profil ;
- navigateur ;
- directive ;
- violation ;
- impact ;
- action proposée.

---

# 163. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0032-deployment-rollout-maintenance-mode-and-rollback-safety.md](./0032-deployment-rollout-maintenance-mode-and-rollback-safety.md)
- [0029-http-api-contracts-problem-details-and-idempotent-commands.md](./0029-http-api-contracts-problem-details-and-idempotent-commands.md)
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [09-module-architecture.md](../02-architecture/09-module-architecture.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
- SEO et JSON-LD : les obligations des sections 30 et 102 du présent ADR sont autonomes ; aucun moteur d'automatisation SEO privé n'est requis.
- [18-security-baseline.md](../04-quality-security/18-security-baseline.md)
- [19-testing-strategy.md](../04-quality-security/19-testing-strategy.md)
- [20-quality-gates-and-ci.md](../04-quality-security/20-quality-gates-and-ci.md)
- [21-observability-and-logging.md](../05-operations/21-observability-and-logging.md)
- [22-deployment-and-environments.md](../05-operations/22-deployment-and-environments.md)
- [24-operational-runbooks.md](../05-operations/24-operational-runbooks.md)
- [25-upgrade-and-maintenance-strategy.md](../05-operations/25-upgrade-and-maintenance-strategy.md)
- [26-release-and-versioning.md](../05-operations/26-release-and-versioning.md)
- [27-client-handover-and-service-closure.md](../05-operations/27-client-handover-and-service-closure.md)
- [28-codex-operating-guide.md](../06-codex/28-codex-operating-guide.md)
- [0000-adr-template.md](./0000-adr-template.md)
- [0001-one-repository-and-database-per-client.md](./0001-one-repository-and-database-per-client.md)
- [0005-file-generation-strategies.md](./0005-file-generation-strategies.md)
- [0006-feature-flags-and-entitlements-separation.md](./0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](./0007-module-composition-and-public-contracts.md)
- [0008-private-package-feed-and-supply-chain.md](./0008-private-package-feed-and-supply-chain.md)
- [0009-client-customizations-and-extension-points.md](./0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](./0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](./0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](./0012-runtime-configuration-and-secrets.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](./0014-explicit-client-upgrades-and-version-pinning.md)
- [0015-deterministic-generation-and-optional-ai.md](./0015-deterministic-generation-and-optional-ai.md)
- [0016-content-and-media-storage-boundaries.md](./0016-content-and-media-storage-boundaries.md)
- [0018-public-form-abuse-protection-and-rate-limiting.md](./0018-public-form-abuse-protection-and-rate-limiting.md)
- [0019-audit-trail-and-sensitive-administrative-actions.md](./0019-audit-trail-and-sensitive-administrative-actions.md)
- [0020-administrator-session-security-and-step-up-authentication.md](./0020-administrator-session-security-and-step-up-authentication.md)
- [0021-administrator-provisioning-invitations-and-account-recovery.md](./0021-administrator-provisioning-invitations-and-account-recovery.md)

---

# 164. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 159 |

---

# 165. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 166. Synthèse de la décision

```text
Profils
→ public standard
→ public avec embed approuvé
→ administration stricte
→ authentification stricte
→ ressource privée
→ erreur minimale

CSP
→ header HTTP
→ versionnée
→ route-specific
→ stricte par défaut
→ Report-Only avant nouvelle enforcement

Scripts
→ self-hosted
→ nonce ou hash pour inline
→ aucun handler inline
→ aucun unsafe-inline script
→ aucun unsafe-eval
→ aucune origine wildcard

Styles
→ self-hosted
→ nonce pour les blocs inline
→ attributs inline interdits comme cible
→ exception limitée et revue si nécessaire

Blazor
→ script framework self
→ WebSocket exact
→ profil Server testé
→ profil WASM distinct si nécessaire
→ aucun unsafe-eval général

Identity
→ aucun tiers
→ no-store
→ no-referrer
→ frame-ancestors none
→ form-action self

Headers
→ HSTS
→ nosniff
→ Referrer-Policy
→ Permissions-Policy
→ COOP
→ CORP
→ X-Frame-Options
→ cache adapté

Tiers
→ origine exacte
→ version verrouillée
→ SRI lorsque nécessaire
→ revue sécurité et confidentialité
→ interdit sur les pages sensibles

Reporting
→ endpoint borné
→ rate limiting
→ données non fiables
→ URLs neutralisées
→ métriques agrégées
→ aucune action métier

Caddy
→ HTTPS
→ HSTS
→ headers statiques
→ erreurs sécurisées

Application
→ nonce
→ CSP dynamique
→ profil de route
→ cache sensible

Qualité
→ tests navigateur
→ tests Blazor
→ tests Identity
→ headers sur erreurs
→ aucune CSP dupliquée
```

> Une Content Security Policy efficace ne consiste pas à ajouter un header générique à toutes les réponses. Elle consiste à définir précisément les capacités nécessaires à chaque zone de l’application, puis à refuser toute source, script, iframe ou fonctionnalité qui n’a pas été explicitement justifiée.
