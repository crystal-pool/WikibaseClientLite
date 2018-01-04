using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WikibaseClientLite.ModuleExporter.Schema
{

    [JsonObject]
    public class TasksConfig
    {

        public IList<JObject> Actions { get; set; }

    }

}
