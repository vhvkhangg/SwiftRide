export function readConfig(mode = 'demo', gatewayUrl = '') {
  if (mode !== 'demo' && mode !== 'api') return { mode, gatewayUrl: '', error: 'EXPO_PUBLIC_APP_MODE phải là demo hoặc api.' };
  if (mode === 'demo') return { mode, gatewayUrl: '', error: '' };
  try {
    const url = new URL(gatewayUrl);
    if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash) throw new Error();
    return { mode, gatewayUrl: url.href.replace(/\/$/, ''), error: '' };
  } catch {
    return { mode, gatewayUrl: '', error: 'Chế độ API cần EXPO_PUBLIC_GATEWAY_URL là URL HTTP(S), không chứa mật khẩu, query hoặc fragment.' };
  }
}
