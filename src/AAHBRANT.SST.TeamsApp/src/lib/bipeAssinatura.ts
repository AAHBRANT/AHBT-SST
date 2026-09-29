// Bipe curto de "assinatura aceita" (dois tons subindo), gerado pelo Web Audio — sem arquivo de som.
// Chamado depois que o backend confirma a assinatura/presença, por digital ou facial. Nunca lança:
// se o navegador bloquear o áudio (autoplay) ou não houver saída de som, a assinatura já valeu e o
// aviso visual continua na tela.
let contexto: AudioContext | null = null;

export function tocarBipeAssinaturaAceita(): void {
  try {
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    contexto ??= new Ctx();
    const ctx = contexto;
    void ctx.resume();

    const inicio = ctx.currentTime;
    [
      { frequencia: 880, atraso: 0 },
      { frequencia: 1320, atraso: 0.14 },
    ].forEach(({ frequencia, atraso }) => {
      const oscilador = ctx.createOscillator();
      const ganho = ctx.createGain();
      oscilador.type = 'sine';
      oscilador.frequency.value = frequencia;
      ganho.gain.setValueAtTime(0.0001, inicio + atraso);
      ganho.gain.exponentialRampToValueAtTime(0.25, inicio + atraso + 0.02);
      ganho.gain.exponentialRampToValueAtTime(0.0001, inicio + atraso + 0.16);
      oscilador.connect(ganho).connect(ctx.destination);
      oscilador.start(inicio + atraso);
      oscilador.stop(inicio + atraso + 0.18);
    });
  } catch {
    // sem áudio disponível — segue sem bipe
  }
}
