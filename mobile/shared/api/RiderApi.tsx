import { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';
import { initialTrip, places } from '../domain';
import { Button, Card, CityMap, Eyebrow, Icon, Metric, Note, palette, styles, T, Title } from '../ui';
import { GatewayError } from '../gateway';
import { errorText, isNotFound, usePoll } from './hooks';
import type { ApiScreenProps } from './ApiApp';
import { coordinates, estimateRoute, isTerminal, priceText, riderCanCancel, shortId, statusName, statusText } from './trip';
import type { QuoteDto, TripDto } from './trip';

export type SagaDto = { state: string; amount: number; paymentMethod: string; failureReason: string | null; deadlineUtc: string; completedAtUtc: string | null };
export const sagaText: Record<string, string> = {
  AwaitingPayment: 'Đang chờ Payment Service xử lý', Completed: 'Thanh toán thành công', Declined: 'Thanh toán bị từ chối',
  Abandoned: 'Quá hạn thanh toán', Compensating: 'Đang hoàn tiền', Refunded: 'Đã hoàn tiền',
};

const placeId = (address: string) => places.find(place => place.name === address)?.id ?? '';
/** CityMap only needs pickup/destination ids; reuse it with a minimal demo-shaped object. */
export const mapOf = (pickup: string, destination: string) => ({ ...initialTrip, pickup: pickup || 'university', destination });

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
    <T style={styles.caption}>{`Saga: ${saga.state} · ${saga.paymentMethod} · ${priceText(saga.amount)}`}</T>
    {saga.state === 'AwaitingPayment' && <T style={styles.caption}>{`Hạn chót: ${new Date(saga.deadlineUtc).toLocaleTimeString('vi-VN')}`}</T>}
    {!!saga.failureReason && <T style={[styles.caption, { color: palette.coral }]}>{`Lý do: ${saga.failureReason}`}</T>}
  </View>;
}

