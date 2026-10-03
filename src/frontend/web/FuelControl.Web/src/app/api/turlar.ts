// OpenAPI'dan generatsiya qilingan sxemadan (schema.d.ts, `npm run api`) qulay tur nomlari.
import type { components } from './schema';

type Sxema = components['schemas'];

export type Ruxsat = Sxema['Ruxsat'];
export type Rol = Sxema['Rol'];
export type TolovTuri = Sxema['TolovTuri'];
export type HarakatTuri = Sxema['HarakatTuri'];
/** API'da `guruh` oddiy satr (katta-kichik harfga bog'liq emas): operator | kun | oy. */
export type HisobotGuruhi = 'Operator' | 'Kun' | 'Oy';

export type Foydalanuvchi = Sxema['FoydalanuvchiDto'];
export type LoginJavobi = Sxema['LoginJavobiDto'];
export type Aparat = Sxema['AparatDto'];
export type Yoqilgi = Sxema['YoqilgiTuriDto'];
export type Sotuv = Sxema['SotuvDto'];
export type Tolov = Sxema['TolovDto'];
export type SotuvYaratish = Sxema['SotuvYaratishDto'];
export type Smena = Sxema['SmenaDto'];
export type SmenaTafsilot = Sxema['SmenaTafsilotDto'];
export type BoshqaruvBugun = Sxema['BoshqaruvBugunDto'];
export type Hisobot = Sxema['HisobotDto'];
export type HisobotQatori = Sxema['HisobotQatoriDto'];
export type AuditYozuvi = Sxema['AuditYozuviDto'];
export type HisobHarakati = Sxema['HisobHarakatiDto'];
export type OperatorHisob = Sxema['OperatorHisobDto'];
export type NarxTarixi = Sxema['NarxTarixiDto'];
export type ZaxiraJavobi = Sxema['ZaxiraJavobiDto'];
