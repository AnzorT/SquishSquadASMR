import React, { useEffect, useState } from 'react';
import { BackHandler, Keyboard, KeyboardAvoidingView, Modal, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import ToggleSwitch from '../../components/candy/ToggleSwitch';
import TestWallet from '../../components/TestWallet';
import sfx from '../../audio/sfx';
import { Btn, Icon, InkText, Lettering, Orb, PageBg, PageHeader, Plaque, Stage, FONT, INK } from '../../royal';

// Settings in the new look, landscape (REDESIGN_V7 step 3): the Menu's gear opens it (the old Lobby retires). Left:
// sound, nickname, and the places that lived in the Lobby (Achievements, Stats, Level Pass). Right: feedback, replay the
// tutorial, log out, and a tester's Test wallet. The same handlers as the old sheet (App.js).

const LINKS = [
  ['trophies', 'Achievements', 'M8 4h8v5a4 4 0 0 1-8 0Z M8 6H5a3 3 0 0 0 3 4 M16 6h3a3 3 0 0 1-3 4 M12 13v4 M8 20h8 M10 17h4'],
  ['stats', 'Stats', 'M5 20V11 M12 20V5 M19 20v-6'],
  ['road', 'Level Pass', 'M12 3l2.6 5.4 5.9.9-4.3 4.1 1 5.9L12 16.5 6.8 19.3l1-5.9L3.5 9.3l5.9-.9Z'],
];

function Panel({ title, children, style }) {
  return (
    <Plaque variant="vel" radius={22} rim={3.5} ring={2} lip={4} style={[styles.panel, style]}>
      <Lettering text={title} variant="gold" size={19} />
      {children}
    </Plaque>
  );
}

export default function SettingsRoyal({ onBack, nickname, onSaveNickname, onSubmitFeedback, onLogout, onReplayTutorial, onOpenStats, onOpenAchievements, onOpenRoad, tester, wallet, fpsOn, onToggleFps }) {
  const [sound, setSound] = useState({ music: true, effects: true });
  useEffect(() => sfx.subscribe(setSound), []);
  const [nick, setNick] = useState((nickname || '').slice(0, 10));
  useEffect(() => setNick((nickname || '').slice(0, 10)), [nickname]);
  const [toast, setToast] = useState(null);
  const [feedback, setFeedback] = useState(null); // the text while the feedback box is open
  const [wall, setWall] = useState(false); // a tester's Test wallet on show
  const flash = (t) => {
    setToast(t);
    setTimeout(() => setToast((c) => (c === t ? null : c)), 2200);
  };
  useEffect(() => {
    const sub = BackHandler.addEventListener('hardwareBackPress', () => {
      if (feedback != null) setFeedback(null);
      else if (wall) setWall(false);
      else onBack();
      return true;
    });
    return () => sub.remove();
  }, [feedback, wall, onBack]);
  const go = { trophies: onOpenAchievements, stats: onOpenStats, road: onOpenRoad };
  const save = () => {
    if (!nick.trim()) return;
    Keyboard.dismiss();
    onSaveNickname(nick.trim());
    flash('Nickname updated!');
  };
  const send = async () => {
    const t = (feedback || '').trim();
    if (!t) return;
    try {
      await onSubmitFeedback(t);
      setFeedback(null);
      flash('Thanks for the feedback!');
    } catch (e) {
      flash('Could not send. Try again!');
    }
  };
  return (
    <View style={styles.root}>
      <PageBg />
      <Stage>
        <PageHeader title="Settings" note={nickname ? `Playing as ${nickname}` : null} onBack={onBack} />
        <View style={styles.cols}>
          <View style={styles.col}>
            <Panel title="Sound">
              <View style={styles.row}>
                <InkText size={15}>Music</InkText>
                <ToggleSwitch value={sound.music} onToggle={() => sfx.setMusicOn(!sound.music)} />
              </View>
              <View style={styles.row}>
                <InkText size={15}>Sound effects</InkText>
                <ToggleSwitch value={sound.effects} onToggle={() => sfx.setEffectsOn(!sound.effects)} />
              </View>
            </Panel>
            <Panel title="Nickname">
              <View style={styles.row}>
                <TextInput style={styles.input} value={nick} onChangeText={setNick} maxLength={10} placeholder="Your name" placeholderTextColor="#b9a2e8" returnKeyType="done" onSubmitEditing={save} />
                <Btn label="SAVE" size={15} width={86} height={36} radius={12} onPress={save} />
              </View>
            </Panel>
          </View>
          <View style={styles.col}>
            <Panel title="Your squad">
              {LINKS.map(([k, label, d]) =>
                go[k] ? (
                  <Plaque key={k} variant="amy" tips radius={14} onPress={go[k]} style={styles.link}>
                    <Icon d={d} size={18} width={2.3} />
                    <InkText size={15}>{label}</InkText>
                  </Plaque>
                ) : null,
              )}
            </Panel>
          </View>
          <View style={styles.col}>
            <Panel title="More">
              <Plaque variant="amy" tips radius={14} onPress={() => setFeedback('')} style={styles.link}>
                <Icon d="M4 5h16v11H8l-4 4Z" size={18} width={2.3} />
                <InkText size={15}>Send feedback</InkText>
              </Plaque>
              {onReplayTutorial ? (
                <Plaque variant="amy" tips radius={14} onPress={onReplayTutorial} style={styles.link}>
                  <Icon d="M4 12a8 8 0 1 0 2.3-5.7 M4 4v4h4" size={18} width={2.3} />
                  <InkText size={15}>Replay tutorial</InkText>
                </Plaque>
              ) : null}
              {tester ? (
                <Plaque variant="amy" tips radius={14} onPress={() => setWall(true)} style={styles.link}>
                  <Icon d="M4 7h16v12H4Z M4 7l3-3h10l3 3 M15 13h2" size={18} width={2.3} />
                  <InkText size={15}>Test wallet</InkText>
                </Plaque>
              ) : null}
              <Btn label="LOG OUT" variant="vel" size={16} height={40} radius={14} onPress={onLogout} style={styles.logout} />
            </Panel>
          </View>
        </View>
        {tester && wall ? (
          <View style={styles.tester}>
            <Plaque variant="pearl" radius={22} rim={3.5} ring={2} lip={4} style={styles.testerIn}>
              <ScrollView style={{ alignSelf: 'stretch' }} contentContainerStyle={{ padding: 10 }} showsVerticalScrollIndicator={false}>
                <TestWallet wallet={wallet} fpsOn={fpsOn} onToggleFps={onToggleFps} />
              </ScrollView>
            </Plaque>
            <Orb icon="close" size={34} onPress={() => setWall(false)} style={styles.testerX} />
          </View>
        ) : null}
        {toast ? (
          <Plaque variant="gold" tips style={styles.toast}>
            <Text style={styles.toastT}>{toast}</Text>
          </Plaque>
        ) : null}
      </Stage>
      <Modal visible={feedback != null} transparent animationType="fade" onRequestClose={() => setFeedback(null)} statusBarTranslucent supportedOrientations={['landscape']}>
        <KeyboardAvoidingView style={styles.fbWrap}>
          <Plaque variant="vel" radius={22} rim={3.5} ring={2} lip={4} style={styles.fb}>
            <Lettering text="Feedback" variant="gold" size={20} />
            <TextInput style={styles.fbInput} value={feedback || ''} onChangeText={setFeedback} placeholder="Tell us what you think..." placeholderTextColor="#b9a2e8" multiline textAlignVertical="top" />
            <View style={styles.fbRow}>
              <Btn label="CANCEL" variant="vel" size={15} width={120} height={38} radius={12} onPress={() => setFeedback(null)} />
              <Btn label="SEND" size={15} width={120} height={38} radius={12} onPress={send} />
            </View>
          </Plaque>
        </KeyboardAvoidingView>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { ...StyleSheet.absoluteFillObject, backgroundColor: '#24123f' },
  cols: { position: 'absolute', left: 22, top: 74, right: 22, bottom: 18, flexDirection: 'row', gap: 18 },
  col: { flex: 1, gap: 14 },
  panel: { alignItems: 'stretch', justifyContent: 'flex-start', paddingHorizontal: 16, paddingTop: 10, paddingBottom: 14, gap: 10 },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 10 },
  input: { flex: 1, height: 36, borderRadius: 12, paddingHorizontal: 12, backgroundColor: 'rgba(20,0,50,0.45)', borderWidth: 1.5, borderColor: 'rgba(255,236,170,0.55)', color: '#ffffff', fontFamily: FONT.bodyHeavy, fontSize: 15 },
  link: { height: 42, flexDirection: 'row', gap: 10, paddingHorizontal: 14, justifyContent: 'flex-start' },
  logout: { marginTop: 4 },
  tester: { ...StyleSheet.absoluteFillObject, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(24,8,40,0.6)' },
  testerIn: { width: 420, height: 330, justifyContent: 'flex-start' },
  testerX: { position: 'absolute', left: '50%', marginLeft: 196, top: 18 },
  toast: { position: 'absolute', alignSelf: 'center', left: '35%', right: '35%', bottom: 18, height: 34 },
  toastT: { fontFamily: FONT.display, fontSize: 14, color: INK, includeFontPadding: false },
  fbWrap: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(24,8,40,0.6)' },
  fb: { width: 460, paddingHorizontal: 18, paddingTop: 12, paddingBottom: 16, gap: 12, alignItems: 'stretch' },
  fbInput: { height: 110, borderRadius: 14, padding: 12, backgroundColor: 'rgba(20,0,50,0.45)', borderWidth: 1.5, borderColor: 'rgba(255,236,170,0.55)', color: '#ffffff', fontFamily: FONT.body, fontSize: 14 },
  fbRow: { flexDirection: 'row', justifyContent: 'center', gap: 16 },
});
