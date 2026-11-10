# Régénération reconnue et preuves end-to-end publiques

## Reconnaissance de la cible

L'exécution initiale exige une cible absente. Une cible existante, même vide,
n'est acceptée que si `.sitefactory/generation-state.json` est valide.
Le document d'état lie les versions du générateur et du template, la
configuration structurelle, la liste des fichiers et leurs hashes.
Une erreur de syntaxe, schéma, type, Unicode ou hash produit
`SF-GEN-STATE-001` et le code métier `6`, sans écriture ni stack trace.

Aucune adoption d'une sortie ancienne, réparation d'état ou migration implicite.
Un changement de configuration/template/générateur ou de structure est refusé,
pas transformé en upgrade. Ajouter, retirer ou renommer une page documentaire
reste un changement de structure non autorisé par cette coupe.

## Stratégies et conflits

- `generated` : preuve par hash du dernier contenu, jamais overwrite silencieux.
- `create-once` : création si absent ; existant préservé.
- `custom` : appartient au client ; aucune écriture automatique.
- `protected` : interdit toute écriture, création comprise.
- `merge` : pas de fusion improvisée ; contrat spécifique requis.

Les cinq stratégies suivent [ADR-0005](../adr/0005-file-generation-strategies.md).
Le préflight contrôle toutes les destinations avant la première écriture,
y compris le dernier fichier physiquement invalide, collisions, traversal,
liens et `.git`. Les observations sont recontrôlées avant application.
Une modification tardive d'état ou de fichier géré provoque un conflit,
pas l'application d'un plan recalculé.

Le writer est atomique **par fichier seulement**. Une panne I/O/annulation peut
laisser des fichiers déjà écrits ; l'état de propriété ne doit pas être avancé
sur une mise à jour incomplète. Ce n'est pas une transaction filesystem globale.

## Scénarios publics acquis

Les [tests de régénération](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Generation/RegenerationTests.cs)
et [tests d'état](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Generation/GenerationStateTests.cs)
prouvent stabilité, état corrompu, customizations et refus.
Les [tests documentaires](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.Generation.cs)
comparent deux repositories identiques dans deux racines : mêmes fichiers
canoniques, y compris premier rapport et état.

Les [E2E](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.EndToEnd.cs)
modifient une source autorisée sans changer titres/navigation/structure.
Seuls la page attendue, le rapport et l'état changent ; le lock reste stable.
Après stabilisation, une nouvelle génération conserve les octets et dates
des fichiers inchangés. Une édition de sortie gérée est refusée même si la
source change aussi. Le snapshot préparé est appliqué, pas une nouvelle lecture
silencieuse d'une source modifiée entre plan et exécution.

Le même moteur génère ensuite la fixture d'arborescence publique ; son README
customisé reste préservé. Ses fichiers sentinelles ne prouvent pas un build .NET.
Aucun module ou manifest Business n'est nécessaire.

## Artefacts et portée de la preuve

`sitefactory.lock.json` représente la composition résolue sans secret ;
`generation-report.json` distingue créations, modifications, préservations,
conflits et diagnostics ; l'état est le document interne de reconnaissance.
Les données opérationnelles non déterministes ne peuvent être exclues d'une
comparaison que si leur contrat le documente explicitement.

Les preuves de build/test de la solution publique et les
[23 Facts PostgreSQL](../../tests/PostgreSql/README.md) sont séparées des E2E
documentaires ; elles ne sont pas rejouées pour cette adaptation.
Les anciens compteurs de fichiers/tests clients concernent la suite Business
privée, pas la contribution Documentation publique.
Aucune validation distante Windows/Linux ni SourceLink public n'est prétendue.

P03-F1 conserve **OPEN / UNKNOWN**, 749 HTTP 200 / 19 HTTP 503 historiques,
cause non démontrée, risque accepté avec réserve ; réexamen **2026-10-23**.
Aucune campagne ultérieure verte ne vaut correction causale.
