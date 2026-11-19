// Leitura mínima de EXIF (só JPEG): data original e GPS gravados pela câmera do celular. Usada na foto
// de ocorrência enviada de arquivo (18/11/2026) — o que vier daqui é informado como "lido da foto",
// nunca como comprovado. Foto repassada por WhatsApp ou print de tela não traz nada: devolve vazio.
// Precisa ser lido do arquivo ORIGINAL: a compressão por canvas (comprimirImagem) descarta o EXIF.

export interface ExifFoto {
  capturadaEm?: string; // ISO 8601 em UTC
  fusoMinutos?: number; // mesmo sentido de Date.getTimezoneOffset() (UTC-3 → 180)
  latitude?: number;
  longitude?: number;
}

const TAG_EXIF_IFD = 0x8769;
const TAG_GPS_IFD = 0x8825;
const TAG_DATA_ORIGINAL = 0x9003;
const TAG_FUSO_ORIGINAL = 0x9011;

export function lerExifJpeg(buffer: ArrayBuffer): ExifFoto {
  try {
    const v = new DataView(buffer);
    if (v.byteLength < 4 || v.getUint16(0) !== 0xffd8) return {};
    let pos = 2;
    while (pos + 4 <= v.byteLength) {
      const marcador = v.getUint16(pos);
      if ((marcador & 0xff00) !== 0xff00 || marcador === 0xffda) break; // início da imagem: sem EXIF
      const tamanho = v.getUint16(pos + 2);
      if (marcador === 0xffe1 && pos + 10 <= v.byteLength && v.getUint32(pos + 4) === 0x45786966 && v.getUint16(pos + 8) === 0) {
        return lerTiff(v, pos + 10);
      }
      pos += 2 + tamanho;
    }
  } catch {
    // Arquivo truncado ou EXIF malformado: segue sem os dados, a foto entra assim mesmo.
  }
  return {};
}

function lerTiff(v: DataView, base: number): ExifFoto {
  const le = v.getUint16(base) === 0x4949;
  const u16 = (o: number) => v.getUint16(base + o, le);
  const u32 = (o: number) => v.getUint32(base + o, le);
  if (u16(2) !== 42) return {};

  function entradas(ifd: number) {
    const mapa = new Map<number, { tipo: number; qtd: number; valor: number }>();
    const n = u16(ifd);
    for (let i = 0; i < n; i++) {
      const e = ifd + 2 + i * 12;
      const tipo = u16(e + 2), qtd = u32(e + 4);
      const tamanho = qtd * ({ 1: 1, 2: 1, 3: 2, 4: 4, 5: 8, 7: 1, 9: 4, 10: 8 } as Record<number, number>)[tipo];
      mapa.set(u16(e), { tipo, qtd, valor: tamanho > 4 ? u32(e + 8) : e + 8 });
    }
    return mapa;
  }
  const texto = (e?: { qtd: number; valor: number }) => {
    if (!e) return undefined;
    let s = '';
    for (let i = 0; i < e.qtd; i++) { const c = v.getUint8(base + e.valor + i); if (!c) break; s += String.fromCharCode(c); }
    return s.trim() || undefined;
  };
  const graus = (e?: { qtd: number; valor: number }) => {
    if (!e || e.qtd < 3) return undefined;
    const r = (i: number) => { const d = u32(e.valor + i * 8 + 4); return d ? u32(e.valor + i * 8) / d : NaN; };
    const g = r(0) + r(1) / 60 + r(2) / 3600;
    return Number.isFinite(g) ? g : undefined;
  };

  const resultado: ExifFoto = {};
  const ifd0 = entradas(u32(4));

  const exifPtr = ifd0.get(TAG_EXIF_IFD);
  if (exifPtr) {
    const exif = entradas(u32(exifPtr.valor));
    const data = texto(exif.get(TAG_DATA_ORIGINAL));
    const fuso = texto(exif.get(TAG_FUSO_ORIGINAL));
    Object.assign(resultado, interpretarData(data, fuso));
  }

  const gpsPtr = ifd0.get(TAG_GPS_IFD);
  if (gpsPtr) {
    const gps = entradas(u32(gpsPtr.valor));
    const lat = graus(gps.get(2)), lon = graus(gps.get(4));
    const refLat = texto(gps.get(1)), refLon = texto(gps.get(3));
    // 0,0 é o que alguns aparelhos gravam sem sinal de GPS: tratado como ausência.
    if (lat != null && lon != null && (lat !== 0 || lon !== 0) && lat <= 90 && lon <= 180) {
      resultado.latitude = refLat === 'S' ? -lat : lat;
      resultado.longitude = refLon === 'W' ? -lon : lon;
    }
  }
  return resultado;
}

// "2026:11:16 07:42:10" + fuso opcional "-03:00". Sem fuso, vale o fuso do aparelho que está enviando.
export function interpretarData(data?: string, fuso?: string): Pick<ExifFoto, 'capturadaEm' | 'fusoMinutos'> {
  const m = data?.match(/^(\d{4}):(\d{2}):(\d{2}) (\d{2}):(\d{2}):(\d{2})/);
  if (!m) return {};
  const [ano, mes, dia, h, mi, s] = m.slice(1).map(Number);
  if (!ano || mes < 1 || mes > 12 || dia < 1 || dia > 31) return {};
  const f = fuso?.match(/^([+-])(\d{2}):(\d{2})$/);
  if (f) {
    const deslocamento = (f[1] === '-' ? 1 : -1) * (Number(f[2]) * 60 + Number(f[3]));
    const utc = Date.UTC(ano, mes - 1, dia, h, mi, s) + deslocamento * 60_000;
    return { capturadaEm: new Date(utc).toISOString(), fusoMinutos: deslocamento };
  }
  const local = new Date(ano, mes - 1, dia, h, mi, s);
  if (Number.isNaN(local.getTime())) return {};
  return { capturadaEm: local.toISOString(), fusoMinutos: local.getTimezoneOffset() };
}
