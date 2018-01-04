﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WikibaseClientLite.ModuleExporter.ObjectModel
{
    public class FileSystemLuaModuleFactory : LuaModuleFactory
    {
        public FileSystemLuaModuleFactory(string rootPath)
        {
            RootPath = Path.GetFullPath(rootPath);
        }

        public string RootPath { get; }

        /// <inheritdoc />
        public override ILuaModule GetModule(string title)
        {
            if (title.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Title cannot end with .lua.", nameof(title));
            if (!Directory.Exists(RootPath)) Directory.CreateDirectory(RootPath);
            return new LuaModule(Path.Combine(RootPath, title + ".lua"));
        }

        private sealed class LuaModule : ILuaModule
        {

            private TextWriter writer;

            public LuaModule(string fileName)
            {
                FileName = fileName;
            }

            public string FileName { get; set; }

            public void Append(string content)
            {
                if (writer == null) writer = File.CreateText(FileName);
                writer.Write(content);
            }

            /// <inheritdoc />
            public void AppendLine(string content)
            {
                if (writer == null) writer = File.CreateText(FileName);
                writer.WriteLine(content);
            }

            /// <inheritdoc />
            public Task SubmitAsync(string editSummary)
            {
                writer.WriteLine();
                writer.Write("-- Summary: ");
                writer.WriteLine(editSummary);
                writer.Close();
                writer = null;
                return Task.CompletedTask;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                writer?.Dispose();
            }
        }

    }
}