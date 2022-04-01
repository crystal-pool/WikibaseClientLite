using Newtonsoft.Json.Linq;
using VDS.RDF;
using WikibaseClientLite.ModuleExporter.ObjectModel;
using WikibaseClientLite.ModuleExporter.Sparql;
using WikibaseClientLite.ModuleExporter.Tasks;

namespace WikibaseClientLite.ModuleExporter;

partial class TaskActionDispatcher
{

    public async Task ExportItemsAction(JObject options)
    {
        var sourceDump = (string)options["dumpFile"];
        var exporter = new DataModulesExporter(logger)
        {
            Languages = options["languages"]?.ToObject<IList<string>>(),
        };
        var destDir = (string)options["exportDirectory"];
        if (destDir != null)
        {
            using (var mf = new FileSystemLuaModuleFactory(destDir))
            using (var dumpReader = File.OpenText(sourceDump))
            {
                await exporter.ExportItemsAsync(dumpReader, mf);
                await mf.ShutdownAsync();
            }
        }
        var destSite = (string)options["exportSite"];
        if (destSite != null)
        {
            using (var mf = new WikiSiteLuaModuleFactory(await mwSiteProvider.GetSiteAsync(destSite),
                       (string)options["exportSitePrefix"], logger))
            using (var dumpReader = File.OpenText(sourceDump))
            {
                await exporter.ExportItemsAsync(dumpReader, mf);
                await mf.ShutdownAsync();
            }
        }
    }

    public async Task ExportSiteLinksAction(JObject options)
    {
        var sourceDump = (string)options["dumpFile"];
        var exporter = new DataModulesExporter(logger)
        {
            ClientSiteName = (string)options["clientSiteName"]
        };
        var shards = (int?)options["shards"] ?? 1;
        var destDir = (string)options["exportDirectory"];
        if (destDir != null)
        {
            using (var mf = new FileSystemLuaModuleFactory(destDir))
            using (var dumpReader = File.OpenText(sourceDump))
            {
                await exporter.ExportSiteLinksAsync(dumpReader, mf, shards);
                await mf.ShutdownAsync();
            }
        }
        var destSite = (string)options["exportSite"];
        if (destSite != null)
        {
            using (var mf = new WikiSiteLuaModuleFactory(await mwSiteProvider.GetSiteAsync(destSite),
                       (string)options["exportSitePrefix"], logger))
            using (var dumpReader = File.OpenText(sourceDump))
            {
                await exporter.ExportSiteLinksAsync(dumpReader, mf, shards);
                await mf.ShutdownAsync();
            }
        }
    }

    public async Task ExecuteAotSparqlAction(AotSparqlOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        using (var namespaceMap = new NamespaceMapper())
        {
            foreach (var entry in options.NamespaceMapping)
                namespaceMap.AddNamespace(entry.Key, new Uri(entry.Value));
            using (var moduleFactory = await OptionUtility.CreateExportModuleFactoryAsync(options.ExportModulePrefix, mwSiteProvider, logger))
            using (var executor = new AotSparqlExecutor(options.DataSource, namespaceMap, uri =>
                   {
                       // Blank node.
                       if (uri == null) return "_:";
                       // One.
                       if (uri == Utility.WikibaseRdfUnityEntity) return "1";
                       if (namespaceMap.ReduceToQName(uri.ToString(), out var qname))
                       {
                           // Remove prefix for local entities.
                           if (qname.StartsWith("wd:", StringComparison.OrdinalIgnoreCase))
                               return qname.Substring(3);
                           return qname;
                       }
                       throw new InvalidOperationException($"Cannot reduce {uri} into its QName.");
                   }, logger.ForContext<AotSparqlExecutor>()))
            {
                var exporter = new AotSparqlModuleExporter(logger, moduleFactory, executor);
                {
                    var (configSite, configModule) = await OptionUtility.ResolveSiteAndTitleAsync(options.ConfigModule, mwSiteProvider);
                    await exporter.LoadConfigFromModuleAsync(configSite, configModule);
                    logger.Information("Loaded config from {Module}.", options.ConfigModule);
                }
                await exporter.ExportModulesAsync();
            }
        }
    }

}
