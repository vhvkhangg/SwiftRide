import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
import { Button, Card, Eyebrow, Icon, Metric, Note, palette, styles, T, Title } from '../ui';
import LiveMap from '../map/LiveMap';
import { PlaceRow, PlaceSearch, quickPlaces } from '../map/PlacePicker';
import { coordinateText, reverseGeocode } from '../map/geo';
import type { Place } from '../map/geo';
import type { LatLng } from '../map/mapScript';
import { useRoute } from '../map/useRoute';
import { GatewayError } from '../gateway';
import { errorText, isNotFound, usePoll } from './hooks';
import type { ApiScreenProps } from './ApiApp';
import { isTerminal, paymentMethods, paymentMethodText, pointsOf, priceText, riderCanCancel, shortId, statusName, statusText } from './trip';
import type { PaymentMethod, QuoteDto, TripDto } from './trip';

export type SagaDto = { state: string; amount: number; paymentMethod: string; failureReason: string | null; deadlineUtc: string; completedAtUtc: string | null };
export const sagaText: Record<string, string> = {
  AwaitingPayment: 'Đang chờ Payment Service xử lý', Completed: 'Thanh toán thành công', Declined: 'Thanh toán bị từ chối',
  Abandoned: 'Quá hạn thanh toán', Compensating: 'Đang hoàn tiền', Refunded: 'Đã hoàn tiền',
};


export function TripRoute({ pickup, dropoff }: { pickup: string; dropoff: string }) {
  return <View style={styles.route}>
    <View style={styles.row}><View style={styles.pickupDot} /><View style={styles.flex}><T style={styles.caption}>ĐIỂM ĐÓN</T><T style={styles.place}>{pickup}</T></View></View>
    <View style={styles.routeDash} />
    <View style={styles.row}><View style={styles.destinationDot} /><View style={styles.flex}><T style={styles.caption}>ĐIỂM ĐẾN</T><T style={styles.place}>{dropoff}</T></View></View>
  </View>;
}

export function SagaCard({ saga }: { saga: SagaDto | null }) {
  if (!saga) return <View style={styles.row}><ActivityIndicator color={palette.teal} /><T style={styles.flex}>Đang chờ Saga thanh toán khởi tạo...</T></View>;
  return <View style={{ gap: 6 }}>
    <T style={styles.place}>{sagaText[saga.state] ?? saga.state}</T>
    <T style={styles.caption}>{`Saga: ${saga.state} · ${paymentMethodText(saga.paymentMethod)} · ${priceText(saga.amount)}`}</T>
    {saga.state === 'AwaitingPayment' && <T style={styles.caption}>{`Hạn chót: ${new Date(saga.deadlineUtc).toLocaleTimeString('vi-VN')}`}</T>}
    {!!saga.failureReason && <T style={[styles.caption, { color: palette.coral }]}>{`Lý do: ${saga.failureReason}`}</T>}
  </View>;
}

