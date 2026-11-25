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

    [Fact]
    public void Move_PutsTheTabAtItsNewPlace_AndKeepsItThere()
    {
        var order = new MainTabOrder();
        order.Sync(["a", "b", "c"]);

        order.Move("c", 0);

        Assert.Equal(["c", "a", "b", "d"], order.Sync(["a", "b", "c", "d"]));
    }

    [Fact]
    public void Move_RaisesChanged_OnlyWhenTheOrderChanges()
    {
        var order = new MainTabOrder();
        order.Sync(["a", "b"]);
        var changes = 0;
        order.Changed += () => changes++;

        order.Move("a", 0);
        order.Move("a", 1);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void UsageTab_StaysLast()
    {
        var order = new MainTabOrder();
        order.Sync(["a", MainTabOrder.UsageKey]);

        Assert.Equal(["a", "b", MainTabOrder.UsageKey], order.Sync(["a", MainTabOrder.UsageKey, "b"]));

        order.Move("a", 5);
        Assert.Equal(["b", "a", MainTabOrder.UsageKey], order.Keys);

        order.Move(MainTabOrder.UsageKey, 0);
        Assert.Equal(["b", "a", MainTabOrder.UsageKey], order.Keys);

        order.Set([MainTabOrder.UsageKey, "b"]);
        Assert.Equal(["b", MainTabOrder.UsageKey], order.Keys);
    }
}
