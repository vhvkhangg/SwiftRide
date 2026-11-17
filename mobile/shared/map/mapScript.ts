// Leaflet controller shared by web (evaluated with the bundled `leaflet`) and native (inlined into the WebView HTML).
// Kept as plain JavaScript source because Hermes does not keep function source for `fn.toString()`.
// Signature of the body: (L, element, post) => ({ update(state) }); post({ latitude, longitude }) is called on tap.

export type LatLng = { latitude: number; longitude: number };
export type MapState = {
  pickup?: LatLng | null;
  destination?: LatLng | null;
  driver?: LatLng | null;
  /** Road geometry as [latitude, longitude] pairs; a straight dashed line is drawn when missing. */
  route?: [number, number][] | null;
  pickable?: boolean;
};

export const hoChiMinhCity: LatLng = { latitude: 10.7769, longitude: 106.7009 };

export const mapScript = `
var map = L.map(element, { zoomControl: true, attributionControl: true }).setView([${hoChiMinhCity.latitude}, ${hoChiMinhCity.longitude}], 14);
L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
}).addTo(map);
var layer = L.layerGroup().addTo(map);
var pickable = false;
var lastKey = '';
var lastPoints = [];
function frame() {
  if (lastPoints.length > 1) map.fitBounds(lastPoints, { padding: [36, 36], maxZoom: 16 });
  else if (lastPoints.length === 1) map.setView(lastPoints[0], 15);
}
// The container gets its final size after layout (and on rotation); Leaflet must be told, then re-frame the points.
if (window.ResizeObserver) new ResizeObserver(function () { map.invalidateSize(); frame(); }).observe(element);
map.on('click', function (event) {
  if (pickable) post({ latitude: event.latlng.lat, longitude: event.latlng.lng });
});
function point(p) { return [p.latitude, p.longitude]; }
function dot(p, color, label) {
  L.circleMarker(point(p), { radius: 9, color: '#ffffff', weight: 3, fillColor: color, fillOpacity: 1 })
    .bindTooltip(label, { permanent: true, direction: 'top', offset: [0, -10] }).addTo(layer);
}
return {
  update: function (state) {
    pickable = !!state.pickable;
    element.style.cursor = pickable ? 'crosshair' : '';
    layer.clearLayers();
    var points = [];
    if (state.pickup && state.destination) {
      var line = state.route && state.route.length > 1 ? state.route : [point(state.pickup), point(state.destination)];
      L.polyline(line, { color: '#087F8C', weight: 5, opacity: 0.85, dashArray: state.route && state.route.length > 1 ? null : '8 8' }).addTo(layer);
      line.forEach(function (p) { points.push(p); });
    }
    if (state.driver) { dot(state.driver, '#12383D', 'Tài xế'); points.push(point(state.driver)); }
    if (state.pickup) { dot(state.pickup, '#087F8C', 'Đón'); points.push(point(state.pickup)); }
    if (state.destination) { dot(state.destination, '#BC573E', 'Đến'); points.push(point(state.destination)); }
    // Re-fit only when the set of points changes, so the user's own pan/zoom is kept while polling.
    var key = JSON.stringify([state.pickup, state.destination, state.driver, state.route ? state.route.length : 0]);
    map.invalidateSize();
    if (key !== lastKey) { lastKey = key; lastPoints = points; frame(); }
  }
};
`;
