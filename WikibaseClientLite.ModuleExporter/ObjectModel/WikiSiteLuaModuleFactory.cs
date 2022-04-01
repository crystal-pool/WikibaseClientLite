using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Serilog;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikibaseClientLite.ModuleExporter.ObjectModel;

public class WikiSiteLuaModuleFactory : LuaModuleFactory
{

    private readonly ILogger logger;

    public WikiSiteLuaModuleFactory(WikiSite site, string titlePrefix, ILogger logger)
    {
        Site = site;
        TitlePrefix = titlePrefix;
        batchBlock = new BatchBlock<QueuedWritingTask>(50, new GroupingDataflowBlockOptions { BoundedCapacity = 100 });
        writingBlock = new ActionBlock<ICollection<QueuedWritingTask>>(WritingBlockActionAsync,
            new ExecutionDataflowBlockOptions { BoundedCapacity = 1, MaxDegreeOfParallelism = 1 });
        this.logger = logger.ForContext<WikiSiteLuaModuleFactory>();
        batchBlock.LinkTo(writingBlock, new DataflowLinkOptions { PropagateCompletion = true });
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
        return new MemoryBufferedLuaModule(page, this);
    }

    /// <inheritdoc />
    public override Task ShutdownAsync()
    {
        batchBlock.Complete();
        return writingBlock.Completion;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        batchBlock.Complete();
        base.Dispose(disposing);
    }

    private class QueuedWritingTask
    {

        public QueuedWritingTask(WikiPage page, string newContent, string summary)
        {
            Page = page;
            NewContent = newContent;
            Summary = summary;
        }

        public WikiPage Page { get; }

        public string NewContent { get; }

        public string Summary { get; }

    }

    private readonly BatchBlock<QueuedWritingTask> batchBlock;
    private readonly ActionBlock<ICollection<QueuedWritingTask>> writingBlock;

    private async Task WritingBlockActionAsync(ICollection<QueuedWritingTask> queued)
    {
        await queued.Select(t => t.Page).RefreshAsync(PageQueryOptions.None);
        var updatedRequests = 0;
        var updatedPages = 0;
        using (var sha1Provider = SHA1.Create())
        {
            foreach (var task in queued)
            {
                if (task.Page.ContentLength == Encoding.UTF8.GetByteCount(task.NewContent))
                {
                    var hash = Utility.BytesToHexString(sha1Provider.ComputeHash(Encoding.UTF8.GetBytes(task.NewContent)));
                    // Content is unchanged.
                    if (string.Equals(task.Page.LastRevision.Sha1, hash, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                task.Page.Content = task.NewContent;
                var oldRevid = task.Page.LastRevisionId;
                logger.Information("Updating {Page}.", task.Page);
                await task.Page.UpdateContentAsync(task.Summary, false, true);
                if (task.Page.LastRevisionId != oldRevid) updatedPages++;
                updatedRequests++;
            }
        }
        logger.Debug("Updated {Updated}/{Requests}/{Total} pages.", updatedPages, updatedRequests, queued.Count);
    }

    internal async Task QueuePageForWritingAsync(WikiPage page, string content, string summary)
    {
        await batchBlock.SendAsync(new QueuedWritingTask(page, content, summary));
    }

    private sealed class MemoryBufferedLuaModule : ILuaModule
    {

        private readonly WikiPage page;
        private readonly WikiSiteLuaModuleFactory owner;
        private readonly StringBuilder sb = new StringBuilder();
        private TextWriter writer;

        public MemoryBufferedLuaModule(WikiPage page, WikiSiteLuaModuleFactory owner)
        {
            Debug.Assert(page != null);
            Debug.Assert(owner != null);
            this.page = page;
            this.owner = owner;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            writer?.Dispose();
            writer = null;
            sb.Clear();
        }

        /// <inheritdoc />
        public TextWriter Writer
        {
            get
            {
                if (writer == null)
                {
                    writer = new StringWriter(sb) { NewLine = "\n" };
                }
                return writer;
            }
        }

        /// <inheritdoc />
        public async Task SubmitAsync(string editSummary)
        {
            string newContent;
            if (writer == null)
            {
                newContent = "";
            }
            else
            {
                writer.Close();
                writer = null;
                var contentLength = sb.Length;
                while (contentLength > 0 && char.IsWhiteSpace(sb[contentLength - 1]))
                    contentLength--;
                newContent = sb.ToString(0, contentLength);
            }
            await owner.QueuePageForWritingAsync(page, newContent, editSummary);
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
        public TextWriter Writer
        {
            get
            {
                EnsureWriter();
                return tempWriter;
            }
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
