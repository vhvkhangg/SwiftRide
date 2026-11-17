export type Role = 'rider' | 'driver';
export type Place = { id: string; name: string; address: string };

// Four sample places in Ho Chi Minh City (no map/geocoding service). Coordinates live in api/trip.ts.
export const places: Place[] = [
  { id: 'university', name: 'Đại học Sài Gòn', address: '273 An Dương Vương, Quận 5' },
  { id: 'market', name: 'Chợ Bến Thành', address: 'Cửa Nam, đường Lê Lợi, Quận 1' },
  { id: 'postoffice', name: 'Bưu điện Thành phố', address: '2 Công xã Paris, Quận 1' },
  { id: 'park', name: 'Công viên Tao Đàn', address: 'Cổng đường Trương Định, Quận 1' },
];

export const findPlace = (id: string) => places.find(place => place.id === id);
/** Trip addresses are the place names the app sent; unknown addresses map to ''. */
export const placeIdOf = (address: string) => places.find(place => place.name === address)?.id ?? '';
