# Processus externes des tests CLI

`CliProcessRunner` délègue chaque commande à `OwnedProcessRunner`. Les helpers
Git des tests `.gitignore` et PowerShell des diagnostics du harnais utilisent
la même frontière. Les builds/tests des projets générés et les hosts Web,
Worker et Migrator lancés par ces tests passent donc par ce cleanup.

## Propriété et terminaison

- Un superviseur privé confirme son isolation avant de recevoir la commande
  sur un pipe réservé à l'utilisateur courant. Aucun enfant de la commande
  ne peut démarrer avant cette confirmation.
- Windows : un Job Object par invocation, sans breakaway, contient le
  superviseur et ses descendants. Le runner termine le job et attend que
  son compteur de processus actifs soit nul. La fermeture du dernier handle
  du job déclenche aussi sa terminaison.
- Linux : un superviseur dans sa propre session est un `CHILD_SUBREAPER`.
  Il adopte les descendants orphelins, inspecte uniquement ses propres
  enfants via `/proc/self/task/*/children`, les termine via des **pidfds**
  puis les récolte. La fin du cleanup exige aussi `ECHILD` du noyau, pas
  seulement un snapshot `/proc` vide pendant une adoption concurrente.
  Un PID réutilisé ne peut pas désigner une cible différente
  après l'ouverture du pidfd. Cette voie nécessite Linux avec pidfd et les
  exports libc correspondants, disponibles dans l'environnement Linux testé.
  En dernier recours, seul le groupe de sa session encore ancrée est tué ;
  cette terminaison anormale fait échouer le runner.
- Le résultat de la commande est transmis indépendamment des flux de sortie.
  Le cleanup précède l'attente de leur EOF : un descendant ne peut donc pas
  conserver indéfiniment les pipes ouverts après la sortie du parent.
- Le délai de commande reste celui de l'appelant. Le cleanup a son propre
  budget de **10 secondes**, indépendant du token annulé ; le balayage/reaping
  Linux est borné à **5 secondes**. Un échec est explicite, sans arguments,
  contenu de commande ni secret dans son diagnostic.

Aucune recherche par nom `dotnet`/`MSBuild`, aucun shutdown global des build
servers, aucun délai ou retry de suppression de dossier ne remplace cette
preuve de terminaison. Il s'agit du cycle de vie des outils de test, pas d'une
sandbox d'exécution de plugins non fiables.

L'entrypoint privé de l'assembly de tests fournit le superviseur et la fixture
de processus ; les autres arguments sont délégués à l'entrypoint xUnit normal.
Aucun projet, package ou composant produit supplémentaire n'est introduit.

## Régression R1

`CliProcessRunnerTests` synchronise un parent et un descendant par pipes.
Le descendant conserve un fichier ouvert et ne sort pas spontanément.
Le parent peut terminer, être annulé ou dépasser son timeout. Le test exige
la terminaison des deux, la suppression du dossier temporaire et la survie
d'un processus témoin préexistant, lancé depuis le même exécutable.

La reproduction exécutée avant correction a échoué : après la sortie du
parent, le descendant survivait et les handles hérités empêchaient l'EOF,
jusqu'au timeout de l'ancien runner. La fixture libère ses propres processus
même lors de cette preuve négative.

Les E2E conservent leur suppression stricte des temporaires et leurs preuves
de génération/build/tests/régénération. Le harnais PostgreSQL conserve ses
shards, délais et assertions ; ses commandes .NET générées bénéficient du
runner corrigé. Le helper `git init` des tests de scanner Infrastructure,
sans lancement de build ou de host, n'est pas refactoré par cette correction.
