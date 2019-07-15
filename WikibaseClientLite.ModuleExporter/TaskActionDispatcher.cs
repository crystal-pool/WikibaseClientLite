using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace WikibaseClientLite.ModuleExporter
{
    public partial class TaskActionDispatcher
    {

        private readonly ILogger rootLogger;
        private readonly WikiSiteProvider mwSiteProvider;
        private ILogger logger;

        private static readonly JsonSerializer jSerializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public TaskActionDispatcher(ILogger rootLogger, WikiSiteProvider mwSiteProvider)
        {
            this.rootLogger = rootLogger ?? throw new ArgumentNullException(nameof(rootLogger));
            this.mwSiteProvider = mwSiteProvider;
        }

        public async Task DispatchAction_(JObject jOptions)
        {
            var actionName = (string)jOptions["action"];
            if ((bool?)jOptions["disabled"] ?? false)
            {
                rootLogger.Debug("Skipped action: {Task}", actionName);
                return;
            }
            rootLogger.Information("Starting action: {Task}", actionName);
            // TODO use action name.
            logger = rootLogger.ForContext<TaskActionDispatcher>();
            var method = this.GetType().GetMethod(actionName + "Action",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.InvokeMethod);
            object options;
            using (var reader = jOptions.CreateReader())
                options = jSerializer.Deserialize(reader, method.GetParameters()[0].ParameterType);
            var result = method.Invoke(this, new[] { options });
            if (result is Task t) await t;
        }

    }
}
