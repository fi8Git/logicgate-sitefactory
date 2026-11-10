# Repository Git local vers site documentaire

## Chaîne publique disponible

`LocalGitRepositoryReader` → `RepositoryDocumentationScanner` →
`DocumentationModelLoader`/`DocumentationSite` →
`DocumentationModule.CreateContribution(site)` →
`GenerationComposition` → plan → exécuteur physique existant.

Le module n'a pas de second moteur de génération. Il réutilise
`GenerationPlan`, `PlannedFile`, stratégies, path safety, writer atomique,
lock, rapport et état.
Cette chaîne est accessible par **composition C#**.
La CLI publique compose Documentation, mais génère ses pages embarquées :
aucune commande d'import de repository n'est encore exposée.

## Sources et modèle

Le lecteur vérifie un repository Git local explicite sans modifier `.git`,
cloner ou contacter un remote. Les seules sources V1 sont README principal,
variantes README autorisées et Markdown sous docs, avec exclusions explicites
de `.git`, `bin`, `obj` et autres répertoires non documentaires.
Ordre ordinal stable ; chemins relatifs et logical IDs bornés.

Le scanner ne suit pas de symlink permettant une sortie de racine.
La lecture UTF-8 et les limites de profondeur, nombre et taille sont bornées.
Le modèle indépendant du rendu déduit les titres du premier heading valide,
puis d'un fallback déterministe. Navigation dérivée des paths ;
routes/logical IDs en collision ou invalides refusés.

[Scanner concret](../../src/LogicGate.SiteFactory.Modules.Documentation/Scanning/RepositoryDocumentationScanner.cs)
et [loader](../../src/LogicGate.SiteFactory.Modules.Documentation/Model/DocumentationModelLoader.cs).

## Site et sécurité du contenu

README devient home ; docs imbriqués deviennent pages et sections de navigation.
Les liens internes autorisés sont résolus et projetés vers les routes HTML.
Le rendu du modèle utilise Markdig avec HTML brut désactivé, liens contrôlés
et éléments de layout encodés. Aucun script ou iframe arbitraire n'est activé.
Le rendu embarqué, plus limité, échappe également les textes.

Le scénario public README + Getting Started + Architecture/Overview produit
home, deux pages, CSS, lock, rapport et état : sept artefacts pour **ce scénario**,
pas un compteur universel.
Le site statique n'a pas de projet .NET : pas de restore/build/test de sortie
à annoncer. La solution SiteFactory est validée séparément.

## Assertions E2E existantes

[Tests de génération](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.Generation.cs)
et [E2E repository](../../tests/LogicGate.SiteFactory.Infrastructure.Tests/Repositories/RepositoryDocumentationScannerTests.EndToEnd.cs) :

- home, contenu, navigation et chaque cible/fragment de lien ;
- seules les sources autorisées ; `.git` byte-identical avant/après ;
- même moteur pour Documentation et la contribution fictive publique ;
- update de contenu autorisé, changements attendus seulement ;
- régénération stable, lock déterministe, customizations préservées ;
- deux racines identiques → mêmes octets, rapport et état inclus ;
- traversal, symlink sortant, collision de routes et projection fichier/dossier ;
- contenu malveillant neutralisé, sortie liée et dernier nom invalide refusés
  avant création des premiers fichiers ;
- état/hash géré, modification tardive et structure changée refusés.

Ces preuves locales acquises ne sont pas rejouées ici.
Le scénario Business historique utilise ses propres exécutables/fixtures privés ;
il ne constitue pas une dépendance de ces tests publics.
Le [harnais PostgreSQL public](../../tests/PostgreSql/README.md) valide les
bibliothèques runtime séparément : 20 Facts Infrastructure + 3 Hosts,
pas les vingt Facts privés historiques.
CI distante Windows/Linux non validée ; aucun GitHub Pages ou déploiement livré.
