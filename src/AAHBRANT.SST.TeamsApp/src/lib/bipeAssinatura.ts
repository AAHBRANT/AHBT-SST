// Bipes gerados pelo Web Audio — sem arquivo de som. "Assinatura aceita" é chamado depois que o backend
// confirma a assinatura/presença, por digital ou facial. Nenhum bipe lança:
// se o navegador bloquear o áudio (autoplay) ou não houver saída de som, a assinatura já valeu e o
// aviso visual continua na tela.
let contexto: AudioContext | null = null;

function tocarTons(tons: { frequencia: number; atraso: number }[], duracao: number): void {
  try {
    const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return;
    contexto ??= new Ctx();
    const ctx = contexto;
    void ctx.resume();

    const inicio = ctx.currentTime;
    tons.forEach(({ frequencia, atraso }) => {
      const oscilador = ctx.createOscillator();
      const ganho = ctx.createGain();
      oscilador.type = 'sine';
      oscilador.frequency.value = frequencia;
      ganho.gain.setValueAtTime(0.0001, inicio + atraso);
      ganho.gain.exponentialRampToValueAtTime(0.25, inicio + atraso + 0.02);
      ganho.gain.exponentialRampToValueAtTime(0.0001, inicio + atraso + duracao - 0.02);
      oscilador.connect(ganho).connect(ctx.destination);
      oscilador.start(inicio + atraso);
      oscilador.stop(inicio + atraso + duracao);
    });
  } catch {
    // sem áudio disponível — segue sem bipe
  }
}

// Bipe de "assinatura aceita": dois tons subindo.
export function tocarBipeAssinaturaAceita(): void {
  tocarTons([{ frequencia: 880, atraso: 0 }, { frequencia: 1320, atraso: 0.14 }], 0.18);
}

// Bipe de "leitura da digital concluída, pode retirar o dedo": um tom curto e mais grave, diferente do
// de assinatura aceita. No cadastro, avisa quem está no leitor quando tirar o dedo para a 2ª leitura.
export function tocarBipeLeituraDigital(): void {
  tocarTons([{ frequencia: 660, atraso: 0 }], 0.25);
}
