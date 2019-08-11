using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using WikibaseClientLite.ModuleExporter.Sparql.Contracts;
using SparqlQuery = VDS.RDF.Query.SparqlQuery;

namespace WikibaseClientLite.ModuleExporter.Sparql
{

    public class AotSparqlExecutor
    {
        private readonly INamespaceMapper namespaceMapper;
        private readonly Func<Uri, string> uriSerializer;

        private readonly LeviathanQueryProcessor queryProcessor;
        private readonly SparqlQueryParser queryParser;

        public AotSparqlExecutor(ISparqlDataset dataset, INamespaceMapper namespaceMapper, Func<Uri, string> uriSerializer)
        {
            Dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
            this.namespaceMapper = namespaceMapper ?? throw new ArgumentNullException(nameof(namespaceMapper));
            this.uriSerializer = uriSerializer ?? throw new ArgumentNullException(nameof(uriSerializer));
            queryProcessor = new LeviathanQueryProcessor(dataset);
            queryParser = new SparqlQueryParser();
        }

        public ISparqlDataset Dataset { get; }

        public string SerializeUri(Uri nodeUri)
        {
            return uriSerializer(nodeUri);
        }

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
                    queryString.SetBlankNode(name);
                }
            }
            // Parse the query.
            var query = queryParser.ParseFromString(queryString);
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
            var query = queryParser.ParseFromString(queryString);
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

        private readonly object emptyObject = new object();
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
                    return emptyObject;
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
                    return n.AsDateTime().ToUniversalTime().Ticks;
                case LiteralNode n:
                    return n.Value;
                default:
                    throw new NotSupportedException($"Serializing {node.NodeType} node is not supported.");
            }
        }

    }

}
