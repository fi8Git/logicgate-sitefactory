# LogicGate SiteFactory — Déploiement et environnements

## Informations du document

| Élément | Valeur |
|---|---|
| Produit | LogicGate SiteFactory |
| Document | Déploiement et environnements |
| Version | 1.0 |
| Statut | Contrat cible ; périmètre public livré précisé ci-dessous |
| Propriétaire | LogicGate |
| Public principal | LogicGate, développeurs, opérateurs, contributeurs, Codex |
| Profil de déploiement initial | `docker-compose-vps` |
| Environnements | Development, Staging, Production |
| Infrastructure cible | VPS Linux, Docker Compose, Caddy, PostgreSQL |
| Documents parents | `../../README.md`, `../../AGENTS.md`, `../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md`, `../02-architecture/core-module-boundary.md`, `../adr/0023-data-retention-deletion-and-privacy-lifecycle.md`, `../adr/0028-database-transaction-boundaries-and-concurrency-control.md`, `../adr/0020-administrator-session-security-and-step-up-authentication.md`, `../adr/0021-administrator-provisioning-invitations-and-account-recovery.md`, `13-generation-pipeline.md`, `14-client-configuration-schema.md`, `15-cli-specification.md`, `18-security-baseline.md`, `19-testing-strategy.md`, `20-quality-gates-and-ci.md`, `21-observability-and-logging.md` |
| Dernière mise à jour | 2026-10-21 |

---

## Périmètre livré et limites opérationnelles

Le runtime public valide explicitement Development/Test/Staging/Production et
sa configuration ; le Migrator est limité à Development/Test sur une base existante.
La valeur structurelle `docker-compose-vps` est reconnue dans la V1 mais ne
déploie rien. Aucun `sitefactory deploy`, image applicative cliente, Compose/Caddy
client, provisioning de rôles production ou orchestration de déploiement n'est livré.

Le [harnais public](../../tests/PostgreSql/README.md) provisionne uniquement un
PostgreSQL 18.6 **éphémère de test**, en TLS VerifyFull et loopback dynamique.
Ses preuves Windows .NET vers PostgreSQL Linux ne prouvent ni un déploiement
client ni la CI distante, toujours non validée.

Les prescriptions suivantes constituent le **profil cible de déploiement client**.
Les intégrer à une application exige une décision et des preuves distinctes ;
elles ne décrivent pas un service public déjà opéré. Les outils, chemins et
commandes cibles sont illustratifs et ne doivent pas être exécutés comme des
commandes disponibles du produit. Aucun secret, environnement de production
ou compte administrateur par défaut ne doit être déduit de ces exemples.

### Lecture des sections cibles

Les sections numérotées conservent les décisions techniques normatives, notamment
les restrictions de sécurité. Elles ne constituent pas une liste de fonctionnalités
livrées. Les identifiants `sample-*` sont fictifs, syntaxiquement compatibles avec
les contrats publics, mais ne désignent aucun module ou template enregistré.
La publication et les packages restent à réaliser selon ADR-0033 à ADR-0039.

---

## 1. Objet du contrat cible

Ce document définit la stratégie de déploiement et de gestion des environnements des applications générées avec LogicGate SiteFactory.

Il précise notamment :

- les environnements supportés ;
- leurs responsabilités ;
- les règles d’isolation ;
- la configuration runtime ;
- la gestion des secrets ;
- le profil Docker Compose ;
- l’utilisation de Caddy ;
- l’utilisation de PostgreSQL ;
- la construction et la promotion des images ;
- la préparation des migrations ;
- la procédure de déploiement ;
- les health checks ;
- les smoke tests ;
- les sauvegardes avant déploiement ;
- les rollbacks ;
- la gestion des volumes ;
- la coexistence de plusieurs clients sur un VPS ;
- les déploiements de staging et de production ;
- la traçabilité des versions ;
- les procédures d’urgence ;
- les contrôles de sécurité ;
- les tests attendus ;
- les contraintes applicables à Codex.

Le déploiement reste distinct de la génération.

La commande :

```text
sitefactory generate
```

ne doit jamais déployer automatiquement une application en production.

---

## 2. Résumé architectural

Le profil initial est :

```text
docker-compose-vps
```

Il repose sur :

```text
Un repository par client

Une application ASP.NET Core par client

Une base PostgreSQL par client

Des volumes persistants par client

Un réseau Docker isolé par projet

Caddy comme reverse proxy et terminaison HTTPS

Des secrets fournis par l’environnement

Des migrations appliquées explicitement

Des sauvegardes avant modification des données

Des health checks et smoke tests

Un rollback documenté
```

Vue conceptuelle :

```text
Internet
   │
   ▼
Caddy
   │
   ▼
Application ASP.NET Core
   │
   ├── PostgreSQL
   ├── App_Data
   └── Data Protection Keys
```

---

## 3. Objectifs

La stratégie doit favoriser :

- l’isolation des clients ;
- la reproductibilité ;
- la sécurité ;
- la simplicité opérationnelle ;
- la traçabilité ;
- la testabilité ;
- la restauration ;
- la maîtrise des migrations ;
- la réduction des différences entre environnements ;
- la capacité de rollback ;
- la limitation des manipulations manuelles ;
- l’exploitation par une équipe réduite.

---

## 4. Non-objectifs du MVP

Le MVP ne cherche pas à fournir :

- Kubernetes ;
- un orchestrateur distribué ;
- un déploiement multi-région ;
- un cluster PostgreSQL ;
- un déploiement serverless ;
- un système multi-cloud ;
- un blue/green entièrement automatisé ;
- un canary deployment ;
- une mise à l’échelle automatique ;
- plusieurs instances applicatives par défaut ;
- une haute disponibilité contractuelle ;
- un service mesh ;
- un déploiement production déclenché par chaque push ;
- une migration automatique au démarrage ;
- un accès direct de SiteFactory aux productions clientes ;
- une console centralisée multi-client dès le MVP.

Ces capacités pourront être étudiées si le nombre de clients, la criticité ou les engagements contractuels le justifient.

---

# 5. Principes fondamentaux

## 5.1 Un client reste isolé

Chaque client possède au minimum :

```text
Repository
Application
Base PostgreSQL
Réseau Docker
Volumes
Secrets
Nom de projet Compose
Configuration
Backups
```

Deux applications clientes ne partagent pas :

- le même schéma applicatif ;
- les mêmes volumes ;
- les mêmes clés Data Protection ;
- le même mot de passe PostgreSQL ;
- les mêmes comptes administrateurs ;
- le même fichier `.env`.

---

## 5.2 Le même artefact doit être promu

La stratégie cible privilégie :

```text
Build une fois
→ tester l’artefact
→ déployer le même artefact
```

L’image testée en staging doit idéalement être la même que celle déployée en production.

Il faut éviter :

```text
recompiler différemment pour chaque environnement
```

---

## 5.3 Les secrets ne font pas partie de l’artefact

L’image Docker et le repository restent identiques entre environnements.

Les différences proviennent principalement de :

- variables d’environnement ;
- secrets ;
- domaines ;
- ressources ;
- intégrations ;
- kill switches opérationnels.

---

## 5.4 Aucun déploiement production implicite

Un déploiement production nécessite :

- une version explicite ;
- un environnement explicite ;
- une validation humaine ;
- un backup lorsque les données peuvent être affectées ;
- une migration contrôlée ;
- des smoke tests ;
- une stratégie de rollback.

---

## 5.5 Aucune modification manuelle dans un conteneur

Il est interdit de modifier directement :

- le code ;
- la configuration ;
- les assets ;
- les fichiers applicatifs ;

à l’intérieur d’un conteneur en fonctionnement.

Toute modification durable doit provenir :

- du repository ;
- d’une image versionnée ;
- d’une configuration d’environnement ;
- d’un volume prévu.

---

## 5.6 Aucun secret dans Git

Les environnements réels ne doivent pas dépendre d’un fichier secret versionné.

Le repository contient uniquement :

```text
.env.example
.env.production.example
.env.staging.example
```

avec des valeurs fictives ou vides.

---

## 5.7 Les données persistantes restent hors du conteneur

Doivent être persistés dans des volumes :

```text
PostgreSQL
App_Data
Médias
Documents
Data Protection Keys
Données Caddy nécessaires
```

La recréation d’un conteneur ne doit pas supprimer les données applicatives.

---

## 5.8 Les migrations restent explicites

La production n’exécute jamais automatiquement :

```csharp
Database.Migrate()
```

au démarrage.

Le déploiement ne doit pas confondre :

```text
mise à jour du code
```

et :

```text
modification du schéma de données
```

---

# 6. Environnements supportés

Les environnements initiaux sont :

```text
Development
Staging
Production
```

D’autres environnements tels que :

```text
Preview
QA
Performance
DisasterRecovery
```

