namespace Shop.Modules.Catalog.UnitTests;

public class CatalogSmokeTests
{
    [Fact]
    public void Domain_assembly_is_named_after_the_module() =>
        Assert.Equal(
            "Shop.Modules.Catalog.Domain",
            global::Shop.Modules.Catalog.Domain.AssemblyReference.Assembly.GetName().Name);
}
