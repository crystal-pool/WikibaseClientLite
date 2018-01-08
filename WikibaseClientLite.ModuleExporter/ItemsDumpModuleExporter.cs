using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using WikibaseClientLite.ModuleExporter.ObjectModel;
using WikiClientLibrary.Wikibase;
using WikiClientLibrary.Wikibase.DataTypes;

namespace WikibaseClientLite.ModuleExporter
{
    public class ItemsDumpModuleExporter
    {
        private int _Shards = 13;
        private static readonly string[] defaultLanguages = { "en-us", "en" };

        public ItemsDumpModuleExporter(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Number of shards.
        /// </summary>
        public int Shards
        {
            get { return _Shards; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _Shards = value;
            }
        }

        public IList<string> Languages { get; set; }

        public ILogger Logger { get; }

        private void WriteProlog(ILuaModule module, int shard)
        {
            module.AppendLine("----------------------------------------");
            module.AppendLine("-- Powered by WikibaseClientLite");
            module.AppendLine($"-- Shard: {shard}/{_Shards}");
            module.AppendLine("----------------------------------------");
            module.AppendLine();
            module.AppendLine("local data = {");
        }

        private void WriteEpilog(ILuaModule module)
        {
            module.AppendLine();
            module.AppendLine("}");
            module.AppendLine();
            module.AppendLine("return data");
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

        public async Task ExportItemsAsync(TextReader itemsDumpReader, LuaModuleFactory moduleFactory)
        {
            if (itemsDumpReader == null) throw new ArgumentNullException(nameof(itemsDumpReader));
            if (moduleFactory == null) throw new ArgumentNullException(nameof(moduleFactory));
            var languages = new List<string>(Languages ?? defaultLanguages);
            var shards = Enumerable.Range(0, _Shards).Select(index =>
            {
                var module = moduleFactory.GetModule(index.ToString());
                WriteProlog(module, index + 1);
                return module;
            }).ToList();
            try
            {
                int items = 0, properties = 0;
                var isFirstEntity = new BitArray(_Shards, true);
                using (var jreader = new JsonTextReader(itemsDumpReader))
                {
                    if (jreader.Read())
                    {
                        if (jreader.TokenType != JsonToken.StartArray) throw new JsonException("Expect StartArray token.");
                        while (jreader.Read() && jreader.TokenType != JsonToken.EndArray)
                        {
                            if (jreader.TokenType != JsonToken.StartObject) throw new JsonException("Expect StartObject token.");
                            var entity = SerializableEntity.Load(jreader);
                            if (entity.Type == EntityType.Item) items++;
                            else if (entity.Type == EntityType.Property) properties++;
                            var shardIndex = Utility.HashItemId(entity.Id) % _Shards;
                            var shard = shards[shardIndex];
                            entity.Labels = FilterMonolingualTexts(entity.Labels, languages);
                            entity.Descriptions = FilterMonolingualTexts(entity.Descriptions, languages);
                            entity.Aliases = FilterMonolingualTexts(entity.Aliases, languages);
                            if (isFirstEntity[shardIndex])
                                isFirstEntity[shardIndex] = false;
                            else
                                shard.AppendLine(",");
                            shard.Append(entity.Id);
                            shard.Append(" = ");
                            shard.Append("[==========[");
                            shard.Append(entity.ToJsonString());
                            shard.Append("]==========]");
                        }
                    }
                }
                Logger.Information("Exported LUA modules for {Items} items and {Properties} properties.", items, properties);
                for (var i = 0; i < shards.Count; i++)
                {
                    Logger.Information("Submitting shard: {Current}/{Total}.", i + 1, _Shards);
                    WriteEpilog(shards[i]);
                    await shards[i].SubmitAsync($"Export Wikibase items. Shard {i + 1}/{_Shards}");
                }
            }
            finally
            {
                foreach (var s in shards) s.Dispose();
            }
        }

    }
}
