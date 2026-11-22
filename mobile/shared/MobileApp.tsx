import { ActivityIndicator, ScrollView, StyleSheet } from 'react-native';
import { SafeAreaProvider, SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { useFonts } from 'expo-font';
import { BeVietnamPro_400Regular } from '@expo-google-fonts/be-vietnam-pro/400Regular';
import { BeVietnamPro_500Medium } from '@expo-google-fonts/be-vietnam-pro/500Medium';
import { BeVietnamPro_700Bold } from '@expo-google-fonts/be-vietnam-pro/700Bold';
import type { Role } from './domain';
import { readConfig } from './config';
import ApiApp from './api/ApiApp';
import { Eyebrow, Note, palette, styles, T, Title } from './ui';

const config = readConfig(process.env.EXPO_PUBLIC_GATEWAY_URL, process.env.EXPO_PUBLIC_KEYCLOAK_ISSUER);

export default function MobileApp({ role }: { role: Role }) {
  const [loaded, fontError] = useFonts({ BeVietnamPro_400Regular, BeVietnamPro_500Medium, BeVietnamPro_700Bold });
  return <SafeAreaProvider><StatusBar style="dark" /><SafeAreaView style={shell.safe} edges={['top', 'bottom']}>
    {!loaded && !fontError ? <ActivityIndicator accessibilityLabel="Đang tải ứng dụng" style={styles.flex} color={palette.teal} />
      : config.error ? <ScrollView contentContainerStyle={[styles.content, { paddingTop: 40 }]}><Eyebrow>SWIFTRIDE · {role.toUpperCase()}</Eyebrow><Title>Cấu hình chưa hợp lệ</Title><Note warning>{config.error}</Note><T style={styles.caption}>Sửa biến môi trường rồi khởi động lại Expo.</T></ScrollView>
      : <ApiApp role={role} gatewayUrl={config.gatewayUrl} issuer={config.keycloakIssuer} />}
  </SafeAreaView></SafeAreaProvider>;
}

const shell = StyleSheet.create({ safe: { flex: 1, backgroundColor: palette.white, width: '100%', maxWidth: 560, alignSelf: 'center' } });
