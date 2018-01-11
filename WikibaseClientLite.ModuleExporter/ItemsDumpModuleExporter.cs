using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Luaon.Json;
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

        private void WriteProlog(TextWriter writer, string id, string refLabel)
        {
            writer.WriteLine("----------------------------------------");
            writer.WriteLine("-- Powered by WikibaseClientLite");
            writer.WriteLine("-- Entity: {0} ({1})", id, refLabel);
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

        public async Task ExportItemsAsync(TextReader itemsDumpReader, LuaModuleFactory moduleFactory)
        {
            if (itemsDumpReader == null) throw new ArgumentNullException(nameof(itemsDumpReader));
            if (moduleFactory == null) throw new ArgumentNullException(nameof(moduleFactory));
            var languages = new List<string>(Languages ?? defaultLanguages);
            int items = 0, properties = 0;
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
                        else if (entity.Type == EntityType.Property)
                            properties++;

                        // Preprocess
                        entity.Labels = FilterMonolingualTexts(entity.Labels, languages);
                        entity.Descriptions = FilterMonolingualTexts(entity.Descriptions, languages);
                        entity.Aliases = FilterMonolingualTexts(entity.Aliases, languages);

                        // Persist
                        using (var module = moduleFactory.GetModule(entity.Id))
                        {
                            using (var writer = module.GetWriter())
                            {
                                WriteProlog(writer, entity.Id, entity.Labels["en"]);
                                using (var luawriter = new JsonLuaWriter(writer) {CloseOutput = false})
                                {
                                    entity.WriteTo(luawriter);
                                }

                                WriteEpilog(writer);
                            }

                            await module.SubmitAsync($"Export entity {entity.Id}.");
                        }

                        Logger.Information("Exported LUA modules for {Items} items and {Properties} properties.", items, properties);
                    }
                }
            }
        }

    }
}
