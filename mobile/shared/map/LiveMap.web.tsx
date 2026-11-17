import { useEffect, useRef } from 'react';
import type { ComponentRef } from 'react';
import { View } from 'react-native';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { mapScript } from './mapScript';
import type { LatLng, MapState } from './mapScript';
import { mapStyles } from './mapStyles';

type Controller = { update: (state: MapState) => void };
export type LiveMapProps = MapState & { onPick?: (point: LatLng) => void; height?: number; label: string };

/** Web: Leaflet from npm, rendered into a plain DOM node. */
export default function LiveMap({ onPick, height = 260, label, ...state }: LiveMapProps) {
  const host = useRef<ComponentRef<typeof View>>(null);
  const controller = useRef<Controller | null>(null);
  const pick = useRef(onPick);
  pick.current = onPick;

  useEffect(() => {
    const element = host.current as unknown as HTMLElement;
    const container = document.createElement('div');
    container.style.width = '100%';
    container.style.height = '100%';
    element.appendChild(container);
    // eslint-disable-next-line no-new-func
    controller.current = new Function('L', 'element', 'post', mapScript)(L, container, (p: LatLng) => pick.current?.(p)) as Controller;
    return () => { controller.current = null; container.remove(); };
  }, []);

  useEffect(() => { controller.current?.update({ ...state, pickable: !!onPick }); });

  return <View ref={host} accessibilityRole="image" accessibilityLabel={label} style={[mapStyles.map, { height }]} />;
}
