using OpenGE.Scheduling;
using Xunit;

namespace Tests.Scheduling;

public class ContextTests
{
    [Fact]
    public void Constructor_PreservesTimingValues()
    {
        var context = new UpdateContext(0.016, 60);

        Assert.Equal(0.016, context.DeltaTime);
        Assert.Equal(60, context.UpdateRate);
    }

    [Fact]
    public void EqualContexts_HaveEqualValues()
    {
        var first = new UpdateContext(0.25, 4);
        var equal = new UpdateContext(0.25, 4);
        var different = new UpdateContext(0.5, 2);

        Assert.Equal(first, equal);
        Assert.NotEqual(first, different);
    }
}
