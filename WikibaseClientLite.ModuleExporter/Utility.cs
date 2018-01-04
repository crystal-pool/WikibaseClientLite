using System;
using System.Collections.Generic;
using System.Text;

namespace WikibaseClientLite.ModuleExporter
{
    public static class Utility
    {

        public static int HashItemId(char prefix, int id)
        {
            prefix = char.ToUpperInvariant(prefix);
            return unchecked (prefix * 6291469 + id);
        }

        public static int HashItemId(string id)
        {
            id = id.Trim();
            return HashItemId(id[0], Convert.ToInt32(id.Substring(1)));
        }

    }
}
