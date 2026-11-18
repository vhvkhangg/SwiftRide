import { useState } from 'react';
import { ActivityIndicator, Pressable, TextInput, View } from 'react-native';
import { places } from '../domain';
import { coordinates } from '../api/trip';
import { Button, Icon, palette, styles, T } from '../ui';
import { searchPlaces } from './geo';
import type { Place } from './geo';

/** Sample places as quick picks (real coordinates in Ho Chi Minh City). */
export const quickPlaces: Place[] = places.map(place => ({ latitude: coordinates[place.id][0], longitude: coordinates[place.id][1], address: place.name }));

/** A selectable pickup/destination row; the active row receives map taps and search results. */
export function PlaceRow({ label, place, active, color, onPress }: { label: string; place: Place | null; active: boolean; color: string; onPress: () => void }) {
  return <Pressable accessibilityRole="radio" accessibilityState={{ checked: active }} accessibilityLabel={`${label}: ${place?.address ?? 'chưa chọn'}`} onPress={onPress}
    style={[styles.input, styles.row, active && { borderColor: palette.teal, backgroundColor: palette.mint }]}>
    <Icon name="pin" color={color} />
    <View style={styles.flex}><T style={styles.caption}>{label}{active ? ' · CHẠM BẢN ĐỒ ĐỂ CHỌN' : ''}</T><T style={styles.place} numberOfLines={2}>{place?.address ?? 'Chưa chọn'}</T></View>
  </Pressable>;
}

/** Address search (Nominatim, Vietnam) plus the four quick picks. */
export function PlaceSearch({ onSelect }: { onSelect: (place: Place) => void }) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<Place[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const search = async () => {
    setError('');
    setBusy(true);
    try { setResults(await searchPlaces(query)); } catch { setError('Không tìm được địa chỉ lúc này. Chạm lên bản đồ để chọn.'); } finally { setBusy(false); }
  };
  const choose = (place: Place) => { onSelect(place); setResults(null); setQuery(''); };
  return <View style={{ gap: 8 }}>
    <View style={styles.row}>
      <TextInput accessibilityLabel="Tìm địa chỉ" placeholder="Tìm địa chỉ, ví dụ: Chợ Bến Thành" placeholderTextColor={palette.muted} value={query} onChangeText={setQuery}
        onSubmitEditing={() => { if (query.trim()) void search(); }} returnKeyType="search" autoCorrect={false} style={[styles.input, styles.flex]} />
      <Button title="Tìm" variant="secondary" disabled={!query.trim()} busy={busy} onPress={search} />
    </View>
    {busy && <ActivityIndicator color={palette.teal} />}
    {!!error && <T style={[styles.caption, { color: palette.coral }]}>{error}</T>}
    {results?.length === 0 && <T style={styles.caption}>Không có kết quả. Thử tên đường hoặc địa danh khác.</T>}
    {results?.map((place, index) => <Pressable key={`${place.latitude},${place.longitude},${index}`} accessibilityRole="button" accessibilityLabel={place.address} onPress={() => choose(place)} style={[styles.row, { paddingVertical: 6 }]}>
      <Icon name="pin" /><T style={styles.flex}>{place.address}</T>
    </Pressable>)}
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
      {quickPlaces.map(place => <Pressable key={place.address} accessibilityRole="button" accessibilityLabel={`Chọn ${place.address}`} onPress={() => choose(place)}
        style={{ paddingHorizontal: 12, paddingVertical: 8, borderRadius: 12, borderWidth: 1, borderColor: palette.line, backgroundColor: palette.white }}>
        <T style={styles.caption}>{place.address}</T>
      </Pressable>)}
    </View>
    <T style={styles.caption}>Bản đồ và địa chỉ © OpenStreetMap. Đường đi từ OSRM.</T>
  </View>;
}
