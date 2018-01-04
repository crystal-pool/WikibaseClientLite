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
            var exporter = new ItemsDumpModuleExporter
            {
                Languages = options["languages"]?.ToObject<IList<string>>(),
                Shards = (int?)options["shards"] ?? 1,
            };
            var destDir = (string)options["exportDirectory"];
            if (destDir != null)
            {
                var mf = new FileSystemLuaModuleFactory(destDir);
                using (var dumpReader = File.OpenText(sourceDump))
                {
                    await exporter.ExportItemsAsync(dumpReader, mf);
                }
            }
        }

    }
}
