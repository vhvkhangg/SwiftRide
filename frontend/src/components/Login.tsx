import { useState } from 'react';
import type { FormEvent } from 'react';
import type { Role } from '../lib/demo';
import { Button, Icon, Logo } from './ui';

export default function Login({ onLogin }: { onLogin: (role: Role) => void }) {
  const [account, setAccount] = useState('');
  const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false);
  const [error, setError] = useState('');
  function submit(event: FormEvent) {
    event.preventDefault();
    const role = account.trim().toLowerCase() === 'rider@swiftride.demo' ? 'rider' : account.trim().toLowerCase() === 'driver@swiftride.demo' ? 'driver' : null;
    if (!role || password !== 'demo1234') { setError('Tài khoản hoặc mật khẩu chưa đúng. Dùng tài khoản demo bên dưới nhé.'); return; }
    onLogin(role);
  }
  return <main className="login-page">
    <section className="login-story"><Logo light /><div className="login-story-copy"><span className="eyebrow">THÀNH PHỐ CỦA BẠN. NHỊP ĐIỆU CỦA BẠN.</span><h1>Đi cùng<br />nhịp sống<span>.</span></h1><p>Từ góc phố quen đến điểm hẹn mới.<br />SwiftRide đồng hành trên từng hành trình.</p></div>
      <div className="login-map" aria-hidden="true"><svg viewBox="0 0 640 330"><defs><pattern id="login-grid" width="100" height="85" patternUnits="userSpaceOnUse" patternTransform="rotate(-24)"><rect x="8" y="8" width="83" height="67" rx="10" fill="none" stroke="#ffffff" strokeOpacity=".12" /></pattern></defs><rect width="640" height="330" fill="url(#login-grid)" /><path d="M80 240 190 190 230 250 410 170 370 110 530 35" fill="none" stroke="#a1d7cb" strokeWidth="5" strokeLinecap="round" /><circle cx="80" cy="240" r="10" fill="#fff" stroke="#087f8c" strokeWidth="5" /><circle cx="530" cy="35" r="10" fill="#e7c393" /><g transform="translate(320 190)"><rect x="-25" y="-25" width="50" height="50" rx="16" fill="white" /><g transform="translate(-14 -14)" color="#087f8c"><Icon name="bike" size={28} /></g></g></svg><div className="login-float"><span className="status-dot" /><div><strong>Mỗi chuyến đi, một kết nối.</strong><span>Rider & Driver · Cùng một hành trình</span></div></div></div>
      <div className="login-story-footer"><span>ĐƯỢC THIẾT KẾ CHO NHỮNG CHUYẾN ĐI MỖI NGÀY</span><Icon name="arrow" /></div>
    </section>
    <section className="login-form-area"><div className="login-topline"><span className="edition">SWIFTRIDE / WEB EXPERIENCE</span><span className="demo-badge"><span />Bản demo</span></div><div className="login-form-wrap"><span className="welcome-icon"><Icon name="spark" size={25} /></span><h2>Chào mừng trở lại</h2><p className="muted">Đăng nhập để tiếp tục hành trình của bạn.</p>
      <form onSubmit={submit}><label htmlFor="account">Tài khoản</label><input id="account" type="email" autoComplete="username" placeholder="Nhập tài khoản của bạn" required value={account} onChange={event => { setAccount(event.target.value); setError(''); }} /><label htmlFor="password">Mật khẩu</label><div className="password-field"><input id="password" type={visible ? 'text' : 'password'} autoComplete="current-password" placeholder="Nhập mật khẩu" required value={password} onChange={event => { setPassword(event.target.value); setError(''); }} /><button type="button" aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={visible} onClick={() => setVisible(!visible)}><Icon name="eye" /></button></div>{error && <p className="form-error" role="alert">{error}</p>}<Button type="submit" icon="arrow">Đăng nhập</Button></form>
      <div className="demo-accounts"><div className="divider-label"><span>KHÁM PHÁ GIAO DIỆN</span></div><p>Chọn tài khoản mẫu để điền nhanh.</p><div className="demo-account-buttons">{(['rider', 'driver'] as const).map(role => <button key={role} onClick={() => { setAccount(`${role}@swiftride.demo`); setPassword('demo1234'); setError(''); }}><Icon name={role === 'rider' ? 'pin' : 'bike'} /><span>{role === 'rider' ? 'Tôi là hành khách' : 'Tôi là tài xế'}</span><Icon name="arrow" size={16} /></button>)}</div><small>Mật khẩu mẫu: <code>demo1234</code></small></div>
    </div><p className="login-disclaimer"><Icon name="shield" size={16} />Bản xem trước độc lập. Không xác thực hay thanh toán thật.</p></section>
  </main>;
}
