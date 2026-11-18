import { useEffect, useState } from 'react';
import { findRoute } from './geo';
import type { Route } from './geo';
import type { LatLng } from './mapScript';

/** Road route between two points, recomputed only when a point actually moves. */
export function useRoute(from: LatLng | null, to: LatLng | null): Route | null {
  const [route, setRoute] = useState<Route | null>(null);
  const key = from && to ? `${from.latitude},${from.longitude};${to.latitude},${to.longitude}` : '';
  useEffect(() => {
    if (!from || !to) { setRoute(null); return; }
    let current = true;
    setRoute(null);
    void findRoute(from, to).then(result => { if (current) setRoute(result); });
    return () => { current = false; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  return route;
}
