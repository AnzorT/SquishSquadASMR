// The Main Menu's crest opens the Level Pass (menu.js: tapping .sq-crest calls SquishMenu.levels). Kept apart from
// MainMenuScreen.cs (both classes are partial) so the two can change separately.
using System.Collections.Generic;
using PlushCrush.App.LevelPass;

namespace PlushCrush.App.MainMenu
{
    public sealed partial class MenuState
    {
        /// <summary>XP per level (state.xp.per: config/levels xpPerLevel), for the pass's "N XP to go".</summary>
        public int XpPer = 100;

        /// <summary>The Level Pass's prizes (state.road), level 1 first. Null: none, as the page has it with no app
        /// data (every level shown, no prizes; XP 0 of 0 unless XpInto / XpNeed are set).</summary>
        public List<RoadPrize> Road;
    }

    public sealed partial class MainMenuScreen
    {
        /// <summary>Claims made on the Level Pass. Null: each is marked claimed at once (the page outside the app).</summary>
        public LevelPassClaimHandler OnLevelClaim;

        void OpenLevelPass()
        {
            // what the page reads: state.xp (or { level, 0, 0, 100 } without it), state.road, the wallet, the creature
            var pass = new LevelPassState
            {
                xp = new LevelXp
                {
                    level = _state.Level,
                    into = (long)(_state.XpInto ?? 0f),
                    need = (long)(_state.XpNeed ?? 0f),
                    per = _state.XpPer,
                },
                road = _state.Road != null ? new List<RoadPrize>(_state.Road) : new List<RoadPrize>(),
                coins = _state.Coins,
                creature = _state.CreatureKey,
            };
            App.Router.Show(new LevelPassScreen(pass, OnLevelClaim), remember: true);
        }
    }
}
