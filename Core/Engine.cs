namespace WinPla;

public static class Engine
{
    const int SettledCycles = 3, MaxAttempts = 15;

    sealed class Track
    {
        public int Settled, Attempts;
        public HashSet<PlaceStatus> Logged = [];
        public bool Done => Settled >= SettledCycles || Attempts >= MaxAttempts;
    }

    /// <summary>One pass over the existing windows. One PlaceResult per matched window, or one with WindowNotFound per rule with no windows.</summary>
    public static IReadOnlyList<PlaceResult> ApplyOnce(Config config) => Pass(config, null);

    /// <summary>Watches for config.WatchSeconds (or until cancelled) and places every new window covered by the rules.</summary>
    public static async Task WatchAsync(Config config, CancellationToken ct)
    {
        Log.Write($"Watch started ({config.WatchSeconds} s, {config.Rules.Count} rules)");
        var track = new Dictionary<nint, Track>();
        var clock = System.Diagnostics.Stopwatch.StartNew();   // immune to clock changes (time sync at sign-in)
        while (!ct.IsCancellationRequested && clock.Elapsed.TotalSeconds < config.WatchSeconds)
        {
            try { Pass(config, track); }
            catch (Exception ex) { Log.Write("Error in watch cycle: " + ex); }
            // ponytail: polls every 1 s; switch to SetWinEventHook(EVENT_OBJECT_SHOW) if the overhead matters
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { break; }
        }
        Log.Write("Watch finished");
    }

    // track == null: single pass. With track: per-HWND state (a window stops being watched once it settles or runs out of attempts).
    // Runs on thread-pool threads (after await, Task.Run): switches the thread to PerMonitorV2 and restores it.
    static List<PlaceResult> Pass(Config config, Dictionary<nint, Track>? track)
    {
        var oldDpi = NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.PER_MONITOR_AWARE_V2);
        try { return PassPhysical(config, track); }
        finally { NativeMethods.SetThreadDpiAwarenessContext(oldDpi); }
    }

    static List<PlaceResult> PassPhysical(Config config, Dictionary<nint, Track>? track)
    {
        var monitors = Monitors.GetAll();
        var windows = AppWindows.GetAll();
        var results = new List<PlaceResult>();
        foreach (var rule in config.Rules.Where(r => r.Enabled))
        {
            var matched = windows.Where(w => string.Equals(w.ExeName, rule.ExeName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matched.Count == 0) { results.Add(new PlaceResult(rule, null, PlaceStatus.WindowNotFound)); continue; }
            var mon = monitors.FirstOrDefault(m => string.Equals(m.Id, rule.MonitorId, StringComparison.OrdinalIgnoreCase));
            foreach (var w in matched)
            {
                Track? t = null;
                if (track is not null)
                {
                    if (!track.TryGetValue(w.Handle, out t)) track[w.Handle] = t = new Track();
                    if (t.Done) continue;
                }
                // A hung window would block SetWindowPlacement; it is reported as Failed but retried on the next pass.
                bool hung = mon is not null && NativeMethods.IsHungAppWindow(w.Handle);
                var status = mon is null ? PlaceStatus.MonitorDisconnected : hung ? PlaceStatus.Failed : Placer.Apply(w, rule, mon);
                results.Add(new PlaceResult(rule, w, status));
                if (t is null) continue;
                if (status != PlaceStatus.AlreadyOk && t.Logged.Add(status))
                    Log.Write($"{status}{(hung ? " (not responding)" : "")}: {w.ExeName} \"{w.Title}\" (hwnd {w.Handle:X}) -> {mon?.Label ?? rule.MonitorLabel}");
                // Neither counts as an attempt: a monitor that wakes up late or a window that responds again still gets placed.
                if (mon is null || hung) continue;
                t.Attempts++;
                t.Settled = status == PlaceStatus.AlreadyOk ? t.Settled + 1 : 0;
            }
        }
        return results;
    }
}
