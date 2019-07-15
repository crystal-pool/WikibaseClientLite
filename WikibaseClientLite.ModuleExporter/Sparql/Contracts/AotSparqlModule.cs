using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WikibaseClientLite.ModuleExporter.Sparql.Contracts
{

    [JsonObject]
    public class AotSparqlModuleRoot
    {

        /// <summary>
        /// All the SPARQL result sets contained in the current module.
        /// </summary>
        public IDictionary<string, AotSparqlQueryResultSet> ResultSets { get; set; }

    }

    [JsonObject]
    public class AotSparqlQueryResultSet
    {

        private static readonly string[] emptyPivotParams = { };

        /// <summary>
        /// A list of pivot parameter names.
        /// </summary>
        public IList<string> PivotParams { get; set; } = emptyPivotParams;

        /// <summary>
        /// SPARQL query results by pivot parameter value set.
        /// </summary>
        public IList<AotSparqlQueryResult> Results { get; set; }

    }

    [JsonObject]
    public class AotSparqlQueryResult
    {

        private static readonly object[] emptyPivotValues = { };

        /// <summary>
        /// A list of pivot parameter values, in the same order as <see cref="AotSparqlQueryResultSet.PivotParams"/>.
        /// </summary>
        public IList<object> PivotValues { get; set; } = emptyPivotValues;

        /// <summary>
        /// Query result rows, consisting of column values (cells).
        /// </summary>
        public IList<IList<object>> Rows { get; set; }

    }

}
