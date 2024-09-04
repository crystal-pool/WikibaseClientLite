using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Serilog;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace WikibaseClientLite.ModuleExporter.ObjectModel;

public class WikiSiteLuaModuleFactory : LuaModuleFactory
{

    private readonly ILogger logger;

    private readonly Channel<QueuedWritingTask> channel =
        Channel.CreateBounded<QueuedWritingTask>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

    private readonly Task writerTask;

    public WikiSiteLuaModuleFactory(WikiSite site, string titlePrefix, ILogger logger)
    {
        Site = site;
        TitlePrefix = titlePrefix;
        this.logger = logger.ForContext<WikiSiteLuaModuleFactory>();
        writerTask = WriteAsync(channel.Reader);
    }

    public WikiSite Site { get; }

    public string TitlePrefix { get; }

    public override ILuaModule GetModule(string title)
    {
        var page = new WikiPage(Site, TitlePrefix + title);
        return new MemoryBufferedLuaModule(page, this);
    }

    public override async Task ShutdownAsync()
    {
        channel.Writer.Complete();
        await channel.Reader.Completion;
        // Also wait for the writing operation to async complete.
        await writerTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            channel.Writer.TryComplete();
        }
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

    private async Task WriteAsync(ChannelReader<QueuedWritingTask> reader)
    {
        await foreach (var batch in reader.ReadAllAsync().Buffer(100))
        {
            var updatedRequests = 0;
            var updatedPages = 0;

            try
            {
                await batch.Select(t => t.Page).RefreshAsync(PageQueryOptions.None);

                var consecutiveFailures = 0;
                foreach (var task in batch)
                {
                    try
                    {
                        if (task.Page.ContentLength == Encoding.UTF8.GetByteCount(task.NewContent))
                        {
                            var hash = Utility.BytesToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(task.NewContent)));
                            // Content is unchanged.
                            if (string.Equals(task.Page.LastRevision?.Sha1, hash, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        var oldRevid = task.Page.LastRevisionId;
                        logger.Information("Updating {Page}.", task.Page);
                        await task.Page.EditAsync(new()
                        {
                            Content = task.NewContent,
                            Summary = task.Summary,
                            Bot = true,
                        });
                        if (task.Page.LastRevisionId != oldRevid) updatedPages++;
                        updatedRequests++;
                        consecutiveFailures = 0;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to write LUA module: {Page}.", task.Page);
                        consecutiveFailures++;
                        if (consecutiveFailures > 5)
                        {
                            logger.Error("Too many failures. Not continuing.");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Nobody else is going to drain the queue.
                channel.Writer.Complete(ex);
                break;
            }
            finally
            {
                logger.Debug("Updated {Updated}/{Requests}/{Total} pages.", updatedPages, updatedRequests, batch.Count);
            }
        }

        channel.Writer.TryComplete();
        while (channel.Reader.TryRead(out var task))
        {
            logger.Verbose("Draining task: {Page}.", task.Page);
        }
    }

    internal async Task QueuePageForWritingAsync(WikiPage page, string content, string summary)
    {
        await channel.Writer.WriteAsync(new QueuedWritingTask(page, content, summary));
    }

    private sealed class MemoryBufferedLuaModule : ILuaModule
    {

        private readonly WikiPage page;
        private readonly WikiSiteLuaModuleFactory owner;
        private readonly StringBuilder sb = new StringBuilder();
        private StringWriter writer;

        public MemoryBufferedLuaModule(WikiPage page, WikiSiteLuaModuleFactory owner)
        {
            Debug.Assert(page != null);
            Debug.Assert(owner != null);
            this.page = page;
            this.owner = owner;
        }

        public void Dispose()
        {
            writer?.Dispose();
            writer = null;
            sb.Clear();
        }

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

}
