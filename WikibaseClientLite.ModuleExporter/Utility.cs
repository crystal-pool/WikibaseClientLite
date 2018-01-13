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

        public static int HashString(string s)
        {
            const int M = 11126858;
            var hash = 1;
            foreach (var c in s) hash = (hash % M) * 193 + c;
            return hash;
        }

        public static string BytesToHexString(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

    }
}
