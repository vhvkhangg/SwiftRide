// Pure helpers for API mode (no React Native imports, so node tests can load them).

// Trip status is serialized as a number by Trip Service (see docs/mobile-api-contract.md).
export const tripStatus = ['Requested', 'PriceConfirmed', 'DriverAccepted', 'EnRoute', 'PickedUp', 'DroppedOff', 'PaymentPending', 'Paid', 'Cancelled', 'Failed'] as const;
export type TripStatusName = typeof tripStatus[number] | 'Unknown';

export const statusText: Record<TripStatusName, string> = {
  Requested: 'Chờ xác nhận giá', PriceConfirmed: 'Đang tìm tài xế', DriverAccepted: 'Tài xế đã nhận',
  EnRoute: 'Tài xế đang đến', PickedUp: 'Đang di chuyển', DroppedOff: 'Đã trả khách',
  PaymentPending: 'Đang thanh toán', Paid: 'Đã thanh toán', Cancelled: 'Đã hủy', Failed: 'Không thành công', Unknown: 'Đang đồng bộ',
};

/** Unknown values map to 'Unknown' so the UI keeps polling instead of assuming success. */
export function statusName(value: unknown): TripStatusName {
  if (typeof value === 'number') return tripStatus[value] ?? 'Unknown';
  if (typeof value === 'string') return (tripStatus as readonly string[]).includes(value) ? value as TripStatusName : 'Unknown';
  return 'Unknown';
}

export const isTerminal = (status: TripStatusName) => status === 'Paid' || status === 'Cancelled' || status === 'Failed';
export const riderCanCancel = (status: TripStatusName) => ['Requested', 'PriceConfirmed', 'DriverAccepted', 'EnRoute'].includes(status);

export type TripDto = {
  id: string; riderId: string; driverId: string | null; pickupAddress: string; dropoffAddress: string;
  fareEstimate: number | null; status: number | string; createdAt: string; updatedAt: string | null;
  paymentFailureReason?: string | null; declineCount?: number; declineThreshold?: number | null;
  pickupLatitude?: number | null; pickupLongitude?: number | null; dropoffLatitude?: number | null; dropoffLongitude?: number | null;
};
export type QuoteDto = { base: number; distance: number; time: number; surge: number; toll: number; discount: number; tax: number; total: number };
export type InvitationDto = {
  id: string; expiresAtUtc: string; pickupAddress: string; dropoffAddress: string; fareEstimate: number | null;
  pickupLatitude?: number | null; pickupLongitude?: number | null; dropoffLatitude?: number | null; dropoffLongitude?: number | null;
};

type WithPoints = { pickupLatitude?: number | null; pickupLongitude?: number | null; dropoffLatitude?: number | null; dropoffLongitude?: number | null };
const latLng = (latitude?: number | null, longitude?: number | null) =>
  typeof latitude === 'number' && typeof longitude === 'number' && Number.isFinite(latitude) && Number.isFinite(longitude) ? { latitude, longitude } : null;
/** Pickup/dropoff points of a trip or invitation; null when the server did not store them (older trips). */
export const pointsOf = (value: WithPoints) => ({
  pickup: latLng(value.pickupLatitude, value.pickupLongitude),
  destination: latLng(value.dropoffLatitude, value.dropoffLongitude),
});
export type PaymentDto = { id: string; tripId: string; amount: number; status: string; createdAt: string; updatedAt: string | null };

// Coordinates of the sample places (WGS84). Trip/Matching need pickup coordinates to find drivers.
export const coordinates: Record<string, [number, number]> = {
  university: [10.7596, 106.6822], market: [10.7725, 106.6980], postoffice: [10.7798, 106.6999], park: [10.7745, 106.6922],
};

/** Great-circle distance in km. */
export function haversineKm([lat1, lon1]: [number, number], [lat2, lon2]: [number, number]) {
  const rad = (deg: number) => deg * Math.PI / 180;
  const a = Math.sin(rad(lat2 - lat1) / 2) ** 2 + Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(rad(lon2 - lon1) / 2) ** 2;
  return 6371 * 2 * Math.asin(Math.sqrt(a));
}

/** Road estimate from straight-line distance: ×1.3 detour, 22 km/h city speed, at least 1 minute. */
export function estimateRoute(from: [number, number], to: [number, number]) {
  const distance = Math.round(haversineKm(from, to) * 1.3 * 10) / 10;
  return { distance, estimatedTime: Math.max(1, Math.round(distance / 22 * 60)) };
}

/** Prices are shown exactly as Pricing returns them (VND); the UI never rescales them. */
export const priceText = (value: number | null | undefined) =>
  value == null ? '—' : `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 }).format(value)} ₫`;

export type Claims = { sub: string; email: string; name: string; roles: string[]; exp: number };

/** Reads (does not verify) the access token payload; the Gateway and services verify the signature. */
export function readClaims(token: string): Claims | null {
  try {
    const part = token.split('.')[1];
    const base64 = part.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(part.length / 4) * 4, '=');
    const json = decodeURIComponent(Array.from(atob(base64), c => `%${c.charCodeAt(0).toString(16).padStart(2, '0')}`).join(''));
    const payload = JSON.parse(json);
    if (typeof payload.sub !== 'string' || typeof payload.exp !== 'number') return null;
    const roles = Array.isArray(payload.role) ? payload.role.filter((r: unknown) => typeof r === 'string') : typeof payload.role === 'string' ? [payload.role] : [];
    return { sub: payload.sub, email: payload.email ?? '', name: payload.name ?? payload.preferred_username ?? payload.email ?? '', roles, exp: payload.exp };
  } catch { return null; }
}

export const shortId = (id: string | null | undefined) => id ? id.slice(0, 8).toUpperCase() : '—';
