export type Role = 'rider' | 'driver';
export type Status = 'idle' | 'quoted' | 'searching' | 'accepted' | 'enroute' | 'pickedup' | 'droppedoff' | 'payment_pending' | 'paid' | 'cancelled' | 'failed';
export type Place = { id: string; name: string; address: string; district: string; position: [number, number]; distance: string; minutes: number; fare: number };

export const places: Place[] = [
  { id: 'university', name: 'Đại học Sài Gòn', address: '273 An Dương Vương, Phường Chợ Quán', district: 'Khu vực Quận 5', position: [490, 420], distance: '3,2', minutes: 12, fare: 45000 },
  { id: 'market', name: 'Chợ Bến Thành', address: 'Cửa Nam, đường Lê Lợi', district: 'Khu vực Quận 1', position: [835, 320], distance: '3,2', minutes: 12, fare: 45000 },
  { id: 'postoffice', name: 'Bưu điện Thành phố', address: '2 Công xã Paris, Phường Sài Gòn', district: 'Khu vực Quận 1', position: [940, 170], distance: '4,1', minutes: 16, fare: 56000 },
  { id: 'park', name: 'Công viên Tao Đàn', address: 'Cổng đường Trương Định', district: 'Khu vực Quận 1', position: [695, 190], distance: '2,8', minutes: 10, fare: 39000 },
];

export type DemoState = {
  role: Role | null;
  status: Status;
  pickup: string;
  destination: string;
  online: boolean;
  paymentError: boolean;
  attempts: number;
  tripNumber: number;
};

export const initialState: DemoState = {
  role: null, status: 'idle', pickup: 'university', destination: '',
  online: true, paymentError: false, attempts: 0, tripNumber: 1024,
};

export type Action =
  | { type: 'role'; role: Role | null }
  | { type: 'place'; field: 'pickup' | 'destination'; id: string }
  | { type: 'quote' | 'book' | 'accept' | 'depart' | 'pickup' | 'dropoff' | 'pay' | 'settle' | 'retry' | 'cancel' | 'reject' | 'fail' | 'edit' | 'reset' | 'offer' | 'payment-error' }
  | { type: 'online'; value: boolean }
  | { type: 'preview'; role: Role; status: Status };

const transitions: Partial<Record<Action['type'], [Status, Status]>> = {
  book: ['quoted', 'searching'], accept: ['searching', 'accepted'], depart: ['accepted', 'enroute'],
  pickup: ['enroute', 'pickedup'], dropoff: ['pickedup', 'droppedoff'], pay: ['droppedoff', 'payment_pending'],
  settle: ['payment_pending', 'paid'], edit: ['quoted', 'idle'],
};

export function demoReducer(state: DemoState, action: Action): DemoState {
  if (action.type === 'role') return { ...state, role: action.role };
  if (action.type === 'reset') return { ...initialState, role: state.role, tripNumber: state.tripNumber + 1 };
  if (action.type === 'preview') return { ...initialState, role: action.role, status: action.status, destination: action.status === 'idle' ? '' : 'market' };
  if (action.type === 'place') {
    if (state.status !== 'idle' || !places.some(place => place.id === action.id)) return state;
    return { ...state, [action.field]: action.id };
  }
  if (action.type === 'quote') {
    if (state.status !== 'idle' || !state.destination || state.pickup === state.destination) return state;
    return { ...state, status: 'quoted' };
  }
  if (action.type === 'online') return { ...state, online: action.value };
  if (action.type === 'offer') {
    if (!state.online || !['idle', 'quoted'].includes(state.status)) return state;
    return { ...state, status: 'searching', pickup: 'university', destination: 'market', attempts: 0 };
  }
  if (action.type === 'cancel' && ['quoted', 'searching', 'accepted', 'enroute'].includes(state.status)) return { ...state, status: 'cancelled' };
  if (action.type === 'reject' && state.status === 'searching') {
    const attempts = state.attempts + 1;
    return { ...state, attempts, status: attempts >= 3 ? 'failed' : 'searching' };
  }
  if (action.type === 'fail' && state.status === 'searching') return { ...state, status: 'failed' };
  if (action.type === 'payment-error' && state.status === 'payment_pending') return { ...state, paymentError: true };
  if (action.type === 'retry' && state.status === 'payment_pending') return { ...state, paymentError: false };
  const transition = transitions[action.type];
  if (transition && state.status === transition[0]) {
    if (action.type === 'settle' && state.paymentError) return state;
    return { ...state, status: transition[1] };
  }
  return state;
}

export const storageKey = 'swiftride-ui-v1';
const statuses: Status[] = ['idle', 'quoted', 'searching', 'accepted', 'enroute', 'pickedup', 'droppedoff', 'payment_pending', 'paid', 'cancelled', 'failed'];

export function readSnapshot(raw: string | null): DemoState {
  try {
    const value = JSON.parse(raw ?? 'null');
    if (!value || ![null, 'rider', 'driver'].includes(value.role) || !statuses.includes(value.status)) return initialState;
    if (!places.some(place => place.id === value.pickup) || !(value.destination === '' || places.some(place => place.id === value.destination))) return initialState;
    if (value.status !== 'idle' && (!value.destination || value.pickup === value.destination)) return initialState;
    if (typeof value.online !== 'boolean' || typeof value.paymentError !== 'boolean') return initialState;
    if (!Number.isInteger(value.attempts) || value.attempts < 0 || value.attempts > 3) return initialState;
    if (!Number.isSafeInteger(value.tripNumber) || value.tripNumber < 1024) return initialState;
    return { role: value.role, status: value.status, pickup: value.pickup, destination: value.destination, online: value.online, paymentError: value.paymentError, attempts: value.attempts, tripNumber: value.tripNumber };
  } catch { return initialState; }
}

export const money = (amount: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
export const findPlace = (id: string) => places.find(place => place.id === id) ?? places[0];
export const quoteFor = (state: DemoState) => findPlace(state.destination || 'market');
export const tripId = (state: DemoState) => `SR-${state.tripNumber}`;
export const paymentKey = (state: DemoState) => `demo-payment-${tripId(state)}`;

export const screens: { code: string; label: string; role: Role; status: Status }[] = [
  { code: 'R01', label: 'Chọn hành trình', role: 'rider', status: 'idle' },
  { code: 'R02', label: 'Xác nhận giá', role: 'rider', status: 'quoted' },
  { code: 'R03', label: 'Tìm tài xế', role: 'rider', status: 'searching' },
  { code: 'R04', label: 'Theo dõi chuyến', role: 'rider', status: 'enroute' },
  { code: 'R05', label: 'Thanh toán', role: 'rider', status: 'payment_pending' },
  { code: 'R06', label: 'Chuyến hoàn tất', role: 'rider', status: 'paid' },
  { code: 'D01', label: 'Trang chủ tài xế', role: 'driver', status: 'idle' },
  { code: 'D02', label: 'Lời mời nhận chuyến', role: 'driver', status: 'searching' },
  { code: 'D03', label: 'Đi đón khách', role: 'driver', status: 'accepted' },
  { code: 'D04', label: 'Thực hiện chuyến', role: 'driver', status: 'pickedup' },
  { code: 'D05', label: 'Kết quả chuyến', role: 'driver', status: 'payment_pending' },
];
