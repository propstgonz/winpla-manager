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

    /// <summary>Una pasada sobre las ventanas existentes. Un PlaceResult por ventana casada, o uno con WindowNotFound por regla sin ventanas.</summary>
    public static IReadOnlyList<PlaceResult> ApplyOnce(Config config) => Pass(config, null);

    /// <summary>Vigila durante config.WatchSeconds (o hasta cancelación) y coloca cada ventana nueva de las reglas.</summary>
    public static async Task WatchAsync(Config config, CancellationToken ct)
    {
        Log.Write($"Vigilancia iniciada ({config.WatchSeconds} s, {config.Rules.Count} reglas)");
        var track = new Dictionary<nint, Track>();
        var end = DateTime.UtcNow.AddSeconds(config.WatchSeconds);
        while (!ct.IsCancellationRequested && DateTime.UtcNow < end)
        {
            try { Pass(config, track); }
            catch (Exception ex) { Log.Write("Error en ciclo de vigilancia: " + ex); }
            // ponytail: sondeo cada 1 s; pasar a SetWinEventHook(EVENT_OBJECT_SHOW) si el consumo importa
            try { await Task.Delay(1000, ct); }
            catch (OperationCanceledException) { break; }
        }
        Log.Write("Vigilancia terminada");
    }

    // track == null: pasada única. Con track: estado por HWND (se deja de vigilar al asentarse o agotar intentos).
    static List<PlaceResult> Pass(Config config, Dictionary<nint, Track>? track)
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
                var status = mon is null ? PlaceStatus.MonitorDisconnected : Placer.Apply(w, rule, mon);
                results.Add(new PlaceResult(rule, w, status));
                if (t is null) continue;
                t.Attempts++;
                t.Settled = status == PlaceStatus.AlreadyOk ? t.Settled + 1 : 0;
                if (status != PlaceStatus.AlreadyOk && t.Logged.Add(status))
                    Log.Write($"{status}: {w.ExeName} \"{w.Title}\" (hwnd {w.Handle:X}) -> {mon?.Label ?? rule.MonitorLabel}");
            }
        }
        return results;
    }
}