export default function RiderApi({ call, claims }: ApiScreenProps) {
  const [loading, setLoading] = useState(true);
  const [pickup, setPickup] = useState<Place | null>(quickPlaces[0]);
  const [destination, setDestination] = useState<Place | null>(null);
  const [field, setField] = useState<'pickup' | 'destination'>('destination');
  const [quote, setQuote] = useState<QuoteDto | null>(null);
  const [trip, setTrip] = useState<TripDto | null>(null);
  const [saga, setSaga] = useState<SagaDto | null>(null);
  const [error, setError] = useState('');
  const [method, setMethod] = useState<PaymentMethod>('Wallet');
  const idempotencyKey = useRef('');
  const newKey = () => { idempotencyKey.current = `mobile-${claims.sub}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`; };
  const status = trip ? statusName(trip.status) : null;
  const draftRoute = useRoute(trip ? null : pickup, trip ? null : destination);
  const tripPoints = trip ? pointsOf(trip) : { pickup: null, destination: null };
  const tripRoute = useRoute(tripPoints.pickup, tripPoints.destination);

  const reload = async (id: string) => setTrip(await call<TripDto>('GET', `/api/trips/${id}`));
  const run = (task: () => Promise<void>) => async () => {
    setError('');
    try { await task(); } catch (e) {
      setError(errorText(e));
      if (e instanceof GatewayError && e.status === 409 && trip) await reload(trip.id).catch(() => undefined);
    }
  };

  // Recover the rider's unfinished trip after reload or re-login (HTTP is the source of truth).
  useEffect(() => {
    call<TripDto>('GET', '/api/trips/active')
      .then(setTrip)
      .catch(e => { if (!isNotFound(e)) setError(errorText(e)); })
      .finally(() => setLoading(false));
  }, [call]);

  usePoll(async () => { if (trip) await reload(trip.id); }, 2000, !!trip && !!status && !isTerminal(status));
  const paymentPhase = !!status && ['DroppedOff', 'PaymentPending', 'Paid', 'Failed'].includes(status);
  usePoll(async () => {
    if (!trip) return;
    try { setSaga(await call<SagaDto>('GET', `/api/trips/${trip.id}/payment-saga`)); } catch (e) { if (!isNotFound(e)) throw e; }
  }, 2000, paymentPhase && (!saga || saga.state === 'AwaitingPayment' || saga.state === 'Compensating'));

  // Map tap / search result -> the active field; the address comes from OpenStreetMap (coordinates until it answers).
  const choose = (target: 'pickup' | 'destination', place: Place) => {
    setQuote(null);
    (target === 'pickup' ? setPickup : setDestination)(place);
    if (target === 'pickup' && !destination) setField('destination');
  };
  const pick = (point: LatLng) => {
    const target = field;
    choose(target, { ...point, address: `Đang lấy địa chỉ… (${coordinateText(point)})` });
    void reverseGeocode(point).then(address => (target === 'pickup' ? setPickup : setDestination)(current =>
      current && current.latitude === point.latitude && current.longitude === point.longitude ? { ...point, address } : current));
  };
  const resolving = !!pickup?.address.startsWith('Đang lấy') || !!destination?.address.startsWith('Đang lấy');

  const getQuote = run(async () => {
    if (!draftRoute) return;
    setQuote(await call<QuoteDto>('POST', '/api/matching/quote', { distance: draftRoute.distance, estimatedTime: draftRoute.estimatedTime, rideType: 'standard' }));
    newKey();
  });
  // The method is part of the booking payload, so a new choice gets a new idempotency key (the old one would answer 409).
  const chooseMethod = (next: PaymentMethod) => { if (next !== method) { setMethod(next); newKey(); setError(''); } };
  const book = run(async () => {
    if (!pickup || !destination || !draftRoute) return;
    // One idempotency key per quote: tapping again after a lost response returns the same trip.
    setTrip(await call<TripDto>('POST', '/api/trips/', {
      pickupAddress: pickup.address, dropoffAddress: destination.address,
      pickupLatitude: pickup.latitude, pickupLongitude: pickup.longitude,
      dropoffLatitude: destination.latitude, dropoffLongitude: destination.longitude,
      distance: draftRoute.distance, estimatedTime: draftRoute.estimatedTime, rideType: 'standard', idempotencyKey: idempotencyKey.current,
      paymentMethod: method,
    }));
  });
  const confirm = run(async () => { await call('POST', `/api/trips/${trip!.id}/confirm-price`, {}); await reload(trip!.id); });
  const cancel = run(async () => { await call('POST', `/api/trips/${trip!.id}/cancel`, {}); await reload(trip!.id); });
  const reset = () => { setTrip(null); setQuote(null); setSaga(null); setDestination(null); setField('destination'); setMethod('Wallet'); setError(''); };

  if (loading) return <ActivityIndicator accessibilityLabel="Đang tải chuyến" color={palette.teal} style={{ marginTop: 60 }} />;
  const banner = !!error && <Note warning>{error}</Note>;

  if (!trip) return <>
    <LiveMap label="Bản đồ chọn điểm đón và điểm đến" pickup={pickup} destination={destination} route={draftRoute?.path} onPick={quote ? undefined : pick} />
    {!quote ? <Card>
      <Eyebrow>{`CHÀO ${(claims.name || claims.email).toUpperCase()}`}</Eyebrow><Title>Bạn muốn đi đâu?</Title>
      <PlaceRow label="ĐIỂM ĐÓN" place={pickup} active={field === 'pickup'} color={palette.teal} onPress={() => setField('pickup')} />
      <PlaceRow label="ĐIỂM ĐẾN" place={destination} active={field === 'destination'} color={palette.coral} onPress={() => setField('destination')} />
      <PlaceSearch onSelect={place => choose(field, place)} />
      {pickup && destination && (draftRoute
        ? <View style={styles.row}><Metric label={draftRoute.source === 'osrm' ? 'Quãng đường (OSRM)' : 'Quãng đường ước tính'} value={`${draftRoute.distance.toLocaleString('vi-VN')} km`} /><Metric label="Thời gian" value={`${draftRoute.estimatedTime} phút`} /></View>
        : <View style={styles.row}><ActivityIndicator color={palette.teal} /><T style={styles.flex}>Đang tìm đường đi...</T></View>)}
      {draftRoute?.source === 'estimate' && <Note warning>Không lấy được đường đi từ OSRM; quãng đường đang ước tính theo đường chim bay.</Note>}
      {banner}
      <Button title="Xem giá từ Pricing" icon="arrow" disabled={!pickup || !destination || !draftRoute || resolving} onPress={getQuote} />
    </Card> : <Card>
      <Eyebrow>BÁO GIÁ TỪ MATCHING & PRICING</Eyebrow><Title>{priceText(quote.total)}</Title>
      <TripRoute pickup={pickup!.address} dropoff={destination!.address} />
      <View style={styles.row}><Metric label="Quãng đường" value={`${draftRoute!.distance.toLocaleString('vi-VN')} km`} /><Metric label="Thời gian" value={`${draftRoute!.estimatedTime} phút`} /></View>
      <View style={styles.divider} />
      {/* Every field is an amount: distance/time are the per-km and per-minute charges, surge is the peak-hour surcharge. */}
      {quote.base !== 0 && <Line label="Phí mở cửa" value={priceText(quote.base)} />}
      <Line label={`Cước theo km (${draftRoute!.distance.toLocaleString('vi-VN')} km)`} value={priceText(quote.distance)} />
      <Line label={`Cước theo phút (${draftRoute!.estimatedTime} phút)`} value={priceText(quote.time)} />
      <Line label="Phụ thu giờ cao điểm" value={priceText(quote.surge)} />
      <Line label="Phí phụ" value={priceText(quote.toll)} />
      <Line label="Giảm giá" value={priceText(quote.discount)} />
      <Line label="Thuế VAT" value={priceText(quote.tax)} />
      <Note>Giá tham khảo. Giá chốt do Trip Service lấy lại từ Pricing khi tạo chuyến.</Note>
      <View style={styles.divider} />
      <MethodChoice value={method} onChange={chooseMethod} />
      {banner}
      <Button title="Đặt xe" icon="arrow" onPress={book} />
      <Button title="Sửa hành trình" variant="secondary" onPress={() => { setQuote(null); setError(''); }} />
    </Card>}
  </>;

  const map = <LiveMap label={`Bản đồ chuyến từ ${trip.pickupAddress} đến ${trip.dropoffAddress}`} pickup={tripPoints.pickup} destination={tripPoints.destination} route={tripRoute?.path} />;
  const header = <><Eyebrow>{`CHUYẾN ${shortId(trip.id)} · ${statusText[status!].toUpperCase()}`}</Eyebrow></>;

  if (status === 'Paid' || status === 'Cancelled' || status === 'Failed') return <>{map}<Card>
    <View style={styles.center}><View style={[styles.heroIcon, status !== 'Paid' && { backgroundColor: palette.sand }]}><Icon name={status === 'Paid' ? 'check' : 'warning'} size={34} color={status === 'Paid' ? palette.teal : palette.coral} /></View>
      {header}<Title>{status === 'Paid' ? 'Bạn đã đến nơi.' : status === 'Cancelled' ? 'Chuyến đã hủy' : 'Chuyến không thành công'}</Title>
      <T style={[styles.muted, { textAlign: 'center' }]}>{status === 'Paid' ? 'Payment Service đã thu tiền và Trip đã ghi nhận.' : status === 'Cancelled' ? 'Không phát sinh thanh toán cho chuyến đã hủy.' : trip.paymentFailureReason ? `Lý do: ${trip.paymentFailureReason}` : `Không tìm được tài xế sau ${trip.declineCount ?? 0} lần từ chối.`}</T>
    </View>
    <TripRoute pickup={trip.pickupAddress} dropoff={trip.dropoffAddress} />
    <View style={styles.row}><Metric label="Giá chuyến" value={priceText(trip.fareEstimate)} /><Metric label="Thanh toán" value={paymentMethodText(trip.paymentMethod)} /></View>
    {paymentPhase && <SagaCard saga={saga} />}
    <Button title="Đặt chuyến mới" icon="arrow" onPress={reset} />
  </Card></>;

  return <>
    {map}
    <Card>
      {header}
      <Title>{status === 'Requested' ? 'Xác nhận giá chuyến đi' : status === 'PriceConfirmed' ? 'Tìm bạn đồng hành.' : status === 'DriverAccepted' ? 'Đã có tài xế!' : status === 'EnRoute' ? 'Tài xế đang đến.' : status === 'PickedUp' ? 'Đang đến điểm hẹn.' : status === 'DroppedOff' || status === 'PaymentPending' ? 'Đang thanh toán' : 'Đang đồng bộ trạng thái'}</Title>
      {status === 'PriceConfirmed' && <View style={styles.row}><ActivityIndicator color={palette.teal} /><T style={styles.flex}>Matching đang mời tài xế gần điểm đón. App tự cập nhật mỗi 2 giây.</T></View>}
      {status === 'PriceConfirmed' && (trip.declineCount ?? 0) > 0 && <Note warning>{`Đã có ${trip.declineCount} tài xế từ chối (ngưỡng k = ${trip.declineThreshold ?? '?'}). Đang mời tài xế khác.`}</Note>}
      {trip.driverId && <View style={styles.person}><View style={styles.avatar}><Icon name="bike" size={28} /></View><View style={styles.flex}><T style={styles.place}>{`Tài xế ${shortId(trip.driverId)}`}</T><T style={styles.caption}>Mã tài xế lấy từ Trip Service</T></View></View>}
      <TripRoute pickup={trip.pickupAddress} dropoff={trip.dropoffAddress} />
      <T style={styles.fare}>{priceText(trip.fareEstimate)}</T>
      <Line label="Thanh toán" value={paymentMethodText(trip.paymentMethod)} />
      {status === 'Requested' && <Note>Giá đã được Trip Service chốt từ Pricing. Xác nhận để bắt đầu tìm tài xế.</Note>}
      {paymentPhase && <SagaCard saga={saga} />}
      {banner}
      {status === 'Requested' && <Button title="Xác nhận giá và tìm tài xế" icon="arrow" onPress={confirm} />}
      {status && riderCanCancel(status) && <Button title={status === 'Requested' ? 'Từ chối giá' : 'Hủy chuyến'} variant="danger" onPress={cancel} />}
    </Card>
  </>;
}

