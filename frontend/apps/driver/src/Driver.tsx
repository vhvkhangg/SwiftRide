import { useState } from 'react';
import type { Dispatch } from 'react';
import { findPlace, quoteFor, tripId } from '../../../shared/lib/demo';
import type { Action, DemoState } from '../../../shared/lib/demo';
import { Avatar, Badge, Button, Fare, Icon, Notice, Person, RouteSummary, SafetyNote, Title } from '../../../shared/components/ui';
import type { Confirmation } from '../../../shared/TripShell';

export default function Driver({ state, dispatch, onConfirm }: { state: DemoState; dispatch: Dispatch<Action>; onConfirm: (confirmation: Confirmation) => void }) {
  const [dismissed, setDismissed] = useState(false);
  const fare = quoteFor(state);

  if (['idle', 'quoted'].includes(state.status) || (state.status === 'searching' && dismissed && state.attempts > 0)) return <>
    <div className="driver-welcome"><Avatar role="driver" /><div><span className="eyebrow">CHÚC BẠN MỘT NGÀY TỐT LÀNH</span><h1>Chào Minh Anh.</h1></div></div>
    <p className="muted">Sẵn sàng cho những hành trình mới?</p>
    <div className="vehicle-card"><span className="vehicle-art"><Icon name="bike" size={43} /></span><div><strong>Honda Vision</strong><small>Trắng · SwiftRide Bike</small></div><span className="plate">59-S1<br />123.45</span></div>
    <div className={`availability ${!state.online ? 'availability-off' : ''}`}><span className="availability-icon"><Icon name={state.online ? 'location' : 'clock'} size={26} /></span><strong>{state.online ? 'Sẵn sàng nhận chuyến' : 'Bạn đang ngoại tuyến'}</strong><p>{state.online ? 'Bật kết nối, đón những cơ hội mới. Chuyến phù hợp sẽ xuất hiện tại đây.' : 'Nghỉ một chút nhé. Bật nhận chuyến khi bạn đã sẵn sàng.'}</p></div>
    <div className="switch-row"><div><strong>Nhận chuyến</strong><small>{state.online ? 'Đang bật' : 'Đang tắt'}</small></div><button type="button" role="switch" aria-checked={state.online} aria-label="Nhận chuyến" className={`switch ${state.online ? 'switch-on' : ''}`} onClick={() => dispatch({ type: 'online', value: !state.online })}><span /></button></div>
    <SafetyNote>Vị trí mẫu · Không theo dõi GPS thật.</SafetyNote>
    {dismissed && <Notice>Đã từ chối lời mời. {state.attempts}/3 lần thử trong kịch bản demo.</Notice>}
    <div className="simulation-step"><span>ĐIỀU KHIỂN DEMO</span><p>Tạo một lời mời mẫu để khám phá luồng tài xế.</p><button disabled={!state.online} onClick={() => { if (dismissed) setDismissed(false); else dispatch({ type: 'offer' }); }}>{dismissed ? 'Xem lời mời tiếp theo' : 'Mô phỏng chuyến mới'}<Icon name="arrow" size={15} /></button></div>
  </>;

  if (state.status === 'searching') return <>
    <Badge>Yêu cầu mới</Badge><Title title="Một hành trình đang chờ" subtitle="Có chuyến đi phù hợp ở gần bạn." />
    <div className="offer-distance"><span><Icon name="location" size={20} />Cách bạn khoảng</span><strong>0,8 <small>km</small></strong></div>
    <RouteSummary state={state} /><div className="trip-metrics"><span><Icon name="pin" size={16} />{fare.distance} km</span><span><Icon name="clock" size={16} />Khoảng {fare.minutes} phút</span><span><Icon name="bike" size={17} />Xe máy</span></div>
    <div className="quote-box"><Fare state={state} large /><p className="fine-print muted">Giá chuyến mô phỏng, không phải thu nhập thực nhận.</p></div>
    <div className="button-row"><Button kind="secondary" onClick={() => { dispatch({ type: 'reject' }); setDismissed(true); }}>Từ chối</Button><Button icon="arrow" onClick={() => dispatch({ type: 'accept' })}>Nhận chuyến</Button></div>
    <SafetyNote>Kiểm tra hành trình trước khi nhận chuyến.</SafetyNote>
  </>;

  if (state.status === 'accepted' || state.status === 'enroute') return <>
    <Badge>{state.status === 'accepted' ? 'Đã nhận chuyến' : 'Đang đến đón khách'}</Badge><Title title={state.status === 'accepted' ? 'Mình đi đón Khánh nhé!' : 'Đến điểm đón khách'} subtitle={state.status === 'accepted' ? 'Hành khách đang chờ bạn bắt đầu di chuyển.' : 'Điểm hẹn ở ngay phía trước.'} />
    <div className="eta"><Icon name="location" /><span>Khoảng 0,8 km</span><strong>3<small>phút</small></strong></div>
    <Person role="rider" />
    <div className="pickup-note emphasized"><Icon name="pin" size={24} /><div><span>ĐIỂM ĐÓN KHÁCH</span><strong>Cổng chính {findPlace(state.pickup).name}</strong><small>{findPlace(state.pickup).address}</small></div></div>
    <div className="destination-note"><Icon name="arrow" size={17} />Đến {findPlace(state.destination).name}</div>
    {state.status === 'accepted' ? <Button icon="arrow" onClick={() => dispatch({ type: 'depart' })}>Bắt đầu đi đón</Button> : <><SafetyNote>Chỉ xác nhận sau khi khách đã lên xe.</SafetyNote><Button icon="check" onClick={() => onConfirm({ title: 'Khách đã lên xe?', body: 'Xác nhận bạn đã đón đúng hành khách tại điểm hẹn. Chuyến đi sẽ chuyển sang đang thực hiện.', label: 'Xác nhận đã đón khách', action: { type: 'pickup' } })}>Đã đón khách</Button></>}
  </>;

  if (state.status === 'pickedup') return <>
    <Badge>Đang thực hiện chuyến</Badge><Title title={`Đến ${findPlace(state.destination).name}`} subtitle="Một hành trình an toàn, một trải nghiệm tốt." />
    <div className="destination-card"><span className="stop-square" /><div><span className="field-caption">ĐIỂM TRẢ KHÁCH</span><strong>{findPlace(state.destination).name}</strong><small>{findPlace(state.destination).address}</small></div></div>
    <Person role="rider" /><div className="trip-metrics"><span><Icon name="receipt" size={16} />{tripId(state)}</span><span><Icon name="pin" size={16} />{fare.distance} km</span></div><Fare state={state} large />
    <SafetyNote>Xác nhận sau khi đã trả khách đúng điểm đến.</SafetyNote><Button icon="check" onClick={() => onConfirm({ title: 'Đã đến điểm trả khách?', body: 'Chỉ hoàn tất chặng đường khi hành khách đã xuống xe an toàn. Thanh toán sẽ được xử lý ở bước tiếp theo.', label: 'Xác nhận đã trả khách', action: { type: 'dropoff' } })}>Đã trả khách</Button>
  </>;

  if (state.status === 'cancelled' || state.status === 'failed') return <>
    <div className="result-symbol symbol-neutral"><Icon name="info" size={35} /></div><Title title={state.status === 'cancelled' ? 'Chuyến đi đã được hủy' : 'Yêu cầu đã kết thúc'} subtitle="Không phát sinh thanh toán trong bản demo." /><RouteSummary state={state} /><Button onClick={() => dispatch({ type: 'reset' })}>Về trang chủ</Button>
  </>;

  const paid = state.status === 'paid';
  return <>
    <div className="result-symbol"><Icon name="check" size={36} /></div><Title title="Cảm ơn một hành trình tốt" subtitle="Bạn đã trả khách tại điểm đến." />
    <Notice tone={paid ? 'info' : 'warning'}><strong>{paid ? 'Thanh toán đã hoàn tất' : state.paymentError ? 'Thanh toán cần kiểm tra lại' : state.status === 'droppedoff' ? 'Chờ hành khách thanh toán' : 'Thanh toán đang xử lý'}</strong><p>{paid ? 'Giao dịch mô phỏng đã được ghi nhận.' : 'Kết quả thanh toán được cập nhật riêng với việc hoàn thành chặng đường.'}</p></Notice>
    <div className="summary-card"><div className="receipt-heading">CHUYẾN VỪA QUA<span>{tripId(state)}</span></div><Person role="rider" /><RouteSummary state={state} compact /><Fare state={state} large /></div>
    <Button onClick={() => { if (paid) dispatch({ type: 'reset' }); else onConfirm({ title: 'Về trang chủ demo?', body: 'Thanh toán chưa hoàn tất. Trong bản demo, thao tác này xóa kịch bản hiện tại để bắt đầu lại; không xác nhận đã thu tiền.', label: 'Đặt lại kịch bản demo', action: { type: 'reset' } }); }}>Về trang chủ</Button>
    {!paid && <div className="simulation-step"><span>ĐIỀU KHIỂN DEMO · PHẢN HỒI THANH TOÁN</span><p>Mô phỏng kết quả hệ thống, không phải tài xế xác nhận thu tiền.</p>{state.status === 'droppedoff' ? <button onClick={() => dispatch({ type: 'pay' })}>Mô phỏng bắt đầu thanh toán<Icon name="arrow" size={15} /></button> : state.paymentError ? <button onClick={() => dispatch({ type: 'retry' })}>Mô phỏng kiểm tra lại</button> : <div><button onClick={() => dispatch({ type: 'settle' })}>Mô phỏng thanh toán thành công<Icon name="check" size={15} /></button><button onClick={() => dispatch({ type: 'payment-error' })}>Mô phỏng lỗi</button></div>}</div>}
  </>;
}
