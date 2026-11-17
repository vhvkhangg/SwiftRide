import { useEffect, useRef } from 'react';
import { GatewayError } from '../gateway';

/** Runs task now and every ms while enabled; skips a tick if the previous one is still running. */
export function usePoll(task: () => Promise<void>, ms: number, enabled: boolean) {
  const saved = useRef(task);
  saved.current = task;
  useEffect(() => {
    if (!enabled) return;
    let running = false;
    const tick = async () => {
      if (running) return;
      running = true;
      try { await saved.current(); } catch { /* task reports its own errors */ } finally { running = false; }
    };
    void tick();
    const timer = setInterval(tick, ms);
    return () => clearInterval(timer);
  }, [enabled, ms]);
}

export const errorText = (error: unknown) =>
  error instanceof GatewayError ? (error.status === 409 ? 'Trạng thái chuyến vừa thay đổi. Đã tải lại dữ liệu mới nhất.' : error.message)
    : error instanceof Error ? error.message : 'Có lỗi không xác định.';

export const isNotFound = (error: unknown) => error instanceof GatewayError && error.status === 404;
