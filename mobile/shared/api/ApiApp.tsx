import { useState } from 'react';
import { LinearGradient } from 'expo-linear-gradient';
import { Pressable, ScrollView, StyleSheet, View } from 'react-native';
import type { Role } from '../domain';
import { Button, Card, Eyebrow, font, Icon, Note, palette, styles, T, Title } from '../ui';
import { useSession } from './session';
import type { Call } from './session';
import type { Claims } from './trip';
import RiderApi from './RiderApi';
import DriverApi from './DriverApi';

export type ApiScreenProps = { call: Call; claims: Claims };

export default function ApiApp({ role, gatewayUrl, issuer }: { role: Role; gatewayUrl: string; issuer: string }) {
  const session = useSession(role, gatewayUrl, issuer);
  const [tab, setTab] = useState<'trip' | 'account'>('trip');
  const required = role === 'rider' ? 'Rider' : 'Driver';
  if (!session.claims) return <ApiLogin role={role} ready={session.ready} busy={session.busy} error={session.error} issuer={issuer} onSignIn={session.signIn} />;
  if (!session.claims.roles.includes(required)) return <ScrollView contentContainerStyle={[styles.content, { paddingTop: 40 }]}>
    <Eyebrow>SWIFTRIDE · {role.toUpperCase()}</Eyebrow><Title>Sai loại tài khoản</Title>
    <Note warning>{`${session.claims.email} không có quyền ${required}. App ${required} chỉ dùng tài khoản có role ${required} trong Keycloak.`}</Note>
    <Button title="Đăng xuất" variant="secondary" icon="logout" onPress={session.signOut} />
  </ScrollView>;
  const props = { call: session.call, claims: session.claims };
  return <View style={styles.page}>
    <View style={shell.header}><View style={styles.flex}><T style={shell.brand}>SwiftRide<T style={{ color: palette.teal }}>.</T></T><Eyebrow>{role === 'rider' ? 'RIDER · HÀNH KHÁCH' : 'DRIVER · TÀI XẾ'}</Eyebrow></View><View style={shell.live}><T style={shell.liveText}>API</T></View></View>
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={[styles.content, { paddingTop: 12 }]}>
      {/* Keep the trip screen mounted so polling and local state survive switching tabs. */}
      <View style={[{ gap: 16 }, tab !== 'trip' && { display: 'none' }]}>{role === 'rider' ? <RiderApi {...props} /> : <DriverApi {...props} />}</View>
      {tab === 'account' && <>
        <Eyebrow>TÀI KHOẢN KEYCLOAK</Eyebrow><Title>{`Chào ${session.claims.name || session.claims.email}.`}</Title>
        <Card><View style={styles.row}><View style={styles.avatar}><Icon name="user" /></View><View style={styles.flex}><T style={styles.subtitle}>{session.claims.roles.filter(r => ['Rider', 'Driver', 'Admin'].includes(r)).join(' · ')}</T><T selectable style={styles.caption}>{session.claims.email}</T></View></View>
          <View style={styles.divider} /><T style={styles.caption}>{`Mã người dùng (sub): ${session.claims.sub}`}</T><T style={styles.caption}>{`Gateway: ${gatewayUrl}`}</T></Card>
        <Note>Token do Keycloak cấp, chỉ giữ trong bộ nhớ của app. Đăng xuất sẽ thu hồi refresh token; chuyến đang chạy không bị hủy.</Note>
        <Button title="Đăng xuất" variant="secondary" icon="logout" onPress={async () => { await session.signOut(); setTab('trip'); }} />
      </>}
    </ScrollView>
    <View accessibilityRole="tablist" style={shell.tabs}>
      {(['trip', 'account'] as const).map(item => <Pressable key={item} accessibilityRole="tab" accessibilityLabel={item === 'trip' ? 'Chuyến đi' : 'Tài khoản'} accessibilityState={{ selected: tab === item }} onPress={() => setTab(item)} style={shell.tab}>
        <Icon name={item === 'trip' ? (role === 'driver' ? 'bike' : 'home') : 'user'} color={tab === item ? palette.teal : palette.muted} /><T style={[styles.caption, tab === item && { color: palette.teal, fontFamily: font.bold }]}>{item === 'trip' ? 'Chuyến đi' : 'Tài khoản'}</T>
      </Pressable>)}
    </View>
  </View>;
}

