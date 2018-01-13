using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WikibaseClientLite.ModuleExporter.ObjectModel
{
    public abstract class LuaModuleFactory : IDisposable
    {

        public abstract ILuaModule GetModule(string title);

        public abstract Task ShutdownAsync();

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
