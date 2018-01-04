using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WikibaseClientLite.ModuleExporter.ObjectModel
{
    public interface ILuaModule : IDisposable
    {

        void Append(string content);

        void AppendLine(string content);

        Task SubmitAsync(string editSummary);

    }

    public static class LuaModuleExtensions
    {

        public static void AppendLine(this ILuaModule module)
        {
            module.AppendLine(null);
        }
        
    }
}
