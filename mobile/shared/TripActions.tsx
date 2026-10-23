import type { Role } from './domain';
import type { TripScreenProps } from './MobileApp';
import { Button } from './ui';

export default function TripActions({ role, trip, dispatch, confirm }: TripScreenProps & { role: Role }) {
  if (['paid', 'cancelled', 'failed'].includes(trip.status)) return <Button title={role === 'driver' ? 'Về trang chủ' : 'Đặt chuyến mới'} icon="arrow" onPress={() => dispatch({ type: 'reset' })} />;
  if (role === 'rider') {
    if (trip.status === 'idle') return <Button title="Xem giá chuyến đi" icon="arrow" disabled={!trip.destination || trip.destination === trip.pickup} onPress={() => dispatch({ type: 'quote' })} />;
    if (trip.status === 'quoted') return <Button title="Xác nhận đặt xe" icon="arrow" onPress={() => dispatch({ type: 'book' })} />;
    if (trip.status === 'droppedoff') return <Button title="Thanh toán mô phỏng" icon="arrow" onPress={() => dispatch({ type: 'pay' })} />;
    if (trip.status === 'payment_pending' && trip.paymentError) return <Button title="Kiểm tra lại thanh toán" disabled={trip.paymentAttempts >= 3} onPress={() => dispatch({ type: 'retry' })} />;
  } else {
    if (trip.status === 'searching') return <Button title="Nhận chuyến" icon="check" onPress={() => dispatch({ type: 'accept' })} />;
    if (trip.status === 'accepted') return <Button title="Bắt đầu đi đón" icon="arrow" onPress={() => dispatch({ type: 'depart' })} />;
    if (trip.status === 'enroute') return <Button title="Đã đón khách" icon="check" onPress={() => confirm('Đã đón khách?', 'Xác nhận hành khách đã lên xe. Chuyến sẽ chuyển sang đang di chuyển.', () => dispatch({ type: 'pickup' }))} />;
    if (trip.status === 'pickedup') return <Button title="Đã trả khách" icon="check" onPress={() => confirm('Đã trả khách?', 'Xác nhận khách đã đến nơi. Thanh toán được xử lý riêng, chưa coi là đã thu tiền.', () => dispatch({ type: 'dropoff' }))} />;
  }
  return null;
}
