using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Extensions.Logging;
using WikibaseClientLite.ModuleExporter.Schema;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WikibaseClientLite.ModuleExporter
{
    public class WikiSiteProvider : IDisposable, IWikiFamily
    {

        private readonly List<MwSite> siteConfig;
        private readonly ConcurrentDictionary<string, WikiSite> sitesCacheDict;
        private readonly WikiClient wikiClient;
        private readonly ILogger logger;
        private readonly SerilogLoggerProvider loggerAdapter;

        public WikiSiteProvider(IEnumerable<MwSite> siteConfig, ILogger logger)
        {
            if (siteConfig == null) throw new ArgumentNullException(nameof(siteConfig));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.siteConfig = siteConfig.ToList();
            wikiClient = new WikiClient();
            sitesCacheDict = new ConcurrentDictionary<string, WikiSite>(StringComparer.InvariantCultureIgnoreCase);
            loggerAdapter = new SerilogLoggerProvider(logger);
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
                    s.ModificationThrottler.ThrottleTime = TimeSpan.FromSeconds(0.1);
                    s.Logger = loggerAdapter.CreateLogger(name);
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
}
