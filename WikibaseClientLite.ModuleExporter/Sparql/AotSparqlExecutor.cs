using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using WikibaseClientLite.ModuleExporter.Sparql.Contracts;
using SparqlQuery = VDS.RDF.Query.SparqlQuery;

namespace WikibaseClientLite.ModuleExporter.Sparql
{

    public sealed class AotSparqlExecutor : IDisposable
    {
        private readonly INamespaceMapper namespaceMapper;
        private readonly Func<Uri, string> uriSerializer;
        private readonly ILogger logger;

        private readonly IGraph underlyingGraph;
        private readonly ISparqlQueryProcessor queryProcessor;
        private readonly SparqlQueryParser queryParser;

        public AotSparqlExecutor(string dataSetUri, INamespaceMapper namespaceMapper, Func<Uri, string> uriSerializer, ILogger logger)
        {
            if (dataSetUri == null) throw new ArgumentNullException(nameof(dataSetUri));
            this.namespaceMapper = namespaceMapper ?? throw new ArgumentNullException(nameof(namespaceMapper));
            this.uriSerializer = uriSerializer ?? throw new ArgumentNullException(nameof(uriSerializer));
            var uri = new Uri(new Uri(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar, UriKind.Absolute), dataSetUri);
            if (uri.IsFile)
            {
                underlyingGraph = new Graph();
                underlyingGraph.LoadFromFile(uri.LocalPath);
                logger.Information("Loaded {Count} triples.", underlyingGraph.Triples.Count);
                var dataset = new InMemoryDataset(underlyingGraph);
                queryProcessor = new LeviathanQueryProcessor(dataset);
            }
            else
            {
                var endpoint = new SparqlRemoteEndpoint(uri);
                queryProcessor = new RemoteQueryProcessor(endpoint);
                logger.Information("Using SPARQL endpoint from {Host}.", uri.Host);
            }
            queryParser = new SparqlQueryParser();
            this.logger = logger;
        }

        public string SerializeUri(Uri nodeUri)
        {
            return uriSerializer(nodeUri);
        }

        private SparqlQuery GuardedParseQueryString(SparqlParameterizedString query)
        {
            try
            {
                return queryParser.ParseFromString(query);
            }
            catch (RdfParseException ex)
            {
                string context = null;
                if (ex.HasPositionInformation)
                {
                    var expr = query.ToString().Split('\n');
                    var sb = new StringBuilder();
                    for (int i = Math.Max(0, ex.StartLine - 1 - 2), j = Math.Min(expr.Length, ex.StartLine - 1); i < j; i++)
                    {
                        sb.AppendFormat("{0,-3}    ", i + 1);
                        sb.AppendLine(expr[i]);
                    }
                    for (int i = Math.Max(0, ex.StartLine - 1), j = Math.Min(expr.Length, ex.EndLine); i < j; i++)
                    {
                        sb.AppendFormat("{0,-3}  ! ", i + 1);
                        sb.AppendLine(expr[i]);
                    }
                    for (int i = Math.Max(0, ex.EndLine), j = Math.Min(expr.Length, ex.EndLine + 2); i < j; i++)
                    {
                        sb.AppendFormat("{0,-3}    ", i + 1);
                        sb.AppendLine(expr[i]);
                    }
                    context = sb.ToString();
                }
                logger.Warning("Failed to parse query string. {Error}\n{Context}", ex.Message, context);
                throw;
            }
        }

        private readonly Uri dummyUri = new Uri("http://example.org/dummyUri");

        public IList<string> GetResultVariables(string queryExpr, IEnumerable<string> parameterNames = null)
        {
            var queryString = new SparqlParameterizedString(queryExpr);
            // Import namespace presets. 
            foreach (var prefix in namespaceMapper.Prefixes)
            {
                if (!queryString.Namespaces.HasNamespace(prefix))
                {
                    queryString.Namespaces.AddNamespace(prefix, namespaceMapper.GetNamespaceUri(prefix));
                }
            }
            // Apply parameter placeholders.
            if (parameterNames != null)
            {
                foreach (var name in parameterNames)
                {
                    queryString.SetUri(name, dummyUri);
                }
            }
            // Parse the query.
            var query = GuardedParseQueryString(queryString);
            return query.Variables.Where(v => v.IsResultVariable).Select(v => v.Name).ToList();
        }

        public SparqlResultSet Execute(string queryExpr, IDictionary<string, INode> paramValues = null)
        {
            var queryString = new SparqlParameterizedString(queryExpr);
            // Import namespace presets. 
            foreach (var prefix in namespaceMapper.Prefixes)
            {
                if (!queryString.Namespaces.HasNamespace(prefix))
                {
                    queryString.Namespaces.AddNamespace(prefix, namespaceMapper.GetNamespaceUri(prefix));
                }
            }
            // Apply parameters.
            if (paramValues != null)
            {
                foreach (var (name, value) in paramValues)
                {
                    queryString.SetParameter(name, value);
                }
            }
            // Parse the query.
            var query = GuardedParseQueryString(queryString);
            foreach (var prefix in namespaceMapper.Prefixes)
            {
                if (!query.NamespaceMap.HasNamespace(prefix))
                {
                    query.NamespaceMap.AddNamespace(prefix, namespaceMapper.GetNamespaceUri(prefix));
                }
            }
            var result = queryProcessor.ProcessQuery(query);
            if (!(result is SparqlResultSet resultSet))
                throw new NotSupportedException("Persisting AOT query results other than SparqlResultSet is not supported.");
            return resultSet;
        }

        public AotSparqlQueryResult ExecuteAndSerialize(string queryExpr, IList<string> resultVariables, IDictionary<string, INode> paramValues)
        {
            var results = Execute(queryExpr, paramValues);
            var columnCount = resultVariables.Count;
            var resultRows = new List<IList<object>>();
            foreach (var row in results)
            {
                var resultRow = new object[columnCount];
                var colIndex = 0;
                foreach (var varName in resultVariables)
                {
                    resultRow[colIndex] = row.HasBoundValue(varName) ? SerializeNode(row.Value(varName)) : null;
                    colIndex++;
                }
                resultRows.Add(resultRow);
            }
            return new AotSparqlQueryResult { Rows = resultRows };
        }

        private readonly object boxedTrue = true, boxedFalse = false;

        public object SerializeNode(INode node)
        {
            switch (node)
            {
                case null:
                    return null;
                case UriNode uriNode:
                    return uriSerializer(uriNode.Uri);
                case BlankNode _:
                    return uriSerializer(null);
                case StringNode n:
                    if (string.IsNullOrEmpty(n.Language))
                        return n.AsString();
                    else
                        return new[] { n.Value, n.Language };
                case BooleanNode n:
                    return n.AsBoolean() ? boxedTrue : boxedFalse;
                case ByteNode n:
                    return n.AsInteger();
                case SignedByteNode n:
                    return n.AsInteger();
                case LongNode n:
                    return n.AsInteger();
                case UnsignedLongNode n:
                    return n.AsDouble();
                case FloatNode n:
                    return n.AsFloat();
                case DoubleNode n:
                    return n.AsDouble();
                case DecimalNode n:
                    return n.AsDecimal();
                case DateTimeNode n:
                    var dt = n.AsDateTimeOffset();
                    // LUA 5.1 does not have long, so we use double by default.
                    return new[] { dt.ToUnixTimeMilliseconds(), dt.Offset.TotalMinutes };
                case LiteralNode n:
                    return n.Value;
                default:
                    throw new NotSupportedException($"Serializing {node.NodeType} node is not supported.");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            underlyingGraph?.Dispose();
        }
    }

}
