import { useEffect, useState } from 'react';
import { ActivityIndicator, Switch, View } from 'react-native';
import { Button, Card, Eyebrow, Icon, Metric, Note, palette, styles, T, Title } from '../ui';
import LiveMap from '../map/LiveMap';
import { PlaceSearch, quickPlaces } from '../map/PlacePicker';
import { coordinateText, reverseGeocode } from '../map/geo';
import type { Place } from '../map/geo';
import type { LatLng } from '../map/mapScript';
import { useRoute } from '../map/useRoute';
import { GatewayError } from '../gateway';
import { errorText, isNotFound, usePoll } from './hooks';
import type { ApiScreenProps } from './ApiApp';
import { SagaCard, TripRoute } from './RiderApi';
import type { SagaDto } from './RiderApi';
import { isTerminal, pointsOf, priceText, shortId, statusName, statusText } from './trip';
import type { InvitationDto, TripDto } from './trip';

const secondsLeft = (utc: string) => Math.max(0, Math.round((new Date(utc).getTime() - Date.now()) / 1000));

export default function DriverApi({ call, claims }: ApiScreenProps) {
  const [loading, setLoading] = useState(true);
  const [online, setOnline] = useState(false);
  const [position, setPosition] = useState<Place>(quickPlaces[0]);
  const [invitations, setInvitations] = useState<InvitationDto[]>([]);
  const [trip, setTrip] = useState<TripDto | null>(null);
  const [saga, setSaga] = useState<SagaDto | null>(null);
  const [error, setError] = useState('');
  const status = trip ? statusName(trip.status) : null;
  const tripPoints = trip ? pointsOf(trip) : { pickup: null, destination: null };
  const tripRoute = useRoute(tripPoints.pickup, tripPoints.destination);
  const offer = invitations[0] ? pointsOf(invitations[0]) : { pickup: null, destination: null };
  const offerRoute = useRoute(offer.pickup, offer.destination);

  const reload = async (id: string) => setTrip(await call<TripDto>('GET', `/api/trips/${id}`));
  const run = (task: () => Promise<void>) => async () => {
    setError('');
    try { await task(); } catch (e) {
      setError(errorText(e));
      if (e instanceof GatewayError && e.status === 409 && trip) await reload(trip.id).catch(() => undefined);
    }
  };
  const sendLocation = (isAvailable: boolean, place: LatLng = position) =>
    call('POST', '/api/drivers/location', { latitude: place.latitude, longitude: place.longitude, isAvailable });

  useEffect(() => {
    call<TripDto>('GET', '/api/trips/active')
      .then(setTrip)
      .catch(e => { if (!isNotFound(e)) setError(errorText(e)); })
      .finally(() => setLoading(false));
  }, [call]);

  const waiting = online && !trip;
  // Matching ignores GPS older than DriverAvailability:LocationTtlSeconds, so keep it fresh while waiting.
  usePoll(async () => { await sendLocation(true); }, 60_000, waiting);
  usePoll(async () => {
    try { setInvitations(await call<InvitationDto[]>('GET', '/api/trips/driver/invitations')); } catch (e) { setError(errorText(e)); }
  }, 2000, waiting);
  usePoll(async () => { if (trip) await reload(trip.id); }, 2000, !!trip && !!status && !isTerminal(status));
  const paymentPhase = !!status && ['DroppedOff', 'PaymentPending', 'Paid', 'Failed'].includes(status);
  usePoll(async () => {
    if (!trip) return;
    try { setSaga(await call<SagaDto>('GET', `/api/trips/${trip.id}/payment-saga`)); } catch (e) { if (!isNotFound(e)) throw e; }
  }, 2000, paymentPhase && (!saga || saga.state === 'AwaitingPayment' || saga.state === 'Compensating'));

  const toggle = run(async () => { const next = !online; await sendLocation(next); setOnline(next); if (!next) setInvitations([]); });
  // Simulated GPS: tap the map, search, or a quick pick moves the driver; Matching gets it at once when online.
  const move = (place: Place) => run(async () => { setPosition(place); if (online) await sendLocation(true, place); })();
  const pick = (point: LatLng) => {
    move({ ...point, address: coordinateText(point) });
    void reverseGeocode(point).then(address => setPosition(current =>
      current.latitude === point.latitude && current.longitude === point.longitude ? { ...point, address } : current));
  };
  const accept = (id: string) => run(async () => { await call('POST', `/api/trips/${id}/driver-accept`, {}); setInvitations([]); await reload(id); })();
  const reject = (id: string) => run(async () => { await call('POST', `/api/trips/${id}/driver-reject`, {}); setInvitations(list => list.filter(item => item.id !== id)); })();
  const step = (action: 'enroute' | 'pickup' | 'dropoff') => run(async () => {
    await call('POST', `/api/trips/${trip!.id}/${action}`, {});
    // The simulated car moves with the trip: at the pickup after pickup, at the destination after dropoff.
    const at = action === 'pickup' ? tripPoints.pickup : action === 'dropoff' ? tripPoints.destination : null;
    if (at) setPosition({ ...at, address: action === 'pickup' ? trip!.pickupAddress : trip!.dropoffAddress });
    await reload(trip!.id);
  });
  const home = () => { setTrip(null); setSaga(null); setError(''); };

  if (loading) return <ActivityIndicator accessibilityLabel="Đang tải chuyến" color={palette.teal} style={{ marginTop: 60 }} />;
  const banner = !!error && <Note warning>{error}</Note>;

  if (!trip) return <>
    <LiveMap label="Bản đồ vị trí tài xế và lời mời" driver={position} pickup={offer.pickup} destination={offer.destination} route={offerRoute?.path} onPick={pick} />
    <Card>
      <Eyebrow>KHÔNG GIAN TÀI XẾ</Eyebrow><Title>{`Chào ${claims.name || claims.email}.`}</Title>
      <View style={styles.row}><View style={styles.flex}><T style={styles.subtitle}>{online ? 'Sẵn sàng nhận chuyến' : 'Đang nghỉ ngơi'}</T><T style={styles.caption}>{online ? 'Vị trí được gửi lên Matching Service mỗi 60 giây.' : 'Bật để Matching có thể mời bạn.'}</T></View>
        <Switch accessibilityLabel="Sẵn sàng nhận chuyến" value={online} onValueChange={() => { void toggle(); }} trackColor={{ false: '#B8C5C2', true: palette.teal }} thumbColor={palette.white} /></View>
      <T style={styles.caption}>VỊ TRÍ CỦA BẠN (GIẢ LẬP GPS) · CHẠM BẢN ĐỒ ĐỂ ĐỔI</T>
      <View style={styles.row}><Icon name="pin" color={palette.ink} /><T style={[styles.place, styles.flex]} numberOfLines={2}>{position.address}</T></View>
      <PlaceSearch onSelect={move} />
      {banner}
    </Card>
    {online && (invitations.length === 0
      ? <Card><View style={styles.row}><ActivityIndicator color={palette.teal} /><T style={styles.flex}>Đang chờ lời mời. App kiểm tra mỗi 2 giây.</T></View></Card>
      : invitations.map(invitation => <Card key={invitation.id}>
        <View style={styles.row}><View style={styles.avatar}><Icon name="bike" size={27} /></View><View style={styles.flex}><Eyebrow>{`LỜI MỜI · CÒN ${secondsLeft(invitation.expiresAtUtc)} GIÂY`}</Eyebrow><Title>Chuyến đi gần bạn.</Title></View></View>
        <TripRoute pickup={invitation.pickupAddress} dropoff={invitation.dropoffAddress} />
        <T style={styles.caption}>GIÁ TRỊ CHUYẾN</T><T style={styles.fare}>{priceText(invitation.fareEstimate)}</T>
        <Button title="Nhận chuyến" icon="check" onPress={() => accept(invitation.id)} />
        <Button title="Từ chối lời mời" variant="danger" onPress={() => reject(invitation.id)} />
      </Card>))}
  </>;

  const header = <Eyebrow>{`CHUYẾN ${shortId(trip.id)} · ${statusText[status!].toUpperCase()}`}</Eyebrow>;
  const map = <LiveMap label={`Bản đồ chuyến từ ${trip.pickupAddress} đến ${trip.dropoffAddress}`} driver={position} pickup={tripPoints.pickup} destination={tripPoints.destination} route={tripRoute?.path} />;
  if (status && (isTerminal(status) || status === 'DroppedOff' || status === 'PaymentPending')) return <>{map}<Card>
    {header}
    <Title>{status === 'Paid' ? 'Một hành trình tốt.' : status === 'Cancelled' ? 'Hành khách đã hủy chuyến' : status === 'Failed' ? 'Chuyến không thành công' : 'Chờ hành khách thanh toán'}</Title>
    <TripRoute pickup={trip.pickupAddress} dropoff={trip.dropoffAddress} />
    <View style={styles.row}><Metric label="Giá chuyến" value={priceText(trip.fareEstimate)} /><Metric label="Hành khách" value={shortId(trip.riderId)} /></View>
    {paymentPhase && <SagaCard saga={saga} />}
    {!!trip.paymentFailureReason && <Note warning>{`Lý do: ${trip.paymentFailureReason}`}</Note>}
    {banner}
    {isTerminal(status) && <Button title="Về trang chủ" icon="arrow" onPress={home} />}
  </Card></>;

  return <>
    {map}
    <Card>
      {header}
      <Title>{status === 'DriverAccepted' ? 'Bạn đã nhận chuyến.' : status === 'EnRoute' ? 'Đang đi đón khách.' : status === 'PickedUp' ? 'Đưa khách đến nơi.' : 'Đang đồng bộ trạng thái'}</Title>
      <View style={styles.person}><View style={styles.avatar}><Icon name="user" size={28} /></View><View style={styles.flex}><T style={styles.place}>{`Hành khách ${shortId(trip.riderId)}`}</T><T style={styles.caption}>Mã hành khách lấy từ Trip Service</T></View></View>
      <TripRoute pickup={trip.pickupAddress} dropoff={trip.dropoffAddress} />
      <T style={styles.fare}>{priceText(trip.fareEstimate)}</T>
      <Note>{status === 'PickedUp' ? 'Chỉ xác nhận trả khách khi đã kết thúc hành trình. Trip sẽ khởi động Saga thanh toán.' : 'Hành khách vẫn có thể hủy trước khi được đón.'}</Note>
      {banner}
      {status === 'DriverAccepted' && <Button title="Bắt đầu đi đón" icon="arrow" onPress={step('enroute')} />}
      {status === 'EnRoute' && <Button title="Đã đón khách" icon="check" onPress={step('pickup')} />}
      {status === 'PickedUp' && <Button title="Đã trả khách" icon="check" onPress={step('dropoff')} />}
    </Card>
  </>;
}
