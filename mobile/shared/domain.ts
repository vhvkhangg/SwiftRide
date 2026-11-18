export type Role = 'rider' | 'driver';
export type Place = { id: string; name: string; address: string };

// Four sample places in Ho Chi Minh City, offered as quick picks next to map tap and address search.
// Coordinates live in api/trip.ts.
export const places: Place[] = [
  { id: 'university', name: 'Đại học Sài Gòn', address: '273 An Dương Vương, Quận 5' },
  { id: 'market', name: 'Chợ Bến Thành', address: 'Cửa Nam, đường Lê Lợi, Quận 1' },
  { id: 'postoffice', name: 'Bưu điện Thành phố', address: '2 Công xã Paris, Quận 1' },
  { id: 'park', name: 'Công viên Tao Đàn', address: 'Cổng đường Trương Định, Quận 1' },
];
