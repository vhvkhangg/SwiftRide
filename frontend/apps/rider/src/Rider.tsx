import { useState } from 'react';
import type { Dispatch } from 'react';
import { findPlace, money, places, quoteFor, tripId } from '../../../shared/lib/demo';
import type { Action, DemoState } from '../../../shared/lib/demo';
import { Badge, Button, Fare, Icon, Modal, Notice, Person, RouteSummary, SafetyNote, Title } from '../../../shared/components/ui';

export default function Rider({ state, dispatch, onCancel, onDetails }: { state: DemoState; dispatch: Dispatch<Action>; onCancel: () => void; onDetails: () => void }) {
  const [picker, setPicker] = useState<'pickup' | 'destination' | null>(null);
  const [search, setSearch] = useState('');
  const fare = quoteFor(state);
  const normalize = (value: string) => value.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').toLowerCase();
  const results = places.filter(place => normalize(`${place.name} ${place.address}`).includes(normalize(search)));
  const select = (id: string) => { if (picker) dispatch({ type: 'place', field: picker, id }); setPicker(null); setSearch(''); };

  if (state.status === 'idle') return <>
    <div className="panel-greeting">XIN CHÀO, KHÁNH<span><Icon name="spark" size={16} />Một ngày thật đẹp</span></div>
    <Title title="Bạn muốn đi đâu?" subtitle="Chọn điểm đến. Phần còn lại, để SwiftRide." />
    <div className="location-fields">{(['pickup', 'destination'] as const).map(field => <button className="location-field" key={field} onClick={() => { setPicker(field); setSearch(''); }}><span className={field === 'pickup' ? 'stop-dot' : 'stop-square'} /><span><span className="field-caption">{field === 'pickup' ? 'ĐIỂM ĐÓN' : 'ĐIỂM ĐẾN'}</span><strong className={!state[field] ? 'placeholder' : ''}>{state[field] ? findPlace(state[field]).name : 'Bạn muốn đến đâu?'}</strong></span><Icon name={field === 'pickup' ? 'location' : 'search'} size={18} /></button>)}</div>
    {state.pickup === state.destination && <p className="form-error">Điểm đến cần khác điểm đón.</p>}
    <Button disabled={!state.destination || state.pickup === state.destination} onClick={() => dispatch({ type: 'quote' })} icon="arrow">Xem giá chuyến đi</Button>
    <div className="suggestions"><div className="small-section-label">GỢI Ý CHO HÀNH TRÌNH CỦA BẠN</div>{places.filter(place => place.id !== state.pickup).slice(0, 3).map(place => <button className="suggestion" key={place.id} onClick={() => dispatch({ type: 'place', field: 'destination', id: place.id })}><span className="suggestion-icon"><Icon name="pin" size={18} /></span><span><strong>{place.name}</strong><small>{place.district}</small></span><Icon name="chevron" size={15} /></button>)}</div>
    <div className="home-promise"><span><Icon name="shield" size={22} /></span><div><strong>An tâm trên từng cây số</strong><p>Giá rõ ràng. Hành trình trong tầm tay.</p></div></div>
    {picker && <Modal title={picker === 'pickup' ? 'Bạn đang ở đâu?' : 'Bạn muốn đến đâu?'} onClose={() => setPicker(null)}><div className="search-input"><Icon name="search" /><input autoFocus aria-label="Tìm địa điểm mẫu" placeholder="Tìm địa chỉ, địa điểm…" value={search} onChange={event => setSearch(event.target.value)} /></div><p className="muted picker-note">4 địa điểm mẫu · Chưa kết nối dịch vụ tìm địa chỉ.</p><div className="place-results">{results.map(place => <button key={place.id} onClick={() => select(place.id)}><span className="suggestion-icon"><Icon name="pin" /></span><span><strong>{place.name}</strong><small>{place.address}</small></span><Icon name="chevron" size={16} /></button>)}{results.length === 0 && <div className="empty-search"><Icon name="search" size={30} /><strong>Chưa tìm thấy địa điểm</strong><p>Thử “Bến Thành”, “Tao Đàn” hoặc “Sài Gòn”.</p></div>}</div></Modal>}
  </>;

  if (state.status === 'quoted') return <>
    <button className="back-link" onClick={() => dispatch({ type: 'edit' })}><Icon name="back" size={17} />Chỉnh sửa hành trình</button>
    <Title eyebrow="CHỈ CÒN MỘT BƯỚC" title="Chuyến đi của bạn" subtitle="Mọi thứ đã sẵn sàng. Mình đi thôi!" />
    <RouteSummary state={state} />
    <div className="ride-option"><div className="ride-illustration"><Icon name="bike" size={39} /></div><div><strong>SwiftRide Bike</strong><small>Linh hoạt giữa nhịp phố</small><span>{fare.distance} km <span>·</span> Khoảng {fare.minutes} phút</span></div><span className="option-selected"><Icon name="check" size={14} /></span></div>
    <div className="quote-box"><Fare state={state} label="Giá dự kiến" large /><div className="included"><Icon name="check" size={14} />Đã bao gồm thuế và phí mô phỏng</div></div>
    <details className="price-details"><summary>Chi tiết giá<Icon name="down" size={16} /></summary><div><span>Cước chuyến đi</span><strong>{money(Math.round(fare.fare / 1.1))}</strong></div><div><span>Thuế mô phỏng</span><strong>{money(fare.fare - Math.round(fare.fare / 1.1))}</strong></div><small>Giá cố định cho từng điểm đến mẫu, không phải báo giá thực tế.</small></details>
    <Button icon="arrow" onClick={() => dispatch({ type: 'book' })}>Xác nhận đặt xe</Button><SafetyNote>Giá được xác nhận trước khi bạn đặt chuyến.</SafetyNote>
    <button className="text-link cancel-link" onClick={onCancel}>Hủy yêu cầu</button>
  </>;

  if (state.status === 'searching') return <>
    <div className="radar-illustration"><span /><span /><div><Icon name="bike" size={35} /></div></div>
    <Title title={state.attempts ? 'Đang tìm tài xế khác' : 'Đang tìm tài xế'} subtitle={state.attempts ? `Lời mời trước đã bị từ chối. Đang thử lần ${state.attempts + 1}/3.` : 'Kết nối bạn với một tài xế ở gần.'} />
    <div className="search-status" role="status"><span className="loading-dots"><i /><i /><i /></span>Đang chờ tài xế phản hồi</div>
    <div className="summary-card"><RouteSummary state={state} compact /><Fare state={state} label="Giá đã xác nhận" /></div>
    <Button kind="secondary" onClick={onCancel}>Hủy yêu cầu</Button>
    <div className="simulation-step"><span>ĐIỀU KHIỂN DEMO</span><p>Chưa có tài xế thật. Chọn một kết quả để tiếp tục.</p><div><button onClick={() => dispatch({ type: 'accept' })}>Tài xế nhận<Icon name="arrow" size={15} /></button><button onClick={() => dispatch({ type: 'fail' })}>Không tìm thấy</button></div></div>
  </>;

  if (['accepted', 'enroute', 'pickedup'].includes(state.status)) return <>
    <Badge>{state.status === 'pickedup' ? 'Chuyến đi đang diễn ra' : 'Đã kết nối tài xế'}</Badge>
    <Title title={state.status === 'accepted' ? 'Tài xế đã nhận chuyến' : state.status === 'enroute' ? 'Tài xế đang đến' : 'Đang trên hành trình'} subtitle={state.status === 'accepted' ? 'Minh Anh đang chuẩn bị đến đón bạn.' : state.status === 'enroute' ? 'Bạn chuẩn bị sẵn sàng nhé.' : `Điểm đến tiếp theo: ${findPlace(state.destination).name}.`} />
    {state.status !== 'accepted' && <div className="eta"><Icon name="clock" /><span>{state.status === 'enroute' ? 'Đến đón sau khoảng' : 'Thời gian chuyến mẫu'}</span><strong>{state.status === 'enroute' ? '3' : fare.minutes}<small>phút</small></strong></div>}
    <Person role="driver" plate />
    <div className="pickup-note"><Icon name="pin" /><div><span>{state.status === 'pickedup' ? 'ĐIỂM TRẢ KHÁCH' : 'ĐIỂM ĐÓN CỦA BẠN'}</span><strong>{state.status === 'pickedup' ? findPlace(state.destination).name : `Cổng chính ${findPlace(state.pickup).name}`}</strong></div></div>
    <Fare state={state} /><SafetyNote>{state.status === 'pickedup' ? 'Chúc bạn có một hành trình thật dễ chịu.' : 'Kiểm tra đúng biển số trước khi lên xe.'}</SafetyNote>
    <Button kind="secondary" onClick={onDetails}>Chi tiết chuyến đi</Button>
    {state.status !== 'pickedup' && <button className="text-link cancel-link" onClick={onCancel}>Hủy chuyến</button>}
    <div className="simulation-step"><span>ĐIỀU KHIỂN DEMO · HÀNH ĐỘNG CỦA TÀI XẾ</span><button onClick={() => dispatch({ type: state.status === 'accepted' ? 'depart' : state.status === 'enroute' ? 'pickup' : 'dropoff' })}>{state.status === 'accepted' ? 'Tài xế bắt đầu đi đón' : state.status === 'enroute' ? 'Mô phỏng đã đón khách' : 'Mô phỏng đã trả khách'}<Icon name="arrow" size={15} /></button></div>
  </>;

  if (['droppedoff', 'payment_pending'].includes(state.status)) return <>
    <div className={`result-symbol ${state.paymentError ? 'symbol-warning' : ''}`}>{state.status === 'droppedoff' ? <Icon name="wallet" size={34} /> : state.paymentError ? <Icon name="clock" size={34} /> : <span className="spinner" />}</div>
    <Title title={state.status === 'droppedoff' ? 'Bạn đã đến nơi!' : state.paymentError ? 'Cần kiểm tra thanh toán' : 'Đang xử lý thanh toán'} subtitle={state.status === 'droppedoff' ? 'Hoàn tất thanh toán để khép lại chuyến đi.' : state.paymentError ? 'Chưa xác định được kết quả giao dịch.' : 'Chuyến đi đã kết thúc. Vui lòng chờ xác nhận.'} />
    <div className="summary-card"><div className="receipt-heading">CHI TIẾT CHUYẾN ĐI<span>{tripId(state)}</span></div><RouteSummary state={state} compact /><Fare state={state} label="Số tiền thanh toán" large /></div>
    {state.status === 'droppedoff' ? <Button icon="arrow" onClick={() => dispatch({ type: 'pay' })}>Thanh toán mô phỏng</Button> : state.paymentError ? <><Notice tone="warning">Không tạo giao dịch mới. Kiểm tra lại bằng mã giao dịch hiện tại.</Notice><Button onClick={() => dispatch({ type: 'retry' })}>Kiểm tra trạng thái</Button></> : <><Notice>Giao dịch đang được xử lý. Không cần gửi lại yêu cầu.</Notice><div className="simulation-step"><span>ĐIỀU KHIỂN DEMO</span><div><button onClick={() => dispatch({ type: 'settle' })}>Xác nhận thành công<Icon name="check" size={15} /></button><button onClick={() => dispatch({ type: 'payment-error' })}>Mô phỏng lỗi</button></div></div></>}
    <p className="centered muted fine-print">Không trừ tiền thật · Thanh toán mô phỏng</p>
  </>;

  const success = state.status === 'paid';
  const cancelled = state.status === 'cancelled';
  return <>
    <div className={`result-symbol ${success ? '' : 'symbol-neutral'}`}><Icon name={success ? 'check' : cancelled ? 'close' : 'search'} size={38} /></div>
    <Title title={success ? 'Chuyến đi hoàn tất' : cancelled ? 'Yêu cầu đã được hủy' : 'Chưa tìm được tài xế'} subtitle={success ? 'Cảm ơn bạn đã đồng hành cùng SwiftRide.' : cancelled ? 'Bạn có thể đặt chuyến khác khi sẵn sàng.' : 'Hiện chưa có tài xế phù hợp. Hãy thử lại sau nhé.'} />
    <div className="receipt"><div className="receipt-heading">{success ? 'BIÊN NHẬN CHUYẾN ĐI' : 'THÔNG TIN YÊU CẦU'}<span>{tripId(state)}</span></div><RouteSummary state={state} compact />{success && <Person role="driver" />}<div className="receipt-total">{success ? <><Fare state={state} label="Đã thanh toán" large /><Badge>Thành công</Badge></> : <span>Không phát sinh thanh toán trong bản demo.</span>}</div></div>
    <Button icon="arrow" onClick={() => dispatch({ type: 'reset' })}>{success ? 'Đặt chuyến mới' : 'Về màn hình đặt xe'}</Button>
    <SafetyNote>{success ? 'Hẹn gặp bạn trên hành trình tiếp theo.' : 'Trạng thái yêu cầu đã được cập nhật.'}</SafetyNote>
  </>;
}
