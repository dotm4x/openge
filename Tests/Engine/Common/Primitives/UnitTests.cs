using OpenGE.Common.Primitives;
using Xunit;

namespace Tests.Common.Primitives;

public class UnitTests
{
    [Fact]
    public void Value_IsEqualToDefaultValue()
    {
        Assert.Equal(default, Unit.Value);
    }
}