function ApiLogin({ role, ready, busy, error, issuer, onSignIn }: { role: Role; ready: boolean; busy: boolean; error: string; issuer: string; onSignIn: () => Promise<void> }) {
  const driver = role === 'driver';
  return <ScrollView contentContainerStyle={{ paddingBottom: 24 }}>
    <LinearGradient colors={driver ? ['#073F48', '#12747D'] : ['#087F8C', '#07606B']} style={login.hero}>
      <View style={styles.row}><T style={login.brand}>SwiftRide.</T><View style={login.badge}><T style={login.badgeText}>{role.toUpperCase()}</T></View></View>
      <T style={login.heroTitle}>{driver ? 'Cầm lái.\nChủ động mỗi ngày.' : 'Đi cùng\nnhịp sống.'}</T>
    </LinearGradient>
    <View style={login.form}>
      <Eyebrow>ĐĂNG NHẬP BẰNG KEYCLOAK</Eyebrow><Title>{driver ? 'Đăng nhập tài xế' : 'Đăng nhập hành khách'}</Title>
      <T style={styles.muted}>Trang đăng nhập của Keycloak sẽ mở ra. App không nhận và không lưu mật khẩu của bạn.</T>
      {!!error && <Note warning>{error}</Note>}
      {!ready && <Note warning>{`Đang kết nối Keycloak tại ${issuer}. Nếu thông báo này không mất, kiểm tra container keycloak đã chạy.`}</Note>}
      <Button title="Đăng nhập với Keycloak" icon="arrow" disabled={!ready} busy={busy} onPress={onSignIn} />
      <T style={styles.caption}>{driver ? 'Tài khoản dev: driver.demo@example.com hoặc driver.other@example.com (mật khẩu xem README).' : 'Tài khoản dev: rider.demo@example.com, rider.other@example.com, hoặc đăng ký tài khoản Rider mới.'}</T>
    </View>
  </ScrollView>;
}

const shell = StyleSheet.create({
  header: { paddingHorizontal: 22, paddingVertical: 11, flexDirection: 'row', alignItems: 'center', backgroundColor: palette.white },
  brand: { fontFamily: font.bold, fontSize: 23, lineHeight: 31, letterSpacing: -1 },
  live: { backgroundColor: palette.mint, paddingHorizontal: 10, paddingVertical: 3, borderRadius: 7 }, liveText: { fontFamily: font.bold, fontSize: 10, letterSpacing: 2, color: palette.teal },
  tabs: { flexDirection: 'row', borderTopWidth: 1, borderTopColor: palette.line, backgroundColor: palette.white, paddingTop: 8, paddingBottom: 4 }, tab: { flex: 1, minHeight: 52, alignItems: 'center', justifyContent: 'center', gap: 3 },
});
const login = StyleSheet.create({
  hero: { padding: 25, paddingTop: 28, minHeight: 230, gap: 28, borderBottomLeftRadius: 32, borderBottomRightRadius: 32 },
  brand: { color: palette.white, fontFamily: font.bold, fontSize: 26, lineHeight: 36, letterSpacing: -1 },
  badge: { backgroundColor: '#FFFFFF25', paddingHorizontal: 10, paddingVertical: 3, borderRadius: 7 }, badgeText: { color: palette.white, fontFamily: font.bold, fontSize: 9, letterSpacing: 2 },
  heroTitle: { fontFamily: font.bold, color: palette.white, fontSize: 34, lineHeight: 45, letterSpacing: -1.5 },
  form: { padding: 24, paddingTop: 28, gap: 14 },
});
