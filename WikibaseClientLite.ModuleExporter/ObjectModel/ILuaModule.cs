namespace WikibaseClientLite.ModuleExporter.ObjectModel;

/// <summary>
/// Represents a LUA module, either on disk or on MediaWiki site.
/// </summary>
public interface ILuaModule : IDisposable
{

    /// <summary>
    /// Gets the writer that can be used to write module content.
    /// </summary>
    /// <remarks>
    /// Usually this property returns the same instance for the same <see cref="ILuaModule"/>.
    /// Do not close the returned <see cref="TextWriter"/>.
    /// </remarks>
    TextWriter Writer { get; }

    /// <summary>
    /// Closes <see cref="Writer"/> and submits the module.
    /// </summary>
    /// <param name="editSummary">The edit summary.</param>
    Task SubmitAsync(string editSummary);

}
