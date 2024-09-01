using System.Diagnostics;
using Luaon;
using Luaon.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using WikibaseClientLite.ModuleExporter.ObjectModel;
using WikiClientLibrary.Wikibase;
using WikiClientLibrary.Wikibase.DataTypes;
using Formatting = Luaon.Formatting;

namespace WikibaseClientLite.ModuleExporter;

public class DataModulesExporter
{

    private static readonly string[] defaultLanguages = { "en-us", "en" };

    public DataModulesExporter(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ClientSiteName { get; set; }

    public IList<string> Languages { get; set; }

    public ILogger Logger { get; }

    public TimeSpan StatusReportInterval { get; set; } = TimeSpan.FromSeconds(20);

    private void WriteProlog(TextWriter writer, string prologText)
    {
        writer.WriteLine("----------------------------------------");
        writer.WriteLine("-- Powered by WikibaseClientLite");
        writer.Write("-- ");
        writer.WriteLine(prologText);
        writer.WriteLine("----------------------------------------");
        writer.WriteLine();
        writer.Write("local data = ");
    }

    private void WriteEpilog(TextWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine();
        writer.WriteLine("return data");
    }

    private static WbMonolingualTextCollection FilterMonolingualTexts(WbMonolingualTextCollection collection, ICollection<string> languages)
    {
        var c = new WbMonolingualTextCollection();
        foreach (var l in languages)
        {
            c[l] = collection[l];
        }

        return c;
    }

    private static WbMonolingualTextsCollection FilterMonolingualTexts(WbMonolingualTextsCollection collection, ICollection<string> languages)
    {
        var c = new WbMonolingualTextsCollection();
        foreach (var l in languages)
        {
            c[l] = collection[l];
        }

        return c;
    }

    public async Task ExportItemsAsync(Stream itemDumpStream, LuaModuleFactory moduleFactory)
    {
        ArgumentNullException.ThrowIfNull(itemDumpStream);
        ArgumentNullException.ThrowIfNull(moduleFactory);

        var languages = new List<string>(Languages ?? defaultLanguages);
        int items = 0, properties = 0;
        var statusReportSw = Stopwatch.StartNew();
        await foreach (var entity in SerializableEntity.LoadAllAsync(itemDumpStream))
        {
            if (entity == null) continue;
            if (entity.Type == EntityType.Item) items++;
            else if (entity.Type == EntityType.Property)
                properties++;

            // Preprocess
            entity.Labels = FilterMonolingualTexts(entity.Labels, languages);
            entity.Descriptions = FilterMonolingualTexts(entity.Descriptions, languages);
            entity.Aliases = FilterMonolingualTexts(entity.Aliases, languages);
            foreach (var c in entity.Claims)
            {
                // Mitigates 4458a8b567a3e7c5efd3f693af23ec95e86870d4
                // TODO Cleanup mitigation after next version of WCL
                if (c.MainSnak.Hash == "") c.MainSnak.Hash = null!;
                foreach (var s in c.References.SelectMany(r => r.Snaks))
                {
                    if (s.Hash == "") s.Hash = null!;
                }
            }

            // Persist
            using (var module = moduleFactory.GetModule(entity.Id))
            {
                await using (var writer = module.Writer)
                {
                    WriteProlog(writer, $"Entity: {entity.Id} ({entity.Labels["en"]})");
                    await using (var luawriter = new JsonLuaWriter(writer) { CloseOutput = false })
                    {
                        // https://github.com/JamesNK/Newtonsoft.Json/issues/2910
                        // Seems nobody cares
                        // TODO Remove JSON node conversion
                        await JToken.Parse(entity.ToJsonString()).WriteToAsync(luawriter);
                    }

                    WriteEpilog(writer);
                }

                await module.SubmitAsync($"Export entity {entity.Id}.");
            }

            if (statusReportSw.Elapsed > StatusReportInterval)
            {
                statusReportSw.Restart();
                Logger.Information("Exported LUA modules for {Items} items and {Properties} properties.", items, properties);
            }
        }
        Logger.Information("Exported LUA modules for {Items} items and {Properties} properties.", items, properties);
    }

    public async Task ExportSiteLinksAsync(Stream itemDumpStream, LuaModuleFactory moduleFactory, int shardCount)
    {
        ArgumentNullException.ThrowIfNull(itemDumpStream);
        if (moduleFactory == null) throw new ArgumentNullException(nameof(moduleFactory));
        if (shardCount <= 0) throw new ArgumentOutOfRangeException(nameof(shardCount));
        if (ClientSiteName == null) throw new ArgumentNullException(nameof(ClientSiteName));

        var shards = Enumerable.Range(0, shardCount).Select(index =>
        {
            var module = moduleFactory.GetModule(index.ToString());
            WriteProlog(module.Writer, $"Shard: {index + 1}/{shardCount}");
            return module;
        }).ToList();
        var shardLuaWriters = shards.Select(m =>
                new LuaTableTextWriter(m.Writer) { CloseWriter = false, Formatting = Formatting.Prettified })
            .ToList();
        foreach (var writer in shardLuaWriters) writer.WriteStartTable();
        try
        {
            foreach (var entity in SerializableEntity.LoadAll(itemDumpStream))
            {
                var siteLink = entity?.SiteLinks.FirstOrDefault(l => l.Site == ClientSiteName);
                if (siteLink == null) continue;
                var shardIndex = Utility.HashString(siteLink.Title) % shardCount;
                var writer = shardLuaWriters[shardIndex];
                writer.WriteKey(siteLink.Title);
                writer.WriteLiteral(entity.Id);
            }

            Logger.Information("Exporting LUA modules. Shards = {Shards}", shards.Count);
            for (var i = 0; i < shards.Count; i++)
            {
                shardLuaWriters[i].WriteEndTable();
                shardLuaWriters[i].Close();
                WriteEpilog(shards[i].Writer);
                await shards[i].SubmitAsync($"Export SiteLink table. Shard {i + 1}/{shards.Count}.");
            }
        }
        finally
        {
            foreach (var s in shards) s.Dispose();
        }
    }

}