restent hors du MVP tant qu’un besoin réel n’est pas identifié.

---

# 7. Environnement Development

## 7.1 Objectif

Development est destiné à :

- développer ;
- déboguer ;
- tester localement ;
- produire des migrations ;
- valider les modules ;
- prévisualiser le projet.

---

## 7.2 Hébergement

L’application peut être exécutée :

```bash
dotnet run
```

PostgreSQL peut être exécuté dans Docker.

Exemple conceptuel :

```text
Application locale
→ PostgreSQL local en conteneur
```

L’application complète peut également être exécutée avec Docker Compose.

---

## 7.3 Données

Les données de développement sont :

- fictives ;
- reproductibles ;
- sans donnée client réelle ;
- réinitialisables.

Exemples :

```text
Example Project
contact@example.test
admin@example.test
```

---

## 7.4 Secrets locaux

Les secrets de développement utilisent :

- .NET User Secrets ;
- variables d’environnement ;
- fichier local ignoré par Git.

---

## 7.5 SMTP

Le développement doit privilégier :

- un serveur de capture local ;
- un fake ;
- un provider sandbox ;
- une implémentation désactivée.

Il ne doit pas envoyer involontairement des emails réels.

---

## 7.6 Identity

Un seed de développement peut créer :

- utilisateur administrateur fictif ;
- rôles ;
- données de démonstration.

Ce comportement ne doit pas être activé automatiquement en production.

---

## 7.7 SEO

Development peut utiliser :

```text
domaine .test
noindex
contenus de démonstration
placeholders signalés
```

---

## 7.8 Logs

Development peut utiliser :

```text
niveau Debug
console lisible
détails d’erreur locaux
```

Les secrets et données personnelles restent interdits.

---

# 8. Environnement Staging

## 8.1 Objectif

Staging sert à :

- valider un artefact avant production ;
- exécuter une recette interne ;
- exécuter une recette client ;
- tester les migrations ;
- tester Docker ;
- tester Caddy ;
- tester les intégrations ;
- exécuter les smoke tests ;
- tester une procédure de rollback.

---

## 8.2 Recommandation

Staging est fortement recommandé pour :

- migration de données ;
- nouveau module ;
- modification Identity ;
- modification de sécurité ;
- changement de route ;
- changement de configuration Docker ;
- upgrade important ;
- nouvelle intégration externe ;
- projet renforcé.

Pour un petit projet vitrine sans modification de données, une staging temporaire peut être suffisante.

---

## 8.3 Isolation

Staging possède :

```text
Base PostgreSQL distincte
Volumes distincts
Clés Data Protection distinctes
Secrets distincts
Domaine distinct
Projet Compose distinct
```

---

## 8.4 Données

Les données de production ne doivent pas être copiées telles quelles en staging.

Les données de staging sont :

- fictives ;
- anonymisées ;
- ou explicitement nettoyées.

---

## 8.5 Domaine

Exemples :

```text
staging.client.fr
preview.client.fr
```

Le domaine doit être clairement distinct de la production.

---

## 8.6 Indexation

Staging doit être non indexable.

Contrôles :

```text
robots.txt avec Disallow: /
meta robots noindex
absence du sitemap public
protection d’accès éventuelle
```

---

## 8.7 Protection d’accès

Selon le projet, staging peut être protégée par :

- authentification applicative ;
- restriction IP ;
- protection HTTP ;
- VPN ;
- règle Caddy.

La protection ne doit pas empêcher les contrôles automatisés autorisés.

---

## 8.8 Emails

Staging doit utiliser :

- sandbox ;
- destinataire d’essai ;
- allowlist ;
- réécriture du destinataire ;
- fake.

Elle ne doit pas envoyer de messages à des prospects ou clients réels par accident.

---

## 8.9 Intégrations externes

Les intégrations utilisent :

- sandbox ;
- compte de test ;
- environnement de staging ;
- provider désactivé.

Une clé de production ne doit pas être utilisée.

---

# 9. Environnement Production

## 9.1 Objectif

Production héberge le service réellement utilisé.

Elle doit être :

- explicitement configurée ;
- sécurisée ;
- sauvegardée ;
- observable ;
- restaurable ;
- documentée.

---

## 9.2 Configuration

Production utilise :

```text
ASPNETCORE_ENVIRONMENT=Production
```

---

## 9.3 Données

Production contient les données réelles du client.

Elles ne doivent jamais être utilisées directement dans :

- tests ;
- démonstrations ;
- staging ;
- captures destinées à la documentation ;
- prompts LLM non autorisés.

---

## 9.4 Secrets

Les secrets de production sont :

- uniques ;
- robustes ;
- stockés hors Git ;
- accessibles uniquement aux opérateurs autorisés ;
- renouvelables ;
- absents des logs.

---

## 9.5 AdminSeed

Production doit utiliser :

```text
AdminSeed__Enabled=false
```

hors procédure temporaire contrôlée.

Le mot de passe du seed doit être retiré de l’environnement après initialisation.

---

## 9.6 SEO

Production exige :

- domaine réel ;
- HTTPS ;
- sitemap ;
- robots cohérent ;
- canonical de production ;
- absence de placeholder ;
- absence de données de démonstration.

---

## 9.7 Logs

Production utilise :

```text
logs structurés JSON
niveau Information
health checks
rotation
diagnostics détaillés protégés
```

---

# 10. Tableau comparatif des environnements

| Élément | Development | Staging | Production |
|---|---|---|---|
| Données réelles | Non | Non, sauf données anonymisées validées | Oui |
| Domaine `.test` | Autorisé | Non recommandé | Interdit |
| HTTPS | Local selon besoin | Oui | Obligatoire |
| Indexation | Non | Non | Selon pages |
| Logs Debug | Autorisés | Limités | Désactivés par défaut |
| Developer Exception Page | Autorisée localement | Non | Non |
| SMTP réel | Non | Sandbox ou allowlist | Oui |
| Provider LLM réel | Optionnel avec données fictives | Provider de test ou approuvé | Explicitement approuvé |
| AdminSeed | Développement possible | Contrôlé | Désactivé |
| Backups | Facultatifs | Recommandés | Obligatoires |
| Migrations automatiques | Local uniquement si explicitement autorisé | Non | Non |
| Données de démonstration | Autorisées | Contrôlées | Interdites |
| Health checks | Oui | Oui | Oui |
| Smoke tests | Locaux | Obligatoires | Obligatoires |

---

# 11. Parité des environnements

## 11.1 Principe

Staging doit être suffisamment proche de production pour détecter :

- erreurs de configuration ;
- erreurs Docker ;
- erreurs de migration ;
- erreurs de reverse proxy ;
- erreurs de volume ;
- erreurs de sécurité ;
- erreurs de runtime.

---

## 11.2 Différences acceptables

Les différences légitimes incluent :

```text
Domaine
Secrets
Taille des ressources
Données
Provider SMTP
Provider externe
Niveau de logs
Indexation
```

---

## 11.3 Différences à éviter

```text
Version différente de PostgreSQL

Image applicative différente

Modules différents

Migration différente

Mode de rendu différent

Configuration de sécurité affaiblie

Structure de volumes différente
```

---

## 11.4 Détection de dérive

Les fichiers de déploiement doivent être versionnés autant que possible.

Les changements manuels côté serveur doivent être :

- documentés ;
- reportés dans le repository si durables ;
- revus ;
- intégrés au runbook.

---

# 12. Profil `docker-compose-vps`

## 12.1 Services principaux

Le profil comprend :

```text
web
postgres
caddy
```

Selon l’exploitation, Caddy peut être :

- inclus dans le Compose client ;
- ou exploité comme reverse proxy mutualisé au niveau du VPS.

Le choix doit rester explicite.

---

## 12.2 Baseline recommandée

Pour le premier projet pilote :

```text
un stack Compose par client
```

avec :

- réseau dédié ;
- volumes dédiés ;
- secrets dédiés ;
- domaine dédié.

---

## 12.3 Structure générée

Structure cible :

```text
deploy/
├── docker-compose.yml
├── development/
│   └── docker-compose.override.yml
├── staging/
│   ├── docker-compose.staging.yml
│   └── .env.staging.example
├── production/
│   ├── docker-compose.production.yml
│   └── .env.production.example
├── caddy/
│   └── Caddyfile.example
└── scripts/
    ├── deploy.sh
    ├── rollback.sh
    ├── check-production.sh
    └── smoke-test.sh
```

La structure exacte pourra être simplifiée dans le premier incrément.

---

# 13. Nom du projet Compose

Le nom Compose doit identifier :

- client ;
- environnement.

Format recommandé :

```text
{client-slug}-{environment}
```

Exemples :

```text
cabinet-alpha-staging
cabinet-alpha-production
```

Variable possible :

```text
COMPOSE_PROJECT_NAME
```

---

## 13.1 `container_name`

