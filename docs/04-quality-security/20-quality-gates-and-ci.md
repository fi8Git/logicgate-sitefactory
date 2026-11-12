# LogicGate SiteFactory — Quality gates et intégration continue

Ce guide décrit la CI publique **opérationnelle** et les règles de qualité à
préserver. Une proposition de workflow ou un ADR accepté ne prouve pas qu’un
contrôle est exécuté. Les capacités du moteur restent celles du [README](../../README.md).

Sources de vérité : [workflow](../../.github/workflows/ci.yml),
[contrat d’exécution et d’agrégation](../../scripts/public-ci/README.md),
[harnais](../../scripts/public-ci/Invoke-PublicCi.ps1),
[validateurs](../../scripts/public-ci/PublicCiProof.ps1) et
[contre-épreuves](../../tests/PublicCi/Test-PublicCi.ps1).
Les détails d’implémentation restent dans ces sources, sans copie du YAML ici.

## CI actuellement exécutée

Le workflow versionné est **Public CI**, déclenché par une pull request, un push
sur main ou workflow_dispatch. Chaque PR, y compris documentaire, suit le
parcours complet ; aucun filtre de chemins ne dispense des contrôles requis.
Un déclenchement manuel reste disponible pour une campagne explicitement voulue,
sans doubler les checks automatiques d’une PR.

| Job exécuté | Preuve exigée |
|---|---|
| Public offline / Windows | Onze suites, 2 726 Passed, onze TRX |
| Public offline / Linux | Le même inventaire, 2 726 Passed, onze TRX |
| Public PostgreSQL REQUIRED / Linux | 23 Facts réels sur cinq shards, cinq TRX ; P03 768/768 HTTP 200 |
| Require complete public proof | Agrégation des trois rapports : SourceLink VERIFIED, 27 TRX, provenance, inventaires et cleanup conformes |

La matrice utilise windows-latest et ubuntu-latest ; PostgreSQL s’exécute sous
Linux. Les deux plateformes sont obligatoires, pas deux inventaires fonctionnels
distincts. La solution [SiteFactory.slnx](../../SiteFactory.slnx) contient
23 projets, dont onze projets de tests. Le SDK .NET 10 est déclaré dans
[global.json](../../global.json), avec sa politique de roll-forward : la version
déclarée n’est pas une mesure du SDK ou du runtime effectivement chargé.
Une version requise impossible à installer doit faire échouer le job.

