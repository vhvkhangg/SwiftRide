import { useState } from 'react';
import { LinearGradient } from 'expo-linear-gradient';
import { Pressable, ScrollView, StyleSheet, TextInput, View } from 'react-native';
import type { TextInputProps } from 'react-native';
import type { Role } from '../domain';
import { Button, Card, Eyebrow, font, Icon, Note, palette, styles, T, Title } from '../ui';
import { useSession } from './session';
import type { Call } from './session';
import type { Claims } from './trip';
import { emptyRegistration, minPasswordLength, registerRider, registrationError } from './register';
import type { Registration } from './register';
import RiderApi from './RiderApi';
import DriverApi from './DriverApi';

export type ApiScreenProps = { call: Call; claims: Claims };

export default function ApiApp({ role, gatewayUrl, issuer }: { role: Role; gatewayUrl: string; issuer: string }) {
  const session = useSession(role, gatewayUrl, issuer);
  const [tab, setTab] = useState<'trip' | 'account'>('trip');
  const required = role === 'rider' ? 'Rider' : 'Driver';
  if (!session.claims) return <ApiLogin role={role} ready={session.ready} busy={session.busy} error={session.error} issuer={issuer} gatewayUrl={gatewayUrl} onSignIn={session.signIn} />;
  if (!session.claims.roles.includes(required)) return <ScrollView contentContainerStyle={[styles.content, { paddingTop: 40 }]}>
    <Eyebrow>SWIFTRIDE · {role.toUpperCase()}</Eyebrow><Title>Sai loại tài khoản</Title>
    <Note warning>{role === 'driver'
      ? `${session.claims.email} không phải tài khoản tài xế. Tài khoản tài xế do quản trị viên SwiftRide cấp.`
      : `${session.claims.email} không phải tài khoản hành khách. Đăng xuất rồi đăng nhập bằng tài khoản hành khách hoặc đăng ký mới.`}</Note>
    <Button title="Đăng xuất" variant="secondary" icon="logout" onPress={session.signOut} />
  </ScrollView>;
  const props = { call: session.call, claims: session.claims };
  return <View style={styles.page}>
    <View style={shell.header}><View style={styles.flex}><T style={shell.brand}>SwiftRide<T style={{ color: palette.teal }}>.</T></T><Eyebrow>{role === 'rider' ? 'RIDER · HÀNH KHÁCH' : 'DRIVER · TÀI XẾ'}</Eyebrow></View></View>
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={[styles.content, { paddingTop: 12 }]}>
      {/* Keep the trip screen mounted so polling and local state survive switching tabs. */}
      <View style={[{ gap: 16 }, tab !== 'trip' && { display: 'none' }]}>{role === 'rider' ? <RiderApi {...props} /> : <DriverApi {...props} />}</View>
      {tab === 'account' && <>
        <Eyebrow>TÀI KHOẢN</Eyebrow><Title>{`Chào ${session.claims.name || session.claims.email}.`}</Title>
        <Card><View style={styles.row}><View style={styles.avatar}><Icon name="user" /></View><View style={styles.flex}><T style={styles.subtitle}>{session.claims.roles.filter(r => ['Rider', 'Driver', 'Admin'].includes(r)).join(' · ')}</T><T selectable style={styles.caption}>{session.claims.email}</T></View></View>
          <View style={styles.divider} /><T style={styles.caption}>{`Mã người dùng (sub): ${session.claims.sub}`}</T><T style={styles.caption}>{`Gateway: ${gatewayUrl}`}</T></Card>
        <Note>Phiên đăng nhập chỉ giữ trong bộ nhớ của app. Đăng xuất sẽ kết thúc phiên; chuyến đang chạy không bị hủy.</Note>
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

type LoginProps = { role: Role; ready: boolean; busy: boolean; error: string; issuer: string; gatewayUrl: string; onSignIn: (loginHint?: string) => Promise<void> };

function ApiLogin({ role, ready, busy, error, issuer, gatewayUrl, onSignIn }: LoginProps) {
  const driver = role === 'driver';
  // Only Riders can sign up in the app; Driver accounts are created by an administrator.
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [registered, setRegistered] = useState('');
  return <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={{ paddingBottom: 24 }}>
    <LinearGradient colors={driver ? ['#073F48', '#12747D'] : ['#087F8C', '#07606B']} style={login.hero}>
      <View style={styles.row}><T style={login.brand}>SwiftRide.</T><View style={login.badge}><T style={login.badgeText}>{role.toUpperCase()}</T></View></View>
      <T style={login.heroTitle}>{driver ? 'Cầm lái.\nChủ động mỗi ngày.' : 'Đi cùng\nnhịp sống.'}</T>
    </LinearGradient>
    <View style={login.form}>
      {mode === 'register' && !driver
        ? <RegisterForm gatewayUrl={gatewayUrl} onDone={email => { setRegistered(email); setMode('login'); }} onCancel={() => setMode('login')} />
        : <>
          <Eyebrow>{driver ? 'TÀI XẾ' : 'HÀNH KHÁCH'}</Eyebrow><Title>Đăng nhập</Title>
          {registered
            ? <Note>{`Đã tạo tài khoản ${registered}. Bấm Đăng nhập và nhập mật khẩu vừa đặt.`}</Note>
            : <T style={styles.muted}>Bạn sẽ được chuyển tới trang đăng nhập bảo mật của SwiftRide.</T>}
          {!!error && <Note warning>{error}</Note>}
          {!ready && <Note warning>{`Đang kết nối máy chủ đăng nhập (${issuer}). Nếu thông báo này không mất, kiểm tra backend đã chạy.`}</Note>}
          <Button title="Đăng nhập" icon="arrow" disabled={!ready} busy={busy} onPress={() => onSignIn(registered || undefined)} />
          {driver
            ? <T style={styles.caption}>Tài khoản tài xế do quản trị viên SwiftRide cấp. Tài khoản dev: driver.demo@example.com, driver.other@example.com (mật khẩu xem README).</T>
            : <>
              <View style={login.or}><View style={login.line} /><T style={styles.caption}>Chưa có tài khoản?</T><View style={login.line} /></View>
              <Button title="Đăng ký" variant="secondary" icon="user" disabled={busy} onPress={() => { setRegistered(''); setMode('register'); }} />
              <T style={styles.caption}>Tài khoản dev: rider.demo@example.com, rider.other@example.com (mật khẩu xem README).</T>
            </>}
        </>}
    </View>
  </ScrollView>;
}

function RegisterForm({ gatewayUrl, onDone, onCancel }: { gatewayUrl: string; onDone: (email: string) => void; onCancel: () => void }) {
  const [form, setForm] = useState<Registration>(emptyRegistration);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const set = (key: keyof Registration) => (value: string) => { setForm(current => ({ ...current, [key]: value })); setError(''); };
  const submit = async () => {
    const problem = registrationError(form);
    if (problem) { setError(problem); return; }
    setBusy(true);
    try { onDone(await registerRider(gatewayUrl, form)); } catch (e) { setError(e instanceof Error ? e.message : 'Chưa tạo được tài khoản. Thử lại.'); } finally { setBusy(false); }
  };
  return <>
    <Eyebrow>HÀNH KHÁCH MỚI</Eyebrow><Title>Đăng ký</Title>
    <View style={styles.row}>
      <Field label="Họ" value={form.lastName} onChangeText={set('lastName')} autoComplete="family-name" textContentType="familyName" style={styles.flex} />
      <Field label="Tên" value={form.firstName} onChangeText={set('firstName')} autoComplete="given-name" textContentType="givenName" style={styles.flex} />
    </View>
    <Field label="Email" value={form.email} onChangeText={set('email')} autoComplete="email" textContentType="emailAddress" keyboardType="email-address" autoCapitalize="none" />
    <Field label={`Mật khẩu (ít nhất ${minPasswordLength} ký tự)`} value={form.password} onChangeText={set('password')} secureTextEntry autoComplete="new-password" textContentType="newPassword" autoCapitalize="none" />
    <Field label="Nhập lại mật khẩu" value={form.confirm} onChangeText={set('confirm')} secureTextEntry autoComplete="new-password" textContentType="newPassword" autoCapitalize="none" onSubmitEditing={submit} returnKeyType="done" />
    {!!error && <Note warning>{error}</Note>}
    <Button title="Tạo tài khoản" icon="arrow" busy={busy} onPress={submit} />
    <Button title="Đã có tài khoản? Đăng nhập" variant="secondary" disabled={busy} onPress={onCancel} />
  </>;
}

function Field({ label, style, ...props }: TextInputProps & { label: string }) {
  return <View style={[{ gap: 6 }, style]}>
    <T style={login.label}>{label}</T>
    <TextInput accessibilityLabel={label} placeholderTextColor={palette.muted} autoCorrect={false} maxLength={254} {...props} style={styles.input} />
  </View>;
}

const shell = StyleSheet.create({
  header: { paddingHorizontal: 22, paddingVertical: 11, flexDirection: 'row', alignItems: 'center', backgroundColor: palette.white },
  brand: { fontFamily: font.bold, fontSize: 23, lineHeight: 31, letterSpacing: -1 },
  tabs: { flexDirection: 'row', borderTopWidth: 1, borderTopColor: palette.line, backgroundColor: palette.white, paddingTop: 8, paddingBottom: 4 }, tab: { flex: 1, minHeight: 52, alignItems: 'center', justifyContent: 'center', gap: 3 },
});
const login = StyleSheet.create({
  hero: { padding: 25, paddingTop: 28, minHeight: 230, gap: 28, borderBottomLeftRadius: 32, borderBottomRightRadius: 32 },
  brand: { color: palette.white, fontFamily: font.bold, fontSize: 26, lineHeight: 36, letterSpacing: -1 },
  badge: { backgroundColor: '#FFFFFF25', paddingHorizontal: 10, paddingVertical: 3, borderRadius: 7 }, badgeText: { color: palette.white, fontFamily: font.bold, fontSize: 9, letterSpacing: 2 },
  heroTitle: { fontFamily: font.bold, color: palette.white, fontSize: 34, lineHeight: 45, letterSpacing: -1.5 },
  form: { padding: 24, paddingTop: 28, gap: 14 },
  label: { fontFamily: font.medium, fontSize: 13, color: palette.ink },
  or: { flexDirection: 'row', alignItems: 'center', gap: 10, marginTop: 4 }, line: { flex: 1, height: 1, backgroundColor: palette.line },
});
