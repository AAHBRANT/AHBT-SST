// Som de confirmação da urna da CIPA: imita o "trim-trim-trim... triiim" da urna eletrônica, gerado pelo
// Web Audio (não é o arquivo do TSE). Nunca lança: sem áudio, o voto já valeu e a tela "FIM" aparece.
let contexto: AudioContext | null = null;

function ctx(): AudioContext | null {
  try {
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return null;
    contexto ??= new Ctx();
    void contexto.resume();
    return contexto;
  } catch {
    return null;
  }
}

function tom(c: AudioContext, frequencia: number, inicio: number, duracao: number, volume = 0.07) {
  const o = c.createOscillator();
  const g = c.createGain();
  o.type = 'square';
  o.frequency.value = frequencia;
  g.gain.setValueAtTime(0, inicio);
  g.gain.linearRampToValueAtTime(volume, inicio + 0.005);
  g.gain.setValueAtTime(volume, inicio + duracao - 0.01);
  g.gain.linearRampToValueAtTime(0, inicio + duracao);
  o.connect(g).connect(c.destination);
  o.start(inicio);
  o.stop(inicio + duracao + 0.01);
}

export function tocarSomFimUrna(): void {
  const c = ctx();
  if (!c) return;
  try {
    const t0 = c.currentTime;
    for (let i = 0; i < 5; i++) tom(c, i % 2 ? 2300 : 2000, t0 + i * 0.09, 0.08);
    tom(c, 2000, t0 + 0.5, 0.6);
  } catch {
    // sem áudio
  }
}

export function tocarCliqueUrna(): void {
  const c = ctx();
  if (!c) return;
  try {
    tom(c, 1500, c.currentTime, 0.05, 0.05);
  } catch {
    // sem áudio
  }
}