Ces nombres sont les exigences des inventaires fermés, pas des résultats d’une
nouvelle exécution documentaire. Le [README](../../README.md#ci-limites-et-publication)
et [CONTRIBUTING](../../CONTRIBUTING.md#321-checks-attendus) donnent une référence
publique acquise avec son run, sa tentative et son SHA. Une preuve locale Windows
contre PostgreSQL Linux ne certifie pas une exécution .NET Linux distante.

## Isolation et contrôles effectifs

- Permissions GitHub limitées à contents: read, actions approuvées épinglées par
  SHA, checkout complet sans credentials persistés. Aucun secret ou accès
  propriétaire n’est nécessaire ; aucun secret de publication/production n’est
  fourni au code d’une PR.
- Le checkout vérifié fournit le SHA réellement exécuté. Pour une PR, il peut
  s’agir du commit de fusion de test, distinct du HEAD de la branche. La liste
  des 23 projets et les entrées autorisées sont contrôlées avant copie ; les
  octets et chemins du scratch sont vérifiés. Aucun projet inconnu, cycle,
  dépendance interdite, ressource externe non autorisée ou redirection ne doit
  pouvoir contourner cette sélection.
- Scratch et cache NuGet neufs et possédés par le job. Sources, packages et
  métadonnées Git temporaires nécessaires à SourceLink sont isolés du checkout ;
  aucun credential, historique synthétique ou configuration privée n’y est copié.
- Restore verrouillé, guards, format en vérification sans correction automatique,
  build Release avec analyseurs .NET, contrôle de la composition évaluée,
  des binaires, des symboles et des dépendances autorisées. Versions et sources
  NuGet restent explicites, centralisées et verrouillées ; une dérive de lock
  exige une modification revue. Un build Debug ne prouve pas une release.
- Les comptes offline par projet sont figés ; les identités du build viennent
  de sa discovery, après recoupement/exclusion des Facts PostgreSQL attendus. Les
  TRX sont comparés à cette discovery, pas utilisés comme leur propre oracle.
  L’inventaire PostgreSQL conserve ses 23 identités fermées explicites.
  Les tests [PostgreSQL REQUIRED](../../tests/PostgreSql/README.md) utilisent
  l’instance réelle éphémère, TLS VerifyFull et cinq shards séquentiels ; un mock
  ou une base en mémoire ne les remplace jamais.
- Les jobs offline exécutent la commande d’audit des vulnérabilités NuGet directes
  et transitives. Le script vérifie son code de sortie ; l’agrégateur ne relit
  pas un rapport de vulnérabilités et n’applique pas de seuil de sévérité.
  Ne pas assimiler le succès du job à une attestation d’absence de vulnérabilité.

### SourceLink et PDB

Le validateur lit les PDB produits : record SourceLink requis dans chaque PDB
concerné, repository et SHA attendus, clés non ambiguës couvrant les documents
suivis vers leurs chemins publics exacts, octets rapprochés des checksums PDB.
Un record absent/vide, une URL ou révision étrangère, un document non couvert
ou un mapping inattendu est refusé ; un autre PDB ne compense pas un record
manquant. Les sources générées et tierces verrouillées autorisées, notamment
xUnit, gardent leur traitement distinct. Une URL répondant 200 ne suffit pas.

## Provenance, tentatives et agrégation

Le check doit conserver exactement le nom **Require complete public proof**.
Il s’exécute après les jobs de preuve, même lorsqu’un besoin échoue, et exige :

1. Trois rapports Windows, Linux, PostgreSql, sans doublon ni manque ; résultats
   GitHub success, rapports PASSED, SourceLink VERIFIED et cleanup Completed.
2. Le même **SHA exécuté** et le même **runId**. Chaque tentative est un entier
   strictement positif, au plus égal à la tentative courante du workflow run.
   Ainsi 1/1/1 ou 2/1/1 peuvent être valides à la tentative 2 ; une tentative
   future, nulle ou invalide est refusée. Ne jamais réécrire la provenance d’un
   rapport plus ancien pour le faire passer pour la tentative courante.
3. Inventaires exacts, 27 TRX admissibles, identités, résultats, compteurs,
   multiplicités et durées cohérents avec chaque invocation ; fichiers présents
   et checksums conformes, y compris toute la preuve PostgreSQL transportée.
4. Pour PostgreSQL, union exacte des 20 Facts Infrastructure et 3 Hosts sur cinq
   shards, P03 complet et cleanup vérifié. Aucune preuve offline ne le remplace.

Tout artefact étranger, rouge, incomplet, dupliqué ou d’un autre SHA/run reste
refusé, même issu d’une tentative antérieure réussie en apparence. Une coche
GitHub ou un exit code isolé ne remplace pas la validation de la preuve.

## Échecs, cleanup et diagnostic

Un processus non nul reste FAILED, même avec des résultats partiels ou un TRX
apparemment Passed. TRX absent, invalide ou incomplet restent distincts ; les
valeurs non observées restent inconnues. Le diagnostic conserve suite fermée,
provenance, code de sortie réel, compteurs et identités/outcomes autorisés
réellement disponibles, sans arguments. Les identités paramétrées restent
expurgées et distinctes. L’archiveur de réussite ne transforme pas du rouge en vert.

Les diagnostics contrôlés sont écrits **avant suppression du scratch**. Leur
échec d’écriture ne doit pas empêcher le cleanup ; un défaut de reporting ou
cleanup empêche un succès complet. Supprimer uniquement ressources, processus,
secrets/certificats et métadonnées temporaires possédés, restaurer l’environnement
modifié, préserver le checkout et les ressources préexistantes. Le contrat
PostgreSQL exige aussi zéro base, rôle et session possédés résiduels avant
la destruction de son conteneur exact.

Seules les preuves contrôlées sont téléversées, y compris en échec, avec une
rétention de sept jours déclarée dans le workflow. Aucun stdout/stderr brut,
message d’exception, stack trace, argument de test, pièce jointe, secret,
chemin personnel, dump, clé ou configuration de production ne doit être publié.
Le masquage GitHub est complémentaire : il ne justifie jamais de journaliser un
secret, dont une forme encodée peut échapper au masquage.

Les jobs ont des bornes : 25 minutes offline, 120 minutes PostgreSQL, 5 minutes
agrégation ; le budget de 360 secondes PostgreSQL est **par shard**, avec des
budgets auxiliaires distincts. Une annulation n’est pas un succès. Les durées
proviennent des mesures des rapports ; une donnée absente reste UNKNOWN,
pas zéro. Fiabilité, sécurité et diagnostic priment sur durée et coût ; toute
optimisation doit préserver les preuves et démontrer son équivalence.

Pour un rouge : identifier la première étape réelle, distinguer produit et
runner, conserver la preuve expurgée, tenter une reproduction ciblée quand
possible et suivre tout défaut récurrent. Une relance doit avoir une raison
diagnostique précise ; aucun retry global ou relance identique pour espérer du
vert, ni augmentation arbitraire de délai. Ne pas appeler un échec « flaky »
sans preuve ou désactiver durablement un gate à cause d’un incident CI.

**P03-F1 reste OPEN / UNKNOWN** : historique **749 HTTP 200 / 19 HTTP 503**,
cause non démontrée, risque accepté avec réserve ; réexamen **2026-11-16**, sans
prolongation tacite. Une campagne verte ne prouve pas la cause corrigée.
Conserver les [observations historiques](../../tests/readiness-concurrency-regression.md).

## Règles de qualité et décisions humaines

Une PR, même avec un seul mainteneur, doit suivre [CONTRIBUTING](../../CONTRIBUTING.md) :
objectif borné, revue distincte de l’implémentation, tests adaptés, documentation,
risques, conversations bloquantes résolues et checks requis verts avant fusion.
Le squash est la stratégie de fusion par défaut ; la revue sensible recherche
une seconde lecture indépendante lorsqu’elle est disponible. L’état des réglages
GitHub n’est pas certifié par ce guide et aucune modification de protection
n’en découle. Une exception au push direct doit être formellement documentée.

Ne pas supprimer un test, réduire une assertion, masquer un échec avec
continue-on-error, ignorer un warning/analyzer ou modifier un check obligatoire
pour obtenir du vert. Une alerte d’outil externe ou de runtime d’action déprécié
doit être analysée et corrigée avant incompatibilité. Un workflow ne devient
pas opérationnel par la seule validité de son YAML ; un badge ne prouve pas
l’exécution d’un contrôle absent.

Une vulnérabilité critique bloque l’intégration/publication ; une haute aussi,
sauf mitigation validée. Les moyennes sont analysées et suivies, les faibles
peuvent aller au backlog. Sources inconnues et licences interdites doivent être
traitées. Ces règles de revue demeurent obligatoires même lorsqu’un outil
supplémentaire reste à configurer ; elles ne sont pas un seuil automatique
nouvellement attribué à l’audit NuGet courant.

Tout changement de plateforme, matrice, gate, seuil de couverture, outil
structurant, secret, permission d’écriture, feed/registry, versionnement,
publication, déploiement, protection, bypass ou auto-merge requiert une décision
humaine explicite. Une dérogation est temporaire, versionnée, liée à une tâche
et visible dans la PR/release : gate/erreur, raison, risque, compensation,
propriétaire, date, expiration et plan de correction. Elle ne permet pas une
nouvelle release exposée à une vulnérabilité critique sans mitigation formelle.
Après bypass autorisé : documenter, rejouer les checks dès que possible,
vérifier la branche, corriger la cause et rendre compte ; ne pas banaliser le bypass.

Incident de secret/artefact : interrompre la diffusion sans recopier le secret,
prévenir le responsable, révoquer/rotater sous autorisation, retirer ou déprécier
l’artefact, examiner accès/exécutions, corriger, revalider les gates et documenter.
Aucune opération de production, migration, publication, correction automatique
ou fusion n’est autorisée par une contribution ordinaire.

## Contrôles futurs, non livrés par ce workflow

Le repository ne contient actuellement que ci.yml sous .github/workflows/.
Les noms suivants sont des propositions ; aucun workflow supplémentaire ni
succès de scan n’est déduit de ce tableau.

| Proposition | Règles conservées pour sa réalisation |
|---|---|
| generated-project.yml | Valider une sortie par les vrais contrats/CLI et des fixtures explicites, sans raccourci interne. Le site Documentation actuel est statique, pas une application .NET. Un futur template produisant réellement du .NET devra prouver restore/build/tests, sécurité, lock/rapport, reproductibilité et non-écrasement. |
| security.yml | Scans de secrets, SAST et supply chain selon les outils approuvés. Détections bloquantes jamais ignorées silencieusement ; exclusions localisées, justifiées, documentées, testées et révisées. Gitleaks, Semgrep, CodeQL et scans d’images ne sont pas configurés par ci.yml ; leur absence n’est pas un résultat vert. |
| nightly.yml | Tests coûteux, navigateur, performance, restore drill, compatibilité et scans approfondis selon les capacités livrées. Échec signalé, analysé et suivi par une tâche ; les erreurs essentielles restent détectées sur PR. |
| release.yml | Publication explicite et protégée ; jamais déclenchée implicitement par un merge main ou publiée depuis une PR. Pas de test de pipeline par publication d’un vrai package stable depuis une branche de travail. |
| dependency-review.yml | Examiner nouvelles dépendances directes/transitives, vulnérabilités, sources, licences et versions ; risque non accepté bloquant. |

Ces exigences ne sont pas révoquées par l’absence des outils. Elles s’appliquent
aux composants concernés lorsqu’ils sont introduits. Ne pas créer des workflows,
projets ou catégories de tests vides pour satisfaire un exemple documentaire.

- **Publication** : aucun package NuGet ni release officielle disponible.
  Les futurs packages publics suivent [ADR-0035](../adr/0035-partage-public-component-via-nuget.md)
  et [ADR-0036](../adr/0036-versioner-sitefactory-and-its-modules-with-semver-and-explicit-compatibility.md) :
  admission des IDs, source et version explicites, immutabilité. Une correction
  produit une nouvelle version ; tag, commit, version et artefacts doivent coïncider.
  Exiger main verte, Windows/Linux, tests complets, sorties générées réellement
  applicables, migrations/upgrade des versions supportées, scans, packages inspectés,
  documentation, changelog et notes (compatibilité, actions manuelles, dépréciations,
  limites). SBOM, provenance, checksums/signatures prévus ne contiennent ni secret,
  donnée client ni chemin privé inutile. Une release candidate ajoute pilote et
  validation manuelle. Un build réussi n’autorise aucune publication.
- **Secrets et confiance** : permissions minimales explicites par job, credentials
  jamais dans configuration versionnée, arguments visibles, logs, caches ou
  artefacts. Publication/production utilisent des environnements protégés,
  approbation humaine et secrets minimaux remplaçables sans changer les workflows.
  Aucun checkout de code non fiable avant exposition d’un secret, usage dangereux
  de pull_request_target, interpolation shell d’entrée non fiable ou écriture
  globale. Tout nouveau workflow est revu sur triggers, conditions, chemins,
  expressions, scripts, plateformes, permissions, secrets et nom des checks.
- **Isolation et performance** : aucun cache partagé ni cache de build actuel.
  Un éventuel cache doit être validé selon le niveau de confiance de l’événement,
  l’OS, le SDK, les locks et la configuration NuGet ; jamais de secret ou artefact
  sensible, et build/restitution propres avant release. Ni filtre de chemins,
  parallélisme ou annulation supplémentaire sans équivalence démontrée. Ne pas
  omettre Windows, test critique, génération applicable ou analyse pour gagner du
  temps ; aucun artefact ancien non vérifié. Une release ne doit pas être annulée
  par une nouvelle exécution sans décision ; déploiement futur sérialisé par cible.
- **Indicateurs et maintenance** : couverture, mutation testing, scans navigateur
  ou visuels et seuils de performance ne sont pas des résultats de cette CI.
  Mesurer la baseline avant tout seuil validé, rendre les indicateurs visibles et
  suivis avec date/condition de passage en gate ; pas d’assertions sans valeur
  pour gonfler un score. Les mises à jour compatibles peuvent être regroupées,
  les majeures/sensibles restent séparées, les alertes de sécurité prioritaires ;
  aucun auto-merge implicite. macOS ou une matrice multi-.NET exigent un besoin et
  une décision de support explicite.

## Exécution locale et références

Les [commandes locales](../../CONTRIBUTING.md#30-commandes-de-validation),
[guards et preuve isolée](../../CONTRIBUTING.md#301-guards-et-preuve-publique-isolée)
et [PostgreSQL REQUIRED](../../CONTRIBUTING.md#302-postgresql-required-distinct)
sont maintenus dans CONTRIBUTING. Le workflow utilise les modes Offline,
PostgreSql et Aggregate du harnais ; une exécution locale ne devient pas une
preuve distante par simple similitude de commandes.

Avant modification, lire [AGENTS](../../AGENTS.md), le périmètre/les contrats
concernés, les scripts, tests, projets, configuration SDK/dépendances et revue
applicable. La [stratégie de tests](19-testing-strategy.md) définit les scénarios ;
la [baseline de sécurité](18-security-baseline.md) conserve les exigences détaillées.
Le compte rendu indique uniquement contrôles, plateformes, mesures et résultats
observés, éléments non exécutés et décisions restantes ; jamais « tout testé »
à partir des seuls tests existants verts.