L’utilisation explicite de :

```yaml
container_name:
```

doit être évitée par défaut.

Compose peut gérer les noms à partir du projet et du service.

Cela réduit :

- collisions ;
- problèmes de scaling ;
- configuration manuelle.

---

# 14. Réseaux Docker

## 14.1 Réseau privé

Chaque stack possède un réseau privé.

Exemple conceptuel :

```text
cabinet-alpha-production_default
```

---

## 14.2 PostgreSQL

PostgreSQL est accessible uniquement depuis le réseau privé prévu.

---

## 14.3 Caddy mutualisé

Si Caddy est mutualisé entre plusieurs stacks, un réseau proxy partagé peut être ajouté.

Dans ce cas :

- seul le service web rejoint le réseau proxy ;
- PostgreSQL reste exclusivement sur le réseau privé du client ;
- les noms et routes doivent être maîtrisés.

---

## 14.4 Réseau public inutile

L’application ne doit pas rejoindre un réseau Docker global sans besoin.

---

# 15. Volumes Docker

## 15.1 Volumes obligatoires

```text
postgres_data
app_data
data_protection_keys
caddy_data
caddy_config
```

Les volumes Caddy ne sont nécessaires dans la stack client que si Caddy n’est pas mutualisé.

---

## 15.2 Préfixes

Les noms doivent être isolés par client et environnement.

Exemples :

```text
cabinet_alpha_prod_postgres_data
cabinet_alpha_prod_app_data
cabinet_alpha_prod_data_protection_keys
```

---

## 15.3 Données éphémères

Les fichiers temporaires ne doivent pas être stockés dans les volumes de backup sauf nécessité.

---

## 15.4 Suppression

La commande :

```bash
docker compose down
```

ne supprime pas les volumes par défaut.

La commande :

```bash
docker compose down --volumes
```

est interdite en production sans procédure destructive explicite.

---

# 16. Service `web`

## 16.1 Responsabilités

Le service web contient :

- application ASP.NET Core ;
- pages publiques ;
- administration ;
- modules ;
- health checks.

---

## 16.2 Image

L’image est :

- versionnée ;
- construite en Release ;
- sans secret ;
- sans données client ;
- non root lorsque possible ;
- issue d’un commit identifiable.

---

## 16.3 Port

Port interne recommandé :

```text
8080
```

Il n’est pas directement exposé à Internet lorsque Caddy est utilisé.

---

## 16.4 Environnement

Variables minimales conceptuelles :

```text
ASPNETCORE_ENVIRONMENT
ASPNETCORE_URLS
ConnectionStrings__MainDatabase
DataProtection__KeysPath
```

Les modules ajoutent leurs propres variables validées.

---

## 16.5 Dépendance PostgreSQL

Le service peut attendre le health check de PostgreSQL avant démarrage selon Compose.

L’application doit néanmoins gérer proprement une indisponibilité ultérieure de la base.

---

# 17. Service `postgres`

## 17.1 Image

L’image PostgreSQL utilise une version majeure explicite.

Exemple conceptuel :

```text
postgres:{major-version}
```

Une image flottante telle que :

```text
postgres:latest
```

est interdite en production.

---

## 17.2 Variables

```text
POSTGRES_DB
POSTGRES_USER
POSTGRES_PASSWORD
```

La valeur du mot de passe reste hors Git.

---

## 17.3 Port

Aucun mapping public :

```yaml
ports:
  - "5432:5432"
```

dans le profil production standard.

---

## 17.4 Volume

Les données sont montées sur le volume PostgreSQL persistant.

---

## 17.5 Health check

Le conteneur possède un health check simple et non sensible.

---

## 17.6 Compte applicatif

Le compte utilisé par l’application n’est pas superutilisateur.

Une séparation entre compte d’administration et compte applicatif peut être ajoutée pour les projets renforcés.

---

# 18. Service `caddy`

## 18.1 Responsabilités

Caddy assure :

- HTTPS ;
- certificats ;
- renouvellement ;
- redirection HTTP vers HTTPS ;
- reverse proxy ;
- headers ;
- logs d’accès selon la politique.

---

## 18.2 Ports

Caddy expose :

```text
80
443
```

---

## 18.3 Configuration

Le repository peut contenir :

```text
Caddyfile.example
```

Le fichier réel est installé ou généré lors de la préparation du serveur.

Il ne contient aucun secret.

---

## 18.4 Administration Caddy

L’API d’administration ne doit pas être exposée publiquement.

---

## 18.5 Validation

Avant reload :

```text
valider la configuration Caddy
```

Une configuration invalide ne doit pas remplacer la configuration active.

---

## 18.6 Mutualisation

Un Caddy mutualisé peut servir plusieurs clients sur un VPS.

Il doit conserver :

- routes distinctes ;
- logs maîtrisés ;
- certificats distincts ;
- backends isolés ;
- configuration versionnée ou sauvegardée.

---

# 19. Hébergement de plusieurs clients sur un VPS

## 19.1 Autorisation initiale

Plusieurs projets clients peuvent partager un VPS physique si :

- leurs risques sont compatibles ;
- les ressources sont suffisantes ;
- les stacks restent isolées ;
- les secrets restent distincts ;
- les volumes restent distincts ;
- les bases restent distinctes ;
- les sauvegardes restent identifiables.

---

## 19.2 Ce qui peut être mutualisé

Selon la stratégie :

```text
VPS
Caddy
Docker Engine
Infrastructure de backup
Monitoring opérateur
```

---

## 19.3 Ce qui ne doit pas être mutualisé fonctionnellement

```text
Application
Base PostgreSQL
DbContext
Données
Volumes
Data Protection Keys
Secrets
Comptes administrateurs
```

---

## 19.4 VPS dédié

Un VPS dédié est recommandé pour :

- projet renforcé ;
- données sensibles ;
- forte charge ;
- exigence contractuelle ;
- dépendances spécifiques ;
- isolation réglementaire ;
- risque opérationnel élevé.

---

## 19.5 Ressources

La mutualisation nécessite une surveillance de :

- mémoire ;
- CPU ;
- disque ;
- I/O ;
- nombre de conteneurs ;
- croissance PostgreSQL ;
- backups.

Un client ne doit pas pouvoir saturer silencieusement les ressources des autres.

---

# 20. Structure du VPS

Structure conceptuelle :

```text
/opt/logicgate/
├── clients/
│   ├── cabinet-alpha/
│   │   ├── production/
│   │   └── staging/
│   └── entreprise-beta/
│       └── production/
├── backups/
├── caddy/
└── scripts/
```

Les chemins exacts doivent être standardisés dans les runbooks.

---

## 20.1 Permissions

Chaque dossier doit posséder des permissions adaptées.

Les fichiers `.env` de production ne doivent être lisibles que par les opérateurs ou comptes nécessaires.

---

## 20.2 Repository sur le VPS

Deux stratégies sont possibles :

### Image immuable

Le VPS ne contient pas obligatoirement le repository complet.

### Build sur le VPS

Le repository est cloné sur un commit ou tag exact.

La stratégie image immuable est la cible préférée.

---

# 21. Construction des images

## 21.1 Stratégie cible

```text
CI
→ build
→ tests
→ image
→ scan
→ registry privé
→ déploiement par tag ou digest
```

---

## 21.2 Tag

Formats possibles :

```text
1.2.0
1.2.0-rc.1
sha-abc1234
```

La production ne doit pas utiliser uniquement :

```text
latest
```

---

## 21.3 Digest

Pour une traçabilité renforcée, la production peut verrouiller l’image par digest.

---

## 21.4 Métadonnées

L’image doit permettre d’identifier :

- version ;
- commit ;
- date de build ;
- repository ;
- produit.

Aucun secret ne doit apparaître dans les labels.

---

## 21.5 Build local ou VPS

Pendant le premier pilote, une construction sur le VPS peut être tolérée si :

- le commit est exact ;
- le repository est propre ;
- les tests ont déjà réussi en CI ;
- le Dockerfile est identique ;
- le build est consigné ;
- aucun secret n’entre dans les couches.

Cette stratégie constitue un mode transitoire.

---

# 22. Registry

## 22.1 Statut MVP

Un registry privé est recommandé mais ne doit pas bloquer le premier projet pilote si le processus de build sur VPS est maîtrisé.

---

## 22.2 Accès

Les credentials du registry :

- restent hors Git ;
- possèdent une portée limitée ;
- sont renouvelables ;
- ne sont pas affichés.

---

## 22.3 Nettoyage

Les anciennes images doivent être conservées suffisamment longtemps pour permettre un rollback.

Une politique de rétention doit éviter une croissance indéfinie.

---

# 23. Configuration runtime

## 23.1 Ordre de priorité

Ordre conceptuel :

```text
appsettings.json
→ appsettings.{Environment}.json
→ variables d’environnement
→ secrets d’environnement
```