/** Wallet / Card choice before booking. Card is a simulated gateway: the app never asks for card numbers. */
export function MethodChoice({ value, onChange }: { value: PaymentMethod; onChange: (method: PaymentMethod) => void }) {
  return <View accessibilityRole="radiogroup" accessibilityLabel="Phương thức thanh toán" style={{ gap: 8 }}>
    <T style={styles.subtitle}>Thanh toán</T>
    <View style={styles.row}>
      {paymentMethods.map(item => {
        const selected = item === value;
        return <Pressable key={item} accessibilityRole="radio" accessibilityState={{ checked: selected }} accessibilityLabel={paymentMethodText(item)} onPress={() => onChange(item)}
          style={[styles.input, styles.flex, styles.row, { justifyContent: 'center' }, selected && { borderColor: palette.teal, backgroundColor: palette.mint }]}>
          <Icon name={item === 'Card' ? 'card' : 'wallet'} color={selected ? palette.teal : palette.muted} />
          <T style={[styles.place, !selected && { color: palette.muted }]}>{paymentMethodText(item)}</T>
        </Pressable>;
      })}
    </View>
    {value === 'Card' && <T style={styles.caption}>Thẻ mô phỏng cổng thanh toán: app không nhập số thẻ; Payment Service duyệt tối đa 5.000.000 ₫ mỗi giao dịch.</T>}
  </View>;
}

function Line({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><T style={[styles.flex, styles.muted]}>{label}</T><T style={styles.place}>{value}</T></View>;
}
