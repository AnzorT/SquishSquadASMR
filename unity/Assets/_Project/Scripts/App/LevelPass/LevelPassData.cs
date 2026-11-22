// The Level Pass's data, as the page gets it from the app (src/game/player.js gamePlayer: xp, road; the wallet's
// coins; the selected creature), and the page's own rules for what each level shows (webgame/app/menu.js levelData,
// xpTo, rewardCard). Field names follow the app's JSON, so layout.json's stand-in player reads straight in.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PlushCrush.App.LevelPass
{
    /// <summary>state.xp: the player's level, XP into it and needed for the next; per = config/levels xpPerLevel.</summary>
    [Serializable]
    public sealed class LevelXp
    {
        public int level = 1;
        public long into;
        public long need;
        public int per = 100;
    }

    /// <summary>One level's prize on the road (state.road[L - 1]). k: coins, gems or chest; n: the amount; t: the
    /// chest's size (common, rare, epic, legendary, set); g / gn / cn: the chest's group, group name, own name.</summary>
    [Serializable]
    public sealed class RoadPrize
    {
        public int lv;
        public string k;
        public long n;
        public string t, g, gn, cn;
        public bool got, claimable;
    }

    /// <summary>What the Level Pass shows. select: the level picked when it opens (0 = as the page picks: the first
    /// prize waiting, else the next level).</summary>
    [Serializable]
    public sealed class LevelPassState
    {
        public LevelXp xp = new LevelXp();
        public List<RoadPrize> road = new List<RoadPrize>();
        public long coins;
        public string creature = "mittens";
        public int select;
    }

    /// <summary>A claim the pass asks for: one level (CLAIM, or a second tap on its node) or all of them (CLAIM ALL;
    /// Level is then the first of them, as the page sends it).</summary>
    public sealed class LevelPassClaim
    {
        public int Level;
        public string Kind;
        public bool All;
    }

    /// <summary>The app's answer (GameScreen.js 'claimed'): Ok, or Error ("claimed", "need_level", anything else);
    /// Road: a chest, which the app opens on its own screen (the pass leaves it "Claiming…").</summary>
    public sealed class LevelPassClaimResult
    {
        public bool Ok;
        public string Error;
        public bool Road;
    }

    /// <summary>Claims go to the caller (the backend, later). Call done once with the result, on the main thread.</summary>
    public delegate void LevelPassClaimHandler(LevelPassClaim claim, Action<LevelPassClaimResult> done);

    /// <summary>Something a level unlocks: a room, the yard games, the house's next look (menu.js levelData).</summary>
    public sealed class LevelUnlock
    {
        public string Key, Name, Kind; // kind: room, yard, tier
        public int Tier;
    }

    /// <summary>One level on the track (menu.js levelData's rows).</summary>
    public sealed class LevelRow
    {
        public int L;
        public RoadPrize Prize;
        public readonly List<LevelUnlock> Unlocks = new List<LevelUnlock>();
        public bool Reached, Got, Claimable;
    }

    /// <summary>A reward card's content (menu.js rewardCard).</summary>
    public sealed class RewardCard
    {
        public string Tag, Title, Desc;
        public string ArtKind; // chest, plainChest, pile, room
        public string ArtKey;  // chest: g-t; plainChest: t; pile: coins / gems; room: kitchen, yard, tier1...
    }

    public static class LevelPassRules
    {
        // main-menu-scene.js ROOMS, in its order (living opens at 1: never shown as an unlock)
        static readonly string[] RoomKeys = { "living", "kitchen", "bathroom", "bedroom", "backyard", "dance", "movie", "beach" };
        static readonly string[] RoomNames = { "Living Room", "Kitchen", "Bathroom", "Bedroom", "Backyard", "Dance Room", "Movie Room", "Beach" };
        static readonly int[] RoomLevels = { 1, 2, 4, 6, 8, 11, 12, 30 };
        const int BackyardLevel = 8;

        public static string ChestName(string t)
        {
            switch (t)
            {
                case "common": return "Common";
                case "rare": return "Rare";
                case "epic": return "Epic";
                case "legendary": return "Legendary";
                case "set": return "Rainbow";
                default: return "";
            }
        }

        /// <summary>A number as the page writes it (toLocaleString('en-US')): 1,250.</summary>
        public static string Fmt(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Every level on the track: the road's length, at least 30 (menu.js levelData).</summary>
        public static List<LevelRow> Rows(LevelPassState s)
        {
            var road = s.road ?? new List<RoadPrize>();
            int level = s.xp != null ? s.xp.level : 1;
            int n = Math.Max(road.Count, 30);
            var rows = new List<LevelRow>(n);
            for (int L = 1; L <= n; L++)
            {
                var r = L - 1 < road.Count ? road[L - 1] : null;
                var row = new LevelRow { L = L, Prize = r, Reached = level >= L, Got = r != null && r.got, Claimable = r != null && r.claimable };
                for (int i = 0; i < RoomKeys.Length; i++)
                    if (RoomLevels[i] == L && L > 1) row.Unlocks.Add(new LevelUnlock { Key = RoomKeys[i], Name = RoomNames[i], Kind = "room" });
                if (L == BackyardLevel) row.Unlocks.Add(new LevelUnlock { Key = "yard", Name = "Yard Games", Kind = "yard" });
                if (L == 5) row.Unlocks.Add(new LevelUnlock { Key = "tier", Name = "Cozy home", Kind = "tier", Tier = 1 });
                if (L == 15) row.Unlocks.Add(new LevelUnlock { Key = "tier", Name = "Lovely home", Kind = "tier", Tier = 2 });
                if (L == 25) row.Unlocks.Add(new LevelUnlock { Key = "tier", Name = "Royal palace", Kind = "tier", Tier = 3 });
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>XP still to earn before level L (menu.js xpTo).</summary>
        public static long XpTo(LevelXp xp, int L)
        {
            if (xp.level >= L) return 0;
            long n = Math.Max(0, xp.need - xp.into);
            int per = xp.per > 0 ? xp.per : 100;
            for (int j = xp.level + 1; j < L; j++) n += (long)per * (j + 1);
            return n;
        }

        /// <summary>The cards under a level: its prize first, then what it unlocks (menu.js rewardCard).</summary>
        public static List<RewardCard> Cards(LevelRow row)
        {
            var cards = new List<RewardCard>();
            if (row.Prize != null)
            {
                var r = row.Prize;
                if (r.k == "chest")
                {
                    bool group = !string.IsNullOrEmpty(r.g);
                    cards.Add(new RewardCard
                    {
                        Tag = "CHEST",
                        Title = !string.IsNullOrEmpty(r.cn) ? r.cn : ChestName(r.t) + " Chest",
                        Desc = (!string.IsNullOrEmpty(r.gn) ? r.gn + " · " : "") + ChestName(r.t) + " chest",
                        ArtKind = group ? "chest" : "plainChest",
                        ArtKey = group ? r.g + "-" + r.t : r.t,
                    });
                }
                else
                {
                    bool gems = r.k == "gems";
                    cards.Add(new RewardCard
                    {
                        Tag = gems ? "GEMS" : "COINS",
                        Title = Fmt(r.n) + (gems ? " Gems" : " Coins"),
                        Desc = gems ? "Gems to spend in the Shop" : "Coins to spend in the Shop",
                        ArtKind = "pile",
                        ArtKey = gems ? "gems" : "coins",
                    });
                }
            }
            foreach (var u in row.Unlocks)
            {
                cards.Add(new RewardCard
                {
                    Tag = u.Kind == "tier" ? "HOUSE" : u.Kind == "yard" ? "GAMES" : "NEW ROOM",
                    Title = u.Name,
                    Desc = u.Kind == "room" ? "A new room opens in your house"
                        : u.Kind == "yard" ? "The backyard's 7 yard games (buy each one's toy there to play it)"
                        : "Your house gets its next look",
                    ArtKind = "room",
                    ArtKey = u.Kind == "tier" ? "tier" + u.Tier : u.Key,
                });
            }
            return cards;
        }
    }
}
