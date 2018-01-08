using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;

namespace WikibaseClientLite.ModuleExporter
{
    public partial class TaskActionDispatcher
    {

        private readonly ILogger rootLogger;
        private readonly WikiSiteProvider mwSiteProvider;
        private ILogger logger;

        public TaskActionDispatcher(ILogger rootLogger, WikiSiteProvider mwSiteProvider)
        {
            this.rootLogger = rootLogger ?? throw new ArgumentNullException(nameof(rootLogger));
            this.mwSiteProvider = mwSiteProvider;
        }

        public async Task DispatchAction_(JObject options)
        {
            var actionName = (string)options["action"];
            rootLogger.Information("Starting action: {Task}", actionName);
            // TODO use action name.
            logger = rootLogger.ForContext<TaskActionDispatcher>();
            var result = this.GetType().InvokeMember(actionName + "Action",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.InvokeMethod,
                null, this, new object[] {options});
            if (result is Task t) await t;
        }

    }
}
