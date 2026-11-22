// The Main Menu's Game Park overlay: the games carousel shown in the park after PLAY (webgame/app/menu.js showGames,
// its CSS .sq-pk ...), put back together from the pieces the game itself drew (tools/unity-port/export-game-park.mjs)
// and moved the way the page moves them. The menu screen lays it over itself once the walk to the park is done.
//
//   the dim            .sq-pkdim: opacity .5s ease
//   the header         back orb + "Game Park": opacity .4s ease, translateY(-14px -> 0) .5s cubic-bezier(.2,.9,.25,1)
//   rays, halo         static; opacity .6s ease .2s (to .55 and 1)
//   the cards          per slot (placement): transform .5s cubic-bezier(.3,1.3,.5,1), opacity .35s ease. The centre one
//                      at 0, scale 1; the others at +-round(CW * .45 + |d| * 40), scale .8, opacity 0, untouchable.
//                      Per card (entry): opacity .45s ease, translateX(160px) scale(.8) -> none .6s
//                      cubic-bezier(.2,1.2,.4,1), after .05 + |i - first centre| * .07 s.
//                      Becoming the centre one: sqCen .5s ease, brightness 1.35 -> 1 (a brightened twin of each
//                      picture fading out, the words' colours brightened); the others' buttons saturate(.7).
//   Squish Time        the creature squishing (sqSqz 1.9s ease-in-out infinite, from its bottom centre) with the
//                      squish lines (sqLines 1.9s ease-out infinite); Frybo's card plays its clip instead
//   arrows, dots       the arrows at .4 at either end; the current dot 18 wide and gold (transition all .3s)
//   gestures           arrows, dots, a swipe on the carousel (more than 36 px across, more across than down); a tap
//                      within 250 ms of a swipe is ignored; PLAY or the picture of the centre card: playAt (the card
//                      to .94 in .18s ease, to 1.04 at 180 ms, the game starts at 420 ms); a locked game: a toast
//   the creature       'cheer' (else 'happy') on PLAY, a random move every 6.5 s: raised as CreatureMove (menu.js
//                      allowed() filters by what the creature can do: AllowedMoves)
// The last game chosen is remembered (PlayerPrefs, as the page's localStorage 'squishsquad.park.v1').
// Every piece sits where layout.json says (A15 pixels); the carousel keeps its place from the right edge like the page's.
using System;
using System.Collections.Generic;
using PlushCrush.App.Motion;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

namespace PlushCrush.App.MainMenu
{
    /// <summary>A game on the carousel. Best: its progress chip (null: "New!"); Description: its line (null: the
    /// page's own words for it). Squish Time's chip and line are made from the creature's name.</summary>
    public sealed class GameParkGame
    {
        public string Id, Name, Best, Description;
        public bool Open = true;

        public GameParkGame(string id, string name, string best = null, bool open = true, string description = null)
        {
            Id = id; Name = name; Best = best; Open = open; Description = description;
        }

        /// <summary>The design's own first card (menu.js SQUISH): always open.</summary>
        public static GameParkGame SquishTime() => new GameParkGame("squish", "Squish Time");
    }

    public sealed class GameParkOverlay : VisualElement
    {
        const float K = 2.8125f;                       // A15 px per dp
        public const string LastGameKey = "squishsquad.park.v1";
        static readonly string[] IdleMoves = { "happy", "look", "love", "cheer", "wave", "hop" };
        static readonly string[] PlayMoves = { "cheer", "happy" };
        // menu.js DESC: each game's line on its card (the games' own words)
        static readonly Dictionary<string, string> Desc = new Dictionary<string, string>
        {
            { "squishdoku", "One creature in every row, column and patch. No two may touch!" },
            { "sort", "Remember who sits where. The doors shut, it shakes… sort them back!" },
            { "stack", "Move them one at a time to match the goal. Big never sits on small!" },
            { "hop", "Hop from cushion to cushion. Clear them all, then hop onto the ★." },
        };
        const char Star = '★';

        /// <summary>The back orb (or the phone's back button): the overlay goes, the menu walks back to the house.</summary>
        public event Action Back;
        /// <summary>PLAY on an open game, after the card's bounce (420 ms): the game's id.</summary>
        public event Action<string> Play;
        /// <summary>The creature should play this move ('cheer', 'happy', 'look'...).</summary>
        public event Action<string> CreatureMove;
        /// <summary>A sound the page plays here: 'whoosh' (another card), 'tap' (PLAY).</summary>
        public event Action<string> Sound;
        /// <summary>menu.js allowed(): the moves this creature can do, from a list (null: all of them).</summary>
        public Func<string[], string[]> AllowedMoves;

        public int Current => _cur;
        public bool Leaving => _leaving;

        sealed class Card
        {
            public GameParkGame Game;
            public GameParkLayout.CardSpec Spec;
            public VisualElement Slot, Body, WinHit, Go, GoNormal, GoDim, GoBright;
            public CssTransition SlotMove, SlotFade, BodyMove, BodyFade;
            public readonly List<VisualElement> Bright = new List<VisualElement>();
            public readonly List<Words> Texts = new List<Words>();
            public float CentreSince = float.NegativeInfinity;
            public float LastFlash = -1f;
            public bool Centre, Pressed;
            public GameParkLayout.ClipSpec Clip;
            public VisualElement ClipBox;
            public VisualElement DescBox;
            public string DescText;
        }

        /// <summary>A label of the words Unity draws, with its colours (brightened during sqCen).</summary>
        sealed class Words
        {
            public Label Label;
            public Color Color, ShadowColor;
            public TextShadow Shadow;
            public bool HasShadow;
        }

        sealed class Dot
        {
            public VisualElement Box, Off, On;
            public CssTransition Change;   // [width (page px), how much it's the current one 0..1]
        }

        readonly GameParkLayout _L;
        readonly float _k;                 // the page's scale: UI units per page px
        readonly List<Card> _cards = new List<Card>();
        readonly List<Dot> _dots = new List<Dot>();
        readonly string _creatureKey, _creatureName;
        readonly bool _instant;
        float _carLeft, _carTop, _carW;     // the carousel's box on the A15, dp
        VisualElement _dim, _header, _car, _mid, _dotRow, _arrowL, _arrowR, _rays, _halo, _toastRow;
        Label _measure;
        CssTransition _dimFade, _headerFade, _headerMove, _raysFade, _haloFade;
        int _cur, _first;
        bool _shown, _leaving, _starting, _wrapped, _videoShown;
        float _downX, _downY, _swiped = float.NegativeInfinity;
        bool _down;
        IVisualElementScheduledItem _idle;
        VideoPlayer _video;
        RenderTexture _videoTexture;
        VisualElement _videoView;

