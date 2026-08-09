using System.Collections.Generic;
using UnityEngine;

namespace PixelRoad.Data
{
    public sealed class UnlockRepository
    {
        private readonly string key;
        private readonly HashSet<string> unlockedIds = new HashSet<string>();

        public UnlockRepository(string storageKey)
        {
            key = storageKey;
            Load();
        }

        public bool IsUnlocked(SpotDefinition spot)
        {
            return spot.InitiallyUnlocked || unlockedIds.Contains(spot.Id);
        }

        public bool SaveUnlocked(string spotId)
        {
            if (!unlockedIds.Add(spotId))
            {
                return false;
            }

            PlayerPrefs.SetString(key, string.Join("|", unlockedIds));
            PlayerPrefs.Save();
            return true;
        }

        private void Load()
        {
            unlockedIds.Clear();
            string raw = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] ids = raw.Split('|');
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]))
                {
                    unlockedIds.Add(ids[i]);
                }
            }
        }
    }
}
