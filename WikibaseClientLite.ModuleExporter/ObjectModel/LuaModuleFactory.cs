using System;
using System.Collections.Generic;
using System.Text;

namespace WikibaseClientLite.ModuleExporter.ObjectModel
{
    public abstract class LuaModuleFactory
    {

        public abstract ILuaModule GetModule(string title);

    }
}
