using ExamplePlugin.Tests;
using System;
using System.Linq;
using System.Reflection;
using Una.Drawing;

namespace ExamplePlugin;

public sealed partial class ExamplePlugin
{
    private void InitializeTests()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        UdtDocument doc = UdtLoader.LoadFromAssembly(assembly, "shared.box.xml");
        StylesheetRegistry.Register("tests", doc.Stylesheet!);

        var tests = assembly
                   .GetTypes()
                   .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(DrawingTest)))
                   .ToList();

        foreach (var test in tests) {
            var instance = (DrawingTest?)Activator.CreateInstance(test);

            if (null != instance) {
                _tests[test.Name] = instance;
                
                if (_categories.Contains(instance.Category) == false)
                    _categories.Add(instance.Category);
            }
        }
        
        _categories.Sort();
    }
}