---

## 23.2 Configuration client

`client.sitefactory.yml` définit le projet généré.

Il ne remplace pas la configuration runtime de l’application.

---

## 23.3 Options typées

Les paramètres runtime utilisent des options typées et validées.

Une option essentielle manquante doit produire un échec clair au démarrage.

---

## 23.4 Valeurs sensibles

Les valeurs sensibles ne doivent jamais être affichées dans un diagnostic.

---

# 24. Fichiers `.env`

## 24.1 Exemples versionnés

```text
.env.example
.env.staging.example
.env.production.example
```

Ils contiennent :

- noms de variables ;
- commentaires ;
- valeurs fictives ;
- marqueurs `CHANGE_ME`.

---

## 24.2 Fichiers réels

```text
.env.staging
.env.production
```

restent hors Git.

---

## 24.3 Permissions

Sur Linux, les fichiers doivent posséder des permissions restrictives.

Exemple conceptuel :

```text
600
```

---

## 24.4 Vérification

Les scripts doivent vérifier :

- présence des variables requises ;
- valeurs non vides ;
- absence de placeholder ;
- longueurs minimales lorsque pertinent.

Ils ne doivent pas afficher les valeurs.

---

# 25. Secrets requis

Liste conceptuelle selon les modules :

```text
ConnectionStrings__MainDatabase
POSTGRES_PASSWORD
Smtp__Password
AdminSeed__Password
Registry__Password
Llm__Providers__Remote__ApiKey
```

La liste finale est produite par la composition des modules.

---

## 25.1 Secrets propres aux environnements

Chaque environnement possède ses propres secrets.

Une clé de staging ne doit pas être réutilisée en production.

---

## 25.2 Rotation

La rotation doit être possible sans reconstruire l’image.

---

## 25.3 Secret supprimé

Une ancienne valeur doit être retirée :

- de l’environnement ;
- des fichiers ;
- du gestionnaire ;
- des accès ;
- des procédures.

---

# 26. DNS

## 26.1 Préconditions

Avant un déploiement public :

- domaine connu ;
- enregistrement DNS configuré ;
- IP correcte ;
- TTL adapté ;
- Caddy prêt ;
- ports ouverts.

---

## 26.2 Bascule

Pour une migration de domaine ou de serveur, réduire le TTL suffisamment tôt peut faciliter la bascule.

---

## 26.3 Vérification

Les smoke tests doivent vérifier que le domaine résout vers l’infrastructure attendue.

---

# 27. Firewall

## 27.1 Ports

Baseline :

```text
22
80
443
```

---

## 27.2 PostgreSQL

Le port :

```text
5432
```

reste fermé publiquement.

---

## 27.3 SSH

L’accès SSH doit être limité :

- clés ;
- utilisateurs autorisés ;
- IP lorsque possible ;
- protection contre les tentatives répétées.

---

## 27.4 Ports applicatifs

Le port interne ASP.NET Core n’est pas ouvert publiquement.

---

# 28. Utilisateurs système

## 28.1 Root

L’exploitation courante ne doit pas utiliser directement `root`.

---

## 28.2 Compte opérateur

Un compte dédié peut gérer :

- Docker ;
- fichiers de déploiement ;
- scripts ;
- backups.

L’accès au groupe Docker constitue un privilège élevé.

---

## 28.3 Clés SSH

Les clés doivent être :

- individuelles ;
- renouvelables ;
- retirées au départ d’un opérateur ;
- protégées localement.

---

# 29. Préparation initiale d’un VPS

Étapes de référence :

```text
1. Installer les mises à jour
2. Créer l’utilisateur opérateur
3. Configurer SSH par clé
4. Désactiver les accès inutiles
5. Configurer le firewall
6. Installer Docker
7. Installer Docker Compose
8. Préparer les répertoires
9. Configurer Caddy
10. Préparer les backups
11. Configurer la rotation des logs
12. Vérifier l’espace disque
13. Documenter le serveur
```

---

## 29.1 Vérification finale

```text
SSH fonctionne
HTTP/HTTPS disponibles
PostgreSQL non exposé
Docker fonctionnel
Compose fonctionnel
Caddy valide
Backups accessibles
```

---

# 30. Préconditions d’un déploiement

Avant tout déploiement :

```text
[ ] Version identifiée
[ ] Commit identifié
[ ] Branche principale verte
[ ] Artefact ou image disponible
[ ] Tests réussis
[ ] Scans réussis
[ ] Configuration validée
[ ] Secrets présents
[ ] Dossier de déploiement valide
[ ] Espace disque suffisant
[ ] Backup requis identifié
[ ] Migration identifiée
[ ] Rollback préparé
[ ] Fenêtre de maintenance définie si nécessaire
```

---

# 31. Plan de déploiement

Un déploiement doit posséder un plan.

Contenu minimal :

```text
Client
Environnement
Version actuelle
Version cible
Image ou commit
Modules affectés
Configuration affectée
Migration
Backup
Étapes
Smoke tests
Rollback
Opérateur
Date
```

---

# 32. Déploiement sans migration

Flux de référence :

```text
1. Vérifier les préconditions
2. Vérifier la version active
3. Télécharger ou construire l’image cible
4. Valider Docker Compose
5. Valider Caddy si modifié
6. Mettre à jour la référence d’image
7. Recréer le service web
8. Attendre le health check
9. Exécuter les smoke tests
10. Vérifier les logs
11. Consigner le résultat
```

---

# 33. Déploiement avec migration

Flux de référence :

```text
1. Vérifier les préconditions
2. Vérifier la migration
3. Inspecter le SQL ou le code généré
4. Vérifier la compatibilité de rollback
5. Réaliser un backup
6. Vérifier le backup
7. Mettre l’application en maintenance si nécessaire
8. Appliquer la migration explicitement
9. Vérifier l’historique des migrations
10. Déployer l’image cible
11. Attendre le readiness
12. Exécuter les smoke tests
13. Vérifier les données principales
14. Quitter la maintenance
15. Consigner le résultat
```

---

# 34. Application des migrations

## 34.1 Principes

Une seule opération de migration doit être exécutée à la fois.

La migration doit utiliser :

- version exacte ;
- code exact ;
- configuration exacte ;
- base exacte ;
- environnement exact.

---

## 34.2 Mode préféré — bundle de migration

La stratégie cible peut utiliser un bundle de migration construit en CI depuis la release exacte.

Avantages :

- artefact identifiable ;
- pas de SDK complet dans l’image runtime ;
- procédure reproductible ;
- version contrôlée.

---

## 34.3 Mode alternatif — conteneur SDK temporaire

Un conteneur SDK temporaire peut :

- rejoindre le réseau Docker du client ;
- utiliser le code de la release exacte ;
- exécuter la commande EF Core ;
- être supprimé après usage.

---

## 34.4 Mode opérateur

Une migration depuis un poste opérateur peut être envisagée uniquement avec :

- connexion sécurisée ;
- accès privé ;
- tunnel SSH éventuel ;
- version exacte ;
- logs contrôlés.

La base ne doit pas être exposée publiquement pour faciliter cette opération.

---

## 34.5 Interdictions

```text
Auto-migration au démarrage

Migration lancée depuis une ancienne branche

Migration sans backup lorsque destructive

Migration simultanée par plusieurs opérateurs

Migration sans vérifier l’environnement cible
```

---

# 35. Stratégie expand-contract

Pour les évolutions sensibles :

```text
Expand
→ ajouter la nouvelle structure compatible

Migrate
→ copier ou transformer les données

Switch
→ utiliser la nouvelle structure

Contract
→ supprimer l’ancienne structure plus tard
```

Cette approche facilite :

- rollback applicatif ;
- déploiement progressif ;
- réduction des interruptions.

---

# 36. Mode maintenance

## 36.1 Cas d’usage

Un mode maintenance peut être nécessaire pour :

- migration non compatible ;
- import ;
- restauration ;
- opération destructive ;
- maintenance planifiée.

---

## 36.2 Implémentation

Approches possibles :

- page statique Caddy ;
- feature opérationnelle ;
- application dédiée temporaire.

Le mode maintenance ne doit pas bloquer :

- l’accès opérateur nécessaire ;
- les health checks adaptés ;
- la restauration.

---

## 36.3 Message

Le message public doit rester sobre.

Il ne doit pas exposer le détail technique de l’opération.

---

# 37. Health checks pendant le déploiement

## 37.1 Liveness

```text
/health/live
```

vérifie que le processus fonctionne.

---

## 37.2 Readiness

```text
/health/ready
```

vérifie que l’application peut servir le trafic attendu.

---

## 37.3 Attente

Le script de déploiement doit attendre :

- un nombre limité de tentatives ;
- un timeout ;
- un statut cohérent.

Il ne doit pas attendre indéfiniment.

---

