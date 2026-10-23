import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, TextInput, View } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { validLogin } from './domain';
import type { Role } from './domain';
import { Button, Eyebrow, font, Icon, Note, palette, styles, T } from './ui';

export default function Login({ role, onLogin }: { role: Role; onLogin: () => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false);
  const [error, setError] = useState('');
  const driver = role === 'driver';
  function submit() {
    if (!validLogin(email, password, role)) { setError(`Dùng tài khoản ${role}@swiftride.demo và mật khẩu demo1234.`); return; }
    onLogin();
  }
  return <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : 'height'} style={styles.flex}>
    <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={loginStyles.content}>
      <LinearGradient colors={driver ? ['#073F48', '#12747D'] : ['#087F8C', '#07606B']} style={loginStyles.hero}>
        <View style={styles.row}><T style={loginStyles.brand}>SwiftRide.</T><View style={loginStyles.badge}><T style={loginStyles.badgeText}>{role.toUpperCase()}</T></View></View>
        <View style={loginStyles.orbit} /><View style={loginStyles.orbitSmall} />
        <View style={loginStyles.heroCopy}><T style={loginStyles.heroTitle}>{driver ? 'Cầm lái.\nChủ động mỗi ngày.' : 'Đi cùng\nnhịp sống.'}</T><T style={loginStyles.heroSubtitle}>{driver ? 'Một kết nối mới. Một hành trình tốt.' : 'Từ góc phố quen đến điểm hẹn mới.'}</T></View>
        <View style={loginStyles.heroFooter}><Icon name={driver ? 'bike' : 'pin'} color="#C6EAE0" /><T style={loginStyles.heroSubtitle}>{driver ? 'ỨNG DỤNG DÀNH CHO TÀI XẾ' : 'ỨNG DỤNG DÀNH CHO HÀNH KHÁCH'}</T></View>
      </LinearGradient>
      <View style={loginStyles.form}>
        <Eyebrow>CHÀO MỪNG TRỞ LẠI</Eyebrow><T accessibilityRole="header" style={styles.title}>{driver ? 'Đăng nhập tài xế' : 'Đăng nhập hành khách'}</T>
        <T style={styles.muted}>{driver ? 'Sẵn sàng đón những hành trình mới?' : 'Điểm đến tiếp theo của bạn là đâu?'}</T>
        <T style={styles.place}>Tài khoản</T><TextInput accessibilityLabel="Tài khoản" value={email} onChangeText={value => { setEmail(value); setError(''); }} placeholder={`${role}@swiftride.demo`} placeholderTextColor={palette.muted} keyboardType="email-address" autoCapitalize="none" autoCorrect={false} autoComplete="email" style={styles.input} />
        <T style={styles.place}>Mật khẩu</T><View><TextInput accessibilityLabel="Mật khẩu" value={password} onChangeText={value => { setPassword(value); setError(''); }} placeholder="Nhập mật khẩu" placeholderTextColor={palette.muted} secureTextEntry={!visible} autoCapitalize="none" autoCorrect={false} autoComplete="current-password" returnKeyType="go" onSubmitEditing={submit} style={[styles.input, loginStyles.passwordInput]} /><Pressable accessibilityRole="button" accessibilityLabel={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} onPress={() => setVisible(!visible)} style={[styles.iconButton, loginStyles.passwordToggle]}><Icon name={visible ? 'eyeOff' : 'eye'} /></Pressable></View>
        {!!error && <T accessibilityRole="alert" style={{ color: palette.coral }}>{error}</T>}
        <Button title="Đăng nhập" icon="arrow" onPress={submit} />
        <Button title="Điền tài khoản demo" variant="secondary" onPress={() => { setEmail(`${role}@swiftride.demo`); setPassword('demo1234'); setError(''); }} />
        <Note>Demo giao diện, không xác thực thật. Không nhập mật khẩu cá nhân.</Note>
      </View>
    </ScrollView>
  </KeyboardAvoidingView>;
}

const loginStyles = StyleSheet.create({
  passwordInput: { paddingRight: 56 },
  passwordToggle: { position: 'absolute', right: 4, top: 0, bottom: 0, width: 48 },
  content: { paddingBottom: 24 }, hero: { padding: 25, paddingTop: 28, minHeight: 290, gap: 28, overflow: 'hidden', borderBottomLeftRadius: 32, borderBottomRightRadius: 32 },
  brand: { color: palette.white, fontFamily: font.bold, fontSize: 26, lineHeight: 36, letterSpacing: -1 },
  badge: { backgroundColor: '#FFFFFF25', paddingHorizontal: 10, paddingVertical: 3, borderRadius: 7 }, badgeText: { color: palette.white, fontFamily: font.bold, fontSize: 9, letterSpacing: 2 },
  heroCopy: { gap: 10 }, heroTitle: { fontFamily: font.bold, color: palette.white, fontSize: 34, lineHeight: 45, letterSpacing: -1.5 }, heroSubtitle: { color: '#D2E7E2', fontSize: 11, lineHeight: 19, flexShrink: 1 },
  heroFooter: { flexDirection: 'row', alignItems: 'center', gap: 10 }, form: { padding: 24, paddingTop: 28, gap: 14 },
  orbit: { position: 'absolute', width: 245, height: 245, borderRadius: 125, borderWidth: 1, borderColor: '#FFFFFF20', right: -120, top: 48 },
  orbitSmall: { position: 'absolute', width: 185, height: 185, borderRadius: 95, borderWidth: 25, borderColor: '#FFFFFF08', right: -90, top: 78 },
});