export default function RiderApi({ call, claims }: ApiScreenProps) {
  const [loading, setLoading] = useState(true);
  const [pickup, setPickup] = useState('university');
  const [destination, setDestination] = useState('');
  const [quote, setQuote] = useState<QuoteDto | null>(null);
  const [trip, setTrip] = useState<TripDto | null>(null);
  const [saga, setSaga] = useState<SagaDto | null>(null);
  const [error, setError] = useState('');
  const idempotencyKey = useRef('');
  const status = trip ? statusName(trip.status) : null;
  const route = estimateRoute(coordinates[pickup], coordinates[destination || 'market']);

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

  const getQuote = run(async () => {
    setQuote(await call<QuoteDto>('POST', '/api/matching/quote', { ...route, rideType: 'standard' }));
    idempotencyKey.current = `mobile-${claims.sub}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
  });
  const book = run(async () => {
    const [latitude, longitude] = coordinates[pickup];
    // One idempotency key per quote: tapping again after a lost response returns the same trip.
    setTrip(await call<TripDto>('POST', '/api/trips/', {
      pickupAddress: places.find(p => p.id === pickup)!.name, dropoffAddress: places.find(p => p.id === destination)!.name,
      pickupLatitude: latitude, pickupLongitude: longitude, ...route, rideType: 'standard', idempotencyKey: idempotencyKey.current,
    }));
  });
  const confirm = run(async () => { await call('POST', `/api/trips/${trip!.id}/confirm-price`, {}); await reload(trip!.id); });
  const cancel = run(async () => { await call('POST', `/api/trips/${trip!.id}/cancel`, {}); await reload(trip!.id); });
  const reset = () => { setTrip(null); setQuote(null); setSaga(null); setDestination(''); setError(''); };

  if (loading) return <ActivityIndicator accessibilityLabel="Đang tải chuyến" color={palette.teal} style={{ marginTop: 60 }} />;
  const banner = !!error && <Note warning>{error}</Note>;

  if (!trip) return <>
    <CityMap trip={mapOf(pickup, destination)} />
    {!quote ? <Card>
      <Eyebrow>{`CHÀO ${(claims.name || claims.email).toUpperCase()}`}</Eyebrow><Title>Bạn muốn đi đâu?</Title>
      <PlacePicker label="ĐÓN TÔI TẠI" value={pickup} onChange={id => { setPickup(id); if (id === destination) setDestination(''); }} />
      <PlacePicker label="ĐƯA TÔI ĐẾN" value={destination} exclude={pickup} onChange={setDestination} />
      {banner}
      <Button title="Xem giá từ Pricing" icon="arrow" disabled={!destination} onPress={getQuote} />
    </Card> : <Card>
      <Eyebrow>BÁO GIÁ TỪ MATCHING & PRICING</Eyebrow><Title>{priceText(quote.total)}</Title>
      <TripRoute pickup={places.find(p => p.id === pickup)!.name} dropoff={places.find(p => p.id === destination)!.name} />
      <View style={styles.row}><Metric label="Quãng đường ước tính" value={`${route.distance.toLocaleString('vi-VN')} km`} /><Metric label="Thời gian ước tính" value={`${route.estimatedTime} phút`} /></View>
      <View style={styles.divider} />
      {/* Every field is an amount: distance/time are the per-km and per-minute charges, surge is the peak-hour surcharge. */}
      {quote.base !== 0 && <Line label="Phí mở cửa" value={priceText(quote.base)} />}
      <Line label={`Cước theo km (${route.distance.toLocaleString('vi-VN')} km)`} value={priceText(quote.distance)} />
      <Line label={`Cước theo phút (${route.estimatedTime} phút)`} value={priceText(quote.time)} />
      <Line label="Phụ thu giờ cao điểm" value={priceText(quote.surge)} />
      <Line label="Phí phụ" value={priceText(quote.toll)} />
      <Line label="Giảm giá" value={priceText(quote.discount)} />
      <Line label="Thuế VAT" value={priceText(quote.tax)} />
      <Note>Giá tham khảo. Giá chốt do Trip Service lấy lại từ Pricing khi tạo chuyến.</Note>
      {banner}
      <Button title="Đặt xe" icon="arrow" onPress={book} />
      <Button title="Sửa hành trình" variant="secondary" onPress={() => { setQuote(null); setError(''); }} />
    </Card>}
  </>;

  const pickupId = placeId(trip.pickupAddress);
  const map = <CityMap trip={mapOf(pickupId, placeId(trip.dropoffAddress))} />;
  const header = <><Eyebrow>{`CHUYẾN ${shortId(trip.id)} · ${statusText[status!].toUpperCase()}`}</Eyebrow></>;

  if (status === 'Paid' || status === 'Cancelled' || status === 'Failed') return <Card>
    <View style={styles.center}><View style={[styles.heroIcon, status !== 'Paid' && { backgroundColor: palette.sand }]}><Icon name={status === 'Paid' ? 'check' : 'warning'} size={34} color={status === 'Paid' ? palette.teal : palette.coral} /></View>
      {header}<Title>{status === 'Paid' ? 'Bạn đã đến nơi.' : status === 'Cancelled' ? 'Chuyến đã hủy' : 'Chuyến không thành công'}</Title>
      <T style={[styles.muted, { textAlign: 'center' }]}>{status === 'Paid' ? 'Payment Service đã thu tiền và Trip đã ghi nhận.' : status === 'Cancelled' ? 'Không phát sinh thanh toán cho chuyến đã hủy.' : trip.paymentFailureReason ? `Lý do: ${trip.paymentFailureReason}` : `Không tìm được tài xế sau ${trip.declineCount ?? 0} lần từ chối.`}</T>
    </View>
    <TripRoute pickup={trip.pickupAddress} dropoff={trip.dropoffAddress} />
    <View style={styles.row}><Metric label="Giá chuyến" value={priceText(trip.fareEstimate)} /><Metric label="Tài xế" value={shortId(trip.driverId)} /></View>
    {paymentPhase && <SagaCard saga={saga} />}
    <Button title="Đặt chuyến mới" icon="arrow" onPress={reset} />
  </Card>;

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
      {status === 'Requested' && <Note>Giá đã được Trip Service chốt từ Pricing. Xác nhận để bắt đầu tìm tài xế.</Note>}
      {paymentPhase && <SagaCard saga={saga} />}
      {banner}
      {status === 'Requested' && <Button title="Xác nhận giá và tìm tài xế" icon="arrow" onPress={confirm} />}
      {status && riderCanCancel(status) && <Button title={status === 'Requested' ? 'Từ chối giá' : 'Hủy chuyến'} variant="danger" onPress={cancel} />}
    </Card>
  </>;
}

function Line({ label, value }: { label: string; value: string }) {
  return <View style={styles.row}><T style={[styles.flex, styles.muted]}>{label}</T><T style={styles.place}>{value}</T></View>;
}

function PlacePicker({ label, value, exclude, onChange }: { label: string; value: string; exclude?: string; onChange: (id: string) => void }) {
  return <View style={{ gap: 8 }}>
    <T style={styles.caption}>{label}</T>
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
      {places.filter(place => place.id !== exclude).map(place => <Pressable key={place.id} accessibilityRole="radio" accessibilityState={{ checked: value === place.id }} accessibilityLabel={`${label} ${place.name}`} onPress={() => onChange(place.id)}
        style={{ paddingHorizontal: 12, paddingVertical: 8, borderRadius: 12, borderWidth: 1, borderColor: value === place.id ? palette.teal : palette.line, backgroundColor: value === place.id ? palette.mint : palette.white }}>
        <T style={[styles.caption, { color: value === place.id ? palette.teal : palette.ink }]}>{place.name}</T>
      </Pressable>)}
    </View>
  </View>;
}
