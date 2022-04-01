namespace WikibaseClientLite.ModuleExporter.Tasks
{

    public class AotSparqlOptions
    {

        /// <summary>
        /// Local graph dump path, or remote SPARQL endpoint URL.
        /// </summary>
        public string DataSource { get; set; }

        public string ConfigModule { get; set; }

        public string ExportModulePrefix { get; set; }

        public IDictionary<string, string> NamespaceMapping { get; set; }

    }

}
