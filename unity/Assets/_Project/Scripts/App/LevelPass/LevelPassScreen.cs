// The Main Menu's Level Pass (webgame/app/menu.js M.levels), put back together from the pieces the game itself drew
// (tools/unity-port/export-level-pass.mjs), for any player's state:
//   - the header: back, "Level Pass", CLAIM ALL (n) when more than one prize waits, LV n with its pink bar and
//     "into / need XP", the coins;
//   - the track: every level (the road's length, at least 30) on a rail that fills up to "you"; it scrolls sideways
//     by dragging, gliding on after a flick (menu.js dragRow); a tap picks a level, a second tap on one waiting to be
//     claimed claims it; levels waiting bob (sqClaim);
//   - the panel: the picked level's lettering, its line, its action (CLAIM, Claiming…, CLAIMED / UNLOCKED,
//     "N XP to go"), its reward cards;
//   - claiming: the caller's handler (the backend, later) answers; without one the level is marked claimed at once,
//     as the page does outside the app. Coins and gems fly to the coin pill (9 icons).
//   - opening: the header at once, the track and the panel's content fading in over 0.18 s (no other open animation:
//     .sq-pass{animation:none}).
// Positions are the page's design px × k, and every picture lands on the A15's whole pixel where the page draws it.
using System;
using System.Collections.Generic;
using PlushCrush.App.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlushCrush.App.LevelPass
{
    public sealed class LevelPassScreen : AppScreen
    {
        const float K = Pictures.K;
        const int Pending = 1, Done = 2;          // menu.js pend[L]: 1 while the app answers, 'done' once claimed
        const float DragThreshold = 6f;           // dragRow: dp before a press becomes a drag
        const float WheelStep = 33f;              // design px per wheel step (a browser's 100 px notch is ~3 Unity steps)

        readonly LevelPassClaimHandler _onClaim;
        readonly LevelPassState _state;
        readonly Dictionary<int, int> _pend = new Dictionary<int, int>();
        LevelPassLayout _L;
        float _k = 384f / 393f;
        List<LevelRow> _rows;
        int _sel;

        VisualElement _root, _header, _right, _track, _inner, _panel, _panelContent, _fx, _coinIcon;
        Rect _firstArt;            // the first reward card's art box (root dp), where a burst starts
        bool _hasFirstArt;
        Vector2 _coinCenter;       // the coin pill's coin (root dp), where a burst ends (kept from its last layout)
        bool _hasCoin;
        float _w;                  // the screen's width, dp
        float _scroll;             // the track's scroll, design px (as scrollLeft)
        float _fill;               // the track and panel's opacity while they fade in
        bool _laidOut;

        // drag (menu.js dragRow)
        int _pointer = -1;
        float _x0, _s0, _lx, _lt, _v;
        bool _moved;
        IVisualElementScheduledItem _glide;

        public LevelPassScreen(LevelPassState state, LevelPassClaimHandler onClaim = null)
        {
            _state = state ?? new LevelPassState();
            if (_state.xp == null) _state.xp = new LevelXp();
            if (_state.road == null) _state.road = new List<RoadPrize>();
            _onClaim = onClaim;
        }

        float D(float designPx) => designPx * _k;
        static float NowMs => Time.realtimeSinceStartup * 1000f;
        float Cx(int level) => _L.track.x0 + (level - 1) * _L.track.col;   // a level's centre on the track (cxOf)
        LevelRow Row(int level) => level >= 1 && level <= _rows.Count ? _rows[level - 1] : null;

        protected override VisualElement Build()
        {
            _root = new VisualElement { name = "level-pass" };
            _root.style.overflow = Overflow.Hidden;
            _root.style.backgroundColor = (Color)new Color32(0xb4, 0x7a, 0x42, 0xFF); // the wood's own colour, under its picture
            _L = LevelPassLayout.Load();
            if (_L == null)
            {
                _root.Add(new Label("Level Pass pictures missing: run tools/unity-port/export-level-pass.mjs"));
                return _root;
            }
            _k = _L.page.k;
            _rows = LevelPassRules.Rows(_state);
            _sel = _state.select > 0 ? Mathf.Clamp(_state.select, 1, _rows.Count) : FirstSelection();

            var bg = Pictures.Abs("background", 0, 0, 0, 0);
            bg.style.width = Length.Percent(100); bg.style.height = Length.Percent(100);
            bg.style.backgroundImage = new StyleBackground(Pictures.Tex(_L.background.image));
            bg.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            _root.Add(bg);

            BuildHeader();

            var T = _L.track;
            _track = Pictures.Abs("track", 0, D(T.top), 0, D(T.height));
            _track.style.width = Length.Percent(100);
            _track.style.overflow = Overflow.Hidden;
            _track.pickingMode = PickingMode.Position;
            _track.style.opacity = 0f;
            _track.RegisterCallback<PointerDownEvent>(OnDown);
            _track.RegisterCallback<PointerMoveEvent>(OnMove);
            _track.RegisterCallback<PointerUpEvent>(e => OnUp(e.pointerId));
            _track.RegisterCallback<PointerCancelEvent>(e => OnUp(e.pointerId));
            // a tap after a drag isn't a tap (dragRow swallows the click)
            _track.RegisterCallback<ClickEvent>(e => { if (_moved) { e.StopPropagation(); _moved = false; } }, TrickleDown.TrickleDown);
            _track.RegisterCallback<WheelEvent>(OnWheel);
            _root.Add(_track);

            var P = _L.panel;
            _panel = Pictures.Abs("panel", D(P.left), D(P.top), 0, D(P.height));
            _panel.style.width = StyleKeyword.Auto;
            _panel.style.right = D(P.right);
            _root.Add(_panel);

            _fx = Pictures.Abs("fly", 0, 0, 0, 0);
            _fx.style.width = Length.Percent(100); _fx.style.height = Length.Percent(100);
            _root.Add(_fx);

            // the track and the panel need the screen's width: drawn once it's known (and again if it changes)
            _root.RegisterCallback<GeometryChangedEvent>(e =>
            {
                float w = e.newRect.width;
                if (w <= 0f || Mathf.Abs(w - _w) < 0.01f) return;
                _w = w;
                DrawTrack();
                DrawPanel();
                if (!_laidOut) { _laidOut = true; Center(_sel); Reveal(); }
            });
            return _root;
        }

        public override bool OnBack() => false; // the router goes back to the menu

        int FirstSelection()
        {
            foreach (var r in _rows) if (r.Claimable) return r.L;
            return Mathf.Min(_rows.Count, _state.xp.level + 1);
        }

        int NClaim()
        {
            int n = 0;
            foreach (var r in _rows) if (r.Claimable && !_pend.ContainsKey(r.L)) n++;
            return n;
        }

        /// <summary>The app's new player data (the page's App.on('state')): the selection stays, claims now marked
        /// got stop waiting.</summary>
        public void SetState(LevelXp xp, List<RoadPrize> road)
        {
            if (xp != null) _state.xp = xp;
            _state.road = road ?? new List<RoadPrize>();
            _rows = LevelPassRules.Rows(_state);
            var keys = new List<int>(_pend.Keys);
            foreach (var L in keys) { var r = Row(L); if (r != null && r.Got) _pend.Remove(L); }
            if (_sel > _rows.Count) _sel = _rows.Count;
            Redraw();
        }

        public void SetCoins(long coins) { _state.coins = coins; if (_right != null) DrawHeaderRight(); }

        void Redraw()
        {
            if (_L == null) return;
            DrawHeaderRight();
            if (_w > 0f) { DrawTrack(); DrawPanel(); }
        }

        // ---------------- the header ----------------

        void BuildHeader()
        {
            var H = _L.header;
            float hx = D(H.left), hy = D(H.top);
            _header = Pictures.Abs("header", hx, hy, 0, D(H.height));
            _header.style.width = StyleKeyword.Auto;
            _header.style.right = D(H.right);
            _header.pickingMode = PickingMode.Ignore;
            _root.Add(_header);

            // back: the orb (its box takes the tap)
            float bx = hx + D(H.back.box[0]), by = hy + D(H.back.box[1]);
            var back = Pictures.Abs("back", bx - hx, by - hy, D(H.back.box[2]), D(H.back.box[3]));
            back.pickingMode = PickingMode.Position;
            back.Add(Pictures.Place(H.back.pic, bx, by, bx, by));
            back.RegisterCallback<ClickEvent>(_ => Close());
            _header.Add(back);
            _header.Add(Pictures.Place(H.title.pic, hx + D(H.title.box[0]), hy + D(H.title.box[1]), hx, hy));

            // the plaques, in a row from the right edge (their widths follow their text)
            _right = new VisualElement { name = "plaques", pickingMode = PickingMode.Ignore };
            _right.style.position = Position.Absolute;
            _right.style.right = 0; _right.style.top = 0; _right.style.bottom = 0;
            _right.style.flexDirection = FlexDirection.Row;
            _right.style.alignItems = Align.Center;
            _header.Add(_right);
            DrawHeaderRight();
        }

        void DrawHeaderRight()
        {
            var H = _L.header;
            _right.Clear();
            int n = NClaim();
            if (n > 1)
            {
                var all = Plaque("claim-all", H.claimAll);
                all.pickingMode = PickingMode.Position;
                all.Add(new PassText(H.claimAll.text, _k, "CLAIM ALL (" + n + ")", D(H.claimAll.height), TextAnchor.MiddleLeft));
                all.RegisterCallback<ClickEvent>(_ => ClaimAll());
                _right.Add(all);
            }

            // LV n, the bar, into / need XP
            var xp = _state.xp;
            float f = xp.need > 0 ? Mathf.Clamp01((float)xp.into / xp.need) : 0f;
            f = Mathf.Round(f * 1000f) / 1000f;   // (width: (f * 100).toFixed(1) + '%')
            var lv = Plaque("lv", H.lv);
            lv.Add(new PassText(H.lv.text, _k, "LV " + xp.level, D(H.lv.height), TextAnchor.MiddleLeft));
            var bar = new VisualElement { name = "bar", pickingMode = PickingMode.Ignore };
            bar.style.width = D(H.lv.bar.width); bar.style.height = D(H.lv.bar.height);
            bar.style.marginLeft = D(H.lv.gap); bar.style.flexShrink = 0;
            bar.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                bar.Clear();
                var p = InRoot(bar);
                bar.Add(Pictures.Stretched(H.lv.bar.track, p.x, p.y, D(H.lv.bar.width), p.x, p.y));
                if (f > 0f) bar.Add(Pictures.Stretched(H.lv.bar.fill, p.x, p.y, D(H.lv.bar.width * f), p.x, p.y)); // (inside the track, rounded like it)
            });
            lv.Add(bar);
            var small = new PassText(H.lv.text2, _k, xp.need > 0 ? LevelPassRules.Fmt(xp.into) + " / " + LevelPassRules.Fmt(xp.need) + " XP" : "",
                D(H.lv.height), TextAnchor.MiddleLeft);
            small.style.marginLeft = D(H.lv.gap);
            lv.Add(small);
            if (_right.childCount > 0) lv.style.marginLeft = D(H.gap);
            _right.Add(lv);

            // the coins, the coin over the pill's left end
            var coins = Plaque("coins", H.coins);
            var ib = H.coins.icon.box;
            _coinIcon = Pictures.Abs("coin", D(ib[0]), D(ib[1]), D(ib[2]), D(ib[3]));
            coins.Add(_coinIcon);
            var icon = _coinIcon;
            coins.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                icon.Clear();
                var p = InRoot(icon);
                icon.Add(Pictures.Place(H.coins.icon.pic, p.x, p.y, p.x, p.y));
                _coinCenter = new Vector2(p.x + icon.layout.width / 2f, p.y + icon.layout.height / 2f);
                _hasCoin = true;
            });
            coins.Add(new PassText(H.coins.text, _k, LevelPassRules.Fmt(_state.coins), D(H.coins.height), TextAnchor.MiddleLeft));
            coins.style.marginLeft = D(H.gap);
            _right.Add(coins);
        }

        // A plaque: a row between its paddings; its frame drawn behind once its width is known.
        VisualElement Plaque(string name, LevelPassLayout.PlaqueSpec spec)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            e.style.flexShrink = 0;
            e.style.height = D(spec.height);
            e.style.paddingLeft = D(spec.padLeft);
            e.style.paddingRight = D(spec.padRight);
            var frame = Pictures.Abs("frame", 0, 0, 0, 0);
            e.Add(frame);
            float lastX = float.NaN, lastW = float.NaN;
            e.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                var p = InRoot(e);
                float w = e.layout.width;
                if (p.x == lastX && w == lastW) return;
                lastX = p.x; lastW = w;
                frame.Clear();
                frame.Add(Pictures.Stretched(spec.frame, p.x, p.y, w, p.x, p.y));
            });
            return e;
        }

        // an element's top-left in the screen's space (dp)
        Vector2 InRoot(VisualElement e) => new Vector2(e.worldBound.x - _root.worldBound.x, e.worldBound.y - _root.worldBound.y);

        // ---------------- the track ----------------

        float InnerWidth => Cx(_rows.Count) + _L.track.endPad;
        float MaxScroll => Mathf.Max(0f, InnerWidth - _w / _k);

        void DrawTrack()
        {
            var T = _L.track;
            float top = D(T.top);       // the track's top on the screen; the inner row's x is the screen's, before scrolling
            _track.Clear();
            _inner = Pictures.Abs("track-row", 0, 0, D(InnerWidth), D(T.height));
            _track.Add(_inner);
            int N = _rows.Count;
            var xp = _state.xp;
            float f = xp.need > 0 ? Mathf.Clamp01((float)xp.into / xp.need) : 0f;
            float fill = Mathf.Round(Mathf.Min(Cx(N), Cx(xp.level) + f * T.col) * 10f) / 10f;   // (toFixed(1))
            float railY = top + D(T.railTop);

            _inner.Add(Pictures.Stretched(T.rail, D(Cx(1)), railY, D(Cx(N) - Cx(1)), 0f, top));
            _inner.Add(Pictures.Stretched(T.fill, D(Cx(1)), railY, D(Mathf.Max(0f, fill - Cx(1))), 0f, top));

            foreach (var row in _rows) _inner.Add(Node(row, top));

            // you: the frame (a picture per sub-pixel phase), the creature in it, the pointer under it
            var Y = T.you;
            float yx = D(fill - T.youSize / 2f), yy = top + D(T.youTop);
            int ph = Phase(yx, Y.phases);
            _inner.Add(Pictures.Place(Y.frame[ph], yx, yy, 0f, top));
            var cre = LevelPassLayout.Find(Y.creatures, _state.creature);
            if (cre != null) _inner.Add(Pictures.Place(cre, yx, yy, 0f, top));
            _inner.Add(Pictures.Place(Y.pointer[ph], yx, yy, 0f, top));

            _track.style.opacity = _fill;
            SetScroll(_scroll);
        }

        // which of the pictures drawn at sub-pixel phases fits a box starting at x (dp)
        static int Phase(float x, float[] phases)
        {
            float px = x * K, f = px - Mathf.Floor(px);
            int best = 0; float bd = 9f;
            for (int i = 0; i < phases.Length; i++)
            {
                float d = Mathf.Abs(f - phases[i]); d = Mathf.Min(d, 1f - d);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        VisualElement Node(LevelRow row, float top)
        {
            var T = _L.track; var N = T.nodes;
            _pend.TryGetValue(row.L, out int p);
            bool cl = row.Claimable && p == 0;
            bool got = row.Got || p == Done || (row.Reached && row.Prize == null);
            bool cur = row.L == _state.xp.level && !cl;   // (the claim bob's transform wins over .cur's scale)
            bool sel = _sel == row.L;
            string look = (row.Reached ? "gold" : "brown") + (cur ? "-cur" : "") + (sel ? "-sel" : cl ? "-glow" : "");
            string badge = cl ? "ex" : got ? (cur ? "ok-cur" : "ok") : row.Reached ? null : "lock";

            float nx = D(Cx(row.L) - T.nodeSize / 2f), ny = top + D(T.nodeTop), size = D(T.nodeSize);
            int ph = Phase(nx, N.phases);
            var node = Pictures.Abs("level-" + row.L, nx, ny - top, size, size);
            node.pickingMode = PickingMode.Position;
            var lk = LevelPassLayout.Find(N.looks, look);
            if (lk != null) node.Add(Pictures.Place(lk.pics[ph], nx, ny, nx, ny));
            if (badge != null) { var b = LevelPassLayout.Find(N.badges, badge); if (b != null) node.Add(Pictures.Place(b.pics[ph], nx, ny, nx, ny)); }
            // the number (drawn by Unity), grown with the node on the current level
            var num = Pictures.Abs("number", 0, 0, size, size);
            num.Add(new PassText(row.Reached ? N.goldText : N.brownText, _k, row.L.ToString(), size, TextAnchor.MiddleCenter));
            if (cur) { num.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50)); num.style.scale = new Scale(new Vector2(N.curScale, N.curScale)); }
            node.Insert(lk != null ? 1 : 0, num); // over the node, under its badge
            int level = row.L;
            node.RegisterCallback<ClickEvent>(_ => TapLevel(level));
            if (cl)
            {
                // .sq-ln.cl: sqClaim 1.2s ease-in-out infinite, translateY 0 -> -4px -> 0
                var bob = new CssAnimation { Duration = N.bob.duration, Iterations = float.PositiveInfinity, Timing = Easing.Parse(N.bob.easing) };
                bob.Key(new CssKeyframe(0f).Move(0f, 0f)).Key(new CssKeyframe(0.5f).Move(0f, D(N.bob.y))).Key(new CssKeyframe(1f).Move(0f, 0f));
                new CssAnimator(node).Play(bob);
            }
            return node;
        }

        void Center(int level) => SetScroll(Cx(level) - _w / _k / 2f);

        void SetScroll(float s)
        {
            _scroll = Mathf.Clamp(s, 0f, MaxScroll);
            ApplyScroll();
        }

        // the row moves by whole pixels, so every picture in it stays on the pixel grid
        void ApplyScroll() { if (_inner != null) _inner.style.left = -Pictures.Snap(D(_scroll)); }

        void OnDown(PointerDownEvent e)
        {
            if (e.button > 0) return;
            StopGlide();
            _pointer = e.pointerId;
            _x0 = _lx = e.position.x;
            _s0 = _scroll;
            _lt = NowMs;
            _v = 0f;
            _moved = false;
        }

        void OnMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointer) return;
            float dx = e.position.x - _x0;
            if (!_moved && Mathf.Abs(dx) > DragThreshold) { _moved = true; _track.CapturePointer(_pointer); }
            if (!_moved) return;
            float t = NowMs;
            SetScroll(_s0 - dx / _k);
            _v = (e.position.x - _lx) / Mathf.Max(1f, t - _lt) / _k;   // design px per ms
            _lx = e.position.x; _lt = t;
        }

        void OnUp(int pointerId)
        {
            if (pointerId != _pointer) return;
            _pointer = -1;
            if (_track.HasPointerCapture(pointerId)) _track.ReleasePointer(pointerId);
            if (!_moved || Mathf.Abs(_v) < 0.05f) return;
            // the glide: on at the release's speed, slowing ×0.94 every 16 ms, until it's under 0.02 px/ms
            float last = NowMs;
            _glide = _track.schedule.Execute(() =>
            {
                float now = NowMs, dt = Mathf.Min(40f, now - last);
                last = now;
                SetScroll(_scroll - _v * dt);
                _v *= Mathf.Pow(0.94f, dt / 16f);
                if (Mathf.Abs(_v) <= 0.02f) StopGlide();
            }).Every(0);
        }

        void StopGlide()
        {
            if (_glide != null) { _glide.Pause(); _glide = null; }
        }

        void OnWheel(WheelEvent e)
        {
            float d = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x) ? e.delta.y : e.delta.x;
            if (d == 0f) return;
            StopGlide();
            SetScroll(_scroll + d * WheelStep);
            e.StopPropagation();
        }

        // ---------------- the panel ----------------

        void DrawPanel()
        {
            var P = _L.panel; var C = P.column; var Cd = P.card;
            float px = D(P.left), py = D(P.top), wd = _w / _k, pw = wd - P.left - P.right;
            _panel.Clear();
            _panel.Add(Pictures.Stretched(P.frame, px, py, D(pw), px, py));
            _panelContent = Pictures.Abs("content", 0, 0, D(pw), D(P.height));
            _panelContent.pickingMode = PickingMode.Ignore;
            _panelContent.style.opacity = _fill;
            _panel.Add(_panelContent);
            _hasFirstArt = false;
            var row = Row(_sel);
            if (row == null) return;
            _pend.TryGetValue(row.L, out int p);

            // the left column: LEVEL n, its line, the action
            float colX = P.left + P.padLeft, colY = P.top + P.padTop + (P.itemsHeight - (C.actTop + C.actHeight)) / 2f;
            var lt = LevelPassLayout.Find(C.lettering, row.L + "-" + (row.Reached ? "gold" : "white"));
            if (lt != null) _panelContent.Add(Pictures.Place(lt, D(colX + (C.width - lt.w) / 2f), D(colY), px, py));
            else _panelContent.Add(Lettering("LEVEL " + row.L, row.Reached, 24f, colX, colY, C.width, C.letteringHeight, px, py));

            var cards = LevelPassRules.Cards(row);
            string sub = !row.Reached ? "Reach it to get " + (cards.Count > 1 ? "these" : "this") : row.L == _state.xp.level ? "Your level" : "Reached";
            var subText = new PassText(C.sub, _k, sub, D(C.subHeight), TextAnchor.MiddleCenter);
            subText.style.position = Position.Absolute;
            subText.style.left = D(colX) - px; subText.style.top = D(colY + C.subTop) - py; subText.style.width = D(C.width);
            _panelContent.Add(subText);

            float ax = D(colX + (C.width - C.actWidth) / 2f), ay = D(colY + C.actTop);
            if (row.Prize != null && row.Claimable && p == 0)
            {
                _panelContent.Add(Pictures.Place(C.claim, ax, ay, px, py));
                var hit = Pictures.Abs("claim", ax - px, ay - py, D(C.actWidth), D(C.actHeight));
                hit.pickingMode = PickingMode.Position;
                int level = row.L;
                hit.RegisterCallback<ClickEvent>(_ => Claim(level));
                _panelContent.Add(hit);
            }
            else
            {
                _panelContent.Add(Pictures.Place(C.actFrame, ax, ay, px, py));
                var act = Pictures.Abs("action", ax - px, ay - py, D(C.actWidth), D(C.actHeight));
                act.style.flexDirection = FlexDirection.Row;
                act.style.justifyContent = Justify.Center;
                act.style.alignItems = Align.Center;
                Pic icon = null; string text;
                if (p == Pending) text = "Claiming…";
                else if (row.Reached) { icon = C.check; text = row.Prize != null ? "CLAIMED" : "UNLOCKED"; }
                else { icon = C.@lock; text = LevelPassRules.Fmt(LevelPassRules.XpTo(_state.xp, row.L)) + " XP to go"; }
                if (icon != null)
                {
                    var ib = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
                    ib.style.width = D(C.iconSize); ib.style.height = D(C.iconSize); ib.style.flexShrink = 0;
                    ib.Add(Pictures.Place(icon, 0f, 0f));
                    act.Add(ib);
                }
                var at = new PassText(C.actText, _k, text, D(C.actHeight), TextAnchor.MiddleLeft);
                if (icon != null) at.style.marginLeft = D(C.iconGap);
                act.Add(at);
                _panelContent.Add(act);
            }

            // the reward cards, centred in what's left of the panel (at most 196 wide each, shrinking to fit)
            float itemsX = colX + C.width + P.gap, itemsW = pw - P.padLeft - C.width - P.gap - P.padRight, iy = P.top + P.padTop;
            int n = Mathf.Max(1, cards.Count);
            float cw = Mathf.Max(0f, Mathf.Min(Cd.basis, (itemsW - Cd.gap * (n - 1)) / n)), total = n * cw + Cd.gap * (n - 1);
            float x0 = itemsX + (itemsW - total) / 2f;
            if (cards.Count == 0)
            {
                _panelContent.Add(Pictures.Stretched(Cd.bg, D(x0), D(iy), D(cw), px, py));
                AddDesc("Nothing new at this level", x0, iy + Cd.noneDescTop, cw, px, py);
                return;
            }
            for (int i = 0; i < cards.Count; i++) AddCard(cards[i], x0 + i * (cw + Cd.gap), iy, cw, px, py, i == 0);
        }

        // One reward card, in the page's painting order: its background, title and line, then (positioned, later in
        // the page's paint) the tag, the glow and the art, which can overlap the title.
        void AddCard(RewardCard c, float x, float y, float cw, float px, float py, bool first)
        {
            var Cd = _L.panel.card;
            float arX = x + Cd.padX, arW = cw - 2f * Cd.padX, arY = y + Cd.padTop;
            _panelContent.Add(Pictures.Stretched(Cd.bg, D(x), D(y), D(cw), px, py));
            var t = LevelPassLayout.Find(Cd.titles, c.Title);
            if (t != null) _panelContent.Add(Pictures.Place(t, D(x + (cw - t.w) / 2f), D(y + Cd.titleTop), px, py));
            else _panelContent.Add(Lettering(c.Title, false, 15f, x, y + Cd.titleTop, cw, Cd.titleHeight, px, py));
            AddDesc(c.Desc, x, y + Cd.descTop, cw, px, py);

            var tag = LevelPassLayout.Find(Cd.tags, c.Tag);
            if (tag != null) _panelContent.Add(Pictures.Place(tag, D(x + (cw - tag.w) / 2f), D(y + Cd.tagTop), px, py));
            _panelContent.Add(Pictures.Place(Cd.glow, D(arX + arW / 2f - Cd.glow.w / 2f), D(arY + Cd.artHeight / 2f - Cd.glow.h / 2f), px, py));
            NamedPic art = null; float artX = 0f, artY = 0f;
            switch (c.ArtKind)
            {
                case "chest":
                    // img.chest: 150% of the art's height, margins -22% / -20% of its width, centred
                    art = LevelPassLayout.Find(Cd.chests, c.ArtKey);
                    if (art != null)
                    {
                        float mt = Cd.chestFit.marginTopPct * arW, mb = Cd.chestFit.marginBottomPct * arW;
                        artX = arX + (arW - art.w) / 2f; artY = arY + (Cd.artHeight - (art.h + mt + mb)) / 2f + mt;
                    }
                    break;
                case "plainChest": art = LevelPassLayout.Find(Cd.plainChests, c.ArtKey); break;
                case "pile": art = LevelPassLayout.Find(Cd.piles, c.ArtKey); break;
                case "room":
                    art = LevelPassLayout.Find(Cd.rooms, c.ArtKey);
                    if (art != null) { artX = arX + (arW - art.w) / 2f; artY = arY; }
                    break;
            }
            if (art != null)
            {
                if (c.ArtKind == "plainChest" || c.ArtKind == "pile") { artX = arX + (arW - art.w) / 2f; artY = arY + (Cd.artHeight - art.h) / 2f; }
                _panelContent.Add(Pictures.Place(art, D(artX), D(artY), px, py));
            }
            if (first) { _firstArt = new Rect(D(arX), D(arY), D(arW), D(Cd.artHeight)); _hasFirstArt = true; }
        }

        // A description: in the lines the browser broke it into (for the nearest card width), else wrapped by Unity.
        void AddDesc(string text, float x, float y, float cw, float px, float py)
        {
            var Cd = _L.panel.card;
            float tw = cw - 2f * Cd.padX, lh = Cd.desc.lineHeight > 0f ? Cd.desc.lineHeight : Cd.desc.size * 1.25f;
            LevelPassLayout.DescLines best = null;
            if (Cd.descLines != null)
                foreach (var d in Cd.descLines)
                    if (d.key == text && (best == null || Mathf.Abs(d.width - tw) < Mathf.Abs(best.width - tw))) best = d;
            if (best != null && best.lines != null)
            {
                for (int i = 0; i < best.lines.Length; i++)
                {
                    var line = new PassText(Cd.desc, _k, best.lines[i], D(lh), TextAnchor.MiddleCenter);
                    line.style.position = Position.Absolute;
                    line.style.left = D(x + Cd.padX) - px; line.style.top = D(y + i * lh) - py; line.style.width = D(tw);
                    _panelContent.Add(line);
                }
                return;
            }
            var l = new PassText(Cd.desc, _k, text, D(lh), TextAnchor.UpperCenter);
            l.style.position = Position.Absolute;
            l.style.left = D(x + Cd.padX) - px; l.style.top = D(y) - py; l.style.width = D(tw);
            l.style.height = StyleKeyword.Auto;
            foreach (var child in l.Children()) if (child is Label lab) { lab.style.whiteSpace = WhiteSpace.Normal; lab.style.height = StyleKeyword.Auto; }
            _panelContent.Add(l);
        }

        // Lettering with no picture (a level past the exported road, a title not exported): Fredoka, outlined.
        VisualElement Lettering(string text, bool gold, float size, float x, float y, float w, float h, float px, float py)
        {
            var l = new Label(text).Use(Fonts.Fredoka700);
            l.pickingMode = PickingMode.Ignore;
            l.style.position = Position.Absolute;
            l.style.left = D(x) - px; l.style.top = D(y) - py; l.style.width = D(w); l.style.height = D(h);
            l.style.fontSize = D(size);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.color = gold ? (Color)new Color32(0xff, 0xd2, 0x4a, 0xff) : (Color)new Color32(0xff, 0xf6, 0xe2, 0xff);
            l.style.unityTextOutlineColor = (Color)new Color32(0x6b, 0x44, 0x26, 0xff);
            l.style.unityTextOutlineWidth = 0.25f;
            return l;
        }

        // ---------------- taps, claims, the burst ----------------

        void TapLevel(int level)
        {
            // a tap picks a level; a second tap on a level waiting to be claimed claims it
            if (_sel == level) { Claim(level); return; }
            _sel = level;
            Redraw();
        }

        void Claim(int level)
        {
            var row = Row(level);
            if (row == null || !row.Claimable || _pend.ContainsKey(level)) return;
            _pend[level] = Pending;
            Redraw();
            if (_onClaim != null) _onClaim(new LevelPassClaim { Level = level, Kind = row.Prize != null ? row.Prize.k : null }, OnClaimed);
            else { _pend[level] = Done; Burst(level); Redraw(); }   // (outside the app: claimed at once)
        }

        void ClaimAll()
        {
            var ls = new List<int>();
            foreach (var r in _rows) if (r.Claimable && !_pend.ContainsKey(r.L)) ls.Add(r.L);
            if (ls.Count == 0) return;
            foreach (var L in ls) _pend[L] = Pending;
            Redraw();
            if (_onClaim != null) _onClaim(new LevelPassClaim { Level = ls[0], All = true }, OnClaimed);
            else { foreach (var L in ls) { _pend[L] = Done; Burst(L); } Redraw(); }
        }

        // The answer (menu.js App.on('claimed')): every level still "Claiming…" is claimed, or back to waiting.
        void OnClaimed(LevelPassClaimResult r)
        {
            if (View == null || View.panel == null) return; // closed meanwhile
            if (r == null) r = new LevelPassClaimResult { Ok = false, Error = "failed" };
            if (r.Road) return; // a chest: the app opens its own screen for it
            var ls = new List<int>();
            foreach (var kv in _pend) if (kv.Value == Pending) ls.Add(kv.Key);
            if (r.Ok) foreach (var L in ls) { _pend[L] = Done; Burst(L); }
            else
            {
                foreach (var L in ls) _pend.Remove(L);
                App.Toast(r.Error == "claimed" ? "Already claimed" : r.Error == "need_level" ? "Reach that level first" : "Something went wrong. Try again!");
            }
            Redraw();
        }

        // 9 coins (or gems) from the reward's art, or its level's node, to the coin pill's coin (menu.js burst).
        void Burst(int level)
        {
            var row = Row(level);
            if (row == null || row.Prize == null || row.Prize.k == "chest" || _L.fly == null) return;
            var F = _L.fly;
            var T = _L.track;
            Rect from = _sel == level && _hasFirstArt ? _firstArt
                : new Rect(D(Cx(level) - T.nodeSize / 2f) - Pictures.Snap(D(_scroll)), D(T.top + T.nodeTop), D(T.nodeSize), D(T.nodeSize));
            if (!_hasCoin) return;
            Vector2 toC = _coinCenter;
            var icon = LevelPassLayout.Find(F.icons, row.Prize.k == "gems" ? "gem" : "coin");
            if (icon == null) return;
            float half = icon.w / 2f;
            var move = Easing.Parse(F.easing);
            var fade = Easing.Parse(F.fadeEasing);
            for (int i = 0; i < F.count; i++)
            {
                // design px, as the page: the start jittered by +-20 / +-15
                float ax = from.center.x / _k - half + (UnityEngine.Random.value - 0.5f) * F.jitter[0];
                float ay = from.center.y / _k - half + (UnityEngine.Random.value - 0.5f) * F.jitter[1];
                float bx = toC.x / _k - half, by = toC.y / _k - half;
                var e = Pictures.Abs("fly", D(ax), D(ay), D(icon.w), D(icon.h));
                e.Add(Pictures.Place(icon, 0f, 0f));
                float at = F.start + i * F.every;
                var go = new CssAnimation { Duration = F.duration, Delay = at, Timing = move, Fill = AnimFill.Forwards };
                go.Key(new CssKeyframe(0f).Move(0f, 0f).Scale(1f)).Key(new CssKeyframe(1f).Move(D(bx - ax), D(by - ay)).Scale(F.scale));
                var off = new CssAnimation { Duration = F.fadeDuration, Delay = at + F.fadeDelay, Timing = fade, Fill = AnimFill.Forwards };
                off.Key(new CssKeyframe(0f).Fade(1f)).Key(new CssKeyframe(1f).Fade(0f));
                new CssAnimator(e, "50% 50%", autostart: true, inReference: false).Play(go).Play(off);
                _fx.Add(e);
                var gone = e;
                e.schedule.Execute(() => gone.RemoveFromHierarchy()).StartingIn((long)((F.removeAt + i * F.every) * 1000f));
            }
        }

        // ---------------- opening and closing ----------------

        // The track and the panel's content fade in (0.18 s ease) once drawn; the header is there from the start.
        void Reveal()
        {
            if (ReferenceOverlay.FreezeAnimations) { SetFill(1f); return; }
            var ease = Easing.Ease;
            float t0 = -1f;
            IVisualElementScheduledItem tick = null;
            tick = _root.schedule.Execute(() =>
            {
                if (t0 < 0f) { t0 = Time.realtimeSinceStartup; return; }   // (from the first drawn frame, as the page)
                float t = (Time.realtimeSinceStartup - t0) / 0.18f;
                SetFill(ease(Mathf.Clamp01(t)));
                if (t >= 1f) tick.Pause();
            }).Every(0);
        }

        void SetFill(float o)
        {
            _fill = o;
            if (_track != null) _track.style.opacity = o;
            if (_panelContent != null) _panelContent.style.opacity = o;
        }

        void Close()
        {
            StopGlide();
            App.Router.Back(); // OnBack() says no, so the router goes back to the menu
        }
    }
}
