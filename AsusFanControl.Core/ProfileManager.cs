using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AsusFanControl.Core
{
    public class FanProfile
    {
        public string Name { get; set; }
        public FanCurve Curve { get; set; }
        public List<string> TriggerProcesses { get; set; } = new List<string>();

        public FanProfile() { }

        public FanProfile(string name, FanCurve curve, IEnumerable<string> triggerProcesses)
        {
            Name = name;
            Curve = curve;
            TriggerProcesses = triggerProcesses.ToList();
        }

        public override string ToString()
        {
            var curveStr = Curve?.ToString() ?? "";
            var procStr = string.Join(";", TriggerProcesses.Select(p => Uri.EscapeDataString(p)));
            return $"{Uri.EscapeDataString(Name)}|{curveStr}|{procStr}";
        }

        public static FanProfile FromString(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return null;
            var parts = data.Split('|');
            if (parts.Length < 3) return null;

            return new FanProfile
            {
                Name = Uri.UnescapeDataString(parts[0]),
                Curve = FanCurve.FromString(parts[1]),
                TriggerProcesses = parts[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.UnescapeDataString).ToList()
            };
        }
    }

    public class ProfileManager
    {
        private readonly List<FanProfile> _profiles = new List<FanProfile>();
        private readonly object _lock = new object();
        private string _activeProfileName;

        public IReadOnlyList<FanProfile> Profiles => _profiles;
        public string ActiveProfileName => _activeProfileName;

        public void LoadProfiles(string serialized)
        {
            lock (_lock)
            {
                _profiles.Clear();
                _activeProfileName = null;
                if (string.IsNullOrWhiteSpace(serialized)) return;

                var entries = serialized.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var entry in entries)
                {
                    var profile = FanProfile.FromString(entry);
                    if (profile != null)
                        _profiles.Add(profile);
                }
            }
        }

        public string SaveProfiles()
        {
            lock (_lock)
            {
                return string.Join("||", _profiles.Select(p => p.ToString()));
            }
        }

        public void AddProfile(FanProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            lock (_lock)
            {
                _profiles.RemoveAll(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
                _profiles.Add(profile);
            }
        }

        public void RemoveProfile(string name)
        {
            lock (_lock)
            {
                _profiles.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static string NormalizeProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return null;
            var name = processName;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            return name.ToLowerInvariant();
        }

        public FanProfile CheckActiveProfile(FanCurve defaultCurve)
        {
            try
            {
                var runningProcesses = new HashSet<string>(
                    Process.GetProcesses().Select(p =>
                    {
                        try { return NormalizeProcessName(p.ProcessName); }
                        catch { return null; }
                    }).Where(n => n != null),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (var profile in _profiles)
                {
                    if (profile.TriggerProcesses == null) continue;
                    if (profile.TriggerProcesses.Any(tp =>
                    {
                        var normalized = NormalizeProcessName(tp);
                        return normalized != null && runningProcesses.Contains(normalized);
                    }))
                    {
                        _activeProfileName = profile.Name;
                        return profile;
                    }
                }
            }
            catch
            {
                // Ignore process enumeration errors
            }

            _activeProfileName = null;
            return null;
        }
    }
}
