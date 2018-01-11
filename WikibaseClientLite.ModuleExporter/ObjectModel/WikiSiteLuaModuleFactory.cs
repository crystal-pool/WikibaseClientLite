using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikibaseClientLite.ModuleExporter.ObjectModel
{
    public class WikiSiteLuaModuleFactory : LuaModuleFactory
    {

        public WikiSiteLuaModuleFactory(WikiSite site, string titlePrefix)
        {
            Site = site;
            TitlePrefix = titlePrefix;
        }

        /// <summary>
        /// The MediaWiki site to publish the modules.
        /// </summary>
        public WikiSite Site { get; }

        /// <summary>
        /// Prefix of the LUA module titles, including <c>Module:</c> namespace prefix.
        /// </summary>
        public string TitlePrefix { get; }

        /// <inheritdoc />
        public override ILuaModule GetModule(string title)
        {
            var page = new WikiPage(Site, TitlePrefix + title);
            // return new LuaModule(page);
            return new MemoryBufferedLuaModule(page);
        }

        private sealed class MemoryBufferedLuaModule : ILuaModule
        {

            private readonly WikiPage page;
            private readonly StringBuilder sb = new StringBuilder();
            private TextWriter writer;

            public MemoryBufferedLuaModule(WikiPage page)
            {
                Debug.Assert(page != null);
                this.page = page;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                writer?.Dispose();
                writer = null;
                sb.Clear();
            }

            /// <inheritdoc />
            public TextWriter GetWriter()
            {
                if (writer == null) writer = new StringWriter(sb);
                return writer;
            }

            /// <inheritdoc />
            public async Task SubmitAsync(string editSummary)
            {
                if (writer == null)
                {
                    page.Content = "";
                }
                else
                {
                    writer.Close();
                    writer = null;
                    page.Content = sb.ToString();
                }
                await page.UpdateContentAsync(editSummary, false, true);
            }
        }

        private sealed class LuaModule : ILuaModule
        {

            private readonly WikiPage page;
            private string tempFileName;
            private TextWriter tempWriter;

            public LuaModule(WikiPage page)
            {
                Debug.Assert(page != null);
                this.page = page;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                tempWriter?.Dispose();
                tempWriter = null;
                if (tempFileName != null)
                    File.Delete(tempFileName);
            }

            private void EnsureWriter()
            {
                if (tempWriter == null)
                {
                    var fileName = Path.GetTempFileName();
                    tempFileName = fileName;
                    tempWriter = File.CreateText(fileName);
                }
            }

            /// <inheritdoc />
            public TextWriter GetWriter()
            {
                EnsureWriter();
                return tempWriter;
            }

            /// <inheritdoc />
            public async Task SubmitAsync(string editSummary)
            {
                if (tempWriter == null)
                {
                    page.Content = "";
                }
                else
                {
                    tempWriter.Close();
                    tempWriter = null;
                    page.Content = await File.ReadAllTextAsync(tempFileName);
                }
                await page.UpdateContentAsync(editSummary, false, true);
            }
        }

    }
}
