import { useEffect, useId, useRef } from 'react';
import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { findPlace, money, quoteFor } from '../lib/demo';
import type { DemoState, Role } from '../lib/demo';

const paths = {
  arrow: 'M5 12h14m-5-5 5 5-5 5', back: 'M19 12H5m5-5-5 5 5 5',
  pin: 'M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0Z M15 10a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  location: 'M12 2v3m0 14v3M2 12h3m14 0h3M19 12a7 7 0 1 1-14 0 7 7 0 0 1 14 0M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  clock: 'M12 8v5l3 2M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
  check: 'm5 12 4 4L19 6', close: 'm6 6 12 12M6 18 18 6',
  chevron: 'm9 5 7 7-7 7', down: 'm6 9 6 6 6-6',
  shield: 'm12 2 8 3v6c0 5-8 11-8 11S4 16 4 11V5l8-3Zm-4 9 3 3 5-5',
  info: 'M12 11v6m0-10v.01M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0',
  bike: 'M7 18a3 3 0 1 1-6 0 3 3 0 0 1 6 0m16 0a3 3 0 1 1-6 0 3 3 0 0 1 6 0M20 18 16 5h-4m4 3h4M4 18h9l3-7H8l-4 7M3 9h7M8 9v2',
  eye: 'M2 12s3-7 10-7 10 7 10 7-3 7-10 7S2 12 2 12Zm13 0a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  logout: 'M9 4H4v16h5m0-8h13m-5-5 5 5-5 5',
  layers: 'm12 3 10 6-10 6L2 9l10-6ZM2 13l10 6 10-6M2 17l10 6 10-6',
  plus: 'M12 5v14M5 12h14', minus: 'M5 12h14',
  receipt: 'M5 2h14v20l-3-2-4 2-4-2-3 2V2Zm3 5h8M8 11h8M8 15h4',
  search: 'm16 16 5 5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
  swap: 'M8 3v18m-4-4 4 4 4-4M16 21V3m-4 4 4-4 4 4',
  wallet: 'M20 8V4H5a3 3 0 0 0 0 6h16v11H5a3 3 0 0 1-3-3V7m19 6h-6v5h6m-3-2.5h.01',
  spark: 'm12 2 3 7 7 3-7 3-3 7-3-7-7-3 7-3 3-7',
};

export type IconName = keyof typeof paths;
export function Icon({ name, size = 20, className = '' }: { name: IconName; size?: number; className?: string }) {
  return <svg className={className} width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>;
}

export function Logo({ light = false }: { light?: boolean }) {
  return <span className={`logo ${light ? 'logo-light' : ''}`}><svg width="33" height="36" viewBox="0 0 36 38" aria-hidden="true"><path d="M31 7H15a8 8 0 0 0 0 16h7a3 3 0 0 1 0 6H6M4 16h17a8 8 0 0 1 0 16H7" fill="none" stroke="currentColor" strokeWidth="5.5" strokeLinecap="round" /></svg><span>Swift<span>Ride</span><span className="logo-period">.</span></span></span>;
}

export function Avatar({ role, size = 'normal' }: { role: Role; size?: 'normal' | 'small' }) {
  const driver = role === 'driver';
  return <svg className={`avatar avatar-${size}`} viewBox="0 0 64 64" role="img" aria-label={driver ? 'Ảnh minh họa Minh Anh' : 'Ảnh minh họa Khánh'}>
    <rect width="64" height="64" rx="32" fill={driver ? '#d5ebe5' : '#f5e9d5'} />
    <path d="M7 64c0-18 11-27 25-27s25 9 25 27" fill={driver ? '#145c63' : '#c28952'} />
    <path d="M25 34h14v14c-4 5-10 5-14 0" fill="#d89573" />
    <ellipse cx="32" cy="27" rx="14" ry="17" fill="#ecc1a0" />
    <path d={driver ? 'M18 28C11 10 23 5 32 7c18-4 18 14 15 23l-5-13c-5 6-16 0-21 4Z' : 'M18 28C10 12 21 3 35 6c14 0 17 14 11 24l-3-12-10-5-12 9Z'} fill="#24393b" />
    <path d="M25 28h1m12 0h1m-11 9q4 3 8 0" fill="none" stroke="#624538" strokeWidth="1.6" strokeLinecap="round" />
    {!driver && <path d="M20 25h10v7H20Zm14 0h10v7H34Zm-4 2h4" fill="none" stroke="#24393b" strokeWidth="1.5" />}
  </svg>;
}

