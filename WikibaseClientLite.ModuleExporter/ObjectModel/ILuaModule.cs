using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WikibaseClientLite.ModuleExporter.ObjectModel
{
    public interface ILuaModule : IDisposable
    {

        TextWriter GetWriter();

        Task SubmitAsync(string editSummary);

    }

}
