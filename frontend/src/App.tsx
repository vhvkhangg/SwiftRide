import { useEffect, useReducer, useRef, useState } from 'react';
import { demoReducer, initialState, paymentKey, readSnapshot, screens, storageKey, tripId } from './lib/demo';
import type { Role } from './lib/demo';
import CityMap from './components/CityMap';
import Login from './components/Login';
import Rider from './components/Rider';
import Driver from './components/Driver';
import type { Confirmation } from './components/Driver';
import { Avatar, Badge, Button, Fare, Icon, Logo, Modal, Notice, RouteSummary } from './components/ui';

function restore() {
  try { return readSnapshot(localStorage.getItem(storageKey)); } catch { return initialState; }
}

export default function App() {
  const [state, dispatch] = useReducer(demoReducer, undefined, restore);
  const [modal, setModal] = useState<'demo' | 'details' | 'account' | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [toast, setToast] = useState('');
  const [storageWarning, setStorageWarning] = useState(false);
  const panelRef = useRef<HTMLElement>(null);
  const firstRender = useRef(true);

  useEffect(() => {
    try { localStorage.setItem(storageKey, JSON.stringify(state)); setStorageWarning(false); } catch { setStorageWarning(true); }
  }, [state]);
  useEffect(() => {
    if (firstRender.current) { firstRender.current = false; return; }
    const panel = panelRef.current;
    if (panel) { panel.scrollTop = 0; panel.focus({ preventScroll: true }); }
    window.scrollTo({ top: 0, behavior: 'instant' });
  }, [state.status, state.role]);
  useEffect(() => { if (toast) { const timer = window.setTimeout(() => setToast(''), 4000); return () => window.clearTimeout(timer); } }, [toast]);

  const login = (role: Role) => { dispatch({ type: 'role', role }); setModal(null); };
  if (!state.role) return <Login onLogin={login} />;

  const paymentScene = ['droppedoff', 'payment_pending', 'paid', 'cancelled', 'failed'].includes(state.status);
  const step = ['idle', 'quoted'].includes(state.status) ? 0 : state.status === 'searching' ? 1 : ['accepted', 'enroute', 'pickedup'].includes(state.status) ? 2 : 3;
  const cancel = () => setConfirmation({ title: 'Bạn muốn hủy chuyến?', body: 'Yêu cầu hiện tại sẽ được kết thúc. Không phát sinh phí hủy trong bản demo này.', label: 'Xác nhận hủy', action: { type: 'cancel' }, danger: true });

  return <div className="app-shell">
    <a className="skip-link" href="#trip-panel">Đến nội dung chuyến đi</a>
    <header className="app-header"><Logo /><div className="header-separator" /><span className="header-tagline">Đi cùng nhịp sống.</span>
      <nav className="role-tabs" aria-label="Góc nhìn demo"><button aria-pressed={state.role === 'rider'} className={state.role === 'rider' ? 'active' : ''} onClick={() => login('rider')}><Icon name="pin" size={16} />Hành khách</button><button aria-pressed={state.role === 'driver'} className={state.role === 'driver' ? 'active' : ''} onClick={() => login('driver')}><Icon name="bike" size={18} />Tài xế</button></nav>
      <div className="header-actions"><button className="demo-trigger" onClick={() => setModal('demo')}><span className="status-dot" /><span>Kịch bản demo</span><Icon name="layers" size={16} /></button><button className="account-trigger" onClick={() => setModal('account')} aria-label="Tài khoản demo"><Avatar role={state.role} size="small" /><span>{state.role === 'rider' ? 'Khánh' : 'Minh Anh'}</span><Icon name="down" size={14} /></button></div>
    </header>
    <main className={`workspace ${paymentScene ? 'workspace-result' : ''}`}>
      <div className="panel-column"><div className="journey-topline"><span>{state.role === 'rider' ? 'CHUYẾN ĐI CỦA BẠN' : 'KHÔNG GIAN TÀI XẾ'}</span><span>{state.status === 'idle' ? 'SWIFTRIDE BIKE' : tripId(state)}</span></div>
        <section id="trip-panel" className={`trip-panel ${paymentScene ? 'result-panel' : ''}`} ref={panelRef} tabIndex={-1} aria-label={state.role === 'rider' ? 'Đặt và theo dõi chuyến đi' : 'Điều khiển chuyến đi của tài xế'}><div className="sheet-handle" />
          {state.role === 'rider' ? <Rider key={`${state.status}-${state.tripNumber}`} state={state} dispatch={dispatch} onCancel={cancel} onDetails={() => setModal('details')} /> : <Driver key={`${state.status}-${state.tripNumber}`} state={state} dispatch={dispatch} onConfirm={setConfirmation} />}
        </section>
        <div className="panel-footer"><Icon name="shield" size={14} /><span>Dữ liệu minh họa · Không có giao dịch thật</span></div>
      </div>
      <CityMap state={state} onChoose={state.role === 'rider' && state.status === 'idle' ? id => { if (id === state.pickup) { setToast('Chọn điểm đến khác điểm đón nhé.'); return; } dispatch({ type: 'place', field: 'destination', id }); } : undefined} />
      <div className="journey-track" aria-label="Tiến trình chuyến đi">{['Hành trình', 'Kết nối', 'Di chuyển', 'Hoàn tất'].map((label, index) => <div key={label} className={`${index === step ? 'current' : ''} ${index < step ? 'done' : ''}`}><span>{index < step ? <Icon name="check" size={12} /> : `0${index + 1}`}</span>{label}</div>)}</div>
    </main>
    {storageWarning && <div role="status" className="storage-warning">Trình duyệt không cho lưu tiến trình. Tải lại trang sẽ bắt đầu lại.</div>}
    {toast && <div className="toast" role="status"><Icon name="info" />{toast}</div>}
    {modal === 'account' && <Modal title="Tài khoản demo" onClose={() => setModal(null)}><div className="account-card"><Avatar role={state.role} /><div><h3>{state.role === 'rider' ? 'Khánh' : 'Minh Anh'}</h3><p>{state.role === 'rider' ? 'Hành khách' : 'Tài xế'} · {state.role}@swiftride.demo</p></div></div><Notice>Đây là lựa chọn vai trò để xem trước UI, không phải hệ thống xác thực hoặc phân quyền thật.</Notice><Button kind="secondary" icon="logout" onClick={() => { dispatch({ type: 'role', role: null }); setModal(null); }}>Đăng xuất demo</Button></Modal>}
    {modal === 'details' && <Modal title="Chi tiết chuyến đi" onClose={() => setModal(null)}><Badge>{tripId(state)}</Badge><RouteSummary state={state} /><Fare state={state} large /><div className="detail-row"><span>Phương tiện</span><strong>Honda Vision · Trắng</strong></div><div className="detail-row"><span>Tài xế</span><strong>Minh Anh · 59-S1 123.45</strong></div><SafetyDetail /><Button kind="secondary" onClick={() => setModal(null)}>Đóng chi tiết</Button></Modal>}
    {modal === 'demo' && <Modal title="Khám phá SwiftRide" onClose={() => setModal(null)} wide><p className="muted">Chọn màn hình để xem trước. Thao tác này thay thế kịch bản demo hiện tại; không gọi backend.</p><div className="screen-picker"><button onClick={() => { dispatch({ type: 'role', role: null }); setModal(null); }}><span>A01</span><strong>Đăng nhập</strong><Icon name="arrow" size={16} /></button>{screens.map(screen => <button key={screen.code} onClick={() => { dispatch({ type: 'preview', role: screen.role, status: screen.status }); setModal(null); }}><span>{screen.code}</span><strong>{screen.label}</strong><Icon name="arrow" size={16} /></button>)}</div><div className="scenario-actions"><Button kind="secondary" onClick={() => { dispatch({ type: 'preview', role: 'rider', status: 'failed' }); setModal(null); }}>Không có tài xế</Button><Button kind="secondary" onClick={() => { dispatch({ type: 'preview', role: 'rider', status: 'payment_pending' }); dispatch({ type: 'payment-error' }); setModal(null); }}>Lỗi thanh toán</Button></div><div className="demo-footnote"><Icon name="info" size={17} />Chuyển tab Hành khách / Tài xế để xem cùng một chuyến từ hai phía.</div></Modal>}
    {confirmation && <Modal title={confirmation.title} onClose={() => setConfirmation(null)}><p className="confirm-copy">{confirmation.body}</p><div className="button-row"><Button kind="secondary" onClick={() => setConfirmation(null)}>Quay lại</Button><Button kind={confirmation.danger ? 'danger' : 'primary'} onClick={() => { dispatch(confirmation.action); setConfirmation(null); }}>{confirmation.label}</Button></div></Modal>}
  </div>;

  function SafetyDetail() {
    return <p className="fine-print muted">Mã thanh toán mô phỏng: <code>{paymentKey(state)}</code>. Dữ liệu chỉ được lưu trên trình duyệt này.</p>;
  }
}
