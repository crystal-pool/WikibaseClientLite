using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WikibaseClientLite.ModuleExporter.ObjectModel;

namespace WikibaseClientLite.ModuleExporter
{
    partial class TaskActionDispatcher
    {

        public async Task ExportItemsAction(JObject options)
        {
            var sourceDump = (string)options["dumpFile"];
            var exporter = new ItemsDumpModuleExporter(logger)
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
                using (var mf = new WikiSiteLuaModuleFactory(await mwSiteProvider.GetWikiSiteAsync(destSite),
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
            var exporter = new ItemsDumpModuleExporter(logger)
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
                using (var mf = new WikiSiteLuaModuleFactory(await mwSiteProvider.GetWikiSiteAsync(destSite),
                    (string)options["exportSitePrefix"], logger))
                using (var dumpReader = File.OpenText(sourceDump))
                {
                    await exporter.ExportSiteLinksAsync(dumpReader, mf, shards);
                    await mf.ShutdownAsync();
                }
            }
        }

    }
}
