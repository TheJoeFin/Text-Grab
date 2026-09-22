using Text_Grab.Views;

namespace Tests;

public class GrabFrameRedrawTests
{
    [Fact]
    public async Task RunAsync_NavigationDuringOcrDrawsOnlyLatestPageAndRejectsStaleResults()
    {
        GrabFrame.RedrawCoordinator redraws = new();
        TaskCompletionSource finishFirstOcr = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<int> startedPages = [];
        List<int> renderedPages = [];
        int activeDraws = 0;
        int maximumActiveDraws = 0;

        Task DrawPageAsync(int pageIndex)
        {
            return redraws.RunAsync(async version =>
            {
                activeDraws++;
                maximumActiveDraws = Math.Max(maximumActiveDraws, activeDraws);
                startedPages.Add(pageIndex);

                if (pageIndex == 0)
                    await finishFirstOcr.Task;

                if (redraws.IsCurrent(version))
                    renderedPages.Add(pageIndex);

                activeDraws--;
            });
        }

        Task firstDraw = DrawPageAsync(0);
        redraws.Invalidate();
        Task secondDraw = DrawPageAsync(1);
        redraws.Invalidate();
        Task latestDraw = DrawPageAsync(2);

        Assert.Equal([0], startedPages);
        Assert.False(secondDraw.IsCompleted);
        Assert.False(latestDraw.IsCompleted);

        finishFirstOcr.SetResult();
        await Task.WhenAll(firstDraw, secondDraw, latestDraw)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal([0, 2], startedPages);
        Assert.Equal([2], renderedPages);
        Assert.Equal(1, maximumActiveDraws);
        Assert.Equal(0, activeDraws);
    }

    [Fact]
    public async Task RunAsync_ResetWhileDrawsArePendingWaitsForNewPageRequest()
    {
        GrabFrame.RedrawCoordinator redraws = new();
        TaskCompletionSource finishOcr = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int startedDraws = 0;
        int renderedDraws = 0;

        Task firstDraw = redraws.RunAsync(async version =>
        {
            startedDraws++;
            await finishOcr.Task;
            if (redraws.IsCurrent(version))
                renderedDraws++;
        });
        Task queuedDraw = redraws.RunAsync(_ =>
        {
            startedDraws++;
            renderedDraws++;
            return Task.CompletedTask;
        });

        redraws.Invalidate();
        finishOcr.SetResult();
        await Task.WhenAll(firstDraw, queuedDraw)
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(1, startedDraws);
        Assert.Equal(0, renderedDraws);

        await redraws.RunAsync(version =>
        {
            Assert.True(redraws.IsCurrent(version));
            startedDraws++;
            renderedDraws++;
            return Task.CompletedTask;
        });

        Assert.Equal(2, startedDraws);
        Assert.Equal(1, renderedDraws);
    }

    [Fact]
    public async Task RunAsync_InvalidationWithoutPendingRequestDoesNotRedraw()
    {
        GrabFrame.RedrawCoordinator redraws = new();
        TaskCompletionSource finishOcr = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int drawCount = 0;
        bool appliedResult = false;

        Task draw = redraws.RunAsync(async version =>
        {
            drawCount++;
            await finishOcr.Task;
            appliedResult = redraws.IsCurrent(version);
        });

        redraws.Invalidate();
        finishOcr.SetResult();
        await draw.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(1, drawCount);
        Assert.False(appliedResult);
    }

    [Fact]
    public async Task RunAsync_CompletedDrawIsNotRepeated()
    {
        GrabFrame.RedrawCoordinator redraws = new();
        int drawCount = 0;

        await redraws.RunAsync(version =>
        {
            Assert.True(redraws.IsCurrent(version));
            drawCount++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, drawCount);
    }

    [Fact]
    public async Task RunAsync_FailedOcrReleasesPendingRedraw()
    {
        GrabFrame.RedrawCoordinator redraws = new();
        TaskCompletionSource finishOcr = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool renderedLatestPage = false;

        Task failedDraw = redraws.RunAsync(async _ =>
        {
            await finishOcr.Task;
            throw new InvalidOperationException("OCR failed.");
        });
        Task latestDraw = redraws.RunAsync(version =>
        {
            renderedLatestPage = redraws.IsCurrent(version);
            return Task.CompletedTask;
        });

        Assert.False(latestDraw.IsCompleted);
        finishOcr.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => failedDraw.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        await latestDraw.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(renderedLatestPage);
    }
}
