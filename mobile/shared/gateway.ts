export class GatewayError extends Error {
  status: number | undefined;
  constructor(message: string, status?: number) {
    super(message);
    this.name = 'GatewayError';
    this.status = status;
  }
}

export async function requestGateway(baseUrl: string, path: string, options: RequestInit = {}, timeoutMs = 15000): Promise<unknown> {
  let base: URL;
  try { base = new URL(baseUrl); } catch { throw new GatewayError('Cấu hình Gateway không hợp lệ.'); }
  if (!['http:', 'https:'].includes(base.protocol) || base.username || base.password || base.search || base.hash || !path.startsWith('/') || path.startsWith('//') || path.includes('\\')) throw new GatewayError('Cấu hình Gateway không hợp lệ.');
  const url = new URL(`${base.href.replace(/\/$/, '')}${path}`);
  if (url.origin !== base.origin) throw new GatewayError('Đường dẫn phải thuộc Gateway đã cấu hình.');
  const controller = new AbortController();
  const abort = () => controller.abort();
  options.signal?.addEventListener('abort', abort, { once: true });
  if (options.signal?.aborted) controller.abort();
  const timer = setTimeout(abort, timeoutMs);
  try {
    const response = await fetch(url.href, { ...options, signal: controller.signal });
    if (!response.ok) throw new GatewayError(response.status === 401 ? 'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.' : response.status === 403 ? 'Tài khoản không có quyền thực hiện thao tác này.' : `Gateway trả lỗi HTTP ${response.status}. Kiểm tra trạng thái trước khi gửi lại.`, response.status);
    if (response.status === 204) return null;
    try { return await response.json(); } catch (error) {
      if (controller.signal.aborted) throw error;
      throw new GatewayError('Phản hồi Gateway không phải JSON hợp lệ.');
    }
  } catch (error) {
    if (error instanceof GatewayError) throw error;
    throw new GatewayError(controller.signal.aborted ? 'Yêu cầu bị hủy hoặc hết thời gian chờ. Chưa xác định kết quả; kiểm tra trạng thái trước khi thử lại.' : 'Không kết nối được Gateway. Kiểm tra mạng; không tự tạo lại chuyến hoặc thanh toán.');
  } finally {
    clearTimeout(timer);
    options.signal?.removeEventListener('abort', abort);
  }
}
