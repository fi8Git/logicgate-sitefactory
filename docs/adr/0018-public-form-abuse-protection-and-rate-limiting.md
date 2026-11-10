---
adrId: ADR-0018
title: Protect public forms with layered abuse controls and server-side rate limiting
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
  - ./0031-application-startup-validation-and-runtime-compatibility.md
  - ../02-architecture/core-module-boundary.md
  - ../02-architecture/10-feature-flags-and-entitlements.md
  - ./0023-data-retention-deletion-and-privacy-lifecycle.md
  - ./0020-administrator-session-security-and-step-up-authentication.md
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
  - ./0003-postgresql-as-primary-database.md
  - ./0004-explicit-production-migrations.md
  - ./0006-feature-flags-and-entitlements-separation.md
  - ./0007-module-composition-and-public-contracts.md
  - ./0009-client-customizations-and-extension-points.md
  - ./0010-structured-observability-with-opentelemetry.md
  - ./0011-docker-compose-vps-deployment-profile.md
  - ./0012-runtime-configuration-and-secrets.md
  - ./0014-explicit-client-upgrades-and-version-pinning.md
  - ./0015-deterministic-generation-and-optional-ai.md
  - ./0016-content-and-media-storage-boundaries.md
  - ./0017-background-jobs-and-reliable-side-effects.md
targetVersion: 0.1.0
---

# ADR-0018 — Protection des formulaires publics contre les abus et limitation de débit

## Portée publique et état d'implémentation

Le statut `accepted` et la date ci-dessous décrivent la décision architecturale
historique, pas la disponibilité de ses mécanismes dans la distribution publique.
Le moteur, le ModuleSdk et le module Documentation ne fournissent pas de
formulaire métier public ni la chaîne anti-abus complète décrite ici.
Les exemples de soumission, de lead, d'outbox, de quarantaine et de challenge sont
des contrats et scénarios de référence pour les applications qui les implémenteront.

Les décisions, restrictions et preuves attendues restent obligatoires dans leur
périmètre ; aucune protection de formulaire ou capacité de production n'est
déclarée livrée par cet ADR. Les responsabilités d'un opérateur et d'un module
consommateur ne créent aucune dépendance du build public vers une distribution privée.

## 1. Statut

```text
accepted
```

La décision prescrit la protection des formulaires publics à l’aide d’une défense en profondeur combinant :

```text
validation serveur
+ limites de taille
+ limitation de débit
+ limitation de concurrence
+ jetons de soumission
+ idempotence
+ signaux anti-automatisation
+ challenge humain facultatif
+ quarantaine
+ observabilité
+ kill switch opérationnel
```

Aucun mécanisme unique n’est considéré comme suffisant.

En particulier :

```text
CORS n’est pas un mécanisme anti-spam.

CORS n’est pas une autorisation.

CORS n’empêche pas un client HTTP direct.

Une validation JavaScript n’est pas une protection serveur.

Un champ honeypot n’arrête pas un attaquant déterminé.

Un CAPTCHA ne remplace pas le rate limiting.

Une adresse IP ne constitue pas une identité fiable.

Une réponse HTTP réussie ne doit pas entraîner plusieurs leads identiques.
```

---

## 2. Résumé

Le flux de référence d’une soumission publique est :

```text
requête reçue par Caddy
→ contrôle des limites réseau et de taille
→ résolution de l’adresse source depuis un proxy approuvé
→ limitation globale et par source
→ validation du Content-Type et du corps
→ validation du jeton de formulaire
→ validation des champs
→ évaluation des signaux d’abus
→ challenge éventuel
→ décision allow / quarantine / reject
→ création idempotente du lead
→ message outbox dans la même transaction
→ réponse générique
```

Les notifications, emails et intégrations externes sont exécutés par les jobs persistants définis dans l’ADR-0017.

Le système distingue :

```text
Validation fonctionnelle
→ les données sont-elles valides ?

Protection contre les abus
→ la soumission semble-t-elle automatisée ou excessive ?

Idempotence
→ cette même intention a-t-elle déjà été traitée ?

Autorisation
→ cet appelant peut-il effectuer cette action ?

Rate limiting
→ cette source consomme-t-elle trop rapidement les ressources ?

Challenge humain
→ une vérification supplémentaire est-elle nécessaire ?
```

---

## 3. Invariants principaux

Les invariants suivants sont obligatoires :

```text
Toute validation importante est exécutée côté serveur.

Une soumission rate-limited ne crée aucun lead.

Une soumission rejetée ne déclenche aucun email.

Une soumission mise en quarantaine ne déclenche pas
les notifications métier ordinaires avant validation.

Une nouvelle tentative avec le même identifiant de soumission
ne crée pas un second lead.

Les limites sont appliquées avant les traitements coûteux.

Les adresses sources ne proviennent que de proxies approuvés.

Un header X-Forwarded-For fourni directement par Internet
n’est jamais considéré comme fiable.

Les compteurs de métriques n’utilisent pas les adresses IP
ou les identifiants de soumission comme labels.

Le système peut désactiver temporairement un formulaire public
sans supprimer son module ou son entitlement.

Les contrôles anti-abus ne constituent pas une protection
contre un déni de service volumétrique.
```

---

## 4. Contexte

Les formulaires publics constituent une frontière exposée à Internet.

Ils peuvent être utilisés pour :

- contacter une entreprise ;
- demander un devis ;
- déposer une demande ;
- créer un lead ;
- s’inscrire à une liste ;
- initier un parcours métier ;
- transmettre des informations.

Ils peuvent également être détournés pour :

- envoyer du spam ;
- créer des milliers de leads ;
- saturer PostgreSQL ;
- déclencher des emails ;
- consommer un provider externe ;
- remplir les logs ;
- saturer le worker ;
- provoquer des coûts ;
- tester des payloads malveillants ;
- envoyer du contenu offensant ;
- contourner les limites d’un autre service ;
- utiliser le site comme relais d’email.

Une soumission peut provenir :

- d’un navigateur légitime ;
- d’un robot simple ;
- d’un navigateur automatisé ;
- d’un script HTTP ;
- d’un réseau partagé ;
- d’un proxy ;
- d’une adresse IPv6 tournante ;
- d’un service distribué.

Il n’existe donc pas de signal unique permettant de distinguer parfaitement un humain légitime d’un abus.

---

## 5. Problème à résoudre

Comment permettre à un visiteur légitime d’envoyer un formulaire tout en limitant :

- le spam ;
- les robots ;
- les soumissions répétées ;
- les doublons ;
- les attaques distribuées ;
- les coûts d’email ;
- les écritures excessives ;
- les payloads volumineux ;
- les appels externes ;
- les faux positifs ;
- la collecte excessive de données réseau ?

La solution doit également :

- rester accessible ;
- fonctionner derrière Caddy ;
- ne pas dépendre obligatoirement d’un CAPTCHA ;
- conserver une expérience simple ;
- être configurable par type de formulaire ;
- rester compatible avec une instance web unique ;
- préparer une évolution multi-instance ;
- permettre une réponse opérationnelle rapide.

---

## 6. Portée

Cette décision couvre :

- formulaires publics ;
- endpoints anonymes ;
- création de leads ;
- demande de devis ;
- validation serveur ;
- antiforgery ;
- jetons de soumission ;
- idempotence ;
- rate limiting ;
- concurrence ;
- forwarded headers ;
- honeypots ;
- signaux comportementaux ;
- scoring déterministe ;
- challenges humains ;
- quarantaine ;
- notifications ;
- observabilité ;
- protection des données ;
- kill switches ;
- tests ;
- déploiement ;
- procédures d’incident.

---

## 7. Hors portée

Cette décision ne définit pas :

- une protection DDoS volumétrique complète ;
- un WAF obligatoire ;
- un fournisseur CAPTCHA définitif ;
- un système biométrique ;
- une identification certaine des personnes ;
- une liste universelle d’adresses malveillantes ;
- un moteur de détection par machine learning ;
- une surveillance inter-clients centralisée ;
- les uploads publics, sauf profil spécifique ;
- une messagerie instantanée ;
- un chatbot public ;
- une modération humaine complète ;
- une garantie d’absence totale de spam.

---

## 8. Decision drivers

Par ordre de priorité :

1. **Protection des ressources du service**
2. **Absence de notification abusive**
3. **Validation serveur**
4. **Réduction des doublons**
5. **Préservation des leads légitimes**
6. **Protection de PostgreSQL et des workers**
7. **Minimisation des données personnelles**
8. **Dégradation contrôlée**
9. **Accessibilité**
10. **Observabilité**
11. **Configuration par formulaire**
12. **Indépendance du fournisseur de challenge**
13. **Compatibilité avec Caddy et Docker Compose**
14. **Testabilité**
15. **Possibilité d’ajouter un WAF ou CDN**
16. **Coût raisonnable pour le MVP**

---

## 9. Contraintes

La décision doit respecter :

```text
ASP.NET Core

Caddy comme reverse proxy initial

Application non exposée directement

PostgreSQL

Jobs persistants

Validation serveur

Réponses bornées

Aucune IP brute dans les métriques

Aucun secret dans les logs

Challenge facultatif

Aucun appel fournisseur avant les contrôles locaux

Aucun CORS considéré comme protection

Compatibilité Windows et Linux

Configuration typée et validée
```

---

## 10. Hypothèses

Cette décision repose sur les hypothèses suivantes :

- le profil initial utilise une seule instance web par projet ;
- Caddy est le seul reverse proxy public du profil standard ;
- un taux faible de faux positifs reste préférable à une acceptation aveugle ;
- les formulaires de contact ordinaires n’acceptent pas de fichier ;
- les soumissions légitimes par source sont peu fréquentes ;
- un challenge externe peut être indisponible ;
- le nombre de formulaires par projet reste limité ;
- les seuils devront être ajustés à partir des métriques réelles ;
- un attaquant déterminé peut contourner les signaux purement clients.

---

# 11. Option A — Validation fonctionnelle uniquement

## 11.1 Description

Le formulaire vérifie seulement :

- champs obligatoires ;
- email ;
- téléphone ;
- longueurs ;
- format.

---

## 11.2 Avantages

- simple ;
- expérience sans friction ;
- faible coût ;
- peu de configuration.

---

## 11.3 Inconvénients

- aucune limite de débit ;
- aucune détection de doublon ;
- spam facile ;
- notifications massives ;
- consommation de ressources ;
- robots capables de produire des données valides.

---

## 11.4 Risques

- milliers de leads valides au sens du schéma ;
- boîte email saturée ;
- PostgreSQL rempli ;
- coûts de provider ;
- traitement manuel impossible.

---

## 11.5 Réversibilité

```text
élevée
```

mais l’incident peut déjà avoir produit des données et notifications.

---

# 12. Option B — CAPTCHA obligatoire pour chaque soumission

## 12.1 Description

Toute soumission publique exige un challenge fourni par un service externe.

---

## 12.2 Avantages

- réduit les robots simples ;
- solution connue ;
- faible logique interne ;
- signal supplémentaire fourni par un spécialiste.

---

## 12.3 Inconvénients

