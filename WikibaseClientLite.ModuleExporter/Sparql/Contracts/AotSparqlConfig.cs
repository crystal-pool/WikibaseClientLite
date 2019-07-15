using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WikibaseClientLite.ModuleExporter.Sparql.Contracts
{

    /// <summary>
    /// AOT SPARQL config in client site Scribunto module.
    /// </summary>
    [JsonObject]
    public class AotSparqlSiteConfig
    {

        public IDictionary<string, SparqlQuery> Queries { get; set; }

    }

    [JsonObject]
    public class SparqlQuery
    {

        public const string ParamsQueryParamPrefix = "p_";

        public string SourceQuery { get; set; }

        public string ParamsQuery { get; set; }

        public string ClusteredBy { get; set; }

    }

}
