using BenchmarkDotNet.Attributes;
using System;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Text_Grab.Views;

namespace Text_Grab.Benchmarks;

[MemoryDiagnoser]
public class FullscreenGrabMemoryBenchmark
{
    private Thread? _uiThread;
    private Dispatcher? _dispatcher;
    private Application? _app;
    private FullscreenGrab? _fsgWindow;
    private readonly AutoResetEvent _initDone = new(false);
    [GlobalSetup]
    public void GlobalSetup()
    {
        // Baseline GC
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        // Start STA UI thread and create Application + Dispatcher
        _uiThread = new Thread(() =>
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            _initDone.Set();
            Dispatcher.Run();
        });
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.IsBackground = true;
        _uiThread.Start();
        if (!_initDone.WaitOne(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("UI thread initialization timed out.");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_dispatcher != null)
        {
            _dispatcher.Invoke(() =>
            {
                _fsgWindow?.Close();
                _fsgWindow = null;
                _app?.Shutdown();
                _app = null;
            });
            _dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            if (_uiThread != null && !_uiThread.Join(2000))
            {
                try
                {
                    _uiThread.Abort();
                }
                catch
                { /* best-effort */
                }
            }

            _dispatcher = null;
            _uiThread = null;
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
    }

    [Benchmark(Description = "FSG Launch (No Freeze)")]
    public long LaunchFullscreenGrabNoFreeze()
    {
        if (_dispatcher == null)
            return 0;
        long memoryBefore = GC.GetTotalMemory(false);
        _dispatcher.Invoke(() =>
        {
            _fsgWindow = new FullscreenGrab();
            _fsgWindow.Show();
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
            }, DispatcherPriority.ApplicationIdle);
        }, DispatcherPriority.Normal);
        Thread.Sleep(100);
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;
        _dispatcher.Invoke(() =>
        {
            _fsgWindow?.Close();
            _fsgWindow = null;
        }, DispatcherPriority.Normal);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        Thread.Sleep(50);
        return memoryUsed;
    }

    [Benchmark(Description = "FSG Launch with Background Image")]
    public long LaunchFullscreenGrabWithFreeze()
    {
        if (_dispatcher == null)
            return 0;
        long memoryBefore = GC.GetTotalMemory(false);
        _dispatcher.Invoke(() =>
        {
            _fsgWindow = new FullscreenGrab();
            _fsgWindow.Show();
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
            }, DispatcherPriority.ApplicationIdle);
        }, DispatcherPriority.Normal);
        Thread.Sleep(150);
        
        // Trigger background image capture (simulates freeze mode)
        bool captureSuccess = false;
        _dispatcher.Invoke(() =>
        {
            try
            {
                _fsgWindow?.SetImageToBackground();
                captureSuccess = true;
            }
            catch
            {
                // Background capture may fail in headless/benchmark environment
                captureSuccess = false;
            }
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
            }, DispatcherPriority.ApplicationIdle);
        }, DispatcherPriority.Normal);
        
        Thread.Sleep(250);
        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;
        _dispatcher.Invoke(() =>
        {
            _fsgWindow?.Close();
            _fsgWindow = null;
        }, DispatcherPriority.Normal);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        Thread.Sleep(50);
        return memoryUsed;
    }

    [Benchmark(Description = "FSG Multiple Open/Close Cycles")]
    public long MultipleFullscreenGrabCycles()
    {
        if (_dispatcher == null)
            return 0;
        long memoryBefore = GC.GetTotalMemory(false);
        for (int i = 0; i < 5; i++)
        {
            _dispatcher.Invoke(() =>
            {
                _fsgWindow = new FullscreenGrab();
                _fsgWindow.Show();
            }, DispatcherPriority.Normal);
            Thread.Sleep(50);
            _dispatcher.Invoke(() =>
            {
                _fsgWindow?.Close();
                _fsgWindow = null;
            }, DispatcherPriority.Normal);
            Thread.Sleep(50);
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        GC.WaitForPendingFinalizers();
        Thread.Sleep(100);
        long memoryAfter = GC.GetTotalMemory(false);
        return memoryAfter - memoryBefore;
    }

    [Benchmark(Description = "FSG Background Image Toggle Cycles")]
    public long FreezeToggleCycles()
    {
        if (_dispatcher == null)
            return 0;
        long memoryBefore = GC.GetTotalMemory(false);
        _dispatcher.Invoke(() =>
        {
            _fsgWindow = new FullscreenGrab();
            _fsgWindow.Show();
        }, DispatcherPriority.Normal);
        Thread.Sleep(100);
        
        // Toggle background image on and off 3 times
        for (int i = 0; i < 3; i++)
        {
            _dispatcher.Invoke(() =>
            {
                try
                {
                    // Capture background
                    _fsgWindow?.SetImageToBackground();
                }
                catch { /* May fail in test environment */ }
            }, DispatcherPriority.Normal);
            Thread.Sleep(150);
            
            _dispatcher.Invoke(() =>
            {
                // Clear background image
                if (_fsgWindow != null)
                {
                    var imageField = typeof(FullscreenGrab).GetField("BackgroundImage", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (imageField?.GetValue(_fsgWindow) is System.Windows.Controls.Image img)
                    {
                        img.Source = null;
                    }
                }
            }, DispatcherPriority.Normal);
            Thread.Sleep(100);
        }

        long memoryAfter = GC.GetTotalMemory(false);
        long memoryUsed = memoryAfter - memoryBefore;
        _dispatcher.Invoke(() =>
        {
            _fsgWindow?.Close();
            _fsgWindow = null;
        }, DispatcherPriority.Normal);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive);
        Thread.Sleep(50);
        return memoryUsed;
    }
}