- dépendance externe ;
- accessibilité ;
- confidentialité ;
- latence ;
- coût potentiel ;
- indisponibilité bloquant tous les formulaires ;
- robots avancés capables de contourner certains challenges.

---

## 12.4 Risques

- perte de conversion ;
- fournisseur indisponible ;
- transmission de données ;
- challenge impossible pour certains utilisateurs ;
- faux sentiment de sécurité.

---

## 12.5 Réversibilité

```text
élevée
```

---

# 13. Option C — Rate limiting par adresse IP uniquement

## 13.1 Description

Chaque adresse IP possède un quota de soumissions.

---

## 13.2 Avantages

- simple ;
- rapide ;
- peu de données ;
- efficace contre les robots élémentaires ;
- support natif côté application.

---

## 13.3 Inconvénients

- réseaux NAT ;
- IPv6 ;
- proxies ;
- adresses tournantes ;
- attaques distribuées ;
- IP falsifiée si le proxy est mal configuré ;
- risque de blocage collectif.

---

## 13.4 Risques

- blocage d’une entreprise entière ;
- contournement par rotation d’adresses ;
- confiance incorrecte dans `X-Forwarded-For` ;
- cardinalité excessive.

---

## 13.5 Réversibilité

```text
élevée
```

---

# 14. Option D — Défense en profondeur adaptative

## 14.1 Description

Le système combine :

- limites ;
- idempotence ;
- validation ;
- signaux ;
- challenge facultatif ;
- quarantaine ;
- observabilité ;
- kill switch.

---

## 14.2 Avantages

- aucune dépendance unique ;
- meilleure protection ;
- challenge uniquement lorsque nécessaire ;
- réduction des faux positifs ;
- meilleure réponse opérationnelle ;
- meilleure protection des providers ;
- architecture extensible.

---

## 14.3 Inconvénients

- davantage de logique ;
- configuration ;
- tests ;
- suivi des seuils ;
- stockage d’états supplémentaires ;
- besoin de runbooks.

---

## 14.4 Risques

- scoring mal calibré ;
- règles incohérentes ;
- challenge trop fréquent ;
- quarantaine non surveillée ;
- complexité excessive.

---

## 14.5 Réversibilité

```text
élevée
```

Chaque couche peut être ajustée indépendamment.

---

# 15. Comparaison des options

Échelle indicative :

```text
1 = défavorable
5 = très favorable
```

| Critère | Poids | Validation seule | CAPTCHA obligatoire | IP uniquement | Défense en profondeur |
|---|---:|---:|---:|---:|---:|
| Protection globale | 5 | 1 | 4 | 3 | 5 |
| Accessibilité | 5 | 5 | 2 | 4 | 5 |
| Résilience fournisseur | 4 | 5 | 1 | 5 | 5 |
| Attaques distribuées | 5 | 1 | 3 | 1 | 4 |
| Faux positifs | 4 | 5 | 3 | 2 | 4 |
| Protection des ressources | 5 | 1 | 3 | 4 | 5 |
| Observabilité | 4 | 2 | 3 | 3 | 5 |
| Maintenabilité | 4 | 4 | 3 | 4 | 4 |
| Réversibilité | 4 | 5 | 5 | 5 | 5 |
| Adaptation au risque | 5 | 1 | 2 | 2 | 5 |

L’option D est retenue.

---

# 16. Décision

Nous décidons que les formulaires publics utilisent une défense en profondeur appliquée côté serveur.

Le pipeline possède les niveaux suivants :

```text
1. Protection réseau et reverse proxy
2. Limites de requête
3. Rate limiting global
4. Rate limiting partitionné
5. Jeton de formulaire
6. Idempotence
7. Validation fonctionnelle
8. Signaux anti-automatisation
9. Challenge éventuel
10. Décision d’abus
11. Persistance
12. Jobs et notifications
```

---

## 16.1 Aucun mécanisme unique

Un formulaire ne doit pas être considéré comme protégé uniquement parce qu’il possède :

- un CAPTCHA ;
- un honeypot ;
- une regex ;
- CORS ;
- un token antiforgery ;
- une limite IP.

---

## 16.2 Décision déterministe

Le scoring initial utilise des règles déterministes et versionnées.

Le MVP n’utilise pas de modèle AI pour accepter ou rejeter un lead.

---

# 17. Profils de protection

Chaque formulaire public référence un profil de protection.

Exemples conceptuels :

```text
public-contact-standard

public-quote-standard

public-high-risk

public-upload-restricted
```

---

## 17.1 Contenu du profil

Un profil doit pouvoir déclarer :

```text
RequestBodyLimit

FieldLimits

RateLimitPolicy

ConcurrencyPolicy

FormTokenPolicy

DuplicatePolicy

HoneypotPolicy

TimingPolicy

ChallengeMode

QuarantinePolicy

NotificationPolicy
```

---

## 17.2 Version

Le profil possède une version.

---

## 17.3 Override client

Un override client ne peut pas réduire une limite de sécurité minimale sans décision explicite.

---

# 18. Configuration structurelle et runtime

## 18.1 Structurelle

Le formulaire déclare dans sa composition :

- Form ID ;
- champs ;
- profil de protection ;
- capability ;
- endpoint ;
- workflow.

---

## 18.2 Runtime

Les seuils opérationnels peuvent être configurés par environnement :

- limites ;
- challenge activé ;
- sampling ;
- mode de quarantaine ;
- kill switch.

---

## 18.3 Secrets

Le credential du fournisseur de challenge suit l’ADR-0012.

---

# 19. Identifiant du formulaire

Chaque formulaire possède un identifiant stable.

Exemples :

```text
public.contact

public.quote-request

public.callback-request
```

---

## 19.1 Usage

Le Form ID est utilisé dans :

- policies ;
- logs ;
- métriques ;
- idempotence ;
- configuration ;
- audit.

---

## 19.2 Nom de route

Le Form ID ne dépend pas uniquement de l’URL.

---

# 20. Reverse proxy

Dans le profil initial :

```text
Internet
→ Caddy
→ application
```

L’application ne doit pas être accessible directement depuis Internet.

---

## 20.1 Ports

Le service web ne publie aucun port hôte dans le profil standard.

---

## 20.2 Avantage

Cette règle réduit le risque de contourner :

- les règles Caddy ;
- les headers normalisés ;
- les limites edge ;
- les futurs contrôles WAF.

---

# 21. Forwarded headers

L’application peut utiliser l’adresse source transmise par Caddy uniquement lorsque celui-ci est explicitement approuvé.

---

## 21.1 Sources approuvées

La configuration doit définir :

- Known Proxy ;
- Known Network ;
- Forward Limit ;
- headers acceptés.

---

## 21.2 Header Internet

Une requête Internet ne doit pas pouvoir choisir sa partition de rate limiting avec :

```text
X-Forwarded-For
X-Real-IP
Forwarded
```

---

## 21.3 Caddy

Caddy doit remplacer ou normaliser les headers entrants selon la configuration retenue.

---

## 21.4 Échec de confiance

Si la chaîne de proxy n’est pas valide, l’application utilise l’adresse immédiate et produit un diagnostic.

---

# 22. Normalisation des adresses sources

Une adresse source est normalisée avant partitionnement.

---

## 22.1 IPv4

Utiliser une représentation canonique.

---

## 22.2 IPv6

Une politique peut regrouper les adresses par préfixe afin de limiter la rotation facile d’adresses.

---

## 22.3 Préfixe IPv6

Le préfixe doit être configurable.

Il ne doit pas être supposé universellement fiable.

---

## 22.4 Adresse absente

Une requête sans adresse source exploitable utilise une partition de secours stricte.

---

# 23. Données personnelles réseau

Une adresse IP peut constituer une donnée personnelle.

---

## 23.1 Baseline

La limitation locale peut utiliser l’adresse en mémoire sans la persister.

---

## 23.2 Source fingerprint

Lorsqu’une persistance temporaire est nécessaire, utiliser un identifiant pseudonymisé tel qu’un HMAC :

```text
HMAC(rotation-key, normalized-source)
```

---

## 23.3 Rotation

La clé de pseudonymisation doit pouvoir être renouvelée afin de limiter la corrélation dans le temps.

---

## 23.4 Logs

Les logs applicatifs ne contiennent pas l’adresse IP brute par défaut.

---

## 23.5 Accès Caddy

Les logs d’accès du reverse proxy possèdent leur propre politique de minimisation et de rétention.

---

# 24. Limites de requête

Les limites doivent être appliquées avant la désérialisation complète et les traitements coûteux.

---

## 24.1 Limite du corps

Un formulaire texte standard utilise une limite initiale prudente.

Exemple de baseline :

```text
64 KiB
```

La valeur exacte appartient au profil.

---

## 24.2 Champs

Chaque champ possède :

- longueur maximale ;
- type ;
- normalisation ;
- validation ;
- nombre d’occurrences.

---

## 24.3 Collections

Le nombre d’éléments d’une collection est borné.

---

## 24.4 Profondeur

Les formats structurés ne doivent pas accepter une profondeur arbitraire.

---

# 25. Content-Type

Un endpoint accepte uniquement les Content-Types prévus.

Exemples possibles :

```text
application/x-www-form-urlencoded

application/json
```

---

## 25.1 Multipart

```text
multipart/form-data
```

est refusé lorsqu’aucun upload n’est attendu.

---

## 25.2 Upload public

L’ajout d’un upload public nécessite un profil distinct et les protections de l’ADR-0016.

---

## 25.3 Content-Type inconnu

Réponse :

```text
415 Unsupported Media Type
```

---

# 26. Limitation globale

Une limite globale protège l’instance contre un volume excessif.

---

## 26.1 Objectif

Limiter les requêtes simultanées ou le débit total avant :

- PostgreSQL ;
- provider de challenge ;
- worker ;
- email ;
- intégration.

---

## 26.2 Concurrence

Une limite de concurrence peut être utilisée sur les endpoints publics coûteux.

---

## 26.3 Queue d’attente

La queue d’attente du rate limiter doit rester nulle ou très faible.

Une longue attente consommerait elle-même des ressources.

---

# 27. Limitation partitionnée

Le rate limiting est appliqué par partition.

Partitions possibles :

```text
Form ID

Source réseau normalisée

Endpoint

Client technique

Environnement
```

---

## 27.1 Clé conceptuelle

```text
{FormId}:{SourcePartition}
```

---

## 27.2 User-Agent

Le User-Agent ne doit pas être utilisé comme identité principale.

Il est facilement falsifiable.

---

## 27.3 Cookie anonyme

Un cookie signé peut être utilisé comme signal secondaire.

Sa suppression ne doit pas permettre de contourner toutes les limites.

---

# 28. Algorithmes de limitation

Selon la politique, le système peut utiliser :

- token bucket ;
- sliding window ;
- fixed window ;
- concurrency limiter.

---

## 28.1 Baseline

Le profil standard privilégie :

```text
token bucket ou sliding window
+ limite de concurrence
```

---

## 28.2 Burst

Un petit burst peut permettre :

- double clic ;
- retry réseau ;
- navigation légitime.

---

## 28.3 Absence de burst illimité

Une grande capacité de burst réduirait la protection initiale.

---

