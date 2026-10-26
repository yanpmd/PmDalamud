using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ExamplePlugin;

public sealed partial class ExamplePlugin
{
    private const double AverageIntervalMilliseconds = 500.0;
    private const int    HistorySize                 = 120;

    private Stopwatch? _stopwatch;
    private Stopwatch? _loopTimer;
    private Stopwatch? _testTimer;
    private Stopwatch? _peakTimer;

    private Queue<double>? _frameTimes;
    private double         _fps;
    private double         _lastFrameTimeMilliseconds;
    private double         _averageFrameTimeMilliseconds;
    private double         _elapsedAverageTimeMilliseconds;
    private double         _testTime;
    private double         _testPeakTime;
    private float[]        _fpsHistory       = new float[HistorySize];
    private float[]        _frameTimeHistory = new float[HistorySize];
    private int            _dataIndex        = 0; // Current position in the circular buffer

    private void InitializeMeasurements()
    {
        _frameTimes                     = new Queue<double>();
        _lastFrameTimeMilliseconds      = 0.0;
        _averageFrameTimeMilliseconds   = 0.0;
        _elapsedAverageTimeMilliseconds = 0.0;

        _stopwatch = Stopwatch.StartNew();
        _loopTimer = Stopwatch.StartNew();
        _testTimer = Stopwatch.StartNew();
        _peakTimer = Stopwatch.StartNew();
    }

    private void UpdateFrameTime(double deltaTime)
    {
        long elapsedTicks = _stopwatch!.ElapsedTicks;
        _stopwatch.Restart();

        _lastFrameTimeMilliseconds = (elapsedTicks * 1000.0) / Stopwatch.Frequency;

        _frameTimes!.Enqueue(_lastFrameTimeMilliseconds);
        _elapsedAverageTimeMilliseconds += deltaTime;

        while (_elapsedAverageTimeMilliseconds >= AverageIntervalMilliseconds) {
            double sum = 0;
            foreach (double frameTime in _frameTimes) {
                sum += frameTime;
            }

            _averageFrameTimeMilliseconds = sum / _frameTimes.Count;
            _frameTimes.Clear();
            _elapsedAverageTimeMilliseconds -= AverageIntervalMilliseconds;
            _fps                            =  1000.0 / _averageFrameTimeMilliseconds;
        }

        _fpsHistory[_dataIndex]       = (float)(1000.0f / _lastFrameTimeMilliseconds);
        _frameTimeHistory[_dataIndex] = (float)_lastFrameTimeMilliseconds;
        _dataIndex                    = (_dataIndex + 1) % HistorySize;
    }

    private string GetMetricsString()
    {
        StringBuilder sb = new();

        sb.Append($"FPS: {_fps:F2}\t");
        sb.AppendLine($"Frame Time: {_averageFrameTimeMilliseconds:F2} ms\t\t");
        sb.Append($"Draw Time: {_testTime:F2} ms\t");
        sb.Append($"Peak: {_testPeakTime:F2} ms");

        return sb.ToString();
    }
}