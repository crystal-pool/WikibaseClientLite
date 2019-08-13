using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Luaon.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using VDS.RDF;
using VDS.RDF.Nodes;
using WikibaseClientLite.ModuleExporter.ObjectModel;
using WikibaseClientLite.ModuleExporter.Sparql.Contracts;
using WikiClientLibrary.Scribunto;
using WikiClientLibrary.Sites;

namespace WikibaseClientLite.ModuleExporter.Sparql
{
    public class AotSparqlModuleExporter
    {

        private readonly LuaModuleFactory moduleFactory;
        private readonly AotSparqlExecutor executor;

        private static readonly JsonSerializer luaModuleJsonSerializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public AotSparqlModuleExporter(ILogger logger, LuaModuleFactory moduleFactory, AotSparqlExecutor executor)
        {
            this.moduleFactory = moduleFactory;
            this.executor = executor;
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ILogger Logger { get; }

        public TimeSpan StatusReportInterval { get; set; } = TimeSpan.FromSeconds(30);

        public AotSparqlSiteConfig SiteConfig { get; set; }

        public async Task LoadConfigFromModuleAsync(WikiSite site, string moduleName)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            if (moduleName == null) throw new ArgumentNullException(nameof(moduleName));
            var config = await site.ScribuntoLoadDataAsync<AotSparqlSiteConfig>(moduleName, luaModuleJsonSerializer);
            Logger.Information("Loaded config from {Site}. Queries count: {QueriesCount}.", site, config.Queries.Count);
            SiteConfig = config;
        }

        private string SerializeClusterKey(INode clusterKey)
        {
            switch (clusterKey)
            {
                case UriNode n:
                    return "U" + executor.SerializeUri(n.Uri);
                case StringNode n:
                    return "S" + n.Value + "@" + n.Language;
                case LongNode n:
                    return "I" + n.Value;
                default:
                    throw new NotSupportedException($"Clustering by {clusterKey.NodeType} is not supported.");
            }
        }

        private void WriteParamValue(INode node, JsonWriter writer)
        {
            switch (node)
            {
                case UriNode n:
                    writer.WriteValue(executor.SerializeUri(n.Uri));
                    break;
                case StringNode n:
                    writer.WriteValue(n.Value + "@" + n.Language);
                    break;
                case LongNode n:
                    writer.WriteValue(n.Value);
                    break;
                default:
                    throw new NotSupportedException($"Query parameter with type {node.NodeType} is not supported.");
            }
        }

