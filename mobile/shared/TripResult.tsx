import { ActivityIndicator, View } from 'react-native';
import type { TripScreenProps } from './MobileApp';
import { fareFor, money, paymentKey, tripId } from './domain';
import { Button, Card, Demo, Eyebrow, Icon, Note, palette, Person, Route, styles, T, Title } from './ui';

export function Terminal({ trip, dispatch, driver = false }: TripScreenProps & { driver?: boolean }) {
  const paid = trip.status === 'paid';
  return <View style={{ gap: 16, paddingTop: 24 }}><Card>
    <View style={styles.center}><View style={[styles.heroIcon, !paid && { backgroundColor: palette.sand }]}><Icon name={paid ? 'check' : 'warning'} size={34} color={paid ? palette.teal : palette.coral} /></View>
      <Eyebrow>{tripId(trip)}</Eyebrow><Title>{paid ? (driver ? 'Một hành trình tốt.' : 'Bạn đã đến nơi.') : trip.status === 'cancelled' ? 'Chuyến đã hủy' : driver ? 'Chưa có chuyến phù hợp' : 'Chưa tìm được tài xế'}</Title>
      <T style={[styles.muted, { textAlign: 'center' }]}>{paid ? 'Cảm ơn bạn đã đồng hành cùng SwiftRide.' : trip.status === 'cancelled' ? 'Chuyến demo đã dừng. Không có giao dịch phát sinh.' : driver ? 'Bạn đã từ chối các lời mời mẫu. Về trang chủ để nhận chuyến khác.' : 'Kịch bản tìm tài xế đã kết thúc. Bạn có thể tạo chuyến mới.'}</T>
    </View>
    {paid && <><View style={styles.divider} /><Route trip={trip} /><Person driver={!driver} /><View style={styles.divider} /><Eyebrow>{driver ? 'GIÁ TRỊ CHUYẾN DEMO' : 'ĐÃ THANH TOÁN MÔ PHỎNG'}</Eyebrow><T style={styles.fare}>{money(fareFor(trip).fare)}</T><Note>Thanh toán đã hoàn tất trong kịch bản demo.</Note></>}
    <Button title={driver ? 'Về trang chủ' : 'Đặt chuyến mới'} icon="arrow" onPress={() => dispatch({ type: 'reset' })} />
  </Card></View>;
}

export function Payment({ trip, dispatch, driver = false }: TripScreenProps & { driver?: boolean }) {
  const pending = trip.status === 'payment_pending';
  const exhausted = trip.paymentAttempts >= 3 && trip.paymentError;
  return <View style={{ gap: 16, paddingTop: 24 }}><Card>
    <Eyebrow>{tripId(trip)} · ĐÃ TRẢ KHÁCH</Eyebrow><Title>{driver ? 'Cảm ơn một hành trình tốt.' : 'Hoàn tất thanh toán'}</Title>
    <Route trip={trip} /><View style={styles.divider} /><T style={styles.caption}>TỔNG TIỀN MẪU</T><T style={styles.fare}>{money(fareFor(trip).fare)}</T>
    <View style={styles.row}><Icon name="card" /><View style={styles.flex}><T style={styles.place}>Ví mô phỏng SwiftRide</T><T style={styles.caption}>Không liên kết tài khoản hoặc thẻ thật</T></View></View>
    {!pending && <Note>{driver ? 'Chờ hành khách thanh toán. Hoàn thành chặng đường không có nghĩa đã nhận tiền.' : 'Chuyến đi đã kết thúc. Bấm bên dưới để mô phỏng thanh toán.'}</Note>}
    {pending && (trip.paymentError ? <Note warning>{exhausted ? 'Đã hết 3 lần thử. Giao dịch vẫn chờ xử lý, không báo thành công hoặc tạo giao dịch mới.' : 'Chưa xác định được kết quả. Kiểm tra lại cùng giao dịch để tránh trừ tiền lặp.'}</Note> : <View style={styles.row}><ActivityIndicator color={palette.teal} /><T style={styles.flex}>Đang chờ kết quả mô phỏng...</T></View>)}
    {pending && <T selectable style={styles.caption}>Mã giao dịch: {paymentKey(trip)} · Lần {trip.paymentAttempts}/3</T>}
    {!driver && !pending && <Button title="Thanh toán mô phỏng" icon="arrow" onPress={() => dispatch({ type: 'pay' })} />}
    {!driver && pending && trip.paymentError && <Button title="Kiểm tra lại thanh toán" disabled={exhausted} onPress={() => dispatch({ type: 'retry' })} />}
  </Card>
    <Demo><T style={styles.caption}>Chọn phản hồi hệ thống. Không thực hiện thu tiền thật.</T>
      {driver && !pending && <Button title="Mô phỏng bắt đầu thanh toán" variant="secondary" onPress={() => dispatch({ type: 'pay' })} />}
      {pending && !trip.paymentError && <><Button title="Mô phỏng thanh toán thành công" onPress={() => dispatch({ type: 'settle' })} /><Button title="Mô phỏng lỗi thanh toán" variant="secondary" onPress={() => dispatch({ type: 'payment-error' })} /></>}
      {driver && pending && trip.paymentError && !exhausted && <Button title="Mô phỏng kiểm tra lại" variant="secondary" onPress={() => dispatch({ type: 'retry' })} />}
      {exhausted && <T style={styles.caption}>Dùng Kịch bản demo để bắt đầu ví dụ khác. Khi nối BE, kết quả cần được đối soát từ Payment Service.</T>}
      {!pending && !driver && <T style={styles.caption}>Điều khiển kết quả xuất hiện sau khi bắt đầu thanh toán.</T>}
    </Demo>
  </View>;
}
