using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AsusFanControl.Core
{
    public class FanProfile
    {
        [JsonPropertyName("n")]
        public string Name { get; set; }

        [JsonPropertyName("c")]
        public FanCurve Curve { get; set; }

        [JsonPropertyName("p")]
        public List<string> TriggerProcesses { get; set; } = new List<string>();

        public FanProfile() { }

        public FanProfile(string name, FanCurve curve, IEnumerable<string> triggerProcesses)
        {
            Name = name;
            Curve = curve;
            TriggerProcesses = triggerProcesses.ToList();
        }

        public string ToJson() => JsonSerializer.Serialize(this);

        public static FanProfile FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<FanProfile>(json);
            }
            catch
            {
                return null;
            }
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

                try
                {
                    var data = JsonSerializer.Deserialize<List<FanProfile>>(serialized);
                    if (data != null)
                        _profiles.AddRange(data);
                }
                catch (JsonException)
                {
                    Debug.WriteLine("Settings are not valid JSON.");
                }
            }
        }

        public string SaveProfiles()
        {
            lock (_lock)
            {
                return JsonSerializer.Serialize(_profiles);
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
                name = name.Substring(0, name.Length - 4);
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