# 29. Seuils initiaux

Les seuils exacts restent configurables et doivent être calibrés.

Exemple de baseline de démarrage pour un formulaire de contact :

```text
Par source et par formulaire :
- burst court limité
- quelques soumissions sur une fenêtre de plusieurs minutes
- quota horaire borné

Globalement :
- concurrence limitée
- débit total compatible avec PostgreSQL et le worker

Duplicate window :
- plusieurs minutes

Form token lifetime :
- durée bornée
```

---

## 29.1 Pas de contrat universel

Les seuils ne doivent pas être considérés comme universels ou suffisants pour tous les clients.

---

## 29.2 Ajustement

Les changements utilisent :

- métriques ;
- faux positifs ;
- trafic légitime ;
- incidents ;
- capacité du serveur.

---

# 30. Réponse rate-limited

Une requête bloquée par le rate limiter reçoit :

```text
429 Too Many Requests
```

---

## 30.1 Retry-After

Lorsque possible, la réponse inclut :

```text
Retry-After
```

---

## 30.2 Corps

Le message reste générique.

Il ne révèle pas :

- seuil exact ;
- état du compteur ;
- règle déclenchée ;
- score d’abus.

---

## 30.3 Persistance

Aucun lead ou job métier n’est créé.

---

# 31. Limites en mémoire

Le rate limiter initial peut fonctionner dans le processus web.

---

## 31.1 Avantages

- faible latence ;
- absence de dépendance ;
- simplicité du MVP.

---

## 31.2 Limites

Les compteurs sont réinitialisés lors :

- d’un redémarrage ;
- d’un déploiement ;
- d’un changement d’instance.

---

## 31.3 Multi-instance

Plusieurs instances nécessiteraient :

- store partagé ;
- edge rate limiting ;
- WAF ;
- ou service distribué.

---

## 31.4 Baseline

Le modèle in-process est accepté uniquement parce que le profil initial utilise une instance web.

---

# 32. Protection contre les attaques distribuées

Une attaque répartie sur de nombreuses adresses peut contourner les quotas par source.

---

## 32.1 Contrôles complémentaires

- limite globale ;
- challenge adaptatif ;
- quarantaine ;
- seuils par Form ID ;
- kill switch ;
- protection hébergeur ;
- CDN ou WAF futur.

---

## 32.2 Limite applicative

Le rate limiting applicatif ne protège pas la bande passante ou le VPS contre une attaque volumétrique majeure.

---

# 33. CORS

CORS contrôle principalement ce qu’un navigateur autorise à un script d’une autre origine.

---

## 33.1 CORS ne bloque pas

CORS ne bloque pas :

- curl ;
- Postman ;
- un script serveur ;
- un bot ;
- un navigateur automatisé ;
- une requête construite directement.

---

## 33.2 Décision

CORS ne doit jamais être cité comme mécanisme de :

- rate limiting ;
- authentification ;
- autorisation ;
- anti-spam ;
- validation.

---

## 33.3 Configuration

La politique CORS reste minimale et adaptée aux APIs qui en ont réellement besoin.

---

# 34. Antiforgery et CSRF

Les formulaires navigateur same-origin doivent utiliser les protections antiforgery fournies par la plateforme lorsque le modèle d’hébergement le permet.

---

## 34.1 Objectif

L’antiforgery réduit notamment les soumissions déclenchées depuis un autre site au nom du navigateur.

---

## 34.2 Limite

Un robot peut charger la page, obtenir un token et soumettre le formulaire.

---

## 34.3 Conclusion

L’antiforgery est une protection complémentaire.

Ce n’est pas un CAPTCHA ni un rate limiter.

---

## 34.4 Clés Data Protection

Les clés Data Protection doivent être persistées conformément à l’ADR-0011.

---

## 34.5 Token invalide

Le client reçoit une réponse contrôlée lui demandant de recharger le formulaire.

---

# 35. Jeton de formulaire

Un formulaire peut inclure un jeton signé distinct contenant :

```text
FormId

SubmissionId

IssuedAtUtc

ExpiresAtUtc

RandomNonce
```

---

## 35.1 Signature

Le jeton est protégé par les mécanismes de Data Protection ou un mécanisme équivalent approuvé.

---

## 35.2 Objectifs

- lier la soumission au formulaire ;
- fournir un Submission ID ;
- limiter la durée de vie ;
- améliorer l’idempotence ;
- détecter certains replays.

---

## 35.3 Limite

Le jeton ne prouve pas que l’appelant est humain.

---

## 35.4 Logs

Le token complet ne doit pas être journalisé.

---

# 36. Durée de vie du jeton

Le jeton possède une durée bornée.

---

## 36.1 Trop court

Une durée trop faible peut pénaliser :

- les utilisateurs lents ;
- l’accessibilité ;
- les formulaires longs.

---

## 36.2 Trop long

Une durée trop élevée augmente la fenêtre de replay.

---

## 36.3 Expiration

Un token expiré ne crée aucun lead.

L’utilisateur reçoit une demande de rechargement.

---

# 37. Submission ID

Chaque intention de soumission possède un identifiant opaque.

---

## 37.1 Unicité

Une contrainte PostgreSQL garantit l’unicité :

```text
FormId + SubmissionId
```

---

## 37.2 Retry réseau

Si le client réessaie avec le même Submission ID après un timeout, le serveur retourne le résultat déjà connu.

---

## 37.3 Nouvel envoi

Une nouvelle soumission volontaire utilise un nouvel identifiant.

---

## 37.4 Secret

Le Submission ID n’est pas un secret.

Il ne doit toutefois pas être utilisé comme label métrique.

---

# 38. Idempotence

L’idempotence protège contre :

- double clic ;
- retry navigateur ;
- retry proxy ;
- timeout après commit ;
- nouvelle tentative client.

---

## 38.1 Transaction

La persistance du lead et du Submission ID est atomique dans PostgreSQL.

---

## 38.2 Notifications

La notification utilise une clé d’idempotence dérivée de la soumission.

---

## 38.3 Réponse

Un duplicate exact peut recevoir la même confirmation générique que l’original.

---

## 38.4 Limite

L’idempotence ne détecte pas deux nouvelles soumissions ayant des Submission IDs différents.

---

# 39. Détection secondaire des doublons

Le système peut calculer un fingerprint sur des champs normalisés.

Exemple conceptuel :

```text
FormId
+ email normalisé
+ téléphone normalisé
+ contenu normalisé
+ fenêtre temporelle
```

---

## 39.1 Pseudonymisation

Le fingerprint utilise un HMAC avec une clé protégée.

---

## 39.2 Usage

Il peut servir à :

- détecter une répétition ;
- augmenter le score d’abus ;
- regrouper des doublons ;
- éviter plusieurs notifications.

---

## 39.3 Pas de rejet automatique universel

Deux demandes légitimes peuvent avoir un contenu proche.

Le fingerprint n’est pas une preuve suffisante à lui seul.

---

# 40. Validation fonctionnelle

La validation est exécutée côté serveur.

---

## 40.1 Email

- longueur bornée ;
- syntaxe raisonnable ;
- normalisation ;
- aucune tentative de vérifier l’existence par divulgation.

---

## 40.2 Téléphone

- longueur bornée ;
- caractères autorisés ;
- normalisation ;
- règles adaptées au produit.

---

## 40.3 Message

- longueur maximale ;
- contrôle des caractères ;
- normalisation des fins de ligne ;
- absence de HTML actif ;
- nombre de liens analysé comme signal.

---

## 40.4 Noms

- longueur ;
- Unicode valide ;
- absence de caractère de contrôle.

---

## 40.5 Validation client

La validation JavaScript améliore l’expérience, mais le serveur répète tous les contrôles.

---

# 41. Normalisation

La normalisation ne doit pas dénaturer le message utilisateur.

---

## 41.1 Email

La casse de la partie domaine peut être normalisée.

La gestion de la partie locale doit rester prudente.

---

## 41.2 Téléphone

Les séparateurs visuels peuvent être retirés afin de produire une représentation comparable.

---

## 41.3 Texte

Les espaces et fins de ligne peuvent être normalisés uniquement pour les comparaisons et fingerprints.

Le contenu original validé peut être conservé.

---

# 42. Contenu actif

Les formulaires publics textuels ne doivent pas accepter de contenu actif.

---

## 42.1 HTML

Le HTML est :

- refusé ;
- encodé ;
- ou sanitizé selon un contrat explicite.

---

## 42.2 Scripts

Tout script ou handler HTML est interdit.

---

## 42.3 Affichage administrateur

Le contenu est encodé lorsqu’il est affiché dans l’administration.

---

# 43. Liens

Le nombre de liens peut être utilisé comme signal d’abus.

---

## 43.1 Pas de règle universelle

Un message légitime peut contenir un lien.

---

## 43.2 Signaux

- nombreux liens ;
- URL raccourcie ;
- schéma inhabituel ;
- domaine répété ;
- texte vide avec liens.

---

## 43.3 Décision

Le signal contribue à une décision.

Il ne constitue pas nécessairement un rejet direct.

---

# 44. Honeypot

Un formulaire peut inclure un champ non pertinent pour un utilisateur humain.

---

## 44.1 Signal

Un champ rempli augmente le niveau de suspicion.

---

## 44.2 Accessibilité

Le champ ne doit pas :

- gêner un lecteur d’écran ;
- être présenté comme obligatoire ;
- être facilement rempli par l’autofill légitime.

---

## 44.3 Pas de rejet unique

La baseline ne rejette pas systématiquement une demande sur ce seul signal.

---

## 44.4 Nom stable

Le nom du champ ne doit pas prétendre être un secret.

Un robot peut l’identifier.

---

# 45. Durée d’interaction

Le système peut mesurer le temps entre :

- l’émission du jeton ;
- la soumission.

---

## 45.1 Soumission très rapide

Une soumission extrêmement rapide constitue un signal.

---

## 45.2 Utilisateur légitime

L’autofill, les technologies d’assistance ou un formulaire simple peuvent produire une soumission rapide.

---

## 45.3 Conclusion

La durée n’est jamais l’unique motif de rejet.

---

# 46. JavaScript

L’absence ou la présence de JavaScript peut constituer un signal faible.

---

## 46.1 Accessibilité

Un utilisateur sans JavaScript doit pouvoir utiliser le formulaire lorsque l’architecture du site le permet.

---

## 46.2 Sécurité

Un attaquant peut exécuter JavaScript.

Le système ne doit pas considérer sa présence comme une preuve humaine.

---

# 47. User-Agent

Le User-Agent peut être analysé comme signal secondaire.

---

## 47.1 Limite

Il est contrôlé par l’appelant.

---

## 47.2 Données

Il ne doit pas être conservé intégralement sans besoin.

---

## 47.3 Métriques

Il ne doit pas être utilisé comme label non borné.

---

# 48. Scoring déterministe

Les signaux peuvent produire un score ou une classification.

---

## 48.1 Exemples de signaux

```text
Rate limit proche du seuil

Submission ID déjà vu

Fingerprint répété

Honeypot rempli

Temps extrêmement court

Nombre élevé de liens

Form token invalide

Challenge invalide

Payload à la limite maximale

Pattern de contenu répétitif
```

