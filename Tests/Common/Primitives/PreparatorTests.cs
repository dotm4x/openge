using OpenGE.Common.Primitives;
using Xunit;

namespace Tests.Common.Primitives;

public class PreparatorTests
{
    [Fact]
    public void Ensure_ComposesBeforeConfiguringAndRunsEachOnce()
    {
        var preparator = new Preparator();
        var calls = new List<string>();

        preparator.Ensure(() => calls.Add("compose"), () => calls.Add("configure"));
        preparator.Ensure(() => calls.Add("compose"), () => calls.Add("configure"));

        Assert.Equal(new[] { "compose", "configure" }, calls);
        Assert.Equal(PreparationState.Prepared, preparator.State);
        Assert.True(preparator.Prepared);
    }

    [Fact]
    public void EnsureComposed_ThenEnsure_DoesNotRepeatComposition()
    {
        var preparator = new Preparator();
        var composeCount = 0;
        var configureCount = 0;

        preparator.EnsureComposed(() => composeCount++);
        Assert.Equal(PreparationState.Composed, preparator.State);
        preparator.Ensure(() => composeCount++, () => configureCount++);

        Assert.Equal(1, composeCount);
        Assert.Equal(1, configureCount);
        Assert.True(preparator.Prepared);
    }

    [Fact]
    public void Ensure_WhenCompositionThrows_FailsPermanently()
    {
        var preparator = new Preparator();
        var expected = new InvalidOperationException();

        Assert.Same(
            expected,
            Assert.Throws<InvalidOperationException>(() =>
                preparator.Ensure(() => throw expected, () => { })
            )
        );

        Assert.Equal(PreparationState.Failed, preparator.State);
        Assert.Throws<InvalidOperationException>(() => preparator.Ensure(() => { }, () => { }));
    }

    [Fact]
    public void Ensure_WhenConfigurationThrows_DoesNotRepeatCompositionOrConfiguration()
    {
        var preparator = new Preparator();
        var composeCount = 0;
        var configureCount = 0;

        Assert.Throws<InvalidOperationException>(() =>
            preparator.Ensure(
                () => composeCount++,
                () =>
                {
                    configureCount++;
                    throw new InvalidOperationException();
                }
            )
        );

        Assert.Equal(PreparationState.Failed, preparator.State);
        Assert.Throws<InvalidOperationException>(() =>
            preparator.Ensure(() => composeCount++, () => configureCount++)
        );
        Assert.Equal(1, composeCount);
        Assert.Equal(1, configureCount);
    }
}
