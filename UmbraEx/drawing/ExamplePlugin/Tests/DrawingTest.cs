using Dalamud.Bindings.ImGui;
using System;
using System.Reflection;
using Una.Drawing;

namespace ExamplePlugin.Tests;

internal abstract class DrawingTest
{
    /// <summary>
    /// The name of the test.
    /// </summary>
    public abstract string Name { get; }
    
    /// <summary>
    /// The category of the test.
    /// </summary>
    public abstract string Category { get; }

    /// <summary>
    /// Invoked when the test is activated.
    /// </summary>
    public abstract void OnActivate();

    /// <summary>
    /// Invoked when the test is deactivated.
    /// </summary>
    public abstract void OnDeactivate();

    /// <summary>
    /// Renders ImGUI elements in the configuration window.
    /// </summary>
    public abstract void RenderConfig();

    /// <summary>
    /// Renders the test.
    /// </summary>
    public abstract void RenderTest(ImDrawListPtr dl);
    
    public virtual uint BackgroundColor => 0x80000000;

    /// <summary>
    /// Loads a template file from the "templates" directory relative to the assembly
    /// location.
    /// </summary>
    /// <param name="templateFile"></param>
    /// <returns></returns>
    protected static Node CreateNodeFromFile(string templateFile)
    {
        UdtDocument doc = UdtLoader.LoadFromAssembly(Assembly.GetExecutingAssembly(), templateFile);

        if (null == doc.RootNode) {
            throw new Exception($"The UDT file \"{templateFile}\" has no root node.");
        }

        return doc.RootNode;
    }
}