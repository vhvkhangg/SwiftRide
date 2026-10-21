import { useRef, useState } from 'react';
import type { PropsWithChildren } from 'react';
import { ActivityIndicator, KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import type { TextProps } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import Svg, { Circle, Path, Rect, Text as SvgText } from 'react-native-svg';
import { findPlace } from './domain';
import type { Trip } from './domain';

export const palette = { ink: '#12383D', teal: '#087F8C', dark: '#064D56', muted: '#557078', paper: '#F3F5F0', line: '#DFE8E5', mint: '#E5F3EF', coral: '#BC573E', sand: '#FBF0DA', white: '#FFFFFF' };
export const font = { regular: 'BeVietnamPro_400Regular', medium: 'BeVietnamPro_500Medium', bold: 'BeVietnamPro_700Bold' };
export function T({ style, ...props }: TextProps) { return <Text {...props} style={[styles.text, style]} />; }

const iconPaths = {
  eye: 'M2 12s3-7 10-7 10 7 10 7-3 7-10 7S2 12 2 12ZM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z',
  eyeOff: 'm3 3 18 18M10 5.2A12 12 0 0 1 12 5c7 0 10 7 10 7a19 19 0 0 1-3 4M6 6.5A20 20 0 0 0 2 12s3 7 10 7a12 12 0 0 0 5.5-1.5M9.9 9.9a3 3 0 0 0 4.2 4.2',
  arrow: 'M5 12h14m-6-6 6 6-6 6', back: 'M19 12H5m6-6-6 6 6 6',
  pin: 'M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0ZM12 7v6m-3-3h6',
  user: 'M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM4 22v-3a8 8 0 0 1 16 0v3',
  home: 'M3 10 12 3l9 7v11h-6v-7H9v7H3Z',
  check: 'm5 12 4 4L20 5', close: 'm6 6 12 12M6 18 18 6',
  layers: 'm12 3 10 5-10 5L2 8Zm-9 10 9 5 9-5M3 18l9 5 9-5',
  card: 'M3 5h18v14H3ZM3 10h18M6 15h4',
  bike: 'M9 17a4 4 0 1 1-8 0 4 4 0 0 1 8 0Zm14 0a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM5 17l5-9 5 9h4L14 4h4M8 8h7',
  warning: 'M12 3 1 21h22ZM12 9v5m0 3v1',
  logout: 'M9 4H3v16h6m5-13 5 5-5 5M7 12h12',
};
export type IconName = keyof typeof iconPaths;
export function Icon({ name, color = palette.teal, size = 22 }: { name: IconName; color?: string; size?: number }) {
  return <Svg width={size} height={size} viewBox="0 0 24 24" accessible={false}><Path d={iconPaths[name]} fill="none" stroke={color} strokeWidth={1.7} strokeLinecap="round" strokeLinejoin="round" /></Svg>;
}

export function Button({ title, onPress, variant = 'primary', disabled = false, busy = false, icon }: {
  title: string; onPress: () => void | Promise<void>; variant?: 'primary' | 'secondary' | 'danger'; disabled?: boolean; busy?: boolean; icon?: IconName;
}) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const running = useRef(false);
  const lastPress = useRef(0);
  const loading = busy || pending;
  const blocked = disabled || loading;
  async function press() {
    if (blocked || running.current || Date.now() - lastPress.current < 350) return;
    lastPress.current = Date.now();
    running.current = true;
    setError('');
    try {
      const result = onPress();
      if (result) { setPending(true); await result; }
    } catch {
      setError('Không thực hiện được thao tác. Dữ liệu nhập vẫn giữ nguyên; vui lòng thử lại.');
    } finally {
      running.current = false;
      setPending(false);
    }
  }
  return <><Pressable accessibilityRole="button" accessibilityLabel={title} accessibilityState={{ disabled: blocked, busy: loading }} disabled={blocked} onPress={press}
    style={({ pressed }) => [styles.button, variant === 'primary' ? styles.primary : styles.secondary, blocked && { opacity: 0.45 }, pressed && { opacity: 0.75 }]}>
    <T style={[styles.buttonText, { color: variant === 'primary' ? palette.white : variant === 'danger' ? palette.coral : palette.teal }]}>{title}</T>
    {loading ? <ActivityIndicator accessibilityLabel="Đang xử lý" color={variant === 'primary' ? palette.white : palette.teal} /> : icon && <Icon name={icon} color={variant === 'primary' ? palette.white : palette.teal} size={20} />}
  </Pressable>{!!error && <T accessibilityRole="alert" style={{ color: palette.coral }}>{error}</T>}</>;
}

