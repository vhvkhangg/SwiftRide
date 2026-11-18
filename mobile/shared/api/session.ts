import { useCallback, useEffect, useRef, useState } from 'react';
import { Platform } from 'react-native';
import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';
import { requestGateway } from '../gateway';
import type { Role } from '../domain';
import { readClaims } from './trip';
import type { Claims } from './trip';

// Only matters if expo-auth-session ever opens a popup on web; our web sign-in uses a full-page redirect.
WebBrowser.maybeCompleteAuthSession();

export const clientId = 'swiftride-mobile';
const web = Platform.OS === 'web';
const pendingKey = 'swiftride-oidc-pending';
const scopes = ['openid', 'profile', 'email'];
type Tokens = { access: string; refresh: string | undefined; idToken: string | undefined; expiresAt: number };
export type Call = <T>(method: 'GET' | 'POST', path: string, body?: unknown) => Promise<T>;

/**
 * Keycloak Authorization Code + PKCE (client swiftride-mobile, no client secret).
 * Tokens stay in memory only: reloading the app asks Keycloak again instead of keeping a refresh token on disk.
 */
export function useSession(role: Role, gatewayUrl: string, issuer: string) {
  const discovery = AuthSession.useAutoDiscovery(issuer);
  // Web uses a full-page redirect (popups are blocked by many mobile browsers); native uses the system browser.
  const redirectUri = web ? `${window.location.origin}/` : AuthSession.makeRedirectUri({ scheme: `swiftride-${role}`, path: 'auth' });
  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    { clientId, redirectUri, scopes, usePKCE: true, prompt: AuthSession.Prompt.Login },
    discovery);
  const [tokens, setTokens] = useState<Tokens | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const current = useRef<Tokens | null>(null);
  const refreshing = useRef<Promise<string> | null>(null);
  current.current = tokens;

  const store = (result: AuthSession.TokenResponse) => {
    const next = { access: result.accessToken, refresh: result.refreshToken, idToken: result.idToken, expiresAt: (result.issuedAt + (result.expiresIn ?? 300)) * 1000 };
    current.current = next;
    setTokens(next);
    return next.access;
  };

  useEffect(() => {
    if (!response) return;
    if (response.type !== 'success') { setBusy(false); if (response.type === 'error') setError('Đăng nhập bị từ chối. Thử lại.'); return; }
    if (!discovery || !request?.codeVerifier) return;
    AuthSession.exchangeCodeAsync({ clientId, code: response.params.code, redirectUri, extraParams: { code_verifier: request.codeVerifier } }, discovery)
      .then(store)
      .catch(() => setError('Không đổi được mã đăng nhập lấy token. Thử đăng nhập lại.'))
      .finally(() => setBusy(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [response]);

  // Web: finish a redirect that came back with ?code=…&state=… (state must match the one we stored).
  useEffect(() => {
    if (!web || !discovery) return;
    const params = new URLSearchParams(window.location.search);
    const pending = sessionStorage.getItem(pendingKey);
    if (!params.has('code') && !params.has('error')) return;
    sessionStorage.removeItem(pendingKey);
    window.history.replaceState(null, '', '/');
    const saved = pending ? JSON.parse(pending) as { state: string; verifier: string } : null;
    if (params.has('error') || !saved || saved.state !== params.get('state')) { setError('Đăng nhập không hợp lệ hoặc đã bị hủy. Thử lại.'); return; }
    setBusy(true);
    AuthSession.exchangeCodeAsync({ clientId, code: params.get('code')!, redirectUri, extraParams: { code_verifier: saved.verifier } }, discovery)
      .then(store)
      .catch(() => setError('Không đổi được mã đăng nhập lấy token. Thử đăng nhập lại.'))
      .finally(() => setBusy(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [discovery]);

  /** `loginHint` pre-fills the email on the login page (used right after in-app sign-up). */
  const signIn = useCallback(async (loginHint?: string) => {
    setError('');
    setBusy(true);
    const extraParams = loginHint ? { login_hint: loginHint } : undefined;
    if (web && discovery) {
      const auth = new AuthSession.AuthRequest({ clientId, redirectUri, scopes, usePKCE: true, prompt: AuthSession.Prompt.Login, extraParams });
      const url = await auth.makeAuthUrlAsync(discovery);
      sessionStorage.setItem(pendingKey, JSON.stringify({ state: auth.state, verifier: auth.codeVerifier }));
      window.location.assign(url);
      return;
    }
    if (loginHint && discovery) {
      // The hook's request has fixed parameters, so a hinted login uses its own request and exchanges the code here.
      const auth = new AuthSession.AuthRequest({ clientId, redirectUri, scopes, usePKCE: true, prompt: AuthSession.Prompt.Login, extraParams });
      const result = await auth.promptAsync(discovery);
      if (result.type !== 'success') { setBusy(false); if (result.type === 'error') setError('Đăng nhập bị từ chối. Thử lại.'); return; }
      await AuthSession.exchangeCodeAsync({ clientId, code: result.params.code, redirectUri, extraParams: { code_verifier: auth.codeVerifier! } }, discovery)
        .then(store)
        .catch(() => setError('Không đổi được mã đăng nhập lấy token. Thử đăng nhập lại.'))
        .finally(() => setBusy(false));
      return;
    }
    const result = await promptAsync();
    if (result.type !== 'success') setBusy(false);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [promptAsync, discovery, redirectUri]);

  const signOut = useCallback(async () => {
    const value = current.current;
    current.current = null;
    setTokens(null);
    if (value?.refresh && discovery?.revocationEndpoint) await AuthSession.revokeAsync({ clientId, token: value.refresh }, discovery).catch(() => undefined);
    // Web: also end the Keycloak browser (SSO) session, otherwise the next login is locked to the same user.
    if (web && discovery?.endSessionEndpoint) {
      const query = new URLSearchParams({ client_id: clientId, post_logout_redirect_uri: redirectUri, ...(value?.idToken ? { id_token_hint: value.idToken } : {}) });
      window.location.assign(`${discovery.endSessionEndpoint}?${query}`);
    }
  }, [discovery, redirectUri]);

  /** Single-flight refresh: Keycloak rotates refresh tokens, so two parallel refreshes would log the user out. */
  const accessToken = useCallback(async () => {
    const value = current.current;
    if (!value) throw new Error('Chưa đăng nhập.');
    if (value.expiresAt - Date.now() > 30_000) return value.access;
    if (!value.refresh || !discovery) { setTokens(null); throw new Error('Phiên đăng nhập hết hạn.'); }
    refreshing.current ??= AuthSession.refreshAsync({ clientId, refreshToken: value.refresh }, discovery)
      .then(store)
      .catch(e => { current.current = null; setTokens(null); throw e; })
      .finally(() => { refreshing.current = null; });
    return refreshing.current;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [discovery]);

  const call: Call = useCallback(async <T,>(method: 'GET' | 'POST', path: string, body?: unknown) => {
    const token = await accessToken();
    return await requestGateway(gatewayUrl, path, {
      method,
      headers: { Authorization: `Bearer ${token}`, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }) },
      body: body === undefined ? undefined : JSON.stringify(body),
    }) as T;
  }, [accessToken, gatewayUrl]);

  const claims: Claims | null = tokens ? readClaims(tokens.access) : null;
  return { ready: !!request && !!discovery, busy, error, claims, signIn, signOut, call };
}