## 37.4 Échec

Un readiness en échec déclenche :

- arrêt du déploiement ;
- consultation des logs ;
- rollback éventuel.

---

# 38. Smoke tests

## 38.1 Baseline publique

```text
HTTP redirige vers HTTPS

Certificat valide

Accueil retourne 200

Pages principales accessibles

Aucun domaine de test

Aucun placeholder

Headers de sécurité présents

Canonical correcte

Sitemap accessible

robots.txt cohérent
```

---

## 38.2 Baseline administration

```text
/admin inaccessible anonymement

/admin/login accessible

Connexion administrateur possible

Logout POST fonctionnel

Route admin protégée

Diagnostic détaillé protégé
```

---

## 38.3 Baseline données

```text
PostgreSQL accessible par l’application

Migration attendue présente

Lecture et écriture de contrôle possibles

App_Data disponible

Data Protection disponible
```

---

## 38.4 Baseline formulaire

Lorsque le projet contient un formulaire :

```text
Soumission de test

Validation serveur

Persistance

Affichage administration

Notification contrôlée
```

La production doit utiliser une donnée de test clairement identifiable et supprimable si un test de bout en bout est exécuté.

---

## 38.5 Baseline modules

Les smoke tests doivent couvrir les modules affectés par la release.

---

# 39. Déploiement d’une nouvelle application

Flux initial :

```text
1. Créer les répertoires
2. Installer les fichiers Compose
3. Installer les secrets
4. Créer les volumes
5. Configurer Caddy
6. Démarrer PostgreSQL
7. Créer ou migrer la base
8. Démarrer l’application
9. Vérifier les health checks
10. Créer le premier administrateur
11. Désactiver AdminSeed
12. Vérifier la connexion
13. Exécuter les smoke tests
14. Activer le trafic
15. Vérifier les backups
```

---

# 40. Initialisation de l’administrateur

Procédure :

```text
1. AdminSeed désactivé initialement
2. Fournir temporairement email et mot de passe
3. Activer AdminSeed
4. Démarrer ou recréer le conteneur
5. Vérifier la création
6. Tester la connexion
7. Désactiver AdminSeed
8. Retirer le mot de passe de l’environnement
9. Recréer le conteneur
10. Vérifier la connexion existante
```

Le mot de passe ne doit jamais apparaître dans les logs ou commandes affichées.

---

# 41. Rollback applicatif

## 41.1 Condition

Un rollback applicatif est possible si la version précédente reste compatible avec le schéma actuel.

---

## 41.2 Procédure

```text
1. Identifier la version précédente
2. Modifier la référence d’image
3. Recréer le service web
4. Vérifier le health check
5. Exécuter les smoke tests
6. Vérifier les logs
7. Consigner le rollback
```

---

## 41.3 Image précédente

La version précédente doit rester disponible dans le registry ou sur le serveur pendant la période de validation.

---

# 42. Rollback de base de données

## 42.1 Principe

Le rollback de base ne repose pas uniquement sur :

```text
migration Down
```

La protection principale est :

```text
backup vérifié
```

---

## 42.2 Restauration

Une restauration peut nécessiter :

- arrêt des écritures ;
- confirmation ;
- sauvegarde de l’état défaillant pour analyse ;
- restauration PostgreSQL ;
- restauration App_Data ;
- restauration des clés ;
- redéploiement de la version compatible ;
- smoke tests.

---

## 42.3 Impact

La restauration peut entraîner une perte des données créées après le backup.

Cette conséquence doit être évaluée avant l’opération.

---

# 43. Critères déclenchant un rollback

Exemples :

```text
Application ne démarre pas

Readiness reste en échec

Erreur 5xx généralisée

Connexion admin impossible

Formulaires non persistés

Migration incohérente

Données critiques incorrectes

Régression de sécurité

Volumes non montés

Secrets non reconnus
```

Un défaut cosmétique limité ne déclenche pas nécessairement un rollback complet.

---

# 44. Déploiement interrompu

En cas d’interruption :

1. déterminer l’étape atteinte ;
2. vérifier l’état de la migration ;
3. vérifier les conteneurs ;
4. vérifier la version active ;
5. consulter le rapport ;
6. reprendre uniquement si l’état est connu ;
7. sinon restaurer ou revenir à la version précédente.

Le script ne doit pas supposer que l’opération précédente a réussi.

---

# 45. Atomicité du déploiement

Docker Compose ne fournit pas une transaction globale.

La procédure doit donc :

- ordonner les étapes ;
- vérifier chaque résultat ;
- limiter les modifications simultanées ;
- préserver l’ancienne image ;
- préparer le rollback.

---

# 46. Disponibilité pendant le déploiement

## 46.1 MVP

Avec une seule instance applicative, une courte interruption peut survenir lors de la recréation du conteneur.

Cette limite doit être documentée.

---

## 46.2 Maintenance planifiée

Pour les opérations sensibles, une fenêtre de maintenance peut être utilisée.

---

## 46.3 Évolution future

Une stratégie de déploiement sans interruption pourra être étudiée avec :

- deux instances ;
- reverse proxy ;
- health-based routing ;
- blue/green ;
- base compatible.

Elle reste hors du MVP initial.

---

# 47. Déploiement de configuration uniquement

Certaines modifications nécessitent uniquement :

```text
variable d’environnement
→ recréation du conteneur
→ smoke tests
```

Exemples :

- niveau de log ;
- kill switch ;
- destinataire SMTP ;
- seuil de rétention.

Une modification d’entitlement, de module ou de package nécessite un nouveau cycle de génération ou d’upgrade.

---

# 48. Redémarrage versus recréation

## 48.1 Redémarrage

Un simple redémarrage conserve la configuration du conteneur existant.

---

## 48.2 Recréation

Une modification :

- d’image ;
- de variable ;
- de volume ;
- de réseau ;
- de Compose ;

nécessite généralement une recréation du conteneur.

---

## 48.3 Règle

Le runbook doit préciser l’action attendue.

Il ne faut pas supposer qu’un `restart` applique une nouvelle configuration.

---

# 49. Secrets et redéploiement

Une rotation de secret peut nécessiter :

1. créer le nouveau secret ;
2. mettre à jour l’environnement ;
3. recréer le service concerné ;
4. vérifier ;
5. révoquer l’ancien secret ;
6. consigner l’opération.

L’ordre dépend du provider et de la capacité à accepter deux secrets temporairement.

---

# 50. Data Protection

## 50.1 Volume

Les clés Data Protection résident dans un volume persistant propre à l’environnement.

---

## 50.2 Environnements

Development, Staging et Production ne partagent pas les mêmes clés.

---

## 50.3 Déploiement

Une recréation de conteneur doit conserver les clés.

---

## 50.4 Restauration

Les clés doivent être restaurées avec l’application lorsque cela est nécessaire.

---

## 50.5 Perte des clés

La perte peut invalider :

- cookies ;
- sessions ;
- tokens ;
- données protégées.

---

# 51. App_Data

## 51.1 Contenu

Peut contenir :

- médias ;
- documents ;
- exports temporaires ;
- clés Data Protection si cette structure est retenue ;
- fichiers persistants des modules.

---

## 51.2 Permissions

Le conteneur web possède uniquement les droits nécessaires.

---

## 51.3 Backup

Les données permanentes sont incluses dans les backups.

Les fichiers temporaires peuvent être exclus selon une politique explicite.

---

## 51.4 Migration de stockage

Toute modification de structure App_Data doit être versionnée et testée.

---

# 52. Sauvegardes avant déploiement

## 52.1 Obligatoires

Un backup est obligatoire avant :

- migration ;
- import ;
- suppression ;
- restauration partielle ;
- changement important de stockage ;
- upgrade susceptible de modifier des données.

---

## 52.2 Déploiement sans données

Un déploiement purement applicatif compatible peut ne pas exiger un nouveau backup immédiat si :

- un backup récent valide existe ;
- aucune donnée n’est modifiée ;
- le rollback est simple.

La décision doit être documentée.

---

## 52.3 Vérification

Il ne suffit pas de lancer la commande.

Il faut vérifier :

- code de sortie ;
- présence du fichier ;
- taille cohérente ;
- emplacement ;
- absence d’erreur ;
- capacité de restauration selon la politique.

---

# 53. Backups hors VPS

Une copie hors du serveur doit être prévue pour les productions exploitées durablement par LogicGate lorsque le niveau de service le justifie.

Une sauvegarde stockée uniquement sur le même disque ne protège pas contre :

- panne disque ;
- perte du VPS ;
- erreur de suppression ;
- compromission complète ;
- incident fournisseur.

---

# 54. État post-déploiement

Après succès, consigner :

```text
Version déployée
Commit
Image tag
Image digest éventuel
Migration appliquée
Backup associé
Date UTC
Opérateur
Résultat des health checks
Résultat des smoke tests
Warnings
```

