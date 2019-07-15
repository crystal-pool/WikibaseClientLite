using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using WikibaseClientLite.ModuleExporter.ObjectModel;
using WikiClientLibrary.Sites;

namespace WikibaseClientLite.ModuleExporter.Tasks
{
    public static class OptionUtility
    {

        public static async Task<(WikiSite site, string title)> ResolveSiteAndTitleAsync(string expr, IWikiFamily wikiFamily)
        {
            if (string.IsNullOrEmpty(expr)) throw new ArgumentException("Value cannot be null or empty.", nameof(expr));
            var parts = expr.Split(':', 2);
            var site = await wikiFamily.GetSiteAsync(parts[0]);
            return (site, parts[1]);
        }

        public static async Task<LuaModuleFactory> CreateExportModuleFactoryAsync(string expr, IWikiFamily wikiFamily, ILogger logger)
        {
            if (string.IsNullOrEmpty(expr)) throw new ArgumentException("Value cannot be null or empty.", nameof(expr));
            var parts = expr.Split(':', 2);
            if (string.Equals(parts[0], "local"))
            {
                return new FileSystemLuaModuleFactory(parts[1]);
            }
            var site = await wikiFamily.GetSiteAsync(parts[1]);
            return new WikiSiteLuaModuleFactory(site, parts[1], logger);
        }

    }
}
