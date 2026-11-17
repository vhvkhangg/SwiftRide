function httpUrl(value: string) {
  const url = new URL(value);
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error();
  return url.href.replace(/\/$/, '');
}

export const defaultGatewayUrl = 'http://localhost:5100';
export const defaultKeycloakIssuer = 'http://localhost:8180/realms/swiftride';

/** Gateway and Keycloak addresses; defaults match docker compose on the same machine. */
export function readConfig(gatewayUrl = '', keycloakIssuer = '') {
  let gateway: string;
  try { gateway = httpUrl(gatewayUrl || defaultGatewayUrl); } catch {
    return { gatewayUrl: '', keycloakIssuer: '', error: 'EXPO_PUBLIC_GATEWAY_URL phải là URL HTTP(S), không chứa mật khẩu, query hoặc fragment.' };
  }
  try { return { gatewayUrl: gateway, keycloakIssuer: httpUrl(keycloakIssuer || defaultKeycloakIssuer), error: '' }; } catch {
    return { gatewayUrl: '', keycloakIssuer: '', error: 'EXPO_PUBLIC_KEYCLOAK_ISSUER phải là URL realm Keycloak, ví dụ http://localhost:8180/realms/swiftride.' };
  }
}