---

# 55. Rapport de déploiement

Un fichier ou rapport structuré peut être produit :

```text
deployment-report.json
```

Structure conceptuelle :

```json
{
  "schemaVersion": 1,
  "deploymentId": "4a8aa4d8-57cc-47cf-9126-7f3704d4c9dd",
  "clientSlug": "cabinet-alpha",
  "environment": "production",
  "versionFrom": "1.1.0",
  "versionTo": "1.2.0",
  "commitSha": "abc1234",
  "image": {
    "tag": "1.2.0",
    "digest": "sha256:..."
  },
  "migration": {
    "required": true,
    "name": "AddPortfolioModule",
    "status": "success"
  },
  "backup": {
    "required": true,
    "status": "success"
  },
  "health": {
    "liveness": "healthy",
    "readiness": "healthy"
  },
  "smokeTests": {
    "status": "success"
  },
  "status": "success"
}
```

---

## 55.1 Données interdites

Le rapport ne contient pas :

- secret ;
- chaîne de connexion ;
- données client ;
- contenu de backup ;
- mots de passe ;
- clés ;
- sortie complète des fichiers d’environnement.

---

# 56. Traçabilité des versions

Chaque production doit permettre d’identifier :

```text
ApplicationVersion
CommitSha
GeneratorVersion
CatalogVersion
TemplateVersion
ThemeVersion
ModuleVersions
MigrationVersion
```

Les versions SiteFactory sont conservées dans :

```text
sitefactory.lock.json
```

---

# 57. Déploiement depuis la CI

## 57.1 Statut MVP

La CI peut construire et publier les artefacts.

Le déploiement production automatisé reste optionnel et séparé.

---

## 57.2 Environnement protégé

Une future automatisation doit utiliser :

- environnement GitHub `production` ;
- secrets limités ;
- approbation humaine ;
- concurrence stricte ;
- branche ou tag autorisé ;
- rapport.

---

## 57.3 Pull requests

Une pull request ne doit jamais déployer en production.

---

## 57.4 Main

Un push sur `main` ne doit pas automatiquement déployer tous les clients.

---

# 58. Commandes CLI futures

Groupe futur :

```text
sitefactory deploy check
sitefactory deploy plan
sitefactory deploy apply
sitefactory deploy status
sitefactory deploy rollback
```

Ces commandes restent hors du premier MVP tant que les runbooks manuels ne sont pas stabilisés.

---

## 58.1 `deploy check`

Vérifierait :

- configuration ;
- environnement ;
- image ;
- secrets requis ;
- version active ;
- health ;
- espace disque ;
- backup.

---

## 58.2 `deploy plan`

Produirait :

- version source ;
- version cible ;
- migration ;
- backup ;
- étapes ;
- smoke tests ;
- rollback.

---

## 58.3 `deploy apply`

Nécessiterait :

```text
--environment
--version
--execute
--confirm-deployment
```

Production nécessiterait une confirmation plus explicite.

---

## 58.4 Séparation

Ces commandes appelleraient les mêmes services de déploiement et ne contiendraient pas directement toute la logique dans les handlers CLI.

---

# 59. Gestion des erreurs de déploiement

Codes recommandés :

| Code | Signification |
|---|---|
| `SF-DEPLOY-001` | Environnement inconnu |
| `SF-DEPLOY-002` | Configuration de déploiement invalide |
| `SF-DEPLOY-003` | Version cible invalide |
| `SF-DEPLOY-004` | Image introuvable |
| `SF-DEPLOY-005` | Secret requis absent |
| `SF-DEPLOY-006` | Espace disque insuffisant |
| `SF-DEPLOY-007` | Backup requis en échec |
| `SF-DEPLOY-008` | Migration en échec |
| `SF-DEPLOY-009` | Docker Compose invalide |
| `SF-DEPLOY-010` | Caddy invalide |
| `SF-DEPLOY-011` | Liveness en échec |
| `SF-DEPLOY-012` | Readiness en échec |
| `SF-DEPLOY-013` | Smoke tests en échec |
| `SF-DEPLOY-014` | Rollback en échec |
| `SF-DEPLOY-015` | Confirmation manquante |
| `SF-DEPLOY-016` | Environnement de production non sûr |
| `SF-DEPLOY-017` | Version active inconnue |
| `SF-DEPLOY-018` | État intermédiaire incohérent |

---

# 60. Logs de déploiement

Événements possibles :

```text
DeploymentPlanCreated
DeploymentStarted
BackupCompleted
MigrationStarted
MigrationCompleted
ImagePulled
ApplicationRecreated
HealthCheckPassed
SmokeTestsPassed
DeploymentCompleted
DeploymentFailed
RollbackStarted
RollbackCompleted
```

---

## 60.1 Propriétés autorisées

```text
DeploymentId
ClientSlug
Environment
VersionFrom
VersionTo
CommitSha
MigrationName
Duration
Result
ErrorCode
```

---

## 60.2 Propriétés interdites

```text
Secrets
Connection string
Mot de passe
Contenu des données
Contenu du backup
Fichier .env complet
```

---

# 61. Monitoring après déploiement

Après déploiement, LogicGate doit surveiller pendant une période adaptée :

- erreurs 5xx ;
- redémarrages ;
- readiness ;
- PostgreSQL ;
- formulaire ;
- SMTP ;
- espace disque ;
- logs ;
- performances principales.

Une simple réussite immédiate du health check ne suffit pas toujours à confirmer la stabilité.

---

# 62. Fenêtre de validation

Une période de stabilisation peut être définie après le déploiement.

Pendant cette période :

- conserver l’image précédente ;
- éviter une nouvelle modification non urgente ;
- surveiller les erreurs ;
- vérifier les parcours ;
- préparer un rollback rapide.

---

# 63. Déploiements concurrents

Un seul déploiement doit être actif par :

```text
client
+ environnement
```

Une seconde opération doit :

- attendre ;
- ou échouer clairement.

---

## 63.1 Lock opérationnel

Un fichier ou mécanisme de verrouillage peut empêcher deux opérateurs d’exécuter simultanément une migration ou un déploiement.

---

# 64. Planification des déploiements

Les déploiements sensibles doivent être effectués :

- pendant une période connue ;
- avec un opérateur disponible ;
- avec un accès au rollback ;
- avec les dépendances critiques accessibles ;
- sans autre opération concurrente.

---

# 65. Déploiement d’urgence

## 65.1 Hotfix

Un hotfix doit toujours suivre :

```text
branche
→ correction
→ tests
→ build
→ scans
→ version
→ déploiement
```

La procédure peut être accélérée, mais les contrôles essentiels restent obligatoires.

---

## 65.2 Bypass

Un bypass de CI ou de protection nécessite :

- justification ;
- périmètre ;
- validation ;
- réexécution des checks après l’incident ;
- compte-rendu.

---

## 65.3 Kill switch

Lorsqu’une fonctionnalité présente un risque, un kill switch peut permettre de la désactiver sans rollback complet.

---

# 66. Arrêt d’un environnement

## 66.1 Staging

Staging peut être arrêtée lorsqu’elle n’est pas utilisée si :

- aucune donnée nécessaire n’est perdue ;
- sa recréation est documentée ;
- les volumes sont conservés ou supprimés volontairement ;
- le DNS et Caddy restent cohérents.

---

## 66.2 Production

Un arrêt de production doit être :

- planifié ;
- communiqué ;
- observé ;
- réversible.

---

# 67. Suppression d’un environnement

La suppression d’un environnement nécessite :

```text
1. Identifier les données
2. Réaliser un export ou backup final
3. Vérifier les obligations de conservation
4. Retirer le trafic
5. Arrêter les services
6. Archiver la configuration nécessaire
7. Supprimer les conteneurs
8. Supprimer les volumes uniquement après validation
9. Retirer les secrets
10. Retirer DNS et Caddy
11. Documenter
```

La suppression des volumes n’est jamais implicite.

---

# 68. Transfert de l’exploitation au client

Lorsque LogicGate ne conserve pas l’exploitation, le transfert doit inclure :

- repository ;
- versions ;
- configuration sans secret exposé inutilement ;
- procédure de gestion des secrets ;
- Docker Compose ;
- Caddy ;
- migrations ;
- backups ;
- restauration ;
- health checks ;
- runbooks ;
- responsabilité des mises à jour ;
- responsabilité de sécurité ;
- échéances de certificats et domaines ;
- accès administrateurs.

---

# 69. Fin d’un service mensuel

La fin d’un service mensuel ne doit pas provoquer automatiquement :

- arrêt du site ;
- suppression des backups existants sans décision ;
- suppression de HTTPS ;
- suppression des protections ;
- exposition de la base ;
- suppression des données.

Les responsabilités doivent être transférées et documentées.

---

# 70. Tests Development

Vérifier :

