using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>
    /// Top 10 rounds and overall stats (games, wins, win rate), saved on the device (PlayerPrefs).
    /// A round is recorded when you die or win.
    /// </summary>
    public static class HighScores
    {
        public const string PrefsKey = "TerritoryIO.Scores";
        public const int MaxEntries = 10;

        [Serializable]
        public class Entry
        {
            public string name;
            public float percent;
            public int kills;
            public bool won;
            public string date;
        }

        [Serializable]
        public class Table
        {
            public List<Entry> entries = new List<Entry>();
            public int games;
            public int wins;
            public int totalKills;
            public float bestPercent;

            public float WinRate => games > 0 ? 100f * wins / games : 0f;
        }

        public static Table Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var t = JsonUtility.FromJson<Table>(json);
                    if (t != null)
                    {
                        if (t.entries == null) t.entries = new List<Entry>();
                        return t;
                    }
                }
                catch (Exception) { /* corrupt prefs: start a new table */ }
            }
            return new Table();
        }

        /// <summary>Records a round. Returns its place in the top 10 (1-10), or 0 if it didn't make it.</summary>
        public static int Add(string name, float percent, int kills, bool won)
        {
            Table t = Load();
            t.games++;
            if (won) t.wins++;
            t.totalKills += kills;
            t.bestPercent = Mathf.Max(t.bestPercent, percent);

            var e = new Entry
            {
                name = name,
                percent = percent,
                kills = kills,
                won = won,
                date = DateTime.Now.ToString("yyyy-MM-dd")
            };
            t.entries.Add(e);
            t.entries.Sort((a, b) =>
            {
                int c = b.percent.CompareTo(a.percent);
                return c != 0 ? c : b.kills.CompareTo(a.kills);
            });
            if (t.entries.Count > MaxEntries) t.entries.RemoveRange(MaxEntries, t.entries.Count - MaxEntries);

            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(t));
            PlayerPrefs.Save();
            int index = t.entries.IndexOf(e);
            return index >= 0 ? index + 1 : 0;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }
    }
}
