// The Main Menu, round 11 (the user's requests of 2026-11-14 on the Nov 13 design's menu):
//  - HUD: no "Cozy home" / "Next room" pills, no GAMES tab, no "Let's play" board (PLAY stays, on its own),
//    CREATURES is CHARACTERS, and the coin / gem pills have room between the icon and the number
//    (the markup changes are patches in webgame/patches.mjs; the look is the CSS below)
//  - the level crest is alive (wings flap, the gold shines, sparkles, an XP ring, a "!" for prizes waiting) and opens
//    the Level Pass: every level, its prize and what it unlocks (rooms, the yard games, the house's next look)
//  - PLAY: the creature turns, walks left and the camera follows it to the Game Park, a new part of the map left of the
//    house (drawn with the scene's own sky, grass, trees and weather). There the games line up in a carousel like the
//    app's Achievements screen; the one in the middle plays a 5 s clip of the real game (app/previews/<id>.mp4)
//  - CHARACTERS: a full screen to look at every character (its live rig) and swap the one standing in front of the house
// main-menu.js hands this file its insides (opts._x, patched in) and calls SquishMenu.crest / .play / .levels /
// .characters / .mounted / .inPark; main-menu-scene.js calls SquishMenu.parkArt for the park's picture.
(function () {
  'use strict';
  const App = window.SquishApp || null;
  const M = window.SquishMenu = {};

  // ---------- helpers ----------
  const esc = t => String(t == null ? '' : t).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
  const fmt = n => n == null ? '' : Number(n).toLocaleString('en-US');
  const clamp = (x, a, b) => Math.max(a, Math.min(b, x));
  const r1 = x => (Math.round(x * 10) / 10).toString();
  const wait = ms => new Promise(r => setTimeout(r, ms));
  const tween = (ms, fn, ease) => new Promise(res => {
    const t0 = performance.now();
    const step = () => { const t = Math.min(1, (performance.now() - t0) / ms); fn(ease ? ease(t) : t, t); if (t < 1) requestAnimationFrame(step); else res(); };
    requestAnimationFrame(step);
  });
  const EIO = t => t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
  const EIOC = t => t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
  const EOUT = t => 1 - (1 - t) * (1 - t);
  const ft = (t, s, z, c) => window.MainMenu.ft(t, s, z, c);
  const ic = (d, c, w) => '<svg viewBox="0 0 24 24" fill="none" stroke="' + (c || 'currentColor') + '" stroke-width="' + (w || 2.2) + '" stroke-linecap="round" stroke-linejoin="round"><path d="' + d + '"/></svg>';
  const BACK = 'M15 5L8 12L15 19', LEFT = 'M14 6L8 12L14 18', RIGHT = 'M10 6L16 12L10 18', CHECK = 'M5 12.5l4.5 4.5L19 7.5';
  const LOCK = s => '<svg viewBox="0 0 16 16" width="' + s + '" height="' + s + '"><path d="M4.5 7V5a3.5 3.5 0 0 1 7 0v2" stroke="#3d1268" stroke-width="1.8" fill="none"/><rect x="2.5" y="7" width="11" height="8" rx="2" fill="#ffd54a" stroke="#3d1268" stroke-width="1.2"/></svg>';
  const STAR = '<svg viewBox="0 0 24 24"><path d="M12 2.5l2.9 6 6.6.8-4.9 4.5 1.3 6.5L12 17l-5.9 3.3 1.3-6.5L2.5 9.3l6.6-.8Z" fill="#ffc928" stroke="#5a2a06" stroke-width="1.6" stroke-linejoin="round"/></svg>';
  const SPARK = '<svg viewBox="0 0 20 20"><path d="M10 0 Q11.4 8.6 20 10 Q11.4 11.4 10 20 Q8.6 11.4 0 10 Q8.6 8.6 10 0Z" fill="#fff7c8"/></svg>';
  const sfx = n => { try { const S = window.YardGames && window.YardGames.SFX; if (S && S[n]) S[n](); } catch (e) { /* no audio */ } };
  const send = (t, p) => { if (App) App.send(t, p); };
  const S = () => (App && App.state) || {};
  const BL = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';
  const coinImg = () => (window.SquishCoin && window.SquishCoin.coin(0)) || BL;
  const gemImg = () => (window.SquishCoin && window.SquishCoin.gem(0)) || BL;
  // a page opened before the coin / gem art is drawn (straight into the park): fill the icons in once it is
  function fillIcons(root, n) { root.querySelectorAll('img[data-cur]').forEach(im => { const s = im.dataset.cur === 'gem' ? gemImg() : coinImg(); if (s !== BL && im.getAttribute('src') !== s) im.src = s; });
    if ((n | 0) < 12 && [...root.querySelectorAll('img[data-cur]')].some(im => im.getAttribute('src') === BL)) setTimeout(() => fillIcons(root, (n | 0) + 1), 400); }
  const allowed = (I, list) => (window.SquishConfig && I && I.ak ? window.SquishConfig.filter(I.ak, list) : list);
  const pill = (img, n, cls) => '<div class="mm-pill mm-p tip v-vel ' + (cls || '') + '"><img alt="" data-cur="' + (img === gemImg() && img !== BL ? 'gem' : 'coin') + '" src="' + img + '"><span class="tx-w">' + fmt(n) + '</span></div>';
  let UID = 0;

  // ---------- the look ----------
  let cssOn = false;
  function addCss() {
    if (cssOn) return; cssOn = true;
    const s = document.createElement('style'); s.id = 'sq-menu-css';
    s.textContent = `
/* HUD: room between the coin / gem icon and the number, and between the pills */
.mm-hud .mm-pill{gap:10px;padding-right:14px;}
.mm-hud .mm-top > .mm-pill + .mm-pill,.mm-hud .mm-top > .mm-pill + .mm-orb{margin-left:9px;}
/* PLAY on its own (the "Let's play" board is gone) */
.mm-card.sq-solo{width:212px;padding:0;left:18px;bottom:18px;border:0;background:none;box-shadow:none;}
.mm-card.sq-solo .mm-play{height:64px;border-radius:20px;}
.sq-glint{position:absolute;inset:0;border-radius:inherit;overflow:hidden;pointer-events:none;}
.sq-glint::after{content:'';position:absolute;top:-20%;bottom:-20%;width:26%;left:-40%;background:linear-gradient(100deg,transparent,rgba(255,255,255,.8),transparent);transform:skewX(-18deg);animation:sqSweep 3.4s ease-in-out infinite;}
@keyframes sqSweep{0%,58%{left:-40%;}86%,100%{left:125%;}}
/* the level crest, alive */
.mm-lv{right:12px;bottom:6px;}
.sq-crest{position:relative;cursor:pointer;-webkit-tap-highlight-color:transparent;}
.sq-crest .sq-cin{animation:sqFloat 3.6s ease-in-out infinite;transform-origin:50% 62%;}
.sq-crest:active .sq-cin{animation:none;transform:scale(.92);}
@keyframes sqFloat{0%,100%{transform:translateY(0) scale(1);}50%{transform:translateY(-3px) scale(1.04);}}
.sq-crest .mm-crest{width:118px;height:102px;animation:sqGlow 3.6s ease-in-out infinite;}
@keyframes sqGlow{0%,100%{filter:drop-shadow(0 4px 4px rgba(20,0,50,.45)) drop-shadow(0 0 2px rgba(255,214,110,.25));}50%{filter:drop-shadow(0 4px 4px rgba(20,0,50,.45)) drop-shadow(0 0 13px rgba(255,214,110,.95));}}
.sq-wing1{transform-origin:44px 50px;animation:sqFlapL 3.2s ease-in-out infinite;}
.sq-wing-1{transform-origin:76px 50px;animation:sqFlapR 3.2s ease-in-out infinite;}
@keyframes sqFlapL{0%,60%,100%{transform:rotate(0);}68%{transform:rotate(17deg);}76%{transform:rotate(-5deg);}84%{transform:rotate(12deg);}93%{transform:rotate(0);}}
@keyframes sqFlapR{0%,60%,100%{transform:rotate(0);}68%{transform:rotate(-17deg);}76%{transform:rotate(5deg);}84%{transform:rotate(-12deg);}93%{transform:rotate(0);}}
.sq-cstar{transform-origin:60px 18px;animation:sqStarFlip 4.8s ease-in-out infinite;}
@keyframes sqStarFlip{0%,78%,100%{transform:scaleX(1);}86%{transform:scaleX(-1);}94%{transform:scaleX(1);}}
.sq-sweep{animation:sqShine 4.2s ease-in-out infinite;}
@keyframes sqShine{0%,52%{transform:translateX(-80px);}84%,100%{transform:translateX(80px);}}
.sq-spark{position:absolute;width:13px;height:13px;pointer-events:none;opacity:0;animation:sqTw 2.6s ease-in-out infinite;}
.sq-spark svg{width:100%;height:100%;display:block;filter:drop-shadow(0 0 3px rgba(255,236,150,.9));}
@keyframes sqTw{0%,100%{opacity:0;transform:scale(.3) rotate(0);}45%{opacity:1;transform:scale(1) rotate(45deg);}70%{opacity:0;transform:scale(.4) rotate(90deg);}}
.sq-badge{position:absolute;right:16px;top:4px;min-width:21px;height:21px;padding:0 5px;box-sizing:border-box;border-radius:999px;background:radial-gradient(circle at 35% 30%,#ffa6dc,#ff3fa8 60%,#c0127a);color:#fff;font:900 12.5px/21px Nunito,sans-serif;text-align:center;box-shadow:0 0 0 2px #fff,0 0 0 3.6px #3d1268,0 3px 6px rgba(0,0,0,.35);animation:sqBob 1.1s ease-in-out infinite;pointer-events:none;}
@keyframes sqBob{0%,100%{transform:translateY(0) scale(1);}50%{transform:translateY(-3px) scale(1.12);}}
/* ---- the Game Park ---- */
.sq-pk{position:absolute;inset:0;pointer-events:none;}
.sq-pkdim{position:absolute;inset:0;opacity:0;transition:opacity .5s ease;background:linear-gradient(90deg,rgba(30,8,64,0) 0%,rgba(30,8,64,.12) 24%,rgba(30,8,64,.5) 48%,rgba(30,8,64,.58) 100%),linear-gradient(180deg,rgba(30,8,64,.55) 0,rgba(30,8,64,0) 22%);}
.sq-pk.in .sq-pkdim{opacity:1;}
.sq-pk .mm-pg{pointer-events:none;}
.sq-pk .mm-pgh{pointer-events:auto;opacity:0;transform:translateY(-14px);transition:opacity .4s ease,transform .5s cubic-bezier(.2,.9,.25,1);}
.sq-pk.in .mm-pgh{opacity:1;transform:none;}
.sq-pk .mm-pgh .s{font:700 11.5px Nunito,sans-serif;color:#f3eaff;text-shadow:0 1px 2px rgba(30,0,60,.8);}
.sq-car{position:absolute;right:0;top:56px;bottom:0;pointer-events:auto;touch-action:pan-y;}
.sq-rays{position:absolute;left:50%;top:50%;width:560px;height:560px;margin:-280px 0 0 -280px;border-radius:50%;pointer-events:none;opacity:0;transition:opacity .6s ease .2s;
  background:repeating-conic-gradient(from 0deg,rgba(255,240,180,.5) 0deg 9deg,rgba(255,240,180,0) 9deg 22deg);
  -webkit-mask-image:radial-gradient(circle,#000 12%,rgba(0,0,0,0) 62%);mask-image:radial-gradient(circle,#000 12%,rgba(0,0,0,0) 62%);animation:sqSpin 12s linear infinite;}
.sq-pk.in .sq-rays{opacity:.55;}
@keyframes sqSpin{to{transform:rotate(360deg);}}
.sq-halo{position:absolute;left:50%;top:50%;width:320px;height:320px;margin:-160px 0 0 -160px;border-radius:50%;pointer-events:none;opacity:0;transition:opacity .6s ease .2s;
  background:radial-gradient(circle,rgba(255,255,255,.85) 0%,rgba(255,230,160,.5) 30%,rgba(255,120,220,.28) 50%,rgba(255,255,255,0) 70%);animation:sqHalo 2s ease-in-out infinite alternate;}
.sq-pk.in .sq-halo{opacity:1;}
@keyframes sqHalo{from{transform:scale(.9);}to{transform:scale(1.12);}}
.sq-slot{position:absolute;left:50%;top:50%;height:318px;margin-top:-161px;transition:transform .5s cubic-bezier(.3,1.3,.5,1),opacity .35s ease;}
.sq-slot .sq-gc{width:100%;height:100%;opacity:0;transform:translateX(160px) scale(.8);transition:opacity .45s ease,transform .6s cubic-bezier(.2,1.2,.4,1);}
.sq-pk.in .sq-slot .sq-gc{opacity:1;transform:none;}
.sq-gc{position:relative;box-sizing:border-box;display:flex;flex-direction:column;align-items:stretch;gap:7px;padding:8px 10px 10px;border-radius:24px;border-width:3.5px;cursor:pointer;
  background:var(--spk) padding-box,var(--dia) padding-box,linear-gradient(180deg,var(--gcl) 0%,var(--gc) 45%,var(--gcd) 100%) padding-box,var(--rimG) border-box !important;
  box-shadow:0 0 0 2px var(--ink),0 5px 0 2px var(--ink),0 14px 26px rgba(10,0,40,.55),inset 0 0 0 1.5px rgba(255,236,170,.6) !important;}
.sq-gc .win{width:100%;flex:1;min-height:0;border-radius:16px;overflow:hidden;position:relative;background:#1e0a40;box-shadow:0 0 0 2px var(--ink),0 0 0 4.5px #ffd76a,0 0 0 6px var(--ink);}
.sq-gc .win > *{position:absolute;inset:0;width:100%;height:100%;display:block;object-fit:cover;}
.sq-gc .win::after{content:'';position:absolute;inset:0;border-radius:inherit;box-shadow:inset 0 -10px 16px rgba(0,0,0,.22),inset 0 2px 0 rgba(255,255,255,.25);pointer-events:none;}
.sq-gc .ttl{flex:none;display:flex;justify-content:center;line-height:0;margin:-3px 0 -2px;}
.sq-gc .info{flex:none;display:flex;align-items:center;gap:12px;}
.sq-gc .info .txt{flex:1;min-width:0;display:flex;flex-direction:column;align-items:flex-start;gap:5px;text-align:left;}
.sq-gc .info .ds{font:700 12px/1.3 Nunito,sans-serif;color:#fff;text-shadow:0 1px 2px rgba(30,0,60,.55);max-width:100%;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;}
.sq-gc .best{margin-top:0;padding:2px 10px;border-radius:999px;background:rgba(30,0,60,.38);box-shadow:inset 0 0 0 1px rgba(255,236,170,.6);font:800 10.5px Nunito,sans-serif;color:#ffe9a8;white-space:nowrap;max-width:100%;overflow:hidden;text-overflow:ellipsis;}
.sq-gc .go{width:148px;height:46px;flex:none;border-radius:15px;display:flex;align-items:center;justify-content:center;}
.sq-gc .go:active{transform:translateY(2px);}
.sq-slot:not(.c) .go{pointer-events:none;filter:saturate(.7);}
.sq-slot.c .sq-gc{animation:sqCen .5s ease;}
.sq-gc.lock .win > :not(.lockv){filter:saturate(.2) brightness(.7);}
.sq-gc .lockv{position:absolute;inset:0;justify-content:center;display:flex;flex-direction:column;align-items:center;gap:4px;font:700 12px Fredoka,sans-serif;color:#fff;text-align:center;text-shadow:0 1px 2px #000;}
.sq-sqz{background:radial-gradient(circle at 50% 42%,#ffe3f6,#ffb3e6 55%,#ff8fd6);}
.sq-sqz img{position:absolute;left:20%;top:8%;width:60%;height:88%;object-fit:contain;transform-origin:50% 100%;animation:sqSqz 1.9s ease-in-out infinite;}
@keyframes sqSqz{0%,100%{transform:scale(1,1);}18%{transform:scale(1.22,.72);}30%{transform:scale(.9,1.14);}42%{transform:scale(1.05,.95);}54%{transform:scale(1,1);}70%{transform:scale(1.12,.84) rotate(-4deg);}82%{transform:scale(.95,1.06) rotate(3deg);}}
.sq-sqz i{position:absolute;top:38%;width:9%;height:24%;border:3px solid #fff;border-color:#fff transparent;border-radius:50%;opacity:0;animation:sqLines 1.9s ease-out infinite;}
.sq-sqz i.l{left:3%;border-width:0 0 0 3px;border-left-color:#fff;}.sq-sqz i.r{right:3%;border-width:0 3px 0 0;border-right-color:#fff;}
@keyframes sqLines{0%,12%{opacity:0;}20%{opacity:1;}46%,100%{opacity:0;}}
@keyframes sqCen{0%{filter:brightness(1.35);}100%{filter:none;}}
.sq-arr{position:absolute;top:50%;margin-top:-20px;z-index:60;}
.sq-arr.l{left:10px;}.sq-arr.r{right:12px;}
.sq-arr.off{opacity:.4;}
.sq-pos{position:absolute;left:50%;bottom:1px;transform:translateX(-50%);display:flex;gap:6px;z-index:55;}
.sq-pos i{cursor:pointer;width:7px;height:7px;border-radius:50%;background:rgba(255,255,255,.35);box-shadow:0 0 0 1.5px rgba(61,18,104,.6);transition:all .3s;}
.sq-pos i.on{background:#ffd24a;width:18px;border-radius:4px;}
/* ---- shared page bits ---- */
.sq-toast.r{left:auto;right:18px;transform:none;animation:none;}
.sq-toast{position:absolute;left:50%;bottom:20px;transform:translateX(-50%);max-width:70%;padding:5px 18px;font:700 13px Fredoka,sans-serif;text-align:center;animation:mmToast 2.4s ease forwards;pointer-events:none;z-index:90;}
.sq-chip{display:inline-flex;align-items:center;gap:5px;height:24px;padding:0 11px;border-radius:999px;font:800 11px Nunito,sans-serif;color:#fff;background:rgba(255,255,255,.12);box-shadow:inset 0 0 0 1.5px rgba(255,236,170,.45);white-space:nowrap;cursor:pointer;flex:none;}
.sq-chip.on{background:linear-gradient(180deg,#fff1b0,#ffd650 45%,#f6a524);color:#3d1268;box-shadow:0 0 0 1.6px #3d1268,0 2px 0 1.6px #3d1268;}
/* ---- Level Pass ---- */
.sq-lvx{height:34px;display:flex;align-items:center;gap:9px;padding:0 14px 0 8px;cursor:default;}
.sq-lvx b{font:700 15px Fredoka,sans-serif;}
.sq-lvbar{width:118px;height:12px;border-radius:999px;background:rgba(20,0,50,.55);box-shadow:inset 0 0 0 1.5px rgba(255,236,170,.5);overflow:hidden;position:relative;}
.sq-lvbar i{position:absolute;left:0;top:0;bottom:0;border-radius:999px;background:linear-gradient(180deg,#ffb3e6,#ff4fbf 60%,#d0128a);box-shadow:inset 0 1.5px 0 rgba(255,255,255,.55);}
.sq-lvx small{font:800 10.5px Nunito,sans-serif;color:#f6e8ff;white-space:nowrap;}
.sq-lvall{height:30px;padding:0 13px;display:flex;align-items:center;font:700 12.5px Fredoka,sans-serif;animation:mmGlow 2.4s ease-in-out infinite;}
.sq-lvprev{position:absolute;left:16px;top:66px;bottom:14px;width:236px;border-radius:22px;border-width:3.5px;display:flex;flex-direction:column;align-items:center;padding:12px 12px 14px;cursor:default;text-align:center;}
.sq-lvprev .rib{margin-top:-2px;}
.sq-lvprev .art{width:118px;height:118px;margin:6px 0 4px;display:flex;align-items:center;justify-content:center;position:relative;}
.sq-lvprev .art::before{content:'';position:absolute;inset:-14px;border-radius:50%;background:radial-gradient(circle,rgba(255,240,190,.55),rgba(255,240,190,0) 68%);animation:sqHalo 2s ease-in-out infinite alternate;}
.sq-lvprev .art > *{position:relative;max-width:100%;max-height:100%;}
.sq-lvprev .t{margin-top:2px;line-height:0;}
.sq-lvprev .d{font:700 11.5px Nunito,sans-serif;color:#eadcff;margin-top:4px;line-height:1.3;min-height:30px;}
.sq-lvprev .also{display:flex;flex-wrap:wrap;justify-content:center;gap:5px;margin-top:6px;}
.sq-lvprev .act{margin-top:auto;width:100%;height:42px;border-radius:14px;display:flex;align-items:center;justify-content:center;gap:6px;font:700 14px Fredoka,sans-serif;}
.sq-lvprev .act.v-gold{animation:mmGlow 2.4s ease-in-out infinite;cursor:pointer;}
.sq-lvprev .act.done{cursor:default;}
.sq-lvprev .act > svg:not(.mm-ft){width:18px;height:18px;}
.sq-lvtrack{position:absolute;left:266px;right:0;top:62px;bottom:0;overflow-x:auto;overflow-y:hidden;scrollbar-width:none;-webkit-overflow-scrolling:touch;}
.sq-lvtrack::-webkit-scrollbar{display:none;}
.sq-lvin{position:relative;height:100%;}
.sq-rail{position:absolute;left:0;top:44px;height:14px;border-radius:999px;background:rgba(20,0,50,.5);box-shadow:inset 0 0 0 1.5px rgba(255,236,170,.35);}
.sq-rail i{position:absolute;left:0;top:0;bottom:0;border-radius:999px;background:linear-gradient(180deg,#fff3b8,#ffd24a 45%,#f09a14);box-shadow:inset 0 1.5px 0 rgba(255,255,255,.6),0 0 10px rgba(255,214,110,.7);transition:width .8s cubic-bezier(.2,.9,.25,1);}
.sq-you{position:absolute;top:0;width:34px;height:34px;margin-left:-17px;border-radius:50%;background:radial-gradient(circle at 35% 30%,#fff,#ffe6f6 50%,#ffb6e4);box-shadow:0 0 0 2.5px #3d1268,0 0 0 5px #ffd24a,0 0 0 6.6px #3d1268,0 0 16px 6px rgba(255,214,110,.75);z-index:5;overflow:hidden;animation:sqBob 1.6s ease-in-out infinite;}
.sq-you img{position:absolute;left:4%;top:6%;width:92%;height:92%;object-fit:contain;}
.sq-youp{position:absolute;top:35px;width:0;height:0;margin-left:-6px;border:6px solid transparent;border-top:7px solid #ffd24a;z-index:5;}
.sq-lc{position:absolute;top:0;width:96px;bottom:0;display:flex;flex-direction:column;align-items:center;}
.sq-lm{margin-top:36px;width:30px;height:30px;border-radius:50%;display:flex;align-items:center;justify-content:center;font:700 13px Fredoka,sans-serif;color:#fff;z-index:2;box-sizing:border-box;
  background:radial-gradient(circle at 35% 30%,#b98cff,#5a1cb8 70%);box-shadow:0 0 0 2px #3d1268,0 2px 0 2px #3d1268;}
.sq-lm.on{background:radial-gradient(circle at 35% 30%,#fffbe0,#ffd24a 50%,#e08a10);color:#3d1268;}
.sq-lt{position:relative;margin-top:12px;width:84px;border-radius:16px;border:0;padding:6px 4px 7px;display:flex;flex-direction:column;align-items:center;gap:3px;cursor:pointer;box-sizing:border-box;
  background:radial-gradient(120% 120% at 50% 0%,#9b6bff,#5a1cb8 60%,#3a0e86);box-shadow:0 0 0 2px #3d1268,0 3px 0 2px #3d1268,inset 0 0 0 1.5px rgba(255,236,170,.4);transition:transform .15s;}
.sq-lt + .sq-lt{margin-top:9px;}
.sq-lt .ar{width:58px;height:52px;display:flex;align-items:center;justify-content:center;}
.sq-lt .ar > *{max-width:100%;max-height:100%;}
.sq-lt .lb{font:700 11px Fredoka,sans-serif;color:#fff;line-height:1.1;text-shadow:0 1px 0 #3d1268,1px 0 0 #3d1268,-1px 0 0 #3d1268,0 -1px 0 #3d1268;max-width:78px;}
.sq-lt.un{background:radial-gradient(120% 120% at 50% 0%,#fff1b0,#ffc83a 55%,#e08a10);}
.sq-lt.un .lb{color:#3d1268;text-shadow:0 1px 0 rgba(255,255,255,.7);}
.sq-lt.un .tag{position:absolute;top:-8px;left:50%;transform:translateX(-50%);padding:1px 7px;border-radius:999px;background:#ff4fbf;color:#fff;font:900 8px Nunito,sans-serif;letter-spacing:.8px;white-space:nowrap;box-shadow:0 0 0 1.5px #3d1268;}
.sq-lt.lk{filter:saturate(.35) brightness(.78);}
.sq-lt.lk .ar > *{opacity:.85;}
.sq-lt.got{filter:saturate(.5) brightness(.85);}
.sq-lt.cl{animation:sqClaim 1.2s ease-in-out infinite;}
@keyframes sqClaim{0%,100%{transform:translateY(0);box-shadow:0 0 0 2px #3d1268,0 3px 0 2px #3d1268,0 0 6px 2px rgba(255,214,110,.5),inset 0 0 0 1.5px rgba(255,236,170,.6);}50%{transform:translateY(-4px);box-shadow:0 0 0 2px #3d1268,0 3px 0 2px #3d1268,0 0 18px 7px rgba(255,214,110,.95),inset 0 0 0 1.5px rgba(255,236,170,.6);}}
.sq-lt.sel{outline:3px solid #fff;outline-offset:3px;}
.sq-lt .st{position:absolute;right:-6px;top:-6px;width:22px;height:22px;border-radius:50%;display:flex;align-items:center;justify-content:center;box-shadow:0 0 0 2px #fff,0 0 0 3.5px #3d1268;}
.sq-lt .st.ok{background:#26a94e;}.sq-lt .st.ok svg{width:14px;height:14px;}
.sq-lt .st.lkb{background:#7a3ce0;}
.sq-lt .clm{position:absolute;bottom:-9px;left:50%;transform:translateX(-50%);padding:1px 8px;border-radius:999px;background:linear-gradient(180deg,#fff1b0,#ffd650 45%,#f6a524);color:#3d1268;font:900 9px Nunito,sans-serif;letter-spacing:.8px;box-shadow:0 0 0 1.5px #3d1268;}
.sq-lc.cur .sq-lm{transform:scale(1.18);box-shadow:0 0 0 2px #3d1268,0 2px 0 2px #3d1268,0 0 14px 5px rgba(255,214,110,.9);}
.sq-fly{position:absolute;width:26px;height:26px;z-index:95;pointer-events:none;transition:transform .75s cubic-bezier(.5,-0.2,.7,1),opacity .2s ease .62s;}
/* ---- Characters ---- */
.sq-cs{position:absolute;left:14px;top:60px;bottom:12px;width:278px;display:flex;flex-direction:column;align-items:center;}
.sq-cs .stage{position:relative;width:236px;height:200px;flex:none;}
.sq-cs .stage .rays{position:absolute;left:50%;top:52%;width:300px;height:300px;margin:-150px 0 0 -150px;border-radius:50%;background:repeating-conic-gradient(rgba(255,240,180,.45) 0deg 9deg,rgba(255,240,180,0) 9deg 22deg);-webkit-mask-image:radial-gradient(circle,#000 10%,rgba(0,0,0,0) 62%);mask-image:radial-gradient(circle,#000 10%,rgba(0,0,0,0) 62%);animation:sqSpin 14s linear infinite;opacity:.5;}
.sq-cs .stage .pad{position:absolute;left:50%;bottom:6px;width:170px;height:34px;margin-left:-85px;border-radius:50%;background:radial-gradient(closest-side,rgba(255,255,255,.75),rgba(255,236,170,.4) 55%,rgba(255,236,170,0));}
.sq-cs .stage .pad::after{content:'';position:absolute;left:30px;right:30px;top:9px;bottom:9px;border-radius:50%;background:radial-gradient(circle at 45% 35%,#f6efe4,#cbbca6);box-shadow:0 0 0 2px rgba(61,18,104,.5);}
.sq-cs .rig{position:absolute;left:50%;bottom:4px;width:196px;height:196px;margin-left:-98px;}
.sq-cs .rig.pop{animation:sqPop .55s cubic-bezier(.2,1.4,.4,1);}
.sq-cs .rig.sil{filter:saturate(.3) brightness(.78) drop-shadow(0 0 8px rgba(200,160,255,.8));}
@keyframes sqPop{0%{transform:scale(.55) translateY(20px);opacity:0;}100%{transform:none;opacity:1;}}
.sq-cs .np{margin-top:-2px;height:30px;padding:0 18px;display:flex;align-items:center;justify-content:center;cursor:default;position:relative;}
.sq-cs .np .mm-ft{margin:-6px 0 -5px;}
.sq-cs .np .pips{position:absolute;left:50%;top:-9px;transform:translateX(-50%);display:flex;gap:1px;}
.sq-cs .np .pips svg{width:12px;height:12px;filter:drop-shadow(0 1px 0 #3d1268);}
.sq-cs .chips{display:flex;flex-wrap:wrap;justify-content:center;gap:5px;margin-top:9px;}
.sq-cs .chips .sq-chip{cursor:default;height:21px;font-size:10px;}
.sq-cs .info{font:700 11px Nunito,sans-serif;color:#eadcff;margin-top:6px;min-height:14px;text-align:center;}
.sq-cs .act{margin-top:auto;width:236px;height:44px;border-radius:15px;display:flex;align-items:center;justify-content:center;gap:6px;font:700 15px Fredoka,sans-serif;}
.sq-cs .act.v-gold{animation:mmGlow 2.4s ease-in-out infinite;}
.sq-cs .act.done{cursor:default;}
.sq-cs .act > svg:not(.mm-ft){width:18px;height:18px;}
.sq-cg{position:absolute;left:304px;right:14px;top:60px;bottom:10px;display:flex;flex-direction:column;}
.sq-cf{display:flex;gap:6px;overflow-x:auto;padding:3px 3px 8px;scrollbar-width:none;flex:none;}
.sq-cf::-webkit-scrollbar{display:none;}
.sq-cgr{flex:1;overflow-y:auto;overflow-x:hidden;display:grid;grid-template-columns:repeat(auto-fill,minmax(76px,1fr));gap:9px 8px;padding:6px 6px 14px;scrollbar-width:none;align-content:start;}
.sq-cgr::-webkit-scrollbar{display:none;}
.sq-cc{position:relative;width:100%;aspect-ratio:1696/2528;border:0;padding:0;background:none;cursor:pointer;container-type:inline-size;transition:transform .15s;}
.sq-cc:active{transform:scale(.95);}
.sq-cc img{position:absolute;pointer-events:none;}
.sq-cc .nmT{position:absolute;display:flex;align-items:center;justify-content:center;text-align:center;font:700 9cqw/1 Fredoka,sans-serif;}
.sq-cc.no .art{filter:brightness(0) opacity(.62) drop-shadow(0 0 3px rgba(255,255,255,.35));}
.sq-cc.no .frm,.sq-cc.no .bg,.sq-cc.no .nmi{filter:saturate(.25) brightness(.8);}
.sq-cc .lk{position:absolute;right:6%;top:4%;width:24%;filter:drop-shadow(0 1px 1px rgba(0,0,0,.4));}
.sq-cc .lk svg{width:100%;height:auto;display:block;}
.sq-cc.pv::before{content:'';position:absolute;left:5%;right:5%;top:2%;bottom:1.5%;border-radius:10% / 7%;box-shadow:0 0 0 3px #fff,0 0 14px 4px rgba(255,255,255,.7);}
.sq-cc.me::after{content:'IN USE';position:absolute;left:50%;top:-6px;transform:translateX(-50%);padding:1px 7px;border-radius:999px;background:linear-gradient(180deg,#fff1b0,#ffd650 45%,#f6a524);color:#3d1268;font:900 8px Nunito,sans-serif;letter-spacing:.8px;box-shadow:0 0 0 1.5px #3d1268;white-space:nowrap;z-index:3;}
.sq-cc .nw{position:absolute;right:-4px;top:-4px;width:12px;height:12px;border-radius:50%;background:#ff4fbf;box-shadow:0 0 0 2px #fff;z-index:3;}
@media (prefers-reduced-motion: reduce){.sq-crest .sq-cin,.sq-crest .mm-crest,.sq-wing1,.sq-wing-1,.sq-cstar,.sq-sweep,.sq-rays{animation:none;}}
`;
    document.head.appendChild(s);
  }

  // ---------- 1. the level crest, alive ----------
  let ringShown = false;
  M.crest = function (L, crestFn) {
    addCss();
    const s0 = S(), xp = s0.xp, lv = (xp && xp.level) || L, id = 'sqcr' + (++UID);
    let svg = crestFn(lv);
    // wings: a wrapper each, flapping from the shoulder
    svg = svg.replace(/<g transform="translate\(60 0\) scale\((-?1) 1\) translate\(-60 0\)">([\s\S]*?)<\/g>/g, (m, s) => '<g class="sq-wing' + s + '">' + m + '</g>');
    // the star on top flips now and then
    svg = svg.replace('<path d="M60 4 ', '<g class="sq-cstar"><path d="M60 4 ').replace('<circle cx="60" cy="58" r="31"', '</g><circle cx="60" cy="58" r="31"');
    // the XP ring (in the gold band, over its studs) and a shine sweeping the medallion
    const f = xp && xp.need > 0 ? clamp(xp.into / xp.need, 0, 1) : 0, C = 2 * Math.PI * 27.2, a = (C * f).toFixed(1);
    const ring = xp ? '<circle cx="60" cy="58" r="27.2" fill="none" stroke="#5a1f86" stroke-opacity=".55" stroke-width="4.4"/>'
      + '<circle cx="60" cy="58" r="27.2" fill="none" stroke="url(#' + id + 'r)" stroke-width="4.4" stroke-linecap="round" transform="rotate(-90 60 58)" stroke-dasharray="' + a + ' ' + C.toFixed(1) + '">'
      + (ringShown ? '' : '<animate attributeName="stroke-dasharray" from="0 ' + C.toFixed(1) + '" to="' + a + ' ' + C.toFixed(1) + '" dur="1.4s" begin="0.3s" fill="freeze" calcMode="spline" keySplines=".2 .9 .25 1" keyTimes="0;1"/>') + '</circle>' : '';
    if (xp) ringShown = true;
    svg = svg.replace('<circle cx="60" cy="58" r="23"', ring + '<circle cx="60" cy="58" r="23"');
    const rib = 'url(#' + id + 'b)';
    svg = svg.replace(/<path d="M38 88 L60 80 L82 88 L78 98 L60 92 L42 98Z"[^>]*\/>/, '<path d="M36 86 L21 87.5 L26.5 92.5 L21 98 L36 96.5Z M84 86 L99 87.5 L93.5 92.5 L99 98 L84 96.5Z" fill="#d08a1a" stroke="#3d1268" stroke-width="1.8" stroke-linejoin="round"/>'
      + '<path d="M33 83.5 Q60 78 87 83.5 L87 97 Q60 91.5 33 97Z" fill="' + rib + '" stroke="#3d1268" stroke-width="2" stroke-linejoin="round"/><path d="M35.5 84.6 Q60 79.6 84.5 84.6" stroke="#fffbe8" stroke-width="0.9" fill="none" opacity=".85"/>')
      .replace(/<text x="60" y="95"([^>]*)font-size="8"([^>]*)>LEVEL<\/text>/, '<text x="60" y="92.2"$1font-size="8.6"$2>LEVEL</text>');
    svg = svg.replace('<path d="M36 86 L21', '<g clip-path="url(#' + id + 'c)"><g class="sq-sweep"><rect x="52" y="14" width="13" height="92" fill="#ffffff" opacity=".42" transform="rotate(22 60 58)"/></g></g><path d="M36 86 L21');
    svg = svg.replace('</defs>', '<linearGradient id="' + id + 'b" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff6c8"/><stop offset=".55" stop-color="#ffd85a"/><stop offset="1" stop-color="#f7b02e"/></linearGradient><clipPath id="' + id + 'c"><circle cx="60" cy="58" r="30"/></clipPath><linearGradient id="' + id + 'r" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ffc2ec"/><stop offset=".5" stop-color="#ff4fbf"/><stop offset="1" stop-color="#b8128a"/></linearGradient></defs>');
    const sparks = [[4, 18, 0], [96, 30, 0.9], [20, 70, 1.7], [88, 76, 0.45]].map(([l, t, d]) => '<i class="sq-spark" style="left:' + l + 'px;top:' + t + 'px;animation-delay:' + d + 's">' + SPARK + '</i>').join('');
    const n = (s0.road || []).filter(r => r.claimable).length;
    return '<div class="sq-cin">' + svg + '</div>' + sparks + (n ? '<i class="sq-badge">' + (n > 9 ? '9+' : n) + '</i>' : '');
  };

  // ---------- 2. the Game Park's picture (main-menu-scene.js svg(st) with st.park) ----------
  // viewBox 0 0 1000 460 like the house scene, so the two meet at the edge: the same sky, horizon (y ~ 238) and grass.
  const PAD = 160;   // where the creature stands (the house's pad is at 500, both at y 421)
  M.PAD = PAD;
  // the road's top and bottom edge where the park meets the house (park x 1000 = house x 0)
  const ROAD_T = 401, ROAD_B = 447;
  // stepping stones in the style of the house path's own (main-menu-scene.js path(): a shadow ellipse + a lighter stone),
  // sized like its nearest ones, one line along the road. Stone k sits at house x 440 - 62k: the house picture draws the
  // ones on its side, the park the rest (the two near the seam are drawn by both, in the same place)
  const stone = (X, k, c0, c1, mix) => { const y = 424 + (k % 2 ? 3 : -3), a = 23 + (k % 3) * 1.5, b = 5.8;
    return '<ellipse cx="' + r1(X) + '" cy="' + r1(y + 1.5) + '" rx="' + r1(a) + '" ry="' + b + '" fill="' + c1 + '"/><ellipse cx="' + r1(X) + '" cy="' + r1(y) + '" rx="' + r1(a) + '" ry="' + b + '" fill="' + mix(c0, '#ffffff', 0.35) + '" stroke="' + c1 + '" stroke-width="0.8"/>'; };
  const stones = (lo, hi, off, c0, c1, mix) => { let g = ''; for (let k = 0; k < 40; k++) { const X = 440 - 62 * k; if (X < lo - 40) break; if (X <= hi + 40) g += stone(X + off, k, c0, c1, mix); } return g; };
  // the house picture's half of the road: from the left edge to the creature's pad (main-menu-scene.js path(), patched in)
  // (straight, the same width all the way, running into the house's path where that path crosses its height: x 432 / 413)
  M.houseRoad = function (st, c0, c1, mix) {
    let g = '<path d="M-60 ' + ROAD_T + ' H452 V' + ROAD_B + ' H-60Z" fill="' + c0 + '"/>'
      + '<path d="M-60 ' + ROAD_T + ' H432 M-60 ' + ROAD_B + ' H413" stroke="' + c1 + '" stroke-width="1.6" fill="none"/>';
    return g + stones(-60, 440, 0, c0, c1, mix);
  };

  M.parkArt = function (st, P) {
    const OL = '#4a3a2e', mix = P.mix, rnd = P.rnd, snow = st.season === 'snowy', rain = st.season === 'rainy';
    const dim = c => rain && c[0] === '#' ? mix(c, '#7a8692', 0.18) : c;
    let g = '<defs><linearGradient id="mmPkStripe" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ff8fcf"/><stop offset="1" stop-color="#e2459e"/></linearGradient>'
      + '<radialGradient id="mmPkDoor" cx="0.5" cy="0.8" r="0.9"><stop offset="0" stop-color="#fff2b8"/><stop offset="0.5" stop-color="#ffb84a"/><stop offset="1" stop-color="#9a3a1a"/></radialGradient>'
      + '<linearGradient id="mmPkGold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fffbe0"/><stop offset="0.45" stop-color="#ffd24a"/><stop offset="1" stop-color="#e08a10"/></linearGradient></defs>';
    // its sky without the cloud and the lighthouse at its right edge (the edge would cut them where it meets the house)
    const sky = P.sky(st).replace(/<g><g transform="translate\(1000 60\)[\s\S]*?<\/g><animateTransform[^>]*\/><\/g>/, '').replace(/<g opacity="0\.75"><path d="M968 222[\s\S]*?<\/g>/, '');
    g += '<clipPath id="mmPkClip"><rect x="-200" y="-100" width="1200" height="700"/></clipPath><g clip-path="url(#mmPkClip)">';
    g += sky + P.ground(st);
    // the Ferris wheel, far back
    const wx = 690, wy = 122, R = 84;
    let wh = '<g transform="translate(' + wx + ' ' + wy + ')">';
    wh += '<path d="M-8 4 L-58 ' + (238 - wy) + ' M8 4 L58 ' + (238 - wy) + ' M-36 ' + (150 - wy + 80) + ' H36" stroke="' + dim('#e9e2f2') + '" stroke-width="5" stroke-linecap="round"/><path d="M-8 4 L-58 ' + (238 - wy) + ' M8 4 L58 ' + (238 - wy) + '" stroke="' + OL + '" stroke-width="1" opacity=".5"/>';
    let rot = '<g>';
    rot += '<circle r="' + R + '" fill="none" stroke="' + dim('#ffffff') + '" stroke-width="4"/><circle r="' + (R - 7) + '" fill="none" stroke="' + dim('#ffd0ec') + '" stroke-width="2"/>';
    for (let i = 0; i < 12; i++) { const a = i * Math.PI / 6; rot += '<path d="M0 0 L' + r1(Math.cos(a) * R) + ' ' + r1(Math.sin(a) * R) + '" stroke="' + dim('#f3ecff') + '" stroke-width="1.8"/>'; }
    const GC = ['#ff6fb5', '#ffd24a', '#5fd0ff', '#8f7bff', '#5fe0a0', '#ff9a4a', '#ff6fb5', '#5fd0ff', '#ffd24a', '#8f7bff'];
    for (let i = 0; i < 10; i++) {
      const a = i * Math.PI / 5, cx = Math.cos(a) * R, cy = Math.sin(a) * R;
      rot += '<g transform="translate(' + r1(cx) + ' ' + r1(cy) + ')"><g><animateTransform attributeName="transform" type="rotate" from="0" to="-360" dur="60s" repeatCount="indefinite"/>'
        + '<path d="M0 0 V6" stroke="' + OL + '" stroke-width="1.4"/><path d="M-9 6 H9 L7 17 Q0 21 -7 17Z" fill="' + dim(GC[i]) + '" stroke="' + OL + '" stroke-width="1.4"/><path d="M-6 9 H6" stroke="#ffffff" stroke-width="1.6" opacity=".7"/></g></g>';
      rot += '<circle cx="' + r1(cx) + '" cy="' + r1(cy) + '" r="2.4" fill="#fff7c8"><animate attributeName="opacity" values="1;.3;1" dur="' + (1.2 + (i % 3) * 0.4) + 's" repeatCount="indefinite"/></circle>';
    }
    rot += '<circle r="9" fill="url(#mmPkGold)" stroke="' + OL + '" stroke-width="1.6"/><animateTransform attributeName="transform" type="rotate" from="0" to="360" dur="60s" repeatCount="indefinite"/></g>';
    wh += rot + '</g>';
    g += '<g opacity="' + (rain ? 0.82 : 0.95) + '">' + wh + '</g>';
    // trees behind
    g += P.tree(352, 288, 0.8, st, 41) + P.tree(905, 280, 0.85, st, 42, { fruit: true }) + P.tree(560, 276, 0.6, st, 43);
    // string lights: tent top -> pole -> off to the right
    const pole = (x, y0, y1) => '<path d="M' + x + ' ' + y0 + ' V' + y1 + '" stroke="' + OL + '" stroke-width="5" stroke-linecap="round"/><path d="M' + x + ' ' + y0 + ' V' + y1 + '" stroke="#c98a50" stroke-width="3" stroke-linecap="round"/><circle cx="' + x + '" cy="' + y0 + '" r="4" fill="url(#mmPkGold)" stroke="' + OL + '" stroke-width="1.2"/>';
    const lights = (x0, y0, x1, y1, sag, seed) => {
      let h = '<path d="M' + x0 + ' ' + y0 + ' Q' + r1((x0 + x1) / 2) + ' ' + r1((y0 + y1) / 2 + sag) + ' ' + x1 + ' ' + y1 + '" stroke="#3a2a2e" stroke-width="1.2" fill="none"/>';
      const n = Math.round(Math.abs(x1 - x0) / 20), BC = ['#ffe680', '#ff8fcf', '#8fe4ff', '#b6ff8f', '#ffb86a'];
      for (let i = 1; i < n; i++) {
        const t = i / n, x = (1 - t) * (1 - t) * x0 + 2 * (1 - t) * t * (x0 + x1) / 2 + t * t * x1, y = (1 - t) * (1 - t) * y0 + 2 * (1 - t) * t * ((y0 + y1) / 2 + sag) + t * t * y1, c = BC[(i + seed) % 5];
        h += '<circle cx="' + r1(x) + '" cy="' + r1(y + 4) + '" r="3.2" fill="' + c + '" stroke="' + OL + '" stroke-width=".7"><animate attributeName="opacity" values="1;.35;1" dur="' + r1(1 + rnd(i, seed) * 1.4) + 's" begin="' + r1(rnd(i, seed + 3)) + 's" repeatCount="indefinite"/></circle>'
          + '<circle cx="' + r1(x) + '" cy="' + r1(y + 4) + '" r="7" fill="' + c + '" opacity=".25"/>';
      }
      return h;
    };
    // the kiosk (popcorn & tickets)
    const kx = 520, ky = 300;
    g += '<g transform="translate(' + kx + ' ' + ky + ')"><ellipse cx="2" cy="1" rx="44" ry="5" fill="#1d2a16" opacity=".2"/>'
      + '<rect x="-34" y="-50" width="68" height="50" fill="' + dim('#fff4e2') + '" stroke="' + OL + '" stroke-width="1.6"/><rect x="-34" y="-22" width="68" height="22" fill="' + dim('#7fc4ff') + '" stroke="' + OL + '" stroke-width="1.4"/>'
      + '<path d="M-26 -14 H26" stroke="#ffffff" stroke-width="2" opacity=".6"/><rect x="-24" y="-46" width="48" height="20" rx="3" fill="#3a1e4e"/><text x="0" y="-32" text-anchor="middle" font-family="Fredoka,sans-serif" font-weight="700" font-size="11" fill="#ffd24a">POP!</text>'
      + [0, 1, 2, 3, 4, 5].map(i => '<path d="M' + (-40 + i * 13.4) + ' -50 h13.4 l-1 12 q-5.7 6 -11.4 0Z" fill="' + dim(i % 2 ? '#ffffff' : '#ff6f8a') + '" stroke="' + OL + '" stroke-width="1.2"/>').join('')
      + '<path d="M-42 -50 Q0 -64 42 -50" fill="' + dim('#ff6f8a') + '" stroke="' + OL + '" stroke-width="1.4"/>'
      + [[-14, -58], [-4, -61], [6, -60], [15, -57], [1, -66]].map(([x, y]) => '<circle cx="' + x + '" cy="' + y + '" r="4.2" fill="#fffbea" stroke="#e8c88a" stroke-width=".8"/>').join('') + '</g>';
    // the balloon cart
    const bx = 860, by = 318;
    let bl = '<g transform="translate(' + bx + ' ' + by + ')"><ellipse cx="0" cy="2" rx="30" ry="4.5" fill="#1d2a16" opacity=".2"/><rect x="-22" y="-24" width="44" height="22" rx="4" fill="' + dim('#8f7bff') + '" stroke="' + OL + '" stroke-width="1.5"/><circle cx="-13" cy="0" r="5" fill="#3a2a2e"/><circle cx="13" cy="0" r="5" fill="#3a2a2e"/>';
    const BB = [['#ff6fb5', -20, -86], ['#ffd24a', -4, -98], ['#5fd0ff', 12, -88], ['#5fe0a0', 2, -76], ['#ff9a4a', -12, -70]];
    BB.forEach(([c, x, y], i) => {
      bl += '<g><animateTransform attributeName="transform" type="translate" values="0 0;' + (i % 2 ? 2 : -2) + ' -4;0 0" dur="' + (2.6 + i * 0.4) + 's" repeatCount="indefinite"/><path d="M0 -24 Q' + r1(x * 0.5) + ' ' + r1((y - 24) / 2) + ' ' + x + ' ' + (y + 13) + '" stroke="#5a4a5a" stroke-width=".8" fill="none"/>'
        + '<ellipse cx="' + x + '" cy="' + y + '" rx="11" ry="13" fill="' + dim(c) + '" stroke="' + OL + '" stroke-width="1.3"/><ellipse cx="' + (x - 4) + '" cy="' + (y - 5) + '" rx="3" ry="4.5" fill="#ffffff" opacity=".55"/><path d="M' + (x - 2) + ' ' + (y + 13) + ' l2 3 l2 -3Z" fill="' + dim(c) + '"/></g>';
    });
    g += bl + '</g>';
    // the big top, behind where the creature stands, its marquee above his head
    const tx = 150, tb = 304, tw = 128, tt = 116, tv = 206;
    let tent = '<g><ellipse cx="' + (tx + 4) + '" cy="' + (tb + 2) + '" rx="' + (tw + 12) + '" ry="9" fill="#1d2a16" opacity=".22"/>';
    // walls
    for (let i = 0; i < 8; i++) { const x0 = tx - tw + i * (2 * tw / 8); tent += '<rect x="' + r1(x0) + '" y="' + tv + '" width="' + r1(2 * tw / 8 + 0.4) + '" height="' + (tb - tv) + '" fill="' + dim(i % 2 ? '#fff4e6' : '#ff7fbf') + '"/>'; }
    tent += '<rect x="' + (tx - tw) + '" y="' + tv + '" width="' + 2 * tw + '" height="' + (tb - tv) + '" fill="none" stroke="' + OL + '" stroke-width="2"/>';
    // the doorway, lit
    tent += '<path d="M' + (tx - 34) + ' ' + tb + ' V' + (tv + 40) + ' Q' + tx + ' ' + (tv + 6) + ' ' + (tx + 34) + ' ' + (tv + 40) + ' V' + tb + 'Z" fill="url(#mmPkDoor)" stroke="' + OL + '" stroke-width="2"/>'
      + '<path d="M' + (tx - 34) + ' ' + (tv + 40) + ' Q' + (tx - 14) + ' ' + (tv + 50) + ' ' + (tx - 22) + ' ' + tb + ' H' + (tx - 34) + 'Z M' + (tx + 34) + ' ' + (tv + 40) + ' Q' + (tx + 14) + ' ' + (tv + 50) + ' ' + (tx + 22) + ' ' + tb + ' H' + (tx + 34) + 'Z" fill="' + dim('#d93a86') + '" stroke="' + OL + '" stroke-width="1.4"/>';
    // canopy: alternating stripes from the peak
    const n = 9;
    for (let i = 0; i < n; i++) {
      const a0 = tx - tw - 8 + i * (2 * (tw + 8) / n), a1 = a0 + 2 * (tw + 8) / n;
      tent += '<path d="M' + tx + ' ' + tt + ' L' + r1(a0) + ' ' + tv + ' L' + r1(a1) + ' ' + tv + 'Z" fill="' + dim(i % 2 ? '#fff4e6' : 'url(#mmPkStripe)') + '"/>';
    }
    tent += '<path d="M' + tx + ' ' + tt + ' L' + (tx - tw - 8) + ' ' + tv + ' H' + (tx + tw + 8) + 'Z" fill="none" stroke="' + OL + '" stroke-width="2" stroke-linejoin="round"/>';
    // scalloped valance
    let sc = '';
    for (let i = 0; i < 12; i++) { const x0 = tx - tw - 8 + i * (2 * (tw + 8) / 12), w = 2 * (tw + 8) / 12; sc += '<path d="M' + r1(x0) + ' ' + tv + ' h' + r1(w) + ' v4 q' + r1(-w / 2) + ' 12 ' + r1(-w) + ' 0Z" fill="' + dim(i % 2 ? '#ffd24a' : '#ff5fa8') + '" stroke="' + OL + '" stroke-width="1.3"/>'; }
    tent += sc;
    // flag on top
    tent += '<path d="M' + tx + ' ' + tt + ' V' + (tt - 26) + '" stroke="' + OL + '" stroke-width="2.4"/><circle cx="' + tx + '" cy="' + (tt - 27) + '" r="3" fill="url(#mmPkGold)" stroke="' + OL + '" stroke-width="1"/>'
      + '<path d="M' + tx + ' ' + (tt - 24) + ' l20 5 l-20 6Z" fill="' + dim('#ffd24a') + '" stroke="' + OL + '" stroke-width="1.2"><animateTransform attributeName="transform" type="skewY" values="0;-8;0;6;0" dur="1.6s" repeatCount="indefinite" additive="sum"/></path>';
    // marquee sign on the canopy: GAME PARK with bulbs
    const mw = 112, mh = 30, mx = tx - mw / 2, my = 150;
    tent += '<rect x="' + (mx - 2) + '" y="' + (my + 2) + '" width="' + (mw + 4) + '" height="' + mh + '" rx="8" fill="#2a0a4e" opacity=".35"/>'
      + '<rect x="' + mx + '" y="' + my + '" width="' + mw + '" height="' + mh + '" rx="8" fill="#5a1cb8" stroke="' + OL + '" stroke-width="2"/><rect x="' + (mx + 4) + '" y="' + (my + 4) + '" width="' + (mw - 8) + '" height="' + (mh - 8) + '" rx="5" fill="none" stroke="#ffd24a" stroke-width="1.4"/>';
    for (let i = 0; i < 14; i++) {
      const t = i / 14, per = 2 * (mw + mh), d = t * per; let x, y;
      if (d < mw) { x = mx + d; y = my; } else if (d < mw + mh) { x = mx + mw; y = my + d - mw; } else if (d < 2 * mw + mh) { x = mx + mw - (d - mw - mh); y = my + mh; } else { x = mx; y = my + mh - (d - 2 * mw - mh); }
      tent += '<circle cx="' + r1(x) + '" cy="' + r1(y) + '" r="2.3" fill="#fff6b0"><animate attributeName="fill" values="#fff6b0;#ff9ad6;#fff6b0" dur="1.4s" begin="' + r1(i * 0.1) + 's" repeatCount="indefinite"/></circle>';
    }
    tent += '<text x="' + tx + '" y="' + (my + 21) + '" text-anchor="middle" font-family="Fredoka,Nunito,sans-serif" font-weight="700" font-size="15.5" letter-spacing="1" fill="#3d1268" stroke="#3d1268" stroke-width="4" stroke-linejoin="round">GAME PARK</text>'
      + '<text x="' + tx + '" y="' + (my + 20) + '" text-anchor="middle" font-family="Fredoka,Nunito,sans-serif" font-weight="700" font-size="15.5" letter-spacing="1" fill="url(#mmPkGold)">GAME PARK</text>';
    if (snow) tent += '<path d="M' + tx + ' ' + (tt + 2) + ' L' + (tx - 40) + ' ' + (tt + 30) + ' Q' + tx + ' ' + (tt + 22) + ' ' + (tx + 40) + ' ' + (tt + 30) + 'Z" fill="#ffffff" opacity=".9"/>';
    tent += '</g>';
    g += tent;
    g += pole(392, 150, 302) + pole(760, 160, 306) + pole(948, 168, 318);
    g += lights(tx, tt - 2, 392, 152, 26, 0) + lights(392, 152, 760, 162, 30, 2) + lights(760, 162, 948, 170, 22, 4);
    // bunting low across the middle
    let bt = '<path d="M392 186 Q576 214 760 192" stroke="#3a2a2e" stroke-width="1" fill="none"/>';
    for (let i = 1; i < 16; i++) { const t = i / 16, x = (1 - t) * (1 - t) * 392 + 2 * (1 - t) * t * 576 + t * t * 760, y = (1 - t) * (1 - t) * 186 + 2 * (1 - t) * t * 214 + t * t * 192; bt += '<path d="M' + r1(x - 5) + ' ' + r1(y) + ' h10 l-5 10Z" fill="' + dim(['#ff6fb5', '#ffd24a', '#5fd0ff', '#5fe0a0'][i % 4]) + '" stroke="' + OL + '" stroke-width=".7"/>'; }
    g += bt;
    // bushes along the back
    [[300, 318, 1], [450, 322, 0.9], [610, 320, 0.95], [700, 326, 1.1], [960, 330, 1]].forEach(([x, y, s], i) => { g += P.bush(x, y, s, st, i + 51); });
    // the cobbled road along the front, from the creature's pad to the right edge: there it carries on, at the same height, into
    // the road drawn into the house picture (SquishMenu.houseRoad), so the park and the house are joined by one road
    const c0 = snow ? '#e8ecef' : mix(st.P.sand, '#cdbb98', 0.4), c1 = mix(c0, '#7a6a52', 0.25);
    g += '<path d="M-60 ' + ROAD_T + ' H1060 V' + ROAD_B + ' H-60Z" fill="' + c0 + '"/>'
      + '<path d="M-60 ' + ROAD_T + ' H1060 M-60 ' + ROAD_B + ' H1060" stroke="' + c1 + '" stroke-width="1.6" fill="none"/>';
    g += stones(-1060, 60, 1000, c0, c1, mix);
    // front trees and bushes frame it
    g += P.tree(14, 380, 1.4, st, 44, { fruit: true });   // (behind the road)
    g += P.bush(300, 474, 1.4, st, 56) + P.bush(640, 478, 1.3, st, 57) + P.bush(880, 472, 1.2, st, 58);   // (below the road, not on it)
    // (no vignette: the park sits right next to the house picture, and two vignettes would show a seam where they meet)
    g += P.weather(st) + '</g>';
    // on the seam with the house (drawn past the edge, over the house's own edge): a big tree and bushes, so the two pictures meet behind them
    // (the road runs between them)
    g += P.tree(1002, 396, 1.6, st, 46, { fruit: true }) + P.bush(964, 400, 1.3, st, 59) + P.bush(1044, 398, 1.25, st, 60) + P.bush(976, 478, 1.5, st, 61) + P.bush(1044, 480, 1.6, st, 62);
    return g;
  };

  // ---------- 3. PLAY: the walk to the Game Park and the games carousel ----------
  let park = null; // { x, dx, ui, cur, idle, leaving }
  M.inPark = () => !!park;

  function parkLayer(x) {
    let L = x.world.querySelector('.sq-parkL');
    const key = (x.st && x.st.season) + ':' + (x.st && x.st.L);
    if (!L) { L = document.createElement('div'); L.className = 'sq-parkL'; L.style.cssText = 'position:absolute;top:0;left:-100%;width:100%;height:100%;pointer-events:none;'; x.world.insertBefore(L, x.scene.nextSibling); }
    if (L.dataset.k !== key) { L.innerHTML = window.MainMenuScene.svg(Object.assign({}, x.st, { park: true })); L.dataset.k = key; const sv = L.firstChild; if (sv && sv.style) sv.style.overflow = 'visible'; }
    return L;
  }

  // walk the creature for `ms` while place(t) moves him and the camera; spin = the way he faces (-90 left, 90 right)
  function walk(x, ms, place, spin) {
    const ctrl = x.ctrl, I = ctrl && ctrl._I, c = x.cre(), key = c && c.key, HW = window.HouseWalk;
    const E = {}, ws = {}, prev = I ? I.ext : null;
    if (I && HW) I.ext = E; else x.stage.classList.add('walk');
    let last = performance.now();
    return tween(ms, (e, t) => {
      const now = performance.now(), dt = Math.min(0.05, (now - last) / 1000); last = now;
      place(t);
      if (I && HW) {
        for (const k in E) E[k] = 0;
        const w = Math.min(1, t / 0.08, (1 - t) / 0.1);
        try { HW.step(key, ws, E, Math.max(0, w), dt, 1, spin); } catch (err) { /* no gait for him */ }
      }
    }).then(() => { if (I && HW) { for (const k in E) E[k] = 0; I.ext = prev; } x.stage.classList.remove('walk'); });
  }

  // the house picture darkens its own edges; while the park is beside it that edge would show as a seam
  function vignette(x, on) { const v = x.scene.querySelector('rect[fill$="Vig)"]'); if (v) { v.style.transition = 'opacity .45s ease'; v.style.opacity = on ? '' : '0'; } }

  async function enterPark(x, instant) {
    if (park || x.busy || x.inside || !x.geo) return false;
    const g = x.geo, W = g.W;
    x.busy = true;
    park = { x, cur: 0 };
    x.closePage(); x.ov.querySelectorAll('.mm-pick').forEach(e => e.remove());
    x.fade(false);
    if (x.sh) x.sh.stop();
    parkLayer(x); vignette(x, false);
    const ctrl = x.ctrl, I = ctrl && ctrl._I, X0 = g.X(500), X1 = g.X(PAD);
    park.dx = X1 - W - X0;
    if (instant) {
      x.world.style.transform = 'translateX(' + W + 'px)'; x.stage.style.transform = 'translateX(' + park.dx + 'px)';
      if (ctrl) ctrl.play('happy');
      showGames(x, true); return true;
    }
    if (ctrl) ctrl.play('idle');
    sfx('whoosh');
    await tween(380, e => { if (I) I.spinOff = -90 * e; }, EIO);
    // the creature heads left across the screen while the camera pans after him (the park slides in from the left)
    await walk(x, 2300, t => {
      const scr = X0 + (X1 - X0) * EOUT(t), cam = W * EIOC(clamp((t - 0.06) / 0.94, 0, 1));
      x.world.style.transform = 'translateX(' + cam.toFixed(1) + 'px)';
      x.stage.style.transform = 'translateX(' + (scr - X0 - cam).toFixed(1) + 'px)';
    }, -90);
    await tween(380, e => { if (I) I.spinOff = -90 * (1 - e); }, EIO);
    if (I) I.spinOff = 0;
    if (!park) return true;
    if (x.ctrl) x.ctrl.play('happy');
    showGames(x, false);
    return true;
  }

  async function leavePark() {
    if (!park || park.leaving) return; const x = park.x; park.leaving = true;
    clearInterval(park.idle);
    const ui = park.ui; if (ui) { stopVideo(ui); ui.classList.remove('in'); setTimeout(() => ui.remove(), 420); }
    const g = x.geo, W = g.W, X0 = g.X(500), X1 = g.X(PAD), ctrl = x.ctrl, I = ctrl && ctrl._I;
    if (ctrl) ctrl.play('idle');
    sfx('whoosh');
    await tween(340, e => { if (I) I.spinOff = 90 * e; }, EIO);
    await walk(x, 2100, t => {
      const scr = X1 + (X0 - X1) * EIO(t), cam = W * (1 - EIOC(clamp(t / 0.94, 0, 1)));
      x.world.style.transform = 'translateX(' + cam.toFixed(1) + 'px)';
      x.stage.style.transform = 'translateX(' + (scr - X0 - cam).toFixed(1) + 'px)';
    }, 90);
    await tween(340, e => { if (I) I.spinOff = 90 * (1 - e); }, EIO);
    if (I) I.spinOff = 0;
    x.world.style.transform = ''; x.stage.style.transform = '';
    vignette(x, true);
    const pl = x.world.querySelector('.sq-parkL'); if (pl) pl.remove();   // its seam tree would show at the house's left edge
    park = null;
    x.busy = false; x.hudDraw(); x.fade(true);
    if (x.sh && x.ctrl) x.sh.attach(x.ctrl);
    if (x.ctrl) x.ctrl.play('happy');
  }

  const LASTG = 'squishsquad.park.v1';
  // a game's clip: app/previews/<id>.mp4 + its first frame <id>.jpg (tools/previews/make.mjs)
  // (Squish Time's clip is Frybo being squished: another creature plays its own squish on the card instead)
  const PREVIEWS = { squish: 1, squishdoku: 1, sort: 1, stack: 1, hop: 1 }, SQUISH_CLIP_KEY = 'frybo';
  // each game's line on its card (the games' own words)
  const DESC = { squishdoku: 'One creature in every row, column and patch. No two may touch!', sort: 'Remember who sits where. The doors shut, it shakes… sort them back!',
    stack: 'Move them one at a time to match the goal. Big never sits on small!', hop: 'Hop from cushion to cushion. Clear them all, then hop onto the ★.' };
  const shade = (c, k) => { const n = parseInt(c.slice(1), 16), f = v => Math.max(0, Math.min(255, Math.round(k < 0 ? v * (1 + k) : v + (255 - v) * k))); return '#' + [n >> 16 & 255, n >> 8 & 255, n & 255].map(v => f(v).toString(16).padStart(2, '0')).join(''); };

  function showGames(x, instant) {
    const G = x.games(), k = x.k, W = x.geo.W, c = x.cre(), Q = x.Q;
    let cur = 0; try { const id = JSON.parse(localStorage.getItem(LASTG) || 'null'); const i = G.findIndex(g => g.id === id); if (i >= 0) cur = i; } catch (e) { /* none */ }
    park.cur = cur;
    const nOpen = G.filter(g => x.open(g)).length;
    // the card fills the gap between the two arrows (each 40 wide, 10 / 12 in from the sides, ~8 clear of the card)
    const pgW = W / k, carL = Math.round(Math.max(200, pgW * 0.29)), CW = Math.max(300, Math.round(pgW - carL - 118));
    const card = (g, i) => {
      const op = x.open(g), col = g.color || '#ff8a3d', pv = PREVIEWS[g.id] && !(g.id === 'squish' && (!c || c.key !== SQUISH_CLIP_KEY));
      const best = g.id === 'squish' ? 'Playing as ' + (c ? c.name : '') : g.best != null ? g.best : 'New!';
      const desc = g.id === 'squish' ? 'Squeeze, poke and stretch ' + (c ? c.name : 'your character') + '. Every squish pays coins!' : (DESC[g.id] || '');
      return '<div class="sq-slot" data-i="' + i + '" style="width:' + CW + 'px;margin-left:' + (-CW / 2) + 'px"><div class="sq-gc mm-p' + (op ? '' : ' lock') + '" style="--gc:' + col + ';--gcl:' + shade(col, 0.35) + ';--gcd:' + shade(col, -0.42) + ';transition-delay:' + (0.05 + Math.abs(i - cur) * 0.07).toFixed(2) + 's">'
        + '<div class="ttl">' + ft(g.name, 'white', 22) + '</div>'
        + '<div class="win">' + (pv ? '<img alt="" src="app/previews/' + g.id + '.jpg" data-fb="' + i + '">' : g.id === 'squish' ? squishCard(c, Q) : (g.icon || '')) + (op ? '' : '<div class="lockv">' + LOCK(44) + '</div>') + '</div>'
        + '<div class="info"><div class="txt">' + (desc ? '<div class="ds">' + esc(desc) + '</div>' : '') + '<div class="best">' + esc(best) + '</div></div>'
        + '<button class="go mm-p tip v-gold" data-go="' + i + '">' + ft(op ? 'PLAY' : 'LOCKED', 'whiteB', 22) + '</button></div></div></div>';
    };
    const ui = document.createElement('div'); ui.className = 'sq-pk';
    ui.innerHTML = '<div class="sq-pkdim"></div><div class="mm-pg" style="transform:scale(' + k + ');width:' + (W / k) + 'px">'
      + '<div class="mm-pgh"><button class="mm-orb" data-x aria-label="Back to the house">' + ic(BACK, '#fff', 3) + '</button>' + ft('Game Park', 'gold', 30)
      + '<div class="s">' + nOpen + ' games · ' + esc(c ? c.name : 'your creature') + ' plays every one</div><div class="mm-sp"></div>' + pill(coinImg(), Q ? Q.wallet().coins : 0) + '</div>'
      + '<div class="sq-car" style="left:' + carL + 'px"><div class="sq-rays"></div><div class="sq-halo"></div>' + G.map(card).join('')
      + '<button class="mm-orb sq-arr l" aria-label="Previous game">' + ic(LEFT, '#fff', 3) + '</button><button class="mm-orb sq-arr r" aria-label="Next game">' + ic(RIGHT, '#fff', 3) + '</button>'
      + '<div class="sq-pos">' + G.map(() => '<i></i>').join('') + '</div></div></div>';
    x.ov.appendChild(ui); x.fitFT(ui); park.ui = ui; fillIcons(ui);
    ui.querySelectorAll('img[data-fb]').forEach(im => { im.onerror = () => {
      const g = G[+im.dataset.fb], T = window.SQ_THUMBS || {}, s = c && Q ? Q.src(c.id) : null;
      if (g.id === 'squish' && s) { im.onerror = null; im.src = T[s] || s; im.style.objectFit = 'contain'; im.style.background = 'radial-gradient(circle at 50% 40%,#ffe3f6,#ff8fd6)'; return; }
      const d = document.createElement('div'); d.innerHTML = g.icon || ''; im.replaceWith(d.firstChild || d); }; });
    const slots = [...ui.querySelectorAll('.sq-slot')], car = ui.querySelector('.sq-car');
    const place = () => {
      slots.forEach((s, i) => {
        const d = i - park.cur, a = Math.abs(d);
        const tx = d === 0 ? 0 : Math.sign(d) * Math.round(CW * 0.45 + a * 40), sc = d === 0 ? 1 : 0.8;
        s.style.transform = 'translate(' + tx + 'px,0) scale(' + sc + ')';
        s.style.opacity = d === 0 ? 1 : 0;
        s.style.zIndex = 50 - a; s.style.pointerEvents = d === 0 ? '' : 'none';
        s.classList.toggle('c', d === 0);
      });
      ui.querySelector('.sq-arr.l').classList.toggle('off', park.cur === 0);
      ui.querySelector('.sq-arr.r').classList.toggle('off', park.cur === G.length - 1);
      ui.querySelectorAll('.sq-pos i').forEach((e, i) => e.classList.toggle('on', i === park.cur));
      playVideo(ui, G, park.cur);
    };
    const go = i => { i = clamp(i, 0, G.length - 1); if (i === park.cur) return; park.cur = i; sfx('whoosh'); place(); try { localStorage.setItem(LASTG, JSON.stringify(G[i].id)); } catch (e) { /* full */ } };
    park.go = go;
    place();
    ui.querySelector('[data-x]').onclick = () => leavePark();
    ui.querySelector('.sq-arr.l').onclick = () => go(park.cur - 1);
    ui.querySelector('.sq-arr.r').onclick = () => go(park.cur + 1);
    ui.querySelectorAll('.sq-pos i').forEach((e, i) => { e.onclick = () => go(i); });
    let downX = null, downY = 0, swiped = 0;
    car.addEventListener('pointerdown', e => { downX = e.clientX; downY = e.clientY; });
    car.addEventListener('pointerup', e => { if (downX == null) return; const dx = (e.clientX - downX) / k, dy = (e.clientY - downY) / k; downX = null; if (Math.abs(dx) > 36 && Math.abs(dx) > Math.abs(dy)) { swiped = Date.now(); go(park.cur + (dx < 0 ? 1 : -1)); } });
    car.addEventListener('pointercancel', () => { downX = null; });
    slots.forEach((s, i) => s.addEventListener('click', e => {
      if (Date.now() - swiped < 250) return;
      if (i !== park.cur) { go(i); return; }
      if (e.target.closest('[data-go]') || e.target.closest('.win')) playAt(x, G[i], s);
    }));
    // (a timer too: right after a cold start the WebView can hold animation frames back for seconds)
    requestAnimationFrame(() => requestAnimationFrame(() => ui.classList.add('in')));
    setTimeout(() => ui.classList.add('in'), 120);
    if (instant) ui.classList.add('in');
    // the creature stays lively while you choose
    park.idle = setInterval(() => { const ct = x.ctrl, I = ct && ct._I; if (!park || park.leaving || !ct) return; const L = allowed(I, ['happy', 'look', 'love', 'cheer', 'wave', 'hop']); if (L.length) ct.play(L[Math.floor(Math.random() * L.length)]); }, 6500);
  }

  // Squish Time for a creature without its own clip: its picture squished and stretched like in the game, with squish lines
  function squishCard(c, Q) {
    const T = window.SQ_THUMBS || {}, s = c && Q ? Q.src(c.id) : null;
    return '<div class="sq-sqz"><i class="l"></i><i class="r"></i>' + (s ? '<img alt="" src="' + (T[s] || s) + '">' : '') + '</div>';
  }

  function playVideo(ui, G, i) {
    ui.querySelectorAll('.sq-slot').forEach((s, j) => {
      const win = s.querySelector('.win'), v = win.querySelector('video');
      if (j === i && PREVIEWS[G[j].id] && win.querySelector('img[data-fb]')) {
        if (v) { v.play().catch(() => {}); return; }
        const vid = document.createElement('video');
        vid.muted = true; vid.loop = true; vid.playsInline = true; vid.autoplay = true; vid.preload = 'auto';
        vid.setAttribute('muted', ''); vid.setAttribute('playsinline', ''); vid.setAttribute('aria-hidden', 'true');
        vid.poster = 'app/previews/' + G[j].id + '.jpg'; vid.src = 'app/previews/' + G[j].id + '.mp4';
        vid.onerror = () => vid.remove();
        win.appendChild(vid); vid.play().catch(() => {});
      } else if (v) { v.pause(); v.removeAttribute('src'); try { v.load(); } catch (e) { /* gone */ } v.remove(); }
    });
  }
  function stopVideo(ui) { ui.querySelectorAll('video').forEach(v => { v.pause(); v.removeAttribute('src'); try { v.load(); } catch (e) { /* gone */ } v.remove(); }); }

  async function playAt(x, g, slot) {
    if (!park || park.leaving || park.starting) return;
    if (!x.open(g)) { toast(park.ui, g.name + ' isn\'t open yet'); return; }
    park.starting = true; sfx('tap');
    const ct = x.ctrl; if (ct) { const L = allowed(ct._I, ['cheer', 'happy']); if (L.length) ct.play(L[0]); }
    const gc = slot.querySelector('.sq-gc'); gc.style.transition = 'transform .18s ease'; gc.style.transform = 'scale(.94)'; setTimeout(() => { gc.style.transform = 'scale(1.04)'; }, 180);
    try { localStorage.setItem(LASTG, JSON.stringify(g.id)); } catch (e) { /* full */ }
    await wait(420);
    park.starting = false;
    x.playGame(g);
  }

  function toast(host, t, cls) {
    if (!host) return; const pg = host.querySelector('.mm-pg') || host;
    pg.querySelectorAll('.sq-toast').forEach(e => e.remove());
    const d = document.createElement('div'); d.className = 'sq-toast mm-p tip v-gold' + (cls ? ' ' + cls : ''); d.textContent = t; pg.appendChild(d); setTimeout(() => d.remove(), 2400);
  }

  M.play = function (x) { addCss(); if (!x || !window.MainMenuScene) return false; enterPark(x, false); return true; };
  // the app opened the menu on the park (back from a game started there): straight in, no walk
  M.mounted = function (x) {
    addCss();
    // (the app's start data can arrive after this page's first scripts: app/bridge.js calls this again when it does)
    M._x = x;
    if (!App || App.screen !== 'park' || App._parkDone) return;
    App._parkDone = true; App.screen = null;
    // (only the layout is waited for: on a cold start the creature's rig can take seconds; it appears in the park when ready)
    x.fade(false);
    let n = 0; const go = () => { if (x.geo) enterPark(x, true).then(ok => { if (!ok) x.fade(true); }); else if (n++ < 300) setTimeout(go, 60); else x.fade(true); };
    go();
  };
  // Android back while in the park walks home (the back orb has data-x, so SquishApp.back finds it)

  // ---------- art for prizes and unlocks ----------
  const CHEST = { common: ['#8fd2ff', '#2f7fd6'], rare: ['#7ee8b4', '#16a86a'], epic: ['#c89bff', '#7a2ad8'], legendary: ['#ffc65a', '#d9772f'], set: ['#ff9ad6', '#8f5cf6'] };
  const CHEST_NAME = { common: 'Common', rare: 'Rare', epic: 'Epic', legendary: 'Legendary', set: 'Rainbow' };
  function chestArt(t) {
    const c = CHEST[t] || CHEST.common, id = 'sqch' + (++UID), rb = t === 'set';
    return '<svg viewBox="0 0 64 56"><defs><linearGradient id="' + id + '" x1="0" y1="0" x2="' + (rb ? 1 : 0) + '" y2="1">' + (rb ? '<stop offset="0" stop-color="#ff9ad6"/><stop offset=".33" stop-color="#ffe27a"/><stop offset=".66" stop-color="#7fe9b8"/><stop offset="1" stop-color="#8fb8ff"/>' : '<stop offset="0" stop-color="' + c[0] + '"/><stop offset="1" stop-color="' + c[1] + '"/>') + '</linearGradient></defs>'
      + '<ellipse cx="32" cy="52" rx="24" ry="3.5" fill="#000" opacity=".25"/><rect x="8" y="24" width="48" height="26" rx="5" fill="url(#' + id + ')" stroke="#3d1268" stroke-width="2.4"/>'
      + '<path d="M8 26 Q8 8 32 8 Q56 8 56 26Z" fill="url(#' + id + ')" stroke="#3d1268" stroke-width="2.4" stroke-linejoin="round"/><path d="M14 22 Q16 13 30 12" stroke="#fff" stroke-width="2.4" fill="none" opacity=".55" stroke-linecap="round"/>'
      + '<rect x="6" y="22" width="52" height="7" rx="3" fill="#ffd24a" stroke="#3d1268" stroke-width="2"/><rect x="27" y="25" width="10" height="13" rx="2.5" fill="#ffe680" stroke="#3d1268" stroke-width="2"/><circle cx="32" cy="31" r="2" fill="#3d1268"/>'
      + '<path d="M17 8 V50 M47 8 V50" stroke="#ffd24a" stroke-width="3" opacity=".9"/><path d="M17 8 V50 M47 8 V50" stroke="#3d1268" stroke-width="0.8" opacity=".5"/></svg>';
  }
  const ROOM_GLYPH = {
    living: 'M5 15v-4a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v4 M3 15h18v3H3Z M5 18v2 M19 18v2',
    kitchen: 'M5 10h14v8a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2Z M3 10h18 M9 6c0-1 1-1 1-2 M13 6c0-1 1-1 1-2',
    bathroom: 'M4 12h16v3a4 4 0 0 1-4 4H8a4 4 0 0 1-4-4Z M6 12V6a2 2 0 0 1 4 0 M8 19v2 M16 19v2',
    bedroom: 'M3 18V8 M3 13h18v5 M21 18v-5 M6 13v-2a2 2 0 0 1 2-2h3v4',
    backyard: 'M12 3l5 7h-3l4 6H6l4-6H7Z M12 16v5',
    dance: 'M12 3v3 M12 6a6 6 0 1 0 .01 0Z M6.5 12h11 M12 6v12 M8 8.5l8 7 M16 8.5l-8 7',
    movie: 'M4 7h16v12H4Z M4 11h16 M8 7l2 4 M13 7l2 4',
    beach: 'M12 4a8 8 0 0 1 8 8H4a8 8 0 0 1 8-8Z M12 12v8 M8 20h8',
    yard: 'M6 9h12a3 3 0 0 1 3 3v2a3 3 0 0 1-5.4 1.8L14.5 14h-5l-1.1 1.8A3 3 0 0 1 3 14v-2a3 3 0 0 1 3-3Z M8 11v3 M6.5 12.5h3',
    tier: 'M3 18h18 M5 18l-1-9 5 4 3-7 3 7 5-4-1 9',
  };
  const ROOM_COL = { living: '#e3946a', kitchen: '#f2b235', bathroom: '#3fb4e8', bedroom: '#7f97c9', backyard: '#5fb04a', dance: '#8f7bff', movie: '#c46a3e', beach: '#e8a840', yard: '#2fc6a8', tier: '#d9772f' };
  function glyphArt(key) {
    const col = ROOM_COL[key] || '#8f7bff';
    return '<svg viewBox="0 0 56 56"><circle cx="28" cy="28" r="24" fill="' + col + '" stroke="#3d1268" stroke-width="2.4"/><circle cx="28" cy="28" r="19" fill="none" stroke="#ffffff" stroke-width="1.5" opacity=".45"/><ellipse cx="21" cy="17" rx="8" ry="4.5" fill="#fff" opacity=".35" transform="rotate(-25 21 17)"/>'
      + '<g transform="translate(14 14) scale(1.17)" fill="none" stroke="#ffffff" stroke-width="2.3" stroke-linecap="round" stroke-linejoin="round"><path d="' + ROOM_GLYPH[key] + '"/></g></svg>';
  }
  function prizeArt(r, big) {
    if (!r) return '';
    if (r.k === 'coins' || r.k === 'gems') {
      const src = r.k === 'coins' ? coinImg() : gemImg(), n = big ? 3 : 2;
      return '<div style="position:relative;width:100%;height:100%">' + Array.from({ length: n }, (_, i) => '<img alt="" src="' + src + '" style="position:absolute;width:' + (big ? 62 : 64) + '%;left:' + (n === 3 ? [2, 36, 19][i] : [8, 30][i]) + '%;top:' + (n === 3 ? [26, 26, 2][i] : [22, 6][i]) + '%;filter:drop-shadow(0 2px 2px rgba(30,0,40,.45))">').join('') + '</div>';
    }
    return chestArt(r.t);
  }
  const prizeName = r => !r ? '' : r.k === 'coins' ? fmt(r.n) + ' Coins' : r.k === 'gems' ? fmt(r.n) + ' Gems' : (CHEST_NAME[r.t] || '') + ' Chest';
  const prizeShort = r => !r ? '' : r.k === 'coins' || r.k === 'gems' ? fmt(r.n) : (CHEST_NAME[r.t] || '') + ' chest';

  // ---------- 4. the Level Pass ----------
  const COL = 96;   // one level's column on the track
  function levelData(x) {
    const s = S(), Sc = window.MainMenuScene, xp = s.xp || { level: (x.st && x.st.L) || 1, into: 0, need: 0, per: 100 }, road = s.road || [];
    const N = Math.max(road.length, 30), rows = [];
    for (let L = 1; L <= N; L++) {
      const r = road[L - 1] || null, un = [];
      Object.keys(Sc.ROOMS).forEach(k => { if (Sc.ROOMS[k].lv === L && L > 1) un.push({ key: k, name: Sc.ROOMS[k].n, kind: 'room' }); });
      if (L === Sc.ROOMS.backyard.lv) un.push({ key: 'yard', name: 'Yard Games', kind: 'yard' });
      const TI = { 5: 'Cozy home', 15: 'Lovely home', 25: 'Royal palace' };
      if (TI[L]) un.push({ key: 'tier', name: TI[L], kind: 'tier' });
      rows.push({ L, r, un, reached: xp.level >= L, got: !!(r && r.got), claimable: !!(r && r.claimable) });
    }
    return { xp, rows };
  }
  // XP still to earn before level L
  function xpTo(xp, L) {
    if (xp.level >= L) return 0; let n = Math.max(0, (xp.need || 0) - (xp.into || 0)); const per = xp.per || 100;
    for (let j = xp.level + 1; j < L; j++) n += per * (j + 1);
    return n;
  }
  const UN_TEXT = { room: 'A new room opens in your house', yard: 'The backyard\'s 7 yard games', tier: 'Your house gets its next look' };

  M.levels = function (x) {
    addCss(); if (!x || x.busy || x.inside) return;
    let D = levelData(x), sel = null, pend = {};
    const first = D.rows.find(r => r.claimable);
    sel = first ? first.L : Math.min(D.rows.length, D.xp.level + 1);
    const nClaim = () => D.rows.filter(r => r.claimable && !pend[r.L]).length;
    const head = () => {
      const xp = D.xp, f = xp.need > 0 ? clamp(xp.into / xp.need, 0, 1) : 0, n = nClaim();
      return '<button class="mm-orb" data-x aria-label="Back">' + ic(BACK, '#fff', 3) + '</button>' + ft('Level Pass', 'gold', 30) + '<div class="mm-sp"></div>'
        + (n > 1 ? '<button class="sq-lvall mm-p tip v-gold" data-all>CLAIM ALL (' + n + ')</button>' : '')
        + '<div class="sq-lvx mm-p tip v-vel"><b class="tx-w">LV ' + xp.level + '</b><div class="sq-lvbar"><i style="width:' + (f * 100).toFixed(1) + '%"></i></div><small>' + (xp.need ? fmt(xp.into) + ' / ' + fmt(xp.need) + ' XP' : '') + '</small></div>'
        + pill(coinImg(), x.Q ? x.Q.wallet().coins : 0, 'sq-coins');
    };
    const tile = (row, u) => {
      if (u) return '<button class="sq-lt un' + (row.reached ? '' : ' lk') + (sel === row.L ? ' sel' : '') + '" data-l="' + row.L + '"><span class="tag">' + (u.kind === 'tier' ? 'HOUSE' : u.kind === 'yard' ? 'GAMES' : 'NEW ROOM') + '</span><div class="ar">' + glyphArt(u.key) + '</div><div class="lb">' + esc(u.name) + '</div>'
        + (row.reached ? '' : '<span class="st lkb">' + LOCK(13) + '</span>') + '</button>';
      const r = row.r, p = pend[row.L], cl = row.claimable && !p, got = row.got || p === 'done';
      return '<button class="sq-lt' + (cl ? ' cl' : got ? ' got' : row.reached ? '' : ' lk') + (sel === row.L ? ' sel' : '') + '" data-l="' + row.L + '"><div class="ar">' + prizeArt(r) + '</div><div class="lb">' + esc(prizeShort(r)) + '</div>'
        + (got ? '<span class="st ok">' + ic(CHECK, '#fff', 3.4) + '</span>' : row.reached ? '' : '<span class="st lkb">' + LOCK(13) + '</span>') + (cl ? '<span class="clm">CLAIM</span>' : '') + '</button>';
    };
    const track = () => {
      const xp = D.xp, f = xp.need > 0 ? clamp(xp.into / xp.need, 0, 1) : 0, w = D.rows.length * COL + 24;
      const fill = Math.min(w - 12, 12 + (xp.level - 1) * COL + COL / 2 + f * COL), cs = x.cre(), Q = x.Q, T = window.SQ_THUMBS || {}, src = cs && Q ? Q.src(cs.id) : null;
      return '<div class="sq-lvin" style="width:' + w + 'px"><div class="sq-rail" style="left:12px;width:' + (w - 24) + 'px"><i style="width:' + (fill - 12).toFixed(1) + 'px"></i></div>'
        + '<div class="sq-you" style="left:' + fill.toFixed(1) + 'px">' + (src ? '<img alt="" src="' + (T[src] || src) + '">' : '') + '</div><i class="sq-youp" style="left:' + fill.toFixed(1) + 'px"></i>'
        + D.rows.map(row => '<div class="sq-lc' + (row.L === xp.level ? ' cur' : '') + '" style="left:' + (12 + (row.L - 1) * COL) + 'px"><div class="sq-lm' + (row.reached ? ' on' : '') + '">' + row.L + '</div>'
          + (row.r ? tile(row) : '') + row.un.map(u => tile(row, u)).join('') + '</div>').join('') + '</div>';
    };
    const preview = () => {
      const row = D.rows[sel - 1]; if (!row) return '';
      const p = pend[row.L], main = row.r ? null : row.un[0], also = row.r ? row.un : row.un.slice(1);
      const art = row.r ? prizeArt(row.r, true) : main ? glyphArt(main.key) : '';
      const title = row.r ? prizeName(row.r) : main ? main.name : 'Level ' + row.L;
      const desc = row.r ? (row.r.k === 'chest' ? 'A chest of new characters' : 'Prize for reaching level ' + row.L) : main ? UN_TEXT[main.kind] : '';
      let act;
      if (row.r && row.claimable && !p) act = '<button class="act mm-p tip v-gold" data-claim="' + row.L + '">' + ft('CLAIM', 'whiteB', 19) + '</button>';
      else if (p === 1) act = '<div class="act mm-p tip v-vel done"><span class="tx-w">Claiming…</span></div>';
      else if (row.reached) act = '<div class="act mm-p tip v-vel done">' + ic(CHECK, '#7dffa8', 3.2) + '<span class="tx-w">' + (row.r ? 'CLAIMED' : 'UNLOCKED') + '</span></div>';
      else act = '<div class="act mm-p tip v-vel done">' + LOCK(16) + '<span class="tx-w">' + fmt(xpTo(D.xp, row.L)) + ' XP to go</span></div>';
      return '<div class="rib">' + ft('LEVEL ' + row.L, row.reached ? 'gold' : 'white', 20) + '</div><div class="art">' + art + '</div><div class="t">' + ft(title, 'white', 17) + '</div><div class="d">' + esc(desc) + '</div>'
        + (also.length ? '<div class="also">' + also.map(u => '<span class="sq-chip">' + esc(u.name) + '</span>').join('') + '</div>' : '') + act;
    };
    const p = x.pageOpen('<div class="mm-pgh">' + head() + '</div><div class="sq-lvprev mm-p v-vel">' + preview() + '</div><div class="sq-lvtrack">' + track() + '</div>');
    const pg = p.querySelector('.mm-pg'), hd = p.querySelector('.mm-pgh'), pv = p.querySelector('.sq-lvprev'), tr = p.querySelector('.sq-lvtrack');
    const redraw = keepScroll => { const sl = tr.scrollLeft; hd.innerHTML = head(); pv.innerHTML = preview(); tr.innerHTML = track(); x.fitFT(p); if (keepScroll) tr.scrollLeft = sl; bind(); fillIcons(p); };
    fillIcons(p);
    const center = L => { tr.scrollLeft = Math.max(0, 12 + (L - 1) * COL + COL / 2 - tr.clientWidth / 2); };
    const claim = L => {
      const row = D.rows[L - 1]; if (!row || !row.claimable || pend[L]) return;
      pend[L] = 1; sfx('tap'); redraw(true);
      if (App && App.inApp) send('claim', { lv: L, k: row.r.k });
      else { pend[L] = 'done'; burst(L); redraw(true); }
    };
    const burst = L => {
      const row = D.rows[L - 1]; if (!row || !row.r || row.r.k === 'chest') return;
      const from = tr.querySelector('.sq-lt[data-l="' + L + '"]'), to = hd.querySelector('.sq-coins img') || hd; if (!from) return;
      const pr = pg.getBoundingClientRect(), k = x.k, a = from.getBoundingClientRect(), b = to.getBoundingClientRect(), src = row.r.k === 'gems' ? gemImg() : coinImg();
      sfx('score');
      for (let i = 0; i < 9; i++) {
        const im = document.createElement('img'); im.className = 'sq-fly'; im.src = src;
        const ax = (a.left + a.width / 2 - pr.left) / k - 13 + (Math.random() - 0.5) * 40, ay = (a.top + a.height / 2 - pr.top) / k - 13 + (Math.random() - 0.5) * 30;
        im.style.left = ax + 'px'; im.style.top = ay + 'px'; pg.appendChild(im);
        const bx = (b.left + b.width / 2 - pr.left) / k - 13, by = (b.top + b.height / 2 - pr.top) / k - 13;
        setTimeout(() => { im.style.transform = 'translate(' + (bx - ax) + 'px,' + (by - ay) + 'px) scale(.7)'; im.style.opacity = '0'; }, 30 + i * 55);
        setTimeout(() => im.remove(), 1300 + i * 55);
      }
    };
    function bind() {
      hd.querySelector('[data-x]').onclick = close;
      const all = hd.querySelector('[data-all]');
      if (all) all.onclick = () => { const ls = D.rows.filter(r => r.claimable && !pend[r.L]).map(r => r.L); if (!ls.length) return; ls.forEach(L => { pend[L] = 1; }); sfx('tap'); redraw(true); if (App && App.inApp) send('claim', { all: true, lv: ls[0] }); else { ls.forEach(L => { pend[L] = 'done'; burst(L); }); redraw(true); } };
      tr.querySelectorAll('[data-l]').forEach(b => b.onclick = () => { const L = +b.dataset.l; if (sel === L) { claim(L); return; } sel = L; sfx('tap'); redraw(true); });
      const cb = pv.querySelector('[data-claim]'); if (cb) cb.onclick = () => claim(+cb.dataset.claim);
    }
    bind();
    requestAnimationFrame(() => center(sel));
    // the app's answers: a claim's result, then the player's new state (prize marked got, coins up)
    const offs = [];
    if (App) {
      offs.push(App.on('claimed', m => {
        if (m.road) return; // a chest: the app opens its level road (the chest smash)
        const ls = Object.keys(pend).filter(L => pend[L] === 1).map(Number);
        if (m.ok) { ls.forEach(L => { pend[L] = 'done'; burst(L); }); }
        else { ls.forEach(L => { delete pend[L]; }); toast(p, m.error === 'claimed' ? 'Already claimed' : m.error === 'need_level' ? 'Reach that level first' : 'Something went wrong. Try again!'); }
        redraw(true);
      }));
      offs.push(App.on('state', () => { D = levelData(x); Object.keys(pend).forEach(L => { const r = D.rows[L - 1]; if (r && r.got) delete pend[L]; }); redraw(true); }));
    }
    function close() { offs.forEach(f => f && f()); x.closePage(); if (x.ctrl) x.ctrl.play('happy'); }
  };

  // ---------- 5. Characters ----------
  const PLQ = ['v-wood', 'v-bronze', 'v-amy', 'v-gold', 'v-rainbow'], NFT = ['wood', 'bronze', 'amethyst', 'whiteB', 'white'];
  M.characters = function (x) {
    addCss(); const Q = x.Q; if (!Q || !window.RiveRig || x.busy || x.inside) return false;
    const C4 = window.SQ_CARD4, T = window.SQ_THUMBS || {}, on = c => c && c.art && (!window.SquishConfig || window.SquishConfig.creatureOn(c.key));
    const all = Q.ROSTER.filter(on), own = id => Q.owns(id);
    const byR = (a, b) => C4 ? C4.byRar(b, a) : b.rar - a.rar || a.id - b.id;
    const sets = [].concat(Q.SETS, [Q.HEROES, Q.ELEMENTAL, Q.SEASON]).filter(s => s && all.some(c => c.set === s.key));
    let filter = 'all', pick = x.selId(), rig = null, rigKey = null;
    const mine = () => x.selId();
    const list = () => {
      const L = all.filter(c => filter === 'all' ? true : filter === 'owned' ? own(c.id) : c.set === filter);
      return L.filter(c => own(c.id)).sort(byR).concat(L.filter(c => !own(c.id)).sort(byR));
    };
    const cardHtml = c => {
      const id = c.id, s = Q.src(id), d = C4 ? C4.of(c) : null, o = own(id), cls = 'sq-cc' + (o ? '' : ' no') + (id === pick ? ' pv' : '') + (id === mine() ? ' me' : '');
      if (!d) return '<button class="' + cls + '" data-id="' + id + '" aria-label="' + esc(c.name) + '"><img class="art" alt="" loading="lazy" style="inset:0;width:100%;height:100%;object-fit:contain" src="' + (T[s] || s) + '">' + (o ? '' : '<span class="lk">' + LOCK(24) + '</span>') + '</button>';
      const bx = p => 'left:' + d[p + 'L'] + ';top:' + d[p + 'T'] + ';width:' + d[p + 'W'] + ';height:' + d[p + 'H'];
      return '<button class="' + cls + '" data-id="' + id + '" aria-label="' + esc(c.name) + '"><img class="bg" alt="" loading="lazy" src="' + d.cdBg + '" style="' + bx('cdBg') + ';object-fit:cover;object-position:center bottom"><img class="art" alt="" loading="lazy" src="' + (T[s] || s) + '" style="' + bx('cdArt') + ';object-fit:contain;object-position:center bottom"><img class="frm" alt="" loading="lazy" src="' + d.cdFrame + '" style="inset:0;width:100%;height:100%">'
        + (d.cdHasName ? '<img class="nmi" alt="" loading="lazy" src="' + d.cdName + '" style="' + bx('cdNm') + ';object-fit:contain">' : '<span class="nmT" style="' + bx('cdNm') + ';color:' + d.cdInk + '">' + esc(c.name) + '</span>')
        + (o ? '' : '<span class="lk">' + LOCK(24) + '</span>') + '</button>';
    };
    const nOwn = all.filter(c => own(c.id)).length;
    const p = x.pageOpen('<div class="mm-pgh"><button class="mm-orb" data-x aria-label="Back">' + ic(BACK, '#fff', 3) + '</button>' + ft('Characters', 'gold', 30)
      + '<div class="s">' + nOwn + ' of ' + all.length + ' collected · tap one to meet it</div><div class="mm-sp"></div></div>'
      + '<div class="sq-cs"><div class="stage"><div class="rays"></div><div class="pad"></div><div class="rig"></div></div><div class="who"></div></div>'
      + '<div class="sq-cg"><div class="sq-cf"></div><div class="sq-cgr"></div></div>');
    const stage = p.querySelector('.sq-cs .stage'), who = p.querySelector('.sq-cs .who'), grid = p.querySelector('.sq-cgr'), fl = p.querySelector('.sq-cf');
    const chips = () => {
      fl.innerHTML = [['all', 'All ' + all.length], ['owned', 'Mine ' + nOwn]].concat(sets.map(s => [s.key, s.name])).map(([k, n]) => '<button class="sq-chip' + (filter === k ? ' on' : '') + '" data-f="' + k + '">' + esc(n) + '</button>').join('');
      fl.querySelectorAll('[data-f]').forEach(b => b.onclick = () => { filter = b.dataset.f; sfx('tap'); chips(); fill(); grid.scrollTop = 0; });
    };
    const fill = () => { grid.innerHTML = list().map(cardHtml).join(''); grid.querySelectorAll('[data-id]').forEach(b => b.onclick = () => choose(+b.dataset.id)); };
    const showRig = (c, pop) => {
      let host = stage.querySelector('.rig');
      if (rig && rigKey === c.key && !pop) return;
      if (rig) { try { rig.destroy(); } catch (e) { /* gone */ } rig = null; }
      const fresh = host.cloneNode(false); host.replaceWith(fresh); host = fresh;
      host.className = 'rig' + (pop ? ' pop' : '') + (own(c.id) ? '' : ' sil');
      try { rig = window.RiveRig.mount(host, c.key, {}); rigKey = c.key; } catch (e) { rig = null; }
      if (rig) setTimeout(() => { if (!rig) return; const L = allowed(rig._I, own(c.id) ? ['happy', 'love', 'cheer'] : ['look', 'surprised']); if (L.length) rig.play(L[Math.floor(Math.random() * L.length)]); }, 350);
    };
    const info = c => {
      const r = clamp(c.rar | 0, 0, 4), o = own(c.id), set = sets.find(s => s.key === c.set), RA = Q.RAR && Q.RAR[r], e = o && Q.entry ? Q.entry(c.id) : null;
      const stg = e && Q.STAGES && Q.STAGES[e.st || 0], fin = e && Q.FIN && Q.FIN.find(f => f.k === (e.eq || 'n'));
      let act;
      if (!o) act = '<button class="act mm-p tip v-vel" data-shop>' + LOCK(16) + '<span class="tx-w">Find it in chests</span></button>';
      else if (c.id === mine()) act = '<div class="act mm-p tip v-vel done">' + ic(CHECK, '#7dffa8', 3.2) + '<span class="tx-w">IN FRONT OF YOUR HOUSE</span></div>';
      else act = '<button class="act mm-p tip v-gold" data-sel>' + ft('CHOOSE', 'whiteB', 20) + '</button>';
      who.innerHTML = '<div class="np mm-p tip ' + PLQ[r] + '"><div class="pips">' + STAR.repeat(r + 1) + '</div>' + ft(c.name, NFT[r], 19) + '</div>'
        + '<div class="chips">' + (RA ? '<span class="sq-chip" style="background:' + RA.dot + ';color:#2a0a4e;box-shadow:0 0 0 1.5px #3d1268">' + RA.label + '</span>' : '') + (set ? '<span class="sq-chip">' + esc(set.name) + '</span>' : '') + '</div>'
        + '<div class="info">' + (o ? esc([stg && stg.name, fin && fin.k !== 'n' ? fin.name : null].filter(Boolean).join(' · ')) : 'Not in your squad yet') + '</div>' + act;
      x.fitFT(who);
      const sb = who.querySelector('[data-sel]'); if (sb) sb.onclick = () => choose(c.id, true);
      const sh = who.querySelector('[data-shop]'); if (sh) sh.onclick = () => { sfx('tap'); close(); send('open', { what: 'shop' }); };
    };
    const mark = () => grid.querySelectorAll('[data-id]').forEach(b => { const id = +b.dataset.id; b.classList.toggle('pv', id === pick); b.classList.toggle('me', id === mine()); });
    function choose(id, confirm) {
      const c = Q.ROSTER[id]; if (!c) return;
      if (confirm) {
        if (!own(id) || !x.Lb) return;
        x.Lb.select(id); x.preview = null; x.mountRig(); x.hudDraw();
        sfx('win'); if (rig) { const L = allowed(rig._I, ['cheer', 'happy', 'dance']); if (L.length) rig.play(L[0]); }
        toast(p, c.name + ' is in front of your house!', 'r');
        info(c); mark(); return;
      }
      const changed = id !== pick; pick = id; sfx('tap');
      showRig(c, changed); info(c); mark();
    }
    chips(); fill();
    const c0 = Q.ROSTER[pick] || all[0]; if (c0) { pick = c0.id; showRig(c0, true); info(c0); mark(); }
    const sc = grid.querySelector('.sq-cc.pv'); if (sc) sc.scrollIntoView({ block: 'center' });
    // the rig is destroyed with the page, however it closes
    const watch = setInterval(() => { if (!p.isConnected) { clearInterval(watch); if (rig) { try { rig.destroy(); } catch (e) { /* gone */ } rig = null; } } }, 500);
    function close() { clearInterval(watch); if (rig) { try { rig.destroy(); } catch (e) { /* gone */ } rig = null; } x.closePage(); if (x.ctrl) x.ctrl.play('happy'); }
    p.querySelector('[data-x]').onclick = close;
    return true;
  };

  // ---------- 6. the page framework (REDESIGN_V7): the new menu pages (webgame/app/pages/*.js) ----------
  // A page registers itself: SquishMenu.pages.shop = (x, opts, P) => { … }, where x is the menu's insides (the same as
  // the Level Pass gets) and P the page kit below. SquishMenu.open('shop', opts) opens it over the menu; the app can ask
  // for one too ({ type: 'page', name }). Android back closes it (its back orb has data-x).
  M.pages = {};
  M.kit = { esc, fmt, clamp, r1, wait, tween, ft, ic, pill, toast, sfx, send, LOCK, STAR, SPARK, CHECK, BACK, LEFT, RIGHT, coinImg, gemImg, fillIcons, addCss,
    // the page shell: a full-screen page with the menu's header (back orb, gold title, a note, the right-hand pills)
    // → { page, pg (the 852-wide scaled box to fill), close() }
    page(x, o) {
      addCss();
      const p = x.pageOpen('<div class="mm-pgh"><button class="mm-orb" data-x aria-label="Back">' + ic(BACK, '#fff', 3) + '</button>' + ft(o.title, 'gold', 30)
        + (o.note ? '<div class="s">' + esc(o.note) + '</div>' : '') + '<div class="mm-sp"></div>' + (o.right || '') + '</div>' + (o.body || ''));
      fillIcons(p);
      const close = () => { if (o.onClose) try { o.onClose(); } catch (e) { /* page gone */ } x.closePage(); if (x.ctrl) x.ctrl.play('happy'); };
      p.querySelector('[data-x]').onclick = close;
      return { page: p, pg: p.querySelector('.mm-pg'), close };
    },
  };
  M.open = function (name, opts) {
    const x = M._x, fn = M.pages[name];
    if (!x || !fn || x.busy || x.inside) return false;
    addCss();
    try { fn(x, opts || {}, M.kit); return true; } catch (e) { console.error('[SquishMenu] page ' + name, e); return false; }
  };
  if (App) App.on('page', m => { if (m && m.name) M.open(m.name, m.opts); });
})();
