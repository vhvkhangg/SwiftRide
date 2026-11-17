function httpUrl(value: string) {
  const url = new URL(value);
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error();
  return url.href.replace(/\/$/, '');
}

export const defaultKeycloakIssuer = 'http://localhost:8180/realms/swiftride';

export function readConfig(mode = 'demo', gatewayUrl = '', keycloakIssuer = '') {
  if (mode !== 'demo' && mode !== 'api') return { mode, gatewayUrl: '', keycloakIssuer: '', error: 'EXPO_PUBLIC_APP_MODE phải là demo hoặc api.' };
  if (mode === 'demo') return { mode, gatewayUrl: '', keycloakIssuer: '', error: '' };
  let gateway: string;
  try { gateway = httpUrl(gatewayUrl); } catch {
    return { mode, gatewayUrl: '', keycloakIssuer: '', error: 'Chế độ API cần EXPO_PUBLIC_GATEWAY_URL là URL HTTP(S), không chứa mật khẩu, query hoặc fragment.' };
  }
  try { return { mode, gatewayUrl: gateway, keycloakIssuer: httpUrl(keycloakIssuer || defaultKeycloakIssuer), error: '' }; } catch {
    return { mode, gatewayUrl: '', keycloakIssuer: '', error: 'EXPO_PUBLIC_KEYCLOAK_ISSUER phải là URL realm Keycloak, ví dụ http://localhost:8180/realms/swiftride.' };
  }
}
