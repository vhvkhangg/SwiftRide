import { useEffect, useRef } from 'react';
import type { ComponentType, Ref } from 'react';
import { View } from 'react-native';
import { WebView } from 'react-native-webview';
import type { WebViewMessageEvent } from 'react-native-webview';
import { mapScript } from './mapScript';
import type { LatLng, MapState } from './mapScript';
import { mapStyles } from './mapStyles';

type WebViewHandle = { injectJavaScript: (script: string) => void };
// react-native-webview ships class typings that do not line up with React 19's JSX types; describe the props we use.
const NativeWebView = WebView as unknown as ComponentType<{
  ref?: Ref<WebViewHandle>; originWhitelist: string[]; source: { html: string; baseUrl: string }; onMessage: (event: WebViewMessageEvent) => void;
  javaScriptEnabled: boolean; setSupportMultipleWindows: boolean; style: object;
}>;

export type LiveMapProps = MapState & { onPick?: (point: LatLng) => void; height?: number; label: string };

// Leaflet 1.9.4 from unpkg, pinned with Subresource Integrity (hashes match the npm package used on web).
const html = `<!doctype html><html><head><meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1">
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" integrity="sha256-p4NxAoJBhIIN+hmNHrzRCf9tD/miZyoHS5obTRR9BMY=" crossorigin="">
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js" integrity="sha256-20nQCchB9co0qIjJZRGuk2/Z9VM+kNiyxNV1lvTlZBo=" crossorigin=""></script>
<style>html,body,#map{margin:0;height:100%;background:#E9EEE5}</style></head><body><div id="map"></div><script>
var controller = (function (L, element, post) {${mapScript}})(L, document.getElementById('map'), function (p) { window.ReactNativeWebView.postMessage(JSON.stringify(p)); });
window.__update = function (state) { controller.update(state); };
window.ReactNativeWebView.postMessage(JSON.stringify({ ready: true }));
</script></body></html>`;

/** Native: the same Leaflet controller inside a WebView; state goes in with injectJavaScript, taps come back as messages. */
export default function LiveMap({ onPick, height = 260, label, ...state }: LiveMapProps) {
  const web = useRef<WebViewHandle>(null);
  const ready = useRef(false);
  const latest = useRef<MapState>(state);
  latest.current = { ...state, pickable: !!onPick };
  const push = () => { if (ready.current) web.current?.injectJavaScript(`window.__update(${JSON.stringify(latest.current)});true;`); };

  useEffect(push);

  const onMessage = (event: WebViewMessageEvent) => {
    let data: unknown;
    try { data = JSON.parse(event.nativeEvent.data); } catch { return; }
    if (data && typeof data === 'object' && 'ready' in data) { ready.current = true; push(); return; }
    const point = data as LatLng;
    if (Number.isFinite(point?.latitude) && Number.isFinite(point?.longitude)) onPick?.(point);
  };

  return <View accessibilityRole="image" accessibilityLabel={label} style={[mapStyles.map, { height }]}>
    <NativeWebView ref={web} originWhitelist={['*']} source={{ html, baseUrl: 'https://swiftride-mobile.local/' }} onMessage={onMessage} javaScriptEnabled
      setSupportMultipleWindows={false} style={{ flex: 1, backgroundColor: 'transparent' }} />
  </View>;
}