- application locale ;
- PostgreSQL local ;
- migrations ;
- seed fictif ;
- SMTP capturé ;
- logs Debug ;
- domaine `.test` ;
- noindex ;
- absence de secrets dans Git.

---

# 71. Tests Staging

Vérifier :

- image cible ;
- base distincte ;
- volumes distincts ;
- clés distinctes ;
- domaine distinct ;
- noindex ;
- email sandbox ;
- migration ;
- rollback ;
- smoke tests ;
- accès protégé.

---

# 72. Tests Production

Avant mise en service :

```text
HTTPS
Caddy
Headers
PostgreSQL privé
Secrets
Volumes
Data Protection
Backups
Restore
Health
Identity
Formulaire
SEO
Logs
Espace disque
Rollback
```

---

# 73. Tests Docker Compose

La CI doit vérifier :

- syntaxe ;
- résolution de configuration ;
- services ;
- réseaux ;
- volumes ;
- variables requises ;
- absence de port PostgreSQL public ;
- absence de secret ;
- absence de `latest` en production ;
- cohérence des profiles.

---

# 74. Tests Caddy

Vérifier :

- configuration valide ;
- domaine ;
- reverse proxy ;
- redirection HTTPS ;
- headers ;
- health ;
- absence d’administration publique ;
- logs ;
- comportement en cas de backend indisponible.

---

# 75. Tests de migration

Pour une release avec migration :

```text
Base de staging représentative
→ backup
→ migration
→ déploiement
→ smoke tests
→ rollback applicatif lorsque compatible
→ restauration de test lorsque nécessaire
```

---

# 76. Tests de rollback

Chaque procédure de rollback doit être testée avant d’être utilisée comme garantie.

Vérifier :

- image précédente disponible ;
- Compose modifiable ;
- health restauré ;
- compatibilité base ;
- logs ;
- temps de reprise ;
- données.

---

# 77. Tests des scripts

Les scripts doivent être testés sur :

```text
Linux
```

et, pour les scripts destinés au poste opérateur :

```text
Windows ou PowerShell multiplateforme
```

Ils doivent vérifier :

- arguments ;
- chemins ;
- confirmations ;
- codes de sortie ;
- annulation ;
- absence de secret ;
- absence de suppression hors périmètre.

---

# 78. Tests de sécurité

Vérifier notamment :

- secrets absents de l’image ;
- secrets absents des logs ;
- fichiers `.env` non versionnés ;
- PostgreSQL non public ;
- application non root lorsque prévu ;
- Caddy sécurisé ;
- ports limités ;
- Data Protection persistée ;
- staging non indexable ;
- production sans Debug ;
- AdminSeed désactivé.

---

# 79. Tests de coexistence sur VPS

Lorsque plusieurs clients partagent un VPS, vérifier :

- réseaux distincts ;
- volumes distincts ;
- noms Compose distincts ;
- bases distinctes ;
- domaines distincts ;
- secrets distincts ;
- aucune route Caddy croisée ;
- aucune collision de ports ;
- ressources suffisantes.

---

# 80. Quality gates de déploiement

Un déploiement ne peut pas être déclaré réussi si :

```text
Version inconnue

Artefact non testé

Secret requis absent

Backup requis échoué

Migration échouée

Docker Compose invalide

Caddy invalide

Liveness échoué

Readiness échoué

Smoke tests échoués

Logs critiques présents

Rapport absent
```

---

# 81. Critères d’acceptation du MVP

La stratégie sera considérée comme opérationnelle lorsque :

1. les environnements Development, Staging et Production sont définis ;
2. leurs différences sont documentées ;
3. le profil `docker-compose-vps` existe ;
4. chaque client possède un projet Compose isolé ;
5. les réseaux sont isolés ;
6. les volumes sont isolés ;
7. les secrets sont isolés ;
8. PostgreSQL n’est pas exposé publiquement ;
9. Caddy assure HTTPS ;
10. les ports publics sont limités ;
11. l’application est construite en Release ;
12. l’image ne contient aucun secret ;
13. les versions d’image sont explicites ;
14. l’usage de `latest` est interdit en production ;
15. la configuration runtime utilise des variables d’environnement ;
16. les fichiers `.env` réels restent hors Git ;
17. les options sont validées au démarrage ;
18. AdminSeed est désactivé par défaut ;
19. les clés Data Protection sont persistées ;
20. App_Data est persisté ;
21. les migrations sont explicites ;
22. une méthode de migration contrôlée existe ;
23. un backup précède les migrations sensibles ;
24. les health checks existent ;
25. les smoke tests existent ;
26. un rollback applicatif est documenté ;
27. une restauration de base est documentée ;
28. la version déployée est traçable ;
29. un rapport de déploiement peut être produit ;
30. staging est non indexable ;
31. les emails de staging sont sécurisés ;
32. la coexistence de plusieurs clients peut être vérifiée ;
33. la suppression des volumes reste explicite ;
34. les scripts retournent des codes de sortie fiables ;
35. les tests Docker et déploiement réussissent ;
36. aucun déploiement production n’est déclenché par une pull request.

---

# 82. Décisions validées

La baseline de déploiement est la suivante :

```text
Le profil initial est docker-compose-vps.

Les environnements supportés sont Development,
Staging et Production.

Chaque client possède une application et une base isolées.

Chaque environnement possède ses propres volumes,
secrets et clés Data Protection.

Le même artefact doit idéalement être promu
de staging vers production.

Les images sont versionnées.

La production n’utilise pas uniquement le tag latest.

Les secrets restent hors Git et hors image.

Les fichiers .env réels restent hors Git.

PostgreSQL n’est pas exposé publiquement.

Caddy assure la terminaison HTTPS.

Les ports publics standards sont 22, 80 et 443.

L’application s’exécute non root lorsque possible.

Les données persistent dans des volumes.

La suppression des volumes est toujours explicite.

Les migrations de production ne sont jamais automatiques.

Une migration est appliquée depuis la version exacte
du projet à déployer.

Un backup est réalisé avant une migration sensible.

La stratégie cible utilise un bundle de migration
ou un conteneur SDK temporaire contrôlé.

Le health liveness reste distinct du readiness.

Docker utilise le liveness comme check de vie du conteneur.

Les smoke tests sont obligatoires après déploiement.

Le rollback applicatif utilise une image précédente identifiée.

Le rollback de données repose principalement sur un backup vérifié.

Une seule opération de déploiement ou migration
est active par client et environnement.

Plusieurs clients peuvent partager un VPS physique
si leurs stacks restent isolées et si les ressources sont suffisantes.

Un projet renforcé peut nécessiter un VPS dédié.

Staging ne contient pas de données de production non anonymisées.

Staging est non indexable.

Staging n’envoie pas d’emails réels sans allowlist explicite.

Une pull request ne déploie jamais en production.

Un push sur main ne déploie pas automatiquement tous les clients.

Le déploiement reste séparé de la génération SiteFactory.

Codex ne peut pas activer seul un déploiement production
ou une migration automatique.
```

---

# 83. Règles pour Codex

## 83.1 Lecture préalable

Avant toute modification liée au déploiement, Codex doit consulter :

```text
../../README.md
../../AGENTS.md
../adr/0032-deployment-rollout-maintenance-mode-and-rollback-safety.md
../02-architecture/core-module-boundary.md
../adr/0023-data-retention-deletion-and-privacy-lifecycle.md
../adr/0028-database-transaction-boundaries-and-concurrency-control.md
../adr/0020-administrator-session-security-and-step-up-authentication.md
../adr/0021-administrator-provisioning-invitations-and-account-recovery.md
13-generation-pipeline.md
14-client-configuration-schema.md
15-cli-specification.md
18-security-baseline.md
19-testing-strategy.md
20-quality-gates-and-ci.md
21-observability-and-logging.md
22-deployment-and-environments.md
AGENTS.md
```

Ainsi que :

- Dockerfile ;
- fichiers Compose ;
- Caddyfile ;
- scripts ;
- migrations ;
- workflows ;
- health checks ;
- runbooks ;
- configuration du projet concerné.

---

## 83.2 Ce que Codex peut faire

Codex peut :

- générer un fichier Compose prévu ;
- durcir un Dockerfile ;
- ajouter un health check ;
- ajouter un script de déploiement ;
- ajouter un script de smoke test ;
- ajouter une validation ;
- corriger un réseau ;
- corriger un volume ;
- préparer un plan de migration ;
- ajouter un rapport de déploiement ;
- ajouter des tests ;
- documenter un rollback ;
- corriger une fuite de secret.

---

## 83.3 Ce que Codex ne doit pas faire

Codex ne doit pas :

