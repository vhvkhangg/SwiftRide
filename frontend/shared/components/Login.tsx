import { useState } from 'react';
import type { FormEvent } from 'react';
import type { Role } from '../lib/demo';
import { isDemoAccount } from '../lib/portal';
import { Button, Icon, Logo } from './ui';

export default function Login({ role, onLogin }: { role: Role; onLogin: () => void }) {
  const [account, setAccount] = useState('');
  const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false);
  const [error, setError] = useState('');
  function submit(event: FormEvent) {
    event.preventDefault();
    if (!isDemoAccount(role, account, password)) { setError(`Dùng tài khoản ${role === 'rider' ? 'hành khách' : 'tài xế'} mẫu bên dưới và mật khẩu đúng để đăng nhập.`); return; }
    onLogin();
  }
  return <main className="login-page">
    <section className="login-story"><div className="portal-brand"><Logo light /><span className="portal-label">{role === 'rider' ? 'RIDER' : 'DRIVER'}</span></div><div className="login-story-copy"><span className="eyebrow">{role === 'rider' ? 'THÀNH PHỐ CỦA BẠN. NHỊP ĐIỆU CỦA BẠN.' : 'VỮNG TAY LÁI. ĐÓN HÀNH TRÌNH MỚI.'}</span><h1>{role === 'rider' ? <>Đi cùng<br />nhịp sống</> : <>Cùng bạn<br />trên mọi nẻo đường</>}<span>.</span></h1><p>{role === 'rider' ? 'Từ góc phố quen đến điểm hẹn mới.' : 'Nhận chuyến, đón khách và theo dõi hành trình.'}<br />SwiftRide đồng hành trên từng hành trình.</p></div>
      <div className="login-map" aria-hidden="true"><svg viewBox="0 0 640 330"><defs><pattern id="login-grid" width="100" height="85" patternUnits="userSpaceOnUse" patternTransform="rotate(-24)"><rect x="8" y="8" width="83" height="67" rx="10" fill="none" stroke="#ffffff" strokeOpacity=".12" /></pattern></defs><rect width="640" height="330" fill="url(#login-grid)" /><path d="M80 240 190 190 230 250 410 170 370 110 530 35" fill="none" stroke="#a1d7cb" strokeWidth="5" strokeLinecap="round" /><circle cx="80" cy="240" r="10" fill="#fff" stroke="#087f8c" strokeWidth="5" /><circle cx="530" cy="35" r="10" fill="#e7c393" /><g transform="translate(320 190)"><rect x="-25" y="-25" width="50" height="50" rx="16" fill="white" /><g transform="translate(-14 -14)" color="#087f8c"><Icon name="bike" size={28} /></g></g></svg><div className="login-float"><span className="status-dot" /><div><strong>Mỗi chuyến đi, một kết nối.</strong><span>Rider & Driver · Cùng một hành trình</span></div></div></div>
      <div className="login-story-footer"><span>ĐƯỢC THIẾT KẾ CHO NHỮNG CHUYẾN ĐI MỖI NGÀY</span><Icon name="arrow" /></div>
    </section>
    <section className="login-form-area"><div className="login-topline"><span className="edition">SWIFTRIDE / {role.toUpperCase()}</span><span className="demo-badge"><span />Bản demo</span></div><div className="login-form-wrap"><span className="welcome-icon"><Icon name={role === 'rider' ? 'pin' : 'bike'} size={25} /></span><h2>{role === 'rider' ? 'Đăng nhập hành khách' : 'Đăng nhập tài xế'}</h2><p className="muted">{role === 'rider' ? 'Chọn điểm đến và bắt đầu chuyến đi của bạn.' : 'Sẵn sàng nhận chuyến và đồng hành cùng khách hàng.'}</p>
      <form onSubmit={submit}><label htmlFor="account">Tài khoản</label><input id="account" type="email" autoComplete="username" placeholder="Nhập tài khoản của bạn" required value={account} onChange={event => { setAccount(event.target.value); setError(''); }} /><label htmlFor="password">Mật khẩu</label><div className="password-field"><input id="password" type={visible ? 'text' : 'password'} autoComplete="current-password" placeholder="Nhập mật khẩu" required value={password} onChange={event => { setPassword(event.target.value); setError(''); }} /><button type="button" aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={visible} onClick={() => setVisible(!visible)}><Icon name="eye" /></button></div>{error && <p className="form-error" role="alert">{error}</p>}<Button type="submit" icon="arrow">Đăng nhập</Button></form>
      <div className="demo-accounts"><div className="divider-label"><span>KHÁM PHÁ GIAO DIỆN</span></div><p>Tài khoản mẫu dành riêng cho {role === 'rider' ? 'hành khách' : 'tài xế'}.</p><div className="demo-account-buttons"><button onClick={() => { setAccount(`${role}@swiftride.demo`); setPassword('demo1234'); setError(''); }}><Icon name={role === 'rider' ? 'pin' : 'bike'} /><span>Điền tài khoản {role === 'rider' ? 'Rider' : 'Driver'} mẫu</span><Icon name="arrow" size={16} /></button></div><small>Mật khẩu mẫu: <code>demo1234</code></small></div>
    </div><p className="login-disclaimer"><Icon name="shield" size={16} />Bản xem trước độc lập. Không xác thực hay thanh toán thật.</p></section>
  </main>;
}
