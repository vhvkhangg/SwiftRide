import { Switch, View } from 'react-native';
import { fareFor, money, statusLabels, tripId } from '../../../shared/domain';
import type { TripScreenProps } from '../../../shared/MobileApp';
import { Payment, Terminal } from '../../../shared/TripResult';
import { Button, Card, CityMap, Demo, Eyebrow, Icon, Metric, Note, palette, Person, Route, styles, T, Title } from '../../../shared/ui';

export default function Driver(props: TripScreenProps) {
  const { trip, dispatch, confirm } = props;
  const fare = fareFor(trip);
  if (['paid', 'cancelled', 'failed'].includes(trip.status)) return <Terminal {...props} driver />;
  if (['droppedoff', 'payment_pending'].includes(trip.status)) return <Payment {...props} driver />;
  return <>
    <CityMap trip={trip} />
    {trip.status === 'idle' && <>
      <Card><Eyebrow>KHÔNG GIAN TÀI XẾ</Eyebrow><Title>Chào Minh Anh.</Title><T style={styles.muted}>Chủ động hành trình. Kết nối mỗi ngày.</T><Person driver /><View style={styles.divider} />
        <View style={styles.row}><View style={styles.flex}><T style={styles.subtitle}>{trip.online ? 'Sẵn sàng nhận chuyến' : 'Đang nghỉ ngơi'}</T><T style={styles.caption}>{trip.online ? 'Bạn đang bật nhận lời mời mẫu.' : 'Bật kết nối khi bạn sẵn sàng.'}</T></View><Switch accessibilityLabel="Sẵn sàng nhận chuyến" value={trip.online} onValueChange={value => dispatch({ type: 'online', value })} trackColor={{ false: '#B8C5C2', true: palette.teal }} thumbColor={palette.white} /></View>
        <Note>{trip.online ? 'Vị trí và bản đồ là minh họa. Không theo dõi GPS thật.' : 'Không có lời mời mới khi đang tắt nhận chuyến.'}</Note>
      </Card>
      <Demo><T style={styles.caption}>Tạo lời mời riêng cho app Driver. Chưa nhận chuyến từ app Rider.</T><Button title="Mô phỏng chuyến mới" icon="arrow" disabled={!trip.online} onPress={() => dispatch({ type: 'offer' })} /></Demo>
    </>}
    {trip.status === 'searching' && <>
      <Card><View style={styles.row}><View style={styles.avatar}><Icon name="bike" size={27} /></View><View style={styles.flex}><Eyebrow>LỜI MỜI MỚI</Eyebrow><Title>Chuyến đi gần bạn.</Title></View></View>
        {trip.attempts > 0 && <Note warning>Đã từ chối {trip.attempts}/3 lần. Đây là lời mời tiếp theo trong kịch bản mô phỏng.</Note>}
        <Person /><Route trip={trip} /><View style={styles.divider} /><View style={styles.row}><Metric value={`${fare.distance} km`} label="Quãng đường mẫu" /><Metric value={`${fare.minutes} phút`} label="Thời gian mẫu" /></View><T style={styles.caption}>GIÁ TRỊ CHUYẾN · CHƯA TRỪ PHÍ</T><T style={styles.fare}>{money(fare.fare)}</T>
        <Button title="Từ chối lời mời" variant="danger" onPress={() => confirm('Từ chối lời mời?', 'Kịch bản sẽ thử lời mời tiếp theo, tối đa 3 lần.', () => dispatch({ type: 'reject' }))} />
      </Card>
    </>}
    {['accepted', 'enroute', 'pickedup'].includes(trip.status) && <>
      <Card><Eyebrow>{tripId(trip)} · {statusLabels[trip.status].toUpperCase()}</Eyebrow><Title>{trip.status === 'pickedup' ? 'Đưa khách đến nơi.' : trip.status === 'accepted' ? 'Bạn đã nhận chuyến.' : 'Đang đi đón khách.'}</Title><Person /><Route trip={trip} /><View style={styles.divider} />
        <View style={styles.row}><Metric value={`${fare.minutes} phút`} label="Thời gian mẫu" /><Metric value={money(fare.fare)} label="Giá chuyến mẫu" /></View>
        <Note>{trip.status === 'pickedup' ? 'Chỉ xác nhận trả khách khi đã kết thúc hành trình.' : 'Đối chiếu thông tin hành khách trước khi xác nhận đón.'}</Note>
        {trip.status !== 'pickedup' && <Button title="Hủy chuyến" variant="danger" onPress={() => confirm('Hủy chuyến?', 'Chuyến demo sẽ kết thúc. Chỉ cho hủy trước khi đón khách.', () => dispatch({ type: 'cancel' }))} />}
      </Card>
    </>}
  </>;
}