---

## 48.2 Version

Les règles possèdent une version.

---

## 48.3 Explication interne

La décision conserve des Reason Codes internes.

---

## 48.4 Réponse publique

Les détails du score ne sont pas retournés à l’appelant.

---

# 49. Décisions anti-abus

Valeurs recommandées :

```text
allow

challenge-required

quarantine

reject

rate-limited
```

---

## 49.1 `allow`

La soumission peut devenir un lead ordinaire.

---

## 49.2 `challenge-required`

Une vérification supplémentaire est nécessaire.

---

## 49.3 `quarantine`

La soumission est conservée pour revue, sans notification métier ordinaire.

---

## 49.4 `reject`

La soumission n’est pas enregistrée comme lead métier.

---

## 49.5 `rate-limited`

Le débit autorisé est dépassé.

---

# 50. Quarantaine

La quarantaine permet de préserver un possible lead légitime sans déclencher d’effet externe immédiat.

---

## 50.1 Données

Une soumission en quarantaine peut conserver :

- données du formulaire ;
- Reason Codes ;
- version de la policy ;
- date ;
- fingerprint pseudonymisé.

---

## 50.2 Accès

La revue nécessite une permission dédiée.

Exemple :

```text
Forms.AbuseReview
```

---

## 50.3 Notification

Aucun email commercial ordinaire n’est envoyé avant validation.

---

## 50.4 Validation humaine

Un opérateur peut :

- accepter ;
- rejeter ;
- marquer comme spam ;
- fusionner avec un doublon.

---

## 50.5 Rétention

Les quarantaines non traitées possèdent une rétention bornée.

---

# 51. Rejet silencieux ou réponse générique

La réponse publique ne doit pas permettre à un bot de calibrer précisément chaque règle.

---

## 51.1 Validation fonctionnelle

Les erreurs destinées à l’utilisateur légitime peuvent rester explicites.

---

## 51.2 Signaux d’abus

Une réponse générique peut être utilisée pour :

- quarantaine ;
- duplicate déjà accepté ;
- certaines suspicions.

---

## 51.3 Rate limiting

Le rate limiting retourne explicitement `429`.

---

## 51.4 Hard reject

Un rejet pour payload invalide ou challenge impossible utilise un statut contrôlé sans détailler les règles anti-abus.

---

# 52. Challenge humain

Le challenge humain est facultatif et provider-agnostic.

---

## 52.1 Modes

Valeurs conceptuelles :

```text
disabled

adaptive

required
```

---

## 52.2 `disabled`

Aucun provider externe n’est appelé.

---

## 52.3 `adaptive`

Le challenge est demandé uniquement selon la policy.

---

## 52.4 `required`

Toute soumission doit être validée par le provider.

---

# 53. Contrat du challenge

Infrastructure expose un contrat minimal.

Exemple conceptuel :

```csharp
public interface IHumanVerificationService
{
    Task<HumanVerificationResult> VerifyAsync(
        HumanVerificationRequest request,
        CancellationToken cancellationToken);
}
```

---

## 53.1 Domain

Domain ne dépend pas du SDK fournisseur.

---

## 53.2 Token

Le token du challenge :

- est reçu du client ;
- est validé côté serveur ;
- n’est pas journalisé ;
- n’est pas stocké durablement.

---

# 54. Validation du challenge

Le serveur doit vérifier lorsque le provider le permet :

- succès ;
- expiration ;
- hostname ;
- action ;
- score ou résultat ;
- usage unique ou replay ;
- erreurs fournisseur.

---

## 54.1 Client-side uniquement

Une validation effectuée uniquement dans le navigateur est interdite.

---

## 54.2 Réponse fournisseur

La réponse complète du fournisseur n’est pas journalisée par défaut.

---

# 55. Indisponibilité du challenge

La politique dépend du mode.

---

## 55.1 `required`

Le formulaire échoue de manière fermée.

---

## 55.2 `adaptive`

La policy peut :

- appliquer une limitation plus stricte ;
- placer en quarantaine ;
- refuser les soumissions à haut risque.

Elle ne doit pas automatiquement autoriser une soumission fortement suspecte.

---

## 55.3 `disabled`

L’indisponibilité du provider n’a aucun effet.

---

# 56. Accessibilité du challenge

Un challenge doit proposer une alternative accessible.

---

## 56.1 Baseline

Éviter une dépendance exclusive à :

- une image visuelle ;
- une interaction fine ;
- un chronomètre court ;
- une modalité inaccessible.

---

## 56.2 Défaillance

Un utilisateur ne pouvant pas accomplir le challenge doit disposer d’un parcours alternatif documenté lorsque le formulaire est essentiel.

---

# 57. Confidentialité du challenge

Avant l’activation d’un provider externe, documenter :

- données transmises ;
- destination ;
- cookies éventuels ;
- adresse IP ;
- rétention ;
- finalité ;
- sous-traitants.

---

## 57.1 Consentement

Le besoin d’information ou de consentement dépend du provider et du cadre applicable.

---

## 57.2 Baseline

Le challenge externe n’est pas activé implicitement par SiteFactory.

---

# 58. Kill switch

Chaque formulaire public sensible doit pouvoir être désactivé opérationnellement.

---

## 58.1 Exemple

```text
public.contact.submission.disabled
```

---

## 58.2 Effet

Le kill switch :

- refuse les nouvelles soumissions ;
- n’efface aucun lead ;
- n’arrête pas l’administration ;
- produit un diagnostic.

---

## 58.3 Réponse publique

Le message doit rester professionnel et proposer une alternative lorsqu’elle existe.

Il ne doit pas exposer la cause de sécurité.

---

# 59. Feature flags

Un feature flag peut contrôler :

- un nouveau moteur de scoring ;
- un challenge adaptatif ;
- une nouvelle policy ;
- une variante d’interface.

---

## 59.1 Limite

Un feature flag ne peut pas désactiver les contrôles fondamentaux de sécurité sans décision explicite.

---

## 59.2 Entitlement

L’activation technique ne crée aucun entitlement commercial.

---

# 60. Persistance des leads

Une soumission autorisée est persistée dans PostgreSQL.

---

## 60.1 Transaction

La transaction peut créer :

- le lead ;
- l’identifiant de soumission ;
- l’état anti-abus ;
- le message outbox de notification.

---

## 60.2 Échec

Si le commit échoue :

- aucun lead n’est considéré comme accepté ;
- aucun email n’est envoyé.

---

## 60.3 Retry

Une nouvelle tentative utilisant le même Submission ID peut reprendre le résultat.

---

# 61. État anti-abus du lead

Valeurs possibles :

```text
accepted

quarantined

confirmed-spam

false-positive
```

---

## 61.1 Séparation

Le statut anti-abus reste distinct du statut commercial du lead.

---

## 61.2 Exemple

Un lead peut être :

```text
AbuseStatus = accepted
LeadStatus = new
```

---

## 61.3 Archivage

L’archivage commercial ne signifie pas nécessairement que la soumission était du spam.

---

# 62. Notifications

Les notifications passent par l’ADR-0017.

---

## 62.1 Lead accepté

Un message outbox idempotent peut déclencher la notification.

---

## 62.2 Quarantaine

Aucune notification commerciale ordinaire.

Une alerte de quarantaine agrégée peut être prévue.

---

## 62.3 Rejet

Aucun job métier.

---

## 62.4 Rate limited

Aucun job.

---

# 63. Autoresponder

Envoyer automatiquement un email à l’adresse saisie peut transformer le formulaire en outil de nuisance.

---

## 63.1 Baseline

Les emails de confirmation à une adresse non vérifiée sont désactivés par défaut.

---

## 63.2 Activation

Un autoresponder nécessite :

- rate limiting renforcé ;
- idempotence ;
- contenu neutre ;
- aucun lien sensible ;
- limitation par destinataire ;
- monitoring ;
- configuration explicite.

---

## 63.3 Email bombing

Le système doit empêcher qu’un attaquant déclenche de nombreux emails vers une victime.

---

# 64. Limitation par destinataire

Lorsqu’un email sortant vers l’adresse fournie est autorisé, une limite secondaire peut utiliser un fingerprint pseudonymisé du destinataire.

---

## 64.1 Valeur brute

L’email ne doit pas être utilisé comme label métrique.

---

## 64.2 Dédoublonnage

Une même confirmation ne doit pas être envoyée plusieurs fois pour le même Submission ID.

---

# 65. Protection du worker

Les rate limits HTTP ne suffisent pas si chaque demande acceptée crée plusieurs jobs coûteux.

---

## 65.1 Contrôles

- un seul job logique par effet ;
- idempotency key ;
- concurrence bornée ;
- quotas provider ;
- dead-letter ;
- kill switch ;
- backlog monitoring.

---

## 65.2 Bulk spam

Une augmentation rapide des leads acceptés doit déclencher une alerte avant saturation des queues.

---

# 66. Protection de PostgreSQL

Les requêtes publiques doivent éviter :

- recherches coûteuses non indexées ;
- transactions longues ;
- payloads volumineux ;
- écritures multiples inutiles ;
- logs persistants détaillés par requête bloquée.

---

## 66.1 Index

Les index nécessaires incluent selon le modèle :

```text
FormId + SubmissionId

AbuseStatus

CreatedAtUtc

DuplicateFingerprint
```

---

## 66.2 Fingerprint

Le fingerprint ne doit pas provoquer un index global inter-client.

---

# 67. Protection des logs

Une attaque peut chercher à saturer les logs.

---

## 67.1 Rate-limited logs

Les refus attendus ne doivent pas produire une stack trace.

---

## 67.2 Agrégation

Les événements fréquents peuvent être :

- échantillonnés ;
- agrégés ;
- transformés en métriques.

---

## 67.3 Données utilisateur

Le message du formulaire n’est pas journalisé.

---

## 67.4 Injection de log

Les champs utilisateurs restent structurés, encodés et bornés lorsqu’un identifiant doit être journalisé.

---

# 68. Observabilité

Événements recommandés :

```text
PublicFormSubmissionReceived

PublicFormRateLimited

PublicFormTokenInvalid

PublicFormDuplicateDetected

PublicFormChallengeRequired

PublicFormChallengeFailed

PublicFormQuarantined

PublicFormRejected

PublicFormAccepted

PublicFormKillSwitchActive

PublicFormPolicyEvaluationFailed
```

---

## 68.1 Propriétés autorisées

```text
FormId

PolicyId

PolicyVersion

Decision

ReasonCode

Environment

ClientSlug

Result

Duration

CorrelationId
```

---

## 68.2 Données interdites

```text
Email

Téléphone

Nom

Message

Adresse IP brute

Challenge token

Form token

Submission token complet

Secret

User-Agent complet
```

---

# 69. Métriques

Exemples :

```text
public form submissions

public form accepted

public form quarantined

public form rejected

public form rate limited

public form duplicates

challenge requests

challenge failures

submission duration
```

---

## 69.1 Labels autorisés

```text
form_id

policy_id

decision

reason_category

environment
```

---

## 69.2 Labels interdits

```text
IP

SourceFingerprint

SubmissionId

Email

UserId

TraceId

CorrelationId
```

