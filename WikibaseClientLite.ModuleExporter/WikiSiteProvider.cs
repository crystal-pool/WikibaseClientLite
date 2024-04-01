using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Serilog;
using WikibaseClientLite.ModuleExporter.Schema;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using ILogger = Serilog.ILogger;

namespace WikibaseClientLite.ModuleExporter;

public class WikiSiteProvider : IDisposable, IWikiFamily
{

    private readonly List<MwSite> siteConfig;
    private readonly ConcurrentDictionary<string, WikiSite> sitesCacheDict;
    private readonly WikiClient wikiClient;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger logger;

    public WikiSiteProvider(IEnumerable<MwSite> siteConfig, ILogger logger)
    {
        if (siteConfig == null) throw new ArgumentNullException(nameof(siteConfig));
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        this.siteConfig = siteConfig.ToList();
        wikiClient = new WikiClient();
        sitesCacheDict = new ConcurrentDictionary<string, WikiSite>(StringComparer.InvariantCultureIgnoreCase);
        loggerFactory = new LoggerFactory(Enumerable.Empty<ILoggerProvider>(),
                new LoggerFilterOptions { MinLevel = LogLevel.Warning })
            .AddSerilog(logger);
        this.logger = logger;
    }

    public async Task<WikiSite> GetSiteAsync(string name)
    {
        if (!sitesCacheDict.TryGetValue(name, out var site))
        {
            var config = siteConfig.FirstOrDefault(s => s.Name == name);
            if (config == null) throw new ArgumentException("Missing WikiSite: " + name + ".", nameof(name));
            site = sitesCacheDict.GetOrAdd(name, _ =>
            {
                WikiSite s;
                if (!string.IsNullOrEmpty(config.UserName))
                {
                    s = new WikiSite(wikiClient, new SiteOptions(config.ApiEndpoint), config.UserName, config.Password);
                    s.AccountAssertionFailureHandler = new AccountAssertionFailureHandler(config.UserName, config.Password);
                }
                else
                {
                    s = new WikiSite(wikiClient, config.ApiEndpoint);
                }
                s.ModificationThrottler.ThrottleTime = TimeSpan.FromSeconds(1);
                s.Logger = loggerFactory.CreateLogger(name);
                logger.Verbose("Created WikiSite {Name} with Account {UserName}.", config.Name, config.UserName);
                return s;
            });
        }

        await site.Initialization;
        return site;
    }

    /// <inheritdoc />
    public string TryNormalize(string prefix)
    {
        return siteConfig
            .FirstOrDefault(c => string.Equals(c.Name, prefix, StringComparison.InvariantCultureIgnoreCase))
            ?.Name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        wikiClient.Dispose();
    }

    private class AccountAssertionFailureHandler : IAccountAssertionFailureHandler
    {

        private readonly string _UserName;
        private readonly string _Password;

        public AccountAssertionFailureHandler(string userName, string password)
        {
            _UserName = userName;
            _Password = password;
        }

        /// <inheritdoc />
        public async Task<bool> Login(WikiSite site)
        {
            await site.LoginAsync(_UserName, _Password);
            return true;
        }

    }

}
