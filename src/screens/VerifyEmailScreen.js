import React, { useState } from 'react';
import { StyleSheet, Text, View, useWindowDimensions } from 'react-native';
import ShadowText from '../components/candy/ShadowText';
import IntroBackground from '../squad/IntroBackground';
import RigCreature from '../components/RigCreature';
import Pressable from '../components/Pressable';
import HopDots from '../components/candy/HopDots';
import { BtnText, CandyBtn, F, GOLD_RING } from '../squad/ui';
import { reloadVerified, sendVerification } from '../firebase/auth';

// Email verification (REDESIGN_V6 step 8, ruling 4: "make sure they are
// real"): an email/password account plays once its address is confirmed.
// Register sends the link; this waits for it — "I've verified" asks Firebase
// again, "Send it again" resends (Firebase limits how often), and "Log out"
// is the way back. It also catches accounts made before verification existed.

// the sign-in screen's wood look (REDESIGN_V8): brown ink and outlines, a cream card, a gold button
const INK = '#5b3418';
const RIM = '#6b4426';

export default function VerifyEmailScreen({ email, onVerified, onLogout }) {
  // landscape (REDESIGN_V8): Mittens and the title on the left, the card on the right
  const win = useWindowDimensions();
  const wide = win.width > win.height;
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState('');
  const check = async () => {
    if (busy) return;
    setBusy(true);
    try {
      if (await reloadVerified()) return onVerified();
      setNote("Not yet — tap the link in the email first. Check your spam folder too.");
    } catch (e) {
      setNote('Could not check right now. Try again in a moment.');
    } finally {
      setBusy(false);
    }
  };
  const resend = async () => {
    try {
      await sendVerification();
      setNote(`Sent again to ${email}.`);
    } catch (e) {
      setNote('Please wait a minute before sending another.');
    }
  };
  return (
    <IntroBackground>
      <View style={[styles.col, wide && styles.row2]}>
        <View style={wide ? styles.side : null}>
          <RigCreature creatureKey="mittens" style={[styles.mittens, wide && styles.mittensWide]} />
          <ShadowText style={styles.title} shadows={[[0, 4, RIM], [2.5, 0, RIM], [-2.5, 0, RIM], [0, -2.5, RIM]]}>
            Check your email
          </ShadowText>
        </View>
        <View style={[styles.cardLip, wide && styles.cardWide]}>
          <View style={styles.card}>
            <Text style={styles.text}>
              We sent a link to <Text style={styles.email}>{email}</Text>. Tap it to confirm it's really you, then come back here.
            </Text>
            {!!note && <Text style={styles.note}>{note}</Text>}
            <CandyBtn kind="gold" onPress={check} padV={10} lip={5} stretch>
              {busy ? (
                <HopDots size={14} style={{ height: 26 }} />
              ) : (
                <BtnText ring={GOLD_RING} size={20}>
                  I've verified
                </BtnText>
              )}
            </CandyBtn>
            <View style={styles.row}>
              <Pressable onPress={resend} hitSlop={8}>
                <Text style={styles.link}>Send it again</Text>
              </Pressable>
              <Pressable onPress={onLogout} hitSlop={8}>
                <Text style={styles.link}>Log out</Text>
              </Pressable>
            </View>
          </View>
        </View>
      </View>
    </IntroBackground>
  );
}

const styles = StyleSheet.create({
  col: { flex: 1, justifyContent: 'center', paddingHorizontal: 22, gap: 10 },
  row2: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 30 },
  side: { alignItems: 'center', gap: 6 },
  mittens: { width: 150, height: 150, alignSelf: 'center' },
  mittensWide: { width: 180, height: 180 },
  cardWide: { width: 400 },
  title: { alignSelf: 'center', fontFamily: F.display, fontSize: 34, lineHeight: 40, color: '#ffffff' },
  cardLip: { borderRadius: 28, backgroundColor: RIM, paddingBottom: 5 },
  card: { gap: 14, padding: 18, borderRadius: 28, backgroundColor: '#fff6e2', borderWidth: 2.5, borderColor: RIM },
  text: { fontFamily: F.heavy, fontSize: 14, lineHeight: 20, color: INK, textAlign: 'center' },
  email: { fontFamily: F.black, color: '#c25e00' },
  note: { fontFamily: F.heavy, fontSize: 12.5, lineHeight: 17, color: '#d0345a', textAlign: 'center' },
  row: { flexDirection: 'row', justifyContent: 'space-between', paddingHorizontal: 6 },
  link: { fontFamily: F.black, fontSize: 13, color: '#8a3a0e' },
});