---

# 70. Alertes

Alertes possibles :

```text
Hausse rapide des soumissions

Rate limiting très fréquent

Nombre élevé de quarantaines

Challenge provider indisponible

Forte augmentation des doublons

Backlog de notifications

Formulaire désactivé trop longtemps

Taux de validation anormalement faible

Volume PostgreSQL inhabituel
```

---

## 70.1 Seuil

Une alerte doit utiliser :

- durée ;
- baseline ;
- volume ;
- contexte.

---

## 70.2 Runbook

Les alertes critiques référencent un runbook.

---

# 71. Dashboard opérationnel

Le dashboard peut afficher :

- soumissions ;
- acceptations ;
- rejets ;
- quarantaines ;
- doublons ;
- rate limits ;
- challenge ;
- latence ;
- jobs de notification ;
- évolution temporelle.

---

## 71.1 Pas de PII

Aucune donnée de formulaire n’est affichée dans le dashboard d’observabilité.

---

# 72. Revue de quarantaine

Une vue dédiée peut permettre :

- inspection ;
- acceptation ;
- rejet ;
- fusion ;
- marquage spam.

---

## 72.1 Autorisation

Permission conceptuelle :

```text
Forms.AbuseReview
```

---

## 72.2 Audit

Chaque décision humaine est auditée.

---

## 72.3 Affichage sécurisé

Les contenus sont encodés et les liens ne sont pas activés automatiquement.

---

# 73. Rétention

Les données anti-abus utilisent une rétention adaptée.

---

## 73.1 Compteurs en mémoire

Ils disparaissent au redémarrage.

---

## 73.2 Fingerprints

Les fingerprints persistants utilisent une rétention courte et documentée.

---

## 73.3 Quarantaines

Les quarantaines sont supprimées ou anonymisées après leur période définie.

---

## 73.4 Leads acceptés

Ils suivent la politique normale du module lead.

---

## 73.5 Logs

Les événements anti-abus ne sont pas conservés indéfiniment.

---

# 74. Codes de raison internes

Exemples :

```text
RATE_LIMIT_SOURCE

RATE_LIMIT_GLOBAL

CONCURRENCY_LIMIT

FORM_TOKEN_INVALID

FORM_TOKEN_EXPIRED

SUBMISSION_DUPLICATE

HONEYPOT_FILLED

TOO_FAST

TOO_MANY_LINKS

CHALLENGE_FAILED

CHALLENGE_UNAVAILABLE

PAYLOAD_LIMIT

POLICY_FAILURE
```

---

## 74.1 Réponse publique

Ces codes ne sont pas nécessairement exposés au visiteur.

---

## 74.2 Versionnement

Une règle retirée peut conserver son code historique dans les audits existants.

---

# 75. Réponses HTTP

Mapping recommandé :

| Situation | Statut indicatif |
|---|---:|
| Soumission acceptée | `200` ou `201` |
| Traitement asynchrone accepté | `202` |
| Erreur de validation | `400` ou `422` |
| Token invalide | `400` |
| Challenge requis | `403` ou contrat applicatif dédié |
| Rate limited | `429` |
| Corps trop volumineux | `413` |
| Content-Type interdit | `415` |
| Formulaire désactivé | `503` ou réponse métier contrôlée |

---

## 75.1 Cohérence

Le mapping exact doit rester cohérent dans tous les modules.

---

## 75.2 Pas de détails internes

Les réponses ne révèlent pas :

- score ;
- blacklist ;
- seuil ;
- fingerprint ;
- structure PostgreSQL ;
- provider.

---

# 76. Temps de réponse

Un rejet doit rester rapide.

---

## 76.1 Tarpitting

Ajouter volontairement de longues attentes par requête est déconseillé dans le profil initial.

---

## 76.2 Raisons

Un attaquant peut maintenir de nombreuses connexions et consommer les ressources du serveur.

---

## 76.3 Challenge

Le challenge adaptatif reste préférable à une attente artificielle longue.

---

# 77. Validation de la destination

Un Form ID ne doit pas permettre de choisir librement :

- une adresse email ;
- un webhook ;
- une URL ;
- une queue ;
- un provider.

---

## 77.1 Configuration

Les destinations proviennent d’une configuration approuvée.

---

## 77.2 Prévention

Cette règle prévient notamment :

- SSRF ;
- email bombing ;
- exfiltration ;
- utilisation du site comme relais.

---

# 78. Endpoints génériques

Un endpoint tel que :

```text
POST /api/forms/{arbitraryName}
```

ne doit pas accepter un schéma et une destination entièrement fournis par l’appelant.

---

## 78.1 Form Registry

Chaque Form ID est enregistré dans un registre explicite.

---

## 78.2 Form inconnu

Un Form ID inconnu est refusé avant tout traitement coûteux.

---

# 79. Configuration des champs

Les champs autorisés sont déclarés.

---

## 79.1 Champ inconnu

Un champ supplémentaire est :

- refusé ;
- ou ignoré explicitement selon le contrat.

La baseline privilégie le refus pour les APIs structurées.

---

## 79.2 Mass assignment

La désérialisation ne doit pas permettre de définir :

- statut ;
- rôle ;
- entitlement ;
- destinataire ;
- AbuseStatus ;
- propriété interne.

---

# 80. Uploads publics

Les pièces jointes publiques sont désactivées dans le profil standard.

---

## 80.1 Activation future

Un formulaire avec upload nécessite :

- profil dédié ;
- taille stricte ;
- allowlist ;
- streaming ;
- quarantaine ;
- malware scan ;
- quota ;
- aucune publication directe ;
- ADR-0016.

---

## 80.2 Base64

Un fichier ne doit pas être contourné par un champ Base64 dans un JSON.

---

# 81. Réseaux partagés et NAT

Plusieurs utilisateurs légitimes peuvent partager une adresse.

---

## 81.1 Conséquence

Une limite IP trop stricte peut produire des faux positifs.

---

## 81.2 Compensation

- burst limité ;
- challenge adaptatif ;
- cookie secondaire ;
- quarantaine ;
- support utilisateur ;
- seuils ajustables.

---

## 81.3 Pas d’allowlist large

Une grande entreprise ne doit pas recevoir automatiquement un accès illimité à partir de son IP.

---

# 82. IPv6

Un attaquant peut disposer de nombreuses adresses dans un préfixe.

---

## 82.1 Partition

Une policy peut limiter par préfixe plutôt que par adresse complète.

---

## 82.2 Faux positifs

Le choix du préfixe doit être testé et configurable.

---

## 82.3 Logs

Le préfixe brut ne doit pas devenir un label métrique.

---

# 83. Allowlist et denylist

Une allowlist ou denylist réseau peut être utilisée dans un contexte opérateur.

---

## 83.1 Baseline

Aucune liste globale publique non maintenue ne constitue une dépendance obligatoire.

---

## 83.2 Denylist

Une denylist :

- possède une expiration ;
- une raison ;
- un propriétaire ;
- un audit.

---

## 83.3 Allowlist

Une allowlist ne contourne pas :

- validation ;
- limites globales ;
- taille ;
- idempotence ;
- autorisation.

---

# 84. Blocage temporaire

Des abus répétés peuvent déclencher un blocage temporaire.

---

## 84.1 Identité

Le blocage utilise une source pseudonymisée ou une règle edge.

---

## 84.2 Durée

La durée reste bornée.

---

## 84.3 Blocage permanent

Un blocage permanent par IP est déconseillé sans justification, en raison des réattributions et réseaux partagés.

---

# 85. Fail-open et fail-closed

Chaque dépendance doit définir son comportement.

---

## 85.1 Rate limiter local

Toujours disponible avec le processus.

---

## 85.2 PostgreSQL indisponible

La soumission ne peut pas être considérée comme acceptée.

---

## 85.3 Challenge requis indisponible

Fail-closed.

---

## 85.4 Challenge adaptatif indisponible

Quarantaine ou refus renforcé selon le risque.

---

## 85.5 Observabilité indisponible

Le formulaire peut continuer si les contrôles essentiels restent opérationnels.

---

# 86. Dégradation contrôlée

En cas d’incident partiel, la plateforme peut :

- réduire les limites ;
- exiger le challenge ;
- placer les soumissions en quarantaine ;
- désactiver les notifications ;
- activer le kill switch ;
- désactiver temporairement le formulaire.

---

## 86.1 Interdiction

Elle ne doit pas supprimer silencieusement tous les contrôles anti-abus.

---

# 87. Protection DDoS

L’application protège principalement les ressources applicatives.

---

## 87.1 Limites

Elle ne peut pas à elle seule protéger contre :

- saturation du lien réseau ;
- grand botnet ;
- SYN flood ;
- saturation du VPS avant Caddy ;
- attaque volumétrique majeure.

---

## 87.2 Mesures futures

- protection hébergeur ;
- CDN ;
- WAF ;
- reverse proxy managé ;
- firewall spécialisé ;
- rate limiting edge.

---

## 87.3 Révision

L’introduction d’un edge provider partagé peut nécessiter un ADR distinct.

---

# 88. Tests unitaires

Tester notamment :

- normalisation de Form ID ;
- source partition ;
- IPv4 ;
- IPv6 ;
- rate limit keys ;
- Submission ID ;
- duplicate fingerprint ;
- transitions de décision ;
- Reason Codes ;
- policy version ;
- seuils ;
- réponses.

---

# 89. Tests de validation

Couvrir :

```text
champ manquant

email invalide

téléphone invalide

message trop long

caractères de contrôle

champ inconnu

HTML actif

collection trop grande

JSON trop profond

payload vide
```

---

# 90. Tests de limites HTTP

Vérifier :

- corps à la limite ;
- corps au-dessus ;
- Content-Type valide ;
- Content-Type interdit ;
- multipart interdit ;
- requête lente ;
- connexion interrompue ;
- encodage invalide.

---

# 91. Tests du rate limiter

Tester :

```text
première soumission

burst autorisé

quota dépassé

Retry-After

fenêtre renouvelée

limite globale

limite de concurrence

partition par formulaire

partition par source

queue d’attente bornée
```

---

# 92. Tests des forwarded headers

Simuler :

```text
requête directe avec X-Forwarded-For falsifié

requête via proxy approuvé

chaîne de plusieurs proxies

proxy inconnu

header mal formé

Forward Limit dépassée
```

L’attaquant ne doit pas pouvoir choisir arbitrairement sa partition.

---

# 93. Tests IPv6

Tester :

- représentation compressée ;
- représentation étendue ;
- préfixe identique ;
- préfixe différent ;
- adresse IPv4-mapped ;
- normalisation stable.

---

# 94. Tests d’idempotence

Vérifier :

```text
même Submission ID simultanément

même Submission ID après timeout

même Submission ID après commit

Submission ID différent avec mêmes données

contrainte unique PostgreSQL

un seul message outbox
```

---

# 95. Tests du jeton

Couvrir :

- token valide ;
- signature invalide ;
- token expiré ;
- mauvais Form ID ;
- token rejoué ;
- token d’un autre environnement ;
- token tronqué ;
- token absent.

---

# 96. Tests anti-automatisation

