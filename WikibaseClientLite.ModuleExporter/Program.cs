using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Serilog;
using Serilog.Core;
using WikibaseClientLite.ModuleExporter.CommandLine;
using WikibaseClientLite.ModuleExporter.Schema;

namespace WikibaseClientLite.ModuleExporter
{
    internal static class Program
    {

        private static Logger logger;

        private static readonly JsonSerializer jsonSerializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        static async Task<int> Main(string[] rawArgs)
        {
            var args = new CommandArguments(rawArgs.Select(CommandLineParser.ParseArgument));
            if ((bool?)args["help"] ?? (bool?)args["h"] ?? (bool?)args["?"] ?? false)
            {
                ShowHelp();
                return 1;
            }
            var tasksFileName = (string)args[0];
            if (string.IsNullOrWhiteSpace(tasksFileName))
            {
                tasksFileName = Path.GetFullPath("tasks.json");
                if (!File.Exists(tasksFileName))
                {
                    Console.Error.WriteLine("Cannot find tasks.json in the current directory.");
                    ShowHelp();
                    return 1;
                }
            }
            var config = LoadTasksConfig(tasksFileName);
            var loggerConfig = new LoggerConfiguration();
            if (config.Logging != null)
            {
                loggerConfig.ReadFrom.KeyValuePairs(config.Logging);
            }
            else
            {
                loggerConfig
                    .MinimumLevel.Information()
                    .WriteTo.Console();
            }
            if ((bool?)args["v"] ?? false) loggerConfig.MinimumLevel.Verbose();
            logger = loggerConfig.CreateLogger();
            try
            {
                using (var siteProvider = new WikiSiteProvider(config.MwSites ?? Enumerable.Empty<MwSite>(), logger))
                {
                    var dispatcher = new TaskActionDispatcher(logger, siteProvider);
                    foreach (var action in config.Actions)
                    {
                        await dispatcher.DispatchAction_(action);
                    }
                }

                using (var proc = Process.GetCurrentProcess())
                {
                    logger.Information("Peak working set = {PeakWS:0.00}MB", proc.PeakWorkingSet64 / 1024.0 / 1024.0);
                }

            }
            finally
            {
                // We don't want to lose logs on Discord.
                logger.Dispose();
            }
            return 0;
        }

        private static TasksConfig LoadTasksConfig(string fileName)
        {
            using (var reader = File.OpenText(fileName))
            using (var jreader = new JsonTextReader(reader))
            {
                return jsonSerializer.Deserialize<TasksConfig>(jreader);
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine(@"WikibaseClientLite.ModuleExporter

Usage
    wbclexport [TaskConfigurationFile] [-v]

where
    TaskConfigurationFile   the path of JSON configuration file defining the actions to be executed;
                                the default value is ./tasks.json
    -v                      show verbose log
");
        }

    }
}
