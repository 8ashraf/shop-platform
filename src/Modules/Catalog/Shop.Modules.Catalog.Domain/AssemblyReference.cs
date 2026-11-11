using System.Reflection;

namespace Shop.Modules.Catalog.Domain;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
