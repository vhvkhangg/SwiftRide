import { useEffect, useReducer, useRef, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { initialTrip, readSnapshot, snapshot, storageKey, tripReducer } from './domain';
import type { Role } from './domain';

const saveError = 'Không lưu được tiến trình. Giữ ứng dụng mở và thử thao tác lại.';

export function useTrip(role: Role) {
  const [trip, dispatch] = useReducer(tripReducer, initialTrip);
  const [ready, setReady] = useState(false);
  const [storageError, setStorageError] = useState('');
  const [reload, setReload] = useState(0);
  const [canSave, setCanSave] = useState(false);
  const writes = useRef(Promise.resolve());
  useEffect(() => {
    let mounted = true;
    AsyncStorage.getItem(storageKey(role)).then(raw => {
      if (!mounted) return;
      const restored = readSnapshot(raw, role);
      dispatch({ type: 'restore', trip: restored ?? initialTrip });
      setStorageError(raw && !restored ? 'Dữ liệu demo cũ không hợp lệ. Đã mở chuyến mới.' : '');
      setCanSave(true);
      setReady(true);
    }).catch(() => {
      if (mounted) { setStorageError('Không đọc được dữ liệu. Thử lại để tránh ghi đè chuyến đã lưu.'); setReady(true); }
    });
    return () => { mounted = false; };
  }, [role, reload]);

  useEffect(() => {
    if (!ready || !canSave) return;
    writes.current = writes.current.then(() => AsyncStorage.setItem(storageKey(role), snapshot(trip, role)))
      .then(() => { setStorageError(error => error === saveError ? '' : error); })
      .catch(() => { setStorageError(saveError); });
  }, [trip, ready, canSave, role]);

  return { trip, dispatch, ready, canSave, storageError, retryLoad: () => { setReady(false); setReload(value => value + 1); } };
}
