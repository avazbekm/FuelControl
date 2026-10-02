import createClient, { type Middleware } from 'openapi-fetch';
import type { paths } from './schema';

/** Server xatosi (ProblemDetails) — `detail` foydalanuvchiga ko'rsatiladi. */
export class ApiXato extends Error {
  constructor(public readonly status: number, xabar: string) { super(xabar); }
}

/** Tarmoq xatosi (internet yo'q / server javob bermadi). */
export class AloqaXato extends Error {}

let tokenOl: () => string | null = () => null;
let ruxsatsiz: () => void = () => {};

/** Auth xizmati o'zini shu yerda ro'yxatdan o'tkazadi (aylanma bog'liqlikdan qochish uchun). */
export function apiSozla(token: () => string | null, chiqar: () => void) {
  tokenOl = token;
  ruxsatsiz = chiqar;
}

const auth: Middleware = {
  onRequest({ request }) {
    const t = tokenOl();
    if (t) request.headers.set('Authorization', `Bearer ${t}`);
    return request;
  },
  onResponse({ response, request }) {
    if (response.status === 401 && !request.url.endsWith('/auth/login')) ruxsatsiz();
    return response;
  },
};

export const api = createClient<paths>({ baseUrl: location.origin });
api.use(auth);

type Natija<D> = { data?: D; error?: unknown; response: Response };

/** openapi-fetch natijasini ochadi: muvaffaqiyatda ma'lumot, aks holda ApiXato/AloqaXato. */
export async function ol<D, T = D>(sorov: Promise<Natija<D>>): Promise<T> {
  let n: Natija<D>;
  try {
    n = await sorov;
  } catch {
    throw new AloqaXato('Aloqa yo\'q');
  }
  if (n.response.ok) return n.data as unknown as T;
  const e = n.error as { detail?: string; title?: string } | string | undefined;
  const xabar = typeof e === 'string' ? e : e?.detail ?? e?.title ?? `HTTP ${n.response.status}`;
  throw new ApiXato(n.response.status, xabar);
}
