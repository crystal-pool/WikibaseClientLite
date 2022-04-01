using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WikibaseClientLite.ModuleExporter.Schema
{

    [JsonObject]
    public class TasksConfig
    {

        public IDictionary<string, string> Logging { get; set; }

        public IList<JObject> Actions { get; set; }

        public IList<MwSite> MwSites { get; set; }

    }

    public class MwSite
    {

        public string Name { get; set; }

        public string ApiEndpoint { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

    }

}
