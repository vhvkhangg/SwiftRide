export type Role = 'rider' | 'driver';
export type Status = 'idle' | 'quoted' | 'searching' | 'accepted' | 'enroute' | 'pickedup' | 'droppedoff' | 'payment_pending' | 'paid' | 'cancelled' | 'failed';
export type Place = { id: string; name: string; address: string; distance: string; minutes: number; fare: number };

// ponytail: four local places and fixed fares; replace with Gateway search/quote responses when BE is connected.
export const places: Place[] = [
  { id: 'university', name: 'Đại học Sài Gòn', address: '273 An Dương Vương, Quận 5', distance: '3,2', minutes: 12, fare: 45000 },
  { id: 'market', name: 'Chợ Bến Thành', address: 'Cửa Nam, đường Lê Lợi, Quận 1', distance: '3,2', minutes: 12, fare: 45000 },
  { id: 'postoffice', name: 'Bưu điện Thành phố', address: '2 Công xã Paris, Quận 1', distance: '4,1', minutes: 16, fare: 56000 },
  { id: 'park', name: 'Công viên Tao Đàn', address: 'Cổng đường Trương Định, Quận 1', distance: '2,8', minutes: 10, fare: 39000 },
];

export type Trip = {
  status: Status;
  pickup: string;
  destination: string;
  online: boolean;
  attempts: number;
  paymentAttempts: number;
  paymentError: boolean;
  tripNumber: number;
};

export const initialTrip: Trip = {
  status: 'idle', pickup: 'university', destination: '', online: true,
  attempts: 0, paymentAttempts: 0, paymentError: false, tripNumber: 1024,
};

export type Action =
  | { type: 'place'; field: 'pickup' | 'destination'; id: string }
  | { type: 'online'; value: boolean }
  | { type: 'restore'; trip: Trip }
  | { type: 'preview'; status: Status }
  | { type: 'quote' | 'book' | 'accept' | 'depart' | 'pickup' | 'dropoff' | 'pay' | 'settle' | 'retry' | 'cancel' | 'reject' | 'fail' | 'edit' | 'reset' | 'offer' | 'payment-error' };

const transitions: Partial<Record<Action['type'], [Status, Status]>> = {
  quote: ['idle', 'quoted'], book: ['quoted', 'searching'], accept: ['searching', 'accepted'],
  depart: ['accepted', 'enroute'], pickup: ['enroute', 'pickedup'], dropoff: ['pickedup', 'droppedoff'],
  pay: ['droppedoff', 'payment_pending'], settle: ['payment_pending', 'paid'], edit: ['quoted', 'idle'],
};

export function tripReducer(state: Trip, action: Action): Trip {
  if (action.type === 'restore') return action.trip;
  if (action.type === 'reset') return { ...initialTrip, online: state.online, tripNumber: state.tripNumber + 1 };
  if (action.type === 'preview') return {
    ...initialTrip, tripNumber: state.tripNumber + 1, status: action.status,
    destination: action.status === 'idle' ? '' : 'market',
    paymentAttempts: ['payment_pending', 'paid'].includes(action.status) ? 1 : 0,
  };
  if (action.type === 'place') {
    if (state.status !== 'idle' || !places.some(place => place.id === action.id)) return state;
    return { ...state, [action.field]: action.id };
  }
  if (action.type === 'online') return state.status === 'idle' ? { ...state, online: action.value } : state;
  if (action.type === 'offer') return state.online && state.status === 'idle'
    ? { ...state, status: 'searching', pickup: 'university', destination: 'market' } : state;
  if (action.type === 'cancel') return ['quoted', 'searching', 'accepted', 'enroute'].includes(state.status)
    ? { ...state, status: 'cancelled' } : state;
  if (action.type === 'reject' && state.status === 'searching') {
    const attempts = state.attempts + 1;
    return { ...state, attempts, status: attempts >= 3 ? 'failed' : 'searching' };
  }
  if (action.type === 'fail' && state.status === 'searching') return { ...state, status: 'failed' };
  if (action.type === 'payment-error' && state.status === 'payment_pending') return { ...state, paymentError: true };
  if (action.type === 'retry' && state.status === 'payment_pending' && state.paymentError && state.paymentAttempts < 3) {
    return { ...state, paymentError: false, paymentAttempts: state.paymentAttempts + 1 };
  }
  if (action.type === 'quote' && (!state.destination || state.destination === state.pickup)) return state;
  if (action.type === 'settle' && state.paymentError) return state;
  const transition = transitions[action.type];
  return transition && state.status === transition[0]
    ? { ...state, status: transition[1], paymentAttempts: action.type === 'pay' ? 1 : state.paymentAttempts } : state;
}

export const statusLabels: Record<Status, string> = {
  idle: 'Sẵn sàng', quoted: 'Báo giá', searching: 'Tìm tài xế', accepted: 'Đã nhận chuyến',
  enroute: 'Đang đi đón', pickedup: 'Đang di chuyển', droppedoff: 'Đã trả khách',
  payment_pending: 'Chờ thanh toán', paid: 'Hoàn tất', cancelled: 'Đã hủy', failed: 'Không có tài xế',
};

export function readSnapshot(raw: string | null, role: Role): Trip | null {
  try {
    const envelope = JSON.parse(raw ?? 'null');
    if (!envelope || envelope.version !== 1 || envelope.role !== role) return null;
    const value = envelope.trip;
    if (!value || !Object.hasOwn(statusLabels, value.status)) return null;
    if (!places.some(place => place.id === value.pickup)) return null;
    if (value.destination !== '' && !places.some(place => place.id === value.destination)) return null;
    if (value.status !== 'idle' && (!value.destination || value.destination === value.pickup)) return null;
    if (typeof value.online !== 'boolean' || typeof value.paymentError !== 'boolean') return null;
    if (!Number.isInteger(value.attempts) || value.attempts < 0 || value.attempts > 3) return null;
    if (!Number.isInteger(value.paymentAttempts) || value.paymentAttempts < 0 || value.paymentAttempts > 3) return null;
    if (['payment_pending', 'paid'].includes(value.status) && value.paymentAttempts < 1) return null;
    if (value.paymentError && value.status !== 'payment_pending') return null;
    if (!Number.isSafeInteger(value.tripNumber) || value.tripNumber < 1024 || value.tripNumber >= Number.MAX_SAFE_INTEGER) return null;
    return {
      status: value.status, pickup: value.pickup, destination: value.destination,
      online: value.online, attempts: value.attempts, paymentAttempts: value.paymentAttempts,
      paymentError: value.paymentError, tripNumber: value.tripNumber,
    };
  } catch { return null; }
}

export const snapshot = (trip: Trip, role: Role) => JSON.stringify({ version: 1, role, trip });
export const storageKey = (role: Role) => `swiftride-mobile-${role}-v1`;
export const validLogin = (email: string, password: string, role: Role) => email.trim().toLowerCase() === `${role}@swiftride.demo` && password === 'demo1234';
export const findPlace = (id: string) => places.find(place => place.id === id) ?? places[0];
export const fareFor = (trip: Trip) => findPlace(trip.destination || 'market');
export const tripId = (trip: Trip) => `SR-${trip.tripNumber}`;
export const paymentKey = (trip: Trip) => `demo-payment-${tripId(trip)}`;
export const money = (amount: number) => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount);
export const isActive = (trip: Trip) => !['idle', 'paid', 'cancelled', 'failed'].includes(trip.status);
