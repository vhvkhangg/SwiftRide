import { useState } from 'react';
import { findPlace, places } from '../lib/demo';
import type { DemoState } from '../lib/demo';
import { Icon } from './ui';

const streets = [
  'M-100 90 1300 90', 'M-100 160 1300 160', 'M-100 240 1300 240', 'M-100 320 1300 320',
  'M-100 420 1300 420', 'M-100 520 1300 520', 'M-100 610 1300 610', 'M-100 700 1300 700',
  'M160-100V900', 'M280-100V900', 'M390-100V900', 'M490-100V900', 'M600-100V900',
  'M720-100V900', 'M835-100V900', 'M940-100V900', 'M1050-100V900',
];

export default function CityMap({ state, onChoose }: { state: DemoState; onChoose?: (id: string) => void }) {
  const [zoom, setZoom] = useState(1);
  const pickup = findPlace(state.pickup);
  const destination = findPlace(state.destination || 'market');
  const hasRoute = !['idle', 'failed', 'cancelled'].includes(state.status);
  const approaching = ['accepted', 'enroute'].includes(state.status);
  const showDriver = ['accepted', 'enroute', 'pickedup'].includes(state.status) || state.role === 'driver';
  const vehicle: [number, number] = approaching ? [pickup.position[0] + 110, pickup.position[1] + 100] : state.status === 'pickedup' ? [720, destination.position[1]] : [600, 520];
  const tripRoute = `M${pickup.position[0]} ${pickup.position[1]} H600 V320 H${destination.position[0]} V${destination.position[1]}`;
  const approachRoute = `M${vehicle[0]} ${vehicle[1]} V${pickup.position[1]} H${pickup.position[0]}`;
  const route = approaching ? approachRoute : tripRoute;

  return <section className="city-map" aria-label="Bản đồ minh họa hành trình, không phải GPS trực tiếp">
    <svg className="map-art" viewBox="340 50 770 590" preserveAspectRatio="xMidYMid slice">
      <defs>
        <pattern id="city-blocks" width="120" height="100" patternUnits="userSpaceOnUse"><rect width="120" height="100" fill="#f1f2ef" /><rect x="9" y="9" width="43" height="35" rx="4" fill="#e8eae5" stroke="#e0e4de" /><rect x="60" y="9" width="50" height="35" rx="4" fill="#e9eae5" stroke="#e0e4de" /><rect x="9" y="52" width="68" height="38" rx="4" fill="#e8ebe7" stroke="#e0e4de" /><rect x="85" y="52" width="25" height="38" rx="3" fill="#e5e9e3" /></pattern>
        <filter id="map-shadow" x="-50%" y="-50%" width="200%" height="200%"><feDropShadow dx="0" dy="3" stdDeviation="5" floodOpacity="0.15" /></filter>
      </defs>
      <rect x="-500" y="-500" width="2500" height="2000" fill="url(#city-blocks)" />
      <g transform={`translate(725 350) scale(${zoom}) translate(-725 -350)`}>
        <path d="M1160-100C945 30 1200 150 1100 270S1070 400 1240 480 1130 640 1000 770" stroke="#bed9dc" strokeWidth="100" fill="none" />
        <path d="M1160-100C945 30 1200 150 1100 270S1070 400 1240 480 1130 640 1000 770" stroke="#c9e2e4" strokeWidth="82" fill="none" />
        <rect x="618" y="110" width="90" height="119" rx="10" fill="#d5e3ce" />
        <rect x="736" y="176" width="78" height="50" rx="7" fill="#d9e5d1" />
        <path d="M360 557h174l54 38H367Z" fill="#d3e2ca" />
        <g stroke="#daddd7" strokeWidth="15" fill="none">{streets.map(path => <path key={path} d={path} />)}</g>
        <g stroke="#fffefa" strokeWidth="11" fill="none">{streets.map(path => <path key={path} d={path} />)}</g>
        <path d="M300 660 1010-30M150 550 930 770" stroke="#daddd7" strokeWidth="21" fill="none" />
        <path d="M300 660 1010-30M150 550 930 770" stroke="#fffefa" strokeWidth="16" fill="none" />
        <g className="map-street-labels" fill="#9ca59e" fontSize="10" textAnchor="middle">
          <text x="830" y="84">NGUYỄN THỊ MINH KHAI</text><text x="770" y="313">LÊ LỢI</text>
          <text x="480" y="414">AN DƯƠNG VƯƠNG</text><text x="770" y="513">TRẦN HƯNG ĐẠO</text>
          <text x="840" y="603">VÕ VĂN KIỆT</text><text transform="translate(713 240) rotate(-90)">TRƯƠNG ĐỊNH</text>
          <text transform="translate(934 402) rotate(-90)">NAM KỲ KHỞI NGHĨA</text>
        </g>
        <g fill="#a7b0a5" fontSize="21" fontWeight="500" letterSpacing="5"><text x="420" y="280">QUẬN 5</text><text x="795" y="450">QUẬN 1</text></g>
        <g fill="#89a17d" fontSize="11"><text x="636" y="149">CÔNG VIÊN</text><text x="640" y="166">TAO ĐÀN</text><text x="750" y="198">DINH ĐỘC LẬP</text></g>
        <text x="1080" y="330" transform="rotate(65 1080 330)" fill="#89aeb3" fontSize="14" letterSpacing="3">SÔNG SÀI GÒN</text>
        {state.status === 'searching' && <g className="search-rings" transform={`translate(${pickup.position[0]} ${pickup.position[1]})`}><circle r="100" fill="#087f8c" opacity=".05" /><circle r="67" fill="#087f8c" opacity=".07" /><circle r="36" fill="#087f8c" opacity=".1" /></g>}
        {hasRoute && <><path d={route} stroke="white" strokeWidth="10" fill="none" strokeLinejoin="round" /><path className="route-line" d={route} stroke="#087f8c" strokeWidth="5" fill="none" strokeLinejoin="round" strokeLinecap="round" /></>}
        {places.map(place => {
          const isPickup = place.id === state.pickup;
          const isDestination = place.id === state.destination;
          const active = isPickup || isDestination;
          return <g key={place.id} className={onChoose ? 'map-place clickable' : 'map-place'} transform={`translate(${place.position[0]} ${place.position[1]})`} role={onChoose ? 'button' : undefined} tabIndex={onChoose ? 0 : undefined} aria-label={`Chọn ${place.name} làm điểm đến`} onClick={() => onChoose?.(place.id)} onKeyDown={event => { if (onChoose && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); onChoose(place.id); } }}>
            <circle r={active ? 20 : 14} fill="white" filter="url(#map-shadow)" />
            {isDestination ? <path d="M0 11S-8 2-8-3a8 8 0 1 1 16 0c0 5-8 14-8 14Z" fill="#de8265" /> : <circle r={isPickup ? 11 : 5} fill={isPickup ? '#087f8c' : '#a9b4aa'} />}
            {isPickup && <circle r="5" fill="white" />}
            {active && <g filter="url(#map-shadow)"><rect x="-76" y="-57" width="152" height="27" rx="7" fill="white" /><text y="-39" textAnchor="middle" fontSize="10.5" fontWeight="600" fill="#243f42">{place.name}</text></g>}
          </g>;
        })}
        {showDriver && <g transform={`translate(${vehicle[0]} ${vehicle[1]})`}><circle r="24" fill="#fff" filter="url(#map-shadow)" /><g transform="translate(-13 -13)" color="#087f8c"><Icon name="bike" size={26} /></g><circle cx="18" cy="-18" r="5" fill="#087f8c" stroke="white" strokeWidth="2" /></g>}
      </g>
    </svg>
    <div className="map-heading"><span className="map-city"><span className="status-dot" />TP. Hồ Chí Minh<Icon name="down" size={15} /></span><span className="map-caption">Một hành trình mới đang chờ bạn.</span></div>
    <div className="map-controls"><button className="icon-button" aria-label="Phóng to bản đồ" disabled={zoom >= 1.5} onClick={() => setZoom(value => Math.min(1.5, value + .15))}><Icon name="plus" /></button><button className="icon-button" aria-label="Thu nhỏ bản đồ" disabled={zoom <= .85} onClick={() => setZoom(value => Math.max(.85, value - .15))}><Icon name="minus" /></button><button className="icon-button" aria-label="Đưa bản đồ về vị trí mẫu" onClick={() => setZoom(1)}><Icon name="location" /></button></div>
    <div className="map-bottom"><span><Icon name="layers" size={14} />Bản đồ minh họa · Không phải GPS trực tiếp</span><span>SWIFTRIDE CITY GUIDE</span></div>
    <div className="map-card"><span className="map-card-icon"><Icon name="bike" size={25} /></span><div><strong>Đi gần. Kết nối xa.</strong><small>Chuyến đi nhỏ, trải nghiệm khác biệt.</small></div></div>
  </section>;
}