- exposer PostgreSQL ;
- ajouter un secret dans Git ;
- ajouter un secret dans une image ;
- utiliser `latest` en production ;
- activer une auto-migration ;
- lancer une migration production ;
- déployer automatiquement depuis une pull request ;
- modifier une production sans confirmation ;
- supprimer un volume ;
- exécuter `docker compose down --volumes` en production ;
- afficher un fichier `.env` ;
- modifier directement un conteneur ;
- contourner un backup ;
- déclarer un rollback possible sans vérifier la compatibilité ;
- utiliser des données production en staging ;
- rendre staging indexable ;
- utiliser une clé SMTP de production en staging ;
- partager les clés Data Protection entre clients ;
- partager la base de plusieurs clients ;
- créer un accès SSH root permanent ;
- ouvrir un port supplémentaire sans validation.

---

## 83.4 Décision humaine requise

Codex doit signaler et attendre une décision pour :

- nouveau profil de déploiement ;
- nouveau fournisseur cloud ;
- registry ;
- déploiement automatique ;
- exposition réseau ;
- Caddy mutualisé ;
- partage de VPS ;
- VPS dédié ;
- nouvelle méthode de secrets ;
- changement de version PostgreSQL majeure ;
- migration destructive ;
- changement de structure des volumes ;
- déploiement sans interruption ;
- haute disponibilité ;
- suppression d’environnement ;
- transfert de l’exploitation ;
- réduction des backups ;
- modification d’une politique de rollback.

---

## 83.5 Compte-rendu attendu

Le compte-rendu doit préciser :

```text
Environnement concerné
Version source
Version cible
Image concernée
Dockerfile affecté
Compose affecté
Caddy affecté
Ports affectés
Réseaux affectés
Volumes affectés
Secrets requis
Migration requise
Backup requis
Health checks
Smoke tests
Rollback
Tests exécutés
Build
Résultats
Risques
Décisions humaines restantes
```

Codex ne doit pas déclarer un déploiement réussi s’il n’a pas vérifié le readiness et les smoke tests prévus.

---

# 84. Anti-patterns interdits

## 84.1 Tag `latest` en production

```yaml
image: logicgate/client:latest
```

comme seule référence.

---

## 84.2 PostgreSQL public

```yaml
ports:
  - "5432:5432"
```

---

## 84.3 Secret dans Compose

```yaml
environment:
  POSTGRES_PASSWORD: <EXTERNAL_SECRET>
```

dans un fichier versionné.

---

## 84.4 Secret dans le Dockerfile

```dockerfile
ENV SMTP_PASSWORD=<EXTERNAL_SECRET>
```

---

## 84.5 Migration au démarrage

```csharp
await dbContext.Database.MigrateAsync();
```

en production.

---

## 84.6 Données dans le conteneur

Les médias sont stockés uniquement dans le filesystem éphémère de l’image.

---

## 84.7 Modification manuelle du conteneur

```bash
docker exec
```

est utilisé pour éditer durablement un fichier applicatif.

---

## 84.8 Suppression implicite des volumes

Un script utilise automatiquement :

```bash
docker compose down --volumes
```

---

## 84.9 Staging identique au domaine production

Le staging utilise une route publique ambiguë ou indexable sans protection.

---

## 84.10 Données production en staging

Un dump réel est restauré sans anonymisation ni validation.

---

## 84.11 Rebuild différent en production

Une image est recompilée avec un code ou des dépendances différentes après la recette staging.

---

## 84.12 Déploiement depuis une branche locale non identifiée

Le serveur utilise une branche avec des modifications non committées.

---

## 84.13 Rollback sans image précédente

La documentation promet un rollback alors que l’ancien artefact n’est plus disponible.

---

## 84.14 Rollback base avec `Down` uniquement

Aucun backup n’est disponible.

---

## 84.15 AdminSeed permanent

Le seed administrateur reste actif après la mise en service.

---

## 84.16 Logs Debug permanents

Production conserve durablement un niveau Debug pour diagnostiquer un ancien incident.

---

# 85. Roadmap d’implémentation

## Phase déploiement 1 — Development

```text
PostgreSQL local
configuration Development
User Secrets
Docker Compose local
health checks
```

---

## Phase déploiement 2 — Image

```text
Dockerfile multi-stage
Release
non-root
metadata
tests
```

---

## Phase déploiement 3 — Compose production

```text
web
postgres
réseau
volumes
variables
health
```

---

## Phase déploiement 4 — Caddy

```text
HTTPS
reverse proxy
headers
logs
validation
```

---

## Phase déploiement 5 — Scripts opérateur

```text
preflight
deploy
smoke tests
rollback
status
```

---

## Phase déploiement 6 — Migrations

```text
bundle ou SDK temporaire
backup
application explicite
validation
```

---

## Phase déploiement 7 — Staging

```text
domaine
noindex
sandbox email
base isolée
recette
```

---

## Phase déploiement 8 — Registry

```text
image CI
scan
push
pull
tag
digest
```

---

## Phase déploiement 9 — Automatisation contrôlée

```text
deploy check
deploy plan
deploy apply
environnement protégé
approbation
rapport
```

---

## Phase déploiement 10 — Disponibilité renforcée

```text
deux instances
blue/green
health routing
rollback rapide
```

Cette phase reste hors MVP.

---

# 86. Checklist Development

```text
[ ] Configuration Development
[ ] PostgreSQL isolé
[ ] Données fictives
[ ] Secrets locaux hors Git
[ ] SMTP capturé
[ ] Seed fictif
[ ] Domaine .test
[ ] Noindex
[ ] Logs locaux
[ ] Migrations testables
```

---

# 87. Checklist Staging

```text
[ ] Image cible
[ ] Domaine distinct
[ ] HTTPS
[ ] Noindex
[ ] Accès protégé
[ ] Base distincte
[ ] Volumes distincts
[ ] Secrets distincts
[ ] Clés Data Protection distinctes
[ ] Données fictives ou anonymisées
[ ] SMTP sandbox ou allowlist
[ ] Migration testée
[ ] Smoke tests
[ ] Rollback testé
```

---

# 88. Checklist Production avant déploiement

```text
[ ] Version cible connue
[ ] Commit connu
[ ] CI verte
[ ] Image disponible
[ ] Scan image réussi
[ ] Configuration validée
[ ] Secrets présents
[ ] AdminSeed désactivé
[ ] Espace disque suffisant
[ ] Backup requis préparé
[ ] Migration inspectée
[ ] Rollback préparé
[ ] Compose valide
[ ] Caddy valide
[ ] Fenêtre de maintenance définie
```

---

# 89. Checklist Production après déploiement

```text
[ ] Conteneurs actifs
[ ] Liveness sain
[ ] Readiness sain
[ ] HTTPS actif
[ ] Certificat valide
[ ] Headers présents
[ ] Accueil accessible
[ ] Admin protégé
[ ] Login fonctionnel
[ ] Logout fonctionnel
[ ] Formulaire fonctionnel
[ ] Données persistées
[ ] SMTP contrôlé
[ ] PostgreSQL non public
[ ] Logs sans erreur critique
[ ] Aucun secret dans les logs
[ ] Aucun placeholder
[ ] Sitemap et robots cohérents
[ ] Backup vérifié
[ ] Rapport de déploiement produit
```

---

# 90. Checklist de rollback

```text
[ ] Cause identifiée
[ ] Écritures arrêtées si nécessaire
[ ] Version précédente disponible
[ ] Compatibilité base vérifiée
[ ] Backup disponible
[ ] Image précédente sélectionnée
[ ] Service recréé
[ ] Health sain
[ ] Smoke tests réussis
[ ] Données vérifiées
[ ] Incident documenté
```

---

# 91. Checklist de coexistence multi-client

```text
[ ] Projet Compose distinct
[ ] Réseau privé distinct
[ ] Base distincte
[ ] Volume PostgreSQL distinct
[ ] App_Data distinct
[ ] Data Protection distincte
[ ] Secrets distincts
[ ] Domaine distinct
[ ] Route Caddy distincte
[ ] Aucun port en conflit
[ ] Backups identifiables
[ ] Ressources suffisantes
[ ] Logs identifiables
```

---

# 92. Conclusion

La stratégie de déploiement SiteFactory doit garantir qu’un projet généré peut être mis en service sans transformer le VPS en environnement artisanal impossible à reproduire.

Elle repose sur :

```text
artefact versionné
+ configuration séparée
+ secrets isolés
+ volumes persistants
+ migrations explicites
+ backups
+ health checks
+ smoke tests
+ rollback
+ traçabilité
```

Le déploiement ne se limite pas à démarrer un conteneur.

Il doit permettre de répondre clairement à quatre questions :

```text
Quelle version est déployée ?

Les données sont-elles protégées ?

Le service fonctionne-t-il réellement ?

Comment revenir à un état stable en cas d’échec ?
```

> SiteFactory doit rendre un déploiement reproductible et contrôlable. Il ne doit jamais rendre une opération de production implicite ou irréversible.
