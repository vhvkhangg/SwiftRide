import { useState } from 'react';
import { ActivityIndicator, Pressable, TextInput, View } from 'react-native';
import { fareFor, findPlace, money, places, statusLabels, tripId } from '../../../shared/domain';
import type { TripScreenProps } from '../../../shared/MobileApp';
import { Payment, Terminal } from '../../../shared/TripResult';
import { Button, Card, CityMap, Demo, Eyebrow, Icon, Metric, Note, palette, Person, Route, Sheet, styles, T, Title } from '../../../shared/ui';

const searchable = (text: string) => text.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');

export default function Rider(props: TripScreenProps) {
  const { trip, dispatch, confirm } = props;
  const [field, setField] = useState<'pickup' | 'destination' | null>(null);
  const [query, setQuery] = useState('');
  const [details, setDetails] = useState(false);
  const fare = fareFor(trip);
  const cancel = () => confirm('Hủy chuyến?', 'Chuyến demo sẽ dừng. Bạn có thể tạo chuyến mới sau khi hủy.', () => dispatch({ type: 'cancel' }));
  if (['paid', 'cancelled', 'failed'].includes(trip.status)) return <Terminal {...props} />;
  if (['droppedoff', 'payment_pending'].includes(trip.status)) return <Payment {...props} />;
  return <>
    <CityMap trip={trip} />
    {trip.status === 'idle' && <>
      <Card><Eyebrow>CHÀO KHÁNH · ĐI CÙNG SWIFTRIDE</Eyebrow><Title>Bạn muốn đi đâu?</Title><T style={styles.muted}>Một chuyến đi nhỏ. Một ngày thật khác.</T>
        {(['pickup', 'destination'] as const).map(item => <Pressable key={item} accessibilityRole="button" accessibilityLabel={item === 'pickup' ? 'Chọn điểm đón' : 'Chọn điểm đến'} onPress={() => { setField(item); setQuery(''); }} style={[styles.input, styles.row]}>
          <Icon name="pin" color={item === 'pickup' ? palette.teal : palette.coral} /><View style={styles.flex}><T style={styles.caption}>{item === 'pickup' ? 'ĐÓN TÔI TẠI' : 'ĐƯA TÔI ĐẾN'}</T><T style={styles.place}>{trip[item] ? findPlace(trip[item]).name : 'Chọn điểm đến'}</T></View><Icon name="arrow" size={17} />
        </Pressable>)}
        {trip.destination === trip.pickup && <Note warning>Điểm đến cần khác điểm đón.</Note>}
      </Card>
      <Eyebrow>ĐIỂM ĐẾN GỢI Ý</Eyebrow>
      {places.filter(place => place.id !== trip.pickup).map(place => <Pressable key={place.id} accessibilityRole="button" accessibilityLabel={`Đến ${place.name}`} onPress={() => dispatch({ type: 'place', field: 'destination', id: place.id })} style={[styles.row, { paddingVertical: 10 }]}><View style={styles.avatar}><Icon name="pin" /></View><View style={styles.flex}><T style={styles.place}>{place.name}</T><T style={styles.caption}>{place.address}</T></View><Icon name="arrow" size={18} /></Pressable>)}
    </>}
    {trip.status === 'quoted' && <>
      <Card><Eyebrow>HÀNH TRÌNH CỦA BẠN</Eyebrow><Title>Đi thôi, Khánh.</Title><Route trip={trip} /><View style={styles.divider} />
        <View style={styles.row}><View style={styles.avatar}><Icon name="bike" size={29} /></View><View style={styles.flex}><T style={styles.subtitle}>SwiftRide Bike</T><T style={styles.caption}>1 hành khách · Xe máy</T></View><Icon name="check" /></View>
        <View style={styles.row}><Metric label="Quãng đường mẫu" value={`${fare.distance} km`} /><Metric label="Thời gian mẫu" value={`${fare.minutes} phút`} /></View>
        <T style={styles.fare}>{money(fare.fare)}</T><Button title="Chi tiết giá mẫu" variant="secondary" onPress={() => setDetails(true)} />
        <Button title="Sửa hành trình" variant="secondary" onPress={() => dispatch({ type: 'edit' })} /><Button title="Từ chối giá" variant="danger" onPress={cancel} />
      </Card>
    </>}
    {trip.status === 'searching' && <>
      <Card><View style={styles.center}><View style={styles.heroIcon}><ActivityIndicator size="large" color={palette.teal} /></View><Eyebrow>ĐANG KẾT NỐI</Eyebrow><Title>Tìm bạn đồng hành.</Title><T style={[styles.muted, { textAlign: 'center' }]}>Đang tìm tài xế phù hợp cho hành trình của bạn.</T></View><Route trip={trip} /><T style={styles.fare}>{money(fare.fare)}</T><Button title="Hủy tìm tài xế" variant="danger" onPress={cancel} /></Card>
      <Demo><T style={styles.caption}>Demo không kết nối app Driver. Chọn phản hồi để đi tiếp.</T><Button title="Mô phỏng tài xế nhận" onPress={() => dispatch({ type: 'accept' })} /><Button title="Mô phỏng không có tài xế" variant="secondary" onPress={() => dispatch({ type: 'fail' })} /></Demo>
    </>}
    {['accepted', 'enroute', 'pickedup'].includes(trip.status) && <>
      <Card><Eyebrow>{tripId(trip)} · {statusLabels[trip.status].toUpperCase()}</Eyebrow><Title>{trip.status === 'pickedup' ? 'Đang đến điểm hẹn.' : trip.status === 'accepted' ? 'Đã có tài xế!' : 'Tài xế đang đến.'}</Title><Person driver />
        <Note>{trip.status === 'pickedup' ? 'Bạn đang trên chuyến đi. Thời gian hiển thị là dữ liệu mẫu.' : 'Kiểm tra tên tài xế và biển số trước khi lên xe.'}</Note><Route trip={trip} /><View style={styles.row}><Metric label="Thời gian mẫu" value={`${fare.minutes} phút`} /><Metric label="Giá chuyến mẫu" value={money(fare.fare)} /></View>
        {trip.status !== 'pickedup' && <Button title="Hủy chuyến" variant="danger" onPress={cancel} />}
      </Card>
      <Demo><T style={styles.caption}>Mô phỏng sự kiện tài xế, không phải quyền thao tác của hành khách.</T>
        <Button title={trip.status === 'accepted' ? 'Mô phỏng tài xế đi đón' : trip.status === 'enroute' ? 'Mô phỏng đã đón khách' : 'Mô phỏng đã trả khách'} onPress={() => dispatch({ type: trip.status === 'accepted' ? 'depart' : trip.status === 'enroute' ? 'pickup' : 'dropoff' })} />
      </Demo>
    </>}
    <Sheet visible={field !== null} title={field === 'pickup' ? 'Bạn đang ở đâu?' : 'Bạn muốn đến đâu?'} onClose={() => setField(null)}>
      <TextInput accessibilityLabel="Tìm địa điểm" placeholder="Tìm trong 4 địa điểm mẫu" placeholderTextColor={palette.muted} value={query} onChangeText={setQuery} style={styles.input} autoCorrect={false} />
      <T style={styles.caption}>Địa điểm mẫu tại TP. Hồ Chí Minh. Không dùng GPS.</T>
      {places.filter(place => searchable(`${place.name} ${place.address}`).includes(searchable(query.trim()))).map(place => <Pressable key={place.id} accessibilityRole="button" accessibilityLabel={place.name} onPress={() => { if (field) dispatch({ type: 'place', field, id: place.id }); setField(null); }} style={[styles.row, { minHeight: 64, paddingVertical: 8 }]}><Icon name="pin" /><View style={styles.flex}><T style={styles.place}>{place.name}</T><T style={styles.caption}>{place.address}</T></View>{field && trip[field] === place.id && <Icon name="check" />}</Pressable>)}
      {!places.some(place => searchable(`${place.name} ${place.address}`).includes(searchable(query.trim()))) && <Note>Không tìm thấy. Thử “Ben Thanh” hoặc “Tao Dan”.</Note>}
    </Sheet>
    <Sheet visible={details} title="Chi tiết giá mẫu" onClose={() => setDetails(false)}><T style={styles.fare}>{money(fare.fare)}</T><T>{fare.distance} km · {fare.minutes} phút · SwiftRide Bike</T><Note>Giá cố định theo điểm đến trong demo, chưa tính quãng đường thực, thuế, phí hoặc khuyến mãi từ Matching & Pricing Service.</Note><Button title="Đã hiểu" onPress={() => setDetails(false)} /></Sheet>
  </>;
}