Tester :

- honeypot vide ;
- honeypot rempli ;
- soumission rapide ;
- durée normale ;
- nombreux liens ;
- contenu répété ;
- User-Agent absent ;
- JavaScript absent ;
- signaux combinés.

---

## 96.1 Faux positifs

Tester également des cas légitimes :

- autofill ;
- lecteur d’écran ;
- navigation clavier ;
- réseau d’entreprise ;
- second envoi après correction ;
- navigateur sans JavaScript.

---

# 97. Tests du challenge

Couvrir :

```text
mode disabled

mode adaptive

mode required

token valide

token invalide

token expiré

hostname incorrect

action incorrecte

provider indisponible

timeout

rate limit provider

credential absent
```

---

# 98. Tests de quarantaine

Vérifier :

- persistance ;
- absence de notification ;
- permission de revue ;
- acceptation humaine ;
- rejet humain ;
- fusion de doublon ;
- audit ;
- rétention.

---

# 99. Tests des notifications

Vérifier :

```text
accepted
→ un job idempotent

duplicate
→ aucun second job

quarantine
→ aucune notification commerciale

reject
→ aucun job

rate-limited
→ aucun job
```

---

# 100. Tests d’autoresponder

Lorsqu’il est activé :

- limitation par destinataire ;
- idempotence ;
- absence d’open relay ;
- contenu fixe ;
- aucun secret ;
- doublon ;
- email bombing ;
- provider indisponible.

---

# 101. Tests CORS

Un test HTTP direct doit prouver qu’un client non navigateur peut envoyer la requête malgré une politique CORS.

---

## 101.1 Objectif

Empêcher toute fausse hypothèse selon laquelle CORS bloque les bots.

---

# 102. Tests de protection des logs

Injecter :

- retours à la ligne ;
- HTML ;
- secrets factices ;
- longues valeurs ;
- User-Agent volumineux.

Vérifier :

- aucune injection de log ;
- aucune PII ;
- aucune valeur non bornée ;
- aucun token.

---

# 103. Tests PostgreSQL

Vérifier :

- contrainte d’idempotence ;
- concurrence ;
- transaction lead/outbox ;
- rollback ;
- quarantaines ;
- fingerprints ;
- index ;
- rétention.

---

# 104. Tests de panne

Simuler :

- PostgreSQL arrêté ;
- worker arrêté ;
- challenge arrêté ;
- Caddy mal configuré ;
- Data Protection Keys indisponibles ;
- queue saturée ;
- observabilité indisponible.

---

## 104.1 Résultat

Aucune panne ne doit conduire à une acceptation fictive ou une duplication incontrôlée.

---

# 105. Tests multi-instance futurs

Même si le profil initial est mono-instance, les tests ou la documentation doivent rendre visible que les compteurs locaux ne sont pas globaux.

---

## 105.1 Interdiction

Ne pas activer plusieurs instances sans adapter la limitation de débit.

---

# 106. Tests de sécurité

Vérifier notamment :

```text
X-Forwarded-For falsifié

Mass assignment

Payload excessif

JSON bomb

Regex excessive

HTML actif

SSRF par destination

Email bombing

Replay

Double submit

Bypass du challenge

Contournement CORS

Form ID arbitraire

Endpoint direct sans Caddy
```

---

# 107. Tests de performance

Mesurer :

- coût de partitionnement ;
- coût de validation ;
- coût du fingerprint ;
- latence PostgreSQL ;
- concurrence ;
- allocation ;
- impact d’une rafale ;
- volume de logs ;
- backlog worker.

---

# 108. Tests multiplateformes

Les règles fonctionnelles sont testées sur :

```text
Windows x64

Linux x64
```

Les tests de proxy, Caddy, Docker et réseau sont validés sur Linux.

---

# 109. Quality gates

Une fonctionnalité de formulaire public est bloquée lorsque :

```text
La validation importante est uniquement côté client.

Aucun rate limiting n’est appliqué.

Le corps de requête n’est pas borné.

Le Content-Type n’est pas contrôlé.

X-Forwarded-For est accepté depuis n’importe quelle source.

CORS est présenté comme contrôle anti-abus.

La soumission n’est pas idempotente.

Une soumission rate-limited crée un lead.

Une quarantaine déclenche une notification ordinaire.

Un autoresponder est activé sans limite par destinataire.

Le token de challenge est validé uniquement côté client.

Une IP brute est utilisée comme label métrique.

Un endpoint permet de choisir une destination arbitraire.

Un upload public est accepté sans profil renforcé.
```

---

# 110. Quality gates de production

La production est bloquée lorsque :

```text
L’application est accessible directement depuis Internet.

Les proxies approuvés ne sont pas configurés.

Les limites de requête sont absentes.

Le profil anti-abus est inconnu.

Le credential de challenge requis manque.

La policy de failover du challenge est indéfinie.

Le kill switch n’est pas disponible.

Le worker de notification est incompatible.

Les métriques ou alertes essentielles sont absentes.

Le formulaire utilise un profil de développement.
```

---

# 111. Codes d’erreur recommandés

| Code | Signification |
|---|---|
| `SF-FORM-001` | Formulaire inconnu |
| `SF-FORM-002` | Formulaire désactivé |
| `SF-FORM-003` | Formulaire non disponible dans cet environnement |
| `SF-FORM-004` | Schéma de formulaire invalide |
| `SF-FORM-005` | Champ obligatoire absent |
| `SF-FORM-006` | Champ inconnu |
| `SF-FORM-007` | Valeur de champ invalide |
| `SF-FORM-008` | Valeur de champ trop longue |
| `SF-FORM-009` | Corps de requête vide |
| `SF-FORM-010` | Corps de requête trop volumineux |
| `SF-FORM-011` | Content-Type non supporté |
| `SF-FORM-012` | Encodage invalide |
| `SF-FORM-013` | Contenu actif interdit |
| `SF-FORM-014` | Soumission impossible à persister |
| `SF-FORM-015` | Notification impossible à planifier |
| `SF-FORM-TOKEN-001` | Jeton de formulaire absent |
| `SF-FORM-TOKEN-002` | Jeton de formulaire invalide |
| `SF-FORM-TOKEN-003` | Jeton de formulaire expiré |
| `SF-FORM-TOKEN-004` | Form ID du jeton incompatible |
| `SF-FORM-TOKEN-005` | Replay détecté |
| `SF-FORM-TOKEN-006` | Jeton d’un autre environnement |
| `SF-FORM-SUBMISSION-001` | Submission ID absent |
| `SF-FORM-SUBMISSION-002` | Submission ID invalide |
| `SF-FORM-SUBMISSION-003` | Submission ID déjà traité |
| `SF-FORM-SUBMISSION-004` | Conflit d’idempotence |
| `SF-RATE-001` | Limite globale atteinte |
| `SF-RATE-002` | Limite par source atteinte |
| `SF-RATE-003` | Limite par formulaire atteinte |
| `SF-RATE-004` | Limite de concurrence atteinte |
| `SF-RATE-005` | Partition de source invalide |
| `SF-RATE-006` | Proxy non approuvé |
| `SF-RATE-007` | Forwarded header invalide |
| `SF-RATE-008` | Configuration de rate limiting invalide |
| `SF-ABUSE-001` | Policy anti-abus inconnue |
| `SF-ABUSE-002` | Policy anti-abus invalide |
| `SF-ABUSE-003` | Honeypot rempli |
| `SF-ABUSE-004` | Soumission trop rapide |
| `SF-ABUSE-005` | Trop de liens |
| `SF-ABUSE-006` | Contenu répétitif |
| `SF-ABUSE-007` | Duplicate fingerprint détecté |
| `SF-ABUSE-008` | Soumission mise en quarantaine |
| `SF-ABUSE-009` | Soumission rejetée |
| `SF-ABUSE-010` | Source temporairement bloquée |
| `SF-ABUSE-011` | Évaluation anti-abus échouée |
| `SF-CHALLENGE-001` | Challenge requis |
| `SF-CHALLENGE-002` | Token de challenge absent |
| `SF-CHALLENGE-003` | Token de challenge invalide |
| `SF-CHALLENGE-004` | Token de challenge expiré |
| `SF-CHALLENGE-005` | Hostname du challenge invalide |
| `SF-CHALLENGE-006` | Action du challenge invalide |
| `SF-CHALLENGE-007` | Provider de challenge indisponible |
| `SF-CHALLENGE-008` | Credential de challenge absent |
| `SF-CHALLENGE-009` | Provider de challenge interdit |
| `SF-CHALLENGE-010` | Politique d’accessibilité insuffisante |

---

# 112. Audit

Les opérations suivantes doivent être auditables :

- changement de profil anti-abus ;
- modification des seuils de production ;
- activation du challenge ;
- désactivation du challenge ;
- activation du kill switch ;
- ajout d’une denylist ;
- revue de quarantaine ;
- acceptation d’un faux positif ;
- marquage spam ;
- activation d’un autoresponder.

---

## 112.1 Valeurs sensibles

L’audit ne contient pas :

- contenu intégral ;
- token ;
- adresse IP brute ;
- credential ;
- message du lead.

---

# 113. Runbooks

Les procédures doivent couvrir :

```text
Pic soudain de spam

Provider de challenge indisponible

Faux positifs nombreux

Backlog de notifications

Formulaire à désactiver

Attaque distribuée

Adresse ou préfixe abusif

Caddy mal configuré

Forwarded headers falsifiés

Autoresponder utilisé pour du spam
```

---

# 114. Réponse à un pic de spam

Flux recommandé :

```text
confirmer le signal
→ identifier le formulaire
→ vérifier les métriques
→ limiter davantage le débit
→ activer le challenge adaptatif ou obligatoire
→ suspendre les notifications
→ activer la quarantaine
→ activer le kill switch si nécessaire
→ analyser
→ corriger
→ restaurer progressivement
```

---

## 114.1 Suppression massive

Les leads ne doivent pas être supprimés automatiquement sur la seule base d’un pic.

Une procédure de classification et de revue est nécessaire.

---

# 115. Règles pour les modules

Un module exposant un formulaire public doit déclarer :

```text
FormId

SchemaVersion

ProtectionProfileId

RequestBodyLimit

FieldLimits

SubmissionPolicy

NotificationPolicy

ChallengeSupport

KillSwitch

Capabilities

ReasonCodes
```

---

## 115.1 Destination

La destination métier est déclarée dans le module ou sa configuration.

Elle n’est pas fournie par la requête.

---

## 115.2 Tests

Le module fournit les tests de validation et d’abus correspondant à son profil.

---

# 116. Règles pour les customizations

Une customization peut :

- ajouter des validations ;
- ajouter des signaux ;
- définir un formulaire client ;
- ajouter un challenge approuvé.

---

## 116.1 Interdictions

Elle ne doit pas :

- désactiver le rate limiting ;
- faire confiance à un header client ;
- ajouter une destination arbitraire ;
- envoyer un email directement dans la requête ;
- journaliser le contenu ;
- accepter un upload sans profil ;
- contourner le kill switch.

---

# 117. Règles pour LogicGate

LogicGate doit :

