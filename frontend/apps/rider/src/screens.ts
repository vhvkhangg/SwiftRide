import type { DemoScreen } from '../../../shared/lib/demo.ts';

export const screens: DemoScreen[] = [
  { code: 'R01', label: 'Chọn hành trình', status: 'idle' },
  { code: 'R02', label: 'Xác nhận giá', status: 'quoted' },
  { code: 'R03', label: 'Tìm tài xế', status: 'searching' },
  { code: 'R04', label: 'Theo dõi chuyến', status: 'enroute' },
  { code: 'R05', label: 'Thanh toán', status: 'payment_pending' },
  { code: 'R06', label: 'Chuyến hoàn tất', status: 'paid' },
];