export function Card({ children }: PropsWithChildren) { return <View style={styles.card}>{children}</View>; }
export function Eyebrow({ children }: PropsWithChildren) { return <T style={styles.eyebrow}>{children}</T>; }
export function Title({ children }: PropsWithChildren) { return <T accessibilityRole="header" style={styles.title}>{children}</T>; }
export function Note({ children, warning = false }: PropsWithChildren<{ warning?: boolean }>) {
  return <View style={[styles.note, warning && { backgroundColor: palette.sand }]}><Icon name={warning ? 'warning' : 'check'} size={19} color={warning ? palette.coral : palette.teal} /><T style={styles.noteText}>{children}</T></View>;
}
export function Demo({ children }: PropsWithChildren) { return <View style={styles.demo}><Eyebrow>ĐIỀU KHIỂN MÔ PHỎNG</Eyebrow>{children}</View>; }
export function Metric({ label, value }: { label: string; value: string }) {
  return <View style={styles.metric}><T style={styles.metricValue}>{value}</T><T style={styles.caption}>{label}</T></View>;
}

export function Route({ trip }: { trip: Trip }) {
  return <View style={styles.route}>
    <View style={styles.row}><View style={styles.pickupDot} /><View style={styles.flex}><T style={styles.caption}>ĐIỂM ĐÓN</T><T style={styles.place}>{findPlace(trip.pickup).name}</T></View></View>
    <View style={styles.routeDash} />
    <View style={styles.row}><View style={styles.destinationDot} /><View style={styles.flex}><T style={styles.caption}>ĐIỂM ĐẾN</T><T style={styles.place}>{findPlace(trip.destination).name}</T></View></View>
  </View>;
}

export function Person({ driver = false }: { driver?: boolean }) {
  return <View style={styles.person}>
    <View style={styles.avatar}><Icon name={driver ? 'bike' : 'user'} size={28} /></View>
    <View style={styles.flex}><T style={styles.place}>{driver ? 'Minh Anh' : 'Khánh'}</T><T style={styles.caption}>{driver ? 'Honda Vision · Trắng' : 'Hành khách · Tài khoản demo'}</T></View>
    {driver && <View style={styles.plate}><T style={styles.plateText}>59-S1</T><T style={styles.plateText}>123.45</T></View>}
  </View>;
}

export function Sheet({ visible, title, onClose, children }: PropsWithChildren<{ visible: boolean; title: string; onClose: () => void }>) {
  const insets = useSafeAreaInsets();
  return <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose} statusBarTranslucent>
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : 'height'} style={styles.modalBackdrop}>
      <View accessibilityViewIsModal style={[styles.sheet, { paddingBottom: Math.max(insets.bottom, 20) }]}>
        <View style={styles.sheetHandle} /><View style={styles.row}><T accessibilityRole="header" style={[styles.subtitle, styles.flex]}>{title}</T><Pressable accessibilityRole="button" accessibilityLabel="Đóng" onPress={onClose} style={styles.iconButton}><Icon name="close" /></Pressable></View>
        <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={styles.sheetBody}>{children}</ScrollView>
      </View>
    </KeyboardAvoidingView>
  </Modal>;
}

const positions: Record<string, [number, number]> = { university: [62, 177], market: [281, 116], postoffice: [307, 53], park: [150, 57] };
export function CityMap({ trip }: { trip: Trip }) {
  const pickup = positions[trip.pickup];
  const destination = positions[trip.destination];
  return <View accessibilityRole="image" accessibilityLabel={`Bản đồ minh họa. Điểm đón ${findPlace(trip.pickup).name}${destination ? `, điểm đến ${findPlace(trip.destination).name}` : ''}. Không theo dõi GPS.`} style={styles.map}>
    <Svg width="100%" height={220} viewBox="0 0 360 220" accessible={false}>
      <Rect width={360} height={220} fill="#E9EEE5" />
      {[18, 80, 142, 204, 266, 328].map(horizontal => [15, 76, 137, 198].map(vertical => <Rect key={`${horizontal}-${vertical}`} x={horizontal} y={vertical} width={46} height={42} rx={9} fill="#DFE6DC" />))}
      <Path d="M342-20C285 45 360 75 336 135S285 170 330 250" fill="none" stroke="#BEDDDC" strokeWidth={37} />
      <Path d="M-30 201 380 63M105-30 215 260M-20 55H390M-20 120H320" fill="none" stroke="#FAFBF5" strokeWidth={14} />
      <Rect x={116} y={30} width={62} height={51} rx={18} fill="#BED6BD" />
      <SvgText x={126} y={49} fontSize={8} fill="#5B7765">CÔNG VIÊN</SvgText><SvgText x={133} y={62} fontSize={8} fill="#5B7765">TAO ĐÀN</SvgText>
      <SvgText x={9} y={111} fontSize={8} fill="#72837C">TRẦN HƯNG ĐẠO</SvgText><SvgText x={212} y={43} fontSize={8} fill="#72837C">QUẬN 1</SvgText>
      {destination && <Path d={`M${pickup[0]} ${pickup[1]} L${pickup[0]} 145 L${destination[0]} 145 L${destination[0]} ${destination[1]}`} fill="none" stroke={palette.teal} strokeWidth={5} strokeLinecap="round" strokeLinejoin="round" />}
      <Circle cx={pickup[0]} cy={pickup[1]} r={13} fill="#C5E3DE" /><Circle cx={pickup[0]} cy={pickup[1]} r={7} fill={palette.teal} stroke="white" strokeWidth={3} />
      {destination && <Circle cx={destination[0]} cy={destination[1]} r={9} fill={palette.coral} stroke="white" strokeWidth={3} />}
    </Svg>
    <View style={styles.mapLabel}><T style={styles.mapLabelText}>TP. HỒ CHÍ MINH · BẢN ĐỒ MINH HỌA</T></View>
  </View>;
}