- définir les profils ;
- calibrer les seuils ;
- surveiller les métriques ;
- traiter la quarantaine ;
- maintenir les runbooks ;
- protéger les forwarded headers ;
- surveiller les fournisseurs ;
- vérifier les faux positifs ;
- documenter les données transmises.

---

## 117.1 LogicGate ne doit pas

- considérer un CAPTCHA comme protection complète ;
- considérer CORS comme protection ;
- conserver indéfiniment des IP brutes ;
- activer un autoresponder sans analyse ;
- exposer le score anti-abus ;
- rendre l’application directement accessible ;
- modifier les seuils sans rapport.

---

# 118. Règles pour Codex

Codex peut :

- implémenter les policies ;
- ajouter le rate limiting ;
- créer les validateurs ;
- créer les tests ;
- ajouter les métriques ;
- ajouter le challenge derrière une abstraction ;
- produire les runbooks.

---

## 118.1 Codex ne doit pas

```text
Présenter CORS comme protection

Faire confiance à X-Forwarded-For sans Known Proxy

Désactiver le rate limiting

Ajouter un CAPTCHA obligatoire sans décision

Envoyer un email directement dans l’endpoint

Logguer un formulaire complet

Stocker une IP brute dans une métrique

Créer un endpoint générique de formulaire libre

Ajouter un upload public sans profil sécurisé

Modifier les seuils de production

Activer un kill switch réel

Supprimer des leads réels
```

---

## 118.2 Décision humaine

Une décision humaine est requise pour :

- nouveau provider de challenge ;
- transmission d’une nouvelle donnée externe ;
- modification forte des seuils ;
- activation obligatoire du challenge ;
- autoresponder ;
- upload public ;
- blocage réseau durable ;
- purge de quarantaines ;
- ajout d’un WAF.

---

# 119. Anti-patterns interdits

## 119.1 CORS comme anti-bot

```text
Le domaine externe est bloqué par CORS,
donc le formulaire est protégé.
```

---

## 119.2 Validation JavaScript uniquement

Le serveur accepte tout payload correctement sérialisé.

---

## 119.3 Confiance aveugle dans `X-Forwarded-For`

L’attaquant choisit une nouvelle IP à chaque requête.

---

## 119.4 CAPTCHA sans rate limiting

Chaque requête consomme le provider avant toute limite locale.

---

## 119.5 Email dans la requête

Le temps de réponse dépend du SMTP.

---

## 119.6 Autoresponder libre

Une adresse arbitraire reçoit un email à chaque requête.

---

## 119.7 Honeypot comme seule protection

Un champ caché constitue toute la stratégie anti-spam.

---

## 119.8 Log du message

Chaque spam est copié dans le backend d’observabilité.

---

## 119.9 IP comme label

Une série métrique est créée pour chaque adresse source.

---

## 119.10 Endpoint dynamique

L’appelant choisit le destinataire ou le webhook.

---

## 119.11 Limite après persistance

Le lead et l’email sont créés avant de vérifier le quota.

---

## 119.12 Blocage permanent automatique

Une adresse partagée est bloquée définitivement après quelques erreurs.

---

# 120. Sécurité

## 120.1 Actifs

- disponibilité ;
- PostgreSQL ;
- worker ;
- SMTP ;
- leads ;
- données personnelles ;
- réputation du domaine ;
- quotas fournisseur ;
- logs ;
- VPS.

---

## 120.2 Menaces

- spam ;
- bot ;
- email bombing ;
- DDoS applicatif ;
- headers falsifiés ;
- mass assignment ;
- payload bomb ;
- replay ;
- double submit ;
- XSS stockée ;
- SSRF ;
- saturation de queue ;
- exfiltration.

---

## 120.3 Contrôles

- reverse proxy ;
- forwarded headers approuvés ;
- limites ;
- rate limiting ;
- idempotence ;
- validation ;
- challenge ;
- quarantaine ;
- outbox ;
- secrets séparés ;
- observabilité ;
- kill switch ;
- tests.

---

# 121. Threat model simplifié

| Menace | Contrôle principal |
|---|---|
| Robot simple | Honeypot, timing et rate limiting |
| Script HTTP direct | Validation et rate limiting serveur |
| Header IP falsifié | Known Proxies et application non exposée |
| Double clic | Submission ID et contrainte unique |
| Email bombing | Autoresponder désactivé ou limité |
| Spam distribué | Limite globale, challenge et quarantaine |
| Payload massif | Body limit avant désérialisation |
| XSS stockée | Validation, encodage et sanitization |
| Destination arbitraire | Form Registry et configuration approuvée |
| Provider challenge indisponible | Failover défini par profil |
| Saturation worker | Idempotence, concurrence et backlog monitoring |
| DDoS volumétrique | Protection hébergeur ou edge future |

---

# 122. Conséquences positives

La décision apporte :

- réduction du spam ;
- protection des ressources ;
- moins de doublons ;
- moins d’emails abusifs ;
- meilleure qualité des leads ;
- visibilité opérationnelle ;
- challenge facultatif ;
- meilleure accessibilité ;
- réponse rapide aux incidents ;
- séparation entre sécurité et CORS ;
- idempotence ;
- compatibilité avec les jobs persistants ;
- possibilité d’ajouter un edge provider.

---

# 123. Conséquences négatives

La décision entraîne :

- configuration supplémentaire ;
- state anti-abus ;
- tests réseau ;
- risque de faux positifs ;
- maintenance de policies ;
- gestion de quarantaine ;
- provider éventuel ;
- métriques et alertes ;
- besoin de calibrage ;
- complexité des forwarded headers ;
- procédures d’incident.

---

# 124. Risques acceptés

## 124.1 Faux positifs

Une soumission légitime peut être mise en quarantaine ou limitée.

Réduction :

- plusieurs signaux ;
- challenge ;
- revue ;
- seuils ajustables ;
- alternative de contact.

---

## 124.2 Compteurs perdus au redémarrage

Le rate limiter in-process réinitialise ses compteurs.

Ce risque est accepté pour le profil mono-instance.

---

## 124.3 Contournement distribué

Une attaque distribuée peut contourner les limites par source.

Réduction :

- limite globale ;
- challenge ;
- kill switch ;
- edge protection future.

---

## 124.4 Dépendance challenge

Lorsqu’il est activé, le provider peut être indisponible.

La policy doit définir une dégradation contrôlée.

---

# 125. Mesures compensatoires

- limites globales ;
- rate limiting partitionné ;
- concurrence bornée ;
- Submission ID ;
- duplicate fingerprint ;
- antiforgery ;
- jeton signé ;
- honeypot ;
- timing ;
- challenge adaptatif ;
- quarantaine ;
- jobs idempotents ;
- métriques ;
- alertes ;
- kill switch ;
- WAF futur.

---

# 126. Performance

## 126.1 Ordre des contrôles

Les contrôles les moins coûteux sont exécutés en premier.

Ordre indicatif :

```text
route
→ taille
→ rate limit
→ token
→ validation
→ scoring
→ challenge
→ PostgreSQL
```

---

## 126.2 Fingerprint

Le calcul reste borné et n’analyse pas un contenu non limité.

---

## 126.3 Regex

Les expressions régulières doivent éviter les comportements catastrophiques.

---

## 126.4 Challenge

Le provider externe n’est appelé qu’après les contrôles locaux.

---

# 127. Coûts

## 127.1 Coûts supplémentaires

- développement ;
- challenge éventuel ;
- observabilité ;
- quarantaine ;
- support ;
- edge futur.

---

## 127.2 Coûts évités

- emails inutiles ;
- stockage spam ;
- temps de qualification ;
- saturation PostgreSQL ;
- incidents SMTP ;
- coûts CRM ;
- réputation du domaine dégradée ;
- indisponibilité.

---

# 128. Maintenabilité

Pour chaque formulaire public, il doit être possible de répondre :

```text
Quel est son Form ID ?

Quel schéma accepte-t-il ?

Quel profil de protection utilise-t-il ?

Quelles limites sont appliquées ?

Comment l’adresse source est-elle résolue ?

Comment les doublons sont-ils évités ?

Un challenge est-il actif ?

Quelle est la politique de quarantaine ?

Quels emails peuvent être déclenchés ?

Quel kill switch le désactive ?

Quels dashboards et runbooks le couvrent ?
```

---

# 129. Compatibilité

## 129.1 Profils

Les profils possèdent une version.

---

## 129.2 Form tokens

Le format du jeton possède une version ou un purpose stable.

---

## 129.3 Policies

Une nouvelle version de policy ne doit pas modifier silencieusement la production.

---

## 129.4 Multi-instance

Le passage à plusieurs instances nécessitera une stratégie de rate limiting partagée.

---

## 129.5 Breaking change

```text
Breaking change expected: no
```

pour les nouveaux projets, puisque cette décision constitue la baseline.

L’ajout de jetons ou d’idempotence à un ancien formulaire peut nécessiter une adaptation du front-end.

---

# 130. Migration depuis un formulaire non protégé

Le processus est :

```text
inventorier les endpoints
→ définir les Form IDs
→ définir les limites
→ ajouter validation serveur
→ ajouter rate limiting
→ ajouter Submission ID
→ ajouter antiforgery
→ ajouter les signaux
→ séparer les emails en jobs
→ ajouter les métriques
→ tester
→ activer progressivement
```

---

## 130.1 Données historiques

Les anciens doublons peuvent être identifiés séparément.

Ils ne doivent pas être supprimés automatiquement.

---

# 131. Migration depuis un CAPTCHA obligatoire

Le processus peut être :

```text
mesurer le trafic
→ ajouter les contrôles locaux
→ définir une policy adaptative
→ tester les faux positifs
→ réduire progressivement le challenge
→ surveiller
```

---

## 131.1 Retour arrière

Le mode `required` reste disponible en cas d’incident.

---

# 132. Migration vers un WAF ou edge provider

Le processus futur devra préserver :

- Form IDs ;
- limites applicatives ;
- idempotence ;
- validation ;
- challenge ;
- logs ;
- kill switch.

---

## 132.1 Edge

Le WAF ajoute une couche.

Il ne remplace pas les contrôles applicatifs.

---

## 132.2 Forwarded headers

La chaîne de proxies et les sources approuvées devront être révisées.

---

# 133. Transfert client

Le handover doit inclure :

- Form IDs ;
- profils ;
- seuils ;
- provider challenge ;
- credentials à recréer ;
- forwarded headers ;
- Caddy ;
- métriques ;
- alertes ;
- quarantaines ;
- runbooks ;
- politiques de rétention ;
- kill switches.

---

## 133.1 Compte fournisseur

Un provider challenge doit utiliser un compte ou credential transférable ou recréé pour le client.

---

# 134. Clôture de service

La clôture peut inclure :

- traitement des quarantaines ;
- export des leads ;
- retrait du challenge LogicGate ;
- révocation des credentials ;
- transfert des policies ;
- retrait des alertes ;
- conservation ou suppression des fingerprints selon la politique.

---

# 135. Production

```text
Production execution: out of scope
```

Cet ADR ne constitue pas une autorisation :

