using OpenGE.Common.Primitives;
using Xunit;

namespace Tests.Common.Primitives;

public class SequenceTests
{
    [Fact]
    public void Start_ExecutesAllStepsInOrder()
    {
        var calls = new List<int>();
        var sequence = new Sequence().Step(_ => calls.Add(1)).Step(_ => calls.Add(2));

        var result = sequence.Start();

        Assert.Same(sequence, result);
        Assert.Equal(new[] { 1, 2 }, calls);
        Assert.True(sequence.Completed);
        Assert.False(sequence.Running);
        Assert.Equal(2, sequence.Index);
    }

    [Fact]
    public void Next_ExecutesOneStepAndProvidesItsIndex()
    {
        var contexts = new List<SequenceContext>();
        var sequence = new Sequence().Step(context => contexts.Add(context));

        Assert.True(sequence.Next());
        Assert.False(sequence.Next());

        Assert.Single(contexts);
        Assert.Same(sequence, contexts[0].Sequence);
        Assert.Equal(0, contexts[0].Index);
        Assert.True(sequence.Completed);
    }

    [Fact]
    public void Skip_DuringStep_OmitsRequestedSteps()
    {
        var calls = new List<int>();
        var sequence = new Sequence()
            .Step(context =>
            {
                calls.Add(0);
                context.Sequence.Skip();
            })
            .Step(_ => calls.Add(1))
            .Step(_ => calls.Add(2));

        sequence.Start();

        Assert.Equal(new[] { 0, 2 }, calls);
    }

    [Fact]
    public void Pause_DuringStep_PausesAfterCurrentStepAndCanResume()
    {
        var calls = new List<int>();
        var sequence = new Sequence()
            .Step(context =>
            {
                calls.Add(1);
                context.Sequence.Pause();
            })
            .Step(_ => calls.Add(2));

        sequence.Start();
        Assert.Equal(new[] { 1 }, calls);
        Assert.True(sequence.Paused);
        Assert.False(sequence.Running);

        sequence.Start();

        Assert.Equal(new[] { 1, 2 }, calls);
        Assert.True(sequence.Completed);
    }

    [Fact]
    public void Restart_DuringStep_RunsFromBeginningAfterCurrentStep()
    {
        var calls = new List<int>();
        var sequence = new Sequence();
        var restartOnce = true;
        sequence
            .Step(context =>
            {
                calls.Add(0);
                if (restartOnce)
                {
                    restartOnce = false;
                    context.Sequence.Restart();
                }
            })
            .Step(_ => calls.Add(1));

        sequence.Start();

        Assert.Equal(new[] { 0, 0, 1 }, calls);
        Assert.True(sequence.Completed);
    }

    [Fact]
    public void Step_WhenCalledDuringExecution_ThrowsWithoutAddingStep()
    {
        var sequence = new Sequence();
        sequence.Step(_ => sequence.Step(_ => { }));

        Assert.Throws<InvalidOperationException>(() => sequence.Start());
        Assert.Equal(1, sequence.Count);
    }

    [Fact]
    public void Step_WhenActionThrows_StopsAndAllowsReset()
    {
        var sequence = new Sequence().Step(_ => throw new InvalidOperationException());

        Assert.Throws<InvalidOperationException>(() => sequence.Start());
        Assert.False(sequence.Running);
        Assert.False(sequence.Paused);
        Assert.Equal(1, sequence.Index);

        sequence.Reset();

        Assert.Equal(0, sequence.Index);
        Assert.False(sequence.Completed);
    }

    [Fact]
    public void Clear_RemovesStepsAndResetsExecution()
    {
        var sequence = new Sequence().Step(_ => { });
        sequence.Next();

        sequence.Clear();

        Assert.Equal(0, sequence.Count);
        Assert.Equal(0, sequence.Index);
        Assert.True(sequence.Completed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void GoTo_WithInvalidIndex_ThrowsArgumentOutOfRangeException(int index)
    {
        var sequence = new Sequence().Step(_ => { });

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.GoTo(index));
    }

    [Fact]
    public void Skip_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var sequence = new Sequence();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Skip(-1));
    }

    [Fact]
    public void SkipTo_BeforeCurrentPosition_ThrowsArgumentOutOfRangeException()
    {
        var sequence = new Sequence().Step(_ => { }).Step(_ => { });
        sequence.Next();

        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.SkipTo(0));
    }

    [Fact]
    public void Step_WithNullAction_ThrowsArgumentNullException()
    {
        var sequence = new Sequence();

        Assert.Throws<ArgumentNullException>(() => sequence.Step(null!));
    }
}
