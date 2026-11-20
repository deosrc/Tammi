using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class MainTabOrderTests
{
    [Fact]
    public void Sync_DropsClosedTabs_AndAppendsNewOnes()
    {
        var order = new MainTabOrder();
        order.Sync(["a", "b"]);

        Assert.Equal(["b", "c"], order.Sync(["c", "b"]));
    }

    [Fact]
    public void Set_KeepsTheGivenOrder_AcrossSync()
    {
        var order = new MainTabOrder();
        order.Set(["b", "a"]);

        Assert.Equal(["b", "a", "c"], order.Sync(["a", "c", "b"]));
    }
}
