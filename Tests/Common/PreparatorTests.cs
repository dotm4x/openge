using Mirage.Common;
using Mirage.Common.Primitives;
using Xunit;

namespace Mirage.Tests.Common;

/// <summary>Verifies preparation ordering, failure handling, and lifecycle integration.</summary>
public sealed class PreparatorTests
{
    /// <summary>Verifies that each phase executes once in order.</summary>
    [Fact]
    public void PreparationExecutesInOrderOnce()
    {
        var preparator = new Preparator();
        var phases = new List<string>();
        Assert.Equal(PreparationState.Unprepared, preparator.State);
        Assert.False(preparator.Prepared);
        void Compose()
        {
            Assert.Equal(PreparationState.Composing, preparator.State);
            phases.Add("compose");
        }
        void Configure()
        {
            Assert.Equal(PreparationState.Configuring, preparator.State);
            phases.Add("configure");
        }
        preparator.Ensure(Compose, Configure);
        preparator.Ensure(Compose, Configure);
        preparator.EnsureComposed(Compose);
        Assert.Equal(new[] { "compose", "configure" }, phases);
        Assert.True(preparator.Prepared);
    }

    /// <summary>Verifies that configuration can follow earlier composition.</summary>
    [Fact]
    public void CompositionCanPrecedeConfiguration()
    {
        var preparator = new Preparator();
        preparator.EnsureComposed(() => { });
        Assert.Equal(PreparationState.Composed, preparator.State);
        Assert.False(preparator.Prepared);
        preparator.Ensure(() => throw new Exception("Repeated composition"), () => { });
        Assert.True(preparator.Prepared);
    }

    /// <summary>Verifies that callback failures are permanent.</summary>
    /// <param name="failConfiguration">Whether configuration rather than composition fails.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailurePropagatesAndPreventsRetries(bool failConfiguration)
    {
        var preparator = new Preparator();
        var failure = new Exception("Failure");
        var configurationCalls = 0;
        void Compose()
        {
            if (!failConfiguration)
                throw failure;
        }
        void Configure()
        {
            configurationCalls++;
            throw failure;
        }
        Assert.Same(failure, Assert.Throws<Exception>(() => preparator.Ensure(Compose, Configure)));
        Assert.Equal(PreparationState.Failed, preparator.State);
        Assert.False(preparator.Prepared);
        Assert.Throws<InvalidOperationException>(() => preparator.Ensure(Compose, Configure));
        Assert.Throws<InvalidOperationException>(() => preparator.EnsureComposed(Compose));
        Assert.Equal(failConfiguration ? 1 : 0, configurationCalls);
    }

    /// <summary>Verifies that callbacks cannot recursively prepare the owner.</summary>
    /// <param name="duringConfiguration">Whether reentry occurs during configuration.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReentryIsRejectedInBothPhases(bool duringConfiguration)
    {
        var preparator = new Preparator();
        void Reenter() => preparator.Ensure(() => { }, () => { });
        Assert.Throws<InvalidOperationException>(() =>
            preparator.Ensure(
                duringConfiguration ? () => { } : Reenter,
                duringConfiguration ? Reenter : () => { }
            )
        );
        Assert.Equal(PreparationState.Failed, preparator.State);
    }

    /// <summary>Verifies argument validation before preparation begins.</summary>
    [Fact]
    public void NullCallbacksDoNotStartPreparation()
    {
        var preparator = new Preparator();
        Assert.Throws<ArgumentNullException>(() => preparator.EnsureComposed(null!));
        Assert.Throws<ArgumentNullException>(() => preparator.Ensure(() => { }, null!));
        Assert.Equal(PreparationState.Unprepared, preparator.State);
    }

    /// <summary>Verifies that engine restarts preserve completed preparation.</summary>
    [Fact]
    public void EngineRestartReusesPreparation()
    {
        var engine = new TestEngine();
        engine.Start();
        engine.Stop();
        engine.Start();
        engine.Stop();
        Assert.Equal(1, engine.Compositions);
        Assert.Equal(1, engine.Configurations);
        engine.Destroy();
    }

    private sealed class TestEngine : Engine
    {
        public int Compositions { get; private set; }
        public int Configurations { get; private set; }

        protected override IEnumerable<Module> Compose()
        {
            Compositions++;
            yield break;
        }

        protected override void Configure() => Configurations++;
    }
}
