// In-app Rider sign-up. The app only collects the form; the Gateway creates the account
// (POST /api/auth/register -> Keycloak Admin API) and the new account always gets the Rider role.
// Signing in afterwards still goes through the standard login page (Authorization Code + PKCE).
import { GatewayError, requestGateway } from '../gateway';

export type Registration = { lastName: string; firstName: string; email: string; password: string; confirm: string };
export const emptyRegistration: Registration = { lastName: '', firstName: '', email: '', password: '', confirm: '' };
export const minPasswordLength = 12;

/** Same rules as the Gateway/realm policy; returns the first problem in form order, or '' when valid. */
export function registrationError(form: Registration): string {
  const email = form.email.trim();
  if (!form.lastName.trim()) return 'Nhập họ của bạn.';
  if (!form.firstName.trim()) return 'Nhập tên của bạn.';
  if (form.lastName.trim().length > 100 || form.firstName.trim().length > 100) return 'Họ và tên tối đa 100 ký tự mỗi phần.';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email) || email.length > 254) return 'Email không hợp lệ.';
  if (form.password.length < minPasswordLength) return `Mật khẩu cần ít nhất ${minPasswordLength} ký tự.`;
  if (form.password.length > 128) return 'Mật khẩu tối đa 128 ký tự.';
  if (form.password.toLowerCase() === email.toLowerCase()) return 'Mật khẩu không được trùng email.';
  if (form.password !== form.confirm) return 'Mật khẩu nhập lại không khớp.';
  return '';
}

/** Creates the account; resolves with the normalized email, throws an Error with a user-facing message. */
export async function registerRider(gatewayUrl: string, form: Registration): Promise<string> {
  const email = form.email.trim();
  try {
    await requestGateway(gatewayUrl, '/api/auth/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password: form.password, firstName: form.firstName.trim(), lastName: form.lastName.trim() }),
    });
    return email;
  } catch (error) {
    if (!(error instanceof GatewayError)) throw new Error('Chưa tạo được tài khoản. Thử lại.');
    if (error.status === 409) throw new Error('Email này đã được đăng ký. Hãy đăng nhập.');
    if (error.status === 429) throw new Error('Bạn thử đăng ký quá nhiều lần. Đợi vài phút rồi thử lại.');
    if (error.status === 400 && error.detail) throw new Error(error.detail);
    if (error.status) throw new Error(error.detail ?? 'Chưa tạo được tài khoản lúc này. Thử lại sau ít phút.');
    // Network error / timeout: the account may exist already; trying again answers 409 in that case.
    throw new Error(`${error.message} Nếu thử lại báo email đã tồn tại thì tài khoản đã được tạo.`);
  }
}