        private void WriteProlog(TextWriter writer, string prologText)
        {
            writer.WriteLine("----------------------------------------");
            writer.WriteLine("-- Powered by WikibaseClientLite");
            writer.WriteLine("-- AOT SPARQL query result");
            writer.Write("-- ");
            writer.WriteLine(prologText);
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

        public async Task ExportModulesAsync()
        {
            if (SiteConfig == null) throw new ArgumentNullException(nameof(SiteConfig));
            var clusterDict = new ConcurrentDictionary<string, AotSparqlModuleRoot>();
            var queryCounter = 0;
            foreach (var (queryName, queryContent) in SiteConfig.Queries)
            {
                queryCounter++;
                Logger.Information("Processing query {Counter}/{Total}: {Name}.",
                    queryCounter, SiteConfig.Queries.Count, queryName);
                try
                {
                    // Execute params query first.
                    var paramsResult = executor.Execute(queryContent.ParamsQuery);
                    var clusterVariable = SparqlQuery.ParamsQueryParamPrefix + queryContent.ClusteredBy;
                    if (!paramsResult.Variables.Contains(clusterVariable))
                        throw new InvalidOperationException(
                            $"Specified clustering variable {clusterVariable} does not exist in the result set from ParamsQuery.");
                    Logger.Information("Parameter query returned {Count} results.", paramsResult.Count);
                    if (paramsResult.Count == 0) continue;
                    var paramNames = paramsResult.Variables
                        .Where(v => v.StartsWith(SparqlQuery.ParamsQueryParamPrefix))
                        .Select(v => (ParamName: v.Substring(SparqlQuery.ParamsQueryParamPrefix.Length), ResultName: v))
                        .ToList();
                    var pivotParamNames = paramNames.Where(p => p.ParamName != queryContent.ClusteredBy).ToList();
                    var resultVariables = executor.GetResultVariables(queryContent.SourceQuery, paramNames.Select(n => n.ParamName));
                    int minResultRows = int.MaxValue, maxResultRows = -1;
                    var paramSetCounter = 0;
                    // Traverse the params query result set.
                    foreach (var row in paramsResult)
                    {
                        paramSetCounter++;
                        Logger.Verbose("Processing param set {Counter}/{Total}.", paramSetCounter, paramsResult.Count);
                        var clusterNode = row[clusterVariable];
                        var clusterKey = SerializeClusterKey(clusterNode);
                        var queryParams = paramNames.ToDictionary(p => p.ParamName, p => row.Value(p.ResultName));
                        var cluster = clusterDict.GetOrAdd(clusterKey,
                            k => new AotSparqlModuleRoot
                            {
                                ResultSets = new Dictionary<string, AotSparqlQueryResultSet>()
                            });
                        AotSparqlQueryResultSet resultSet;
                        lock (cluster)
                        {
                            if (!cluster.ResultSets.TryGetValue(queryName, out resultSet))
                            {
                                resultSet = new AotSparqlQueryResultSet { Results = new List<AotSparqlQueryResult>() };
                                if (pivotParamNames.Count > 0)
                                    resultSet.PivotParams = pivotParamNames.Select(p => p.ParamName).ToList();
                                resultSet.Columns = resultVariables;
                                cluster.ResultSets.Add(queryName, resultSet);
                            }
                        }
                        var queryResult = executor.ExecuteAndSerialize(queryContent.SourceQuery, resultVariables, queryParams);
                        minResultRows = Math.Min(minResultRows, queryResult.Rows.Count);
                        maxResultRows = Math.Max(maxResultRows, queryResult.Rows.Count);
                        lock (cluster)
                        {
                            if (pivotParamNames.Count > 0)
                            {
                                queryResult.PivotValues = pivotParamNames
                                    .Select(p => executor.SerializeNode(queryParams[p.ParamName]))
                                    .ToList();
                            }
                            resultSet.Results.Add(queryResult);
                        }
                    }
                    if (maxResultRows < 0)
                    {
                        Debug.Assert(minResultRows == int.MaxValue);
                        minResultRows = -1;
                    }
                    Logger.Information("Executed query {QueryName} with {ParamRows} param sets. Source query results: min: {MinResults}, max: {MaxResults}.",
                        queryName, paramsResult.Count, minResultRows, maxResultRows);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to execute query {QueryName}.", queryName);
                    throw;
                }
            }
            Logger.Information("Writing {Count} clustered modules…", clusterDict.Count);
            var statusReportSw = Stopwatch.StartNew();
            var writtenCount = 0;
            foreach (var (name, root) in clusterDict)
            {
                using (var module = moduleFactory.GetModule(name))
                {
                    WriteProlog(module.Writer, "Cluster key: " + name);
                    using (var jwriter = new JsonLuaWriter(module.Writer) { CloseOutput = false })
                    {
                        luaModuleJsonSerializer.Serialize(jwriter, root);
                    }
                    WriteEpilog(module.Writer);
                    await module.SubmitAsync("Export clustered SPARQL query result for " + name + ".");
                    writtenCount++;
                    if (statusReportSw.Elapsed >= StatusReportInterval)
                    {
                        Logger.Information("Written {Count}/{Total} clustered modules.", writtenCount, clusterDict.Count);
                    }
                    statusReportSw.Restart();
                }
            }
            Logger.Information("Written {Count}/{Total} clustered modules.", writtenCount, clusterDict.Count);
        }

    }
}
