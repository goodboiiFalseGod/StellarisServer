using System.Runtime.CompilerServices;
using Stellaris.Session.M3;

[assembly: InternalsVisibleTo("Stellaris.M3ServerChecks")]

namespace Stellaris.Server;

/// <summary>Runtime-owned completion diagnostics; never supplies simulation time or policy.</summary>
internal sealed class M3SimulationRate
{
    private const int CAPACITY = 512;
    private readonly TimeSpan[] _completions = new TimeSpan[CAPACITY];
    private TimeSpan? _last;
    private TimeSpan? _epoch;
    private string _mode = "not-running";
    private int _head;
    private int _count;
    private bool _invalid;

    internal M3SimulationRateView Observe(TimeSpan now, string phase, bool paused, bool reset = false, bool completed = false)
    {
        if (now < TimeSpan.Zero || _last is { } last && now < last)
        {
            Clear();
            _invalid = true;
            return new("unavailable", null, 0, 0, null);
        }
        _last = now;
        var mode = phase != "running" ? "not-running" : paused ? "paused" : "running";
        if (reset || _invalid || mode != _mode)
        {
            Clear();
            _epoch = mode == "running" ? now : null;
        }
        _mode = mode;
        _invalid = false;
        if (mode != "running") { return new(mode, null, 0, 0, now.TotalMilliseconds); }
        _epoch ??= now;
        var cutoff = now >= TimeSpan.FromSeconds(10) ? now - TimeSpan.FromSeconds(10) : TimeSpan.Zero;
        if (_epoch.Value > cutoff) { cutoff = _epoch.Value; }
        while (_count > 0 && _completions[_head] <= cutoff)
        {
            _head = (_head + 1) % CAPACITY;
            _count--;
        }
        if (completed && now > cutoff)
        {
            if (_count == CAPACITY)
            {
                Clear();
                _invalid = true;
                return new("unavailable", null, 0, 0, now.TotalMilliseconds);
            }
            _completions[(_head + _count) % CAPACITY] = now;
            _count++;
        }
        var seconds = (now - cutoff).TotalSeconds;
        return seconds < 2 ? new("warming-up", null, _count, seconds, now.TotalMilliseconds) : new("measured", _count / seconds, _count, seconds, now.TotalMilliseconds);
    }

    private void Clear()
    {
        _head = 0;
        _count = 0;
        _epoch = null;
    }
}
