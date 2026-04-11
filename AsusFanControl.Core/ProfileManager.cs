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
            // Format: name|curveData|proc1;proc2;proc3
            var curveStr = Curve?.ToString() ?? "";
            var procStr = string.Join(";", TriggerProcesses);
            return $"{Name}|{curveStr}|{procStr}";
        }

        public static FanProfile FromString(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return null;
            var parts = data.Split('|');
            if (parts.Length < 3) return null;

            return new FanProfile
            {
                Name = parts[0],
                Curve = FanCurve.FromString(parts[1]),
                TriggerProcesses = parts[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList()
            };
        }
    }

    public class ProfileManager
    {
        private readonly List<FanProfile> _profiles = new List<FanProfile>();
        private string _activeProfileName;

        public IReadOnlyList<FanProfile> Profiles => _profiles;
        public string ActiveProfileName => _activeProfileName;

        public void LoadProfiles(string serialized)
        {
            _profiles.Clear();
            if (string.IsNullOrWhiteSpace(serialized)) return;

            var entries = serialized.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var profile = FanProfile.FromString(entry);
                if (profile != null)
                    _profiles.Add(profile);
            }
        }

        public string SaveProfiles()
        {
            return string.Join("||", _profiles.Select(p => p.ToString()));
        }

        public void AddProfile(FanProfile profile)
        {
            _profiles.RemoveAll(p => p.Name == profile.Name);
            _profiles.Add(profile);
        }

        public void RemoveProfile(string name)
        {
            _profiles.RemoveAll(p => p.Name == name);
        }

        public FanProfile CheckActiveProfile(FanCurve defaultCurve)
        {
            try
            {
                var runningProcesses = new HashSet<string>(
                    Process.GetProcesses().Select(p =>
                    {
                        try { return p.ProcessName.ToLowerInvariant(); }
                        catch { return null; }
                    }).Where(n => n != null),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (var profile in _profiles)
                {
                    if (profile.TriggerProcesses.Any(tp =>
                        runningProcesses.Contains(tp.Replace(".exe", "").ToLowerInvariant())))
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
