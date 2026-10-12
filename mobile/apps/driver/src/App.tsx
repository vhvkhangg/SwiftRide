import MobileApp from '../../../shared/MobileApp';
import type { Screen } from '../../../shared/MobileApp';
import Driver from './Driver';

const screens: Screen[] = [
  { code: 'D01', label: 'Trang chủ tài xế', status: 'idle' },
  { code: 'D02', label: 'Lời mời nhận chuyến', status: 'searching' },
  { code: 'D03', label: 'Đi đón khách', status: 'accepted' },
  { code: 'D04', label: 'Thực hiện chuyến', status: 'pickedup' },
  { code: 'D05', label: 'Kết quả chuyến', status: 'payment_pending' },
];

export default function App() { return <MobileApp role="driver" screens={screens} renderTrip={props => <Driver {...props} />} />; }
