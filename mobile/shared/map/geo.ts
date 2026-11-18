// OpenStreetMap services used by the apps (no API key):
// - Nominatim for address search / reverse geocoding (usage policy: at most 1 request per second, user-triggered only).
// - OSRM demo server for the road route, distance and duration sent to Pricing.
// Every call has a timeout and a fallback, so a slow or blocked service never blocks booking.
import { estimateRoute } from '../api/trip';
import type { LatLng } from './mapScript';

export type Place = LatLng & { address: string };
export type Route = { distance: number; estimatedTime: number; path: [number, number][] | null; source: 'osrm' | 'estimate' };

const nominatim = 'https://nominatim.openstreetmap.org';
const osrm = 'https://router.project-osrm.org';
// Native apps must identify themselves to Nominatim. Browsers send their own User-Agent, and a custom one
// would only add a CORS preflight, so it is set on React Native only.
const native = typeof navigator !== 'undefined' && navigator.product === 'ReactNative';
const headers: Record<string, string> = { Accept: 'application/json', 'Accept-Language': 'vi', ...(native ? { 'User-Agent': 'SwiftRide-student-app/1.0' } : {}) };

async function getJson(url: string, timeoutMs = 10000): Promise<unknown> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    const response = await fetch(url, { headers, signal: controller.signal });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return await response.json();
  } finally { clearTimeout(timer); }
}

export const coordinateText = (p: LatLng) => `${p.latitude.toFixed(5)}, ${p.longitude.toFixed(5)}`;

/** Short Vietnamese label from a Nominatim result: place name + street + ward/district, max 200 chars. */
export function shortAddress(result: { name?: string; display_name?: string; address?: Record<string, string> }): string {
  const a = result.address ?? {};
  const street = [a.house_number, a.road].filter(Boolean).join(' ');
  const area = a.suburb ?? a.quarter ?? a.city_district ?? a.district ?? a.city;
  const parts = [result.name, street, area].filter((part, index, all) => part && all.indexOf(part) === index);
  const text = parts.length ? parts.join(', ') : (result.display_name ?? '');
  return text.slice(0, 200);
}

export async function reverseGeocode(point: LatLng): Promise<string> {
  try {
    const data = await getJson(`${nominatim}/reverse?format=jsonv2&addressdetails=1&zoom=18&lat=${point.latitude}&lon=${point.longitude}`) as Parameters<typeof shortAddress>[0];
    return shortAddress(data) || coordinateText(point);
  } catch { return coordinateText(point); }
}

/** Search limited to Vietnam, biased to Ho Chi Minh City. */
export async function searchPlaces(query: string): Promise<Place[]> {
  const q = encodeURIComponent(query.trim());
  if (!q) return [];
  const data = await getJson(`${nominatim}/search?format=jsonv2&addressdetails=1&limit=5&countrycodes=vn&viewbox=106.55,10.90,106.85,10.68&q=${q}`) as { lat: string; lon: string }[];
  return (Array.isArray(data) ? data : []).map(item => ({
    latitude: Number(item.lat), longitude: Number(item.lon), address: shortAddress(item as Parameters<typeof shortAddress>[0]),
  })).filter(place => Number.isFinite(place.latitude) && Number.isFinite(place.longitude));
}

/** Road route from OSRM; falls back to the straight-line estimate (×1.3, 22 km/h) when OSRM is unavailable. */
export async function findRoute(from: LatLng, to: LatLng): Promise<Route> {
  try {
    const data = await getJson(`${osrm}/route/v1/driving/${from.longitude},${from.latitude};${to.longitude},${to.latitude}?overview=full&geometries=geojson`) as
      { routes?: { distance: number; duration: number; geometry: { coordinates: [number, number][] } }[] };
    const route = data.routes?.[0];
    if (!route) throw new Error('no route');
    return {
      distance: Math.max(0.1, Math.round(route.distance / 100) / 10),
      estimatedTime: Math.max(1, Math.round(route.duration / 60)),
      path: route.geometry.coordinates.map(([longitude, latitude]) => [latitude, longitude]),
      source: 'osrm',
    };
  } catch {
    return { ...estimateRoute([from.latitude, from.longitude], [to.latitude, to.longitude]), path: null, source: 'estimate' };
  }
}