        static float Now => Time.realtimeSinceStartup;
        // the transitions' clock: frozen like the reference screenshots, everything has arrived
        static float T => ReferenceOverlay.FreezeAnimations ? float.MaxValue : Time.realtimeSinceStartup;
        static float JsRound(float x) => Mathf.Floor(x + 0.5f);   // Math.round (C#'s rounds halves to even)

        /// <summary>The overlay for these games (Squish Time first, as menu.js games() lists them) and the creature
        /// standing in the park. instant: the entry starts at once (the page's way back from a game), otherwise two
        /// frames after it's shown (the page's requestAnimationFrame x 2). startIndex: the card in the centre
        /// (otherwise the last one chosen, else the first).</summary>
        public GameParkOverlay(IList<GameParkGame> games, string creatureKey, string creatureName, bool instant = false, int startIndex = -1)
        {
            name = "game-park";
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            _creatureKey = creatureKey;
            _creatureName = creatureName ?? "";
            _instant = instant;

            _L = GameParkLayout.Load();
            if (_L == null || _L.page == null || _L.cards == null)
            {
                Add(new Label("Game Park pictures missing: run tools/unity-port/export-game-park.mjs"));
                return;
            }
            _k = _L.page.k;
            _carLeft = _L.car.box[0] / K; _carTop = _L.car.box[1] / K; _carW = _L.car.box[2] / K;

            if (games != null)
                foreach (var g in games)
                {
                    var spec = g != null ? _L.Card(g.Id) : null;
                    if (spec == null) { Debug.LogWarning("[PlushCrush] Game Park: no pictures for game '" + (g != null ? g.Id : "null") + "' (left out)"); continue; }
                    _cards.Add(new Card { Game = g, Spec = spec });
                }

            // the card in the centre: asked for, else the last one chosen (menu.js LASTG), else the first
            _cur = 0;
            if (startIndex >= 0) _cur = Mathf.Clamp(startIndex, 0, Mathf.Max(0, _cards.Count - 1));
            else
            {
                string last = PlayerPrefs.GetString(LastGameKey, "");
                for (int i = 0; i < _cards.Count; i++) if (_cards[i].Game.Id == last) { _cur = i; break; }
            }
            _first = _cur;

            Build();
            Place(Now, true);

            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            RegisterCallback<AttachToPanelEvent>(_ => OnShown());
            RegisterCallback<DetachFromPanelEvent>(_ => StopClip());
            schedule.Execute(Tick).Every(0);
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            float k = _k;
            // the dim: the whole screen, stretched with it (its gradients are in % of it)
            _dim = Picture(_L.dim, 0, 0, "park-dim");
            _dim.style.left = 0; _dim.style.top = 0; _dim.style.right = 0; _dim.style.bottom = 0;
            _dim.style.width = StyleKeyword.Auto; _dim.style.height = StyleKeyword.Auto;
            Add(_dim);
            _dimFade = new CssTransition(0f, 0.5f, 0f, "ease");

            // the carousel: from carL to the right edge, below the header (.sq-car)
            _car = new VisualElement { name = "park-carousel" };
            _car.style.position = Position.Absolute;
            _car.style.left = _carLeft; _car.style.top = _carTop; _car.style.right = 0; _car.style.bottom = 0;
            Add(_car);
            // the A15's carousel box, kept centred in it (the cards, rays, halo and dots are centred in the carousel)
            _mid = new VisualElement { name = "park-centre", pickingMode = PickingMode.Ignore };
            _mid.style.position = Position.Absolute;
            _mid.style.left = Length.Percent(50); _mid.style.marginLeft = -_carW / 2f; _mid.style.width = _carW;
            _mid.style.top = 0; _mid.style.bottom = 0;
            _car.Add(_mid);

            _rays = Picture(_L.rays, _carLeft, _carTop, "park-rays");
            _halo = Picture(_L.halo, _carLeft, _carTop, "park-halo");
            _mid.Add(_rays); _mid.Add(_halo);
            _rays.style.opacity = 0; _halo.style.opacity = 0;
            _raysFade = new CssTransition(0f, 0.6f, 0.2f, "ease");
            _haloFade = new CssTransition(0f, 0.6f, 0.2f, "ease");

            for (int i = 0; i < _cards.Count; i++) BuildCard(_cards[i], i);

            // the dots (z 55, over the cards)
            _dotRow = new VisualElement { name = "park-dots", pickingMode = PickingMode.Ignore };
            _dotRow.style.position = Position.Absolute;
            _dotRow.style.left = 0; _dotRow.style.top = 0; _dotRow.style.width = _carW; _dotRow.style.height = Length.Percent(100);
            _mid.Add(_dotRow);
            for (int i = 0; i < _cards.Count; i++)
            {
                int index = i;
                var d = new Dot();
                d.Box = new VisualElement { name = "park-dot" };
                d.Box.style.position = Position.Absolute;
                d.Off = Sliced(_L.dots.off.image, _L.dots.slice);
                d.On = Sliced(_L.dots.on.image, _L.dots.slice);
                d.Box.Add(d.Off); d.Box.Add(d.On);
                d.Box.RegisterCallback<ClickEvent>(_ => Go(index));
                d.Change = new CssTransition(new[] { _L.dots.wOff, 0f }, 0.3f, 0f, "ease");
                _dotRow.Add(d.Box);
                _dots.Add(d);
            }

            // the arrows (z 60), 10 in from the carousel's left, 12 from the right edge
            _arrowL = Picture(_L.arrows.left, _carLeft, _carTop, "park-arrow-left");
            _arrowR = Picture(_L.arrows.right, _carLeft, _carTop, "park-arrow-right");
            _arrowR.style.left = StyleKeyword.Auto;
            _arrowR.style.right = (_L.car.box[0] + _L.car.box[2] - (_L.arrows.right.px[0] + _L.arrows.right.px[2])) / K;
            _arrowL.pickingMode = PickingMode.Position; _arrowR.pickingMode = PickingMode.Position;
            _arrowL.RegisterCallback<ClickEvent>(_ => Go(_cur - 1));
            _arrowR.RegisterCallback<ClickEvent>(_ => Go(_cur + 1));
            _car.Add(_arrowL); _car.Add(_arrowR);

            // a swipe across the carousel (pointerdown / pointerup anywhere on it)
            _car.RegisterCallback<PointerDownEvent>(e => { _down = true; _downX = e.position.x; _downY = e.position.y; });
            _car.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!_down) return;
                _down = false;
                float dx = (e.position.x - _downX) / _k, dy = (e.position.y - _downY) / _k;
                if (Mathf.Abs(dx) > 36f && Mathf.Abs(dx) > Mathf.Abs(dy)) { _swiped = Now; Go(_cur + (dx < 0 ? 1 : -1)); }
            });
            _car.RegisterCallback<PointerCancelEvent>(_ => _down = false);

            // the header (.mm-pgh: it takes the taps on its strip)
            var hb = _L.header.box;
            _header = new VisualElement { name = "park-header" };
            _header.style.position = Position.Absolute;
            _header.style.left = hb[0] / K; _header.style.top = hb[1] / K; _header.style.width = hb[2] / K; _header.style.height = hb[3] / K;
            var back = Picture(_L.header.back, hb[0] / K, hb[1] / K, "park-back");
            back.pickingMode = PickingMode.Position;
            back.RegisterCallback<ClickEvent>(_ => Leave());
            _header.Add(back);
            _header.Add(Picture(_L.header.title, hb[0] / K, hb[1] / K, "park-title"));
            _header.style.opacity = 0;
            Add(_header);
            _headerFade = new CssTransition(0f, 0.4f, 0f, "ease");
            _headerMove = new CssTransition(-14f * k, 0.5f, 0f, "cubic-bezier(.2,.9,.25,1)");

            // where toasts go: centred, 20 up from the bottom (.sq-toast)
            _toastRow = new VisualElement { name = "park-toasts", pickingMode = PickingMode.Ignore };
            _toastRow.style.position = Position.Absolute;
            _toastRow.style.left = 0; _toastRow.style.right = 0; _toastRow.style.bottom = _L.toast.bottom * k;
            _toastRow.style.flexDirection = FlexDirection.Row;
            _toastRow.style.justifyContent = Justify.Center;
            _toastRow.style.alignItems = Align.FlexEnd;
            Add(_toastRow);

            // (a hidden label in the line's style, to measure words when a line must be broken here)
            _measure = new Label("") { pickingMode = PickingMode.Ignore };
            StyleWords(_measure, _L.text.desc, 0f);
            _measure.style.position = Position.Absolute;
            _measure.style.visibility = Visibility.Hidden;
            Add(_measure);
        }

        void BuildCard(Card c, int i)
        {
            var s = c.Spec;
            var sb = _L.slot.box;
            float sx = sb[0] / K, sy = sb[1] / K;   // the slot's corner on the A15 (dp): the card's pieces are placed from it

            c.Slot = new VisualElement { name = "park-slot-" + c.Game.Id, pickingMode = PickingMode.Ignore };
            c.Slot.style.position = Position.Absolute;
            c.Slot.style.left = sx - _carLeft; c.Slot.style.top = sy - _carTop;
            c.Slot.style.width = sb[2] / K; c.Slot.style.height = sb[3] / K;
            c.Slot.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            _mid.Add(c.Slot);

            c.Body = new VisualElement { name = "park-card", pickingMode = PickingMode.Ignore };
            c.Body.style.position = Position.Absolute;
            c.Body.style.left = 0; c.Body.style.top = 0; c.Body.style.width = sb[2] / K; c.Body.style.height = sb[3] / K;
            c.Body.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50));
            c.Slot.Add(c.Body);

            float delay = Mathf.Round((0.05f + Mathf.Abs(i - _first) * 0.07f) * 100f) / 100f;   // toFixed(2)
            c.SlotMove = new CssTransition(new[] { 0f, 0f, 1f, 1f }, 0.5f, 0f, "cubic-bezier(.3,1.3,.5,1)");
            c.SlotFade = new CssTransition(1f, 0.35f, 0f, "ease");
            c.BodyMove = new CssTransition(Out(), 0.6f, delay, "cubic-bezier(.2,1.2,.4,1)");
            c.BodyFade = new CssTransition(0f, 0.45f, delay, "ease");
            c.Body.style.opacity = 0;

            // 1. the frame (wood, rims, the window's rings; Squish Time's title)
            AddPic(c, c.Body, s.frame.image, s.frame.bright, s.frame.px, sx, sy, "frame");

            // 2. the window's picture
            bool locked = !c.Game.Open && GameParkLayout.Has(s.locked);
            var sq = s.squish;
            bool squish = sq != null && sq.creature != null && !string.IsNullOrEmpty(sq.creature.image);
            bool clip = squish && sq.clip != null && !string.IsNullOrEmpty(sq.clip.image) && sq.clip.creature == _creatureKey;
            if (locked) AddPic(c, c.Body, s.locked.image, s.locked.bright, s.locked.px, sx, sy, "window");
            else if (clip) AddPic(c, c.Body, sq.clip.image, sq.clip.bright, sq.clip.px, sx, sy, "window");
            else AddPic(c, c.Body, s.win.image, s.win.bright, s.win.px, sx, sy, "window");
            if (squish)
            {
                // what's inside the window moves: clipped to it (.win: overflow hidden, rounded)
                var box = WindowBox(s.win, sx, sy);
                c.Body.Add(box);
                if (clip) { c.Clip = sq.clip; c.ClipBox = box; }
                else AddSquishing(c, box, sq, s.win);
            }
            // 3. the lock, 4. the window's shading (over the picture, the lock and the creature)
            if (!c.Game.Open && GameParkLayout.Has(s.@lock)) AddPic(c, c.Body, s.@lock.image, s.@lock.bright, s.@lock.px, sx, sy, "lock");
            if (GameParkLayout.Has(s.shade)) c.Body.Add(Picture(s.shade, sx, sy, "shade"));

            // the window takes taps (menu.js: a tap on .win or the button plays)
            c.WinHit = new VisualElement { name = "park-window-hit" };
            c.WinHit.style.position = Position.Absolute;
            c.WinHit.style.left = s.win.box[0] / K - sx; c.WinHit.style.top = s.win.box[1] / K - sy;
            c.WinHit.style.width = s.win.box[2] / K; c.WinHit.style.height = s.win.box[3] / K;
            int index = i;
            c.WinHit.RegisterCallback<ClickEvent>(_ => Tapped(index));
            c.Body.Add(c.WinHit);

            // 5. the line and the progress chip (words drawn here)
            c.DescText = c.Game.Description ?? DescriptionFor(c.Game.Id);
            if (s.desc != null && s.desc.lines != null && s.desc.lines.Length > 0 && !string.IsNullOrEmpty(c.DescText))
            {
                c.DescBox = new VisualElement { name = "park-desc", pickingMode = PickingMode.Ignore };
                c.DescBox.style.position = Position.Absolute;
                c.DescBox.style.left = 0; c.DescBox.style.top = 0;
                c.Body.Add(c.DescBox);
                if (c.DescText == s.desc.text)
                {
                    // the browser's own line breaks
                    var lines = new List<string>();
                    foreach (var l in s.desc.lines) lines.Add(l.text);
                    FillDesc(c, lines);
                }
                // otherwise broken here once fonts are measured (Relayout)
            }
            AddChip(c, sx, sy);

            // 6. the button: PLAY / LOCKED, its saturate(.7) twin (not the centre card), its brightened twin
            var go = s.go;
            c.Go = new VisualElement { name = "park-go" };
            c.Go.style.position = Position.Absolute;
            c.Go.style.left = go.box[0] / K - sx; c.Go.style.top = go.box[1] / K - sy;
            c.Go.style.width = go.box[2] / K; c.Go.style.height = go.box[3] / K;
            bool lockedGo = !c.Game.Open && GameParkLayout.Has(go.locked);
            var normal = lockedGo ? go.locked : go.play;
            var dim = lockedGo ? go.lockedDim : go.dim;
            var bright = lockedGo ? go.lockedBright : go.bright;
            float gx = go.box[0] / K, gy = go.box[1] / K;
            c.GoNormal = Picture(normal, gx, gy, "go");
            c.GoDim = GameParkLayout.Has(dim) ? Picture(dim, gx, gy, "go-dim") : null;
            c.GoBright = GameParkLayout.Has(bright) ? Picture(bright, gx, gy, "go-bright") : null;
            c.Go.Add(c.GoNormal);
            if (c.GoDim != null) c.Go.Add(c.GoDim);
            if (c.GoBright != null) { c.GoBright.style.opacity = 0; c.Go.Add(c.GoBright); }
            // pressed: .sq-gc .go:active{transform:translateY(2px)} (instant)
            c.Go.RegisterCallback<PointerDownEvent>(_ => c.Go.style.translate = new Translate(0, 2f * _k));
            c.Go.RegisterCallback<PointerUpEvent>(_ => c.Go.style.translate = new Translate(0, 0));
            c.Go.RegisterCallback<PointerLeaveEvent>(_ => c.Go.style.translate = new Translate(0, 0));
            c.Go.RegisterCallback<PointerCancelEvent>(_ => c.Go.style.translate = new Translate(0, 0));
            c.Go.RegisterCallback<ClickEvent>(_ => Tapped(index));
            c.Body.Add(c.Go);
        }

        static float[] Rest() => new[] { 0f, 0f, 1f, 1f };
        float[] Out() => new[] { 160f * _k, 0f, 0.8f, 0.8f };   // translateX(160px) scale(.8)

        string DescriptionFor(string id)
        {
            if (id == "squish") return "Squeeze, poke and stretch " + (_creatureName.Length > 0 ? _creatureName : "your character") + ". Every squish pays coins!";
            string d;
            return Desc.TryGetValue(id, out d) ? d : "";
        }

        // the window's box, clipping what's in it to its rounded corners
        VisualElement WindowBox(GameParkLayout.WinSpec w, float sx, float sy)
        {
            var box = new VisualElement { name = "park-window", pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = w.box[0] / K - sx; box.style.top = w.box[1] / K - sy;
            box.style.width = w.box[2] / K; box.style.height = w.box[3] / K;
            float r = w.radius / K;
            box.style.borderTopLeftRadius = r; box.style.borderTopRightRadius = r;
            box.style.borderBottomLeftRadius = r; box.style.borderBottomRightRadius = r;
            box.style.overflow = Overflow.Hidden;
            return box;
        }

        // Squish Time: the lines (sqLines) and the creature squishing (sqSqz) from the bottom centre of its <img> box.
        // CSS writes scale() then rotate(): the scale is the outer box, the turn the inner one, so the turn happens
        // first (UI Toolkit's own order would turn after scaling).
        void AddSquishing(Card c, VisualElement box, GameParkLayout.SquishSpec sq, GameParkLayout.WinSpec w)
        {
            float wx = w.box[0] / K, wy = w.box[1] / K;
            var lines = new CssAnimation { Duration = 1.9f, Iterations = float.PositiveInfinity, Timing = Easing.EaseOut };
            lines.Key(new CssKeyframe(0f).Fade(0f)).Key(new CssKeyframe(0.12f).Fade(0f)).Key(new CssKeyframe(0.2f).Fade(1f))
                 .Key(new CssKeyframe(0.46f).Fade(0f)).Key(new CssKeyframe(1f).Fade(0f));
            foreach (var l in new[] { sq.lineL, sq.lineR })
            {
                if (!GameParkLayout.Has(l)) continue;
                var e = Picture(l, wx, wy, "squish-line");
                box.Add(e);
                new CssAnimator(e).Base(new CssPose { ScaleX = 1f, ScaleY = 1f, Opacity = 0f }).Play(lines);
            }

            // the creature's picture: the default one is in the project; the others are generated (creatures.json)
            Texture2D tex = null, texBright = null;
            int[] px = sq.creature.px;
            if (!string.IsNullOrEmpty(_creatureKey) && _creatureKey != sq.creature.key)
            {
                var item = GameParkLayout.FindCreature(_creatureKey);
                if (item != null && item.px != null && item.px.Length == 4)
                {
                    tex = MenuLayout.Optional("ParkCreatures/sqz-" + _creatureKey);
                    texBright = MenuLayout.Optional("ParkCreatures/sqz-" + _creatureKey + "-bright");
                    if (tex != null) px = item.px;
                }
            }
            if (tex == null) { tex = GameParkLayout.Picture(sq.creature.image); texBright = GameParkLayout.Picture(sq.creature.bright); px = sq.creature.px; }

            var ib = sq.imgBox;
            float ix = ib[0] / K, iy = ib[1] / K;
            var outer = new VisualElement { name = "squish-creature", pickingMode = PickingMode.Ignore };
            outer.style.position = Position.Absolute;
            outer.style.left = ix - wx; outer.style.top = iy - wy; outer.style.width = ib[2] / K; outer.style.height = ib[3] / K;
            var inner = new VisualElement { name = "squish-turn", pickingMode = PickingMode.Ignore };
            inner.style.position = Position.Absolute;
            inner.style.left = 0; inner.style.top = 0; inner.style.width = ib[2] / K; inner.style.height = ib[3] / K;
            outer.Add(inner);
            box.Add(outer);
            if (tex != null)
            {
                inner.Add(Picture(tex, px, ix, iy, "squish-still"));
                if (texBright != null) { var b = Picture(texBright, px, ix, iy, "squish-still-bright"); b.style.opacity = 0; inner.Add(b); c.Bright.Add(b); }
            }
            // sqSqz{0%,100%{scale(1,1)} 18%{scale(1.22,.72)} 30%{scale(.9,1.14)} 42%{scale(1.05,.95)} 54%{scale(1,1)}
            //       70%{scale(1.12,.84) rotate(-4deg)} 82%{scale(.95,1.06) rotate(3deg)}} 1.9s ease-in-out infinite
            float[] at = { 0f, 0.18f, 0.30f, 0.42f, 0.54f, 0.70f, 0.82f, 1f };
            float[] sx = { 1f, 1.22f, 0.9f, 1.05f, 1f, 1.12f, 0.95f, 1f };
            float[] sy = { 1f, 0.72f, 1.14f, 0.95f, 1f, 0.84f, 1.06f, 1f };
            float[] rot = { 0f, 0f, 0f, 0f, 0f, -4f, 3f, 0f };
            var scale = new CssAnimation { Duration = 1.9f, Iterations = float.PositiveInfinity, Timing = Easing.EaseInOut };
            var turn = new CssAnimation { Duration = 1.9f, Iterations = float.PositiveInfinity, Timing = Easing.EaseInOut };
            for (int j = 0; j < at.Length; j++)
            {
                scale.Key(new CssKeyframe(at[j]).Scale(sx[j], sy[j]));
                turn.Key(new CssKeyframe(at[j]).Rot(rot[j]));
            }
            new CssAnimator(outer, "50% 100%").Play(scale);
            new CssAnimator(inner, "50% 100%").Play(turn);
        }

        void AddChip(Card c, float sx, float sy)
        {
            var b = c.Spec.best;
            if (b == null || b.box == null || b.box.Length != 4 || _L.chip == null || string.IsNullOrEmpty(_L.chip.image)) return;
            string text = c.Game.Id == "squish" ? "Playing as " + _creatureName : (c.Game.Best ?? "New!");
            var chip = new VisualElement { name = "park-chip", pickingMode = PickingMode.Ignore };
            chip.style.position = Position.Absolute;
            chip.style.left = b.box[0] / K - sx; chip.style.top = b.box[1] / K - sy;
            chip.style.height = b.box[3] / K;
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.FlexStart;
            chip.style.paddingLeft = (b.textBox[0] - b.box[0]) / K;
            chip.style.paddingRight = (b.box[0] + b.box[2] - b.textBox[0] - b.textBox[2]) / K;
            chip.style.paddingTop = (b.textBox[1] - b.box[1]) / K;
            // its pill (behind the words), stretched between its ends, and the pill brightened
            var cb = _L.chip.box;
            foreach (var img in new[] { _L.chip.image, _L.chip.bright })
            {
                if (string.IsNullOrEmpty(img)) continue;
                var bg = Sliced(img, _L.chip.slice);
                bg.style.left = (_L.chip.px[0] - cb[0]) / K; bg.style.top = (_L.chip.px[1] - cb[1]) / K;
                bg.style.right = (cb[0] + cb[2] - _L.chip.px[0] - _L.chip.px[2]) / K;
                bg.style.bottom = (cb[1] + cb[3] - _L.chip.px[1] - _L.chip.px[3]) / K;
                if (img == _L.chip.bright) { bg.style.opacity = 0; c.Bright.Add(bg); }
                chip.Add(bg);
            }
            var row = WordsRow(c, text, _L.text.best, _L.stars != null ? _L.stars.best : null, _L.stars != null ? _L.stars.bestSpace : 0f, b.textBox[3] / K);
            row.style.position = Position.Relative;
            chip.Add(row);
            c.Body.Add(chip);
        }

        // The line, in rows (each a line of the browser's, or of the same width broken here), at the line's place
        void FillDesc(Card c, List<string> lines)
        {
            var d = c.Spec.desc;
            float sx = _L.slot.box[0] / K, sy = _L.slot.box[1] / K;
            var first = d.lines[0].box;
            float lineH = _L.text.desc.lineHeight * _k;
            c.DescBox.Clear();
            for (int i = 0; i < lines.Count && i < 2; i++)
            {
                float[] box = i < d.lines.Length && lines[i] == d.lines[i].text ? d.lines[i].box : null;
                float x = (box != null ? box[0] : first[0]) / K - sx;
                float y = box != null ? box[1] / K - sy : first[1] / K - sy + i * lineH;
                float h = (box != null ? box[3] : first[3]) / K;
                var row = WordsRow(c, lines[i].TrimEnd(), _L.text.desc, _L.stars != null ? _L.stars.desc : null, _L.stars != null ? _L.stars.descSpace : 0f, h);
                row.style.position = Position.Absolute;
                row.style.left = x; row.style.top = y;
                c.DescBox.Add(row);
            }
        }

        // Words in a row: labels, with the ★ (which the fonts don't have) as the browser's own picture of it, and the
        // spaces next to it as wide as the browser's.
        VisualElement WordsRow(Card c, string text, GameParkLayout.TextStyle st, GameParkLayout.Pic star, float space, float height)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.height = height;
            var parts = text.Split(Star);
            bool hasStar = GameParkLayout.Has(star) && star.box != null && star.box.Length == 4;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (parts.Length > 1)
                {
                    int lead = p.Length - p.TrimStart(' ').Length, trail = p.Length - p.TrimEnd(' ').Length;
                    string core = p.Trim(' ');
                    if (i > 0 && lead > 0) row.Add(Spacer(lead * space / K));
                    if (core.Length > 0) row.Add(WordsLabel(c, core, st, height));
                    if (i < parts.Length - 1 && trail > 0 && core.Length > 0) row.Add(Spacer(trail * space / K));
                    else if (i < parts.Length - 1 && core.Length == 0 && trail > 0 && i == 0) row.Add(Spacer(trail * space / K));
                }
                else if (p.Length > 0) row.Add(WordsLabel(c, p, st, height));
                if (i < parts.Length - 1)
                {
                    if (hasStar) row.Add(StarBox(c, star));
                    else row.Add(WordsLabel(c, Star.ToString(), st, height));   // (no picture: the font's fallback)
                }
            }
            return row;
        }

        static VisualElement Spacer(float width)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.width = width; e.style.flexShrink = 0;
            return e;
        }

        VisualElement StarBox(Card c, GameParkLayout.Pic star)
        {
            var box = new VisualElement { name = "star", pickingMode = PickingMode.Ignore };
            box.style.width = star.box[2] / K; box.style.height = star.box[3] / K; box.style.flexShrink = 0;
            float ox = star.box[0] / K, oy = star.box[1] / K;
            box.Add(Picture(star, ox, oy, "star-picture"));
            if (!string.IsNullOrEmpty(star.bright))
            {
                var b = Picture(star.bright, star.px, ox, oy, "star-bright");
                b.style.opacity = 0;
                box.Add(b);
                c.Bright.Add(b);
            }
            return box;
        }

        Label WordsLabel(Card c, string text, GameParkLayout.TextStyle st, float height)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            var w = new Words { Label = l };
            StyleWords(l, st, height, w);
            c.Texts.Add(w);
            return l;
        }

        // the page's font, size, colour, letter spacing and shadow, at the page's scale
        void StyleWords(Label l, GameParkLayout.TextStyle st, float height, Words w = null)
        {
            l.Use(Fonts.For(st.font, st.weight));
            l.style.fontSize = st.size * _k;
            l.style.letterSpacing = st.letterSpacing * _k;
            var color = Css.ToColor(st.color, Color.white);
            l.style.color = color;
            l.style.unityTextAlign = TextAnchor.MiddleLeft;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.flexShrink = 0;
            if (height > 0f) l.style.height = height;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            if (w != null) w.Color = color;
            if (st.shadows != null && st.shadows.Length > 0)
            {
                var s = st.shadows[0];
                var ts = new TextShadow { offset = new Vector2(s.x * _k, s.y * _k), blurRadius = s.blur * _k, color = Css.ToColor(s.color) };
                l.style.textShadow = new StyleTextShadow(ts);
                if (w != null) { w.Shadow = ts; w.ShadowColor = ts.color; w.HasShadow = true; }
            }
        }

        // ------------------------------------------------------------------ pictures

        VisualElement Picture(GameParkLayout.Pic p, float originX, float originY, string elementName)
        {
            if (!GameParkLayout.Has(p)) return Empty(elementName);
            return Picture(p.image, p.px, originX, originY, elementName);
        }

        VisualElement Picture(string image, int[] px, float originX, float originY, string elementName)
        {
            return Picture(GameParkLayout.Picture(image), px, originX, originY, elementName);
        }

        // A picture at its A15 pixels (px), placed from an origin (dp): whole screen pixels on the A15, never resampled.
        static VisualElement Picture(Texture2D tex, int[] px, float originX, float originY, string elementName)
        {
            var e = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            if (px == null || px.Length != 4) return e;
            e.style.left = px[0] / K - originX; e.style.top = px[1] / K - originY;
            e.style.width = px[2] / K; e.style.height = px[3] / K;
            if (tex != null)
            {
                e.style.backgroundImage = new StyleBackground(tex);
                e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }
            return e;
        }

        static VisualElement Empty(string elementName)
        {
            var e = new VisualElement { name = elementName, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            return e;
        }

        // a picture and its brightened twin (sqCen), the twin see-through until the card becomes the centre one
        void AddPic(Card c, VisualElement parent, string image, string bright, int[] px, float sx, float sy, string elementName)
        {
            parent.Add(Picture(image, px, sx, sy, elementName));
            if (string.IsNullOrEmpty(bright)) return;
            var b = Picture(bright, px, sx, sy, elementName + "-bright");
            b.style.opacity = 0;
            parent.Add(b);
            c.Bright.Add(b);
        }

        // A pill stretched sideways: its ends (slice px each, the picture's own) stay whole, its middle stretches.
        static VisualElement Sliced(string image, int slice)
        {
            var e = new VisualElement { name = image, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
            e.style.backgroundImage = new StyleBackground(GameParkLayout.Picture(image));
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            e.style.unitySliceLeft = slice; e.style.unitySliceRight = slice;
            e.style.unitySliceTop = 0; e.style.unitySliceBottom = 0;
            e.style.unitySliceScale = 1f / K;   // the picture's px are the A15's, 2.8125 to a UI unit
            return e;
        }

        // ------------------------------------------------------------------ showing, leaving

        void OnShown()
        {
            if (_shown || _L == null) return;
            _shown = true;
            float now = Now;
            if (_cur >= 0 && _cur < _cards.Count) _cards[_cur].CentreSince = now;   // sqCen runs from the start
            // (menu.js: requestAnimationFrame twice, or a 120 ms timer: whichever comes first; at once when instant)
            if (_instant) SetIn(true, now);
            else schedule.Execute(() => { if (!_leaving) SetIn(true, Now); }).StartingIn(33);
            // the creature stays lively while you choose (setInterval 6500)
            _idle = schedule.Execute(Idle).StartingIn(6500).Every(6500);
            StartClipIfCentre();
        }

        void SetIn(bool on, float now)
        {
            _dimFade.Set(on ? 1f : 0f, now);
            _headerFade.Set(on ? 1f : 0f, now);
            _headerMove.Set(on ? 0f : -14f * _k, now);
            _raysFade.Set(on ? 1f : 0f, now);
            _haloFade.Set(on ? 1f : 0f, now);
            foreach (var c in _cards)
            {
                if (!c.Pressed) c.BodyMove.Set(on ? Rest() : Out(), now);   // (playAt's inline transform wins)
                c.BodyFade.Set(on ? 1f : 0f, now);
            }
        }

        /// <summary>Leave the park (the back orb): the overlay plays its way out and goes, Back is raised.</summary>
        public void Leave()
        {
            if (_leaving) return;
            Hide();
            Back?.Invoke();
        }

        /// <summary>The phone's back button: leaves the park. Always handled while the overlay is up.</summary>
        public bool HandleBack()
        {
            if (!_leaving) Leave();
            return true;
        }

        /// <summary>The way out (menu.js leavePark: 'in' taken off, the overlay removed 420 ms later).</summary>
        public void Hide(Action done = null)
        {
            if (_leaving) return;
            _leaving = true;
            if (_idle != null) _idle.Pause();
            StopClip();
            SetIn(false, Now);
            schedule.Execute(() => { RemoveFromHierarchy(); if (done != null) done(); }).StartingIn(420);
        }

        void Idle()
        {
            if (_leaving) return;
            var L = Allowed(IdleMoves);
            if (L.Length > 0 && CreatureMove != null) CreatureMove(L[UnityEngine.Random.Range(0, L.Length)]);
        }

        string[] Allowed(string[] list)
        {
            if (AllowedMoves == null) return list;
            return AllowedMoves(list) ?? new string[0];
        }

        // ------------------------------------------------------------------ choosing a game

        /// <summary>Another card to the centre (menu.js go: clamped, a whoosh, remembered).</summary>
        public void Go(int i)
        {
            if (_cards.Count == 0) return;
            i = Mathf.Clamp(i, 0, _cards.Count - 1);
            if (i == _cur) return;
            _cur = i;
            if (Sound != null) Sound("whoosh");
            Place(Now, false);
            Remember(_cards[i].Game.Id);
        }

        static void Remember(string id)
        {
            PlayerPrefs.SetString(LastGameKey, id);
        }

        void Place(float now, bool first)
        {
            float k = _k, CW = _L.page.CW;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                int d = i - _cur, a = Mathf.Abs(d);
                float tx = d == 0 ? 0f : Mathf.Sign(d) * JsRound(CW * 0.45f + a * 40f), sc = d == 0 ? 1f : 0.8f;
                var move = new[] { tx * k, 0f, sc, sc };
                float fade = d == 0 ? 1f : 0f;
                if (first) { c.SlotMove.Jump(move); c.SlotFade.Jump(fade); }
                else { c.SlotMove.Set(move, now); c.SlotFade.Set(fade, now); }
                bool centre = d == 0;
                if (centre && !c.Centre && !first) c.CentreSince = now;   // .sq-slot.c .sq-gc{animation:sqCen .5s ease}
                if (!centre) c.CentreSince = float.NegativeInfinity;
                c.Centre = centre;
                var pick = centre ? PickingMode.Position : PickingMode.Ignore;   // the others: pointer-events none
                c.WinHit.pickingMode = pick;
                c.Go.pickingMode = pick;
                if (!centre) c.Go.style.translate = new Translate(0, 0);
                // .sq-slot:not(.c) .go{filter:saturate(.7)}
                c.GoNormal.style.display = centre || c.GoDim == null ? DisplayStyle.Flex : DisplayStyle.None;
                if (c.GoDim != null) c.GoDim.style.display = centre ? DisplayStyle.None : DisplayStyle.Flex;
            }
            // z-index 50 - |d|: the nearer, the higher (the same: in the page's order)
            var order = new List<int>();
            for (int i = 0; i < _cards.Count; i++) order.Add(i);
            order.Sort((x, y) =>
            {
                int zx = 50 - Mathf.Abs(x - _cur), zy = 50 - Mathf.Abs(y - _cur);
                return zx != zy ? zx.CompareTo(zy) : x.CompareTo(y);
            });
            foreach (int i in order) _cards[i].Slot.BringToFront();
            _dotRow.BringToFront();
            // the arrows at either end, the current dot
            _arrowL.style.opacity = _cur == 0 ? _L.arrows.offOpacity : 1f;
            _arrowR.style.opacity = _cur == _cards.Count - 1 ? _L.arrows.offOpacity : 1f;
            for (int i = 0; i < _dots.Count; i++)
            {
                var v = i == _cur ? new[] { _L.dots.wOn, 1f } : new[] { _L.dots.wOff, 0f };
                if (first) _dots[i].Change.Jump(v); else _dots[i].Change.Set(v, now);
            }
            if (!first) StartClipIfCentre();
        }

        void Tapped(int i)
        {
            if (Now - _swiped < 0.25f) return;   // (the click that ends a swipe)
            if (i != _cur) { Go(i); return; }
            PlayAt(i);
        }

        void PlayAt(int i)
        {
            if (_leaving || _starting) return;
            var c = _cards[i];
            if (!c.Game.Open) { ShowToast(c.Game.Name + " isn't open yet"); return; }
            _starting = true;
            if (Sound != null) Sound("tap");
            var L = Allowed(PlayMoves);
            if (L.Length > 0 && CreatureMove != null) CreatureMove(L[0]);
            // gc.style.transition = 'transform .18s ease'; transform scale(.94), then scale(1.04) at 180 ms
            float now = Now;
            c.Pressed = true;
            c.BodyMove.Duration = 0.18f; c.BodyMove.Delay = 0f; c.BodyMove.Timing = Easing.Ease;
            c.BodyFade.Duration = 0f; c.BodyFade.Delay = 0f;
            c.BodyMove.Set(new[] { 0f, 0f, 0.94f, 0.94f }, now);
            schedule.Execute(() => c.BodyMove.Set(new[] { 0f, 0f, 1.04f, 1.04f }, Now)).StartingIn(180);
            Remember(c.Game.Id);
            string id = c.Game.Id;
            schedule.Execute(() => { _starting = false; if (Play != null) Play(id); }).StartingIn(420);
        }

        // ------------------------------------------------------------------ the toast

        /// <summary>The page's toast (.sq-toast, a gold plaque low in the middle): mmToast 2.4s ease, then gone.</summary>
        public void ShowToast(string text)
        {
            if (_L == null || _L.toast == null || _L.text == null || _L.text.toast == null) return;
            _toastRow.Clear();
            var tt = _L.toast;
            var plaque = new VisualElement { name = "park-toast", pickingMode = PickingMode.Ignore };
            plaque.style.flexDirection = FlexDirection.Row;
            plaque.style.paddingLeft = (tt.textBox[0] - tt.box[0]) / K;
            plaque.style.paddingRight = (tt.box[0] + tt.box[2] - tt.textBox[0] - tt.textBox[2]) / K;
            plaque.style.paddingTop = (tt.textBox[1] - tt.box[1]) / K;
            plaque.style.paddingBottom = (tt.box[1] + tt.box[3] - tt.textBox[1] - tt.textBox[3]) / K;
            plaque.style.maxWidth = Length.Percent(tt.maxWidth * 100f);
            var bg = Sliced(tt.image, tt.slice);
            bg.style.left = (tt.px[0] - tt.box[0]) / K; bg.style.top = (tt.px[1] - tt.box[1]) / K;
            bg.style.right = (tt.box[0] + tt.box[2] - tt.px[0] - tt.px[2]) / K;
            bg.style.bottom = (tt.box[1] + tt.box[3] - tt.px[1] - tt.px[3]) / K;
            plaque.Add(bg);
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            StyleWords(label, _L.text.toast, tt.textBox[3] / K);
            label.style.position = Position.Relative;
            plaque.Add(label);
            // mmToast{0%{opacity:0;translate(-50%,-8px)} 10%,85%{opacity:1;translate(-50%,0)} 100%{opacity:0}}
            var a = new CssAnimation { Duration = 2.4f, Timing = Easing.Ease, Fill = AnimFill.Forwards };
            a.Key(new CssKeyframe(0f).Move(0f, -8f * _k).Fade(0f))
             .Key(new CssKeyframe(0.1f).Move(0f, 0f).Fade(1f))
             .Key(new CssKeyframe(0.85f).Move(0f, 0f).Fade(1f))
             .Key(new CssKeyframe(1f).Fade(0f));
            new CssAnimator(plaque, (string)null, true, false).Play(a);   // (before it's shown: it starts when shown)
            _toastRow.Add(plaque);
            plaque.schedule.Execute(() => plaque.RemoveFromHierarchy()).StartingIn(2400);
        }

        // ------------------------------------------------------------------ every frame

        void Tick()
        {
            if (_L == null) return;
            float t = T;
            _dim.style.opacity = _dimFade.Value1(t);
            _header.style.opacity = _headerFade.Value1(t);
            _header.style.translate = new Translate(0, _headerMove.Value1(t));
            _rays.style.opacity = _raysFade.Value1(t);
            _halo.style.opacity = _haloFade.Value1(t);

            foreach (var c in _cards)
            {
                Pose(c.Slot, c.SlotMove.Value(t), c.SlotFade.Value1(t));
                Pose(c.Body, c.BodyMove.Value(t), c.BodyFade.Value1(t));
                // sqCen{0%{filter:brightness(1.35)} 100%{filter:none}} .5s ease: the brightened twins fade out
                float flash = 0f;
                if (c.Centre && !ReferenceOverlay.FreezeAnimations)
                {
                    float p = (t - c.CentreSince) / 0.5f;
                    if (p < 1f) flash = 1f - Easing.Ease(Mathf.Clamp01(p));
                }
                if (Mathf.Abs(flash - c.LastFlash) > 1e-4f)
                {
                    c.LastFlash = flash;
                    foreach (var b in c.Bright) b.style.opacity = flash;
                    if (c.GoBright != null) c.GoBright.style.opacity = c.Centre ? flash : 0f;
                    float k = 1f + 0.35f * flash;
                    foreach (var w in c.Texts)
                    {
                        w.Label.style.color = Brighter(w.Color, k);
                        if (w.HasShadow)
                        {
                            var s = w.Shadow;
                            s.color = Brighter(w.ShadowColor, k);
                            w.Label.style.textShadow = new StyleTextShadow(s);
                        }
                    }
                }
            }
            PlaceDots(t);
            if (_videoView != null && _video != null && !_videoShown && _video.frame > 0)
            {
                _videoShown = true;
                _videoView.style.display = DisplayStyle.Flex;
            }
        }

        static Color Brighter(Color c, float k) => new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);

        static void Pose(VisualElement e, float[] m, float opacity)
        {
            e.style.translate = new Translate(m[0], m[1]);
            e.style.scale = new Scale(new Vector2(m[2], m[3]));
            e.style.opacity = opacity;
        }

        // .sq-pos: the row centred in the carousel, 6 apart; each dot its width (7, the current one 18), its colour
        // between white .35 and gold (the two pictures crossfaded)
        void PlaceDots(float t)
        {
            var D = _L.dots;
            float k = _k, rowW = 0f;
            var vals = new float[_dots.Count][];
            for (int i = 0; i < _dots.Count; i++) { vals[i] = _dots[i].Change.Value(t); rowW += vals[i][0] * k; }
            rowW += D.gap * k * Mathf.Max(0, _dots.Count - 1);
            float centre = _carW / 2f, x = centre - rowW / 2f, top = D.row[1] / K - _carTop;
            for (int i = 0; i < _dots.Count; i++)
            {
                var d = _dots[i];
                float w = vals[i][0], m = Mathf.Clamp01(vals[i][1]);
                var pic = D.on;
                // on whole A15 pixels, as the pictures were taken
                float left = Mathf.Round((_carLeft + x) * K + pic.offset[0]) / K - _carLeft;
                d.Box.style.left = left;
                d.Box.style.top = top + pic.offset[1] / K;
                d.Box.style.width = pic.px[2] / K - (D.wOn - w) * k;
                d.Box.style.height = pic.px[3] / K;
                d.On.style.opacity = m;
                d.Off.style.opacity = 1f - m;
                x += w * k + D.gap * k;
            }
        }

        // The carousel's left edge from the screen's shape, as the page works it out: carL = round(max(200, pgW * .29))
        // in the page's px (pgW: the screen's width over k). On the A15: 247. Its words, if they must be broken here.
        void Relayout()
        {
            if (_L == null) return;
            float W = layout.width, H = layout.height;
            if (float.IsNaN(W) || W <= 0f || H <= 0f) return;
            float k = H / _L.page.designHeight, pgW = W / k;
            float carL = JsRound(Mathf.Max(200f, pgW * 0.29f));
            _car.style.left = carL * k;
            if (!_wrapped)
            {
                _wrapped = true;
                foreach (var c in _cards)
                    if (c.DescBox != null && c.DescBox.childCount == 0) FillDesc(c, Wrap(c.DescText, c.Spec.desc.maxWidth / K));
            }
        }

        // Greedy line breaking (as the browser does), two lines at most, the second ending in … when cut
        // (-webkit-line-clamp: 2)
        List<string> Wrap(string text, float maxWidth)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            string line = "";
            foreach (var w in words)
            {
                string cand = line.Length == 0 ? w : line + " " + w;
                if (line.Length == 0 || Width(cand) <= maxWidth) line = cand;
                else { lines.Add(line); line = w; }
            }
            if (line.Length > 0) lines.Add(line);
            if (lines.Count > 2)
            {
                string rest = string.Join(" ", lines.GetRange(1, lines.Count - 1).ToArray());
                while (rest.Length > 0 && Width(rest + "…") > maxWidth)
                {
                    int sp = rest.LastIndexOf(' ');
                    rest = sp > 0 ? rest.Substring(0, sp) : rest.Substring(0, rest.Length - 1);
                }
                lines = new List<string> { lines[0], rest + "…" };
            }
            return lines;
        }

        float Width(string s)
        {
            float w = 0f;
            var parts = s.Split(Star);
            var star = _L.stars != null ? _L.stars.desc : null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    w += _measure.MeasureTextSize(parts[i], 0f, MeasureMode.Undefined, 0f, MeasureMode.Undefined).x;
                if (i < parts.Length - 1 && star != null && star.box != null && star.box.Length == 4) w += star.box[2] / K;
            }
            return w;
        }

        // ------------------------------------------------------------------ Frybo's clip

        void StartClipIfCentre()
        {
            if (_cur < 0 || _cur >= _cards.Count) return;
            var c = _cards[_cur];
            if (c.Clip == null || _leaving || panel == null) { StopClip(); return; }
            if (_video != null) return;
            var clip = Resources.Load<VideoClip>(GameParkLayout.Folder + c.Clip.video);
            if (clip == null) return;   // the first frame stays (win-squish-clip)
            int vw = c.Clip.size != null && c.Clip.size.Length == 2 ? c.Clip.size[0] : (int)clip.width;
            int vh = c.Clip.size != null && c.Clip.size.Length == 2 ? c.Clip.size[1] : (int)clip.height;
            _videoTexture = new RenderTexture(vw, vh, 0);
            _videoTexture.Create();
            var go = new GameObject("Game Park clip");
            _video = go.AddComponent<VideoPlayer>();
            _video.playOnAwake = false;
            _video.isLooping = true;
            _video.audioOutputMode = VideoAudioOutputMode.None;
            _video.renderMode = VideoRenderMode.RenderTexture;
            _video.targetTexture = _videoTexture;
            _video.clip = clip;
            _video.Play();
            // object-fit: cover, centred, in the window's box (it clips)
            var box = c.ClipBox;
            float bw = c.Spec.win.box[2] / K, bh = c.Spec.win.box[3] / K, aspect = (float)vw / vh;
            float w = bw, h = bw / aspect;
            if (h < bh) { h = bh; w = bh * aspect; }
            _videoView = new VisualElement { name = "squish-clip", pickingMode = PickingMode.Ignore };
            _videoView.style.position = Position.Absolute;
            _videoView.style.left = (bw - w) / 2f; _videoView.style.top = (bh - h) / 2f;
            _videoView.style.width = w; _videoView.style.height = h;
            _videoView.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_videoTexture));
            _videoView.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _videoView.style.display = DisplayStyle.None;   // until its first frame is drawn
            _videoShown = false;
            box.Add(_videoView);
        }

        void StopClip()
        {
            if (_videoView != null) { _videoView.RemoveFromHierarchy(); _videoView = null; }
            if (_video != null) { _video.Stop(); UnityEngine.Object.Destroy(_video.gameObject); _video = null; }
            if (_videoTexture != null) { _videoTexture.Release(); UnityEngine.Object.Destroy(_videoTexture); _videoTexture = null; }
        }
    }
}