- de bloquer une adresse réelle ;
- de modifier des seuils ;
- d’activer un CAPTCHA ;
- de supprimer des leads ;
- d’activer un kill switch ;
- de changer Caddy ;
- de purger une quarantaine de production.

---

# 136. Réversibilité

## 136.1 Classification

```text
élevée pour les seuils et providers

moyenne pour les jetons et contrats front-end

faible pour les emails ou effets déjà déclenchés
```

---

## 136.2 Challenge

Le provider peut être remplacé derrière le contrat public.

---

## 136.3 Quarantaine

Une soumission peut être reclassée tant qu’elle est conservée.

---

# 137. Plan d’implémentation

## Étape 1 — Contrats

Définir :

- `FormId` ;
- `SubmissionId` ;
- `AbuseDecision` ;
- `AbuseReasonCode` ;
- `ProtectionProfileId`.

---

## Étape 2 — Forwarded headers

Configurer :

- Caddy ;
- Known Proxies ;
- Forward Limit ;
- tests de spoofing.

---

## Étape 3 — Limites HTTP

Ajouter :

- body size ;
- Content-Type ;
- champs ;
- collections ;
- timeouts.

---

## Étape 4 — Rate limiting

Ajouter :

- global ;
- par source ;
- par formulaire ;
- concurrence ;
- `Retry-After`.

---

## Étape 5 — Jeton et idempotence

Ajouter :

- Form Token ;
- Submission ID ;
- contrainte PostgreSQL ;
- retry idempotent.

---

## Étape 6 — Signaux

Ajouter :

- honeypot ;
- timing ;
- liens ;
- doublons ;
- scoring versionné.

---

## Étape 7 — Quarantaine

Créer :

- statut ;
- stockage ;
- permissions ;
- revue ;
- rétention.

---

## Étape 8 — Challenge facultatif

Créer :

- contrat provider ;
- mode disabled/adaptive/required ;
- validation serveur ;
- failover.

---

## Étape 9 — Notifications

Déplacer les effets dans l’outbox et les jobs.

---

## Étape 10 — Opérations

Ajouter :

- métriques ;
- alertes ;
- dashboard ;
- kill switch ;
- runbooks.

---

# 138. Tâches liées proposées

```text
SF-TASK-204 — Define public form protection profiles

SF-TASK-205 — Add trusted forwarded-header configuration

SF-TASK-206 — Add public form request-size limits

SF-TASK-207 — Add partitioned public-form rate limiting

SF-TASK-208 — Add public-form concurrency limiting

SF-TASK-209 — Add signed public-form submission tokens

SF-TASK-210 — Add idempotent submission identifiers

SF-TASK-211 — Add deterministic abuse signals and scoring

SF-TASK-212 — Add public-form quarantine workflow

SF-TASK-213 — Define provider-neutral human verification contracts

SF-TASK-214 — Add optional adaptive challenge verification

SF-TASK-215 — Move public-form notifications to the outbox

SF-TASK-216 — Add abuse-protection metrics and alerts

SF-TASK-217 — Add forwarded-header spoofing tests

SF-TASK-218 — Add public-form abuse and accessibility tests

SF-TASK-219 — Add public-form incident runbooks
```

Les identifiants définitifs seront attribués lors de la création du backlog.

---

# 139. Tests et preuves attendus

Pour considérer cette décision correctement implémentée :

```text
La validation serveur est toujours exécutée.

Le corps de requête est borné.

Les Content-Types sont allowlistés.

L’application n’est pas accessible directement depuis Internet.

Les forwarded headers ne sont acceptés que depuis Caddy.

Un X-Forwarded-For falsifié ne change pas la partition.

Le rate limiting global fonctionne.

Le rate limiting par source fonctionne.

Le rate limiting par formulaire fonctionne.

La concurrence est bornée.

Une réponse 429 contient un comportement contrôlé.

Une soumission rate-limited ne crée aucun lead.

Le Submission ID rend un retry idempotent.

Un double clic ne crée qu’un seul lead.

Le jeton est lié au Form ID et à l’environnement.

L’antiforgery est utilisé sans être considéré comme anti-bot.

CORS n’est utilisé dans aucune décision d’abus.

Le honeypot n’est pas l’unique motif universel de rejet.

Le challenge est validé côté serveur.

Une panne du challenge suit la policy prévue.

Une quarantaine ne déclenche aucun email métier.

Un lead accepté crée un seul message outbox.

Un rejet ne crée aucun job.

L’autoresponder est désactivé par défaut.

Aucune IP brute n’est un label métrique.

Les logs ne contiennent aucun contenu de formulaire.

Le kill switch permet de suspendre le formulaire.

Les tests couvrent l’accessibilité et les réseaux partagés.

Les tests fonctionnent sur Windows et Linux.
```

---

# 140. Conditions de révision

Réexaminer cette décision si :

- plusieurs instances web deviennent la norme ;
- un CDN ou WAF devient obligatoire ;
- les attaques distribuées deviennent fréquentes ;
- les uploads publics deviennent standards ;
- les faux positifs dépassent un seuil acceptable ;
- le challenge adaptatif devient trop coûteux ;
- un service central de réputation est introduit ;
- un moteur de détection statistique est proposé ;
- les obligations réglementaires sur les IP évoluent ;
- le profil VPS est remplacé ;
- les formulaires deviennent des APIs publiques partenaires ;
- une authentification forte remplace certains formulaires anonymes.

---

# 141. Indicateurs à suivre

- soumissions par formulaire ;
- acceptations ;
- quarantaines ;
- rejets ;
- rate limits ;
- doublons ;
- challenges ;
- échecs de challenge ;
- faux positifs ;
- temps de traitement ;
- backlog de notifications ;
- taille des quarantaines ;
- coût du provider ;
- volume PostgreSQL ;
- formulaire désactivé ;
- incidents de forwarded headers ;
- attaques distribuées détectées.

---

# 142. Signaux d’alerte

La décision ou son implémentation doit être renforcée si :

```text
des bots contournent facilement les limites

les faux positifs augmentent fortement

le provider challenge devient indispensable

des IP brutes apparaissent dans les métriques

des soumissions rate-limited créent des leads

des doublons déclenchent plusieurs emails

les quarantaines ne sont jamais revues

l’application est exposée directement

les forwarded headers sont acceptés globalement

les logs grossissent pendant les attaques

le worker est saturé par les formulaires

CORS est encore présenté comme contrôle de sécurité
```

---

# 143. Conséquences observées

Cette section est append-only après mise en œuvre.

## 143.1 État initial

```text
Aucune conséquence observée à la date d’acceptation.

La décision précède l’implémentation complète
des profils de protection, du rate limiting
et de la quarantaine des formulaires publics.
```

Les observations futures devront préciser :

- date ;
- version SiteFactory ;
- Form ID ;
- profil ;
- trafic ;
- décision ;
- faux positifs ;
- incident ou bénéfice ;
- action proposée.

---

# 144. Références

- [README.md](../../README.md)
- [AGENTS.md](../../AGENTS.md)
- [0031-application-startup-validation-and-runtime-compatibility.md](./0031-application-startup-validation-and-runtime-compatibility.md)
- Les profils et types de formulaires sont définis de manière autonome dans les sections 16 et 17 du présent ADR ; aucun catalogue de pages privé n'est requis.
- [core-module-boundary.md](../02-architecture/core-module-boundary.md)
- [10-feature-flags-and-entitlements.md](../02-architecture/10-feature-flags-and-entitlements.md)
- [0023-data-retention-deletion-and-privacy-lifecycle.md](./0023-data-retention-deletion-and-privacy-lifecycle.md)
- [0020-administrator-session-security-and-step-up-authentication.md](./0020-administrator-session-security-and-step-up-authentication.md)
- [13-generation-pipeline.md](../03-generation/13-generation-pipeline.md)
- [14-client-configuration-schema.md](../03-generation/14-client-configuration-schema.md)
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
- [0003-postgresql-as-primary-database.md](./0003-postgresql-as-primary-database.md)
- [0004-explicit-production-migrations.md](./0004-explicit-production-migrations.md)
- [0006-feature-flags-and-entitlements-separation.md](./0006-feature-flags-and-entitlements-separation.md)
- [0007-module-composition-and-public-contracts.md](./0007-module-composition-and-public-contracts.md)
- [0009-client-customizations-and-extension-points.md](./0009-client-customizations-and-extension-points.md)
- [0010-structured-observability-with-opentelemetry.md](./0010-structured-observability-with-opentelemetry.md)
- [0011-docker-compose-vps-deployment-profile.md](./0011-docker-compose-vps-deployment-profile.md)
- [0012-runtime-configuration-and-secrets.md](./0012-runtime-configuration-and-secrets.md)
- [0014-explicit-client-upgrades-and-version-pinning.md](./0014-explicit-client-upgrades-and-version-pinning.md)
- [0015-deterministic-generation-and-optional-ai.md](./0015-deterministic-generation-and-optional-ai.md)
- [0016-content-and-media-storage-boundaries.md](./0016-content-and-media-storage-boundaries.md)
- [0017-background-jobs-and-reliable-side-effects.md](./0017-background-jobs-and-reliable-side-effects.md)

---

# 145. Décision humaine

| Élément | Valeur |
|---|---|
| Décideur | LogicGate |
| Date | 2026-07-29 |
| Résultat | Accepté |
| Version cible | SiteFactory `0.1.0` |
| Révision automatique | Aucune |
| Révision conditionnelle | Selon les déclencheurs de la section 140 |

---

# 146. Historique

| Date | Changement |
|---|---|
| 2026-07-29 | Proposition initiale |
| 2026-07-29 | Décision acceptée comme baseline du MVP |

---

# 147. Synthèse de la décision

```text
Internet
→ Caddy
→ forwarded headers approuvés
→ application non exposée directement

Requête
→ taille bornée
→ Content-Type autorisé
→ rate limit global
→ rate limit par source
→ concurrence bornée

Formulaire
→ Form ID stable
→ token signé
→ Submission ID
→ antiforgery
→ validation serveur

Idempotence
→ contrainte PostgreSQL
→ retry sans duplication
→ un seul lead
→ un seul message outbox

Anti-automatisation
→ honeypot
→ timing
→ liens
→ fingerprint
→ règles déterministes
→ aucun signal unique considéré comme preuve

Décision
→ allow
→ challenge-required
→ quarantine
→ reject
→ rate-limited

Challenge
→ facultatif
→ provider-agnostic
→ validation serveur
→ accessibilité
→ failover explicite

Effets
→ lead et outbox dans la même transaction
→ notifications par worker
→ aucune notification pour reject ou quarantine

Protection des données
→ IP brute non journalisée par défaut
→ fingerprint pseudonymisé
→ aucune PII dans les métriques
→ rétention bornée

Incident
→ limites renforcées
→ challenge
→ quarantaine
→ suspension des notifications
→ kill switch

Limite
→ CORS ne protège pas le formulaire
→ l’application ne remplace pas une protection DDoS edge
```

> Un formulaire public ne doit pas seulement vérifier que les données ont le bon format. Il doit également contrôler combien de ressources une source peut consommer, empêcher les doublons, différer les effets externes et rester exploitable lorsqu’un attaquant imite parfaitement un navigateur légitime.
