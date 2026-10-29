namespace Shop.Modules.Identity.UnitTests;

public class IdentitySmokeTests
{
    [Fact]
    public void Domain_assembly_is_named_after_the_module() =>
        Assert.Equal(
            "Shop.Modules.Identity.Domain",
            global::Shop.Modules.Identity.Domain.AssemblyReference.Assembly.GetName().Name);
}