export function Button({ children, kind = 'primary', icon, className = '', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { kind?: 'primary' | 'secondary' | 'text' | 'danger'; icon?: IconName }) {
  return <button type="button" className={`button button-${kind} ${className}`} {...props}>{children}{icon && <Icon name={icon} size={19} />}</button>;
}

export function Badge({ children, tone = 'teal' }: { children: ReactNode; tone?: 'teal' | 'amber' | 'gray' }) {
  return <span className={`badge badge-${tone}`}><span className="status-dot" />{children}</span>;
}

export function Title({ eyebrow, title, subtitle }: { eyebrow?: string; title: string; subtitle?: string }) {
  return <div className="section-heading">{eyebrow && <div className="eyebrow">{eyebrow}</div>}<h1>{title}</h1>{subtitle && <p>{subtitle}</p>}</div>;
}

export function RouteSummary({ state, compact = false }: { state: DemoState; compact?: boolean }) {
  const pickup = findPlace(state.pickup);
  const destination = findPlace(state.destination || 'market');
  return <div className={`route-summary ${compact ? 'route-compact' : ''}`}>
    <div className="route-stop"><span className="stop-dot" /><div><span className="field-caption">ĐIỂM ĐÓN</span><strong>{pickup.name}</strong>{!compact && <small>{pickup.address}</small>}</div></div>
    <div className="route-stop"><span className="stop-square" /><div><span className="field-caption">ĐIỂM ĐẾN</span><strong>{destination.name}</strong>{!compact && <small>{destination.address}</small>}</div></div>
  </div>;
}

export function Fare({ state, label = 'Giá chuyến', large = false }: { state: DemoState; label?: string; large?: boolean }) {
  return <div className={`fare ${large ? 'fare-large' : ''}`}><span>{label}</span><strong>{money(quoteFor(state).fare)}</strong></div>;
}

export function Person({ role, plate = false }: { role: Role; plate?: boolean }) {
  return <div className="person"><Avatar role={role} /><div className="person-name"><strong>{role === 'driver' ? 'Minh Anh' : 'Khánh'}</strong><small>{role === 'driver' ? 'Honda Vision · Trắng' : 'Hành khách của bạn'}</small></div>{plate && <span className="plate">59-S1<br />123.45</span>}</div>;
}

export function Notice({ children, tone = 'info' }: { children: ReactNode; tone?: 'info' | 'warning' }) {
  return <div className={`notice notice-${tone}`}><Icon name={tone === 'warning' ? 'clock' : 'info'} size={19} /><div>{children}</div></div>;
}

export function Modal({ title, children, onClose, wide = false }: { title: string; children: ReactNode; onClose: () => void; wide?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); return () => { dialog?.close(); }; }, []);
  return <dialog ref={ref} className={`modal ${wide ? 'modal-wide' : ''}`} aria-labelledby={titleId} onCancel={onClose} onClick={event => { if (event.target === event.currentTarget) onClose(); }}>
    <div className="modal-inner"><div className="modal-heading"><h2 id={titleId}>{title}</h2><button className="icon-button" onClick={onClose} aria-label="Đóng"><Icon name="close" /></button></div>{children}</div>
  </dialog>;
}

export function SafetyNote({ children = 'Kiểm tra đúng biển số trước khi lên xe.' }: { children?: ReactNode }) {
  return <p className="safety-note"><Icon name="shield" size={17} />{children}</p>;
}