export const styles = StyleSheet.create({
  text: { fontFamily: font.regular, fontSize: 14, lineHeight: 22, color: palette.ink },
  flex: { flex: 1 }, row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  page: { flex: 1, backgroundColor: palette.paper }, content: { padding: 20, paddingTop: 0, gap: 16, paddingBottom: 28 },
  title: { fontFamily: font.bold, fontSize: 27, lineHeight: 37, letterSpacing: -0.8 },
  subtitle: { fontFamily: font.bold, fontSize: 20, lineHeight: 29 },
  eyebrow: { fontFamily: font.bold, fontSize: 10, lineHeight: 17, letterSpacing: 1.5, color: palette.teal },
  caption: { fontSize: 11, lineHeight: 18, color: palette.muted }, muted: { color: palette.muted },
  card: { backgroundColor: palette.white, padding: 20, borderRadius: 24, gap: 17, borderWidth: 1, borderColor: '#E6ECE7' },
  button: { minHeight: 52, borderRadius: 15, paddingHorizontal: 17, paddingVertical: 13, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 10 },
  primary: { backgroundColor: palette.teal }, secondary: { backgroundColor: palette.white, borderWidth: 1, borderColor: palette.line },
  buttonText: { fontFamily: font.bold, fontSize: 13, lineHeight: 22, flexShrink: 1, textAlign: 'center' },
  note: { backgroundColor: palette.mint, borderRadius: 14, padding: 14, flexDirection: 'row', alignItems: 'flex-start', gap: 10 },
  noteText: { flex: 1, fontSize: 12, lineHeight: 21 }, demo: { borderWidth: 1, borderStyle: 'dashed', borderColor: '#B9CDCA', padding: 14, borderRadius: 18, gap: 10 },
  metric: { flex: 1, gap: 2 }, metricValue: { fontFamily: font.bold, fontSize: 20, lineHeight: 28 },
  route: { gap: 4 }, pickupDot: { width: 12, height: 12, borderRadius: 6, borderWidth: 3, borderColor: palette.teal },
  destinationDot: { width: 12, height: 12, borderRadius: 3, backgroundColor: palette.coral },
  routeDash: { height: 15, borderLeftWidth: 1, borderStyle: 'dashed', borderColor: '#B2C5C0', marginLeft: 5 },
  place: { fontFamily: font.medium, fontSize: 14, lineHeight: 22 },
  person: { flexDirection: 'row', alignItems: 'center', gap: 12 }, avatar: { width: 48, height: 48, borderRadius: 18, alignItems: 'center', justifyContent: 'center', backgroundColor: palette.mint },
  plate: { paddingVertical: 4, paddingHorizontal: 8, borderWidth: 1, borderColor: palette.line, borderRadius: 8 }, plateText: { fontSize: 10, lineHeight: 15, fontFamily: font.bold, textAlign: 'center' },
  modalBackdrop: { flex: 1, backgroundColor: '#082F3866', justifyContent: 'flex-end', alignItems: 'center' },
  sheet: { maxHeight: '90%', width: '100%', maxWidth: 560, backgroundColor: palette.white, borderTopLeftRadius: 28, borderTopRightRadius: 28, paddingHorizontal: 22 },
  sheetHandle: { width: 38, height: 4, backgroundColor: palette.line, borderRadius: 2, alignSelf: 'center', marginTop: 12, marginBottom: 8 },
  sheetBody: { gap: 14, paddingTop: 12, paddingBottom: 16 }, iconButton: { minWidth: 48, minHeight: 48, alignItems: 'center', justifyContent: 'center' },
  map: { marginHorizontal: -20, marginBottom: -34, overflow: 'hidden' }, mapLabel: { position: 'absolute', top: 12, left: 20, backgroundColor: '#FFFFFFED', paddingHorizontal: 10, paddingVertical: 3, borderRadius: 7 },
  mapLabelText: { fontSize: 8, lineHeight: 16, letterSpacing: 1, color: palette.muted },
  input: { minHeight: 54, backgroundColor: '#F6F8F5', borderWidth: 1, borderColor: palette.line, borderRadius: 13, paddingHorizontal: 15, paddingVertical: 12, fontFamily: font.regular, fontSize: 14, color: palette.ink },
  divider: { height: 1, backgroundColor: palette.line }, center: { alignItems: 'center', gap: 15 },
  heroIcon: { width: 76, height: 76, borderRadius: 28, backgroundColor: palette.mint, alignItems: 'center', justifyContent: 'center', marginVertical: 8 },
  fare: { fontFamily: font.bold, fontSize: 32, lineHeight: 42, color: palette.teal, letterSpacing: -1 },
});
