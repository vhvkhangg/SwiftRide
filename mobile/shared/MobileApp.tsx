import { useEffect, useRef, useState } from 'react';
import type { ComponentRef, Dispatch, ReactNode } from 'react';
import { ActivityIndicator, BackHandler, Platform, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { SafeAreaProvider, SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { useFonts } from 'expo-font';
import { BeVietnamPro_400Regular } from '@expo-google-fonts/be-vietnam-pro/400Regular';
import { BeVietnamPro_500Medium } from '@expo-google-fonts/be-vietnam-pro/500Medium';
import { BeVietnamPro_700Bold } from '@expo-google-fonts/be-vietnam-pro/700Bold';
import { isActive, statusLabels, tripId } from './domain';
import type { Action, Role, Status, Trip } from './domain';
import { useTrip } from './useTrip';
import Login from './Login';
import TripActions from './TripActions';
import { readConfig } from './config';
import { Button, Card, Eyebrow, font, Icon, Note, palette, Sheet, styles, T, Title } from './ui';

export type Screen = { code: string; label: string; status: Status };
export type Confirm = (title: string, message: string, onConfirm: () => void) => void;
export type TripScreenProps = { trip: Trip; dispatch: Dispatch<Action>; confirm: Confirm };
type Props = { role: Role; screens: Screen[]; renderTrip: (props: TripScreenProps) => ReactNode };
const config = readConfig(process.env.EXPO_PUBLIC_APP_MODE, process.env.EXPO_PUBLIC_GATEWAY_URL);

export default function MobileApp(props: Props) {
  const [loaded, fontError] = useFonts({ BeVietnamPro_400Regular, BeVietnamPro_500Medium, BeVietnamPro_700Bold });
  return <SafeAreaProvider><StatusBar style="dark" /><SafeAreaView style={shell.safe} edges={['top', 'bottom']}>
    {!loaded && !fontError ? <ActivityIndicator accessibilityLabel="Đang tải ứng dụng" style={styles.flex} color={palette.teal} /> : config.error || config.mode === 'api' ? <ScrollView contentContainerStyle={[styles.content, { paddingTop: 40 }]}><Eyebrow>SWIFTRIDE · {props.role.toUpperCase()}</Eyebrow><Title>Chưa thể kết nối API</Title><Note warning>{config.error || 'Chế độ API chưa nối Auth và chuyến đi. Cần contract từ BE trước khi sử dụng; không đăng nhập demo hoặc tạo dữ liệu giả trong chế độ này.'}</Note><T style={styles.caption}>Kiểm tra EXPO_PUBLIC_APP_MODE và EXPO_PUBLIC_GATEWAY_URL, sau đó khởi động lại Expo. Dùng EXPO_PUBLIC_APP_MODE=demo để xem giao diện mô phỏng.</T></ScrollView> : <AppContent {...props} />}
  </SafeAreaView></SafeAreaProvider>;
}

function AppContent({ role, screens, renderTrip }: Props) {
  const { trip, dispatch, ready, canSave, storageError, retryLoad, retrySave, saveFailed, saving } = useTrip(role);
  const [signedIn, setSignedIn] = useState(false);
  const [tab, setTab] = useState<'trip' | 'account'>('trip');
  const [showDemo, setShowDemo] = useState(false);
  const [confirmation, setConfirmation] = useState<{ title: string; message: string; accept: () => void } | null>(null);
  const scroll = useRef<ComponentRef<typeof ScrollView>>(null);
  const confirm: Confirm = (title, message, accept) => { setConfirmation({ title, message, accept }); setShowDemo(true); };
  useEffect(() => { scroll.current?.scrollTo({ y: 0, animated: false }); }, [trip.status, tab]);
  useEffect(() => {
    if (Platform.OS !== 'android') return;
    const handler = BackHandler.addEventListener('hardwareBackPress', () => {
      if (!signedIn) return false;
      if (tab === 'account') { setTab('trip'); return true; }
      if (trip.status === 'quoted') { dispatch({ type: 'edit' }); return true; }
      if (isActive(trip)) {
        setConfirmation({ title: 'Thoát ứng dụng?', message: 'Thoát không hủy chuyến. Tiến trình demo được lưu trên thiết bị nếu bộ nhớ hoạt động bình thường.', accept: () => BackHandler.exitApp() });
        setShowDemo(true);
        return true;
      }
      return false;
    });
    return () => handler.remove();
  }, [signedIn, tab, trip, dispatch]);
  if (!ready) return <ActivityIndicator accessibilityLabel="Đang khôi phục chuyến" style={styles.flex} color={palette.teal} />;
  if (!canSave) return <View style={[styles.content, { paddingTop: 40 }]}><Title>Chưa đọc được dữ liệu</Title><Note warning>{storageError}</Note><Button title="Thử đọc lại" onPress={retryLoad} /></View>;
  if (!signedIn) return <Login role={role} onLogin={() => setSignedIn(true)} />;
  return <View style={styles.page}>
    <View style={shell.header}><View style={styles.flex}><T style={shell.brand}>SwiftRide<T style={{ color: palette.teal }}>.</T></T><Eyebrow>{role === 'rider' ? 'RIDER · HÀNH KHÁCH' : 'DRIVER · TÀI XẾ'}</Eyebrow></View><Pressable accessibilityRole="button" accessibilityLabel="Kịch bản demo" onPress={() => { setConfirmation(null); setShowDemo(true); }} style={shell.demoButton}><Icon name="layers" /><T style={styles.caption}>Demo</T></Pressable></View>
    <ScrollView ref={scroll} keyboardShouldPersistTaps="handled" contentContainerStyle={[styles.content, tab === 'account' && { paddingTop: 20 }]}>
      {!!storageError && <Note warning>{storageError}</Note>}
      {saveFailed && <Button title="Thử lưu lại" variant="secondary" busy={saving} onPress={retrySave} />}
      {tab === 'trip' ? renderTrip({ trip, dispatch, confirm }) : <>
        <Eyebrow>KHÔNG GIAN CỦA BẠN</Eyebrow><Title>{role === 'rider' ? 'Chào Khánh.' : 'Chào Minh Anh.'}</Title>
        <Card><View style={styles.row}><View style={styles.avatar}><Icon name="user" /></View><View style={styles.flex}><T style={styles.subtitle}>{role === 'rider' ? 'Hành khách' : 'Tài xế'}</T><T selectable style={styles.caption}>{role}@swiftride.demo</T></View></View><View style={styles.divider} /><T>{role === 'rider' ? 'SwiftRide Bike · Di chuyển trong thành phố' : 'Honda Vision · Trắng · 59-S1 123.45'}</T></Card>
        <Note>Đây là tài khoản demo riêng của app {role === 'rider' ? 'Rider' : 'Driver'}. Chưa kết nối xác thực, GPS hoặc thanh toán thật.</Note>
        <Button title="Quay lại chuyến đi" onPress={() => setTab('trip')} />
        <Button title="Đăng xuất" variant="secondary" icon="logout" onPress={() => confirm('Đăng xuất?', 'Chuyến demo vẫn giữ trên thiết bị. Đăng xuất không hủy chuyến.', () => { setSignedIn(false); setTab('trip'); })} />
      </>}
      <T style={[styles.caption, { textAlign: 'center', marginTop: 6 }]}>Dữ liệu minh họa · Không có giao dịch thật</T>
    </ScrollView>
    {tab === 'trip' && <View style={shell.actions}><TripActions role={role} trip={trip} dispatch={dispatch} confirm={confirm} /></View>}
    <View accessibilityRole="tablist" style={shell.tabs}>
      {(['trip', 'account'] as const).map(item => <Pressable key={item} accessibilityRole="tab" accessibilityLabel={item === 'trip' ? 'Chuyến đi' : 'Tài khoản'} accessibilityState={{ selected: tab === item }} onPress={() => setTab(item)} style={shell.tab}>
        <Icon name={item === 'trip' ? (role === 'driver' ? 'bike' : 'home') : 'user'} color={tab === item ? palette.teal : palette.muted} /><T style={[styles.caption, tab === item && { color: palette.teal, fontFamily: font.bold }]}>{item === 'trip' ? 'Chuyến đi' : 'Tài khoản'}</T>
      </Pressable>)}
    </View>
    <Sheet title={confirmation?.title ?? 'Kịch bản demo'} visible={showDemo} onClose={() => setShowDemo(false)}>
      {confirmation ? <>
        <T>{confirmation.message}</T><Button title={`Xác nhận: ${confirmation.title.replace(/\?$/, '')}`} onPress={() => { setShowDemo(false); confirmation.accept(); }} /><Button title="Quay lại" variant="secondary" onPress={() => setShowDemo(false)} />
      </> : <><Note warning>Chọn màn hình sẽ thay thế chuyến demo hiện tại. Hai app không đồng bộ với nhau.</Note>
      <T style={styles.caption}>{tripId(trip)} · {statusLabels[trip.status]}</T>
      {screens.map(screen => <Button key={screen.code} variant="secondary" title={`${screen.code} · ${screen.label}`} onPress={() => {
        setShowDemo(false);
        const change = () => { dispatch({ type: 'preview', status: screen.status }); setTab('trip'); };
        if (isActive(trip)) confirm('Thay kịch bản?', 'Tiến trình chuyến demo hiện tại sẽ bị thay thế.', change); else change();
      }} />)}</>}
    </Sheet>
  </View>;
}

const shell = StyleSheet.create({
  safe: { flex: 1, backgroundColor: palette.white, width: '100%', maxWidth: 560, alignSelf: 'center' },
  actions: { paddingHorizontal: 20, paddingVertical: 8, backgroundColor: palette.white },
  header: { paddingHorizontal: 22, paddingVertical: 11, flexDirection: 'row', alignItems: 'center', backgroundColor: palette.white },
  brand: { fontFamily: font.bold, fontSize: 23, lineHeight: 31, letterSpacing: -1 }, demoButton: { minWidth: 48, minHeight: 48, alignItems: 'center', justifyContent: 'center' },
  tabs: { flexDirection: 'row', borderTopWidth: 1, borderTopColor: palette.line, backgroundColor: palette.white, paddingTop: 8, paddingBottom: 4 }, tab: { flex: 1, minHeight: 52, alignItems: 'center', justifyContent: 'center', gap: 3 },
});
