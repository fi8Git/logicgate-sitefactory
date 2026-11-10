using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Cli;
using LogicGate.SiteFactory.Infrastructure.Templates;
using LogicGate.SiteFactory.Modules.Documentation;

DocumentationModule module = new(new TemplateManifestJsonReader());
ModuleCatalog modules = new([new(module.Descriptor.Id.Value, module.Descriptor.Version.Value, module)]);
GenerationComposition composition = new(modules, [module.CreateContribution()]);

return await SiteFactoryCli.RunConsoleAsync(args, composition);
