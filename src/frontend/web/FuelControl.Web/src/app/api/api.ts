import createClient, { type Middleware } from 'openapi-fetch';
import type { paths } from './schema';

/** Server xatosi (ProblemDetails) — `detail` foydalanuvchiga ko'rsatiladi. */
export class ApiXato extends Error {
  /** ProblemDetails.extensions (masalan, `kerakliAparatlar`). */
  constructor(public readonly status: number, xabar: string, public readonly qoshimcha: Record<string, unknown> = {}) { super(xabar); }
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

const yarat = (baseUrl: string) => {
  const c = createClient<paths>({ baseUrl });
  c.use(auth);
  return c;
};

/**
 * API klienti. Standart — shu domen. Ilova boshlanishida sozlama.json'dan o'qilgan manzil bilan `apiManzilOrnat` qayta yaratadi
 * (ESM live binding: `api` ni import qilganlar har chaqiruvda joriy qiymatni ko'radi — doim `api.GET(...)` shaklida ishlating).
 */
export let api = yarat(location.origin);

/** Boshqa domendagi API (Cloudflare Pages) — `""` bo'lsa shu domen. */
export function apiManzilOrnat(asos: string) {
  api = yarat(asos || location.origin);
}

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
